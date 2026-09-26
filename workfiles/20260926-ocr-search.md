# OCR Search

> Working document — **pre-created** on 2026-09-26, during the design of
> `workfiles/20260926-file-explorer.md`, at the user's request: recognise the text inside the
> indexed images, so the file explorer's search also finds a file by the words it shows, not only
> by its name. **Its design has not started**: the scoping batch is still to be asked.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer (`workfiles/20260926-file-explorer.md`) indexes every file under a base folder
and matches the words typed against the relative path. This follow-up runs an **OCR** over the
indexed images and stores the recognised text **in the index**, so a search also matches the
words visible inside an image — a screenshot found by a word it displays, a meme by its caption.

Depends on the file explorer: its index format already tolerates **extra tab-separated columns**
after the relative path, reserved for this text; its scan, progress line and `↻` button are the
natural hooks for the OCR pass.

Candidate engine: `Windows.Media.Ocr` — built into Windows 10, offline, no third-party library,
reachable from the app's `net10.0-windows10.0.19041.0` target like the WinRT APIs it already uses
(`Windows.Data.Pdf`, `Windows.Media.Editing`), the languages being those installed in Windows.

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones. To be completed by the scoping batch when the task starts.

- [ ] Engine: `Windows.Media.Ocr` (built in, offline, the installed Windows languages), or a
  third-party one (Tesseract) despite the app's *Windows components only* rule?
- [ ] Which files: raster images only, or also the PDF pages and the videos' first frame the app
  can already render?
- [ ] When: right after each scan in the background, throttled, only for new or changed files
  (the index would then keep a size + last-write stamp per entry), or on demand?
- [ ] Storage: the text in `files.index` (the reserved column), or a sidecar file?
- [ ] Matching: the OCR words count like the path's, or rank below a name match?
- [ ] Feedback: how a row found by its content is told apart (a marker, the matching snippet in
  the tooltip)?
- [ ] Progress: *OCR 12/346* on the panel's status line, like the scan?
- [ ] Limits: a maximum image size, a per-file time budget, a way to rebuild the OCR column?

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

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | Not started |
| Unit tests | | | Not started |
| README | | | Not started |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|

---

*Last updated: 2026-09-26*
