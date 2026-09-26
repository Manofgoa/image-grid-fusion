# Forced Background

> Working document — a global effect forcing one background (a solid color, a linear or a radial
> gradient) behind every cell and the gaps between them, computed over the whole exported image.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A second **global effect** (RULES.md § Global Effects), after the Soundtrack: **Forced background**
(*fond forcé*). Off by default. Turned on, it paints one fill over the **whole grid** — cells and the
gaps between them — and turns off the **Background** cell effect of every cell, so the forced fill
shows behind every image. A cell can turn its own Background back on afterwards; it is then drawn
over the forced fill, as today.

Three modes:

- **Solid** — one color.
- **Linear gradient** — spanning the whole exported image, not repeated per cell.
- **Radial gradient** — idem, around a center of the exported image.

Relevant components (from the scout pass):

- `src/ImageGridFusion/UI/MainForm.cs` — the **Global effects row** (`_globalRow`, line ~140), its
  label and the `♪ Soundtrack` toggle with its options; the grid-wide state lives as `MainForm`
  fields (no grid state class).
- `src/ImageGridFusion/Composition/Compositor.cs` — `Render` (line ~46) starts from a transparent
  ARGB bitmap, `Draw` loops over the cells, `DrawCell` (line ~146) fills the cell Background from
  `ImageLook.Background` / `BackgroundEffect.Fill`; `AutomaticBackground(frame, cell)` (line ~93)
  gives a cell's automatic band color. The gaps between cells are never painted today.
- `src/ImageGridFusion/Composition/BackgroundEffect.cs` — the cell Background effect (automatic or
  chosen color, opacity).
- `GridExport.cs` — white under the image for JPG (`Flattened`) and for GIF / MP4 frames
  (`g.Clear(Color.White)` line ~149); PNG keeps the alpha.
- `GridPreview.PaintCheckerboard` (UI/GridPreview.cs ~1015) — the preview's checkerboard under
  transparent areas, painted before the cells.

---

## Behaviour

### State

- A grid-wide state, a `MainForm` field like the soundtrack's, **not persisted**.
- Its settings are kept while it is off (RULES.md, Global Effects Row); turned on again, it applies
  them as they were.
- **Color while never activated**: as long as the effect has **never been on**, its color follows
  the **automatic background color of cell 1** (the cell's band color, `Compositor.AutomaticBackground`).
  The first time it is turned on, that color is taken as its own and no longer follows cell 1.
  Its own **Reset** and **Clear all** bring back that following behaviour.

### Turning It On and Off

| Event | Cells' Background effect | Forced fill |
|---|---|---|
| Toggle turned on | Turned **off** in every cell (settings kept) | Painted |
| A cell's Background checkbox checked afterwards | That cell on again, drawn over the forced fill | Painted |
| Toggle turned off | *Open question* | Not painted |

### Global Effect Rules (from RULES.md § Global Effects)

- In the Global effects row, toggle + options beside it, options shown only while it is on; the row
  keeps one height; locked while exporting.
- Stays enabled with no cell selected.
- Untouched by an image replacement and by the cell *Reset* buttons; kept on swap and layout change.
- **Clear all** resets it — back to its initial state (off, color following cell 1).

---

## Rendering

- The forced fill covers the **whole canvas** — every cell and every gap — and is painted **before
  the cells**, once per render, in `Compositor.Draw` (or one shared helper it calls), so the
  preview, PNG / JPG, GIF and MP4 all get it from one place.
- Its geometry is **relative to the canvas** (fractions of its width / height, angle), so the
  preview and an export at another size look the same (resolution-independent, like the cell
  effects).
- Cells whose Background is off are transparent behind their image, so the forced fill shows
  there; a cell whose Background is on paints over it.
- Outputs without alpha keep flattening on white **under** the forced fill; the preview keeps its
  checkerboard under it.

---

## UI (proposal)

In the Global effects row, after the Soundtrack's controls:

- Toggle `▣ Forced background` (an `OptionButton`, like `♪ Soundtrack`).
- While on: a **mode** selector (Solid / Linear / Radial), the **color** button(s), the mode's
  geometry control (angle for Linear), and a **Reset** button ending its options.

---

## Test Impact

No test project exists in the repository (`src/` holds `ImageGridFusion` only). Pending the answer
to the tests question below.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *Pending — see Open Questions* | | |

---

## Open Questions

- [x] ~~Which cells does the forced background apply to, when a cell's Background effect is on?~~ →
  Turning it on turns off the Background of every cell; a cell can turn its own back on afterwards.
- [x] ~~Which area does it cover?~~ → The whole exported image: cells and the gaps between them.
- [x] ~~Are gradients in scope?~~ → Yes: Solid, Linear gradient and Radial gradient.
- [ ] Turning the toggle off: what happens to the cells whose Background it turned off?
- [ ] While it is on, an image entering a cell (or a cell *Reset*) brings the cell's Background back
  **on** (its default state), covering the forced fill: keep that, or keep it off?
- [ ] The effect's own Reset: back to its default state (off), or stays on with the default settings?
- [ ] Gradient definition: two colors plus an angle (linear) / a fixed center (radial), or more?
- [ ] Gradient second color: what default?
- [ ] Opacity: opaque only, or an opacity slider (PNG alpha, white under the other outputs)?
- [ ] Color while never activated, when cell 1 holds no image: which color?
- [ ] Unit tests: none (no test project), or create one?

---

## Design Iterations

### Iteration 1 — 2026-09-26

Initial design from the user's brief and the scoping answers: a second global effect, in the
Global effects row, off by default; turning it on turns off every cell's Background; it covers the
whole canvas including the gaps; Solid / Linear / Radial modes. Late user input: while never
activated, its color follows cell 1's automatic background color, restored by its Reset and by
Clear all. Rendering placed as one canvas fill before the cell loop in `Compositor.Draw`, since
cells with their Background off are already transparent.

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
| 1 | With the forced background on, what happens to a cell whose Background effect is on? | It turns off Background in every cell; it can be turned back on in a cell afterwards | 2026-09-26 |
| 2 | Which area does it cover? | Cells and the gaps between them | 2026-09-26 |
| 3 | Are the gradient modes in scope? | Solid + linear + radial | 2026-09-26 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-26 |
| 5 | Turning the toggle off: what happens to the cells whose Background it turned off? | | |
| 6 | While on, an arriving image / a cell Reset brings Background back on: keep or keep it off? | | |
| 7 | The effect's own Reset: back to off, or stays on with defaults? | | |
| 8 | Gradient definition: two colors + angle / fixed center, or more? | | |
| 9 | Gradient second color default? | | |
| 10 | Opacity: opaque only or a slider? | | |
| 11 | Color while never activated, cell 1 empty? | | |
| 12 | Unit tests: none or create a test project? | | |

---

*Last updated: 2026-09-26*
