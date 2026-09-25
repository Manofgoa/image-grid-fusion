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
| `UI/MainForm.cs` — `PickFiles()` (l.459) | The only **open** dialog (`OpenFileDialog`, *Add images*, multiselect): every kind of content a cell accepts — images, videos, PDF, text. Sets no `InitialDirectory` |
| `UI/MainForm.cs` — `Save()` (l.666), `DefaultSaveFolder()` (l.911) | The only **save** dialog (`SaveFileDialog`), PNG or MP4 export. Opens on the folder of the first loaded image's file, else *My Pictures* |
| `UI/StartupRegistration.cs` | The only per-user state the app writes today: the *Start with Windows* registry value (`HKCU\…\Run`). Nothing else survives a restart |
| `README.md` | Documents the features |

There is no settings store yet (no `Properties.Settings`, no settings file): this work creates the
first one. Project: WinForms, `net10.0-windows10.0.19041.0`.

Related: `workfiles/20260925-soundtrack.md` (design stage) may add an audio picker later.

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
- **Storage**: a JSON settings file, `%AppData%\ImageGridFusion\settings.json` — the app's first
  settings store, open to other settings later.
- **Written as soon as a dialog closes** with a file picked, so a crash never loses it.

| Dialog | Remembered folder | Updated by |
|---|---|---|
| Open — *Add images* (`PickFiles`) | Last loading folder | The folder of the files picked |
| Save — export PNG / MP4 (`Save`) | Last export folder | The folder of the file saved |

Pending (see Open Questions): what happens when the folder no longer exists.

---

## Test Impact

Pending — see Open Questions. The repository has no test project (`src/` holds `ImageGridFusion`
only); every previous workfile shipped without unit tests.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|

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
- [ ] The remembered folder no longer exists (deleted, USB drive unplugged): nearest existing parent
  folder, or the dialog's default folder?
- [ ] Unit tests: none, verified by hand like every previous workfile, or a first test project?

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
| 1 | Which actions update the remembered folder? | Browse dialog only | 2026-09-25 |
| 2 | One folder shared by every open dialog, or one per kind? | One per kind | 2026-09-25 |
| 3 | Are the save (export) dialogs concerned? | Yes, with a folder of their own | 2026-09-25 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-25 |
| 5 | "One per kind" with a single open dialog: per dialog, or per content kind? | One per dialog | 2026-09-26 |
| 6 | Export folder: replaces the *first image's folder* rule, or only after a first export? | Today's rule until a first export, then the last export folder | 2026-09-26 |
| 7 | Storage: JSON file in `%AppData%`, or the registry? | JSON file in `%AppData%` | 2026-09-26 |
| 8 | Written when a dialog closes, or when the app closes? | When the dialog closes | 2026-09-26 |
| 9 | Remembered folder missing: nearest existing parent, or default folder? | | 2026-09-26 |
| 10 | Unit tests: none, or a first test project? | | 2026-09-26 |

---

*Last updated: 2026-09-26*
