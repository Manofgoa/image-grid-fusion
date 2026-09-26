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

> **Superseded on 2026-09-26** (see Iteration 3): `video-mute.md` has since been delivered — the
> Volume cell effect (0–200 %, Mute) and the **mix of every heard video**, in the preview (an audio
> graph, each video at its volume up to 200 %, in step with its frames) and in the export
> (`VideoEncoder.Mixer`, every heard video looped with its video at its volume, clipped, one AAC
> track; `Animation.Heard`). `global-fade.md` has settled the Global effects row: just above the
> bottom bar, options inline, locked while exporting, **Clear all** removes the global effects,
> `RULES.md` gains a § Global Effects. The bullets below describe the code as it was before.

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
| Toggle | A button of the **Global effects row**, just above the bottom bar (designed by `global-fade.md`, created here if it has not landed yet); its options inline in the same row; locked while exporting |
| Removal | **Clear all** removes it, like every global effect (`global-fade.md`). The effects toolbar's *Reset* acts on cell effects only |
| File | A *Browse…* button in the options (audio and video files), and a file **dropped on the row**; the file's name is shown |
| Source | One file, **audio or video** (a video contributes its sound track only) |
| Where it is heard | **Preview and video exports**. GIF and still exports stay silent |
| Mix | One more source in the **existing mix of every heard video** (`Animation.Heard`) — played over them, never replacing them. The mix itself is unchanged |
| Duration | The **grid's duration rules**: a shorter soundtrack **loops**, a longer one is **cut** |
| Stills-only grid | With no animated content the grid has no duration of its own: the **soundtrack's length** becomes it. The preview loops the soundtrack; the export becomes an **MP4 video** as long as the soundtrack (the stills + the sound), where it would otherwise be a PNG |
| Volume | **One slider, 0–200 %**, default 100 %, the soundtrack's volume, in the effect's options — like the Volume cell effect. The videos keep their own level |
| Per-video sound | Each video's own sound is turned off or leveled by the **Volume** cell effect, already delivered (`video-mute.md`) — out of this scope |
| Persistence | Not persisted, like every effect |

### Mixing

- **Preview**: the soundtrack becomes **one more input of the existing audio graph**, at its
  volume, looping on the grid's duration and kept in step with the grid's position.
- **Exports**: the soundtrack becomes **one more source of `VideoEncoder.Mixer`**, looped on the
  grid's duration, at its gain, clipped with the rest.

---

## Test Impact

**No unit test** — declined by the user (Q&A 12): the solution has no test project, and the work is
verified manually, like `video-mute.md` and `global-fade.md`.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (manual verification: soundtrack heard over the videos in the preview and the MP4 export, looped / cut, volume 0–200 %, stills-only grid exported as a video) | — | — |

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
- [x] ~~A grid with **no animated content** (stills only) has no duration: is the effect disabled
      there, or does the export become a video lasting the soundtrack?~~ → The export becomes an MP4
      as long as the soundtrack; the preview loops it
- [x] ~~Soundtrack volume range: 0–100 %, or 0–200 % like the Volume cell effect (the preview's audio
      graph now supports it)?~~ → 0–200 %, default 100 %
- [x] ~~Does the effects toolbar's *Reset* remove the soundtrack too, or only the cell effects?~~
      → Neither asked nor needed: `global-fade.md`'s agreed rule applies — *Reset* acts on cell
      effects, **Clear all** removes the global effects
- [x] ~~Tests: create a test project for the mixing logic, or verify manually only (the choice made
      by `video-mute.md` and `global-fade.md`)?~~ → Manual verification only, no test project
- [x] ~~Is every video with sound mixed **always** (the grid's sound rule changes for good), or only
      while the Soundtrack effect is active (today's single elected sound otherwise)?~~
      → Moot: `video-mute.md` has delivered the mix of every heard video, always

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

### Iteration 3 — 2026-09-26

The second question batch was dismissed; meanwhile the codebase moved. `video-mute.md` is delivered:
every heard video is already mixed in the preview (audio graph, up to 200 %) and in the export
(`VideoEncoder.Mixer`). The soundtrack therefore becomes **one more source of the existing mix**
rather than a new mixing step; OQ on the always-on mix is moot. `global-fade.md` has settled the
Global effects row (above the bottom bar, locked while exporting, removed by Clear all), which
also settles the *Reset* question. Still open: stills-only grid, volume range, tests.

### Iteration 4 — 2026-09-26

Last answers (Q&A 9, 10, 12): a stills-only grid takes the soundtrack's length — the preview loops
it and the export becomes an MP4 of that length instead of a PNG; the volume slider runs 0–200 %,
default 100 %; no unit tests, manual verification. No open question left. Documentation in scope
if the go covers it: README, and `GLOSSARY.md` gains *Soundtrack* (plus the § Global Effects of
`RULES.md` if `global-fade.md` has not landed it yet).

### Iteration 5 — 2026-09-26 — ✅ Implemented

Go given for code, tests and documentation (Q&A 14). Branch gate: stays on `main`, the repo's
standing choice (no worktree requested).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 4 | 2026-09-26 | Declined by the user — no test project, manual verification (Q&A 12) |
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
| 9 | Grid with no animated content: disabled, or video lasting the soundtrack? | An MP4 as long as the soundtrack; the preview loops it | 2026-09-25 |
| 10 | Soundtrack volume range? | 0–200 %, default 100 % | 2026-09-25 |
| 11 | Does *Reset* remove the soundtrack too? | Not asked — settled by `global-fade.md` (Clear all removes global effects; *Reset* is cell-only) | 2026-09-25 |
| 12 | Tests: create a test project, or manual verification only? | Manual verification only | 2026-09-25 |
| 13 | Is every video mixed always, or only while the soundtrack is active? | Not asked — moot, `video-mute.md` delivered the always-on mix | 2026-09-26 |
| 14 | Start the implementation? | Code, tests and documentation | 2026-09-26 |

---

*Last updated: 2026-09-26*
