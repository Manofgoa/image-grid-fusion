# Delete All Images

> Working document — a button at the bottom left of the window's bottom bar that removes every image at once.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today images can only be removed one by one (the **×** on a hovered cell, or `Delete` on the selected
cell). This adds a single button, at the **bottom left** of the bottom bar, that clears the grid in one
click and brings the app back to its initial state.

Relevant components:

- `src/ImageGridFusion/UI/MainForm.cs` — builds the bottom bar (`TableLayoutPanel`: status line in
  column 0, a `FlowLayoutPanel` with **Copy** / **Save…** in column 1) and enables the buttons in
  `UpdateButtons()` on `GridPreview.ImagesChanged`.
- `src/ImageGridFusion/UI/GridPreview.cs` — owns the image list; removal goes through the private
  `RemoveAt(index)` (dispose, reset selection and hover, `OnImagesChanged()`).
- `README.md` — *Features* and *Output* sections.

---

## Behaviour

- **No confirmation**: one click removes every image immediately.
- **Full reset**: the app goes back to its initial state. In practice, the only state besides the
  images is the active layout, the mirror toggle and the selection, and all of them already reset when
  the image count drops to 0 (`OnImagesChanged()` sets the layout to `null`, which turns the mirror off
  and hides the layout strip). There is no other setting to reset (the crop threshold slider is still
  only *Planned*).
- **Empty grid**: the button stays visible but is **disabled**, like **Copy** and **Save…**
  (`UpdateButtons()`).
- **Status line**: after clearing, a confirmation replaces the current message, through the existing
  `ShowStatus()` (4 s): `1 image removed.` / `{n} images removed.`
- **No keyboard shortcut**: the button is the only way to clear everything.

## UI — Bottom Bar

- The bottom bar gets a third column: `[Clear all] [status line, fills] [Copy] [Save…]`.
  The status line moves right of the new button and keeps filling the free width.
- Label **Clear all**, text only; the button follows the existing ones: a standard WinForms `Button`,
  `AutoSize = true`.

## Code

- `GridPreview` gets a public `Clear()`: disposes every image, empties the list, resets selection,
  hover and pressed state, then calls `OnImagesChanged()` **once** (a single `ImagesChanged` /
  `LayoutChanged`, not one per image). Does nothing when the grid is already empty.
- `MainForm` adds the button, wires `Click` to a handler that reads the image count, calls
  `_preview.Clear()` and shows the status message; the button is enabled in `UpdateButtons()`.

---

## Test Impact

The solution has no test project, and the change is UI-only (a button and a list clear in a
`Control`). Nothing testable changes: no test is created or updated.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project, UI-only change) | — | — |

---

## Open Questions

- [x] ~~Button label (and icon or not)?~~ → **Clear all**, text only
- [x] ~~Keyboard shortcut for clearing everything?~~ → None
- [x] ~~Status line after clearing: a confirmation message, and/or wipe the current message?~~ → A confirmation message (`{n} images removed.`) replacing the current one

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design from the request and the scoping answers (Q1–Q4): a button at the bottom left of the
bottom bar, no confirmation, full reset, disabled on an empty grid. Exploration found that a "full
reset" needs nothing beyond removing the images: layout, mirror and selection already reset with an
empty grid, and the app keeps no other setting. `GridPreview.Clear()` notifies once. No test project,
so no test impact.

### Iteration 2 — 2026-09-23

Open questions answered (Q5–Q7): label **Clear all** (text only), no keyboard shortcut, and a
confirmation on the status line after clearing (`{n} images removed.`, via `ShowStatus()`).

### Iteration 3 — 2026-09-24 — ✅ Implemented

Go given (after an earlier "No"): implement the design as it stands, code and README. The run happens in a
dedicated git worktree on `feature/delete-all-images`, merged back into `main` and removed at the end,
at the user's request (other sessions are working in the `main` checkout meanwhile).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project, UI-only change |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Confirm before deleting all images? | No confirmation | 2026-09-23 |
| 2 | What is reset besides the images? | Everything (back to the app's initial state) | 2026-09-23 |
| 3 | Button behaviour on an empty grid? | Disabled (visible, greyed out) | 2026-09-23 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-23 |
| 5 | Button label (and icon or not)? | Clear all, text only | 2026-09-23 |
| 6 | Keyboard shortcut for clearing everything? | None | 2026-09-23 |
| 7 | Status line after clearing? | A confirmation message, replacing the current one | 2026-09-23 |

---

*Last updated: 2026-09-23*
