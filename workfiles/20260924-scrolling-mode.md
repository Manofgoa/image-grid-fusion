# Scrolling Mode

> Working document — a mode where the images rotate through the cells of the layout,
> played live in the preview and exported as a video.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A new **rotation mode** (topic name: *scrolling mode*). While it is on, the images move
through the cells of the active layout like a carousel: at every step, each image jumps to
the next cell along a loop, and the image in the last cell comes back to the first one.
The mode is played **live** in the grid preview and can be **exported as a video file**.

Relevant components:

| Component | Role in this feature |
|---|---|
| `UI/GridPreview.cs` | Draws the grid; would play the live rotation |
| `UI/MainForm.cs` | Buttons, shortcuts, status line; hosts the mode toggle and the video export |
| `UI/LayoutStrip.cs` | Left strip with the layout thumbnails and the mirror toggle |
| `Composition/Compositor.cs` | Renders image *i* into cell *i*; renders each video frame |
| `Composition/CanvasSizer.cs` | Picks the canvas width from which image lands in which cell |
| `Imaging/VideoFrames.cs` | Already uses `Windows.Media.Editing` (`MediaComposition`) to decode videos |

---

## Rotation

Agreed:

- **Carousel of cells**: images keep their order and shift one cell per step along a loop;
  the image in the last cell of the loop goes back to the first cell.
- **Hard cut**: no animated slide. The arrangement changes instantly, then holds for **1 second**
  before the next step.
- With **N** images there are exactly **N** distinct arrangements; after N steps the grid is back
  to its starting arrangement.
- Each image is fitted to the cell it currently occupies with the usual fitting rules
  (cells of a layout may have different ratios, so an image's crop / bands change as it moves).
- **1 image**: nothing to rotate — the mode is unavailable.
- **Loop order: clockwise** around the grid, geometrically — a true carousel. Cells lined up in a
  single row go left → right and wrap from the last one back to the first. Default layouts
  (cell numbers as in the README's *Layouts*):

  | Layout | Loop |
  |---|---|
  | 2 images (any) | 1 → 2 → 1 |
  | 3 · Big left, Three columns, Featured | 1 → 2 → 3 → 1 |
  | 3 · Big top | 1 → 3 → 2 → 1 |
  | 4 · Grid | 1 → 2 → 4 → 3 → 1 |
  | 4 · Four columns, Featured, Big left | 1 → 2 → 3 → 4 → 1 |
  | 4 · Big top | 1 → 4 → 3 → 2 → 1 |

  A **mirrored** layout follows the clockwise path of the mirrored grid, which reverses the cell
  order of the loop (e.g. *Big left* mirrored: 1 → 3 → 2 → 1).

---

## Live preview

Agreed: the rotation plays in the grid preview itself.

- **Toggle**: a **`Carrousel`** check box in a **new toolbar at the top** of the window. The app has
  no top toolbar today (the controls live in the left layout strip and the bottom bar), so this
  feature introduces it. The check box is disabled with a single image.
- Checking it starts the rotation from the current arrangement; unchecking it stops it and brings
  the images back to their own cells (the arrangement the user built).

- **Pause on hover**: as soon as the mouse is over the preview, the rotation pauses and the grid
  shows the user's own arrangement again; every interaction (hover outline, **×**, select, swap,
  replace by drop, sliders, drop zone) works as usual on it. When the mouse leaves the preview,
  the rotation restarts from that arrangement, taking any edit into account.
- An edit that leaves a single image unchecks and disables `Carrousel`.
- `Copy` / `Ctrl+C` is unchanged: it copies the still image of the user's own arrangement,
  whatever step is on screen.

---

## Video export

Agreed: the rotation can be exported as a video file.

- **Encoder**: `Windows.Media.Editing.MediaComposition`, already used by `VideoFrames` to decode
  videos — one clip per arrangement, each lasting 1 s, rendered to an **MP4 (H.264)** file with
  `RenderToFileAsync`. No third-party library, in line with the README's *Tech* section.
- **Frames**: each arrangement is rendered once with `Compositor` (images reordered so image *k*
  lands in its current cell), then handed to the composition.
- **Frame size**: `CanvasSizer` depends on which image lands in which cell, so it differs from one
  arrangement to the next, while a video needs one frame size: the video uses the **largest canvas
  over the N arrangements**, so no image is downscaled at any step (still clamped to 4096 px).
  The live preview is not affected: it keeps fitting the grid to the window.
- **H.264 constraints**: even dimensions (the 1200:628 height may come out odd); the canvas'
  4096 px maximum stays within what the Windows H.264 encoder accepts.
- Cells holding a video, a PDF or a text show their **current page / frame, frozen** — the source
  video does not play inside the exported video.
- **Length**: exactly **one full loop** — N seconds for N images, starting from the user's own
  arrangement; replayed in a loop, it has no visible seam.
- **Trigger**: while `Carrousel` is checked, **`Save…` / `Ctrl+S` writes an MP4** instead of a PNG
  (the save dialog offers `.mp4`). Unchecked, `Save…` writes a PNG as today.

---

## Test Impact

The solution has no test project, and the user chose to keep it that way: no test project is
created for the rotation logic (loop order, arrangement at step *k*, fixed frame size). The
feature is checked by running the app. No test is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project, by decision) | — | — |

---

## Open Questions

- [x] ~~Loop order: reading order (cell 1 → 2 → 3 → 4 → 1), or a geometric clockwise path around the grid (e.g. 2×2 grid: 1 → 2 → 4 → 3 → 1)?~~ → Clockwise, geometric
- [x] ~~Video length: exactly one full loop (N seconds, loops seamlessly when replayed), or a user-chosen duration / number of loops?~~ → One full loop
- [x] ~~How is the mode turned on: a toggle below the mirror toggle in the layout strip, or a toggle next to Copy / Save?~~ → A `Carrousel` check box in a new top toolbar
- [x] ~~How is the video exported: `Save…` writes an MP4 instead of a PNG while the mode is on, or a separate `Save video…` button?~~ → `Save…` writes an MP4 while the mode is on
- [x] ~~Preview interactions while the rotation plays: rotation pauses while the mouse is over the preview, any edit stops the mode, or interactions are disabled while it plays?~~ → Pause on hover, back to the user's arrangement, everything stays editable
- [x] ~~`Copy` while the mode is on: copies the still image of the starting arrangement, the arrangement currently shown, or is disabled?~~ → Still image of the user's own arrangement
- [x] ~~Video frame size: the largest canvas over all N arrangements (no image downscaled at any step), or the canvas of the starting arrangement?~~ → Largest canvas over the N arrangements
- [x] ~~Tests: create a first test project to pin the rotation logic, or keep the solution test-free as so far?~~ → No test project

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial scoping with the user: carousel of cells, hard cut every 1 second, live preview in the
grid plus a video export; exploration expected to be straightforward (single scout pass).
Exploration found no test project, a PNG-only output path (`Compositor.Render`), and
`Windows.Media.Editing` already referenced — a candidate MP4 encoder with no new dependency.
Eight questions remain open (loop order, video length, UI entry points, interactions, Copy,
frame size, tests).

### Iteration 2 — 2026-09-24

First batch answered. Loop is clockwise and geometric (per-layout table, mirrored layouts reverse
it); the video is one full loop; `Save…` writes an MP4 while the mode is on. The user rejected both
proposed toggle placements: the mode is a `Carrousel` check box in a **new top toolbar**, which the
app does not have yet. Four questions remain (interactions, Copy, frame size, tests).

### Iteration 3 — 2026-09-24

Second batch answered, no open question left. The rotation pauses while the mouse is over the
preview, showing the user's own arrangement, fully editable, and restarts when it leaves. `Copy`
keeps copying the still of the user's arrangement. The video uses the largest canvas over the N
arrangements. No test project is created; the video export route (MediaComposition → MP4 H.264)
moves from *proposed* to agreed.

### Iteration 4 — 2026-09-24 — ✅ Implemented

The go was declined twice, then given for the code ("Go implémente"): code only, no README
update, no test by earlier decision. Branch Gate: stays on `main`, the standing choice for this
repository; other sessions work on `main` in parallel, so the run commits often and stages only
its own files.

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
| 1 | What does "rotation" mean for this mode? | Carousel of cells | 2026-09-24 |
| 2 | How do images move from one position to the next? | Hard cut, then a 1-second hold | 2026-09-24 |
| 3 | What is the expected deliverable? | Live preview + export | 2026-09-24 |
| 4 | Is the exploration expected to be straightforward or tricky / long? | Straightforward | 2026-09-24 |
| 5 | Loop order: reading order or geometric clockwise path? | Clockwise, geometric | 2026-09-24 |
| 6 | Video length: one full loop or user-chosen? | One full loop | 2026-09-24 |
| 7 | Where is the mode toggled? (below Mirror, or next to Copy / Save) | Neither — a `Carrousel` check box in a top toolbar |  2026-09-24 |
| 8 | How is the video exported? | `Save…` writes an MP4 while the mode is on | 2026-09-24 |
| 9 | Preview interactions while the rotation plays? | Pause on hover, everything stays editable | 2026-09-24 |
| 10 | What does Copy do while the mode is on? | Still image of the user's own arrangement | 2026-09-24 |
| 11 | Video frame size? | Largest canvas over the N arrangements | 2026-09-24 |
| 12 | Create a first test project for the rotation logic? | No | 2026-09-24 |

---

*Last updated: 2026-09-24*
