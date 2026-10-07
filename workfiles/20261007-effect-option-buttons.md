# Effect Option Buttons

> Working document — every choice button of the cell effects' options drawn in the Background's
> thumbnail format: a pictogram in a box, its name below.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The cell effects' options mix three looks for their buttons:

| Look | Where | Code |
|---|---|---|
| **Thumbnail** — a schematic drawing in a 56×40 box, the name below, the selected one highlighted, the hovered one lit | Background (Fill) | `BackgroundFillStrip : ThumbnailStrip<BackgroundFill>` |
| **Text button** — a grey push button holding a word | Zoom (Contain, Fill), Rotate (0°, 90°, 180°, 270°), Flip (Horizontal, Vertical) | `MainForm.OptionButton` (`CheckBox`, `Appearance.Button`) |
| **Small icon + text button** — a 16 px icon before the word | Crop (Free, 1:1, 4:3, 16:9, 9:16, 100 %), Blur (Gaussian, Pixelate) | `OptionButton` / `RadioButton` with `EffectIcons.Ratio`, `WholeImage`, `Gaussian`, `Pixelate` |

The Background's thumbnails become **the standard**: size and layout. Every other choice button of
the **cell effects** takes that format, each one getting a **pictogram** drawn for it, its name
below.

Out of scope (agreed):
- The **Global** options (Format already uses the format; Fade's curve buttons, Soundtrack, Borders
  stay as they are).
- The effects' own **Reset** button, ending the options row, and the toolbars' Reset buttons.

---

## The Standard

What "the Background's format" means, taken from `ThumbnailStrip<T>`:

- A **box** of **56 × 40** logical px (`BackgroundFillStrip.Box`), scaled to the DPI, holding the
  pictogram; the item's **name below** it, centred; an item as wide as its box or its name, whichever
  is wider, plus 4 px of padding on each side; 4 px between items.
- **Pressed** item: filled with the highlight color at low opacity, outlined 2 px in the highlight
  color. **Hovered** item: lit (`ControlLight`), hand cursor. **Disabled** strip: colors faded,
  name in `GrayText`.
- The **tooltip** of the hovered item says what it does.
- Every strip of the cell effects uses **the same box size**, so every item is the same height and
  the options toolbar keeps its height (already the Background's, its tallest).

---

## The Buttons

| Effect | Items | Pressed | Click |
|---|---|---|---|
| **Zoom** | Contain, Fill | **Zero or one** — the fit mode in force | As today: picks that fit mode; on the pressed one, leaves it for the free zoom it gave |
| **Rotate** | 0°, 90°, 180°, 270° | **Zero or one** — the one the angle falls exactly on, none with a fine angle | As today: sets that rotation, the fine angle back to 0° |
| **Flip** | Horizontal, Vertical | **Each on its own** — none, either or both | As today: toggles that flip |
| **Crop** | Free, 1:1, 4:3, 16:9, 9:16 | **Zero or one** — the ratio kept | As today: keeps that ratio |
| | 100 % | **Never** — an action | As today: the kept part back to the whole image, the ratio freed |
| **Blur** | Gaussian, Pixelate | **Exactly one** | As today: picks that kind |

- Behaviour, tooltips and the activation rule (§ Options Toolbar: acting on an option turns the
  effect on) **do not change** — only the look.
- The controls keep their **place** in each row: Zoom's slider and label before Contain / Fill,
  Rotate's quarter turns before the fine angle, Blur's kinds before the intensity.

### Pictograms

To be settled (see Open Questions). Draft, one per item, in the Background's vocabulary — the
**cell** a grey box, the **image** a white rectangle outlined dark:

| Item | Pictogram |
|---|---|
| Contain | The image whole inside the cell, bands on two sides |
| Fill | The image larger than the cell, its overflow drawn faded beyond the cell's outline |
| 0° / 90° / 180° / 270° | An asymmetric image (a landscape rectangle with a marker in one corner) turned by that angle |
| Horizontal / Vertical | The asymmetric image and its mirror on either side of a dashed axis, vertical / horizontal |
| Free / 1:1 / 4:3 / 16:9 / 9:16 | The image with the kept part at that ratio (Free: a dashed kept part), the cut-off part dimmed |
| 100 % | The whole image, the bars on its edges |
| Gaussian / Pixelate | A shape softened / a shape in coarse squares |

### Code

- `ThumbnailStrip<T>` holds **one selected value** and is built over an **enum**. The new strips
  need zero, one or several pressed items, an action item, and the crop's ratios (a list of
  doubles, not an enum). The strip is **generalized** — exact shape decided at implementation
  (e.g. items given as a list, a per-item pressed state) — without changing the Background's and
  the Format's behaviour.
- One derived strip per effect (or per group of items), next to `BackgroundFillStrip`, drawing its
  pictograms.
- `MainForm`: the `OptionButton` / `RadioButton` fields replaced by the strips; their `Checked`
  syncing (`MainForm.cs` ~2690–2735) becomes the strips' pressed state; the DPI icon refresh
  (`MainForm.cs` ~790–830) loses the icons no longer used. `OptionButton` is removed if nothing
  uses it any more; the `EffectIcons` drawings no longer used are removed.

### Rule

`RULES.md` § Effects › Options Toolbar gains: **a choice among values in a cell effect's options is
a thumbnail** of the Background's format (`ThumbnailStrip`) — a pictogram, its name below — so a new
effect follows it.

---

## Test Impact

The repository has **no test project**: nothing testable by a unit test changes in a way an
existing suite covers. Checked by hand at launch (every row's look, pressed states, clicks, DPI,
disabled state).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] The plain **checkboxes** of the cell effects' options — Frames' *Freeze*, Volume's *Mute*,
  Background's *Automatic color* and *Soften* — and Background's *Color…* button: do they stay as
  they are?
- [ ] The Animations' **Type** drop-down (one value, Zoom, for now): stays a drop-down, or becomes a
  strip?
- [ ] The **pictograms' style**: the Background's schematic vocabulary (grey cell, white image), or
  the existing 16 px `EffectIcons` drawn larger?
- [ ] Crop's **100 %** (an action, never pressed): in the same strip as the ratios, set apart by a
  wider gap, or as it is in the Background's — no distinction?

---

## Design Iterations

### Iteration 1 — 2026-10-07

Initial design from the request and the scoping answers: cell effects only; the text-only buttons
get a pictogram and their name below; the effects' Reset buttons untouched. The Background's
`ThumbnailStrip` format becomes the standard for Zoom, Rotate, Flip, Crop and Blur; the strip is
generalized for zero / several pressed items and action items; a rule is added to RULES.md.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository |
| RULES.md | | | |
| README / README.fr | | | |
| GLOSSARY / GLOSSARY.fr | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Scope of the harmonization | Cell effects only | 2026-10-07 |
| 2 | What the text-only buttons become | A pictogram, their name below | 2026-10-07 |
| 3 | The effect's own Reset button | Left as it is | 2026-10-07 |
| 4 | Exploration depth | Straightforward | 2026-10-07 |
| 5 | Plain checkboxes and *Color…*: stay as they are? | | |
| 6 | Animations' Type drop-down: stays or becomes a strip? | | |
| 7 | Pictograms' style | | |
| 8 | Crop's 100 %: set apart or not? | | |

---

*Last updated: 2026-10-07*
