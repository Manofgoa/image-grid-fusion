# File Explorer

> Working document — a collapsible file explorer panel at the right of the preview: a search box
> over a base folder and its subfolders, served by an index cached next to the exe, favorites marked
> with a heart, and rows dragged into the cells.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, files reach the grid from the Explorer (drop), the clipboard (Ctrl+V), the *Add images*
picker and the command line: the user browses to them each time. This work adds a **file explorer
panel** inside the app: a **base folder** chosen once in the settings, every file under it recorded
in an **index** cached next to the exe, a **search box** that suggests the 10 best matches as the
user types, **favorites** marked with a heart, and rows dragged into the cells exactly like a drop
from the Explorer.

Components concerned (line numbers as of 2026-09-26):

| Component | Role today | Change |
|---|---|---|
| `UI/MainForm.cs` — constructor, l.261–270 | Dock chain: `_preview` (Fill), `_layouts` (Left, 80 px), the rows Top / Bottom | Adds the panel, `Dock = Right`, right after `Controls.Add(_preview)` |
| `UI/MainForm.cs` — `AddFilesAsync(paths, targetCell)`, l.761 | The one entry point of every file arrival: `-1` appends like *Add images*, `≥ 0` replaces that cell | Called for an activated row; a dragged row reaches it through `OnDragDrop` (l.612) as a `FileDrop` |
| `UI/MainForm.cs` — `OnDragEnter` l.590, `OnPreviewDragOver` l.604 | Accept any `FileDrop`, highlight the cell under the pointer; no check on the drag's source | Untouched: a drag started inside the app is a `FileDrop` like any other |
| `UI/MainForm.cs` — `_settingsMenu` l.277, `ShowSettings()` l.1260, `PickBorderColor()` ~l.1740 | The ⚙ menu, its items read from the registry on demand, a dialog per item | New item *File explorer folder…* |
| `UI/MainForm.cs` — `OnShown` l.519, `ExportAnimationAsync` l.1089 | Startup work with `Task.Run` + `async/await`; progress through `Progress<T>` | Starts the index load, then the background rescan |
| `UI/AppSettings.cs` | Registry `HKCU\Software\ImageGridFusion`: a typed getter with a default + a `SaveXxx` per value | Two values: the base folder, the panel's open state |
| `UI/GridPreview.cs` — `CellAt` l.314, `DropZoneBounds` l.1172 | Maps a drop point to a cell, or `-1` on the *Add images* zone | Untouched |
| `README.md`, `GLOSSARY.md` | Documentation | A section and four terms |

New files: `UI/FileExplorerPanel.cs` (the panel), `Explorer/FileIndex.cs` (the cache: scan, load,
save, remove), `Explorer/FileSearch.cs` (folding, matching, ranking), `Explorer/Favorites.cs`.

The internal ✥ swap between cells is not OLE-based (no `DoDragDrop` anywhere in `src/`), so a drag
started from the panel cannot collide with it. The app's UI texts are in English: so are the
panel's.

Related: `workfiles/20260926-remember-last-folder.md` (designed, not implemented: it chose a JSON
settings file — this work keeps the registry store that exists, see § Settings);
`workfiles/20260926-ocr-search.md` (pre-created follow-up: the search will also look inside the
images, see § Index File).

---

## Panel

Agreed:

- A panel **at the right of the preview**, between the toolbars above and the Global effects row
  below (the band `_preview` and `_layouts` share), **collapsible, open at start-up**; its open /
  closed state is **remembered between sessions** (§ Settings).
- **Fixed width**: 280 logical px open (`LogicalToDeviceUnits`), a **20 px strip** when collapsed.
  The preview (`Dock = Fill`) shrinks by that width; nothing else moves.
- **Collapse / expand**: a `»` button in the panel's header hides it; the strip left in its place
  carries a `«` button (tooltip *Show the file explorer*) that brings it back and focuses the
  search box. Collapsed, the panel keeps its search text, results and state.
- Contents, top to bottom:

  | Row | Content |
  |---|---|
  | Header | *Files* label, the `»` button at the right |
  | Search | The search box (placeholder *Search files…*), the `↻` button beside it (tooltip *Rescan the folder*; disabled while a scan runs) |
  | Status | One line: the scan progress, else the index summary — *346 files · indexed 21:03*, *No base folder*, or an error |
  | Caption | *Favorites (12)* while the box is empty; *57 results — first 10* / *3 results* / *No result* during a search |
  | List | The rows (favorites, or the results), filling the rest of the height |

- **A row** = the heart (`♡` grey, `♥` red when a favorite) in front of the **file name only**;
  the full path in a tooltip. Owner-drawn list; the heart's hit zone is the first 20 logical px.
- **Box empty (or blanks only)**: the list shows **every favorite** — no 10-limit — the **most
  recently added first**. **Box with text**: the 10 best matches (§ Search), the favorites among
  them marked with `♥` and **not promoted**.
- **No base folder yet**: the panel opens normally (its list empty, favorites aside), the status
  line saying *No base folder*. **At the first keystroke**, the list area is replaced by an
  invitation that explains why: *The search looks through one folder and its subfolders. Choose it
  to index your files.* with a **Choose folder…** button — the same action as the ⚙ menu item.
  Once a folder is chosen, the scan starts (§ Index File) and the pending search runs on it.
- **While exporting**: the panel stays usable (search, favorites, drag); a drop on the grid follows
  the cells' existing rule during an export. Nothing new.

---

## Search

Agreed:

- **Runs at each keystroke**, synchronously, from the in-memory index (no disk read): the first
  10 rows appear as the user types. Empty box → the favorites (§ Panel).
- **Folding**, applied once to every index entry when it is loaded or scanned, and to the query:
  Unicode decomposition (`FormD`), the combining marks dropped, then lower-case invariant. So
  **accents and case are ignored**: *ete* matches *Été.jpg*.
- **Words**: the query split on blanks; **every word must appear** (substring) in the folded
  **relative path** of the entry — file name *and* subfolders: *vacances chat* finds
  `Vacances 2025\chat.jpg`, *chat noir* finds `Photo_Chat-Noir.jpg`.
- **Ranking — best match first**, ties broken in order:

  | # | Criterion | Best |
  |---|---|---|
  | 1 | Number of words found in the **file name** itself (not only in its folders) | More |
  | 2 | Position of the first query word in the file name (a name that *starts* with it beats one that contains it) | Earlier |
  | 3 | Length of the file name | Shorter |
  | 4 | Relative path, ordinal, case-insensitive | A → Z |

- **10 results shown**; the caption tells how many matched in all (§ Panel). Favorites get no
  bonus.
- **Enter in the search box** activates the first row (§ Interactions); **↓** moves the focus to
  the list.

---

## Index File

Agreed:

- **Location**: `files.index`, in the exe's folder (`AppContext.BaseDirectory`), next to
  `favorites.txt`. If the folder is not writable, the index stays in memory for the session and
  the status line says *Index not saved: <error>* — no fallback location.
- **Format**, UTF-8 text: three header lines — `ImageGridFusion index 1`, the base folder
  (absolute), the scan's date and time (ISO 8601) — then **one relative path per line**. Extra
  **tab-separated columns** after the path are tolerated and ignored by this version, so a later
  task (`workfiles/20260926-ocr-search.md`) can add the text found in each file without breaking
  the format.
- **What is indexed**: **every file** under the base folder and its subfolders, whatever its
  extension — except the **hidden and system** files and folders (`Thumbs.db`, `desktop.ini`,
  `.git`, `$RECYCLE.BIN`…), skipped with their whole content
  (`EnumerationOptions.AttributesToSkip = Hidden | System`). Inaccessible subfolders are skipped
  too (`IgnoreInaccessible`), never fatal.
- **Load at start-up** (`OnShown`): the file is read if its header carries the version and the
  **same base folder as the setting** (full path, case-insensitive); else it is ignored. The panel
  is usable as soon as it is loaded — before any disk scan.
- **Rescan, in the background** (`Task.Run`, `Progress<T>` marshalled to the UI thread like the
  export's), started **at every launch** right after the load, by the **`↻` button**, and when the
  **base folder changes**. Two passes over the disk:

  | Pass | Status line | Purpose |
  |---|---|---|
  | Count | *Counting… 1 234* (the running count) | Knows the total, so the next pass shows an exact ratio |
  | Index | *Indexing… 5/346* | Records the relative paths |

  The status text is refreshed at most every 100 ms. When the pass ends, the new list **replaces**
  the in-memory index on the UI thread, the current search is re-run, the file is rewritten
  (temp file + atomic move, so a crash keeps the previous index), and the status line shows
  *346 files · indexed 21:03*.
- **One scan at a time**: `↻` and a folder change **cancel** a running scan and start over; the
  `↻` button is disabled while a scan runs. A base folder that no longer exists: status *Folder not
  found: <path>*, the last index kept.
- **A row whose file no longer exists** (checked when the row is dragged, activated, or its
  location opened): the entry is **removed from the index** — memory, then the file rewritten —
  and from the favorites, the list refreshes, and the status line says *File not found — removed
  from the index*. No other consistency check between two scans: a file created since the last
  scan appears at the next launch or `↻`.
- **Memory**: one string + its folded form per entry — a 100 000-file folder costs a few tens of
  MB and a search on it stays under 10 ms.

---

## Favorites

Agreed:

- **Stored in `favorites.txt`**, next to `files.index`: one **absolute** path per line, UTF-8, in
  the **order they were added** (a new favorite appended at the end). Absolute, so a favorite
  survives a rescan and a change of base folder. Written at once on every toggle (temp file +
  move); loaded at start-up.
- **Toggled by clicking the heart** in front of a row — in the results as in the favorites list.
  Clicking the row elsewhere selects it (and starts a drag, § Interactions).
- **Shown** while the search box is empty, all of them, the **most recently added first** (the
  file's order reversed); during a search, only marked (`♥`), never promoted.
- **Removed** when its file turns out missing (§ Index File). A favorite outside the current base
  folder stays a favorite.

---

## Interactions

Agreed:

- **Drag & drop**: mouse down on a row (outside the heart), then a move beyond
  `SystemInformation.DragSize`, starts `DoDragDrop(new DataObject(DataFormats.FileDrop,
  [fullPath]), DragDropEffects.Copy)`. From there, everything is the Explorer's path: `OnDragEnter`
  accepts the `FileDrop`, the cell under the pointer is highlighted, and `OnDragDrop` resolves the
  target — a **cell** (its image replaced, effects reset per RULES.md) or the **Add images zone /
  empty canvas** (`CellAt` → `-1`, appended like *Add images*). The panel is a drag source only,
  never a drop target.
- **Double-click, or Enter on a selected row**: the panel raises `FileActivated(path)`; the form
  calls `AddFilesAsync([path])` — the *Add images* semantics (free slots first, then the replace
  rule of README § Adding images).
- **Right-click**: a context menu with **Open file location** — the Explorer opened on the file,
  selected (`explorer.exe /select,"<path>"`).
- **Tooltip**: the full path of the row under the mouse.
- **Missing file**: the checks of § Index File run before the drag, the activation or the menu
  action; none of them starts on a missing file.

---

## Settings

Agreed:

- **Store**: the registry values of `UI/AppSettings.cs`, following its pattern (a typed getter
  with a default, a `SaveXxx`), consistent with RULES.md § Global Effects (*an app setting … stored
  in the registry*). The JSON file designed by `workfiles/20260926-remember-last-folder.md` does
  not exist yet; that work may migrate these values when it lands.

  | Value | Type | Default | Read | Written |
  |---|---|---|---|---|
  | `ExplorerFolder` | string, absolute path | none (*No base folder*) | At start-up | By the ⚙ item and the panel's *Choose folder…* button |
  | `ExplorerPanelOpen` | bool | true | At start-up | On every collapse / expand |

- **⚙ menu item** *File explorer folder…*, tooltip *The folder the file explorer searches, with
  its subfolders; remembered between sessions*: opens a `FolderBrowserDialog` preselected on the
  current folder; **OK** saves the value, drops the in-memory index and starts a scan (§ Index
  File). Cancel changes nothing. The panel's *Choose folder…* button runs the same method.

---

## Test Impact

**None.** The repository has **no test project** (`src/` holds `ImageGridFusion` only), and the
user chose to ship this work **without unit tests**, like every previous workfile (Q&A #13): it is
verified by hand in the app. The checks, run at delivery:

| Behaviour | Check |
|---|---|
| Folding | *ete* finds *Été.jpg*; *CHAT* finds *chat.jpg* |
| Words in the relative path | *vacances chat* finds `Vacances 2025\chat.jpg`; *chat noir* finds `Photo_Chat-Noir.jpg` |
| Ranking | A name starting with the word above one containing it; a name hit above a folder-only hit |
| 10-limit and caption | *57 results — first 10*; the favorites list shows them all, most recent first |
| Index file | `files.index` next to the exe: header, base folder, relative paths; hidden / system entries absent; ignored after a change of base folder |
| Progress | *Counting… n*, then *Indexing… 5/346*, then *346 files · indexed hh:mm* |
| Missing file | A row whose file was deleted disappears on drag / double-click / menu, with the status text; also gone from the favorites |
| Favorites | `favorites.txt`: toggled at once, absolute paths, order of addition |
| Panel | Collapse with `»`, the strip's `«`, state remembered after a restart; the preview shrinks |
| Interactions | Drag onto a cell (replaced) and onto *Add images* (appended); double-click / Enter; *Open file location* |
| No base folder | The invitation at the first keystroke, its button and the ⚙ item both start the scan |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~Unit tests: none, verified by hand like every previous workfile, or a first xUnit project for
  the pure logic (search, index file, favorites)?~~ → None, verified by hand
- [x] ~~Hidden and system files and folders (`Thumbs.db`, `desktop.ini`, `.git`, `$RECYCLE.BIN`…):
  skipped by the scan, or indexed like the others?~~ → Skipped
- [x] ~~The collapse control: the `»` / `«` buttons of the panel and its strip (§ Panel), a button in
  the bottom bar, or both?~~ → The panel's `»` / `«` buttons
- [x] ~~The favorites list (box empty): sorted by file name, or in the order they were added?~~ →
  The most recently added first

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial scope from the user's request, settled in the scoping batch (Q&A #1–#12): a collapsible
panel open at start-up, the search run at each keystroke on every word of the relative path,
accents and case ignored, rows showing the heart and the file name only, every file indexed, the
index rebuilt in the background at start-up and by a `↻` button with a count pass first for an
exact *5/346* progress, results ranked by best match, favorites shown — all of them — while the box
is empty and only marked during a search, stored in a dedicated file next to the exe; drag & drop,
double-click and a context menu; without a base folder, the first search invites the user to set
it and says why; the subject is straightforward (single scout pass).

Two corrections given during the batch: the favorites were first asked as a second, always
visible list, then withdrawn — one list, the favorites shown while the box is empty, with no
10-limit; and the accents must be ignored. Two additions: the indexing shows a textual progress
(*5/346*, no bar), and a follow-up task — OCR on the images, so the search also looks inside them —
is pre-created as `workfiles/20260926-ocr-search.md`; the index format keeps a column for it.

The scout pass found: a Dock-based main form where a `Dock = Right` panel plugs in after
`_preview`; one entry point for every file arrival, `AddFilesAsync`, and a `FileDrop` accepted
without any check on its source; the registry-backed `AppSettings` with its ⚙ menu wiring; the
`Task.Run` + `Progress<T>` pattern of the export for the background scan; no test project.
Four questions remain open.

### Iteration 2 — 2026-09-26

Q&A #13–#16: no unit tests, verified by hand (the Test Impact table becomes the delivery
checklist); hidden and system files and folders skipped by the scan; the panel collapses with its
own `»` / `«` buttons, nothing added to the bottom bar; the favorites list shows the most recently
added first. No open question remains.

### Iteration 3 — 2026-09-26 — ✅ Implemented

Go given for the code and the documentation (*Implement code, unit tests and documentation*, the
unit tests being declined at Q&A #13). The run stays on `main`, the standing choice of this
repository — no worktree asked.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 2 | 2026-09-26 | Declined — no test project, verified by hand (Q&A #13) |
| README | | | Section *File explorer*, the ⚙ item under *Tray & startup*, the OCR follow-up under *Planned*; GLOSSARY: *File explorer*, *Base folder*, *Index*, *Favorite* |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | The panel at the right: collapsible and open at start-up, always visible, or collapsible and closed at start-up? | Collapsible, open at start-up | 2026-09-26 |
| 2 | When does the search run: at each keystroke, or on Enter / a button? | At each keystroke | 2026-09-26 |
| 3 | What "matches the name": every word in the file name, the text as typed, starts with, or every word in the relative path? | Every word in the relative path (subfolders count) — and accents ignored too, like the case | 2026-09-26 |
| 4 | A result row: heart + name + subfolder, heart + name only, or heart + thumbnail + name? | Heart + name only | 2026-09-26 |
| 5 | Files indexed: only the types the app opens, or every file? | Every file | 2026-09-26 |
| 6 | When is the index rebuilt: at start-up in the background + `↻`, manually only, or a live watcher? | At start-up in the background + `↻` | 2026-09-26 |
| 7 | Order of the 10 results: favorites first then best match, best match only, alphabetical, or most recent? | Best match; favorites not promoted; favorites stored in a dedicated file next to the exe. First asked as a separate always-visible favorites list, then withdrawn: one list, favorites shown while the box is empty, no 10-limit | 2026-09-26 |
| 8 | Search box empty: the favorites, or nothing? | The favorites | 2026-09-26 |
| 9 | How a row joins the grid: drag & drop + double-click, drag & drop only, or drag & drop + double-click + context menu? | Drag & drop + double-click + context menu (*Open file location*) | 2026-09-26 |
| 10 | No base folder configured: a message with a button, a message only, or the panel hidden? | At the first search, invite the user to set the folder, explaining why | 2026-09-26 |
| 11 | Progress *5/346*: which denominator — the previous index's count, a count pass first, or folders? | A count pass first | 2026-09-26 |
| 12 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-26 |
| 13 | Unit tests: none, verified by hand, or a first xUnit project for the pure logic? | None, verified by hand | 2026-09-26 |
| 14 | Hidden and system files and folders: skipped, or indexed like the others? | Skipped | 2026-09-26 |
| 15 | The collapse control: the panel's `»` / `«` buttons, a bottom-bar button, or both? | The panel's buttons | 2026-09-26 |
| 16 | The favorites list: sorted by name, in the order added, or the most recent first? | The most recently added first | 2026-09-26 |

---

*Last updated: 2026-09-26*
