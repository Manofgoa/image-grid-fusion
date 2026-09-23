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

Producers run off the UI thread, inside the existing `Task.Run` of `AddFilesAsync`.

### Paged sources

A preview with more than one page / frame keeps a handle on its source: page count (or
duration), current position, and a way to render another position. `SourceImage` keeps its role
(the bitmap the grid and the export draw); changing the position swaps the cell's bitmap. Moving
or swapping the cell keeps its position.

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
  single-file + R2R). Minimum OS becomes Windows 10 2004.
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
- Frames are taken at the nearest key frame (fast; precision irrelevant at a coarse step).
- Frame rendered at the video's native resolution.

## PDF

- Initial page 1, browsed **page by page** with the slider.
- **Whole page, best effort**: the page is rendered faithfully; the readability floor applies to
  plain text only — small body text in a PDF may stay unreadable in a small cell.
- Rendered with its long side at 1600 px (enough for the export, without pushing the canvas
  to 4096 on its own).

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

## Slider

- Drawn in `GridPreview.OnPaint`, **inside the cell, only while the cell is hovered** — a
  sibling of the × button. Hidden during a drag, like the ×.
- Only for cells whose source has more than one position.
- **Live while dragging**: each move requests the new position; only the latest request is
  rendered, intermediate ones are dropped. Rendering runs off the UI thread.
- Never exported: `Compositor.Render` draws only the bitmaps.

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

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

---

*Last updated: 2026-09-23*
