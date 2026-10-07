# Zoom Keeps the Image in Place

> Working document — zooming an image grows or shrinks it around its own center: the image never
> moves in its cell.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a zoom moves the image in its cell:

- The image's position is stored as `ImageLook.Focus` — the point of the image (in fractions of
  it) placed at the **cell's center** (`FitCalculator.Placement`). A zoom keeping the focus grows
  the image **around the cell's center**: the offset of the image's center from the cell's center
  is `(0.5 − focus) × drawn size`, so it scales with the zoom.
- The **wheel** (`GridPreview.ZoomAt`) zooms around the **point under the cursor**.
- The **slider** (`GridPreview.ZoomSelected`) keeps the focus — around the cell's center.
- Both then bring the image back **within its stops** (`FitCalculator.WithinStops`): covering the
  cell on an axis it overflows, inside it on an axis it does not fill — a second move.
- The **Animations** motion (`ImageLook.ZoomAt` × `MotionEffect.ZoomAt`, drawn by
  `Compositor.DrawCell` / `AutomaticBackground` with the same focus) pulses around the cell's
  center: a moved image drifts back and forth.

The goal: **the image's center stays where it is** — the position readout's `x` / `y` do not change
when the zoom does.

Components: `Composition/FitCalculator.cs`, `Composition/ImageLook.cs`, `Composition/Compositor.cs`,
`UI/GridPreview.cs` (`ZoomAt`, `ZoomSelected`), RULES.md, README (en / fr).

---

## Behaviour

### The Rule

A zoom keeps the **offset of the image's center from the cell's center** — the value the position
readout shows (`GridPreview.ExportOffsets`, the center of the box `FitCalculator.ComputeTurned`
draws the image in). The image grows or shrinks around its own center.

| Route | Today | After |
|---|---|---|
| Wheel over a cell (`GridPreview.ZoomAt`) | Around the point under the cursor, then back within the stops | Around the image's center; no stops |
| Zoom effect's slider (`GridPreview.ZoomSelected`) | Around the cell's center (focus kept), then back within the stops | Around the image's center; no stops |
| Animations, Zoom motion (`Compositor`, `ImageLook.ZoomAt`) | Pulses around the cell's center | Pulses around the image's center |
| Contain / Fill (`GridPreview.FitSelected`) — pressing, switching or unpressing | Focus kept, back within the stops | Around the image's center; no stops |

- **No stops after a zoom**: the offset is kept even when it leaves a band — zooming out a moved
  image may uncover a side of the cell; the Background fills it, as it does after a drag past an
  edge. The stops remain the drag's and the arrow keys' (magnetic stops), untouched.
- **The 10 % coverage stays** (`FitCalculator.MinCoveredShare`, applied at every drawing): zooming
  out an image moved far past an edge pushes it back just enough to keep covering 10 % of the cell,
  so it can still be grabbed — the **only case where a zoom moves the image**. The next zoom starts
  from where it was drawn.
- An image **never moved** (offset `0, 0`) zooms as today with the slider: centered, it stays
  centered.
- The **wheel no longer zooms toward the cursor** (decided at scoping: the image's center for every
  route). Ctrl keeps its meaning (the finer step), and crossing 100 % still stops on it.
- The **position readout** no longer shows on a zoom, by its own rule (§ Position Readout: it shows
  when the offset changes) — except where the 10 % coverage pushes the image back.

### Computing It

The stored representation stays `Focus` — the move, the stops, the magnets, flips and quarter turns
all work on it. A zoom from `z₀` to `z₁` converts the focus instead:

- One helper, `FitCalculator.FocusKeepingCenter(cell, shown, from, to, focus, degrees)`: the focus
  at `to` whose drawn box (`ComputeTurned(…).Bounds`) has the same center as the box drawn at `from`,
  so the fine angle and the 10 % coverage are honoured.
  - Without a fine angle, one `TurnedFit.FocusAt` is exact.
  - With one, the cover scale depends on where the image stands: Newton's steps on the drawn center
    (slopes measured over a focus step of 1e-4), each halved until it brings the center closer, up
    to 16 passes, 0.01 px close enough. Where no focus reaches the center (a turned image moved far
    off its cell, ~0.5 % of random extreme cases), the closest one found is kept.
  - The result may be **unclamped** — beyond what the 10 % coverage lets the drawing show — as a
    drag's focus is: zooming back in brings the image back exactly where it was.
- `GridPreview.ZoomAt` (now `ZoomAt(index, notches, fine)`), `ZoomSelected` and `FitSelected` call
  it in place of their focus computation and `WithinStops`; `FitSelected` converts from the zoom
  shown before the button to the zoom the mode gives (`ImageLook.ZoomIn`). `WithinStops`,
  `GridPreview.TurnedBack` and `GridPreview.KeptPartPoint`, left without callers, are removed.
- The **Animations** motion: `ImageLook.FocusAt(time, cell, shown)` converts the stored focus from
  the zoom at time 0 (`ImageLook.ZoomIn`) to the zoom at `time` (`ImageLook.ZoomAt`), read by
  `Compositor.DrawCell` and `Compositor.AutomaticBackground`, so the preview, the exports and the
  automatic background agree. The stored focus is untouched.

### Interactions

- **Undo history**: unchanged — a zoom still changes `Zoom` and `Focus`, one step per gesture.
- **Resizable zones**: the wheel scales the zone while its bars show, unchanged. Alt + wheel
  (`20261007-resizable-zone-alt-zoom`, implemented meanwhile) calls `ZoomAt` and keeps the image's
  center too — in the crop edit view as well, where it used to keep the image point under the cursor.
- **A fit mode following its cell** (a separator, a layout, the format, a crop resizing the cell or
  the image while Contain / Fill is pressed): not a zoom gesture — the focus is read as stored,
  unchanged.
- **Free format**, the zoom label, the **zoom badge**: unchanged.

---

## Documentation

- **RULES.md** — under § Effects › Rendering (next to the `ImageLook.ZoomIn` rule): every zoom keeps
  the image's center offset, through the one helper; a new zoom route or zoom reader does the same.
- **README.md / README.fr.md** — the wheel line ("around the point under the mouse") and "Zooming
  keeps the image where it was moved, at any zoom, but always brings it back within its stops"
  rewritten; the Animations section if it says where the motion zooms.
- **GLOSSARY.md / GLOSSARY.fr.md** — the Animations zoom around the image's center; "a zoom" left
  out of what shows the position readout.

---

## Test Impact

Not applicable — the repository has no test project (as for `workfiles/20261007-zoom-fit.md`). The
conversion stays a pure static function in `FitCalculator`, ready to be pinned the day one exists.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~Contain / Fill: keep the image's center like the other routes, or keep today's behaviour
  (focus kept, back within the stops)?~~ → Like the other routes: around the image's center, no
  stops
- [x] ~~The 10 % coverage (`FitCalculator.MinCoveredShare`, applied at every drawing): zooming out an
  image moved far past an edge can leave it covering less than 10 % of the cell — keep the guarantee
  (the image is pushed back just enough, the only case where a zoom moves it), or let it go for the
  zoom?~~ → Kept

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Scoping answers: the image's center stays fixed; routes — the wheel, the Zoom effect's options and
the Animations zoom (Contain / Fill not picked, left open); the offset is kept even when it leaves a
band; a straightforward subject (one scout pass).

Exploration: the position is a focus at the cell's center, so every zoom scales the offset; the
wheel anchors the cursor, the slider the cell's center, both re-clamp within the stops; the
Animations motion zooms with the same focus. Design: keep `Focus`, convert it on each zoom through
one `FitCalculator` helper, drop `WithinStops` from the zoom routes, and give the drawing a focus
converted for the motion's zoom.

### Iteration 2 — 2026-10-07

Open questions answered: Contain / Fill keep the image's center too — every zoom route follows one
rule; the 10 % coverage stays, the only case where a zoom moves the image. No open question left.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go for code and documentation, on the current checkout (`main`, the app's standing choice), once
the recently finished sessions are checked for an impact. Checked: `c028fa3`
(`20261007-resizable-zone-alt-zoom`, finished) makes Alt + wheel over a resizable zone call
`ZoomAt` "exactly as without bars" — it inherits the rule — and, in the crop edit view, zoom around
the image point under the cursor (`ZoomAt`'s `at` argument, `GridPreview.KeptPartPoint`): a zoom
route the rule now covers too (see the implementation choices). The other sessions (background
edge extension, search options, kebab-case folders, index folder naming) do not touch the zoom.

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

No project rule broken.

- **The crop edit view's Alt zoom keeps the image's center** (divergent from
  `20261007-resizable-zone-alt-zoom`, which kept the image point under the cursor there): the user's
  rule covers every wheel zoom. Its `at` argument and `GridPreview.KeptPartPoint` are removed, and the
  Alt line of RULES.md § Resizable Zones and the README (en / fr) rewritten.
- **Turned images**: a single conversion pass oscillated on a turned image moved off its cell (the
  cover scale depends on the position); `FocusKeepingCenter` solves it with halved Newton steps and
  keeps the closest focus where the exact center cannot be reached. Checked on 20 000 random cases
  (scratchpad program on the built dll): exact without a fine angle; with one, 92 left off by more
  than 0.5 px, all far off their cell, besides the 10 % coverage cases.
- **Unclamped focus**: the converted focus is stored as computed, the 10 % coverage applied at the
  drawing, like a drag's — zooming back in restores the image's place.
- **Dead code removed**: `FitCalculator.WithinStops` (both overloads) and `GridPreview.TurnedBack`.
- **Names**: `FitCalculator.FocusKeepingCenter`, `ImageLook.FocusAt`; RULES.md gets
  § Effects › Rendering › Zoom Keeps the Image in Place.
- **Glossary updated** (the design expected nothing there): the Animations entry and the position
  readout's triggers mentioned the zoom.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-07 | `9c3c852` zoom routes, `3c54916` Animations |
| Unit tests | 3 | 2026-10-07 | Not applicable — no test project; checked by a scratchpad program instead (Iteration 4) |
| RULES.md | 3, 4 | 2026-10-07 | `3d90002` |
| README | 3 | 2026-10-07 | `d7c6f21` (en / fr) |
| Glossary | 4 | 2026-10-07 | `a1e4306` (en / fr) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What stays fixed when zooming? | The image's center | 2026-10-07 |
| 2 | Which zoom routes are concerned? | The wheel over a cell, the Zoom effect's options, the Animations zoom | 2026-10-07 |
| 3 | If keeping the offset leaves a band when zooming out? | Keep the offset | 2026-10-07 |
| 4 | Straightforward or tricky? | Straightforward | 2026-10-07 |
| 5 | Contain / Fill: keep the image's center too? | Yes, like the other routes | 2026-10-07 |
| 6 | Keep the 10 % coverage guarantee on a zoom out? | Yes, kept | 2026-10-07 |
| 7 | Go — scope and where? | Check the recently finished sessions for an impact, then go: code and documentation, current checkout | 2026-10-07 |

---

*Last updated: 2026-10-07*
