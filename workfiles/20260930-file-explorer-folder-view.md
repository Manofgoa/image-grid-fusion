# File Explorer Folder View

> Working document — a view of the file explorer that browses the base folder as openable folders.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer (`UI/FileExplorerPanel.cs`, `UI/ThumbnailGrid.cs`, `Explorer/FileIndex.cs`,
`Explorer/FileSearch.cs`) shows today either the **favorites** (search box empty), **every file**
(`*`) or the **matches** of the typed words, over the whole base folder. Its files are never seen
by folder.

This task adds a **folder view**: an option of the panel that shows the content of one folder of the
base folder — its subfolders as **folder tiles**, then its files as the usual tiles — a folder tile
opening that folder, a breadcrumb and an ↑ button going back up. The search box stays and searches
**the open folder and its subfolders**.

Components concerned:

| Component | Role today | Change |
|---|---|---|
| `UI/FileExplorerPanel.cs` | Search box, status and caption lines, the grid, the size slider; builds the list in `RefreshRows` | The view toggle, the breadcrumb row, the folder list, the scoped search |
| `UI/ThumbnailGrid.cs` | Paints and hit-tests the tiles of `ExplorerRow(FullPath, Name)` | A folder tile: its look, no heart, its double-click |
| `Explorer/FileSearch.cs` | `All` and `Search` over `IndexEntry` lists | The folder listing and the search limited to a folder, from the index |
| `UI/AppSettings.cs` | The explorer's settings in `settings.json` | The view and the open folder remembered |
| `UI/MainForm.cs` | Saves the explorer's settings on its events | Saves the new two |

---

## The Folder View

- A **view** of the file explorer, next to the current one — called the **search view** from now on
  (favorites / `*` / matches). The user switches between the two with a toggle (placement: see
  Open Questions).
- In the folder view, the list shows the **open folder** — the base folder itself at first.
- The view and the open folder are **remembered between sessions** (Q&A #4), in `settings.json`
  through `UI/AppSettings.cs` (RULES.md § App Settings).
- Without a base folder, the folder view shows the invitation (*Choose folder…*), as a typed search
  does today.

### Layout (disposition A — Q&A #1)

```
┌ Files                        » ┐
│ [📁] [Search files…      ] [↻] │   ← the view toggle (placement open)
│ [↑] Base › Vacances › 2025     │   ← breadcrumb row (placement open)
│ 1 234 files · indexed 14:02    │   ← status line
│ ┌────────┐┌────────┐┌────────┐ │
│ │ folder ││ folder ││  file  │ │   ← folder tiles first, then files
│ └Plage───┘└Montagne┘└img01───┘ │
│ ▭ ─────────○────────────── ▭   │   ← tile size slider, unchanged
└────────────────────────────────┘
```

- **One grid**: the folder tiles and the file tiles share it, at the same size, loaded load by load
  like any list (§ Load, GLOSSARY).
- The **breadcrumb** names the base folder, then every folder down to the open one; each segment
  but the last is clickable and opens that folder. The **↑** button opens the parent folder; it is
  disabled at the base folder.

---

## Folder Content

Read **from the index only**, never the disk (GLOSSARY § Index): the folders are the path segments of
the index's entries.

- The **subfolders** of the open folder: the distinct next segments of the entries under it.
- Its **files**: the entries directly in it.
- **Order** (Q&A #3): the folders first, **A→Z** by name; then the files, **the most recently
  created first**, then by name — the order of `*`.
- A folder holding no file anywhere below it is **not in the index**, so it does not appear (see
  Open Questions).
- The caption line says what the list holds, e.g. `2025 — 2 folders, 38 files`.

---

## Opening and Going Up

| Action | Does |
|---|---|
| Double-click on a folder tile | Opens that folder: its content from the top, the search box cleared |
| Enter on a selected folder tile | Same |
| A breadcrumb segment clicked | Opens that folder |
| **↑** clicked | Opens the parent folder |
| Keyboard way up | See Open Questions |

- Double-click / Enter on a **file tile** still adds the file, like Add images.
- Going back up **selects the folder just left** and scrolls it into view, as Explorer does.

---

## Search in the Folder View

Q&A #2: the search is **limited to the open folder and its subfolders**.

- **Box empty**: the open folder's content (§ Folder Content) — not the favorites; the favorites are
  the search view's.
- **Words typed**: the matches of `FileSearch.Search` among the entries under the open folder, in
  its ranking; the words match the path **relative to the base folder**, as today.
- **`*`**: every file under the open folder, the most recently created first.
- The breadcrumb stays; the caption says `12 results in 2025`.
- Whether folders are among the results: see Open Questions.

---

## Folder Tiles

- A folder tile has the tile's box and its name below; its look is open (see Open Questions).
- Favorites never hold folders (`AddFavorites` skips them): whether a folder tile gets a heart, and
  what dragging it does, are open (see Open Questions).
- The context menu's *Open file location* on a folder tile: see Open Questions.

---

## Refreshes

- A **rescan** (at start-up or ↻) keeps the open folder and the place, like any refresh that keeps
  the place.
- The open folder **no longer in the index** (deleted, renamed, emptied) — at a rescan or at
  start-up from the remembered one: see Open Questions.
- The **base folder changed** from the ⚙ menu: the folder view opens at its root.

---

## Documentation Impact

| Document | Change |
|---|---|
| `README.md` | The file explorer section: the folder view, the toggle, the breadcrumb, the scoped search |
| `GLOSSARY.md` | *File explorer* updated; new terms *Folder view*, *Search view*, *Open folder*, *Folder tile*, *Breadcrumb* |
| `RULES.md` | None expected |

---

## Test Impact

The solution has **no test project** (`ImageGridFusion.slnx` holds the app alone), and the earlier
workfiles left it so by decision. Nothing is automated: the behaviours below are checked by hand in
the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A folder lists its subfolders A→Z, then its files newest first | — (manual) | — |
| Double-click / Enter opens a folder; ↑ and the breadcrumb go up, the folder left selected | — (manual) | — |
| The search and `*` only return files under the open folder | — (manual) | — |
| The view and the open folder come back after a restart | — (manual) | — |
| A rescan keeps the open folder and the place | — (manual) | — |

---

## Open Questions

- [ ] Where does the view toggle go — a button left of the search box, or in the header next to *Files*?
- [ ] Where does the breadcrumb go — a row of its own under the search box, or in place of the caption line?
- [ ] What does a folder tile look like — Windows' folder thumbnail, a drawn folder glyph, or a mosaic of its first images? With its file count?
- [ ] A folder tile: a heart? Draggable onto a cell (and then what)? What does its context menu offer?
- [ ] Which key goes up — Backspace, Alt+↑, both?
- [ ] The open folder no longer in the index: back to the nearest parent still there, or to the base folder?
- [ ] Folders without any file below them are invisible (the index holds files only): acceptable, or should the view read the disk?
- [ ] A search in the folder view: files only, or matching folders too?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-30

Initial design from the scoping pass: four dispositions drawn inline (A tiles + breadcrumb, B tree
above, C tree beside, D inline folders), the user chose **A**. The search in the folder view is
limited to the open folder and its subfolders; folders come first A→Z, then files newest first; the
view and the open folder are remembered in `settings.json`. The subject was judged straightforward:
one scout pass over `FileExplorerPanel`, `ThumbnailGrid`, `FileIndex`, `FileSearch`,
`ShellThumbnail` and `AppSettings`. Findings: the index holds files only (folders are derived from
paths), the grid knows one kind of row (`ExplorerRow`), and there is no test project. Eight open
questions listed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — manual verification (see *Test Impact*) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which disposition for the folder view (A tiles + breadcrumb, B tree above, C tree beside, D inline folders)? | A — folder tiles + breadcrumb | 2026-09-30 |
| 2 | In the folder view, what does the search box do? | Limited to the open folder and its subfolders; box empty = the folder's content | 2026-09-30 |
| 3 | In which order is a folder's content shown? | Folders A→Z, then files newest first | 2026-09-30 |
| 4 | What is remembered between sessions? | The view and the open folder | 2026-09-30 |
| 5 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-09-30 |

---

*Last updated: 2026-09-30*
