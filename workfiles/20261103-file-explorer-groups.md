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
| **Shared prefix** | Folders | One section per prefix shared by sibling folders — `Outils RS (3)` |
| **Name kind** | Files | Files named by a person apart from files named by a machine — a GUID, a hash, a camera or screenshot stamp |

One **on / off button** turns every grouping on or off at once.

Components: `UI/ThumbnailGrid` (layout, hit-test, keyboard, paint), `UI/FileExplorerPanel`
(`ListFolderAsync` builds the rows), `Explorer/SharedPrefix` (the prefixes), `UI/AppSettings`
(the button's state).

---

## Sections

- A **section** is a titled header line followed by its tiles; the next section starts on a **new
  line of tiles**, never after the previous one's last tile.
- The **header**: a thin line across the panel with its title and count, `── Outils RS (3) ────`;
  **always expanded**, no collapse (Q&A 3). Not a tile: not selectable, no menu, no drag.
- **Order**: the groups first, then the items without a group (Q&A user request). Proposed full
  order — see Open Questions:
  1. one section per **folder prefix** group, A→Z by prefix;
  2. **Other folders** — the folders in no prefix group;
  3. **Named files** — files with a human name;
  4. **Automatic names** — files with a machine name.
- Inside a section, the existing order is kept: folders A→Z, files in the sort order.
- A list that would hold a **single section** shows **no header** at all (proposed): grouping
  changes nothing visible where there is nothing to separate.

## Folder Prefix Groups

- Built on `SharedPrefix`: sibling folders sharing at least one whole leading word, compared
  ignoring case, the same separators (space, `-`, `_`, `.`).
- **Two folders at least** make a group — like the flap's prefix, which needs a sibling.
- Nested prefixes (`Outils RS Alpha`, `Outils RS Alpha 2`, `Outils RS Beta`): proposed — one group
  per **shortest** shared prefix (`Outils RS`), the largest group, no sub-sections.
- `Outils RS` itself next to `Outils RS 2`: today it gets no prefix (it would keep no word of its
  own); proposed — it joins the `Outils RS` group anyway, as its first member.

## Automatic File Names

A file name is **automatic** when a machine made it up rather than a person. Proposed detection,
on the name without its extension (`Explorer/NameKind`, a new pure class):

| Pattern | Examples |
|---|---|
| GUID | `3f2504e0-4f89-11d3-9a0c-0305e82c3301`, braces or not |
| Long hex run (hash) | `a94a8fe5ccb19ba61c4c0873d391e987982fbbd3`, 16 hex digits or more |
| Digits and separators only | `20220321_132643`, `1698765432123`, `001` |
| Camera / device stamp | `IMG_20220321_132643`, `VID_…`, `PXL_…`, `DSC_0042`, `DCIM…` |
| Screenshot / app stamp | `Screenshot_2026-11-03-10-24-09`, `Capture d'écran 2026-11-03 102409`, `WhatsApp Image 2026-11-03 at 10.24.09` |
| Random token | `F3xKz9aWkAA1b2c` (an X media name): 10 characters or more, letters and digits mixed, no word-like run |

- Everything else is **named**: `Wojak triste`, `Meme chat 2`, `facture EDF`.
- Which of the stamp rows count as automatic is an open question (a screenshot's name is a
  machine's, but it is how a person finds it again).

## Toggle

- One **on / off button** for every grouping at once (Q&A 2), in the tile size row next to the
  🔳 / 📃 view buttons (proposed), an icon per § Icons (proposed 🗂️), its tooltip saying what it
  does; pressed while the groups are on.
- **Remembered** between sessions in `settings.json` (`AppSettings.ExplorerGroups`). Default
  proposed: **on**.

## Views

- Proposed: the **folder view** only — where the user browses one folder's subfolders and files.
  The search view, `*`, the favorites and the People view keep their lists as they are (their own
  order already carries a meaning: relevance tiers, newest favorites, people).
- Both displays: **Thumbnails** and **Details** (a header row between the rows) — proposed.

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

The app has **no unit test project** (`src/` holds `ImageGridFusion` only): nothing testable is
added to a test project. The name detection (`NameKind`) and the grouping are pure functions that
would be the first candidates if one is created — see Open Questions. Checked by hand in the app
otherwise.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [ ] Full section order: folder prefix groups → Other folders → Named files → Automatic names?
- [ ] Nested folder prefixes: one group per shortest shared prefix (`Outils RS`), no sub-sections?
- [ ] Inside a prefix group, does a tile's flap still show `Outils RS ›` small, or only the rest of
  its name, the header already saying the prefix?
- [ ] Which stamp names count as automatic: camera (`IMG_…`, `PXL_…`), screenshots, messaging apps
  (`WhatsApp Image …`)?
- [ ] Section titles: `Other folders`, `Named files`, `Automatic names` (UI in English)?
- [ ] No header when the list would hold a single section?
- [ ] Views: the folder view only, or the search view too?
- [ ] Details view: headers too?
- [ ] Toggle: next to the 🔳 / 📃 buttons, 🗂️, on by default?
- [ ] The folders' shared-prefix principle applied to the **named files** too (`Meme chat 1`,
  `Meme chat 2` → a `Meme chat` group)? Not chosen in the first answers — out of scope unless asked.
- [ ] Create a unit test project for `NameKind` and the grouping, or keep the hand checks?

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

---

*Last updated: 2026-11-03*
