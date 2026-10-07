# Wheel Zoom Step

> Working document — the mouse wheel zooms by steps of 5 %, 1 % with Ctrl held.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the wheel over a cell zooms **geometrically** — 4 notches double the zoom
(`GridPreview.NotchesPerDoubling`), crossing 100 % stops on it — and **Ctrl + wheel** moves it onto
the next multiple of 5 % (`workfiles/20260926-ctrl-wheel-5-percent-step.md`). The Zoom slider takes
the stock wheel (its log-scale units), and its Ctrl + wheel steps the zoom by 5 % like the cell
(`MainForm.StepZoom`, wired through `StepSlider.ControlWheel`).

The request swaps this around for the zoom: **the plain wheel steps by 5 percentage points, Ctrl by
1 point** — **25 and 5 points above 200 %** — on the cell and on the Zoom slider's wheel alike. The
other option sliders keep their Ctrl + wheel = 5 %.

Components:

| Component | Role today |
|---|---|
| `UI/WheelSteps.cs` | `Snap(value, step, notches)`, `Percent = 5`, `WithControl(Message)` |
| `UI/GridPreview.cs` — `OnMouseWheel`, `ZoomAt` | Wheel over a cell: geometric, or `Snap(…, 5, …)` with Ctrl |
| `UI/StepSlider.cs` | Option slider: stock wheel, `ControlWheel` / `ControlStep` with Ctrl |
| `UI/MainForm.cs` — `_zoom`, `StepZoom`, `SetZoom` | The Zoom slider, log scale (`Log2(zoom) × 100`), 10 %–1600 % |
| `README.md` lines 28 and 78 | Describe the wheel and Ctrl + wheel |

---

## Wheel over a Cell

- One notch moves the zoom by a number of **percentage points** (additive: 100 → 105 → 110 %, not
  ×1.05), coarser above 200 %:

  | Zoom range the notch moves in | Plain wheel | Ctrl + wheel |
  |---|---|---|
  | Up to 200 % | 5 points | 1 point |
  | Above 200 % | 25 points | 5 points |

- The range is the one the notch **moves into**: up from 200 % or more, or down from above 200 %,
  takes the coarse step; so 190 → 195 → 200 → 225 → 250 up, and 250 → 225 → 200 → 195 down. 200 %
  being a multiple of every step, the two ranges join on it.
- From a value between two multiples of the step, the first notch **stops on the next multiple in
  its direction** — `WheelSteps.Snap` as it is: 103 % → 105 % up, → 100 % down; 213 % → 225 % up,
  → 200 % down; with Ctrl, 103.4 % → 104 % up, → 103 % down.
- Several notches at once (a fast wheel) are taken **one at a time**, each with the step of the
  range it moves into, so a burst crossing 200 % lands where the same notches one by one would.
- The zoom stays within **10 %** and the **maximum zoom** (§ Maximum Zoom, 2000 % by default).
- 100 % being a multiple of both steps, the "crossing 100 % stops on it" rule holds by itself.
- Everything else is unchanged: zoom around the point under the mouse, live then smoothed when the
  wheel stops, the zoom badge, the cells where the wheel does nothing (crop edit view, a gesture
  running, export).
- The geometric zoom (`NotchesPerDoubling`) has no more use and goes.

## Zoom Tab Controls

- The **Zoom slider's wheel** follows the same steps as the cell — the table above, plain and with
  Ctrl — applied exactly (the log scale is too coarse at high zooms — the reason `StepZoom` exists).
  It therefore takes over the plain wheel too, not only Ctrl + wheel.
- The slider's **drag**, its snap to 100 % near its mark, and its **keyboard** (arrows, Page Up /
  Down, in its log units) are unchanged.

## Other Option Sliders

- **Unchanged**: opacities, volume, fine angle, borders' thickness, Frames… keep the stock wheel and
  **Ctrl + wheel = 5 %**. Ctrl therefore means *coarse* on them and *fine* on the zoom — accepted.

## Maximum Zoom

- The maximum zoom is **2000 %** by default (1600 % before), an **app setting** kept in
  `settings.json` (RULES.md § App Settings) under `"MaxZoom"`, **in percent** (`"MaxZoom": 2000`).
  **No UI control**: it is edited by hand in the file, and read **once at start-up** — a change
  takes effect at the next launch.
- **Accepted values**: whole numbers from **200** to **10 000**. At start-up the value applied is:

  | In the file | Applied, and written back into the file |
  |---|---|
  | A whole number from 200 to 10 000 | That value — the file is left as it is |
  | Above 10 000 | 10 000 |
  | Below 200 | 200 |
  | Missing, not a whole number, unreadable | 2000 |

- **The file always shows the value applied**: whenever it does not hold it, it is written at
  start-up, so the setting can be found and read. A write that fails says so in the status line
  (§ App Settings), the value applied all the same.
- It bounds everything the zoom's maximum bounds: the zoom itself (`ImageLook.WithZoom`), the Zoom
  slider's range, the wheel over a cell and over the slider.
- The Composition layer may not reference `UI/AppSettings.cs`: `ImageLook.MaxZoom` becomes a value
  set at start-up (`Program.Main`) from the setting, before the main window is built.

---

## Test Impact

Not applicable — the repository has no test project (as for
`workfiles/20260926-ctrl-wheel-5-percent-step.md`). `WheelSteps.Snap` stays a pure static function,
ready to be pinned the day one exists.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~What is a "5 % step"?~~ → Percentage points, additive (100 → 105 → 110 %)
- [x] ~~From an off-grid value (103 %), what does a plain notch give?~~ → It snaps onto the next
  multiple of 5 in its direction (105 % up, 100 % down)
- [x] ~~Where does the new step apply?~~ → The wheel over a cell **and** the Zoom tab's controls
- [x] ~~At high zooms a 5-point step is small (100 → 1600 % takes 300 notches, against 16 today):
  5 points everywhere, or a coarser step above some zoom?~~ → Above 200 %, 25 points (5 with Ctrl)
- [x] ~~The other option sliders: keep Ctrl + wheel = 5 % (Ctrl then means the opposite on the zoom),
  or follow the zoom (plain wheel = 5 %, Ctrl = finest unit)?~~ → Unchanged
- [x] ~~The Zoom slider's keyboard (arrows, Page Up / Down): unchanged, or 5 / 1 points as well?~~ →
  Unchanged
- [x] ~~The maximum zoom in `settings.json`: in percent (`"MaxZoom": 2000`) or as a factor (`20`)?~~
  → In percent
- [x] ~~Written into the file by the app when missing, so it can be found and edited — or only read,
  absent meaning 2000 %?~~ → Written when missing
- [x] ~~A value above 10 000 overwritten in the file at start-up: with 10 000 (the cap) or 2000 (the
  default)? And a value below 200, or unreadable: overwritten too?~~ → Clamped: above 10 000 →
  10 000, below 200 → 200, unreadable → 2000, each written back into the file
- [x] ~~Which values are accepted (others falling back to 2000 %)?~~ → 200 to 10 000 *(revised
  2026-10-07, see Iteration 7: out of range is clamped, not reset to 2000)*

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-06

Initial design from the request ("le zoom molette doit être de pas de 5 % ; si Ctrl maintenu, 1 %")
and the scoping batch: additive steps snapped onto their multiples, on the cell and on the Zoom
slider's wheel. The exploration found that the request inverts the current behaviour (Ctrl gave
the 5 % step, the plain wheel was geometric), which raises the three open questions above.

### Iteration 2 — 2026-10-06

Answers to the three open questions: above 200 % the steps become 25 points (5 with Ctrl); the
other option sliders and the Zoom slider's keyboard stay as they are. The agent settled the
boundary: the step is the one of the range a notch moves into, notches taken one at a time, so
200 % joins both ranges (… 195 → 200 → 225 …).

### Iteration 3 — 2026-10-06 — ✅ Implemented

Go given ("vas-y dans un worktree, code et doc"): code and README; unit tests not applicable (no
test project). The run works in the worktree `.claude/worktrees/wheel-zoom-step` on
`feature/wheel-zoom-step`, fast-forwarded into `main` and removed at the end.

### Iteration 4 — 2026-10-06 — 🧭 Implementation choices

No rule broken, no divergence from the frozen design. Choices the design left open:

- **`WheelSteps.Zoom(percent, notches, fine)`** holds the rule — constants `ZoomStep` (5),
  `FineZoomStep` (1), `CoarseZoomFrom` (200), `CoarseZoomFactor` (5) — and loops one notch at a
  time over the existing `Snap`; `Percent` stays the other sliders' Ctrl + wheel step. Not clamped:
  the callers clamp (`ImageLook.WithZoom`, `StepZoom`).
- **`GridPreview.ZoomAt`** keeps its "crossing 100 % stops on it" check: a single notch always lands
  on 100 % anyway, but a burst of notches (fast wheel) crossing it still stops there, as before.
  `NotchesPerDoubling` is removed.
- **`StepSlider.Wheel`** (`Action<int, bool>`, notches + Control held) is a new hook taking over the
  whole wheel, winning over `ControlWheel`; the zoom slider uses it (`StepZoom(notches, fine)`), the
  Frames slider keeps `ControlWheel`.
- **README**: the Features line on the zoom, the Ctrl + wheel paragraph of § Effects (the zoom
  slider now the exception) and § Zoom's options line.

### Iteration 5 — 2026-10-06 — ⚙️ Post-implementation — Maximum zoom in settings.json

Requested after the user's test ("on va augmenter le zoom max à 2000 % et on va mettre cela dans
le fichier de settings .json sans pour autant le rendre personnalisable dans l'UI"): the maximum
zoom goes from 1600 % to **2000 %**, and is read from `settings.json` (§ App Settings) with no UI
control. `ImageLook.MaxZoom` is a constant of the Composition layer, which may not reference
`UI/AppSettings.cs`: the value has to be handed down at start-up. Open questions 7–9 below.

### Iteration 6 — 2026-10-07 — ⚙️ Post-implementation — Maximum zoom: answers and overwrite

Answers: in percent, written into the file when missing, 200 to 10 000 accepted (§ Maximum Zoom).
Then a new request, while the adjustment started: "si valeur > 10 000 lors de l'ouverture au
démarrage, overrider la valeur dans le .json et l'écrire" — a value above 10 000 is replaced in the
file at start-up, not only ignored. Its replacing value is an open question.

### Iteration 7 — 2026-10-07 — ⚙️ Post-implementation — Maximum zoom clamped and written back

Answers to questions 13–14: a value above 10 000 becomes 10 000, below 200 becomes 200 (clamped,
no longer reset to 2000), an unreadable one 2000 — and in every case the file is rewritten with the
value applied, so it always shows it (§ Maximum Zoom).

Implemented in the worktree `.claude/worktrees/max-zoom-setting` (`feature/max-zoom-setting`),
fast-forwarded into `main` and removed. Choices the design left open, no rule broken:

- **`AppSettings.MaxZoom`** (percent, clamped, 2000 when missing or not a whole number),
  `HoldsMaxZoom`, `SaveMaxZoom`, constants `DefaultMaxZoom` / `MinMaxZoom` / `MaxMaxZoom`.
- **`ImageLook.MaxZoom`** becomes a settable static property (20 until set), set in `Program.Main`
  before the main window is built — its Zoom slider takes its range from it at construction.
- **The write-back** is `MainForm.WriteMaxZoom`, called at the start of the constructor so a failure
  can reach the status line; it runs with a hidden start (`--tray`) too.
- Checked by hand on the worktree build: `"MaxZoom": 50000` was rewritten to `10000` at launch.
- **README** (English and French): the Features zoom line, § Zoom's options line and the
  *Settings file* paragraph, which now describes `"MaxZoom"`. RULES.md and the glossary unchanged.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 7 | 2026-10-06, 2026-10-07 | `WheelSteps.Zoom`, `GridPreview.ZoomAt`, `StepSlider.Wheel`, `MainForm.StepZoom`; then the maximum zoom setting (`AppSettings.MaxZoom`, `ImageLook.MaxZoom`, `MainForm.WriteMaxZoom`) |
| Unit tests | — | — | Not applicable — no test project |
| README | 3, 7 | 2026-10-06, 2026-10-07 | Features (zoom), § Effects (Ctrl + wheel), § Zoom; then the maximum zoom, English and French |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What is a "5 % step" — percentage points or a ×1.05 factor? | Percentage points | 2026-10-06 |
| 2 | From an off-grid value (103 %), a plain notch: snap onto a multiple of 5, or a pure ±5? | Snap onto the multiple of 5 | 2026-10-06 |
| 3 | Where does the new step apply: the wheel over a cell only, or the Zoom tab's controls too? | The wheel and the Zoom controls | 2026-10-06 |
| 4 | Depth of the exploration: straightforward, or tricky / long? | Straightforward | 2026-10-06 |
| 5 | High zooms: 5 points everywhere, or a coarser step above some zoom? | "5 % devient 25 % au-dessus de 200 %, 1 % devient 5 % au-dessus de 200 %" | 2026-10-06 |
| 6 | The other sliders: keep Ctrl + wheel = 5 %, or follow the zoom? | Unchanged | 2026-10-06 |
| 7 | The Zoom slider's keyboard: unchanged, or 5 / 1 points? | Unchanged | 2026-10-06 |
| 8 | Go for the implementation? | Code and documentation, in a worktree | 2026-10-06 |
| 9 | Is the task finished? | No — maximum zoom to 2000 %, read from settings.json, no UI (Iteration 5) | 2026-10-06 |
| 10 | The maximum zoom's unit in settings.json: percent or factor? | Percent | 2026-10-07 |
| 11 | Written into the file when missing, or only read? | Written when missing | 2026-10-07 |
| 12 | Which values are accepted? | 200 to 10 000 | 2026-10-07 |
| 13 | A value above 10 000 overwritten with 10 000 (the cap) or 2000 (the default)? | 10 000 (the cap) | 2026-10-07 |
| 14 | A value below 200 or unreadable: overwritten too, or only ignored? | Overwritten too | 2026-10-07 |

---

*Last updated: 2026-10-07*
