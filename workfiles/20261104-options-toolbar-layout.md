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
  the toolbar never grows for it.

### Estimated Heights (to be measured in the run)

Read from the code, not measured: the cell options toolbar is about **100 logical px** inside its padding
(the Background's three lines of 26-px sliders), the Global one about **175** (the Layers' list of four
34-px rows over its icon buttons). A C line holding a slider is about **44 px** (a 15-px name over a 26-px
slider), a thumbnail strip about **62 px** (a 56 × 40 box and its name). So in the cell toolbar, C holds
**about two slider lines, or one thumbnail line, per column**: C there is mostly a row of columns of one or
two lines — what the mockup of the parent session called C, wrapped (open question 1).

---

## Feature Lines of the Pilot Tabs

Proposed split, one feature per line (open question 4):

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

The two hand-made separators go: the columns of lines replace them.

### Layers

The **tall blocks** first, each a column of its own, the toolbar's full height: the layer list over its
icon buttons, then the text field. Then the lines:

| # | Leads | Controls |
|---|---|---|
| 1 | `Style` | The text styles' strip (scrolled sideways as today), Save style… |
| 2 | `Font` | The font, the emoji font, Bold, Italic, ALL CAPS |
| 3 | `Align` | The four alignments, Box |
| 4 | `Color` | Eyedropper ▾, Color…, the opacity slider |
| 5 | ☐ `Outline` | Eyedropper ▾, Color…, the opacity slider, the Width slider |
| 6 | ☐ `Background` | Eyedropper ▾, Color…, the opacity slider |
| 7 | ☐ `Shadow` | Eyedropper ▾, Color…, the opacity slider |
| 8 | `Shadow shape` | Halo / Drop, the Size slider |

---

## Mechanism

One description of a tab's options, laid out three ways — never three hand-built layouts per tab.

- **A tab's options are a list of feature lines** (a `FeatureLine`: its leading checkbox or caption, its
  controls) plus, ahead of them, its **tall blocks** (the Layers' list and text field). Built once per tab
  in place of today's `OptionLine()` nesting.
- **A labeled slider** is one control: the slider, its name, its value — drawn as C wants it (name and
  value above) or as A wants it (the value after it, the name in the label column).
- **One layout panel** per options row, replacing the pilot tabs' `FlowLayoutPanel`: it arranges the lines
  in C, A or B within the toolbar's height (open questions 1 and 2), the controls themselves never
  rebuilt — the events, tooltips and state updates of today's code keep their controls.
- **The height** stays computed by `FitEffectRows()` from every tab's preferred height; a pilot tab's
  preferred height is that of its tallest line or block — never more — so the toolbars' heights do not
  change.
- The other tabs keep their `FlowLayoutPanel` until they are migrated.

---

## Rules and Backlog

- **RULES.md § Options Toolbar** gains a **Feature Lines** part: one feature per line, C then A then B,
  the fit rule, the labeled slider's shape — the rule every migrated tab follows (open question 6).
- **RULES.md § Color Options** — the opacity's shape (*the opacity icon, a slider, `Opacity: N %`*)
  becomes, in a migrated tab, the icon and `Opacity` over the slider, `N %` at the right (open question 5).
- **GLOSSARY.md / GLOSSARY.fr.md**: *Feature line* (*ligne de fonctionnalité*).
- **TODO-FEATURES.md**: no existing row matches this work. A row for the **tabs left to migrate**, and the
  Global toolbar's choices that are not option thumbnails yet (the Borders' style drop-down, the Fade's
  curve radio buttons) — open question 6.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*): no
unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| Background, Frames and Layers show one feature per line, top-aligned, in C at the usual window width | The running app | Manual check |
| Narrowing the window switches a pilot tab to A, then B, as the fit rule says | The running app | Manual check |
| Neither toolbar changes height; switching tabs moves nothing | The running app | Manual check |
| Every pilot control still works: activates its effect, undo, Resets, tooltips, eyedroppers | The running app | Manual check |
| The other tabs look as before | The running app | Manual check |

---

## Open Questions

- [ ] 1. **The fit rule**: when is C, A or B chosen — measured per tab at every resize of the window, or
  decided once per tab at design time? And what "fits" means: every column of lines within the toolbar's
  width?
- [ ] 2. **When B does not fit either** (a narrow window — the minimum is 480 px wide): the options clipped
  at the right as today, or a horizontal scrollbar?
- [ ] 3. **A line's leading element and the columns**: a line without a checkbox leads with its caption;
  do the leading elements of a column share one width, so the controls start aligned? A thin line between
  two columns of lines?
- [ ] 4. **The feature lines of the pilot tabs** as listed in § Feature Lines of the Pilot Tabs.
- [ ] 5. **The labeled slider**: one compact width for every slider (≈ 120 px; the Frames' frame sliders
  wider), the opacity icon before its name above the slider (§ Color Options updated)?
- [ ] 6. **Rules and backlog**: RULES.md § Options Toolbar › Feature Lines written now for every tab, and a
  TODO-FEATURES row for the tabs left to migrate and the Global choices that are not thumbnails yet?

---

## Design Iterations

### Iteration 1 — 2026-11-04

Initial design. Agreed before exploration: the layouts C, A, B in that order (parent session), the slider's
name and value above it, lines top-aligned, the toolbars' height kept, three pilot tabs (Background,
Frames, Layers). Proposed: the feature lines of each pilot, one description per tab laid out three ways
by one panel. Read from the code: with the height kept, C holds about two slider lines per column in the
cell toolbar, so it wraps into columns.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | No test project in the repository |
| RULES.md | | | |
| GLOSSARY.md / .fr.md | | | |
| README.md / .fr.md | | | |
| TODO-FEATURES.md | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Slider's name and value: above it, or after it as today? | Above it | 2026-11-04 |
| 2 | Lines in the toolbar's height: top-aligned or centered; height kept or grown? | Top-aligned, height kept | 2026-11-04 |
| 3 | Scope: every tab at once, cell effects first, or a few pilot tabs? | A few pilot tabs (Background, Frames, Layers) | 2026-11-04 |
| 4 | Depth of the exploration? | Straightforward: one pass | 2026-11-04 |
| 5 | Open question 1 — the fit rule: measured at every resize, or decided per tab? | | 2026-11-04 |
| 6 | Open question 2 — when B does not fit: clipped or scrolled? | | 2026-11-04 |
| 7 | Open question 3 — leading elements of a column one width; a line between columns? | | 2026-11-04 |
| 8 | Open question 4 — the pilot tabs' feature lines as listed? | | 2026-11-04 |
| 9 | Open question 5 — the labeled slider's width and the opacity icon? | | 2026-11-04 |
| 10 | Open question 6 — RULES.md Feature Lines now, and a backlog row for the rest? | | 2026-11-04 |

---

*Last updated: 2026-11-04*
