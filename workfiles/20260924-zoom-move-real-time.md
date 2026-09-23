# Zoom Move Real Time

> Working document — show panning and zooming a cell live, while the gesture is in progress.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

When a zoomed-in image is dragged inside its cell (pan), or its zoom is changed, the preview should
follow the gesture **in real time** instead of catching up only when the mouse stops or is released.

Scope agreed with the user (Q&A #1–#4):

- Gestures: **pan** (drag inside a zoomed cell), **zoom slider** (the vertical pill on the cell's left
  edge), and **mouse-wheel zoom**.
- Quality trade-off: **light preview during the gesture, full-quality render at its end**.
- **Video and animated cells behave the same** as still images.
- Exploration depth: straightforward — a single scout pass.
- Deliverables: code and README; no unit tests (Q&A #8, #9).

Relevant components: `UI/GridPreview.cs` (gestures, `_cache` bitmap, `SetLook`, `RedrawCell`),
`Composition/Compositor.cs` (`DrawCell`), `UI/AnimationPlayer.cs` (frame decoding at display size).

---

## Current Behaviour (findings)

- `OnMouseMove` → `PanBy` / `ZoomTo` → `SetLook` → `UpdateDisplaySizes` + `RedrawCell` on **every**
  mouse move. The look is updated live; only the display lags.
- `RedrawCell` redraws the cell into `_cache` synchronously with `Compositor.DrawCell`, which uses
  `HighQualityBicubic` + `HighQuality` pixel offset/compositing from the **full-resolution** source
  bitmap (`image.Bitmap`), then calls `Invalidate(cell)`.
- `Invalidate` only posts a `WM_PAINT`, the lowest-priority message. While each mouse move costs a
  full-quality redraw, a new `WM_MOUSEMOVE` is always waiting when the handler returns, so the paint
  is starved: the screen updates only when the mouse pauses or is released. This is the symptom.
- `UpdateDisplaySizes` calls `_player.SetDisplaySize` for every cell on every zoom step: for an
  animated/video cell, a new zoom may mean a new decode size.
- There is **no mouse-wheel zoom** today (`GridPreview` has no `OnMouseWheel`). The only zoom control
  is the slider. Zoom range: `ImageLook.MinZoom` … `ImageLook.MaxZoom`, log scale, snap to 100 %.
- The README does not document zoom or pan.

---

## Design

### Live display during a gesture

1. **Synchronous paint**: while a gesture is live, `RedrawCell` follows `Invalidate(cell)` with
   `Update()`, so the cell is painted before the next mouse move is processed.
2. **Light render**: while a gesture is live, the cell is drawn in a *fast* mode —
   `Compositor.DrawCell(..., fast: true)` uses bilinear interpolation and high-speed compositing; the
   pixel offset stays `HighQuality`, since changing it would shift the frame by half a pixel.
   Geometry (fit, crop threshold, zoom, focus, orientation, grayscale, band color) is unchanged, so
   the light frame sits exactly where the final one will.
3. **Final render**: when the gesture ends, the cell is redrawn once at full quality.
4. **One live image at a time** (`GridPreview._live`): `BeginLive` on another image first ends the
   previous gesture (its full render included); `EndLive` refreshes the decode sizes and redraws the
   cell in full.

### Gesture boundaries

| Gesture | Starts | Ends (final render) |
|---|---|---|
| Pan | first `PanBy` of a press on a zoomed cell | mouse up / capture lost |
| Zoom slider | mouse down on the slider | mouse up / capture lost |
| Wheel zoom | first wheel notch | ~150 ms after the last notch |

### Video and animated cells

Same path as still images: the frame drawn is the cell's current frame (the hovered/zoomed cell is
already held still by `UpdateHold`). The decode size handed to `AnimationPlayer` is refreshed at the
end of the gesture rather than on every step, the frame being scaled from its current size meanwhile.

### Wheel zoom

New gesture (Q&A #5–#7):

- **Input**: the plain wheel over a cell zooms that cell — no modifier. Ignored while the preview is
  locked (export in progress), like the zoom slider, and while another gesture runs (pan, cell drag,
  zoom or page slider).
- **Step**: one notch (`WHEEL_DELTA` = 120, partial deltas accumulated per cell, reset when the wheel
  moves to another cell) multiplies the zoom by
  2^(1/4) — four notches double it — clamped to `ImageLook.MinZoom` … `ImageLook.MaxZoom`; crossing
  100 % lands on exactly 100 %, like the slider's snap.
- **Anchor**: the point of the image under the cursor stays under the cursor; the focus is shifted
  accordingly (then clamped by the fit as usual). A cursor over a band anchors on the nearest image
  edge. At or below 100 % the focus goes back to the center, the existing `ImageLook.WithZoom` rule.
- **End**: the full-quality render happens after ~150 ms without a notch (a one-shot timer, restarted
  on each notch).

---

## Test Impact

No unit test is created or updated: the user chose to keep the solution test-free (Q&A #8), as every
previous workfile did. The gesture boundaries, fast/final render switch, wheel step, snap and cursor
anchor stay untested by decision, not because nothing testable changes. Verification is manual: pan
and slider-zoom a large photo, wheel-zoom on a corner of a still image, then the same on a video and a
GIF cell; check the final frame sharpens on release / after the wheel stops.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (declined, Q&A #8) | — | — |

---

## Documentation

README: add a short description of the zoom and pan gestures — zoom slider, wheel zoom under the
cursor, drag to pan a zoomed image, Ctrl + drag to swap (Q&A #9).

---

## Open Questions

- [x] ~~Wheel zoom: which input zooms — the plain wheel over a cell, or Ctrl + wheel only?~~ → Plain wheel
- [x] ~~Wheel zoom: anchored on the point under the cursor, or on the current center (focus)?~~ → Under the cursor
- [x] ~~Wheel zoom: when does the full-quality render happen — after a short quiet delay (~150 ms) following the last notch?~~ → Yes, ~150 ms after the last notch
- [x] ~~Unit tests: keep the solution test-free, as in every previous workfile?~~ → Yes, no unit tests
- [x] ~~README: document the zoom/pan gestures (including the new wheel zoom), or leave it as is?~~ → Document them

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial design from the scoping batch and one scout pass over `GridPreview` / `Compositor`.
Root cause identified: full-quality synchronous redraws on every mouse move starve `WM_PAINT`.
Proposal: synchronous `Update()` during gestures, a fast `DrawCell` mode while the gesture lasts,
one full-quality redraw at its end; animated decode size refreshed at gesture end. Wheel zoom is a
new gesture (it does not exist yet) — its behaviour is left to Open Questions.

### Iteration 2 — 2026-09-24

Open questions answered (Q&A #5–#9). Wheel zoom specified: plain wheel, anchored under the cursor,
full-quality render ~150 ms after the last notch; step of 2^(1/4) per notch with the 100 % snap and
ignored while locked (proposed by the agent, part of the design submitted for the go). Deliverables:
code and README; unit tests declined.

### Iteration 3 — 2026-09-24 — ✅ Implemented

Go given ("Go implémente") after a first "No" at the gate. Taken as the design's full deliverables —
code and README, unit tests declined by design. Branch Gate: stays on `main`, the standing choice for
this repository.

### Iteration 4 — 2026-09-24 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Go read as the full scope**: "Go implémente" matched no gate option word for word; taken as the
  design's deliverables (code + README), since the design already listed the README and declined tests.
- **Branch Gate not asked**: stayed on `main`, the standing choice recorded for this repository.
- **Pixel offset kept in fast mode** (divergent): the design said "default pixel offset"; kept
  `HighQuality` so the light frame does not jump half a pixel against the final one.
- **One live image at a time**: a gesture started on another cell (e.g. a pan within 150 ms of a wheel
  zoom elsewhere) ends the previous one first, with its full render.
- **Pan goes live on its first move**, not on the press — a plain click on a zoomed cell draws nothing new.
- **Wheel ignored during another gesture**, and partial deltas accumulated per cell.
- **Anchor details**: the point under the cursor is clamped to the image when over a band; at or below
  100 % the focus returns to the center (existing `WithZoom` rule), so the anchor applies above 100 % only.

Parallel work: another session committed on `main` during the run (`ce497f6`, a workfile); only the
files of this run were committed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-09-24 | `cf72d93` fast `DrawCell`, `e6c55db` live pan and zoom slider, `ca58a79` wheel zoom; builds with 0 warnings. Manual verification pending |
| Unit tests | 2 | 2026-09-24 | Declined — solution kept test-free (Q&A #8) |
| README | 3 | 2026-09-24 | `3f84202` zoom slider, wheel zoom, pan and live display |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which gestures should refresh the display in real time? | Pan, wheel zoom, and the zoom slider | 2026-09-24 |
| 2 | If the full render is too heavy to follow the mouse, which trade-off during the gesture? | Light preview during the gesture, full render at its end | 2026-09-24 |
| 3 | Are video / animated cells concerned? | Yes, same behaviour | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-24 |
| 5 | Wheel zoom: plain wheel or Ctrl + wheel? | Plain wheel | 2026-09-24 |
| 6 | Wheel zoom: anchored on the cursor or on the center? | Under the cursor | 2026-09-24 |
| 7 | Wheel zoom: full-quality render after a short quiet delay? | Yes, ~150 ms after the last notch | 2026-09-24 |
| 8 | Unit tests: keep the solution test-free? | Yes — unit tests not selected | 2026-09-24 |
| 9 | README: document the zoom/pan gestures? | Yes | 2026-09-24 |

---

*Last updated: 2026-09-24*
