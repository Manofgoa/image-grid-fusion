# GIPHY GIF and Sticker Views

> Working document — the GIPHY view split into two views, GIPHY GIFs and GIPHY Stickers, and no call to
> GIPHY until a search is asked for.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The GIPHY view (`workfiles/20261105-online-library-giphy.md`) is one entry of the file explorer's views
drop-down, its **🎞️ GIFs** and **😎 Stickers** buttons choosing what is searched. Entering it shows the
**trending** of the kind chosen — a call to GIPHY the first time in the session — and emptying its box shows
the trending again, a call too when not kept.

Two changes, one goal: **cycling the views with Ctrl+F, or entering a GIPHY view in any way, never spends a
call of the key's 100 an hour**.

1. **Two views**: *GIPHY GIFs* and *GIPHY Stickers*, two entries of the views drop-down, both in the Ctrl+F
   cycle; the two buttons go.
2. **No call without a search**: a GIPHY view calls GIPHY only on **Enter** in its box — an empty box giving
   the trending, as today — or on the next page of the list it shows (the scroll's Loading… slot). Entering the
   view, emptying the box, setting the key call nothing.

Components: `Explorer/ExplorerMode.cs`, `UI/FileExplorerPanel.cs` (the mode, the cycle, the drop-down, the
Enter key, ↑ / breadcrumb), `UI/FileExplorerPanel.Giphy.cs` (the view's lists, the kind), `UI/AppSettings.cs`
(the saved mode), `UI/UiIcons.cs`, `Resources/Strings*.resx`, and the docs.

---

## Two Views

| Point | Design |
|---|---|
| The views | `ExplorerMode.GiphyGifs` and `ExplorerMode.GiphyStickers` in place of `ExplorerMode.Giphy`, in this order, after Favorites: the drop-down lists **Folders, People, Favorites, GIPHY GIFs, GIPHY Stickers** |
| Their names | `GIPHY GIFs` / `GIPHY Stickers` in English, `GIPHY GIF` / `GIPHY Stickers` in French |
| The kind | Each view's kind is fixed (`GiphyKind.Gifs` / `Stickers`): `_giphyKind` follows the mode, the GIFs / Stickers toggle and `ChangeGiphyKind` are removed, with their strings (`ExplorerAccGiphy*`, `ExplorerTipGiphyGifs` / `Stickers`) — their tooltips' content (*arrives as an MP4*, *made for the layers*) moved to the views' entries if the drop-down has tooltips, else dropped *(see Open Questions)* |
| Ctrl+F | Both views in the cycle (`NextMode`), both skipped while no key is set |
| The saved view | Saved by name, as now; a `Giphy` saved by the previous version is read as `GiphyGifs` (`AppSettings.ExplorerMode`) |
| Everything else | Shared by both: the key, the rating, the quota and its breadcrumb count, the downloads, the hearts, the attribution, the hidden sort / groups buttons |

---

## No Call Without a Search

What calls GIPHY, and what no longer does:

| Event in a GIPHY view | Today | After |
|---|---|---|
| **Enter** in the box, text typed | A search — a call | Unchanged |
| **Enter** in the box, empty | The trending — a call unless kept | Unchanged |
| The Loading… slot scrolled into view, GIPHY holding more | The next page — a call | Unchanged: it continues a list asked for |
| The view entered (drop-down, Ctrl+F, the saved view at start-up) | The trending — a call the first time | **No call**: the view's kept list, else the invitation *(below)* |
| The box emptied | The trending — a call unless kept | **No call**: the list shown stays, the breadcrumb saying *Enter to search* as for a text typed |
| The key set or changed | The trending — a call | **No call**: the invitation; the lists kept forgotten (another key) |
| The other kind chosen (the toggle) | The same search in the other kind — a call | Gone with the toggle: entering the other view shows its own kept list |
| ↑, Backspace, Alt+↑, the breadcrumb's *GIPHY* | The trending — a call unless kept | *(see Open Questions)* |
| The rating changed in the ⚙ menu | The search shown done again — a call | *(see Open Questions)* |

### What Each View Keeps

- **Its last list**, for the session: the rows, the query (null: the trending), whether GIPHY holds more —
  per kind, beside the trending already kept per kind (`_giphyTrending`). Entering the view shows it as it was
  left, its thumbnails loaded again from the CDN (out of the budget).
- **Nothing yet** → the **invitation**: the view's grid replaced by the invite panel, as without a key, saying
  to type a search and press Enter — Enter on the empty box showing the trending *(its exact form: see Open
  Questions)*.
- The **box's text** stays as the cycle leaves it (Ctrl+F selects it, as in every view): a text differing from
  the list's query shows *Enter to search* in the breadcrumb, as today.
- A change of the key forgets both views' lists and trending; a change of the rating *(see Open Questions)*.

---

## Docs

| File | Change |
|---|---|
| `RULES.md` § Online Libraries | The two views, a view's entry in the views drop-down each; *the trending of each kind kept for the session* → each view's last list kept, nothing called until Enter or a next page |
| `GLOSSARY.md` / `.fr.md` | *GIPHY view* → the two views, the buttons gone; *File explorer*'s four views → five |
| `README.md` / `.fr.md` | The GIPHY view paragraph, the views drop-down's list, the Ctrl+F cycle's list |
| `SHORTCUTS.md` / `.fr.md` | Ctrl+F's row (*the GIPHY views skipped*), ↑ / Backspace / Alt+↑ rows if their behaviour changes |

---

## Test Impact

The app has no test project (`src/ImageGridFusion.slnx` holds `ImageGridFusion.csproj` only): nothing testable
is pinned by unit tests. The checks are made by hand in the app — the call count of the breadcrumb unchanged
after cycling Ctrl+F through both views.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [ ] The views' icons in the drop-down: 🎞️ / 😎 (the buttons' today), 🌍 for both, or 🌍 GIFs / another?
- [ ] The invitation at a view's first entry: a text only (*type a search, then Enter — Enter on an empty box
      shows the trending*), or the same text with a **Trending** button?
- [ ] ↑, Backspace, Alt+↑ and the breadcrumb's *GIPHY*: still the trending (an explicit action — a call unless
      kept), or the box emptied only, Enter then needed?
- [ ] The rating changed while a GIPHY view shows: the search shown done again (a call, as today), or the kept
      lists forgotten and the invitation shown?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design from the request and the scoping batch (Q&A 1–4): a new workfile; two views, both in the
Ctrl+F cycle, the GIFs / Stickers buttons removed; a view entered shows its last list kept for the session,
else an invitation, never a call; only Enter and a next page call GIPHY. Four questions left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: the app has no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where to record this work: a post-implementation iteration of `20261105-online-library-giphy.md`, or a new workfile? | A new workfile | 2026-11-05 |
| 2 | How the two categories show | Two views of the views drop-down, both in the Ctrl+F cycle | 2026-11-05 |
| 3 | What a GIPHY view shows when entered without a search validated | Its last results kept for the session, else an invitation | 2026-11-05 |
| 4 | Depth of the exploration | Straightforward: one pass | 2026-11-05 |
| 5 | The views' icons | | 2026-11-05 |
| 6 | The invitation's form | | 2026-11-05 |
| 7 | ↑ / Backspace / Alt+↑ / breadcrumb *GIPHY* | | 2026-11-05 |
| 8 | The rating changed while a view shows | | 2026-11-05 |

---

*Last updated: 2026-11-05*
