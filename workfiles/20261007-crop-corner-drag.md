# Crop Corner Drag

> Working document — grabbing a corner of the crop's kept part (or of the blur's sharp rectangle)
> moves the two bars meeting there at once, on both axes.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

In the crop's edit view, the kept part is set today by **four bars**, each moved on its own axis,
and by a drag **inside** it that moves it whole (`workfiles/20260930-crop-effect.md`). Resizing
it on both axes takes two drags. The blur's sharp rectangle has the same four bars, with the same
code (`workfiles/20260925-blur-mask.md`).

This work adds the **four corners** as handles, for the **crop and the blur** alike: a corner
grabbed moves the two bars meeting there together, following the mouse on both axes. The bars,
the move inside the kept part and every other gesture stay as they are.

Relevant components:

| Component | Role today |
|---|---|
| `Composition/CropEffect.cs` | `WithSeenSide` moves one side (seen fractions), the ratio held around the center; `MovedSeen` moves the whole kept part |
| `Composition/BlurEffect.cs` | `BarSide`, shared by both effects' bars; `WithSide` moves one side of the sharp rectangle (cell fractions) |
| `UI/GridPreview.cs` — `ShownBars`, `BarAt`, `DragBar`, `BarCursor` | The bars shown, the bar within reach (`BarReach` = 12 logical px), the drag with its snap (`BarSnap` = 6) and minimum gap (`BarMinGap` = 8) |
| `UI/GridPreview.cs` — `OnMouseDown` / `OnMouseMove` / `UpdateHover` | Priority: bars → kept part (move) → separator → handle / pan; the cursor per zone |
| `UI/GridPreview.cs` — `PaintBars`, `PaintBarGrip` | The four bars and a grip at the middle of each side, white while hovered or dragged |

---

## Interaction

Applies to the **crop's edit view** and to the **blur's bars** alike — wherever `ShownBars` shows
the four bars as handles.

### Corner Hit Area

- A **corner** is the intersection of a vertical and a horizontal bar of the framed rectangle (the
  crop's kept part, the blur's sharp rectangle): top-left, top-right, bottom-left, bottom-right.
- Its reach is the square of **`BarReach`** (12 logical px, scaled) around the intersection on each
  axis — the same reach as the bars, so a corner is exactly the place where two bars are within
  reach at once.
- **Priority**: a corner comes **before the bars** — within its square, the corner is grabbed, not
  the nearer bar. Then the bars, the kept part, the separator, the handle and the pan, as today. A
  corner lying on the cell's edge is grabbed before the separator, like a bar there (README §
  Resizing the cells). The **×** and the source-name icon keep their priority over all of it (`onControl`).
- **Cursor**: `SizeNWSE` on the top-left and bottom-right corners, `SizeNESW` on the other two —
  while hovered and while dragged.

### Corner Drag — Free

The blur always, the crop with **Free** pressed:

- The grabbed corner **follows the mouse** on both axes; the opposite corner stays fixed. The grab
  offset is kept on each axis, as `_barGrab` does for a bar, so the corner does not jump to the
  cursor.
- Each axis follows the bar rules on its own: kept within its span (the image for the crop, the
  cell for the blur), at least `BarMinGap` from the opposite side, and **snapped onto the span's
  edge** within `BarSnap` (RULES.md § On-Cell Handles) — so a corner dragged into the image's (or
  the cell's) corner lands exactly on it.
- One drag is **one undo step** and holds the free output format, like a bar (`InGesture`,
  `CanvasRatio`) — both read the dragged state, which the corner joins.

### Corner Drag — Ratio Kept (crop only)

When a ratio is kept (1:1, 4:3, 16:9, 9:16):

- The **opposite corner stays fixed**, and the kept part keeps the ratio while it grows or shrinks
  — unlike a bar, which reshapes it around its center.
- **The larger rectangle leads** (Q&A #6): from the fixed corner, the mouse gives a width and a
  height; the kept part takes the **larger** of the two rectangles at the ratio — the one fitting
  the width, the one fitting the height — so the corner never lags behind the cursor on either
  axis, the usual behaviour of crop tools.
- The kept part **stops at the image's edges**: where the ratio would push it out, it is cut back to
  the largest rectangle at the ratio that fits from the fixed corner (the ratio wins over the
  mouse). An axis that reaches its edge within `BarSnap` lands exactly on it, the other following
  the ratio.
- It never shrinks below `BarMinGap` on either axis.
- **No keyboard modifier**: the ratio chosen in the options decides alone (Q&A #3).

---

## Rendering

- An **L-bracket** at each corner of the framed rectangle, a helper indicator: fluorescent green
  (`HelperColor`) over the black halo (`HelperHalo`), **thicker than the bars**, its two arms lying
  along the two bars. It turns **white** while hovered or dragged, like a grip.
- The **middle grips** of the four bars stay.
- Drawn in `PaintBars` with the grips (`grips: true`) — so it shows only while the bars are
  handles, for the crop and the blur, never on the indicators an undo shows briefly
  (`PaintRestored`, `grips: false`).
- Size: arms of **16 logical px** from the corner (`CornerArm`), **6 logical px** thick
  (`CornerWidth`; `BarGripWidth` is 8, the bars 2), outlined by a 1 px halo like the grips. An arm is
  **never longer than half the rectangle** on its axis, so the four brackets never overlap on a
  small kept part.
- While a corner is hovered, no bar counts as hovered: the middle grips stay green, only the
  bracket turns white.

---

## Documentation

Both languages, in the same commit (`../CLAUDE.md` § French Versions):

| File | Change |
|---|---|
| `README.md` / `README.fr.md` § Crop | The corners: grabbed, both bars moved; under a ratio, the opposite corner fixed and the ratio held |
| `README.md` / `README.fr.md` § Blur | The corners of the sharp rectangle |
| `README.md` / `README.fr.md` § Resizing the cells | A corner on the cell's edge grabbed before the separator, like a bar |
| `GLOSSARY.md` / `GLOSSARY.fr.md` — *Crop edit view* | Its corners, grabbed to move two bars at once |

---

## Test Impact

The solution holds **no test project** and every previous workfile stayed test-free. Nothing is
pinned unless the go asks for tests — then a test project would be a scope addition, not a given.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(none — test-free, see above)* | | |

---

## Open Questions

- [x] ~~**Blur too?** The blur's bars share `ShownBars` / `BarAt` / `DragBar` with the crop's.~~ →
  Yes: the blur's corners are grabbed too, free (it has no ratio).
- [x] ~~**Following the mouse under a ratio**: which axis leads?~~ → The larger rectangle: the
  corner never lags behind the cursor.
- [x] ~~**Docs**: README only, or README + GLOSSARY?~~ → README (§ Crop, § Blur, § Resizing the cells) and
  the GLOSSARY *Crop edit view* entry, both languages.

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

### Iteration 2 — 2026-10-07

Open questions answered (Q&A #5–7):

- **The blur too**: its sharp rectangle gets the same corners and L-brackets, free only. The
  interaction and rendering sections now cover both effects.
- **Under a ratio, the larger rectangle leads**: the corner never lags behind the cursor; cut back
  to the largest rectangle at the ratio fitting from the fixed corner at the image's edges.
- **Docs**: a new § Documentation lists README § Crop, § Blur, § Resizing the cells and the GLOSSARY *Crop
  edit view* entry, both languages. The § Resizing the cells line came from the read of README: a bar on
  the cell's edge is grabbed before the separator, and a corner there follows it.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given: code, unit tests and documentation (no test project — nothing to pin, see § Test
Impact). Branch Gate: stays on `main`, the repository's standing choice.

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **`BarCorner`** (`Composition/BlurEffect.cs`, next to `BarSide`): a record struct of the vertical
  and the horizontal side meeting at a corner — the hovered and dragged corner, compared by value.
- **`CropEffect.WithSeenCorner`** carries the whole corner rule (free and under a ratio); the blur
  needs no new method: `WithSide` is applied for each of the two sides.
- **`GridPreview.BarFraction`** extracted from `DragBar`: the snapped fraction and the minimum gap
  of one bar, shared by `DragBar` and `DragCorner` so a corner snaps exactly as a bar.
- **Corner reach**: the nearest corner by `dx + dy` when several are within reach (a tiny
  rectangle).
- **Hover**: a hovered corner clears the hovered bar, the kept part and the separator, so only its
  bracket turns white; the wheel is ignored while a corner is dragged, like a bar.
- **Bracket**: drawn as one 6-point polygon (filled, then outlined with the halo), its arms capped
  at half the rectangle — see § Rendering.
- **French docs**: the bracket is a *crochet en L*, the glossary's term for the Corners style's
  L-bracket.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-07 | `BarCorner`, `CropEffect.WithSeenCorner`; `GridPreview`: `CornerAt`, `DragCorner`, `BarFraction`, `PaintCorners` |
| Unit tests | 3 | 2026-10-07 | None — no test project (§ Test Impact) |
| README | 3 | 2026-10-07 | § Crop, § Blur, § Resizing the cells — EN and FR |
| GLOSSARY | 3 | 2026-10-07 | *Crop edit view* — EN and FR |
| Manual validation | | | Pending — the app launched for the user |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | With a ratio kept (1:1, 4:3, 16:9, 9:16), what does a corner drag do? | Keeps the ratio — the opposite corner fixed, the kept part growing or shrinking at the ratio | 2026-10-07 |
| 2 | How is a grabbable corner shown? | An L-bracket at each corner, thicker than the bars, white on hover | 2026-10-07 |
| 3 | A keyboard modifier during a corner drag (e.g. Shift keeping the ratio in Free)? | No — out of scope; the ratio chosen in the options decides alone | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-07 |
| 5 | Should the blur's corners be grabbed too? | Yes — the blur too, free only | 2026-10-07 |
| 6 | Under a ratio, which rule makes the corner follow the mouse? | The larger rectangle leads — the corner never lags behind the cursor | 2026-10-07 |
| 7 | Which docs get the corners: README + GLOSSARY (both languages), or README only? | README + GLOSSARY, both languages | 2026-10-07 |

---

*Last updated: 2026-10-07*
