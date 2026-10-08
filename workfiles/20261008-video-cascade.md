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
- **Loop**: once the last one has played, the cascade starts over from the first — every content
  back on its starting point.

To settle (see § Open Questions): what one turn plays, its sound, the video length, the contents
that animate without taking part (PDF pages, long texts, Animations motions), the effect's options,
its availability, the restart on toggle, the progress line.

---

## Timeline Model (proposal)

One pure computation in `Composition`, read by every consumer — like `Animation.VideoLength`:

- A **cascade schedule** built from the ordered images: per taking-part image a **turn** —
  `[offset, offset + turn length)` on the grid's clock — the offsets the running sum of the previous
  turns (plus the gap, if Q&A decides one).
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
- Options: see § Open Questions.

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

- [ ] **Turn content**: what does one turn play? *Proposal*: from its **starting point to the end of
  its trim**, once (turn length = `PlayedLength − StartTime`). Alternative: a whole loop of the played
  part starting at the starting point (wrapping round to it).
- [ ] **Sound**: is a content heard **only during its turn**, silent while it waits? *Proposal*: yes.
- [ ] **Video length**: *Proposal*: the **sum of the turns**; an Animations cycle longer than that sum
  still sets the length (`Animation.LoopOf` unchanged for motions); the soundtrack loops or is cut on
  it as today; the Fade fades the whole cascade's start and end.
- [ ] **Animated contents not taking part** — PDF of several pages, text longer than its cell,
  Animations motion on a still: *Proposal*: they keep playing **in parallel** on the grid's clock, as
  today.
- [ ] **Options**: does the effect have options? *Proposal*: a **Gap** slider (pause between two
  turns, 0 to 3 s, 0 by default), plus its own Reset. Alternative: no option, the tab's checkbox only.
- [ ] **Availability**: *Proposal*: disabled, with a tooltip, while fewer than **two** contents can
  take part.
- [ ] **Restart on toggle**: RULES.md § Preview Playback says an effect change does not start the grid
  over. *Proposal*: turning the Cascade on or off (and changing the Gap) **starts the grid over**, so
  the first video plays at once — an amendment of § Preview Playback.
- [ ] **Progress line**: *Proposal*: drawn on the playing content only, its turn's progress; none on
  the waiting ones.

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
| 5 | Turn content, sound, video length, non-taking-part contents | | 2026-10-08 |
| 6 | Options, availability, restart on toggle, progress line | | 2026-10-08 |

---

*Last updated: 2026-10-08*
