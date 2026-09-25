# Video Mute and Volume

> Working document — a sound effect to mute a video or set its volume, from 0 to 200 %.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Being able to mute a video of the grid, and more generally to set its volume: a slider from 0 %
to 200 %, with a **Mute** check box that gets checked when the slider reaches 0. It acts on the
preview and on the exported videos alike.

Components concerned:

| Component | Role today |
|---|---|
| `Composition/ImageLook.cs` | Effects state of a cell + image pair; `ImageEffect` enum, in toolbar order |
| `Composition/Animation.cs` — `SoundSource` | Picks the **single** sound of the grid: image 1 when it is a video with sound, else the first video with sound in grid order; a frozen video has none |
| `UI/PreviewSound.cs` | Plays that sound in the preview with `Windows.Media.Playback.MediaPlayer`, kept in step with the frames |
| `UI/AnimationPlayer.cs` | Calls `PreviewSound.Follow(Animation.SoundSource(...))` |
| `Imaging/GridExport.cs`, `Imaging/CarouselExport.cs` | Pass the sound source's path, loop and start to `VideoEncoder.Create` |
| `Imaging/VideoEncoder.cs` — `Sound` | Decodes the sound to 16-bit PCM with a Source Reader and feeds it to the AAC encoder |
| `UI/MainForm.cs` | Effects toolbar (`_effectButtons`), options toolbar (`_options[effect]`), `OptionSlider`, `ChangeLook` |
| `README.md` | Documents the sound (l. 111) and the exports (l. 112–115) |

---

## Sound Effect

Agreed during scoping:

- A new **effect** of the effects toolbar, following every rule of `RULES.md` § Effects: it belongs
  to the cell + image pair, lives on `ImageLook`, is not persisted, is reset when the cell's image
  is replaced, follows the image when two cells are swapped, and is removed by *Reset*.
- It acts on the **preview and the exports**.

Options toolbar, while the effect is selected:

- A **volume slider**, from **0 %** to **200 %**, with its percentage label.
- A **Mute** check box, **checked** when the slider reaches 0.

To settle (see Open Questions): the effect's name, what activating it does, how the check box and
the slider interact beyond "0 checks Mute", which images enable its button, and what a muted sound
source means for the grid's single sound.

---

## Preview

`PreviewSound` uses `MediaPlayer`, whose `Volume` is limited to **0…1**: 100 % at most. Reaching
200 % in the preview needs another player (e.g. `Windows.Media.Audio.AudioGraph`, whose node gain
may exceed 1) — see Open Questions.

---

## Export

`VideoEncoder.Sound.Write` holds each decoded sample as 16-bit PCM before handing it to the AAC
encoder: the volume is applied there by scaling the samples, **clamped** to the 16-bit range
above 100 %. A muted sound source writes no sound track (see Open Questions for what replaces it).

---

## README

Update the *Sound* bullet (l. 111) and add the effect to the effects list: the volume, the mute,
their effect on the preview and the exports.

---

## Test Impact

The repository has **no test project** today (`src/` holds `ImageGridFusion` only) — see Open
Questions. Behaviours that would be pinned:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `ImageLook` sound state: defaults, activation, volume 0 ⇒ muted, reset | `src/ImageGridFusion.Tests/Composition/ImageLookTests.cs` | Create (pending the test-project decision) |
| `Animation.SoundSource` with a muted video | `src/ImageGridFusion.Tests/Composition/AnimationTests.cs` | Create (pending the test-project decision) |
| PCM gain: scaling at 50 % / 200 %, clamping to the 16-bit range | `src/ImageGridFusion.Tests/Imaging/SoundGainTests.cs` | Create (pending the test-project decision) |

---

## Open Questions

- [ ] What is the effect called — "Volume", "Sound", or "Mute"?
- [ ] What does activating the effect do: mute directly, or leave the volume at 100 % unmuted?
- [ ] How do the Mute check box and the slider interact beyond "slider at 0 checks Mute"?
- [ ] When the grid's sound source is muted, does the grid go silent, or does the sound move to the next video with sound?
- [ ] Which images enable the effect's button: videos with a sound track only, and a frozen video?
- [ ] 200 % in the preview: switch the preview sound to `AudioGraph`, or cap the preview at 100 % while the export amplifies?
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
| 1 | How does the mute fit in the app? | A "Mute" effect in the effects toolbar | 2026-09-25 |
| 2 | What does the mute act on? | Preview and exports | 2026-09-25 |
| 3 | What is a video's default sound state? | A slider sets the volume, up to 200 %; reaching 0 checks the "Mute" box | 2026-09-25 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-25 |
| 5 | What is the effect called? | | |
| 6 | What does activating the effect do? | | |
| 7 | How do the Mute check box and the slider interact? | | |
| 8 | Muted sound source: silent grid, or the next video's sound? | | |
| 9 | Which images enable the effect's button? | | |
| 10 | 200 % in the preview: `AudioGraph`, or preview capped at 100 %? | | |
| 11 | No test project: create one, or no unit tests? | | |

---

*Last updated: 2026-09-25*
