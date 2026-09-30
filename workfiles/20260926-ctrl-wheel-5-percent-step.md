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
- **Over an option slider** (options toolbar and global options toolbar), the stock `TrackBar` moves by
  `SmallChange = 1` per notch — 1 % for most sliders, 1° for the fine angle, one hundredth of a
  doubling for the zoom slider, one frame for Frames.

With **Ctrl held**, every one of these wheels moves by **5 %**, the value **snapped onto the grid of
multiples of 5** (103 % → 105 % → 110 %; 103 % → 100 % the other way). Without Ctrl, nothing
changes.

Zoom, fine angle, frames and borders thickness use a step fitted to their unit — see § Over an
Option Slider. Ctrl + wheel over the file explorer's tiles (the tile size) is another control, out
of this scope, and unchanged.

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

- The step applies to the **zoom percentage** (`Zoom × 100`): 100 % → 105 % → 110 %…, a flat
  5 points over the whole 10 %…1600 % range — Ctrl is the fine adjustment, the plain wheel keeps
  the big multiplicative jumps.
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
| Borders opacity | % (`MinOpacity`…100) | 5 %, snapped |
| Borders thickness | thousandths of the grid's shorter side, shown as 0.1 %…6.0 % | **0.5 %**, snapped (0.5 → 1.0 → 1.5…) |
| Zoom | hundredths of a doubling (log scale) | 5 % of the **zoom percentage** shown, snapped — the same values as on the cell |
| Rotate — fine angle | degrees (±`MaxFineAngle`) | **5°**, snapped (0 → 5 → 10…) |
| Frames | frame index | **5 % of the frame count** — the position in the animation snapped on the 5 % grid, at least one frame per notch |

- Moving a slider by Ctrl+wheel goes through its usual `ValueChanged` path, so it **activates the
  effect** like any other change of its options (RULES.md § Options Toolbar).
- **Wheel routing unchanged**: Ctrl+wheel goes to the slider the plain wheel goes to today (the
  focused one, or the hovered one when Windows routes it so) — no new hover handling.

---

## Implementation

- **`UI/WheelSteps.cs`** — the pure helpers: `Snap(value, step, notches)` gives the multiple of
  `step` reached after `notches` (from between two multiples, the first notch stops on the next one;
  a value within 1/1000 of a step of a multiple counts as on it); `Within(value, target, notches,
  min, max)` moves an integer control at least one unit per notch and clamps it; `WithControl(m)`
  reads Control from the wheel message's own `MK_CONTROL` flag, as `ThumbnailGrid` already does.
- **Cell** — `GridPreview.WndProc` records the flag of each `WM_MOUSEWHEEL`; `OnMouseWheel` passes
  it to `ZoomAt(index, location, notches, fine)`, which computes `Snap(Zoom × 100, 5, notches) / 100`
  instead of the ×2^(1/4) per notch. Cursor anchoring, stops, badge and `WithZoom`'s clamp and
  activation are shared.
- **Sliders** — `UI/StepSlider.cs`, a `TrackBar` recording the same flag in `WndProc`; with Control,
  `OnMouseWheel` marks the event handled (no stock nor native move), accumulates the notches, then
  either calls its `ControlWheel` hook or moves `Value` to `Within(Snap(Value, ControlStep, …))`.
  `MainForm.OptionSlider` returns it, with a `controlStep` parameter (5 by default; 5 thousandths for
  the borders thickness).
  - **Zoom slider** — `MainForm.StepZoom`: snaps the selected image's zoom (its kept one when off)
    and applies it **exactly** through `ApplyZoom` — the thumb set to the nearest log-scale position
    while `_syncingEffects` holds — because hundredths of a doubling are coarser than 5 % above
    ~700 %.
  - **Frames slider** — `MainForm.StepFrames`: snaps `index / count` on the 5 % grid, back to the
    nearest index, at least one frame per notch.

## Test Impact

Not applicable — the repository has no test project (same as the previous workfiles). The snapping
helper is written as a pure static function, so it can be pinned the day a test project exists.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~Which wheels does Ctrl affect?~~ → The cell zoom **and** the option sliders (options toolbar
  and global options toolbar).
- [x] ~~How does the 5 % step apply?~~ → Snapped onto multiples of 5 (103 → 105 → 110).
- [x] ~~Does the wheel without Ctrl change?~~ → No, unchanged.
- [x] ~~Zoom — the cell range is 10 %…1600 %: a flat 5-point step is a 50 % jump at 10 % and takes
  ~300 notches from 100 % to 1600 %. Keep 5 points everywhere, on the cell and on the (log-scale)
  Zoom slider alike?~~ → Yes, 5 points everywhere, cell and Zoom slider.
- [x] ~~Rotate — fine angle (degrees, ±45°): 5° snapped onto multiples of 5°, or 5 % of the range
  (4.5°)?~~ → 5°, snapped.
- [x] ~~Frames (frame index): 5 frames per notch, or 5 % of the frame count (the position in the
  animation, snapped on the 5 % grid, at least one frame)?~~ → 5 % of the frame count.
- [x] ~~Sliders — a stock `TrackBar` takes the wheel when it has the focus (and, depending on the
  Windows "scroll inactive windows" setting, when hovered). Leave that routing as it is, or make a
  hovered slider take Ctrl+wheel even without focus?~~ → Left as it is.
- [x] ~~Borders thickness (shown 0.1 %…6.0 %): a 5-point step is nearly its whole range. Step of
  0.5 % snapped, 5 % of the range, or keep its plain wheel even with Ctrl?~~ → 0.5 %, snapped.

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

### Iteration 2 — 2026-09-30

The code moved since Iteration 1 (commit aa38add and later): the Global effects row became the
global effects toolbar over the global options toolbar, and the Borders global effect added two
sliders built by `OptionSlider` — thickness and opacity. The opacity follows the plain 5 % rule;
the thickness (0.1 %…6.0 %) gets its own open question. The first open-question batch was
dismissed; the user asked for it again as multiple-choice questions.

### Iteration 3 — 2026-09-30

Every open question answered, all on the recommended option: a flat 5-point zoom step on the cell
and the Zoom slider, 5° snapped for the fine angle, 5 % of the frame count for Frames, the wheel
routing of the sliders unchanged, 0.5 % snapped for the Borders thickness. The slider table and
the implementation sketch now state each step.

### Iteration 4 — 2026-09-30 — ✅ Implemented

Go given by the user ("lance l'implémentation"), read as code + documentation — unit tests do not
apply, the repository has no test project. The run waited for the "exported video length" session
to finish in the same checkout, then started on `main` (the repository's working branch).

### Iteration 5 — 2026-09-30 — 🧭 Implementation choices

- **Control read from the wheel message** (`MK_CONTROL`), the idiom `ThumbnailGrid` already uses,
  rather than `ModifierKeys`. Side effect worth knowing: a precision touchpad's pinch reaches
  applications as Ctrl + wheel, so pinching over a cell now zooms by 5 % steps.
- **The zoom slider applies the exact snapped zoom** instead of moving its log-scale thumb: one unit
  of that scale is ~0.7 % at 100 % but ~11 % at 1600 %, too coarse to land on multiples of 5; the
  thumb is placed on the nearest position, and the label shows the exact percentage.
- **"At least one unit per notch" for every slider**, not only Frames — a no-op where the step is a
  whole number of units, a guarantee that no notch is lost.
- Names: `WheelSteps` (helpers), `StepSlider` (the slider), `StepZoom` / `StepFrames` / `ApplyZoom`
  in `MainForm`.
- The go was read as **code + documentation** (unit tests not applicable); the run stayed on
  **`main`**, the repository's working branch — no branch question.
- No project rule broken.

### Iteration 6 — 2026-09-30

The user tested the delivery by hand and confirmed the task is finished.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-09-30 | Cell wheel (`GridPreview`), option sliders (`StepSlider`, `MainForm`) |
| Unit tests | 5 | 2026-09-30 | Not applicable — no test project |
| README | 5 | 2026-09-30 | Zoom gesture line, options row bullet |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which wheels does Ctrl affect? | The cell zoom and the option sliders | 2026-09-26 |
| 2 | How does the 5 % step apply? | Snapped onto multiples of 5 | 2026-09-26 |
| 3 | Does the wheel without Ctrl keep its behaviour? | Yes, unchanged | 2026-09-26 |
| 4 | Is the subject straightforward or tricky? | Straightforward — a single scout pass | 2026-09-26 |
| 5 | Zoom: keep a flat 5-point step on the cell and the Zoom slider despite the 10 %…1600 % range? | Yes, 5 points everywhere | 2026-09-30 |
| 6 | Fine angle: 5° snapped, or 5 % of the range (4.5°)? | 5° snapped | 2026-09-30 |
| 7 | Frames: 5 frames, or 5 % of the frame count? | 5 % of the frame count | 2026-09-30 |
| 8 | Sliders: leave the wheel routing as is, or let a hovered slider take Ctrl+wheel without focus? | Left as it is | 2026-09-30 |
| 9 | Borders thickness: 0.5 % snapped, 5 % of the range, or excluded? | 0.5 % snapped | 2026-09-30 |

---

*Last updated: 2026-09-30*
