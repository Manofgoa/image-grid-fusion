# Border Color in the Borders Options

> Working document — the Borders' color moves from the ⚙ menu into the Borders tab's options, becomes
> an ordinary option of the effect, and the last color chosen is remembered in the settings file.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the Borders' color is an **app setting of the ⚙ menu** (*Border color…*, with a swatch): it is
applied at once, saved in `settings.json` (`AppSettings.BorderColor` / `SaveBorderColor`, key
`BorderColor`, hotpink by default), and kept out of everything the effect owns — no Reset touches it,
the undo history leaves it out (`GlobalState` comment, `MainForm.RestoreState` keeps the current
color), *Clear all* keeps it.

The goal:

- the color is set from the **Borders tab's options** (Global toolbar, bottom of the window), with a
  **Color…** button showing a swatch of the color in use — the same control as the Background's;
- it becomes an **ordinary option of the effect**: its Resets and *Clear all* bring back the default,
  the undo history covers it, and changing it turns the Borders on;
- the **last color chosen is remembered** in `settings.json` and taken back at start-up.

The ⚙ menu loses its *Border color…* item. **Twitter corners by default stays in the ⚙ menu**,
unchanged — out of scope.

Relevant code:

| Where | What |
|---|---|
| `UI/MainForm.cs` — `_borderColor` (menu item), `PickBorderColor`, `UpdateBorderColorSwatch` | The ⚙ menu route, to be replaced |
| `UI/MainForm.cs` — `_globalOptions[GlobalEffect.Borders]` | The Borders options row: style, thickness, opacity, outer frame, Twitter corners |
| `UI/MainForm.cs` — `_backgroundColor`, `SetSwatch(ButtonBase, Color)` | The Background's Color… button with its rounded swatch, the model to follow |
| `UI/MainForm.cs` — `ChangeBorders`, `ResetGlobal…` (`GridBorders.Initial(_borders.Color, …)`), `BordersInitial`, `RestoreState` | Where the color is currently kept apart |
| `UI/GridHistory.cs` — `GlobalState` | Its comment says the color is left out of the history |
| `UI/AppSettings.cs` — `BorderColor`, `SaveBorderColor`, `DefaultBorderColor` | The stored value, its key and default |
| `Composition/GridBorders.cs` — `Initial(color, rounded)` | The initial state |

---

## UI

- A **Color…** button in the Borders options row — `Button`, `AutoSize`, `TextImageRelation.ImageBeforeText`,
  its image the rounded swatch of the borders' color (`SetSwatch(ButtonBase, Color)`, redrawn at the
  monitor's DPI with the other swatches).
- Placement: **right after the style** drop-down — style, Color…, thickness, opacity, outer frame,
  Twitter corners — the color next to the line's shape.
- Tooltip: what it does and that the color is remembered between sessions.
- Click → the standard color dialog, preselected on the current color. **OK** → the change goes
  through `ChangeBorders` (§ Options Toolbar: acting on any option activates the effect — the
  Borders are turned on). **Cancel** → nothing.
- Locked while exporting, like every global option (`ChangeBorders` already returns while exporting;
  the button is disabled with the row).
- The ⚙ menu's **Border color…** item is **removed**, with its tooltip, click handler, swatch update
  and the `Enabled = !IsExporting` line.

---

## Behaviour

The color becomes part of the effect's state like its other settings (RULES.md § Global Effects).

| Event | Color |
|---|---|
| Start-up | The color remembered in `settings.json`; hotpink when none was saved |
| Color… → OK | Applied, the Borders turned on, **the color remembered** |
| The Borders' own Reset / the Global Reset / *Clear all* | Back to **hotpink** (`AppSettings.DefaultBorderColor`); the remembered color left as it is |
| Undo / redo | Restored with the step, like the other Borders settings; the remembered color left as it is |
| Borders turned off | Kept, like every setting of an effect turned off |

- **Only a color chosen in the dialog is remembered**: a Reset, *Clear all* or an undo changes the
  color in force, never the one saved — the next launch starts from the last color picked.
- **Default**: the Borders' initial state is `GridBorders.Initial(AppSettings.DefaultBorderColor,
  _roundedByDefault)` — hotpink — in the Resets, *Clear all* and `BordersInitial` (which enables the
  Global Reset, the Borders' Reset and *Clear all*). At start-up the borders take the remembered
  color instead: with a remembered color other than hotpink, the Borders are not in their initial
  state, so their Reset, the Global Reset and *Clear all* are enabled from the start — a Reset
  there does change something (the color back to hotpink).

- **Undo history**: `GlobalState.Borders` already carries the color; `RestoreState` stops overriding it
  with the current one, and the `GlobalState` comment changes accordingly. `GridHistory.Describe`
  already labels a Borders change.
- **Settings key**: unchanged (`BorderColor`), so a color saved by an earlier version is taken back.

---

## Documentation

| File | Change |
|---|---|
| `README.md` / `README.fr.md` | § Borders *Color* line: the Color… button of the Borders options; the overview bullet (line 46); § Undo: the borders' color no longer listed as not covered; the ⚙ menu paragraph no longer lists Border color; § Settings file keeps *border color* (still remembered) |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | *Borders*: its color set from its options and remembered, no longer an app setting of the ⚙ menu |
| `RULES.md` | § Global Effects: the Borders' color is no longer the example of an app setting read by a global effect — reworded (a global effect's option may be remembered between sessions); § Undo History: the Borders' color no longer left out of the step |

---

## Test Impact

The repository has **no test project** (`src/` holds the app only), so no unit test is created or
updated. Nothing in the planned work justifies creating a test project for it.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~What is the color's **default** brought back by the Resets and *Clear all* — hotpink, or the
  last color remembered?~~ → Hotpink; the remembered color is only the start-up one
- [x] ~~Which changes **overwrite the remembered color** — only a color chosen in the dialog, or every
  change of the color in force (Reset, undo / redo included)?~~ → Only a color chosen in the dialog
- [x] ~~Where does the **Color…** button sit in the Borders options row?~~ → Right after the style

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request and the scoping batch (Q&A 1–4): a Color… button with a swatch in
the Borders options, the color an ordinary option of the effect (Resets, *Clear all*, undo), the last
color chosen remembered in `settings.json`, the ⚙ menu item removed, Twitter corners by default left
in the ⚙ menu. Single scout pass (subject judged straightforward), done directly: no test project
exists, so Test Impact is empty. Three questions left open: the default, what overwrites the
remembered color, the button's placement.

### Iteration 2 — 2026-10-08

Open questions answered (Q&A 5–7): the Resets and *Clear all* bring back **hotpink**, the remembered
color being the start-up one only; **only a color chosen in the dialog** is remembered — a Reset,
*Clear all* or an undo never overwrites it; the **Color…** button sits **right after the style**.
Consequence written into § Behaviour: with a remembered color other than hotpink, the Borders are
not in their initial state at start-up, so their Resets and *Clear all* are enabled from the start.

### Iteration 3 — 2026-10-09 — ✅ Implemented

Go given: code, unit tests and documentation (no test project, so no test), in a worktree on
`feature/border-color-in-borders-options`. Scope frozen as the design sections stand.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which control shows the color in the Borders options? | Color… button with a swatch, like the Background's | 2026-10-08 |
| 2 | Once in the effect, how does the color behave with the Resets and undo? | An ordinary option of the effect: Resets bring back the default, undo restores it, the last color chosen remembered in `settings.json` and taken back at start-up | 2026-10-08 |
| 3 | The ⚙ menu also holds Twitter corners by default — what about it? | Only the color moves; Twitter corners by default stays in the ⚙ menu | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-08 |
| 5 | What is the default brought back by the Resets and *Clear all*? | Hotpink; the remembered color is taken back at start-up only | 2026-10-08 |
| 6 | Which changes overwrite the remembered color? | Only a color chosen in the Color… dialog | 2026-10-08 |
| 7 | Where does the Color… button sit in the Borders options row? | Right after the style drop-down | 2026-10-08 |
| 8 | Go for the implementation? Scope, and where? | Code, unit tests and documentation; in a worktree | 2026-10-09 |

---

*Last updated: 2026-10-09*
