# Keyboard Image Move

> Working document — moving the selected image within its cell with the arrow keys, Ctrl for a
> larger step.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today an image is moved within its cell by a mouse drag only (`GridPreview.PanBy`, the Zoom
effect's `Focus`). The arrow keys give the same move with a precise step: **1 px** per press,
**10 px** with **Ctrl**. They act only while the **Zoom** tab is selected, on the **selected cell**.

Relevant components:

| Component | Role |
|---|---|
| `UI/MainForm.cs` — `ProcessCmdKey` | Form-level shortcuts (Ctrl+V/C/S, Delete, Escape); the arrows are handled here, since the preview is not focusable |
| `UI/GridPreview.cs` — `PanBy` | Moves an image by a delta in pixels from where it is shown: magnetic stops (`PanMagnet`, 24 px resistance, Shift = free), never past the share of the cell it keeps covering, stored as `ImageLook.WithFocus` |
| `UI/PanMagnet.cs` | The magnetic stops of one axis: the cell's center and edges |
| `Composition/ImageLook.cs` — `WithFocus` | Stores the move in fractions of the oriented image and **activates the Zoom effect** |

---

## Behaviour

### Keys

| Keys | Move of the selected image |
|---|---|
| ← → ↑ ↓ | 1 px in that direction, on the preview |
| Ctrl + ← → ↑ ↓ | 10 px in that direction |
| Shift added to either | The same step, ignoring the magnetic stops — like Shift during a drag |
| Held down | Repeats at the keyboard's repeat rate, like successive presses |

- A **pixel** is a **screen pixel** of the preview (device unit, not scaled with
  `LogicalToDeviceUnits`): the finest step whatever Windows' display scaling.
- The move goes through **`PanBy`**, as a drag of that delta: the same limit (never past the share
  of the cell the image keeps covering), the same storage in fractions (it survives resizing,
  layout and format changes), the same activation of the Zoom effect — acting on the image
  activates the effect, as § Options Toolbar requires.

### When the Arrows Act

All of these hold, otherwise the arrows keep their usual behaviour:

- the **Zoom** tab is the selected effect tab;
- a cell is selected and holds an image;
- the grid is not locked (no export running), like the drag.

### Magnetic Stops

The drag's stops — the cell's center and its edges — hold a move by the arrows for **one press**:

- A press that reaches or passes a stop **stops exactly on it**, its fluorescent green guide shown
  (`PaintPanGuides`, as during a drag).
- The **next press** in a direction leaves it, by one step from the stop; moving back inward over
  an edge stop is free, as for the drag.
- The guide stays while the stop holds: it goes with the next arrow press leaving it, a mouse press
  in the preview, another cell or another tab selected.
- Shift + arrows ignore the stops (see § Keys).

### Focus

The arrows are taken from the form (`ProcessCmdKey`) whenever § When the Arrows Act holds,
**whichever control has the focus** — the Zoom slider and the file explorer included — **except a
text box being typed in** (the file explorer's search box, `_explorer.IsEditingText`), which keeps
its arrows and Ctrl + arrows. Outside these conditions, every control keeps its arrows.

---

## Test Impact

The repository holds **no test project**: nothing testable by unit tests is created or updated.
The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~What do the magnetic stops (center, edges) do to a move by the arrows?~~ → They hold for one press: a press stops on the stop, guide shown, the next one leaves it; Shift ignores them
- [x] ~~Which focused controls keep their own arrows (the Zoom slider, the file explorer, a text box)?~~ → Only a text box being typed in; otherwise the image gets them
- [x] ~~Is a "pixel" a screen pixel, or a logical pixel scaled with Windows' display scaling?~~ → A screen pixel

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-06

Scoping answered by the user: the arrows move the image **within its cell** (the pan), not into
another cell; 1 px per press, 10 px with Ctrl; **Zoom tab only**. Exploration found the drag's
`PanBy` reusable as is, and the arrows to be handled in `MainForm.ProcessCmdKey`, the preview
taking no keyboard focus. Three points stay open: the magnetic stops, the focus conflicts, the
pixel unit.

### Iteration 2 — 2026-10-06

The three open points settled by the user: the magnetic stops hold a move by the arrows for one
press (guide shown, Shift ignoring them); the arrows go to the image whatever has the focus, except
a text box being typed in; the step is in screen pixels. § Keys, § Magnetic Stops and § Focus
updated.

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
| 1 | What does "moving the image" with the keyboard mean? | Within its cell (the pan) | 2026-10-06 |
| 2 | Steps for the arrows alone and with Ctrl? | 1 px / 10 px, on the preview | 2026-10-06 |
| 3 | When do the arrows act on the selected image? | Zoom tab selected only | 2026-10-06 |
| 4 | Straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-06 |
| 5 | What do the magnetic stops do to a move by the arrows? | Hold for one press, guide shown; Shift + arrows ignore them | 2026-10-06 |
| 6 | Which focused controls keep their own arrows? | Only a text box being typed in; the image gets them otherwise, the Zoom slider included | 2026-10-06 |
| 7 | Screen pixel or logical pixel? | Screen pixel | 2026-10-06 |

---

*Last updated: 2026-10-06*
