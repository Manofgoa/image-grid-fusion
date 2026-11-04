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
  3. one section per **series** — named files sharing their first words, 3 files or more —
     A→Z (Iteration 8);
  4. **Named files** — the files in no group: the human names, and the machine names whose
     pattern is turned off and no other pattern takes (Q&A 9, 10);
  5. one section per **file pattern** group, in the patterns' order, a known source's and a
     detected source's included.

  The files break "the groups first": the human names, the more telling, come before the pattern
  groups (Q&A 9).
- Inside a section, the existing order is kept: folders A→Z, files in the sort order.
- A header shows **even when the list holds a single section** (Q&A 13: the proposal to drop it
  was not kept) — with the groups on, every list is titled.

## Folder Prefix Groups

- Built on `SharedPrefix` (`SharedPrefix.Groups`): sibling folders sharing their **first word**,
  compared ignoring case, the same separators (space, `-`, `_`, `.`); the group's title is the
  longest run of leading words **all** of them share, as its first folder writes it —
  `outils_rs-gamma` joins `Outils RS Alpha` and `Outils RS Beta` under `Outils RS` (Iteration 7).
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
kind counts (Q&A 7). The patterns, **in the order they are tried** (Iteration 8):

| Group | Pattern | Examples |
|---|---|---|
| **Camera** | Camera / device stamp; `P` and exactly 7 digits | `IMG_20220321_132643`, `VID_…`, `PXL_…`, `DSC_0042`, `P1010001` |
| **Screenshots** | Screenshot stamp | `Screenshot_2026-11-03-10-24-09`, `Capture d'écran 2026-11-03 102409` |
| **Messaging** | Messaging app stamp | `WhatsApp Image 2026-11-03 at 10.24.09`, `IMG-20240614-WA0041`, Telegram, Signal… |
| **AI** | An AI generator's stamp | `OIG1 (10)`, `grok_video_2025-09-17-…`, `grok_image_xgsztgj`, `grok-video-<guid>`, `ChatGPT Image 11 mai 2025, 22_56_56`, `High_Performance_16x9_…` |
| **A known source** — one group each | A downloader's or an app's prefix, followed by anything | `ssstwitter.com_1751324860904`, `Snapinsta.app_video_…`, `twittervid.com_…`, `FaceApp_1677628281955`, `Videoleap_2025_07_08_…`, `VideoEditor_20240621_013522`, `VideoCapture_20230704-055003`, `sketch1669713742812`, `Clipped_image_20230917_082805` |
| **Generic names** | A browser's or a site's default name, with or without a number | `téléchargement (1)`, `images (2)`, `image-6`, `videoplayback (12)`, `maxresdefault (5)`, `full`, `unnamed`, `untitled` |
| **Identifiers** | GUID, long hex run (hash, 16 hex digits or more), random token (10 characters or more, letters and digits mixed, no word-like run), an X media id (15 characters, upper case, lower case and digits) | `3f2504e0-4f89-11d3-9a0c-0305e82c3301`, `a94a8fe5ccb19ba6…`, `F3xKz9aWkAA1b2c`, `GEhjfvEWQAE7EPO` |
| **Short codes** | 5 to 8 lower-case letters and digits, a digit before the last character — or any such run after a leading `-` | `d9omli`, `qy6xu8`, `-oong5f`, `-htaykg` — never `breton2`, `aoc1` |
| **Date stamps** | A date and a time, digits and separators only | `20230809_212344`, `2022-07-18 13.22.09` |
| **Timestamps** | A Unix time, in seconds or milliseconds (10 or 13 digits) | `1711612478`, `1746944459842` |
| **Numbers** | Any other name of digits and separators only | `001`, `12-3` |
| **A detected source** — one group each | A leading word shared by 3 files or more of the folder, followed by a stamp (digits, a date, a code), no pattern above taking them; titled by that word as its first file writes it | a future `snaptik_…`, `y2mate.com - …` |

- Everything else matches no pattern: `Wojak triste`, `Meme chat 2`, `facture EDF`.
- **Each pattern is turned off on its own** (Iteration 3): a **⚙ menu submenu**, one checkbox per
  pattern group, all checked by default, remembered in `settings.json`. A file whose pattern is
  turned off goes to the **next pattern it matches** still on, else among the files in no group —
  **no catch-all section** (Q&A 10): `IMG_20220321_132643` with Camera off matches no other pattern
  and lands among the Named files. A pattern's place in the table is that fallback order.
- **More patterns as they are identified**: a new pattern is a row of `NamePatterns` and a checkbox
  of the submenu, nothing else; a new known source, a row of the known sources.
- **The submenu** (Iteration 8): **Series** first, then one checkbox per pattern, the known sources in
  a **Sources ▸** submenu of their own, one checkbox each, and one **Detected sources** checkbox for
  every source found by itself — all checked by default.

## File Series

Named files sharing their first words, the `Outils RS` principle applied to files (Iteration 8):

- A series is **3 files or more** of the folder whose names share their first word — compared
  ignoring case and accents, words split on spaces, `-`, `_` and `.` — titled by the longest run of
  leading words all of them share, as its first file writes it: `marlène schiappa …` (32),
  `homme soja …` (22), `citation …` (12).
- Only the files **no pattern took** are grouped in series: a pattern always wins.
- **Turned on or off** by the submenu's **Series** checkbox, on by default; with it off, they stay
  among the Named files.

## Search Result Groups

The search view's results grouped by **how they were found** (Q&A 8):

- **Every search** gets them — the search view's and a search typed in the folder view; `*`, the
  favorites and *Similar to …* stay unsectioned (Iteration 7).
- **Two groups only** (Q&A 12), each match in the group of its tier (`FileSearch` tiers):

  | Group | Holds |
  |---|---|
  | **Path** | Found by its name or its subfolders, an effect preset by its description, a person's file by their name — names all |
  | **Content** | Found by what the file holds: the text read (OCR, every grade), the tags and captions (Seen), the similarity tier |

- **Ranking** (Q&A 11): a file found **by its name and by its content** (its 💡) comes **first**, then
  by its name alone, then the rest of Path in today's tiers; Content keeps today's tiers. A file
  found both ways is listed once, in Path, with its 💡. Whether the search already knows a name
  match's content hit (`SearchMatch.ContentWord`) is checked at implementation: it did not — a
  file found by its path was never looked for in its content. Now a file whose name holds every
  word, with the content search on (💡), is looked for there too; holding them all, it ranks first
  (tier −1, `Rank.ByNameAndContent`), its light bulb shown (Iteration 7).
- The new first tier applies with the groups off too (Q&A 13): it is a ranking, not a group.

## Toggles

- **Two on / off buttons** (Q&A 8, superseding Q&A 2's single one): the **folder view's groups** and
  the **search results' groups**, each turning its view's groupings on or off at once, **side by
  side, always visible**, in the tile size row next to the 🔳 / 📃 view buttons (Q&A 14): 🗂️ **Folder
  groups** and 🗃️ **Search groups**, their tooltips saying which view they group; pressed while on
  (Iteration 7).
- A third button between them, 📚 **Series**, turns the series on or off — the same state as the
  submenu's **Series** checkbox, both kept in step, remembered with the groups turned off
  (Iteration 10).
- **Remembered** between sessions in `settings.json`; **on** by default.
- The pattern checkboxes live in the **⚙ menu's File explorer groups**, after *File explorer pages
  per load*: 📷 Camera, 🖥️ Screenshots, 💬 Messaging, 🆔 Identifiers, 🔢 Numbers, each with examples in its
  tooltip; they work under the folder view's button: it off, no group shows whatever they say
  (Iteration 7).

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

### Iteration 6 — 2026-11-03 — ✅ Implemented

Go given (Q&A 16): code, tests and documentation, in a worktree on `feature/file-explorer-groups`.
The scope is frozen as the sections above stand.

### Iteration 7 — 2026-11-03 — 🧭 Implementation choices

⚠️ Rule broken — a shortcut change in the same commit as its code (RULES.md § Keyboard Shortcuts)
   The arrows' new behaviour across the headers went into the grid's commit, SHORTCUTS.md and
   SHORTCUTS.fr.md into a commit of their own right after it.
   Why: the run keeps code and documentation in separate commits; both rules could not hold.
   Autonomous run: reported instead of asked.

- **Folder groups**: by the first word shared, titled by the longest run all members share
  (§ Folder Prefix Groups).
- **Search sections in every search**, the folder view's included; *Similar to …* unsectioned
  (§ Search Result Groups).
- **The name-and-content tier** needed the search to look for a name match in its content: done
  only when every word is in the name and 💡 is on (§ Search Result Groups).
- **Section titles carry the count of the whole list**, not of the tiles loaded yet.
- **Patterns**: Camera also takes `P1010001` (`P` and 7 digits), a WhatsApp `IMG-…-WA0001` left to
  Messaging; Messaging takes Telegram's `photo_2024-01-02_…`, Signal, `FB_IMG_…`, `received_…`,
  Snapchat; a random token is 10 letters and digits or more, both present, switching kind (lower,
  upper, digit) at least every third character — `grok-video-ba71b543-…` lands in Identifiers.
- **Icons**: 🗂️ and 🗃️ for the buttons (📑 is the Layers'), 📷 🖥️ 💬 🆔 🔢 for the patterns.
- **The header**: its title in bold, a thin grey line after it across the row, its height a line
  plus 10 px; the first line of a section scrolled into view brings its header with it.
- **Checks**: the worktree's build got a copy of the main build's `settings.json` and `index\` so
  the folder view could be seen on the real base folder; launched with `--new-instance`.

### Iteration 8 — 2026-11-04 — ⚙️ Post-implementation — More name groups

User request: list every group the patterns find in `X:\Dropbox\Outils RS` (2 995 files, its
subfolders included); `ssstwitter.com_1751324860904.mp4` landed among the Named files, it should be
a group of its own. The survey found, among the 1 366 named files, machine-made names left out —
ssstwitter.com (175), OIG (60), Grok (24), ChatGPT Image (15), Videoleap (15), FaceApp (11)… —
generic web names (~45), short meme codes (~150), human series (`marlène schiappa …` 32), and
Numbers mixing date stamps with Unix times (1 172). Answers (Q&A 17–19):

- **Sources**: both — the known ones fixed, each its own group and checkbox under a **Sources ▸**
  submenu; the others **detected** by themselves, one **Detected sources** checkbox.
- **New groups**: **AI** (OIG, Grok, ChatGPT Image, High_Performance together), **Generic names**,
  **Short codes**, and **Numbers split** into **Date stamps** and **Timestamps**.
- **Series**: the named files sharing their first words are grouped too, a new group option turned
  on or off, on by default.

§ Sections, § File Name Patterns and the new § File Series describe it. Also: Camera's `P` and
7 digits now matches the whole name (`P34452747D6385635G` was taken for a camera's).

### Iteration 9 — 2026-11-04 — 🧭 Implementation choices

Iteration 8 implemented (Q&A 20: code and documentation). No rule broken.

- **A group is a `NameGroup`** — an id, a title, a match — instead of an enum value: the known sources
  are groups like the others. The ids turned off are kept as text in `settings.json`; the ones saved
  before (`Camera`, `Numbers`…) keep their meaning.
- **Detected sources**: the leading word — every letter of it, dots inside (`y2mate.com`) — then a
  stamp right after it: 4 digits or more, or a code of 5 letters and digits holding a digit. Never a
  small word (`les`, `the`). Their checkbox, **Detected sources**, ends the **Sources** submenu.
- **Series**: accents folded too (`marlène` and `marlene` one series); small words never end a title
  (`Taxez les crabes`, `Taxez les riches` → `Taxez`) nor make one alone.
- **AI** also takes DALL·E and Firefly; **Generic names** also `hq720`, `hqdefault`, `sddefault`,
  `unnamed`, `untitled`, `sans titre`, `downloads`; **Screenshots** also `Capture-décran-…` and
  `Capture-ecran-…`; **Identifiers** also an X media id (`GEhjfvEWQAE7EPO`, 15 characters, upper and
  lower case).
- **Icons**: 📚 Series, 🤖 AI, 🌐 Sources and each known source, 🔭 Detected sources, 🗒️ Generic names,
  🔣 Short codes, 🗓️ Date stamps, ⏱️ Timestamps.
- **On `X:\Dropbox\Outils RS`** (2 995 files): Date stamps 1 149, Identifiers 419, Short codes 229,
  ssstwitter.com 175, Screenshots 110, AI 108, Generic names 44, the other sources 5 to 15 each, 31
  series (`marlène schiappa` 32, `homme soja` 22…), Named files 552 — down from 1 366.

### Iteration 10 — 2026-11-04 — ⚙️ Post-implementation — Buttons shown, Series button

User report: the buttons turning the groups on or off do not show, and the series want one of their
own.

- **Fix**: the tile size row is a table of 6 columns (`_sizeRow.ColumnCount`); the two buttons added in
  columns 6 and 7 went to a second row, out of sight. The table gets its columns.
- **New**: a 📚 **Series** button between 🗂️ and 🗃️ (§ Toggles).

### Iteration 11 — 2026-11-04 — 🧭 Implementation choices

Iteration 10 implemented. No rule broken.

- The row has 9 columns now; the three buttons show after 🔳 / 📃: 🗂️, 📚, 🗃️.
- 📚 changes the same state as the submenu's **Series** (`PatternsOff`), saved through
  `FileExplorerPanel.PatternsOffChanged`.
- **Seen, out of scope**: in a window narrower than the panel's saved width, the panel overflows on
  the right and its last controls are cut off — `main` does the same; not changed here.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7, 9, 11 | 2026-11-03 | `NamePatterns`, `SharedPrefix.Groups`, the name-and-content tier, the grid's sections, the panel's sections and buttons, the ⚙ submenu and the settings |
| Unit tests | 7 | 2026-11-03 | None: no test project (Q&A 15); the patterns and the folder groups checked on sample names in a throw-away console, the sections in the app |
| README | 7, 9, 11 | 2026-11-03 | README / GLOSSARY in English and French, SHORTCUTS in English and French, RULES.md § File Explorer Sections |

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
| 16 | Go for implementation? | Code, tests and documentation — in a worktree | 2026-11-03 |
| 17 | Sources like ssstwitter.com, FaceApp, Videoleap, OIG: detected by themselves, a fixed known list, or both? | Both | 2026-11-04 |
| 18 | Which other groups? (AI together, generic names, short codes, Numbers split) | All four | 2026-11-04 |
| 19 | Do the named series (marlène schiappa …, homme soja …) become groups too? | Special cases: a new group option, turned on or off, on by default | 2026-11-04 |
| 20 | Go for the More name groups adjustment? | Code and documentation | 2026-11-04 |

---

*Last updated: 2026-11-04*
