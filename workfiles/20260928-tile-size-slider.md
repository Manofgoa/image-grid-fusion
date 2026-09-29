# Tile Size Slider

> Working document — a slider at the bottom of the file explorer setting the tile size, the tiles
> filling each row, the panel's width dragged from its edge, the mouse wheel over the tiles moving
> the slider, the thumbnails enlarged to fit, the size and the width remembered.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer (`workfiles/20260926-file-explorer.md`) shows its files as **tiles** — a
thumbnail, the heart in a medallion, the name below. This work, first delivered on 2026-09-29 as a
size **in columns** (Iterations 1–4, superseded), now gives the panel:

- a **size slider** at the bottom, setting the **tile size** — 100 to 1000 logical px;
- rows that are **always full**: as many tiles per row as fit at that size, stretched to the row's
  width, so no space is lost — widening the panel grows the tiles until one more fits, when they
  all shrink at once, as Windows' thumbnail view roughly does;
- a **splitter** on the panel's left edge, setting the panel's **width** in place of the `−` / `+`
  columns;
- thumbnails **enlarged to fit** their tile when the Shell's are smaller;
- the wheel over the tiles moving the slider; the size and the width remembered between sessions.

Origin: the user's request *"Miniatures de fichiers, scrollbar en bas pour ajuster la taille"*,
refined on 2026-09-26 into a size in columns (Iteration 1), then redesigned on 2026-09-29 after
the first delivery (Iterations 5–6).

Vocabulary of this workfile — see also `GLOSSARY.md`:

| Term | Meaning |
|---|---|
| Tile size (*taille des tuiles*), `TileSize` in the code | The nominal width of a tile, 100 to 1000 logical px, from the slider: the threshold at which one more tile fits on a row |
| Per row (*par ligne*), `PerRow` in the code | How many tiles a row holds: what fits at the tile size, one at least |
| Drawn width | A row's width shared by its tiles, stretched to fill it: never narrower than the tile size, less than one tile size wider |
| Bucket | The size the thumbnails are loaded at from the Shell — 256, 512 or 1024 px, the smallest not below the drawn width |

Components: `UI/FileExplorerPanel.cs` (the header, the rows, the open width), `UI/ThumbnailGrid.cs`
(the tiles' layout, the keyboard, the thumbnail cache, the painting), `UI/AppSettings.cs` (the
registry), `UI/MainForm.cs` (the splitter, the start-up values, the saving).

---

## The Panel's Width

- A **splitter** on the panel's left edge: a WinForms `Splitter` docked right in `MainForm`, added
  to the controls just before the panel so it docks against its edge, 6 logical px wide, the
  `VSplit` cursor. Dragging it resizes the panel, the **preview giving way**; the window does not
  move. Bounds: the panel at least **140** logical px (a 100 px tile and its surround), the preview
  at least **320** (`MinSize` / `MinExtra`, in device px).
- The panel keeps its **open width** in logical px (`OpenWidth`): taken from its actual width when
  the splitter moves — and when the DPI scales it — and applied again when the panel reopens.
  Collapsed, the panel is its 20 px strip and the splitter is **hidden**. Default: 240 px — one
  200 px tile, the look of the first version's single column.
- The header loses the `−` / `+` buttons and the count: *Files* and `»` only. `ColumnsChanged` and
  `FollowExplorerWidth` go; a `WidthChanged` event is raised when the splitter stops, and `MainForm`
  saves the width (§ Settings).

---

## The Size Slider

- **Where**: a **sixth row** of the panel's table (`_content`), **under the list** — the bottom of
  the panel. Disposition B of the mockup shown on 2026-09-26: a **small tile glyph** at the left,
  the **slider** across the width, a **large tile glyph** at the right; **no value readout**. The
  row is 34 logical px tall, sized in `ApplyMetrics()` like the status and caption rows, the inner
  table's single row taking all of it (Iteration 4); it hides with the content when the panel is
  collapsed.
- **Control**: a WinForms `TrackBar`, horizontal, `AutoSize = false`, **100 to 1000** (logical px),
  `TickStyle.None` — the thumb on the row's centre line, level with the glyphs at any DPI —
  `SmallChange = 20`, `LargeChange = 100`; always enabled. Left = small tiles, right = one wide tile
  per row on any panel.
- **Glyphs**: two labels showing `▭` (U+25AD) from *Segoe UI Symbol*, the left one at the panel's
  font size, the right one 6 pt larger; tooltips *Smaller tiles* / *Larger tiles*. They are
  indicators, not buttons.
- **Tooltip**: the slider's says *Tile size: 200 px*, kept up to date.
- **Acting on it**: dragging the thumb, clicking the track, the arrow keys when it has the focus,
  the wheel over it (the `TrackBar`'s own), and the wheel over the tiles (§ Mouse Wheel) — every
  change applies **live**: the grid re-lays its tiles at once; the thumbnails are not reloaded
  unless the bucket changes (§ Thumbnails).

---

## Filling the Rows

In `ThumbnailGrid`, a `TileSize` property (logical px, clamped to 100..1000) replaces `Columns` and
`Span`; the layout is computed from it and the grid's width:

- **W** = the grid's client width minus the two insets — the vertical scrollbar, when shown, is
  already out of the client width; the layout follows `OnClientSizeChanged`, so the splitter and
  the scrollbar's coming and going re-lay the tiles (stable: a scrollbar only appears when the
  content does not fit, and content that fits with it fits without).
- **Per row** n = max(1, ⌊(W + gap) / (size + gap)⌋), size the tile size in device px.
- **Drawn width** = ⌊(W − (n − 1) × gap) / n⌋, the remainder (under n px) left at the right;
  **height** = width × 3 / 4, the 4:3 kept; the name row and the heart medallion keep their sizes.
- **Never from the number of files**: n and the drawn width come from the size and the width only.
  A single result, or a last row with one tile, gets the width it would have in a full row — never
  the whole row (the user's precision, Iteration 6).
- Hence: widening the panel grows the tiles continuously until one more fits, when they all shrink
  at once; a smaller tile size adds a tile per row as soon as one fits, the tiles shrinking at
  once, and between two thresholds changes nothing — the size is a threshold, the rows are always
  full.
- `TileBounds(index)` lays index `% n` and `/ n`; `CellW`, `CellH`, `IndexAt` and `UpdateExtent()`
  follow (rows = ⌈count / n⌉).
- **Keyboard**: `↑` / `↓` move by one row of n tiles, staying in their column at the top row;
  `←` / `→` by one tile.
- **On a relayout** (size or width): `UpdateBox()` then `UpdateExtent()`, the selection kept, and
  the tile that was at the top of the view stays in view — the scroll position set to its new row.

---

## Thumbnails

- **Buckets**: `UpdateBox()` asks the Shell for **256, 512 or 1024 px** — the smallest not below
  the drawn width in device px, 1024 beyond — and the cache box is that square's 4:3
  (side × side · 3 / 4); `ThumbnailCache.SetBox` clears the cache only when the bucket changes, so
  dragging the slider or the splitter within a bucket reloads nothing. The Shell answers with what
  it has (`BiggerSizeOk`), often less than asked for a video.
- **Enlarged to fit**: `PaintTile` draws the cached thumbnail **scaled to the tile** — up or down,
  proportions kept, centred, bands on the other axis (a 16:9 video in a 4:3 tile: bands above and
  below) — with bicubic interpolation; the cache keeps the Shell's pixels as they came (`Fit` never
  enlarges), so no enlarged bitmap sits in memory.
- **Memory**: the cache's cap is a **byte budget**: `MaxCached` = 64 MB / (box width × box height
  × 4), clamped between 16 and 200 entries — 200 at 256, about 85 at 512, 21 at 1024.

---

## Mouse Wheel

- Over the tiles (the cursor in the grid), the wheel **moves the slider**: one notch **multiplies
  the tile size by 1.15** (up = larger tiles) or divides it (down), rounded, clamped to 100..1000
  — about 17 notches from end to end, the same feel at every size. The deltas of a free-spinning
  wheel are accumulated, a notch being 120 units.
- **No modifier**: the wheel **alone** does it (Q&A #6) — the app's convention, the wheel zooming
  the cells. The list scrolls with its **scrollbar** and the **keyboard**, not with the wheel.
- Mechanism: `ThumbnailGrid` overrides `OnMouseWheel` without calling the base class; the gesture
  raises a `SizeStepRequested` event with the direction, the panel sets the new size on the
  slider, which applies it like any other change.
- The wheel reaches the control **under the cursor** through Windows' *Scroll inactive windows when
  I hover over them* (on by default since Windows 10); with it off, the wheel goes to the focused
  control, like the rest of the app.

---

## Settings

- `AppSettings.ExplorerWidth` (registry `HKCU\Software\ImageGridFusion`, DWORD, logical px, 140 at
  least, default 240) and `SaveExplorerWidth(int)`: saved when the splitter stops (`WidthChanged`).
- `AppSettings.ExplorerTileSize` (DWORD, 100 to 1000, default 200) and `SaveExplorerTileSize(int)`:
  saved on **every change** — the slider, the wheel (`TileSizeChanged`).
- Both on the `ExplorerColumns` pattern: read at start-up, clamped, the registry errors swallowed
  to the default; saved by `MainForm`, the errors shown in the status bar.
- `ExplorerColumns` and `ExplorerSpan` are **no longer read nor written**; their values may stay in
  the registry, harmless. `MainForm` applies the width and the size at start-up:
  `_explorer.OpenWidth = AppSettings.ExplorerWidth; _explorer.TileSize = AppSettings.ExplorerTileSize;`.
- Not an effect: no toolbar, no Reset button; the size and the width belong to the panel.

---

## Documentation

- **README** § *File explorer*: the *Tiles* bullet — the panel's width dragged from its left edge,
  the preview giving way, the width remembered; the *Tile size* bullet — the slider from 100 to
  1000 px, the rows always full (as many tiles as fit, stretched to the row), the thumbnails
  enlarged to fit, the wheel over the tiles.
- **GLOSSARY**: *File explorer* (as many tiles per row as fit at the tile size), *Tile* (its
  thumbnail enlarged or reduced to its box), *Tile size* (the nominal width from the slider, the
  threshold at which one more tile fits) revised; the columns wording gone.

---

## Test Impact

**None.** The repository has **no test project** (`src/` holds `ImageGridFusion` only), and every
previous workfile shipped without unit tests, verified by hand (`workfiles/20260926-file-explorer.md`,
Q&A #13 — the standing choice, kept here). The checks of the redesign, run at delivery:

| Behaviour | Check |
|---|---|
| Rows full | Panel 300 px wide, size 100: 2 per row of about 146 px; size 200: 1 per row of 300 px; the tiles reach the row's right edge |
| Threshold | Size 200, the panel dragged from 300 to 420 px: one 300 → 400 px tile per row, then two of about 206 px at once |
| Single file | One favorite only, size 100 in a 300 px panel: its tile is about 146 px (the width of a full row of two), not 300 |
| Splitter | The panel resizes from its left edge, the preview giving way; not below 140 px, the preview keeping 320; the width remembered after a restart; hidden while the panel is collapsed |
| Slider | 100 to 1000 live; the wheel over the tiles: ×1.15 per notch up, ÷1.15 down, 100 and 1000 the ends; the list scrolling with its scrollbar only |
| Enlarged | A video's small Shell thumbnail fills its 1000 px tile, bands above and below, sharp enough |
| Buckets | Tile 300 px → the Shell asked for 512; 700 → 1024; the cache cleared at a bucket change only (no reload while dragging within one) |
| Keyboard | `↑` / `↓` move by row at every size; `←` / `→` by tile |
| Start-up | Width 420 and size 150 remembered; `ExplorerWidth` 50 → 140, `ExplorerTileSize` 5000 → 1000 |
| Collapse | `»` gives the 20 px strip and hides the splitter; `«` restores the open width |

The first delivery (the size in columns) was checked on 2026-09-29 by a UI Automation script
(`checks.ps1`, in the session's scratchpad, not in the repository) driving the built app twice,
with screenshots: 13 checks — positions, `+`, `−`, the wheel, start-up, the panel's width — all
passed (Iteration 4).

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~The wheel over the tiles moves the slider: the wheel **alone** (the list scrolling with its
  scrollbar only), or **`Ctrl` + wheel** (the wheel alone keeping the list's scroll, Explorer's
  convention)?~~ → The wheel **alone**; the list scrolls with its scrollbar and the keyboard
  (Q&A #6, Iteration 2)
- [x] ~~`−` (one column fewer): the tile size back to **1** like `+`, **kept when it still divides**
  the new count (4 columns at size 2 → 2 columns: kept; 3 at size 3 → 2: back to 1), or the
  **full width kept** (a size equal to the count stays equal to the new count, the others back
  to 1)?~~ → Back to **1**, like `+` (Q&A #7, Iteration 2) *(moot since Iteration 6: no columns)*

Added by Iteration 5 (the post-implementation redesign):

- [x] ~~The panel's width: `−` / `+` kept (in steps, the window following), a **splitter** on the
  panel's edge (continuous, the preview giving way), or a **width slider** in the header
  (continuous, the window following)?~~ → The **splitter** (Q&A #8, Iteration 6)
- [x] ~~The row model: the number per row is **what fits** — ⌊width / nominal size⌋, the tiles
  stretched to fill the row, the bottom slider setting the nominal size — or the slider sets the
  **number per row** directly, the tiles filling the row?~~ → **What fits**, the tiles stretched
  (Q&A #9, Iteration 6)
- [x] ~~A thumbnail smaller than its box: **enlarged to fit** (bands on the other axis) or enlarged
  to **cover** the box (cropped)?~~ → **Enlarged to fit** (Q&A #10, Iteration 6)
- [x] ~~The smallest tile size (so the most per row): 100 px, 150 px, or an explicit cap of 5 per
  row?~~ → **100 px**, the slider from 100 to 1000 (Q&A #11, Iteration 6)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-28

Scoping on 2026-09-26, before any exploration (Q&A #1–#5): a mockup of four dispositions of the
bottom row was shown — A a real scrollbar, B a slider between a small and a large tile icon, C a
slider with the size read beside it, D a slider between `−` / `+` steppers. The user chose **B**,
the size remembered in the registry, a straightforward exploration; the first answers (a free
size from 100 × 75 to 400 × 300, the columns kept) were **redefined** by the user right after, in
four messages that this design follows:

1. the slider sets **how many columns a tile occupies**, from 1 to the column count; `+` / `−`
   keep setting the columns; raising the count goes back to one tile per column; the last count
   **and** size are remembered for the next launch;
2. odd counts: 3 columns allow 1 or 3, never 2;
3. 4 columns allow 1, 2 or 4 — the size is a **divisor** of the count;
4. the mouse wheel with the cursor over the files moves the size slider.

Exploration: one read-only agent on the grid and the panel (the `Columns` chain, the table of
rows, `TileBounds`, the keyboard, `UpdateBox` / `UpdateExtent`, the `ExplorerColumns` pattern,
`FollowExplorerWidth` — untouched, the width not changing); the thumbnails read directly after the
app's quit cut the second agent (`ThumbnailCache`: keyed by path, 200 entries, dropped by
`SetBox`; the Shell asked for `max(256, TileW)`; no test project).

Design: the sections above — the divisors as the slider's positions, the tile covering its columns
and their gaps at 4:3, slots = N / s for the layout and the keyboard, the thumbnails reloaded at
the new size under a byte budget, the wheel as one position per notch, `ExplorerSpan` saved on
every change. Two points stay open: the wheel's modifier and what `−` does to the size.

### Iteration 2 — 2026-09-28 — Open questions settled

The two questions, asked twice through the multiple-choice tool (both asks cancelled), answered in
the chat: the wheel **alone** moves the slider over the tiles (« Oui M », then « Oui » on the
reading *oui, molette seule*), and `−` brings the size back to **1** like `+` (the proposal,
accepted with the same « Oui »). § Tile Size Rules, § Mouse Wheel, § Documentation and the checks
of § Test Impact updated; no open question left.

### Iteration 3 — 2026-09-28 — ✅ Implemented

Go given for the code and the documentation (*Implémenter code et documentation*), the unit tests
not applying (no test project, § Test Impact). The run stays on `main`, the standing choice of
this app — no branch question. The scope is the sections above as they stand at the go.

### Iteration 4 — 2026-09-29 — 🧭 Implementation choices

Delivered in three code commits (the `ExplorerSpan` setting; the slider, the grid and the wiring;
the slider row's fix), one for the README and the glossary, and the workfile's own. **No rule
broken.** The choices the design left open, or that the run took:

- **Ticks on both sides** (`TickStyle.Both`) instead of `TickStyle.BottomRight`: with the ticks
  below only, the trackbar draws its thumb in its upper part, and the glyphs — centred in the row —
  sat lower; with ticks on both sides the thumb is a rectangle on the row's centre line, level with
  the glyphs at any DPI. Found on the first screenshots, with a second cause fixed at the same
  time: the row's inner table had no row style, so its single row took the trackbar's preferred
  height (70 px at 150 %) and overflowed the cell — it now takes the cell's height.
- **Row height** 34 logical px (the design said *about 30*), the ticks needing the room.
- **Glyphs**: `▭` from *Segoe UI Symbol* at the panel's font size on the left, 6 pt larger on the
  right.
- **Tile height** `TileW × 3 / 4` as designed: at 125 % or 150 % DPI a one-column tile is 1 px
  shorter than before (187 instead of 188 px at 125 %), the 4:3 kept.
- **Wheel**: several notches in one message give as many steps, raised one by one, the slider
  saturating at its ends; the wheel over the slider itself is the `TrackBar`'s own.
- **Checks by script** rather than by hand (§ Test Impact), the registry values the script touches
  restored afterwards; the UI Automation tree exposes the WinForms `TrackBar` without its range
  pattern, so the script reads its position and range from the control itself.

### Iteration 5 — 2026-09-29 — ⚙️ Post-implementation — Tiles filling the row, a continuous size, the thumbnails enlarged

Asked right after the delivery, with a screenshot of 5 columns at size 5: a video's Shell thumbnail
(about 640 px wide) lost in the middle of a 1032 × 774 box. The request: smaller tile sizes and
more columns at most — or a slider for the side area's width in place of `−` / `+` — and, above
all, **1 to 5 (or more) tiles per row without wasted space**: the tile size varying continuously
as the area grows, then shrinking at once when one more tile fits on the row, as Windows' thumbnail
view roughly does; and the thumbnail **enlarged to fill its box** when the box is larger than what
the Shell gives. The design is being redrawn from these; the width control, the row-filling model,
the enlargement and the smallest size are asked (Q&A #8–#11); the code is untouched until the go.

### Iteration 6 — 2026-09-29 — ⚙️ Post-implementation — The redesign settled

A mockup of three width controls (A `−` / `+` in steps, B a splitter on the panel's edge, C a
width slider in the header) with a strip showing the rows filling as the panel widens; the user
chose **B**, confirmed the **what-fits** model with the tiles stretched to the row, the thumbnails
**enlarged to fit**, and **100 px** as the smallest size (Q&A #8–#11). The design sections were
rewritten for it — § The Panel's Width, § The Size Slider, § Filling the Rows, § Thumbnails,
§ Mouse Wheel, § Settings, § Documentation, § Test Impact — with these points decided here:

- the slider spans **100 to 1000 px**, `TickStyle.None`, a tooltip with the value;
- the thumbnails are loaded by **buckets** (256 / 512 / 1024 px) and scaled to the tile at paint
  time, so dragging never reloads within a bucket, and no enlarged bitmap is kept;
- the wheel changes the size by **15 % per notch**, the same feel at every size;
- the panel's bounds: **140 px** at least, the preview keeping **320**; defaults **240 px** wide
  and **200 px** tiles, the look of the first version;
- `ExplorerColumns` and `ExplorerSpan` are retired, `ExplorerWidth` and `ExplorerTileSize` take
  their place; `FollowExplorerWidth` goes with the `−` / `+` buttons.

Right after, the user's precision: a single result must not get the whole row — it gets the width
it would have in a full row. The model already does so (n never depends on the number of files);
§ Filling the Rows says it, and § Test Impact checks it.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-09-29 | `73747c2` the `ExplorerSpan` setting; `2357997` the slider, `ThumbnailGrid.Span`, the wheel, the cache budget, the wiring; `1b5784a` the slider row's fix (Iteration 4). Checked by script, 13 checks, screenshots. The redesign of Iteration 6: pending its go |
| Unit tests | — | — | Does not apply — no test project, checked by script and screenshots (§ Test Impact) |
| README | 3 | 2026-09-29 | `1515917` — § *File explorer*: the *Tiles* bullet revised, a *Tile size* bullet added; GLOSSARY: *File explorer* and *Tile* revised, *Tile size* added. To revise with the redesign |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does the bottom scrollbar set: the tile size with `−` / `+` kept for the columns, the tile size with the columns deduced from the width, or the columns themselves (replacing `−` / `+`)? | The tile size, `−` / `+` kept — then refined by the user: the size **in columns**, a divisor of the count (Iteration 1) | 2026-09-26 |
| 2 | Which disposition for the bottom row (mockup): A a real scrollbar, B a slider between two tile icons, C a slider with the size read beside it, D a slider between `−` / `+` steppers? | B — slider + icons | 2026-09-26 |
| 3 | Which size range, 4:3 kept: 100 × 75 – 400 × 300, 80 × 60 – 320 × 240, or 150 × 113 – 600 × 450? | 100 × 75 – 400 × 300 — superseded by the size in columns, 1 to N (Iteration 1) | 2026-09-26 |
| 4 | Is the chosen size remembered between sessions? | Yes, in the registry — the count and the size together (the user) | 2026-09-26 |
| 5 | Exploration depth: straightforward, or tricky / long? | Straightforward | 2026-09-26 |
| 6 | The wheel over the tiles: alone, or `Ctrl` + wheel? | The wheel **alone** — « Oui M », confirmed by « Oui » (Iteration 2) | 2026-09-28 |
| 7 | `−`: the size back to 1, kept if it still divides, or the full width kept? | Back to **1**, like `+` — the proposal, accepted (Iteration 2) | 2026-09-28 |
| 8 | The panel's width control: `−` / `+` in steps, a splitter on the panel's edge, or a width slider in the header (mockup)? | B — the **splitter**, the preview giving way (Iteration 6) | 2026-09-29 |
| 9 | The row model: the number per row is what fits at the nominal size set by the slider, the tiles stretched to fill — or the slider sets the number per row? | **What fits**, the tiles stretched to fill the row (Iteration 6) | 2026-09-29 |
| 10 | A thumbnail smaller than its box: enlarged to fit, or to cover (cropped)? | **Enlarged to fit**, bands on the other axis (Iteration 6) | 2026-09-29 |
| 11 | The smallest tile size: 100 px, 150 px, or a cap of 5 per row? | **100 px**, the slider from 100 to 1000 (Iteration 6) | 2026-09-29 |

---

*Last updated: 2026-09-29*
