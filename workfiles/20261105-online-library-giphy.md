# Online Library — GIPHY

> Working document — search an online media library (GIPHY first) from the file explorer, and bring
> its GIFs into the grid like any file.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer only lists what lies on the disk under the base folder. The user wants to search
**external content libraries** from it too — Tenor first in the original request (2026-10-14), others
later (imgflip maybe).

- **Tenor is out**: Google closed its public API — no new key since 2026-01-13, every integration cut
  on 2026-06-30. **GIPHY** is the first library (Q&A 4), **KLIPY** and imgflip being candidates for
  later ones on the same abstraction.
- **Where it shows**: a **fourth view of the views drop-down** — Folders / People / Favorites /
  GIPHY — rather than a row of source tabs above the panel (Q&A 4): one selector, in line with the
  drop-down chosen on 2026-10-27 (`workfiles/20261027-face-detection.md`, Q&A 6).
- **Its media reach the grid as files**, like the URL zone's (RULES.md § Content From a URL): downloaded
  into the temp folder, then dropped, double-clicked, or dropped onto the Layers or the Grid background.

Components concerned (from the scout pass):

| Concern | Where |
|---|---|
| Views | `Explorer/ExplorerMode.cs` (persisted by name, `AppSettings.ExplorerMode`), `FileExplorerPanel._modes`, `ModeIcons`, `Mode` / `SetMode` / `ChangeMode`, `NextMode` (Ctrl+F cycle), `DrawModeItem` |
| Rows and loads | `ExplorerRow` (`ThumbnailGrid.cs`, keyed on `FullPath`), `FileExplorerPanel._list`, `LoadMore` / `ShowLoaded` — today a slice of a list already complete |
| Thumbnails | `ThumbnailGrid`'s loader (`Work()`), keyed on a path; `FaceCrops.TryParse` is the precedent of a pseudo-path |
| Caption line | `Breadcrumb.SetPath(segments, trailing)` |
| Drag and activation | `FileExplorerPanel.StartDrag` (needs an existing file), `FileActivated` → `MainForm.AddFilesAsync` |
| Download | `Web/Downloader.Client` (shared `HttpClient`, 30 s), `Web/DirectMediaResolver`, `Web/UrlDownloads` |
| Settings | `UI/AppSettings.cs` (a string setting like `ExplorerFolder`), the ⚙ menu (`MainForm._settingsMenu`), `UI/NameDialog` |

---

## Terms

The word **source** is taken: the folder view's *known sources* and *sources found by themselves* are
file origins read from a name (GLOSSARY § Name pattern). The new concept needs its own word — proposed
**online library** (*bibliothèque en ligne*): a web service searched from the file explorer, GIPHY the
first. To confirm (Open Questions).

---

## The GIPHY View

- A value `Giphy` of `ExplorerMode`, its entry in the views drop-down after its icon (RULES.md § Icons),
  saved by name like the others.
- The search box queries GIPHY instead of the index; the **🔠 / 💡 criteria**, the sort order, the
  sections buttons and the breadcrumb's folders do not apply there (disabled or hidden — Open
  Questions).
- The breadcrumb reads `GIPHY` then the query, its trailing text what the list holds.
- **Attribution**: GIPHY's API terms (section 5A) require its **Powered By GIPHY** mark, visible wherever
  its content shows. Where it sits is an Open Question.
- **Without a key**, the view shows an invitation to set one, a link to GIPHY's developer site and the
  ⚙ menu's entry.

### GIFs and Stickers

- A **GIFs / Stickers** toggle beside the search box chooses what the view searches (Q&A 7): GIPHY's
  GIFs, or its **stickers** — transparent, made to lie over something, so the natural content of a
  layer. One kind at a time: a load stays one call.

### Requests

- `GET /v1/gifs/search` or `/v1/stickers/search` (`q`, `limit`, `offset`, `rating`, `lang`) through
  `Downloader.Client`, parsed with `JsonDocument` as `XPostResolver` does.
- **The empty box shows the trending** (Q&A 10): `/v1/gifs/trending` or `/v1/stickers/trending`, one
  call when the view opens or the box is emptied — as File System shows the favorites.
- **Budget**: a beta key allows **100 calls an hour**. A load is one call; nothing is fetched in the
  background, nothing per tile. A refused call (429) says so in the list's caption, the list kept.
- A newer query cancels the one running.

### Loads

- The view's list is **fetched page by page**: `LoadMore` asks the library for the next page while it
  says more remain, instead of slicing `_list`. The **Loading…** tile shows while it runs.
- Its page size — Open Question.

### Thumbnails

- A GIPHY tile's thumbnail is one of the media's small renditions, fetched from GIPHY's CDN (media
  requests do not count against the key's budget), kept in memory for the session.
- The loader learns a **pseudo-path** for it, as `FaceCrops` did: no file on the disk until the media
  is used.
- **The thumbnails play** (Q&A 11): every GIPHY tile in view plays its animation while the results show,
  not only once clicked — a small animated rendition (a few hundred KB at most), its frames decoded
  once, the tiles scrolled out of view not drawn. `ThumbnailGrid` has no animated tile today: it gets
  one, for this view only.

### Reaching the Grid

- A GIPHY tile is **downloaded before it is used**: into the temp folder (kebab-case folder, its name in
  one `FolderName` constant — `CLAUDE.md` § Folder Names), the same media reused, the folder emptied at
  start-up by the normal instance only, as `UrlDownloads` is.
- **A click downloads, then the tile can be dragged** (Q&A 9): a click on a GIPHY tile starts its
  download, a gauge on the tile while it runs; once the file is there the tile is marked ready, and a
  drag from it carries `DataFormats.FileDrop` as a file tile's does. A drag started from a tile not
  downloaded yet does nothing but start the download. A **double-click** downloads if needed, then calls
  `AddFilesAsync` — a cell, as a file tile's double-click does.
- From then on it goes the file's way — a cell, a layer (a drop on the Layers tab), the Grid background
  alike, with no route of its own.
- **The rendition downloaded** (Q&A 8): a GIF's **MP4** — light and sharp, the app plays videos; a
  sticker's **GIF** — the only rendition keeping its transparency.

### API Key

- A string app setting, `AppSettings.GiphyApiKey`, in `settings.json` next to the exe (Q&A 6) — never in
  the repository.
- Set from a **⚙ menu** entry, *GIPHY API key…*, through `NameDialog` (its value shown in clear — Open
  Question), and from the view's invitation.
- Out of the undo history.

---

## Test Impact

The app has **no unit test project** (`src/` holds `ImageGridFusion` only). Whether one is created for
this work is an Open Question; until then, the behaviours are checked by hand:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A GIPHY search answer parsed into rows (id, title, renditions, total count) | — (by hand, or a test project — Open Questions) | — |
| The next page asked with the right offset, none once the total is reached | — | — |
| A 429 / an invalid key reported in the caption, the list kept | — | — |
| `ExplorerMode.Giphy` saved and read back by name | — | — |

---

## Open Questions

- [ ] **Term**: *online library* (*bibliothèque en ligne*) for the new concept, the word *source* being
  taken?
- [x] ~~**Empty search box**: GIPHY's trending GIFs (one call), or nothing until something is typed?~~ →
  The trending, of the kind chosen (Q&A 10)
- [x] ~~**GIFs and stickers**: GIFs only, or the stickers too (transparent — the backlog's *Stickers in
  layers*), and how chosen (a toggle, sections)?~~ → Both, a GIFs / Stickers toggle (Q&A 7)
- [x] ~~**Downloaded rendition**: MP4 (light, sharp, no transparency) or GIF (heavier, 256 colours,
  transparency for the stickers)?~~ → MP4 for a GIF, GIF for a sticker (Q&A 8)
- [x] ~~**When the download happens**: at the drag's start (the drag waits for it, a gauge on the tile), or
  ahead (on hover / selection), or a first click then a drag?~~ → A click downloads, then the tile can be
  dragged; a double-click downloads and adds (Q&A 9)
- [x] ~~**Thumbnails**: still, or animated in the list?~~ → Animated, playing while the results show
  (Q&A 11)
- [ ] **Page size**: how many results a load asks for (GIPHY's beta allows up to 50 a call)?
- [ ] **Rating**: which content rating the requests carry (`g`, `pg`, `pg-13`, `r`), fixed or a setting?
- [ ] **Attribution placement**: the Powered By GIPHY mark next to the search box, on the breadcrumb line,
  or under the list?
- [ ] **Ctrl+F cycle**: does it go through the GIPHY view (skipped without a key, as People is without the
  face analysis)?
- [ ] **Search criteria and sort**: hidden or disabled in the GIPHY view?
- [ ] **Hearting a GIPHY tile**: no heart, or downloaded into a folder next to the exe and made a
  favorite (as the pasted favorites)?
- [ ] **API key dialog**: the key shown in clear in `NameDialog`, or masked?
- [ ] **Unit tests**: create a test project for the GIPHY parsing and paging, or check by hand as the
  previous workfiles did?
- [ ] **Backlog**: the *Stickers in layers* row of `TODO-FEATURES.md` names GIPHY stickers — mark the part
  this workfile covers, split the row, or leave it?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Scoping settled with the user: a fourth view of the views drop-down, GIPHY first (Tenor's API being
closed), the API key in `settings.json` from the ⚙ menu, the subject tricky / long. A scout pass of three
read-only agents mapped the views, the loads, the thumbnails, the drag route, the downloads, the settings
and the HTTP code (§ Overview). Two points were checked against later work: the views drop-down chosen on
2026-10-27 replaces the tabs of the original request, and the word *source* is taken by the name
patterns. Every design choice left is listed in § Open Questions.

### Iteration 2 — 2026-11-05

First batch of open questions answered (Q&A 7–10): a GIFs / Stickers toggle, one kind a load; the
trending shown in the empty box; a GIF downloaded as its MP4, a sticker as its GIF; a click downloads a
tile, which can then be dragged, a double-click downloading and adding it. § The GIPHY View updated.

### Iteration 3 — 2026-11-05

The user, before the question was asked: the GIF animations play in the list during the search, not
only once a tile is clicked (Q&A 11). § Thumbnails: animated tiles, a new capability of `ThumbnailGrid`.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Scoping batch: depth, key storage, downloaded format, Tenor tab extras | Dismissed — "maybe not Tenor: Klipy? Giphy? Which is best?" → Tenor's API closed; GIPHY recommended, Klipy second | 2026-10-14 |
| 2 | Scoping batch again, with the library choice | Dismissed | 2026-10-14 |
| 3 | (User) Do recent developments go against choices made here? | The views drop-down (2026-10-27) and the word *source* (2026-11-04) touch the request; nothing else contradicts it | 2026-11-04 |
| 4 | Where the library lives (source tabs or a view of the drop-down), which library | A view of the views drop-down; GIPHY — with an explanation of the Powered By GIPHY attribution | 2026-11-05 |
| 5 | Depth of the subject | Tricky / long | 2026-11-05 |
| 6 | Where the API key lives | ⚙ menu → `settings.json` | 2026-11-05 |
| 7 | GIFs only, or the stickers too? | Both, a GIFs / Stickers toggle | 2026-11-05 |
| 8 | Downloaded rendition: MP4 or GIF? | MP4 for a GIF, GIF for a sticker | 2026-11-05 |
| 9 | When the download happens | A click downloads, then the drag is possible | 2026-11-05 |
| 10 | The empty search box: trending or nothing? | The trending | 2026-11-05 |
| 11 | Thumbnails still or animated? | (User, before it was asked) The GIF animations play during the search, not only on a click | 2026-11-05 |
| 12 | Page size of a load | | 2026-11-05 |
| 13 | Content rating of the requests | | 2026-11-05 |
| 14 | Where the Powered By GIPHY mark sits | | 2026-11-05 |
| 15 | Does the Ctrl+F cycle go through the GIPHY view? | | 2026-11-05 |

---

*Last updated: 2026-11-05*
