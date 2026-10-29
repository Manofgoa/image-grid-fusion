# Eyedropper Ergonomics

> Working document — the color options' split button made one joined control, and the eyedropper's
> Sample moved from a slider on the options line into a ▾ menu at the right of the eyedropper.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every color option of an effect — the Background's, the Borders', and the four colors of a text layer
(text, outline, box, shadow) — takes its color through a split button (`UI/ColorSplitButton`): the
eyedropper part, then the **Color…** part (origin: `workfiles/20261021-color-eyedropper.md`, RULES.md
§ Color Options). Two ergonomic complaints:

- The eyedropper does not read as **one control** with Color…: today they are two native buttons side by
  side, each with its own border.
- The **Sample** slider (1–5 px) and its `Sample: N px` label take room on every color line, for a
  setting that is almost never changed.

The work:

1. The split button becomes **one joined control**: one frame, three parts — the eyedropper, a **▾**,
   then Color… with its swatch — separated by thin lines inside the frame.
2. The Sample slider and its label leave every options line; the size is chosen from the **▾ menu**.

Scope: the color split button and its Sample only. The wider layout of the options toolbars (one feature
per line, wrapping, homogeneous sliders) is another subject, handled by its own session.

---

## Attached Split Button

Agreed with the user (Iteration 2, mockup C — replacing mockup A of Iteration 1):

```
 ┌──────┐┌───────────┐
 │ 💧 ▾ ││ ■ Color…  │
 └──────┘└───────────┘
```

| Part | Look | Click | Tooltip |
|---|---|---|---|
| Eyedropper button | A native button: the eyedropper icon (`EffectIcons.Eyedropper`), then a small **▾** on its right; drawn **pressed** while armed | On the icon: arms the eyedropper, as today. On the ▾: opens the **sample menu** below the button | As today, with the sample size added: `Takes the borders' color from the preview (sample: 3 px): click a cell; Escape or a right click cancels` |
| Color… button | A native button: the swatch of the color in use, then `Color…`, as today | Opens the standard color dialog, as today | As today |

- **Two native buttons kept**, as today, but **touching**: no gap between them (the second overlapping
  the first's right border by 1 px, so the two borders read as one line).
- The eyedropper button grows by the ▾'s width; it stays as tall as the Color… button.
- It stays **where the split button is today** on each line (option B, moved next to its checkbox,
  rejected).
- Disabled as a whole, as today, when its option is.
- It keeps its API for its owners: the eyedropper's pressed state, the Color… button's swatch, one click
  event per part, one tooltip per part.

---

## Sample Menu

Agreed with the user (parent session, then scoping):

- The **Sample** slider and its `Sample: N px` label are **removed** from every options line: the
  Background's color line, the Borders' line, and the four color lines of the Layers text options.
- The ▾ part opens a menu of **five radio items**, `1 px` to `5 px`
  (`GridPreview.MinEyedropperSample` … `MaxEyedropperSample`), the size in use checked, under a disabled
  caption `Eyedropper sample`.
- The size in use **shows in the tooltips only** (the eyedropper's and the ▾'s) and as the menu's check —
  **nothing drawn on the button** (scoping answer).
- Unchanged from RULES.md § Color Options: **one value per color option**, **1 px at each launch**; it
  **activates nothing**, is **out of the undo history**, and **no Reset touches it**.
- The value lives on the split button itself (a `Sample` property), so `MainForm.ArmEyedropper` reads it
  from the button it arms — no separate control to pass along.

---

## Code Map

| Piece | Where |
|---|---|
| Joined control, its three parts, the ▾ menu, the `Sample` value | `UI/ColorSplitButton.cs` |
| `SampleSlider()`, `SampleText`, Background / Borders samples and labels, their tooltips, the enable lists | `UI/MainForm.cs` (~264–368 fields, 505 / 564 lines, 947 wiring, 3422 helper, 3482 / 3508 / 3529 arming) |
| Four text-layer samples and labels, their wiring, the enable list, `ArmLayerEyedropper` | `UI/MainForm.Layers.cs` (52–72, 129–142, 267–283, 443–446, 750) |
| Eyedropper icon re-made at the DPI | `MainForm.Layers.cs` ~397 and its Background / Borders counterpart |
| Rule | RULES.md § Color Options (the Sample bullet reworded for the menu) |
| Docs | README.md / README.fr.md § Picking a color, the Background fill line (~125), the Borders (~251); GLOSSARY.md / GLOSSARY.fr.md *Eyedropper* row |

**Overlap with other sessions**: `workfiles/20261029-text-layer-alignment.md` (designed, not implemented)
adds options to the text layer's lines in `MainForm.Layers.cs` `BuildLayerOptions` — the same `AddRange`
lines (129–142) and enable list (443–446) this work edits. Whichever lands second rebases its line edits
on the other's; no design conflict.

---

## Test Impact

The app has **no test project** (`src/` holds the app only): nothing testable is created or updated.
The behaviour is checked by hand — the joined control's look and hover at 100 % and 150 % DPI, the ▾
menu, an armed pick at 1 px and 5 px, the tooltips.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] Choosing a size in the ▾ menu: does it only set the size, or set it **and arm the eyedropper** at
  once? (Recommended: set it only — the click on 💧 stays the one way to arm.)
- [ ] A **right click on the eyedropper part**: does it open the same sample menu too? (Recommended: no —
  the ▾ is the one way, and a right click cancels an armed eyedropper today.)
- [ ] The **opacity at the right of the color choice**, generalised to every use of a color: designed in
  this workfile, or in a workfile of its own?
- [ ] Which color options get it: only those with an opacity today — Background (`Opacity`), Borders
  (the brackets' `Opacity`), the text layer's box — moved to the right of Color…, or **every** color
  option, adding one to the text, its outline and its shadow (a new rendering setting, not only a move)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Initial design, from the parent session's agreed points and the scoping batch:

- The split button becomes one joined control — eyedropper, ▾, Color… — in one native-looking frame,
  where it stands today (mockup A; B, moved next to its checkbox, and C, two buttons kept, rejected).
- The Sample slider and label leave the six color lines; a ▾ menu of 1–5 px radio items replaces them,
  the size shown in the tooltips and the menu's check only.
- The Sample keeps its RULES.md exception (per option, 1 px at launch, activates nothing, no undo, no
  Reset), reworded for the menu.

### Iteration 2 — 2026-10-29

User feedback on the mockups:

- **Mockup C rather than A**: the two native buttons are kept, touching, the ▾ inside the eyedropper
  button at the right of its icon (section *Attached Split Button* rewritten).
- **Opacity is a color matter**: the user sees the opacity of a color as a generalisable subject — it
  should sit **at the right of the color choice**, and that placement should then apply to **every use
  of a color**. Its scope (this workfile or another, which color options get one) is asked — Q&A 7–8.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project |
| README | | | README.md / README.fr.md, GLOSSARY.md / GLOSSARY.fr.md, RULES.md § Color Options |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does "a more tightly attached eyedropper" mean (mockups A one joined button, B A next to its checkbox, C two buttons touching)? | A — one joined button, where it is today | 2026-10-29 |
| 2 | Does the sample size chosen in the ▾ menu show on the button? | Tooltip only | 2026-10-29 |
| 3 | Which sizes does the ▾ menu offer? | 1 to 5 px, as today | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-29 |
| 5 | Choosing a size in the ▾ menu: only set it, or set it and arm the eyedropper? | | |
| 6 | A right click on the eyedropper part: opens the sample menu too? | | |
| 7 | The opacity at the right of the color choice, for every color: designed in this workfile, or in its own? | | |
| 8 | Which color options get an opacity at the right of Color…: those that have one today, moved, or every one? | | |

---

*Last updated: 2026-10-29*
