# Eyedropper Ergonomics

> Working document — every color option laid out the same way: the eyedropper as a Windows split
> button with its Sample in its ▾ menu, Color… attached to it, then the color's opacity at its right.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every color option — the Background's, the Borders', and the four colors of a text layer (text, outline,
box, shadow) — takes its color through a split button (`UI/ColorSplitButton`): the eyedropper part, then
the **Color…** part (origin: `workfiles/20261021-color-eyedropper.md`, RULES.md § Color Options).
Complaints and wishes:

- The eyedropper does not read as **attached** to Color….
- The **Sample** slider (1–5 px) and its `Sample: N px` label take room on every color line, for a
  setting that is almost never changed.
- The **opacity** of a color is a color matter: it belongs **at the right of the color choice**, for
  **every** color option — today only three have one, each placed its own way.

The work:

1. The eyedropper becomes **Windows' own split button** (`UI/SplitButton`, RULES.md § Icons): its icon,
   its ▾ built in; **Color…** touches it on its right.
2. The Sample slider and its label leave every options line; the size is chosen from the eyedropper's
   **▾ menu**, the choice arming the eyedropper at once.
3. Every color option gets an **opacity** right after Color…: moved for the Background, the Borders and
   the text's box, **new** for the text, its outline and its shadow.

The resulting shape of every color option:

```
 ┌──────┬─┐┌───────────┐
 │  💧  │▾││ ■ Color…  │  ◐ ───●──── Opacity: 80 %
 └──────┴─┘└───────────┘
```

Out of scope: the wider layout of the options toolbars (one feature per line, wrapping, homogeneous
sliders), handled by the parent session ("Ergonomie des outils dans les effets"). The opacity's shape
below follows today's Background one; if that session settles another shape for the sliders, it wins.

---

## Attached Split Button

Agreed with the user (Iteration 2, mockup C), made a Windows split button by RULES.md § Icons ("a button
opening a menu of variants is Windows' own split button … never a second button beside it"):

| Part | Look | Click | Tooltip |
|---|---|---|---|
| Eyedropper | A `SplitButton` (`BS_SPLITBUTTON`): the eyedropper icon (`EffectIcons.Eyedropper`, a kept drawing) and its built-in ▾; drawn **pressed** while armed | Main part: arms the eyedropper at the size in use, as today. ▾: opens the **sample menu** below the button | As today, with the size added: `Takes the borders' color from the preview (sample: 3 px): click a cell; Escape or a right click cancels` |
| Color… | A native button: the swatch of the color in use, then `Color…`, as today | Opens the standard color dialog, as today | As today |

- The two buttons **touch**: no gap between them, the Color… button overlapping the eyedropper's right
  border by 1 px so the two borders read as one line.
- The eyedropper button is as tall as Color…; its width is the system's ideal size (icon + arrow).
- It stays **where the split button is today** on each line (option B, moved next to its checkbox,
  rejected).
- **Pressed while armed**: the system split button has no checked state; it is held pressed with
  `BM_SETSTATE` while the eyedropper is armed, released on a pick or a cancel.
- `SplitButton` opens its menu **above** it today (Copy ▾ / Save ▾ sit at the bottom of the window); it
  gains the direction its owner wants — **below** for the eyedropper, the options toolbar being at the top.
- Disabled as a whole, as today, when its option is.
- `ColorSplitButton` keeps its role and its API for its owners: the eyedropper's pressed state, the
  Color… button's swatch, one click event per part, one tooltip per part, and now the `Sample` value.

---

## Sample Menu

Agreed with the user (parent session, scoping, Q&A 5–6):

- The **Sample** slider and its `Sample: N px` label are **removed** from every options line: the
  Background's color line, the Borders' line, and the four color lines of the Layers text options.
- The eyedropper's ▾ opens a menu of **five radio items**, `1 px` to `5 px`
  (`GridPreview.MinEyedropperSample` … `MaxEyedropperSample`), the size in use checked, under a disabled
  caption `Eyedropper sample`. No icons: they are values (RULES.md § Icons).
- **Choosing a size arms the eyedropper** at that size at once, as a click on its main part would
  (Q&A 5). Choosing while it is already armed for this option changes the size of the armed eyedropper.
- **Only the ▾ opens the menu**: a right click on the eyedropper does not (it cancels an armed one, as
  today) (Q&A 6).
- The size in use **shows in the tooltip and the menu's check only** — nothing drawn on the button
  (scoping answer).
- Unchanged from RULES.md § Color Options: **one value per color option**, **1 px at each launch**; it
  activates nothing **by itself** (arming does what it does today: a pick changes the color, a cancel
  nothing), is **out of the undo history**, and **no Reset touches it**.
- The value lives on the split button (`ColorSplitButton.Sample`), so `MainForm.ArmEyedropper` reads it
  from the button it arms — no separate control to pass along.

---

## Color Opacity

Agreed with the user (Iteration 2, Q&A 7–8): every color option has an opacity, **at the right of
Color…**.

| Color option | Today | After |
|---|---|---|
| Background | `BackgroundEffect.Opacity`, its icon, slider and label **before** the split button | Moved right after Color… — same value, same range (0–100 %) |
| Borders | `GridBorders.Opacity` (10–100 %), after Thickness, the **brackets only** — disabled for the line styles | Moved right after Color…; its reach on the line styles: open question |
| Text layer's box | `LayerTextStyle.BackgroundOpacity` (0–100 %), at the end of the box's line | Right after Color… (already there once the Sample leaves) |
| Text layer's text | — | **New**: the letters' opacity |
| Text layer's outline | — | **New**: the outline's opacity |
| Text layer's shadow | — | **New**: the shadow's opacity |

- **One shape** for every color opacity: today's Background one — the opacity icon
  (`EffectIcons.Opacity`), a slider, `Opacity: N %` — 0–100 % by 10 % steps, the Borders keeping their
  10 % floor (`GridBorders.MinOpacity`).
- An ordinary option: **acting on it turns its effect on** (and, for the text's outline, box and shadow,
  their own checkbox, as their color does today), it is in the **undo history**, the Resets bring back its
  default.
- **The color and its opacity stay apart**: a pick or the dialog's OK sets the color, never the opacity;
  the swatch stays opaque (shared RULES.md § Colour Items).
- **New text settings** (RULES.md § Text Styles): `LayerTextStyle` gains `Opacity`, `OutlineOpacity` and
  `ShadowOpacity`, 1 by default — every built-in style unchanged — each written by `ToJson` under a named
  key and read by `FromJson` (a missing or foreign value giving 1), and listed in `TextStyleStrip`'s
  tooltip. A text style saves them like the other settings.
- **Drawing** (`TextRaster`): the letters' brush, the outline's brush and the shadow's color take their
  alpha. How a translucent text and its outline overlap: open question.

---

## Code Map

| Piece | Where |
|---|---|
| The eyedropper as a `SplitButton`, its menu, the `Sample` value, Color… attached | `UI/ColorSplitButton.cs` |
| The menu's direction | `UI/SplitButton.cs` (`ShowMenu`, today `AboveRight`) |
| `SampleSlider()`, `SampleText`, Background / Borders samples and labels, their tooltips, the enable lists, the arming | `UI/MainForm.cs` (fields ~264–407, the color lines ~534 / ~619, the wiring ~947–1111, `ArmEyedropper`) |
| The Background's and the Borders' opacity moved after Color… | `UI/MainForm.cs` (`backgroundColorLine`, the Borders' `AddRange`, the Borders' enable rule ~5330) |
| Four text-layer samples and labels, their wiring, the enable list, `ArmLayerEyedropper`; the three new opacities | `UI/MainForm.Layers.cs` (`BuildLayerOptions`: `fontLine`, `outlineLine`, `backgroundLine`, `shadowLine`) |
| The three new text opacities, their JSON keys, their drawing | `Imaging/TextRaster.cs` (`LayerTextStyle`, `PaintText`, `Shadow`), `UI/TextStyleStrip` tooltip |
| Eyedropper icon re-made at the DPI | `MainForm.Layers.cs` and its Background / Borders counterpart |
| Rules | RULES.md § Color Options (the Sample in the menu, the opacity after Color…), § Text Styles unchanged (the new settings follow it) |
| Docs | README.md / README.fr.md § Picking a color, the Background fill line, the Borders, the text layer's settings; GLOSSARY.md / GLOSSARY.fr.md *Eyedropper* row, *Borders* row |

**Overlap with other sessions**: the text layer's lines in `MainForm.Layers.cs` `BuildLayerOptions` were
just reworked by the text-layer alignment and the icons work (both merged into `main`); this work edits
the same `AddRange` lines and enable list, on top of them. The parent session's options-toolbar layout
work touches every options line too: whichever lands second rebases its line edits.

---

## Test Impact

The app has **no test project** (`src/` holds the app only): nothing testable is created or updated.
The behaviour is checked by hand — the split button's look, its pressed state while armed and its menu at
100 % and 150 % DPI, an armed pick at 1 px and 5 px, the tooltips, each opacity in the preview and a PNG
export, a text style saved and applied with the new opacities.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Choosing a size in the ▾ menu: does it only set the size, or set it **and arm the eyedropper** at
  once?~~ → It sets the size and arms the eyedropper.
- [x] ~~A **right click on the eyedropper part**: does it open the same sample menu too?~~ → No: only the ▾.
- [x] ~~The **opacity at the right of the color choice**, generalised to every use of a color: designed in
  this workfile, or in a workfile of its own?~~ → In this workfile.
- [x] ~~Which color options get it: only those with an opacity today, moved, or **every** color option?~~
  → Every color option: moved for the Background, the Borders and the box, new for the text, its outline
  and its shadow.
- [ ] **The Borders' opacity on the line styles**: today it covers the corner brackets only, disabled for
  the line styles. Does it now also cover the outer frame and the inner lines (a translucent line letting
  the image under it, or the canvas in a gap, show through)? (Recommended: yes — every use of the color.)
- [ ] **A translucent text over its outline**: the outline is drawn under the letters; with the letters
  below 100 %, does the outline show through them, or is it cut out under the letters so only its outer
  part shows? (Recommended: cut out — the text reads as one color, the outline staying around it.)

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

### Iteration 3 — 2026-10-29

Answers to Q&A 5–8, and RULES.md § Icons, merged into `main` meanwhile (`workfiles/20261029-menu-icons.md`):

- **The eyedropper is a Windows split button** (`UI/SplitButton`): § Icons now forbids a second button
  beside a button opening a menu of variants — mockup C, built the system's way. `SplitButton` gains a
  menu direction (below), and the armed state is held with `BM_SETSTATE`.
- **Choosing a size arms the eyedropper** (Q&A 5); **only the ▾ opens the menu** (Q&A 6).
- **The color opacity is designed here** (Q&A 7), **for every color option** (Q&A 8): section *Color
  Opacity* added — the Background's, the Borders' and the box's moved after Color…, three new text
  settings (text, outline, shadow) following § Text Styles.
- Two questions raised by the generalisation: the Borders' opacity on the line styles, a translucent text
  over its outline.

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
| 1 | What does "a more tightly attached eyedropper" mean (mockups A one joined button, B A next to its checkbox, C two buttons touching)? | A — one joined button, where it is today *(revised: C, Iteration 2)* | 2026-10-29 |
| 2 | Does the sample size chosen in the ▾ menu show on the button? | Tooltip only | 2026-10-29 |
| 3 | Which sizes does the ▾ menu offer? | 1 to 5 px, as today | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-29 |
| 5 | Choosing a size in the ▾ menu: only set it, or set it and arm the eyedropper? | Set it and arm | 2026-10-29 |
| 6 | A right click on the eyedropper part: opens the sample menu too? | No, only the ▾ | 2026-10-29 |
| 7 | The opacity at the right of the color choice, for every color: designed in this workfile, or in its own? | In this workfile | 2026-10-29 |
| 8 | Which color options get an opacity at the right of Color…: those that have one today, moved, or every one? | Every color option | 2026-10-29 |
| 9 | The Borders' opacity: does it now cover the line styles' frame and inner lines too? | | |
| 10 | A translucent text: does its outline show through the letters, or is it cut out under them? | | |

---

*Last updated: 2026-10-29*
