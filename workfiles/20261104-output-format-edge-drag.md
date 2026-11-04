# Output Format Edge Drag

> Working document — the canvas's edges and corners dragged in the preview to change the output
> format's shape, which puts the format in Custom.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The output format is chosen in the Format tab of the Global toolbar: fixed formats, Free, and the
**Custom** format of `workfiles/20261104-custom-output-format.md` — a ratio, or exact pixels, typed in
its *Width* and *Height* fields. The canvas is drawn by `GridPreview` in the rectangle
`CanvasBounds()` gives: the largest at `CanvasRatio` within the working area (the control less its
margins and the drop zone), centered — or, with exact pixels, the real size when it fits.

This workfile adds a **direct gesture** (user request, 2026-11-04): while the **Format tab is
selected**, dragging a **vertical edge** of the canvas changes its width, a **horizontal edge** its
height, a **corner** both. The gesture puts the format in **Custom** and writes its *Width* and
*Height* fields, as typing in them would.

No row of `workfiles/TODO-FEATURES.md` matches: the *Custom output format* row is already marked with
its own workfile.

Components: `UI/GridPreview.cs` (`CanvasBounds`, `UpdateRatio`, the mouse handlers, `OnPaint`, the
helper colors), `UI/MainForm.cs` (`ApplyCustom`, `PngSize`, `ShowCustomFields`, the selected global tab →
preview flag next to `LayersActive`), `Composition/CustomFormat.cs` (`Of`, `WithWidth`, `WithHeight`).

---

## When the Edges Take the Mouse (Q&A #1)

- **Only while the Format tab is selected** — as the layers take the mouse only while the Layers tab
  is (RULES.md § Layers): a flag on the preview, `GridPreview.FormatActive`, set by `MainForm` where it
  sets `LayersActive` from the selected global tab.
- **Not** in a layer's edit view (the canvas is the layer's), **not while exporting** (the Format tab is
  locked), not while the eyedropper or the cutout's Pick is armed.
- **Hit area**: a band along each of the canvas's four edges, a few logical px on both sides of it
  (`LogicalToDeviceUnits`), and a square where two bands meet — the corners winning over the edges.
  It takes priority over the cells' gestures **on its own area only** (RULES.md § On-Cell Handles),
  so the cells keep every gesture inside it.
- **Cursors**: ↔ (`SizeWE`) over a vertical edge, ↕ (`SizeNS`) over a horizontal one, the diagonal
  ones (`SizeNWSE`, `SizeNESW`) over the corners.
- **Grips** (Q&A #7): while the Format tab is selected, eight small grips — the middle of each edge and
  the four corners — show where the canvas is dragged. **Helper indicators** (RULES.md § On-Cell Helper
  Indicators): fluorescent green over the black halo, **white** while hovered or dragged, drawn in
  `GridPreview.OnPaint` only. The hit area stays the whole band along each edge, not the grip alone.

## The Drag

- **The opposite edge stays put** (Q&A #5): during the drag, the dragged edge follows the cursor and the
  opposite one does not move — the canvas drawn in a **drag rectangle**, held by `CanvasBounds()` while
  the drag runs, within the working area (the control less its margins and the drop zone). `UpdateRatio`
  holds the ratio meanwhile, as for a separator or a crop bar. **On release**, the canvas is fitted and
  centered again at the new ratio — or drawn at real size with exact pixels, when it fits.
- **Edges** (Q&A #1): left / right change the width, top / bottom the height, the other side unchanged.
- **Corners** (Q&A #3): width and height at once, freely — no ratio kept.
- **Ctrl held — symmetric** (Q&A #3): the opposite edge moves too, by the same amount the other way, so
  the canvas's center stays where it is — the one case where the opposite edge moves during the drag. Read from `ModifierKeys` at each move, so it can be pressed or
  released during the drag.
- **Puts the format in Custom**, through the same route as the fields (`MainForm.ApplyCustom`): the
  format becomes Custom at the first move, its fields and its thumbnail follow live.
- **The Custom mode is kept** (Q&A #2): *Exact pixels* on, the drag changes the *Width* / *Height*
  pixels; off, it changes the ratio. From another format, the mode is the one the Custom fields hold —
  a ratio while they are unset.
- **Scale**: one device pixel of the canvas as shown is worth `PNG size ÷ canvas shown` pixels of the
  fields, taken **once, at the press** — the PNG export's size of the format in force
  (`MainForm.PngSize` at the Copy cap: the exact size with exact pixels, else the native size). At real
  size, one device pixel is one pixel.
- **The fields after a drag** — the drag rectangle × that scale, rounded — in both modes (Q&A #6): with
  exact pixels, the pixels; without, the **native size's pixels** as the ratio's terms (`1847 × 1200`,
  the thumbnail reading `1847:1200`), precise rather than rounded to a readable ratio.
- **Bounds**: the values go through `CustomFormat` (`WithWidth` / `WithHeight` / `Of`): a side within
  its mode's bounds, the shape within 1:10 – 10:1. The canvas drawn during the drag is the one the
  bounded values give, so it never shows a shape the format refuses.
- **One undo step**: the mouse button is held, so `GridHistory` commits once the drag ends (RULES.md
  § Undo History) — nothing to add.
- The **Free ratio** is not involved: the format is Custom from the first move.

## Live Indicator (Q&A #3)

- While the drag runs, a **readout** of what the format becomes: `1080 × 1350 px` with exact pixels,
  the ratio otherwise — its decimal form, `1.54:1`, the fields' terms being too long to read at a
  glance.
- **At the center of the canvas** (Q&A #8), in large type, over the cells; gone at the release.
- A **helper indicator**: fluorescent green over its black halo (`HelperColor`, `HelperHalo`), drawn
  in `GridPreview.OnPaint` only, never in the exports (RULES.md § On-Cell Helper Indicators).

---

## Documentation

- `RULES.md` § Output Format: the Custom format also set by dragging the canvas's edges while the
  Format tab is selected.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: the *Custom format* row mentions the drag.
- `SHORTCUTS.md` / `SHORTCUTS.fr.md`: the edge / corner drag and Ctrl held during it, in § Mouse Over
  the Grid.
- `README.md` / `README.fr.md`: the Format tab's paragraph, if it describes the Custom format.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*): no
unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| Format tab not selected: no resize cursor on the canvas's edges, the cells' gestures unchanged | The running app | — |
| Twitter, drag the right edge: Custom picked, its ratio follows, the fields and the thumbnail live | The running app | — |
| Exact `1080 × 1350` at real size: dragging the right edge by 100 device px gives `1180 × 1350` | The running app | — |
| A corner changes both sides; Ctrl keeps the center in place | The running app | — |
| The bounds: the shape stops at 1:10 / 10:1, a side at 100 px with exact pixels | The running app | — |
| One drag is one undo step; Ctrl+Z gives the previous format back | The running app | — |
| The readout shows during the drag only, at the canvas's center, and never in a Copy | The running app | — |
| The grips show while the Format tab is selected only, white while hovered | The running app | — |
| During a drag the opposite edge stays put; on release the canvas is centered again | The running app | — |

---

## Open Questions

- [x] ~~During the drag, does the opposite edge stay where it is (the canvas re-centered and re-fitted on
  release), or is the canvas re-fitted and re-centered at every move?~~ → The opposite edge stays put,
  the canvas fitted and centered on release (Q&A #5)
- [x] ~~With *Exact pixels* off, what are the ratio's terms after a drag — the PNG native size's pixels
  (`1847:1200`), or a ratio rounded to a readable one?~~ → The native size's pixels (Q&A #6)
- [x] ~~Are the draggable edges shown by drawn grips while the Format tab is selected, or by the cursor
  only?~~ → Green grips at the edges' middles and the corners (Q&A #7)
- [x] ~~Where does the live readout stand — the canvas's center, or next to the cursor?~~ → At the
  canvas's center (Q&A #8)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-04

Scoping batch answered (Q&A #1–#4): the edges take the mouse while the Format tab is selected only;
with exact pixels the drag changes the pixels; corners, a live readout and Ctrl for a symmetric drag
on top of the edges; a straightforward subject, one direct exploration pass. The pass located the canvas
rectangle (`GridPreview.CanvasBounds`), the Custom route (`MainForm.ApplyCustom`, `CustomFormat`'s
bounds), the ratio held during gestures (`UpdateRatio`) and the global tab → preview flag
(`LayersActive`). Initial design written; four questions left open.

### Iteration 2 — 2026-11-04

The four questions answered (Q&A #5–#8): the opposite edge stays put during the drag, the canvas
fitted and centered again on release; without exact pixels, the fields take the native size's pixels;
green grips at the middle of each edge and at the corners while the Format tab is selected; the live
readout at the canvas's center, the ratio in decimal form. No question left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — hand checks |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When can the canvas's edges be dragged? | While the Format tab is selected only | 2026-11-04 |
| 2 | With *Exact pixels* on, what does the drag do? | Changes Width / Height in pixels, exact pixels kept | 2026-11-04 |
| 3 | Which extras on top of the edges? | Corners, a live readout, Ctrl for a symmetric drag (no snapping onto known formats) | 2026-11-04 |
| 4 | Straightforward or tricky subject? | Straightforward | 2026-11-04 |
| 5 | During the drag: the opposite edge fixed, or the canvas re-fitted at every move? | The opposite edge fixed, fitted and centered on release | 2026-11-04 |
| 6 | Ratio mode: the ratio's terms after a drag? | The native size's pixels (`1847 × 1200`) | 2026-11-04 |
| 7 | Drawn grips on the edges, or the cursor only? | Green grips at the edges' middles and the corners | 2026-11-04 |
| 8 | Where does the live readout stand? | At the canvas's center | 2026-11-04 |

---

*Last updated: 2026-11-04*
