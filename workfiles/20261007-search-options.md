# Search Options

> Working document — two toggle buttons left of the file explorer's search box choosing where the
> search looks: in the file names, in the files' content, or both.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer's search matches every typed word against the file's **relative path** (its name
and its subfolders) and, for a word missing from it, against the file's **content text** (OCR of
images and a PDF's first page, text and HTML files read — `workfiles/20260926-ocr-search.md`). Today
both are always searched.

Two **search criteria buttons** are added to the search row, between the **📁** folder-view toggle and
the search box, each turning one criterion on or off:

| Button | Criterion | Default |
|---|---|---|
| **Aa** | In the file **name** (the relative path, as today) | On |
| **💡** | In the file's **content** text — the light bulb already marking a tile found by its content | On |

Components: `UI/FileExplorerPanel.cs` (search row, `Find`, `SearchFolder`), `Explorer/FileSearch.cs`
(`Search`, `Rank.Of`).

---

## UI

- Search row today: `[📁] [search box] [↻]` — a 3-column `TableLayoutPanel` (`_searchRow`), the 📁 a
  `CheckBox` with `Appearance.Button`, 26 × 23.
- Becomes `[📁] [Aa] [💡] [search box] [↻]`: the two new buttons are **toggle buttons with an icon**,
  built like the 📁 (`CheckBox`, `Appearance.Button`, same size and margins), **pressed = criterion on**.
- Each has a **tooltip** saying what it searches and its state — *off — click to turn on*, *on — click
  to turn off*, or *on — at least one criterion stays on* for the only one pressed; **💡**'s adds a
  second line while the ⚙ OCR setting is off (`ApplyCriteriaTips`, refreshed by the toggles and by
  `ContentSearch`).
- Both are `CheckBox`es with `AutoCheck` off, toggled by `FileExplorerPanel.ToggleCriterion`, which
  ignores a click on the only one pressed.
- **At least one stays on**: a click on the last pressed button does **nothing** — it stays pressed,
  its tooltip saying at least one criterion stays on.
- **Not remembered**: both pressed at every launch; process state only, not an app setting, not in
  the undo history (the file explorer is outside it, RULES.md § Undo History).

---

## Search

| Criteria on | A word matches a file when it is found… |
|---|---|
| Name + content (default) | in its relative path, or else in its content text — today's behaviour |
| Name only | in its relative path (name **and** subfolders, as today) — the content texts are not read (`contentOf` null) |
| Content only | in its content text — the path is not read; a file without a content text matches nothing |

- Ranking unchanged: path matches first, then content matches (`Rank.CompareTo`); content only, every
  match is a content match, so they rank among themselves by the existing tie-breakers.
- The 💡 on a tile and its arrow keep showing the first word found in the content.
- With `*` alone (every file) or an empty box (the favorites), the buttons stay **clickable with no
  effect**: their state counts as soon as words are typed.
- `FileSearch.Search` takes `byName` (default true): false, the path is not read. `Find` passes
  `byName` and gives no content texts while 💡 is released; `SearchFolder` finds no folder while Aa is.
- Toggling a button **runs the search again** at once, as typing does (back to the top: a new search).
- Applies to the **search view** and the **folder view** alike — same box, same `Find`.
- Content only, in the **folder view**: the **folder tiles are hidden** — a folder has no content; only
  the files found by their content show.
- **💡 stays enabled** while the ⚙ menu's *Search file contents (OCR)* is off: it searches the texts
  already extracted, as today; its tooltip then says the extraction is stopped.

---

## Documentation

- `README.md` / `README.fr.md`, § File explorer: the two buttons, their defaults, the three modes.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: a new term, **Search criteria** (*critères de recherche*) — the
  **Aa** and **💡** buttons; the *File explorer* row mentions them.
- The README documents no content search otherwise (its docs were declined in
  `workfiles/20260926-ocr-search.md`, and § Planned still lists the OCR): the criteria bullet names
  the ⚙ setting that extracts the texts, nothing more.

---

## Test Impact

The solution has **no test project** (`src/ImageGridFusion.slnx` holds the app only): no unit test is
created. The behaviours are checked by hand on the running app:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Name only: a word found only in a content text finds nothing | — (manual check) | — |
| Content only: a word in a file name but not in its content finds nothing | — (manual check) | — |
| Both on: today's results unchanged | — (manual check) | — |
| `FileSearch.Search` with `byName` false / no content: the three modes | scratchpad console over `FileSearch.cs` (not kept) | 7 cases, all passed |
| The last pressed button cannot be released | — (manual check) | — |

---

## Open Questions

- [x] ~~What does **Aa** cover: the relative path (name **and** subfolders, as today) or the file name only?~~ → The whole relative path, as today
- [x] ~~Content only, in the **folder view**: are the matching **folder tiles** still shown (folders have no content)?~~ → Hidden
- [x] ~~While the ⚙ menu's **Search file contents (OCR)** is off (texts already extracted still searched), how does **💡** behave?~~ → Stays enabled, searching the texts already extracted; its tooltip says the extraction is stopped
- [x] ~~A click on the **last pressed** button: does nothing, or switches to the other criterion?~~ → Does nothing; its tooltip says at least one stays on
- [x] ~~With `*` alone or an empty box (every file / the favorites), do the buttons stay clickable with no effect?~~ → Yes, clickable with no effect
- [x] ~~Documentation: README (EN + FR) only, or also a glossary term (*search criteria*)?~~ → README and glossary, EN + FR
- [ ] *(found during the run, out of scope)* The README still lists the OCR search under § Planned and does not describe the content search (declined in `workfiles/20260926-ocr-search.md`): document it now?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the request and the scoping answers: two toggle buttons with an icon (**Aa**,
**💡**) between 📁 and the search box, both on by default, at least one always on, not remembered
between sessions. The search reads the path, the content text or both accordingly; ranking and the
bulb unchanged. No test project: manual checks only.

### Iteration 2 — 2026-10-07

Four open questions answered: **Aa** keeps covering the whole relative path; content only hides
the folder tiles of the folder view; **💡** stays enabled with the ⚙ OCR setting off; a click on the
last pressed button does nothing. *UI* and *Search* updated.

### Iteration 3 — 2026-10-07

Last two questions answered: the buttons stay clickable with no effect under `*` or an empty box;
the documentation covers the README and the glossary, both languages. *Search* updated, a
*Documentation* section added. No open question left.

### Iteration 4 — 2026-10-07 — ✅ Implemented

Go given: code, tests and documentation, in a dedicated worktree (`feature/search-options`),
fast-forwarded into `main` and removed at the end. No unit test: no test project.

### Iteration 5 — 2026-10-07 — 🧭 Implementation choices

- **Tooltips** worded by the agent: what each criterion searches, then its state; 💡's second line
  while the ⚙ OCR setting is off.
- **`AutoCheck` off** on both buttons, toggled by hand in `ToggleCriterion`: the simplest way to keep
  the last one pressed, keyboard (Space) included.
- **Re-run only with words typed** (not `*`, not an empty box), so the favorites and `*` lists keep
  their scroll — the "clickable with no effect" of Iteration 3.
- **Checks**: no test project — the three modes checked by a throwaway console project in the
  scratchpad compiling `FileSearch.cs` (7 cases, all passed); the window itself left to the user's
  hand test. The worktree's `bin` got a copy of `main`'s `settings.json`, `favorites.txt` and
  `Index\` to search a real folder (not committed).
- **README**: describes the buttons only; it documents no content search otherwise — raised as a
  new Open Question, not done (scope).
- **Incident (design phase)**: a `git commit -a` on `main` swept in another session's uncommitted
  `BackgroundEffect.cs`; the commit was reset (`--soft`) at once and redone with the workfile only,
  the file left modified as it was.
- ⚠️ **Rule broken — the launch path** (`CLAUDE.md` § Launch): the delivery launch ran a build of
  `main` output to the scratchpad (`dotnet build -o`), not `src/ImageGridFusion/bin/…/ImageGridFusion.exe`
  — that exe was locked by two instances of another session (*Extension pixels bord image*), which
  were left running. The scratchpad build got a copy of `settings.json`, `favorites.txt` and `Index\`;
  what it saves stays there.
- Merged: rebased onto `main` (no conflict), fast-forwarded, worktree and branch removed.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-10-07 | `FileSearch.Search(byName)`, the Aa / 💡 buttons in `FileExplorerPanel` |
| Unit tests | 5 | 2026-10-07 | None — no test project; a scratchpad check instead (see *Test Impact*) |
| README | 5 | 2026-10-07 | § File explorer, *Search criteria* bullet, EN + FR |
| Glossary | 5 | 2026-10-07 | *Search criteria* term, *File explorer* row, EN + FR |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Shape of the two buttons next to 📁? | Toggle buttons with an icon (`[Aa] [💡]`, pressed = on) | 2026-10-07 |
| 2 | Both criteria unchecked: what happens? | Impossible, one stays on | 2026-10-07 |
| 3 | Is the buttons' state remembered between sessions? | No, both on at every launch | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-10-07 |
| 5 | What does **Aa** cover? | The whole relative path (name and subfolders), as today | 2026-10-07 | |
| 6 | Content only, folder view: folder tiles shown? | Hidden | 2026-10-07 | |
| 7 | 💡 while the ⚙ OCR setting is off? | Stays enabled, the tooltip says the extraction is stopped | 2026-10-07 | |
| 8 | Click on the last pressed button? | Nothing happens | 2026-10-07 | |
| 9 | Buttons with `*` / an empty box? | Clickable, no effect | 2026-10-07 | |
| 10 | Documentation scope? | README + glossary (EN + FR) | 2026-10-07 | |

---

*Last updated: 2026-10-07*
