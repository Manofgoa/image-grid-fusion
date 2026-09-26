# Add Layouts

> Working document — new layouts for 2 to 4 images, offered in an "Advanced" group of the layout
> strip, hidden by default.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The catalog (`Composition/GridLayout.cs`) offers 1 layout for 1 image, 3 for 2, 4 for 3 and 5 for 4.
This work adds more layouts, from every family — regular grids, mosaics, bands, free forms — still
**within 4 cells** (`GridLayout.MaxImages` unchanged; more cells is out of scope).

The existing layouts stay as they are and remain the visible ones. Every new layout is **advanced**:
it lives in an **Advanced** group of the layout strip (`UI/LayoutStrip.cs`), collapsed by default and
revealed by a click. When the strip's content is taller than the strip, a **vertical scrollbar**
appears.

Components:

| Component | Change |
|---|---|
| `Composition/GridLayout.cs` | New catalog entries, flagged advanced |
| `UI/LayoutStrip.cs` | Advanced group header, expand / collapse, vertical scrolling |
| `README.md` | *Layouts* section: the advanced group and its layouts |
| `GLOSSARY.md` | New term: *Advanced layout* |

---

## Layout Model

What the engine already allows, and the new layouts must respect:

- A layout is a set of **rectangles on a grid of units** (`columns × rows`) tiling the canvas
  exactly. No L-shaped cell: "free forms" are made of rectangles.
- **Cell 0 is the featured cell** and takes image 1; the others follow in reading order.
- **One mirror axis** per layout (`MirrorAxis.None / Horizontal / Vertical`).
- The canvas ratio is 1200:628 (≈ 1.91). Cell ratios below are given for it, as in the README:
  below 1 suits portraits, around 1.9 landscapes, above 3 panoramas.

New flag on `GridLayout`: **`IsAdvanced`**. `For(count)` keeps returning every layout of the count,
the basic ones first (so `Default(count)` is unchanged); the strip splits them on the flag.

---

## Proposed Advanced Layouts

12 layouts: 2 for 2 images, 4 for 3 images, 6 for 4 images. None for 1 image (a single cell has no
other layout).

### 2 images

| Id | Name | Family | Units | Cells (x, y, w, h) — featured first | Mirror | Cell ratios |
|---|---|---|---|---|---|---|
| `2-split-rows` | Two thirds + one third, stacked | Bands | 1 × 3 | (0,0,1,2) (0,2,1,1) | Vertical | 2.87 · 5.73 |
| `2-quarter` | Three quarters + one quarter | Bands | 4 × 1 | (0,0,3,1) (3,0,1,1) | Horizontal | 1.43 · 0.48 |

```
Two thirds + one third, stacked   Three quarters + one quarter
+-------------------+             +--------------+----+
|                   |             |              |    |
|         1         |             |      1       | 2  |
+-------------------+             |              |    |
|         2         |             +--------------+----+
+-------------------+
```

### 3 images

| Id | Name | Family | Units | Cells (x, y, w, h) — featured first | Mirror | Cell ratios |
|---|---|---|---|---|---|---|
| `3-rows` | Three rows | Regular grid | 1 × 3 | (0,0,1,1) (0,1,1,1) (0,2,1,1) | None | 5.73 each |
| `3-big-centre` | Big centre | Mosaic | 4 × 1 | (1,0,2,1) (0,0,1,1) (3,0,1,1) | None | 0.95 · 0.48 ×2 |
| `3-big-top-uneven` | Big top, uneven | Bands | 3 × 2 | (0,0,3,1) (0,1,2,1) (2,1,1,1) | Horizontal | 3.82 · 2.55 · 1.27 |
| `3-corner` | Corner | Free form | 3 × 3 | (0,0,2,2) (2,0,1,3) (0,2,2,1) | Horizontal | 1.91 · 0.64 · 3.82 |

```
Three rows             Big centre             Big top, uneven        Corner
+-------------------+  +----+---------+----+  +-------------------+  +------------+------+
|         1         |  |    |         |    |  |         1         |  |            |      |
+-------------------+  | 2  |    1    | 3  |  +------------+------+  |     1      |      |
|         2         |  |    |         |    |  |     2      |  3   |  |            |  2   |
+-------------------+  +----+---------+----+  +------------+------+  +------------+      |
|         3         |                                                |     3      |      |
+-------------------+                                                +------------+------+
```

*Big centre* is symmetric: its featured cell is in the middle, so image 2 goes left and image 3 right.

### 4 images

| Id | Name | Family | Units | Cells (x, y, w, h) — featured first | Mirror | Cell ratios |
|---|---|---|---|---|---|---|
| `4-rows` | Four rows | Regular grid | 1 × 4 | (0,0,1,1) (0,1,1,1) (0,2,1,1) (0,3,1,1) | None | 7.64 each |
| `4-big-centre` | Big centre | Mosaic | 4 × 2 | (1,0,2,2) (0,0,1,2) (3,0,1,1) (3,1,1,1) | Horizontal | 0.95 · 0.48 · 0.95 ×2 |
| `4-tall-left-mixed` | Tall left, mixed | Mosaic | 3 × 2 | (0,0,1,2) (1,0,2,1) (1,1,1,1) (2,1,1,1) | Horizontal | 0.64 · 2.55 · 1.27 ×2 |
| `4-uneven-grid` | Uneven grid | Bands | 3 × 2 | (0,0,2,1) (2,0,1,1) (0,1,2,1) (2,1,1,1) | Horizontal | 2.55 · 1.27 · 2.55 · 1.27 |
| `4-bricks` | Bricks | Free form | 3 × 2 | (0,0,2,1) (2,0,1,1) (0,1,1,1) (1,1,2,1) | Horizontal | 2.55 · 1.27 ×2 · 2.55 |
| `4-corner` | Corner | Free form | 3 × 3 | (0,0,2,2) (2,0,1,2) (0,2,2,1) (2,2,1,1) | Horizontal | 1.91 · 0.95 · 3.82 · 1.91 |

```
Four rows              Big centre             Tall left, mixed
+-------------------+  +----+---------+----+  +------+-------------+
|         1         |  |    |         | 3  |  |      |      2      |
+-------------------+  | 2  |    1    +----+  |  1   +------+------+
|         2         |  |    |         | 4  |  |      |  3   |  4   |
+-------------------+  +----+---------+----+  +------+------+------+
|         3         |
+-------------------+
|         4         |
+-------------------+

Uneven grid            Bricks                 Corner
+------------+------+  +------------+------+  +------------+------+
|     1      |  2   |  |     1      |  2   |  |            |      |
+------------+------+  +------+-----+------+  |     1      |  2   |
|     3      |  4   |  |  3   |     4      |  |            |      |
+------------+------+  +------+------------+  +------------+------+
                                              |     3      |  4   |
                                              +------------+------+
```

---

## Interplay with Cell Resize

`workfiles/20260926-cell-resize.md` (designed, go pending) lets the user drag the separators
between cells. It does **not** block any proposed layout: each one is a plain unit tiling that the
current engine renders as is. But once it ships, several proposals become **the same topology as an
existing layout, at other proportions** — reachable by dragging, though without exact proportions
(its snapping only targets the layout's own positions and alignable separators).

| Proposal | With cell resize |
|---|---|
| Two thirds + one third, stacked | *Two rows*, separator dragged to 2/3 |
| Three quarters + one quarter | *Two columns* (or *Two thirds + one third*), separator dragged to 3/4 |
| Big top, uneven | *Big top* (3 images), bottom separator dragged to 2/3 |
| Uneven grid | *Grid*, both vertical arms dragged to 2/3 and realigned |
| Bricks | *Grid*, vertical line broken: top arm at 2/3, bottom arm at 1/3 |
| Corner (4 images) | *Grid*, cross moved to (2/3, 2/3) |
| Big centre (3 images) | *Three columns* resized — but image 1 then sits left, not centre (a swap fixes it) |
| Corner (3 images) | *Big left* mirrored and resized — but image 1 then takes the tall column (a swap fixes it) |
| Three rows, Four rows, Big centre (4 images), Tall left, mixed | **New topologies**, not reachable |

Consequences for the order of delivery:

**Cell resize is delivered first** (Q&A #11): this work only adds catalog entries and the strip's
Advanced group; the new layouts' separators come from cell resize's generic definition, and its
*Grid* cross (dynamic arms) must also apply to the other 2 × 2 topologies — *Uneven grid*, *Bricks*,
*Corner* (4 images) — if they are kept.

---

## Layout Strip

- **Basic thumbnails** first, as today (the existing layouts, in their current order).
- Then an **Advanced** header, a clickable row labelled `Advanced ▸` when collapsed, `Advanced ▾`
  when expanded. **Collapsed by default.**
- Expanded, the count's **advanced thumbnails** follow the header, drawn like the basic ones
  (hover, active highlight, tooltip with the layout name).
- The header is **hidden when the image count has no advanced layout** (1 image, no image).
- **Vertical scrollbar**: appears only when the content is taller than the strip; the mouse wheel
  scrolls it; the thumbnails narrow by the scrollbar's width while it is shown.
- Picking an advanced layout behaves like any layout: the mirror toggle turns off, the images keep
  their order, the effects are kept (RULES.md, *Scope and State*).
- The number of images changing still brings back the count's default layout, which is basic.

Pending (see Open Questions): where the mirror toggle goes, what collapsing does while an advanced
layout is active, and how long the expanded state lasts.

---

## Test Impact

The solution holds **no test project** (`ImageGridFusion.slnx` lists the app only), so no existing
test can be updated. Whether to create one is an Open Question; until it is settled:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Every layout tiles its units exactly, without overlap, with `Count` between 1 and 4 | *(pending)* | *(pending)* |
| `Default(count)` is still the first basic layout | *(pending)* | *(pending)* |
| Advanced layouts come after the basic ones in `For(count)` | *(pending)* | *(pending)* |
| `Mirrored()` of each asymmetric advanced layout still tiles exactly, and twice gives back the original | *(pending)* | *(pending)* |

---

## Open Questions

- [ ] Is the proposed list kept as is, or are some layouts dropped (e.g. *Three rows* at 5.73 and
      *Four rows* at 7.64 give very thin bands)?
- [ ] The proposals reachable through cell resize (see *Interplay with Cell Resize*): kept as exact
      presets, or dropped in favour of dragging?
- [x] ~~Order of delivery against cell resize: this work before or after it?~~ → After: the cell
      resize session notifies this one once implemented, then the go is asked here
- [ ] Where does the mirror toggle go: right after the basic thumbnails (fixed place, above the
      Advanced header), or at the very end of the strip (after the advanced thumbnails)?
- [ ] Collapsing the group while an advanced layout is active: the active thumbnail stays visible
      below the header, collapsing is refused, or it hides like the others?
- [ ] How long does the expanded state last: the whole session, or until the image count changes?
- [ ] Tests: create a test project for the catalog, or no unit test for this work?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Scoping answers: every family, all new layouts in an "Advanced" group hidden by default and
revealed on click, 4 cells at most, a scrollbar when the strip overflows, a straightforward subject.

Initial proposal: 12 advanced layouts built from rectangles on the existing unit grid (2 for
2 images, 4 for 3, 6 for 4), an `IsAdvanced` flag on `GridLayout`, an `Advanced ▸ / ▾` header in the
strip, hidden when the count has no advanced layout, and a vertical scrollbar shown on overflow.

### Iteration 2 — 2026-09-26

User remark: *Bricks* seems to need the cell resize task first. Checked against
`workfiles/20260926-cell-resize.md`: it renders with the current engine, so no dependency — but
*Bricks* is the *Grid* with its vertical line broken, and five other proposals are also existing
topologies at other proportions. Added *Interplay with Cell Resize*, and two questions: keep those
proposals as exact presets or drop them, and the order of delivery against cell resize.

### Iteration 3 — 2026-09-26

Order of delivery settled (Q&A #11): **after cell resize**. The cell resize session is asked to
notify this one once its implementation is delivered; the go for this workfile is proposed then.
Delivering second, this work only adds catalog entries and the strip's Advanced group; the new
layouts' separators must follow cell resize's generic definition, the 2 × 2 cross caveat included.

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
| 1 | Which layout families to propose? | All of them — grids, mosaics, bands, free forms — every new one in an "Advanced" group, hidden by default and shown on click | 2026-09-26 |
| 2 | Up to how many cells? | Out of scope: 4 cells at most for now | 2026-09-26 |
| 3 | What if the strip gets too long? | A scrollbar | 2026-09-26 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-09-26 |
| 5 | Keep the proposed list, or drop some? | | |
| 6 | Where does the mirror toggle go? | | |
| 7 | Collapsing while an advanced layout is active? | | |
| 8 | Lifetime of the expanded state? | | |
| 9 | Tests: create a test project, or none? | | |
| 10 | Proposals reachable through cell resize: exact presets, or dropped? | | |
| 11 | Order of delivery against cell resize? | After it: the cell resize session notifies this one once implemented, then the go is proposed here | 2026-09-26 |

---

*Last updated: 2026-09-26*
