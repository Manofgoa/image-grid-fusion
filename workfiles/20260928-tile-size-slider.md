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
  ticks and the thumb say it. The row is about 30 logical px tall, sized in `ApplyMetrics()` like
  the status and caption rows; it hides with the content when the panel is collapsed, and it adds
  nothing to the width (`ApplyWidth()` and `Surround` untouched).
- **Control**: a WinForms `TrackBar`, horizontal, `AutoSize = false`, `TickStyle.BottomRight`, one
  tick per position (`TickFrequency = 1`), `SmallChange = LargeChange = 1`. Its positions are the
  **divisors of N** in ascending order — `Minimum = 0`, `Maximum = divisors.Count − 1`, the value
  an index into them; left = 1 column (small tiles), right = N (one tile across the panel):

  | N | Positions (tile size, in columns) |
  |---|---|
  | 1 | 1 — the slider is **disabled** |
  | 2 | 1, 2 |
  | 3 | 1, 3 |
  | 4 | 1, 2, 4 |
  | 5 | 1, 5 |

- **Glyphs**: two labels showing `▭` (U+25AD) from *Segoe UI Symbol*, the left one small, the right
  one larger; tooltips *Smaller tiles* / *Larger tiles*. They are indicators, not buttons.
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
- **`−` (one column fewer)**: **open question** (§ Open Questions); the proposal is back to **1** as
  well, so both buttons behave alike.
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
- **Modifier**: **open question** (§ Open Questions) — the wheel alone (the list then scrolls with
  its scrollbar only), or `Ctrl` + wheel, the wheel alone scrolling the list as today.
- Mechanism: `ThumbnailGrid` overrides `OnMouseWheel`; the gesture raises a `SizeStepRequested`
  event with the direction, the panel moves the slider one position, which applies the size like
  any other change. Without the gesture's modifier, the base class scrolls as today.
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
  N columns, a divisor, `+` back to one per column, the wheel over the tiles), the size remembered
  with the count.
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
| `−` | Per the answer to the open question |
| Wheel | Over the tiles: one position per notch, up = larger; at the ends nothing; the list still scrolls (per the modifier chosen) |
| Thumbnails | A size change shows the thumbnails again, sharp at the new size; the memory stays under the budget (Task Manager, 5 columns at size 5, scrolling through 100 favorites) |
| Keyboard | `↑` / `↓` move by row at every size; `←` / `→` by tile |
| Start-up | 4 columns at size 2 remembered after a restart; `ExplorerSpan` = 3 with 4 columns → 1 |
| Width | The panel and the window width unchanged by the slider; collapse / expand hide and show it |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [ ] The wheel over the tiles moves the slider: the wheel **alone** (the list scrolling with its
  scrollbar only), or **`Ctrl` + wheel** (the wheel alone keeping the list's scroll, Explorer's
  convention)? *(Asked on 2026-09-26, the question cancelled by the app's quit; asked again.)*
- [ ] `−` (one column fewer): the tile size back to **1** like `+`, **kept when it still divides**
  the new count (4 columns at size 2 → 2 columns: kept; 3 at size 3 → 2: back to 1), or the
  **full width kept** (a size equal to the count stays equal to the new count, the others back
  to 1)?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Does not apply — no test project, verified by hand (§ Test Impact) |
| README | | | |

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
| 6 | The wheel over the tiles: alone, or `Ctrl` + wheel? | *(pending — the first ask cancelled by the app's quit)* | 2026-09-28 |
| 7 | `−`: the size back to 1, kept if it still divides, or the full width kept? | *(pending)* | 2026-09-28 |

---

*Last updated: 2026-09-28*
