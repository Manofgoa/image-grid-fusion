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

### After the Implementation — 2026-11-05

The same script, the worktree's Debug build, `--new-instance` — so the icon cache is **never written** and every
emoji icon is drawn after the window, a first launch's case; a normal instance's later starts read the cache instead:

| Build | Window shown | Input idle |
|---|---|---|
| Debug, before | 1 434 – 1 823 ms (≈ 1.45 s warm) | 1 435 – 2 132 ms |
| Debug, icons at their size and deferred | 1 106 – 1 373 ms (≈ 1.12 s) | 1 107 – 1 376 ms |
| + the font drop-down filled when opened | 1 034 – 1 322 ms (≈ 1.04 s) | 1 252 – 1 529 ms |
| + the file explorer's content held | 1 013 – 1 293 ms (≈ 1.04 s) | 1 138 – 1 294 ms |

Traced, the main thread reaches `OnShown` in 1 021 ms instead of 2 078; the `MainForm` constructor takes 766 ms
instead of 1 687.

---

## Candidate Levers

What each latitude level would allow (Q&A 3 asks which ones the user takes).

### A — Same Behaviour, Less Work

Nothing visible changes; the same window appears sooner.

- **Emoji icons drawn at their size**: rendered at **four times the icon's side, 48 em at least**
  (`UiIcon.EmFor`, `Oversampling`) instead of 192, then scaled down as before.
- **Emoji icons cached on disk** (agreed, Q&A 6): the bitmaps drawn kept in a kebab-case folder next to the exe
  (`icon-cache\`, `IconCache.FolderName`, `../CLAUDE.md` § Folder Names): one subfolder per stamp —
  `IconCache.DrawingVersion` and Segoe UI Emoji's file size and date —, the older ones deleted at the first write;
  one PNG per glyph and size, its code points then its size (`1f50d_20.png`), written aside then moved over, off the
  UI thread, copied byte for byte both ways — a later start reads small PNGs instead of drawing. **Written by the normal instance only**: a
  `--new-instance` reads it and draws what is missing without writing (RULES.md § Single Instance). Expected: what
  is left of the icons' time after the first launch.
- **The font drop-down filled when it opens**: its items (every installed family) added at the first drop-down
  or the first focus — keyboard included (`MainForm.FillFonts`) —, its selected font alone at start-up.
- ~~Hidden options built at first use~~ — not taken (Q&A 9): a heavy refactor of `MainForm`, its controls being
  fields, and the options toolbar's height (the tallest tab's, RULES.md § Options Toolbar) to know without them.

### B — Build and Runtime Settings (not taken, Q&A 10)

- ReadyToRun is already on for `dotnet publish`; measured gain ~12 %.
- For the Debug build: ReadyToRun or `TieredPGO` off at build — little gain expected, and a Debug build is meant
  to JIT. Self-contained or composite ReadyToRun: a much larger exe for a few percent.

### C — Visible Changes Allowed (taken: two parts, Q&A 9)

- **The file explorer's content held until the window has shown** (implemented variant, Iteration 6): the panel
  object is still constructed with the window — `MainForm` reads and sets it in ~130 places — but its content is
  hidden (`FileExplorerPanel.ContentHeld`), so none of its controls is created, laid out or painted before the
  window shows: its area shows the panel's background at its width, then the content is let go at the first idle
  moment after the window has painted (`MainForm.FinishStartup`). `FileExplorerPanel.Start` still runs from
  `OnShown`.
- **The icons drawn after the window shows, while their cache is empty** — a first launch, a new DPI, Segoe UI
  Emoji updated: while `UiIcon.Deferring` (set in `Program.Main`, unless the app starts hidden), an icon missing
  from the cache is handed out blank; `FinishStartup` draws them (`UiIcon.DrawDeferred`), paints each into its
  shared bitmap and every copy still alive, gives the split buttons theirs again (`UpdateEffectIcons`) and repaints
  the window. With the cache filled (§ A), the icons are read before the window shows.
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
| An icon of another size is not read; the first write deletes the older stamps' folders | `tests/ImageGridFusion.Tests/UI/IconCacheTests.cs` | Create |

---

## Open Questions

- [x] ~~Which latitude levels — A, B, C — does the work take?~~ → A and C; B not taken (Q&A 5, 8, 10)
- [x] ~~B, explained in § B: taken or not?~~ → Not taken: the time goes into the app's own code, not the JIT (Q&A 10)
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

### Iteration 4 — 2026-11-05

B explained in plain words, then not taken (Q&A 10): the build settings stay as they are. No open question left;
the scope is A and the two parts of C.

### Iteration 5 — 2026-11-05 — ✅ Implemented

Go given: code, unit tests and documentation (Q&A 11), in a worktree on `feature/initial-load-speed`. The scope is
frozen as the sections above stand: the emoji icons drawn at their size and cached on disk by the normal
instance, the font drop-down filled at its first opening, the file explorer panel built after the window shows,
the icons drawn after it while their cache is empty.

### Iteration 6 — 2026-11-05 — 🧭 Implementation choices

- ⚠️ **A new dependency, for the tests only** — CONTRIBUTING.md § Pull requests says *No new dependency*: the test
  project uses **MSTest 4.2.3** (the version in the local NuGet cache), never shipped; the app's dependencies are
  unchanged. CONTRIBUTING.md § Tests says so.
- **The file explorer's content held, not the panel built later**: building the panel after `OnShown` would have
  meant guarding ~130 reads and writes of `_explorer` in `MainForm`; its content hidden until the first idle moment
  spares its controls' creation, layout and paint, the panel object still built with the window.
- **"After the window shows" is the first idle moment** (`Application.Idle`, once): a `BeginInvoke` from `OnShown`
  would run before the window's first paint. The explorer's content and the deferred icons share that moment
  (`MainForm.FinishStartup`).
- **Deferred icons filled in place**: the copies handed out blank are painted over once drawn, so no caller changes;
  the split buttons, which copy their image into a native list, are given theirs again by `UpdateEffectIcons`.
- **No deferral when starting hidden** (`--tray`): the tray menu would show blank icons until the window opens.
- **The emoji drawn at four times its side, 48 em at least**; one cache subfolder per stamp, the older ones deleted
  at the first write; files written aside then moved over.
- **The font drop-down filled at its first focus too**, so the arrow keys find every family.
- **A defect the tests found, fixed**: GDI+ rounds the faint pixels of an icon when it is copied by drawing; the cache
  copies byte for byte (`IconCache.ExactCopy`) when it writes and reads.
- **Measured from the first-launch case only**: every agent launch is a `--new-instance`, which never writes the cache;
  the later starts of a normal instance read it — not measured.
- Not done, reported: `UiIcon.Copy` still copies by drawing (`new Bitmap(...)`), rounding faint edge pixels as before
  this work.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5, 6 | 2026-11-05 | Icons at their size, cached, deferred; font drop-down filled when opened; explorer content held |
| Unit tests | 5, 6 | 2026-11-05 | New project `tests/ImageGridFusion.Tests` (MSTest): `IconCacheTests`, `UiIconTests` — 12 passing |
| README | 5, 6 | 2026-11-05 | `--new-instance` line (EN, FR); RULES.md § Start-Up and § Icons; CONTRIBUTING § Tests (EN, FR); GLOSSARY *Icon* (EN, FR) |

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
| 10 | B, explained in plain words: taken or not? | No, without B | 2026-11-05 |
| 11 | Implementation go: scope, and where? | Code, unit tests and documentation; in a worktree | 2026-11-05 |

---

*Last updated: 2026-11-05*
