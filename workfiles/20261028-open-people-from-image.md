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
  and while the face analysis is off.
- **Entries**: one per **person** of the file, found as the tooltip finds them
  (`PeopleSnapshot.OwnerOf` on `FaceKeys.Of(path, box)`):
  - a face in a **group** → that group; a face in a **sub-group** → the **top group** it was merged
    into (only top groups open, as `RefreshPeopleRows` already does), entry labelled with the top's
    name (or the sub-group's own, as `PeopleOf` does — see Open Questions);
  - an **unnamed group** → an entry too (*Unnamed* — see Open Questions for its label);
  - a **face alone** (`PeopleSnapshot.Ungrouped`) → an entry too, opening a person of that one face
    (see Open Questions for when that person is created);
  - two faces of the same group → one entry.
- **Clicked**: the explorer switches to the **People view** (`ExplorerMode.People`) and opens the
  group (`OpenGroup`), the search box cleared.

---

## Group Title

- A group open in the People view (not the people list, not the *Hidden* tile) shows a **title at the
  top of its list**: the group's name, or *Unnamed*.
- **Clicked** → a popup like JavaScript's `prompt()` asks the name — the existing `NameDialog`
  (*"The person's name:"*, the current name selected, OK / Cancel) — then the same path as *Rename…*
  (`RenameGroup`: `PersonOf`, `person.Name`, `SavePeople`).
- The popup stays within the explorer (see Open Questions for its position).

---

## Test Impact

To fill once the Open Questions are settled. Candidates: the people of a file → the entries of the
submenu (dedup, sub-group → top group, faces alone) as a pure function over a `PeopleSnapshot`.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| | | |

---

## Open Questions

- [x] ~~Where does the People submenu show?~~ → File tiles of the explorer only.
- [x] ~~Does a face alone get an entry?~~ → Yes, opening a person of that one face.
- [x] ~~How is the name asked from the group title?~~ → A popup like JavaScript's `prompt()`.
- [ ] A face alone: when is its person created (written to `people.json`)?
- [ ] Hidden people: listed in the submenu?
- [ ] What does an entry show — name / *Unnamed*, a face picture, a file count?
- [ ] Where does the name popup open?

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

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the People submenu show? | File tiles only | 2026-10-28 |
| 2 | A face alone of the photo: does the submenu offer it? | Yes, an entry too | 2026-10-28 |
| 3 | How does the group title ask the new name? | A popup like JavaScript's `prompt()` | 2026-10-28 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-28 |
| 5 | A face alone: when is its person created? | | |
| 6 | Hidden people in the submenu? | | |
| 7 | What does an entry show? | | |
| 8 | Where does the name popup open? | | |

---

*Last updated: 2026-10-28*
