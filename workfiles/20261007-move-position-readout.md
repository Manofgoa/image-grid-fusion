# Move Position Readout

> Working document — showing the image's x / y offset, in export pixels, in fluorescent green while it
> is moved in its cell, fading out after a delay exactly like the zoom badge.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

While the zoom changes, its percentage shows over the cell — the **zoom badge**, a helper indicator
held 1 s after the last change, then faded out over 0.3 s. A move of the image within its cell
(mouse drag, arrow keys) shows its magnetic guides only: nothing says **how far** the image is from
the center. The **position readout** gives that distance, in the pixels of the export, with the
zoom badge's look and timing.

Relevant components:

| Component | Role |
|---|---|
| `UI/GridPreview.cs` — `ShowZoomBadge`, `OnZoomBadgeTick`, `InvalidateZoomBadge`, `ZoomBadgePath`, `PaintZoomBadge` | The zoom badge: its timer (`ZoomBadgeHold` 1000 ms, `ZoomBadgeFade` 300 ms, `ZoomBadgeTick` 30 ms), its text path right-aligned below the ×, painted green over the black halo, clipped to the cell |
| `UI/GridPreview.cs` — `PanBy` | The one route of every move: the mouse drag (`OnMouseMove`, `_panning`) and the arrow keys (`KeyPan` ← `PanSelected`, `JumpSelected`) |
| `UI/GridPreview.cs` — `ShowRestored`, `PaintRestored` | What an undo / redo restore shows briefly; a focus change outside a turn / flip already shows the guides the image rests on |
| `Composition/ImageLook.cs` — `Focus` | The move, stored as the point of the oriented image kept at the cell's center |
| `Composition/FitCalculator.cs` — `ComputeTurned` | Where the image is drawn in a cell: its box (`Bounds`), the turned image's when it has a fine angle |
| `Composition/CanvasSizer.cs` — `Compute`; `Composition/Compositor.cs` — `Cells` | The export canvas and its cells (shrunk by the Borders' gap) |

---

## Behaviour

### What It Shows

- **x and y**: the offset of the image's **center from the cell's center** — `0, 0` for an image
  centered, its default place; signed values otherwise.
- The image's center is the center of the box it is drawn in (`FitCalculator.ComputeTurned(...).Bounds`),
  the turned image's box when it has a fine angle — the box the magnetic stops already use.
- In **export pixels**: the offset the image has in the PNG export of the grid as it stands — the
  canvas `CanvasSizer.Compute` gives for the current images (`ImageLook.Shown` sizes), layout and
  output format, its cells from `Compositor.Cells` with the Borders' gap. It does not change when the
  window is resized. Rounded to the nearest integer.
- An MP4's canvas may be one pixel smaller on an odd side (`Animation.EvenSize`): the readout gives
  the PNG's geometry, the difference staying under a pixel.

### When It Shows

| Event | Readout |
|---|---|
**The rule**: an image **staying in its cell** whose offset — the readout's value, in export pixels —
changes shows its readout, whatever changed it (Q&A 9, 13). Each such image shows its own, so
several may show at once.

| Change | Readouts shown |
|---|---|
| The image dragged with the mouse (`_panning`) | Its own, updated at each move |
| The image moved with the arrow keys — 1 px, Ctrl, Shift, Ctrl + Shift jump | Its own, updated at each press |
| The image's look: zoom (wheel, slider) — the wheel keeping the point under the mouse, the image brought back within its stops —, fine angle, quarter turn, flip, crop | Its own, when its offset changes |
| The grid's geometry: a separator dragged, the layout, the output format, the Borders' gap | Every image whose offset changes |
| The export canvas resized by another image arriving, leaving or cropped (`CanvasSizer.Compute`) | Every other image whose offset changes — its offset in export px follows the canvas |
| An undo / redo restore | Every image whose offset changes, briefly, as the zoom badge and the guides of `ShowRestored` |

- **Not shown**:
  - an image **arriving** in a cell, replaced, shifting into another cell after a deletion, or
    **swapped** — it changed cell, it did not move in it;
  - an image centered staying centered — its offset stays `0, 0`;
  - the window resized — the export pixels do not change;
  - the **Animations** effect's motion playing — its zoom moves the center continuously, and would
    keep the readout on; the readout reads the motion's **starting state** (`ImageLook.Zoom`), as a
    PNG shows it.
- The arrow keys keep their step of 1 **preview** px (10 with Ctrl): the readout, in export px, may
  jump by several units per press. Changing the step is out of this workfile's scope.

- **Timing**: the zoom badge's — full opacity for `ZoomBadgeHold` (1 s) after the last change, then
  faded out over `ZoomBadgeFade` (0.3 s), each image's readout on its own clock. During a **mouse
  gesture** (`GridPreview.InGesture` — an image, a separator or a crop bar dragged), it stays at full
  opacity while the button is down, even with the mouse still: the hold and the fade start at the
  release.
- **Look**: the zoom badge's — bold text of `ZoomBadgeTextSize`, `HelperColor` over the `HelperHalo`
  outline. A helper indicator: preview only, never in the exports.

### Where and How It Reads

- **At the image's center**, following the image as it moves — wherever the zoom badge is
  (top-right, below the ×): the two are independent and may show together.
- Three parts, all `HelperColor` over the `HelperHalo`, faded together:
  - a **dot** on the image's center;
  - a **dashed line** from the cell's center to the image's center — none while they coincide
    (`0, 0`);
  - the **text** just below the dot, centered on it.
- **Kept inside the cell**: near the cell's edge, or past it for an image pushed beyond its stops,
  the text is pushed back so it shows whole in the cell; the dot and the line are clipped to the cell.
- **Two lines**: `x -35px`, then `y +12px` below it.
  - A signed integer: `+` for a positive value, `-` (hyphen-minus) for a negative one, no sign for
    `0` (`x 0px`).
  - **x positive to the right, y positive downward** — the image's pixel rows, the screen's
    convention.

---

## Test Impact

The repository holds **no test project** (`CONTRIBUTING.md`): nothing testable by unit tests is
created or updated. The change is checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | Not applicable: no test project |

---

## Open Questions

- [x] ~~Where does the readout sit in the cell, and how does it live beside the zoom badge?~~ → At
      the image's center, following the image; independent of the zoom badge (top-right)
- [x] ~~Text format: `x −35  y +12`, `−35, +12 px`, two lines?~~ → Two lines, `x -35px` then `y +12px`
- [x] ~~Sign of y: positive **downward** (the image's pixel rows, the screen's convention) or
      **upward**?~~ → Downward
- [x] ~~Mouse drag: strictly the zoom badge's timing (fades 1 s after the last move, even with the
      button still held), or held while the button is down, the fade starting at release?~~ → Held
      while the button is down; the hold and the fade start at the release
- [x] ~~The mockup's option D also drew a **dot** on the image's center and a **dashed line** from the
      cell's center to it: are they part of the readout, or the text only?~~ → Part of it: text, dot
      and dashed line
- [x] ~~Near the cell's edge — or past it, an image pushed beyond its stops — the text centered on the
      image's center would be cut by the cell: kept inside the cell, or cut?~~ → Kept inside the cell
- [x] ~~A zoom or a fine angle that moves the image's center (the wheel keeping the point under the
      mouse, the image brought back within its stops): does it show the readout too?~~ → Yes:
      whenever the image's center moves
- [x] ~~The arrow keys move 1 **preview** px (10 with Ctrl): the readout, in export px, jumps by
      several units per press. Kept as is, or out of this workfile's scope?~~ → Kept as is, out of
      scope
- [x] ~~"Whenever the center moves": a change of the **image's own look** only (move, zoom, fine angle,
      quarter turn, flip, crop), or also a change of the **grid's geometry** that shifts it in export
      pixels (separator drag, layout, format, Borders' gap)? The Animations motion, moving the center
      continuously, would keep the readout on forever: left out either way?~~ → The grid's geometry
      too, on every image whose offset changes; the Animations motion left out

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design, from the scoping batch (Q&A 1–4): the offset of the image's center from the cell's
center, in export pixels, shown on a mouse drag, an arrow-key move and an undo / redo restore that
moves the image, with the zoom badge's look and timing. Exploration done directly (one area,
`GridPreview`, questions chaining): every move goes through `PanBy`; the zoom badge and the restore
indicators give the timer, the paint and the restore hook to mirror.

### Iteration 2 — 2026-10-07

Answers to Q&A 5–8, from a mockup of four placements (below the ×, top-left, bottom-center, at the
image's center): the readout sits **at the image's center** and follows it, independent of the zoom
badge; **two lines**, `x -35px` / `y +12px`; **y positive downward**; during a mouse drag it is
**held while the button is down**, its hold and fade starting at the release. Two questions follow
from the placement chosen: the mockup's dot and dashed line, and the text near the cell's edge.

### Iteration 3 — 2026-10-07

Answers to Q&A 9–12: the readout shows **whenever the image's center moves** — a zoom or a fine
angle too, not only a move; the arrow keys' step stays as it is (out of scope); the readout is the
mockup's D in full — **text, dot and dashed line** from the cell's center; the text is **kept inside
the cell**. One question follows from "whenever": its boundary (Q&A 13).

### Iteration 4 — 2026-10-07

Answer to Q&A 13: the grid's geometry counts too. The rule becomes **an image staying in its cell
whose offset in export pixels changes shows its readout**, whatever changed it, one readout per
image. Consequences written into § When It Shows: a separator, the layout, the format, the Borders'
gap, and the export canvas resized by another image all show the readouts of the images they shift;
an image changing cell (arrival, replacement, shift, swap) does not; the Animations motion is left
out, the readout reading its starting state. The hold during a drag extends to every mouse gesture
(`InGesture`), a separator or a crop bar dragged included. Documentation steps added to the
Implementation Log (README, GLOSSARY, RULES, with their French versions). No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project |
| README (+ `README.fr.md`) | | | |
| GLOSSARY (+ `GLOSSARY.fr.md`) — the position readout | | | |
| RULES — § On-Cell Helper Indicators, § Undo History (`ShowRestored`) | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In which space are the x / y pixels expressed? | Export pixels | 2026-10-07 |
| 2 | What do x and y stand for? | The offset from the center (0, 0 = centered, signed) | 2026-10-07 |
| 3 | Which moves show the coordinates? | Mouse drag, arrow keys, undo / redo restore | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-07 |
| 5 | Where does the readout sit (A below the ×, B top-left, C bottom-center, D at the image's center)? | D — at the image's center, following the image | 2026-10-07 |
| 6 | Text format? | Two lines: `x -35px` then `y +12px` | 2026-10-07 |
| 7 | Sign of y: positive downward or upward? | Downward | 2026-10-07 |
| 8 | Mouse drag: zoom badge's timing strictly, or held while the button is down? | Held while the button is down; 1 s after the release, then the fade | 2026-10-07 |
| 9 | Does a zoom or a fine angle moving the image's center show the readout too? | Yes — whenever the image's center moves | 2026-10-07 |
| 10 | Arrow keys step 1 preview px, the readout jumping several export px: kept as is? | Kept as is — out of scope | 2026-10-07 |
| 11 | Are the mockup's dot and dashed line part of the readout? | Yes — text, dot and dashed line | 2026-10-07 |
| 12 | Near or past the cell's edge: text kept inside the cell, or cut? | Kept inside the cell | 2026-10-07 |
| 13 | "Whenever the center moves": the image's own look only, or the grid's geometry (separators, layout, format, Borders' gap) too? | The grid's geometry too — on every image whose offset changes; the Animations motion left out | 2026-10-07 |

---

*Last updated: 2026-10-07*
