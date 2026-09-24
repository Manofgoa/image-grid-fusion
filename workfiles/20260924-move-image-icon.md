# Move Image Icon

> Working document — make the drag handle that swaps a cell's image easier to grab: a large handle in the middle of the hovered cell.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, swapping two images is done by dragging a small round **✥** handle (24 logical px),
placed just below the **×** in the top-right corner of the hovered cell. A drag anywhere else
in the cell pans the image within it (when zoomed in above 100 %). The handle is small and far
from where the eye is, so it is awkward to aim at.

Goal: replace it with a **large handle centered in the hovered cell**, mouse only, without
touching the other hover controls.

Relevant component: `src/ImageGridFusion/UI/GridPreview.cs` — `HandleBounds`, `PaintHandle`,
`UpdateHover` (hot state, `SizeAll` cursor), `OnMouseDown` (handle → swap, elsewhere → pan).

---

## Agreed Scope

Settled with the user before exploration (see Q&A 1–4):

- **Direction**: a large handle in the center of the cell, shown on hover.
- **Devices**: mouse only — hover is available to reveal the handle; no touch behaviour.
- **Scope**: the swap handle only. The ×, the toolbar, the zoom slider and the page slider stay
  as they are.

---

## Current Behaviour (from the code)

| Aspect | Today |
|---|---|
| Position | Below the ×: `CloseBounds(cell)` shifted down by `ButtonGap` (top-right corner) |
| Size | `24` logical px, same as the × |
| Look | Black disc, alpha 150 (230 when hot), four white arrows out of the center |
| Shown when | The cell is hovered, not while dragging, not while the grid is locked (export) |
| Press on it | Starts a swap drag (ghost at 40 %, drop target outlined), cursor `SizeAll` |
| Press elsewhere in the cell | Pans the image when its zoom is above 100 %, otherwise does nothing |

The other hover controls occupy the edges: × top-right, toolbar rows top-left, zoom slider
along the left edge, page slider along the bottom. **The center of the cell is free.**

---

## Proposed Design

### Position and size

- The handle is a disc **centered in the cell**, **48 logical px** in diameter (twice today's),
  the same in every cell that has room for it.
- The old handle below the × is **removed**: the central one replaces it.

### Look

- Same visual language as today: dark translucent disc, four white arrows (✥), arrow strokes
  thicker in proportion to the size.
- Resting opacity **like the ×** (alpha 150), 230 when the mouse is over the handle — as today.

### Interaction

- Unchanged: press on the handle → swap drag; press elsewhere → pan (zoomed image). The center
  zone taken by the handle is simply no longer a pan zone — panning still works from anywhere
  else in the cell.
- Unchanged: cursor `SizeAll` over the handle, hidden while dragging and while locked.

### Small cells

- When the centered 48 px disc would overlap another control (×, toolbar rows, zoom slider,
  page slider), it **shrinks**, still centered, down to **24 px**.
- Below 24 px, it **falls back** to today's spot and size: 24 px just below the ×.

### README

- The *No image list* section still says `Ctrl` + drag swaps a zoomed image — stale since
  `d3f0826` removed it — and never mentions the handle. The line describing the swap gesture
  is updated to describe the central handle.

---

## Test Impact

No unit test project exists in this app, and the change is pure WinForms hit-testing and
painting in `GridPreview`: **nothing to create or update**. Checked by running the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; UI painting and hit-testing only) | — | — |

---

## Open Questions

- [x] ~~Handle size: fixed (e.g. 48 px, twice today's), or proportional to the cell (e.g. a quarter of its short side, clamped between 32 and 64 px)?~~ → Fixed 48 px
- [x] ~~The old small handle below the ×: removed (replaced by the central one), or kept as well?~~ → Removed
- [x] ~~Visibility while the mouse is in the cell but not on the handle: as opaque as the × (alpha 150), or discreet (fainter) so it hides less of the image, then fully opaque when hovered?~~ → Like the × (alpha 150, 230 when hot)
- [x] ~~Cells too small for a central handle clear of the other controls: shrink it down to 24 px, and below that fall back to today's spot under the ×? Or always keep it centered, overlapping if need be?~~ → Shrink down to 24 px, then fall back under the ×

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial proposal from the scoping answers (central handle, mouse only, swap handle only) and
the scout pass on `GridPreview.cs`: a large ✥ disc centered in the hovered cell, same drag
behaviour, the center simply stops being a pan zone. Sizing, removal of the old handle,
resting visibility and small-cell fallback left as Open Questions. README swap line to fix.

### Iteration 2 — 2026-09-24

Open Questions answered (Q&A 5–8): fixed 48 px, old handle removed, resting opacity like the ×,
and in cells too small the centered disc shrinks down to 24 px before falling back under the ×.
The 48 px is the size wherever it fits; the shrink only applies when it would overlap another
control. Design sections updated; no Open Question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project, UI only |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which direction for moving: whole image draggable, top bar on hover, big central handle, or propose after exploring? | Big central handle | 2026-09-24 |
| 2 | Which devices must it work on? | Mouse only | 2026-09-24 |
| 3 | Scope: moving only, or harmonize the other cell icons too? | Moving only | 2026-09-24 |
| 4 | Is the subject simple or tricky / long? | Simple | 2026-09-24 |
| 5 | Handle size: fixed or proportional to the cell? | Fixed 48 px | 2026-09-24 |
| 6 | Old handle below the ×: removed or kept? | Removed | 2026-09-24 |
| 7 | Resting visibility: as opaque as the × or discreet? | Like the × | 2026-09-24 |
| 8 | Small cells: shrink then fall back under the ×, or always centered? | Shrink, then fall back under the × | 2026-09-24 |
| 9 | Go for implementation? | | 2026-09-24 |

---

*Last updated: 2026-09-24*
