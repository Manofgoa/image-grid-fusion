# Drag & Drop Ghost

> Working document — a ghost thumbnail that follows the cursor while dragging, plus drop-target
> highlighting, for both the in-grid swap and files dropped from Explorer.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a drag gives almost no visual feedback: dragging a cell onto another only switches the
cursor to `SizeAll` and tints the target cell; dragging files from Explorer only shows the `Copy`
cursor. This task adds a **ghost**, a translucent thumbnail that follows the cursor, to both kinds
of drag, highlights the cell that will receive the drop, and dims the source cell during a swap.

Scope agreed in the scoping batch (Q&A #1–#4):

| Drag | Ghost | Target highlight | Source cell |
|---|---|---|---|
| **In-grid swap** (cell onto cell) | Thumbnail of the dragged image, following the cursor | Cell under the cursor | **Dimmed** during the drag |
| **File drop** (from Explorer) | Thumbnail of the dragged file(s), following the cursor | Cell that will receive the file | n/a (the source is outside the app) |

Components involved:

- `src/ImageGridFusion/UI/GridPreview.cs`: custom control that paints the preview and runs the
  swap drag with raw mouse events (`OnMouseDown` / `OnMouseMove` / `OnMouseUp`, no `DoDragDrop`).
  It already tracks `_pressed`, `_pressPoint`, `_dragging`, `_dropTarget` and paints a
  translucent highlight overlay on the target cell.
- `src/ImageGridFusion/UI/MainForm.cs`: handles file drops on the form and on the preview
  (`DragEnter` sets `Copy`, `DragDrop` loads the files). There is no `DragOver` handler, and the
  preview is not told where the cursor is during an external drag.

Dependency: the target highlight for a file drop relies on **drop onto a cell replaces it**, from
Milestone 2 of `20260923-application-v1.md`. That milestone has its go (Iteration 7 there) but is
not delivered yet: right now every drop appends.

---

## In-Grid Swap — Current State

- The drag starts once the cursor leaves the `SystemInformation.DragSize` dead zone around the
  press point. The control keeps the mouse capture until release, so move events keep coming
  even outside its bounds.
- `OnPaint` draws the cached composition, then the target overlay (`Highlight` at alpha 90) when
  `_dropTarget` is a cell other than `_pressed`, then the selection border, then the × on hover
  (hidden while dragging).
- On release over another cell, `Swap` exchanges the two images and the selection follows the
  dragged image.

## In-Grid Swap — Planned

- **Ghost**: a translucent thumbnail of `_images[_pressed]` painted last in `OnPaint`, positioned
  from the current cursor location. It is invalidated on every mouse move while dragging (only the
  old and new ghost rectangles, not the whole control).
- **Source cell dimmed**: painted over `cells[_pressed]` while `_dragging`.
- **Target highlight**: kept as it is today.
- Size, opacity, anchoring, dimming style and behaviour outside the control: see Open Questions.

---

## File Drop — Current State

- `MainForm.OnDragEnter` accepts `DataFormats.FileDrop` with `DragDropEffects.Copy`. Nothing
  else happens until `OnDragDrop` loads the files off the UI thread and appends them.
- No file is decoded during the drag.

## File Drop — Planned

- The preview is told the cursor position during an external drag (a `DragOver` handler) so it can
  highlight the receiving cell, and forgets it on `DragLeave` / `DragDrop`.
- Ghost source and highlight rule: see Open Questions.

---

## Test Impact

No unit test project exists: tests were declined for v1 (Q&A #12 of `20260923-application-v1.md`),
and the planned work is painting only. Pending confirmation (Open Questions).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (pending Q&A #11) | — | — |

---

## Open Questions

- [ ] Ordering with Milestone 2 (drop onto a cell): implement this after it, or bring the cell
      targeting for file drops in here?
- [ ] File drop ghost: Explorer's own drag image (Windows shell thumbnail, nothing decoded), or a
      thumbnail the app decodes from the file on entry?
- [ ] File drop highlight: which cells light up — the hovered cell only, the whole canvas when the
      file will be appended, the cell the replace rule picks when the grid is full?
- [ ] In-grid ghost: size, opacity and anchoring on the cursor?
- [ ] In-grid ghost outside the preview (over the buttons, outside the window): clipped at the
      preview edges, or following the cursor everywhere?
- [ ] Source cell dimming style: dark translucent overlay, desaturation, or something else?
- [ ] Unit tests: none (consistent with v1), or create a test project for the extractable logic
      (ghost bounds)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design built from the scoping batch (Q&A #1–#4) and a single read-only pass over
`GridPreview.cs`, `MainForm.cs`, the README and the v1 workfile:

- Ghost for both drags, thumbnail following the cursor plus target highlight; source cell dimmed
  during a swap.
- The in-grid ghost is painted by `GridPreview` itself: the swap is already driven by raw mouse
  events with capture, so no `DoDragDrop` is needed.
- The file-drop highlight depends on Milestone 2's drop onto a cell, which is not delivered yet.
- Seven open questions listed before any detailed design.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which drag & drop shows the ghost: in-grid swap, file drop, or both? | Both | 2026-09-23 |
| 2 | What does the ghost look like? | Thumbnail following the cursor + highlight of the target cell | 2026-09-23 |
| 3 | What happens to the source cell during the drag? | Dimmed | 2026-09-23 |
| 4 | Expected depth of the subject? | Straightforward (single scout pass) | 2026-09-23 |
| 5 | Ordering with Milestone 2: after it, or bring drop-onto-cell targeting in here? | | |
| 6 | File drop ghost: Explorer's drag image, or a thumbnail decoded by the app? | | |
| 7 | File drop highlight: which cells light up? | | |
| 8 | In-grid ghost: size, opacity and anchoring? | | |
| 9 | In-grid ghost outside the preview: clipped or everywhere? | | |
| 10 | Source cell dimming style? | | |
| 11 | Unit tests: none, or a test project for the ghost bounds? | | |

---

*Last updated: 2026-09-23*
