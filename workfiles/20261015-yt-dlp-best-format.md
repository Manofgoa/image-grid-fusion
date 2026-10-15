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
  order is the reverse (AV1 > VP9 > H.264), so a sort is passed (`-S`, resolution first, then that
  codec order); the sound prefers AAC (`m4a`). The exact `-S` string is checked against yt-dlp's
  ranking with `--list-formats` / `--simulate` during the run.

---

## Companions — Deno and ffmpeg

- **Deno** (`DenoLand.Deno`) — yt-dlp's JavaScript runtime, needed for YouTube's formats. Passed
  explicitly: `--js-runtimes deno:<deno.exe>`, never looked for on the `PATH` (the yt-dlp rule:
  RULES.md § Content From a URL).
- **ffmpeg** (`yt-dlp.FFmpeg`, yt-dlp's own builds) — merges the video and sound tracks. Passed
  explicitly with `--ffmpeg-location`.
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
| **The ⚙ menu** | yt-dlp is there and a companion is missing | An **Install yt-dlp companions…** entry, as the OCR engine's; hidden once both are there |
| **A fresh yt-dlp install** | The **Install yt-dlp…** offer | The same confirmation lists the companions' commands after yt-dlp's |

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

### Iteration 2 — 2026-10-15

Four Open Questions answered. Deno and ffmpeg become **one batch**, "the yt-dlp companions", offered
in one confirmation; they live in **folders of their own** next to the exe (`deno\`, `ffmpeg\`); the
offer comes from **four places** — a failed load, a hint on a load that succeeded without them, the
⚙ menu, a fresh yt-dlp install (§ When They Are Offered); the format ranks the **resolution first**,
H.264 > VP9 > AV1 at equal resolution.

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
| 5 | Deno: optional companion of its own, or installed together with ffmpeg? | With ffmpeg, one batch | 2026-10-15 |
| 6 | Companions' placement: under `yt-dlp\` or folders of their own? | Folders of their own (`deno\`, `ffmpeg\`) | 2026-10-15 |
| 7 | When is the companions' install offered? | On a failed load, as a hint on a smaller success, from the ⚙ menu, with a fresh yt-dlp install | 2026-10-15 |
| 8 | Codecs above 1920: largest first or H.264 first? | Largest first | 2026-10-15 |
| 9 | Progress with two tracks: as is, or one percentage? | | 2026-10-15 |
| 10 | Tests: create a test project, or none? | | 2026-10-15 |

---

*Last updated: 2026-10-15*
