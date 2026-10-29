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
  and while the face analysis is off. Its text: **People**, **first** in the menu, followed by a
  separator. One computation, `Explorer/PhotoPeople.Of`; a face found after the last grouping is left
  out until the groups are computed again.
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
  (`FaceGrouping`'s order), the hidden ones last in the same order. The faces are **40 × 30** logical
  px, cut out off the UI thread one after the other, a blank picture of that size meanwhile so the
  entries keep their height.
- **Clicked**: the explorer switches to the **People view** (`ChangeMode`, remembered as if picked in
  the views drop-down) and opens the group (`OpenGroup`), the search box cleared.
- **A face alone, clicked**: its **person is created at once** — the face pinned to it, as `PersonOf`
  does for an unnamed group — and `people.json` saved (status line *New person: Unnamed*); once the
  groups computed again hold it (`_openAfterRegroup`, checked in `RegroupAsync`), its group of one face
  (a person with a face stays a group, `FaceGrouping.Compute`) is opened. Opening another group
  meanwhile cancels that. It stays a person even if it is never named.

---

## Group Title

- A group open in the People view (not the people list, not the *Hidden* tile) shows a **title at the
  top of its list**: the group's name, or *Unnamed*, in a larger bold font — a strip of its own
  above the tiles, staying in place while they scroll, in the Thumbnails and Details views alike
  (`_groupTitle`, a label docked at the top of the list's host, its font 4 pt larger than the panel's). Hovered, it
  shows a hand cursor and the tooltip *Click to rename*.
- **Clicked** → a popup like JavaScript's `prompt()` asks the name — the existing `NameDialog`
  (*"The person's name:"*, the current name selected, OK / Cancel) — then the same path as *Rename…*
  (`RenameGroup`: `PersonOf`, `person.Name`, `SavePeople`).
- The popup opens **centered on the explorer panel**, not on the whole window — for the title and
  for *Rename…* / F2 alike: one `FileExplorerPanel.Rename(FaceGroup)`, `NameDialog.CenterOn`.

---

## Safer People Decisions

Added by Iteration 5 (⚙️ post-implementation).

### Confirmation Submenus

A decision that takes something away no longer happens in one click: its menu item opens a
**submenu** of a single item that does it.

| Menu item (unchanged) | Its submenu's item |
|---|---|
| *Not <name>* (a file of an open group) | *Remove from <name>* |
| *Hide* (a group) | *Hide <name>* |
| *Un-merge* (a sub-group) | *Un-merge <name>* |

*Rename…*, *Unhide*, *Open* and the drags stay as they are. `<name>` is the group's name or *Unnamed*.

### Undo — One Timeline

Every People decision is a **step of the undo history**, in one timeline with the grid's: Ctrl+Z takes
back the last action, whatever it was, and Ctrl+Y / Ctrl+Shift+Z redo it.

- **Which decisions**: all of them — *Not <name>*, a rename (the title, *Rename…*, F2), *Hide* /
  *Unhide*, a merge, *Un-merge*, a face dragged onto a group, a person created from a face alone.
- **How**: the people's state joins the step (RULES.md § Undo History: *a new piece of composed state
  joins the snapshot and the restore*): `GridState` gains a **people state** — an immutable copy of
  every person (`People.Snapshot()`, `PersonData`) and the decision that led to it, replaced by each
  decision (`FileExplorerPanel.SavePeople`). `MainForm.CaptureState` reads it, `MainForm.RestoreState`
  puts it back: the store takes the copy (`People.Restore`), `people.json` is saved, the groups computed
  again. No per-action inverse.
- **Committed like any step**: once the state settles (`GridHistory`), a decision being instant.
- **The status line** names it, as for the grid: `↶ Undone: Not Alice — 3 more` (`GridHistory.Describe`
  reading the decision's text).
- **Not decisions**: loading `people.json` at start-up and *Forget faces…* are no steps. They start a
  new **generation** of the people's state: the step already committed takes it without a new step
  (`GridHistory` rebased), and a step of an older generation restores the grid only, the people left as
  they are — *Forget faces…* stays final, as its confirmation says.
- Undo while the People view shows a group that the restore removes: the view goes back to the people
  list, as a regrouping already does.

---

## Documentation

- `README.md` / `README.fr.md` § People view: the **People** submenu of a file tile, the group title.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: *Face group* gains the title and the submenu, or a term of its own.
- `RULES.md` § People: the submenu and the title under *Decisions*.
- Iteration 5: `RULES.md` § People (decisions in the undo history, confirmation submenus) and
  § Undo History (the people's state in a step; the file explorer stays out, its People decisions
  aside); README EN + FR § People view and § Undo; GLOSSARY EN + FR *Undo history*, *Step*,
  *Face group*.

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
- [x] ~~Ctrl+Z on the People view: which decisions does it undo?~~ → All of them; Ctrl+Y redoes.
- [x] ~~Ctrl+Z: the People's own history, or one timeline shared with the grid's?~~ → One timeline.
- [x] ~~The *Not <name> › Remove* submenu: only *Not <name>*, or the other decisions too?~~ → *Hide* and
  *Un-merge* too.
- [x] ~~The folder idea: sent to the face-grouping-accuracy session?~~ → Not now: the user tests the
  new grouping first, once merged.

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

### Iteration 3 — 2026-10-28 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`feature/open-people-from-image`).

### Iteration 4 — 2026-10-28 — 🧭 Implementation choices

No rule broken. Choices the frozen design left open:

- The **People** submenu comes **first** in the tile menu, with its own separator.
- The computation is a pure function, `Explorer/PhotoPeople.Of` (record `PhotoPerson`); a face not in
  the last grouping is left out.
- Entry faces at **40 × 30** logical px, loaded off the UI thread, a blank placeholder meanwhile.
- Opening from the submenu goes through `ChangeMode`: the People view is **remembered** as if picked.
- A face alone opens **after the regrouping** that holds its new person (`_openAfterRegroup`); opening
  another group meanwhile cancels it; status line *New person: Unnamed*.
- The title is a label docked at the top of the list's host, the panel's font + 4 pt, bold; hidden in
  the people list, the *Hidden* tile, the invitation and the other views.
- `RenameGroup` now calls `Rename(FaceGroup)`, shared with the title; `NameDialog.CenterOn` places the
  popup on the explorer, kept inside the screen's working area.

### Iteration 5 — 2026-10-29 — ⚙️ Post-implementation — Safer People decisions

User feedback after testing:

1. *"Not this person"* was clicked by mistake: it should open a **submenu**, *Not <name> › Remove*,
   so that one click never takes a face out.
2. The People decisions should be **undone with Ctrl+Z**. This changes a recorded rule: RULES.md
   § People keeps them *out of the undo history*.
3. Most faces end up in one huge mixed *Unnamed* group; the user suggests separating them **by
   folder**. Not designed here: the grouping is being reworked by the face-grouping-accuracy session
   (`workfiles/20261028-face-grouping-accuracy.md`, not merged yet), where the idea belongs.

Lead from the code: `People.Snapshot()` already copies every person (`PersonData`), so a People step
can be the whole people state before a decision — restored, saved, regrouped — as `GridHistory` keeps
the whole grid state, never a per-action inverse. Open Questions added for points 1–2.

### Iteration 6 — 2026-10-29 — ⚙️ Post-implementation — Safer People decisions designed

Q&A 9–12: Ctrl+Z covers every People decision, in **one timeline** with the grid's; the confirmation
submenu applies to *Not <name>*, *Hide* and *Un-merge*; the folder idea waits for the new grouping.
Design written in § Safer People Decisions: the people's state joins `GridState`; loading and *Forget
faces…* start a new generation, never undone. This reverses RULES.md § People's *out of the undo
history*.

### Iteration 7 — 2026-10-29 — 🧭 Implementation choices

Go given for Iteration 6 (code, tests and documentation), in a worktree
(`feature/safer-people-decisions`). No rule broken. Choices the design left open:

- The submenus' items: *Remove from <name>*, *Hide <name>*, *Un-merge <name>* — the parent items keep
  their text and lose their click.
- `PeopleState` lives in `Explorer/People.cs`, next to `PersonData`; `People.Restore` rebuilds every
  person from a copy. Two states are equal only when they are the same object.
- The panel exposes `PeopleState` and raises `PeopleReset` on a new generation; `MainForm` answers it
  with `GridHistory.Rebase`. A restore of an older generation's step also rebases the step restored,
  so the history sees no change to commit and the redo steps are kept.
- A restore writes `people.json` quietly — the status line is `MainForm`'s `↶ Undone: <decision>` —
  and reports only a write failure.
- The decision's text in the status line is the one `SavePeople` already reported (`Named: Alice`,
  `Hidden: Bob`…).
- Documentation also covers README § Undo & redo (EN + FR), where the history's coverage is listed.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 7 | 2026-10-28, 2026-10-29 | `PhotoPeople`, `NameDialog.CenterOn`, `FileExplorerPanel`; submenus, `PeopleState`, `GridHistory.Rebase` |
| Unit tests | 3, 7 | 2026-10-28, 2026-10-29 | None — no test project in the repository |
| README (EN + FR) | 3, 7 | 2026-10-28, 2026-10-29 | § People view; § Undo & redo |
| Glossary (EN + FR) | 3, 7 | 2026-10-28, 2026-10-29 | *Face group*; *Step* |
| RULES.md | 3, 7 | 2026-10-28, 2026-10-29 | § People; § Undo History |

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
| 9 | Ctrl+Z: which People decisions? | All of them | 2026-10-29 |
| 10 | Ctrl+Z: own history or shared with the grid? | One timeline | 2026-10-29 |
| 11 | Submenu *› Remove*: which decisions? | Not, Hide and Un-merge | 2026-10-29 |
| 12 | Folder idea sent to the grouping session? | No, test the new grouping first | 2026-10-29 |

---

*Last updated: 2026-10-29*
