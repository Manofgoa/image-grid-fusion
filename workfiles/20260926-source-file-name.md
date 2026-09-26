# Source File Name

> Working document — the selected cell shows the name of its image's source file, in fluorescent
> green at its bottom, with an icon opening the file's folder in Windows Explorer.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

When a cell is selected, the preview shows, **at the bottom left of that cell**, the **file name**
(name + extension) its image came from, drawn as a **helper indicator** (fluorescent green over
the black halo, preview only, never exported). Right after the name, a small **folder icon** opens
**Windows Explorer on the file's folder, the file already selected**.

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
  clipped to the cell.
- **What**: `Path.GetFileName(FilePath)` — name + extension, no folder.
- **Too wide**: shortened with an **ellipsis in the middle**, the start and the extension kept
  (`vacances-ete-2…plage.jpg`); the icon always stays visible after it.
- **Tooltip**: hovering the name **or** the icon shows the file's **full path**.
- **Look**: a helper indicator (RULES.md § *On-Cell Helper Indicators*): `HelperColor` text over the
  `HelperHalo` outline, same font style as the zoom badge.
- **Preview only**: drawn in `GridPreview.OnPaint`, never in `Compositor` — exports, the clipboard
  copy and video playback are untouched.
- **No source file**: a label naming the source in place of the name, no icon, no tooltip:
  `Pasted image` for a clipboard image (`ImageLoader.FromImage`); the label of a text image
  (`ImageLoader.FromText`, pasted **or dropped**): see Open Questions.
- **Blur handles**: the name stays shown while the Blur bars are drawn; the bars are painted
  **over** it and keep the click priority on their own hit area (RULES.md § *On-Cell Handles*).
- **Hidden while a cell is being dragged** (swap), like the cell buttons.

## Folder Icon

- A small **folder glyph**, drawn right after the name, in the helper colours; it turns **white**
  while hovered (the hover feedback the rule allows for a handle).
- **Click**: `explorer.exe /select,"<FilePath>"` — Explorer opens on the folder, the file selected.
- Its hit area takes **priority over the cell's gestures on that area only** (no selection change,
  no drag, no pan starts from it); the cursor is `Cursors.Hand` over it.
- **The file no longer exists** (moved or deleted since it was loaded): if its **folder still
  exists**, Explorer opens on it with nothing selected; in every case the **status bar** says the
  file was not found.

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
- [ ] A text image can be pasted **or dropped** (`MainForm` drop handler, line ~553): which label does it show?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

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
| 10 | A text image can be pasted or dropped: which label does it show? | | |

---

*Last updated: 2026-09-26*
