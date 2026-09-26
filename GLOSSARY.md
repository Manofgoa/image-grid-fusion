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
| Effect (*effet*), also *cell effect* | A transformation of a cell + image pair, turned on or off from the effects toolbar: Background, Zoom, Rotate, Flip, Frames, Black & white, Blur, Volume. Turned off, it keeps its settings |
| Background (*fond*) | The cell effect painting the fill behind an image — the automatic band color or a chosen one, at an opacity; on by default, off leaves the cell transparent |
| Global effect (*effet global*) | A transformation of the whole grid, turned on or off from the global effects toolbar: Soundtrack, Borders |
| Global effects toolbar | The mirror of the effects toolbar at the bottom of the window: the "Global effects" label, one tab per global effect and the Reset button, standing on the global options toolbar |
| Global options toolbar | The always-visible row just above the bottom bar, below the global effects toolbar, holding the selected global tab's options and the effect's own Reset button |
| Soundtrack (*bande son*) | The global effect mixing the sound of an audio or video file over the heard videos, at its own volume; it loops or is cut to the grid's duration, and gives a grid of stills its length |
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
| Options toolbar | The always-visible row above the effects toolbar, holding the selected tab's options and the effect's own Reset button |
| Helper indicator (*indicateur d'aide*) | A measure or geometry aid drawn over a cell in the preview only — guides, handles, value readouts such as the zoom percentage; always fluorescent green (see RULES.md) |
| File explorer (*explorateur de fichiers*) | The collapsible panel at the right of the preview: a search box over the base folder's index, the favorites while the box is empty, shown as tiles in 1 to 5 columns; its tiles are dragged into the cells like files from the Explorer |
| Tile (*tuile*) | One file in the file explorer's grid: its thumbnail in a 200 × 150 box, the heart in a medallion at its corner, its name below |
| Base folder (*dossier de base*) | The folder the file explorer indexes, with its subfolders; an app setting of the ⚙ menu |
| Index (*index*) | The list of every file under the base folder, cached in `files.index` next to the exe, loaded at start-up and rescanned in the background or with ↻; what the search reads, never the disk |
| Favorite (*favori*) | A file hearted in the file explorer; kept in `favorites.txt` next to the exe, shown — all of them, the newest first — while the search box is empty |
| Last video (*dernière vidéo*) | The MP4 or GIF the last animated Copy generated in the session, its file kept in the temp folder: put back on the clipboard by **Copy last MP4 / GIF**, saved elsewhere by **Save last…**, whatever the grid has become; replaced by the next animated Copy, forgotten at exit. App state, not an effect |
