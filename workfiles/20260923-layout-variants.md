# Layout Variants

> Working document — several layouts per image count, picked by clicking a layout thumbnail.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today each image count (2, 3, 4) has exactly one layout, hard-coded in
`Composition/GridLayout.cs` (`Cells` and `CellFractions`). This task adds **layout variants**:
for a given image count, the user picks one of several layouts by clicking a thumbnail.

The starting ideas from the brief were "the big image on the right" (mirror of the 3-image
layout) and "2 stacked images"; after discussing a catalog, the user kept every proposed layout,
with mirrored versions obtained through a single "mirror" button rather than separate layouts.

Out of scope: the 1-image case (its frame design is a separate future task, see
`20260923-application-v1.md`), the output ratio (stays `1200:628`), the fitting rule.

---

## Current Engine (as found in the code)

| Consumer | Use of the layout |
|---|---|
| `Composition/GridLayout.cs` | `Cells(count, canvas)` → pixel rectangles; `CellFractions(count)` → same cells as fractions of the canvas |
| `Composition/CanvasSizer.cs` | `CellFractions` → canvas width at which no image is downscaled |
| `Composition/Compositor.cs` | `Cells` → where each image is drawn |
| `UI/GridPreview.cs` | `Cells` → preview rendering, hit-testing (selection, ×, drag-to-swap, drop target) |

The two methods describe the same cells twice (integer pixels and fractions). Everything else
only consumes cells by index, so a variant only has to change what these two return.

---

## Layout Catalog

Canvas ratio `R = 1200 / 628 ≈ 1.91`. A cell's ratio decides which images it suits: < 1 favours
portraits and phone screenshots, ≈ 1.9 landscapes and desktop screenshots, > 3 panoramas only.
**★** = the current layout, default of its count. Every layout below is kept (Q&A #5–#7).
Mirrored versions are not separate layouts: they come from the mirror toggle (see
`### Mirror`).

### 2 images

| Id | Sketch | Cells | Cell ratios | Suits |
|---|---|---|---|---|
| ★ `2-columns` | `[1│2]` | two halves side by side | 0.95 · 0.95 | two portraits / squares |
| `2-rows` | `[1]` over `[2]` | two halves stacked | 3.82 · 3.82 | panoramas, banners — crops or bands most images |
| `2-split` | `[1 1│2]` | 2/3 · 1/3 side by side | 1.27 · 0.64 | one landscape + one portrait |

### 3 images

| Id | Sketch | Cells | Cell ratios | Suits |
|---|---|---|---|---|
| ★ `3-big-left` | `[1│2/3]` | left half · two stacked quarters on the right | 0.95 · 1.91 · 1.91 | one portrait + two landscapes |
| `3-columns` | `[1│2│3]` | three thirds side by side | 0.64 · 0.64 · 0.64 | three portraits / phone screenshots |
| `3-featured` | `[1 1│2/3]` | left 2/3 · two stacked cells on the right third | 1.27 · 1.27 · 1.27 | one featured landscape + two, all cells the same shape |
| `3-big-top` | `[1]` over `[2│3]` | top half · two quarters below | 3.82 · 1.91 · 1.91 | one panorama + two landscapes |

### 4 images

| Id | Sketch | Cells | Cell ratios | Suits |
|---|---|---|---|---|
| ★ `4-grid` | `[1│2]` over `[3│4]` | 2 × 2 quarters | 1.91 × 4 | four landscapes |
| `4-columns` | `[1│2│3│4]` | four quarters side by side | 0.48 × 4 | four phone screenshots (≈ 9:19.5 → very little crop) |
| `4-featured` | `[1 1│2/3/4]` | left 2/3 · three stacked cells on the right third | 1.27 · 1.91 · 1.91 · 1.91 | one featured image + three landscapes |
| `4-big-left` | `[1│2/3/4]` | left half · three stacked cells on the right half | 0.95 · 2.87 · 2.87 · 2.87 | one portrait + three wide images |
| `4-big-top` | `[1 1 1]` over `[2│3│4]` | top half · three thirds below | 3.82 · 1.27 · 1.27 · 1.27 | one panorama + three |

Integer splits keep the v1 rule: cells tile the canvas exactly with no gap. Each layout is
described on a small grid of integer units (e.g. 3 × 3 for `4-featured`), and a boundary at `u`
units out of `n` on a side of `size` px falls at `floor(size × u / n)` — the v1 `floor(size / 2)`
for halves — so neighbour cells share it and no pixel is lost. The 1-image case is a catalog
entry too (`1-single`), with no alternative.

### Mirror

A single **mirror** toggle, next to the thumbnails, replaces dedicated mirrored layouts
(Q&A #8). "Big image on the right" from the brief is `3-big-left` with the mirror on.

- It flips the layout **along its asymmetric axis** (Q&A #15): left↔right for layouts whose
  featured cell is on the left, top↔bottom for big-top layouts.
- On a **symmetric** layout the toggle is **disabled** (Q&A #16): flipping would only reorder the
  images, which drag-to-swap already does.
- It is **reset to off** whenever the layout or the image count changes (Q&A #17).
- Mirroring flips the cells in grid units, so the mirrored layout tiles the canvas as exactly as
  the original; a mirrored cell may be 1 px wider or narrower than its unmirrored counterpart.

| Layout | Flipped along | Mirrored version |
|---|---|---|
| `2-split` | horizontal | `[2│1 1]` |
| `3-big-left`, `3-featured`, `4-featured`, `4-big-left` | horizontal | featured cell on the right |
| `3-big-top`, `4-big-top` | vertical | featured cell at the bottom |
| `2-columns`, `2-rows`, `3-columns`, `4-grid`, `4-columns` | — | toggle disabled |

### Image-to-Cell Mapping

Image `1` always takes the **featured cell** (the big one), the others follow in the reading
order of the remaining cells (Q&A #18). The mirror moves the cells, not the images: in
`3-big-left` mirrored, image 1 is the big cell on the right. The v1 "reading order" (last image,
replace rule, command-line order) therefore means **image order**, which is the cell order of the
layout — unchanged for the default layouts.

---

## Variant Selection (UI)

- **Layout thumbnails** (Q&A #1): one small button per layout available for the current image
  count; clicking one applies it and re-renders the preview immediately. The active one is
  highlighted.
- **Look** (Q&A #11): neutral schematic rectangles drawing the shape of the cells — no rendering
  of the actual images.
- **Placement** (Q&A #12): a **vertical strip on the left of the preview**, thumbnails stacked
  top to bottom, the mirror toggle below them. The bottom bar keeps Copy / Save and the status
  line. The **right** side of the preview is reserved for the add-images drop zone
  (`20260923-drop-zone.md`).

  ```
  +-----+------------------------------+
  | [□] |                              |
  | [□] |           preview            |
  | [□] |                              |
  | [⇄] |                              |
  +-----+------------------------------+
  | status               Copy  Save…   |
  +------------------------------------+
  ```

- The **active thumbnail** shows the layout as applied, mirror included; clicking it again does
  nothing. Each thumbnail has a tooltip with the layout name; the mirror's tooltip names its axis,
  or says it is unavailable on a symmetric layout.
- The **mirror toggle** draws two triangles facing away from a dashed axis, turned upright for a
  vertical flip; it looks pressed when on and is greyed out when disabled.
- The strip is 80 px wide (logical), slightly darker than the preview. Dropping files on it acts
  like a drop elsewhere in the window (appended).
- The selection follows its image across layout changes.
- With 0 or 1 image there is nothing to choose: the strip is hidden.
- **Image count changes** (Q&A #10): the layout goes back to the **default** of the new count,
  and the mirror to off.
- **Not remembered across launches** (Q&A #3): every launch starts on the default layouts.
- **No keyboard shortcut** (Q&A #13): layouts and mirror are mouse-only.
- Canvas sizing and export use the **active variant**: `CanvasSizer` computes the width from that
  variant's cell fractions, so switching variant can change the output resolution.

---

## Code Structure

- `Composition/GridLayout.cs`: `GridLayout` is now a class whose instances are layouts — cells on
  a grid of units, cell 0 featured — with `Cells(canvas)`, `CellFractions()`, `MirrorAxis`,
  `IsMirrored` and `Mirrored()`. Static members keep the output ratio, `MaxImages`, and the
  catalog: `For(count)` (default first) and `Default(count)`.
- `Compositor.Render` / `Draw`, `CanvasSizer.Compute`: take the layout to use; `CanvasSizer`
  rejects a layout whose cell count differs from the image count.
- `UI/GridPreview.cs`: holds the active layout (`ActiveLayout`, `SetLayout`), uses it for
  rendering and hit-testing, **resets it to the default** when the image count changes, and
  raises `LayoutChanged`.
- `UI/LayoutStrip.cs`: new custom-painted control — thumbnails, mirror toggle, tooltips; raises
  `LayoutPicked` and `MirrorToggled`.
- `UI/MainForm.cs`: docks the strip on the left above the bottom bar, relays the events between
  strip and preview, and exports with the active layout.

---

## Test Impact

**No unit tests** — declined by the user again for this task (Q&A #14); v1 has no test project
either (v1 Q&A #12). Nothing is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined) | — | — |

---

## Open Questions

- [x] ~~Which 2-image variants are kept?~~ → All: `2-columns` (default), `2-rows`, `2-split`
- [x] ~~Which 3-image variants are kept?~~ → All: `3-big-left` (default), `3-columns`,
      `3-featured`, `3-big-top` (`3-big-right` becomes the mirror of `3-big-left`)
- [x] ~~Which 4-image variants are kept?~~ → All: `4-grid` (default), `4-columns`, `4-featured`,
      `4-big-left`, `4-big-top`
- [x] ~~Mirrors: a dedicated thumbnail per mirrored variant (e.g. `3-big-right`), or a single
      "mirror" toggle applying to every asymmetric layout?~~ → A single mirror toggle
- [x] ~~Mirror axis: horizontal only (big-top layouts cannot be flipped), or flip along whichever
      axis the layout is asymmetric on (big-top → big-bottom)?~~ → Along the asymmetric axis
- [x] ~~Mirror on a symmetric layout: disabled, or allowed (reverses the image order)?~~ →
      Disabled
- [x] ~~Mirror state when the layout or the image count changes: kept, or reset to off?~~ →
      Reset to off
- [x] ~~Which image goes where when the variant changes: image 1 always takes the featured (big)
      cell, then the others in reading order — or strict reading order of the cells
      (left→right, top→bottom), so in `3-big-right` image 1 would be the top-left small cell?~~ →
      Image 1 always takes the featured cell
- [x] ~~When the image count changes (image added or removed): back to the default variant of the
      new count, or back to the variant last picked for that count during the session?~~ →
      Default of the new count
- [x] ~~Thumbnail look: neutral schematic rectangles, or miniatures showing the actual images?~~ →
      Neutral schematic rectangles
- [x] ~~Thumbnail row placement: in the bottom bar left of Copy/Save, a bar above the preview, or a
      vertical strip on the side?~~ → Vertical strip on the left of the preview
- [x] ~~Keyboard shortcut to cycle through the variants of the current count?~~ → None
- [x] ~~Unit tests: still none, or create a test project now for the layouts (every variant tiles
      the canvas exactly, cell count matches, fractions match pixels)?~~ → Still none

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design from the brief and the scoping batch (Q&A #1–#4):

- Variants picked by clicking layout thumbnails; not remembered across launches.
- "2 stacked images" came from an earlier agent suggestion, not from the user: the user asked to
  discuss which layouts are worth adding → the layout catalog is a proposal, every variant still
  to be accepted or dropped.
- Code explored directly (single question: how the layout engine works) — layouts are hard-coded
  per count in `GridLayout`, consumed by index everywhere else.
- Proposed by the agent, open to review: current layouts stay the defaults, thumbnail row hidden
  with 0–1 image, canvas sized on the active variant, layouts described once as fractions.

### Iteration 2 — 2026-09-23

Catalog settled (Q&A #5–#8): every proposed layout is kept. Mirrored layouts are replaced by a
single mirror toggle, so `3-big-right` leaves the catalog and becomes `3-big-left` mirrored.
Three questions follow from the toggle (axis, symmetric layouts, persistence of its state).

### Iteration 3 — 2026-09-23

Mirror and mapping settled (Q&A #15–#18): the mirror flips along the layout's asymmetric axis
(big-top gains a big-bottom version), is disabled on symmetric layouts, and resets to off on any
layout or count change. Image 1 always takes the featured cell; new `### Image-to-Cell Mapping`.

### Iteration 4 — 2026-09-23

UI and tests settled (Q&A #10–#12, #14): the layout resets to the default when the image count
changes; thumbnails are neutral schematics in a vertical strip on the left of the preview, with
the mirror toggle below them; no unit tests. Only the keyboard shortcut remains open.

### Iteration 5 — 2026-09-23

Cross-reference from `20260923-drop-zone.md` (its Q&A #11): the right side of the preview is
reserved for the add-images drop zone. No conflict with the left thumbnail strip; noted in
`### Variant Selection (UI)`.

### Iteration 5 — 2026-09-23

Last open question answered (Q&A #13): no keyboard shortcut. The design is complete.

### Iteration 6 — 2026-09-23

Go for implementation asked (Q&A #19): **No** — the gate holds, the design stays open.

### Iteration 7 — 2026-09-23 — ✅ Implemented

Go given (Q&A #20–#21): **code + README**, on `main` (user's deliberate choice). Scope frozen as
the design sections stand at this iteration. Unit tests stay declined (Q&A #14).

### Iteration 8 — 2026-09-23 — 🧭 Implementation choices

Code and README delivered on `main`. Choices the frozen design did not state:

- **Integer unit grids**: layouts are described on grids of units rather than fractions, which
  keeps exact tiling for thirds and for mirrored layouts (floating-point fractions could leave a
  1 px gap or overlap between neighbours).
- **Layout reset lives in `GridPreview`**, not in `MainForm` as the proposal said: the preview
  owns the images and is the only place that sees the count change; it raises `LayoutChanged`
  for the strip.
- **`1-single`** is a catalog entry, so every image count goes through the same code path.
- **Clicking the active thumbnail does nothing** — it does not turn the mirror off.
- **Active thumbnail shows the mirror**; tooltips name each layout (English, like the rest of the
  UI) and explain the mirror state.
- **Mirror icon** drawn by hand (triangles and a dashed axis) rather than a Unicode glyph, whose
  availability depends on the fonts.
- **Strip accepts file drops**, handled as a drop elsewhere in the window, so the new strip does
  not become a dead zone for the v1 drop rule.
- **Selection kept** across layout changes (it follows its image); hover is reset.
- **Property named `ActiveLayout`**: `Layout` clashes with the inherited `Control.Layout` event.
- **README**: the replace rule now says "the last image (image 4)" instead of "the last cell in
  reading order", since a mirrored layout's reading order no longer matches the image order.

Verification: build with no warning; a scratch harness (outside the repo) checked that each of
the 13 layouts, and its mirror, covers every pixel exactly once on 5 canvas sizes, that
mirroring twice returns the original, and rendered the window off-screen (3 images mirrored,
4 images big-top mirrored, 2 rows with the mirror disabled, 1 image with the strip hidden). The
clicks themselves were not exercised by a human.

No project rule was broken. No gap outside the frozen scope was found.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7, 8 | 2026-09-23 | Layout engine, then thumbnail strip and mirror |
| Unit tests | 4 | 2026-09-23 | Declined by the user (Q&A #14) |
| README | 8 | 2026-09-23 | Layouts section rewritten, mirror, replace rule wording |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the user pick a variant? | Layout thumbnails | 2026-09-23 |
| 2 | What does "2 stacked images" mean exactly? | Unknown — it was a Claude suggestion; the user wants to discuss nice layouts to add | 2026-09-23 |
| 3 | Is the chosen variant remembered? | No, default at every launch | 2026-09-23 |
| 4 | Depth of exploration? | Straightforward | 2026-09-23 |
| 5 | Which 2-image variants are kept? | All three: `2-columns`, `2-rows`, `2-split` | 2026-09-23 |
| 6 | Which 3-image variants are kept? | All: big-left (+ big-right), columns, featured, big-top | 2026-09-23 |
| 7 | Which 4-image variants are kept? | All four new ones: columns, featured, big-left, big-top (+ grid) | 2026-09-23 |
| 8 | Mirrors: dedicated thumbnails or a mirror toggle? | A single mirror toggle | 2026-09-23 |
| 9 | Image-to-cell mapping when the variant changes? | Asked as #18 | 2026-09-23 |
| 10 | Variant after the image count changes? | Default of the new count | 2026-09-23 |
| 11 | Thumbnail look? | Neutral schematics | 2026-09-23 |
| 12 | Thumbnail row placement? | Vertical strip on the left of the preview | 2026-09-23 |
| 13 | Keyboard shortcut to cycle variants? | None | 2026-09-23 |
| 14 | Unit tests for the layouts? | No | 2026-09-23 |
| 15 | Mirror axis: horizontal only, or the layout's asymmetric axis? | The asymmetric axis | 2026-09-23 |
| 16 | Mirror on a symmetric layout? | Disabled | 2026-09-23 |
| 17 | Mirror state when layout or count changes? | Reset to off | 2026-09-23 |
| 18 | Image-to-cell mapping when the variant changes? | Image 1 always takes the featured cell | 2026-09-23 |
| 19 | Go for implementation: which scope? | No — the gate holds | 2026-09-23 |
| 20 | Go given ("Lance le dév"): which scope — code only, or code + README? | Code + README | 2026-09-23 |
| 21 | Current branch is `main`: which branch for the implementation? | Stay on `main` | 2026-09-23 |

---

*Last updated: 2026-09-23*
