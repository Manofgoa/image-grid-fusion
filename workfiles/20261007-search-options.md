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
- Each has a **tooltip** saying what it searches and whether it is on.
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

---

## Test Impact

The solution has **no test project** (`src/ImageGridFusion.slnx` holds the app only): no unit test is
created. The behaviours are checked by hand on the running app:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Name only: a word found only in a content text finds nothing | — (manual check) | — |
| Content only: a word in a file name but not in its content finds nothing | — (manual check) | — |
| Both on: today's results unchanged | — (manual check) | — |
| The last pressed button cannot be released | — (manual check) | — |

---

## Open Questions

- [x] ~~What does **Aa** cover: the relative path (name **and** subfolders, as today) or the file name only?~~ → The whole relative path, as today
- [x] ~~Content only, in the **folder view**: are the matching **folder tiles** still shown (folders have no content)?~~ → Hidden
- [x] ~~While the ⚙ menu's **Search file contents (OCR)** is off (texts already extracted still searched), how does **💡** behave?~~ → Stays enabled, searching the texts already extracted; its tooltip says the extraction is stopped
- [x] ~~A click on the **last pressed** button: does nothing, or switches to the other criterion?~~ → Does nothing; its tooltip says at least one stays on
- [x] ~~With `*` alone or an empty box (every file / the favorites), do the buttons stay clickable with no effect?~~ → Yes, clickable with no effect
- [x] ~~Documentation: README (EN + FR) only, or also a glossary term (*search criteria*)?~~ → README and glossary, EN + FR

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | None planned — no test project (see *Test Impact*) |
| README | | | |
| Glossary | | | |

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
