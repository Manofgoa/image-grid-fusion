<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English · [<img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français](README.fr.md)

# Image Grid Fusion

A tiny, fast-starting Windows desktop app that merges 1 to 4 images into a single image sized for Twitter/X's in-feed ratio — or a square, a portrait, a story, a 16:9 or the content's own ratio.

Each word used here has one meaning, given in the [Glossary](GLOSSARY.md).

## Features

- Fast-starting `.exe` with a GUI, no installer
- Merges 1 to 4 images into one; a single image fills the whole canvas and is exportable
- Output ratio picked among a few formats — **Twitter** 1200:628 (≈1.91:1, the default), **Square** 1:1, **Portrait** 4:5, **Story** 9:16, **Landscape** 16:9, or **Free**, the content's own; the ratio matters, not the resolution (see Format, Canvas size)
- Drag & drop images onto the `.exe` icon or onto the window, or paste them with `Ctrl+V`
- Paste or drop a text too, from any app: it becomes an image, rendered like a text file, its bold, italic, underline, strike and colors kept (see Pasted text)
- Not only images: videos, PDFs, text files, and any file Windows shows a thumbnail for, are turned into an image (see Previews)
- An **Add images** drop zone right of the preview: drop files onto it to add them after the current ones, or click it to pick files
  - With no image, the empty grid does the same: click it to pick files (a **+** and a hand cursor show it is clickable), drop files onto it, or paste them
- A **file explorer** panel at the right of the preview: type a few letters to find a file in a base folder and its subfolders — from an index cached next to the exe, so it answers as you type — or type `*` to browse every file, the newest first — or by name or size —, or browse it folder by folder with 📁; see them as thumbnail tiles, loaded a few screens at a time as you scroll, heart them as favorites — or drop files from Explorer, or a cell by its ✥ handle, onto the panel — and drag them into a cell (see File explorer)
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
  - A **soundtrack** — the sound of an audio or video file — can be mixed over them, for the whole grid (see Global)
  - A **fade** brings the whole mix in from silence at the start of the video, and out to silence at its end (see Fade)
  - Zoom a cell from 10 % to 2000 % (the maximum can be changed, see Settings file): with the **Zoom** effect's slider (it snaps to 100 %), or with the mouse wheel over any cell, around the point of the image under the mouse, which stays under it (crossing 100 % stops on it). Each notch moves the zoom by 5 %, onto the multiples of 5: 103 % → 105 % → 110 %, or 100 % the other way; with **Ctrl** held, by 1 %. Above 200 % the steps grow to 25 % (5 % with Ctrl): 195 % → 200 % → 225 % → 250 %. The wheel over the Zoom slider takes the same steps
  - Drag an image to move it in its cell, at any zoom — past the cell's edges too, to center a detail lying on the border of the image; the area it uncovers gets the band color (see Fitting rules), and at least 10 % of the cell always stays covered so it can be grabbed back
    - Magnetic stops: the image stops where one of its edges lines up with an edge of the cell, and where it is centered; keep dragging about 24 px to go past a stop (moving back inside over an edge is free). While it is held, a dashed fluorescent green guide shows the stop: along the aligned edge, or through the center (both lines cross when centered both ways)
    - Hold `Shift` while dragging to ignore the stops
  - The **arrow keys** move the selected image the same way, whatever tab is selected, 1 screen pixel per press, 10 with `Ctrl` (holding a key repeats it). They go to the image once the preview is clicked; a click on a slider, a list or the file explorer gives them back to it. A magnetic stop holds the image for one press: it stops exactly on it, the guide shows, and the next press leaves it — an edge stop on the way back in too; add `Shift` to ignore the stops. `Ctrl+Shift` + an arrow jumps straight to the next stop in that direction — the center or an edge, whichever comes first — and holds the image there with its guide. In the crop's edit view they do not move the image, like a drag
    - Zooming never moves the image otherwise: the wheel keeps the point under the mouse in place, the slider, Contain / Fill and the Animations keep the image's center where it was moved — even when that uncovers a band; only an image moved nearly out of its cell is pushed back, to keep covering 10 % of it
  - While the zoom changes (wheel or slider), its percentage shows in fluorescent green in the top-right corner of the cell, just below the ×; it stays 1 s after the last change, then fades out. Never in the exports
  - While an image moves in its cell — dragged, pushed by the arrow keys, zoomed with the wheel, turned, cropped, or shifted by a separator, the format or the borders — its **position** shows in fluorescent green: an X cross on the image's center, a dashed line to it from the cell's center at half opacity, and its offset from that center in the pixels of the export, `x -35px` over `y +12px` (x to the right, y downward), in the top-right corner of the cell, below the zoom percentage when it shows. Meanwhile the cell's ✥ handle is hidden: a press at the center moves the image. It stays while the mouse button is held, then 1 s after the last change, then fades out. Not in the crop's edit view; never in the exports
  - Zooming and moving show live, smoothed once the gesture ends (the wheel: once it stops turning); not while exporting
  - Drag the **✥** handle shown in the middle of a hovered cell onto another cell to swap the two images (in a small cell, it shrinks, or sits below the **×**)
  - Drop a file or a text onto a cell to replace it
  - **Clear all** (bottom left) or `Ctrl+N` removes every image and the global effects at once, and brings the format back to Twitter, with no confirmation, back to the initial state
- **Undo** with `Ctrl+Z`, as many times as needed, and **redo** with `Ctrl+Y` or `Ctrl+Shift+Z`: images added, replaced, deleted or swapped, effects, layout, separators, format and global effects (see Undo & redo)
- Effects per cell, from the effect tabs at the top of the window (see Effects), and the format and global effects for the whole grid, from the Global tabs at the bottom, above the bottom bar (see Global)
  - **Borders** on the grid, off at start-up: hotpink brackets at its four corners, or a gap between the cells drawn as a solid, dashed, dotted or double line, with an optional outer frame; the grid's corners rounded the way Twitter / X shows images; their color is set from their options, the last one chosen remembered (see Borders)
- Copy to clipboard (`Ctrl+C`) or save (`Ctrl+S`): a PNG, or an MP4 video when the grid holds content that plays or a soundtrack is on; the ▾ arrow next to each button forces a looping GIF or an MP4 video; Copy's also offers a light JPEG for sharing in chat apps that cap image size (WhatsApp: 16 MB)
- Lives in the notification area: closing the window only hides it, the tray icon brings it back, and it can start with Windows (see Tray & startup)
- The window remembers its size between launches — never its maximized state: it opens un-maximized, centered, at the size it last had, shrunk to fit a smaller screen (see Tray & startup)
- Runs once: launching it again brings the running window back, with the files given (see Tray & startup)

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
- **Index**: every file under the base folder — hidden and system entries skipped — is listed in `files.index`, in the `index\` folder next to the exe. It is loaded at start-up, so the search works at once; then the folder is **rescanned in the background**, at every launch and with the **↻** button, and the panel's status line follows: *Counting… 1 234*, then *Indexing… 5/346*, then *346 files · indexed 21:03*. Each file's creation date is recorded with its path, for the `*` list. The search never reads the disk.
- **Search**: as you type, **every match**, the best first. Every word typed must appear in the file's name or its subfolders, accents and case ignored: *ete* finds *Été.jpg*, *vacances chat* finds `Vacances 2025\chat.jpg`. Ranked in groups: the files whose name holds the words — the more of them, the earlier — then those found by their subfolders only, then those found by their content (see Search criteria); inside a group, the files follow the **Sort** drop-down. The caption says how many matched.
- **Search criteria**: the **Aa** and **💡** buttons between **📁** and the search box choose where the words are looked for — pressed, a criterion is on. **Aa**: in the file's name and its subfolders; **💡**: in the text inside the file, the one the **⚙** menu's **Search file contents (OCR)** extracts (with the setting off, the texts already extracted are still searched). Both are on at start-up, and the search finds a file by either; at least one stays on — clicking the only one pressed does nothing. With 💡 alone, the folder view shows no folder tile, a folder having no text. Clicking one runs the search again; on the favorites or `*`, nothing changes until words are typed. Not remembered between sessions.
- **Found in its content**: a tile the search found by the text inside it shows a **💡** in a medallion under its heart, its tooltip giving the words around; on an image or a PDF page read by the OCR, an **arrow** goes from the bulb to the word found. The **⚙** menu's **OCR result style** sets how it is drawn, remembered between sessions: **Curved arrow**, on by default — a slight curve arriving straight down onto the word (or straight up, from below, onto a word above the bulb), rather than a straight line; **Arrow thickness**, from **0 (none)** — no arrow — to **5 px**, 2 by default, its dark outline not counted, the head growing with it; **Arrow color…**, amber until another is chosen, the outline staying dark.
- **`Ctrl+F`**, from anywhere in the window, puts the focus in the search box, its text selected — the panel opened first if it was collapsed; the box's hint says so. `Escape` there gives the focus back to the grid, the search kept.
- **Everything**: type `*` alone to list **every file of the index**, in the **Sort** drop-down's order — the most recently created first by default: a file copied or downloaded yesterday comes first, however old the photo. The caption says *All files (12 345)*. With words (`* chat`), the star is ignored and the search is a normal one.
- **Sort**: the drop-down at the bottom right of the panel, after the tile size slider, orders the files: **Date ↓** (the most recently created first, the default), **Name A→Z** or **Size ↑** (the smallest first). It orders `*` and a browsed folder, and the files of each group of a search, the best matches still first; the favorites and the folders keep their own order. A size not known yet — an index written by an older version, until the start-up scan rewrites it — sorts last. The order is remembered between sessions.
- **Loading as you scroll**: every list — favorites, search, `*` — shows its tiles a few screens at a time: 2 by default, the **⚙** menu's **File explorer pages per load** choosing 1 to 10, remembered between sessions. The last slot of a load is a **Loading…** tile; scroll down to it (or press `↓`, `→`, `Page Down` or `End` past the last tile) and the next screens load, the first new thumbnail taking its place. Once the list is whole, no Loading… tile is left. Loading more, a rescan ending or a heart removed from the favorites keeps the list where it is; only a new search brings it back to the top.
- **Favorites**: click the heart at the corner of a tile (♡ → ♥). They are kept in `favorites.txt` next to the exe, as absolute paths. While the search box is empty, the grid shows every favorite, the newest first; during a search they are only marked, not promoted.
- **Dropping favorites**: drop files onto the panel — anywhere on it, search results or favorites, and on its collapsed strip too — and they become favorites, the last one dropped the newest; the panel is framed while you hover it. Any file, even outside the base folder; a folder is skipped; a file already a favorite moves back to the top. A search on screen stays, its hearts updated. A cell works the same: drag it by its **✥** handle out of the grid and release it over the panel — no swap — and its file becomes a favorite. The panel's own tiles are not dropped back onto it (their heart does it), and a text dragged from another app is refused. It works during an export too.
- **Pasted favorites**: a cell with no file — a pasted image, a pasted or dropped text — is first saved into `favorites-from-pasted\` next to the exe, named `pasted-yyyyMMdd-HHmmss` (`-2`, `-3`… when taken): the image as it was pasted, as a PNG, without the cell's effects; a text as it came, `.rtf`, `.html` or `.txt`, so it reads back with its styles and all its pages. The cell then takes that file as its own: its name shows, with the folder icon that opens Explorer on it, and dropping it again adds no copy. Un-heart one of these favorites and its file goes to the Recycle Bin — the app made it for the favorite; any other favorite un-hearted leaves its file alone.
- **Into the grid**: drag a tile onto a cell to replace its image, or onto the **Add images** zone to add it — exactly like a file from the Explorer; double-click a tile, or press `Enter` on it, to add it like the Add images picker; `Enter` in the search box takes the first result. Right-click a tile for **Copy** — the file itself on the clipboard, as `Ctrl+C` in the Explorer, pastable in a folder, a chat app or a browser — **Copy PNG** — its image as a cell shows it when the file arrives (a video's frame at 10 % of its length, a PDF's or a text's page), at full size, as a bitmap and in the PNG clipboard format — and **Open file location**; `Ctrl+C` with the tiles focused does Copy on the selected tile, never copying the grid. Hover a tile for its full path.
- A file deleted since the last scan leaves the index (and the favorites) the moment it is dragged, double-clicked or opened, with a message on the status line; a file created since appears at the next launch or **↻**.
- **Folder view**: the **📁** button left of the search box switches the panel from its search view (favorites, search, `*`) to browsing the base folder **folder by folder**, and back. The open folder is listed **from the disk**, as Explorer lists it: its subfolders first, A→Z — empty ones included — then its files, in the **Sort** drop-down's order; a file created since the last scan shows at once. A **folder tile** shows the thumbnail Windows gives the folder (a drawn folder when it has none) and its name followed by the number of files below it, its subfolders' included, as the index counts them. Double-click it, or press `Enter` on it, to open it; a folder tile has no heart and does not drag, and its right-click offers **Open in Explorer**. The caption line becomes a **breadcrumb**: **↑** opens the parent folder, each folder of the path opens when clicked, and the counts follow at its end; `Backspace` or `Alt+↑` go up too, the folder just left selected. A search typed in the folder view looks **only below the open folder**: the matching folders first, then the matching files; `*` lists every folder below it, each followed by its subfolders, then every file below it, in the **Sort** drop-down's order. The view and the open folder are remembered between sessions; a folder gone from the disk gives way to its nearest parent still there.
- The **»** button collapses the panel to a thin strip, its **«** brings it back; the state is remembered between sessions.

## Effects

- At the top of the window, the options row, then the tabs hanging below it: an **Effects** label, one tab per effect — **Background**, **Crop**, **Zoom**, **Animations**, **Rotate**, **Flip**, **Frames**, **Black & white**, **Blur**, **Volume** — and, at the far right, a **Reset** button as tall as the tabs. They act on the selected cell; with no cell selected, both rows are disabled.
- Each tab holds a checkbox, checked while its effect is on for the selected cell. Clicking it turns the effect on or off, and selects the tab.
- Clicking a tab elsewhere selects it: its options show in the row above, joined to it. The selected tab stays selected when another cell is selected, or none. The options row is always there, empty until a tab is selected.
- The choices of the options — the Background's fills, the Crop's ratios, the Zoom's Contain and Fill, the quarter turns, the mirrors, the Blur's kinds — are **thumbnails**, all of one size: each draws what it does, its name below, its tooltip saying it; the pressed ones are highlighted, the hovered one lit.
- Turning an effect off keeps its settings: it is drawn as its default (no background, the whole image, 100 % centered, upright, unflipped, playing from the beginning, in color, sharp, heard at 100 %) until it is turned on again, as it was. An effect that is off shows its kept settings in its options.
- Changing any option of an effect turns it on, from its kept settings.
- **Ctrl + wheel** on an option's slider — here and in the global effects' options — moves it by 5 %, onto the multiples of 5 (103 % → 105 %), instead of one unit per notch: 5° for the fine angle, 0.5 % for the borders' thickness, and the Frames sliders by 5 % of the frames or pages they cover. The slider takes the wheel as it does without Ctrl. The zoom slider is the exception: its wheel takes the steps of the wheel over the cell, Ctrl making them finer (see Zoom).
- The options row ends with a **Reset** button that brings the selected tab's effect back to its default state: default settings, turned off — except the Background, turned back on (see Background).
- The **Reset** at the far right of the tabs does it for every effect of the selected cell at once, and also puts every separator of the grid back where its layout places it (see Resizing the cells).
- An effect that does not apply to the selected cell (**Frames** on a still image, **Volume** on an image without sound) keeps its tab selectable, but its checkbox and options are disabled; the checkbox's tooltip says why.
- An effect belongs to the cell and its image: replacing the image (drop, `Ctrl+V`, picker) or removing an image clears the effects of the cells whose image changes — but the Volume of the images shifting after a removal, kept so what is heard does not change; swapping two cells or changing the layout keeps them.
- Effects show in the preview, in every export, and on videos while they play.

### Background

- The fill painted behind the image, over its whole cell: the bands around it and its transparent pixels show it. **On by default**, for every image placed in a cell, with the automatic color at 100 %.
- **Automatic color** (checked by default): the color the fitting rules compute from the part of the image shown (see Fitting rules).
- **Opacity**: from 0 to 100 % (default 100 %), over the whole background — the extended edges included.
- **Fill**: four thumbnails, **Color** by default.
  - **Color**: the flat fill around the image.
  - **Corner pixel**, **Miter** and **Background corners** extend the image's **edges**: its top row of pixels is stretched up to the top of the cell, its bottom row down to the bottom, its side columns out to the sides — the image as drawn in the cell, so cropped, rotated, flipped, zoomed and in black & white. Where the image is smaller than its cell both ways, each mode fills the four corners its own way: the color of the image's corner pixel; a mitered joint — each corner split on its diagonal, each half the flat color of its edge's pixel next to the image's corner, a 1 px line in the corner pixel's color drawn over the diagonal at 50 % opacity; or the background color. With a fine angle, the edges extended are those of the rectangle the turned image covers, its corners spilling over them.
  - The color settings still count: the flat fill stays under the image (its transparent pixels show it), fills the Background corners, and is what Blend goes to.
- The fill's settings sit on two lines beside the thumbnails: the color's (*Automatic color*, Opacity, *Color…*, showing the color in use as a swatch) over the extended edges' (Edges, Blend, Soften).
- **Edges**: from 0 to 100 % (default 100 %): the extended edges' own opacity over the background color, which keeps its own — at 0 %, the bands show the background color only.
- **Blend**: from 0 to 100 % (default 0 %): the extended edges blend into the background color with the distance from the image, reaching that share of it at the cell's edge — at 100 %, they vanish there.
- **Soften** (unchecked by default): blurs the extended edges, more with the distance from the image, the image itself staying sharp.
- Edges, Blend and Soften act on the extended edges only: disabled with the Color fill, their values kept.
- The Crop's edit view extends the edges of the whole image it shows.
- The **color button** is painted with the color in use. Clicking it opens the standard color dialog, on that color; choosing a color unchecks *Automatic color*. Checking it again drops the chosen color; unchecking it keeps the automatic color of the moment as the chosen one.
- **Black & white** turns the background gray too, whatever its color.
- **Off**, or below 100 %, the cell is transparent behind its image: the preview shows grey and white squares there, as drawing apps do. A PNG — saved, or the PNG format of a copy — keeps the transparency; the copied bitmap, the MP4 video and the GIF show white instead.
- Replacing the image, the Background's own Reset and the tabs' Reset bring it back on, automatic, at 100 %, with the Color fill, Edges at 100 %, Blend at 0 % and Soften unchecked.

### Crop

- Keeps a part of the image, which then **becomes the image**: the kept part fills the cell by the fitting rules, as a whole image would (up to 15 % cropped, bands beyond); its automatic background is computed on it, the color following the bars live; and the canvas is sized so it is not downscaled (see Canvas size). **10 % cut off each edge** when activated.
- **Edit view**: while the Crop tab is selected and the crop is on, the selected cell shows the **whole image** — rotated and flipped, but neither zoomed nor turned by a fine angle — fitted whole, the part cut off dimmed, on the background the cropped image gets. The other cells, the other tabs and every export show the cropped image.
- Four fluorescent green bars across the image set each side of the kept part on its own — solid along the kept part, dashed beyond it up to the image's edge, as the guides are, and grabbable all along; a bar dragged within 6 px of the image's edge snaps onto it. A drag **inside the kept part moves it whole**, its size kept, stopped at the image's edges. Elsewhere on that cell, a drag does nothing while the edit view shows; the ✥ handle and the × keep working.
- Its **four corners**, each marked by a green L-bracket, move the two bars meeting there at once, following the mouse on both axes while the opposite corner stays put; each bar snaps onto the image's edge as on its own. Under a ratio (see the options below), the kept part keeps it: it grows or shrinks from the opposite corner, as far as the mouse goes on either axis, stopped at the image's edges.
- The **mouse wheel**, anywhere over that cell, grows the kept part (up) or shrinks it (down) by 5 % a notch, keeping its ratio — the thumbnail's, or its current shape under Free — around its center, or around the point under the cursor with **Ctrl** held. Growing against an edge of the image, it slides along it and keeps growing the other way, up to the largest kept part at that ratio; shrinking stops at the bars' minimum gap. With **Alt** held, the wheel zooms the image instead, as it does without the bars (see Zoom): the zoom does not show in this view, only its percentage, and the point of the image under the cursor stays where the cropped cell shows it.
- The crop **follows the image**: turning or flipping it keeps the same part. Zoom, fine angle and Blur apply to the cropped image.
- Options: the ratios **Free**, **1:1**, **4:3**, **16:9** and **9:16**, each thumbnail drawing the kept part at its ratio. Picking one reshapes the kept part to the largest rectangle at that ratio inside it, around its center; under a ratio, a dragged bar takes the two across along, around the center, so the ratio holds. The ratio is in pixels, so 1:1 is square whatever the image. A quarter turn turns it along — 16:9 becomes 9:16 — and frees a 4:3, which has no 3:4 thumbnail.
- **100 %**, set apart after the ratios by a line, keeps the **whole image**: every bar back on its edge, the ratio freed (Free pressed). Like any option it turns the crop on first — one click on a crop that is off turns it on at 100 %. The crop's own Reset still brings back 10 % in from each edge, off.

### Zoom

- Options: the zoom, from 10 % to the maximum zoom (2000 % by default) on a log scale, snapping to 100 %. The wheel over it moves the zoom by the wheel's steps over a cell — 5 %, 1 % with Ctrl, 25 % / 5 % above 200 % (see above); its arrow keys keep their own small steps.
- **Contain** and **Fill**, after the zoom, fit the image to its cell: **Contain** shows the whole image, bands on one side; **Fill** covers the cell, the overflow cropped. The fit **lasts**: while one is pressed, the zoom follows the cell — resized by a separator, a layout, the format — and the image, cropped or turned; the slider and the label show the zoom it gives. The image stays where it was moved, and can still be dragged.
  - Pressing the other one switches to it; unpressing the pressed one, the wheel or the slider leave the fit for a free zoom, starting from the zoom shown.
  - A fine angle still zooms the fitted image just enough to cover what it covers unturned.
- The mouse wheel and dragging keep working on every cell, selected or not (see above) — but on the selected cell, while the Crop or Blur tab shows its bars, the wheel scales their zone instead (see Crop, Blur), unless **Alt** is held: Alt + wheel zooms the image as usual, Ctrl still making its steps finer; the effect is on as soon as the image is zoomed or moved. On a cell whose zoom is off, they start from the image as shown, replacing the kept zoom.

### Animations

- Plays a motion in the cell, on the grid's clock. One kind for now, **Zoom**: the image zooms in, then back out, continuously — from where the other effects place it (the Zoom effect included) to +20 % and back around its own center, on a sine, slowing down at both ends.
- Options: the **Type** drop-down (**Zoom**), then the duration of one **back-and-forth**, from 1 s to 30 s (6 s by default): the further right, the faster.
- Applies to every image, a frozen one included. The zoom keeps the image's center where the Zoom effect puts it.
- The motion follows the grid's clock, so the preview plays what the export gives: turning the effect on, or changing its duration, picks the motion up where the clock stands; an image arriving or removed starts it over with the grid.
- A moving cell **plays a loop**, as long as its back-and-forth: it shows the progress line over that cycle (a playing video keeps its own loop's), makes **Copy** and **Save** produce an MP4 video, and counts in the video length like a video's loop — the longest loop wins, and over a grid of stills it sets the length even with the soundtrack on. The video starts at the starting state; a PNG shows that state.

### Rotate

- Options: the four rotations, **0°**, **90°**, **180°** and **270°**, and a fine angle from −45° to +45° by 1°, added to the rotation. A rotation is pressed only while the angle falls exactly on it; clicking one sets that angle, the fine angle back to 0°.
- At a fine angle, the image turns around the center of its cell, zoomed just enough to keep covering the part of the cell it covers unturned: no corner of the cell is left empty.
- Moving a turned image: the magnetic stops, their guides and the 10 % margin apply to the box around the turned image. Within its stops it keeps covering its cell; pushed beyond them, it keeps its place and zoom, and its uncovered corners get the band color.

### Flip

- Options: **Horizontal** and **Vertical**, each on its own.

### Frames

- For a video, an animated GIF, a PDF of several pages or a long text only; the button is disabled on other images.
- Options: at the left, the **trim** — **From** and **To**, each a slider along the frames — then, at the right, **Starts at**, a slider along the frames or pages, and **Freeze**.
- **Trim** (a video or an animated GIF only; disabled on a PDF or a text): only the frames from **From** to **To**, both played, play — in the preview, in the sound, in every export and in the video length; the whole content by default. A bound pushed against the other stops there, one frame apart at least.
- **Fields**: each of the three sliders has its value in text fields, for a frame-accurate setting — for a video, its **minutes : seconds : frame** within the second, at the file's own frame rate; for a GIF, one field, its **frame number**. **↑ / ↓** (or the wheel over a field) add or take 1, **5** with **Ctrl**, carrying over from one field to the next like a clock (frame 23 at 24 fps + ↑ → the next second's frame 0). A typed number applies on **Enter** or when the field is left; an invalid one is put back. A PDF or a text shows its page instead.
- Not frozen, **Starts at** sets where the content **starts playing** — the trim's start by default — in the preview and in the exported video, its sound included; its slider covers the trimmed frames only.
- Frozen, the content stops on the frame the slider picks, in the preview and in every export: a frozen cell is exported as that still, and a grid whose contents are all frozen is copied and saved as a PNG. A frozen video has no sound.

### Black & white

- Options: the intensity, from 0 (the colors) to 100 %; the bands follow it.

### Blur

- Blurs the bands around a rectangle of the cell, which stays sharp; the rectangle is centered on half of the cell when activated.
- Four fluorescent green bars across the cell, two vertical and two horizontal, set each side of the sharp rectangle on its own, drawn as the crop's (see Crop): drag them while the blur's options show and the blur is on. A bar dragged within 6 px of its edge of the cell snaps onto it, so no thin blurred strip is left there. Its four corners, each marked by a green L-bracket, move the two bars meeting there at once. The **mouse wheel**, anywhere over that cell, grows or shrinks the sharp rectangle as it does the crop's kept part (see Crop) — 5 % a notch, its shape kept, around its center or the point under the cursor with Ctrl — within the cell; with **Alt** held, it zooms the image instead (see Zoom).
- The rectangle stays in place in the cell when the image is zoomed, moved or turned.
- Options: **Gaussian** or **Pixelate**, and the intensity, relative to the cell's size so an export looks like the preview.

### Volume

- For a video with a sound track only, frozen included (its volume applies again once it plays); disabled on other images.
- Options: **Mute**, then the volume, from 0 to 200 %. The slider reaching 0 checks Mute. Checking Mute keeps the slider's level, and unchecking it brings it back; unchecking it at 0 brings the volume back to 100 %. The slider always stays usable: moving it above 0 unmutes.
- A video added or dropped in (or replacing another) is **heard at 100 %**, the effect off, whatever the other cells play: the sounds of every video are mixed at once. Both Resets bring it back there. Turning the effect off makes the video heard at 100 %.
- Above 100 %, the sound is amplified, clipped where it goes beyond full scale. The preview and the exported video both play it at its volume.

## Global

- What concerns the whole grid — not a cell — sits at the bottom of the window, the mirror of the cell effects at the top: the tabs, then the options row below them, just above the bottom bar. The tabs row holds a **Global** label, the **Format** tab, one tab per global effect — **Soundtrack**, **Fade**, **Borders**, **Cascade** — and, at the far right, a **Reset** button as tall as the tabs.
- Each global effect's tab holds a checkbox, checked while its effect is on. Clicking it turns the effect on or off, keeping its settings, and selects the tab. Clicking a tab elsewhere selects it: its options show in the row below, joined to it. No tab is selected at start-up; the options row is always there, as tall as the Format's thumbnails, empty until a tab is selected.
- The **Format** is a setting, always in force: its tab has no checkbox.
- An effect that is off shows its kept settings in its options, and changing any of them turns it on.
- The options row ends with a **Reset** button that brings the selected tab back to its initial state — the one **Clear all** restores: the format Twitter, an effect off with its initial settings; the **Reset** at the far right of the tabs does it for all of them at once. The cells' **Reset** buttons leave the Global tabs alone, and theirs leave the cells alone.
- They work with no cell selected, and are locked while exporting.

### Format

- The ratio of the final image or video — the preview's canvas and every export — picked from a row of thumbnails, each drawing the current layout, its cells as resized, at the format's ratio, its name below; the active one is highlighted, a click on another picks it.
- **Free**, the content's own ratio, outlined dashed; **Twitter**, 1200:628 (≈1.91:1), Twitter / X's in-feed ratio and the default; **Square 1:1**; **Portrait 4:5**; **Story 9:16**; **Landscape 16:9**. Hover a thumbnail for what it suits.
- The grid stretches to the format, and every image fits its cell by the usual fitting rule (see Fitting rules).
- **Free** takes, between 9:16 and 21:9, the ratio where the images lose the least — what the fitting rule crops off plus the bands it leaves, over every cell, a big cell counting more: a single image gets its own ratio. Empty cells and texts (which take their cell's shape) do not count; with nothing that counts, it is Twitter's ratio. It follows the grid as it changes — an image added, removed, swapped, cropped or turned, another layout — but holds while a separator or a crop bar is dragged, or the wheel scales the crop's kept part, computed again when it is released or the wheel stops, so the canvas never changes shape under the mouse.
- The **layout strip** draws its thumbnails at the format's ratio.
- Not remembered: the app starts in Twitter, and **Clear all** and the **Reset** buttons bring it back there.

### Soundtrack

- The sound of an **audio file** (mp3, wav, m4a, aac, wma, flac…) or of a **video** is mixed **over** the sounds of the videos, which keep playing at their own Volume — in the preview and in the exported MP4 video; a GIF has no sound.
- Check the **Soundtrack** tab: with no file yet, it opens a picker, and the soundtrack stays off if it is cancelled; with one, it turns the soundtrack on or off, keeping its file and volume. A file **dropped on the Global tabs or options** becomes the soundtrack and turns it on; so does one picked with **Browse…**. A file Windows reads no sound track from is refused, with a status-line message.
- Options: **Browse…**, the file's name (its whole path in a tooltip; *No file* until one is chosen), and the volume, from 0 to 200 % — above 100 %, amplified and clipped like a video's. Set before any file, the volume waits for the first one.
- Its **Reset** forgets the file: the soundtrack off, the volume back to 100 %.
- It follows the grid's duration, the longest loop: a shorter soundtrack **loops**, a longer one is **cut**. A grid of stills has no duration of its own: with the soundtrack on, it lasts as long as the soundtrack — **Copy** and **Save** then produce an MP4 video of the stills and the sound instead of a PNG.
- The preview plays it while the grid holds an image, from its start when turned on — and again, with the images, whenever one arrives or is removed — looping on the grid's duration.

### Fade

- The whole sound mix — every heard video and the soundtrack — **rises from silence** over the fade's duration at the start of the video, and **falls back to silence** over as long before its end. In the exported MP4 video, and in the preview on every loop of the grid: a sound shorter than the grid keeps looping within it without fading, only the grid's start and end fade. A GIF has no sound.
- **Off at start-up**, and back to that state with **Clear all** and the Resets. Check the **Fade** tab to turn it on or off, its settings kept.
- Options: the **duration**, from 0.1 to 5 s by 0.1 s, 1 s at first, used for both ends; then the **curve**, two buttons drawing the fade's shape — **Squared**, the first one (the gain grows as the square of the ramp, heard as a steady rise), or **Linear** — their names in tooltips.
- A video shorter than two fades gets two fades of half its length: the sound rises, then falls straight away.
- While **nothing is heard** — no video plays with its sound, and no soundtrack is on above 0 % — the Fade's checkbox and options are disabled, the checkbox's tooltip saying why; its settings are kept for when a sound comes back.

### Borders

- **Off at start-up**, and back to that state with **Clear all** and the Resets. Check the **Borders** tab to turn them on — in the **Corners** style the first time — or off, their settings kept.
- Options:
  - the **style**: **Corners** (the app's signature, the default), **Solid**, **Dashed**, **Dotted** or **Double**;
  - **Color…**, with a swatch of the color in use, opens the standard color dialog: the color chosen applies at once, turns the Borders on, and is remembered for the next start-up, in the settings file (see Tray & startup). Hotpink by default — the color the Resets and **Clear all** bring back, the remembered one left as it is;
  - the **thickness**, from 0.1 to 6 % of the grid's shorter side (default 0.6 %), so the preview and every export size look the same;
  - **Opacity**, from 10 to 100 % (full by default): the corner brackets' opacity, as they lie over the images. Enabled in the Corners style only;
  - **Outer frame**, off by default: also borders the grid itself. Disabled in the Corners style, whose brackets already are its frame;
  - **Twitter corners**, for every style, on by default, in the **Twitter** format only (see below).
- **Corners**: an L-shaped bracket over the images at each of the grid's four corners, each arm covering 10 % of the edge it lies on — the cells are left as they are.
- The other styles leave a real **gap** between the cells: the grid keeps its size and the cells shrink to make room — and, with the outer frame, leave a margin as wide around the grid. The style fills the gap; what it leaves unpainted (between dashes or dots, inside the double line) is transparent, like a cell without background (see Background). Clicking in a gap acts on one of the two cells beside it; a separator is still dragged from the gap (see Resizing the cells).
- **Twitter corners**: Twitter / X shows a posted image with rounded corners, which would cut into the corner brackets. With this option, the grid's **four outer corners** are rounded as Twitter rounds them — a radius of 3 % of the grid's longer side, its 16 px on an image shown about 540 px across — and the corner brackets and the outer frame follow the curve, so the preview shows what Twitter will.
  - The **preview** shows the rounded-off corners **cut out**, as Twitter will show them. The **exports** — PNG, JPEG for sharing, GIF, MP4 video — are never cut: the corner brackets and the outer frame fill the rounded-off corners out to the square angle, their inner edge following the curve, so Twitter's own rounding, whatever its radius at the size it shows the image, never uncovers a white or transparent sliver. The gap styles without an outer frame keep the image there, for Twitter to round.
  - With the Borders off, the corners are square.
  - In any other **format** (see Format), the option is disabled — its label saying *Twitter format only* — its setting kept, and the corners are square; back in Twitter, they are rounded again as set.
  - **Twitter corners by default**, a checkable item of the **⚙** menu, on until changed and remembered between sessions, sets whether the option is on at start-up and after **Clear all**. Changing it leaves the open grid as it is.
- The borders show in the preview and in every export: PNG, JPEG for sharing, GIF and MP4 video.

### Cascade

- The videos and animated GIFs play **one after the other** instead of all at once: the first cell's, then the second's, then the third's… in **cell order** — a swap or another layout changes the order. Once the last one has played, the cascade starts over from the first.
- A **turn** is one whole loop of what the content plays — its trim (see Frames) — from its **starting point** round to it again. Before its turn, a content stands still on its starting point; after it, on the last frame it played. It is **heard during its turn only**.
- Taking part: every video or animated GIF that plays. A frozen one is a still; a PDF of several pages, a text longer than its cell and an Animations motion keep playing **alongside**, on the grid's clock, as without the Cascade.
- **Off at start-up**, and back to that state with **Clear all** and the Resets. Check the **Cascade** tab to turn it on or off, its settings kept. Turning it on or off, or changing its pause, **starts the grid over**: the first content plays at once.
- Option: the **Pause**, from 0 to 3 s by 0.1 s, 0 at first: how long every content stands still after each turn — the last one included, so the video pauses as evenly when it starts over.
- The **video length** is the whole cascade — every turn and its pause — unless an Animations cycle, or a content playing alongside, lasts longer; the soundtrack loops or is cut on it, and the Fade fades the cascade's start and end. The **progress line** shows on the content playing its turn only.
- While **no video nor animated GIF plays**, the Cascade's checkbox and option are disabled, the checkbox's tooltip saying why; its settings are kept.
- The preview, the MP4 video and the GIF play it alike; a PNG shows every content on its starting point, as without it.

## Previews

A file that is not an image is turned into one when it can be previewed. The first match wins:

| File | Becomes | Slider |
|---|---|---|
| Animated GIF (two frames or more) | Its frames, played with their own delays | Frame by frame |
| Image GDI+ can decode (png, jpg, bmp, gif, tif…) | The image itself | — |
| Video (mp4, mov, m4v, avi, wmv, mkv, webm, 3gp, mpg, ts…) | A frame, first shown at 10 % of the duration | Frame by frame, at the file's frame rate |
| PDF | A whole page, rendered with its long side at 1600 px | Page by page |
| Text, whatever its extension — an `.rtf` or `.html` with its styles | The text in a monospace font (see below) | Page by page, when the text needs several |
| Anything else Windows shows a thumbnail for in Explorer (webp, heic, some documents…) | That thumbnail | — |

- A file none of them handles is skipped, with a status-line message; the app never draws an icon or a placeholder instead.
- A video whose codec Windows lacks (HEVC without its Store extension, some mkv / avi) falls back to its Windows thumbnail, if any.
- The slider is the **Frames** effect's (see Effects), never in the output. The image follows it live while dragging.
- **Text** is recognized from its content: at most 1 MB, UTF-8 or UTF-16 with a byte order mark, and no NUL byte in its first 8 KB. The extension only decides how it reads: an `.rtf` file, or an `.htm` / `.html` one, is read like a pasted rich text (see Pasted text), with its bold, italic, colors and highlights — plain text when nothing can be read from it; any other extension, as plain text. It is rendered on pages shaped like its cell, at the cell's size on the smallest canvas (its longer side 1200 px), and laid out again when the cell changes (layout, swap, image count, a separator released, another format), keeping the reading position.
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

- **Live preview**: every cell plays on one clock, so pages and views change together, each from the starting point of its Frames effect. An image arriving in a cell — by any route, in an empty cell or replacing another — and an image removed **start the grid over**: every animated image from its starting point, at the same instant, the soundtrack from its beginning, so the preview plays what the export gives; a frozen image stays on its frame; a swap or a layout change changes nothing. With the **Cascade** on, the videos and animated GIFs play one after the other instead (see Cascade). Hovering a cell no longer holds it still: freezing goes through the Frames effect.
- **Progress line**: every playing cell — and every cell moved by its Animations effect, over the motion's cycle — shows, along its bottom edge just inside the selection outline, a fluorescent green line growing from the left edge as its loop plays — read from the clock at each repaint, so it glides even for a PDF or a text that only changes every second — and starting over at each loop. None on a frozen content; on the selected cell, the Blur's or the Crop's bars take its place while they show. In the preview only, never in the exports.
- **Sound**: the sounds of every video with sound are **mixed**, each at its Volume — a frozen or muted video adds nothing — and the soundtrack over them when it is on (see Soundtrack); the Fade brings the mix in and out (see Fade). In the preview, each sound plays in step with its own video (held with it, looping with it); the exported video carries the mix, each sound from its video's starting point, looping with it — within its trim — in one AAC track.
- **Export**: as soon as a content plays (not frozen), a cell moves by its Animations effect, or a soundtrack is on, **Save** writes an **MP4 video** (H.264, AAC sound) instead of a PNG, and **Copy** puts an MP4 file on the clipboard (written to `%TEMP%\ImageGridFusion`, one file per export, cleaned at the next start), pastable in Explorer, chat apps or mail. The buttons name what they produce: **Copy PNG** / **Copy MP4**, **Save PNG…** / **Save MP4…**.
  - The **▾ arrow** on the right of Copy and of Save opens a menu that forces the format for that export only: **GIF** or **MP4 Video**. They are disabled while nothing plays — Save's arrow with them, Copy's staying enabled for its **JPEG for sharing** (see Output); for a still of animated content, freeze it with the Frames effect. Both arrows open again as soon as a last video exists, for **Copy last** and **Save last** (see Output).
  - A **GIF** loops forever and has no sound; each frame gets its own 256-color palette. Copied, it goes on the clipboard both as a file and in the GIF clipboard format, which some apps paste directly. A large canvas at 30 fps makes heavy GIFs.
  - Every content starts from the starting point of its Frames effect (its beginning without it), and a frozen one stays on its frame; a cell whose Animations effect is on moves from its starting state. The video or GIF lasts as long as the longest loop — a trimmed content's being its trimmed part, an animation's cycle counting as one — the shorter ones starting over until it ends. 30 fps (GIF frames last 3 or 4 hundredths of a second, so the length stays exact). That length shows at all times in the bottom bar, left of Copy (see Output, *Length readout*).
  - The canvas is sized once, from the first frames (see Canvas size), and rounded down to even dimensions; the bands keep the color of the first frame.
  - The status line names the files whose sound is in the export, the soundtrack's included. A sound Windows cannot re-encode is left out of the mix, with a note in the status line.
- **While exporting**, the status line shows the progress with a **Cancel** button, and the grid is locked: no adding, removing, swapping, clearing, changing the layout or the effects. The animation keeps playing. Closing the window only hides it and the export goes on; quitting (see Tray & startup) cancels the export first. A cancelled export leaves no file.
- An **export ending while the app is not in the foreground** — MP4 or GIF, Copy or Save, done or failed — draws attention: the taskbar button flashes until the window comes to the front; the window hidden in the tray, a notification from the tray icon says what was copied or saved (click it to open the window). Nothing for a cancel, nor while the app is in front.

## Layouts

Each image count offers several layouts, picked by clicking a thumbnail in the strip on the left of the preview. The strip is always shown, so the preview keeps its size: with a single image it holds that count's only layout, and with no image the same thumbnail greyed out. The first layout of each count is the default; the app starts on it, and goes back to it whenever the number of images changes.

From the top, the strip holds the mirror toggle (see Mirror), the layouts below, then a **More ▸** header: click it to show that count's extra layouts (see More layouts), click **More ▾** to hide them again. It starts collapsed, and collapses again whenever the number of images changes; collapsed while one of the extra layouts is active, it keeps that one's thumbnail shown. A strip taller than the window scrolls, with the mouse wheel or its scrollbar; the thumbnails keep their size.

Image **1** always takes the featured (big) cell; the other images follow in reading order (left→right, top→bottom). Cell ratios are given for a 1.91:1 canvas (the Twitter format): below 1 suits portraits and phone screenshots, around 1.9 landscapes, above 3 panoramas.

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
- On the selected cell, a Blur or Crop bar lying on its edge — or one of their corners there — is grabbed before the separator; the separator stays reachable from the neighbour cell, or once that tab is unselected or its effect is off.
- A text is laid out again for its new cell once the separator is released. Not while exporting.

## Fitting rules

- Each image is scaled to fill its cell, with no gap between cells — unless the Borders leave one, the cells shrinking for it (see Borders).
- Up to a threshold of the overflowing axis may be cropped in total, split evenly on both sides — 15% (7.5% per side).
- Beyond that threshold, the image is cropped exactly to it and centered, and the remaining bands (and transparent pixels) are filled with a background color — automatically chosen as below, unless the Background effect sets another one or none (see Background):
  - the image's own background, when at least three sides of the part the cell shows carry one uniform color (identical or very close, JPEG noise and slight gradients included) — a white product shot gets white bands even if its subject is mostly red. A side where the subject touches the edge, or a mostly transparent side, does not count;
  - otherwise the most frequent color of the whole image.
  The sides are those of the part actually shown, after the crop and every effect (zoom, focus, rotation and fine angle), so the color follows them. An animation keeps the same color while it plays.
  The area an image moved past its cell's edges uncovers is filled the same way, from the sides of the part still shown.
- With the **Crop** effect, the kept part stands for the whole image: the rule, and the automatic color, apply to it.
- The threshold is fixed: to crop more, zoom in; to crop less, zoom out; and move the image to choose what the cell shows (see the Zoom effect).
- The same rule applies whether the source image is too small (upscaled) or too large (downscaled).
- EXIF orientation is applied on load, so photos from phones appear upright.

## Canvas size

Output resolution is kept as high as possible so source images aren't needlessly downscaled: the canvas is the size at which no image — the kept part of a cropped one — is downscaled in the active layout, with its cells as resized, at the format's ratio (see Format), its **longer side** clamped between 1200 and 4096 px — the width of a Twitter, Landscape or wide Free canvas, the height of a Story or Portrait one (675 × 1200 to 2304 × 4096 in Story).

## Undo & redo

- `Ctrl+Z` takes back the last change, again and again, up to the **50** last ones; `Ctrl+Y` or `Ctrl+Shift+Z` redoes what was taken back. A new change after an undo drops what could still be redone.
- Everything the grid is made of is covered: images added, replaced, deleted or swapped, **Clear all**, every effect setting, on / off and Reset, the layout, its mirror and the separators, the format and the global effects. Not covered: the selection and the selected tabs, what the ⚙ menu sets, the file explorer, the last video.
- A **gesture is one step**: a drag from press to release (a separator, the blur or crop bars, a move, a ✥ swap, a slider), a burst of the wheel. A step is taken once the grid has stayed still for about 0.3 s, so two changes closer than that — two very fast clicks — make one.
- The status line names what was taken back or redone and how many steps remain that way: `↶ Undone: image deleted — 4 more`, `↷ Redone: Blur — nothing more`.
- The green indicators of what changed show for a moment on the cell, whatever tab is selected: the zoom percentage, the blur bars, the edges of the crop, the guides of the edge or center an image moved back onto, the position of an image moved — held 1 s, then fading. The bars cannot be grabbed then: select their tab to move them.
- A step touching a single cell selects it; others leave the selection as it is. Bringing images back or taking them out starts the grid over, like adding or deleting them; a swap, a layout or an effect change does not.
- In the file explorer's search box, these keys undo the typed text instead. Locked while exporting, and while a gesture runs. The history starts empty at each launch — files dropped on the `.exe` icon are its starting point — and is not saved.

## Output

- **Copy** button / `Ctrl+C`: puts the full-resolution result on the clipboard, both as a standard bitmap (transparent cells on white) and in the PNG clipboard format (transparency kept); while a content plays, an MP4 file instead. Its ▾ arrow copies a GIF or an MP4 video (see Animated content), or a **JPEG for sharing**. With the file explorer's tiles focused, `Ctrl+C` copies the selected tile's file instead (see [File explorer](#file-explorer)).
  - **JPEG for sharing**, always available: a light copy for chat apps that cap image size (WhatsApp: 16 MB), where the full-resolution PNG can be too heavy. The still, its long edge reduced to 2560 px at most (never enlarged), transparent cells on white, JPEG quality 90 — usually 1–2 MB. It goes on the clipboard as a `.jpg` file (written to `%TEMP%\ImageGridFusion`, cleaned at the next start), pasted as is by chat apps, Explorer or mail, plus the same image as a bitmap for Paint or Word. `Ctrl+C` stays the full copy.
  - **Previous copy**: every Copy — PNG, MP4, GIF, JPEG for sharing — also keeps what it copied in a `previous\` folder next to the exe (**Open app folder** in the ⚙ menu), the last one only: the one before is deleted. Its file is named after the cells' files, in cell order, without their extensions, joined by ` + ` (`cat + beach.mp4`) — a cell without a file (pasted) skipped, a name met twice kept once — or `fusion-yyyyMMdd-HHmmss` when no cell has a file. It is a copy: the clipboard still points at the temp file. **Copy last** generates nothing, so it keeps nothing. A file that cannot be written (disk full, read-only folder, a name too long for Windows) does not fail the copy: the status line says why it was not kept.
- **Copy last MP4** / **Copy last GIF** button, right of Copy's ▾ — also the last entry of Copy's ▾ menu, with the time it was generated: puts the last video or GIF that Copy generated in this session back on the clipboard, without generating it again — a long generation is not lost when another copy overwrites the clipboard before it is pasted. Whatever the grid has become since, even emptied. It reads **Copy last video**, disabled, until one was generated; a PNG copy, a JPEG for sharing or a Save replaces nothing, a cancelled or failed export neither. Hover it for what it holds — generated at, size, length, sound, encoding time — and *the grid has changed since* when it has. The file stays in `%TEMP%\ImageGridFusion` until the next start; gone before that, the button says so and empties.
- **Save** button / `Ctrl+S`: saves the result as a PNG file; while a content plays, as an MP4 video. Its ▾ arrow saves a GIF or an MP4 video.
  - **Save last MP4…** / **Save last GIF…**, the last entry of Save's ▾ menu: saves the last video Copy generated (see above) where you choose — its file copied there, nothing generated.
- **Length readout** `⏱ 12.5 s`, left of Copy, always shown: what the MP4 video Copy and Save would produce lasts — the longest playing loop (an Animations cycle included), the whole cascade with the Cascade on, or the soundtrack's length over stills — updated as the grid changes; `⏱ —` while they produce a PNG. Seconds with one decimal whatever the length, as the export summaries write it. Hover it for the detail: the file that sets the length, then every other animated content with its loop and how many times it plays (a frozen one: *frozen, a still*), and the soundtrack, cut or looping. A readout, not a button: clicking it does nothing.
- A status line reports feedback and errors (skipped files with no preview, ignored excess files, removed images, what an undo or a redo changed, copy/save confirmation or failure).

## Tray & startup

- The app shows an icon in the Windows notification area (system tray) for as long as it runs.
- **Closing the window** (its **×**, `Alt+F4`, or *Close window* in the taskbar) only hides it: the app keeps running, with its grid unchanged.
- **Click** the tray icon to bring the window back — maximized again if it was minimized while maximized. **Right-click** it for a menu: **Open**, or **Quit** to close the app completely. Quitting is the only way to exit; logging off or shutting down Windows closes it too.
- **Start with Windows**: the **⚙** button in the bottom bar opens a menu with this checkable option, off by default. Ticked, the app is launched at session start, hidden: only the tray icon appears.
  - It is stored as an `ImageGridFusion.lnk` shortcut in your Startup folder (`shell:startup`), running the exe with `--tray`. No registry, no admin rights.
- The same menu holds **Twitter corners by default** (see Borders), and **File explorer folder…** and **File explorer pages per load** (see File explorer). It ends with **Open app folder**, which opens Explorer on the folder of the running exe, the exe selected: where `settings.json` and the app's other files live (see Settings file below).
  - If the exe is moved, the shortcut follows it the next time it is launched from its new place (any copy of the exe launched takes the registration over, except with `--new-instance`).
  - Disabling the app in Windows *Settings → Apps → Startup* is not reflected by the option.
- **Window size**: the size the window had when it was last closed or hidden is remembered in the settings file and used at the next launch, whatever the screen's scale. A window closed maximized or minimized reopens at its normal size, un-maximized; a size larger than the screen it opens on is shrunk to its working area. With nothing remembered, the window opens at its default size.
- **One instance per exe**: launching the exe again — its icon, a shortcut, files dropped on it, *Open with* — starts no second app. The running one comes to the front — from the tray, minimized (maximized again if it was) or behind other windows — and loads the files given, as a drop would (refused while exporting). A launch with `--tray` while the app runs leaves it as it is, and a `--title` given then is ignored. Copies of the exe in other folders are separate apps, each with its own instance.
- **`--new-instance`**: starts an instance of its own even while one runs, for tests — Claude Code launches the app this way (see `CLAUDE.md`). It does not answer later launches, and writes nothing of its own accord: the Startup shortcut is not moved to it, its window size is not remembered, an older version's registry settings are not moved. A setting changed by hand in it is saved as usual. Combines with `--title`, `--tray` and files, in any order.
- **Second title**: `ImageGridFusion.exe --title "Undo / redo"` opens a window titled *Image Grid Fusion — Undo / redo* — in its title bar, its taskbar button and Alt+Tab — to tell instances running side by side apart (Claude Code passes the name of its session, see `CLAUDE.md`). Combines with `--tray` and with files, in any order; the value is the argument right after `--title`. Missing or blank, it is ignored; given twice, the last one wins. The tray icon's tooltip shows it too, on a second line below *Image Grid Fusion* — cut with `…` past the 127 characters Windows allows a tooltip. It is never remembered.
- **Last folders**: the **Add images** picker, **Choose a soundtrack** and the Save dialogs open on the folder they last used, remembered between sessions — one for Add images, one for the soundtrack, one shared by the exports and **Save last…**. Only a file picked in the dialog updates it: a drop or a paste does not. A folder gone since (deleted, drive unplugged) gives way to its nearest parent still there. Until a first save, the Save dialogs open on the folder of the first image's file, else *Pictures*.
- **Settings file**: every setting above — the last border color chosen, Twitter corners by default, the file explorer's folder, panel, width, tile size, pages per load, view and open folder, order of the files, the window size, the last folders — is kept in `settings.json`, next to the exe, like the `index\` folder and `favorites.txt`; each copy of the exe has its own. One setting has no control in the app and is edited in the file by hand: the **maximum zoom**, `"MaxZoom"`, in percent — 2000 by default, 200 to 10 000. It is read at launch, so a change takes effect at the next one, and the app writes back the value it applies whenever the file does not hold it: missing or unreadable → 2000, above 10 000 → 10 000, below 200 → 200. The app never uses the registry: the settings an older version kept in `HKCU\Software\ImageGridFusion` are moved into the file at the first launch, then that key is deleted, and an older Start with Windows registration (`HKCU\…\Run`) becomes the Startup folder shortcut.

## Build & run

See [CONTRIBUTING.md § Build](CONTRIBUTING.md#build).

## Tech

C# / WinForms on .NET 10, using `System.Drawing` (GDI+) with high-quality bicubic interpolation. Previews and exports use Windows' own components only, no third-party library: `Windows.Data.Pdf` for PDFs, Media Foundation for videos (`Windows.Media.Editing` for the Frames slider's stills, the Source Reader for frame-by-frame playback, the Sink Writer for the MP4 export), WIC's GIF encoder for the GIF export, and the Shell's `IShellItemImageFactory` for thumbnails.

## Planned

- A dedicated frame design for the single-image case.
- OCR over the indexed images, so the file explorer's search also finds the words shown inside them.
- The file explorer's list detached into a window of its own, to sit beside the main window or on another screen.

## License

[MIT](LICENSE) © Manofgoa. Contributions: see [CONTRIBUTING.md](CONTRIBUTING.md); security issues: see [SECURITY.md](SECURITY.md).
