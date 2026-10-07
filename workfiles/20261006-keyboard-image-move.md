# Keyboard Image Move

> Working document — moving the selected image within its cell with the arrow keys, Ctrl for a
> larger step.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today an image is moved within its cell by a mouse drag only (`GridPreview.PanBy`, the Zoom
effect's `Focus`). The arrow keys give the same move with a precise step: **1 px** per press,
**10 px** with **Ctrl**. They act on the **selected cell**, whatever tab is selected, while the
preview has the keyboard focus.

Relevant components:

| Component | Role |
|---|---|
| `UI/MainForm.cs` — `ProcessCmdKey` | Form-level shortcuts (Ctrl+V/C/S, Delete, Escape); the arrows are handled here while the preview has the focus |
| `UI/GridPreview.cs` — `PanBy` | Moves an image by a delta in pixels from where it is shown: magnetic stops (`PanMagnet`, 24 px resistance, Shift = free), never past the share of the cell it keeps covering, stored as `ImageLook.WithFocus` |
| `UI/PanMagnet.cs` | The magnetic stops of one axis: the cell's center and edges |
| `Composition/ImageLook.cs` — `WithFocus` | Stores the move in fractions of the oriented image and **activates the Zoom effect** |
| `UI/GridPreview.cs` — `PanSelected`, `EndKeyPan` | The arrows' entry point: `PanBy` with no resistance, then `PanMagnet.Settle` on both axes; the move's guides and its end |

---

## Behaviour

### Keys

| Keys | Move of the selected image |
|---|---|
| ← → ↑ ↓ | 1 px in that direction, on the preview |
| Ctrl + ← → ↑ ↓ | 10 px in that direction |
| Shift + ← → ↑ ↓ | 1 px, ignoring the magnetic stops — like Shift during a drag |
| Ctrl + Shift + ← → ↑ ↓ | **Straight to the next stop** in that direction — the center or an edge, whichever comes first — held there with its guide, as a press landing on it; nothing when no stop lies ahead |
| Held down | Repeats at the keyboard's repeat rate, like successive presses |

- A **pixel** is a **screen pixel** of the preview (device unit, not scaled with
  `LogicalToDeviceUnits`): the finest step whatever Windows' display scaling.
- The move goes through **`PanBy`**, as a drag of that delta: the same limit (never past the share
  of the cell the image keeps covering), the same storage in fractions (it survives resizing,
  layout and format changes), the same activation of the Zoom effect — acting on the image
  activates the effect, as § Options Toolbar requires.

### When the Arrows Act

All of these hold, otherwise the arrows keep their usual behaviour:

- the **preview has the keyboard focus** (see § Focus) — whatever effect tab is selected, or none;
- a cell is selected and holds an image;
- the selected cell does not show the **crop's edit view** (Crop tab selected, crop on), where a
  drag does not move the image either;
- the grid is not locked (no export running), like the drag.

### Magnetic Stops

The drag's stops — the cell's center and its edges — hold a move by the arrows for **one press**:

- A press that reaches or passes a stop **stops exactly on it**, its fluorescent green guide shown
  (`PaintPanGuides`, as during a drag).
- The **next press** in a direction leaves it, by one step from the stop.
- An **edge** stop holds both ways: on the way **out**, as for the drag, and on the way **in** too —
  a press moving back inside over it stops on it, which the drag does not do. At 1 px the image
  lands on the edge anyway: the stop only adds the guide and one press on it.
- The guide stays while the stop holds: it goes with the next arrow press leaving it, a mouse press
  in the preview, another cell or another tab selected.
- Shift + arrows ignore the stops (see § Keys).
- Implemented as `PanBy` with a **resistance of 0**, `stepwise` — the inward edge stops, and a
  stop a press lands on exactly holding it as one it passes —, followed by
  `PanMagnet.Settle()`, which forgets how far the press went past the stop: the next press leaves it
  from the stop itself. A move with no delta on an axis keeps that axis' stop holding.
- The move by the arrows **goes on** while the selected image keeps the image and the look the
  last press left it (`GridPreview._keyPan`): any other change of its look — the wheel, the zoom
  slider, an effect — ends it like the gestures above, and the next press starts with no stop
  held.

### Focus

The **focused control keeps its arrows**: a slider, a list, the file explorer's tiles or its search
box. The preview takes the keyboard focus when it is **clicked** (a mouse press on it), and the
arrows then move the selected image, taken from the form (`ProcessCmdKey`) while
`GridPreview` is focused. Clicking another control gives it the arrows back.

- `GridPreview.IsInputKey` claims the arrows, so an arrow the image does not take (no cell, the
  crop's edit view) never moves the focus to the next control.
- Selecting another tab ends the move by the arrows (`MainForm.SelectEffect`), its guides with it.

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
- [x] ~~Which focused controls keep their own arrows (the Zoom slider, the file explorer, a text box)?~~ → Only a text box being typed in; otherwise the image gets them *(revised 2026-10-06, see Iteration 6)*
- [x] ~~Is a "pixel" a screen pixel, or a logical pixel scaled with Windows' display scaling?~~ → A screen pixel
- [x] ~~Does the edge stop's inward hold apply with Ctrl only, or to the 1 px steps too?~~ → Both steps
- [x] ~~With every tab moving the image, which focused controls keep their arrows?~~ → The focused control; a click on the preview gives it the arrows *(revises the earlier focus decision, see Iteration 6)*

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

### Iteration 3 — 2026-10-06 — ✅ Implemented

Go given by the user: **code and documentation** (no test project, so no unit test), in a
dedicated worktree (`feature/keyboard-image-move`), fast-forwarded into `main` and removed at the
end.

### Iteration 4 — 2026-10-06 — 🧭 Implementation choices

- **One-press hold**: `PanMagnet` gained `Settle()` (the overshoot past the stop forgotten) and an
  early return for a move of 0 on an axis, keeping its stop held — without it, a settled edge stop
  would let go on the other axis' press. Neutral for the drag, whose overshoot is never exactly 0.
- **End of the move by the arrows**: tracked as the image and the look the last press left
  (`_keyPan`), rather than hooking every route that changes an image — a mouse press, another
  cell, another tab (`MainForm.UpdateEffects`) clear it explicitly; any other change of the
  image or its look ends it by itself.
- **Redraw**: each press opens the live gesture (`BeginLive`) and restarts the wheel's end timer,
  so the cell is redrawn in full once the keys stop, as after the wheel.
- **Steps**: `MainForm.ArrowStep` (1) and `ArrowControlStep` (10), screen pixels passed as is to
  `PanBy`, which works in client pixels.
- **Documentation**: the README only (§ Drag an image to move it, and the file explorer's arrows).
  RULES and GLOSSARY unchanged: no new term, no rule beyond this feature.
- No rule broken.

### Iteration 5 — 2026-10-06 — ⚙️ Post-implementation — Edge stops both ways, every tab

Requested by the user after testing:

1. With **Ctrl**, an **edge** stop must hold the image for one press **on the way in too**, not
   only on the way out — today a press moving back inward crosses it freely, as the drag does.
2. The arrows move the image **whatever tab is selected**, not with the Zoom tab only.

Open before the code is touched: whether the inward hold is for Ctrl only, and which focused
controls keep their arrows now that every tab moves the image.

### Iteration 6 — 2026-10-06 — ⚙️ Post-implementation — Focus and inward hold settled

The user settled the two points: the inward edge hold applies to both steps; the focused control
keeps its arrows, a click on the preview giving it the focus. Decided along with them, as the drag
does: no arrow move in the crop's edit view. § When the Arrows Act, § Magnetic Stops and § Focus
updated.

Implemented: `PanMagnet.Move` takes `bothWays`, adding the edge crossed inward — reached only when
strictly ahead, so leaving an edge inward stays free; `GridPreview` takes the focus on a mouse
press and claims the arrows (`IsInputKey`); `MainForm.ProcessCmdKey` checks `_preview.Focused`
instead of the Zoom tab and the search box. Choices not stated by the design: the `IsInputKey`
override, and the end of the move on a tab change moved from `UpdateEffects` to `SelectEffect`.

### Iteration 7 — 2026-10-06 — ⚙️ Post-implementation — A stop reached exactly holds too

Reported by the user after testing: no green bar on the way in. The code only held a press that
went **past** a stop; a press landing **exactly** on it — always at 1 px, and at 10 px when going
out one step and back one — neither stopped nor showed the guide, betraying § Magnetic Stops ("a
press that reaches or passes a stop"). The same held the center at 1 px. Fix: for the arrows, a
stop reached exactly holds the press as one passed; `PanMagnet.Move`'s `bothWays` becomes
`stepwise`, covering both.

### Iteration 8 — 2026-10-07 — ⚙️ Post-implementation — Ctrl + Shift jumps to the edge

Requested by the user after testing: **Ctrl + Shift + arrow** moves the image **straight to the
edge** concerned — the edge stop and its dashed green guide — instead of 10 px ignoring the stops.

Open before the code is touched: which stops the jump lands on (the edges only, or the center
too).

### Iteration 9 — 2026-10-07 — ⚙️ Post-implementation — The jump lands on the next stop

The user chose the **next stop**, the center included: from an edge, two presses reach the other
edge. Ctrl + Shift no longer gives a 10 px step ignoring the stops; Shift alone still gives 1 px
ignoring them. Nothing ahead → nothing moves. § Keys updated.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4, 6, 7 | 2026-10-06 | `PanMagnet.Settle` / `stepwise`, `GridPreview.PanSelected` / `EndKeyPan` / focus, `MainForm.ProcessCmdKey` |
| Unit tests | 3 | 2026-10-06 | Not applicable: no test project |
| README | 3, 6 | 2026-10-06 | Arrow keys under the image move: every tab, the preview's focus, edge stops both ways |

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
| 8 | Is the task finished? (after testing) | No: with Ctrl, an edge stop must hold one press inward too; the arrows move the image whatever tab is selected | 2026-10-06 |
| 9 | Inward edge hold: Ctrl only, or the 1 px steps too? | Both steps | 2026-10-06 |
| 10 | With every tab moving the image, which focused controls keep their arrows? | The focused control; a click on the preview gives it the arrows | 2026-10-06 |
| 11 | Is the task finished? (after testing the adjustment) | No: no green bar seen on the way in — relaunch | 2026-10-06 |
| 12 | Is the task finished? (after testing the exact-landing fix) | No: Ctrl + Shift + arrow moves the image straight to the edge concerned | 2026-10-07 |
| 13 | Which stops does Ctrl + Shift land on: the edges only, or the center too? | The next stop ahead, the center included | 2026-10-07 |

---

*Last updated: 2026-10-07*
