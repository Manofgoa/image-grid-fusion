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
- Refined (Q&A #5–#8): 10 % margin, a center stop besides the edge stops, green guides during the
  drag, 24 px resistance, a zoom clamping the image back within its stops.

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

- The **edge stops** are the boundaries of the *in range* zone: the positions where an image edge
  is aligned on a cell edge. A **center stop** sits where the image is centered on that axis
  (focus at the center of the image, as the fitting rule draws it) — Q&A #6.
- The **minimum margin** is the visible overlap the image always keeps with the cell on each axis:
  **10 % of the cell's width / height**, capped by the image's own size on that axis (Q&A #5).
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
- **Magnetic stops** (without Shift): when the image reaches a stop, it stays on it until the drag
  has gone **24 logical px** past it (Q&A #7), scaled with `LogicalToDeviceUnits`; it then jumps to
  the mouse position and follows it, up to the hard limit.

  | Stop | Held when the image moves |
  |---|---|
  | Edge stop | **Outward** only — moving back inward is free, no stop is felt on the way back in |
  | Center stop | Across it, **either way** |

- The resistance is counted **per axis**: a diagonal drag can pass a stop on one axis and stay held
  on the other.
- **Shift held**: no magnetic stop at all — no resistance, no guide — only the hard limit (Q&A #2,
  #7). It is read on every mouse move, so it can be pressed or released during the drag; releasing
  it leaves the image where it is.
- Live display, fast then full-quality render, and the cursor stay as the live pan does today.

### Magnetic Guides

Shown **during the drag only**, while the image is **held on a stop** (Q&A #6), drawn over the image
in the preview — never in `Compositor`, so never exported:

| Stop held | Guide |
|---|---|
| Edge stop | A **dashed** line along the cell edge the image edge is aligned on |
| Center stop, horizontal axis | A **vertical** line through the cell center, across the cell |
| Center stop, vertical axis | A **horizontal** line through the cell center, across the cell |

- Both center stops held at once draw the two lines **crossing** at the cell center.
- Color: the **fluorescent green** of the blur bars (`GridPreview.BarColor`), so it shows on any image.
- A guide disappears as soon as its stop is passed, and all of them when the mouse is released.

### Zoom

- **100 % and below no longer recenters** the image: `ImageLook.WithZoom` keeps the focus at every
  zoom (Q&A #8, the "recenter" option not chosen).
- **A zoom always brings the image back within its stops** (Q&A #8): after a slider step or a wheel
  notch, the position is clamped to the *in range* zone on each axis, even if the image had been
  pushed beyond. For an image larger than the cell, it covers the cell; for an image smaller than the
  cell (zoom-out, bands), it lies inside the cell.
- The wheel's anchor under the cursor now applies at every zoom, then that clamp.

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

No unit test is created or updated: the user chose to keep the solution test-free (Q&A #9), as every
previous workfile did. The margin, the image ∩ cell fit, the no-regression of unmoved images, the
magnetic stops and the zoom clamp stay untested by decision, not because nothing testable changes.
Verification is manual: drag a photo past each edge at 50 %, 100 % and 200 % (with and without
Shift), check the guides, the 10 % margin, the band color of the uncovered area, a wheel zoom on a
pushed image, then a copy / save and a video cell.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined, Q&A #9) | — | — |

---

## Documentation

README (Q&A #10): replace "Drag a zoomed-in image to move it within its cell"
with the new gesture — at every zoom, past the edges with the magnetic stops (edges and center) and
their green guides, Shift to ignore them, a margin always visible, a zoom bringing the image back
within its stops — and mention in *Crop threshold* that the area uncovered by a moved image gets
the band color.

---

## Open Questions

- [x] ~~Minimum margin: 10 % of the cell's width / height stays covered by the image on each axis?~~
      → 10 % of the cell
- [x] ~~Magnetic stops: only the positions where an image edge is aligned on a cell edge, or also the
      centered position?~~ → Both, with fluorescent green guides during the drag: a dashed line on
      the magnetized edge, a vertical and a horizontal line crossing over the image for the center
- [x] ~~Resistance distance of the magnetic stop: how far must the drag insist past the stop (e.g.
      24 logical px)?~~ → 24 logical px; Shift skips the stops altogether
- [x] ~~Zoom and a moved image: does 100 % and below still recenter the image, and does a zoom keep an
      in-range image within the stops?~~ → A zoom always brings the image back within its stops,
      even when it had been pushed beyond; no recentering at 100 % and below
- [x] ~~Unit tests: create a test project to pin `FitCalculator` and the magnetic stop, or keep the
      solution test-free as before?~~ → Stay test-free
- [x] ~~README: update the pan description and the band-color note as described in *Documentation*?~~
      → Yes

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

### Iteration 2 — 2026-09-25

Open questions answered (Q&A #5–#8). Margin set to 10 % of the cell. The center becomes a magnetic
stop too, held across either way, on top of the edge stops (held outward only). New: **magnetic
guides** in fluorescent green during the drag — a dashed line on the magnetized edge, a vertical and
a horizontal line crossing at the cell center — drawn by the preview only. Resistance: 24 logical px;
Shift bypasses every stop and guide. Zoom: always clamps the position back within the stops, and no
longer recenters at 100 % and below (the recentering option was not chosen).

### Iteration 3 — 2026-09-25

Last open questions answered (Q&A #9, #10): no unit tests, the solution stays test-free; the README
gets the new pan description and the band-color note. Design complete, submitted for the go.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 3 | 2026-09-25 | Declined — solution kept test-free (Q&A #9) |
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
| 5 | Minimum margin: 10 % of the cell on each axis? | 10 % of the cell | 2026-09-25 |
| 6 | Magnetic stops: edge alignment only, or also the centered position? | Both; fluorescent green dashed line on the magnetized edge, and for the center two crossing lines (vertical and horizontal) over the image, during the drag only | 2026-09-25 |
| 7 | Resistance distance of the magnetic stop? | 24 logical px, Shift to skip insisting | 2026-09-25 |
| 8 | Zoom and a moved image: recenter at 100 % and below; keep an in-range image within the stops? | Always clamp to the stops | 2026-09-25 |
| 9 | Unit tests: create a test project, or stay test-free? | Stay test-free | 2026-09-25 |
| 10 | README: update the pan description and the band-color note? | Yes | 2026-09-25 |

---

*Last updated: 2026-09-25*
