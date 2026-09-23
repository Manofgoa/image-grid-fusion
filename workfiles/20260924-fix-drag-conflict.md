# Fix Drag Conflict

> Working document — a dedicated drag handle for swapping cells, so that dragging anywhere else
> in a cell always moves the image within it.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a left-button drag in a cell does one of two things, decided at press time in
`GridPreview.OnMouseDown` (`src/ImageGridFusion/UI/GridPreview.cs`):

- **Pan** (move the image within its cell) when `Look.Zoom > 1` and `Ctrl` is not held;
- **Swap** (drag & drop onto another cell, with a ghost) otherwise.

The user wants to move the image in its cell and sometimes gets a swap instead — whenever the
image is at 100 % or below, or `Ctrl` is held. The fix separates the two gestures by **where**
the drag starts, not by the zoom state:

- a **drag handle** icon, shown on the hovered cell, starts the swap;
- a drag started **anywhere else** in the cell always pans.

---

## Current Behaviour (codebase findings)

| Point | Where | Finding |
|---|---|---|
| Gesture choice | `OnMouseDown`, `_panning = Look.Zoom > 1 && !Ctrl` | Swap is the fallback whenever the image is not zoomed in |
| Swap start | `OnMouseMove` → `StartDrag` once the pointer leaves the system drag rectangle | Ghost taken from the cell, grab point kept relative to the press point |
| Pan | `PanBy` → `Look.WithFocus` | Moves the focus; clamped by `FitCalculator.Compute` so the cell never shows beyond the image |
| Pan at ≤ 100 % | `ImageLook.WithZoom` resets `Focus` to the center at ≤ 100 % | "The image is centered again, as the fitting rule draws it" — nothing to pan |
| Top-right corner | `CloseBounds`: 24 px × at `cell.Right - 6`, `cell.Y + 6` | Already occupied by the remove button |
| Toolbar | `ToolSlot`: rows of 24 px buttons left of the ×, wrapping when narrow | The column **below** the × is always free |
| Left edge | `ZoomSliderBounds`: vertical pill below the toolbar rows | — |
| Bottom | `SliderBounds`: page slider across the bottom (multi-page images only) | — |
| Overlays hidden | Close/toolbar/zoom drawn only when hovered, `!_dragging`, `!_locked` | Same rule fits the handle |
| Docs | `README.md` lines 21 and 23 | "Drag a zoomed-in image to move it…; `Ctrl` + drag swaps it instead" / "Drag a cell onto another to swap the two images" |
| Tests | No test project in the repository | UI gesture code, nothing unit-testable |

---

## Drag Handle

- **Glyph**: four-arrow move symbol (✥), drawn in white on the same dark translucent disc as the
  × (`150` alpha, `230` when hot), 24 px logical.
- **Visibility**: only on the hovered cell, under the same conditions as the × and the toolbar
  (not while dragging, not while the grid is locked by an export).
- **Placement**: top-right of the cell — *see Open Questions* (the × already sits there).
- **Hover**: `SizeAll` cursor over the handle (it announces a move, not a click), disc turns hot.
- **Press on the handle**: selects the cell (as a press on the image does today) and arms the swap;
  the swap starts once the pointer leaves the system drag rectangle, exactly as today (ghost,
  drop-target highlight, swap on release over another cell).
- **Click without moving**: selects the cell, nothing else.

## Pan (everywhere else in the cell)

- A press anywhere in the cell that is not the ×, a toolbar button, the zoom slider, the page slider
  or the handle **always** arms a pan — whatever the zoom.
- At 100 % or below, the pan does nothing (the fitting rule keeps the image centered): the gesture
  never turns into a swap. The press still selects the cell.
- `Ctrl` + drag — *see Open Questions*.

## Documentation

`README.md`, the two gesture lines under the cell actions: the swap starts from the handle, a drag
elsewhere moves a zoomed-in image within its cell.

---

## Test Impact

Nothing testable changes: the repository has no test project, and the whole change is mouse
gesture routing and painting inside `GridPreview` (a WinForms control). Validated by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit-testable behaviour) | — | — |

---

## Open Questions

- [ ] The × already takes the top-right corner. Where does the handle go: just **below** the ×
  (the toolbar keeps all its slots), just **left** of the × (the toolbar loses one slot per row
  and wraps sooner), or **in the corner**, the × moving one slot left?
- [ ] `Ctrl` + drag currently swaps a zoomed-in image. Remove it (the handle is the only way to
  swap), or keep it as a shortcut?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial design from the request and the scoping answers: a move handle (✥), shown on hover only,
is the sole way to start a swap; a drag anywhere else always pans, and simply does nothing when the
image is not zoomed in. Placement next to the existing × and the fate of `Ctrl` + drag left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project, UI gesture code only |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When is the drag & drop icon visible in a cell? | On hover only | 2026-09-24 |
| 2 | Which glyph for the icon? | Four-arrow move symbol ✥ | 2026-09-24 |
| 3 | Outside the icon, is drag & drop entirely impossible? | Yes, only from the icon — elsewhere a drag always pans, even with nothing to move | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — single exploration pass | 2026-09-24 |
| 5 | Where does the handle go, given the × already takes the top-right corner? | | |
| 6 | `Ctrl` + drag: remove it or keep it as a swap shortcut? | | |

---

*Last updated: 2026-09-24*
