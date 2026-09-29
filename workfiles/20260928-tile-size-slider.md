# Tile Size Slider

> Working document — a slider at the bottom of the file explorer setting how many columns a tile
> spans, the mouse wheel over the tiles moving it, the size remembered with the column count.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer (`workfiles/20260926-file-explorer.md`) shows its files as **tiles** of
200 × 150 logical px, in **1 to 5 columns** chosen with the `−` / `+` buttons of its header; the
panel is as wide as its columns and the window follows. This work adds a **size slider** at the
bottom of the panel: a tile spans **1 to N columns** — N the column count — so the same panel shows
small tiles or a few large ones. The wheel over the tiles moves the slider; the size is remembered
between sessions, with the column count.

Origin: the user's request *"Miniatures de fichiers, scrollbar en bas pour ajuster la taille"*,
refined on 2026-09-26 into a size **in columns** (see Iteration 1).

Vocabulary of this workfile — see also `GLOSSARY.md`:

| Term | Meaning |
|---|---|
| Column | The unit of the panel's width: 200 logical px of tile plus the 8 px gap; N = 1 to 5, from `−` / `+` |
| Tile size (*taille des tuiles*), `Span` in the code | The number of columns a tile spans, s, a **divisor of N**; from the slider |
| Slots | The tiles per row: N / s |

Components: `UI/FileExplorerPanel.cs` (the header, the rows, the width), `UI/ThumbnailGrid.cs`
(the tiles' layout, the keyboard, the thumbnail cache), `UI/AppSettings.cs` (the registry),
`UI/MainForm.cs` (the start-up values, the saving, the window following the width).

---

## The Size Slider

- **Where**: a **sixth row** of the panel's table (`_content`), **under the list** — the bottom of
  the panel. Disposition B of the mockup shown on 2026-09-26: a **small tile glyph** at the left,
  the **slider** across the width, a **large tile glyph** at the right; **no value readout** — the
  ticks and the thumb say it. The row is 34 logical px tall, sized in `ApplyMetrics()` like the
  status and caption rows, the inner table's single row taking all of it (Iteration 4); it hides
  with the content when the panel is collapsed, and it adds nothing to the width (`ApplyWidth()`
  and `Surround` untouched).
- **Control**: a WinForms `TrackBar`, horizontal, `AutoSize = false`, `TickStyle.Both` — the thumb
  then sits on the row's centre line, level with the glyphs at any DPI (Iteration 4) — one tick per
  position (`TickFrequency = 1`), `SmallChange = LargeChange = 1`. Its positions are the
  **divisors of N** in ascending order — `Minimum = 0`, `Maximum = divisors.Count − 1`, the value
  an index into them; left = 1 column (small tiles), right = N (one tile across the panel):

  | N | Positions (tile size, in columns) |
  |---|---|
  | 1 | 1 — the slider is **disabled** |
  | 2 | 1, 2 |
  | 3 | 1, 3 |
  | 4 | 1, 2, 4 |
  | 5 | 1, 5 |

- **Glyphs**: two labels showing `▭` (U+25AD) from *Segoe UI Symbol*, the left one at the panel's
  font size, the right one 6 pt larger; tooltips *Smaller tiles* / *Larger tiles*. They are
  indicators, not buttons.
- **Tooltips**: the slider's says *Tile size, in columns*; disabled at N = 1, it says *One column:
  a single tile size*.
- **Acting on it**: dragging the thumb, clicking the track, the arrow keys when it has the focus,
  the wheel over it (the `TrackBar`'s own), and the wheel over the tiles (§ Mouse Wheel) — every
  change applies **live**: the grid re-lays its tiles at once, the thumbnails reloading at the new
  size (§ Thumbnails).

---

## Tile Size Rules

- **A divisor of N, always**: the tile size s divides the column count N, so every row is full —
  no half-empty row of 2-column tiles in 3 columns (the user's rule: 3 columns → 1 or 3; 4 → 1, 2
  or 4).
- **`+` (one column more)**: the size goes back to **1** — one tile per column (the user's rule).
- **`−` (one column fewer)**: the size goes back to **1** as well — both buttons behave alike
  (Q&A #7).
- **Start-up**: the column count and the tile size come from the registry (§ Settings); a size that
  does not divide the count (values edited by hand, an older version) falls back to 1.
- **Programmatic column changes** (the start-up value) do not reset the size: only the buttons do.
  The `Columns` setter still forces a size that no longer divides the new count back to 1, so the
  grid is never inconsistent.
- The size is a **panel setting**, not a per-file state: every tile has the same size.

---

## Tile Geometry

In `ThumbnailGrid`:

- A new `Span` property (1 to `Columns`, dividing it; a value that does not is normalised to 1)
  next to `Columns`; `TileWidth`, `TileHeight`, `Gap`, `Inset` stay the **column** constants, and
  `FileExplorerPanel.ApplyWidth()` keeps reading them.
- **Tile size in device px**: `TileW = LogicalToDeviceUnits(TileWidth) × s + GapPx × (s − 1)` — the
  tile covers its columns **and the gaps between them** — and `TileH = TileW × 3 / 4`, the 4:3
  kept. At 100 %: 200 × 150, 408 × 306, 616 × 462, 824 × 618, 1032 × 774.
- **Slots per row**: N / s. `TileBounds(index)` lays index `% slots` and `/ slots`; `CellW`, `CellH`
  and `UpdateExtent()` follow (rows = ⌈count / slots⌉); the name row and the heart medallion keep
  their sizes.
- **Keyboard**: `↑` / `↓` move by one row of `slots` tiles, staying in their slot at the top row;
  `←` / `→` by one tile, as today.
- **On a size change**: `UpdateBox()` then `UpdateExtent()` (the DPI and font precedent), the
  selection kept, and the tile that was at the top of the view stays in view — the scroll position
  set to its new row.

---

## Thumbnails

- **Shell size**: `UpdateBox()` already asks the Shell for `max(256, TileW)` px and scales into the
  box; with the size it asks for the spanned width — up to 1032 px at 100 %, 1548 at 150 % — the
  Shell answering with what it has (`BiggerSizeOk`, like the cells' 1024 px previews).
- **Reload on a size change**: `ThumbnailCache.SetBox` already drops every thumbnail when the box
  changes; the visible tiles then ask for theirs again as they are painted, the most recently
  painted first, so a size change shows empty boxes for a moment, then sharp thumbnails.
- **Memory**: the cache's cap becomes a **byte budget**: `MaxCached` = 64 MB / (box width × box
  height × 4), clamped between 16 and 200 entries — 200 at 200 × 150 (24 MB at most), about 130 at
  408 × 306, 58 at 616 × 462, 32 at 824 × 618, 21 at 1032 × 774; the floor keeps a screenful cached
  at the largest sizes and high DPI (a 1548 × 1161 thumbnail is 7 MB: 16 of them, 115 MB).

---

## Mouse Wheel

- Over the tiles (the cursor in the grid), the wheel **moves the slider**: one position per notch
  (120 units, the deltas of a free-spinning wheel accumulated), **up = larger tiles**, down =
  smaller — the direction of the cells' zoom. At the ends, nothing happens.
- **No modifier**: the wheel **alone** does it (Q&A #6) — the app's convention, the wheel zooming
  the cells. The list then scrolls with its **scrollbar** and the **keyboard**, no longer with the
  wheel.
- Mechanism: `ThumbnailGrid` overrides `OnMouseWheel` without calling the base class; the gesture
  raises a `SizeStepRequested` event with the direction, the panel moves the slider one position,
  which applies the size like any other change.
- The wheel reaches the control **under the cursor** through Windows' *Scroll inactive windows when
  I hover over them* (on by default since Windows 10); with it off, the wheel goes to the focused
  control, like the rest of the app.

---

## Settings

- `AppSettings.ExplorerSpan` (registry `HKCU\Software\ImageGridFusion`, DWORD `ExplorerSpan`, 1 to
  5, default 1) and `SaveExplorerSpan(int)`, on the `ExplorerColumns` pattern: read at start-up,
  clamped, the registry errors swallowed to the default; saved by `MainForm` on the panel's new
  `SpanChanged` event, the errors shown in the status bar like `SaveExplorerColumns`.
- **Saved on every change** — the slider, the wheel, and the automatic reset of `+` / `−` — so the
  next launch restores the last **column count and tile size** together, as the user asked.
- `MainForm` applies the size after the count at start-up:
  `_explorer.Columns = AppSettings.ExplorerColumns; _explorer.Span = AppSettings.ExplorerSpan;` —
  the panel normalising a non-divisor to 1.
- Not an effect: no toolbar, no Reset button; the size belongs to the panel like the column count.

---

## Documentation

- **README** § *File explorer*, the *Tiles* bullet: the size slider at the bottom of the panel (1 to
  N columns, a divisor, `+` / `−` back to one per column, the wheel over the tiles), the size
  remembered with the count.
- **GLOSSARY**: *Tile* revised (its thumbnail in a box of one or more columns, 200 × 150 per
  column), a new *Tile size* entry.

---

## Test Impact

**None.** The repository has **no test project** (`src/` holds `ImageGridFusion` only), and every
previous workfile shipped without unit tests, verified by hand (`workfiles/20260926-file-explorer.md`,
Q&A #13 — the standing choice, kept here). The checks, run at delivery:

| Behaviour | Check |
|---|---|
| Positions | 1 column: slider disabled; 2 → 1, 2; 3 → 1, 3; 4 → 1, 2, 4; 5 → 1, 5 |
| Geometry | 4 columns at size 2: two 408 × 306 tiles per row, the gap between the columns covered; size 4: one 824 × 618 tile per row; the names and hearts in place |
| `+` | 2 columns at size 2 → `+` → 3 columns, size 1 |
| `−` | 4 columns at size 2 → `−` → 3 columns, size 1 |
| Wheel | Over the tiles: one position per notch, up = larger; at the ends nothing; the list scrolls with its scrollbar and the keyboard, not with the wheel |
| Thumbnails | A size change shows the thumbnails again, sharp at the new size; the memory stays under the budget (Task Manager, 5 columns at size 5, scrolling through 100 favorites) |
| Keyboard | `↑` / `↓` move by row at every size; `←` / `→` by tile |
| Start-up | 4 columns at size 2 remembered after a restart; `ExplorerSpan` = 3 with 4 columns → 1 |
| Width | The panel and the window width unchanged by the slider; collapse / expand hide and show it |

Run at delivery — 2026-09-29, by a UI Automation script (`checks.ps1`, in the session's scratchpad,
not in the repository) driving the built app twice, with screenshots of the window: **Positions**,
**`+`**, **`−`**, **Wheel** (up, at the end, down, with one column), **Start-up** (4 columns at
size 2 restored; `ExplorerSpan` 3 with 4 columns → 1) and **Width** (the panel's width identical at
sizes 1, 2 and 4) — 13 checks, all passed, on both builds (Iteration 4). **Geometry** checked on the
screenshots: one favorite, its tile on 2 columns, on 4, on 1, the heart and the name in place. Not
run: **Thumbnails** under the budget with 100 favorites, **Keyboard** by row at every size (one
favorite only on this machine), collapse / expand — the mechanisms are the existing ones.

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
  to 1)?~~ → Back to **1**, like `+` (Q&A #7, Iteration 2)

Added by Iteration 5 (the post-implementation redesign):

- [ ] The panel's width: `−` / `+` kept (in steps, the window following), a **splitter** on the
  panel's edge (continuous, the preview giving way), or a **width slider** in the header
  (continuous, the window following)?
- [ ] The row model: the number per row is **what fits** — ⌊width / nominal size⌋, the tiles
  stretched to fill the row, the bottom slider setting the nominal size — or the slider sets the
  **number per row** directly, the tiles filling the row?
- [ ] A thumbnail smaller than its box: **enlarged to fit** (bands on the other axis) or enlarged
  to **cover** the box (cropped)?
- [ ] The smallest tile size (so the most per row): 100 px, 150 px, or an explicit cap of 5 per row?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-09-29 | `73747c2` the `ExplorerSpan` setting; `2357997` the slider, `ThumbnailGrid.Span`, the wheel, the cache budget, the wiring; `1b5784a` the slider row's fix (Iteration 4). Checked by script, 13 checks, screenshots (§ Test Impact) |
| Unit tests | — | — | Does not apply — no test project, checked by script and screenshots (§ Test Impact) |
| README | 3 | 2026-09-29 | `1515917` — § *File explorer*: the *Tiles* bullet revised, a *Tile size* bullet added; GLOSSARY: *File explorer* and *Tile* revised, *Tile size* added |

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
| 8 | The panel's width control: `−` / `+` in steps, a splitter on the panel's edge, or a width slider in the header (mockup)? | | 2026-09-29 |
| 9 | The row model: the number per row is what fits at the nominal size set by the slider, the tiles stretched to fill — or the slider sets the number per row? | | 2026-09-29 |
| 10 | A thumbnail smaller than its box: enlarged to fit, or to cover (cropped)? | | 2026-09-29 |
| 11 | The smallest tile size: 100 px, 150 px, or a cap of 5 per row? | | 2026-09-29 |

---

*Last updated: 2026-09-29*
