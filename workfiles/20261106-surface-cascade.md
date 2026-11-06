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
| **Each surface** | Layout (separators included), cells (image + `ImageLook`), layers (+ the Layers on / off), Borders (+ on / off), Grid background (+ on / off, its background image included) |
| **Shared by every surface** | Output format (Custom included), Soundtrack, Fade, Cascade (cell cascade) settings |
| **Open** | Seams — per surface like the Borders, or shared (§ Open Questions) |

- **Multi-surface layer**: a layer can be flagged to show on several surfaces (Q&A 1). Which surfaces, and
  whether it is one layer or a copy per surface, are open (§ Open Questions).
- The format is shared: one video has one canvas size (§ Export).

---

## Surface Duration

Agreed (Q&A 2, Q&A 3):

- **Per surface**: each surface has its own mode — **Auto** or a **fixed** duration.
- **Auto** = the surface's own grid length: `Animation.GridLength(images, cascade, layers)` over that surface
  only — its longest playing content, an Animations cycle, or **its whole cell cascade** when the Cascade is on —
  **without the soundtrack**.
- A surface of stills only has no grid length: its Auto duration falls back to a default (value open).
- **Fixed** shorter than the contents: they are cut at the surface's end. Longer: they keep looping until it.
- **Combined with the cell Cascade**: the Cascade plays inside each surface; the surface cascade plays the
  surfaces one after the other. Two separate settings.

---

## Timing and Sound

- **One clock** stays (`AnimationPlayer`'s `PlaybackClock`, RULES.md § Transport). A **surface schedule** —
  one definition, in `Composition/`, beside `CascadeSchedule` — maps the grid time to *(surface index, time inside
  the surface)* from the surfaces' durations; the total is their sum.
- Every reader of a content's time reads the **time inside its surface**: the preview (`AnimationPlayer.Position`,
  `TurnOf`, `InTurn`), the export (`GridExport.Job.ContentTime`), the mix (`MixedSound`).
- **A surface starts over** when it begins: its contents from their starting points, its cell cascade from its
  first turn (RULES.md § Preview Playback).
- **Only the playing surface is heard** — its cells' and its layers' sounds; the others are silent.
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

- [ ] **Activation**: is the surface cascade a global effect (a Global toolbar tab with its checkbox, off = only
  the active surface plays and exports), or always on as soon as there are two surfaces?
- [ ] **Preview**: while editing, does the preview play the whole sequence (the shown surface following the clock),
  or loop the active surface alone, the sequence played only on demand?
- [ ] **Surfaces UI**: where are they listed, switched, added, removed, reordered? (mockups to come)
- [ ] **Duration UI**: where is a surface's Auto / fixed duration set?
- [ ] **Stills-only surface in Auto**: which default duration (e.g. 3 s)?
- [ ] **Multi-surface layer**: shown on every surface, or on surfaces the user picks? One shared layer (an edit
  changes it everywhere) or a copy per surface?
- [ ] **Multi-surface layer's playback and sound**: does its content play on continuously across surfaces, or start
  over with each surface? Is it heard on every surface it shows on?
- [ ] **Seams**: per surface like the Borders, or shared?
- [ ] **Transition**: a plain cut between surfaces, or a crossfade? (the backlog's *Cascade transitions* row
  covers a crossfade between cell turns — a partial match)
- [ ] **Fade**: only at the whole video's start and end, or also at each surface change?
- [ ] **Cell Cascade settings**: shared by every surface (one on / off and pause), or per surface?
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

---

*Last updated: 2026-11-06*
