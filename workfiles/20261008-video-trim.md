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
between them plays — in the preview, in the sound, in the exports, in the video length. The starting
point gets the same precise controls.

Each of the three times (start, end, starting point) is set by:

- a **slider**;
- text fields — for a **video**, three: **minutes**, **seconds**, and the **frame within the
  second**; for an **animated GIF**, one: the **frame number**;
- in a field, **↑ / ↓** (or the wheel) add / take **1**, and **5** with **Ctrl** held.

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

Settled by the user (Q&A 1–10):

- **The starting point stays**, and **coexists** with the trim: the image loops from the start to
  the end, but begins playing at its starting point, which is kept **inside [start, end]**.
- **Overflow carries over**, like a clock: frame 23 at 24 fps + ↑ → frame 0 of the next second;
  second 59 + ↑ → second 0 of the next minute; and back the same way with ↓. A value never leaves its
  allowed range (§ Frames Options — UI).
- **Applies to videos and animated GIFs**, counted in the **content's own frames**: a video's frame
  rate as the file gives it, a GIF's own frames. The other animated contents (PDF of several pages,
  long text) do not get the trim, nor the starting point's fields: those controls are disabled there,
  with a tooltip saying why (RULES.md § Effects Toolbar); their starting point slider stays as today.

---

## Trim Model

- The trim is part of the **Frames effect**, next to the starting point and *Freeze*: on / off,
  kept settings, *Reset* and the undo history follow the effect's rules (RULES.md § Effects) with
  nothing special — it is a value of `FramesEffect`, so the snapshot of `ImageLook` carries it.
- **Default state**: start at the first frame, end after the last one — the whole content plays,
  as today. *Reset* (the effect's own, the toolbar's) brings that back; a replaced image, or one
  shifting after a deletion, arrives untrimmed.
- **Stored as frame indices** — `FramesEffect.First` and `Last`, both played, `Last` null for the
  content's last frame — not as a share: a video or a GIF never lays out again, and an index keeps the
  exact frame the user picked.
- **Minimum span**: one frame — **To** may stand on **From**'s frame, never before it.
- **A video's pages are its frames**, at the file's frame rate (`VideoFrames`, 30 fps when the file
  gives none): before, 100 positions 1 % apart. So the starting point, the frozen frame and the trim
  stand on any frame. PDFs and texts keep their page positions.
- **One definition of the played span** — `SourceImage` gives it (start, end, length) and every
  consumer reads it there, never `Pages.LoopDuration` directly for a trimmed content:
  - `Animation.LoopOf` → the **trimmed length**, so `Animation.VideoLength`, the length readout,
    the export length and the soundtrack's loop follow by themselves (RULES.md § Video Length);
  - the **preview** plays content time `start + LoopTime(position, length)`;
  - the **sound** of the video is heard inside the span only, looping with it, in the preview
    (`PreviewSound.Span`: the audio node's own start and end times) and in the MP4 (`MixedSound.From`);
  - the **exports** read frames inside the span.
- **Frozen**: a frozen image shows its starting point's frame, which is inside the span — the trim
  itself changes nothing else for a still.
- **Preview Playback**: a trim change replays **its own image only**, like any Frames change
  (RULES.md § Preview Playback) — no start-over of the grid.

---

## Frames Options — UI

Layout **A** (Q&A 7), in the options toolbar's current height (the Background already holds two
lines there):

```
┌───────────────────────────────────────────┬────────────────────────────────────────────┐
│ From  [────■──────────]  [0]:[04]:[12]    │ Starts at [──■────────]  [0]:[06]:[00]     │
│ To    [──────────■────]  [0]:[21]:[00]    │ ☐ Freeze                                   │
└───────────────────────────────────────────┴────────────────────────────────────────────┘
   video: minutes : seconds : frame          GIF: one field, the frame number
```

| Control | Behaviour |
|---|---|
| **From slider** (start) | One step per frame, over the **whole content**; stopped at **To**'s frame |
| **To slider** (end) | One step per frame, over the **whole content**; the **last frame played**, stopped at **From**'s frame |
| Ctrl + wheel on a slider | 5 % of the frames it covers, onto the multiples of 5 % of its course |
| **Starts at slider** (starting point) | Its course covers the **trimmed part only**, [start, end], one step per frame (Q&A 6); it narrows when the trim does |
| *Freeze* | Kept, as today: holds the image on the starting point's frame |
| **Video fields** (each of the three) | **Minutes** — not capped at 59, a video over an hour shows `75`; **seconds** — 0 to 59; **frame** — the frame within the second, 0 to the file's frame rate − 1 |
| **GIF field** (each of the three) | One field: the **frame number** in the GIF, 1 to its frame count (the numbering of today's `3 / 12` label) |
| ↑ / ↓ in a field | +1 / −1 of that field's unit, carrying over between the video's fields (§ Agreed Scope); **Ctrl** → ±5 |
| Wheel over a field | As ↑ / ↓, Ctrl → ±5 |
| Typing a number | Applied on **Enter** or when the field is **left**; an invalid value is put back as it was; **Escape** puts the text back |
| ↑ / ↓ on minutes or seconds | The frame within the second kept, cut to the new second's frame count |
| Editing keys in a field | Ctrl+C / V / Z / Y, Delete and Escape edit the field's text, not the grid (`MainForm.ProcessCmdKey`) |
| PDF or text | The trim's lines disabled, their tooltip saying why; the starting point shows its page label instead of fields |

- **Acting on any of them activates the effect** (RULES.md § Options Toolbar).
- A slider and its fields always show the same time: moving the slider rewrites the fields, a
  field's change moves the slider.
- **Ranges**: **To** never stands before **From**; pushing a bound against the other stops it there,
  it does not push the other. The starting point stays inside [start, end]: a bound moved past it
  brings it along (`FramesEffect.WithTrim`).
- A thin line separates the trim (left) from the starting point and *Freeze* (right); the From / To
  captions, sliders and fields stand in a grid so they line up.
- The *Starts at* label is replaced by the fields (no `Starts at: 0:06` text left); with *Freeze*
  checked, the label of that line reads **Frozen on** instead of **Starts at**, as today.

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

- [x] ~~**Frame field's rate**: what "a frame within the second" counts — the video's own frame rate,
  or the export's fixed 30 fps grid? And for a GIF?~~ → The content's own frames: the video's frame
  rate read from the file; a GIF gets a single frame-number field (Q&A 5, 9)
- [x] ~~**Starting point slider's range**: the whole content, clamped, or the trimmed part only?~~ →
  The trimmed part only, one step per frame (Q&A 6)
- [x] ~~**Layout** of the Frames options and the bounds' labels?~~ → Layout A: From / To on two lines
  at the left, Starts at and Freeze at the right (Q&A 7)
- [x] ~~**Typing in a field**: each keystroke or Enter / leaving? The wheel?~~ → Enter or leaving the
  field, an invalid value put back; the wheel acts as ↑ / ↓ (Q&A 8)

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

### Iteration 2 — 2026-10-08

The four open questions answered (Q&A 5–8): the frame field counts the file's own frames (the video's
frame rate); the starting point slider covers the trimmed part only; layout A (From / To at the left,
Starts at and Freeze at the right), labels `From` / `To`; a typed value is applied on Enter or leaving
the field, the wheel acting as the arrows.

### Iteration 3 — 2026-10-08

User request (Q&A 9): for an animated GIF, **one field** — the frame number — replaces the three
minutes / seconds / frame fields.

### Iteration 4 — 2026-10-08

User request (Q&A 10): the **starting point gets the same fields** — three for a video, one for a
GIF — next to its slider, replacing the `Starts at: 0:06` label text. So it becomes frame-accurate on
a video or a GIF (one slider step per frame of the span); PDFs and texts keep their page slider.

### Iteration 5 — 2026-10-08 — ✅ Implemented

Go given: **code, unit tests and documentation**, in a **worktree** (`.claude/worktrees/video-trim`,
branch `feature/video-trim`, from `main`). The scope is the design sections above, as they stand.

### Iteration 6 — 2026-10-08 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state, or that it stated otherwise:

- **A video's pages became its frames** (`VideoFrames`, at the file's frame rate, 30 fps when it
  gives none or an absurd one): the starting point, Freeze and the trim needed frame-accurate pages —
  they were 100 positions 1 % apart.
- **Frame indices, not times**: the trim is stored as `FramesEffect.First` / `Last` (frame indices)
  instead of content time — exact, and no rounding between a time and its frame.
- **To is the last frame played** (inclusive), so a GIF reads `From 1 To 12` and a video's To shows
  that frame's time; the minimum span is one frame, To standing on From's frame.
- **Both trim sliders cover the whole content** (not From up to end − 1, To from start + 1): one scale
  for both, a bound stopped against the other.
- **Ctrl + wheel** on the three sliders: 5 % of the frames the slider covers (the starting point's
  rule before, now over its trimmed course).
- **Preview sound**: looped inside the trim by the audio node's own `StartTime` / `EndTime`
  (`PreviewSound.Span`); a part Windows refuses leaves the whole sound looping.
- **Fields**: Escape puts the typed text back; ↑ / ↓ on minutes or seconds keep the frame within the
  second, cut to the new second's frame count; a focused field keeps Ctrl+C / V / Z / Y, Delete and
  Escape (`MainForm.ProcessCmdKey`) — Delete would else remove the selected image.
- **PDF and text**: the trim's lines are disabled with a tooltip on them; the starting point keeps its
  page label instead of fields.
- **Layout**: the From / To rows in a grid, so their sliders line up; a thin line between the trim and
  the starting point.
- **RULES.md** gets a § Video Length › Played Part (the one definition of what a content plays).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5, 6 | 2026-10-08 | Video pages per frame; trim model (`FramesEffect`, `SourceImage`, `Animation`, `AnimationPlayer`, `PreviewSound`, `GridExport`, `VideoEncoder`); `FrameField` + Frames options in `MainForm` — four commits |
| Unit tests | 5 | 2026-10-08 | Not applicable — no test project |
| README | 5 | 2026-10-08 | `README.md` + `README.fr.md`; also `GLOSSARY.md` + `GLOSSARY.fr.md` (Trim / Découpe), `RULES.md` (§ Played Part) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the existing starting point relate to the new start / end? | Both coexist: the loop runs start → end, playback begins at the starting point, kept within [start, end] | 2026-10-08 |
| 2 | Overflow in the minutes / seconds / frames fields? | Carry over, like a clock; clamped to the content's bounds, start < end | 2026-10-08 |
| 3 | Which contents get the start / end? | Videos and animated GIFs, in their own frames | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-08 |
| 5 | Frame field's rate (video's own fps or export's 30 fps; GIF)? | The file's own frames: the video's frame rate; a GIF's own frames | 2026-10-08 |
| 6 | Starting point slider's range (whole content or trimmed part)? | The trimmed part only | 2026-10-08 |
| 7 | Layout of the Frames options and the bounds' labels? | Layout A — From / To on two lines at the left, Starts at and Freeze at the right | 2026-10-08 |
| 8 | Typing in a field (each keystroke or Enter / leave) and the wheel? | Enter or leaving the field, invalid put back; the wheel acts as ↑ / ↓ | 2026-10-08 |
| 9 | *(user, unprompted)* GIF fields | One field instead of the three for a GIF | 2026-10-08 |
| 10 | *(user, unprompted)* Starting point fields | The starting point also gets the three fields (one for a GIF) | 2026-10-08 |

---

*Last updated: 2026-10-08*
