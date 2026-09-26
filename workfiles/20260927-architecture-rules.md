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
(Iteration 1). File references are as of commit `c703607`.

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
  `ShowInExplorerClicked`), raised by `OnX()` methods.
- The **effects lifecycle** of § Effects › Scope and State (reset on replace / shift, kept on swap)
  is implemented in **one place**: `GridPreview`'s `RemoveAt` / `Replace` / `Swap`. The Volume's
  sound on arrival lives in `Composition/Animation.cs` (`SoundOnArrival`).

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
  (`AnimationPlayer.cs:238-241`).
- Every `Bitmap`, reader and frame is **disposed by its owner**, in a `finally` or a `Dispose` chain.

### A7 — Code Style

- File-scoped namespaces; nullable and implicit usings enabled (`.csproj`).
- Classes `sealed` by default; `static` classes for stateless computations (`Compositor`,
  `FitCalculator`, `DominantColor`, `CanvasSizer`, `ImageLoader`, `GridExport`).
- **One main type per file**, named after it, with its small companion types beside it
  (e.g. `MediaFoundation.cs`, `BlurEffect.cs`, `FitCalculator.cs`) — *not* strictly one type per
  file: 15 files hold more than one.

---

## Candidate Rules — UX Conventions

### U1 — Persistence

| What | Store | Written |
|---|---|---|
| Cell and global effects, the grid, the images | **Nothing** — not persisted (already in RULES.md) | — |
| Scalar **app settings** (Borders color, Twitter corners by default, explorer folder / open / columns) | Registry, `HKCU\Software\ImageGridFusion`, through `UI/AppSettings.cs` — one typed getter + one `SaveX` per value, the first-launch default a constant there | On change |
| Start with Windows | Registry `Run` key, `UI/StartupRegistration.cs` | On change |
| **Collections and caches** (favorites, file index) | Text files **next to the exe** (`favorites.txt`, `files.index`), one entry per line | **Atomically**: `.tmp` then `File.Move(overwrite: true)` |
| Generated files (last video, light copy) | `%TEMP%\ImageGridFusion` | Swept at the **next start-up**, files in use left for later |

- **No settings file** (no JSON, no XML): the JSON store designed by
  `20260926-remember-last-folder.md` was superseded by `20260926-file-explorer.md` and never built.
- **Failures**: a read failure falls back to the default, silently; a write failure keeps the value
  **for the session** and says so in the status line (`20260923-barre-etat-windows.md`,
  `20260926-cell-borders.md`).

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

### Not Retained

| Candidate | Why not |
|---|---|
| Ctrl + wheel = 5 % steps on every slider | Decided in `20260926-ctrl-wheel-5-percent-step.md`, reused by `20260927-emoji-stickers.md`, but **not implemented** — see Open Questions |
| Image provenance label (pasted / dropped / file) reused across features | One workfile only (`20260926-source-file-name.md`); the other occurrence is a list, not a decision |
| "Remembered between sessions" for window size, background fading | Designed, not built (`20260927-remember-window-size.md`, `20260926-cell-background-fading.md`) |

---

## Known Gaps

Exceptions the code shows against the rules above. Reported only — **no refactoring** here.

| Rule | Gap | Where |
|---|---|---|
| A3 | The Borders exist **twice**: `MainForm._borders` (the toolbar's copy, kept while off) and `GridPreview`'s render copy, pushed by `ApplyBorders` | `MainForm.cs:230`, `MainForm.cs:2296-2299` |
| A6 | The fire-and-forget `Task.Run(reader.Dispose)` leaves a failing `Dispose` unobserved | `AnimationPlayer.cs:241` |
| U4 | Each Copy builds its own `DataObject`; no shared helper (only the animated path has one, `PutOnClipboard`) | `MainForm.cs:1044`, `1058`, `1193` |
| — | `MainForm.cs` (2 393 lines) and `GridPreview.cs` (2 170 lines) are six times the size of any other file; no rule is broken, noted for context | — |

---

## Test Impact

Nothing testable changes: the deliverable is documentation only (`RULES.md` and/or a new file,
`GLOSSARY.md`, possibly `CLAUDE.md`). The app has no test project anyway.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (documentation only) | — | — |

---

## Open Questions

- [ ] Which candidate rules are kept (A1–A7, U1–U5)?
- [ ] Where do they go: all in `RULES.md`, or the code architecture (A1–A7) in a new
  `ARCHITECTURE.md` imported by `CLAUDE.md`, the UX conventions (U1–U5) in `RULES.md`?
- [ ] A4 generalises the existing § Effects › Rendering and § On-Cell Helper Indicators rules:
  rewrite them around the new rule, or leave them untouched and add A4 beside them?
- [ ] Ctrl + wheel 5 % steps: written now as a rule (the code catching up when its workfile is
  implemented), or left out until it is built?
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
| 5 | Which candidate rules are kept (A1–A7, U1–U5)? | | |
| 6 | Where do they go: all in `RULES.md`, or A1–A7 in `ARCHITECTURE.md` and U1–U5 in `RULES.md`? | | |
| 7 | A4: rewrite the existing Rendering / Helper Indicators rules around it, or add it beside them? | | |
| 8 | Ctrl + wheel 5 % steps: a rule now, or left out until built? | | |
| 9 | Known gaps: also in the rules file, or only in this workfile? | | |

---

*Last updated: 2026-09-27*
