# Explorer Tile Copy Menu

> Working document — **Copy** and **Copy PNG** in the right-click menu of the file explorer's tiles.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer's tiles (search view, favorites, folder view) already have a right-click menu,
holding a single entry: **Open file location** on a file tile, **Open in Explorer** on a folder
tile (`FileExplorerPanel._menu`, `_openLocation`). This task adds two entries for **file tiles**:

- **Copy** — puts the **file itself** on the clipboard, as Ctrl+C does in the Windows Explorer: it
  can then be pasted into a folder, a chat app, a browser.
- **Copy PNG** — puts the **image** of the file on the clipboard, as the app shows it when the
  file arrives in a cell: a still image decoded, the frame of a video or an animated GIF, the
  rendered page of a PDF or a text.

Components: `UI/FileExplorerPanel.cs` (the menu, its `Opening` handler, the status line through
`ShowTransient`), `UI/ThumbnailGrid.cs` (a right-click already selects the tile under it,
`OnMouseDown` → `Select`), `Imaging/ImageLoader.TryLoadFile` (the image a cell shows), the grid's
own Copy in `UI/MainForm.cs` (the clipboard formats a PNG is given there).

---

## Menu

| Tile | Entries |
|---|---|
| File | **Copy** (shortcut shown: `Ctrl+C`), **Copy PNG**, a separator, **Open file location** |
| Folder | **Open in Explorer** only, as today — Copy and Copy PNG **hidden** |
| Loading… slot, empty space | No menu (as today: `RowAt` gives no row, the opening is cancelled) |

- The menu acts on the **tile right-clicked**, which the right-click selects (existing behaviour).
- **Ctrl+C** with the **tiles focused** (`ThumbnailGrid`) does **Copy** on the selected file tile —
  the menu's Copy, not the grid's. `MainForm.ProcessCmdKey` hands it to the explorer, next to the
  search box's own whitelist (`IsEditingText`). With the tiles focused, Ctrl+C belongs to them: a
  folder tile, or no tile, selected → nothing is copied, the grid neither. Anywhere else, Ctrl+C
  copies the grid, as today. Copy PNG has no shortcut.
- A file gone from the disk: both entries go through `Exists(row)`, as Open file location does —
  the file leaves the index and the favorites, the status line says so, nothing is copied.

## Copy

- The clipboard gets a **file drop list** holding the tile's full path
  (`DataObject.SetFileDropList`), set with `copy: true` so it stays after the app exits.
- Status line, for a few seconds (`ShowTransient`): `Copied: <file name>`.
- A clipboard failure (`ExternalException`): `Copy failed: <message>`, as an error.

## Copy PNG

- The image is **`ImageLoader.TryLoadFile(path)`'s bitmap** — what the cell shows when the file
  arrives in it — at its **full size**, no effect applied (no crop, zoom, background…: the file
  alone, not a cell). For a video, that is the frame at **10 % of its length**
  (`VideoFrames.InitialPage`), not its often black first frame; for an animated GIF, a PDF or a
  text, their initial page.
- Loaded **off the UI thread** (`TryLoadFile` blocks on file and WinRT calls), the clipboard set
  back on the UI thread; the `SourceImage` disposed once copied.
- The clipboard formats are **those of the grid's Copy as PNG** (`MainForm`): a standard bitmap
  flattened on white (`Compositor.Flattened`) for most apps, plus the `"PNG"` format keeping the
  transparency, which browsers paste more reliably.
- Status line: `Copied as PNG: <file name> (<width> × <height>)`.
- A file with no preview at all (`TryLoadFile` gives null): `No image to copy: <file name>`, as an
  error. The entry is not disabled beforehand — knowing it means loading the file.

---

## Test Impact

The repository has **no test project** (`src/` holds `ImageGridFusion` only). The change is UI
wiring — a menu, the clipboard, an existing loader — with no new pure logic to pin.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| None — no test project, nothing testable without the clipboard and the UI | — | — |

---

## Open Questions

- [x] ~~For a video, Copy PNG takes the frame the cell shows on arrival — at 10 % of its length
  (`VideoFrames.InitialPage`) — or the very first frame (often black)?~~ → As the cell: at 10 %
- [x] ~~On a folder tile: Copy and Copy PNG hidden (only Open in Explorer stays), or shown
  disabled?~~ → Hidden
- [x] ~~Ctrl+C on a selected tile (the tiles focused): does it do Copy, or is the menu the only
  route?~~ → Ctrl+C does Copy while the tiles are focused; the grid's Ctrl+C everywhere else
- [x] ~~Menu order: Copy, Copy PNG, separator, Open file location — or Open file location
  first?~~ → Copy first

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, from the scoping answers (Q&A 1–3): Copy puts the file on the clipboard, Copy PNG
the image as a cell shows it (a frame, a rendered page for a video, a GIF, a PDF, a text), file
tiles only. Exploration found the existing menu (Open file location / Open in Explorer), the
right-click already selecting the tile, the grid Copy's PNG clipboard formats, and no test
project. Four points left open, above.

### Iteration 2 — 2026-10-08

The four open points settled (Q&A 5–8): a video's frame at 10 % as the cell shows it, Copy / Copy
PNG hidden on folder tiles, Ctrl+C doing Copy while the tiles are focused, Copy first in the menu.
Added from reading `MainForm.ProcessCmdKey`: with the tiles focused, Ctrl+C belongs to them — a
folder tile or none selected copies nothing, not the grid; the menu's Copy shows its `Ctrl+C`.

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree on `feature/explorer-tile-copy-menu`.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository — see § Test Impact |
| README | | | `README.md` and `README.fr.md` § file explorer: the right-click menu's entries, Ctrl+C on a tile |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does Copy put on the clipboard for a file tile? | The file itself, as Ctrl+C in the Windows Explorer | 2026-10-08 |
| 2 | What does Copy PNG do on a tile that is not a still image (video, animated GIF, PDF, text)? | Its first frame / preview, as the app shows it in a cell | 2026-10-08 |
| 3 | Does the menu apply to folder tiles too? | No, file tiles only | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-08 |
| 5 | For a video, Copy PNG takes the frame at 10 % (as the cell) or the very first one? | As the cell, at 10 % | 2026-10-08 |
| 6 | On a folder tile, Copy / Copy PNG hidden or disabled? | Hidden | 2026-10-08 |
| 7 | Ctrl+C on a selected tile does Copy? | Yes, while the tiles are focused | 2026-10-08 |
| 8 | Menu order? | Copy, Copy PNG, separator, Open file location | 2026-10-08 |

---

*Last updated: 2026-10-08*
