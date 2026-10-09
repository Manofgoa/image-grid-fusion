# Borders: Outer Frame Without Inner Lines

> Working document — let the Borders global effect draw its outer frame alone, the cells touching.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the Borders' line styles (Solid, Dashed, Dotted, Double) always leave a **gap between the
cells**, filled by the style; the **Outer frame** option only *adds* the same band around the grid.
There is no way to get a frame around the grid while the cells keep touching.

This work adds an **Inner lines** option to the Borders: unchecked, the gap between the cells disappears
— the cells touch as without Borders — while the outer frame, when checked, is still drawn.

Components:

| Component | Role |
|---|---|
| `Composition/GridBorders.cs` | The record (`Pattern`, `Thickness`, `OuterFrame`…), `HasGap`, `Inset`, `Draw`, `DrawOver`, `Bands` |
| `UI/MainForm.cs` | The Borders options (`_bordersOuterFrame`, `UpdateBorders`, `ChangeBorders`), `SeamsUnavailable` |
| `Composition/SeamField.cs` | The Seams global effect, skipped while the borders leave a gap |
| `README.md` / `README.fr.md`, `GLOSSARY.md` / `GLOSSARY.fr.md` | The Borders' options and vocabulary |

---

## Rendering

- **Inner lines checked** (the default, today's behaviour): unchanged — a gap between the cells,
  filled by the style; with the outer frame, a margin as wide around the grid.
- **Inner lines unchecked, Outer frame checked**: the cells **touch** each other, with no gap and no
  line between them; they shrink **only on the canvas edges**, by the frame's width, and the frame is
  drawn in the style around the grid. The frame's **thickness is unchanged**: the same Thickness slider.
- **Both unchecked**: allowed — the Borders stay on and draw nothing, the cells as without Borders.
  With the Twitter corners, the images keep the rounded-off corners, for Twitter to round, as the gap
  styles without outer frame do today.
- The Twitter corners keep working as today for the frame (`DrawFrameCorners`).
- The **Corners** style is not affected: Inner lines, like Outer frame, does not apply to it.

## Seams

- With Inner lines unchecked the cells touch, so the **Seams** global effect **applies again**: it
  reads `HasGap`, which is false then (§ Model). Its seams are the edges the cells share; the frame,
  over the canvas edge, does not change them.

## Model

- `GridBorders` gets a new member, `InnerLines` (`bool`), **true** in `GridBorders.Initial`.
- Today `HasGap` (`Pattern != Corners`) means two things at once — *a line style* and *a gap between
  the cells*. It splits:
  - `IsLineStyle` — `Pattern != Corners`: what the Outer frame / Inner lines checkboxes and the
    Opacity slider's enabling, and the `Draw` / `DrawOver` branch, read;
  - `HasGap` — `IsLineStyle && InnerLines`: the cells no longer touch — what `SeamField.Of` and
    `MainForm.SeamsUnavailable` read.
- `Inset`: the half-widths on the edges shared with a neighbour apply only with `InnerLines`; the
  canvas-edge inset stays `OuterFrame ? width : 0`.
- `Bands`: the boundary lines between cells are yielded only with `InnerLines`; the frame's sides as
  today. The Solid style fills the region outside the cells, which then is the frame alone.
- The new member is part of `GridBorders`, so of the undo history's step (`GridState`) by itself.

## UI

- A checkbox **Inner lines** in the Borders' options, just before **Outer frame**, **checked by
  default**.
- Enabled like Outer frame: in the line styles only, disabled in Corners, its setting kept.
- The two checkboxes are independent: both may be unchecked (§ Rendering).
- An ordinary option: acting on it turns the Borders on (`ChangeBorders`); the Borders' Reset, the
  Global Reset and *Clear all* bring it back checked; **not remembered** between sessions.
- Tooltip: says it draws the borders between the cells; unchecked, the cells touch.

---

## Test Impact

There is **no test project** in this repository (`CONTRIBUTING.md`: *check a change by hand in the
running app*), so no unit test is created or updated. The behaviours below are checked by hand in the
running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Inner lines unchecked + Outer frame: cells touch, frame drawn around the grid, in every line style | — (manual) | — |
| Inner lines checked: rendering unchanged | — (manual) | — |
| Both unchecked: nothing drawn, cells as without Borders | — (manual) | — |
| Inner lines unchecked: the Seams enabled and drawn between the touching cells | — (manual) | — |
| Corners style: Inner lines and Outer frame disabled, settings kept | — (manual) | — |
| Resets and *Clear all* bring Inner lines back checked; Ctrl+Z restores it | — (manual) | — |

---

## Open Questions

- [x] ~~What does "outer without inner" look like?~~ → A frame around the grid, the cells touching
- [x] ~~How is it reached in the options?~~ → An **Inner** checkbox next to Outer frame, checked by default
- [x] ~~The frame's thickness without inner lines?~~ → Unchanged, the same Thickness slider
- [x] ~~Inner and Outer frame **both unchecked** in a line style: allowed (the Borders on, nothing
      drawn), or at least one of the two always stays checked?~~ → Allowed, nothing drawn
- [x] ~~The **Seams** global effect, disabled today while the borders leave a gap: available again when
      Inner is unchecked, since the cells touch?~~ → Yes, available again
- [x] ~~The checkbox's **label**: `Inner`, `Inner lines`, `Between cells`…?~~ → `Inner lines`

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design from the scoping batch (Q&A 1–4): an **Inner** checkbox, checked by default; unchecked,
the cells touch and only the outer frame is drawn, at the same thickness. `HasGap` split into
`IsLineStyle` and `HasGap`. Three open questions left: both checkboxes unchecked, the Seams, the label.
No feature backlog row matches (`workfiles/TODO-FEATURES.md` mentions no border).

### Iteration 2 — 2026-10-09

Open questions answered (Q&A 5–7): both checkboxes may be unchecked — the Borders on, nothing drawn;
the Seams apply again with Inner lines unchecked, the cells touching; the checkbox is labelled
**Inner lines**, placed just before Outer frame. New § Seams; § Rendering, § UI and § Test Impact
updated.

### Iteration 3 — 2026-10-09 — ✅ Implemented

Go given: code, tests and documentation, in a worktree
(`.claude/worktrees/borders-outer-without-inner`, branch `feature/borders-outer-without-inner`).
Scope frozen as the sections above stand.

### Iteration 4 — 2026-10-09 — 🧭 Implementation choices

- No rule broken.
- `InnerLines` is the record's third positional member, right after `Thickness` and before
  `OuterFrame`, so the two options sit side by side; `GridBorders.Initial` is the only constructor
  call.
- The Seams needed no change of their own: `SeamField.Of` and `MainForm.SeamsUnavailable` already read
  `HasGap`, which now means *inner lines on*.
- In `Bands`, the boundary lines are skipped by iterating over no orientation when the inner lines are
  off; the frame's sides are yielded as before.
- Tooltip: *Draws the borders between the cells; unchecked, the cells touch*.
- Checked offscreen with a scratch project referencing the built assembly (outside the repository):
  Solid frame alone, Dashed frame alone with Twitter corners, Double with both, Solid with neither —
  the cells touch without inner lines, are unchanged with neither, and are unchanged with both.
- Spotted, not done (out of scope): the README documents no **Seams** global effect at all.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-09 | `GridBorders.InnerLines` / `IsLineStyle`; the Inner lines checkbox |
| Unit tests | 3 | 2026-10-09 | Not applicable: no test project in the repository; checked offscreen (Iteration 4) |
| README (EN + FR) | 3 | 2026-10-09 | The Inner lines option, the gap only with it |
| Glossary (EN + FR) | 3 | 2026-10-09 | Borders and Gap rows |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Visually, what is "outer without inner"? | A frame around the grid, the cells touching | 2026-10-09 |
| 2 | How does the user get it in the Borders' options? | An **Inner** checkbox next to Outer, checked by default | 2026-10-09 |
| 3 | The outer frame's thickness without inner lines? | Unchanged, from the same setting | 2026-10-09 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-09 |
| 5 | Inner and Outer frame both unchecked: allowed, or one always stays checked? | Allowed — nothing drawn | 2026-10-09 |
| 6 | The Seams available again with Inner unchecked? | Yes | 2026-10-09 |
| 7 | The checkbox's label? | `Inner lines` | 2026-10-09 |

---

*Last updated: 2026-10-09*
