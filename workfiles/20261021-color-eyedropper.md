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

The task adds an **eyedropper**, the first route: a button next to each color option arms it, the
color is then read from the **cells of the preview** under the cursor, a click takes it. The Windows
dialog stays, as the second route.

Agreed at scoping (Q&A 1–4):

- **Every color option** gets both routes — the Background's and the Borders' color today, any future
  color option by the same rule.
- The eyedropper reads the **cells of the preview**, nowhere else (not the file explorer's tiles, not
  the rest of the screen).
- The gesture is **button, then click**: the button arms it; hovering previews the color; a click
  takes it; Escape or a right click cancels.

---

## Color Routes

| Route | Control | Does |
|---|---|---|
| Eyedropper (first) | A button next to the color option's **Color…** button | Arms the eyedropper over the preview |
| Windows dialog (second) | The existing **Color…** button | Opens the standard color dialog, preselected on the color in use — unchanged |

- The eyedropper button sits **before** the Color… button, so it is the first route read.
  *(Form and placement: Open Question.)*
- The two routes end in the **same setter** as today: a color picked by either one is applied exactly
  as the dialog's OK applies it — the Background leaves its automatic mode, the Borders' color is
  remembered for the next start-up.
- Both are **disabled while exporting**, as the Color… buttons are, and the eyedropper is disarmed when
  an export starts.

---

## Eyedropper Gesture

- **Armed** by its button: the preview's cursor becomes an eyedropper over the cells.
- **Hover**: the color under the cursor is previewed *(how: Open Question)*.
- **Left click on a cell**: takes the color — the option gets it, the effect is turned on if it was off
  (RULES.md § Options Toolbar: acting on any option activates the effect).
- A click on **another cell than the selected one** reads that cell's color; it **does not select it**:
  the Background picked applies to the selected cell, as the dialog does.
- **Escape, a right click**: cancels — the option keeps the color it had, nothing changes.
- While armed, the preview's other gestures (move, wheel zoom, handles, separators, ✥ swap, drops of
  the ×) are **suspended**: the eyedropper owns the preview's mouse.
- **Undo history**: the armed state is a gesture (`GridPreview.InGesture`), so the hover previews never
  commit a step; a pick is **one step**, a cancel none (RULES.md § Undo History).

---

## Sampling

- What is read: the **cell as composed**, as the exports draw it (`Compositor`), without the helper
  indicators, the selection outline or any preview-only feedback *(exact source and transparency:
  Open Questions)*.
- The reading has no alpha: the option's own opacity stays as set (the Background's Opacity, the
  Borders' opacity).

---

## Test Impact

The app has **no test project** (CONTRIBUTING.md: "check a change by hand in the running app"):
nothing testable is created or updated. The behaviours below are checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| The Background's eyedropper takes a cell's color, unchecks Automatic color, turns the Background on | — (manual check) | — |
| The Borders' eyedropper takes a cell's color, turns the Borders on, remembers the color at the next start-up | — (manual check) | — |
| A click on another cell reads it without selecting it | — (manual check) | — |
| Escape and a right click cancel, the color unchanged, no undo step | — (manual check) | — |
| One pick is one undo step; Ctrl+Z gives back the previous color | — (manual check) | — |
| The Color… buttons still open the Windows dialog, preselected on the color in use | — (manual check) | — |
| The eyedropper is disabled while exporting, and disarmed when an export starts | — (manual check) | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1.

- [ ] **Hover preview**: while hovering, is the color applied live to the option (the cell or the
      borders show the result, given back on a cancel), or only shown beside the cursor until the
      click?
- [ ] **Loupe**: is a magnifier drawn beside the cursor (the pixels around it enlarged, the one read
      outlined, its hex value), or just the eyedropper cursor?
- [ ] **Sample size**: one pixel, or the average of a small square (3 × 3, 5 × 5 pixels of the cell)?
- [ ] **Source**: the cell as composed — background, effects, borders included, as the export draws it
      — or the image alone (its pixels as drawn, without the background or the borders)?
- [ ] **Snapshot**: is the grid read from a snapshot taken when the eyedropper is armed (stable under
      a live preview, nothing fed back), or live as it plays?
- [ ] **Playing contents**: are the videos and animations held (the grid paused) while the eyedropper
      is armed, so the color under the cursor stays put?
- [ ] **Transparent spots**: an empty cell, a cell without fill, a gap between cells, a transparent
      pixel — ignored (nothing read, the cursor saying so), or read as what the preview shows
      (checkerboard, white)?
- [ ] **One pick or several**: is the eyedropper disarmed after a click, or does it stay armed for
      another pick until Escape?
- [ ] **Button form and placement**: an icon button before Color… (💧-like pictogram), a split part of
      the Color… button, or another place?
- [ ] **Keyboard shortcut**: is the eyedropper armed by a key as well (e.g. `I`, as in image editors),
      while a color option's tab is shown?
- [ ] **OCR arrow color**: the ⚙ menu's *Arrow color…* is a color option too — does it get the
      eyedropper (read from the preview's cells), or stay with the dialog only?
- [ ] **Windows dialog**: anything to change in it — its custom colors remembered between sessions,
      the picked colors added to them, opened fully expanded (`FullOpen`)?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project (manual checks) |
| README | | | |

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
| 14 | Keyboard shortcut to arm it? | | |
| 15 | OCR arrow color: eyedropper too? | No, the dialog only | 2026-10-21 |
| 16 | Windows dialog: anything to change? | Checked together: nothing changes, custom colors remembered, picked colors added, opened full — clarified in #18 | 2026-10-21 |
| 17 | Where does the sample size slider sit, and is it remembered? | In the effect's options, next to the eyedropper; remembered: see #19 | 2026-10-21 |
| 18 | Windows dialog: "nothing changes" was checked with the three changes — which holds? | | |
| 19 | Sample size value: shared by every eyedropper, remembered between sessions? | | |
| 20 | Sample size default value? | | |
| 21 | Does moving the sample size slider activate the effect, and is it in the undo history? | | |

---

*Last updated: 2026-10-21*
