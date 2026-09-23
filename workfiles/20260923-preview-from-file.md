# Preview From File

> Working document — turn dropped non-image files (video, PDF, text, anything Windows has a
> thumbnail for) into an image placed in the grid, with a hover slider to browse pages / frames.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a dropped (or pasted, or startup) file enters the grid only if GDI+ can decode it as an
image; anything else is counted as *"skipped: not a readable image"*. Goal: when a file can be
previewed as an image, produce that image and place it like any other.

- **Video** → a frame; **PDF** → a page; **plain text / code** → the text rendered by the app.
- **Any other file with a Windows Shell thumbnail** → that thumbnail. When Windows has none, the
  app does **not** try to reproduce one.
- **Multi-page / temporal sources** (video, PDF, long text) get a **slider inside the cell, shown
  only while the cell is hovered**, to move through pages / frames.
- **Text must stay readable in the exported image**: the font shrinks to fit the content, never
  under a readability floor; beyond that, the text is paginated.
- Files with no preview at all are **rejected as today**.

Relevant components:

| File | Role |
|---|---|
| `Imaging/ImageLoader.cs` | `TryLoadFile(path) → SourceImage?`, synchronous, the actual accept / reject boundary (GDI+ decode, EXIF rotation, 32bpp copy) |
| `Composition/SourceImage.cs` | Immutable cell content: owned `Bitmap`, `Dominant` color, `FilePath` |
| `UI/MainForm.cs` | `AddFilesAsync(paths, targetCell)` loads on `Task.Run`, then `_preview.Add`; status line reports skipped / ignored files. Drop, paste (file list) and startup files all go through it |
| `UI/GridPreview.cs` | Single GDI+ `Control`: paints every cell in `OnPaint`, tracks `_hovered`, paints the hover outline and the hover-only × button (`PaintCloseButton`, `CloseBounds`) |
| `Composition/CanvasSizer.cs` / `Compositor.cs` | Export: canvas width = the width at which **no image is downscaled**, clamped to [1200, 4096]; `Compositor.Render` draws each `SourceImage.Bitmap` in its cell |
| `ImageGridFusion.csproj` | `net10.0-windows`, WinForms, **no PackageReference**, single-file framework-dependent + ReadyToRun publish |

---

## Preview Pipeline

`ImageLoader.TryLoadFile` stays the single entry point; it tries, in order, and stops at the
first producer that succeeds:

1. **GDI+ image** — current behaviour, unchanged.
2. **Video** — frame extraction (see *Video*).
3. **PDF** — page rendering (see *PDF*).
4. **Text** — text rendering (see *Text*).
5. **Shell thumbnail** — `IShellItemImageFactory::GetImage` with `SIIGBF_THUMBNAILONLY`: an
   icon is never accepted as a thumbnail, so "Windows has no thumbnail" = failure = next step.
6. **Rejected** — counted in the status line as today (message reworded to *"no preview
   available"*).

A dedicated producer that fails (e.g. a video whose codec Media Foundation lacks) falls through
to the Shell thumbnail, then to rejection.

**Exception — `.svg`**: the Shell thumbnail is tried **before** text, so a drawing shows as a
drawing when a thumbnail handler exists (e.g. PowerToys); without one, its source is rendered as
text.

Producers run off the UI thread, inside the existing `Task.Run` of `AddFilesAsync`.

How each producer recognises its files:

| Producer | Recognised by | File |
|---|---|---|
| Video | Extension: mp4, m4v, mov, avi, wmv, asf, mkv, webm, 3gp, 3g2, mpg, mpeg, ts, m2ts, mts | `Imaging/VideoFrames.cs` |
| PDF | `%PDF-` within the first KB, whatever the extension | `Imaging/PdfPages.cs` |
| Text | Content sniffing (see *Text*) | `Imaging/TextPages.cs` |
| Shell thumbnail | Windows answers `SIIGBF_THUMBNAILONLY` (1024 px requested, alpha kept) | `Imaging/ShellThumbnail.cs` |

A PDF is read into memory, so it stays unlocked like an image; a video is read through its
`StorageFile` while it is in the grid.

### Paged sources

`Composition/PageSource.cs` — page count, initial page, a label per page, `Render(page)` (off the
UI thread, one call at a time per source), and for text only `PageSize` / `Resize`. A
`SourceImage` holds its `Pages` and the `Page` it shows; `ShowPage` swaps the bitmap and
recomputes the dominant color. Moving or swapping the cell keeps its position; removing or
replacing it disposes the source.

`UI/PageLoader.cs` renders requested pages off the UI thread, one at a time per image, keeping
only the latest request; a page that fails to render leaves the previous one shown.

---

## Dependencies

Agreed: **native Windows APIs only, no NuGet** (the dependency review's "native first"
recommendation, confirmed by the user).

| Content | API | Cost |
|---|---|---|
| Shell thumbnail | `IShellItemImageFactory` (COM interop, `SHCreateItemFromParsingName`) | 0 |
| PDF page at any resolution | `Windows.Data.Pdf` (`PdfPage.RenderToStreamAsync` + `DestinationWidth`) | 0 |
| Video frame at any time | `Windows.Media.Editing.MediaComposition.GetThumbnailAsync(time, w, h, NearestKeyFrame)` | 0 |
| Text | GDI+, in-house layout | 0 |

- Requires `TargetFramework` → `net10.0-windows10.0.19041.0` (WinRT projections via CsWinRT;
  adds a few MB of `Microsoft.Windows.SDK.NET.dll` + `WinRT.Runtime.dll`, compatible with
  single-file + R2R). Minimum OS becomes Windows 10 2004, stated in the README.
- WinRT async calls are awaited (`AsTask()`), never `.Result`.
- Known limit: video depends on the codecs installed. H.264 mp4 / mov work everywhere; HEVC needs
  the Store extension; mkv / avi are uneven. Third-party fallback (FFMediaToolkit + FFmpeg,
  30–80 MB, LGPL) is **not** planned — it breaks the "tiny exe" promise of the README.
- `Windows.Data.Pdf` does not render annotations / form fields, nor encrypted files.

---

## Video

- **Initial frame at 10 %** of the duration (skips black intro frames).
- **Slider step = duration / 100, never under 1 s**: at most 100 positions, never two positions
  less than a second apart. A video shorter than 2 s has a single position (no slider).
- Frames are taken at the **exact** time of each position (`NearestFrame`), so neighbouring
  positions never repeat the same key frame.
- Frame rendered at the video's native resolution (its encoding width × height).
- A single-position video shows the 10 % frame. Label: `m:ss`, or `h:mm:ss` past an hour.

## PDF

- Initial page 1, browsed **page by page** with the slider.
- **Whole page, best effort**: the page is rendered faithfully; the readability floor applies to
  plain text only — small body text in a PDF may stay unreadable in a small cell.
- Rendered with its long side at 1600 px, white background. Like any large image, a portrait
  page in a small or landscape cell widens the canvas, up to 4096 px, by the no-downscale rule.
- Label: `n / N`.

## Text

- **Recognition by content sniffing**, whatever the extension: size ≤ 1 MB, valid UTF-8 (BOM or
  not) or UTF-16 with BOM, and no NUL byte in the first 8 KB (for UTF-8).
- **Readability by construction**: `CanvasSizer` widens the canvas until no image is downscaled
  (up to 4096 px), so a font height expressed in **pixels of the text bitmap** is at least that
  height in the exported image. The floor is therefore a pixel value of the rendered bitmap.
- **Floor 24 px, ceiling 96 px** (font height in bitmap pixels ≈ export pixels; 24 px ≈ 12 px on
  screen when X shows the image ~600 px wide).
- **Fit to content**: the font is the largest size in [24, 96] at which the whole text fits the
  page. When even 24 px does not fit, the text is **paginated** at 24 px and browsed with the
  slider, like a PDF.
- **Page geometry follows the cell**: the page has the aspect ratio of the cell it sits in, at
  that cell's size on a 1200 px canvas (so a text page never widens the canvas on its own). It is
  **re-rendered** when that geometry changes — layout switch, image count change, cell move or
  swap. Re-pagination keeps the reading position: the new page is the one holding the first line
  of the old page.
- Long lines wrap; tabs are expanded.
- Rendering: Consolas (monospace, so columns and rows are plain arithmetic), near-black on white,
  margin 4 % of the page's shorter side (8 px minimum), tab stops every 4 columns, word wrap at
  spaces, hard break only inside a word wider than a line. Font height = em size in pixels.
- An empty or whitespace-only file is not text (no preview). Line endings are normalised.
- On load, the text is laid out on a provisional 1200 × 628 page; once placed, its cell's shape
  replaces it (the provisional page may show letterboxed for a moment).

## Slider

- Drawn in `GridPreview.OnPaint`, **inside the cell, only while the cell is hovered** — a
  sibling of the × button. Hidden during a drag, like the ×.
- Only for cells whose source has more than one position.
- **Live while dragging**: each move requests the new position; only the latest request is
  rendered, intermediate ones are dropped. Rendering runs off the UI thread.
- Never exported: `Compositor.Render` draws only the bitmaps.
- A dark pill, 24 px high, 6 px inside the cell's bottom edge, with a track, a white thumb and the
  page label (`n / N` or a time) on the right; not shown when the pill would be under 120 px wide.
  Hand cursor over it; pressing it neither selects the cell nor starts a swap.

---

## Test Impact

**No unit test changes**: the solution has no test project, and the user chose **not to create
one** — every behaviour is checked manually. The manual checks to run:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Text font fits in [24, 96] px, paginates at 24 px below it | — (manual: short / long .txt) | — |
| Text page re-rendered on layout change, reading position kept | — (manual) | — |
| Text detection accepts text of any extension, rejects binary | — (manual: .log, no-extension file, .exe) | — |
| Video: first frame at 10 %, step = duration / 100 (min 1 s) | — (manual: short and long mp4) | — |
| PDF: page 1 first, slider page by page | — (manual) | — |
| Producer order and fall-through (dedicated → Shell thumbnail → reject) | — (manual: HEVC or mkv video, .docx, .zip) | — |
| Slider only on hover, live while dragging, absent from export | — (manual, painting) | — |

---

## Open Questions

- [x] ~~Dependency policy: adopt the recommendation above (native first, TFM →
      `net10.0-windows10.0.19041.0`, no NuGet)?~~ → Yes, native only, no FFmpeg fallback
- [x] ~~PDF readability: body text of a whole page in a cell cannot reach the floor (an 11 pt line
      on an A4 page in a half-width cell ≈ 8 px). Accept whole pages as best effort, or something
      else?~~ → Whole page, best effort; the floor applies to plain text only
- [x] ~~Text page geometry: page follows its cell's aspect ratio (re-rendered when the layout or
      the cell changes), or a fixed page shape?~~ → Follows its cell, re-rendered on change
- [x] ~~Readability floor value, and a ceiling for very short text?~~ → 24 px floor, 96 px ceiling
- [x] ~~How is a file recognised as text: extension list, or content sniffing?~~ → Content
      sniffing
- [x] ~~Video: initial frame and slider step?~~ → 10 %, step = duration / 100, min 1 s
- [x] ~~Slider: image updated live while dragging, or on release?~~ → Live, latest request only
- [x] ~~Tests: create a test project for the pure logic?~~ → No, manual checks

Raised by the implementation run (outside the frozen scope, not implemented):

- [x] ~~The drop zone's file picker (another workfile) defaults to an *Images* filter: videos,
      PDFs and text need *All files*. Add them to the default filter?~~ → Already done on
      `main` by another session: `MainForm.PickerFilter` lists images, videos, PDF and text
      *(checked 2026-09-24)*
- [x] ~~SVG files are XML, so they are rendered as their **source text** (text comes before the
      Shell thumbnail). Prefer the thumbnail for svg (needs a thumbnail handler, e.g. PowerToys)?~~
      → Thumbnail first for `.svg`, source text only when Windows has none (see Iteration 5)
- [x] ~~Key-frame precision: on the generated test video, `NearestFrame` was as fast as
      `NearestKeyFrame` (~450 ms per frame). Real videos with long key-frame intervals would show
      the same frame for several slider positions. Switch to `NearestFrame`?~~ → Yes, exact frame
      (see Iteration 5)
- [x] ~~A portrait PDF page in a small or landscape cell pushes the canvas to 4096 px (bigger
      export files). Acceptable, or render PDF pages smaller?~~ → Kept as is

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design from the two scoping batches and a read-only scout pass (drop pipeline, hover
UI, dependency review):

- Producers chained behind `ImageLoader.TryLoadFile`: GDI+ → video → PDF → text → Shell
  thumbnail (thumbnail only, never an icon) → rejected as today.
- Hover-only slider inside the cell for paged sources (video, PDF, paginated text), painted
  next to the × button, never exported.
- Text: shrink-to-fit with a floor in bitmap pixels, which the no-downscale rule of
  `CanvasSizer` carries unchanged into the export; paginated below the floor.
- Dependencies: native-first recommendation, awaiting the user's confirmation.

### Iteration 2 — 2026-09-23

All open questions answered in two batches:

- Dependencies: native Windows APIs only, TFM → `net10.0-windows10.0.19041.0`, no NuGet.
- PDF: whole page, best effort — the readability floor is a plain-text guarantee only.
- Text: recognised by content sniffing; font in [24, 96] px; page shaped like its cell and
  re-rendered when the cell geometry changes, keeping the reading position.
- Video: starts at 10 %, step = duration / 100 with a 1 s minimum.
- Slider: live while dragging, only the latest position rendered.
- Tests: no test project; manual checks listed in *Test Impact*.

### Iteration 3 — 2026-09-23 — ✅ Implemented

Go given ("Allez go", after a first "No"). Scope frozen as the design sections stand in
Iteration 2: code, plus the README (the design itself states the minimum OS there); no unit
tests, as agreed. Work stays on `main` — the project's standing choice.

### Iteration 4 — 2026-09-24 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Recognition**: video by extension list; PDF by its `%PDF-` header, extension ignored; text
  as designed. An empty / whitespace-only text file has no preview.
- **Locks**: a PDF is read into memory (no lock); a video is read through `StorageFile` while
  it sits in the grid.
- **Text rendering**: Consolas, near-black on white, 4 % margin, 4-column tabs, word wrap;
  provisional 1200 × 628 layout at load, re-laid out once placed.
- **Video**: native frame size from the encoding properties; a lone position shows 10 %;
  labels `m:ss` / `h:mm:ss`.
- **Shell thumbnail**: 1024 px requested, alpha channel kept (the HBITMAP is bottom-up: copied
  row by row). As a side effect, formats GDI+ cannot decode but Windows thumbnails (webp, heic)
  now enter the grid, at up to 1024 px.
- **Slider**: geometry and label as described in *Slider*; a failed page render keeps the
  previous page.
- **Status line**: "skipped: no preview available".
- **Corrected statement**: the design said a 1600 px PDF page would not widen the canvas on its
  own — wrong in small or landscape cells (canvas up to 4096 px). Implemented as designed
  (1600 px); the *PDF* section now says so, and an Open Question asks whether to change it.
- **Verification**: no test project, so a scratch harness (outside the repo) ran every producer
  on generated samples (numbered 30 s video, text, UTF-16, empty, binary, a public PDF, docx,
  svg) and drove the real `GridPreview` offscreen: text re-laid out to 600 × 628 then 600 × 314
  after a swap, the PDF slider dragged to page 235 / 407, export free of the slider. A first
  version of the thumbnail copy came out upside down; fixed before committing.
- **Parallel sessions**: the drop-zone and delete-all-images sessions committed to `main`
  during the run; their files were never staged with this run's commits.

### Iteration 5 — 2026-09-24 — ⚙️ Post-implementation — SVG thumbnail first, exact video frames

Answers to the questions the run raised (Q&A #17–19):

- `.svg`: the Shell thumbnail is tried before text; the source text stays the fallback.
- Video: frames at the exact position (`NearestFrame`) instead of the nearest key frame.
- PDF pages keep their 1600 px long side, even when that widens the canvas to 4096 px.
- The picker filter question was already settled on `main` by another session.

### Iteration 6 — 2026-09-24 — 🧭 Implementation choices

Go for Iteration 5: **code only**. No project rule broken.

- `.svg` recognised by its extension (the thumbnail-first rule needs to know before sniffing).
- The README is **not** updated (code only was chosen): its *Previews* table still says text is
  recognised whatever the extension, without the `.svg` exception.
- Waited for another session's merge (`4e9dd79`) to finish before writing, and committed only
  `ImageLoader.cs` and `VideoFrames.cs`; that session's and the background-color session's files
  were left untouched.
- Verification: build clean; the generated test video cannot tell the two precisions apart (every
  one-second clip starts with a key frame), so the gain shows only on real videos with long
  key-frame intervals; with no thumbnail handler on this machine, `diagram.svg` still falls back
  to its source text, as designed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 6 | 2026-09-24 | TFM, page model, four producers, page loader, slider, status message; then SVG thumbnail first, exact video frames |
| Unit tests | — | 2026-09-23 | Declined: no test project (Q&A #16); harness checks listed in Iteration 4 |
| README | 4 | 2026-09-24 | Previews section, features, status line, publish path, minimum OS, tech. Iteration 5 changes declined (code only) |
| Manual validation | 4 | 2026-09-24 | Validated by the user on committed `main` (`75918c9`, which also holds the live-animation work); app closed cleanly |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which file types produce a preview in this first version? | Everything that has a Windows thumbnail — but if Windows has none, do not try to reproduce it; plus video, PDF, plain text / code | 2026-09-23 |
| 2 | How to keep text readable in the final image? | Fit to content: shrink the font to fit, never under a readability floor | 2026-09-23 |
| 3 | Which page / frame for multi-page or temporal sources? | A slider to browse: video at a logical, useful step (not frame by frame), PDF page by page | 2026-09-23 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-23 |
| 5 | A file with no Windows thumbnail, not video / PDF / text: what does the drop do? | Rejected as today | 2026-09-23 |
| 6 | Dependency policy for producing previews? | "What do you advise? Ask a Fable subagent" → recommendation recorded in *Dependencies*, confirmed in #9 | 2026-09-23 |
| 7 | Where does the page / frame slider appear? | In the cell, only while it is hovered | 2026-09-23 |
| 8 | Text too long even at the floor? | Paginate + slider | 2026-09-23 |
| 9 | Adopt the native-first dependency policy (TFM change, no NuGet)? | Yes, native first | 2026-09-23 |
| 10 | PDF readability: whole page best effort, or something else? | Whole page, best effort | 2026-09-23 |
| 11 | Text page geometry: follows the cell, or fixed shape? | Follows its cell | 2026-09-23 |
| 12 | Readability floor value and ceiling? | 24 px floor, 96 px ceiling | 2026-09-23 |
| 13 | Text recognition: extension list or content sniffing? | Content sniffing | 2026-09-23 |
| 14 | Video: initial frame and slider step? | 10 %, step = duration / 100, min 1 s | 2026-09-23 |
| 15 | Slider: live while dragging, or on release? | Live while dragging | 2026-09-23 |
| 16 | Create a test project for the pure logic? | No, manual checks | 2026-09-23 |
| 17 | SVG: render its source text, or prefer the Windows thumbnail? | Windows thumbnail first, text as the fallback | 2026-09-24 |
| 18 | Video frames: nearest key frame, or exact frame? | Exact frame | 2026-09-24 |
| 19 | Portrait PDF page pushing the canvas to 4096 px: keep, or render smaller? | Keep | 2026-09-24 |

---

*Last updated: 2026-09-24*
