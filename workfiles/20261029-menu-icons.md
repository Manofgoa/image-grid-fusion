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
| Cell | Copy cell (Ctrl+C) | 📋 |
| Cell | Find similar images | 🔍 |
| Layer | Duplicate | The layer list's Duplicate icon, drawn (`EffectIcons.Layer`) — *reused* |
| Layer | Delete | The layer list's Delete icon, drawn (`EffectIcons.Layer`) — *reused* |
| Layer | Find similar images | 🔍 |
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
| Text style | Delete ▸ Delete <name> | 🗑️ |
| Tray | Open | The app's icon (`AppIcon`) — *reused* |
| Tray | Quit | 🚪 |

---

## Other Controls

*(Iterations 7–8, post-implementation.)* The same icons beyond the menus: the tabs, the buttons, the
drop-downs and the options' small icons — **emoji first, over the app's drawings too**: a drawing is
kept only where no emoji says the thing. The arrows and symbols become emoji where one exists. All
drawn in full color (`MenuIcon`, Direct2D), at the control's DPI size. Approved on a preview.

| Where | Control | Icon (✎ = drawing kept) |
|---|---|---|
| Effect tabs | Background, Crop, Cutout, Zoom, Animations, Rotate, Flip, Frames, Colors, Blur, Volume | 🖼️, ✎, ✂️, 🔍, ✨, 🔃, 🪞, 🎞️, 🎨, 💧, 🔊 |
| Effects toolbar | ★ Preset…, Reset | ⭐ Preset…, ↩️ |
| Global tabs | Format, Soundtrack, Fade, Borders, Seams, Cascade, Layers | ✎, 🎵, ✎, ✎, ✎, ⏭️, 📑 |
| Global / options toolbars | Every Reset | ↩️ |
| Layer list | Add image, Paste, Add text, Copy, Duplicate, Edit, Done, Delete, To back, Lower, Raise, To front | 🖼️, 📋, 🔤, 📄, ✎, ✏️, ✅, ❌, ⏬, 🔽, 🔼, ⏫ — the adds' green plus gone |
| Bottom bar | Clear all, ⚙, Copy PNG, its ▾, Copy last video, Save PNG…, its ▾ | 🧹, ⚙️, 📋, 🔽, 🕘, 💾, 🔽 |
| Transport | Play / Pause | ▶️ / ⏸️ |
| File explorer | Aa (name search), 💡 (content search), ↻, » / «, × (*Similar to*), ▦, ☰, Choose folder…, Load (URL zone), Browse… (Soundtrack) | 🔠, 💡, 🔄, ⏩ / ⏪, ✖️, 🔳, 📃, 📁, ⬇️, 📂 |
| Views drop-down | Folders, People, Favorites | 📁, 👥, ❤️ — full color, no longer one tint |
| Sort drop-down | Date ↓, Name A→Z, Size ↑ | 📅 Date ⬇️, 🔠 Name A→Z, 📦 Size ⬆️ |
| Animations' Type | Zoom | 🔍 |
| Kept drawings | Crop, Format, Fade, Borders, Seams, Duplicate, the eyedropper, the Fade curves, the intensity and opacity icons, the text alignments, the option thumbnails | ✎ |
| No icon | The font drop-down (each font in its face), the Borders style drop-down, the Color… part (its swatch) | — |

**The menus follow**: one icon per action — the layer menu's *Delete* becomes ❌ with the layer
list's; *Twitter corners by default* and the layer menu's *Duplicate* keep the drawings kept.

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

### Iteration 6 — 2026-10-29 — 🧭 Implementation choices

- **Two menus grew on `main` since the design**: the cell menu's *Copy cell* and the layer list's
  context menu (*Duplicate*, *Delete*, *Find similar images*). Every menu being in scope, they got
  their icons by the agreed criterion — 📋, the layer list's own drawn Duplicate and Delete icons, 🔍
  — and § Entries lists them.
- **One place**: `UI/MenuIcons.cs` — a `MenuIcon` per action (an emoji, or an icon the app draws),
  `MenuIcons.Set` on an entry, `MenuIcons.Attach` on its menu.
- **Drawn as a menu opens** (its `Opening`), at 16 logical px in the DPI of its source control — its
  own for the tray's — the whole tree of its entries at once, an entry drawn again only when that
  size changed. Each entry holds its own copy of the shared bitmap, disposed with it: the text
  style menu's entries are made anew at each opening.
- **The emoji**: `TextRaster.Render` in Segoe UI Emoji at its 192 px em, the ink's bounds trimmed,
  fitted bicubic into the square — `TextRaster` untouched.
- **Checked on screen**: the ⚙ menu at the checking instance's scale — full-color emoji, the checked
  *Twitter corners by default* framed, the disabled entries greyed. The other menus were not opened
  (an empty grid disables Copy ▾ / Save ▾, no base folder for the tiles): left to the delivery
  launch.
- No rule broken.

### Iteration 7 — 2026-10-29 — ⚙️ Post-implementation — Buttons and drop-downs

The user asks for the same logic, **emoji first**, on the icons of the **buttons**, the
**drop-downs** and the other controls — the file explorer's views drop-down (Folders, People,
Favorites) given as an example of icons that look poor. Scope to be settled (inventory, then a
scoping batch) before any code.

### Iteration 8 — 2026-10-29 — ⚙️ Post-implementation — Every control, emoji first

Scoped with the user: every control — drop-downs, buttons with a glyph, text buttons, tabs and the
options' small icons; **emoji over the app's drawings** wherever one fits, a drawing kept only where
none says the thing; the arrows and symbols as emoji where one exists. The control-by-control
preview approved as shown: § Other Controls. `RULES.md` § Menus widens to every control, the
criterion now emoji first.

### Iteration 9 — 2026-10-29 — ✅ Implemented — Every control, emoji first

Go given for the adjustment: code, tests and documentation, in a new worktree
(`.claude/worktrees/control-icons`, branch `feature/control-icons`, from `feature/menu-icons`).

### Iteration 10 — 2026-10-29 — ⚙️ Post-implementation — Standard split buttons

Seen on the first build of Iteration 9, the user rejects the result ("nothing is right"): the Copy ▾
and Save ▾ arrows drawn as a 🔽 emoji in a button of their own make no sense — they must be the
**standard Windows split button**, its arrow built into the button. What else is wrong is asked
before going on; the Iteration 9 code stays uncommitted meanwhile.

### Iteration 11 — 2026-10-29 — ⚙️ Post-implementation — Sort direction as text

The sort drop-down's direction stays **text** — `Date ↓`, `Size ↑` — never a ⬇️ / ⬆️ emoji; only the
entry's icon is an emoji. *(Revises Q&A 15 for the arrows inside a label.)*

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5, 6 | 2026-10-29 | `UI/MenuIcons.cs`, every menu wired |
| Unit tests | 5 | 2026-10-29 | None: no test project in the solution |
| RULES.md | 5 | 2026-10-29 | § Menus › Menu Icons |
| Glossary (EN + FR) | 5 | 2026-10-29 | *Menu icon* / *Icône de menu* |
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
| 12 | Merge `feature/menu-icons` into `main`? | Not answered yet: the worktree and its branch kept | 2026-10-29 |
| 13 | Adjustment: which controls get emoji-first icons? | Drop-downs, buttons with an emoji glyph, text buttons, tabs and the options' small icons | 2026-10-29 |
| 14 | Adjustment: the icons the app already draws (tabs, Reset, layer list…): kept or replaced? | Replaced where an emoji fits — validated first on a preview | 2026-10-29 |
| 15 | Adjustment: the arrows and symbols (▾ » « × ▦ ☰ ↓): text or emoji? | Emoji where one exists | 2026-10-29 |
| 16 | Adjustment: the preview of every control's icon (emoji, or the drawing kept): approved? | Approved as shown | 2026-10-29 |
| 17 | Go for the adjustment? | Code, tests and documentation, in a new worktree | 2026-10-29 |
| 18 | Besides the split buttons, what is wrong on the first build? | | 2026-10-29 |

---

*Last updated: 2026-10-29*
