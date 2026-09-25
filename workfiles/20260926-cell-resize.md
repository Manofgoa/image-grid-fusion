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
| `UI/MainForm.cs` / `UI/LayoutStrip.cs` | Layout picked in the strip, mirror toggle |

---

## Separators

A **separator** is a maximal straight segment of boundary shared by cells on both of its sides.
Dragging it moves **every cell touching it**, on both sides, and **no other cell**. The outer border
of the canvas is not a separator.

| Layout | Separators |
|---|---|
| Single | none |
| Two columns / Two rows / Two thirds + one third | one, between the two cells |
| Three columns / Four columns | one between each pair of neighbour columns |
| Big left, Featured (3 and 4 images) | the long vertical one (featured cell ↔ the stacked cells, which all follow), plus one horizontal separator between each pair of stacked cells |
| Big top (3 and 4 images) | the long horizontal one (featured cell ↔ the row below), plus one vertical separator between each pair of cells of that row |
| Grid (4 images) | the cross — see *Open Questions* |

Mirroring does not change this list, only where the separators sit.

---

## Gesture

- **Hit area**: a band of ±4 logical px (scaled with `LogicalToDeviceUnits`) centred on the
  separator, along its length. The cursor becomes ↔ (`SizeWE`) on a vertical separator and ↕
  (`SizeNS`) on a horizontal one.
- **Drag**: pressing in the band and moving moves the separator along its normal axis; the preview
  is redrawn live.
- **Priority**: on its band, the separator wins over the cell's own gestures (drag / pan, swap
  handle drag). Against the selected cell's effect handles (blur bars snapped onto a cell edge) —
  see *Open Questions*.
- **Locked grid** (during an export): separators are neither highlighted nor draggable, like every
  other grid action.
- Resizing is a **grid** gesture, not an effect: it has no button in the effects toolbar and does
  not depend on the selected cell.

---

## Sizes Lifetime

The sizes belong to the **grid** (the cell slots), not to the images.

| Event | Sizes |
|---|---|
| Another layout is picked in the strip | Reset to the layout's own proportions |
| The number of images changes (add, delete) | Reset — the default layout of the new count applies |
| The mirror toggle is clicked | See *Open Questions* |
| A cell's image is replaced (drop, Ctrl+V, browse) | Kept |
| Two cells are swapped | Kept — the images change slots, the slots keep their sizes |
| The effects toolbar's *Reset* button | See *Open Questions* |
| The app is restarted | Not persisted — every layout starts on its own proportions |

---

## Rendering and Export

- `Compositor` already draws from the layout's cells: a resized grid shows the same way in the
  preview, the PNG / GIF / MP4 exports and video playback, with no dedicated code.
- The tiling stays exact: neighbour cells share each boundary, computed as `floor(size × fraction)`,
  so no pixel is lost or covered twice at any canvas size.
- `CanvasSizer` computes the output width from the **resized** cell fractions: widening a cell that
  holds a large image may raise the output width. The 1200:628 ratio is unchanged.
- Effects keep their geometry in fractions of the cell (RULES — *Scope and State*): the blur bars,
  pan, zoom and fine angle follow the new cell shape; the cover zoom is recomputed for it.
- Text pages take the shape of their cell (`FitPagesToCells`): when they are laid out again during
  a resize — see *Open Questions*.

---

## Constraints

- **Minimum cell size**: see *Open Questions*.
- **Snapping**: see *Open Questions*.

---

## Test Impact

See *Open Questions* — every previous workfile kept the solution test-free, and the solution has no
test project. Behaviours that would be pinned if tests were written:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A resized layout still tiles the canvas exactly (no gap, no overlap) at any canvas size | `GridLayoutTests.cs` (new test project) | Create |
| Moving a separator changes only the cells touching it | `GridLayoutTests.cs` | Create |
| The minimum cell size clamps a separator drag | `GridLayoutTests.cs` | Create |
| Mirroring a resized layout (per the decision taken) | `GridLayoutTests.cs` | Create |
| `CanvasSizer` uses the resized fractions | `CanvasSizerTests.cs` | Create |

---

## Open Questions

- [ ] Grid (4 images): at the cross, the two separators cannot both be split in two — only one of
  the two lines can be broken at a time. Which behaviour?
- [ ] Mirror toggle on a resized layout: are the sizes mirrored with it, or reset?
- [ ] Minimum cell size, below which a separator stops?
- [ ] How are the sizes reset without changing the layout (double-click on a separator, the
  effects toolbar's *Reset*, nothing)?
- [ ] Snapping while dragging a separator (its default position, alignment with a neighbour
  separator, none)?
- [ ] Separator vs the selected cell's blur bars snapped onto the same edge: which one wins?
- [ ] Text pages: laid out again live during the drag, or once on release?
- [ ] Layout strip: does the active thumbnail show the resized proportions?
- [ ] Unit tests: keep the solution test-free, or create a test project for the geometry?

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
| 1 | How does the user resize the cells? | Dragging the separators, on the preview | 2026-09-25 |
| 2 | When a separator moves, what moves with it? | Only the neighbour cells — the grid may become irregular | 2026-09-25 |
| 3 | What happens to the sizes when the layout changes? | Reset to the layout's proportions; not persisted | 2026-09-25 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward — single exploration pass | 2026-09-25 |
| 5 | Grid (4 images): behaviour at the cross? | | |
| 6 | Mirror toggle on a resized layout: mirrored or reset? | | |
| 7 | Minimum cell size? | | |
| 8 | How are the sizes reset without changing the layout? | | |
| 9 | Snapping while dragging a separator? | | |
| 10 | Separator vs blur bars on the same edge: which wins? | | |
| 11 | Text pages: laid out live during the drag or on release? | | |
| 12 | Layout strip: does the active thumbnail show the resized proportions? | | |
| 13 | Unit tests: test-free, or a new test project? | | |

---

*Last updated: 2026-09-26*
