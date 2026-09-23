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
- **Publish**: framework-dependent single file — `PublishReadyToRun`, `PublishSingleFile`,
  `SelfContained=false`, `RuntimeIdentifier=win-x64`, set in the `.csproj` (the standard
  `.gitignore` excludes `*.pubxml`). `dotnet publish src/ImageGridFusion -c Release` produces a
  ≈ 260 KB exe; requires the .NET 10 Desktop runtime (installed on the dev machine).
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
| 1 | `1` whole canvas | ≈ 1.91:1 |
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
  split evenly (7.5 % each side). The threshold is a parameter of the fitting computation
  (default `0.15`), not a hard-coded literal, so the future slider task can drive it.
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
- `W = clamp(max_i W_i, 1200, 4096)`, then `H` follows from the ratio. The 1200 px floor
  (reference 1200×628) upscales small sources rather than producing a tiny output.
- Because the scale accounts for the 15 % threshold, an image in "crop + bands" mode counts at
  its real applied scale, not at its fill scale.

---

## User Interface

Single resizable window. **No list**: the preview *is* the interface.

### Grid Preview

- Shows the composed result exactly as it will be exported, at the output ratio, scaled to fit the
  window (letterboxed). Rendered at display size for responsiveness; full resolution is rendered
  only on export.
- Empty state: a centered hint — drop 1 to 4 images or paste them with `Ctrl+V`.
- 1 image: shown alone on the whole canvas (single cell, same fitting rule), exportable.

### Selection

- Clicking a cell selects it (highlighted border). Clicking outside the cells or pressing `Esc`
  clears the selection.

### Removing an Image

- Hovering a cell shows a **×** in its top-right corner; clicking it removes the image.
- `Delete` removes the selected image.

### Reordering

- Dragging a cell onto another cell **swaps** the two images.

### Adding & Replacing Images

| Action | Fewer than 4 images | 4 images |
|---|---|---|
| Drop a file **onto a cell** (Explorer) | Replaces that cell | Replaces that cell |
| Drop a file elsewhere in the window | Appended | Replaces the selected cell, else the last cell in reading order |
| `Ctrl+V` (bitmap, or files copied in Explorer) | Appended | Replaces the selected cell, else the last cell in reading order |
| Files dropped on the `.exe` icon (command-line arguments) | Loaded in argument order | Nothing is selected at startup → excess replaces the last cell |

Several files added at once, going beyond 4 (paste, drop, command line): free slots are filled
in order, the **first** excess file applies the replace rule above, and the remaining excess
files are ignored with a status message. Several files dropped **onto a cell**: the first
replaces that cell, the others follow the same rule. While files from Explorer are dragged over
the preview, the cell they would replace is highlighted.

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
- Both are disabled when there is no image.
- A status line (left of the buttons) shows short messages for 4 s: skipped files, ignored
  excess files, copied, saved, nothing to paste.
- Copy/Save/Paste failures (clipboard held by another app, write denied) show a short **red**
  message in the status line for 8 s — no blocking dialog.

---

## Delivery Plan

The v1 is delivered in two milestones, so the direction can be checked on a running app early.

### Milestone 1 — Minimal app

Implementation authorized on 2026-09-23 (Iteration 4), code only.
Work happens on `main` (user's deliberate choice, Q&A #23).

- Solution and WinForms project (`net10.0-windows`).
- `Composition/` in full: layouts 1–4, canvas sizing (1200–4096 px), fitting rule with the 15 %
  threshold, dominant color fill, compositor.
- Intake: drop on the window, `Ctrl+V`, command-line arguments; images are appended up to 4,
  any excess is ignored.
- Grid preview rendered like the final result; click to select, `Esc` to deselect, `Delete` to
  remove, × on hover to remove, drag a cell onto another to swap.
- `Ctrl+C` / Copy button to the clipboard; `Ctrl+S` / Save button writing PNG.

### Milestone 2 — Rest of v1

Everything else in the design sections: drop onto a cell, replace rule when full, EXIF
orientation, status line, publish configuration, README update.
Once Milestone 1 has fixed the interfaces, these can be split across parallel agents
(e.g. grid interactions vs. loading/saving).

---

## Future Tasks (out of scope)

Planned as separate workfiles, not part of v1:

- **Crop threshold slider** — let the user adjust the 15 % threshold from the UI.
- **Single-image frame design** — an app-specific frame or decoration when only one image is
  present (v1 shows it alone, full canvas).

---

## Test Impact

**No unit tests** in v1 — declined by the user (Q&A #12). Nothing is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined) | — | — |

---

## Open Questions

- [x] ~~Publish mode: self-contained or framework-dependent single file?~~ → Framework-dependent
      single file
- [x] ~~Reordering by drag inside the grid: swap or move?~~ → Swap
- [x] ~~Several files added at once beyond the 4-image limit?~~ → Fill free slots, the first
      excess file applies the replace rule, the rest is ignored with a status message
- [x] ~~With a single image?~~ → Shown alone on the whole canvas, exportable (a dedicated frame
      design comes in a future task)
- [x] ~~Minimum canvas width when every source is small?~~ → 1200 px floor (small sources are
      upscaled)
- [x] ~~How should Copy/Save failures be reported (clipboard busy, write denied)?~~ → Short red
      message in the status line, no blocking dialog
- [x] ~~Should Copy give visible feedback?~~ → Status line message for a few seconds

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

### Iteration 2 — 2026-09-23

Open questions answered (Q&A #17–#20), a minimal first version requested, future tasks named:

- Publish: framework-dependent single file.
- Drag inside the grid swaps the two images.
- Excess files at once: free slots first, the first excess file replaces, the rest is ignored.
- One image: shown alone on the whole canvas and exportable — layout table gains a 1-image row,
  Copy/Save are enabled from 1 image.
- The user asked for a minimal app first, to check the direction on a running app → new
  `## Delivery Plan` with Milestone 1 (minimal) and Milestone 2 (rest of v1).
- The user plans a crop-threshold slider and a single-image frame design as later tasks →
  new `## Future Tasks` section; the threshold becomes a parameter (default `0.15`) so the
  slider task does not have to rework the fitting code.

### Iteration 3 — 2026-09-23

Last open question answered and Milestone 1 scope settled (Q&A #21–#23):

- Canvas width gets a 1200 px floor: `W = clamp(max_i W_i, 1200, 4096)`.
- Drag-to-swap, × on hover and Save as PNG move from Milestone 2 into Milestone 1.
- Branch: stay on `main`, deliberately.
- Go for implementation: **No** — the gate holds, the design stays open.

### Iteration 4 — 2026-09-23 — ✅ Implemented

Go given for **Milestone 1, code only**, on `main` (user's choice). Scope frozen as described in
`### Milestone 1 — Minimal app`. Milestone 2 and the README update stay gated; unit tests stay
declined.

### Iteration 5 — 2026-09-23 — 🧭 Implementation choices

Milestone 1 delivered. Choices the frozen design did not state:

- **UI language**: labels in English (`Copy`, `Save…`, empty-state hint), consistent with the
  README.
- **Preview look**: dark gray background (`#404040`), selection border and drop-target overlay
  in the system highlight color, × drawn as a dark translucent circle.
- **× acts on mouse down**, not on release.
- **Swap**: the selection follows the dragged image to its new cell.
- **Loading**: files are decoded in order only until the free slots are filled — excess files are
  not decoded at all (still ignored silently, as Milestone 1 has no status line).
- **Fully transparent image**: dominant color falls back to black.
- **Export bitmap** is 24bpp (no alpha channel), which keeps the PNG smaller.
- **Mid-run user request**: a second solution `src/ImageGridFusion.slnx` was added next to the
  root `ImageGridFusion.slnx` ("add a slnx at the root of src" read literally — the root one is
  kept).

Gaps found, not implemented (scope freeze) — offered as open questions:

- Copy/Save failures (clipboard held by another app, write denied) fall back to the default
  WinForms exception dialog.
- No feedback after Copy — the Milestone 2 status line would cover it.

No project rule was broken.

### Iteration 6 — 2026-09-23 — ⚙️ Post-implementation — Failure and copy feedback

The two gaps found in Milestone 1 are settled (Q&A #24–#25): Copy/Save failures show a red
status-line message instead of the WinForms exception dialog, and Copy confirms itself with a
status-line message. Both land with the Milestone 2 status line.

### Iteration 7 — 2026-09-23 — ⚙️ Post-implementation — Go for Milestone 2

The user liked Milestone 1 and gave the go for **Milestone 2, code + README** (Q&A #26), on
`main`. Scope frozen as described in `### Milestone 2 — Rest of v1` plus Iteration 6. Unit tests
stay declined.

### Iteration 8 — 2026-09-23 — 🧭 Implementation choices

Milestone 2 delivered — the v1 scope is complete. Choices the frozen design did not state:

- **Publish settings in the `.csproj`**, not in a publish profile: the standard `.gitignore`
  excludes `*.pubxml`. Side effect: `RuntimeIdentifier=win-x64` moves the build output to
  `bin/<Configuration>/net10.0-windows/win-x64/`. Published exe ≈ 260 KB, input-idle ≈ 260 ms
  after launch on the dev machine.
- **Drop highlight**: files dragged from Explorer over a cell highlight it before the drop.
- **Several files dropped onto a cell**: the first replaces the cell, the others follow the normal
  add rule (on a full grid, the second one replaces the selected/last cell).
- **Excess files** past the one replacement are not decoded; they are reported as "ignored",
  even if some would not have been readable.
- **Status line**: messages in English; info for 4 s, errors in red for 8 s; an extra
  "Nothing to paste" message when the clipboard holds neither files nor an image; Paste failures
  are reported like Copy/Save failures.
- **README** rewritten by a subagent from the design sections, reviewed before commit; it adds a
  short "Planned" list for the future tasks.

No project rule was broken. Work stayed on `main` (user's choice, Q&A #23).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 7 | 2026-09-23 | Milestone 1 (Iteration 4), Milestone 2 (Iteration 7) — v1 complete |
| Unit tests | 1 | 2026-09-23 | Declined by the user (Q&A #12) |
| README | 7 | 2026-09-23 | Rewritten for v1 |

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
| 17 | Publish mode (after the runtime check)? | Framework-dependent single file | 2026-09-23 |
| 18 | Drag inside the grid: swap or move? | Swap | 2026-09-23 |
| 19 | Several files beyond the limit at once? | Free slots first, first excess replaces, rest ignored | 2026-09-23 |
| 20 | Behaviour with a single image? | Full canvas, exportable; a dedicated frame design will come in a later task | 2026-09-23 |
| 21 | Minimum canvas width for small sources? | 1200 px floor | 2026-09-23 |
| 22 | Which extras go into Milestone 1 (drag-to-swap, × on hover, EXIF, Save)? | Drag-to-swap, × on hover, Save as PNG | 2026-09-23 |
| 23 | Which branch for the implementation? | Stay on `main` | 2026-09-23 |
| 24 | How are Copy/Save failures reported? | Red message in the status line | 2026-09-23 |
| 25 | Visual feedback after Copy? | Status line message | 2026-09-23 |
| 26 | Go for Milestone 2: which scope? | Code + README | 2026-09-23 |

---

*Last updated: 2026-09-23*
