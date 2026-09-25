# Pan Beyond Cell Bounds

> Working document — let an image be moved past the edges of its cell, so a part lying on the
> image's border can be centered; the uncovered area gets the band color, as when zooming out.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today an image can only be moved (panned) when zoomed in above 100 %, and only until an image edge
reaches the cell edge: a detail on the border of the image can never be brought to the center of
the cell. This workfile lets the image go **past the cell's edges**, the area it uncovers being
filled with the **band color**, under the same rules as the bands left by a zoom-out.

Scope agreed with the user (Q&A #1–#4):

- **Limit**: the image may almost leave the cell, but a **minimum margin of it stays visible** so it
  can always be grabbed back.
- **Gesture**: the same drag as today, with a **magnetic stop** at the edges — the image first stops
  where it stops today, then goes on if the drag insists. **Holding Shift ignores the magnetic stop.**
- **Zoom**: available **at every zoom, 100 % and zoom-out included**, not only when zoomed in.
- Exploration depth: straightforward — a single scout pass.

Relevant components: `Composition/FitCalculator.cs` (`Compute` — where the part shown is clamped
today), `Composition/ImageLook.cs` (`Focus`, `WithFocus`, `WithZoom`), `UI/GridPreview.cs`
(`OnMouseDown` / `OnMouseMove` pan branch, `PanBy`, wheel zoom anchor, `ZoomTo`),
`Composition/Compositor.cs` (`DrawCell`, band fill), `Composition/BandColor.cs` (`For`).

---

## Current Behaviour (findings)

- **Position model**: `ImageLook.Focus` is the point of the oriented image kept at the center of the
  cell, in fractions of the image; `WithFocus` clamps it to `[0, 1]`.
- **Clamp at draw time**: `FitCalculator.Compute(cell, image, threshold, zoom, focus)` centers the
  source part on the focus, then clamps it inside the image (`Math.Clamp(..., 0, image − part)`), so
  the cell never shows beyond the image on an axis where the image overflows. **An axis where the
  image does not fill the cell stays centered**, whatever the focus.
- **Pan gesture** (`GridPreview.OnMouseMove`): a press outside the swap handle starts `_panning`;
  the drag calls `PanBy` **only when `Look.Zoom > 1`** — at 100 % or below the drag moves nothing.
  `PanBy` recomputes the focus from where the image is actually shown (the clamped fit), so a stored
  focus never drifts past the visible position.
- **Zoom**: `ImageLook.WithZoom` puts the focus back to the center at 100 % or below. The wheel zoom
  keeps the point under the cursor under it (above 100 % only), then the clamp applies.
- **Background**: `Compositor.DrawCell` fills the whole cell with `BandColor.For(part shown)` before
  drawing the image — the color of the sides of the **part actually shown**, so it already follows
  the zoom and the focus (README, *Crop threshold*). The uncovered area of a moved image gets this
  fill for free.
- **Blur effect**: its rectangle is stored in fractions of the cell (`BlurEffect.Area(cell)`), so it
  stays in place when the image moves — unchanged by this work.
- **Exports**: `Compositor.Render` / `Draw` go through the same `DrawCell` → `FitCalculator`, so the
  exports and video playback follow automatically.
- **Tests**: the solution has **no test project**; every previous workfile stayed test-free.
- **README**: lines 20–22 describe the zoom and "Drag a zoomed-in image to move it within its cell";
  line 163 says the band color follows the zoom and the focus.

---

## Design

### Position and Limits

Per axis, the image drawn (its whole rectangle at the current scale and zoom) sits in one of three
zones relative to the cell:

| Zone | Image larger than the cell on that axis | Image smaller than the cell on that axis |
|---|---|---|
| **In range** (reachable as today) | It covers the cell: no image edge inside the cell | It lies inside the cell: no image edge outside the cell |
| **Beyond** (new) | An image edge has entered the cell, uncovering band color | An image edge has left the cell, cutting the image |
| **Hard limit** | The image still overlaps the cell by the **minimum margin** | Same |

- The **magnetic stops** are the boundaries of the *in range* zone: the positions where an image
  edge is aligned on a cell edge.
- The **minimum margin** is the visible overlap the image always keeps with the cell on each axis
  (value: Open Question), capped by the image's own size on that axis.
- `FitCalculator.Compute` no longer clamps the part shown inside the image, nor centers an axis the
  image does not fill: it places the image rectangle with the focus at the cell center, clamps that
  placement to the hard limit only, and returns the **intersection** of the image with the cell as
  `Destination`, with the matching `Source`. The in-range clamp moves to the gesture (magnetic stop).
- `ImageLook.WithFocus` no longer clamps the focus to `[0, 1]` — a focus beyond the image is how an
  image past the edge is stored. The limit depends on the cell size, so it can only be applied where
  the cell is known: in `FitCalculator.Compute` at draw time, and in `PanBy` (which already starts
  from the position actually shown).
- The position stays stored as a focus in fractions of the image: it survives resizing, layout
  changes and swaps like today.

### Drag Gesture

- The drag moves the image **at every zoom**, 50 % … 400 % (the `Zoom > 1` guard is removed). A
  press on the swap handle still swaps; a drag elsewhere still never turns into a swap.
- **Magnetic stop** (without Shift): while the image moves **outward** across a stop, it stays on the
  stop until the drag has gone a **resistance distance** past it (value: Open Question), scaled with
  `LogicalToDeviceUnits`; it then jumps to the mouse position and follows it, up to the hard limit.
  Moving back **inward** is free: no stop is felt on the way back in.
- The resistance is counted **per axis**: a diagonal drag can pass the stop on one axis and stay held
  on the other.
- **Shift held**: no magnetic stop at all, only the hard limit. It is read on every mouse move, so it
  can be pressed or released during the drag; releasing it leaves the image where it is.
- Live display, fast then full-quality render, and the cursor stay as the live pan does today.

### Zoom

- Behaviour of the zoom (slider and wheel) for a moved image, and whether 100 % and below still
  recenters it: Open Question.

### Background

- Unchanged rule: the cell is filled with `BandColor.For(part shown)`, the part shown being now the
  intersection of the image with the cell. The uncovered area therefore gets the image's own
  background when three sides of the part shown are uniform, otherwise the most frequent color of
  its sides — exactly the bands' rule, following the position live.
- Grayscale applies to it as to the bands (existing `Gray(bands)`).

### Reset and Lifetime

- The cell's *Reset* tool puts the image back to the center, as it does today for the focus.
- Replacing an image, deleting, swapping and changing the layout treat the position as they treat
  the focus today (it is the same field).

---

## Test Impact

To be settled (Open Question): the solution has no test project. The candidates, if tests are
created, are pure functions of `Composition/`:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `FitCalculator.Compute` keeps at least the minimum margin of the image in the cell, on each axis | *(no test project yet)* | — |
| `FitCalculator.Compute` returns the intersection of the image and the cell, `Source` matching `Destination` | *(no test project yet)* | — |
| An in-range focus draws exactly as before (no regression for unmoved images) | *(no test project yet)* | — |
| The magnetic stop holds until the resistance distance, then releases; Shift bypasses it | *(no test project yet)* | — |

---

## Documentation

README (to be confirmed, Open Question): replace "Drag a zoomed-in image to move it within its cell"
with the new gesture — at every zoom, past the edges with the magnetic stop, Shift to ignore it, a
margin always visible — and mention in *Crop threshold* that the area uncovered by a moved image gets
the band color.

---

## Open Questions

- [ ] Minimum margin: 10 % of the cell's width / height stays covered by the image on each axis?
- [ ] Magnetic stops: only the positions where an image edge is aligned on a cell edge, or also the
      centered position?
- [ ] Resistance distance of the magnetic stop: how far must the drag insist past the stop (e.g.
      24 logical px)?
- [ ] Zoom and a moved image: does 100 % and below still recenter the image, and does a zoom keep an
      in-range image within the stops?
- [ ] Unit tests: create a test project to pin `FitCalculator` and the magnetic stop, or keep the
      solution test-free as before?
- [ ] README: update the pan description and the band-color note as described in *Documentation*?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Initial design from the scoping batch (Q&A #1–#4) and one direct scout pass over `FitCalculator`,
`ImageLook`, `GridPreview` (pan and zoom) and `Compositor` / `BandColor`. The clamp that keeps the
cell inside the image moves from the draw (`FitCalculator`) to the gesture (magnetic stop); the draw
only enforces the minimum margin and returns the image ∩ cell. The background needs no new rule: the
band fill already covers the whole cell with the color of the part shown. Margin, stops, resistance,
zoom interaction, tests and README left to Open Questions.

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
| 1 | How far may the image leave the cell? | A minimum margin stays visible | 2026-09-25 |
| 2 | How is the edge crossed? | Magnetic stop, Shift to ignore the magnetism | 2026-09-25 |
| 3 | Does moving past the edges also apply when the image is not zoomed in? | At every zoom, zoom-out included | 2026-09-25 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-25 |
| 5 | Minimum margin: 10 % of the cell on each axis? | | |
| 6 | Magnetic stops: edge alignment only, or also the centered position? | | |
| 7 | Resistance distance of the magnetic stop? | | |
| 8 | Zoom and a moved image: recenter at 100 % and below; keep an in-range image within the stops? | | |
| 9 | Unit tests: create a test project, or stay test-free? | | |
| 10 | README: update the pan description and the band-color note? | | |

---

*Last updated: 2026-09-25*
