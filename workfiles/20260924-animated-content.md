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

### Model (as implemented)

- `PageSource` gains `LoopDuration` (zero = still), `OpenAnimation()` → an `AnimationReader`,
  `PageAt(t)` (what the slider shows) and `TimeOf(page)` (where the animation resumes).
  `SourceImage.IsAnimated` = `LoopDuration > 0`.
- `AnimationReader.FrameAt(t)` returns the frame at `t`, or `null` when unchanged; `Reset()`
  forces the next frame after a re-layout; `ProbeTimes(loop)` lists where to look for a
  non-empty still. Each reader is independent: the preview and an export read in parallel.
- `StepReader` serves PDFs (1 s per page), texts (1 s per half-page view) and GIFs (their delays).
- `VideoReader` (Media Foundation Source Reader, RGB32) serves videos: sequential reads, a seek to
  the key frame before `t` when going back or more than 2 s ahead; crops to the minimum display
  aperture and applies the rotation stored by phones.
- `GifFrames` (new `PageSource`, tried before the plain image decoder): GDI+ frames and delays;
  delays under 20 ms are stretched to 100 ms, like browsers. GIFs get the hover slider too.
- `PdfPages` and `GifFrames` serialize their rendering (a lock): the slider and several readers
  may render at the same time.

---

## Live Preview

Agreed: the preview **animates live** — videos play, GIFs animate, PDF pages and text views
advance every second in their cells.

- One shared playback clock for the whole grid, so all cells are in step.
- Frames are produced off the UI thread (same spirit as `PageLoader`).
- Video playback needs **sequential** decoding: the current `GetThumbnailAsync` per frame
  (nearest key frame) is far too slow and too coarse for 30 fps. See *Technical Route*.
- **Hover slider**: hovering a multiple-content cell **pauses its animation** and shows the
  slider for manual browsing, as today; leaving the cell resumes the animation from where it
  stands. The other cells keep playing.
- **Sound**: the preview plays the **export's audio source** (see *Export*), in step with that
  video — it pauses while that cell is paused by the hover, and loops with it.

As implemented (`UI/AnimationPlayer.cs`, `UI/PreviewSound.cs`):

- One loop per animated image, polling every 33 ms: decode off the UI thread, show on it
  (`SourceImage.ShowFrame`, which keeps the dominant color), then redraw only that cell in the
  preview cache (`Compositor.DrawCell`).
- Frames larger than their cell are scaled down to it off the UI thread, so the UI thread draws
  about 1:1; exports use their own readers at full resolution.
- A new animated image starts on a whole second of the clock; a cell resumed after a hover goes on
  from its paused position (not re-aligned), or from `TimeOf(page)` when its slider moved it.
- The sound plays through WinRT's `MediaPlayer` (looping, no Windows media overlay), re-synced when
  it drifts more than 250 ms from the frames.
- A text laid out again (layout change) resets its reader, and its step count follows the layout.
- The preview keeps playing while an export runs.

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
- **Main image** = **image 1** (the first cell), whether the layout has a featured cell or not.
- **Forced image frame**: for each multiple content, the **first frame that is not empty**
  (e.g. not a fully black frame).
- **`Copy`** with multiple content copies an **MP4 video**: the same video as `Save…`, rendered
  to a file then put on the clipboard as a file (pastable in Explorer, chat apps, mail).
- **Progress**: percentage in the **status line** with a **Cancel** action; the app stays usable,
  but the grid is locked (no add / remove / swap / layout change) while the export runs.
- **Shared infrastructure**: this workfile **owns** the video export (playback clock, per-frame
  compositing, encoder, audio muxing). *Scrolling mode* builds on it, and multi-content cells
  **animate inside the rotation video** too — that workfile's *frozen* assumption is to be
  revised there.

Details (proposed during design, implemented as written unless noted):

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
- The checkbox sits in the buttons `FlowLayoutPanel`, **left of `Copy`** (order: *Force as image*,
  *Copy*, *Save…*); its state is kept for the session. It applies to `Copy` too: checked → `Copy`
  copies the still image, as today.
- `Copy`'s video file: written to `%TEMP%\ImageGridFusion\fusion-{timestamp}.mp4` and left there
  (the clipboard needs the file to outlive the copy); every `.mp4` there is removed at startup.
- `Save…` and `Copy` run the same export; a second export cannot start while one runs.

As implemented (`Imaging/VideoEncoder.cs`, `Imaging/GridExport.cs`, `UI/MainForm.cs`):

- **Video**: Sink Writer, H.264 High profile, about 0.12 bit per pixel and frame, clamped to
  [2, 40] Mbit/s; RGB32 input, software encoder.
- **Sound**: the source's first audio track decoded to 16-bit PCM (44.1 or 48 kHz, mono or stereo)
  and re-encoded to AAC at 192 kbit/s — not muxed as-is, so any codec Windows decodes works;
  written up to 1 s ahead of the video, looped every duration of its video, cut at the end of each
  loop and of the export. A sound that cannot be re-encoded → silent video, and the status line
  says `no sound ({file}: its sound cannot be re-encoded)`.
- **Empty frame**: 32 × 32 samples, flat when within 24 per channel of the average, empty from
  98 % flat samples; videos probed every 0.5 s, at most 240 probes (2 min).
- **Force as image with animated content**: rendered off the UI thread, grid locked, status
  `Rendering the image…`, no Cancel (not cancellable).
- **Lock** (`GridPreview.Locked`): clicks other than on a slider are ignored (no selection, no ×,
  no swap, no drop zone); drop highlight, paste, drops, `Delete`, *Clear all* and the layout strip
  are disabled; adding files says `The grid is locked until the export ends.` Closing the window
  cancels the export and closes once it has stopped.
- **Status**: `Exporting the video… 42 %` + *Cancel*; `Saved {name} (W × H, m:ss).`;
  `Copied {name} to the clipboard (W × H, m:ss).`; `Video export cancelled.`; `Export failed: …`.
  A cancelled or failed export deletes its file.

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

Agreed: **Option A — Media Foundation interop**, no new dependency. A source whose codec has no
Media Foundation decoder is reported in the status line like today's unreadable files.

As implemented: `Imaging/MediaFoundation.cs` declares the COM interfaces and entry points. Sound
is **re-encoded** to AAC rather than muxed as-is (see *Export*), so any audio codec Windows decodes
works, and looping and cutting stay sample-accurate.

---

## Test Impact

No test project exists, and the user chose to **keep the solution test-free**: no unit test is
created or updated. The pure logic (loop durations, frame / page / scroll offset at time `t`,
looping, export length, even-size rounding, empty-frame detection, audio source choice) stays
untested by decision, not because nothing testable changes.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined: no test project) | — | — |

---

## Open Questions

- [x] ~~Technical route: Media Foundation interop (zero dependency) or FFmpeg?~~ → Media Foundation interop
- [x] ~~"Main image" in a layout without a featured cell: image 1 (first cell), or no main image (straight to the first video of the grid)?~~ → Image 1, in every layout
- [x] ~~Live animation vs hover slider: hovering a cell pauses its animation and shows the slider for manual browsing, the slider only sets where the animation resumes, or the slider goes away for multiple content?~~ → Hover pauses the cell's animation and shows the slider
- [x] ~~Sound in the live preview: silent, or the export's audio source plays?~~ → The export's audio source plays
- [x] ~~`Copy` with multiple content in the grid: copies the still image (first non-empty frames), copies the frames currently shown, or is disabled?~~ → Copies an MP4 video
- [x] ~~Progress of a long video export: progress + cancel in the status line, or a modal progress dialog with Cancel?~~ → Status line + Cancel
- [x] ~~Scrolling mode overlap: which workfile owns the shared video export (clock, compositing per frame, encoder), and when both apply, do multi-content cells animate inside the rotation video (this workfile) or stay frozen (current *scrolling mode* assumption)?~~ → This workfile owns it; contents animate inside the rotation video
- [x] ~~Tests: create a first test project to pin the timing logic, or keep the solution test-free?~~ → No tests
- [ ] *(found during the run, out of scope)* The live preview draws each new animated frame without the image's look from *action on image* (rotation, flips, black & white, zoom): `GridPreview.RedrawCell` builds `new Frame(image.Bitmap, image.Dominant)` without `image.Look`, while full redraws and exports apply it. Fix it here or in *action on image*?
- [ ] *(found during the run)* The rotation phones store in videos (`MF_MT_VIDEO_ROTATION`) is applied clockwise to played frames, untested on a real rotated file: to check by hand with a portrait phone video.

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

### Iteration 2 — 2026-09-24

All eight open questions answered. Media Foundation interop (no dependency) for decoding and
encoding; main image = image 1 in every layout; hovering a cell pauses its animation and shows
the slider; the preview plays the export's audio source; `Copy` copies an MP4 video (file on the
clipboard, written under `%TEMP%`); export progress + Cancel in the status line, grid locked
meanwhile; this workfile owns the shared video export and contents animate inside the
*scrolling mode* rotation video; no test project. Added with them, for the user to read:
"Force as image" applies to `Copy` too, one export at a time, temp videos of previous sessions
cleaned at startup.

### Iteration 3 — 2026-09-24 — ✅ Implemented

Go given ("Vas y implémente") after a first "No". Scope frozen on the design sections as they
stand. Taken as code + README (the tests were already declined); the run stays on `main`, the
standing choice for this repository.

### Iteration 4 — 2026-09-24 — 🧭 Implementation choices

No project rule was broken. Choices the frozen design did not state:

- **Go read as code + README**: "Vas y implémente" named no option; the tests were already
  declined, so the only difference was the README, updated.
- **GIFs get the hover slider** (frame by frame): making them a `PageSource` gives it for free,
  and it matches the hover rule of the other animated sources. Delays under 20 ms → 100 ms.
- **Preview frames scaled down** to their cell off the UI thread (performance); exports decode at
  full resolution with their own readers.
- **Resume after a hover** is not re-aligned to the whole second: a held cell may step a fraction
  of a second apart from the others afterwards.
- **Sound re-encoded** to AAC (PCM 44.1 / 48 kHz, mono / stereo, 192 kbit/s) instead of muxing the
  source's AAC as-is: works for every codec Windows decodes, and loops / cuts cleanly.
- **Encoder settings**: H.264 High profile, ~0.12 bit per pixel and frame, [2, 40] Mbit/s,
  software encoder.
- **Video rotation and display aperture** applied to played frames; the rotation is untested on a
  real phone video (see Open Questions).
- **Seek rule**: back in time, or more than 2 s ahead → seek to the key frame before.
- **Empty-frame thresholds**: 32 × 32 samples, ±24 per channel, 98 %; video probes every 0.5 s,
  240 at most.
- **Forced still with animated content** runs off the UI thread with the grid locked, without
  Cancel.
- **Checkbox placement**: left of `Copy` rather than right next to `Save…`, so Copy and Save stay
  side by side.
- **Temp cleanup** removes every `.mp4` of `%TEMP%\ImageGridFusion` at startup, including one a
  second running instance just copied.
- **Lock scope**: while locked, clicks outside the sliders are ignored altogether (no selection
  either); the preview keeps playing.
- **Verification without a test project**: two throwaway harnesses in the session scratchpad (not
  in the repository) encoded a synthetic video with a generated sine sound and checked the decoded
  orientation, frame timing, seek back, sound presence and 7 s looping, even canvas size, forced
  still skipping a black intro, cancellation deleting the file, text steps, live playback, hover
  hold / resume, and a full `MainForm` export with the grid locked then unlocked.
- **Parallel sessions**: *action on image* integrated its looks into `GridExport` right after this
  run's export commit (its own commit `d9c2b1f`); the preview's per-frame redraw still ignores
  looks (see Open Questions). The *scrolling mode* workfile still assumes frozen contents and a
  `MediaComposition` encoder: to revise there.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-09-24 | Animation model, PDF / text / GIF animation, Media Foundation reader and encoder, grid export, live preview with sound, Save / Copy MP4 with Force as image, progress, Cancel and lock |
| Unit tests | 2 | 2026-09-24 | Declined: the solution stays test-free (throwaway scratchpad harnesses used instead, see Iteration 4) |
| README | 3 | 2026-09-24 | *Animated content* section, Features, Previews (animated GIF), Output |
| Manual validation | 4 | 2026-09-24 | Tested by the user in the app built from `main` (`7d4a782`), closed without error; finalized at the user's request |

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
| 9 | Technical route: Media Foundation interop or FFmpeg? | Media Foundation interop | 2026-09-24 |
| 10 | Main image in a layout without a featured cell? | Image 1 | 2026-09-24 |
| 11 | Live animation vs hover slider? | Hover pauses the cell's animation and shows the slider | 2026-09-24 |
| 12 | Sound in the live preview? | The export's audio source plays | 2026-09-24 |
| 13 | What does Copy do with multiple content? | Copies an MP4 video | 2026-09-24 |
| 14 | Progress / cancel of a long video export? | Status line + Cancel | 2026-09-24 |
| 15 | Scrolling mode overlap: ownership and combined behaviour? | This workfile owns the export; contents animate in the rotation video | 2026-09-24 |
| 16 | Create a first test project? | No | 2026-09-24 |
| 17 | Go for the implementation? | No (first ask), then "Vas y implémente" | 2026-09-24 |

---

*Last updated: 2026-09-24*
