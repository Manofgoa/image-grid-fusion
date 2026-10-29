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
  history covers it with no code of its own (RULES.md § Undo History), and a text style carries it.
- Saved with the text styles (`text-styles.json`) under the key **`alignment`**, by name (`Left`,
  `Center`, `Right`, `Justified`) — a missing or foreign value gives `Center` (RULES.md § Text Styles).
- The text styles' tooltip (`TextStyleStrip.Describe`) names it when it is not the default: `aligned
  left`, `aligned right`, `justified`.

---

## Rendering

- The layer's **box does not change** with the alignment: its width stays the widest line's, its
  height the lines' (`TextRaster.Render`). Changing the alignment never moves nor resizes the layer —
  only the lines move inside it.
- `TextRaster.CreateLayout` sets the layout's text alignment from the style instead of the fixed
  `CENTER` (DirectWrite `DWRITE_TEXT_ALIGNMENT`: `LEADING` 0 for Left and Justified, `TRAILING` 1,
  `CENTER` 2), still after the width is set to the widest line.
- The outline, the background box and the emojis follow the lines as drawn: they are drawn from the
  same layout.

### Justified

- Every line is **stretched to the text's widest line** — across every paragraph — by widening its
  spaces between words, **except the last line of a paragraph**, aligned **left** — the usual rule
  (Word, DirectWrite). The spaces typed before a line's first word or after its last are left as they
  are.
- A **paragraph** ends at a **blank line** or at the end of the text: the lines between two blank
  lines are stretched, the last one of each left-aligned. A blank line itself draws nothing.
- A line with **no space** (a single word, an emoji alone) cannot be stretched: it is **left-aligned**,
  as a paragraph's last line is — its letters are never spread.
- **Stretched by the app** (`TextRaster.Justify`), never by DirectWrite's `JUSTIFIED`, which leaves a
  line ended by a line break — every line of a text layer, which never wraps — as it is: the layout's
  clusters (`GetClusterMetrics`) give each line's width and its spaces between words, and each such
  space of a line to stretch gets an equal share of the missing width as trailing spacing
  (`IDWriteTextLayout1.SetCharacterSpacing`), the alignment `LEADING`.

---

## UI

- **Four icon buttons** in the Layers options, on the font line, **after Bold, Italic and ALL CAPS**,
  before the color: Left, Center, Right, Justified — one pressed at a time, the selected text layer's
  alignment.
- Each is a `CheckBox` with `Appearance.Button` and `AutoCheck` off — pressed by
  `UpdateLayerOptions` only, so clicking the pressed one leaves it pressed — its icon drawn by
  `EffectIcons.Alignment` at the monitor's DPI with the layer list's — four lines of varying lengths,
  aligned as the button says, the classic pictograms — its name in its tooltip (`Align left`,
  `Center`, `Align right`, `Justify`, the last one saying what a paragraph is).
- A click goes through `ChangeLayerStyle`, as every style control does: the text layer drawn again,
  the Layers turned on (RULES.md § Global Toolbar: acting on any option activates the effect).
- Enabled and synced like the other style controls (`UpdateLayerOptions`): disabled without a text
  layer selected, showing a new text's style then.

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

- [x] ~~**Paragraph** (justified): what ends a paragraph — a blank line, or only the end of the
      text?~~ → A blank line (or the end of the text)
- [x] ~~**Line without a space** (justified): left-aligned as the last line is, or its letters spread
      to the full width?~~ → Left-aligned

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

### Iteration 2 — 2026-10-29

Q&A 6–7 answered: in justified, a paragraph ends at a blank line (or the text's end), and a line
without a space is left-aligned, its letters never spread. No open question left.

### Iteration 3 — 2026-10-29 — ✅ Implemented

Go given for code, unit tests and documentation (no test project: nothing to write there), in a
worktree, `.claude/worktrees/text-layer-alignment` on `feature/text-layer-alignment`, created from
`main` after the text styles (`20261029-text-layer-style-presets.md`) were merged into it.

### Iteration 4 — 2026-10-29 — 🧭 Implementation choices

- **The text styles were merged into `main` before the go**: `LayerTextStyle` had gained Italic, ALL
  CAPS, a shadow and its JSON (`text-styles.json`). The alignment followed RULES.md § Text Styles: its
  `alignment` key in `ToJson` / `FromJson`, and a word in the style strip's tooltip — only when it is
  not Center, the default, so the built-in styles' tooltips do not change.
- **Justified stretched by the app**, the second fallback of the design: `SetCharacterSpacing` on the
  spaces between words, the line widths read from the clusters (`GetClusterMetrics`) rather than
  `GetLineMetrics`, which gives no width. DirectWrite's `JUSTIFIED` and the U+2028 route were not
  tried: every line of a text layer ends a DirectWrite paragraph, and the clusters gave the paragraph
  and the no-space rules in one place.
- **Stretched to the text's widest line**, across paragraphs — the box's width — rather than the
  paragraph's own widest line, so the right edge is straight for the whole text.
- **Spaces before the first word or after the last** of a line are not widened: only the spaces
  between words are.
- **The buttons stand after ALL CAPS**, not right after Bold: Italic and ALL CAPS, merged since, sit
  after Bold, and the alignment buttons close the text's shape settings before the color.
- **Checked by a scratch program**, outside the repository: the four alignments rendered through
  `TextRaster.Render` — the same box for each, the right and justified lines as designed (two
  paragraphs, a one-word line, an emoji) — and the `alignment` key read back, a missing one giving
  Center.
- No project rule broken.

### Iteration 5 — 2026-10-29 — ⚙️ Post-implementation — No alignment by default, the text block

The user, after the delivery: *« il manque un truc essentiel : la taille de l'élément en layer ! On doit
pouvoir choisir un alignement mais aussi AUCUN alignement. Par défaut, AUCUN. Si on en choisit un, ça
crée une taille de bloc qu'on peut redimensionner directement dans la surface de travail. Et les sauts
de lignes sont calculés au sein de cette surface. »*

- A fifth choice, **None**, the **default** — replacing Center as the default (Q&A 5).
- Choosing an alignment gives the layer a **text block**: a size of its own, **resized directly on the
  working surface**, the lines **wrapped within it**.
- Design questions asked before any code: Q&A 8–13.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-29 | The alignment in the style, the layout, the four buttons |
| Unit tests | 3 | 2026-10-29 | Not applicable: no test project; checked by a scratch program |
| README | 3 | 2026-10-29 | `README.md`, `README.fr.md` |
| Glossary | 3 | 2026-10-29 | `GLOSSARY.md`, `GLOSSARY.fr.md`: Text layer, Text style |

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
| 6 | Justified: what ends a paragraph — a blank line, or the end of the text only? | A blank line | 2026-10-29 |
| 7 | Justified: a line without a space — left-aligned, or its letters spread? | Left-aligned | 2026-10-29 |
| 8 | None: how do the lines stand within the text? | | 2026-10-29 |
| 9 | The block: its width only, or its width and height? | | 2026-10-29 |
| 10 | The block's width when an alignment is chosen? | | 2026-10-29 |
| 11 | Justified with wrapping: what ends a paragraph? | | 2026-10-29 |
| 12 | A word longer than the block? | | 2026-10-29 |
| 13 | The alignment and the block: in the text style, or on the layer? | | 2026-10-29 |

---

*Last updated: 2026-10-29*
