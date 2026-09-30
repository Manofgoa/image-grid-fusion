# Restore Last Screen

> Working document — at the next launch, open the window on the monitor it was last used on, when
> that monitor is still connected.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the window opens **centered on the screen the mouse is on**, at the size it had at its last
use (`workfiles/20260927-remember-window-size.md`), clamped to that screen's working area, never
maximized. On a multi-monitor desk, the window comes back wherever the mouse happens to be, not on
the monitor it was used on.

The window should reopen on the **monitor it was last used on**, if that monitor is available.

Components concerned:

| Component | Role today |
|---|---|
| `src/ImageGridFusion/UI/MainForm.cs` — constructor (l.252–258) | `StartPosition = CenterScreen`, the remembered (or default) logical `ClientSize`, `MinimumSize` |
| `MainForm.OnLoad` (l.670) | Sets `_opened`; grows the default size by the global tabs row; clamps the size to `Screen.FromControl(this).WorkingArea`; `CenterToScreen()` — which WinForms centers on the screen **under the mouse** (no owner) |
| `MainForm.OnFormClosing` (l.720) → `SaveWindowSize` (l.2364) | Saves the normal logical client size, once the window was opened in the session; a registry error reported in the status line |
| `src/ImageGridFusion/UI/AppSettings.cs` | The `HKCU\Software\ImageGridFusion` store; `WindowClientSize` / `SaveWindowClientSize` |
| `src/ImageGridFusion/ImageGridFusion.csproj` | `PerMonitorV2`: every monitor has its own DPI, the window rescaled when it changes monitor |
| `README.md` | *Features* (l.40) and *Tray & startup* (l.391) describe the remembered size |

---

## Remembered Screen

### Agreed (Q&A #1–3)

- **The monitor only**: the window reopens on the monitor it was last used on, **centered** on it,
  at the size already remembered. Not its exact position, not its maximized state (it still opens
  un-maximized, as the remembered-size design says).
- **Monitor no longer connected**: the window opens centered on the **primary screen**, at the
  remembered size, shrunk if it does not fit.
- **The remembered size no longer fits that monitor** (resolution changed, smaller scale): the window
  is **brought within its working area** — shrunk, never below the minimum size — the clamp
  `OnLoad` already applies. With the window centered, nothing straddles a monitor edge.

### Design

**Identity of a monitor.** Its `Screen.DeviceName` (`\\.\DISPLAY2`): the name Windows gives the
display output, unchanged by a change of resolution, scale or arrangement. A monitor unplugged makes
its name disappear from `Screen.AllScreens` — "not available". Considered and dropped: a point
(the window's center) or the monitor's bounds — both break on a resolution change or when the
monitors are rearranged, landing on a wrong monitor rather than falling back.

**Storage.** One string value `WindowScreen` in `HKCU\Software\ImageGridFusion`. In `AppSettings`:

| Member | Behaviour |
|---|---|
| `string? WindowScreen` | The saved device name; `null` when none was saved, the value is empty, or the registry cannot be read |
| `SaveWindowScreen(string deviceName)` | Writes it; throws on a registry error, like the other `SaveX` |

The class summary gains the window's screen.

**When it is saved.** With the size, at the same moment and under the same condition: in
`SaveWindowSize` (renamed `SaveWindowPlacement`), on every close or hide, once the window was
opened in the session. The monitor saved is `Screen.FromControl(this)` — the one holding the largest
part of the window; for a minimized window, Windows answers with the monitor of its normal position.
A registry error: "Window placement not remembered: …" in the status line, one message for both.

**When it is applied.**

1. **Constructor**: the **target screen** is resolved — the remembered monitor when it is in
   `Screen.AllScreens`, else the primary screen when a monitor was remembered, else none (see Open
   Questions). With a target, `StartPosition = Manual` and `Location` = the top-left of its working
   area, so the window's handle is **created on that monitor** and gets its DPI from the start — the
   logical size scaled once, as the default is today, no rescale after a move between monitors.
2. **Load**: the size clamp is unchanged, against `Screen.FromControl(this).WorkingArea` — the
   target's. The window is then centered on that working area **by hand**: `CenterToScreen()`
   would bring it back under the mouse. Without a target, today's `CenterToScreen()` stays.

**Limits, accepted.** Windows may renumber the outputs (`DISPLAY1` ↔ `DISPLAY2`) after a driver
update or a dock change: the window then opens on the monitor now bearing that name. The screen is
remembered per user, shared by every instance: the last one closed wins — as the size.

---

## Documentation

- `README.md` — *Features* (l.40): the window reopens on the monitor it was last used on — the
  primary screen when that one is gone — centered, at the size it last had.
- `README.md` — *Tray & startup* (l.391): the **Window size** bullet becomes **Window size and
  screen**: the monitor is remembered with the size; unplugged, the primary screen takes its place.
- `GLOSSARY.md`, `RULES.md`: nothing — no effect, no new term.

---

## Test Impact

None: the solution has no test project (declined since v1, Q&A #12 of
`workfiles/20260923-application-v1.md`); the step is recorded as not applicable in the
Implementation Log.

Manual checks after the run:

| Check | Expected |
|---|---|
| Window on monitor 2, *Quit*, mouse on monitor 1, relaunch | Opens on monitor 2, centered |
| Same, closed maximized | Opens on monitor 2, un-maximized, centered |
| Window on monitor 2, *Quit*, unplug monitor 2, relaunch | Opens on the primary screen, centered, shrunk if needed |
| Monitors at different scales | Same apparent size, relative to the UI, on the remembered monitor |
| Lower monitor 2's resolution below the remembered size | Fits its working area, never below 480 × 320 logical |
| `--tray` launch, *Quit* without opening the window | Remembered screen untouched |
| Delete `WindowScreen` in the registry | See Open Questions |

---

## Open Questions

- [ ] Nothing remembered yet — the first launch after this change, or `WindowScreen` deleted: open
  under the mouse as today, or on the primary screen?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run took on its own —
flagged `🧭 Implementation choices` — and of every adjustment requested afterwards — flagged
`⚙️ Post-implementation`. One entry per request, in the order the requests were made.

### Iteration 1 — 2026-09-30

Scoping batch answered (after two interrupted attempts on 2026-09-29): the monitor only, centered;
the primary screen when it is gone; the size clamped to its working area; exploration
straightforward. A single scout pass, made directly (no subagent: one question, the files named by
`workfiles/20260927-remember-window-size.md`). Design proposed: the monitor's device name in one
registry string, saved with the size, resolved in the constructor so the window is created on that
monitor with its DPI, centered on it by hand at load. One question left: the placement when nothing
is remembered.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply says so rather
than staying blank.

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
| 1 | "Last used screen": what is restored at the next launch — monitor + exact position, monitor only, or monitor + maximized state? | Monitor only | 2026-09-30 |
| 2 | That monitor no longer connected at launch: where does the window open — primary screen with the size kept, or today's behaviour? | Primary screen, size kept | 2026-09-30 |
| 3 | The monitor is there but the remembered placement no longer fits it: bring it within the screen, center it, or keep it as is? | Bring it within the screen | 2026-09-30 |
| 4 | Exploration depth: straightforward or tricky / long? | Straightforward | 2026-09-30 |
| 5 | Nothing remembered yet: open under the mouse as today, or on the primary screen? | | |

---

*Last updated: 2026-09-30*
