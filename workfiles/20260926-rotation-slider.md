# Rotation Slider

> Working document — the Rotate effect's slider covers the whole circle, −180° to +180°, instead
> of a ±45° fine angle added to the quarter turns.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the Rotate effect's options are four quarter-turn option thumbnails (**0°**, **90°**,
**180°**, **270°**) and a fine-angle slider from −45° to +45°, added to the quarter turn;
Ctrl+R / Ctrl+Shift+R turn the hovered or selected image a quarter turn. The user wants a
single slider able to go all the way around the circle.

Relevant components:

| Component | Role today |
|---|---|
| `Composition/ImageLook.cs` | `Rotation` (0/90/180/270) + `FineAngle` (±`MaxFineAngle` = 45); `Rotate(quarterTurns)` turns the flips and the focus with the image; `WithRotation`, `WithFineAngle`; `KeptRotation` |
| `Composition/FitCalculator.cs` | `ComputeTurned(cell, orientedSize, zoom, focus, fineAngle)` — the cover zoom and the turn transform around the cell's center |
| `Composition/Compositor.cs` | `DrawCell`: orients the bitmap (quarter turn + flips), then applies the fine-angle turn |
| `UI/MainForm.cs` | `_quarterTurns` (a `QuarterTurnStrip`), `_fineAngle` (`OptionSlider(-45, 45, 5)`, 160 × 26 px), `_fineAngleLabel`, `SetFineAngle`, `TurnQuarter` (Ctrl+R / Ctrl+Shift+R, `workfiles/20260930-rotation-ctrl-r.md`), the sync of the options with the selected cell |
| `UI/QuarterTurnStrip.cs` | The four quarter-turn option thumbnails (`ThumbnailStrip<int>`), the one the angle falls exactly on pressed |
| `UI/GridPreview.cs` | Pan stops and zoom around the cursor, computed with `FineAngle` |

---

## Options Toolbar — Rotate

- The four quarter-turn option thumbnails **stay**, before the slider, as shortcuts (Q&A #11): a
  click sets the angle exactly on that quarter turn; the one the angle falls exactly on is pressed,
  none otherwise — as today. Their labels follow the slider's range: **0°**, **90°**, **180°**,
  **−90°** (the pictograms unchanged).
- One slider, **−180° → +180°**, by 1°, centered on 0°; its label reads `Angle: +37°` (same format
  as today: sign, then degrees; `0°` without sign).
- **No snapping**: the angle moves freely, degree by degree.
- Keyboard: arrows move by 1° (`SmallChange`); a click on the slider's bar beside the thumb, and
  PageUp / PageDown, jump by **15°** (`LargeChange`).
- Width: **360 px** (1 px per degree with the mouse), instead of the 160 px of the other option sliders.
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
  buttons did: the flips swap axis, the focus and the Crop's kept part (`CropEffect.QuarterTurned`)
  turn with the image, so the image seen on screen turns continuously — no jump when the slider
  passes ±45°, ±135°.
- At ±45° exactly, the nearer quarter is the current one (no flip-flop around the boundary).
- `KeptRotation`, `TurnOn` / `TurnOff` / `Reset` keep working on (`Rotation`, `FineAngle`), unchanged.
- `MaxFineAngle` stays (internal bound of the decomposition); the UI no longer uses it.

## Ctrl+R / Ctrl+Shift+R

- They go to the **next quarter turn** that way (Q&A #12), so they always land on a right angle:
  Ctrl+R to the nearest quarter turn strictly clockwise (+37° → +90°, +90° → +180°, +180° → −90°),
  Ctrl+Shift+R strictly counter-clockwise (+37° → 0°, 0° → −90°). On a quarter turn, that is ±90°
  as today.

## Cover

- Unchanged behaviour, now on the whole circle: at any angle, the image turns around the center of
  its cell, zoomed just enough to keep covering it (no empty corner). Since the fine part never
  exceeds ±45° on the oriented image, `FitCalculator.ComputeTurned` already computes it.

## Documentation

- **README** and **README.fr** (same commit, `../CLAUDE.md` § French Versions) § *Rotate*: the
  fine angle becomes one angle slider from −180° to +180° by 1°, after the quarter-turn thumbnails
  (0°, 90°, 180°, −90°); Ctrl+R / Ctrl+Shift+R go to the next quarter turn; the 8 mentions of "fine angle" (Background edges, Crop, Zoom…) become "an angle
  other than a quarter turn" where they speak to the user.
- **GLOSSARY** and **GLOSSARY.fr**: a new **Angle** row — the Rotate effect's angle, −180° to +180°,
  the image zoomed to keep covering its cell. *Fine angle* stays, redefined as the code's ±45° part
  of the angle beyond its nearest quarter turn (`ImageLook.FineAngle`), so RULES.md, which names it
  in the Crop and Zoom rules, stays true untouched.

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

- [x] ~~Page step of the slider (click on the track, PageUp / PageDown): 15°, 45° or 90°?~~ → 15°
- [x] ~~The four quarter-turn option thumbnails, now pictograms: removed as decided (Q&A #1), or kept
      beside the slider as shortcuts to 0°, 90°, 180°, 270°?~~ → Kept beside the slider
      *(revises Q&A #1, see Iteration 6)*
- [x] ~~Ctrl+R / Ctrl+Shift+R on an angle off a quarter (e.g. +37°): add ±90° keeping the rest
      (+127°), or go to the next quarter turn that way (+90°)?~~ → The next quarter turn that way
- [x] ~~Width of the slider: 361 positions on today's 160 px leave ~0.4 px per degree with the
      mouse. Keep 160 px (the arrows give the exact degree), or widen it (e.g. 360 px, 1 px per degree)?~~
      → 360 px, 1 px per degree

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

### Iteration 2 — 2026-09-26

The slider is widened to 360 px (Q&A #6). The page step question (Q&A #5) is asked again: the
user asked what the slider's track is.

### Iteration 3 — 2026-09-26

The page step is 15° (Q&A #7): a click on the bar beside the thumb, and PageUp / PageDown, jump by
15°. No open question remains.

### Iteration 4 — 2026-09-30

Design re-checked against the code, after the Crop effect landed (`workfiles/20260930-crop-effect.md`):
`Rotate(quarterTurns)` now also quarter-turns the crop, so `WithAngle`, going through it, keeps the
kept part on the same content; the Crop's consumers read `ImageLook.Shown`, which the quarter +
fine decomposition leaves unchanged. The rest of the rotation code (quarter-turn buttons, fine-angle
slider, `WithRotation` / `WithFineAngle`) is as the design describes. No open question.

### Iteration 5 — 2026-10-10

Design re-checked against the code again. Two changes landed since: the quarter turns became option
thumbnails (`QuarterTurnStrip`, `workfiles/20261007-effect-option-buttons.md`), and Ctrl+R /
Ctrl+Shift+R turn an image a quarter turn, the fine angle back to 0°
(`workfiles/20260930-rotation-ctrl-r.md`). Both reopen a question: whether the thumbnails go, and
what a quarter turn does to a free angle. The documentation now includes the French README and
glossary, and *Fine angle* is kept in the glossary as the code's term so RULES.md stays untouched.

### Iteration 6 — 2026-10-10

The quarter-turn thumbnails stay beside the slider as shortcuts (Q&A #11, revising Q&A #1), their
last one labelled −90° to match the slider's range. Ctrl+R / Ctrl+Shift+R go to the next quarter
turn that way (Q&A #12). No open question remains.

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
| 5 | Page step of the slider: 15°, 45° or 90°? | Asked what the track is — explained, asked again (#7) | 2026-09-26 |
| 6 | Width of the slider: keep 160 px or widen it? | Widen to 360 px | 2026-09-26 |
| 7 | Page step (a click on the slider's bar beside the thumb, PageUp / PageDown): 15°, 45° or 90°? | 15° | 2026-09-26 |
| 8 | Go for the implementation? | No — the gate holds | 2026-09-26 |
| 9 | Go for the implementation? | Dismissed — asked again | 2026-09-30 |
| 10 | Go for the implementation? | Dismissed | 2026-09-30 |
| 11 | Quarter-turn thumbnails: removed, or kept beside the slider? | Kept beside the slider | 2026-10-10 |
| 12 | Ctrl+R on an angle off a quarter: +90° keeping the rest, or the next quarter turn? | The next quarter turn | 2026-10-10 |
| 13 | Go for the implementation? | | |

---

*Last updated: 2026-10-10*
