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
- **Style**: a mix of two styles **chosen per case** — the criterion is an open question:
  - **drawn** icons, as the effects and options rows draw them (`UI/EffectIcons.cs`: drawn in code
    at the size asked for, **in color** — a cyan lens with an orange handle, an orange-to-red arrow…);
  - **emoji glyphs** (Segoe UI Emoji), as the tile tooltip shows them.
- **A rule in `RULES.md`** for every future menu entry.

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

The criterion choosing between a drawn icon and an emoji glyph is an open question. Whatever it is:

- **One icon per action, wherever it shows**: *Find similar images* in the cell's and the tile's
  menu, *Copy* in the tile's and the URL zone's, every *Download the … engine…* — one definition,
  shared (a single place listing the menu icons, beside `EffectIcons`).
- **Drawn at the menu's size in device pixels** (16 logical px, `LogicalToDeviceUnits`),
  `ImageScaling = None`, drawn again when the DPI changes — never a 16 px bitmap stretched.
- An entry whose meaning already has an icon in the app **reuses it** — e.g. *Twitter corners by
  default* the Borders' tab icon.

---

## Entries

To be filled once the style criterion is settled: per menu, each entry and its icon.

---

## RULES.md

A new section, `## Menus` › `### Menu Icons`, to be written once the style is settled: which entries
get an icon, how its style is chosen, one icon per action shared, drawn at the DPI size.

---

## Test Impact

The solution holds **no test project** (`ImageGridFusion.slnx`: the app only). Nothing is unit
tested; the delivery is checked by hand in the running app, at 100 % and at a higher scale.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [ ] **Style criterion**: when is an entry's icon drawn, when an emoji glyph? ("D or C depending on
  the case")
- [ ] **Emoji in full color or in one tint?** Full color needs Direct2D (`TextRaster`) to render
  each glyph into a bitmap; one tint is the tooltip's GDI way.
- [ ] **Checked toggles**: accept the framed icon in place of the ✓ (Windows' standard), keep the ✓
  by giving the toggles no icon, or a renderer drawing both?
- [ ] **The confirming item** of *Hide ▸ Hide*, *Un-merge ▸ Un-merge*, *Not this person ▸ Remove*:
  the parent's icon repeated, or none (a value of a submenu)?
- [ ] **Glossary / README**: a *Menu icon* term, a README line — or the rule in `RULES.md` only?

---

## Design Iterations

### Iteration 1 — 2026-10-29

Scoping batch answered: every menu, the actions and toggles only, a mix of drawn icons and emoji
glyphs chosen per case, a rule in `RULES.md`. Exploration: eight menus inventoried, no custom
renderer, the checked-image look of the default renderer, the People submenu's DPI-sized images,
`EffectIcons` drawn in color, emoji in full color through Direct2D only. No test project.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the solution |
| RULES.md | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which menus are in scope, and a rule for future menus? | ⚙, Copy ▾ / Save ▾, every context menu; a rule in `RULES.md` | 2026-10-29 |
| 2 | Which entries get an icon? | The actions and toggles — not the captions nor a submenu's values | 2026-10-29 |
| 3 | Which icon style? | "D or C depending on the case" — a drawn style and emoji glyphs, mixed | 2026-10-29 |
| 4 | Straightforward or tricky? | Straightforward: one scout pass | 2026-10-29 |

---

*Last updated: 2026-10-29*
