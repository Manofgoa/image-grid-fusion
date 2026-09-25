# UI Cleanup

> Working document — turn the effects toolbar into tabs (checkbox = active, tab = options shown),
> keep an effect's settings while it is off, move Reset to the far right at the tabs' size, and
> remove the Carousel feature.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Three clean-ups of the main window (`src/ImageGridFusion/UI/MainForm.cs`):

1. **Effects as tabs.** Today one toggle button per effect carries two states at once — *active on
   the selected cell* and *its options are shown* — and the user cannot tell them apart. Each effect
   becomes a **tab** holding an **activation checkbox**: the checkbox says whether the effect is
   active, the selected tab says whose options are shown. Turning an effect off **keeps its
   settings**; a per-effect **Reset** button in the options row brings its defaults back.
2. **Reset** moves to the **far right** of the row, and has **the same height as the tabs** (today it
   is a real `Button` with WinForms' default padding, bigger than the `CheckBox`-as-button toggles).
3. **Carousel removed**, entirely — it is judged useless.

The rest of `RULES.md` § Effects (state on the cell + image pair, reset table, rendering in
`Compositor.DrawCell`, handles on the selected cell only) is unchanged.

---

## Effects Tabs

### Layout

From top to bottom, below the top bar:

```
┌ Top bar ──────────────────────────────────────────────────────────┐
│ Options of the selected tab   [Intensity ▭▭▭] [Kind …]    [Reset]  │  ← options row, always visible
└┬────────┬┘ ┌─────────┐ ┌─────────┐ ┌─────────┐            [Reset]  ← tabs row, tabs hang downward
 │☑ Blur  │  │☐ Zoom   │ │☑ B & W  │ │☐ Rotate │ …
 └────────┘  └─────────┘ └─────────┘ └─────────┘
                        ( grid )
```

- **Options row on top**, **tabs row below it**, the tabs hanging **downward** from the options row:
  the selected tab is drawn joined to the options panel it controls, the others behind.
- The **Effects** label stays at the start of the tabs row.
- Tab order is unchanged: Zoom, Rotate, Flip, Frames, Black & white, Blur.
- The tabs row's **Reset** (all effects) sits at its **far right**, **as tall as a tab**. It is not
  a tab and has no checkbox.
- The **options row is always visible**, at a fixed height (the tallest effect's options), so
  nothing below it jumps when the selection changes. It is **empty** while no tab is selected.
- The options row ends with the **effect's own Reset** button (see *Settings Kept While Off*).

### States

| Element | Means |
|---|---|
| Tab's checkbox checked | The effect is **active on the selected cell** — it shows in the preview and the exports |
| Selected tab | Its **options** show in the options row (and, when the effect is checked, its on-cell handles) |

- The **selected tab belongs to the toolbar**, not to the cell: selecting another cell **keeps the
  same tab selected**, whether or not the effect is active on the new cell. Its checkbox and options
  then reflect the new cell.
- At startup, no tab is selected (empty options row) until the user clicks one.

### Interactions

| Action | Does |
|---|---|
| Click a tab (outside its checkbox) | **Selects** it — shows its options. Activates nothing |
| Click a tab's checkbox | **Toggles** the effect on / off, **keeping its settings**, and **selects** the tab |
| **Act on any option** of the selected tab (slider, button, checkbox) | Applies it **and checks the tab's checkbox** if it was not |
| The effect's own **Reset** (options row) | Brings back that effect's **default state**: its default settings **and** its default on / off — today every effect is off by default, so it gets unchecked |
| The tabs row's **Reset** | **Every** effect of the selected cell back to its default state (settings and on / off) — today: all defaults, all unchecked |

- Clicking the checkbox over and over only turns the effect on and off: its settings never move.
- Acting on an option of an unchecked effect starts from its **kept settings**, then applies the
  change.
- The wheel and drag gestures on a cell activate Zoom as soon as the image is zoomed or moved. On a
  cell whose Zoom is **off with kept settings**, the gesture starts from **what is shown** (100 %
  centered) and **replaces** the kept zoom and focus — no jump under the mouse.
- The effect's own Reset is the one control of the options row that does **not** check the effect:
  it brings back the default state, on / off included.

### Settings Kept While Off

- Each effect of a cell + image pair has its **settings** and an **on / off** state, independent of
  each other. Unchecking only turns it off: zoom and focus, rotation and fine angle, flips, starting
  point and freeze, grayscale intensity, blur rectangle, kind and intensity are all kept.
- An effect that is **off renders as its defaults**: the preview, the exports, the canvas sizing
  (e.g. a quarter turn swapping the cell's axes) and the magnetic guides ignore its kept settings.
- The options of an unchecked effect show its **kept settings**, editable.
- Kept settings follow the effect's existing lifecycle (`RULES.md` § Scope and State): cleared when
  the image is replaced or shifts after a deletion, kept on swap and layout change, not persisted.
- Today, `ImageLook` (`Composition/ImageLook.cs`) ties both together — `Deactivate` restores
  defaults, `Frames` / `Grayscale` / `Blur` are active when non-null, Zoom / Rotate / Flip through an
  `Activations` bit set. The model splits the two, with the rendered values derived from the
  settings of the effects that are on.

### Effect Not Applicable (e.g. Frames on a Still Image)

- Its tab **stays selectable**, and stays selected when a cell it does not apply to is selected.
- Its **checkbox is disabled**, with a **tooltip** saying why (e.g. Frames: only for videos,
  animated GIFs and content of several pages).
- Its **options are disabled** (shown, greyed out).

### No Cell Selected

- Both rows **visible and disabled**; the selected tab stays highlighted, its options greyed out.

### On-Cell Handles

- An effect's handles (the blur bars) are drawn on the selected cell while its tab is selected
  **and the effect is checked** — not for an unchecked effect.

### New Rule — Acting on an Option Activates the Effect

To add to `RULES.md` § Effects (options toolbar), as requested by the user:

> Acting on any control of an effect's options **activates** the effect on the selected cell (its
> checkbox gets checked) before applying the change, starting from the effect's kept settings, so a
> change never happens without showing.

### Interaction with the Background Effect Workfile

`workfiles/20260925-background-effect.md` (in design, not implemented) adds a **Background** effect
**on by default**, and proposes that resets bring every effect back to its defaults. Both Reset
buttons of this workfile follow the same principle — *default state, on / off included* — so the
Background will come back **checked** after either Reset. That workfile also plans
`ImageLook.Background` as `null` while inactive, which *Settings Kept While Off* replaces: whichever
lands second adapts to the other.

### Documents Updated

| Document | Change |
|---|---|
| `RULES.md` § Scope and State | Settings kept while off; an effect off renders as its defaults |
| `RULES.md` § Effects Toolbar / Options Toolbar / On-Cell Handles | Tabs + checkbox, the new click table, the selected tab kept across cells, non-applicable effect (checkbox disabled + tooltip), options row always visible with the effect's Reset, handles only when checked, the new rule above |
| `GLOSSARY.md` | *Effects toolbar* and *Options toolbar* redefined; add *Effect tab* and *Activation checkbox* |
| `README.md` § Effects (lines ~40–44) | Tabs, checkbox, settings kept while off, the two Reset buttons |

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

The repository has **no test project** (no `*Tests*.csproj`). The settings-kept-while-off split of
`ImageLook` is testable logic, but pinning it means creating a test project, which this workfile does
not plan. **No unit test is created or updated.**

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Checking or unchecking the checkbox of a tab that is **not** selected: does it also select
  that tab?~~ → Yes, it selects the tab.
- [x] ~~**Frames** tab selected, then a **still image** cell is selected: what does the options row
  show?~~ → The tab stays selected, its options and its checkbox are disabled, and a tooltip on the
  checkbox says why.
- [x] ~~**Blur** tab selected but blur **unchecked**: are the blur bars drawn on the cell?~~ → No, only
  when checked.
- [x] ~~**No cell selected**: how do the two rows look?~~ → Both visible and disabled; the selected tab
  stays highlighted, its options greyed out.
- [x] ~~The effect's own **Reset** (options row): does it also uncheck the effect?~~ → It brings back
  the effect's default state, on / off included: unchecked for an effect off by default (all of
  today's), checked for one on by default (the planned Background).
- [x] ~~The tabs row's **Reset** (all effects): defaults **and** everything unchecked, or only
  unchecked?~~ → Defaults and unchecked — read with the answer above as *every effect back to its
  default state*, so a Background on by default comes back checked.
- [x] ~~**Wheel / drag on a cell whose Zoom is off** with a kept zoom: where does the gesture start?~~ →
  From what is shown (100 % centered), replacing the kept zoom.

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

### Iteration 4 — 2026-09-25

Answers to the open questions: toggling a non-selected tab's checkbox **selects** it; a
non-applicable effect's tab (Frames on a still image) **stays selectable**, with its **checkbox and
options disabled** and a **tooltip** on the checkbox saying why — replacing the current rule of a
disabled button; the blur bars show **only when the blur is checked**; with **no cell selected**,
both rows are visible and disabled, the selected tab still highlighted.

### Iteration 5 — 2026-09-25

User: turning a tab off must **not lose its settings**; a **Reset** button in the effect's options row
brings its defaults back; clicking the checkbox over and over only turns the effect on and off.
Changes the effect model: settings and on / off become independent, an effect off renders as its
defaults (new section *Settings Kept While Off*). Acting on an option now starts from the kept
settings instead of the defaults. Three questions emerge (the effect's Reset and the checkbox, the
tabs row's Reset, cell gestures on a Zoom that is off).

### Iteration 6 — 2026-09-25

Answers: the effect's own **Reset** brings back its **default state, on / off included** — unchecked
for today's effects, checked for the Background effect planned on by default
(`workfiles/20260925-background-effect.md`); the tabs row's **Reset** brings every effect back to its
defaults, all unchecked — applied with the same principle, so a Background on by default comes back
checked; a wheel / drag gesture on a cell whose Zoom is off starts **from what is shown** and
replaces the kept zoom. New section *Interaction with the Background Effect Workfile*. No open
question left.

### Iteration 7 — 2026-09-25 — ✅ Implemented

Go given for code, tests and documentation (no test project: nothing to create). The run stays on
`main`, as recorded for this repository.

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
| 5 | Does toggling a non-selected tab's checkbox also select it? | Yes | 2026-09-25 |
| 6 | Frames tab selected + still image cell: what does the options row show? | Tab selected, options and checkbox disabled, tooltip on the checkbox saying why | 2026-09-25 |
| 7 | Blur tab selected, blur unchecked: are the bars drawn? | No, only when checked | 2026-09-25 |
| 8 | No cell selected: how do the rows look? | Visible and disabled, selected tab highlighted, options greyed out | 2026-09-25 |
| 9 | Does the effect's own Reset also uncheck it? | Defaults + unchecked when the effect is off by default; the Background (see "Nouvel effet fond") will be on by default | 2026-09-25 |
| 10 | Tabs row's Reset: defaults and unchecked, or only unchecked? | Defaults + everything unchecked | 2026-09-25 |
| 11 | Wheel / drag on a cell whose Zoom is off with kept settings: where does it start? | From what is shown | 2026-09-25 |
| 12 | Go for implementation? | Code, unit tests and documentation | 2026-09-25 |

---

*Last updated: 2026-09-25*
