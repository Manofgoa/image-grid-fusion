# Open People From an Image

> Working document — from a file's tile, open the face groups of the people in it, and rename the
> group open from a title at the top of its list.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The People view (workfiles/20261027-face-detection.md) groups the faces of the indexed images, but
reaching the group of a person seen in a given photo means scrolling the People view and recognising
the face. The goal: **name people easily**, starting from a photo.

1. **People submenu** — the context menu of a file's tile with faces gets a submenu listing the people
   of that file, **unnamed included**; an entry opens that person's group in the People view.
2. **Group title** — a group open in the People view shows its name as a **title at the top of its
   list**; clicked, it asks the new name in a `prompt()`-like popup.

Components: `UI/FileExplorerPanel.cs` (tile menu `_menu`, `_menu.Opening`, `OpenGroup`, `RenameGroup`,
`PersonOf`, `RefreshPeopleRows`), `UI/FileExplorerPanel.Vision.cs` (`PeopleOf`, the tooltip's people),
`Explorer/FaceGrouping.cs` (`PeopleSnapshot.OwnerOf`, `Ungrouped`), `Explorer/People.cs`,
`UI/NameDialog.cs`, `UI/ThumbnailGrid.cs`.

---

## People Submenu

- **Where**: the context menu of a **file tile** of the file explorer only — not the cells' menu.
  Every view where a file tile shows (search, folder, People — an open group's files included).
- **When**: the file has at least one face in the face index (`FaceIndex.FacesOf`); hidden otherwise,
  and while the face analysis is off. Its text: **People**, before the people items of the menu.
- **Entries**: one per **person** of the file, found as the tooltip finds them
  (`PeopleSnapshot.OwnerOf` on `FaceKeys.Of(path, box)`):
  - a face in a **group** → that group; a face in a **sub-group** → the **top group** it was merged
    into (only top groups open, as `RefreshPeopleRows` already does), the entry named as the tooltip
    names it (`PeopleOf`): the sub-group's own name when it has one, else the top's;
  - an **unnamed group** → an entry too, *Unnamed*;
  - a **face alone** (`PeopleSnapshot.Ungrouped`) → an entry too, *Unnamed*;
  - a **hidden** person → an entry too, listed **after** the others, its text ending with
    `(hidden)`;
  - two faces of the same group → one entry.
- **An entry shows**: the **face cut out of this photo** as its image (the face that put the person
  in the list, `FaceCrops`), then the name or *Unnamed*, then the person's file count —
  `Alice (12 files)`, `Unnamed (1 file)`. Order: the named A→Z, then the unnamed the larger first
  (`FaceGrouping`'s order), the hidden ones last in the same order.
- **Clicked**: the explorer switches to the **People view** (`ExplorerMode.People`) and opens the
  group (`OpenGroup`), the search box cleared.
- **A face alone, clicked**: its **person is created at once** — the face pinned to it, as `PersonOf`
  does for an unnamed group — and `people.json` saved; the groups computed again, its group of one
  face (a person with a face stays a group, `FaceGrouping.Compute`) is then opened. It stays a person
  even if it is never named.

---

## Group Title

- A group open in the People view (not the people list, not the *Hidden* tile) shows a **title at the
  top of its list**: the group's name, or *Unnamed*, in a larger bold font — a strip of its own
  above the tiles, staying in place while they scroll, in the Thumbnails and Details views alike. Hovered, it
  shows a hand cursor and the tooltip *Click to rename*.
- **Clicked** → a popup like JavaScript's `prompt()` asks the name — the existing `NameDialog`
  (*"The person's name:"*, the current name selected, OK / Cancel) — then the same path as *Rename…*
  (`RenameGroup`: `PersonOf`, `person.Name`, `SavePeople`).
- The popup opens **centered on the explorer panel**, not on the whole window — for the title and
  for *Rename…* / F2 alike (one `RenameGroup`).

---

## Documentation

- `README.md` / `README.fr.md` § People view: the **People** submenu of a file tile, the group title.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: *Face group* gains the title and the submenu, or a term of its own.
- `RULES.md` § People: the submenu and the title under *Decisions*.

---

## Test Impact

**No unit test**: the repository has no test project (`ImageGridFusion.slnx` holds the app only). The
entries' computation is kept a pure function over a `PeopleSnapshot` and a file's faces, so a test
can pin it if a test project is added later.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none, no test project — | — | — |

---

## Open Questions

- [x] ~~Where does the People submenu show?~~ → File tiles of the explorer only.
- [x] ~~Does a face alone get an entry?~~ → Yes, opening a person of that one face.
- [x] ~~How is the name asked from the group title?~~ → A popup like JavaScript's `prompt()`.
- [x] ~~A face alone: when is its person created (written to `people.json`)?~~ → At the opening.
- [x] ~~Hidden people: listed in the submenu?~~ → Yes, last, marked `(hidden)`.
- [x] ~~What does an entry show — name / *Unnamed*, a face picture, a file count?~~ → The face cut
  out of the photo, the name or *Unnamed*, the file count.
- [x] ~~Where does the name popup open?~~ → Centered on the explorer panel, *Rename…* / F2 included.

---

## Design Iterations

### Iteration 1 — 2026-10-28

Initial design from the request and the scoping batch (Q&A 1–4): a *People* submenu on the file
tiles, every person of the file listed — unnamed groups and faces alone included — opening its group
in the People view; a title at the top of an open group's list, clicked to rename through the
existing `prompt()`-like `NameDialog`. Exploration done directly (subject sized *straightforward*,
questions chained): `NameDialog` already exists; a person with a single pinned face stays a group
(`FaceGrouping.Compute` keeps every person with a face), so a face alone can be opened by creating its
person.

### Iteration 2 — 2026-10-28

Second batch (Q&A 5–8) settled every Open Question: a face alone becomes a person when its entry is
clicked; hidden people listed last, marked `(hidden)`; an entry shows the face cut out of the photo,
the name or *Unnamed* and the file count; the name popup centered on the explorer panel, *Rename…* /
F2 included. The sub-group naming follows the tooltip's (`PeopleOf`). No test project in the
repository: Test Impact says so. Documentation section added.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository |
| README (EN + FR) | | | |
| Glossary (EN + FR) | | | |
| RULES.md | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the People submenu show? | File tiles only | 2026-10-28 |
| 2 | A face alone of the photo: does the submenu offer it? | Yes, an entry too | 2026-10-28 |
| 3 | How does the group title ask the new name? | A popup like JavaScript's `prompt()` | 2026-10-28 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-28 |
| 5 | A face alone: when is its person created? | At the opening | 2026-10-28 |
| 6 | Hidden people in the submenu? | Yes, marked | 2026-10-28 |
| 7 | What does an entry show? | Face + name + file count | 2026-10-28 |
| 8 | Where does the name popup open? | Centered on the explorer | 2026-10-28 |

---

*Last updated: 2026-10-28*
