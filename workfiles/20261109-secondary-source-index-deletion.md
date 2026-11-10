# Secondary Source Index Deletion

> Working document — removing a secondary source must leave no index of it on disk.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

RULES.md § Secondary Sources already states that *a removed source's index is deleted once its work is
over*, and the code does it: `FileExplorerPanel.ApplySources` cancels the removed source's scan and run, then
`DeleteSourceIndexAsync` awaits `SourceRoot.Work` and calls `SecondarySources.DeleteIndex`, which deletes
`index\secondary-sources\<id>\` recursively. Yet the user observed a removed source's index folder **left on
disk**.

Goal: a removed source's index goes for sure — at removal, and, when that fails or never ran, at the next
start — and the removal's confirmation says how much room it frees.

Components: `Explorer/SecondarySources.cs`, `UI/FileExplorerPanel.Sources.cs`, `UI/SecondarySourcesWindow.cs`,
`Program.cs` (start-up cleanups), `Resources/Strings*.resx`, RULES.md § Secondary Sources.

---

## Diagnosis

Read in the code (2026-11-09), no reproduction yet:

| Route | Finding |
|---|---|
| Window → `MainForm.EditSecondarySources` → `SecondarySourceEntries` setter → `ApplySources` | Called on every removal, after the settings are saved (even when saving fails) |
| `ApplySources` | Marks the source `Removed`, cancels `Scan` and `Extraction`, leaves it if open, starts `DeleteSourceIndexAsync` (fire-and-forget) |
| `SourceRoot.Work` | Holds the scan (`ScanSourceAsync`) and the run (`StartSourceRun`); the passes' saves (`TrySave`) all happen inside `Extract`, awaited by the run — no writer found outside `Work` but *Forget visual analysis…*'s fire-and-forget save of an unloaded source's texts |
| `DeleteIndex` | `Directory.Delete(recursive)` once — **no retry**; an `IOException` (a file still held for a moment: antivirus, Search indexer, a save finishing) leaves the folder, said once on the status line (`SourcesIndexNotDeleted`), easily missed |
| App closed while the removed source's work is still cancelling (a Caption step takes seconds) | The deletion never runs; **nothing catches it later** |
| A source taken out of `settings.json` by hand, or removed by another instance sharing the exe's folder | `ApplySources` at start-up only adds/keeps: an index folder matching no source is **never** looked at |

So the likely causes are a transient lock with no retry, or a deletion cut short by the exit; the second
safety net — the start-up cleanup the user asked for — covers both, and every other way an orphan appears.

On the user's Debug build today, `index\secondary-sources\` holds exactly the two folders of the two sources
in `settings.json` (`195cc95434b4b37e` = `X:\Dropbox\Images IA`, `08c786b4ada766a9` = `X:\Dropbox\Ventes\2026`):
no orphan to look at.

---

## Deletion at Removal

- `SecondarySources.DeleteIndex` **retries** on `IOException` / `UnauthorizedAccessException`: a few attempts
  spaced out (proposed: 5 attempts, 200 ms apart, off the UI thread), then throws the last error as today.
- A failure is still said on the status line (`SourcesIndexNotDeleted`); its text adds that the next start
  will try again.
- Nothing else changes in `ApplySources` / `DeleteSourceIndexAsync`: the work awaited, then the deletion.

---

## Start-Up Cleanup of Orphans

- At start-up, the **normal instance only** (never a `--new-instance`, which makes no write of its own accord —
  RULES.md § Single Instance) deletes every folder of `index\secondary-sources\` whose name is **not the id of a
  source** of `AppSettings.SecondarySources` (`SecondarySources.IdOf`).
- Only folders named like an id (16 lowercase hex digits) are touched; anything else there is left alone.
- Run off the UI thread, next to the other start-up cleanups (`Program.Main`: `UrlDownloads.Clean`,
  `GiphyFiles.Clean`), before the explorer loads the sources' stores — so it never races a source's own load.
- One definition: `SecondarySources.Orphans(folderNames, sources)` gives the folders to delete (pure, tested),
  `SecondarySources.CleanOrphans()` deletes them and gives how many went, a failure left for the next start.
- **Said only when it deleted something**: once the window shows, the status line says how many orphan indexes
  were deleted (e.g. *2 indexes of removed sources deleted*, English and French texts); nothing is said
  otherwise, nor for a failure (tried again at the next start).

---

## Removal Confirmation

- The confirmation of the sources window (`SecondarySourcesWindow.RemoveSelected`, `SourcesRemovePrompt`)
  already says the index is deleted and the folder untouched; it **adds the room freed**: the size the index
  folder takes on disk (`SecondarySources.IndexSize(sourceFolder)`, the sum of its files' lengths), formatted
  like the other sizes (`Texts`, `KB` / `Ko`).
- No index on disk yet (a source never opened) → the prompt as today, without the size.

---

## What Is Not an Index

- The **decisions on the people** (`people.json`) are kept: the faces of a removed source pinned to a person or
  excluded from one stay, each with its copy of the embedding, so re-adding the source finds its faces in their
  people again. Only `index\secondary-sources\<id>\` goes.
- English and French texts changed together (RULES.md § UI Language).

---

## Documentation

- RULES.md § Secondary Sources: the removed source's index deleted at removal, retried, and the orphans
  deleted at the next start by the normal instance.
- GLOSSARY.md / GLOSSARY.fr.md *Secondary source*: "a removed one's index deleted" stays true — no change
  expected.
- README / README.fr: checked during the run; changed only if they describe the removal.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `Orphans` gives the id-named folders matching no source, case ignored | `tests/ImageGridFusion.Tests/Explorer/SecondarySourceIndexTests.cs` | Create |
| `Orphans` leaves a folder not named like an id | same | Create |
| `Orphans` gives nothing when every folder is a source's | same | Create |
| `IndexSize` sums the files of a source's index folder; 0 when it does not exist | same | Create |
| `DeleteIndex` deletes the folder, and does nothing when it does not exist | same | Create |

---

## Open Questions

- [x] ~~Start-up cleanup: silent, or a status line saying how many orphan indexes were deleted?~~ → The status
      line says it, only when at least one was deleted
- [x] ~~Faces of the removed source pinned to a person in `people.json`: kept or forgotten with the index?~~ →
      Kept: they are decisions, not an index
- [x] ~~Reproduction: when the folder stayed, was a 🔄 analysis running in that source, or was the app closed
      soon after the removal?~~ → Not remembered; the design covers every case

---

## Design Iterations

### Iteration 1 — 2026-11-09

Scoping answers: the user saw a removed source's index folder left on disk (a bug); orphans are cleaned at
start-up; the removal's confirmation gives the room freed; the subject is expected to be straightforward.
Code read: the deletion exists but runs once, without retry, and is lost when the app closes first; nothing
ever deletes an orphan. Proposed: retries at removal, start-up cleanup of the folders matching no source by the
normal instance, the size in the confirmation.

### Iteration 2 — 2026-11-10

Answers: the start-up cleanup says on the status line how many orphan indexes it deleted, nothing otherwise;
the pinned and excluded faces of a removed source stay in `people.json`; the reproduction context is not
remembered. No open question left.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| RULES.md | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What did you observe? | The removed source's index folder stayed on disk | 2026-11-09 |
| 2 | Clean orphan index folders at start-up? | Yes, at start-up (normal instance) | 2026-11-09 |
| 3 | Confirmation when removing a source? | Confirm, with the room freed | 2026-11-09 |
| 4 | Depth of the subject? | Straightforward | 2026-11-09 |
| 5 | Start-up cleanup: silent or said? | Status line, only when something was deleted | 2026-11-10 | |
| 6 | Pinned faces of a removed source: kept or forgotten? | Kept | 2026-11-10 | |
| 7 | Reproduction context? | Not remembered | 2026-11-10 | |

---

*Last updated: 2026-11-10*
