# Open Exe Folder

> Working document — a ⚙ menu item opening the running exe's folder in Explorer.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app keeps its data next to the exe — `settings.json`, `files.index`, `favorites.txt`,
`favorites-from-pasted\` (RULES.md § App Settings) — and several copies of the exe can run side by
side, each with its own data. Nothing in the app shows where the running one lives.

A new item of the **⚙ menu** (`MainForm._settingsMenu`) opens Explorer on the folder of the
**running exe**, the exe selected.

Components: `src/ImageGridFusion/UI/MainForm.cs` (the menu, `ShowInExplorer`), `README.md` /
`README.fr.md` (§ Tray & startup).

---

## Menu Item

- Label: **Open app folder**.
- Placed **at the end of the ⚙ menu, after a separator**: it is an action, not a setting, so it
  stands apart from the settings above it (the separator follows the last group of the menu as it
  stands when implemented).
- In the **⚙ menu only** — not in the tray icon's menu (*Open* / *Quit*).
- Always enabled, with a tooltip: *Opens Explorer on the folder of this exe, the exe selected — where
  settings.json and the app's other files live*.

## Behaviour

- Click → Explorer opens on the folder of the **running exe**, `ImageGridFusion.exe` selected
  (`explorer.exe /select,"<path>"`), as *Open file location* does in the file explorer.
- The path is the running process's own (`Environment.ProcessPath`), so a worktree's build or a
  copy of the exe elsewhere opens its own folder — not `AppContext.BaseDirectory` assumptions.
- It reuses `MainForm.ShowInExplorer(path)`, which already selects a file, falls back to its folder
  and reports an Explorer failure in the status line — no second implementation.
- `Environment.ProcessPath` null (not expected for a WinForms exe) → falls back to
  `Application.ExecutablePath`.

## Documentation

- `README.md` § Tray & startup, the line listing what the ⚙ menu holds: ends with **Open app folder**,
  opening Explorer on the running exe's folder, the exe selected — where `settings.json` and the
  other data files live. `README.fr.md` mirrored in the same commit.
- No new glossary term, no RULES.md change: it is an action of the ⚙ menu, not a setting, an effect
  nor a new rule.

---

## Test Impact

Nothing testable changes: the app has no unit test project, and the item only starts Explorer
through the existing `ShowInExplorer`. Checked by hand on the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (none: shell action, no test project) | — | — |

---

## Open Questions

- [x] ~~What does the item open?~~ → The exe's folder, the exe selected (`/select`)
- [x] ~~Where in the ⚙ menu, with which label?~~ → At the end, after a separator, *Open app folder*
- [x] ~~Also in the tray icon's menu?~~ → No, the ⚙ menu only
- [ ] The README's line listing the ⚙ menu does not name its *File contents* group (*Search file
  contents (OCR)*, *Rebuild content index*), added by another workfile — complete it? (found during
  the run, out of scope)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the request ("in the general options menu, add an item opening the current
exe's folder in Explorer") and the scoping answers: *Open app folder* at the end of the ⚙ menu after
a separator, Explorer on the running exe's folder with the exe selected, through the existing
`ShowInExplorer`; ⚙ menu only; README EN / FR updated.

### Iteration 2 — 2026-10-07 — ✅ Implemented

Go given: code, tests and documentation, in a dedicated worktree (`feature/open-exe-folder` under
`.claude/worktrees/`), fast-forwarded into `main` and removed at the end.

### Iteration 3 — 2026-10-07 — 🧭 Implementation choices

- The item got a **tooltip**, like every other item of the ⚙ menu (the design did not state one).
- The separator and the item follow the menu's last group as it stood: *File contents*.
- The README sentence is appended to the existing line listing the ⚙ menu, rather than a new bullet:
  the bullets under that line belong to *Start with Windows*.
- No rule broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 2 | 2026-10-07 | `MainForm._openAppFolder`, through `ShowInExplorer(Environment.ProcessPath)` |
| Unit tests | — | — | Not applicable: no test project, shell action only |
| README | 2 | 2026-10-07 | § Tray & startup, EN and FR |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does the item do once clicked? | Opens the exe's folder, the exe selected | 2026-10-07 |
| 2 | Where in the ⚙ menu, with which label? | At the end, after a separator — *Open app folder* | 2026-10-07 |
| 3 | Also in the tray icon's menu (Open / Quit)? | No, the ⚙ menu only | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — single scout pass | 2026-10-07 |
| 5 | Start implementing the workfile? | Code, tests and documentation — in a separate worktree | 2026-10-07 |

---

*Last updated: 2026-10-07*
