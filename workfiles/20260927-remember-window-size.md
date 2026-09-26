# Remember Window Size

> Working document — remember, between two launches, the size the window had at its last use;
> never its maximized state.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the window opens centered on the screen the mouse is on, at a fixed client size of
960 × 860 logical pixels — scaled to the monitor's DPI by `AutoScaleMode.Dpi`, the app being
PerMonitorV2 — with a minimum of 480 × 320. Whatever the user resized it to is lost at the next
launch, while the file explorer's panel already remembers its open state and its column count
between sessions.

The window should reopen at the size it had at its **last use** — the size it had when it was last
closed or hidden — and **never maximized**, even when it was closed maximized.

Components concerned:

| Component | Role today |
|---|---|
| `src/ImageGridFusion/UI/MainForm.cs` — constructor (l.193–205) | `StartPosition = CenterScreen`, `ClientSize = 960 × 860`, `MinimumSize = 480 × 320`, set before the DPI scaling |
| `MainForm.OnFormClosing` (l.590) | The **×**, `Alt+F4` and the taskbar's *Close* only hide the window (`Hide()`); a real close — *Quit*, logoff, shutdown — goes through, delayed while an export stops |
| `MainForm.OnShown` (l.560), `OnVisibleChanged` (l.579), `OnDpiChanged` (l.471) | First-show work; hidden / shown; icons redrawn when the window moves to another monitor |
| `MainForm.FollowExplorerWidth` (l.2106) | Already resizes the window programmatically — when not maximized, clamped to the screen's working area — as the file explorer widens |
| `MainForm.OnLoad` — **in progress**, uncommitted in the working tree (`workfiles/20260926-global-effects-tabs.md`) | Grows the window by the global effects' tabs row at start-up, capped at the working area's height, then re-centers it — designed for the default size |
| `src/ImageGridFusion/UI/AppSettings.cs` | The per-user registry store, `HKCU\Software\ImageGridFusion`: a getter per setting, returning its default when the value is missing or the registry cannot be read; a `SaveX` per setting, throwing on failure; the form wraps each save in a try / catch and reports "… not remembered" in the status line |
| `src/ImageGridFusion/UI/TrayApplicationContext.cs` | Owns the app's lifetime: shows the window at start (not with `--tray`), hides it on close, quits from the tray through `MainForm.CloseForGood` (l.554) |
| `README.md` | *Features* and *Tray & startup* list what is remembered between sessions |

No true full-screen mode exists in the app: "full screen" is the **maximized** state (the ▢ button,
`Win+↑`, a double-click on the title bar).

---

## Remembered Size

### Agreed (Q&A #1–3)

- **Size only**: width and height. Not the position — the window keeps opening **centered on the
  screen the mouse is on**, as today — and not the maximized state.
- **Closed maximized (or minimized)**: the **normal size** — the one the window had before it was
  maximized, its `RestoreBounds` — is remembered; the window reopens **un-maximized**, at that size.
- **Larger than the screen**: shrunk to the **working area** of the screen it opens on, never below
  the window's minimum size.

### Design

**Unit.** The **client size in logical pixels (96 DPI)**, exactly what the constructor sets today
with `ClientSize = new Size(960, 860)`: WinForms scales it to the monitor's DPI at creation, so a
remembered size takes the same road as the default and looks the same, relative to the UI, on a
screen at another scale. Saved as the client size divided by `DeviceDpi / 96`, rounded.

**Storage.** Two DWORD values in `HKCU\Software\ImageGridFusion`, next to the other settings:
`WindowWidth` and `WindowHeight`. In `AppSettings`:

| Member | Behaviour |
|---|---|
| `Size? WindowClientSize` | Both values present and positive → the size; else `null` — nothing saved, a value missing or ≤ 0, or the registry unreadable (the existing `IsRegistryError` filter) |
| `SaveWindowClientSize(Size size)` | Writes both values; throws on a registry error, like the other `SaveX` |

The class summary, which lists the settings, gains the window size. No settings file, no ⚙ menu
entry: nothing to reset — the default comes back when the values are absent.

**When it is saved.** When the window closes, whatever the reason: hidden to the tray by the **×**,
`Alt+F4` or the taskbar's *Close*; closed for good by *Quit*, a logoff or a shutdown. In
`OnFormClosing`, first thing, before the hide / export decisions (a close delayed by an export saves
once more when it goes through — the same value, harmless). A registry error is reported like the
other settings — "Window size not remembered: …" in the status line, read when the window comes back.

- Only when the window was **opened at least once** in the session (a flag set at load): a `--tray`
  run quit from the tray without ever opening the window is not a use and changes nothing.
- A crash or a *Task Manager* kill saves nothing; the previous size stays. Saving at every resize
  end was considered and dropped: `ResizeEnd` follows neither maximize / restore nor the
  explorer-driven resize, and closing covers every case with one write.
- The normal client size while maximized or minimized: `RestoreBounds.Size` minus the non-client
  frame (`SizeFromClientSize(Size.Empty)`, computed from the window styles, valid in every state);
  while normal, `ClientSize`.

**When it is applied.**

1. **Constructor**: `ClientSize = AppSettings.WindowClientSize ?? new Size(960, 860)`, at the same
   spot as today, so the DPI scaling and `MinimumSize` apply to it as they apply to the default.
   `StartPosition` stays `CenterScreen`; the window still opens un-maximized.
2. **Load** (`OnLoad` — the handle created, the window already centered on its screen, not yet
   visible, so no visible jump): the window is **shrunk to the working area** of
   `Screen.FromControl(this)` when it exceeds it, width and height independently, `MinimumSize` as
   the floor — the clamp `FollowExplorerWidth` already uses — then **re-centered**
   (`CenterToScreen`).

**Interaction — the global effects' tabs row.** The `OnLoad` override that
`workfiles/20260926-global-effects-tabs.md` is adding grows the window by the tabs row's height at
start-up, so the preview keeps the size it had when the global effects sat in one row. That growth
is meant for the **default** size: applied to a remembered size, the window would grow by a row at
each launch — opened at the remembered size, grown, saved grown. The two land in one `OnLoad`: grow
**only when no size was remembered**, then clamp to the working area, then re-center.

> Note — the load clamp applies to whatever size the window opens with, the remembered size **or
> the default**: today, at 150 % on a 1080p screen, the default's 860 logical px of height become
> 1290 device px, more than the screen's working area, and the window overflows; after this work it
> fits. A small change of today's behaviour, in the spirit of the agreed rule.

**Limits, accepted.** A window straddling two monitors of different scales at closing is measured
with the DPI Windows gives it. The size is remembered per user, shared by every instance running
side by side: the last one closed wins. At a fractional scale, the round trip through logical
pixels may lose 1 px on the first relaunch (measured at 125 %: a 982 × 753 client reopens at
982 × 752), then stays put.

---

## Documentation

- `README.md` — *Features*: a bullet after "Lives in the notification area…": the window remembers
  its size between launches — never its maximized state: it opens un-maximized, centered, at the
  size it last had, shrunk to fit a smaller screen.
- `README.md` — *Tray & startup*: a bullet after the ⚙ menu settings: the size the window had when
  last closed or hidden, remembered per user in `HKCU\Software\ImageGridFusion`, whatever the
  screen's scale; closed maximized or minimized, it reopens at its normal size.
- `GLOSSARY.md`, `RULES.md`: nothing — no effect, no new term.

---

## Test Impact

None: the solution has no test project (declined since v1, Q&A #12 of
`workfiles/20260923-application-v1.md`, and every workfile since stayed test-free); the step is
recorded as not applicable in the Implementation Log.

Manual checks after the run:

| Check | Expected |
|---|---|
| Resize the window, *Quit*, relaunch | Same size, centered, un-maximized |
| Resize, hide with **×**, *Quit* from the tray, relaunch | Same size |
| Maximize (or minimize), *Quit*, relaunch | Un-maximized, at the size it had before |
| Remembered size larger than the screen (lower the resolution, or a smaller screen) | Fits the working area, centered; never below 480 × 320 logical |
| `--tray` launch, *Quit* without opening the window | Remembered size untouched |
| Delete `WindowWidth` / `WindowHeight` in the registry | Default 960 × 860 back |
| Two monitors at different scales | Same apparent size, relative to the UI, on both |

---

## Open Questions

None — the scoping batch settled the user's side (Q&A #1–4); the design choices above are the
agent's proposals, open to the user's reading before the go.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run took on its own —
flagged `🧭 Implementation choices` — and of every adjustment requested afterwards — flagged
`⚙️ Post-implementation`. One entry per request, in the order the requests were made.

### Iteration 1 — 2026-09-27

Scoping batch answered: size only, the normal size when closed maximized, shrunk to the screen,
exploration straightforward. A single scout pass, made directly (no subagent: the files are named
by `RULES.md`, three greps answered everything). Design proposed: the logical client size in two
registry DWORDs of the existing `AppSettings` store, saved when the window closes or hides (once
opened in the session), applied in the constructor like the default, clamped to the working area at
load — the default included. Found in the working tree, uncommitted: an `OnLoad` override from
`workfiles/20260926-global-effects-tabs.md` growing the window at start-up — the design confines
that growth to the default size.

### Iteration 2 — 2026-09-27 — ✅ Implemented

Go given for the code, the unit tests (not applicable) and the documentation. Branch: stays on
`main`, the repository's standing choice (no worktree asked). At the go, the working tree was
clean: the concurrent `OnLoad` change had left it.

### Iteration 3 — 2026-09-27 — 🧭 Implementation choices

No divergence from the frozen design, no rule broken. Names: `AppSettings.WindowClientSize` /
`SaveWindowClientSize`; in `MainForm`, `_sizeRemembered` (set in the constructor), `_opened` (set at
load), `SaveWindowSize` and `DeviceToLogicalUnits`, the reverse of `LogicalToDeviceUnits`. The tabs
row growth of `OnLoad` (commit 39e4a5a, landed during the design) applies only when no size was
remembered, as designed; the clamp and the re-centering follow in both cases. Checked by script on
the built exe, with SC_CLOSE (the real ×): maximized then closed → the normal client size saved;
minimized then closed → the same; reopened at that size, un-maximized; 9000 × 9000 remembered → the
window fits the working area (3840 × 2100). A raw WM_CLOSE (`Process.CloseMainWindow`) is classed
*TaskManagerClosing* by WinForms and closes the app for real: the size is saved on that path too.
Finding: 1 px of rounding at fractional scales on the first relaunch, then stable (see the limits).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply says so rather
than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 2 | 2026-09-27 | `AppSettings.WindowClientSize` / `SaveWindowClientSize`; `MainForm`: constructor, `OnLoad`, `OnFormClosing`, `SaveWindowSize`. Checked by script: ×, maximized, minimized, 9000 × 9000 clamped |
| Unit tests | 1 | 2026-09-27 | Not applicable — no test project (see Test Impact) |
| README | 2 | 2026-09-27 | *Features* bullet, *Tray & startup* bullet |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What to remember: the size only, or the size and the position? | Size only | 2026-09-27 |
| 2 | Closed maximized (or full screen): what is kept? | The normal size it had before — it reopens un-maximized, at that size | 2026-09-27 |
| 3 | Remembered size larger than the current screen (other screen, resolution changed)? | Shrunk to the screen's working area, not below the minimum size | 2026-09-27 |
| 4 | Straightforward, or tricky / long to explore? | Straightforward — a single scout pass | 2026-09-27 |

---

*Last updated: 2026-09-27*
