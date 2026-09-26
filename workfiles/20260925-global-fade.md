# Global Fade

> Working document — a first **global effect**, **Fade**, applied to the whole grid rather than
> to one cell; it starts with the sound: a fade-in at the start and a fade-out at the end.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every effect today belongs to a **cell + image** pair (see [RULES.md](../RULES.md) § Effects).
The **Fade** is the first effect that belongs to the **grid**: it is a **global effect**.

It starts with the **sound** — the grid's **mix** (every heard video, each at its Volume
effect, since `video-mute.md`): the mix rises from silence over a duration at the start and
falls back to silence over the same duration at the end. A visual fade (to / from
black) may join it later; it is out of this workfile's scope.

It is heard **in the preview and in the MP4 export**.

This workfile also creates the **rule** separating global effects from cell effects, in
`RULES.md`, and the matching glossary term.

---

## UI — Global Effects Row

- A **dedicated row at the bottom** of the window, **just above the bottom bar** (Clear all /
  status line / Copy / Save), labelled **Global effects**, always visible.
- It follows the **cell-effect model** of `RULES.md` (tabs aside): each global effect has an
  **activation checkbox** — **☑ Fade** only for now — followed by its options, **always shown**,
  in the same row (no separate options row).
  - Its **settings and its on / off state are independent**: unchecked, it keeps its settings and
    shows them in its options; it is heard as its default (no fade) until checked again.
  - **Acting on any option turns it on** (the checkbox gets checked) before applying the change.
  - A **Reset** button ends the row: it brings the Fade back to its **default state** — 1 s,
    Squared, **off**.
- Fade options: **one duration**, applied to the fade-in and the fade-out alike — a slider from
  **0.1 s to 5 s by 0.1 s**, **1 s** by default, with its value shown (`1.0 s`).
- A **curve** choice, two exclusive buttons after the slider: **Squared** (default — gain x²,
  heard as a steady rise) and **Linear**.
- **Not applicable** when **nothing is heard** (no video, or every video muted, frozen or without
  a sound track): its checkbox and options are disabled, the checkbox's tooltip saying why. Its
  state is kept and applies again as soon as a sound is heard.
- The row is **locked while exporting**, like the cell effects: the export keeps the setting it
  started with.
- **Clear all** removes the global effects too: back to their default state.

```
┌─ top bar ────────────────────────────────────────────────────────────────┐
│ Effects  [Zoom][Rotate][Flip][Frames][B&W][Blur] [Reset]                   │
│ (options of the selected cell effect)                                      │
│                                                                            │
│                              grid preview                                  │
│                                                                            │
│ Global effects  [☑ Fade]  Duration [──●────] 1.0 s  [Squared][Linear] [Reset] │
│ [Clear all]  status line …        [☐ Force as image] [Copy] [Save] [⚙]    │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Sound Fade — Behaviour

- **Envelope**: gain 0 → 1 over the duration **D** from the start, 1 → 0 over **D** before the
  end; 1 in between. Along the ramp, with *x* the ramp's progress from 0 to 1: gain = *x*²
  (Squared) or *x* (Linear).
- **Short content**: when 2 × D exceeds the length, the effective D is **half the length** — the
  sound rises then falls straight away, never above its normal volume.
- **What fades**: the **whole mix** — every heard video, and the soundtrack
  (`soundtrack.md`) once it exists — at the mix's output, after the voices are summed.
- **Export** (MP4): start = the video's time 0, end = the export's length (the longest loop).
  The sounds keep looping inside the export as today — only the export's own start and end fade.
- **Preview**: the fade follows the **grid's loop** — the export's length, the longest loop —
  so the preview sounds exactly like the export: a sound shorter than the grid's loop keeps
  looping without fading inside it, and the mix fades only at the grid loop's start and end.
  (Today `PreviewSound` only knows each video's own position: the grid's loop length and the
  position in it must reach it.)

---

## Global Effect vs Effect — Rule to Create

In `RULES.md`: the current § Effects is **renamed § Cell Effects**, and a new **§ Global Effects**
follows it, holding this table and the Global effects row's rules (row above the bottom bar,
activation checkbox, options inline and always shown, settings kept when off, acting on an
option turns it on, Reset at the end of the row, disabled with a tooltip when not applicable,
locked while exporting):

| | **Effect** (cell effect) | **Global effect** |
|---|---|---|
| Belongs to | A cell + image pair | The grid |
| UI | The effects toolbar (top), its options in the options toolbar | The **Global effects** row (bottom), its options in the same row |
| No cell selected | Disabled | Stays enabled; disabled only when it does not apply (e.g. nothing heard for the Fade) |
| Image replaced, cell *Reset* (both) | Reset | Untouched |
| Its own *Reset* | The options row's Reset | The Reset ending the Global effects row |
| *Clear all* | Reset (no image left) | Reset |
| Swap, layout change | Kept, follows the image | Kept |
| Rendering | `Compositor.DrawCell` | At the grid level, in the preview and in every export |
| Persistence | Not persisted | Not persisted |

Glossary (`GLOSSARY.md`): **Effect** keeps meaning the cell effect (noted "also *cell
effect*"); new terms:

- **Global effect** — a transformation of the whole grid, turned on or off from the Global
  effects row: Fade. Turned off, it keeps its settings.
- **Global effects row** — the always-visible row just above the bottom bar: the "Global
  effects" label, each global effect's activation checkbox and options, the Reset button.

---

## Current State (explored)

Re-explored 2026-09-26, after `video-mute.md` was delivered (the first pass described a single
elected sound, now gone).

| Topic | Where | What it does today |
|---|---|---|
| Preview sound | `UI/PreviewSound.cs` | A Windows **`AudioGraph`**: one input node per video with sound, at its Volume gain (`OutgoingGain`), kept in step with its frames by `Sync(image, position, playing)`; one **device output node** (`_output`) — its `OutgoingGain` can carry the fade for the whole mix |
| Preview clock | `UI/AnimationPlayer.cs:26-170` | Calls `_sound.Sync(image, time, playing)` per video every tick; the grid loop's length and position are not passed to the sound yet |
| Export sound | `Imaging/VideoEncoder.cs:197-330` (`Mixer`) | Every voice decoded to PCM 16-bit, **summed** into a mix buffer, clamped to 16-bit (`_clipped`), then encoded to AAC; `_frames` / `_lengthFrames` give each step's position against the export length |
| Export seam | `VideoEncoder.cs:280-296` (`Mixer.WriteUntil`) | Multiply the summed mix by the fade gain **before the clamp**: one place, for every voice at once |
| Export length | `VideoEncoder.cs:45` | `Mixer.Create(sounds, length.Ticks, failed)` — the export length already reaches the mixer |
| Soundtrack | `workfiles/20260925-soundtrack.md` (design) | A second global effect, a forced sound track, planned in **this** Global effects row, created by whichever lands first |
| Tests | — | **No test project** in the repository |
| Cell-effect UI | `RULES.md` § Effects (revised) | Effect **tabs** with an activation checkbox; settings **kept when off**; options always shown; **acting on an option turns the effect on**; a per-effect *Reset* ends the options row; a non-applicable effect has its checkbox disabled with a tooltip saying why |

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
- [x] ~~2. **Glossary** — does "Effect" keep meaning a cell effect, with "Global effect" as a separate term (as drafted)?~~ → Yes, "Effect" stays the cell effect; "Global effect" is a separate term
- [x] ~~3. **Row position** — the Global effects row: its own row just above the bottom bar (as drafted), or inside the bottom bar?~~ → Its own row, just above the bottom bar
- [x] ~~4. **No sound** — is the Fade button disabled when the grid has no sound (still images only, all videos silent or frozen)?~~ → Disabled; a Fade already on keeps its setting *(refined 2026-09-26, see Iteration 6: when nothing is heard)*
- [x] ~~5. **Duration** — range, step and default (proposal: 0.1–5 s by 0.1 s, default 1 s)?~~ → 0.1–5 s by 0.1 s, default 1 s
- [x] ~~6. **Short content** — when 2 × D exceeds the length, is D clamped to half the length (fade-in then straight fade-out)?~~ → Clamped to half the length
- [x] ~~7. **Preview loop** — which loop does the preview fade on: the grid's loop (the export's length, faithful to the export), or the sounded video's own loop (differs when another content loops longer)?~~ → The grid's loop, faithful to the export
- [x] ~~8. **Curve** — linear gain, or a smoother curve (e.g. squared, closer to perceived loudness)?~~ → A UI choice: Squared / Linear buttons in the row
- [x] ~~9. **Tests** — create a test project (xUnit) to pin the envelope, or no unit tests for this workfile?~~ → No unit tests, no test project
- [x] ~~10. **Toggle behaviour** — click toggles on / off; is the duration slider shown only while Fade is on, or always (disabled when off)?~~ → Shown only while Fade is on *(revised 2026-09-26, see Iteration 6: options always shown, cell-effect model)*
- [x] ~~11. **Export lock** — is the Global effects row locked while exporting, like the cell effects?~~ → Locked
- [x] ~~13. **Control model** — does the Global effects row follow the revised cell-effect model (activation checkbox, settings kept when off, options always shown, acting on an option turns it on), replacing the toggle with options shown only while on (OQ 10)?~~ → Yes, the cell-effect model
- [x] ~~14. **What fades** — the whole mix (every heard video, and the soundtrack once it exists), or the videos only?~~ → The whole mix
- [x] ~~15. **Not applicable** — Fade disabled when **nothing is heard** (every video muted, frozen or silent), or only when no video has a sound track?~~ → When nothing is heard
- [x] ~~16. **Reset** — a *Reset* button for the Fade in the row, like the options row's per-effect Reset, or none (Clear all only)?~~ → Yes, ending the row
- [x] ~~12. **Status of the rule** — does the new rule go into `RULES.md` as a new § Global Effects next to § Effects, with § Effects renamed "Cell effects"?~~ → Yes: new § Global Effects, § Effects renamed § Cell Effects

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

### Iteration 3 — 2026-09-26

- OQ 4, 5, 6, 10 answered (Q&A 9–12): Fade **disabled without sound**; duration **0.1–5 s by
  0.1 s, default 1 s**; D **clamped to half** a short length; the slider shows **only while Fade
  is on**.

### Iteration 4 — 2026-09-26

- OQ 2, 8, 11, 12 answered (Q&A 13–16): the curve is a **UI choice**, Squared / Linear; the row
  is **locked while exporting**; `RULES.md` gets a **§ Global Effects**, § Effects becoming
  **§ Cell Effects**; "Effect" stays the cell effect in the glossary, "Global effect" added.
- Default curve set to **Squared** (the answer made the curve a choice without naming a default).
- No open question left.

### Iteration 5 — 2026-09-26

- The context moved under the design: `video-mute.md` was delivered (the grid's sound is now a
  **mix** of the heard videos, previewed through an `AudioGraph`, exported through a `Mixer`), and
  `RULES.md` § Effects was revised (tabs, activation checkbox, settings kept when off, acting on
  an option turns the effect on). `soundtrack.md` counts on this Global effects row.
- Current State re-explored directly (a single question); the fade applies to the **mix**.
- The go question was dismissed; new open questions 13–16 raised by these changes.

### Iteration 6 — 2026-09-26

- OQ 13–16 answered (Q&A 18–21): the Global effects row follows the **cell-effect model**
  (activation checkbox, settings kept when off, options always shown, acting on an option turns
  it on) — this revises OQ 10; the **whole mix** fades; **not applicable when nothing is heard**,
  checkbox disabled with a tooltip; a **Reset** ends the row.
- No open question left.

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
| 9 | OQ 4 — No sound: Fade button disabled? | **Disabled** | 2026-09-26 |
| 10 | OQ 5 — Duration: range, step, default? | **0.1–5 s by 0.1 s, 1 s** | 2026-09-26 |
| 11 | OQ 6 — Short content: D clamped to half the length? | **Clamped to half** | 2026-09-26 |
| 12 | OQ 10 — Duration slider: shown only while Fade is on, or always? | **Only while on** | 2026-09-26 |
| 13 | OQ 8 — Curve: linear or smoother? | **A UI choice**: Squared / Linear | 2026-09-26 |
| 14 | OQ 11 — Export lock: Global effects row locked while exporting? | **Locked** | 2026-09-26 |
| 15 | OQ 12 — Rule placement: new § Global Effects, § Effects renamed "Cell effects"? | **Yes, both** | 2026-09-26 |
| 16 | OQ 2 — Glossary: "Effect" stays the cell effect, "Global effect" a separate term? | **Yes** | 2026-09-26 |
| 17 | Go for implementation? | Dismissed — asked to re-ask the questions as MCQ | 2026-09-26 |
| 18 | OQ 13 — Control model: follow the revised cell-effect model? | **Yes** | 2026-09-26 |
| 19 | OQ 14 — What fades: the whole mix, or the videos only? | **The whole mix** | 2026-09-26 |
| 20 | OQ 15 — Not applicable: when nothing is heard, or when no sound track? | **Nothing heard** | 2026-09-26 |
| 21 | OQ 16 — Reset button for the Fade in the row? | **Yes, ending the row** | 2026-09-26 |

---

*Last updated: 2026-09-26*
