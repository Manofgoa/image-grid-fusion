# Shortcuts List

> Working document — every keyboard shortcut and modifier / wheel gesture listed in a `SHORTCUTS.md`
> shown on GitHub, the same list shown in the app, and a rule keeping it complete.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app has some sixty shortcuts, spread over the README's prose (some not mentioned at all: Ctrl+R,
Ctrl+Shift+R, PageUp / PageDown / Home / End on the tiles, Down from the search box). This work gives
them **one place**:

- **`SHORTCUTS.md`** at the repository root, with its French version **`SHORTCUTS.fr.md`** (shared
  `../CLAUDE.md` § French Versions): the shortcuts grouped by where they work, each with what it does.
  Linked from `README.md` / `README.fr.md`.
- **In the app**, a **Keyboard shortcuts** window showing the shortcuts and what they do, read from
  `SHORTCUTS.md` **embedded in the exe** — one source for GitHub and the app.
- **A rule** in `RULES.md`: every shortcut added, changed or removed updates `SHORTCUTS.md` and
  `SHORTCUTS.fr.md` in the same commit as the code.

Components: `SHORTCUTS.md`, `SHORTCUTS.fr.md`, `ImageGridFusion.csproj` (embedded resource), a new
`UI/ShortcutsWindow.cs` and its parser, `MainForm` (⚙ menu entry, key), `RULES.md`, `README.md` /
`README.fr.md`, `GLOSSARY.md` / `GLOSSARY.fr.md`.

---

## Scope

| In the list | Out of it |
|---|---|
| Keys and key combinations (Ctrl+Z, Space, F2, arrows…) | Plain drags and plain clicks (moving an image, the ✥ handle, dropping a file) |
| Wheel gestures, with or without a modifier (wheel, Ctrl / Alt / Shift + wheel) | Context menus opened without a key |
| A modifier held during a drag or a click (Shift ignoring the stops, Shift / Ctrl + click in Pick) | |
| Double-clicks with a meaning (separator reset, layer edit view, tile activation) | |
| **Windows' own behaviour** in the app's windows and controls — Tab / Shift+Tab, Enter / Escape in a dialog, the plain wheel scrolling a list or the layout strip, Alt+F4, a text field's editing keys — each in the section where it works, the ones valid everywhere in a last **Windows** section | |

---

## SHORTCUTS.md — Format

The file is read by people on GitHub **and parsed by the app**, so its shape is a contract:

- Line 1: the **language line** (shared rules § French Versions), ignored by the app.
- `# Keyboard Shortcuts — Image Grid Fusion`, then one short sentence linking to the README; ignored
  by the app.
- One **`## <Where it works>`** heading per context — a **section** of the window.
- Under each, one table **`| Shortcut | Does |`**: a row per shortcut, its keys first, what it does in
  one short line. A condition that matters (*not while exporting*, *a layer selected*) is in the
  *Does* cell.
- **Keys written as `<kbd>`**: `<kbd>Ctrl</kbd>+<kbd>Z</kbd>`, `<kbd>Wheel</kbd>`, `<kbd>Ctrl</kbd>+<kbd>Wheel</kbd>`
  — key caps on GitHub, drawn as key caps in the window; the text between them (`+`, `/`, `,`, *held
  while dragging an image*) shown as written.
- Nothing else is read by the app: a paragraph between the tables shows on GitHub only.

The French file has the **same sections, same rows, same order**; its keys are the same (Ctrl, Shift,
Alt — the keyboard's labels), its headings and *Does* cells in French, with the glossary's French terms.

---

## Shortcut Inventory

The inventory lives in **`SHORTCUTS.md`** itself (§ One place per fact): eleven sections — Anywhere,
Selected Image, Mouse Over the Grid, Crop and Blur Bars, Layers, Pick and Eyedropper, Options, File
Explorer, Search Box and URL Zone, Dialogs, Windows — every row read from the code on 2026-10-29. Beside the
exploration's first list, the code gave: **Ctrl+C** copying the selected cell or layer alone (the grid
with nothing selected), **Ctrl+Shift+C** copying the grid, **Ctrl held while dragging a layer** leaving a
copy, and the shortcuts window's own keys.

---

## Shortcuts Window

- Title **Keyboard shortcuts**, opened from the **⚙ menu** (*Keyboard shortcuts…*, before *Open app
  folder*) and by **F1** (`MainForm.ProcessCmdKey`, before the text-field guard: F1 means nothing to a
  text field).
- **Modeless**: it stays open beside the main window while the shortcuts are tried; one instance —
  opened again, it comes back to the front. Escape closes it.
- Shows **only** the sections and their rows — the headings, then per row the keys and what they do;
  the language line, the title and the paragraphs are left out.
- Content read from **`SHORTCUTS.md` embedded in the exe** — the English file only, like the whole
  UI: linked from the repository root in
  `ImageGridFusion.csproj` as `<EmbeddedResource Include="..\..\SHORTCUTS.md"
  LogicalName="ImageGridFusion.SHORTCUTS.md" />`, read like `Explorer/VisionTags.txt`
  (`GetManifestResourceStream`, UTF-8).
- **No new package**: a small parser of its own (`##` headings, `|` table rows, the header and
  separator rows skipped, `<kbd>` tags read as keys, `*…*` and `` `…` `` stripped to their text). The
  app has no Markdown renderer and avoids heavy dependencies.
- Drawn with `SystemColors`, sized with `LogicalToDeviceUnits` (no theme in the app), on the model of
  `UI/NameDialog.cs` but **resizable**, the list scrolling: per section its heading in bold, per row
  the keys as key caps (a rounded box per `<kbd>`) in a left column as wide as the widest, what it
  does on the right, wrapped.
- Each **section has its own keys' column** — the widest of its rows, at most 45 % of the width — so a
  long row elsewhere does not push every description away; a `+` glued to a word (`<kbd>Ctrl</kbd>+arrow`)
  is drawn apart, as between two keys. The measures are taken once per font.
- Its own keys: Escape closes it; ↑ ↓, PageUp PageDown, Home End scroll it — listed in `SHORTCUTS.md`
  § Dialogs like any other.
- The main window hidden in the tray, it closes: owned, it would otherwise stay alone on screen.
- Out of the undo history; nothing persisted.

---

## Rule — Every Shortcut Documented

A new section of `RULES.md`, **§ Keyboard Shortcuts**:

- `SHORTCUTS.md` lists **every** shortcut of the app — keys, wheel gestures, a modifier held during a
  drag or a click, double-clicks with a meaning — in the section of where it works.
- A shortcut **added, changed or removed** updates its row in `SHORTCUTS.md` **and**
  `SHORTCUTS.fr.md` **in the same commit** as the code: a commit changing a key handler
  (`ProcessCmdKey`, `OnKeyDown`, `OnMouseWheel`, `ModifierKeys`, a double-click) without them is
  incomplete.
- The file's format is the app's contract (§ SHORTCUTS.md — Format above): a row the parser cannot
  read does not show in the window.
- **One place per fact**: the README and the other docs never list keys — they link to the section of
  `SHORTCUTS.md` (`SHORTCUTS.fr.md` from a French doc) where they are.
- Enforced by the rule alone: no hook, no test.

---

## README

`README.md` and `README.fr.md` are **trimmed** of their shortcut mentions (§ One place per fact):

- A sentence whose purpose is to give a key or a wheel gesture is **replaced by a link** to the
  `SHORTCUTS.md` section where it lives (`SHORTCUTS.fr.md` from the French README); a feature's
  description keeps saying what the feature does, without the keys.
- A **link to `SHORTCUTS.md`** near the top (after the glossary's line), and a mention of the window
  (F1, ⚙ menu).
- The README stays readable alone: what a feature does is said there, how to trigger it by key is in
  `SHORTCUTS.md`. The **wheel stays named** where it is the feature's own gesture (the zoom, the crop's
  zone); its modifiers and the step sizes go to `SHORTCUTS.md` (the tile size's 15 % a notch moved there).
- The glossary names the window's key through a link too.

---

## Glossary

A new term in `GLOSSARY.md` and `GLOSSARY.fr.md`: **Shortcuts window** (*fenêtre des raccourcis*) — the
window listing every keyboard shortcut and gesture of the app and what it does, read from
`SHORTCUTS.md`; opened by F1 or the ⚙ menu, modeless.

---

## Test Impact

**No unit test**: the repository has no test project (`CONTRIBUTING.md`: *check a change by hand in
the running app*), and this work creates none (the rule is enforced by itself, Q&A 5).

Checked by hand in the running app:

| Behaviour to check | How |
|---|---|
| The window lists every section and row of `SHORTCUTS.md`, keys as key caps | Open it from ⚙ and by F1, compare |
| The language line, the title and the paragraphs do not show | Same |
| Modeless; opened again, it comes back to the front; Escape closes it | Use the app with it open, press F1 again |
| F1 works in a text field and breaks nothing there | Press it in the search box, in a Frames field |

---

## Open Questions

- [x] ~~What enforces the rule beyond `RULES.md`: the rule alone, or also a Claude Code hook?~~ → The
      rule alone
- [x] ~~Which key opens the window?~~ → F1, plus the ⚙ menu entry
- [x] ~~Is the window modeless or modal?~~ → Modeless
- [x] ~~How are the keys written in `SHORTCUTS.md`?~~ → `<kbd>Ctrl</kbd>+<kbd>Z</kbd>`, drawn as key caps
      in the window
- [x] ~~Which file does the app embed?~~ → `SHORTCUTS.md`, English, like the whole UI
- [x] ~~The README's shortcut mentions: kept, or trimmed?~~ → Trimmed, the README linking to
      `SHORTCUTS.md` (§ README)
- [x] ~~Windows' own behaviour listed?~~ → Yes, all of it (§ Scope)
- [x] ~~A glossary term for the window?~~ → Yes, *Shortcuts window* / *fenêtre des raccourcis*
- [ ] `GLOSSARY.md` / `.fr.md` and `RULES.md` still name keys in several rows: trim them to links to
      `SHORTCUTS.md` too, as the rule's *one place per fact* now asks? *(raised by the run, Iteration 5)*

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Request: a list of the shortcuts in a `.md` shown on GitHub, and in the app a readme with just the
shortcuts and what they do.

Scoping answers (Q&A 1–4): `SHORTCUTS.md` + `SHORTCUTS.fr.md` linked from the README; in the app, a
window showing the embedded `SHORTCUTS.md`; keys plus wheel and modifier gestures and meaningful
double-clicks; a simple subject, one scout pass.

Exploration: about sixty shortcuts inventoried (§ Shortcut Inventory) — nearly all keys in
`MainForm.ProcessCmdKey`, the others in `GridPreview`, `ThumbnailGrid`, `FileExplorerPanel`,
`FrameField`, `StepSlider`, `EffectPresetDialog`, `UrlZone`. No Markdown renderer and no help entry in
the app; F1 free; embedded resources already used (`VisionTags.txt`, `app.ico`), a root file linkable
with a `LogicalName`. Shift+arrows, suspected of losing Shift, checked: `ArrowDirection` masks the
modifiers to read the arrow only, Shift reaches `KeyPan`.

Proposal: § SHORTCUTS.md — Format, § Shortcuts Window.

### Iteration 2 — 2026-10-29

Request: add a rule to the project so that every shortcut is documented in `SHORTCUTS.md`
automatically. → § Rule — Every Shortcut Documented, written into `RULES.md` at implementation; how it
is enforced beyond the rule left as an Open Question.

### Iteration 3 — 2026-10-29

Open Questions answered (Q&A 5–12): the rule alone; F1 and the ⚙ menu; a modeless window; keys as
`<kbd>`, drawn as key caps; the English file embedded; the README trimmed to links (§ README, and the
rule's *one place per fact*); Windows' own behaviour listed too (§ Scope, § Windows); a glossary term.
No question left: the design is complete.

### Iteration 4 — 2026-10-29 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`feature/shortcuts-list`). The scope is
frozen as the sections above stand.

### Iteration 5 — 2026-10-29 — 🧭 Implementation choices

No project rule broken.

- **Inventory checked against the code**, not the exploration's list alone: Ctrl+C (the selection alone),
  Ctrl+Shift+C (the grid) and Ctrl + drag of a layer (a copy) added. The inventory section now points to
  `SHORTCUTS.md` rather than repeating it.
- **The window's own keys** — Escape, ↑ ↓, PageUp PageDown, Home End scrolling — added, and listed in
  `SHORTCUTS.md` § Dialogs per the new rule. Space does not scroll it.
- **A keys' column per section**, at most 45 % of the width, instead of one for the whole list: one long
  row (Pick, Windows) pushed every description to the middle. Measures cached per font; a glued `+`
  drawn apart.
- **The window closes when the main window hides** to the tray, rather than staying alone on screen.
- **Parser**: `UI/ShortcutList.Parse` — `##` headings, table rows, header and separator skipped, `<kbd>`
  read as keys, `*` and `` ` `` stripped, HTML entities decoded; `SHORTCUTS.md` linked into the csproj
  with `Link` so it shows in the IDE.
- **README trimming**: the wheel kept named where it is the feature's gesture, its modifiers and step
  sizes moved out; *as `Ctrl+C` in the Explorer* became *as Copy in the Explorer*; the tile size's 15 % a
  notch moved into `SHORTCUTS.md`. 40 lines changed in each language, in step.
- **Glossary**: the window's key linked, not named, per the rule's *one place per fact*.
- **Not done — out of the frozen scope**: `GLOSSARY.md` / `.fr.md` and `RULES.md` still name keys in
  several rows (the undo history's Ctrl+Z, the layer edit view's Escape…); the rule now says the other docs
  link instead. Offered as an Open Question.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 5 | 2026-10-29 | `UI/ShortcutList.cs`, `UI/ShortcutsWindow.cs`, F1 and the ⚙ entry in `MainForm`, the embedded resource |
| Unit tests | 4 | 2026-10-29 | None — no test project in the repository; checked in the running app (F1 opens it in ~70 ms, Escape closes it) |
| SHORTCUTS.md / SHORTCUTS.fr.md | 4, 5 | 2026-10-29 | Eleven sections, both languages in step |
| RULES.md | 4 | 2026-10-29 | § Keyboard Shortcuts |
| README | 4, 5 | 2026-10-29 | Trimmed to links, both languages |
| Glossary | 4, 5 | 2026-10-29 | *Shortcuts window* / *fenêtre des raccourcis* |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In what form does the list appear on GitHub? | `SHORTCUTS.md` + `SHORTCUTS.fr.md`, linked from the README | 2026-10-29 |
| 2 | How does the app show the list? | A window showing `SHORTCUTS.md` embedded in the exe | 2026-10-29 |
| 3 | Which shortcuts are listed? | Keys, wheel and modifier gestures, meaningful double-clicks | 2026-10-29 |
| 4 | How deep is the exploration? | Simple — one scout pass | 2026-10-29 |
| 5 | What enforces the rule beyond `RULES.md`? | The rule alone | 2026-10-29 |
| 6 | Which key opens the window? | F1 (plus the ⚙ menu) | 2026-10-29 |
| 7 | Modeless or modal window? | Modeless | 2026-10-29 |
| 8 | Keys written as `<kbd>` or plain text? | `<kbd>` | 2026-10-29 |
| 9 | Which file does the app embed? | `SHORTCUTS.md`, English | 2026-10-29 |
| 10 | The README's shortcut mentions: kept, or trimmed to a link? | Trimmed to links to `SHORTCUTS.md` | 2026-10-29 |
| 11 | Windows' own behaviour listed? | Yes, all of it | 2026-10-29 |
| 12 | A glossary term for the window? | Yes | 2026-10-29 |

---

*Last updated: 2026-10-29*
