# Architecture Rules

> Working document — the design rules the current code and the workfiles already follow, but that
> `RULES.md` does not state yet: code architecture and recurring UX conventions.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

`RULES.md` states feature rules — effects, toolbars, handles, global effects, playback, undo,
components, icons… — and, case by case, many "one definition, read there" rules. What the app does
consistently **across** features — how the code is split, who owns the state, how background work
and native resources are handled, how files are written, how feedback reaches the user — lives only
in the code and in the workfiles.

This work **writes those rules down**, so every future change follows them.

Agreed at scoping (see Q&A 1–4):

- **Both kinds** of rules: code architecture **and** UX conventions not yet written.
- **Descriptive, with gaps reported**: a rule is written only when the code already follows it;
  where the code has exceptions, they are listed in § Known Gaps below. **No refactoring** in this
  task — the deliverable is documentation only.
- **Location**: decided once the candidate rules are known (Open Question).

Evidence: a read-only exploration of `src/ImageGridFusion/` and `workfiles/` (Iteration 1),
re-checked on 2026-09-30 (Iteration 2) and on 2026-11-04 (Iteration 3). File references are as of
commit `35417b7`, all under `src/ImageGridFusion/`.

---

## Candidate Rules — Code Architecture

### A1 — The WinForms Boundary

- **No WinForms type outside `UI/`**: no `System.Windows.Forms` in `Composition/`, `Imaging/`,
  `Explorer/`, `Web/` or `Components/`, neither as `using` nor fully qualified — zero exception.
- **`Components/` is reached from the top only**: `UI/` and `Program.cs` reference it, nothing else.
- `Program.cs` (root namespace) is the entry point: the single-instance lock, the pending component
  swaps, `ErrorLog`, then `MainForm` / `TrayApplicationContext`.
- One project, so any layering is a **convention**, not enforced by assemblies.

The folder layering drafted in Iteration 1 (Composition and Explorer at the base, Imaging above
Composition, UI on top) **no longer holds**: the graph now has cycles (§ Known Gaps). Whether to
write it anyway as a target is an Open Question.

### A2 — Immutable State, Mutable Resources

- Settings and composed state are **immutable**: `sealed record`s changed with `with` expressions,
  never setters — `ImageLook` and every effect record, `GridBorders`, `Separator`, `GlobalState`,
  `GridLayers`, `Layer`, `LayerPlacement`, `CutoutEffect`, `UpscaleEffect`, `LayerTextStyle`,
  `NamedTextStyle`. Small value types are `readonly record struct` (`Frame`, `CellState`,
  `CutoutPoint`, `LayerFrame`…). A snapshot with hand-written equality is a `sealed class` with
  get-only properties (`GridState`, `PeopleState`).
- A **mutable class** is reserved for a **resource holder**: `SourceImage` owns its bitmap, its
  `PageSource` and its caches — the cut-out masks and result, the upscaled bitmaps and pages, the
  reduced preview copy — swapping and disposing them (`ShowPage`, `SetCut`, `SetUpscaled`); its
  `Look` is only replaced as a whole value. `GridLayout` is a class for its static catalog,
  effectively immutable (`With*` returns new instances).

### A3 — State Ownership and Flow

- **`GridPreview` owns what is in the grid**: the cells' images and looks (`_images`), the layout
  and separators, the layers (`_layers`, `SetLayers`). `MainForm` holds no image list; it reads
  `Images`, `SelectedImage`, `ActiveLayout`, `Layers`.
- **`MainForm` owns the global effects** (`_soundtrack`, `_fade`, `_seams`, `_cascade`, `_borders`
  and their on flags) and the app-level state; it pushes what the preview draws down to it
  (`Borders`, the seams). `GridState` / `GlobalState` are **snapshots** built by
  `MainForm.CaptureState`, owned by the undo history — never a live owner.
- **Down** (MainForm → GridPreview): methods for per-image changes (`SetSelectedLook`), properties
  for grid-wide settings.
- **Up**: C# events named after **what happened** — `XChanged` for state (`ImagesChanged`,
  `SelectedImageChanged`, `LayoutChanged`, `RatioChanged`, `PlaybackChanged`), `XClicked` /
  `XRequested` for an intent (`AddImagesClicked`, `UpRequested`), the gesture's name for a gesture
  leaving the control (`SwapDragMoved`, `ReleasedOffGrid`).
- The **effects lifecycle** of § Effects › Scope and State is implemented in **one place**,
  `GridPreview`'s `RemoveAt` / `Replace` / `Swap`; every route into a cell goes through
  `GridPreview.Add` (already stated by § Preview Playback and § Arrival Zoom).

### A4 — One Rendering Path

The pattern behind § Effects › Rendering, § The Cutout Exception, § Layers, § Cell Copy and
§ On-Cell Helper Indicators, stated once for the whole grid:

- **Every pixel that can reach an output is drawn by `Compositor`** (`Draw`, `DrawCell`,
  `DrawLayers`, `Render`, `Cells`…): the preview, the still Copy / Save, the cell copy and every
  animated export call it — no second drawing of the grid anywhere.
- What only the preview shows — the checkerboard, outlines, drag visuals, helper indicators, the
  crop edit view, the Pick view, the badges — is drawn by `GridPreview` **over** a cached
  `Compositor` bitmap, never in `Compositor`.
- What is too slow for a frame — a cut-out, an enlargement — is computed **outside `Compositor`**
  and handed to it as the image's bitmap (`SourceImage.DrawnBitmap` / `DrawnPages`).

### A5 — Background Work

- Long work — exports, scans, content passes, component installs, cut-outs, enlargements — runs in
  **`Task.Run`**, awaited from the UI thread, and reports through a **`Progress<T>`** created on
  the UI thread, or through `Completed` / `Failed` events raised after an `await`.
- It is cancelled by a **`CancellationToken`** checked once per unit of work (frame, file, tile),
  or, for a request bound to an image, by **staleness** — the image disposed or its look changed
  before its turn (`CutoutService`).
- Results come back by `await` continuations; an explicit `BeginInvoke` only for a callback with
  no captured context (`ThumbnailGrid.cs:2229`, `GridPreview.Eyedropper.cs:513`). **Nothing blocks
  the UI thread** on a task (`.Result`, `.Wait()`): the `GetAwaiter().GetResult()` calls live in
  worker-only code (`ContentExtractor`, `PdfPages`, `VideoFrames`).
- **No partial file survives**: a cancelled or failed export deletes its output (`GridExport`, a
  `finally` while `!finished`); a download writes to a partial file or a staging folder and moves
  it into place on success (`Web/Downloader`, `UpscaleCache`, `ComponentInstaller`).
- **While exporting**, the export keeps the settings it started with (already stated for the
  toolbars and the undo history).

### A6 — Native Resources

- **Media Foundation objects live on the thread pool**, released there too —
  `Task.Run(reader.Dispose)` (`AnimationPlayer.cs:700`) — so a release never blocks the UI thread.
- Every `Bitmap`, reader, sample and frame is **disposed by its owner**, in a `finally` or a
  `Dispose` chain (already required per frame by § Export Memory).
- A **resident model** (ONNX session) is **released after 5 minutes unused**, its use serialized
  by a gate taken without waiting at release time (`CutoutService`, `UpscaleService`, the text
  encoder of `FileExplorerPanel.Vision`).
- Native interop sits next to its feature: Media Foundation, GIF, shell thumbnails and
  DirectWrite / Direct2D in `Imaging/`; the analysis models in `Explorer/` and `Imaging/`; small
  `user32` / `gdi32` window helpers in `UI/`. The Iteration 1 claim "isolated in `Imaging/`" no
  longer holds and is dropped.

### A7 — Code Style

- File-scoped namespaces everywhere; nullable enabled (`.csproj`), no `#nullable` directive.
- Classes `sealed` by default, `abstract` for a base (`PageSource`, `AnimationReader`,
  `ComponentSource`, `ThumbnailStrip`, `SelectionStrip`); `static` classes for stateless
  computations (`Compositor`, `FitCalculator`, `CanvasSizer`, `GridExport`…).
- **One main type per file**, named after it, its small companion types beside it.
- A **large control is split into partial files by feature**, named `<Type>.<Feature>.cs`
  (`GridPreview.Cutout.cs`, `.Eyedropper`, `.Layers`, `.Upscale`; `MainForm.Components.cs`,
  `.Layers`; `FileExplorerPanel.Vision.cs`); `partial` is otherwise only for source generators.

### A8 — One Definition, Read Everywhere

- A value several places need — the preview, the exports, a readout, the gestures — is **computed
  in one member** and read there, never re-derived; RULES.md names the member and says "a new
  consumer reads it there too".
- RULES.md already applies it **case by case** — § Output Format (`CanvasRatio`), § Video Length,
  § Played Part, § Cascade, § Transport (`GridTime`), § The Crop Exception (`ImageLook.Shown`),
  § Rendering (`ZoomIn`), § The Cutout Exception (`DrawnBitmap`), § Effect Presets
  (`LookPreset.Saved`)… This rule states the pattern once, so a new shared value follows it before
  it has a section of its own.

---

## Candidate Rules — UX Conventions

### U1 — Files the App Writes

Where the settings live is already a rule (§ App Settings). What no section says yet:

- **Every data file next to the exe is written atomically**: to `<file>.tmp`, then
  `File.Move(overwrite: true)` — `settings.json`, `favorites.json`, `people.json`,
  `text-styles.json`, `components.json`, every file of `index\` (`AppSettings.cs:112`,
  `Favorites.cs:295`, `People.cs:225`, `TextStyles.cs:169`, `ComponentManifest.cs:186`,
  `FileIndex.cs:124`, `ContentIndex.cs:336`, `VisionIndex.cs:136`, `FaceIndex.cs:168`).
- **What is generated goes under `%TEMP%\ImageGridFusion\`**, in a folder per feature, emptied at
  the next start-up by the normal instance only — files in use left for later.
- **A failed save says so in the status line** and keeps the value for the session — stated for the
  settings only today, applied to every save (`IsSaveError` filters, `MainForm.cs:5817`).

### U2 — Status Line

- The outcome of an action — information, progress, error — goes to the **status line** of the
  window or panel where it happened, and **stays until the next message** (no timer); errors in red
  (`MainForm.ShowStatus`, `FileExplorerPanel.ShowStatus`).
- A **message box** is used only to **confirm** an action that removes something (forget faces,
  delete a preset, uninstall, close while installing…), for the errors of a modal window without a
  status line (the Component updates window), and by `ErrorLog` for what nothing caught. The
  Iteration 1 claim "no message box anywhere" no longer holds.

### U4 — Clipboard

- A Copy puts **one content in several formats**, so the target app picks what it supports: bitmap
  + `PNG` (a still, `MainForm.PutStillOnClipboard`), file + raw `GIF` (an animated GIF), file +
  bitmap (a cell copy, the JPEG for sharing); every paste reads `PNG` first (`ClipboardImage`) —
  the last two already in § Cell Copy.

### U5 — DPI

- Every size in pixels is a **logical** size, scaled with `LogicalToDeviceUnits` (~28 UI files),
  the main window in `AutoScaleMode.Dpi`. **Weak**: ~31 literal sizes are left unscaled
  (§ Known Gaps).

### U6 — Ctrl + Wheel on Sliders

- **Every option slider is a `StepSlider`** (`MainForm.OptionSlider`, `ColorsSlider`,
  `CutoutSlider`): the wheel alone moves it as a `TrackBar`; **with Ctrl**, onto the **next multiple
  of its `ControlStep`** — 5 % by default (`WheelSteps.Percent`) — one step per notch, or its own
  `ControlWheel` for a slider whose value is not the unit it shows.
- Ctrl is read from the **wheel message itself** (`WheelSteps.WithControl`), so it works whatever
  window has the focus.
- The file explorer's tile size slider is no option: there, Ctrl + wheel over the tiles sizes them
  (Glossary › Tile size).

### U7 — "…" Opens a Window

- A label ending with **`…`** opens a dialog or a window before acting (*Save GIF…*, *Browse…*,
  *Rename…*, *Component updates…*); a label without it acts at once.

### Not Retained

| Candidate | Why not |
|---|---|
| U3 — progress as `Label… value` in the status line | Only the export does it; the indexing and content passes have their own bars |
| Image provenance label reused across features | One workfile only (`20260926-source-file-name.md`) |
| Registry vs file for the settings | Settled by § App Settings |
| "COM isolated in `Imaging/`" (old A6) | No longer true: models in `Explorer/`, window helpers and a shell COM import in `UI/` |

---

## Known Gaps

Exceptions the code shows against the rules above. Reported only — **no refactoring** here.

| Rule | Gap | Where |
|---|---|---|
| A1 | **The folders reference each other in cycles**: Composition → Imaging; Imaging → Explorer (the ONNX runtime's resolver lives in `Explorer/OcrEngineFiles`); Explorer → Imaging, Explorer → Web; Web → UI (only `MainForm.TempVideoFolder` and `AppSettings.VideoMaxSide`) | `Composition/GridLayers.cs:2`, `Imaging/CutoutEngine.cs:179`, `Explorer/ContentExtractor.cs:3`, `Explorer/DirectEngineDownload.cs:2`, `Web/UrlDownloads.cs:19`, `Web/YtDlp.cs:138` |
| A2 | `SourceImage` exposes public setters besides `Look`: `TextOrigin`, `Dropped`, `PickEmbedding` | `Composition/SourceImage.cs:53`, `:56`, `:182` |
| A3 | The Borders exist **twice**: `MainForm._borders` and `GridPreview`'s render copy, pushed by the `Borders` property | `UI/MainForm.cs:449`, `UI/GridPreview.cs:102` |
| A5 | The Component updates window checks its cancellation only at the end of a step | `UI/ComponentsWindow.cs:473`, `:495` |
| A6 | The fire-and-forget `Task.Run(reader.Dispose)` leaves a failing `Dispose` unobserved | `UI/AnimationPlayer.cs:700` |
| A6 | The 5-minute idle release is written three times, no shared helper | `UI/CutoutService.cs:17-31`, `UI/UpscaleService.cs:19-42`, `UI/FileExplorerPanel.Vision.cs:492` |
| A7 | `UrlLoadException` is neither `sealed` nor `abstract` | `Web/UrlLoadStep.cs:22` |
| U1 | Written in place, not atomically: the user's tag vocabulary template, a pasted favorite | `Explorer/VisionTags.cs:109`, `Explorer/PastedFavorites.cs:68` |
| U4 | The file explorer builds its own bitmap + `PNG` instead of `PutStillOnClipboard` | `UI/FileExplorerPanel.cs:1253` |
| U5 | ~31 literal sizes in `UI/` without `LogicalToDeviceUnits`, e.g. `OptionSlider`'s `Size(160, 26)` | `UI/MainForm.cs:3618` |
| — | `MainForm` and `GridPreview` (their partial files included) hold ~14 000 of the ~55 000 lines; no rule broken, noted for context | — |

---

## Test Impact

Nothing testable changes: the deliverable is documentation only (`RULES.md` and/or a new file,
`GLOSSARY.md`, possibly `CLAUDE.md`). The app has no test project anyway.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (documentation only) | — | — |

---

## Open Questions

- [ ] Which candidate rules are kept (A1–A8, U1, U2, U4–U7)?
- [ ] Where do they go: all in `RULES.md`, or the code architecture (A1–A8) in a new
  `ARCHITECTURE.md` imported by `CLAUDE.md`, the UX conventions in `RULES.md`?
- [ ] A1: write only the WinForms boundary (what holds), or also the folder layering as a target,
  its cycles listed as known exceptions?
- [ ] A4 and A8 state the pattern behind existing sections: add them beside, the sections left as
  they are, or shorten the sections around them?
- [x] ~~Ctrl + wheel 5 % steps: written now as a rule (the code catching up when its workfile is
  implemented), or left out until it is built?~~ → Moot: implemented on 2026-09-30, now candidate
  U6 like the others (Iteration 2)
- [ ] Are § Known Gaps also recorded in the rules file (a "Known exceptions" line under each rule),
  or only in this workfile?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-27

Scoping settled the nature (code + UX), the stance (descriptive, gaps reported, no refactoring)
and left the location to the proposal. Exploration: five read-only agents, a scout pass then a
deepening pass each — layers, state model, rendering / export pipeline, persistence, gestures /
feedback / clipboard — plus direct checks (types per file, the Ctrl + wheel workfile's status, the
explorer's own status line).

Result: 7 code-architecture candidates (A1–A7), 5 UX candidates (U1–U5), 3 candidates set aside
(not built or not recurring), 4 known gaps.

### Iteration 2 — 2026-09-30

The open questions were never answered (the session stopped while they were asked, then the
second asking was declined). Meanwhile ~45 commits landed on `main`, so the candidates were
re-checked against the code before asking again:

- **U1 rewritten**: RULES.md § App Settings now puts the settings in `settings.json` next to the
  exe and removes the registry — the registry / JSON contradiction is settled there. U1 keeps only
  what that section does not say: the atomic writes and the temp folder swept at start-up.
- **U6 added**: Ctrl + wheel 5 % steps are implemented (`StepSlider`, `WheelSteps`, the cell zoom);
  its "not built" row and its Open Question go.
- **A8 added**: "one definition, read everywhere", the pattern RULES.md now states case by case
  (§ Video Length, § The Crop Exception).
- **A3 corrected**: `SoundOnArrival` no longer exists (the Volume exception was reduced); the
  gesture events `SwapDragMoved` / `ReleasedOffGrid` noted beside `XChanged` / `XClicked`.
- A1, A2, A4, A5, A7, U2–U5 re-checked, unchanged; the known gaps are still there, their line
  numbers updated.

### Iteration 3 — 2026-11-04

The questions stayed unanswered for five weeks; ~1 200 commits later (the code grew from ~15 000
to ~55 000 lines, with `Web/`, `Components/`, partial classes, ONNX engines) the user chose to
re-check the candidates before deciding (Q&A 10). Four read-only agents, one targeted pass each —
a scout pass was not needed, each candidate already naming its evidence:

- **A1 reduced to the WinForms boundary**: the folders now reference each other in cycles, so the
  layering can no longer be written as a description; the cycles go to § Known Gaps, and writing
  the layering as a target becomes an Open Question.
- **A2 widened**: the new state types follow it; `GridState` / `PeopleState` are immutable classes,
  `SourceImage` holds the cut-out and upscale caches.
- **A3 rewritten**: `GridPreview` owns the cells and the layers, `MainForm` the global effects;
  `GridState` / `GlobalState` are snapshots; the events also use `XRequested`.
- **A4 widened** to the cut-outs, enlargements and layers; **A8** now points at a dozen sections.
- **A5**: staleness as a second way to cancel; the partial-file rule extended to downloads.
- **A6 reduced**: the "COM isolated in `Imaging/`" claim is dropped; the 5-minute idle release of
  the models added.
- **A7**: the partial files by feature added.
- **U1** extended to every data file and to failed saves; **U2** corrected — message boxes are
  used for confirmations, a modal window's errors and `ErrorLog`; **U3 dropped** (the export only);
  **U4** mostly in § Cell Copy already; **U5** kept but marked weak; **U7 added** (`…` labels).
- § Known Gaps rebuilt from the agents' findings: 11 rows.
- The earlier open questions (Q&A 5–7, 9) are superseded by Q&A 11–15.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | Not applicable — documentation only |
| Unit tests | | | Not applicable — no test project, nothing testable changes |
| Rules (`RULES.md` / new file) | | | |
| Glossary | | | |
| README | | | Not applicable — the README describes the app's features, not its design rules |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which kind of rules: code architecture, UX conventions, or both? | Both | 2026-09-27 |
| 2 | Only what the code already does, also a target it does not meet everywhere, or descriptive with the gaps reported? | Descriptive, gaps reported in the workfile, no refactoring | 2026-09-27 |
| 3 | Where do the rules live: `RULES.md` or a separate `ARCHITECTURE.md`? | "It depends on what you propose" — decided after the proposal | 2026-09-27 |
| 4 | Exploration depth: straightforward or tricky / long? | Tricky / long — scout pass then deepening | 2026-09-27 |
| 5 | Which candidate rules are kept (A1–A8, U1–U6)? | Never answered — superseded by Q&A 11 (Iteration 3) | 2026-11-04 |
| 6 | Where do they go: all in `RULES.md`, or A1–A8 in `ARCHITECTURE.md` and U1–U6 in `RULES.md`? | Never answered — superseded by Q&A 12 (Iteration 3) | 2026-11-04 |
| 7 | A4: rewrite the existing Rendering / Helper Indicators rules around it, or add it beside them? | Never answered — superseded by Q&A 14 (Iteration 3) | 2026-11-04 |
| 8 | Ctrl + wheel 5 % steps: a rule now, or left out until built? | Never answered — moot, implemented meanwhile (Iteration 2) | 2026-09-30 |
| 9 | Known gaps: also in the rules file, or only in this workfile? | Never answered — superseded by Q&A 15 (Iteration 3) | 2026-11-04 |
| 10 | The workfile is five weeks behind the code: re-check it (iteration 3), close it, or start over? | Iteration 3 | 2026-11-04 |
| 11 | Which candidate rules are kept (A1–A8, U1, U2, U4–U7)? | | |
| 12 | Where do they go: all in `RULES.md`, or A1–A8 in `ARCHITECTURE.md` and the UX ones in `RULES.md`? | | |
| 13 | A1: the WinForms boundary only, or also the folder layering as a target with its cycles as exceptions? | | |
| 14 | A4 and A8: added beside the existing sections, or the sections shortened around them? | | |
| 15 | Known gaps: also in the rules file, or only in this workfile? | | |

---

*Last updated: 2026-11-04*
