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
- The global Resets and *Clear all* bring the format back to Twitter **and** the Custom fields to
  unset (§ The Format Tab's Options); the Format tab's Reset button, the global Reset and *Clear all*
  count as modified while the format is not Twitter **or the Custom fields are set** (`FormatInitial`).
  `CustomFormat` is then `null` in the composed state.

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
  (`FormatStrip.PaintThumbnail`). Its **label is its value** (Q&A #7): `3:2` with *Exact pixels* off —
  the two fields divided by their greatest common divisor, `1910 × 1000` reading `191:100` — and
  `1080×1350` with it on; its tooltip says *Custom*, then what it gives.
- **Its default is taken from the format before it** (Q&A #6): while the fields are **unset** — at
  launch, after the global Resets or *Clear all* — the first pick of Custom (its thumbnail or a field)
  fills them with the PNG native size of the format then in force (`MainForm.PngSize`, the Copy cap),
  *Exact pixels* off, so the ratio does not change until a field does. The Resets bring the fields back
  to unset; the Format tab's Reset button counts as modified while the format is not Twitter.
  Unset, the fields show that native size greyed, the next pick's values.
- **Two numeric fields**, *Width* and *Height*, and an **Exact pixels** checkbox, after the strip,
  always shown, enabled whatever format is selected.
- **Acting on any of them selects Custom** (the Format's equivalent of RULES.md § Options Toolbar,
  *acting on any option activates the effect*), then applies the change.
- A change applies **live**: a number typed applies digit by digit **while it fits** the bounds as it
  stands, never rewritten under the caret; one that does not fit waits for its commit — Enter, the focus
  leaving, the arrows or the wheel — then is brought back within the bounds, the field rewritten. The
  undo history commits the step once it settles (RULES.md § Undo History), so typing `1350` is one step.
- A focused field keeps the keys a text field takes — Ctrl+Z / Y, Ctrl+C / V, Delete, Escape
  (`MainForm.IsEditingCustomField` in the `ProcessCmdKey` whitelist); Space already did.
- **Bounds** (Q&A #9), `CustomFormat.MinSide` / `MaxSide` / `MaxElongation`:
  - each field from **1 to 8192** with *Exact pixels* off, **100 to 8192** with it on — turning it on
    brings a smaller value up to 100;
  - in both modes, the shape stays between **1:10 and 10:1**: the field just changed is brought back
    to the limit against the other one (`Width 3000`, `Height 200` → `Height 300`).
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
  (`PNG W × H`) follow the same way. The MP4 and GIF entries, which had no tooltip, get one giving the
  video's size (`MainForm.RefreshVideoSizeTooltips`), for every format.
- The preview's ratio skips its 0.2 % tolerance for Custom (`GridPreview.UpdateRatio`), kept for the
  other formats: the exact size and the preview's shape agree to the pixel.
- The **video canvas** stays rounded down to even sides (`Animation.EvenSize`, Q&A #8): an odd exact
  size — `1081 × 1350` — gives an exact PNG and a `1080 × 1350` MP4 / GIF; the MP4 / GIF entries'
  tooltips give the size they really produce.
- The **position readout** (`GridPreview.ExportOffsets`) reads the PNG export's canvas: it gives
  offsets in the exact size's pixels.
- **Cell copy** is untouched: it has its own native size (RULES.md § Cell Copy).

### The Preview at Real Size

With *Exact pixels*, the preview's canvas is drawn at its **real final size** — one pixel of the PNG
export (`CanvasSizer.Exactly` at the Copy cap, the exact size itself) one device pixel of the screen —
whenever it **fits the working area** (`GridPreview.CanvasBounds`: the control less its margins and the
drop zone), centered there; larger, it is fitted as for every format. The ratio alone (*Exact pixels*
off), the other formats and a layer's edit view keep the fitted canvas. Turning *Exact pixels* on or
off, or a size change with the same ratio, lays the preview out again (`GridPreview.UpdateRatio`).
- `GridExport.Job.Capture` captures the exact size beside the ratio when an export starts, so a
  change during an export cannot reach it (the Format tab is locked meanwhile anyway).

---

## Documentation

- `RULES.md` § Output Format: the Custom format, its exact pixels and the caps.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: a *Custom format* (*format personnalisé*) row; *Output format* and
  *Native size* rows mention it.
- `README.md` / `README.fr.md`: the formats' list, if it names them.
- `SHORTCUTS.md` / `SHORTCUTS.fr.md`: the Width and Height fields' arrows, wheel and Enter (§ Options).

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
| Exact `800 × 600` in a large window: the canvas drawn 800 × 600 device pixels; `4000 × 5000`: fitted | The running app | — |
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
- [x] ~~The fields' bounds: the ratio's range with *Exact pixels* off, the pixels' with it on?~~ →
  The shape between 1:10 and 10:1, a side between 100 and 8192 px (Q&A #9)
- [x] ~~The Custom fields' default — at launch, after a Reset or *Clear all*?~~ → Taken from the
  format shown before Custom is first picked (Q&A #6)
- [x] ~~The Custom thumbnail's label: `Custom`, or its value (`3:2`, `1080 × 1350`)?~~ → Its value
  (Q&A #7)
- [x] ~~An odd exact size (`1081 × 1350`) in a video, which needs even sides?~~ → Rounded to even in
  the video only (Q&A #8)

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

### Iteration 2 — 2026-11-04

Three of the four questions answered (Q&A #6–#8): the Custom fields unset until Custom is first
picked, then filled from the native size of the format in force, the Resets unsetting them; the
thumbnail labelled with its value; an odd exact size rounded to even in the videos only. The bounds'
question (#5) was not understood: asked again in plainer words (#9).

### Iteration 3 — 2026-11-04

Bounds settled (Q&A #9): the shape between 1:10 and 10:1 — applied with *Exact pixels* on too, so
an exact size cannot be more elongated than a ratio —, a side between 100 and 8192 px with exact
pixels. No question left open.

### Iteration 4 — 2026-11-04 — ✅ Implemented

Go given: code, checks and documentation, in a worktree (`feature/custom-output-format`). The
backlog row *Custom output format* is marked with this workfile. Scope frozen as the sections above
stand.

### Iteration 5 — 2026-11-04 — 🧭 Implementation choices

⚠️ Rule broken — *Keyboard Shortcuts* (RULES.md: a commit touching `ProcessCmdKey` updates
`SHORTCUTS.md` / `.fr.md` in the same commit). The whitelist change went in with the UI commit
(`2918b1a`), the rows in the next one (`3ef05ad`): spotted after the commit, a new commit chosen over
rewriting it. Autonomous run: reported instead of asked.

Choices the frozen design did not state:

- **`FormatInitial`** counts the Custom fields too: set, the Resets and *Clear all* are enabled, so
  they really bring back the initial state (the design said *unchanged in meaning*).
- **Live typing** applies a number only while it fits the bounds; out of them, it waits for its commit,
  then is clamped — so the caret never jumps while typing `1350` with exact pixels (`1` is under 100).
- **Keys** of a focused Custom field stay its own (Delete would otherwise delete the selected image).
- **No 0.2 % ratio tolerance** for Custom in `UpdateRatio`.
- **MP4 / GIF entries' tooltips** added for every format, the design asking for the size they produce.
- **Fields**: `NumericUpDown`, 64 px, the first used in the app; *Width* and *× Height* labels; the
  Exact pixels checkbox without icon, as the app's other plain checkboxes.
- **Cell copy** untouched: `GridPreview.RenderCellAlone` reads the ratio alone, as designed.
- **Turning Exact pixels on** with a ratio such as `3 × 2` gives `100 × 100`: each value brought up to
  100, then the elongation's limit — as the design's bounds say.
- **Hand checks** of § Test Impact not run by the agent: the app is launched for the user.

### Iteration 6 — 2026-11-04 — ⚙️ Post-implementation — Exact pixels shown at real size

User request: with *Exact pixels*, when the size fits the working area, the preview shows it at its
real final size. Added § Sizing › The Preview at Real Size: one export pixel a device pixel, centered,
the fitted canvas otherwise; the preview laid out again when only the exact size changes.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5, 6 | 2026-11-04 | `4ea7f45` model and sizing, `59b0a4b` preview / history / exports, `2918b1a` Format tab UI; Iteration 6: the preview at real size |
| Unit tests | 5 | 2026-11-04 | No test project — checks by hand (see Test Impact), left to the user |
| README | 5 | 2026-11-04 | `19779f4` RULES, READMEs, glossaries; `3ef05ad` shortcuts |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How is the custom format kept and shown among the Format thumbnails: a list of the user's formats, a single one with its last value kept, or a single one not kept? | A single Custom format, **not kept** between sessions | 2026-11-04 |
| 2 | How are the ratio or the dimensions entered: one text field parsing `3:2` / `1080x1350`, two fields and a mode, or a small dialog? | **Two fields and a mode**: Width × Height, an *Exact pixels* checkbox (off = a ratio) | 2026-11-04 |
| 3 | With exact dimensions, what becomes of the PNG max size entries and the Video size: exact everywhere, exact unless a cap is smaller, or exact PNG and capped video? | **Exact, unless a cap is smaller**: a PNG max size entry and the Video size still reduce it, the ratio kept | 2026-11-04 |
| 4 | Is the subject straightforward or tricky / long to explore? | **Straightforward**: one scout pass | 2026-11-04 |
| 5 | The fields' bounds: ratio 1:4–4:1 and pixels 100–8192, the Free ratio's range, or 1:10–10:1 and 16–8192? | Not understood — asked again in plainer words (#9) | 2026-11-04 |
| 6 | The Custom fields' default: taken from the current format, a fixed 1080 × 1350, or a fixed 1920 × 1080? | **Taken from the current format**: the native size of the format shown before, *Exact pixels* off; a Reset empties them for next time | 2026-11-04 |
| 7 | The Custom thumbnail's label: its value, or `Custom`? | **Its value**: `3:2`, or `1080×1350` with *Exact pixels* | 2026-11-04 |
| 8 | An odd exact size in a video: rounded to even in the video, or even fields only? | **Rounded to even in the video**: the PNG exact, the MP4 / GIF one pixel less, their tooltip giving the real size | 2026-11-04 |
| 9 | The bounds, re-explained: how elongated a Custom shape may be (*Exact pixels* off) — 4 times, as Free, or 10 times —, the smallest and largest side in pixels (on) — 100, 16 or 1 to 8192? | **Up to 10 times** (10:1 to 1:10); **100 to 8192 px** a side | 2026-11-04 |

---

*Last updated: 2026-11-04*
