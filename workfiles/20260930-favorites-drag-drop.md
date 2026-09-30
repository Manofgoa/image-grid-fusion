# Favorites Drag & Drop

> Working document — adding favorites by drag & drop: files from the Windows Explorer, and cells
> dragged by their ✥ handle, dropped onto the file explorer panel; a pasted image saved first.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a file becomes a favorite only through the heart of its tile, in the file explorer
(`UI/FileExplorerPanel.cs` → `Explorer/Favorites.cs`, `favorites.txt` next to the exe). This task
adds two ways, both ending with a drop onto the **file explorer panel**:

- **Files from the Windows Explorer** (or the desktop) dropped onto the panel.
- **A cell's image**, dragged by its **✥ handle** and released over the panel instead of another
  cell. A cell with a file favors that file; a cell without one (a pasted image) is **saved first**
  into `favorites-from-pasted/` next to the exe, and the saved file becomes the favorite.

The app's own tiles are **not** a source: their heart already does it.

Components: `FileExplorerPanel` (drop target, feedback), `Favorites` (an *add* that never removes —
`Toggle` would un-heart a file already there), `GridPreview` (the ✥ drag leaving the grid),
`MainForm` (wiring the preview's drag to the panel, the pasted image's save).

---

## Agreed Scope

Settled at scoping (Q&A #1–#5):

| Point | Decision |
|---|---|
| Drop target | The **whole file explorer panel** — the search results as well as the favorites list — framed while a drop is hovering |
| Sources | The **Windows Explorer** (files dragged from outside the app) and the **cells**; not the app's tiles |
| Gesture from a cell | The **✥ handle**: it still swaps inside the grid; released over the panel, the cell's image becomes a favorite |
| A cell without a file | Not refused: the image is saved into `favorites-from-pasted/` next to the exe, then that file becomes the favorite |
| Depth | Straightforward: one scout pass |

---

## Drop From the Windows Explorer

- The panel accepts a `FileDrop` (`DataFormats.FileDrop`), shows the **copy** effect and its hover
  frame (§ Feedback), and on the drop adds every file to the favorites, **in the order given**, the
  last one ending at the top (the list is *the newest first*).
- Favorites hold absolute paths and are listed straight from `favorites.txt`, not from the index
  (`FileExplorerPanel.RefreshRows`): a file **outside the base folder** is a favorite like any other,
  shown with its Shell thumbnail.
- **Today**, the panel does not register as a drop target: a file dropped on it falls through to the
  window's handler (`MainForm.OnDragDrop`, *"elsewhere in the window: added like a paste"*) and lands
  in the grid. With this task, the panel takes the drop for the favorites; the grid no longer
  receives files dropped on the panel.
- A **text** dropped on the panel (`TextData`, from another app) is not a file: see Open Questions.

## Drop From a Cell (✥ Handle)

- The ✥ swap drag is an **internal mouse-capture gesture** of `GridPreview` (`OnMouseMove` /
  `OnMouseUp`, `Swap`), not an OLE drag: the preview keeps receiving the mouse outside its bounds.
- While the ✥ drag is over the panel (screen point inside the panel's bounds), the panel shows its
  hover frame and the preview's drop-target highlight is off (no cell targeted).
- Released over the panel: no swap; the preview raises an event with the dragged cell's image;
  `MainForm` resolves the file (below) and hands it to the panel, which adds it to the favorites.
- Released anywhere else outside the grid: nothing, as today.
- Locked while exporting like every other gesture on the cells (the ✥ handle is inert then).

## Images Without a File

`SourceImage.FilePath` is null for three kinds of image (`GridPreview.SourceName`):

| Kind | Arrives by | Named in the preview |
|---|---|---|
| Pasted image | Ctrl+V of a bitmap (`ImageLoader.FromImage`) | *Pasted image* |
| Pasted text | Ctrl+V of a text (`TextPages`) | *Pasted text* |
| Dropped text | A text dragged from another app (`SourceImage.Dropped`) | *Dropped text* |

- A pasted image is saved into **`favorites-from-pasted/`** next to the exe
  (`AppContext.BaseDirectory`), the folder created when missing; the saved file then goes through
  the same path as a file.
- Format, name, what is saved (the original bitmap or the cell as drawn), the texts, and what the
  cell becomes afterwards: see Open Questions.
- A save that fails (disk full, rights) adds nothing and says why in the panel's status line.

## Adding a Favorite

- A new `Favorites.Add(fullPath)` — adds when missing, never removes, writes the file (the
  `Toggle` / `Remove` pattern: atomic write through `.tmp`, a write error thrown as an
  `IsFileError`, the change kept for the session). Several files are added with **one** write.
- A file already a favorite: see Open Questions.
- The panel then refreshes: the favorites list keeps its place (`RefreshRows(keepPlace: true)`), a
  search's tiles get their hearts redrawn.

## Feedback

- **Hover frame**: while an accepted drop hovers the panel (OLE drag or ✥ drag), a frame is drawn
  around the panel's list. It is interaction feedback, not a helper indicator (RULES.md
  § On-Cell Helper Indicators): it takes the preview's **drop-target highlight** colour.
- **After the drop**: the panel's transient status line (`ShowTransient`, 5 s) says what was added —
  `Added to favorites: name` or `Added to favorites: N files`; errors on the same line, in red.

## Documentation

- `README.md` § File explorer: the favorites bullet gains the two drops and the pasted image's
  folder.
- `GLOSSARY.md` *Favorite*: a file hearted in the file explorer **or dropped onto it** — from the
  Explorer, or a cell by its ✥ handle —, a pasted image saved into `favorites-from-pasted/`.

---

## Test Impact

**None.** The repository has **no test project** (`src/` holds `ImageGridFusion` only); like every
previous workfile, the delivery is checked by hand or by a script driving the built app
(`workfiles/20260928-tile-size-slider.md` § Test Impact). The checks planned at delivery:

| Behaviour | Check |
|---|---|
| Explorer drop | Two files dropped from the Windows Explorer onto the search results: both favorites, the last at the top of the list, `favorites.txt` rewritten once |
| Outside the base folder | A file from another drive dropped: listed in the favorites with its thumbnail |
| No longer into the grid | A file dropped onto the panel does not reach the grid |
| ✥ onto the panel | A cell with a file dragged by ✥ onto the panel: its file a favorite, no swap, the cell unchanged |
| ✥ elsewhere | Released outside both the grid and the panel: nothing |
| Pasted image | Ctrl+V of a bitmap, ✥ onto the panel: a file in `favorites-from-pasted/`, now a favorite |
| Frame | Shown while hovering (both drags), gone on leave, drop, or Escape / capture lost |
| Write error | `favorites.txt` read-only: the red status line, the favorite kept for the session |

---

## Open Questions

- [ ] A file dropped that is **already a favorite**: moved to the top (as if added again), or left in
  place?
- [ ] A drop while a **search** is showing: the search stays (the hearts redrawn), or the panel
  switches to the favorites list?
- [ ] The **collapsed panel** (the 20 px strip): does it accept the drop too?
- [ ] A **tile dragged from the panel** and released on the panel (tiles are not a source): nothing,
  or a favorite anyway? (Today it falls through to the window and lands in the grid.)
- [ ] A **folder** dropped from the Explorer: ignored, or its files added?
- [ ] Every file type, or only what the cells can show? (The cells show anything, through the Shell
  thumbnail as a last resort.)
- [ ] The pasted image's **format and name** in `favorites-from-pasted/` (e.g. PNG,
  `pasted-20260930-184512.png`)?
- [ ] What is saved: the **original pasted bitmap**, or the cell **as drawn** with its effects?
- [ ] After the save, does the **cell adopt the saved file** (its name shown instead of *Pasted image*,
  *Show in Explorer* available, a second ✥ drop reusing the file instead of saving a duplicate)?
- [ ] **Pasted and dropped texts** (no file either): saved too — as the rendered page (PNG) or as the
  text itself (`.txt` / `.rtf`) — or refused with a message?
- [ ] A favorite from `favorites-from-pasted/` **un-hearted**: its file kept, or deleted?
- [ ] A **text** (not a file) dropped from another app onto the panel: ignored, or saved like a
  pasted text?
- [ ] A drop onto the panel **during an export**: accepted (the favorites do not touch the grid), or
  refused like the grid's drops?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-30

Scoping: the request limited cells to images with a file (*"not a pasted one"*); the user corrected
it before any answer — a pasted image is saved into `favorites-from-pasted/` next to the exe, then
favored. Scoping batch answered: the whole panel as the target, the Windows Explorer and the cells
as sources (not the tiles), the ✥ handle released over the panel, a straightforward subject.

Scout pass (done directly: one chain of questions around the panel, the favorites and the ✥ drag):
the ✥ drag is a mouse-capture gesture of `GridPreview`, so it reaches the panel by screen point, not
by OLE; the panel registers no drop target today, its drops falling through to the window's
*add like a paste*; `Favorites` only has `Toggle` / `Remove`, an `Add` is needed; three kinds of
image have no file (pasted image, pasted text, dropped text). Design sections drafted; thirteen
open questions listed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Does not apply — no test project, checked by hand or script (§ Test Impact) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | *(unprompted correction)* | Not only cells with a file: a pasted image is saved into `favorites-from-pasted/` under the exe, then favored | 2026-09-30 |
| 2 | Where is a file dropped to become a favorite? | The whole file explorer panel | 2026-09-30 |
| 3 | Which sources besides the cells? | The Windows Explorer (not the app's tiles) | 2026-09-30 |
| 4 | From a cell, which gesture starts the drag? | The ✥ handle released outside the grid, over the panel | 2026-09-30 |
| 5 | Straightforward or tricky / long? | Straightforward | 2026-09-30 |
| 6 | A file dropped that is already a favorite: moved to the top, or left in place? | | |
| 7 | A drop while a search shows: the search stays, or the panel switches to the favorites? | | |
| 8 | Does the collapsed panel (the strip) accept the drop? | | |
| 9 | A tile dragged from the panel and released on it: nothing, or a favorite? | | |
| 10 | A folder dropped from the Explorer: ignored, or its files added? | | |
| 11 | Every file type, or only what the cells can show? | | |
| 12 | The pasted image's format and name? | | |
| 13 | Saved: the original pasted bitmap, or the cell as drawn? | | |
| 14 | After the save, does the cell adopt the saved file? | | |
| 15 | Pasted and dropped texts: saved (PNG render, or the text), or refused? | | |
| 16 | A favorite from `favorites-from-pasted/` un-hearted: file kept, or deleted? | | |
| 17 | A text (not a file) dropped from another app onto the panel? | | |
| 18 | A drop onto the panel during an export: accepted, or refused? | | |

---

*Last updated: 2026-09-30*
