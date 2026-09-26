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
- **Corners** style: an L-bracket at each of the **four corners of the grid**, its arms covering
  10 % of the edges — nothing between the cells.
- Other styles: lines **between the cells**, plus an optional **outer frame**.
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
  | `Thickness` | *see Open Questions* | Fraction of the grid's shorter side — resolution-independent |
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
  2. **Thickness** slider with its value label.
  3. **Outer frame** checkbox — *see Open Questions* for its state in the Corners style.
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

- A **border pass** at the end of `Compositor.Draw`, after the `DrawCell` loop, from the cells
  already computed: every output gets it from one place (RULES.md: global effects render at the
  grid level).
- The live-drag fast path redraws the borders over the cell it repaints.
- Styles:

  | Style | Drawing |
  |---|---|
  | Corners | An L-bracket at each of the four corners of the grid, drawn inside the canvas; each arm covers 10 % of the grid edge it lies on |
  | Solid | Continuous line |
  | Dashed | GDI+ dash pattern, scaled with the thickness |
  | Dotted | Round dots, spacing scaled with the thickness |
  | Double | Two parallel lines, each ⅓ of the thickness, ⅓ gap between them |

- Lines between the cells (all styles but Corners) are **centred on the cell boundaries**; shared
  edges between neighbours are drawn **once** (deduplicated), so dash patterns stay regular on
  irregular layouts.
- Outer frame: drawn **inside** the canvas along its edge, with the same visible thickness as an
  inner border.

## Rules and Glossary

To record with the README step:

- RULES.md § Global Effects: a global effect may read an **app setting** persisted outside its
  state (the Borders color) — the effect state itself stays not persisted.
- GLOSSARY: *Global effect* lists Borders; new entry *Corners style*.

---

## Test Impact

To be settled with the user (see *Open Questions*): the solution has no test project, and every
previous workfile stayed test-free.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 — not only the
blocking ones.

- [x] ~~**Placement** — Grid group of the effects toolbar, or the new Global effects row?~~ →
  Global effects row, next to the Soundtrack *(revised 2026-09-26, see Iteration 3)*
- [x] ~~**Corners geometry** — every cell, or the four corners of the grid?~~ → the four corners
  of the grid only
- [ ] **Corners arm length** — 10 % of the edge the arm lies on (unequal arms on a non-square
  grid), or 10 % of the grid's shorter side (equal arms)?
- [ ] **Outer frame in the Corners style** — the checkbox disabled (the brackets already are the
  frame), or does it add the full frame around the brackets?
- [x] ~~**Color model** — the settings color is the border color, or a default?~~ → **the** border
  color, no color control in the options
- [x] ~~**Start-up state and persistence**~~ → on at start-up in the Corners style; only the color
  is remembered
- [ ] **Overlay or gap** — do the borders cover the edges of the images (cells unchanged), or
  shrink the cells to leave room for them?
- [ ] **Thickness** — range and default?
- [x] ~~**Default color**~~ → hotpink (`#FF69B4`)
- [x] ~~**Effects *Reset* button** — does it leave the borders alone?~~ → yes, settled by RULES.md
  § Global Effects
- [ ] **Unit tests** — stay test-free like every previous workfile?

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
| 17 | Borders over the images, or cells shrunk to make room? | | 2026-09-26 |
| 18 | Thickness range and default? | | 2026-09-26 |
| 19 | Corners arm length basis; Outer frame checkbox in the Corners style? | | 2026-09-26 |
| 20 | Unit tests: stay test-free? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
