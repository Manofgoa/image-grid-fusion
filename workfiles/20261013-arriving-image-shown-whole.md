# Arriving Image Shown Whole

> Working document — an image arriving in a cell is zoomed so it shows whole (Contain's zoom, the
> mode itself left unpressed), and pressing Contain or Fill recenters the image.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two behaviours, implemented together:

1. **An arriving image shows whole.** Today an image arriving in a cell follows the fitting rule
   (README § Fitting rules): it fills the cell, up to 15 % cropped, bands beyond. From now on, it
   arrives at the zoom **Contain** would give — the whole image visible in its cell, bands on one
   side — as a **free zoom**: the Zoom effect is on, Contain is **not** pressed.
2. **Contain / Fill recenter the image** — the design of
   [20261012-contain-fill-recenters-image.md](20261012-contain-fill-recenters-image.md), taken over
   here unchanged (§ Contain / Fill Recenter below); that workfile is marked as taken over.

Components: `GridPreview.Add` / `RemoveAt` (the arrival and shift routes, every route into a cell
going through `Add`), `GridPreview.OnImagesChanged` (the layout and the Free ratio settle there),
`FitCalculator.FitZoom` (Contain's zoom, from the shapes of the cell and the image only),
`ImageLook.WithZoom` (the free zoom, the effect turned on), `GridPreview.FitSelected` (the fit
buttons), RULES.md, README (en / fr), GLOSSARY (en / fr).

---

## Arrival Zoom

| Event | The image's zoom |
|---|---|
| An image **arrives** — drop, Ctrl+V, Browse, files at launch, explorer tile, URL zone, an image replaced | **Free zoom = Contain's zoom** for its cell, centered; the Zoom effect **on**, Contain unpressed |
| An image **shifts** into another cell after a deletion (its effects reset, RULES.md § Scope and State) | The same, for its new cell |
| A swap, an undo / redo restore | Unchanged — the image keeps its look |
| A later change of the cell's shape — separator, layout strip, format, crop, turn | Unchanged — a free zoom, it does not follow the cell (Contain pressed does) |
| The Zoom effect's *Reset*, the effects toolbar's *Reset*, the effect turned off | As today: 100 %, the fitting rule — **not** the arrival zoom |

- **When it is computed**: after the grid settles for the arrival — the new default layout and the
  Free format's ratio (`OnImagesChanged`) — on the cell the image is drawn in. The Free ratio does not
  read the zoom (`OutputFormats.FreeRatio`), so nothing loops.
- **Value**: `FitCalculator.FitZoom(cell, look.Shown(bitmap), ZoomFit.Contain)` — relative to the
  fitting rule's 100 %, so below 100 % whenever the rule would crop; resolution-independent (shapes
  only), clamped by `ImageLook.WithZoom` to [MinZoom, MaxZoom].
- **Texts** (`SourceImage.LaysOutZoom`) are left out: laid out to fill their cell, they keep 100 %,
  as Contain and Fill keep them.
- **Undo**: the arrival is one step, as today (`GridHistory` commits once settled); the arrival zoom
  is part of it.
- **Position readout**: not shown — an image new in its cell (RULES.md § Position Readout).

### Code

- `GridPreview.Add`: remembers the indices of the cells that received an image; after
  `OnImagesChanged`, sets their arrival zoom.
- `GridPreview.RemoveAt`: after `OnImagesChanged`, sets the arrival zoom of the shifted images.
- One helper, `this.ShowWhole(index)` (name to settle), computing the zoom on `this.CellBounds()[index]`.

---

## Contain / Fill Recenter

Taken over from [20261012-contain-fill-recenters-image.md](20261012-contain-fill-recenters-image.md):

| Action | Image position |
|---|---|
| **Contain** or **Fill** pressed — from the free zoom, or from the other mode | **Recentered**: `Focus = (0.5, 0.5)`, the image's center on the cell's center (offset 0, 0) |
| The pressed mode unpressed (back to the free zoom it gave) | **Unchanged** — nothing moves, as today |
| The Zoom slider, the Animations' Zoom motion | Unchanged rule — they keep the image's center (`FitCalculator.ImageCenter`) |
| The wheel (Alt + wheel over a zone included) | Unchanged rule — keeps the image point under the cursor |

- **One-off recentering**: at the press only. While the mode stays pressed, a drag or the arrow keys
  still move the image freely, the mode staying pressed. A later change of the cell's shape keeps the
  fit's zoom following it, the position as it stands.
- **Position readout**: shown by its own rule, briefly `x 0px` / `y 0px` — nothing to code.
- **Text content**: Contain and Fill keep it at 100 % and it does not drag; recentering changes nothing visible.
- With a **fine angle**: the fit zoom is unchanged; the image's center is put on the cell's center.
- **Code**: `GridPreview.FitSelected` — for `Contain` / `Fill`, `fitted.WithFocus(<center>)` instead of
  `FitCalculator.FocusKeeping(…)`; `ZoomFit.None` keeps its computation. Its summary says so.

---

## Rules and Docs

- **RULES.md § Zoom Keeps the Image in Place**: Contain / Fill leave the routes *without a cursor*
  keeping the image's center and become the exception — a fit mode pressed recenters the image.
- **RULES.md § Effects › Rendering** (or a new subsection): an arriving or shifted image gets
  Contain's zoom as a free zoom, its effects otherwise at their default state — the one place the
  arrival state of the Zoom differs from its default state, which stays 100 %.
- **README.md / README.fr.md**: § Fitting rules (an arriving image is shown whole, zoomed out by the
  Zoom effect), line 39 and § Zoom's Contain / Fill line (they recenter the image).
- **GLOSSARY.md / GLOSSARY.fr.md**: *Fit mode* — pressing it recenters the image.

---

## Test Impact

The repository has **no unit test project** (`src/` holds the app only): nothing testable is created
or updated. The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] An image already in the grid whose cell changes shape **because of** an arrival or a deletion
  (a new default layout, a new Free ratio): is its zoom computed again, or left as it stands?
- [ ] When Contain's zoom is 100 % (the image already at its cell's ratio): Zoom effect on at 100 %,
  or left off?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-13

Initial design, from the scoping batch (Q&A 1–4): a new workfile taking over 20261012's recentering;
an arriving image — every route, a shifted one included — gets Contain's zoom as a free zoom, the
Zoom effect on, Contain unpressed, frozen afterwards; the Resets keep bringing back 100 %. Texts left
out. Explored directly (one scout pass, the questions chaining on `GridPreview.Add`): the layout and
the Free ratio settle in `OnImagesChanged`, the Free ratio and the canvas sizing do not read the
zoom. Two questions left open: the images already present whose cell changes shape, and the 100 % case.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository — does not apply |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | The recentering is already designed in 20261012 (not implemented): how to organise? | A new workfile taking 20261012 over; both implemented together, 20261012 marked as taken over | 2026-10-13 |
| 2 | "Like Contain without activating it": what becomes of an arriving image's zoom? | A free zoom set at arrival to Contain's zoom, the Zoom effect on, Contain unpressed; frozen afterwards; the Reset brings back 100 % | 2026-10-13 |
| 3 | Which arrivals? | Every arrival route, and an image shifted after a deletion; not a swap nor an undo restore | 2026-10-13 |
| 4 | Depth of the codebase exploration? | Straightforward — a single scout pass | 2026-10-13 |
| 5 | An image already in the grid whose cell changes shape because of an arrival or a deletion: zoom computed again? | | |
| 6 | Contain's zoom at 100 %: Zoom effect on at 100 %, or left off? | | |

---

*Last updated: 2026-10-13*
