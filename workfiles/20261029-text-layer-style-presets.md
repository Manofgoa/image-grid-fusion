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
- **Applied** to the selected text layer in one click.

The meme style needs three settings the text layers do not have yet: **ALL CAPS**, **Italic** and a
**Shadow**. They are added to `LayerTextStyle` and to the Layers options here.

**Depends on** the Layers workfile (`20261029-layers.md`), still being adjusted in its own session:
its Iterations 12 (layers without cells) and 13 (fonts shown in their own face) touch the same
options. See § Sequencing.

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
| **Shadow** — on / off, color, size | | ✅ |
| Background box — on / off, color, opacity | ✅ | |

### New Settings

- **Italic**: DirectWrite's italic style (`CreateTextFormat`'s style argument); a family without an
  italic face gets DirectWrite's oblique.
- **ALL CAPS**: the text is **drawn** in capitals, the **typed text kept as typed** — turning it off
  gives the text back as it was, and the field shows what was typed. Upper-cased with the current
  culture (`é` → `É`); the emojis are untouched.
- **Shadow**: see Open Questions (its kind, and whether it excludes the outline). Proposed: a **soft
  dark halo around the letters**, as imgflip's — the text in the shadow's color, blurred, under the
  outline and the letters, over the box; its **size** a fraction of a line, as the outline's width.
  Default: off, black, 8 %.
- `LayerTextStyle.Default` gets them **off**: a new text looks as today.

---

## Built-in Styles

Shipped in the code, in this order (Q&A #3). Values proposed — fine-tuned on screen during the run.

| Name | Font | Bold | Italic | Caps | Color | Outline | Shadow | Box |
|---|---|---|---|---|---|---|---|---|
| **Default** | Segoe UI | ✅ | | | White | Black, 8 % | | |
| **Meme** | Impact | | | ✅ | White | | Black, 10 % | |
| **Subtitle** | Segoe UI | | | | White | | | Black, 60 % |
| **Label** | Segoe UI | ✅ | | | Black | | | White, 100 % |
| **Neon** | Arial Black | ✅ | | | Fluorescent yellow (255, 235, 0) | Black, 14 % | | |
| **Subtle** | Segoe UI | | | | White | Black, 3 % | | |

- **Default** is the app's own default style (`LayerTextStyle.Default`), listed first so a text can
  always be brought back to it — the agent's addition, open to review (Iteration 1).
- Impact and Arial Black ship with Windows. A style whose font is missing draws in the font
  DirectWrite falls back to, as a font typed in by hand would.

---

## The User's Styles

- **Save style…** saves the **selected text layer's current style** under a name asked in the app's
  name dialog (`NameDialog`), the name required.
- The **same name** as one of the user's styles → replaces it, after a confirmation. The name of a
  built-in style is refused (or see Open Questions on built-ins).
- Listed **after the built-in styles**, in the order saved.
- Kept between sessions (storage: see Open Questions), **out of the undo history** — like the
  effect presets (RULES.md § Effect Presets), saving or deleting a style is not a step.
- Each setting written by hand under a named key, a missing or foreign value giving its default —
  never a serialized `LayerTextStyle` (the rule of RULES.md § Effect Presets), so a later setting
  reads an older file.

---

## Applying a Style

- A click on a style **applies it to the selected text layer**: one style change, one step of the
  undo history by itself (`GridHistory`), like any change of its settings.
- Disabled while no text layer is selected, as the text's other settings are.
- The style **matching** the selected layer's current style exactly is shown as such (pressed /
  selected), none when it was changed by hand since.
- What a **new text** (Add text) gets: see Open Questions.

---

## UI

Where the styles are shown, applied and saved in the Layers options: see Open Questions — a
mockup of the candidate layouts is shown before the question.

The three new settings join the text's settings lines: **Italic** and **ALL CAPS** next to *Bold*,
the **Shadow** — checkbox, color split button (RULES.md § Color Options, its eyedropper and
sample), size slider — on a line of its own like the outline's.

---

## Sequencing

The Layers workfile's own session still has adjustments to deliver on the same
options (`MainForm.Layers.cs`, `TextRaster.cs`). See Open Questions: this workfile's run starts once
that session's run is delivered, or runs alongside it.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*):
no unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| Each built-in style applied to a text layer looks as its row says, emojis kept in color | The running app | — (by hand) |
| ALL CAPS draws capitals, the field keeps the typed text, off gives it back | The running app | — (by hand) |
| Italic, Shadow drawn in the preview and in a PNG export alike | The running app | — (by hand) |
| A saved style survives a restart; a same-name save replaces after confirmation | The running app | — (by hand) |
| Applying a style is one Ctrl+Z step; saving one is none | The running app | — (by hand) |

---

## Open Questions

- [x] ~~What does a style hold: the style only, + size, + place?~~ → The style only (Q&A #1)
- [x] ~~imgflip's missing settings (ALL CAPS, shadow, italic): added, or approximated?~~ → All three added (Q&A #2)
- [x] ~~Which built-in styles besides the meme one?~~ → Subtitle, Label, Neon, Subtle (Q&A #3)
- [ ] **UI**: where are the styles shown, applied and saved — a strip of style thumbnails, a drop-down
      with previews, a gallery opened by a button?
- [ ] **New text**: Add text uses `LayerTextStyle.Default` always, or the last style applied / used?
- [ ] **Management**: can the user's styles be renamed and deleted (context menu)? Are the built-in
      ones fixed, or can they be hidden / deleted too?
- [ ] **Storage**: in `settings.json` (an app setting), or a file of their own next to the exe
      (`text-styles.json`)?
- [ ] **Shadow kind**: a soft halo around the letters (imgflip's look), or a drop shadow offset
      down-right?
- [ ] **Shadow and outline**: independent (both may be on), or exclusive as imgflip's radio buttons
      (Shadow / Outline / None)?
- [ ] **Alignment**: imgflip has left / center / right for several lines; the text layers center
      them. Added to the style here, or out of scope?
- [ ] **Sequencing**: this run waits for the Layers session's run to be delivered, or runs alongside
      it (same files)?

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

---

*Last updated: 2026-10-29*
