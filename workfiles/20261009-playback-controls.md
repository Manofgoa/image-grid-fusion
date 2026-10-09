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
- The **grid time shown** is `LoopTime(GridTime, VideoLength)`: the timeline wraps with the grid's
  loop, as the export does.
- **Seek(t)** sets the clock to `t ∈ [0, VideoLength)`. Every content follows by itself — they all
  apply `LoopTime` downstream; a Cascade content lands before, in or after its turn as the schedule
  says. `_soundtrackStart` and the playbacks' `Offset` stay whole seconds of the **same** clock, so a
  seek moves them all together.
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

Proposed layouts — see Open Questions (mockup shown in the conversation). The window's stack, top
to bottom (`MainForm.cs:487-495`): options row, effects tabs, the preview (with the layout strip
left and the file explorer right), the global options row, the Global tabs, the bottom bar
(`_bottom`: Clear, status line, `_outputButtons` with ⚙, `⏱` length readout, Copy, Save).

| | Where | Note |
|---|---|---|
| A | A new row docked between the preview and the global options row | Next to what it plays; the `⏱` readout stays in the bottom bar |
| B | Same row as A, the `⏱` readout **moved into it** as its total | One place for the length (`RefreshLength`, `MainForm.cs:1842`, its tooltip `LengthDetail` with it) |
| C | Inside the bottom bar, in place of part of the status line | Cheap, but competes with the status text and Cancel |

### Controls

- A **Play / Pause button** (▶ / ❚❚), `TabStop = false`, its click giving the focus back to the
  preview.
- A **timeline**: a custom-drawn control (a `TrackBar` cannot draw markers), its length the video
  length (the `RefreshLength` value), the elapsed part filled, a thumb at the grid time.
  - **Dragging** it seeks: the cells follow the thumb (frames and motion, decode throttled to the
    paint rate), and **scrub audio** plays (§ Scrub Audio). Released, the playback goes on from there
    if it was playing, stays paused otherwise.
  - A **click** on the track seeks there.
- A **time readout** `3.2 s / 10.0 s`, in the `Seconds` format of the `⏱` readout.
- **No video length** (no image, a grid of stills without soundtrack nor motion — `⏱ —`): the
  transport stays in place, **disabled**, so nothing moves (proposed — same principle as the
  toolbars).
- **Locked while exporting** — like the global toolbar and the cell effects (`IsExporting`): the
  pause is the export's (§ Pause During Copy / Export).

### Cascade Turn Markers

While the Cascade is on, the timeline shows its schedule (`CascadeSchedule`): a mark at the start
of each turn, and each pause visible between two turns. Style: see Open Questions.

### Keyboard

- **Space** toggles Play / Pause, taken in `MainForm.ProcessCmdKey` (`MainForm.cs:1090-1164`, before
  `default`), not while exporting.
- Left to the focused control when it needs the key: a text field (`_explorer.IsEditingText`,
  `IsEditingFrameField`, any `TextBoxBase`). Whether a focused **button** keeps its native Space click:
  see Open Questions.
- No conflict found: no `Keys.Space` handler exists in `UI/` today.

---

## Scrub Audio

While the timeline is dragged (Q2), short snippets of the **mix as it would be heard at the thumb's
time** play — editor-style.

- Route: the preview's own audio graph (`PreviewSound`, WinRT `AudioGraph`), not the export's
  `MixedSound` (which re-opens and decodes every file, and cannot start a Cascade mid-schedule).
- `PreviewSound.Snippet(t)`: for each voice heard at `t` — `InTurn` evaluated **at `t`**, the trim's
  played part, the soundtrack's `LoopTime` — `node.Seek(target)`, `Start()`, then `Stop()` after
  ~100 ms (a one-shot timer, restarted at each scrub step), the Fade's gain at `t` as `MasterGain`.
- A short gain ramp in and out of each snippet avoids clicks; the soundtrack timer and the 250 ms drift
  re-seek (`PreviewSound.cs:17,181`) are suspended while dragging, and a snippet never leaves a voice
  marked playing.

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
- **TODO-FEATURES.md**: the *Pause during Copy / export* row marked with this workfile — see Open
  Questions.

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

- [ ] Where does the transport go — A, B or C (§ Placement)?
- [ ] A focused button and Space: does Space always toggle Play / Pause (text fields aside), or does a focused button keep its native click?
- [ ] Cascade turn markers: ticks only, or a segment per turn with the pauses shown as gaps?
- [ ] Mark the backlog row *Pause during Copy / export* with this workfile?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| RULES.md | | | |
| README | | | |
| GLOSSARY | | | |
| TODO-FEATURES.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Besides the timeline and Play / Pause, what is in scope? | Space = Play / Pause, Cascade turn markers, Pause during Copy / export. Not frame-by-frame nor Home | 2026-10-09 |
| 2 | What is heard while the timeline is dragged? | Scrub audio — short snippets, editor-style | 2026-10-09 |
| 3 | Paused, the grid starts over (image added / removed, Cascade changed): what happens? | Stays paused, at 0 | 2026-10-09 |
| 4 | Depth of the subject? | Straightforward — a single scout pass | 2026-10-09 |
| 5 | Where does the transport go — A, B or C? | | |
| 6 | A focused button and Space? | | |
| 7 | Cascade turn markers: ticks or segments? | | |
| 8 | Mark the backlog row with this workfile? | | |

---

*Last updated: 2026-10-09*
