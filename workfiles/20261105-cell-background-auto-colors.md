# Cell Background Auto Colors

> Working document — the cell's Background gets **Auto** as its gradient's destination, and its source
> chosen from a **From** choice instead of the *Automatic color* checkbox.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

User request (2026-11-05): *"Background d'une cellule n'a pas la destination « Auto » ni comme source
« Auto » (auto source => utile si => destination darker par ex)"*.

Today (`workfiles/20260926-forced-background.md`, Q&A #19):

| | Source (color 1) | Destination (color 2, gradients) |
|---|---|---|
| **Cell's Background** | The *Automatic color* checkbox (the cell's band color, live) or a chosen color | `To`: Darker, Black, White, Chosen — **no Auto** (`FillControls(offersAuto: false)`, `BackgroundEffect.WithStyle` turning Auto into Darker) |
| **Grid background** | Follows the first cell until first turned on, then its own | `To`: **Auto, last cell**, Darker, Black, White, Chosen |

The cell's Auto source already works (*Automatic color* + Darker is possible) but does not read as a
**choice** beside `To`; and a chosen source cannot go **to** the cell's automatic color.

Goal:

- **Destination Auto** for a cell: its **own automatic color** (the band color the *Automatic color*
  checkbox gives today), followed live — useful with a chosen source: Chosen → Auto.
- **Source as a choice**: the checkbox replaced by a **From** choice, symmetric with `To`, Auto one of its
  values — Auto → Darker reading as such.

Relevant code:

| Where | What |
|---|---|
| `Composition/FillStyle.cs` | `SecondColor` (its `Auto` documented as "a cell offers none"), `SecondFor(first, automatic)`, `Paint` / `OpaquePixels` taking the `automatic` color standing for Auto |
| `Composition/BackgroundEffect.cs` | `Automatic` / `Color` (the source), `WithStyle` refusing Auto, `Fill(automatic)`, `ToJson` / `FromJson` (`automatic`, `color`) |
| `Composition/Compositor.cs` | `DrawBackground` — `background.Style.Paint(g, cell, flat, null, adjust)`: **`null` for Auto**, though `automatic` is in hand; `FlatFill` (the Seams, solid fills only) |
| `Composition/EdgeExtension.cs` | The same `Paint` / `OpaquePixels` calls with `null`, under the extended edges |
| `UI/MainForm.GridBackground.cs` | `FillControls` (shared by both fills: the `To` drop-down with swatches, `offersAuto`, `ChoiceText` → `Auto, last cell`), `BackgroundSecondSwatch` (passes `null` for Auto) |
| `UI/MainForm.cs` | `_backgroundAutomatic` (the checkbox), `SetBackgroundAutomatic`, `BackgroundRoute` (a pick unchecks it), the Background's color line |

---

## Destination Auto (cell)

- `To` offers **Auto** in a cell's Background, labelled **Auto** (the Grid background keeps *Auto, last
  cell*), its tooltip saying it is the cell's automatic color, followed live.
- It is the **cell's automatic color** — the one `Compositor.AutomaticBackground` gives for the part of
  the image shown, whatever the source is.
- **Auto → Auto is allowed**: a gradient from a color to itself, so a flat fill — the second color's own
  opacity still showing.
- Its swatch in the drop-down is the selected cell's automatic color (`_preview.SelectedAutomaticBackground`).
- Painting: `DrawBackground` and `EdgeExtension` pass the cell's automatic color to `FillStyle.Paint` /
  `OpaquePixels` instead of `null`; `BackgroundEffect.WithStyle` no longer turns Auto into Darker.
- Presets: `FillStyle.ToJson` already writes `second: "Auto"`; a cell's preset now keeps it.

---

## Source From (cell)

- The *Automatic color* checkbox is replaced by a **From** drop-down, built like `To` — each choice after
  the swatch of the color it gives now — leading the fill's color line, before its eyedropper + Color… and
  its opacity (Q&A #5):

  | From | Color 1 |
  |---|---|
  | **Auto** (the default) | The cell's automatic color, live — the checkbox checked today |
  | **Black** | Black |
  | **White** | White |
  | **Chosen** | The color picked with the eyedropper or Color… |

- Its caption is **Color** while the fill is **Solid**, **From** for a gradient (Linear, Radial), facing `To`
  (Q&A #6). It is never disabled: on a solid fill it is the fill's color.
- A color taken with the eyedropper or **Color…** sets From to **Chosen** (today: unchecks *Automatic
  color*); From set back to Auto drops the chosen color; From set to Chosen from another value keeps the
  color it gave at that moment as the chosen one — the checkbox's unchecking today.
- `To`'s **Darker** darkens the first color whatever its source — Black → Darker gives black.
- The cell's **To default stays Darker** (Q&A #8): From Auto → To Darker, a gradient showing as soon as
  Linear or Radial is picked.
- State: `BackgroundEffect` holds the source as one value (Auto, Black, White, Chosen) beside its chosen
  color, replacing the `Automatic` flag; acting on From turns the Background on, like any option.
- Presets: a new `first` key (`Auto`, `Black`, `White`, `Chosen`); a preset without it reads the older
  `automatic` key (true → Auto, false → Chosen with its `color`). `LookPreset.Describe` names the source.
- **Cell only** (Q&A #7): the Grid background keeps its first color as it is; its *Auto, first cell* stays
  in the backlog row *Gradient fills, the rest*. `FillControls` gains the From choice for the cell's use only.

---

## Docs

- `README.md` / `README.fr.md` § Background (lines ~121, 127, 132, 135): *Automatic color* → From, `To`
  gains Auto.
- `GLOSSARY.md` / `GLOSSARY.fr.md` — *Shared fill*: Auto no longer the Grid background's only.
- `RULES.md` § Options Toolbar — the checkbox list names *Automatic color*: removed from it.
- `SHORTCUTS.md`: no shortcut changes.

---

## Test Impact

The repository has **no unit test project** (`src/` holds the app only): nothing to create or update.
Checked by hand in the running app:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A cell's Linear gradient Chosen → Auto ends on the cell's automatic color, live when the zoom moves | — (manual) | — |
| Auto → Darker draws as *Automatic color* + Darker did | — (manual) | — |
| Auto → Auto draws a flat fill | — (manual) | — |
| An eyedropper pick sets From to Chosen; back to Auto drops it | — (manual) | — |
| A preset saved with To Auto restores To Auto | — (manual) | — |
| From Black / White fills with black / white; the caption reads `Color` on Solid, `From` on a gradient | — (manual) | — |
| A preset saved before this change (`automatic` key) restores its source | — (manual) | — |

---

## Open Questions

- [x] ~~What shape does **From** take, and which values? (`To` is a drop-down with swatches.)~~ → A
  drop-down like `To`: Auto, Black, White, Chosen
- [x] ~~On a **Solid** fill, how is the From line captioned (the line is the fill's color, not a gradient's
  start)?~~ → `Color` while Solid, `From` for a gradient
- [x] ~~Does the **Grid background** get the same From choice — its first color *Auto, first cell* followed
  live (backlog row *Gradient fills, the rest*) — since `FillControls` is shared?~~ → No: cell only, the
  Grid background's stays in the backlog
- [x] ~~The cell's **To default**: stays Darker, or becomes Auto?~~ → Stays Darker
- [ ] The worktree `feature/options-toolbar-layout` (go given) rebuilds the Background's options as one
  feature per line, its line 3 led by the *Automatic color* checkbox: implement this after it is merged,
  or now?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Scoping batch answered (Q&A #1–#4): the cell's destination Auto is its own automatic color, live; the
source becomes a From choice replacing the *Automatic color* checkbox; Auto → Auto allowed (a flat fill);
the subject is straightforward — one scout pass, done directly (the questions chain on three files).
Scout findings: the painter already takes an `automatic` color for Auto, the cell passes `null`; the
`To` control is a drop-down shared with the Grid background (`FillControls`); the Background's options
are being rebuilt by the `options-toolbar-layout` worktree. Five questions left open.

### Iteration 2 — 2026-11-05

Q&A #5–#8: From is a drop-down like `To` — Auto, Black, White, Chosen —, captioned `Color` on a solid
fill and `From` for a gradient; the cell only, the Grid background untouched; `To` stays Darker by
default. Derived: `BackgroundEffect`'s source becomes one value, a new `first` preset key with the older
`automatic` one still read. The order with the `options-toolbar-layout` worktree stays open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Cell destination Auto: which color? | The cell's own automatic color, live | 2026-11-05 |
| 2 | Source Auto (the cell has the *Automatic color* checkbox): what is expected? | A From row of choices, symmetric with To, replacing the checkbox | 2026-11-05 |
| 3 | Source and destination both Auto? | Allowed — a flat fill | 2026-11-05 |
| 4 | Depth of the subject? | Straightforward — one scout pass | 2026-11-05 |
| 5 | From: shape and values? | A drop-down like To: Auto, Black, White, Chosen | 2026-11-05 |
| 6 | From's caption on a Solid fill? | `Color` while Solid, `From` for a gradient | 2026-11-05 |
| 7 | Grid background gets From too? | No — cell only | 2026-11-05 |
| 8 | The cell's To default? | Stays Darker | 2026-11-05 |
| 9 | Order with the options-toolbar-layout worktree? | | |

---

*Last updated: 2026-11-05*
