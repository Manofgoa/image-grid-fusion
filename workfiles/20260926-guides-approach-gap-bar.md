# Guides Approach and Gap Bar

> Working document — show the magnetic guides earlier, while the image approaches a stop, with a
> bar at 90° to the guide measuring the gap left before the stop.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a magnetic guide shows only once the image is **held** on its stop
(`workfiles/20260925-pan-beyond-cell-bounds.md`, *Magnetic Guides*): the user gets no warning that a
stop is coming, and no sense of how far it is.

This task makes every guide appear **before** the stop is reached, as soon as the image comes within
an **approach distance** of it, and draws a **gap bar** — a segment perpendicular to the guide,
linking the image to the guide — whose length is the gap still to cover. The bar shrinks as the image
gets closer and disappears when the stop holds it; the guide then stays as it does today.

Components:

- `UI/PanMagnet.cs` — one instance per axis, drives the stops; it knows the stops and the position,
  and is where the approach state is computed.
- `UI/GridPreview.cs` — `PanBy` feeds the magnets, for the drag and the arrow keys alike, and
  invalidates the cell when a guide appears or goes; `PaintPanGuides` draws the guides of the pressed
  cell during a drag, of the selected cell after an arrow key (`KeyPanned`), through
  `PaintGuideLines` / `GuidePen(opacity)` (dashed green line over a dark halo fading with it, clipped
  to the cell).
- `RULES.md` — *On-Cell Helper Indicators* gets the rule, since it applies to every guide.

---

## Guides Concerned

- **Every guide** (scoping answer): the rule is general to the helper indicators, not specific to one
  guide.
- Today the guides are the **magnetic stop guides of the pan**, on both axes:

  | Stop | Guide |
  |---|---|
  | Edge stop | Dashed line along the cell edge the image would leave the cell by |
  | Center stop, horizontal axis | Dashed vertical line through the cell center |
  | Center stop, vertical axis | Dashed horizontal line through the cell center |

- **Not concerned**: the blur bars. They are **handles**, drawn at all times while the Blur tab is
  selected and the effect is on — there is nothing to show earlier. Their snap onto the cell edge
  (6 logical px) is unchanged.
- **Shift held** (free pan): still no stop, no guide, and no gap bar.

---

## Approach Distance

- A guide shows as soon as the image is within the **approach distance** of its stop, measured in a
  **fraction of the cell** (scoping answer) on the axis concerned: of the cell's **width** for the
  horizontal axis, of its **height** for the vertical axis. It thus survives resizing and layout
  changes, like the effects' geometry.
- Value: **10 %** of the cell by default (Q&A #5), the order of the pan's 10 % margin — an **app
  setting** of the ⚙ menu (Iteration 9): **Guides show from**, a submenu of choices, **At the stop
  only** (0, the guides as before this task, held only), 5 %, **10 %**, 15 %, 20 %, 25 %. Applied at
  once, remembered between sessions in `settings.json` (`GuideApproach`, in percent; an unknown value
  falls back to 10 %), out of the undo history like every app setting.
- **Position only** (Q&A #6): the guide shows while the stop is within reach, whatever the direction
  of the drag — moving away from it inside the reach keeps it shown, so micro-moves never make it
  flicker.
- Gap measured, in device px, between:

  | Stop | From | To |
  |---|---|---|
  | Edge stop | The image's edge concerned | The cell edge |
  | Center stop | The image's center line | The cell's center line |

  Both come from what `PanMagnet` already receives (`position`, `low`, `high`, `center`), on the
  turned image's bounding box, as today.
- **One stop per axis**: when two stops are within reach on an axis (an image larger than 80 % of the
  cell can be near both an edge and the center), the **nearest** one shows.
- An edge stop counts **from inside the stops** only, where reaching it outward holds the image: an
  image pushed beyond its stops shows no approach. An axis whose stops **coincide** (an image exactly
  filling it) shows none either — the image has no room to come near anything, and its guide would
  otherwise stay on for the whole drag. One computation: `PanMagnet.Approach` (Iteration 7).
- The **edge guide's side** follows today's rule: the side an image covering the cell uncovers, the
  side a smaller image crosses.
- The approach is shown **while the image is moved** — during a drag, everything going when the
  mouse is released, and after an **arrow key**, as long as its guides show today (`KeyPanned`):
  the arrow keys get the approach and the gap bar too (Q&A #12).
- A drag shows it only **once the image has moved** (`GridPreview._panMoved`): a mere click on a
  centered image does not flash its center guides. `Shift` is read from the last move
  (`GridPreview._panFree`), so a free move shows nothing (Iteration 7).

### Progressive Opacity

- While approaching, the guide's **opacity grows with the closeness** (Q&A #7): faint at the edge of
  the reach, full once the stop holds the image. The held guide is drawn exactly as today.
- The halo fades with the line, so a faint guide does not leave a dark dashed trace.
- Curve: **linear** in the gap, from a minimum opacity at the approach distance to 100 % at the stop.
  Minimum: **20 %** (Q&A #10), so the guide is visible as soon as it appears yet clearly fainter
  than the held one.

---

## Gap Bar

- A **segment perpendicular to the guide** (scoping answer), starting on the guide. Its **length is
  the gap left**: it shrinks as the image gets closer.
  - Center stop: toward the image's center line, which it reaches.
  - Edge stop: **into the cell** — an image covering the cell has its edge outside the cell, where the
    bar would be clipped out of sight; drawn inward, the measure stays visible (Iteration 7).
- It **disappears when the stop holds** the image (gap 0): only the guide stays, as today.
- While the image is held and the drag goes on toward the resistance, no bar is drawn (the image
  does not move).
- Drawing: a **helper indicator** (RULES.md) — fluorescent green `HelperColor` over the black
  `HelperHalo`, in `GridPreview.OnPaint` only, never in `Compositor`; **solid** (the guide is the
  dashed one), 2 px over a 4 px halo, with a short tick across each end (5 logical px each way,
  `GapBarTick`) so a short gap still reads as a measure (`GridPreview.PaintGapBars`).
- Position along the guide: through the **image's center** (Iteration 8), during a drag and after an
  arrow key alike — at its height for a vertical guide, at its abscissa for a horizontal one — kept
  inside the cell (the bar and its ticks fully visible). The mouse plays no part: the bar follows the
  image it measures.
- The **position readout** stays shown alongside (Q&A #13): it tells the offset from the cell's center,
  the bar the gap left to a stop — the solid bar, perpendicular to its guide, stays distinct from the
  readout's dashed line at 50 %.
- The bar stays at **full opacity** (Q&A #11): it is the measure, always legible; its length tells
  the closeness, the guide's opacity only echoes it.

---

## Rules and Documentation

- `RULES.md`, *On-Cell Helper Indicators › Guides Approach*: a guide shows **from an approach
  distance** in fractions of the cell, with a **gap bar** at 90° while the stop is not reached.
- `README.md` and `README.fr.md`, in the same commit (`../CLAUDE.md` § French Versions): the pan's
  guides paragraph mentions the early display and the gap bar.

---

## Test Impact

**No unit tests** (Q&A #9): the repository has no test project and none is created for this task.
The behaviour is validated by hand in the app — reach at 10 %, nearest stop, opacity ramp, gap bar
through the image's center, disappearing on hold, Shift, fine
angle, the position readout alongside.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none, by decision (Q&A #9) | — | — |

---

## Open Questions

- [x] ~~Approach distance: which fraction of the cell?~~ → 10 %
- [x] ~~Is the approach shown by **position only** (the stop within reach, whatever the drag's
      direction), or only when the image **moves toward** the stop?~~ → Position only
- [x] ~~While approaching, is the guide drawn **like the held guide**, or **distinguished** (e.g. dimmer)
      so the moment the stop holds stays visible?~~ → Opacity growing progressively with the closeness
- [x] ~~Where along the guide is the gap bar drawn: at the **mouse position**, or in the **middle of the
      cell**?~~ → At the mouse *(revised 2026-10-13, see Iteration 8: through the image's center)*
- [x] ~~Unit tests: with no test project in the repository, create one for the approach logic of
      `PanMagnet`, or no unit tests?~~ → No unit tests
- [x] ~~Progressive opacity: which **minimum** opacity at the edge of the reach?~~ → 20 %
- [x] ~~Does the **gap bar** fade with the guide, or stay at full opacity?~~ → Full opacity
- [x] ~~The **arrow keys** now move the image with the same stops, their guides drawn on the selected
      cell (`GridPreview.KeyPanned`): do they get the approach and the gap bar too — and where does
      the bar go, with no mouse to follow?~~ → Yes, the bar through the image's center *(revised
      2026-10-13, see Iteration 8: the drag too)*
- [x] ~~The **position readout** (cross on the image's center, dashed line from the cell's center)
      shows during the move: near the center stop, how do the gap bar and it share the cell?~~ → Both
      shown

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

- Request: show the guides earlier, with a bar at 90° showing the gap left.
- Scoping (Q&A #1–#4): every guide; approach distance in fractions of the cell; the bar is a
  segment perpendicular to the guide; straightforward exploration, single pass.
- Exploration: the only guides are the pan's magnetic stop guides (`PanMagnet`,
  `GridPreview.PaintPanGuides`); the blur bars are handles always shown, left out.
- Proposed: per-axis approach state in `PanMagnet`, nearest stop per axis, solid gap bar with end
  ticks, disappearing on hold; rule added to RULES.md.

### Iteration 2 — 2026-09-26

- Answers (Q&A #5–#8): approach distance 10 % of the cell; approach by position only; gap bar at
  the mouse, kept inside the cell.
- Request (Q&A #7): instead of choosing between a guide drawn like the held one and a dimmer one,
  make its **opacity grow progressively** as the image gets closer. Added *Progressive Opacity*:
  linear in the gap, halo fading with the line, full opacity once held.
- New open questions: the minimum opacity, and whether the gap bar fades too.

### Iteration 3 — 2026-09-26

- Answers (Q&A #9–#11): no unit tests; minimum opacity 20 %; the gap bar stays at full opacity.
- No open question left: the design is ready for the go.

### Iteration 4 — 2026-10-10

- The go question was dismissed on 2026-09-26; no code was written.
- Re-read against the current code: `PaintGuideLines` / `GuidePen(opacity)` now draw guides at any
  opacity, the halo fading with them — the progressive opacity reuses them. Two premises changed and
  reopen the design: the arrow keys now move the image through the stops (`PanMagnet.Move`
  `stepwise`, guides on the selected cell), and the position readout shows during the move.

### Iteration 5 — 2026-10-10

- Answers (Q&A #12–#13): the arrow keys get the approach and the gap bar, the bar through the image's
  center; the gap bar and the position readout are both shown.
- The README's French version is updated with the English one (rule added since Iteration 1).
- No open question left: the design is ready for the go.

### Iteration 6 — 2026-10-13 — ✅ Implemented

- Go given: code, tests and documentation (no unit tests, Q&A #9). Re-checked against the code first:
  no commit since Iteration 5 touches the guides.
- Branch: stays on `main`, the standing choice for this repository.

### Iteration 7 — 2026-10-13 — 🧭 Implementation choices

- No rule broken.
- **Edge gap bar drawn into the cell**, from the guide: for an image covering the cell, the edge
  concerned lies outside the cell, and a bar running to it would be clipped out of sight.
- **No approach outside the stops** (an image pushed beyond them, which leaves inward freely) and **none
  on an axis whose stops coincide** (an image exactly filling it, whose guide would stay on for the
  whole drag).
- **A drag shows the approach once the image has moved**, not at the press: a click selecting a
  centered image would otherwise flash its center guides.
- **Shift** is read from the last move (`_panFree`), not from the keyboard at paint time.
- With the arrow keys, the approach lasts as the held guides already did (`KeyPanned`): until another
  gesture, cell or tab, or a change of the image's look.
- Sizes: gap bar 2 px over a 4 px halo, ticks 5 logical px each way, kept inside the cell.
- RULES.md gets a *Guides Approach* subsection under *On-Cell Helper Indicators*.

### Iteration 8 — 2026-10-13 — ⚙️ Post-implementation — Gap bar through the image

- User request, after testing: the bars must be placed **relative to the image**, not to the mouse
  position.
- Changed: the gap bar crosses its guide through the **image's center** during a drag too, as it
  already did after an arrow key; the mouse no longer places it. Revises Q&A #8.
- RULES.md and both READMEs follow.

### Iteration 9 — 2026-10-13 — ⚙️ Post-implementation — Approach distance setting

- User request, after testing: make the bars' distance configurable in a settings menu, persisted.
- Changed: the approach distance becomes an **app setting** of the ⚙ menu, **Guides show from**,
  following the pattern of *File explorer pages per load*: a submenu of ticked choices — At the stop
  only (0), 5 %, 10 % (the default), 15 %, 20 %, 25 % — applied at once, saved in `settings.json`
  (`GuideApproach`), an unknown value falling back to 10 %.
- Choices taken without asking (autonomous run): the submenu rather than a numeric field, as the
  other numeric settings of the menu; its steps; the *At the stop only* choice, bringing back the
  guides as they were before this task; its place, after *Twitter corners by default* (the other
  setting of the grid).
- RULES.md and both READMEs follow.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7, 8 | 2026-10-13 | `6d13a1c` gap bar through the image's center (Iteration 8); `9963110` — `PanMagnet.Approach`, `GridPreview.PaintPanGuides` / `PaintGapBars`; builds with 0 warnings |
| Unit tests | 3 | 2026-09-26 | Not applicable — no test project, declined (Q&A #9) |
| README | 7, 8 | 2026-10-13 | `77b004e` (Iteration 8); `18570a1` — `README.md` and `README.fr.md`, the guides paragraph |
| RULES.md | 7, 8 | 2026-10-13 | `f519d1c` (Iteration 8); `96463ac` — *On-Cell Helper Indicators › Guides Approach* |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which guides get the early display and the gap bar? | Every guide | 2026-09-25 |
| 2 | What does "earlier" mean? | A fraction of the cell | 2026-09-25 |
| 3 | What does the "bar at 90°" look like? | A segment perpendicular to the guide, its length the gap left, shrinking to nothing when the stop holds | 2026-09-25 |
| 4 | Is the exploration straightforward or tricky? | Straightforward | 2026-09-25 |
| 5 | Approach distance: which fraction of the cell? | 10 % | 2026-09-26 |
| 6 | Approach by position only, or only when moving toward the stop? | Position only | 2026-09-26 |
| 7 | Approaching guide drawn like the held one, or distinguished? | "Can the opacity increase progressively during the approach?" — yes, adopted | 2026-09-26 |
| 8 | Gap bar at the mouse position or in the middle of the cell? | At the mouse | 2026-09-26 |
| 9 | Unit tests: create a test project for `PanMagnet`, or none? | No unit tests | 2026-09-26 |
| 10 | Progressive opacity: which minimum opacity at the edge of the reach? | 20 % | 2026-09-26 |
| 11 | Does the gap bar fade with the guide, or stay at full opacity? | Full opacity | 2026-09-26 |
| 12 | Arrow keys: approach and gap bar too, and where does the bar go without a mouse? | Yes, the bar through the image's center | 2026-10-10 |
| 13 | Gap bar and position readout near the center stop: how do they share the cell? | Both shown | 2026-10-10 |

---

*Last updated: 2026-10-13*
