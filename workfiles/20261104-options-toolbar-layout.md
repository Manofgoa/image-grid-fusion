# Options Toolbar Layout

> Working document — the options toolbars laid out as aligned feature lines, one feature per line, instead
> of a flow of editors.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The two options toolbars — the cell effects' (above the effects toolbar) and the Global one's (below the
Global toolbar) — show each tab's options as one `FlowLayoutPanel` row (`_options` / `_globalOptions` in
`UI/MainForm.cs`), its sub-lines grouped by hand per effect with `OptionLine()`: the Background on three
lines beside two thumbnail strips, the Cutout in two blocks, the Frames in three columns split by
separators, the Layers in four blocks (`BuildLayerOptions`, `UI/MainForm.Layers.cs`). The user finds it
reads as a flow of editors and wants it **aligned, one feature per line**.

Origin: the UI ergonomics review of the parent session *Ergonomie des outils dans les effets*, where three
layouts were shown as mockups and ranked by the user.

**Scope — pilot tabs**: the shared mechanism is built and applied to **three tabs**, chosen to cover both
toolbars and the hardest cases:

| Tab | Toolbar | Why it is a pilot |
|---|---|---|
| **Background** | Cell effects | The most crowded cell options: two thumbnail strips, two color options, the edges' sliders |
| **Frames** | Cell effects | Sliders with frame fields after them, a checkbox alone, columns split by separators today |
| **Layers** | Global | Tall blocks (the layer list, the text field) beside many lines of text settings |

Every other tab keeps its current layout until it is migrated (see § Rules and Backlog).

---

## The Three Layouts

Agreed with the user from the parent session's mockups, in order of preference: **C when it fits, else
A, else B**.

| Layout | What it is |
|---|---|
| **C — Feature lines** (first choice) | One line per feature. Its checkbox leads the line when the feature has one (`☑ Automatic color`), else its caption (`Fill`, `To`, `Edges`); then its controls — color split button, thumbnails, drop-downs, sliders. Each slider is compact, with its **name and value above it** (`Width          8 %`). Lines run **top-down**; when the toolbar's height is full, the next line starts a **new column** to the right |
| **A — Aligned grid in columns** | One feature per line too, the feature names **right-aligned in a column**, then the controls; a slider on **one row** — the slider, then its value. Denser in height than C, so more lines fit a column. When the height is full, a new column of lines to the right |
| **B — Ribbon groups** (last resort) | One **framed group** per feature, its name at the bottom, its controls **stacked** inside. The narrowest of the three |

### Agreed in the scoping batch (2026-11-04)

- **The slider's name and value above it** in C: the name at the left, the value at the right, over a
  compact slider.
- **Lines top-aligned** in the toolbar, never centered.
- **The toolbars' height is kept**: § Options Toolbar's rule stands — one height, the tallest options' —
  so nothing moves when another tab is selected. C fills that height with lines, then wraps into columns;
  the toolbar never grows for it — beyond the room kept for the scrollbar (§ Fit Rule).

### Heights

A tab laid out as feature lines asks for its tallest line or block **and room for two lines of sliders**, so C
stacks two slider lines in a column: a slider line is about **44 logical px** (its name over a 26-px slider), a
thumbnail line about **82** (a 56 × 40 box and its name). The cell options toolbar holds **two slider lines, or
one thumbnail line, per column** — about as tall as before, plus the scrollbar's room; the Global one keeps
the Layers' list height, about **206 px** of lines (§ Fit Rule).

---

## Feature Lines of the Pilot Tabs

One feature per line, agreed (open question 4):

### Background

| # | Leads | Controls |
|---|---|---|
| 1 | `Fill` | The fill thumbnails (Color, Corner pixel, Miter, Background corners) |
| 2 | `Gradient` | The kind thumbnails (Solid, Linear, Radial), the Angle slider |
| 3 | ☐ `Automatic color` | Eyedropper ▾, Color…, the opacity slider |
| 4 | `To` | The second color's drop-down, eyedropper ▾, Color…, its opacity slider |
| 5 | `Edges` | The Edges and Blend sliders, ☐ Soften |

### Frames

| # | Leads | Controls |
|---|---|---|
| 1 | `Trim from` | The From slider, its frame field |
| 2 | `Trim to` | The To slider, its frame field |
| 3 | `Starts at` | The starting point's slider, its frame field |
| 4 | ☐ `Freeze` | — |
| 5 | `Speed` | The Speed slider |

The two hand-made separators go: the columns of lines replace them. The trim's sliders have no caption — their
line's names them — and the starting point's and the speed's captions show their value alone.

### Layers

The **tall blocks** first, each a column of its own, the toolbar's full height: the layer list over its
icon buttons, then the text field. Then the lines:

| # | Leads | Controls |
|---|---|---|
| 1 | `Style` | The text styles' strip, as wide as its column, scrolled sideways past it (Save style… stays in its menu) |
| 2 | `Font` | The font, the emoji font, Bold, Italic, ALL CAPS |
| 3 | `Align` | The four alignments, Box |
| 4 | `Color` | Eyedropper ▾, Color…, the opacity slider |
| 5 | ☐ `Outline` | Eyedropper ▾, Color…, the opacity slider, the Width slider |
| 6 | ☐ `Background` | Eyedropper ▾, Color…, the opacity slider |
| 7 | ☐ `Shadow` | Eyedropper ▾, Color…, the opacity slider |
| 8 | `Shadow shape` | Halo / Drop, the Size slider |

---

## Fit Rule

- **Decided once per tab**, at design time: each migrated tab has **one layout**, C, A or B, fixed in its
  code — never recomputed when the window is resized. It is the first of C, A, B whose columns of lines fit
  the toolbar's width with the window **1920 px wide** (maximized on a Full HD screen), the Reset button
  aside — about 1 813 logical px for the lines. Measured: **Background in C** (1 799 px), **Frames in C**
  (871 px), **Layers in A** (1 694 px; 1 890 px in C, wider than the toolbar).
- **A layout wider than the window** is scrolled sideways by a **horizontal scrollbar** under the options,
  shown while they do not fit. Nothing is clipped in a migrated tab.
- **Room is kept for the scrollbar** at the bottom of **both** options toolbars, in every tab, whether it
  shows or not: each toolbar is taller by a scrollbar's height (`SystemInformation.HorizontalScrollBarHeight`,
  ≈ 17 px), so nothing moves when it appears or when another tab is selected. A tab not migrated yet keeps
  its `FlowLayoutPanel`, clipped at the right as today, the room left empty under it.
- **In a column of lines**, the leading elements — checkboxes and captions — share **one width**, so the
  controls of its lines start aligned; a **thin vertical line** separates two columns (and a tall block from
  the next column).

## Mechanism

One description of a tab's options, laid out three ways — never three hand-built layouts per tab.

- **A tab's options are a list of feature lines** (a `FeatureLine`: its leading checkbox or caption, its
  controls) plus, ahead of them, its **tall blocks** (the Layers' list and text field). Built once per tab
  in place of today's `OptionLine()` nesting, with the layout chosen for the tab (§ Fit Rule).
- **A labeled slider** is one control: the slider, its name, its value — drawn as C wants it (name and
  value above) or as A wants it (the value after it, the name in the label column). **One compact width**,
  ≈ 120 logical px, for every slider of a migrated tab — the Frames' trim and starting-point sliders aside,
  ≈ 200, to aim at a frame. A slider with an icon (the opacity's) keeps it **at the left of the slider**, as
  today, its name and value above the slider only.
- **One layout panel** per options row, replacing the pilot tabs' `FlowLayoutPanel`: it arranges the lines
  in the tab's layout — C, A or B — within the toolbar's height, scrolled sideways when wider than the
  window (§ Fit Rule), the controls themselves never rebuilt — the events, tooltips and state updates of today's code keep their controls.
- **The height** stays computed by `FitEffectRows()` from every tab's preferred height, plus the
  scrollbar's room; a pilot tab's preferred height is that of its tallest line or block — never more.
- The other tabs keep their `FlowLayoutPanel` until they are migrated.
- **In the code** (`UI/FeatureLines.cs`): `FeatureLinesPanel` (`AddBlock`, `AddLine`, the scrollbar, the room
  `ScrollRoom`), `SliderBox` (a slider, its caption, its icon), `SliderCaption` (a `Label` drawing `Name: value`
  split above its slider; `Caption` replacing the name), `OptionLayout` (Lines, Grid, Groups). A tab's layout is
  chosen where its panel is created, in `MainForm`'s `_options` / `_globalOptions`.

---

## Rules and Backlog

- **RULES.md § Options Toolbar** gains a **Feature Lines** part, written now for every tab: one feature per
  line, C then A then B fixed per tab at 1920 px, the leading elements' shared width and the columns' line,
  the scrollbar and its kept room, the labeled slider's shape — the rule every migrated tab follows, the
  pilots first; its *one height* sentence says the scrollbar's room is part of it. § Global Toolbar points
  to it.
- **RULES.md § Color Options** — the opacity's shape (*the opacity icon, a slider, `Opacity: N %`*)
  becomes, in a migrated tab, the icon at the left of the slider, `Opacity` and `N %` above it.
- **GLOSSARY.md / GLOSSARY.fr.md**: *Feature line* (*ligne de fonctionnalité*).
- **TODO-FEATURES.md**: no existing row matches this work. A row for the **tabs left to migrate**, and the
  Global toolbar's choices that are not option thumbnails yet (the Borders' style drop-down, the Fade's
  curve radio buttons) — added by the run.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*): no
unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| Background, Frames and Layers show one feature per line, top-aligned, in their fixed layout, the window maximized at 1920 px | The running app | Manual check |
| Narrowing the window shows the scrollbar under a pilot tab and scrolls it; the layout does not change | The running app | Manual check |
| Each toolbar is taller by the scrollbar's room only; switching tabs and the scrollbar appearing move nothing | The running app | Manual check |
| Every pilot control still works: activates its effect, undo, Resets, tooltips, eyedroppers | The running app | Manual check |
| The other tabs look as before | The running app | Manual check |

---

## Open Questions

- [x] ~~1. **The fit rule**: when is C, A or B chosen — measured per tab at every resize of the window, or
  decided once per tab at design time?~~ → Decided once per tab, at a reference width (§ Fit Rule)
- [x] ~~2. **When B does not fit either** (a narrow window — the minimum is 480 px wide): the options clipped
  at the right as today, or a horizontal scrollbar?~~ → A horizontal scrollbar
- [x] ~~3. **A line's leading element and the columns**: do the leading elements of a column share one width?
  A thin line between two columns of lines?~~ → Both
- [x] ~~4. **The feature lines of the pilot tabs** as listed in § Feature Lines of the Pilot Tabs.~~ → As listed;
  the Frames' hand-made separators go
- [x] ~~5. **The labeled slider**: one compact width for every slider (≈ 120 px; the Frames' frame sliders
  wider), the opacity icon before its name above the slider (§ Color Options updated)?~~ → One width but the
  Frames' frame sliders; the opacity icon stays at the left of the slider
- [x] ~~6. **Rules and backlog**: RULES.md § Options Toolbar › Feature Lines written now for every tab, and a
  TODO-FEATURES row for the tabs left to migrate and the Global choices that are not thumbnails yet?~~ → Both
- [x] ~~7. **The reference width** at which each tab's layout is decided?~~ → 1920 px, the window maximized
- [x] ~~8. **The scrollbar's place**: room kept for it under the options in every tab, the toolbar taller by
  it; the toolbar growing only while it shows; or scroll arrows at the ends instead?~~ → Room kept in every
  tab, both toolbars taller by it

---

## Design Iterations

### Iteration 1 — 2026-11-04

Initial design. Agreed before exploration: the layouts C, A, B in that order (parent session), the slider's
name and value above it, lines top-aligned, the toolbars' height kept, three pilot tabs (Background,
Frames, Layers). Proposed: the feature lines of each pilot, one description per tab laid out three ways
by one panel. Read from the code: with the height kept, C holds about two slider lines per column in the
cell toolbar, so it wraps into columns.

### Iteration 2 — 2026-11-04

Open questions 1 to 4 answered: each tab's layout is **fixed at design time** (not measured at every
resize); a layout wider than the window gets a **horizontal scrollbar**; the leading elements of a column
share one width and columns are split by a thin line; the pilot tabs' lines are agreed as listed. New § Fit
Rule. Two questions follow from them: the reference width (7) and the scrollbar's place in a fixed height (8).

### Iteration 3 — 2026-11-04

Open questions 5 to 8 answered: the layouts are fixed at a **1920-px** window; the **room for the scrollbar
is kept** at the bottom of both toolbars, in every tab, so they grow by its height once and never move
afterwards; every slider of a migrated tab has **one compact width**, the Frames' frame sliders aside, the
opacity icon **kept at the left of the slider**; RULES.md § Options Toolbar › Feature Lines is written now,
and a TODO-FEATURES row keeps the tabs left to migrate. Every open question is answered.

### Iteration 4 — 2026-11-04 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/options-toolbar-layout`,
branch `feature/options-toolbar-layout`). Scope frozen as the sections above stand.

### Iteration 5 — 2026-11-05 — 🧭 Implementation choices

No project rule broken. The choices the frozen design left open:

- **Layers fixed in A**: in C its lines are 1 890 logical px wide, more than the ≈ 1 813 px the toolbar leaves
  with a 1920-px window; A is 1 694 px. Background (1 799 px) and Frames (871 px) fit in C. Measured on a 4K screen
  at 125 %, the device widths divided by 1.25.
- **Room for two lines of sliders** in a tab's preferred height: with the tallest line alone, the cell toolbar
  shrank to one slider line a column and the Background overflowed 1920 px. The toolbar keeps about the height it had
  (plus the scrollbar's room). A tab's height is computed again when its controls are scaled to the DPI
  (`FeatureLinesPanel.PreferredHeightChanged`).
- **Hidden controls keep their room** (the starting point's field and value, one shown at a time): the layout never
  reads `Visible`, so nothing moves when one shows.
- **Captions**: the trim's sliders have none, their leads renamed *Trim from* / *Trim to*; the starting point's and
  the speed's show their value alone (`SliderCaption.Caption = ""`); the edges' opacity reads *Opacity*, under its
  line's *Edges*.
- **The trim's enabling and tooltip** go to its controls one by one — no container holds them any more — the tooltip
  on its two leads.
- **The Background's second color**: `FillControls.SecondControls` lists its controls, enabled for a gradient
  wherever they are laid (its line here, `SecondLine` in the Grid background).
- **A control anchored at the right fills its column** (the text styles' strip); a block anchored at the bottom
  takes the toolbar's height (the text field).
- **No wheel scrolling** of the lines: the scrollbar only, the sliders keeping the wheel.
- **Style line**: no *Save style…* button exists — it stays in the strip's menu — so the line holds the strip alone.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-11-05 | `UI/FeatureLines.cs`; Background, Frames, Layers migrated; the scrollbar's room in `FitEffectRows` |
| Unit tests | — | — | No test project in the repository; checked by hand in the running app |
| RULES.md | 5 | 2026-11-05 | § Options Toolbar › Feature Lines, § Color Options' opacity, § Global Toolbar |
| GLOSSARY.md / .fr.md | 5 | 2026-11-05 | *Feature line* / *Ligne de fonctionnalité* |
| README.md / .fr.md | — | — | Not applicable: the READMEs do not describe the options toolbars |
| TODO-FEATURES.md | 5 | 2026-11-05 | Row *Feature lines for the other tabs* |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Slider's name and value: above it, or after it as today? | Above it | 2026-11-04 |
| 2 | Lines in the toolbar's height: top-aligned or centered; height kept or grown? | Top-aligned, height kept | 2026-11-04 |
| 3 | Scope: every tab at once, cell effects first, or a few pilot tabs? | A few pilot tabs (Background, Frames, Layers) | 2026-11-04 |
| 4 | Depth of the exploration? | Straightforward: one pass | 2026-11-04 |
| 5 | Open question 1 — the fit rule: measured at every resize, or decided per tab? | Decided per tab | 2026-11-04 |
| 6 | Open question 2 — when B does not fit: clipped or scrolled? | A horizontal scrollbar | 2026-11-04 |
| 7 | Open question 3 — leading elements of a column one width; a line between columns? | Both | 2026-11-04 |
| 8 | Open question 4 — the pilot tabs' feature lines as listed? | Yes, as listed | 2026-11-04 |
| 9 | Open question 5 — the labeled slider's width and the opacity icon? | One width (Frames' wider); icon at the left of the slider | 2026-11-04 |
| 10 | Open question 6 — RULES.md Feature Lines now, and a backlog row for the rest? | Both | 2026-11-04 |
| 11 | Open question 7 — the reference width? | 1920 px, maximized | 2026-11-04 |
| 12 | Open question 8 — the scrollbar's place in the fixed height? | Room kept in every tab | 2026-11-04 |

---

*Last updated: 2026-11-05*
