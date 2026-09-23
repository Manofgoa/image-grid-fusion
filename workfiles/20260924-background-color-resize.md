# Background Color Resize

> Working document — the band (background) color of a cell must follow the majority color of the
> edges of the part the cell shows after zoom, instead of falling back on the whole image.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

When an image does not fill its cell, the remaining bands (and transparent pixels) are painted with a
background color computed by `BandColor` (`src/ImageGridFusion/Composition/BandColor.cs`).

What the code does today:

1. `Compositor.DrawCell` computes the part of the bitmap the cell shows (crop, zoom, focus,
   orientation) and asks `BandColor.For(bitmapPart, bitmapSize)` for the color — so the lookup is
   **already** based on the visible part, and already runs at every redraw (cached per region).
2. `BandColor.Background` returns a color only when **at least three of the four sides** of that part
   are uniform and agree (CIELAB ΔE ≤ 10, 90 % of a 2 %-deep band matching).
3. Otherwise it falls back on `Dominant` — the most frequent color of the **whole image**
   (`DominantColor.Compute`, computed once in `BandColor.Of`).

The reported symptom — "it takes the base image" — is step 3: as soon as the zoomed view has busy
edges, the color jumps to a color of the full image that may not even be visible in the cell.

Goal: the fallback, and more generally the color, comes from the edges of the **visible part** of the
image in its cell, recomputed live during zoom and pan, identically in the preview and in the export.

---

## Color Rule

Agreed with the user (Q&A #1–#3):

| Aspect | Decision |
|---|---|
| "Current view" | The part of the image visible **in each cell**, after crop, zoom and pan (the `bitmapPart` `Compositor.DrawCell` already computes) |
| When | **Live**: during zoom and pan, at every redraw — as today, through the per-region cache of `BandColor.For` |
| Outputs | **Preview and exported image** — both already go through `Compositor.DrawCell`, so one change in `BandColor` covers both |

Rule (Q&A #6, #7):

1. **Unchanged** — if at least three sides of the visible part are uniform and agree, their average
   color wins (a white product shot keeps white bands).
2. **New fallback** — otherwise, the **most frequent color of the edge bands of the visible part**:
   the pixels of the four bands (same 2 % depth as the uniform-side test) are pooled, transparent ones
   (alpha < 128) ignored, quantized to 4 bits per channel as `DominantColor` does; the most populated
   bucket wins and its pixels are averaged.
3. If the edges hold no opaque pixel at all, the whole-image `Dominant` stays as the last resort.

Implemented as `BandColor.MostFrequentOnSides`, called between `Background` and `Dominant` in
`BandColor.For`; each edge pixel counts once (corners are not counted twice). The band depths are
shared with the uniform-side test through `BandColor.Depths`.

Computed on the 512 px sample `BandColor` already keeps, so the cost per region stays a few thousand
pixels at most — compatible with the live requirement. The cache (`_last`) is unchanged.

---

## Video Cells

Agreed with the user (message during exploration, Q&A #5): for a video (or any animation), the color
is computed on the **start frame chosen with the horizontal slider** (the page `SourceImage.Page`
shows), not on another frame, and stays fixed while it plays.

| Output | Today | Planned |
|---|---|---|
| Preview | `SourceImage.ShowPage` computes `BandColor.Of` on the chosen page; `ShowFrame` keeps it while playing | ✅ already right — no change |
| Grid still export | `BandColor.Of(item.Source.Render(item.Page))` — the chosen page | ✅ already right — no change |
| Grid video export (`GridExport`, line ~92) | `BandColor.Of(FrameAt(TimeSpan.Zero))` — **frame 0**, not the chosen page | Use the item's `BandColor`, computed on the chosen page |
| Carousel export (`CarouselExport`, line ~50) | Same, frame 0 for an animated item | Same fix — applied to both branches (playing contents or forced to images) |

`BandColor.For` already accepts a frame of another size than the one sampled ("the frame shown may be
another frame of the same animation"), so reusing the chosen page's `BandColor` on the exported frames
is supported as is.

---

## Documentation

README, section *Crop* (lines ~145–148): the "otherwise the most frequent color of the whole image"
bullet becomes the edge-majority rule, and the animation sentence states that the color comes from the
start frame chosen with the slider.

---

## Test Impact

No unit test is created or updated: the user chose to stay without a test project (Q&A #8), as every
previous workfile did. The behaviours below stay untested by decision, not because nothing testable
changes. Verification is manual:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Busy edges on the zoomed view → band color is the majority color of those edges, not a whole-image color | — (manual) | — |
| Three uniform sides → unchanged average color | — (manual) | — |
| Color follows live while zooming / panning | — (manual) | — |
| Exported image uses the same color as the preview | — (manual) | — |
| Video export: color of the slider's start frame, not frame 0 | — (manual) | — |

---

## Open Questions

- [x] ~~What is the "current view"?~~ → The visible part of the image in each cell (Q&A #1)
- [x] ~~When is the color recomputed?~~ → Live, during zoom and pan (Q&A #2)
- [x] ~~Which outputs?~~ → Preview and exported image (Q&A #3)
- [x] ~~Which frame for a video?~~ → The start frame chosen with the horizontal slider (Q&A #5)
- [x] ~~Keep the "three uniform sides" rule first, or the edge majority alone?~~ → Three uniform sides
  first; only the whole-image fallback is replaced by the edge majority (Q&A #6)
- [x] ~~Edge depth used for the majority?~~ → The same 2 % bands as the uniform-side test (Q&A #7)
- [x] ~~Unit tests: stay without a test project?~~ → Yes, manual checks (Q&A #8)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Scoping batch answered (visible part per cell, live, preview + export). Exploration showed the lookup
is already per visible part and live; the defect is the fallback on the whole-image dominant color
when fewer than three sides are uniform. Proposal: fallback on the majority color of the visible
part's edge bands. The user added that a video uses the slider's start frame; the preview and still
export already do, the video and carousel exports use frame 0 — to fix.

### Iteration 2 — 2026-09-24

Open questions settled: the three-uniform-sides rule stays first; only its fallback changes, from the
whole-image dominant color to the majority color of the visible part's 2 % edge bands (Q&A #6, #7).
No test project, manual checks (Q&A #8). The Color Rule and Test Impact sections now state it as
agreed design.

### Iteration 3 — 2026-09-24 — ✅ Implemented

Go given for the code only (Q&A #9): README and unit tests are not part of the run. Branch: stays on
`main`, the standing choice for this repository. Scope frozen as the Color Rule and Video Cells
sections stand above.

### Iteration 4 — 2026-09-24 — 🧭 Implementation choices

- The edge majority counts each pixel of the four 2 % bands once — the corners, shared by two bands,
  are not counted twice, so no corner weighs more than the middle of a side.
- The carousel export takes the item's `BandColor` on both of its branches: when contents play
  (previously frame 0) and when they are forced to images (previously recomputed on the chosen page,
  which gives the same color). One line instead of two paths.
- The item's `BandColor` is the one the preview computed on the chosen page, possibly from a
  scaled-down page bitmap; `BandColor` samples at 512 px at most either way, so the color matches the
  preview's.
- The still export (`GridExport.Render`, line ~163) keeps recomputing on the chosen page, as the frozen
  design said "no change".
- No rule broken. Branch: `main`, the standing choice for this repository (no Branch Gate question).

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-09-24 | `BandColor` edge-majority fallback; video and carousel exports use the chosen page's color |
| Unit tests | — | 2026-09-24 | Not applicable: no test project, manual checks only (Q&A #8) |
| README | — | 2026-09-24 | Declined: the go covered the code only (Q&A #9) |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What is the "current view" whose edges give the color? | The visible part in each cell | 2026-09-24 |
| 2 | When is the color recomputed? | Live, during zoom and pan | 2026-09-24 |
| 3 | Which outputs are concerned? | Preview and exported image | 2026-09-24 |
| 4 | Depth of the subject? | No preference — single scout pass | 2026-09-24 |
| 5 | (User, unprompted) Which frame for a video? | The start frame chosen with the horizontal slider | 2026-09-24 |
| 6 | Keep the three-uniform-sides rule first, or edge majority alone? | Three uniform sides first, edge majority as fallback | 2026-09-24 |
| 7 | Edge depth for the majority: 2 % or thicker? | 2 %, as today | 2026-09-24 |
| 8 | Unit tests: stay without a test project? | Yes, manual checks | 2026-09-24 |
| 9 | Go for implementation? | Implement the code | 2026-09-24 |

---

*Last updated: 2026-09-24*
