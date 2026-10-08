# Previous Copy Folder

> Working document — every Copy also keeps its content in a `previous` folder next to the exe, the
> last one only, named after the cells' files.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a Copy leaves nothing durable behind: the PNG goes to the clipboard as data only, and the
MP4, GIF and light JPEG live in `%TEMP%\ImageGridFusion` until the next start, which cleans them
(`MainForm.TempVideoFolder`, `CleanTempVideos`). Once the clipboard is overwritten and the app
restarted, the last copied content is gone.

The change: every Copy also writes its content into a **`previous`** folder next to the `.exe`
(`AppContext.BaseDirectory`), which only ever holds **the last copied content** — the one before is
deleted. Its file is named after the **files of the grid's cells**, combined.

Components: `UI/MainForm.cs` (the three Copy routes), a new small class owning the folder and the
naming, the README (EN + FR), the glossary (EN + FR).

---

## Scope

| Route | Kept in `previous` | File |
|---|---|---|
| **Copy PNG** (`Copy(null)`, `Ctrl+C` on a still grid) | ✅ | `.png` — the PNG bytes already encoded for the clipboard's PNG format (transparency kept) |
| **Copy MP4** / **Copy GIF** (`Copy(format)`) | ✅ | `.mp4` / `.gif` — a copy of the temp file once the export succeeded |
| **JPEG for sharing** (`CopyForSharing`) | ✅ | `.jpg` — a copy of the temp file |
| **Copy last MP4 / GIF** (`CopyLastVideo`) | ❌ | Generates nothing: it puts back a content already copied |
| **Save…** / **Save last…** | ❌ | Not a Copy |
| A cancelled or failed export | ❌ | Nothing was copied; `previous` keeps what it had |

- The temp folder is **unchanged**: the clipboard and the *last video* keep pointing at the temp
  file. `previous` holds a **copy**, so emptying it never breaks *Copy last MP4 / GIF*.

---

## The `previous` Folder

- `Path.Combine(AppContext.BaseDirectory, FolderName)`, `FolderName = "previous"` — kebab-case,
  one constant next to the code owning it (`../CLAUDE.md` § Folder Names), like
  `PastedFavorites.FolderName` and `FileIndex.FolderName`.
- Created on the first Copy that needs it.
- **Last content only**: the new file is written first, then every other file of the folder is
  deleted — a failed write never leaves the folder empty. A file that cannot be deleted (in use) is
  left for the next Copy.
- Subfolders, if any, are left alone: only the folder's files are deleted.
- A `--new-instance` instance writes it too: a Copy is the user's action, not a write *of its own
  accord* (RULES.md § Single Instance).

---

## File Name

Built from the **files of the grid's cells**, in **cell order**:

| Rule | Example |
|---|---|
| Each cell's file name **without its extension** | `chat.jpg` → `chat` |
| Joined by **` + `** | `chat + plage.mp4` |
| A cell with no file (pasted image, pasted text) is **skipped** | `chat + [pasted] + plage` → `chat + plage` |
| The same name twice (same file in two cells, or two files with one name) appears **once**, case-insensitively | `chat + chat` → `chat` |
| No cell has a file | `fusion-YYYYMMDD-HHMMSS` — the name the temp files and Save's dialog already use |
| Extension | `.png`, `.mp4`, `.gif` or `.jpg`, the copied content's |
| Too long | See Open Questions |

The source names are file names already, so they hold no invalid character.

---

## Status Line

Unchanged when `previous` is written. Its failure does not fail the Copy — see Open Questions.

---

## Documentation

- `README.md` + `README.fr.md`, § Copy: every Copy also keeps its content in `previous\` next to the
  exe, the last one only, named after the cells' files.
- `GLOSSARY.md` + `GLOSSARY.fr.md`: see Open Questions.

---

## Test Impact

The repository has **no test project** (`src/` holds `ImageGridFusion` only). The naming is a pure
static function (cell file paths → file name), ready to be pinned the day one exists.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | Not applicable: no test project |

---

## Open Questions

- [ ] **Clipboard and Save names**: should the combined name also be given to the temp file put on
  the clipboard (MP4, GIF, JPEG — the name seen when pasting into Explorer or a chat app) and to
  Save's dialog default name? *Proposal: no — `previous` only, as asked; the rest stays
  `fusion-…`.*
- [ ] **Length cap**: *Proposal: whole names are added while the name stays within 100 characters;
  the ones left out are counted — `chat + plage + 3 more.mp4`. A first name longer than 100
  characters on its own is cut, ending with `…`.*
- [ ] **Write failure** (disk full, folder read-only): *Proposal: the Copy still succeeds — the
  content is on the clipboard — and the status line adds `Not kept in previous\: <reason>`.*
- [ ] **Glossary**: add a term? *Proposal: **Previous copy** (*copie précédente*) — the last copied
  content, kept in `previous\` next to the exe, named after the cells' files.*

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request and the scoping batch (Q&A 1–4): every Copy route (PNG, MP4 / GIF,
JPEG for sharing) also writes its content into `previous\` next to the exe, as a copy — the temp
folder unchanged; the folder keeps the last content only, the new file written before the others
are deleted; the name joins the cells' file names with ` + `, cells without a file skipped,
duplicates once, `fusion-…` when none. Four proposals left open: names on the clipboard / Save,
length cap, write failure, glossary term.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which Copy routes keep their content in `previous`? | Every Copy — PNG, MP4 / GIF, JPEG for sharing; not *Copy last MP4 / GIF* | 2026-10-08 |
| 2 | Link with the temp folder `%TEMP%\ImageGridFusion`? | An extra copy: the temp folder stays as it is, `previous` holds a copy | 2026-10-08 |
| 3 | How are the cells' file names combined? | Joined by ` + `, cell order, no extension, duplicates removed, capped; no file → `fusion-YYYYMMDD-HHMMSS` | 2026-10-08 |
| 4 | Straightforward or tricky subject? | Straightforward — one scout pass | 2026-10-08 |

---

*Last updated: 2026-10-08*
