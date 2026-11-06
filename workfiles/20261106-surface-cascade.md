# Surface Cascade

> Working document — several **work surfaces** (cells + layers, each its own layout, borders and grid
> background), played one after the other in the preview and the exported video, the soundtrack staying global.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Request of the user (2026-11-06), *Cascade avancée*:

- The **work surface** — the cells and the layers — can be **cascaded** with a following surface, and so on.
- The **global effects' music** (the Soundtrack) **stays global**: it plays across every surface.
- When a surface holds sounds (its videos), **only that surface's sounds** are heard while it plays.
- The passage from one surface to the next is set **by a duration**, or **Auto**: the length of the longest
  content of that surface.

Today the grid is one surface: `GridState` holds the cells, the layout, the format and one flat `GlobalState`
mixing what is truly shared (Soundtrack, Fade, Cascade) with what belongs to the grid drawn (Borders, Seams,
Layers, Grid background). The preview and the exports play one grid on one clock; the video length is
`Animation.VideoLength`.

Components touched (from the scout pass): `UI/GridHistory` (`GridState`, `GlobalState`), `UI/MainForm`
(`CaptureState` / `RestoreState`, the Global toolbar, the transport), `UI/GridPreview` (+ `.Layers`),
`UI/AnimationPlayer`, `UI/PreviewSound`, `Composition/Animation`, `Composition/VideoCascade`,
`Composition/CanvasSizer`, `Imaging/GridExport`, `Imaging/VideoEncoder` (`MixedSound`).

Related work: [20261106-video-range.md](20261106-video-range.md) redefines the video length too (a start and an
end on the timeline, past the loop) — see § Open Questions.

---

## Terms

| Term | Meaning |
|---|---|
| **Surface** (*surface*) | One work surface: its layout (separators included), its cells — images and their looks —, its layers, its Borders and its Grid background. The grid of today is one surface |
| **Active surface** (*surface active*) | The surface shown and edited in the preview, the one the effects toolbars and the Global toolbar's per-surface tabs act on |
| **Surface cascade** (*cascade de surfaces*) | The surfaces played one after the other, in their order, in the preview and the exported video |
| **Surface duration** (*durée de surface*) | How long a surface plays in the surface cascade: **Auto** — its own grid length — or a **fixed** duration |
| **Multi-surface layer** (*calque multi-surfaces*) | A layer flagged to show on several surfaces, not only on the one it was added to |

---

## What a Surface Holds

Agreed in the scoping batch (Q&A 1):

| Belongs to | Items |
|---|---|
| **Each surface** | Layout (separators included), cells (image + `ImageLook`), layers (+ the Layers on / off), Borders (+ on / off), Seams (+ on / off), Grid background (+ on / off, its background image included), cell Cascade (+ on / off, its pause), its duration |
| **Shared by every surface** | Output format (Custom included), Soundtrack, Fade, the Surfaces effect's on / off, the multi-surface layers |

- Seams per surface, like the Borders: they follow the layout (Q&A 10). The cell Cascade per surface: one surface
  may cascade its cells, the next play them all at once (Q&A 11).

- **Multi-surface layer**: a layer flagged *multi-surface* shows on **every** surface (Q&A 1, Q&A 7). It is **one
  shared layer**, held once, outside the surfaces: an edit — its place, its look, its content — changes it on every
  surface. Its content plays **continuously** across the whole sequence, on the global clock like the soundtrack —
  never started over at a surface change — and is **heard on every surface** (Q&A 12). It takes no part in a
  surface's cell cascade, and counts in no surface's Auto duration.
- The format is shared: one video has one canvas size (§ Export).

---

## Surface Duration

Agreed (Q&A 2, Q&A 3):

- **Per surface**: each surface has its own mode — **Auto** or a **fixed** duration.
- **Auto** = the surface's own grid length: `Animation.GridLength(images, cascade, layers)` over that surface
  only — its longest playing content, an Animations cycle, or **its whole cell cascade** when its Cascade is on —
  **without the soundtrack** nor the multi-surface layers.
- A surface of stills only has no grid length: its Auto duration is **3 s** (Q&A 9).
- **Fixed** shorter than the contents: they are cut at the surface's end. Longer: they keep looping until it.
- **Combined with the cell Cascade**: the Cascade plays inside each surface; the surface cascade plays the
  surfaces one after the other. Two separate settings.

---

## Activation and Preview

- **A Surfaces tab** of the Global toolbar (a global effect, RULES.md § Global Toolbar), with its activation
  checkbox (Q&A 5). **Off**: only the active surface plays, is heard and is exported; the other surfaces are kept as
  they are. **On**: the surfaces play one after the other, in their order.
- **The preview loops the active surface alone** while editing (Q&A 6). The transport's **timeline shows the whole
  sequence** while the Surfaces effect is on; **Play** or a click on the timeline plays the sequence, the surface
  shown following the clock. How the preview goes back to the active surface alone is part of the UI proposal.
- **A plain cut** from one surface to the next (Q&A 8); no crossfade in this workfile — the backlog's *Cascade
  transitions* row keeps it.

---

## Timing and Sound

- **One clock** stays (`AnimationPlayer`'s `PlaybackClock`, RULES.md § Transport). A **surface schedule** —
  one definition, in `Composition/`, beside `CascadeSchedule` — maps the grid time to *(surface index, time inside
  the surface)* from the surfaces' durations; the total is their sum.
- Every reader of a content's time reads the **time inside its surface**: the preview (`AnimationPlayer.Position`,
  `TurnOf`, `InTurn`), the export (`GridExport.Job.ContentTime`), the mix (`MixedSound`).
- **A surface starts over** when it begins: its contents from their starting points, its cell cascade from its
  first turn (RULES.md § Preview Playback).
- **Only the playing surface is heard** — its cells' and its layers' sounds; the others are silent. The
  multi-surface layers and the soundtrack are heard throughout.
- **The Soundtrack** plays across the whole sequence, its loop on the total length, never restarted at a surface
  change. The Fade applies to the whole video's start and end (between surfaces: open).
- **The video length** (`Animation.VideoLength`) becomes the sum of the surfaces' durations while the surface
  cascade plays.

---

## Export

- **One canvas** for every surface: the native size is the **largest** of the surfaces' (`CanvasSizer.Compute`
  per surface, the max of the long side, the cap and `EvenSize` applied once); the Custom format's exact pixels
  stay global.
- `GridExport.Job` captures **one snapshot per surface** (items, layout, borders, seams, cascade schedule,
  background, layers) plus the shared items (format, soundtrack, fade, caps, encoder); a frame at time *t* is drawn
  from the surface the schedule gives, by the same `Compositor.Draw`.
- The mix's voices of a surface are **windowed** on its span: heard from its start to its end only, their
  `TurnAt` / `Cycle` relative to the surface's start; the soundtrack voice unchanged.
- A PNG (Copy / Save) gives the **active surface** (to confirm, § Open Questions).

---

## Composed State and Undo

- `GridState` gains the **list of surfaces** — each a `Surface` record (cells, layout, borders, seams?, layers,
  grid background, duration) — and keeps the shared items (format, custom, soundtrack, fade, cascade, people).
  `GlobalState` is split accordingly (the scout's seam). `GridPreview`'s `EditState` is a precedent for swapping
  a surface's content in and out of the preview.
- Adding, removing, reordering surfaces and changing a duration are **steps** of the undo history
  (RULES.md § Undo History). Switching the active surface is not — like the selection — but a restore touching
  another surface switches to it.
- Not persisted, like the rest of the composition.

---

## UI

To be proposed visually (mockups, then a choice): where the surfaces are listed and switched, added, removed,
reordered; where a surface's duration is set; where the multi-surface flag of a layer is set.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Grid time → (surface index, time inside it), total = sum of durations, wrap at the total | `tests/ImageGridFusion.Tests/Composition/SurfaceScheduleTests.cs` | Create |
| Auto duration = the surface's grid length (cell cascade included, soundtrack excluded); stills-only fallback; fixed duration kept | `tests/ImageGridFusion.Tests/Composition/SurfaceScheduleTests.cs` | Create |
| Video length with surfaces = sum of durations; one surface = today's rule | `tests/ImageGridFusion.Tests/Composition/AnimationLengthTests.cs` | Create |
| Export canvas = the largest surface's native size, cap applied once | `tests/ImageGridFusion.Tests/Composition/CanvasSizerSurfacesTests.cs` | Create |
| A surface's sound voice is heard within its span only; the soundtrack across all | `tests/ImageGridFusion.Tests/Imaging/MixedSoundSurfaceTests.cs` | Create |
| `GridState` equality and `Images` cover every surface (the history keeps every surface's images alive) | `tests/ImageGridFusion.Tests/UI/GridStateSurfacesTests.cs` | Create |

---

## Open Questions

- [x] ~~**Activation**: a global effect with its checkbox, or always on from two surfaces?~~ → A **Surfaces** tab of
  the Global toolbar, with its checkbox (Q&A 5)
- [x] ~~**Preview**: the whole sequence, or the active surface looping alone?~~ → The active surface loops alone; the
  sequence is played from the timeline (Q&A 6)
- [ ] **Surfaces UI**: where are they listed, switched, added, removed, reordered? (mockups to come)
- [ ] **Duration UI**: where is a surface's Auto / fixed duration set?
- [x] ~~**Stills-only surface in Auto**: which default duration?~~ → 3 s (Q&A 9)
- [x] ~~**Multi-surface layer**: every surface or chosen ones? Shared or copies?~~ → Every surface, one shared layer
  (Q&A 7)
- [x] ~~**Multi-surface layer's playback and sound**: continuous or started over? Heard everywhere?~~ → Continuous
  on the global clock, heard on every surface (Q&A 12)
- [x] ~~**Seams**: per surface or shared?~~ → Per surface (Q&A 10)
- [x] ~~**Transition**: cut or crossfade?~~ → A plain cut; the crossfade stays in the backlog's *Cascade
  transitions* row (Q&A 8)
- [ ] **Fade**: only at the whole video's start and end, or also at each surface change?
- [x] ~~**Cell Cascade settings**: shared or per surface?~~ → Per surface, on / off and pause (Q&A 11)
- [ ] **PNG and Cell copy**: the active surface only?
- [ ] **Resets and Clear all**: does the Global Reset / *Clear all* remove every surface but one?
- [ ] **Video range** ([20261106-video-range.md](20261106-video-range.md)): its start and end apply on the whole
  sequence's timeline; which workfile is implemented first?

---

## Design Iterations

### Iteration 1 — 2026-11-06

Initial proposal from the request and the scoping batch: a surface holds its layout, cells, layers, Borders and
Grid background; the format, Soundtrack, Fade and Cascade settings are shared; a layer may be flagged
multi-surface; each surface has its own duration, Auto (its grid length, its cell cascade included) or fixed; the
cell Cascade plays inside each surface. One clock, a surface schedule mapping the grid time to a surface and its
local time; only the playing surface heard, the soundtrack across all; one export canvas, the largest surface's.
Scout pass (three read-only agents): the seams in `GridState` / `GlobalState`, `AnimationPlayer`, `GridExport.Job`
and `MixedSound`.

### Iteration 2 — 2026-11-06

Answers to Q&A 5–8: a **Surfaces** tab of the Global toolbar with its checkbox, off leaving the active surface
alone; the preview loops the active surface while editing, the timeline playing the whole sequence on demand; a
multi-surface layer shows on every surface, one shared layer; a plain cut between surfaces. New § Activation and
Preview; § What a Surface Holds updated.

### Iteration 3 — 2026-11-06

Answers to Q&A 9–12: a stills-only surface lasts 3 s in Auto; the Seams and the cell Cascade (on / off, pause)
move to each surface; a multi-surface layer plays continuously on the global clock and is heard on every surface,
outside every surface's cascade and Auto duration. § What a Surface Holds, § Surface Duration and § Timing and
Sound updated.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| GLOSSARY | | | Surface, Active surface, Surface cascade, Surface duration, Multi-surface layer — EN + FR |
| RULES.md | | | A § Surfaces section |
| SHORTCUTS | | | If surfaces get shortcuts |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does a surface own (the soundtrack staying global, the format shared)? | Cells + layers + layout + Borders + Grid background — and a layer can be flagged *multi-surface*, shown on several surfaces | 2026-11-06 |
| 2 | Where is a surface's duration (Auto / fixed) set? | Per surface | 2026-11-06 |
| 3 | How does it live with the current Cascade (cell after cell)? | Both combinable: the cell Cascade plays inside each surface; in Auto, a surface lasts its whole cell cascade | 2026-11-06 |
| 4 | Straightforward or tricky / long? | Straightforward — a single scout pass | 2026-11-06 |
| 5 | Activation: a Global toolbar tab with its checkbox, or always on from two surfaces? | A **Surfaces** tab of the Global toolbar with its checkbox; off, only the active surface plays and exports, the others kept | 2026-11-06 |
| 6 | Preview while editing: the whole sequence, or the active surface looping alone? | The active surface loops alone; the timeline shows the whole sequence, Play or a click on it plays the sequence, the surface shown following the clock | 2026-11-06 |
| 7 | Multi-surface layer: on every surface or chosen ones; one shared layer or copies? | On **every** surface, **one shared** layer: an edit changes it everywhere | 2026-11-06 |
| 8 | Transition between surfaces: cut or crossfade? | A plain **cut**; the crossfade stays in the backlog | 2026-11-06 |
| 9 | A stills-only surface in Auto: which default duration? | 3 s | 2026-11-06 |
| 10 | Seams: per surface or shared? | Per surface | 2026-11-06 |
| 11 | Cell Cascade settings: shared by every surface, or per surface? | Per surface | 2026-11-06 |
| 12 | A multi-surface layer's content: plays on continuously across surfaces, or starts over with each one? Heard on every surface? | Continuous across the sequence, heard on every surface | 2026-11-06 |
| 13 | Fade: only at the whole video's start and end, or also at each surface change? | | 2026-11-06 |
| 14 | PNG Copy / Save and Cell copy with the Surfaces effect on: the active surface? | | 2026-11-06 |
| 15 | Global Reset and Clear all: every surface removed but one? | | 2026-11-06 |
| 16 | Video range workfile: which one is implemented first? | | 2026-11-06 |

---

*Last updated: 2026-11-06*
