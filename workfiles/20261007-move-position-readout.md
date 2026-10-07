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
| `UI/GridPreview.cs` — `PanBy` | The one route of every move: the mouse drag (`OnMouseMove`, `_panning`) and the arrow keys (`KeyPan` ← `PanSelected`, `JumpSelected`); its `free` argument (Shift) ignores the stops |
| `UI/GridPreview.cs` — `FitSelected`, `ZoomOf` | The Zoom effect's fit modes (Contain / Fill): the image put in a mode, pulled back within its stops |
| `UI/GridPreview.cs` — `OnMouseWheel`, `ScaleZone`, `EditsCrop` | The wheel over the selected cell: it scales the crop's or the blur's zone while their bars show, zooms the image otherwise; the crop edit view |
| `UI/GridPreview.cs` — `InGesture` | A gesture running: something pressed or dragged (`_pressed`, `_draggedBar`, `_draggedCorner`, `_draggedSeparator`, `_movedCrop`), a wheel or slider zoom not at rest yet (`_live`, `_wheelEnd`, 150 ms after the last notch) |
| `UI/GridPreview.cs` — `ShowRestored`, `PaintRestored` | What an undo / redo restore shows briefly; a focus change outside a turn / flip already shows the guides the image rests on |
| `Composition/ImageLook.cs` — `Focus`, `ZoomIn` | The move, stored as the point of the oriented image kept at the cell's center; the zoom, read through `ZoomIn(cell, shown)` — the fit mode resolved — never `Zoom` (RULES.md § Effects › Rendering) |
| `Composition/FitCalculator.cs` — `ComputeTurned` | Where the image is drawn in a cell: its box (`Bounds`), the turned image's when it has a fine angle |
| `Composition/CanvasSizer.cs` — `Compute`; `Composition/Compositor.cs` — `Cells` | The export canvas and its cells (shrunk by the Borders' gap) |
| `UI/GridPreview.cs` — `ExportOffsets`, `UpdateReadouts`, `OnReadoutTick`, `ScheduleReadouts`, `PaintReadouts`, `PaintReadout`, `ReadoutPath` | **The readout** (implemented): the offsets in export px; their comparison at each paint with the ones last painted, showing the readouts that changed; their timer; their paint |

---

## Behaviour

### What It Shows

- **x and y**: the offset of the image's **center from the cell's center** — `0, 0` for an image
  centered, its default place; signed values otherwise.
- The image's center is the center of the box it is drawn in (`FitCalculator.ComputeTurned(...).Bounds`),
  the turned image's box when it has a fine angle — the box the magnetic stops already use. Its zoom
  is read through `ImageLook.ZoomIn(cell, shown)`, never `ImageLook.Zoom`.
- In **export pixels**: the offset the image has in the PNG export of the grid as it stands — the
  canvas `CanvasSizer.Compute` gives for the current images (`ImageLook.Shown` sizes), layout and
  output format, its cells from `Compositor.Cells` with the Borders' gap. It does not change when the
  window is resized. Rounded to the nearest integer.
- An MP4's canvas may be one pixel smaller on an odd side (`Animation.EvenSize`): the readout gives
  the PNG's geometry, the difference staying under a pixel.

### When It Shows

**The rule**: an image **staying in its cell** whose offset — the readout's value, in export pixels —
changes shows its readout, whatever changed it (Q&A 9, 13). Each such image shows its own, so
several may show at once.

| Change | Readouts shown |
|---|---|
| The image dragged with the mouse (`_panning`) | Its own, updated at each move |
| The image moved with the arrow keys — 1 px, Ctrl, Shift, Ctrl + Shift jump | Its own, updated at each press |
| The image's look: zoom (wheel, slider, the Contain / Fill buttons) — the wheel keeping the point under the mouse, the image brought back within its stops —, fine angle, quarter turn, flip, crop (its bars, its corners, the wheel scaling its kept part) | Its own, when its offset changes |
| The grid's geometry: a separator dragged, the layout, the output format (the Free format computed again after a drag or a crop wheel burst), the Borders' gap — an image in a fit mode getting a new zoom with its cell | Every image whose offset changes |
| The export canvas resized by another image arriving, leaving or cropped (`CanvasSizer.Compute`) | Every other image whose offset changes — its offset in export px follows the canvas |
| An undo / redo restore | Every image whose offset changes, briefly, as the zoom badge and the guides of `ShowRestored` |

- **Not shown**:
  - an image **arriving** in a cell, replaced, shifting into another cell after a deletion, or
    **swapped** — it changed cell, it did not move in it;
  - an image centered staying centered — its offset stays `0, 0`;
  - the window resized — the export pixels do not change;
  - the **Animations** effect's motion playing — its zoom moves the center continuously, and would
    keep the readout on; the readout reads the motion's **starting state** (`ImageLook.ZoomIn`, the
    zoom at time 0), as a PNG shows it;
  - a cell showing its **crop edit view** (`EditsCrop`): no readout on it while the view shows, and
    what changes there shows nothing — the offset it leaves is taken as the new starting value, so
    leaving the view shows nothing either. The other cells keep theirs (e.g. the Free format computed
    again after a crop wheel burst).
- The arrow keys keep their step of 1 **preview** px (10 with Ctrl): the readout, in export px, may
  jump by several units per press. Changing the step is out of this workfile's scope.

- **Detection** (implemented): at each paint, every image's offset is compared with the one last
  painted (`UpdateReadouts`), so every route — a move, a look, the grid's geometry, the export
  canvas, a restore — is covered by one check, none calling it; `ShowRestored` is left untouched.
- **Timing**: the zoom badge's — full opacity for `ZoomBadgeHold` (1 s) after the last change, then
  faded out over `ZoomBadgeFade` (0.3 s), each image's readout on its own clock. During a
  **gesture** (`GridPreview.InGesture` — something pressed or dragged: the image, a separator, a bar
  or a corner; a wheel or slider zoom not at rest yet), it stays at full opacity, even with the mouse
  still: the hold and the fade start when the gesture ends. Every readout showing is held, an earlier
  one still fading included.
- Hidden during a swap (the ✥ drag), like the restore indicators.
- **Look**: the zoom badge's — bold text of `ZoomBadgeTextSize`, `HelperColor` over the `HelperHalo`
  outline. A helper indicator: preview only, never in the exports.

### Where and How It Reads

- Two parts, all `HelperColor` over the `HelperHalo`, faded together:
  - **at the image's center**, following the image: an **X cross** — two diagonal strokes of about
    30 px each, crossing on the center, to aim at it — and a **dashed line** from the cell's center
    to the image's center at **50 % opacity** — none while they coincide (`0, 0`); both clipped to
    the cell;
  - the **text**, in the cell's **top-right corner, right-aligned below the ×** — the zoom badge's
    place — or **just below the zoom badge** when it shows on the same cell.
- **While the readout shows, the cell's ✥ swap handle is hidden** — neither drawn nor grabbed: a
  press there moves the image like the rest of the cell. It comes back once the readout has faded.
- Sizes: the cross's strokes 30 px, 2 px wide; the line 2 px, dashed 4 / 3 like the pan guides,
  antialiased; the text the zoom badge's size.
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
- [x] ~~While a cell shows its **crop edit view** (the whole image fitted whole, the export's center
      not on screen; a drag there moves the crop, the wheel or a corner scales the kept part), what
      does that cell's readout do?~~ → Nothing: no readout on that cell while its edit view shows; the
      other cells keep theirs

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

### Iteration 5 — 2026-10-07

Asked for the go, the user asked instead (Q&A 14) for one agent to bring the design up to date with
the work other sessions finished on `main` meanwhile, and one to push `main` (done: `2d894d1..527aaf5`).
Refreshed against `main` at `527aaf5`, and against two rule updates sent by those sessions:

- **Zoom fit modes** (RULES.md § Effects › Rendering): the zoom is read through
  `ImageLook.ZoomIn(cell, shown)`, never `ImageLook.Zoom`; the Contain / Fill buttons
  (`FitSelected`) and an image in a fit mode getting a new zoom with its cell join the changes that
  show the readout.
- **Resizable zones** (RULES.md § On-Cell Handles › Resizable Zones): the wheel over a cell showing
  crop or blur bars scales that zone instead of zooming; a crop scaled that way, or by its new
  corners, changes the kept part, so the image's offset — a crop change, already covered.
- **Gestures**: the hold follows `InGesture` as it now stands — the corners and a wheel or slider
  zoom not at rest included — rather than "while the button is down".
- The content-search work is not on `main`: nothing of it concerns this workfile.

One question follows: the crop edit view (Q&A 15). A stray table header in § When It Shows removed.

### Iteration 6 — 2026-10-07

Answer to Q&A 15: **no readout on a cell while it shows its crop edit view**; what changes there
shows nothing, leaving the view included; the other cells keep theirs. No open question left.

### Iteration 7 — 2026-10-07 — ✅ Implemented

Go given (Q&A 16): code, tests and documentation. Implemented on `main` — this app's work lands on
`main` (the user's standing choice), no branch question asked.

### Iteration 8 — 2026-10-07 — 🧭 Implementation choices

- ⚠️ **Rule broken — Branch Gate** (`create-workfile` skill): on `main`, the branch question was not
  asked; the work was done on `main`, the user's standing choice for this app (their memory "work on
  main only").
- **Detection at paint time**: rather than a call in each route, `UpdateReadouts` compares, at each
  paint, every image's export offset with the one last painted. Every route is covered by one check,
  a future one included, and a restore needs nothing in `ShowRestored`. RULES.md § Undo History says
  so (the readout as the exception to "added to `ShowRestored`"), and a new § Position Readout
  (under § On-Cell Helper Indicators) states the rule.
- **The hold during a gesture** keeps every readout showing at full opacity, an earlier one still
  fading included; the timer polls every 30 ms while `InGesture` holds, so the hold starts at most
  30 ms after the gesture ends.
- **Hidden during a swap** (`_dragging`), like the restore indicators.
- **The dashed line** is drawn whenever the offset in export px is not `0, 0`, antialiased, with the
  pan guides' pens (2 px, dashed 4 / 3, the 4 px halo).
- **Sizes**: the dot 3 px in radius; the text the zoom badge's size, 6 px below the dot, kept 4 px
  inside the cell's edges.
- **Numbers**: invariant culture, so a negative value always reads `-35` (never a culture's own
  minus sign); `0` reads `0`, unsigned.
- **No layout holding the images** (a transition): no offsets, no readout; the next paint takes them
  as new.

### Iteration 9 — 2026-10-07 — ⚙️ Post-implementation — Readout in the top-right, cross, hidden handle

Requested after testing the delivery:

- The **text** moves to the cell's **top-right**, where the zoom badge is, and **below the zoom badge**
  when it shows too.
- The **dashed line** to the center stays, at **50 % opacity**.
- The **dot** gives way to an **X cross** of about 30 px a stroke, to aim at the center more easily.
- During a move, the cell's **✥ swap handle is hidden**. Read as: hidden — neither drawn nor
  grabbed — while the cell's readout shows, so a press at the center keeps moving the image.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7, 8 | 2026-10-07 | `GridPreview`: offsets, detection at paint, timer, paint |
| Unit tests | 7 | 2026-10-07 | Not applicable: no test project |
| README (+ `README.fr.md`) | 7 | 2026-10-07 | The readout under the zoom badge's line; the restore's indicators |
| GLOSSARY (+ `GLOSSARY.fr.md`) — the position readout | 7 | 2026-10-07 | New entry *Position readout* |
| RULES — § On-Cell Helper Indicators, § Undo History (`ShowRestored`) | 8 | 2026-10-07 | New § Position Readout; § Undo History names the readout and its exception |

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
| 14 | The design is complete: start implementing? | Neither choice: one agent to refresh the design against the code other sessions finished, and say whether it can start or needs more info; one agent to push | 2026-10-07 |
| 15 | What does a cell's readout do while it shows its crop edit view? | Nothing on that cell; the other cells keep theirs | 2026-10-07 |
| 16 | The design is complete and up to date with main: start implementing? | Code, tests and documentation | 2026-10-07 |

---

*Last updated: 2026-10-07*
