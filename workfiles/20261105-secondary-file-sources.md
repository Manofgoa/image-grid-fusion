# Secondary File Sources

> Working document — extra folders shown as folders of their own in the file explorer's folder
> view, drawn in blue to mark another environment, searched and analysed only once opened.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer indexes **one** base folder with its subfolders: its list of files, the OCR passes,
the faces, the visual analysis, all run over it in the background, and every search reads it. The
request: **secondary sources** — other folders on disk, wherever they are — shown in the folder view
as **folders of the base folder's root**, kept apart from the rest:

- searched **only while opened** — never by the search view, nor from the base folder's root;
- their OCR and content analysis run **only on demand**: the user opens the source and clicks 🔄;
- drawn in **blue**, so the user sees they enter another environment;
- a right click on the tile **opens its location** in Windows' Explorer;
- two exceptions to *apart*, once analysed: their **faces** join the People view, their images are
  among the results of **Find similar images**.

Components involved (all under `src/ImageGridFusion/`):

- `UI/AppSettings.cs` — the base folder (`ExplorerFolder`); the list of sources joins it.
- `UI/MainForm.cs` — the ⚙ menu (the base folder's entry calls `_explorer.SetBaseFolder`).
- `Explorer/FileIndex.cs`, `ContentIndex.cs`, `FaceIndex.cs`, `VisionIndex.cs` — one file each in
  `index\` next to the exe, each holding its `BaseFolder`, keyed by paths relative to it.
- `UI/FileExplorerPanel.cs` — one `_index` / `_contents` / `_faces` / `_vision`, one `_scan`,
  `_openFolder` relative to `_baseFolder`; `ListFolderAsync`, `FolderRow`, `FolderSections`, `Activate`,
  `Navigate`, `SegmentsOf` (breadcrumb), `Rescan`, `StartExtraction` → `Extract(index, contents, faces,
  vision, …)`, `Regroup` (the face grouping); `UI/FileExplorerPanel.Vision.cs` — `SimilarRows`.
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
- **Removed**: its index files are deleted, its faces leave the People view and its images the similar
  images. The folder on disk is never touched; a person's pinned faces keep their copy of the embedding
  (§ People), as when a file moves.

## In the Folder View

- At the **base folder's root**, the sources come **first**, before the real subfolders, in a list
  section of their own, **Secondary sources** (`FolderSections`) — each a **folder tile**, an
  `ExplorerRow` marked as a source (a new field), added in `ListFolderAsync` at the root only.
- **Its name**: the folder's own name; its full path in the tooltip.
- **Blue**: the source's tile, **every folder tile inside the source**, and the source's **breadcrumb
  segments** are drawn in blue instead of the manila color — `PaintFolderGlyph` given the row's color,
  the Windows folder thumbnail tinted the same way — so the user always sees they are in another
  environment.
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
  cancellation), never the base folder's `_index` / `_contents` / `_faces` / `_vision`: the search view
  and the root searches never see a source. Only the People view's grouping and *Find similar images*
  read the sources' stores too — below.
- **Loaded at start-up for those two only**: each source's `files.faces` and `files.vision` (with its
  `files.index`, to turn its keys into full paths) are read from the disk, never the folder itself, so a
  source analysed before is in the People view and the similar images at once.

## 🔄 Inside a Source

- While a source (or one of its subfolders) is open, **🔄 acts on that source only**: it reads its
  list of files again, then runs the **content passes** over it — `Extract(…)` called with the source's
  index and stores, as is — each pass under its usual setting (the content search, *Face analysis*,
  *Visual analysis*, *Captions*) and engine, **the face pass included**. Its own progress bars, in the
  explorer's usual place.
- **One worker at a time**: while a source's run goes, the base folder's passes wait, and pick up
  where they were once it ends or pauses.
- **Leaving the source pauses its run** — going up to the base folder, opening another source,
  another view. The work done is kept (a pass done is never redone, § Content OCR Passes); the next 🔄
  in that source picks up where it stopped.
- At the base folder's root or below it, 🔄 is unchanged: the base folder's index, never a source.
- The base folder's automatic runs (start-up, rescan, `StartExtraction`) never start a source's.

## Searching Inside a Source

- With a source open in the folder view, a search looks **only below the open folder of that source**
  (the folder view's rule), reading its index and its content texts — 🔠 and 💡 as for the base folder.
- From the base folder's root, a search never reaches a source; the search view never does — a
  person's name searched under 🔠 included (`SearchPeople` lists the base folder's files only).

## Faces in the People View

- Once a source's face pass has run, **its faces join the People view**, grouped with the base
  folder's (`Regroup` gathers every store's faces, each turned into a full path by its own index, before
  `FaceGrouping.Compute`): a person found in both shows the files of both when opened, a source's files
  among them as ordinary file tiles.
- The decisions (`people.json`) are keyed by full path already: they apply to a source's faces as to the
  others.
- A source's file in an open group follows the explorer's rules for a file: its menu, its *People*
  submenu, its tooltip's 👨‍🦰 line read from the store holding it.

## Similar Images Across the Sources

- **Find similar images** — from a tile of the base folder or of a source, from a cell, or an image
  dropped onto the search box — lists the images standing out **among the base folder and every
  analysed source** together (`SearchSimilar` given every vision store's entries; the spread computed
  over them all), the source's images among the results as ordinary file tiles.
- The **similarity tier** of a 💡 search stays within the search's scope: the base folder's, or the open
  source's while one is open.

## Favorites

- A file of a source hearted is an ordinary favorite: shown with every favorite while the search box is
  empty, wherever the folder view stands; its full path in `favorites.json` like any other.

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

- [x] ~~**Placement**: the source tiles at the base folder's root — first, in a section of their own, or
  mixed A→Z?~~ → First, before the real subfolders, in a list section *Secondary sources*.
- [x] ~~**Color**: which one, and its reach?~~ → Blue — the source's tile, every folder inside it, its
  breadcrumb segments.
- [x] ~~**Name**: the folder's own name, or a name the user gives it?~~ → The folder's name, its full path
  in the tooltip.
- [x] ~~**Which passes on 🔄 in a source**: every pass, or the face pass skipped?~~ → Every pass its
  setting turns on, the faces included — and the faces join the People view (#14).
- [x] ~~**Leaving the source while its analysis runs**: goes on, or pauses?~~ → Pauses, picking up where
  it stopped at the next 🔄 in that source.
- [x] ~~**Find similar images from a file of a source**: that source only, or disabled?~~ → Across every
  source: the base folder and every analysed source, whatever the image it starts from.
- [x] ~~**Favorites**: with every favorite, or only while the source is open?~~ → With every favorite.
- [x] ~~**"Toutes sources" confirmed?** Find similar images across the base folder and every analysed
  source, whatever the image it starts from — a tile of the base folder included?~~ → Yes, everywhere.

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

### Iteration 2 — 2026-11-05

The open questions answered:

- Placement: a *Secondary sources* list section first at the root. Color: blue, inside the whole
  source and its breadcrumb segments. Name: the folder's own.
- **Faces**: analysed by 🔄 like the other passes, and **joining the People view** — an exception to
  Iteration 1's *apart*: the grouping gathers the base folder's and every source's faces. Their stores
  are loaded at start-up for it (and for the similar images), never the folders themselves.
- **Leaving a source pauses its run**, resumed by the next 🔄 there; one worker at a time, the base
  folder's passes waiting meanwhile.
- **Find similar images spans every source** — the base folder and every analysed source, whatever the
  image it starts from — the second exception to *apart*. The 💡 similarity tier stays within the
  search's scope. *(Interpretation of the answer "Toutes sources", confirmed by #16.)*
- Favorites: a source's file hearted is an ordinary favorite.

### Iteration 3 — 2026-11-05

The user asks for a source's index in files of its own, apart from the main index's — in subfolders of
the main `index\` folder for instance. Already the design (§ The Source's Index:
`index\secondary-sources\<id>\`, the four files each); stated as a requirement now, nothing changed.

### Iteration 4 — 2026-11-05 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree on `feature/secondary-file-sources`.
The scope is frozen as the sections above stand.

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
| 3 | Outside its open folder, does a source show in the search view, the People view, Find similar images? | Nowhere — entirely apart until its folder is opened *(revised by #14 and #12)* | 2026-11-05 |
| 4 | How deep is the subject? | Straightforward — a single scout pass | 2026-11-05 |
| 5 | *(user, unprompted)* | The source's folder has a different color, to show it is another environment | 2026-11-05 |
| 6 | *(user, unprompted)* | A right click on the folder opens its location through a menu item | 2026-11-05 |
| 7 | Placement of the source tiles at the root? | First, before the real subfolders, in a list section of their own, *Secondary sources* | 2026-11-05 |
| 8 | Color of a source, and its reach? | Blue — the source's tile, every folder inside it, its breadcrumb segment | 2026-11-05 |
| 9 | Name shown on the source's tile? | The folder's own name, its full path in the tooltip | 2026-11-05 |
| 10 | Which passes 🔄 runs in a source (the face pass skipped, the People view never showing a source)? | Not understood why the faces would not be used — explanation given, asked again (#14) | 2026-11-05 |
| 11 | Leaving the source while its analysis runs? | Pause — resumed where it stopped at the next 🔄 | 2026-11-05 |
| 12 | Find similar images from a file of a source? | "Toutes sources" — read as: across the base folder and every analysed source | 2026-11-05 |
| 13 | A hearted file of a source among the favorites? | With every favorite | 2026-11-05 |
| 14 | A source's faces: where would they be used? | In the People view — after a 🔄, they join its groups like the base folder's | 2026-11-05 |
| 15 | *(user, unprompted)* | A source's index lives in files other than the main index's — in subfolders of the main `index\` folder, for example | 2026-11-05 |
| 16 | Does *Find similar images* span the base folder and every analysed source, whatever the image it starts from? | Yes, everywhere — from any tile, cell or dropped image | 2026-11-05 |

---

*Last updated: 2026-11-05*
