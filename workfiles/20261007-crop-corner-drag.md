# Crop Corner Drag

> Working document — grabbing a corner of the crop's kept part moves the two bars meeting there
> at once, on both axes.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

In the crop's edit view, the kept part is set today by **four bars**, each moved on its own axis,
and by a drag **inside** it that moves it whole (`workfiles/20260930-crop-effect.md`). Resizing
it on both axes takes two drags.

This work adds the **four corners** as handles: a corner grabbed moves the two bars meeting there
together, following the mouse on both axes. The bars, the move inside the kept part and every
other gesture stay as they are.

Relevant components:

| Component | Role today |
|---|---|
| `Composition/CropEffect.cs` | `WithSeenSide` moves one side (seen fractions), the ratio held around the center; `MovedSeen` moves the whole kept part |
| `UI/GridPreview.cs` — `ShownBars`, `BarAt`, `DragBar`, `BarCursor` | The bars shown, the bar within reach (`BarReach` = 12 logical px), the drag with its snap (`BarSnap` = 6) and minimum gap (`BarMinGap` = 8) |
| `UI/GridPreview.cs` — `OnMouseDown` / `OnMouseMove` / `UpdateHover` | Priority: bars → kept part (move) → separator → handle / pan; the cursor per zone |
| `UI/GridPreview.cs` — `PaintBars`, `PaintBarGrip` | The four bars and a grip at the middle of each side, white while hovered or dragged |
| `Composition/BlurEffect.cs` | `BarSide`, shared by the blur's and the crop's bars |

---

## Interaction

### Corner Hit Area

- A **corner** is the intersection of a vertical and a horizontal bar of the kept part: top-left,
  top-right, bottom-left, bottom-right.
- Its reach is the square of **`BarReach`** (12 logical px, scaled) around the intersection on each
  axis — the same reach as the bars, so a corner is exactly the place where two bars are within
  reach at once.
- **Priority**: a corner comes **before the bars** — within its square, the corner is grabbed, not
  the nearer bar. Then the bars, the kept part, the separator, the handle and the pan, as today.
  The **×** and the source-name icon keep their priority over all of it (`onControl`).
- **Cursor**: `SizeNWSE` on the top-left and bottom-right corners, `SizeNESW` on the other two —
  while hovered and while dragged.

### Corner Drag — Free

- The grabbed corner **follows the mouse** on both axes; the opposite corner stays fixed. The grab
  offset is kept on each axis, as `_barGrab` does for a bar, so the corner does not jump to the
  cursor.
- Each axis follows the bar rules on its own: kept within the image, at least `BarMinGap` from the
  opposite side, and **snapped onto the image's edge** within `BarSnap` (RULES.md § On-Cell
  Handles) — so a corner dragged into the image's corner lands exactly on it.
- One drag is **one undo step** and holds the free output format, like a bar (`InGesture`,
  `CanvasRatio`) — both read the dragged state, which the corner joins.

### Corner Drag — Ratio Kept

When a ratio is kept (1:1, 4:3, 16:9, 9:16):

- The **opposite corner stays fixed**, and the kept part keeps the ratio while it grows or shrinks
  — unlike a bar, which reshapes it around its center.
- The mouse is followed as closely as the ratio allows — the rule is Open Question 2.
- The kept part **stops at the image's edges**: on the side where the ratio would push it out, it
  stops growing (the ratio wins over the mouse). Snapping applies to the axis that reaches an edge,
  the other following the ratio.
- **No keyboard modifier**: the ratio chosen in the options decides alone (Q&A #3).

---

## Rendering

- An **L-bracket** at each corner of the kept part, a helper indicator: fluorescent green
  (`HelperColor`) over the black halo (`HelperHalo`), **thicker than the bars**, its two arms lying
  along the two bars. It turns **white** while hovered or dragged, like a grip.
- The **middle grips** of the four bars stay.
- Drawn in `PaintBars` with the grips (`grips: true`) — so it shows only while the bars are
  handles, never on the indicators an undo shows briefly (`PaintRestored`, `grips: false`).
- Proposed size: arms of **16 logical px** from the corner, **6 logical px** thick
  (`BarGripWidth` is 8, the bars 2). Adjusted at implementation if it hides too much of the image.

---

## Test Impact

The solution holds **no test project** and every previous workfile stayed test-free. Nothing is
pinned unless the go asks for tests — then a test project would be a scope addition, not a given.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(none — test-free, see above)* | | |

---

## Open Questions

- [ ] **Blur too?** The blur's bars share `ShownBars` / `BarAt` / `DragBar` with the crop's. Should
  its four corners be grabbed the same way, or does this stay a crop-only feature?
  *Proposed: the blur too, free only (it has no ratio) — the same handles behaving alike.*
- [ ] **Following the mouse under a ratio**: the corner can't follow both axes. Which one leads?
  *Proposed: the axis that gives the **larger** kept part — the mouse projected so the corner never
  lags behind the cursor on either axis — the usual behaviour of crop tools.* Alternative: the
  projection onto the diagonal from the fixed corner (smoother near the diagonal, lags off it).
- [ ] **Docs**: README § Crop (and README.fr.md) get a line for the corners; the GLOSSARY *Crop
  edit view* entry (and GLOSSARY.fr.md) mentions them. Agreed, or README only?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design, from the scoping answers (Q&A #1–4) and a single read of the crop's bar code:

- Four corners added as handles in the crop's edit view, grabbed before the bars, each moving the
  two bars meeting there; diagonal cursors.
- Free: both axes follow the mouse, each snapped and clamped like a bar.
- Ratio kept: opposite corner fixed, the ratio held, stopped at the image's edges.
- An L-bracket drawn at each corner, thicker than the bars, white while hovered or dragged; the
  middle grips kept.
- Three open questions: the blur's corners, the mouse-following rule under a ratio, the docs touched.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| GLOSSARY | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | With a ratio kept (1:1, 4:3, 16:9, 9:16), what does a corner drag do? | Keeps the ratio — the opposite corner fixed, the kept part growing or shrinking at the ratio | 2026-10-07 |
| 2 | How is a grabbable corner shown? | An L-bracket at each corner, thicker than the bars, white on hover | 2026-10-07 |
| 3 | A keyboard modifier during a corner drag (e.g. Shift keeping the ratio in Free)? | No — out of scope; the ratio chosen in the options decides alone | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-07 |
| 5 | Should the blur's corners be grabbed too? | | |
| 6 | Under a ratio, which rule makes the corner follow the mouse? | | |
| 7 | Which docs get the corners: README + GLOSSARY (both languages), or README only? | | |

---

*Last updated: 2026-10-07*
