# Explorer Transparency Checkerboard

> Working document — the file explorer's thumbnails drawn over white and grey squares instead of a
> flat grey, so a transparent image (or folder icon) shows its transparency.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer paints every thumbnail box with a flat grey (`SystemColors.ControlLight`) before
drawing the thumbnail. A PNG with an alpha channel, a folder icon from Windows or the drawn folder
glyph then sit on grey, and nothing tells a transparent area from a grey one.

The flat grey becomes a **checkerboard** — white and grey squares, the usual sign of transparency —
over the **whole 4:3 box**, finer than the preview's, its squares scaled to the box.

Components:

| Component | Where the grey / flat fill is today |
|---|---|
| `UI/ThumbnailGrid.PaintPicture` | The tile's box (Thumbnails view) and the Details row's thumbnail — `SystemColors.ControlLight` |
| `UI/ThumbnailGrid.PaintStrip` | Each thumbnail of a folder row's strip (Details view) — `SystemColors.ControlLight` |
| `UI/ThumbnailGrid.PaintLoading` | The Loading… slot — `SystemColors.ControlLight` |
| `UI/UrlZone.UrlPreview.OnPaint` | The URL zone's preview — `g.Clear(SystemColors.Window)` |

Already right, nothing to change: `Imaging/ShellThumbnail` copies Windows' thumbnails **keeping their
alpha channel**, and `ThumbnailCache.Fit` / `UrlZone.Fit` draw into 32-bit ARGB bitmaps — the
transparency reaches the paint, it is only hidden by the grey under it.

**Folders are asked at 256 px.** For some folders, Windows' thumbnail at 384–512 px comes **opaque**,
its transparent areas painted black — the same folder at 256 or 1024 px keeps its alpha. Windows
gives a folder's thumbnail at 256 px whatever the size asked, so `ThumbnailCache` asks a folder —
a path `Directory.Exists` finds — through `ShellThumbnail.TryLoadFolder`, at **256 px**
(`ShellThumbnail.FolderSide`) whatever the tile's bucket: nothing is lost, and the alpha comes with it.

**The black around an opaque folder is cleared.** Should a folder's thumbnail still come fully opaque,
the black **joined to its edges** — pixels near black (each channel ≤ 8), reached from the border
through black pixels only — is made transparent; black inside the picture, behind the folder's outline,
stays. Folders only: a file's opaque thumbnail (a JPEG with black borders) is its real content.

---

## Where the Checkerboard Goes

Every thumbnail box of the file explorer:

| Box | Checkerboard |
|---|---|
| A file's tile (Thumbnails view) | ✅ whole 4:3 box, bands around the image included |
| A folder's tile — Windows' thumbnail **or** the drawn folder glyph | ✅ whole box, behind the folder |
| A Details row's thumbnail (file or folder) | ✅ whole box |
| A folder row's strip thumbnails (Details view) | ✅ each strip box |
| The Loading… slot (both views) | ✅ whole slot, "Loading…" text over it |
| The URL zone's preview | ✅ whole preview, in every state — empty (🔗), failed (⚠), busy, ready |
| A tile whose thumbnail is not loaded yet, or has none | ✅ the box shows the checkerboard alone |

The borders, the heart and bulb medallions, the selection and hover outlines, the bulb's arrow, the
spinner's veil and the play badge are drawn over it as today.

---

## The Pattern

- **Colors**: white and the preview's grey, `(204, 204, 204)` (`GridPreview.CheckerGrey`) — one
  definition for both.
- **Finer, scaled to the box**: the square's side follows the box it fills, so a tile shows about the
  same number of squares whatever the tile size — proposed: **the box's height / 16, at least 3 device
  px** (a 75 px high tile → ~5 px squares, a 750 px one → ~47 px): 16 rows of squares in every box.
- **Sharp squares**: filled with the nearest texel, sampled at the pixels' centers, whatever the
  caller's interpolation — the explorer draws in high-quality bicubic.
- **Anchored on the box's top-left corner**: each box starts with a white square, so the pattern does
  not slide with the scroll and every tile looks the same.
- **One helper**, shared by the explorer and the URL zone: `UI/Checkerboard.cs` —
  `Checkerboard.Fill(Graphics g, Rectangle box)` computing the square from the box and filling it,
  its `TextureBrush` cached per square size (a handful of sizes in use: tiles, Details thumbnails,
  strips, the Loading… slot, the URL preview), kept for the app's life — the brush holds its own copy
  of the tile — the cache emptied past 32 sizes, as the tile size slider moves.
- The preview's own checkerboard (`GridPreview.PaintCheckerboard`, 8 logical px, anchored on the
  canvas, cut by the Twitter corners) is **not changed** — only its grey is shared:
  `Checkerboard.Grey`, read by the preview and the eyedropper's loupe.

---

## Documentation

- `GLOSSARY.md` — the *Tile* row: its thumbnail drawn over white and grey squares showing its
  transparency.
- `GLOSSARY.fr.md` — the same row, in French, in the same commit.
- README: untouched by the checkerboard (it does not describe the tiles' background); its folder view
  paragraph (EN + FR) follows § Folder Tiles, with the glossary's *Folder tile* row (EN + FR).

---

## Folder Tiles

Origin: Iterations 7, 8 and 9, asked after the checkerboard was delivered.

- **The folder fills its tile**: Windows' folder thumbnail is **trimmed to its visible part** — the
  pixels whose alpha is above 32, the faint shadow left out — in `ShellThumbnail.TryLoadFolder`, after the
  black around is cleared; the trimmed picture is then scaled to the tile like any thumbnail, its
  proportions kept, so the folder takes the tile's full height. The **drawn folder** (no Windows
  thumbnail) grows the same way: 90 % of the tile, at its 4:3 shape.
- **The folder's name in bold on its front flap** (Thumbnails view): the folder's own name — not the
  file count — centered on the lower part of the folder, its yellow front flap (from 58 % to 96 % of
  the picture's height, 8 % in from each side; the drawn folder's front panel), on **two lines at
  most**, cut with `…`. Bold, in the panel's font, drawn as a path (`ThumbnailGrid.PaintFolderName`),
  its size a **third of the flap's height**, never under the tile names' size — **almost black over a
  white halo** — legible on the yellow as on a photo.
- **Under the tile, the file count alone** — `140 files`, `1 file` — in place of `name (140)`: the name
  is on the folder. Until the index gives the count, the name stays there. The Details view keeps
  `name (140)` on its row. `ExplorerRow` carries the count apart (`Count`), its `Name` the bare name.
- Thumbnails view only: a Details row's thumbnail is a quarter of the tile and its name is next to it.

---

## Test Impact

No unit test project exists in the repository (`src/` holds the app only), and the change is pure
painting: **no test created or updated**. Checked by hand on a transparent PNG, a folder with a
Windows thumbnail, a folder without one, the Details view, the Loading… slot and the URL zone.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; painting only) | — | — |

---

## Open Questions

- [x] ~~Square size: the box's height / 16 with a 3 px minimum, or another factor / a fixed small size?~~ → Height / 16, at least 3 px
- [x] ~~URL zone preview with no image — empty (🔗) and failed (⚠) states: checkerboard too, or plain?~~ → Checkerboard too, in every state
- [x] ~~Documentation: add the checkerboard to the glossary's *Tile* row (EN + FR)?~~ → Yes, EN + FR

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-22

Scope set with the user (Q&A 1–4): the checkerboard replaces the grey in the tiles, the Details view
(row thumbnails and folder strips), the URL zone preview and the Loading… slot; it covers the whole
4:3 box, bands included; its squares are finer than the preview's, scaled to the box. The subject is
straightforward: one scout pass, done directly.

The exploration found the four fills to replace (`PaintPicture`, `PaintStrip`, `PaintLoading`,
`UrlPreview.OnPaint`) and that the thumbnails already keep their alpha (`ShellThumbnail`). Proposed:
one shared `Checkerboard` helper, white + the preview's grey, square = box height / 16 (min 3 px),
anchored per box.

### Iteration 2 — 2026-10-22

Open questions settled (Q&A 5–7): squares at the box's height / 16, at least 3 px; the URL preview
shows the checkerboard in every state, empty and failed included; the glossary's *Tile* row (EN + FR)
mentions the checkerboard. No open question left.

### Iteration 3 — 2026-10-22 — ✅ Implemented

Go given (Q&A 8): code, tests and documentation, in the current checkout. Branch Gate: stays on
`main`, the standing choice for this repository.

### Iteration 4 — 2026-10-22 — 🧭 Implementation choices

- **Rows of squares**: height / 16 gives **16 rows** per box, not ~12 as the design text and the
  question's description said — a miscount; the formula chosen by the user is kept as is.
- **Sharp squares**: `Checkerboard.Fill` sets nearest-neighbour interpolation and the half pixel
  offset for its fill, then restores the caller's modes — the explorer paints in high-quality
  bicubic, which would blur the squares' edges.
- **Brush lifetime**: the brushes live in a static cache for the app's life, not disposed with a tile
  bitmap (the brush copies it, the bitmap is disposed at once); the cache is emptied past 32 sizes.
- **Shared grey**: `GridPreview.CheckerGrey` removed; the preview and the eyedropper's loupe read
  `Checkerboard.Grey`.
- **Commits**: the helper (with the shared grey), then its four uses, then the glossary — three
  commits.

No rule broken.

### Iteration 5 — 2026-10-22 — ⚙️ Post-implementation — Folders with a black background

Reported by the user (screenshot, Q&A 9): some folder tiles show a black square behind the folder
instead of the checkerboard. Probed with Windows' `IShellItemImageFactory`: for those folders, the
thumbnail asked at 384 or 512 px comes back 256 × 256 and **fully opaque**, black where the folder is
transparent; asked at 256 or 1024 px, the same folder keeps its alpha. The other folders keep it at
every size. The tiles were at the 512 bucket, hence the black. Decided (Q&A 10): folders are always
asked at 256 px — their real size — whatever the tile's bucket.

### Iteration 6 — 2026-10-22 — ⚙️ Post-implementation — Black around a folder cleared

Asked by the user while Iteration 5 was being coded: black detected around the thumbnail is removed.
Applied to folders whose thumbnail comes fully opaque (the case Windows lost the alpha): the near-black
pixels joined to the edges become transparent, a flood from the border; the black inside the folder's
picture stays. Files left alone — their black borders are content.

### Iteration 7 — 2026-10-22 — ⚙️ Post-implementation — Bigger folder icons

Asked by the user once the folder fix was tested (Q&A 12): the folder icons have too much margin
around them. Decided (Q&A 13): Windows' thumbnail is trimmed to its visible part, then fitted to the
tile; the drawn folder grows the same way. See § Folder Tiles.

### Iteration 8 — 2026-10-22 — ⚙️ Post-implementation — Folder name in bold on the folder

Asked in the same message: the folder's name drawn in bold over the folder. Decided (Q&A 14–16): on the
front flap, two lines at most; almost black over a white halo; the name and count kept under the tile.
See § Folder Tiles.

### Iteration 9 — 2026-10-22 — ⚙️ Post-implementation — File count under a folder tile

Asked by the user while Iterations 7–8 were being coded, reversing Q&A 16: under a folder's tile, the
file count replaces the folder's name. The name is on the folder itself (Iteration 8); the Details
view keeps both on its row.

### Iteration 10 — 2026-10-22 — 🧭 Implementation choices

For Iterations 7–9:

- **Visible part**: alpha 32 or more (`ShellThumbnail.VisibleAlpha`), so the folder's faint shadow does
  not keep a margin; a thumbnail with nothing visible is kept whole.
- **Name size**: a third of the flap's height, not half per line as § Folder Tiles first said — two
  lines then fill about 80 % of the flap and a long name wraps instead of shrinking.
- **Flap of Windows' folder**: from 58 % to 96 % of the trimmed picture's height, 8 % in from each side
  — measured on the Windows 11 folder; a folder with a custom icon gets its name at the same place.
- **Drawn folder**: 90 % of the tile's height at 4:3, or 90 % of its width when narrower; its name on
  its front panel, 8 % in from each side and 5 % from top and bottom.
- **Count**: `ExplorerRow.Count` added, `Name` holding the bare folder name; the Details row reads
  `NameAndCount`, unchanged on screen. Before the index counts, the name stays under the tile.
- **Docs**: the README's folder view paragraph (EN + FR) and the glossary's *Folder tile* row (EN + FR)
  described "name then count": updated.
- **Not checked on screen by the agent**: the checking launch opened in a folder without subfolders,
  and going up would have changed the remembered open folder.

No rule broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-22 | `UI/Checkerboard.cs`; `ThumbnailGrid` (tiles, Details thumbnails, strips, Loading…), `UrlZone.UrlPreview`; built, 0 warning |
| Unit tests | 3 | 2026-10-22 | Not applicable — no test project, painting only |
| Code — folders at 256 px, black around cleared | 5, 6 | 2026-10-22 | `ShellThumbnail.TryLoadFolder` / `ClearOuterBlack`, called by `ThumbnailCache`; built, 0 warning |
| Code — folders trimmed, name on the flap, count below | 7, 8, 9 | 2026-10-22 | `ShellThumbnail.Trim`; `ThumbnailGrid.PaintFolderName` / `FolderFlap`, `PaintFolderGlyph` grown; `ExplorerRow.Count`; built, 0 warning |
| Glossary (EN + FR) | 3, 9 | 2026-10-22 | *Tile* row; *Folder tile* row |
| README | 9 | 2026-10-22 | Folder view paragraph (EN + FR) — the checkerboard itself needed none |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the checkerboard replace the grey? | Tiles (Thumbnails view), Details view, URL zone preview, Loading… slot — all four | 2026-10-22 |
| 2 | What surface of the tile does it cover — the image's rectangle only, or the whole 4:3 box? | The whole 4:3 box | 2026-10-22 |
| 3 | Which pattern — the preview's, or finer, scaled to the tile? | Finer, scaled to the tile | 2026-10-22 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-22 |
| 5 | Square size factor? | Height / 16, at least 3 px | 2026-10-22 |
| 6 | URL preview's empty / failed states: checkerboard or plain? | Checkerboard too | 2026-10-22 |
| 7 | Documentation: glossary *Tile* row updated? | Yes, EN + FR | 2026-10-22 |
| 8 | Go for implementation? (scope, where) | Code, tests and documentation — current checkout (`main`) | 2026-10-22 |
| 9 | Task finished? | Not yet — some folders have a black background (screenshot) | 2026-10-22 |
| 10 | How to fix the opaque folder thumbnails — ask another size on an opaque one, always 256 for folders, or nothing? | Folders always asked at 256 px | 2026-10-22 |
| 11 | (user's request) | And black detected around the thumbnail is removed | 2026-10-22 |
| 12 | Task finished? | "Not bad" — the folder icons have too much margin; draw the folder's name in bold over it | 2026-10-22 |
| 13 | How to enlarge the folder icon — trim the empty margin, or a fixed zoom? | Trim the empty margin | 2026-10-22 |
| 14 | Where to draw the bold name — front flap, center, bottom band? | On the front flap | 2026-10-22 |
| 15 | Which style — dark over a light halo, white over a black one? | Dark over a light halo | 2026-10-22 |
| 16 | The name under the tile — kept, or only the count? | Kept | 2026-10-22 |
| 17 | (user's request) | Under the tile, show the file count in place of the folder's name | 2026-10-22 |

---

*Last updated: 2026-10-22*
