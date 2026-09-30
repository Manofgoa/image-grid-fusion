# Animated Zoom

> Working document — a new effect that zooms the image in and out continuously inside its cell,
> its speed set by a slider.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Inside the viewport a cell gives its image, the image **zooms in, then out, back and forth,
continuously** (a "breathing" / Ken Burns-like motion). A slider in the options toolbar sets the
**speed**. It is a **new effect with its own tab**, separate from the existing (static) Zoom effect,
and it follows every rule of RULES.md § *Effects*.

It **animates in the animated exports** (MP4, GIF); still exports (PNG, copy as image) take its
**starting state**.

Components concerned (from the scout pass):

| Component | Role today | What the effect needs |
|---|---|---|
| `Composition/ImageLook.cs` | Immutable per-image effect state; `ImageEffect` enum; `Zoom` + `Focus`, `Kept*` off-state memory, `TurnOn` / `TurnOff` / `Reset` | A new enum member and settings record (like `BlurEffect` / `FramesEffect`), its `Kept*` field and its cases |
| `Composition/Compositor.cs` `DrawCell` → `FitCalculator.ComputeTurned` | Applies `Zoom` / `Focus` / fine angle; no time parameter | A time value reaching `DrawCell`, turned into an extra zoom factor |
| `UI/AnimationPlayer.cs` | One async loop per **animated** source (33 ms), shared `Stopwatch`; stills never repaint | A preview driver that also repaints **still** images carrying the effect |
| `Imaging/GridExport.cs` `RenderAnimation` | Global `time = k / 30 s`; length = longest source loop | The same `time` passed to the effect; the zoom cycle counted in the length |
| `UI/MainForm.cs` | Tabs, options panels (`OptionSlider`), `HasAnimation` gates the GIF / MP4 export arrows | The tab, its icon, its options, and `HasAnimation` counting the effect |

---

## Behaviour

### Agreed

- **Its own effect**, with its own tab, activation checkbox, options and Reset, independent of the
  static Zoom effect. The tab is named **Animations**.
- **Continuous back-and-forth**: zooms in, then back out, in a loop, with no jump.
- **Smooth**: a sine wave, slowing down at both ends before turning back.
- **Fixed amplitude**: the zoom goes from the starting state to **+20 %** and back; only the speed
  is set.
- **Speed slider** = the **duration of one back-and-forth**, from **1 s to 30 s**, **6 s** by
  default, shown next to it (`6 s`); the slider's right end is the fastest (shortest cycle).
- **Centre**: the zoom multiplies the static Zoom, so it keeps the static Zoom's pan point
  (`Focus`) at the cell's centre, as the static Zoom does — "around the pan point" and "around the
  cell's centre" are the same thing here, except where the cover clamp shifts the image near an
  edge, exactly as for the static Zoom.
- **Exports**: the animated exports (MP4, GIF) show the motion; still exports show the **starting
  state**.
- **Starting state** = the bottom of the oscillation: the image as the other effects place it (the
  static Zoom included), with no extra zoom.
- Follows RULES.md § *Effects*: state on `ImageLook`, not persisted; off keeps its settings and draws
  as its defaults (no motion); Reset on image replaced, kept on swap and layout change; acting on
  any option turns it on.
- **Applies to every image** (still, video, animated GIF, frozen, file preview): its checkbox is
  never disabled.
- A cell carrying the effect (on) **counts as animation**: it enables the GIF / MP4 export arrows
  (`HasAnimation`) and makes MP4 the adapted default format, like a playing video.
- **A choice of animation kind**: the Animations options start with a **Type** drop-down holding
  **Zoom** only for now, so other kinds can be added later; the settings record carries the kind
  and the cycle duration.
- **The cycle is a playing loop**: a still carrying the effect (on) — a frozen image included —
  *plays* a loop as long as its cycle. It counts in `Animation.VideoLength` like a video's loop: the
  video length is the longest loop, zoom cycles included; with a soundtrack over a grid of stills,
  the zoom cycle gives the length (the soundtrack looped or cut to it). A shorter cycle starts over
  until the video ends, like any shorter content.
- **One clock**: the zoom follows the grid's clock (`AnimationPlayer`), the one the grid's start
  over resets. Turning the effect on or changing its duration takes the motion where that clock
  stands (a small jump); the preview always shows what the export gives.
- **Progress line**: a still carrying the effect shows it, following the zoom cycle; a video (or
  GIF) carrying it keeps its own loop's line.

---

## Rendering

- **Zoom factor at time *t***: `extra(t) = 1 + 0.2 × wave(t / cycle)`, where
  `wave(p) = (1 − cos 2πp) / 2` goes 0 → 1 → 0 over one cycle, smoothly. It **multiplies** the
  static Zoom, before
  the fine angle's cover zoom — `FitCalculator.ComputeTurned(cell, size, look.Zoom × extra(t), …)`.
- **Time reaches `DrawCell`** through the `Frame` it draws (a new field, `TimeSpan`), so the effect
  stays in one place (RULES.md § *Rendering*):
  - **Animated exports**: the global `time` of `GridExport.RenderAnimation` (frame *k* → *k* / 30 s).
  - **Still exports**: `TimeSpan.Zero` → starting state.
  - **Preview**: the preview's clock (see *Preview Driver*).
- **Resolution-independent**: the factor is a zoom ratio, identical at every size.

## Preview Driver

- `AnimationPlayer` only runs for animated sources; a grid of stills never repaints on its own.
- A **preview timer** (33 ms, same pace as `AnimationPlayer`) invalidates the cells whose image
  carries the effect (on), and only those; it stops when no cell carries it.
- Animated sources carrying the effect are repainted by that timer as well, so the zoom advances
  between two video frames.
- The zoom's time is read from `AnimationPlayer`'s clock, so the grid's **start over** (RULES.md
  § *Preview Playback*: an image arriving or deleted) also brings every zoom back to its starting
  state, at the same instant as the videos — the preview plays what an export gives. Turning the
  effect on or changing its duration does not restart anything: the motion is taken where the clock
  stands.

## Exports

- `HasAnimation` also counts the effect.
- **Length**: decided in `Animation.VideoLength` only (RULES.md § *Video Length*), so the export,
  the preview's soundtrack loop and the length readout follow at once. The zoom cycle of a still
  carrying the effect enters it as a playing loop (see *Behaviour*).
- **Crop**: the zoom applies to the cropped image, like the static Zoom (RULES.md § *The Crop
  Exception*) — nothing to add, it multiplies the static Zoom after the crop.

## UI

- **Effect tab**: **Animations**, after the Zoom tab (its only kind is a zoom), with its own icon in
  `EffectIcons`.
- **Options**: the **Type** drop-down (`Zoom`), the speed slider (cycle duration) with its value
  label (`OptionSlider`, like Zoom's), then the effect's Reset button. Changing the type or the
  duration turns the effect on (RULES.md § *Options Toolbar*).
- **Progress line** (`GridPreview`): drawn for a still carrying the effect, its fraction read from
  the clock over the zoom cycle.

---

## Test Impact

The solution holds **no test project** (`ImageGridFusion.slnx` lists the app only) and every
previous workfile stayed test-free — whether to create one is an Open Question.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(pending the test-project decision)* | | |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~**Tab name** — what is the effect called in the UI (English, like the other tabs)?~~ →
  **Animations**
- [x] ~~**Range** — how far does it zoom in: a fixed amplitude, an amplitude slider, or two
  bounds?~~ → **Fixed amplitude**, +20 %
- [x] ~~**Speed slider unit** — cycle duration in seconds, or an abstract speed; range and
  default?~~ → **Duration of one back-and-forth**, 1–30 s, 6 s by default
- [x] ~~**Easing** — smooth (sine, slows at both ends) or constant speed (triangle)?~~ → **Smooth
  (sine)**
- [x] ~~**Centre** — around the static Zoom's pan point (`Focus`), or always the cell's centre?~~ →
  **Not a choice**: the zoom multiplies the static Zoom, which already centres its pan point in the
  cell — both options coincide (see Iteration 2). Not asked.
- [x] ~~**Animations tab** — the name is plural: does the tab hold only this zoom motion, or a
  choice of animation kind (Zoom for now) leaving room for others?~~ → **A Type drop-down**, Zoom
  its only entry for now
- [x] ~~**Export length** — how the zoom cycle combines with the grid's length (longest video loop,
  or the Soundtrack's on a grid of stills), and what it gives alone on a grid of stills?~~ → **The
  cycle is a playing loop** in `Animation.VideoLength`: the longest loop wins, and it gives the
  length over a soundtrack on a grid of stills
- [x] ~~**Preview phase** — one shared clock for every cell, or the motion restarting from its
  starting state when the effect is turned on / its speed changes?~~ → **The grid's clock**; turning
  it on takes the motion where the clock stands
- [ ] **Unit tests** — stay test-free like every previous workfile?
- [x] ~~**Progress line** — the helper indicator now drawn under every playing cell (GLOSSARY):
  does a still image carrying the effect show one, following the zoom cycle; and on a video, whose
  loop does it follow?~~ → **Shown on a still**, over the zoom cycle; a video keeps its own loop's
  line

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the request (*"Effet de zoom — sur le viewport accordé à une cellule, proposer
un zoom avant/arrière sur l'image. Slider pour déterminer la vitesse"*) and the scoping batch
(Q&A 1–4): a separate effect with its own tab, a continuous back-and-forth, animated in the animated
exports and at its starting state in the still ones. Single scout pass (depth: straightforward) on
two angles — the existing Zoom effect as a template, and how time flows through the preview and the
exports. It found that `DrawCell` receives no time and that still images never repaint, hence the
`Frame` time field and the preview timer. Eight questions left open.

### Iteration 2 — 2026-09-26

First batch answered (Q&A 5–8): the tab is named **Animations**; a **fixed amplitude** (+20 %,
the example value of the chosen option); the speed slider sets the **duration of one
back-and-forth** (1–30 s, 6 s by default); a **smooth sine** motion. The *Centre* question is
dropped unasked: multiplying the static Zoom keeps its pan point at the cell's centre, so its two
options were the same behaviour. The plural tab name raises a new question (does the tab offer a
choice of animation kind?). The Soundtrack global effect, which now gives a grid of stills its
length (GLOSSARY), is folded into the export-length question.

### Iteration 3 — 2026-09-30

The second batch was dismissed and asked again. Meanwhile, the rules gained the **progress line**
(a helper indicator under every playing cell, following its loop): a new question, whether a still
carrying the effect shows one.

### Iteration 4 — 2026-09-30

The rules moved on while the questions waited: **Video Length** (one definition,
`Animation.VideoLength`, read by the export, the soundtrack loop and the length readout), **Preview
Playback** (an image arriving or deleted starts the grid over; an effect change does not) and the
**Crop** (zoom applies to the cropped image). The design now reads the length from
`Animation.VideoLength` and the zoom's time from `AnimationPlayer`'s clock, so the start over
covers it; the export-length question is reframed on `VideoLength` (does a zoom cycle count as a
playing loop, even against a soundtrack?).

### Iteration 5 — 2026-09-30

Second batch answered (Q&A 10, 11, 13, 14): the Animations options start with a **Type** drop-down
(Zoom only, for now); a zoom cycle is a **playing loop** in `Animation.VideoLength` — the longest
loop wins, and it gives the length over a soundtrack on a grid of stills; the motion follows the
**grid's clock**, so turning the effect on catches it mid-cycle but the preview matches the export;
a still carrying the effect shows the **progress line** over its cycle. Placement decided with it:
the Animations tab right after Zoom. Only the unit-test question remains.

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
| 1 | Where does the animated zoom live: a new effect with its own tab, or an option of the existing Zoom? | New effect, its own tab | 2026-09-26 |
| 2 | Which motion: continuous back-and-forth, direction chosen in the options, or one way looping? | Continuous back-and-forth | 2026-09-26 |
| 3 | Exports: animated in the animated exports (stills take the starting state), or preview only? | Animated in the animated exports | 2026-09-26 |
| 4 | Exploration depth: straightforward, or tricky / long? | Straightforward | 2026-09-26 |
| 5 | Tab name (Zoom motion, Pulse, Breathe, Ken Burns)? | Animations (typed by the user) | 2026-09-26 |
| 6 | Range: amplitude slider, fixed amplitude, or two bounds? | Fixed amplitude | 2026-09-26 |
| 7 | Speed slider: duration of one back-and-forth (1–30 s, 6 s), or abstract speed 1–10? | Duration of one back-and-forth | 2026-09-26 |
| 8 | Easing: smooth (sine) or constant speed? | Smooth (sine) | 2026-09-26 |
| 9 | Centre: the static Zoom's pan point, or the cell's centre? | Not asked — both options coincide (Iteration 2) | 2026-09-26 |
| 10 | Export length with videos / Soundtrack, and on a grid of stills? | The cycle counts as a playing loop | 2026-09-26 |
| 11 | Preview phase: shared clock, or restart on activation / speed change? | The grid's clock | 2026-09-26 |
| 12 | Unit tests: stay test-free? | | 2026-09-26 |
| 13 | Animations tab: only the zoom motion, or a choice of animation kind? | A choice of kind (Zoom for now) | 2026-09-26 |
| 14 | Progress line: shown on a still carrying the effect (zoom cycle)? Which loop on a video? | Shown on a still, over the zoom cycle; a video keeps its own loop | 2026-09-30 |

---

*Last updated: 2026-09-30*
