# Ctrl+F Cycle — Every Cycle Starts at the Folder View

> Working document — the first `Ctrl+F` of a view cycle goes to the folder view, whatever view
> the file explorer shows (favorites, People, GIPHY…); the next presses go on from there.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today (origin: `workfiles/20261007-ctrl-f-search-focus.md`, `workfiles/20261029-ctrl-f-view-cycle.md`,
`workfiles/20261102-ctrl-f-cycle-folder-root.md`):

- `Ctrl+F` with the search box **not** focused (or the panel collapsed) opens the panel if needed and
  focuses the search box, its text selected — the view does not change.
- Pressed again **while the search box has the focus**, it moves to the **next view** of
  `ExplorerMode`'s order — Folders → People → Favorites → GIPHY GIFs → GIPHY Stickers, round the end,
  People skipped while the face analysis is off, a GIPHY view skipped while hidden or without a key;
  `Ctrl+Shift+F` to the previous one (`FileExplorerPanel.NextMode(step)`).
- The folder view reached through the cycle opens at the base folder, the folder left selected.

The cycle always starts **from the view shown**: from GIPHY Stickers, the next press goes to Folders;
from Favorites, it goes to GIPHY GIFs, and Folders is three presses away.

The request: **the first press of a cycle goes to the folder view**, whatever the view shown; the
following presses of the same cycle go on in today's order from there. Leaving the search box ends
the cycle: back in the box, the next cycle starts at the folder view again.

> User's example: *I put GIPHY back (the views drop-down), I come back into the search box and I
> cycle → it goes back to Folders.*

Components:

- `UI/FileExplorerPanel.cs` — `FocusSearch(int step)` (the cycle), `NextMode`, `IsOffered`,
  `ChangeMode`, the search box `_search`.
- `UI/MainForm.cs` — `ProcessCmdKey` binds `Ctrl+F` / `Ctrl+Shift+F` to `FocusSearch(±1)`.
- `SHORTCUTS.md` / `SHORTCUTS.fr.md` — the `Ctrl+F` and `Ctrl+Shift+F` rows.

---

## Behaviour

### Agreed

| Press | Search box focused? | Cycle running? | Does |
|---|---|---|---|
| `Ctrl+F` | No (or panel collapsed) | — | **Unchanged**: opens the panel if needed, focuses the box, its text selected. The view does not change, and **no cycle starts** |
| `Ctrl+F` or `Ctrl+Shift+F` | Yes | No | **New**: the **folder view**, whatever the view shown — the folder view itself included — at the base folder, the folder left selected, as an arrival through the cycle today. A cycle starts |
| `Ctrl+F` | Yes | Yes | Unchanged: the next view in today's order, from the view shown |
| `Ctrl+Shift+F` | Yes | Yes | Unchanged: the previous view |

- **A cycle** is the run of cycling presses made without the search box losing the focus and
  without its text changing. Either ends it:
  - the search box **losing the focus** — a click on a tile, the views drop-down, the grid, another
    window;
  - its **text changed** — typed, erased, pasted: a search typed there starts a new cycle.
- **The folder view already shown** at a cycle's start: the press **stays** on it, back at the base
  folder with the folder left selected; the next press goes on to People.
- **Both directions**: `Ctrl+Shift+F` starts a cycle at the folder view too; its next presses go
  back from there (GIPHY Stickers, GIPHY GIFs, Favorites…).
- **The order is today's**, taken up from the folder view: Folders → People → Favorites → GIPHY GIFs
  → GIPHY Stickers, the same skips.
- **The search text is kept**, selected again, as the cycle keeps it today.

### Planned code

- A flag on `FileExplorerPanel`, `_viewCycling`: set by a cycling press in `FocusSearch`, cleared
  when the search box loses the focus (`_search.Leave`) and when its text changes
  (`_search.TextChanged`) — except while `FocusSearch` itself changes the view, guarded so a view
  change touching the text does not end the cycle it belongs to.
- In `FocusSearch`, the search box focused: the mode is `ExplorerMode.Folders` when no cycle is
  running — whatever the step, the folder view itself included — `NextMode(step)` otherwise; the
  rest unchanged (the open folder reset to the base folder, the folder left selected, `ChangeMode`).
- The choice of the next view moved into a pure function the tests can reach
  (`Explorer/ViewCycle.Next(current, step, cycling, available)`), `NextMode` calling it with
  `IsOffered` and the People / GIPHY skips.

---

## Documentation

- `SHORTCUTS.md` / `SHORTCUTS.fr.md` — the `Ctrl+F` row (*pressed again there, the next view*): the
  first press of a cycle goes to the folder view, the next ones on from there, a cycle ending when
  the box loses the focus or its text changes; the `Ctrl+Shift+F` row: the first press of a cycle
  goes to the folder view too. In the **same commit as the code** (RULES.md § Keyboard Shortcuts).
- `README.md` / `README.fr.md` — § File explorer, the search box's key bullet, if it describes the
  cycle's order.
- RULES.md unchanged: no rule names the views' cycle.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| No cycle running: `Ctrl+F` gives Folders from every view | `tests/ImageGridFusion.Tests/Explorer/ViewCycleTests.cs` | Create |
| Cycle running: the next view in `ExplorerMode`'s order, round the end, the unavailable views skipped | `tests/ImageGridFusion.Tests/Explorer/ViewCycleTests.cs` | Create |
| No cycle running, the folder view shown: Folders again (no move to People) | `tests/ImageGridFusion.Tests/Explorer/ViewCycleTests.cs` | Create |
| No cycle running, step -1 (`Ctrl+Shift+F`): Folders; cycle running, step -1: the previous view | `tests/ImageGridFusion.Tests/Explorer/ViewCycleTests.cs` | Create |

The cycle's end (`_search.Leave`, `_search.TextChanged`) is WinForms wiring, checked in the app, not
unit-tested.

---

## Open Questions

- [x] ~~What does the first `Ctrl+F` do, the search box not focused?~~ → Focus only, as today; the
  next press — the first of a cycle — goes to the folder view.
- [x] ~~The cycle's order after the folder view?~~ → Today's, taken up from Folders.
- [x] ~~The search text on the way back to the folder view?~~ → Kept, as today.
- [x] ~~What is "the first" press?~~ → The first press of a **cycle**: leaving the search box and
  coming back starts a new cycle, back at the folder view.
- [x] ~~The folder view **already shown** at the start of a cycle: stays on it, or goes on to
  People?~~ → Stays on it, back at the base folder; the next press goes on to People.
- [x] ~~`Ctrl+Shift+F` as the first press of a cycle: the folder view too, or the previous view?~~
  → The folder view too; its next presses go back from there.
- [x] ~~Typing in the search box between two presses: does it end the cycle?~~ → Yes: a text
  change ends the cycle, as the focus leaving does.

---

## Design Iterations

### Iteration 1 — 2026-11-10

Scoping answered by the user: the first `Ctrl+F` keeps focusing only; the first press of a cycle
goes to the folder view whatever the view shown, the next ones on in today's order; the text kept.
Clarified unprompted: "first" means the first press of a cycle — the search box left and focused
again, the next cycle starts at the folder view again. A cycle is tracked by a flag cleared when the
search box loses the focus; the view choice moved into a pure, tested function. Three points left
open (§ Open Questions).

### Iteration 2 — 2026-11-10

The three points settled: the folder view already shown stays (back at the base folder);
`Ctrl+Shift+F` starts a cycle at the folder view too; a text change in the box ends the cycle, as
the focus leaving does — `_search.TextChanged` clears the flag, guarded while `FocusSearch` changes
the view. Behaviour table, planned code, documentation and tests updated. No open question left.

### Iteration 3 — 2026-11-10 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree
(`.claude/worktrees/ctrl-f-cycle-starts-at-folders`, branch `feature/ctrl-f-cycle-starts-at-folders`).

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| SHORTCUTS.md / .fr.md | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | `Ctrl+F` with the search box not focused: focus + folder view, or focus only? | Focus only | 2026-11-10 |
| 2 | The cycle after the folder view: today's order, or another? | Today's order, from Folders | 2026-11-10 |
| 3 | The search text when going back to the folder view: kept, cleared, selected? | Kept, as today | 2026-11-10 |
| 4 | Is the subject straightforward or tricky? | Straightforward — one exploration pass | 2026-11-10 |
| 5 | *(user, unprompted)* What "first" means | The first press of a cycle: GIPHY put back, the search box focused again, cycling → Folders again | 2026-11-10 |
| 6 | The folder view already shown at a cycle's start: stay, or go on to People? | Stays on Folders (base folder); the next press goes to People | 2026-11-10 |
| 7 | `Ctrl+Shift+F` starting a cycle: Folders, or the previous view? | Folders too | 2026-11-10 |
| 8 | Typing in the box between two presses: ends the cycle or not? | Yes, a text change ends it | 2026-11-10 |

---

*Last updated: 2026-11-10*
