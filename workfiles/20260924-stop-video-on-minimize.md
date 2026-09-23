# Stop Video On Minimize

> Working document — stop the videos, animations and sound of the grid while the window is hidden in the tray.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Closing the window (×, Alt+F4, taskbar Close) only hides it: the app keeps running in the
notification area. Today the grid keeps playing while hidden — animated images keep decoding
frames and the sound source keeps playing, so the user hears a window they cannot see.

Goal: when the window goes to the tray, every video / animation and the sound **stop**; when it
is brought back from the tray, they **play again from the start**.

Relevant components:

| Component | Role |
|---|---|
| `src/ImageGridFusion/UI/MainForm.cs` — `OnFormClosing` | A user close cancels the close and calls `Hide()` |
| `src/ImageGridFusion/UI/TrayApplicationContext.cs` — `ShowForm` | Tray click / **Open** shows the window again |
| `src/ImageGridFusion/UI/GridPreview.cs` — `SyncPlayer` | Owns the `AnimationPlayer`, syncs it with the grid's images |
| `src/ImageGridFusion/UI/AnimationPlayer.cs` | Plays every animated image on one clock, plus the sound through `PreviewSound` |
| `src/ImageGridFusion/UI/PreviewSound.cs` | Windows `MediaPlayer` for the grid's sound source |

---

## Behaviour

| Event | Effect |
|---|---|
| Window hidden to the tray (×, Alt+F4, taskbar Close) | Every animated image stops, the sound stops. Each cell keeps the frame it showed |
| Window shown again (tray click, tray **Open**) | Every animated image plays again **from the start** (position 0), the sound too, in step |
| Window minimized to the taskbar | ❌ Not a trigger — playback goes on, as today |
| Hovered / slider-held cell at restore | Keeps the existing rule: it is held still, the others play |
| Export running while hidden | Unaffected — the export does not depend on the preview player |

"Resume" and "stop, back to start" combine as: what played before hiding plays again after
showing, restarted from its beginning. Since every animated image of the grid plays (except
the held one), this means the whole grid restarts.

---

## Design

Hiding the window changes the effective visibility of `GridPreview` (a child control): WinForms
raises its `VisibleChanged`. A minimize does not change `Visible`, so reacting to visibility
matches the "tray only" scope by construction.

1. **`AnimationPlayer.Stop()`** (new): cancels and clears every playback (as `Dispose` does for
   playbacks) and stops the sound (`_sound.Follow(null)`, which disposes the media player). The
   player stays usable: a later `Sync` starts everything again.
   - Playbacks restarted by `Sync` begin on a whole second of the shared clock with position 0:
     that already is "from the start", in step.
2. **`GridPreview.OnVisibleChanged`** (override): hidden → `_player.Stop()`; shown → `SyncPlayer()`.
3. **`GridPreview.SyncPlayer`**: does nothing while the control is not visible, so no change of
   the grid can restart playback behind a hidden window (e.g. startup with `--tray`, where the
   window is never shown until the tray icon is clicked).

---

## Test Impact

The repository has no unit-test project (`ImageGridFusion.slnx` holds only the app). The change
lives in WinForms controls and Windows media playback; nothing is testable without a UI host.
**No unit test is created or updated.** Checked by hand: play a grid with a video carrying sound,
close to tray (silence, no CPU for decoding), reopen (restart from 0 with sound), minimize to the
taskbar (playback goes on).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project) | — | — |

---

## Open Questions

None — every design question was settled in the scoping batch (see Q&A Log).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial design from the scoping batch: going to the tray stops every animation and the sound;
coming back from the tray restarts them from the start; a taskbar minimize is not a trigger.
Implemented by reacting to `GridPreview`'s visibility, with a new `AnimationPlayer.Stop()` and a
visibility guard in `SyncPlayer`. README's *Tray & startup* section gets one line.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Not applicable: no test project in the repository |
| README | | | *Tray & startup*: playback stops while hidden, restarts from the start when reopened |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When the app goes to the tray, what happens to playing videos / sounds: pause (keep position), stop (back to start), or mute only? | Stop (back to start) | 2026-09-24 |
| 2 | When the window is reopened from the tray icon, should playback resume? | Yes, resume | 2026-09-24 |
| 3 | Which cases trigger the stop: tray only, or also a taskbar minimize? | Tray only | 2026-09-24 |
| 4 | Exploration depth: straightforward or tricky / long? | Straightforward | 2026-09-24 |

---

*Last updated: 2026-09-24*
