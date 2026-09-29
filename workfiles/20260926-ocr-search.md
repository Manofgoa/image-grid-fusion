# Content Search (OCR and Text)

> Working document — the file explorer's search also finds a file by the **text it contains**:
> the words recognised (OCR) in its images and PDF pages, and the native text of the text files.
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

Sources of the content text:

| File | Content text |
|---|---|
| Still image (PNG, JPG, BMP, GIF, TIFF, WebP…) | OCR of the image |
| PDF | OCR of its rendered pages (see *PDF* below) |
| Text file, and other files holding text | Their native text, read — no OCR |
| Video, animated GIF frames beyond the first, anything else | None |

Everything stays within the app's *Windows components only* rule (README): no third-party
library, no package.

Components touched: `Explorer/FileIndex.cs` (entries, scan, file), `Explorer/FileSearch.cs`
(matching, ranking), `UI/FileExplorerPanel.cs` (background pass, status line), `UI/ThumbnailGrid.cs`
(content-match indicator), a new extractor under `Explorer/` or `Imaging/`.

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
  raising `Loaded` — the template for a throttled queue repainting tiles as results arrive.
- **OCR** — `Windows.Media.Ocr` is reachable from the current target
  (`net10.0-windows10.0.19041.0`) with no project change. It takes a `SoftwareBitmap`:
  `Windows.Graphics.Imaging.BitmapDecoder` (WIC) decodes a file straight into one — every WIC
  format, WebP and HEIC included when their Windows codecs are installed, at full resolution
  (the app's GDI+ / shell-thumbnail fallback would give a low-resolution WebP).
- **PDF** — `Imaging/PdfPages.cs` renders pages with `Windows.Data.Pdf` (thread-safe, fixed
  1600 px long side, a private constant). `Windows.Data.Pdf` has **no text API** and the app has no
  other PDF reader: a PDF's text, even a native text layer, is reachable only by OCR of its pages.
- **Text** — `TextPages.TryReadText` already decides what a text file is, by content, not
  extension: at most 1 MB, not empty, UTF-16 with a BOM or valid UTF-8 with no NUL in its first
  8 KB. `HtmlReader` / `RtfReader` parse clipboard HTML / RTF into `StyledText`.
- **Tests** — the solution has **no test project**; nothing references `FileIndex` / `FileSearch`
  but the explorer's own files.

---

## Extraction

- **When** — in the **background**, after each scan (start-up and ↻), for the files that are
  **new or changed** since their text was extracted; the text is **cached**, and a search reads
  the cache only, never the disk.
- **Change detection** — each entry keeps the file's **size + last-write time** at extraction; a
  rescan carries a file's content text over when both still match, and queues the file again
  otherwise.
- **Order and throttling** — one file at a time, on a background worker, cancelled with the scan
  (folder change, ↻, closing).
- **Images** — decoded with `BitmapDecoder` into a `SoftwareBitmap`, recognised with
  `Windows.Media.Ocr`.
- **PDF** — its pages rendered by `Windows.Data.Pdf` at an OCR resolution, each recognised.
- **Text** — read as text, no OCR.

## Search

- **Unified** — the existing search box finds a file whose **name or content** holds the words.
- **Indicator** — a tile found by its content only carries a small indicator.

---

## Test Impact

**None.** The repository has **no test project**, and every previous workfile shipped without unit
tests, verified by hand (`workfiles/20260926-file-explorer.md`, Q&A #13 — the standing choice, kept
here). The checks to run at delivery:

| Behaviour | Check |
|---|---|
| OCR of an image | A screenshot holding a distinctive word: found by that word once the pass is over, with the indicator |
| OCR of a PDF | A scanned PDF and a native-text PDF: both found by a word of an inner page |
| Text file | A `.txt` / `.md` note: found by a sentence fragment it holds |
| Name first | A word both in a file's name and in another file's content: the name match ranks first |
| Incremental | Restart the app: nothing is re-extracted; edit one file: only that file is re-extracted |
| Cancellation | Change the base folder or press ↻ during the pass: it stops and restarts cleanly |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~Engine: `Windows.Media.Ocr`, or a third-party one (Tesseract)?~~ → Not asked as such:
  `Windows.Media.Ocr` is the only engine the *Windows components only* rule allows; its
  **languages** remain open below.
- [x] ~~Which files?~~ → Images + PDF by OCR (Q&A #1), plus the native text of text files and
  other files holding text (Q&A #5) — which formats exactly remains open below.
- [x] ~~When?~~ → In the background after each scan, cached, new or changed files only (Q&A #2).
- [x] ~~Matching: the OCR words count like the path's?~~ → Unified with the name search (Q&A #3);
  the ranking remains open below.
- [ ] **OCR languages**: the languages of the Windows user profile (`TryCreateFromUserProfileLanguages`,
  one engine), or French **and** English whenever both are installed (two passes, slower)?
- [ ] **PDF text layer**: OCR of the rendered pages only (the only Windows API route), or a minimal
  built-in reader of the native text layer (Deflate + text operators, exact on simple PDFs, blind to
  custom font encodings), falling back to OCR?
- [ ] **PDF pages**: every page, or the first N (e.g. 10) to keep the pass short on long documents?
- [ ] **Other files holding text**: beyond plain text files (detected by content like `TextPages`),
  which formats — HTML (tags stripped), RTF, Office Open XML (`.docx` / `.xlsx` / `.pptx`, read as
  zipped XML with `System.IO.Compression`)?
- [ ] **Storage**: a sidecar file next to `files.index` (e.g. `files.content`), or the reserved
  extra column of `files.index` itself?
- [ ] **Text cap**: keep the whole text, or its first N KB per file (e.g. 32 KB), so the cache stays
  quick to load and each keystroke quick to match?
- [ ] **Mixed matching and ranking**: may one word match the name and another the content (every
  word in *name or content*), and do content matches rank **below** every name match?
- [ ] **Indicator**: what the tile shows for a content match — a small badge in the corner opposite
  the heart, the matching snippet in its tooltip, or both?
- [ ] **Progress**: the pass shown on the panel's status line (*Text 12 / 346*), like the scan, or
  silent?
- [ ] **Limits**: skip images above `OcrEngine.MaxImageDimension` (downscale instead?), a per-file
  time budget, and a way to rebuild every content text (e.g. ↻ with Shift, or a ⚙ menu entry)?

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
reachable as-is, `Windows.Data.Pdf` has no text API, no test project. The questions the design
cannot settle alone are listed under *Open Questions*.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | Not started |
| Unit tests | | | None planned — no test project (see *Test Impact*) |
| README | | | Not started |

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
| 6 | OCR languages: the Windows user profile's, or French and English both? | | |
| 7 | PDF text layer: OCR of the pages only, or a minimal built-in text-layer reader with OCR as fallback? | | |
| 8 | PDF pages: every page, or the first N? | | |
| 9 | Other files holding text: HTML, RTF, Office Open XML? | | |
| 10 | Storage: a sidecar file, or the extra column of `files.index`? | | |
| 11 | Text cap: the whole text, or the first N KB per file? | | |
| 12 | Mixed matching (one word in the name, another in the content) and content matches below name matches? | | |
| 13 | Indicator: badge, snippet tooltip, or both? | | |
| 14 | Progress on the status line, or silent? | | |
| 15 | Limits: oversized images, per-file time budget, a full rebuild command? | | |

---

*Last updated: 2026-09-29*
