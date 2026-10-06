# Image Grid Fusion

App-level agent instructions, on top of the rules shared by every mini-app (`../CLAUDE.md`).

@RULES.md
@GLOSSARY.md

## Launch

Every launch of the app by an agent — the delivery launch of `../CLAUDE.md` § Launch After Delivery
and the agent's own checking launches alike — passes the **session's name** as the second title
(RULES.md § Command-Line Arguments), so instances launched side by side by parallel sessions tell
which implementation each one tests:

```bash
src/ImageGridFusion/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGridFusion.exe --title "<session name>" [test files…]
```

- The session's name is read **at launch time** with the session tool on `self`, so a renamed
  session is honoured.
- Its **status marker is removed** (`🏗️`, `✅`, `❓`, `🚦`, `⏳`): it changes while the app runs.
  `🏗️ Undo / redo` → `--title "Undo / redo"`.
- A worktree's build runs its own exe, with the same argument.
- The name cannot be read → launched without `--title`, and the report says so.
- The survival check (`Get-Process ImageGridFusion`) finds the instance by its `MainWindowTitle`,
  `Image Grid Fusion — <session name>`, among the others.
