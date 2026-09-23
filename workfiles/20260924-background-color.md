# Background Color

> Working document — the band color comes from the image's own background when at least three of the
> borders of its visible part share one uniform color, instead of the image's most frequent color.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

When an image does not fill its cell, the leftover bands (and transparent pixels) are filled with the
image's **dominant color** — today the most frequent color of the whole image
(`Composition/DominantColor.cs`: 64-px nearest-neighbor sample, 4-bit-per-channel buckets, the most
populated bucket averaged), computed once per shown page and stored on the image.

That color is often wrong for images that sit on a background: a product shot on white whose subject
is mostly red gets red bands. The new rule: **if at least three of the four sides of the visible part
of the image carry the same uniform color (identical or very close), that color is the band color**;
otherwise the current majority color stays.

Because the visible part depends on the cell, the zoom and the focus (`FitCalculator.Compute` →
`Fit.Source`), the border color is decided **when the cell is drawn**, not when the image is loaded.

Components involved:

| Component | Role |
|---|---|
| `Composition/DominantColor.cs` | Majority color, unchanged; stays the fallback, called by `BandColor.Of` |
| `Composition/BandColor.cs` | `BandColor.Of(Bitmap)`: 512-px sample of the reference frame plus its `Dominant`; `For(part, size)`: the detection on any part of it |
| `Composition/SourceImage.cs` | `BandColor` property (replaces `Dominant`), built on load and in `ShowPage`, never in `ShowFrame` |
| `Composition/Compositor.cs` | `Frame(Bitmap, BandColor, Look)`; `DrawCell` computes the fit first, maps `Fit.Source` to the bitmap (`BitmapPart`), asks `BandColor.For` |
| `Imaging/GridExport.cs` | `Item` carries the `BandColor`; animated items build it from the frame they export (`BandColor.Of`) |
| `UI/GridPreview.cs` | Builds its `Frame` with `image.BandColor` |
| `README.md` | *Fitting rules* describes the band color (line 140) |

---

## Border Background Detection

Agreed with the user (Q&A #1–#3, #6, #7):

- **A side** is a thin band along one edge of the visible part. It **votes** only if it is
  **uniform**: at least **90 %** of its opaque pixels lie within the tolerance of the side's color.
  A side where the subject touches the edge does not vote.
- **Tolerance** is **moderate**: perceptual distance **ΔE ≤ 10** (CIELAB, ΔE76) — absorbs JPEG noise
  and slight gradients; two slightly different off-whites match.
- **Transparent borders do not vote**: a side whose band is mostly transparent does not vote; with
  fewer than three voting sides the majority color stays, as today.
- **Visible part**: the detection runs on the borders of the part of the image the cell shows
  (`Fit.Source`), so it follows the zoom and the focus. Zoomed out (≤ 100 %) the whole image is
  visible, so its real borders are used. At 100 % the threshold crop counts too: always the part
  the cell shows, one rule (Q&A #8).
- **Same frame as today**: the pixels come from the frame the dominant color is computed on today —
  source image, shown page, first frame of an animation in export. Animated content keeps a stable
  color while playing (no flicker), as today.

Proposed algorithm:

1. **Reference sample** (once per shown page, next to `Dominant`): a nearest-neighbor copy of the
   bitmap whose long side is at most **512 px**, kept as a pixel array. Large enough for a 400 %
   zoom to still leave a ~128-px visible region; nearest neighbor keeps real pixel colors.
2. **Visible region** (at draw time): `Fit.Source`, expressed in the oriented image, is mapped back to
   bitmap coordinates through the inverse of `ImageLook.Orientation` (as `DrawOriented` already does),
   then scaled to the sample. Rotations and flips only permute the sides; the rule is symmetric.
3. **Bands**: each side's band is **2 %** of the perpendicular dimension of the region, at least 1 px.
   Corners belong to both adjacent bands.
4. **Side color**: mean color (RGB) of the band's opaque pixels (alpha ≥ 128), compared in CIELAB.
   The side is uniform when ≥ 90 % of those pixels are within ΔE 10 of that mean; a band with fewer
   than half its pixels opaque does not vote.
5. **Agreement**: among uniform sides, the largest group whose colors are pairwise within ΔE 10.
   A group of **3 or 4** sides wins; its color is the mean of the group's side colors.
   A 2 + 2 split, or fewer than 3 uniform sides → no border background.
6. **Result**: the border background when found, otherwise `Dominant` — the whole image's majority
   color, as today, stable when zooming (Q&A #9). Black & white is applied on top by the compositor,
   as today.

Cost: only band pixels are read — a few thousand per cell. The last answer is still kept (one entry,
keyed by the part in sample pixels), since the preview redraws the same part at every animation tick;
it is swapped as a whole, so an export thread can share the `BandColor` of a still image with the
preview. Memory: at most 512 × 512 × 4 B = 1 MB per image for the sample.

---

## README

*Fitting rules*, line 140: "filled with the dominant color of the whole image" becomes a description of
the new rule — the background of the visible part when at least three of its sides share one uniform
color, otherwise the image's most frequent color.

---

## Test Impact

No unit test is created or updated: the user chose to keep the solution test-free (Q&A #5), as every
previous workfile did. The detection (side uniformity, 3-of-4 agreement, 2 + 2 split, transparent
sides, visible-region mapping) stays untested by decision, not because nothing testable changes.
Verification is manual, on a few images: product on white, subject touching one edge, JPEG with noisy
background, transparent PNG, zoomed image.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| None — test-free by decision (Q&A #5) | — | — |

---

## Open Questions

- [x] ~~Unit tests: keep the solution test-free as before, or create a test project for the detection?~~
      → Test-free, manual checks
- [x] ~~Transparent borders (a PNG logo on transparency): a mostly transparent side does not vote
      (falls back to the majority color), or transparency counts as a background?~~ → Does not vote
- [x] ~~Zoom around a focus: detect on the source image's borders (as the majority color today), or on
      the borders of the zoomed visible area?~~ → Borders of the zoomed visible area
- [x] ~~Visible part at 100 %: the threshold crop already hides up to 7.5 % per side on the overflowing
      axis. Detect on the visible part always (one rule), or on the whole image unless zoomed in?~~
      → Always the visible part
- [x] ~~Fallback when the visible borders do not agree: the whole image's majority color (as today), or
      the majority color of the visible part?~~ → The whole image's majority color

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

### Iteration 2 — 2026-09-24

Answers Q&A #5–#7: test-free, transparent sides do not vote, detection on the **visible zoomed part**.
The last answer moves the detection out of `DominantColor.Compute`: the visible part depends on cell,
zoom and focus, so it is decided in `Compositor.DrawCell`. Each image keeps a 512-px reference sample
of the frame its dominant color comes from (load / `ShowPage`, first frame in export), carried by
`Frame` and `GridExport.Item`; the visible `Fit.Source` is mapped back to the sample through the
inverse orientation. Keeps Q&A #3 (stable color while an animation plays). Two new questions: the
threshold crop at 100 %, and the fallback color.

### Iteration 3 — 2026-09-24

Answers Q&A #8–#9: the detection always uses the part the cell shows, threshold crop included, zoomed
or not; when the visible borders do not agree, the band color falls back to the whole image's majority
color, unchanged. No open question remains.

### Iteration 4 — 2026-09-24 — ✅ Implemented

Go given ("GO", read as code and README: the README section belongs to the frozen design; unit tests
not applicable by decision). Branch: stays on `main`, the project's standing choice. Scope frozen as
described in the sections above.

### Iteration 5 — 2026-09-24 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Scope of "GO"**: read as code + README — the README section belongs to the frozen design; unit
  tests not applicable (Q&A #5).
- **Names**: the class is `BandColor`; it replaces `Color Dominant` in `Frame`, `SourceImage` and
  `GridExport.Item` (property `BandColor`), and keeps the whole-image majority as `BandColor.Dominant`.
  `DominantColor` itself is unchanged.
- **One-entry cache** in `BandColor.For` — the design said none was needed; added because the preview
  asks for every cell at every animation tick, and cheap to keep thread-safe (immutable record swapped
  as a whole).
- **Mostly transparent**: fewer than half of the band's pixels opaque; a pixel is opaque from alpha 128.
- **Colors**: a side's color is the RGB mean of its opaque pixels; the winning color is the mean of the
  agreeing sides' colors; distances use CIELAB ΔE76 on a linearized sRGB → XYZ (D65) conversion.
- **Sample**: drawn with source-copy compositing so alpha survives; the part shown is rounded to whole
  sample pixels. `Compositor.BitmapPart` is extracted from `DrawOriented`, which now receives it.
- **Manual check** (no test project): a throwaway console in the scratchpad ran `BandColor` on synthetic
  images — white background → white, even with the subject touching one edge; subject touching two
  opposite edges, a 2 + 2 split or a transparent background → majority color; noisy off-white and a
  white/off-white mix → the background; zoomed into the subject → the subject's color at the visible
  borders.
- **Commits** waited twice for another session's merge in the shared `main` checkout; its uncommitted
  files were never staged.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-09-24 | `BandColor` class, then compositor / source image / export / preview wiring; build clean. Throwaway sanity check on synthetic images (scratchpad, not in the repo) — see Iteration 5 |
| Unit tests | 4 | 2026-09-24 | Not applicable — test-free by decision (Q&A #5) |
| README | 4 | 2026-09-24 | *Fitting rules*: the band color rule |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What counts as a side's color? | Thin band, uniform (≥ ~90 % of pixels close); a side where content touches the edge does not vote | 2026-09-24 |
| 2 | How tolerant is "very close"? | Moderate (~ΔE 10) | 2026-09-24 |
| 3 | Animated content: which frame does the border rule use? | The same frame as the current majority color | 2026-09-24 |
| 4 | Exploration depth? | Straightforward | 2026-09-24 |
| 5 | Unit tests: stay test-free, or create a test project? | Test-free | 2026-09-24 |
| 6 | Transparent borders: no vote, or transparency counts? | No vote | 2026-09-24 |
| 7 | Zoom: source borders or zoomed-area borders? | Zoomed visible area | 2026-09-24 |
| 8 | Visible part at 100 % (threshold crop): always the visible part, or the whole image unless zoomed in? | Always the visible part | 2026-09-24 |
| 9 | Fallback: whole-image majority color, or visible-part majority color? | Whole-image majority color | 2026-09-24 |

---

*Last updated: 2026-09-24*
