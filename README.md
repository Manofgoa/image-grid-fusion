# Image Grid Fusion

A tiny, fast-starting Windows desktop app that merges 1 to 4 images into a single image sized for Twitter/X's in-feed ratio.

## Features

- Fast-starting `.exe` with a GUI, no installer
- Merges 1 to 4 images into one; a single image fills the whole canvas and is exportable
- Output ratio locked to 1200:628 (≈1.91:1); the ratio matters, not the resolution (see Canvas size)
- Drag & drop images onto the `.exe` icon or onto the window, or paste them with `Ctrl+V`
- An **Add images** drop zone right of the preview: drop files onto it to add them after the current ones, or click it to pick files
- Several layouts per image count, picked from a strip of thumbnails, plus a mirror toggle (see Layouts)
- No image list: the grid preview *is* the interface
  - Click a cell to select it, `Esc` to deselect
  - Hover a cell to outline it and show a **×** to remove it, or press `Delete` to remove the selected one
  - Drag a cell onto another to swap the two images
  - Drop a file onto a cell to replace it
  - **Clear all** (bottom left) removes every image at once, with no confirmation, back to the initial state
- Copy to clipboard (`Ctrl+C`) or save as PNG (`Ctrl+S`)

## Adding images

- While cells are free, new images fill them in order.
- Once the grid is full, a new image replaces the selected cell, or the last image (image 4) if none is selected.
- Adding several files at once (paste, drop, the Add images picker, or command-line arguments): free slots are filled first, the first excess file applies the replace rule above, and any further excess is ignored, with a status-line message.

## Layouts

Each image count offers several layouts, picked by clicking a thumbnail in the strip on the left of the preview (the strip is hidden with a single image). The first layout of each count is the default; the app starts on it, and goes back to it whenever the number of images changes.

Image **1** always takes the featured (big) cell; the other images follow in reading order (left→right, top→bottom). Cell ratios are given for a 1.91:1 canvas: below 1 suits portraits and phone screenshots, around 1.9 landscapes, above 3 panoramas.

**1 image** - fills the whole canvas

```
+-------------------+
|                   |
|         1         |
|                   |
+-------------------+
```

**2 images**

```
Two columns (default)  Two rows               Two thirds + one third
+---------+---------+  +-------------------+  +------------+------+
|         |         |  |         1         |  |            |      |
|    1    |    2    |  +-------------------+  |     1      |  2   |
|         |         |  |         2         |  |            |      |
+---------+---------+  +-------------------+  +------------+------+
0.95 · 0.95            3.82 · 3.82            1.27 · 0.64
```

**3 images**

```
Big left (default)     Three columns          Featured               Big top
+---------+---------+  +------+------+------+  +------------+------+  +-------------------+
|         |    2    |  |      |      |      |  |            |  2   |  |         1         |
|    1    +---------+  |  1   |  2   |  3   |  |     1      +------+  +---------+---------+
|         |    3    |  |      |      |      |  |            |  3   |  |    2    |    3    |
+---------+---------+  +------+------+------+  +------------+------+  +---------+---------+
0.95 · 1.91 · 1.91     0.64 each              1.27 each              3.82 · 1.91 · 1.91
```

**4 images**

```
Grid (default)         Four columns           Featured               Big left               Big top
+---------+---------+  +----+----+----+----+  +------------+------+  +---------+---------+  +-------------------+
|    1    |    2    |  |    |    |    |    |  |            |  2   |  |         |    2    |  |         1         |
+---------+---------+  | 1  | 2  | 3  | 4  |  |     1      |  3   |  |    1    |    3    |  +------+------+------+
|    3    |    4    |  |    |    |    |    |  |            |  4   |  |         |    4    |  |  2   |  3   |  4   |
+---------+---------+  +----+----+----+----+  +------------+------+  +---------+---------+  +------+------+------+
1.91 each              0.48 each              1.27 · 1.91 ×3         0.95 · 2.87 ×3         3.82 · 1.27 ×3
```

### Mirror

The toggle below the thumbnails flips the active layout along its asymmetric axis: left↔right for the layouts whose featured cell is on the left (*Two thirds + one third*, *Big left*, *Featured*), top↔bottom for *Big top*. It is disabled on symmetric layouts, where flipping would only reorder the images, and it turns off whenever the layout or the number of images changes. The images keep their cells: image 1 moves with the featured cell.

```
Big left, mirrored
+---------+---------+
|    2    |         |
+---------+    1    |
|    3    |         |
+---------+---------+
```

## Fitting rules

- Each image is scaled to fill its cell, with no gap between cells.
- Up to 15% of the overflowing axis may be cropped in total (7.5% per side).
- Beyond that threshold, the image is cropped exactly to 15% and centered, and the remaining bands are filled with the dominant color of the whole image.
- The same rule applies whether the source image is too small (upscaled) or too large (downscaled).
- EXIF orientation is applied on load, so photos from phones appear upright.

## Canvas size

Output resolution is kept as high as possible so source images aren't needlessly downscaled: the canvas width is the width at which no image is downscaled in the active layout, clamped between 1200 and 4096 px; height follows from the 1200:628 ratio.

## Output

- **Copy** button / `Ctrl+C`: puts the full-resolution result on the clipboard, both as a standard bitmap and in the PNG clipboard format.
- **Save** button / `Ctrl+S`: saves the result as a PNG file.
- A status line reports feedback and errors (skipped files, ignored excess files, removed images, copy/save confirmation or failure).

## Build & run

- Run: `dotnet run --project src/ImageGridFusion`
- Publish: `dotnet publish src/ImageGridFusion -c Release` → `src/ImageGridFusion/bin/Release/net10.0-windows/win-x64/publish/ImageGridFusion.exe`, a framework-dependent single-file ReadyToRun exe that requires the .NET 10 Desktop Runtime.

## Tech

C# / WinForms on .NET 10, using `System.Drawing` (GDI+) with high-quality bicubic interpolation.

## Planned

- Crop threshold slider, to adjust the 15% limit from the UI.
- A dedicated frame design for the single-image case.
