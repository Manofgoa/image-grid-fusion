# Emoji Fonts

> Working document — a text layer's emojis drawn in an emoji font of the user's choice, other than
> Windows' Segoe UI Emoji, picked from a drop-down of their own.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a text layer's emojis are always drawn in **Segoe UI Emoji**: DirectWrite's system font fallback
picks it for every character the text's font lacks (`Imaging/TextRaster.CreateLayout`, the system
collection, no fallback of its own). The goal:

- an **emoji font** chosen per text, independent of the text's font: the letters stay in the text's font,
  the emojis are drawn in the emoji font;
- the emoji fonts listed in a **drop-down of their own**, next to the font drop-down;
- the fonts offered: the **emoji fonts installed** on Windows, plus a set of **emoji fonts shipped with
  the app** in a subfolder next to the exe, already downloaded, with a **menu entry to update them**.

Relevant components:

| Component | Role today |
|---|---|
| `Imaging/TextRaster` | Draws a text with DirectWrite through Direct2D (`DrawTextLayout`, color glyphs on for the letters, off for the outline and the shadow); `FontFamilies()` lists the installed families through GDI+ |
| `LayerTextStyle` (in `TextRaster.cs`) | The text's style — `Font`, bold, italic, caps, colors, outline, shadow, box — its `ToJson` / `FromJson` under named keys |
| `UI/MainForm.Layers.cs` | The Layers options: the font drop-down `_layerFont`, owner-drawn, each family in its own face (`DrawFontItem`) |
| `UI/TextStyles`, `UI/TextStyleStrip` | The text styles — built-in and the user's (`text-styles.json`) — and their strip of samples |
| `UI/UiIcons` | Renders the app's emoji icons through `TextRaster.Render`: must keep Segoe UI Emoji |
| `Explorer/DirectEngineDownload` | The engines' download loop: pinned URLs, SHA-256 checked, into a folder next to the exe |

### Prerequisite — Text Flag Emojis

**No development before `workfiles/20261102-text-flag-emojis.md` is delivered**, and this design is
re-read against what it delivered before the go. That workfile, being implemented in another session,
changes the same code:

| It adds | What it means here |
|---|---|
| A **private font collection** in `TextRaster.Factories`, loaded from memory (`CreateInMemoryFontFileLoader`, a font set, `CreateFontCollectionFromFontSet`) | The shipped emoji fonts join that infrastructure — files added to a font set — rather than a second one |
| A **font fallback** on every layout: the flags (regional indicators, 🏴, the tag characters) to a **Twemoji Mozilla flag subset** embedded in the exe, then the system's mappings | The emoji font's mappings slot into the same builder; their order against the flag subset is an open question |
| **Text cells** drawing every emoji in color through `TextRaster`, one emoji at a time, two columns wide | A text cell could take an emoji font too — the scope question below |
| Twemoji's **CC-BY 4.0** attribution in the README's technical paragraph | The shipped fonts' attributions go in the same place |

---

## Emoji Fonts Landscape

The emoji fonts that exist, as known at design time (to be checked when pinning the files):

| Font | Look | Color format | License | Usable |
|---|---|---|---|---|
| Segoe UI Emoji | Fluent 2D, flat (Windows 11) | COLR | Ships with Windows | ✅ the default |
| Noto Color Emoji (Google) | Android's, rounded | COLRv1 or CBDT (bitmaps) | OFL 1.1 | ✅ |
| Twemoji (Mozilla's build) | Twitter / X's | COLRv0 | Graphics CC-BY 4.0 | ✅ matches the Twitter format |
| OpenMoji | Thick outlines, illustration | COLRv0 / SVG builds | CC BY-SA 4.0 | ✅ |
| Blobmoji | Google's former blobs | CBDT | Apache 2.0 | ✅ |
| Fluent Emoji (Microsoft, 3D / Color / Flat) | 3D or flat | Assets MIT, no official font (community builds only) | MIT | ⚠️ to check |
| Noto Emoji, Segoe UI Symbol | Monochrome | Outlines | OFL / Windows | ✅ black and white |
| JoyPixels, Apple Color Emoji, Samsung | — | — | Proprietary | ❌ not redistributable |

---

## Rendering

- **The emoji font is a font fallback mapping**, added to the fallback the flag workfile builds
  (`IDWriteFactory2.CreateFontFallbackBuilder`): the emoji ranges mapped to the emoji font, the system's
  mappings after them (`GetSystemFontFallback`) for everything else; set on the layout
  (`IDWriteTextLayout2.SetFontFallback`). One fallback per emoji font in use, cached. The letters' font
  stays first: a character it has (©, ♥, a digit) stays in it, as today.
- **The shipped fonts are private**: their files added to the private font set the flag workfile creates
  (`AddFontFile` beside its in-memory flag subset → `CreateFontCollectionFromFontSet`), that collection
  passed as the mapping's — never installed into Windows.
- **The default emoji font draws as today**: Segoe UI Emoji, no mapping of the app's but the flag
  subset's — the icons (`UiIcons`) keep drawing that way.
- **The flags follow the chosen emoji font**: its mapping comes **before** the flag subset's and covers the
  flags' code points too (regional indicators, 🏴, the tag characters) when the font has them, so every
  emoji of a text has one style. The embedded Twemoji flag subset then only serves Segoe UI Emoji, which has
  no flags, and an emoji font without flags.
- **A font missing** — a shipped one not downloaded, an installed one uninstalled — draws as Segoe UI Emoji;
  the style keeps its name.
- **Text layers only**: the text cells keep Segoe UI Emoji and the flag subset, as the flag workfile
  delivers them.
- **Formats**: `DrawTextLayout` with `ENABLE_COLOR_FONT` draws COLRv0 for sure; COLRv1, CBDT and SVG glyphs
  depend on the Direct2D version — each shipped font's build is chosen for one that draws, checked when it is
  pinned (COLRv0 preferred when a font offers it).
- **Outline and shadow**: they draw the emojis as plain shapes (`colorFont: false`), from the base glyphs'
  outlines. A font whose base glyphs are empty (bitmap-only, some COLR builds) would leave its emojis without
  outline or shadow — checked per font; if one is kept anyway, the shapes are taken from the color render's
  alpha instead.

---

## User Interface

- **An Emoji drop-down** next to the font drop-down in the Layers options, listing the emoji fonts only,
  Segoe UI Emoji first (the default), then the others A→Z, installed and shipped together, a family once.
- **An emoji font** is recognised among the installed families as a **color font holding 😀** (U+1F600:
  `IDWriteFontFace2.IsColorFont`, `HasCharacter`) — whatever its name; Noto Emoji or Segoe UI Symbol, not
  color, are not emoji fonts.
- **The emoji fonts leave the main font drop-down** (`TextRaster.FontFamilies` leaves them out); a style
  naming one as its font is still read and drawn as before.
- **Part of the text style**: `LayerTextStyle.EmojiFont`, `"Segoe UI Emoji"` by default, under its own key
  (`emojiFont`) in `ToJson` / `FromJson` — a missing or blank value giving the default — and in
  `TextStyleStrip`'s tooltip; saved in the user's text styles. The built-in styles keep Segoe UI Emoji.
- **Each font drawn as a sample** of a few emojis in it, then its name: rendered through `TextRaster` (GDI
  draws emojis in one color), each sample cached once drawn. No icon (§ Icons: the font drop-downs show each
  font in its face).
- **An emoji font is an ordinary option** of the text: it turns the Layers effect on, is one undo step, is
  reset with the text's style.

---

## Shipped Fonts

- **Four fonts**: **Twemoji** (Mozilla's build, COLRv0, CC-BY 4.0), **Noto Color Emoji** (OFL 1.1),
  **OpenMoji** (CC BY-SA 4.0), **Blobmoji** (Apache 2.0). For each, the build that `DrawTextLayout` draws in
  color is chosen when it is pinned (COLRv0 preferred; Noto's COLRv1 or CBDT, Blobmoji's CBDT checked).
- **Downloaded at the first launch, after asking**: the app offers them while their folder is missing — one
  confirmation, their names and total size; a **Yes** downloads them, a **No** is remembered
  (`AppSettings`) so the offer is not repeated; the ⚙ menu offers them again while missing. A
  `--new-instance` makes no offer of its own accord (RULES.md § Single Instance).
- Into **`emoji-fonts\` next to the exe** (`FolderName`, kebab-case per `../CLAUDE.md` § Folder Names),
  from **pinned URLs, each checked against its SHA-256** — the engines' loop, `DirectEngineDownload`. Never
  installed into Windows.
- **⚙ Update emoji fonts**, once they are there: the **latest release** of each font, found from its
  GitHub releases, no pinned hash (HTTPS only), replacing the file in `emoji-fonts\`; the texts drawn again
  with it. A font whose download fails keeps its file.
- Their licenses credited in the README's technical paragraph, beside the flag subset's (CC-BY and
  CC BY-SA require the attribution).

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*): no
unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| A text layer with emojis in each emoji font: the letters in the text's font, the emojis in the chosen one, in color | The running app | — (by hand) |
| Each emoji font with an outline, a Halo and a Drop shadow: the emojis outlined and shadowed | The running app | — (by hand) |
| The Emoji drop-down: Segoe UI Emoji first, the installed and shipped fonts, each as a sample in its face, no duplicate | The running app | — (by hand) |
| A text with an emoji font exported as PNG, MP4 and copied alone: the emojis in that font | The running app | — (by hand) |
| The flags (🇫🇷, 🏴󠁧󠁢󠁳󠁣󠁴󠁿) with Segoe UI Emoji (Twemoji subset) and with Noto / OpenMoji (their own) | The running app | — (by hand) |
| The first launch without `emoji-fonts\`: the offer, **No** remembered, **Yes** downloading the four, checked | The running app | — (by hand) |
| The ⚙ menu's update: the latest releases downloaded, the texts drawn with them | The running app | — (by hand) |
| A user text style saved with an emoji font, applied to another text: the emoji font follows | The running app | — (by hand) |
| The main font drop-down: no emoji font left in it | The running app | — (by hand) |
| A style naming a font not downloaded: drawn in Segoe UI Emoji, no error | The running app | — (by hand) |
| The app's emoji icons (`UiIcons`) and the text cells' emojis unchanged | The running app | — (by hand) |

---

## Open Questions

- [x] ~~Which emoji fonts are shipped with the app?~~ → Twemoji, Noto Color Emoji, OpenMoji, Blobmoji
- [x] ~~How are they "pre-downloaded"?~~ → At the first launch, after asking
- [x] ~~What does the ⚙ menu's update fetch?~~ → The latest release of each font, no pinned hash
- [x] ~~Is the emoji font part of a text style?~~ → Yes, in the user's styles; the built-in ones keep Segoe UI Emoji
- [x] ~~Do the emoji fonts leave the main font drop-down?~~ → Yes
- [x] ~~How is an installed font recognised as an emoji font?~~ → A color font holding 😀 (U+1F600)
- [x] ~~Scope: text layers only, or also the text cells?~~ → Text layers only
- [x] ~~The flags with an emoji font holding its own?~~ → The chosen font's; the Twemoji subset only where the font has none

---

## Design Iterations

### Iteration 1 — 2026-11-02

Initial design from the scoping batch: the emoji font is a font fallback for the emojis only, the letters
staying in the text's font; picked from a drop-down of its own; the installed emoji fonts plus fonts shipped
in a subfolder next to the exe, already downloaded, updated from a menu entry. Landscape of the existing
emoji fonts recorded. The rendering route (a fallback of the app's, a private collection for the shipped
fonts) comes from reading `TextRaster`.

### Iteration 2 — 2026-11-02

The user asks to wait for the session *Caractères drapeaux dans les textes*
(`workfiles/20261102-text-flag-emojis.md`) before developing, and to take what it delivers into account.
§ Prerequisite added; § Rendering now builds on its private font collection and font fallback instead of
making its own; two open questions follow from it (the text cells' scope reworded, the flags' order
added). Test Impact rewritten as checks by hand: the repository has no test project.

### Iteration 3 — 2026-11-02

The eight open questions answered (Q&A 5–12): the four fonts shipped (Twemoji, Noto Color Emoji, OpenMoji,
Blobmoji), downloaded at the first launch after asking, updated from the ⚙ menu to their latest releases;
the emoji font part of the user's text styles; the emoji fonts out of the main drop-down, recognised as
color fonts holding 😀; text layers only; the flags from the chosen font when it has them. Decided with
them, to review: a **No** to the first-launch offer remembered, the ⚙ menu offering the download again; no
offer from a `--new-instance`; the first download pinned (SHA-256), the update not; a missing font drawn as
Segoe UI Emoji, its name kept. § Rendering, § User Interface, § Shipped Fonts and the Test Impact updated.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| GLOSSARY | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does a chosen emoji font apply to in a text layer? | The emojis only — the letters stay in the text's font | 2026-11-02 |
| 2 | How are the emoji fonts presented? | A drop-down of their own, next to the font drop-down | 2026-11-02 |
| 3 | Where do the emoji fonts offered come from? | The installed ones, plus fonts shipped with the app in a subfolder, pre-downloaded, with a menu entry to update them | 2026-11-02 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-11-02 |
| 5 | Which emoji fonts are shipped? | Twemoji, Noto Color Emoji, OpenMoji, Blobmoji | 2026-11-02 |
| 6 | How are they pre-downloaded? | At the first launch, after asking | 2026-11-02 |
| 7 | What does the update fetch? | The latest release of each font | 2026-11-02 |
| 8 | Is the emoji font part of a text style? | Yes — the user's styles; the built-in ones keep Segoe UI Emoji | 2026-11-02 |
| 9 | Do the emoji fonts leave the main font drop-down? | Yes | 2026-11-02 |
| 10 | How is an installed emoji font recognised? | A color font holding 😀 | 2026-11-02 |
| 11 | Text layers only, or the text cells too? | Text layers only | 2026-11-02 |
| 12 | The flags with an emoji font holding its own: the chosen font's, or the Twemoji flag subset's? | The chosen font's | 2026-11-02 |

---

*Last updated: 2026-11-02*
