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

  When the site offers no single file at all (YouTube's usual case), the load fails with
  **This video needs ffmpeg** and the **Install ffmpeg…** offer (§ Companions).
- **Codecs** — see Open Questions: the reader is Media Foundation (`Imaging/VideoReader.cs`). H.264
  and AAC always decode; VP9 needs the *VP9 Video Extensions* (usually preinstalled), AV1 the *AV1
  Video Extension* (not always there). yt-dlp's default order prefers AV1 > VP9 > H.264 at equal
  resolution, so a sort is passed (`-S`) to rank the codecs Windows reads first; the sound prefers
  AAC (`m4a`).

---

## Companions — Deno and ffmpeg

- **Deno** (`DenoLand.Deno`) — yt-dlp's JavaScript runtime, needed for YouTube's formats. Passed
  explicitly: `--js-runtimes deno:<deno.exe>`, never looked for on the `PATH` (the yt-dlp rule:
  RULES.md § Content From a URL).
- **ffmpeg** (`yt-dlp.FFmpeg`, yt-dlp's own builds) — merges the video and sound tracks. Passed
  explicitly with `--ffmpeg-location`.
- **Not shipped**, like yt-dlp: each looked for in its own folder only (its executable at the root,
  else the first one below it, as `YtDlp.Find` does — the zips nest a `bin\`), each folder name in one
  constant `FolderName` (kebab-case). Placement: see Open Questions.
- **Installed with winget**, the command shown and confirmed first, nothing installed without that
  **Yes**:

  ```
  winget install --id DenoLand.Deno --exact --location "<deno folder>" --accept-package-agreements
         --accept-source-agreements --disable-interactivity
  winget install --id yt-dlp.FFmpeg --exact --location "<ffmpeg folder>" --accept-package-agreements
         --accept-source-agreements --disable-interactivity
  ```

- Each companion is **optional**: missing, its option is left out of yt-dlp's arguments and yt-dlp
  does what it can without it. When to offer them: see Open Questions.

---

## Downloaded File

- The name carries the height: `%(extractor)s-%(id)s-%(height)sp.%(ext)s` — the **same file already
  there is reused** (RULES.md § Content From a URL), and a video first loaded without ffmpeg at 360p
  is then downloaded again at its larger size once ffmpeg is there, instead of reusing the small one.
- With ffmpeg, yt-dlp downloads two files then merges them: the percentage runs once per track; the
  merged file is the one `--print after_move:filepath` gives, as today. Progress display: see Open
  Questions.

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

The repository has **no test project** (`ImageGridFusion.slnx` holds the app only). The selector built
from the Video size and the companions present is a pure function that could be pinned — see Open
Questions.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(pending the test-project question)* | | |

---

## Open Questions

- [x] ~~ffmpeg: single file only, optional, or required?~~ → **Optional**, installed on demand next
  to the exe with winget on the user's **Yes**: the largest format with it, the largest single file
  without it.
- [x] ~~"The largest possible": up to where?~~ → **Up to the Video size** (`AppSettings.VideoMaxSide`).
- [x] ~~Which sites?~~ → **Every site** yt-dlp resolves, one selector.
- [ ] **Deno** — YouTube needs it to list its formats at all. Optional companion like ffmpeg, or
  installed together with it?
- [ ] **Placement** — the companions in subfolders of `yt-dlp\` (`yt-dlp\deno\`, `yt-dlp\ffmpeg\`), one
  folder for the whole yt-dlp route, or folders of their own next to the exe (`deno\`, `ffmpeg\`)?
- [ ] **When to offer the install** — yt-dlp already installed without its companions (every current
  install): offered only when a load fails for want of them, also as a caption hint when a load
  succeeded with a smaller file, and / or from the ⚙ menu (as the OCR engine is)? And a fresh yt-dlp
  install: the companions in the same confirmation?
- [ ] **Codecs above 1920** — at 2560 or 4096, YouTube's H.264 stops at 1080p: the **largest first**,
  whatever the codec (VP9 / AV1, which may not decode on every machine), or **H.264 first**?
- [ ] **Progress with two tracks** — the percentage restarting for the sound track, plus a *Merging…*
  step: acceptable as is, or one percentage for the whole?
- [ ] **Tests** — create a test project to pin the selector, or none?

---

## Design Iterations

### Iteration 1 — 2026-10-15

Initial design from the scoping batch (ffmpeg optional, capped at the Video size, every site) and a
read of `Web/YtDlp.cs`. The exploration turned up a second cause: yt-dlp needs **Deno** for YouTube
since 2025.11, and `yt-dlp.yt-dlp`'s winget manifest declares `DenoLand.Deno` and `yt-dlp.FFmpeg` as
dependencies, both skipped today. The design adds Deno next to ffmpeg; its status is an Open Question.
The user's own yt-dlp could not be run here (none found under the repository's builds), so
`--list-formats` for the failing video was not checked.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | ffmpeg: single file only, optional, or required? | Optional (installed on demand, winget, on Yes) | 2026-10-15 |
| 2 | "The largest possible": up to where? | Capped at the Video size | 2026-10-15 |
| 3 | Which sites does the format choice cover? | Every yt-dlp site | 2026-10-15 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-15 |

---

*Last updated: 2026-10-15*
