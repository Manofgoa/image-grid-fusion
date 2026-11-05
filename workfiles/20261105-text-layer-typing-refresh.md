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

### Pending Text and Style, Rendered at a Limited Rate

- **One route for every live change of a text layer**: the text field (`ChangeLayerText`) and the style's live
  changes (`ChangeLayerStyle`) — the outline width, shadow size and opacity sliders, the color dialog and the
  eyedropper following the cursor (`LayerColorRoute`). They no longer render: they **record the text and style
  wanted** for the selected text layer and ask for a render. The field and the sliders are never blocked.
- One-shot changes (a checkbox, the font, an alignment, a style applied from the strip, Box) take the route too,
  their final render following at once rather than after the pause.
- A render runs **at most every ~50 ms** while changes keep coming (throttle), the **last text and style wanted**
  always winning: a change made while a render runs is drawn by the next one, the ones in between are never drawn.
- **Off the UI thread**: `TextRaster.Render` runs on a worker, one at a time (its factories are per thread
  already, `[ThreadStatic]`); the result is put in place on the UI thread only if the layer is still the one it
  was asked for and its text, style and box unchanged meanwhile — dropped otherwise, as a cutout request whose
  look changed (RULES.md § The Cutout Exception).
- The result is matched to its layer by **content** (`GridPreview.ReplaceTextContent`): the layer showing the content
  the change was asked from takes the new bitmap, line, text, style and box, keeping its look, place and visibility
  as they stand then; a result older than what shows is dropped (a request number).
- **What the layer is wanted to draw** — its text, style and box as last changed — is what every reader takes while
  a render is pending (`MainForm.WantedOf`): the options shown, the next change composed on it, Duplicate, Save style.
- **Export, copy, edit view, another layer's change, the emoji fonts redrawn** while a render is pending: the
  pending change is drawn in full at once (synchronously) and put in place first (`MainForm.FlushTextRender`), or
  dropped when its layer is gone — so the undo history and the exports never miss the last keys. A text box's
  side dragged draws the layer itself with the wanted text and style, the pending change dropped.

### Cheaper Steps While Typing

- **Draft render** while keys come: the text drawn at the font size whose line is as tall as the preview shows it —
  at least 32 px — instead of 192 px; the layer's place is unchanged (its size measures a line,
  `LinePixels` follows the bitmap): slightly less sharp until the pause, as agreed.
- **Fast redraw**: the preview's area redrawn with `fast: true` while typing, as during a gesture.
- **`UpdateButtons` once**, not twice: a text change keeps the selection — the layer's content replaced in
  place does not raise `SelectedImageChanged` when only its text changed.
- **Only the options a text touches** while typing: the layer list's row name; the thumbnail and the rest of
  `UpdateLayerOptions` / `UpdateButtons` (captions, tooltips, canvas sizing) wait for the final render.

### Final Render

- **~300 ms after the last change** — the undo history's settle time — the text is rendered once more at full
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

- [x] ~~While typing, may the text show **slightly less sharp** (draft at the preview's resolution) until the pause,
  then sharp — or must every step stay at full resolution, only throttled?~~ → Draft while typing, sharp at the pause
- [x] ~~Do the **other live changes of a text layer** — its sliders (outline width, shadow size, opacities), the
  color dialog and the eyedropper following the cursor — take the same route (throttled, off the UI thread, draft
  then final), or the text field only?~~ → All of them, one route
- [x] ~~The **final render's delay** after the last key: ~300 ms (the undo history's settle), or shorter?~~ → ~300 ms

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

### Iteration 2 — 2026-11-05

The three open questions answered: the draft while typing is accepted; every live change of a text layer — its
sliders, the color dialog, the eyedropper, and the one-shot settings — takes the same route as the text field; the
final render comes ~300 ms after the last change.

### Iteration 3 — 2026-11-05 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/text-layer-typing-refresh`, branch
`feature/text-layer-typing-refresh`).

### Iteration 4 — 2026-11-05 — 🧭 Implementation choices

- **The worker** is a dedicated background thread (`UI/TextLayerRenderer`), not the thread pool: `TextRaster`'s
  factories are per thread, so they are made once. It catches every exception and hands it back, so a failure ends
  the gesture and is said on the status line rather than killing the worker.
- **The draft's font size** is computed from the line per pixel of font size of the content shown
  (`TextWanted.LinePerEm`) — a content drawn in full gives `LinePixels / EmSize`, a draft its own —, since the
  current content may itself be a draft.
- **The result is matched by content**, the layer keeping its place and look as they stand when it arrives
  (`GridPreview.ReplaceTextContent`), rather than by index or by the layer record: a layer moved meanwhile keeps the
  text.
- **One wanted change at a time**: a change of another layer puts the pending one in place first.
- **The flush points** are `EnsureUpscaled` (every export and copy route calls it) and `BeginExport`, a new
  `GridPreview.EditOpening` event raised before the layer and background edit views open (applying layers would close
  them), and `EmojiFontsChanged`.
- **The text box's side drag** (a gesture of its own, out of the frozen scope) still draws in full synchronously, now
  with the wanted text and style, the pending change dropped — so it never loses keys.
- **The color dialog's OK and the eyedropper's pick** compose on the wanted style; a pick equal to the last followed
  color asks nothing, the final render already on its way.
- **Docs**: a bullet in RULES.md § Layers (the route, `WantedOf`, the flush points). README, GLOSSARY and their French
  versions unchanged: nothing the user reads about the layers changes but the draft's sharpness while typing.
- **Not verified in the app by the agent**: the checking instance could not be brought to the front over the user's
  desktop, and driving keys blind was dropped. The build passes (0 warnings); the typing is to be checked by hand.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-11-05 | Worker, preview, form routing; build 0 warnings |
| Unit tests | 3 | 2026-11-05 | Not applicable: no test project in the repository |
| README | 4 | 2026-11-05 | Unchanged (nothing user-facing to document); RULES.md § Layers updated |
| Hand check in the app | | | Left to the user (see Iteration 4) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | While typing, what should the preview do? | Throttled follow + full render at the pause, each step cheaper | 2026-11-05 |
| 2 | What is slow, concretely? | The field itself, the preview late, the whole app frozen | 2026-11-05 |
| 3 | When is it slowest? | From a text as short as `edgd dgsdg sdgds` | 2026-11-05 |
| 4 | Exploration depth? | Straightforward | 2026-11-05 |
| 5 | May the text show slightly less sharp while typing (draft)? | Yes, draft | 2026-11-05 |
| 6 | Same route for the text layer's other live changes? | Yes, all of them | 2026-11-05 |
| 7 | Final render's delay after the last key? | ~300 ms | 2026-11-05 |

---

*Last updated: 2026-11-05*
