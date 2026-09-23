# Animated Content

> Working document — play multi-content sources (videos, animated GIFs, multi-page PDFs, long
> texts) live in the grid, and export the grid as an MP4 video whenever one is present, unless
> "Force as image" is checked.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every cell is a still: a video shows one frame (10 % of its duration), a PDF one page, a
text one screenful, a GIF its first frame. The hover slider (*preview from file*) lets the user
browse pages / frames by hand, and `Save…` always writes a PNG.

Goal: a cell holding **multiple content** plays it. When at least one such cell is in the grid,
`Save…` writes an **MP4 video** of the animated grid instead of a PNG. A **"Force as image"**
checkbox, visible only while multiple content is present, brings back the PNG export.

Relevant components:

| File | Role today |
|---|---|
| `Composition/PageSource.cs` | Abstract multi-page source: `Count`, `InitialPage`, `Label`, `PageSize`, `Resize`, `Render(page)` |
| `Composition/SourceImage.cs` | Cell content: owned `Bitmap`, `Dominant` color, `FilePath`, optional `Pages` + current `Page`; `ShowPage` swaps the bitmap |
| `Imaging/VideoFrames.cs` | `MediaComposition.GetThumbnailAsync` at `step × page` (step = duration / 100, ≥ 1 s), nearest key frame; duration known, no audio |
| `Imaging/PdfPages.cs` | `Windows.Data.Pdf` page rendering |
| `Imaging/TextPages.cs` | Text wrapped and paginated to fit its cell (`LinesPerPage`, `Layout.PageCount`) |
| `Imaging/ImageLoader.cs` | `TryLoadFile` entry point; GIFs are decoded as their first frame only |
| `UI/PageLoader.cs` | Renders requested pages off the UI thread, coalesced per image, raises `PageShown` |
| `UI/GridPreview.cs` | GDI+ preview; hover slider drives `PageLoader.Request(image, page)` |
| `Composition/GridLayout.cs` | Cell 0 is the featured cell (2×2 in `*-featured` layouts) and takes image 1 |
| `Composition/Compositor.cs` | `Render(images, layout, threshold)` draws each `SourceImage.Bitmap` as-is |
| `Composition/CanvasSizer.cs` | Canvas width clamped to [1200, 4096] |
| `UI/MainForm.cs` | `_saveButton` / `_copyButton` in a `FlowLayoutPanel`; `Save()` writes PNG only |

Constraints found by exploration:

- **Zero third-party dependency** today (no `PackageReference`); the README's *Tech* section
  presents the WinRT / Windows-native route as a design choice.
- Target `net10.0-windows10.0.19041.0`, single-file framework-dependent publish.
- **No test project** in the solution.
- The *scrolling mode* workfile (`20260924-scrolling-mode.md`, in design) also plans an MP4
  export, and assumes multi-content cells stay **frozen** in its video.

---

## Multiple Content

A source is **multiple** when it has more than one frame / page to show:

| Source | Multiple when | One step | Duration of one loop |
|---|---|---|---|
| Video | always | its own frames (continuous playback) | the video's duration |
| Animated GIF | more than one frame | its own frame delays | sum of its frame delays |
| PDF | more than one page | next page every **1 s** | page count × 1 s |
| Text | more than one screenful | every **1 s**, scroll by half a screenful: the new view keeps the lower **50 %** of the previous one on top | step count × 1 s |

- Text steps are **hard jumps** every second, not a smooth scroll ("every 1 second, the next
  content"). Last step: the view that shows the end of the text.
- A single-page PDF, a one-screen text, a one-frame GIF and a plain image are **still** content.
- Further formats are expected later ("on étoffera par la suite"): the design keeps one
  abstraction every format plugs into.

### Proposed model

A time-based contract next to `PageSource`: a source exposes its **loop duration** and returns
the **frame at a time `t`** (`t` taken modulo its duration). PDF and text map `t` to a page /
scroll offset; GIF to a frame index; video to a decoded frame. GIF support needs a new source
(multi-frame decoding via GDI+ `FrameDimension.Time` + the frame-delay property), since GIFs
are still-only today.

---

## Live Preview

Agreed: the preview **animates live** — videos play, GIFs animate, PDF pages and text views
advance every second in their cells.

- One shared playback clock for the whole grid, so all cells are in step.
- Frames are produced off the UI thread (same spirit as `PageLoader`).
- Video playback needs **sequential** decoding: the current `GetThumbnailAsync` per frame
  (nearest key frame) is far too slow and too coarse for 30 fps. See *Technical Route*.

To settle: how the live animation coexists with the hover slider; whether the preview plays
sound (see Open Questions).

---

## Export

Agreed:

- **Trigger**: any multiple content in the grid (not only videos) switches `Save…` to an MP4
  export.
- **"Force as image"** checkbox: visible **only** while at least one multiple content is in the
  grid; checked → `Save…` writes a PNG as today.
- **Length**: the longest loop among the multiple contents; shorter ones **loop** from their
  start until the video ends.
- **Format**: MP4 (H.264) **with sound**: the audio of the **main image** when it is a video;
  otherwise the audio of the **first video of the grid** (grid order).
- **Forced image frame**: for each multiple content, the **first frame that is not empty**
  (e.g. not a fully black frame).

Proposed (to be confirmed by reading this section):

- The export always starts at `t = 0` for every source, whatever the preview shows.
- **30 fps**, constant.
- **Canvas size**: computed once, from the frames at `t = 0`, then fixed for the whole video
  (a video needs one frame size; PDF pages may differ in size). Dimensions rounded down to even
  values (H.264). The existing 4096 px clamp keeps the canvas within the Windows H.264 encoder's
  limits.
- **Dominant color** (bands behind a cell) kept from the frame at `t = 0`, so bands do not
  flicker.
- **Audio source**: "first video of the grid" means the first video **that has an audio track**;
  the audio loops together with its video. No video with audio → silent MP4.
- **Empty frame**: a frame whose sampled pixels are almost all the same color (black, white or
  any flat color). Search order: video samples every 0.5 s, PDF pages in order, GIF frames in
  order; text is never empty. If every frame is empty → the frame at `t = 0`.
- `Save…` dialog: `MP4 video (*.mp4)` filter, default name `fusion-{timestamp}.mp4`.
- The checkbox sits in the buttons `FlowLayoutPanel`, next to `Save…`; its state is kept for the
  session.

To settle: what `Copy` does with multiple content, how progress / cancel is shown during a
long export (see Open Questions).

---

## Technical Route

The export needs: sequential frame-accurate decoding of each video, compositing each output frame
with the existing `Compositor`, H.264 encoding, and muxing one source's audio.

| Option | Decoding | Encoding + audio | Dependency |
|---|---|---|---|
| **A — Media Foundation interop** | `IMFSourceReader` (RGB32 output, sequential, fast) | `IMFSinkWriter` H.264 + the source's AAC samples muxed as-is | None (COM interop declared in the app) |
| **B — FFmpeg** | `ffmpeg` process / wrapper | `ffmpeg` | `ffmpeg.exe` (~80 MB) bundled or required on `PATH`, likely a NuGet wrapper |

- Option A keeps the README's zero-dependency choice; cost: a few hundred lines of COM interop
  declarations, and the risk that some source codecs lack a Media Foundation decoder.
- `MediaComposition` (WinRT, suggested by *scrolling mode*) fits one clip per second of stills,
  but not 30 fps per-frame compositing, nor frame-accurate sequential reads.
- The same decoder would serve the live preview (paced to the clock) and the export (as fast as
  possible).

To settle: the route (see Open Questions).

---

## Test Impact

No test project exists. The pure logic this feature adds is testable without UI: loop durations,
frame / page / scroll offset at time `t`, looping of shorter sources, export length, even-size
rounding, empty-frame detection, audio source choice. Pending the test-project question.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (pending the test-project question) | — | — |

---

## Open Questions

- [ ] Technical route: Media Foundation interop (zero dependency) or FFmpeg?
- [ ] "Main image" in a layout without a featured cell: image 1 (first cell), or no main image (straight to the first video of the grid)?
- [ ] Live animation vs hover slider: hovering a cell pauses its animation and shows the slider for manual browsing, the slider only sets where the animation resumes, or the slider goes away for multiple content?
- [ ] Sound in the live preview: silent, or the export's audio source plays?
- [ ] `Copy` with multiple content in the grid: copies the still image (first non-empty frames), copies the frames currently shown, or is disabled?
- [ ] Progress of a long video export: progress + cancel in the status line, or a modal progress dialog with Cancel?
- [ ] Scrolling mode overlap: which workfile owns the shared video export (clock, compositing per frame, encoder), and when both apply, do multi-content cells animate inside the rotation video (this workfile) or stay frozen (current *scrolling mode* assumption)?
- [ ] Tests: create a first test project to pin the timing logic, or keep the solution test-free?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial scoping with the user: four multiple-content formats in scope (video, animated GIF, PDF,
text); any of them switches `Save…` to MP4; "Force as image" checkbox shown only then; export
length = longest content, shorter ones loop; MP4 with the main image's video audio, falling back
to the first video of the grid; forced image uses the first non-empty frame; the preview animates
live. Exploration (tricky / long: scout + one deepening pass) found the `PageSource` model and
the hover slider already committed, GIFs still-only, video frames fetched by thumbnail (too slow
for playback), no audio anywhere, a zero-dependency stack, no test project, and an overlapping
video export in the *scrolling mode* workfile. Eight questions remain open.

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
| 1 | Which multiple-content formats are in scope? | Videos, animated GIFs, PDF, text | 2026-09-24 |
| 2 | Export length when contents differ in duration? | Longest one, the others loop | 2026-09-24 |
| 3 | Video format and sound? | MP4 with the sound of the main image's video | 2026-09-24 |
| 4 | Which frame does "Force as image" use? | The first frame that is not empty (e.g. not fully black) | 2026-09-24 |
| 5 | Does the in-app preview animate too? | Yes, live | 2026-09-24 |
| 6 | Without any video, does a PDF / GIF / text switch the export to video? | Yes, any multiple content | 2026-09-24 |
| 7 | Sound when the main image is not a video? | The first video of the grid | 2026-09-24 |
| 8 | Is the exploration expected to be straightforward or tricky / long? | Tricky / long | 2026-09-24 |
| 9 | Technical route: Media Foundation interop or FFmpeg? | | 2026-09-24 |
| 10 | Main image in a layout without a featured cell? | | 2026-09-24 |
| 11 | Live animation vs hover slider? | | 2026-09-24 |
| 12 | Sound in the live preview? | | 2026-09-24 |
| 13 | What does Copy do with multiple content? | | 2026-09-24 |
| 14 | Progress / cancel of a long video export? | | 2026-09-24 |
| 15 | Scrolling mode overlap: ownership and combined behaviour? | | 2026-09-24 |
| 16 | Create a first test project? | | 2026-09-24 |

---

*Last updated: 2026-09-24*
