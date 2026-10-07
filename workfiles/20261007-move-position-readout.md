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
| The image is dragged with the mouse (`_panning`) | Shown over its cell, updated at each move |
| The image is moved with the arrow keys — 1 px, Ctrl, Shift, Ctrl + Shift jump | Shown over its cell, updated at each press |
| An undo / redo restore moves an image (its focus changes, not by a turn or a flip) | Shown briefly, as the zoom badge and the guides of `ShowRestored` |

- **Timing**: the zoom badge's — full opacity for `ZoomBadgeHold` (1 s) after the last change, then
  faded out over `ZoomBadgeFade` (0.3 s).
- **Look**: the zoom badge's — bold text of `ZoomBadgeTextSize`, `HelperColor` over the `HelperHalo`
  outline, clipped to the cell. A helper indicator: preview only, never in the exports.

---

## Test Impact

The repository holds **no test project** (`CONTRIBUTING.md`): nothing testable by unit tests is
created or updated. The change is checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | Not applicable: no test project |

---

## Open Questions

- [ ] Where does the readout sit in the cell, and how does it live beside the zoom badge?
- [ ] Text format: `x −35  y +12`, `−35, +12 px`, two lines?
- [ ] Sign of y: positive **downward** (the image's pixel rows, the screen's convention) or **upward**?
- [ ] Mouse drag: strictly the zoom badge's timing (fades 1 s after the last move, even with the
      button still held), or held while the button is down, the fade starting at release?
- [ ] A zoom or a fine angle that moves the image's center (the wheel keeping the point under the
      mouse, the image brought back within its stops): does it show the readout too?
- [ ] The arrow keys move 1 **preview** px (10 with Ctrl): the readout, in export px, jumps by
      several units per press. Kept as is, or out of this workfile's scope?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In which space are the x / y pixels expressed? | Export pixels | 2026-10-07 |
| 2 | What do x and y stand for? | The offset from the center (0, 0 = centered, signed) | 2026-10-07 |
| 3 | Which moves show the coordinates? | Mouse drag, arrow keys, undo / redo restore | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-07 |
| 5 | Where does the readout sit (A below the ×, B top-left, C bottom-center, D at the image's center)? | | 2026-10-07 |
| 6 | Text format? | | 2026-10-07 |
| 7 | Sign of y: positive downward or upward? | | 2026-10-07 |
| 8 | Mouse drag: zoom badge's timing strictly, or held while the button is down? | | 2026-10-07 |
| 9 | Does a zoom or a fine angle moving the image's center show the readout too? | | 2026-10-07 |
| 10 | Arrow keys step 1 preview px, the readout jumping several export px: kept as is? | | 2026-10-07 |

---

*Last updated: 2026-10-07*
