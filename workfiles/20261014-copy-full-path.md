# Copy Full Path

> Working document — a **Copy full path** entry in the right-click menu of the file explorer's tiles.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer's tiles and Details rows already have a right-click menu
(`FileExplorerPanel._menu`): **Copy**, **Copy PNG**, a separator and **Open file location** on a
file, **Open in Explorer** alone on a folder (`workfiles/20261008-explorer-tile-copy-menu.md`).
This task adds a **Copy full path** entry putting the full path of the tile's file — or folder —
on the clipboard as **text**.

Components: `UI/FileExplorerPanel.cs` (the menu, its `Opening` handler, the status line through
`ShowTransient`, `Exists`), `UI/ThumbnailGrid.cs` (`RowAt`; a right-click already selects the
tile under it). Docs: `README.md` / `README.fr.md` § file explorer.

---

## Menu

| Right-clicked | Entries |
|---|---|
| File tile or row (search view, favorites, folder view) | Copy · Copy PNG · **Copy full path** · separator · Open file location |
| Folder tile or row (folder view) | **Copy full path** · separator · Open in Explorer |
| Loading… slot, empty space | No menu (unchanged: `RowAt` gives no row, the opening is cancelled) |

- **Label**: `Copy full path`.
- It acts on the **tile right-clicked**, which the right-click selects (existing behaviour), like
  the other entries.
- On a folder tile, the separator now shows too, between Copy full path and Open in Explorer.
- **No keyboard shortcut**: the menu is the only way in.

## What Is Copied

- The row's **`ExplorerRow.FullPath`**, **raw**: `C:\Images\photo.png`, no quotes, as **text** on
  the clipboard (`Clipboard.SetText`, Unicode text; `FileExplorerPanel.CopySelectedPath`) — pastable in a text field, a terminal, a chat.
- A folder: its full path, without a trailing `\`.
- The tile's file or folder is checked first with `Exists`, as Copy and Open file location do: a
  missing one is removed from the index and the list, the status line saying so, and nothing is
  copied.
- **Feedback**: the status line, through `ShowTransient`, `Copied path: <full path>`; a clipboard
  failure (`ExternalException`) shows `Copy failed: <message>` as an error, as Copy does.

---

## Test Impact

None: the change is UI wiring — a menu entry, the clipboard, the status line — with no new pure
logic to pin, as for `workfiles/20261008-explorer-tile-copy-menu.md`. Checked by hand in the
launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no testable logic) | — | — |

---

## Open Questions

- [x] ~~On which explorer items does the entry show?~~ → File tiles and rows **and** folder tiles
  and rows.
- [x] ~~Path format?~~ → Raw, no quotes.
- [x] ~~Label?~~ → `Copy full path`.
- [x] ~~Place in the menu: after Copy PNG (in the copies' group, before the separator), or with Open
  file location after the separator?~~ → In the copies' group, after Copy PNG; on a folder tile, the
  separator shows between it and Open in Explorer.
- [x] ~~A keyboard shortcut (e.g. `Ctrl+Shift+C` with the tiles focused), or the menu only?~~ → No
  shortcut: the menu only (confirmed by the user after a first « no preference »).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-14

Scoping: the entry shows on files and folders, copies the raw path, is labelled `Copy full path`;
the subject is simple, explored in one pass. Exploration found the existing menu and its `Opening`
handler hiding the copies on a folder tile, `ExplorerRow.FullPath` holding the absolute path,
`Exists` guarding the other entries, `ShowTransient` for feedback, and no tested logic in that
menu. Proposed: the entry in the copies' group, the separator shown on folder tiles too, the path
as text, `Copied path: …` in the status line, no shortcut.

### Iteration 2 — 2026-10-14

The entry goes in the copies' group, after Copy PNG, the separator shown on folder tiles too (as
proposed). No preference on a keyboard shortcut at first, then confirmed by the user: no shortcut, the menu
only. No open question left.

### Iteration 3 — 2026-10-14 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree on `feature/copy-full-path`
(`.claude/worktrees/copy-full-path`). The scope is the design sections as they stand.

### Iteration 4 — 2026-10-14 — 🧭 Implementation choices

No divergent choice: the entry, its place, the separator on folder tiles, the raw text copy, the
`Exists` check and the status line messages are as designed. The text goes through
`Clipboard.SetText`, the plain form of the Unicode text format the design named. No rule broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-14 | `FileExplorerPanel`: the `Copy full path` entry, its `Opening` visibility, `CopySelectedPath` — built, 0 warnings |
| Unit tests | 3 | 2026-10-14 | Not applicable: no testable logic (see § Test Impact) |
| README | 3 | 2026-10-14 | `README.md` and `README.fr.md` § file explorer: Copy full path on file tiles and folder tiles |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | On which explorer items does the entry show? | Files and folders | 2026-10-14 |
| 2 | Path format: raw or quoted? | Raw | 2026-10-14 |
| 3 | Label? | Copy full path | 2026-10-14 |
| 4 | Is the subject simple, or tricky / long? | Simple | 2026-10-14 |
| 5 | Place in the menu? | With the copies, after Copy PNG | 2026-10-14 |
| 6 | A keyboard shortcut, or the menu only? | No preference at first, then « Pas de raccourci clavier »: the menu only | 2026-10-14 |

---

*Last updated: 2026-10-14*
