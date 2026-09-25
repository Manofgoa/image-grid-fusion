# Image Grid Fusion

A tiny, fast-starting Windows desktop app that merges 1 to 4 images into a single image sized for Twitter/X's in-feed ratio.

## Features

- Fast-starting `.exe` with a GUI, no installer
- Merges 1 to 4 images into one; a single image fills the whole canvas and is exportable
- Output ratio locked to 1200:628 (≈1.91:1); the ratio matters, not the resolution (see Canvas size)
- Drag & drop images onto the `.exe` icon or onto the window, or paste them with `Ctrl+V`
- Not only images: videos, PDFs, text files, and any file Windows shows a thumbnail for, are turned into an image (see Previews)
- An **Add images** drop zone right of the preview: drop files onto it to add them after the current ones, or click it to pick files
- Several layouts per image count, picked from a strip of thumbnails, plus a mirror toggle (see Layouts)
- No image list: the grid preview *is* the interface
  - Click a cell to select it, `Esc` to deselect; the effects row acts on the selected cell (see Effects)
  - Hover a cell to outline it and show a **×** to remove it, or press `Delete` to remove the selected one
  - Videos, animated GIFs, PDFs of several pages and long texts play live in their cell (see Animated content); the **Frames** effect sets where one starts, or freezes it on a frame
  - Zoom a cell from 10 % to 1600 %: with the **Zoom** effect's slider (it snaps to 100 %), or with the mouse wheel over any cell, around the point under the mouse (4 notches double the zoom; crossing 100 % stops on it)
  - Drag an image to move it in its cell, at any zoom — past the cell's edges too, to center a detail lying on the border of the image; the area it uncovers gets the band color (see Fitting rules), and at least 10 % of the cell always stays covered so it can be grabbed back
    - Magnetic stops: the image stops where one of its edges lines up with an edge of the cell, and where it is centered; keep dragging about 24 px to go past a stop (moving back inside over an edge is free). While it is held, a dashed fluorescent green guide shows the stop: along the aligned edge, or through the center (both lines cross when centered both ways)
    - Hold `Shift` while dragging to ignore the stops
    - Zooming keeps the image where it was moved, at any zoom, but always brings it back within its stops
  - While the zoom changes (wheel or slider), its percentage shows in fluorescent green in the top-right corner of the cell, just below the ×; it stays 1 s after the last change, then fades out. Never in the exports
  - Zooming and moving show live, smoothed once the gesture ends (the wheel: once it stops turning); not while exporting
  - Drag the **✥** handle shown in the middle of a hovered cell onto another cell to swap the two images (in a small cell, it shrinks, or sits below the **×**)
  - Drop a file onto a cell to replace it
  - **Clear all** (bottom left) removes every image at once, with no confirmation, back to the initial state
- Effects per cell, from the effects row below the top bar (see Effects)
- Copy to clipboard (`Ctrl+C`) or save (`Ctrl+S`): a PNG, or an MP4 video when the grid holds animated content
- Lives in the notification area: closing the window only hides it, the tray icon brings it back, and it can start with Windows (see Tray & startup)

## Adding images

- While cells are free, new images fill them in order.
- Once the grid is full, a new image replaces the selected cell, or the last image (image 4) if none is selected.
- Adding several files at once (paste, drop, the Add images picker, or command-line arguments): free slots are filled first, the first excess file applies the replace rule above, and any further excess is ignored, with a status-line message.

## Effects

- The effects row, below the top bar, starts with an **Effects** label, then one button per effect — **Zoom**, **Rotate**, **Flip**, **Frames**, **Black & white**, **Blur** — and a **Reset** button. It acts on the selected cell: a button is pressed when its effect is active on it. With no cell selected, the row is disabled.
- Click an inactive effect to activate it and show its options in a row below; click an active one to show its options; click it again, while its options show, to remove it, bringing back its default (100 % centered, upright, unflipped, playing from the beginning, in color, sharp).
- **Reset** removes every effect of the selected cell at once.
- An effect belongs to the cell and its image: replacing the image (drop, `Ctrl+V`, picker) or removing an image clears the effects of the cells whose image changes; swapping two cells or changing the layout keeps them.
- Effects show in the preview, in every export, and on videos while they play.

### Zoom

- Options: the zoom, from 10 % to 1600 % on a log scale, snapping to 100 %.
- The mouse wheel and dragging keep working on every cell, selected or not (see above); the effect is active as soon as the image is zoomed or moved.

### Rotate

- Options: the four rotations, **0°**, **90°**, **180°** and **270°**, and a fine angle from −45° to +45° by 1°, added to the rotation. A rotation button is pressed only while the angle falls exactly on it; clicking one sets that angle, the fine angle back to 0°.
- At a fine angle, the image turns around the center of its cell, zoomed just enough to keep covering the part of the cell it covers unturned: no corner of the cell is left empty.
- Moving a turned image: the magnetic stops, their guides and the 10 % margin apply to the box around the turned image. Within its stops it keeps covering its cell; pushed beyond them, it keeps its place and zoom, and its uncovered corners get the band color.

### Flip

- Options: **Horizontal** and **Vertical**, each on its own.

### Frames

- For a video, an animated GIF, a PDF of several pages or a long text only; the button is disabled on other images.
- Options: a slider along the frames or pages, and **Freeze**.
- Not frozen, the slider sets where the content **starts playing** — its beginning by default — in the preview and in the exported video, its sound included.
- Frozen, the content stops on the frame the slider picks, in the preview and in every export: a frozen cell is exported as that still, and a grid whose contents are all frozen is copied and saved as a PNG. A frozen video has no sound.

### Black & white

- Options: the intensity, from 0 (the colors) to 100 %; the bands follow it.

### Blur

- Blurs the bands around a rectangle of the cell, which stays sharp; the rectangle is centered on half of the cell when activated.
- Four fluorescent green bars across the cell, two vertical and two horizontal, set each side of the sharp rectangle on its own: drag them while the blur's options show. A bar dragged within 6 px of its edge of the cell snaps onto it, so no thin blurred strip is left there.
- The rectangle stays in place in the cell when the image is zoomed, moved or turned.
- Options: **Gaussian** or **Pixelate**, and the intensity, relative to the cell's size so an export looks like the preview.

## Previews

A file that is not an image is turned into one when it can be previewed. The first match wins:

| File | Becomes | Slider |
|---|---|---|
| Animated GIF (two frames or more) | Its frames, played with their own delays | Frame by frame |
| Image GDI+ can decode (png, jpg, bmp, gif, tif…) | The image itself | — |
| Video (mp4, mov, m4v, avi, wmv, mkv, webm, 3gp, mpg, ts…) | A frame, first shown at 10 % of the duration | Positions a coarse step apart: duration / 100, never under 1 s |
| PDF | A whole page, rendered with its long side at 1600 px | Page by page |
| Text, whatever its extension | The text in a monospace font (see below) | Page by page, when the text needs several |
| Anything else Windows shows a thumbnail for in Explorer (webp, heic, some documents…) | That thumbnail | — |

- A file none of them handles is skipped, with a status-line message; the app never draws an icon or a placeholder instead.
- A video whose codec Windows lacks (HEVC without its Store extension, some mkv / avi) falls back to its Windows thumbnail, if any.
- The slider is the **Frames** effect's (see Effects), never in the output. The image follows it live while dragging.
- **Text** is recognized from its content: at most 1 MB, UTF-8 or UTF-16 with a byte order mark, and no NUL byte in its first 8 KB. It is rendered on pages shaped like its cell, at the cell's size on a 1200 px canvas, and laid out again when the cell changes (layout, swap, image count), keeping the reading position.
- **Readable text**: the font is the largest size between 24 and 96 px at which the whole text fits one page; below 24 px, the text is paginated at 24 px instead. Since the canvas is never narrower than the width at which no image is downscaled (see Canvas size), the text is at least that tall in the output. PDFs are rendered whole, so their small print may stay unreadable in a small cell.

## Animated content

A cell holding **multiple content** plays it, live in the preview and in the exported video:

| Content | Plays | One loop lasts |
|---|---|---|
| Video | Its frames, decoded one after the other | The video's duration |
| Animated GIF | Its frames, each for its own delay (0 or 10 ms delays stretched to 100 ms, like browsers do) | The sum of its delays |
| PDF of several pages | The next page every second | 1 s per page |
| Text longer than its cell | Every second, the view moves down half a page, keeping the lower half of the previous view on top, until the end of the text shows | 1 s per view |

A single-page PDF, a text that fits its cell, a one-frame GIF and plain images stay still.

- **Live preview**: every cell plays on one clock, so pages and views change together, each from the starting point of its Frames effect. Hovering a cell no longer holds it still: freezing goes through the Frames effect.
- **Sound**: the sound of image 1 when it is a video with sound, else of the first video with sound in grid order — a frozen video has none. The preview plays it in step with that video (held with it, looping with it), and the exported video carries it, from the video's starting point.
- **Export**: as soon as the grid holds multiple content, **Save** writes an **MP4 video** (H.264, AAC sound) instead of a PNG, and **Copy** puts an MP4 file on the clipboard (written to `%TEMP%\ImageGridFusion`, cleaned at the next start), pastable in Explorer, chat apps or mail.
  - Every content starts from the starting point of its Frames effect (its beginning without it), and a frozen one stays on its frame; the video lasts as long as the longest loop, the shorter ones starting over until it ends. 30 fps.
  - The canvas is sized once, from the first frames (see Canvas size), and rounded down to even dimensions; the bands keep the color of the first frame.
  - A sound Windows cannot re-encode leaves the video silent, with a note in the status line.
- **Force as image**: this checkbox, next to Copy, appears only while the grid holds multiple content that plays (not frozen). Checked, Copy and Save produce a PNG again, each content showing its **first frame that is not empty** (not a flat black, white or single-color frame, like a video's intro), else its first frame.
- **While exporting**, the status line shows the progress with a **Cancel** button, and the grid is locked: no adding, removing, swapping, clearing, changing the layout or the effects. The animation keeps playing. Closing the window only hides it and the export goes on; quitting (see Tray & startup) cancels the export first. A cancelled export leaves no file.

## Layouts

Each image count offers several layouts, picked by clicking a thumbnail in the strip on the left of the preview. The strip is always shown, so the preview keeps its size: with a single image it holds that count's only layout, and with no image the same thumbnail greyed out. The first layout of each count is the default; the app starts on it, and goes back to it whenever the number of images changes.

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
+---------+---------+  +------+-----+------+  +------------+------+  +-------------------+
|         |    2    |  |      |     |      |  |            |  2   |  |         1         |
|    1    +---------+  |  1   |  2  |  3   |  |     1      +------+  +---------+---------+
|         |    3    |  |      |     |      |  |            |  3   |  |    2    |    3    |
+---------+---------+  +------+-----+------+  +------------+------+  +---------+---------+
0.95 · 1.91 · 1.91     0.64 each              1.27 each              3.82 · 1.91 · 1.91
```

**4 images**

```
Grid (default)         Four columns           Featured               Big left               Big top
+---------+---------+  +----+----+----+----+  +------------+------+  +---------+---------+  +-------------------+
|    1    |    2    |  |    |    |    |    |  |            |  2   |  |         |    2    |  |         1         |
+---------+---------+  | 1  | 2  | 3  | 4  |  |     1      |  3   |  |    1    |    3    |  +------+-----+------+
|    3    |    4    |  |    |    |    |    |  |            |  4   |  |         |    4    |  |  2   |  3  |  4   |
+---------+---------+  +----+----+----+----+  +------------+------+  +---------+---------+  +------+-----+------+
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
- Up to a threshold of the overflowing axis may be cropped in total, split evenly on both sides — 15% (7.5% per side).
- Beyond that threshold, the image is cropped exactly to it and centered, and the remaining bands (and transparent pixels) are filled with a background color:
  - the image's own background, when at least three sides of the part the cell shows carry one uniform color (identical or very close, JPEG noise and slight gradients included) — a white product shot gets white bands even if its subject is mostly red. A side where the subject touches the edge, or a mostly transparent side, does not count;
  - otherwise the most frequent color of the whole image.
  The sides are those of the part actually shown, after the crop and every effect (zoom, focus, rotation and fine angle), so the color follows them. An animation keeps the same color while it plays.
  The area an image moved past its cell's edges uncovers is filled the same way, from the sides of the part still shown.
- The threshold is fixed: to crop more, zoom in; to crop less, zoom out; and move the image to choose what the cell shows (see the Zoom effect).
- The same rule applies whether the source image is too small (upscaled) or too large (downscaled).
- EXIF orientation is applied on load, so photos from phones appear upright.

## Canvas size

Output resolution is kept as high as possible so source images aren't needlessly downscaled: the canvas width is the width at which no image is downscaled in the active layout, clamped between 1200 and 4096 px; height follows from the 1200:628 ratio.

## Output

- **Copy** button / `Ctrl+C`: puts the full-resolution result on the clipboard, both as a standard bitmap and in the PNG clipboard format; with animated content, an MP4 file instead (see Animated content).
- **Save** button / `Ctrl+S`: saves the result as a PNG file; with animated content, as an MP4 video, unless **Force as image** is checked.
- A status line reports feedback and errors (skipped files with no preview, ignored excess files, removed images, copy/save confirmation or failure).

## Tray & startup

- The app shows an icon in the Windows notification area (system tray) for as long as it runs.
- **Closing the window** (its **×**, `Alt+F4`, or *Close window* in the taskbar) only hides it: the app keeps running, with its grid unchanged.
- **Click** the tray icon to bring the window back. **Right-click** it for a menu: **Open**, or **Quit** to close the app completely. Quitting is the only way to exit; logging off or shutting down Windows closes it too.
- **Start with Windows**: the **⚙** button in the bottom bar opens a menu with this checkable option, off by default. Ticked, the app is launched at session start, hidden: only the tray icon appears.
  - It is stored as a per-user `ImageGridFusion` value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, running the exe with `--tray`. No settings file, no admin rights.
  - If the exe is moved, the registration follows it the next time it is launched from its new place (any copy of the exe launched takes the registration over).
  - Disabling the app in Windows *Settings → Apps → Startup* is not reflected by the option.
- Several instances can run side by side, each with its own window and tray icon.

## Build & run

- Run: `dotnet run --project src/ImageGridFusion`
- Publish: `dotnet publish src/ImageGridFusion -c Release` → `src/ImageGridFusion/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/ImageGridFusion.exe`, a framework-dependent single-file ReadyToRun exe that requires the .NET 10 Desktop Runtime and **Windows 10 version 2004 or later**.

## Tech

C# / WinForms on .NET 10, using `System.Drawing` (GDI+) with high-quality bicubic interpolation. Previews and exports use Windows' own components only, no third-party library: `Windows.Data.Pdf` for PDFs, Media Foundation for videos (`Windows.Media.Editing` for the Frames slider's stills, the Source Reader for frame-by-frame playback, the Sink Writer for the MP4 export), and the Shell's `IShellItemImageFactory` for thumbnails.

## Planned

- A dedicated frame design for the single-image case.
