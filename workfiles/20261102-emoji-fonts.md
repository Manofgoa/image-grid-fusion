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

`workfiles/20261102-text-flag-emojis.md` is **delivered** (merged as `22d7b2d`); this design was re-read
against it (Iteration 4). What it delivered, and what it means here:

| It delivered (RULES.md § Text Emojis) | What it means here |
|---|---|
| The **flag font** — Twemoji Mozilla cut down to its flags (`Imaging/TwemojiFlags.ttf`), in a private collection loaded from memory (`TextRaster.CreateFlagCollection`: in-memory loader, font set, `CreateFontCollectionFromFontSet`) | The shipped emoji fonts get a private collection the same way, from their files |
| **No font fallback**: the flag font **set on each flag sequence** of the layout (`TextRaster.SetFlagFont`, `SetFontCollection` / `SetFontFamilyName` on the ranges of `Emojis.Flags`) — a fallback left a sequence's invisible characters to the text's font, cutting it apart | The emoji font is set the same way, on each emoji element's range — never a fallback |
| England, Scotland, Wales and 🏴‍☠️ **joined at private code points** U+E000–U+E003 (`Emojis.Joined`) — DirectWrite never joins their tags | Those four stay the flag font's, whatever the emoji font |
| The flag font's **color glyphs' outlines filled** — Twemoji's base glyphs are empty, leaving no outline and no shadow | The shipped fonts are downloaded as published: their empty or missing outlines need another route (§ Rendering) |
| `Emojis.IsEmoji` / `Elements` — what an emoji is (`Emoji_Presentation`, VS16, keycaps, flags), one definition | The ranges the emoji font is set on |
| Twemoji's **CC-BY 4.0** attribution in the README's Tech paragraph | The shipped fonts' attributions go in the same place |

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

- **The emoji font is set on each emoji** of the layout, as the flag font is on each flag: the ranges of the
  text elements `Emojis.IsEmoji` recognises, `SetFontCollection` (the shipped fonts' collection, or the
  system's for an installed font) and `SetFontFamilyName` on each — never a font fallback, which would leave a
  sequence's invisible characters (ZWJ, VS16, a skin tone) to the text's font. The emoji font therefore
  **wins over the text's font** on an emoji, as the flag font does; a character that is no emoji (©, ♥
  without VS16, a digit) stays in the text's font.
- **The shipped fonts are private and loaded from memory**: each file read into memory and given its own
  collection the way the flag font is (`TextRaster.CreateMemoryCollection`: in-memory loader, font set,
  `CreateFontCollectionFromFontSet`), when a text first needs it, kept per thread in `TextRaster.Factories` and
  loaded again once `EmojiFonts.Generation` changes (a download, an update) — never installed into Windows, and
  never holding its file open, so an update can replace it.
- **The family set is DirectWrite's name of the font** (its WWS family, read from the collection), not the app's:
  OpenMoji's is `OpenMoji Color`. The drop-down and the style keep the app's name.
- **The default emoji font draws as today**: Segoe UI Emoji, nothing set — the system fallback, as
  delivered by the flag workfile; the icons (`UiIcons`) keep drawing that way.
- **The country flags follow the chosen emoji font** when it has them (it holds the regional indicators):
  its font is set on them too, after the flag font, so every emoji of a text has one style. Otherwise — Segoe
  UI Emoji, a font without flags — the flag font keeps them. **England, Scotland, Wales and 🏴‍☠️ stay the flag
  font's**, at their private code points: DirectWrite joins them in no other font.
- **A font missing** — a shipped one not downloaded, an installed one uninstalled — draws as Segoe UI Emoji;
  the style keeps its name.
- **Text layers only**: the text cells keep Segoe UI Emoji and the flag subset, as the flag workfile
  delivers them.
- **Formats**: `DrawTextLayout` with `ENABLE_COLOR_FONT` draws COLRv0, COLRv1 and CBDT in color on the WIC
  render target — checked with each shipped build (Iteration 6).
- **Outline and shadow from the color render**: they draw the emojis as plain shapes (`colorFont: false`),
  from the base glyphs' outlines — empty in Twemoji (the flag workfile found it) and in the COLRv0 builds,
  missing in the bitmap ones (CBDT). With an emoji font other than Segoe UI Emoji, the shape the outline's
  rings and the shadow's mask are made of is the **alpha of the text drawn in color**, at full opacity,
  instead of the plain-shape draw: the letters give the same shape, the emojis get theirs. Segoe UI Emoji
  keeps today's path.

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
- **Each font drawn as a sample** of three emojis in it (😀👍❤️), then its name: rendered through `TextRaster`
  (GDI draws emojis in one color), each sample cached once drawn. The closed box, narrow, shows the sample alone.
  No icon (§ Icons: the font drop-downs show each font in its face).
- **An emoji font is an ordinary option** of the text: it turns the Layers effect on, is one undo step, is
  reset with the text's style.

---

## Shipped Fonts

- **Four fonts**, pinned in `Imaging/EmojiFonts` (about 22 MB in all):

  | Font | Build pinned | Format | License | Latest release from |
  |---|---|---|---|---|
  | Twemoji Mozilla | `mozilla/twemoji-colr` v0.7.0, `Twemoji.Mozilla.ttf` | COLRv0 | Graphics CC-BY 4.0 | The release's asset |
  | Noto Color Emoji | `googlefonts/noto-emoji` v2026-09-24-unicode18_0, `2D/fonts/Noto-COLRv1.ttf` | COLRv1 | OFL 1.1 | The file at the release's tag |
  | OpenMoji | `hfg-gmuend/openmoji` 17.0.0, `font/OpenMoji-color-glyf_colr_0/…ttf` | COLRv0 | CC BY-SA 4.0 | The file at the release's tag |
  | Blobmoji | `C1710/blobmoji` v15.0, `Blobmoji.ttf` | CBDT | Apache 2.0 | The release's asset |
- **Downloaded at the first launch, after asking**: the app offers them while none is there — one
  confirmation, their names and total size; a **Yes** downloads them, a **No** is remembered
  (`AppSettings.EmojiFontsDeclined`) so the offer is not repeated; the ⚙ menu's **Download the emoji fonts…**
  offers them again while one is missing. A `--new-instance` makes no offer of its own accord (RULES.md § Single
  Instance).
- Into **`emoji-fonts\` next to the exe** (`FolderName`, kebab-case per `../CLAUDE.md` § Folder Names),
  from **pinned URLs, each checked against its SHA-256** — the engines' loop, `DirectEngineDownload`. Never
  installed into Windows.
- **⚙ Update the emoji fonts**, once one is there: the **latest release** of each font, found from its
  GitHub releases (`api.github.com/…/releases/latest`), no pinned hash (HTTPS only), replacing the file in
  `emoji-fonts\` — a download identical to the file changes nothing, one that does not start as a font is
  refused; the status line names the fonts updated and those that failed. A font whose download fails keeps its
  file. After a download or an update, the Emoji drop-down is read again and every text layer with an emoji font
  of its own is drawn again.
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

### Iteration 4 — 2026-11-02

Re-read against the delivered `workfiles/20261102-text-flag-emojis.md` (merged as `22d7b2d`), as Iteration 2
required. Its run replaced the font fallback by the flag font set on each flag sequence, joined the four
tagged flags at private code points, and filled its glyphs' empty outlines. Consequences here:

- **The emoji font is set on each emoji's range** (`Emojis.IsEmoji`), never a fallback — for the reason the
  flag run found; it wins over the text's font on an emoji.
- **The shipped fonts get a private collection of their own**, from their files, beside the flag font's.
- **Q&A 12 narrowed**: the chosen font takes the **country flags** when it has them; England, Scotland, Wales
  and 🏴‍☠️ stay the flag font's — DirectWrite joins them in no other font.
- **Outline and shadow from the color render's alpha** with any emoji font but Segoe UI Emoji: the shipped
  fonts are downloaded as published, their base glyphs empty (COLRv0) or missing (CBDT), so the fix the flag
  run made in its own font cannot be made in them.

§ Prerequisite, § Rendering updated.

### Iteration 5 — 2026-11-02 — ✅ Implemented

Go given: code, the checks by hand and the documentation, in a worktree (`feature/emoji-fonts`). The scope
is frozen as Iteration 4 left it.

### Iteration 6 — 2026-11-02 — 🧭 Implementation choices

Divergent choices of the run, each in the domain sections above:

- **The shipped fonts loaded from memory, one collection per font, when first needed** — not their files added
  to one font set: a collection on the files would hold them open, and the update could not replace them.
- **The family set is the one DirectWrite reads from the font** (its WWS name): OpenMoji's is `OpenMoji Color`,
  and setting the app's `OpenMoji` drew Segoe UI Emoji instead.
- **The builds pinned**: Noto's **COLRv1** (5 MB) rather than its CBDT one (10.7 MB), and Blobmoji's CBDT — both
  drawn in color by `DrawTextLayout` on this Windows; OpenMoji's `glyf_colr_0`.
- **The update** compares the download with the file (identical → nothing changes) and refuses a file that does
  not start as a font; its outcome names the fonts updated and failed.
- **The first-launch offer** shows while **no** emoji font is there; a partial download leaves it to the ⚙ menu.
  The test instance is recognised by the form's existing flag (`savesWindowSize`, false for `--new-instance`).
- **Redrawing the texts after a download or an update** goes through `GridPreview.SetLayers`: it is a step of the
  undo history, the step before it holding the texts as they were drawn.
- **The closed Emoji drop-down shows the sample alone**, 64 px wide; its list shows the sample and the name.
- **`DirectEngineDownload`** gained a download without ONNX Runtime, and its file save, hash and delete became
  public for the update; **`UiIcons.UpdateEmojiFonts`** (🔄) is the update's icon.
- **The checks by hand ran through the app's `TextRaster`** in a scratch harness, not the app's window: every
  font drawn with an outline and a halo (letters, 😀, 👍🏽, 👨‍👩‍👧, 🇫🇷, 🇪🇺, 🏴󠁧󠁢󠁳󠁣󠁴󠁿, 🏴‍☠️, ❤️, ©), a missing font drawn as Segoe
  UI Emoji, the real download (hashes checked) and the real update (nothing newer). The Emoji drop-down, the
  first-launch dialog, the ⚙ entries, the text styles and the exports are left to the delivery test.

No project rule was broken.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-11-02 | Rendering, the four fonts, the Emoji drop-down, the download and the update |
| Unit tests | 5 | 2026-11-02 | No test project: checked by hand through `TextRaster` (Iteration 6); the window's checks left to the delivery test |
| README | 5 | 2026-11-02 | The emoji font in the Layers section, the fonts credited in Tech; English and French |
| GLOSSARY | 5 | 2026-11-02 | *Emoji font* row, the text style's row; English and French |
| RULES | 5 | 2026-11-02 | § Text Emojis: the emoji font, the installed ones, the app's, the outlines |

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
