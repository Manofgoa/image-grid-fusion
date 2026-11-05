# Text Layer Typing Refresh

> Working document — typing in a text layer's field no longer redraws everything at each key.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Typing in the Layers options' text field redraws the selected text layer **at each key**, synchronously on
the UI thread: the field itself lags, the preview follows late and the whole app freezes for a moment — from
a text as short as `edgd dgsdg sdgds` (user report, 2026-11-05).

Goal (agreed): the preview **follows the typing at a limited rate**, **cheaper** at each step, then a **full
render** once the typing stops. The field never waits for a render.

Components: `UI/MainForm.Layers.cs` (`ChangeLayerText`, `ReplaceTextLayer`, `UpdateLayerOptions`,
`UpdateLayerList`), `Imaging/TextRaster.cs` (`Render`, `Draw`, `Shadow`), `UI/GridPreview.Layers.cs`
(`SetLayers`, `ReplaceSelectedLayer`, `RedrawLayersChange`, `RedrawArea`), `UI/MainForm.cs` (`UpdateButtons`).

---

## What a Key Costs Today

Read in the code, 2026-11-05. Each key (`_layerText.TextChanged` → `ChangeLayerText` → `ReplaceTextLayer`),
all on the UI thread:

| # | Step | Cost |
|---|---|---|
| 1 | `TextRaster.Render` at `EmSize` **192 px** a line | A bitmap of the whole text — ~1 600 × 260 px for 16 characters — through 1 to 4 Direct2D software passes (WIC render target), the shadow a **CPU box blur** over the whole bitmap (`Shadow`, `BoxBlur`, several passes), the layers merged by hand (`Over`), then copied into a `Bitmap` (`ToBitmap`). Grows with the text's length |
| 2 | `GridPreview.SetLayers` → `RedrawLayersChange` → `RedrawArea` | The union of the old and new boxes drawn again into the cache **at full quality** (typing is no gesture): grid background, every cell under it (`Compositor.DrawCell`), borders, **every shown layer**, the 1 600-px text bitmap reduced with high-quality bicubic. Grows with the box, so with the text |
| 3 | `SetLayers` → `SyncPlayer`, `ReleaseImage` of the previous content, `ContentVersion++` | Small |
| 4 | `LayersChanged` → `UpdateButtons` | Copy / Save captions, length readout, PNG size tooltips (canvas sizing), `UpdateEffects`, `UpdateGlobalEffects` → `UpdateLayerOptions`: every option control set again, `UpdateLayerList` making a **new thumbnail** from the 1 600-px bitmap (high-quality bicubic) |
| 5 | `SelectedImageChanged` — the selected layer's content is a new `SourceImage` | **`UpdateButtons` a second time**: step 4 again |

The undo history commits once the state settles (~300 ms): not a per-key cost.

---

## Design

### Pending Text, Rendered at a Limited Rate

- `ChangeLayerText` no longer renders: it **records the text wanted** for the selected text layer and asks for a
  render. The field is never blocked.
- A render runs **at most every ~50 ms** while keys keep coming (throttle), the **last text wanted** always
  winning: a text typed while a render runs is drawn by the next one, the ones in between are never drawn.
- **Off the UI thread**: `TextRaster.Render` runs on a worker, one at a time (its factories are per thread
  already, `[ThreadStatic]`); the result is put in place on the UI thread only if the layer is still the one it
  was asked for and its text, style and box unchanged meanwhile — dropped otherwise, as a cutout request whose
  look changed (RULES.md § The Cutout Exception).
- **Selection change, undo, export, edit view, layer removed** while a render is pending: the pending text is
  applied at once (synchronously) before the change, or dropped when its layer is gone — so the undo history
  and the exports never miss the last keys.

### Cheaper Steps While Typing

- **Draft render** while keys come: the text drawn at the em the preview actually shows — its line height in
  device pixels, at least 32 px — instead of 192 px; the layer's place is unchanged (its size measures a line,
  `LinePixels` follows the bitmap). *(Open Question 1.)*
- **Fast redraw**: the preview's area redrawn with `fast: true` while typing, as during a gesture.
- **`UpdateButtons` once**, not twice: a text change keeps the selection — the layer's content replaced in
  place does not raise `SelectedImageChanged` when only its text changed.
- **Only the options a text touches** while typing: the layer list's row name; the thumbnail and the rest of
  `UpdateLayerOptions` / `UpdateButtons` (captions, tooltips, canvas sizing) wait for the final render.

### Final Render

- **~300 ms after the last key** — the undo history's settle time — the text is rendered once more at full
  resolution (`EmSize`), redrawn at full quality, the thumbnail made, `UpdateButtons` run once.
- The undo history keeps its rule: one step once the state settles. Typing is held as a gesture (`InGesture`)
  until the final render is in place, so the step holds the full-resolution content, never a draft.

---

## Test Impact

The repository has **no unit test project** (only `src/ImageGridFusion/ImageGridFusion.csproj`): nothing is
created here, the behaviour is checked by hand in the app — a long text, a shadow and an outline on, typed fast,
then Ctrl+Z.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] While typing, may the text show **slightly less sharp** (draft at the preview's resolution) until the pause,
  then sharp — or must every step stay at full resolution, only throttled?
- [ ] Do the **other live changes of a text layer** — its sliders (outline width, shadow size, opacities), the
  color dialog and the eyedropper following the cursor — take the same route (throttled, off the UI thread, draft
  then final), or the text field only?
- [ ] The **final render's delay** after the last key: ~300 ms (the undo history's settle), or shorter?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design from the scoping answers (throttled follow + cheaper steps + final render; the field, the preview and
the whole app all lag; slow from a 16-character text; exploration straightforward) and the code read: the text
rendered off the UI thread at a limited rate, the last text winning; a draft at the preview's resolution, a fast
redraw and the options touched only while typing; a full render ~300 ms after the last key, typing held as a gesture
so the undo step holds it. No row of `TODO-FEATURES.md` matches.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | While typing, what should the preview do? | Throttled follow + full render at the pause, each step cheaper | 2026-11-05 |
| 2 | What is slow, concretely? | The field itself, the preview late, the whole app frozen | 2026-11-05 |
| 3 | When is it slowest? | From a text as short as `edgd dgsdg sdgds` | 2026-11-05 |
| 4 | Exploration depth? | Straightforward | 2026-11-05 |
| 5 | May the text show slightly less sharp while typing (draft)? | | |
| 6 | Same route for the text layer's other live changes? | | |
| 7 | Final render's delay after the last key? | | |

---

*Last updated: 2026-11-05*
