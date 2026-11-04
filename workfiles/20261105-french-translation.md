# French Translation

> Working document — the app's UI in French, chosen in a ⚙ menu setting, applied at the next start.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app is English only: every user-visible text is a literal hard-coded in C# — no `.resx`, no
`*.Designer.cs`, no `CurrentUICulture` anywhere (roughly 300 to 500 texts, most of them in
`UI/MainForm*.cs`, `UI/FileExplorerPanel.cs`, `UI/ComponentsWindow.cs`, and a few in
`Composition/` and `Components/`).

The work:

- a **UI language** app setting — **English** (the default) or **Français** — kept in
  `settings.json`, picked from a **⚙ submenu**;
- applied at the **next start**: picking a language saves it and offers to restart now;
- every user-visible text moved behind one lookup, with its French translation;
- the shortcuts window showing `SHORTCUTS.fr.md` in French.

---

## UI Language Setting

- **`AppSettings.Language`**: `"en"` or `"fr"`, stored by name under `Language` in `settings.json`;
  missing, unreadable or unknown → `"en"`. Saved by `AppSettings.SaveLanguage`, the save error
  reported in the status line like the other settings (`AppSettings.IsSaveError`).
- **Applied once, at start-up**: `Program.Main` sets `CultureInfo.DefaultThreadCurrentUICulture`
  and `CultureInfo.CurrentUICulture` from it before any form is created (next to
  `ImageLook.MaxZoom = …`), so the background tasks get it too. Only the **UI** culture changes:
  `CurrentCulture` (sorting, `ToUpper`) stays the system's, as today.
- **The ⚙ submenu** mirrors *Video size*: a `ToolStripMenuItem` field, one entry per language
  (`Tag` = its code), checked in `MainForm.ShowSettings` as the menu opens, its click calling
  `MainForm.SetLanguage`. Each language is named **in its own language** — `English`, `Français` —
  whatever the UI language. Its icon 🌐 (`UiIcons`, RULES.md § Icons). Label: see Open Questions.
- **Picking the language already in force** does nothing.
- **Picking another one** saves it, then asks *Restart now?* — in the language just picked — :
  - **Yes** → the existing restart route (`IComponentsHost.Restart` in `UI/MainForm.Components.cs`,
    extracted into a `MainForm.RestartApp` both callers use): `--new-instance` and `--title` carried
    over, the old instance waited for before the single-instance lock;
  - **No** → the status line says the language applies at the next start.
- A `--new-instance` instance writes the setting on the user's pick, like every user action.
- Out of the undo history, like every app setting.

---

## Translated Texts

### In scope

Everything the app shows that it wrote itself:

- menus (⚙, Copy ▾ / Save ▾, every context menu), buttons, tabs, labels, check boxes, drop-down
  entries, tooltips — the tile tooltip's fixed parts included;
- the status line, message boxes, dialog titles and texts (`NameDialog`, `EffectPresetDialog`,
  the Upscale window, the Component updates window and its balloons);
- the badges and texts the preview or a control draws (*Cutting out…*, *Not upscaled*, *Drop an
  image here*, *Loading…*, *nothing close enough*, `Similar to <name>`…);
- the descriptions the domain builds for display — an effect preset's lines (`LookPreset.Describe`),
  the Cutout's summary (`CutoutEffect`), the Frames speed text — and the component installer's
  progress texts (`Components/ComponentInstaller`);
- the list sections' titles (*Other folders*, *Named files*, *Path*, *Content*…) and the name
  patterns' names and tooltips.

### Out of scope

- The user's content: file and folder names, texts typed into cells and layers, person names,
  preset descriptions, the user's vocabulary.
- **The app's name**, `Image Grid Fusion`, and the second title.
- Product and format names: Twitter, X, PNG, MP4, GIF, JPEG, Real-ESRGAN, yt-dlp, winget…
- The keys' names (`Ctrl`, `Shift`, `Space`…).
- What is written to disk for the app or the agents: `errors.log`, the Debug memory logs,
  `settings.json` keys, `components.json`, the `fusion-…` file names, the folder names.
- The vision tags (already bilingual) and the captions (Florence-2 writes English).
- The docs besides `SHORTCUTS.fr.md` — see Open Questions.

### Terminology

The French texts use the French terms of [GLOSSARY.fr.md](../GLOSSARY.fr.md) — *Fond*,
*Recadrage*, *Détourage*, *Agrandissement*, *Disposition*, *Cellule*, *Calque*… A UI term it does
not give is chosen by the run and added to the French glossary when it is a glossary term.

### Lookup

One lookup for every text, the French one beside its English one — the mechanism is an Open
Question. In either case:

- a text is looked up **once per place it is shown**, when the control or the message is built:
  no text is cached in a `static readonly` field initialised before `Program.Main` sets the culture;
- composite texts are **format strings with numbered placeholders** (`"{0} files"`), never
  fragments glued together, so the French word order is free;
- plurals keep the app's current way (one string per form where the code already has two).

---

## Shortcuts Window

- `SHORTCUTS.fr.md` is **embedded** next to `SHORTCUTS.md` (`ImageGridFusion.SHORTCUTS.fr.md`);
  `ShortcutList.Load` reads the French one when the UI language is French. Same structure, same
  parser (`ShortcutList.Parse`, `##` sections, the header row skipped).
- Its sentence saying the app shows the list in English is rewritten: the app shows this list when
  its language is French.

---

## Numbers and Units

Today the displayed numbers use `InvariantCulture` (`12.5 s`, `0.0 %`, `×1.25`) and the file sizes
French units in every language (`412 Ko`, `Explorer/FileSizes.cs`). What changes with the UI
language is an Open Question.

---

## Rules and Docs

- **RULES.md** gains a § **UI Language**: one lookup for every user-visible text; a new text is
  added **in both languages in the same commit**; the out-of-scope list; the culture set once in
  `Program.Main`; `SHORTCUTS.fr.md` embedded and kept in step (already required by § Keyboard
  Shortcuts).
- **GLOSSARY.md / GLOSSARY.fr.md**: a *UI language* (*langue de l'interface*) row.
- **README.md / README.fr.md**: the setting mentioned where the ⚙ menu's settings are.

---

## Test Impact

No test project in this repository (CONTRIBUTING.md: *check a change by hand in the running app*):
no unit test is created or updated. The run checks by hand:

| Behaviour to check | Where | Create / Update |
|---|---|---|
| No `Language` in `settings.json` → English UI | App launched fresh | Check by hand |
| ⚙ › language submenu: the language in force checked, each named in its own language | ⚙ menu | Check by hand |
| Picking Français → saved, *Restart now?* in French; Yes restarts in French, `--title` kept | ⚙ menu | Check by hand |
| Picking the language in force → nothing | ⚙ menu | Check by hand |
| French UI: menus, tabs, options, tooltips, status line, dialogs, badges, file explorer, Component updates window, Upscale window in French | Whole app | Check by hand |
| French UI: the shortcuts window shows `SHORTCUTS.fr.md` | F1 | Check by hand |
| Back to English after a restart: nothing left in French | Whole app | Check by hand |

---

## Open Questions

- [ ] What carries the texts and their translations: `.resx` resources (`Strings.resx` +
  `Strings.fr.resx`, a strongly typed class generated by MSBuild, a `fr\` satellite assembly next to
  the exe), or a hand-written C# class holding each English text beside its French one?
- [ ] Numbers and units in French: decimal comma (`12,5 s`) and the file sizes in the language's
  units (`412 Ko` in French, `412 KB` in English), or the point kept everywhere, or nothing changed?
- [ ] The ⚙ submenu's label: always bilingual (`Language / Langue`), so it is found whatever the
  language in force, or translated (`Language` / `Langue`)?
- [ ] The French docs quote the UI labels in English (shared rule, `../CLAUDE.md` § French
  Versions). With a French UI: `SHORTCUTS.fr.md` — now shown by the app — quotes the French labels
  and `README.fr.md` / `GLOSSARY.fr.md` keep the English ones; or every French doc moves to the
  French labels; or nothing changes?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design, from the scoping batch (English by default, a ⚙ submenu, applied at the next start
with a restart offered, a straightforward subject) and one scout pass over the code: no
localization infrastructure exists, every text is a literal in code, the ⚙ menu's *Video size*
submenu and the Component updates window's restart route are the patterns to reuse,
`SHORTCUTS.fr.md` already parses with `ShortcutList.Parse`. Four Open Questions left to the user:
the lookup mechanism, numbers and units, the submenu's label, the French docs' labels.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — checks by hand (see Test Impact) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | First launch (no language in `settings.json`): which language? | English | 2026-11-04 |
| 2 | Where is the language chosen in the UI? | A submenu of the ⚙ menu | 2026-11-04 |
| 3 | What happens when the language changes? | Applied at the next start; the app offers to restart | 2026-11-04 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-11-04 |
| 5 | What carries the texts: `.resx` or a hand-written C# class? | | |
| 6 | Numbers and units in French? | | |
| 7 | The ⚙ submenu's label: bilingual or translated? | | |
| 8 | The French docs' UI labels with a French UI? | | |

---

*Last updated: 2026-11-05*
