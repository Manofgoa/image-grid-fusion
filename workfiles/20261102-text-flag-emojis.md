# Text Flag Emojis

> Working document — flag characters (🇪🇺, 🇫🇷…) drawn as flags, in color, in the text layers and the
> text cells.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A flag emoji is a **pair of regional indicator symbols** (U+1F1E6–U+1F1FF): 🇪🇺 is `U+1F1EA U+1F1FA`
(E, U). A font that knows the pair draws one flag; Windows' own emoji font, **Segoe UI Emoji, has no
flags** — it draws the two letters, boxed (`EU`). The app's texts therefore show letters where the user
pasted a flag.

Goal: a flag pasted or typed into a text shows **as a flag, in color**:

| Text | Drawn by today | In scope |
|---|---|---|
| A **text layer** | `Imaging/TextRaster` — DirectWrite through Direct2D, color glyphs on (`D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT`); the emojis in color through DirectWrite's system font fallback to Segoe UI Emoji | ✅ |
| A **text cell** — a text file, a text pasted or dropped (`Imaging/TextPages`) | GDI+ `Graphics.DrawString` in Consolas, a monospace grid of one column per UTF-16 unit | ✅ |
| The UI showing the text — the layer's text field, the layer list | WinForms (GDI) | ❌ out of scope: they keep showing the letters |

**Input**: the flag arrives as characters — pasted, or typed with another tool. No flag picker (the
Windows emoji panel, Win+., offers none; not added).

Every export goes through `Compositor`, which draws the layers' and the cells' bitmaps: what the
preview shows, the PNG / MP4 / GIF and the cell copy show too.

---

## The Flag Font

- A **free emoji font, cut down to the flags** (a subset), **embedded in the exe** as a resource — never
  downloaded, never installed in Windows. The other emojis stay Segoe UI Emoji: only the flags take the
  new font's style.
- **The font is Twemoji Mozilla** (`mozilla/twemoji-colr`), **COLRv0** — Segoe UI Emoji's format, the one
  the legacy `ID2D1RenderTarget` path `TextRaster` uses today draws for sure, so no change to the
  drawing path. Its graphics are under **CC-BY 4.0**: attribution required.
- **The subset** holds:
  - the 26 regional indicators and every valid pair's flag (the country flags, 🇪🇺 and 🇺🇳 included);
  - the **subdivision flags** — England, Scotland, Wales: 🏴 (U+1F3F4) followed by tag characters
    (U+E0020–U+E007F) — with 🏴 itself and **🏴‍☠️** (🏴 ZWJ ☠ VS16, its ZWJ, ☠ and VS16 in the subset), so
    the sequences built on 🏴 stay in one font. 🏴 and 🏴‍☠️ then take Twemoji's style.

  Made once with fontTools (`pyftsubset`), the file committed under `src/ImageGridFusion/Imaging/`, its
  license file next to it; the command that made it written in `CONTRIBUTING.md`, so it can be made again.
- **Attribution**: the README's technical paragraph names the font and its license, as it names the other
  third-party components (§ One place per fact: there only).

---

## Text Layers — `TextRaster`

- The flag font is loaded **from memory** into a **private font collection** — DirectWrite's
  `IDWriteFactory5::CreateInMemoryFontFileLoader`, a font set (`IDWriteFactory3::CreateFontSetBuilder`),
  `CreateFontCollectionFromFontSet` — once per thread, with the other factories (`TextRaster.Factories`).
- A **font fallback** maps the flags' code points to the flag font, **before** the system's mappings:
  `IDWriteFactory2::CreateFontFallbackBuilder`, `AddMapping` (the regional indicators U+1F1E6–U+1F1FF,
  🏴 U+1F3F4 and the tag characters U+E0020–U+E007F), then `AddMappings(GetSystemFontFallback)` so every other emoji
  still falls back to Segoe UI Emoji; set on each layout (`IDWriteTextLayout2::SetFontFallback`).
- The **chosen font wins** where it has the character: a font drawing the regional indicators itself
  keeps doing so (rare; Segoe UI, the default, does not).
- Nothing else changes: the flag is drawn by `DrawText` with color glyphs, a **plain shape** in the
  shadow's mask (`colorFont: false`) and in the outline's rings, like every emoji.
- Requires Windows 10 1703 — the app's target (10.0.19041) is above it.

---

## Text Cells — `TextPages`

Today a text cell draws every character with GDI+ in Consolas, one column per UTF-16 unit; a flag is
**four** UTF-16 units (two surrogate pairs), and a line may be wrapped between them. GDI+ draws no color
glyph.

- **Every emoji is drawn in color**, the flags among them, by **`TextRaster`** (Direct2D, the flag font's
  fallback included) — one emoji at the cell's font size, its bitmap cached by emoji and size, then placed
  at its column with `Graphics.DrawImage`. The rest of the text stays GDI+ in Consolas.
- **An emoji** is a text element that is a flag (a regional indicator pair, a subdivision flag), holds VS16
  (U+FE0F), or starts with a code point of Unicode's `Emoji_Presentation` property — .NET has no API for
  it, so a compact range table in the code, one definition. A lone regional indicator is no emoji there:
  drawn as GDI+ draws it.
- **The grid counts text elements** (`StringInfo`, extended grapheme clusters), never UTF-16 units: an
  emoji — a flag, a ZWJ sequence, a skin tone — is one element, **two columns wide**, as in a terminal;
  any other element one column. A line is **never wrapped inside an element**, and a column's place is
  the columns before it, never its UTF-16 offset.
- A style run (`StyledText`) covering an emoji keeps its **highlight** behind it; its **ink** does not
  color it — an emoji keeps its own colors.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*):
no unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| A text layer `Vive l'🇪🇺 et la 🇫🇷 !` draws both flags in color, the other emojis unchanged (Segoe UI Emoji) | The running app | — (by hand) |
| The flags with an outline, a Halo and a Drop shadow, bold and italic, in a box and justified | The running app | — (by hand) |
| A text layer with flags exported as PNG, MP4 and copied alone (Ctrl+C): the flags in color | The running app | — (by hand) |
| England, Scotland, Wales and 🏴‍☠️ in a text layer: each one flag, in color | The running app | — (by hand) |
| A text pasted into a cell with flags and other emojis (😀, 👍🏽, 👨‍👩‍👧): drawn in color, two columns each, never split by a line wrap, the columns after them aligned | The running app | — (by hand) |
| A styled text cell (highlight, ink) holding an emoji: the highlight behind it, the emoji in its own colors | The running app | — (by hand) |
| A text cell zoomed in and out (`Sharpen`): the flags stay sharp and in their column | The running app | — (by hand) |
| An invalid pair (🇽🇽) and a lone regional indicator draw letters, no crash | The running app | — (by hand) |

---

## Open Questions

- [x] ~~Which flag font: Twemoji Mozilla (COLRv0, drawn by today's path, CC-BY 4.0) or Noto Color Emoji
  (COLRv1, OFL, the drawing path to check first)?~~ → Twemoji Mozilla
- [x] ~~The subdivision flags (England, Scotland, Wales: 🏴 + tag characters) — in the subset? They need 🏴
  itself in the flag font, so 🏴 and 🏴‍☠️ take its style too.~~ → Yes, 🏴 and 🏴‍☠️ with them
- [x] ~~In a text cell, which emojis are drawn in color: the flags only, or every emoji?~~ → Every emoji
- [x] ~~In a text cell, how many columns does an emoji take: 2 (a terminal's convention), or 1?~~ → 2

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-02

Initial design, from the scoping batch (Q&A 1–4) and one exploration pass of `TextRaster` and
`TextPages`: the text layers and the text cells in scope, the UI out; the flags from a flag-only subset
of a free emoji font embedded in the exe, given to DirectWrite as a private collection behind a font
fallback placed before the system's; the text cells drawing their emojis through `TextRaster` and
counting text elements. Four questions left open: the font, the subdivision flags, which emojis a text
cell colors, an emoji's width in its grid.

### Iteration 2 — 2026-11-02

The four open questions answered (Q&A 5–8): Twemoji Mozilla (COLRv0) is the flag font; the subdivision
flags are in its subset, with 🏴 and 🏴‍☠️; a text cell draws **every** emoji in color through
`TextRaster`, each two columns wide, its grid counting text elements. § The Flag Font, § Text Layers and
§ Text Cells updated; the Test Impact gains the subdivision flags, the other emojis of a text cell and a
styled one.

### Iteration 3 — 2026-11-02 — ✅ Implemented

Go given: code, the checks by hand and the documentation, in a worktree (`feature/text-flag-emojis`).
The scope is frozen as Iteration 2 left it.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project: checked by hand |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In which texts must the flags show? | The text layers and the text cells | 2026-11-02 |
| 2 | Segoe UI Emoji has no flags: where do they come from? | A font embedded in the exe, cut down to the flags, used for the flags only | 2026-11-02 |
| 3 | How is a flag entered? | Pasted or typed only — no picker | 2026-11-02 |
| 4 | Is the subject straightforward or tricky? | Straightforward: one exploration pass | 2026-11-02 |
| 5 | Which flag font? | Twemoji Mozilla (COLRv0) | 2026-11-02 |
| 6 | The subdivision flags in the subset? | Yes — 🏴 and 🏴‍☠️ take its style | 2026-11-02 |
| 7 | Text cells: flags only in color, or every emoji? | Every emoji | 2026-11-02 |
| 8 | Text cells: an emoji's width in columns? | 2 columns | 2026-11-02 |

---

*Last updated: 2026-11-02*
