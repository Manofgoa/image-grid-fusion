# UI Cleanup

> Working document — turn the effects toolbar into tabs (checkbox = active, tab = options shown),
> move Reset to the far right at the tabs' size, and remove the Carousel feature.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Three clean-ups of the main window (`src/ImageGridFusion/UI/MainForm.cs`):

1. **Effects as tabs.** Today one toggle button per effect carries two states at once — *active on
   the selected cell* and *its options are shown* — and the user cannot tell them apart. Each effect
   becomes a **tab** holding an **activation checkbox**: the checkbox says whether the effect is
   active, the selected tab says whose options are shown.
2. **Reset** moves to the **far right** of the row, and has **the same height as the tabs** (today it
   is a real `Button` with WinForms' default padding, bigger than the `CheckBox`-as-button toggles).
3. **Carousel removed**, entirely — it is judged useless.

Everything else of `RULES.md` § Effects (state on the cell + image pair, reset table, rendering in
`Compositor.DrawCell`, handles on the selected cell only) is unchanged.

---

## Effects Tabs

### Layout

From top to bottom, below the top bar:

```
┌ Top bar ─────────────────────────────────────────────────┐
│ Options of the selected tab   [Intensity ▭▭▭] [Kind …]    │  ← options row, always visible
└┬────────┬┘ ┌─────────┐ ┌─────────┐ ┌─────────┐   [Reset]  ← tabs row, tabs hang downward
 │☑ Blur  │  │☐ Zoom   │ │☑ B & W  │ │☐ Rotate │ …
 └────────┘  └─────────┘ └─────────┘ └─────────┘
                     ( grid )
```

- **Options row on top**, **tabs row below it**, the tabs hanging **downward** from the options row:
  the selected tab is drawn joined to the options panel it controls, the others behind.
- The **Effects** label stays at the start of the tabs row.
- Tab order is unchanged: Zoom, Rotate, Flip, Frames, Black & white, Blur.
- **Reset** sits at the **far right** of the tabs row, **as tall as a tab**. It is not a tab and has no
  checkbox.
- The **options row is always visible**, at a fixed height (the tallest effect's options), so
  nothing below it jumps when the selection changes. It is **empty** while no tab is selected.

### States

| Element | Means |
|---|---|
| Tab's checkbox checked | The effect is **active on the selected cell** |
| Selected tab | Its **options** show in the options row (and its on-cell handles, e.g. the blur bars) |

- The **selected tab belongs to the toolbar**, not to the cell: selecting another cell **keeps the
  same tab selected**, whether or not the effect is active on the new cell. Its checkbox and options
  then reflect the new cell.
- At startup, no tab is selected (empty options row) until the user clicks one.

### Interactions

| Action | Does |
|---|---|
| Click a tab (outside its checkbox) | **Selects** it — shows its options. Activates nothing |
| Check a tab's checkbox | Activates the effect with its defaults |
| Uncheck a tab's checkbox | Deactivates the effect, restoring its defaults |
| **Act on any option** of the selected tab (slider, button, checkbox, on-cell handle) | Applies it **and checks the tab's checkbox** if it was not |
| Reset | Removes every effect of the selected cell (all checkboxes unchecked); the selected tab stays |

- Acting on an option of an inactive effect starts from that effect's **defaults**, then applies the
  change — the same result as checking the box, then changing the option.
- The wheel and drag gestures on a cell already activate Zoom as soon as the image is zoomed or
  moved: they follow the same rule, unchanged.

### New Rule — Acting on an Option Activates the Effect

To add to `RULES.md` § Effects (options toolbar), as requested by the user:

> Acting on any control of an effect's options — or on its on-cell handles — **activates** the
> effect on the selected cell (its checkbox gets checked) before applying the change. An option is
> never shown editable while having no effect.

### Documents Updated

| Document | Change |
|---|---|
| `RULES.md` § Effects Toolbar / Options Toolbar / On-Cell Handles | Tabs + checkbox, the new click table, the selected tab kept across cells, options row always visible, the new rule above |
| `GLOSSARY.md` | *Effects toolbar* and *Options toolbar* redefined; add *Effect tab* and *Activation checkbox* |
| `README.md` § Effects (lines ~40–42) | Describe tabs, checkbox, Reset on the right |

---

## Carousel Removal

What the feature does today: the preview rotates the images one cell clockwise every second
(paused on hover or drag), and while the **Carrousel** checkbox is on, Copy / Save export that
rotation as an MP4.

Removed entirely:

| Where | What goes |
|---|---|
| `Composition/Carousel.cs` | The whole file |
| `Imaging/CarouselExport.cs` | The whole file |
| `UI/GridPreview.cs` | `_carouselTimer`, `PlaysCarousel`, `SyncCarousel` / `PauseCarousel` / `ResumeCarousel` / `OnCarouselTick` / `ShowCarouselStep`, and their hooks in hover, drag, paint and dispose |
| `UI/MainForm.cs` | The `_carousel` checkbox, `ExportsCarousel`, the carousel branch of the Copy / Save export paths |

Must keep working: the **plain video export** (`ExportVideoAsync`, shared with the carousel branch
today), `Compositor.Draw`, and the preview's timer disposal, hover and drag handling for every other
feature.

Not touched: `README.md`, `RULES.md` and `GLOSSARY.md` do not mention the carousel. Older workfiles
that mention it are history and stay as they are.

---

## Test Impact

The repository has **no test project** (no `*Tests*.csproj`), and the changes are UI wiring
(`MainForm`, `GridPreview`) plus the removal of a feature. **No unit test is created or updated.**

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; UI-only change) | — | — |

---

## Open Questions

- [ ] Checking or unchecking the checkbox of a tab that is **not** selected: does it also select that
  tab? *(Proposed: yes — you see the options of what you just toggled.)*
- [ ] **Frames** tab selected, then a **still image** cell is selected (Frames does not apply, its tab
  is disabled): what does the options row show? *(Proposed: empty; the Frames tab stays the
  remembered selection and comes back on an animated cell.)*
- [ ] **Blur** tab selected but blur **unchecked**: are the blur bars drawn on the cell?
  *(Proposed: yes, at their default position — dragging one activates the blur, per the new rule.)*
- [ ] **No cell selected**: how do the two rows look? *(Proposed: both visible and disabled; the
  selected tab stays highlighted, its options greyed out.)*

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Initial request: Reset to the far right; remove the Carousel; effect buttons enlarged to match
Reset; "which effect is active / whose options are shown" is unclear — the user suggests tabs with
an activation checkbox inside each tab. The agent agreed: the checkbox and the selected tab carry
the two states the single toggle button mixes today.

### Iteration 2 — 2026-09-25

User: with tabs, the **options row goes above** and the **tabs below**, hanging downward. Scoping
answers: the block stays **under the top bar** (options row, then tabs, then grid), Reset at the far
right of the tabs row; **clicking a tab only selects it**, only the checkbox activates; Carousel
**removed entirely**; exploration expected **straightforward** (one scout pass). The agent's
proposal of greyed-out options for an unchecked effect was accepted here, then superseded by
Iteration 3.

### Iteration 3 — 2026-09-25

User: the options row is **always displayed**, empty when no tab is selected; since these are tabs,
the **selected tab is kept** when another cell is selected; **acting on any option checks the
activation checkbox** — and that becomes a **rule** (`RULES.md`). Supersedes the greyed-out options
of Iteration 2: an unchecked effect's options are editable, and editing one activates it.

Exploration findings folded in: the whole toolbar lives in `MainForm.cs` (`_effects` flow panel,
`EffectButton()`, `ToggleEffect`, `UpdateEffects`); Reset's larger size comes from being a plain
`Button` next to `CheckBox`-as-button toggles, with no explicit sizing; the carousel spans four
source files and shares `ExportVideoAsync` with the plain video export; no test project exists.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |
| RULES.md / GLOSSARY.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the "options above, tabs below" block go? | Under the top bar (options, tabs, grid); Reset far right | 2026-09-25 |
| 2 | What does a tab click do, and how do an unchecked effect's options behave? | Tab click = selection only; options greyed out — *superseded by Iteration 3: editable, and editing activates* | 2026-09-25 |
| 3 | How far does the Carousel removal go? | Entirely (UI, code, settings, tests, docs) | 2026-09-25 |
| 4 | Exploration depth? | Straightforward — one scout pass | 2026-09-25 |
| 5 | Does toggling a non-selected tab's checkbox also select it? | | |
| 6 | Frames tab selected + still image cell: what does the options row show? | | |
| 7 | Blur tab selected, blur unchecked: are the bars drawn? | | |
| 8 | No cell selected: how do the rows look? | | |

---

*Last updated: 2026-09-25*
