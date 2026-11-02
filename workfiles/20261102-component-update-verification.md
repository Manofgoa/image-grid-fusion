# Component Update Verification

> Working document — a window listing every downloadable component of the app, grouped by kind, with
> its installed, pinned and latest versions, to update one or all of them and go back to a safe version.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app downloads several components on demand, never shipped with it: the engines (OCR, face,
vision, caption, cutout), the shared native runtimes, and the yt-dlp tools (yt-dlp, Deno, ffmpeg).
Emoji fonts are designed to join them (`workfiles/20261102-emoji-fonts.md`). Today nothing tells
which version is installed, whether a newer one exists, or lets the user update one. A component is
"installed" when its files exist, and is never downloaded again.

This work adds:

- a **Component updates window**, separate from the main window, listing every downloadable
  component **grouped by kind**: its name, its version, the date of its latest version;
- a **check of the latest version** of each component, **when the window opens** and with
  **Check now**, a spinner in each row until its answer arrives;
- an **Update** button per component and an **Update all** button;
- a **components manifest**, `components.json` next to the exe, holding the pinned versions (editable
  by hand), the installed version and its dates, and an optional **forced version**: a safe fallback
  without rebuilding the app;
- going **back to the forced version or the pinned one** from the window;
- a **restart offered** at the end of the updates when a file in use could not be replaced.

The request's title says *periodic*, but the check runs only when the window opens or on **Check
now** (Q&A 3). There is no background check and no badge.

### What Exists

| Component | Code | Source | Version identity today | Lands in |
|---|---|---|---|---|
| OCR engine models | `Explorer/OcrEngineFiles` (private `EngineFile(Name, Package, Version, Entry, Sha256)`) | NuGet `rapidocrnet` 4.2.0, zip entries by HTTP ranges (`Web/RemoteZip`) | The package version, duplicated in `ImageGridFusion.csproj` | `ocr-engine\` |
| ONNX Runtime native | Same, `OnnxRuntime` | NuGet `microsoft.ml.onnxruntime` 1.29.0 | Package version, tied to the managed reference | `ocr-engine\` (shared by every engine) |
| SkiaSharp native | Same | NuGet `skiasharp.nativeassets.win32` 3.119.1 | Package version, tied to the managed reference | `ocr-engine\` |
| Face engine | `Explorer/FaceEngineFiles` | Hugging Face `opencv/face_detection_yunet`, `opencv/face_recognition_sface`, **`main`** | None, the date in the file names only | `face-engine\` |
| Vision engine | `Explorer/VisionEngineFiles` | Hugging Face `onnx-community/siglip2-base-patch16-256-ONNX` and `google/siglip2-base-patch16-256`, commit-pinned | The commit in the URL constants; `EmbeddingModelId` keys the vision caches | `vision-engine\` |
| Caption engine | `Explorer/CaptionEngineFiles` | Hugging Face `onnx-community/Florence-2-base`, commit-pinned | The commit in `Revision` | `vision-engine\` |
| Cutout engine | `Imaging/CutoutEngineFiles` | IS-Net: GitHub release `danielgatis/rembg` `v0.0.0`; MobileSAM: Hugging Face `Acly/MobileSAM`, commit-pinned | The URL pieces | `cutout-engine\` |
| yt-dlp, Deno, ffmpeg | `Web/WingetTool` (`Web/YtDlp`, `Web/YtDlpCompanions`) | winget `yt-dlp.yt-dlp`, `DenoLand.Deno`, `yt-dlp.FFmpeg`, `--location` | None, nothing pinned, never upgraded | `yt-dlp\`, `deno\`, `ffmpeg\` |
| Emoji fonts | Designed only (`workfiles/20261102-emoji-fonts.md`) | Undecided there | — | `emoji-fonts\` (planned) |
| Twemoji flag subset | Embedded in the exe (`feature/text-flag-emojis`) | Built once | Fixed at build time | **Not a component**: never downloaded |

- The direct engines share `EngineFile(Name, Url, Sha256, Size)` and one download loop,
  `Explorer/DirectEngineDownload`, which skips a file already there and checks the SHA-256 only at
  download time (`<name>.~downloading`, hashed, moved into place).
- `onnxruntime.dll` and `libSkiaSharp.dll` are loaded through `OcrEngineFiles.ResolveNativeLibraries`
  at their first use and stay mapped until the process ends: Windows refuses to replace them meanwhile.
- The model files are read when a session opens: the cutout engine (`UI/CutoutService`) and the
  vision text model (`FileExplorerPanel._textEncoder`) stay resident, each released after 5 minutes
  unused; the face, vision image, caption and RapidOCR sessions live for one pass or one query.
- The yt-dlp tools run as child processes only: replaceable whenever no URL load runs.
- The ⚙ menu offers each missing engine (`Download the … engine…`) and the yt-dlp companions, hidden
  once installed (`UI/MainForm.cs`). `UI/ShortcutsWindow` is the modeless single-window pattern
  (`MainForm.ShowShortcuts`: activated if open, else created and `Show(this)`).

---

## Components Window

Layout **A — one grouped list** (Q&A 1), drawn in the mockup of 2026-11-02:

- A **modeless window**, owned by the main window, opened by **Component updates…** in the ⚙ menu.
  There is one at a time: opened again, it comes back to the front (`ShortcutsWindow`'s pattern).
- **One list**, a header row per **group** (the component's kind), then one row per component.
- **Groups**, in this order: **Engines**, **Runtimes**, **Web tools**, **Emoji fonts** (§ Open Questions
  on the last two).
- **Columns**:

  | Column | Shows |
  |---|---|
  | Component | Its name (`Vision engine — SigLIP 2`), its upstream in the tooltip |
  | Installed | The installed version, `—` when not installed |
  | Installed on | When it was first installed |
  | Updated on | When it was last replaced, `—` if never |
  | Pinned | The version the app's code pins (the hard-coded value) |
  | Forced | The forced version of the manifest, `—` when none |
  | Latest | The latest upstream version; a **spinner** while it is being checked; *Couldn't check*, its reason in the tooltip, when it failed |
  | Released | The latest version's date, `—` when the source gives none |
  | Actions | **Update** while the latest differs from the installed, **Install** while not installed, and a **▾** menu: *Back to the forced version*, *Back to the pinned version* (each only when it differs from the installed) |

- **The check** runs **when the window opens** and on **Check now**: every row at once, each row's
  spinner stopping on its own answer. Nothing runs in the background, and nothing runs while the
  window is closed. The time of the last check is shown at the top.
- **Update all (N)** at the bottom updates every component whose latest version differs from the
  installed one, one after the other, each row showing its progress. **Close** closes the window, and
  updates already running carry on.
- A row's buttons are disabled while it updates; an engine downloading through the ⚙ menu's entry
  shows as updating there too.
- No shortcut is added (`SHORTCUTS.md` unchanged), apart from Escape closing the window, as in the
  shortcuts window.

---

## Components Manifest

`components.json`, **next to the exe**, read and written through one class (`UI/ComponentManifest`,
name to settle at implementation):

- **The hard-coded values stay in the code**: each component's **pinned version** and its files
  (URLs, SHA-256, sizes) as today. They are the fallback when the file is missing or unreadable.
- **The file holds, per component** (keyed by a stable id: `vision-engine`, `yt-dlp`…):

  | Key | Meaning |
  |---|---|
  | `pinned` | The pinned version, **editable by hand**. Written from the code's value when the file is created |
  | `forced` | Optional: a version the user forces as the safe fallback, without rebuilding the app |
  | `installed` | The version installed now |
  | `installedOn` | When it was first installed |
  | `updatedOn` | When it was last replaced |
  | `files` | The installed version's files with their SHA-256, as downloaded |

- **Missing → recreated** from the hard-coded values. A component whose files are already there (an
  install older than this feature) is recorded as the **pinned version**, installed on its files'
  creation date.
- **Version of the code newer than the file**: a pin changed in the code since the file was written
  leaves `pinned` alone when the user edited it, and takes the code's value otherwise (§ Open Questions).
- An unreadable file is reported in the status line and treated as missing for the session. It is
  never overwritten silently: it is kept as `components.json.bad` before being recreated.
- **`--new-instance`** recreates nothing on its own: a missing file stays in memory only. User actions
  in the window write it, as `people.json` and `text-styles.json` are written.

---

## Versions by Source

One **source** per kind of upstream, each giving the latest version, its date, and the files to fetch
for a version:

| Source | Latest version | Date | Files for a version |
|---|---|---|---|
| Hugging Face repository | `GET https://huggingface.co/api/models/<repo>/revision/main?blobs=true`: the commit, and each file's LFS SHA-256 | `lastModified` | `resolve/<commit>/<file>`; the SHA-256 known beforehand from the API |
| GitHub release | `GET https://api.github.com/repos/<repo>/releases/latest` (rembg's fixed `v0.0.0` tag: the asset's `updated_at`) | `published_at` / asset's `updated_at` | The asset's download URL |
| NuGet package | `GET https://api.nuget.org/v3-flatcontainer/<id>/index.json`, the last stable version | The registration's `published` | The same zip entries, by HTTP ranges, as today |
| winget package | `winget show --id <id> --exact`: its `Version` | Its `Release Date` when the manifest has one | `winget install --id <id> --exact --version <v> --location <folder>` |

- **A Hugging Face component is up to date** when each of its files' SHA-256 at `main` equals the
  installed one: a commit touching only the README offers no update.
- **The installed version of a winget tool** is read from the tool itself: `yt-dlp --version`,
  `deno --version`, `ffmpeg -version` (its first line), through `WingetTool.RunAsync`.
- **An update to the latest** has no hash pinned in the code: the files are checked against the hash
  the source gives when it gives one (Hugging Face, NuGet's package), and their SHA-256 is **recorded
  in the manifest** after the download in every case.
- The checks need no account. GitHub's anonymous limit (60 requests an hour) is far above one check
  per opening.

---

## Updating

- **Download first, then swap**: the new version is downloaded into a staging folder next to the
  component's own (`<folder>.~update\`), its files checked. Then the current version's files are
  **deleted** and the new ones moved into place, and the manifest is updated (`installed`, `updatedOn`,
  `files`). A failure or a cancel deletes the staging folder and leaves the installed version untouched.
- **Back to the forced or pinned version** goes the same way: the target version downloaded, then the
  current files deleted.
- **A file in use**: what can be released is released before the swap (§ Open Questions). What stays
  locked — the native runtimes once loaded, a tool while a URL load runs — is **left staged**: the swap
  is done at the next start, in `Program.Main` **before anything loads them**, and the row says
  *Restart to finish*.
- **Shared folders**: the caption and vision engines share `vision-engine\`, so only the updated
  component's own files are deleted, never the folder.
- **The vision engine's model id** (`EmbeddingModelId`) is part of its version: a new SigLIP version
  empties the vision caches, as a model change does today.
- The download loops (`DirectEngineDownload`, `OcrEngineFiles.DownloadAsync`) take the file list of
  the version asked for, instead of the hard-coded one only, and download into the staging folder.

## Restart

- When the updates end with at least one swap left for the next start, the window asks: **Restart
  now** / **Later**.
- **Restart now** starts the exe again with the same arguments (`--title`, `--new-instance` and the
  files aside), the new process **waiting for the old one to end** before taking the single-instance
  lock, then closes the app as its tray *Exit* does.
- **Later**: the swaps are done at the next start whenever it comes.

---

## Rules and Docs

- **RULES.md**, a new section **§ Downloadable Components**: the manifest as the one place for
  installed versions, the hard-coded pin as its fallback, a new downloadable component registered
  with its group and source (the emoji fonts being the next one), the staged swap for a locked file.
- **GLOSSARY.md / .fr.md**: *Component (composant)*, *Component updates window*, *Pinned version*,
  *Forced version*, *Components manifest*.
- **README.md / .fr.md**: the window in the features list; the network calls it makes in the technical
  paragraph next to the engines' downloads.

---

## Test Impact

**No unit test**: the repository has no test project (`CONTRIBUTING.md`: *check a change by hand in
the running app*), and this work creates none.

Checked by hand in the running app:

| Behaviour to check | How |
|---|---|
| Every installed and missing component is listed in its group, with its versions and dates | Open the window with some engines installed, some not |
| A spinner per row until its latest version arrives; *Couldn't check* offline | Open it online, then with the network cut |
| `components.json` recreated from the code's values when deleted, older installs recorded as pinned | Delete it, start, open the window |
| Update of one engine: downloaded, old files deleted, manifest updated, the engine still works | Update the face engine, run the face pass |
| Back to the pinned / forced version | Edit `forced` by hand, go back to it, then to the pinned one |
| A locked runtime: staged, *Restart to finish*, swapped at the next start; Restart now relaunches | Use the OCR, then update ONNX Runtime |
| yt-dlp updated through winget, its version read back | Update it with an older one installed |

---

## Open Questions

- [ ] What is a row: one per **engine** (the cutout engine's IS-Net and MobileSAM together), or one
  per **upstream** (IS-Net and MobileSAM on two rows, each with its own version and date)?
- [ ] The **native runtimes** (ONNX Runtime, SkiaSharp): updatable like the others, or shown with the
  version the build is compiled against and never updated (a native newer than its managed binding
  can crash)?
- [ ] The **winget tools** have no pin today: pin a version in the code (installed with
  `winget --version`), or have no pinned version for them (no *Back to the pinned version*)?
- [ ] **Files in use**: release the resident sessions and pause the content passes so most updates swap
  at once, staging only the native runtimes; or stage every file in use, the restart doing the rest?
- [ ] The **forced version** in the manifest: a version or commit alone (the files' URLs derived from
  it, their SHA-256 optional), or a full list of files with their URLs and hashes?
- [ ] A **pin changed in the code** (a new build) while the manifest holds the older one: the code wins
  unless the user edited `pinned`, or the manifest always wins?
- [ ] The **emoji fonts**: is their group part of this work (empty until
  `workfiles/20261102-emoji-fonts.md` is delivered, its ⚙ *update* entry replaced by this window), or
  left to that workfile entirely?
- [ ] The ⚙ menu's **Download the … engine…** entries: kept beside the window, or removed (the
  window's *Install* replacing them)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-02

Initial design from the request and the scoping batch (Q&A 1–4), after a mockup of three layouts:

- Layout A, one grouped list, in a modeless window opened from the ⚙ menu.
- The pinned components (the engines) are checked against their upstream and updated on demand; once
  updated, they can go back to the pinned version.
- The pinned versions move to an editable `components.json` next to the exe, recreated from the
  hard-coded values when missing. It also holds the installed version, its install and update dates,
  and an optional forced version as a safe fallback without rebuilding. The window shows all of it and
  goes back to the forced or the pinned version, the current version's files deleted once the other
  is downloaded.
- The check runs only when the window opens and on *Check now*: the *periodic* of the request is
  dropped.
- Exploration (one scout pass): the sources and identities of each component (§ What Exists), the
  files locked at run time, the ⚙ menu and the shortcuts window's pattern. The emoji fonts are designed
  only, and the Twemoji flag subset is embedded, so it is not a component.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project (§ Test Impact) |
| RULES.md | | | |
| README (+ .fr) | | | |
| GLOSSARY (+ .fr) | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which layout for the window (mockup: A one grouped list, B two panes, C a card per group)? | A — one grouped list | 2026-11-02 |
| 2 | The engines are pinned (fixed URLs + SHA-256): what does updating mean for them? | Check the upstream and offer to update; once updated, offer to go back to the project's pinned version. The pinned versions go into an editable `.json` file, to force a safe fallback version without rebuilding; recreated from the hard-coded values when missing. It also holds each component's install date, last update date and an optional forced version. The window's grid shows all of it and goes back to the forced or the hard-coded version, deleting the current version's files after the download | 2026-11-02 |
| 3 | When does the check run, and how is the user told? | Only when the window is open | 2026-11-02 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-11-02 |
| 5 | A row per engine, or per upstream? | | |
| 6 | The native runtimes: updatable, or tied to the build? | | |
| 7 | The winget tools: pin a version in the code, or no pinned version? | | |
| 8 | Files in use: release the sessions and swap at once, or stage everything in use? | | |
| 9 | The forced version: a version alone, or a full file list? | | |
| 10 | A pin changed in the code: which wins, the code or the manifest? | | |
| 11 | The emoji fonts' group: part of this work, or left to the emoji fonts workfile? | | |
| 12 | The ⚙ menu's *Download the … engine…* entries: kept or removed? | | |

---

*Last updated: 2026-11-02*
