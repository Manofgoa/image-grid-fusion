# Background Effect

> Working document — a new effect, **Background**, turning the fill painted behind a cell's
> image into a per-cell setting: automatic or chosen color, opacity, or no fill at all.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every cell is filled, before its image is drawn, with an **automatic band color** computed
from the image (`BandColor.For`, painted by `Compositor.DrawCell`). This fill is always there and
always opaque.

The **Background** effect makes that fill an effect tab of the effects toolbar, with options:

- **On by default** — it is the default state of every new image placed in a cell, so the
  rendering of a fresh grid is unchanged.
- **Automatic color** checkbox, checked by default — the current band-color rules.
- **Opacity** slider, 0–100 %, default 100 %.
- **Color** button, painted with the background color in use; a click opens the standard color
  dialog.
- **Off** — nothing is painted behind the image: the cell is **transparent**, shown in the
  preview by a grey / white **checkerboard**, as in drawing apps.

It follows every effect rule of [RULES.md](../RULES.md) (state on the image's immutable look,
not persisted, tabs and activation checkbox, settings kept while off, acting on an option turns
the effect on, its own Reset button, rendering in `Compositor.DrawCell`), with one exception:
its default state is **on** (see *Default State and Resets*).

---

## Current State (explored)

| Topic | Where | What it does today |
|---|---|---|
| Automatic color | `Composition/BandColor.cs:87-146` | Uniform edge bands (≥3 agreeing sides, ΔE76 ≤ 10) → their average; else most frequent color on the edge bands; else the image's dominant color (`DominantColor.cs`) |
| Scope and cache | `Composition/SourceImage.cs`, `BandColor.cs:36,90-97` | One `BandColor` per source image; recomputed when the shown part (crop / zoom / move) changes, last answer cached |
| Painting | `Composition/Compositor.cs:90-95` | Fills the **whole cell** with that color before drawing the image — bands and transparent pixels show it; the black-and-white intensity desaturates it (`Gray(bands, gray)`) |
| Animations | `BandColor.cs:9-14`, `Imaging/GridExport.cs` | Color taken from the still frame and kept while playing |
| Effect state | `Composition/ImageLook.cs:6-15,36-199,272` | `ImageEffect` enum (order = tab order): Zoom, Rotate, Flip, Frames, BlackAndWhite, Blur, Volume. `TurnOn` / `TurnOff` (settings kept in `Kept…` fields) / `Reset` / `Activate` / `Deactivate` switches. `ImageLook.None` = every effect at its default; `WithoutEffects()` keeps the Volume (its own exception) |
| New image | `SourceImage.cs:31`, `Compositor.cs:10,73`, `UI/GridPreview.cs:747`, `UI/MainForm.cs:1162` | Starts from `ImageLook.None`; the resets go back to `None` or to `Reset(effect)` |
| Toolbars | `UI/MainForm.cs`, `UI/EffectTabs` | Effect tabs with activation checkbox; options row above, ended by the effect's own Reset button (`_effectResetButton`); toolbar Reset button (`_resetButton`) |
| Icons | `UI/EffectIcons.cs` | One vector-drawn method per icon |
| Transparency | `Compositor.cs:43`, `Imaging/GridExport.cs:119`, `GifEncoder.cs:87`, `VideoEncoder.cs:76` | **None**: `Compositor.Render` is 24 bpp, the export bitmap 32 bpp RGB (no alpha); the preview cache (`UI/GridPreview.cs:328`) is ARGB but always painted opaque; no checkerboard anywhere |
| Copy | `UI/MainForm.cs:740-742` | The clipboard gets the image both as a bitmap (`SetImage`) and as a `PNG` stream |
| Grid background | `GridLayout.cs` | Cells edge to edge, no gap, no global background color |

---

## Effect State

- New `ImageEffect.Background` case — **first** of the enum, so its tab comes first — and a
  `BackgroundEffect` immutable record on `ImageLook`, next to `BlurEffect`:

  | Field | Default | Meaning |
  |---|---|---|
  | `Automatic` | `true` | Color from the band-color rules |
  | `Color` | — (unset) | The chosen color, used when `Automatic` is off |
  | `Opacity` | `1.0` (100 %) | Alpha of the fill, 0–1 |

- `ImageLook.Background` is `null` while the effect is **off**; `KeptBackground` holds its
  settings meanwhile, like every other effect (`TurnOn` / `TurnOff`).
- A chosen color is **not remembered** across the automatic mode: re-checking *Automatic color*
  drops it, and unchecking it **freezes the current automatic color** (the one computed for the
  shown part at that moment) as the chosen color.
- Nothing is persisted (effects rule).

## Default State and Resets

- The Background's **default state is on**, with its default settings (automatic, 100 %) —
  the one effect whose default is on, besides the Volume's own rule.
- `ImageLook.None` (every effect at its default) therefore carries the Background **on**, so
  every path that starts from it — a new image, a replaced image, the toolbar's Reset — gets it
  without a change of its own.
- The resets follow RULES.md as it stands, which already brings every effect back to its default
  state, on / off included:

  | Event | Background of the cells concerned |
  |---|---|
  | The cell's image is replaced (drop, Ctrl+V, browse) | Default state — on, automatic, 100 % |
  | An image is deleted | Default state for the images that shift into another cell |
  | Two cells are swapped | Kept — it follows the image |
  | The layout changes | Kept |
  | The Background's own *Reset* button | Default state — on, automatic, 100 % |
  | The effects toolbar's *Reset* button | Default state, with every other effect |

- `ImageLook.Reset(ImageEffect.Background)` returns the effect **on** with its defaults,
  unlike every other effect's `Reset`, which turns it off.

### RULES.md — The Background Exception

RULES.md draws an effect that is off *as its defaults*; the Background's defaults are an opaque
automatic fill, while off must be transparent. A **Background exception** is added to RULES.md,
next to *The Volume Exception*:

- **Off draws no fill**: the cell is transparent behind its image, its settings kept.
- Its **default state is on** (automatic, 100 %): a new image, a replaced one, an image shifting
  after a deletion, and every *Reset* — its own and the toolbar's — bring it back on.

The GLOSSARY's *Effect* entry lists the Background with the other effects.

## Effects Toolbar and Options Toolbar

- One more **effect tab**, first, before Zoom — the background is the lowest layer of the cell —
  with its activation checkbox and its own icon in `EffectIcons`. It applies to every image, so
  its checkbox is never disabled.
- Options of the Background, in this order, then the effect's own **Reset** button ending the
  row:
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
- While the Background is off, its options show its **kept settings**, and acting on any of them
  **turns it on** first (RULES.md) — a color picked, the opacity moved or the checkbox clicked.
- As implemented (Iteration 6): the button reads **Color…**, its face shows the color **opaque**
  (a WinForms control takes no translucent back color), its text black or white by luminance; the
  face is refreshed only while the Background tab is selected. The automatic color it shows and
  that unchecking freezes is computed for the selected cell as the preview draws it
  (`GridPreview.SelectedAutomaticBackground` → `Compositor.AutomaticBackground`). One
  `ColorDialog` serves the whole session, so its custom colors stay. The opacity has its own icon
  (`EffectIcons.Opacity`), the label reads `Opacity: n %`; the checkbox and the color button have
  tooltips.

## Rendering

- In `Compositor.DrawCell`, the existing fill becomes:

  | Background state | Fill behind the image |
  |---|---|
  | On, automatic | Band color (current rules), alpha = opacity |
  | On, chosen color | Chosen color, alpha = opacity |
  | Off | Nothing — the cell stays transparent |

- The fill still covers the **whole cell**, so it also shows through the transparent pixels of
  the image itself (PNG with alpha), as today.
- The **black-and-white** effect desaturates the fill **whatever its color** — automatic or
  chosen — at its intensity.

### Preview — Checkerboard

- Wherever the result is not fully opaque (background off, opacity < 100 %, transparent image
  pixels), a grey / white **checkerboard** is visible underneath.
- Squares of **8 logical px**, scaled with `LogicalToDeviceUnits` — a screen-space aid, not
  part of the image.
- **Preview only**: drawn by the preview under the composed grid, never by the `Compositor`,
  so it never reaches an export.
- As implemented: a texture brush (white and grey 204) anchored on the canvas, painted under the
  whole grid at every paint; a cell drawn again into the preview cache is **cleared to
  transparent first**, else its previous frame would show through where it has no background.

### Exports

| Output | Where the cell is transparent |
|---|---|
| PNG (Save) | **Transparency kept** — the rendering bitmap carries alpha (32 bpp ARGB) |
| MP4 video, GIF, and every other output without alpha | Flattened on **white** |
| Copy (clipboard), `PNG` stream | **Transparency kept** |
| Copy (clipboard), bitmap flavour (`SetImage`) | Flattened on **white** |

- `Compositor.Render` (every still: Save PNG, Copy) is now 32 bpp **ARGB**; the MP4 / GIF frame
  loop clears its canvas to **white** before each frame; the copied bitmap is
  `Compositor.Flattened` (24 bpp, on white), the `PNG` stream is saved from the ARGB render.

---

## Test Impact

**Nothing to test** — the user chose to stay test-free (Q&A #11): the solution has no test
project, as in every previous workfile. The default state, the resets and the frozen automatic
color stay untested by decision, not because nothing testable changes.

Manual verification: drop an image (Background on, automatic); change the opacity and check the
checkerboard shows through; pick a color (checkbox unchecks), re-check and uncheck (the current
automatic color is frozen); turn the effect off (checkerboard only, settings kept), act on an
option (turns it back on); apply black and white on a chosen color; replace, delete and swap
cells, the Background's Reset and the toolbar's Reset (back on, automatic, 100 %); export PNG
(alpha kept) and MP4 / GIF (white); copy; same on a playing video.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 — not only the
blocking ones.

- [x] ~~**Resets vs. "active by default"** — what do an image replacement, a deletion and the
  cell's *Reset* do, now that an effect is active by default?~~ → Back to every effect's
  defaults; RULES.md amended (Q&A #5) *(revised 2026-09-26, see Iteration 3: RULES.md already
  says so, no amendment needed for the resets)*
- [x] ~~**Exports and transparency** — which formats keep it, what do the others show?~~ → PNG
  keeps the alpha; JPEG, video, GIF and other formats without alpha flatten on white (Q&A #6)
- [x] ~~**Checkerboard** — where, and what size?~~ → Preview only, 8 logical px squares scaled
  with `LogicalToDeviceUnits` (Q&A #7)
- [x] ~~**Black-and-white effect** — does it desaturate a chosen color too?~~ → Yes, always
  (Q&A #8)
- [x] ~~**Re-checking *Automatic color*** — is the chosen color remembered?~~ → No: unchecking
  freezes the current automatic color (Q&A #9)
- [x] ~~**Button position** in the effects row?~~ → First, before Zoom (Q&A #10)
- [x] ~~**Unit tests** — stay test-free?~~ → Yes, manual verification (Q&A #11)
- [x] ~~**Off ≠ drawn as its defaults** — RULES.md draws an effect that is off as its defaults,
  yet the Background off must be transparent~~ → A Background exception in RULES.md: off draws
  no fill, the default state is on (Q&A #13)
- [x] ~~**Copy (clipboard)** — does the copied image keep the transparency?~~ → The `PNG` stream
  keeps the alpha, the bitmap flavour is flattened on white (Q&A #14)
- [ ] **Blur over a transparent background** *(found during the run, not implemented)* — the blur
  draws its blurred copy of the cell **over** the sharp image; where the cell has no background,
  that copy is partly transparent near the image's edges, so the sharp image shows through the
  blurred bands there. Fix: replace the bands instead of drawing over them — which needs the
  blur to know what lies under the cell (transparent in the preview, white in MP4 / GIF).

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

### Iteration 2 — 2026-09-26

Every open question answered (Q&A #5–#11):
- Resets bring every effect back to its **defaults** (Background active, automatic, 100 %) —
  new *Resets and RULES.md Amendment* section.
- Exports: PNG keeps the transparency, the other formats flatten on white — *Exports*.
- Checkerboard: preview only, 8 logical px squares — *Preview — Checkerboard*.
- Black and white desaturates a chosen color too.
- The chosen color is not remembered: unchecking *Automatic color* freezes the current automatic
  color.
- The Background toggle comes first in the effects row.
- No unit tests; manual verification listed in *Test Impact*.

### Iteration 3 — 2026-09-26

Realigned on RULES.md and the code as they now stand (the user asked whether the design is
ready; several workfiles landed since Iteration 2 — effect tabs, UI clean-up, video mute, GIF
export):
- The effects toolbar is now **tabs with an activation checkbox**, the options toolbar sits
  **above** and is always visible, each effect has its **own Reset** button, and the cell's
  Reset tool is gone — *Effects Toolbar and Options Toolbar* rewritten.
- An effect turned off **keeps its settings** and acting on an option turns it on —
  `KeptBackground` added, behaviour stated.
- RULES.md already brings every effect back to its default state, on / off included: the
  planned amendment of the reset table is dropped; `Resets and RULES.md Amendment` becomes
  *Default State and Resets*, with `ImageLook.None` carrying the Background on and
  `Reset(Background)` returning it on.
- Exports: JPEG no longer exists; outputs are PNG, MP4, GIF, and the clipboard copy.
- *Current State* refreshed (line numbers, effect list, reset call sites).
- Two new open questions: RULES.md's *off is drawn as its defaults* against the transparent
  off state, and the clipboard copy.

### Iteration 4 — 2026-09-26

Both open questions answered (Q&A #13–#14):
- A **Background exception** is added to RULES.md, next to the Volume's: off draws no fill,
  the default state is on — new *RULES.md — The Background Exception* subsection; the GLOSSARY
  lists the Background among the effects.
- Copy: the clipboard's `PNG` stream keeps the transparency, its bitmap flavour is flattened on
  white — *Exports* table.

### Iteration 5 — 2026-09-26 — ✅ Implemented

Go given for code, tests and documentation (Q&A #16), after a go relayed by another session was
set aside until the user confirmed it here. Branch Gate: stays on `main`, the standing choice for
this repository. Scope frozen on the design sections as they stand.

### Iteration 6 — 2026-09-26 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:
- `ImageLook.Background` defaults to `BackgroundEffect.Default` in its initializer, so
  `ImageLook.None` — the default state every path starts from — has it on; `Reset(Background)`
  returns it on; `ImageEffect.Background` is the enum's first case (tab order, activation bit).
- The automatic color shown on the button and frozen by unchecking is computed on the preview's
  cell by a new `Compositor.AutomaticBackground`, the same computation as `DrawCell`; the button
  face is refreshed only while the Background tab is selected, as it changes at every zoom or move.
- The button reads **Color…**, its face opaque (WinForms refuses a translucent back color), its
  text black or white by luminance. One `ColorDialog` for the session keeps its custom colors.
  New icons `EffectIcons.Background` (tab) and `EffectIcons.Opacity` (slider); label
  `Opacity: n %`; tooltips on the checkbox and the color button.
- Preview: cells drawn again into the cache are cleared to transparent first, and the
  checkerboard is a texture brush painted under the whole canvas.
- Exports: `Compositor.Render` became ARGB for every still; the MP4 / GIF loop clears to white at
  each frame (it drew each frame over the previous one, which a transparent cell would reveal);
  `Compositor.Flattened` gives the copied bitmap.
- Found, not implemented (scope freeze): the blur over a transparent background lets the sharp
  image show through its bands near the image's edges — added to *Open Questions*.
- Verified with a throwaway harness in the scratchpad (not committed): off → alpha 0 in the PNG
  render and white once flattened; 50 % → alpha 127; a chosen color kept while off and back when
  turned on; `Reset` and `WithoutEffects` → on, automatic; black & white grays a chosen color.
- Commits: two of this run's changes were swept into another session's commit `eaa76f5`
  (*remember last folder*): `UI/EffectIcons.cs` whole and part of `UI/MainForm.cs`; the rest is in
  `e1ebfb7`. History left as is, `main` being shared; that session was asked to commit its own
  paths only. The same had happened to Iteration 3 of this workfile (`cc23c85`).
- RULES.md and GLOSSARY.md changed: the 21 running sessions of the workspace were messaged to
  re-read them; no session title needed correcting.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 6 | 2026-09-26 | `bfba764` state, `8ff3ea0` rendering, `6a10ec4` preview, `eaa76f5` (icons, swept by another session) + `e1ebfb7` Background tab and copy |
| Unit tests | 6 | 2026-09-26 | None, by decision (Q&A #11); scratch harness run, not committed |
| RULES.md / GLOSSARY.md | 6 | 2026-09-26 | `77775e7` — the Background exception, Background among the cell effects |
| README | 6 | 2026-09-26 | `5ac670a` — Background section, effects list, fitting rules, Copy |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When the Background is inactive, what does the cell area not covered by the image show? | Transparent, shown by grey / white squares as in drawing apps | 2026-09-25 |
| 2 | With *Automatic color* checked, how does the color button behave? | Enabled; choosing a color unchecks the automatic mode | 2026-09-25 |
| 3 | Opacity slider range and default? | 0–100 %, default 100 % | 2026-09-25 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — single scout pass | 2026-09-25 |
| 5 | Resets (image replaced, image deleted, cell *Reset*): back to the effects' defaults, RULES.md amended? | Back to defaults, RULES.md amended | 2026-09-26 |
| 6 | Exports: which formats keep the transparency, what do the others show? | PNG keeps the alpha, the others flatten on white | 2026-09-26 |
| 7 | Checkerboard: preview only? Square size? | Preview only, 8 logical px | 2026-09-26 |
| 8 | Does the black-and-white effect desaturate a chosen color too? | Yes, always | 2026-09-26 |
| 9 | Re-checking *Automatic color*: is the chosen color remembered? | No — unchecking freezes the current automatic color | 2026-09-26 |
| 10 | Position of the Background toggle in the effects row? | First, before Zoom | 2026-09-26 |
| 11 | Unit tests: stay test-free? | Yes | 2026-09-26 |
| 12 | Go for implementation? | No — the gate holds | 2026-09-26 |
| 13 | RULES.md draws an effect off as its defaults: Background exception, off = no fill? | Yes — Background exception in RULES.md | 2026-09-26 |
| 14 | Copy (clipboard): does the copied image keep the transparency? | `PNG` stream keeps the alpha, bitmap flavour on white | 2026-09-26 |
| 15 | Go for implementation? | No — the gate holds | 2026-09-26 |
| 16 | Go for implementation (confirming a go relayed by another session)? | Code, tests and documentation | 2026-09-26 |

---

*Last updated: 2026-09-26*
