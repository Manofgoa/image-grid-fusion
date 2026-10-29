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
| Engine | a new `Imaging/UpscaleEngineFiles.cs` and `Imaging/UpscaleEngine.cs` on `Explorer/DirectEngineDownload.cs`, a new `UI/UpscaleService.cs` mirroring `UI/CutoutService.cs` |
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
  order): the cut-out and the upscale are the two model-computed effects, and the drawn bitmap is
  built in that order (§ Drawn Bitmap). Its icon: an emoji from `UiIcons` (RULES.md § Icons) — 🔍
  unless a closer one says "enlarge".
- **State**: a record `UpscaleEffect` (`Factor` 2 or 4), `Default` = ×2, with `ToJson` / `FromJson` /
  `Describe` (RULES.md § Effect Presets) and a cache key (`Factor` and the model).
- **Options toolbar**: the factors as **option thumbnails** (`ThumbnailStrip`, 56 × 40, RULES.md
  § Options Toolbar): **×2** and **×4**, one pressed; then the effect's **Reset**. Acting on a
  thumbnail turns the effect on.
- **Size limit**: a factor whose result would exceed `CanvasSizer.MaxLongSide` (8192) on the longer
  side is **disabled**, its tooltip saying why; when even ×2 would, the checkbox is disabled too.
  What exceeds the PNG cap can never be drawn at its size.
- **Layers**: a layer gets it like any cell effect (RULES.md § Layers) — a text layer's checkbox
  disabled (it sharpens).
- **Effect presets**: the factor is saved when the effect is on (`LookPreset.Saved`).

---

## Engine

- **Never shipped** (as every engine): downloaded on demand into **`upscale-engine\` next to the
  exe** (`UpscaleEngineFiles.FolderName`), from pinned URLs, each file checked against its SHA-256
  through `DirectEngineDownload`; it runs on the OCR engine's ONNX Runtime, fetched with it when
  missing.
- **Offered** when the effect is turned on without it — a **No** leaves it off — and from the ⚙ menu
  while missing (*Download the upscale engine…*), as `CutoutEngineReady` / `OfferCutoutEngineDownload`
  do.
- **Models** (scout candidates, licence BSD-3-Clause, input `float32 [1,3,h,w]` RGB in [0,1],
  dynamic height and width, output `[1,3,s·h,s·w]`):

  | Model | File | Size | Source |
  |---|---|---|---|
  | RealESRGAN ×2 (RRDBNet) | `real_esrgan_x2.onnx` | 67.1 MB | `SceneWorks/real-esrgan-onnx` @ `09f741bac80a246b407da3ee902bf5f3291b602f` |
  | RealESRGAN ×4 (RRDBNet) | `real_esrgan_x4.onnx` | 67.1 MB | same revision |
  | realesr-general-x4v3 (compact, ×4) | `realesr-general-x4v3.onnx` | 4.9 MB | `jonathanst29/tinier-upscale-models` @ `899dc1e4b22bbf1955c2f1739c085edc080cb366` |

  Which ones ship in the download: Open Question 2. The hashes are taken from the Hugging Face API
  by the scout and checked again at implementation.
- **Sessions**: `UpscaleEngine` copies `CutoutEngine.Load` — no memory arena, no memory pattern, two
  intra-op threads — loaded lazily, released after 5 minutes unused (`UpscaleService`'s idle timer).
- **Tiling**: the image is run in tiles (about 256 px with a 16 px overlap, the agent's choice at
  implementation), so a large image stays within memory; the result is exactly `factor × w` by
  `factor × h`, so the effects' fractions stay exact.
- **Alpha**: the model reads RGB only. The image is fed over its own colors (not over white), and
  its alpha channel is enlarged separately (bilinear) and put back.

---

## Drawn Bitmap

- The upscaled bitmap is computed **off the UI thread** by `UI/UpscaleService` — one model at a time
  behind a gate, a request whose look changed before its turn dropped, failures reported through the
  status line — and kept on the `SourceImage`, counted in its `MemoryBytes`, released on `Dispose`.
- **Every reader draws it** through `SourceImage.DrawnBitmap` (RULES.md § The Cutout Exception lists
  them): it now builds `Bitmap` → cut-out → upscale, each stage cached on its own key. The size
  check of `DrawnBitmap` (`cut.Bitmap.Size == Bitmap.Size`) becomes a check on the stage's expected
  size.
- **Fractions stay fractions**: the crop, the focus, the zoom, the blur's rectangle and the cutout's
  points are fractions of the image as loaded; `ImageLook.Shown` reads the drawn size, so they hold
  as long as the ratio is exact. Every pixel-space reader of `image.Bitmap.Size` that draws from
  `DrawnBitmap` is checked at implementation (the scout named `GridPreview.cs` around 500, 1951, 2277,
  2375, 3535, and `SourceImage.Sizing`, which reads `Bitmap.Size`).
- **Canvas sizing**: Open Question 1.
- **Cutout and Upscale together**: Open Question 3.
- **While it is computed**: the cell shows the image as it is, with an *⤢ Upscaling…* badge (the
  preview only), asked for **at every paint** like `PaintCutout`, so any route changing a look is
  covered by itself. **Exports wait** for the upscales they draw (`WaitForUpscalesAsync` beside
  `WaitForCutoutsAsync`, under `BeginExport`); one that fails is drawn as it is, the status line
  saying why.
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
- **Where the upscaled frames are kept**: Open Question 4.
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
| Engine missing: the offer, a **No** leaving the effect off, the ⚙ menu entry | ⚙ menu |
| An effect preset with Upscale restored | File explorer |

---

## Open Questions

- [ ] **Canvas sizing** — should an upscaled image **widen the PNG's native size** (its drawn bitmap
  counts as a larger image: `Sharpening` 1), or keep the canvas as it was and only draw the cell
  from more pixels (`Sharpening` = the factor, as a PDF page)?
- [ ] **Models downloaded** — RealESRGAN ×2 and ×4 (134 MB together)? With the compact x4v3 (4.9 MB)
  as a faster ×4 choice? Or the compact one alone (×4 only, then shrunk for ×2)?
- [ ] **Cutout and Upscale together** — upscale the original image and apply the cutout's mask
  enlarged (cleaner edges, one mask), or upscale the cut-out (the model seeing only the subject)?
- [ ] **A GIF's upscaled frames** — kept in memory (fast, but a 400 × 300 GIF of 50 frames at ×4 is
  about 380 MB), or written to a cache folder in `%TEMP%` and read back per frame (light, slower)?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository — manual checks only |
| README | | | |
| Glossary | | | |
| RULES.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In what form does the upscale arrive in the app? | A cell effect | 2026-10-29 |
| 2 | Which contents can be upscaled? | Still images and animated GIFs | 2026-10-29 |
| 3 | Which enlargement factor? | ×2 or ×4, chosen in the options | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-29 |
| 5 | Canvas sizing: widen the native size, or only sharpen the cell? | | |
| 6 | Which models are downloaded? | | |
| 7 | Cutout + Upscale: upscale then mask, or upscale the cut-out? | | |
| 8 | A GIF's upscaled frames: in memory or in a temp cache? | | |

---

*Last updated: 2026-10-29*
