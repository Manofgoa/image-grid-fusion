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
| `Composition/LookPreset.cs` | An effect preset saves the Rotate effect as `rotation` + `fineAngle`, restores it through `WithRotation` / `WithFineAngle`, and describes it (`Describe`: `90° +37°`) |
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
- Ctrl + wheel: onto the next multiple of **5°**, the default `ControlStep` of every option slider
  (`StepSlider`, `workfiles/20260926-ctrl-wheel-5-percent-step.md`); the plain wheel keeps the stock
  step, as the fine angle does (`workfiles/20261006-wheel-zoom-step.md`).
- Width: **360 px** (1 px per degree with the mouse), instead of the 160 px of the other option sliders
  — a deliberate exception, like the Colors effect's 90 px ones.
- −180° and +180° are the same look. The slider keeps the value the user put it on: when the cell's
  angle is re-read into the slider and the slider's current value is equivalent (mod 360°), the
  slider is left where it is, so dragging to −180° does not make the thumb jump to +180°. On
  selecting another cell, a half turn is shown as **+180°**.

## Angle Model

- **The quarter turn stays while the slider moves; the fine angle takes the whole rest**, within
  ±180° (`ImageLook.MaxFineAngle` = 180): `ImageLook.WithAngle(degrees)` sets
  `FineAngle = WrappedAngle(degrees − Rotation)`, `Rotation` untouched. The image is fitted in its
  quarter turn before it is turned, so the slider turns it **smoothly**, with no jump anywhere.
- `ImageLook.Angle` = `WrappedAngle(Rotation + FineAngle)`, −179…+180, what the slider and the
  presets' description show; `ImageLook.WrappedAngle` is the one wrapping rule, read by the UI too.
- The **quarter turn** changes only through the thumbnails and Ctrl+R (`WithRotation`, the fine angle
  back to 0°, the flips, the focus and the crop turning with it as before).
- Hence the same angle reached from two quarter turns is not the same look: at +90° by the slider from
  0°, the image is fitted landscape then turned, so zoomed more than the **90°** thumbnail, which
  fits it portrait. The thumbnail is pressed only on its exact quarter with no fine angle.
- `KeptRotation`, `TurnOn` / `TurnOff` / `Reset` keep working on (`Rotation`, `FineAngle`), unchanged.
- `FitCalculator`, `CanvasSizer`, `CellCopy`, the stops and the zoom routes take any fine angle as
  they are: their geometry is cos / sin based, never bounded to ±45°.

## Ctrl+R / Ctrl+Shift+R

- They act on the cell under the pointer, else the selected layer (`workfiles/20261029-layers.md`):
  the same rule for both, through the Rotate effect of its look.
- They go to the **next quarter turn** that way (Q&A #12), so they always land on a right angle:
  Ctrl+R to the nearest quarter turn strictly clockwise (+37° → +90°, +90° → +180°, +180° → −90°),
  Ctrl+Shift+R strictly counter-clockwise (+37° → 0°, 0° → −90°). On a quarter turn, that is ±90°
  as today. `ImageLook.NextQuarterTurn(direction)` gives it, `WithRotation` sets it, the fine angle
  back to 0°.

## Effect Presets

- The preset's JSON keeps its keys, `rotation` and `fineAngle`, so the presets already saved restore
  as before. Restoring sets them exactly (`WithRotation` then `WithFineAngle`, now within ±180°):
  the quarter turn decides the fit, so it is never re-derived from the angle.
- Its description line shows the angle as the slider does — `+127°` instead of `90° +37°`.

## Layers

- A layer's look is an `ImageLook` too: its Rotate effect gets the slider with nothing else to do.
  The layer's own angle (its round handle, `LayerPlacement`) is not the Rotate effect, and is left alone.

## Cover

- Unchanged behaviour, now on the whole circle: at any angle, the image turns around the center of
  its cell, zoomed just enough to keep covering it (no empty corner): `FitCalculator.ComputeTurned`
  computes it for any fine angle, its cover scale capped at 8 as before.
- The Crop's edit view still shows the image in its quarter turn, without the fine angle: with a fine
  angle beyond ±45°, it can stand at another quarter than the cell shows.

## Documentation

- **README** and **README.fr** (same commit, `../CLAUDE.md` § French Versions) § *Rotate*: the
  fine angle becomes one angle slider from −180° to +180° by 1°, after the quarter-turn thumbnails
  (0°, 90°, 180°, −90°); Ctrl+R / Ctrl+Shift+R go to the next quarter turn; the 8 mentions of "fine angle" (Background edges, Crop, Zoom…) become "an angle
  other than a quarter turn" where they speak to the user.
- **SHORTCUTS.md** and **SHORTCUTS.fr.md**, **in the same commit as the Ctrl+R code** (RULES.md
  § Keyboard Shortcuts): Ctrl+R / Ctrl+Shift+R go to the next quarter turn clockwise /
  counter-clockwise.
- **GLOSSARY** and **GLOSSARY.fr**: a new **Angle** row — the Rotate effect's angle, −180° to +180°,
  the quarter turn last chosen plus the fine angle. *Fine angle* stays, redefined as the part of the
  angle beyond its quarter turn, within ±180° (`ImageLook.FineAngle`), so RULES.md, which names it in
  the Crop and Zoom rules, stays true untouched.

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

### Iteration 7 — 2026-10-22

Design re-checked against the code: the changes since (Colors replacing Black & white, the arrival
zoom, documents laid out for their zoom) leave the rotation code as the design describes it. No
open question.

### Iteration 8 — 2026-11-04

Design re-checked against the code once more. Two consumers of the rotation landed since: the
effect presets save it as `rotation` + `fineAngle` and describe it (`LookPreset`), and the layers
carry an `ImageLook` of their own. The presets keep their format, restore through `WithAngle` and
describe the angle as the slider shows it; the layers need nothing. The new Keyboard Shortcuts rule
puts `SHORTCUTS.md` / `SHORTCUTS.fr.md` in the Ctrl+R commit. No open question.

### Iteration 9 — 2026-11-04

The user asked whether newer work goes against the choices made here. Every workfile since the
design that touches the rotation was read (Crop, Ctrl+R, option thumbnails, undo, wheel steps,
position readout, zoom fit and arrival, native size, presets, cell copy, Cutout, Upscale, Layers,
text layers, shortcuts list): **none contradicts it**. Their fine-angle rules — the cover zoom, the
box the stops use, the native size's cover scale, the edge extension — keep holding, since the
decomposition into quarter turn + fine angle is kept. Points added to the design rather than
conflicts:
- The option sliders now step onto multiples with Ctrl + wheel (`StepSlider.ControlStep`): the angle
  slider takes the default, 5°.
- Ctrl+R acts on the selected layer when the pointer is over no cell: the next-quarter rule applies
  to it the same way.
- The quarter-turn thumbnails' last label becomes −90° (Iteration 6), where
  `workfiles/20261007-effect-option-buttons.md` wrote 270°: a change of that workfile's label only.

### Iteration 10 — 2026-11-04 — ✅ Implemented

Go given ("Go", Q&A #16), read as **code, unit tests and documentation**: the design sections as
they stand at Iteration 9 are the frozen scope. No test project: the unit tests step does not apply.

### Iteration 11 — 2026-11-04 — 🧭 Implementation choices

- **Divergent — the angle model.** The frozen design took the quarter turn nearest to the angle and
  promised no jump at ±45°. It cannot work as written: the fitting rule fits the image in its quarter
  turn **before** turning it (`FitCalculator.ComputeTurned` → `DrawnSize` on the oriented size), so a
  landscape image fitted landscape at +45° and portrait at −45° from the next quarter is drawn at
  another size — the image would jump in size, its crop ratio and the Free format with it. Closest
  workable variant: the slider keeps the quarter turn and moves the fine angle over ±180°; the
  geometry already took any angle. Cost: the same angle reached from two quarter turns is zoomed
  differently, and the Crop's edit view stays in the quarter turn (see § Angle Model, § Cover).
- **Divergent — presets.** Restored exactly (`WithRotation` + `WithFineAngle`), not through
  `WithAngle`: the quarter turn decides the fit, so it is never re-derived.
- `ImageLook.WrappedAngle` is public, the one wrapping rule: the slider's ±180° check and the
  thumbnails' −90° label read it. The slider is a `StepSlider` from `MainForm.AngleSlider`, as the
  Colors' sliders are made; its fields renamed `_angle` / `_angleLabel`.
- The thumbnails' tooltips drop "the fine angle back to 0°" and say "90° counter-clockwise" for −90°.
- `SHORTCUTS.md` / `SHORTCUTS.fr.md`: the two Ctrl+R rows give examples of the next quarter turn.
- README: only the Rotate section changes; its other mentions of the fine angle stay true and are
  left as they are.
- The workfile's Iterations 8 to 10 had been written twice, a copy misplaced after Iteration 4 by an
  edit of this session: that copy was removed.
- Checked by a scratchpad program on the built assembly (not in the repo): angles, wrapping at ±180°,
  the next quarter turn both ways, the effect turned off and on again keeping its angle — all as
  designed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 11 | 2026-11-04 | Model, options, Ctrl+R (with SHORTCUTS.md / .fr) — see Iteration 11 |
| Unit tests | | | Not applicable — no test project |
| README | 11 | 2026-11-04 | README / README.fr § Rotate, GLOSSARY / GLOSSARY.fr: Angle, Fine angle |

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
| 13 | Go for the implementation? | Dismissed | 2026-10-10 |
| 14 | Go for the implementation? | Dismissed | 2026-10-22 |
| 15 | Go for the implementation? | Not answered — the user asked instead to check newer work against this design | 2026-11-04 |
| 16 | Go for the implementation? | "Go" — read as code, tests and documentation | 2026-11-04 |
| 17 | Branch gate: a dedicated worktree, or the main checkout? | A dedicated worktree, `feature/rotation-slider` | 2026-11-04 |

---

*Last updated: 2026-11-04*
