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
  the Borders. It has **no options**: the toggle alone, so the row's height does not change.
- It rounds the **grid's four outer corners only** — not each cell, not the gap between the cells.
- **Radius: Twitter's own**, fixed, no slider. Measured on the user's screenshot: a radius of
  ~24 px on an image displayed 810 px wide — Twitter's 16 CSS px on a ~540 px wide display at
  150 % — that is **3 % of the displayed width**. Twitter shows a landscape grid by its width and a
  portrait one by its height, so the radius is **3 % of the grid's longer side**
  (`RoundedCorners.RadiusShare = 0.03`).
- Its state is on / off only. Not persisted; *Clear all* brings it back to its **start-up state**
  (see Start-Up Setting); swap, layout change, image replaced and the cells' *Reset* buttons leave
  it alone; locked while exporting.
- Hit-testing is unchanged: a rounded-off corner still belongs to its cell.

### Start-Up Setting

- A setting in the **⚙ menu** chooses whether the effect is **on or off at start-up**, remembered
  between sessions in the registry, like the Border color (`UI/AppSettings.cs`) — an app setting
  read by a global effect, per RULES.md § Global Effects. See Open Questions for its scope, its
  value at first launch and when it applies.

### Rendering

- At the grid level, in `Compositor.Draw`, **after** the cells and the Borders.
- **Outputs with alpha** (PNG, the clipboard's PNG): everything outside the rounded rectangle of
  the canvas becomes **transparent**, with an **anti-aliased** edge.
- **Outputs without alpha** (JPEG for sharing, MP4, GIF, the clipboard's bitmap): the corners are
  **not cut** — they keep the image's pixels, and Twitter rounds them itself, so no white or black
  sliver ever shows in its light or dark mode. The Borders still **follow the curve** there.
- So `Compositor.Draw` takes whether the output keeps alpha: the mask is applied only when it does.
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

None, by the user's decision (Q&A #8): the solution has no test project, and every previous
workfile stayed test-free.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

- [x] ~~What happens when the Borders are turned on?~~ → Off at start-up, Corners style when turned
  on, *Clear all* turns them off (Q&A #1)
- [x] ~~How to handle Twitter's rounding?~~ → A new global effect rounding the grid's corners,
  the Borders following the curve (Q&A #2)
- [x] ~~Round what?~~ → The grid's four outer corners only (Q&A #3)
- [x] ~~Radius: unit, default and range?~~ → Twitter's own, computed by the agent: 3 % of the
  grid's longer side, fixed, no slider (Q&A #5)
- [x] ~~Outputs without alpha: what do the rounded-off corners get?~~ → Not cut, Twitter rounds
  them; the Borders still follow the curve (Q&A #6)
- [x] ~~The effect at start-up: off or on?~~ → A ⚙ setting switches it (Q&A #7)
- [x] ~~Unit tests?~~ → None, as before (Q&A #8)
- [ ] Name and toggle label: *Rounded corners* ("◜ Rounded corners"), distinct from the Borders'
  *Corners style*? (left unanswered in Q&A #7)
- [ ] Start-up setting: for the Rounded corners only, or for the Borders too?
- [ ] Start-up setting: its value at first launch, before it is ever changed — on or off?
- [ ] Start-up setting: changed from the ⚙ menu, does it also switch the effect right away, or only
  at the next start-up and *Clear all*?

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

### Iteration 2 — 2026-09-26

Answers to Q&A #5–#8:
- **Radius**: "must be like Twitter's rounding, up to you to compute it" — measured on the user's
  screenshot (~24 px on 810 px, i.e. 16 CSS px on ~540 px): fixed at 3 % of the grid's longer side;
  the effect loses its slider and has no options at all.
- **Outputs without alpha**: corners not cut, Twitter rounds them; the Borders follow the curve in
  every output; `Compositor.Draw` masks only outputs with alpha.
- **Start-up state**: instead of choosing, "add a setting to switch the default value" — a new ⚙
  setting, remembered in the registry. Its scope, first-launch value and timing become Open
  Questions; the name, left unanswered, stays open.
- **Unit tests**: none.

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
| 5 | Radius: unit, default and range? | "Must be like Twitter's rounding, up to you to compute it" → 3 % of the longer side, measured on the screenshot | 2026-09-26 |
| 6 | Outputs without alpha: what do the rounded-off corners get? | Not cut, the Borders follow the curve (recommended) | 2026-09-26 |
| 7 | Name, label and state at start-up of the new effect? | "Add a setting to switch the default value" — name not answered | 2026-09-26 |
| 8 | Unit tests: none again? | None, as before (recommended) | 2026-09-26 |
| 9 | Name and toggle label of the new effect? | | 2026-09-26 |
| 10 | Start-up setting: Rounded corners only, or the Borders too? | | 2026-09-26 |
| 11 | Start-up setting: value at first launch? | | 2026-09-26 |
| 12 | Start-up setting: applies right away, or at the next start-up and *Clear all* only? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
