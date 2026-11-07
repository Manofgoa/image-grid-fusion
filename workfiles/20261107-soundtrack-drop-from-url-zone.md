# Soundtrack Drop From the URL Zone

> Working document — dropping the URL zone's media (and any file) onto the Soundtrack tab or its options,
> and onto the Layers, with a per-position drop target on the Global toolbar.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The user drags the URL zone's thumbnail — a media downloaded from X / Twitter, YouTube… — onto the
**Soundtrack** tab and gets the **forbidden cursor**: nothing happens. The same must work for the
**Layers** if it does not already.

On paper it should work: the README (§ Soundtrack) says *"A file dropped on the Global tabs or options
becomes the soundtrack"*, and `MainForm` wires `DragEnter` / `DragDrop` on the two rows
(`_globalTabsRow`, `_globalOptionsRow`, `MainForm.cs` ~1392) — Layers first (`DroppedOnLayers`), then the
Grid background's options (`DroppedOnGridBackground`), else `LoadSoundtrackAsync(paths[0])`. The URL zone
drags a plain `DataFormats.FileDrop` (`FileExplorerPanel.StartUrlDrag`, `DragDropEffects.Copy`), as the
tiles and GIPHY results do.

What the code shows about the gap:

- Only the **rows** have `AllowDrop`; the controls they hold — the tab strip (`EffectTabs<GlobalEffect>`,
  a custom `Control`), the options host and every options panel — do not, and no `DragOver` is handled:
  the rows decide the effect once, at `DragEnter`, for the whole row.
- The exact reason the cursor is refused over the tab is **not established** by reading (see
  § Root Cause). The design below does not depend on it: every control of the two rows becomes an
  explicit drop target deciding the effect at each position.

Scope: one drop route for the Global toolbar, deciding **per position** what a drop does — Soundtrack,
Layers, Grid background, or nothing — for **any file drag** (URL zone, explorer tiles, GIPHY results,
Windows Explorer).

---

## Drop Targets on the Global Toolbar

One decision, `GlobalDrop` (a pure function, tested), from where the cursor stands:

| Cursor over | Selected global tab | Drop does |
|---|---|---|
| The **Soundtrack** tab | any | Soundtrack |
| The **Layers** tab | any | Layers |
| The **Grid background** tab | any | Grid background (its image) |
| Another tab (Format, Fade, Borders, Seams, Cascade) | any | **Nothing — refused** |
| The global options toolbar | Soundtrack | Soundtrack |
| The global options toolbar | Layers | Layers |
| The global options toolbar | Grid background | Grid background (its image, as today) |
| The global options toolbar | another tab, or none | **Nothing — refused** |
| The rows' empty parts (label, padding, Reset buttons) | any | **Nothing — refused** |

- **Restricted** (user decision): a drop on the Format, Borders… tab or options **no longer** loads the
  soundtrack. Only the Soundtrack tab and its options do.
- The Grid background's **tab** takes a drop too (user decision), so the three file-taking effects
  follow one rule: their tab, or their options while selected.
- A drop on a tab **selects it** before acting, as the Layers drop does today
  (`DropLayerFilesAsync` → `SelectGlobalEffect`): the Soundtrack's options then show the file loaded.
- **The cursor tells before the drop**: `DragOver` gives `Copy` where the drop would act, `None` (the
  forbidden cursor) elsewhere — computed at each move, never once at `DragEnter`.
- Every control of the two rows is a drop target — the tab strip, the options host, every options
  panel and its children — each routed to the one handler (a recursive wiring, a control added later
  covered by itself). The cell effects' rows and the preview keep their own handlers, untouched.
- **While exporting** the Global toolbar is locked: every drop is **refused at the hover** — `None`,
  the forbidden cursor — Soundtrack, Layers and Grid background alike.

## Soundtrack Drop

- **Any file drag** — the URL zone, the explorer's tiles, the GIPHY results, Windows Explorer — goes
  through the same route (user decision).
- **A file that cannot carry sound is refused at the hover** (user decision): the drag holds a file
  whose extension may carry a sound track — `SoundtrackFile.AudioExtensions` or the video extensions —
  → `Copy`; none → `None`, the forbidden cursor. One check, `SoundtrackFile.MayCarrySound(path)`, the
  video extensions moved where both the picker and it read them.
- **Several files**: the first one that may carry sound is loaded; the others are ignored.
- **At the drop**, `LoadSoundtrackAsync` as today: a file Windows reads no sound track from — a video
  without sound, a GIPHY MP4, a YouTube video track downloaded without ffmpeg — changes nothing, the
  status line saying so (`Strings.MainStatusNoSoundTrack`, existing). Loaded, it turns the Soundtrack on,
  plays the whole file, keeps the previous Loops (RULES.md § Soundtrack Part).

## Layers Drop

- Same route, same per-position decision: the Layers tab, or the options while Layers is selected →
  `DropLayerFilesAsync`, as today. Any file `AddLayerFilesAsync` takes; no extension check at the hover
  beyond what the drop already refuses.
- Whatever made the URL zone's drag refused over the Soundtrack tab is fixed for the Layers by the same
  change: both tabs live in the same strip.

## Root Cause

To be established at the start of the implementation run, by reproducing the refused drag (the URL zone
→ the Soundtrack tab, then → the Layers tab) before any change, and recorded in the 🧭 entry. Leads:
the tab strip and the options panels are not drop targets themselves; the rows decide at `DragEnter`
only. The design fixes it either way; the cause is recorded so the rules can say it.

## Docs and Rules

- **RULES.md § Global Toolbar**: a line — a file dropped onto the Global toolbar goes to the effect under
  the cursor only — the Soundtrack, the Layers or the Grid background, its tab or its options while
  selected —, the cursor refusing elsewhere and while exporting; one decision, `GlobalDrop`.
- **README.md / README.fr.md § Soundtrack**: "dropped on the Global tabs or options" → "dropped onto the
  Soundtrack tab, or its options while it is selected — from the URL zone, the file explorer or
  Windows' Explorer"; a file that cannot carry sound refused. § Layers: the URL zone named among the
  sources. § Grid background: its tab takes a drop too.
- The comment above `_globalTabsRow` (*"A file dropped anywhere on either row becomes the soundtrack"*)
  follows.
- No new UI text is expected (the refusal is the cursor; the no-sound message exists). One added after
  all goes into both `.resx` files.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `GlobalDrop`: the Soundtrack tab → Soundtrack, whatever tab is selected | `tests/ImageGridFusion.Tests/UI/GlobalDropTests.cs` | Create |
| `GlobalDrop`: the Layers tab → Layers; the options with Layers selected → Layers | `tests/ImageGridFusion.Tests/UI/GlobalDropTests.cs` | Create |
| `GlobalDrop`: the Grid background tab, and the options with it selected → Grid background | `tests/ImageGridFusion.Tests/UI/GlobalDropTests.cs` | Create |
| `GlobalDrop`: the Format / Borders tab, the options of another effect or none → nothing |
| `GlobalDrop`: while exporting → nothing, wherever the cursor stands | `tests/ImageGridFusion.Tests/UI/GlobalDropTests.cs` | Create |
| `SoundtrackFile.MayCarrySound`: mp3, m4a, mp4, webm (any case) yes; jpg, png, gif, txt, no extension no | `tests/ImageGridFusion.Tests/UI/GlobalDropTests.cs` | Create |
| The Soundtrack's pick among several files: the first that may carry sound, none → refused | `tests/ImageGridFusion.Tests/UI/GlobalDropTests.cs` | Create |

---

## Open Questions

- [x] ~~**1. The Grid background tab**: should its tab take a drop too?~~ → Yes, its tab too: one rule
  for the Soundtrack, the Layers and the Grid background
- [x] ~~**2. While exporting**: refuse at the hover or at the drop?~~ → At the hover, the forbidden cursor

---

## Design Iterations

### Iteration 1 — 2026-11-07

Initial design. Scoping answers (Q&A 1–6): the drop target is the Soundtrack tab and its options; any
file drag uses the route; a file without sound is refused with the forbidden cursor; a straightforward
exploration; the user sees the forbidden cursor over the Soundtrack tab today; the soundtrack drop is
restricted to the Soundtrack tab and its options. Proposed: one per-position decision for the Global
toolbar (`GlobalDrop`), every control of its rows a drop target with a `DragOver`, the extension check
`SoundtrackFile.MayCarrySound`, the root cause reproduced and recorded at the start of the run.

### Iteration 2 — 2026-11-07

Open Questions answered (Q&A 7–8): the Grid background's tab takes a drop like its options, one rule
for the three file-taking global effects; while exporting, every drop on the Global toolbar is refused at
the hover. No question left open.

### Iteration 3 — 2026-11-07 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree on `feature/soundtrack-drop-from-url-zone`.
The scope is frozen as the sections above stand.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| RULES.md | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Soundtrack: where may the URL zone's thumbnail be dropped? | The tab and its options, like the Layers | 2026-11-07 |
| 2 | Soundtrack: which drag sources? | Any file drop — URL zone, explorer tiles, GIPHY, Windows Explorer — one route | 2026-11-07 |
| 3 | Soundtrack: a content without sound? | Refused, said why — the forbidden cursor at the hover when detectable, else the status line | 2026-11-07 |
| 4 | Exploration depth? | Straightforward | 2026-11-07 |
| 5 | What happens today when the thumbnail is dragged onto the Soundtrack tab? | Forbidden cursor, nothing | 2026-11-07 |
| 6 | Keep the soundtrack drop on every Global tab and options, or restrict it? | Restrict to the Soundtrack tab and its options | 2026-11-07 |
| 7 | Should the Grid background's tab take a drop too? | Yes, its tab too | 2026-11-07 |
| 8 | While exporting: refuse at the hover, or at the drop? | At the hover | 2026-11-07 |

---

*Last updated: 2026-11-07*
