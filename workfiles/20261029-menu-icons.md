# Menu Icons

> Working document — an icon in front of the menu entries of Image Grid Fusion, and a rule so every
> future menu entry gets one.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app's menus (WinForms `ContextMenuStrip`) show text only today, a ✓ in front of the checked
toggles. This work gives their entries an icon, so they are recognised at a glance, and writes a
rule into `RULES.md` so a future entry follows the same style.

Settled at scoping (2026-10-29):

- **Every menu is in scope**: the ⚙ menu, Copy ▾ / Save ▾, and every context menu.
- **Entries with an icon**: the **actions and toggles** of a menu — never the greyed captions
  (*File contents*, *People*, *Image content*), never the **values** of a submenu (`1920 px`,
  `Normal`, `Image center`…).
- **Style**: a mix of two styles **chosen per case** (criterion: § Icon Style):
  - **drawn** icons, as the effects and options rows draw them (`UI/EffectIcons.cs`: drawn in code
    at the size asked for, **in color** — a cyan lens with an orange handle, an orange-to-red arrow…);
  - **emoji glyphs** (Segoe UI Emoji), as the tile tooltip shows them.
- **A rule in `RULES.md`** for every future menu entry.

**Out of scope**: the **grouping** of the menus' entries (their order, groups, captions, submenus)
stays as it is — the user will discuss regrouping them **after** the icons are in the code, in a
work of its own.

---

## Menus Today

| Menu | Field | Where | Entries |
|---|---|---|---|
| ⚙ settings | `_settingsMenu` | `UI/MainForm.cs` | ~25 top-level entries in 4 groups, 3 greyed captions, 7 submenus of values |
| Copy ▾ | `_copyMenu` | `UI/MainForm.cs` | GIF, MP4 Video, JPEG for sharing, PNG max size ▸, Copy last video |
| Save ▾ | `_saveMenu` | `UI/MainForm.cs` | GIF, MP4 Video, PNG max size ▸, Save last video… |
| A cell's | `_cellMenu` | `UI/MainForm.cs` | Find similar images |
| A tile's | `FileExplorerPanel._menu` | `UI/FileExplorerPanel.cs` | People ▸ (each face cut out of the photo already), Open, Rename… (F2), Hide ▸, Unhide, Un-merge ▸, Not this person ▸, Edit description…, Find similar images, Copy (Ctrl+C), Copy as PNG, Copy full path, Open file location |
| The URL zone's | `UrlZone._menu` | `UI/UrlZone.cs` | Copy, Copy as PNG, Copy full path, Open file location — the tile's texts (`FileExplorerPanel.CopyText`…) |
| A text style's | `_textStyleMenu` | `UI/MainForm.Layers.cs` | Rename…, Replace with current style, Delete |
| The tray icon's | `TrayApplicationContext._menu` | `UI/TrayApplicationContext.cs` | Open, Quit |

- No custom renderer: every menu draws with WinForms' default one.
- **A checked entry with an image loses its ✓**: the default renderer draws the image in a
  highlighted frame instead. Every toggle of the ⚙ menu is concerned (open question).
- The only menu images today: the People submenu's faces, `ImageScaling = None` at a size in
  device pixels (`LogicalToDeviceUnits`) — the way to stay crisp at any DPI.
- **Emoji in color**: GDI and GDI+ draw a Segoe UI Emoji glyph in **one color** — the tile tooltip
  (`UI/IndexToolTip`) gives each icon its own tint. Only Direct2D draws them in full color, as the
  text layers do (`Imaging/TextRaster`, RULES.md § Layers).

---

## Icon Style

- **The criterion — emoji, unless an exception**, in this order:
  1. An entry whose meaning **already has an icon in the app** reuses it: a drawn one
     (`EffectIcons` — *Twitter corners by default* takes the Borders' tab icon), the app's own icon
     (`AppIcon` — the tray's *Open*), or an emoji the app already shows for it (the tile tooltip's
     💡 caption, 🏷 tags, 📝 text read, 👨‍🦰 people).
  2. Otherwise a **Segoe UI Emoji glyph**.
  3. A **new drawn icon** only when no emoji says the thing — none needed today.
- **Emoji in full color**: each glyph rendered by **Direct2D** (`Imaging/TextRaster`'s DirectWrite
  route) into a bitmap of the menu's size, kept in a cache per glyph and size — never GDI, which
  draws it in one color.
- **Checked toggles**: the default renderer's look is kept — checked, the icon shows in its
  highlighted frame, in place of the ✓ (Windows' standard). No custom renderer.
- **A confirming item** (*Hide ▸ Hide*, *Un-merge ▸ Un-merge*, *Not this person ▸ Remove*) takes its
  parent's icon: it is the action itself.
- **One icon per action, wherever it shows**: *Find similar images* in the cell's and the tile's
  menu, *Copy* in the tile's and the URL zone's, every *Download the … engine…* — one definition,
  shared (a single place listing the menu icons, beside `EffectIcons`).
- **Drawn at the menu's size in device pixels** (16 logical px, `LogicalToDeviceUnits`),
  `ImageScaling = None`, drawn again when the DPI changes — never a 16 px bitmap stretched.
- An entry whose meaning already has an icon in the app **reuses it** — e.g. *Twitter corners by
  default* the Borders' tab icon.

---

## Entries

Per menu, each entry with an icon — ▢ a toggle, ▸ a submenu (its values without icon). *Reused*
marks an icon the app already shows for that meaning.

### ⚙ Settings

| Entry | Icon |
|---|---|
| Start with Windows ▢ | 🚀 |
| Twitter corners by default ▢ | Borders tab icon, drawn (`EffectIcons.Borders`) — *reused* |
| Video size ▸ | 🎬 |
| Guides show from ▸ | 📏 |
| Gap bars from ▸ | 📐 |
| File explorer folder… | 📁 |
| File explorer pages per load ▸ | 📄 |
| Install yt-dlp companions… | ⬇️ |
| Download the cutout engine… | ⬇️ |
| *File contents* (caption) | — |
| Search file contents (OCR) ▢ | 📝 — *reused* (the tooltip's text read) |
| Rebuild content index | 🔄 |
| Download the OCR engine… | ⬇️ |
| OCR result style ▸ | ↪️ |
| *People* (caption) | — |
| Face analysis ▢ | 👨‍🦰 — *reused* (the tooltip's people) |
| Face grouping ▸ | 👥 |
| Download the face engine… | ⬇️ |
| Forget faces… | 🗑️ |
| *Image content* (caption) | — |
| Visual analysis ▢ | 🏷️ — *reused* (the tooltip's tags) |
| Similar images ▸ | 🎚️ |
| Download the vision engine… | ⬇️ |
| Captions ▢ | 💡 — *reused* (the tooltip's caption) |
| Download the caption engine… | ⬇️ |
| Forget visual analysis… | 🗑️ |
| Open app folder | 📂 |

The entries of *OCR result style ▸* are toggles and actions, not values: *Curved arrow* ▢ ⤴️,
*Arrow thickness* ▸ ➖, *Arrow color…* its **swatch** (shared RULES.md § Colour Items — already the
rule for a colour item, untouched).

### Copy ▾ / Save ▾

| Entry | Icon |
|---|---|
| GIF | 🎞️ |
| MP4 Video | 🎬 |
| JPEG for sharing (Copy only) | 📤 |
| PNG max size ▸ | 🖼️ |
| Copy last video / Save last video… | 🕘 |

### Context Menus

| Menu | Entry | Icon |
|---|---|---|
| Cell | Find similar images | 🔍 |
| Tile | People ▸ | 👨‍🦰 — *reused*; its entries keep their faces |
| Tile | Open (a group) | 👥 |
| Tile | Rename… | ✏️ |
| Tile | Hide ▸ Hide | 🙈 |
| Tile | Unhide | 👁️ |
| Tile | Un-merge ▸ Un-merge | ✂️ |
| Tile | Not this person ▸ Remove | 🚫 |
| Tile | Edit description… | ✏️ |
| Tile | Find similar images | 🔍 |
| Tile, URL zone | Copy | 📋 |
| Tile, URL zone | Copy as PNG | 🖼️ |
| Tile, URL zone | Copy full path | 🔗 |
| Tile, URL zone | Open file location | 📂 |
| Text style | Rename… | ✏️ |
| Text style | Replace with current style | 📥 |
| Text style | Delete | 🗑️ |
| Tray | Open | The app's icon (`AppIcon`) — *reused* |
| Tray | Quit | 🚪 |

---

## RULES.md

A new section, `## Menus` › `### Menu Icons` (origin: this workfile):

- Every **action or toggle** of a menu has an icon — the ⚙ menu, Copy ▾ / Save ▾, every context
  menu; never a greyed caption, never a submenu's values. A confirming item takes its parent's.
- **Its style**: the icon the app already shows for that meaning (`EffectIcons`, `AppIcon`, the tile
  tooltip's emoji), else a **Segoe UI Emoji glyph in full color**, else — no emoji saying it — a new
  drawn icon in `EffectIcons`' manner.
- **One icon per action**, defined once (the menu icons' single place) and shared by every menu
  showing the action.
- Drawn at the menu's size in **device pixels**, `ImageScaling = None`, the emoji by Direct2D;
  never a small bitmap stretched.
- A checked toggle shows its icon framed, as the default renderer draws it: no ✓ beside it.

## Glossary

A new row in `GLOSSARY.md` and its French twin in `GLOSSARY.fr.md`, in the same commit
(`../CLAUDE.md` § French Versions):

| Term | Meaning |
|---|---|
| Menu icon (*icône de menu*) | The picture in front of a menu entry — every action and toggle of the ⚙ menu, Copy ▾ / Save ▾ and the context menus, never a caption nor a submenu's values: the icon the app already shows for that meaning, else an emoji in full color; a checked toggle shows it framed instead of a ✓ |

No README line: the README does not describe the menus' look.

---

## Test Impact

The solution holds **no test project** (`ImageGridFusion.slnx`: the app only). Nothing is unit
tested; the delivery is checked by hand in the running app, at 100 % and at a higher scale.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [x] ~~**Style criterion**: when is an entry's icon drawn, when an emoji glyph?~~ → Emoji, unless
  the app already has an icon for that meaning (reused), a new drawing only when no emoji says it
- [x] ~~**Emoji in full color or in one tint?**~~ → Full color, rendered by Direct2D
- [x] ~~**Checked toggles**~~ → The framed icon in place of the ✓, the default renderer's look
- [x] ~~**The confirming item** of *Hide ▸*, *Un-merge ▸*, *Not this person ▸*~~ → The parent's icon
- [x] ~~**The icon of each entry** (§ Entries): approved as drafted, or changes?~~ → Approved as drafted
- [x] ~~**Glossary / README**: a *Menu icon* term, a README line — or the rule in `RULES.md` only?~~
  → `RULES.md` and a *Menu icon* term in the glossary, English and French; no README line

---

## Design Iterations

### Iteration 1 — 2026-10-29

Scoping batch answered: every menu, the actions and toggles only, a mix of drawn icons and emoji
glyphs chosen per case, a rule in `RULES.md`. Exploration: eight menus inventoried, no custom
renderer, the checked-image look of the default renderer, the People submenu's DPI-sized images,
`EffectIcons` drawn in color, emoji in full color through Direct2D only. No test project.

### Iteration 2 — 2026-10-29

The style settled: emoji in full color (Direct2D) unless the app already shows an icon for that
meaning — reused — a new drawing only when no emoji says it; checked toggles framed as the default
renderer draws them; a confirming item takes its parent's icon. § Entries drafted menu by menu,
§ RULES.md drafted.

### Iteration 3 — 2026-10-29

The user set aside the **regrouping** of the menu entries: discussed after the icons are
integrated, not here. § Overview says it is out of scope; the entries keep their order and groups.

### Iteration 4 — 2026-10-29

§ Entries approved as drafted. The docs: the rule in `RULES.md` and a *Menu icon* term in the
glossary, English and French (§ Glossary); no README line. No open question left.

### Iteration 5 — 2026-10-29 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`.claude/worktrees/menu-icons`, branch
`feature/menu-icons`). No unit test: the solution holds no test project.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the solution |
| RULES.md | | | |
| Glossary (EN + FR) | | | |
| README | 4 | 2026-10-29 | Not needed: decided at design |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which menus are in scope, and a rule for future menus? | ⚙, Copy ▾ / Save ▾, every context menu; a rule in `RULES.md` | 2026-10-29 |
| 2 | Which entries get an icon? | The actions and toggles — not the captions nor a submenu's values | 2026-10-29 |
| 3 | Which icon style? | "D or C depending on the case" — a drawn style and emoji glyphs, mixed | 2026-10-29 |
| 4 | Straightforward or tricky? | Straightforward: one scout pass | 2026-10-29 |
| 5 | Style criterion: the app's icon else an emoji, things as emoji and verbs drawn, or emoji unless none fits? | Emoji unless an exception: the app's icon reused, a new drawing when no emoji fits | 2026-10-29 |
| 6 | Emoji in full color (Direct2D) or in one tint (GDI)? | Full color | 2026-10-29 |
| 7 | Checked toggles: framed icon, no icon (✓ kept), or both drawn? | Framed icon | 2026-10-29 |
| 8 | The confirming item of Hide ▸, Un-merge ▸, Not this person ▸: the parent's icon, or none? | The parent's icon | 2026-10-29 |
| 9 | The icon of each entry (§ Entries): approved, or changes? | Approved as drafted | 2026-10-29 |
| 10 | Glossary / README: a term, a line, or RULES.md only? | RULES.md and a glossary term | 2026-10-29 |
| 11 | Go: scope and where? | Code, tests and documentation, in a worktree | 2026-10-29 |

---

*Last updated: 2026-10-29*
