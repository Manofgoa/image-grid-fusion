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
- **Factor**: ×2 or ×4, chosen in the options.

---

## Effect and Options

- **Tab**: `Upscale`, placed **right after Cutout** in `ImageEffect` (the enum order is the toolbar
  order): the cut-out and the upscale are the two model-computed effects (the drawn bitmap is built
  upscale first, then the cut-out — § Drawn Bitmap). Its icon: 🆙 (`UiIcons.Upscale`) — 🔍 is the
  Zoom's.
- **State**: a record `UpscaleEffect` (`Factor` 2 or 4), `Default` = ×2, with `ToJson` / `FromJson` /
  `Describe` (RULES.md § Effect Presets) and a cache key (`Factor` and the model).
- **Options toolbar**: the factors as **option thumbnails** (`UI/UpscaleStrip`, 56 × 40, RULES.md
  § Options Toolbar): **×2** and **×4**, one pressed — the image as loaded in the corner of its
  enlargement dashed in blue —, then, set apart, the **Save…** action; then the effect's **Reset**.
  Acting on a factor turns the effect on; *Save…* does not.
- **Size limit**: a factor whose result would exceed `CanvasSizer.MaxLongSide` (8192) on the longer
  side is **refused and faded**, its tooltip saying why; when even ×2 would, the checkbox is disabled
  too (`UpscaleEffect.Fits`).
  What exceeds the PNG cap can never be drawn at its size.
- **Layers**: a layer gets it like any cell effect (RULES.md § Layers) — a text layer's checkbox
  disabled (it sharpens).
- **Effect presets**: the factor is saved when the effect is on (`LookPreset.Saved`).
- **The effect stays temporary**: the upscaled image is a cache of the `SourceImage`, never a file of
  its own, computed again in each session (Q&A 11).

### Save Upscaled Image

An **action** writing the upscaled image to a real file, for when one is wanted (Q&A 11) — the effect
itself unchanged, the cell untouched:

- **Two routes, one action** (Q&A 13): **Save…**, a thumbnail of the Upscale options after the ×2 / ×4
  ones, set apart as an action (RULES.md § Options Toolbar — as Crop's 100 %); and **Save upscaled
  image…** in the **cell's context menu** (`MainForm.ShowCellMenu`), 💾 (`UiIcons.SaveUpscaled`), shown
  for an image the Upscale applies to. Both enabled while the effect is on for the image; when its
  upscale is not ready yet, the action waits for it once the file is chosen, the grid locked as an
  export waits (`MainForm.SaveUpscaledAsync`).
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
- **Models**: RealESRGAN ×2 and ×4, **both in one download** (134 MB), the factor picking the model
  — never one model resized for the other. Licence BSD-3-Clause, credited with the other engines;
  input `float32 [1,3,h,w]` RGB in [0,1], dynamic height and width, output `[1,3,s·h,s·w]`:

  | Model | File | Size | Source |
  |---|---|---|---|
  | RealESRGAN ×2 (RRDBNet) | `real_esrgan_x2.onnx` | 67.1 MB | `SceneWorks/real-esrgan-onnx` @ `09f741bac80a246b407da3ee902bf5f3291b602f` |
  | RealESRGAN ×4 (RRDBNet) | `real_esrgan_x4.onnx` | 67.1 MB | same revision |

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
- **While it is computed**: the cell shows the image as it is, with an *⤢ Upscaling…* badge (the
  preview only), asked for **at every paint** like `PaintCutout`, so any route changing a look is
  covered by itself. **Exports wait** for the upscales they draw (`WaitForUpscalesAsync` beside
  `WaitForCutoutsAsync`, under `BeginExport`); one that fails is drawn as it is, the status line
  saying why.
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
- The frames are upscaled **in the background**, the badge showing the progress
  (*⤢ Upscaling… 12 / 40*); the GIF keeps playing as it is until every frame is ready, then switches.
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
| The badge while computing, an export waiting for it | Preview, Copy |
| Crop, zoom, focus, blur and fine angle unchanged when the effect is turned on | Preview |
| Cutout + Upscale on one image | Preview, PNG |
| An animated GIF: progress badge, then upscaled frames in the MP4 / GIF export | Preview, export |
| Video, PDF, text: checkbox disabled with its tooltip; a factor over 8192 disabled | Effects toolbar |
| Engine missing: the Component updates window opened on its row, the effect back to off; installed, updated, uninstalled from the window, the sessions released | Component updates window |
| *Save upscaled image…* from the options and from the cell's menu: a still's PNG at the upscaled size, cut out with the Cutout on; a GIF's animated GIF, its delays kept | File dialog, Explorer |
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

- **Found on the way**: the delivery only invalidated the preview, whose cache is drawn again only
  when the images change — the cell kept showing the image as loaded. It is drawn again at once, as a
  cut-out is (fixes code that betrayed the design).
- **Asked by the user** (Q&A 18): the preview may draw the images reduced; the highest resolution is
  what the final export needs. § Drawn Bitmap updated.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 6, 7 | 2026-11-03 | Effect, engine and component, drawn bitmap and GIF pages, tab, options, save action |
| Unit tests | 6 | 2026-11-03 | Not applicable: no test project in the repository — manual checks only |
| README | 6 | 2026-11-03 | § Upscale, the tabs, the components, the tech credits — English and French |
| Glossary | 6 | 2026-11-03 | Upscale, Upscale engine, the effects' list — English and French |
| RULES.md | 6 | 2026-11-03 | § The Upscale Exception |

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

---

*Last updated: 2026-11-04*
