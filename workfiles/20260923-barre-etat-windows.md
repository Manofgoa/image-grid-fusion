# Windows Tray & Startup

> Working document — tray icon, close-to-tray, and an opt-in "start with Windows" setting.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Three linked behaviours for the WinForms app:

1. **Tray icon**: the app shows an icon in the Windows notification area (system tray) while it runs.
2. **Close to tray**: closing the window (the **×**, `Alt+F4`) only hides it — the process keeps
   running, and the window can be reopened from the tray icon. The app really exits only from the
   tray menu's **Quit** (or when Windows ends the session).
3. **Start with Windows**: an opt-in setting, **in the main window**, that registers the app to
   launch at session start. When launched that way, the app starts **hidden**: only the tray icon
   appears.

Components touched: `Program.cs` (entry point, application lifetime), `UI/MainForm.cs` (closing
behaviour, the ⚙ settings menu), and new files: `UI/TrayApplicationContext.cs` (lifetime and tray
icon), `UI/StartupRegistration.cs` (the `Run` value), `UI/AppIcon.cs` and `app.ico` (the icon).

---

## Current State (from the codebase)

- `Program.Main` runs `Application.Run(new MainForm(args))`: closing `MainForm` ends the message
  loop, hence the process.
- `MainForm` has no icon of its own (no `ApplicationIcon` in the `.csproj`, no `.ico` in the repo):
  the window and the exe use the default WinForms icon.
- No settings persistence exists (no settings file, no registry access).
- Command-line arguments are all treated as files to load (`_startupFiles`, loaded in `OnShown`).
- Bottom bar: a `TableLayoutPanel` with the status line on the left and the **Copy** / **Save…**
  buttons on the right.

---

## Application Lifetime

- The message loop no longer belongs to `MainForm`: a `TrayApplicationContext` (an
  `ApplicationContext`) owns the `NotifyIcon` and the `MainForm`, and `Program.Main` runs
  `Application.Run(context)`.
- **Normal launch**: the context shows the form, exactly as today.
- **Launch with `--tray`** (the startup registration's argument): the form is created but not
  shown; only the tray icon appears. `--tray` (`TrayApplicationContext.HiddenArgument`, matched
  case-insensitively) is stripped from the arguments in `Program.Main` before they reach
  `_startupFiles`, so it is never treated as a file.
- The form is deliberately **not** the context's `MainForm`: `Application.Run` would show it, and
  closing it would end the app.
- **Several instances are allowed** (user decision): each instance has its own window and its own
  tray icon; relaunching the exe never reuses a running instance.

## Closing & Reopening

- `MainForm.FormClosing` with `CloseReason.UserClosing` (the **×**, `Alt+F4`, the taskbar's
  *Close window*) is **cancelled** and the form is **hidden** instead. The grid state (images,
  layout, mirror) stays as it was.
- Any other close reason (`WindowsShutDown`, `TaskManagerClosing`, `ApplicationExitCall`, and
  `None` — a bare `WM_CLOSE` sent by another program) closes for real, so a logoff or shutdown is
  never blocked; the context then ends the app (`FormClosed` → `ExitThread`).
- **Reopen**: clicking the tray icon, or its menu's **Open**, shows the window, restores it if
  minimized, and brings it to the front.
- **Quit** (tray icon's right-click menu) **closes the app completely**: `MainForm.CloseForGood()`
  closes the form without the hide, then the context disposes the tray icon (so no ghost icon
  remains in the tray) and ends the process. It is the only user-facing way to exit.
- No notification when the window is hidden: it disappears silently.
- The minimize button keeps its default behaviour (window minimized to the taskbar).

## Tray Icon

- `NotifyIcon`, visible for the whole life of the process, tooltip `Image Grid Fusion`.
- Context menu (right click): **Open**, separator, **Quit**.
- A **single left click** reopens the window.
- Icon: a **dedicated `src/ImageGridFusion/app.ico`** — a 2×2 grid of rounded tiles, blue, green,
  amber and red — with frames at 16, 20, 24, 32, 40, 48, 64 px (32-bit DIB) and 256 px (PNG). It is
  the project's `ApplicationIcon` (the exe) and an embedded resource that `AppIcon.Load()` reads
  for the window (`MainForm.Icon`); the tray takes that icon's small-size frame
  (`SystemInformation.SmallIconSize`).
- The `.ico` was drawn by a throwaway generator in the session scratchpad; the generator is not
  part of the repository.

## Start with Windows

- Registration: a value named `ImageGridFusion` under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, holding `"<exe path>" --tray`, where
  `<exe path>` is `Environment.ProcessPath` (falling back to `Application.ExecutablePath`).
  Per-user, no admin rights needed.
- **The registry is the only source of truth**: no settings file. The item is ticked from the
  value's presence **each time the ⚙ menu opens**, and toggling it writes / deletes the value. A
  registry that cannot be read shows the item unticked.
- **Off by default**: nothing is registered until the user ticks the setting.
- The setting lives **in the main window**: a small **⚙ button** in the bottom bar, left of
  **Copy**, with a *Settings* tooltip, opens above itself a menu holding a checkable
  **Start with Windows** item. The menu is the home for future settings. The button sizes itself
  like **Copy** / **Save…** (same height).
- A registry write failure shows an error in the status line and leaves the item as it was.
- **Moved exe**: at every launch (`StartupRegistration.Refresh()` in `Program.Main`), if the `Run`
  value exists but differs from the current `"<exe path>" --tray`, it is silently rewritten with
  the current path (a failure is ignored); the item stays ticked.
  Consequence: launching another copy of the exe (e.g. a `dotnet run` build next to the published
  exe) moves the registration to that copy.
- Known limit: disabling the app in Windows *Settings → Apps → Startup* (the `StartupApproved`
  key) is not reflected by the control — it only reflects the `Run` value.

---

## Test Impact

The solution has no test project (declined in v1, Q&A #12 of `20260923-application-v1.md`, and
in every workfile since), and the user chose to stay without one (Q&A #10): the behaviours are
checked manually.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| **×** / `Alt+F4` hides the window, the process and the tray icon stay | — (manual) | — |
| Tray click / **Open** restores the window with its grid unchanged | — (manual) | — |
| Tray right click → **Quit** ends the process, no icon left in the tray | — (manual) | — |
| Registered path differing from the current exe → rewritten at launch | — (manual, `regedit`) | — |
| Setting on → `Run` value written with `--tray`; off → value deleted | — (manual, `regedit`) | — |
| Launch with `--tray` → no window, tray icon only; `--tray` not loaded as a file | — (manual) | — |
| Two instances → two tray icons, independent | — (manual) | — |

---

## Open Questions

- [x] ~~What form does the "Start with Windows" setting take in the window — a checkbox in the
  bottom bar (e.g. left of **Copy**), or something else (a small ⚙ menu button)?~~ → ⚙ button in
  the bottom bar, opening a menu with a checkable item
- [x] ~~Which icon for the tray (and, as a side effect, the window and the exe)? The repo has none:
  create a dedicated `.ico` (e.g. a 2×2 grid glyph) set as `ApplicationIcon`, or use the default
  WinForms / system application icon?~~ → dedicated `.ico`, 2×2 grid glyph, set as `ApplicationIcon`
- [x] ~~Reopen from the tray on a **single** click, or on a **double** click (Windows convention for
  many apps)?~~ → single left click
- [x] ~~On the first close to tray, show a one-time balloon notification ("Image Grid Fusion is
  still running in the notification area"), or nothing?~~ → nothing
- [x] ~~If the exe was moved after being registered, the `Run` value points to the old path: show
  the setting as ticked anyway (value present), or as unticked (value present but not pointing to
  the current exe), or silently rewrite it to the current path on launch?~~ → silently rewritten
  at launch, item stays ticked
- [x] ~~Unit tests: stay without a test project (manual checks above), as in every previous
  workfile?~~ → yes, manual checks only

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-23

Initial design from the user's request and the scoping batch (Q&A #1–#4):

- Setting in the main window (not in the tray menu, no first-launch prompt).
- Launched at session start → hidden, tray icon only (`--tray` argument).
- Several instances allowed, each with its own tray icon.
- Subject sized as straightforward: one scout pass of the codebase.

Proposed: an `ApplicationContext` owning the tray icon and the form; user close cancelled into a
hide; real exit from the tray **Quit** or a system-initiated close; registration in the per-user
`Run` key, with the registry as the only source of truth. Six questions left open.

### Iteration 2 — 2026-09-23

User answers to Q&A #5–#7:

- Setting: a ⚙ button in the bottom bar, left of **Copy**, opening a menu with a checkable
  **Start with Windows** item (room for future settings).
- Icon: a dedicated multi-size `.ico` (2×2 grid glyph), set as `ApplicationIcon` and reused for the
  window and the tray.
- Tray: a single left click reopens the window; right click opens the menu.

Three questions remain open (balloon, moved exe, tests).

### Iteration 3 — 2026-09-23

User answers to Q&A #8–#10:

- No notification when the window is hidden to the tray.
- A `Run` value pointing to another exe path is silently rewritten with the current path at
  launch; the item stays ticked (noted consequence: another copy of the exe takes over the
  registration).
- No test project: manual checks only.

### Iteration 4 — 2026-09-23

User request: *right click on the notification icon must allow closing the app completely*.
Already covered by the tray menu's **Quit**; the design now states explicitly that it is reached
by a right click and ends the process, and a manual check pins it. No open question left.

### Iteration 5 — 2026-09-24 — ✅ Implemented

Go given by the user (*"GO implémente dans un worktree spécifique que tu supprimeras à la fin"*),
after the first go was declined (Q&A #11). Read as **code only**: the go names neither tests nor
documentation. Work happens in a dedicated worktree on `feature/barre-etat-windows`, branched from
`main` at `7eb1142`; the worktree is removed at the end of the run.

### Iteration 6 — 2026-09-24 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Go read as code only**: the README is not updated (it does not mention the tray, closing to
  the tray, or the setting yet).
- **Icon**: 2×2 grid of rounded tiles in blue, green, amber and red. Frames 20, 40 and 64 px were
  added to the planned 16/24/32/48/256 so the icon stays sharp at 125 %, 150 % and 200 % scaling.
  Small frames are 32-bit DIBs, the 256 px frame is a PNG. The generator lives in the scratchpad
  and is not committed.
- **Icon loading**: `app.ico` is both the `ApplicationIcon` and an embedded resource
  (`AppIcon.Load()`), because a WinForms window does not pick up the exe's icon on its own; the
  tray takes the small-size frame.
- **Item ticked each time the ⚙ menu opens**, not once when the window is built: it follows a
  change made elsewhere (another instance, `regedit`) without restarting.
- **`CloseReason.None`** (a bare `WM_CLOSE` from another program, e.g.
  `Process.CloseMainWindow`) closes for real, like the other non-user reasons: WinForms reports
  the ×, `Alt+F4` and the taskbar's *Close window* as `UserClosing`, which is the only reason that hides.
- **Placement**: the three new classes live in `UI/`; the `--tray` constant is
  `TrayApplicationContext.HiddenArgument`, matched case-insensitively.
- **⚙ button**: menu opened above the button, *Settings* tooltip, auto-sized to the height of
  **Copy** / **Save…** (a fixed height looked shorter at high DPI).
- **Registry errors**: a read failure shows the item unticked, a failure while rewriting a moved
  path at launch is ignored, a toggle failure shows `Start with Windows failed: …` in the status
  line.
- **Branch**: `feature/barre-etat-windows` is **not merged** into `main`: `main` holds another
  session's uncommitted work (preview from file) in `MainForm.cs`. The branch is kept after the
  worktree is removed.

Checked by running the built exe: `--tray` starts with no window and the process running; a
normal start shows the window with the new icon and the ⚙ button; a simulated × (`SC_CLOSE`)
hides the window and the process keeps running. The tray menu, the reopen click and the
registry toggle are left to the manual checks in `## Test Impact`.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5, 6 | 2026-09-24 | Icon, tray lifetime and close-to-tray, Start with Windows; 3 commits on `feature/barre-etat-windows` |
| Unit tests | — | 2026-09-24 | Not applicable: no test project, manual checks only (Q&A #10) |
| README | — | 2026-09-24 | Not done: the go covered the code only |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How is "start with Windows" offered? (tray menu checkbox / first-launch prompt + menu / setting in the window) | Setting in the window | 2026-09-23 |
| 2 | When launched by Windows at session start: hidden (tray only) or window shown? | Hidden, tray icon only | 2026-09-23 |
| 3 | Relaunching the exe while an instance runs: single instance (reopen it) or several instances? | Several instances allowed | 2026-09-23 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-09-23 |
| 5 | Form and placement of the "Start with Windows" setting in the window? | ⚙ button with a menu (first ask dismissed, re-asked) | 2026-09-23 |
| 6 | Tray icon: dedicated `.ico` or default icon? | Dedicated `.ico` | 2026-09-23 |
| 7 | Reopen from the tray on single or double click? | Single click | 2026-09-23 |
| 8 | One-time balloon on the first close to tray? | No, nothing | 2026-09-23 |
| 9 | Registered path no longer matching the current exe: ticked, unticked, or rewritten? | Rewritten at launch | 2026-09-23 |
| 10 | Unit tests: stay without a test project? | Yes, manual checks | 2026-09-23 |
| 11 | Go for implementation? (No / code / code, tests and documentation) | No — the gate holds | 2026-09-23 |

---

*Last updated: 2026-09-24*
