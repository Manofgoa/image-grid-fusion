# Ctrl + Wheel: 5 % Steps

> Working document — holding Ctrl while turning the mouse wheel moves the cell zoom and the
> option sliders by steps of 5 %, snapped onto multiples of 5.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the wheel changes values in fine, uneven increments:

- **Over a cell**, each notch multiplies the zoom by 2^(1/4) (`GridPreview.OnMouseWheel` →
  `ZoomAt`, `NotchesPerDoubling = 4`): 100 % → 119 % → 141 % → 168 % → 200 %. Crossing 100 %
  snaps exactly onto 100 %.
- **Over an option slider** (options toolbar and Global effects row), the stock `TrackBar` moves by
  `SmallChange = 1` per notch — 1 % for most sliders, 1° for the fine angle, one hundredth of a
  doubling for the zoom slider, one frame for Frames.

With **Ctrl held**, every one of these wheels moves by **5 %**, the value **snapped onto the grid of
multiples of 5** (103 % → 105 % → 110 %; 103 % → 100 % the other way). Without Ctrl, nothing
changes.

No modifier is read on any wheel path today (`ModifierKeys` only serves the Ctrl+V/C/S shortcuts and
Shift for free placement), so Ctrl+wheel is free.

---

## Behaviour

### The Step

- One wheel notch with Ctrl = one move to the **next multiple of 5** in the wheel's direction:
  from a value already on the grid, ±5; from a value off the grid, to the nearest multiple on that
  side (103 → 105 up, 103 → 100 down).
- Several notches in one event (fast wheels) move that many steps; fractional notches accumulate as
  they already do over a cell (`_wheelDelta`).
- The result is **clamped** to the control's range (cell zoom: `ImageLook.MinZoom`…`MaxZoom`,
  10 %…1600 %; each slider: its `Minimum`…`Maximum`). A bound that is not a multiple of 5 is still
  reachable: the last step stops on it.
- **Without Ctrl, every wheel behaves exactly as today.**

### Over a Cell (Zoom)

- The step applies to the **zoom percentage** (`Zoom × 100`): 100 % → 105 % → 110 %…
- Everything else in `ZoomAt` is unchanged: the pixel under the cursor stays under the cursor, the
  Zoom effect is turned on (`ImageLook.WithZoom` already activates it), locked cells and drags still
  ignore the wheel.

### Over an Option Slider

The step is expressed in the unit **the user reads** next to the slider, not in the slider's raw
value:

| Slider | Raw value | Ctrl step |
|---|---|---|
| Background opacity, Black & white, Blur intensity | % | 5 %, snapped |
| Volume, Soundtrack volume | % (0…`MaxLevel`×100) | 5 %, snapped |
| Zoom | hundredths of a doubling (log scale) | *see Open Questions* |
| Rotate — fine angle | degrees (±`MaxFineAngle`) | *see Open Questions* |
| Frames | frame index | *see Open Questions* |

- Moving a slider by Ctrl+wheel goes through its usual `ValueChanged` path, so it **activates the
  effect** like any other change of its options (RULES.md § Options Toolbar).
- Which slider receives the wheel (focused vs hovered): *see Open Questions*.

---

## Implementation Sketch

- **One pure helper** computing "next multiple of `step` in direction `notches`, clamped" — shared
  by the cell and the sliders.
- **Cell**: `GridPreview.OnMouseWheel` reads `ModifierKeys`; with Ctrl, the target zoom comes from
  the helper on `Zoom × 100` instead of `Math.Pow(2, …)`. `ZoomAt` takes the target zoom (or a
  zoom function) so the cursor-anchoring code stays shared.
- **Sliders**: every slider is built by `MainForm.OptionSlider` (`MainForm.cs:1251`). It returns a
  small `TrackBar` subclass overriding `OnMouseWheel`: with Ctrl it computes the new value with the
  helper — through an optional raw ↔ displayed-unit mapping for the sliders whose raw value is not
  the unit shown (Zoom, maybe Frames) — sets it, and marks the event handled so the stock 1-unit
  move does not add to it. Without Ctrl it defers to the stock behaviour.

---

## Test Impact

Not applicable — the repository has no test project (same as the previous workfiles). The snapping
helper is written as a pure static function, so it can be pinned the day a test project exists.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~Which wheels does Ctrl affect?~~ → The cell zoom **and** the option sliders (options toolbar
  and Global effects row).
- [x] ~~How does the 5 % step apply?~~ → Snapped onto multiples of 5 (103 → 105 → 110).
- [x] ~~Does the wheel without Ctrl change?~~ → No, unchanged.
- [ ] Zoom — the cell range is 10 %…1600 %: a flat 5-point step is a 50 % jump at 10 % and takes
  ~300 notches from 100 % to 1600 %. Keep 5 points everywhere, on the cell and on the (log-scale)
  Zoom slider alike?
- [ ] Rotate — fine angle (degrees, ±45°): 5° snapped onto multiples of 5°, or 5 % of the range
  (4.5°)?
- [ ] Frames (frame index): 5 frames per notch, or 5 % of the frame count (the position in the
  animation, snapped on the 5 % grid, at least one frame)?
- [ ] Sliders — a stock `TrackBar` takes the wheel when it has the focus (and, depending on the
  Windows "scroll inactive windows" setting, when hovered). Leave that routing as it is, or make a
  hovered slider take Ctrl+wheel even without focus?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the request "Scroll par pas de 5% si touche CTRL maintenue" and the scoping
batch: Ctrl+wheel steps by 5 %, snapped onto multiples of 5, on the cell zoom and on every option
slider; the plain wheel is untouched. The exploration located the single wheel path of the cell
(`GridPreview.OnMouseWheel` / `ZoomAt`) and the single factory of every slider
(`MainForm.OptionSlider`). Four points remain open: the zoom's wide range, the fine angle's unit,
the frames' unit, and which slider receives the wheel.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which wheels does Ctrl affect? | The cell zoom and the option sliders | 2026-09-26 |
| 2 | How does the 5 % step apply? | Snapped onto multiples of 5 | 2026-09-26 |
| 3 | Does the wheel without Ctrl keep its behaviour? | Yes, unchanged | 2026-09-26 |
| 4 | Is the subject straightforward or tricky? | Straightforward — a single scout pass | 2026-09-26 |
| 5 | Zoom: keep a flat 5-point step on the cell and the Zoom slider despite the 10 %…1600 % range? | | |
| 6 | Fine angle: 5° snapped, or 5 % of the range (4.5°)? | | |
| 7 | Frames: 5 frames, or 5 % of the frame count? | | |
| 8 | Sliders: leave the wheel routing as is, or let a hovered slider take Ctrl+wheel without focus? | | |

---

*Last updated: 2026-09-26*
