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
| `Imaging/TextPages.cs` | Text file, pasted text, `.rtf` / `.html` read with their styles; pages shaped like the cell on the smallest canvas at the output format; font = largest in [24, 96] px at which the whole text fits one page, else pages at 24 px; scrolls half a page per step when longer than a page |
| `Imaging/PdfPages.cs` | PDF pages rendered through WinRT, long side fixed at 1600 px, one page per step when animated; no `PageSize`, never re-laid out |
| `Imaging/ShellThumbnail.cs` | Any other file: the Windows Explorer thumbnail at 1024 px, one still, no pages |
| `Imaging/ImageLoader.cs` | Tries GIF → image → video → PDF → SVG thumbnail → text → shell thumbnail |
| `UI/GridPreview.cs` `FitPagesToCells` | Re-lays out a source with a `PageSize` whenever its cell's (oriented) shape changes — the cells of `Compositor.Cells` on `CanvasSizer.Smallest(ratio)`, the Borders' gap included — keeping the reading position, then re-requests the page and refreshes the player |
| `Composition/ImageLook.cs` | `Zoom` (the free zoom, 0.1–16) and `ZoomFit` (Contain / Fill), read through **`ZoomIn`** (RULES.md § Rendering); `ZoomAt` / `FocusAt` add the Animations effect's Zoom motion |
| `Composition/Compositor.cs` `DrawCell` + `FitCalculator` | Cover-fits the bitmap, multiplies by `ZoomAt`, places by `FocusAt`, one `DrawImage` from the full bitmap; `FitCalculator.FocusKeeping` keeps the image in place on a zoom |
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

- **The zoom read** is `ImageLook.ZoomIn` (RULES.md § Rendering), never `Zoom`: the page having its
  cell's shape, Contain and Fill both resolve to 100 % on a re-laid-out text.
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
  there is nothing to pan; what shows is chosen through the pages. *Zoom Keeps the Image in Place*
  (RULES.md) has nothing to keep there: the **reading position** is what a zoom keeps.
- **Frames**: the page count and the scroll steps come from the zoomed page. The starting point (a
  fraction) and the reading position are kept on every re-layout, as on a cell resize today; a
  frozen text stays about where its Frames effect points.
- **With the Crop effect on**: the zoom does **not** re-lay the text out — the kept part must keep
  showing the same text. The text keeps its 100 % layout and zooms like an image (the focus moving
  it again, the zoom keeping it in place as for any image), but **sharp**: its page is rendered at
  the zoom's scale, like a PDF page (below).

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
  **as far as the file's thumbnail handler allows** (some cap their size). The thumbnail is now kept
  with a one-page source, `Imaging/ThumbnailPages.cs`, which asks Windows again.
- **Zoom out**: unchanged — the thumbnail shrinks inside its cell, the Background around it.
- **Frames**: none, as today.

### Re-rendering

- The **reference size** is the one text pages already use: the cell's size on the smallest canvas
  at the output format (`Compositor.Cells` on `CanvasSizer.Smallest(ratio)`, the gap included), so
  a format or a gap change re-lays the text out too, as today. Exports keep drawing from the same
  bitmap, as today.
- The zoom read everywhere is `ImageLook.ZoomIn` (Contain / Fill resolved), so a fit mode re-renders
  a PDF or a thumbnail like a free zoom. An undo or redo that changes the zoom re-renders too.
- During a zoom **gesture** (wheel, drag), the current bitmap is scaled as today; the document is
  re-rendered once the zoom **stops changing**, so the gesture stays fluid. A re-laid-out text is
  drawn meanwhile at its zoom divided by the one its pages hold (`ImageLook.WithZoomLaidOut`), 100 %
  once they follow it. The pages follow **200 ms** after the look's last change, no gesture running
  (`GridPreview._pagesFit`, then `FitPagesToCells`).
- **Sharp means 1:1 on the reference canvas**: a page is rendered with the pixels its drawn size
  there asks (`GridPreview.SharpSide`), never below its unzoomed resolution. A preview larger than
  the smallest canvas may still magnify it slightly, as text pages already were.
- **The canvas sizing ignores the sharpening**: it reads each image at its unzoomed resolution
  (`PageSource.Sharpening`, `Frame.SizingSize`, `SourceImage.SizingSize`), so a page rendered at
  4096 px never makes the export bigger on its own.
- The drawn geometry — preview, exports, readouts, guides — reads `SourceImage.DrawnLook`, the look
  with the zoom a text's pages hold taken out; dragging or arrow-moving a re-laid-out text does
  nothing.
- A zoomed-in PDF page, thumbnail, or cropped text page is **capped at 4096 px on its long side**,
  so a 1600 % zoom does not allocate hundreds of megabytes: past that cap it is magnified again,
  slightly soft. A re-laid-out text needs no cap: it is always rendered at its cell's size.
- Only the **Zoom** effect drives the re-render. The **Animations** effect's Zoom motion
  (`workfiles/20260926-animated-zoom.md`, `ImageLook.ZoomAt`) stays a scaled draw on top of it: a
  re-laid-out text breathes around its own page, drawn at 100 % times the motion's factor.

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

### Iteration 3 — 2026-09-30

Confirmation batch (Q8, Q10–Q12), the go having been declined. Confirmed: the zoom **re-wraps** a
text (no crop of the 100 % layout); the **4096 px cap**; the **Animated Zoom** left out. New, since
the Crop effect landed (`workfiles/20260930-crop-effect.md`): a text with the Crop on is **not
re-laid out** by the zoom — it zooms like an image, rendered sharp at the zoom's scale, under the
same cap. The re-render timing (after the gesture) stays the agent's design (Q9 not asked).

### Iteration 4 — 2026-10-10

Refresh against the rules and code that landed since Iteration 3, the design unchanged in substance
(the user chose "refresh, then go"). The zoom is read through `ImageLook.ZoomIn` (fit mode Contain /
Fill, `workfiles/20261007-zoom-fit.md`), which resolves to 100 % on a re-laid-out text; *Zoom Keeps
the Image in Place* (`workfiles/20261007-zoom-keeps-image-in-place.md`) keeps the reading position
on a re-laid-out text, the image point on a cropped one; the reference size is the cell on the
smallest canvas at the **output format**, the Borders' gap included (`FitPagesToCells` as it stands);
the *Animated Zoom* is now the **Animations** effect's Zoom motion, still a scaled draw, on top of the
re-laid-out page; an undo or redo changing the zoom re-renders. `.rtf` / `.html` texts are covered as
`TextPages`. Still no test project.

### Iteration 5 — 2026-10-10 — ✅ Implemented

Go given: code, unit tests and documentation. Scope frozen as of Iteration 4. Branch: `main`, as
the app's standing practice (work lands on main).

### Iteration 6 — 2026-10-10 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Canvas sizing unsharpened** — not in the design, needed to keep "exports as today": the canvas
  sizing reads each image at its unzoomed resolution (`PageSource.Sharpening`, `Frame.SizingSize`),
  otherwise a PDF page sharpened to 4096 px would have pushed every export to 4096 px.
- **Scaled feedback during a gesture for a re-laid-out text** — the design's "bitmap scaled as
  today" made concrete: the text is drawn at its zoom divided by its pages' layout zoom
  (`ImageLook.WithZoomLaidOut(layoutZoom)`), so it grows or shrinks under the wheel, then is laid
  out again.
- **Re-render trigger**: a timer of 200 ms restarted by every look change of a document, firing
  once no gesture runs (`GridPreview._pagesFit`); the existing `FitPagesToCells` does the layout and
  the sharpening, so cell, format, gap, undo and look changes share one path.
- **Sharpness target**: 1:1 on the smallest canvas at the output format, the reference text pages
  already used (`GridPreview.SharpSide`); a preview larger than that canvas may still magnify a bit.
- **One-page source for thumbnails** (`ThumbnailPages`): the Explorer thumbnail is now kept with a
  `PageSource`, so the existing page loader renders it again. Side effect: the Frames options
  (disabled on a still) label it `1 / 1`, as a one-page PDF.
- **Pan on a re-laid-out text** returns early (`GridPreview.PanBy`); the wheel still stores a focus,
  which has no effect until the text is cropped.
- **Margins** of a re-laid-out text keep their 100 % size on the rendered page; the layout is
  degenerate at extreme zooms (at 1600 % a cell holds a couple of characters, thousands of pages),
  as the design asks.
- **One code commit**: the text layout and the sharpening share `PageSource`, `FitPagesToCells` and
  the drawn look, so they landed together (`b1af09a`); README in its own commit.
- **Checked** with a throwaway program (scratchpad, not committed): a 120-line text on a 600 × 314
  page gives 24 pages at 100 %, 6 at 50 %, 96 at 200 %; sharpening renders a cropped text at 3×,
  capped at 4096 px, back to 1×.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-10-10 | `b1af09a` |
| Unit tests | 5 | 2026-10-10 | Not applicable — no test project in the solution; checked with a throwaway program instead |
| README | 5 | 2026-10-10 | `d764f1f` — `README.md` and `README.fr.md`: Zoom, Previews |

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
| 8 | Does the zoom re-wrap the text (line breaks recomputed, no line cut) or crop it? | Re-wrap | 2026-09-30 |
| 9 | When is a document re-rendered during a zoom gesture? | Not asked (the batch was dismissed, then trimmed to four questions) — left to the agent as designed: after the gesture, the current bitmap scaled meanwhile | 2026-09-30 |
| 10 | How far does a zoomed-in PDF page / thumbnail stay sharp (render cap)? | 4096 px | 2026-09-30 |
| 11 | Does the Animated Zoom effect also re-render a text? | No | 2026-09-30 |
| 12 | A text with the Crop effect on: does the zoom still re-lay it out, or fall back to a scaled draw? | Scaled draw (sharp, no re-layout) | 2026-09-30 |

---

*Last updated: 2026-10-10*
