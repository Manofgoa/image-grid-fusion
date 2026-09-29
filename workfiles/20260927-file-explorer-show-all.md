# File Explorer — Show All

> Working document — a `*` search showing the whole index in the file explorer, newest first, and
> lists that load their tiles page by page as they scroll, behind a *Loading…* slot.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the file explorer (`UI/FileExplorerPanel.cs`, `UI/ThumbnailGrid.cs`, `Explorer/*`) shows
either **every favorite** (search box empty) or the **10 best matches** of a search. There is no
way to browse the base folder's content without typing a filter.

This task adds:

- the **`*` search**: typing `*` in the search box shows **every file of the index**, the most
  recent first — no button;
- **loading as you scroll**, for every list of the explorer — `*`, search results, favorites: the
  grid holds a first load of **N pages** of tiles (N = 2 by default, a setting of the ⚙ menu,
  remembered), its last slot a **Loading…** placeholder; scrolling down to it loads N pages more.
  The search's 10-limit goes away.

Components: `FileExplorerPanel` (the `*` query, the loads, the caption), `ThumbnailGrid` (the
placeholder slot, append without resetting the scroll, the page size, the "placeholder in view"
signal), `FileSearch` (every match, ranked), `FileIndex` / `IndexEntry` (a date per file, index
format version 2), `AppSettings` + `MainForm` (the pages setting in the ⚙ menu), README § File
explorer, GLOSSARY.

---

## The `*` Search

- The search box holding **`*` alone** (blanks around it ignored) shows **all the files of the
  index**.
- `*` **with words** (`* chat`) is a normal search on the words, the star ignored: ranked by
  relevance, not by date.
- The empty box still shows the favorites; any other text, the search results.
- Without a base folder, `*` shows the invitation, like any typed search; with the index not loaded
  yet, *Waiting for the index…*.
- The search box's placeholder text mentions it: *Search files… (* for all)*.

## Order of All Files

- **The most recent first**, by the file's **creation date** — when it arrived in the folder: a
  copy or a download made yesterday comes first, however old the photo — then by relative path for
  equal dates.
- The date is read **from the index only** — the explorer never reads the disk to sort, like the
  search.

## Index File

- The index gets a **date per file**, written by the scan as a second tab-separated column (the
  format already tolerates extra columns): `relative\path.jpg<TAB>2026-09-27T10:12:33.0000000Z`.
- The header becomes `ImageGridFusion index 2`. An index of version 1 is **rejected** by `Load`
  like any unreadable one (*No index yet*), and the background scan run at every launch rewrites
  it with the dates — nothing else to migrate.
- The scan takes the date from the enumeration itself (`FileSystemEnumerable` over
  `FileSystemEntry.CreationTimeUtc`), no extra disk access per file.
- `IndexEntry` carries it as a `DateTime` (UTC).
- The OCR search workfile (`20260926-ocr-search.md`, in design) weighs the index's extra column
  for its text: the date takes the second column, a later text column would come after it.

## Loading as You Scroll

Applies to the three lists: `*`, search results, favorites.

- A list is computed **once** (in memory, sorted) when its source changes; the grid shows only
  its loaded part.
- **A page** is what the view can show at once: tiles per row × rows the view's height holds
  (a partly visible row counted), at the current tile size and panel width.
- **A load** is **N pages** (N = the setting, § Pages Setting):
  - the **first load** shows **N pages − 1 tiles**, the last slot being a **Loading…** placeholder
    — a tile-sized box with the text *Loading…* centred, no thumbnail, no heart, no name;
  - when the placeholder comes **into view** (scrolled down to it, or the view enlarged until it
    shows), the next N pages load: the **first new tile takes the placeholder's slot**, and a new
    placeholder ends the new load, N pages further. The new tiles are appended **at once** (the
    list is already in memory), their thumbnails arriving one by one as today: the placeholder is
    barely seen;
  - when the rest of the list fits in the load, it is shown whole and **no placeholder** is left.
- Appending a load **keeps the scroll position and the selection**; only a new list (another
  search, a cleared box) brings the grid back to the top with a first load.
- The page is measured at each load: resizing the panel or changing the tile size keeps the tiles
  already loaded, the next load uses the new page.
- Keyboard: the placeholder is not selectable; moving the selection past the last loaded tile
  (`↓`, `→`, `PageDown`, `End`) scrolls it into view, which loads the next N pages.
- Thumbnails: unchanged — already requested only for the painted tiles, in the background, and
  cached.
- **Search results**: every match, best first (the current ranking), loaded the same way — the
  10-limit is removed. The ranked list is sorted once per query.
- **Favorites**: loaded the same way, for uniformity (they are few, so usually one load).

## Pages Setting

- **Pages loaded at a time**: how many pages a load holds — **2 by default** — set from the
  **⚙** menu and **remembered between sessions** in the registry (`AppSettings.ExplorerPages`),
  like the tile size. Changing it applies from the next load.
- The ⚙ menu gets an item **File explorer pages per load**, opening a submenu of exclusive
  choices **1 to 10**, the current one checked (2 by default). A registry value outside 1–10 falls
  back to 2.

## Caption

| List | Caption |
|---|---|
| Favorites | `Favorites (12)` — unchanged |
| `*` | `All files (12,345)` |
| Search | `57 results` — the *— first 10* suffix goes away |

## Refreshes That Keep the Place

- A **background scan** ending (at launch or ↻) while a list is shown: the list is rebuilt from the
  new index **keeping as many tiles loaded as before and the scroll position**, so browsing is not
  thrown back to the top.
- **Un-hearting** a tile in the favorites list: the tile leaves the list, the scroll and the loaded
  tiles kept (today the list jumps back to the top).
- **Hearting / un-hearting** in the `*` or search list: the tile is only redrawn (it stays).
- A file found missing (drag, double-click, Open file location) leaves the list the same way,
  the place kept.

---

## Documentation Impact

- README § File explorer: the `*` search, its order, the loads with the *Loading…* slot, the
  search no longer limited to 10, the index's date column, the pages setting (and the ⚙ menu's
  list of items); the features line at the top.
- GLOSSARY: *Index* (a date per file), *File explorer* (the `*` list), *Tile* (the Loading…
  placeholder), a *Load* / *Pages loaded at a time* entry.

---

## Test Impact

**None** — the app has no test project, and the user chose to ship this work verified by hand,
like every previous workfile (Q&A #8). The behaviours to check by hand at delivery:

| Behaviour to check | Test file | Create / Update |
|---|---|---|
| Index v2 round-trip: the date column written then read back; a v1 index rejected, then rewritten by the launch scan | — (by hand) | — |
| `*` lists every file by creation date, newest first, ties by path | — (by hand) | — |
| Search returns every match, ranked, no 10-limit | — (by hand) | — |
| First load: N pages − 1 tiles + the Loading… slot; scrolled to it, the next N pages, the first new tile in its slot | — (by hand) | — |
| No placeholder once the list is whole; the scroll kept on every append | — (by hand) | — |
| The pages setting changed from ⚙, remembered between sessions | — (by hand) | — |

---

## Open Questions

- [x] ~~How does the button fit with the search and the favorites?~~ → A toggle next to ↻:
  pressed, the empty box shows all files instead of the favorites; typing replaces it, clearing
  returns to the toggle's list (Q&A #1) *(revised 2026-09-29, see Iteration 3: no button, `*`
  searches everything)*
- [x] ~~Order of all files?~~ → The most recent first (Q&A #2)
- [x] ~~Does loading as you scroll apply only to all files?~~ → Everywhere: all files, search
  results, favorites (Q&A #3)
- [x] ~~Which date means "most recent": the creation date or the last write date?~~ → The
  creation date (Q&A #5)
- [x] ~~Is the toggle's state remembered between sessions?~~ → Yes, in the registry like the
  columns count (Q&A #6) *(revised 2026-09-29, see Iteration 3: no toggle left)*
- [x] ~~Clicking the toggle while a search is typed: only choose, or also clear the box?~~ → Also
  clear the box (Q&A #7) *(revised 2026-09-29, see Iteration 3: no toggle left)*
- [x] ~~Unit tests: none, or a first test project?~~ → None, verified by hand (Q&A #8)
- [x] ~~`*` with words (`* chat`): only `*` alone means "all", or `*` plus words filters and orders
  newest first?~~ → `*` alone only; with words, a normal search, the star ignored (Q&A #10)
- [x] ~~When is the *Loading…* slot replaced?~~ → At once, the thumbnails arriving one by one
  (Q&A #11)
- [x] ~~The pages setting's control in the ⚙ menu, and its range?~~ → A submenu of exclusive
  choices 1 to 10 (Q&A #12)

---

## Design Iterations

### Iteration 1 — 2026-09-27

Scoping batch answered (Q&A #1–#4): a toggle next to ↻, newest first, scroll loading everywhere,
a straightforward subject (one scout pass, done directly: a single chain of questions around the
explorer). Findings: the index stores no date (one relative path per line, extra columns
tolerated), so newest-first needs a date column and an index format version 2; the search keeps
only its 10 best; `ThumbnailGrid.Rows` resets the scroll on every assignment, so appending needs
its own path; thumbnails are already loaded for painted tiles only. First design written: § Toggle,
§ Order of All Files, § Index File, § Loading as You Scroll, § Caption, § Refreshes That Keep the
Place. Four questions left open.

### Iteration 2 — 2026-09-27

Open questions answered (Q&A #5–#8): the **creation date** orders all files; the toggle is
**remembered** in the registry; clicking it during a search **clears the box**; **no unit tests**,
verified by hand. § Toggle, § Order of All Files, § Index File and § Test Impact updated. No open
question left.

### Iteration 3 — 2026-09-29

The user answered the go with feedback instead of a choice (Q&A #9):

- **No button**: typing `*` searches everything. § Toggle replaced by § The `*` Search; the
  toggle's setting (Q&A #6) and its click behaviour (Q&A #7) go with it.
- **Loads of pages, not 10 tiles**: a load is N pages of the view as it currently is, the first
  one N pages − 1 tiles, the last slot a **Loading…** placeholder; reaching it loads N pages more,
  the first new tile taking its slot. The fixed batch of 60 is dropped. § Loading as You Scroll
  rewritten.
- **N configurable**, 2 by default, in the settings, remembered: § Pages Setting added.

Meanwhile the tile size slider workfile reshaped the grid (tiles as many per row as fit at a tile
size of 100–1 000 px, the panel as wide as its splitter): "1 to 5 columns" references removed, the
page measured from the current layout. Three questions opened: `*` with words, when the Loading…
slot is replaced, the setting's control and range.

### Iteration 4 — 2026-09-29

Open questions answered (Q&A #10–#12): **`*` alone** means all, `* chat` being a normal search;
the Loading… slot is replaced **at once**, the thumbnails arriving one by one; the pages setting is
a ⚙ **submenu of choices 1 to 10** (the user widened the proposed 1–5). § The `*` Search,
§ Loading as You Scroll and § Pages Setting updated. No open question left.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 2 | 2026-09-27 | Declined — no test project, verified by hand (Q&A #8) |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the "show all" button fit with the search and the favorites? | Toggle next to ↻: pressed, the empty box shows the whole index instead of the favorites; typing replaces it, clearing returns to the toggle's list | 2026-09-27 |
| 2 | Order of the whole index? | The most recent first | 2026-09-27 |
| 3 | Does loading as you scroll apply only to "show all"? | Everywhere: all files, search results, favorites | 2026-09-27 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-27 |
| 5 | Which date means "most recent": creation or last write? | Creation date | 2026-09-27 |
| 6 | Toggle state remembered between sessions? | Yes, remembered | 2026-09-27 |
| 7 | Clicking the toggle during a search: only choose the empty box's list, or also clear the box? | Also clear the box | 2026-09-27 |
| 8 | Unit tests: none, or a first test project? | None, verified by hand | 2026-09-27 |
| 9 | Go: no, code only, or code + tests + documentation? | Feedback instead: no button, `*` searches everything; instead of 10, load 2 pages of the current view minus 1 tile, the last slot a "Loading…" placeholder that, reached at the bottom of the scroll, loads 2 pages more and is replaced by the first new thumbnail; the number of pages (2 by default) configurable in the settings and remembered | 2026-09-29 |
| 10 | `*` with words: only `*` alone means all, or `*` + words filters and orders newest first? | `*` alone only; with words, a normal search | 2026-09-29 |
| 11 | When is the Loading… slot replaced: at once, or once the new tiles' thumbnails in view are loaded? | At once | 2026-09-29 |
| 12 | The pages setting's control in the ⚙ menu, and its range? | A submenu of choices 1 to 10 (proposed 1 to 5) | 2026-09-29 |
| 13 | Go: no, code only, or code + tests + documentation? | | |

---

*Last updated: 2026-09-29*
