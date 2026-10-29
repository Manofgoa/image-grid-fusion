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

What the code does today (exploration, 2026-10-29), the rows to write. The wording is final at
implementation; the sections follow the contexts.

### Anywhere

| Shortcut | Does |
|---|---|
| Ctrl+Z | Undo |
| Ctrl+Y, Ctrl+Shift+Z | Redo |
| Ctrl+V | Paste files, an image or a text into a cell |
| Ctrl+C | Copy the grid (PNG / MP4 / GIF) — the selected file when the explorer's tiles have the focus |
| Ctrl+S | Save the grid |
| Ctrl+N | Clear all |
| Ctrl+R / Ctrl+Shift+R | Turn the image under the mouse — else the selected one — a quarter turn clockwise / counter-clockwise |
| Ctrl+F / Ctrl+Shift+F | Go to the explorer's search box; pressed again there, next / previous view |
| Space | Play / Pause, unless a text field has the focus |
| F1 *(new)* | Show the keyboard shortcuts — in a text field too |

In a text field (search box, Frames fields, layer text), Ctrl+V / C / Z / Y, Delete and Escape act on
its text.

### Selected cell or layer

| Shortcut | Does |
|---|---|
| Arrows | Move the selected image 1 px, the preview focused |
| Ctrl+arrows | Move it 10 px |
| Shift+arrows | Move it 1 px, ignoring the magnetic stops |
| Ctrl+Shift+arrows | Jump to the next stop |
| Delete | Remove the selected layer, else the selected cell's image |
| Escape | Close the layer edit view, else deselect |

### Preview — mouse

| Shortcut | Does |
|---|---|
| Wheel | Zoom the image under the mouse, around the point under it |
| Ctrl+wheel | Finer zoom steps |
| Shift held while dragging an image | Ignore the magnetic stops |
| Double-click on a separator | Put it back in its default place |

### Crop and Blur bars

| Shortcut | Does |
|---|---|
| Wheel | Grow / shrink the zone 5 % a notch, its ratio kept, around its center |
| Ctrl+wheel | Same, around the point under the mouse |
| Alt+wheel | Zoom the image instead |
| Ctrl+Alt+wheel | Finer zoom of the image |

### Layers (Layers tab selected)

| Shortcut | Does |
|---|---|
| Wheel over the selected layer | Grow / shrink it 5 % a notch |
| Shift+wheel over the selected layer | Turn it 5° a notch |
| Shift held while dragging the round handle | Turn by 15° steps |
| Double-click on the selected layer | Open its edit view |
| Double-click outside it, in the edit view | Close the edit view |

### Pick and eyedropper

| Shortcut | Does |
|---|---|
| Shift+click (Pick armed) | Add a point to keep, Pick staying armed |
| Ctrl+click (Pick armed) | Add a point to leave out, Pick staying armed |
| Escape (Pick armed) | Cancel Pick |
| Any key, any button but the left one (eyedropper armed) | Cancel the eyedropper |

### Options

| Shortcut | Does |
|---|---|
| Ctrl+wheel over a slider | One step a notch |
| Wheel / Ctrl+wheel over the Zoom slider | The wheel's zoom steps / its finer ones |
| Up / Down in a Frames field | ±1 frame |
| Ctrl+Up / Ctrl+Down in a Frames field | ±5 frames |
| Wheel / Ctrl+wheel over a Frames field | ±1 / ±5 frames a notch |
| Enter / Escape in a Frames field | Apply / give back the value |

### File explorer — tiles

| Shortcut | Does |
|---|---|
| Arrows | Move between the tiles |
| PageUp / PageDown | Move by a page |
| Home / End | First / last tile |
| Enter, double-click | Bring the file into a cell, or open the folder / group |
| Backspace, Alt+Up | Parent folder |
| F2 | Rename the selected group (People view) |
| Ctrl+C | Copy the selected file |
| Ctrl+wheel | Larger / smaller tiles |

### File explorer — search box and URL zone

| Shortcut | Does |
|---|---|
| Enter | Bring the first result into a cell |
| Down | Go to the first tile |
| Alt+Up | Parent folder |
| Escape | Give the focus back to the preview |
| Enter in the URL field | Load the URL |

### Dialogs

| Shortcut | Does |
|---|---|
| Ctrl+Enter in a preset's description | Save the preset |
| Enter / Escape | OK / Cancel |

### Windows

The standard behaviour valid everywhere, inventoried at implementation: Tab / Shift+Tab (next /
previous control), Alt+F4 (close the window), Alt+Space (window menu), a text field's editing keys
(Ctrl+A, Ctrl+Backspace…), the plain wheel scrolling a list, the layout strip or the options. Those
tied to one context (the wheel scrolling the tiles, the layout strip) go in its section.

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
  `SHORTCUTS.md`.

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository |
| SHORTCUTS.md / SHORTCUTS.fr.md | | | |
| RULES.md | | | |
| README | | | |
| Glossary | | | |

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
