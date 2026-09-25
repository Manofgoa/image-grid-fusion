# Soundtrack

> Working document — force a soundtrack over the whole grid, mixed over the videos' own sound.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Let the user force a **soundtrack** on the grid: a **global effect** (not tied to a cell + image
pair), whose source can be a **video or an audio file**. It is **mixed over** the sound of the
grid's videos rather than replacing it. Each video's own sound can still be turned off
individually if the user wants.

Initial request (user, in French):

> Pouvoir forcer une bande son. Effet global. Ça peut être une vidéo ou un audio. Ça se met par
> dessus les sons des autres vidéos (mixage). On peut si on veut aller désactiver le son de
> chaque vidéo.

---

## Scoping

Questions the design depends on, split by who answers them cheapest.

| Question | Answered by |
|---|---|
| Where the soundtrack is heard: preview, exports, or both | the user |
| What happens when the soundtrack and the grid's duration differ | the user |
| Which volume controls the mix offers | the user |
| Expected depth of the exploration | the user |
| How the existing global effects (global fade, background) are exposed in the UI | the codebase |
| Whether per-video mute already exists, and how | the codebase |
| How audio is played in the preview and whether exports carry audio today | the codebase |

---

## Current State (scout pass, 2026-09-25)

Findings, inputs to the design — not decisions.

- **One sound per grid, no mixing.** `Animation.SoundSource` (`Composition/Animation.cs:31`) elects a
  single image: image 1 when it is a playing video with sound, else the first one in grid order; a
  frozen video has no sound. Every other video of the grid is silent, in the preview and in the
  exports.
- **Preview**: `UI/PreviewSound.cs` holds **one** WinRT `MediaPlayer` for that elected image
  (`Follow`, `Sync` against the frame position with a drift threshold), driven from
  `UI/AnimationPlayer.cs:96,120`. Its volume is capped at 100 %.
- **Exports**: Media Foundation only (no ffmpeg, no NAudio). `Imaging/VideoEncoder.Create(path,
  size, length, soundPath, soundLoop, soundStart)` writes the elected sound through its private
  `Sound` class (`VideoEncoder.cs:191`): one source reader, decoded to 16-bit PCM, encoded to AAC,
  interleaved ahead of the video. Callers: `GridExport.cs:50,117`, `CarouselExport.cs:40`. It
  already loops the sound (`soundLoop`). GIF and still exports carry no sound.
- **Global effects**: none exists in code yet. `workfiles/20260925-global-fade.md` (design stage)
  plans a **Global effects row** at the bottom of the window — one toggle per global effect, its
  options inline in the same row, enabled without a selected cell, disabled only when the effect
  does not apply, state not persisted, applied at grid level in the preview and every export.
- **Per-video mute**: not implemented. `workfiles/20260925-video-mute.md` (design stage, 7 open
  questions) plans it as a **cell effect** on `ImageLook`: volume slider 0–200 % + Mute checkbox.
- **Tests**: the solution has **no test project**.

---

## Soundtrack Effect

Agreed design (scoping answers, Q&A 1–3); the points still open are listed in `## Open Questions`.

| Aspect | Behaviour |
|---|---|
| Kind | **Global effect** — belongs to the grid, not to a cell + image pair |
| Toggle | A button of the **Global effects row** at the bottom of the window (planned by `global-fade.md`, created here if it does not exist yet); its options inline in the same row |
| File | A *Browse…* button in the options (audio and video files), and a file **dropped on the row**; the file's name is shown |
| Source | One file, **audio or video** (a video contributes its sound track only) |
| Where it is heard | **Preview and video exports**. GIF and still exports stay silent |
| Mix | Played **over** the sound of **every video with sound** in the grid, mixed together — never replacing it. A frozen video has no sound |
| Duration | The **grid's duration rules**: a shorter soundtrack **loops**, a longer one is **cut** |
| Volume | **One slider**, the soundtrack's volume, in the effect's options. The videos keep their own level |
| Per-video sound | Each video's own sound can be turned off individually — delivered by `workfiles/20260925-video-mute.md`, **out of this scope** |
| Persistence | Not persisted, like every effect |

### Mixing

- **Preview**: one `MediaPlayer` **per video with sound**, plus one for the soundtrack, each kept in
  step with the grid's position (each video looping with its own frames, the soundtrack looping on
  the grid's duration) with the drift correction of `PreviewSound`.
- **Exports**: `VideoEncoder` gains a mixing step — every source (each video's sound, the
  soundtrack) decoded to PCM at one common format, summed sample by sample (the soundtrack with its
  gain), clamped to the 16-bit range, then encoded to AAC as today. `Animation.SoundSource`'s single
  elected image becomes a list of sound sources.

---

## Test Impact

The solution has no test project (see Open Questions). Behaviours worth pinning, if one is created:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| PCM mix: sum with the soundtrack's gain, clamped to the 16-bit range | `ImageGridFusion.Tests/SoundMixTests.cs` | Create |
| Soundtrack shorter than the grid loops; longer is cut at the grid's duration | `ImageGridFusion.Tests/SoundMixTests.cs` | Create |
| Mix with only one side present (soundtrack only / video sound only) | `ImageGridFusion.Tests/SoundMixTests.cs` | Create |

---

## Open Questions

- [x] ~~Which video sound does the soundtrack mix over — the single elected sound of today
      (`SoundSource`), or **every** video with sound, mixed together (a much larger change)?~~
      → Every video with sound, mixed together
- [x] ~~Per-video mute: left to `workfiles/20260925-video-mute.md`, or absorbed into this workfile?~~
      → Left to `video-mute.md`; out of this scope
- [x] ~~Where does the soundtrack's toggle live — a **Global effects row** created here (the one
      `global-fade.md` plans), a button of the effects toolbar enabled without a selected cell, or
      the top bar?~~ → The Global effects row, created here if it does not exist yet
- [x] ~~How is the file picked — a *Browse* button in the options, and/or a file dropped on the row?~~
      → Both; the file name is shown
- [ ] A grid with **no animated content** (stills only) has no duration: is the effect disabled
      there, or does the export become a video lasting the soundtrack?
- [ ] Soundtrack volume range: 0–100 %, or 0–200 % like the planned per-video volume?
- [ ] Does the effects toolbar's *Reset* remove the soundtrack too, or only the cell effects?
- [ ] Tests: create a test project for the mixing logic, or verify manually only?
- [ ] Is every video with sound mixed **always** (the grid's sound rule changes for good), or only
      while the Soundtrack effect is active (today's single elected sound otherwise)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Initial design from the request and the scoping answers: a global Soundtrack effect, audio or video
source, heard in the preview and video exports, mixed over the grid's video sound, looped or cut to
the grid's duration, one volume slider. Scout pass (three read-only agents): today the grid has a
single elected sound and no mixing anywhere, no global effect exists in code, per-video mute is
still in design, and there is no test project. Eight open questions front-loaded.

### Iteration 2 — 2026-09-26

First batch of answers (Q&A 5–8): the soundtrack mixes over **every** video with sound, not only
today's single elected one — preview and export mixing widened to N sources; per-video mute stays
in `video-mute.md`, out of scope; the toggle lives in the Global effects row, created here if
needed; the file is picked with *Browse…* or dropped on the row. New open question: whether the
all-videos mix applies always or only while the soundtrack is active.

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
| 1 | Where is the soundtrack heard: preview, exports, or both? | Preview and video exports; GIF and still exports stay silent | 2026-09-25 |
| 2 | When the soundtrack and the grid's duration differ, what happens? | The grid's duration rules: a shorter soundtrack loops, a longer one is cut | 2026-09-25 |
| 3 | Which volume controls does the mix offer? | One soundtrack volume slider in the effect's options; videos keep their level, each one can be muted | 2026-09-25 |
| 4 | Is the subject expected to be straightforward, or tricky / long? | Straightforward — a single scout pass | 2026-09-25 |
| 5 | Which video sound does the soundtrack mix over? | Every video with sound, mixed together — not only today's elected one | 2026-09-25 |
| 6 | Per-video mute: here or in the video-mute workfile? | Left to `video-mute.md` | 2026-09-25 |
| 7 | Where does the soundtrack's toggle live? | The Global effects row planned by `global-fade.md`, created here if it does not exist yet | 2026-09-25 |
| 8 | How is the file picked? | A *Browse…* button in the options, and a file dropped on the row; the file name shown | 2026-09-25 |
| 9 | Grid with no animated content: disabled, or video lasting the soundtrack? | | 2026-09-25 |
| 10 | Soundtrack volume range? | | 2026-09-25 |
| 11 | Does *Reset* remove the soundtrack too? | | 2026-09-25 |
| 12 | Tests: create a test project, or manual verification only? | | 2026-09-25 |
| 13 | Is every video mixed always, or only while the soundtrack is active? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
