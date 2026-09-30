# Crop Effect

> Working document — a new cell effect, Crop, zoned with four bars like the Blur.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A new **cell effect**, **Crop** (*recadrage*), in the effects toolbar. Four bars — the Blur's
zoning mechanics — delimit the part of the image that is kept; that part **becomes the image**:
it is fitted into the cell by the ordinary fitting rule and filled out to the cell, every other
effect applying to it as it would to a complete image.

Out of scope: grouping several effects under one tab (considered, then dropped by the user — see
Q&A 2).

Relevant components:

| Component | Role |
|---|---|
| `Composition/BlurEffect.cs` | The model to mirror: four sides in fractions, `WithSide` with a min gap, `Area` |
| `Composition/ImageLook.cs` | The effect's state: `ImageEffect` enum, `TurnOn` / `TurnOff` / `Reset`, kept settings |
| `Composition/Compositor.cs` | `Frame.Size` (read by the canvas sizing), `DrawCell`, `AutomaticBackground` |
| `Composition/FitCalculator.cs` | The fitting rule, applied unchanged to the cropped part |
| `Composition/BandColor.cs` | `For(part, size)`: the automatic background of the part shown |
| `UI/GridPreview.cs` | The blur bars: hit-testing, dragging, snapping, painting — to generalise to the crop bars |
| `UI/MainForm.cs`, `UI/EffectIcons.cs` | The effect tab, its options, its icon |

---

## Behaviour

### Agreed

- **Crop and fill** (Q&A 1): the part inside the four bars is kept, the rest is discarded, and the
  kept part fills the cell.
- **As if it were a complete image** (Q&A 5): the kept part goes through the **ordinary fitting
  rule** (`FitCalculator` — fills the cell, cropping at most 15 % along the overflowing axis, bands
  beyond), and the **automatic background** is computed on the kept part with the same rules as on
  a whole image (`BandColor.For` on that part).
- The background color is recomputed **live** while a bar is dragged.
- The zoning works **like the Blur's**: four bars (left, top, right, bottom), each dragged on its
  own, kept apart by a minimum gap so they never cross, snapping exactly onto the edge within
  6 logical px (RULES.md § On-Cell Handles), drawn as helper indicators (fluorescent green).

- **Edit view** (Q&A 6): while the Crop tab is selected and the effect is on, the selected cell
  shows the **whole oriented image**, fitted whole (contain, no zoom, no fine angle) so every edge
  is reachable, the discarded part dimmed, the four bars on the kept part's edges; the cell's
  background already takes the kept part's color, live. Everywhere else — other cells, another
  tab, the exports — the cropped result is drawn.
- **Follows the image** (Q&A 7): the sides are stored in **fractions of the image**, so the crop
  keeps the same content when the image is turned or flipped, and survives resizing and layout
  changes. A Crop exception to RULES.md § Scope and State (*geometry in fractions of the cell*).
- **The canvas follows the kept part** (Q&A 8), as it follows a rotation: `Frame.Size` becomes the
  cropped part's oriented size, so the canvas sizing — preview and exports — treats the kept part
  as the image. The grid's proportions change live while a bar is dragged.
- **Default zone** (Q&A 9): **10 % cut off each edge** — the effect shows as soon as it is turned
  on, the bars easy to grab. Turned off, it is drawn as the whole image.

- **Pipeline** (Q&A 11): source image → orientation (rotate / flip) → **crop** → fitting rule,
  zoom, focus and fine angle → background → black & white, blur on the cell. Zoom, Rotate's fine
  angle and Blur thus apply to the cropped image; the Blur's bars stay in the cell's frame,
  unchanged.
- **Tab position** (Q&A 12): **between Background and Zoom** — the first geometry step, in the
  enum's order *background, geometry, rendering, sound*.

### Options Toolbar

Agreed (Q&A 10): **aspect-ratio buttons**, each showing a **preview of its format** — a small
rectangle drawn at that ratio in the button — then the effect's own **Reset**, ending the row.

| Button | Ratio kept (width : height, as the image is seen) |
|---|---|
| Free | None — each bar moves on its own (the default) |
| 1:1 | 1 : 1 |
| 4:3 | 4 : 3 |
| 16:9 | 16 : 9 |
| 9:16 | 9 : 16 |

The buttons are exclusive, like the Blur's Gaussian / Pixelate pair. Proposed details, to be taken
by the run if nothing is said:

- **Picking a ratio** reshapes the current zone to the **largest rectangle at that ratio** that
  fits inside it, **around its center** — it never grows past the zone nor the image.
- **Dragging a bar under a ratio** moves that bar; the two bars across follow **around the zone's
  center on the other axis**, so the ratio holds. The drag stops where the other axis would leave
  the image.
- The ratio is measured in **pixels of the image as seen** (not in fractions), so a 1:1 crop is
  square whatever the image's shape.
- The ratio is part of the effect's **settings**: kept while off, back to *Free* on every Reset.
  Picking one is *acting on an option*, so it turns the effect on (RULES.md § Options Toolbar).
- A **quarter turn** of the image turns the kept part with it (it follows the image), so a 16:9
  crop becomes 9:16 as seen, and the selected button follows (16:9 ↔ 9:16; 4:3 has no 3:4 button,
  so it becomes *Free* with the zone unchanged).

### State and Lifecycle

The general effect rules apply (RULES.md § Effects), nothing special:

| Event | Crop |
|---|---|
| The cell's image is replaced | Reset — default state |
| An image is deleted | Reset for the images shifting into another cell |
| Two cells are swapped | Kept — follows the image |
| The layout changes | Kept |
| Its own *Reset*, the toolbar's *Reset* | Reset |
| Turned off | Settings kept, drawn as its default (the whole image) |

---

## Rules and Documentation to Update

- **RULES.md** — § Scope and State: a *Crop Exception* subsection — its sides in fractions of the
  image, following it when turned or flipped; the kept part treated as the image by the fitting
  rule, the automatic background and the canvas sizing; the edit view.
- **GLOSSARY.md** — *Effect*: Crop added to the list; a *Crop* entry.
- **README.md** — the effects section: the Crop effect.

---

## Test Impact

The repository holds **no test project**: nothing testable by unit tests is created or updated.
The behaviour is checked by hand in the launched app (and by a scripted checker in the scratchpad
if the run needs one).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~1. While the bars are being adjusted, what does the cell show?~~ → The edit view: the whole
  image, the discarded part dimmed, the background already in the kept part's color
- [x] ~~2. Does the crop follow the image's content when it is turned or flipped?~~ → Yes, sides in
  fractions of the image (a Crop exception in RULES.md)
- [x] ~~3. Does the canvas sizing follow the cropped part?~~ → Yes, like a rotation, live
- [x] ~~4. What zone does the crop start with when it is turned on?~~ → 10 % cut off each edge
- [x] ~~5. What does the options toolbar hold besides the Reset button?~~ → Aspect-ratio buttons
  (Free, 1:1, 4:3, 16:9, 9:16), each previewing its format

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-30

Initial design. Scoping batch answered: crop **and fill** (not a mask, not a choice between the
two); effect grouping **dropped**; the subject **straightforward** (single exploration pass, done
directly — the questions chained on the Blur's mechanics). Then the user's precision: the kept part
is treated **as a complete image** — the fitting rule and the automatic background apply to it,
the color following the bars live. From that, the pipeline (crop right after orientation, before
the fitting rule), the edit view, the image-fraction frame of reference, the canvas sizing and the
tab position are proposed, pending Open Questions 1–5.

### Iteration 2 — 2026-09-30

Open Questions 1–4 answered, each on the recommended or first option: the **edit view** while the
tab is selected and the effect on; the crop **follows the image** (fractions of the image, a
RULES.md exception); the **canvas follows the kept part**, live, like a rotation; the default zone
**10 % in from each edge**. Open Question 5 (the options toolbar's content) remains.

### Iteration 3 — 2026-09-30

Open Question 5 answered: the options toolbar holds **aspect-ratio buttons**, each drawing a
preview of its format, before the Reset. The proposed pipeline (crop before the fitting rule,
zoom, fine angle and blur) and the tab position (between Background and Zoom) confirmed. The
ratios' behaviour — picking one, dragging under one, the pixel measure, a quarter turn — written
as proposed details. No Open Question remains.

### Iteration 4 — 2026-09-30 — ✅ Implemented

Go given: code, tests and documentation (no test project — the unit-test step does not apply).
The scope is frozen as the sections above stand. The work lands on `main` (the user's standing
preference for this app).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Does not apply — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What becomes of the cell when cropping — mask the outside, crop and fill, or a choice? | Crop and fill | 2026-09-30 |
| 2 | Is grouping effects under one tab part of this workfile? | No — dropped altogether | 2026-09-30 |
| 3 | Which groupings did you have in mind? | No preference (moot, see 2) | 2026-09-30 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-09-30 |
| 5 | *(user's precision, unprompted)* | The background is computed after the crop, with the same rules as for a complete image, on the kept part; its color changes live as the crop's limits move | 2026-09-30 |
| 6 | While the bars are adjusted, what does the cell show? | The edit view (recommended) | 2026-09-30 |
| 7 | Does the crop follow the image when turned / flipped, or stay fixed in the cell? | Follows the image (recommended) | 2026-09-30 |
| 8 | Does the canvas sizing follow the cropped part? | Yes, like the rotation — live | 2026-09-30 |
| 9 | What zone does the crop start with? | 10 % cut off each edge | 2026-09-30 |
| 10 | What does the options toolbar hold besides Reset? | Aspect ratios imposed, with a preview of the format in each button | 2026-09-30 |
| 11 | Do zoom, fine angle and blur apply to the already cropped image? | Yes, after the crop (recommended) | 2026-09-30 |
| 12 | Where does the Crop tab go? | Between Background and Zoom | 2026-09-30 |

---

*Last updated: 2026-09-30*
