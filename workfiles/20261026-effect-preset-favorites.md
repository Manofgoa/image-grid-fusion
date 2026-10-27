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
  Applied, the preset then gets a fresh arrival zoom in its new cell. An image **moved** while its
  zoom is still the arrival one is saved as an arrival zoom with its focus: applied, the new cell
  computes the arrival zoom, the focus kept.
- **Background**: on by default (RULES.md § The Background Exception), so turning it off is a
  choice: saved when on, like the others, **and saved as off** when off — the preset then arrives
  without a fill. The one effect whose *off* is saved. On at its default settings, it is not written:
  the file arrives with that default anyway.

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
- Here the dialog has a third button, **File only**: the plain file favorite of today, its effects
  not saved.

### The Dialog

A small modal dialog:

- Its title, **Save effect preset**, then the file's name.
- The list of the effects it saves, one line each with its key values (`Frames — 0:12.04 → 0:18.10`,
  `Crop — 16:9`), read-only.
- A multi-line **Description** text box, focused, optional (empty allowed), `Enter` a new line,
  `Ctrl+Enter` validating.
- **Save** / **Cancel** — **Save** / **File only** / **Cancel** when opened by the ✥ handle.
- **Edit description…** reopens it on an existing preset: the description only, the effects list
  read-only, **Save** / **Cancel**.

---

## Explorer

### Storage

Favorites become a list of **entries**: a file favorite (a path) or a preset (an id, a path, a
description, the date it was saved, its effects). The order — newest first — covers both kinds.

- They live in **one `favorites.json`** next to the exe, replacing `favorites.txt`, read and written
  by `Explorer/Favorites.cs` only, rewritten whole through a `.tmp` file as today.
- **Migration, once**: with no `favorites.json` and a `favorites.txt`, the paths are read in their
  order as file favorites, the JSON written, then `favorites.txt` deleted. A `--new-instance`
  instance migrates nothing (RULES.md § Single Instance): it reads `favorites.txt` as it is — and
  writes `favorites.json` on a change of favorites, from what it read; the normal instance then finds
  the JSON already there and only deletes the old file.
- Effects are written by hand under named keys, as `settings.json` is (`System.Text.Json`,
  `JsonObject`), never by serializing `ImageLook`. A format version leaves room for later effects.
- A file favorite stays **unique per path**, as today; presets are identified by their own id.

### Tiles

- Shown in the favorites list (search box empty), among the file favorites, newest first.
- **Two lines** under the thumbnail: the file's name, grey as every tile's, then the **first line of
  the description** in the text color (none when the description is empty); the whole description
  in the tile's **tooltip**, above the path. A list holding presets gives every tile the second
  line's height, so the rows stay even.
- A **★ FX** badge — pale blue, dark blue text — at the thumbnail's **top-right** corner: the heart's
  medallion holds the top-left one.
- The **heart**, full, deletes the preset — after a confirmation (*Delete this preset?*, its
  description quoted), since the description would be lost. A file favorite's heart is unchanged.
- The tile's context menu adds **Edit description…** on a preset — the description only.
- **Copy**, **Copy PNG**, **Copy full path**, **Open file location** act on its file, as on any tile.
- A preset whose file is the one of a **pasted favorite** keeps that file: the file goes to the
  Recycle Bin only once no favorite and no preset uses it any more.
- A preset whose **file is gone** is **kept**: its tile shows the missing thumbnail, its drag or
  double-click says the file is missing in the status line, and only its heart removes it — a
  description is never dropped silently. (A file favorite whose file is gone keeps today's rule.)

### Search

- A non-empty search — in the **search view** and in the **folder view** alike — also goes through
  the favorites (`FileSearch.SearchFavorites`), merged into the index's matches: every preset, and
  every file favorite not found already, wherever it is — outside the base folder or the open folder
  (Iteration 6).
- A preset matches by its **file's name** (the name tier, as the file does) or by its **description**.
- The description is searched under the **Aa** criterion — the user's own words, like a name. With
  💡 alone, the presets are not searched: they hold no content of their own (their file is still
  found by its content, as a file).
- The description is folded with `FileSearch.Fold` at save time; every query word must be found in
  the name, the path or the description (AND, as today).
- A preset found by its description ranks in **its own tier**, after the path matches and before the
  content matches: every match carries its tier as a number (`SearchMatch.Tier`, `PresetMatch.Tier`)
  and the presets (`FileSearch.SearchPresets`, newest first) are merged into the files' list, ahead of
  the files of their tier. The caption counts them with the files; `*` lists files only.
- Details view: a preset found by its description shows the **words around** the one found, the
  search's words highlighted in yellow, as a content match does (`HighlightSpans`, the excerpt's
  windowing generalized to take another source than `ContentIndex`).
- The folder view **browsed** lists the disk: presets don't appear there; its **search** finds them (Iteration 6).

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
- [x] ~~Storage: one `favorites.json` replacing `favorites.txt` (migrated once), or `favorites.txt`
  kept for the files plus a separate presets file?~~ → One `favorites.json`, `favorites.txt` migrated
  once.
- [x] ~~How does a preset tile look (badge, description shown on the tile or only in its tooltip)?~~
  → Variant C: ★ FX badge, the file's name then the description's first line, greyed; the whole
  description in the tooltip.
- [x] ~~The heart on a preset tile: clicking it deletes the preset (with a confirmation), or no heart
  and a **Delete preset** entry in the context menu?~~ → The heart deletes it, after a confirmation.
- [x] ~~The ✥ released over the explorer with effects: does the dialog also offer **File only** (a
  plain file favorite, as today), next to Save and Cancel?~~ → Yes: Save / File only / Cancel.
- [x] ~~Which search criterion covers the description: **Aa** (name), **💡** (content), or both?~~
  → **Aa**.
- [x] ~~A preset whose file is gone: kept (its drag saying the file is missing), or dropped like a
  file favorite whose file is gone?~~ → Kept; only its heart removes it.
- [x] ~~Editing a preset: the description only, or also **Update effects** from the selected
  cell?~~ → The description only.
- [x] ~~The Background turned off in the cell: saved as off (the preset arrives without a fill), or
  ignored like any effect off (it arrives with its default fill)?~~ → Saved as off.

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

### Iteration 2 — 2026-10-26

The eight open questions answered (Q&A 5–12), every one on the recommended option: one
`favorites.json` replacing `favorites.txt`, migrated once (a `--new-instance` instance migrating
nothing); preset tiles in variant C of the mockup (★ FX badge, file name over the description's first
line, the whole description in the tooltip); the heart deleting a preset after a confirmation; the ✥
dialog offering **File only**; the description searched under **Aa**; a preset whose file is gone
kept until its heart removes it; **Edit description…** editing the description only; the Background
turned off saved as off. No question left open.

### Iteration 3 — 2026-10-26 — ✅ Implemented

Go given: **Code, unit tests and documentation**, in the **current checkout** — `main`, the standing
choice for this repository (no Branch Gate question). Scope frozen as the sections above stand.

### Iteration 4 — 2026-10-26 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state, the domain sections updated:

- **Badge corner**: the ★ FX badge sits at the thumbnail's **top-right** corner — the heart's medallion
  already holds the top-left one, which the design took for the other.
- **Description line color**: the text color, not grey — every tile's name is already grey, so a
  grey description would not read apart from it.
- **Even rows**: a list holding presets gives every tile the second line's height.
- **Moved on arrival**: an image moved while its zoom is still the arrival one saves an arrival zoom
  with its focus (the design only said an untouched arrival zoom is not saved).
- **Background at its defaults** is not written: the file arrives with that default anyway.
- **Migration after a test instance**: a `favorites.json` already there (written by a `--new-instance`
  instance from the `favorites.txt` it read) is kept, the old file only deleted.
- **Search merge**: tiers as numbers on every match (`SearchMatch.Tier`), the presets placed ahead of
  the files of their tier; the caption counts them.
- **The effects' JSON** is written by each effect record (`ToJson` / `FromJson`, read through
  `PresetJson`): their settings are private-init, only the record can build itself back.
- **Dialog for a cell without a file**: it names *Pasted image* / *Pasted text — saved into
  favorites-from-pasted*; the file is saved only on **Save** (or **File only**), never on Cancel.
- **Checked**: no test project — a throwaway console harness in the session's scratchpad (not in the
  repository) ran 30 checks: the JSON round trip of every effect, the moved arrival zoom, the
  Background off and colored, `favorites.txt` read with its duplicate dropped, presets added,
  reloaded, described, removed, a file favorite removed leaving its presets, the description search
  accents folded, the file-name search. It found one bug, fixed: the focus written as floats did not
  read back from an unparsed JSON object (`93488bb`). The UI itself — dialog, tiles, drag — is left to
  the user's hand test.

### Iteration 5 — 2026-10-27 — ⚙️ Post-implementation — Badge drawn centered

The user's test: the ★ FX badge looked badly drawn — uneven margins, its content not centered (the ★
drawn by a fallback font, the text box's leading and descent counted as content). It is now drawn as
shapes: a five-pointed star polygon as tall as the letters' capitals, then *FX* as a glyph outline,
both measured on their ink, centered together with even margins all round, anti-aliased.

The same message asked that the search look into the presets' descriptions too: it already does — a
*contains* match on every word, accents and case folded, in the file's name or the description,
under **Aa** (checked on a copy of the user's favorites: *déplaisant*, *extrêmement*, *vous trouve*
all find the preset). Nothing changed there; the user retests.

### Iteration 6 — 2026-10-27 — ⚙️ Post-implementation — Favorites and presets found in the folder view

The user's retest: the preset was not found because the explorer was in the **folder view**, whose
search the design kept to the disk. Request: *even in the folder view, the favorites **and** the
presets must be found*.

- A search in the **folder view** now goes through the favorites too, as the search view does: the
  presets by their file's name or description, and the **file favorites** by their name — **wherever
  they are**, below the open folder or not — merged into the files by tier.
- The **file favorites** join the search view's results the same way: one not already found as a
  file of the index (outside the base folder, dropped from the Explorer) shows by its name. A file
  found both ways shows once.
- Under **Aa** only, as before; `*` still lists files only.

### Iteration 7 — 2026-10-27 — ⚙️ Post-implementation — Badge as a cached, pixel-exact image

The user's second look at the badge: a border bleeding right and below, unequal left and right
margins, neither the star nor the text centered vertically. Causes: the grid paints with a half-pixel
offset (`PixelOffsetMode.HighQuality`), which shifted the fill and the border; the content stood on
fractional positions. The user asked for it to be made an image kept by the app.

- The badge is drawn **once into a bitmap** (`ThumbnailGrid.PresetBadge`), kept by the grid until its
  name font or the resolution changes, and copied pixel for pixel onto each preset tile.
- In it: the capitals of *FX* scaled to a whole number of pixels on whole rows, the 1 px border inside
  the bitmap, the star from the left margin and the text up to the right one, the gap taking the
  fraction — equal margins on every side; the star centered on the letters' middle, as a font
  centers ★, its tip a pixel above the capitals, its lower points on their base.
- Checked offline at 96, 120 and 144 DPI: margins 3 / 3 / 5 px on each side, star and letters
  sharing their rows.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-26 | `LookPreset` and the effects' JSON, `Favorites` in `favorites.json`, tiles, search, dialog, ★ Preset…, ✥ release, drop and activation (`7a91a91`…`5bf6747`) |
| Unit tests | 4 | 2026-10-26 | Not applicable — no test project in the repository; a throwaway harness ran 30 checks (Iteration 4) |
| README | 4 | 2026-10-26 | README + README.fr, GLOSSARY + GLOSSARY.fr, RULES.md § Effect Presets |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Is a preset linked to its source file, or applicable to any image? | Linked to its file | 2026-10-26 |
| 2 | Which effects does a preset save? | Only the turned-on effects | 2026-10-26 |
| 3 | How is a preset saved from a cell? | Dedicated button + ✥ | 2026-10-26 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward | 2026-10-26 |
| 5 | Storage: one `favorites.json` replacing `favorites.txt`, or a separate presets file? | One `favorites.json`, migrated once | 2026-10-26 |
| 6 | How does a preset tile look (variants A / B / C of the mockup)? | C — two lines: file name, then the description's first line | 2026-10-26 |
| 7 | The heart on a preset tile: delete with confirmation, or no heart and a menu entry? | Deletes, with a confirmation | 2026-10-26 |
| 8 | ✥ released with effects: does the dialog also offer **File only**? | Yes — Save preset / File only / Cancel | 2026-10-26 |
| 9 | Which search criterion covers the description? | Aa | 2026-10-26 |
| 10 | A preset whose file is gone: kept or dropped? | Kept | 2026-10-26 |
| 11 | Editing a preset: description only, or also **Update effects**? | The description only | 2026-10-26 |
| 12 | The Background off in the cell: saved as off, or ignored? | Saved as off | 2026-10-26 |

---

*Last updated: 2026-10-27*
