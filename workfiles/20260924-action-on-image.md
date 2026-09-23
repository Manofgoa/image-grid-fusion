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
- The *animated content* workfile (`20260924-animated-content.md`, implemented during this
  design) makes multi-page cells play: the actions belong to the image, so they apply to every
  page / frame, in the preview and in the MP4 / still export (`Imaging/GridExport.cs`).

---

## Hover Toolbar

- Shown on the **hovered** cell only, never in the output, hidden while a swap drag is running
  (like the **×**).
- Placed along the **top** of the cell, left of the **×**, as a row of 24 px buttons with the
  same look as the **×** (semi-transparent dark round, white glyph, darker when hot). Glyphs are
  drawn as vectors, not taken from a symbol font.
- Hidden while an export runs (grid locked), like the **×**.
- Buttons, left to right:

| Button | Glyph | Action | State shown |
|---|---|---|---|
| Rotate left | Arc arrow turning counter-clockwise | Rotate 90° counter-clockwise | — |
| Rotate right | Arc arrow turning clockwise | Rotate 90° clockwise | — |
| Flip horizontally | Filled triangle and its outlined mirror on each side of a vertical axis | Flip left↔right | Filled with the highlight color while on |
| Flip vertically | Same, around a horizontal axis | Flip top↔bottom | Filled with the highlight color while on |
| Black & white | Half-filled circle | Black & white | Filled with the highlight color while on |
| Reset | Almost full counter-clockwise arc around a dot | Back to the image as loaded (every action off, zoom 100 %) | Shown only while at least one action is active |

- **Narrow cell**: when the row does not fit left of the **×**, the buttons wrap onto a second
  row, and the zoom slider starts below the last row, shortened accordingly. Each button keeps its
  slot whether Reset is shown or not, and the slider always leaves room for the full toolbar, so
  nothing moves when Reset appears.
- No keyboard shortcut: the actions are reached from the toolbar only.
- The **zoom slider** is a separate vertical pill along the **left** edge of the cell, below the
  button row and above the page slider (see Zoom).
- Clicking a button or the zoom slider neither selects the cell nor starts a swap (same rule as
  the page slider).

---

## Transformations

The actions are stored **on the image** (`SourceImage`), not on the cell:

| Property | Values | Default |
|---|---|---|
| `Rotation` | 0, 90, 180, 270 (clockwise) | 0 |
| `FlipX` / `FlipY` | on / off | off |
| `Grayscale` | on / off | off |
| `Zoom` | 0.5 to 4 (1 = the fitting rule as today) | 1 |
| `Focus` | point of the image shown at the center of the cell, in normalized image coordinates | center (0.5, 0.5) |

- **Orientation** (rotation + flips) is applied **before** fitting: a 90° / 270° rotation swaps
  the image's width and height, so the fitting rule (crop threshold, bands) and the canvas size
  work on the **oriented** size — a portrait rotated to landscape fills a landscape cell.
- Flips are expressed in the **screen** frame: ⇆ always flips left↔right as the user sees it,
  whatever the rotation.
- A rotation turns the flips with it (a quarter turn swaps which axis is flipped) and the
  `Focus`, so the same part of the image stays centered.
- **Black & white** is a luminance grayscale (0.299 R + 0.587 G + 0.114 B) of the image. The
  bands and transparent pixels use the same grayscale of the dominant color, so they turn grey
  too.
- The actions **follow the image**: kept when it is swapped with another cell and when the
  layout (or the image count) changes — `Focus` being normalized, the same part of the image
  stays centered in the new cell shape. Replacing the image with another file (drop on the cell,
  or a full grid receiving a new image) starts from a fresh image, with no action.
- The actions are applied **when drawing** (`Compositor.DrawCell`): the bitmap part to show goes
  to a rotated / flipped parallelogram, through a grayscale color matrix. No transformed copy is
  kept, so a multi-page source keeps its actions whatever page or frame it shows, at no cost per
  frame.
- A **text** turned a quarter is laid out again in the turned shape of its cell, so it still
  fills its cell once rotated.

---

## Zoom

- **Range 50 % → 400 %**. **Zoom 100 %** is the image as the fitting rule draws it today; the
  zoom scales that fit around `Focus`.
- **Zoom in** (> 100 %): the part of the image drawn in the cell shrinks around `Focus`. On an
  axis where the fitting rule leaves bands at 100 %, zooming in reduces the bands first, and the
  image stays centered on that axis until it overflows.
- **Zoom out** (< 100 %): the image shrinks inside its cell, centered, and the uncovered area
  shows the dominant color, like the bands.
- **Slider**: vertical pill along the left edge of the cell, zoom going up on a **log scale**
  (50 → 100 % and each doubling take the same length, so 100 % sits at a third of the track),
  100 % marked by a tick and **snapping** within 4 px of it; the current percentage shows in a
  pill right of the thumb while the slider is hovered or dragged; the image follows it live.
- Back to 100 % or below, `Focus` returns to the center: 100 % is exactly the fitting rule.
- **Pan**: while the zoom is **above 100 %**, dragging the image moves `Focus`, clamped so the
  cell never shows beyond the image on an axis where the image overflows.
- **Swap**: at 100 % or below, dragging a cell swaps it as today. A zoomed-in image is swapped
  with **Ctrl + drag**; Ctrl + drag swaps whatever the zoom.
- The canvas size is computed from the **unzoomed** fit, so moving the slider never changes the
  output resolution.
- Animated frames are decoded at the cell size in the frame's own orientation, enlarged by a zoom
  in, so a zoomed video stays sharp while playing.

---

## Test Impact

No test project exists in the solution, and the solution stays test-free (Q&A #12). The
behaviours below would be the ones to pin if a test project were added:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Rotation 90° / 270° swaps the size used by the fit and the canvas | — | Not applicable (no test project) |
| Flip is in the screen frame whatever the rotation | — | Not applicable (no test project) |
| Zoom 1 gives the same `Fit` as today; zoom > 1 shrinks `Source` around `Focus` | — | Not applicable (no test project) |
| `Focus` is clamped so the cell never shows beyond the image on an overflowing axis | — | Not applicable (no test project) |

---

## Open Questions

- [x] ~~How is **pan** (drag inside a zoomed image) told apart from the existing **swap** (drag a cell onto another)?~~ → Drag pans while zoomed in (> 100 %), swaps otherwise; Ctrl + drag always swaps
- [x] ~~Where does the **zoom slider** go, given the bottom is the page slider's and the top holds the buttons?~~ → Vertical pill along the left edge of the cell
- [x] ~~What is the **zoom range**?~~ → 50 % → 400 %, zoom out allowed (dominant-color bands around)
- [x] ~~How are the actions **reset** — a dedicated button, double-click, nothing?~~ → ↺ button in the toolbar, shown only while an action is active
- [x] ~~Do the actions **follow the image** when it is swapped, and survive a layout change?~~ → Yes, they belong to the image; replacing it with another file starts fresh
- [x] ~~What happens to the toolbar in a **cell too narrow** for all the buttons (e.g. *Four columns*)?~~ → The buttons wrap onto a second row, the zoom slider shortens below
- [x] ~~Are **keyboard shortcuts** on the selected cell part of the scope?~~ → No, out of scope
- [x] ~~Unit tests: stay test-free like the previous workfiles, or add a test project?~~ → Stay test-free
- [ ] *(raised by the implementation run)* Tooltips on the toolbar buttons, naming each action — the glyphs alone may not tell rotate left from reset
- [ ] *(raised by the implementation run)* Update the README (*Features* list, a note under *Fitting rules* on zoom and rotation) — left out, the go covered the code only

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

### Iteration 2 — 2026-09-24

Answers Q&A #5–#8. Pan vs swap: drag pans only while zoomed in, Ctrl + drag always swaps. Zoom
slider: vertical, along the left edge. Zoom range widened to **50 % → 400 %**, so zooming out is
now part of the design (image centered, dominant-color area around). A **↺ reset** button joins
the toolbar, shown only while an action is active.

### Iteration 3 — 2026-09-24

Answers Q&A #9–#12. The actions follow the image across swaps and layout changes. A narrow cell
wraps the toolbar onto a second row. No keyboard shortcut. The solution stays test-free, so unit
tests are declined.

### Iteration 4 — 2026-09-24 — ✅ Implemented

Go given for **the code only** (README not authorized, unit tests already declined). The run
stays on `main`, the standing choice for this repository.

### Iteration 5 — 2026-09-24 — 🧭 Implementation choices

Choices the run took on its own; the domain sections above now describe them.

- **Draw-time actions** instead of a transformed bitmap derived from each page: the compositor
  maps the visible part of the bitmap to a rotated / flipped parallelogram, with a grayscale color
  matrix. Same result, and nothing to recompute per animation frame.
- **Grey bands**: the grayscale of the dominant color, not a new dominant color of the grey image
  (same color, no extra pass).
- **Zoom slider**: log scale, 100 % tick with a 4 px snap, percentage pill right of the thumb.
  Back to 100 % or below recenters the image, so 100 % is exactly the fitting rule and a reset
  image counts as untouched.
- **Rotation carries the flips and the focus**, so the flips stay in the screen frame and the
  centered part stays centered.
- **Rotated text** is laid out again in the turned cell shape, so it fills its cell.
- **Toolbar and zoom slider hidden while an export runs**, like the ×.
- **Stable toolbar slots**: every button keeps its place whether Reset is shown, and the zoom
  slider always leaves room for the full toolbar.
- **Vector glyphs** instead of the ⟲ ⟳ ⇆ ⇅ ◐ ↺ characters, which depend on symbol-font coverage;
  the flip icon is a filled triangle and its outlined mirror around an axis.
- **Animated frames** are decoded at the size the zoom and rotation need. A held (hovered) video
  keeps its last frame at the previous resolution until it plays again.
- **Commits**: the whole preview UI (toolbar, zoom slider, pan) landed in one commit, its changes
  being interleaved in `GridPreview.cs`.
- **Verification**: no test project (unit tests declined); the orientation / zoom math and the
  toolbar, slider, pan and swap interactions were checked with throwaway programs in the session
  scratchpad, rendering the preview off screen.

No project rule was broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-09-24 | Composition (`ImageLook`, `FitCalculator`, `Compositor`, `SourceImage`), export (`GridExport`), preview UI (`GridPreview`) |
| Unit tests | 3 | 2026-09-24 | Declined: the solution stays test-free (Q&A #12) |
| README | 4 | 2026-09-24 | Not authorized: the go covered the code only |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the user reach the actions on one image? | Toolbar shown on hover | 2026-09-24 |
| 2 | Which kind of rotation? | 90° steps | 2026-09-24 |
| 3 | Can the zoomed image be moved inside its cell? | Zoom + drag to pan | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward (single scout pass) | 2026-09-24 |
| 5 | How is pan told apart from swap? | Drag pans when zoomed in, swaps at 100 % or below; Ctrl + drag swaps a zoomed image | 2026-09-24 |
| 6 | Where does the zoom slider go? | Vertical, along the left edge of the cell | 2026-09-24 |
| 7 | What is the zoom range? | 50 % → 400 % | 2026-09-24 |
| 8 | How are the actions reset? | ↺ button in the toolbar | 2026-09-24 |
| 9 | Do the actions follow the image on swap and survive a layout change? | Yes, they follow the image | 2026-09-24 |
| 10 | What happens to the toolbar in a too narrow cell? | Buttons wrap onto a second row | 2026-09-24 |
| 11 | Are keyboard shortcuts in scope? | No, out of scope | 2026-09-24 |
| 12 | Unit tests: stay test-free or add a test project? | Stay test-free | 2026-09-24 |

---

*Last updated: 2026-09-24*
