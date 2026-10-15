# Layout Auto Size

> Working document — two **Auto** buttons in the layout strip resizing the current layout's
> separators so the images, shown whole, line up: same height side by side, same width stacked.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The user's request: in the layout strip, an **Auto** button that resizes the current layout, as close
to the images' ratios as it can, so that every image in it, shown in *Contain*, gets the same height.
The screenshot behind it: two columns, a 16:9 video and a 4:3 photo, both shown whole — the video
fills its cell's width with bands above and below, the photo too, at another height; their tops and
bottoms do not line up.

Agreed so far (Q&A 1–4):

- **Two buttons**, one per axis — one Auto vertical, one Auto horizontal.
- A **one-shot action**: a click moves the separators once; nothing follows afterwards.
- Every image gets its **zoom recomputed as on arrival**: the zoom Contain gives in its new cell, as a
  free zoom, centered, Contain unpressed (`ImageLook.WithArrivalZoom`).
- Exploration sized as **straightforward**.

Relevant components:

| Component | Role here |
|---|---|
| `Composition/GridLayout.cs` | The edges of the cells, `Separators()`, `Range(separator)` (the 10 % minimum), `WithSeparator` |
| `UI/LayoutStrip.cs` | The strip: mirror toggle, thumbnails, More group — where the buttons go |
| `UI/GridPreview.cs` | `ApplySizes` (new sizes, the ratio and the pages follow), `ShowWhole` (the arrival zoom), `CellBounds` |
| `Composition/FitCalculator.cs` | `FitZoom(cell, shown, ZoomFit.Contain)` |
| `Composition/ImageLook.cs` | `Shown` — the image as seen: cropped, then turned |
| `UI/MainForm.cs` | Wires the strip's events to the preview (`MirrorToggled`, `ActiveLayoutClicked`) |

---

## Behaviour

### The Principle — Cells Shaped Like Their Images

An image shown whole in a cell leaves no band when the cell has the image's shape. Auto moves the
separators of **one orientation** so that every cell's shape is **as close as possible to its image's
shape** (`ImageLook.Shown`): it minimises, over the cells, the squared difference between the log of
the cell's ratio and the log of its image's ratio.

In a row of cells side by side, that gives **widths proportional to the images' ratios**: every image,
shown whole, gets the **same height** — the canvas's height when the row is wide enough, a smaller
common height with equal bands above and below otherwise. In a column of stacked cells, the mirror:
heights proportional to the inverse ratios, every image the **same width**.

The screenshot's case (Twitter format, 1200 × 628): 16:9 + 4:3 → widths 57 % / 43 %; both images
386 px high, with the same bands above and below.

### The Two Buttons

- One button moves the **vertical separators** only (cells side by side resized: images the same
  height); the other the **horizontal separators** only (stacked cells resized: images the same
  width). Which name and icon each one carries: Open Questions.
- A button whose orientation has **no separator** in the current layout (*Two columns* has no
  horizontal one) is **disabled**, its tooltip saying why — the mirror toggle's rule.
- Disabled, like the rest of the strip, while there is no image and while exporting.

### What Is Taken Into Account

- The image **as seen** (`ImageLook.Shown`): crop, quarter turns — like the fitting rule and the Free
  format.
- The **cells as drawn** — the Borders' gap shrinking them included — so the bands come out equal on
  the real cells.
- **Texts are left out** (they take their cell's shape, like in the Free format): a separator moved
  only by texts stays where it is; a text beside images takes what is left.
- The **10 % minimum** of a separator (`GridLayout.Range`) holds: an extreme ratio is approached, not
  reached.
- A **mirrored** layout is resized as it stands.

### After the Click

- The separators move in **one step** (`GridPreview.ApplySizes`): one undo step, the text pages laid
  out again, the Free ratio computed again — in a single row, the Free format then tends to the
  images' combined ratio, the bands vanishing.
- Every image but a text gets the **arrival zoom** in its new cell (`ShowWhole`'s computation, applied
  to all of them): centered, Contain unpressed, `ZoomOnArrival` set again. Its own zoom and position
  are replaced.
- The active thumbnail keeps drawing the layout's own proportions; clicking it, or the effects Reset,
  still brings the separators back.

---

## Test Impact

The app has **no test project**. The geometry (the separator positions Auto computes) is a pure
function in `Composition/` that a test could pin. Whether a test project is created for it: Open
Questions.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(pending Q8)* | | |

---

## Open Questions

- [ ] Which button moves which separators, and their names / icons ("Auto vertical", "Auto horizontal")?
- [ ] Where do the two buttons go in the layout strip?
- [ ] On layouts where a cell spans several rows or columns (Big left, Featured…), keep the principle
  (each cell shaped like its image), or force strictly equal heights across all images?
- [ ] Create a test project for the geometry, or no tests?

---

## Design Iterations

### Iteration 1 — 2026-10-15

Initial design from the request and the scoping batch (Q&A 1–4). One principle covers every layout:
the separators of one orientation move so each cell's shape is as close as possible to its image's,
which gives equal heights in a row and equal widths in a column. Two one-shot buttons, disabled when
their orientation has no separator; the arrival zoom recomputed for every image. Texts left out, the
10 % minimum and the Borders' gap honoured.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which layouts does Auto act on? | One Auto vertical button and one Auto horizontal button | 2026-10-15 |
| 2 | One-shot action or persistent mode? | One-shot action | 2026-10-15 |
| 3 | What happens to each image's zoom? | Recomputed as on arrival (Contain's zoom as a free zoom, centered) | 2026-10-15 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-15 |
| 5 | Which button moves which separators, and their names / icons? | | |
| 6 | Where do the buttons go in the strip? | | |
| 7 | Spanning cells: shapes like the images, or strictly equal heights? | | |
| 8 | Test project for the geometry? | | |

---

*Last updated: 2026-10-15*
