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

### Proposed — pending the Open Questions

- **Pipeline**: source image → orientation (rotate / flip) → **crop** → fitting rule, zoom, focus
  and fine angle → background → black & white, blur on the cell. Zoom, Rotate's fine angle and
  Blur thus apply to the cropped image; the Blur's bars stay in the cell's frame, unchanged.
- **Edit view** (Open Question 1): while the Crop tab is selected and the effect is on, the selected
  cell shows the **whole oriented image**, fitted whole (contain, no zoom, no fine angle) so every
  edge is reachable, the discarded part dimmed, the four bars on the kept part's edges; the cell's
  background already takes the kept part's color, live. Everywhere else — other cells, another tab,
  the exports — the cropped result is drawn.
- **Frame of reference** (Open Question 2): the sides stored in **fractions of the image**, so the
  crop follows the image's content when it is turned or flipped, and survives resizing and layout
  changes. This departs from RULES.md § Scope and State (*geometry in fractions of the cell*),
  which would get a Crop exception.
- **Canvas sizing** (Open Question 3): `Frame.Size` becomes the cropped part's oriented size, so the
  canvas sizing treats the kept part as the image, as it already does with rotation.
- **Default settings** (Open Question 4).
- **Options toolbar** (Open Question 5): at least the effect's own **Reset**.
- **Tab position**: between Background and Zoom — the first geometry step, in the enum's order
  *background, geometry, rendering, sound*.

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

- **RULES.md** — § Scope and State: a Crop exception to *geometry in fractions of the cell*, if
  Open Question 2 goes that way.
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

- [ ] 1. While the bars are being adjusted, what does the cell show — the whole image with the
  discarded part dimmed (edit view), or something else?
- [ ] 2. Does the crop follow the image's content when it is turned or flipped (sides in fractions
  of the image), or stay fixed in the cell?
- [ ] 3. Does the canvas sizing (and so the exported grid's proportions) follow the cropped part,
  as it follows a rotation, or keep the whole image's proportions?
- [ ] 4. What zone does the crop start with when it is turned on?
- [ ] 5. What does the options toolbar hold besides the Reset button?

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
| 6 | While the bars are adjusted, what does the cell show? | | |
| 7 | Does the crop follow the image when turned / flipped, or stay fixed in the cell? | | |
| 8 | Does the canvas sizing follow the cropped part? | | |
| 9 | What zone does the crop start with? | | |

---

*Last updated: 2026-09-30*
