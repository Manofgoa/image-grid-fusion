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
  two colors, alpha taken into account. It applies to every band as it exists now; a future
  non-solid background (e.g. a blurred extension of the image) would get its own fade later, out of
  this scope.

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
- Its depth is **resolution-independent**: a share of the **smallest cell's** smaller dimension,
  set by the user (see Setting), so the preview and an export at another size look the same.
- Each cell draws **its own half** of the transition, from its own color to the **seam color** —
  the **50 / 50 mix** of the two band colors — and meets the neighbour's half on the seam without a
  step. This also keeps the preview's per-cell redraw exact.
- A seam fades **even when the neighbour shows no band there** (its image covers that side): the
  gradient goes toward its band color, which is computed from the sides of its image, so the
  transition stays consistent with what it shows.
- The outer border of the grid has no neighbour: no fade there.
- Where an edge is shared with several neighbours (a tall cell next to two stacked cells), the strip
  is split into one segment per neighbour, and **everything stays continuous**: where two segments
  meet, the seam color is interpolated between theirs over the strip depth, so no step shows along
  the edge.
- In a **corner** where two strips of a cell cross (e.g. the centre of a 2×2 grid), the two
  gradients are **mixed**, and the corner point reaches the mix of every cell meeting there, the same
  value from each cell — no step across either seam.

## Colors and Alpha

- Colors interpolated **with their alpha** (premultiplied), so a transparent color never drags a
  dark fringe into the gradient. Band colors are opaque today; the rule keeps the gradient correct
  if one ever is not.
- The colors blended are the band colors **as shown**, the black & white effect applied.
- Transparent pixels of the image already show the band fill: they show the gradient where it lies.
- The image itself is never faded: its edges stay opaque.

## Rendering

- Drawn in `Compositor.DrawCell` (or right after the band fill), so the preview, the exports and
  video playback all show it, like every other background drawing.
- `DrawCell` receives, per shared edge, the neighbour's band color(s); `Compositor.Draw` and the
  preview's single-cell redraw compute them from the layout.
- A change of a cell's band color (zoom, focus, image replaced, black & white) also redraws its
  neighbours' strips in the preview.

## Setting

- One grid-level setting, in the **bottom bar**, next to the export buttons and *Force as image*,
  always visible:
  - an on / off **checkbox** (*Fade seams*);
  - a **depth slider**, in % of the smallest cell's smaller dimension, **10 %** by default.
- **Remembered across launches** (on / off and depth); **off at the very first launch**.
- Not an effect: no tab, not tied to a cell, unaffected by the effects' *Reset*.
- Moving the slider while the fade is off turns it on, like acting on an effect's option.

---

## Test Impact

No test project exists; the user chose to ship this work **without unit tests**: it is verified
manually in the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests, by decision) | — | — |

---

## Open Questions

- [x] ~~"If fixed color": what is the other case?~~ → A first stage: the gradient applies to every
  band as it is today; a future non-solid background gets its own fade later
- [x] ~~"Take alpha into account": what does it cover?~~ → Interpolation with alpha (premultiplied),
  and the gradient showing through the image's transparent pixels; the image's edges are not faded
- [x] ~~Seam color?~~ → The 50 / 50 mix: each cell fades from its color to it
- [x] ~~A neighbour whose image covers its side of the seam?~~ → Fade toward its band color anyway
- [x] ~~Corners and edges shared with several neighbours?~~ → Everything continuous: seam color
  interpolated where segments meet, corners mixing both strips
- [x] ~~Strip depth: fixed or adjustable, default?~~ → Adjustable slider, % of the smallest cell,
  10 % by default
- [x] ~~Where does the setting live?~~ → Bottom bar, next to the export buttons and *Force as image*
- [x] ~~On or off at startup, remembered across launches?~~ → Remembered (on / off and depth); off at
  the very first launch
- [x] ~~No test project: create one, or ship without unit tests?~~ → No unit tests

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

### Iteration 2 — 2026-09-26

Answers to Q5–Q8: the gradient is a first stage covering every band as it is today; alpha means
premultiplied interpolation plus the gradient showing through transparent pixels (the image's edges
are not faded); the seam color is the 50 / 50 mix; a seam fades even when the neighbour's image
covers its side. The corner question is reworded to cover the multi-neighbour case too, and
placement and startup state are split.

### Iteration 3 — 2026-09-26

The Q9–Q12 batch was dismissed; the agent's recommendations were re-asked in one question (Q13) and
accepted: continuous corners and multi-neighbour seams, an adjustable depth slider (10 % of the
smallest cell by default), the setting in the bottom bar, remembered across launches and off at the
first launch, no unit tests. The design is complete.

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
| 5 | "If fixed color": what is the other case? | A first stage; a future non-solid background gets its own fade later | 2026-09-26 |
| 6 | "Take alpha into account": what does it cover? | Premultiplied interpolation + gradient through transparent pixels | 2026-09-26 |
| 7 | Seam color rule? | The 50 / 50 mix | 2026-09-26 |
| 8 | Neighbour whose image covers its side of the seam? | Fade toward its band color anyway | 2026-09-26 |
| 9 | Corners and edges shared with several neighbours? | Dismissed — re-asked as Q13 | 2026-09-26 |
| 10 | Strip depth: fixed or adjustable, default? | Dismissed — re-asked as Q13 | 2026-09-26 |
| 11 | Where does the setting live? | Dismissed — re-asked as Q13 | 2026-09-26 |
| 12 | On or off at startup, remembered across launches? | Dismissed — re-asked as Q13 | 2026-09-26 |
| 13 | Accept the five recommendations (continuous corners, 10 % slider, bottom bar, remembered, no unit tests)? | Yes, all five | 2026-09-26 |

---

*Last updated: 2026-09-26*
