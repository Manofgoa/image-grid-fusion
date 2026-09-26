# Frames Speed

> Working document — a playback speed for the Frames effect.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The Frames effect sets where an animated image starts playing, or freezes it on a frame. It gets a
third setting: a **speed**, a **multiplier** of the image's own pace (×1 by default), chosen with a
slider and its value readout in the Frames options.

It applies to every animated image — videos, animated GIFs, PDFs of several pages and texts longer
than their cell (which scroll).

Today's pace, found in the code:

| Source | Pace today | Where |
|---|---|---|
| Video | Its own frame rate, `LoopDuration` = its duration | `Imaging/VideoFrames.cs` |
| Animated GIF | Its own frame delays, `LoopDuration` = their sum | `Imaging/GifFrames.cs` |
| PDF of several pages | 1 s per page | `Animation.StepDuration`, `Imaging/PdfPages.cs` |
| Long text | 1 s per view (half a page) | `Animation.StepDuration`, `Imaging/TextPages.cs` |

The "1 s" the request mentions is `Animation.StepDuration`: it stays the ×1 pace of PDFs and texts;
the multiplier scales it like it scales a GIF's delays or a video's frame rate.

---

## Settings

- `FramesEffect` gains a `Speed` (double, default **1**), next to `Position` and `Frozen`, with a
  `WithSpeed` builder. Like every effect setting, it is kept while the effect is off, reset by every
  *Reset*, and follows the image on a swap (RULES.md § Scope and State).
- Range and step: see Open Questions.

---

## Timing

One clock for the whole grid, as today; the speed maps the grid's time to the image's own time:

- **Source time** = `StartTime + gridTime × Speed`, looped on the source's `LoopDuration`.
- **Loop in grid time** = `LoopDuration / Speed`: the length of the grid's loop
  (`Animation.GridLength`, `Animation.VideoLength`) and of the export (`GridExport.Job.Length`) use
  it, so a slowed-down GIF lengthens the exported video, a sped-up one shortens it.
- The starting point stays a place along the frames (`Position`), in source time: the speed does not
  move it.
- A **frozen** image ignores the speed (it shows one frame).
- Applied in one place for the preview (`AnimationPlayer.RunAsync`) and one for the exports
  (`GridExport`'s frame loop), through a shared helper in `Animation` — the frame drawn is then
  handed to `Compositor.DrawCell` as today.

---

## Sound

A video with sound whose speed is not ×1: see Open Questions.

---

## UI

- In the Frames options, after **Freeze**: a speed slider (`OptionSlider`, like Volume) and its
  value label.
- Acting on it turns the effect on, starting from its kept settings (RULES.md § Options Toolbar).
- Label format, and whether the slider is disabled while Freeze is checked: see Open Questions.
- README § Frames and § Animated content: the speed setting, and "1 s per page / view" becomes the
  ×1 pace.

---

## Test Impact

**None.** The repository has no test project (`src/` holds `ImageGridFusion` only); earlier
workfiles shipped without unit tests and are verified manually in the app. To be confirmed by the
go choice.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~How is the speed expressed?~~ → A multiplier of the image's own pace, ×1 by default
- [x] ~~Which images does it apply to?~~ → Videos, animated GIFs, and scrolling content: PDFs of several pages, long texts
- [x] ~~Which control?~~ → A slider with its value shown next to it, like the Volume slider
- [ ] Range and step of the multiplier?
- [ ] Label format of the value?
- [ ] A video with sound at a speed other than ×1: what happens to its sound?
- [ ] While **Freeze** is checked, is the speed slider disabled?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Request: "Effect Frames: be able to choose the speed — currently it's 1 s, I think". Scoping batch
answered: a multiplier, for every animated image including scrolling PDFs and texts, set with a
slider and its value. Exploration (single pass, straightforward subject): the 1 s is
`Animation.StepDuration`, the pace of PDFs and texts only; GIFs and videos follow their own timing.
Design: `FramesEffect.Speed`, source time = start + grid time × speed, loop in grid time =
loop / speed, used by the preview, the exports and the grid's length. Four questions left open.

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
| 1 | How is the speed expressed? | A multiplier (×0.25 … ×4 style), ×1 by default | 2026-09-26 |
| 2 | Which images does the speed apply to? | GIFs, videos, and also scrolling content such as PDFs and texts | 2026-09-26 |
| 3 | Which control in the Frames options? | Slider + value | 2026-09-26 |
| 4 | Straightforward or tricky / long subject? | Straightforward | 2026-09-26 |
| 5 | Range and step of the multiplier? | | |
| 6 | Label format of the value? | | |
| 7 | Sound of a video at a speed other than ×1? | | |
| 8 | Speed slider disabled while Freeze is checked? | | |

---

*Last updated: 2026-09-26*
