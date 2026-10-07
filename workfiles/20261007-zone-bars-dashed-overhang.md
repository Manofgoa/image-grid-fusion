# Zone Bars Dashed Overhang

> Working document — the part of a resizable zone's bars running beyond the zone drawn dashed, like
> the center / edge guides, so the zone itself reads at a glance.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A **resizable zone** (RULES.md § On-Cell Handles › Resizable Zones) is framed by four green bars —
the crop's kept part in the crop edit view, the blur's sharp rectangle in the cell. Each bar runs
across the **whole bounds** (`Bars.Span`: the cell for the blur, the whole image for the crop), solid
from end to end, so the zone's rectangle does not stand out from the lines crossing it.

This draws each bar in two parts:

- its **side** — the stretch between the zone's two corners, on the zone's edge — **solid**, as
  today;
- its **overhang** — from each of the zone's corners outward to the edge of the bounds — **dashed**,
  in the pattern of the magnetic stops' guides.

Every resizable zone gets it, a future one included: it becomes a rule of § Resizable Zones.

Components: `UI/GridPreview.cs` only — `PaintBars` (the bars, used by the handles and by the restore
flash), alongside `PaintGuideLines` (the guides' pens it matches). `RULES.md`, `GLOSSARY.md`,
`README.md` and their French versions for the documentation.

---

## Drawing

### Today

`GridPreview.PaintBars(g, cell, bars, opacity, grips)` draws, clipped to the cell, four full-length
lines from `bars.Span`'s edge to edge, each twice: the black halo (`HelperHalo`, 4 logical px), then
the green line (`HelperColor`, 2 logical px), both solid. Then, with `grips`, the four grips at the
middle of the sides and the four L-brackets at the zone's corners (`PaintCorners`).

The guides (`PaintGuideLines`) use the same halo, solid, under a green pen of the same width with
`DashPattern = [4, 3]` (in pen widths: 8 px dash, 6 px gap at 100 %).

### Planned

| Part of a bar | From → to | Halo | Green line |
|---|---|---|---|
| Side | One zone corner → the other | Solid, as today | Solid, as today |
| Overhang (up to two per bar) | The zone corner → the bounds' edge | Solid, continuous with the side's | Dashed, `[4, 3]` — the guides' pattern |

- **Dash phase**: each overhang is drawn **from the zone's corner outward**, so a dash always starts
  right at the corner — the side runs straight into a dash, never into a gap, whatever the overhang's
  length.
- **No overhang** when the zone lies on the bounds' edge (a bar snapped onto it, § On-Cell Handles):
  that segment has zero length and nothing is drawn.
- **Opacity**: the overhang takes the bars' `opacity`, so the restore flash fades it with the rest.
- **Clip**: unchanged — the whole drawing stays clipped to the cell (the crop's span, the whole
  image, can run past it).
- **Grips and corners**: unchanged, on the zone's sides and corners.
- **One pen definition**: the dashed green pen is the guides' one — same width, same pattern, same
  colour — not a second look-alike.

### Where It Applies

| Drawn | Overhang dashed |
|---|---|
| The blur bars, as handles (Blur tab selected, blur on) | ✅ |
| The crop bars in the crop edit view (Crop tab selected, crop on) | ✅ |
| The restore flash after Ctrl+Z / Ctrl+Y (`PaintRestored`) — blur bars and crop edges, without grips | ✅ |

### Hit Area

**Unchanged.** `BarAt` measures the distance to the bar's line across the whole cell, so a press on
the dashed overhang grabs the bar as today: the change is visual only.

### Hover and Drag

**Unchanged.** While a bar is hovered or dragged, only its **grip** turns white (`PaintBarGrip`),
and a hovered or dragged **corner** only its L-bracket (`PaintCorners`); the bar's line — its solid
side and its dashed overhangs alike — stays green. The overhang follows its bar's line: green, as
the side.

---

## Rule and Documentation

- **RULES.md § On-Cell Handles › Resizable Zones** — a new bullet: the zone's bars are **solid along
  its sides, dashed beyond them** up to its bounds, in the guides' pattern, in the restore flash too;
  the dashed part stays grabbable. A new zone gets it by drawing its bars through `PaintBars`.
- **GLOSSARY.md** (+ `GLOSSARY.fr.md`) — the *Resizable zone* row says its bars are solid along the
  zone and dashed beyond it.
- **README.md** (+ `README.fr.md`) — the Crop section's bars bullet says it once; the Blur section's
  bars bullet points to it ("drawn as the crop's, see Crop"), as it already does for the wheel.

---

## Test Impact

**No unit test.** The repository has no test project, and the change is pure GDI+ painting in
`GridPreview` with no geometry worth extracting: the segment ends are the zone's and the span's edges,
already computed. Checked by hand in the running app (crop edit view, blur bars, restore flash).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; painting only) | — | — |

---

## Open Questions

- [x] ~~What does the part of a bar beyond the zone look like?~~ → Dashed up to the bounds' edge, the
  guides' pattern; the zone's sides stay solid (Q1).
- [x] ~~Does it apply to the restore flash?~~ → Yes (Q2).
- [x] ~~Does the dashed part stay grabbable?~~ → Yes, the change is visual only (Q2).
- [x] ~~A general rule of the resizable zones, or crop and blur only?~~ → A rule, in RULES.md
  § Resizable Zones (Q3).
- [x] ~~**Hover / drag white**: the bar's line never turns white today, only its grip and the
  corners' brackets — line unchanged, or the whole bar white?~~ → Line unchanged: solid and dashed,
  it stays green; only the grip and the brackets turn white, as today (Q5).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Scoping batch answered (Q1–Q4): overhang dashed up to the bounds' edge in the guides' pattern, the
sides solid; also in the restore flash; still grabbable; written as a rule of § Resizable Zones;
subject expected straightforward, explored in a single pass.

Exploration: the bars are drawn by `GridPreview.PaintBars` for the handles and for the restore flash
alike, full-length over `Bars.Span`, solid; the guides' dashed pen is `PaintGuideLines`'
(`[4, 3]` over a solid halo); `BarAt` already hit-tests the whole line. One point left open: the
bar's line does not turn white on hover today, only its grip.

### Iteration 2 — 2026-10-07

Hover / drag settled (Q5): the bar's line stays green while hovered or dragged, its dashed overhang
included — only the grip and the corners' brackets turn white, as today. Nothing left open.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go for code, tests and documentation, in a dedicated worktree (`feature/zone-bars-dashed-overhang`
under `.claude/worktrees/`), fast-forwarded into `main` and removed at the end.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project, painting only |
| README | | | |
| RULES.md / GLOSSARY | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does the part of a bar beyond the zone look like — dashed up to the bounds' edge, short dashed stubs, or removed? | Dashed up to the bounds' edge | 2026-10-07 |
| 2 | Where else does the dashed overhang apply — hover / drag white, the restore flash, still grabbable? | All three | 2026-10-07 |
| 3 | Written as a general rule of the resizable zones in RULES.md, or crop and blur only? | A rule in RULES.md | 2026-10-07 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-10-07 |
| 5 | Hover / drag white: line unchanged (only grip and brackets whiten), or the whole bar white, side and overhangs? | Line unchanged — stays green, only grip and brackets whiten | 2026-10-07 |

---

*Last updated: 2026-10-07*
