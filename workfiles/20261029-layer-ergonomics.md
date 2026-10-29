# Layer Ergonomics

> Working document — a rotation icon on the layer's rotation handle, and placement guides that snap a
> moved layer onto the grid, the cells and the other layers.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Three improvements to moving a layer on the grid (`workfiles/20261029-layers.md`):

1. Over the **rotation handle** — the round knob above the selected layer's top edge — the **mouse cursor**
   becomes a **rotation cursor**, so the knob reads as "turn"; the green knob itself is unchanged.
2. **Placement guides**: while a layer is dragged, dashed guides show where it lines up with the
   **grid** (its center and edges) and the **cells** (their edges and centers), and the layer
   **snaps** onto them, as an image does in its cell.
3. **Alignment with the other layers**: the same guides, **fainter**, where the moved layer's
   top / middle / bottom (left / center / right) lines up with another layer's — **one line per row or
   column**, however many layers share it.

Components: `UI/GridPreview.Layers.cs` (the layer's mouse gestures, the outline and the rotation handle),
`UI/GridPreview.cs` (the guides' drawing: `PaintGuideLines`, `GuidePen`, `PaintGapBars`, `ApproachOpacity`,
`GuideApproach`), `Composition/GridLayers.cs` (`LayerPlacement`: `Corners`, `MovedBy`).

---

## Rotation Cursor

Today (`PaintLayerSelection`): a stem from the middle of the top edge, then a knob — a disc of
`RotationKnob` = 7 logical px radius, filled green (`HelperColor`), white while hovered or dragged, over
the black halo. The cursor over it is `Cursors.Hand` (`LayerMouseMove`).

- The **cursor** over the knob, and for as long as the knob is dragged, is a **rotation cursor**: a
  circular arrow, drawn like the system cursors — white with a black outline — its hot spot at its
  center (Q&A #5).
- Windows has no such cursor: the app makes it, at the monitor's DPI so it stays sharp, once.
- The **green knob is not touched**: no icon drawn on it, same size, same hit area, same colours (green,
  white while hovered or dragged), same gesture (Shift snapping to 15°).

---

## Placement Guides

### Lines

Every line is vertical (a position on x) or horizontal (on y), in the control's pixels:

| Source | Vertical lines | Horizontal lines | Opacity |
|---|---|---|---|
| **Grid** | Left edge, center, right edge of the canvas (`CanvasBounds`) | Top, middle, bottom | Normal |
| **Cells** | Left edge, center, right edge of every cell as drawn — the Borders' gap shrinking it (`CellBounds`, `Compositor.Cells`) | Top, middle, bottom of every cell | Normal |
| **Other layers** | Left, center, right of each other **shown** layer's box (`GridLayers.Shown`, the selected one left out) | Top, middle, bottom | **Fainter** (half) |

- A **rotated layer** — the moved one or another — counts by its **bounding rectangle**: the smallest
  upright rectangle holding its turned box (`LayerPlacement.Corners`); its middle is the layer's center
  (Q&A #3).
- The moved layer offers **three references per axis**: its bounding rectangle's left, center and right
  (top, middle, bottom).
- **One line per position**: lines at the same position (within half a pixel) are one line — several
  layers aligned on one row give one line, and a grid or cell line wins over a layer line at the same
  place (its opacity is the normal one).

### Snapping

- **Magnetic**, like an image in its cell (Q&A #1): a dragged layer **snaps** a reference onto a line it
  comes near, on each axis independently; **Shift** held ignores every line (no snap, no guide), as for
  the cells' pan.
- The **snapping model** — proximity or resistance — is an open question.
- A snapped layer's center stays inside the grid (`LayerPlacement.MovedBy`'s clamp).

### Showing

- **Approach** (RULES.md § Guides Approach, which a new guide follows): on each axis, the **nearest line**
  within the approach distance (*Guides show from*, the ⚙ menu's setting) shows its guide, its opacity
  growing from 20 % at the reach to full at the line, with a **gap bar** from the layer's point (its center
  line, or the edge nearest the guide with *Gap bars from* edges) to the guide; 0 % shows a guide only once
  the layer rests on its line. The base of the distance — the grid, the cell — is an open question.
- **Resting**: every line a reference of the layer lies exactly on (within half a pixel) shows, at full
  opacity — the grid's and cells' lines — or at the fainter one — the other layers' — **all of them**, not
  only the one it snapped to: a layer whose left and right both line up shows both.
- The extent of a line — the whole grid, or only the stretch it aligns — is an open question.
- Drawn in the helper indicators' dashed green over the black halo (`GuidePen`, `PaintGuideLines`), in
  the **preview only**, while the drag lasts; gone on release.

### Out of Scope

- The **layer edit view**: the layer is a cell there, the cells' own guides apply, untouched.
- The **rotation** gesture: no guide; Shift's 15° steps stay its only snap.
- Hidden layers: neither lines nor snapped.
- The undo history: unchanged — a drag is one step, snapped or not.

---

## Test Impact

There is no test project (CONTRIBUTING.md): the change is checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

- [x] ~~Do the guides snap the layer?~~ → Yes, magnetic like an image in its cell, Shift ignoring them, shown from the approach distance (Q&A #1)
- [x] ~~Which grid references give a guide?~~ → The grid's center and edges, and every cell's edges and center (Q&A #2)
- [x] ~~What is a rotated layer's top / middle / bottom?~~ → Its bounding rectangle, its middle the layer's center (Q&A #3)
- [x] ~~Rotation icon: which style on the knob?~~ → None on the knob: the mouse cursor becomes a rotation cursor over it (Q&A #5)
- [ ] Snapping model: by proximity (the layer follows the mouse, snapped while a reference is within a few pixels of a line) or by resistance (held on a line until dragged 24 px past it, as the cells' pan)?
- [ ] Approach distance: a share of the grid, of the cell under the layer, or a fixed number of pixels?
- [ ] Extent of a guide: across the whole grid, or only the stretch it aligns (the moved layer to the farthest aligned one, a cell's line along that cell)?
- [ ] Wheel resize of a layer: snap / show guides too, or the drag only?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Scoping batch answered (Q&A #1–4): magnetic guides like the cells' pan, the grid's and the cells'
references, a rotated layer by its bounding rectangle, a straightforward subject (one exploration pass).
The exploration found the rotation knob a 7 px disc (`PaintLayerSelection`) and the cells' magnet
(`PanMagnet`) built for one image between two edges and a center, not for many lines: the layers get a
snap of their own, drawn with the cells' guide pen, approach opacity and gap bars. Five questions left
open: the icon's style, the snapping model, the approach distance's base, a guide's extent, the wheel.

### Iteration 2 — 2026-10-29

Asked to choose among three icons drawn on the knob, the user wants the **cursor** to take the rotation
image, not the green drawing (Q&A #5): § Rotation Icon becomes § Rotation Cursor — a rotation cursor
over the knob and while it is dragged, the knob unchanged. The four other questions were not answered
and are asked again.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Do the placement guides snap the layer? | Yes, magnetic like an image in its cell: Shift ignores them, shown from the approach distance | 2026-10-29 |
| 2 | Which grid references give a guide? | The grid and the cells: the grid's center and edges, every cell's edges and center | 2026-10-29 |
| 3 | What counts as a rotated layer's top / middle / bottom? | Its bounding rectangle, as seen on screen | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-29 |
| 5 | Rotation icon: which style on the knob? | The cursor takes the rotation image, not the green drawing | 2026-10-29 |
| 6 | Snapping model: proximity or resistance? | | |
| 7 | Approach distance: a share of the grid, of the cell, or fixed pixels? | | |
| 8 | Extent of a guide: the whole grid or the stretch it aligns? | | |
| 9 | Wheel resize: snap and guides too, or the drag only? | | |

---

*Last updated: 2026-10-29*
