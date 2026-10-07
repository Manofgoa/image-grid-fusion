# Single Instance

> Working document — launching the exe again brings the running instance back instead of
> starting a new process, with a launch argument that bypasses it for tests.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today every launch of `ImageGridFusion.exe` starts a new process, with its own window and tray
icon (README § Tray & startup: *"Several instances can run side by side"*). From now on:

- A launch while an instance of **the same exe** already runs starts no second app: it hands its
  work to the running instance and exits.
- The running instance comes back to the front — minimized, hidden in the tray, or behind other
  windows.
- Files given to the second launch (dropped on the exe icon, *Open with*) are loaded by the
  running instance, like a drop.
- A launch argument starts an independent instance anyway, for tests (e.g. the agent's own checks
  while the user's instance runs).

Components: `Program.Main` (the lock and the hand-over), `UI/TrayApplicationContext` (bringing the
window back — `ShowForm` already does it for the tray click), `UI/MainForm` (`AddFilesAsync`, used
for the startup files), README § Tray & startup.

---

## Behaviour

### One Instance per Exe Location

- The lock is keyed on the **exe's full path** (case-insensitive): a Debug build under `bin/` and a
  published copy elsewhere are two different apps, each with its own single instance.
- It is **per Windows user session**: another user logged on the same machine runs their own.

### A Second Launch

| The running instance's window is | The second launch does |
|---|---|
| Visible, in front | Nothing more than loading its files, if any |
| Behind other windows | Brings it to the front |
| Minimized | Restores it — **maximized again if it was maximized** before being minimized — and brings it to the front |
| Hidden in the tray (closed) | Shows it and brings it to the front |

- A second launch carrying **`--tray`** (Windows start-up while the app already runs) **leaves the
  running instance as it is**: nothing shown, nothing loaded.
- A **`--title`** it carries is **ignored**: the running instance keeps its second title; only the
  files are handed over.
- It then **exits at once**: no window, no tray icon, not even briefly.
- **Its files** are handed over as full paths (relative ones resolved against the second launch's
  own working directory) and loaded with `AddFilesAsync`, exactly like a drop on the window:
  free slots first, then the replace rule, then the excess ignored with a status-line message.
  While an export runs, they are **refused** with the usual status message, like a drop.
- If the running instance does not answer (hung, closing), the second launch gives up after a
  short timeout and exits without starting a new instance.

### The Test Bypass Argument

- Proposed name: **`--new-instance`**.
- A launch with it starts a new instance even when one already runs.
- That instance is **fully independent**: it takes no lock and answers no second launch. The
  "normal" instance stays the one later launches bring back; with none running, the next normal
  launch starts one, whatever `--new-instance` instances run beside it.
- In `MainForm`, the switch is `savesWindowSize`.
- It **leaves the user's settings alone** where the app writes them on its own:
  - no `StartupRegistration.Refresh` — the *Start with Windows* shortcut (user's *Startup* folder)
    keeps pointing at the exe it points at;
  - no `RegistryMigration.Run` — an older version's registry values stay for a normal launch to
    move;
  - the window size is **read** at start-up but **never saved** to `settings.json` when the window
    closes or hides.
  Settings the user changes by hand in that instance (⚙ menu, explorer…) are saved as usual.
- It is not a file to load (filtered out like `--tray` and `--title`), and mixes with them in any
  order (RULES.md § Command-Line Arguments).

### Restoring a Minimized Window

- The tray click (`TrayApplicationContext.ShowForm`) and the second launch bring the window back
  the same way: a window minimized while maximized comes back **maximized**, one minimized at its
  normal size comes back at that size (today the tray click always restores the normal size).

### Technical Approach

`UI/SingleInstance.cs`:

- A named `Mutex` (`Local\ImageGridFusion-{first 16 hex digits of the SHA-256 of the upper-cased exe
  path}`) taken in `Program.Main` before anything else — the arguments parsed first, then the lock,
  then `ApplicationConfiguration.Initialize`, `RegistryMigration.Run` and
  `StartupRegistration.Refresh`, which a second launch never runs.
- A named pipe `{key}-{Windows session id}` — pipe names are machine-wide, unlike the mutex's
  `Local\` — opened `CurrentUserOnly` on both sides. The running instance listens in the
  background (`SingleInstance.Listen`) and posts each message to the UI thread
  (`TrayApplicationContext.Launched`: show the window, then `MainForm.AddLaunchFiles`); a later
  launch connects (3 s timeout), writes one full path per line, and exits.
- Windows' foreground lock: the later launch reads the running process' id from the pipe
  (`GetNamedPipeServerProcessId`) and calls `AllowSetForegroundWindow` for it before writing.
- Files handed over before the window was ever shown (a `--tray` start) wait in a list and are
  loaded with the startup files, in `OnShown`.
- If the mutex cannot be created at all, the launch runs alone, without lock nor listening, rather
  than not at all.
- The restore (tray click and hand-over alike) is `ShowWindow(SW_RESTORE)`, which brings a minimized
  window back to the state it had before — maximized or normal.

---

## Documentation

- README § Tray & startup: the line *"Several instances can run side by side, each with its own
  window and tray icon; the last window closed sets the size remembered."* is replaced by the
  single-instance behaviour and the `--new-instance` argument.
- The app's **CLAUDE.md § Launch**: **every** agent launch — the delivery launch and the agent's own
  checks — passes `--new-instance` next to `--title`, so an agent's test never brings back, nor
  loads files into, the user's instance or another session's. The command line shown there gets it;
  a worktree build whose source predates the argument is launched without it (it would load it as a
  file), as for `--title`. The other running sessions of the workspace are told of the change
  (global rule on CLAUDE.md changes).
- README.fr.md mirrors every README change, in the same commit (`../CLAUDE.md` § Repository Docs).
- The app's **RULES.md**, a new row in § *Command-Line Arguments* and a § *Single Instance* below it: the app is single-instance per exe
  location; when the agent needs its own instance while one of the same exe runs (the user's,
  which it must not disturb nor can rebuild over), it launches with `--new-instance`.

---

## Test Impact

The solution holds no test project (`ImageGridFusion.slnx` lists only the app). Nothing testable
by unit tests changes: the behaviour is process-level (mutex, pipe, window activation) and is
verified by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project; process-level behaviour, verified by hand) | — | — |

---

## Open Questions

- [x] ~~Files given to a second launch?~~ → Loaded by the running instance, like a drop
- [x] ~~Instance hidden in the tray?~~ → Shown and brought to the front, like minimized
- [x] ~~Two copies of the exe?~~ → One instance per exe location
- [x] ~~An instance started with `--new-instance`: fully independent, or the instance later normal
      launches bring back?~~ → Fully independent
- [x] ~~Where is the agent-side instruction documented?~~ → README + the app's RULES.md
- [x] ~~A second launch carrying `--tray`?~~ → Leaves the running instance as it is
- [x] ~~A window minimized while maximized?~~ → Restored maximized, the tray click too
- [x] ~~Agent launches (`--title`, sessions side by side) vs the single instance?~~ → Every agent
      launch passes `--new-instance` (app `CLAUDE.md` § Launch)
- [x] ~~A normal second launch carrying `--title`?~~ → Ignored, the running instance keeps its title
- [x] ~~A `--new-instance` launch runs `StartupRegistration.Refresh`: skip it?~~ → Skipped
- [x] ~~A `--new-instance` instance closing writes the remembered window size: skip it?~~ → Skipped,
      the size is still read *(revised 2026-09-30, see Iteration 4: `settings.json`, no more
      registry; `RegistryMigration.Run` skipped too)*

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-27

- Scoping answers: files forwarded and loaded like a drop; the window shown from the tray too;
  one instance per exe location; subject judged straightforward (single scout pass, done directly).
- The user's follow-up request: a launch argument starting a specific instance for tests, when
  the normal one cannot be rebuilt and relaunched — proposed as `--new-instance`, documented.
- Proposed technique: named mutex keyed on the exe path + named pipe hand-over.
- Remaining questions listed above.

### Iteration 2 — 2026-09-30

- `--new-instance` instances are fully independent of the lock.
- Agent-side documentation in the app's RULES.md, user-side in the README.
- A second launch with `--tray` leaves the running instance alone.
- A window minimized while maximized comes back maximized — the tray click included, a change of
  today's behaviour.

### Iteration 3 — 2026-09-30

- Emerged while re-reading the start-up: a `--new-instance` instance neither refreshes the startup
  registration nor saves the window size, so tests never change the user's automatic settings.

### Iteration 4 — 2026-09-30

- The app's settings moved meanwhile (RULES.md § App Settings): `settings.json` next to the exe,
  *Start with Windows* a shortcut in the *Startup* folder, `RegistryMigration.Run` at start-up.
  Iteration 3's two skips still hold on the new storage; wording updated.
- Same principle extended: a `--new-instance` launch skips `RegistryMigration.Run`, an automatic
  write too. Put to the user with the go.

### Iteration 5 — 2026-10-07

- The codebase moved meanwhile: a `--title <text>` argument (the second title, RULES.md
  § Command-Line Arguments), passed by every agent launch (`CLAUDE.md` § Launch) so sessions testing
  side by side tell their windows apart; a French README to keep in step.
- Documentation updated accordingly: `--new-instance` goes in RULES.md's arguments table, README.fr.md
  mirrors the README.
- Two new questions: how agent launches meet the single instance, and what a handed-over `--title`
  does.

### Iteration 6 — 2026-10-07

- Every agent launch passes `--new-instance`, written in the app's `CLAUDE.md` § Launch.
- A `--title` handed over by a second launch is ignored.

### Iteration 7 — 2026-10-07 — ✅ Implemented

- Go given for code and documentation (no unit tests: no test project), in a dedicated worktree
  (`feature/single-instance`, fast-forwarded into `main` and removed at the end).

### Iteration 8 — 2026-10-07 — 🧭 Implementation choices

- No rule broken.
- Pipe name carries the Windows session id, and both ends are `CurrentUserOnly`: pipe names are
  machine-wide, so two users running the same exe would otherwise collide.
- A mutex that cannot be created lets the app run unlocked rather than refuse to start.
- Files handed over before the window was first shown are queued and loaded with the startup files.
- A second launch with `--tray` does not even connect to the running instance.
- The agent rule went into RULES.md as a § *Single Instance* subsection under § *Command-Line
  Arguments*, plus the app's `CLAUDE.md` § Launch.
- Code in two commits: the tray's maximized restore, then the single instance itself (lock,
  hand-over and `--new-instance` share `Program.Main`).
- Checked by hand on the worktree build: a second launch exits in ~0.25 s and leaves one process; a
  window minimized while maximized comes back maximized; a relative file path is loaded; `--tray`
  exits; `--new-instance` runs beside. Not checked: the window taking the foreground, which needs a
  launch by the user (the agent's shell has no foreground right to lend).
- Incident while checking: the test script first targeted the first `ImageGridFusion` process found,
  which was another session's instance (*Rotation avec CTRL+R*): it was maximized then minimized
  once. The script was then pinned to its own process id.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 7 | 2026-10-07 | `SingleInstance`, `Program.Main`, tray restore, `MainForm.AddLaunchFiles` / `savesWindowSize` |
| Unit tests | 1 | 2026-09-27 | Not applicable — no test project, process-level behaviour |
| README | 7 | 2026-10-07 | README.md + README.fr.md; RULES.md § Single Instance; app CLAUDE.md § Launch |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Files given to a second launch: loaded by the running instance, or ignored? | Loaded like a drop | 2026-09-27 |
| 2 | Instance hidden in the tray: does a second launch show it? | Yes, show it | 2026-09-27 |
| 3 | Two copies of the exe: one instance for all, or one per location? | One per location | 2026-09-27 |
| 4 | Subject straightforward or tricky / long? | Straightforward | 2026-09-27 |
| 5 | *(user, unprompted)* | Add a launch argument starting a specific instance for tests, when the normal one cannot be rebuilt and relaunched; document it | 2026-09-27 |
| 6 | `--new-instance` instance: fully independent, or the one later launches bring back? | Fully independent | 2026-09-27 |
| 7 | Where is the agent-side instruction documented? | README + the app's RULES.md | 2026-09-27 |
| 8 | Second launch with `--tray`: leave the running instance as is, or show it? | Leave it as is | 2026-09-27 |
| 9 | Window minimized while maximized: restored maximized or normal? | Maximized, like before | 2026-09-27 |
| 10 | `--new-instance`: skip the startup registration refresh? | Yes, skip it | 2026-09-30 |
| 11 | `--new-instance`: skip saving the window size? | Yes, skip it | 2026-09-30 |
| 12 | Agent launches (`--title`, sessions side by side) vs the single instance? | Every agent launch passes `--new-instance` | 2026-10-07 |
| 13 | Normal second launch with `--title`: change the running title, or ignore it? | Ignored | 2026-10-07 |

---

*Last updated: 2026-10-07*
