# Video Mute and Volume

> Working document — a Volume effect to set a video's volume from 0 to 200 % or mute it, the
> sounds of every video of the grid being mixed.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Being able to mute a video of the grid, and more generally to set its volume: a slider from 0 %
to 200 %, with a **Mute** check box. The grid no longer plays a single elected sound: the sounds of
**all its videos are mixed**, each with the volume of its cell. It acts on the preview and on the
exported videos alike.

Components concerned:

| Component | Role today |
|---|---|
| `Composition/ImageLook.cs` | Effects state of a cell + image pair (settings + on / off); `ImageEffect` enum, in toolbar order |
| `Composition/Animation.cs` — `SoundSource` | Elects the **single** sound of the grid: image 1 when it is a video with sound, else the first video with sound in grid order; a frozen video has none. Replaced by the mix |
| `UI/PreviewSound.cs` | Plays that one sound in the preview with a `Windows.Media.Playback.MediaPlayer` (volume capped at 100 %), kept in step with the frames |
| `UI/AnimationPlayer.cs` | Calls `PreviewSound.Follow(Animation.SoundSource(...))` |
| `Imaging/GridExport.cs` | Captures the sound source's path, loop and start into its `Job`, passes them to `VideoEncoder.Create` |
| `Imaging/VideoEncoder.cs` — `Sound` | One Source Reader, decoded to 16-bit PCM, encoded to AAC, looped, written ahead of the video |
| `UI/MainForm.cs` | Effect tabs with their activation checkbox, options toolbar, `OptionSlider`, `ChangeLook` |
| `README.md` | Documents the sound (*Sound* bullet) and the exports |
| `RULES.md` | § Effects — the Volume effect follows it; its contextual default state is a new case |

Related: `workfiles/20260925-soundtrack.md` (design stage) plans a global soundtrack **mixed over**
the videos' sound, and asks whether per-video mute belongs to it or to this workfile.

---

## Volume Effect

Agreed:

- A new **effect** named **Volume**, with its tab and activation checkbox in the effects toolbar,
  following `RULES.md` § Effects: it belongs to the cell + image pair, lives on `ImageLook`, is not
  persisted, follows the image when two cells are swapped, is kept when the layout changes.
- It acts on the **preview and the exports**.

### Options

From left to right: the **Mute** check box, then the volume slider and its label.

- A **volume slider**, **0 %** to **200 %**, with its percentage label.
- A **Mute** check box, **independent** of the slider:
  - the slider reaching **0** checks Mute;
  - checking Mute **keeps** the slider's value, so unchecking brings it back;
  - the slider is **never disabled**, muted or at 0: moving it above 0 unmutes, so the sound comes
    back from the slider itself;
  - unchecking Mute while the slider is at 0 puts it back to **100 %**;
  - moving the slider above 0 unchecks Mute.
- Per `RULES.md`, acting on any option turns the effect on; the options row ends with the effect's
  own *Reset*.
- Turning the effect on with no settings kept (its checkbox, or an option acted on while it is off)
  starts at **100 %, not muted** — what an effect that is off already sounds like; an effect that is
  off shows those settings in its options.
- State: `Composition/VolumeEffect.cs` (`Level` 0…2, `IsMuted`, `Gain`), held by `ImageLook.Volume`,
  its kept settings in `ImageLook.KeptVolume`; `ImageLook.SoundGain` is 1 while the effect is off.

### Applicability

The checkbox is enabled for **every video with a sound track**, a frozen one included (its settings
take effect again once it plays). For any other image — still, animated GIF, video without a sound
track, preview of a text / PDF — the checkbox and the options are disabled, with a tooltip saying why.

### Default State and Arrival

The Volume effect's default state is the general one of `RULES.md` § Effects — default settings,
**off**: a video with sound is **heard at 100 %**, whatever the other cells play.

| Situation when the video arrives (drop, Ctrl+V, browse, replacement) | Its volume |
|---|---|
| Whatever the other cells hold | **100 %**, heard — the effect off |

Several videos arriving at once are all heard: the grid's mix is heard from the start, and each
one is muted or lowered from its Volume tab afterwards.

- **Heard** means actually heard: a video with a sound track, playing (not frozen), whose Volume
  effect does not silence it (not muted, volume above 0). The mix, the soundtrack and the fade
  read it (`SourceImage.IsHeard`, `Animation.Heard`).
- **On / off**: a video arrives with the Volume effect **off**, drawn as its defaults — 100 %.
  Turning the effect off makes the video heard at 100 %.
- **Reset** (the effect's own and the global one) brings back that default state: heard at
  100 %, the effect off.
- **Deletion**: the videos shifting into another cell **keep their volume** — an exception to the
  `RULES.md` reset on deletion, so a shift never changes what is heard. Deleting a heard video
  changes nothing for the others.
- Code: `SourceImage.HasSound` / `SourceImage.IsHeard`; `GridPreview.Add` and `MainForm.ResetLook`
  treat the Volume like every other effect (no `Animation.SoundOnArrival`, no
  `VolumeEffect.Muted`); each Reset button is enabled only when it would change something.
  `ImageLook.WithoutEffects` keeps the volume.

---

## Mixing

Every video of the grid with a sound track contributes its sound, **scaled by its cell's volume**,
looping with its own video from its own starting point (Frames effect). A frozen video contributes
nothing. A muted video contributes nothing.

### Preview

`PreviewSound` held one `MediaPlayer`, whose `Volume` is limited to **0…1**. It is replaced by one
**`Windows.Media.Audio.AudioGraph`**: one `AudioFileInputNode` per video with sound (looping
endlessly), whose `OutgoingGain` is the cell's volume (**up to 2**, so 200 % is heard in the
preview), all mixed into the default output device. Each node is kept in step with its own video's
frames from `AnimationPlayer`'s loop — position, pause, loop — with the 250 ms drift threshold.

- A muted, frozen or held video keeps its node, **paused**; unmuting resumes it in step at once.
- The graph is created with the first video with sound, and **stopped** while the grid holds none,
  so an idle preview holds no audio stream open. A graph or a node Windows cannot create leaves that
  sound out of the preview; the frames still play.

### Export

`VideoEncoder.Sound` becomes `VideoEncoder.Mixer`: one `Voice` (Source Reader) per heard video,
from `GridExport.Job.Sounds` (`MixedSound`: path, loop, start, gain), each looping on its own
video's loop from its own start, **summed with its gain** and **clamped** to the 16-bit range, then
encoded to a single AAC track, written in steps of 100 ms. The export's length stays the longest loop.

- Format: **stereo**, 16-bit, at **44.1 kHz when every sound is**, else 48 kHz; a mono sound goes to
  both sides; a sound shorter than its loop is silent until the loop starts over.
- A sound Windows cannot decode at that rate or re-encode is left out of the mix. The status line
  names the mixed files, then the left-out ones: `sound: a.mp4 + b.mp4 (c.mp4: its sound cannot be
  re-encoded)`, or `no sound (…)` when none is left.

### Shared with the Soundtrack

This workfile **builds the mixer** — preview and export. `workfiles/20260925-soundtrack.md` then
plugs its global soundtrack into it as one more source.

---

## README

Rewrite the *Sound* bullet (mixed sounds instead of the elected one) and document the Volume effect:
slider, Mute, every video heard on arrival, the volume kept on a deletion shift, preview and exports.

## RULES

Record in `RULES.md` § Effects, next to the event table (*The Volume Exception*): a deletion keeps
the volume of the shifted videos. The Volume's default state is the general one — off, heard at
100 % — so nothing else is recorded there. `GLOSSARY.md` keeps *Heard* and loses *Sound on arrival*.

---

## Test Impact

**None.** The repository has no test project (`src/` holds `ImageGridFusion` only), and the user
chose to ship this work **without unit tests**: it is verified manually in the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests, by decision) | — | — |

---

## Open Questions

- [x] ~~What is the effect called — "Volume", "Sound", or "Mute"?~~ → **Volume**
- [x] ~~What does activating the effect do: mute directly, or leave the volume at 100 % unmuted?~~ → Superseded: the volume is set **on arrival**, from the other cells (see § Sound on Arrival); how it maps onto on / off is asked below *(revised 2026-09-27, see Iteration 10: every video arrives heard at 100 %, the effect off)*
- [x] ~~How do the Mute check box and the slider interact beyond "slider at 0 checks Mute"?~~ → **Independent** check box (see § Options)
- [x] ~~When the grid's sound source is muted, does the grid go silent, or does the sound move to the next video with sound?~~ → Neither: the sounds of all videos are **mixed**, each with its cell's volume
- [x] ~~How does the sound on arrival map onto the effect's on / off: muted on arrival = effect **on** with Mute checked, audible = effect **off** (100 %)?~~ → Yes: muted = on with Mute checked, audible = off *(revised 2026-09-27, see Iteration 10: no video is muted on arrival any more)*
- [x] ~~What does the Volume effect's *Reset* (and the global *Reset*) bring back: the rule on arrival, recomputed from the other cells, or plainly 100 %?~~ → The rule on arrival, recomputed *(revised 2026-09-27, see Iteration 10: plainly 100 %, the effect off)*
- [x] ~~Deleting an image: the videos shifting into another cell are reset per `RULES.md` — recompute their sound on arrival, or keep their volume?~~ → Keep their volume (exception to the rule) *(confirmed 2026-09-27, see Iteration 10)*
- [x] ~~"Audible video" for the rule on arrival: a video with a sound track, playing (not frozen), not muted — does a volume at 0 % count as muted, and a video with no sound track count as silent?~~ → Actually heard: sound track, not frozen, not muted, volume above 0 *(the rule on arrival dropped 2026-09-27, see Iteration 10; the meaning of heard stays, for the mix)*
- [x] ~~Which images enable the effect's checkbox: videos with a sound track only — and a frozen video?~~ → Every video with a sound track, a frozen one included
- [x] ~~Preview mixing and 200 %: one `AudioGraph` (gain up to 2, one mix), or several `MediaPlayer`s (native, capped at 100 % in the preview)?~~ → `AudioGraph`
- [x] ~~The soundtrack workfile also needs the mixer: does this workfile build the mixer (preview + export), the soundtrack building on it?~~ → Yes, this workfile builds it
- [x] ~~No test project exists: create one for this work, or ship without unit tests?~~ → No unit tests

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Scoping answered by the user: a new effect of the effects toolbar, acting on the preview and the
exports, with a 0–200 % volume slider and a Mute check box checked at 0; subject expected to be
straightforward, so a single scout pass. The scout pass found that the grid plays a single sound
(`Animation.SoundSource`), that the preview's `MediaPlayer` caps the volume at 100 %, that the export
re-encodes 16-bit PCM where a gain can be applied, and that no test project exists. The points the
design cannot settle alone are listed in Open Questions.

### Iteration 2 — 2026-09-25

User answers: the effect is named **Volume**; the Mute check box is **independent** of the slider;
the volume a video gets depends on the other cells when it arrives (100 % when no other cell is
audible, muted otherwise), instead of a fixed default; and the grid **mixes the sounds of all its
videos**, each with its cell's volume, instead of electing one sound. The mixing turns
`PreviewSound` and `VideoEncoder.Sound` into mixers.

Meanwhile `main` moved on (`workfiles/20260925-ui-cleanup.md` delivered): effects are now tabs with
an activation checkbox, an effect turned off keeps its settings and is drawn as its defaults, acting
on an option turns the effect on, each effect has its own *Reset*, and the carousel (and
`CarouselExport.cs`) is gone. The design is aligned on those rules. `workfiles/20260925-soundtrack.md`
plans a global soundtrack mixed over the videos, which overlaps with the mixer.

New open questions: the mapping of the sound on arrival onto on / off, *Reset*, deletion, the exact
meaning of "audible", the preview mixer, and the split with the soundtrack workfile.

### Iteration 3 — 2026-09-25

User answers: a video muted on arrival has the Volume effect on with Mute checked, an audible one
has it off (100 %); every *Reset* recomputes the rule on arrival; a deletion keeps the shifted
videos' volume, an exception to `RULES.md`; "audible" means actually heard. § Sound on Arrival and
§ RULES updated.

### Iteration 4 — 2026-09-25

User answers: the checkbox is enabled for every video with a sound track, a frozen one included; the
preview mixes with an `AudioGraph` (200 % heard); this workfile builds the mixer the soundtrack will
reuse; no unit tests. § Applicability, § Preview, § Shared with the Soundtrack and § Test Impact
updated. No open question remains.

### Iteration 5 — 2026-09-26 — ✅ Implemented

Go given by the user ("GO", after a first "No"), read as the full scope: code, README and RULES —
unit tests were declined in Iteration 4. Branch Gate: **stays on `main`**, the standing choice of
this repository. Scope frozen on the design sections as they stand.

### Iteration 6 — 2026-09-26 — 🧭 Implementation choices

No project rule was broken. Choices the frozen design did not state:

- **Turning the effect on with nothing kept** starts at 100 %, not muted, so the options of an effect
  that is off show what is heard; muting goes through the Mute check box (which turns the effect on).
- **Export format**: always stereo, 44.1 kHz only when every sound is, else 48 kHz; mono spread to
  both sides; a sound shorter than its loop is padded with silence (the single-sound export left a
  gap); a sound whose decoded rate is not the one asked for is left out, like one that cannot be
  re-encoded.
- **Status line**: lists every mixed file, then the left-out ones (see § Export).
- **Preview**: a node per video with sound, a muted or frozen one kept paused rather than removed;
  the graph stopped while no video with sound is in the grid.
- **Several videos arriving at once** get the rule in the order they arrive: the first heard (when no
  other cell is), the others muted.
- **Reset buttons** are enabled only when the reset would change something, the Volume's rule on
  arrival included (the global one used to be enabled as soon as any effect was on).
- **GLOSSARY** gets *Heard* and *Sound on arrival*, next to the RULES exception.
- **Builds** went to the scratchpad: the app's own `bin` executable was locked by a running
  instance, left untouched.

### Iteration 7 — 2026-09-26 — ⚙️ Post-implementation — slider never disabled

User feedback while testing: reaching 0 must not grey the volume slider, otherwise bringing the
sound back is awkward. The slider now stays enabled whatever the mute — muted from the check box or
at 0 — and moving it above 0 unmutes, as the design already said. § Options updated; README follows.

### Iteration 8 — 2026-09-26 — ⚙️ Post-implementation — Mute left of the slider

User request while testing: put "Mute" to the left of the slider. The options row now reads Mute,
then the slider and its label. § Options updated; README follows.

### Iteration 9 — 2026-09-26 — ⚙️ Post-implementation — task closed

Validated by the user after testing in the app (Iterations 7 and 8 included): the task is finished.

### Iteration 10 — 2026-09-27 — ⚙️ Post-implementation — every video arrives heard

The sound on arrival surprised the user twice: on the evening of 2026-09-26 (a video dropped beside
a heard one had no sound — session *Pas de son sur la vidéo*, answered as the rule, not a bug), and
on 2026-09-27 ("the sound of several videos is not mixed"). Asked what to do with the rule — keep
it, every video heard on arrival, a ⚙ setting, or test first — the user chose **every video heard
on arrival**: a video with sound arrives heard at 100 %, the Volume effect off, whatever the other
cells play, so the grid's mix is heard at once; both Resets bring back that state. The volume of
the videos shifting after a deletion stays **kept** (Q&A 23), the reason of 2026-09-25 still
holding. `Animation.SoundOnArrival` and `VolumeEffect.Muted` go; `RULES.md`'s *Volume Exception*
keeps its deletion line only; `GLOSSARY.md` loses *Sound on arrival*; the README follows.
§ Sound on Arrival becomes § Default State and Arrival; § README and § RULES updated; the revised
Open Questions annotated. Go given for code and documentation (Q&A 24), on `main`. Delivered the
same day: the code, then README / RULES / GLOSSARY, in two commits, built clean — no divergent
choice; the app launched with two videos with sound, both heard at once.

### Iteration 11 — 2026-09-27 — ⚙️ Post-implementation — task closed

Validated by the user after testing in the app (Iteration 10): the task is finished.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 6, 7, 8, 10 | 2026-09-27 | State, export mixer, preview mixer, Volume tab — 4 commits; verified with a scratchpad harness exporting 3 videos (one without sound, one at 200 %) and an all-muted grid. Iteration 10: the sound on arrival dropped — `Animation.SoundOnArrival` and `VolumeEffect.Muted` removed, `GridPreview.Add` and `MainForm.ResetLook` general — 1 commit, built clean |
| Unit tests | 4 | 2026-09-25 | Declined by the user — no test project, manual verification |
| README | 6, 7, 8, 10 | 2026-09-27 | Volume section, Effects and Sound bullets; the slider never disabled; Mute first. Iteration 10: a video arrives heard at 100 %, the Reset sentence without its Volume case |
| RULES | 6, 10 | 2026-09-27 | § Effects — *The Volume Exception*; GLOSSARY: Volume, Heard, Sound on arrival. Iteration 10: the exception reduced to the deletion shift; GLOSSARY loses *Sound on arrival* |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the mute fit in the app? | A "Mute" effect in the effects toolbar | 2026-09-25 |
| 2 | What does the mute act on? | Preview and exports | 2026-09-25 |
| 3 | What is a video's default sound state? | A slider sets the volume, up to 200 %; reaching 0 checks the "Mute" box | 2026-09-25 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-25 |
| 5 | What is the effect called? | Volume | 2026-09-25 |
| 6 | What does activating the effect do? | Depends on the other cells: no other video with sound → 100 % for the added video, otherwise 0 % (mute); replacing the only video with sound → 100 %; replacing one of several → 0 % (mute) | 2026-09-25 |
| 7 | How do the Mute check box and the slider interact? | Independent check box | 2026-09-25 |
| 8 | Muted sound source: silent grid, or the next video's sound? | Neither: the sounds are mixed, each with its own volume per cell | 2026-09-25 |
| 9 | Which images enable the effect's checkbox? | Every video with a sound track (frozen included) | 2026-09-25 |
| 10 | Preview mixing and 200 %: `AudioGraph`, or several `MediaPlayer`s capped at 100 %? | `AudioGraph` | 2026-09-25 |
| 11 | No test project: create one, or no unit tests? | No unit tests | 2026-09-25 |
| 12 | Sound on arrival ↔ on / off: muted = on with Mute checked, audible = off? | Yes, muted = on, audible = off | 2026-09-25 |
| 13 | What does *Reset* bring back: the rule on arrival, or 100 %? | The rule on arrival, recomputed | 2026-09-25 |
| 14 | Deletion: recompute the shifted videos' sound, or keep it? | Keep their volume | 2026-09-25 |
| 15 | What counts as an "audible" video for the rule on arrival? | Actually heard: sound track, not frozen, not muted, volume above 0 | 2026-09-25 |
| 16 | Does this workfile build the mixer the soundtrack will reuse? | Yes, this workfile | 2026-09-25 |
| 17 | May the implementation begin? | No — the gate holds | 2026-09-26 |
| 18 | May the implementation begin? | GO — full scope (code, README, RULES) | 2026-09-26 |
| 19 | Is the task finished? | Dismissed; the user tests first — then asks that reaching 0 does not grey the slider | 2026-09-26 |
| 20 | Is the task finished? | Dismissed — then asks for Mute left of the slider | 2026-09-26 |
| 21 | Is the task finished? | Yes — validated by the user after testing in the app | 2026-09-26 |
| 22 | The sound on arrival surprised you twice (26/09 evening, 27/09): keep the rule, every video heard on arrival, a ⚙ setting, or test first? | Every video heard on arrival: 100 %, the effect off — the mix heard at once | 2026-09-27 |
| 23 | An image deleted: the shifted videos keep their volume (exception kept), or are reset like the other effects? | Kept — the exception stays, a shift never changes what is heard | 2026-09-27 |
| 24 | May the adjustment be implemented? | GO — code and documentation (README, RULES, GLOSSARY); unit tests stay declined | 2026-09-27 |
| 25 | Is the task finished? | Yes — validated by the user after testing in the app | 2026-09-27 |

---

*Last updated: 2026-09-27*
