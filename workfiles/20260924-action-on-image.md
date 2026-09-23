# Action on Image

> Working document — per-image actions from a hover toolbar: zoom (slider) with drag-to-pan,
> black & white, horizontal / vertical flip, and 90° rotation.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today an image is always drawn the same way: scaled to fill its cell by the fitting rule
(`FitCalculator`: up to 15 % crop on the overflowing axis, then centered with dominant-color
bands), exactly as decoded (EXIF orientation applied on load). The only per-cell controls are
the **×** (top-right, on hover) and, for multi-page sources, the page slider (bottom, on hover).

Goal: act on **one image specifically**, from a toolbar shown when its cell is hovered:

- **Zoom** with a slider, and **drag the image** inside its cell to choose the visible part
- **Black & white**
- **Flip** horizontal and vertical
- **Rotate** by 90° steps

Every action shows in the preview **and** in the output (copy / save), like any cell content.

Relevant components:

| File | Role today |
|---|---|
| `Composition/SourceImage.cs` | Cell content: owned `Bitmap`, `Dominant` color, `FilePath`, optional `Pages` + current `Page`; `ShowPage` swaps the bitmap |
| `Composition/FitCalculator.cs` | `Compute(cell, imageSize, threshold)` → `Fit(Source, Destination)`, always centered |
| `Composition/Compositor.cs` | `Draw` fills the cell with `Dominant`, clips to the cell, draws `Bitmap` through `Fit` |
| `Composition/CanvasSizer.cs` | Canvas width at which no image is downscaled, from the image sizes |
| `Composition/DominantColor.cs` | Dominant color of a bitmap, used for bands and transparent pixels |
| `UI/GridPreview.cs` | Hover state (`_hovered`, `_hoveringClose`, `_hoveringSlider`), `CloseBounds` (top-right, 24 px), `SliderBounds` (bottom pill, multi-page only, hidden under 120 px), press → select, drag past `SystemInformation.DragSize` → swap |
| `UI/LayoutStrip.cs` | Holds the layout **Mirror** toggle — a different feature (flips the layout, not an image) |

Constraints found by exploration:

- **No test project** in the solution; the previous workfiles declined unit tests.
- **Dragging a cell already means "swap with another cell"** (`OnMouseMove` → `StartDrag`):
  drag-to-pan must be told apart from it (see Open Questions).
- The bottom of the cell belongs to the page slider, the top-right corner to the **×**.
- The word *Mirror* is taken by the layout toggle: the image action is called **Flip** in the UI
  and the README to avoid confusion.
- The *animated content* workfile (`20260924-animated-content.md`, not implemented) makes
  multi-page cells play: the actions belong to the image, so they apply to every page / frame.

---

## Hover Toolbar

- Shown on the **hovered** cell only, never in the output, hidden while a swap drag is running
  (like the **×**).
- Placed along the **top** of the cell, left of the **×**, as a row of 24 px buttons with the
  same look as the **×** (semi-transparent dark round, white glyph, lighter when hot).
- Buttons, left to right:

| Button | Action | State shown |
|---|---|---|
| ⟲ | Rotate 90° counter-clockwise | — |
| ⟳ | Rotate 90° clockwise | — |
| ⇆ | Flip horizontally | Highlighted while on |
| ⇅ | Flip vertically | Highlighted while on |
| ◐ | Black & white | Highlighted while on |
| 🔍 zoom slider | See Zoom | Current percentage |

- Clicking a button neither selects the cell nor starts a swap (same rule as the page slider).

---

## Transformations

The actions are stored **on the image** (`SourceImage`), not on the cell:

| Property | Values | Default |
|---|---|---|
| `Rotation` | 0, 90, 180, 270 (clockwise) | 0 |
| `FlipX` / `FlipY` | on / off | off |
| `Grayscale` | on / off | off |
| `Zoom` | ≥ 1 (1 = the fitting rule as today) | 1 |
| `Focus` | point of the image shown at the center of the cell, in normalized image coordinates | center (0.5, 0.5) |

- **Orientation** (rotation + flips) is applied **before** fitting: a 90° / 270° rotation swaps
  the image's width and height, so the fitting rule (crop threshold, bands) and the canvas size
  work on the **oriented** size — a portrait rotated to landscape fills a landscape cell.
- Flips are expressed in the **screen** frame: ⇆ always flips left↔right as the user sees it,
  whatever the rotation.
- **Black & white** is a luminance grayscale of the image. The bands and transparent pixels use
  the dominant color of the **transformed** image, so they turn grey too.
- The transformed bitmap is derived from the page bitmap, so a multi-page source keeps its
  actions when its page changes (slider today, playback with *animated content*).

---

## Zoom

- **Zoom 100 %** is the image as the fitting rule draws it today; the slider only zooms **in**
  (range: see Open Questions).
- Zooming shrinks the part of the image drawn in the cell, around `Focus`.
- **Pan**: dragging the image moves `Focus`, clamped so the cell never shows beyond the image on
  an axis where the image overflows. On an axis where the fitting rule leaves bands at 100 %,
  zooming in reduces the bands first, and the image stays centered on that axis until it
  overflows.
- The canvas size is computed from the **unzoomed** fit, so moving the slider never changes the
  output resolution.

---

## Test Impact

No test project exists in the solution, and the previous workfiles declined unit tests. The
behaviours below would be the ones to pin if a test project were added:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Rotation 90° / 270° swaps the size used by the fit and the canvas | — | Not applicable (no test project) |
| Flip is in the screen frame whatever the rotation | — | Not applicable (no test project) |
| Zoom 1 gives the same `Fit` as today; zoom > 1 shrinks `Source` around `Focus` | — | Not applicable (no test project) |
| `Focus` is clamped so the cell never shows beyond the image on an overflowing axis | — | Not applicable (no test project) |

---

## Open Questions

- [ ] How is **pan** (drag inside a zoomed image) told apart from the existing **swap** (drag a cell onto another)?
- [ ] Where does the **zoom slider** go, given the bottom is the page slider's and the top holds the buttons?
- [ ] What is the **zoom range**?
- [ ] How are the actions **reset** — a dedicated button, double-click, nothing?
- [ ] Do the actions **follow the image** when it is swapped, and survive a layout change?
- [ ] What happens to the toolbar in a **cell too narrow** for all the buttons (e.g. *Four columns*)?
- [ ] Are **keyboard shortcuts** on the selected cell part of the scope?
- [ ] Unit tests: stay test-free like the previous workfiles, or add a test project?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial design from the request and the scoping batch (Q&A #1–#4): actions reached from a
**hover toolbar** on the cell, rotation by **90° steps**, zoom with a slider **and drag-to-pan**.
The actions are stored on the image, orientation is applied before the fitting rule, black &
white is a luminance grayscale, and the canvas size ignores the zoom. The image flip is named
*Flip* to keep it apart from the layout *Mirror* toggle.

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
| 1 | How does the user reach the actions on one image? | Toolbar shown on hover | 2026-09-24 |
| 2 | Which kind of rotation? | 90° steps | 2026-09-24 |
| 3 | Can the zoomed image be moved inside its cell? | Zoom + drag to pan | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward (single scout pass) | 2026-09-24 |
| 5 | How is pan told apart from swap? | | |
| 6 | Where does the zoom slider go? | | |
| 7 | What is the zoom range? | | |
| 8 | How are the actions reset? | | |
| 9 | Do the actions follow the image on swap and survive a layout change? | | |
| 10 | What happens to the toolbar in a too narrow cell? | | |
| 11 | Are keyboard shortcuts in scope? | | |
| 12 | Unit tests: stay test-free or add a test project? | | |

---

*Last updated: 2026-09-24*
