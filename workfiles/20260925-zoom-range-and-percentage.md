# Zoom Range and Percentage

> Working document — widen the zoom range of a cell, show the zoom percentage in the top-right
> corner of the cell while it changes, in fluorescent green, and make fluorescent green the rule
> for every helper indicator drawn over a cell in the preview.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Three deliverables:

1. **Wider zoom range** — today a cell zooms from 50 % to 400 %; both bounds move outwards.
2. **Zoom percentage** — while the zoom of a cell changes, its value (e.g. `119 %`) shows in the
   **top-right corner of that cell**, in fluorescent green, then fades out.
3. **Rule** — `RULES.md` gains a rule: every **helper indicator** drawn over a cell in the preview
   is fluorescent green. It covers **existing indicators too** (Q&A #3): those that break it are
   recolored by this workfile.

Scope agreed with the user (Q&A #1–#4):

- **Range**: the agent proposes figures after reading the code (see *Zoom Range*, Open Question 1).
- **Percentage display**: visible from the first change, held ~1 s after the last one, then fades
  out. **Preview only**, never in the exports.
- **Rule**: applies to every helper indicator, existing ones included.
- Exploration depth: straightforward — a single scout pass.

Relevant components: `Composition/ImageLook.cs` (`MinZoom`, `MaxZoom`, `WithZoom`),
`UI/GridPreview.cs` (`ZoomAt`, `ZoomTo`, `ZoomTrack`, `OnPaint`, `BarColor`, `PaintBlurBars`,
`PaintPanGuides`, `CloseBounds`, `_wheelEnd`), `README.md`, `RULES.md`, `GLOSSARY.md`.

---

## Current Behaviour (findings)

- **Bounds**: `ImageLook.MinZoom = 0.5`, `MaxZoom = 4` (`ImageLook.cs:12-13`), enforced in one
  place, `WithZoom` (`Math.Clamp`, `ImageLook.cs:79`). Every zoom path goes through it.
- **What 100 % means**: `Zoom = 1` is the automatic fit of the cell (fill, cropping at most the
  crop threshold — `FitCalculator.Scale`); the zoom multiplies on top of it. It is **not** the native
  pixel size.
- **Inputs changing the zoom**:
  - mouse wheel over a cell — `OnMouseWheel` → `ZoomAt` (`GridPreview.cs:1500-1526`), multiplicative,
    `NotchesPerDoubling = 4`, crossing 100 % stops on it;
  - the zoom slider along the cell's left edge while hovered — `ZoomTo` (`GridPreview.cs:1468`),
    log2 scale between `MinZoom` and `MaxZoom` (`ZoomTrack`, `ZoomFraction`, `ZoomY`), snaps to 100 %;
  - no keyboard shortcut; the hover toolbar's *Reset* resets it with the other actions.
- **Moved image**: `FitCalculator.MinCoveredShare = 0.1` keeps `min(10 % of the cell, drawn size)`
  covered **per axis** — a smaller minimum zoom does not conflict with it.
- **Top-right corner**: it holds the hover toolbar's **close button** (`CloseBounds`: 24 px, 6 px
  inset). The wheel zooms under the mouse, so the cell is hovered and that button is shown while
  zooming — the percentage cannot sit exactly on it (Open Question 2).
- **Helper indicators today** (all drawn in `GridPreview.OnPaint` after the cached composition is
  blitted, so preview-only by construction):

  | Indicator | Where | Colour |
  |---|---|---|
  | Blur bars | `PaintBlurBars` | `BarColor` (57, 255, 20) over a black halo (160, 0, 0, 0) |
  | Blur bar grips | `PaintBarGrip` | `BarColor`, **white when hovered** |
  | Magnetic-stop guides | `PaintPanGuides` | `BarColor` over the black halo, dashed / solid |
  | Selection outline | `OnPaint` | `SystemColors.Highlight` |
  | Drop-target highlight | `OnPaint` | translucent `SystemColors.Highlight` fill |
  | Dragged-from cell dim | `OnPaint` | translucent black fill |
  | Hover outline | `PaintHoverOutline` | translucent white |
  | Empty-state drop zone | canvas-level dashed border | white / `ForeColor` |

  `BarColor` is the one shared fluorescent green; its comment ties it to the blur bars only.
- **No fade mechanism** exists; `System.Windows.Forms.Timer` is already used (`_wheelEnd`,
  `WheelEndDelay = 150` ms, and `_carouselTimer`).
- **Tests**: the solution has **no test project**.
- **README**: line 20 states "Zoom a cell from 50 % to 400 %".

---

## Zoom Range

- **Proposal: 25 % → 1600 %** (`MinZoom = 0.25`, `MaxZoom = 16`) — powers of two, so the slider's
  log scale keeps whole octaves: 6 doublings instead of 3, 24 wheel notches end to end
  (`NotchesPerDoubling` unchanged).
- The slider keeps its height: each doubling takes half the length it takes today. 100 % stays at
  a third of the track from the bottom (2 octaves below, 4 above — as 1 below, 2 above today); its
  snap is unchanged.
- The `ZoomTrack` comment ("50 % → 100 % and each doubling take the same length") is updated.
- The bounds stay in `ImageLook` only; `WithZoom` stays the single clamp.

---

## Zoom Percentage

- **Trigger**: every zoom change on a cell — wheel (`ZoomAt`) and slider (`ZoomTo`) alike. Not a
  *Reset* click (it is not a zoom gesture). Shown on the **cell being zoomed**, selected or not.
- **Value**: `Zoom × 100`, rounded to the unit, formatted `119 %`. It reads `100 %` when the zoom
  stops on it.
- **Lifetime**: fully opaque while the zoom changes; held **1 s** after the last change, then fades
  out over **300 ms**. A new change during the fade brings it back to full opacity. Zooming another
  cell moves it there at once (one badge at a time).
- **Look**: fluorescent green text over the **black halo** used by the bars and guides (a text
  outline via a `GraphicsPath`), bold, fixed logical size scaled with `LogicalToDeviceUnits` — a
  helper indicator, not part of the composition, so it does not scale with the cell.
- **Position**: top-right corner of the cell, right-aligned — exact placement relative to the close
  button: Open Question 2.
- **Drawing**: a `PaintZoomBadge` method called from `OnPaint` beside `PaintBlurBars` /
  `PaintPanGuides`; never in `Compositor`, so never in the exports nor the video export.
- **Fade**: a dedicated `System.Windows.Forms.Timer` (~30 ms ticks during the fade only), each tick
  invalidating the badge area only; stopped once transparent.

---

## Helper Indicator Rule

- **New section in `RULES.md`** — *On-Cell Helper Indicators*:
  - every helper indicator drawn over a cell in the preview (guides, handles, readouts such as the
    zoom percentage) is **fluorescent green** (57, 255, 20), over the black halo that keeps it
    legible on any image;
  - it is drawn in the preview only, never in the exports;
  - the colour is defined **once** and shared.
- **`GLOSSARY.md`**: new term *Helper indicator (indicateur d'aide)* — definition per Open
  Question 3.
- **Code**: `BarColor` becomes `HelperColor` (and the black halo a shared `HelperHalo`), its comment
  generalised; every helper indicator uses them.
- **Existing indicators recolored**: per Open Questions 3 and 4.

---

## Test Impact

**Nothing to test** — the solution has no test project, and creating one is outside this scope
(every previous workfile stayed test-free). The bounds are constants; the badge is paint-only.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [ ] **Range**: 25 % → 1600 % as proposed, or another range (e.g. 10 % → 1000 %, 20 % → 800 %)?
- [ ] **Placement vs the close button**: the cell is hovered while zooming, so the close button is
  shown in the top-right corner. Badge **just below** the close button, **to its left** on the
  same row, or **hide the close button** while the badge shows?
- [ ] **What counts as a helper indicator**: only the measure / geometry aids (guides, handles,
  readouts — already green except the hovered grip), or also the interaction feedback (selection
  outline, drop-target highlight, hover outline, drag dim) — which would then be recolored green?
- [ ] **Hovered blur grip**: it turns white when hovered. Keep that hover feedback, or make it a
  green variant (e.g. a brighter / filled green)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Initial design from the request ("widen zoom min/max, show the % top-right in the cell while
zooming, in fluorescent green"), the follow-up request (a rule: every helper indicator drawn over a
cell in the preview is fluorescent green), the scoping answers (Q&A #1–#4) and the scout pass:
range proposed at 25 % → 1600 %, a fading badge drawn from `OnPaint` only, a shared `HelperColor`,
the rule in `RULES.md` and the term in `GLOSSARY.md`. Four questions left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |
| RULES.md / GLOSSARY.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How far to widen the zoom range? | The agent proposes figures after reading the code | 2026-09-25 |
| 2 | How does the percentage appear and disappear? | Visible while zooming, then fades out (~1 s after the last change); preview only | 2026-09-25 |
| 3 | What does the fluorescent-green helper rule cover? | Everything, existing indicators included (recolored in this workfile) | 2026-09-25 |
| 4 | Exploration depth? | Straightforward — single scout pass | 2026-09-25 |
| 5 | Range: 25 % → 1600 % or another? | | 2026-09-25 |
| 6 | Badge placement vs the close button? | | 2026-09-25 |
| 7 | What counts as a helper indicator? | | 2026-09-25 |
| 8 | Hovered blur grip colour? | | 2026-09-25 |

---

*Last updated: 2026-09-25*
