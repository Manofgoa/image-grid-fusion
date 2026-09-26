# Global Effects Tabs

> Working document — turn the Global effects row into a tabbed toolbar, the mirror image of the
> cell effects toolbar: tabs standing up from an options row, at the bottom of the window.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Before this task, the global effects (Soundtrack, Borders) lived in one flat **Global effects row** just above
the bottom bar: a label, one toggle per global effect, its options beside it, shown only while it
is on (`MainForm._globalRow`, a `FlowLayoutPanel`).

The cell effects already use a richer pattern at the top of the window: an **options toolbar**,
then an **effects toolbar** of tabs hanging down from it, each tab with an activation checkbox,
a toolbar *Reset* at the far right and an effect *Reset* ending the options row.

Goal: give the global effects the **same pattern, mirrored vertically** — the tabs row stands on
top of the options row, the selected tab joined to it at its bottom edge — so both halves of the
window frame the preview symmetrically.

Components: `UI/EffectTabs.cs` (owner-painted tabs, orientation hard-coded, keyed by
`ImageEffect`), `UI/MainForm.cs` (`_tabsRow`, `_optionsRow`, `PaintOptionsEdge`, `FitEffectRows`,
`_globalRow`, `UpdateGlobalEffects`, `UpdateBorders`, `ToggleSoundtrack`, `ToggleBorders`,
`ClearAll`), `UI/EffectIcons.cs`, `README.md`, `RULES.md`, `GLOSSARY.md`.

---

## Layout

Bottom of the window, top to bottom:

```
┌──────────────── preview ────────────────┐
└──────────────────────────────────────────┘
 Global effects ╭☑ Soundtrack╮╭☑ Borders╮          [Reset]   ← global tabs row
────────────────╯            ╰───────────────────────────────
 [selected tab's options .................]        [Reset]   ← global options row
══════════════════════ bottom bar ═══════════════════════════
```

- **Global tabs row**: a **Global effects** label, one **tab** per global effect (Soundtrack,
  Borders), then, at the far right, a **Reset** button as tall as the tabs — the mirror of the
  effects toolbar.
- **Global options row**: below the tabs, just above the bottom bar; it holds the **selected
  tab's options only**, then the effect's own **Reset** button ending the row; **empty** while no
  tab is selected.
- The tabs **stand up** from the options row: the seam is the options row's **top** edge, and the
  selected tab is drawn **joined** to it (no line between them), the others resting on it.
- Both rows keep **one height each**, the options row at its tallest options' height, so nothing
  moves when another tab is selected or an effect is turned on or off.
- The new tabs row would take its height from the preview (today the options sit inline with the
  toggles). To compensate, the **initial window height grows by the global tabs row's height**
  (`ClientSize`, today 960 × 860 in the `MainForm` constructor), so the preview keeps its current
  size at startup. `MinimumSize` is unchanged.
- Each global tab carries an **icon**, drawn in `EffectIcons` like the cell effect tabs, same size
  and style: a music note for Soundtrack, a corner bracket for Borders.

## Behaviour

Aligned on the cell effects toolbar (RULES.md § Effects Toolbar, § Options Toolbar), with the
differences global effects already have (RULES.md § Global Effects):

| | Cell effects toolbar | Global tabs |
|---|---|---|
| Activation checkbox in the tab | ✅ | ✅ — checked while the global effect is on |
| Click on a tab, outside its checkbox | Selects it, activates nothing | Same |
| Click on the checkbox | On / off, keeps settings, selects the tab | Same |
| Options of an effect that is off | Shown, kept settings | Same — shown even while off |
| Acting on any option | Turns the effect on first | Same |
| Selected tab at startup | None | None — the options row starts empty |
| Selected tab | Belongs to the toolbar | Belongs to the global toolbar, independent of the cell toolbar's |
| No cell selected | Disabled | **Stays enabled** |
| While exporting | Locked | Locked — the export keeps the settings it started with |

### Reset Buttons

| Button | Does |
|---|---|
| The global effect's own *Reset* (end of the global options row) | That global effect back to its initial state |
| The global tabs row's *Reset* (far right) | Every global effect back to its initial state, all at once |

- **Initial state** = the state *Clear all* already restores (`ClearAll`): Soundtrack **off, no
  file**; Borders **off**, `GridBorders.Initial(color, Twitter corners by default)` — the **color**
  and the **Twitter corners default** are kept, they are app settings of the ⚙ menu, not part of
  the effect.
- The cell toolbars' *Reset* buttons still leave the global effects alone, and the global *Reset*
  buttons leave the cells alone.

- The Soundtrack's own *Reset* is **complete**: the file is forgotten, the effect off, the volume
  back to its default — the *Clear all* state.

### Soundtrack Without a File

- Checking the Soundtrack's box with **no file chosen opens the file dialog**, as the toggle does
  today (`ToggleSoundtrack`): a chosen file turns the effect on, a cancelled dialog leaves it off.
- The options' *Browse* button does the same.
- The volume stays adjustable with no file; it turns the effect on only once a file is chosen (an
  effect with no sound to play has nothing to show). The level set meanwhile (`_soundtrackLevel`)
  is given to the first file, and is part of the initial state: moved away from 100 %, it enables
  the Resets and *Clear all*.
- With no file, the file label reads **No file**, greyed.
- A sound file **dropped** on either global row sets the soundtrack (`_globalTabsRow`,
  `_globalOptionsRow`), as a drop on the old row did.

## Implementation

- **`EffectTabs<TEffect>`** (`UI/EffectTabs.cs`), generic over an enum of effects, rather than
  duplicated: its titles come from the owner (`MainForm.EffectTitle` for the cells, `ToString()`
  for the grid), and `standing: true` puts the options row's edge along its bottom — the seam's y,
  the fill of the non-selected tabs stopping short of it, and the tab side drawn closed flip with it.
- **`GlobalEffect`** (`Composition/GlobalEffect.cs`): `Soundtrack`, `Borders`, in tab order.
- **Icons** (`EffectIcons.Soundtrack`, `EffectIcons.Borders`): two beamed eighth notes, violet to
  blue; a hot pink L-bracket over the corner of a grey picture.
- **`MainForm`**: `_globalRow` replaced by `_globalTabsRow` (label, `_globalTabs`,
  `_globalResetButton`) and `_globalOptionsRow` (`_globalOptions` panels, `_globalEffectResetButton`),
  docked above `_bottom`; `FitEffectRows` sizes them like the cell rows; `PaintOptionsEdge` takes the
  row and its orientation. The *Soundtrack* / *Borders* toggle buttons are gone, the tabs' checkboxes
  replace them.
- New handlers: `SelectGlobalEffect`, `ToggleGlobalEffect`, `ResetGlobalEffects(effect?)` — the
  latter also used by `ClearAll`. `ChangeBorders` and `SetSoundtrackVolume` turn their effect on.
- **Taller window**: `OnLoad` adds the global tabs row's height to the window (capped at the screen's
  working area) and centers it again.
- The descriptive tooltips of the removed toggles moved to an option: the Soundtrack's to *Browse…*,
  the Borders' to the style list. The tabs show no tooltip of their own.

---

## Test Impact

The repository has **no unit test project** (no `*.Tests` project, no `.sln` besides the app's
`.csproj`). The change is UI-only (painting, layout, click routing in `MainForm` / `EffectTabs`),
so nothing testable outside the UI changes, and no test is created.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: UI-only change, no test project exists | — | — |

---

## Open Questions

- [x] ~~Do the global tabs carry an icon, like the cell effect tabs (drawn in `EffectIcons`)?~~
      → Yes, drawn like the others: a music note (Soundtrack), a corner bracket (Borders)
- [x] ~~Soundtrack with no file: what does checking its box, or acting on its options, do?~~
      → The checkbox (and *Browse*) opens the file dialog, as today; cancelling leaves it off
- [x] ~~Soundtrack's own *Reset*: back to "no file, off" (the Clear all state), or keep the file
      and reset only its volume?~~ → Complete: no file, off, default volume
- [x] ~~The preview loses one tabs row of height: accepted as is?~~ → The initial window height
      grows by the global tabs row's height, so the preview keeps its startup size

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the user's request ("global effects as tabs / toolbar, like the effects at the
top but the other way round") and the scoping batch: mirrored layout (tabs above, options below,
selected tab joined at its bottom edge), behaviour aligned on the cell effects toolbar
(checkbox per tab, options shown while off, acting on an option turns it on, no tab selected at
startup), both *Reset* buttons. Direction: generalize `EffectTabs` (generic key + edge) instead
of duplicating it. No unit tests exist, none planned.

### Iteration 2 — 2026-09-26

Open questions answered: the global tabs get icons drawn like the cell tabs; checking the
Soundtrack with no file opens the file dialog, as today; the Soundtrack's own *Reset* is complete
(no file, off); the lost preview height is compensated by a taller initial window, as the user
proposed.

### Iteration 3 — 2026-09-27 — ✅ Implemented

Go given ("GO", read as *code, tests and documentation*, the choice every earlier workfile of this
app got; no test project exists, so documentation = README, RULES.md, GLOSSARY.md), on `main`
(standing choice). Before it, the user asked whether the recent changes endanger the task — 22
commits since Iteration 2 (file explorer, copy last video, progress line) plus
`borders-off-and-rounded-corners`, implemented meanwhile:

- **No blocker.** The file explorer docks right, between the top toolbars and the global row; the
  *Copy last* button lives in the bottom bar; the content version counts borders / soundtrack
  changes through the existing mutators, which this task keeps. No worktree or unmerged branch.
- **Borders' initial state changed**: off by default, with an opacity slider and a *Twitter
  corners* checkbox whose default is an app setting. The design already defined the *Reset* state
  as the *Clear all* one; its outdated detail (on, Corners) is corrected.
- **Drop on the row**: a sound file dropped on today's row sets the soundtrack; kept on both new
  rows.
- Pending workfiles that will land on the new rows later: `global-fade` (already designed on the
  cell-effect model, consistent), `forced-background` (drafted on the old row: its "toggle, options
  beside it" becomes a tab), `ctrl-wheel-5-percent-step` (sliders of the row, unaffected),
  `colors-effect` (renames a cell tab title, unaffected by the generalization).

### Iteration 4 — 2026-09-27 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **"GO" read as the third option** (code, tests and documentation), the one every earlier
  workfile of this app got; tests not applicable.
- **Stayed on `main`**, the standing choice of this repository, no Branch Gate question.
- **`EffectTabs<TEffect>`**, generic over the enum, the titles given by the owner: the static
  `EffectTabs.Title` moved to `MainForm.EffectTitle`. `colors-effect` planned its tab rename in
  `EffectTabs.cs`; it now lands in `MainForm.EffectTitle`.
- **`GlobalEffect`** placed in `Composition/`, next to `ImageEffect`.
- **Volume before a file**: kept in `_soundtrackLevel`, given to the first file, and counted in the
  initial state (Resets and *Clear all* enabled once it moved).
- **"No file"**, greyed, in the file label while none is chosen — the options now show without one.
- **Tooltips** of the removed toggles moved to *Browse…* and to the Borders' style list.
- **Taller window** done in `OnLoad` (after DPI scaling): + the global tabs row's height, capped at
  the screen's working area, then centered again.
- **Sliders** of the global options take the options row's white, as in the top options row (they
  were grey on the old grey row).
- Docs: RULES.md § *Global Effects Row* renamed *Global Effects Toolbar*; the glossary's *Global
  effects row* replaced by *Global effects toolbar* and *Global options toolbar*. The pending
  workfiles still saying *Global effects row* (`forced-background`, `global-fade`,
  `ctrl-wheel-5-percent-step`) were left untouched — out of scope.
- Found out of scope, not fixed: GLOSSARY.md's *Borders* row still says "on at start-up", stale
  since `borders-off-and-rounded-corners`.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | Iteration 3 | 2026-09-27 | Generic `EffectTabs`; `GlobalEffect` + icons; the two global rows in `MainForm`; the taller window; a comment — five commits |
| Unit tests | Iteration 3 | 2026-09-27 | Not applicable: no test project, UI-only change |
| README | Iteration 3 | 2026-09-27 | § Global effects, Soundtrack, Borders; the overview line |
| Rules & glossary | Iteration 3 | 2026-09-27 | RULES.md § Global Effects table + § Global Effects Toolbar (was Row); GLOSSARY.md: global effects toolbar, global options toolbar |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | "The other way round": how do the global tabs and options stack? | Mirror: tabs above, standing up from the options row, options just above the bottom bar | 2026-09-26 |
| 2 | Is the global tabs' behaviour aligned on the cell effects toolbar? | Yes: checkbox per tab, options shown while off, acting on an option turns it on, no tab selected at startup, fixed-height options row | 2026-09-26 |
| 3 | Which *Reset* buttons for the global effects? | Both, like the effects: one at the far right of the tabs row (all global effects), one ending each effect's options | 2026-09-26 |
| 4 | Expected depth of the subject? | Straightforward — single scout pass | 2026-09-26 |
| 5 | Do the global tabs carry an icon? | Yes, drawn like the others | 2026-09-26 |
| 6 | Soundtrack with no file: what do its checkbox and options do? | The checkbox opens the file dialog, as today | 2026-09-26 |
| 7 | Soundtrack's own *Reset*: remove the file, or keep it? | Complete: no file, off | 2026-09-26 |
| 8 | The preview loses one tabs row of height: accepted? | "Didn't get it — at worst, raise the initial window height?" → the initial window grows by the tabs row's height | 2026-09-26 |
| 9 | Start implementing? | No — the gate holds | 2026-09-26 |
| 10 | (user) Do the recent changes endanger the task? Then GO | Analysis in Iteration 3: no blocker; GO read as code + documentation, on `main` | 2026-09-27 |
| 11 | Is the task finished? | | |

---

*Last updated: 2026-09-27*
