# Rewind on Arrival

> Working document — whenever an image arrives in a cell, or one is removed, every animated image
> plays again from its starting point at the same instant, and the soundtrack from its beginning.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every animated image of the preview plays on **one clock** (`AnimationPlayer._clock`, a
`Stopwatch` started once, never restarted), each from the starting point of its Frames effect. An
image starts playing when it appears in the grid, anchored on the **whole second of the clock
before its arrival**, so that the steps of PDF pages and text views change together in every cell.
Two videos added one after the other are therefore **out of step**: the second one starts wherever
the first one has got to, up to a second earlier or later. The soundtrack starts on a whole second
when it is turned on, or when the first image arrives, and never starts again.

The user's request: **when a video is added, every video goes back to its first frame** — the grid
plays as its export would, every content from its start, together. Scoped with the user (Q&A #1–3)
to every arrival and every deletion, every animated image, the soundtrack with them.

Components concerned (line numbers as read on 2026-09-30):

| Component | Role today |
|---|---|
| `src/ImageGridFusion/UI/AnimationPlayer.cs` — `_clock` (l.19), `Playback` (l.275) | The shared `Stopwatch`; one `Playback` per animated image: `Offset` (clock time its loop started), `PausedAt` (the time a frozen image stands at, `null` while playing), `Reader`, `Cancellation` |
| `AnimationPlayer.Sync` (l.39) | Plays the animated images not playing yet, anchored on the whole second before now minus their `StartTime` (l.54–55); stops the ones gone; a frozen one stands at its start (`StandAtStart`, l.185); then follows the video sounds and the soundtrack |
| `AnimationPlayer.Position` (l.183), `RunAsync` (l.190) | `PausedAt ?? clock − Offset`, looped on the image's duration, polled every 33 ms: the frame at that time is decoded off the UI thread and shown; the video's sound is kept in step with it at every iteration (`_sound.Sync`, l.203) |
| `AnimationPlayer.Update` (l.91) | The Frames effect of one image changed: frozen, it stands at its start; else it plays again from its starting point, **alone** (`Offset = clock − StartTime`) |
| `AnimationPlayer.Stop` (l.130), `UpdateSoundtrack` (l.151), `SyncSoundtrack` (l.171) | Hidden window: everything stops; the soundtrack plays while the grid holds an image, from `_soundtrackStart` (a whole second, set when it starts, l.162), resynced every 100 ms on the grid's loop then on its own duration |
| `src/ImageGridFusion/UI/PreviewSound.cs` — `Sync` (l.115), `Drift` (l.17) | The AudioGraph node of a sound (a video's, the soundtrack's) is **seeked** to the time asked for whenever it drifts by more than 250 ms, so a jump back in time on the clock is followed by the sound by itself |
| `src/ImageGridFusion/Composition/SourceImage.cs` — `IsAnimated` (l.41), `IsFrozen` (l.44), `Plays` (l.47), `StartPage` / `StartTime` (l.56–59) | An animated image: a video, an animated GIF, a PDF of several pages, a text longer than its cell; its starting point, the page the Frames effect points at (the first without it), as a time in its loop |
| `src/ImageGridFusion/Composition/AnimationReader.cs`, `Imaging/VideoReader.cs` | The frame decoders, asked for a time: `StepReader` renders the step at that time (nothing when unchanged); `VideoReader.Read` **seeks by itself** when asked for an earlier time than the last one shown |
| `src/ImageGridFusion/UI/GridPreview.cs` — `Add` (l.252) | **Every arrival route converges here**: a file dropped (on a cell or the canvas, from the Explorer or a tile of the file explorer), Ctrl+V (files, an image, a text), *Browse…* — all through `MainForm.AddFilesAsync` (l.943), `Paste` (l.874) or `AddTextAsync` (l.935). Replaces the target cell or fills the free slots, then `OnImagesChanged()` (l.280) |
| `GridPreview.RemoveAt` (l.892) | **Every deletion**: the cell's **×** (l.522) and the selected cell's deletion (l.332). Disposes the image, resets the effects of the images that shift (their volume kept), then `OnImagesChanged()` (l.907) |
| `GridPreview.Clear` (l.357), `Swap`, `OnImagesChanged` (l.923), `SyncPlayer` (l.988) | *Clear all* disposes everything; a swap only reorders; `OnImagesChanged` refits the pages, **syncs the player** (l.939, synchronously) and raises `ImagesChanged`; `SyncPlayer` does nothing while the window is hidden |
| `GridPreview.WindowVisibleChanged` (l.751), `ShowFrames` (l.1272) | Hidden in the tray, the player stops; shown again, `Sync` plays everything from the start on one whole second. A Frames effect change goes to `AnimationPlayer.Update` |
| `src/ImageGridFusion/Composition/Animation.cs` — `LoopTime` (l.13), `GridLength` (l.21) | The loop arithmetic shared by the preview and the exports; the exports render every content from its starting point at their time 0 already |
| `README.md` — *Animated content* (l.204–205), *Soundtrack* (l.143) | Describe the one clock, the starting points, and the soundtrack playing "from its start when turned on" |
| `RULES.md`, `GLOSSARY.md` | No rule about the preview's playback yet; *Heard*, *Starting point*, *Frozen* are defined |

**Concurrent work, landed**: `workfiles/20260925-video-mute.md` iteration 10 was implemented on
`main` on 2026-09-27 (2a8ab2d): every video arrives heard at 100 %, the sound-on-arrival line gone
from `Add`, the images that shift on a deletion keeping their volume in `RemoveAt`. `AnimationPlayer`
and `PreviewSound` are untouched since 2026-09-26.

---

## Scope

### Agreed (Q&A #1–4)

- **What starts the grid over** (Q&A #1): an image **arriving** in a cell, whatever it is — a video,
  an animated GIF, a still, a text or PDF preview — by **any route** (drop, Ctrl+V, *Browse…*, a
  tile of the file explorer), in an empty cell or **replacing** another image; and an image
  **deleted** (the **×**, the selected cell's deletion).
- **What starts over** (Q&A #2): **every animated image** — video, animated GIF, PDF pages, text
  views — from its **starting point**: the page its Frames effect points at, the first frame without
  the effect. A **frozen** image stays on its frame. The arriving image is one of them: it starts
  with the others.
- **The soundtrack** (Q&A #3) starts over **from its beginning at the same instant**, so the mix
  stays what an export gives.
- **Together, and playing**: every image and the soundtrack restart at **one instant**, and keep
  playing — nothing pauses.
- **Not a trigger** (the assumptions stated with Q&A #1, accepted): a **swap**, a **layout change**,
  an **effect** change (a Frames effect change replays its own image alone, as today), the
  soundtrack's own toggle or file change (another file plays from its start, as today), *Clear all*
  (nothing is left to play; the soundtrack goes back to its initial state).
- Judged **straightforward** (Q&A #4): one scouting pass, closed.

### Out of Scope

- The exports: they already render every content from its starting point at time 0, with the
  soundtrack from its beginning — the preview joins them.
- A *Restart* button, a play / pause control: none exists, none is added.

---

## Design

### The Restart

One new member, `AnimationPlayer.Restart()`, public like `Sync` and `Stop`:

> Plays every animated image again from its starting point, all at this instant, and the soundtrack
> from its beginning: the clock starts over.

| Step | What it does | Why |
|---|---|---|
| `_clock.Restart()` | The restart instant becomes the clock's origin | Zero is a whole second: the whole-second anchoring of `Sync` (l.54–55) — an image that becomes animated later, a text that grows after a layout change — stays in step with the images restarted here |
| For every `Playback` whose `PausedAt` is `null`: `Offset = −Image.StartTime` | Its position at the restart instant is exactly its `StartTime`: the starting point of its Frames effect, the first frame without it | The frozen ones (`PausedAt` set by `StandAtStart`) are left where they stand; unfrozen later, `Update` recomputes their `Offset` from the clock as today |
| `_soundtrackStart = TimeSpan.Zero` when it is not `null` | The soundtrack's loop starts at the restart instant | `null` means it does not play (no soundtrack, or no image): left `null`, `UpdateSoundtrack` sets it when it starts |
| `SyncSoundtrack()` | The soundtrack is seeked **now**, not up to 100 ms later at the next timer tick | `PreviewSound.Sync` seeks only beyond a 250 ms drift: a soundtrack within 250 ms of its start is left alone, inaudibly |

No decoder is reset: `VideoReader.Read` seeks by itself when asked for an earlier time; `StepReader`
renders the new step, or keeps its frame when the step is unchanged (a GIF back in its first step
shows the same frame). One frame decoded for the old time may still be shown once (≤ 33 ms plus the
decode), then the next iteration asks for the new time — harmless. The video sounds follow their
frames through `_sound.Sync` at the next iteration, seeked by the same 250 ms rule.

`Restart` runs on the UI thread, like every member of the player: `RunAsync` reads the clock on the
UI thread between its awaits, so no decode thread ever sees the clock mid-restart.

### Where It Is Called

Two call sites in `GridPreview`, both **right after `OnImagesChanged()`**, which has synced the
player: the arriving image's playback exists, the removed image's is gone.

| Method | After `OnImagesChanged()` |
|---|---|
| `Add` (l.280) | `_player.Restart()` — once per call, whatever the number of images added (a multi-file drop restarts once) |
| `RemoveAt` (l.907) | `_player.Restart()` — the remaining images start over; with none animated left, only the soundtrack does |

Not called from `Clear` (nothing is left; `UpdateSoundtrack` drops the soundtrack's start when no
image remains, and *Clear all* resets the soundtrack anyway), `Swap`, the layout setter, `SetLook`
/ `ShowFrames`, nor the soundtrack setter.

**Hidden window**: `SyncPlayer` does nothing, so `Restart` finds no playback and only restarts the
clock; `_soundtrackStart` is `null` (the player is stopped). Shown again, `Sync` plays everything
from the start on one whole second, as today.

**Exporting**: arrivals are refused (`RefuseWhileExporting`) and the preview is locked (no **×**, no
deletion), so nothing starts over during an export — the export keeps the preview it started from.

### What Changes for the User

- Two videos dropped one after the other **play together**, each from its starting point, exactly
  as the exported MP4 will show them; a still dropped next to them makes them start over too.
- Deleting a cell makes the remaining videos start over together.
- The soundtrack starts over with the images at every arrival or deletion; it still starts on its
  own when turned on or given another file.
- A frozen image, a swap, a layout change, a Frames effect change: as today.

---

## Documentation

| File | Change |
|---|---|
| `README.md` — *Animated content*, the **Live preview** bullet (l.204) | Adds: an image arriving in a cell, or one removed, **starts the grid over** — every animated image from its starting point, at the same instant, the soundtrack from its beginning — so the preview plays what the export gives; a frozen image stays on its frame; a swap or a layout change changes nothing |
| `README.md` — *Soundtrack*, the preview bullet (l.143) | "from its start when turned on" gains "and again, with the images, whenever one arrives or is removed" |
| `RULES.md` — new section **## Preview Playback**, after *Global Effects* | The rule below, so every future route into a cell keeps it |
| `GLOSSARY.md` | New row **Start over** (*repartir de zéro*): every animated image playing again from its starting point at one instant, the soundtrack from its beginning — what an image arriving in a cell, or one removed, does to the grid |

The rule, as written in `RULES.md`:

> ## Preview Playback
>
> Applies to whatever changes the grid's content (origin: `workfiles/20260927-video-add-rewind.md`).
>
> - An image **arriving** in a cell — by any route, in an empty cell or replacing another — and an
>   image **deleted** make the grid **start over**: every animated image plays again from its
>   **starting point** (its Frames effect's), at the **same instant**, and the soundtrack from its
>   **beginning**; a frozen image stays on its frame. The preview then plays what an export gives.
> - A swap, a layout change, an effect change and the soundtrack's own toggle do **not** start the
>   grid over: the images keep playing as they are (a Frames effect change replays its own image
>   only).
> - The restart is `AnimationPlayer.Restart`, called from `GridPreview` where the images change;
>   every route into a cell goes through `GridPreview.Add`, so a new one starts the grid over by
>   itself.

---

## Test Impact

The repository holds **no test project** — `src/ImageGridFusion/ImageGridFusion.csproj` is its
only project (checked by the three scouting passes: no `*Tests*` folder, no `[Fact]` / `[Test]`
anywhere). `AnimationPlayer` runs on the WinForms UI thread over Media Foundation and a Windows
AudioGraph: nothing unit-testable is created or changed. **No unit test** — the behaviour is
checked by hand:

| Check | Expected |
|---|---|
| Drop a video, wait a few seconds, drop a second one | Both play from their first frame at the same moment, in step from then on; their sounds follow |
| Drop a still next to two playing videos | Both videos start over together |
| Delete one of three videos with **×** | The two left start over together |
| A video with a Frames starting point at 40 %, another video dropped | It starts over from its 40 % frame, the other from its first |
| A frozen video, another video dropped | The frozen one does not move; the other starts from its first frame |
| Soundtrack on, a video dropped | The soundtrack starts over from its beginning with the video |
| Two cells swapped, the layout changed, a Rotate or Zoom changed | Nothing starts over |
| Window hidden to the tray, shown again | Everything plays from the start, as today |

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(none — no test project; see above)* | — | — |

---

## Open Questions

None — the scoping batch settled the triggers, what starts over, the Frames effect and the
soundtrack (Q&A #1–3), and the exploration left no open point.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-27

Scoping answered in one batch (Q&A #1–4): every arrival (any image, any route, empty cell or
replacement) and every deletion start the grid over; every animated image from its starting point,
frozen ones left alone; the soundtrack with them; judged straightforward. Three read-only scouting
passes (playback clock, soundtrack, arrival and deletion routes) closed every angle: one shared
`Stopwatch`, one `Playback` per image, all arrivals through `GridPreview.Add`, all deletions through
`RemoveAt`, the sounds seeked by themselves beyond a 250 ms drift, no test project. Design proposed:
`AnimationPlayer.Restart()` — the clock restarted so its origin stays a whole second, every playing
image re-anchored on its starting point, the soundtrack's start at zero and seeked at once — called
after `OnImagesChanged()` in `Add` and `RemoveAt`. Documentation: README, a *Preview Playback* rule,
a glossary row. No open question.

### Iteration 2 — 2026-09-30 — main moved, design unchanged

Three days after the go was declined, `main` moved: `workfiles/20260925-video-mute.md` iteration 10
landed (2a8ab2d, 2026-09-27) — every video arrives heard at 100 %, the sound-on-arrival line gone
from `GridPreview.Add`, the images that shift on a deletion keeping their volume in `RemoveAt` —
then the file explorer and window size work. `AnimationPlayer`, `PreviewSound`, `SourceImage` and
the loop helpers are untouched since 2026-09-26. The concerned members were re-read: the two hooks
still land right after `OnImagesChanged()` in `Add` (l.280) and `RemoveAt` (l.907), and nothing
else in the design changes. The line numbers of § Overview, § Design and § Documentation were
refreshed; the concurrent-work note became a landed one. No open question.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Does not apply — no test project in the repository (see *Test Impact*) |
| README | | | With `RULES.md` and `GLOSSARY.md` |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which events put every video back to its first frame? (assumptions stated: they restart together, at the same instant, and keep playing; a swap or a layout change restarts nothing) | All four: the arrival of a video, of an animated GIF, of any image, and a deletion (×) | 2026-09-27 |
| 2 | What starts over, and where is the "first frame" when the Frames effect is on? | Every animated image (videos and GIFs), at its starting point; a frozen one stays frozen | 2026-09-27 |
| 3 | Does the soundtrack start over from its beginning at the same time? | Yes, with the videos | 2026-09-27 |
| 4 | Straightforward, or tricky / long? | Straightforward — one scouting pass | 2026-09-27 |
| 5 | The design is complete and no open question remains: implement? (No / the code / code, unit tests and documentation) | No — the gate holds | 2026-09-27 |

---

*Last updated: 2026-09-30*
