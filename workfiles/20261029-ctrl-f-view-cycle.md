# Ctrl+F View Cycle

> Working document — repeated Ctrl+F cycles the file explorer's views; the views drop-down
> reordered (Folders, People, Favorites) and given an icon per entry.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today `Ctrl+F` opens the file explorer if it is collapsed and puts the focus in its search box, its
text selected (origin: `workfiles/20261007-ctrl-f-search-focus.md`). The view — Favorites, Folders,
People — is only changed from the **views drop-down** (`FileExplorerPanel._modes`, its entries in
`ExplorerMode`'s order).

This work:

1. makes **repeated `Ctrl+F`** cycle the views, and `Ctrl+Shift+F` cycle them backward;
2. **reorders** the views: **Folders**, **People**, **Favorites**;
3. draws a **coloured icon** before each entry of the drop-down.

Components: `Explorer/ExplorerMode.cs`, `UI/FileExplorerPanel.cs` (the drop-down, `FocusSearch`,
`Mode`), `UI/MainForm.cs` (`ProcessCmdKey`), `UI/AppSettings.cs` (`ExplorerMode`'s fallback).

Not in the feature backlog (`workfiles/TODO-FEATURES.md`): no row matches.

---

## Keyboard

| Key | Search box not focused | Search box focused |
|---|---|---|
| `Ctrl+F` | As today: the panel opened if collapsed, the search box focused, its text selected. **The view does not change** | The **next view** in the cycle |
| `Ctrl+Shift+F` | The same as `Ctrl+F` | The **previous view** in the cycle |

- "Focused" means the **search box itself** (`_search.Focused`): a press from the URL zone's field,
  or from anywhere else, only brings the focus to the search box, as today.
- The cycle **wraps**: Folders → People → Favorites → Folders, and the reverse.
- **People is skipped** while the face analysis is off (`FileExplorerPanel.FaceAnalysis`): the cycle
  is then Folders ⇄ Favorites. The drop-down still lists People — picked by hand, it shows its
  invitation to turn the analysis on, as today.
- A view reached by `Ctrl+F` is **the same as one picked in the drop-down**: it goes through the
  same route (`ChangeMode`), so it is saved and refreshed the same way (`FolderViewChanged`).
- After the change, the search box **keeps the focus**, its text kept and **selected again**, so a
  third `Ctrl+F` keeps cycling and typing replaces the text.
- `Ctrl+Shift+F` is bound under the same condition as `Ctrl+F` in `MainForm.ProcessCmdKey` (the
  explorer editing text, or the focus out of any text box): no other binding uses it today.

---

## Views Order

- `ExplorerMode` becomes **`Folders, People, Favorites`**: the drop-down's entries are its values
  themselves (`Enum.GetValues<ExplorerMode>()`, their names as labels, `_modes.SelectedIndex =
  (int)mode`), so the order and the names live in one place.
- The view is **saved by name** (`AppSettings.ExplorerMode`): reordering the enum changes nothing for
  a saved setting.
- **Default view** at the very first launch — no `ExplorerMode` saved, no older folder-view flag —
  becomes **Folders**, the first entry. An older settings file holding the folder-view flag keeps its
  meaning: `1` → Folders, `0` → Favorites.
- The panel's own initial value (`[DefaultValue]`, `_folderView` initially on, the drop-down's initial
  index) becomes Folders too; the constructor applies that view at once (`ApplyView`: the breadcrumb,
  the folder hint).
- *Find similar images* still switches to Favorites (the search view), as today.

---

## Drop-Down Icons

- The drop-down is **owner-drawn** (`DrawMode.OwnerDrawFixed`): each entry draws a **Segoe UI Emoji
  glyph in a colour of its own**, then its name — the same technique as the tile tooltip
  (`UI/IndexToolTip`): GDI draws no full-colour emoji.

  | View | Glyph | Colour |
  |---|---|---|
  | Folders | 📁 `U+1F4C1` | Amber (214, 150, 0), the tooltip's 💡 one |
  | People | 👥 `U+1F465` | The tooltip's people colour (202, 80, 16) |
  | Favorites | ❤ `U+2764` | Red (232, 17, 35) |

  One table, `FileExplorerPanel.ModeIcons`, in `ExplorerMode`'s order; drawn by
  `FileExplorerPanel.DrawModeItem`.
- The **closed box** shows the selected entry's icon too, not only the open list.
- The drop-down is **widened** to hold the icon and the longest name: `ModesWidth` 84 → **96** logical
  px (icon 16, *Favorites* 47, arrow 17, gaps), scaled with the DPI like before.
- Selected / hovered entries keep the system's highlight; disabled, the icon and the text are drawn in
  `SystemColors.GrayText`.
- The drop-down's tooltip says that `Ctrl+F` again cycles the views, `Ctrl+Shift+F` backward.

---

## Documentation

- `README.md` / `README.fr.md`: the `Ctrl+F` line (repeated presses cycle the views, `Ctrl+Shift+F`
  backward, People skipped while the face analysis is off) and the views drop-down's order
  (**Folders**, **People**, **Favorites**, with their icons) in § Folder view.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: the *File explorer* row lists the views in the new order.

---

## Test Impact

No unit test project exists in the repository (`ImageGridFusion.slnx` holds the app only): nothing
is created or updated. The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~What does each Ctrl+F do?~~ → The first one focuses the search box as today; while the search
  box is focused, each next one moves to the next view.
- [x] ~~Extras to the cycle?~~ → `Ctrl+Shift+F` backward; People skipped while the face analysis is
  off; Folders the default view at the first launch.
- [x] ~~Icon style?~~ → Coloured Segoe UI Emoji glyphs, as in the tile tooltip, in the closed box too.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Initial design from the user's request and the scoping batch: the first `Ctrl+F` keeps today's
behaviour, the following ones (search box focused) cycle Folders → People → Favorites, `Ctrl+Shift+F`
backward, People skipped while the face analysis is off; `ExplorerMode` reordered, Folders the
default view at the first launch; the drop-down owner-drawn with a coloured emoji glyph per entry.

### Iteration 2 — 2026-10-29 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/ctrl-f-view-cycle`,
branch `feature/ctrl-f-view-cycle`, from `main` at `7e4b44a`).

### Iteration 3 — 2026-10-29 — 🧭 Implementation choices

No rule broken. Choices the frozen design left open:

- **One source for the entries**: the drop-down holds the `ExplorerMode` values themselves, their names
  as labels — the hard-coded `["Favorites", "Folders", "People"]` is gone.
- **Panel default**: `_folderView` starts on and the constructor calls `ApplyView`, so the panel is
  coherent before `MainForm` sets the saved view.
- **Older settings**: the folder-view flag saved as `0` by an older version still means Favorites; only
  a settings file holding neither value gives Folders.
- **Cycle**: `FileExplorerPanel.FocusSearch(int step)` (1 for `Ctrl+F`, -1 for `Ctrl+Shift+F`) and
  `NextMode(step)`; the view changes through `ChangeMode`, so it is saved like a pick in the drop-down.
  The press that opens a collapsed panel never cycles.
- **Width**: 96 logical px, measured (see § Drop-Down Icons) rather than the 112 first tried. At the
  default panel width (240 px), the search box is 12 logical px narrower than before — it was already
  narrow there; a wider panel is unaffected in practice. Reported, not changed (scope freeze).
- **Rendering**: on this machine Windows draws the glyphs in full colour through `TextRenderer` (the
  heart's red tones, the amber folder); the given colour applies where it draws them flat.
- **Checked in the app** (a `--new-instance` build of the worktree, no settings): Folders shown at the
  first launch with its 📁; `Ctrl+F` ×2 → Favorites (People skipped, the face analysis off);
  `Ctrl+Shift+F` → Folders; the open list shows 📁 👥 ❤ before the names.
- **Documentation**: README (EN + FR) § `Ctrl+F` and § Folder view; GLOSSARY (EN + FR) *File explorer*
  row. RULES.md unchanged: no rule names the views' order or Ctrl+F.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 2, 3 | 2026-10-29 | Views reordered, Ctrl+F / Ctrl+Shift+F cycle, drawn icons; checked in the app |
| Unit tests | 2 | 2026-10-29 | Not applicable — no test project |
| README | 3 | 2026-10-29 | README and GLOSSARY, EN + FR |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does each Ctrl+F do? | First press focuses the search box as today; while it is focused, each next press moves to the next view | 2026-10-29 |
| 2 | Extras to the Folders → People → Favorites cycle? | Ctrl+Shift+F backward; skip People while the face analysis is off; Folders the default view at the first launch | 2026-10-29 |
| 3 | Icon style before each entry? | Coloured emoji (📁 👥 ❤), Segoe UI Emoji glyphs as in the tile tooltip, in the closed box too | 2026-10-29 |
| 4 | Simple or tricky / long subject? | Simple — one scout pass | 2026-10-29 |

---

*Last updated: 2026-10-29*
