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
  (favorites / `*` / matches).
- **The toggle** (Q&A #6): a 📁 push button at the **start of the search row**, left of the search
  box; pressed while the folder view shows. Its tooltip names the view it switches to.
- In the folder view, the list shows the **open folder** — the base folder itself at first.
- The view and the open folder are **remembered between sessions** (Q&A #4), in `settings.json`
  through `UI/AppSettings.cs` (RULES.md § App Settings).
- Without a base folder, the folder view shows the invitation (*Choose folder…*), as a typed search
  does today.

### Layout (disposition A — Q&A #1)

```
┌ Files                          » ┐
│ [📁] [Search files…        ] [↻] │   ← the view toggle, pressed
│ 1 234 files · indexed 14:02      │   ← status line, unchanged
│ [↑] Base › Vacances › 2025  (40) │   ← breadcrumb, in place of the caption line
│ ┌────────┐┌────────┐┌────────┐   │
│ │ folder ││ folder ││  file  │   │   ← folder tiles first, then files
│ └Plage (42)Montagne (7)img01─┘   │
│ ▭ ─────────○────────────── ▭     │   ← tile size slider, unchanged
└──────────────────────────────────┘
```

- **One grid**: the folder tiles and the file tiles share it, at the same size, loaded load by load
  like any list (§ Load, GLOSSARY).
- **The breadcrumb** (Q&A #7) takes the **caption line's place** in the folder view — no row added:
  the **↑** button, then the base folder's name and every folder down to the open one, each segment
  but the last clickable and opening that folder; the counts the caption gave follow at its end. Too
  long for the panel, it drops its first segments behind an ellipsis, the open folder always shown.
  **↑** opens the parent folder; it is disabled at the base folder. The search view keeps its caption.

---

## Folder Content

Q&A #12, #14: the open folder is **listed from the disk**, as Explorer lists it — its subfolders and
its files. Every subfolder shows, an empty one included (the index holds files only, so a folder
without any file below it would be missing from it), and a file added since the last scan shows at
once, without ↻. The index serves the search, `*` and the folder tiles' counts only.

- The **subfolders** of the open folder, as on the disk; hidden and system ones skipped, as the scan
  skips them.
- Its **files**: the files directly in it, as on the disk, hidden and system ones skipped; their
  creation dates come with the enumeration, like the scan's.
- **Order** (Q&A #3): the folders first, **A→Z** by name; then the files, **the most recently
  created first**, then by name — the order of `*`.
- The listing is read **off the UI thread**; the status line says so while it takes time.
- The breadcrumb's end says what the list holds, e.g. `2 folders, 38 files`.

---

## Opening and Going Up

| Action | Does |
|---|---|
| Double-click on a folder tile | Opens that folder: its content from the top, the search box cleared |
| Enter on a selected folder tile | Same |
| A breadcrumb segment clicked | Opens that folder |
| **↑** clicked | Opens the parent folder |
| **Backspace** or **Alt+↑** in the grid (Q&A #10) | Opens the parent folder |
| **Alt+↑** in the search box | Same; Backspace there still erases the text |

- Double-click / Enter on a **file tile** still adds the file, like Add images.
- Going back up **selects the folder just left** and scrolls it into view, as Explorer does.

---

## Search in the Folder View

Q&A #2: the search is **limited to the open folder and its subfolders**, from the index.

- **Box empty**: the open folder's content (§ Folder Content) — not the favorites; the favorites are
  the search view's.
- **Words typed** (Q&A #13): the **folders** under the open folder whose name matches come **first**,
  then the matching **files**, each part in `FileSearch.Search`'s ranking; the words match the path
  **relative to the base folder**, as today. The folders are those of the index — the ones holding
  files; a double-click on one opens it (the search box cleared).
- **`*`** (Q&A #16): **every folder** under the open folder first, **A→Z** by their path relative to
  the open folder (so a folder's subfolders follow it), then **every file** under it, the most recently
  created first. The folders are those of the index, as for words typed.
- The breadcrumb stays; its end says `12 results`.

---

## Folder Tiles

- **Look** (Q&A #8): the thumbnail **Windows gives the folder** (`ShellThumbnail`, often a preview of
  its content), a **drawn folder glyph** when it has none. The name is followed by the folder's
  **file count**, e.g. `Plage (42)` (Q&A #15): **every file below it**, its subfolders' included,
  counted from the index — instant, no disk read; a folder the index does not hold (empty, or created
  since the scan) shows `(0)` until the next scan.
- **Actions** (Q&A #9): **no heart** (favorites never hold folders), **not draggable**. Its context
  menu offers **Open in Explorer**, opening the folder itself in Windows Explorer.

---

## Refreshes

- A **rescan** (at start-up or ↻) keeps the open folder and the place, like any refresh that keeps
  the place.
- The open folder **gone from the disk** (deleted, renamed) — at a refresh or at start-up from the
  remembered one (Q&A #11): the view goes up to the **nearest parent still there**, the base folder
  at worst, and the status line says so for a few seconds.
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
| A folder lists its subfolders A→Z — empty ones included — then its files newest first | — (manual) | — |
| A folder tile shows Windows' thumbnail or the glyph, its count, no heart, no drag; *Open in Explorer* | — (manual) | — |
| Double-click / Enter opens a folder; ↑, Backspace, Alt+↑ and the breadcrumb go up, the folder left selected | — (manual) | — |
| The search returns the matching folders, then files, under the open folder only | — (manual) | — |
| `*` lists every folder below the open one A→Z, then every file below it newest first | — (manual) | — |
| A file created since the last scan shows in its folder without ↻; its folder's count lags until the scan | — (manual) | — |
| The open folder deleted: the view goes up to the nearest parent | — (manual) | — |
| The view and the open folder come back after a restart | — (manual) | — |
| A rescan keeps the open folder and the place | — (manual) | — |

---

## Open Questions

- [x] ~~Where does the view toggle go — a button left of the search box, or in the header next to *Files*?~~ → A 📁 push button at the start of the search row
- [x] ~~Where does the breadcrumb go — a row of its own under the search box, or in place of the caption line?~~ → In place of the caption line, the counts at its end
- [x] ~~What does a folder tile look like — Windows' folder thumbnail, a drawn folder glyph, or a mosaic of its first images? With its file count?~~ → Windows' thumbnail, a drawn glyph without one; the file count after the name
- [x] ~~A folder tile: a heart? Draggable onto a cell (and then what)? What does its context menu offer?~~ → No heart, no drag; the menu offers *Open in Explorer*
- [x] ~~Which key goes up — Backspace, Alt+↑, both?~~ → Both in the grid; Alt+↑ in the search box too
- [x] ~~The open folder no longer in the index: back to the nearest parent still there, or to the base folder?~~ → The nearest parent still there
- [x] ~~Folders without any file below them are invisible (the index holds files only): acceptable, or should the view read the disk?~~ → The folder view reads the disk
- [x] ~~A search in the folder view: files only, or matching folders too?~~ → Matching folders too, first
- [x] ~~The folder view reads the disk for the folders: its files too, or from the index?~~ → Its files too: the open folder is listed from the disk
- [x] ~~A folder tile's count: the files directly in it, or every file below it?~~ → Every file below it, from the index
- [x] ~~`*` in the folder view: files only, or every folder below the open one too?~~ → Every folder below it first, then every file

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

### Iteration 2 — 2026-10-01

The eight open questions answered (Q&A #6–13): the toggle is a 📁 push button at the start of the
search row; the breadcrumb takes the caption line's place; a folder tile shows Windows' thumbnail (a
glyph without one) and its file count, has no heart, is not draggable and offers *Open in Explorer*;
Backspace and Alt+↑ go up; a vanished open folder falls back to its nearest parent; the folder view
**reads the disk** so empty folders show; a search in the folder view returns matching folders first.
Reading the disk raises three new questions (what is read from the disk, what the count counts, `*`
with folders), listed as open.

### Iteration 3 — 2026-10-01

The last three questions answered (Q&A #14–16): the open folder is listed **entirely from the disk**
— folders and files — the index serving the search, `*` and the counts; a folder tile counts **every
file below it**, from the index; `*` lists **every folder** below the open one before the files.
Agent's proposal on the latter, open to the user's reading before the go: the folders of `*` are
sorted by their path relative to the open folder, so each one's subfolders follow it. No open
question remains.

### Iteration 4 — 2026-10-01 — ✅ Implemented

Go given for code, tests and documentation (no test project: manual verification). Branch Gate:
**stay on `main`**, the standing choice for this repository.

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
| 6 | Where does the view toggle go? | A 📁 button left of the search box | 2026-09-30 |
| 7 | Where does the breadcrumb go? | In place of the caption line | 2026-09-30 |
| 8 | What does a folder tile look like? | Windows' folder thumbnail (a glyph without one) + file count | 2026-09-30 |
| 9 | A folder tile: heart, drag, context menu? | Context menu only (*Open in Explorer*); no heart, no drag | 2026-09-30 |
| 10 | Which key goes up? | Backspace and Alt+↑ | 2026-10-01 |
| 11 | The open folder no longer in the index: where does the view go? | The nearest parent still there | 2026-10-01 |
| 12 | Folders without files below them: invisible, or read from the disk? | Read the disk | 2026-10-01 |
| 13 | A search in the folder view: files only, or folders too? | Folders too | 2026-10-01 |
| 14 | The folder view reads the disk: its files too, or from the index? | The disk too | 2026-10-01 |
| 15 | A folder tile's count: direct files, or every file below? | Every file below, from the index | 2026-10-01 |
| 16 | `*` in the folder view: files only, or folders too? | Folders too | 2026-10-01 |

---

*Last updated: 2026-10-01*
