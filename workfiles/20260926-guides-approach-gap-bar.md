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
- `UI/GridPreview.cs` — `PanBy` feeds the magnets and invalidates the cell when a guide appears or
  goes; `PaintPanGuides` draws the guides (dashed green line over a dark halo, clipped to the cell).
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
- Value: see *Open Questions*.
- Gap measured, in device px, between:

  | Stop | From | To |
  |---|---|---|
  | Edge stop | The image's edge concerned | The cell edge |
  | Center stop | The image's center line | The cell's center line |

  Both come from what `PanMagnet` already receives (`position`, `low`, `high`, `center`), on the
  turned image's bounding box, as today.
- **One stop per axis**: when two stops are within reach on an axis (an image larger than 80 % of the
  cell can be near both an edge and the center), the **nearest** one shows.
- The **edge guide's side** follows today's rule: the side an image covering the cell uncovers, the
  side a smaller image crosses.
- The approach is shown **during the drag only**, like the guides today; everything goes when the
  mouse is released.

---

## Gap Bar

- A **segment perpendicular to the guide** (scoping answer), from the image (its edge, or its center
  line for a center stop) to the guide. Its **length is the gap left**: it shrinks as the image gets
  closer.
- It **disappears when the stop holds** the image (gap 0): only the guide stays, as today.
- While the image is held and the drag goes on toward the resistance, no bar is drawn (the image
  does not move).
- Drawing: a **helper indicator** (RULES.md) — fluorescent green `HelperColor` over the black
  `HelperHalo`, in `GridPreview.OnPaint` only, never in `Compositor`; **solid** (the guide is the
  dashed one), with a short tick across each end so a short gap still reads as a measure.
- Position along the guide: see *Open Questions*.

---

## Rules and Documentation

- `RULES.md`, *On-Cell Helper Indicators*: a guide shows **from an approach distance** in fractions
  of the cell, with a **gap bar** at 90° while the stop is not reached.
- `README.md`: the pan's guides paragraph mentions the early display and the gap bar.

---

## Test Impact

The repository has **no test project** today. See *Open Questions*.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(pending the test question)* | | |

---

## Open Questions

- [ ] Approach distance: which fraction of the cell?
- [ ] Is the approach shown by **position only** (the stop within reach, whatever the drag's
      direction), or only when the image **moves toward** the stop?
- [ ] While approaching, is the guide drawn **like the held guide**, or **distinguished** (e.g. dimmer)
      so the moment the stop holds stays visible?
- [ ] Where along the guide is the gap bar drawn: at the **mouse position**, or in the **middle of the
      cell**?
- [ ] Unit tests: with no test project in the repository, create one for the approach logic of
      `PanMagnet`, or no unit tests?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| RULES.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which guides get the early display and the gap bar? | Every guide | 2026-09-25 |
| 2 | What does "earlier" mean? | A fraction of the cell | 2026-09-25 |
| 3 | What does the "bar at 90°" look like? | A segment perpendicular to the guide, its length the gap left, shrinking to nothing when the stop holds | 2026-09-25 |
| 4 | Is the exploration straightforward or tricky? | Straightforward | 2026-09-25 |
| 5 | Approach distance: which fraction of the cell? | | |
| 6 | Approach by position only, or only when moving toward the stop? | | |
| 7 | Approaching guide drawn like the held one, or distinguished? | | |
| 8 | Gap bar at the mouse position or in the middle of the cell? | | |
| 9 | Unit tests: create a test project for `PanMagnet`, or none? | | |

---

*Last updated: 2026-09-26*
