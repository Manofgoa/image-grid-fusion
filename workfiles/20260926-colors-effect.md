# Colors Effect

> Working document — the Black & white effect becomes **Colors**: saturation, hue, brightness and
> contrast, every setting centered on 0.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The **Black & white** effect only offers one slider, *Intensity*, from 0 (the colors) to 100 %, and
turning it on applies 100 % at once. It becomes a general color-adjustment effect:

- The tab is renamed **Colors**.
- Its options become four sliders: **Saturation**, **Hue**, **Brightness**, **Contrast**.
- Every slider is **centered on 0**: 0 leaves the image unchanged, the default of every setting is
  0. Black & white is no longer a setting of its own: it is **Saturation −100 %**.

Components touched (from the scout pass):

| Component | Today | Becomes |
|---|---|---|
| `Composition/ImageLook.cs` | `ImageEffect.BlackAndWhite`, `double? Grayscale` (0–1), `KeptGrayscale`, activated at `Grayscale = 1`, `WithGrayscale` | `ImageEffect.Colors`, a `ColorsEffect? Colors` record + `KeptColors`, activated at `ColorsEffect.Default` (all 0), `WithColors` |
| `Composition/Compositor.cs` | `GrayscaleMatrix(intensity)` set on the `ImageAttributes`; `Gray(bands, intensity)` for the band color | `ColorsMatrix(colors)` — one composed 5×5 `ColorMatrix`; the band color passed through the same matrix |
| `UI/EffectTabs.cs` | Tab text `"Black & white"` | `"Colors"` |
| `UI/EffectIcons.cs` | `BlackAndWhite(size)`: a half black / half white disc | `Colors(size)`: see *Tab Icon* |
| `UI/MainForm.cs` | `_grayscaleIcon`, `_grayscale` slider (0–100), `_grayscaleLabel` "Intensity: n%" | Four slider + label pairs, see *Options* |
| `README.md` | `### Black & white` section, tab list line 43 | `### Colors` section, tab list |
| `GLOSSARY.md` | Effect list names "Black & white" | Names "Colors" |

---

## Settings

Every setting is stored as a value in [−1, 1] (hue in degrees), the default 0 being the identity.

| Setting | Slider | Default | −100 % end | +100 % end |
|---|---|---|---|---|
| Saturation | −100 … +100 % | 0 | Black & white (the luminance, as today's 100 %) | Saturation doubled |
| Hue | −180 … +180° | 0° | Hue turned by −180° | Hue turned by +180° (same as −180°) |
| Brightness | −100 … +100 % | 0 | See *Open Questions* — proposed: offset of −50 % (strongly darkened, not flat black) | Proposed: offset of +50 % |
| Contrast | −100 … +100 % | 0 | Flat mid-gray (factor 0 around 50 %) | Proposed: factor ×3 around 50 % |

- **Default state** (RULES.md, *Scope and State*): the four settings at 0, the effect off. Every
  *Reset* brings it back there.
- **Consequence of the defaults at 0**: checking the tab's checkbox with the settings at their
  defaults turns the effect on **without any visible change** — unlike today, where it turned the
  image black & white at once. The effect shows as soon as a slider moves (and moving a slider turns
  the effect on, RULES.md *Options Toolbar*).
- Turned off, the effect keeps its four settings and is drawn as its defaults (identity).

---

## Rendering

- The four settings compose **one** `ColorMatrix`, set on the `ImageAttributes` in
  `Compositor.DrawCell` as today — one drawing pass, no extra cost, the preview, the exports and
  video playback all showing it.
- Order of composition: **saturation → hue → contrast → brightness**.
  - Saturation: each channel mixed with the luminance (weights 0.299 / 0.587 / 0.114, as today),
    factor `1 + s`.
  - Hue: the luminance-preserving hue rotation matrix (the one of SVG `feColorMatrix hueRotate`) —
    an approximation of an HSL hue turn, close enough for an adjustment slider.
  - Contrast: scaling around 0.5 (translation row of the matrix).
  - Brightness: an offset on the three channels (translation row).
- GDI+ clamps each channel to [0, 1] after the matrix.
- **Bands** follow the effect as they follow the black & white today: the band color goes through
  the same matrix.
- An effect at its defaults (all 0) sets **no** matrix, so an unchanged image is drawn exactly as
  without the effect.
- Resolution-independent by nature (a per-pixel color transform).

---

## Options

- The options toolbar row of the Colors tab holds four **slider + label** pairs, in the order
  Saturation, Hue, Brightness, Contrast, then the effect's own Reset button ending the row as for
  every effect.
- Labels, signed like the Rotate effect's angle: `Saturation: +25 %`, `Hue: −30°`,
  `Brightness: 0 %`, `Contrast: +10 %`.
- Today's *Intensity* icon next to the slider goes (four labelled sliders do not need it).
- Layout: see *Open Questions* — four 160 px sliders with their labels (≈ 1,050 px) do not fit the
  default 960 px window width.
- Acting on any slider turns the effect on first, starting from its kept settings (RULES.md).

---

## Tab Icon

The half black / half white disc no longer describes the effect. Proposed: a **color wheel** — a
disc of hue sectors in a gray ring, keeping the ring of today's icon (see *Open Questions*).

---

## Test Impact

**Nothing to test** — the solution has no test project, and creating one is outside this scope
(every previous workfile stayed test-free). Verification is manual: a photo and a video cell; each
slider dragged to both ends and back to 0; Saturation −100 % giving the same black & white as
before; the bands following; the effect turned off and on again (settings kept); the effect's own
Reset and the toolbar's Reset (all back to 0, off); a copy / save (PNG and MP4) showing the colors.

---

## Open Questions

- [ ] Layout of the four sliders in the options row: one row of narrower sliders (≈ 90 px, ≈ 750 px
  in all, fits 960 px), or another arrangement?
- [ ] Strength at the ends: Brightness ±100 % = offset ±50 % (not flat black / white), Contrast
  +100 % = ×3 — accepted, or other ends?
- [ ] Tab icon: a color wheel in a gray ring, or another drawing?
- [ ] Checking the checkbox with the defaults at 0 shows nothing until a slider moves — accepted?

---

## Design Iterations

### Iteration 1 — 2026-09-26

Initial design from the scoping batch: the tab is renamed **Colors**; four settings — Saturation,
Hue, Brightness, Contrast; every slider centered on 0 (±100 %, hue ±180°), default 0 = unchanged;
black & white becomes Saturation −100 %. Rendering through one composed `ColorMatrix` in
`Compositor.DrawCell`, the bands following. Four questions left open: slider layout, strength at the
ends, tab icon, the invisible activation at the defaults.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — manual verification (see *Test Impact*) |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Name of the tab replacing "Black & white"? | **Colors** | 2026-09-26 |
| 2 | Which settings? | **Saturation, Hue, Brightness, Contrast** | 2026-09-26 |
| 3 | Range and default model of the sliders? | **Centered on 0**: −100 … +100 % (hue in degrees), default 0 = unchanged | 2026-09-26 |
| 4 | Straightforward or tricky / long? | **Straightforward** — a single scout pass | 2026-09-26 |
| 5 | Layout of the four sliders? | | |
| 6 | Strength at the ends (Brightness, Contrast)? | | |
| 7 | Tab icon? | | |
| 8 | Checkbox at the defaults showing nothing — accepted? | | |

---

*Last updated: 2026-09-26*
