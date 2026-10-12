# Contain / Fill Recenter the Image

> Working document — pressing the Zoom effect's Contain or Fill puts the image back at the center
> of its cell, instead of keeping it where it was moved.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, pressing **Contain** or **Fill** (the Zoom effect's fit modes) keeps the **image's center**
where it stands (RULES.md § Zoom Keeps the Image in Place): an image dragged off-center stays
off-center once fitted — Contain then no longer shows the whole image, Fill no longer covers the
cell. The fit modes should give what their names promise: pressing one **recenters** the image.

Components: `GridPreview.FitSelected` (the only route of the fit buttons, called by
`MainForm.SetZoomFit`), `ImageLook.Focus` (the image point kept at the cell's center, `Center` =
`(0.5, 0.5)`), RULES.md, README (en / fr), GLOSSARY (en / fr).

---

## Behaviour

| Action | Image position |
|---|---|
| **Contain** or **Fill** pressed — from the free zoom, or from the other mode | **Recentered**: `Focus = (0.5, 0.5)`, the image's center on the cell's center (offset 0, 0) |
| The pressed mode unpressed (back to the free zoom it gave) | **Unchanged** — nothing moves, as today |
| The Zoom slider, the Animations' Zoom motion | Unchanged rule — they keep the image's center (`FitCalculator.ImageCenter`) |
| The wheel (Alt + wheel over a zone included) | Unchanged rule — keeps the image point under the cursor |

- **One-off recentering**: the image is recentered at the press only. While the mode stays pressed,
  a drag or the arrow keys still move the image freely, and the mode stays pressed (README: *"can
  still be dragged"*). A later change of the cell's shape (separator, layout, format, crop, turn)
  keeps the fit's zoom following it, the position as it stands — no new recentering.
- **Position readout**: shown as its rule says (RULES.md § Position Readout — the offset of an image
  staying in its cell changes): it appears briefly and reads `x 0px` / `y 0px`. Nothing to code —
  `GridPreview.UpdateReadouts` sees the change by itself.
- **Undo**: the press is one step, as today (`GridHistory` commits it once settled).
- **Text content**: Contain and Fill keep it at 100 % and it does not drag; recentering it changes
  nothing visible.
- With a **fine angle**: the fit zoom is unchanged; the image's center is put on the cell's center.

### Code

`GridPreview.FitSelected`: for `ZoomFit.Contain` / `ZoomFit.Fill`, the fitted look takes the
**center** as its focus (`fitted.WithFocus(<center>)`) instead of `FitCalculator.FocusKeeping(…)`;
`ZoomFit.None` keeps the current computation (the zoom shown kept, nothing moves). Its summary is
updated to say so.

### Rules and Docs

- **RULES.md § Zoom Keeps the Image in Place**: Contain / Fill leave the list of the routes *without a
  cursor* keeping the image's center, and become the **exception**: a fit mode pressed **recenters**
  the image (the mode unpressed moves nothing). The section's opening line ("nothing else moves it")
  names that exception.
- **README.md / README.fr.md**, line 39 (zooming never moves the image otherwise) and line 136 (the
  fit modes — *"The image stays where it was moved, and can still be dragged"*): Contain / Fill
  recenter the image, which can still be dragged afterwards.
- **GLOSSARY.md / GLOSSARY.fr.md**, *Fit mode*: pressing it puts the image back at the cell's center.

---

## Test Impact

The repository has **no unit test project** (`src/` holds the app only): nothing testable is created
or updated. The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~Which fit modes recenter?~~ → Both Contain and Fill, at the press; unpressing moves nothing; the slider and the Animations keep their rule.
- [x] ~~Does the image stay locked at the center while the mode is pressed?~~ → No: one-off recentering at the press; a drag or the arrows move it freely afterwards, the mode staying pressed.
- [x] ~~Does the position readout show the recentering?~~ → Yes, as its rule says: briefly, `x 0px` / `y 0px`.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-12

Initial design, from the scoping batch (Q&A 1–4): Contain and Fill recenter the image at the press
(`Focus = Center` in `GridPreview.FitSelected`), once; unpressing, the slider, the wheel and the
Animations keep their current rule; the position readout shows the recentering by its own rule.
RULES.md § Zoom Keeps the Image in Place, the README and the glossary (en / fr) follow. No test
project in the repository: the Test Impact is empty, said so.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository — does not apply |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When Contain or Fill is pressed, what becomes of the image's position? | Both recenter (offset 0, 0) at the press; unpressing moves nothing; the slider and the Animations keep their rule | 2026-10-12 |
| 2 | Once recentered, does the image stay centered while the mode is pressed? | One-off recentering: a drag or the arrows move it freely afterwards | 2026-10-12 |
| 3 | Should the position readout show during the recentering? | Yes, as its rule says | 2026-10-12 |
| 4 | Depth of the codebase exploration? | Straightforward — a single scout pass | 2026-10-12 |

---

*Last updated: 2026-10-12*
