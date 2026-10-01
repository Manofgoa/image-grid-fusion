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

The task makes that ratio a **choice**: the **output format**, picked among a few thumbnails. The
grid stretches to fill the chosen ratio and every image fits its cell by the existing fitting rule —
nothing else changes in how a cell is drawn.

Agreed at scoping (Q&A #1–#4):

- A format **is the aspect ratio of the final canvas**, nothing more: no target resolution, no
  platform options bundled with it.
- The formats: **Free**, **Twitter** (the existing 1200:628), **Square 1:1**, **Portrait 4:5**,
  **Story 9:16**, **Landscape 16:9**.
- It is set in a **Format tab** of the bottom toolbar, whose *Global effects* label becomes
  **Global**: the toolbar now holds a setting beside the effects.
- The exploration was a single scout pass (the subject is expected to be straightforward).

---

## Formats

| Format | Ratio (W:H) | Typical use |
|---|---|---|
| Free | see Open Questions | — |
| Twitter | 1200:628 (≈ 1.91:1) | Twitter / X in-feed image — **the default**, today's only ratio |
| Square | 1:1 | Instagram / Facebook feed, avatars |
| Portrait | 4:5 | Instagram / Facebook portrait feed |
| Story | 9:16 | Stories, Reels, TikTok, Shorts |
| Landscape | 16:9 | YouTube, LinkedIn video, screens |

- One enum, `OutputFormat`, in `Composition/`, its order the thumbnails' order; each value gives its
  ratio (`OutputFormat.Ratio`) and its name.

---

## The Ratio in the Code

The ratio is centralised but **constant**; its readers today:

| Reader | Where | Change |
|---|---|---|
| `GridLayout.RatioWidth` / `RatioHeight` / `HeightFor` | `Composition/GridLayout.cs:20-21, 104` | Become the **current format's** ratio, passed in rather than read from constants |
| Export canvas size | `Composition/CanvasSizer.cs:12-32` (`Compute`, width clamped to [1200, 4096], height from `HeightFor`; line 25 turns cell fractions into heights) | Takes the format's ratio — clamping, see Open Questions |
| Callers of `CanvasSizer.Compute` | `Compositor.Render` (`Compositor.cs:45`, PNG), `GridExport.RenderAnimation` (`GridExport.cs:138`, GIF / MP4, then `Animation.EvenSize`) | Pass the format |
| Preview canvas | `UI/GridPreview.cs:1290-1309` (`CanvasBounds`, the largest 1200:628 rectangle in the control) | The largest rectangle **of the format's ratio** |
| Text / page loaders | `Imaging/ImageLoader.cs:24, 33` (`Size(RatioWidth, RatioHeight)`), `GridPreview.cs:1381` | Read the format's ratio |
| Layout strip thumbnails | `UI/LayoutStrip.cs:205` (`HeightFor`) | See Open Questions |

The Borders' Twitter corners radius is 3 % of the grid's **longer side**, read from the canvas: it
follows any format by itself.

---

## UI — the Global Toolbar

### Rename

- The bottom tabs' label `"Global effects →"` (`MainForm.cs:184`) becomes **`"Global →"`**.
- In the docs, the *global effects toolbar* becomes the **global toolbar**, the *global options
  toolbar* stays as is; a *global effect* keeps its name (Soundtrack, Fade, Borders) — the Format is
  a **global setting**, not an effect.

### The Format Tab

- A new tab **Format**, in `GlobalEffect`'s tab row (the enum drives `EffectTabs<GlobalEffect>`),
  placed **first** — the format is chosen before the rest — with an icon from `EffectIcons`.
- It has **no activation checkbox**: a format is always in force. `EffectTabs` gains an opt-in for a
  checkbox-less tab — no glyph in `Tabs()`, none painted in `PaintTab`, `HitTest` never reports
  `OnCheck` for it. `ToggleGlobalEffect`'s `default:` branch must never receive it.
- Its options, in the global options toolbar: **one thumbnail per format**, the active one
  highlighted, its name in a tooltip (and / or under it — see Open Questions); then the tab's own
  **Reset**, bringing back **Twitter**.
- Precedent: the Crop effect's ratio buttons (`OptionButton` + `EffectIcons.Ratio`, a rectangle of
  the ratio centred in a square, Free as a dashed rectangle — `MainForm.cs:119-122, 470-473`,
  `EffectIcons.cs:133`).

### Locked While Exporting

Like every global tab: the export keeps the format it started with.

---

## Rules to Update

- **RULES.md § Global Effects / § Global Effects Toolbar**: the toolbar is the **Global toolbar**;
  it may hold a **global setting** tab — no activation checkbox, always in force, its options
  showing its value, its own Reset bringing back its default.
- **RULES.md** — a new short section **Output Format**: the ratio has one definition (the current
  format), read by the preview, the canvas sizing, the exports and the loaders; a new consumer
  reads it there, never the 1200:628 constants.
- **GLOSSARY.md**: *Output format*, *Global toolbar*, *Global setting*; *Global effects toolbar*
  replaced.
- **README.md**: the Global toolbar section and the Format tab.

---

## Test Impact

The repository holds **no test project** (declined since `20260923-application-v1.md`, Q&A #12,
and every workfile since): nothing is created or updated. The behaviour is checked by hand in the
launched app:

| Check | Expected |
|---|---|
| Pick each format | The preview canvas takes its ratio, the cells stretch, the images fit them |
| Copy / Save a PNG, an MP4, a GIF in Square and in Story | The file has the format's ratio (within the even-size rounding) |
| The Format tab | No checkbox; its thumbnails select the format; its Reset brings back Twitter |
| *Global* label | Reads `Global →` |
| Twitter corners on Story | The rounded corners follow the new canvas |

---

## Open Questions

- [ ] What is **Free**? Today there is no free ratio: every canvas is 1200:628.
- [ ] Is the chosen format **remembered between sessions** (an app setting) or, like the global
  effects, back to Twitter at each start?
- [ ] Do the toolbar's **Reset** (all) and **Clear all** bring the format back to Twitter?
- [ ] How do the **thumbnails** look (plain ratio shapes, the grid's layout drawn in the ratio, a
  live miniature of the grid) and are they **labelled**?
- [ ] Do the **layout strip's** thumbnails take the current format's ratio?
- [ ] **Export size**: the width is clamped to [1200, 4096] today; for Story it gives 2133 to 7282 px
  tall. Clamp the **longer side** instead?
- [ ] The **Twitter corners** (Borders): kept for every format, or only meaningful in Twitter?

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
Crop ratio buttons are the closest precedent for ratio thumbnails. Initial design above, seven
open questions.

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

---

*Last updated: 2026-10-01*
