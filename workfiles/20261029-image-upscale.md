# Image Upscale

> Working document — a cell effect enlarging a small image with Real-ESRGAN, ×2 or ×4, computed in
> the background on this machine, for still images and animated GIFs.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A small image drawn large in its cell is blurry: the canvas sizing (`CanvasSizer.Compute`) never
gives it more pixels than it has. The **Upscale** effect gives it more: a Real-ESRGAN model enlarges
the image ×2 or ×4, and the enlarged bitmap is what every reader draws.

Backlog row: `workfiles/TODO-FEATURES.md` — *AI upscale — a small image enlarged by a model
(Real-ESRGAN), downloaded on demand like the other engines*.

Components touched (from the scout pass):

| Area | Files |
|---|---|
| Effect state | `Composition/ImageLook.cs` (`ImageEffect`, property, `Kept*`, `IsActive`, `Activate`, `Deactivate`, diff, `With*`), a new `Composition/UpscaleEffect.cs` |
| Drawn bitmap | `Composition/SourceImage.cs` (`DrawnBitmap`, the cache, `MemoryBytes`, `Dispose`, `Sizing`) |
| Animated GIFs | `Composition/PageSource.cs`, `Imaging/GifFrames.cs`, `Composition/AnimationReader.cs`, `UI/AnimationPlayer.cs`, `Imaging/GridExport.cs` |
| Engine | a new `Imaging/UpscaleEngineFiles.cs` and `Imaging/UpscaleEngine.cs`, its component in `Components/ComponentRegistry.cs`, its release in `UI/MainForm.Components.cs` (`IComponentsHost.ReleaseAsync`), a new `UI/UpscaleService.cs` mirroring `UI/CutoutService.cs` |
| Preview / exports | `UI/GridPreview.cs` (request at paint, badge), `UI/MainForm.cs` (options, gate, offer, ⚙ menu, export wait) |
| Presets | `Composition/LookPreset.cs` |
| Icons | `UI/UiIcons.cs` |

---

## Agreed Scope

Settled with the user before exploration (Q&A 1–4):

- **A cell effect**, `Upscale`: a tab of the effects toolbar with its activation checkbox, per
  cell + image pair, not persisted, reset with the image like every effect (RULES.md § Effects ›
  Scope and State). Not an action replacing the image, not a file written next to it.
- **Contents**: still images and **animated GIFs**. Videos, PDFs, texts and thumbnails: checkbox
  disabled, its tooltip saying why (a PDF, a text and a thumbnail already render again at any
  resolution — `PageSource.Sharpens`; a video is out of scope).
- **Factor and model**: ×2 or ×4, **Quality** or **Fast**, chosen in the options; nothing is computed
  until the **Upscale** button or the checkbox asks for it (Q&A 20–22).

---

## Effect and Options

- **Tab**: `Upscale`, placed **right after Cutout** in `ImageEffect` (the enum order is the toolbar
  order): the cut-out and the upscale are the two model-computed effects (the drawn bitmap is built
  upscale first, then the cut-out — § Drawn Bitmap). Its icon: 🆙 (`UiIcons.Upscale`) — 🔍 is the
  Zoom's.
- **State**: a record `UpscaleEffect` — `Factor` 2 or 4 and `Model` (**Quality** or **Fast**), `Default` =
  ×2 Quality — with `ToJson` / `FromJson` / `Describe` (RULES.md § Effect Presets) and a cache key. On the
  look it is the **applied** setting: the one an enlargement was computed for (Q&A 22).
- **Options toolbar**: two **option thumbnail** strips (`UI/UpscaleStrip`, 56 × 40, RULES.md § Options
  Toolbar) — **Quality** / **Fast**, then **×2** / **×4** — then an **Upscale** button, then the effect's
  **Reset**. The strips show the setting **chosen**, the look's while the effect is on, its kept one
  otherwise. **Picking a thumbnail computes nothing and turns nothing on** (Q&A 20): it only chooses —
  the exception to *acting on any option activates the effect*, as the eyedropper's sample is. The
  **Upscale** button applies the choice: the **Upscale window** (below) for it, or at once when the image
  already holds an enlargement for that setting in this session.
- **The checkbox**: checked without an enlargement for the chosen setting, it **opens the Upscale
  window** (Q&A 22) — a Cancel leaves the effect off; with one, it turns on at once. Unchecked, the effect
  is off, the enlargement kept in memory for a later check.
- **Size limit**: a factor whose result would exceed `CanvasSizer.MaxLongSide` (8192) on the longer
  side is **refused and faded**; when even ×2 would, the checkbox is disabled too (`UpscaleEffect.Fits`).
  What exceeds the PNG cap can never be drawn at its size.
- **Why, said in the options row** (Q&A 26): a line after the Upscale button names each factor refused
  and the size it would make (`×2 would make 10000 × 5138 px, past the 8192 px a PNG holds`), or why the
  effect does not apply to the image; empty otherwise.
- **Layers**: a layer gets it like any cell effect (RULES.md § Layers) — a text layer's checkbox
  disabled (it sharpens).
- **Effect presets**: the factor and the model are saved when the effect is on (`LookPreset.Saved`).
- **The effect stays temporary**: the upscaled image is a cache of the `SourceImage`, never a file of
  its own, computed again in each session (Q&A 11, Q&A 23).

### Upscale Window

A modal, cancellable window computing an enlargement, so the grid is never touched meanwhile (Q&A 20):

- **Before**: what it will make — the **size** (`1024 × 1536 → 4096 × 6144`), the **PNG's weight**
  estimated (`≈ 45 MB`: the source file's bytes per pixel when it is a PNG, else about 2 bytes a pixel,
  times the pixels), the **duration** estimated (tiles × a time per tile for the model and factor —
  measured on this machine at the last run of the session, a constant before) — and the model and factor
  chosen. **Upscale** starts, **Cancel** closes.
- **During**: a progress bar, the share done and the time left (from the tiles done), **Cancel**
  stopping it at the next tile; the rest of the app blocked behind the window.
- **After**: the effect turned on with the setting computed — one step of the undo history — and the
  window closed. A failure says why there, the effect left as it was.
- A GIF: its frames one after the other, the share counting frames and tiles.

### Save Upscaled Image

An **action** writing the upscaled image to a real file, for when one is wanted (Q&A 11) — the effect
itself unchanged, the cell untouched:

- **One route** (Q&A 13, Q&A 21): **Save upscaled image…** in the **cell's context menu**
  (`MainForm.ShowCellMenu`), 💾 (`UiIcons.SaveUpscaled`), shown for an image the Upscale applies to,
  **enabled once its enlargement is computed** and the effect on — no thumbnail in the options any more.
- **What it holds** (Q&A 14): the **upscaled image alone**, whole — the image as loaded, ×2 or ×4, the
  cut-out when the Cutout is on (§ Drawn Bitmap: upscale, then the mask) — never the crop, the
  orientation, the colors or any other effect: a source file to reuse. The cell as drawn stays Cell
  copy's business.
- **A still** is saved as a **PNG** (its alpha kept); **an animated GIF as an animated GIF** of the
  upscaled frames (Q&A 15), each frame read from the upscale cache and written with its own delay
  (`Imaging/GifEncoder`), its trim ignored — the whole GIF, as the image as loaded.
- **The file dialog** opens in the image's folder, its name proposed as `<name>-x2.png` / `-x4.gif`;
  an image without a file (pasted) gets `upscaled-x4.png` in the user's Pictures folder. Out of the
  undo history.

### Crop to a New Image

An image too large to upscale may hold a small part worth enlarging: the Crop's **Keep** action makes the
kept part the cell's image, so the Upscale — and every effect — treats it as a whole image (Q&A 27–30):

- **An action of the Crop's options**: a **Keep** thumbnail set apart after the ratios, beside the **100 %** one
  (RULES.md § Options Toolbar — an action, never pressed), enabled while the Crop is on, on a **still** of a
  **cell** (a GIF, a video, a PDF, a text, a layer: disabled, its tooltip saying why).
- **The new image is the kept part of the image as loaded** (Q&A 27): the source's pixels, no orientation, colors,
  blur nor background drawn into them — `CropEffect.Pixels` of the bitmap, copied.
- **The look carries over as effects** (Q&A 28): every effect kept as it was — orientation, zoom and focus,
  colors, blur, background, animations — **but the Crop**, consumed, and the **Cutout**, its mask and points made
  for the former image, both back to their default state; the **Upscale** turned off, its setting kept, the new
  image holding no enlargement. The zoom and the focus are relative to the image as shown, which keeps its size
  and shape: the cell looks the same.
- **Named after its origin** (Q&A 29): an image without a file, like a pasted one (`SourceImage.FilePath` null — a
  favorite saves it into `favorites-from-pasted\`), keeping the file it was cut from (`SourceImage.CroppedFrom`):
  the preview names it `<name> (crop)`, *Save upscaled image…* proposes `<name>-crop-x4.png` in that file's folder.
- **One step of the undo history**: the cell's image replaced, its look kept (`GridPreview.ReplaceImage`); Ctrl+Z
  gives back the whole image with its crop. The former image goes through `ReleaseImage`, kept alive by the history.

---

## Engine

- **A component** (RULES.md § Downloadable Components): **one** entry of `ComponentRegistry` — one
  upstream, the Hugging Face repository below, its two files — in the **Engines** group, *Upscale
  engine — Real-ESRGAN*, BSD-3-Clause, its purpose *Enlarges a small image ×2 or ×4, for the Upscale
  effect*. Its files are pinned in `UpscaleEngineFiles` (`EngineFile`s, as `CutoutEngineFiles.PickFiles`),
  installed from the **Component updates window** into **`upscale-engine\` next to the exe**
  (`UpscaleEngineFiles.FolderName`), each checked against its SHA-256. It runs on the OCR engine's ONNX
  Runtime, a row the window installs with it when missing.
- **Turned on without it**: the window opens on its row (`MainForm.ShowComponents` with a
  `ComponentNeed`), highlighted, its balloon saying the Upscale effect needs it; the effect **goes back
  to off** at once. No ⚙ menu entry, no dialog of its own: nothing installs anywhere else.
- **Released before an update or an uninstall**: `IComponentsHost.ReleaseAsync` releases
  `UpscaleService`'s sessions once the upscale running is done (`ReleaseNowAsync`, as `CutoutService`),
  so the files can be swapped.
- **Two components** (Q&A 20), one per upstream, each installed on its own: **Quality** — RealESRGAN
  ×2 and ×4, both in one download (134 MB), the factor picking the model; **Fast** — Real-ESRGAN's
  compact model, `realesr-general-x4v3` (SRVGGNetCompact, 4.9 MB, many times faster on the CPU, a little
  less fine), ×4 only: its ×2 is its ×4 reduced by half (high-quality bicubic). The window opens on the
  row of the model chosen when it is missing. Licence BSD-3-Clause, credited with the other engines;
  input `float32 [1,3,h,w]` RGB in [0,1], dynamic height and width, output `[1,3,s·h,s·w]`:

  | Model | File | Size | Source |
  |---|---|---|---|
  | RealESRGAN ×2 (RRDBNet) | `real_esrgan_x2.onnx` | 67.1 MB | `SceneWorks/real-esrgan-onnx` @ `09f741bac80a246b407da3ee902bf5f3291b602f` |
  | RealESRGAN ×4 (RRDBNet) | `real_esrgan_x4.onnx` | 67.1 MB | same revision |
  | realesr-general-x4v3 (compact) | `realesr-general-x4v3.onnx` | 4.9 MB | `jonathanst29/tinier-upscale-models` @ `899dc1e4b22bbf1955c2f1739c085edc080cb366` |

  The hashes are taken from the Hugging Face API by the scout and checked again at implementation;
  the ×2 file is checked to be a true ×2 on a test image.
- **Sessions**: `UpscaleEngine` copies `CutoutEngine.Load` — no memory arena, no memory pattern — on
  **half the cores** (2 to 8 intra-op threads), loaded lazily per factor, released after 5 minutes
  unused (`UpscaleService`'s idle timer).
- **Tiling**: the image is run in tiles of 256 px, 16 px read around each (its seams hidden), each
  padded to a multiple of 4 by repeating its last row and column (the ×2 model folds its input in
  two); the result is exactly `factor × w` by `factor × h`, so the effects' fractions stay exact.
- **Alpha**: the model reads RGB only. The image is fed over its own colors (not over white), and
  its alpha channel is enlarged separately (bilinear) and put back.

---

## Drawn Bitmap

- The upscaled bitmap is computed **off the UI thread** by `UI/UpscaleService` — one model at a time
  behind a gate, a request whose look changed before its turn dropped, failures reported through the
  status line — and kept on the `SourceImage`, counted in its `MemoryBytes`, released on `Dispose`.
- **Every reader draws it** through `SourceImage.DrawnBitmap` (RULES.md § The Cutout Exception lists
  them): it now builds `Bitmap` → upscale → cut-out (below), each stage cached on its own key. The
  size check of `DrawnBitmap` (`cut.Bitmap.Size == Bitmap.Size`) becomes a check on the stage's
  expected size.
- **Fractions stay fractions**: the crop, the focus, the zoom, the blur's rectangle and the cutout's
  points are fractions of the image as loaded; `ImageLook.Shown` reads the drawn size, so they hold
  as long as the ratio is exact. Every pixel-space reader of `image.Bitmap.Size` that draws from
  `DrawnBitmap` is checked at implementation (the scout named `GridPreview.cs` around 500, 1951, 2277,
  2375, 3535, and `SourceImage.Sizing`, which reads `Bitmap.Size`).
- **Canvas sizing — the upscaled image widens the native size**: its drawn bitmap counts as a larger
  image (`Sharpening` stays 1), so `CanvasSizer.Compute` grows the PNG's canvas until it is not
  downscaled, within [1200, cap] — that is where the gain shows. `SourceImage.Sizing`, which reads
  `Bitmap.Size` today, reads the drawn size as `Frame.Sizing` does, so the preview's readouts and
  the export agree. A video export stays clamped to the Video size as ever.
- **Cutout and Upscale together — upscale, then the mask**: the model enlarges the **whole image**
  (never the cut-out), then the cutout's mask — computed once on the image as loaded, unchanged —
  is enlarged and applied to it with its treatments (`CutoutMask.Apply`, its strengths shares of the
  longer side, so resolution-independent). `DrawnBitmap` builds `Bitmap` → upscale → cut, the
  cut-out of an upscaled image cached on the upscale's key and the cutout's.
- **Computed in the Upscale window only** (Q&A 20): never asked for at paint, never in the background.
  An image whose look has the Upscale on without its enlargement — a preset restored, an undo after the
  session's cache let it go — is drawn as loaded with a *⤢ Not upscaled* badge (preview only).
- **No grid Copy or Save without it** (Q&A 21): a Copy / Save of the grid (PNG, MP4, GIF, JPEG) drawing
  such an image opens the Upscale window for it first; a Cancel cancels the Copy / Save.
- **Once ready**, the cell is drawn again at once (its share of the preview's cache), as a cut-out is.
- **The preview draws a reduced copy** (Q&A 18): an enlarged image larger than its cell is drawn in
  the preview from a copy scaled down to about its cell, never from the full enlargement; the exports,
  Cell copy and *Save upscaled image…* read the full enlargement, the highest resolution usable.
- **Undo history**: nothing special — the effect is on the look; the upscaled bitmap is a cache of
  the `SourceImage`, weighed by `MemoryBytes` in the history's budget.

---

## Animated GIFs

- A GIF's frames are decoded **on demand**, one at a time (`GifFrames.Render`, `StepReader`), and the
  export reads them through `PageSource.Render` / `OpenAnimation`, **not** through `DrawnBitmap`. So
  the upscale of a GIF is an **`UpscaledPages : PageSource`** wrapping its pages: `Render` and
  `OpenAnimation` give the upscaled frame, which covers the preview, the Frames slider and every
  export at once.
- The frames are upscaled in the **Upscale window**, frame after frame; the GIF switches to them once
  every one is ready.
- **The upscaled frames are kept on disk**, never all in memory: each written as it is computed
  into a cache folder, **`%TEMP%\ImageGridFusion\upscale-cache\`** (kebab-case, its name in one
  constant `UpscaleCache.FolderName` — shared RULES § Folder Names), one file per frame keyed by the
  GIF's file, its stamp, the frame and the factor; `UpscaledPages.Render` reads a frame back on
  demand. The folder is emptied at start-up by the normal instance only — a `--new-instance` one
  deletes nothing of its own accord (RULES.md § Single Instance), as `url-downloads\`.
- The preview's player shrinks each frame to the cell (`AnimationPlayer.ScaleDown`): expected — the
  preview never draws more than the cell; the gain shows in the exports and in a large cell.

---

## Test Impact

The repository has **no test project** (`CONTRIBUTING.md`: *check a change by hand in the running
app*). Nothing is created or updated; the checks are made by hand on the launched app:

| Behaviour to check by hand | Where |
|---|---|
| A small still, ×2 then ×4: sharper in a large cell and in the PNG export; the PNG's native size grows (or not, per Question 1) | Preview, Copy / Save |
| The Upscale window: size, weight and duration before; progress, time left and Cancel during; the app blocked meanwhile | Upscale window |
| Picking Quality / Fast or ×2 / ×4 computes nothing; Upscale or the checkbox opens the window; a setting already computed applies at once | Options toolbar |
| Fast ×2 and ×4: much quicker than Quality, the sizes exact | Upscale window |
| A grid Copy / Save with an Upscale not computed (a preset restored): the window first, Cancel cancels | Copy / Save |
| Crop, zoom, focus, blur and fine angle unchanged when the effect is turned on | Preview |
| Cutout + Upscale on one image | Preview, PNG |
| An animated GIF: progress badge, then upscaled frames in the MP4 / GIF export | Preview, export |
| Video, PDF, text: checkbox disabled with its tooltip; a factor over 8192 disabled | Effects toolbar |
| Engine missing: the Component updates window opened on its row, the effect back to off; installed, updated, uninstalled from the window, the sessions released | Component updates window |
| *Save upscaled image…* from the cell's menu, enabled once computed: a still's PNG at the upscaled size, cut out with the Cutout on; a GIF's animated GIF, its delays kept | File dialog, Explorer |
| An effect preset with Upscale restored | File explorer |

---

## Open Questions

- [x] ~~**Canvas sizing** — should an upscaled image widen the PNG's native size, or only draw the
  cell from more pixels?~~ → It widens the native size (`Sharpening` 1)
- [x] ~~**Models downloaded** — ×2 and ×4, with the compact x4v3, or the compact one alone?~~ →
  RealESRGAN ×2 and ×4 (134 MB), no compact model
- [x] ~~**Cutout and Upscale together** — upscale then the mask, or upscale the cut-out?~~ → Upscale
  the whole image, then apply the cutout's mask enlarged
- [x] ~~**A GIF's upscaled frames** — in memory, or in a temp cache?~~ → A cache folder in `%TEMP%`,
  read back per frame
- [x] ~~**Where *Save upscaled image…* lives** — the Upscale options, the cell's menu, or both?~~ →
  Both
- [x] ~~**What it saves** — the upscaled image alone, or the cell as drawn with its effects?~~ → The
  upscaled image alone, whole, with the cut-out when the Cutout is on
- [x] ~~**A GIF** — an animated GIF of the upscaled frames, or the frame shown as a PNG?~~ → An
  animated GIF

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Initial design from the scoping answers (a cell effect, stills and animated GIFs, ×2 / ×4) and a
single scout pass over the Cutout pipeline, the engine download and the GIF / canvas paths. Proposed
by the agent, open to review: the tab after Cutout, the factor thumbnails, the 8192 limit, the
`UpscaledPages` wrapper for GIFs, the badge and the export wait mirroring Cutout. Four questions left
open.

### Iteration 2 — 2026-10-29

The four open questions answered (Q&A 5–8): the upscaled image widens the PNG's native size; the
download holds RealESRGAN ×2 and ×4 only; with the Cutout on, the whole image is upscaled and the
mask applied after, enlarged; a GIF's upscaled frames go to `%TEMP%\ImageGridFusion\upscale-cache\`
and are read back per frame. § Engine, § Drawn Bitmap and § Animated GIFs updated. No question left.

### Iteration 3 — 2026-11-03

The code moved meanwhile: every downloadable component is now installed from the **Component updates
window** (`workfiles/20261102-component-update-verification.md`, RULES.md § Downloadable Components).
§ Engine follows it: the upscale engine is one component of `ComponentRegistry`, installed from the
window, opened on its row when the effect is turned on without it (the effect back to off); no offer
dialog, no ⚙ menu entry; its sessions released through `IComponentsHost.ReleaseAsync`. Nothing else in
the design depends on what changed. The go question (Q&A 10) and the backlog question (Q&A 9) were
interrupted before an answer.

### Iteration 4 — 2026-11-03

The user asked whether the upscale makes a new image or a temporary one. Kept a **temporary effect**,
plus an **action saving the upscaled image to a file** (Q&A 11) — § Save Upscaled Image. Three
questions opened about it.

### Iteration 5 — 2026-11-03

The three questions on the save action answered (Q&A 13–15): a button in the Upscale options and an
entry of the cell's menu; the upscaled image alone, whole, cut out when the Cutout is on; a GIF saved
as an animated GIF. Proposed by the agent: the PNG for a still, the file dialog in the image's folder
with `-x2` / `-x4` in the name, the action waiting for an upscale not ready. No question left.

### Iteration 6 — 2026-11-03 — ✅ Implemented

Go given (Q&A 17): code, tests and documentation, in a worktree on `feature/image-upscale`. The backlog
row *AI upscale* marked with this workfile (Q&A 16).

### Iteration 7 — 2026-11-03 — 🧭 Implementation choices

What the run decided on its own, the domain sections updated to match. No project rule broken.

- **Icons**: 🆙 for the tab (🔍 is the Zoom's); 💾 for *Save upscaled image…* in the cell's menu, a
  field of its own (`UiIcons.SaveUpscaled`). In the options, *Save…* is a thumbnail drawn like the
  others (an image, and a blue arrow into a tray).
- **Threads**: half the cores, 2 to 8, where the cutout engine takes 2: an upscale takes seconds a tile.
- **Tiles**: 256 px, a 16 px margin, padded to a multiple of 4 (§ Engine).
- **A cut-out of the image as loaded** stays drawn while the enlarged image's is computed
  (`SourceImage.DrawnBitmap`), so the cell does not flash uncut; `CutoutPending` stays true meanwhile.
- **Badge**: the *Upscaling… N %* badge takes the place of the *Cutting out…* one while both wait; its
  percentage counts the tiles, a GIF's the frames.
- **A GIF's pages**: `SourceImage.DrawnPages` — read by the player, the page loader and the exports —
  and `AnimationPlayer.Reopen`, called by the preview when they change (`GridPreview.FollowDrawnPages`).
  A new virtual, `PageSource.UpscaledFrom`, says a GIF's frames are enlarged and at which size.
- **Cache keys**: a GIF's file — its full path, size and last write — hashed; a GIF without a file gets
  a tag of its own. Each frame written through a partial file.
- **The waits are not cancellable**, as the Cutout's: a GIF upscaled ×4 before an export may hold the
  grid for minutes.
- **A GIF saved** by *Save upscaled image…* is written by the app's GIF encoder, which writes opaque
  frames: a GIF's transparency is not kept there (a still's PNG keeps its alpha).
- **The cell's menu entry** is shown for every image the Upscale applies to, enabled while it is on.
- **Not run on the real models**: the agent downloaded nothing; the pinned hashes were checked against
  the Hugging Face API. The first run of the models is the user's test, once the engine is installed
  from the Component updates window.

### Iteration 8 — 2026-11-04 — ⚙️ Post-implementation — Freeze after a ×4 upscale

The user's first test: a 1024 × 1536 image upscaled ×4 reached 96 % (23 tiles of 24), then the app
froze and Windows closed it (`AppHangB1`: the UI thread stopped answering, no exception). Measured
apart: a tile takes ~12 s and ~800 MB; composing the preview with the 4096 × 6144 enlargement takes
~0.1 s, the 8192 px PNG ~0.5 s, a paint of the preview after the delivery under 5 ms — the cause lies
elsewhere, looked for with the frozen UI thread's stack.

- **Not reproduced**: the real main window, hosted by a probe with a 1024 × 1536 image turned to ×4,
  went through the 24 tiles (~6 min) and the delivery with no freeze, the version the user tested as
  well as the fixed one — its UI answering throughout, a paint after the delivery 2 to 6 ms, the
  process ~400 MB; the machine has room (31 GB, 10 GB free). The freeze depends on the user's session.
- **Fixed all the same**: the cell drawn again at the delivery, and the preview drawing an enlargement
  past 2048 px from a reduced copy (`SourceImage.ShownBitmap`, 32 bpp premultiplied, made once) — a
  redraw of the cell went through the 25 Mpx bitmap, ~0.1 s each, every gesture.
- The next test runs under a watcher reading the UI thread's stack if the window stops answering.

- **Found on the way**: the delivery only invalidated the preview, whose cache is drawn again only
  when the images change — the cell kept showing the image as loaded. It is drawn again at once, as a
  cut-out is (fixes code that betrayed the design).
- **Asked by the user** (Q&A 18): the preview may draw the images reduced; the highest resolution is
  what the final export needs. § Drawn Bitmap updated.

### Iteration 9 — 2026-11-04 — ⚙️ Post-implementation — The Upscale window, a fast model

Asked by the user while retesting ×4: a confirmation saying the size and the weight of the result before
an enlargement starts, so a ×4 is not launched instead of a ×2; a faster model, even a little less fine;
the options choosing only, an **Upscale** button computing, in a modal, cancellable window blocking the
app meanwhile; *Save* in the cell's right-click menu only, enabled once the image is upscaled.

- **Options** (Q&A 20): **Quality** (RRDBNet ×2 / ×4) and **Fast** (`realesr-general-x4v3`, its ×2 its ×4
  halved) beside ×2 / ×4; picking one computes nothing — the exception to *acting on any option activates
  the effect*. The **Upscale** button applies the choice; the look holds the setting applied.
- **The Upscale window**: size, estimated PNG weight and duration before; progress, time left and Cancel
  during. Computed there only — never at paint, no background service queue, no *Upscaling…* badge.
- **The checkbox** (Q&A 22) opens the window when the chosen setting has no enlargement yet.
- **No grid Copy / Save without the enlargement** (Q&A 21): the window opens first, Cancel cancels it.
- **Save upscaled image…** (Q&A 21): the cell's menu only, enabled once computed; the options' *Save…*
  thumbnail goes.
- **Per session** (Q&A 23), as before.

### Iteration 10 — 2026-11-04 — 🧭 Implementation choices

The Upscale window adjustment delivered (Q&A 24: code and documentation). What the run decided on its own:

- **The duration estimate**: seconds per megapixel of the image as loaded — every frame of a GIF's — for each model
  and factor: measured at the last run of the session, constants before (Quality ×4 230 s, ×2 60 s, Fast 15 s, from
  the ~15 s a 256 px tile took here). Measured on the probe: 1 min 34 s estimated, ~1 min 22 s taken (Quality ×2,
  1024 × 1536).
- **The weight estimate**: the source file's bytes per pixel when it is of the format written (PNG, GIF), else 2
  bytes a pixel — shown `≈`, *an estimate*.
- **Two enlargements kept per still** (`SourceImage.KeptUpscales`), the oldest disposed: going back to the previous
  setting is instant, without holding every one (100 MB for a 4096 × 6144).
- **The setting chosen** lives in the toolbar, reset to the image's applied — or kept — setting when another image is
  selected or another setting applied (`MainForm.UpscaleChosen`). **Upscale** is enabled unless the setting chosen is
  the one applied.
- **A cancelled Copy or Save of the grid** says so in the status line.
- **The Fast model is not run here**: its hash and its contract (RGB in [0, 1], ×4, dynamic size) were checked on the
  Hugging Face API, the file not downloaded by the agent. Its first run is the user's, once installed.
- **No project rule broken.**

### Iteration 11 — 2026-11-04 — ⚙️ Post-implementation — The Upscale button's spacing

The user's look at the delivery: the **Upscale** button cramped — its icon against its border and its text, the
button against the ×4 thumbnail. It gets an inner padding, a gap after its icon (drawn into the icon's image: a
WinForms button lays its image and its text touching) and a margin from the thumbnails.

### Iteration 12 — 2026-11-04 — ⚙️ Post-implementation — Why an upscale is not possible, said

The user's test on a 5000 × 2569 JPEG: the Upscale tab greyed, nothing to click, the reason in the checkbox's tooltip
only — ×2 would make 10000 × 5138, past the PNG's 8192 px. Asked (Q&A 25, 26): explain why an upscale is not possible,
keep possible the ones that are; the reason as a visible text in the options row.

- The rule stays: a factor whose result passes 8192 px is refused, the effect unavailable only when every factor is.
- **The options row says why**, in a line after the Upscale button: each factor refused with the size it would make,
  or why the effect does not apply to the image (a video, a PDF, a text, a thumbnail).
- **Delivered**: `MainForm.UpscaleReason`, a label wrapping at 320 px after the button; the smallest factor refused named,
  the larger ones refused with it (`Too large to upscale: ×2 would make 10000 × 5138 px, past the 8192 px a PNG holds`;
  `×4 would make 8192 × 12288 px, …` for a 2048 × 3072). Checked on a render of the options row on both images.

### Iteration 13 — 2026-11-04 — ⚙️ Post-implementation — Crop to a new image

The user, cropping the 5000 × 2569 JPEG: the kept part is small enough to upscale, yet the image is not. Proposed by
the user: a Crop option making the cropped image a new image of the cell, as a pasted one, rather than a crop-aware
upscale. Settled (Q&A 27–30): the kept part of the source as loaded; every effect kept as an effect, the Crop and
the Cutout reset, the Upscale off; named after its file; in this workfile. § Crop to a New Image.

### Iteration 14 — 2026-11-04 — 🧭 Implementation choices

Crop to a new image delivered (Q&A 31: code and documentation). Decided by the run, no project rule broken:

- **Keep** after **100 %** in the Crop strip, an action like it (`CropChoice.Keep`); refused — faded, its tooltip saying
  why — without the Crop on, on a layer, on a content with pages (`MainForm.CropKeepRefusal`), and while exporting.
- The kept part copied with `CompositingMode.SourceCopy`, its alpha kept; the status line gives its size and Ctrl+Z.
- The Upscale **turned off**, its setting kept for the next Upscale; a Keep of an image already cropped keeps the first
  file as its origin (`CroppedFrom = FilePath ?? CroppedFrom`).
- The preview names it `<stem> (crop)`, without the extension; the Upscale window too.
- Checked on a probe with the user's 5000 × 2569 JPEG, the Crop on and a quarter turn: a 4000 × 2055 image, its file
  none, its origin kept, the turn kept as an effect, the image as shown unchanged (2055 × 4000); ×2 then possible, ×4
  refused with its reason; Ctrl+Z gave the 5000 × 2569 image back with its crop.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 6, 7, 8, 10 | 2026-11-04 | Effect, engine and component, drawn bitmap and GIF pages, tab, options, save action; the preview's reduced copy, the redraw at delivery; the Upscale window, the Fast model |
| Unit tests | 6 | 2026-11-03 | Not applicable: no test project in the repository — manual checks only |
| README | 6, 10, 12, 14 | 2026-11-04 | § Upscale, the tabs, the components, the tech credits — English and French |
| Glossary | 6, 10, 14 | 2026-11-04 | Upscale, Upscale window, Upscale engine, the effects' list — English and French |
| RULES.md | 6, 8, 10 | 2026-11-04 | § The Upscale Exception — its reduced preview copy, its window; § Options Toolbar's exception |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In what form does the upscale arrive in the app? | A cell effect | 2026-10-29 |
| 2 | Which contents can be upscaled? | Still images and animated GIFs | 2026-10-29 |
| 3 | Which enlargement factor? | ×2 or ×4, chosen in the options | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-29 |
| 5 | Canvas sizing: widen the native size, or only sharpen the cell? | Widen the native size | 2026-10-29 |
| 6 | Which models are downloaded? | RealESRGAN ×2 and ×4 (134 MB) | 2026-10-29 |
| 7 | Cutout + Upscale: upscale then mask, or upscale the cut-out? | Upscale, then the mask | 2026-10-29 |
| 8 | A GIF's upscaled frames: in memory or in a temp cache? | A cache in `%TEMP%` | 2026-10-29 |
| 9 | Mark the backlog row *AI upscale* with this workfile? | Interrupted — not answered | 2026-10-29 |
| 10 | Go for implementation — scope and where? | Interrupted — not answered | 2026-10-29 |
| 11 | Which form: a temporary effect, with a persistent cache, with a *Save* action, or a new image? | The temporary effect plus a *Save upscaled image…* action | 2026-11-03 |
| 12 | Update the workfile for the Component updates window? | Yes | 2026-11-03 |
| 13 | Where does *Save upscaled image…* live? | Both: the Upscale options and the cell's menu | 2026-11-03 |
| 14 | What does it save? | The upscaled image alone | 2026-11-03 |
| 15 | A GIF: animated GIF or the frame shown? | An animated GIF | 2026-11-03 |
| 16 | Mark the backlog row *AI upscale* with this workfile? | Yes | 2026-11-03 |
| 17 | Go for implementation — scope and where? | Code, tests and documentation — in a worktree | 2026-11-03 |
| 18 | (User remark on the freeze) How must the preview draw an enlarged image? | Reduced if need be; the highest resolution is for the final export | 2026-11-04 |
| 19 | Download dotnet-stack into the scratchpad to read the frozen stack? | Yes | 2026-11-04 |
| 20 | Which models in the options? | Quality (RRDBNet) and Fast (realesr-general-x4v3) | 2026-11-04 |
| 21 | A grid Copy / Save with the Upscale on and not computed? | No Copy / Save until computed; *Save…* in the options removed, the cell's menu instead | 2026-11-04 |
| 22 | The tab's checkbox without an enlargement? | Opens the Upscale window | 2026-11-04 |
| 23 | Keep the enlargement between sessions? | No, per session | 2026-11-04 |
| 24 | Go for the Upscale window adjustment? | Code and documentation | 2026-11-04 |
| 25 | An image whose enlargement would pass 8192 px: enlarge then cap, cap ×2 only, or refuse? | Explain why some upscales are not possible; make possible those that are | 2026-11-04 |
| 26 | Show the reason as a visible text in the options row? | Yes | 2026-11-04 |
| 27 | Crop to a new image: what does it hold? | The source's kept part only; the effects stay effects, never drawn into the bitmap | 2026-11-04 |
| 28 | The cell's other effects afterwards? | Kept, but the Crop and the Cutout | 2026-11-04 |
| 29 | The link to the original file? | Kept for the name | 2026-11-04 |
| 30 | Where is it designed and implemented? | Here, in this workfile | 2026-11-04 |
| 31 | Go for Crop to a new image? | Code and documentation | 2026-11-04 |

---

*Last updated: 2026-11-04*
