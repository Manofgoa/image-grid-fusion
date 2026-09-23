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
2. **Light render**: while a gesture is live, the cell is drawn in a *fast* mode — `DrawCell` takes a
   quality flag; fast mode uses a cheap interpolation (bilinear) and default pixel offset/compositing.
   Geometry (fit, crop threshold, zoom, focus, orientation, grayscale, dominant-color bands) is
   unchanged, so the light frame sits exactly where the final one will.
3. **Final render**: when the gesture ends, the cell is redrawn once at full quality.

### Gesture boundaries

| Gesture | Starts | Ends (final render) |
|---|---|---|
| Pan | first `PanBy` of a press on a zoomed cell | mouse up / capture lost |
| Zoom slider | mouse down on the slider | mouse up / capture lost |
| Wheel zoom | first wheel notch | a short quiet delay after the last notch (see Open Questions) |

### Video and animated cells

Same path as still images: the frame drawn is the cell's current frame (the hovered/zoomed cell is
already held still by `UpdateHold`). The decode size handed to `AnimationPlayer` is refreshed at the
end of the gesture rather than on every step, the frame being scaled from its current size meanwhile.

### Wheel zoom

New gesture — see Open Questions for its exact behaviour.

---

## Test Impact

To be settled — see Open Questions. Every previous workfile kept the solution test-free by user
decision; nothing is written here until that is confirmed for this one.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [ ] Wheel zoom: which input zooms — the plain wheel over a cell, or Ctrl + wheel only?
- [ ] Wheel zoom: anchored on the point under the cursor, or on the current center (focus)?
- [ ] Wheel zoom: when does the full-quality render happen — after a short quiet delay (~150 ms) following the last notch?
- [ ] Unit tests: keep the solution test-free, as in every previous workfile?
- [ ] README: document the zoom/pan gestures (including the new wheel zoom), or leave it as is?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which gestures should refresh the display in real time? | Pan, wheel zoom, and the zoom slider | 2026-09-24 |
| 2 | If the full render is too heavy to follow the mouse, which trade-off during the gesture? | Light preview during the gesture, full render at its end | 2026-09-24 |
| 3 | Are video / animated cells concerned? | Yes, same behaviour | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-24 |
| 5 | Wheel zoom: plain wheel or Ctrl + wheel? | | |
| 6 | Wheel zoom: anchored on the cursor or on the center? | | |
| 7 | Wheel zoom: full-quality render after a short quiet delay? | | |
| 8 | Unit tests: keep the solution test-free? | | |
| 9 | README: document the zoom/pan gestures? | | |

---

*Last updated: 2026-09-24*
