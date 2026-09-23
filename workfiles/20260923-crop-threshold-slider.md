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

| Component | Role today |
|---|---|
| `Composition/FitCalculator.cs` | Holds `DefaultCropThreshold = 0.15`; `Scale` / `Compute` already take a `threshold` parameter (clamped to [0, 0.99]) |
| `Composition/CanvasSizer.cs` | `Compute(images, threshold)` — the output width depends on the threshold |
| `Composition/Compositor.cs` | `Render(images, threshold)` and `Draw(g, images, canvas, threshold)` — both already parameterized |
| `UI/GridPreview.cs` | Calls `Compositor.Draw` with the default; caches the rendered preview until images or size change |
| `UI/MainForm.cs` | Calls `Compositor.Render` with the default for copy and save; bottom bar = status line (left) + Copy / Save (right) |

---

## Agreed Design

| Aspect | Decision |
|---|---|
| Placement | In the main bar of the window, always visible *(exact spot: see Open Questions — the app has no top toolbar today)* |
| Range | 0% – 50%, step 5% (11 positions) |
| Default | 15% (`FitCalculator.DefaultCropThreshold`, unchanged) |
| Persistence | Session only — back to 15% at every launch; no settings file |

### Behaviour

- The slider value is the total share of the overflowing axis that may be cropped (split evenly on both sides),
  exactly as the fitting rule describes it today.
- At 0%, images are fitted without any crop (bands on the non-overflowing axis). At 50%, up to half of the
  overflowing axis may be cropped.
- A single threshold value, owned by the window, drives both:
  - the **preview** — `GridPreview` gets a threshold property; setting it drops the cached render and repaints;
  - the **export** — `Compositor.Render(_preview.Images, threshold)` for copy and save.
- Preview and export must always use the same value — what you see is what you copy.
- The output canvas size (`CanvasSizer`) depends on the threshold, so the exported resolution may change with it;
  this is expected and needs no extra handling.

### Composition layer

No change expected: `FitCalculator`, `CanvasSizer` and `Compositor` already take the threshold as a parameter.
`DefaultCropThreshold` stays the initial slider value.

### README

- `## Fitting rules`: describe the threshold as adjustable (0–50%, step 5%, 15% by default, not remembered between launches)
  instead of a fixed 15%.
- `## Output` or `## Features`: mention the slider.
- `## Planned`: remove the *Crop threshold slider* entry.

---

## Test Impact

No test project exists in the repository today, and the composition layer already takes the threshold as a
parameter — the new behaviour is UI wiring. Whether to create a test project is an open question; the rows
below apply only if it is created.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `FitCalculator.Compute` with threshold 0 crops nothing (source = whole image, bands on the other axis) | `tests/ImageGridFusion.Tests/FitCalculatorTests.cs` | create *(pending Open Question)* |
| `FitCalculator.Compute` with threshold 0.5 crops at most 50% of the overflowing axis, centered | `tests/ImageGridFusion.Tests/FitCalculatorTests.cs` | create *(pending Open Question)* |

---

## Open Questions

- [ ] The app has no top toolbar: where does the slider go — in the existing bottom bar (between the status line and Copy / Save), or in a new top bar?
- [ ] Does the preview update live while the slider is dragged, or only when it is released?
- [ ] What does the slider show next to it — a label with the value (e.g. `Crop: 15%`), a tooltip only, or nothing?
- [ ] Create a unit-test project to pin the threshold bounds of `FitCalculator`, or leave tests out (UI wiring only)?

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
| 1 | Where should the slider go? | Main toolbar | 2026-09-23 |
| 2 | Which value range? | 0–50%, step 5% | 2026-09-23 |
| 3 | Is the chosen value kept between launches? | No, session only | 2026-09-23 |
| 4 | Is the subject straightforward or tricky? | Straightforward | 2026-09-23 |
| 5 | No top toolbar exists: bottom bar or new top bar? | | |
| 6 | Live preview update while dragging, or on release? | | |
| 7 | What is shown next to the slider? | | |
| 8 | Create a unit-test project? | | |

---

*Last updated: 2026-09-23*
