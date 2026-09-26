# Emoji Stickers

> Working document — emojis laid over the grid, several at once, each one moved, rotated and
> resized, picked from a palette, a free field or the 20 most recently used.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A new **global effect**, **Emojis**, lays emojis over the whole grid. Each emoji is free on the
grid — it may straddle several cells — and is **selected first**, then moved by dragging, resized
with the wheel and rotated with Ctrl+wheel. The emojis are chosen from a palette of common emojis,
a free field (any emoji, typed or pasted, Win+. opening the Windows panel), or the **20 most
recently used**.

Components concerned (from the scout pass):

| Area | Where |
|---|---|
| Effect state | New immutable record `GridEmojis` in `Composition/`, next to `GridBorders.cs`; new case in the `GlobalEffect` enum (`Composition/GlobalEffect.cs`) |
| Rendering | `Compositor.Draw` — one grid-level call after the borders, like `borders?.Draw(...)`; exports through `Imaging/GridExport.cs` (`Job.Capture`) |
| Color glyphs | The app is pure GDI+ (`net10.0-windows10.0.19041.0`, no package), and GDI+ draws emojis **monochrome**: WPF text rendering rasterizes them (`UseWPF` in the `.csproj`) — see *Rendering* |
| Gestures | `UI/GridPreview.cs` — `OnMouseDown` / `OnMouseMove` / `OnMouseUp` / `OnMouseWheel` priority chains; `OnPaint` for the selection indicator |
| Delete key | `UI/MainForm.cs` (`case Keys.Delete when _preview.HasSelection …`) |
| Global effects row | `UI/MainForm.cs` (`_globalTabs`, `_globalOptions[GlobalEffect.X]`), `UI/EffectTabs.cs`, `UI/EffectIcons.cs` |
| Recent emojis | `UI/AppSettings.cs` if remembered between sessions (registry, `Software\ImageGridFusion`) |

---

## Nature — a Global Effect

Agreed at scoping (Q&A #1): the emojis belong to the **grid**, not to a cell + image pair
(RULES.md § Global Effects).

| Event | Emojis |
|---|---|
| An image is replaced, deleted, swapped; a cell *Reset* button | Untouched — they stay where they are on the grid |
| The layout changes, a separator is dragged | Kept, at the same place on the grid |
| *Clear all* | Reset — no emoji left |
| The Emojis toggle is turned off | Hidden from the preview and the exports, **kept**; turned on again, they come back as they were |
| Export running | The row is locked, and the emojis cannot be selected nor edited |

- **Off at start-up**, with no emoji. Its options show only while it is on (Global effects row
  rule), so adding the first emoji starts by turning the toggle on.
- **Not persisted**: the emojis on the grid are lost when the app closes, like every effect state.
- Geometry in **fractions of the grid**: centre (x, y) in fractions of the grid's width and height,
  size in fractions of the grid's **shorter side**, angle in degrees. The emojis keep their place
  and proportions when the window or the export size changes.

---

## Emoji Picker

Agreed at scoping (Q&A #2, #5): a palette of common emojis, a free field, and the 20 most recently
used.

- **Palette** (proposed, 20): 😂 ❤️ 😍 🔥 👍 😎 🥳 😭 🤔 👀 ✨ 💯 🎉 😱 🙏 💀 🤣 😘 👑 ⭐
- **Free field**: a text box and an **Add** button (Enter adds too). What is added is the **first
  grapheme cluster** of the text (`StringInfo`), so a composed emoji (skin tone, ZWJ family, flag)
  stays whole; an empty field adds nothing.
- **Recent**: the 20 emojis most recently **added to the grid**, most recent first, without
  duplicates — adding one already in the list moves it to the front.
- Clicking an emoji (palette or recent) **adds it** to the grid, **selects it** and closes the
  popup.

### Popup (Q&A #7)

- The Emojis options in the Global effects row hold a **"😀 ▾" button**; it opens a popup panel
  under it, keeping the row at its single height.
- The panel, top to bottom: **Recent** (hidden while empty), **Palette**, then the **free field**
  with its **Add** button.
- Clicking outside the panel, or Escape, closes it without adding anything.

### A New Emoji

- Placed at the **centre of the grid**, upright, size **20 %** of the grid's shorter side (proposed).
- Drawn **above** the emojis already there.

---

## Gestures

Agreed at scoping (Q&A #3): drag + wheel, but an emoji must be **selected first** to act on it.

| Gesture | On | Does |
|---|---|---|
| Click, or a drag started on it | An **unselected** emoji | Selects it only — it does not move (Q&A #8) |
| Drag | The **selected** emoji | Moves it |
| Wheel, pointer over it | The selected emoji | Resizes it: ×1.1 per notch up, ÷1.1 down, between 3 % and 100 % of the grid's shorter side (proposed) |
| Ctrl+wheel, pointer over it | The selected emoji | Rotates it: 5° per notch, free angle (proposed) |
| Delete | — | Removes the selected emoji |
| Escape, click outside every emoji | — | Deselects the emoji |

- Hit-testing uses the emoji's **rotated square**; the emoji on top wins where several overlap.
- The emojis are tested **before** the cell's gestures (after the ✕ and source-icon hits),
  on their own area only.
- The wheel acts on the selected emoji **only while the pointer is over it** (Q&A #9); elsewhere
  it zooms the cell under the pointer, as today — also while an emoji is selected.
- A moved emoji keeps its **centre inside the grid**; the part outside is clipped at the grid edge.
- **Exclusive selections** (Q&A #11): selecting an emoji clears the cell selection (the effects
  toolbar gets disabled), selecting a cell clears the emoji selection. Delete removes whichever is
  selected — the emoji, or the cell's image as today.
- **Brought to the front** (Q&A #12): selecting an emoji moves it above every other emoji, and it
  stays there once deselected.

### Selection Indicator

- The selected emoji is outlined by its rotated square in the **helper colour** (fluorescent
  green over the black halo, `HelperColor` / `HelperHalo`), drawn in `GridPreview.OnPaint`
  only — never in the exports (RULES.md § On-Cell Helper Indicators).
- Nothing is drawn around the emojis that are not selected.

---

## Rendering

- `GridEmojis.Draw(Graphics, Size canvas)` is called by `Compositor.Draw` **after the borders**, so
  the emojis are above everything, in the preview and every export (image, GIF, video).
- GDI+ cannot draw color glyphs: each emoji is **rasterized once** into a transparent ARGB bitmap
  by a color-capable renderer, **cached** by (emoji, pixel size), then drawn with `g.DrawImage`
  under a rotation transform. The cache keeps video exports from re-rasterizing every frame.
- The renderer is **WPF text rendering** (Q&A #6): `UseWPF=true` in the `.csproj`, no package —
  it ships with the Windows Desktop runtime. A `FormattedText` drawn into a `RenderTargetBitmap`,
  its pixels copied into a GDI+ `Bitmap`. WPF is used for this rasterization only; the UI stays
  WinForms.
- The font is **Segoe UI Emoji** (the Windows color emoji font).

---

## Test Impact

None, by the user's decision (Q&A #13): the solution has no test project, and every previous
workfile stayed test-free.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

- [x] ~~Cell effect, global overlay, or a new content type?~~ → Global effect over the grid (Q&A #1)
- [x] ~~How is the emoji picked?~~ → Palette + free field (Q&A #2), plus the 20 most recently used (Q&A #5)
- [x] ~~How is a placed emoji handled?~~ → Drag + wheel, the emoji selected first (Q&A #3)
- [x] ~~Which color glyph renderer: WPF (`UseWPF`, no package), SkiaSharp (NuGet), or DirectWrite interop?~~ → WPF (Q&A #6)
- [x] ~~Where does the picker sit: inline in the Global effects row, or in a popup opened from a button in the row?~~ → Popup from a button in the row (Q&A #7)
- [x] ~~A drag started on an **unselected** emoji: selects it only, or selects and moves it at once?~~ → Selects it only (Q&A #8)
- [x] ~~Where does the wheel act while an emoji is selected: over that emoji only, or anywhere on the grid?~~ → Over that emoji only (Q&A #9)
- [ ] Are the 20 recent emojis remembered between sessions (in the registry, where every app setting lives — the app has no settings `.json`), or for the session only?
- [x] ~~Emoji selection and cell selection: exclusive (selecting one clears the other), or both kept at once?~~ → Exclusive (Q&A #11)
- [x] ~~Stacking: does selecting an emoji bring it to the front?~~ → Yes, and it stays there (Q&A #12)
- [x] ~~Unit tests: none, as in the previous workfiles, or a test project for this feature?~~ → None (Q&A #13)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-27

- Scoping batch answered: a **global effect** over the whole grid rather than a cell effect or a
  new content type; palette + free field; drag + wheel with the emoji selected first; the subject
  sized as straightforward (single scout pass).
- Added on request: the **20 most recently used** emojis shown in the picker.
- Scout pass (three read-only agents): the rendering stack is pure GDI+, so color emojis need a
  glyph rasterizer; the global effect wiring follows `GridBorders`; the sticker hit-test slots into
  `GridPreview`'s input chains before the cell gestures, and the wheel needs an early branch ahead
  of the cell zoom.
- Proposed defaults, to be confirmed with the design: the 20-emoji palette, a new emoji at the
  grid's centre at 20 % of its shorter side, ×1.1 / 5° per wheel notch, size bounds 3 %–100 %,
  Escape to deselect, emojis drawn above the borders.

### Iteration 2 — 2026-09-27

First batch of open questions answered: WPF rasterizes the color glyphs (no package); the
picker is a popup opened from a "😀 ▾" button in the row; a drag on an unselected emoji only
selects it; the wheel acts on the selected emoji only while the pointer is over it, the cell zoom
staying everywhere else.

### Iteration 3 — 2026-09-27

Second batch answered: emoji and cell selections are exclusive; a selected emoji comes to the
front and stays there; no unit tests. The recent emojis' persistence stays open — the user asked
why the registry rather than a settings `.json`: the app has none, every remembered setting lives
in the registry.

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
| 1 | What do the emojis belong to: a cell effect, a global overlay on the grid, or a new content type? | Global overlay on the grid | 2026-09-27 |
| 2 | How is the emoji to add chosen? | Palette + free field | 2026-09-27 |
| 3 | How is a placed emoji handled? | Drag + wheel OK, but the emoji must be **selected first** to act on it | 2026-09-27 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-09-27 |
| 5 | *(request, not a question)* | Also show the 20 most recently used emojis | 2026-09-27 |
| 6 | Which color glyph renderer? | WPF (`UseWPF`, no package) | 2026-09-27 |
| 7 | Where does the picker sit? | A popup opened from a button in the row | 2026-09-27 |
| 8 | What does a drag on an unselected emoji do? | Selects it only | 2026-09-27 |
| 9 | Where does the wheel act while an emoji is selected? | Over that emoji only | 2026-09-27 |
| 10 | Are the recent emojis remembered between sessions (registry)? | Asked back: why the registry, aren't the project's settings `.json` enough? → the app has no settings `.json`; every remembered setting is in the registry (`UI/AppSettings.cs`) | 2026-09-27 |
| 11 | Are the emoji selection and the cell selection exclusive? | Exclusive | 2026-09-27 |
| 12 | Does selecting an emoji bring it to the front? | Yes | 2026-09-27 |
| 13 | Unit tests for this feature? | None | 2026-09-27 |
| 14 | Are the recent emojis remembered between sessions, in the registry with the other settings? | | |

---

*Last updated: 2026-09-27*
