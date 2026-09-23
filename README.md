# Image Grid Fusion

A tiny, fast-starting Windows desktop app that merges 2, 3 or 4 images into a single image sized for Twitter/X's in-feed ratio.

## Features

- Fast-starting `.exe` with a GUI, no installer
- Merges 2, 3 or 4 images (never more) into one
- Output ratio locked to ~1.91:1 (517x269 is the reference; the ratio matters, not the resolution)
- Drag & drop images onto the `.exe` icon or onto the window
- Or launch empty and paste images one after another with `Ctrl+V` (screenshots or copied files)
- Remove images from the list before merging

## Layouts

**2 images** - side by side, 50/50

```
+--------+--------+
|        |        |
|   1    |   2    |
|        |        |
+--------+--------+
```

**3 images** - first image takes the left half, the two others split the right half vertically

```
+--------+--------+
|        |   2    |
|   1    +--------+
|        |   3    |
+--------+--------+
```

**4 images** - 2x2 grid, equal cells

```
+--------+--------+
|   1    |   2    |
+--------+--------+
|   3    |   4    |
+--------+--------+
```

## Fitting rules

- Each image is scaled to fill its cell.
- Up to 15% may be cropped along the overflowing axis (top/bottom or left/right).
- If filling the cell would crop more than that, the image is centered instead and the remaining space is filled with the image's dominant color.
- The same rule applies whether the source image is too small (upscaled) or too large (downscaled).
- Output resolution is kept as high as possible so source images aren't needlessly downscaled, capped around 4096 px wide.

## Tech

C# / WinForms on .NET 10.
