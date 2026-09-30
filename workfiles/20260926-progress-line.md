# Progress Line

> Working document — a fluorescent green line along an edge of every cell whose content plays,
> showing how far it has played, advancing continuously, always shown, in the preview only.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every cell whose image **plays** — a video, an animated GIF, a PDF of several pages, a text longer
than its cell (`SourceImage.Plays`) — gets a **progress line**: a helper indicator (RULES.md § On-Cell
Helper Indicators) showing where the content stands in its loop. It advances **continuously**, from
the playback clock, not step by step with the frames or the pages: a text moving every second gets a
line that glides, not one that jumps every second. It is shown **all the time** — the cell selected
or not, hovered or not, whatever effect tab is selected, while an export runs — and it never reaches
the exports.

| Component | Role |
|---|---|
| `UI/AnimationPlayer.cs` | Owns the one playback clock (`Stopwatch`) and each image's playback; exposes the progress |
| `Composition/Animation.cs` | Pure timing rules shared by preview and export; gets the progress fraction helper |
| `UI/GridPreview.cs` | Paints the line in `OnPaint`, repaints it from a ~60 fps timer |
| `README.md`, `GLOSSARY.md` | Documentation |

Scoping answers (Q&A #1–4): the edge was chosen on the edge map below (Q&A #5: the bottom edge), a
frozen content shows **no line**, the line is paced by the **clock and a ~60 fps repaint**, the
exploration was a single scout pass.

---

## What the Line Shows

- **Which cells**: every cell whose image `Plays` (animated and not frozen), while the player runs —
  the window shown. No line on a still, on a frozen content (its playback stands on `PausedAt`), or
  while the window is hidden (nothing plays then: `AnimationPlayer.Stop()`).
- **Value**: the position within the content's **own loop**, as a fraction 0..1 —
  `Animation.LoopTime(position, LoopDuration) / LoopDuration`, `position` being what
  `AnimationPlayer.Position` gives today (`UI/AnimationPlayer.cs:183`): the shared `Stopwatch` minus
  the playback's offset. With a starting point set by the Frames effect, the first lap starts at that
  point's fraction and the next laps from 0, exactly as the content plays today
  (`Animation.LoopTime`; the export does the same, `Imaging/GridExport.cs:145`). The line resets to
  the cell's edge at each loop.
- **Continuous**: sampled from the clock at every paint, so its motion never depends on the content's
  step cadence — a video frame decoded at each 33 ms poll, a PDF page or a text view every second
  (`Animation.StepDuration`). Sub-millisecond resolution, nothing accumulated, no drift.
- **Always shown**: on every playing cell, selected or not, hovered or not, whatever tab is selected;
  while an export runs too (the animation keeps playing, `GridPreview.Locked`). The cell being
  dragged keeps it, dimmed with the cell: the line is painted under the drag dim and the drop-target
  highlight.
- **Preview only**: painted in `GridPreview.OnPaint`, never in `Compositor` (RULES.md).
- **Not the soundtrack**: a grid of stills with a soundtrack shows no line — nothing moves in its
  cells; the soundtrack's own progress is out of scope (Q&A #7).
- **Frames speed** (`workfiles/20260926-frames-speed.md`, designed, not shipped): its formula lands in
  the same `Position` / `LoopTime` computation, so the line follows it with no change of its own.

---

## Placement

> The edge was chosen on the edge map below, once the exploration had drawn it (Q&A #1, #5): the
> bottom edge, just inside the selection outline.

### Edge map

What the preview already draws at each edge of a cell (`UI/GridPreview.cs`), and when:

| Edge | Occupant | When |
|---|---|---|
| All four | Selection outline: 3 px inset band, system highlight colour (`OnPaint`, l. 438–442) | Selected cell |
| All four | Hover outline: 1 px inset translucent white band (`PaintHoverOutline`, l. 2088–2105) | Hovered cell |
| All four | Blur bars: fluorescent green 2 px over a 4 px halo, on the cell's edges by default (`PaintBlurBars`, l. 1764–1798) | Selected cell, Blur tab selected, blur on |
| All four | Pan guide: dashed green line 2 px inside the edge (`PaintPanGuides`) | The dragged image, held on a magnetic stop |
| Bottom | File name and folder icon, green, their line box ending 6 px above the edge (`SourceNamePath`, l. 1876–1910) | Selected cell, not dragging |
| Top right | × close button, 24 px, 6 px from the corner (`CloseBounds`, l. 1200–1205), the zoom badge under it (`ZoomBadgePath`, l. 1412–1429) | Hovered cell / while the zoom changes |
| Middle | ✥ swap handle, 48 px (24 px in a small cell, then right under the ×) | Hovered cell |
| Grid corners only | Borders' Corners brackets, over the images, 10 % of each grid edge, baked in the cached grid (`GridBorders.DrawOver`) | Borders on, Corners style |
| Grid corners only | Twitter rounded corners: the cached grid's pixels made transparent (`GridBorders.CutCorners`); nothing painted in `OnPaint` is cut | Borders on, Twitter corners on |
| Whole cell | Drag dim, drop-target highlight, external-drop highlight | While dragging / dropping |

Verdict per edge: **left** is the freest (outlines and bars only); **top** carries the × and the zoom
badge at its right end, **right** their column; **bottom** carries the file name of the selected cell
and, on the grid's two lower cells, the Corners brackets and the Twitter rounding.

### The bottom edge, just inside the selection outline

- A horizontal line along the **bottom edge**, from the cell's **left edge** to the fraction of its
  width played — the reading every video player taught. No track for the remaining part (Q&A #8):
  the line grows from the left edge, the rest of the edge is left as it is.
- **Just inside the selection outline**: a 2 logical px line over a 4 px halo, like the blur bars,
  the halo's lower edge on the inner edge of the outline — 3 px from the cell's bottom, every size
  scaled with `LogicalToDeviceUnits`. So the selection outline (3 px) and the hover band (1 px) never
  cover it, and the file name of the selected cell, whose line box ends 6 px above the edge, sits
  right on top of the halo without being covered.
- Painted **over the cached grid** — the image, the Corners brackets at the grid's lower corners, the
  Borders' gap fill — and **under the interaction feedback** (drag dim, drop-target highlight,
  selection and hover outlines), like the other helper indicators. Not cut by the Twitter corners,
  like the selection outline and the blur bars: it crosses the rounded-off corner over a few pixels
  on the grid's two lower cells.
- The **blur bars** use the same green on the same edge: on the selected cell while they show, the
  line is **hidden** — the bars replace it for as long as they show, the other cells keep theirs
  (Q&A #6).
- Colours: `HelperColor` / `HelperHalo`, shared (RULES.md).

---

## Timing and Repaint

- **`AnimationPlayer.ProgressOf(SourceImage image)`** → `double?`: `null` without a playback (a still,
  a stopped player) or with a frozen one (`PausedAt` set); else the fraction of the loop, from the
  clock, through a new pure helper **`Animation.Progress(TimeSpan time, TimeSpan loop)`** =
  `LoopTime(time, loop) / loop`, 0 when `loop <= 0`. `AnimationPlayer` stays the one place that knows
  a playback's position; nothing else reads the clock.
- **Repaint**: a `System.Windows.Forms.Timer` in `GridPreview`, **16 ms** (≈60 fps — Windows ticks it
  at about 15.6 ms), running while the preview is visible and at least one image plays. Started
  whenever the player is synced (`SyncPlayer`) or an image's Frames effect changes (`_player.Update`);
  a tick that finds nothing to repaint stops it; hiding the window stops it with the player.
- **Each tick** invalidates only the line's **strip** of each playing cell — the halo's band, cell
  wide — as the zoom badge invalidates its bounds (`OnZoomBadgeTick`). WinForms clips `OnPaint` to the
  invalidated region, so a tick repaints a few pixel rows per cell, not the grid. The per-frame
  `RedrawCell` (`Invalidate(cell)`) repaints the line too, from the same fraction.
- **`OnPaint`**: `PaintProgressLines(g, cells)` right after `g.DrawImageUnscaled(_cache, …)`, before
  the drag dim: for each cell whose image has a progress, the halo pen then the green pen
  (`SmoothingMode.None`, crisp like the bars), `DrawLine` from the cell's left edge to
  `left + round(fraction × width)`, clipped to the cell. Skipped for the cell whose blur bars show.
- **Cost**: N strips of about 4 px × cell width at 60 fps — a few ms of CPU per second; the 33 ms
  decode loop of the videos is untouched.

---

## Documentation

- `README.md` § Animated content: a **Progress** bullet right after *Live preview* — the green line
  along the bottom of every playing cell, from its left edge, continuous, preview only, absent on a
  frozen content.
- `README.md` § Features: a sub-bullet after the playing-contents one (l. 23), like the zoom badge's.
- `GLOSSARY.md`: a *Progress line (ligne de progression)* row.
- `RULES.md`: unchanged — the generic helper-indicator rules cover it; its own visibility (always,
  except frozen) is documented in the README.

---

## Test Impact

No test project exists in the app (declined in v1, Q&A #12 of `workfiles/20260923-application-v1.md`,
and in every workfile since): the behaviours are checked by hand, and the step is recorded as not
applicable in the Implementation Log.

Manual checks after the run:

| Check | Expected |
|---|---|
| A video in a cell | The line crosses the cell in the video's duration, glides at 60 fps, resets at the loop |
| A long text (10 views) | The line glides across in 10 s, never jumping every second |
| A PDF of N pages, an animated GIF | N s per lap; the sum of the GIF's delays |
| Frames starting point at 50 % | The line starts mid-cell, reaches the right edge, restarts from the left edge |
| Freeze, then unfreeze | The line disappears; it comes back from the starting point |
| Select the cell | The outline does not cover the line; the file name sits above it |
| Blur tab, blur on | The line disappears from the selected cell while its bars show; the other cells keep theirs |
| Borders: Corners, Twitter corners | Drawn over the bracket arms at the lower corners; crosses the rounded-off corner like the selection outline |
| Hide the window (tray), show it again | The line stops with the playback and resumes with it |
| Export MP4 / GIF / PNG | No line in the output; the preview's line keeps moving during the export |
| 150 % DPI | Line 3 px, halo 6 px, 5 px inside the edge |
| Four videos playing | The app stays responsive; CPU close to today's |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~Edge: the bottom edge just inside the selection outline (recommended), the top edge, or a vertical line on the left or right edge?~~ → The bottom edge, just inside the selection outline (Q&A #5)
- [x] ~~The selected cell while its blur bars show, same green on the same edge: line hidden (recommended), or both drawn side by side?~~ → Hidden on that cell while the bars show (Q&A #6)
- [x] ~~A grid of stills with a soundtrack shows no line, nothing moving in the cells: show the soundtrack's own progress somewhere (Global effects row)? Recommended: out of scope.~~ → Out of scope (Q&A #7)
- [x] ~~The remaining part of the loop: nothing (recommended, the line grows from the left edge), or a dim full-width track under it?~~ → No track, the line grows from the left edge (Q&A #8)
- [ ] *(raised by the run, 2026-09-30)* A content whose decoding fails keeps its playback entry (`AnimationPlayer.RunAsync` swallows the error and the cell keeps its frame): its line keeps gliding over a still frame. Hide it — stand the playback where it failed — or leave it?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the request — a fluorescent green line showing the content's progress, advancing
continuously rather than at each step of the content (not every second for a text), a ~10 ms timer
suggested, always shown, "what space is available?" — and the scoping batch (Q&A #1–4): every playing
content, always shown, preview only, clock-driven with a ~60 fps repaint (10 ms judged too fine: a
WinForms timer ticks at ~15.6 ms at best, and continuity comes from sampling the clock, not from the
tick), hidden on a frozen content. The exploration (single scout pass) gave the edge map, the
playback seam (`AnimationPlayer.Position` on one `Stopwatch`, frozen = `PausedAt`), the absence of a
test project and the README spots. The bottom edge just inside the selection outline is proposed;
the edge, the blur bars overlap, the soundtrack and a track are Open Questions.

### Iteration 2 — 2026-09-26

The four Open Questions answered as recommended (Q&A #5–8): the bottom edge just inside the
selection outline, the line hidden on the selected cell while its blur bars show, no indicator for
the soundtrack, no track for the remaining part. The design sections describe the agreed solution;
no question remains open, the go is asked.

### Iteration 3 — 2026-09-30 — ✅ Implemented

Go given for the code, the unit tests (not applicable, see Test Impact) and the documentation
(Q&A #9). Branch: the run stays on `main`, the standing choice of this app — no worktree was
requested.

### Iteration 4 — 2026-09-30 — 🧭 Implementation choices

Delivered as designed: `Animation.Progress` (pure, 0 ≤ p < 1, 0 without a loop),
`AnimationPlayer.ProgressOf` (`null` without a playback or with `PausedAt` set), and in `GridPreview`
the 16 ms timer, the strip invalidation, `PaintProgressLines` right after the cached grid, the line
skipped on the cell whose blur bars show. The choices the design left open:

- **Erasing a vanished line**: each tick also invalidates the strips it invalidated at the previous
  tick (`_progressStrips`), so the line of a content just frozen, or of a stopped player, is wiped by
  the next tick — the design only said what to invalidate for a playing cell.
- **Timer life**: started from `SyncPlayer` and from the Frames change (`ShowFrames`), stopped by a
  tick that finds nothing to repaint — as designed; nothing else touches it.
- **Constants**: `ProgressLineWidth` 2, `ProgressHaloWidth` 4, `ProgressTick` 16, next to the other
  `GridPreview` constants; the strip is the halo's band, its lower edge `SelectionWidth` above the
  cell's bottom (`ProgressStrip`).
- **Nothing at 0**: a line whose end rounds onto the left edge is not drawn.
- **Checks**: the app launched with two screen recordings of the user's Videos folder and a generated
  75-line text, its window captured twice 2 s apart with `PrintWindow`: the three lines glide (about
  50 px in 2 s on a 640 px cell), the short video's line had started over between the two captures,
  every line sits inside the bottom edge. The first check instance exited on its own (code 0) before
  the capture — quit from outside the run; a second one served the captures, then was stopped.
- **Gap found, not implemented** (scope freeze): a content whose decoding fails keeps its playback,
  so its line keeps gliding over the frame it kept — raised as an Open Question.
- No rule broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-09-30 | `fc632ba` on `main`: `Animation.Progress`, `AnimationPlayer.ProgressOf`, the `GridPreview` timer, strips and `PaintProgressLines` |
| Unit tests | — | 2026-09-26 | Not applicable: no test project, manual checks (Test Impact) |
| README | 3 | 2026-09-30 | `832e96d`: README § Features sub-bullet and § Animated content *Progress line* bullet, GLOSSARY *Progress line* row |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the progress line go? | Wait for the exploration, show me the free space | 2026-09-26 |
| 2 | A content frozen by the Frames effect: what does the line do? | Line hidden | 2026-09-26 |
| 3 | What cadence moves the line? | Clock-driven position, repaint at ~60 fps | 2026-09-26 |
| 4 | Is the exploration straightforward, or tricky / long? | Straightforward: a single scout pass | 2026-09-26 |
| 5 | Which edge, now that the edge map is known? | The bottom edge, just inside the selection outline | 2026-09-26 |
| 6 | The selected cell while its blur bars show: line hidden, or both drawn? | Line hidden on that cell | 2026-09-26 |
| 7 | Show the soundtrack's own progress somewhere, or out of scope? | Out of scope | 2026-09-26 |
| 8 | A dim track for the remaining part, or nothing? | No track | 2026-09-26 |
| 9 | Go for implementation? | Code, unit tests and documentation | 2026-09-30 |

---

*Last updated: 2026-09-30*
