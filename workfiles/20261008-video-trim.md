# Video Trim

> Working document — choosing where a video or an animated GIF begins and ends, in the Frames tab.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The Frames effect today sets **where an animated image starts playing** (its *starting point*, one
slider of 100 positions for a video) or, frozen, **the frame it shows**. The whole content always
plays: its loop is the file's full length.

This task adds a **trim**: a **start** and an **end** chosen in the Frames tab, so that only the part
between them plays — in the preview, in the sound, in the exports, in the video length.

Each of the two bounds is set by:

- a **slider**;
- three **text fields**: **minutes**, **seconds**, and the **frame within the second**;
- in a field, **↑ / ↓** add / take **1**, and **5** with **Ctrl** held.

Components concerned (from the exploration):

| Component | Today | Role in the trim |
|---|---|---|
| `Composition/FramesEffect.cs` | `Position` (starting point, a share 0..1), `Frozen` | Holds the trim's start and end |
| `Composition/SourceImage.cs` | `StartPage`, `StartTime`, `Plays`, `IsFrozen` | Gives the played span to every consumer |
| `Composition/Animation.cs` | `LoopOf` reads `Pages.LoopDuration` | The loop becomes the trimmed length → `VideoLength` follows |
| `UI/AnimationPlayer.cs` | `LoopTime(position, pages.LoopDuration)` | Plays inside the span, loops back to its start |
| `UI/PreviewSound.cs` | `Sync(image, time, playing)` | Heard inside the span only |
| `Imaging/GridExport.cs`, `Imaging/VideoEncoder.cs` (`MixedSound`) | Loop = full length, `Start` = starting point | Frames and sound inside the span |
| `Imaging/VideoFrames.cs` | 100 positions, 1 % apart; duration known, **frame rate not read** | Gives its frame rate (`VideoEncodingProperties.FrameRate`) |
| `Imaging/GifFrames.cs` | Per-frame delays | Its frames are the trim's steps |
| `UI/MainForm.cs` | `_frames` slider, `_framesLabel`, `_freeze` | The new controls of the Frames options |

---

## Agreed Scope

Settled by the user at scoping (Q&A 1–4):

- **The starting point stays**, and **coexists** with the trim: the image loops from the start to
  the end, but begins playing at its starting point, which is kept **inside [start, end]**.
- **Overflow carries over**, like a clock: frame 24 of 25 + ↑ → frame 0 of the next second; second
  59 + ↑ → second 0 of the next minute; and back the same way with ↓. A value never leaves the
  content's bounds, and the start always stays before the end.
- **Applies to videos and animated GIFs**, counted in the content's own frames. The other animated
  contents (PDF of several pages, long text) do not get the trim: its controls are disabled there,
  with a tooltip saying why (RULES.md § Effects Toolbar).

---

## Trim Model

- The trim is part of the **Frames effect**, next to the starting point and *Freeze*: on / off,
  kept settings, *Reset* and the undo history follow the effect's rules (RULES.md § Effects) with
  nothing special — it is a value of `FramesEffect`, so the snapshot of `ImageLook` carries it.
- **Default state**: start at the first frame, end after the last one — the whole content plays,
  as today. *Reset* (the effect's own, the toolbar's) brings that back; a replaced image, or one
  shifting after a deletion, arrives untrimmed.
- **Stored in content time** (`TimeSpan` from the beginning of the file), not as a share: a video or
  a GIF never lays out again, and a time keeps the exact frame the user picked.
- **Minimum span**: one frame — the end is at least one frame after the start.
- **One definition of the played span** — `SourceImage` gives it (start, end, length) and every
  consumer reads it there, never `Pages.LoopDuration` directly for a trimmed content:
  - `Animation.LoopOf` → the **trimmed length**, so `Animation.VideoLength`, the length readout,
    the export length and the soundtrack's loop follow by themselves (RULES.md § Video Length);
  - the **preview** plays content time `start + LoopTime(position, length)`;
  - the **sound** of the video is heard inside the span only, looping with it, in the preview and
    in the MP4 (`MixedSound` gains the span's start);
  - the **exports** read frames inside the span.
- **Frozen**: a frozen image shows its starting point's frame, which is inside the span — the trim
  itself changes nothing else for a still.
- **Preview Playback**: a trim change replays **its own image only**, like any Frames change
  (RULES.md § Preview Playback) — no start-over of the grid.

---

## Frames Options — UI

Controls of the Frames options toolbar, at the toolbar's current height (the Background already
holds two lines there):

| Control | Behaviour |
|---|---|
| **Start slider** | One step per frame of the content, from the first frame to the end − 1 frame |
| **End slider** | One step per frame, from the start + 1 frame to the end of the content |
| **Minutes field** (each bound) | Minutes of the bound's time; not capped at 59 — a video longer than an hour shows `75` |
| **Seconds field** (each bound) | 0 to 59 |
| **Frame field** (each bound) | The frame within the second, from 0 |
| ↑ / ↓ in a field | +1 / −1 of that field's unit, carrying over (§ Agreed Scope); **Ctrl** → ±5 |
| Starting point slider, *Freeze* | Kept, as today; the starting point stays within [start, end] |

- **Acting on any of them activates the effect** (RULES.md § Options Toolbar).
- The slider and the three fields of a bound always show the same time: moving the slider rewrites
  the fields, a field's change moves the slider.
- Pushing a bound against the other stops it there (one frame apart); it does not push the other.

The labels, the exact layout and the fields' editing behaviour are in § Open Questions.

---

## Test Impact

The repository has **no test project** (`src/ImageGridFusion/ImageGridFusion.csproj` only): **no
unit test is created or updated**. The check is manual, in the running app — the trimmed loop in the
preview, its sound, the length readout, an MP4 and a GIF export.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project) | — | — |

---

## Open Questions

- [ ] **Frame field's rate**: what "a frame within the second" counts — the video's own frame rate
  (read from the file: 0..23 at 24 fps, 0..59 at 60 fps), or the export's fixed 30 fps grid? And
  for a GIF, whose frames have their own delays?
- [ ] **Starting point slider's range**: does it keep spanning the whole content (clamped into
  [start, end], its positions outside unreachable), or span the trimmed part only?
- [ ] **Layout** of the Frames options: where the two bounds, the starting point and *Freeze* sit in
  the options toolbar, and the bounds' labels.
- [ ] **Typing in a field**: is a typed value applied at each keystroke or on Enter / leaving the
  field (an invalid one reverted)? Does the **wheel** over a field act as ↑ / ↓?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request and the scoping answers (Q&A 1–4): a trim of the Frames effect —
start and end, each a slider and minutes / seconds / frame fields with ↑ / ↓ (±1, ±5 with Ctrl),
overflow carrying over; the starting point kept inside the span; videos and animated GIFs only.
The played span has one definition on `SourceImage`, read by the loop, the preview, the sound and
the exports, so the video length follows by itself. Stored in content time, one frame at least.
Four questions left open: the frame field's rate, the starting point slider's range, the layout,
the fields' editing.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the existing starting point relate to the new start / end? | Both coexist: the loop runs start → end, playback begins at the starting point, kept within [start, end] | 2026-10-08 |
| 2 | Overflow in the minutes / seconds / frames fields? | Carry over, like a clock; clamped to the content's bounds, start < end | 2026-10-08 |
| 3 | Which contents get the start / end? | Videos and animated GIFs, in their own frames | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-08 |
| 5 | Frame field's rate (video's own fps or export's 30 fps; GIF)? | | |
| 6 | Starting point slider's range (whole content or trimmed part)? | | |
| 7 | Layout of the Frames options and the bounds' labels? | | |
| 8 | Typing in a field (each keystroke or Enter / leave) and the wheel? | | |

---

*Last updated: 2026-10-08*
