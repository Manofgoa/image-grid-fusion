# Search Results Sort

> Working document — a drop-down choosing how the file explorer orders its lists: relevance, date,
> name or size.
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

Agreed (Q&A 1):

- **In**: a search's results, the `*` list, the folder view (browsed and searched).
- **Out**: the favorites, shown while the search box is empty — they keep their order. *(The
  "Something else" choice ticked alongside: see Open Questions.)*

---

## Sort Orders

Agreed (Q&A 2): **Relevance**, then **Date** descending, **Name** ascending, **Size** ascending.

| Entry | Order | Ties broken by |
|---|---|---|
| Relevance | `Rank` as today — the default for a search with words | The relative path, as today |
| Date ↓ | The most recently created first (`IndexEntry.Created`) | Name, then relative path |
| Name ↑ | A→Z on the file name, accents and case ignored (`NameOrder`, as the folder view already uses) | Relative path |
| Size ↑ | The smallest first | Name, then relative path |

How "Relevance **then** Date / Name / Size" reads — four entries, or Relevance always first with the
drop-down choosing the tie-break — is an Open Question.

### Size Needs the Index

The index file does not hold the size: `IndexEntry.Stamp` (size + last write) exists only on a
**scanned** entry, an entry loaded from `index\files.index` has none.

Proposed:

- `files.index` gets a third column, the size; the header becomes `ImageGridFusion index 3`.
- An `index 2` file is still read (no size), so a launch never starts from an empty list: the
  sizes are unknown until the start-up scan rewrites the index, an entry of unknown size sorted
  **last** in Size ↑.
- The folder view's browse reads the size from the disk directly (`FileSystemEntry.Length`, next
  to the creation time `FolderListing.Read` already reads).

---

## UI

Agreed (Q&A 3): a **drop-down** (`ComboBox`, `DropDownList` style).

- Proposed place: in the search row, between the criteria buttons (**Aa**, **💡**) and the search
  box — `_searchRow` gets one more `AutoSize` column. Alternative: the caption / breadcrumb row.
  See Open Questions.
- Its tooltip says what the chosen order does.
- Changing it re-orders the shown list at once, from the first load (the list scrolls back to the
  top), the search itself not re-run when only the order changed.

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

- [ ] **"Something else"** was ticked among the lists, with no text: which other list should be
  sorted?
- [ ] **"Relevance then Date ↓ / Name ↑ / Size ↑"**: (a) four entries in the drop-down, Relevance
  being one of them; or (b) Relevance always the primary order of a search, the drop-down choosing
  the tie-break among Date / Name / Size?
- [ ] **No words typed** (`*`, a browsed folder) — there is no relevance: is the Relevance entry
  disabled / hidden with the list falling back to Date ↓ (today's order), or does the drop-down
  keep a separate choice for these lists?
- [ ] **Folder view's subfolders**: do they stay first and A→Z whatever the order, or follow it
  (Date ↓ by their creation, Name ↑; they have no size)?
- [ ] **Remembered** between sessions (an app setting in `settings.json`), or back to the default
  at each launch?
- [ ] **Place** of the drop-down: in the search row next to **Aa** / **💡**, or on the caption /
  breadcrumb line?
- [ ] **Size in the index**: is the proposed `index 3` format (§ Size Needs the Index) fine — an
  old index read without sizes until the start-up scan rewrites it?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — none |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which file explorer lists does the sort apply to? | A search's results, `*`, the folder view — and "Something else" (no text). Not the favorites | 2026-10-09 |
| 2 | Which sort criteria? | "select ? Pertinence puis Date DESC ou Abc ASC ou Taille AS" — Relevance, then Date descending, Name ascending, Size ascending | 2026-10-09 |
| 3 | How is the order chosen? | A drop-down | 2026-10-09 |
| 4 | Straightforward or tricky / long to explore? | Straightforward | 2026-10-09 |
| 5 | Which other list does "Something else" mean? | | 2026-10-09 |
| 6 | Relevance then Date / Name / Size: four entries, or Relevance first with a tie-break? | | 2026-10-09 |
| 7 | No words typed (`*`, a browsed folder): what does Relevance become? | | 2026-10-09 |
| 8 | Folder view's subfolders: first and A→Z always, or following the order? | | 2026-10-09 |

---

*Last updated: 2026-10-09*
