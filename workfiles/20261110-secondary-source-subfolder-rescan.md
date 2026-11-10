# Secondary Source Subfolder Rescan

> Working document — 🔄 clicked inside a subfolder of a secondary source reads and analyses that subfolder only.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, 🔄 inside a secondary source (`FileExplorerPanel.Rescan` → `LoadAndScanSourceAsync(source, analyse: true)`)
always reads the **whole source** again (`ScanSourceAsync` → `FileIndex.Scan(source.Folder)`) and then runs the passes
over **every file of its index** (`StartSourceRun` → `ExtractAsync` → `Extract`), wherever the folder view stands in
it. On a large source (a NAS, another drive), refreshing one folder costs a full read and a full analysis.

Goal: when the folder view is **inside a subfolder** of a secondary source, 🔄 reads and analyses **that subfolder and
its descendants only**; the rest of the source's index stays as it is. At the source's root, 🔄 keeps doing the whole
source.

Out of scope (user's decision): the base folder's 🔄 — it keeps reading the whole base folder, and its automatic passes
are unchanged.

Components: `Explorer/FileIndex.cs`, `UI/FileExplorerPanel.Sources.cs`, `UI/FileExplorerPanel.cs` (`Rescan`, `Extract`),
`Resources/Strings*.resx` (the 🔄 tooltip), RULES.md § Secondary Sources, GLOSSARY *Secondary source*.

⚠️ `workfiles/20261109-secondary-source-index-deletion.md`, designed in another session, also touches
`FileExplorerPanel.Sources.cs` — a merge may need care.

---

## The Scope of a 🔄

- **The scope** is the folder view's **open folder** relative to the source (`_openFolder`, which is already relative to
  `ViewRoot` while a source is open): `""` at the source's root — the whole source, as today — otherwise that subfolder,
  **recursively** (its descendants included).
- The scope is taken **when 🔄 is clicked** and held by the run: navigating afterwards does not change what it covers.

## Scan of a Subfolder

- `FileIndex` gains a scan **under a subfolder**: the enumeration starts at `root\<scope>`, recursively, with the same
  options (hidden / system skipped, inaccessible ignored) and the same two phases (counting, then recording), every
  relative path still computed **against the source's root**, so its entries are directly comparable with the index's.
- The result is **merged** into the source's index (`FileIndex` gains a method returning a new index): the entries
  **not under** the scope kept as they are, the entries under it **replaced** by the scan's — a file gone from the
  subfolder leaves the index, a new one enters it. The source's scan date (`ScannedAt`) is kept: the whole source was
  not read.
- The merged index is saved into the source's own `files.index` and taken over through `SourceRoot.SetIndex`, exactly as
  a whole scan's — the tile's file count, the folder tree and the entries by path follow by themselves.
- Progress: the status line and the first bar show the subfolder's counting and recording, the same texts as today.

## Passes Over a Subfolder

- `ExtractAsync` / `Extract` take the scope. **`RetainOnly` still runs over the whole index** — so the texts, faces and
  embeddings of the files outside the scope are kept, and only those of the files gone from the subfolder are dropped.
- Each pass's files — `ReadImages(index)`, `Read(index, pass)` — are **filtered to the scope**
  (`FolderTree.IsUnder(entry.RelativePath, scope)`), so every bar, every `n / total` of the status line and every
  *finished* counts the subfolder's files only.
- The tags recomputed when the vocabulary changed (Vision pass) read no image: they stay computed over the whole index.
- The base folder's passes wait meanwhile and resume once the run ends or is paused, as today (`RunSourceAsync`'s
  `finally` → `StartExtraction`).

## Navigation During a Scoped Run

- **Unchanged** (user's decision): the run goes on while the folder view stays inside the source — any of its folders,
  in or out of the scope — and is paused only when the source is left (another source, the base folder, another view),
  by `SetOpenSource` → `PauseSourceRun`. What it did is kept; a later 🔄 picks it up over its own scope.

## 🔄 Tooltip

- See Open Questions.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A scan under a subfolder records only the files under it, recursively, their relative paths against the root | `tests/ImageGridFusion.Tests/Explorer/FileIndexSubfolderScanTests.cs` | Create |
| A scan under a subfolder skips hidden / system entries as the whole scan does | `tests/ImageGridFusion.Tests/Explorer/FileIndexSubfolderScanTests.cs` | Create |
| The merge keeps every entry outside the scope unchanged, replaces those under it, drops a file gone from it, adds a new one | `tests/ImageGridFusion.Tests/Explorer/FileIndexSubfolderScanTests.cs` | Create |
| The merge does not take a sibling folder sharing the scope's name as a prefix (`a\b` vs `a\bc`) for part of the scope | `tests/ImageGridFusion.Tests/Explorer/FileIndexSubfolderScanTests.cs` | Create |
| The merge keeps the index's `ScannedAt` | `tests/ImageGridFusion.Tests/Explorer/FileIndexSubfolderScanTests.cs` | Create |

The filtering of the passes' files lives in `FileExplorerPanel` (private, UI) and relies on `FolderTree.IsUnder`; it is
not unit-tested beyond it.

---

## Open Questions

- [ ] What happens when the open subfolder **no longer exists** on the disk when 🔄 is clicked?
- [ ] Does the 🔄 **tooltip** change inside a subfolder of a source?
- [ ] Does the status line **name the subfolder** being analysed during a scoped run?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-10

Initial design from the user's request (*« si on est dans un sous dossier au moment où on fait le refresh d'indexation,
ne lancer l'indexation que sur ce dossier là »*) and the scoping batch (Q&A 1–4):

- 🔄 inside a source's subfolder: scan **and** passes restricted to that subfolder, recursively; the rest of the
  source's index untouched; the source's root unchanged (whole source).
- Secondary sources only — the base folder's 🔄 unchanged.
- The pause rule unchanged: leaving the source pauses the run, moving inside it does not.
- Code read: `Extract` starts with `RetainOnly(index.Entries)`, so the scope cannot be given by passing a filtered index
  — it would drop the texts, faces and embeddings of the rest of the source. Hence a scope parameter beside the whole
  index, and a scan whose result is merged into the index rather than replacing it.

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
| 1 | When 🔄 is clicked in a subfolder of a secondary source, what does the indexing cover? | Scan + passes, recursive: the subfolder and its descendants only, the rest of the source's index untouched | 2026-11-10 |
| 2 | Does the same apply to the base folder's 🔄 in one of its subfolders? | No — secondary sources only | 2026-11-10 |
| 3 | During a run started on a subfolder, another folder of the same source is opened: what becomes of it? | It goes on while inside the source, paused only when the source is left — as today | 2026-11-10 |
| 4 | How deep should the code exploration go? | Straightforward — a single scout pass | 2026-11-10 |
| 5 | The open subfolder no longer exists when 🔄 is clicked: what happens? | | |
| 6 | Does the 🔄 tooltip change inside a subfolder of a source? | | |
| 7 | Does the status line name the subfolder being analysed? | | |

---

*Last updated: 2026-11-10*
