# Text Layer Alignment

> Working document — the alignment of a text layer's lines: left, center, right or justified.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A text layer (`workfiles/20261029-layers.md` § Text Layer) draws its lines **centered** within the
widest one, always: `TextRaster.CreateLayout` sets `SetTextAlignment(CENTER)` once the widest line is
known. This workfile lets the user choose the alignment of the lines: **Left**, **Center**, **Right**
or **Justified**.

Components:

- `Imaging/TextRaster.cs` — `LayerTextStyle` (the record a text layer is drawn in) and the
  DirectWrite layout.
- `UI/MainForm.Layers.cs` — the Layers options, their style controls and `ChangeLayerStyle`.
- `UI/EffectIcons` — the drawn icons of the layer list's buttons (`EffectIcons.LayerAction`).

**Related**: `workfiles/20261029-text-layer-style-presets.md` (another session) asks in its Open
Questions whether the alignment belongs to the style. This workfile settles it: **it does** — a style
preset carries it.

---

## Style

- `LayerTextStyle` gets an **`Alignment`** member, a new enum `LayerTextAlignment` — `Left`, `Center`,
  `Right`, `Justified`.
- **Default: `Center`** — `LayerTextStyle.Default` keeps drawing the text as today, so a new text and
  every existing call look the same.
- Being part of the style, the alignment is in the `Layer` (immutable, held by `GridLayers`): the undo
  history covers it with no code of its own (RULES.md § Undo History), and a style preset of the
  related workfile carries it.

---

## Rendering

- The layer's **box does not change** with the alignment: its width stays the widest line's, its
  height the lines' (`TextRaster.Render`). Changing the alignment never moves nor resizes the layer —
  only the lines move inside it.
- `TextRaster.CreateLayout` sets the layout's text alignment from the style instead of the fixed
  `CENTER` (DirectWrite `DWRITE_TEXT_ALIGNMENT`: `LEADING` 0, `TRAILING` 1, `CENTER` 2, `JUSTIFIED` 3),
  still after the width is set to the widest line.
- The outline, the background box and the emojis follow the lines as drawn: they are drawn from the
  same layout.

### Justified

- Every line is **stretched to the widest line's width** by widening its spaces, **except the last
  line of a paragraph**, aligned **left** — the usual rule (Word, DirectWrite).
- A **paragraph**: see Open Questions.
- A line with **no space** (a single word, an emoji alone) cannot be stretched: see Open Questions.
- **Technical risk**: the text layers never wrap (`NO_WRAP`), every line ends with a line break typed
  by the user, and DirectWrite does not justify a line ending a paragraph — `JUSTIFIED` alone would
  likely leave every line left-aligned. The implementation checks it first; if so, the lines are
  stretched by the app:
  - either the line breaks inside a paragraph are given to DirectWrite as **line separators**
    (U+2028), which end a line without ending the paragraph, if DirectWrite justifies before them;
  - or each line to stretch gets its extra width spread over its spaces with
    `IDWriteTextLayout1.SetCharacterSpacing` on each space's range, the line's width read from
    `GetLineMetrics` — the alignment then `LEADING`.

  Whichever works is kept, and reported as an implementation choice.

---

## UI

- **Four icon buttons** in the Layers options, on the font line, **right after Bold**: Left, Center,
  Right, Justified — one pressed at a time, the selected text layer's alignment.
- Each is a toggle-looking button (a `CheckBox` with `Appearance.Button`, or a button drawn pressed),
  its icon drawn by `EffectIcons` like the layer list's — four lines of varying lengths, aligned as the
  button says, the classic pictograms — its name in its tooltip (`Align left`, `Center`,
  `Align right`, `Justify`).
- A click goes through `ChangeLayerStyle`, as every style control does: the text layer drawn again,
  the Layers turned on (RULES.md § Global Toolbar: acting on any option activates the effect).
- Enabled and synced like the other style controls (`MainForm.Layers.cs` around line 370 and 395):
  disabled without a text layer selected, showing `LayerTextStyle.Default` then.

---

## Documentation

- `GLOSSARY.md` / `GLOSSARY.fr.md`: the *Text layer* entry names the alignment among its settings.
- `README.md` / `README.fr.md`: the text layers' description, where it lists their settings.

---

## Test Impact

The app has **no test project** (`src/` holds `ImageGridFusion` only): nothing to create or update.
The behaviours below are checked by hand at launch.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] **Paragraph** (justified): what ends a paragraph — a **blank line** (the lines between two blank
      lines stretched, the last one of each left-aligned), or only **the end of the text** (every line
      stretched but the very last)?
- [ ] **Line without a space** (justified): left-aligned as the last line is, or its **letters spread**
      to the full width?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Scoped with the user (Q&A 1–5): the alignment is a member of `LayerTextStyle`, so style presets carry
it; Center by default, as today; in justified, the last line of a paragraph is left-aligned; four icon
buttons after Bold. The box keeps its size whatever the alignment. Justification of lines ended by a
typed line break is a DirectWrite risk, with two fallbacks the implementation picks from. Two
questions left: what a paragraph is, and a line without a space.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project |
| README | | | |
| Glossary | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the alignment live: in the text style, or on the layer apart from it? | In the style (`LayerTextStyle`) | 2026-10-29 |
| 2 | Justified: what becomes of a paragraph's last line? | Aligned left | 2026-10-29 |
| 3 | Which control in the Layers options? | Four icon buttons | 2026-10-29 |
| 4 | Is the exploration straightforward, or tricky / long? | Straightforward | 2026-10-29 |
| 5 | *(given by the user, unasked)* Default alignment | Center | 2026-10-29 |
| 6 | Justified: what ends a paragraph — a blank line, or the end of the text only? | | 2026-10-29 |
| 7 | Justified: a line without a space — left-aligned, or its letters spread? | | 2026-10-29 |

---

*Last updated: 2026-10-29*
