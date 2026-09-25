# Cell Background Fading

> Working document — fade the bands of neighbour cells into each other along their shared edges.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every cell fills the area its image does not cover — and the image's transparent pixels — with a
single **band color** (`BandColor.For`, painted by `Compositor.DrawCell` before the image). Two
neighbour cells with different band colors therefore meet on a hard line.

This work adds a **fade at the seams**: a transition strip along every edge shared by two cells,
where each cell's band color blends into its neighbour's. The rest of the cell keeps its own color.

Scope agreed with the user (scoping batch, 2026-09-26):

- The **background** is the area not covered by the image (the bands), not the image itself —
  images are never faded into each other.
- The fade is a **strip along the shared edges**, not a gradient across the whole cell or grid.
- It is a **grid-level setting**, not an effect: one setting for every seam, not a cell + image
  pair, so the effect rules of RULES.md do not apply to it.
- Band colors are a **fixed (solid) color** today: the fade starts as a **linear gradient** between
  two colors, alpha taken into account.

---

## Current State (codebase)

| Fact | Where |
|---|---|
| A cell's band color is computed from the sides of the part it shows: the image's uniform background, else the most frequent side color, else the dominant color. Always opaque | `Composition/BandColor.cs` |
| It depends on the part shown, so it changes with zoom and focus | `BandColor.For(part, size)` |
| `DrawCell` fills the whole cell with it, then draws the image clipped to the cell; the black & white effect grays it | `Composition/Compositor.cs:90-95` |
| Cells tile the canvas exactly, no gap; neighbours share their boundary pixel line | `GridLayout.Cells` |
| Each cell is drawn **independently**: `Compositor.Draw` loops `DrawCell`, and the preview redraws a single cell into its cache at every animation frame | `Compositor.cs:54-61`, `UI/GridPreview.cs:909-925` |
| Exports go through `Compositor.Draw`, into a 24 bpp bitmap (no alpha in the output) | `Imaging/GridExport.cs:133`, `Compositor.Render` |
| The ⚙ settings menu holds app settings only (Start with Windows, in the registry); there is no grid-level settings row | `UI/MainForm.cs:917` |
| No test project exists | repository root |

---

## Fade Geometry

- For each edge a cell shares with a neighbour, a **strip** inside the cell, along that edge.
- Its depth is **resolution-independent**: a share of the canvas (or of the smaller cell), so the
  preview and an export at another size look the same.
- Cells drawn one at a time (the preview's per-cell redraw) require that each cell draws **its own
  half** of the transition, from its own color to the **seam color**, and meets the neighbour's half
  on the seam without a step.
- The outer border of the grid has no neighbour: no fade there.
- Where an edge is shared with several neighbours (a tall cell next to two stacked cells), the strip
  is split into one segment per neighbour — see Open Questions.

## Colors and Alpha

- Colors interpolated **with their alpha** (premultiplied), so a transparent color never drags a
  dark fringe into the gradient. Band colors are opaque today; the rule keeps the gradient correct
  if one ever is not.
- The colors blended are the band colors **as shown**, the black & white effect applied.
- Transparent pixels of the image already show the band fill: they show the gradient where it lies.

## Rendering

- Drawn in `Compositor.DrawCell` (or right after the band fill), so the preview, the exports and
  video playback all show it, like every other background drawing.
- `DrawCell` receives, per shared edge, the neighbour's band color(s); `Compositor.Draw` and the
  preview's single-cell redraw compute them from the layout.
- A change of a cell's band color (zoom, focus, image replaced, black & white) also redraws its
  neighbours' strips in the preview.

## Setting

- One grid-level setting: fade on / off, and possibly its depth — placement, default and
  persistence in Open Questions.

---

## Test Impact

No test project exists — whether to create one is an Open Question.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(pending the test-project decision)* | | |

---

## Open Questions

- [ ] "If fixed color": what is the other case? Today bands are always one solid color — is it
  (a) a first stage, a future non-solid background (e.g. a blurred extension of the image) getting
  its own fade later, or (b) a fade only when the band color is the image's real uniform background,
  not a fallback color?
- [ ] "Take alpha into account": interpolation with alpha (premultiplied) and the gradient showing
  through the image's transparent pixels — or something else (e.g. the image's own edges fading
  into the neighbour)?
- [ ] Seam color: each cell fades from its color to the **50 / 50 mix** at the seam (continuous,
  symmetric, works with per-cell redraw) — or another rule?
- [ ] A neighbour whose image covers its side of the seam shows no band there: fade toward its band
  color anyway, or no fade on that seam?
- [ ] Edge shared with several neighbours: one segment per neighbour, with a step between segments
  along the edge — or blend the segments into each other too?
- [ ] Strip depth: fixed, or adjustable (slider)? Default depth?
- [ ] Where does the setting live (layout strip, bottom bar next to the export buttons, ⚙ menu), and
  is it on by default?
- [ ] Is the setting remembered across launches?
- [ ] No test project: create one for this work, or ship without unit tests?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the user's request ("fading between the cell backgrounds; with a fixed color,
start with a gradient, alpha taken into account") and the scoping batch: bands only, a strip along
shared edges, one grid-level setting. Scout pass on the codebase: band color per cell, cells drawn
independently (so each cell draws its half of the fade toward a seam color), no grid-level settings
row, no test project.

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
| 1 | What is a cell's "background"? | The area not covered by the image (the bands) | 2026-09-26 |
| 2 | Where does the fade happen? | A strip along the edges shared by neighbour cells | 2026-09-26 |
| 3 | At what level is it set? | One grid-level setting | 2026-09-26 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-09-26 |
| 5 | "If fixed color": what is the other case? | | 2026-09-26 |
| 6 | "Take alpha into account": what does it cover? | | 2026-09-26 |
| 7 | Seam color rule? | | 2026-09-26 |
| 8 | Neighbour whose image covers its side of the seam? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
