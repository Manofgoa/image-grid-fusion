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
- **Emoji icons cached on disk** (agreed, Q&A 6): the bitmaps drawn kept in a kebab-case folder next to the exe
  (one `FolderName` constant, `../CLAUDE.md` § Folder Names), keyed by glyph, size and the Segoe UI Emoji file's
  version — a later start reads small PNGs instead of drawing. **Written by the normal instance only**: a
  `--new-instance` reads it and draws what is missing without writing (RULES.md § Single Instance). Expected: what
  is left of the icons' time after the first launch.
- **The font drop-down filled when it opens**: its items (every installed family) added at the first drop-down,
  its selected font alone at start-up. Expected: ~150 ms.
- ~~Hidden options built at first use~~ — not taken (Q&A 9): a heavy refactor of `MainForm`, its controls being
  fields, and the options toolbar's height (the tallest tab's, RULES.md § Options Toolbar) to know without them.

### B — Build and Runtime Settings

- ReadyToRun is already on for `dotnet publish`; measured gain ~12 %.
- For the Debug build: ReadyToRun or `TieredPGO` off at build — little gain expected, and a Debug build is meant
  to JIT. Self-contained or composite ReadyToRun: a much larger exe for a few percent.

### C — Visible Changes Allowed (taken: two parts, Q&A 9)

- **The file explorer panel built after the window shows**: the window appears with its right-hand area at the
  panel's width (`AppSettings.ExplorerWidth`, open or collapsed as remembered), empty on the panel's background;
  the panel is built and filled right after `MainForm.OnShown`, before `FileExplorerPanel.Start` reads the index.
  Every reader of `_explorer` before then must cope with it missing, or a light placeholder stands for it until
  it is built.
- **The icons drawn after the window shows, while their cache is empty** — a first launch, a new DPI, Segoe UI
  Emoji updated: the controls show without their icon, then get it an instant later; with the cache filled
  (§ A), the icons are read before the window shows, as today.
- Not taken: the tabs' options built at their first selection — a heavy refactor of `MainForm` for an
  invisible part (no tab is selected at start-up).

---

## Test Impact

The repository has **no test project** yet (`src/ImageGridFusion.slnx` holds the app alone). The behaviours below
are the testable ones; a go including unit tests creates `tests/ImageGridFusion.Tests` for them.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| An icon's cache key changes with its glyph, its size and the emoji font's version | `tests/ImageGridFusion.Tests/UI/IconCacheTests.cs` | Create |
| A cached icon read back is pixel for pixel the one written; a missing or unreadable file reads as no entry | `tests/ImageGridFusion.Tests/UI/IconCacheTests.cs` | Create |
| A `--new-instance` reads the cache and writes nothing to it | `tests/ImageGridFusion.Tests/UI/IconCacheTests.cs` | Create |
| An emoji icon drawn at the reduced em fills its square as at 192 em (same ink bounds within a pixel) | `tests/ImageGridFusion.Tests/UI/UiIconTests.cs` | Create |

---

## Open Questions

- [x] ~~Which latitude levels — A, B, C — does the work take?~~ → A and C; B pending its explanation (Q&A 5, 8)
- [ ] B, explained in § B: taken or not?
- [x] ~~Which parts of C are deferred after the window shows — the file explorer panel, the icons on a first launch, the tabs' options?~~ → The file explorer panel, and the icons while their cache is empty; the tabs' options stay built at start-up (Q&A 9)
- [x] ~~May the emoji icons be cached on disk next to the exe, and is a `--new-instance` allowed to write that cache?~~ → Yes, written by the normal instance only; a `--new-instance` reads it and writes nothing (Q&A 6)
- [x] ~~Should the start-up time be measured inside the app (a Debug log of its phases) to check the gain, or by the
  external script only?~~ → Neither: the gain is judged by the user's own feel; nothing added to the app (Q&A 7)

---

## Design Iterations

### Iteration 1 — 2026-11-05

Scope set to the window appearing (Q&A 1); the Debug build is the one the user runs (Q&A 2). Start-up measured
and profiled: the `MainForm` constructor is 81 % of the time to the window, the emoji icons a quarter of it.
Levers listed by latitude level, for the user to choose.

### Iteration 2 — 2026-11-05

Levels A and C taken, B to be explained further (Q&A 5). The emoji icons' disk cache agreed, written by the
normal instance only (Q&A 6). No measurement added to the app: the user judges the gain by feel (Q&A 7).

### Iteration 3 — 2026-11-05

C narrowed to two parts (Q&A 9): the file explorer panel built after the window shows, and the icons drawn after
it while their cache is empty; the tabs' options built at first selection dropped. B still to be explained in
plain words (Q&A 8, 10). Test Impact filled: no test project exists yet.

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
| 5 | Which latitude levels (A, B, C, detailed in § Candidate Levers)? | A and C taken; B to be explained further — re-asked below | 2026-11-05 |
| 6 | Emoji icons cached on disk — and by a `--new-instance`? | Yes, written by the normal instance only | 2026-11-05 |
| 7 | Start-up time measured inside the app, or by the external script only? | Neither: the user's own feel | 2026-11-05 |
| 8 | B explained: taken or not? | Asked again what B does — the explanation had not reached them; re-asked in plain text below | 2026-11-05 |
| 9 | Which parts of C are deferred after the window shows? | The file explorer panel, and the icons on a launch whose cache is empty; not the tabs' options | 2026-11-05 |
| 10 | B, explained in plain words: taken or not? | | 2026-11-05 |

---

*Last updated: 2026-11-05*
