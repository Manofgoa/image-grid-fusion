# Empty Cell Click

> Working document — clicking the empty grid does what the **Add images** drop zone does.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The request: *"Behaviour when the thumbnail is empty: on click, same action as Add images."*

The grid never holds an empty cell — the layout always has as many cells as there are images
(`GridPreview.OnImagesChanged` resets the layout to the image count). The only empty surface is the
**empty canvas**: with no image, the preview shows a dashed rectangle and the text
*"Drop 1 to 4 images here, or paste them with Ctrl+V"* (`GridPreview.PaintEmptyState`). That is the
"empty thumbnail" of the request (Q&A #5).

Goal: a click on the empty canvas opens the **Add images** file picker, exactly like a click on the
drop zone, and the canvas shows that it is clickable.

Components: `UI/GridPreview.cs` (mouse handling, hover, empty-state painting),
`UI/MainForm.cs` (`PickFiles`, wired to `DropZoneClicked`), `README.md`.

---

## Click Behaviour

- A **single left click** on the empty canvas opens the same picker as the drop zone
  (`MainForm.PickFiles`: multi-select, same filter), and the chosen files are added the same way
  (`AddFilesAsync`, no target cell).
- Same click semantics as the drop zone: the press arms it, the release **inside the canvas** fires
  it; a release outside does nothing.
- Only while the grid is empty (`_images.Count == 0`). Once an image is there, a click on the canvas
  keeps today's behaviour (select the cell, or clear the selection outside the cells).
- Not while the grid is locked (export running) — moot with no image, kept for consistency with
  the drop zone's `_locked` check.
- Where the files go: with no image, the picked files fill the cells from image 1 — the scoping
  answers "clicked cell first" and "the click also selects the cell" (Q&A #1, #2) have nothing to
  act on in an empty grid, so the drop zone's behaviour applies as is.
- Drag and drop and `Ctrl+V` onto the empty canvas are unchanged.

---

## Clickable Hint

- The **hand cursor** over the empty canvas, as over the drop zone.
- The existing hover outline of the empty canvas stays.
- **No fluorescent green**: the empty canvas is not a helper indicator (Q&A #6); the
  *On-Cell Helper Indicators* rule does not apply and is not extended (Q&A #7).
- Text / icon (Q&A #3 asked for one): see Open Questions.

---

## Test Impact

No unit test changes: the repository has no test project, and the change is UI-only
(mouse handling and painting in `GridPreview`), with no logic a unit test could pin without a
WinForms harness. Validated by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (none: no test project, UI-only change) | — | — |

---

## Open Questions

- [ ] **Hint text and icon**: what the empty canvas shows to say it is clickable — proposal: a
  **"+"** above the text, like the drop zone's, and the text becoming
  *"Click to pick 1 to 4 images, drop them here, or paste them with Ctrl+V"*; both in the usual
  grey, white on hover, like the drop zone.
- [ ] **Green remark**: the message *"If suggestion: use fluorescent green, remember? If not explicit
  enough, add it to the rules"* was read as "every visual hint is fluorescent green" — the answers
  (Q&A #6, #7) say otherwise. What does it ask for?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design from the request, the first scoping batch (Q&A #1–#4) and a single direct scout
pass (the questions chained: the Add images action → its fill logic → the click on the grid).

- The scout found that the grid has no empty cell: the only empty surface is the empty canvas,
  which made the first batch's answers on destination and selection moot (Q&A #1, #2).
- Mid-turn remark on fluorescent green, read as "a visual hint must be fluorescent green, add it to
  the rules if not explicit enough" — `RULES.md` limits the green to helper indicators (measure /
  geometry aids) and excludes interaction feedback.
- Second batch (Q&A #5–#7): the target is the empty canvas; no green here; the existing drop zone
  keeps its style. No rule change.
- Design: a single click on the empty canvas opens the Add images picker; hand cursor. Hint text
  and icon, and the meaning of the green remark, left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project, UI-only change |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where do the images picked from an empty cell's click go? | The clicked cell first, then the next empty ones (moot: no empty cell exists, see #5) | 2026-09-26 |
| 2 | Which gesture opens the picker, and what happens to the selection? | Single click, the cell is selected too (moot: nothing to select in an empty grid) | 2026-09-26 |
| 3 | A visual hint that the empty cell is clickable? | Hand cursor + a text / icon | 2026-09-26 |
| 4 | Straightforward or tricky subject? | Straightforward — a single scout pass | 2026-09-26 |
| 5 | What is the "empty thumbnail" — the empty canvas, the greyed layout thumbnail, or both? | The empty canvas (no image) | 2026-09-26 |
| 6 | Which fluorescent-green hint shows the empty canvas is clickable? | No green here (the question was not understood) | 2026-09-26 |
| 7 | Does the widened green rule cover the existing Add images drop zone? | No — no rule widening | 2026-09-26 |
| 8 | Hint text and icon on the empty canvas? | | |
| 9 | What does the green remark ask for? | | |

---

*Last updated: 2026-09-26*
