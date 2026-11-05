# Initial Load Speed

> Working document — making the main window appear sooner after the exe is launched.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The time from launching `ImageGridFusion.exe` to its main window shown and usable. The user launches the
**Debug** build straight from its build folder and is fine with that; the goal is to optimise what can be
optimised in the start-up path as it stands.

Out of scope (user's answer, Q&A 1): the file explorer's index, favorites and first thumbnails (already loaded
after the window is shown, `MainForm.OnShown` → `FileExplorerPanel.Start`), the files given as arguments, the
window brought back from the tray.

---

## Measurements — 2026-11-05

Measured from the process start to a main window whose title carries the session name, then to input idle;
`--new-instance --title …`, the Debug folder's `settings.json`, 4 runs each (`scratchpad/measure.ps1`).

| Build | Window shown | Input idle |
|---|---|---|
| Debug (`bin/Debug/…/win-x64`) | 1 434 – 1 823 ms (≈ 1.45 s warm) | 1 435 – 2 132 ms |
| Release, `dotnet publish` (single-file, ReadyToRun) | 1 236 – 1 322 ms (≈ 1.28 s) | 1 323 – 1 490 ms |

ReadyToRun saves only ~12 %: the JIT is not the main cost.

### Where the Time Goes

A sampled thread-time trace of the Debug build (`dotnet-trace`, profile `dotnet-sampled-thread-time`), main
thread only, up to `MainForm.OnShown` — 2 078 ms under tracing (≈ 1.4× the untraced time):

| Part | Traced ms | Share | Notes |
|---|---|---|---|
| Runtime start, `Program.Main` before the form | ~120 | 6 % | Components manifest 26, `ComponentRegistry` 23 |
| **`MainForm` constructor** | **1 687** | **81 %** | |
| ↳ **Emoji icons** (`UiIcon.DrawEmoji`, all callers) | **~540** | **26 %** | Each glyph drawn by `TextRaster.Render` at **192 em** (`TextRaster.EmSize`), then its ink found and scaled down to 16–24 px — ~5 ms an icon, ~100 icons (`UpdateEffectIcons` 493, `FileExplorerPanel.UpdateIcons` 142, `UpdateLayerIcons` 179 — overlapping) |
| ↳ `FileExplorerPanel` constructor | 332 | 16 % | Its icons ~140 of it, `UrlZone` 88 |
| ↳ `BuildLayerOptions` | 217 | 10 % | The font drop-down's handle and items: `ComboBox.UpdateItemHeight` 153 |
| ↳ Layout (`ResumeLayout`, auto-scale) | ~100 | 5 % | |
| ↳ Menus (`ToolStripItemCollection.Add`), `UpdateButtons`, the rest | ~200 | 10 % | |
| `TrayApplicationContext` — the window shown (`SetVisibleCore`) | 218 | 10 % | Handle creation of every control, first layout and paint |

---

## Candidate Levers

What each latitude level would allow (Q&A 3 asks which ones the user takes).

### A — Same Behaviour, Less Work

Nothing visible changes; the same window appears sooner.

- **Emoji icons drawn at their size**: rendered at an em close to the icon (a few times its side) instead of
  192, so Direct2D rasterises ~1/50 of the pixels and `InkBounds` scans as few. Expected: most of the ~540 ms.
- **Emoji icons cached on disk** (optional, Q&A 4): the bitmaps drawn kept in a kebab-case folder next to the exe,
  keyed by glyph, size and the Segoe UI Emoji file's version — a later start reads small PNGs instead of
  drawing. Expected: what is left of the icons' time after the first launch.
- **The font drop-down filled when it opens**: its items (every installed family) added at the first drop-down,
  its selected font alone at start-up. Expected: ~150 ms.
- **Hidden options built at first use**: the options of a tab not selected at start-up (none is) created when
  the tab is first selected. Larger refactor: the options toolbar's height is the tallest tab's (RULES.md
  § Options Toolbar), so each tab's preferred height must be known without building it. Expected: part of the
  constructor's controls and layout time.

### B — Build and Runtime Settings

- ReadyToRun is already on for `dotnet publish`; measured gain ~12 %.
- For the Debug build: ReadyToRun or `TieredPGO` off at build — little gain expected, and a Debug build is meant
  to JIT. Self-contained or composite ReadyToRun: a much larger exe for a few percent.

### C — Visible Changes Allowed

- **Progressive window**: shown with the preview and the toolbars first, the file explorer panel and the
  options of the tabs built right after `OnShown`, so the window appears in a fraction of the time and fills in
  over a few hundred ms.
- **Icons filled in after the window shows**: controls shown without their icons, which appear an instant later.

---

## Test Impact

To be filled once the levers are chosen.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|

---

## Open Questions

- [ ] Which latitude levels — A, B, C — does the work take?
- [ ] May the emoji icons be cached on disk next to the exe, and is a `--new-instance` allowed to write that cache?
- [ ] Should the start-up time be measured inside the app (a Debug log of its phases) to check the gain, or by the
  external script only?

---

## Design Iterations

### Iteration 1 — 2026-11-05

Scope set to the window appearing (Q&A 1); the Debug build is the one the user runs (Q&A 2). Start-up measured
and profiled: the `MainForm` constructor is 81 % of the time to the window, the emoji icons a quarter of it.
Levers listed by latitude level, for the user to choose.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which moment is the "initial load": the window appearing, the file explorer, the files given as arguments, the window back from the tray? | The window appearing | 2026-11-05 |
| 2 | What prompted it, how long does it take, in which build? | Launches the Debug exe from its build folder, which suits them for now; optimise what can be optimised at load | 2026-11-05 |
| 3 | How far may we go: same behaviour, build settings too, visible changes too? | Asked to detail the three possibilities first — re-asked below | 2026-11-05 |
| 4 | Straightforward or tricky / long? | Straightforward — a single scout pass | 2026-11-05 |
| 5 | Which latitude levels (A, B, C, detailed in § Candidate Levers)? | | 2026-11-05 |
| 6 | Emoji icons cached on disk — and by a `--new-instance`? | | 2026-11-05 |
| 7 | Start-up time measured inside the app, or by the external script only? | | 2026-11-05 |

---

*Last updated: 2026-11-05*
