# Drag & Drop Ghost

> Working document — a ghost thumbnail that follows the cursor while dragging, plus drop-target
> highlighting, for both the in-grid swap and files dropped from Explorer.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Before this task, a drag gave almost no visual feedback: dragging a cell onto another only switched the
cursor to `SizeAll` and tinted the target cell; dragging files from Explorer only showed the `Copy`
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
  (`DragEnter` sets `Copy`, `OnPreviewDragOver` tells the preview which cell is hovered through
  `ShowDropTarget`, `DragLeave` / `DragDrop` clear it, `DragDrop` loads the files).

Dependency: the target highlight for a file drop relies on **drop onto a cell replaces it** and on
the **replace rule when full**, both from Milestone 2 of `20260923-application-v1.md`. This task
is implemented after Milestone 2 and reuses its cell targeting (Q&A #5). **Milestone 2 is
delivered** (`8696907`, v1 complete in `5d2584e`): the dependency is met.

Overlap: `20260923-drop-zone.md` (designed, not implemented) adds an "Add images" strip to the
right of the canvas, highlighted while files are dragged over it. See Open Questions.

---

## In-Grid Swap

What `GridPreview` does since this task (commit `aafb58e`):

- The drag starts once the cursor leaves the `SystemInformation.DragSize` dead zone around the
  press point (`StartDrag`). The control keeps the mouse capture until release, so move events
  keep coming even outside its bounds.
- **Ghost** (Q&A #8): at drag start, the source cell's region of the cached composition (the cell
  **as the preview shows it**) is scaled once into a ghost bitmap at **40 % of the cell size**
  (`GhostScale`, aspect kept, high-quality bicubic). It is drawn at **70 % opacity**
  (`GhostOpacity`, colour matrix) at every paint.
  - **Anchored at the grab point**: `ghost.Location = cursor − (pressPoint − cell.Location) × 0.4`.
  - Painted **last** in `OnPaint`, above the dimming, the target highlight and the selection
    border.
  - **Clipped at the preview's edges** (Q&A #9): drawn by `GridPreview` itself, no extra window;
    it reappears as soon as the cursor comes back.
  - On each mouse move, only the old and new ghost rectangles are invalidated; the whole control
    is invalidated only when the target cell changes.
- **Source cell dimmed** (Q&A #10): black overlay at alpha 120 (the × button uses 150) over
  `cells[_pressed]` while dragging.
- **Target highlight**: `Highlight` overlay at alpha 90 on the hovered cell, none on the source
  cell itself — unchanged.
- **End of drag** (`EndDrag`): on release (swap if over another cell), or on **capture loss**
  (Alt+Tab, a dialog opening) which cancels the drag without swapping. The ghost bitmap is
  disposed, dimming and highlight disappear.
- Guard: if the pressed cell no longer exists when the drag would start or paint, nothing is
  drawn for it (see the gap reported in Iteration 7).

---

## File Drop

What `MainForm` and `GridPreview` do since this task (commits `bae3ca8`, `cc15f60`):

- **Ghost** (Q&A #6): **Windows' own drag image**, the thumbnail Explorer produces (stack and
  count for several files). The app decodes nothing during the drag.
  - .NET 10 WinForms forwards the drag to the shell's drop-target helper only when
    `DragEventArgs.DropImageType` is set (> `Invalid`), and the following `DragOver` events reuse
    the value set in `DragEnter` (checked in `dotnet/winforms` `DropTarget.cs`). `OnDragEnter`
    therefore sets `DropImageType.Copy` whenever it accepts files; no drop-description text.
- **Highlight: everything that will receive the file** (Q&A #7). `OnPreviewDragOver` passes the
  cursor position (client coordinates) to `GridPreview.ShowDropTarget(Point?)`, which resolves the
  target itself (`DropTargetBounds`) and stores it as a rectangle; `DragLeave` / `DragDrop` pass
  `null`.

  | Cursor | Grid | Highlighted |
  |---|---|---|
  | Over a cell | any | That cell (it will be replaced) |
  | Outside every cell | not full (empty included) | The whole canvas (the file will be appended) |
  | Outside every cell | full | The cell the excess rule picks: the selected cell, else the last in reading order |

  Same `Highlight` tint as the in-grid swap; on an empty grid it is painted over the dashed
  empty state. The excess rule is one helper, `ExcessTarget()`, used by both `Add` and the
  highlight.
  Only drags over the preview are highlighted; over the button row, Windows' drag image alone
  shows the drag.
- Dropping itself is unchanged: onto a cell, the first file replaces it; elsewhere the files are
  added (`AddFilesAsync(paths, target)`).
- **Drop zone** (Q&A #13): this task is implemented **before** `20260923-drop-zone.md`. The
  whole-canvas highlight when appending **stays** once the drop zone exists; the strip only adds
  its own highlight when the cursor is over it.

---

## Test Impact

**No unit tests**, declined by the user (Q&A #11): no test project exists (tests were declined
for v1 too), and the planned work is painting only. Checked on screen. Nothing is created or
updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined) | — | — |

---

## Open Questions

- [x] ~~Ordering with Milestone 2 (drop onto a cell): implement this after it, or bring the cell
      targeting for file drops in here?~~ → After Milestone 2, reusing its cell targeting
- [x] ~~File drop ghost: Explorer's own drag image (Windows shell thumbnail, nothing decoded), or a
      thumbnail the app decodes from the file on entry?~~ → Windows' drag image
- [x] ~~File drop highlight: which cells light up — the hovered cell only, the whole canvas when the
      file will be appended, the cell the replace rule picks when the grid is full?~~ → Everything
      that will receive the file: hovered cell, else the whole canvas when appending, else the
      cell the replace rule picks
- [x] ~~In-grid ghost: size, opacity and anchoring on the cursor?~~ → ~40 % of the cell, 70 %
      opacity, anchored at the grab point
- [x] ~~In-grid ghost outside the preview (over the buttons, outside the window): clipped at the
      preview edges, or following the cursor everywhere?~~ → Clipped at the preview edges
- [x] ~~Source cell dimming style: dark translucent overlay, desaturation, or something else?~~ →
      Dark translucent overlay
- [x] ~~Unit tests: none (consistent with v1), or create a test project for the extractable logic
      (ghost bounds)?~~ → None
- [x] ~~Overlap with `20260923-drop-zone.md`: once the "Add images" strip exists, appending gets its
      own target. Does the "whole canvas when appending" highlight stay, or does the strip take
      it over — and which of the two tasks is implemented first?~~ → Ghost first; the whole-canvas
      highlight stays, the strip adds its own highlight when hovered
- [ ] Pressing `Delete` or `Ctrl+V` while a cell is being dragged changes the images under the
      drag (found during the implementation run, pre-existing, not fixed): `_pressed` /
      `_dropTarget` keep stale indexes, so the release may swap the wrong images or throw. Cancel
      the drag whenever the images change?

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

### Iteration 2 — 2026-09-23

All seven open questions answered (Q&A #5–#11); the design sections now describe the full solution:

- Ordering: implemented **after Milestone 2**, whose drop onto a cell and replace rule it reuses.
- File drop: Windows' own drag image as the ghost (nothing decoded); the highlight shows
  everything that will receive the file (hovered cell / whole canvas when appending / the cell
  the replace rule picks when full).
- In-grid swap: ghost at ~40 % of the cell, 70 % opacity, anchored at the grab point, clipped at
  the preview's edges; source cell under a dark translucent overlay.
- Unit tests declined.

### Iteration 3 — 2026-09-23

Go for implementation asked (Q&A #12): **No**. The gate holds; the design stays as it is,
waiting for Milestone 2 of `20260923-application-v1.md` to be delivered first.

### Iteration 4 — 2026-09-23

The user asked whether implementation can start. Re-reading the repository before answering:

- **Milestone 2 was already delivered** (`8696907`, `5d2584e`), in parallel with this design.
  The reason stated in Iteration 3 was therefore stale when written; the dependency is met.
- The "Current State" sections described the Milestone 1 code: they now describe the existing
  `OnPreviewDragOver` / `ShowDropTarget` wiring and the single highlight overlay. The planned
  file-drop highlight becomes an extension of `ShowDropTarget` (cell / whole canvas / nothing)
  plus a replace-rule helper shared with `Add`.
- New open question: overlap with the not-yet-implemented `20260923-drop-zone.md`, whose strip
  also highlights during a file drag.

### Iteration 5 — 2026-09-23

Drop-zone overlap settled (Q&A #13): the ghost is implemented first, and the whole-canvas append
highlight is kept when the drop zone arrives; the strip adds its own highlight on hover. No open
question remains.

### Iteration 6 — 2026-09-23 — ✅ Implemented

Go given for **code only** (Q&A #14). Scope frozen as the design sections stand at Iteration 5.
Unit tests stay declined (Q&A #11); the README is not part of the go.

### Iteration 7 — 2026-09-23 — 🧭 Implementation choices

Code delivered on `main` (user's choice) in three commits: in-grid ghost (`aafb58e`), file-drop
highlight (`bae3ca8`), Windows drag image (`cc15f60`). Choices the frozen design did not state:

- **Windows drag image**: checked in the WinForms source — the shell helper is only called when
  `DropImageType` is set, so `OnDragEnter` sets `DropImageType.Copy` (no drop-description text).
- **Ghost bitmap** built once at drag start (high-quality bicubic), then only blitted with 70 %
  alpha, instead of rescaling the cell at every paint.
- **Dimming**: black at alpha 120, a bit lighter than the × button's 150, so the source image
  stays readable.
- **Cancel** = mouse capture loss (Alt+Tab, a dialog); `Esc` does not cancel a drag (it still only
  clears the selection, as before).
- **`ShowDropTarget(Point?)`** replaces `ShowDropTarget(int)`: the preview resolves the target from
  the cursor itself and keeps it as a rectangle, so the cell / canvas / excess-cell cases share one
  code path.
- **`ExcessTarget()`**: name of the helper extracted from `Add`.
- **Empty grid**: the canvas highlight is painted over the dashed empty state.
- **Guards** against a pressed cell that no longer exists (paint and drag start).

Gap found, not fixed (scope freeze) — offered as an open question: `Delete` / `Ctrl+V` during a
swap drag leaves stale indexes.

Not verified on screen: the build passes (0 warnings), but no drag was driven in the running app.

No project rule was broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7 | 2026-09-23 | In-grid ghost, file-drop highlight, Windows drag image; on `main` |
| Unit tests | 2 | 2026-09-23 | Declined by the user (Q&A #11) |
| README | 6 | 2026-09-23 | Not part of the go (code only, Q&A #14) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which drag & drop shows the ghost: in-grid swap, file drop, or both? | Both | 2026-09-23 |
| 2 | What does the ghost look like? | Thumbnail following the cursor + highlight of the target cell | 2026-09-23 |
| 3 | What happens to the source cell during the drag? | Dimmed | 2026-09-23 |
| 4 | Expected depth of the subject? | Straightforward (single scout pass) | 2026-09-23 |
| 5 | Ordering with Milestone 2: after it, or bring drop-onto-cell targeting in here? | After Milestone 2 | 2026-09-23 |
| 6 | File drop ghost: Explorer's drag image, or a thumbnail decoded by the app? | Windows' drag image | 2026-09-23 |
| 7 | File drop highlight: which cells light up? | Everything that will receive the file (hovered cell / whole canvas when appending / replace-rule cell when full) | 2026-09-23 |
| 8 | In-grid ghost: size, opacity and anchoring? | ~40 % of the cell, 70 % opacity, anchored at the grab point | 2026-09-23 |
| 9 | In-grid ghost outside the preview: clipped or everywhere? | Clipped at the preview edges | 2026-09-23 |
| 10 | Source cell dimming style? | Dark translucent overlay | 2026-09-23 |
| 11 | Unit tests: none, or a test project for the ghost bounds? | None | 2026-09-23 |
| 12 | Go for implementation? | No — the gate holds | 2026-09-23 |
| 13 | Overlap with the drop zone: does the whole-canvas append highlight stay, and which task goes first? | Ghost first, canvas highlight kept; the strip adds its own | 2026-09-23 |
| 14 | Go for implementation (second ask)? | Implement the code (code only) | 2026-09-23 |

---

*Last updated: 2026-09-23*
