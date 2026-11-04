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
- **Effect presets** (RULES.md § Effect Presets): the speed is saved with the Frames effect under its
  own key — `"speed"` in `FramesEffect.ToJson`, read back onto a step of the range by `FromJson`
  (`FramesEffect.SpeedOf`), ×1 when missing — and `LookPreset.Describe` names it after the starting
  point when it is not ×1 and the content is not frozen (`whole, starting at 0 %, ×1.5`).
- It applies to a **layer**'s content as to a cell's (RULES.md § Layers): the same `SourceImage`
  members are read for both, and a layer never takes part in the Cascade.

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

The two conversions are shared helpers of `Animation`, read by `SourceImage` and by the export's
`GridExport.Item` alike: `LoopAtSpeed(played, speed)` (the loop on the grid's clock) and
`AtSpeed(loopTime, loop, speed)` (the content time, kept inside the played part — one tick before
its end at most). At ×1 both give their input back unchanged.

- **Every reader of the loop in grid time** switches from `PlayedLength` to `LoopLength`:
  `Animation.LoopOf` (so the grid's loop, `Animation.VideoLength`, the export's length, the
  preview's wrap, the soundtrack's loop, the length readout and the timeline), the Cascade's turn
  (`CascadeSchedule.TurnOf`, a turn being one whole loop), the progress line
  (`AnimationPlayer`'s `Animation.Progress` calls) and the length readout's tooltip
  (`MainForm`'s `Loop`). A slowed-down GIF lengthens the video and its turn, a sped-up one shortens
  them.
- **Every reader of the content time** keeps going through `ContentTime`: the preview's frames, its
  sound sync. The export's `GridExport.Item` gets the grid-time loop and a `Speed` init property, its
  own `ContentTime` going through `Animation.AtSpeed`.
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
- **Export**: `MixedSound` carries the speed (a last parameter, 1 by default) and its loop and start
  on the video's clock (`LoopLength`, `StartTime`), `From` staying in the file; the mixer's `Voice`
  (`VideoEncoder`) decodes the file at the mix's rate divided by the speed, rounded to a whole rate,
  and mixes those samples one for one — Windows' resampler does the speed change. Its loop, start,
  turn and cascade stay counted in the mix's frames; only the seek to the starting point
  (`From + Start × Speed`) and the frames read back from the file use the decoded rate. A file
  Windows cannot decode at that rate is left out of the mix, like any sound it cannot re-encode.
- It stays *heard* at any speed (RULES.md § The Volume Exception is unchanged).

---

## UI

- In the Frames options — today the trim's two lines (`From` / `To`) at the left, a separator, then
  the starting point's line over **Freeze**: the speed gets a **third column** at the right, after
  a second separator — its value label over its slider (`OptionSlider`) — so the options toolbar
  keeps its height.
- Acting on it turns the effect on, starting from its kept settings (RULES.md § Options Toolbar).
- Its label reads **`Speed: ×1.5`** (invariant culture, `×1`, `×0.25`, `×1.75`…), written by
  `FramesEffect.SpeedText`, shared with the presets' description. The slider runs from 1 to 16 —
  steps of `SpeedStep` — its page step 4 (×1), Ctrl + wheel one step; a tooltip says what it does.
  No icon: RULES.md § Icons covers menus, tabs, buttons and drop-down entries, not sliders.
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
- [x] ~~Where does the speed slider go in the Frames options, now that they hold the trim?~~ → A third column after a separator, the label over the slider

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

### Iteration 4 — 2026-10-10

Placement chosen among three drawn layouts: a **third column** after a separator, the label over the
slider, the toolbar's height unchanged. § UI updated. No open question remains.

### Iteration 5 — 2026-11-04

The user asked whether the developments since the design (≈ 600 commits) go against its choices.
Checked: none does. The timing model is unchanged — the same readers of `PlayedLength` / `StartTime`
(`Animation.LoopOf` and `GridLength`, `CascadeSchedule.TurnOf`, `GridExport`'s `Item` and
`MixedSound`, `AnimationPlayer`'s offsets and progress, `MainForm`'s readout tooltip); the preview's
sound still plays through an `AudioFileInputNode`, the export's mixer still decodes at a chosen rate;
`Animation.StepDuration` is still the 1 s of PDFs and texts, nothing else sets a pace; no other
workfile nor `TODO-FEATURES.md` designs a speed. The new developments add to the scope, never against
it: **effect presets** (RULES.md § Effect Presets) require the speed in `FramesEffect.ToJson` /
`FromJson` and in `LookPreset.Describe`; **layers** play their contents through the same members, so
the speed reaches them unchanged; an **upscaled GIF**'s pages keep the source's timing
(`UpscaledPages` delegates `TimeOf` / `LoopDuration`). The icons rule (RULES.md § Icons) covers
menus, tabs, buttons and drop-down entries, not sliders: the speed slider needs none. § Settings
updated with the presets and the layers.

### Iteration 6 — 2026-11-04 — ✅ Implemented

Go given: **code, unit tests and documentation**. Unit tests: none to write — the repository has no
test project (§ Test Impact). Branch Gate: a **dedicated worktree**, `feature/frames-speed`, merged
into `main` with `--no-ff` at the end. Scope frozen on the design sections as they stand.

### Iteration 7 — 2026-11-04 — 🧭 Implementation choices

What the run decided on its own, the design sections updated to match:

- **Shared conversions**: `Animation.LoopAtSpeed` and `Animation.AtSpeed`, read by `SourceImage`
  and `GridExport.Item`; the content time is clamped one tick before the end of the played part, so
  a rounding at a speed other than ×1 never reads past it.
- **`FramesEffect`**: `MinSpeed`, `MaxSpeed`, `SpeedStep`, `WithSpeed`, and `SpeedOf` bringing any
  value onto a step of the range (a preset's included); `SpeedText` writes `×1.5`, for the label and
  the presets' description, which names the speed only when it is not ×1 and the content plays.
- **`GridExport.Item.Speed`** is an init property rather than a positional parameter, and
  `MixedSound.Speed` a last positional one, 1 by default: no caller that does not play changes.
- **Export sound**: the decoded rate is the mix's divided by the speed, **rounded** — 44 100 / 2.75
  is not whole — a speed error under 0.01 %. Its loop, start and turn stay in the mix's frames, so
  the Cascade's schedule is unchanged.
- **Preview sound**: the node's `PlaybackSpeedFactor` is set in `PreviewSound.Sync`, at each sync,
  whenever it differs from the image's speed.
- **Slider**: values 1 to 16, page step 4, Ctrl + wheel one step, a tooltip; no icon (RULES.md §
  Icons does not cover sliders).
- **RULES.md** gets a § Played Part › Speed subsection — the two clocks and which members are which —
  and § Cascade reads `LoopLength`.
- **The workfile committed on the branch**: the `main` checkout was in the middle of another
  session's conflict resolution, so the run's workfile entries ride on `feature/frames-speed`
  rather than on `main` directly.
- **Out of scope, noted**: the length readout's tooltip names each content's loop without saying it
  plays at another speed — a possible follow-up, not done.

No rule broken. A content's speed change replays that content only (RULES.md § Preview Playback),
as any Frames change: its record equality includes the speed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 6, 7 | 2026-11-04 | Effect and presets, timing, sound, UI — four commits on `feature/frames-speed`, plus a `this.` style fix |
| Unit tests | 6 | 2026-11-04 | None: the repository has no test project (§ Test Impact) |
| README | 6, 7 | 2026-11-04 | `README.md` and `README.fr.md`: § Frames, § Animated content |
| RULES / GLOSSARY | 6, 7 | 2026-11-04 | RULES.md § Played Part › Speed, § Cascade; GLOSSARY *Speed* and *Turn*, English and French |

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
| 11 | Where does the speed slider go in the Frames options? | A — a third column after a separator | 2026-10-10 |
| 12 | Go for implementation? | Not answered — the user asked instead to check the new developments against the design | 2026-11-04 |
| 13 | Go for implementation? | Code, unit tests and documentation | 2026-11-04 |
| 14 | Branch Gate: where does the run work? | A dedicated worktree, merged into `main` at the end | 2026-11-04 |

---

*Last updated: 2026-11-04*
