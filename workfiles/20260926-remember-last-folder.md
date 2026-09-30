# Remember Last Folder

> Working document — remember, between two launches of the app, the last folder used to load
> files, and the last folder used to export.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, every launch opens the file pickers on a folder chosen by Windows: the user browses back to
the same folder each time. The app should remember the **last folder used** and open its pickers
there, **across launches**.

Components concerned:

| Component | Role today |
|---|---|
| `UI/MainForm.cs` — `PickFiles()` | Open dialog *Add images* (multiselect): every kind of content a cell accepts — images, videos, PDF, text. Sets no `InitialDirectory` |
| `UI/MainForm.cs` — `BrowseSoundtrack()` | Open dialog *Choose a soundtrack* (audio or video file). Sets no `InitialDirectory` |
| `UI/MainForm.cs` — `SaveAs()` | Save dialog of the exports — PNG, MP4 or GIF. Opens on `DefaultSaveFolder()`: the folder of the first loaded image's file, else *My Pictures* |
| `UI/MainForm.cs` — `SaveLastVideo()` | Save dialog of **Save last…** — copies the last generated MP4 / GIF. Opens on `DefaultSaveFolder()` too |
| `UI/MainForm.cs` — `PickExplorerFolder()` | Folder dialog of the file explorer's base folder — already remembered, as that setting itself. Not concerned |
| `UI/StartupRegistration.cs` | *Start with Windows*: a per-user value in the `HKCU\…\Run` key launching the exe hidden in the tray (`TrayApplicationContext.HiddenArgument`); also hosts `IsRegistryError`, used by `AppSettings` |
| `RULES.md` § Global Effects | Says the Borders' color is "stored in the registry (`UI/AppSettings.cs`)" |
| `UI/AppSettings.cs` | The app settings remembered between sessions, per user, in the **registry** (`HKCU\Software\ImageGridFusion`) — *no settings file*: border color, Twitter corners, explorer folder / panel / width / tile size / pages per load, window size. `RULES.md` § Global Effects names it as the app settings' store |
| `README.md` | Documents the features |

Project: WinForms, `net10.0-windows10.0.19041.0`.

---

## Remembered Folders

Agreed:

- **What updates it**: only a file picked through a **browse dialog** (the dialog closed with *Open*
  / *Save*). A drop from the Explorer and a Ctrl+V of a file **do not** change it. A cancelled
  dialog changes nothing.
- **One remembered folder per dialog**, not one shared by every dialog. Inside a dialog, every
  kind of content (images, videos, PDF, text) shares its folder. A future picker — e.g. the
  soundtrack's — gets a folder of its own.
- **Exports are concerned too**, with a **folder of their own**, separate from the loading one.
- **Export folder**: today's rule (folder of the first loaded image's file, else *My Pictures*)
  still applies **until a first export** has been made; from then on, the export dialog always opens
  on the last export folder.
- **Storage**: the app's settings file, `settings.json` next to the `.exe` — see § Settings File.
- **Written as soon as a dialog closes** with a file picked, so a crash never loses it.
- ***Choose a soundtrack*** has a **folder of its own**, separate from *Add images*.
- **Save last…** **shares the exports' folder**: it opens on it and updates it.
- **Missing folder** (deleted, USB drive unplugged): the dialog opens on the **nearest existing
  parent** of the remembered folder; when none exists, on the dialog's default folder (Windows'
  choice for the open dialog, today's rule for the export dialog).

| Dialog | Remembered folder | Updated by |
|---|---|---|
| Open — *Add images* (`PickFiles`) | Last loading folder | The folder of the files picked |
| Open — *Choose a soundtrack* (`BrowseSoundtrack`) | Last soundtrack folder | The folder of the file picked |
| Save — export PNG / MP4 / GIF (`SaveAs`) | Last export folder | The folder of the file saved |
| Save — **Save last…** (`SaveLastVideo`) | Last export folder (shared) | The folder of the file saved |

---

## Settings File

Agreed — the registry in `AppSettings` was never wanted:

- **Every app setting** lives in **`settings.json`** (JSON), **next to the `.exe`**
  (`AppContext.BaseDirectory`, like `files.index` and `favorites.txt`). The app **never uses the
  registry**.
- **In this workfile**, `AppSettings` moves off the registry into that file — border color, Twitter
  corners by default, explorer folder / panel open / width / tile size / pages per load, window
  size — with the remembered folders added to it. Its readers keep their defaults and clamps; a
  missing or unreadable file gives the defaults.
- **Migration, once** (`UI/RegistryMigration.cs`, run first by `Program.Main`): when
  `HKCU\Software\ImageGridFusion` exists, each of its values the file does **not have yet** is copied
  into it, then the key is deleted — the file's own values win. On failure nothing is deleted and the
  next start-up tries again. The obsolete values (`ExplorerColumns`, `ExplorerSpan`) go with the key.
  This is the registry's last use.
- **Each copy of the exe has its own file** (Debug, Release, published): the first copy launched
  takes the registry's values, the others start from the defaults.
- **Writes**: each save reads the file again, sets its values and rewrites it whole, through a
  `.tmp` file moved over it — another instance's saves are kept, a failure never leaves half a file.
  A failed save says so in the status line (`… not remembered`), like the other settings.
- ***Start with Windows***: the `Run` value is replaced by a **shortcut in the user's *Startup*
  folder** (`shell:startup`), `ImageGridFusion.lnk`, launching the exe with the same hidden argument
  (written through `IShellLinkW`). The migration turns an existing `Run` value into that shortcut,
  then deletes the value. At each start-up an existing shortcut is rewritten to point at the exe
  launched, as the `Run` value was.
- **Rule**: "settings in `settings.json` next to the `.exe`, never the registry" is written into
  `RULES.md` at implementation, and its registry mention (§ Global Effects) is fixed; the other
  running sessions are told.

---

## Test Impact

**None.** The repository has no test project (`src/` holds `ImageGridFusion` only), and the user
chose to ship this work **without unit tests**, like every previous workfile (Q&A #10): it is
verified by hand in the app — pick files, restart, reopen the dialog; same for an export; rename
the remembered folder and check the parent is used.

---

## Open Questions

- [x] ~~"One folder per kind": the app has a **single** open dialog today, for every kind of cell
  content. Does it mean one folder for that dialog (a future picker — e.g. the soundtrack's — getting
  its own), or one per content kind (images / videos / PDF / text) inside it?~~ → One per dialog
- [x] ~~Export dialog: does the remembered export folder **replace** today's rule (folder of the first
  loaded image, else *My Pictures*), or does that rule still apply until a first export has been
  made?~~ → Today's rule until a first export, the last export folder afterwards
- [x] ~~Storage: a JSON settings file under `%AppData%\ImageGridFusion\`, or the registry
  (`HKCU\Software\ImageGridFusion`), next to the *Start with Windows* value?~~ → JSON file,
  `%AppData%\ImageGridFusion\settings.json`
- [x] ~~When is it written: as soon as a dialog closes with a file picked, or when the app
  closes?~~ → As soon as the dialog closes with a file picked
- [x] ~~The remembered folder no longer exists (deleted, USB drive unplugged): nearest existing parent
  folder, or the dialog's default folder?~~ → Nearest existing parent, else the dialog's default
- [x] ~~Unit tests: none, verified by hand like every previous workfile, or a first test
  project?~~ → None, verified by hand
- [x] ~~Storage, now that `UI/AppSettings.cs` keeps the app settings in the registry: move to it, or
  keep the JSON file decided in Q&A #7?~~ → A settings file next to the `.exe`, never the registry
- [x] ~~*Choose a soundtrack*: a remembered folder of its own, shared with *Add images*, or not
  concerned?~~ → A folder of its own
- [x] ~~**Save last…**: shares the exports' folder, a folder of its own, or not concerned?~~ → Shares
  the exports' folder
- [x] ~~The existing `AppSettings` values (border color, Twitter corners, explorer settings, window
  size): moved to the file in this workfile, or in a workfile of their own?~~ → In this workfile
- [x] ~~Values already saved in the registry: migrated once into the file (then the key deleted), or
  left behind (defaults on first launch)?~~ → Migrated once, then the key deleted
- [x] ~~*Start with Windows* needs the `HKCU\…\Run` value: replaced by a shortcut in the user's
  *Startup* folder, kept as the one exception, or removed?~~ → A shortcut in the *Startup* folder
- [x] ~~"Never the registry" and "settings in a file next to the `.exe`": written into `RULES.md` (and
  its registry mention fixed)?~~ → Yes, at implementation
- [x] ~~File format and name: `settings.json` (JSON) next to the `.exe`?~~ → `settings.json`

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial scope from the user's request, settled in the scoping batch (Q&A #1–#4): only browse dialogs
update the remembered folder, one folder per kind of dialog, the export dialog remembers its own
folder, the subject is straightforward (single scout pass). The scout pass found a single open
dialog, a single save dialog, and no settings store. Six questions remain open.

### Iteration 2 — 2026-09-26

Q&A #5–#8: one folder per dialog (not per content kind); the export dialog keeps today's rule until
a first export, then opens on the last export folder; stored in `%AppData%\ImageGridFusion\settings.json`;
written as soon as a dialog closes with a file picked. Two questions remain: missing folder, unit
tests.

### Iteration 3 — 2026-09-26

Q&A #9–#10: a missing remembered folder falls back to its nearest existing parent, else to the
dialog's default folder; no unit tests, verified by hand. No open question remains.

### Iteration 4 — 2026-09-30

The code moved on since the design (other sessions), before any go: re-checked before asking it.

- A settings store now exists: `UI/AppSettings.cs`, in the **registry**, stated "no settings file",
  and named by `RULES.md` as the app settings' store — it contradicts the JSON decision (Q&A #7).
- A second open dialog: *Choose a soundtrack* (`BrowseSoundtrack`).
- A second save dialog: **Save last…** (`SaveLastVideo`), opening on `DefaultSaveFolder()` like the
  exports.
- The explorer's base-folder dialog is already remembered (it is that setting): not concerned.

Three questions reopened or added.

### Iteration 5 — 2026-09-30

Q&A #11–#13. The user corrects the storage: the registry in `AppSettings` was **never wanted** —
the settings go in a **file next to the `.exe`**, and the **registry is never used**. The folders are
stored there (replacing the `%AppData%` JSON file of Q&A #7). *Choose a soundtrack* gets a folder of
its own; **Save last…** shares the exports' folder. The move away from the registry raises new
scope questions: the existing `AppSettings` values, *Start with Windows* (`StartupRegistration`,
`HKCU\…\Run`), migrating the values already saved, and recording the rule.

### Iteration 6 — 2026-09-30

Q&A #14–#18: every `AppSettings` value moves to `settings.json` next to the `.exe` in this workfile;
the registry values are migrated once, then the key deleted; *Start with Windows* becomes a
*Startup* folder shortcut (an existing `Run` value migrated the same way); the rule goes into
`RULES.md`. New § Settings File. No open question remains.

### Iteration 7 — 2026-09-30 — ✅ Implemented

Go given by the user through the session *Drag-drop images dans Favoris* ("attends le go de…",
then "lancer les dévs"): code, README and `RULES.md` (no unit tests, Q&A #10). Stays on `main`, the
standing choice for this app. The Debug build is left alone while that session's instance runs.

### Iteration 8 — 2026-09-30 — 🧭 Implementation choices

- **Branch**: `main`, the app's standing choice — no Branch Gate question.
- **`AppSettings` keeps its API**, file-backed: `Save(params (name, value)[])`, `Has`, `IsSaveError`;
  the MainForm catches moved from `StartupRegistration.IsRegistryError` to `AppSettings.IsSaveError`
  (and `StartupRegistration.IsStartupError` for *Start with Windows*, `COMException` included).
- **Migration merges** rather than running only without a file: a value already in the file wins, the
  registry's fills the gaps. It also covers an older build still running and writing the key back.
- **One file per exe copy** — the consequence of "next to the `.exe`".
- **Save reads the file again first** (commit `29f7c8a`), found while checking: without it, two
  instances would overwrite each other's settings with their in-memory copies.
- **Shortcut refresh**: rewritten at each start-up when it exists, instead of comparing its target.
- **Folders**: `LastAddFolder`, `LastSoundtrackFolder`, `LastExportFolder` in `settings.json`; the
  folder is saved right after the dialog's OK, before the files load or the export starts.
- **Checks**: Debug and Release builds without warnings; first launch of the Debug build migrated the
  8 registry values into `bin\Debug\…\settings.json` and deleted the key. The dialogs, the missing
  folder and the Startup shortcut are left to the user's hand test.
- **Launch**: the instance launched for the user runs the build before `29f7c8a` — stopping it to
  relaunch was refused by the permission classifier. It is relaunched once closed.
- **Rule propagation**: 10 of the 14 running sessions were told of the `RULES.md` change; the last 4
  (*Explorateur de fichier : dossiers ouvrables*, *Emojis superposés sur contenu*, *Règles
  d'architecture workfiles*, *Rechercher fichier par contenu OCR*) were blocked by the messaging
  limit.
- No rule broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7, 8 | 2026-09-30 | `7eddc3b` settings file + migration + Startup shortcut, `fb64169` dialogs' folders, `29f7c8a` re-read before save |
| Unit tests | 3 | 2026-09-26 | Declined — no test project, verified by hand (Q&A #10) |
| README | 7 | 2026-09-30 | `c6b6f9a` |
| RULES.md | 7 | 2026-09-30 | `57defd0` § App Settings, § Global Effects' registry mention fixed |
| Manual validation | 8 | 2026-09-30 | Tested by hand by the user and confirmed finished |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which actions update the remembered folder? | Browse dialog only | 2026-09-25 |
| 2 | One folder shared by every open dialog, or one per kind? | One per kind | 2026-09-25 |
| 3 | Are the save (export) dialogs concerned? | Yes, with a folder of their own | 2026-09-25 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-25 |
| 5 | "One per kind" with a single open dialog: per dialog, or per content kind? | One per dialog | 2026-09-26 |
| 6 | Export folder: replaces the *first image's folder* rule, or only after a first export? | Today's rule until a first export, then the last export folder | 2026-09-26 |
| 7 | Storage: JSON file in `%AppData%`, or the registry? | JSON file in `%AppData%` | 2026-09-26 |
| 8 | Written when a dialog closes, or when the app closes? | When the dialog closes | 2026-09-26 |
| 9 | Remembered folder missing: nearest existing parent, or default folder? | Nearest existing parent | 2026-09-26 |
| 10 | Unit tests: none, or a first test project? | None, verified by hand | 2026-09-26 |
| 11 | Storage: the registry via `AppSettings`, or the JSON file of Q&A #7? | JSON file kept — then corrected: a file next to the `.exe`, never the registry, the registry in `AppSettings` was a mistake | 2026-09-30 |
| 12 | *Choose a soundtrack*: own folder, shared with *Add images*, or not concerned? | Own folder | 2026-09-30 |
| 13 | **Save last…**: exports' folder, own folder, or not concerned? | Exports' folder | 2026-09-30 |
| 14 | Existing `AppSettings` values: moved here, or a workfile of their own? | In this workfile | 2026-09-30 |
| 15 | Registry values already saved: migrated once, or left behind? | Migrated once, key deleted | 2026-09-30 |
| 16 | *Start with Windows*: *Startup* folder shortcut, registry exception, or removed? | *Startup* folder shortcut | 2026-09-30 |
| 17 | Rule written into `RULES.md`? | Yes, `RULES.md` | 2026-09-30 |
| 18 | File: `settings.json` next to the `.exe`? | Yes, `settings.json` | 2026-09-30 |

---

*Last updated: 2026-09-30*
