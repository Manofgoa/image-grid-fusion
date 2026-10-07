# Content Search (OCR and Text)

> Working document — the file explorer's search also finds a file by the **text it contains**:
> the words recognised (OCR) in its images and PDFs, and the native text of its text and HTML files.
> Pre-created on 2026-09-26 during `workfiles/20260926-file-explorer.md`, design started on
> 2026-09-29.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer (`workfiles/20260926-file-explorer.md`) indexes every file under the base folder
and matches the typed words against the **relative path** only. This task gives every indexed file
a **content text**, extracted in the background and cached, so the same search box also finds a
file by the words **inside** it — a screenshot by a word it displays, a meme by its caption, a scan
by its content, a note by a sentence it holds.

| File | Content text |
|---|---|
| Still image (PNG, JPG, BMP, TIFF, WebP, HEIC…), a GIF's first frame | OCR of the image |
| PDF | OCR of its **first page** |
| Text file (detected by content, whatever its extension) | Its text, read — no OCR |
| HTML file (`.htm`, `.html`) | Its text, tags stripped |
| Video, anything else | None — found by its name only |

Everything stays within the app's *Windows components only* rule (README): no third-party
library, no package.

Components touched: `Explorer/FileIndex.cs` (index folder, stamps), `Explorer/FileSearch.cs`
(matching, ranking), a new content cache and extractor under `Explorer/`, `UI/FileExplorerPanel.cs`
(background pass, pause, progress bar, status line), `UI/ThumbnailGrid.cs` (content badge),
`UI/MainForm.cs` (the ⚙ setting and rebuild entries), `UI/AppSettings.cs` (the setting).

---

## Existing Code (exploration 2026-09-29, refreshed 2026-10-07)

- **Index** — `files.index`, next to the exe (`FileIndex.DefaultPath`), **format 2**
  (`ImageGridFusion index 2`): three header lines (version, base folder, scan time), then
  `{relative path}\t{creation time UTC, "o"}` per file; `Load` reads the first two columns and
  ignores any further one; an older header, another base folder or an unreadable file returns null
  (no migration — the panel then scans). Saved atomically (`.tmp` then move). `IndexEntry`
  (in `FileSearch.cs`) holds `RelativePath`, `Created`, `Folded`, `NameStart`, `Name`: **no size, no
  last-write time**. `FileIndex.Scan` (static) rebuilds the list **from scratch**, two passes, the
  second over a `FileSystemEnumerable` reading the creation time from each `FileSystemEntry` — its
  `Length` and `LastWriteTimeUtc` are there for free, unread. The only in-place change is
  `FileIndex.Remove` (a clicked file found missing, `FileExplorerPanel.Exists`), the index saved
  again on the UI thread.
- **Search** — runs on every keystroke, on the UI thread: the query folded (accents dropped, lower
  case) and split on white space; **every word** a substring of the folded relative path.
  `FileSearch.Search(entries, words)` returns **every match**, sorted by `Rank` (name hits, first
  position in the name, name length, path) — no limit any more; `*` lists everything, newest
  first (`FileSearch.All`). Two call sites: the search view (`RefreshRows`) and the **folder view**
  (`SearchFolder`, the entries filtered below the open folder by the panel, its folders searched as
  synthetic path-only entries). The rows are then shown a few pages at a time behind a *Loading…*
  tile; the caption reads *N results*.
- **Tiles** — `ThumbnailGrid` paints each `ExplorerRow(FullPath, Name, IsFolder)`; the heart
  medallion sits in the **top-left** corner (`HeartBounds`, `Medallion` 24, `MedallionInset` 4
  logical px), skipped on folder tiles.
- **Panel layout** — `FileExplorerPanel._content`, a 6-row table: header, search row (📁, box, ↻),
  status line, caption / breadcrumb, the grid, the size slider; the row heights set in
  `ApplyMetrics`.
- **Background work** — `FileExplorerPanel.Restart()` cancels and renews the one
  `CancellationTokenSource` (`_scan`), used by `SetBaseFolder`, ↻ and `Dispose`; `ScanAsync` reports
  `ScanProgress` to the status line, disables ↻ while it runs, then `SetIndex` + `RefreshRows`.
  `ThumbnailCache` runs its own single-worker queue raising `Loaded`.
- **⚙ menu** — built in `MainForm` (`_settingsMenu`), its entries calling the panel's public API
  (`_explorer.SetBaseFolder`, `PagesPerLoad`).
- **App data** — `settings.json`, `favorites.txt`, `favorites-from-pasted\` and `files.index` all
  sit next to the exe (RULES.md § App Settings).
- **OCR** — `Windows.Media.Ocr` is reachable from the current target
  (`net10.0-windows10.0.19041.0`) with no project change. It takes a `SoftwareBitmap`:
  `Windows.Graphics.Imaging.BitmapDecoder` (WIC) decodes a file straight into one — every WIC
  format, WebP and HEIC included when their Windows codecs are installed, at full resolution.
- **PDF** — `Imaging/PdfPages.cs` renders pages with `Windows.Data.Pdf` (thread-safe, fixed
  1600 px long side, a private constant). `Windows.Data.Pdf` has **no text API**: a PDF's text,
  even a native text layer, is reachable only by OCR of its pages.
- **Text** — `TextPages.TryReadText` decides what a text file is, by content: at most 1 MB, not
  empty, UTF-16 with a BOM or valid UTF-8 with no NUL in its first 8 KB. `HtmlReader` parses
  clipboard HTML into `StyledText`.
- **Tests** — the solution has **no test project**; nothing references `FileIndex` / `FileSearch`
  but the explorer's own files.

---

## Index Folder

- Every indexing file lives in a subfolder **`Index\` next to the exe**: `files.index` (the list
  of indexed files) and the new **`files.content`** (the content texts).
- **Migration** — at start-up, a `files.index` found next to the exe while `Index\files.index`
  does not exist is **moved** into `Index\` as it is (format 2 unchanged), so the upgrade does not
  rescan from nothing.
- `favorites.txt`, `favorites-from-pasted\` and `settings.json` **stay next to the exe**: they are
  not indexing data. RULES.md § App Settings, which lists `files.index` beside the exe, is updated
  with the move.

## Extraction

- **An app setting** — the whole content extraction (OCR of images and PDFs, text and HTML files
  alike) runs only while **Search file contents (OCR)** is checked, an entry of the ⚙ menu,
  remembered in `settings.json` (`AppSettings`), **off by default**; off, the search is by path
  only, as today (Q&A #23).
- **Its menu group** — the two content entries, *Search file contents (OCR)* and *Rebuild content
  index*, form a **group of the ⚙ menu**, not a submenu: a separator, a grey **File contents**
  caption that does nothing when clicked, then the two entries (Iteration 12).
- **Turned off** — the extraction stops; the texts already extracted are **kept and still
  searched**, the badge included; turned on again, the pass resumes where it stopped (Q&A #24).
- **When** — in the **background**, **after** each scan (start-up and ↻): the list of files is
  indexed first — it is what the search needs — then the content texts, **one file at a time**,
  for the files whose content text is **missing or stale**; a search reads the cache only, never
  the disk.
- **Paused during a search** — the extraction pauses, between two files, while the tiles'
  **thumbnails are loading** (`ThumbnailCache`'s queue not empty) and for **1 s after the last
  keystroke** in the search box; it resumes on its own once the display is calm, even with the
  search still shown (Q&A #25).
- **Status line** — while a file is analysed, the summary gets the pass's count and the file's
  name: *12 345 files · indexed 17:20 · OCR: 254/3 500 (xxxx.png)* — the file's rank in the pass
  out of the files the pass extracts, thousands grouped like the scan's *Indexing…* count — the
  same *OCR:* label for a text or HTML file, the count being the whole pass's; for a file of the
  no-text kind, the count without a name. Back to the plain summary when the pass ends or pauses
  (Iteration 11).
- **Change detection** — the scan also reads each file's **size + last-write time**, from the
  `FileSystemEntry` it already enumerates (no extra disk access), and keeps them **in memory** on
  the `IndexEntry` — `files.index` stays format 2, the stamps being needed only right after a scan.
  `files.content` keeps, per file, the stamp it was extracted at; a file whose stamp still matches
  keeps its text, the others are queued. A file with no text (a video, an image with no word, a
  failed extraction) is recorded **with an empty text**, so it is not retried until it changes.
- **Worker** — started at the end of `ScanAsync`, with the **scan's cancellation token**, so the
  folder change, ↻, the rebuild and closing the app cancel it through `Restart()` like the scan;
  one file at a time, off the UI thread; the results written to `files.content` as the pass
  progresses (saved atomically, `.tmp` then move, like `files.index`), so a pass cut short resumes
  where it stopped — saved every 30 s and at the end. The search shown (not the favorites, nor `*`)
  refreshes (`RefreshRows`, keeping its place) as texts arrive, every 5 s, and once at the end.
  Closing the app mid-pass may lose what the last 30 s extracted: the final save runs on the
  worker, which the process does not wait for.
- **Missing file** — a clicked file found missing, removed from the index (`FileIndex.Remove`),
  leaves the content cache too; the cache is shared between the worker and the UI thread, so it is
  thread-safe.
- **OCR engines** — **French and English**: one `OcrEngine` per language, each created when its
  Windows OCR language is installed; an image is recognised by each engine and the texts are
  concatenated. With only one of the two installed, that one alone; with neither, the engine of the
  user profile's languages; with no OCR language at all, images and PDFs get no text (text files
  still do).
- **Which way** (`ContentExtractor.KindOf`, by extension) — an image extension (`.png`, `.jpg`,
  `.jpeg`, `.jpe`, `.jfif`, `.bmp`, `.dib`, `.gif`, `.tif`, `.tiff`, `.webp`, `.heic`, `.heif`,
  `.avif`, `.ico`, `.jxr`, `.wdp`) → OCR; `.pdf` → OCR of its first page; a video, audio, archive,
  executable, `.psd` or Office Open XML extension → no text, the file not even opened; anything
  else → read as text when it passes the text test.
- **Images** — decoded with `BitmapDecoder` into a `SoftwareBitmap`, oriented as its EXIF says (a
  GIF gives its first frame); an image longer than `OcrEngine.MaxImageDimension` (10 000 px) on a
  side is **downscaled** to it, not skipped — its EXIF orientation then ignored, so the scaled size
  cannot stretch it across the turned axes.
- **PDF** — its **first page only**, rendered by `Windows.Data.Pdf` with its long side at
  **2 400 px** (`PdfPages.TryRenderFirstPage`, the preview's 1 600 px kept), then recognised like an
  image.
- **Text** — a file passing the `TextPages` text test (≤ 1 MB, UTF-8 / UTF-16): its text as
  `TextPages` reads it (`TextPages.TryReadContent`) — an `.htm` / `.html` file without its markup
  (head, scripts and styles left out, entities decoded), and likewise an `.rtf` file without its
  control words, both falling back to the raw text when that reader finds nothing.
- **Cap** — each content text is kept to its **first 32 768 characters**, white space and control
  characters collapsed into single spaces. With both engines, an image's text holds what each
  recognised, one after the other — often the same words twice, which the search does not mind.
- **Time budget** — none: every source is bounded (one image, one PDF page, ≤ 1 MB of text).
- **Rebuild** — a ⚙ menu entry, **Rebuild content index** (built in `MainForm` with the other
  entries, calling `_explorer.RebuildContentIndex()`), clears every content text in memory and
  rescans the folder — the list keeping the priority — the extraction then redoing every file
  (after installing an OCR language, for instance); disabled while no base folder is set or the
  setting is off.
- **Pause** — checked between two files, every 200 ms while it holds (`ThumbnailGrid.LoadingThumbnails`,
  the last keystroke's time kept by the panel).

## `files.content`

UTF-8 without BOM, in `Index\`: a version line, the normalized base folder (a file for another
folder is ignored, like `files.index`), then one line per file:

```
{relative path}\t{size}\t{last-write UTC ticks}\t{text}
```

Tabs and line breaks in the text become spaces. Loaded in the background after `files.index`; the
texts are **folded once at load** (accents dropped, lower case), like the paths, so a keystroke
never folds them again. A file leaving the index (rescan, deletion from the panel) leaves
`files.content` at its next save.

## Search

- **Unified** — the existing search box: **each word** may be found in the file's **path or its
  content text** (*facture 2024*: *facture* in the name, *2024* in the text).
- **Ranking** — a file whose words are **all found in its path ranks before** every file needing
  its content: a new leading key of `Rank`, before the name hits; within each group, the current
  ranking (name hits, first position, name length, path). Every match is listed, loaded by pages as
  today, *N results* counting both groups.
- **Where** — in **both views**: the search view, and a search typed in the folder view (the files
  below the open folder), the badge included; the folder tiles stay searched by their name
  (Q&A #21).
- **Untouched** — `*` (every file, newest first) and the favorites (empty box) do not read the
  content; the folder view's folder tiles stay path-only.
- **Before the cache is loaded** — the search matches paths only, as today, and refreshes once the
  content texts arrive.

## Content Badge

- A tile found **thanks to its content** (at least one word matched in its text, not in its path)
  carries a **light bulb** 💡 in a medallion like the heart's, at the tile's **top-left, under the
  heart** (Iteration 13 — it replaces the T of the top-right corner).
- **Its tooltip**, while the pointer is over the bulb: *Found in its content:* then an **excerpt**
  of the text around the first word the content found — **4 words before, 4 after**, an ellipsis
  where the text goes on — the tile's tooltip (its path) everywhere else on the tile.
- Carried by `ExplorerRow.ContentWord` (from `SearchMatch.ContentWord`: the first query word found
  in the content only); the excerpt computed when the bulb is hovered (`ContentIndex.ExcerptOf`),
  not at search time. Clicking the bulb does what a click on the tile does.
- Tiles found by their name alone, and the favorites shown while the search box is empty, have no
  bulb.

## Progress Bar

- A **miniature progress bar** (`UI/IndexingBar.cs`) directly **under the search box**, across the
  panel's content width, **3 px** high (scaled with the DPI), **blue** (0, 120, 215), on a
  transparent track; the unknown total shown by a quarter-length segment sweeping the track.
- It shows **every indexing job** — the scan, the content extraction, and any future indexing job:
  proportional to the job's progress (files done / files to do), a job whose total is not known yet
  (the scan's counting phase) shown as a short segment sweeping the track.
- Its 3 px row is **always reserved** — a new absolute row of the panel's table, between the search
  row and the status line — so nothing moves when it appears; the bar is **hidden while no job
  runs**.
- The scan's existing status text stays as it is.
- **↻ during the extraction** — enabled again as soon as the scan ends; pressed, it rescans the
  file list (the priority), then the extraction resumes where it stopped (Q&A #26).

---

## Test Impact

**None.** The repository has **no test project**, and every previous workfile shipped without unit
tests, verified by hand (`workfiles/20260926-file-explorer.md`, Q&A #13 — the standing choice, kept
here). The checks to run at delivery:

| Behaviour | Check |
|---|---|
| OCR of an image | A screenshot holding a distinctive word: found by that word once the pass is over, with the bulb under the heart, its tooltip showing the words around it |
| French and English | A French word with accents typed without them, and an English word: both found |
| OCR of a PDF | A scanned PDF and a native-text PDF: found by a word of their first page, not of their second |
| Text / HTML | A `.txt` note found by a sentence fragment; an `.html` page by a word of its text, not by a tag name |
| Cap | A text file whose word sits beyond its first 32 KB: not found by it |
| Mixed words | One word in a file's name, another in its text: found, with the bulb |
| Name first | A word in one file's name and in another file's text: the name match ranks first |
| Large image | An image longer than the OCR maximum: downscaled and read |
| Incremental | Restart the app: nothing is re-extracted; edit one file: only that file is |
| Rebuild | ⚙ *Rebuild content index*: every file extracted again, the bar running |
| Cancellation | Change the base folder or press ↻ during the pass: it stops and restarts cleanly |
| Index folder | An existing `files.index` next to the exe moved into `Index\` at start-up, no full rescan from nothing; `favorites.txt` untouched |
| Progress bar | 3 px, blue, under the box, during the scan and the extraction; hidden when idle, nothing moving |
| OCR setting | Off at first launch: no extraction runs, text files included; turned on from ⚙: the pass starts after the file list, remembered after a restart; turned off mid-pass: it stops, the texts already extracted still found |
| Status line | During the pass: *… · indexed HH:MM · OCR: 254/3 500 (name.png)*, the count and the name changing file by file; the plain summary at the end |
| Pause | Typing a search while the pass runs: the status line's file name stops changing while the thumbnails load, the pass resuming about 1 s after they are all shown |
| ↻ | Enabled once the scan ends; pressed during the extraction: the list rescanned, then the extraction resumes without redoing the files done |
| Folder view | A search typed in the folder view finds a file below the open folder by its content, with the bulb |
| Menu group | ⚙: a separator, the grey *File contents* caption, then the two content entries |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~Engine: `Windows.Media.Ocr`, or a third-party one (Tesseract)?~~ → Not asked as such:
  `Windows.Media.Ocr` is the only engine the *Windows components only* rule allows.
- [x] ~~Which files?~~ → Images + PDF by OCR (Q&A #1), plus the native text of text files and
  other files holding text (Q&A #5).
- [x] ~~When?~~ → In the background after each scan, cached, new or changed files only (Q&A #2).
- [x] ~~Matching: the OCR words count like the path's?~~ → Unified with the name search (Q&A #3).
- [x] ~~OCR languages?~~ → French **and** English, one engine each when installed (Q&A #6).
- [x] ~~PDF text layer?~~ → OCR of the rendered page only, no built-in text-layer reader (Q&A #7).
- [x] ~~PDF pages?~~ → The first page only (Q&A #8).
- [x] ~~Other files holding text?~~ → HTML only, beyond plain text files; no RTF, no Office
  (Q&A #9).
- [x] ~~Storage?~~ → A subfolder `Index\` next to the exe, holding `files.index` (moved there) and
  the content cache (Q&A #10, #16).
- [x] ~~Text cap?~~ → The first 32 KB per file (Q&A #11).
- [x] ~~Mixed matching and ranking?~~ → Each word in the path or the content; files found by their
  path alone first (Q&A #12).
- [x] ~~Indicator?~~ → A badge only (Q&A #13).
- [x] ~~Progress?~~ → A miniature blue bar, 3–4 px, under the search box (Q&A #14), for every
  indexing job, the scan included (Q&A #17).
- [x] ~~Limits?~~ → Oversized images downscaled then read (Q&A #18); a ⚙ *Rebuild content index*
  entry (Q&A #19); no per-file time budget, every source being bounded.
- [x] ~~**Folder view**: does the content search also apply to a search typed in the folder view?~~
  → Yes, like the search view, with the badge (Q&A #21).
- [x] ~~**↻ during the extraction**?~~ → Enabled once the scan ends, the extraction resuming after
  the rescan (Q&A #26; #22 had been answered with the OCR setting instead).
- [x] ~~**OCR setting scope**?~~ → The whole content extraction, text and HTML included (Q&A #23).
- [x] ~~**OCR turned off**?~~ → The texts kept and still searched; turned on again, the pass resumes
  (Q&A #24).
- [x] ~~**Pause trigger**?~~ → The thumbnails loading, and 1 s after the last keystroke (Q&A #25).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 0 — 2026-09-26 — Pre-created

Placeholder created during the file explorer's design, at the user's request. No scoping batch,
no exploration yet: the questions above are the agent's first list, to be reviewed and completed
with the user before Iteration 1.

### Iteration 1 — 2026-09-29

Design started from `/create-workfile Rechercher un fichier par contenu OCR`, on this pre-created
workfile rather than a duplicate. Scoping batch (Q&A #1–4): images + PDF by OCR, in the background
after each scan and cached, unified with the name search; the subject judged straightforward, so
one scout pass (two read-only agents: index / search pipeline, OCR / PDF capabilities).

Request during the scouting (Q&A #5): text files, PDFs and other files holding text are searched by
their text as well — the task widens from *OCR search* to *content search*, the title renamed.

Findings recorded under *Existing Code*: no size / date per index entry (needed for incremental
extraction), the search is a per-keystroke substring match capped at 10, `Windows.Media.Ocr`
reachable as-is, `Windows.Data.Pdf` has no text API, no test project.

### Iteration 2 — 2026-09-29 — Extraction answers

Q&A #6–9: OCR in French and English (two engines), PDFs read by OCR of their first page only, HTML
as the only structured text format beyond plain text. *Extraction* written accordingly.

### Iteration 3 — 2026-09-29 — Storage and search answers

Q&A #10–13: the content cache and `files.index` in an indexing subfolder next to the exe (the
user's own answer, replacing both offered options), 32 KB per file, each word in the path or the
content with path-only matches first, a badge only. *Index Folder*, *`files.content`*, *Search* and
*Content Badge* written.

### Iteration 4 — 2026-09-29 — Progress bar

User request (Q&A #14): a miniature blue progress bar under the search box, 3–4 px high at most.
*Progress Bar* written — 3 px, row always reserved.

### Iteration 5 — 2026-09-29 — Last answers

Q&A #16–19: the folder named `Index\`, an existing `files.index` moved into it, `favorites.txt`
left next to the exe; the bar showing every indexing job, the scan and future ones included; an
oversized image downscaled then read; a ⚙ *Rebuild content index* entry. No open question left.

### Iteration 6 — 2026-10-07 — Refreshed against the current code

The go question of 2026-09-29 was cut short by the end of the session, unanswered; meanwhile the
file explorer moved on (`file explorer show all`, the folder view, favorites dropped onto the
panel). At the user's request (Q&A #20), a read-only refresh (two agents: index / search, panel UI)
before the go:

- *Existing Code* rewritten: `files.index` is format 2 with a creation time per file; the search
  returns every match (no best 10), loaded by pages; the folder view has its own search site;
  the heart is top-left; the ⚙ menu lives in `MainForm`.
- *Index Folder*: the format-2 file moved as it is; RULES.md § App Settings updated with the move.
- *Extraction*: the size and last-write stamps read from the scan's `FileSystemEntry`, kept in
  memory only (no format 3); the worker started at the end of `ScanAsync` on the scan's token; the
  search refreshed as texts arrive; a missing file removed from the cache with the index.
- *Search*: path-only matches first through a new leading `Rank` key; every match listed;
  `*`, favorites and folder tiles untouched.
- *Content Badge*: top-right. *Progress Bar*: a new table row between the search row and the status
  line.
- Two new open questions: the folder view (Q&A #21), ↻ during the extraction (Q&A #22).

### Iteration 7 — 2026-10-07 — OCR as an app setting, paused during a search

Q&A #21: the content search applies to the folder view's search too, the badge included — *Search*
updated. Q&A #22, answered with new requirements instead of the choice offered:

- The OCR is an **app setting** to turn on, a checkable ⚙ entry, off by default (*off by default*
  is the agent's reading of *an option to turn on*).
- It runs **after** the file list's indexing, which keeps the priority, **one file at a time**.
- The status line names the file being analysed, after the summary: *… · OCR on xxxx.png*.
- It **pauses during a search**, so the thumbnails are not slowed down.

*Extraction* updated. New open questions: the setting's scope (Q&A #23), what turning it off does
(Q&A #24), what pauses the pass (Q&A #25), and ↻ asked again (Q&A #26).

### Iteration 8 — 2026-10-07 — Setting scope, pause, ↻

Q&A #23–26: the setting gates the whole content extraction, text and HTML included — named
*Search file contents (OCR)* in the ⚙ menu; turned off, the texts already extracted are kept and
still searched; the pass pauses while thumbnails load and 1 s after the last keystroke; ↻ is
enabled again once the scan ends. Agent's choices stated for the go: *Reading name* on the status
line for a text or HTML file; the rebuild entry disabled while the setting is off. No open question
left.

### Iteration 9 — 2026-10-07 — ✅ Implemented

Go given for the **code only** (Q&A #27), in a **dedicated worktree** on its own branch, to be
tested by the user and merged into `main` once accepted. README, Glossary and RULES.md are not
part of this go.

### Iteration 10 — 2026-10-07 — 🧭 Implementation choices

The run on branch `feature/content-search`, in the worktree `.claude/worktrees/content-search`
(the Branch Gate settled by the go itself: a dedicated worktree). No rule broken. The choices the
frozen design did not state, now in the domain sections:

- **Which way per file** — by extension: a list of image extensions for OCR, `.pdf`, a list of
  media / archive / executable / Office extensions never opened, anything else tried as text.
- **RTF read without markup** — the design named HTML only; an `.rtf` file passes the text test
  anyway, and `TextPages` reads it like a paste, so its words are kept without the control words
  rather than with them.
- **Cap in characters** — 32 768 characters, white space and control characters collapsed.
- **Two engines, texts joined** — the same words often twice; harmless for a substring search.
- **Oversized image** — downscaled with its EXIF orientation ignored.
- **PDF page at 2 400 px** on its long side for the OCR.
- **Timings** — paused polls every 200 ms; texts saved every 30 s and at the end; the search shown
  refreshed every 5 s and at the end. Closing the app mid-pass may lose the last 30 s of work.
- **Rebuild** — clears the texts in memory and rescans; the extraction redoes every file after the
  scan.
- **Stamps** — `FileStamp(Size, Written)` on `IndexEntry`, null for an entry loaded from
  `files.index`; `files.content` holds the last write as UTC ticks.
- **Code style** — the new code qualifies instance members with `this.` (the user's convention for
  added code), the lines only modified keeping their style.

Checks run at delivery (a test folder in the scratchpad, the worktree's build, UI Automation to
type in the search box, the window captured): every row of *Test Impact* passed — OCR of a PNG and
of a 12 000 px PNG, French accents folded, the PDF's first page only, HTML without its script and
title, the 32 KB cap, mixed words with the badge, the name match first, nothing re-extracted after
a restart and only the edited file after an edit, the legacy `files.index` moved into `Index\`, the
bar and *OCR on name* during the pass, the plain summary and the bar still while paused by a
keystroke, the folder view's search with the badge — except **Rebuild** and **Cancellation by a
base folder change**, which need the ⚙ menu and are left to the user's test.

**Left open by the code-only go**: RULES.md § App Settings still says `files.index` lives next to
the exe, README / Glossary say nothing of the content search yet.

### Iteration 11 — 2026-10-07 — ⚙️ Post-implementation — Count on the status line

User request, after testing (Q&A #29): *OCR on xxx.png* becomes *OCR: 254/3500 (xxx.png)* — the
pass's count before the file's name. *Extraction › Status line* updated. Agent's choices: the
*OCR:* label for every file of the pass — the *Reading* of a text or HTML file dropped, the count
being the whole pass's; the count shown alone for a file of the no-text kind, so it never
flickers away; the numbers grouped like the scan's own count (*3 500*).

### Iteration 12 — 2026-10-07 — ⚙️ Post-implementation — The content entries grouped in the ⚙ menu

User request, after testing (Q&A #30): the two settings tied to the content analysis form a group
of the menu — ideally not a submenu. *Extraction › Its menu group* written: a separator, a grey
*File contents* caption, then the two entries. Agent's choices: the caption's wording, and a
caption rather than a submenu.

### Iteration 13 — 2026-10-07 — ⚙️ Post-implementation — A light bulb with an excerpt

User request, same answer (Q&A #30): a result found by its content and not by its name shows a
light bulb at the thumbnail's top-left, under the heart, its tooltip saying it was found in the
content, with an excerpt around it — a few words before, a few after. *Content Badge* rewritten:
the bulb replaces the T of the top-right corner. Agent's choices: 4 words each side, around the
first query word the content found; the tooltip's wording; the excerpt computed on hover.
Checked: `ContentIndex.ExcerptOf` called by reflection on the built assembly — 4 words each side,
accents folded, ellipses only where the text goes on, null when the word is missing. The bulb's
drawing and its tooltip were **not** checked on screen: the check window took the keyboard focus
while the user was typing elsewhere, and the checks were stopped; left to the user's test.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 9, 10, 11, 12, 13 | 2026-10-07 | Delivered on the worktree branch `feature/content-search`, to be merged into `main` once tested |
| Unit tests | | | None planned — no test project (see *Test Impact*) |
| README | | | Declined for now (Q&A #27, code only) — the file explorer's section, plus the Glossary (*Content text*, *Index folder*, *Index* revised), each with its `.fr.md` in the same commit; RULES.md § App Settings (`files.index` in `Index\`) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which files are OCR'd so their text is searchable: still images, images + PDF, or images + PDF + videos / GIF? | Images + PDF | 2026-09-29 |
| 2 | When is the OCR text computed: in the background and cached, on demand, or manually? | In the background, cached | 2026-09-29 |
| 3 | How does the search use it: name or content unified, a name / content toggle, or a prefix? | Name or content, unified | 2026-09-29 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-29 |
| 5 | *(user, unprompted)* | Text files, PDFs and files holding text must also be searched by their text | 2026-09-29 |
| 6 | OCR languages: the Windows user profile's, or French and English both? | French and English | 2026-09-29 |
| 7 | PDF text layer: OCR of the pages only, or a minimal built-in text-layer reader with OCR as fallback? | OCR of the pages | 2026-09-29 |
| 8 | PDF pages: every page, or the first N? | The first page only | 2026-09-29 |
| 9 | Other files holding text: HTML, RTF, Office Open XML? | HTML | 2026-09-29 |
| 10 | Storage: a sidecar file, or the extra column of `files.index`? | A subfolder next to the exe for the indexing; the list of indexed files goes in it too | 2026-09-29 |
| 11 | Text cap: the whole text, or the first N KB per file? | The first 32 KB | 2026-09-29 |
| 12 | Mixed matching (one word in the name, another in the content) and content matches below name matches? | Mixed, content after name | 2026-09-29 |
| 13 | Indicator: badge, snippet tooltip, or both? | Badge only | 2026-09-29 |
| 14 | Progress on the status line, or silent? | *(user, unprompted)* A miniature progress bar under the search box, blue for instance, 3 or 4 px high at most | 2026-09-29 |
| 15 | Limits: oversized images, per-file time budget, a full rebuild command? | Split into #18 and #19 | 2026-09-29 |
| 16 | The indexing subfolder: its name, and what becomes of the current `files.index`? | `Index\`, `files.index` moved into it | 2026-09-29 |
| 17 | What does the progress bar show: the extraction only, or the scan too? | The extraction, the scan, and every future indexing job | 2026-09-29 |
| 18 | An image larger than the OCR accepts: downscaled, or skipped? | Downscaled then read | 2026-09-29 |
| 19 | A way to re-extract everything: a ⚙ menu entry, Shift + ↻, or nothing? | A ⚙ menu entry | 2026-09-29 |
| 20 | *(2026-09-29, the go question — cut short by the end of the session, unanswered.)* How do we resume: refresh against the current code then go, go now (code, tests and docs / code only), or no? | Refresh, then go | 2026-10-07 |
| 21 | Folder view: the content search also in a search typed in the folder view, with the badge, or in the search view only? | Yes, like the search view | 2026-10-07 |
| 22 | ↻ during the extraction: enabled once the scan ends (the extraction resuming after the rescan), or disabled until the extraction ends? | *(not answered as such)* The OCR is an option to turn on in the app's settings. On, it indexes file by file, with an indication next to the file count and the last indexing time naming the file being analysed — e.g. *OCR on xxxx.png*. It comes AFTER the indexing of the file list, which has priority so the search works. It is paused during a search, so it does not slow down the thumbnails | 2026-10-07 |
| 23 | OCR setting scope: the OCR only (images, PDF), or the whole content extraction (text and HTML files too)? | The whole content extraction, text and HTML included | 2026-10-07 |
| 24 | OCR turned off: the texts already extracted kept and searched, or ignored while off? | Kept and still searched | 2026-10-07 |
| 25 | Pause trigger: the thumbnails loading plus a moment after the last keystroke, or as long as the search box holds a query? | The thumbnails loading, and a moment after the last keystroke | 2026-10-07 |
| 26 | ↻ during the extraction (#22 again): enabled once the scan ends, or disabled until the extraction ends? | Enabled once the scan ends | 2026-10-07 |
| 27 | The design is complete: start the implementation — code, tests and docs / code only / no? | Code, in a dedicated worktree; once done, the user tests it and it is merged into `main` if OK | 2026-10-07 |
| 28 | Once tested: merge into `main`, merge with the docs, adjustments, or not tested yet? | Adjustments | 2026-10-07 |
| 29 | Which adjustments? | *OCR on xxx.png* to become *OCR: 254/3500 (xxx.png)* | 2026-10-07 |
| 30 | After this test: merge into `main`, merge with the docs, other adjustments, or not tested yet? | The two settings tied to the content analysis grouped in the menu, ideally not as a submenu; a result found by its content and not by its name: a light bulb at the thumbnail's top-left, under the heart, its tooltip saying it was found in the content, with an excerpt around it (a few words before, a few after) | 2026-10-07 |

---

*Last updated: 2026-10-07*
