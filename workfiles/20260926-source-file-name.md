# Source File Name

> Working document — the selected cell shows the name of its image's source file, in fluorescent
> green at its bottom, with an icon opening the file's folder in Windows Explorer.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

When a cell is selected, the preview shows, **at the bottom left of that cell**, the **file name**
(name + extension) its image came from, drawn as a **helper indicator** (fluorescent green over
the black halo, preview only, never exported). Just **before** the name, on its left, a small
**folder icon** opens **Windows Explorer on the file's folder, the file already selected**.

An image without a source file (pasted from the clipboard, or a dropped text) shows a **label
naming its source instead of the name**, with **no icon**.

Components concerned:

- `Composition/SourceImage.cs` — `FilePath` (`string?`): the file the image came from; `null` for
  a clipboard image (`ImageLoader.FromImage`) or a pasted text (`ImageLoader.FromText`).
- `UI/GridPreview.cs` — `OnPaint` draws the helper indicators after the cached composite
  (`HelperColor`, `HelperHalo`); the zoom badge (`PaintZoomBadge`) is the text model to follow:
  a `GraphicsPath` string, bold, halo pen of 4 logical px, clipped to the cell. Hit testing and
  cursors are resolved in the hover update (`_hoveringClose`, `_hoveringHandle`, `Cursors.Hand`).

---

## Display

- **When**: a cell is selected (`_selected >= 0`) and holds an image. Only the selected cell shows
  it — hovering another cell shows nothing.
- **Where**: bottom left of the cell, inset like the cell buttons (`ButtonInset`, 6 logical px),
  clipped to the cell: the folder icon first, then the name, `ButtonGap` apart. Without a file (no
  icon), the label starts at the inset.
- **What**: `Path.GetFileName(FilePath)` — name + extension, no folder.
- **Too wide**: shortened with an **ellipsis in the middle**, the start and the extension kept
  (`vacances-ete-2…plage.jpg`); the icon always stays visible before it.
- **Tooltip**: hovering the name **or** the icon shows the file's **full path**.
- **Look**: a helper indicator (RULES.md § *On-Cell Helper Indicators*): `HelperColor` text over the
  `HelperHalo` outline (4 logical px), bold like the zoom badge but smaller — **12 logical px**
  (`SourceNameTextSize`; the badge is 16). Placed on the font's line height, not on the glyphs, so
  the name never jumps with its letters.
- **Too narrow**: a cell with no room left for the icon and its gap shows nothing.
- **Preview only**: drawn in `GridPreview.OnPaint`, never in `Compositor` — exports, the clipboard
  copy and video playback are untouched.
- **No source file**: a label naming the source in place of the name, no icon, no tooltip. The
  image **remembers how it arrived** (set where `MainForm` creates it, since `FilePath` alone cannot
  tell a pasted text from a dropped one):

  | Arrived as | Label |
  |---|---|
  | A clipboard image (Ctrl+V, `ImageLoader.FromImage`) | `Pasted image` |
  | A pasted text (Ctrl+V, `ImageLoader.FromText`) | `Pasted text` |
  | A dropped text (drag and drop, `ImageLoader.FromText`) | `Dropped text` |

  The flag is `SourceImage.Dropped`, set by `MainForm.AddTextAsync(…, dropped)`; a text image is
  recognised by `Pages is TextPages`.
- **Blur handles**: the name stays shown while the Blur bars are drawn; the bars are painted
  **over** it and keep the click priority on their own hit area (RULES.md § *On-Cell Handles*).
- **Hidden while a cell is being dragged** (swap), like the cell buttons.
- The fitted name is measured once per image, width and DPI (`_fittedName`), not at every paint.

## Folder Icon

- A small **folder glyph** (a silhouette with its tab up left, **16 logical px**,
  `SourceIconSize`), drawn at the bottom left of the cell, **left of the name**, `ButtonGap`
  apart, in the helper colours; it turns **white** while hovered (the hover feedback the rule allows for a handle).
- **Click**: `explorer.exe /select,"<FilePath>"` — Explorer opens on the folder, the file selected.
  `GridPreview` raises `ShowInExplorerClicked` with the path; `MainForm.ShowInExplorer` checks the
  file and runs Explorer. Explorer failing to start shows its error in the status bar.
- It works **during an export** too: opening Explorer changes nothing in the grid.
- Its hit area takes **priority over the cell's gestures on that area only** (no selection change,
  no drag, no pan starts from it); the cursor is `Cursors.Hand` over it.
- **The file no longer exists** (moved or deleted since it was loaded): if its **folder still
  exists**, Explorer opens on it with nothing selected; in every case the **status bar** says
  `File not found: <path>`, as an error.

---

## Test Impact

**None.** The repository has no test project (`src/` holds `ImageGridFusion` only), like every
previous workfile. The behaviour is purely drawing and a shell call in `GridPreview`; it is checked
by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Where is the name drawn?~~ → Bottom left of the selected cell, the icon right after it (Q&A #1)
- [x] ~~What does the icon do?~~ → Opens Explorer on the folder, the file selected (Q&A #2)
- [x] ~~Image without a source file?~~ → A label in place of the name, no icon (Q&A #3)
- [x] ~~A name wider than the cell: how is it shortened?~~ → Ellipsis in the middle, start and extension kept, icon always visible (Q&A #5)
- [x] ~~The Blur effect's bottom bar can sit on the bottom edge, where the name is: what happens?~~ → The name stays; the bars are drawn over it and keep their click priority (Q&A #6)
- [x] ~~Wording of the label shown without a source file (the app's UI is in English)?~~ → Depends on the source: `Pasted image`, and a text label (Q&A #7)
- [x] ~~A tooltip with the full path when hovering the name or the icon?~~ → Yes, on both (Q&A #8)
- [x] ~~The source file no longer exists when the icon is clicked: what happens?~~ → Explorer opens the folder if it still exists, and the status bar says the file was not found (Q&A #9)
- [x] ~~A text image can be pasted **or dropped** (`MainForm` drop handler, line ~553): which label does it show?~~ → The image remembers its origin: `Pasted image`, `Pasted text`, `Dropped text` (Q&A #10)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial proposal, from the scoping batch (Q&A #1–#4) and one direct exploration pass of
`SourceImage` and `GridPreview`: the file name as a helper indicator at the bottom left of the
selected cell, a folder icon after it running `explorer /select`, a label without icon for an image
with no source file. Hidden during a swap drag. No unit tests (no test project).

### Iteration 2 — 2026-09-26

Answers to the five front-loaded questions (Q&A #5–#9): middle ellipsis, the name kept under the
Blur bars, a label depending on the source, a full-path tooltip on the name and the icon, a missing
file opening its folder with a status message. A new question emerged: a text image is not only
pasted but also dropped, so `Pasted text` would be wrong for a dropped one (Q&A #10).

### Iteration 3 — 2026-09-26

The label of a file-less image follows its origin (Q&A #10): the image remembers whether it was
pasted or dropped — `Pasted image`, `Pasted text`, `Dropped text`. No question left open.

### Iteration 4 — 2026-09-26 — ✅ Implemented

Go given for the code, the unit tests and the documentation. The run stays on `main` (standing
choice for this repository). Unit tests do not apply: there is no test project.

### Iteration 5 — 2026-09-26 — 🧭 Implementation choices

Choices the frozen design left open, taken during the run:

- **Text size** 12 logical px bold, smaller than the zoom badge's 16: a file name is long and sits
  permanently on the cell.
- **Folder icon**: a filled silhouette, 16 logical px, right after the shortened name.
- **Cell too narrow** for the icon and its gap: nothing is drawn.
- **During an export**: the icon still opens Explorer (it changes nothing in the grid).
- **Wiring**: `GridPreview.ShowInExplorerClicked` event → `MainForm.ShowInExplorer`, which owns the
  file check, the Explorer call and the status messages (`File not found: <path>`, or Explorer's
  own error if it cannot start).
- **Origin flag**: a settable `SourceImage.Dropped`, set by `MainForm.AddTextAsync`.
- **Build**: the first build could not copy `ImageGridFusion.exe` (locked by another running
  instance), so it was checked in a scratchpad output folder; that instance was gone by the
  launch, and the app was rebuilt into its normal `bin` folder for it.

No project rule was broken. The run stayed on `main`.

### Iteration 6 — 2026-09-26 — ⚙️ Post-implementation — Icon left of the name

The user asked for the folder icon **on the left of the file name** instead of after it: the icon
now sits at the bottom-left inset, the name follows it. A file-less label, which has no icon,
still starts at the inset.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 6 | 2026-09-26 | `SourceImage.Dropped`, `MainForm` (origin flag, `ShowInExplorer`), `GridPreview` (name, icon, tooltip, hit testing) |
| Unit tests | 4 | 2026-09-26 | Not applicable — no test project |
| README | 4 | 2026-09-26 | *Features*, under the cell selection |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where and how is the file name shown on the selected cell? | Bottom left, name + extension, the icon right after it; preview only | 2026-09-26 |
| 2 | What does a click on the icon do? | Opens Explorer on the folder, the file selected (`explorer /select,`) | 2026-09-26 |
| 3 | An image without a source file (pasted with Ctrl+V): what does the cell show? | A label in place of the name, no icon | 2026-09-26 |
| 4 | Is the subject straightforward, or tricky / long to explore? | Straightforward — a single exploration pass | 2026-09-26 |
| 5 | A name wider than the cell: how is it shortened? | Ellipsis in the middle, the start and the extension kept | 2026-09-26 |
| 6 | The Blur bottom bar on the bottom edge, where the name is: what happens? | The name stays shown; the bars are drawn over it and keep their click priority | 2026-09-26 |
| 7 | Wording of the label without a source file? | Depends on the source (e.g. `Pasted image`, `Pasted text`) | 2026-09-26 |
| 8 | A tooltip with the full path on the name or the icon? | Yes, on both | 2026-09-26 |
| 9 | The source file no longer exists when the icon is clicked? | Explorer opens the folder if it still exists, and the status bar says the file was not found | 2026-09-26 |
| 10 | A text image can be pasted or dropped: which label does it show? | The image remembers its origin: `Pasted text` or `Dropped text` (`Pasted image` for a clipboard image) | 2026-09-26 |

---

*Last updated: 2026-09-26*
