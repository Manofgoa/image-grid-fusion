# Global Effects Tabs

> Working document — turn the Global effects row into a tabbed toolbar, the mirror image of the
> cell effects toolbar: tabs standing up from an options row, at the bottom of the window.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the global effects (Soundtrack, Borders) live in one flat **Global effects row** just above
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
- The preview gives up the height of one tabs row compared with today (the options were inline
  with the toggles).

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

- **Initial state** = the state *Clear all* already restores: Soundtrack **off, no file**;
  Borders **on**, Corners style, default thickness and outer frame — the **color is kept**, it is
  an app setting of the ⚙ menu, not part of the effect.
- The cell toolbars' *Reset* buttons still leave the global effects alone, and the global *Reset*
  buttons leave the cells alone.

### Soundtrack Without a File

- Today, turning the Soundtrack toggle on with no file chosen opens the file dialog
  (`ToggleSoundtrack`). *(see Open Questions)*

## Implementation Direction

- **`EffectTabs` generalized** rather than duplicated: made generic over its tab key
  (`ImageEffect` for the cells, a new `GlobalEffect` enum — `Soundtrack`, `Borders` — for the
  grid), and given an **edge** setting: seam at the top with tabs hanging down (today), or seam at
  the bottom with tabs standing up. The edge flips the seam's y, the tab fill origin and which
  border segment the selected tab omits.
- `MainForm.PaintOptionsEdge` (the seam across the rest of the tabs row) takes the same edge.
- `_globalRow` is replaced by a global tabs row + a global options row (one options panel per
  global effect, like `_options`), docked at the bottom above `_bottom`, sized by `FitEffectRows`.
- The existing mutators (`ToggleSoundtrack`, `BrowseSoundtrack`, `SetSoundtrackVolume`,
  `ToggleBorders`, `ChangeBorders`) are kept; option changes go through a "turn on first" step,
  like the cell options.

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

- [ ] Do the global tabs carry an icon, like the cell effect tabs (drawn in `EffectIcons`)?
- [ ] Soundtrack with no file: what does checking its box, or acting on its options, do?
- [ ] Soundtrack's own *Reset*: back to "no file, off" (the Clear all state), or keep the file and
      reset only its volume?
- [ ] The preview loses one tabs row of height: accepted as is?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project; UI-only change |
| README | | | § Global effects (l. 110-132) and l. 35 to rewrite |
| Rules & glossary | | | RULES.md § Global Effects / Global Effects Row, GLOSSARY.md rows |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | "The other way round": how do the global tabs and options stack? | Mirror: tabs above, standing up from the options row, options just above the bottom bar | 2026-09-26 |
| 2 | Is the global tabs' behaviour aligned on the cell effects toolbar? | Yes: checkbox per tab, options shown while off, acting on an option turns it on, no tab selected at startup, fixed-height options row | 2026-09-26 |
| 3 | Which *Reset* buttons for the global effects? | Both, like the effects: one at the far right of the tabs row (all global effects), one ending each effect's options | 2026-09-26 |
| 4 | Expected depth of the subject? | Straightforward — single scout pass | 2026-09-26 |
| 5 | Do the global tabs carry an icon? | | |
| 6 | Soundtrack with no file: what do its checkbox and options do? | | |
| 7 | Soundtrack's own *Reset*: remove the file, or keep it? | | |
| 8 | The preview loses one tabs row of height: accepted? | | |

---

*Last updated: 2026-09-26*
