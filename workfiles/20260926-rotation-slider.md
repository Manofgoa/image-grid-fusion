# Rotation Slider

> Working document — the Rotate effect's slider covers the whole circle, −180° to +180°, instead
> of four quarter-turn buttons plus a ±45° fine angle.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the Rotate effect's options are four quarter-turn buttons (**0°**, **90°**, **180°**,
**270°**) and a fine-angle slider from −45° to +45°, added to the quarter turn. The user wants a
single slider able to go all the way around the circle.

Relevant components:

| Component | Role today |
|---|---|
| `Composition/ImageLook.cs` | `Rotation` (0/90/180/270) + `FineAngle` (±`MaxFineAngle` = 45); `Rotate(quarterTurns)` turns the flips and the focus with the image; `WithRotation`, `WithFineAngle`; `KeptRotation` |
| `Composition/FitCalculator.cs` | `ComputeTurned(cell, orientedSize, zoom, focus, fineAngle)` — the cover zoom and the turn transform around the cell's center |
| `Composition/Compositor.cs` | `DrawCell`: orients the bitmap (quarter turn + flips), then applies the fine-angle turn |
| `UI/MainForm.cs` | `_quarterTurns` (4 option buttons), `_fineAngle` (`OptionSlider(-45, 45, 5)`, 160 × 26 px), `_fineAngleLabel`, `SetFineAngle`, the sync of the options with the selected cell |
| `UI/GridPreview.cs` | Pan stops and zoom around the cursor, computed with `FineAngle` |

---

## Options Toolbar — Rotate

- The four quarter-turn buttons are **removed**.
- One slider, **−180° → +180°**, by 1°, centered on 0°; its label reads `Angle: +37°` (same format
  as today: sign, then degrees; `0°` without sign).
- **No snapping**: the angle moves freely, degree by degree.
- Keyboard: arrows move by 1° (`SmallChange`); page step: see Open Questions.
- Width: see Open Questions.
- −180° and +180° are the same look. The slider keeps the value the user put it on: when the cell's
  angle is re-read into the slider and the slider's current value is equivalent (mod 360°), the
  slider is left where it is, so dragging to −180° does not make the thumb jump to +180°. On
  selecting another cell, a half turn is shown as **+180°**.

## Angle Model

- The model keeps its current decomposition, so the renderer, the cover zoom and the pan stops are
  untouched: `Rotation` = the quarter turn **nearest** to the angle, `FineAngle` = the rest, within
  ±45°. Example: +130° → `Rotation` 90, `FineAngle` +40; −100° → `Rotation` 270, `FineAngle` −10.
- A new `ImageLook.Angle` (−180…+180, derived: `Rotation + FineAngle` brought into that range) and
  `ImageLook.WithAngle(int degrees)` replace `WithRotation` / `WithFineAngle` on the UI side.
- `WithAngle` crosses a quarter-turn boundary through `Rotate(quarterTurns)`, as the quarter-turn
  buttons did: the flips swap axis and the focus turns with the image, so the image seen on screen
  turns continuously — no jump when the slider passes ±45°, ±135°.
- At ±45° exactly, the nearer quarter is the current one (no flip-flop around the boundary).
- `KeptRotation`, `TurnOn` / `TurnOff` / `Reset` keep working on (`Rotation`, `FineAngle`), unchanged.
- `MaxFineAngle` stays (internal bound of the decomposition); the UI no longer uses it.

## Cover

- Unchanged behaviour, now on the whole circle: at any angle, the image turns around the center of
  its cell, zoomed just enough to keep covering it (no empty corner). Since the fine part never
  exceeds ±45° on the oriented image, `FitCalculator.ComputeTurned` already computes it.

## Documentation

- **README** § *Rotate*: the options become one angle slider from −180° to +180° by 1°; the
  quarter-turn buttons sentence goes; "at a fine angle" becomes "at any angle other than a quarter
  turn".
- **GLOSSARY**: *Fine angle* is replaced by **Angle** — the Rotate effect's angle, −180° to +180°,
  the image zoomed to keep covering its cell.

---

## Test Impact

**Nothing to test** — the solution has no test project, and creating one is outside this scope
(every previous workfile stayed test-free). Verification is manual: a photo and a video cell, the
slider dragged across ±45°, ±135° and ±180° with and without a flip (no jump), a zoomed and moved
image turned, the effect turned off and on again (angle kept), Reset, then a copy / save (PNG and
MP4).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [ ] Page step of the slider (click on the track, PageUp / PageDown): 15°, 45° or 90°?
- [ ] Width of the slider: 361 positions on today's 160 px leave ~0.4 px per degree with the
      mouse. Keep 160 px (the arrows give the exact degree), or widen it (e.g. 360 px, 1 px per degree)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Request: *"the rotation slider doesn't suit me, I want to be able to go all the way around the
circle"*. Scoping (Q&A #1–4): one slider −180° → +180° replacing the quarter-turn buttons, the
cover zoom kept at every angle, no snapping, straightforward subject. Code read: the model keeps
its quarter turn + fine angle decomposition, driven by a new `WithAngle`, so rendering, cover and
pan stops stay as they are; only the Rotate options and the documentation change.

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
| 1 | How should the 360° rotation look? | One slider −180° → +180°; the quarter-turn buttons go | 2026-09-26 |
| 2 | At any angle, does the image keep covering its cell? | Yes, automatic zoom, as the fine angle does | 2026-09-26 |
| 3 | Should the slider snap onto some angles? | No snapping | 2026-09-26 |
| 4 | Is the subject straightforward or tricky? | Straightforward | 2026-09-26 |
| 5 | Page step of the slider: 15°, 45° or 90°? | | |
| 6 | Width of the slider: keep 160 px or widen it? | | |

---

*Last updated: 2026-09-26*
