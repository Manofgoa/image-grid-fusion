# Cell Resize

> Working document — resizing the cells of the grid by dragging the separators between them.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every layout is a fixed arrangement of units (`GridLayout`, e.g. *Two thirds + one third* is
2 units + 1 unit on a 3 × 1 grid): the cells always keep the catalog proportions. The goal is to let
the user **drag the separator between two cells** to give one of them more room.

Agreed during scoping (Q&A #1–#4):

- The gesture is **dragging the separators**, directly on the preview — no toolbar button, no
  numeric input.
- A separator moves **only the cells adjacent to it**, not a whole grid line: the grid may become
  irregular (mosaic-like).
- Changing the layout **resets** the sizes to the layout's own proportions; nothing is persisted
  between two launches.

Relevant components:

| Component | Role today |
|---|---|
| `Composition/GridLayout.cs` | Catalog of layouts in units; `Cells(canvas)` tiles the canvas with `floor(size × u / n)` boundaries; `CellFractions()`; `Mirrored()` |
| `Composition/CanvasSizer.cs` | Output width from the cell fractions (no image downscaled), clamped to [1200, 4096] |
| `Composition/Compositor.cs` | Draws every cell from `layout.Cells(canvas)` — preview, exports, playback |
| `UI/GridPreview.cs` | Holds the active layout (`SetLayout`, `OnImagesChanged`), hit-testing (`CellBounds`, `UpdateHover`), mouse gestures, text pages refit (`FitPagesToCells`) |
| `UI/MainForm.cs` / `UI/LayoutStrip.cs` | Layout picked in the strip, mirror toggle, effects toolbar *Reset* button |

---

## Separators

A **separator** is a straight segment of boundary shared by cells on both of its sides. Dragging it
moves **every cell touching it**, on both sides, and **no other cell**. The outer border of the
canvas is not a separator.

| Layout | Separators |
|---|---|
| Single | none |
| Two columns / Two rows / Two thirds + one third | one, between the two cells |
| Three columns / Four columns | one between each pair of neighbour columns |
| Big left, Featured (3 and 4 images) | the long vertical one (featured cell ↔ the stacked cells, which all follow), plus one horizontal separator between each pair of stacked cells |
| Big top (3 and 4 images) | the long horizontal one (featured cell ↔ the row below), plus one vertical separator between each pair of cells of that row |
| Grid (4 images) | the cross — see below |

Mirroring does not change this list, only where the separators sit.

### Grid (4 images) — the Cross (Q&A #5)

Only one of the two lines can be broken at a time, otherwise the cells would no longer tile the
canvas. The behaviour is **dynamic**:

| State of the cross | Separators |
|---|---|
| **Aligned** (both lines straight — the starting state) | Four arms: top, bottom, left, right. Dragging an arm moves only the two cells it separates, which breaks its line |
| **Vertical line broken** (top and bottom arms apart) | Top arm and bottom arm each move their row's two cells; the horizontal line is whole and moves all four cells |
| **Horizontal line broken** (left and right arms apart) | Symmetric: left and right arms each move their column's two cells; the vertical line is whole |

A broken line becomes straight again when its two arms are realigned — the alignment snapping makes
that exact — and the cross is back to the *Aligned* state.

This needs no special case: `GridLayout.Separators()` groups the cells along a boundary line by
the cells they **face over a stretch** of it; cells meeting at a single point do not face each other.
The cross's four arms, its whole line once the other is broken, and the separators of every other
layout all follow from that one definition.

---

## Gesture

- **Hit area**: a band of ±4 logical px (scaled with `LogicalToDeviceUnits`) centred on the
  separator, along its length; where several bands overlap (the cross's centre), the nearest
  separator wins. The cursor becomes ↔ (`SizeWE`) on a vertical separator and ↕ (`SizeNS`) on a
  horizontal one.
- **Drag**: pressing in the band and moving moves the separator along its normal axis; the preview
  is redrawn live — only the cells it moves, drawn fast into the cached preview (they keep covering
  the same area) — then in full, smoothed, on release. Pressing a separator does not change the
  selected cell.
- **Double-click** on a separator puts it back at its position in the layout's own proportions
  (Q&A #8). On the Grid's cross, it applies to the arm (or whole line) under the cursor. When its
  neighbours moved so far that its own position would take a cell below the minimum, it stops at the
  minimum instead.
- **Priority**: on its band, the separator wins over the cell's own gestures (drag / pan, swap
  handle drag). The selected cell's **effect handles win over the separator** on their own hit area
  (Q&A #10), as RULES — *On-Cell Handles* already states: a blur bar snapped onto the edge is dragged,
  not the separator; the separator stays reachable once the blur tab is no longer selected or the blur is off,
  or from the neighbour cell.
- **Locked grid** (during an export): separators are neither highlighted nor draggable, like every
  other grid action.
- Resizing is a **grid** gesture, not an effect: it has no tab in the effects toolbar and does
  not depend on the selected cell.

---

## Constraints

- **Minimum cell size** (Q&A #7): every cell keeps at least **10 % of the canvas width** (for a
  vertical separator) or **height** (for a horizontal one). A separator stops where one of the cells
  it moves would go below; relative to the canvas, so the preview and every export size agree.
- **Snapping** (Q&A #9): within **6 logical px** (scaled with `LogicalToDeviceUnits`, like the blur
  bars), a dragged separator snaps exactly onto:
  - its **own position** in the layout's proportions;
  - a **parallel separator it can align with** — every other separator of the same axis is a
    target, but the minimum size makes only the other arm of the Grid's broken line reachable.

  The nearest target wins. Snapping never takes a cell below the minimum size.

---

## Sizes Lifetime

The sizes belong to the **grid** (the cell slots), not to the images.

| Event | Sizes |
|---|---|
| Another layout is picked in the strip | Reset to the layout's own proportions |
| The **active** thumbnail is clicked again (Q&A #12) | Reset to the layout's own proportions, the mirror state kept (`LayoutStrip.ActiveLayoutClicked`) |
| The number of images changes (add, delete) | Reset — the default layout of the new count applies |
| The mirror toggle is clicked (Q&A #6) | **Mirrored** with the layout: the big cell stays big, on the other side |
| A cell's image is replaced (drop, Ctrl+V, browse) | Kept |
| Two cells are swapped | Kept — the images change slots, the slots keep their sizes |
| A separator is double-clicked | That separator only is reset |
| The effects toolbar's *Reset* button (Q&A #8, #14) | **All the separators of the grid** are reset, on top of the selected cell's effects. The button stays disabled with no cell selected, and is enabled whenever the grid is resized, even when the cell's effects are all at their defaults; its tooltip mentions the sizes |
| The app is restarted | Not persisted — every layout starts on its own proportions |

---

## Rendering and Export

- `Compositor` already draws from the layout's cells: a resized grid shows the same way in the
  preview, the PNG / GIF / MP4 exports and video playback, with no dedicated code.
- The tiling stays exact: neighbour cells share each boundary — the very same fraction — computed
  as `floor(size × fraction + 1e-9)` (`GridLayout.Boundary`), so no pixel is lost or covered twice at
  any canvas size, and an unresized layout falls on the same pixels as before.
- The layout raises `LayoutChanged` once a separator is **released** (or reset), not at each step
  of the drag: the strip and the toolbar buttons follow then.
- Mirroring a resized layout flips its fractions; an unresized one starts on its mirrored units
  again, so it stays exactly unresized.
- `CanvasSizer` computes the output width from the **resized** cell fractions: widening a cell that
  holds a large image may raise the output width. The 1200:628 ratio is unchanged.
- Effects keep their geometry in fractions of the cell (RULES — *Scope and State*): the blur bars,
  pan, zoom and fine angle follow the new cell shape; the cover zoom is recomputed for it.
- **Text pages** take the shape of their cell (`FitPagesToCells`) and are laid out again **once, on
  release** of the separator (Q&A #11). During the drag, the current page is drawn into the moving
  cell like any other image.
- **Layout strip** (Q&A #12): the thumbnails keep the catalog shapes; the active one does not
  reflect the resized proportions.

---

## Test Impact

No unit test is created or updated: the user chose to keep the solution test-free (Q&A #13), as
every previous workfile did. The exact tiling of a resized grid, the separators moving only their
neighbour cells, the Grid's dynamic cross, the 10 % minimum, the snapping, the mirrored sizes and
`CanvasSizer` fed the resized fractions stay untested by decision, not because nothing testable
changes. Verification is manual: drag every separator of each layout (mirrored or not), break and
realign the Grid's cross, push a cell to its minimum, double-click a separator, click the active
thumbnail, Reset with a cell selected, a text cell resized, then a copy / save (PNG, GIF and MP4).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (test-free by decision, Q&A #13) | — | — |

---

## Open Questions

- [x] ~~Grid (4 images): at the cross, the two separators cannot both be split in two — only one of
  the two lines can be broken at a time. Which behaviour?~~ → Dynamic: four arms while aligned; the
  first arm moved breaks its line, the other line stays whole until realigned
- [x] ~~Mirror toggle on a resized layout: are the sizes mirrored with it, or reset?~~ → Mirrored
- [x] ~~Minimum cell size, below which a separator stops?~~ → 10 % of the canvas side
- [x] ~~How are the sizes reset without changing the layout (double-click on a separator, the
  effects toolbar's *Reset*, nothing)?~~ → Double-click on a separator (that one), and the effects
  toolbar's *Reset* (the whole grid)
- [x] ~~Snapping while dragging a separator (its default position, alignment with a neighbour
  separator, none)?~~ → Both, within 6 logical px
- [x] ~~Separator vs the selected cell's blur bars snapped onto the same edge: which one wins?~~ →
  The blur bar
- [x] ~~Text pages: laid out again live during the drag, or once on release?~~ → On release
- [x] ~~Layout strip: does the active thumbnail show the resized proportions?~~ → No, catalog shapes;
  clicking the active thumbnail resets the sizes
- [x] ~~Unit tests: keep the solution test-free, or create a test project for the geometry?~~ →
  Test-free
- [x] ~~Effects toolbar *Reset*: which sizes does it reset?~~ → The whole grid

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial proposal from the scoping answers (Q&A #1–#4) and a single read-only pass on the code:
separators defined as maximal shared segments, moving only the cells touching them; a ±4 px drag
band with its own cursor; sizes owned by the grid slots, reset on any layout or image-count change,
kept on replacement and swap, never persisted; rendering and export untouched beyond the layout's
cells, `CanvasSizer` fed the resized fractions. Nine questions left open.

### Iteration 2 — 2026-09-26

Geometry answers (Q&A #5–#8): the Grid's cross is dynamic (four arms while aligned, the first arm
moved breaks its line); the mirror toggle mirrors the sizes; minimum cell size 10 % of the canvas
side; sizes reset by a double-click on a separator and by the effects toolbar's *Reset*. A new
question emerged on the scope of that *Reset* (Q&A #14).

### Iteration 3 — 2026-09-26

Interaction answers (Q&A #9–#12): snapping to the separator's own position and to an alignable
parallel separator, within 6 logical px; the blur bars win over the separator on their hit area;
text pages laid out again on release; the strip thumbnails keep the catalog shapes. The chosen
thumbnail answer also makes a click on the **active** thumbnail reset the sizes — a click that does
nothing today (`LayoutStrip` ignores it) — added to *Sizes Lifetime*.

### Iteration 4 — 2026-09-26

Last answers (Q&A #13–#14): the solution stays test-free; the effects toolbar's *Reset* resets every
separator of the grid, on top of the selected cell's effects. No question remains open.

### Iteration 5 — 2026-09-26

Wording aligned with the updated RULES (effect tabs with an activation checkbox; blur bars shown
while the tab is selected and the effect is on): the resize has no *tab* in the effects toolbar, and
the separator is reachable again once the blur tab is unselected or the blur is off. No behaviour
change.

### Iteration 6 — 2026-09-26 — ✅ Implemented

Go given for code, unit tests and documentation (unit tests: none, test-free by decision,
Q&A #13). The run stays on `main`, the repository's standing choice.

### Iteration 7 — 2026-09-26 — 🧭 Implementation choices

No project rule was broken. Choices the frozen design did not state, now in the domain sections:

- **Branch**: `main`, without asking — the standing choice recorded for this repository.
- **One definition for every separator**: cells facing each other over a stretch of a boundary line
  (`GridLayout.Separators()`); the Grid's dynamic cross follows from it, with no special case.
- **Hit test**: the nearest separator wins where bands overlap; pressing one keeps the selection.
- **Live drag**: only the moved cells are drawn again, fast, into the cached preview; the grid is
  drawn in full, smoothed, on release, when the frames are decoded at the new size and the text
  pages laid out again. `LayoutChanged` is raised on release, not at each step.
- **Snapping targets**: the separator's own position plus every parallel separator, nearest first;
  the 10 % minimum leaves only the Grid's other arm reachable.
- **Double-click** on a separator whose own position would break the minimum stops at the minimum.
- **Reset button** enabled whenever the grid is resized, even with the cell's effects at defaults.
- **Active thumbnail**: a new `LayoutStrip.ActiveLayoutClicked` event; the active thumbnail is
  painted from `WithDefaultSizes()`, mirror kept.
- **Pixels**: `GridLayout.Boundary` = `floor(size × fraction + 1e-9)`, so unresized layouts keep
  their former pixels; mirroring an unresized layout starts again on its mirrored units.
- **Documentation**: a *Resizing the cells* section in the README (plus the Reset, canvas size, text
  and mirror mentions); a RULES bullet saying separators resize the grid and are no effect; GLOSSARY
  entries *Separator* and *Arm*.
- **Build**: `bin\Debug` was locked by an app instance not started by this run; the compile was
  checked, and the delivery build goes to a separate output folder rather than killing that instance.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 6, 7 | 2026-09-26 | `GridLayout` + `Separator`, `GridPreview` gesture, Reset / active thumbnail wiring — three commits |
| Unit tests | 6 | 2026-09-26 | Not applicable — test-free by decision (Q&A #13); geometry checked once with a throwaway script (tiling at 4 canvas sizes, cross states, mirror) |
| README | 6, 7 | 2026-09-26 | *Resizing the cells* section; RULES and GLOSSARY updated in their own commit |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the user resize the cells? | Dragging the separators, on the preview | 2026-09-25 |
| 2 | When a separator moves, what moves with it? | Only the neighbour cells — the grid may become irregular | 2026-09-25 |
| 3 | What happens to the sizes when the layout changes? | Reset to the layout's proportions; not persisted | 2026-09-25 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward — single exploration pass | 2026-09-25 |
| 5 | Grid (4 images): behaviour at the cross? | Dynamic — the first arm moved breaks its line; free again once realigned | 2026-09-26 |
| 6 | Mirror toggle on a resized layout: mirrored or reset? | Mirrored | 2026-09-26 |
| 7 | Minimum cell size? | 10 % of the canvas side | 2026-09-26 |
| 8 | How are the sizes reset without changing the layout? | Double-click on a separator, and the effects toolbar's *Reset* | 2026-09-26 |
| 9 | Snapping while dragging a separator? | Own position + alignment with a parallel separator | 2026-09-26 |
| 10 | Separator vs blur bars on the same edge: which wins? | The blur bar | 2026-09-26 |
| 11 | Text pages: laid out live during the drag or on release? | On release | 2026-09-26 |
| 12 | Layout strip: does the active thumbnail show the resized proportions? | No, catalog shapes; clicking it again resets the sizes | 2026-09-26 |
| 13 | Unit tests: test-free, or a new test project? | Test-free | 2026-09-26 |
| 14 | Effects toolbar *Reset*: which sizes does it reset? | The whole grid | 2026-09-26 |

---

*Last updated: 2026-09-26*
