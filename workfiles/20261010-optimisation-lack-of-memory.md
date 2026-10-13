# Optimisation: Lack of Memory — Video Export

> Working document — why the MP4 / GIF export runs out of memory when several videos are in the
> grid, how to bound that memory so the export never crashes, and the performance leads found on
> the way (export and preview).
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

With several videos in the grid, the animated export (Copy / Save as MP4 or GIF) often dies in the
.NET "unhandled exception" dialog:

```
System.OutOfMemoryException: Insufficient memory to continue the execution of the program.
   at System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(Int32 e…)
   at ImageGridFusion.Imaging.MediaFoundation.CreateMemoryBuffer(Int32 …)
   at ImageGridFusion.Imaging.VideoEncoder.WriteFrame(Bitmap frame, TimeSpan …)
```

Goals, in order:

1. **Never crash**: an export that cannot get its memory ends with a clear message, the grid and
   the last video untouched.
2. **Bound the export's memory** so that case stays rare: fewer bytes per frame, fewer frames in
   flight, fewer decoders open at once, fewer parked images.
3. **Performance leads**, export and preview — the cheap ones in scope.

Components: `Imaging/GridExport.cs` (the render loop), `Imaging/VideoEncoder.cs` and
`Imaging/MediaFoundation.cs` (the sink writer), `Imaging/VideoReader.cs` (the source reader),
`UI/AnimationPlayer.cs` (the preview's playback), `UI/GridHistory.cs` (the undo history),
`UI/MainForm.cs` (the export orchestration, the clipboard, the ⚙ menu), `Composition/Compositor.cs`
and the effects (the per-frame draw path).

Exploration: two passes on 2026-10-10 — six read-only scouts, then four deepenings (the sink
writer's back-pressure, the per-frame allocations, the memory resident during an export, the
preview's quick wins). Nothing was measured on a real export yet: the figures below are computed
from the code, and § 3 adds the probe that settles them.

---

## Symptoms — What Happens Today

### The crash

- Every frame of the export (30 per second of video) is drawn on one canvas `Bitmap` and handed to
  Media Foundation by `VideoEncoder.WriteFrame`, which asks `MFCreateMemoryBuffer` for a **fresh
  system-memory block the size of the whole canvas in RGB32** (W × H × 4 bytes), copies the frame
  into it, calls `WriteSample`, then drops its own reference.
- The canvas is the one `CanvasSizer.Compute` gives — "the size at which no image is downscaled",
  longer side clamped to [1200, 4096] px (RULES § Output Format). With several videos it sits **at
  the 4096 clamp**: one frame is **36 MiB** (16:9, 4096 × 2304), **64 MiB** (Square, 4096 × 4096),
  35 MB (Twitter, 4096 × 2143).
- When Windows refuses that block, `MFCreateMemoryBuffer` returns `E_OUTOFMEMORY`, which
  `Marshal.ThrowExceptionForHR` raises as `OutOfMemoryException`. The process is **x64** (`win-x64`,
  no GC cap, `ImageGridFusion.csproj`): the refusal means the **system commit limit** (RAM plus
  pagefile, shared by every process) is reached — not an address-space limit.
- The export runs in `Task.Run` (`MainForm.ExportAnimationAsync`). Its `catch` filter lists
  `ExternalException`, `InvalidOperationException`, `IOException` and `UnauthorizedAccessException`
  — **not `OutOfMemoryException`** (and the Media Foundation failure is no `COMException`:
  `E_OUTOFMEMORY` has its own mapping). The `finally` unlocks the grid, then the exception escapes
  through the `async void` `Copy` / `SaveAs` handlers to `Application.ThreadException`, and the app
  has **no handler** (`Program.Main`): WinForms shows its default dialog — the one of the report —
  with *Continuer* / *Quitter*.
- Why the **largest allocation fails first**: the per-frame buffer is the biggest single native
  request of the loop, so it is where the shortage surfaces, whichever code consumed the memory.

### Where the memory goes

The audit found **no leak at frame rate** in the normal path: every `Bitmap`, `Graphics`, `Region`,
`ImageAttributes`, sample and buffer of the per-frame draw path is released by `using` or `finally`;
the export's loop reuses one canvas; the GIF encoder streams through WIC. The memory is a **peak**,
proportional to the number of videos and to their native size, plus one unmeasured unknown.

Per video, with F = W × H × 4 bytes per frame (1080p: 8.3 MB; 4K: 33 MB), during an export:

| Holder | Size | Why it is there |
|---|---|---|
| The **preview's reader**, left open while the grid is held | ≈ 4 F (read-ahead sample, decoder buffers, RGB32 converter) | `BeginExport` only pauses the clock: `AnimationPlayer.Hold` cancels nothing, the readers stay open, frozen |
| The **export's own reader** | ≈ 4 F | Opened eagerly in `GridExport.FirstFrame`, kept to the end |
| The export's **current frame** of that cell, plus the next one during the swap | 1–2 F | `VideoReader.ToBitmap` allocates a native-size bitmap per decoded frame; the previous one is disposed once the new one exists |
| The **shown frame** in the grid | F, or the cell's size when scaled down | `SourceImage.Bitmap` |
| An **audio voice** if the video is heard | Small: 0.1 s PCM steps, 1 s ahead | `VideoEncoder.Mixer` — bounded, verified |

≈ **10 F per video**: about 85 MB per 1080p video, about 330 MB per 4K video. Four 4K videos: about
1.3 GB before anything else.

On top of it, once per export:

| Holder | Size |
|---|---|
| The canvas, the writer's RGB32 → 4:2:0 converter, the software H.264 encoder's working frames | ≈ 3.5 × canvas ≈ 130 MB at 4096 × 2304 |
| The **undo history**: every image that left the grid (replaced, removed, cleared) is parked, with its native bitmap and its page source, until its step drops off — up to **50 steps** | 0 to **1.6 GB** with 4K videos replaced one after the other (8.3 MB per parked 1080p video, 33 MB per 4K one, the whole file for a GIF, the whole PDF for a PDF page) |
| The file explorer's thumbnail caches, the preview cache, the process base | ≈ 250–300 MB |
| **The sink writer's own queue of accepted RGB32 frames** — the unknown | Its default throttling "limits the data rate by blocking `WriteSample`", and Microsoft documents neither the unit nor the depth. If the software encoder lags behind the renderer and the writer holds a few seconds of 4096-wide frames, that is **gigabytes** (36–64 MiB each, plus the converter's copy) |

Worked totals, without the writer's queue: 4 videos at 1080p ≈ 0.65–1.1 GB; 4 videos at 4K ≈
1.6–3.2 GB. A laptop with 8 GB of RAM, a browser open and a system-managed pagefile reaches its
commit limit there; a 16 GB desktop needs the writer's queue to grow — which is what the probe
(§ 3) will show first.

### What the GC cannot do about it

GDI+ bitmaps and Media Foundation buffers are **native** memory. The managed heap grows by a few KB
per frame, so the garbage collector never runs during an export, and it would have nothing to
reclaim anyway: nothing is leaked. `GC.Collect` or `GC.AddMemoryPressure` would change nothing —
the memory is **held on purpose**, by readers and buffers that are alive. The fix is to hold less,
and to fail cleanly when even that is too much.

### Secondary findings

- **A Square export at 4096 × 4096** is 65 536 macroblocks, above H.264 Level 5.2 (36 864, i.e.
  4096 × 2304). Whether the inbox encoder refuses it or writes a non-compliant stream is unverified;
  the video size cap (§ 2.3) keeps every format inside Level 5.1.
- **A GIF copied to the clipboard** is read whole into memory (`File.ReadAllBytes`), wrapped in a
  `MemoryStream` for the `"GIF"` clipboard format, then copied again by
  `Clipboard.SetDataObject(copy: true)`: two to three times the GIF's size, repeated by *Copy last
  GIF*. The MP4 goes as a file-drop list only. The last video and the previous copy live on disk
  only.
- **Exception-path leaks** (one-shot, not at frame rate): `VideoReader.ToBitmap` allocates its
  buffer and bitmap before the `try` (the buffer leaks if `new Bitmap` throws — likely under memory
  pressure, making a shortage worse); `VideoReader.ReadAhead` loses a sample if `ReadFormat` throws;
  `AnimationPlayer.ScaleDown` leaks the native frame if `new Bitmap` throws; `SeamField.Strip` never
  disposes its bitmap on an exception; `GridExport.FirstFrame` leaks a page if `BandColor.Of`
  throws; `VideoFrames` has no `Dispose`, so a parked video's `MediaComposition` waits for a
  finalizer.

---

## Design

### 1. Never Crash — Clean Failure

- `OutOfMemoryException` joins the `catch` filters of `MainForm.ExportAnimationAsync`, the
  clipboard step of `Copy`, `CopyLastVideo` and the still export, with the status-line message
  **"Not enough memory to export — close other applications, or lower the video size (⚙ menu)"**;
  the partial file is deleted (already done by `GridExport.RenderAnimation`'s `finally`), the last
  video and the previous copy stay as they were (already the case). `GridExport.Job.Capture` — the
  stills copied on the UI thread — moves inside the same `try`.
- A **global last resort** in `Program.Main`: `Application.ThreadException` and
  `AppDomain.CurrentDomain.UnhandledException` write the exception to `errors.log` next to the exe
  (the app's data lives beside it, RULES § App Settings) and show a plain message box naming the
  error, the app staying up — never the .NET dialog again.

### 2. Bound the Export's Memory

#### 2.1 The preview's readers released during an export

- `BeginExport` **closes the preview's readers**: `AnimationPlayer` cancels every playback loop so
  the existing `finally` releases its reader (never a direct `Dispose` from the UI thread: a decode
  may be in progress). The images **keep their shown frame and their position**; the clock stays
  held as today.
- `EndExport` **reopens them**: the playbacks are synced again and sought to the grid time, so the
  grid resumes exactly where it stood — RULES § Transport still holds: the export "holds the grid …
  then gives it back as it was".
- Saving: ≈ 4 F per video — 130 MB for four 1080p videos, 530 MB for four 4K ones. A short hitch
  when four 4K readers reopen after the export is accepted.
- The preview's sound voices follow the same rule (released on hold, reopened on release); nothing
  is heard while held anyway.

#### 2.2 Back-pressure on the sink writer

- `MediaFoundation.cs` gains the `MF_SINK_WRITER_STATISTICS` struct (`SinkWriterStatistics`,
  112 bytes, `cb` set before the call) and `IMFSinkWriter.GetStatistics(int streamIndex,
  ref SinkWriterStatistics)` — the method is already declared in the right vtable slot; only its
  parameter changes.
- `VideoEncoder.WriteFrame` **waits before allocating** while `SamplesReceived − SamplesEncoded >
  MaxFramesInFlight` (3 — the inbox encoder works in slice mode, one picture at a time, no B-frames
  by default), sleeping 1 ms between reads, and **gives up waiting after 2 s without progress** so a
  slow or hardware encoder can never deadlock the export. The writer's default throttling stays on:
  it is the only built-in limit — `MF_SINK_WRITER_DISABLE_THROTTLING` is never set.
- Effect: at most 3 raw frames inside the writer, whatever its undocumented queue does — the unknown
  of the table above becomes ≈ 3 × (canvas RGB32 + canvas NV12).

#### 2.3 Video size — an app setting

- A new app setting, **Video size** (`AppSettings.VideoMaxSide`): the longer side a **video
  export** (MP4, GIF) is clamped to — **1920** (Full HD), **2560** (QHD) or **4096** (4K, today's
  maximum) — in the ⚙ menu, remembered between sessions (RULES § App Settings). The PNG keeps 4096.
- `GridExport` caps the canvas `CanvasSizer.Compute` gives for an animated export to that side (then
  `Animation.EvenSize`), the cells scaling with it; the bitrate formula follows. The export summary
  (status line, tooltips of *Copy last*) already shows the size.
- Effect per frame at 16:9: 36 MiB at 4096 → 14 MiB at 2560 → **7.9 MiB at 1920**; every
  canvas-sized buffer shrinks alike (canvas, input buffers, converter, encoder frames, the
  compositor's cell-sized temporaries), and the export gets faster in the same proportion. X /
  Twitter re-encodes anything above 1920 × 1200 anyway.
- Default: **1920** — Open Question *Video size default*, answered 2026-10-13.

#### 2.4 Undo history — a budget for the parked images

- `GridHistory` keeps **50 steps, or fewer when the images they park** — the ones no longer in the
  grid — **exceed 512 MB** (`MaxParkedBytes`): the oldest steps are dropped first, through the
  existing `DisposeDropped`. An image weighs its bitmap (W × H × 4) plus, for a GIF or a PDF, the
  file it keeps in memory.
- `VideoFrames` gets a `Dispose` releasing its `MediaComposition` / `MediaClip` instead of waiting
  for a finalizer (guarded against a render in progress).
- RULES § Undo History and the glossary's *Undo history* entry say so.

#### 2.5 The GIF on the clipboard

- `MainForm.PutOnClipboard` adds the `"GIF"` byte format **only below 64 MB**; above it, the GIF
  goes as a **file-drop list only** — the file is already on disk, which is the one place the disk
  genuinely helps here. *Copy last GIF* follows the same rule.

#### 2.6 Exception-path leaks

The six one-shot leaks of § Secondary findings are fixed: allocations moved inside their `try`, the
frame disposed in `ScaleDown`'s failure path, `SeamField.Strip` and `GridExport.FirstFrame`
releasing on failure, `VideoFrames.Dispose`.

### 3. Measure — the Probe

Nothing above was measured on a real export; the probe makes the first run tell which holder
dominates, and stays useful afterwards:

- `GridExport.Result` gains **`PeakPrivateBytes`**, sampled every 30 frames
  (`Process.PrivateMemorySize64`), shown in the export summary and the *Copy last* tooltips
  (`peak 2.3 GB`).
- In **Debug builds only**, every 30 frames a line goes to
  `%TEMP%\ImageGridFusion\export-<timestamp>.log`, next to the exported file: frame index, canvas
  size, private bytes, managed heap, and the writer's statistics (received − encoded, encoded −
  processed, bytes queued, outstanding sink requests), plus one line after the readers open and one
  after the encoder is created. Reading it: received − encoded climbing → the writer was queueing
  (§ 2.2 is the fix); private bytes flat but high, with a jump when the readers open → the
  per-video readers dominate (§ 2.1, § 2.3); private bytes climbing with a flat gap → a leak
  outside what was audited.

### 4. Performance Leads

#### 4.1 In scope — the preview's quick wins

| # | Change | Where | Effect |
|---|---|---|---|
| 1 | **Fast render while playing**: a video frame is drawn with the fast settings (bilinear) while the grid runs; one HighQuality render of the cell when the grid pauses or stops, as the end of a gesture does today | `GridPreview.RedrawCell` (`fast: live` → `live || running`), `AnimationPlayer.PlaybackChanged` | UI cost per frame divided by 2–4; frames already arrive at 1–1.33 × the cell size, where bilinear is close to HQ; exports keep `fast: false` |
| 2 | **Deadline-based pacing** of each playback loop: a stopwatch deadline advanced by the interval, the wait being `max(1 ms, next − now)`, the deadline reset when more than one interval late (drop, never burst) | `AnimationPlayer.RunAsync` (the `Task.Delay(33)` at the end of the loop) | ≈ 30 fps instead of 13–17 fps per 1080p video today (decode + scale + redraw + 33 ms + timer rounding), an even rhythm |
| 3 | **Premultiplied bitmaps** for what is drawn every frame: the scaled-down frame and the preview cache in `Format32bppPArgb` | `AnimationPlayer.ScaleDown`, `GridPreview`'s cache (after checking `GridBorders.CutCorners` writes premultiplied alpha) | No per-pixel premultiplication at each `DrawCell` and each paint |
| 4 | **Crop edit view painted only when its cell is in the paint's clip** | `GridPreview.OnPaint` → `PaintCropEdit` | Stops a forced HQ render (with the whole edge extension) up to 60 times a second while any other cell plays |
| 5 | **Hoisted per-frame allocations**: the `ImageAttributes` built once per cell per export, the `g.Clip` getter (a new `Region` each call) replaced by `Save` / `Restore`, the edge extension's cell-sized buffers rented from `ArrayPool` or allocated once per cell for an export | `Compositor.DrawCell`, `EdgeExtension`, `BlurRenderer`, `GridBorders` | Up to ≈ 1 GB/s of native and LOH churn removed per extending cell at 1080p; fewer full GCs pausing the UI |
| 6 | **Blur over an extending background**: the inner `DrawCell` builds the extension at full cell size only to shrink it into the small blur bitmap — built at the small size instead | `Compositor.DrawCell` / `BlurRenderer` | Halves the most expensive per-cell work when both are on |

#### 4.2 Candidates for later — not in this workfile (Open Question *Scope*, answered 2026-10-13)

| Lead | What | Gain | Risk |
|---|---|---|---|
| **Hardware encoder** | `MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS` on the sink writer's attributes, with a retry without it when `Create` fails | Encoding several times faster, the encoder's frames in video memory | Fallback undocumented, vendor limits, output differing between machines; needs `MaxFramesInFlight` ≈ 8 |
| **NV12 input** | The app converts the canvas to NV12 (BT.709, nominal range set) and feeds the encoder directly | Input buffer 2.67 × smaller (36 → 14 MiB at 4096 × 2304), the writer's converter and its copy removed | Colour shifts if the matrix is wrong; the conversion needs SIMD or `Parallel.For` |
| **Display-sized decode** | `MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING` and a smaller `MF_MT_FRAME_SIZE` on the preview readers' output type (the docs list resizing among its conversions; "attempts to match", and the code already reads back the real size) | Per-frame bitmap 8.3 MB → ≈ 1.4 MB, `ScaleDown` a no-op, the largest cut in worker CPU for HD / 4K sources, smaller preview readers | Scaling quality to check; aperture and rotation to keep; the export must stay at native size |
| **Off-UI-thread cell rendering** and **one shared tick** | The worker produces the final pre-scaled PArgb cell bitmap; one deadline tick redraws every new cell in one `DrawIntoCache` and one `Invalidate` | The UI thread only blits | `BandColor`'s cache is not thread-safe, GDI+ objects are per thread, the sound sync moves with it |

### Why Not the Disk

The user's first idea was to spill to disk when memory runs short. It does not fit what the memory
is:

- Nothing in the export is a **set of data** that could live on disk: the frames are **transient**
  (one canvas, one buffer in flight), the big holders are **decoders** that must stay in memory to
  decode, and the writer's queue is bounded by § 2.2 without any copy. Writing raw frames to a temp
  file between the renderer and the encoder would only be a slower queue — 37 MiB × 30 per second of
  video.
- Where the disk helps, the file is **already there**: the GIF on the clipboard (§ 2.5), the last
  video, the previous copy.
- The one holder that could be spilled is the undo history's parked images (a temp file per parked
  bitmap, re-read on undo). § 2.4's budget is simpler and bounds the same memory; the spill variant
  was offered and declined (Open Question *Disk*, 2026-10-13).
- When memory still runs short after § 2, the export ends with the message of § 1 — the user closes
  other applications or lowers the video size — instead of a crash.

### Files Touched

- `UI/MainForm.cs` — the catch filters, the message, `BeginExport` / `EndExport`, `PutOnClipboard`,
  the ⚙ menu's *Video size*.
- `Program.cs` — the global handlers, `errors.log`.
- `UI/AnimationPlayer.cs` — readers released on hold and reopened on release, `ScaleDown`'s leak, the
  pacing, PArgb.
- `UI/GridPreview.cs` — fast render while playing, HQ on stop, the crop edit view's paint guard, the
  cache's format. *Another session is changing this file at the time of writing (crop work): the run
  rebases on its state.*
- `UI/GridHistory.cs` — the parked-images budget.
- `UI/AppSettings.cs` — `VideoMaxSide`.
- `Imaging/MediaFoundation.cs` — `SinkWriterStatistics`, `GetStatistics`.
- `Imaging/VideoEncoder.cs` — the back-pressure wait, `FramesInFlight` for the probe.
- `Imaging/VideoReader.cs`, `Imaging/VideoFrames.cs` — the leaks, `Dispose`.
- `Imaging/GridExport.cs` — the video cap, the probe, `PeakPrivateBytes`, `FirstFrame`'s leak.
- `Composition/CanvasSizer.cs` — the maximum side as a parameter.
- `Composition/Compositor.cs`, `EdgeExtension.cs`, `BlurRenderer.cs`, `SeamField.cs`,
  `GridBorders.cs` — hoisted allocations, the blur-over-extension size, `Strip`'s leak.
- `README.md`, `README.fr.md`, `RULES.md`, `GLOSSARY.md`, `GLOSSARY.fr.md` — § Documentation
  Impact.

### Documentation Impact

| Doc | Section | Change |
|---|---|---|
| README(.fr) | § Output / § Sortie | The *Video size* setting; the memory message; `peak` in the export summary |
| README(.fr) | § Animated content / § Contenu animé | The canvas sentence: a video export clamped to the video size |
| README(.fr) | § Canvas size / § Taille du canevas | The video cap next to the 4096 rule |
| README(.fr) | § Tray & startup, or § Tech | `errors.log` next to the exe |
| RULES.md | § Output Format | "longer side clamped to [1200, 4096] px — a video export to [1200, the video size setting]" |
| RULES.md | § Preview Playback › Transport | The hold closes the preview's readers and reopens them at the end |
| RULES.md | § Undo History | 50 steps, or fewer past 512 MB of parked images |
| GLOSSARY(.fr) | *Video size* (new), *Undo history* (amended), *Last video* (the `peak` in its tooltip) | |

---

## Test Impact

Not applicable as unit tests: the repository has **no test project** (CONTRIBUTING § Build: "check
a change by hand in the running app"). The run is checked by hand on the launched app:

| Behaviour to check | How |
|---|---|
| Four videos exported as MP4 at each video size; the export summary shows the size and the peak | Copy MP4 with 4 videos (1080p and 4K files), ⚙ *Video size* at 1920 / 2560 / 4096 |
| The Debug log shows the writer's gap staying ≤ 3 | Open `%TEMP%\ImageGridFusion\export-*.log` after a Debug export |
| The grid resumes where it stood after an export, sound back | Play, Copy MP4, check the timeline position and the sound after the export |
| A failed export (memory) ends with the status message, no dialog; last video intact | A Debug-only fault injected in `WriteFrame` during the run, or a 4K Square grid at 4096 on a low-commit machine |
| GIF Copy above 64 MB: file-drop only, paste into Explorer works | Export a long GIF, paste in a folder |
| Undo history drops old steps past the budget | Replace a 4K video many times, check the undo depth and the memory |
| Preview: ≈ 30 fps, no visible softness once paused; crop bars shown with another cell playing | Play, pause, watch |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~**Scope** (the scoping batch's "?" on the preview): this workfile delivers § 1–3 plus § 4.1's
      quick wins (recommended); or § 1–3 only, the preview untouched; or everything, § 4.2's larger
      leads included (hardware encoder, NV12, display-sized decode)?~~ → § 1–3 plus § 4.1's quick
      wins; § 4.2 stays a list of candidates for a later workfile (2026-10-13)
- [x] ~~**Video size default**: 1920 (recommended — X / Twitter re-encodes above it, 4.5 × less memory
      per frame than today); 4096 (no change by default, the setting there for who needs it); or no
      setting at all, 4096 for everyone?~~ → 1920 by default, 2560 and 4096 in the ⚙ menu (2026-10-13)
- [x] ~~**Undo history budget**: 512 MB of parked images, the oldest steps dropped first
      (recommended); or 50 steps whatever they weigh?~~ → 512 MB of parked images, the oldest steps
      dropped first (2026-10-13)
- [x] ~~**Disk**: no spill — the bounds (§ 2) plus the clean message (§ 1) (recommended); or spill the
      parked history images to temp files instead of dropping steps?~~ → No spill: the bounds of § 2
      plus the clean message of § 1 (2026-10-13)

---

## Design Iterations

### Iteration 1 — 2026-10-10

Initial design, after the scoping batch (Q&A 1–4) and a two-pass exploration. Scouts: the MP4
export pipeline, the source videos' memory, the process configuration, the GIF and clipboard paths,
the preview's rendering, the test and documentation surface. Deepenings: the sink writer's
back-pressure (Microsoft's docs read), the per-frame allocations (no leak at frame rate; six
exception-path leaks), the memory resident during an export (the preview's readers stay open; the
undo history parks every removed image), the preview's quick wins.

Decisions taken: a clean failure and a global handler (§ 1); the preview's readers released during
an export, a back-pressure gate on the writer's statistics, a *Video size* app setting, a budget for
the parked images, the GIF's clipboard bytes capped, the one-shot leaks fixed (§ 2); a probe in the
export result and a Debug log (§ 3); the disk spill not retained for frames (§ Why Not the Disk).
Left open: § Open Questions.

### Iteration 2 — 2026-10-13

The four open questions answered, every recommendation taken (Q&A 5–8): the scope is § 1–3 plus
§ 4.1's quick wins, § 4.2 staying candidates for a later workfile; the *Video size* setting
defaults to 1920; the undo history gets the 512 MB budget for its parked images; no disk spill —
the bounds and the clean message. The design sections only take those defaults; nothing else
changes.

### Iteration 3 — 2026-10-13 — ✅ Implemented

Go given for the code, the tests (not applicable) and the documentation. The run stays on `main`,
the standing decision for this app (no branch question asked). Scope frozen as § Design stands:
§ 1, § 2.1–2.6, § 3, § 4.1, § Documentation Impact.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Not applicable: no test project (CONTRIBUTING § Build) |
| README | | | README.md and README.fr.md, RULES.md, GLOSSARY.md and GLOSSARY.fr.md |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | At what moment of the generation does the app crash (which export, which RAM)? | Unknown — "cherche des pistes d'optimisation vidéo / visuel / performances" too | 2026-10-10 |
| 2 | May the fix touch how the loaded videos are held in memory for the preview, or stay in the generation? | "?" — left to the exploration (→ Open Question *Scope*) | 2026-10-10 |
| 3 | When memory stays insufficient once leaks and caches are fixed: disk, refuse, or degrade? | Spill to disk (→ § Why Not the Disk, Open Question *Disk*) | 2026-10-10 |
| 4 | Straightforward, or tricky / long? | Tricky / long — two passes | 2026-10-10 |
| 5 | Scope: § 1–3 + quick wins, § 1–3 only, or everything? | § 1–3 plus § 4.1's quick wins | 2026-10-13 |
| 6 | Video size default: 1920, 4096, or no setting? | 1920 by default | 2026-10-13 |
| 7 | Undo history: a 512 MB budget, or 50 steps whatever? | The 512 MB budget | 2026-10-13 |
| 8 | Disk: no spill, or spill the parked history images? | No spill | 2026-10-13 |

---

*Last updated: 2026-10-13*
