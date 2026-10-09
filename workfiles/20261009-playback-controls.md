# Playback Controls

> Working document — a transport for the preview: Play / Pause, a timeline over the whole video
> length (Cascade included) to seek in it, and the preview paused while a Copy or an export runs.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the preview plays on its own and cannot be stopped nor moved: every content loops on the
grid's clock, and the only control is the grid **starting over** when an image arrives or leaves.
The user wants to **navigate the whole video** — the length the export will have,
`Animation.VideoLength`: two 5 s videos in Cascade give a 10 s timeline — and to **pause** it.

In scope (scoping answers, Q1–Q3):

- A **timeline** over the video length, showing the current time and the total, dragged to seek.
- A **Play / Pause** button, and **Space** toggling it.
- **Cascade turn markers** on the timeline.
- **Scrub audio**: short snippets heard while the timeline is dragged.
- **Paused during Copy / export** — the backlog row *Pause during Copy / export*
  (`TODO-FEATURES.md`): the preview pauses while a Copy or an export runs, then resumes as it was.

Out of scope: frame-by-frame stepping (`,` / `.`) and a *Home* key (declined at scoping).

Components: `UI/AnimationPlayer.cs` (the clock), `UI/PreviewSound.cs` (the sound), `UI/GridPreview.cs`
(redraws, `Restart` callers), `UI/MainForm.cs` (layout, `ProcessCmdKey`, `BeginExport` / `EndExport`,
`RefreshLength`), `Composition/VideoCascade.cs` (`CascadeSchedule`), `Composition/Animation.cs`
(`VideoLength`).

---

## Grid Clock

The preview already has **one clock**: `AnimationPlayer._clock`, a free-running `Stopwatch`
(`AnimationPlayer.cs:23`), exposed as `GridTime` (`:171`), zeroed by `Restart` only (`:149`), never
wrapped. Everything reads it:

| Reader | How |
|---|---|
| A video / GIF frame | `Position(playback)` (`:311`): `_clock.Elapsed - Offset`, or `StartTime + schedule.Played(i, GridTime)` in Cascade — then `SourceImage.ContentTime` (loop on the played part) |
| The Animations motion, the progress line | `GridTime` (`GridPreview.cs:1640`, `:194`) |
| The Cascade | `CascadeSchedule.Played` / `Plays` on `LoopTime(GridTime, schedule.Length)` (`VideoCascade.cs:77-94`) |
| The soundtrack | `_clock.Elapsed - _soundtrackStart` (`:255`, `:272`), 100 ms timer |
| The Fade | `_clock.Elapsed - origin` (`:304`), 30 ms timer |

Design:

- `_clock` becomes a **pausable, seekable clock** (a small class next to `AnimationPlayer`):
  `Elapsed = origin + (running ? stopwatch.Elapsed : 0)`, with `Pause()`, `Resume()`, `Seek(t)`,
  `Restart()`. **Every** read of `_clock` goes through it — one clock, so frames, motion, Cascade,
  soundtrack and Fade stay in step.
- The **grid time shown** is `LoopTime(GridTime, VideoLength)` (`AnimationPlayer.LoopPosition`), and
  the **clock itself wraps** at the end of the loop (`AnimationPlayer.WrapLoop`, a 30 ms timer): the
  preview loops as the exported video does, the shorter contents starting over with it.
- **Seek(t)** sets the clock to `t ∈ [0, VideoLength)` and puts the grid **where the export stands
  at `t`**: every playing content's `Offset` back to `-StartTime` (a content replayed alone by a
  Frames change included), the soundtrack's start back to 0. Every content follows by itself — they
  all apply `LoopTime` downstream; a Cascade content lands before, in or after its turn as the
  schedule says. `Restart` is `Seek(0)`.
- The global pause **never reuses `Playback.PausedAt`** — that is the Frames effect's frozen state,
  set and cleared by `Update` / `Restart`. A frozen content stays frozen, paused or not.
- While paused:
  - the decode loop (`RunAsync`, `:352-408`) stops advancing, but **renders once after a seek** (a
    dirty flag), so the frame under the timeline's thumb shows;
  - the sound is silent (`playing && !Paused` into `PreviewSound.Sync`, the soundtrack and Fade
    timers stopped);
  - `GridPreview`'s progress timer redraws once after a seek (the Animations cells included).

### Starting Over

§ Preview Playback (RULES.md) is kept: an image arriving or leaving, and the Cascade toggled or its
pause changed, make the grid **start over** (`AnimationPlayer.Restart`, called from `GridPreview.cs`
`:371`, `:490`, `:654`, `:1478`).

- **While paused, the grid starts over and stays paused at 0** (Q3): the first instant of the new
  grid shows.
- `AnimationPlayer` raises a new event when it restarts, pauses, resumes or seeks, so the transport
  follows without polling the clock's state; the time readout itself is refreshed on a UI timer
  while playing.

### Window Hidden

Hiding the window (tray) calls `AnimationPlayer.Stop()` and showing it `Sync` again, which plays from
0 (`GridPreview.cs:1321`). Proposed: unchanged, and **the pause state is kept** — a paused grid comes
back paused at 0.

---

## Transport

### Placement

The window's stack, top to bottom (`MainForm.cs:487-495`): options row, effects tabs, the preview
(with the layout strip left and the file explorer right), the global options row, the Global tabs,
the bottom bar (`_bottom`: Clear, status line, `_outputButtons` with ⚙, `⏱` length readout, Copy,
Save).

- The transport is a **new row docked between the preview and the Global tabs** (`_transportRow`) —
  added to `Controls` just before `_globalTabsRow`, so it docks innermost of the bottom stack, right
  under the preview, **across the whole window width** — under the layout strip and the file explorer
  too, like the Global rows (Q5, layout B of the mockup).
- The **`⏱` length readout moves into it** as its total, and leaves the bottom bar: the length is
  shown in one place. `RefreshLength` (`MainForm.cs:1842`) and its tooltip (`LengthDetail`) go with
  it; the readout keeps its `⏱` and its `⏱ —` without a length.
- Its columns: Play / Pause (auto-size), the timeline (100 %), the time readout `3.2 s / ⏱ 10.0 s`.

### Controls

- A **Play / Pause button** (▶ / ❚❚), `TabStop = false`, its click giving the focus back to the
  preview.
- A **timeline** (`PlaybackTimeline`): a custom-drawn control (a `TrackBar` cannot draw markers),
  never focused, its length the video length (the `RefreshLength` value), the elapsed part filled, a
  thumb at the grid time; 24 logical px tall whatever the row offers.
  - **Pressing** it **holds** the grid (`HoldPlayback`) and seeks under the pointer; **dragging** it
    scrubs: the cells follow the thumb (frames decoded at the player's 33 ms pace, the motions at the
    progress timer's), and **scrub audio** plays (§ Scrub Audio) — a click alone sounds one snippet.
    Released, the hold ends: the playback goes on from there if it was playing, stays paused otherwise.
- The time readout is empty without a length, the `⏱ —` alone showing.
- A **time readout** `3.2 s / ⏱ 10.0 s`, in the `Seconds` format: the current grid time, then the
  `⏱` length readout moved from the bottom bar (§ Placement).
- **No video length** (no image, a grid of stills without soundtrack nor motion — `⏱ —`): the
  transport stays in place, **disabled**, so nothing moves (proposed — same principle as the
  toolbars).
- **Locked while exporting** — like the global toolbar and the cell effects (`IsExporting`): the
  pause is the export's (§ Pause During Copy / Export).

### Cascade Turn Markers

While the Cascade is on, the timeline shows its schedule (`CascadeSchedule`): a **tick at the start
of each turn**, across the track (Q7, style 1 of the mockup). The pauses are not drawn: they are the
space between the end of a turn and the next tick. A turn starting at 0 gets its tick too.

### Keyboard

- **Space** toggles Play / Pause, taken in `MainForm.ProcessCmdKey` (`MainForm.cs:1090-1164`, before
  `default`) while the transport is enabled — with a video length, not exporting; otherwise it goes
  on to the focused control as before.
- Left to the focused control **only in a text field** (`_explorer.IsEditingText`,
  `IsEditingFrameField`, any `TextBoxBase`). A focused **button** does not keep its native Space click
  (Q6): Space is Play / Pause over it, so a Copy or a Save is never run again by accident.
- No conflict found: no `Keys.Space` handler exists in `UI/` today.

---

## Scrub Audio

While the timeline is dragged (Q2), short snippets of the **mix as it would be heard at the thumb's
time** play — editor-style.

- Route: the preview's own audio graph (`PreviewSound`, WinRT `AudioGraph`), not the export's
  `MixedSound` (which re-opens and decodes every file, and cannot start a Cascade mid-schedule).
- `AnimationPlayer.Scrub(t)`: seeks to `t`, then lets every voice heard at `t` play — `InTurn`
  evaluated at the clock, which stands on `t`; the trim's played part; the soundtrack's `LoopTime`; the
  Fade's gain at `t` — through the usual `PreviewSound.Sync`, while a **snippet** is on: the grid
  held, the sound plays as if running.
- The snippet (`_snippetTimer`, 15 ms ticks) ramps a `PreviewSound.ScrubGain` — a factor on the
  output, over the Fade's `MasterGain` — in over ~45 ms, holds ~90 ms, ramps out ~45 ms, then the
  voices stop as the grid stands. Each scrub step restarts the hold; a slow drag lets the voices run
  on, the 250 ms drift re-seek (`PreviewSound.cs:17,181`) bringing them back under the thumb.
- The grid released, a snippet still sounding ends at once.

---

## Pause During Copy / Export

The backlog row, joined to this workfile (Q1).

- Copy, Save and Copy for sharing all run through `MainForm.BeginExport` / `EndExport`
  (`MainForm.cs:2203` / `:2214`), `EndExport` in a `finally` on every path (success, cancel, failure,
  window closed mid-export). The preview **keeps playing** today; the export works on a snapshot
  (`GridExport.Job.Capture`), so pausing the preview never changes what is exported.
- `BeginExport` **holds** the playback, `EndExport` **releases** it, back to the state it had: a
  playing grid resumes, a paused one stays paused. A **hold counter** on the player — not a toggle —
  so the user's pause and the export's hold never cancel each other.
- The release comes **before** `EndExport`'s `Close()` (`_closeAfterExport`).
- The synchronous still path (`RenderStillAsync`, `MainForm.cs:2141-2144`, a grid with no animated
  image) is wrapped in the same hold, so the soundtrack and an Animations motion stop there too.
- While held, the Play / Pause button and Space are locked (`IsExporting`); the timeline shows the
  held position.

---

## Rules and Docs

- **RULES.md § Preview Playback** gets the transport: one clock, seek / pause on it, *starting over
  while paused stays paused at 0*, the export hold. A new consumer of the grid's time reads
  `GridTime` through the clock, never a `Stopwatch` of its own.
- **GLOSSARY.md / GLOSSARY.fr.md**: *Transport* (*barre de lecture*), *Timeline* (*ligne de temps*),
  *Grid time* (*temps de la grille*), *Scrub audio* (*extraits au glissement*).
- **README.md / README.fr.md**: the feature in § Features, Space in the shortcuts.
- **TODO-FEATURES.md**: the *Pause during Copy / export* row marked with this workfile (Q8) — done
  at design time, the backlog living next to the workfiles.
- **README.md / README.fr.md**: the `⏱` readout's description follows it into the transport row.

---

## Test Impact

The repository has **no test project** (`src/ImageGridFusion/ImageGridFusion.csproj` only): **no
unit test is created or updated**. The check is manual, in the running app — play / pause, Space,
seeking a single video, a Cascade of two videos (markers, turns, sound), a soundtrack with a Fade,
scrub audio, an MP4 export while playing and while paused.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | Not applicable — no test project |

---

## Open Questions

- [x] ~~Where does the transport go — A, B or C (§ Placement)?~~ → B: a row under the preview, the `⏱` readout moved into it
- [x] ~~A focused button and Space: does Space always toggle Play / Pause (text fields aside), or does a focused button keep its native click?~~ → Always Play / Pause, text fields aside
- [x] ~~Cascade turn markers: ticks only, or a segment per turn with the pauses shown as gaps?~~ → Ticks at the start of each turn
- [x] ~~Mark the backlog row *Pause during Copy / export* with this workfile?~~ → Yes, marked

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design from the user's request (navigate the whole video length — 10 s for two 5 s videos
in Cascade — and a Play / Pause button), the scoping answers Q1–Q4 and a single scout pass over the
clock, the sound, the window layout and the Copy / export flow.

- One pausable, seekable clock replacing `AnimationPlayer._clock`; the global pause kept apart from
  the Frames effect's `PausedAt`.
- Transport: Play / Pause, a custom timeline with the time readout, Space, Cascade turn markers;
  disabled without a video length; locked while exporting.
- Scrub audio through `PreviewSound` snippets.
- The backlog row *Pause during Copy / export* joined: a hold counter set by `BeginExport` /
  `EndExport` and around the synchronous still render.
- Proposed without a question (to confirm by reading): the transport disabled, not hidden, without a
  video length; the pause kept when the window is hidden and shown again.

### Iteration 2 — 2026-10-09

Answers Q5–Q8, from a mockup of three layouts and two marker styles.

- Placement B: the transport row under the preview, the `⏱` length readout moved into it — the
  bottom bar loses it.
- Space toggles Play / Pause over a focused button too; only text fields keep it.
- Cascade markers: a tick at the start of each turn, the pauses not drawn.
- The backlog row *Pause during Copy / export* marked with this workfile.

### Iteration 3 — 2026-10-09 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/playback-controls`,
branch `feature/playback-controls`). The scope is frozen as the sections above stand.

### Iteration 4 — 2026-10-09 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **The clock wraps at the loop's end** (`AnimationPlayer.WrapLoop`), not only the readout: at every
  loop the preview starts over as the exported video does, so the frame scrubbed at `t` and the frame
  played at `t` are the same. Before, the shorter contents drifted from the export after the first
  loop.
- **A seek re-aligns everything on the export**: offsets back to `-StartTime` — a content replayed
  alone by a Frames change too — and the soundtrack's start back to 0. `Restart` became `Seek(0)`.
- **Pressing the timeline holds the grid** for the whole drag, through the same hold counter as the
  export; a click alone sounds one snippet.
- **Scrub audio through the usual `Sync`**: no separate `PreviewSound.Snippet`; a `_snippet` flag
  lets the voices sound while the grid is held, and a `ScrubGain` factor on the output does the ramps.
  A slow drag hears the sound run on rather than a fixed 100 ms burst per step.
- **The transport row spans the whole window**, under the layout strip and the explorer, docked
  above the Global tabs.
- **The timeline has a fixed preferred height** (24 logical px): docked to fill an auto-sized row, it
  stretched the row to ~130 px.
- **Space with the transport disabled** goes on to the focused control as before.
- **The position readout is empty without a length**, `⏱ —` alone.
- **The paused progress timer stops** after the tick that draws the paused position; a playback
  change starts it again.
- Proposed in Iteration 1 and kept: the transport disabled, not hidden, without a length; the pause
  kept when the window is hidden (the grid comes back paused, at the position the clock holds).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-09 | `PlaybackClock`, `AnimationPlayer` (pause, holds, seek, wrap, scrub), `PreviewSound.ScrubGain`, `GridPreview` API; `PlaybackTimeline` + transport row and Space in `MainForm`; export hold — five commits |
| Unit tests | 3 | 2026-10-09 | Not applicable — no test project |
| RULES.md | 3 | 2026-10-09 | § Preview Playback › Transport; § Undo History (the pause out of it); § Video Length (the readout moved, the loop the preview wraps at) |
| README | 3 | 2026-10-09 | § Animated content › Transport, feature bullet, length readout moved — EN + FR |
| GLOSSARY | 3 | 2026-10-09 | Transport, Timeline, Grid time, Scrub audio; Length readout moved — EN + FR |
| TODO-FEATURES.md | 2 | 2026-10-09 | *Pause during Copy / export* row marked with this workfile |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Besides the timeline and Play / Pause, what is in scope? | Space = Play / Pause, Cascade turn markers, Pause during Copy / export. Not frame-by-frame nor Home | 2026-10-09 |
| 2 | What is heard while the timeline is dragged? | Scrub audio — short snippets, editor-style | 2026-10-09 |
| 3 | Paused, the grid starts over (image added / removed, Cascade changed): what happens? | Stays paused, at 0 | 2026-10-09 |
| 4 | Depth of the subject? | Straightforward — a single scout pass | 2026-10-09 |
| 5 | Where does the transport go — A, B or C? | B — a row under the preview, the `⏱` readout moved into it | 2026-10-09 |
| 6 | A focused button and Space? | Always Play / Pause, text fields aside | 2026-10-09 |
| 7 | Cascade turn markers: ticks or segments? | Ticks at the start of each turn | 2026-10-09 |
| 8 | Mark the backlog row with this workfile? | Yes | 2026-10-09 |

---

*Last updated: 2026-10-09*
