# Content Download Improvements

> Working document — the URL zone made easier to reach and to read: Ctrl+L, a title naming the sources, a
> placeholder, and the downloaded media's name, size and dimensions.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The URL zone (`UI/UrlZone.cs`, the file explorer's last row — RULES.md § Content From a URL) loads the media behind a
URL. Four improvements, from the user's request:

1. **Ctrl+L** puts the focus in its URL field, from anywhere in the window, opening the file explorer when collapsed.
2. A **title** above the field: **Content download** in bold, then the supported sources in parentheses.
3. A **placeholder** in the field naming the shortcut.
4. Once a media is ready, its **name** — the source's title when the file has no telling name —, its **size** and its
   **dimensions** in the caption.

Also checked at the user's request: **Ctrl+F** opens the file explorer when it is collapsed — it already does
(`FileExplorerPanel.FocusSearch`, workfiles/20261007-ctrl-f-search-focus.md); only its `SHORTCUTS.md` row does not say so.

No row of `workfiles/TODO-FEATURES.md` matches this request.

---

## Ctrl+L

- **Where**: anywhere in the main window, under the same guard as Ctrl+F (`MainForm.ProcessCmdKey`): while the file
  explorer is editing text (its search box, the URL field) or no text field has the focus. A text layer's field, a
  frame field, a Custom format field keep Ctrl+L for themselves.
- **Does**: opens the file explorer when collapsed (as its « button and Ctrl+F do, `FileExplorerPanel.SetOpen`), then
  focuses the URL field and **selects its text** — as a browser's address bar — so a paste replaces the URL there.
- One new public entry, `FileExplorerPanel.FocusUrl()`, calling `UrlZone.FocusField()`.
- Locked while exporting? No: Ctrl+F is not, and focusing a field changes nothing of the grid.
- `SHORTCUTS.md` / `SHORTCUTS.fr.md`: a row in § Anywhere next to Ctrl+F; the Ctrl+F row gains "opening the file
  explorer when collapsed".

---

## Title

- A new first row of the zone, above the field (and the preview, which keeps its 56 px square, its top aligned with
  the field): **Content download** in bold, then, in the regular weight and the grey of a hint,
  `(X / Twitter, YouTube, Instagram, TikTok, Reddit…)`.
  - French: **Téléchargement de contenu** (X / Twitter, YouTube, Instagram, TikTok, Reddit…).
- **The list is fixed**: the best-known sites yt-dlp loads, X / Twitter first since the app reads its posts itself.
  It does not depend on yt-dlp being installed — the zone already offers its install when a site needs it.
- **Its tooltip** completes it: direct links to an image or a video, and the 1 800+ sites yt-dlp supports, X / Twitter
  posts read without it.
- Drawn by one control of its own (`UrlZone.UrlTitle`) painting its two runs (bold, then regular grey), each cut with
  `…` when the panel is narrow — the sources first, the bold part last.
- `UrlZone.LogicalHeight` grows by the title's line; the explorer's layout reads it already.

---

## Placeholder

- The field's `PlaceholderText` (`Strings.CompUrlPlaceholder`) names the shortcut:
  - English: `Paste a URL (Ctrl+L)`
  - French: `Collez une URL (Ctrl+L)`
- The sources, until now in the placeholder (`X post, image, video…`), move to the title.

---

## Media Information

The caption of a ready media (`UrlZone.ShowReady`, and `ShowCompanionsHint`'s text) reads
`<name> · <size> · <width>×<height>`, then ` · <duration>` for a content that plays, e.g.
`Rick Astley - Never Gonna Give You Up · 12.4 MB · 1920×1080 · 3:33`.

- **Name**: the downloaded file's name **without its extension** — the file is now named after its source's title
  (§ File Names below), so the name shown and the file are the same.
- **Size**: as today (`Megabytes`, in the UI culture: `12,4 Mo` in French).
- **Dimensions**: the media's pixel size as the app loads it — `ImageLoader.TryLoadFile`, the bitmap's size: an image, a
  GIF's frame, a video's frame — read off the UI thread with the thumbnail (`UrlZone.Inspect`); unreadable, the part
  left out, never `?×?`.
- **Duration**: for a content that plays frame by frame only (`PageSource.HasFrames`: a video, an animated GIF) — its
  whole length as loaded (`PageSource.LoopDuration`), written `m:ss` (`h:mm:ss` from an hour, `0:04` under a minute;
  `0.8 s` under a second, `UrlCaption.Duration`). A still or a PDF shows none; unreadable, left out.
- **Cut when narrow**: the name is cut with `…`, the size, the dimensions and the duration always shown at the end —
  the caption's text rebuilt to the zone's width (`UrlCaption.Fit`, again on every resize), the companions' link kept
  at its start.
- **Tooltip**: the caption's whole text, as today, plus the file's full name (with its extension).

### File Names

What "inferred from the content when unknown" becomes, the file named so on disk — Save…, a favorite, the cell's
name and *Previous copy* reading it:

| Resolver | Today | New stem |
|---|---|---|
| yt-dlp | `%(extractor)s-%(id)s-<height>p` | `%(title).80B [%(id)s]-<height>p` — its title, then its id, so two videos titled alike never share a file and the reuse of a file already there (§ Content From a URL) still holds |
| X / Twitter post | `x-<user>-<id>` | `<author's name> - <start of the post's text> [<id>]` — the display name (30 characters at most), else the handle —, the text cut at a word near 60 characters and within 80 with the id kept whole, its links (`https://t.co/…`) removed; no text → `<author's name> [<id>]` (`XPostResolver.StemOf`) |
| Direct link | `<URL stem>-<tag>` | `<URL stem>-<tag>` when the URL's stem is a telling name; else `<the Content-Disposition file name's stem>-<tag>` when the server gives one; else `<host>-<tag>` (`DirectMediaResolver.StemOf`) |

- Every stem goes through `UrlDownloads.SafeStem` (invalid characters, 80 characters at most).
- A **telling name**: one holding a letter run of **4** or more — `photo`, not `IMG` nor `DSC` — and not a hexadecimal
  id (`a3f9c0d1e2`, `deadbeefcafe`) — `UrlDownloads.IsTelling`.
- The GIPHY files keep their own names (`GiphyFiles.NameOf`, already title + id).

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `SafeStem` keeps a title readable, cuts at 80, replaces invalid characters | `tests/ImageGridFusion.Tests/Web/UrlDownloadsTests.cs` | Create |
| `IsTelling`: `photo-beach` yes; `a3f9c0d1e2`, `deadbeefcafe`, `1234567890`, `IMG_1234`, `img` no | `tests/ImageGridFusion.Tests/Web/UrlDownloadsTests.cs` | Create |
| `Words`: a text on one line cut at a space, a single long word cut within | `tests/ImageGridFusion.Tests/Web/UrlDownloadsTests.cs` | Create |
| `Fit`: the name cut with `…`, the details kept | `tests/ImageGridFusion.Tests/UI/UrlZoneCaptionTests.cs` | Create |
| The X post's stem: author + start of the text cut at a word, links removed, id kept; no text → author + id | `tests/ImageGridFusion.Tests/Web/XPostResolverTests.cs` | Create |
| The direct link's stem: telling URL stem kept, `Content-Disposition` name next, host + tag last | `tests/ImageGridFusion.Tests/Web/DirectMediaResolverTests.cs` | Create |
| yt-dlp's output template carries the title then the id | `tests/ImageGridFusion.Tests/Web/YtDlpTests.cs` | Create |
| The caption's text: name · size · dimensions · duration, each unknown part left out, no duration for a still | `tests/ImageGridFusion.Tests/UI/UrlZoneCaptionTests.cs` | Create |
| The duration's format: `0:04`, `3:33`, `1:02:05`, `0.8 s` | `tests/ImageGridFusion.Tests/UI/UrlZoneCaptionTests.cs` | Create |

Ctrl+L, the title's drawing and the focus are UI — checked by hand at the launch.

---

## Open Questions

- [x] ~~Placeholder wording: `Paste a URL (Ctrl+L)`, or keep a hint of what to paste?~~ → `Paste a URL (Ctrl+L)` /
      `Collez une URL (Ctrl+L)`
- [x] ~~Videos and GIFs: show their **duration** too, after the dimensions?~~ → Yes, for a content that plays only
- [x] ~~X / Twitter post's name: author + start of the text, or something else?~~ → Author's display name + start of
      the text, `[id]` after
- [x] ~~The title's sources: a fixed list of known sites with the rest in a tooltip — or a link to yt-dlp's list of
      supported sites?~~ → Tooltip only, no link

---

## Design Iterations

### Iteration 1 — 2026-11-07

Initial design from the request and the scoping answers: Ctrl+L anywhere (Ctrl+F's guard), opening the explorer and
selecting the field's text; a fixed list of known sites in the title; the downloaded file named after its source's
title on disk, not only in the caption; the caption `name · size · dimensions`, the name cut first. Ctrl+F already opens
the collapsed explorer — only its shortcut row changes.

### Iteration 2 — 2026-11-07

Open questions answered: the placeholder `Paste a URL (Ctrl+L)`; the **duration** added after the dimensions for a video
or an animated GIF; an X post's media named after its author's display name and the start of its text; the title's
other sources in its tooltip only. § Media Information and § Test Impact updated.

### Iteration 3 — 2026-11-07 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree on `feature/content-download-improvements`.

### Iteration 4 — 2026-11-07 — 🧭 Implementation choices

- ⚠️ **Rule broken — a shortcut's `SHORTCUTS.md` rows in the same commit as its code** (RULES.md § Keyboard Shortcuts):
  Ctrl+L's handler (`53fe2a5`) and its rows (`1be324f`) landed in two commits, the run keeping code and docs apart.
  Nothing was blocking: an oversight, not a forced choice. The branch holds both; a squash on merge would rejoin them.
- **A telling name needs 4 letters in a run**, not 3 as designed: 3 would have taken `IMG` and `DSC` — camera names, not
  telling ones — for names.
- **The server's `Content-Disposition` name is taken whatever it is**, not checked for being telling: it is the name the
  server chose for its file.
- **The direct link's name is cut to leave its tag whole** (`Words` within 80 minus the tag): before, a long URL stem
  could lose its tag to `SafeStem`'s cut.
- **The title is a control of its own** (`UrlZone.UrlTitle`) rather than a `Label`: a label draws one font.
- **The dimensions are read through `ImageLoader.TryLoadFile`** — the same load the thumbnail already fell back to —
  rather than a header read per format: one route for an image, a GIF and a video; the thumbnail is still Windows' own
  first.
- **The caption's fitting is a text rebuilt to the width** (`UrlCaption.Fit`, binary search on the name's length,
  measured by `TextRenderer`) rather than two painted parts: the `LinkLabel` and its companions' link stay as they were.
- **Commits**: the URL zone's title, placeholder and caption landed together (`bae359e`, one file, `UrlZone.cs`), Ctrl+L's
  wiring apart (`53fe2a5`).

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-11-07 | `bae359e` URL zone title, placeholder, caption · `53fe2a5` Ctrl+L · `84b751f` file names |
| Unit tests | 3, 4 | 2026-11-07 | `7e342a1` — 18 tests, 5 classes, all passing |
| README | 3, 4 | 2026-11-07 | `1be324f` — English and French |
| SHORTCUTS | 3, 4 | 2026-11-07 | `1be324f` — Ctrl+L row, Ctrl+F row says it opens the explorer |
| RULES / GLOSSARY | 3, 4 | 2026-11-07 | `1be324f` — RULES § Content From a URL; the URL zone row, English and French |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Ctrl+L: where does it act, and what if the file explorer is collapsed? | Anywhere; it opens the explorer first, the field's text selected | 2026-11-07 |
| 2 | Which sources does the title list? | A few known sites yt-dlp loads, then `…`, the rest in a tooltip / link; direct links mentioned | 2026-11-07 |
| 3 | Where does the name come from when the file has no telling name? | The source's title (yt-dlp's title, the X post's text / author), else the URL's; the file named so on disk too | 2026-11-07 |
| 4 | Is the subject straightforward or tricky? | Straightforward — one scout pass | 2026-11-07 |
| 5 | *(user, unprompted)* Ctrl+F must open the explorer too, if it does not already | It already does (`FocusSearch`); its `SHORTCUTS.md` row will say so | 2026-11-07 |
| 6 | Placeholder wording | `Paste a URL (Ctrl+L)` | 2026-11-07 |
| 7 | Duration for videos and GIFs | Yes, after the dimensions | 2026-11-07 |
| 8 | X post's name | Author's display name - start of the text | 2026-11-07 |
| 9 | Title's sources: tooltip or link | Tooltip | 2026-11-07 |
| 10 | Go for implementation (scope, where) | Code, unit tests and documentation; worktree | 2026-11-07 |

---

*Last updated: 2026-11-07*
