# Crop Threshold Slider

> Working document — a slider to adjust the crop threshold (15% today) from the UI.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The fitting rule crops at most a fixed share of the overflowing axis (15%, `FitCalculator.DefaultCropThreshold`)
before falling back to bands filled with the dominant color. The goal is to let the user tune that share
from the main window, with the preview and the exported image (copy / save) following the chosen value.

Listed under `## Planned` in the README: *"Crop threshold slider, to adjust the 15% limit from the UI."*

Relevant components:

| Component | Role |
|---|---|
| `Composition/FitCalculator.cs` | Holds `DefaultCropThreshold = 0.15`; `Scale` / `Compute` take a `threshold` parameter (clamped to [0, 0.99]) — unchanged |
| `Composition/CanvasSizer.cs` | `Compute(images, layout, threshold)` — the output width depends on the threshold — unchanged |
| `Composition/Compositor.cs` | `Render(images, layout, threshold)` and `Draw(g, images, layout, canvas, threshold)` — unchanged |
| `UI/GridPreview.cs` | Holds `CropThreshold`; passes it to `Compositor.Draw`; setting it drops the cached render and repaints |
| `UI/MainForm.cs` | Top bar = slider + value label; renders copy and save with `_preview.CropThreshold`; left `LayoutStrip`; bottom bar = status line (left) + Copy / Save (right) |

---

## Agreed Design

| Aspect | Decision |
|---|---|
| Placement | A **new top bar**, docked at the top of the window across its whole width (above the layout strip), always visible; it is meant to host future settings too. The bottom bar (status + Copy / Save) is unchanged |
| Control | A slider (`TrackBar`, no ticks, 160 × 26 logical px so the thumb is level with the label) followed on its right by a **value label**, e.g. `Crop: 15%`, updated as the slider moves — the label comes after the slider so its changing width never moves it |
| Range | 0% – 50%, step 5% (11 positions) |
| Default | 15% (`FitCalculator.DefaultCropThreshold`, unchanged) |
| Update | **Live**: the preview re-renders on every slider position while dragging, not only on release |
| Persistence | Session only — back to 15% at every launch; no settings file |

### Behaviour

- The slider value is the total share of the overflowing axis that may be cropped (split evenly on both sides),
  exactly as the fitting rule describes it today.
- At 0%, images are fitted without any crop (bands on the non-overflowing axis). At 50%, up to half of the
  overflowing axis may be cropped.
- The slider position is a step index (0 to 10); the window turns it into a percentage (index × 5) for the label
  and a fraction for the preview, so the mouse can never land between two steps.
- A single threshold value, set by the window's slider and held by `GridPreview.CropThreshold`, drives both:
  - the **preview** — setting `CropThreshold` drops the cached render and repaints;
  - the **export** — `Compositor.Render(_preview.Images, _preview.ActiveLayout!, _preview.CropThreshold)` for copy and save.
- Preview and export always use the same value — export reads it from the preview — what you see is what you copy.
- The output canvas size (`CanvasSizer`) depends on the threshold, so the exported resolution may change with it;
  this is expected and needs no extra handling.

### Composition layer

No change: `FitCalculator`, `CanvasSizer` and `Compositor` already take the threshold as a parameter.
`DefaultCropThreshold` stays the initial slider value.

### README

*Not updated — the go covered the code only (see Iteration 3). Planned changes, still pending:*

- `## Fitting rules`: describe the threshold as adjustable (0–50%, step 5%, 15% by default, not remembered between launches)
  instead of a fixed 15%.
- `## Output` or `## Features`: mention the slider.
- `## Planned`: remove the *Crop threshold slider* entry.

---

## Test Impact

**None — deliberately.** No test project exists in the repository, the composition layer already takes the
threshold as a parameter, and the new behaviour is UI wiring only. The user chose not to create a test project
for this task (Q&A #8).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~The app has no top toolbar: where does the slider go — in the existing bottom bar (between the status line and Copy / Save), or in a new top bar?~~ → New top bar
- [x] ~~Does the preview update live while the slider is dragged, or only when it is released?~~ → Live
- [x] ~~What does the slider show next to it — a label with the value (e.g. `Crop: 15%`), a tooltip only, or nothing?~~ → A label with the value
- [x] ~~Create a unit-test project to pin the threshold bounds of `FitCalculator`, or leave tests out (UI wiring only)?~~ → No tests
- [ ] *(raised during implementation, not implemented)* At startup the slider is the only enabled focusable control (Copy / Save are disabled until an image is added), so it takes the keyboard focus: arrow keys, `Home` / `End` and the mouse wheel then change the threshold without the user aiming at it. Keep it, or take the slider out of the startup focus (e.g. focus the preview, or `TabStop = false`)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design from the scoping batch: slider in the main bar, range 0–50% by steps of 5%, default 15%,
session-only value. Exploration showed the composition layer is already parameterized, so the work is limited
to `MainForm` (slider, export calls) and `GridPreview` (threshold property, cache invalidation), plus the README.
Four open questions remain: exact placement (no top toolbar exists), live vs on-release update, value display,
and whether to create a test project.

### Iteration 2 — 2026-09-23

Open questions answered: the slider goes in a **new top bar** (not the existing bottom bar), the preview updates
**live** while dragging, a **value label** (`Crop: 15%`) sits next to the slider, and **no test project** is
created — `## Test Impact` is now explicitly empty. Design sections updated accordingly.

### Iteration 3 — 2026-09-24 — ✅ Implemented

Go given on 2026-09-24 (after a first "No" on 2026-09-23), as a free-form "GO implémente" rather than one of the
three choices; read as the closest one, **Implement the code** — README not covered. Work done in a dedicated
worktree (`.claude/worktrees/crop-threshold-slider`, branch `feature/crop-threshold-slider`), removed at the end
of the run; the branch is kept.

### Iteration 4 — 2026-09-24 — 🧭 Implementation choices

Choices the frozen design did not state, taken during the run:

- **Base moved**: `main` had advanced since the design (layout variants, `LayoutStrip` docked left); the worktree
  branched from local `main` at `7eb1142`. The wiring follows the new signatures (`Compositor.Render/Draw` take a
  `GridLayout`), with no change to the design itself.
- **Where the value lives**: the design said "owned by the window"; the slider in `MainForm` is the source, and the
  value is stored in `GridPreview.CropThreshold`, which the export reads back — one stored value, so preview and
  export cannot drift apart.
- **Slider unit**: `TrackBar` range 0–10 (step index), not 0–50 (percent), so a mouse drag cannot stop at 7% or 12%.
- **Label position**: to the **right** of the slider, not before it: `Crop: 5%` / `Crop: 50%` differ in width and would
  shift the slider while it is dragged.
- **Slider size**: no ticks, `AutoSize = false`, 160 × 26 — with the default automatic height (56 px at 150%) the thumb
  sat well above the vertically centered label.
- **Top bar docking**: added last so it docks first and spans the whole width, above the layout strip; the docking
  comment was rewritten to describe the four docked controls.
- **`[DesignerSerializationVisibility(Hidden)]`** on `GridPreview.CropThreshold`: the WinForms analyzer (WFO1000)
  fails the build on a public settable control property otherwise; same attribute as `LayoutStrip.ActiveLayout`.

No project rule was broken. Out of scope, raised as an open question: the slider takes the keyboard focus at startup.

Verified by hand in the running app (UI Automation + window captures): starts at `Crop: 15%`, thumb level with the
label; with a 3:1 image, 0% shows the whole image with bands, 15% a light crop with bands, 50% fills the cell — the
preview follows each position live.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-09-24 | `e84946e` threshold through preview and export · `fc89a62` top bar slider; branch `feature/crop-threshold-slider` |
| Unit tests | 2 | 2026-09-23 | Not applicable — no test project, declined by the user (Q&A #8) |
| README | 3 | 2026-09-24 | Not done — the go covered the code only; changes listed under `### README` |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where should the slider go? | Main toolbar | 2026-09-23 |
| 2 | Which value range? | 0–50%, step 5% | 2026-09-23 |
| 3 | Is the chosen value kept between launches? | No, session only | 2026-09-23 |
| 4 | Is the subject straightforward or tricky? | Straightforward | 2026-09-23 |
| 5 | No top toolbar exists: bottom bar or new top bar? | New top bar | 2026-09-23 |
| 6 | Live preview update while dragging, or on release? | Live | 2026-09-23 |
| 7 | What is shown next to the slider? | A label with the value | 2026-09-23 |
| 8 | Create a unit-test project? | No tests | 2026-09-23 |
| 9 | Go for implementation? | No | 2026-09-23 |
| 10 | Go for implementation? (asked again by the user) | "GO implémente", in a dedicated worktree removed at the end — read as *Implement the code* | 2026-09-24 |

---

*Last updated: 2026-09-24*
