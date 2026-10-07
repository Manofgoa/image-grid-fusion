# Rotation with Ctrl+R

> Working document — keyboard shortcuts turning a cell's image a quarter turn.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two keyboard shortcuts drive the **Rotate** effect without going through its options:

- **Ctrl+R** turns the image a quarter turn **clockwise**;
- **Ctrl+Shift+R** turns it a quarter turn **counter-clockwise**.

They act on the **hovered cell**, else on the **selected cell**, select that cell and the **Rotate**
tab of the effects toolbar, and put the fine angle back to 0°.

Relevant components:

| Component | Role |
|---|---|
| `UI/MainForm.cs` — `ProcessCmdKey` | Where the app's shortcuts live (Ctrl+V, Ctrl+C, Ctrl+S, Delete, Escape). Ctrl+R is free |
| `Composition/ImageLook.cs` — `Rotate(int quarterTurns)` | A relative quarter turn (negative: counter-clockwise), turning the flips and the focus with it, activating the effect |
| `Composition/ImageLook.cs` — `WithRotation(int degrees)` | What the options' 0° / 90° / 180° / 270° buttons use: an absolute angle, the fine angle back to 0 |
| `UI/MainForm.cs` — `ChangeLook` | Applies an option to the selected image, turning the effect on from its kept settings first (RULES.md § Options Toolbar) |
| `UI/GridPreview.cs` — `SelectUnderPointer` | Selects the cell under the mouse pointer when it holds an image, else keeps the selection; whether an image is selected afterwards |
| `UI/MainForm.cs` — `TurnQuarter` | The shortcuts' action: the target selected, the Rotate tab selected, the quarter turn applied through `ChangeLook` |

---

## Behaviour

### Keys

| Key | Does |
|---|---|
| Ctrl+R | A quarter turn clockwise |
| Ctrl+Shift+R | A quarter turn counter-clockwise |

### Target Cell

- The **hovered cell** when the mouse is over a cell holding an image — read from the pointer's
  position when the key is pressed, by the cells' slots, so a point in the gap between two cells
  belongs to one of them, as for every hit-test.
- Otherwise the **selected cell**.
- Neither → the shortcut does nothing, and no tab is selected.
- The target cell **becomes the selected cell**, so the Rotate tab shows the options of the image
  that just turned.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given for **code and tests**, in a dedicated worktree (`feature/rotation-ctrl-r`), fast-forwarded
into `main` at the end. The documentation (README) is not part of the go: declined.

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

- **Target read from the pointer, not the hover state**: `GridPreview.SelectUnderPointer` hit-tests
  the pointer's position when the key is pressed (`CellAt`, by slots), so a key pressed without a
  mouse move since the last turn still finds its cell.
- **The quarter turn** is `ImageLook.WithRotation(Rotation ± 90)` — the rotation buttons' own
  operation, the fine angle back to 0° — applied through `ChangeLook`, which turns the effect on from
  its kept settings first.
- **No cell under the pointer nor selected**: nothing happens, and the Rotate tab is not selected
  either.
- **Focus**: the shortcuts are taken whatever control holds the focus — no text field of the app
  uses Ctrl+R.
- **Tests**: the go asked for code and tests, but the repository holds no test project (§ Test
  Impact). None written, and no test project created: it is outside the frozen scope.

### Effect State

- The shortcut is an action on the Rotate effect's option: like any option, it **turns the effect
  on first, from its kept settings** (RULES.md § Options Toolbar), then turns it. A Rotate effect
  that was off at 90° comes back on at 90°, then turns to 180°.
- The **fine angle goes back to 0°**, like the options' rotation buttons: 90° + 10° turns to 180°
  exactly (Ctrl+R) or 0° exactly (Ctrl+Shift+R). The quarter turn is counted from the rotation
  without its fine angle.
- It **selects the Rotate tab** of the effects toolbar, so the rotation options show after the
  shortcut.
- It is **ignored while exporting**, like the effects toolbar (the export keeps the settings it
  started with).
- It is taken **whatever control holds the focus**, a text field included: no text field of the app
  uses Ctrl+R.
- The undo history records each turn as a step, and the position readout shows when the turn moves
  the image's center, both by themselves (RULES.md § Undo History, § Position Readout).

---

## Test Impact

The repository holds **no test project**: nothing testable by unit tests is created or updated.
The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Fine angle: does the quarter turn **keep** the fine angle (90° + 10° → 180° + 10°), or put it
  back to 0° like the options' rotation buttons?~~ → Put back to 0°, like the rotation buttons
- [x] ~~Hovered cell other than the selected one: does the shortcut also **select** the hovered cell,
  so the Rotate tab shows the options of the image that just turned?~~ → Yes, the target cell
  becomes the selected cell

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-30

Initial design, from the scoping answers: Ctrl+R clockwise, Ctrl+Shift+R counter-clockwise, on the
hovered cell else the selected one, selecting the Rotate tab. Exploration: Ctrl+R is unused;
`ImageLook.Rotate(±1)` already provides the relative quarter turn; no test project exists.

### Iteration 2 — 2026-09-30

Both open questions answered: the quarter turn puts the fine angle back to 0°, like the options'
rotation buttons; the target cell becomes the selected cell, so the Rotate tab shows the image that
just turned.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-07 | `GridPreview.SelectUnderPointer`, `MainForm.TurnQuarter`, the two cases in `ProcessCmdKey` |
| Unit tests | 4 | 2026-10-07 | Not applicable: no test project in the repository, none created (outside the scope) |
| README | — | 2026-10-07 | Declined: not part of the go (code and tests only) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Rotation direction of the shortcut? | Ctrl+R clockwise, Ctrl+Shift+R counter-clockwise | 2026-09-30 |
| 2 | Which cell does Ctrl+R act on? | The hovered cell, else the selected one | 2026-09-30 |
| 3 | Does Ctrl+R also select the Rotate tab? | Yes | 2026-09-30 |
| 4 | Straightforward or tricky / long subject? | Straightforward | 2026-09-30 |
| 5 | Fine angle kept or put back to 0° by the quarter turn? | Put back to 0° | 2026-09-30 |
| 6 | Hovered cell other than the selected one: selected by the shortcut? | Yes | 2026-09-30 |
| 7 | Go for the implementation? | Code and tests, in a separate worktree | 2026-10-07 |

---

*Last updated: 2026-10-07*
