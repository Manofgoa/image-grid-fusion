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
  source, so a file belongs to one index only: a **base folder** chosen in the ⚙ menu that would overlap
  a source is refused too, the message saying to remove the source first.
- Every change of the window is **saved and applied at once** (`SourcesChanged`); the window keeps nothing.
- **Removed** — after a confirmation naming what is deleted —: its index files are deleted once its scan
  and passes are over, its faces leave the People view and its images the similar
  images. The folder on disk is never touched; a person's pinned faces keep their copy of the embedding
  (§ People), as when a file moves.

## In the Folder View

- At the **base folder's root**, the sources come **first**, before the real subfolders, in a list
  section of their own, **Secondary sources** — each a **folder tile**, an `ExplorerRow` marked
  `InSource`, added in `ListFolderAsync` at the root only, each looked for on the disk on the same worker.
  With the folder groups off, they still come first, without a section.
- **Its name**: the folder's own name; its full path in the tooltip.
- **Blue**: the **source's own tile** and its **name in the breadcrumb** are drawn in blue instead of
  the manila color — `PaintFolderGlyph` given the row's flag, the Windows folder thumbnail turned into
  shades of blue by its brightness (a `ColorMatrix`, the files previewed on it blue too), the
  breadcrumb's source segment (`Breadcrumb.SetPath`'s `sourceAt`) — so the user sees they enter another
  environment. The folders inside a source keep the usual manila color *(Iteration 6)*.
- **Opened** (double-click, Enter), the folder view browses the source as it browses the base folder:
  its subfolders A→Z, its files in the sort order, read from the disk; the **breadcrumb** reads
  `<base> │ <source name> › …` — a **clear separator** `│` between the base folder's name and the
  source's, never the child's `›` *(Iteration 7, replacing Iteration 6's ←)* — the base folder's name
  going back to its root, the source's name to the source's root; ↑ at the source's root leaves it, its
  tile selected. The open folder
  is `_openSource` (null: the base folder) plus `_openFolder`, relative to it; the folder view reads
  through `ViewSource` / `ViewRoot` / `ViewIndex` / `ViewContents` / `ViewTree`.
- **Not remembered**: `OpenFolder` gives "" while a source is open, so the next start opens at the base
  folder's root, where the source's tile is.
- **A source gone** (an unplugged drive, a deleted folder) keeps its tile, faded, its tooltip saying
  it cannot be found; opening it says so on the status line.
- **Right click**: the tile's menu keeps the explorer's existing **Open in Explorer** entry
  (`_openLocation` → `OpenLocation()`), opening the source's own folder on disk — checked to work on a
  source's full path, outside the base folder.

## Source Colors

*(Iteration 8.)* Each source has a **color of its own**, where the first iteration drew them all in blue:

- **The palette** (`UI/SourceColors`): blue — the first source's, as before —, violet, teal, green, plum; never a
  red, an orange or a yellow, which read as an error or a warning. Past five sources, the least held color again.
- **Given when the source is added**, the palette's first color no source holds (`SecondarySources.Colored`), and
  **kept in the settings** with its folder — `SecondarySources` becomes a list of `{ "folder", "color" }`, an older
  list of bare paths read with its colors given in order. Removing a source never recolors the others.
- **What takes it**: the source's tile — the drawn folder, Windows' thumbnail tinted (`SourceColor.Tint`), its file
  count —, its name in the breadcrumb, and the **status line while the source is open** (`FileExplorerPanel.Show`),
  an error staying red. The folders inside it keep the manila color.
- **The status line inside a source** gives the source's own index (`4,251 files · indexed …`), then its own
  run's activity only: before, it gave the base folder's summary beside the source's OCR progress.

## The Source's Index

- **Its own files**: each source has its own `files.index`, `files.content`, `files.faces`,
  `files.vision`, in a kebab-case folder of its own under `index\` —
  `index\secondary-sources\<id>\` (§ Folder Names, the names in one `FolderName` constant), `<id>` the
  first 16 hex digits of the SHA-256 of the source's full path, lower-cased — a source moved or renamed is
  a new one. `IndexFiles` gives the four paths of a folder (`IndexFiles.Main`, `IndexFiles.Of`); the
  passes save into the one they are given.
- **Its list of files is read when the source is opened**, then kept in memory and in its
  `files.index` — never at start-up, never in the background while it is closed. Reopened, it shows
  the cached list at once and reads the disk again behind it, as the base folder does at start-up.
- **One holder per source** in `FileExplorerPanel` (its index, its three stores, its scan, its
  cancellation), never the base folder's `_index` / `_contents` / `_faces` / `_vision`: the search view
  and the root searches never see a source. Only the People view's grouping and *Find similar images*
  read the sources' stores too — below.
- **Loaded at start-up for those two only**: each source's `files.faces` and `files.vision` (with its
  `files.index`, to turn its keys into full paths) are read from the disk, never the folder itself, so a
  source analysed before is in the People view and the similar images at once. Its **content texts**
  are loaded when it is opened: until then, a source's file shown in the People view has no caption,
  tags nor text in its tooltip.

## 🔄 Inside a Source

- While a source (or one of its subfolders) is open, **🔄 acts on that source only**: it reads its
  list of files again, then runs the **content passes** over it — `Extract(…)` called with the source's
  index and stores, as is — each pass under its usual setting (the content search, *Face analysis*,
  *Visual analysis*, *Captions*) and engine, **the face pass included**. Its own progress bars, in the
  explorer's usual place.
- **One worker at a time**: while a source's run goes, the base folder's passes wait, and pick up
  where they were once it ends or pauses.
- **Leaving the source pauses its run** — going up to the base folder, opening another source,
  another view — and so does anything that stops the passes: a ⚙ analysis setting changed, an engine
  installed or updated, *Forget faces…*, *Forget visual analysis…*. The work done is kept (a pass done is
  never redone, § Content OCR Passes); the next 🔄 in that source picks up where it stopped.
- **Nothing turned on** in the ⚙ menu: 🔄 reads the source's list again and the status line says no
  analysis is on.
- Its tooltip says what it does there. The status line keeps the base folder's summary; the source's
  counts are at the end of the breadcrumb.
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
- **Forget faces…** forgets the sources' faces too — their `files.faces` deleted — since they are in the
  People view; **Forget visual analysis…** the sources' embeddings, tags and captions likewise.
- A search inside an open group reads each file in its own index's texts, the base folder's files then
  each source's.
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

### Iteration 5 — 2026-11-05 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state, now in the sections above:

- **A base folder overlapping a source is refused** in the ⚙ menu's picker — the design only refused the
  other way round; without it, a file could belong to two indexes.
- **Removing a source asks a confirmation** naming what is deleted; its index is deleted once its scan and
  passes are over. Every change of the window is saved and applied at once.
- **Forget faces… and Forget visual analysis… cover the sources too** — their faces and images are in the
  People view and the similar images, so forgetting the base folder's alone would leave them showing.
- **Anything that stops the passes pauses a source's run** — a ⚙ analysis setting, an engine swap, a
  Forget… — like leaving it; 🔄 with no analysis on reads the list again and says so.
- **The open source is not remembered**: the next start opens at the base folder's root.
- **The source tiles come first even with the folder groups off**, without a section then.
- **Windows' folder thumbnail is turned into shades of blue** by a color matrix, the files previewed on it
  blue too; the drawn folder takes two blues; a source not found is faded.
- **A source's content texts load when it is opened**, not at start-up: until then, its files' tooltips in
  the People view show their people only.
- **The status line keeps the base folder's summary** inside a source; the source's counts are on the
  breadcrumb.
- **A search inside an open People group** reads each file's own index, the base folder's matches then each
  source's, rather than one ranking across them.
- **The source's id** is the first 16 hex digits of the SHA-256 of its lower-cased full path.
- A **Delete key** on the window's list, written then removed: no shortcut outside the design
  (RULES.md § Keyboard Shortcuts would have needed `SHORTCUTS.md`).
- Docs: besides the README and the glossary, a **§ Secondary Sources** in `RULES.md`, as every feature of
  the app has.

### Iteration 6 — 2026-11-05 — ⚙️ Post-implementation — Blue root only, back arrow in the breadcrumb

The user, after a first look:

- **Only the source's own tile is blue**: the folders inside a source keep the usual manila color.
- **The breadcrumb inside a source no longer starts with the base folder's name**: a **←** stands in its
  place, going back to the base folder's root, then the source's name and the folders down to the open one.
- Agent's reading, by the same rule: in the breadcrumb, only the source's name is blue.

### Iteration 7 — 2026-11-05 — ⚙️ Post-implementation — A clear separator rather than the back arrow

The user's alternative, then chosen over Iteration 6's ←: the breadcrumb inside a source **keeps the base
folder's name**, followed by a **clear separator** — `│` — rather than `›`, which would show a child; then
the source's name, blue, and its folders separated by `›`. The blue root tile only (Iteration 6) stands.

### Iteration 8 — 2026-11-05 — ⚙️ Post-implementation — A color per source

The user, seeing the status line of a source give the base folder's file count beside the source's OCR progress:
the line must turn blue to show a secondary source — and further, **each source has its color**, from a palette,
the first blue, none reading as an error; its folder and its messages in that color. Choices asked: the five colors
above, **fixed when the source is added**, on its tile, its breadcrumb name and the status line — § Source Colors.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 5, 8 | 2026-11-05 | Settings and index folders, blue tiles and breadcrumb, the explorer's sources, the ⚙ window; a color per source, the status line's own summary |
| Unit tests | 8 | 2026-11-05 | `SecondarySourceColorsTests`: the colors given, kept, wrapped |
| README | 5, 8 | 2026-11-05 | § File explorer, English and French |
| GLOSSARY | 5, 8 | 2026-11-05 | *Secondary source*, English and French |
| RULES | 5, 8 | 2026-11-05 | § Secondary Sources |
| SHORTCUTS | — | — | No key or gesture added |

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
| 17 | *(user, after delivery)* | The subfolders of a source need not be blue, only its root; the breadcrumb does not start with the base folder inside a source — a left arrow back to the root instead | 2026-11-05 |
| 18 | Breadcrumb inside a source: the base folder's name with a clear separator, or the ← alone? | The base folder's name, then a clear separator `│`, then the source | 2026-11-05 |

---

*Last updated: 2026-11-05*
