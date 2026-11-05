# Secondary File Sources

> Working document — extra folders shown as folders of their own in the file explorer's folder
> view, a different color marking another environment, searched and analysed only once opened.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer indexes **one** base folder with its subfolders: its list of files, the OCR passes,
the faces, the visual analysis, all run over it in the background, and every search reads it. The
request: **secondary sources** — other folders on disk, wherever they are — shown in the folder view
as **folders of the base folder's root**, kept apart from everything else:

- searched **only while opened** — never by the search view, nor from the base folder's root;
- their OCR and content analysis run **only on demand**: the user opens the source and clicks 🔄;
- their tile drawn in **another color**, so the user sees they enter another environment;
- a right click on the tile **opens its location** in Windows' Explorer.

Components involved (all under `src/ImageGridFusion/`):

- `UI/AppSettings.cs` — the base folder (`ExplorerFolder`); the list of sources joins it.
- `UI/MainForm.cs` — the ⚙ menu (the base folder's entry calls `_explorer.SetBaseFolder`).
- `Explorer/FileIndex.cs`, `ContentIndex.cs`, `FaceIndex.cs`, `VisionIndex.cs` — one file each in
  `index\` next to the exe, each holding its `BaseFolder`, keyed by paths relative to it.
- `UI/FileExplorerPanel.cs` — one `_index` / `_contents` / `_faces` / `_vision`, one `_scan`,
  `_openFolder` relative to `_baseFolder`; `ListFolderAsync`, `FolderRow`, `Activate`, `Navigate`,
  `SegmentsOf` (breadcrumb), `Rescan`, `StartExtraction` → `Extract(index, contents, faces, vision, …)`.
- `UI/ThumbnailGrid.cs` — `ExplorerRow`, the folder tile's drawing (`PaintFolderGlyph`, its manila colors
  hardcoded; the Windows folder thumbnail otherwise).

---

## Managing the Sources

- A **Secondary sources…** entry of the ⚙ menu, next to the base folder's, with its icon (§ Icons),
  opens a small window listing the sources — each its folder path — with **Add…** (a folder picker)
  and **Remove**.
- Kept in `settings.json` (`AppSettings.SecondarySources`, a list of full paths), written by user
  actions only.
- **Refused**: the base folder itself, a folder inside it, a folder holding it, a source already listed
  or inside / holding one — the window saying why. A source never overlaps the base folder nor another
  source, so a file belongs to one index only.
- **Removed**: its index files are deleted (it was never part of anything else). The folder on disk is
  never touched.

## In the Folder View

- At the **base folder's root**, each source is a **folder tile** — an `ExplorerRow` marked as a source
  (a new field), `FolderRow`'s place in `ListFolderAsync` — listed with the real subfolders
  (*placement: Open Questions*).
- **Its color**: the folder drawn in another color than the manila one (`PaintFolderGlyph` given the
  row's color), the Windows folder thumbnail tinted the same way (*color and reach: Open Questions*).
- **Opened** (double-click, Enter), the folder view browses the source as it browses the base folder:
  its subfolders A→Z, its files in the sort order, read from the disk; the **breadcrumb** reads
  `<base> › <source name> › …`, its source segment going back to the source's root, the base segment
  back to the base folder's root. `_openFolder` can no longer be relative to `_baseFolder` alone: the
  open folder carries **its root** (the base folder or a source) and its path relative to it.
- **A source gone** (an unplugged drive, a deleted folder) keeps its tile, faded, its tooltip saying
  it cannot be found; opening it says so on the status line.
- **Right click**: the tile's menu keeps the explorer's existing **Open in Explorer** entry
  (`_openLocation` → `OpenLocation()`), opening the source's own folder on disk — checked to work on a
  source's full path, outside the base folder.

## The Source's Index

- **Its own files**: each source has its own `files.index`, `files.content`, `files.faces`,
  `files.vision`, in a kebab-case folder of its own under `index\` —
  `index\secondary-sources\<id>\` (§ Folder Names, the names in one `FolderName` constant), `<id>` a
  short hash of the source's full path. The index classes take the folder they read and write instead
  of their fixed `DefaultPath`.
- **Its list of files is read when the source is opened**, then kept in memory and in its
  `files.index` — never at start-up, never in the background while it is closed. Reopened, it shows
  the cached list at once and reads the disk again behind it, as the base folder does at start-up.
- **One holder per source** in `FileExplorerPanel` (its index, its three stores, its scan, its
  cancellation), never the base folder's `_index` / `_contents` / `_faces` / `_vision`: everything
  reading those — the search view, the People view, *Find similar images*, the face grouping, the tile
  tooltip of the base folder's files — never sees a source.

## 🔄 Inside a Source

- While a source (or one of its subfolders) is open, **🔄 acts on that source only**: it reads its
  list of files again, then runs the **content passes** over it — `Extract(…)` called with the source's
  index and stores, as is — each pass under its usual setting (the content search, *Face analysis*,
  *Visual analysis*, *Captions*) and engine (*which passes: Open Questions*). Its own progress bars
  (*while leaving the folder: Open Questions*).
- At the base folder's root or below it, 🔄 is unchanged: the base folder's index, never a source.
- The base folder's automatic runs (start-up, rescan, `StartExtraction`) never start a source's.

## Searching Inside a Source

- With a source open in the folder view, a search looks **only below the open folder of that source**
  (the folder view's rule), reading its index and its content texts — 🔠 and 💡 as for the base folder.
- From the base folder's root, a search never reaches a source; the search view never does.
- The People view never shows a source's faces; *Find similar images* never lists a source's images
  (*from inside a source: Open Questions*).

---

## Test Impact

There is no test project in this repository (`CONTRIBUTING.md` § Build): the planned work is checked
by hand in the running app. No unit test is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

Every question the design cannot settle on its own.

- [ ] **Placement**: the source tiles at the base folder's root — first, before the real subfolders, in
  a list section of their own (*Secondary sources*); or mixed A→Z with the subfolders?
- [ ] **Color**: which one — blue, teal, purple…? And its reach: the source's root tile only, or every
  folder tile inside the source too, and its breadcrumb segment?
- [ ] **Name**: the tile shows the folder's own name, or a name the user gives it in the sources window
  (two sources both named `Photos`)?
- [ ] **Which passes on 🔄 in a source**: every pass its setting turns on — the faces included, though
  they show nowhere outside the source (only in the tile tooltip's 👨‍🦰 line, and only if a source's
  faces are grouped, which the People view would not show) — or the OCR, the visual analysis and the
  captions only, the face pass skipped?
- [ ] **Leaving the source while its analysis runs**: it goes on in the background until done (its bars
  showing wherever the folder view is), or it pauses, picking up where it stopped at the next 🔄?
- [ ] **Find similar images from a file of a source**: lists the similar images of that source only,
  or is it disabled there?
- [ ] **Favorites**: a file of a source hearted — shown with every favorite while the search box is
  empty (favorites are global), or only while the source is open?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design from the request and the scoping answers:

- Added and removed from the ⚙ menu (*Secondary sources…*), kept in `settings.json`.
- Shown as folder tiles of the base folder's root, in another color; a right click opens their location
  (the explorer's existing *Open in Explorer* entry, checked on a path outside the base folder).
- Their list of files read when opened, cached in their own index files under
  `index\secondary-sources\<id>\`; OCR and content analysis on 🔄 inside the source only.
- Kept out of everything outside their open folder: search view, root searches, People view, similar
  images — by a holder of their own, never the base folder's index fields.
- Exploration (three read-only scouts): the index classes assume one root and fixed file names; the
  passes (`Extract`) already take their index and stores as parameters; the folder view's navigation,
  breadcrumb and `FolderTree` assume paths under the base folder — the open folder must carry its root.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | No test project in the repository |
| README | | | |
| GLOSSARY | | | New term *Secondary source*, English and French |
| SHORTCUTS | | | Only if a key or gesture is added |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the user add or remove a secondary source? | From the ⚙ menu: a *Secondary sources…* entry opening a list where folders are added or removed | 2026-11-05 |
| 2 | When is a source's list of files (without OCR) read? | When the folder is opened, then cached; 🔄 runs the OCR and content analysis; nothing at start-up | 2026-11-05 |
| 3 | Outside its open folder, does a source show in the search view, the People view, Find similar images? | Nowhere — entirely apart until its folder is opened | 2026-11-05 |
| 4 | How deep is the subject? | Straightforward — a single scout pass | 2026-11-05 |
| 5 | *(user, unprompted)* | The source's folder has a different color, to show it is another environment | 2026-11-05 |
| 6 | *(user, unprompted)* | A right click on the folder opens its location through a menu item | 2026-11-05 |

---

*Last updated: 2026-11-05*
