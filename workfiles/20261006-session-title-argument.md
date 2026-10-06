# Session Title Argument

> Working document — a command-line argument adding a second title to the window, so that
> several instances launched side by side by Claude Code sessions tell which implementation each
> one tests.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

While several implementations are debugged at once — one Claude Code session per worktree, each
launching its own build of the app — the windows all read *Image Grid Fusion*: nothing tells which
one tests what. The app gains a command-line argument carrying a **second title**, shown in the
window's title bar, and a rule makes Claude Code pass the **current session's name** in it at
every launch.

Relevant components:

- `src/ImageGridFusion/Program.cs` — parses the arguments: `--tray` (`TrayApplicationContext.HiddenArgument`)
  is taken out, every other argument is a file to load.
- `src/ImageGridFusion/UI/MainForm.cs:288` — `Text = "Image Grid Fusion";`, the only place the window
  title is set; nothing changes it at runtime.
- `src/ImageGridFusion/UI/TrayApplicationContext.cs` — the tray icon's tooltip, *Image Grid Fusion*,
  given the second title on a line of its own (see § Display).
- No single-instance mechanism exists: every launch is an independent process, so several
  instances with different titles can run side by side as they are.

---

## Command-Line Argument

- Syntax: `--title <text>` (`MainForm.TitleArgument`) — the option followed by its value as the
  **next argument**, like `--tray` is matched (case-insensitive option name).
- Parsed in `Program.Main`, the value handed to `MainForm(string[] args, string? secondTitle)`.
- The option and its value are **taken out of the file list**: neither is loaded as a file.
- Combines freely with `--tray` and with files, in any order:
  `ImageGridFusion.exe --title "Undo / redo" a.png b.mp4`.
- Edge cases:
  - `--title` as the last argument, without a value → ignored, the window keeps its plain title.
  - `--title` followed by an option (`--tray`, `--title`) → no value: that option keeps its own
    meaning.
  - A value that is empty or only blanks → ignored, plain title.
  - The value is trimmed.
  - `--title` given twice → the last one wins.
- Not persisted: the title lives for the process only, never written to `settings.json`.
- The *Start with Windows* shortcut keeps launching with `--tray` only.

---

## Display

- **Window title bar only** — so the taskbar button and Alt+Tab show it too, Windows reading the
  same text.
- Format: `Image Grid Fusion — Undo / redo` — the app's name first, the second title after an
  em dash.
- Without the argument, the title stays `Image Grid Fusion`, unchanged.
- The **tray icon's tooltip** shows it too, on a **second line**: `Image Grid Fusion` then
  `Undo / redo` below it. Without the argument, the tooltip stays `Image Grid Fusion` alone.
- Windows caps a tray tooltip (127 characters in .NET): a second title too long for it is cut,
  ending with `…`; the window title keeps it whole.

---

## Launch Rule

Claude Code launches the app with `--title "<current session name>"` at every launch — the
delivery launch and the agent's own checking launches alike.

- The session name is read with the session tool on `self` (`get_session`) **at launch time**, so a
  renamed session is honoured.
- Its **status marker is removed** (`🏗️`, `✅`, `❓`, `🚦`, `⏳`): it changes while the app runs and
  would soon be wrong. `🏗️ Undo / redo` → `Undo / redo`.
- When the session name cannot be read, the app is launched without `--title`, and the report says
  so.
- Written in three places, by the user's answer *"both + a CLAUDE.md specific to the current
  project"*:

  | File | Versioned | Holds |
  |---|---|---|
  | `../CLAUDE.md` (mini-apps, § Launch After Delivery) | ❌ — the mini-apps root is not a git repository | The principle, for every app accepting `--title`: pass the session name, marker removed |
  | `CLAUDE.md` (this app) | ✅ | The launch command for this app, with `--title` |
  | `RULES.md` (this app) | ✅ | The app rule: the command-line arguments (`--tray`, `--title`, files), how the second title is shown |

- A build older than the argument (a worktree branched before it) would load `--title` and the
  name as two files: such a build is launched **without** it, and the report says so (app
  `CLAUDE.md` § Launch).
- The memory `launch-after-implementation` (auto-memory of this project) gets the same addition, so
  it never contradicts the rule.
- Changing a CLAUDE.md triggers the user's *Rule changes* procedure: the other running sessions of
  the workspace are messaged to re-read it.

---

## Documentation

- `README.md`: a *Second title* bullet in § Tray & startup, next to the instances running side by
  side.
- `RULES.md`: a § Command-Line Arguments section (`--tray`, `--title`, files), before § App Settings.
- `GLOSSARY.md`: a *Second title* (*titre secondaire*) entry.

---

## Test Impact

The repository holds **no test project**: nothing testable by unit tests is created or updated.
The behaviour is checked by hand in the launched app — which the launch rule itself exercises at
every delivery.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|

---

## Open Questions

- [x] ~~Syntax: `--title "<text>"` (value as the next argument, like `--tray`) or `--title="<text>"`
      (one argument)?~~ → `--title "<text>"`, the value as the next argument
- [x] ~~Order in the title bar: `Undo / redo — Image Grid Fusion` (distinctive part first, survives
      the taskbar's truncation) or `Image Grid Fusion — Undo / redo`?~~ → `Image Grid Fusion — Undo / redo`
- [x] ~~The split of the launch rule between `../CLAUDE.md`, this app's `CLAUDE.md` and `RULES.md`
      (§ Launch Rule) — is that the intended reading of "both + a project-specific CLAUDE.md"?~~ → Yes,
      that split
- [x] ~~The edge cases of § Command-Line Argument (missing / blank value ignored, last one wins) —
      agreed?~~ → Agreed

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-06

Initial design from the scoping answers (Q1–Q4) and one scout pass over the code: a `--title <text>`
argument parsed in `Program.cs` next to `--tray`, shown in the window title bar only
(`Undo / redo — Image Grid Fusion`), not persisted; a launch rule passing the session name, status
marker removed, written in the shared `../CLAUDE.md`, this app's `CLAUDE.md` and `RULES.md`.

### Iteration 2 — 2026-10-06

Open questions settled (Q5–Q8): the syntax `--title "<text>"` and the edge cases are kept as
proposed, the rule split across the three files confirmed; the title bar order is **reversed** from
the proposal — `Image Grid Fusion — Undo / redo`, the app's name first.

### Iteration 3 — 2026-10-06 — ✅ Implemented

Go given: code, unit tests and documentation. Run on `main` (standing choice of this repository, no
branch question asked). No test project exists: the unit-test step does not apply.

---

### Iteration 4 — 2026-10-06 — 🧭 Implementation choices

- **Branch**: stayed on `main` without the Branch Gate question — the standing choice of this
  repository (memory *work-on-main-only*).
- **`--title` followed by an option** (`--tray`, `--title`): taken as a missing value, so that option
  keeps its meaning — the frozen design did not say.
- **Older builds**: the app `CLAUDE.md` § Launch passes `--title` only to a build whose source has
  `MainForm.TitleArgument`; an older one (a worktree not yet rebased) would load the option and the
  name as files. Not in the frozen design; added because three parallel sessions run older worktree
  builds.
- **Names and placement**: `MainForm.TitleArgument` next to `TrayApplicationContext.HiddenArgument`'s
  role, a private `MainForm.AppTitle` constant, the title as an optional second constructor parameter;
  `README.md` bullet in § Tray & startup; `RULES.md` § Command-Line Arguments before § App Settings.
- **Rule propagation**: the three other running sessions of the workspace (*Déplacement d'image au
  clavier*, *Gérer CTRL+Z pour annuler*, *Zoom molette pas de 5%*) were messaged to re-read both
  `CLAUDE.md`; no title needed correcting.
- No rule broken.

---

### Iteration 5 — 2026-10-06 — ⚙️ Post-implementation — Tray tooltip

The user asked whether the tray icon's tooltip showed the second title — it did not, by the design
of Q1 (window title bar only) — and asked to add it, **on a line of its own** below the app's name.
§ Display updated: the tooltip reads `Image Grid Fusion` + line break + the second title, cut with
`…` past Windows' tooltip cap.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 5 | 2026-10-06 | `Program.cs`, `MainForm.cs` — commit 6b94297; the tray tooltip — 23e65bd |
| Unit tests | 3 | 2026-10-06 | No test project — not applicable |
| README | 3, 5 | 2026-10-06 | § Tray & startup — commit 6034877; the tray tooltip, `README.fr.md` too — d0f2e5c |
| GLOSSARY / RULES / CLAUDE.md | 3, 4, 5 | 2026-10-06 | c2b6199, 69842a4, 54359bb; `../CLAUDE.md` (not versioned) and the launch memory updated |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where should the second title appear? | Window title bar | 2026-10-06 |
| 2 | Where should the launch rule be written? | Both (shared `CLAUDE.md` + `RULES.md`) + a CLAUDE.md specific to the current project | 2026-10-06 |
| 3 | The session title may carry a status marker: what to pass? | Remove the marker | 2026-10-06 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-06 |
| 5 | Syntax: `--title "<text>"` or `--title="<text>"`? | `--title "<text>"` | 2026-10-06 |
| 6 | Order in the title bar? | `Image Grid Fusion — Undo / redo` | 2026-10-06 |
| 7 | The split of the launch rule between the three files? | Yes, that split | 2026-10-06 |
| 8 | The edge cases (missing / blank value ignored, last one wins)? | Agreed | 2026-10-06 |
| 9 | *(user)* Does the tray tooltip show the second title? | No — added on a second line (Iteration 5) | 2026-10-06 |

---

*Last updated: 2026-10-06*
