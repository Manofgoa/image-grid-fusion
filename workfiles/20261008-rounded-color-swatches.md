# Rounded Color Swatches

> Working document — the colour swatches drawn in the Windows 11 style shared with emoji-selector:
> the shared rule states the style, then Image Grid Fusion applies it.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

emoji-selector's settings-menu workfile wrote a shared rule, `mini-apps/shared/RULES.md` § Colour
Items: a menu item picking a colour shows the colour in use as a swatch. It left the **shape to each
app** — *"image-grid-fusion draws a square, emoji-selector a rounded one"* — and states no style.

emoji-selector's rounded swatch is the one wanted, the Windows 11 look. Two steps:

1. **The shared rule** describes that style exactly and drops the per-app shape, so every mini-app
   draws the same swatch.
2. **Image Grid Fusion** applies it: its ⚙ menu items, and the Background's *Color…* button.

Components: `mini-apps/shared/RULES.md`; `src/ImageGridFusion/UI/MainForm.cs` (`SetSwatch`,
`ShowBackgroundColor`, `OnDpiChanged`, `_backgroundColor`); the README pair.

---

## The Shared Rule

`mini-apps/shared/RULES.md` § Colour Items **extends to buttons**: a menu item **or a button** that
picks a colour shows the colour in use as a swatch, beside its label (Q5). The line *"The shape is
the app's choice…"* is replaced by the style, taken **as emoji-selector draws it today** (`EmojiSelector/UI/MainForm.SetSwatch`), so
emoji-selector is already compliant:

- **Rounded square, Windows 11 style**: 16 logical px (`LogicalToDeviceUnits(16)`), its corners
  `LogicalToDeviceUnits(6)` given as the `Size` of `Graphics.FillRoundedRectangle` /
  `DrawRoundedRectangle`, drawn **antialiased** (`SmoothingMode.AntiAlias`) on a transparent bitmap.
- **Bounds** `(0, 0, size - 1, size - 1)`, the fill and the outline on the same rounded rectangle,
  so the 1 px outline stays inside the bitmap.
- **Opaque** fill: the colour in use, alpha forced to 255.
- The outline (`SystemColors.ControlDark`), the DPI and the refresh bullets stay as they are.

**Not versioned**: `mini-apps/shared/` belongs to no git repository — the rule file is edited in
place, with no commit. The report says so.

---

## Image Grid Fusion

### ⚙ Menu Items

*Border color…* and *Arrow color…* — `MainForm.SetSwatch` draws the rule's swatch instead of the
square. Their refresh is unchanged (after each pick, on a DPI change).

### The Background's Color… Button

Today its **whole face** is the colour (`ShowBackgroundColor`: `BackColor`, the text black or white
for contrast). It follows the rule like the menu items (Q4):

- The **native button** comes back (`UseVisualStyleBackColor`, default colours): its face no longer
  carries the colour.
- The rule's swatch is its **image, before the text** `Color…` (`TextImageRelation.ImageBeforeText`).
- Disabled (no cell selected, export running), WinForms draws the image greyed, like the other
  options.

- It shows the colour the Background draws (`BackgroundShown`, the automatic one included), as now.
- The README pair's Background section (*Color…* on the fill's first line) says it shows the colour
  in use as a swatch.
- Redrawn on a DPI change with the menu items (`OnDpiChanged`), the replaced bitmap disposed.

### One Drawing

A single swatch-drawing method in `MainForm` (the bitmap for a colour at the window's DPI), used by
the menu items and the button.

---

## Test Impact

The repository has **no unit-test project** (`ImageGridFusion.slnx` holds the app only), and the
change is drawing only: no test is created. Checked by hand in the launched app (menu items at
100 % and at a higher DPI, the button enabled and disabled, automatic colour on and off).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project; drawing only) | — | — |

---

## Open Questions

- [x] ~~How does the *Color…* button show its swatch? (a) the native button kept, the rule's swatch
  as its image before the text `Color…` — like the menu items; (b) the button owner-drawn, its
  whole face a rounded rectangle of the colour with the text over it.~~ → (a) the native button,
  the swatch before the text
- [x] ~~Does the shared rule's Colour Items extend to **buttons** picking a colour (any app), or does
  it stay about menu items, the *Color…* button following it as an Image Grid Fusion choice?~~ →
  It extends to buttons, in every mini-app

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Request: check that the shared colour-item rule carries the Windows 11 style emoji-selector
introduced, add it if not, then apply it here. Finding: the rule states no style and leaves the
shape to each app. Scoping answers: the rule takes emoji-selector's exact geometry; in Image Grid
Fusion the ⚙ menu items **and** the Background's *Color…* button change; a straightforward subject
(one scout pass). Two questions left open: the button's rendering, the rule's scope.

### Iteration 2 — 2026-10-08

Open questions answered: the *Color…* button keeps its native face, the swatch as its image before
the text; the shared rule covers buttons picking a colour too. Added: the README pair's *Color…*
mention says it shows the colour as a swatch. No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Shared rule | | | `mini-apps/shared/RULES.md`, not versioned |
| Code | | | |
| Unit tests | 1 | 2026-10-08 | Not applicable — no test project, drawing only |
| README | | | `README.md` / `README.fr.md` § Background: *Color…* shows the colour in use as a swatch |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which Windows 11 geometry does the shared rule set: emoji-selector's, or Windows 11's 4 px control radius (emoji-selector realigned)? | emoji-selector's | 2026-10-08 |
| 2 | In Image Grid Fusion, what turns rounded: the two ⚙ menu items, or the Background's *Color…* button too? | The *Color…* button too | 2026-10-08 |
| 3 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-08 |
| 4 | How does the *Color…* button show its swatch: native button with the swatch as its image, or owner-drawn rounded face? | Native button, swatch before the text | 2026-10-08 |
| 5 | Does the shared rule extend to buttons picking a colour, or stay about menu items? | Yes, buttons included | 2026-10-08 |

---

*Last updated: 2026-10-08*
