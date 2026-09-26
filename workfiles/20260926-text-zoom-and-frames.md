# Text Zoom and Frames

> Working document — the Zoom effect on a document preview (text, PDF, other file) re-renders it:
> sharp when zooming in, more of the document when zooming out, its frames (pages) recomputed from
> what the cell can show.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the Zoom effect is a plain scaled draw of one fixed bitmap, whatever the image's source.
On a document preview this means:

- **Zooming in** magnifies the page bitmap: the text gets **pixelated**.
- **Zooming out** shrinks the page inside its cell, the Background around it: the cell shows
  **the same content, smaller**, never more of it.
- The **frames** of a document (its pages, which the Frames effect browses and plays) are laid out
  for the cell at 100 %, and ignore the zoom.

The goal: a document **re-renders** with the zoom — sharp at any zoom in, showing **more of the
document** when zooming out — and its frames are **recomputed from what the cell can show** at the
current zoom and cell size.

Components concerned (from the scout pass):

| Component | Role today |
|---|---|
| `Composition/PageSource.cs` | Abstract pages of a previewed file: `Count`, `Render(page)`, `PageSize` (set when the source takes its cell's shape), `Resize(pageSize, page)`, `LoopDuration`, `OpenAnimation()` |
| `Imaging/TextPages.cs` | Text file or pasted text; pages shaped like the cell on the 1200 px reference canvas; font = largest in [24, 96] px at which the whole text fits one page, else pages at 24 px; scrolls half a page per step when longer than a page |
| `Imaging/PdfPages.cs` | PDF pages rendered through WinRT, long side fixed at 1600 px, one page per step when animated; no `PageSize`, never re-laid out |
| `Imaging/ShellThumbnail.cs` | Any other file: the Windows Explorer thumbnail at 1024 px, one still, no pages |
| `Imaging/ImageLoader.cs` | Tries GIF → image → video → PDF → SVG thumbnail → text → shell thumbnail |
| `UI/GridPreview.cs` `FitPagesToCells` | Re-lays out a source with a `PageSize` whenever its cell's (oriented) shape changes, keeping the reading position, then re-requests the page and refreshes the player |
| `Composition/ImageLook.cs` | `Zoom` (0.1–16, default 1) and `Focus`, per image |
| `Composition/Compositor.cs` `DrawCell` + `FitCalculator` | Cover-fits the bitmap, multiplies by `Zoom`, places by `Focus`, one `DrawImage` from the full bitmap |
| `Composition/FramesEffect.cs` | Starting point as a **fraction** of the frames, and Frozen — survives a new page count |

---

## Behaviour

### Agreed

- **Zoom in** on a document: it is **re-rendered**, so the text stays sharp — no magnified bitmap.
- **Zoom out** on a text: the cell shows **more of the text** (more lines, more columns) instead of
  the same content shrunk inside a Background margin.
- **Frames recomputed**: a long document is split into frames (pages); their **number and content
  are recomputed** from what fits in the cell at the current zoom and cell size — as they already
  are for a text when its cell's shape changes.
- **Sources**: every document preview — text files (and pasted text), PDF, other files — each as far
  as its source allows (below).

### Text (text files, pasted text)

The zoom changes **how much text the cell shows**, and the pages follow. Changing page is the
Frames effect's job (it already exists), not a pan.

- **Layout**: at any zoom, the text is laid out on a page of the cell's shape **divided by the
  zoom**, at the **font size chosen at 100 %** (the largest in [24, 96] px at which the whole text
  fits one page, else 24 px). The line breaks are **recomputed** for that page. The page is then
  rendered **at its cell's size**, so the font shows at *its 100 % size × the zoom*, always sharp.
  - **Zoom out (< 100 %)**: a larger page — more lines and columns per page, **fewer pages**, the
    text smaller on screen. A text that already fits one page at 100 % shows the same text smaller,
    surrounded by more of its paper.
  - **Zoom in (> 100 %)**: a smaller page — fewer lines and columns per page, **more pages**, the
    text larger on screen, re-wrapped: no line is cut by the cell's edge.
- **Filling**: the text **always fills its cell**, at every zoom. The focus has no effect on it —
  there is nothing to pan; what shows is chosen through the pages.
- **Frames**: the page count and the scroll steps come from the zoomed page. The starting point (a
  fraction) and the reading position are kept on every re-layout, as on a cell resize today; a
  frozen text stays about where its Frames effect points.

### PDF

- **Zoom in**: the page is re-rendered at the resolution the zoomed draw needs (instead of the fixed
  1600 px long side), so it stays sharp. Its geometry does not change: the same part of the page
  shows, at the same size, as today.
- **Zoom out**: **unchanged** — the page shrinks inside its cell, the Background around it.
- **Frames**: unchanged — one PDF page per frame.

### Other Files (Explorer thumbnail)

- The app only gets a **picture** of the file from Windows (its Explorer thumbnail, usually the first
  page), never its content: it cannot lay out more of it, nor split it into pages.
- **Zoom in**: the thumbnail is requested again at the size the zoomed draw needs, so it stays sharp
  **as far as the file's thumbnail handler allows** (some cap their size).
- **Zoom out**: unchanged — the thumbnail shrinks inside its cell, the Background around it.
- **Frames**: none, as today.

### Re-rendering

- The **reference size** is the one text pages already use: the cell's size on the 1200 px canvas.
  Exports keep drawing from the same bitmap, as today.
- During a zoom **gesture** (wheel, drag), the current bitmap is scaled as today; the document is
  re-rendered once the zoom **stops changing**, so the gesture stays fluid.
- A zoomed-in PDF page or thumbnail is **capped at 4096 px on its long side**, so a 1600 % zoom
  does not allocate hundreds of megabytes: past that cap it is magnified again, slightly soft.
  A text needs no cap: it is always rendered at its cell's size.
- Only the static **Zoom** effect drives the re-render. The *Animated Zoom* effect
  (`workfiles/20260926-animated-zoom.md`) stays a scaled draw.

---

## Test Impact

**Nothing to test** — the solution has no test project, and creating one is outside this scope
(every previous workfile stayed test-free).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~At zoom in, do a text's frames stay the 100 % pages (the zoom shows a crop of each page, the
  scroll moving half a page per step), or do they follow the **visible window** (the scroll moving
  by the visible height, so every line passes through the view)?~~ → Neither: the zoom changes how
  much text shows and recomputes the pages; changing page is the Frames effect's job. The text is
  re-wrapped for the zoomed page and always fills its cell (Iteration 2)
- [x] ~~At zoom out, does a PDF show **several pages at once** (tiled to fill the cell, each frame a
  group of pages), or does its page **shrink as today**, only zoom-in sharpness changing?~~ → Shrinks
  as today; only zoom-in sharpness changes
- [x] ~~Other files: is "sharper at zoom in, unchanged at zoom out, no frames" acceptable, given the
  app only gets a thumbnail picture from Windows?~~ → Yes

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the scoping batch (Q1–Q4) and one scout pass (file previews, Zoom rendering).
Zoom in re-renders a document at the zoomed scale; zoom out lays a text out on a larger page at its
100 % font; frames recomputed from the page. Decided by the agent, open to review: the re-render
after the gesture settles, the 4096 px cap, the Animated Zoom left out, a zoomed-out text always
filling its cell. Three questions open: text frames at zoom in, PDF at zoom out, other files.

### Iteration 2 — 2026-09-26

Answers Q5–Q7. **Text**: the zoom no longer keeps the 100 % layout when zooming in — at every zoom
the text is laid out on the cell's shape divided by the zoom, at its 100 % font, re-wrapped, and
rendered at the cell's size: zoom in shows less text on more pages, zoom out more text on fewer
pages, the text always filling its cell, the focus without effect; pages are changed through the
Frames effect. **PDF**: zoom out unchanged, zoom in sharper only. **Other files**: confirmed as
designed. The 4096 px cap now concerns PDF pages and thumbnails only. No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the solution |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | On an image from a text, what should zoom in / zoom out do? | Sharp + more content: zoom in re-renders the text sharp at the scale; zoom out shows more of the text instead of shrinking the image | 2026-09-26 |
| 2 | "Refresh the frames according to what is displayable": what is it about? | Recomputed pages: a long text is split into frames (pages); their number and content are recomputed from what fits in the cell at the current zoom and size | 2026-09-26 |
| 3 | Which sources are concerned? | Text files, PDF, other files | 2026-09-26 |
| 4 | Is the exploration straightforward or tricky / long? | Straightforward — a single scout pass | 2026-09-26 |
| 5 | At zoom in, do a text's frames stay the 100 % pages or follow the visible window? | "The zoom changes the amount of text shown and recomputes the number of pages. Changing page is in Frames now (it already exists)" | 2026-09-26 |
| 6 | At zoom out, does a PDF show several pages at once or shrink as today? | Shrinks as today | 2026-09-26 |
| 7 | Other files: sharper at zoom in, unchanged at zoom out, no frames — acceptable? | Yes | 2026-09-26 |

---

*Last updated: 2026-09-26*
