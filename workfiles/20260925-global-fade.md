# Global Fade

> Working document — a **global effect**, **Fade**, applied to the whole grid rather than
> to one cell; it starts with the sound: a fade-in at the start and a fade-out at the end.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Cell effects belong to a **cell + image** pair ([RULES.md](../RULES.md) § Effects); **global
effects** belong to the grid (§ Global Effects) — **Soundtrack** and **Borders** exist. The
**Fade** is a new global effect.

It starts with the **sound** — the grid's **mix** (every heard video at its Volume, and the
soundtrack at its level): the mix rises from silence over a duration at the start and falls back
to silence over the same duration at the end. A visual fade (to / from black) may join it later;
it is out of this workfile's scope.

It is heard **in the preview and in the MP4 export**.

The rule separating global effects from cell effects, drafted here, **has landed** in `RULES.md`
§ Global Effects with `soundtrack.md` and `global-effects-tabs.md`; this workfile only adds the
Fade to it.

---

## UI — Fade Tab

The Fade follows `RULES.md` § Global Effects Toolbar as it stands: a **tab** of the global effects
toolbar, with its **activation checkbox**, its options in the **global options toolbar** below.

- **Tab**: **☑ Fade**, between **Soundtrack** and **Borders** — the two sound effects side by
  side (`GlobalEffect { Soundtrack, Fade, Borders }`). Named "Fade", not "Sound fade": a later
  image fade joins it as options of the same tab.
- **Options**, left to right in the global options toolbar:
  - **Duration**: one duration, applied to the fade-in and the fade-out alike — a slider from
    **0.1 s to 5 s by 0.1 s**, **1 s** by default, its value shown (`1.0 s`).
  - **Curve**: two exclusive **icon buttons drawing the shape of the fade** (rise, hold, fall),
    their name in a tooltip — **Squared** (default — gain x², heard as a steady rise) and
    **Linear**. The pressed one is the curve in use.
  - The Fade's own **Reset**, ending the toolbar.

```
│ Duration [──●──────] 1.0 s   [╭─╮][/‾\]                                     [↻ Reset] │
│ Global effects  ☑ Soundtrack │ ☑ Fade │ ☐ Borders                              [Reset] │
│ [Clear all]  status line …                                    [Copy] [Save] [⚙]      │
```
- Everything else comes from the rule: settings **kept when off** and shown in the options,
  **acting on an option turns it on**, the tabs' **Reset** and **Clear all** bring back the
  initial state (off, 1 s, Squared), enabled with no cell selected, **locked while exporting**.
- **Not applicable** when **nothing is heard** — no heard video and no soundtrack on (or at 0 %):
  its checkbox and options are disabled, the checkbox's tooltip saying why. Its state is kept and
  applies again as soon as a sound is heard.

---

## Sound Fade — Behaviour

- **Envelope**: gain 0 → 1 over the duration **D** from the start, 1 → 0 over **D** before the
  end; 1 in between. Along the ramp, with *x* the ramp's progress from 0 to 1: gain = *x*²
  (Squared) or *x* (Linear).
- **Length**: the length *L* the fade ends on is the **video length**, `Animation.VideoLength`
  (RULES.md § Video Length) — in the export (`GridExport.Job.Length`, already handed to the
  mixer) and in the preview (the loop `AnimationPlayer` already computes for the soundtrack) — never
  re-derived.
- **Short content**: when 2 × D exceeds the length, the effective D is **half the length** — the
  sound rises then falls straight away, never above its normal volume.
- **What fades**: the **whole mix** — every heard video and the soundtrack — at the mix's
  output, after the voices are summed.
- **Export** (MP4): start = the video's time 0, end = the export's length (the longest loop, or
  the soundtrack's length for a grid of stills).
  The sounds keep looping inside the export as today — only the export's own start and end fade.
- **Preview**: the fade follows the **grid's loop** — the export's length, the longest loop —
  so the preview sounds exactly like the export: a sound shorter than the grid's loop keeps
  looping without fading inside it, and the mix fades only at the grid loop's start and end. The
  grid's clock already reaches `PreviewSound` for the soundtrack (`SyncSoundtrack`).

---

## Rule and Glossary

- `RULES.md` § Global Effects already holds the distinction table and the toolbar's rules; the
  Fade needs **no new rule**, and § Effects **keeps its name** (the rename of Q&A 15 is dropped).
- `GLOSSARY.md`: **Fade** added to the *Global effect* list, and a **Fade** entry — the global
  effect fading the grid's sound in at the start and out at the end, over one duration, on a
  Squared or Linear curve.

---

## Current State (explored)

Re-explored 2026-09-26, after `video-mute.md` was delivered (the first pass described a single
elected sound, now gone), and 2026-09-30, after the soundtrack, the borders and the global effects
tabs landed.

| Topic | Where | What it does today |
|---|---|---|
| Preview sound | `UI/PreviewSound.cs` | A Windows **`AudioGraph`**: one input node per video with sound, at its Volume gain (`OutgoingGain`), kept in step with its frames by `Sync(image, position, playing)`; one **device output node** (`_output`) — its `OutgoingGain` can carry the fade for the whole mix |
| Preview clock | `UI/AnimationPlayer.cs` | Calls `_sound.Sync(image, time, playing)` per video every tick, and `SyncSoundtrack(position, gain)` on the **grid's clock** |
| Export sound | `Imaging/VideoEncoder.cs:197-330` (`Mixer`) | Every voice decoded to PCM 16-bit, **summed** into a mix buffer, clamped to 16-bit (`_clipped`), then encoded to AAC; `_frames` / `_lengthFrames` give each step's position against the export length |
| Export seam | `VideoEncoder.cs:280-296` (`Mixer.WriteUntil`) | Multiply the summed mix by the fade gain **before the clamp**: one place, for every voice at once |
| Export length | `VideoEncoder.cs:45` | `Mixer.Create(sounds, length.Ticks, failed)` — the export length already reaches the mixer |
| Soundtrack | `Composition/Soundtrack.cs`, `UI/PreviewSound.cs:57-122` | Delivered global effect: a file's sound mixed at its level (0–200 %), looped or cut to the grid's loop (`LoopIn`), giving a grid of stills its length; its own voice in the preview graph and in the export mixer |
| Global effects | `Composition/GlobalEffect.cs`, `UI/EffectTabs.cs` | `enum GlobalEffect { Soundtrack, Borders }`, "in the order of the global effects' tabs"; the tabs stand on the global options toolbar |
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
- [x] ~~17. **Tab position** — where does the Fade tab go among Soundtrack and Borders?~~ → Between Soundtrack and Borders
- [x] ~~18. **Rename** — the rule landed without renaming § Effects: is § Effects still renamed § Cell Effects (Q&A 15), or is the rename dropped?~~ → Dropped — § Effects keeps its name
- [x] ~~19. **Tab name** — "Fade", the later image fade joining as options of the same tab, or "Sound fade", a later image fade getting its own tab?~~ → "Fade" — a later image fade joins the same tab
- [x] ~~20. **Options layout** — how the duration and the curve are laid out in the global options toolbar?~~ → Duration slider, then the curve as two icon buttons drawing their shape (tooltip names), then Reset
- [x] ~~12. **Status of the rule** — does the new rule go into `RULES.md` as a new § Global Effects next to § Effects, with § Effects renamed "Cell effects"?~~ → Yes: new § Global Effects, § Effects renamed § Cell Effects *(revised 2026-09-30, see Iteration 8: the rule landed elsewhere, the rename dropped)*

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

### Iteration 7 — 2026-09-30

- The go question was dismissed again; main moved: the **Soundtrack** and **Borders** global
  effects landed, with `RULES.md` § Global Effects (from this workfile's draft) and a **global
  effects toolbar** of tabs standing on a **global options toolbar** (`global-effects-tabs.md`).
- The design follows: the Fade becomes a **tab** of that toolbar, the rule it drafted is no longer
  to be written, the soundtrack joins the faded mix and the "nothing heard" test, and a grid of
  stills with a soundtrack fades over the soundtrack's length.
- The Global effects row of Iterations 1–6 (checkbox and inline options, Reset ending the row) is
  replaced by the landed toolbar; the agreed settings (duration, curve, Reset, lock, Clear all,
  not applicable) are kept.
- New open questions 17–20.

### Iteration 8 — 2026-09-30

- OQ 17–20 answered (Q&A 23–26), after a mockup of three layouts: the **Fade** tab sits **between
  Soundtrack and Borders**; it is named **Fade**, a later image fade joining it; the options are the
  Duration slider, then the curve as **two icon buttons drawing their shape** (Squared / Linear in
  tooltips), then Reset; the **§ Cell Effects rename is dropped** (revises OQ 12).
- No open question left.

### Iteration 9 — 2026-09-30

- `RULES.md` gained § Video Length: one definition, `Animation.VideoLength`, for every consumer.
  The fade's length *L* reads it, in the export and in the preview. The sound code (preview graph,
  export mixer) has not moved since Iteration 7. No open question left.

### Iteration 10 — 2026-09-30 — ✅ Implemented

- Go given: **code, tests and documentation** (Q&A 28) — no unit tests (Q&A 8), README and
  `GLOSSARY.md`. Branch Gate: **stays on `main`**, the standing choice of this repository.
- Scope frozen: the design sections as of Iteration 9.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 2 | 2026-09-26 | Declined — no test project (Q&A 8) |
| README | | | |
| GLOSSARY.md | | | Fade in the *Global effect* list, a Fade entry; `RULES.md` unchanged (the rule landed, rename dropped) |

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
| 22 | Go for implementation? | Dismissed — asked for the questions as MCQ | 2026-09-30 |
| 23 | OQ 17 — Tab position among Soundtrack and Borders? | **Between Soundtrack and Borders** | 2026-09-30 |
| 24 | OQ 18 — Rename § Effects to § Cell Effects, or drop it? | **Dropped** | 2026-09-30 |
| 25 | OQ 19 — Tab name: "Fade" or "Sound fade"? | **Fade** | 2026-09-30 |
| 26 | OQ 20 — Options layout in the global options toolbar? | **C — curve icon buttons** | 2026-09-30 |
| 27 | Go for implementation? | Dismissed — asked for the current state | 2026-09-30 |
| 28 | Go for implementation? | **Code, tests and documentation** | 2026-09-30 |

---

*Last updated: 2026-09-30*
