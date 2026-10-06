# Wheel Zoom Step

> Working document — the mouse wheel zooms by steps of 5 %, 1 % with Ctrl held.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the wheel over a cell zooms **geometrically** — 4 notches double the zoom
(`GridPreview.NotchesPerDoubling`), crossing 100 % stops on it — and **Ctrl + wheel** moves it onto
the next multiple of 5 % (`workfiles/20260926-ctrl-wheel-5-percent-step.md`). The Zoom slider takes
the stock wheel (its log-scale units), and its Ctrl + wheel steps the zoom by 5 % like the cell
(`MainForm.StepZoom`, wired through `StepSlider.ControlWheel`).

The request swaps this around for the zoom: **the plain wheel steps by 5 percentage points, Ctrl by
1 point** — **25 and 5 points above 200 %** — on the cell and on the Zoom slider's wheel alike. The
other option sliders keep their Ctrl + wheel = 5 %.

Components:

| Component | Role today |
|---|---|
| `UI/WheelSteps.cs` | `Snap(value, step, notches)`, `Percent = 5`, `WithControl(Message)` |
| `UI/GridPreview.cs` — `OnMouseWheel`, `ZoomAt` | Wheel over a cell: geometric, or `Snap(…, 5, …)` with Ctrl |
| `UI/StepSlider.cs` | Option slider: stock wheel, `ControlWheel` / `ControlStep` with Ctrl |
| `UI/MainForm.cs` — `_zoom`, `StepZoom`, `SetZoom` | The Zoom slider, log scale (`Log2(zoom) × 100`), 10 %–1600 % |
| `README.md` lines 28 and 78 | Describe the wheel and Ctrl + wheel |

---

## Wheel over a Cell

- One notch moves the zoom by a number of **percentage points** (additive: 100 → 105 → 110 %, not
  ×1.05), coarser above 200 %:

  | Zoom range the notch moves in | Plain wheel | Ctrl + wheel |
  |---|---|---|
  | Up to 200 % | 5 points | 1 point |
  | Above 200 % | 25 points | 5 points |

- The range is the one the notch **moves into**: up from 200 % or more, or down from above 200 %,
  takes the coarse step; so 190 → 195 → 200 → 225 → 250 up, and 250 → 225 → 200 → 195 down. 200 %
  being a multiple of every step, the two ranges join on it.
- From a value between two multiples of the step, the first notch **stops on the next multiple in
  its direction** — `WheelSteps.Snap` as it is: 103 % → 105 % up, → 100 % down; 213 % → 225 % up,
  → 200 % down; with Ctrl, 103.4 % → 104 % up, → 103 % down.
- Several notches at once (a fast wheel) are taken **one at a time**, each with the step of the
  range it moves into, so a burst crossing 200 % lands where the same notches one by one would.
- The zoom stays within **10 %–1600 %** (`ImageLook.MinZoom` / `MaxZoom`).
- 100 % being a multiple of both steps, the "crossing 100 % stops on it" rule holds by itself.
- Everything else is unchanged: zoom around the point under the mouse, live then smoothed when the
  wheel stops, the zoom badge, the cells where the wheel does nothing (crop edit view, a gesture
  running, export).
- The geometric zoom (`NotchesPerDoubling`) has no more use and goes.

## Zoom Tab Controls

- The **Zoom slider's wheel** follows the same steps as the cell — the table above, plain and with
  Ctrl — applied exactly (the log scale is too coarse at high zooms — the reason `StepZoom` exists).
  It therefore takes over the plain wheel too, not only Ctrl + wheel.
- The slider's **drag**, its snap to 100 % near its mark, and its **keyboard** (arrows, Page Up /
  Down, in its log units) are unchanged.

## Other Option Sliders

- **Unchanged**: opacities, volume, fine angle, borders' thickness, Frames… keep the stock wheel and
  **Ctrl + wheel = 5 %**. Ctrl therefore means *coarse* on them and *fine* on the zoom — accepted.

---

## Test Impact

Not applicable — the repository has no test project (as for
`workfiles/20260926-ctrl-wheel-5-percent-step.md`). `WheelSteps.Snap` stays a pure static function,
ready to be pinned the day one exists.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~What is a "5 % step"?~~ → Percentage points, additive (100 → 105 → 110 %)
- [x] ~~From an off-grid value (103 %), what does a plain notch give?~~ → It snaps onto the next
  multiple of 5 in its direction (105 % up, 100 % down)
- [x] ~~Where does the new step apply?~~ → The wheel over a cell **and** the Zoom tab's controls
- [x] ~~At high zooms a 5-point step is small (100 → 1600 % takes 300 notches, against 16 today):
  5 points everywhere, or a coarser step above some zoom?~~ → Above 200 %, 25 points (5 with Ctrl)
- [x] ~~The other option sliders: keep Ctrl + wheel = 5 % (Ctrl then means the opposite on the zoom),
  or follow the zoom (plain wheel = 5 %, Ctrl = finest unit)?~~ → Unchanged
- [x] ~~The Zoom slider's keyboard (arrows, Page Up / Down): unchanged, or 5 / 1 points as well?~~ →
  Unchanged

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-06

Initial design from the request ("le zoom molette doit être de pas de 5 % ; si Ctrl maintenu, 1 %")
and the scoping batch: additive steps snapped onto their multiples, on the cell and on the Zoom
slider's wheel. The exploration found that the request inverts the current behaviour (Ctrl gave
the 5 % step, the plain wheel was geometric), which raises the three open questions above.

### Iteration 2 — 2026-10-06

Answers to the three open questions: above 200 % the steps become 25 points (5 with Ctrl); the
other option sliders and the Zoom slider's keyboard stay as they are. The agent settled the
boundary: the step is the one of the range a notch moves into, notches taken one at a time, so
200 % joins both ranges (… 195 → 200 → 225 …).

### Iteration 3 — 2026-10-06 — ✅ Implemented

Go given ("vas-y dans un worktree, code et doc"): code and README; unit tests not applicable (no
test project). The run works in the worktree `.claude/worktrees/wheel-zoom-step` on
`feature/wheel-zoom-step`, fast-forwarded into `main` and removed at the end.

### Iteration 4 — 2026-10-06 — 🧭 Implementation choices

No rule broken, no divergence from the frozen design. Choices the design left open:

- **`WheelSteps.Zoom(percent, notches, fine)`** holds the rule — constants `ZoomStep` (5),
  `FineZoomStep` (1), `CoarseZoomFrom` (200), `CoarseZoomFactor` (5) — and loops one notch at a
  time over the existing `Snap`; `Percent` stays the other sliders' Ctrl + wheel step. Not clamped:
  the callers clamp (`ImageLook.WithZoom`, `StepZoom`).
- **`GridPreview.ZoomAt`** keeps its "crossing 100 % stops on it" check: a single notch always lands
  on 100 % anyway, but a burst of notches (fast wheel) crossing it still stops there, as before.
  `NotchesPerDoubling` is removed.
- **`StepSlider.Wheel`** (`Action<int, bool>`, notches + Control held) is a new hook taking over the
  whole wheel, winning over `ControlWheel`; the zoom slider uses it (`StepZoom(notches, fine)`), the
  Frames slider keeps `ControlWheel`.
- **README**: the Features line on the zoom, the Ctrl + wheel paragraph of § Effects (the zoom
  slider now the exception) and § Zoom's options line.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-06 | `WheelSteps.Zoom`, `GridPreview.ZoomAt`, `StepSlider.Wheel`, `MainForm.StepZoom` |
| Unit tests | — | — | Not applicable — no test project |
| README | 3 | 2026-10-06 | Features (zoom), § Effects (Ctrl + wheel), § Zoom |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What is a "5 % step" — percentage points or a ×1.05 factor? | Percentage points | 2026-10-06 |
| 2 | From an off-grid value (103 %), a plain notch: snap onto a multiple of 5, or a pure ±5? | Snap onto the multiple of 5 | 2026-10-06 |
| 3 | Where does the new step apply: the wheel over a cell only, or the Zoom tab's controls too? | The wheel and the Zoom controls | 2026-10-06 |
| 4 | Depth of the exploration: straightforward, or tricky / long? | Straightforward | 2026-10-06 |
| 5 | High zooms: 5 points everywhere, or a coarser step above some zoom? | "5 % devient 25 % au-dessus de 200 %, 1 % devient 5 % au-dessus de 200 %" | 2026-10-06 |
| 6 | The other sliders: keep Ctrl + wheel = 5 %, or follow the zoom? | Unchanged | 2026-10-06 |
| 7 | The Zoom slider's keyboard: unchanged, or 5 / 1 points? | Unchanged | 2026-10-06 |
| 8 | Go for the implementation? | Code and documentation, in a worktree | 2026-10-06 |

---

*Last updated: 2026-10-06*
