# Image Grid Fusion

App-level agent instructions, on top of the rules shared by every mini-app (`../CLAUDE.md`).

@RULES.md
@GLOSSARY.md

## Launch

Every launch of the app by an agent — the delivery launch of `../CLAUDE.md` § Launch After Delivery
and the agent's own checking launches alike — passes **`--new-instance`** and the **session's name**
as the second title (RULES.md § Command-Line Arguments), so instances launched side by side by
parallel sessions run apart and tell which implementation each one tests:

```bash
src/ImageGridFusion/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGridFusion.exe --new-instance --title "<session name>" [test files…]
```

- **`--new-instance`, always**: the app runs once per exe (RULES.md § Single Instance) — a launch
  without it would only bring back the instance already running from that exe, the user's or another
  session's, and load the test files into it. With it, the agent's instance stands apart and writes
  none of the user's settings of its own accord.

- The session's name is read **at launch time** with the session tool on `self`, so a renamed
  session is honoured.
- Its **status marker is removed** (`🏗️`, `✅`, `❓`, `🚦`, `⏳`): it changes while the app runs.
  `🏗️ Undo / redo` → `--title "Undo / redo"`.
- A worktree's build runs its own exe, with the same arguments — **each one only if its source
  knows it** (`MainForm.TitleArgument`, `SingleInstance.NewInstanceArgument`): a build older than an
  argument would load it as a file. Such a build is launched without it, and the report says so.
- The name cannot be read → launched without `--title`, and the report says so.
- The survival check (`Get-Process ImageGridFusion`) finds the instance by its `MainWindowTitle`,
  `Image Grid Fusion — <session name>`, among the others.
