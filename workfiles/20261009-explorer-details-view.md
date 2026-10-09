# Explorer Details View

> Working document — a view-mode choice in the file explorer, next to the sort drop-down:
> Thumbnails (today's tiles, the default) or Details, one row per file with its name, the
> content excerpt a search found with the matched words highlighted in yellow, and its size.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the file explorer shows every list — the search view, the favorites, the folder view — as
**tiles** (`UI/ThumbnailGrid.cs`, owner-drawn, one control): a 4:3 thumbnail, the heart medallion,
the light-bulb medallion on a file found by its content, the name below. The tile size slider and
Ctrl + wheel set the tile width; the **Sort** drop-down ends the same row
(`FileExplorerPanel._sizeRow`).

The work adds a **view mode** next to the sort drop-down:

| Mode | Shows |
|---|---|
| **Thumbnails** (default) | Today's tiles, unchanged |
| **Details** | One row per file: its thumbnail at the left, sized by the tile size slider; its **name**, its **size** on the name line; below them, when the search found the file by its content, the **content excerpt** with the matched words highlighted in yellow |

Origin: the user's request of 2026-10-09 — *"nouveau mode d'affichage en option (à côté du tri),
par défaut vignettes mais aussi Details… affiche en dessous le bloc OCR proche (celui du tooltip)
si pertinent avec highlight jaune (comme dans l'autre projet emoji-selector), et dans tous les cas
la taille du fichier"*.

Not matched by any row of a feature backlog (`workfiles/TODO-FEATURES.md` does not exist).

---

## Scope

Agreed (Q&A 1–3):

- **Row disposition D** (Q&A 1): the row's thumbnail follows the **tile size slider** and
  Ctrl + wheel — one setting for both modes; the name and the size on the first line, the excerpt
  below.
- **Excerpt only when relevant** (Q&A 2): shown only for a file the search found **by its
  content** (the files carrying the light bulb today); nothing below the name otherwise — the
  favorites, `*`, a browsed folder, a file matched by its path.
- **Everywhere, remembered** (Q&A 3): the mode applies to the search view, the favorites and the
  folder view (folders become rows too); it is an **app setting**, remembered between sessions
  like the sort order.
- **The size, always**: every file row shows its size, whatever the list.

Out of scope: a column header, sorting by clicking a column, other columns (date, path).

---

## UI

### The View Mode Control

- Placed in the tile size row (`_sizeRow`), **next to the Sort drop-down**.
- **Two icon toggle buttons** (Q&A 9): a grid icon for **Thumbnails**, a list icon for
  **Details**; the current mode's button pressed, each with its tooltip (`Thumbnails`, `Details`).
  One click switches. The glyphs are `▦` and `☰` (Segoe UI Symbol), the buttons the search row's
  size (26 × 23), after the Sort drop-down (`FileExplorerPanel._thumbnailsView` / `_detailsView`).
- The mode is the app setting `ExplorerView` (`AppSettings.ExplorerView`, saved by name, Thumbnails
  when unknown), set in `MainForm` with the sort order and saved on `FileExplorerPanel.ViewChanged`.

### A Details Row

Disposition D of the mockup shown on 2026-10-09:

```
┌──────────┐ facture-edf-mars.png                     412 Ko
│ ♡      💡│ … montant total [électricité] à régler avant le 12 mars …
└──────────┘
```

- **Thumbnail** at the left, 4:3, drawn as a tile's (`ThumbnailCache`), its width **a quarter of
  the tile size** — 25 to 250 px for the slider's 100 to 1 000 (Q&A 8); the row is as tall as the
  thumbnail, at least the text's two lines. The slider and Ctrl + wheel resize it.
- **Name** on the first line, ellipsized; **size** right-aligned on the same line.
- **Excerpt** on the line below, only when the file was found by its content.
- The **heart** and the **light bulb** stay **on the thumbnail**, at a tile's corners, with the
  same clicks and tooltips (Q&A 11). The drag into a cell and the double-click work on the whole
  row. On a small thumbnail the medallions shrink with it, down to 14 logical px, their glyphs'
  fonts scaled alike; the row is never shorter than the two medallions stacked.
- The selected row gets a faint wash of the selection color, its name in that color; the hovered
  row a fainter one. Rows are 4 logical px apart (the inset), not a tile's 8.
- The name and the excerpt are drawn without padding, so they start on the same column; the name in
  the panel's font, the size and the excerpt in the tiles' smaller one, grey.
- The `Loading…` slot becomes a row of its own at the end of the list.

### A Folder Row

In the folder view, a folder in Details (Q&A 10):

```
┌──────────┐ Factures (42)
│  folder  │ ┌────┐┌────┐┌────┐┌────┐┌────┐┌────┐┌────┐┌──
└──────────┘ └────┘└────┘└────┘└────┘└────┘└────┘└────┘└──  ← up to the right edge
```

- Its thumbnail (Windows' folder thumbnail, or the drawn folder) at the left, at a file row's
  width.
- **Name, then its file count in parentheses** — `Factures (42)`, the count the folder tile shows
  today (the index's, every file below it).
- On the line below, a **strip of thumbnails of the first files it contains**, each **half the
  folder thumbnail's size**, side by side **as far right as the row goes**, never scrolling
  sideways: as many as fit.
- The strip shows the files **directly in the folder** only, in the **sort order** chosen
  (Q&A 13); a folder holding only subfolders has an empty strip. They are read **from the index**
  (`FolderTree.FilesIn`, grouped and sorted once per folder and order), not the disk: a file
  created since the last scan joins the strip after the next one.
- Its thumbnails come from a cache of their own (`ThumbnailGrid._stripThumbnails`): loaded at
  128 × 96, the largest a strip draws them, up to 1 000 kept, so resizing never reloads them and a
  screen of strips never evicts itself. A file without a Shell thumbnail (a sound) leaves its box
  empty.
- The strip is **a picture only** (Q&A 14): its thumbnails have no tooltip, no drag, no click of
  their own — the whole row behaves as the folder.
- No heart, no drag, a double-click opens it — as a folder tile.

### The Excerpt and Its Highlight

- The excerpt is the one the light bulb's tooltip shows today: `ContentIndex.ExcerptOf` — the
  first word whose folded form contains the matched word, with the words around it, `…` where the
  text goes on — read from the content index in memory, no disk read.
- **As wide as the row holds** (Q&A 5): one line, as many words around the matched one as the
  text column's width takes, the matched word kept in view (centered when the text goes on both
  sides), `…` where the text goes on. The light bulb's tooltip keeps its 4 words around.
- **Every word of the search** found in the excerpt is highlighted, at each occurrence (Q&A 6) —
  not only the word that made the file found.
- **Highlight like emoji-selector** (the user's reference): an opaque **yellow rectangle
  (#FFFF00)**, no padding nor rounding, as tall as the line, drawn behind the matched substring,
  the text drawn over it with `TextRenderer` (`NoPadding | NoPrefix`, each run measured). The
  matching is case- and accent-insensitive (the app's existing folding), every occurrence of a
  highlighted word marked, overlapping spans merged.

### The Size

- Read from the index (`IndexEntry.Size`, falling back to the stamp's); a file outside the index —
  a favorite elsewhere, a pasted favorite — reads it from the disk once.
- **In French units, base 1024** (Q&A 7), as the user asked — a deliberate exception to the
  English UI: `812 octets`, `412 Ko`, `2,3 Mo`, `1,1 Go`; one decimal below 10 of a unit, none
  above (`12 Ko`, `9,4 Mo`), the French decimal comma; `0 octet` and `1 octet` singular; a value
  rounding up to 1 024 written in the next unit. An unknown size shows `—`
  (`Explorer/FileSizes.cs`).

---

## Code Leads

From the scout pass of 2026-10-09 (inputs, not decisions):

| Piece | Where |
|---|---|
| Tile grid, owner-drawn, virtualized painting | `UI/ThumbnailGrid.cs` — `OnPaint` ~488, `PaintTile` ~550, `PaintLoading` ~531, `PaintFolderGlyph` ~633 |
| Geometry from the tile size | `ThumbnailGrid.LayoutRows` ~918 (`_perRow`, `_tileW`, `_tileH`), `TileBounds` / `NameBounds` / `CellBounds` ~656-699, `UpdateExtent` ~905, `IndexAt` ~861, arrow keys ~306-371 |
| Heart / bulb hit-tests | `HeartBounds` 685, `OnHeart` 881, `BulbBounds` 694, `OnBulb` 701, `ShowBulbTip` ~706-725 |
| Row record | `ExplorerRow(FullPath, Name, IsFolder, ContentWord)` (`ThumbnailGrid.cs:15`) — no size, no excerpt |
| Excerpt | `ContentIndex.ExcerptOf(relativePath, foldedWord, around)` (`Explorer/ContentIndex.cs:97-117`), called with `around = 4` by `FileExplorerPanel.ExcerptOf` (~1566), handed to the grid as `ContentExcerpt` |
| Matched word | `SearchMatch.ContentWord` (`Explorer/FileSearch.cs:221`) — the first query word found in the content only |
| Size | `IndexEntry.Size` (`Explorer/FileSearch.cs:176-211`), persisted in `files.index` (`FileIndex.cs`) |
| Sort drop-down, size row | `FileExplorerPanel._sort` (106), `_sizeRow` (205-207), `ChangeOrder` (243), `SortWidth` (~840) |
| App setting pattern | `AppSettings.ExplorerFileOrder` / `SaveExplorerFileOrder` (193-199), wired in `MainForm` (477-480, 552-554, 3579-3592) |
| Highlight reference | emoji-selector: `EmojiSearch.MatchSpans` (`Data/EmojiSearch.cs:103-152`), `EmojiDetailsPanel.PaintText` (435-465) |

---

## Test Impact

The app has **no test project** (as recorded by `20261009-search-results-sort.md`), and none is
created for this work (Q&A 12): **nothing is unit-tested**, every behaviour below is checked by
hand in the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| The size format (octets, Ko, Mo, Go; rounding; unknown size) | *none — no test project* | — |
| The highlight spans of an excerpt (case / accent-insensitive, every occurrence, merged overlaps) | *none — no test project* | — |
| The view mode setting read back, unknown text falling back to Thumbnails | *none — no test project* | — |

---

## Open Questions

- [x] ~~How wide is the excerpt in a Details row — the tooltip's 4 words around, or as much as the
  row's width holds?~~ → As much as the row's width holds, one line, the matched word in view
- [x] ~~Which words are highlighted in the excerpt — the matched word only, or every word of the
  search found in it?~~ → Every word of the search, at each occurrence
- [x] ~~The size's format and units — English `B / KB / MB / GB` like the rest of the UI, or French
  `octets / Ko / Mo`? Base 1024 or 1000? And a file whose size is unknown?~~ → French
  `octets / Ko / Mo / Go`, base 1024; `—` when unknown
- [x] ~~The row thumbnail's width from the tile size (100 to 1 000 px) — the tile size itself, a
  fraction of it, or capped?~~ → A quarter, 25 to 250 px
- [x] ~~The view mode control's form — a drop-down like Sort, or two toggle buttons (icons)?~~ →
  Two icon toggle buttons
- [x] ~~A folder row in Details — its file count as on the folder tile, or a total size?~~ → Name +
  file count in parentheses, and below it a strip of the first files' thumbnails at half the
  folder thumbnail's size, up to the right edge
- [x] ~~The heart, the light bulb and their tooltips in a Details row — kept on the thumbnail as on a
  tile, or moved?~~ → Kept on the thumbnail
- [x] ~~A test project for the pure helpers (size format, highlight spans), or checked by hand as
  the previous workfiles?~~ → No test project, checked by hand
- [x] ~~A folder row's strip — which files: the files directly in the folder, or every file below
  it (the subfolders' too), and in which order?~~ → The files directly in it, in the sort order
- [x] ~~A folder row's strip — do its thumbnails react (tooltip with the name, drag into a cell,
  double-click loading the file), or are they a picture of the folder only?~~ → A picture only

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design from the request and the scoping batch (Q&A 1–4): a view mode next to Sort,
Thumbnails by default; Details rows in disposition D, the thumbnail following the tile size
slider; the excerpt of the light bulb's tooltip below the name for a file found by its content
only, highlighted in yellow as emoji-selector does; the size on every file row; the mode in every
view and remembered. Scout pass (one pass, the subject judged straightforward): leads in § Code
Leads. Eight questions left open.

### Iteration 2 — 2026-10-09

Answers to the eight open questions (Q&A 5–12): the excerpt as wide as the row holds, every
searched word highlighted; the size in French units, base 1024 (an exception to the English UI,
asked by the user); the row thumbnail a quarter of the tile size; two icon toggle buttons; the
heart and the bulb kept on the thumbnail; no test project. The folder row answer goes further
than the options offered: the name with its file count in parentheses, and below it a **strip of
the first files' thumbnails**, half the folder thumbnail's size, up to the row's right edge —
new § A Folder Row. Two questions follow from it (the strip's files and order, whether its
thumbnails react).

### Iteration 3 — 2026-10-09

The folder row's strip settled (Q&A 13–14): the files directly in the folder, in the sort order,
an empty strip for a folder of subfolders only; a picture only, the row behaving as the folder.
No open question left.

### Iteration 4 — 2026-10-09 — ✅ Implemented

Go given: **code, unit tests and documentation**, in a **worktree** on `feature/explorer-details-view`.
The scope is frozen as the sections above stand. No test project (Q&A 12): checked by hand.

### Iteration 5 — 2026-10-09 — 🧭 Implementation choices

No project rule broken. The choices the frozen design left open, now in the sections above:

- **View buttons**: `▦` / `☰` glyphs, 26 × 23, after Sort; the setting `ExplorerView` saved by name.
- **Strip source**: the index (`FolderTree.FilesIn`), not the disk — consistent with the folder's
  count, cheap per row; a file newer than the last scan is missing from the strip until the next.
- **Strip cache**: a second `ThumbnailCache`, fixed 128 × 96 box, 1 000 entries
  (`ThumbnailCache` takes its cap as an argument).
- **Medallions** on a small Details thumbnail: shrunk to 14 logical px at least, the row kept tall
  enough for both.
- **Selection**: a faint wash over the whole row (stronger when selected), the name in the
  selection color; rows 4 px apart.
- **Excerpt**: one line, words added on each side in turn while the line holds them (the found
  word near the middle), grey; cut with `…` when the found word alone is wider than the line.
  `ContentIndex.WordsAround` gives the words; the tooltip's `ExcerptOf` now reads it too.
- **Highlight**: `Explorer/HighlightSpans.cs`, the text folded one character at a time with
  `FileSearch.Fold` so each folded letter maps back to its original one (a surrogate pair kept
  whole); spans merged.
- **Size text**: singular `octet` for 0 and 1; a rounding to 1 024 moves to the next unit.
- **Checks**: the worktree's exe got copies of the main exe's `settings.json`, `favorites.txt` and
  `index\` so it runs on real data; checked by window captures (favorites, a content search, the
  folder view's base folder). Its copy starts in Details on the base folder.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-09 | Explorer helpers; the Details view in `ThumbnailGrid` and the view buttons in `FileExplorerPanel`; the `ExplorerView` setting |
| Unit tests | 4 | 2026-10-09 | No test project (Q&A 12) — none created; checked by hand in the app |
| README (EN + FR) | 4 | 2026-10-09 | New **Details view** bullet after Sort; the settings file lists the display |
| Glossary (EN + FR) | 4 | 2026-10-09 | New *Details view* row |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which disposition for a Details row (mockups A–D)? | D — the thumbnail follows the tile size slider | 2026-10-09 |
| 2 | When does the content excerpt show below the name? | Only when the search found the words in the file's content | 2026-10-09 |
| 3 | Where does Details apply, and is it remembered? | Everywhere (search, favorites, folder view), remembered between sessions | 2026-10-09 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-10-09 |
| 5 | How wide is the excerpt? | As wide as the row holds | 2026-10-09 |
| 6 | Which words are highlighted? | Every word of the search | 2026-10-09 |
| 7 | The size's format and units? | octets / Ko / Mo, base 1024 | 2026-10-09 |
| 8 | The row thumbnail's width from the tile size? | A quarter, 25 to 250 px | 2026-10-09 |
| 9 | The view mode control's form? | Two icon toggle buttons | 2026-10-09 |
| 10 | A folder row in Details? | "Name + file count in parentheses; on the line below, thumbnails of the first files it contains, 2× smaller than the folder's, going as far right as possible without scrolling" | 2026-10-09 |
| 11 | The heart and the light bulb in a Details row? | On the thumbnail | 2026-10-09 |
| 12 | A test project for the pure helpers? | No, checked by hand | 2026-10-09 |
| 13 | A folder row's strip: which files, in which order? | The files directly in it only | 2026-10-09 |
| 14 | A folder row's strip: do its thumbnails react? | A picture only | 2026-10-09 |

---

*Last updated: 2026-10-09*
