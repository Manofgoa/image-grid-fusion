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

## Shared Fill

Decided 2026-11-04 (Q&A #16): the backlog's *Shared fill and grid background* direction. **One fill**
— a kind and its settings — drawn by **one painter**, used by the cell's **Background** and by this
global effect.

- **The fill** (a record in `Composition/`): its kind — **Solid**, **Linear**, **Radial** — color 1,
  color 2 (the gradients), the angle (Linear), an opacity per color (RULES.md § Color Options).
- **The painter** paints a fill into a rectangle: the **cell** for the Background, the **canvas** for
  the global effect. Its geometry is relative to that rectangle (resolution-independent).
- **The cell's Background** gets the three kinds: its flat fill becomes the shared fill. Its
  **edge-extension** modes (Corner pixel, Miter, Background corners) stay its own, drawn over the
  fill as they are drawn over the flat fill today; *Blend* fades them into the fill, whatever its
  kind.
- **Color 2 in a cell** offers Darker, Black, White and Chosen — no Auto, which belongs to the global
  effect (Q&A #19).
- **Seams** fade only the cells whose fill is **Solid**: a cell with a gradient does not fade, and its
  neighbours do not fade toward it (Q&A #20).
- **Auto colors** are given to the painter by its user: for the Background, the cell's band color
  (today's automatic color); for the global effect, cell 1's and the last cell's (§ Colors and
  Gradients). The backlog's "Auto from the grid's dominant colors" is **not** taken: the user chose
  cell 1 and the last cell.
- Out of scope, left in the backlog: conic and mesh gradients, the grain against banding, the
  pattern, blurred-image, mirror, chosen-image, frosted-glass and palette fills.

---

## Behaviour

### State

- A grid-wide state: a settings record in `Composition/` (like `SeamFade`, with an `Initial`) and an
  on / off flag, both in `GlobalState`, so the undo history covers them; **not persisted**.
- It follows RULES.md § Global Toolbar: the kept settings show while it is off; acting on any of its
  options turns it on; its own *Reset*, the tabs' *Reset* and *Clear all* bring back its **initial
  state** (off, the color following cell 1).
- **Color while never activated**: as long as the effect has **never been on**, its color follows
  the **automatic background color of cell 1** (`Compositor.AutomaticBackground`) — cell 1 empty, the
  first cell holding an image; white when the grid holds none (Q&A #11). The first time it is turned
  on, that color is taken as its own and no longer follows cell 1. Its Resets and
  **Clear all** bring back that following behaviour.

### Turning It On and Off

| Event | Cells' Background effect | Forced fill |
|---|---|---|
| Turned on (checkbox, or acting on an option) | Turned **off** in every cell (settings kept) | Painted |
| A cell's Background checkbox checked afterwards | That cell on again, drawn over the forced fill | Painted |
| Turned off (checkbox, its Reset, the tabs' Reset, Clear all) | Turned back **on** in the cells **it** turned off, their settings as kept; a cell whose Background the user turned on or off meanwhile stays as left | Not painted |
| While on: an image enters a cell (any route), an image shifts after a deletion, a cell *Reset* or the effects toolbar's *Reset* | Comes **off** instead of its default on, so the forced fill keeps showing; turning the effect off turns it back on like the others | Painted |

The effect remembers **which cells it turned off** (the images, so the memory follows a swap); a
cell the user turns on or off by hand leaves that memory.

Turning it on and off changes the cells, which RULES.md's global effects otherwise leave alone, and
while it is on the Background's default state is off: both are exceptions the rules will state
(*The Forced Background Exception*).

### Other Global Effects

- **Borders**: drawn over the forced fill, as they are drawn over the cells today; the **gap** they
  leave between the cells shows the forced fill.
- **Seams**: they fade the cells' **flat fills** into each other; cells whose Background is off have
  none, so the Seams only act between cells whose Background was turned back on.
- **Layers**: drawn last, over the forced fill and the borders. Turning the effect on or off
  **leaves the layers' Background alone** (Q&A #18): off by default on a layer, a layer whose
  Background the user turned on keeps it.

### Effect Presets

A preset saves the Background **turned off** as a choice (RULES.md § Effect Presets). A Background
the forced background turned off is not the user's choice: a preset saved from such a cell records
the Background **as the user left it before** — on, its settings (Q&A #17). A Background the user
turned off by hand while the effect is on is a choice, recorded as off. The preset's JSON gains the
shared fill's settings (`BackgroundEffect.ToJson` / `FromJson`, a missing key giving the default).

---

## Rendering

- The forced fill covers the **whole canvas** — every cell and every gap — and is painted **first**,
  in `Compositor.Draw`, before the cells, the borders and the layers, so the preview, PNG / JPG, GIF
  and MP4 all get it from one place.
- **A grid of layers only** (no layout, `Compositor.Draw`'s early return, the canvas transparent
  around the layers today): the forced fill covers its canvas too.
- Its geometry is **relative to the canvas** (fractions of its width / height, an angle), so the
  preview and an export at another size look the same.
- Cells whose Background is off are transparent behind their image, so the forced fill shows
  there; a cell whose Background is on paints over it.
- Outputs without alpha keep flattening on white **under** the forced fill; the preview keeps its
  checkerboard under it.
- In the Twitter format with the Twitter corners, the preview cuts out the rounded corners as it
  does today; the exports keep the fill in them.

---

## Colors and Gradients

- **Solid**: color 1.
- **Linear**: from color 1 to color 2 across the whole canvas, along an **angle**.
- **Radial**: from color 1 at the **center of the canvas** (fixed) to color 2 at its corners.
- **Color 1**: the effect's color — following cell 1's automatic background until first activated,
  then its own (§ State); set through the eyedropper and Color… (RULES.md § Color Options).
- **Each color has an opacity**, right after its Color… (RULES.md § Color Options): transparent
  where it is below 100 % in a PNG, over white in the outputs without alpha.
- **Color 2** (gradients): one choice among five —
  - **Auto, last cell**: the automatic background color of the last cell holding an image;
  - **Darker**: color 1 darkened;
  - **Black**;
  - **White**;
  - **Chosen**: a color picked with the eyedropper or Color….

  Its default is **Auto, last cell** in the global effect — **following the cells live** while it is
  chosen, like the cell Background's automatic color (Q&A #14, #15) — and **Darker** in a cell's
  Background, which has no Auto.

## UI

- A new **tab** of the Global toolbar, with its activation checkbox and its **icon** (RULES.md
  § Icons: an emoji, else a drawing of `EffectIcons`), after `Seams` (before `Cascade` and
  `Layers`), labelled **Grid background** (Q&A #22).
- Its options, in the global options toolbar:
  - the **mode** — Solid / Linear / Radial — as **option thumbnails** (RULES.md § Options Toolbar,
    56 × 40 pictograms);
  - color 1's **eyedropper + Color…** buttons and its **opacity** (RULES.md § Color Options);
  - for the gradients, color 2's choice among its five, then, for *Chosen*, its eyedropper + Color…
    buttons; its opacity;
  - the mode's geometry control (the angle for Linear);
  - its own **Reset**, ending the row.

---

## Test Impact

**None** (Q&A #12): the repository has no test project, and none is created; the work is checked by
hand on the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — nothing: no test project | — | — |

---

## Open Questions

- [x] ~~Which cells does the forced background apply to, when a cell's Background effect is on?~~ →
  Turning it on turns off the Background of every cell; a cell can turn its own back on afterwards.
- [x] ~~Which area does it cover?~~ → The whole exported image: cells and the gaps between them.
- [x] ~~Are gradients in scope?~~ → Yes: Solid, Linear gradient and Radial gradient.
- [x] ~~The effect's own Reset: back to its default state (off), or stays on with the default
  settings?~~ → Back to the initial state, off — settled by RULES.md § Global Toolbar, written
  since (see Iteration 2).
- [x] ~~Turning it off: what happens to the cells whose Background it turned off?~~ → Turned back on,
  those only; a cell the user changed meanwhile stays as left.
- [x] ~~While it is on, an image entering a cell (or a cell *Reset*) brings the cell's Background back
  **on**, covering the forced fill: keep that, or keep it off?~~ → Off while the effect is on.
- [x] ~~Gradient definition: two colors plus an angle (linear) / a fixed center (radial), or more?~~ →
  Two colors; an angle for the linear, the canvas center for the radial.
- [x] ~~Gradient second color: what default?~~ → Not one default: a choice among Auto (last cell),
  Darker, Black, White and a chosen color.
- [x] ~~Color 2: which of the five is the default?~~ → Auto, last cell in the global effect; Darker
  in a cell.
- [x] ~~Color 2 *Auto, last cell*: follows the cells live, or frozen like color 1 once activated?~~ →
  Live.
- [x] ~~Opacity: opaque only, or an opacity slider (PNG alpha, white under the other outputs)?~~ →
  An opacity per color, after its Color… — settled by RULES.md § Color Options, written since (see
  Iteration 4).
- [x] ~~Color while never activated, when cell 1 holds no image: which color?~~ → The first cell
  holding an image; white when none.
- [x] ~~The tab's label?~~ → **Grid background**, the backlog's name.
- [x] ~~Unit tests: none (no test project), or create one?~~ → None.
- [x] ~~Scope against the backlog: keep this workfile's own fill model, or move to the shared painter
  of *Shared fill and grid background*?~~ → The shared painter (§ Shared Fill).
- [x] ~~A preset saved from a cell whose Background the forced background turned off: save the
  Background as off, or as the user left it before (on)?~~ → As the user left it before.
- [x] ~~Turning the effect on: the layers' Background left alone, or turned off like the cells'?~~ →
  Left alone.
- [x] ~~In a **cell's** Background, color 2's *Auto, last cell* means nothing: which choices does a
  cell offer for color 2?~~ → Darker, Black, White, Chosen — no Auto.
- [x] ~~**Seams** fade the cells' flat colors into each other: with a gradient in a cell, what do they
  fade?~~ → Nothing: the Seams ignore the cells with a gradient.
- [x] ~~The backlog rows: mark *Shared fill and grid background* as designed here, and split *Gradient
  fills*?~~ → Yes: both marked, the rest of *Gradient fills* a row of its own.

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

### Iteration 3 — 2026-10-10

User answers: turned off, the effect turns back on the Background of the cells it turned off (only
those); while on, an arriving image or a cell Reset gets its Background off; gradients have two
colors, an angle for the linear, a fixed canvas center for the radial; color 2 is a choice among
Auto (last cell), Darker, Black, White and a chosen color. New questions: color 2's default, and
whether its Auto follows the cells live.

### Iteration 4 — 2026-11-04

Review against `main` at the user's request ("see whether new developments go against choices made
here"). Nothing implemented since conflicts head-on with a decision; what changed:

- **RULES.md § Color Options** (2026-10-21, 2026-10-29): every color option takes the eyedropper +
  Color… and an **opacity** — the opacity question is settled, one per color.
- **Layers** (2026-10-29): drawn last, over the borders; a grid of layers only skips the cells in
  `Compositor.Draw` and leaves its canvas transparent — the forced fill must cover it too. A layer's
  Background is off by default: whether the effect touches it is a new question.
- **Effect presets** (2026-10-26): a preset records a Background turned off as a choice — one turned
  off by the forced background would be recorded too: new question.
- **RULES.md § Icons** (2026-10-29): the tab takes an icon.
- **Backlog** (`TODO-FEATURES.md`, 2026-10-29 and 2026-11-04): *Shared fill and grid background*,
  *Gradient fills* and *Image cards over a gradient background* cover the same ground in a wider
  direction — a **Grid background** sharing one painter with the cell's Background, Auto colors from
  the grid's dominant colors (here: cell 1's and the last cell's band colors, the user's choice). The
  *Image cards* row relies on the cells' Background turned off under a global gradient, as decided
  here. Not marked yet: the scope question decides how.
- Consistent, nothing to change: the Cell copy leaves the Background out (the forced fill, grid-level,
  is not copied); the eyedropper reads `Compositor.Draw`, so it picks the forced fill; the Twitter
  corners' exports fill them.

### Iteration 5 — 2026-11-04

User answers: the scope moves to the backlog's **shared fill** — one fill and one painter for the
cell's Background and the global effect (§ Shared Fill), the cell's Background gaining the Linear and
Radial kinds, its edge extension unchanged over the fill; the auto colors stay the user's (cell 1,
last cell), not the grid's dominant colors; conic, mesh, grain and the other fills stay in the
backlog. A preset records the Background as the user left it before the forced background; the
layers' Background is left alone. New questions: color 2's choices in a cell, the Seams over a
gradient, marking the backlog rows; the tab's label now weighs *Grid background*.

### Iteration 6 — 2026-11-04

User answers: color 2 in a cell offers no Auto; the Seams ignore the cells with a gradient; the tab is
labelled **Grid background**; the backlog rows are marked — *Shared fill and grid background* and
*Gradient fills* (linear and radial) point here, conic, mesh, dominant-color Auto and grain split
into *Gradient fills, the rest*.

### Iteration 7 — 2026-11-04

Last answers: color 2 defaults to Auto, last cell (global, live) and Darker (cell); cell 1 empty, the
first cell holding an image, else white; no unit tests. No open question left.

### Iteration 8 — 2026-11-04 — ✅ Implemented

Go given: **code and documentation** (no unit tests, as decided). The scope is frozen as the design
sections stand. Run in a dedicated worktree, branch `feature/forced-background`, merged into `main`
on the user's go.

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
| 5 | Turning it off: what happens to the cells whose Background it turned off? | Turned back on — only those it turned off | 2026-10-10 |
| 6 | While on, an arriving image / a cell Reset brings Background back on: keep or keep it off? | Off | 2026-10-10 |
| 7 | The effect's own Reset: back to off, or stays on with defaults? | Not asked — settled by RULES.md § Global Toolbar (back to the initial state, off) | 2026-10-10 |
| 8 | Gradient definition: two colors + angle / fixed center, or more? | Two colors, fixed center | 2026-10-10 |
| 9 | Gradient second color default? | Offer the four (Auto last cell, Darker, Black, White) plus a chosen color | 2026-10-10 |
| 10 | Opacity: opaque only or a slider? | Not asked — settled by RULES.md § Color Options (an opacity per color) | 2026-11-04 |
| 11 | Color while never activated, cell 1 empty? | The first cell holding an image; white when none | 2026-11-04 |
| 12 | Unit tests: none or create a test project? | None | 2026-11-04 |
| 13 | The tab's label? | See #22 | 2026-11-04 |
| 14 | Color 2: which of the five is the default? | Auto, last cell (global); Darker (cell) | 2026-11-04 |
| 15 | Color 2 Auto (last cell): live, or frozen once activated? | Live | 2026-11-04 |
| 16 | Scope against the backlog: own fill model, or the shared painter of *Shared fill and grid background*? | The shared painter | 2026-11-04 |
| 17 | A preset saved from a cell whose Background the effect turned off: Background off or as before? | As before | 2026-11-04 |
| 18 | Turning the effect on: the layers' Background left alone or turned off? | Left alone | 2026-11-04 |
| 19 | In a cell's Background, which choices for color 2? | No Auto: Darker, Black, White, Chosen | 2026-11-04 |
| 20 | Seams over a gradient in a cell: what do they fade? | They ignore the gradient cells | 2026-11-04 |
| 21 | Mark the backlog rows (Shared fill: designed here; Gradient fills: split)? | Yes | 2026-11-04 |
| 22 | The tab's label? | Grid background | 2026-11-04 |

---

*Last updated: 2026-11-04*
