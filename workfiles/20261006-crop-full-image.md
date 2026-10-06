# Crop Full Image

> Working document — a **100 %** button in the Crop options, bringing the kept part back to the
> whole image in one click.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The Crop effect (`workfiles/20260930-crop-effect.md`) starts 10 % in from each edge, and its
bars can only be dragged one at a time. Bringing the kept part back to the **whole image** means
dragging four bars onto the edges — and when the kept part is small, that is exactly when the user
wants it back whole.

A **100 %** button in the Crop options does it in one click.

Relevant components:

| Component | Role |
|---|---|
| `Composition/CropEffect.cs` | The crop's state: four sides in fractions of the image as loaded, an optional ratio |
| `UI/MainForm.cs` | The Crop options row (`_cropRatios`), `ChangeLook`, `UpdateEffects`, tooltips |
| `UI/EffectIcons.cs` | The icons of the options buttons, drawn at the monitor's DPI |
| `UI/GridPreview.cs` | Untouched: the edit view and the free format already follow any new look |

---

## Placement

**In the options toolbar** (Q&A 1), after the ratio buttons, the effect's own *Reset* still ending
the row:

```
[ Free ] [ 1:1 ] [ 4:3 ] [ 16:9 ] [ 9:16 ]  [ ⛶ 100 % ]                    [ ↺ Reset ]
```

Why not inside the kept part, on the cell:

- An effect's options live in the options toolbar (RULES.md § Options Toolbar); the cell itself
  only keeps the ×, the ✥ handle and the gestures (§ Effects Toolbar).
- A button there would hide part of the image, compete with the drag that moves the kept part, and
  no longer fit when the kept part is small — the very case it is for.
- In the toolbar it also works **while the crop is off**: acting on an option activates the effect,
  so one click turns the crop on, at 100 %.

No double-click shortcut on the kept part (Q&A 1).

---

## Behaviour

- **Click**: the kept part becomes the **whole image** — every side on the image's edge — and the
  ratio is **freed** (*Free* pressed), whatever ratio was kept (Q&A 2): no ratio but the image's own
  fits the whole image.
- It is an **option**: like every option, it turns the crop on first, from its kept settings, then
  applies (RULES.md § Options Toolbar). The crop's own *Reset* still brings back its default state
  — off, 10 % in from each edge.
- An **action button**, not a toggle: it carries no pressed state, and stays enabled while the kept
  part is already whole (clicking it then only frees the ratio, if one was kept).
- The edit view, the automatic background, the canvas sizing and the **Free** output format follow
  the new kept part as they follow any crop change: the change goes through `ChangeLook` like the
  ratio buttons, so nothing new is needed in `GridPreview`.

### Look

- **Label**: an icon then **100 %** (Q&A 3); the icon, drawn by `EffectIcons`, shows the crop marks
  pushed out onto the four corners of a frame — the crop spread over the whole image.
- **Tooltip**: *Keeps the whole image: the bars back on its edges, the ratio freed.*

---

## Code Plan

| Where | What |
|---|---|
| `CropEffect.Whole()` | The same crop keeping the whole image, its ratio freed: sides 0 / 0 / 1 / 1, `Ratio = null` |
| `EffectIcons.WholeImage(size)` | The button's icon |
| `MainForm` | The `_cropWhole` button in the Crop options row after the ratios — an `OptionButton` like them, never pressed — its tooltip, its click → `SetCropWhole` → `ChangeLook(ImageEffect.Crop, look => look.WithCrop(crop.Whole()))`, its icon redrawn at the DPI and disposed with the others |

---

## Test Impact

The solution holds **no test project** and every previous workfile stayed test-free; the go asked
for code and documentation only. Nothing is pinned.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| *(none — test-free, see above)* | | |

---

## Open Questions

- [x] ~~Where does the button go: the options toolbar, the toolbar plus a double-click in the kept
  part, or inside the kept part?~~ → **Options toolbar** only *(recommended default — see Q&A 1)*
- [x] ~~Under a kept ratio, what does 100 % do?~~ → **Frees the ratio**, the whole image kept
  *(recommended default — see Q&A 2)*
- [x] ~~Label?~~ → **Icon + "100 %"**, with a tooltip *(recommended default — see Q&A 3)*
- [x] ~~Depth of the exploration?~~ → A single direct pass: the crop code is two files

---

## Design Iterations

### Iteration 1 — 2026-10-01

The user asked for a 100 % button on the Crop and for advice on its placement: inside the crop zone
or in the toolbar. Three placements were drawn as mockups — options toolbar, options toolbar plus a
double-click, inside the kept part — the options toolbar recommended (see § Placement). Four
scoping questions were asked: placement, behaviour under a kept ratio, label, exploration depth.

### Iteration 2 — 2026-10-06 — ✅ Implemented

The scoping questions were interrupted twice (the second time by the app being closed) and never
answered. On 2026-10-06 the user gave the go — *"Vas y dans un worktree code et doc"* — without
answering them: the **recommended option of each question** is applied and recorded as the
design (Q&A 1–4). Code and documentation, no tests (no test project), in a dedicated worktree
(`feature/crop-full-image`), fast-forwarded into `main` and removed at the end.

### Iteration 3 — 2026-10-06 — 🧭 Implementation choices

- **The button's control**: an `OptionButton` — the same grey toggle-looking `CheckBox` as the
  ratio buttons, `AutoCheck` off — never checked, so it sits in the row with their exact look while
  carrying no pressed state, as the design asks.
- **The icon** (`EffectIcons.WholeImage`): a thin dark-orange frame, the orange crop marks of the
  Crop tab's icon pushed out onto its four corners.
- **No rule change**: the button is an ordinary option of an effect; RULES.md already covers it
  (§ Options Toolbar), so only README.md and GLOSSARY.md (the *Crop* entry) were updated.
- No rule broken.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 2 | 2026-10-06 | `CropEffect.Whole`, `EffectIcons.WholeImage`, the `_cropWhole` button in `MainForm` |
| Unit tests | 2 | 2026-10-06 | Not requested — no test project |
| README | 2 | 2026-10-06 | § Crop: the 100 % button |
| GLOSSARY | 2 | 2026-10-06 | *Crop* entry: the 100 % button |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the 100 % button go? (options toolbar *(recommended)* / toolbar + double-click / inside the kept part) | Not answered — go given without an answer: **options toolbar** applied | 2026-10-01 → 2026-10-06 |
| 2 | Under a kept ratio (e.g. 16:9), what does 100 % do? (switch to Free, whole image *(recommended)* / keep the ratio, largest centered on the image / keep the ratio, largest around the current kept part) | Not answered — **Free, whole image** applied | 2026-10-01 → 2026-10-06 |
| 3 | Label? (icon + "100 %" *(recommended)* / "Tout" / icon only) | Not answered — **icon + "100 %"** applied | 2026-10-01 → 2026-10-06 |
| 4 | Exploration depth? (simple / tricky) | Not answered — explored directly, the crop code being two files | 2026-10-01 → 2026-10-06 |

---

*Last updated: 2026-10-06*
