# Kebab-Case Folders

> Working document — the indexing folder renamed `Index` → `index`, and a shared rule naming
> every folder an app creates in kebab-case.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app creates its indexing folder next to the exe as `Index` (`FileIndex.FolderName`,
`src/ImageGridFusion/Explorer/FileIndex.cs`), while its other data folder is already kebab-case
(`favorites-from-pasted`, `PastedFavorites.FolderName`). The folder becomes `index`, and a rule
shared by every mini-app makes every future folder an app creates kebab-case.

---

## Code

- `FileIndex.FolderName`: `"Index"` → `"index"`. Nothing else reads the name: `FileIndex.Folder`,
  `FileIndex.DefaultPath` and `ContentIndex` (its `files.content`) all go through it.
- **No migration**: an existing `Index` folder is left as it is. NTFS ignores the case, so the app
  keeps finding and writing into it (`Directory.CreateDirectory("…\index")` reuses `Index`); only a
  fresh install creates `index`. The user renames their own folder by hand.
- `FileIndex.MoveLegacy` (an index left next to the exe by an older version) is unchanged: it moves
  it into `FileIndex.Folder`, whatever its case on disk.

### Other Runtime Folders Found

| Folder | Created by | Kebab-case |
|---|---|---|
| `favorites-from-pasted\` next to the exe | `PastedFavorites.FolderName` | ✅ |
| `Index\` next to the exe | `FileIndex.FolderName` | ❌ → `index`, this workfile |
| `%TEMP%\ImageGridFusion\` | `MainForm.TempVideoFolder` | ❌ → see Open Questions |

---

## Shared Rule

In `../CLAUDE.md` (rules shared by every mini-app), a new section:

```markdown
## Folder Names

Every folder an app **creates at run time** — next to the exe, in `%TEMP%`, anywhere else — is
named in **kebab-case**: lowercase words joined by hyphens (`index`, `favorites-from-pasted`).

- The **source folders** keep the .NET convention, PascalCase (`Explorer`, `UI`): the rule is about
  what the app writes on disk, not the repository's layout.
- A new folder's name lives in **one constant** (`FolderName`), next to the code that owns it.
```

- `../CLAUDE.md` is **not versioned** (`mini-apps/` is not a git repository): the change is written,
  not committed.
- Being a CLAUDE.md change, it is propagated to the other running sessions of the workspace (user
  instructions § Rule changes).

---

## Documentation

See Open Questions — the README and the glossary still place `files.index` "next to the exe",
which it no longer is since the indexing folder exists (`README.md` § Index and § Settings file,
`GLOSSARY.md` § Index, and their French versions).

---

## Test Impact

Nothing testable changes: the repository has **no test project** (`src/` holds only
`ImageGridFusion`), and the change is a constant's value.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [ ] `%TEMP%\ImageGridFusion` breaks the new rule: rename it `%TEMP%\image-grid-fusion` in this
  workfile, or leave it (the rule then covers future folders only)? Renamed, the old folder's last
  files are no longer cleaned at start-up — it stays in `%TEMP%` until Windows cleans it.
- [ ] Fix the docs placing `files.index` "next to the exe" (it lives in `index\`, with
  `files.content`), English and French, in this workfile?

---

## Design Iterations

### Iteration 1 — 2026-10-07

Initial design from the scoping batch (Q&A 1–4): `FileIndex.FolderName` becomes `"index"`, no
migration of the existing folder; a `## Folder Names` section in `../CLAUDE.md` makes every folder
an app creates at run time kebab-case, the source folders aside. The exploration found a second
runtime folder in PascalCase (`%TEMP%\ImageGridFusion`) and stale docs about `files.index`: both
left as Open Questions.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Shared rule (`../CLAUDE.md`) | | | Not versioned |
| Unit tests | | | No test project — nothing to test |
| README | | | Depends on Open Questions |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Existing `Index` folders on disk: renamed at start-up, or no migration? | No migration | 2026-10-07 |
| 2 | Where does the kebab-case rule live? | `../CLAUDE.md`, shared by every app | 2026-10-07 |
| 3 | What does the rule cover? | The folders an app creates at run time; source folders stay PascalCase | 2026-10-07 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-07 |
| 5 | `%TEMP%\ImageGridFusion`: renamed `image-grid-fusion` here, or left? | | |
| 6 | Fix the docs placing `files.index` next to the exe? | | |

---

*Last updated: 2026-10-07*
