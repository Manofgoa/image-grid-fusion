# Borders Off by Default, and Twitter's Rounded Corners

> Working document — the Borders stop being on at start-up, and gain an option rounding the grid's
> outer corners the way Twitter / X shows posted images.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two changes to the Borders global effect (origin: `workfiles/20260926-cell-borders.md`):

1. **Borders off by default.** The Borders no longer start on: the toggle starts unchecked, and
   *Clear all* turns it back off. Its default settings do not change — turned on, it shows the
   **Corners** style.
2. **Twitter's rounded corners, a Borders option.** Twitter / X displays a posted image with
   **rounded corners**, which cut into the Corners style's brackets (screenshot given by the user:
   the black L-brackets have their angle clipped by Twitter's rounding). A **checkbox** among the
   Borders' options, for **every style**, **checked by default**, rounds the grid's **four outer
   corners** in the preview and the exports, the brackets and the outer frame following the curve —
   so what the app shows is what Twitter shows.

Components touched: `Composition/GridBorders.cs`, `Composition/Compositor.cs`,
`Imaging/GridExport.cs`, `UI/GridPreview.cs`, `UI/MainForm.cs`, `README.md`, `GLOSSARY.md`.

---

## Borders Off by Default

- `MainForm._bordersOn` starts `false`; `ClearAll` sets it back to `false`.
- `GridBorders.Initial` keeps its settings — Corners style, default thickness, no outer frame, the
  ⚙ color — and gains the rounding, **checked** (see below). Turning the toggle on the first time
  shows exactly that.
- *Initial state* of the Borders becomes **off + `GridBorders.Initial`**: `BordersInitial` checks
  `!_bordersOn` instead of `_bordersOn`, so *Clear all* is enabled as soon as the Borders are on or
  their settings changed, and its status message stays "Borders back to their initial state."
- Everything else is kept: the toggle keeps the settings while off, the color stays an app setting
  remembered in the registry, the row is locked while exporting.
- Documentation: README § Borders ("On at start-up" → off at start-up), the README feature list,
  the glossary entry *Borders* ("on at start-up" removed).

---

## Rounded Corners (Borders Option)

### Behaviour

- A **checkbox among the Borders' options** in the Global effects row, next to *Outer frame*,
  enabled for **every style** (Corners included), shown only while the Borders are on, like the
  other options. Label **Twitter corners**, tooltip "Rounds the grid's corners like Twitter / X
  shows images; the borders follow the curve".
- A setting of the Borders: `GridBorders.Rounded` (`bool`); kept while the Borders are off, put back
  to its default by *Clear all* with the other settings; not persisted itself.
- **Its default** comes from the ⚙ setting below: `GridBorders.Initial(color, rounded)`.
- It rounds the **grid's four outer corners only** — not each cell, not the gap between the cells.
- **Radius: Twitter's own**, fixed, no slider. Measured on the user's screenshot: a radius of
  ~24 px on an image displayed 810 px wide — Twitter's 16 CSS px on a ~540 px wide display at
  150 % — that is **3 % of the displayed width**. Twitter shows a landscape grid by its width and a
  portrait one by its height, so the radius is **3 % of the grid's longer side**
  (`GridBorders.CornerRadiusShare = 0.03`).
- **Borders off: square corners** — the rounding is a Borders option, so the grid, which now
  starts with the Borders off, starts square.
- Hit-testing is unchanged: a rounded-off corner still belongs to its cell.

### Default Setting (⚙ Menu)

- A checkable item of the **⚙ menu**, **Twitter corners by default**, sets whether the checkbox is
  checked in the Borders' initial state. It concerns this option only.
- Remembered between sessions per user in `HKCU\Software\ImageGridFusion`, like the Border color
  (`UI/AppSettings.cs`) — an app setting read by a global effect, per RULES.md § Global Effects.
  **Checked** until it is ever changed.
- It sets the **default** only: read at start-up and by *Clear all*. Changing it does **not**
  touch the checkbox of the open grid.

### Rendering

- At the grid level, in `Compositor.Draw`, **after** the cells and the Borders' lines.
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
  the Borders following the curve (Q&A #2) *(revised 2026-09-26, see Iteration 4)*
- [x] ~~Round what?~~ → The grid's four outer corners only (Q&A #3)
- [x] ~~Radius: unit, default and range?~~ → Twitter's own, computed by the agent: 3 % of the
  grid's longer side, fixed, no slider (Q&A #5)
- [x] ~~Outputs without alpha: what do the rounded-off corners get?~~ → Not cut, Twitter rounds
  them; the Borders still follow the curve (Q&A #6)
- [x] ~~The effect at start-up: off or on?~~ → A ⚙ setting switches it (Q&A #7) *(revised 2026-09-26, see Iteration 4)*
- [x] ~~Unit tests?~~ → None, as before (Q&A #8)
- [x] ~~Name and toggle label?~~ → *Rounded corners*, "◜ Rounded corners" (Q&A #9) *(revised 2026-09-26, see Iteration 4)*
- [x] ~~Start-up setting: for the Rounded corners only, or for the Borders too?~~ → This effect
  only (Q&A #10) *(revised 2026-09-26, see Iteration 4)*
- [x] ~~Start-up setting: its value at first launch?~~ → On (Q&A #11) *(revised 2026-09-26, see Iteration 4)*
- [x] ~~Start-up setting: applies right away?~~ → No: at the next start-up and *Clear all* only
  (Q&A #12) *(revised 2026-09-26, see Iteration 4)*
- [x] ~~The ⚙ start-up setting: dropped, or kept as the checkbox's default?~~ → Kept: "Twitter
  corners by default", checked until changed, read at start-up and by *Clear all* (Q&A #13)
- [x] ~~Borders off: square corners, or still rounded?~~ → Square (Q&A #14)
- [x] ~~The checkbox's label?~~ → "Twitter corners" (Q&A #15)

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

### Iteration 3 — 2026-09-26

Answers to Q&A #9–#12: the effect is named *Rounded corners* ("◜ Rounded corners"); the ⚙ item
**Rounded corners at start-up** concerns this effect only, is **on** until changed, and sets the
start-up state only (start-up and *Clear all*), never switching the open grid. No Open Question
left.

Documentation planned with the code: README (§ Borders off at start-up, § Rounded corners, the ⚙
menu, the feature list), GLOSSARY (*Borders* no longer "on at start-up", new *Rounded corners*
entry, *Global effect* list).

### Iteration 4 — 2026-09-26

The user, mid-design: "Twitter's rounded corners must in fact be an option set for every type of
border (checkbox), checked by default." The rounding stops being a global effect of its own (Q&A
#2, Iterations 2–3): it becomes a **checkbox among the Borders' options**, enabled for every style,
checked in `GridBorders.Initial`. What Iterations 2–3 settled stays: Twitter's radius (3 % of the
longer side), corners not cut in outputs without alpha, the Borders following the curve, the
preview re-applying the rounding to redrawn cells. The *Rounded corners* toggle, its name and its ⚙
start-up setting go with the global effect; whether the setting survives as the checkbox's
default, what happens with the Borders off, and the label become Open Questions.

### Iteration 5 — 2026-09-26

Answers to Q&A #13–#15: the ⚙ setting survives as **Twitter corners by default** — it sets the
checkbox's default (checked until changed), read at start-up and by *Clear all*, never touching the
open grid (Q&A #12 carried over); with the Borders off the grid is square; the checkbox is labelled
**Twitter corners**. No Open Question left.

Documentation planned with the code: README (§ Borders: off at start-up, the Twitter corners
option, the ⚙ item; the feature list), GLOSSARY (*Borders* no longer "on at start-up", a *Twitter
corners* entry).

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
| 9 | Name and toggle label of the new effect? | "◜ Rounded corners" (recommended) | 2026-09-26 |
| 10 | Start-up setting: Rounded corners only, or the Borders too? | "It belongs to this feature in particular" → Rounded corners only | 2026-09-26 |
| 11 | Start-up setting: value at first launch? | On | 2026-09-26 |
| 12 | Start-up setting: applies right away, or at the next start-up and *Clear all* only? | At the next start-up and *Clear all* (recommended) | 2026-09-26 |
| 13 | The ⚙ start-up setting: dropped, or kept as the checkbox's default? | Kept, it sets the checkbox's default | 2026-09-26 |
| 14 | Borders off: square corners, or still rounded? | Square (recommended) | 2026-09-26 |
| 15 | The checkbox's label? | "Twitter corners" (recommended) | 2026-09-26 |

---

*Last updated: 2026-09-26*
