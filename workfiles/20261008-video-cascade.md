# Video Cascade

> Working document — a global effect playing the grid's videos one after the other, in cell order,
> instead of all at once.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every playing content of the grid plays **at once**, on one clock, each looping on its played
part (RULES.md § Video Length, § Played Part); the grid's loop is the longest one.

The **Cascade** global effect turns that into a **succession**: the first video plays, then the
second, then the third… each one starting when the previous one ends, the others standing still
meanwhile. Once the last one has played, the cascade starts over from the first.

It is a **global effect** (RULES.md § Global Effects): a tab of the Global toolbar with its activation
checkbox, its options in the global options toolbar, part of the undo step, not persisted.

Components concerned (read during exploration):

| Concern | Where it lives today |
|---|---|
| Content time of a playing image, preview | `SourceImage.ContentTime`, `AnimationPlayer.RunAsync` / `Position` |
| Content time, export | `GridExport.Item.ContentTime`, `GridExport.RenderAnimation` |
| Grid loop / video length | `Animation.GridLength`, `Animation.LoopOf`, `Animation.VideoLength` |
| Sound, preview | `PreviewSound.Span` / `Sync`, driven by `AnimationPlayer.RunAsync` |
| Sound, export | `MixedSound(Path, Loop, Start, Gain, From)`, `VideoEncoder.Mixer` / `Voice` |
| Length readout | `MainForm` (around `UpdateButtons`, the `⏱` label) |
| Global effect wiring | `GlobalEffect` enum, `MainForm` (`_seams` / `_seamsOn` as the latest model), `GridHistory.GlobalState` |

---

## Behaviour

Agreed in the scoping batch (Q&A 1–3):

- **Order**: the **cells' order** — cell 1, then 2, then 3… as the layout numbers its cells
  (`GridPreview.Images`). A swap or a layout change changes the order.
- **Who takes part**: every content that **plays** and has frames — a **video** or an **animated
  GIF** (`SourceImage.Plays && Trims`). A frozen one is a still and takes no turn. Stills stay stills.
- **Waiting**: a taking-part content not playing its turn stands **still**:
  - **before its turn**: on its **starting point** (its Frames effect's, `SourceImage.StartPage`);
  - **after its turn**: on the **last frame it played**.
- **A turn**: **one whole loop** of the content's played part (its trim), from its **starting point**
  round to it again — turn length = `SourceImage.PlayedLength`. With the starting point on the trim's
  first frame, it plays From → To; otherwise it wraps at To back to From and ends just before the
  starting point. *The last frame it played* is therefore the frame just before its starting point
  (To when the starting point is the trim's first frame).
- **Sound**: a content is heard **during its turn only** — silent while it waits, as a frozen one.
- **Loop**: once the last one has played, the cascade starts over from the first — every content
  back on its starting point.
- **Video length** (amends RULES.md § Video Length while the Cascade is on): the **sum of the turns**;
  an Animations cycle longer than that sum still sets the length; the soundtrack loops or is cut on it
  as today; the Fade fades the start and the end of the whole cascade.
- **Not taking part**: PDF pages, texts longer than their cell and Animations motions keep playing
  **in parallel** on the grid's clock, as today.

- **Pause**: the effect's one option, a pause of 0 to 3 s (0 by default, 0.1 s steps) **after every
  turn** — the last one included, so the exported loop pauses evenly when it starts over. During a
  pause every taking-part content stands still, the one just played on its last played frame. The
  video length is then the sum of the turns **and** pauses.
- **Restart**: turning the Cascade on or off, and changing its Pause, **starts the grid over**
  (`AnimationPlayer.Restart`) — the first content plays at once. Amends RULES.md § Preview Playback,
  where an effect change otherwise keeps the images playing as they are.
- **Progress line**: drawn on the content **playing its turn** only, showing its turn's progress;
  none on the waiting ones (nor during a pause).

---

## Timeline Model (proposal)

One pure computation in `Composition`, read by every consumer — like `Animation.VideoLength`:

- A **cascade schedule** built from the ordered images: per taking-part image a **turn** —
  `[offset, offset + turn length)` on the grid's clock — the offsets the running sum of the previous
  turns and of the pauses after them.
- At grid time `t` (taken within the cascade's loop), a taking-part image shows:
  before its turn → its starting point; inside → its content time; after → its last played frame.
- The preview (`AnimationPlayer`), the export (`GridExport.Item`), both sounds and the length readout
  read it there; none re-derives it. Off, every consumer behaves exactly as today.

---

## Global Effect

- A **Cascade** tab in the Global toolbar, **after Seams** (`GlobalEffect.Cascade`), with an icon in
  `EffectIcons`.
- Off at start-up; on / off and its settings in `GlobalState` (undo step), not persisted; reset by the
  Global Resets and *Clear all*, locked while exporting.
- Options: the **Pause** slider (§ Behaviour), then the effect's own Reset.
- **Availability**: disabled, with a tooltip saying why, while **no** content can take part — no
  video or animated GIF playing. With a single one, it simply plays alone, then pauses.

---

## Test Impact

The repository has **no test project** (`src/ImageGridFusion/ImageGridFusion.csproj` only): **no
unit test is created or updated**. The check is manual, in the running app — the succession in the
preview, its sound, the length readout, an MP4 and a GIF export.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project) | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 — not only the
blocking ones. Each one carries the agent's proposal.

- [x] ~~**Turn content**: what does one turn play?~~ → **One whole loop** of its played part, from its
  starting point round to it again (turn length = `PlayedLength`)
- [x] ~~**Sound**: is a content heard only during its turn?~~ → Yes, silent while it waits
- [x] ~~**Video length**~~ → The **sum of the turns**; an Animations cycle longer than that sum still
  sets the length; the soundtrack loops or is cut on it as today; the Fade fades the whole cascade's
  start and end
- [x] ~~**Animated contents not taking part** (PDF pages, long text, Animations motion)~~ → They keep
  playing **in parallel** on the grid's clock, as today
- [x] ~~**Options**~~ → A **Pause** slider (0 to 3 s, 0 by default), plus the effect's own Reset
- [x] ~~**Availability**~~ → Disabled, with a tooltip, while **no** content can take part (at least
  one video or animated GIF playing) — not the proposed two
- [x] ~~**Restart on toggle**~~ → Yes: turning the Cascade on or off, and changing its Pause, starts
  the grid over — an amendment of RULES.md § Preview Playback
- [x] ~~**Progress line**~~ → On the content playing its turn only, its turn's progress; none on the
  waiting ones

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request (« effet global de succession de vidéo en cascade : la première est
jouée puis la 2 puis 3 etc ») and the scoping batch: cell order, videos and animated GIFs taking
part, still on their starting point before their turn and on their last played frame after it. A
single timeline computation proposed; the remaining points listed as Open Questions with proposals.

### Iteration 2 — 2026-10-08

Q&A 5 answered: a turn plays **one whole loop** of the played part from the starting point (not the
proposed starting point → end of trim); sound during the turn only; video length = sum of the turns;
non-taking-part contents in parallel. § Behaviour updated.

### Iteration 3 — 2026-10-08

Q&A 6 answered: a Pause slider, the effect available from **one** taking-part content (not the
proposed two), on / off and Pause starting the grid over, the progress line on the playing content
only. The pause is placed after every turn, the last one included, so the exported loop pauses
evenly. No Open Question left.

### Iteration 4 — 2026-10-08 — ✅ Implemented

Go given: **code, unit tests and documentation**, in a **worktree** (`.claude/worktrees/video-cascade`,
branch `feature/video-cascade`). The design sections as they stand are the frozen scope.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Order of the cascade? | Cell order (reading order of the grid) | 2026-10-08 |
| 2 | What does a video show while not playing (before / after its turn)? | Still on its starting point before, on its last played frame after | 2026-10-08 |
| 3 | Which contents take part? | Videos and animated GIFs (playing, not frozen) | 2026-10-08 |
| 4 | Depth of the exploration? | Straightforward — single scout pass | 2026-10-08 |
| 5 | Turn content, sound, video length, non-taking-part contents | One whole loop of the played part from its starting point · heard during its turn only · sum of the turns (a longer Animations cycle wins, soundtrack and Fade as today) · the others play in parallel | 2026-10-08 |
| 6 | Options, availability, restart on toggle, progress line | Pause slider (0–3 s) · available with at least one video / GIF · on / off and Pause start the grid over · progress line on the playing content only | 2026-10-08 |

---

*Last updated: 2026-10-08*
