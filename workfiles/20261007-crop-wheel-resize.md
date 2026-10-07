# Crop Wheel Resize

> Working document — the mouse wheel grows or shrinks the Crop's kept part, keeping its current ratio.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

In the **crop edit view** (Crop tab selected, crop on, selected cell), the mouse wheel does nothing
today (`GridPreview.OnMouseWheel` returns on `EditsCrop`). It becomes the way to **grow or shrink
the kept part** without dragging its four bars one by one, its **current ratio kept**:

- a ratio button pressed (1:1, 4:3, 16:9, 9:16) — that ratio;
- **Free** — the kept part's proportions as they stand when the notch arrives.

Components: `Composition/CropEffect.cs` (the geometry), `UI/GridPreview.cs` (the wheel), the
README / README.fr, RULES.md and the glossaries (the edit view's description).

---

## Gesture

| Wheel | Fixed point while the kept part is scaled |
|---|---|
| Plain | The kept part's **center** |
| **Ctrl** held | The **point of the image under the cursor** — clamped to the image when the cursor is on a band around it |

- **Where**: anywhere over the cell showing the edit view — inside the kept part, on the dimmed
  part, on the bands. Other cells keep the zoom wheel; the selected cell outside the edit view too.
- **Not while** another gesture runs (a press, a bar or kept-part drag, a separator) or the grid is
  locked (export) — like the zoom wheel.
- Fine-grained wheels add up to whole notches, per cell, as the zoom wheel does (`_wheelDelta`).
- Ctrl therefore means *anchor under the cursor* in the edit view, not *finer step*: the zoom's
  fine step does not apply there.
- Acting on it is acting on the effect, which is already on (the edit view only shows while it is
  on) — no activation to handle.

## Geometry

Computed in the image **as seen** (`CropEffect.Seen` / `WithSeen`), in fractions, the ratio read
in pixels of the oriented image as `WithRatio` / `WithSeenSide` do.

- A notch **scales the kept part around the fixed point**: `new = anchor + (old − anchor) × k`, its
  ratio unchanged since both sides scale by `k`. Step size: see Open Questions.
- **Growing — slide, then stop**: the scale is clamped so the kept part fits in the image
  (width ≤ 1 and height ≤ 1 in fractions); the scaled part is then **shifted back inside** the image
  where it crosses an edge, so it keeps growing on the other side. Growth stops at the **largest
  rectangle at that ratio within the image** — reached exactly, not approached.
- **Shrinking — stops at a minimum**: neither side goes below the bars' minimum gap
  (`BarMinGap`, 8 logical px of the edit view's image span, scaled with `LogicalToDeviceUnits`);
  the scale is clamped so the smaller side lands on it.
- A ratio kept by a button stays **exactly** that ratio; a Free kept part keeps its proportions up
  to the rounding of the fractions.
- New member: `CropEffect.ScaledSeen(double factor, PointF anchor, double minWidth, double minHeight, ImageLook look)`
  — anchor and minimums in fractions of the image as seen; `GridPreview` converts the cursor and
  `BarMinGap` through the edit view's span (`ShownBars(...).Span`).

## Interplay

- **Undo history**: a wheel burst is one step by itself — the burst keeps `GridPreview.InGesture`
  true through `_wheelEnd`, as the zoom wheel's does; `BeginLive` / `EndLive` show it live.
- **Restore indicators**: nothing new — the crop's kept-part edges are already in
  `ShowRestored` / `PaintRestored`.
- **Free format**: the kept part's size changes the Free ratio (`OutputFormats.FreeRatio` reads
  `ImageLook.Shown`); whether it is held during a burst — see Open Questions.
- **Fitting rule, automatic background, canvas sizing** follow the kept part as they do for the
  bars, through `SetLook`.

## Documentation

- **README.md / README.fr.md** § Crop: the edit view's sentence *"Elsewhere on that cell, a drag
  and the mouse wheel do nothing"* becomes: the wheel over the cell grows / shrinks the kept part,
  ratio kept, around its center — the point under the cursor with Ctrl; a drag outside the kept
  part still does nothing.
- **RULES.md** § The Crop Exception: the edit view's description gains the wheel.
- **GLOSSARY.md / GLOSSARY.fr.md**: *Crop edit view* gains *the wheel scaling the kept part*.

---

## Test Impact

The app has **no test project** (CONTRIBUTING.md: a change is checked by hand in the running app).
Nothing is created or updated; the behaviours below are checked by hand at delivery.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [ ] Wheel direction: does a notch **up shrink** the kept part (the cropped image zooms in, like the zoom wheel's up) or **grow** it?
- [ ] Step per notch: the kept part's size **× / ÷ a fixed factor** (e.g. 5 % of its current size), or **snapped onto multiples of 5 %** of the largest kept part at that ratio (like the zoom's steps)?
- [ ] Free format during a burst: is the canvas ratio **held until the wheel stops** (like a bar drag), or **recomputed at every notch**?
- [ ] Helper indicator: does a burst show a **size readout** (a green badge, like the zoom's percentage), or do the bars and the dimmed part suffice?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the request and the scoping answers (Q1–Q4): the wheel anywhere over the
cell in the crop edit view scales the kept part, ratio kept, around its center — the point under
the cursor with Ctrl; growing slides along the image's edges, then stops at the largest kept part
at that ratio. Exploration: `OnMouseWheel` currently returns on `EditsCrop`; Ctrl+wheel is the zoom's
fine step elsewhere; no test project. Four open questions: direction, step, Free format during a
burst, readout.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Not applicable: no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where and when does the wheel resize the kept part? | Anywhere over the cell, in the crop edit view | 2026-10-07 |
| 2 | Which point stays fixed while it grows or shrinks? | The kept part's center; the point under the cursor while Ctrl is held | 2026-10-07 |
| 3 | When the growing kept part reaches an image edge? | Slide along it, then stop (the largest kept part at the ratio within the image) | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-10-07 |
| 5 | Wheel direction: up shrinks or grows? | | |
| 6 | Step per notch: fixed factor or snapped multiples of 5 %? | | |
| 7 | Free format during a burst: held or recomputed per notch? | | |
| 8 | Size readout during a burst? | | |

---

*Last updated: 2026-10-07*
