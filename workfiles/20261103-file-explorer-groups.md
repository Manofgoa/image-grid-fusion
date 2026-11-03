# File Explorer Groups

> Working document — automatic groups in the file explorer's lists: sibling folders sharing their
> leading words (`Outils RS`) gathered under a titled header, files with human names set apart from
> files with automatic names (GUID, hash, camera stamp…), the groups before the rest.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The folder view lists the open folder's subfolders A→Z, then its files in the sort order, as one
run of tiles. Sibling folders whose names start with the same words already get a special drawing
(`SharedPrefix`, `workfiles/20261022-folder-tile-cleanup.md`): the shared words small on the flap,
`Outils RS ›`, the rest large. Nothing gathers them: a long folder mixes them with every other one.

This work adds **groups**: sections of the list, each under a **titled header**, the groups first,
the items belonging to no group after them.

| Grouping | Applies to | Groups |
|---|---|---|
| **Shared prefix** | Folder view — folders | One section per prefix shared by sibling folders — `Outils RS (3)` |
| **Name patterns** | Folder view — files | One section per pattern of machine-made names — Camera, Screenshots, Messaging, Identifiers… — each one **turned off on its own** from a ⚙ submenu |
| **Match kind** | Search view — results | **Path** (found by its name or subfolders) / **Content** (found by what the file holds, OCR included) |

Two **on / off buttons**: one for the folder view's groups, one for the search results' groups.

Components: `UI/ThumbnailGrid` (layout, hit-test, keyboard, paint), `UI/FileExplorerPanel`
(`ListFolderAsync` and the search build the rows), `Explorer/SharedPrefix` (the prefixes),
`Explorer/FileSearch` (the tiers), `UI/AppSettings` (the buttons' and patterns' states), the ⚙ menu
(`MainForm`).

---

## Sections

- A **section** is a titled header line followed by its tiles; the next section starts on a **new
  line of tiles**, never after the previous one's last tile.
- The **header**: a thin line across the panel with its title and count, `── Outils RS (3) ────`;
  **always expanded**, no collapse (Q&A 3). Not a tile: not selectable, no menu, no drag.
- **Order** in the folder view: the folders first, then the files (Q&A 5); within each, the groups
  first, then the items without a group:
  1. one section per **folder prefix** group, A→Z by prefix;
  2. **Other folders** — the folders in no prefix group;
  3. **Named files** — the files in no pattern group: the human names, and the machine names whose
     pattern is turned off and no other pattern takes (Q&A 9, 10);
  4. one section per **file pattern** group, in the patterns' order.

  The files break "the groups first": the human names, the more telling, come before the pattern
  groups (Q&A 9).
- Inside a section, the existing order is kept: folders A→Z, files in the sort order.
- A header shows **even when the list holds a single section** (Q&A 13: the proposal to drop it
  was not kept) — with the groups on, every list is titled.

## Folder Prefix Groups

- Built on `SharedPrefix`: sibling folders sharing at least one whole leading word, compared
  ignoring case, the same separators (space, `-`, `_`, `.`).
- **Two folders at least** make a group — like the flap's prefix, which needs a sibling.
- Nested prefixes (`Outils RS Alpha`, `Outils RS Alpha 2`, `Outils RS Beta`): one group per
  **shortest** shared prefix (`Outils RS`), the largest group, no sub-sections (Q&A 13).
- `Outils RS` itself next to `Outils RS 2`: today it gets no prefix (it would keep no word of its
  own); it joins the `Outils RS` group anyway, as its first member (Q&A 13).
- **The flap is unchanged** inside a group (Q&A 6): `Outils RS ›` small over `Alpha`, groups on or
  off alike.

## File Name Patterns

A file whose name a machine made up rather than a person falls into the group of its **pattern**,
matched on the name without its extension (`Explorer/NamePatterns`, a new pure class). Every stamp
kind counts (Q&A 7). Proposed patterns, **in the order they are tried**:

| Group | Pattern | Examples |
|---|---|---|
| **Camera** | Camera / device stamp | `IMG_20220321_132643`, `VID_…`, `PXL_…`, `DSC_0042`, `DCIM…` |
| **Screenshots** | Screenshot stamp | `Screenshot_2026-11-03-10-24-09`, `Capture d'écran 2026-11-03 102409` |
| **Messaging** | Messaging app stamp | `WhatsApp Image 2026-11-03 at 10.24.09`, Telegram, Signal… |
| **Identifiers** | GUID, long hex run (hash, 16 hex digits or more), random token (10 characters or more, letters and digits mixed, no word-like run) | `3f2504e0-4f89-11d3-9a0c-0305e82c3301`, `a94a8fe5ccb19ba6…`, `F3xKz9aWkAA1b2c` |
| **Numbers** | Digits and separators only | `20220321_132643`, `1698765432123`, `001` |

- Everything else matches no pattern: `Wojak triste`, `Meme chat 2`, `facture EDF`.
- **Each pattern is turned off on its own** (Iteration 3): a **⚙ menu submenu**, one checkbox per
  pattern group, all checked by default, remembered in `settings.json`. A file whose pattern is
  turned off goes to the **next pattern it matches** still on, else among the files in no group —
  **no catch-all section** (Q&A 10): `IMG_20220321_132643` with Camera off matches no other pattern
  and lands among the Named files. A pattern's place in the table is that fallback order.
- **More patterns as they are identified**: a new pattern is a row of `NamePatterns` and a checkbox
  of the submenu, nothing else.

## Search Result Groups

The search view's results grouped by **how they were found** (Q&A 8):

- **Two groups only** (Q&A 12), each match in the group of its tier (`FileSearch` tiers):

  | Group | Holds |
  |---|---|
  | **Path** | Found by its name or its subfolders, an effect preset by its description, a person's file by their name — names all |
  | **Content** | Found by what the file holds: the text read (OCR, every grade), the tags and captions (Seen), the similarity tier |

- **Ranking** (Q&A 11): a file found **by its name and by its content** (its 💡) comes **first**, then
  by its name alone, then the rest of Path in today's tiers; Content keeps today's tiers. A file
  found both ways is listed once, in Path, with its 💡. Whether the search already knows a name
  match's content hit (`SearchMatch.ContentWord`) is checked at implementation.
- The new first tier applies with the groups off too (Q&A 13): it is a ranking, not a group.

## Toggles

- **Two on / off buttons** (Q&A 8, superseding Q&A 2's single one): the **folder view's groups** and
  the **search results' groups**, each turning its view's groupings on or off at once, **side by
  side, always visible**, in the tile size row next to the 🔳 / 📃 view buttons (Q&A 14); an icon
  each per § Icons, chosen at implementation, their tooltips saying which view they group; pressed
  while on.
- **Remembered** between sessions in `settings.json`; **on** by default.
- The pattern checkboxes live in a **⚙ menu submenu** (its name chosen at implementation, e.g.
  *Explorer groups*); they work under the folder view's button: it off, no group shows whatever
  they say.

## Views

- The **folder view** (prefix and pattern groups) and the **search view** (Path / Content). `*`, the
  favorites and the People view keep their lists as they are.
- Both displays: **Thumbnails** and **Details** (a header row between the rows) (Q&A 13).

## Grid Layout (technical)

`ThumbnailGrid` places a tile by its index alone (`TileBounds`: `index % _perRow`,
`index / _perRow`), and so do `IndexAt`, `UpdateExtent`, `PageSize` and the arrow keys. Sections
break that:

- `SetRows` takes the rows **with their section** (a section title, or none), and computes a
  **layout** once per list and width: each tile's line and column, each header's top, the extent.
  `TileBounds`, `IndexAt`, `UpdateExtent` read it; with no section the layout is today's.
- **Keyboard**: Left / Right move through the list order across sections; Up / Down go to the line
  above / below, the closest column, headers skipped; PageUp / PageDown by the lines a page holds.
- **Loads**: the pages per load still count tiles; a header takes no slot. The Loading… slot stays
  after the last tile loaded.
- The headers are painted by the grid, scrolled with it.

---

## Test Impact

The app has **no unit test project** (`src/` holds `ImageGridFusion` only), and none is created for
this work (Q&A 15): the name patterns (`NamePatterns`) and the grouping are checked by hand in the
app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [x] ~~Full section order: folder prefix groups → Other folders → Named files → Automatic names?~~
  → Folders first, then files (Q&A 5); the files' order revisited by Iteration 3, below
- [x] ~~With the patterns as groups of their own: the pattern sections before the files in no group,
  or the files with a human name first?~~ → Named files first (Q&A 9)
- [x] ~~A catch-all **Automatic names** group, or straight to the files in no group?~~ → No
  catch-all: straight to the Named files (Q&A 10)
- [x] ~~Nested folder prefixes: one group per shortest shared prefix (`Outils RS`), no sub-sections?~~
  → Yes, `Outils RS` itself its first member (Q&A 13)
- [x] ~~Inside a prefix group, does a tile's flap still show `Outils RS ›` small?~~ → Unchanged (Q&A 6)
- [x] ~~Which stamp names count as automatic?~~ → Camera, screenshots and messaging apps, all
  (Q&A 7); each its own group, turned off on its own (Iteration 3)
- [x] ~~Search ranking: "the file names and the 💡 as the most relevant"?~~ → A file found by its
  name and its content first, then by its name alone, then by its content alone (Q&A 11)
- [x] ~~Search groups: where go the presets' descriptions, the people's files, Seen, similarity?~~ →
  Path and Content only: descriptions and people in Path, Seen and similarity in Content (Q&A 12)
- [x] ~~The new first tier (name and content) with the search groups off too?~~ → Yes (Q&A 13)
- [x] ~~Section titles?~~ → `Other folders`, `Named files`, the pattern names (`Camera`…), as in the
  preview chosen at Q&A 9; `Path`, `Content` (Q&A 8)
- [x] ~~No header when the list would hold a single section?~~ → Not kept: the header always shows
  (Q&A 13)
- [x] ~~Views: the folder view only, or the search view too?~~ → Both, one button each (Q&A 8)
- [x] ~~Details view: headers too?~~ → Yes (Q&A 13)
- [x] ~~Toggles: placement, icon, default, the ⚙ submenu?~~ → Two buttons side by side next to
  🔳 / 📃, always visible (Q&A 14); on by default and remembered, as proposed; icons and the submenu's
  name chosen at implementation
- [x] ~~The shared-prefix principle applied to the **named files** too (`Meme chat 1`, `Meme chat 2`)?~~
  → Not chosen at Q&A 1: out of scope
- [x] ~~Create a unit test project for `NamePatterns` and the grouping?~~ → No, hand checks (Q&A 15)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-03

Initial request: automatic groups in the file explorer, on the `Outils RS` principle (that
prefix being a group itself, on top of its own drawing), the groups before the items without one;
other ideas wanted, within the subfolders for instance.

Scoping answers: besides the folder prefix groups, files with human names set apart from automatic
names (GUID…); one on / off button for every grouping; a header line per section, always expanded;
a straightforward subject (one scout pass).

Scout pass: `SharedPrefix.Of` already computes the folders' prefixes; `ListFolderAsync` builds the
folder view's rows, folders then files; `ThumbnailGrid` lays tiles out by index only — the sections
need a layout of their own (§ Grid Layout). No test project.

### Iteration 2 — 2026-11-03

Second batch answered: folders first, then files (Q&A 5); the flap keeps `Outils RS ›` small inside
a group (Q&A 6); camera, screenshot and messaging stamps all count as automatic (Q&A 7). Views
(Q&A 8): **two option buttons**, one for the folder view's groups, one for the search results',
replacing the single button of Q&A 2; the results ranked by relevance, the file names and the 💡
matches the most relevant; their groups **Path** / **Content** (OCR included). New sections:
§ Search Result Groups, § Toggles (was § Toggle).

### Iteration 3 — 2026-11-03

User request, mid-turn: as name patterns are identified (camera, etc.), offer each one as a group
that can be **turned off** from a **submenu of the ⚙ menu** (checkboxes); a file whose group is off
goes to another automatic group it matches, or among the files in no group. § Automatic File Names
becomes § File Name Patterns: one group per pattern — Camera, Screenshots, Messaging, Identifiers,
Numbers — in a fallback order, instead of one *Automatic names* group; their order against the
files in no group, and whether a catch-all remains, are open questions.

### Iteration 4 — 2026-11-03

Third batch answered: in the folder view the **Named files** come before the pattern groups
(Q&A 9); no catch-all — a file whose pattern is off and that no other pattern takes goes among the
Named files (Q&A 10); the search ranks a file found **by its name and its content** first (Q&A 11);
the search keeps **two groups**, Path (names, subfolders, presets' descriptions, people) and Content
(text read, Seen, similarity) (Q&A 12).

### Iteration 5 — 2026-11-03

Last batch answered: the shortest prefix makes the folder group, nested ones included; headers in
the Details view too; the name-and-content tier also with the search groups off; the proposal to
drop the header of a single-section list **not kept** — a header always shows (Q&A 13). Two toggle
buttons side by side, always visible (Q&A 14). No test project (Q&A 15). Every open question is
settled.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Besides the folders sharing their leading words, which groupings? (file name prefix, kind, period, similar bursts) | Tell human-named files from automatic names (GUID, etc.) | 2026-11-03 |
| 2 | How are the groupings turned on? (a Groups drop-down, always automatic, an on / off button) | An on / off button turning every grouping off | 2026-11-03 |
| 3 | How does a group show in the list? (collapsible header, group tile, header without collapse) | A header line, without collapse | 2026-11-03 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-11-03 |
| 5 | Full section order: prefix groups → Other folders → Named files → Automatic names? | Folders then files (recommended) | 2026-11-03 |
| 6 | Inside a prefix group, does the flap keep `Outils RS ›` small? | Unchanged | 2026-11-03 |
| 7 | Which stamp names count as automatic? | Camera, screenshots, messaging apps | 2026-11-03 |
| 8 | Which views get the groups? | Two option buttons for the groupings: folder view and results view. For the results, rank by relevance, the file names and the "bulbs" being the most relevant. Possible groups for the search: Path / Content (OCR included) | 2026-11-03 |
| 9 | Pattern sections before or after the files in no group? | Human names first | 2026-11-03 |
| 10 | A catch-all *Automatic names* group, or straight to no group? | No group (recommended) | 2026-11-03 |
| 11 | Search ranking: what "file names and bulbs the most relevant" means | Name AND content first | 2026-11-03 |
| 12 | Search groups: where the descriptions, people, Seen and similarity matches go | Path / Content only | 2026-11-03 |
| 13 | The remaining proposals (nested prefixes, `Outils RS` alone, single section, Details headers, name-and-content tier with the groups off) | Shortest prefix, headers in Details, name + content always; the single-section proposal not kept | 2026-11-03 |
| 14 | Toggles: placement, icon, default, the ⚙ submenu | Two buttons side by side | 2026-11-03 |
| 15 | A unit test project for the name patterns and the grouping? | No, hand checks | 2026-11-03 |
| 16 | Go for implementation? | | 2026-11-03 |

---

*Last updated: 2026-11-03*
