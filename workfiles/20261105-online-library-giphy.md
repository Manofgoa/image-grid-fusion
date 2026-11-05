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
file origins read from a name (GLOSSARY § Name pattern). The new concept takes its own word, **online
library** (*bibliothèque en ligne*, Q&A 19): a web service searched from the file explorer, GIPHY the
first. The code and the glossary use it; a later library (KLIPY, imgflip) is another online library on
the same abstraction.

---

## The GIPHY View

- A value `Giphy` of `ExplorerMode`, its entry in the views drop-down after its icon (RULES.md § Icons),
  saved by name like the others.
- The search box queries GIPHY instead of the index; the **🔠 / 💡 criteria**, the sort order and the
  sections buttons do not apply there: they are **hidden** in the GIPHY view (Q&A 20), the room freed
  taking the GIFs / Stickers toggle, and come back in the other views.
- The breadcrumb reads `GIPHY` then the query — `Trending` for the empty box — its trailing text what the
  list holds (`50 GIFs+` while GIPHY holds more), `Enter to search` while a text typed waits, `Searching…`
  during a call. **↑**, its `GIPHY` segment, Backspace and Alt+↑ go back to the trending (the box emptied).
- A **GIPHY tile has no context menu**: no file to copy or show until it is fetched.
- **Ctrl+F** cycles through it, last — Folders → People → Favorites → GIPHY — **skipped while no key is
  set**, as People is while the face analysis is off (`NextMode`, Q&A 15); `SHORTCUTS.md` and its French
  version updated with it.
- **Content rating**: an app setting of the ⚙ menu — `g`, `pg`, `pg-13`, `r` — **`r` by default**
  (`AppSettings.GiphyRating`, Q&A 13), carried by every request.
- **Attribution**: GIPHY's API terms (section 5A) require its **Powered By GIPHY** mark, visible wherever
  its content shows. It sits **at the right end of the breadcrumb line** of the GIPHY view (Q&A 17),
  always visible, taking no room of its own — the words **Powered by GIPHY** in bold, GIPHY's logo file not
  shipped.
- **Without a key**, the view shows an invitation to set one, a link to GIPHY's developer site and a 🔑
  *GIPHY API key…* button opening the same dialog as the ⚙ menu's entry.
- Its icons: 🌍 the view, 🎞️ GIFs, 😎 Stickers, 🔑 the key, 🔞 the content rating (`UiIcons`).
- The search asks GIPHY in the UI language (`lang`).

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
  background, nothing per tile. A refused call — 401 / 403 an invalid key, 429 the hour used up — is said
  on the status line, the list kept; a refused next page ends the list as it is.
- A newer query cancels the one running.
- **A search starts on Enter only** (Q&A 18): typing costs nothing; the same query pressed again is not
  asked twice. The box emptied brings the trending back (one call, kept for the session). The view opened
  with a text in the box shows the trending, the text waiting for Enter.
- A change of the content rating asks the list shown again with it (one call).

### Loads

- The view's list is **fetched page by page**: `LoadMore` asks the library for the next page while it
  says more remain, instead of slicing `_list`. The **Loading…** tile shows while it runs.
- **50 results a call** (Q&A 16), GIPHY's largest page: a load — the pages per load setting times a page
  — may take less than one call, the rest kept for the next load before another call is made.

### Thumbnails

- A GIPHY tile's thumbnail is its `fixed_width_downsampled` rendition — a GIF 200 px wide, fewer frames
  — else `fixed_width`, else `preview_gif`, fetched from GIPHY's CDN (media requests do not count against
  the key's budget) four at a time, by `UI/OnlineThumbnails`, the 200 last shown kept; a list replaced
  forgets them.
- The tile's row carries the result (`ExplorerRow.Online`), its `FullPath` a key (`giphy:gifs:<id>`) never
  handed to the Shell nor the disk: the grid asks its picture of `ThumbnailGrid.OnlineFrame`, not of the
  Shell's thumbnail cache.
- **The thumbnails play** (Q&A 11): every GIPHY tile in view plays its animation while the results show,
  not only once clicked. Each GIF is **kept whole, compressed**, a frame decoded when shown — the frame
  of the clock's moment, its delays read from the file — and the tiles in view are drawn again every
  40 ms while the view shows and the window is not minimized (`ThumbnailGrid.InvalidateOnline`); the tiles
  scrolled out of view are not drawn.

### Reaching the Grid

- A GIPHY tile is **downloaded before it is used**: into the temp folder (kebab-case folder, its name in
  one `FolderName` constant — `CLAUDE.md` § Folder Names), the same media reused, the folder emptied at
  start-up by the normal instance only, as `UrlDownloads` is.
- **A click downloads, then the tile can be dragged** (Q&A 9): a click on a GIPHY tile starts its
  download (`ThumbnailGrid.RowPressed`), a gauge along the tile's bottom edge while it runs; once the file
  is there the tile shows a ✓ in a dark label at its bottom left, and a drag from it carries
  `DataFormats.FileDrop` as a file tile's does. The file is `<title>-giphy-<id>.mp4` or `.gif` in
  `%TEMP%\ImageGridFusion\giphy-downloads\` (`GiphyFiles`). A drag started from a tile not
  downloaded yet does nothing but start the download. A **double-click** downloads if needed, then calls
  `AddFilesAsync` — a cell, as a file tile's double-click does.
- From then on it goes the file's way — a cell, a layer (a drop on the Layers tab), the Grid background
  alike, with no route of its own.
- **The rendition downloaded** (Q&A 8): a GIF's **MP4** — light and sharp, the app plays videos; a
  sticker's **GIF** — the only rendition keeping its transparency. A GIF without an MP4 rendition arrives as
  its GIF.

### Favorites

- A GIPHY tile has its **heart** (Q&A 21): hearting it downloads the media — the same rendition — into
  **`giphy-favorites\` next to the exe** (its name in one `FolderName` constant), and makes that file an
  ordinary favorite, shown in the Favorites view like any other, as a pasted favorite is
  (`favorites-from-pasted\`). Un-hearted, its file follows the pasted favorites' rule: to the Recycle
  Bin once no effect preset holds it.
- A GIPHY tile already a favorite shows its heart filled, matched by the GIPHY id kept in the file's
  name. A result downloaded already is copied from the temp folder rather than downloaded again.

### API Key

- A string app setting, `AppSettings.GiphyApiKey`, in `settings.json` next to the exe (Q&A 6) — never in
  the repository.
- Set from a **⚙ menu** entry, *GIPHY API key…*, through `NameDialog`, its value **shown in clear**
  (Q&A 22) — it lies in clear in `settings.json` anyway — and from the view's invitation. An empty key
  forgets it.
- Out of the undo history.

---

## Test Impact

The app has **no unit test project** (`src/` holds `ImageGridFusion` only), and none is created for
this work (Q&A 23): the behaviours below are checked by hand in the app launched.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A GIPHY search answer parsed into rows (id, title, renditions, total count) | — (by hand) | — |
| The next page asked with the right offset, none once the total is reached | — | — |
| A 429 / an invalid key reported on the status line, the list kept | — | — |
| `ExplorerMode.Giphy` saved and read back by name | — | — |

---

## Open Questions

- [x] ~~**Term**: *online library* (*bibliothèque en ligne*) for the new concept, the word *source* being
  taken?~~ → *Online library* (Q&A 19)
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
- [x] ~~**Page size**: how many results a load asks for (GIPHY's beta allows up to 50 a call)?~~ → 50 a
  call (Q&A 16)
- [x] ~~**Rating**: which content rating the requests carry (`g`, `pg`, `pg-13`, `r`), fixed or a setting?~~
  → An app setting of the ⚙ menu, `r` by default (Q&A 13)
- [x] ~~**Search trigger**: the GIPHY view searches on Enter only, or after a pause in the typing as the
  index search does — each search costing one of the 100 calls an hour?~~ → Enter only (Q&A 18)
- [x] ~~**Attribution placement**: the Powered By GIPHY mark next to the search box, on the breadcrumb line,
  or under the list?~~ → At the right end of the breadcrumb line (Q&A 17)
- [x] ~~**Ctrl+F cycle**: does it go through the GIPHY view (skipped without a key, as People is without the
  face analysis)?~~ → Yes, last, skipped while no key is set (Q&A 15)
- [x] ~~**Search criteria and sort**: hidden or disabled in the GIPHY view?~~ → Hidden (Q&A 20)
- [x] ~~**Hearting a GIPHY tile**: no heart, or downloaded into a folder next to the exe and made a
  favorite (as the pasted favorites)?~~ → Downloaded into `giphy-favorites\`, an ordinary favorite
  (Q&A 21)
- [x] ~~**API key dialog**: the key shown in clear in `NameDialog`, or masked?~~ → In clear (Q&A 22)
- [x] ~~**Unit tests**: create a test project for the GIPHY parsing and paging, or check by hand as the
  previous workfiles did?~~ → By hand (Q&A 23)
- [x] ~~**Backlog**: the *Stickers in layers* row of `TODO-FEATURES.md` names GIPHY stickers — mark the part
  this workfile covers, split the row, or leave it?~~ → Split: *Stickers in layers, GIPHY* marked with this
  workfile, the rest left to discuss (Q&A 24)

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

### Iteration 4 — 2026-11-05

Second batch (Q&A 12–15): the content rating an app setting of the ⚙ menu, `r` by default; the Ctrl+F
cycle goes through the GIPHY view, skipped without a key. The page size and the attribution's place
were not understood: explained (100 calls an hour, not 100 images) and asked again, with a mockup. A new
question follows from the budget: what triggers a search.

### Iteration 5 — 2026-11-05

Q&A 16–18: 50 results a call; the Powered By GIPHY mark at the right end of the breadcrumb line; a search
starts on Enter only. § Requests, § Loads and the attribution updated.

### Iteration 6 — 2026-11-05

Q&A 19–22: the term *online library*; the criteria, sort and sections buttons hidden in the GIPHY view; a
heart on a GIPHY tile keeps its file in `giphy-favorites\` as an ordinary favorite; the key shown in clear.
§ Terms, § The GIPHY View, § Favorites (new) and § API Key updated.

### Iteration 7 — 2026-11-05

Q&A 23–24: no test project, the behaviours checked by hand; the backlog's *Stickers in layers* row split,
its GIPHY part marked with this workfile. No open question left.

### Iteration 8 — 2026-11-05 — ✅ Implemented

Go given: code, tests and documentation, in a worktree on `feature/online-library-giphy`. The scope is
frozen as the design sections stand at this iteration.

### Iteration 9 — 2026-11-05 — 🧭 Implementation choices

No project rule broken. The choices the frozen design left open, or took another way, now in the domain
sections:

- **Attribution as text**: the words *Powered by GIPHY* in bold, not GIPHY's logo image — shipping its file
  would have meant downloading it into the repository; the text is what the design's sources said suffices.
- **Thumbnails apart from the Shell's cache**: a class of their own, `UI/OnlineThumbnails`, rather than a
  pseudo-path in `ThumbnailCache`'s loader — that cache scales one still per path; an animation needed its
  frames and their timings. Each GIF is kept compressed, a frame decoded when shown, rather than every frame
  decoded once: 50 results × their frames at 200 px would have cost hundreds of MB.
- **Rendition**: `fixed_width_downsampled` for the tiles (fallbacks `fixed_width`, `preview_gif`); a GIF
  without an MP4 rendition is brought as its GIF.
- **The breadcrumb's ↑, its GIPHY segment, Backspace and Alt+↑** go back to the trending — the folder
  keys' meaning in this view. `SHORTCUTS.md` / `.fr.md` say it, with Enter searching GIPHY and a double-click
  downloading first.
- **No context menu on a GIPHY tile**, and `Exists`, the size and the indexed content refuse its row: its
  `FullPath` is a key, never a file.
- **Refusals on the status line**, not the caption: the breadcrumb is the GIPHY view's caption line.
- **The view opened with a text typed** shows the trending, the text waiting for Enter; **a rating change**
  asks the list shown again.
- **Icons**: 🌍 the view, 🎞️ GIFs (the existing GIF icon), 😎 Stickers, 🔑 the key, 🔞 the rating.
- **An empty key forgets it**; the invitation's button takes the 🔑 icon.
- **Hidden in the view**: 🔠 / 💡, the sort and the three groups buttons; the rescan and the Thumbnails /
  Details buttons stay.
- **Checks by hand**: the build is clean, both resources complete; GIPHY's refusal of an invalid key was
  checked against the live API (401, *Unauthorized*). The results, the paging, the thumbnails, the
  download and the drag could not be checked without a GIPHY key: they are left to the user's checks.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 8, 9 | 2026-11-05 | `Web/Giphy`, `Web/GiphyFiles`, `UI/OnlineThumbnails`, `UI/FileExplorerPanel.Giphy.cs`, `UI/MainForm.Giphy.cs`, the grid's online tiles, the texts EN + FR |
| Unit tests | — | 2026-11-05 | None: no test project, checks by hand (Q&A 23) |
| README (EN + FR) | 9 | 2026-11-05 | The GIPHY view, the key, the budget, the attribution; Tech |
| GLOSSARY (EN + FR) | 9 | 2026-11-05 | *Online library*, *GIPHY view*, *Content rating*; four views |
| SHORTCUTS (EN + FR) | 9 | 2026-11-05 | Ctrl+F through the GIPHY view; Enter searching it; ↑ keys back to the trending; double-click downloading first |
| RULES.md | 9 | 2026-11-05 | § Online Libraries |

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
| 12 | Page size of a load | "Wait, 100 images an hour?" → explained: 100 API calls, each up to 50 results; the thumbnails and downloads come from the CDN and do not count. Asked again (#16) | 2026-11-05 |
| 13 | Content rating of the requests | A ⚙ menu setting, `r` by default | 2026-11-05 |
| 14 | Where the Powered By GIPHY mark sits | "I don't understand where you propose" → drawn as a mockup, asked again (#17) | 2026-11-05 |
| 15 | Does the Ctrl+F cycle go through the GIPHY view? | Yes, skipped without a key | 2026-11-05 |
| 16 | Page size of a load (again) | 50, the largest | 2026-11-05 |
| 17 | Where the Powered By GIPHY mark sits (again, with a mockup) | At the right end of the breadcrumb line | 2026-11-05 |
| 18 | Search trigger: Enter only, or after a pause in the typing | Enter only | 2026-11-05 |
| 19 | The term for the new concept | Online library | 2026-11-05 |
| 20 | Search criteria, sort and sections buttons in the GIPHY view | Hidden | 2026-11-05 |
| 21 | Hearting a GIPHY tile | Yes, the file kept in a folder next to the exe | 2026-11-05 |
| 22 | The API key shown in clear or masked | In clear | 2026-11-05 |
| 23 | Unit tests: a test project, or checks by hand | Checks by hand | 2026-11-05 |
| 24 | The backlog's *Stickers in layers* row | Split the row | 2026-11-05 |

---

*Last updated: 2026-11-05*
