# Force As Image Stops Videos

> Working document — while "Force as image" is checked, animated content stops playing and shows
> only its selected frame; the 5% steps become 1% steps.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the **Force as image** checkbox (`MainForm._forceImage`, shown only while the grid holds
animated content) only changes what Copy and Save produce: a PNG instead of an MP4. The preview
keeps playing, and the still export picks each source's **first frame that is not empty**
(`GridExport.RenderStill` → `FirstNonEmptyFrame`), not the frame the user browsed to.

Goals:

1. While the checkbox is checked, the preview **stops animating**: each animated cell shows only
   its **selected frame** — the page its hover slider stands on (`SourceImage.Page`).
2. Moving that slider while forced updates the cell **live**, as it already does while hovering.
3. Unchecking resumes the animation.
4. Every step of 5% in the app becomes a step of **1%**.

Components involved: `UI/MainForm.cs`, `UI/GridPreview.cs`, `UI/AnimationPlayer.cs`,
`Imaging/GridExport.cs` (depending on Q1), `Imaging/VideoFrames.cs` (depending on Q3), `README.md`.

---

## Still Preview While Forced

- `MainForm` forwards the checkbox state to the preview (e.g. a `GridPreview.ForceStill` property,
  set on `CheckedChanged`), which forwards it to `AnimationPlayer`.
- `AnimationPlayer` treats "forced still" as **every playback held**, reusing the existing
  hold mechanism (`Pause` / `Resume`): the playback loop keeps running but renders nothing while paused.
- On entering the forced state, each animated cell is re-rendered at its current page
  (`SourceImage.Page`), so the cell shows exactly the selected frame rather than the last
  animation frame, which sits somewhere between two pages.
- The hover slider keeps working while forced: dragging it renders the page under the cursor live
  (existing `GridPreview` behaviour, see *Hover a cell … slider along its bottom* in the README).
- The checkbox disappears when the grid no longer holds animated content (`UpdateButtons`); its
  checked state is kept as is, so content added later is shown still if the box is still checked.
  *(Agent proposal — see Q2 for which content is concerned.)*

### Resume on Uncheck

- Unchecking releases every playback through `Resume`: each resumes where it stood, or from the
  page its slider was moved to meanwhile — the same rule as leaving a hovered cell (README, *Live
  preview*). A cell still hovered stays held. *(Agent proposal — see Q5.)*

---

## Steps of 1%

Two controls currently move by 5%:

| Control | Where | Today | After |
|---|---|---|---|
| Crop threshold slider (top bar) | `MainForm.ThresholdStepPercent = 5`, `MaxThresholdPercent = 50` | 11 positions, 0–50% by 5% | 51 positions, 0–50% by 1% (`ThresholdStepPercent = 1`) |
| Video frame slider (hover) | `VideoFrames.Step`: `duration / 100`, never under `MinStep = 1 s` | 1% for videos ≥ 100 s; coarser below (a 20 s video: 20 positions, **5%** apart) | See Q3 |

- The crop slider keeps its default of 15% (`FitCalculator.DefaultCropThreshold`), still
  expressed as a step index (`Value = 15`).
- At 1% steps, the 160 px crop slider gives ~3 px per step; the keyboard and wheel still move it
  one step at a time (`SmallChange = LargeChange = 1`). See Q4 for its width.

---

## Documentation

`README.md` lines to update:

- *Force as image* (§ Animated content): the preview stops playing while it is checked, and —
  depending on Q1 — the export uses the selected frame.
- *Fitting rules*: "in steps of 5%" → "in steps of 1%".
- *Supported files* table, Video row: "duration / 100, never under 1 s" — depending on Q3.

---

## Test Impact

The solution holds no unit test project (`ImageGridFusion.slnx` references only
`src/ImageGridFusion`). Nothing is created or updated: the behaviour is checked by running the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project in the solution) | — | — |

---

## Open Questions

- [ ] Q1 — With "Force as image" checked, should Copy / Save export the **selected frame** of each
  content (what the preview now shows), instead of its first frame that is not empty?
- [ ] Q2 — Which animated content stops while forced: videos only, or every animated content
  (GIFs, scrolling texts too — the checkbox is global)?
- [ ] Q3 — Video frame slider: drop the 1 s minimum so every video gets 100 positions 1% apart?
- [ ] Q4 — Crop slider at 51 positions: keep its 160 px width, or widen it?
- [ ] Q5 — On uncheck, resume like a released hover (where it stood, or from the page the slider
  moved it to), or restart every content in sync from its start?
- [ ] Q6 — While forced, the preview sound stops too (nothing plays): confirm?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial design from the request and the scoping answers: forced still reuses the hold mechanism
of `AnimationPlayer` for every playback; the slider stays live; unchecking resumes; the crop
threshold step goes from 5% to 1%. Codebase findings: the still export ignores the selected frame
(Q1), and the video slider is coarser than 1% for videos under 100 s (Q3).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project in the solution |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When "Force as image" is unchecked again, does the video resume animating? | Yes, it resumes | 2026-09-24 |
| 2 | Which control(s) does the 5% → 1% step concern? | Every step at 5% in the app | 2026-09-24 |
| 3 | While forced, does the preview follow the selected frame live when it moves? | Yes, live | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward | 2026-09-24 |
| 5 | Q1 — Export the selected frame instead of the first non-empty frame? | | |
| 6 | Q2 — Videos only, or every animated content? | | |
| 7 | Q3 — Drop the video slider's 1 s minimum step? | | |
| 8 | Q4 — Crop slider width at 51 positions? | | |
| 9 | Q5 — Resume point on uncheck? | | |
| 10 | Q6 — Preview sound stops while forced? | | |

---

*Last updated: 2026-09-24*
