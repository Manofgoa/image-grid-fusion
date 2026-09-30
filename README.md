# Image Grid Fusion

A tiny, fast-starting Windows desktop app that merges 1 to 4 images into a single image sized for Twitter/X's in-feed ratio.

## Features

- Fast-starting `.exe` with a GUI, no installer
- Merges 1 to 4 images into one; a single image fills the whole canvas and is exportable
- Output ratio locked to 1200:628 (≈1.91:1); the ratio matters, not the resolution (see Canvas size)
- Drag & drop images onto the `.exe` icon or onto the window, or paste them with `Ctrl+V`
- Paste or drop a text too, from any app: it becomes an image, rendered like a text file, its bold, italic, underline, strike and colors kept (see Pasted text)
- Not only images: videos, PDFs, text files, and any file Windows shows a thumbnail for, are turned into an image (see Previews)
- An **Add images** drop zone right of the preview: drop files onto it to add them after the current ones, or click it to pick files
  - With no image, the empty grid does the same: click it to pick files (a **+** and a hand cursor show it is clickable), drop files onto it, or paste them
- A **file explorer** panel at the right of the preview: type a few letters to find a file in a base folder and its subfolders — from an index cached next to the exe, so it answers as you type — or type `*` to browse every file, the newest first; see them as thumbnail tiles, loaded a few screens at a time as you scroll, heart them as favorites, and drag them into a cell (see File explorer)
- Several layouts per image count, picked from a strip of thumbnails — more of them under its **More** group — plus a mirror toggle; drag the separator between two cells to resize them (see Layouts)
- No image list: the grid preview *is* the interface
  - Click a cell to select it, `Esc` to deselect; the effect tabs act on the selected cell (see Effects)
    - The selected cell shows, in fluorescent green at its bottom left, after a folder icon, the name of the file its image came from — shortened in the middle when too wide, the extension kept; hover it for the full path. Never in the exports
    - The folder icon left of the name opens Explorer on the file's folder, the file selected (a file moved or deleted since: its folder opens, if still there, and the status bar says so)
    - An image without a file shows how it arrived instead, with no icon: *Pasted image*, *Pasted text* or *Dropped text*
  - Hover a cell to outline it and show a **×** to remove it, or press `Delete` to remove the selected one
  - Videos, animated GIFs, PDFs of several pages and long texts play live in their cell (see Animated content); the **Frames** effect sets where one starts, or freezes it on a frame
    - A fluorescent green **progress line** along the bottom of every playing cell shows how far its loop has played, gliding continuously; none on a frozen content. Never in the exports
  - The sounds of every video are mixed; the **Volume** effect sets each one from 0 to 200 %, or mutes it
  - A **soundtrack** — the sound of an audio or video file — can be mixed over them, for the whole grid (see Global effects)
  - Zoom a cell from 10 % to 1600 %: with the **Zoom** effect's slider (it snaps to 100 %), or with the mouse wheel over any cell, around the point under the mouse (4 notches double the zoom; crossing 100 % stops on it). With **Ctrl** held, each notch moves the zoom by 5 %, onto the multiples of 5: 103 % → 105 % → 110 %, or 100 % the other way
  - Drag an image to move it in its cell, at any zoom — past the cell's edges too, to center a detail lying on the border of the image; the area it uncovers gets the band color (see Fitting rules), and at least 10 % of the cell always stays covered so it can be grabbed back
    - Magnetic stops: the image stops where one of its edges lines up with an edge of the cell, and where it is centered; keep dragging about 24 px to go past a stop (moving back inside over an edge is free). While it is held, a dashed fluorescent green guide shows the stop: along the aligned edge, or through the center (both lines cross when centered both ways)
    - Hold `Shift` while dragging to ignore the stops
    - Zooming keeps the image where it was moved, at any zoom, but always brings it back within its stops
  - While the zoom changes (wheel or slider), its percentage shows in fluorescent green in the top-right corner of the cell, just below the ×; it stays 1 s after the last change, then fades out. Never in the exports
  - Zooming and moving show live, smoothed once the gesture ends (the wheel: once it stops turning); not while exporting
  - Drag the **✥** handle shown in the middle of a hovered cell onto another cell to swap the two images (in a small cell, it shrinks, or sits below the **×**)
  - Drop a file or a text onto a cell to replace it
  - **Clear all** (bottom left) removes every image and the global effects at once, with no confirmation, back to the initial state
- Effects per cell, from the effect tabs at the top of the window (see Effects), and global effects for the whole grid, from their tabs at the bottom, above the bottom bar (see Global effects)
  - **Borders** on the grid, off at start-up: hotpink brackets at its four corners, or a gap between the cells drawn as a solid, dashed, dotted or double line, with an optional outer frame; the grid's corners rounded the way Twitter / X shows images; their color is set from the **⚙** menu and remembered (see Borders)
- Copy to clipboard (`Ctrl+C`) or save (`Ctrl+S`): a PNG, or an MP4 video when the grid holds content that plays or a soundtrack is on; the ▾ arrow next to each button forces a looping GIF or an MP4 video; Copy's also offers a light JPEG for sharing in chat apps that cap image size (WhatsApp: 16 MB)
- Lives in the notification area: closing the window only hides it, the tray icon brings it back, and it can start with Windows (see Tray & startup)
- The window remembers its size between launches — never its maximized state: it opens un-maximized, centered, at the size it last had, shrunk to fit a smaller screen (see Tray & startup)

## Adding images

- While cells are free, new images fill them in order.
- Once the grid is full, a new image replaces the selected cell, or the last image (image 4) if none is selected.
- Adding several files at once (paste, drop, the Add images picker, or command-line arguments): free slots are filled first, the first excess file applies the replace rule above, and any further excess is ignored, with a status-line message.
- A pasted or dropped text is one image, placed by the same rules.

## File explorer

A collapsible panel at the right of the preview, open at start-up: a search box over a **base folder** and its subfolders, and the files it finds as thumbnail tiles, dragged into the cells.

- **Tiles**: each file is a tile showing its thumbnail — the one Windows shows in Explorer, loaded in the background and kept for the session, enlarged or reduced to fill its tile — with the heart in a medallion at its corner and the file name below. The **panel's width** is yours: drag its left edge (the splitter between the preview and the panel), the preview giving way; it is remembered between sessions. The arrows move between tiles, `Enter` adds the selected one.
- **Tile size**: the slider at the bottom of the panel sets the tile size, from 100 to 1 000 px. The rows are always full: as many tiles per row as fit at that size, stretched to the row's width — widen the panel and the tiles grow, until one more fits and they all shrink at once. A lone result gets the width it would have in a full row, never the whole row. The mouse wheel over the tiles scrolls the list; **Ctrl + wheel** moves the slider, 15 % per notch, up for larger tiles. The size is remembered between sessions.
- **Base folder**: chosen from the **⚙** menu (**File explorer folder…**) and remembered between sessions. The first search made without one shows, instead of the list, an invitation with a **Choose folder…** button that does the same.
- **Index**: every file under the base folder — hidden and system entries skipped — is listed in `files.index`, next to the exe. It is loaded at start-up, so the search works at once; then the folder is **rescanned in the background**, at every launch and with the **↻** button, and the panel's status line follows: *Counting… 1 234*, then *Indexing… 5/346*, then *346 files · indexed 21:03*. Each file's creation date is recorded with its path, for the `*` list. The search never reads the disk.
- **Search**: as you type, **every match**, the best first. Every word typed must appear in the file's name or its subfolders, accents and case ignored: *ete* finds *Été.jpg*, *vacances chat* finds `Vacances 2025\chat.jpg`. Ranked by the words found in the file name itself, then a name starting with the first word before one containing it, then the shorter name. The caption says how many matched.
- **Everything**: type `*` alone to list **every file of the index**, the most recently created first — a file copied or downloaded yesterday comes first, however old the photo. The caption says *All files (12 345)*. With words (`* chat`), the star is ignored and the search is a normal one.
- **Loading as you scroll**: every list — favorites, search, `*` — shows its tiles a few screens at a time: 2 by default, the **⚙** menu's **File explorer pages per load** choosing 1 to 10, remembered between sessions. The last slot of a load is a **Loading…** tile; scroll down to it (or press `↓`, `→`, `Page Down` or `End` past the last tile) and the next screens load, the first new thumbnail taking its place. Once the list is whole, no Loading… tile is left. Loading more, a rescan ending or a heart removed from the favorites keeps the list where it is; only a new search brings it back to the top.
- **Favorites**: click the heart at the corner of a tile (♡ → ♥). They are kept in `favorites.txt` next to the exe, as absolute paths. While the search box is empty, the grid shows every favorite, the newest first; during a search they are only marked, not promoted.
- **Into the grid**: drag a tile onto a cell to replace its image, or onto the **Add images** zone to add it — exactly like a file from the Explorer; double-click a tile, or press `Enter` on it, to add it like the Add images picker; `Enter` in the search box takes the first result. Right-click a tile for **Open file location**; hover it for its full path.
- A file deleted since the last scan leaves the index (and the favorites) the moment it is dragged, double-clicked or opened, with a message on the status line; a file created since appears at the next launch or **↻**.
- The **»** button collapses the panel to a thin strip, its **«** brings it back; the state is remembered between sessions.

## Effects

- At the top of the window, the options row, then the tabs hanging below it: an **Effects** label, one tab per effect — **Background**, **Zoom**, **Rotate**, **Flip**, **Frames**, **Black & white**, **Blur**, **Volume** — and, at the far right, a **Reset** button as tall as the tabs. They act on the selected cell; with no cell selected, both rows are disabled.
- Each tab holds a checkbox, checked while its effect is on for the selected cell. Clicking it turns the effect on or off, and selects the tab.
- Clicking a tab elsewhere selects it: its options show in the row above, joined to it. The selected tab stays selected when another cell is selected, or none. The options row is always there, empty until a tab is selected.
- Turning an effect off keeps its settings: it is drawn as its default (no background, 100 % centered, upright, unflipped, playing from the beginning, in color, sharp, heard at 100 %) until it is turned on again, as it was. An effect that is off shows its kept settings in its options.
- Changing any option of an effect turns it on, from its kept settings.
- **Ctrl + wheel** on an option's slider — here and in the global effects' options — moves it by 5 %, onto the multiples of 5 (103 % → 105 %), instead of one unit per notch: 5° for the fine angle, 0.5 % for the borders' thickness, the zoom slider on the same percentages as the wheel over the cell, and the Frames slider by 5 % of the frames or pages. The slider takes the wheel as it does without Ctrl.
- The options row ends with a **Reset** button that brings the selected tab's effect back to its default state: default settings, turned off — except the Background, turned back on (see Background).
- The **Reset** at the far right of the tabs does it for every effect of the selected cell at once, and also puts every separator of the grid back where its layout places it (see Resizing the cells).
- An effect that does not apply to the selected cell (**Frames** on a still image, **Volume** on an image without sound) keeps its tab selectable, but its checkbox and options are disabled; the checkbox's tooltip says why.
- An effect belongs to the cell and its image: replacing the image (drop, `Ctrl+V`, picker) or removing an image clears the effects of the cells whose image changes — but the Volume of the images shifting after a removal, kept so what is heard does not change; swapping two cells or changing the layout keeps them.
- Effects show in the preview, in every export, and on videos while they play.

### Background

- The fill painted behind the image, over its whole cell: the bands around it and its transparent pixels show it. **On by default**, for every image placed in a cell, with the automatic color at 100 %.
- **Automatic color** (checked by default): the color the fitting rules compute from the part of the image shown (see Fitting rules).
- **Opacity**: from 0 to 100 % (default 100 %).
- The **color button** is painted with the color in use. Clicking it opens the standard color dialog, on that color; choosing a color unchecks *Automatic color*. Checking it again drops the chosen color; unchecking it keeps the automatic color of the moment as the chosen one.
- **Black & white** turns the background gray too, whatever its color.
- **Off**, or below 100 %, the cell is transparent behind its image: the preview shows grey and white squares there, as drawing apps do. A PNG — saved, or the PNG format of a copy — keeps the transparency; the copied bitmap, the MP4 video and the GIF show white instead.
- Replacing the image, the Background's own Reset and the tabs' Reset bring it back on, automatic, at 100 %.

### Zoom

- Options: the zoom, from 10 % to 1600 % on a log scale, snapping to 100 %.
- The mouse wheel and dragging keep working on every cell, selected or not (see above); the effect is on as soon as the image is zoomed or moved. On a cell whose zoom is off, they start from the image as shown, replacing the kept zoom.

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
- Four fluorescent green bars across the cell, two vertical and two horizontal, set each side of the sharp rectangle on its own: drag them while the blur's options show and the blur is on. A bar dragged within 6 px of its edge of the cell snaps onto it, so no thin blurred strip is left there.
- The rectangle stays in place in the cell when the image is zoomed, moved or turned.
- Options: **Gaussian** or **Pixelate**, and the intensity, relative to the cell's size so an export looks like the preview.

### Volume

- For a video with a sound track only, frozen included (its volume applies again once it plays); disabled on other images.
- Options: **Mute**, then the volume, from 0 to 200 %. The slider reaching 0 checks Mute. Checking Mute keeps the slider's level, and unchecking it brings it back; unchecking it at 0 brings the volume back to 100 %. The slider always stays usable: moving it above 0 unmutes.
- A video added or dropped in (or replacing another) is **heard at 100 %**, the effect off, whatever the other cells play: the sounds of every video are mixed at once. Both Resets bring it back there. Turning the effect off makes the video heard at 100 %.
- Above 100 %, the sound is amplified, clipped where it goes beyond full scale. The preview and the exported video both play it at its volume.

## Global effects

- The effects of the whole grid — not of a cell — sit at the bottom of the window, the mirror of the cell effects at the top: the tabs, then the options row below them, just above the bottom bar. The tabs row holds a **Global effects** label, one tab per global effect — **Soundtrack**, **Borders** — and, at the far right, a **Reset** button as tall as the tabs.
- Each tab holds a checkbox, checked while its global effect is on. Clicking it turns the effect on or off, keeping its settings, and selects the tab. Clicking a tab elsewhere selects it: its options show in the row below, joined to it. No tab is selected at start-up; the options row is always there, empty until one is.
- An effect that is off shows its kept settings in its options, and changing any of them turns it on.
- The options row ends with a **Reset** button that brings the selected tab's global effect back to its initial state — the one **Clear all** restores; the **Reset** at the far right of the tabs does it for both at once. The cells' **Reset** buttons leave the global effects alone, and theirs leave the cells alone.
- They work with no cell selected, and are locked while exporting.

### Soundtrack

- The sound of an **audio file** (mp3, wav, m4a, aac, wma, flac…) or of a **video** is mixed **over** the sounds of the videos, which keep playing at their own Volume — in the preview and in the exported MP4 video; a GIF has no sound.
- Check the **Soundtrack** tab: with no file yet, it opens a picker, and the soundtrack stays off if it is cancelled; with one, it turns the soundtrack on or off, keeping its file and volume. A file **dropped on the global effects' tabs or options** becomes the soundtrack and turns it on; so does one picked with **Browse…**. A file Windows reads no sound track from is refused, with a status-line message.
- Options: **Browse…**, the file's name (its whole path in a tooltip; *No file* until one is chosen), and the volume, from 0 to 200 % — above 100 %, amplified and clipped like a video's. Set before any file, the volume waits for the first one.
- Its **Reset** forgets the file: the soundtrack off, the volume back to 100 %.
- It follows the grid's duration, the longest loop: a shorter soundtrack **loops**, a longer one is **cut**. A grid of stills has no duration of its own: with the soundtrack on, it lasts as long as the soundtrack — **Copy** and **Save** then produce an MP4 video of the stills and the sound instead of a PNG.
- The preview plays it while the grid holds an image, from its start when turned on — and again, with the images, whenever one arrives or is removed — looping on the grid's duration.

### Borders

- **Off at start-up**, and back to that state with **Clear all** and the Resets. Check the **Borders** tab to turn them on — in the **Corners** style the first time — or off, their settings kept.
- Options:
  - the **style**: **Corners** (the app's signature, the default), **Solid**, **Dashed**, **Dotted** or **Double**;
  - the **thickness**, from 0.1 to 6 % of the grid's shorter side (default 0.6 %), so the preview and every export size look the same;
  - **Opacity**, from 10 to 100 % (full by default): the corner brackets' opacity, as they lie over the images. Enabled in the Corners style only;
  - **Outer frame**, off by default: also borders the grid itself. Disabled in the Corners style, whose brackets already are its frame;
  - **Twitter corners**, for every style, on by default (see below).
- **Corners**: an L-shaped bracket over the images at each of the grid's four corners, each arm covering 10 % of the edge it lies on — the cells are left as they are.
- The other styles leave a real **gap** between the cells: the grid keeps its size and the cells shrink to make room — and, with the outer frame, leave a margin as wide around the grid. The style fills the gap; what it leaves unpainted (between dashes or dots, inside the double line) is transparent, like a cell without background (see Background). Clicking in a gap acts on one of the two cells beside it; a separator is still dragged from the gap (see Resizing the cells).
- **Twitter corners**: Twitter / X shows a posted image with rounded corners, which would cut into the corner brackets. With this option, the grid's **four outer corners** are rounded as Twitter rounds them — a radius of 3 % of the grid's longer side, its 16 px on an image shown about 540 px across — and the corner brackets and the outer frame follow the curve, so the preview shows what Twitter will.
  - The **preview** shows the rounded-off corners **cut out**, as Twitter will show them. The **exports** — PNG, JPEG for sharing, GIF, MP4 video — are never cut: the corner brackets and the outer frame fill the rounded-off corners out to the square angle, their inner edge following the curve, so Twitter's own rounding, whatever its radius at the size it shows the image, never uncovers a white or transparent sliver. The gap styles without an outer frame keep the image there, for Twitter to round.
  - With the Borders off, the corners are square.
  - **Twitter corners by default**, a checkable item of the **⚙** menu, on until changed and remembered between sessions, sets whether the option is on at start-up and after **Clear all**. Changing it leaves the open grid as it is.
- **Color**: the **⚙** menu's **Border color** item, with a swatch of the current color, opens the standard color dialog; the color chosen applies at once and is remembered between sessions, per user, in `HKCU\Software\ImageGridFusion`. Hotpink until one is chosen.
- The borders show in the preview and in every export: PNG, JPEG for sharing, GIF and MP4 video.

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
- **Text** is recognized from its content: at most 1 MB, UTF-8 or UTF-16 with a byte order mark, and no NUL byte in its first 8 KB. It is rendered on pages shaped like its cell, at the cell's size on a 1200 px canvas, and laid out again when the cell changes (layout, swap, image count, a separator released), keeping the reading position.
- **Readable text**: the font is the largest size between 24 and 96 px at which the whole text fits one page; below 24 px, the text is paginated at 24 px instead. Since the canvas is never narrower than the width at which no image is downscaled (see Canvas size), the text is at least that tall in the output. PDFs are rendered whole, so their small print may stay unreadable in a small cell.

### Pasted text

- `Ctrl+V` with a text on the clipboard, or a text dragged from another app, adds it as an image rendered like a text file (see above): same monospace font, same readable sizes, same pages, same scrolling when it is long. It has no file behind it.
- What the clipboard or the drag holds is taken in this order, the first one present winning: files, an image (`Ctrl+V` only — so Excel cells still paste as a picture), rich text (RTF, as Word gives it, else HTML, as browsers give it), plain text.
- A rich text keeps its **bold**, *italic*, underline, strikethrough, text colors and highlights; its fonts and sizes are not kept, the page stays in the monospace font.
- When the whole text sits on a background — code copied from an editor in a dark theme — the page takes that background, and the text without a color of its own turns light on a dark one.
- An HTML holding no text, like an image dragged from a browser, gives way to the plain text that comes with it: the image's address shows as a text.
- An empty text adds nothing; a text longer than 1,048,576 characters is refused. Both say so on the status line.

## Animated content

A cell holding **multiple content** plays it, live in the preview and in the exported video:

| Content | Plays | One loop lasts |
|---|---|---|
| Video | Its frames, decoded one after the other | The video's duration |
| Animated GIF | Its frames, each for its own delay (0 or 10 ms delays stretched to 100 ms, like browsers do) | The sum of its delays |
| PDF of several pages | The next page every second | 1 s per page |
| Text longer than its cell | Every second, the view moves down half a page, keeping the lower half of the previous view on top, until the end of the text shows | 1 s per view |

A single-page PDF, a text that fits its cell, a one-frame GIF and plain images stay still.

- **Live preview**: every cell plays on one clock, so pages and views change together, each from the starting point of its Frames effect. An image arriving in a cell — by any route, in an empty cell or replacing another — and an image removed **start the grid over**: every animated image from its starting point, at the same instant, the soundtrack from its beginning, so the preview plays what the export gives; a frozen image stays on its frame; a swap or a layout change changes nothing. Hovering a cell no longer holds it still: freezing goes through the Frames effect.
- **Progress line**: every playing cell shows, along its bottom edge just inside the selection outline, a fluorescent green line growing from the left edge as its loop plays — read from the clock at each repaint, so it glides even for a PDF or a text that only changes every second — and starting over at each loop. None on a frozen content; on the selected cell, the Blur's bars take its place while they show. In the preview only, never in the exports.
- **Sound**: the sounds of every video with sound are **mixed**, each at its Volume — a frozen or muted video adds nothing — and the soundtrack over them when it is on (see Soundtrack). In the preview, each sound plays in step with its own video (held with it, looping with it); the exported video carries the mix, each sound from its video's starting point, looping with it, in one AAC track.
- **Export**: as soon as a content plays (not frozen), or a soundtrack is on, **Save** writes an **MP4 video** (H.264, AAC sound) instead of a PNG, and **Copy** puts an MP4 file on the clipboard (written to `%TEMP%\ImageGridFusion`, one file per export, cleaned at the next start), pastable in Explorer, chat apps or mail. The buttons name what they produce: **Copy PNG** / **Copy MP4**, **Save PNG…** / **Save MP4…**.
  - The **▾ arrow** on the right of Copy and of Save opens a menu that forces the format for that export only: **GIF** or **MP4 Video**. They are disabled while nothing plays — Save's arrow with them, Copy's staying enabled for its **JPEG for sharing** (see Output); for a still of animated content, freeze it with the Frames effect. Both arrows open again as soon as a last video exists, for **Copy last** and **Save last** (see Output).
  - A **GIF** loops forever and has no sound; each frame gets its own 256-color palette. Copied, it goes on the clipboard both as a file and in the GIF clipboard format, which some apps paste directly. A large canvas at 30 fps makes heavy GIFs.
  - Every content starts from the starting point of its Frames effect (its beginning without it), and a frozen one stays on its frame; the video or GIF lasts as long as the longest loop, the shorter ones starting over until it ends. 30 fps (GIF frames last 3 or 4 hundredths of a second, so the length stays exact). That length shows at all times in the bottom bar, left of Copy (see Output, *Length readout*).
  - The canvas is sized once, from the first frames (see Canvas size), and rounded down to even dimensions; the bands keep the color of the first frame.
  - The status line names the files whose sound is in the export, the soundtrack's included. A sound Windows cannot re-encode is left out of the mix, with a note in the status line.
- **While exporting**, the status line shows the progress with a **Cancel** button, and the grid is locked: no adding, removing, swapping, clearing, changing the layout or the effects. The animation keeps playing. Closing the window only hides it and the export goes on; quitting (see Tray & startup) cancels the export first. A cancelled export leaves no file.
- An **export ending while the app is not in the foreground** — MP4 or GIF, Copy or Save, done or failed — draws attention: the taskbar button flashes until the window comes to the front; the window hidden in the tray, a notification from the tray icon says what was copied or saved (click it to open the window). Nothing for a cancel, nor while the app is in front.

## Layouts

Each image count offers several layouts, picked by clicking a thumbnail in the strip on the left of the preview. The strip is always shown, so the preview keeps its size: with a single image it holds that count's only layout, and with no image the same thumbnail greyed out. The first layout of each count is the default; the app starts on it, and goes back to it whenever the number of images changes.

From the top, the strip holds the mirror toggle (see Mirror), the layouts below, then a **More ▸** header: click it to show that count's extra layouts (see More layouts), click **More ▾** to hide them again. It starts collapsed, and collapses again whenever the number of images changes; collapsed while one of the extra layouts is active, it keeps that one's thumbnail shown. A strip taller than the window scrolls, with the mouse wheel or its scrollbar; the thumbnails keep their size.

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

### More layouts

Under the strip's **More** group, from 2 to 4 images. Several of them are an ordinary layout at other proportions — *Bricks* is the *Grid* with its vertical line broken — offered here with exact proportions in one click.

**2 images**

```
Two thirds + one third, stacked   Three quarters + one quarter
+-------------------+             +--------------+----+
|                   |             |              |    |
|         1         |             |      1       | 2  |
+-------------------+             |              |    |
|         2         |             +--------------+----+
+-------------------+
2.87 · 5.73                       1.43 · 0.48
```

**3 images**

```
Three rows             Big centre             Big top, uneven        Corner
+-------------------+  +----+---------+----+  +-------------------+  +------------+------+
|         1         |  |    |         |    |  |         1         |  |            |      |
+-------------------+  | 2  |    1    | 3  |  +------------+------+  |     1      |      |
|         2         |  |    |         |    |  |     2      |  3   |  |            |  2   |
+-------------------+  +----+---------+----+  +------------+------+  +------------+      |
|         3         |                                                |     3      |      |
+-------------------+                                                +------------+------+
5.73 each              0.95 · 0.48 ×2         3.82 · 2.55 · 1.27     1.91 · 0.64 · 3.82
```

**4 images**

```
Four rows              Big centre             Tall left, mixed
+-------------------+  +----+---------+----+  +------+-------------+
|         1         |  |    |         | 3  |  |      |      2      |
+-------------------+  | 2  |    1    +----+  |  1   +------+------+
|         2         |  |    |         | 4  |  |      |  3   |  4   |
+-------------------+  +----+---------+----+  +------+------+------+
|         3         |  0.95 · 0.48 · 0.95 ×2  0.64 · 2.55 · 1.27 ×2
+-------------------+
|         4         |
+-------------------+
7.64 each

Uneven grid            Bricks                 Corner
+------------+------+  +------------+------+  +------------+------+
|     1      |  2   |  |     1      |  2   |  |            |      |
+------------+------+  +------+-----+------+  |     1      |  2   |
|     3      |  4   |  |  3   |     4      |  |            |      |
+------------+------+  +------+------------+  +------------+------+
2.55 · 1.27 ×2 · 2.55  2.55 · 1.27 ×2 · 2.55  |     3      |  4   |
                                              +------------+------+
                                              1.91 · 0.95 · 3.82 · 1.91
```

In *Big centre* the featured cell is in the middle: image 2 goes left of it, and with 3 images image 3 right of it.

### Mirror

The toggle at the top of the strip flips the active layout along its asymmetric axis: top↔bottom for *Big top* and *Two thirds + one third, stacked*, left↔right for the other asymmetric layouts (*Two thirds + one third*, *Big left*, *Featured*, and among the extra ones *Three quarters + one quarter*, *Big top, uneven*, *Corner*, *Big centre* with 4 images, *Tall left, mixed*, *Uneven grid*, *Bricks*). It is disabled on symmetric layouts, where flipping would only reorder the images, and it turns off whenever the layout or the number of images changes. The images keep their cells: image 1 moves with the featured cell.

```
Big left, mirrored
+---------+---------+
|    2    |         |
+---------+    1    |
|    3    |         |
+---------+---------+
```

A resized layout flips with its sizes: the big cell stays big, on the other side.

### Resizing the cells

Drag the **separator** between two cells to give one of them more room: the cursor turns into ↔ or ↕ within 4 px of it. A separator moves only the cells on both of its sides — the long one of *Big left* moves the big cell and every cell stacked next to it, the short one between two stacked cells only those two — so the grid may become irregular. The preview follows live, smoothed once the separator is released.

- **Grid** (4 images), and the extra layouts whose four cells meet in a cross (*Uneven grid*, *Bricks*, *Corner* with 4 images): while both lines of the cross are straight, each of its four arms moves on its own, between two cells; moving one breaks its line, and the other line then moves in one piece, all four cells with it, until the broken line is straight again. *Bricks* starts with its vertical line broken.
- **Minimum**: a separator stops where a cell it moves would get below 10 % of the canvas width (or height).
- **Magnetic**: within 6 px, it lands exactly back on its place in the layout, or in line with a parallel separator — such as the other arm of a broken line.
- **Back to the layout's sizes**: double-click a separator to put it back; click the active thumbnail again, or the effects **Reset**, to put all of them back. The active thumbnail keeps the layout's own shape.
- The sizes belong to the grid, not to the images: swapping two cells or replacing an image keeps them; picking another layout or changing the number of images starts again on the layout's own sizes. They are not kept between two launches.
- On the selected cell, a blur bar lying on its edge is grabbed before the separator; the separator stays reachable from the neighbour cell, or once the Blur tab is unselected or the blur is off.
- A text is laid out again for its new cell once the separator is released. Not while exporting.

## Fitting rules

- Each image is scaled to fill its cell, with no gap between cells — unless the Borders leave one, the cells shrinking for it (see Borders).
- Up to a threshold of the overflowing axis may be cropped in total, split evenly on both sides — 15% (7.5% per side).
- Beyond that threshold, the image is cropped exactly to it and centered, and the remaining bands (and transparent pixels) are filled with a background color — automatically chosen as below, unless the Background effect sets another one or none (see Background):
  - the image's own background, when at least three sides of the part the cell shows carry one uniform color (identical or very close, JPEG noise and slight gradients included) — a white product shot gets white bands even if its subject is mostly red. A side where the subject touches the edge, or a mostly transparent side, does not count;
  - otherwise the most frequent color of the whole image.
  The sides are those of the part actually shown, after the crop and every effect (zoom, focus, rotation and fine angle), so the color follows them. An animation keeps the same color while it plays.
  The area an image moved past its cell's edges uncovers is filled the same way, from the sides of the part still shown.
- The threshold is fixed: to crop more, zoom in; to crop less, zoom out; and move the image to choose what the cell shows (see the Zoom effect).
- The same rule applies whether the source image is too small (upscaled) or too large (downscaled).
- EXIF orientation is applied on load, so photos from phones appear upright.

## Canvas size

Output resolution is kept as high as possible so source images aren't needlessly downscaled: the canvas width is the width at which no image is downscaled in the active layout, with its cells as resized, clamped between 1200 and 4096 px; height follows from the 1200:628 ratio.

## Output

- **Copy** button / `Ctrl+C`: puts the full-resolution result on the clipboard, both as a standard bitmap (transparent cells on white) and in the PNG clipboard format (transparency kept); while a content plays, an MP4 file instead. Its ▾ arrow copies a GIF or an MP4 video (see Animated content), or a **JPEG for sharing**.
  - **JPEG for sharing**, always available: a light copy for chat apps that cap image size (WhatsApp: 16 MB), where the full-resolution PNG can be too heavy. The still, its long edge reduced to 2560 px at most (never enlarged), transparent cells on white, JPEG quality 90 — usually 1–2 MB. It goes on the clipboard as a `.jpg` file (written to `%TEMP%\ImageGridFusion`, cleaned at the next start), pasted as is by chat apps, Explorer or mail, plus the same image as a bitmap for Paint or Word. `Ctrl+C` stays the full copy.
- **Copy last MP4** / **Copy last GIF** button, right of Copy's ▾ — also the last entry of Copy's ▾ menu, with the time it was generated: puts the last video or GIF that Copy generated in this session back on the clipboard, without generating it again — a long generation is not lost when another copy overwrites the clipboard before it is pasted. Whatever the grid has become since, even emptied. It reads **Copy last video**, disabled, until one was generated; a PNG copy, a JPEG for sharing or a Save replaces nothing, a cancelled or failed export neither. Hover it for what it holds — generated at, size, length, sound, encoding time — and *the grid has changed since* when it has. The file stays in `%TEMP%\ImageGridFusion` until the next start; gone before that, the button says so and empties.
- **Save** button / `Ctrl+S`: saves the result as a PNG file; while a content plays, as an MP4 video. Its ▾ arrow saves a GIF or an MP4 video.
  - **Save last MP4…** / **Save last GIF…**, the last entry of Save's ▾ menu: saves the last video Copy generated (see above) where you choose — its file copied there, nothing generated.
- **Length readout** `⏱ 12.5 s`, left of Copy, always shown: what the MP4 video Copy and Save would produce lasts — the longest playing loop, or the soundtrack's length over stills — updated as the grid changes; `⏱ —` while they produce a PNG. Seconds with one decimal whatever the length, as the export summaries write it. Hover it for the detail: the file that sets the length, then every other animated content with its loop and how many times it plays (a frozen one: *frozen, a still*), and the soundtrack, cut or looping. A readout, not a button: clicking it does nothing.
- A status line reports feedback and errors (skipped files with no preview, ignored excess files, removed images, copy/save confirmation or failure).

## Tray & startup

- The app shows an icon in the Windows notification area (system tray) for as long as it runs.
- **Closing the window** (its **×**, `Alt+F4`, or *Close window* in the taskbar) only hides it: the app keeps running, with its grid unchanged.
- **Click** the tray icon to bring the window back. **Right-click** it for a menu: **Open**, or **Quit** to close the app completely. Quitting is the only way to exit; logging off or shutting down Windows closes it too.
- **Start with Windows**: the **⚙** button in the bottom bar opens a menu with this checkable option, off by default. Ticked, the app is launched at session start, hidden: only the tray icon appears.
  - It is stored as a per-user `ImageGridFusion` value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, running the exe with `--tray`. No settings file, no admin rights.
- The same menu holds **Border color** and **Twitter corners by default** (see Borders), and **File explorer folder…** and **File explorer pages per load** (see File explorer).
  - If the exe is moved, the registration follows it the next time it is launched from its new place (any copy of the exe launched takes the registration over).
  - Disabling the app in Windows *Settings → Apps → Startup* is not reflected by the option.
- **Window size**: the size the window had when it was last closed or hidden is remembered per user, in `HKCU\Software\ImageGridFusion`, and used at the next launch, whatever the screen's scale. A window closed maximized or minimized reopens at its normal size, un-maximized; a size larger than the screen it opens on is shrunk to its working area. With nothing remembered, the window opens at its default size.
- Several instances can run side by side, each with its own window and tray icon; the last window closed sets the size remembered.

## Build & run

- Run: `dotnet run --project src/ImageGridFusion`
- Publish: `dotnet publish src/ImageGridFusion -c Release` → `src/ImageGridFusion/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/ImageGridFusion.exe`, a framework-dependent single-file ReadyToRun exe that requires the .NET 10 Desktop Runtime and **Windows 10 version 2004 or later**.

## Tech

C# / WinForms on .NET 10, using `System.Drawing` (GDI+) with high-quality bicubic interpolation. Previews and exports use Windows' own components only, no third-party library: `Windows.Data.Pdf` for PDFs, Media Foundation for videos (`Windows.Media.Editing` for the Frames slider's stills, the Source Reader for frame-by-frame playback, the Sink Writer for the MP4 export), WIC's GIF encoder for the GIF export, and the Shell's `IShellItemImageFactory` for thumbnails.

## Planned

- A dedicated frame design for the single-image case.
- OCR over the indexed images, so the file explorer's search also finds the words shown inside them.
- The file explorer's list detached into a window of its own, to sit beside the main window or on another screen.
