# yt-dlp Best Format

> Working document — the URL zone's yt-dlp route downloads the largest format a site offers, up to
> the Video size, with ffmpeg and Deno as optional companions of yt-dlp.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A YouTube URL loaded in the URL zone fails with
`yt-dlp: [youtube] XJ-pcvL2yqI: Requested format is not available. Use --list-formats for a list of available formats`.

Today `Web/YtDlp.LoadAsync` asks yt-dlp for `-f b[ext=mp4]/b` — the best **single file** holding video
and sound — and yt-dlp is installed with `--skip-dependencies`, so it runs **without ffmpeg** (by
design: `workfiles/20261009-url-content.md` § yt-dlp, RULES.md § Content From a URL) **and without
Deno**.

Two causes add up:

| Cause | Effect |
|---|---|
| **No JavaScript runtime** | Since yt-dlp 2025.11.12, YouTube's challenges need an external JS runtime — Deno by default, looked for on the `PATH`. Without one, yt-dlp falls back to degraded clients and *some formats may be missing* — often all the usable ones, hence the error |
| **Single file only** | YouTube serves almost nothing as one file above 360p: the high qualities are a video track and a sound track, merged by ffmpeg |

The winget package `yt-dlp.yt-dlp` (2026.08.19) declares both as dependencies — `DenoLand.Deno` and
`yt-dlp.FFmpeg` — which the app's `--skip-dependencies` leaves out. Both are **portable zips**, so
`--location` installs them into a folder of the app's choosing.

Goal: the **largest format available, up to the Video size**, for every site yt-dlp resolves — with
ffmpeg present; the largest single file without it.

Components: `Web/YtDlp.cs` (arguments, install, companions' lookup), `UI/UrlZone.cs` (install offer,
captions), `UI/AppSettings.cs` (`VideoMaxSide`, read only), RULES.md § Content From a URL, README /
README.fr.md (the optional companions).

---

## Format Selection

One selector for **every site** yt-dlp resolves, YouTube included.

- **The cap**: the Video size (`AppSettings.VideoMaxSide`, read when the load starts — 1920 by
  default, 2560 or 4096). A video export is clamped to it anyway (RULES.md § Output Format), so a
  larger file would be downloaded for nothing. The cap applies to the **longer side**, so a portrait
  video (a Short) is capped like a landscape one: `[width<=?S][height<=?S]` (`?` keeps a format whose
  size is unknown).
- **Over the cap only**: when every format is larger than the cap, the largest is taken rather than
  failing.
- **With ffmpeg** — the best video track plus the best sound track, merged into an MP4:

  ```
  -f "bv*[width<=?S][height<=?S]+ba/b[width<=?S][height<=?S]/bv*+ba/b"
  --merge-output-format mp4
  --ffmpeg-location "<ffmpeg folder>"
  ```

- **Without ffmpeg** — the largest single file within the cap:

  ```
  -f "b[width<=?S][height<=?S]/b"
  ```

  When the site offers no single file at all (YouTube's usual case), the load fails with the
  companions' offer (§ Companions › When They Are Offered).
- **Codecs — the largest first**: the resolution ranks first, whatever the codec, so at a Video size
  of 2560 or 4096 a 1440p or 2160p VP9 / AV1 track beats YouTube's 1080p H.264. **At equal
  resolution**, the codecs Windows reads most surely come first: **H.264 > VP9 > AV1** — the reader is
  Media Foundation (`Imaging/VideoReader.cs`): H.264 always decodes, VP9 needs the *VP9 Video
  Extensions* (usually preinstalled), AV1 the *AV1 Video Extension* (not always there). yt-dlp's own
  order is the reverse (AV1 > VP9 > H.264), so a sort is passed: `-S "res,+codec:avc:m4a"`
  (`YtDlp.SortOrder`) — the resolution first, then yt-dlp's reversed codec order, H.264 > H.265 > VP9 >
  AV1, and AAC for the sound. It replaces the former `[ext=mp4]` preference: at equal resolution the
  H.264 / AAC format is the MP4 one. The selector is `YtDlp.FormatSelector(maxSide, merge)`.

---

## Companions — Deno and ffmpeg

- **Deno** (`DenoLand.Deno`) — yt-dlp's JavaScript runtime, needed for YouTube's formats. Passed
  explicitly: `--js-runtimes deno:<deno.exe>`, never looked for on the `PATH` (the yt-dlp rule:
  RULES.md § Content From a URL): `--no-js-runtimes` always comes first, clearing yt-dlp's default
  runtimes, so a Deno or a Node on the `PATH` is never used.
- **ffmpeg** (`yt-dlp.FFmpeg`, yt-dlp's own builds) — merges the video and sound tracks. Passed
  explicitly with `--ffmpeg-location`, **always**: its folder even while it is missing, so yt-dlp
  looks for no other one on the `PATH` and goes on without it.
- **`Web/WingetTool`**: one tool installed with winget into its folder next to the exe — its lookup,
  its install command, its install, the hidden process runner. yt-dlp (`YtDlp.Tool`) and the two
  companions (`YtDlpCompanions.Deno`, `.Ffmpeg`) are three of them; the folder names are constants,
  `YtDlp.FolderName`, `YtDlpCompanions.DenoFolderName` and `FfmpegFolderName`.
- **Not shipped**, like yt-dlp: each looked for in **its own folder next to the exe** only —
  **`deno\`** and **`ffmpeg\`**, siblings of `yt-dlp\` — its executable at the root, else the first one
  below it, as `YtDlp.Find` does (the zips nest a `bin\`); each folder name in one constant
  `FolderName` (kebab-case).
- **One batch, "the yt-dlp companions"**: Deno and ffmpeg are offered **together**, in one
  confirmation showing the winget command of each one missing (a companion already there is left
  out), nothing installed without that **Yes**:

  ```
  winget install --id DenoLand.Deno --exact --location "<exe folder>\deno" --accept-package-agreements
         --accept-source-agreements --disable-interactivity
  winget install --id yt-dlp.FFmpeg --exact --location "<exe folder>\ffmpeg" --accept-package-agreements
         --accept-source-agreements --disable-interactivity
  ```

- Each companion stays **optional** at load time: missing, its option is left out of yt-dlp's
  arguments and yt-dlp does what it can without it.
- An install is a user action: a `--new-instance` instance offers it too (as yt-dlp's).

### When They Are Offered

Every current install has yt-dlp without its companions. The offer comes from four places:

| Where | When | Shows |
|---|---|---|
| **A failed load** | A yt-dlp load fails while a companion is missing | The reason, then an **Install yt-dlp companions…** link in the caption, as **Install yt-dlp…** is; once installed, the load is **retried** by itself |
| **A load that succeeded** | A yt-dlp load succeeds while a companion is missing — the file may be smaller than the site offers | A hint in the caption, with the same link; once installed, the load is retried (a new file name, § Downloaded File) |
| **The ⚙ menu** | yt-dlp is there and a companion is missing | An **Install yt-dlp companions…** entry after *File explorer pages per load*, as the OCR engine's; hidden once both are there, disabled while they install. It runs in the URL zone: the URL of a hint or a failure shown there is loaded again; otherwise the media the zone held comes back, or the caption says `Deno and ffmpeg installed: yt-dlp now loads the largest formats` |
| **A fresh yt-dlp install** | The **Install yt-dlp…** offer | The same confirmation lists the companions' commands after yt-dlp's; they are installed right after it |

- The hint reads `Install yt-dlp companions… for a larger format · <file> · <size>`, in the caption's
  ordinary color, the media staying draggable.

---

## Downloaded File

- The name carries the height: `%(extractor)s-%(id)s%(height&-{}p|)s.%(ext)s` (`youtube-XJ-pcvL2yqI-1080p.mp4`,
  no suffix for a media without a height) — the **same file already there is reused** (RULES.md
  § Content From a URL), and a video first loaded without ffmpeg at 360p is then downloaded again at
  its larger size once ffmpeg is there, instead of reusing the small one.
- With ffmpeg, yt-dlp downloads two files then merges them; the merged file is the one
  `--print after_move:filepath` gives, as today.
- **One gauge for the whole load**: 0 to 100 % over both tracks, each weighted by its size — the
  bytes downloaded over the bytes of every track — never restarting at 0 for the sound track, never
  going back (`YtDlp.TrackGauge`). Two lines of the app's own, told from yt-dlp's by their marks:
  - `--print before_dl:[igf-tracks] …` — the format ids (`137+140`) and each track's size
    (`filesize`, else `filesize_approx`);
  - `--progress-template download:[igf-progress] …` — the status, the track's format id, the bytes
    downloaded, its total (or estimate).

  A track's size unknown, each track counts for an equal share. A single-file load is one track.
- **Joining the video and its sound…** while ffmpeg merges them, from the moment the last track's
  progress says `finished` — yt-dlp's own `[Merger]` line is not printed: `--print` implies
  `--quiet`.

---

## Documentation

- **RULES.md § Content From a URL**: "asked for a single-file format, so no ffmpeg is needed" replaced
  — the largest format up to the Video size, Deno and ffmpeg optional companions in their folders,
  never the `PATH`.
- **README.md / README.fr.md**: the optional companions next to yt-dlp.
- **GLOSSARY.md / GLOSSARY.fr.md**: *Resolver* row — yt-dlp with its companions, if the wording needs it.
- `Web/YtDlp.cs`'s class summary.

---

## Test Impact

**No unit test**: the repository has no test project (`ImageGridFusion.slnx` holds the app only) and
none is created for this work. The selector is checked by hand on the failing video
(`XJ-pcvL2yqI`): without the companions, with them, at each Video size.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (none: no test project, checked by hand) | — | — |

---

## Open Questions

- [x] ~~ffmpeg: single file only, optional, or required?~~ → **Optional**, installed on demand next
  to the exe with winget on the user's **Yes**: the largest format with it, the largest single file
  without it.
- [x] ~~"The largest possible": up to where?~~ → **Up to the Video size** (`AppSettings.VideoMaxSide`).
- [x] ~~Which sites?~~ → **Every site** yt-dlp resolves, one selector.
- [x] ~~**Deno** — YouTube needs it to list its formats at all. Optional companion like ffmpeg, or
  installed together with it?~~ → **With ffmpeg, one batch**: "the yt-dlp companions", one
  confirmation showing both winget commands.
- [x] ~~**Placement** — the companions in subfolders of `yt-dlp\` (`yt-dlp\deno\`, `yt-dlp\ffmpeg\`), one
  folder for the whole yt-dlp route, or folders of their own next to the exe (`deno\`, `ffmpeg\`)?~~ →
  **Folders of their own**: `deno\` and `ffmpeg\` next to the exe.
- [x] ~~**When to offer the install** — yt-dlp already installed without its companions (every current
  install): offered only when a load fails for want of them, also as a caption hint when a load
  succeeded with a smaller file, and / or from the ⚙ menu (as the OCR engine is)? And a fresh yt-dlp
  install: the companions in the same confirmation?~~ → **All four**: on a failed load, as a hint on a
  load that succeeded without them, from the ⚙ menu, and in a fresh yt-dlp install's confirmation.
- [x] ~~**Codecs above 1920** — at 2560 or 4096, YouTube's H.264 stops at 1080p: the **largest first**,
  whatever the codec (VP9 / AV1, which may not decode on every machine), or **H.264 first**?~~ →
  **The largest first**, H.264 > VP9 > AV1 at equal resolution.
- [x] ~~**Progress with two tracks** — the percentage restarting for the sound track, plus a *Merging…*
  step: acceptable as is, or one percentage for the whole?~~ → **One gauge** over both tracks, weighted
  by their sizes, then *Merging…*.
- [x] ~~**Tests** — create a test project to pin the selector, or none?~~ → **None**: checked by hand on
  the failing video.
- [ ] **A package winget already installed elsewhere** *(found during the run, Iteration 5)* — winget
  keeps one install per package id: Deno or ffmpeg installed by the user with winget before, or yt-dlp
  installed for another copy of the app (a worktree build, another folder), and `winget install
  --location` may answer *already installed* without putting anything in the app's folder — the
  install then fails with `winget couldn't install … put … in <folder>`. Handle it (`--force`, a
  download of the release itself as the OCR engine does), or keep the hand-made fallback?

---

## Design Iterations

### Iteration 1 — 2026-10-15

Initial design from the scoping batch (ffmpeg optional, capped at the Video size, every site) and a
read of `Web/YtDlp.cs`. The exploration turned up a second cause: yt-dlp needs **Deno** for YouTube
since 2025.11, and `yt-dlp.yt-dlp`'s winget manifest declares `DenoLand.Deno` and `yt-dlp.FFmpeg` as
dependencies, both skipped today. The design adds Deno next to ffmpeg; its status is an Open Question.
The user's own yt-dlp could not be run here (none found under the repository's builds), so
`--list-formats` for the failing video was not checked.

### Iteration 2 — 2026-10-15

Four Open Questions answered. Deno and ffmpeg become **one batch**, "the yt-dlp companions", offered
in one confirmation; they live in **folders of their own** next to the exe (`deno\`, `ffmpeg\`); the
offer comes from **four places** — a failed load, a hint on a load that succeeded without them, the
⚙ menu, a fresh yt-dlp install (§ When They Are Offered); the format ranks the **resolution first**,
H.264 > VP9 > AV1 at equal resolution.

### Iteration 3 — 2026-10-27

The last two Open Questions answered: **one gauge** for the whole load, both tracks weighted by their
sizes, then a *Merging…* step (§ Downloaded File); **no test project**, the selector checked by hand
(§ Test Impact). No Open Question left: the design is complete.

### Iteration 4 — 2026-10-27 — ✅ Implemented

Go given: **code, tests and documentation** (no unit test — § Test Impact), in a **worktree** on
`feature/yt-dlp-best-format`, created from `main` at the design's last commit.

### Iteration 5 — 2026-10-27 — 🧭 Implementation choices

- **`Web/WingetTool`** — one class for a tool installed with winget into its folder (lookup, command,
  install, hidden process runner); yt-dlp moved onto it, the companions are two more
  (§ Companions).
- **`--no-js-runtimes` always**, then Deno's own `--js-runtimes deno:<path>`; **`--ffmpeg-location`
  always**, ffmpeg's folder even while it is missing — so neither is ever taken from the `PATH`
  (§ Companions).
- **The sort `res,+codec:avc:m4a`** — yt-dlp's reversed codec order puts H.265 between H.264 and VP9,
  and replaces the former `[ext=mp4]` preference (§ Format Selection).
- **The gauge's two lines** — a `before_dl` print of the tracks' ids and sizes, a progress template of
  the app's own; equal shares when a size is unknown (§ Downloaded File).
- **The join step** starts from the last track's `finished` progress, not from yt-dlp's `[Merger]`
  line, which `--print`'s implied `--quiet` hides; its caption is *Joining the video and its sound…*.
- **The name's height** — `%(height&-{}p|)s`, so a media without a height gets no `-NAp`.
- **The ⚙ entry** sits after *File explorer pages per load* and runs in the URL zone; with no URL to
  load again, the media the zone held comes back, or the caption says the companions are installed
  (§ When They Are Offered).
- **Not verified** ⚠️: running yt-dlp was refused by the session's safety check (auto mode), so the
  format selector, the templates and the gauge were never run against YouTube; the options were
  checked against yt-dlp 2026.08.19's README only. The winget installs of Deno and ffmpeg were not run
  either. Found meanwhile: an Open Question on packages winget already installed elsewhere.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 5 | 2026-10-27 | Built, 0 warning; never run against YouTube (§ Iteration 5) |
| Unit tests | 4 | 2026-10-27 | None — no test project, by decision (§ Test Impact) |
| README | 4 | 2026-10-27 | README / README.fr.md, GLOSSARY / GLOSSARY.fr.md, RULES.md § Content From a URL |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | ffmpeg: single file only, optional, or required? | Optional (installed on demand, winget, on Yes) | 2026-10-15 |
| 2 | "The largest possible": up to where? | Capped at the Video size | 2026-10-15 |
| 3 | Which sites does the format choice cover? | Every yt-dlp site | 2026-10-15 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-15 |
| 5 | Deno: optional companion of its own, or installed together with ffmpeg? | With ffmpeg, one batch | 2026-10-15 |
| 6 | Companions' placement: under `yt-dlp\` or folders of their own? | Folders of their own (`deno\`, `ffmpeg\`) | 2026-10-15 |
| 7 | When is the companions' install offered? | On a failed load, as a hint on a smaller success, from the ⚙ menu, with a fresh yt-dlp install | 2026-10-15 |
| 8 | Codecs above 1920: largest first or H.264 first? | Largest first | 2026-10-15 |
| 9 | Progress with two tracks: as is, or one percentage? | One gauge, weighted by the tracks' sizes | 2026-10-15 |
| 10 | Tests: create a test project, or none? | None, checked by hand | 2026-10-15 |

---

*Last updated: 2026-10-27*
