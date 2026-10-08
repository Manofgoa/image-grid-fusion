# Ctrl+N Clear All

> Working document — a `Ctrl+N` keyboard shortcut doing what the bottom bar's **Clear all** button does.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The bottom bar's **Clear all** button (bottom left) removes every image and the global effects at once
and brings the format back to Twitter — the initial state, with no confirmation
(`workfiles/20260923-delete-all-images.md`, which had settled on *no keyboard shortcut*). This workfile
gives it **`Ctrl+N`**, the usual "new document" key.

Components: `UI/MainForm.cs` — `ProcessCmdKey` (the shortcuts), `ClearAll()` (the button's handler),
the `_clearButton` and the tooltips; `README.md` / `README.fr.md`.

---

## Behaviour

- **`Ctrl+N` is the button**: it calls the same `MainForm.ClearAll()` the button's `Click` calls — no
  second code path, no confirmation (the button has none), the same status line naming what was removed.
- **When it acts**: exactly when the button would.
  - `ClearAll()` already returns while exporting (`IsExporting`) and when there is nothing to clear
    (no image, every global effect and the format at their initial state) — the cases where the button
    is disabled (`MainForm.cs`, `_clearButton.Enabled`). `Ctrl+N` relies on those guards, it adds none of
    its own for them.
  - **Ignored during a gesture**, as `Ctrl+Z` / `Ctrl+Y` are (`MainForm.StepHistory`): while
    `GridPreview.InGesture` holds or a mouse button is down (`MouseButtons != MouseButtons.None`) —
    a separator, a crop or blur bar, a swap or an image being dragged, a wheel burst still running.
    The key does nothing then (it is consumed, not passed on). This guard is `Ctrl+N`'s own: the button
    cannot be clicked in the middle of a mouse gesture anyway.
  - **Everywhere in the window**, a focused text field included (the file explorer's search box, any
    other `TextBoxBase`): `Ctrl+N` has no native meaning in a text box, so nothing is taken from it. It is
    **not** added to the explorer's text-editing whitelist at the top of `ProcessCmdKey`.
- **Undo**: nothing to add — a Clear all is already one step of the undo history (`GridHistory` commits
  it once the state settles), and `Ctrl+Z` brings the grid back whichever route cleared it.

---

## UI

- The **Clear all** button gets a **tooltip** — it has none today — saying what it does and naming the
  shortcut, in the tone of the other tooltips:
  `Removes every image and the global effects, and brings the format back to Twitter (Ctrl+N)`.
  Set with the other `_toolTip.SetToolTip` calls of the constructor.
- The button's label stays **Clear all** (no shortcut in the text).

---

## Documentation

- `README.md` § Features, the **Clear all** bullet: the shortcut named next to the button, as Copy /
  `Ctrl+C` and Save / `Ctrl+S` are — ``**Clear all** (bottom left) or `Ctrl+N` removes…``.
- `README.fr.md`: the same bullet, in the same commit (`../CLAUDE.md` § French Versions).
- `GLOSSARY.md` has no *Clear all* entry and `RULES.md` no shortcut list: neither changes.

---

## Test Impact

The repository has **no test project** (`src/ImageGridFusion/ImageGridFusion.csproj` only), and the
change is key routing inside a `Form` calling an existing handler: **no unit test is created or
updated**. The check is manual, in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project; key routing to an existing handler) | — | — |

---

## Open Questions

- [x] ~~During a **gesture** — a separator, a crop or blur bar, a swap or an image being dragged, the
      mouse button still held, or a wheel burst still running (`GridPreview.InGesture`) — should
      `Ctrl+N` be **ignored**, as `Ctrl+Z` / `Ctrl+Y` are (`MainForm.StepHistory`), or **clear anyway**?~~
      → **Ignored**, like undo (Q&A #5)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, from the scoping batch (Q&A #1–#4) and a single direct scout pass (the questions
chained: the button, its handler, `ProcessCmdKey`):

- `Ctrl+N` calls `ClearAll()` — the button exactly, no confirmation, its own guards (exporting, nothing
  to clear) deciding when it acts; everywhere in the window, a focused text field included.
- Tooltip on the button naming `Ctrl+N`; README EN + FR name the shortcut on the Clear all bullet.
- No `TODO-FEATURES.md` row matches (the file does not exist).
- One open question left: the behaviour during a gesture.

### Iteration 2 — 2026-10-08

Open question answered (Q&A #5): `Ctrl+N` is **ignored during a gesture** (`GridPreview.InGesture` or a
mouse button held), as undo is — § Behaviour updated. No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project (see Test Impact) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What should Ctrl+N do compared to the Clear all button? | Exactly the button — same code path, its confirmation if it had one | 2026-10-08 |
| 2 | When should Ctrl+N act? | Like the button (locked while exporting), everywhere, a focused text field included | 2026-10-08 |
| 3 | Where should the shortcut be shown? | Button tooltip + README (EN + FR) | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-08 |
| 5 | During a gesture, is Ctrl+N ignored (like Ctrl+Z) or does it clear anyway? | Ignored, like Ctrl+Z | 2026-10-08 |

---

*Last updated: 2026-10-08*
