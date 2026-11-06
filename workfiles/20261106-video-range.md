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

*Agreed: the export and the preview, the Fade at the range's edges (Q&A 2).*

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

*Agreed: handles plus fields (Q&A 3), an extensible track (Q&A 5).*

- `UI/PlaybackTimeline` gets two **range handles**, start and end, dragged along the track; the part
  outside the range drawn dimmed, the played fill and the thumb inside it.
- Two **time fields**, start and end, on a line **under the timeline** in the transport, with Keep range and
  Fit to soundtrack, so the timeline keeps its width (Iteration 6), for an exact value: typing
  one moves its handle, dragging a handle updates its field. The end field accepts a time past the grid
  loop.
- The track is **extensible**: it maps x ↔ time against its **extent** (`VideoRange.Extent`) — the grid
  loop, or, the range ending past it, the range's end plus one grid loop of room. While a handle is dragged
  the extent is frozen and the end follows the pointer past the track's right edge; released, the track
  lengthens — with no limit. The grid loop's end is marked on the track (a dashed tick), and the Cascade's
  ticks repeat past it.
- A click or a scrub stays inside the range; the time shown beside the timeline counts from the range's
  start, as the exported video does.
- Dragging a handle holds the grid like a seek (`SeekStarted` / `SeekEnded`), the playhead following the
  handle, scrub audio at its place.
- The handles snap onto the grid loop's end and onto the Cascade's ticks, within 6 logical px.
- A minimum range — one frame of the export's frame rate.

### State

*Agreed: the Keep range checkbox (Q&A 10, revising Q&A 4); the undo history and the Resets (Q&A 11).*

- A `VideoRange` value: a start, an end, or *whole* — the whole grid loop, today's behaviour.
- A **Keep range** checkbox (*Garder la plage*) beside the time fields decides what happens when the grid
  loop changes — a video dropped or removed, a trim changed:
  - **checked**: the range typed or dragged is **kept** as it is, in seconds — its start clamped into the
    new loop if the loop got shorter;
  - **unchecked**: the range goes back to *whole*, so the video lasts the new grid loop.
- The checkbox unchecked by default, and not checked by itself when a handle is dragged or a field typed.
- The reset follows the **natural length** — the video length without the range — whatever changed it (a
  drop, a removal, a trim, the Cascade, the soundtrack over stills): `MainForm.FollowLoop`, read with the
  length readout. A step of the undo history put back keeps the range it holds.
- A range covering the loop exactly is stored as *whole* (`null`).
- `VideoRange` and the checkbox held in `GlobalState`, a global setting with no tab, like the Format: part
  of the composed state — an undo step, restored —, reset (*whole*, unchecked) by the Global toolbar's
  Reset and *Clear all*; not persisted.

### Fit to Soundtrack

*Agreed (Q&A 11).*

- A **Fit to soundtrack** button (*Caler sur la bande son*) in the transport, after the time fields and
  the Keep range checkbox, enabled while the Soundtrack is on: it sets the range to **0 → the soundtrack's
  played part** (`To − From`), so the video lasts one play of the chosen part of the music — longer or
  shorter than the grid loop.
- An ordinary change of the range: one undo step; it does not check Keep range by itself (§ State).

### Soundtrack Under the Range

*Agreed (Q&A 11).*

- The soundtrack plays on the grid's clock: at grid time t, its own time is `t` within its played part
  (looped or not, § Soundtrack Trim) — the same in the preview and the export, whatever the range. A
  range starting at 10 s hears the soundtrack from its 10th second.
- `SyncSoundtrack` reads `LoopTime(clock − origin, soundtrack's played length)` without the wrap at the
  video length; `Seek` stops resetting `_soundtrackStart`.

---

## Soundtrack Trim

*Agreed (Q&A 11).*

- `Soundtrack` gains **From**, **To** (the played part, the whole file by default) and **Loop** (on by
  default — today's behaviour).
- Options: the Frames trim's controls — a slider and a minutes : seconds : frame field each for **From**
  and **To**, in frames of the export's rate (30 fps), over the file's duration — then a **Loop** checkbox,
  after the file and the volume. They wait for a file (disabled without one); a new file plays whole,
  keeping the previous one's Loop.
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
| *Fit to soundtrack* gives the range 0 → `To − From`, past the grid loop or short of it | `tests/ImageGridFusion.Tests/Composition/VideoRangeTests.cs` | Create |
| The soundtrack's own time at a grid time: on the clock, within its part, looped or silent past it (Loop off) | `tests/ImageGridFusion.Tests/Composition/SoundtrackTests.cs` | Create |
| The timeline's x ↔ time mapping against an extent longer than the loop | `tests/ImageGridFusion.Tests/UI/PlaybackTimelineTests.cs` | Create |

---

## Open Questions

- [x] ~~What does the range drive: the export and the preview (the preview loops inside it, the Fade at its
      edges), the export only, or the preview only?~~ → The export and the preview, the Fade at its edges
- [x] ~~How are the start and the end set: handles on the timeline, handles plus time fields, or a tab of the
      Global toolbar?~~ → Handles on the timeline plus two time fields
- [x] ~~When the grid loop changes (a video added or removed, a trim changed), what becomes of a set range:
      kept in seconds and clamped, reset to the whole loop, or kept as fractions?~~ → A **Keep range**
      checkbox: checked, the range is kept; unchecked, it goes back to the whole loop
- [x] ~~How far past the grid loop can the end go, and how does the track show it?~~ → No limit: an
      extensible track, the end plus one loop of room, lengthened when the end handle reaches its edge
- [x] ~~Under a range not starting at 0, where does the soundtrack stand: on the grid's clock (its 10th
      second at 10 s), or starting at the range's start?~~ → On the grid's clock
- [x] ~~How are the soundtrack's From / To set: the Frames trim's controls (slider + fields), or another way?~~
      → The Frames trim's controls, then a Loop checkbox
- [x] ~~A soundtrack longer than the grid loop: an action setting the range's end to the soundtrack's part
      (*Fit to soundtrack*), or nothing beyond dragging the end?~~ → A *Fit to soundtrack* button
- [x] ~~The range in the undo history and reset by the Global Resets and *Clear all* (like the Format), or out
      of the history like the playback position?~~ → In the history, reset by the Resets, like the Format

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

### Iteration 2 — 2026-11-06

- Answers: the range drives the export and the preview (the Fade at its edges); it is set by handles
  **and** two time fields; the track is extensible, with no limit past the grid loop.
- Answer: a set range goes back to the whole loop when the loop changes — then **refined** by the user: a
  **Keep range** checkbox decides it — checked, the range typed is kept; unchecked, dropping a new video
  computes the length again. § Video Range › State rewritten; the checkbox's default (unchecked) and that
  editing the range does not check it are proposals.
- The second batch (soundtrack under the range, soundtrack trim controls, *Fit to soundtrack*, undo
  history) was dismissed by that message: Open Questions 5–8 stay open.

### Iteration 3 — 2026-11-06

- The user took the recommendations for Open Questions 5–8: the soundtrack stands on the grid's clock;
  its From / To set by the Frames trim's controls, then a Loop checkbox; a **Fit to soundtrack** button
  in the transport (new § Fit to Soundtrack); the range in the undo history and reset by the Global Resets
  and *Clear all*, like the Format.
- Test Impact: *Fit to soundtrack* and the soundtrack's own time at a grid time added.
- No Open Question left. Still proposals in § State: the Keep range checkbox unchecked by default, and not
  checked by itself when the range is edited.

### Iteration 4 — 2026-11-06 — ✅ Implemented

- Go given: code, unit tests and documentation, in a worktree (`feature/video-range`).

### Iteration 5 — 2026-11-06 — 🧭 Implementation choices

No rule broken. The choices the frozen design did not state:

- **The track's extent**: the grid loop, or the range's end plus one loop when the range ends past it
  (`VideoRange.Extent`); with no range set, the end handle sits on the right edge and is dragged past it.
  The extent is frozen during a handle drag so the handle stays under the pointer; it is recomputed on release.
- **Seeks clamped into the range**: a click or a scrub outside the range lands on its nearest edge.
- **The time readout** beside the timeline counts from the range's start (`3.2 s / ⏱ 10.0 s` is the video's
  time over its length), the thumb staying on the grid's timeline.
- **A range covering the loop exactly is stored as `null`** (*whole*), so it follows the loop like no range.
- **The reset of an unkept range** follows any change of the natural length — not only a drop —, as the
  scoping answer named a trim too; checked in `MainForm.FollowLoop` with the length readout, held off while
  a step is restored.
- **Soundtrack trim controls**: two sliders and two minutes : seconds : frame fields (the Frames trim's
  shape, the "two-thumb slider" of the design not existing in the app), in frames of the export's 30 fps;
  disabled, Loop included, until a file is chosen; a new file plays whole and keeps the previous Loop.
  The Soundtrack's own Reset still forgets the file, its part with it.
- **Fit to soundtrack**: a button after Keep range, its icon 🎼 (`UiIcons.FitToSoundtrack`), enabled with
  the Soundtrack on over a video; the range 0 → the part's length.
- **The undo label** *Video range* is given only when the cells did not change, so a drop resetting the
  range is named by the drop.
- **The range belongs to no tab**: only the Global toolbar's Reset and *Clear all* reset it, not a tab's own Reset.
- **The export**: the GIF plays the range too; the mixer's voices take the range's start (`MixedSound.At`) —
  a cascade voice seeded at its place in the cascade — and a soundtrack not looped plays once
  (`MixedSound.Once`).
- **Test Impact**: the timeline's mapping is pinned through two static helpers (`PlaybackTimeline.XOf`,
  `TimeAt`) rather than by driving the control; the soundtrack's own time is tested through
  `Soundtrack.FileTimeAt`, the video length through its `TimeSpan` overload (a `SourceImage` needs files).
- **Not checked by hand**: the app was launched with a video (the handles and the fields shown), but no
  range was dragged, exported or heard in this run — left to the user's test.

### Iteration 6 — 2026-11-06 — ⚙️ Post-implementation — Range controls under the timeline

- Request: the range's controls beside the timeline take too much room — put them under it.
- The transport gets a second line: the start and end fields, Keep range and Fit to soundtrack under the
  timeline, aligned with it; the timeline takes the row's whole width again. The docs saying *beside the
  timeline* follow.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 6 | 2026-11-06 | Model, export, preview, timeline, transport, Soundtrack options; the range's controls under the timeline |
| Unit tests | 4 | 2026-11-06 | `VideoRangeTests`, `SoundtrackTests`, `AnimationTests`, `PlaybackTimelineTests` — 21 passed |
| RULES.md (§ Video Length, § Transport, Soundtrack) | 4 | 2026-11-06 | § Transport, § Video Length, new § Video Range and § Soundtrack Part |
| GLOSSARY.md / GLOSSARY.fr.md (Range, soundtrack trim) | 4 | 2026-11-06 | Range added; Soundtrack, Video length, Transport, Timeline updated |
| SHORTCUTS.md / SHORTCUTS.fr.md (handle gestures) | 4 | 2026-11-06 | The time fields' rows cover the Soundtrack's and the range's; a plain handle drag is no shortcut |
| Strings.resx / Strings.fr.resx | 4 | 2026-11-06 | 16 texts, both languages |
| README | 4 | 2026-11-06 | § Soundtrack and § Transport, English and French |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Scoping batch: scope, controls, a changing loop, exploration depth | Not answered as choices — replaced by two messages: the end may go past the loop (contents looping), and the soundtrack gets its own start / end / loop | 2026-11-06 |
| 2 | What does the range drive? | The export and the preview (recommended) | 2026-11-06 |
| 3 | How are start and end set? | Handles plus time fields | 2026-11-06 |
| 4 | A set range when the grid loop changes? | Reset to the whole loop — then refined by row 10 | 2026-11-06 |
| 5 | How far past the grid loop can the end go? | An extensible track (recommended) | 2026-11-06 |
| 6 | Where does the soundtrack stand under a range not starting at 0? | Not answered — the user sent row 10 instead | 2026-11-06 |
| 7 | How are the soundtrack's From / To set? | Not answered — the user sent row 10 instead | 2026-11-06 |
| 8 | *Fit to soundtrack* action? | Not answered — the user sent row 10 instead | 2026-11-06 |
| 9 | Range in the undo history and the Resets? | Not answered — the user sent row 10 instead | 2026-11-06 |
| 10 | (User's message on row 4) | The extended value is an option, a checkbox: checked, the duration entered is kept; unchecked, dropping a new video computes the duration again | 2026-11-06 |
| 11 | Rows 6–9 again, with the recommendations as defaults? | Yes, the recommendations: the soundtrack on the grid's clock, the Frames trim's controls plus Loop, a *Fit to soundtrack* button, the range in the undo history like the Format | 2026-11-06 |

---

*Last updated: 2026-11-06*
