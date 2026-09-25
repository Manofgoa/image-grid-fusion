# Cell Borders

> Working document — a **Borders** effect drawing lines between the cells of the grid, with an
> optional outer frame: thickness, color and style customisable, a signature *Corners* style by
> default, its color chosen from the ⚙ settings menu and remembered between sessions.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the cells are laid **edge to edge** with nothing between them. The **Borders** effect draws
borders along the cell boundaries of the whole grid.

- It is the first **global effect**: it belongs to the **grid**, not to a cell + image pair.
- Its toggle sits in a new **Grid** group of the effects toolbar, after a separator; its options
  (style, thickness, outer frame, color) go in the options toolbar.
- Styles: **Corners** (signature style of the app, the default), **Solid**, **Dashed**,
  **Dotted**, **Double**.
- **Corners** style: at each corner, a bracket whose arms cover **10 % of each edge**.
- The border color is set from the **⚙ settings menu** (an item with a color swatch opening the
  color dialog) and **remembered between sessions**.
- Outputs: preview, still export, GIF and video export all show the borders.

It departs from the per-cell effect rules of [RULES.md](../RULES.md) where the grid scope makes
them meaningless (see *Rules Amended*).

---

## Current State (explored)

| Topic | Where | What it does today |
|---|---|---|
| Cell rectangles | `Composition/GridLayout.cs:102-107` | `Cells(Size canvas)` tiles the canvas edge to edge from unit fractions; neighbours share their boundary; no gap |
| Grid drawing | `Composition/Compositor.cs:50-61` | `Draw(g, frames, layout, canvas)` computes the cells then loops `DrawCell` — the one entry point shared by every output |
| Outputs | `UI/GridPreview.cs:353`, `Imaging/GridExport.cs:133` (video / GIF frames), `Imaging/GridExport.cs:185` (still, via `Compositor.Render`) | All go through `Compositor.Draw` |
| Preview fast path | `UI/GridPreview.cs:925` | Redraws a single cell with `DrawCell` directly (live drag) — bypasses `Draw` |
| Resolution | `Composition/CanvasSizer.cs` | Export canvas 1200–4096 px wide; everything is canvas-relative, no explicit scale factor |
| Effects toolbar | `UI/EffectTabs.cs` | Custom-painted control, one tab per `ImageEffect` value (flat list, no grouping); `TabClicked` / `CheckClicked` |
| Toolbar state | `UI/MainForm.cs` `UpdateEffects()` (~1073-1142) | One `enabled = look is not null && !IsExporting` gates the whole tabs control and every options row |
| Selected effect | `UI/MainForm.cs:117`, `SelectEffect` (~978) | `ImageEffect? _selectedEffect`, mirrored into `_effectTabs.Selected` |
| Options rows | `UI/MainForm.cs:75,182-187` | `Dictionary<ImageEffect, FlowLayoutPanel> _options`, one row shown at a time |
| Settings menu | `UI/MainForm.cs:15-16,918-936` | ⚙ button + `ContextMenuStrip`, one item today: *Start with Windows* |
| Persistence | `UI/StartupRegistration.cs` | Only *Start with Windows*, as a `HKCU\…\Run` registry value; no settings file, no settings class |
| Tests | — | No test project in the solution |

---

## Effect State

- A new immutable record `GridBorders`, owned by the **grid** (held next to the layout, passed to
  `Compositor.Draw` and to the export jobs), **not** by `ImageLook`:

  | Field | Default | Meaning |
  |---|---|---|
  | `Style` | `Corners` | `Corners`, `Solid`, `Dashed`, `Dotted`, `Double` |
  | `Thickness` | *see Open Questions* | Fraction of the grid's shorter side — resolution-independent |
  | `OuterFrame` | `false` | Also draw along the outer edge of the grid |
  | `Color` | the settings color | See *Settings Menu* |

- Inactive = no `GridBorders` (null).
- Kept through every cell event: image replaced, image deleted, cells swapped, layout changed.

## Effects Toolbar and Options Toolbar

- `EffectTabs` gets a **separator** then a **Grid** group label, followed by the **Borders** tab,
  with its own icon in `EffectIcons`.
- The Borders tab is **enabled even with no cell selected** (disabled only while exporting); the
  cell effects keep their current gating.
- Clicks follow the RULES.md table (inactive → activate with defaults + select; active → select;
  active and selected → deactivate).
- The selected effect stays **Borders** whatever cell gets selected or deselected — its state does
  not depend on the cell.
- Options row of Borders, in this order:
  1. **Style** — five choices, *Corners* first.
  2. **Thickness** slider with its value label (same layout as the blur's intensity).
  3. **Outer frame** checkbox.

## Settings Menu

- New item in the ⚙ menu: **Border color**, with a **color swatch** painted in the current color.
- Default color, until the user picks one: **hotpink** (HTML `HotPink`, `#FF69B4` —
  `Color.HotPink`).
- A click opens the standard `ColorDialog`, preselected on the current color. **OK** → new color,
  applied at once to the grid and **persisted**; **Cancel** → nothing changes.
- Persisted per user in the registry (`HKCU\Software\ImageGridFusion`), following the
  *Start with Windows* precedent — no settings file.

## Rendering

- A **border pass** at the end of `Compositor.Draw`, after the `DrawCell` loop, from the cells
  already computed: every output gets it from one place.
- The live-drag fast path (`GridPreview.cs:925`) redraws the borders over the cell it repaints.
- Borders are drawn **centred on the cell boundaries**; shared edges between neighbours are drawn
  **once** (deduplicated), so dash patterns stay regular on irregular layouts.
- Outer frame: drawn **inside** the canvas along its edge, with the same visible thickness as an
  inner border.
- Styles:

  | Style | Drawing |
  |---|---|
  | Corners | At each corner, an L-bracket; each arm covers 10 % of the edge it lies on |
  | Solid | Continuous line |
  | Dashed | GDI+ dash pattern, scaled with the thickness |
  | Dotted | Round dots, spacing scaled with the thickness |
  | Double | Two parallel lines, each ⅓ of the thickness, ⅓ gap between them |

## Rules Amended

Proposal, recorded in RULES.md with the README step:

- The effects rules gain a **Global effects** sub-section: a global effect belongs to the grid,
  its toggle lives in the *Grid* group, it stays enabled with no cell selected, cell events never
  reset it, and it is rendered in `Compositor.Draw` (not `DrawCell`).
- GLOSSARY: *Effect* lists Borders; new entries *Global effect* and *Corners style*.

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

- [ ] **Corners geometry** — brackets at the corners of **every cell** (inner intersections then
  form a cross), or at the **four corners of the grid** only? Arm length: 10 % of the edge it lies
  on (unequal arms on a non-square cell), or 10 % of the cell's shorter side (equal arms)?
- [ ] **Color model** — is the settings color **the** border color (no color control in the
  options row), or only the default the options row can override for the session?
- [ ] **Start-up state and persistence** — are the borders active when the app starts? Are the
  style, thickness and outer frame remembered too, or only the color?
- [ ] **Overlay or gap** — do the borders cover the edges of the images (cells unchanged), or
  shrink the cells to leave room for them?
- [ ] **Thickness** — range and default?
- [x] ~~**Default color** — before the user picks one?~~ → hotpink (`#FF69B4`)
- [ ] **Effects *Reset* button** — does it leave the borders alone (it concerns the selected
  cell's effects)?
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
| 1 | Where does the Borders setting go, being global to the grid? | A *Grid* group in the effects toolbar | 2026-09-26 |
| 2 | Which borders are concerned? | Between cells, plus an optional outer frame | 2026-09-26 |
| 3 | Which line styles? | Solid, Dashed, Dotted, Double (+ Corners, from the follow-up message) | 2026-09-26 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — single scout pass | 2026-09-26 |
| 5 | Corners: every cell or grid corners only? Arm length basis? | | 2026-09-26 |
| 6 | Settings color: the border color, or a default the options override? | | 2026-09-26 |
| 7 | Active at start-up? What is remembered besides the color? | | 2026-09-26 |
| 8 | Borders over the images, or cells shrunk to make room? | | 2026-09-26 |
| 9 | Thickness range and default? | | 2026-09-26 |
| 10 | Default color? | Hotpink (HTML), `#FF69B4` | 2026-09-26 |
| 11 | Does the effects *Reset* button leave the borders alone? | | 2026-09-26 |
| 12 | Unit tests: stay test-free? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
