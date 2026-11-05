# Slider Exact Value

> Working document — typing an exact value for an option slider: a click on its value turns it into a
> field where the number is typed in the unit shown.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every option slider of the app is a `StepSlider` (`UI/StepSlider.cs`, a `TrackBar`) beside a value label —
`Opacity: 50 %`, `Angle: +45°`, `Speed: ×1.5`. Reaching an exact value means aiming with the mouse, the
wheel or the arrow keys. The user wants to **type it**: a click on the value turns it into a small field,
the number typed in the unit shown.

Origin: the UI ergonomics review of the parent session *Ergonomie des outils dans les effets*, one
sub-session per subject.

**Agreed in the scoping batch (2026-11-05)**:

- **Opened by a click on the value**, a single click.
- **Edited in place**: the value turns into a text field at the same place, the caption and the unit kept
  around it; nothing around it moves.
- **Every slider** of both options toolbars — the cell effects', the Global one's, the text layers'
  settings — through **one mechanism**, never per slider.

What the code holds today (read in the exploration):

- `OptionSlider(min, max, largeChange, controlStep)` (`UI/MainForm.cs`) returns a bare `StepSlider`; it
  knows no label. Each slider has its own `Label` field (`_backgroundOpacityLabel`, `_blurIntensityLabel`…),
  `AutoSize`, holding **the caption and the value in one text** (`Opacity: 50 %`), set by the slider's
  `ValueChanged` and by `UpdateEffects`. No label carries two values; no label is shared.
- A slider's change goes `ValueChanged` → `ChangeLook(effect, …)` → `look.TurnOn(effect)` → the preview:
  setting `slider.Value` from code takes the same route, the effect activated with it.
- The undo history commits a step once the state has settled (~300 ms, no mouse button held, not
  `InGesture`): one value applied once is one step by itself.
- `UI/FrameField.cs` (the Frames' trim and starting point) is the app's typed-number pattern: digits only,
  **Enter** applies, **Escape** puts the value back, **focus loss** applies, out of range **clamped**,
  ↑ ↓ and the wheel step; `MainForm.ProcessCmdKey` leaves Ctrl+Z, Ctrl+V… to it while it is focused
  (`IsEditingFrameField`).

---

## The Gesture

- **A click on a slider's value label** opens its field. The label shows an I-beam cursor while hovered,
  and a tooltip says what the click does and the bounds (*open question 7*). What the click area is — the
  whole label or its number only — is *open question 1*.
- A **disabled** slider's label (Edges and Blend without an extending fill, Tolerance outside Plain, Speed
  while frozen, a Cutout amount without its treatment, the gradient Angle unless Linear…) opens nothing.
- **From the keyboard**: *open question 5*.
- **Opening the field changes nothing**: the effect is not turned on, no step is made. Only a value applied
  is an option acted on (§ Applying).

## The Field

- A **text box laid over the label's number**, in the label's font, the caption before it and the unit
  after it still showing: `Opacity: [50 ] %`. It **floats over the options row**, never inserted into its
  flow, so nothing moves while it is open. Its width fits the widest value of the slider's range.
- Opened with **its whole text selected**, so typing replaces it.
- **The number is read where it stands in the label's text** (a sign, digits, a decimal part), the caption
  being what comes before it and the unit what comes after — so every existing formatter (`OpacityText`,
  `SignedPercentText`, `HueText`, `CycleText`…) keeps making the label's text, and a new one works by itself.

## Reading What Is Typed

- **The unit shown** is what is typed: `45` for `Angle: +45°`, `1.5` for `Speed: ×1.5`, `0.5` for
  `Duration: 0.5 s` — never the slider's raw position (tenths, a log scale, a negated value).
- **Lenient**: `.` or `,` as the decimal separator; a leading `+` optional; the unit typed with the number
  (`50%`, `45°`, `×1.5`, `x1.5`, `1.5 s`) and spaces ignored.
- **Not a number** (empty, letters): refused — the value is put back, nothing applied.
- **Out of the range shown**: *open question 2* (clamped or refused; the angles wrapped or not).
- **Finer than the slider's grain**: brought to the nearest value the slider holds — a tenth of a second,
  Speed's 0.25 — but the **zoom**, applied exactly (*open question 6* for the Cutout's edges).

## Applying

- **Enter** or **focus loss** applies the value and closes the field; **Escape** closes it, the value put
  back (*open question 3*).
- Applied through **the slider's own route**: its value set from the typed one, so `ValueChanged` →
  `ChangeLook` turns the effect on and updates the preview, the label and the undo history exactly as a drag
  does (RULES.md § Options Toolbar: *acting on any option activates the effect*). The zoom goes through its
  exact path (`ApplyZoom`), the slider's thumb rounded as for an exact wheel zoom.
- **One undo step** per value applied: it is applied once, and the history commits it when it settles. No
  gesture flag is needed.
- **The same value** as the one shown applies nothing (no step, the effect not turned on).
- While the field is focused, `MainForm.ProcessCmdKey` leaves its editing keys to it — Ctrl+Z, Ctrl+Y,
  Ctrl+V, Ctrl+C, Delete, Escape — as for the Frames fields (`IsEditingSliderValue` beside
  `IsEditingFrameField`); Space already skips a text box.
- **↑ ↓ and the wheel in the field**: *open question 4*.

## The Sliders and Their Conversions

Every slider with a value label, read from the code. *Shown* is what the label shows and what is typed; the
conversion is from the shown number to the slider's value.

| Sliders | Shown | Range shown | Shown → slider | Note |
|---|---|---|---|---|
| Percent sliders: Background Opacity, Edges, Blend; Blur Intensity; Cutout Tolerance and its 4 amounts (Opacity, Color, Blur, Pixels); Volume; Soundtrack volume; Grid background Opacity; the fills' second Opacity (×2); Background image Opacity; text layer opacities (×4) | % | 0–100 (Volumes 0–200) | same | Blur Intensity is written `50%`, without the space the others have |
| Borders Opacity | % | 10–100 | same | |
| Seams Depth | % | 1–50 | same | |
| Text layer Outline width / Shadow size | % | 2–20 / 2–30 | same | |
| Colors: Saturation, Brightness, Contrast | % signed | −100–+100 | same | |
| Colors: Hue | ° signed | −180–+180 | same | Circular |
| Rotate: Angle | ° signed | −180–+180 | same | Circular; −180 and +180 are one look |
| Fills' gradient Angle (×2) | ° | 0–359 | same | Circular |
| Zoom; Background image Zoom | % | 10 – the app's max zoom (2000 by default) | 100 × log2(z / 100), dead zone ±4 | Applied **exactly** through `ApplyZoom`; the label shows the zoom in force, not the thumb |
| Animations: Back and forth | s | 1–30 | negated | |
| Fade Duration; Cascade Pause | s, 1 decimal | 0.1–5 / 0–3 | ×10 | |
| Borders Thickness | %, 1 decimal | 0.1–6 | ×10 | |
| Frames: Speed | ×, up to 2 decimals | 0.25–4 | ÷0.25 | Snapped to 0.25 (`FramesEffect` already rounds) |
| Cutout: Grow; Soften; Gaps | %, 1 decimal | −2–+2 / 0–2 / 0–2 | ÷0.02 | The slider's grain (0.02) is finer than the label shows (0.1): *open question 6* |

**Out of scope**: the Frames' starting point and trim sliders — they have no value label, their typed fields
(`FrameField`) exist already.

## Mechanism

One description per slider, one field for every one — never a field built per slider.

- **`SliderValue`** (new, `UI/`): what a slider's shown value is — from the slider's value to the number shown
  and back, its range shown, its decimals, circular or not, and an optional **exact apply** (the zoom's).
  Declared where the slider is built, beside its label. The plain percent sliders share one default
  description (shown = slider value), so most sliders declare nothing more than their label.
- **`ValueField`** (new, `UI/`): the floating text box. `ValueField.Attach(label, slider, sliderValue)` makes
  the label clickable (cursor, tooltip), opens the field over the number on a click, reads, converts,
  bounds and applies the value through the slider (or the exact apply), and closes. One instance shared by
  the whole window: one field open at a time.
- **A new slider** declares its `SliderValue` and attaches its label: RULES.md says so (§ Rules and Docs).
- **The options toolbar layout** (`workfiles/20261104-options-toolbar-layout.md`, designed in parallel) makes a
  **labeled slider** — the name and the value above a compact slider, as one control. The field attaches to
  whatever shows the value: today the combined label, then the labeled slider's value. *Open question 8*.

## Rules and Docs

- **RULES.md § Options Toolbar** gains a **Typed Values** part: every option slider's value takes a typed
  number through `ValueField` — the unit shown, lenient reading, the bounds (as answered), Enter / focus loss /
  Escape, one undo step, the effect activated as by any option; a new slider declares its `SliderValue`.
- **SHORTCUTS.md / SHORTCUTS.fr.md § Options**: a click on a slider's value; Enter, Escape (and ↑ ↓, the wheel,
  the opening key, as answered) in a slider's value field — in the same commit as the code.
- **GLOSSARY.md / GLOSSARY.fr.md**: *Value field* (*champ de valeur*).
- **README.md / README.fr.md**: only if they describe the sliders' gestures — they link to SHORTCUTS.md
  otherwise (§ One place per fact).
- **TODO-FEATURES.md**: no row matches this work.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*): no unit
test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| A click on the value of a percent slider, a signed one, a seconds one, Speed and the Zoom opens the field over the number, its text selected; nothing moves | The running app | Manual check |
| Typing `37`, `37 %`, `0,5`, `+45` applies the value in the unit shown; the label and the slider follow; the effect turns on | The running app | Manual check |
| A typed zoom (`137`) is applied exactly; a typed Speed (`1.3`) snaps to 1.25 | The running app | Manual check |
| Out of range, not a number, Escape, focus loss behave as answered | The running app | Manual check |
| One Ctrl+Z after a typed value undoes it whole; Ctrl+Z in the field undoes the typing only | The running app | Manual check |
| A disabled slider's value opens nothing; Global and text layer sliders work alike | The running app | Manual check |

---

## Open Questions

- [ ] 1. **The click area**: the whole label (`Opacity: 50 %`, caption included — a larger target), or the
  number only? *Recommended: the whole label today; with the layout's labeled slider, its name and its value.*
- [ ] 2. **Out of range**: clamped to the bounds (as the Frames and Format fields do), or refused (the value
  put back)? And the **angles** (Rotate, Hue, gradient Angle): wrapped round (`270°` → `−90°`), or clamped
  like the others? *Recommended: clamped; the angles wrapped.*
- [ ] 3. **Applying and closing**: Enter and focus loss apply and close the field, Escape closes it and puts
  the value back — or Enter applies and keeps the field open, as the Frames fields do? *Recommended: Enter
  applies and closes, the focus back on the slider.*
- [ ] 4. **↑ ↓ and the wheel in the field**: step the value by the slider's small step (Ctrl ×5), applied at
  once as in the Frames fields, or nothing (the slider is right beside it)? *Recommended: nothing — the
  slider does it already.*
- [ ] 5. **From the keyboard**: a key on a focused slider opens its field — F2, Enter, both — or none?
  *Recommended: F2 and Enter.*
- [ ] 6. **The Cutout's Grow, Soften and Gaps**: their slider holds 0.02 steps, their label shows 0.1 — five
  positions read the same. Show **2 decimals** (`1.04 %`), so a typed `0.05` shows as kept, or keep 1 decimal
  and round what is typed to the tenth? *Recommended: 2 decimals.*
- [ ] 7. **A tooltip on every value label** saying the click and the bounds — `Click to type a value (0 to
  100 %)` — or the I-beam cursor alone? *Recommended: the tooltip.*
- [ ] 8. **The overlap with the options toolbar layout**: this design attaches to the value's label, whatever
  its place; whichever run ships second adapts to the other. Leave a note in the shared notes folder and tell
  the *Disposition des barres d'options* session, or wait for its delivery before implementing this one?
  *Recommended: the note and the message; no waiting.*

---

## Design Iterations

### Iteration 1 — 2026-11-05

Initial design. Agreed in the scoping batch: opened by a click on the value, edited in place, every slider of
both options toolbars and the text layers, a two-pass exploration. Read from the code: labels hold the
caption and the value in one text, with no common pairing to their slider; a slider's value set from code
takes the drag's route (activation, undo); `FrameField` gives the keys' pattern; 33 sliders, most a plain
percent, a few with a conversion (zoom log2, negated cycle, tenths, Speed's 0.25, the Cutout's 0.02).
Proposed: one `SliderValue` description per slider and one floating `ValueField` reading the number in the
label's own text; the unit shown typed, read leniently; the zoom applied exactly. Eight open questions.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | No test project in the repository |
| RULES.md | | | |
| SHORTCUTS.md / .fr.md | | | |
| GLOSSARY.md / .fr.md | | | |
| README.md / .fr.md | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How is the field opened? | A click on the value | 2026-11-05 |
| 2 | What shape is the field? | Edited in place | 2026-11-05 |
| 3 | Which sliders? | Every one, cell effects, Global and text layers | 2026-11-05 |
| 4 | Depth of the exploration? | Tricky: scout, then deepen | 2026-11-05 |
| 5 | Open question 1 — the click area? | | |
| 6 | Open question 2 — out of range, and the angles? | | |
| 7 | Open question 3 — applying and closing? | | |
| 8 | Open question 4 — ↑ ↓ and the wheel in the field? | | |

---

*Last updated: 2026-11-05*
