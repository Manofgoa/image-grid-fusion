# Color Eyedropper

> Working document — picking a color option's value with an eyedropper on the preview's cells,
> the standard Windows color dialog staying the other route.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Choosing a color today goes through the standard Windows color dialog only: one shared
`ColorDialog` (`MainForm._colorDialog`, `AnyColor = true`), opened by the **Color…** button of the
Background's options (`PickBackgroundColor`), the **Color…** button of the Borders' options
(`PickBorderColor`), and the ⚙ menu's OCR **Arrow color…** (`PickOcrArrowColor`).

The task adds an **eyedropper**, the first route: the Color… button becomes a split button whose
first part arms it; the color is then read from the **cells of the preview** under the cursor, a
click takes it. The Windows dialog stays, as the second route, and gains remembered custom colors.

- **Every color option of an effect** gets both routes — the Background's and the Borders' color
  today, any future one by the same rule. The ⚙ menu's OCR *Arrow color…* keeps the dialog only: it
  colors the file explorer, not the grid.
- The eyedropper reads the **cells of the preview**, nowhere else — not the file explorer's tiles,
  not the rest of the screen.

---

## Color Routes

| Route | Control | Does |
|---|---|---|
| Eyedropper (first) | The **eyedropper part** of the split Color… button | Arms the eyedropper over the preview |
| Windows dialog (second) | The **Color… part** of the same button, its swatch kept | Opens the standard color dialog, preselected on the color in use, fully expanded |

- **Split button**: one control, two parts side by side — an eyedropper pictogram, then the swatch and
  *Color…* as today — each part with its own hover and its own tooltip. One control class, reused by
  every color option (Background, Borders, any future one).
- The two routes end in the **same setter** as today: a color taken by either one is applied exactly
  as the dialog's OK applies it — the Background leaves its automatic mode (*Automatic color*
  unchecked), the Borders' color is remembered for the next start-up.
- Both parts are **disabled while exporting**, as the Color… buttons are, and an armed eyedropper is
  disarmed (cancelled) when an export starts.
- The Background's split button is disabled with no cell selected, as its options are; the Borders'
  stays enabled (RULES.md § Global Toolbar).

---

## Sample Size

- A **slider, 1 to 5 px**, right after the split button in the color option's line of the options
  toolbar, its value shown as `1 px`: the side of the square of preview pixels averaged by the
  eyedropper (1 = the pixel under the cursor; 3 = the 3 × 3 square centered on it).
- **One value per color option**: the Background's and the Borders' each have their own.
- **1 px at each launch**, never remembered (not in `settings.json`).
- An **eyedropper setting, not an effect option**: moving it activates nothing (the exception to
  RULES.md § Options Toolbar's "acting on any option activates the effect"), it is **out of the undo
  history**, and no *Reset* — the effect's, the toolbar's, *Clear all* — touches it. It stays enabled
  while its effect is off.
- The options toolbar keeps its height: the slider fits on the color line, beside the existing
  controls.

---

## Eyedropper Gesture

### Arming

- Clicking the eyedropper part **arms** it: the grid is **held** (`AnimationPlayer.Hold`, released when
  disarmed — the videos, the animations and the sound pause), then a **snapshot** of the preview's
  cells is taken: the cells **as composed**, the way the exports draw them (`Compositor`: image,
  background, effects, borders), at the preview's own size so a snapshot pixel is a preview pixel,
  without the helper indicators, the selection outline or any preview-only feedback.
- The eyedropper reads **that snapshot only**, until it is disarmed: the live preview of the color
  (below) never changes what is read.
- While armed, the preview's other gestures — move, wheel zoom, handles, separators, ✥ swap, ×, drops
  — are **suspended**: the eyedropper owns the preview's mouse. The split button's eyedropper part
  shows pressed.

### Hover

- Over a cell, the cursor is an **eyedropper**, and the color read (the average of the sample square)
  is previewed **two ways at once**:
  - **Applied live** to the option: the selected cell's background, or the borders, take it in the
    preview — not committed (§ Undo). At most every **33 ms**, the last color reached winning, and the
    cells **drawn fast** (bilinear) while armed, in full once it ends: the mouse moves far more often
    than a cell can be drawn in full.
  - A **loupe** beside the cursor: the snapshot's pixels around it enlarged, the **sample square
    outlined**, the color read as a swatch with its **hex value** (`#RRGGBB`). It is kept inside the
    preview, flipping to the other side of the cursor near an edge, **repainted on its own area only**.
    15 × 15 snapshot pixels, 8 logical px each, the sample square outlined in the helpers' green over
    their black halo, a dark caption below. Preview only, never in the exports.
  - The cursor is an **eyedropper** drawn with a white halo, its tip on the hot spot.
- **Transparent spots are ignored**: an empty cell, a gap between cells, a cell without background
  where the image does not cover it, a transparent pixel of a PNG — any sample square holding a pixel
  of alpha 0 in the snapshot. Nothing is read: the option shows its **original color** again, the
  loupe shows the pixels with `—` for the hex, the cursor is the *no* cursor, a click does nothing.
- Outside the cells (the preview's margins) the same: nothing read, the original color shown.
- A sample square holding **semi-transparent** pixels (an antialiased edge) is averaged weighted by their
  opacity, the result opaque.

### Taking

- A **left click on a cell** over a readable spot **takes** the color: the option gets it through its
  setter (§ Color Routes), the effect is turned on if it was off, and the eyedropper is **disarmed** —
  one pick per arming.
- A click on **another cell than the selected one** reads that cell; it **does not select it**: the
  Background taken applies to the selected cell, as the dialog does.
- The picked color is **added to the dialog's custom colors** (§ Windows Dialog).

### Cancelling

- **Escape** or a **right click**: disarmed, the option back to the color it had, nothing changed.
- The same when the eyedropper is disarmed by anything else: **any other press** — a click outside the
  preview, the eyedropper part clicked again, a middle click — or **any other key** (the modifiers
  aside), each **swallowed** with its release so nothing else acts on it; a press on the window's frame
  (cancelled, the window still moving); the window deactivated, resized; an image arriving or leaving;
  an export starting.

### Undo

- The armed state is a **gesture** (`GridPreview.InGesture`): the live previews never commit a step.
- A pick is **one step**; a cancel leaves **none** (RULES.md § Undo History).

---

## Windows Dialog

The same standard dialog, three changes — for every color option, the OCR arrow's included, since
they share it:

- **Opened fully expanded** (`FullOpen = true`): the spectrum and the RGB / HSL fields shown at once.
- **Custom colors remembered** between sessions: its 16 *custom colors* kept in `settings.json`
  (`UI/AppSettings.cs`, RULES.md § App Settings), loaded into the dialog at start-up, saved back after
  every closing of the dialog (they may be edited in it). A save failure says so in the status line;
  an unreadable value falls back to the empty slots.
- **Picked colors added**: each color taken with the eyedropper is written into the **next free
  slot** — a white one, the dialog's empty value; once the 16 are filled, the writing goes round them again from the first, never reordering
  them. The position of the next slot is remembered with the colors.

---

## Code Map

| Piece | Where |
|---|---|
| Split button | `UI/ColorSplitButton.cs` |
| Eyedropper icon (button and cursor) | `EffectIcons.Eyedropper` |
| Eyedropper, snapshot, loupe, message filter | `UI/GridPreview.Eyedropper.cs` (`GridPreview` made `partial`) |
| Fast drawing while armed | `Compositor.Draw(…, fast)`, `GridPreview.OnPaint` / `RedrawCell` |
| Background / Borders wiring, Sample sliders | `MainForm.ArmBackgroundEyedropper` / `ArmBordersEyedropper` / `ArmEyedropper` |
| Shared dialog, custom colors | `UI/SharedColorDialog.cs`, `AppSettings.CustomColors` / `CustomColorsNext` |

---

## Test Impact

The app has **no test project** (CONTRIBUTING.md: "check a change by hand in the running app"):
nothing testable is created or updated. The behaviours below are checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| The Background's eyedropper takes a cell's color, unchecks Automatic color, turns the Background on | — (manual check) | — |
| The Borders' eyedropper takes a cell's color, turns the Borders on, remembers the color at the next start-up | — (manual check) | — |
| Hover applies the color live and shows the loupe with its hex; the read does not change under a live preview | — (manual check) | — |
| The sample slider (1–5 px) averages the square; it activates nothing, is out of the undo history, 1 px at each launch, one per option | — (manual check) | — |
| The grid is paused while armed and plays again once disarmed | — (manual check) | — |
| An empty cell, a gap, a transparent pixel read nothing; the original color is shown again | — (manual check) | — |
| A click on another cell reads it without selecting it; the eyedropper is disarmed after one pick | — (manual check) | — |
| Escape, a right click and the other disarming events cancel, the color unchanged, no undo step | — (manual check) | — |
| One pick is one undo step; Ctrl+Z gives back the previous color | — (manual check) | — |
| The Color… part opens the dialog expanded, preselected on the color in use; its custom colors survive a restart | — (manual check) | — |
| A picked color lands in the next free custom slot, going round after 16 | — (manual check) | — |
| The eyedropper is disabled while exporting, and disarmed when an export starts | — (manual check) | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1.

- [x] ~~**Hover preview**: applied live to the option, or shown beside the cursor only?~~ → Both
- [x] ~~**Loupe**: a magnifier beside the cursor, or the eyedropper cursor only?~~ → A loupe, with the
      hex value
- [x] ~~**Sample size**: one pixel, or a small square's average?~~ → A slider, 1 to 5 px, in the
      effect's options, one per option, 1 px at each launch, not remembered; an eyedropper setting —
      activates nothing, out of the undo history, untouched by the Resets
- [x] ~~**Source**: the cell as composed, or the image alone?~~ → The cell as composed
- [x] ~~**Snapshot**: taken when armed, or live?~~ → Taken when armed
- [x] ~~**Playing contents**: held while armed?~~ → Yes, the grid paused
- [x] ~~**Transparent spots**: ignored, or read as shown?~~ → Ignored
- [x] ~~**One pick or several**?~~ → Disarmed after one click
- [x] ~~**Button form and placement**?~~ → A split button: the eyedropper part, then the Color… part
- [x] ~~**Keyboard shortcut**?~~ → None
- [x] ~~**OCR arrow color**: eyedropper too?~~ → No, the dialog only
- [x] ~~**Windows dialog**: anything to change?~~ → Opened fully expanded, custom colors remembered,
      picked colors added into the next free slot, going round after 16

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-21

Initial design from the request and the scoping answers: an eyedropper button before each color
option's Color… button, armed then clicked over the preview's cells, the Windows dialog kept as the
second route; both ending in the existing setters. Every color option today (Background, Borders) and
any future one. Twelve open questions listed on the preview, the sampling and the controls.

### Iteration 2 — 2026-10-21

Every open question answered (Q&A 5–23):

- **Preview**: the color applied live and a loupe with the hex value, both; read from a **snapshot**
  of the cells as composed, taken when armed, the grid **held** meanwhile.
- **Sampling**: a **1–5 px** slider per color option, 1 px at each launch, an eyedropper setting
  outside the effect rules (no activation, no undo step, no Reset). Transparent spots read nothing.
- **Gesture**: one pick per arming; a click on another cell reads it without selecting it.
- **Control**: the Color… button becomes a **split button** (eyedropper | Color…); no shortcut.
- **Scope**: the OCR arrow color keeps the dialog only.
- **Dialog**: opened expanded, its custom colors remembered, each pick written into the next free slot,
  going round after 16.
- Added by the design to close the gesture: the other disarming events (tab change, selection change,
  window deactivated, export) cancel like Escape.

### Iteration 3 — 2026-10-21 — ✅ Implemented

Go given: **code, unit tests and documentation**, in the **current checkout**. Branch Gate: the run
stays on `main`, the standing choice of this repository (every run lands on `main`).

### Iteration 4 — 2026-10-21 — Slowness reported during the run

The user, testing the first build: *it works, but it is very slow — why, can it be optimised?*

- **Why**: every mouse move (100+ a second) applied the color under the cursor at once, and each
  application drew the selected cell again **in full quality** (high-quality bicubic from the full-size
  image — a 4K wallpaper in the test), the whole grid for the Borders, synchronously; the whole preview
  was repainted for the loupe on top.
- **Fix**, within the run: the cells drawn **fast** while the eyedropper is armed and in full once at
  the end; the live color applied **at most every 33 ms**, the last one winning, the loupe and its hex
  following the cursor at once; only the loupe's area repainted; a pick no longer restores the option
  before taking the color (one drawing instead of two).

### Iteration 5 — 2026-10-21 — 🧭 Implementation choices

No rule broken. The choices the design left open:

- **Presses and keys while armed are swallowed**: a press anywhere but a left one over the preview, and
  any key but the modifiers, cancel **and** are eaten with their release, so a click on a button or a tab
  only cancels (the design said a click outside cancels; it did not say whether it also acts). A press
  on the window's frame cancels and goes on, so the window still moves. Done by a message filter
  installed while armed (`GridPreview.EyedropperFilter`).
- **A left click on a spot reading nothing** keeps the eyedropper armed.
- **Loupe**: 15 × 15 snapshot pixels at 8 logical px each, below right of the cursor, a dark caption
  with the swatch and `#RRGGBB` (upper case); the sample square outlined in the helpers' green over their
  halo (RULES.md § On-Cell Helper Indicators).
- **Cursor**: the eyedropper icon drawn with a white halo, its tip on the hot spot; the *no* cursor over
  a spot reading nothing.
- **Semi-transparent pixels** are averaged weighted by their opacity.
- **Free custom slot** = a white one, the dialog's empty value: a picked white fills a slot that still
  looks free.
- **Sample slider**: 70 px wide, its label `Sample: 1 px`, its tooltip saying it is not remembered.
- **Tooltips**: the Color… parts now say *in the color dialog*; the eyedropper parts say how to use them.
- **Crop edit view**: with the Crop tab selected while the Borders' eyedropper is armed, the selected cell
  shows its edit view (the uncropped image) while the loupe shows the composed, cropped cell — the
  snapshot is the truth.
- **RULES.md § Color Options** added, so a future color option follows the same rule.
- Checked by hand: the options row (split button, Sample) and the hover by screenshots; the pick, the
  live preview and the speed by the user's own test of the first build — the second build, faster, is
  not checked by the agent beyond its launch.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-21 | Dialog, split button, eyedropper, wiring; speed-up in Iteration 4 |
| Unit tests | 3 | 2026-10-21 | No test project: manual checks (the user's test, the agent's screenshots) |
| README | 3 | 2026-10-21 | README / README.fr § Picking a color, GLOSSARY / GLOSSARY.fr *Eyedropper*, RULES.md § Color Options |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which color options get the eyedropper and the Windows dialog? | All of them (Background + Borders), any future one by the same rule | 2026-10-21 |
| 2 | What does the eyedropper read ("the thumbnails")? | The cells of the preview | 2026-10-21 |
| 3 | How is the eyedropper used? | Button, then click: hover previews, click takes, Escape / right click cancels | 2026-10-21 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single exploration pass | 2026-10-21 |
| 5 | Hover preview: applied live to the option, or shown beside the cursor only? | Both: applied live and shown beside the cursor | 2026-10-21 |
| 6 | Loupe beside the cursor, or the eyedropper cursor only? | A loupe, with the hex value | 2026-10-21 |
| 7 | Sample size: one pixel, or a small square's average? | Adjustable from 1 to 5 px by a slider | 2026-10-21 |
| 8 | Source: the cell as composed, or the image alone? | The cell as composed | 2026-10-21 |
| 9 | Snapshot at arming time, or live reading? | Snapshot at arming time | 2026-10-21 |
| 10 | Playing contents held while armed? | Yes, the grid paused | 2026-10-21 |
| 11 | Transparent spots: ignored, or read as shown? | Ignored | 2026-10-21 |
| 12 | One pick, or armed until Escape? | Disarmed after one click | 2026-10-21 |
| 13 | Button form and placement? | A split button: eyedropper part, then the Color… dialog part | 2026-10-21 |
| 14 | Keyboard shortcut to arm it? | None | 2026-10-21 |
| 15 | OCR arrow color: eyedropper too? | No, the dialog only | 2026-10-21 |
| 16 | Windows dialog: anything to change? | Checked together: nothing changes, custom colors remembered, picked colors added, opened full — clarified in #18 | 2026-10-21 |
| 17 | Where does the sample size slider sit, and is it remembered? | In the effect's options, next to the eyedropper; remembered: see #19 | 2026-10-21 |
| 18 | Windows dialog: "nothing changes" was checked with the three changes — which holds? | The three changes | 2026-10-21 |
| 19 | Sample size value: shared by every eyedropper, remembered between sessions? | Not remembered, 1 px by default (shared or not: see #22) | 2026-10-21 |
| 20 | Sample size default value? | 1 px | 2026-10-21 |
| 21 | Does moving the sample size slider activate the effect, and is it in the undo history? | No: an eyedropper setting — activates nothing, out of the history, not reset | 2026-10-21 |
| 22 | Sample size: one value for every eyedropper, or one per color option? | One per color option | 2026-10-21 |
| 23 | Picked colors added to the dialog's custom colors: in which order, how many? | The next free slot, then going round the 16 without reordering | 2026-10-21 |

---

*Last updated: 2026-10-21*
