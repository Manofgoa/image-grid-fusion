# Background Effect

> Working document — a second effect, **Background**, turning the fill painted behind a cell's
> image into a per-cell setting: automatic or chosen color, opacity, or no fill at all.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every cell is filled, before its image is drawn, with an **automatic band color** computed
from the image (`BandColor.For`, painted by `Compositor.DrawCell`). This fill is always there and
always opaque.

The **Background** effect makes that fill a toggle of the effects toolbar, with options:

- **Active by default** — it is the state of every new image placed in a cell, so the rendering
  of a fresh grid is unchanged.
- **Automatic color** checkbox, checked by default — the current band-color rules.
- **Opacity** slider, 0–100 %, default 100 %.
- **Color** button, painted with the background color in use; a click opens the standard color
  dialog.
- **Inactive** — nothing is painted behind the image: the cell is **transparent**, shown in the
  preview by a grey / white **checkerboard**, as in drawing apps.

It follows every effect rule of [RULES.md](../RULES.md) (state on the image's immutable look,
not persisted, toolbar clicks, options row, rendering in `Compositor.DrawCell`), except where
this workfile amends them (see *Open Questions*).

---

## Current State (explored)

| Topic | Where | What it does today |
|---|---|---|
| Automatic color | `Composition/BandColor.cs:87-146` | Uniform edge bands (≥3 agreeing sides, ΔE76 ≤ 10) → their average; else most frequent color on the edge bands; else the image's dominant color (`DominantColor.cs`) |
| Scope and cache | `Composition/SourceImage.cs:15,44`, `BandColor.cs:36,90-97` | One `BandColor` per source image; recomputed when the shown part (crop / zoom / move) changes, last answer cached |
| Painting | `Composition/Compositor.cs:81-85` | Fills the **whole cell** with that color before drawing the image; the black-and-white look desaturates it (`Gray()`, `Compositor.cs:169-172`) |
| Animations | `BandColor.cs:9-14`, `Imaging/GridExport.cs:158` | Color taken from the still frame and kept while playing; one export path recomputes it per frame |
| Effect state | `Composition/ImageLook.cs:6-13,64-95,142` | `ImageEffect` enum (order = button order); `IsActive` / `Activate` / `Deactivate` switches; `WithoutEffects()` returns `ImageLook.None` |
| Toolbars | `UI/MainForm.cs:38,48,78,152,939` | Effect buttons generated from the enum; options rows listed by hand; `ToggleEffect` implements the click table |
| Icons | `UI/EffectIcons.cs` | One vector-drawn method per icon |
| Transparency | `Compositor.cs:34`, `Imaging/GridExport.cs:91`, `CarouselExport.cs:56`, `VideoEncoder.cs:69` | **None**: `Compositor.Render` is 24 bpp, exports are 32 bpp RGB (no alpha); the preview cache (`UI/GridPreview.cs:368`) is ARGB but always painted opaque; no checkerboard anywhere |
| Grid background | `GridLayout.cs` | Cells edge to edge, no gap, no global background color |

---

## Effect State

- New `ImageEffect.Background` case, and a `BackgroundEffect` immutable record on `ImageLook`,
  next to `BlurEffect`:

  | Field | Default | Meaning |
  |---|---|---|
  | `Automatic` | `true` | Color from the band-color rules |
  | `Color` | — (unset) | The chosen color, used when `Automatic` is off |
  | `Opacity` | `1.0` (100 %) | Alpha of the fill, 0–1 |

- `ImageLook.Background` is `null` when the effect is **inactive**, like `Blur`.
- The **default look of a new image** has the Background active with its defaults — every path
  that places a new image in a cell starts from it instead of `ImageLook.None`.
- Nothing is persisted (effects rule).

## Effects Toolbar and Options Toolbar

- One more toggle in the effects row, with its own icon in `EffectIcons`. Clicks follow the
  RULES.md table (inactive → activate + select; active → select; active and selected →
  deactivate).
- Options row of the Background, in this order:
  1. **Automatic color** checkbox — checked by default.
  2. **Opacity** slider 0–100 %, default 100 %, with its value label (same layout as the blur's
     intensity: icon + slider + label).
  3. **Color** button — its face is painted with the **color in use** (the automatic color when
     the checkbox is checked, the chosen one otherwise).
- Color button:
  - **Always enabled**, even in automatic mode.
  - A click opens the standard WinForms `ColorDialog`, preselected on the color in use.
  - **OK** → the chosen color becomes the background color and the **Automatic color checkbox
    is unchecked**. **Cancel** → nothing changes.

## Rendering

- In `Compositor.DrawCell`, the existing fill becomes:

  | Background state | Fill behind the image |
  |---|---|
  | Active, automatic | Band color (current rules), alpha = opacity |
  | Active, chosen color | Chosen color, alpha = opacity |
  | Inactive | Nothing — the cell stays transparent |

- The fill still covers the **whole cell**, so it also shows through the transparent pixels of
  the image itself (PNG with alpha), as today.
- Preview: wherever the result is not fully opaque (background inactive, opacity < 100 %,
  transparent image pixels), a grey / white **checkerboard** is visible underneath.
- Exports: the rendering bitmaps must carry alpha for transparency to survive (see *Open
  Questions*).

---

## Test Impact

To be settled with the user (see *Open Questions*). The solution has no test project, and every
previous workfile stayed test-free.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 — not only the
blocking ones.

- [ ] **Resets vs. "active by default"** — RULES.md says a replaced image and the cell's *Reset*
  tool leave *no effect active*. Proposal: for these two events every effect goes back to its
  **defaults** (the Background active, automatic, 100 %), and the RULES.md table is amended
  accordingly. Deleting an image (images shifting into another cell) follows the same rule.
- [ ] **Exports and transparency** — which formats keep the transparency, and what do formats
  without alpha (JPEG, video, GIF) show where the cell is transparent?
- [ ] **Checkerboard** — preview only (never in an export)? Square size fixed in logical px
  (e.g. 8, scaled with `LogicalToDeviceUnits`) or relative to the cell?
- [ ] **Black-and-white effect** — does it also desaturate a **chosen** color (it already
  desaturates the automatic one)?
- [ ] **Re-checking *Automatic color*** — is the chosen color remembered, so unchecking again
  brings it back? Or does unchecking always start from the current automatic color?
- [ ] **Button position** — where does the Background toggle sit in the effects row (enum order
  = button order: Zoom, Rotate, Flip, Black and white, Blur)?
- [ ] **Unit tests** — stay test-free like every previous workfile?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Initial design from the user's brief and the scoping answers (Q&A #1–#4): Background effect
active by default, automatic color checkbox (checked by default), opacity slider 0–100 % default
100 %, color button always enabled — choosing a color unchecks the automatic mode; inactive =
transparent, shown by a checkerboard. Codebase explored in one pass (depth: straightforward):
band-color rules, blur wiring, transparency support — findings in *Current State*.

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
| 1 | When the Background is inactive, what does the cell area not covered by the image show? | Transparent, shown by grey / white squares as in drawing apps | 2026-09-25 |
| 2 | With *Automatic color* checked, how does the color button behave? | Enabled; choosing a color unchecks the automatic mode | 2026-09-25 |
| 3 | Opacity slider range and default? | 0–100 %, default 100 % | 2026-09-25 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — single scout pass | 2026-09-25 |
| 5 | Resets (image replaced, image deleted, cell *Reset*): back to the effects' defaults, RULES.md amended? | | 2026-09-25 |
| 6 | Exports: which formats keep the transparency, what do the others show? | | 2026-09-25 |
| 7 | Checkerboard: preview only? Square size? | | 2026-09-25 |
| 8 | Does the black-and-white effect desaturate a chosen color too? | | 2026-09-25 |
| 9 | Re-checking *Automatic color*: is the chosen color remembered? | | 2026-09-25 |
| 10 | Position of the Background toggle in the effects row? | | 2026-09-25 |
| 11 | Unit tests: stay test-free? | | 2026-09-25 |

---

*Last updated: 2026-09-25*
