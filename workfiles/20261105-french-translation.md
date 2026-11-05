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
- **Applied once, at start-up**: `Program.SetUiLanguage`, a **module initializer** — run before
  `Program.Main` and before any type initializer, such as the components' purposes in
  `ComponentRegistry.All` — sets `CultureInfo.DefaultThreadCurrentUICulture` and
  `CultureInfo.CurrentUICulture` from it, so the background tasks get it too. Only the **UI**
  culture changes: `CurrentCulture` (sorting, `ToUpper`) stays the system's, as today.
- **The ⚙ submenu** mirrors *Video size*: a `ToolStripMenuItem` field, one entry per language
  (`Tag` = its code), checked in `MainForm.ShowSettings` as the menu opens, its click calling
  `MainForm.SetLanguage`. Each language is named **in its own language** — `English`, `Français` —
  whatever the UI language. Its icon 🌐 (`UiIcons`, RULES.md § Icons).
- **Its label is bilingual, never translated**: `Language / Langue` in both languages, so it is
  found whatever the language in force.
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
- The ⚙ language submenu's label, bilingual by design.

### Terminology

The French texts use the French terms of [GLOSSARY.fr.md](../GLOSSARY.fr.md) — *Fond*,
*Recadrage*, *Détourage*, *Agrandissement*, *Disposition*, *Cellule*, *Calque*… A UI term it does
not give is chosen by the run and added to the French glossary when it is a glossary term.

The tabs: **Fond, Recadrage, Détourage, Agrandissement, Zoom, Animations, Rotation, Miroir, Lecture**
(Frames), **Couleurs, Flou, Volume**; **Format, Bande son, Fondu, Bordures, Fond de grille, Cascade,
Calques, Raccords** (Seams). The windows: **Agrandissement** (Upscale), **Mises à jour des composants**
(Component updates).

### Persisted Ids

A name that is both an id and a label keeps its id, stored or compared as before, and gains a
displayed name: the built-in text styles (`NamedTextStyle.DisplayName` — *Par défaut, Mème,
Sous-titre, Étiquette, Néon, Discret* —, `TextStyles.IsReservedForUser` refusing them in both
languages), the layouts (`GridLayout.DisplayName`), the name patterns (`NameGroup.Title`, looked up by
`Id`), the views drop-down (`FileExplorerPanel.ModeText`, also given to the accessibility tree).

### Lookup

**`.resx` resources**, the .NET standard:

- `Strings.resx` (English, the neutral culture) and `Strings.fr.resx` (French), in a `Resources/`
  source folder, one entry per text, named after its place and meaning (`SettingsVideoSize`,
  `StatusLanguageNextStart`…);
- a **strongly typed class** generated by MSBuild at build time (`StronglyTypedClassName` on the
  `EmbeddedResource`, no Visual Studio designer needed), so a missing key is a build error;
- the French texts built into a **`fr\ImageGridFusion.resources.dll` satellite assembly** next to
  the exe; a French text missing falls back to the English one (the `ResourceManager`'s own
  fallback).

Whatever the entry:

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
French units in every language (`412 Ko`, `Explorer/FileSizes.cs`). They follow the UI language:

| | English | Français |
|---|---|---|
| Decimal separator | `12.5 s`, `×1.25` | `12,5 s`, `×1,25` |
| File sizes (base 1024, unchanged) | `412 KB`, `2.3 MB`, `GB`, `TB` | `412 Ko`, `2,3 Mo`, `Go`, `To` |

- Only **displayed** numbers change: they are formatted with the UI culture
  (`CultureInfo.CurrentUICulture`), every parse and every serialization — `settings.json`,
  `favorites.json`, presets, `components.json`, the indexes — stays `InvariantCulture`.
- The text fields the user types numbers into (the Custom format's Width / Height, the Frames
  trim's fields) accept the UI language's separator.

---

## Rules and Docs

- **A build check** (`CheckFrenchTexts`, in `ImageGridFusion.csproj`, before the compilation): a
  warning names every key of `Strings.resx` missing from `Strings.fr.resx`, and every key of the
  French file the English one no longer has — the French text can no longer go missing unnoticed.
- **RULES.md** gains a § **UI Language**: one lookup for every user-visible text; a new text is
  added **in both languages in the same commit**; the out-of-scope list; the culture set once in
  `Program.Main`; `SHORTCUTS.fr.md` embedded and kept in step (already required by § Keyboard
  Shortcuts).
- **GLOSSARY.md / GLOSSARY.fr.md**: a *UI language* (*langue de l'interface*) row.
- **README.md / README.fr.md**: the setting mentioned where the ⚙ menu's settings are.
- `README.fr.md`'s effect sections are titled with the French tabs (`### Fond`, `### Lecture`…),
  their anchors following; `SHORTCUTS.fr.md`'s `## Pick et pipette` becomes `## Sélection et pipette`.
- **Every French doc quotes the French UI labels** — `README.fr.md`, `GLOSSARY.fr.md`,
  `SHORTCUTS.fr.md`: `**Copy cell**` becomes the French label the app shows, so a French reader
  finds on screen what the doc says. The English docs keep the English labels.
- **The shared rule changes** — `../CLAUDE.md` § Repository Docs › French Versions, *"the app's UI
  labels (in English) stay as they are"*: the French docs quote the labels **in the language the
  app shows them in French** — French for an app translated into French, English for an app that
  is not. Code, file names, arguments and shortcuts stay as they are. The other mini-apps, all
  English-only, are unchanged by it. `../CLAUDE.md` is outside every app's repository (not
  versioned): edited in place, the change propagated to the running sessions of the workspace
  (global rule *Rule changes*).
- `SHORTCUTS.fr.md`'s and `GLOSSARY.fr.md`'s sentences or rows saying the UI is in English are
  rewritten.

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
| French UI: `12,5 s` in the length readout, `412 Ko` in the Details view; English UI: `12.5 s`, `412 KB` | Transport, file explorer | Check by hand |
| French UI: `settings.json`, presets and indexes still written with a decimal point, read back unchanged | Files next to the exe | Check by hand |
| Back to English after a restart: nothing left in French | Whole app | Check by hand |

---

## Open Questions

- [x] ~~What carries the texts and their translations: `.resx` resources or a hand-written C#
  class?~~ → `.resx`: `Strings.resx` + `Strings.fr.resx`, a strongly typed class generated by
  MSBuild, a `fr\` satellite assembly next to the exe
- [x] ~~Numbers and units in French?~~ → The French way: decimal comma and `Ko` / `Mo` in French,
  decimal point and `KB` / `MB` in English; parsing and serialization stay invariant
- [x] ~~The ⚙ submenu's label: bilingual or translated?~~ → Bilingual, `Language / Langue`, never
  translated
- [x] ~~The French docs' UI labels with a French UI?~~ → Every French doc (README, GLOSSARY,
  SHORTCUTS) quotes the French labels; the shared rule in `../CLAUDE.md` changes to "the labels in
  the language the app shows them in French"
- [x] ~~Out of the frozen scope, offered during the run: a build check comparing the two `.resx`,
  warning when a key has no French text — today a missing French text silently falls back to English?~~
  → Yes, as a post-implementation adjustment (Iteration 5)

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

### Iteration 2 — 2026-11-05

The four Open Questions answered (Q&A 5–8):

- **Lookup**: `.resx` resources with an MSBuild-generated typed class and a `fr\` satellite
  assembly — § Lookup rewritten.
- **Numbers and units**: they follow the UI language — decimal comma and `Ko` in French, decimal
  point and `KB` in English (the file sizes were in `Ko` in every language); displayed numbers only,
  parsing and serialization invariant — § Numbers and Units rewritten.
- **The submenu's label**: bilingual, `Language / Langue`, never translated.
- **The French docs**: every one of them quotes the French UI labels; the shared rule of
  `../CLAUDE.md` § French Versions changes for every mini-app — the labels in the language the app
  shows them in French — § Rules and Docs extended.

No Open Question left.

### Iteration 3 — 2026-11-05 — ✅ Implemented

Go given: **code, unit tests and documentation** — the code, the checks by hand of § Test Impact
(no test project), the READMEs, the French docs, RULES.md, the glossaries and the shared rule of
`../CLAUDE.md`. Scope frozen as the sections above stand.

### Iteration 4 — 2026-11-05 — 🧭 Implementation choices

The run's own decisions, none of them a broken rule:

- **The culture in a module initializer** (`Program.SetUiLanguage`) instead of `Program.Main`: the
  components' purposes are looked up in `ComponentRegistry.All`'s static initializer, which `Main`'s
  first statements reach — a module initializer runs before both.
- **The texts moved by five agents in parallel**, one per area of disjoint files, each writing its
  entries to a file of its own merged into the two `.resx` by a script; keys prefixed by area —
  `Main`, `Panel` (the other `MainForm` partials and dialogs), `Explorer`, `Comp` (components, Upscale
  window, URL zone, tray, error box), `Ui` (strips, preview, domain descriptions), plus `FileSize*` —
  **1 121 entries**. The French docs converted by a sixth agent.
- **Persisted ids kept**, a displayed name added (§ Persisted Ids).
- **Terms the glossary did not give**: Frames → *Lecture*, Seams → *Raccords*, the windows
  *Agrandissement* and *Mises à jour des composants*, the name patterns (*Appareil photo, Captures
  d'écran, Messageries, IA, Noms génériques, Identifiants, Codes courts, Horodatages…*), the text
  layer's *Contour, Ombre, Boîte, Gras, Italique, CAPITALES*, the border styles *Continu, Tirets,
  Pointillé, Double*; the last video *le dernier MP4 / GIF* (masculine).
- **Left as they were**: the Component updates window's dates (`yyyy-MM-dd` in both languages), the
  zoom badge and the position readout (integers, unchanged units), the Start with Windows shortcut's
  description (written to disk), the `errors.log` texts, the X endpoint's `lang=en`.
- **The views drop-down** gives its translated entry text to the accessibility tree (`Format`), which
  read the enum's English name — found by the run's check.
- **Fixed widths**: the Component updates window's main button and Actions column grow with their
  label; the Grid background's second-color drop-down widened to 150 for *Auto, dernière cellule*.
  **Not measured on screen**: the Component updates window's column headers (*Mise à jour le*,
  *Installée le* may end in `…`), the explorer's sort and views drop-downs (92 and 96 px), the Borders
  style and Animations type drop-downs (90 px).
- **The per-area commits were not each built**: the final state builds with no warning; an
  intermediate one may not (the main window's and its partials' changes depend on each other — they
  share the last commit).
- **Checks by hand done by the run**: the app starts in French and in English (`--new-instance`), the
  main window's controls read in French, resp. unchanged in English (UI Automation). The rest of
  § Test Impact is left to the user.

### Iteration 5 — 2026-11-05 — ⚙️ Post-implementation — Missing French text warning

Requested with the merge (Q&A 9): a build warning when a key of `Strings.resx` has no French text in
`Strings.fr.resx`, or when the French file keeps a key the English one dropped — an MSBuild target in
the csproj, run before the compilation; RULES.md § UI Language mentions it.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4, 5 | 2026-11-05 | Setting, resources, 1 121 texts in English and French, numbers and units, shortcuts window; the build check for missing French texts (tested with a missing and a stale key) |
| Unit tests | 4 | 2026-11-05 | No test project — start-up in both languages checked by the run, the rest of Test Impact left to the user |
| README | 4 | 2026-11-05 | Both READMEs: the setting, the sizes' units; `README.fr.md`: the French labels |
| French docs | 4 | 2026-11-05 | `GLOSSARY.fr.md`, `SHORTCUTS.fr.md`: the French labels, embedded `SHORTCUTS.fr.md` |
| Rules | 4, 5 | 2026-11-05 | RULES.md § UI Language (its build check in 5); `../CLAUDE.md` § French Versions (propagated to the running sessions); glossaries' *UI language* row |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | First launch (no language in `settings.json`): which language? | English | 2026-11-04 |
| 2 | Where is the language chosen in the UI? | A submenu of the ⚙ menu | 2026-11-04 |
| 3 | What happens when the language changes? | Applied at the next start; the app offers to restart | 2026-11-04 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — one scout pass | 2026-11-04 |
| 5 | What carries the texts: `.resx` or a hand-written C# class? | `.resx` | 2026-11-05 |
| 6 | Numbers and units in French? | The French way: `12,5 s`, `412 Ko` in French; `12.5 s`, `412 KB` in English | 2026-11-05 |
| 7 | The ⚙ submenu's label: bilingual or translated? | Bilingual, `Language / Langue` | 2026-11-05 |
| 8 | The French docs' UI labels with a French UI? | Every French doc moves to the French labels; the shared rule changes | 2026-11-05 |
| 9 | After the tests: the branch? And the build check for missing French texts? | Merge and finish; yes, the check as an adjustment | 2026-11-05 |

---

*Last updated: 2026-11-05*
