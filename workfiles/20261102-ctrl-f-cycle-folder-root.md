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
| The `Ctrl+F` cycle, the search box focused | **The base folder** (`""`) |
| The views drop-down | Unchanged — the folder left last, as today |
| The first `Ctrl+F` (the box not focused, or the panel collapsed) | Unchanged — the view does not change |
| Start-up | Unchanged — the remembered open folder |

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
  `FolderViewChanged` saves it. The selection and the search text: see § Open Questions.

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

- [ ] `Ctrl+Shift+F` (the cycle backward) arriving on the folder view: the base folder too?
      Proposed: yes — any arrival on Folders through the cycle, whatever its direction.
- [ ] The selection once at the base folder: the base's subfolder on the way to the folder left
      selected (as ↑ and the breadcrumb do), or nothing selected? Proposed: that subfolder selected.
- [ ] The search box's text: kept as the cycle keeps it today (a search then runs over the whole
      base folder), or cleared as a navigation clears it? Proposed: kept, as the cycle does.

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project |
| README | | | |
| SHORTCUTS | | | With the code commit |
| GLOSSARY | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | The cycle arriving on the folder view: where does it open? | The base folder, whatever the depth left | 2026-11-02 |
| 2 | Only the `Ctrl+F` cycle, or the views drop-down too? | `Ctrl+F` only — the drop-down keeps the folder left | 2026-11-02 |
| 3 | What is remembered between sessions after a reset? | The base folder, as for any navigation | 2026-11-02 |
| 4 | Straightforward or tricky / long? | Straightforward — one scout pass | 2026-11-02 |
| 5 | `Ctrl+Shift+F` arriving on the folder view: the base folder too? | | 2026-11-02 |
| 6 | The selection once at the base folder? | | 2026-11-02 |
| 7 | The search box's text: kept or cleared? | | 2026-11-02 |

---

*Last updated: 2026-11-02*
