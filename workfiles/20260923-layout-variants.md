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

Integer splits keep the v1 rule: cells tile the canvas exactly with no gap, and a boundary at
fraction `f` of a side of `n` px falls at `floor(n × f)`, so no pixel is lost to rounding.

### Mirror

A single **mirror** toggle, next to the thumbnails, replaces dedicated mirrored layouts
(Q&A #8). "Big image on the right" from the brief is `3-big-left` with the mirror on.

| Layout | Asymmetric along | Mirrored version |
|---|---|---|
| `2-split` | horizontal | `[2│1 1]` |
| `3-big-left`, `3-featured`, `4-featured`, `4-big-left` | horizontal | featured cell on the right |
| `3-big-top`, `4-big-top` | vertical | featured cell at the bottom *(pending Q&A #15)* |
| `2-columns`, `2-rows`, `3-columns`, `4-grid`, `4-columns` | symmetric | mirror would only reorder images — *(pending Q&A #16)* |

---

## Variant Selection (UI)

- **Layout thumbnails** (user decision): a row of small buttons, one per variant available for
  the current image count; clicking one applies it and re-renders the preview immediately. The
  active one is highlighted.
- With 0 or 1 image there is nothing to choose: the thumbnail row is hidden *(proposed)*.
- **Not remembered across launches** (user decision): every launch starts on the default
  variant of each count.
- Canvas sizing and export use the **active variant**: `CanvasSizer` computes the width from that
  variant's cell fractions, so switching variant can change the output resolution.

---

## Code Impact (proposal)

- `GridLayout`: a layout becomes data — a list of cells expressed as fractions of the canvas —
  from which both `Cells` (pixels, exact integer tiling) and `CellFractions` are derived, so a
  variant is described once instead of twice. A catalog lists the variants per image count, the
  first one being the default.
- `Compositor.Draw`, `CanvasSizer.Compute`: take the layout to use instead of deriving it from
  the image count.
- `GridPreview`: holds the active layout, uses it for rendering and hit-testing.
- `MainForm`: hosts the thumbnail row and keeps the active variant in sync with the image count.

---

## Test Impact

Depends on Open Question "unit tests". v1 has no test project (declined, v1 Q&A #12).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(pending the answer on unit tests)* | — | — |

---

## Open Questions

- [x] ~~Which 2-image variants are kept?~~ → All: `2-columns` (default), `2-rows`, `2-split`
- [x] ~~Which 3-image variants are kept?~~ → All: `3-big-left` (default), `3-columns`,
      `3-featured`, `3-big-top` (`3-big-right` becomes the mirror of `3-big-left`)
- [x] ~~Which 4-image variants are kept?~~ → All: `4-grid` (default), `4-columns`, `4-featured`,
      `4-big-left`, `4-big-top`
- [x] ~~Mirrors: a dedicated thumbnail per mirrored variant (e.g. `3-big-right`), or a single
      "mirror" toggle applying to every asymmetric layout?~~ → A single mirror toggle
- [ ] Mirror axis: horizontal only (big-top layouts cannot be flipped), or flip along whichever
      axis the layout is asymmetric on (big-top → big-bottom)?
- [ ] Mirror on a symmetric layout: disabled, or allowed (reverses the image order)?
- [ ] Mirror state when the layout or the image count changes: kept, or reset to off?
- [ ] Which image goes where when the variant changes: image 1 always takes the featured (big)
      cell, then the others in reading order — or strict reading order of the cells
      (left→right, top→bottom), so in `3-big-right` image 1 would be the top-left small cell?
- [ ] When the image count changes (image added or removed): back to the default variant of the
      new count, or back to the variant last picked for that count during the session?
- [ ] Thumbnail look: neutral schematic rectangles, or miniatures showing the actual images?
- [ ] Thumbnail row placement: in the bottom bar left of Copy/Save, a bar above the preview, or a
      vertical strip on the side?
- [ ] Keyboard shortcut to cycle through the variants of the current count?
- [ ] Unit tests: still none, or create a test project now for the layouts (every variant tiles
      the canvas exactly, cell count matches, fractions match pixels)?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Pending Open Question |
| README | | | |

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
| 9 | Image-to-cell mapping when the variant changes? | | |
| 10 | Variant after the image count changes? | | |
| 11 | Thumbnail look? | | |
| 12 | Thumbnail row placement? | | |
| 13 | Keyboard shortcut to cycle variants? | | |
| 14 | Unit tests for the layouts? | | |
| 15 | Mirror axis: horizontal only, or the layout's asymmetric axis? | | |
| 16 | Mirror on a symmetric layout? | | |
| 17 | Mirror state when layout or count changes? | | |

---

*Last updated: 2026-09-23*
