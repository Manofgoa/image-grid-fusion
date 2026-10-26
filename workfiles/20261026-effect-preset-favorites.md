# Effect Preset Favorites

> Working document — save a cell's turned-on effects as a favorite of its file, described in free
> text, searchable by that text, and dropped back into a cell with its effects.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A favorite today is a file: its absolute path in `favorites.txt`, shown as a tile while the search
box is empty, dragged into a cell where it arrives with every effect at its default state.

An **effect preset** (*preset d'effets*) is a favorite of a new kind: **a file + the effects turned on
in its cell + a description** written in a multi-line text box. Typical use: a video trimmed to a
precise moment (Frames: From / To, starting point), a crop, a turn — saved once, found again by the
words of its description, dragged into a cell where the video arrives already trimmed and cropped.

- **Linked to its file**: a preset is applied by bringing its file into a cell — it never applies to
  another image. A file may have **several presets** (three moments of one video), each its own tile.
- **Turned-on effects only**: the effects off in the cell are not saved; applied, they stay at their
  default state.
- **Created** from a dedicated button of the effects toolbar, and by the ✥ handle released over the
  file explorer when the cell has effects.
- **Searchable**: a search matches the preset's description, in addition to its file's name.

Components concerned (from the exploration):

| Area | Where |
|---|---|
| Favorites store | `Explorer/Favorites.cs` — path-only, unique per path (`_set`, `Drop`) |
| Favorites list, tiles, heart, drag | `UI/FileExplorerPanel.cs` (`RefreshRows`, `ToggleFavorite`, `DoDragDrop`), `UI/ThumbnailGrid.cs` (`IsFavorite`, excerpt) |
| ✥ released over the explorer | `MainForm` `ReleasedOffGrid` → `AddToFavorites(SourceImage)` — the image carries its `Look` |
| Effect state | `Composition/ImageLook.cs` — one nullable record per effect, Zoom / Rotate / Flip flat with an `Activations` mask, kept settings in private `Kept*` slots |
| Arrival of a file in a cell | `MainForm.AddFilesAsync` → `GridPreview.Add` → `ShowWhole` (arrival zoom) |
| Search | `Explorer/FileSearch.cs` (`Rank.Of`, `CompareTier`, `Fold`), `ContentIndex` (excerpt), `HighlightSpans` |

---

## Concept

| | File favorite (today) | Effect preset (new) |
|---|---|---|
| Holds | A file | A file + its turned-on effects + a description |
| Per file | One | Any number, plus the file favorite itself |
| Identity | The path | Its own id |
| Dragged into a cell | Arrives at the default state (arrival zoom) | Arrives with its saved effects |
| Found by | Shown while the search box is empty | Shown while the search box is empty, **and** found by a search on its description or its file's name |

A preset is a stored copy of effect settings, not the cell's state persisted: RULES.md § Scope and
State ("not persisted") still holds for the cells — the docs step adds that nuance.

---

## What a Preset Saves

The effects **turned on** in the cell when the preset is saved, with their settings — never the
kept settings of an effect turned off (`Kept*`).

| Effect | Saved as | Reapplied to the same file |
|---|---|---|
| Crop | Its sides in fractions of the image as loaded, its ratio | Exact — same content, whatever the orientation |
| Frames | Trim From / To (frame indexes), starting point (fraction), frozen | Exact; clamped if the file changed |
| Rotate | Quarter turn, fine angle | Exact |
| Flip | X / Y | Exact |
| Colors, Volume | Their scalars | Exact |
| Animations | Type, cycle | Exact |
| Background | Fill, color, opacity, edge settings | Exact |
| Zoom | Free zoom, focus, fit mode | Relative to the new cell's fit — lands as it did in a cell of the same shape |
| Blur | Rectangle in fractions of the cell, strength, kind | In fractions of the new cell |

- **Zoom untouched since arrival is not saved**: an arrived image carries the arrival zoom
  (`ImageLook.ZoomOnArrival`) — it is the default behaviour, not a choice, so it counts as off.
  Applied, the preset then gets a fresh arrival zoom in its new cell.
- **Background**: on by default (RULES.md § The Background Exception). Saved when on, like the
  others; its *off* state is the subject of an open question.

### Applying

A preset tile brought into a cell — dragged, or any other route a tile has — loads its file as a drop
does (`MainForm.AddFilesAsync`), then sets the image's look to the saved effects **before**
`GridPreview.Add`:

- Built inside `ImageLook` (its members are private-init) by one factory, applying in order:
  orientation (Rotate, Flip), then Crop in as-loaded coordinates, then the others, Zoom and its focus
  last — rotation and flip transform the crop and the focus.
- `GridPreview.ShowWhole` skips an image whose Zoom is on and not `ZoomOnArrival`, so a saved zoom is
  not overwritten by the arrival zoom.
- Everything else is an ordinary arrival: the grid starts over (§ Preview Playback), one undo step.
- The drag carries the file (`DataFormats.FileDrop`, so dropping a preset into another app still
  gives the file) **plus** the preset's id in a private data format, read by `AddFilesAsync`.

---

## Creating a Preset

### The Effects Toolbar Button

- A **★ Preset…** button in the effects toolbar, just left of its **Reset**, as tall as the tabs.
- Enabled while a cell holding an image is selected (disabled with the rest of the toolbar
  otherwise, and while exporting).
- A cell with no file (pasted image, pasted or dropped text) is first saved into
  `favorites-from-pasted\`, as a pasted favorite is today (`PastedFavorites`, `AdoptFile`).

### The ✥ Handle Released Over the Explorer

- A cell **with effects** — at least one effect turned on beyond the default state (Background at
  its defaults and an untouched arrival zoom don't count) — opens the same dialog.
- A cell without effects makes a file favorite, as today.

### The Dialog

A small modal dialog:

- Its title, **Save effect preset**, then the file's name.
- The list of the effects it saves, one line each with its key values (`Frames — 0:12.04 → 0:18.10`,
  `Crop — 16:9`), read-only.
- A multi-line **Description** text box, focused, optional (empty allowed), `Enter` a new line,
  `Ctrl+Enter` validating.
- **Save** / **Cancel**.

---

## Explorer

### Storage

Favorites become a list of **entries**: a file favorite (a path) or a preset (an id, a path, a
description, the date it was saved, its effects). The order — newest first — covers both kinds.
The file holding them is the subject of an open question.

Effects are written by hand under named keys, as `settings.json` is (`System.Text.Json`,
`JsonObject`), never by serializing `ImageLook`. A format version leaves room for later effects.

### Tiles

- Shown in the favorites list (search box empty), among the file favorites, newest first.
- How a preset tile looks — badge, description shown or not — is the subject of an open question.
- The tile's context menu adds **Edit description…** (the dialog, description only) on a preset.
- **Copy**, **Copy PNG**, **Copy full path**, **Open file location** act on its file, as on any tile.
- A preset whose file is the one of a **pasted favorite** keeps that file: the file goes to the
  Recycle Bin only once no favorite and no preset uses it any more.

### Search

- A non-empty search in the **search view** also goes through the presets, injected as extra entries
  next to the index's (`FileSearch.Search` reads `_index.Entries` only today): a preset may point to
  a file outside the base folder.
- A preset matches by its **file's name** (the name tier, as the file does) or by its **description**.
- The description is folded with `FileSearch.Fold` at save time; every query word must be found in
  the name, the path or the description (AND, as today).
- A preset found by its description ranks in **its own tier**, after the path matches and before the
  content matches (`ContentGrade.Description`, ahead of Sure, so `CompareTier` barely changes).
- Details view: a preset found by its description shows the **words around** the one found, the
  search's words highlighted in yellow, as a content match does (`HighlightSpans`, the excerpt's
  windowing generalized to take another source than `ContentIndex`).
- The folder view lists the disk: presets don't appear there, neither browsed nor searched.

---

## Test Impact

No unit test project exists in the repository (`src/` holds the app only); creating one is out of
this scope: **no test created or updated**. Checked by hand: a video trimmed and cropped saved as a
preset, two presets of one video, a preset dropped into a cell of another shape, a search on a word
of a description, the Details excerpt, the ✥ release with and without effects, a pasted image saved
as a preset, a restart reading the file back.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Is a preset linked to its file, or applicable to any image?~~ → Linked to its file; several
  presets per file.
- [x] ~~Which effects does a preset save?~~ → The turned-on effects only.
- [x] ~~How is a preset created?~~ → A dedicated button of the effects toolbar, and the ✥ handle
  released over the explorer when the cell has effects.
- [ ] Storage: one `favorites.json` replacing `favorites.txt` (migrated once), or `favorites.txt`
  kept for the files plus a separate presets file?
- [ ] How does a preset tile look (badge, description shown on the tile or only in its tooltip)?
- [ ] The heart on a preset tile: clicking it deletes the preset (with a confirmation), or no heart
  and a **Delete preset** entry in the context menu?
- [ ] The ✥ released over the explorer with effects: does the dialog also offer **File only** (a
  plain file favorite, as today), next to Save and Cancel?
- [ ] Which search criterion covers the description: **Aa** (name), **💡** (content), or both?
- [ ] A preset whose file is gone: kept (its drag saying the file is missing), or dropped like a
  file favorite whose file is gone?
- [ ] Editing a preset: the description only, or also **Update effects** from the selected cell?
- [ ] The Background turned off in the cell: saved as off (the preset arrives without a fill), or
  ignored like any effect off (it arrives with its default fill)?

---

## Design Iterations

### Iteration 1 — 2026-10-26

Initial design from the request and the scoping batch (Q&A 1–4): a preset is a favorite of its file
carrying the cell's turned-on effects and a free-text description; several per file; created from a
**★ Preset…** button of the effects toolbar and from the ✥ handle released over the explorer when the
cell has effects; applied by bringing its tile into a cell, the look set before `GridPreview.Add`;
searchable by its file's name and its description, the description in a tier of its own between the
path and the content matches. Exploration: one scout pass, three read-only agents (favorites store,
search, effect state). Eight questions left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project in the repository |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Is a preset linked to its source file, or applicable to any image? | Linked to its file | 2026-10-26 |
| 2 | Which effects does a preset save? | Only the turned-on effects | 2026-10-26 |
| 3 | How is a preset saved from a cell? | Dedicated button + ✥ | 2026-10-26 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward | 2026-10-26 |
| 5 | Storage: one `favorites.json` replacing `favorites.txt`, or a separate presets file? | | 2026-10-26 |
| 6 | How does a preset tile look (variants A / B / C of the mockup)? | | 2026-10-26 |
| 7 | The heart on a preset tile: delete with confirmation, or no heart and a menu entry? | | 2026-10-26 |
| 8 | ✥ released with effects: does the dialog also offer **File only**? | | 2026-10-26 |
| 9 | Which search criterion covers the description? | | 2026-10-26 |
| 10 | A preset whose file is gone: kept or dropped? | | 2026-10-26 |
| 11 | Editing a preset: description only, or also **Update effects**? | | 2026-10-26 |
| 12 | The Background off in the cell: saved as off, or ignored? | | 2026-10-26 |

---

*Last updated: 2026-10-26*
