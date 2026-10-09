# Forced Background

> Working document — a global effect forcing one background (a solid color, a linear or a radial
> gradient) behind every cell and the gaps between them, computed over the whole exported image.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A new **global effect** (RULES.md § Global Effects), a tab of the **Global toolbar**: **Forced
background** (*fond forcé*). Off at start-up. Turned on, it paints one fill over the **whole grid**
— cells and the gaps between them — and turns off the **Background** cell effect of every cell, so
the forced fill shows behind every image. A cell can turn its own Background back on afterwards; it
is then drawn over the forced fill, as today.

Three modes:

- **Solid** — one color.
- **Linear gradient** — spanning the whole exported image, not repeated per cell.
- **Radial gradient** — idem, around a center of the exported image.

Relevant components (checked against `main` on 2026-10-10):

- `Composition/GlobalEffect.cs` — the Global toolbar's tabs, in order: `Format`, `Soundtrack`,
  `Fade`, `Borders`, `Seams`, `Cascade`; the tab label is the enum value's name.
- `UI/MainForm.cs` — `_globalTabs` (`EffectTabs<GlobalEffect>`, line ~253), the global options
  toolbar, the global *Reset* button; `CaptureState` / `RestoreState` for the undo history.
- `UI/GridHistory.cs` — `GlobalState` (line ~11), one `<Effect>` + `<Effect>On` pair per global
  effect.
- `Composition/Compositor.cs` — `Render` (line ~48) starts from a transparent ARGB bitmap;
  `Draw` (line ~126) computes the slots, insets them for the `GridBorders`, draws every cell
  (`DrawCell`, which fills the cell Background), then the borders over everything. The gaps
  between cells are never painted.
- `Composition/BackgroundEffect.cs` — the cell Background effect; `Compositor.AutomaticBackground`
  gives a cell's automatic band color.
- `Composition/SeamFade.cs` / `SeamField.cs` — the Seams global effect, fading the cells' flat fills
  into each other.
- White under the image for the outputs without alpha (`Compositor` lines ~69 / ~89); the preview's
  checkerboard under transparent areas (`GridPreview.PaintCheckerboard`).

---

## Behaviour

### State

- A grid-wide state: a settings record in `Composition/` (like `SeamFade`, with an `Initial`) and an
  on / off flag, both in `GlobalState`, so the undo history covers them; **not persisted**.
- It follows RULES.md § Global Toolbar: the kept settings show while it is off; acting on any of its
  options turns it on; its own *Reset*, the tabs' *Reset* and *Clear all* bring back its **initial
  state** (off, the color following cell 1).
- **Color while never activated**: as long as the effect has **never been on**, its color follows
  the **automatic background color of cell 1** (`Compositor.AutomaticBackground`). The first time it
  is turned on, that color is taken as its own and no longer follows cell 1. Its Resets and
  **Clear all** bring back that following behaviour.

### Turning It On and Off

| Event | Cells' Background effect | Forced fill |
|---|---|---|
| Turned on (checkbox, or acting on an option) | Turned **off** in every cell (settings kept) | Painted |
| A cell's Background checkbox checked afterwards | That cell on again, drawn over the forced fill | Painted |
| Turned off (checkbox, its Reset, the tabs' Reset, Clear all) | *Open question* | Not painted |

Turning it on changes the cells, which RULES.md's global effects otherwise leave alone: this is an
exception the rules will state (*The Forced Background Exception*).

### Other Global Effects

- **Borders**: drawn over the forced fill, as they are drawn over the cells today; the **gap** they
  leave between the cells shows the forced fill.
- **Seams**: they fade the cells' **flat fills** into each other; cells whose Background is off have
  none, so the Seams only act between cells whose Background was turned back on.

---

## Rendering

- The forced fill covers the **whole canvas** — every cell and every gap — and is painted **first**,
  in `Compositor.Draw`, before the cells and the borders, so the preview, PNG / JPG, GIF and MP4 all
  get it from one place.
- Its geometry is **relative to the canvas** (fractions of its width / height, an angle), so the
  preview and an export at another size look the same.
- Cells whose Background is off are transparent behind their image, so the forced fill shows
  there; a cell whose Background is on paints over it.
- Outputs without alpha keep flattening on white **under** the forced fill; the preview keeps its
  checkerboard under it.
- In the Twitter format with the Twitter corners, the preview cuts out the rounded corners as it
  does today; the exports keep the fill in them.

---

## UI

- A new **tab** of the Global toolbar, with its activation checkbox, after `Seams` (before
  `Cascade`): label *Open question* (proposal: `Backdrop`).
- Its options, in the global options toolbar:
  - the **mode** — Solid / Linear / Radial — as **option thumbnails** (RULES.md § Options Toolbar,
    56 × 40 pictograms);
  - the **color** button(s);
  - the mode's geometry control (the angle for Linear);
  - its own **Reset**, ending the row.

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
- [x] ~~The effect's own Reset: back to its default state (off), or stays on with the default
  settings?~~ → Back to the initial state, off — settled by RULES.md § Global Toolbar, written
  since (see Iteration 2).
- [ ] Turning it off: what happens to the cells whose Background it turned off?
- [ ] While it is on, an image entering a cell (or a cell *Reset*) brings the cell's Background back
  **on** (its default state), covering the forced fill: keep that, or keep it off?
- [ ] Gradient definition: two colors plus an angle (linear) / a fixed center (radial), or more?
- [ ] Gradient second color: what default?
- [ ] Opacity: opaque only, or an opacity slider (PNG alpha, white under the other outputs)?
- [ ] Color while never activated, when cell 1 holds no image: which color?
- [ ] The tab's label?
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

### Iteration 2 — 2026-10-10

Design rebased on `main`, which moved on while the design was paused:

- The *Global effects row* became the **Global toolbar** (`workfiles/20260926-global-effects-tabs.md`):
  the effect is now a **tab** with an activation checkbox, its options in the global options
  toolbar, the mode chosen with **option thumbnails**.
- RULES.md now states that the global Resets and *Clear all* bring back the **initial state** (off):
  the Reset question is settled.
- The **Borders** (gap, frame) and the **Seams** exist: the forced fill goes under the borders and
  shows in their gap; the Seams only act between cells whose Background is on.
- The **undo history** exists: the effect's state joins `GlobalState`.
- Turning it on changes the cells, while RULES.md's global Resets "leave the cells alone": recorded
  as an exception to state in the rules.
- New open question: the tab's label.

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
| 5 | Turning it off: what happens to the cells whose Background it turned off? | | |
| 6 | While on, an arriving image / a cell Reset brings Background back on: keep or keep it off? | | |
| 7 | The effect's own Reset: back to off, or stays on with defaults? | Not asked — settled by RULES.md § Global Toolbar (back to the initial state, off) | 2026-10-10 |
| 8 | Gradient definition: two colors + angle / fixed center, or more? | | |
| 9 | Gradient second color default? | | |
| 10 | Opacity: opaque only or a slider? | | |
| 11 | Color while never activated, cell 1 empty? | | |
| 12 | Unit tests: none or create a test project? | | |
| 13 | The tab's label? | | |

---

*Last updated: 2026-10-10*
