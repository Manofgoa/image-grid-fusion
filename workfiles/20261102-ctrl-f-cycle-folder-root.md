# Ctrl+F Cycle — Folder View Back to the Base Folder

> Working document — when the `Ctrl+F` view cycle arrives on the folder view, it opens the base
> folder, so pressing `Ctrl+F` a few times brings the folder view back to the top.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, with the search box focused, `Ctrl+F` moves to the next view of the file explorer —
Folders → People → Favorites → Folders, People skipped while the face analysis is off — and
`Ctrl+Shift+F` to the previous one (origin: `workfiles/20261029-ctrl-f-view-cycle.md`). Arriving on
the folder view, it shows the **open folder** as it was left, however deep.

The request: arriving on the folder view **through the cycle**, the open folder becomes the **base
folder**. Pressing `Ctrl+F` until the folder view comes back is then a way to go back up to the top.

Components:

- `UI/FileExplorerPanel.cs` — `FocusSearch(int step)` (the cycle), `NextMode`, `ChangeMode`, the
  `Mode` setter, `_openFolder`, `Navigate`, `FolderViewChanged`.
- `UI/MainForm.cs` — `ProcessCmdKey` binds `Ctrl+F` / `Ctrl+Shift+F` to `FocusSearch(±1)`; the
  `FolderViewChanged` handler saves the view and the open folder (`AppSettings.SaveExplorerMode`).

---

## Behaviour

| Route to the folder view | Open folder |
|---|---|
| The `Ctrl+F` / `Ctrl+Shift+F` cycle, the search box focused | **The base folder** (`""`) |
| The views drop-down | Unchanged — the folder left last, as today |
| The first `Ctrl+F` (the box not focused, or the panel collapsed) | Unchanged — the view does not change |
| Start-up | Unchanged — the remembered open folder |

- **Both directions**: `Ctrl+Shift+F` arriving on Folders resets it too — any arrival through the
  cycle.
- **The folder left stays in sight**: the base's subfolder on the way to the folder left is
  selected, as ↑ and the breadcrumb select the folder below (nothing selected when the folder left
  was the base itself).
- **The search text is kept**, selected again, as the cycle keeps it today: a search typed there
  then runs over the whole base folder.
- **Only the cycle** resets the open folder; picking Folders in the views drop-down keeps today's
  behaviour, so the folder the user was browsing is found again there.
- **Remembered as any navigation**: the base folder becomes the open folder, and it is what
  `settings.json` keeps (`FolderViewChanged` → `AppSettings.SaveExplorerMode`) — the folder left
  before the `Ctrl+F` is not kept anywhere.
- Already at the base folder, nothing differs from today.
- This is the one place a view reached by `Ctrl+F` differs from one picked in the drop-down, which
  `workfiles/20261029-ctrl-f-view-cycle.md` § Keyboard stated as identical: it still goes through
  `ChangeMode`, the open folder set first.

### Planned code

- In `FocusSearch`, the next mode computed once; when it is `ExplorerMode.Folders`, `_openFolder`
  is set to `""` before `ChangeMode`, so the `Mode` setter's `RefreshRows` lists the base folder and
  `FolderViewChanged` saves it. The base's subfolder on the way to the folder left
  (`Path.Combine(_baseFolder, <its first segment>)`) is handed to the listing as its selection, the
  way `GoUp` hands `Navigate` its `select`. The search box is not touched (no `Navigate`, which
  clears it).
- As built: the `Mode` setter's body moved into `SetMode(ExplorerMode, string? select)`, which
  `ChangeMode` calls with its new optional `select`, so the listing selects the folder in one
  refresh. Without a base folder (the invitation shows), the open folder is still reset and
  nothing is selected.

---

## Documentation

- `SHORTCUTS.md` / `SHORTCUTS.fr.md` — the `Ctrl+F` row (*pressed again there, the next view*): the
  folder view reached that way opens at the base folder. In the **same commit as the code**
  (RULES.md § Keyboard Shortcuts).
- `README.md` / `README.fr.md` — § File explorer, the search box's key bullet: the folder view
  reached by the cycle opens at the base folder, so pressing it until Folders comes back goes back
  up to the top.
- `GLOSSARY.md` / `GLOSSARY.fr.md` — the *Open folder* row: the base folder again when the
  `Ctrl+F` cycle arrives on the folder view.
- RULES.md unchanged: no rule names the views' cycle.

---

## Test Impact

The app has **no unit test project** (`src/` holds `ImageGridFusion` only), and the change lives in
a WinForms control's key handling: nothing testable is added. Checked by hand in the app instead.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [x] ~~`Ctrl+Shift+F` (the cycle backward) arriving on the folder view: the base folder too?~~ → Yes,
      any arrival on Folders through the cycle, whatever its direction
- [x] ~~The selection once at the base folder?~~ → The base's subfolder on the way to the folder left,
      as ↑ and the breadcrumb do
- [x] ~~The search box's text: kept or cleared?~~ → Kept and selected again, as the cycle does

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-02

Initial design from the user's request and the scoping batch (Q&A 1–4): arriving on the folder view
through the `Ctrl+F` cycle opens the base folder; the views drop-down keeps today's behaviour; the
base folder is what is remembered. Exploration (one direct pass, the subject straightforward): the
cycle is `FileExplorerPanel.FocusSearch` → `ChangeMode(NextMode(step))`, the open folder
`_openFolder`, saved through `FolderViewChanged`. No row of `TODO-FEATURES.md` matches. Three open
questions left: the backward cycle, the selection, the search text.

### Iteration 2 — 2026-11-02

Open questions answered (Q&A 5–7): `Ctrl+Shift+F` resets the open folder too; the base's subfolder
on the way to the folder left is selected; the search text is kept. § Behaviour and § Planned code
updated. No open question left.

### Iteration 3 — 2026-11-02 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/ctrl-f-cycle-folder-root`,
branch `feature/ctrl-f-cycle-folder-root`).

### Iteration 4 — 2026-11-02 — 🧭 Implementation choices

- **No rule broken.**
- **One refresh with the selection**: the `Mode` setter's body became `SetMode(value, select)`, and
  `ChangeMode` takes an optional `select` — rather than a second `RefreshRows(select:)` after the
  mode change, which would have listed the folder twice.
- **The folder left's subfolder** is the first segment of the open folder (`SegmentsOf(...)[1]`),
  combined with the base folder, the way `OpenFolderAtDepth` builds its selection.
- **Without a base folder**, the open folder is still reset (nothing is listed to select in).
- **Not checked in the app by the agent**: the worktree's exe has no `settings.json` next to it, so
  no base folder nor index — the user's hand test covers it.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-11-02 | `FileExplorerPanel.FocusSearch`, `SetMode`, `ChangeMode(select)`; built, not checked in the app by the agent |
| Unit tests | 3 | 2026-11-02 | Not applicable: no test project |
| README | 3 | 2026-11-02 | EN + FR, § File explorer, the search box's key |
| SHORTCUTS | 3 | 2026-11-02 | EN + FR, `Ctrl+F` and `Ctrl+Shift+F` rows, in the code commit |
| GLOSSARY | 3 | 2026-11-02 | EN + FR, *Open folder* row |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | The cycle arriving on the folder view: where does it open? | The base folder, whatever the depth left | 2026-11-02 |
| 2 | Only the `Ctrl+F` cycle, or the views drop-down too? | `Ctrl+F` only — the drop-down keeps the folder left | 2026-11-02 |
| 3 | What is remembered between sessions after a reset? | The base folder, as for any navigation | 2026-11-02 |
| 4 | Straightforward or tricky / long? | Straightforward — one scout pass | 2026-11-02 |
| 5 | `Ctrl+Shift+F` arriving on the folder view: the base folder too? | Yes, both directions | 2026-11-02 |
| 6 | The selection once at the base folder? | The base's subfolder on the way to the folder left | 2026-11-02 |
| 7 | The search box's text: kept or cleared? | Kept, as the cycle does | 2026-11-02 |

---

*Last updated: 2026-11-02*
