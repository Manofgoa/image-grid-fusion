# Global Fade

> Working document — a first **global effect**, **Fade**, applied to the whole grid rather than
> to one cell; it starts with the sound: a fade-in at the start and a fade-out at the end.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every effect today belongs to a **cell + image** pair (see [RULES.md](../RULES.md) § Effects).
The **Fade** is the first effect that belongs to the **grid**: it is a **global effect**.

It starts with the **sound** — the single sound track of the grid (image 1's sound, else the
first video with sound in grid order): the sound rises from silence over a duration at the
start and falls back to silence over the same duration at the end. A visual fade (to / from
black) may join it later; it is out of this workfile's scope.

It is heard **in the preview and in the MP4 export**.

This workfile also creates the **rule** separating global effects from cell effects, in
`RULES.md`, and the matching glossary term.

---

## UI — Global Effects Row

- A **dedicated row at the bottom** of the window, **just above the bottom bar** (Clear all /
  status line / Copy / Save), labelled **Global effects**, always visible, holding one toggle
  button per global effect — **Fade** only for now.
- **Clear all** removes the global effects too: back to the initial state.
- The global effect's options sit **in the same row**, right of its button (no separate options
  row).
- Fade options: **one duration**, applied to the fade-in and the fade-out alike.

```
┌─ top bar ────────────────────────────────────────────────────────────────┐
│ Effects  [Zoom][Rotate][Flip][Frames][B&W][Blur] [Reset]                   │
│ (options of the selected cell effect)                                      │
│                                                                            │
│                              grid preview                                  │
│                                                                            │
│ Global effects  [Fade]  Duration [──●──────] 1.0 s                         │
│ [Clear all]  status line …        [☐ Force as image] [Copy] [Save] [⚙]    │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Sound Fade — Behaviour

- **Envelope**: gain 0 → 1 over the duration **D** from the start, 1 → 0 over **D** before the
  end; 1 in between.
- **Export** (MP4): start = the video's time 0, end = the export's length (the longest loop).
  The sound keeps looping inside the export as today — only the export's own start and end fade.
- **Preview**: the fade follows the **grid's loop** — the export's length, the longest loop —
  so the preview sounds exactly like the export: when the sounded video is shorter than another
  content, its sound loops without fading inside the grid's loop, and fades only at the grid
  loop's start and end. (Today `PreviewSound.Sync` only knows the sounded video's own loop: the
  grid's loop length and the position in it must reach it.)

---

## Global Effect vs Effect — Rule to Create

A new section in `RULES.md`, next to § Effects, and a glossary entry. Draft, to be validated:

| | **Effect** (cell effect) | **Global effect** |
|---|---|---|
| Belongs to | A cell + image pair | The grid |
| UI | The effects toolbar (top), its options in the options toolbar | The **Global effects** row (bottom), its options in the same row |
| No cell selected | Disabled | Stays enabled; disabled only when it does not apply (e.g. no sound for the Fade) |
| Image replaced, cell *Reset* | Reset | Untouched |
| *Clear all* | Reset (no image left) | Reset |
| Swap, layout change | Kept, follows the image | Kept |
| Rendering | `Compositor.DrawCell` | At the grid level, in the preview and in every export |
| Persistence | Not persisted | Not persisted |

Glossary: **Global effect** — a transformation of the whole grid, toggled from the Global
effects row: Fade.

---

## Current State (explored)

| Topic | Where | What it does today |
|---|---|---|
| Preview sound | `UI/PreviewSound.cs:23-91` | A WinRT `MediaPlayer` (`IsLoopingEnabled = true`) on the sounded video's file; `Sync(position, playing)` plays / pauses it and re-seeks when it drifts by more than 250 ms. `Volume` (0–1) can be set at any time |
| Preview clock | `UI/AnimationPlayer.cs:194-254` | Per-image loop ticking every 33 ms; computes `loop` (the image's loop duration) and `time` (the position in it), then calls `_sound.Sync(time, playing)` for the sounded image — `loop` is not passed |
| Export sound | `Imaging/VideoEncoder.cs:191-370` (`Sound`) | The Source Reader decodes to **PCM 16-bit interleaved**, mono / stereo, 44.1 / 48 kHz; the Sink Writer re-encodes to AAC. The sound loops from its starting point and is cut exactly at the export length (`_length`) |
| Export seam | `VideoEncoder.cs:335-349` (`Sound.Write`) | Each buffer's `time` is already relative to the export start: the natural place to multiply a gain into the PCM before `WriteSample` |
| Export length | `Imaging/CarouselExport.cs:35-40` | `Carousel.Length(...)` → `VideoEncoder.Create(path, canvas, length, soundPath, soundLoop, soundStart)` |
| Tests | — | **No test project** in the repository |

---

## Test Impact

**No unit tests** for this workfile (Q&A 8): the repository has no test project, and none is
created. The fade's envelope stays a pure function, so a later test project can pin it.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~1. **Clear all** — does it also remove the global effects (back to the initial state), or keep them?~~ → Removes them, back to the initial state
- [ ] 2. **Glossary** — does "Effect" keep meaning a cell effect, with "Global effect" as a separate term (as drafted)?
- [x] ~~3. **Row position** — the Global effects row: its own row just above the bottom bar (as drafted), or inside the bottom bar?~~ → Its own row, just above the bottom bar
- [ ] 4. **No sound** — is the Fade button disabled when the grid has no sound (still images only, all videos silent or frozen)?
- [ ] 5. **Duration** — range, step and default (proposal: 0.1–5 s by 0.1 s, default 1 s)?
- [ ] 6. **Short content** — when 2 × D exceeds the length, is D clamped to half the length (fade-in then straight fade-out)?
- [x] ~~7. **Preview loop** — which loop does the preview fade on: the grid's loop (the export's length, faithful to the export), or the sounded video's own loop (differs when another content loops longer)?~~ → The grid's loop, faithful to the export
- [ ] 8. **Curve** — linear gain, or a smoother curve (e.g. squared, closer to perceived loudness)?
- [x] ~~9. **Tests** — create a test project (xUnit) to pin the envelope, or no unit tests for this workfile?~~ → No unit tests, no test project
- [ ] 10. **Toggle behaviour** — click toggles on / off; is the duration slider shown only while Fade is on, or always (disabled when off)?
- [ ] 11. **Export lock** — is the Global effects row locked while exporting, like the cell effects?
- [ ] 12. **Status of the rule** — does the new rule go into `RULES.md` as a new § Global Effects next to § Effects, with § Effects renamed "Cell effects"?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

- Scope: a **global effect** Fade, on the sound first; heard in the preview and in the export.
- UI: a dedicated **Global effects** row at the bottom, the options inline; one duration for
  both fades (Q&A 1–3).
- The user asked for a rule separating global effects from cell effects: drafted above, to land
  in `RULES.md` and `GLOSSARY.md` at implementation (pre-implementation gate).
- Exploration (one scout pass, Q&A 4): the preview sound is a `MediaPlayer` whose volume can be
  driven from the 33 ms animation tick; the export sound is decoded to PCM 16-bit before the AAC
  re-encoding, so the gain is multiplied into the samples. No test project exists.

### Iteration 2 — 2026-09-26

- OQ 1, 3, 7, 9 answered (Q&A 5–8): the preview fades on the **grid's loop**; the Global effects
  row sits **just above the bottom bar**; **Clear all** removes the global effects; **no unit
  tests**, no test project.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 2 | 2026-09-26 | Declined — no test project (Q&A 8) |
| README | | | |
| RULES.md / GLOSSARY.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the Fade, the first global effect, go? (group in the effects row / near Copy-Save / dedicated row) | A dedicated row, **at the bottom**, named **Global effects** | 2026-09-25 |
| 2 | Which settings for the sound fade? (fade in + fade out / one duration / checkboxes + one duration) | **One duration** | 2026-09-25 |
| 3 | Heard in the preview, or only in the MP4 export? | **Preview and export** | 2026-09-25 |
| 4 | Is the subject expected to be straightforward, or tricky / long? | **Straightforward** — one scout pass | 2026-09-25 |
| 5 | OQ 7 — Preview loop: the grid's loop or the sounded video's own loop? | **The grid's loop** | 2026-09-26 |
| 6 | OQ 3 — Row position: above the bottom bar, or inside it? | **Above the bottom bar** | 2026-09-26 |
| 7 | OQ 1 — Clear all: removes the global effects too? | **Yes**, back to the initial state | 2026-09-26 |
| 8 | OQ 9 — Tests: create a test project for the envelope? | **No tests** | 2026-09-26 |
| 9 | OQ 4 — No sound: Fade button disabled? | | 2026-09-26 |
| 10 | OQ 5 — Duration: range, step, default? | | 2026-09-26 |
| 11 | OQ 6 — Short content: D clamped to half the length? | | 2026-09-26 |
| 12 | OQ 10 — Duration slider: shown only while Fade is on, or always? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
