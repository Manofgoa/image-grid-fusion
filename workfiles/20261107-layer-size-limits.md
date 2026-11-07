# Layer Size Limits

> Working document — widen the range a layer can be resized to by the wheel: much smaller and much larger
> than today's 3 %–100 % of the grid's shorter side.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

User request (2026-11-07, French): *« Layers, zoom : il est limité, augmenter sa limite »*.

Scoped with the user (Q&A #1–#4): the **zoom** meant is the **layer's size** — the wheel over the selected
layer, the Layers tab selected, growing or shrinking its box on the grid — not the Zoom effect inside the layer.
It is limited **both ways**: a layer cannot be made large enough nor small enough. The new bounds are left for
the agent to propose. The subject is expected to be straightforward.

Components:

- `Composition/GridLayers.cs` — `LayerPlacement.MinSize` (0.03) and `MaxSize` (1.0), applied by
  `LayerPlacement.WithSize`, its only clamp. `Size` is a fraction of the **grid's shorter side**, measuring the
  layer's content's **longer side** as shown — or, for a text, **one line** (`LinePixels`).
- `UI/GridPreview.Layers.cs` — `LayerWheel`: 5 % a notch (`LayerNotch` 1.05) through `WithSize`, only while the
  pointer is over the selected layer (`OnSelectedLayer`).
- `Composition/Compositor.cs` — `DrawLayer` draws the layer by `DrawCell` into its box, whatever its size.

---

## Today's Limits and What They Cost

| Bound | Today | What it gives on a Twitter grid (1200 × 628) | Why it is short |
|---|---|---|---|
| Min | 3 % | A layer's longer side ≈ 19 px in the PNG, ≈ 10 px in a preview ~330 px high | A small logo, a watermark, a tiny emoji in a corner cannot get smaller |
| Max | 100 % | A layer's longer side = the grid's **height**, 628 px | A landscape image on a landscape grid cannot even span the grid's width; a portrait one is a strip in the middle; nothing can be zoomed past the grid's edges for a crop-by-layer effect |

---

## New Limits

| Bound | New | Rationale |
|---|---|---|
| **Min** | **0.5 %** (`MinSize = 0.005`) | ≈ 3 px on the Twitter PNG, ≈ 41 px on an 8192-px one: small enough for any watermark; below it a layer is a speck at every export size |
| **Max** | **500 %** (`MaxSize = 5.0`) | The extreme case — a 9:16 image covering a 21:9 grid (the Free format's widest) — needs ≈ 415 %; 500 % leaves room to zoom past the edges. Beyond, the layer is mostly off the canvas |

- The **wheel** keeps its 5 % a notch; from 3 % to 0.5 % is ≈ 37 notches, from 100 % to 500 % ≈ 33.
- Every reader of `Size` goes through `LayerPlacement` (`BoxSize`, `Corners`, `Contains`) and `CanvasSizer.ForLayers`:
  nothing else holds the bounds. A new text's 12 % and a new image's 40 % are unchanged.
- **The center stays inside the grid** (`MovedBy` clamps it to [0, 1]): a layer larger than the grid is still
  placed by its center, so half of it at most hangs past an edge — enough to cover the grid and frame a part of it.

### A Small Layer Stays Reachable

Today the wheel and a press act on the selected layer only **over its box** (`OnSelectedLayer`). At 0.5 % a box can
be 2 px in the preview: the pointer would miss it, and the layer could never be grown back by the wheel.

- The selected layer's **reach** — the area taking the wheel and a press — is its box, **grown to at least
  24 × 24 logical px** around its center (scaled with `LogicalToDeviceUnits`), turned with it. Its box stays what is
  drawn and outlined; only the hit-test grows.
- Only the **selected** layer gets the grown reach: selecting a tiny layer is done from the layer list, as today for a
  layer hidden under another.

### A Large Layer Stays Within Memory

`DrawCell` allocates bitmaps the size of the cell it draws into — the Background's **edge extension**
(`Compositor.DrawBackground`: the image rendered alone at the cell's size, then `EdgeExtension` and
`FillStyle.OpaquePixels` at that size). For a cell, that is bounded by the canvas. For a layer at 500 % on an
8192-px canvas, the box's longer side could reach ≈ 41 000 px — a 6.7 GB bitmap.

- Those per-cell intermediate bitmaps get their resolution **capped at `CanvasSizer.MaxLongSide` (8192) on their
  longer side**, the result stretched over the box — the same mechanism the blur's small bitmap already uses
  (`DrawScale`). A cell never reaches the cap, so nothing changes for the cells; an extension is stretched edge
  pixels, so the lower resolution does not show.
- A text layer whose lines are long already has this risk at 100 % (its size measures one line): the cap covers it too.
- The blur's small bitmap is already bounded (its block grows with the cell); the Cutout and Upscale are computed on
  the image as loaded, out of `DrawCell`.

---

## Documentation

- `README.md` / `README.fr.md` § Layers, the wheel line: the range added — *from 0.5 % to 500 % of the grid's shorter
  side*.
- `SHORTCUTS.md` / `SHORTCUTS.fr.md`: the wheel's row unchanged (no bounds there).
- `RULES.md` § Layers: one line for the reach of the selected layer (a new hit-test rule a future gesture follows).
- `GLOSSARY.md` / `GLOSSARY.fr.md`: unchanged.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `WithSize` clamps to [0.005, 5.0]; a size inside is kept | `tests/ImageGridFusion.Tests/Composition/LayerPlacementTests.cs` | Create |
| `BoxSize` at 500 % on a landscape canvas: the content's longer side is 5 × the canvas's shorter side | `tests/ImageGridFusion.Tests/Composition/LayerPlacementTests.cs` | Create |
| The selected layer's reach: a 2-px box is hit within 12 logical px of its center, turned; a large box's reach is its box | `tests/ImageGridFusion.Tests/Composition/LayerPlacementTests.cs` (if the reach lives on `LayerPlacement`) | Create |
| The edge extension's intermediate bitmap is capped at 8192 on its longer side for a box larger than that | `tests/ImageGridFusion.Tests/Composition/` — the class holding the cap's computation | Create |

---

## Open Questions

- [ ] Are **0.5 %** and **500 %** the bounds you want, or other values (e.g. 1 % / 1000 %)?
- [ ] The **grown reach** of a tiny selected layer (24 × 24 logical px around its center): keep it in this scope, or
      leave a tiny layer reachable from the layer list only?
- [ ] The **memory cap** on the per-cell intermediate bitmaps: keep it in this scope (needed for 500 % to be safe),
      or limit the max instead so that it is not needed?

---

## Design Iterations

### Iteration 1 — 2026-11-07

Scoping batch answered: the layer's size (wheel), both bounds, values proposed by the agent, straightforward
subject. Explored directly — one chained question (where the bounds live, then what depends on them).
Proposed: 0.5 %–500 % of the grid's shorter side; the selected layer's reach grown to 24 × 24 logical px so a tiny
layer can be grown back; the per-cell intermediate bitmaps capped at 8192 px so a huge layer cannot exhaust memory.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which zoom of the layers is limited — the layer's size (wheel), the Zoom effect inside it, or both? | The layer's size | 2026-11-07 |
| 2 | Which way does the limit hinder — max, min, both? | Both ways | 2026-11-07 |
| 3 | How far should the new limit go? | Left to the agent to propose | 2026-11-07 |
| 4 | Straightforward or tricky / long to explore? | Straightforward | 2026-11-07 |
| 5 | Bounds 0.5 % / 500 %? | | |
| 6 | Grown reach of a tiny selected layer in scope? | | |
| 7 | Memory cap on the per-cell intermediate bitmaps in scope? | | |

---

*Last updated: 2026-11-07*
