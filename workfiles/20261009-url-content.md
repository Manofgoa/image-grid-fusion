# URL Content

> Working document — a URL zone at the bottom of the file explorer: paste the link of a post or a
> media (an X / Twitter post first), load it, see its preview, drag it into a cell.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a cell only takes local files (drop, Ctrl+V, browse, the file explorer's tiles). The user
wants to **add content from a URL** and get the **photo or video behind it** — first of all the
link of an **X / Twitter post**, which must give the post's video. Example:
`https://x.com/brivael/status/2108570415463530561` (a 720 × 1280 MP4 in the post).

Agreed at scoping (Q1–Q4):

- A **dedicated URL field**, at the **bottom of the file explorer**: the user types or pastes a
  URL, clicks **Load**; a **loader** shows while it downloads, then a **preview** of the media.
- The loaded media is **dragged and dropped into a cell**, exactly like an explorer tile. No other
  route (no Ctrl+V of a URL onto the grid, no command-line URL).
- **Built-in resolvers + optional yt-dlp**: own code for X / Twitter (the public syndication API,
  no account) and for direct links to a media; **yt-dlp** used as a fallback for the other sites
  when it is present.
- A post with several media: the **first one only**.

Agreed in design (Q5–Q8):

- The zone is **one compact row** (layout B): a 56 px thumbnail left of the field and its button.
- Downloads go to the **temp folder** (`%TEMP%\ImageGridFusion\`), not kept between sessions.
- **yt-dlp** lives in a **subfolder next to the exe**; missing, the app **offers to install it
  there with winget**.
- **No extras**: no quoted post's media, no `og:image` / `og:video`, no link dropped on the zone,
  no text cell for a media-less post.

Components: `UI/FileExplorerPanel.cs` (the zone, a new row of `_content`), a new `Web/` folder
(resolvers, download, yt-dlp), `UI/MainForm.cs` (`OnDragDrop` → `AddFilesAsync`, unchanged),
`Imaging/ImageLoader.cs` and `Imaging/ShellThumbnail.cs` (the preview, reused).

---

## URL Zone

### Placement

A new **last row** of the explorer's `_content` table (`FileExplorerPanel`, today 7 rows — row 6
the tile size row), below the tile size slider and the sort drop-down, separated from it by a thin
line. **One compact row** (layout B, ~65 logical px, its height DPI-scaled next to row 6's):

- at the left, the **preview**, a 56 × 56 logical px box;
- at its right, on a first line the **text field** and the **Load** button, on a second line the
  **caption** — the state's text, or the file name and size once ready, cut with `…`, the whole
  of it in the tooltip.

### Controls

- A **text field**, placeholder `Paste a URL (X post, image, video…)`. **Enter** loads, like the
  button. Ctrl+V, Ctrl+Z… are the field's own (a focused text field keeps its keys —
  `MainForm.ProcessCmdKey`'s whitelist).
- A **Load** button, disabled while the field is blank.
- The **preview** and the **caption** show, in turn:

  | State | Preview | Caption |
  |---|---|---|
  | Empty | A dim 🔗 | `Load a URL, then drag its media into a cell` |
  | Resolving | A loader | `Looking for the media…` |
  | Downloading | The post's **poster** when the resolver gives one (X does), dimmed, under the loader | `Downloading… 42 %` (the size received when the length is unknown) |
  | Ready | The media's **thumbnail** (`ShellThumbnail.TryLoad`, `ImageLoader` when Windows has none), a ▶ badge on a video; tooltip `Drag into a cell` | `x-brivael-2108570415463530561.mp4 · 4.2 MB` |
  | Failed | A dim ⚠ | The reason: `No photo or video in this post`, `Post not found or private`, `Not a web address`, a network error… — and for a site only yt-dlp can load while it is missing, the **Install yt-dlp…** offer (§ yt-dlp) |

- A new **Load** cancels the download in progress (`CancellationToken`); its partial file is
  deleted.
- The zone's content — the URL, the preview — is **not persisted** and **out of the undo history**
  (like the file explorer).

### Drag Into a Cell

- A press then a move past the system drag threshold on the **Ready** preview starts
  `DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { path }), Copy)` — the format of a tile
  (`FileExplorerPanel.StartDrag`), so `MainForm.OnDragDrop` → `AddFilesAsync` loads it like any
  file: replacing a cell, starting the grid over, effects reset — nothing new on the grid side.
- `_draggingOwnTile` is set during the drag, as for a tile, so dropping it back on the panel does
  not make it a favorite.
- The preview stays **Ready** after the drop: the same media can be dragged into several cells.

---

## Resolution

One chain, `Web/UrlResolver`, tries the resolvers **in order**; the first that recognises the URL
answers. A new site adds a resolver there.

| # | Resolver | Recognises | Gives |
|---|---|---|---|
| 1 | **X / Twitter** (`Web/XPostResolver`) | `x.com`, `twitter.com`, `mobile.twitter.com`, `fxtwitter.com`, `vxtwitter.com`, `fixupx.com` — `/<user>/status/<id>` and `/i/status/<id>`, any query | The post's first media |
| 2 | **Direct link** (`Web/DirectMediaResolver`) | An `http(s)` URL whose response is `image/*` or `video/*` | The file itself |
| 3 | **yt-dlp** (`Web/YtDlp`) | Anything left, when `yt-dlp.exe` is found | What yt-dlp downloads |

- Only `http` and `https` URLs are accepted; anything else fails at once (`Not a web address`).

### X / Twitter

Checked on the example post (2026-10-09): the syndication endpoint answers without an account.

- `GET https://cdn.syndication.twimg.com/tweet-result?id=<id>&token=<token>`, the token computed
  from the id as the embed widget does: `((id / 1e15) * π)` written in base 36, its `0`s and `.`
  removed.
- `mediaDetails[0]`:
  - `video` / `animated_gif` → the `video_info.variants` entry of type `video/mp4` with the
    **highest bitrate** (the `.m3u8` playlist ignored);
  - `photo` → `media_url_https` with `?name=orig`.
- `media_url_https` is the **poster** shown while the video downloads.
- No `mediaDetails` → `No photo or video in this post`; a 404 or an empty answer →
  `Post not found or private`.

### Direct Link

- A `GET` streamed to disk; the `Content-Type` decides: `image/*` or `video/*` is a media, anything
  else (an HTML page) passes to the next resolver.
- The file's **extension follows the content type** (`image/jpeg` → `.jpg`, `video/mp4` → `.mp4`,
  `video/webm` → `.webm`…), since `VideoFrames.TryOpen` gates on its extension whitelist.

### yt-dlp

- **Not shipped** with the app. It is looked for in **one place only**: `yt-dlp\yt-dlp.exe`, a
  subfolder next to the exe (`AppContext.BaseDirectory`, its name in one constant `FolderName`,
  kebab-case — `../CLAUDE.md` § Folder Names). Neither the `PATH` nor a setting.
- Run with `ProcessStartInfo.ArgumentList` (no shell), hidden window:
  `--no-playlist --newline -f "b[ext=mp4]/b" -o "<folder>/%(extractor)s-%(id)s.%(ext)s" -- <url>` —
  a **single-file** format, so no ffmpeg is needed to merge streams.
- Its `[download]  42.3%` lines feed the progress; its exit code and last `ERROR:` line give the
  failure message.
- Not found → the X and direct-link resolvers still work; any other site ends in **Failed** with
  `This site needs yt-dlp` and an **Install yt-dlp…** link in the caption (§ Installing yt-dlp).

#### Installing yt-dlp

- **Install yt-dlp…** opens a confirmation showing the exact command it will run, and saying it
  accepts winget's source and package agreements on the user's behalf:

  ```
  winget install --id yt-dlp.yt-dlp --exact --location "<exe folder>\yt-dlp" --skip-dependencies
         --accept-package-agreements --accept-source-agreements --disable-interactivity
  ```

  The package is a **portable** one, so `--location` puts it in that folder; `--skip-dependencies`
  leaves out `yt-dlp.FFmpeg` — the single-file format needs no ffmpeg.
- **Yes** → run hidden (`ArgumentList`, no shell); the zone shows a loader and
  `Installing yt-dlp…`; once winget exits with success and `yt-dlp.exe` is found in the folder (at
  its root, else the first one below it), the **load is retried** by itself.
- winget missing (`winget.exe` not found), or failing → **Failed** with the reason (winget's last
  output line) and the folder where `yt-dlp.exe` can be put by hand.
- An install is a user action: a `--new-instance` instance offers it too.
- Updating yt-dlp is **not handled** (out of scope): a site it no longer reads fails with its own
  error message.

### Download

- One shared `HttpClient` (`Web/Downloader`), a browser `User-Agent`, streamed to a `~downloading`
  partial file renamed once complete (the `previous\` pattern), progress reported from
  `Content-Length`.
- File name: `x-<screen_name>-<id>.<ext>` for a post, the URL's last segment (sanitised) for a
  direct link, yt-dlp's template otherwise.
- **Already there** — the same file name in the folder → reused, nothing downloaded.
- The files go to **`%TEMP%\ImageGridFusion\url-downloads\`** — a subfolder of the app's temp
  folder (`MainForm.TempVideoFolder`), so that its cleanup is one folder whatever the extension
  (`.png`, `.webm`, `.webp` included), its name in one constant `FolderName`.
- **Emptied at start-up**, next to `CleanTempVideos` — a file in use is left for a later session.
  A `--new-instance` instance **does not empty it** (RULES.md § Single Instance: no automatic
  write of its own accord), so an agent's instance never deletes the user's downloads.
- Accepted limit (Q6): a downloaded media made a favorite — a cell released on the explorer by its
  ✥ handle — points to a temp file, gone after the next start-up.

---

## Rules and Docs

- **RULES.md** — a new § *Content From a URL*: one resolver chain (`Web/UrlResolver`), a new site
  adds a resolver; a downloaded media reaches a cell **as a file**, by the explorer's drag, never by
  a route of its own; the zone is out of the history; yt-dlp is optional and never shipped.
- **GLOSSARY.md / GLOSSARY.fr.md** — *URL zone* (*zone URL*), *Resolver* (*résolveur*).
- **README.md / README.fr.md** — the feature in § Features, and yt-dlp as an optional companion
  (the `yt-dlp\` folder, the **Install yt-dlp…** offer).
- **TODO-FEATURES.md** — no row matches this request: nothing to mark.

---

## Test Impact

The repository has **no test project** (`src/ImageGridFusion/ImageGridFusion.csproj` only): **no
unit test is created or updated**. The check is manual, in the running app — the example post
(video), a photo post, a post without media, a deleted post, a direct `.jpg` / `.mp4` link, an HTML
page with and without yt-dlp, the yt-dlp install offer (accepted, cancelled), a Load cancelled by
another, the media dragged into two cells, the downloads emptied at the next start-up.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | Not applicable — no test project |

---

## Open Questions

- [x] ~~Layout of the URL zone — A, B or C (mockup)?~~ → B, one compact row with a 56 px thumbnail
- [x] ~~Where do the downloaded files go — next to the exe (`downloads\`, kept) or in
  `%TEMP%\ImageGridFusion\` (temporary)?~~ → The temp folder (`url-downloads\` inside it)
- [x] ~~Where is `yt-dlp.exe` looked for — next to the exe, on the `PATH`, a path set in the ⚙ menu?~~
  → A subfolder next to the exe; missing, the app offers to install it there with winget
- [x] ~~Which extras — the quoted post's media when the post has none, `og:image` / `og:video` for
  ordinary web pages, a link dragged from the browser onto the zone, a media-less post's text as a
  text cell?~~ → None

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design from the user's request (a URL, notably an X post, giving the photo / video behind
it), the scoping answers Q1–Q4, a single scout pass over the file explorer panel and the loading
path, and a live check of the X syndication endpoint on the example post.

- A URL zone as the explorer's last row: field + Load, a preview going loader → poster + progress →
  thumbnail, dragged into a cell as a `FileDrop` of the downloaded file — the grid side unchanged.
- A resolver chain: X / Twitter (syndication API, highest-bitrate MP4, first media only), direct
  link (by content type), yt-dlp as the fallback when present (single-file format, no ffmpeg).
- Proposed without a question (to confirm by reading): Enter loads; a new Load cancels the previous
  one; an already downloaded file is reused; the zone not persisted and out of the history; the
  preview stays draggable after a drop.

### Iteration 2 — 2026-10-09

Answers Q5–Q8, from a mockup of three layouts of the zone.

- Layout B: one compact row — a 56 px preview left of the field, Load and a caption line.
- Downloads in `%TEMP%\ImageGridFusion\url-downloads\`, emptied at start-up (not by a
  `--new-instance` instance); a favorite made from one is an accepted limit.
- yt-dlp looked for in `yt-dlp\` next to the exe only; missing, an **Install yt-dlp…** offer runs
  `winget install --location` there (portable package, dependencies skipped) after a confirmation
  showing the command, then retries the load.
- No extras: quoted post, `og:` tags, link dropped on the zone, text of a media-less post — all out.
- Proposed without a question (to confirm by reading): the `url-downloads\` subfolder rather than
  the temp folder's root, so one cleanup covers every extension; yt-dlp updates not handled.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| RULES.md | | | |
| README | | | |
| GLOSSARY | | | |
| TODO-FEATURES.md | 1 | 2026-10-09 | Not applicable — no matching row |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | By which routes does a URL reach a cell? | A dedicated URL field at the bottom of the file explorer: Load shows a loader then a preview; the loaded media is dragged and dropped into a cell | 2026-10-09 |
| 2 | How is the media behind the URL fetched? | Built-in (X / Twitter syndication API, direct links) + yt-dlp as an optional fallback | 2026-10-09 |
| 3 | An X post with several media? | The first media only | 2026-10-09 |
| 4 | Depth of the subject? | Straightforward — a single scout pass | 2026-10-09 |
| 5 | Layout of the URL zone — A, B or C (mockup)? | B — one compact row | 2026-10-09 |
| 6 | Where do the downloaded files go? | `%TEMP%\ImageGridFusion\` | 2026-10-09 |
| 7 | Where is `yt-dlp.exe` looked for? | In a subfolder next to the exe; when not installed, offer to run a winget command putting it there | 2026-10-09 |
| 8 | Which extras? | None | 2026-10-09 |

---

*Last updated: 2026-10-09*
