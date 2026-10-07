# Ctrl+F Search Focus

> Working document — `Ctrl+F` puts the focus in the file explorer's search box.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A keyboard shortcut to reach the file explorer's search box from anywhere in the window, as in a
browser: `Ctrl+F` opens the explorer if it is collapsed, focuses its search box and selects the
text already typed. `Escape` in the box gives the focus back to the grid. The shortcut is shown in
the app and documented in the README.

Components:

- `UI/MainForm.cs` — `ProcessCmdKey`: the window's shortcuts, with the whitelist of keys the
  search box keeps for itself (`_explorer.IsEditingText`).
- `UI/FileExplorerPanel.cs` — the search box `_search`, `SetOpen` (opening the panel already
  focuses the box and raises `OpenChanged`, which the window saves and uses to show the splitter),
  `OnSearchKeyDown`.
- `README.md` / `README.fr.md` — § File explorer.

---

## Shortcut — Ctrl+F

- `Ctrl+F` anywhere in the window **focuses the explorer's search box and selects all its text**,
  so typing replaces the search and the arrows / `Enter` keep it.
- The explorer **collapsed**: it is **opened first**, as its « button does — the open state saved,
  the splitter shown — then the box focused.
- The box already focused: `Ctrl+F` selects its text again.
- **Another text field focused** (a field of the options toolbars): `Ctrl+F` is **left to it**,
  not taken to the search box.
- The view is left as it is: the search view or the folder view, each with its own search.
- Not locked while exporting: the explorer is not part of the export.

## Leaving the Box — Escape

- `Escape` in the search box **gives the focus back to the grid** (the preview), the cell selection
  untouched. Today `Escape` is already kept by the box (the whitelist in `ProcessCmdKey`) but does
  nothing there.
- It **keeps the text**: the search and its results stay shown; only the focus moves.

## Shortcut Shown

- In the search box's **placeholder**, both views: `Search files… (Ctrl+F, * for all)` and
  `Search this folder… (Ctrl+F, * for all)`. No tooltip.
- README § File explorer (and its French version, in the same commit): `Ctrl+F` and `Escape`.

---

## Test Impact

Nothing testable changes: the work is WinForms key wiring and focus (`ProcessCmdKey`, a
`TextBox`), and the test project holds no UI test of `MainForm` or `FileExplorerPanel`. Checked by
hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (UI wiring only) | — | — |

---

## Open Questions

- [x] ~~`Escape` in the search box: does it clear the text?~~ → No: it only gives the focus back to the grid
- [x] ~~Where is `Ctrl+F` shown: the placeholder, a tooltip on the box, or both?~~ → The placeholder only

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Scoping batch answered (Q&A 1–4): `Ctrl+F` opens a collapsed explorer then focuses the box, its
text selected; `Escape` gives the focus back to the grid; the shortcut is shown in the app and
documented in the README; not active while another text field is focused. Exploration: one direct
pass — `ProcessCmdKey` already whitelists `Escape` for the box, `SetOpen` already opens and focuses.

### Iteration 2 — 2026-10-07

Open questions answered (Q&A 5–6): `Escape` keeps the text and only moves the focus to the grid;
`Ctrl+F` is shown in the placeholder of both views, no tooltip.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no testable change (§ Test Impact) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | The explorer collapsed at `Ctrl+F`: what happens? | Open it, then focus the box | 2026-10-07 |
| 2 | The text already in the search box? | Select it all | 2026-10-07 |
| 3 | Extras in scope (active in another text field, Escape back to the grid, shortcut shown, README)? | Escape back to the grid, shortcut shown, README — not active in another text field | 2026-10-07 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-07 |
| 5 | `Escape` in the search box: does it clear the text? | No — keep the text, only move the focus | 2026-10-07 |
| 6 | Where is `Ctrl+F` shown? | In the placeholder | 2026-10-07 |

---

*Last updated: 2026-10-07*
