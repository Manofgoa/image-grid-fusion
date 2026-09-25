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

- A **volume slider**, **0 %** to **200 %**, with its percentage label.
- A **Mute** check box, **independent** of the slider:
  - the slider reaching **0** checks Mute;
  - checking Mute **keeps** the slider's value (the slider shown disabled), so unchecking brings it back;
  - unchecking Mute while the slider is at 0 puts it back to **100 %**;
  - moving the slider above 0 unchecks Mute.
- Per `RULES.md`, acting on any option turns the effect on; the options row ends with the effect's
  own *Reset*.

### Sound on Arrival

The volume a video gets when it enters a cell depends on the **other cells**:

| Situation when the video arrives (drop, Ctrl+V, browse, replacement) | Its volume |
|---|---|
| No other cell holds an audible video | **100 %**, audible |
| Another cell holds an audible video | **Muted** (0 %) |

Examples given by the user: replacing the only video with sound → the new one is at 100 %;
replacing one video with sound among several → the new one is muted.

- **Audible** means actually heard: a video with a sound track, playing (not frozen), whose Volume
  effect does not silence it (not muted, volume above 0).
- **On / off**: a video muted on arrival has the Volume effect **on**, Mute checked (slider at
  100 %, kept); an audible one has it **off**, drawn as its defaults — 100 %. Turning the effect off
  makes the video audible at 100 %.
- **Reset** (the effect's own and the global one) brings back the **rule on arrival, recomputed**
  from the other cells at that moment, as if the video had just arrived.
- **Deletion**: the videos shifting into another cell **keep their volume** — an exception to the
  `RULES.md` reset on deletion, so a shift never changes what is heard. Deleting the only audible
  video does not make the others audible.

---

## Mixing

Every video of the grid with a sound track contributes its sound, **scaled by its cell's volume**,
looping with its own video from its own starting point (Frames effect). A frozen video contributes
nothing. A muted video contributes nothing.

### Preview

`PreviewSound` today holds one `MediaPlayer`, whose `Volume` is limited to **0…1**. Mixing needs
one sound per audible video, each kept in step with its own video; 200 % needs a gain above 1 —
see Open Questions (`AudioGraph` vs several `MediaPlayer`s).

### Export

`VideoEncoder.Sound` becomes a mixer: one Source Reader per audible video, each converted to the
same PCM format (48 kHz or 44.1 kHz, stereo, 16-bit), each looping on its own video's loop from its
own start, **summed with its gain** and **clamped** to the 16-bit range, then encoded to a single
AAC track. The export's length stays the longest loop. A sound Windows cannot re-encode is left out
of the mix, with a note in the status line (as today for the single sound).

---

## README

Rewrite the *Sound* bullet (mixed sounds instead of the elected one) and document the Volume effect:
slider, Mute, the sound on arrival, preview and exports.

## RULES

Record in `RULES.md` § Effects, next to the event table: the Volume effect's default state depends
on the other cells (the rule on arrival, recomputed by every *Reset*), and a deletion keeps the
volume of the shifted videos.

---

## Test Impact

The repository has **no test project** today (`src/` holds `ImageGridFusion` only) — see Open
Questions. Behaviours that would be pinned:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Volume settings: slider ↔ Mute rules (0 checks Mute, Mute keeps the value, unmuting at 0 → 100 %) | `src/ImageGridFusion.Tests/Composition/ImageLookTests.cs` | Create (pending the test-project decision) |
| Sound on arrival: 100 % with no other audible video, muted otherwise, replacement cases | `src/ImageGridFusion.Tests/Composition/AnimationTests.cs` | Create (pending the test-project decision) |
| Which images contribute to the mix (frozen, muted, no sound track) | `src/ImageGridFusion.Tests/Composition/AnimationTests.cs` | Create (pending the test-project decision) |
| PCM mix: gains summed, clamped to the 16-bit range | `src/ImageGridFusion.Tests/Imaging/SoundMixTests.cs` | Create (pending the test-project decision) |

---

## Open Questions

- [x] ~~What is the effect called — "Volume", "Sound", or "Mute"?~~ → **Volume**
- [x] ~~What does activating the effect do: mute directly, or leave the volume at 100 % unmuted?~~ → Superseded: the volume is set **on arrival**, from the other cells (see § Sound on Arrival); how it maps onto on / off is asked below
- [x] ~~How do the Mute check box and the slider interact beyond "slider at 0 checks Mute"?~~ → **Independent** check box (see § Options)
- [x] ~~When the grid's sound source is muted, does the grid go silent, or does the sound move to the next video with sound?~~ → Neither: the sounds of all videos are **mixed**, each with its cell's volume
- [x] ~~How does the sound on arrival map onto the effect's on / off: muted on arrival = effect **on** with Mute checked, audible = effect **off** (100 %)?~~ → Yes: muted = on with Mute checked, audible = off
- [x] ~~What does the Volume effect's *Reset* (and the global *Reset*) bring back: the rule on arrival, recomputed from the other cells, or plainly 100 %?~~ → The rule on arrival, recomputed
- [x] ~~Deleting an image: the videos shifting into another cell are reset per `RULES.md` — recompute their sound on arrival, or keep their volume?~~ → Keep their volume (exception to the rule)
- [x] ~~"Audible video" for the rule on arrival: a video with a sound track, playing (not frozen), not muted — does a volume at 0 % count as muted, and a video with no sound track count as silent?~~ → Actually heard: sound track, not frozen, not muted, volume above 0
- [ ] Which images enable the effect's checkbox: videos with a sound track only — and a frozen video?
- [ ] Preview mixing and 200 %: one `AudioGraph` (gain up to 2, one mix), or several `MediaPlayer`s (native, capped at 100 % in the preview)?
- [ ] The soundtrack workfile also needs the mixer: does this workfile build the mixer (preview + export), the soundtrack building on it?
- [ ] No test project exists: create one for this work, or ship without unit tests?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| RULES | | | |

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
| 9 | Which images enable the effect's checkbox? | | |
| 10 | Preview mixing and 200 %: `AudioGraph`, or several `MediaPlayer`s capped at 100 %? | | |
| 11 | No test project: create one, or no unit tests? | | |
| 12 | Sound on arrival ↔ on / off: muted = on with Mute checked, audible = off? | Yes, muted = on, audible = off | 2026-09-25 |
| 13 | What does *Reset* bring back: the rule on arrival, or 100 %? | The rule on arrival, recomputed | 2026-09-25 |
| 14 | Deletion: recompute the shifted videos' sound, or keep it? | Keep their volume | 2026-09-25 |
| 15 | What counts as an "audible" video for the rule on arrival? | Actually heard: sound track, not frozen, not muted, volume above 0 | 2026-09-25 |
| 16 | Does this workfile build the mixer the soundtrack will reuse? | | |

---

*Last updated: 2026-09-25*
