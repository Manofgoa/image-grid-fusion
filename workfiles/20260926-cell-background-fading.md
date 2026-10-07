# Cell Background Fading

> Working document — fade the bands of neighbour cells into each other along their shared edges.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every cell paints its **Background** effect behind its image (`ImageLook.Background`,
`Composition/BackgroundEffect.cs`, drawn by `Compositor.DrawBackground`): in the **Color** mode, a
flat fill — the automatic band color or a chosen color, at an opacity — over the whole cell; off, no
fill, the cell transparent. Two neighbour cells with different fills meet on a hard line.

This work adds a **global effect, Seams** (*Jointures*): a transition strip along every edge shared
by two cells, where each cell's flat fill blends into its neighbour's. The rest of the cell keeps its
own fill.

Scope agreed with the user (scoping batch, 2026-09-26, revised 2026-10-07):

- The **background** is the Background effect's fill (the bands and the image's transparent pixels),
  not the image itself — images are never faded into each other.
- The fade is a **strip along the shared edges**, not a gradient across the whole cell or grid.
- It is a **global effect** (RULES.md § Global Effects), not a cell effect: one setting for every
  seam.
- A first stage for **solid fills**: a **linear gradient** between two flat fills, alpha taken into
  account. A cell **extending its edges** (Background modes Corner pixel, Miter, Background corners)
  keeps its seams sharp; a fade of the extension is later work, out of this scope.

---

## Current State (codebase, 2026-10-07)

| Fact | Where |
|---|---|
| The Background effect holds the fill: automatic band color or chosen color, an opacity, a fill mode; on by default, off draws nothing | `Composition/BackgroundEffect.cs`, RULES.md § The Background Exception |
| The automatic color depends on the part shown, so it changes with crop, zoom, focus — and over time with the Animations effect | `BandColor.For`, `Compositor.AutomaticBackground` |
| `DrawCell` draws the background (`DrawBackground`: flat fill, or the edges extended), then the image, then the blur | `Composition/Compositor.cs:142-244` |
| Cells come from the layout and its separators, inset by the Borders when they leave a gap | `Compositor.Cells`, `GridBorders.Inset` / `HasGap` |
| `Compositor.Draw` loops `DrawCell`, then draws the borders; the preview also redraws single cells (animation frames, live gestures) | `Compositor.cs:126-136`, `UI/GridPreview.cs:1587`, `:2913` |
| Global effects: the `GlobalEffect` tabs (Format, Soundtrack, Fade, Borders), not persisted, part of the undo step | `Composition/GlobalEffect.cs`, `UI/GridHistory.cs`, RULES.md § Global Effects / § Undo History |
| The still export is 32 bpp ARGB: a transparent cell stays transparent in a PNG | `Compositor.Render` |
| No test project exists | `CONTRIBUTING.md` |

---

## Fade Geometry

- For each edge a cell shares with a neighbour, a **strip** inside the cell, along that edge.
- Its depth is **resolution-independent**: a share of the **smallest cell's** smaller dimension,
  set by the user (§ Global Effect), so the preview and an export at another size look the same.
- Neighbours are found from the **cells' rectangles** (layouts, separators), never from a layout id.
- Each cell draws **its own half** of the transition, from its own fill to the **seam color** — the
  **50 / 50 mix** of the two fills — and meets the neighbour's half on the seam without a step. This
  also keeps the preview's per-cell redraw exact.
- A seam fades **even when the neighbour shows no band there** (its image covers that side): the
  gradient goes toward its fill, computed from the sides of its image, so the transition stays
  consistent with what it shows.
- The outer border of the grid has no neighbour: no fade there.
- Where an edge is shared with several neighbours (a tall cell next to two stacked cells), the strip
  is split into one segment per neighbour, and **everything stays continuous**: where two segments
  meet, the seam color is interpolated between theirs over the strip depth, so no step shows along
  the edge.
- In a **corner** where two strips of a cell cross (e.g. the centre of a 2×2 grid), the two
  gradients are **mixed**, and the corner point reaches the mix of every cell meeting there, the same
  value from each cell — no step across either seam.

## Which Seams Fade

| Seam between | Fades |
|---|---|
| Two cells whose Background is in the **Color** mode | ✅ |
| A cell whose Background is **off**, and a Color-mode or off cell | ✅ — the off cell counts as a **transparent** fill |
| A cell whose Background **extends its edges** (Corner pixel, Miter, Background corners), and any cell | ❌ — the seam stays sharp on both sides |

## Colors and Alpha

- Colors interpolated **with their alpha** (premultiplied): a fill at a low opacity, or an off
  Background (transparent), fades the strip's **opacity** toward the seam as well as its color, and
  never drags a dark fringe into the gradient.
- The fills blended are the fills **as shown**: the Background's color at its opacity, the black &
  white effect applied (`BackgroundEffect.Fill`, `Compositor.Gray`).
- Transparent pixels of the image already show the fill: they show the gradient where it lies.
- The image itself is never faded: its edges stay opaque.

## Rendering

- Drawn in `Compositor.DrawCell`, as part of the cell's flat fill (`DrawBackground`), so the
  preview, the exports and video playback all show it; the image and the blur come over it as today.
- `DrawCell` receives, per shared edge, the neighbour's fill(s) at the frame's time;
  `Compositor.Draw` and the preview's single-cell redraws compute them from the cells.
- A change of a cell's fill — Background options, crop, zoom, focus, image replaced, black & white,
  the Animations motion — also redraws its neighbours' strips in the preview.
- With the **Borders leaving a gap** (every style but Corners), the cells no longer touch: Seams
  does not apply (§ Global Effect).

## Global Effect

A new **global effect, Seams** (*Jointures*), following RULES.md § Global Effects and § Global
Toolbar:

- A tab **after Borders** in the Global toolbar (`GlobalEffect.Seams`), with its activation checkbox.
- Its options: a **Depth** slider, in % of the smallest cell's smaller dimension, **10 %** by default,
  then its own **Reset**. Acting on the slider turns the effect on.
- **Off at start-up**, not persisted; the global *Reset* buttons and *Clear all* bring back its
  initial state (off, 10 %); untouched by the cell *Reset* buttons, kept on a swap or a layout
  change.
- Part of the **undo step** (`GridState`, `MainForm.CaptureState` / `RestoreState`).
- **Does not apply while the Borders leave a gap**: its checkbox and options disabled, with a
  tooltip saying why, its settings kept — like the Twitter corners outside the Twitter format.
- Locked while exporting, like every global effect; the export keeps the settings it started with.
- Its name avoids *Fade* (the sound global effect) and *Blend* (a Background option); added to the
  glossary (en / fr).

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
  *(revised 2026-10-07, see Iteration 5)*
- [x] ~~On or off at startup, remembered across launches?~~ → Remembered (on / off and depth); off at
  the very first launch *(revised 2026-10-07, see Iteration 5)*
- [x] ~~No test project: create one, or ship without unit tests?~~ → No unit tests
- [x] ~~*(2026-10-07, Iteration 4)* A seam where a cell extends its edges?~~ → No fade: the seam
  stays sharp; the extension gets its own fade later
- [x] ~~*(Iteration 4)* A neighbour whose Background is off or at a low opacity?~~ → Fade toward its
  fill, transparent included (premultiplied)
- [x] ~~*(Iteration 4)* The Borders' gap?~~ → Seams does not apply while a gap shows: disabled with
  a tooltip, settings kept
- [x] ~~*(Iteration 4)* Make the setting a global effect?~~ → Yes: global effect *Seams*, a Global
  toolbar tab after Borders, off at start-up, not persisted

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

### Iteration 4 — 2026-10-07

The user asked to check the impact of the work done since this design (session *Background edge
extension*, ~1 000 commits). Findings:

- The cell's background is now the **Background** cell effect (`ImageLook.Background`,
  `Composition/BackgroundEffect.cs`): automatic or chosen color, an **opacity**, on by default, off
  leaving the cell transparent; drawn by `Compositor.DrawBackground` before the image.
- It has **extending modes** (`workfiles/20261007-background-edge-extension.md`): the image's edges
  stretched over the bands — the non-solid background "if fixed color" pointed at. The fade of this
  design is the solid-color case.
- "Alpha" is now concrete: fills have an opacity, and an off Background is transparent; the PNG
  export is 32 bpp ARGB.
- A grid-level setting is now a **global effect** (RULES.md § Global Effects): Global toolbar tab,
  not persisted, global Resets and *Clear all*, undo step. Q13's bottom bar and remembered setting
  contradict it. *Fade* (sound) and *Blend* (Background) are taken names.
- The **Borders** global effect can leave a **gap** between the cells (`GridBorders.Inset`).
- Separators resize cells; neighbours must be found from the cells' rectangles. The Animations
  effect can change the automatic color over time, so a cell's seams follow its neighbours' frames
  (the preview's per-cell redraws, `GridPreview`, must redraw neighbours).
- Still no test project.

Four questions reopened (Open Questions, Q14–Q17); the design sections are updated once answered.

### Iteration 5 — 2026-10-07

Answers to Q14–Q17. The design sections are rewritten on the current codebase: the fill is the
Background effect's (Color mode); a seam touching an extending Background stays sharp; an off
Background fades as a transparent fill; the setting becomes the global effect **Seams** — a Global
toolbar tab after Borders, its Depth slider (10 %) in its options, off at start-up, not persisted,
in the undo step — replacing Q13's bottom bar and remembered setting; it does not apply while the
Borders leave a gap. Documentation now means README and GLOSSARY in English and French.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README (en / fr) | | | |
| GLOSSARY (en / fr) | | | |

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
| 14 | Seam where a cell extends its edges (Background extending modes)? | No fade — the extension gets its own fade later | 2026-10-07 |
| 15 | Neighbour whose Background is off or at a low opacity? | Fade toward its fill, transparent included | 2026-10-07 |
| 16 | Borders' gap between the cells? | Seams does not apply with a gap: disabled, tooltip, settings kept | 2026-10-07 |
| 17 | Make the setting a global effect (RULES.md § Global Effects)? | Yes — global effect *Seams*, after Borders | 2026-10-07 |

---

*Last updated: 2026-10-07*
