# Whole-Word Search Ranking

> Working document — a search word found as a whole word ranks before one found at a word's start,
> which ranks before one found inside a word, in the path and in the content.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Searching `tique` in the folder view of `Outils RS` lists, in the **Path** section, *Label Rouge
Soviétique.png*, *nobel pas de crise climatique.jpg*, *pas de crise climatique.jpg* **before**
*Tique bulle.jpg* — the only file whose name holds the word itself. Every path match holding the word
in its name sits in one relevance tier, and inside a tier the sort order (Date ↓) decides
(`FileSearch.Search`, `Rank.CompareTier`; workfiles/20261009-search-results-sort.md § Relevance Tiers).

The search then ranks a word by **how** it is found, on top of where:

| Level | `tique` found in | Example |
|---|---|---|
| 1 — **Whole** | a word equal to it | *Tique bulle.jpg* |
| 2 — **Start** | the start of a longer word | *Tiques en été.jpg* |
| 3 — **Inside** | inside a longer word | *Label Rouge Soviétique.png* |

The levels are **relevance**: they rank before the sort order, as the tiers do today.

Components: `Explorer/FileSearch.cs` (`Rank`, `Search`, `SearchFavorites`, the tier numbers),
`Explorer/ContentIndex.cs` (`Find`, the content grade), `Explorer/ContentPass.cs` (`ContentGrade`),
`UI/FileExplorerPanel.cs` (the merge of the favorites, presets and people by tier number, unchanged).

---

## Words and Their Boundaries

- A **word** of a text is a run of letters and digits in its **folded** form (`FileSearch.Fold`:
  accents dropped, lower case); anything else separates two words — a space, `_`, `-`, `.`, brackets,
  `\` and `/` of the path.
- A change between a **letter and a digit** separates two words too: *tique2.jpg* holds `tique` whole,
  *photo01.jpg* holds `photo` whole. A change of **case** does not: *TiqueBulle.jpg* is one word, the
  folded path being read, never the original one.
- The **level of a query word in a text** is the best of its occurrences there: whole if one occurrence
  is bounded on both sides, else start if one is bounded on its left, else inside.
- A **query word** is still one of `FileSearch.Words` — the query folded, split on spaces.
- One helper gives the level (`FileSearch.LevelOf(folded, word, from)` — name chosen at implementation),
  used by every place below.

---

## Path Ranking

Today, a path match's tier is the number of query words found in the **file name**, more first; the
words found in the subfolders only rank after. The levels refine every path tier, never cross it:

1. The tier as today — the files whose name and content hold every word (`ByNameAndContent`), then the
   words found in the name, more first.
2. Inside it, the **number of query words found whole**, more first — in the name for a word found in
   the name, in the path for a word found in the subfolders only.
3. Then the number found at a **word's start**, more first.
4. Then, as today: the sort order when one is chosen, else the first word's position, the shorter name,
   the path.

With one query word, this is the three levels of the table above. With `tique bulle`, a file holding
both whole ranks before one holding `tique` whole and `bulle` at a start (*bulles*), before one holding
`tique` inside.

### The Favorites

The favorites matched by their **file name** (`SearchFavorites`) are merged with the files by tier number
(`FileExplorerPanel`, the search view): their name is ranked by the same levels, so a favorite *Tique.jpg*
and a file *Tique bulle.jpg* stand together, before a favorite *Soviétique.jpg*. The preset descriptions
and the people's names keep their own tier, unchanged inside.

### The Tier Numbers

`SearchMatch.Tier` and `FavoriteMatch.Tier` stay one comparable number: the path tiers are numbered with
their levels folded in (e.g. `tier × (words + 1)² + (words − whole) × (words + 1) + (words − start)`), and
`DescriptionTier`, `PersonTier`, `SimilarityTier`, `IsPathTier` follow, so the **Path** / **Content**
sections and the merge keep working unchanged.

---

## Content Ranking

The path keeps **more weight** than the content: every path tier stays before every content tier (the
user's answer, Q&A 2). Inside the content:

- The **grade** stays first (`ContentGrade`: Sure, Joined, Weak, Seen, SeenInside) — how reliable the slot
  holding the word is.
- Inside a grade, the content matches rank by the **number of query words found whole**, then at a
  **word's start**, more first — the path's rule, on the content text.
- **Joined** has no word boundary (the spaces are removed): a word found joined counts as **inside**.
- **Seen** and **SeenInside** already split a tag or caption word found whole from one found inside; a
  word found at the start of a tag word (`chien` in `chiens`) is **SeenInside** with a start level, so it
  ranks first inside that grade.
- `ContentIndex.Find` returns, for each grade, the slot holding the word at its **best level** rather than
  the first slot holding it, and that level; the excerpt and the arrow follow that slot.

---

## Docs

- `RULES.md` § Content OCR Passes (the grades' order) and § File Explorer Sections (the search's ranking)
  gain the levels.
- `GLOSSARY.md` / `GLOSSARY.fr.md` — *Sort order*'s relevance tiers mention the whole-word levels.
- `README.md` / `README.fr.md`: changed only where they describe the ranking.

---

## Test Impact

Not applicable: the repository has no test project (CONTRIBUTING.md § Build — *check a change by hand
in the running app*). Checked by hand: `tique` in `Outils RS` lists *Tique bulle.jpg* first in **Path**,
whatever the sort order.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | No test project |

---

## Open Questions

- [x] ~~Does a change between a letter and a digit separate two words — `tique2.jpg` holding `tique` whole?~~
  → **Yes** (Q&A 5).
- [x] ~~Does a change of case inside a word separate two words — `TiqueBulle.jpg` holding `tique` whole and
  `bulle` whole?~~ → **No**: the folded path is read (Q&A 6).
- [x] ~~A word found **in the name** outranks a word found **whole** in the subfolders only — for `tique`,
  *Soviétique.jpg* before *Tique\photo.jpg* — as today's tiers do?~~ → **Yes**: the levels refine a tier and
  never cross it (Q&A 7).
- [x] ~~The favorites' file names ranked by the same levels, the preset descriptions and the people's names
  left as they are?~~ → **Yes** (Q&A 8).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design from the user's request and the scoping answers (Q&A 1–4): three levels — whole, start,
inside — refining every path tier and every content grade, the number of words found whole first, then
at a start; the path always before the content; the favorites' names ranked alike; the tier numbers kept
one comparable number.

### Iteration 2 — 2026-11-05

Open questions answered (Q&A 5–8), every proposal kept: a letter ↔ digit change separates two words, a
case change does not; the name stays before the subfolders; the favorites' names ranked alike. § Words
and Their Boundaries updated.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project in the repository |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How many match levels in the path for `tique`? | Three: whole > word start > inside | 2026-11-05 |
| 2 | Where does the whole-word ranking apply? | Path and content — the path always weighing more than the content | 2026-11-05 |
| 3 | With several query words, how to combine? | Count the words found whole — more ranks higher; on a tie, the best level of the others decides | 2026-11-05 |
| 4 | Is the subject straightforward or tricky? | Straightforward — one scout pass | 2026-11-05 |
| 5 | Does a change between a letter and a digit separate two words? | Yes | 2026-11-05 |
| 6 | Does a change of case separate two words? | No | 2026-11-05 |
| 7 | Does a word in the name outrank a whole word in the subfolders only? | Yes, the name first | 2026-11-05 |
| 8 | Are the favorites' file names ranked by the same levels? | Yes; preset descriptions and people's names unchanged | 2026-11-05 |

---

*Last updated: 2026-11-05*
