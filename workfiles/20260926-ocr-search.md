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
(background pass, progress bar), `UI/ThumbnailGrid.cs` (content badge), the ⚙ menu (rebuild entry).

---

## Existing Code (exploration, 2026-09-29)

- **Index** — `FileIndex` holds a `List<IndexEntry>`, each carrying `RelativePath`, `Folded`,
  `NameStart` only: **no size, no last-write time**. `files.index` (next to the exe) is three header
  lines, then one relative path per line; `Load` already tolerates extra tab-separated columns
  (drops them), `Save` never writes any. A rescan (`FileIndex.Scan`, start-up and ↻) rebuilds the
  list **from scratch**, reading names only, and indexes every file whatever its type.
- **Search** — `FileSearch.Search` runs on every keystroke, on the UI thread, no debounce: the query
  is folded (accents dropped, lower case) and split on white space; **every word** must be a
  substring of the folded relative path; ranked by name hits, then first position in the name,
  then name length, then path; only the **best 10** are kept (`FileSearch.Limit`), the caption
  saying *N results — first 10*.
- **Tiles** — `ThumbnailGrid` paints each `ExplorerRow(FullPath, Name)`; the heart medallion
  (`HeartBounds`, `PaintTile`) is the model for a second badge.
- **Background work** — `FileExplorerPanel.Restart()` cancels and renews one
  `CancellationTokenSource` shared by the scan; `ScanAsync` reports through
  `IProgress<ScanProgress>` to the status line. `ThumbnailCache` runs its own single-worker queue
  raising `Loaded` — the template for a throttled queue.
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
  does not exist is **moved** into `Index\`, so the upgrade does not rescan from nothing.
- `favorites.txt` **stays next to the exe**: it is not indexing data.

## Extraction

- **When** — in the **background**, after each scan (start-up and ↻), for the files whose content
  text is **missing or stale**; a search reads the cache only, never the disk.
- **Change detection** — the scan now reads each file's **size + last-write time** (from the
  enumeration's file info, no extra open). `files.content` keeps, per file, the stamp it was
  extracted at; a file whose stamp still matches keeps its text, the others are queued. A file with
  no text (a video, an image with no word, a failed extraction) is recorded **with an empty text**,
  so it is not retried until it changes.
- **Worker** — one file at a time, on a background worker, cancelled with the scan (folder change,
  ↻, closing the app); the results written to `files.content` as the pass progresses (saved
  atomically, `.tmp` then move, like `files.index`), so a pass cut short resumes where it stopped.
- **OCR engines** — **French and English**: one `OcrEngine` per language, each created when its
  Windows OCR language is installed; an image is recognised by each engine and the texts are
  concatenated. With only one of the two installed, that one alone; with neither, the engine of the
  user profile's languages; with no OCR language at all, images and PDFs get no text (text files
  still do).
- **Images** — decoded with `BitmapDecoder` into a `SoftwareBitmap` (a GIF gives its first frame);
  an image longer than `OcrEngine.MaxImageDimension` on a side is **downscaled** to it, not skipped.
- **PDF** — its **first page only**, rendered by `Windows.Data.Pdf` at an OCR resolution (larger
  than the preview's 1600 px), then recognised like an image.
- **Text** — a file passing the `TextPages` text test (≤ 1 MB, UTF-8 / UTF-16): its text. An
  `.htm` / `.html` file passing it: its text with the tags stripped, entities decoded.
- **Cap** — each content text is kept to its **first 32 KB**, white space collapsed.
- **Time budget** — none: every source is bounded (one image, one PDF page, ≤ 1 MB of text).
- **Rebuild** — a ⚙ menu entry, **Rebuild content index**, clears every content text and queues
  the whole folder again (after installing an OCR language, for instance).

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
  its content; within each group, the current ranking (name hits, first position, name length,
  path). The best 10 still show, *N results — first 10* counting both groups.
- **Before the cache is loaded** — the search matches paths only, as today, and refreshes once the
  content texts arrive.

## Content Badge

- A tile found **thanks to its content** (at least one word matched in its text, not in its path)
  carries a **badge**: a small medallion like the heart's, in the tile's **opposite top corner**,
  holding a **T**.
- Badge only — no tooltip, no snippet. Tiles found by their name alone, and the favorites shown
  while the search box is empty, have no badge.

## Progress Bar

- A **miniature progress bar** directly **under the search box**, as wide as the box, **3 px**
  high (scaled with the DPI), **blue**, on a transparent track.
- It shows **every indexing job** — the scan, the content extraction, and any future indexing job:
  proportional to the job's progress (files done / files to do), a job whose total is not known yet
  (the scan's counting phase) shown as a short segment sweeping the track.
- Its 3 px row is **always reserved**, so nothing moves when it appears; the bar is **hidden while
  no job runs**.
- The scan's existing status text stays as it is.

---

## Test Impact

**None.** The repository has **no test project**, and every previous workfile shipped without unit
tests, verified by hand (`workfiles/20260926-file-explorer.md`, Q&A #13 — the standing choice, kept
here). The checks to run at delivery:

| Behaviour | Check |
|---|---|
| OCR of an image | A screenshot holding a distinctive word: found by that word once the pass is over, with the T badge |
| French and English | A French word with accents typed without them, and an English word: both found |
| OCR of a PDF | A scanned PDF and a native-text PDF: found by a word of their first page, not of their second |
| Text / HTML | A `.txt` note found by a sentence fragment; an `.html` page by a word of its text, not by a tag name |
| Cap | A text file whose word sits beyond its first 32 KB: not found by it |
| Mixed words | One word in a file's name, another in its text: found, with the badge |
| Name first | A word in one file's name and in another file's text: the name match ranks first |
| Large image | An image longer than the OCR maximum: downscaled and read |
| Incremental | Restart the app: nothing is re-extracted; edit one file: only that file is |
| Rebuild | ⚙ *Rebuild content index*: every file extracted again, the bar running |
| Cancellation | Change the base folder or press ↻ during the pass: it stops and restarts cleanly |
| Index folder | An existing `files.index` next to the exe moved into `Index\` at start-up, no full rescan from nothing; `favorites.txt` untouched |
| Progress bar | 3 px, blue, under the box, during the scan and the extraction; hidden when idle, nothing moving |

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

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | Not started |
| Unit tests | | | None planned — no test project (see *Test Impact*) |
| README | | | Not started — the file explorer's section, plus the Glossary (*Content text*, *Index folder*) |

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

---

*Last updated: 2026-09-29*
