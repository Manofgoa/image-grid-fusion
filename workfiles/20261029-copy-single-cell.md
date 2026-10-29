# Copy a Single Cell

> Working document — copy one cell's image, or one layer, alone to the clipboard as a transparent
> PNG, rendered with its effects, to paste it elsewhere or into another cell.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today Copy and Ctrl+C put the **whole grid** on the clipboard. The user wants to copy **one cell
alone**: its image **rendered** — every cell effect applied (crop, zoom, orientation, fine angle,
colors, blur…) — **without its Background**, cut to the image as drawn and transparent around it
(an image already cut out stays cut out), as a **PNG** to paste into another app or into another
cell of the grid, where it arrives as a plain image.

The same for the **selected layer**.

Components concerned (from the exploration):

| Area | Where |
|---|---|
| Ctrl+C routing | `MainForm.ProcessCmdKey` (Ctrl+C → `CopyToClipboard()`, the text-field whitelist, the explorer tiles' own Ctrl+C as precedent) |
| Still PNG on the clipboard | `MainForm.Copy` — PNG stream (`"PNG"`) + DIB flattened on white (`Compositor.Flattened`), `PreviousCopy.Keep` |
| Cell context menu | `MainForm.ShowCellMenu`, `_cellMenu` — today shown only when *Find similar images* is available |
| Layer commands | The layer list's icon buttons (`MainForm.Layers.cs`); no context menu on a layer |
| One cell drawn | `Compositor.DrawCell` (the Background drawn only when `look.Background is { }`), `Compositor.DrawLayer` / `LayerCell` |
| Drawn rectangle | `FitCalculator.ComputeTurned` (`Bounds`, `Fit.Destination`) |
| Native size | `CanvasSizer.DrawnScale` (private), `SizedImage` |
| Paste into a cell / a layer | `MainForm` paste (`Clipboard.GetImage()`), `MainForm.Layers.cs` paste — both read the DIB, **not** the `"PNG"` format |

---

## Triggers

- **Cell context menu**: a **Copy cell** entry (`Ctrl+C` shown beside it), first in the cell's
  right-click menu, on the cell clicked. The menu shows on every cell holding an image, whatever the
  visual analysis — *Find similar images* is hidden while `FileExplorerPanel.CanFindSimilar` is false.
  Copy cell is disabled while exporting.
- **Ctrl+C**: with a **cell selected** holding an image, or a **layer selected**, Ctrl+C copies it
  alone; with nothing selected (or an empty cell selected), the whole grid, as today.
- **Ctrl+Shift+C** always copies the **whole grid** — the Copy button's still or video, as Ctrl+C
  did. The **Copy button** and its ▾ menu stay the grid's.
- The text-field whitelist and the explorer tiles' Ctrl+C keep priority, as today.
- **Layer list**: a **Copy** icon button after *Add text* — two sheets, the front one a picture
  (`EffectIcons.LayerAction.Copy`) —, on the selected layer, enabled while one is selected outside its
  edit view.
- Every route goes through `MainForm.CopyAlone`, which waits for the pending cutouts first, as the
  grid's still does.
- **Locked while exporting**, like Copy.

## What Is Rendered

- The cell's content drawn by `Compositor.DrawCell` with **its look minus the Background**
  (`look with { Background = null }`): every other effect applied, from the one place every effect
  is drawn (RULES.md § Rendering). A new cell effect is copied by itself.
- **Cut to the image as drawn**: the rectangle the image is drawn in (`ComputeTurned(...).Bounds`),
  clipped to the cell — what the cell actually shows of the image. Outside the image, transparent.
  **Not trimmed** to the visible pixels: the framing chosen is kept exactly, a PNG's transparent
  margins included.
- An image already transparent (a cut-out PNG) stays transparent: the canvas is ARGB, nothing
  fills it.
- **Animated content**: the frame **shown** at the grid time (playing or paused) — a still, like a
  freeze-frame. The grid is held during the render (`AnimationPlayer.Hold` / `Release`, as Copy).
- **A layer**: its content drawn into its box (`LayerCell`) with its look minus the Background,
  **turned as placed** — as the grid shows it (`Compositor.DrawLayer`'s rotation by its angle) —
  cut to the axis-aligned box of the turned drawn image, its corners transparent.
- The layer edit view open: the edited layer is the selected cell's content — copied like a cell.

## Size

- **Native size**: the smallest cell where the image is **not downscaled as drawn** — the same
  criterion as the grid's PNG (`CanvasSizer.DrawnScale`, now `internal`, read through `SizedImage`:
  its zoom, its fine angle's cover scale; a content that sharpens — PDF, thumbnail, text — read at
  zoom 1). The cell keeps its shape — **its rectangle in the PNG export** (`CanvasSizer.Compute`,
  `Compositor.Cells`), a layer's **box at the smallest canvas of the ratio** (`LayerPlacement.BoxSize`)
  —; its size is scaled until the drawn scale reaches 1. One function, `Composition/CellCopy.Render`.
- **Longer side capped at 8192** (`CanvasSizer.MaxLongSide`), like the PNG Copy.
- **No 1200 floor**: the grid's minimum is an output rule for posting the grid; a cell copied alone
  stays at its native size, a small image giving a small PNG.

## Clipboard

- The same formats as the grid's still Copy: the **`"PNG"` stream** (alpha kept) and the image
  **flattened on white** (`Compositor.Flattened`, DIB) for the apps reading only that.
- The clipboard step is shared with the grid's still Copy (`MainForm.PutStillOnClipboard`).
- When the image shows nothing in its cell, the status line says *Nothing to copy*.
- The status line says what was copied — `Cell copied to the clipboard — PNG 812 × 640, 312 Ko`,
  `Layer copied…` — as `StillSummary` does.
- **Kept in `previous\`** like any Copy (`PreviousCopy.Keep`), named after the copied image's file
  alone (`fusion-…` when it has none), the previous copy deleted.

## Pasting Into a Cell

- The PNG pasted into another cell (Ctrl+V, the layer list's Paste) arrives as a **plain image**:
  the effects are in its pixels, the new cell starts from the default effects (an ordinary arrival —
  Arrival Zoom, the grid starting over).
- **The paste reads the `"PNG"` format first**, its alpha kept, then the DIB (`Clipboard.GetImage()`)
  as today: the cut-out arrives transparent — and so does a PNG another app (a browser) puts on the
  clipboard. Both routes: the cells' Ctrl+V (`MainForm`) and the layer list's Paste
  (`MainForm.Layers.cs`), through `UI/ClipboardImage.Get` (`Clipboard.TryGetData`, `GetData` being
  obsolete). A file drop list still wins over an image, as today.

## Documentation

- `RULES.md`: a short section — what a cell copy renders, one route for the cell and the layer.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: **Cell copy** (*copie de cellule*).
- `README.md` / `README.fr.md`: the feature and its shortcuts (Ctrl+C on the selection,
  Ctrl+Shift+C for the grid), and the paste keeping transparency.

---

## Test Impact

None: the solution has no test project, and every previous workfile stayed test-free (as decided
in `20261029-layers.md`, Q&A #13).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

- [x] ~~Trigger?~~ → The cell's context menu and Ctrl+C on the selected cell (Q&A #1)
- [x] ~~What does the PNG hold?~~ → The image alone, cut out: every effect but the Background,
  cut to the image as drawn, transparent around it (Q&A #2)
- [x] ~~Size?~~ → Native size (Q&A #3)
- [x] ~~Animated content?~~ → A PNG of the frame shown (Q&A #5)
- [x] ~~Layers?~~ → Yes, the selected layer too (Q&A #6)
- [x] ~~Pasted into a cell, how does it arrive?~~ → A plain image, effects baked in (Q&A #7)
- [x] ~~1. Ctrl+C with a cell selected copies the cell: how is the whole grid copied by keyboard
  then?~~ → Ctrl+Shift+C always copies the grid; Ctrl+C copies the grid with nothing selected
  (Q&A #8)
- [x] ~~2. Should the paste into a cell / a layer read the `"PNG"` format first, so a cut-out keeps
  its transparency?~~ → Yes, the PNG first, then the DIB (Q&A #9)
- [x] ~~3. Cut to the image's drawn rectangle, or trimmed to its visible pixels?~~ → The drawn
  rectangle, clipped to the cell (Q&A #10)
- [x] ~~4. A turned layer: copied turned as placed, or upright?~~ → Turned as placed (Q&A #11)
- [x] ~~5. Is a cell copy kept in `previous\` like the grid's Copy?~~ → Yes (Q&A #12)

---

## Design Iterations

### Iteration 1 — 2026-10-29

Scoping answered by the user (Q&A #1–#7), then a scout pass over the code (Ctrl+C and clipboard,
single-cell rendering, menus and layer commands). Initial design:

- Copy cell from the cell's context menu and Ctrl+C on the selected cell; Copy on the selected
  layer from the layer list and Ctrl+C.
- Rendered by `Compositor.DrawCell` with the look minus the Background, at native size (cap 8192,
  no floor), cut to the drawn image, on an ARGB canvas.
- Put on the clipboard as the grid's still Copy does (PNG stream + flattened DIB).
- Found: the paste reads only the DIB, flattened on white — a cut-out would lose its transparency
  (Open Question 2). The cell menu is gated by *Find similar images* and must show without it.

### Iteration 2 — 2026-10-29

Open questions answered (Q&A #8–#12), the domain sections updated:

- Ctrl+C copies the selected cell or layer, the grid with nothing selected; **Ctrl+Shift+C** always
  copies the grid.
- The paste (cells and layers) reads the `"PNG"` format first, so a cut-out keeps its transparency.
- Cut to the drawn rectangle, never trimmed to the visible pixels.
- A turned layer is copied turned, as placed.
- A cell copy is kept in `previous\` like any Copy.

### Iteration 3 — 2026-10-29 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`feature/copy-single-cell`). No unit
tests — the solution has no test project.

### Iteration 4 — 2026-10-29 — 🧭 Implementation choices

No project rule broken. What the frozen design did not state, decided during the run:

- **The cell's shape** is its rectangle in the PNG export (`CanvasSizer.Compute` then
  `Compositor.Cells`), so the zoom fit and the crop match the grid's PNG; a **layer's** is its box at
  `CanvasSizer.Smallest(ratio)` — any canvas gives the same shape, the size being rescaled anyway.
- **One function** for the cell and the layer, `CellCopy.Render(frame, shape, angle)`: the cell scaled
  to native size, the image's drawn box clipped to it, turned by the layer's angle, its axis-aligned
  box the output; drawn into it through a translation and a rotation, as `Compositor.DrawLayer` does.
  `CanvasSizer.DrawnScale` went from `private` to `internal` for it.
- A **text layer** is sized like any content that sharpens (fitted at zoom 1), not by its line height.
- The **cell menu** now shows on every cell holding an image, Copy cell first; *Find similar images*
  hidden while unavailable rather than the whole menu.
- **The layer edit view**: the layer list's Copy is disabled there (no selected layer); `Ctrl+C` copies
  the edited layer as a cell — upright, as the view shows it.
- The copy **waits for the pending cutouts** (`WaitForCutoutsAsync`): the Cutout effect landed on
  `main` meanwhile, and the grid's still waits for them too.
- The clipboard step is **extracted** (`PutStillOnClipboard`), shared with the grid's still Copy.
- A render giving nothing (an image moved out of its cell) says *Nothing to copy* in the status line.
- The **previous copy** is named after the copied image's file alone.
- The paste reads the PNG through `Clipboard.TryGetData` — `GetData` is obsolete in .NET 10.
- Checked outside the app with a scratch console calling `CellCopy.Render`: native size, the
  transparent margin of a cut-out PNG kept, a fine angle's and a turned layer's corners transparent
  (30°: 498 × 460 for a 400 × 300 image, 496 × 460 expected).

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-29 | `CellCopy`, `GridPreview.RenderSelectedAlone` / `RenderCellAlone`, `MainForm.CopyAlone`, Ctrl+C / Ctrl+Shift+C, the cell menu, the layer list's Copy, `ClipboardImage` |
| Unit tests | 4 | 2026-10-29 | None — no test project |
| README | 4 | 2026-10-29 | README and GLOSSARY in English and French; RULES.md § Cell Copy |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How is a single cell's copy triggered? | The cell's context menu, and Ctrl+C on the selected cell | 2026-10-29 |
| 2 | What does the copied PNG hold? | The image alone, cut out: every effect but the Background, transparent around it | 2026-10-29 |
| 3 | At what size is the cell rendered? | Native size | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-29 |
| 5 | For a video or an animated GIF, what is copied? | A PNG of the frame shown | 2026-10-29 |
| 6 | Are the layers concerned? | Yes, the selected layer | 2026-10-29 |
| 7 | Pasted into another cell, how does it arrive? | A plain image, effects baked in | 2026-10-29 |
| 8 | Ctrl+C copying the cell: how is the whole grid copied by keyboard? | Ctrl+Shift+C always copies the grid; Ctrl+C copies the selected cell / layer, the grid with nothing selected; the Copy button stays the grid's | 2026-10-29 |
| 9 | Does the paste read the `"PNG"` format first, keeping transparency? | Yes, the PNG first | 2026-10-29 |
| 10 | Cut to the drawn rectangle, or trimmed to the visible pixels? | The drawn rectangle | 2026-10-29 |
| 11 | A turned layer: copied turned, or upright? | Turned as placed | 2026-10-29 |
| 12 | Is a cell copy kept in `previous\`? | Yes, like Copy | 2026-10-29 |

---

*Last updated: 2026-10-29*
