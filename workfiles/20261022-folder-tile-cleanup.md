# Folder Tile Cleanup

> Working document — the folder tiles of the file explorer lose their frame and their checkerboard,
> their name is drawn with a light drop shadow instead of the white halo, and a prefix shared by
> several folders is drawn small above the rest of the name.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

`workfiles/20261022-explorer-transparency-checkerboard.md` made the folder tiles what they are today
(`UI/ThumbnailGrid.PaintPicture`): the whole 4:3 box filled with the transparency checkerboard
(`Checkerboard.Fill`), Windows' folder thumbnail — or the drawn folder (`PaintFolderGlyph`) — over it,
the folder's name in bold on its front flap, almost black over a white halo (`PaintFolderName`), then
the tile's frame (`SystemColors.ControlDark`, 1 px).

On a folder, the checkerboard and the frame read as noise — a folder is not an image whose
transparency matters — and the white halo around the letters looks smudged. Long sibling names that
start alike (`Outils RS Alpha`, `Outils RS Beta`…) end up cut with `…` on the flap, the part that tells
them apart lost.

Goals:

1. **No checkerboard behind a folder**: the folder sits on the panel's own background.
2. **No resting frame around a folder tile**.
3. **The name legible without the halo**: a light drop shadow instead.
4. **A shared prefix drawn small** above the rest of the name, which keeps the large size.

Components: `UI/ThumbnailGrid.cs` only (`PaintPicture`, `PaintFolderName`, the rows it is given); the
docs that describe the folder tile (`GLOSSARY.md` / `GLOSSARY.fr.md` *Folder tile* row).

---

## Background and Frame

| Box | Today | Planned |
|---|---|---|
| A folder's tile (Thumbnails view) | Checkerboard over the whole box | **Nothing painted**: the panel's background shows around the folder |
| A folder's tile, resting | 1 px `ControlDark` frame | **No frame** |
| A folder's tile, hovered / selected | `HotTrack` 1 px / `Highlight` 2 px frame | **Kept** — interaction feedback |
| A file's tile, the Loading… slot, the URL zone | Checkerboard + frame | Unchanged |
| A Details folder row's thumbnail | Checkerboard + frame | **Same as the tile**: no checkerboard, no resting frame; hover / selection frames kept |
| A Details folder row's strip (files inside) | Checkerboard + frame per box | Unchanged — they are files |

- Applies to both pictures of a folder: **Windows' thumbnail** and the **drawn folder**.
- A folder tile whose thumbnail is not loaded yet shows the panel's background alone (no
  checkerboard), the folder appearing when it is loaded.

---

## Folder Name

- Stays where it is: on the folder's front flap (`FolderFlap` / the drawn folder's front panel),
  centered, bold, drawn as a path, a third of the flap's height, never under the tile names' size, two
  lines at most, cut with `…`.
- **The white halo goes** (`PaintFolderName`'s `halo` pen).
- **A light drop shadow** replaces it: the text stays **almost black** (32, 32, 32), over a **soft
  white shadow** offset down-right under the letters — the best contrast on the flap's yellow. GDI+
  has no blur: the softness is obtained by drawing the shadow path a few times at low opacity around
  its offset, or from a small bitmap scaled up — an implementation choice.

---

## Shared Prefix

When several folders of the shown list start with the same words, the shared part is drawn **small at
the top of the front flap**, followed by `›`, and the rest **large** below it, both on the flap:

```
Outils RS ›
Alpha
```

- **The prefix**: for each folder, the **longest run of whole leading words** it shares with at least
  one other folder of the list, compared ignoring case, **leaving at least one word** of its own name —
  `Outils RS` itself next to `Outils RS 2` gets no prefix.
- **Words** are separated by spaces, `-`, `_` and `.` (a run of them counts as one separator):
  `Outils RS - Alpha` and `Outils_RS_Beta` share `Outils RS`.
- **Siblings only**: a folder is compared with the folders of the shown list that have the **same
  parent folder** — in the folder view, the open folder's subfolders; in a search, the folders found
  under the same parent. Two folders of different parents never share a prefix.
- Each folder shows **its own text** for the prefix (its own case), never another's.
- **The small line**: one line, cut with `…`, in the tile names' size or a fraction of the large size
  (implementation choice), with the same shadow as the name.
- **The large part**: the rest of the name, the separator after the prefix dropped (`Outils RS - Alpha`
  → `Alpha`), on the space left — two lines at most, cut with `…`, as today.
- A folder sharing no prefix is drawn as today (whole name, large).
- Thumbnails view only; the tile's text under it (the file count) is unchanged; the Details row keeps
  `name (140)`.

---

## Documentation

- `GLOSSARY.md` *Folder tile* row: the folder over the panel's background, its name on the front flap
  under a light shadow, a prefix shared with other folders drawn small above the rest.
- `GLOSSARY.fr.md`: the same row in French, same commit.
- `README.md` folder view paragraph: one sentence — sibling folders starting alike show the shared
  part small above the rest of the name. `README.fr.md`: the same sentence in French, same commit.

---

## Test Impact

No unit test project exists in the repository (`src/` holds the app only). The prefix computation is
pure logic and testable, but creating a test project is out of this scope: **no test created or
updated**. Checked by hand on a folder with a Windows thumbnail, a folder without one, siblings sharing
a prefix (`Outils RS …`), siblings sharing none, the hovered and selected tiles.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Hover and selection frames of a folder tile: kept (interaction feedback), or removed too?~~ → Kept; only the resting frame goes
- [x] ~~Details view: a folder row's thumbnail without checkerboard and frame too, or unchanged?~~ → Same treatment; the strip's file thumbnails keep their checkerboard
- [x] ~~The shadow's colors: almost-black text with a soft light shadow, or white text with a soft dark shadow?~~ → Almost-black text, soft white shadow
- [x] ~~Where the small prefix line sits: at the top of the front flap above the large name, or on the folder's tab (top left)?~~ → Top of the front flap
- [x] ~~Word separators for the prefix: spaces only, or also `-`, `_`, `.`?~~ → Spaces, `-`, `_`, `.`
- [x] ~~The list the prefix is computed over: the folders shown together in the list (folder view and its search alike)?~~ → Siblings only: the shown folders with the same parent
- [x] ~~README: one sentence about the shared prefix in the folder view paragraph (EN + FR), or glossary only?~~ → Glossary and README, EN + FR

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-22

Scope set with the user (Q&A 1–4): the frame to remove is the tile's; the checkerboard behind a folder
gives way to the panel's background (files keep theirs); the halo gives way to a light drop shadow; the
subject is straightforward — one scout pass, done directly (a single file, `ThumbnailGrid.cs`).

Added by the user during the scoping (Q&A 5): several folders starting alike (`Outils RS xxxx`) show
the shared part small at the top, `Outils RS ›`, and the rest large below.

The scout found the three pieces in `ThumbnailGrid.PaintPicture` (checkerboard, frame) and
`PaintFolderName` (halo), and the folder rows built by `FileExplorerPanel` (folder view, folder view
search). No backlog row of `TODO-FEATURES.md` matches. Seven open questions listed above.

### Iteration 2 — 2026-10-22

Four open questions settled (Q&A 6, 8–10), from a mockup of three variants: the hover and selection
frames stay, only the resting frame goes; the name stays almost black over a soft white shadow; the
shared prefix sits at the top of the front flap; words split on spaces, `-`, `_` and `.`.

### Iteration 3 — 2026-10-22

Last open questions settled (Q&A 7, 11, 12): the Details view's folder thumbnail gets the same
treatment; the prefix is shared among siblings only — the shown folders with the same parent; the
glossary and the README (EN + FR) describe it. No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — none planned |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which frame to remove on folder tiles: the tile's, the drawn folder's outline, both? | The tile's frame | 2026-10-22 |
| 2 | What goes behind the folder instead of the checkerboard: the panel's background, a light flat fill? | The panel's background; files keep the checkerboard | 2026-10-22 |
| 3 | How to make the name legible without the halo: contrasting color alone, light drop shadow, name under the tile? | Light drop shadow | 2026-10-22 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-22 |
| 5 | *(user, unprompted)* Folders starting alike | Show the shared prefix small at the top (`Outils RS ›`), the rest large | 2026-10-22 |
| 6 | Hover and selection frames of a folder tile: kept or removed? | Kept | 2026-10-22 |
| 7 | Details view folder thumbnail: same treatment or unchanged? | Same treatment | 2026-10-22 |
| 8 | Shadow colors: dark text + light shadow, or white text + dark shadow? | Dark text, light shadow (mockup A) | 2026-10-22 |
| 9 | Small prefix line: top of the front flap, or on the folder's tab? | Top of the front flap | 2026-10-22 |
| 10 | Prefix word separators: spaces only, or also `-`, `_`, `.`? | Spaces, `-`, `_`, `.` | 2026-10-22 |
| 11 | Prefix computed over the folders shown together in the list? | Siblings on disk only (same parent) | 2026-10-22 |
| 12 | README sentence about the prefix (EN + FR), or glossary only? | Glossary and README | 2026-10-22 |
| 13 | Go for implementation: scope and where? | | |

---

*Last updated: 2026-10-22*
