# Explorer Window

> Working document — **pre-created** on 2026-09-26, during the post-implementation adjustments of
> `workfiles/20260926-file-explorer.md`, at the user's request: the file explorer's list shown in
> **a window of its own**, so it can sit beside the main window — or on another screen. **Its
> design has not started**: the scoping batch is still to be asked.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer (`workfiles/20260926-file-explorer.md`) is a panel docked at the right of the
preview, 1 to 5 columns of thumbnail tiles wide. This follow-up lets the user **detach** it into a
separate top-level window: the search box, the status, the favorites and the results move there,
the window is placed and sized freely — on a second screen too — and its tiles are still dragged
into the cells of the main window, a `FileDrop` crossing top-level windows of one process exactly
as it crosses the Explorer's.

Depends on the file explorer: `FileExplorerPanel` already owns everything the window would show;
the work is a host window, the docking / undocking, and what the main window shows meanwhile.

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones. To be completed by the scoping batch when the task starts.

- [ ] How is it detached: a button in the panel's header (⧉), a ⚙ menu item, or a drag of the
  header out of the window?
- [ ] What the main window shows while detached: the collapsed strip with a *Bring back* button,
  nothing at all (the preview taking the room), or the strip only?
- [ ] Docking back: closing the window, its own button, or both?
- [ ] Remembered between sessions: detached or not, the window's position and size (which
  screen), its column count — the same `ExplorerColumns` or one of its own?
- [ ] Keyboard: does the search box in the window keep the main window's shortcuts at bay the
  same way (`Ctrl+V`, `Delete`…), and does `Ctrl+F` in the main window focus the detached box?
- [ ] Always on top of the main window (an owned window), or an independent one that can go
  behind it?
- [ ] A window on a screen at another DPI: the tiles and the thumbnails rescaled there?
- [ ] The tray: hiding the main window hides the explorer window too?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 0 — 2026-09-26 — Pre-created

Placeholder created during the file explorer's adjustments, at the user's request ("une autre
fenêtre, et potentiellement un autre écran"). No scoping batch, no exploration yet: the questions
above are the agent's first list, to be reviewed and completed with the user before Iteration 1.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | Not started |
| Unit tests | | | Not started |
| README | | | Not started |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|

---

*Last updated: 2026-09-26*
