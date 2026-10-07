# Resizable Zone — Alt + Wheel Zooms

> Working document — while a resizable zone's bars show, holding Alt gives the wheel back to the
> image's zoom.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Since `workfiles/20261007-crop-wheel-resize.md`, the wheel over the selected cell showing the bars
of a **resizable zone** (the crop's kept part, the blur's sharp rectangle) scales that zone instead
of zooming the image (RULES.md § On-Cell Handles › Resizable Zones). Zooming that image then means
leaving the tab or turning the effect off.

With **Alt** held, the wheel over that cell **zooms the image again**, exactly as it does without
bars. Every resizable zone gets it, a future one included.

Components: `UI/GridPreview.cs` (`WndProc`, `OnMouseWheel`, `ZoomAt`), possibly `UI/MainForm.cs`
(the Alt key release, see § Alt Key Release), RULES.md, README (en / fr), GLOSSARY (en / fr).

---

## Behaviour

| Wheel over… | Without Alt | With Alt |
|---|---|---|
| The selected cell, a zone's bars shown | Scales the zone (Ctrl: around the cursor) | **Zooms the image** — the normal wheel zoom |
| Any other cell, or no bars shown | Zooms the image | Zooms the image — Alt changes nothing |

- **Identical to the wheel without bars**: same steps (5 %, coarser above 200 %), same anchor (the
  point under the cursor), and **Ctrl keeps its meaning there** — Alt + Ctrl + wheel zooms by the
  finer 1 % steps (`WheelSteps.Zoom`). Same zoom badge, same fit-mode exit, same activation of the
  Zoom effect: the Alt branch calls `ZoomAt` as the no-bars branch does.
- **The bars do not change** while Alt is held: no dimming, no other cursor. The zoom badge, shown
  by `ZoomAt` as usual, is the feedback.
- **Undo**: unchanged — a wheel burst is one step, Alt or not (§ Undo History).
- **Free format**: unchanged — the Alt zoom moves no crop bar, so the free ratio is not held.

### In the Crop Edit View

The crop edit view shows the whole image fitted whole, so the zoom does not show there:

- Alt + wheel **really changes the zoom** — seen as soon as the Crop tab is left or the crop's
  bars are hidden;
- in the edit view, **only the zoom badge** shows it, over the cell as usual (`PaintZoomBadge` is
  drawn whatever the view).
- **Anchor** — the cursor is over the edit view, not over the rendered image. The zoom keeps in
  place the **image point under the cursor in the edit view**: that point, mapped from the edit
  view's geometry (the whole image fitted whole, `Compositor.UncroppedBounds`) into the image as
  seen, then **clamped into the kept part** when the cursor is over the part cut off, stays where
  the rendered cell shows it. What the user points at is the zoom's center. In code:
  `GridPreview.KeptPartPoint` gives the cursor's place in the kept part as drawn by the edit view
  (`Bars.Area`), in fractions clamped to [0, 1], and `ZoomAt` takes it as its optional `at` —
  a point of the image shown, kept where it is drawn — instead of reading the cursor.

### Detecting Alt

`WM_MOUSEWHEEL`'s key flags (`MK_*`) carry Ctrl and Shift, never Alt: Alt is read from
`Control.ModifierKeys` when the wheel message arrives, in `GridPreview.WndProc` next to
`_wheelWithControl` (`_wheelWithAlt`). `OnMouseWheel` then takes the `ZoomAt` branch when it is
set, whatever `ShownBars` gives.

### Alt Key Release

Releasing Alt alone, in a window, can put it into **menu mode** (`WM_SYSKEYUP` → `SC_KEYMENU`: the
system menu gets the keyboard, the next keys go to it). After an Alt + wheel, that release must
**not** do it: the user only held Alt as a modifier. The guard is in place whether or not Windows
would start it (harmless when it does not):

- every wheel message over the grid records whether Alt was held (`GridPreview._altWheeled`) — a
  wheel without Alt clears it;
- `MainForm.WndProc` swallows the keyboard-triggered `SC_KEYMENU` (`lParam == 0`) when
  `GridPreview.TakeAltWheel()` says the wheel was turned with Alt, which also clears the record;
- an Alt pressed and released alone keeps its usual behaviour. Left-over case: Alt + wheel, then
  Alt released while another window has the keyboard — the record stays, and the next lone Alt's
  menu mode is swallowed once.

---

## Documentation

| File | Change |
|---|---|
| RULES.md § On-Cell Handles › Resizable Zones | A bullet: Alt + wheel zooms the image as without bars, Ctrl kept; in the crop edit view only the badge shows it |
| README.md / README.fr.md | Crop and Blur wheel bullets, and the Zoom § "The mouse wheel and dragging keep working…" line: Alt + wheel zooms the image |
| GLOSSARY.md / GLOSSARY.fr.md | *Resizable zone* entry: "…with Ctrl; Alt + wheel zooms the image instead". *Crop edit view* entry unchanged |

---

## Test Impact

Nothing testable changes outside the UI: the work is a branch of the wheel gesture in
`GridPreview` (WinForms) and the Alt release in the form, and the repository has **no test
project**. `ZoneScale.Scaled` and `WheelSteps.Zoom` are untouched.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: UI gesture only, no test project | — | — |

---

## Open Questions

- [x] ~~In the crop edit view, the cursor points at the whole image, not at the rendered cell: which
  point does the Alt zoom keep in place?~~ → The image point under the cursor in the edit view,
  kept at its rendered position, clamped into the kept part (Q5, Iteration 2)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Scoping (Q1–Q4): Alt + wheel is the normal wheel zoom, identical, Ctrl kept; in the crop edit view
it really zooms and only the badge shows it; the bars do not change while Alt is held; a simple
subject, one scout pass.

Exploration (read directly — one chain of questions, all in `GridPreview`'s wheel handling):
`OnMouseWheel` picks `ScaleZone` when `ShownBars` gives bars, `ZoomAt` otherwise; Ctrl comes from
`MK_CONTROL` in `WndProc`, which has no Alt flag — hence `ModifierKeys`; the zoom badge is painted
over the cell whatever the view; `ZoomAt` anchors on the rendered image under the cursor, which the
crop edit view does not show — the open question. The form has no `MenuStrip` (only
`ContextMenuStrip`s), so the Alt release risk is the system menu's menu mode, to be checked.

### Iteration 2 — 2026-10-07

Q5 answered: in the crop edit view, the Alt zoom keeps in place the image point under the cursor,
clamped into the kept part, at its rendered position (§ In the Crop Edit View). No open question
left.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given: code, tests and documentation. Branch gate: stays on `main`, the repository's standing
choice (the run commits there directly).

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

No rule broken. Choices the frozen design left open:

- **Menu-mode guard implemented without proving the menu mode happens**: proving it means sending
  synthetic Alt and wheel input on the user's desktop, during an unattended run. The guard is
  harmless when no `SC_KEYMENU` comes; its left-over case is in § Alt Key Release. To check by
  hand: Alt + wheel over the grid, release Alt, then press ↓ — no system menu must drop.
- **`ZoomAt(…, PointF? at)`**: the edit view's anchor is passed as fractions of the image shown,
  not as a screen point turned back through the fine angle — `before` is already the unturned
  drawing, so the point is placed there directly.
- **The edit view's point is read from `Bars.Area`**, the kept part as the edit view draws it, so
  the clamp into the kept part is a clamp to [0, 1].
- **The Alt record is set by any wheel message over the grid** (a locked grid or a gap included):
  Alt was a modifier there too, and its release should not start the menu mode either.
- **One code commit** for `GridPreview` and `MainForm`: the form's guard reads the preview's
  record, they make one unit. Rules, README and glossary in three more commits.

Tested by the user on the delivered build, task confirmed finished (2026-10-07).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-07 | `GridPreview` (Alt read, Alt branch, `KeptPartPoint`, `ZoomAt(at)`, `TakeAltWheel`), `MainForm.WndProc` |
| Unit tests | 3 | 2026-10-07 | Not applicable — no test project, UI gesture only (see § Test Impact) |
| README | 3 | 2026-10-07 | Crop, Blur and Zoom wheel lines, en / fr |
| RULES / GLOSSARY | 3 | 2026-10-07 | RULES § Resizable Zones; *Resizable zone* entry, en / fr |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Bars shown, Alt + wheel over the selected cell: which zoom? | The normal wheel zoom, identical — Ctrl keeps its meaning (finer steps) | 2026-10-07 |
| 2 | In the crop edit view the zoom does not show: what does Alt + wheel do there? | It really zooms; only the zoom badge shows it in the edit view | 2026-10-07 |
| 3 | While Alt is held, do the zone's bars change? | No, nothing changes | 2026-10-07 |
| 4 | Straightforward or tricky / long to explore? | Straightforward — one scout pass | 2026-10-07 |
| 5 | Crop edit view: which point does the Alt zoom keep in place — the image point under the cursor, the rendered cell's point at the same coordinates, or the cell's center? | The image point under the cursor, clamped into the kept part, kept at its rendered position | 2026-10-07 |

---

*Last updated: 2026-10-07*
