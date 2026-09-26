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
  static Zoom effect.
- **Continuous back-and-forth**: zooms in, then back out, in a loop, with no jump.
- **Speed slider** in its options.
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

### To Settle

See *Open Questions*: the tab's name, the range of the zoom, the unit of the speed slider, the
easing, the centre of the zoom, the export length, the preview's phase, tests.

---

## Rendering

- **Zoom factor at time *t***: `extra(t) = 1 + amplitude × wave(t / cycle)`, where `wave` goes
  0 → 1 → 0 over one cycle (shape: see *Open Questions*). It **multiplies** the static Zoom, before
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

## Exports

- `HasAnimation` also counts the effect.
- **Length**: see *Open Questions* (how a zoom cycle combines with the longest video loop).

## UI

- **Effect tab**: after the Zoom tab (geometry group), with its own icon in `EffectIcons`.
- **Options**: the speed slider with its value label (`OptionSlider`, like Zoom's), then the other
  options this design settles, then the effect's Reset button.

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

- [ ] **Tab name** — what is the effect called in the UI (English, like the other tabs)?
- [ ] **Range** — how far does it zoom in: a fixed amplitude, an amplitude slider, or two bounds?
- [ ] **Speed slider unit** — cycle duration in seconds, or an abstract speed; range and default?
- [ ] **Easing** — smooth (sine, slows at both ends) or constant speed (triangle)?
- [ ] **Centre** — around the static Zoom's pan point (`Focus`), or always the cell's centre?
- [ ] **Export length** — how the zoom cycle combines with the longest video loop (and alone, on a
  grid of stills)?
- [ ] **Preview phase** — one shared clock for every cell, or the motion restarting from its starting
  state when the effect is turned on / its speed changes?
- [ ] **Unit tests** — stay test-free like every previous workfile?

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
| 5 | Tab name? | | 2026-09-26 |
| 6 | Range of the zoom? | | 2026-09-26 |
| 7 | Speed slider unit, range and default? | | 2026-09-26 |
| 8 | Easing: smooth or constant speed? | | 2026-09-26 |
| 9 | Centre: the static Zoom's pan point, or the cell's centre? | | 2026-09-26 |
| 10 | Export length with videos, and on a grid of stills? | | 2026-09-26 |
| 11 | Preview phase: shared clock, or restart on activation / speed change? | | 2026-09-26 |
| 12 | Unit tests: stay test-free? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
