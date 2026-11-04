# Custom Output Format

> Working document — a **Custom** output format beside the fixed ones: a ratio, or exact pixel
> dimensions, typed in the Format tab's options.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The Format tab of the Global toolbar offers fixed formats — Free, Twitter (1200:628, the default),
Square 1:1, Portrait 4:5, Story 9:16, Landscape 16:9 (`Composition/OutputFormat.cs`). The canvas
ratio has one definition, `GridPreview.CanvasRatio`, and the exports' size one rule,
`CanvasSizer.Compute`: the **native size**, the longer side clamped to [1200, cap], the cap per export
kind (RULES.md § Output Format).

This workfile adds a **Custom** format (backlog row *Custom output format*,
`workfiles/TODO-FEATURES.md`, user request 2026-11-04):

- a **ratio** of the user's own — the native size rule unchanged;
- or exact **pixel dimensions** (`1080 × 1350`) — every export then gives **exactly** that size,
  enlarged or reduced from the native size, unless a cap the user chose (a *PNG max size* entry, the
  *Video size*) is smaller, the ratio kept.

Components: `Composition/OutputFormat.cs` (`OutputFormat`, `OutputFormats`), `Composition/CanvasSizer.cs`,
`UI/FormatStrip.cs`, `UI/GridPreview.cs` (`Format`, `UpdateRatio`, `CanvasRatio`, `ExportOffsets`),
`UI/GridHistory.cs` (`GridState`), `UI/MainForm.cs` (the Format options, `SetFormat`,
`ResetGlobalEffects`, `FormatInitial`, `PngSize`, the Copy / Save ▾ entries),
`Imaging/GridExport.cs` (`Job.Capture`, the video canvas).

---

## The Custom Format

- A new value of `OutputFormat`, **`Custom`**, **last** in the enum, so its thumbnail comes after
  Landscape.
- Its state is a small immutable record, **`CustomFormat`** (`Composition/OutputFormat.cs`):
  `Width`, `Height` (positive integers) and `Exact` (bool).
  - `Exact` off: `Width : Height` is a **ratio** — `3 × 2` is 3:2, `1910 × 1000` is 1.91:1.
  - `Exact` on: `Width × Height` are the canvas's **pixels**, the ratio `Width / Height` with them.
- `OutputFormats.Ratio(format)` stays the fixed formats' constant; the Custom ratio is
  `CustomFormat.Ratio`, read by `GridPreview.UpdateRatio` next to the Free ratio:
  `OutputFormats.Ratio(_format) ?? (_format == Custom ? _custom.Ratio : FreeRatio)`. Every consumer
  keeps reading `CanvasRatio` (RULES.md § Output Format), so the preview, the text pages, the layout
  strip and the exports follow the Custom ratio by themselves.
- **Not persisted** (Q&A #1): like the rest of the output format, back to its default at each launch.
- **Part of the composed state**: `GridState` carries the `CustomFormat` beside the `OutputFormat`
  (equality, hash, the restore), so a change of its fields is one undo step like a format change
  (RULES.md § Undo History: a new piece of composed state joins the snapshot and the restore).
- The global Resets and *Clear all* bring the format back to Twitter **and** the Custom fields to their
  default (see *Open Questions* for that default); the Format tab's Reset button counts as modified
  while the format is not Twitter (`FormatInitial` unchanged in meaning).

### Free and Custom

- **Free stays as it is**: computed by `OutputFormats.FreeRatio`, its own thumbnail, never exact
  pixels. Custom is a separate thumbnail; one format is in force at a time.
- The Twitter corners stay in the Twitter format only (RULES.md § Output Format): disabled under
  Custom, their setting kept.

---

## The Format Tab's Options

The row holds the `FormatStrip`, then the Custom fields (Q&A #2):

```
[Free][Twitter][1:1][4:5][9:16][16:9][Custom]   Width [1080] × Height [1350]   [x] Exact pixels
```

- **The Custom thumbnail** draws the current layout at the Custom ratio, as the others do
  (`FormatStrip.PaintThumbnail`); its label — see *Open Questions*.
- **Two numeric fields**, *Width* and *Height*, and an **Exact pixels** checkbox, after the strip,
  always shown, enabled whatever format is selected.
- **Acting on any of them selects Custom** (the Format's equivalent of RULES.md § Options Toolbar,
  *acting on any option activates the effect*), then applies the change.
- A change applies **live**, on each value change; the undo history commits the step once it settles
  (RULES.md § Undo History), so typing `1350` is one step.
- The fields' bounds depend on *Exact pixels* — see *Open Questions*.
- Locked while exporting, like the strip (`SetFormat` returns while `IsExporting`).

---

## Sizing

### Ratio only (Exact pixels off)

The native size rule, unchanged: `CanvasSizer.Compute` / `ForLayers` at the Custom ratio, the longer
side within [1200, cap].

### Exact pixels (Q&A #3 — exact, unless a cap is smaller)

- **One rule**, in `CanvasSizer`: given an exact size, the canvas is that size, reduced to the cap when
  its longer side exceeds it — `scale = min(1, cap / longer side)`, the ratio kept, rounded — **no
  1200 floor**, no native computation. `Compute` and `ForLayers` take the exact size as an optional
  argument (`Size? exact`), so every caller goes through the one rule.
- Per export, the cap is the one it has today:

  | Export | Cap | `1080 × 1350` gives | `4000 × 5000` gives |
  |---|---|---|---|
  | PNG — Copy / Save | 8192 | 1080 × 1350 | 4000 × 5000 |
  | PNG — a *PNG max size* entry (e.g. 1080p, 1920) | the entry's | 1080 × 1350 | 1536 × 1920 |
  | JPEG for sharing | 4096, then reduced to 2560 | 1080 × 1350 | 2048 × 2560 |
  | MP4 / GIF | the *Video size* (1920 by default) | 1080 × 1350 | 1536 × 1920 |

- The *PNG max size* entries keep their text `720p — W × H` and their tooltip; their size is read
  from the same rule (`MainForm.PngSize`), so they show what they give. The Copy / Save tooltips
  (`PNG W × H`) follow the same way.
- The **video canvas** stays rounded to even sides (`Animation.EvenSize`) — see *Open Questions* for
  an odd exact size.
- The **position readout** (`GridPreview.ExportOffsets`) reads the PNG export's canvas: it gives
  offsets in the exact size's pixels.
- **Cell copy** is untouched: it has its own native size (RULES.md § Cell Copy).
- `GridExport.Job.Capture` captures the exact size beside the ratio when an export starts, so a
  change during an export cannot reach it (the Format tab is locked meanwhile anyway).

---

## Documentation

- `RULES.md` § Output Format: the Custom format, its exact pixels and the caps.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: a *Custom format* (*format personnalisé*) row; *Output format* and
  *Native size* rows mention it.
- `README.md` / `README.fr.md`: the formats' list, if it names them.
- `SHORTCUTS.md`: unchanged — no key.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*): no
unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| Custom ratio `3 × 2`, Exact off: the preview at 3:2, the PNG at its native size (longer side ≥ 1200) | The running app | — |
| Exact `1080 × 1350`: PNG Copy / Save exactly 1080 × 1350, under the 1200 floor | The running app | — |
| Exact `4000 × 5000`: the 1080p entry gives 1536 × 1920, the MP4 at the Video size | The running app | — |
| Editing a field selects Custom; Ctrl+Z gives the previous values back | The running app | — |
| The global Reset brings back Twitter and the Custom default | The running app | — |

---

## Open Questions

- [x] ~~Kept between sessions as a list of the user's formats, or only the last one?~~ → One Custom
  format, not persisted (Q&A #1)
- [x] ~~How is it entered in the Format tab's options?~~ → Width × Height fields and an *Exact
  pixels* checkbox (Q&A #2)
- [x] ~~What becomes of the PNG max size entries and the Video size with exact dimensions?~~ → Exact
  size, reduced by a smaller cap, the ratio kept (Q&A #3)
- [x] ~~How does Free coexist with it?~~ → Free unchanged, a thumbnail of its own, never exact
  (follows from Q&A #1–#2)
- [ ] The fields' bounds: the ratio's range with *Exact pixels* off, the pixels' with it on?
- [ ] The Custom fields' default — at launch, after a Reset or *Clear all*?
- [ ] The Custom thumbnail's label: `Custom`, or its value (`3:2`, `1080 × 1350`)?
- [ ] An odd exact size (`1081 × 1350`) in a video, which needs even sides?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-04

Scoping batch answered (Q&A #1–#4): one Custom format, not persisted; two numeric fields and an
*Exact pixels* checkbox; exact dimensions in every export, unless a PNG max size entry or the Video
size is smaller; a straightforward subject, one scout pass. The scout pass located the format model
(`OutputFormat`, `UpdateRatio`), the Format strip, the undo state (`GridState.Format`) and the sizing
(`CanvasSizer.Compute` with its 1200 floor, the caps passed by every export). Initial design written:
a `CustomFormat` record in the composed state, an optional exact size in `CanvasSizer`, the caps
reducing it. Four questions left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — checks by hand (see Test Impact) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How is the custom format kept and shown among the Format thumbnails: a list of the user's formats, a single one with its last value kept, or a single one not kept? | A single Custom format, **not kept** between sessions | 2026-11-04 |
| 2 | How are the ratio or the dimensions entered: one text field parsing `3:2` / `1080x1350`, two fields and a mode, or a small dialog? | **Two fields and a mode**: Width × Height, an *Exact pixels* checkbox (off = a ratio) | 2026-11-04 |
| 3 | With exact dimensions, what becomes of the PNG max size entries and the Video size: exact everywhere, exact unless a cap is smaller, or exact PNG and capped video? | **Exact, unless a cap is smaller**: a PNG max size entry and the Video size still reduce it, the ratio kept | 2026-11-04 |
| 4 | Is the subject straightforward or tricky / long to explore? | **Straightforward**: one scout pass | 2026-11-04 |
| 5 | The fields' bounds, ratio and pixels? | | |
| 6 | The Custom fields' default at launch and after a Reset? | | |
| 7 | The Custom thumbnail's label? | | |
| 8 | An odd exact size in a video? | | |

---

*Last updated: 2026-11-04*
