# File Explorer — Show All

> Working document — a toggle showing the whole index in the file explorer, newest first, and a
> grid that loads its tiles batch by batch as it scrolls.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the file explorer (`UI/FileExplorerPanel.cs`, `UI/ThumbnailGrid.cs`, `Explorer/*`) shows
either **every favorite** (search box empty) or the **10 best matches** of a search. There is no
way to browse the base folder's content without typing a filter.

This task adds:

- a **Show all** toggle next to **↻**: pressed, the empty search box shows **every file of the
  index**, the most recent first, instead of the favorites;
- **loading as you scroll**, for every list of the explorer — all files, search results,
  favorites: the grid holds a first batch of tiles and appends the next one as the scroll nears its
  end. The search's 10-limit goes away.

Components: `FileExplorerPanel` (toggle, list modes, caption, batch requests), `ThumbnailGrid`
(append without resetting the scroll, "near the end" signal), `FileSearch` (paged results),
`FileIndex` / `IndexEntry` (a date per file, index format version 2), README § File explorer,
GLOSSARY.

---

## Toggle

- A **toggle button** in the search row, between the search box and **↻** (a `CheckBox` with
  `Appearance.Button`, as tall as ↻). Tooltip: *Show every file of the folder, the most recent
  first* (pressed: *Show the favorites*).
- **Released** (default): an empty box shows the favorites, as today.
- **Pressed**: an empty box shows **all the files of the index**.
- **Typing** a search replaces either list with the search results, whatever the toggle; the toggle
  keeps its state, and **clearing the box** brings back the list the toggle chooses.
- Clicking the toggle while a search is typed **also clears the box**, so the chosen list shows at
  once: a click always has a visible effect.
- Its state is **remembered between sessions**, in the registry next to the columns count
  (`AppSettings.ExplorerShowAll`, saved on every click); the app reopens on the same list.
- With no index yet (no base folder, index not loaded), the toggle stays clickable; pressed, the
  caption says *Waiting for the index…* (or shows nothing and the invitation, without a base
  folder), as the search does today.

## Order of All Files

- **The most recent first**, by the file's date stored in the index (§ Index File), then by
  relative path for equal dates.
- The date is the file's **creation date** — when it arrived in the folder: a copy or a download
  made yesterday comes first, however old the photo.
- The date is read **from the index only** — the explorer never reads the disk to sort, like the
  search.

## Index File

- The index gets a **date per file**, written by the scan as a second tab-separated column (the
  format already tolerates extra columns): `relative\path.jpg<TAB>2026-09-27T10:12:33`.
- The header becomes `ImageGridFusion index 2`. An index of version 1 is **rejected** by `Load`
  like any unreadable one (*No index yet*), and the background scan run at every launch rewrites
  it with the dates — nothing else to migrate.
- The scan takes the date from the enumeration itself (`FileSystemEnumerable` over
  `FileSystemEntry`, no extra disk access per file).
- The date is the **creation time** (`FileSystemEntry.CreationTimeUtc`); `IndexEntry` carries it as
  a `DateTime` (UTC), written in ISO 8601.

## Loading as You Scroll

Applies to the three lists: all files, search results, favorites.

- A list is computed **once** (sorted, in memory) when its source changes; the grid receives only
  its **first batch**, then **one more batch** each time the scroll comes within **one screen** of
  the end of the tiles loaded. Batch: **60 tiles** (a whole number of rows for 1 to 5 columns).
- Appending a batch **keeps the scroll position and the selection**; only a new list (other
  search, toggle, cleared box) brings the grid back to the top.
- Keyboard: `↓` / `PageDown` / `End` past the last loaded tile loads the next batch, so the
  keyboard can reach every file.
- Thumbnails: unchanged — they are already requested only for the painted tiles, in the
  background, and cached (200 at most).
- **Search results**: every match, best first (the current ranking), paged the same way — the
  10-limit is removed. The ranked list is sorted once per query.
- **Favorites**: paged too, for uniformity (they are few, so usually one batch).

## Caption

| List | Caption |
|---|---|
| Favorites | `Favorites (12)` — unchanged |
| All files | `All files (12,345)` |
| Search | `57 results` — the *— first 10* suffix goes away |

## Refreshes That Keep the Place

- A **background scan** ending (at launch or ↻) while a list is shown: the list is rebuilt from the
  new index **keeping as many tiles loaded as before and the scroll position**, so browsing is not
  thrown back to the top.
- **Un-hearting** a tile in the favorites list: the tile leaves the list, the scroll and the loaded
  tiles kept (today the list jumps back to the top).
- **Hearting / un-hearting** in the all-files list: the tile is only redrawn (it stays).
- A file found missing (drag, double-click, Open file location) leaves the list the same way,
  the place kept.

---

## Documentation Impact

- README § File explorer: the toggle, the order, the scroll loading, the search no longer limited
  to 10, the index's date column; the features line at the top.
- GLOSSARY: *Index* (a date per file), *File explorer* (the all-files list), a *Show all* entry.

---

## Test Impact

**None** — the app has no test project, and the user chose to ship this work verified by hand,
like every previous workfile (Q&A #8). The behaviours to check by hand at delivery:

| Behaviour to check | Test file | Create / Update |
|---|---|---|
| Index v2 round-trip: the date column written then read back; a v1 index rejected, then rewritten by the launch scan | — (by hand) | — |
| All files sorted by creation date, newest first, ties by path | — (by hand) | — |
| Search returns every match, ranked, no 10-limit | — (by hand) | — |
| Paging: batches of 60, appended without moving the scroll | — (by hand) | — |
| Toggle remembered between sessions; a click during a search clears the box | — (by hand) | — |

---

## Open Questions

- [x] ~~How does the button fit with the search and the favorites?~~ → A toggle next to ↻:
  pressed, the empty box shows all files instead of the favorites; typing replaces it, clearing
  returns to the toggle's list (Q&A #1)
- [x] ~~Order of all files?~~ → The most recent first (Q&A #2)
- [x] ~~Does loading as you scroll apply only to all files?~~ → Everywhere: all files, search
  results, favorites (Q&A #3)
- [x] ~~Which date means "most recent": the creation date or the last write date?~~ → The
  creation date (Q&A #5)
- [x] ~~Is the toggle's state remembered between sessions?~~ → Yes, in the registry like the
  columns count (Q&A #6)
- [x] ~~Clicking the toggle while a search is typed: only choose, or also clear the box?~~ → Also
  clear the box (Q&A #7)
- [x] ~~Unit tests: none, or a first test project?~~ → None, verified by hand (Q&A #8)

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

---

*Last updated: 2026-09-27*
