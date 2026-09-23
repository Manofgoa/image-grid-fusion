# Fix README Grid Layout Sample

> Working document — fix the ASCII layout diagrams of the README whose width overflows the common block width.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The `## Layouts` section of `README.md` draws every layout as an ASCII block, laid side by side
on a fixed column grid. Two blocks with three cells in a row are one character too wide, which
breaks the drawings and shifts the blocks that follow:

- **4 images — Big top**: the top row is 21 characters wide, the bottom row (three cells) 22.
  The featured cell's right border stops one character short of the row below it.
- **3 images — Three columns**: the whole block is 22 characters wide, so the *Featured* and
  *Big top* drawings start one column to the right of their headers and ratio captions.

Documentation only: no code, no test.

---

## Diagram Grid

Every diagram block follows one grid, which the fix restores:

| Element | Rule |
|---|---|
| Block width | **21 characters**: 19 interior characters + 2 outer borders |
| Gap between blocks | 2 spaces, so block *k* starts at column `23k` (0-based) |
| Header line (layout names) | Each name padded to 23 characters — already compliant |
| Caption line (cell ratios) | Each caption padded to 23 characters — already compliant |

A row of three cells has 17 interior characters (21 − 4 borders), which cannot split evenly:
the cells are **6 / 5 / 6**, the narrower one in the middle, so the drawing stays symmetric.
The label sits in the cell's centre, rounded to the left as in the other blocks.

### 3 images — Three columns

```
+------+-----+------+
|      |     |      |
|  1   |  2  |  3   |
|      |     |      |
+------+-----+------+
```

### 4 images — Big top

```
+-------------------+
|         1         |
+------+-----+------+
|  2   |  3  |  4   |
+------+-----+------+
```

The ASCII widths are illustrative: the ratio captions (`0.64 each`, `3.82 · 1.27 ×3`) describe
the real, equal cells and are unchanged.

### Other blocks

Every other block (1 image, 2 images, the other 3- and 4-image layouts, *Big left, mirrored*)
was measured at 21 characters with aligned headers and captions: left untouched.

---

## Test Impact

Nothing testable changes: the work edits `README.md` only. No test is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (documentation only) | — | — |

---

## Open Questions

- [x] ~~Widen three-cell rows to 22 with equal cells, or bring them back to 21 with 6/5/6 cells?~~ → Back to 21, cells 6/5/6
- [x] ~~Fix only the reported *Big top* (4 images), or every diagram?~~ → Every diagram: *Three columns* (3 images) too, and every block checked
- [x] ~~Straightforward or tricky subject?~~ → Straightforward: a single measuring pass

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Measured every diagram line of `README.md`: only the two three-cells-in-a-row blocks exceed
21 characters (*Three columns* for 3 images, the bottom row of *Big top* for 4 images).
Both are brought back to 21 with 6/5/6 cells; headers and captions already follow the
23-column grid and need no change.

### Iteration 2 — 2026-09-24 — ✅ Implemented

Go given for code, unit tests and documentation. Code and unit tests do not apply; the run
edits `README.md` only, on `main`.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | — | — | Not applicable: documentation only |
| Unit tests | — | — | Not applicable: nothing testable changes |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Three-cell rows: widen to 22 with equal cells, or back to 21 with 6/5/6 cells? | Back to 21 (6/5/6) | 2026-09-24 |
| 2 | Scope: every diagram, or the reported *Big top* only? | Every diagram | 2026-09-24 |
| 3 | Depth: straightforward or tricky / long? | Straightforward | 2026-09-24 |

---

*Last updated: 2026-09-24*
