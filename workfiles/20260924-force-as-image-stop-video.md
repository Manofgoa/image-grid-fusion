# Force As Image Stops Videos

> Working document — while "Force as image" is checked, animated content stops playing and shows
> only its selected frame, which is also what Copy and Save export; the 5% steps become 1% steps.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the **Force as image** checkbox (`MainForm._forceImage`, shown only while the grid holds
animated content) only changes what Copy and Save produce: a PNG instead of an MP4. The preview
keeps playing, and the still export picks each source's **first frame that is not empty**
(`GridExport.RenderStill` → `FirstNonEmptyFrame`), not the frame the user browsed to.

Goals:

1. While the checkbox is checked, the preview **stops animating** — every animated content:
   videos, GIFs, scrolling texts. Each cell shows only its **selected frame**: the page its hover
   slider stands on (`SourceImage.Page`).
2. Moving that slider while forced updates the cell **live**, as it already does while hovering.
3. Copy and Save, forced to an image, export **the selected frames** — exactly what the preview shows.
4. Unchecking resumes the animation, like a released hover.
5. Every step of 5% in the app becomes a step of **1%**: the crop slider and the video frame slider.

Components involved: `UI/MainForm.cs`, `UI/GridPreview.cs`, `UI/AnimationPlayer.cs`,
`Imaging/GridExport.cs`, `Imaging/VideoFrames.cs`, `README.md`.

---

## Still Preview While Forced

- `MainForm` forwards the checkbox state to the preview (e.g. a `GridPreview.ForceStill` property,
  set on `CheckedChanged`), which forwards it to `AnimationPlayer`.
- `AnimationPlayer` treats "forced still" as **every playback held**, reusing the existing
  hold mechanism (`Pause` / `Resume`): the playback loop keeps running but renders nothing while
  paused. Applies to **every animated content** (videos, GIFs, scrolling texts), the checkbox being global.
- On entering the forced state, each animated cell is re-rendered at its current page
  (`SourceImage.Page`), so the cell shows exactly the selected frame rather than the last
  animation frame, which sits somewhere between two pages.
- The hover slider keeps working while forced: dragging it renders the page under the cursor live
  (existing `GridPreview` behaviour).
- **Sound**: the preview sound is silent while forced — the paused playback already reports
  `playing: false` to `PreviewSound` — and resumes with the animation.
- The checkbox disappears when the grid no longer holds animated content (`UpdateButtons`); its
  checked state is kept as is, so content added later is shown still if the box is still checked.

### Resume on Uncheck

- Unchecking releases every playback through `Resume`: each resumes where it stood, or from the
  page its slider was moved to meanwhile — the same rule as leaving a hovered cell (README, *Live
  preview*). A cell still hovered stays held until the cursor leaves it.

---

## Still Export of the Selected Frames

- `GridExport.Job.Capture` records, for each animated source, the page it shows (`SourceImage.Page`).
- `GridExport.RenderStill` renders each animated source at that page (`PageSource.Render(page)`,
  the same call the hover slider uses), instead of `FirstNonEmptyFrame`.
- `FirstNonEmptyFrame` is removed if nothing else uses it; `EmptyFrame` / `ProbeTimes` stay if
  another caller still needs them.
- The video export (checkbox unchecked) is unchanged: every source plays from its start.

---

## Steps of 1%

| Control | Where | Today | After |
|---|---|---|---|
| Crop threshold slider (top bar) | `MainForm.ThresholdStepPercent = 5`, `MaxThresholdPercent = 50` | 11 positions, 0–50% by 5% | 51 positions, 0–50% by 1% (`ThresholdStepPercent = 1`) |
| Video frame slider (hover) | `VideoFrames.Step`: `duration / 100`, never under `MinStep = 1 s` | 1% for videos ≥ 100 s; coarser below (a 20 s video: 20 positions, 5% apart) | Always `duration / 100`: 100 positions 1% apart, `MinStep` removed |

- The crop slider keeps its default of 15% (`FitCalculator.DefaultCropThreshold`, step index 15)
  and its **160 px width** (~3 px per step); keyboard and wheel move it 1% at a time.
- A video with a single position today (under 2 s) now gets 100 positions like any other;
  `VideoFrames.Positions` and the lone-position case (`Count == 1` → 10% mark) follow from the new step.

---

## Documentation

`README.md` lines to update:

- *Force as image* (§ Animated content): the preview stops playing (sound included) while it is
  checked, and Copy / Save export each content's **selected frame** instead of its first frame
  that is not empty.
- *Fitting rules*: "in steps of 5%" → "in steps of 1%".
- *Supported files* table, Video row: "duration / 100, never under 1 s" → "duration / 100".

---

## Test Impact

The solution holds no unit test project (`ImageGridFusion.slnx` references only
`src/ImageGridFusion`). Nothing is created or updated: the behaviour is checked by running the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project in the solution) | — | — |

---

## Open Questions

- [x] ~~Q1 — With "Force as image" checked, should Copy / Save export the selected frame of each
  content, instead of its first frame that is not empty?~~ → Yes, the selected frame.
- [x] ~~Q2 — Which animated content stops while forced?~~ → Every animated content (videos, GIFs, scrolling texts).
- [x] ~~Q3 — Video frame slider: drop the 1 s minimum so every video gets 100 positions 1% apart?~~ → Yes.
- [x] ~~Q4 — Crop slider at 51 positions: keep its 160 px width, or widen it?~~ → Keep 160 px.
- [x] ~~Q5 — On uncheck, resume like a released hover, or restart every content from its start?~~ → Like a released hover.
- [x] ~~Q6 — While forced, the preview sound stops too?~~ → Yes, silent while forced.

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

### Iteration 2 — 2026-09-24

Open questions answered: the still export uses the selected frames (new section *Still Export of
the Selected Frames*); every animated content stops, not only videos; the video slider loses its
1 s minimum step; the crop slider keeps its width; unchecking resumes like a released hover; the
preview sound is silent while forced.

### Iteration 3 — 2026-09-24 — ✅ Implemented

Go given ("Go implémente", read as *Implement the code*: no documentation authorized). Run on
`main` (standing choice, see the Branch Gate rule of this repo), with small commits because other
sessions change the repository in parallel.

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
| 5 | Q1 — Export the selected frame instead of the first non-empty frame? | Yes, the selected frame | 2026-09-24 |
| 6 | Q2 — Videos only, or every animated content? | Every animated content | 2026-09-24 |
| 7 | Q3 — Drop the video slider's 1 s minimum step? | Yes, always 1% | 2026-09-24 |
| 8 | Q4 — Crop slider width at 51 positions? | Keep 160 px | 2026-09-24 |
| 9 | Q5 — Resume point on uncheck? | Like a released hover | 2026-09-24 |
| 10 | Q6 — Preview sound stops while forced? | Yes, silent | 2026-09-24 |

---

*Last updated: 2026-09-24*
