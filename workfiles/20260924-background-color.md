# Background Color

> Working document — the band color comes from the image's own background when at least three of its
> borders share one uniform color, instead of the image's most frequent color.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

When an image is cropped beyond the threshold, the leftover bands (and transparent pixels) are filled
with the image's **dominant color** — today the most frequent color of the whole image
(`Composition/DominantColor.cs`: 64-px nearest-neighbor sample, 4-bit-per-channel buckets, the most
populated bucket averaged).

That color is often wrong for images that sit on a background: a product shot on white whose subject
is mostly red gets red bands. The new rule: **if at least three of the four sides of the image carry
the same uniform color (identical or very close), that color is the band color**; otherwise the
current majority color stays.

Components involved:

| Component | Role |
|---|---|
| `Composition/DominantColor.cs` | `Compute(Bitmap)` — the single entry point; gains the border detection |
| `Composition/SourceImage.cs` | Calls `Compute` on load and on `ShowPage` — unchanged |
| `Imaging/GridExport.cs` | Calls `Compute` on the first frame of animated items — unchanged |
| `Composition/Compositor.cs` | Paints bands with `Frame.Dominant` — unchanged |
| `README.md` | *Fitting rules* describes the band color (line 140) |

---

## Border Background Detection

Agreed with the user (Q&A #1–#3):

- **A side** is a thin band along one edge of the image. It **votes** only if it is **uniform**:
  at least **90 %** of its opaque pixels lie within the tolerance of the side's color. A side where the
  subject touches the edge does not vote.
- **Tolerance** is **moderate**: perceptual distance **ΔE ≤ 10** (CIELAB, ΔE76) — absorbs JPEG noise
  and slight gradients; two slightly different off-whites match.
- **Same frame as today**: the detection runs on exactly the bitmap `Compute` already receives —
  source image, shown page, first frame of an animation in export. Animated content keeps its color
  while playing, as today.

Proposed algorithm (Iteration 1):

1. **Sample**: draw the image into a nearest-neighbor copy whose long side is at most **256 px**
   (the 64-px sample of the majority color is too coarse for thin borders). Nearest neighbor keeps
   real pixel colors.
2. **Bands**: each side's band is **2 %** of the perpendicular dimension of the sample, at least 1 px.
   Corners belong to both adjacent bands.
3. **Side color**: mean color of the band's opaque pixels. The side is **uniform** when ≥ 90 % of those
   pixels are within ΔE 10 of that mean. A side with too few opaque pixels does not vote (see Open
   Questions).
4. **Agreement**: among uniform sides, find the largest group whose colors are pairwise within ΔE 10.
   A group of **3 or 4** sides wins; its color is the mean of the group's side colors.
   A 2 + 2 split, or fewer than 3 uniform sides → no border background.
5. **Result**: the border background when found, otherwise the current majority color, unchanged.

The public signature `DominantColor.Compute(Bitmap) → Color` stays, so every caller benefits without
change. The class summary is updated to describe the two-step rule.

Unaffected by an image's look: quarter-turn rotations and flips permute the sides (the rule is
symmetric), black & white is applied to the result by the compositor as today (see Open Questions for
the zoom).

---

## README

*Fitting rules*, line 140: "filled with the dominant color of the whole image" becomes a description of
the new rule — the image's border background when at least three sides share one uniform color,
otherwise its most frequent color.

---

## Test Impact

No test project exists; every previous workfile kept the solution test-free by decision. To confirm
for this one (see Open Questions) — the detection is pure logic and would be the first genuinely
unit-testable piece.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *Pending the Open Question on unit tests* | | |

---

## Open Questions

- [ ] Unit tests: keep the solution test-free as before, or create a test project for the detection?
- [ ] Transparent borders (a PNG logo on transparency): a mostly transparent side does not vote
      (falls back to the majority color), or transparency counts as a background?
- [ ] Zoom around a focus: detect on the source image's borders (as the majority color today), or on
      the borders of the zoomed visible area?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial design from the user's request and the scoping batch (Q&A #1–#4): thin uniform side bands
(≥ 90 % within tolerance), moderate tolerance (ΔE ≤ 10), same frame as the current majority color,
straightforward exploration. Detection lives inside `DominantColor.Compute` on a 256-px sample with
2 % bands; 3 or 4 agreeing sides win, otherwise the majority color stays. Three questions left open:
unit tests, transparent borders, zoom.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What counts as a side's color? | Thin band, uniform (≥ ~90 % of pixels close); a side where content touches the edge does not vote | 2026-09-24 |
| 2 | How tolerant is "very close"? | Moderate (~ΔE 10) | 2026-09-24 |
| 3 | Animated content: which frame does the border rule use? | The same frame as the current majority color | 2026-09-24 |
| 4 | Exploration depth? | Straightforward | 2026-09-24 |
| 5 | Unit tests: stay test-free, or create a test project? | | 2026-09-24 |
| 6 | Transparent borders: no vote, or transparency counts? | | 2026-09-24 |
| 7 | Zoom: source borders or zoomed-area borders? | | 2026-09-24 |

---

*Last updated: 2026-09-24*
