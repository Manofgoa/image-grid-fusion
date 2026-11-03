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
| Emoji fonts | `Imaging/EmojiFonts` (`Font(EngineFile File, Family, Repository, Asset, Path)`), delivered on `main` by `workfiles/20261102-emoji-fonts.md` | GitHub releases: `mozilla/twemoji-colr` v0.7.0 (asset), `googlefonts/noto-emoji` v2026-09-24-unicode18_0 (file at the tag), `hfg-gmuend/openmoji` 17.0.0 (file at the tag), `C1710/blobmoji` v15.0 (asset); its update takes `releases/latest`, no hash | The release tag in the pin | `emoji-fonts\` |
| Twemoji flag subset | Embedded in the exe (`Imaging/TwemojiFlags.ttf`, RULES.md § Text Emojis) | Built once | Fixed at build time | **Not a component**: never downloaded |

- The emoji fonts are **loaded from memory**, one collection per font, so their files are never held
  open: an update replaces them at once. After a download or an update the Emoji drop-down is read again
  and the text layers drawn again (`EmojiFonts.Generation`, `GridPreview.SetLayers`). They are offered at
  the **first launch** while none is there (a **No** remembered, `AppSettings.EmojiFontsDeclined`), and
  from the ⚙ menu's *Download the emoji fonts…* and *Update the emoji fonts*.

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
- **A component is one upstream** (Q&A 5): a Hugging Face repository, a GitHub release asset, a NuGet
  package, a winget package — each with its own version and date. An engine made of several takes
  several rows, named after it:

  | Group | Rows |
  |---|---|
  | **Engines** | OCR engine — RapidOCR models (NuGet `rapidocrnet`) · Face engine — YuNet · Face engine — SFace · Vision engine — SigLIP 2 models (`onnx-community/siglip2-base-patch16-256-ONNX`) · Vision engine — SigLIP 2 tokenizer (`google/siglip2-base-patch16-256`) · Caption engine — Florence-2 · Cutout engine — IS-Net · Cutout engine — MobileSAM |
  | **Runtimes** | ONNX Runtime · SkiaSharp |
  | **Web tools** | yt-dlp · Deno · ffmpeg |
  | **Emoji fonts** | Twemoji Mozilla · Noto Color Emoji · OpenMoji · Blobmoji |

  An engine is installed when all its rows are; *Install* is offered on each missing row.
- **What each component is for** (Iteration 5): an **ℹ️ icon right after its name**, its tooltip
  saying what the component does in the app and which features need it, then its upstream and its
  license. One text per component, kept with its registration in the code:

  | Component | Tooltip's purpose line |
  |---|---|
  | OCR engine — RapidOCR models | Reads the text in images and PDF pages for the content search (💡), after Windows OCR |
  | Face engine — YuNet | Finds the faces in the indexed images, for the People view |
  | Face engine — SFace | Tells the faces apart, to group them into people |
  | Vision engine — SigLIP 2 models | Sees what the images show: their tags, the similarity search under 💡, Find similar images |
  | Vision engine — SigLIP 2 tokenizer | Turns a search's words into what the SigLIP 2 text model reads |
  | Caption engine — Florence-2 | Writes a sentence describing each image, for the search under 💡 |
  | Cutout engine — IS-Net | Finds an image's main subject, for the Cutout's Subject mode |
  | Cutout engine — MobileSAM | Picks the object under a click, for the Cutout's Pick mode |
  | ONNX Runtime | Runs every engine's models; shared by all of them |
  | SkiaSharp | Decodes the images for RapidOCR |
  | yt-dlp | Downloads the media of a URL the other resolvers do not know (URL zone) |
  | Deno | Lets yt-dlp read YouTube's formats |
  | ffmpeg | Joins a video track and its sound into one MP4 for yt-dlp |
  | Twemoji Mozilla, Noto Color Emoji, OpenMoji, Blobmoji | Draws a text layer's emojis in that font's style, picked in the Layers options' Emoji drop-down |

  The ℹ️ is a Segoe UI Emoji glyph drawn by the app, as the tile tooltip's icons (RULES.md § Icons).
- **Columns**:

  | Column | Shows |
  |---|---|
  | Component | Its name (`Vision engine — SigLIP 2 models`), then the ℹ️ of its purpose (below) |
  | Installed | The installed version, `—` when not installed |
  | Installed on | When it was first installed |
  | Updated on | When it was last replaced, `—` if never |
  | Pinned | The version the app's code pins (the hard-coded value) |
  | Forced | The forced version of the manifest, `—` when none |
  | Latest | The latest upstream version; a **spinner** while it is being checked; *Couldn't check*, its reason in the tooltip, when it failed |
  | Released | The latest version's date, `—` when the source gives none |
  | Size | What its files take on the disk, after *Installed* (Iteration 21) |
  | Actions | **Update** while the latest differs from the installed, **Install** while not installed, and a **▾** menu: *Back to the forced version*, *Back to the pinned version* (each only when it differs from the installed) |

- **The check** runs **when the window opens** and on **Check now**: every row at once, each row's
  spinner stopping on its own answer. Nothing runs in the background, and nothing runs while the
  window is closed. The time of the last check is shown at the top.
- **A progress bar per row** (Iteration 8), redrawn by the spinner's timer, the list double-buffered (Iteration 15): while a component downloads or installs, its Actions cell
  shows a bar — its share done, the megabytes received over the total (`12 / 48 MB`), then
  *Installing…* while its files are swapped — and a **×** cancelling that row alone, its staging folder
  deleted, the installed version untouched. A winget install, which tells no size, shows an
  indeterminate bar.
- **Several components at once, the UI never blocked** (Iteration 8): *Update*, *Install* and the
  *Back to…* entries of several rows can run **in parallel**, each on its own bar, the window and the
  main window staying usable meanwhile. The work runs off the UI thread; only the bars are updated on
  it. Within that:
  - a row already running takes no second action;
  - a file several rows need (ONNX Runtime, needed by every engine) is downloaded **once**, the rows
    waiting for it showing *Waiting for ONNX Runtime…*;
  - the winget installs run **one at a time**, winget refusing concurrent installs; a queued one shows
    *Waiting for winget…*.
- **Update all (N)** at the bottom starts every row whose latest version differs from the installed
  one, in parallel as above; **Install all (N)** beside it every row not installed, at its forced version,
  else its pinned one (Iteration 16). The window is a grid of three rows: Check now, the list, the buttons
  at the right (Iteration 17).
- **Closing the window while a row runs asks for confirmation**: *N downloads are running. Cancel them
  and close?* — **Yes** cancels them all (staging folders deleted, installed versions untouched) and
  closes; **No** keeps the window open, the downloads going on. The same question is asked when the app
  exits with the window running one.
- **Every install and update goes through the window** (Iteration 4): the ⚙ menu's *Download the …
  engine…* entries and *Install yt-dlp companions…* are removed (Q&A 12, 13), and the in-context offers
  — the Cutout turned on in a mode needing its engine, the content search, the face analysis, the
  visual analysis, the captions, the URL zone's yt-dlp and companions offers — **open the window**
  instead of downloading themselves. No download or winget install runs anywhere else.
- **The emoji fonts too** (Iteration 9): the ⚙ menu's *Download the emoji fonts…* and *Update the
  emoji fonts* are removed; the **first-launch offer** opens the window on the four font rows, its
  balloon saying what they are for — a **No** still remembered (`EmojiFontsDeclined`), the window then
  not opened at the next launch. Their update is a row's *Update* like any other; once a font row is
  swapped, the Emoji drop-down and the text layers are refreshed as today (`EmojiFonts.Generation`).
  Their files are never locked: swapped at once.
- **Opened by an in-context offer** (Q&A 14): the window shows every component as usual, the rows the
  feature needs **highlighted**, the first scrolled into view, and a **balloon tooltip open on it**
  naming the component to install and why it is wanted — *The Cutout's Subject mode needs the cutout
  engine — IS-Net to find the image's main subject.* — built from the feature asking and the
  component's purpose. Nothing installs by itself: the user clicks *Install*.
- **The feature that asked goes back to off at once** (Q&A 15), as the window opens: the Cutout's
  checkbox unchecked, the analysis setting off, the URL load ended — as a **No** does today; the user
  turns it on again once the component is installed. **The content search aside** (Iteration 11): it
  runs with Windows OCR without RapidOCR, so it stays on, as a **No** left it.
- **Install** installs the **forced version when there is one, else the pinned one** — never the latest,
  which *Update* offers once installed (Iteration 11). An engine installed without ONNX Runtime starts the
  runtime's row at the same time.
- The ℹ️ is drawn **before the name**, aligned from row to row (Iteration 14). Its tooltip is the **row's** tooltip: hovering anywhere on the row shows the purpose, the upstream,
  the license, and a failure's reason (Iteration 11).
- **The columns follow the window** (Iteration 12): resized, the list's columns share its width in
  proportion to their opening widths, the Actions column keeping its own; they always add up to the
  list's usable width, so no horizontal scroll bar shows (Iteration 13).
- No shortcut is added but Escape closing the window, as in the shortcuts window (`SHORTCUTS.md`
  § Dialogs).

---

## Components Manifest

`components.json`, **next to the exe**, read and written through one class (`Components/ComponentManifest`):

- **The hard-coded values stay in the code**: each component's **pinned version** and its files
  (URLs, SHA-256, sizes) as today. They are the fallback when the file is missing or unreadable.
- **The file holds, per component** (keyed by a stable id: `vision-engine`, `yt-dlp`…):

  | Key | Meaning |
  |---|---|
  | `pinned` | The pinned version and its files, **a copy of the code's**: rewritten from it at every start (Q&A 10), so a new build's pin always wins |
  | `forced` | Optional, **the user's**: the safe fallback, without rebuilding the app — a full list of files (Q&A 9), `{ "version", "files": [{ "name", "url", "sha256", "size" }] }`, `size` optional; for a winget tool `{ "version" }` alone, winget fetching it. Never written by the app |
  | `installed` | The version installed now |
  | `installedOn` | When it was first installed |
  | `updatedOn` | When it was last replaced |
  | `files` | The installed version's files with their SHA-256, as downloaded |

- **Missing → recreated** from the hard-coded values. A component whose files are already there (an
  install older than this feature) is recorded as the **pinned version**, installed on its files'
  creation date.
- **The code always wins for `pinned`**: rewritten at every start by the normal instance, a hand edit
  of it lasting until the next start only. Imposing a version of one's own is `forced`'s job.
- **A forced version is checked against its own hashes**, written with it: a file whose SHA-256
  differs is refused, the installed version kept.
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
- **The winget tools get a pinned version in the code** (Q&A 7), the one current when this work is
  implemented: a first install and *Back to the pinned version* run `winget install … --version <pin>`,
  so they go back to a version known to work like the engines do.
- **The runtimes are updated like the others** (Q&A 6), from NuGet, their pin the version
  `ImageGridFusion.csproj` references. A native newer than the managed binding compiled in the exe may
  fail at its first use: the window loads no runtime, so it stays open to go back to the pinned version.
  The status line names the runtime when an engine fails to start after such an update.
- **An update to the latest** has no hash pinned in the code: the files are checked against the hash
  the source gives when it gives one (Hugging Face, NuGet's package), and their SHA-256 is **recorded
  in the manifest** after the download in every case.
- The checks need no account. GitHub's anonymous limit (60 requests an hour) is far above one check
  per opening.

---

## Updating

- **Download first, then swap**: the new version is downloaded into a staging folder next to the
  component's own (`<folder>\update-staging\<id>\`), its files checked. Then the current version's files are
  **deleted** and the new ones moved into place, and the manifest is updated (`installed`, `updatedOn`,
  `files`). A failure or a cancel deletes the staging folder and leaves the installed version untouched.
- **Back to the forced or pinned version** goes the same way: the target version downloaded, then the
  current files deleted.
- **A file in use is released, then replaced at once** (Q&A 8): before the swap, the resident sessions
  of the engine updated are closed — the cutout engine (`CutoutService`, its requests waiting), the
  vision text model (`ReleaseTextEncoder(force: true)`) — and the content passes are paused, a running
  pass closing its sessions; all of it resumes once the files are in place, the sessions reopening at
  their next use. One release route per resident engine, called by the update.
- **What stays locked** — the native runtimes once loaded — is **left staged**: the swap is done at the
  next start, in `Program.Main` **before anything loads them**, and the row says *Restart to finish*. A
  runtime not loaded yet in this process is swapped at once.
- **A winget tool** is installed by winget into its own folder, the folder there set aside as
  `<folder>-previous` meanwhile and given back if winget fails (`winget install --version <v> --force`).
  A tool **in use** (a URL load running) cannot be set aside: its row says so — *try again once the URL
  load ends* — rather than staging it, since winget installs into the folder itself (Iteration 11).
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
  lock — the old one's id given in the environment variable `IMAGE_GRID_FUSION_WAIT_FOR_PID`, no new
  command-line argument (Iteration 11) — then closes the app as its tray *Exit* does.
- **Later**: the swaps are done at the next start whenever it comes.

---

## Rules and Docs

- **RULES.md**, a new section **§ Downloadable Components**: the manifest as the one place for
  installed versions, the hard-coded pin as its fallback, a new downloadable component registered
  with its group and source, the release route of a resident engine, the staged swap for a locked file.
  The sections saying an engine is offered "from the ⚙ menu while missing" (§ The Cutout Exception,
  § Content OCR Passes, § People, § Image Content Analysis), and § Text Emojis' emoji fonts downloaded
  and updated from the ⚙ menu, say the Component updates window instead.
- **The branch first takes `main`**: this worktree was cut before the emoji fonts were merged
  (`c695d80`); the implementation starts by bringing it up to date with `main`.
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

- [x] ~~What is a row: one per engine, or one per upstream?~~ → One per upstream, named after its engine
- [x] ~~The native runtimes: updatable, or tied to the build?~~ → Updated like the others
- [x] ~~The winget tools: pin a version in the code, or no pinned version?~~ → A version pinned in the code
- [x] ~~Files in use: release and swap at once, or stage everything in use?~~ → Released, then swapped
  at once; only what stays locked is staged
- [x] ~~The forced version: a version alone, or a full file list?~~ → A full list of files with their
  URLs and hashes (a winget tool: its version alone)
- [x] ~~A pin changed in the code: which wins?~~ → The code, always: `pinned` rewritten at every start
- [x] ~~The emoji fonts: is their group part of this work, or left to `workfiles/20261102-emoji-fonts.md`?~~
  → Waited for the emoji fonts session (Q&A 11); delivered, their four fonts are a group of the window,
  installed and updated through it like every component (Iteration 9)
- [x] ~~The ⚙ menu's Install yt-dlp companions… entry: removed too, or kept?~~ → Removed too
- [x] ~~An in-context offer now opens the window: what does the window show?~~ → Every component,
  the needed rows highlighted with a balloon tooltip naming the component and why it is wanted
- [x] ~~The feature that asked: back to off at once, or waiting and resuming by itself once the
  component is installed?~~ → Back to off at once (Q&A 15)

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

### Iteration 2 — 2026-11-02

The open questions answered (Q&A 5–12):

- A row per upstream: the engines made of several models take several rows (§ Components Window).
- The native runtimes are updated like the others, the window staying usable to go back if one fails.
- The winget tools get a version pinned in the code, installed with `winget --version`.
- Files in use are released and swapped at once; only the native runtimes once loaded and a tool in
  use are staged for the next start.
- The forced version is a full list of files with their hashes, a winget tool's its version alone.
- The code always wins for `pinned`, rewritten at every start: a hand edit of `pinned` no longer lasts,
  `forced` being the user's (this narrows Q&A 2's "editable" to the forced version).
- The ⚙ menu's *Download the … engine…* entries are removed, the window's *Install* replacing them.
- The emoji fonts' group waits for the session implementing the emoji fonts. A new question: the
  ⚙ menu's *Install yt-dlp companions…* entry.

### Iteration 3 — 2026-11-02

Q&A 13: the ⚙ menu's *Install yt-dlp companions…* entry is removed too, the window's *Install* on
Deno and ffmpeg replacing it. One question stays open: the emoji fonts' group, waiting for the
session implementing the emoji fonts.

### Iteration 4 — 2026-11-02

User request: every install and update of a component goes through the new window. Beyond the ⚙
entries already removed, the in-context offers (an effect or an analysis turned on without its
engine, the URL zone's yt-dlp and companions offers) open the window on the rows they need rather
than downloading themselves. New question: how the feature that asked goes on (Q&A 14).

### Iteration 5 — 2026-11-02

User request: show what each component is for, for instance in a tooltip next to its name. An ℹ️
icon after the name, its tooltip giving the component's purpose and the features needing it, then
its upstream and license (§ Components Window).

### Iteration 6 — 2026-11-02

Q&A 14: an in-context offer opens the window with every component, the needed rows highlighted and a
balloon tooltip open on the first, naming the component and why the feature wants it; nothing installs
by itself. The feature that asked goes back to off, as a *No* does today — the agent's reading, left
open for confirmation.

### Iteration 7 — 2026-11-02

Q&A 15 confirms it: the feature that asked goes back to off as soon as the window opens, and is turned
on again by the user once the component is installed. One question stays open: the emoji fonts' group.

### Iteration 8 — 2026-11-02

User request: a progress bar per row on its download / install; several components downloaded or
installed in parallel without blocking the UI; a confirmation when the window is closed during a
download. This replaces *Update all* running the rows one after the other, and *Close* leaving the
running updates going on. The agent added: a × per row to cancel it alone, ONNX Runtime downloaded
once for the rows sharing it, the winget installs queued one at a time (winget refuses concurrent
installs), and the same confirmation when the app exits with downloads running.

### Iteration 9 — 2026-11-02

The emoji fonts session delivered (`main`, `c695d80`): four fonts pinned in `Imaging/EmojiFonts` —
Twemoji Mozilla, Noto Color Emoji, OpenMoji, Blobmoji — from GitHub releases, loaded from memory,
offered at the first launch and downloaded or updated from two ⚙ entries. Following Iteration 4
(every install and update through the window): they are the *Emoji fonts* group, the two ⚙ entries
removed, the first-launch offer opening the window on their rows. The branch takes `main` first.
No open question remains.

### Iteration 10 — 2026-11-02 — ✅ Implemented

Go given: code, unit tests and documentation, in the current worktree
(`claude/component-update-verification-4c9318`), brought up to date with `main` first.

### Iteration 11 — 2026-11-02 — 🧭 Implementation choices

Divergent choices of the run, each in the domain sections above:

- ⚠️ **Rule broken — a commit touching a key handler updates `SHORTCUTS.md` in the same commit**
  (RULES.md § Keyboard Shortcuts): the window's Escape (`ComponentsWindow.ProcessCmdKey`) went in the
  code commit, its row in a separate one right after. Why: the docs were committed apart, by step; noticed
  afterwards. Autonomous run: reported instead of asked.
- **The face models pinned to a commit** (`opencv/face_detection_yunet` 3cc26e7f, `face_recognition_sface`
  3d708243) instead of `main`: the same files and hashes, now a real pinned version. Their pinned label
  keeps the date in their file name (`2023mar`, `2021dec`); the others show their commit (8 characters),
  their NuGet version, their release tag or their winget version.
- **The winget tools' pins**: yt-dlp 2026.10.26, Deno 2.9.8, ffmpeg N-127043-g5a54fcf75e-20260930 —
  winget's latest on 2026-11-02.
- **Install takes the forced version, else the pinned one**; Update the latest. An engine installed
  without ONNX Runtime starts the runtime's row at the same time, rather than a *Waiting for ONNX
  Runtime…* on its own row.
- **The content search stays on** when the OCR engine is missing: it works with Windows OCR alone.
- **A winget tool in use is refused, not staged**: winget installs into the folder itself.
- **The restart's wait** goes through an environment variable, not a command-line argument: RULES.md
  § Command-Line Arguments keeps its four entries.
- **A `--new-instance` finishes the swaps pending** at its start, and writes `components.json` on a
  user's install: they complete a user action, not a write of its own accord.
- **The row's tooltip** carries the purpose, not the ℹ️ alone (WinForms' item tooltips).
- **The rows' buttons are drawn** in the list (owner-drawn `ListView`, `MouseClick`), the progress bar
  with the visual styles' renderer; an indeterminate bar slides for winget.
- **A Hugging Face component** whose files match the latest commit's is up to date, its Latest cell
  showing the commit with a ✓, though its pinned label is a date or an older commit.
- **What the start says**: a manifest unreadable, or the components swapped at the start, in the status
  line once the window shows.
- **Removed with the old downloads**: the engines' `DownloadAsync`, `EmojiFonts.DownloadAsync` /
  `UpdateAsync`, the URL zone's install confirmations (`InstallingStep`, `CompanionsInstalledException`), the
  ⚙ entries and their visibility code.
- **The checks by hand ran in a scratchpad harness** compiling the app's sources with an entry point of its
  own, in its own folder: the window opened, the 17 latest versions read for real (ONNX Runtime 1.31.1
  offered over 1.29.0), YuNet and Twemoji installed with ONNX Runtime started alongside, a locked file left
  staged then swapped as at the next start, `components.json` written. The rows' clicks were sent to the
  handler directly: driving the running app from outside failed (UI Automation sees none of its tree), and
  the real mouse was left alone. **Not run**: a winget install (nothing installed on this machine), a
  restart, the app's own offers — left to the delivery test.
### Iteration 12 — 2026-11-03 — ⚙️ Post-implementation — Columns follow the window

User request: when the window is resized, its content adapts to it. The list's columns share its width as
it changes — each in proportion to its width at the window's opening, the Actions column keeping its
own so its buttons and progress bar never shrink —, the list itself filling the window as before.
Done: the columns refitted on every resize of the list (`ComponentsWindow.FitColumns`), a name too long cut
before its ℹ️ rather than under it; checked in the harness at 1 100 and 1 900 px wide.

### Iteration 13 — 2026-11-03 — ⚙️ Post-implementation — No horizontal scroll bar

User feedback on the delivered window: the content must take all the width available, and no scroll bar
shows when none is needed — a horizontal one showed under the list. The columns are fitted again whenever
the list's usable width changes, the vertical scroll bar appearing after the window opened included, so
the columns always add up to the list's width exactly: no horizontal scroll bar, no gap at the right.

### Iteration 14 — 2026-11-03 — ⚙️ Post-implementation — The ℹ️ before the name

User request: the help icon on the left, aligned from row to row. The ℹ️ is drawn at the start of the
Component cell, the name after it.

### Iteration 15 — 2026-11-03 — ⚙️ Post-implementation — No flicker while downloading

User feedback: the window is very slow and flickers everywhere during a download. The list is drawn
double-buffered; a row's progress is only stored as it comes, and the rows running are redrawn by the
spinner's timer (80 ms), never by each report; the installer reports a zip entry's progress at most every
100 ms, like a direct download.

### Iteration 16 — 2026-11-03 — ⚙️ Post-implementation — Install all

User request: beside Update all, an **Install all**. It starts every row not installed, each at its forced
version, else its pinned one, in parallel like Update all; its count in its text, disabled at 0.

### Iteration 17 — 2026-11-03 — ⚙️ Post-implementation — The space lost at the top

User feedback: at the top of the window a space is lost and a strange black band shows. The bottom row of
buttons was laid out at the top, unpainted, by the docked panels. The window is now a grid of three rows —
Check now, the list taking the rest, the buttons at the right — each in its place whatever the DPI.

### Iteration 18 — 2026-11-03 — ⚙️ Post-implementation — Reading, a step of the bar

User feedback: *Reading the package…* must be a step of the progress bar, not a greyed button. It is drawn
as an indeterminate bar, sliding, until the download starts.

### Iteration 19 — 2026-11-03 — ⚙️ Post-implementation — Check now without flicker

User feedback: clicking Check now makes everything flicker; it refreshes too often. A row's cells are
rewritten only when their text changes, and the list is redrawn only where a row changed, never whole.

### Iteration 20 — 2026-11-03 — ⚙️ Post-implementation — Resizing at once

User feedback: resizing the window is very slow, and should not be. The columns are fitted with the list's
drawing suspended (one redraw, not one per column), once per resize.

### Iteration 21 — 2026-11-03 — ⚙️ Post-implementation — Uninstall, and the size on disk

User request: let the downloaded content be uninstalled — deleted — and show the size of the component's
files or folder.

- A **Size** column after *Installed*: what the component's files take on the disk — its installed files in
  its folder, a winget tool's whole folder —, `—` when not installed.
- **Uninstall** in the row's **▾** menu, once installed: a confirmation naming the size freed, then what
  holds the files released, its files deleted — a tool's folder —, the manifest's installed version and dates
  cleared, the features using it refreshed. A file still locked (a native runtime loaded) is left: the row
  says so, a restart freeing it.

### Iteration 22 — 2026-11-03 — ⚙️ Post-implementation — Open in Explorer

User request: a folder icon opening the folder holding the component — a subfolder or a file — to look at
it in Explorer. A **📂** at the right end of the Actions cell of every component installed: clicked,
Explorer opens on its folder, its first file selected — a winget tool's folder selected in its parent.

### Iteration 23 — 2026-11-03 — ⚙️ Post-implementation — No Close button

User feedback: the Close button is useless, Windows' own × does it. Removed; the × and Escape close the
window, asking first while a download runs.

### Iteration 24 — 2026-11-03 — ⚙️ Post-implementation — Hang at the smallest width

User report: resized to its smallest, the window stopped responding. The columns could not shrink below
half their opening widths, so their total never matched the list's width, and the check made after each
fit (Iteration 13) posted itself again forever. The check now runs only when the list's width changed
since the last fit, and the columns shrink down to a floor of their own, a horizontal scroll bar showing
only below it.
Done: checked in the harness at the window's minimum width — responsive (a 500 ms wait took 503 ms), no
horizontal scroll bar.

### Iteration 25 — 2026-11-03 — ⚙️ Post-implementation — Uninstall all

User request: an **Uninstall all** button. Beside Install all and Update all, its count in its text,
disabled at 0: one confirmation naming the components and the size freed, then every row installed and not
running uninstalled as its own Uninstall does (Iteration 21) — what holds the files released once, a file
still locked left, its row saying a restart frees it.
Done: `ComponentsWindow.UninstallAllAsync`; checked in the harness — two components installed, both removed,
each kind released once.

### Iteration 26 — 2026-11-03 — ⚙️ Post-implementation — The name, a link to its page

User request: the component's name clickable, opening its GitHub repository, its site or its winget page.
The name is drawn as a link — the link color, underlined under the mouse, the hand cursor — and opens in
the browser the upstream's page: the Hugging Face repository, the GitHub repository, the NuGet package's
page; for a winget tool, its project's page — yt-dlp's and its FFmpeg builds' repositories, Deno's site —
winget having no page of its own for a package.

### Iteration 27 — 2026-11-03 — ⚙️ Post-implementation — The total size

User request: at the very bottom, the sum of the sizes. The bottom row shows at its left *Total on disk:
N MB* — the Size column's values added up — kept up to date after every install, update or uninstall.
Done (with Iteration 26): `ComponentSource.PageUrl`, the name drawn in the link color and hit-tested on its
text; *Total on disk* at the bottom left. Checked in the harness — the hover underlines, the 17 pages right.

### Iteration 28 — 2026-11-03 — ⚙️ Post-implementation — A window showing everything

User request: a larger default size, so that everything shows at once — as in their capture, every row and
every column. The window opens as wide as its columns at their opening widths, and as tall as its rows —
measured once the list is laid out —, both within the screen's working area, centered on the main window.
Done: the opening width read from the columns' opening widths — a first fit, before the window had its size,
had shrunk them, so the window opened too narrow — and the height grown to the last row (`FitHeight`). Checked
in the harness: every row and column shown, no scroll bar.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 10 | 2026-11-02 | `Components/` (registry, sources, manifest, installer), `UI/ComponentsWindow`, `UI/MainForm.Components`; the engines' and fonts' own downloads and the ⚙ download entries removed |
| Unit tests | 10 | 2026-11-02 | Declined by the design: no test project (§ Test Impact). Checked in a scratchpad harness instead (Iteration 11) |
| RULES.md | 10 | 2026-11-02 | § Downloadable Components; the engines', fonts' and yt-dlp's offers point to the window |
| README (+ .fr) | 10 | 2026-11-02 | § Components / § Composants; the offers' passages |
| GLOSSARY (+ .fr) | 10 | 2026-11-02 | Component, Component updates window, pinned / forced version, components manifest |
| SHORTCUTS (+ .fr) | 10 | 2026-11-02 | Escape closes the window (§ Dialogs) |
| Adjustments | 12–23 | 2026-11-03 | Code (columns, flicker, layout, Install all, Uninstall, size, Explorer, no Close); RULES, README and GLOSSARY (+ .fr) updated; checked in the harness at 1 100 and 1 900 px, uninstall and size included |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which layout for the window (mockup: A one grouped list, B two panes, C a card per group)? | A — one grouped list | 2026-11-02 |
| 2 | The engines are pinned (fixed URLs + SHA-256): what does updating mean for them? | Check the upstream and offer to update; once updated, offer to go back to the project's pinned version. The pinned versions go into an editable `.json` file, to force a safe fallback version without rebuilding; recreated from the hard-coded values when missing. It also holds each component's install date, last update date and an optional forced version. The window's grid shows all of it and goes back to the forced or the hard-coded version, deleting the current version's files after the download | 2026-11-02 |
| 3 | When does the check run, and how is the user told? | Only when the window is open | 2026-11-02 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-11-02 |
| 5 | A row per engine, or per upstream? | One per upstream | 2026-11-02 |
| 6 | The native runtimes: updatable, or tied to the build? | Updated like the others | 2026-11-02 |
| 7 | The winget tools: pin a version in the code, or no pinned version? | Pin a version in the code | 2026-11-02 |
| 8 | Files in use: release the sessions and swap at once, or stage everything in use? | Release, then replace at once | 2026-11-02 |
| 9 | The forced version: a version alone, or a full file list? | A full list of files | 2026-11-02 |
| 10 | A pin changed in the code: which wins, the code or the manifest? | The code, always | 2026-11-02 |
| 11 | The emoji fonts' group: part of this work, or left to the emoji fonts workfile? | Wait for the other session | 2026-11-02 |
| 12 | The ⚙ menu's *Download the … engine…* entries: kept or removed? | Removed | 2026-11-02 |
| 13 | The ⚙ menu's *Install yt-dlp companions…* entry: removed too, or kept? | Removed too | 2026-11-02 |
| 14 | An in-context offer opens the window: what happens to the feature that asked? | The window opens with everything, the row in question highlighted, with a tooltip open or a popup naming the component to download and why it is wanted | 2026-11-02 |
| 15 | The Cutout checked without its engine, the window opened: does its checkbox stay checked and cut out once installed, or uncheck at once? | Unchecked at once | 2026-11-02 |

---

*Last updated: 2026-11-02*
