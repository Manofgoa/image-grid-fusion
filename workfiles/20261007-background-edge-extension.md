# Background Edge Extension

> Working document — a new Background fill that stretches the image's edge pixels out to the cell's
> edges, with a choice of corner fills, a fade and a softening option.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the Background effect paints one flat fill behind the image — the automatic band color or a
chosen one, at an opacity (`Composition/BackgroundEffect.cs`, drawn by `Compositor.DrawCell` before
the image). The bands left around an image that does not cover its cell show that flat color.

The new option **extends the image's edge pixels** into the bands: the image's top row of pixels is
stretched up to the top of the cell, its bottom row down to the bottom, its left and right columns
out to the sides. Where the image is smaller than its cell **in both directions** (zoomed out, or
moved), the four **corner regions** touch no edge row nor column: they get one of three corner fills,
chosen with thumbnails. A **Blend** slider and a **Soften** checkbox complete it.

Components: `BackgroundEffect` (state), `Compositor.DrawCell` / `DrawUncropped` /
`DrawBackground` and `EdgeExtension` (rendering), `UI/BackgroundFillStrip.cs` on a
`UI/ThumbnailStrip.cs` base shared with `UI/FormatStrip.cs`, the Background options in
`UI/MainForm.cs`.

---

## Fill Modes

The Background's options gain a row of **fill-mode thumbnails**, each a schematic cell drawn like the
Format thumbnails (`FormatStrip`): an image in the middle, its bands, its corners, **its name below**
as in the Format tab. The cell options toolbar grows to their height, for every tab (it keeps one
height, the tallest options' — RULES.md § Options Toolbar).

| Thumbnail | Bands | Corners |
|---|---|---|
| **Color** (default) | The flat fill, as today | The flat fill |
| **Corner pixel** | Edge pixels extended | Flat, the color of the image's corner pixel (clamp-to-edge) |
| **Miter** | Edge pixels extended | Split on the diagonal from the image's corner to the cell's corner: each half continues its edge mirrored past the image's corner (the top row's pixels in the upper half, the left column's in the lower one), the two meeting on the diagonal like a frame's mitered joint |
| **Background corners** | Edge pixels extended | The flat fill (the color in use, at its opacity) |

- The **Color** thumbnail is today's behaviour: the extension is a mode of the Background, not a
  separate effect (Q&A 2, 5).
- The **color controls stay** in every mode: the flat fill is still painted under the whole cell — it
  shows through the image's transparent pixels, fills the Background corners, and is what Blend
  goes to.
- The **opacity applies to the whole background**, the extension included: at 0 % the cell is
  transparent behind its image, as today (Q&A 7).

### Edges Extended

- The edges are those of the image **as drawn in the cell** — after crop, rotation, flip, fine angle,
  zoom and black & white — the rectangle `Compositor.DrawCell` draws into (`fit.Destination`,
  axis-aligned even with a fine angle, since the turned image covers that rectangle; taken a pixel
  inside then, its antialiased edges left out — `EdgeExtension.Covered`). The image is rendered alone
  at the cell's size to read them (`Compositor.DrawBackground`). Extending what is drawn handles
  every other effect at once. With a fine angle, the turned image's corners spill over the bands.
- A band extends **one pixel row or column** of that rectangle, stretched perpendicular to its edge,
  out to the cell's edge. An edge pixel's alpha is extended with it, the flat fill showing through.
- An image covering its cell has no band: nothing to extend, the mode changes nothing.
- The **Blur** effect applies after, over the whole cell, the extension included (it already does for
  the flat fill).
- Videos and animations: extended on every frame, as `DrawCell` draws each one.

### Blend

- A slider **Blend**, **0 – 100 %**, **0 by default** (no fade). Named *Blend*, not *Fade*: the
  glossary's *Fade* is the global sound effect.
- The extension blends toward the flat fill with the distance from the image, linearly, reaching
  **N %** of the flat fill at the cell's edge: 100 makes the extension vanish exactly at the cell's
  edge. Each band fades over its own depth; a corner over its own (by the larger of its two distances).
- Disabled in the **Color** mode (nothing to fade), its value kept.

### Soften

- A checkbox **Soften**, **off by default**: blurs the extension so the streaks of the stretched
  pixels melt — the image itself stays sharp.
- The blur is **progressive**: sharp against the image, so no seam shows at its edge, softer with
  the distance from it (Q&A 6): a pixel averages its edge over ±0.5 px per pixel of distance
  (`EdgeExtension.SoftenSpread`), the edge's end pixel repeating past it — so resolution-independent
  (RULES.md § Rendering). In a Corner pixel corner, the row's and the column's averages are mixed by
  the pixel's distances, so the corner meets both bands without a seam.
- Disabled in the **Color** mode, its value kept.

---

## State

- `BackgroundEffect` gains `Mode` (`BackgroundFill`: Color, CornerPixel, Miter, BackgroundCorners),
  `Blend` (0–1) and `Soften` (bool); `Default` keeps Color, 0, off — so the default state, every Reset and the
  Background exception (on by default, off draws no fill) are unchanged.
- Off, the effect draws nothing, the extension included (Background exception).
- Not persisted, part of `ImageLook` — so the undo history covers it by itself (RULES.md § Undo
  History).
- Acting on any of the new options turns the Background on, like its other options — a click on the
  thumbnail already selected included.

---

## Crop Edit View

`Compositor.DrawUncropped` paints the flat fill behind the whole image. With an extension mode, it
extends the edges of the **whole image** the view shows, in the same mode, fade and soften (Q&A 8)
— the extension always reads the image as drawn.

---

## Test Impact

The repository has **no test project** (`CONTRIBUTING.md`: "There is no test project: check a change
by hand in the running app"). No unit test is created or updated; the behaviours below are checked by
hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project exists | — | — |

Manual checks: each mode on a zoomed-out and moved image (four corners), a contained image (two
bands, no corner), a covering image (no change); fade 0 / 50 / 100; soften on/off; with crop,
rotation, flip, fine angle, black & white, blur; a PNG with transparent edges; a video playing; PNG,
MP4 and GIF exports; undo / redo; Reset buttons.

---

## Open Questions

- [x] ~~How are the corners filled?~~ → Three corner fills chosen by thumbnails (Corner pixel,
  Miter, Background corners), plus a fade slider 0–100 (0 by default, no fade) (Q&A 1)
- [x] ~~Integration (Q&A 2 answered "implicit"): are the thumbnails the Background's fill mode?~~ →
  Yes: four thumbnails, Color first, the color controls kept (Q&A 5)
- [x] ~~Soften the streaks?~~ → A checkbox (Q&A 3)
- [x] ~~Soften: fixed or progressive?~~ → Progressive, sharp against the image (Q&A 6)
- [x] ~~Opacity on the extension?~~ → Yes, on the whole background (Q&A 7)
- [x] ~~Crop edit view?~~ → The whole image's edges extended (Q&A 8)
- [x] ~~Thumbnails with labels or compact?~~ → With labels, the options toolbar taller (Q&A 9)

---

## Design Iterations

### Iteration 1 — 2026-10-07

Initial design from the request and the scoping batch: four corner proposals drawn and discussed
(corner pixel, miter, background corners, fade); the user keeps all of them — three as corner-fill
thumbnails, the fade as a 0–100 slider (0 = none) — and adds a soften checkbox. The extension reads
the image as drawn in the cell, so crop, orientation, fine angle and black & white come for free.
Exploration done directly (subject rated simple): `BackgroundEffect`, `Compositor.DrawCell` /
`DrawUncropped`, `FitCalculator.ComputeTurned`, `FormatStrip`, `BlurRenderer`, no test project.

### Iteration 2 — 2026-10-07

Open questions answered (Q&A 5–9): the thumbnails are the Background's fill mode (Color, Corner
pixel, Miter, Background corners), the color controls kept; soften is progressive; the opacity
covers the extension; the crop edit view extends the whole image; the thumbnails carry their labels,
the cell options toolbar growing to their height.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given: code, tests and documentation. Branch Gate: stays on `main`, the standing choice for this
repository. No unit tests (no test project).

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

- **Miter mirrored, not slanted at 45°**: each corner half takes its edge mirrored past the image's
  corner. Slanting the stretched pixels at 45° would have broken the corner against its band (the
  band's column at the corner's boundary is the corner pixel, the slant would reach far along the
  row there); mirrored, the corner meets its bands without a seam and the diagonal shows the joint.
- **Diagonal from the image's corner to the cell's**, as designed, not at 45°: a non-square corner
  is split on its own diagonal.
- **Fade named Blend** in the code, UI and docs: *Fade* already names the global sound effect, one
  meaning per term (GLOSSARY.md).
- **Soften spread**: ±0.5 px per pixel of distance; in a Corner pixel corner, the row's and the
  column's averages mixed by the distances, so the corner joins both bands.
- **Fine angle**: the covered rectangle is taken a pixel inside, the turned image's antialiased
  edges left out; its corners spill over the bands.
- **`ThumbnailStrip<T>` base** extracted from `FormatStrip` (`FormatPicked` → `Picked`): the Format
  and Background strips share the row, hover, highlight, label and click; `PicksSelected` lets a
  click on the selected Background thumbnail turn the effect on.
- **Thumbnail colors**: each edge of the schematic image its own color (blue top, amber right, coral
  bottom, green left), the corner pixel purple, the flat fill gray; faded while disabled.
- **Cost**: the extension is computed per pixel in managed code, the image rendered a second time
  alone — about 0.2 s for a 1200 × 628 cell at full quality with every option; heavier on MP4
  exports and video playback with an extending fill. Not optimized further (not in the design).
- Branch: `main`, as the repository's standing choice.

### Iteration 5 — 2026-10-07 — ⚙️ Post-implementation — Edge opacity and a two-color miter

Requested after testing:

- **Edge opacity**: act on the extended edges' opacity without changing the flat fill's; the
  Background's opacity over the whole background stays wanted too.
- **Miter redrawn**: not the mirrored edges. Taking the top-left corner: the image's corner pixel
  gives the color of the intersection, the pixel next to it down the left column the color of the
  left part, the pixel next to it along the top row the color of the right part (the request wrote
  `0:1` for both parts — read as the two neighbours of the corner, see Q&A 10); each part filled flat
  with its color, then a **1 px line** in the corner pixel's color, **50 % opacity**, drawn over the
  diagonal. So two colors per corner, plus the line.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-07 | State, rendering, thumbnail strip base, Background options — checked with a scratch rendering harness (every mode, Blend, Soften, fine angle, opacity, black & white) |
| Unit tests | 3 | 2026-10-07 | None: no test project in the repository |
| README (+ `README.fr.md`) | 3 | 2026-10-07 | § Background |
| Glossary (+ `GLOSSARY.fr.md`) | 3 | 2026-10-07 | Background row updated, Edge extension row added |
| Rules | 3 | 2026-10-07 | § The Background's Edge Extension |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How to fill the corners (outside the image in both directions)? Proposals: corner pixel, miter, background corners, fade | All four: thumbnails for 1, 2, 3, and the fade as a slider (0 by default = no fade, up to 100) | 2026-10-07 |
| 2 | How does the option fit in the Background: Color / Edges mode, a checkbox over the color, or a separate effect? | "Implicit from my previous answer" | 2026-10-07 |
| 3 | Add a setting to soften the stretched streaks? | A checkbox for it | 2026-10-07 |
| 4 | Is the subject simple, or tricky / long? | Simple | 2026-10-07 |
| 5 | Are the thumbnails the Background's fill mode, a first Color thumbnail for today's flat fill? | Yes, 4 thumbnails | 2026-10-07 |
| 6 | Soften: fixed blur, or progressive with the distance? | Progressive | 2026-10-07 |
| 7 | Does the opacity apply to the extension too? | Yes, to the whole background | 2026-10-07 |
| 8 | Crop edit view: extend the whole image's edges, or flat fill? | Extend the whole image's edges | 2026-10-07 |
| 9 | Thumbnails with labels (taller options row) or compact with tooltips? | With labels | 2026-10-07 |
| 10 | Miter: the left part takes the pixel below the corner, the right part the pixel right of it? | | 2026-10-07 |
| 11 | Background options row with the new Edges slider: two lines beside the thumbnails, one line with shorter sliders, or appended at the end? | | 2026-10-07 |

---

*Last updated: 2026-10-07*
