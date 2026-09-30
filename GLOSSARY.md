# Glossary — Image Grid Fusion

Words used in the code, the documentation, the workfiles and the conversation, with one meaning
each.

| Term | Meaning |
|---|---|
| Grid (*grille*) | The whole composition, made of **cells** laid out by a **layout** |
| Cell (*cellule*) | One slot of the grid |
| Image | The content of a cell, **whatever its source** — still image, video, animated GIF, preview of a text / PDF or other file. "Image" never means "still image only" |
| Layout (*disposition*) | The arrangement of cells chosen in the layout strip |
| Advanced layout (*disposition avancée*) | A layout offered under the layout strip's **More** group, hidden until the group is expanded (`GridLayout.IsAdvanced`); the others are the **basic** layouts |
| Separator (*séparateur*) | A stretch of boundary between cells, dragged to resize them: it moves every cell on both of its sides, and no other |
| Arm (*bras*) | One of the four separators of the Grid's cross while both of its lines are straight |
| Selected cell | The cell the effects toolbar acts on |
| Effect (*effet*), also *cell effect* | A transformation of a cell + image pair, turned on or off from the effects toolbar: Background, Crop, Zoom, Rotate, Flip, Frames, Black & white, Blur, Volume. Turned off, it keeps its settings |
| Background (*fond*) | The cell effect painting the fill behind an image — the automatic band color or a chosen one, at an opacity; on by default, off leaves the cell transparent |
| Crop (*recadrage*) | The cell effect keeping a part of the image — the **kept part** (*partie gardée*), set by four bars in fractions of the image as loaded, so it follows a turn or a flip — which then stands for the whole image: fitted by the fitting rule, its automatic background computed on it, the canvas sized on it; an aspect ratio may be kept (Free, 1:1, 4:3, 16:9, 9:16). 10 % in from each edge when turned on |
| Crop edit view (*vue d'édition du recadrage*) | What the selected cell shows while the Crop tab is selected and the crop on: the whole image fitted whole, the part cut off dimmed, the bars across it, a drag inside the kept part moving it; preview only |
| Global effect (*effet global*) | A transformation of the whole grid, turned on or off from the global effects toolbar: Soundtrack, Fade, Borders |
| Global effects toolbar | The mirror of the effects toolbar at the bottom of the window: the "Global effects" label, one tab per global effect and the Reset button, standing on the global options toolbar |
| Global options toolbar | The always-visible row just above the bottom bar, below the global effects toolbar, holding the selected global tab's options and the effect's own Reset button |
| Soundtrack (*bande son*) | The global effect mixing the sound of an audio or video file over the heard videos, at its own volume; it loops or is cut to the grid's duration, and gives a grid of stills its length |
| Fade (*fondu*) | The global effect bringing the grid's sound mix — the heard videos and the soundtrack — in from silence at the start of the video and out to silence at its end, over one duration (0.1 to 5 s, 1 s at first), on a Squared or Linear curve; halved on a video shorter than two fades; heard in the MP4 export and in the preview on every loop of the grid; off at start-up, disabled while nothing is heard |
| Borders (*bordures*) | The global effect drawing borders on the grid, off at start-up: the Corners style, or a gap between the cells filled by a line style, with an optional outer frame and the Twitter corners, the brackets at an opacity; its color is an app setting of the ⚙ menu |
| Corners style (*style Coins*) | The Borders' default style, the app's signature: an L-bracket over the images at each of the grid's four corners, each arm covering 10 % of its edge; no gap between the cells |
| Twitter corners (*coins Twitter*) | The Borders' option rounding the grid's four outer corners as Twitter / X rounds a posted image (3 % of the grid's longer side), the brackets and the outer frame following the curve; cut out in the preview only, the exports filling the rounded-off corners with the brackets or the frame, for Twitter to round; on by default, that default an app setting of the ⚙ menu |
| Gap (*espace*) | The space the Borders leave between the cells (every style but Corners), the cells shrinking for it; hit-testing still gives it to the cells beside it |
| Effects toolbar | The always-visible row of effect tabs, hanging below the options toolbar: the "Effects" label, the effect tabs, the Reset button at the far right |
| Effect tab (*onglet*) | One effect's tab in the effects toolbar, holding its activation checkbox; the selected tab is the one whose options show |
| Activation checkbox | The checkbox in an effect tab: checked while the effect is on for the selected cell |
| Fine angle | The Rotate effect's ±45° added to the quarter turn, the image zoomed to keep covering its cell |
| Starting point | Where the Frames effect makes an animated image start playing |
| Frozen | An animated image the Frames effect holds on one frame, shown and exported as a still |
| Heard | A video whose sound is in the grid's mix: it has a sound track, plays (not frozen), and its Volume effect does not mute it |
| Video length (*durée de la vidéo*) | What the exported MP4 or GIF lasts: the longest playing loop, the shorter contents starting over until it ends; the soundtrack's length for a grid of stills with it on; none while the export is a PNG. One rule, `Animation.VideoLength`, read by the export, the preview's soundtrack loop and the length readout |
| Length readout (*durée affichée*) | The `⏱ 12.5 s` label of the bottom bar, left of Copy, always shown: the video length as the grid stands, `⏱ —` without one, the detail in its tooltip. A readout, not a control |
| Start over (*repartir de zéro*) | Every animated image playing again from its starting point at one instant, the soundtrack from its beginning: what an image arriving in a cell, or one removed, does to the grid |
| Options toolbar | The always-visible row above the effects toolbar, holding the selected tab's options and the effect's own Reset button |
| Helper indicator (*indicateur d'aide*) | A measure or geometry aid drawn over a cell in the preview only — guides, handles, value readouts such as the zoom percentage; always fluorescent green (see RULES.md) |
| Progress line (*ligne de progression*) | The helper indicator along the bottom edge of every playing cell: a fluorescent green line growing from the left edge as the content's loop plays, continuous, starting over at each loop; none on a frozen content, nor under the selected cell's blur or crop bars; preview only |
| File explorer (*explorateur de fichiers*) | The collapsible panel at the right of the preview, as wide as its splitter was dragged: a search box over the base folder's index — `*` alone listing every file, the most recently created first — the favorites while the box is empty, shown as tiles filling their rows, a few pages loaded at a time — as many per row as fit at the tile size; its tiles are dragged into the cells like files from the Explorer. Two views, switched by its 📁 button: the **search view** and the **folder view** |
| Search view (*vue recherche*) | The file explorer's view over the whole index: the favorites while the search box is empty, every file for `*`, else the matches |
| Folder view (*vue dossiers*) | The file explorer's view browsing the base folder folder by folder: the **open folder**'s subfolders A→Z, then its files the newest first, as the disk holds them; a search there looks only below the open folder, the matching folders first; remembered between sessions with its open folder |
| Open folder (*dossier ouvert*) | The folder the folder view shows, relative to the base folder — the base folder itself at first; one gone from the disk gives way to its nearest parent still there |
| Folder tile (*tuile de dossier*) | A folder in the folder view's grid: Windows' thumbnail of the folder, or a drawn folder, its name then the number of files below it as the index counts them; double-clicked, it opens; no heart, no drag |
| Breadcrumb (*fil d'Ariane*) | The folder view's caption line: the ↑ button, the base folder's name and every folder down to the open one, each opening when clicked, then what the list holds |
| Tile (*tuile*) | One file in the file explorer's grid: its thumbnail enlarged or reduced to fill a 4:3 box — the row's width shared by the row's tiles — the heart in a medallion at its corner, its name below |
| Tile size (*taille des tuiles*) | The nominal width of a tile in the file explorer, 100 to 1 000 px, set by the slider at the bottom of the panel or Ctrl + wheel over the tiles: the width at which one more tile fits on a row, the tiles stretched to fill it; remembered between sessions |
| Load (*chargement*) | The tiles a file explorer list adds at a time: **pages per load** (1 to 10, 2 by default, an app setting of the ⚙ menu) times a **page** — what the list shows at once — its last slot the **Loading…** tile while more remain, the next load coming when that tile is scrolled into view |
| Base folder (*dossier de base*) | The folder the file explorer indexes, with its subfolders; an app setting of the ⚙ menu |
| Index (*index*) | The list of every file under the base folder with its creation date, cached in `files.index` next to the exe, loaded at start-up and rescanned in the background or with ↻; what the search reads, never the disk |
| Favorite (*favori*) | A file hearted in the file explorer, or dropped onto it — from the Explorer, or a cell released there by its ✥ handle; kept in `favorites.txt` next to the exe, shown — all of them, the newest first — while the search box is empty |
| Pasted favorite (*favori collé*) | A cell with no file — a pasted image, a pasted or dropped text — made a favorite: saved first into `favorites-from-pasted\` next to the exe (the image as a PNG, the text as it came), the cell taking that file as its own; un-hearted, its file goes to the Recycle Bin |
| Last video (*dernière vidéo*) | The MP4 or GIF the last animated Copy generated in the session, its file kept in the temp folder: put back on the clipboard by **Copy last MP4 / GIF**, saved elsewhere by **Save last…**, whatever the grid has become; replaced by the next animated Copy, forgotten at exit. App state, not an effect |
