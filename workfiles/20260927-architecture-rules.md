# Architecture Rules

> Working document — the design rules the current code and the workfiles already follow, but that
> `RULES.md` does not state yet: code architecture and recurring UX conventions.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

`RULES.md` covers the effects, their toolbars, the on-cell handles, the global effects and the
helper indicators. Everything else the app does consistently — how the code is layered, who owns
the state, how background work reports back, where settings are stored, how feedback reaches the
user — lives only in the code and scattered across ~60 workfiles.

This work **writes those rules down**, so every future change follows them.

Agreed at scoping (see Q&A 1–4):

- **Both kinds** of rules: code architecture **and** UX conventions not yet written.
- **Descriptive, with gaps reported**: a rule is written only when the code already follows it;
  where the code has exceptions, they are listed in § Known Gaps below. **No refactoring** in this
  task — the deliverable is documentation only.
- **Location**: decided once the candidate rules are known (Open Question).

Evidence comes from a read-only exploration of `src/ImageGridFusion/` and `workfiles/`
(Iteration 1), re-checked against the code on 2026-09-30 (Iteration 2). File references are as of
commit `72d53d4`.

---

## Candidate Rules — Code Architecture

### A1 — Layers

| Layer | Folder / namespace | Holds | May reference |
|---|---|---|---|
| Composition | `Composition/` | The grid's model and its rendering: layouts, effects, looks, `Compositor` | `System.Drawing` only |
| Explorer | `Explorer/` | File index, search, favorites | Nothing of the app |
| Imaging | `Imaging/` | Reading files (images, video, GIF, PDF, text, HTML, RTF), encoders, exports | Composition |
| UI | `UI/` | WinForms controls and forms, app settings, tray | Composition, Imaging, Explorer |
| Entry point | `Program.cs` (root namespace) | Builds `MainForm` / `TrayApplicationContext` | UI |

- **No WinForms type outside `UI/`**: no `System.Windows.Forms` in Composition, Imaging or
  Explorer, neither as `using` nor fully qualified. `System.Drawing` (GDI+) is the one graphics
  dependency shared across layers.
- A lower layer never references an upper one; Composition and Explorer never reference each other.
- One project, so the layering is a **convention**, not enforced by assemblies.
- Evidence: exhaustive grep of `using ImageGridFusion.*`, `System.Windows.Forms` and inline
  qualified names — zero exception.

### A2 — Immutable State, Mutable Resources

- Settings and effect state are **immutable**: `sealed record` (`ImageLook`, `BackgroundEffect`,
  `BlurEffect`, `FramesEffect`, `VolumeEffect`, `GridBorders`, `Soundtrack`, `Separator`…) changed
  with `with` expressions, never with setters. Small value types are `readonly record struct`
  (`Frame`, `Fit`, `TurnedFit`, `Lab`, `Edges`, `Band`, `Side`).
- A **mutable class** is reserved for a **resource holder**: `SourceImage` owns a `Bitmap` /
  `PageSource` and swaps + disposes them (`ShowPage`, `ShowFrame`); its `Look` is only replaced as a
  whole value. `GridLayout` is a class for its static catalog, but is effectively immutable — its
  `With*` methods return new instances.

### A3 — State Ownership and Flow

- **`GridPreview` owns the cells' state**: the image list (`_images`), each image's look, the
  layout, the separators. `MainForm` holds no image list; it reads `Images`, `SelectedImage`,
  `ActiveLayout`.
- **Down** (MainForm → GridPreview): a method for per-image changes (`SetSelectedLook`, called by
  `MainForm.ChangeLook`), a property for grid-wide settings (`Borders`).
- **Up** (GridPreview → MainForm): C# events named **`XChanged`** for state (`ImagesChanged`,
  `SelectedImageChanged`, `LayoutChanged`) and **`XClicked`** for intents (`AddImagesClicked`,
  `ShowInExplorerClicked`), raised by `OnX()` methods; a gesture that leaves the control is named
  after the gesture (`SwapDragMoved`, `ReleasedOffGrid`).
- The **effects lifecycle** of § Effects › Scope and State (reset on replace / shift, kept on swap)
  is implemented in **one place**: `GridPreview`'s `RemoveAt` / `Replace` / `Swap`; every route
  into a cell goes through `GridPreview.Add` (already stated by § Preview Playback for the restart).

### A4 — One Rendering Path

Generalises the existing § Effects › Rendering and § On-Cell Helper Indicators rules to the whole
grid:

- **Every pixel of the grid that can reach an output is drawn by `Compositor`** (`Draw`,
  `DrawCell`, `Render`, `Cells`, `Flattened`…): the preview (`GridPreview`), the still Copy / Save
  and every animated export (`GridExport`) call it — no second drawing of the grid anywhere.
- `GridPreview.OnPaint` paints a **cached `Compositor` bitmap**, redrawn only when the images, the
  layout or the size change, and adds **preview-only chrome** over it: checkerboard, drop-zone
  highlight, hover / selection outlines, drag visuals, helper indicators.

### A5 — Background Work

- Long work (exports, index scan, animation loading, OCR) runs in **`Task.Run`**, takes a
  **`CancellationToken`** checked **once per unit of work** (per frame, per file) and reports through
  an **`IProgress<T>`** created on the UI thread.
- Results come back to the UI thread through **`await` continuations** or **`Progress<T>`**;
  an explicit `BeginInvoke` only for a callback that has no captured context
  (`ThumbnailGrid`, `FileExplorerPanel`).
- A cancelled or failed export **deletes its partial output file** (`GridExport.RenderAnimation`,
  `finally` + `TryDelete`).
- **While exporting**, the export keeps the settings it started with: the controls that would
  change them are locked (`IsExporting`) — already stated for the effects toolbars.

### A6 — Native Resources

- COM / Media Foundation interop is **isolated in `Imaging/`** (`MediaFoundation.cs`,
  `VideoReader`, `VideoEncoder`, `ShellThumbnail`), behind `IDisposable` wrappers.
- Media Foundation objects **live on the thread pool**: they are created and released there — even
  the release is `Task.Run(reader.Dispose)` so it never blocks the UI thread
  (`AnimationPlayer.cs:273-276`).
- Every `Bitmap`, reader and frame is **disposed by its owner**, in a `finally` or a `Dispose` chain.

### A7 — Code Style

- File-scoped namespaces; nullable and implicit usings enabled (`.csproj`).
- Classes `sealed` by default; `static` classes for stateless computations (`Compositor`,
  `FitCalculator`, `DominantColor`, `CanvasSizer`, `ImageLoader`, `GridExport`).
- **One main type per file**, named after it, with its small companion types beside it
  (e.g. `MediaFoundation.cs`, `BlurEffect.cs`, `FitCalculator.cs`) — *not* strictly one type per
  file: 15 files hold more than one.

### A8 — One Definition, Read Everywhere

- A value several places need — the preview, the exports, a readout, the gestures — is **computed
  in one member** and read there, never re-derived: `Animation.VideoLength` (§ Video Length),
  `ImageLook.Shown` (§ The Crop Exception), `Compositor.Cells` (the Borders' gap),
  `AppSettings` (§ App Settings).
- RULES.md already states it **case by case**; this rule states the pattern, so a new shared value
  follows it without waiting for its own section.

---

## Candidate Rules — UX Conventions

### U1 — Files the App Writes

Where the settings live is **already a rule** since 2026-09-30 (RULES.md § App Settings:
`settings.json` next to the exe, never the registry). What it does not say yet:

| What | Where | How |
|---|---|---|
| Settings, favorites, index | Next to the exe (`settings.json`, `favorites.txt`, `files.index`) | **Atomically**: written to `<file>.tmp`, then `File.Move(overwrite: true)` — `AppSettings.cs:84-86`, `Favorites.cs:126-128`, `FileIndex.cs:91-105` |
| Generated files (last video, light copy) | `%TEMP%\ImageGridFusion` | Swept at the **next start-up** (`MainForm.CleanTempVideos`, off the UI thread), files in use left for later |

- A new data file follows both: next to the exe for what is kept, the temp folder for what is
  generated, and never written in place.

### U2 — Status Line

- Every message to the user — information, progress, error — goes to a **status line**, and
  **stays until the next one replaces it** (no timer); errors are **red**
  (`MainForm.ShowStatus`, origin `20260924-copy-info.md`).
- **No message box** anywhere in the app.
- A panel with its own status line reports there (the file explorer's `ShowStatus`); everything
  else in the window's.

### U3 — Progress Feedback

- Background work reports in the status line as **`Label… value`**: a count (`Indexing… 120/450`)
  or a percentage (`Exporting the video… 42 %`), through the `IProgress<T>` of A5.

### U4 — Clipboard

- A Copy puts **one content in several formats**, so the target app picks what it supports:
  bitmap + `PNG` (still Copy), file + bitmap (light copy), file + raw `GIF` (animated Copy)
  (`20260924-export-as-gif.md`, `20260926-light-copy-for-sharing.md`).

### U5 — DPI

- Every size in pixels is expressed in **logical pixels** and scaled with `LogicalToDeviceUnits`
  (6 UI files) — the handle-snapping rule of RULES.md is one instance.

### U6 — Ctrl + Wheel

Implemented since 2026-09-30 (`20260926-ctrl-wheel-5-percent-step.md`, delivered):

- **Every option slider is a `StepSlider`** (`UI/StepSlider.cs`, built by `MainForm.OptionSlider`):
  the wheel alone moves it as a stock `TrackBar`; **with Control**, it moves onto the **next
  multiple of 5 %** (`WheelSteps.Percent`), one step per notch — or does its own `ControlWheel` for
  a slider whose value is not the unit it shows (the Borders' gap, 0.5 %).
- **On a cell**, the wheel zooms; with Control, by steps of 5 % (`GridPreview.OnMouseWheel`).
- Control is read from the **wheel message itself** (`WheelSteps.WithControl`), not from the
  keyboard state, so it works whatever window has the focus.
- The file explorer's tiles are the one other Ctrl + wheel: it sizes them (Glossary › Tile size),
  the wheel alone scrolling — a list, not a slider.

### Not Retained

| Candidate | Why not |
|---|---|
| Image provenance label (pasted / dropped / file) reused across features | One workfile only (`20260926-source-file-name.md`); the other occurrence is a list, not a decision |
| Registry vs file for the settings | Settled by RULES.md § App Settings (2026-09-30) |

---

## Known Gaps

Exceptions the code shows against the rules above. Reported only — **no refactoring** here.

| Rule | Gap | Where |
|---|---|---|
| A3 | The Borders exist **twice**: `MainForm._borders` (the toolbar's copy, kept while off) and `GridPreview`'s render copy, pushed by `ApplyBorders` | `MainForm.cs:250`, `MainForm.cs:1473` |
| A6 | The fire-and-forget `Task.Run(reader.Dispose)` leaves a failing `Dispose` unobserved | `AnimationPlayer.cs:276` |
| U4 | Each Copy builds its own `DataObject`; no shared helper (only the animated path has one, `PutOnClipboard`) | `MainForm.cs:1171`, `1187`, `1396` |
| — | `MainForm.cs` (2 747 lines) and `GridPreview.cs` (2 441 lines) are several times the size of any other file; no rule is broken, noted for context | — |

---

## Test Impact

Nothing testable changes: the deliverable is documentation only (`RULES.md` and/or a new file,
`GLOSSARY.md`, possibly `CLAUDE.md`). The app has no test project anyway.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (documentation only) | — | — |

---

## Open Questions

- [ ] Which candidate rules are kept (A1–A8, U1–U6)?
- [ ] Where do they go: all in `RULES.md`, or the code architecture (A1–A8) in a new
  `ARCHITECTURE.md` imported by `CLAUDE.md`, the UX conventions (U1–U6) in `RULES.md`?
- [ ] A4 generalises the existing § Effects › Rendering and § On-Cell Helper Indicators rules:
  rewrite them around the new rule, or leave them untouched and add A4 beside them?
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
| 5 | Which candidate rules are kept (A1–A8, U1–U6)? | | |
| 6 | Where do they go: all in `RULES.md`, or A1–A8 in `ARCHITECTURE.md` and U1–U6 in `RULES.md`? | | |
| 7 | A4: rewrite the existing Rendering / Helper Indicators rules around it, or add it beside them? | | |
| 8 | Ctrl + wheel 5 % steps: a rule now, or left out until built? | Never answered — moot, implemented meanwhile (Iteration 2) | 2026-09-30 |
| 9 | Known gaps: also in the rules file, or only in this workfile? | | |

---

*Last updated: 2026-09-30*
