# Application v1

> Working document — first version of Image Grid Fusion: a fast-starting Windows GUI `.exe`
> that merges 2, 3 or 4 images into a single image at Twitter/X's in-feed ratio.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The repository currently holds only `README.md`; no code exists yet. This workfile designs the
whole v1: project setup, image intake (drop, paste, command line), the grid preview used as the
main interaction surface, the composition engine (layouts, canvas size, fitting rule, fill color)
and the output (clipboard, PNG file).

Environment (checked 2026-09-23): .NET SDK `10.0.401` and runtime
`Microsoft.WindowsDesktop.App 10.0.12` are installed on the dev machine.

---

## Stack & Project Setup

- **C# / WinForms on .NET 10** (`net10.0-windows`, `UseWindowsForms`), chosen for startup speed and
  for native, reliable clipboard and drag & drop of images.
- **Image processing**: `System.Drawing` (GDI+), `InterpolationMode.HighQualityBicubic`,
  `PixelOffsetMode.HighQuality`, `ImageAttributes` with `WrapMode.TileFlipXY` (avoids the
  semi-transparent halo GDI+ leaves on scaled image edges).
- **Publish**: `PublishReadyToRun`, `PublishSingleFile`, `RuntimeIdentifier=win-x64`.
  Self-contained or framework-dependent: see Open Questions.
- **High DPI**: `ApplicationHighDpiMode=PerMonitorV2`.
- **No unit test project** in v1 (user decision).
- Layout:

  ```
  ImageGridFusion.sln
  src/ImageGridFusion/
    ImageGridFusion.csproj
    Program.cs                 entry point, command-line arguments
    Composition/               pure logic, no WinForms dependency
      GridLayout.cs            cell rectangles per image count
      CanvasSizer.cs           output canvas size
      FitCalculator.cs         scale / crop / placement of one image in one cell
      DominantColor.cs         dominant color of an image
      Compositor.cs            renders the final bitmap at any target width
    Imaging/
      ImageLoader.cs           file / clipboard decoding, EXIF orientation
    UI/
      MainForm.cs              window, keyboard shortcuts, status line
      GridPreview.cs           custom control: preview + selection + drag & drop
  ```

---

## Output Ratio & Layouts

- **Ratio**: `1200:628` (≈ 1.9108:1), the Twitter/X in-feed standard closest to the 517×269
  reference. Only the ratio matters; the resolution comes from the canvas sizing rule below.
- Canvas `W × H` with `H = round(W × 628 / 1200)`.
- Cells tile the canvas exactly with **no gap** (0 px). Integer split: left/top part =
  `floor(size / 2)`, right/bottom part = the remainder, so no pixel is lost to rounding.

| Images | Cells (reading order) | Cell ratio |
|---|---|---|
| 2 | `1` left half · `2` right half | ≈ 0.955:1 each |
| 3 | `1` left half · `2` top-right quarter · `3` bottom-right quarter | 0.955:1 · ≈ 1.91:1 · ≈ 1.91:1 |
| 4 | `1` top-left · `2` top-right · `3` bottom-left · `4` bottom-right | ≈ 1.91:1 each |

**Reading order** — left to right, then top to bottom — is the image order everywhere: layout
assignment, "last image", command-line order. Removing an image keeps the order of the others
and re-lays them out (e.g. going from 4 to 3 images, image 1 becomes the big left cell).

---

## Fitting Rule

Same rule whether the source is smaller (upscaled) or larger (downscaled) than its cell.
Let `cw × ch` be the cell size and `w × h` the source size.

- `sFill = max(cw / w, ch / h)` — scale that fills the cell.
- `sFit  = min(cw / w, ch / h)` — scale that shows the whole image.
- **Crop threshold**: at most **15 % in total** of the source along the overflowing axis,
  split evenly (7.5 % each side).
- **Applied scale**: `s = min(sFill, sFit / 0.85)`.
  - Filling needs ≤ 15 % crop → `s = sFill`: the cell is filled, centered crop.
  - Filling needs > 15 % crop → **crop exactly to the threshold** (15 % of the overflowing axis,
    centered) and scale that visible part to span the cell on that axis; the image is centered
    on the other axis and the remaining bands are filled with the **dominant color of the whole
    image**.
- Example: a 16:9 screenshot in a 2-image cell (0.955:1) would need 46 % crop to fill; instead
  85 % of its width spans the cell width, and bands appear above and below.
- Transparent sources: the cell is first painted with the image's dominant color, then the image
  is drawn on top, so transparent pixels show that color.

### Dominant Color

Computed on a downsampled copy (≤ 64 px on the longest side) of the whole image: colors are
quantized to 4 bits per channel (4096 buckets), fully transparent pixels are skipped, the most
populated bucket wins, and the result is the average of the pixels in that bucket. Computed once
per image when it is added, then cached.

### Source Loading

- EXIF orientation (tag `0x0112`) is applied on load, since GDI+ does not apply it — phone photos
  would otherwise appear rotated.
- Every source is copied into an independent 32bpp ARGB `Bitmap`, so no file stays locked and
  clipboard data stays valid after the clipboard changes.
- Unsupported or unreadable files are skipped with a status message.

---

## Canvas Size

Resolution as high as possible so no source is needlessly downscaled, capped at 4096 px wide.

- Cell sizes are proportional to `W`, so for each image the applied scale is `s_i = k_i × W`.
- The width at which image `i` is drawn at exactly 1:1 is `W_i = 1 / k_i`.
- `W = min(4096, max_i W_i)`, then `H` follows from the ratio.
- Because the scale accounts for the 15 % threshold, an image in "crop + bands" mode counts at
  its real applied scale, not at its fill scale.

---

## User Interface

Single resizable window. **No list**: the preview *is* the interface.

### Grid Preview

- Shows the composed result exactly as it will be exported, at the output ratio, scaled to fit the
  window (letterboxed). Rendered at display size for responsiveness; full resolution is rendered
  only on export.
- Empty state: a centered hint — drop 2 to 4 images or paste them with `Ctrl+V`.
- 1 image: see Open Questions.

### Selection

- Clicking a cell selects it (highlighted border). Clicking outside the cells or pressing `Esc`
  clears the selection.

### Removing an Image

- Hovering a cell shows a **×** in its top-right corner; clicking it removes the image.
- `Delete` removes the selected image.

### Reordering

- Dragging a cell onto another cell reorders the images (swap or move: see Open Questions).

### Adding & Replacing Images

| Action | Fewer than 4 images | 4 images |
|---|---|---|
| Drop a file **onto a cell** (Explorer) | Replaces that cell | Replaces that cell |
| Drop a file elsewhere in the window | Appended | Replaces the selected cell, else the last cell in reading order |
| `Ctrl+V` (bitmap, or files copied in Explorer) | Appended | Replaces the selected cell, else the last cell in reading order |
| Files dropped on the `.exe` icon (command-line arguments) | Loaded in argument order | Nothing is selected at startup → excess replaces the last cell |

Several files dropped or pasted at once, going beyond 4: see Open Questions.

### Launch

- Launched with files (dropped on the `.exe`): the window opens with the images loaded and the
  preview shown; nothing is exported automatically.
- Launched empty: the window opens in the empty state.

### Output

- **Copy** button + `Ctrl+C`: puts the full-resolution result in the clipboard, both as a standard
  bitmap and in the `PNG` clipboard format (browsers paste the latter more reliably).
- **Save** button + `Ctrl+S`: "Save as" dialog, **PNG** only. Default name
  `fusion-yyyyMMdd-HHmmss.png`, default folder = folder of the first file-backed image, else the
  user's Pictures folder.
- Both are disabled below 2 images.
- A status line shows short messages (skipped file, copied, saved).

---

## Test Impact

**No unit tests** in v1 — declined by the user (Q&A #12). Nothing is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined) | — | — |

---

## Open Questions

- [ ] Publish mode: **self-contained** single file (≈ 60–150 MB, runs on any Windows x64) or
      **framework-dependent** single file (a few MB, needs the .NET 10 Desktop runtime)?
      The runtime `Microsoft.WindowsDesktop.App 10.0.12` is installed on the dev machine.
- [ ] Reordering by drag inside the grid: **swap** the two images (recommended), or **move** the
      dragged image to the target position and shift the others?
- [ ] Several files added at once beyond the 4-image limit (e.g. 3 files pasted on a 3-image grid,
      6 files dropped on the `.exe`): fill the free slots, then apply the replace rule to the
      **first** excess file only and ignore the rest with a status message (recommended), or
      something else?
- [ ] With a single image: show it in the left cell of the 2-image layout with an empty placeholder
      on the right, Copy/Save disabled (recommended), or another behaviour?
- [ ] Minimum canvas width when every source is small (e.g. four 300 px thumbnails): no minimum,
      the output stays small and sharp (recommended), or a floor such as 1200 px (upscaled)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design built from the user's brief and the scoping batch (Q&A #1–#16):

- Stack, ratio, layouts, canvas sizing rule and fitting rule taken from the brief.
- Crop threshold = 15 % total on the overflowing axis; beyond it, crop exactly to the threshold
  and fill the remaining bands with the dominant color of the whole image; no gap between cells.
- The list of images from the README is replaced by the grid preview as the interaction surface:
  selection, × on hover + `Delete` to remove, drag inside the grid to reorder, drop onto a cell to
  replace it.
- Replace rule when full: selected cell, else last cell in reading order (left→right, top→bottom).
- Output: clipboard (bitmap + PNG format) and a Save button writing PNG. Launch with files opens
  the window with the preview.
- No unit test project.
- Proposed by the agent, open to review: ratio constant `1200:628`, `Composition/` kept free of
  WinForms, EXIF orientation applied on load, dominant color by 4-bit quantization, preview
  rendered at display size, `Ctrl+C` / `Ctrl+S` shortcuts, default save name and folder.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 1 | 2026-09-23 | Declined by the user (Q&A #12) |
| README | | | Must be updated: the list UI, the "centered instead" fitting wording and the fill color no longer match the design |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Is the 15 % crop total on the axis or per side? | 15 % total (7.5 % each side) | 2026-09-23 |
| 2 | Beyond the threshold: shrink the whole image with a background, or crop to the threshold and fill the rest? | Crop to the threshold, fill the rest | 2026-09-23 |
| 3 | Fill color: dominant of the whole image, dominant of the edges, or blurred image? | Dominant color of the whole image | 2026-09-23 |
| 4 | Gap between images? | None (0 px) | 2026-09-23 |
| 5 | Output destination(s)? | Clipboard | 2026-09-23 |
| 6 | File format on disk? | PNG | 2026-09-23 |
| 7 | How to control the order of dropped files? | No list: a preview laid out like the final result, reorderable by drag & drop | 2026-09-23 |
| 8 | Launch with files dropped on the `.exe`: preview window or generate and exit? | Window with preview | 2026-09-23 |
| 9 | Clipboard only was chosen, yet PNG as format: what does PNG mean here? | Add a Save button writing PNG | 2026-09-23 |
| 10 | Without a list, how is an image removed from the preview? | × on hover + `Delete` key | 2026-09-23 |
| 11 | What happens with a 5th image? | Dropped onto a cell: replaces that cell. Pasted: replaces the selected cell, else the last cell of the grid. Cell selection must be supported | 2026-09-23 |
| 12 | xUnit project for the pure logic? | No | 2026-09-23 |
| 13 | Which cell is "last" (the answer said left-right, bottom-top)? | Typo: left→right, top→bottom (bottom-right cell) | 2026-09-23 |
| 14 | With fewer than 4 images, a file dropped onto an existing cell? | Replaces the targeted cell | 2026-09-23 |
| 15 | Publish self-contained or framework-dependent? | Asked back whether the .NET 10 runtime is installed — checked: yes (10.0.12). Still open | 2026-09-23 |
| 16 | Depth of exploration? | Straightforward | 2026-09-23 |
| 17 | Publish mode (after the runtime check)? | | |
| 18 | Drag inside the grid: swap or move? | | |
| 19 | Several files beyond the limit at once? | | |
| 20 | Behaviour with a single image? | | |
| 21 | Minimum canvas width for small sources? | | |

---

*Last updated: 2026-09-23*
