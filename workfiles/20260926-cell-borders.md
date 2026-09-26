# Cell Borders

> Working document — a **Borders** global effect drawing lines between the cells of the grid, with
> an optional outer frame: thickness and style customisable, a signature *Corners* style by
> default, its color chosen from the ⚙ settings menu and remembered between sessions.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the cells are laid **edge to edge** with nothing between them. The **Borders** effect draws
borders on the grid.

- It is a **global effect** (RULES.md § Global Effects), the second after the Soundtrack: it
  belongs to the **grid**, not to a cell + image pair.
- Its toggle and its options sit in the **Global effects row**, next to the Soundtrack.
- Styles: **Corners** (signature style of the app, the default), **Solid**, **Dashed**,
  **Dotted**, **Double**.
- **Corners** style: an L-bracket at each of the **four corners of the grid**, painted over the
  images, its arms covering 10 % of the edge they lie on — nothing between the cells.
- Other styles: a real **gap between the cells**, the cells shrinking to make room, filled by the
  border; plus an optional **outer frame** (a margin of the same thickness around the grid).
- **On at start-up**, in the Corners style.
- The border color is an **app setting** set from the **⚙ settings menu** (an item with a color
  swatch opening the color dialog), **hotpink** by default, **remembered between sessions**.
- Outputs: preview, still export, GIF and video export all show the borders.

---

## Current State (explored)

Explored on 2026-09-26, before the Global effects row landed; line numbers may have moved.

| Topic | Where | What it does today |
|---|---|---|
| Cell rectangles | `Composition/GridLayout.cs` `Cells(Size canvas)` | Tiles the canvas edge to edge from unit fractions; neighbours share their boundary; no gap |
| Grid drawing | `Composition/Compositor.cs` `Draw(g, frames, layout, canvas)` | Computes the cells then loops `DrawCell` — the one entry point shared by every output |
| Outputs | `UI/GridPreview.cs` (cache repaint), `Imaging/GridExport.cs` (video / GIF frames via `Draw`, still via `Compositor.Render`) | All go through `Compositor.Draw` |
| Preview fast path | `UI/GridPreview.cs` (live drag) | Redraws a single cell with `DrawCell` directly — bypasses `Draw` |
| Resolution | `Composition/CanvasSizer.cs` | Export canvas 1200–4096 px wide; everything is canvas-relative, no explicit scale factor |
| Global effects row | `UI/MainForm.cs` `_globalRow` (~140-149, 233, 427, 1599) | `FlowLayoutPanel` above the bottom bar: label, Soundtrack toggle and its options; height = tallest control; disabled while exporting |
| Global effect state | `UI/MainForm.cs` `_soundtrackOn`, `ActiveSoundtrack` (~956, 1588) | Toggle flag + kept settings; the active value is pushed to the preview |
| Clear all | `UI/MainForm.cs` `ClearAll()` (~740) | Empties the grid — resets global effects to their initial state (RULES.md) |
| Settings menu | `UI/MainForm.cs` `_settingsButton`, `_settingsMenu`, `ShowSettings()` | ⚙ button + `ContextMenuStrip`, one item today: *Start with Windows* |
| Persistence | `UI/StartupRegistration.cs` | Only *Start with Windows*, as a `HKCU\…\Run` registry value; no settings file, no settings class |
| Tests | — | No test project in the solution |

---

## Effect State

- A new immutable record `GridBorders`, owned by the **grid** (held by `MainForm` like the
  Soundtrack, passed to the preview, to `Compositor.Draw` and to the export jobs):

  | Field | Initial value | Meaning |
  |---|---|---|
  | `Style` | `Corners` | `Corners`, `Solid`, `Dashed`, `Dotted`, `Double` |
  | `Thickness` | `0.006` (0.6 %) | Fraction of the grid's shorter side, 0.1–6 % — resolution-independent |
  | `OuterFrame` | `false` | Also draw along the outer edge of the grid (not used by Corners) |

- The color is **not** part of the effect state: it is read from the app setting (see *Settings
  Menu*) when drawing.
- A toggle flag next to it, as for the Soundtrack: **on** at start-up. Turned off, the settings are
  kept; turned on again, they apply as they were.
- Per RULES.md § Global Effects: untouched by an image replaced, by the cell *Reset* buttons, by a
  swap or a layout change; **Clear all** brings it back to its initial state (on, Corners,
  defaults). Not persisted.

## Global Effects Row

- After the Soundtrack's controls: a **Borders** toggle, then, **only while it is on**, its options
  beside it:
  1. **Style** — five choices, *Corners* first.
  2. **Thickness** slider, **0.1 – 6.0 %** by steps of 0.1, default **0.6 %**, with its value
     label (`0.6 %`).
  3. **Outer frame** checkbox — **disabled in the Corners style** (the brackets already are the
     frame), its value kept for the other styles.
- Enabled with no cell selected; locked while exporting, like the rest of the row.
- The row keeps its single height (tallest control).

## Settings Menu

- New item in the ⚙ menu: **Border color**, with a **color swatch** painted in the current color.
- Default color, until the user picks one: **hotpink** (HTML `HotPink`, `#FF69B4` —
  `Color.HotPink`).
- A click opens the standard `ColorDialog`, preselected on the current color. **OK** → new color,
  applied at once to the grid and **persisted**; **Cancel** → nothing changes.
- Persisted per user in the registry (`HKCU\Software\ImageGridFusion`), following the
  *Start with Windows* precedent — no settings file.
- It is the **only** border color: no color control in the Borders options.

## Rendering

Everything happens in `Compositor.Draw` (RULES.md: global effects render at the grid level), so
the preview, the still export and the GIF / video frames get it from one place.

### Gap Between the Cells (Solid, Dashed, Dotted, Double)

- Gap width `t` = thickness × the canvas's shorter side, in pixels of the canvas being drawn —
  the same look at every size.
- The canvas size does not change (`CanvasSizer` untouched): each **cell shrinks** by `t / 2` on
  every edge it shares with a neighbour. With the outer frame, it also shrinks by `t` on every edge
  lying on the canvas edge; without it, those edges stay on the canvas edge.
- The shrunk rectangles are what `DrawCell` receives: image, fit, crop and every cell effect work
  on them, as on any cell size (effect geometry is in fractions of the cell).
- The gap is then filled by the style:

  | Style | Gap fill |
  |---|---|
  | Solid | Filled with the border color |
  | Dashed | Dashes across the gap, length and spacing scaled with `t` |
  | Dotted | Round dots of diameter `t`, spacing scaled with `t` |
  | Double | Two parallel lines, each ⅓ of `t`, ⅓ of `t` apart |

- Where the style leaves the gap unpainted (between dashes, dots, the Double's middle), it is
  **transparent**, like a cell whose Background is off: the preview's checkerboard, a PNG's alpha,
  white in the outputs without alpha.
- Shared gap segments are drawn **once** (deduplicated), so dash patterns stay regular on
  irregular layouts.

### Corners Style

- No gap, cells unchanged: an L-bracket **painted over the images** at each of the four corners of
  the canvas, inside it.
- Stroke width = `t`; each arm covers **10 % of the edge it lies on** (horizontal arm = 10 % of the
  width, vertical arm = 10 % of the height).

### Preview

- Hit-testing (selection, hover, drop target, swap) keeps using the **unshrunk cell slots**: a point
  in a gap belongs to the cell whose slot contains it, so no dead zone appears between cells.
- Outlines and on-cell handles follow the **shrunk** rectangle, where the image is drawn.
- The live-drag fast path repaints with the same shrunk rectangle.

## Rules and Glossary

To record with the README step:

- RULES.md § Global Effects: a global effect may read an **app setting** persisted outside its
  state (the Borders color) — the effect state itself stays not persisted.
- GLOSSARY: *Global effect* lists Borders; new entry *Corners style*.

---

## Test Impact

None, by the user's decision (Q&A #20): the solution has no test project, and every previous
workfile stayed test-free.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 — not only the
blocking ones.

- [x] ~~**Placement** — Grid group of the effects toolbar, or the new Global effects row?~~ →
  Global effects row, next to the Soundtrack *(revised 2026-09-26, see Iteration 3)*
- [x] ~~**Corners geometry** — every cell, or the four corners of the grid?~~ → the four corners
  of the grid only
- [x] ~~**Corners arm length** — 10 % of the edge the arm lies on, or of the grid's shorter
  side?~~ → 10 % of the edge the arm lies on
- [x] ~~**Outer frame in the Corners style** — checkbox disabled, or full frame added?~~ →
  checkbox disabled
- [x] ~~**Color model** — the settings color is the border color, or a default?~~ → **the** border
  color, no color control in the options
- [x] ~~**Start-up state and persistence**~~ → on at start-up in the Corners style; only the color
  is remembered
- [x] ~~**Overlay or gap** — do the borders cover the edges of the images, or shrink the cells?~~
  → a real gap, the cells shrink (Corners excepted: brackets over the images)
- [x] ~~**Thickness** — range and default?~~ → 0.1–6.0 % of the grid's shorter side, default 0.6 %
- [x] ~~**Default color**~~ → hotpink (`#FF69B4`)
- [x] ~~**Effects *Reset* button** — does it leave the borders alone?~~ → yes, settled by RULES.md
  § Global Effects
- [x] ~~**Unit tests** — stay test-free like every previous workfile?~~ → yes, no tests

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the user's brief and the scoping answers (Q&A #1–#4): global effect in a
*Grid* group of the effects toolbar; borders between the cells with an optional outer frame;
styles Solid, Dashed, Dotted, Double. Codebase explored in one scout pass (depth:
straightforward) — findings in *Current State*.

Added from the user's follow-up message during exploration: a signature **Corners** style, the
default — brackets at each corner covering 10 % of each edge; its color set from the ⚙ settings
menu (item with a color swatch opening the color dialog), remembered between sessions.

### Iteration 2 — 2026-09-26

The user dismissed the first question batch (Q&A #5–#8, still open) and gave the default border
color: **hotpink** (`#FF69B4`) — recorded in *Settings Menu*.

### Iteration 3 — 2026-09-26

RULES.md changed meanwhile (commits by other sessions): effect tabs with activation checkboxes,
and a new **§ Global Effects** with a **Global effects row** above the bottom bar (Soundtrack
first). The design is aligned on it:

- Borders moves from the effects toolbar's *Grid* group to the **Global effects row** (Q&A #13,
  revising Q&A #1); options beside the toggle while on; *Clear all* resets it; cell *Reset* buttons
  never touch it (Q&A #11 settled by the rule).
- **Corners** at the four corners of the grid only (Q&A #14) — every other style draws between the
  cells.
- The ⚙ color is **the** border color (Q&A #15); borders **on at start-up** in the Corners style,
  only the color remembered (Q&A #16).
- *Current State* refreshed for the Global effects row.

### Iteration 4 — 2026-09-26

Last answers (Q&A #17–#20):

- **Gap between the cells** for every style but Corners: the canvas keeps its size, the cells shrink
  by half the gap on shared edges (and by the full gap on outer edges with the outer frame); the
  style fills the gap. Corners stays painted over the images.
- Thickness **0.1–6.0 %** of the grid's shorter side, default **0.6 %**.
- Corners arms = 10 % of the edge they lie on; the *Outer frame* checkbox is disabled in the
  Corners style.
- No unit tests.

Derived by the agent, to confirm with the go: what a style leaves unpainted in the gap is
**transparent** (same rendering as a Background turned off); hit-testing keeps the unshrunk slots
so a gap is never a dead zone, while outlines and handles follow the shrunk rectangle.

### Iteration 5 — 2026-09-26 — ✅ Implemented

Go given: *code, tests and documentation* (Q&A #21) — no unit tests by decision (Q&A #20), so the
documentation step covers README, RULES.md and GLOSSARY.md. Branch: `main`, the repository's
standing choice.

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
| 1 | Where does the Borders setting go, being global to the grid? | A *Grid* group in the effects toolbar *(revised by #13)* | 2026-09-26 |
| 2 | Which borders are concerned? | Between cells, plus an optional outer frame | 2026-09-26 |
| 3 | Which line styles? | Solid, Dashed, Dotted, Double (+ Corners, from the follow-up message) | 2026-09-26 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — single scout pass | 2026-09-26 |
| 5 | Corners: every cell or grid corners only? Arm length basis? | Dismissed — asked again as #14 | 2026-09-26 |
| 6 | Settings color: the border color, or a default the options override? | Dismissed — asked again as #15 | 2026-09-26 |
| 7 | Active at start-up? What is remembered besides the color? | Dismissed — asked again as #16 | 2026-09-26 |
| 8 | Borders over the images, or cells shrunk to make room? | Dismissed — asked again as #17 | 2026-09-26 |
| 9 | Thickness range and default? | Not asked yet — see #18 | 2026-09-26 |
| 10 | Default color? | Hotpink (HTML), `#FF69B4` | 2026-09-26 |
| 11 | Does the effects *Reset* button leave the borders alone? | Settled by RULES.md § Global Effects: yes | 2026-09-26 |
| 12 | Unit tests: stay test-free? | Not asked yet — see #20 | 2026-09-26 |
| 13 | Placement now that RULES.md has a Global effects row? | Global effects row | 2026-09-26 |
| 14 | Corners: every cell, equal arms, or the grid's four corners? | The grid's four corners only | 2026-09-26 |
| 15 | Is the ⚙ color **the** border color, or a default? | **The** border color | 2026-09-26 |
| 16 | Borders on at start-up (and after *Clear all*)? | On, Corners style | 2026-09-26 |
| 17 | Borders over the images, or cells shrunk to make room? | A gap between the cells | 2026-09-26 |
| 18 | Thickness range and default? | 0.1–6.0 %, default 0.6 % | 2026-09-26 |
| 19 | Corners arm length basis; Outer frame checkbox in the Corners style? | 10 % of their edge; checkbox disabled | 2026-09-26 |
| 20 | Unit tests: stay test-free? | No tests | 2026-09-26 |
| 21 | Implementation go? | Code, tests and documentation | 2026-09-26 |

---

*Last updated: 2026-09-26*
