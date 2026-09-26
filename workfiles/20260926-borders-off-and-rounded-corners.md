# Borders Off by Default, and Rounded Corners

> Working document — the Borders stop being on at start-up; a new global effect rounds the grid's
> outer corners, the way Twitter / X shows posted images.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two changes to the global effects (origin of both: `workfiles/20260926-cell-borders.md`):

1. **Borders off by default.** The Borders global effect no longer starts on: the toggle starts
   unchecked, and *Clear all* turns it back off. Its default settings do not change — turned on,
   it shows the **Corners** style.
2. **Rounded corners.** Twitter / X displays a posted image with **rounded corners**, which cut
   into the Corners style's brackets (screenshot given by the user: the black L-brackets have their
   angle clipped by Twitter's rounding). A new **global effect** rounds the grid's **four outer
   corners** in the preview and every export, so what the app shows is what Twitter shows, and the
   Borders follow the curve instead of being cut by it.

Components touched: `Composition/GridBorders.cs`, a new `Composition/` record for the rounding,
`Composition/Compositor.cs`, `Imaging/GridExport.cs`, `UI/GridPreview.cs`, `UI/MainForm.cs`,
`README.md`, `RULES.md`, `GLOSSARY.md`.

---

## Borders Off by Default

- `MainForm._bordersOn` starts `false`; `ClearAll` sets it back to `false`.
- `GridBorders.Initial` is unchanged: Corners style, default thickness, no outer frame, the ⚙ color.
  Turning the toggle on the first time shows exactly that.
- *Initial state* of the Borders becomes **off + `GridBorders.Initial`**: `BordersInitial` checks
  `!_bordersOn` instead of `_bordersOn`, so *Clear all* is enabled as soon as the Borders are on or
  their settings changed, and its status message stays "Borders back to their initial state."
- Everything else is kept: the toggle keeps the settings while off, the color stays an app setting
  remembered in the registry, the row is locked while exporting.
- Documentation: README § Borders ("On at start-up" → off at start-up), the README feature list,
  the glossary entry *Borders* ("on at start-up" removed).

---

## Rounded Corners (Global Effect)

### Behaviour

- A **global effect**, per RULES.md § Global Effects: a toggle in the **Global effects** row, after
  the Borders, its options beside it only while it is on, the row keeping one height.
- It rounds the **grid's four outer corners only** — not each cell, not the gap between the cells.
- Its state: a **radius**, the one setting, as a fraction of the canvas (see Open Questions for the
  unit and the default). Off keeps the radius; on applies it as it was.
- Not persisted; *Clear all* brings it back to its initial state; swap, layout change, image
  replaced and the cells' *Reset* buttons leave it alone; locked while exporting.
- Hit-testing is unchanged: a rounded-off corner still belongs to its cell.

### Rendering

- At the grid level, in `Compositor.Draw`, **after** the cells and the Borders: everything outside
  the rounded rectangle of the canvas becomes **transparent**, with an **anti-aliased** edge.
- Outputs with alpha (PNG, the clipboard's PNG) keep the transparent corners; outputs without alpha
  flatten them like any transparency — see Open Questions for the color.
- Resolution-independent: the radius is a fraction of the canvas, so the preview and every export
  size look the same.

### Borders Following the Curve

- **Corners style**: each bracket follows the rounded corner — its outer edge on the curve, its
  inner edge a concentric curve of radius `radius − width` (square when the width exceeds the
  radius) — then runs straight along each edge to the end of its arm. An arm is at least as long as
  the radius, so the curve always fits in the bracket.
- **Outer frame** (gap styles): drawn along the rounded rectangle, same principle.
- Gap styles without an outer frame: nothing to follow; the corner cells are simply rounded by the
  mask.
- `GridBorders.Draw` / `DrawOver` therefore take the corner radius (or `null` for square corners).

### Preview

- The cached grid is drawn by `Compositor.Draw`, so it is rounded like the exports.
- A cell drawn again into the cache (`RedrawCell`, `RedrawCells`, video playback, live drag) has
  the rounding **re-applied within that cell**, like `DrawBordersOver` repaints the brackets —
  otherwise a corner cell would lose its rounding at the first video frame.
- The **checkerboard** under the grid is clipped to the same rounded rectangle, so the rounded-off
  corners show the preview's background, as on Twitter's page.
- The rounding is not a helper indicator: it is part of the image, never green.

---

## Test Impact

To confirm (see Open Questions): the solution has no test project, and every previous workfile
stayed test-free by the user's decision (`workfiles/20260926-cell-borders.md`, Q&A #20).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests, pending confirmation) | — | — |

---

## Open Questions

- [x] ~~What happens when the Borders are turned on?~~ → Off at start-up, Corners style when turned
  on, *Clear all* turns them off (Q&A #1)
- [x] ~~How to handle Twitter's rounding?~~ → A new global effect rounding the grid's corners,
  the Borders following the curve (Q&A #2)
- [x] ~~Round what?~~ → The grid's four outer corners only (Q&A #3)
- [ ] Radius: unit, default and range? Proposal: a fraction of the canvas's **shorter side**, like
  the Borders' thickness, default **4 %**, slider **1–15 %**, shown as a percentage.
- [ ] Outputs without alpha (JPEG for sharing — the one pasted into Twitter —, MP4, GIF, the
  clipboard's bitmap): what color do the rounded-off corners get? White, like today's flattening,
  shows as white slivers in Twitter's dark mode wherever our radius exceeds Twitter's.
- [ ] The effect at start-up: off (like the Soundtrack and now the Borders) or on?
- [ ] Name and toggle label: *Rounded corners* ("◜ Rounded corners"), distinct from the Borders'
  *Corners style*?
- [ ] Unit tests: none again, as in every previous workfile?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the user's request ("Borders not on by default after all; Twitter rounds the
images, to discuss") and the scoping batch (Q&A #1–#4): Borders off at start-up with unchanged
default settings; a new *Rounded corners* global effect rounding the grid's outer corners in the
preview and every export, the Corners brackets and the outer frame following the curve. Explored
directly (depth: straightforward): `GridBorders`, `Compositor.Draw`, `GridExport`, the preview's
cell redraws and checkerboard, `MainForm`'s Borders toggle and *Clear all*.

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
| 1 | Borders off at start-up: what happens when they are turned on? | Off at start-up, Corners style when turned on (recommended); *Clear all* turns them off | 2026-09-26 |
| 2 | What to do about Twitter's rounding? (global effect / option of the Borders / preview-only indicator / brackets moved inward) | A new *Rounded corners* global effect, in the preview and the exports, the Borders following the curve | 2026-09-26 |
| 3 | Round where? (grid's outer corners / every cell) | The grid's outer corners only (recommended) | 2026-09-26 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-26 |
| 5 | Radius: unit, default and range? | | 2026-09-26 |
| 6 | Outputs without alpha: what do the rounded-off corners get? | | 2026-09-26 |
| 7 | Name, label and state at start-up of the new effect? | | 2026-09-26 |
| 8 | Unit tests: none again? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
