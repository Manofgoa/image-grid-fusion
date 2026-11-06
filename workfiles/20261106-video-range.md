# Video Range and Soundtrack Trim

> Working document — choosing the start and the end of the exported video on the transport's timeline,
> the end possibly past the grid's loop (the contents looping), and the soundtrack's own start, end and loop.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two requests of the user (2026-11-06), designed together since both change what the video lasts:

1. **Video range** — on the transport's timeline (the global playback bar under the preview), choose a
   **start** and an **end** over the grid's whole duration. The end may lie **past the grid's loop**: the
   contents then keep looping — e.g. a GIF repeated under a longer piece of music.
2. **Soundtrack trim** — in the Soundtrack global effect, choose the **start** and the **end** of the part
   of the audio file played, and **whether it loops**.

Today the video length has one definition, `Animation.VideoLength` (RULES.md § Video Length): the grid's
loop — the longest playing content, the Cascade, an Animations cycle — or the soundtrack's length for a
grid of stills. The preview wraps at it (`AnimationPlayer.WrapLoop`), the export lasts it
(`GridExport.Job.Length`), the soundtrack loops or is cut to it (`Soundtrack.LoopIn`).

Components touched: `Composition/Animation`, `Composition/Soundtrack`, `UI/PlaybackTimeline`,
`UI/AnimationPlayer`, `UI/PreviewSound`, `Imaging/GridExport`, `Imaging/VideoEncoder` (mixer),
`UI/GridHistory` (`GlobalState`), `UI/MainForm` (transport wiring, Soundtrack options, Resets).

---

## Terms

| Term | Meaning |
|---|---|
| Grid loop | What `Animation.GridLength` gives today: the longest playing content, the Cascade, an Animations cycle |
| **Range** (*plage*) | The part of the grid's timeline the video plays and exports: a **start** and an **end**, in grid time; by default the whole grid loop (the video length as today) |
| **Soundtrack trim** | The soundtrack's **From** and **To** in its audio file, its **played part**, played once or looped |

---

## Video Range

### What It Drives

*Proposed — see Open Questions 1 and 5.*

| Consumer | Today | With a range |
|---|---|---|
| Video length (`Animation.VideoLength`) | The grid loop, or the soundtrack for stills | **end − start** — the one definition stays one: the range is read there |
| Preview wrap (`AnimationPlayer.WrapLoop`) | At the loop, back to 0 | At **end**, back to **start** (`Seek(start)`) |
| Playhead (`AnimationPlayer.LoopPosition`) | `LoopTime(GridTime, LoopLength)` | The grid time itself, within the range — no modulo at the grid loop, so the playhead does not drop back past it |
| Export frames (`GridExport` run loop) | `time = FrameTime(k)`, k over `FrameCount(Length)` | Grid time `start + FrameTime(k)` for `Job.ContentTime`, the Animations' `frames[i].Time` and the layers; the encoders keep the relative time `FrameTime(k)` |
| Export sound (`MixedSound`, mixer) | Every voice from `Start` (its content start) at sample 0 | Every voice's `Start` shifted by **start**; the Cascade's `_cyclePosition` seeded with `start mod cycle` |
| Fade | Over the video length | Over the range: in at **start**, out at **end** (the mixer's `GainAt(t, length)` already reads the exported length; the preview's `SyncFade` reads the range) |
| Length readout, Copy / Save captions | `⏱ 10.0 s` | `⏱` the range's length; the tooltip saying the range when it is not the whole loop |

Findings from the exploration (inputs, not decisions):

- **Contents already loop past the grid loop**: a content's time is `LoopTime(StartTime + t, loop)`
  (`SourceImage.ContentTime`, `GridExport.Item.ContentTime`), the Cascade's `Played` / `Plays` wrap at
  their schedule (`CascadeSchedule`), and a heard video's preview sound follows the clock — nothing to
  change there. `Seek(t)` with t > 0 already shows the right frames.
- **The preview's soundtrack** wraps at the video length (`AnimationPlayer.SyncSoundtrack`) and is reset
  to its beginning by every `Seek` (`_soundtrackStart = 0`): both change (§ Soundtrack Under the Range).
- **The mixer's voices loop past their length** (`Rewind` in `MixLoop`) and stop only at the exported length.

### Timeline

*Proposed — see Open Questions 2 and 4.*

- `UI/PlaybackTimeline` gets two **range handles**, start and end, dragged along the track; the part
  outside the range drawn dimmed, the played fill and the thumb inside it.
- The track maps x ↔ time against its **extent**, no longer the grid loop alone: the extent is at least the
  grid loop and reaches past the end (Open Question 4), so the end handle can be dragged beyond the loop.
  The grid loop's end is marked on the track (a tick), and the Cascade's ticks repeat past it.
- Dragging a handle holds the grid like a seek (`SeekStarted` / `SeekEnded`), the playhead following the
  handle, scrub audio at its place.
- The handles snap onto the grid loop's end and onto the Cascade's ticks, within 6 logical px.
- A minimum range — one frame of the export's frame rate.

### State

*Proposed — see Open Questions 3 and 8.*

- A `VideoRange` value (start, end, or *whole*), held in `GlobalState` — a global setting with no tab, part
  of the composed state: an undo step, restored, reset to *whole* by the Global toolbar's Reset and *Clear
  all*; not persisted.
- *Whole* follows the grid loop as today; a set range is kept in seconds when the grid loop changes, its
  start clamped into the loop.

### Soundtrack Under the Range

*Proposed — see Open Questions 5 and 7.*

- The soundtrack plays on the grid's clock: at grid time t, its own time is `t` within its played part
  (looped or not, § Soundtrack Trim) — the same in the preview and the export, whatever the range. A
  range starting at 10 s hears the soundtrack from its 10th second.
- `SyncSoundtrack` reads `LoopTime(clock − origin, soundtrack's played length)` without the wrap at the
  video length; `Seek` stops resetting `_soundtrackStart`.

---

## Soundtrack Trim

*Proposed — see Open Question 6.*

- `Soundtrack` gains **From**, **To** (the played part, the whole file by default) and **Loop** (on by
  default — today's behaviour).
- Options: the Frames trim's controls — a two-thumb slider over the file's duration and `m : s` fields
  for From and To — then a **Loop** checkbox, after the file and the volume.
- **Loop on**: the part repeats for as long as the video lasts. **Loop off**: the part plays once from
  the video's time 0 and silence follows.
- A grid of stills takes the **part's length** as its loop (`Soundtrack.LoopIn` returns `To − From`).
- Export: `MixedSound(path, To − From, Start, Level, From)` already plays a part; Loop off adds a
  *no rewind* flag to the voice, silent once the part has played.
- Preview: the soundtrack node (`PreviewSound.SyncSoundtrack`) seeks into `From + t`, and stops at `To`
  when Loop is off — the `Span` the video voices have, given to the soundtrack node.
- The detail strings of the length readout (`MainForm` ~l.2863-2935, *the soundtrack loops / is cut*)
  say *plays once* for Loop off.
- Acting on any of these options turns the Soundtrack on (§ Options Toolbar); the Resets bring back the
  whole file, looped.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `Animation.VideoLength` gives the range's length when set, the grid loop or the soundtrack's part otherwise | `tests/ImageGridFusion.Tests/Composition/AnimationTests.cs` | Create |
| A range past the grid loop is kept; a range kept in seconds is clamped when the loop shortens; *whole* follows it | `tests/ImageGridFusion.Tests/Composition/VideoRangeTests.cs` | Create |
| `Soundtrack.LoopIn` gives `To − From` for a grid of stills; From / To clamped into the file, a minimum part | `tests/ImageGridFusion.Tests/Composition/SoundtrackTests.cs` | Create |
| The timeline's x ↔ time mapping against an extent longer than the loop | `tests/ImageGridFusion.Tests/UI/PlaybackTimelineTests.cs` | Create |

---

## Open Questions

- [ ] What does the range drive: the export and the preview (the preview loops inside it, the Fade at its
      edges), the export only, or the preview only?
- [ ] How are the start and the end set: handles on the timeline, handles plus time fields, or a tab of the
      Global toolbar?
- [ ] When the grid loop changes (a video added or removed, a trim changed), what becomes of a set range:
      kept in seconds and clamped, reset to the whole loop, or kept as fractions?
- [ ] How far past the grid loop can the end go, and how does the track show it?
- [ ] Under a range not starting at 0, where does the soundtrack stand: on the grid's clock (its 10th
      second at 10 s), or starting at the range's start?
- [ ] How are the soundtrack's From / To set: the Frames trim's controls (slider + fields), or another way?
- [ ] A soundtrack longer than the grid loop: an action setting the range's end to the soundtrack's part
      (*Fit to soundtrack*), or nothing beyond dragging the end?
- [ ] The range in the undo history and reset by the Global Resets and *Clear all* (like the Format), or out
      of the history like the playback position?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-06

- Request: *choose the start / end over the whole video duration, on the global playback bar*.
- Request (same day): *the duration may be longer than the video itself, which then loops — useful to put
  music under a repeating GIF*. → the end may lie past the grid loop.
- Request (same day): *in the soundtrack, choose its start / end and whether it loops*. → § Soundtrack Trim.
- The scoping batch (scope, controls, a changing loop, depth) was answered by the two messages above
  rather than by its choices: the unanswered points are Open Questions 1–3; the exploration was taken as
  *tricky* (scout, then deepening on the export and the preview clock).
- Exploration: contents, the Cascade and the heard videos already loop past the grid loop, in the preview
  and the export; the wrap, the playhead, the soundtrack and the Fade are the places reading the loop.
- First proposal written in the domain sections, each part marked with the Open Questions it depends on.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| RULES.md (§ Video Length, § Transport, Soundtrack) | | | |
| GLOSSARY.md / GLOSSARY.fr.md (Range, soundtrack trim) | | | |
| SHORTCUTS.md / SHORTCUTS.fr.md (handle gestures) | | | |
| Strings.resx / Strings.fr.resx | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Scoping batch: scope, controls, a changing loop, exploration depth | Not answered as choices — replaced by two messages: the end may go past the loop (contents looping), and the soundtrack gets its own start / end / loop | 2026-11-06 |
| 2 | What does the range drive? | | |
| 3 | How are start and end set? | | |
| 4 | A set range when the grid loop changes? | | |
| 5 | How far past the grid loop can the end go? | | |
| 6 | Where does the soundtrack stand under a range not starting at 0? | | |
| 7 | How are the soundtrack's From / To set? | | |
| 8 | *Fit to soundtrack* action? | | |
| 9 | Range in the undo history and the Resets? | | |

---

*Last updated: 2026-11-06*
