# OCR Result Style

> Working document — a ⚙ menu "OCR result style" setting how the light bulb's arrow to the found
> word is drawn: curved or straight, its thickness, its color.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A tile found by its content shows a light bulb; on an image or a PDF page read by the OCR, an
**arrow** goes from the bulb to the word found (`ThumbnailGrid.PaintArrow`, designed in
`workfiles/20260926-ocr-search.md` § Arrow to the Word). Today it is fixed: straight, 2 px wide
(logical, scaled with the DPI), its head 7 px, amber (214, 150, 0 — `ThumbnailGrid.BulbColor`)
over a dark translucent halo 1 px wider on each side.

This work makes three of its traits **app settings** of the ⚙ menu, remembered between sessions
(RULES.md § App Settings): a slight **curve**, the **thickness**, the **color**.

Components: `UI/ThumbnailGrid.cs` (drawing), `UI/FileExplorerPanel.cs` (passes the settings to its
grid), `UI/MainForm.cs` (the ⚙ menu), `UI/AppSettings.cs` (persistence).

---

## Menu

In the ⚙ menu's **File contents** section, after **Rebuild content index**, a submenu:

```
File contents
  Search file contents (OCR)
  Rebuild content index
  OCR result style ▸
      Curved arrow            ✓ (checkable, on by default)
      Arrow thickness ▸
          0 (none)
          1 px
          2 px
          3 px
          4 px
          5 px
      Arrow color… [swatch]
```

- **Arrow thickness**: radio-like items, the current one checked, like *File explorer pages per
  load*.
- **Arrow color…**: the standard color dialog, a swatch of the current color on the item, like
  *Border color…*.
- Each choice applies **at once** to the tiles shown, and is saved in `settings.json`; a save that
  fails says so in the status line, a value that cannot be read falls back to its default.
- Every item has a tooltip ending with "remembered between sessions", like its neighbours.

---

## Drawing

### Thickness

- The thickness is the width of the **colored stroke only**, in logical px scaled with
  `LogicalToDeviceUnits`. The **dark halo is not counted**: it stays 1 px wider on each side of
  the stroke, whatever the thickness.
- **0 (none)**: no arrow at all — neither stroke, nor head, nor halo. The bulb stays.
- Default: *open question*.

### Curve

- **Curved arrow** on (the default): the arrow follows a **slight arc** from the bulb to the word
  instead of a straight line; its head follows the arc's direction at its end. Off: today's
  straight arrow.
- Its start stays on the bulb's edge and its tip on the word's top-center (or bottom-center), as
  today.
- How much and which way it bends: *open question*.

### Color

- The color of the stroke and the head; the halo stays dark. Default: today's amber
  (214, 150, 0).
- The bulb glyph itself is unchanged (a color emoji).

---

## Documentation

- `README.md` / `README.fr.md`: the ⚙ menu's **OCR result style** next to *Search file contents
  (OCR)* in the file explorer's section.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: no term changes planned.

---

## Test Impact

The repository has **no test project** (`ImageGridFusion.slnx` holds the app only): nothing to
create or update. The behaviour is checked by hand on the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [ ] Default thickness: today's 2 px, or another?
- [ ] The head's size: fixed at 7 px, or growing with the thickness?
- [ ] The curve: how much and which way does it bend?
- [ ] The color: a way back to the default amber in the menu, or the color dialog only?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial request: a ⚙ menu "OCR result style" with a curved-arrow option (on by default) and a
thickness submenu, 0 (none) to 5 px by 1. Scoping answers (Q&A #1–#4): the menu goes in ⚙ and is
remembered; 0 means no arrow at all; the subject is straightforward (a single direct exploration —
one chained question, the arrow's drawing and the ⚙ menu's pattern). The user then asked what the
current thickness is (2 px), said the black halo is **not** part of the chosen thickness, and added
an **arrow color** option.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the "OCR result style" menu go? | The ⚙ menu, remembered in `settings.json` | 2026-10-07 |
| 2 | Default thickness? | Asked back what it is today → 2 px; the black halo is not part of the thickness | 2026-10-07 |
| 3 | Thickness 0 (none): what becomes of the arrow? | No arrow at all | 2026-10-07 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-07 |
| 5 | (User, unprompted) | Add an option to choose the arrow's color | 2026-10-07 |
| 6 | Default thickness, now known to be 2 px? | | |
| 7 | The head's size with the thickness? | | |
| 8 | The curve's bend? | | |
| 9 | A way back to the default color? | | |

---

*Last updated: 2026-10-07*
