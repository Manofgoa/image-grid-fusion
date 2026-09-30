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

- The drop target is the **whole panel**, open or **collapsed** (the 20 px strip included).
- The panel accepts a `FileDrop` (`DataFormats.FileDrop`), shows the **copy** effect and its hover
  frame (§ Feedback), and on the drop adds every file to the favorites, **in the order given**, the
  last one ending at the top (the list is *the newest first*).
- **Every file type** is accepted, as the cells show anything (the Shell thumbnail as a last resort).
- A **folder** is **ignored**, the files dropped with it added; the message says how many folders
  were skipped. A drop of folders only adds nothing and says so.
- Favorites hold absolute paths and are listed straight from `favorites.txt`, not from the index
  (`FileExplorerPanel.RefreshRows`): a file **outside the base folder** is a favorite like any other,
  shown with its Shell thumbnail.
- **Today**, the panel does not register as a drop target: a file dropped on it falls through to the
  window's handler (`MainForm.OnDragDrop`, *"elsewhere in the window: added like a paste"*) and lands
  in the grid. With this task, the panel takes the drop for the favorites; the grid no longer
  receives files dropped on the panel.
- A **tile of the panel** dragged and released on the panel itself does **nothing** (the tiles are not
  a source): the panel knows the drag is its own (a flag held during its `DoDragDrop`) and shows the
  *none* effect, no frame.
- A **text** dropped on the panel (`TextData`, from another app) is not a file: **ignored** — no
  frame, the *none* effect.

## Drop From a Cell (✥ Handle)

- The ✥ swap drag is an **internal mouse-capture gesture** of `GridPreview` (`OnMouseMove` /
  `OnMouseUp`, `Swap`), not an OLE drag: the preview keeps receiving the mouse outside its bounds.
- While the ✥ drag is over the panel (screen point inside the panel's bounds), the panel shows its
  hover frame and the preview's drop-target highlight is off (no cell targeted).
- Released over the panel: no swap; the preview raises an event with the dragged cell's image;
  `MainForm` resolves the file (below) and hands it to the panel, which adds it to the favorites.
- Released anywhere else outside the grid: nothing, as today.
- Locked while exporting like every other gesture on the cells (the ✥ handle is inert then). A drop
  **from the Windows Explorer** onto the panel is **accepted during an export**: the favorites do not
  touch the grid.

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
- Saved as **PNG** (lossless, alpha kept), named **`pasted-yyyyMMdd-HHmmss.png`** from the local time
  of the drop, `-2`, `-3`… appended when the name is taken.
- What is saved is the **original pasted bitmap** (`SourceImage.Bitmap`), **without the cell's
  effects** (they live on `ImageLook`, applied by `Compositor.DrawCell`): a source file any cell can
  reuse.
- **Texts** (pasted or dropped) are saved as **the text itself**, not a picture, in the form
  `TextData.Parse` read it from — kept on the `SourceImage` when the text arrives: its RTF →
  `pasted-yyyyMMdd-HHmmss.rtf`, else its HTML → `.html` (the fragment the clipboard header points
  to), else its plain text → `.txt`, UTF-8. It reads back with every page and its styles (§ Reading
  Styled Text Files).
- The cell **adopts the saved file**: `SourceImage.FilePath` becomes the saved path, so the cell
  shows the file's name instead of *Pasted image* / *Pasted text* / *Dropped text*, *Show in
  Explorer* works, and a second ✥ drop reuses the file instead of saving a duplicate; its effects are
  kept (nothing is reloaded).
- A favorite **inside `favorites-from-pasted/`** that is **un-hearted** is **deleted**, sent to the
  **Recycle Bin**: the app created it for the favorite. Anywhere else, un-hearting leaves the file
  alone, as today. A cell that adopted the file keeps its image (loaded in memory).
- A save that fails (disk full, rights) adds nothing and says why in the panel's status line.

## Reading Styled Text Files

- Today every text file opens as **plain text** (`TextPages.TryOpen` → `StyledText.Plain`, the
  extension playing no part): an `.rtf` shows its markup.
- With this task, a file ending in **`.rtf`** is read with `RtfReader`, one ending in **`.htm`** or
  **`.html`** with `HtmlReader` (it takes a whole document as HTML), the same readers as a paste; the
  1 MB limit and the text checks unchanged. A file they cannot read (blank, not parsable) falls back
  to plain text, as today. Every other extension is unchanged.
- It holds wherever a file is loaded — a drop, Ctrl+V of files, the picker, the explorer's tiles —
  not only for the saved favorites.

## Adding a Favorite

- A new `Favorites.Add(fullPath)` — adds when missing, never removes, writes the file (the
  `Toggle` / `Remove` pattern: atomic write through `.tmp`, a write error thrown as an
  `IsFileError`, the change kept for the session). Several files are added with **one** write.
- A file **already a favorite** is **moved to the top**, as if added again.
- The panel then refreshes: the favorites list keeps its place (`RefreshRows(keepPlace: true)`); a
  **search** showing **stays**, its tiles' hearts redrawn.

## Feedback

- **Hover frame**: while an accepted drop hovers the panel (OLE drag or ✥ drag), a frame is drawn
  around the panel's list. It is interaction feedback, not a helper indicator (RULES.md
  § On-Cell Helper Indicators): it takes the preview's **drop-target highlight** colour.
- **After the drop**: the panel's transient status line (`ShowTransient`, 5 s) says what was added —
  `Added to favorites: name` or `Added to favorites: N files`; errors on the same line, in red. The
  panel **collapsed**, the message goes to the window's status line instead.

## Documentation

- `README.md` § File explorer: the favorites bullet gains the two drops and the pasted image's
  folder.
- `README.md`, the text files: `.rtf` and `.html` files shown with their styles.
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
| Pasted image | Ctrl+V of a bitmap, ✥ onto the panel: `favorites-from-pasted/pasted-….png`, the pasted bitmap without the cell's effects, now a favorite |
| Folder | A folder and a file dropped together: the file added, the folder skipped and counted in the message |
| Own tile | A tile dragged and released on its own panel: no frame, nothing added, nothing in the grid |
| Frame | Shown while hovering (both drags), gone on leave, drop, or Escape / capture lost |
| Pasted text | A pasted plain text, ✥ onto the panel: `pasted-….txt`, the cell now named after it |
| Adopted file | The same pasted image dropped twice by ✥: one file only, the cell named `pasted-….png` |
| Un-hearted | The heart of a `favorites-from-pasted/` favorite unchecked: the file in the Recycle Bin; a favorite elsewhere un-hearted: the file untouched |
| Dropped text on the panel | A text dragged from a browser onto the panel: no frame, nothing added |
| Styled file | An `.rtf` and an `.html` dropped into cells: shown with their bold and colors, not their markup; a `.txt` unchanged |
| Pasted styled text | A text copied from Word, ✥ onto the panel: `pasted-….rtf`, reloaded from the favorites with its styles |
| During an export | A file dropped from the Explorer onto the panel while exporting: added |
| Write error | `favorites.txt` read-only: the red status line, the favorite kept for the session |

---

## Open Questions

- [x] ~~A file dropped that is **already a favorite**: moved to the top (as if added again), or left in
  place?~~ → Moved to the top, as if added again
- [x] ~~A drop while a **search** is showing: the search stays (the hearts redrawn), or the panel
  switches to the favorites list?~~ → The search stays, the hearts redrawn
- [x] ~~The **collapsed panel** (the 20 px strip): does it accept the drop too?~~ → Yes, framed while
  hovered; the message goes to the window's status line
- [x] ~~A **tile dragged from the panel** and released on the panel (tiles are not a source): nothing,
  or a favorite anyway? (Today it falls through to the window and lands in the grid.)~~ → Nothing
- [x] ~~A **folder** dropped from the Explorer: ignored, or its files added?~~ → Ignored, the files
  dropped with it added
- [x] ~~Every file type, or only what the cells can show? (The cells show anything, through the Shell
  thumbnail as a last resort.)~~ → Every file type
- [x] ~~The pasted image's **format and name** in `favorites-from-pasted/` (e.g. PNG,
  `pasted-20260930-184512.png`)?~~ → PNG, `pasted-yyyyMMdd-HHmmss.png`, `-2`, `-3`… when taken
- [x] ~~What is saved: the **original pasted bitmap**, or the cell **as drawn** with its effects?~~ →
  The original pasted bitmap, without the cell's effects
- [x] ~~After the save, does the **cell adopt the saved file** (its name shown instead of *Pasted image*,
  *Show in Explorer* available, a second ✥ drop reusing the file instead of saving a duplicate)?~~ →
  Yes, its effects kept
- [x] ~~**Pasted and dropped texts** (no file either): saved too — as the rendered page (PNG) or as the
  text itself (`.txt` / `.rtf`) — or refused with a message?~~ → Saved as the text itself: `.rtf`
  when styled, else `.txt`, read back like a text file, every page *(see the question below: an
  `.rtf` file is read back as plain text today)*
- [x] ~~A favorite from `favorites-from-pasted/` **un-hearted**: its file kept, or deleted?~~ → Deleted,
  to the Recycle Bin
- [x] ~~A **text** (not a file) dropped from another app onto the panel: ignored, or saved like a
  pasted text?~~ → Ignored: no frame, the *none* effect
- [x] ~~A **styled text** saved as `.rtf` is read back **as plain text**: the app opens every text file
  with `TextPages.TryOpen` → `StyledText.Plain`, whatever its extension — the favorite would show the
  RTF markup. Save it as `.txt` (the words kept, the styles lost), teach the loader to read `.rtf` /
  `.html` files with `RtfReader` / `HtmlReader`, or save the styled text as a PNG?~~ → The loader
  reads `.rtf` / `.htm` / `.html` files with `RtfReader` / `HtmlReader`
- [x] ~~A drop onto the panel **during an export**: accepted (the favorites do not touch the grid), or
  refused like the grid's drops?~~ → Accepted

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

### Iteration 2 — 2026-09-30

First batch of open questions answered (Q&A #6–#9): a file already a favorite moves to the top; a
search showing stays; the collapsed panel accepts the drop, its message on the window's status line;
a tile released on its own panel does nothing. § Drop From the Windows Explorer, § Adding a
Favorite and § Feedback updated.

### Iteration 3 — 2026-09-30

Second batch answered (Q&A #10–#13): a folder is ignored; every file type is accepted; the pasted
image is saved as a timestamped PNG; the original pasted bitmap is saved, without the cell's
effects. § Drop From the Windows Explorer, § Images Without a File and § Test Impact updated.

### Iteration 4 — 2026-09-30

Third batch answered (Q&A #14–#17): the cell adopts the saved file; texts are saved as the text
itself (`.rtf` when styled, else `.txt`); a `favorites-from-pasted/` favorite un-hearted is sent to
the Recycle Bin; a text dropped onto the panel is ignored. § Images Without a File and § Test Impact
updated.

Checking the text answer: every text file is opened as plain text (`TextPages.TryOpen` →
`StyledText.Plain`), whatever its extension, so a saved `.rtf` would read back as its markup. New
open question (Q&A #19).

### Iteration 5 — 2026-09-30

Last batch answered (Q&A #18–#19): a drop onto the panel is accepted during an export; the loader
learns to read `.rtf` / `.htm` / `.html` files with `RtfReader` / `HtmlReader`, so a styled text is
saved in the form it arrived in (RTF, else HTML, else plain) and reads back with its styles. New
§ Reading Styled Text Files; § Images Without a File, § Documentation and § Test Impact updated. No
open question left.

### Iteration 6 — 2026-09-30 — ✅ Implemented

Go given: code, tests and documentation (no test project: checked by script, § Test Impact). Scope
frozen on the design sections as they stand. Stays on `main` (the app's standing rule, memory
*Work on main only*).

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
| 6 | A file dropped that is already a favorite: moved to the top, or left in place? | Moved to the top | 2026-09-30 |
| 7 | A drop while a search shows: the search stays, or the panel switches to the favorites? | The search stays | 2026-09-30 |
| 8 | Does the collapsed panel (the strip) accept the drop? | Yes | 2026-09-30 |
| 9 | A tile dragged from the panel and released on it: nothing, or a favorite? | Nothing | 2026-09-30 |
| 10 | A folder dropped from the Explorer: ignored, or its files added? | Ignored | 2026-09-30 |
| 11 | Every file type, or only what the cells can show? | Every file type | 2026-09-30 |
| 12 | The pasted image's format and name? | PNG, timestamped: `pasted-20260930-184512.png` | 2026-09-30 |
| 13 | Saved: the original pasted bitmap, or the cell as drawn? | The original pasted bitmap | 2026-09-30 |
| 14 | After the save, does the cell adopt the saved file? | Yes | 2026-09-30 |
| 15 | Pasted and dropped texts: saved (PNG render, or the text), or refused? | Saved as the text itself: `.rtf` when styled, else `.txt` | 2026-09-30 |
| 16 | A favorite from `favorites-from-pasted/` un-hearted: file kept, or deleted? | Deleted, to the Recycle Bin | 2026-09-30 |
| 17 | A text (not a file) dropped from another app onto the panel? | Ignored | 2026-09-30 |
| 18 | A drop onto the panel during an export: accepted, or refused? | Accepted | 2026-09-30 |
| 19 | A styled text saved as `.rtf` reads back as plain text: `.txt`, an RTF / HTML loader, or PNG? | The loader reads `.rtf` / `.html` files with `RtfReader` / `HtmlReader` | 2026-09-30 |

---

*Last updated: 2026-09-30*
