# Zone Wheel Resize

> Working document — the mouse wheel grows or shrinks a resizable green zone (the Crop's kept
> part, the Blur's zone), keeping its current ratio.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A **resizable zone** is a rectangle set by the four fluorescent green bars of an effect, drawn as
handles on the selected cell while that effect's tab is selected and the effect is on
(`GridPreview.ShownBars`). There are two today:

| Zone | Effect | Its **bounds** — what it lives in | Ratio kept |
|---|---|---|---|
| The **kept part** | Crop (edit view) | The **image as seen** (`CropEffect.Seen` fractions) | A ratio button pressed (1:1, 4:3, 16:9, 9:16) — that ratio; **Free** — its current proportions |
| The **sharp rectangle** (the bands around it blurred) | Blur (gaussian or pixelate) | The **cell** (`BlurEffect` fractions) | No ratio buttons — its current proportions |

The mouse wheel becomes the way to **grow or shrink the zone** without dragging its four bars one
by one, its **current ratio kept**. The rule is written once for every resizable zone, so a future
one (any effect showing four bars) gets it by itself. The four-corner drag designed in parallel
(`workfiles/20261007-crop-corner-drag.md`) is widened to every resizable zone the same way (user
request, Iteration 3).

Today the wheel does nothing in the crop edit view (`GridPreview.OnMouseWheel` returns on
`EditsCrop`), and zooms the image under the Blur's bars.

Components: a shared scaling helper in `Composition/`, `Composition/CropEffect.cs`,
`Composition/BlurEffect.cs`, `UI/GridPreview.cs` (the wheel), the README / README.fr, RULES.md and
the glossaries.

---

## Gesture

| Wheel | Fixed point while the zone is scaled |
|---|---|
| Plain | The zone's **center** |
| **Ctrl** held | The **point under the cursor** — clamped to the zone's bounds (the image for the crop, when the cursor is on a band around it) |

- **Where — one rule for every zone**: while the selected tab is one with a resizable zone and its
  bars show (the effect on, `ShownBars(selected)`), the wheel **anywhere over the selected cell**
  acts on the zone — inside it, outside it, on the crop's dimmed part and bands. The image's zoom
  wheel no longer works on that cell then: it works on the other cells, from any other tab, or with
  the Zoom slider. With the effect off (no bars), the wheel zooms as before.
  - Crop: the cell showing the edit view (it returned on `EditsCrop` until now).
  - Blur: the selected cell while the Blur tab is selected and the blur on (it zoomed until now).
- **Not while** another gesture runs (a press, a bar or kept-part drag, a separator) or the grid is
  locked (export) — like the zoom wheel.
- Fine-grained wheels add up to whole notches, per cell, as the zoom wheel does (`_wheelDelta`).
- Ctrl therefore means *anchor under the cursor* over a zone being edited, not *finer step*: the
  zoom's fine step does not apply there.
- Acting on it is acting on the effect, which is already on (the bars only show while it is on) —
  no activation to handle.

## Geometry

Computed in **fractions of the zone's bounds** — the image as seen for the crop
(`CropEffect.Seen` / `WithSeen`), the cell for the blur. The bounds keep their pixel size during a
notch, so scaling both sides of a rectangle in fractions by the same `k` keeps its pixel ratio:
the geometry is **one shared helper** for every zone, in `Composition/`.

- A notch **scales the zone around the fixed point**: `new = anchor + (old − anchor) × k`, its
  ratio unchanged since both sides scale by `k`.
- **Direction and step**: a notch **up grows** the zone (more of the image kept, more of the cell
  kept sharp), a notch down shrinks it; each notch scales its sides by a **fixed factor**, `k = 1.05`
  up, `1 / 1.05` down (`k = 1.05^notches` for several notches at once) — no snapping onto
  multiples.
- **Growing — slide, then stop**: the scale is clamped so the zone fits in its bounds
  (width ≤ 1 and height ≤ 1 in fractions); the scaled zone is then **shifted back inside** where it
  crosses an edge, so it keeps growing on the other side. Growth stops at the **largest rectangle
  at that ratio within the bounds** — reached exactly, not approached.
- **Shrinking — stops at a minimum**: neither side goes below the bars' minimum gap
  (`BarMinGap`, 8 logical px of the bars' span — the edit view's image for the crop, the cell for
  the blur — scaled with `LogicalToDeviceUnits`); the scale is clamped so the smaller side lands on
  it.
- A ratio kept by a crop button stays **exactly** that ratio; a Free kept part and the sharp rectangle
  keep their proportions up to the rounding of the fractions.
- A notch down never grows a zone already below the minimum (it stays as it is).
- New members: the shared helper `ZoneScale.Scaled(RectangleF zone, int notches, PointF? anchor, double minWidth, double minHeight)`
  (`Composition/ZoneScale.cs`, all in fractions of the bounds, a `null` anchor meaning the zone's
  center, `ZoneScale.NotchFactor` = 1.05), then `CropEffect.ScaledSeen(notches, anchor, minWidth, minHeight, look)`
  and `BlurEffect.Scaled(notches, anchor, minWidth, minHeight)` built on it; `GridPreview.ScaleZone`
  converts the cursor and `BarMinGap` through the bars' span (`ShownBars(...).Span`) and calls the
  effect of the selected tab. `OnMouseWheel` routes a notch there whenever `ShownBars(cell)` is not
  null, to `ZoomAt` otherwise.
- The Free-format hold is the flag `GridPreview._wheelHoldsRatio`, set by a crop notch, read by
  `UpdateRatio`, released by `EndLive` — the wheel's end, or any other gesture taking over — which
  then computes the ratio again.

## Interplay

- **Undo history**: a wheel burst is one step by itself — the burst keeps `GridPreview.InGesture`
  true through `_wheelEnd`, as the zoom wheel's does; `BeginLive` / `EndLive` show it live.
- **Restore indicators**: nothing new — the crop's kept-part edges and the blur bars are already in
  `ShowRestored` / `PaintRestored`.
- **No new helper indicator**: no size readout during a burst — the bars (and the crop's dimmed
  part) already show the zone.
- **Free format — held during a crop burst**: the kept part's size changes the Free ratio
  (`OutputFormats.FreeRatio` reads `ImageLook.Shown`). Like a crop bar drag, the canvas ratio is
  **held while the wheel turns** (`GridPreview.UpdateRatio` returns while a crop wheel burst runs)
  and **computed again when it stops** (the `_wheelEnd` tick), so the canvas does not change shape
  under the mouse. The sharp rectangle does not weigh in the Free ratio: nothing to hold.
- **Fitting rule, automatic background, canvas sizing** follow the kept part as they do for the
  bars, through `SetLook`.

## Documentation

- **README.md / README.fr.md** § Crop: the edit view's sentence *"Elsewhere on that cell, a drag
  and the mouse wheel do nothing"* becomes: the wheel over the cell grows (up) / shrinks (down) the
  kept part by 5 % a notch, ratio kept, around its center — the point under the cursor with Ctrl —
  sliding along the image's edges, then stopping; a drag outside the kept part still does nothing.
  § Format: the Free ratio is also held while the wheel scales a kept part.
  § Blur: the wheel over the selected cell scales its zone the same way, within the cell, while
  the Blur tab is selected and the blur on; § Zoom: *"the mouse wheel … keeps working on every
  cell"* gains the exception — not on the selected cell while a tab with a resizable zone shows
  its bars.
- **RULES.md** § On-Cell Handles: a new rule for every **resizable zone** (four bars) — the wheel
  scales it, ratio kept, center or point under the cursor with Ctrl, sliding then stopping at its
  bounds, the shared helper; § The Crop Exception: the edit view's description gains the wheel;
  § Output Format: the Free ratio held during a crop wheel burst too.
- **GLOSSARY.md / GLOSSARY.fr.md**: *Crop edit view* gains *the wheel scaling the kept part*;
  *Free format* is held during a crop wheel burst too; a new term, *Resizable zone* (*zone
  dimensionnable*), the one RULES.md names.

---

## Test Impact

The app has **no test project** (CONTRIBUTING.md: a change is checked by hand in the running app).
Nothing is created or updated; the behaviours below are checked by hand at delivery.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [x] ~~Wheel direction: does a notch **up shrink** the kept part (the cropped image zooms in, like the zoom wheel's up) or **grow** it?~~ → Up grows, down shrinks
- [x] ~~Step per notch: the kept part's size **× / ÷ a fixed factor** (e.g. 5 % of its current size), or **snapped onto multiples of 5 %** of the largest kept part at that ratio (like the zoom's steps)?~~ → Fixed factor: × 1.05 up, ÷ 1.05 down
- [x] ~~Free format during a burst: is the canvas ratio **held until the wheel stops** (like a bar drag), or **recomputed at every notch**?~~ → Held until the wheel stops, computed again then
- [x] ~~Helper indicator: does a burst show a **size readout** (a green badge, like the zoom's percentage), or do the bars and the dimmed part suffice?~~ → No readout: the bars and the dimmed part suffice
- [x] ~~Blur zone vs zoom wheel: while the Blur tab is selected and the blur on, the wheel over the selected cell zooms the image today. Does it scale the blur zone **anywhere over the cell** (the zoom then only from other tabs, the slider, or other cells — as in the crop edit view), or **only over the blurred zone**, the zoom wheel kept elsewhere on the cell?~~ → Whenever a tab with a resizable zone is selected (its bars showing), the wheel over the selected cell resizes the zone, anywhere on the cell

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

### Iteration 2 — 2026-10-07

Open questions answered (Q5–Q8): a notch up grows the kept part, down shrinks it; each notch scales
it by a fixed factor of 1.05; the Free format's ratio is held during a burst and computed again
when the wheel stops; no size readout. Gesture, Geometry, Interplay and Documentation updated —
the Free-format hold also reaches RULES.md § Output Format, the README § Format and the glossaries.

### Iteration 3 — 2026-10-07

User request: what this workfile defines — and what `workfiles/20261007-crop-corner-drag.md`
defines in parallel — holds for **every resizable green zone**, the Blur / pixelation zone
included, not only the Crop's kept part. Scope widened: the workfile becomes *Zone Wheel Resize*
(file name kept), the geometry moves to one shared helper in fractions of the zone's bounds (the
image as seen for the crop, the cell for the blur), and RULES.md § On-Cell Handles gets a rule for
every resizable zone. The corner-drag session was informed by message. New open question: the
blur zone's wheel against the zoom wheel on the same cell.

### Iteration 4 — 2026-10-07

Q9 answered: whenever the selected tab has a resizable zone and its bars show, the wheel over the
selected cell resizes the zone, anywhere on the cell — the image's zoom wheel then works on the
other cells, from another tab or with the slider. Gesture § *Where* rewritten as one rule for every
zone. No open question left.

### Iteration 5 — 2026-10-07 — ✅ Implemented

Go given: code, tests and documentation. Branch: `main`, per the standing preference that this
app's work lands on main (no branch question). Before the first write, the Blur's zone was named
by its code: the **sharp rectangle** (`BlurEffect`), the bands around it blurred — growing it keeps
more of the cell sharp. The corner drag (`workfiles/20261007-crop-corner-drag.md`) is already on
main, Blur included.

### Iteration 6 — 2026-10-07 — 🧭 Implementation choices

- ⚠️ **Rule broken — the launch path** (`CLAUDE.md` § Launch): the exe under
  `src/ImageGridFusion/bin/Debug/…` was locked by a running instance (PID 47664, no second title —
  not started by this session), so the build failed to copy it. It was not killed: the build was
  sent to the session's scratchpad (`dotnet build -o …\scratchpad\build`) and launched from there,
  with `--title` as the rule asks. Why: no compliant option without closing an instance someone
  may be testing. Autonomous run: reported instead of asked.
- **Signatures**: the helper takes the **notches** (`int`) rather than a factor, and a **nullable
  anchor** (`null` = the zone's center), so the 1.05 factor lives in one place
  (`ZoneScale.NotchFactor`) and the center is computed from the zone being scaled.
- **A notch down never grows** a zone already below the minimum gap (it can be after a window
  shrink): it stays as it is.
- **The Free-format hold** is released by `EndLive`, not by the wheel's timer alone: any gesture
  taking over ends it and computes the ratio again, so it can never stay held.
- **The crop wheel shows live** through `BeginLive` / `EndLive`, like the zoom wheel — what keeps
  `InGesture` true for the undo history.
- **Glossary**: a new term, *Resizable zone*, since RULES.md now names it (§ On-Cell Handles ›
  Resizable Zones).
- **README**: the Crop section does not repeat that a wheel burst is one undo step — § Undo
  already says it (one place per fact).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-10-07 | `ZoneScale`, `CropEffect.ScaledSeen`, `BlurEffect.Scaled`; `GridPreview.OnMouseWheel` / `ScaleZone`, the Free-format hold |
| Unit tests | — | — | Not applicable: no test project |
| README | 5 | 2026-10-07 | README.md / README.fr.md § Crop, Zoom, Blur, Format |
| RULES.md | 5 | 2026-10-07 | § On-Cell Handles › Resizable Zones, § The Crop Exception, § Output Format |
| Glossary | 5 | 2026-10-07 | GLOSSARY.md / GLOSSARY.fr.md: Crop edit view, Resizable zone, Free format |
| Validation | 6 | 2026-10-07 | Task confirmed finished by the user |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where and when does the wheel resize the kept part? | Anywhere over the cell, in the crop edit view | 2026-10-07 |
| 2 | Which point stays fixed while it grows or shrinks? | The kept part's center; the point under the cursor while Ctrl is held | 2026-10-07 |
| 3 | When the growing kept part reaches an image edge? | Slide along it, then stop (the largest kept part at the ratio within the image) | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-10-07 |
| 5 | Wheel direction: up shrinks or grows? | Up grows | 2026-10-07 |
| 6 | Step per notch: fixed factor or snapped multiples of 5 %? | Fixed factor, 5 % | 2026-10-07 |
| 7 | Free format during a burst: held or recomputed per notch? | Held until the wheel stops | 2026-10-07 |
| 8 | Size readout during a burst? | Nothing more | 2026-10-07 |
| 9 | Blur zone: wheel anywhere over the cell, or only over the zone (zoom elsewhere)? | Whenever a tab with a resizable zone is selected, the wheel acts on the resizing — anywhere over the cell | 2026-10-07 |

---

*Last updated: 2026-10-07*
