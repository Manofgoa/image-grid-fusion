# Text Layer Style Presets

> Working document — named text styles for the text layers: built-in ones, a meme style among them,
> and the user's own saved from the selected text layer.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A text layer (`workfiles/20261029-layers.md` § Text Layer) has its own style, `LayerTextStyle`
(`Imaging/TextRaster.cs`): font and bold, color, outline (color, width), background box (color,
opacity). Setting a look by hand takes a dozen clicks every time.

This workfile adds **text styles** — named `LayerTextStyle`s:

- **Built-in styles** shipped with the app, one of them looking like imgflip's meme text (the
  user's capture: Impact, ALL CAPS, white, a dark shadow around the letters).
- **The user's styles**, saved from the selected text layer's current style, under a name.
- **Applied** to the selected text layer in one click, from a **strip of style thumbnails** in the
  Layers options; the last one applied is the style of the next new text.

The meme style needs three settings the text layers do not have yet: **ALL CAPS**, **Italic** and a
**Shadow**. They are added to `LayerTextStyle` and to the Layers options here.

**Implemented after** the Layers workfile's pending adjustments (`20261029-layers.md`, Iterations 12
and 13), which touch the same options — see § Sequencing.

---

## What a Text Style Holds

The **style only** (Q&A #1): what `LayerTextStyle` holds — never the layer's text, size, place or
angle, nor its cell effects (`ImageLook`). Applying a style to a layer changes how its letters are
drawn, nothing else; its box follows the new content as shown, as for any style change.

| Setting | Today | Added here |
|---|---|---|
| Font, Bold | ✅ | |
| **Italic** | | ✅ on / off |
| **ALL CAPS** | | ✅ on / off |
| Color | ✅ | |
| Outline — on / off, color, width | ✅ | |
| **Shadow** — on / off, kind, color, size | | ✅ |
| Background box — on / off, color, opacity | ✅ | |

The **alignment** of several lines stays out of scope (Q&A #11): they stay centered.

### New Settings

- **Italic**: DirectWrite's italic style (`CreateTextFormat`'s style argument); a family without an
  italic face gets DirectWrite's oblique.
- **ALL CAPS**: the text is **drawn** in capitals, the **typed text kept as typed** — turning it off
  gives the text back as it was, and the field shows what was typed. Upper-cased with the current
  culture (`é` → `É`); the emojis are untouched.
- **Shadow** (Q&A #8, #9) — two kinds, chosen in its options:

  | Kind | Drawn |
  |---|---|
  | **Halo** | A soft blur of the text in the shadow's color, all around the letters — imgflip's look |
  | **Drop** | The same blur, offset down-right at 45° |

  - Its **size** is a fraction of a line, as the outline's width: the halo's blur radius; for the
    drop, its offset, blurred over half of it.
  - **Independent of the outline** (Q&A #9): both may be on; the order from the bottom is the box,
    the shadow, the outline, the letters. The emojis cast a shadow as plain shapes, as they are
    outlined.
  - Default: off, Halo, black, 8 %. The bitmap's margin (`TextRaster.Render`'s `pad`) grows to hold
    it, so it is never cut.
- `LayerTextStyle.Default` gets them **off**: a new text without a style applied looks as today.

---

## Built-in Styles

Shipped in the code, in this order (Q&A #3). Values proposed — fine-tuned on screen during the run.

| Name | Font | Bold | Italic | Caps | Color | Outline | Shadow | Box |
|---|---|---|---|---|---|---|---|---|
| **Default** | Segoe UI | ✅ | | | White | Black, 8 % | | |
| **Meme** | Impact | | | ✅ | White | | Halo, black, 10 % | |
| **Subtitle** | Segoe UI | | | | White | | | Black, 60 % |
| **Label** | Segoe UI | ✅ | | | Black | | | White, 100 % |
| **Neon** | Arial Black | ✅ | | | Fluorescent yellow (255, 235, 0) | Black, 14 % | | |
| **Subtle** | Segoe UI | | | | White | Black, 3 % | | |

- **Default** is the app's own default style (`LayerTextStyle.Default`), listed first so a text can
  always be brought back to it — the agent's addition (Iteration 1), kept.
- Impact and Arial Black ship with Windows. A style whose font is missing draws in the font
  DirectWrite falls back to, as a font typed in by hand would.
- **Fixed** (Q&A #7): no rename, no delete, no replace; their names are reserved.

---

## The User's Styles

- **Save style…** saves the **selected text layer's current style** under a name asked in the app's
  name dialog (`NameDialog`), the name required; disabled while no text layer is selected.
- The **same name** as one of the user's styles → replaces it, after a confirmation. A built-in
  style's name is refused, the dialog saying why.
- Listed **after the built-in styles**, in the order saved.
- **Managed from a thumbnail's context menu** (Q&A #7), the user's styles only:
  - *Rename…* — the name dialog, the same rules as Save;
  - *Replace with current style* — the selected text layer's style, disabled without one;
  - *Delete* — opening a submenu whose single item does it (*Delete <name>*), as the People's
    decisions taking something away (RULES.md § People).
- Kept in **`text-styles.json` next to the exe** (Q&A #10), read and written by one class
  (`TextStyles`), like `favorites.json` and `people.json`: the user's styles and the last style
  applied (§ Applying a Style). Each setting written by hand under a named key, a missing or foreign
  value giving its default — never a serialized `LayerTextStyle` (the rule of RULES.md § Effect
  Presets), so a later setting reads an older file. A file that cannot be read gives no user style
  and says so in the status line; one that cannot be written says so too.
- **Out of the undo history**, like the effect presets: saving, renaming, replacing or deleting a
  style is not a step. They are user actions: a `--new-instance` writes them too, as `people.json`.

---

## Applying a Style

- A click on a style's thumbnail **applies it to the selected text layer**: one style change, one
  step of the undo history by itself (`GridHistory`), like any change of its settings.
- The thumbnails are **disabled while no text layer is selected**, as the text's other settings are.
- The thumbnail of the style **matching** the selected layer's current style exactly is **pressed**;
  none once a setting was changed by hand.
- **A new text takes the last style applied** (Q&A #6): *Add text* (and a pasted text made a layer)
  uses it; **Default** until one is applied. It is remembered between sessions in
  `text-styles.json`, by name: a style deleted or renamed since gives Default (renaming carries it
  over to the new name).

---

## UI

**A strip of style thumbnails** (Q&A #5, layout A), in the Layers options, between the text field
and the text's settings lines:

- One **thumbnail per style**, in the option thumbnails' format (RULES.md § Options Toolbar,
  `ThumbnailStrip`): **"Aa"** drawn by `TextRaster` in the style — "AA" under ALL CAPS — over the
  cell's grey, its name below, the matching one pressed; its tooltip names the style and sums up its
  settings.
- The built-in styles first, then a line, then the user's; **Save style…** ends the strip, set
  apart as an action by a wider gap and a line.
- Past the width it has, the strip **scrolls horizontally** (its own scroll bar) — the options
  toolbar's height unchanged.
- The thumbnails are drawn once per style and DPI, again when a style is saved or replaced.

The three new settings join the text's settings lines:

- **Italic** and **ALL CAPS** checkboxes next to *Bold*.
- The **Shadow** on a line of its own, like the outline's: its checkbox, two option thumbnails
  **Halo** and **Drop**, its color split button (RULES.md § Color Options, its eyedropper and
  sample), its size slider. Acting on any of them turns the shadow on, as the outline's controls do.

---

## Sequencing

The Layers workfile's own session still has adjustments to deliver on the same files
(`UI/MainForm.Layers.cs`, `Imaging/TextRaster.cs`). This workfile is designed now; its
**implementation waits** until that session's run is delivered (Q&A #12), and starts from the `main`
holding it.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*):
no unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| Each built-in style applied to a text layer looks as its row says, emojis kept in color | The running app | — (by hand) |
| ALL CAPS draws capitals, the field keeps the typed text, off gives it back | The running app | — (by hand) |
| Italic, Shadow (Halo and Drop, with and without the outline) drawn in the preview and in a PNG export alike, never cut | The running app | — (by hand) |
| A saved style survives a restart; a same-name save replaces after confirmation; a built-in name is refused | The running app | — (by hand) |
| Rename, Replace, Delete from the context menu; none on a built-in style | The running app | — (by hand) |
| Add text takes the last style applied, also after a restart; Default once it is deleted | The running app | — (by hand) |
| Applying a style is one Ctrl+Z step; saving, renaming, deleting one is none | The running app | — (by hand) |

---

## Open Questions

- [x] ~~What does a style hold: the style only, + size, + place?~~ → The style only (Q&A #1)
- [x] ~~imgflip's missing settings (ALL CAPS, shadow, italic): added, or approximated?~~ → All three added (Q&A #2)
- [x] ~~Which built-in styles besides the meme one?~~ → Subtitle, Label, Neon, Subtle (Q&A #3)
- [x] ~~**UI**: where are the styles shown, applied and saved — a strip of style thumbnails, a drop-down
      with previews, a gallery opened by a button?~~ → A strip of style thumbnails (Q&A #5)
- [x] ~~**New text**: Add text uses `LayerTextStyle.Default` always, or the last style applied / used?~~ → The last style applied, remembered (Q&A #6)
- [x] ~~**Management**: can the user's styles be renamed and deleted (context menu)? Are the built-in
      ones fixed, or can they be hidden / deleted too?~~ → The user's renamed, replaced, deleted; the built-in ones fixed (Q&A #7)
- [x] ~~**Storage**: in `settings.json` (an app setting), or a file of their own next to the exe
      (`text-styles.json`)?~~ → `text-styles.json` (Q&A #10)
- [x] ~~**Shadow kind**: a soft halo around the letters (imgflip's look), or a drop shadow offset
      down-right?~~ → Both, a choice of the shadow's options (Q&A #8)
- [x] ~~**Shadow and outline**: independent (both may be on), or exclusive as imgflip's radio buttons
      (Shadow / Outline / None)?~~ → Independent (Q&A #9)
- [x] ~~**Alignment**: imgflip has left / center / right for several lines; the text layers center
      them. Added to the style here, or out of scope?~~ → Out of scope (Q&A #11)
- [x] ~~**Sequencing**: this run waits for the Layers session's run to be delivered, or runs alongside
      it (same files)?~~ → After its delivery (Q&A #12)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Requested: text style presets for the text layers — save one from the current settings, and ship
several, one looking like imgflip's meme text (the user's capture).

Scoped with the user (Q&A #1–#4): a style holds the style only; ALL CAPS, Italic and a Shadow are
added to the text layers for the meme style; Subtitle, Label, Neon and Subtle ship beside it; a
single exploration pass. Explored: `LayerTextStyle` and `TextRaster` (`Imaging/TextRaster.cs`), the
Layers options (`UI/MainForm.Layers.cs`), `AddTextLayer` using `LayerTextStyle.Default`; no test
project.

Proposed: the built-in table with values; a **Default** entry added by the agent; Save style… through
`NameDialog`, same name replacing after a confirmation; styles out of the undo history, written by
hand under named keys; applying one is a style change, one undo step; the style matching the
selected layer shown as such. Eight questions left open.

### Iteration 2 — 2026-10-29

The eight questions answered (Q&A #5–#12), three layouts drawn before the UI one:

- A **strip of style thumbnails** — "Aa" in each style — between the text field and the settings,
  Save style… ending it, scrolling horizontally past its width.
- A **new text takes the last style applied**, remembered between sessions; Default at first.
- The **user's styles** renamed, replaced and deleted from a thumbnail's context menu, the delete
  through a submenu; the **built-in ones fixed**, their names reserved.
- The **Shadow** has two kinds, **Halo** and **Drop**, chosen in its options; it is **independent of
  the outline**, drawn under it.
- Kept in **`text-styles.json`** next to the exe, with the last style applied (by name).
- The **alignment** of several lines stays out of scope.
- The implementation **waits for the Layers session's delivery**.

Agent's choices within those answers, open to review: the last style applied remembered by name (a
deleted one giving Default, a renamed one followed); the shadow's size a fraction of a line for both
kinds, the drop blurred over half its offset; the shadow's kinds as two option thumbnails; the
**Default** entry kept first among the built-in styles.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does a text style hold? | The style only | 2026-10-29 |
| 2 | For the imgflip-like style, add the missing settings (ALL CAPS, shadow, italic)? | All three: ALL CAPS, shadow and italic | 2026-10-29 |
| 3 | Which built-in styles besides the imgflip-like one? | Subtitle, Label, Neon, Subtle | 2026-10-29 |
| 4 | Is the exploration straightforward, or tricky / long? | Straightforward | 2026-10-29 |
| 5 | UI: a strip of style thumbnails, a drop-down with previews, or a gallery opened by a button? | A strip of style thumbnails (layout A) | 2026-10-29 |
| 6 | New text: always the default style, or the last style applied? | The last style applied, remembered | 2026-10-29 |
| 7 | Management: rename / delete the user's styles; the built-in ones fixed or deletable? | The user's editable, the built-in ones fixed | 2026-10-29 |
| 8 | Shadow: a soft halo around the letters, or a drop shadow offset down-right? | Both, a choice in its options | 2026-10-29 |
| 9 | Shadow and outline: independent, or exclusive? | Independent | 2026-10-29 |
| 10 | Storage: `settings.json`, or a file of their own? | `text-styles.json` | 2026-10-29 |
| 11 | Alignment of several lines: added to the style, or out of scope? | Out of scope | 2026-10-29 |
| 12 | Sequencing: after the Layers session's run, or alongside it? | After its delivery | 2026-10-29 |

---

*Last updated: 2026-10-29*
