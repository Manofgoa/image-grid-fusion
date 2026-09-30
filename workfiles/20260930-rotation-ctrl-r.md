# Rotation with Ctrl+R

> Working document — keyboard shortcuts turning a cell's image a quarter turn.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two keyboard shortcuts drive the **Rotate** effect without going through its options:

- **Ctrl+R** turns the image a quarter turn **clockwise**;
- **Ctrl+Shift+R** turns it a quarter turn **counter-clockwise**.

They act on the **hovered cell**, else on the **selected cell**, and select the **Rotate** tab of
the effects toolbar.

Relevant components:

| Component | Role |
|---|---|
| `UI/MainForm.cs` — `ProcessCmdKey` | Where the app's shortcuts live (Ctrl+V, Ctrl+C, Ctrl+S, Delete, Escape). Ctrl+R is free |
| `Composition/ImageLook.cs` — `Rotate(int quarterTurns)` | A relative quarter turn (negative: counter-clockwise), turning the flips and the focus with it, activating the effect |
| `Composition/ImageLook.cs` — `WithRotation(int degrees)` | What the options' 0° / 90° / 180° / 270° buttons use: an absolute angle, the fine angle back to 0 |
| `UI/MainForm.cs` — `ChangeLook` | Applies an option to the selected image, turning the effect on from its kept settings first (RULES.md § Options Toolbar) |
| `UI/GridPreview.cs` — `_hovered` | The hovered cell index, private today |

---

## Behaviour

### Keys

| Key | Does |
|---|---|
| Ctrl+R | A quarter turn clockwise |
| Ctrl+Shift+R | A quarter turn counter-clockwise |

### Target Cell

- The **hovered cell** when the mouse is over a cell holding an image.
- Otherwise the **selected cell**.
- Neither → the shortcut does nothing.

### Effect State

- The shortcut is an action on the Rotate effect's option: like any option, it **turns the effect
  on first, from its kept settings** (RULES.md § Options Toolbar), then turns it. A Rotate effect
  that was off at 90° comes back on at 90°, then turns to 180°.
- It **selects the Rotate tab** of the effects toolbar, so the rotation options show after the
  shortcut.
- It is **ignored while exporting**, like the effects toolbar (the export keeps the settings it
  started with).

---

## Test Impact

The repository holds **no test project**: nothing testable by unit tests is created or updated.
The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] Fine angle: does the quarter turn **keep** the fine angle (90° + 10° → 180° + 10°), or put it
  back to 0° like the options' rotation buttons?
- [ ] Hovered cell other than the selected one: does the shortcut also **select** the hovered cell,
  so the Rotate tab shows the options of the image that just turned?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Rotation direction of the shortcut? | Ctrl+R clockwise, Ctrl+Shift+R counter-clockwise | 2026-09-30 |
| 2 | Which cell does Ctrl+R act on? | The hovered cell, else the selected one | 2026-09-30 |
| 3 | Does Ctrl+R also select the Rotate tab? | Yes | 2026-09-30 |
| 4 | Straightforward or tricky / long subject? | Straightforward | 2026-09-30 |
| 5 | Fine angle kept or put back to 0° by the quarter turn? | | |
| 6 | Hovered cell other than the selected one: selected by the shortcut? | | |

---

*Last updated: 2026-09-30*
