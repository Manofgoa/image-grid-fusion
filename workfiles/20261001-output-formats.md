# Output Formats

> Working document — several aspect ratios for the final content (Twitter, Square, Portrait 4:5,
> Story 9:16, Landscape 16:9, Free), picked from thumbnails in a **Format** tab of the bottom
> toolbar, renamed **Global**.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the grid has **one output ratio**, hard-coded: Twitter / X's in-feed ratio **1200:628**
(`GridLayout.RatioWidth` / `RatioHeight`, `GridLayout.HeightFor`). Every canvas — the preview, the
PNG, the MP4 and the GIF — is drawn at it.

The task makes that ratio a **choice**: the **output format**, picked among thumbnails. The grid
stretches to fill the chosen ratio and every image fits its cell by the existing fitting rule —
nothing else changes in how a cell is drawn.

- A format **is the aspect ratio of the final canvas**, nothing more: no target resolution, no
  platform options bundled with it.
- It is set in a **Format tab** of the bottom toolbar, whose *Global effects* label becomes
  **Global**.
- It is **not persisted**: **Twitter** at every start, and back to Twitter with the Global
  toolbar's Reset, the Format tab's Reset and *Clear all*.

---

## Formats

| Format | Ratio (W:H) | Typical use |
|---|---|---|
| Free | The content's natural ratio, see § Free | Whatever loses the least of the images |
| Twitter | 1200:628 (≈ 1.91:1) | Twitter / X in-feed image — **the default**, today's only ratio |
| Square | 1:1 | Instagram / Facebook feed, avatars |
| Portrait | 4:5 | Instagram / Facebook portrait feed |
| Story | 9:16 | Stories, Reels, TikTok, Shorts |
| Landscape | 16:9 | YouTube, LinkedIn video, screens |

- One enum, `OutputFormat`, in `Composition/`, its order the thumbnails' order (Free first, then
  Twitter…); each fixed value gives its ratio and its name.
- One function gives **the current canvas ratio** — the fixed format's, or Free's computed one.
  Every reader listed in § The Ratio in the Code reads it there.

### Free

The canvas takes the ratio that **loses the least of the images**:

- **Searched** between **9:16 and 21:9**, with the current layout and its separators' sizes.
- **Lost area** of a cell = what the fitting rule cuts off (an image covering its cell, within the
  crop threshold) or leaves empty (the bands of an image contained in it), as a fraction of the
  cell; summed over the cells **weighted by their area**. The ratio with the smallest sum wins.
- **One image** in a one-cell layout: its own ratio, within the bounds — nothing lost.
- The image's size is the one every consumer reads — `ImageLook.Shown`: cropped by the Crop
  effect, then rotated (RULES § The Crop Exception).
- **Cells without a sized image** (empty, a text or page preview, which adapts to its cell) do not
  weigh in. With **nothing** that weighs in, Free is **Twitter's ratio**.
- **Recomputed** whenever the content changes — an image arriving, deleted, swapped, cropped,
  turned; the layout; the separators. While a **separator is dragged**, the ratio is held, and
  recomputed when it is released, so the canvas does not change shape under the mouse.

---

## The Ratio in the Code

The ratio is centralised but **constant**; its readers today, and what they become:

| Reader | Where | Change |
|---|---|---|
| `GridLayout.RatioWidth` / `RatioHeight` / `HeightFor` | `Composition/GridLayout.cs:20-21, 104` | The constants go; `HeightFor` takes the ratio |
| Export canvas size | `Composition/CanvasSizer.cs:12-32` (`Compute`; line 25 turns cell fractions into heights) | Takes the ratio; the **longer side** is clamped to **[1200, 4096]** (today the width), the other side from the ratio |
| Callers of `CanvasSizer.Compute` | `Compositor.Render` (`Compositor.cs:45`, PNG), `GridExport.RenderAnimation` (`GridExport.cs:138`, GIF / MP4, then `Animation.EvenSize`) | Pass the ratio the export started with |
| Preview canvas | `UI/GridPreview.cs:1290-1309` (`CanvasBounds`) | The largest rectangle **of the current ratio** in the control |
| Text / page loaders | `Imaging/ImageLoader.cs:24, 33`, `GridPreview.cs:1381` | Read the current ratio, following a format change the way they follow a layout change |
| Layout strip thumbnails | `UI/LayoutStrip.cs:205` (`HeightFor`) | Drawn in the current ratio — tall and narrow in Story, the strip scrolling as it already does |

Export sizes with the longer side clamped: Twitter, Landscape and a landscape Free unchanged
(width 1200 to 4096); Square 1200² to 4096²; Story 675×1200 to 2304×4096; Portrait 960×1200 to
3277×4096 (then even-sized for the videos).

### The Borders' Twitter Corners

- Offered in the **Twitter format only**. In every other format the option is **disabled**, its
  tooltip saying why, its setting **kept**, and nothing is rounded — preview and exports.
- Back in Twitter, it is drawn again as it was set. The ⚙ menu's default is untouched.

---

## UI — the Global Toolbar

### Rename

- The bottom tabs' label `"Global effects →"` (`MainForm.cs:184`) becomes **`"Global →"`**.
- In the docs, the *global effects toolbar* becomes the **global toolbar**; the *global options
  toolbar* keeps its name; a *global effect* keeps its name (Soundtrack, Fade, Borders) — the
  Format is a **global setting**, not an effect.

### The Format Tab

- A new tab **Format**, **first** in the row (the format is chosen before the rest), with an icon
  from `EffectIcons` — a ratio rectangle.
- **No activation checkbox**: a format is always in force. `EffectTabs` gains an opt-in for a
  checkbox-less tab — no glyph in `Tabs()`, none painted in `PaintTab`, `HitTest` never reports
  `OnCheck` for it. `ToggleGlobalEffect`'s `default:` branch never receives it.
- Its options, in the global options toolbar: **one thumbnail per format** (variant B), then the
  tab's own **Reset**, bringing back Twitter, enabled while the format is not Twitter.

### Format Thumbnails

- Each thumbnail draws the **current layout** — its cells as the grid stands, separators' sizes
  included — **schematically** (one flat block per cell, like the layout strip) **in the format's
  ratio**, all of them at one height; the format's **name below**.
- **Free** is drawn at its computed ratio, with a **dashed** outline.
- The **active** format is highlighted like the active layout (`LayoutStrip.PaintButton`: the
  highlight fill and inset border); a hovered one gets the hover wash.
- A small owner-drawn control (e.g. `FormatStrip`) laid out horizontally; the global options
  toolbar takes its height, being sized on its tallest options (RULES § Options Toolbar) — the
  bottom toolbar grows by that much for every tab.
- Redrawn when the layout, the separators or (for Free) the content change.

### Reset and Clear All

- The Global toolbar's **Reset** and **Clear all** bring the format back to **Twitter**, with the
  global effects; the Reset is enabled while the format is not Twitter either, and *Clear all*
  counts it in what it reports as removed.

### Locked While Exporting

Like every global tab: the export keeps the format — Free's ratio included — it started with.

---

## Rules to Update

- **RULES.md § Global Effects / § Global Effects Toolbar**: the toolbar is the **Global toolbar**;
  it may hold a **global setting** tab — no activation checkbox, always in force, its options
  showing its value, its own Reset bringing back its default, reset with the global effects.
- **RULES.md** — a new short section **Output Format**: the canvas ratio has **one definition**
  (the current format's, Free computed), read by the preview, the canvas sizing, the exports, the
  loaders and the layout strip; a new consumer reads it there, never a constant. The export's
  longer side is clamped to [1200, 4096].
- **RULES.md § The Crop Exception**: the Free ratio is one more consumer of `ImageLook.Shown`.
- **GLOSSARY.md**: *Output format*, *Free format*, *Global toolbar*, *Global setting*; *Global
  effects toolbar* replaced; *Twitter corners* — Twitter format only.
- **README.md**: the Global toolbar section and the Format tab.

---

## Test Impact

The repository holds **no test project** (declined since `20260923-application-v1.md`, Q&A #12,
and every workfile since): nothing is created or updated. The behaviour is checked by hand in the
launched app:

| Check | Expected |
|---|---|
| Pick each format | The preview canvas takes its ratio, the cells stretch, the images fit them |
| Copy / Save a PNG, an MP4, a GIF in Square and in Story | The file has the format's ratio, its longer side within [1200, 4096] |
| Free with one image | The canvas has the image's ratio |
| Free with several images | The canvas changes shape as images arrive; not while a separator is dragged |
| The Format tab | First, no checkbox; its thumbnails show the layout in each ratio and select the format; its Reset brings back Twitter |
| The layout strip | Its thumbnails take the current ratio |
| Global Reset, Clear all, restart | Back to Twitter |
| Twitter corners outside Twitter | Disabled with a tooltip, nothing rounded; back in Twitter, rounded again |
| *Global* label | Reads `Global →` |

---

## Open Questions

- [x] ~~What is **Free**?~~ → The content's natural ratio: the one, between 9:16 and 21:9, losing
  the least area (bands and crops) over the cells, weighted by their area (Q&A #6, #12)
- [x] ~~Is the chosen format **remembered between sessions**?~~ → No: Twitter at every start (Q&A #7)
- [x] ~~Do the toolbar's **Reset** (all) and **Clear all** bring the format back to Twitter?~~ →
  Yes, both (Q&A #8)
- [x] ~~How do the **thumbnails** look?~~ → Variant B: the current layout drawn schematically in
  each ratio, the name below (Q&A #5)
- [x] ~~Do the **layout strip's** thumbnails take the current format's ratio?~~ → Yes (Q&A #9)
- [x] ~~**Export size**: clamp the longer side instead?~~ → Yes, the longer side in [1200, 4096]
  (Q&A #10)
- [x] ~~The **Twitter corners**: every format, or only Twitter?~~ → Twitter only; disabled with a
  tooltip elsewhere, setting kept (Q&A #11)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-01

Scoping answered by the user (format = canvas ratio; six formats; a Format tab in the bottom
toolbar renamed Global; a single exploration pass). Scout findings: the ratio is the 1200:628
constant of `GridLayout`, read by `CanvasSizer`, `GridPreview.CanvasBounds`, `ImageLoader` and the
layout strip; the global tabs are an `EffectTabs<GlobalEffect>` that always draws a checkbox; the
Crop ratio buttons are the closest precedent for ratio thumbnails. Initial design, seven open
questions.

### Iteration 2 — 2026-10-01

Every open question answered (Q&A #5–#12): thumbnails of the layout in each ratio; Free = the
least-loss ratio; not persisted, reset by the Global Reset and Clear all; the layout strip follows
the ratio; the export's longer side clamped to [1200, 4096]; the Twitter corners in Twitter only.
Added from those answers, for the user to read before the go: Free ignores the empty cells and the
text / page previews, falls back to Twitter's ratio with nothing to weigh, and is held while a
separator is dragged; the Format tab comes first; the bottom toolbar grows to the thumbnails'
height.

### Iteration 3 — 2026-10-01 — ✅ Implemented

Go given for code, tests and documentation (no test project: nothing to create). Branch: `main`,
the repository's standing choice (the work lands on `main`).

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
| 1 | What is a "format"? | The aspect ratio of the final canvas | 2026-10-01 |
| 2 | Which formats? | Free, Twitter, Square 1:1, Portrait 4:5, Story 9:16, Landscape 16:9 | 2026-10-01 |
| 3 | Where is it set, and is "Global effects" renamed? | A tab of the bottom toolbar, renamed "Global" | 2026-10-01 |
| 4 | Simple or tricky / long? | Simple — a single exploration pass | 2026-10-01 |
| 5 | How do the Format thumbnails look (variants A–D drawn inline)? | B — the current layout drawn in each ratio, the name below | 2026-10-01 |
| 6 | What is Free? | The content's natural ratio | 2026-10-01 |
| 7 | Is the format remembered between sessions? | No — Twitter at every start | 2026-10-01 |
| 8 | Do Reset (all) and Clear all bring back Twitter? | Yes, both | 2026-10-01 |
| 9 | Do the layout strip's thumbnails take the current format's ratio? | Yes | 2026-10-01 |
| 10 | Export size: clamp the longer side instead of the width? | Yes — the longer side in [1200, 4096] | 2026-10-01 |
| 11 | Twitter corners: every format, or Twitter only? | Twitter only | 2026-10-01 |
| 12 | Free with several images: how is the natural ratio found? | The least loss: bands and crops summed over the cells, ratio between 9:16 and 21:9 | 2026-10-01 |

---

*Last updated: 2026-10-01*
