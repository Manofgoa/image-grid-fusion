# Open Directory Link

> Working document — a blue **Open directory** link at the end of the status line after a successful
> Save or Copy, opening Explorer on the file just written.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

After a Save or a Copy succeeds, the status line of the bottom bar says what was written
(*Saved fusion-….png — 2400 × 1256, 1.2 MB…*), but finding the file means opening Explorer by hand —
for a Copy, the previous copy kept in `previous\` next to the exe is not even named. The request: a
blue **Open directory** link right after that message, opening Explorer on the folder, **the file
selected**.

Components involved:

- `UI/MainForm.cs` — the bottom bar's status line (`_statusLine`, a `FlowLayoutPanel` holding `_status`
  then `_cancelButton`), `ShowStatus`, `FitStatusWidth`, and the existing `ShowInExplorer(path)` (Explorer
  `/select,` on a file, its folder when the file is gone, the status line saying so).
- `UI/PreviousCopy.cs` — the previous copy: `Keep` / `KeepCopyOf` write the file but do not return its
  path yet.
- The routes writing a file, below.

---

## Placement and Look

- A `LinkLabel` in `_statusLine`, **right after `_status`** and before `_cancelButton`: the link reads as
  the end of the message. Hidden by default.
- Text: **Open directory** (`Strings.resx`), **Ouvrir le dossier** in French (`Strings.fr.resx`).
- Its icon: **📂** before the text (§ Icons), one `UiIcons` field, drawn at 16 logical px and again when
  the DPI changes, as the buttons' icons.
- The `LinkLabel`'s own colors — Windows' link blue, the hand cursor, underlined on hover.
- Its tooltip: the **full path** of the file it opens.
- `FitStatusWidth` subtracts the link's width while it shows, so a long message wraps before pushing the
  link or the output buttons out of the window.

## Behaviour

- **Click** → `ShowInExplorer(path)`: Explorer on the folder, the file selected; a file gone since opens
  its folder and says so on the status line (already the method's behaviour).
- **One entry point**: `ShowStatus(string message, bool error = false, string? savedFile = null)`. A
  message with `savedFile` shows the link on that file; **every other call hides it** — a later
  message, an error, a progress line — so the link never outlives the message it belongs to.
- Never shown with an error message.

## Routes

Every route the user chose: Save, Copy, Save last / Save upscaled, Cell copy.

| Route | Method | File the link opens |
|---|---|---|
| Save PNG (button, PNG max size entries) | `SaveAs` (still) | The file chosen in the dialog |
| Save MP4 / GIF | `SaveAs` (animated) | The file chosen in the dialog |
| Save last MP4 / GIF… | `SaveLastVideo` | The file chosen in the dialog |
| Save upscaled image… (cell menu) | `SaveUpscaledAsync` | The file chosen in the dialog |
| Copy PNG (button, key, PNG max size entries) | `Copy` (still) | The previous copy in `previous\` |
| Copy MP4 / GIF | `Copy` (animated) | The previous copy in `previous\` |
| Copy ▾ JPEG for sharing | the sharing copy | The previous copy in `previous\` |
| Cell copy (key, *Copy cell*, layer list *Copy*) | `CopyAlone` | The previous copy in `previous\` |
| Copy last MP4 / GIF | `CopyLastVideo` | The last video's file in `%TEMP%\ImageGridFusion` — the one the clipboard points at, emptied at the next start |

- `PreviousCopy.Keep` / `KeepCopyOf` **return the path** of the file kept; `KeepPrevious` returns it with
  the status suffix, `null` when the copy could not be kept.
- A Copy whose previous copy could not be kept (`MainStatusNotKept`): **no link** — the message says why,
  and a PNG copied has no other file.

---

## Documentation

- `README.md` § Output — the Copy / Save lines and *Previous copy*: the link after a success.
  `README.fr.md` in the same commit (French label as the French UI shows it).
- `GLOSSARY.md` / `GLOSSARY.fr.md`: no new term planned — a link is not a concept of its own.
- `SHORTCUTS.md`: no key, nothing to add.

---

## Test Impact

There is **no unit test project** in this repository (`ImageGridFusion.slnx` holds the app alone), so
nothing is created or updated. The behaviour is checked by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [x] ~~French text of the link: *Ouvrir le dossier*?~~ → **Ouvrir le dossier**
- [x] ~~Does the link take an icon (📂) before its text, as § Icons asks of buttons?~~ → Yes, 📂 before it
- [x] ~~*Copy last MP4 / GIF* puts back the last video's temp file without keeping a new previous copy:
      link to that temp file, or no link?~~ → A link to that temp file
- [x] ~~A Copy whose previous copy could not be kept: no link, or (MP4 / GIF / JPEG) a link to the temp
      file the clipboard points at?~~ → No link

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design from the request and the scoping batch (Q1–Q4): a `LinkLabel` after the status text,
shown by `ShowStatus` when given the file written, hidden by any other message; a click opens Explorer
with the file selected through the existing `ShowInExplorer`; every Save and Copy route, Save last,
Save upscaled and the cell copy included — a Copy pointing at its previous copy in `previous\`, whose
path `PreviousCopy` now returns. Four details left open.

### Iteration 2 — 2026-11-05

The four open details settled (Q5–Q8): the French text *Ouvrir le dossier*; a 📂 icon before the text;
*Copy last MP4 / GIF* shows the link on the last video's temp file; a Copy whose previous copy was not
kept shows none. No open question left.

### Iteration 3 — 2026-11-05 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/open-directory-link`, branch
`feature/open-directory-link`). Scope frozen as of Iteration 2.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the link show? | At the end of the status line | 2026-11-05 |
| 2 | What does a click do? | Opens Explorer, the file selected | 2026-11-05 |
| 3 | Which routes show it? | Save (PNG / MP4 / GIF / JPEG), Copy (previous copy), Save last / Save upscaled, Cell copy | 2026-11-05 |
| 4 | Is the subject straightforward or tricky? | Straightforward — a single scout pass | 2026-11-05 |
| 5 | French text of the link? | Ouvrir le dossier | 2026-11-05 |
| 6 | An icon before it? | Yes, 📂 | 2026-11-05 |
| 7 | Copy last MP4 / GIF: link or not? | A link to the last video's temp file | 2026-11-05 |
| 8 | Copy whose previous copy was not kept: link or not? | No link | 2026-11-05 |

---

*Last updated: 2026-11-05*
