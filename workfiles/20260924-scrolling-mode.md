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
| `Composition/GridLayout.cs` | `ClockwiseLoop()`: the loop of cells, from the layout's geometry |
| `Composition/Carousel.cs` | Pure rules: step timing, arrangement at a step, cell of an image, one canvas for every step |
| `Imaging/CarouselExport.cs` | Writes the carousel's MP4 with the existing `VideoEncoder` |
| `UI/GridPreview.cs` | Plays the carousel live (`PlaysCarousel`), pauses it under the pointer |
| `UI/MainForm.cs` | `Carrousel` check box in the top bar; `Save…` routes to the carousel export |

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
  single row go left → right and wrap from the last one back to the first. It is computed from the
  layout's geometry, not listed per layout: each cell is placed where it first meets the border,
  walking the border clockwise from the top-left corner. Default layouts
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

- **Toggle**: a **`Carrousel`** check box at the **right end of the top bar** (the bar holding the
  Crop slider, added in parallel to this design). It is unchecked and disabled below two images, and
  stays usable during an export, which only affects the preview.
- Checking it starts the rotation from the current arrangement; unchecking it stops it and brings
  the images back to their own cells (the arrangement the user built).
- Any change of images, layout or mirror, and hiding the window in the tray, sends the carousel
  back to the user's own arrangement; the next step comes a full second later. Nothing plays behind
  a hidden window.
- Animated contents keep playing while they move. Text pages keep the shape of their own cell and
  are fitted with the usual rules in the others. The carousel has its own one-second clock, not the
  animation clock, so PDF pages and text views do not change in step with it.
- **Pause on hover**: as soon as the mouse (or a drag from Explorer, or a gesture holding the mouse)
  is over the preview control — drop zone included — the rotation pauses and the grid
  shows the user's own arrangement again; every interaction (hover outline, **×**, select, swap,
  replace by drop, sliders, drop zone) works as usual on it. When the mouse leaves the preview,
  the rotation restarts from that arrangement, taking any edit into account, after a full second on
  it. The pointer is also checked on every tick, since a drop from Explorer may bring no mouse message.
- An edit that leaves a single image unchecks and disables `Carrousel`.
- `Copy` / `Ctrl+C` exports the **carousel's video** while `Carrousel` is checked, like `Save…`,
  and puts the MP4 file on the clipboard the way the animated-content copy does (written to
  `%TEMP%\ImageGridFusion`), whatever step is on screen and whether *Force as image* is checked.
  Unchecked, it behaves as without the mode.

---

## Video export

Agreed: the rotation can be exported as a video file.

- **Encoder**: the app's own `VideoEncoder` (Media Foundation Sink Writer, H.264, 30 fps), used by
  the animated-content export. No third-party library, in line with the README's *Tech* section.
- **Frames**: each arrangement is drawn once with `Compositor` (images reordered so image *k* lands
  in its current cell), then written as the 30 frames of its second. Progress and Cancel in the
  status line, the grid locked meanwhile, as for any video export.
- **Frame size**: `CanvasSizer` depends on which image lands in which cell, so it differs from one
  arrangement to the next, while a video needs one frame size: the video uses the **largest canvas
  over the N arrangements**, so no image is downscaled at any step (still clamped to 4096 px).
  The live preview is not affected: it keeps fitting the grid to the window.
- **H.264 constraints**: even dimensions (rounded down, as the animated export does); the canvas'
  4096 px maximum stays within what the Windows H.264 encoder accepts.
- **Animated contents** (video, GIF, PDF of several pages, long text):
  - *Force as image* unchecked: they **play** while they move, from their start, as in the
    animated-content export — the shorter ones starting over, the sound of the animated export
    (image 1's, else the first video with sound) carried by the video. The canvas and the band
    colors come from their first frames.
  - *Force as image* checked: each shows its **current page, frozen** — the page its slider
    selects, rendered at full size. The video is **silent**.
- **Length**: a whole number of **full loops** — N seconds each for N images, starting from the
  user's own arrangement, so replayed in a loop it has no visible seam. One loop when nothing plays;
  when contents play, as many loops as needed for the longest content to end (e.g. 3 images and a
  7.5 s video: 3 loops, 9 s).
- **Trigger**: while `Carrousel` is checked, **`Save…` / `Ctrl+S` writes the carousel's MP4**,
  whatever the contents and whether *Force as image* is checked (the save dialog offers `.mp4`).
  Unchecked, `Save…` behaves as without the mode (PNG, or the animated-content MP4).

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
- [x] ~~`Copy` while the mode is on: copies the still image of the starting arrangement, the arrangement currently shown, or is disabled?~~ → Still image of the user's own arrangement *(revised 2026-09-24, see Iteration 6)*
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

### Iteration 5 — 2026-09-24 — 🧭 Implementation choices

No project rule was broken. The code moved a lot in parallel between design and go (top bar,
animated content, MP4 export, *Force as image*); the choices below adapt the frozen design to it.

- **Top bar**: the design planned a new top toolbar; one now exists (Crop slider), so the check box
  went to its right end instead of a second bar.
- **Encoder**: the existing `VideoEncoder` (Sink Writer, 30 fps) instead of `MediaComposition`, so
  both video exports share one encoder, frame rate and even-size rule.
- **Frozen contents**: "current page / frame" became the page each slider selects, rendered at full
  size — the rule *Force as image* adopted meanwhile. Consequence: the carousel video is silent.
- **Force as image × Carrousel**: `Save…` writes the carousel video even with *Force as image*
  checked (the contents are frozen anyway); *Force as image* still governs `Copy` and the preview.
- **Copy**: "unchanged" kept literally — it follows the current rules, so it gives the animated MP4
  when the grid holds animated content, never the carousel.
- **Loop order**: computed from each layout's geometry (border walk) rather than a hard-coded
  table, so mirrored layouts follow on their own; it matches the table for every default layout.
- **Pause**: over the whole preview control (drop zone included), during Explorer drags and mouse
  captures, re-checked on every tick; a resume holds the user's arrangement a full second.
- **Reset**: any change of images, layout, mirror or window visibility restarts from the user's
  own arrangement; the check box stays usable during an export.
- **Separate clock**: the carousel's one-second timer is not the animation clock, so PDF pages and
  text views are not in step with it.
- **Label**: `Carrousel` as the user wrote it, while the rest of the UI is in English.
- **Parallel sessions**: another session edited `GridExport.cs`, `MainForm.cs`, `AnimationPlayer.cs`
  and `GridPreview.cs` during the run. The export went into a new file, and each commit was checked
  to hold only this run's lines.
- **Not verified by hand**: built with 0 warning and 0 error (into a scratch folder: another
  session's running instance held the exe), not run.

### Iteration 6 — 2026-09-24 — ⚙️ Post-implementation — Copy exports the carousel video

Requested by the user while testing: in carousel mode, `Copy` must export a video. It now writes
the carousel's MP4, like `Save…`, and puts the file on the clipboard as the animated-content copy
does. This reverses the Q&A 10 decision (Copy kept the still of the user's own arrangement).

### Iteration 7 — 2026-09-24 — ⚙️ Post-implementation — Contents play in the carousel video

Requested by the user while testing: with a video in the grid and *Force as image* unchecked, the
video must play in the carousel video, and every video must reach its end, even if that takes
several carousel loops. The carousel export now plays animated contents like the animated export
(from their start, shorter ones starting over, with its sound), and its length is the smallest
whole number of carousel loops covering the longest content. *Force as image* keeps the frozen,
silent behaviour. This reverses the frozen-contents point of the design (Iteration 3) and the
"silent" consequence of Iteration 5. Carrying the sound is taken as part of "the video plays".

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | Iterations 5, 6 | 2026-09-24 | 4 commits: carousel logic, carousel export, live preview, check box and Save; then Copy exports the carousel video |
| Unit tests | — | 2026-09-24 | Not applicable: no test project, by decision (Q&A 12) |
| README | — | 2026-09-24 | Not done: the go covered the code only |

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
