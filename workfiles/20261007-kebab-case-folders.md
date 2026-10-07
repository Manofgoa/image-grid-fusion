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
| `%TEMP%\ImageGridFusion\` | `MainForm.TempVideoFolder` | ❌ → left as it is: the rule covers future folders (Q&A 5) |

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
- It applies to the folders created **from now on**: an existing one keeps its name unless a
  workfile renames it (Image Grid Fusion's `%TEMP%\ImageGridFusion` stays).
```

- `../CLAUDE.md` is **not versioned** (`mini-apps/` is not a git repository): the change is written,
  not committed.
- Being a CLAUDE.md change, it is propagated to the other running sessions of the workspace (user
  instructions § Rule changes).

---

## Documentation

The docs still place `files.index` "next to the exe", which it no longer is since the indexing
folder exists. They now name the `index\` folder (Q&A 6), English and French in the same commit:

| File | Where | Becomes |
|---|---|---|
| `README.md` / `README.fr.md` | § Index | `files.index`, in the `index\` folder next to the exe |
| `README.md` / `README.fr.md` | § Settings file | like the `index\` folder and `favorites.txt` |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | Index row | cached in `indexiles.index` next to the exe |
| `RULES.md` | § App Settings | like the `index\` folder and `favorites.txt` |

---

## Test Impact

Nothing testable changes: the repository has **no test project** (`src/` holds only
`ImageGridFusion`), and the change is a constant's value.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

- [x] ~~`%TEMP%\ImageGridFusion` breaks the new rule: rename it `%TEMP%\image-grid-fusion` in this
  workfile, or leave it?~~ → Left as it is; the rule covers the folders created from now on
- [x] ~~Fix the docs placing `files.index` "next to the exe" (it lives in `index\`, with
  `files.content`), English and French, in this workfile?~~ → Fixed, § Documentation

---

## Design Iterations

### Iteration 1 — 2026-10-07

Initial design from the scoping batch (Q&A 1–4): `FileIndex.FolderName` becomes `"index"`, no
migration of the existing folder; a `## Folder Names` section in `../CLAUDE.md` makes every folder
an app creates at run time kebab-case, the source folders aside. The exploration found a second
runtime folder in PascalCase (`%TEMP%\ImageGridFusion`) and stale docs about `files.index`: both
left as Open Questions.

### Iteration 2 — 2026-10-07

Open Questions settled (Q&A 5–6): `%TEMP%\ImageGridFusion` keeps its name, the rule saying it
covers the folders created from now on; the docs placing `files.index` next to the exe are fixed
(README, glossary, RULES.md § App Settings), English and French.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given (Q&A 7): code and documentation, in a dedicated worktree
(`.claude/worktrees/kebab-case-folders`, branch `feature/kebab-case-folders`), fast-forwarded into
`main` and removed at the end.

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

- **Worktree despite "Current checkout"**: the go's answers disagreed — the free text asked for
  "a separate worktree", the second question said *Current checkout*. The explicit text won.
- **The shared rule names the app** for its example: "Image Grid Fusion's `%TEMP%\ImageGridFusion`",
  since `../CLAUDE.md` is read by every app.
- **`RULES.md` § App Settings** also said "like `files.index` and `favorites.txt`": fixed in its own
  commit with the docs (§ Documentation lists it).
- No rule broken.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-07 | `FileIndex.FolderName` = `"index"`; built |
| Shared rule (`../CLAUDE.md`) | 3 | 2026-10-07 | § Folder Names; not versioned, so not committed; sent to the running session *Zoom sans déplacement d'image* |
| Unit tests | 3 | 2026-10-07 | Not applicable — no test project |
| README | 3 | 2026-10-07 | README, glossary (English and French) and RULES.md § App Settings |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Existing `Index` folders on disk: renamed at start-up, or no migration? | No migration | 2026-10-07 |
| 2 | Where does the kebab-case rule live? | `../CLAUDE.md`, shared by every app | 2026-10-07 |
| 3 | What does the rule cover? | The folders an app creates at run time; source folders stay PascalCase | 2026-10-07 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-07 |
| 5 | `%TEMP%\ImageGridFusion`: renamed `image-grid-fusion` here, or left? | Left as it is | 2026-10-07 |
| 6 | Fix the docs placing `files.index` next to the exe? | Fix them | 2026-10-07 |
| 7 | Go for implementation? (scope, where) | Code and docs, in a separate worktree | 2026-10-07 |

---

*Last updated: 2026-10-07*
