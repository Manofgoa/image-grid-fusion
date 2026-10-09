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
- Range: **×0.25 to ×4**, in steps of **0.25** (16 notches, ×1 on the fourth). Constants
  `MinSpeed`, `MaxSpeed`, `SpeedStep` on `FramesEffect`; `WithSpeed` clamps to the range.

---

## Timing

One clock for the whole grid (`AnimationPlayer`'s `PlaybackClock`, RULES.md § Transport), as today.
Since 2026-10-08 a content's time goes through **one definition**, the played part
(`SourceImage.PlayedFrom` / `PlayedLength` / `ContentTime`, RULES.md § Played Part); the speed
slots in there, between the **loop time** (grid time, what the clock, the Cascade and the video
length count) and the **content time** (what the frames and the sound are read at):

| Member of `SourceImage` | Today | With the speed |
|---|---|---|
| `Speed` (new) | — | `Look.Frames?.Speed ?? 1` while it plays, 1 otherwise |
| `PlayedFrom`, `PlayedLength` | Content time | Unchanged: content time, the trim's |
| `LoopLength` (new) | — | `PlayedLength / Speed`: the played part's loop **in grid time** |
| `StartTime` | Content time from `PlayedFrom` | **Grid time**: the starting point's offset in the loop, `/ Speed` |
| `ContentTime(position)` | `PlayedFrom + LoopTime(position, PlayedLength)` | `PlayedFrom + LoopTime(position, LoopLength) × Speed` |

- **Every reader of the loop in grid time** switches from `PlayedLength` to `LoopLength`:
  `Animation.LoopOf` (so the grid's loop, `Animation.VideoLength`, the export's length, the
  preview's wrap, the soundtrack's loop, the length readout and the timeline), the Cascade's turn
  (`CascadeSchedule.TurnOf`, a turn being one whole loop), the progress line
  (`AnimationPlayer`'s `Animation.Progress` calls) and the length readout's tooltip
  (`MainForm`'s `Loop`). A slowed-down GIF lengthens the video and its turn, a sped-up one shortens
  them.
- **Every reader of the content time** keeps going through `ContentTime`: the preview's frames, its
  sound sync. The export's `GridExport.Item` gets the speed and the grid-time loop, its own
  `ContentTime` following the same formula.
- The starting point and the trim stay places along the frames, in content time: the speed moves
  neither, and their fields (minutes : seconds : frame, GIF frame number) are unchanged.
- A **frozen** image ignores the speed (it shows one frame, `Speed` reads 1).
- An Animations motion's cycle is not a content: the speed does not touch it.

---

## Sound

A video with sound **follows its speed, its pitch with it** (higher when faster, lower when slower —
no time-stretching):

- **Preview**: the video's `AudioFileInputNode` (`PreviewSound`) plays with its
  `PlaybackSpeedFactor` set to the speed; its span (`PreviewSound.Span`) and its drift check stay in
  content time, as today. The scrub snippets follow by themselves.
- **Export**: `MixedSound` carries the speed; the mixer's `Voice` (`VideoEncoder`) decodes the
  file at `rate / speed` and mixes those samples one for one at `rate` — the resampler does the
  speed change — its loop, start, turn and cascade counted in that decoded rate.
- It stays *heard* at any speed (RULES.md § The Volume Exception is unchanged).

---

## UI

- In the Frames options — today the trim's two lines (`From` / `To`) at the left, a separator, then
  the starting point's line over **Freeze** at the right: a speed slider (`OptionSlider`) and its
  value label. Placement: see Open Questions.
- Acting on it turns the effect on, starting from its kept settings (RULES.md § Options Toolbar).
- Its label reads **`Speed: ×1.5`** (invariant culture, `×1`, `×0.25`, `×1.75`…).
- While **Freeze** is checked, the speed slider is **disabled** (a frozen image plays nothing); the
  speed is kept for when it is unfrozen.
- README § Frames and § Animated content: the speed setting, and "1 s per page / view" becomes the
  ×1 pace.

---

## Test Impact

**None.** The repository still has no test project (`src/` holds `ImageGridFusion` only, checked
2026-10-10); earlier workfiles shipped without unit tests and are verified manually in the app. To
be confirmed by the go choice.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~How is the speed expressed?~~ → A multiplier of the image's own pace, ×1 by default
- [x] ~~Which images does it apply to?~~ → Videos, animated GIFs, and scrolling content: PDFs of several pages, long texts
- [x] ~~Which control?~~ → A slider with its value shown next to it, like the Volume slider
- [x] ~~Range and step of the multiplier?~~ → ×0.25 to ×4, step 0.25
- [x] ~~Label format of the value?~~ → `Speed: ×1.5`
- [x] ~~A video with sound at a speed other than ×1: what happens to its sound?~~ → It follows the speed, its pitch changing with it, in the preview and the export
- [x] ~~While **Freeze** is checked, is the speed slider disabled?~~ → Yes, disabled; the speed is kept
- [ ] Where does the speed slider go in the Frames options, now that they hold the trim?

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

### Iteration 2 — 2026-09-26

Second batch answered: range ×0.25–×4 in steps of 0.25; label `Speed: ×1.5`; a video's sound
follows its speed with its pitch (preview audio graph speed factor, export mixer resampling); the
speed slider is disabled while Freeze is checked. § Settings, § Sound and § UI updated. No open
question remains.

### Iteration 3 — 2026-10-10

Resumed after two weeks: the go was never asked (the session ended on the question), and the user
chose to refresh the design first. The code moved under it — the trim and its played part
(`workfiles/20261008-video-trim.md`), the Cascade (`workfiles/20261008-video-cascade.md`), the
transport's one clock (`workfiles/20261009-playback-controls.md`). § Timing rewritten on the played
part: a new `SourceImage.LoopLength` (grid time) beside `PlayedLength` (content time), `StartTime`
moved to grid time, `ContentTime` scaling by the speed; the Cascade's turn and the video length
follow `LoopLength`. § Sound updated: `PlaybackSpeedFactor` in the preview, decoding at
`rate / speed` in the export. One new question: the slider's place, the Frames options now holding
the trim.

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
| 5 | Range and step of the multiplier? | ×0.25 → ×4, step 0.25 | 2026-09-26 |
| 6 | Label format of the value? | `Speed: ×1.5` | 2026-09-26 |
| 7 | Sound of a video at a speed other than ×1? | Follows the speed, higher / lower pitch | 2026-09-26 |
| 8 | Speed slider disabled while Freeze is checked? | Yes, disabled | 2026-09-26 |
| 9 | Go for implementation? | Not answered — the session ended on the question | 2026-09-26 |
| 10 | How do we resume the workfile? | Refresh the design first | 2026-10-10 |
| 11 | Where does the speed slider go in the Frames options? | | |

---

*Last updated: 2026-10-10*
