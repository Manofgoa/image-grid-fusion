# Outline on Hover

> Working document — a discreet outline on the cell under the cursor, distinct from the selection
> border, so the user always sees which image the pointer is on.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, hovering a cell only shows its **×** close button; nothing else tells which image the
cursor is on. The selection is a 3 px `SystemColors.Highlight` border. This task adds a **hover
outline**: a thin, neutral, semi-transparent contour, clearly lighter than the selection, on the
cell under the cursor.

Scope agreed in the scoping batch (Q&A #1–#3):

- **Style**: discreet neutral — thin white/grey semi-transparent contour, never in the selection
  colour.
- **Where**: every cell under the cursor, **including the selected cell** (both contours coexist)
  and **during a drag**.
- **Depth**: straightforward.

Components involved:

- `src/ImageGridFusion/UI/GridPreview.cs`: custom control that paints the preview. It already
  tracks the hovered cell (`_hovered`, updated by `UpdateHover` on mouse move, reset on mouse
  leave, removal and drag end), the swap drag target (`_dropTarget`) and the Explorer drop target
  (`_externalTarget`, set by `ShowDropTarget`). The selection border is painted in `OnPaint` with
  `Pen(SystemColors.Highlight, 3)` and `PenAlignment.Inset`.
- `src/ImageGridFusion/UI/MainForm.cs`: forwards the Explorer drag position to
  `GridPreview.ShowDropTarget` (no change expected).

Finding from the exploration: **there are no empty cells**. `GridLayout.Cells(count, …)` lays out
exactly as many cells as there are images (1 to 4), filling the canvas. The only "empty" surface
is the empty state (0 images), a dashed canvas with a hint text. The scoping answer "empty cells"
therefore maps to that empty state, which **does** get the outline (Q&A #4).

---

## Hover Outline

### Look

- Contour of the hovered cell, **inset** inside the cell (`PenAlignment.Inset`, like the
  selection), so two adjacent cells never share a line.
- **1 logical px** (`LogicalToDeviceUnits(1)`), **white at ~50 % alpha**
  (`Color.FromArgb(128, 255, 255, 255)`) (Q&A #6).
- On the **selected cell**, the hover outline is drawn **just inside** the selection border
  (rectangle deflated by the selection thickness), so both stay visible instead of the thicker
  one hiding the other (Q&A #6).
- On the **empty canvas**, the outline is drawn just inside its dashed border, so it does not
  merge with the dashes.

### When It Shows

| Situation | Outlined cell |
|---|---|
| Mouse move, no button pressed | `_hovered` (already tracked) |
| Cursor on the **×** | The same cell — the outline stays |
| Swap drag (cell onto cell) | The cell under the cursor (`_dropTarget`), **source cell included**, drawn above its dimming (Q&A #5) |
| Explorer file drag | The cell under the cursor, only when the cursor is on a cell — not when the drop falls back to the whole canvas or the excess-rule cell (Q&A #5) |
| Empty state (0 images), mouse move or Explorer file drag | The **whole canvas**, while the cursor is on it (Q&A #4, #5) |
| Cursor leaves the control, cell removed | None (already reset today) |

The empty canvas has no cell index, so `GridPreview` tracks "cursor on the empty canvas" next to
`_hovered`, both for mouse moves and for `ShowDropTarget`.

### Paint Order

In `OnPaint`: composition → source dimming → drop highlight → selection border → **hover
outline** → **×** → ghost. The ghost stays on top of everything.

---

## Test Impact

**No unit test changes**: the solution has no test project, and the whole change is painting in a
WinForms control (`GridPreview.OnPaint`), with no logic that can be asserted outside a rendered
control.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (painting only, no test project) | — | — |

---

## Open Questions

- [x] ~~Empty state (0 images): does the dashed canvas get a hover outline?~~ → **Yes**, the whole
      canvas while the cursor is on it (Q&A #4)
- [x] ~~During drags, which cell gets the outline?~~ → The **cell under the cursor** in both the
      swap drag (source included) and the Explorer file drag (only on a cell, or on the empty
      canvas) (Q&A #5)
- [x] ~~Exact look?~~ → 1 px white at ~50 % alpha, inside the selection border on the selected
      cell (Q&A #6)
- [x] ~~README: mention the outline?~~ → **Yes**, e.g. "Hover a cell to outline it and show a
      **×** to remove it" (Q&A #7)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design from the scoping batch: discreet neutral outline, inset, on the hovered cell,
coexisting with the selection border (drawn inside it) and kept during drags. Exploration showed
there are no empty cells, only the 0-image empty state. Four questions left open: empty state,
drag behaviour, exact look, README wording.

### Iteration 2 — 2026-09-23

Open questions answered (Q&A #4–#7). Changed from the proposals: the **empty canvas does get the
outline** (proposed: no). Derived from #4 and #5 together: the outline covers the empty canvas
during an Explorer file drag too, since it follows whatever surface is under the cursor. Look,
drag behaviour and README update as proposed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Not applicable: no test project, painting only |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Hover outline style? | Discreet neutral (thin white/grey semi-transparent) | 2026-09-23 |
| 2 | Where does the outline appear? | Filled cells, empty cells, the selected cell, during a drag | 2026-09-23 |
| 3 | Exploration depth? | Straightforward | 2026-09-23 |
| 4 | Empty state (0 images): hover outline on the dashed canvas? | Yes | 2026-09-23 |
| 5 | During drags, which cell gets the outline? | The cell under the cursor, in both drags, source cell included | 2026-09-23 |
| 6 | Exact look: 1 px white ~50 % alpha, inside the selection on the selected cell? | Yes | 2026-09-23 |
| 7 | README: mention the outline on the hover line? | Yes | 2026-09-23 |
| 8 | Go for implementation? | | |

---

*Last updated: 2026-09-23*
