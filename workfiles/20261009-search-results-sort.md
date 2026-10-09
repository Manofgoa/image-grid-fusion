# Search Results Sort

> Working document — a drop-down choosing how the file explorer orders its files: date, name or
> size, after the relevance of a search.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every file explorer list has one fixed order:

| List | Current order | Where |
|---|---|---|
| A search's matches (search view and folder view) | Best first — path matches before content matches, then the words found in the file name, the first word's position in the name, the shorter name, the relative path | `FileSearch.Search` / `Rank.CompareTo` |
| `*` (search view, and the folder view's `*` search) | The most recently created first, then the relative path | `FileSearch.All` |
| The folder view's open folder, browsed | Subfolders A→Z (`NameOrder`), then files the newest first, then by name | `FolderListing.Read` (`Explorer/FolderTree.cs`) |
| The folder view's `*` search | Folders A→Z by path (`FolderTree.ComparePaths`), then files as `*` | `FileExplorerPanel.SearchFolder` |
| Favorites (search box empty) | The newest first | *(out of scope, see § Scope)* |

The work adds a **sort drop-down** to the file explorer so the user chooses the order of these
lists.

---

## Scope

Agreed (Q&A 1, 5):

- **In**: a search's results, the `*` list, the folder view (browsed and searched) — its **files**.
- **Out**: the favorites, shown while the search box is empty — they keep their order.
- **Out**: the folder view's **subfolders** (Q&A 8) — always listed first, ordered as today: A→Z
  when browsed (`NameOrder`), A→Z by path for `*` (`FolderTree.ComparePaths`), best first for a
  search with words. Only the files below them follow the drop-down.

---

## Sort Orders

Agreed (Q&A 2, 6, 7): the drop-down holds **three orders** — **Date ↓**, **Name ↑**, **Size ↑**.
**Relevance is not one of them**: a search with words is always ordered by relevance **first**, the
drop-down ordering the files that are equally relevant; with no words (`*`, a browsed folder) the
drop-down's order applies alone.

| Entry | Order | Ties broken by |
|---|---|---|
| Date ↓ (default — today's order) | The most recently created first (`IndexEntry.Created`) | Name, then relative path |
| Name ↑ | A→Z on the file name, accents and case ignored (`NameOrder`, as the folder view already uses) | Relative path |
| Size ↑ | The smallest first | Name, then relative path |

| List | Ordered by |
|---|---|
| A search with words (search view or folder view) | Relevance tier, then the drop-down's order |
| `*`, a browsed folder | The drop-down's order |

### Relevance Tiers

Agreed (Q&A 9): a search's relevance is cut into **three tiers**, the drop-down ordering the files
inside each tier:

| Tier | The file is found by |
|---|---|
| 1 | Its **file name** — the words found in the name |
| 2 | Its **subfolders** only — a path match with no word in the name |
| 3 | Its **content** text — a word found only there (`SearchMatch.ByContent`) |

- Today's other `Rank` keys — the first word's position in the name, the name length, the relative
  path — are **dropped** for the files: the drop-down's order and its ties take over.

- With several words, the path matches keep today's **number of words found in the file name**
  (`Rank.NameHits`, more first): all of them in the name before part of them, part of them before
  none (tier 2). One word typed gives exactly the three tiers above. The content matches are one
  tier, whatever their name holds.
- `FileSearch.Search` takes the order and sorts by `(tier, chosen order)`; the folders a folder-view
  search finds keep today's full `Rank` (§ Scope).

### Size Needs the Index

The index file does not hold the size: `IndexEntry.Stamp` (size + last write) exists only on a
**scanned** entry, an entry loaded from `index\files.index` has none.

Agreed (Q&A 12):

- `files.index` gets a third column, the size; the header becomes `ImageGridFusion index 3`.
- An `index 2` file is still read (no size), so a launch never starts from an empty list: the
  sizes are unknown until the start-up scan rewrites the index, an entry of unknown size sorted
  **last** in Size ↑.
- The folder view's browse reads the size from the disk directly (`FileSystemEntry.Length`, next
  to the creation time `FolderListing.Read` already reads).

---

## UI

Agreed (Q&A 3, 10, 11): a **drop-down** (`ComboBox`, `DropDownList` style).

- Entries: **Date ↓**, **Name A→Z**, **Size ↑**; Date ↓ at first.
- Place: at the **bottom right** of the panel, at the end of the tile size row, after the slider's
  `▭` glyph — `_sizeRow` gets a fourth `AutoSize` column, the slider shrinking for it.
- Its tooltip says what the chosen order does, and that a search puts the relevance first.
- Changing it re-orders the shown list at once, from the first load (the list scrolls back to the
  top), the search itself not re-run when only the order changed. The favorites (search box empty)
  are not re-ordered: the drop-down stays enabled, it applies as soon as a search or `*` shows.
- **Remembered** between sessions: an app setting in `settings.json` (`UI/AppSettings.cs`), like the
  tile size and the folder view; an unknown or missing value falls back to Date ↓.

---

## Test Impact

The repository has **no test project** (`src/ImageGridFusion/ImageGridFusion.csproj` only): **no
unit test is created or updated**. The check is manual, in the running app — each order on a
search, on `*`, on a browsed folder, and the sizes before / after the start-up scan.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project) | — | — |

---

## Open Questions

- [x] ~~**"Something else"** was ticked among the lists, with no text: which other list should be
  sorted?~~ → None, a mistake (Q&A 5).
- [x] ~~**"Relevance then Date ↓ / Name ↑ / Size ↑"**: four entries, or Relevance always first with
  the drop-down choosing the tie-break?~~ → Relevance always first for a search with words, the
  drop-down orders the equally relevant files (Q&A 6).
- [x] ~~**No words typed** (`*`, a browsed folder): what does Relevance become?~~ → The drop-down's
  order (Date ↓, Name ↑ or Size ↑) applies alone (Q&A 7).
- [x] ~~**Folder view's subfolders**: first and A→Z always, or following the order?~~ → Always first,
  ordered as today (Q&A 8).
- [x] ~~**How coarse is the relevance** before the drop-down's order takes over?~~ → Three tiers:
  file name, subfolders only, content (Q&A 9, § Relevance Tiers).
- [x] ~~**Remembered** between sessions, or back to Date ↓ at each launch?~~ → Remembered, in
  `settings.json` (Q&A 10).
- [x] ~~**Place** of the drop-down: the search row, or the caption / breadcrumb line?~~ → Neither: at
  the bottom right, at the end of the tile size row (Q&A 11).
- [x] ~~**Size in the index**: is the `index 3` format fine?~~ → Yes, an `index 2` file still read
  without sizes (Q&A 12).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Scoping batch answered (Q&A 1–4): sort the search results, `*` and the folder view, not the
favorites; orders Relevance, Date ↓, Name ↑, Size ↑; a drop-down; a straightforward subject, one
scout pass. The scout pass mapped the current orders (§ Overview) and found the size missing from
the index file (§ Size Needs the Index). Seven Open Questions left.

### Iteration 2 — 2026-10-09

Q&A 5–8 answered: no other list than the three agreed (favorites out); the drop-down holds Date ↓,
Name ↑ and Size ↑ only — a search with words stays ordered by relevance first, the drop-down
ordering the equally relevant files, and applies alone without words; the folder view's subfolders
stay first, ordered as today. New Open Question: how coarse the relevance is before the drop-down
takes over.

### Iteration 3 — 2026-10-09

Q&A 9–12 answered: three relevance tiers (file name, subfolders only, content) — the first word's
position and the name length no longer ranked, the drop-down's order taking over inside a tier,
the number of words in the name kept for several words; the order remembered in `settings.json`;
the drop-down at the end of the tile size row, bottom right, rather than either place proposed; the
`index 3` format with the size, an `index 2` file still read. No Open Question left.

### Iteration 4 — 2026-10-09 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/search-results-sort`,
branch `feature/search-results-sort`). Scope frozen as the sections above stand.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — none |
| README (EN + FR) | | | § File explorer: the Search and Everything paragraphs (the ranking, `*`'s order), the sort drop-down |
| Glossary (EN + FR) | | | The *Sort order* term; *Tile size* row mentions the row it shares |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which file explorer lists does the sort apply to? | A search's results, `*`, the folder view — and "Something else" (no text). Not the favorites | 2026-10-09 |
| 2 | Which sort criteria? | "select ? Pertinence puis Date DESC ou Abc ASC ou Taille AS" — Relevance, then Date descending, Name ascending, Size ascending | 2026-10-09 |
| 3 | How is the order chosen? | A drop-down | 2026-10-09 |
| 4 | Straightforward or tricky / long to explore? | Straightforward | 2026-10-09 |
| 5 | Which other list does "Something else" mean? | None, a mistake | 2026-10-09 |
| 6 | Relevance then Date / Name / Size: four entries, or Relevance first with a tie-break? | Relevance first, the drop-down breaks the ties (Date ↓ / Name ↑ / Size ↑) | 2026-10-09 |
| 7 | No words typed (`*`, a browsed folder): what does Relevance become? | "Date DESC ou Abc ASC ou Taille ASC" — the drop-down's order applies alone | 2026-10-09 |
| 8 | Folder view's subfolders: first and A→Z always, or following the order? | Always first, A→Z | 2026-10-09 |
| 9 | How coarse is the relevance before the drop-down's order takes over? | Three tiers: file name, subfolders only, content | 2026-10-09 |
| 10 | Is the order remembered between sessions? | Remembered | 2026-10-09 |
| 11 | Where does the drop-down go? | "En bas à droite de la ligne de taille de vignette (le slider)" — bottom right, at the end of the tile size row | 2026-10-09 |
| 12 | Is the `index 3` format with the size fine? | Yes, the old index still read | 2026-10-09 |

---

*Last updated: 2026-10-09*
