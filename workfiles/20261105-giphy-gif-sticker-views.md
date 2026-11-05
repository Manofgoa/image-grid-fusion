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

Three changes, one goal: **cycling the views with Ctrl+F, or entering a GIPHY view in any way, never spends a
call of the key's 100 an hour**.

1. **Two views**: *GIPHY GIFs* and *GIPHY Stickers*, two entries of the views drop-down, both in the Ctrl+F
   cycle; the two buttons go.
2. **No call without a search**: a GIPHY view calls GIPHY only on **Enter** in its box — an empty box giving
   the trending, as today — or on the next page of the list it shows (the scroll's Loading… slot). Entering the
   view, emptying the box, setting the key call nothing.
3. **Each view shown or hidden**: a **🌍 GIPHY** submenu of the ⚙ menu, a checkbox per view, the **GIPHY API
   key…** and **GIPHY content rating** entries moved into it.

Components: `Explorer/ExplorerMode.cs`, `UI/FileExplorerPanel.cs` (the mode, the cycle, the drop-down, the
Enter key, ↑ / breadcrumb), `UI/FileExplorerPanel.Giphy.cs` (the view's lists, the kind), `UI/AppSettings.cs`
(the saved mode), `UI/UiIcons.cs`, `Resources/Strings*.resx`, and the docs.

---

## Two Views

| Point | Design |
|---|---|
| The views | `ExplorerMode.GiphyGifs` and `ExplorerMode.GiphyStickers` in place of `ExplorerMode.Giphy`, in this order, after Favorites: the drop-down lists **Folders, People, Favorites, GIPHY GIFs, GIPHY Stickers** |
| Their names | `GIPHY GIFs` / `GIPHY Stickers` in English, `GIPHY GIF` / `GIPHY Stickers` in French |
| Their icon | 🌍 (`UiIcons.OnlineLibrary`) for both, their names telling them apart |
| The kind | Each view's kind is fixed (`GiphyKind.Gifs` / `Stickers`): `_giphyKind` follows the mode, the GIFs / Stickers toggle and `ChangeGiphyKind` are removed, with their strings (`ExplorerAccGiphy*`, `ExplorerTipGiphyGifs` / `Stickers`) — what their tooltips said (*arrives as an MP4*, *made for the layers*) stays in the README |
| Ctrl+F | Both views in the cycle (`NextMode`), both skipped while no key is set, each skipped while hidden |
| Shown or hidden | Each view an app setting, **shown by default** (`AppSettings`, `settings.json`): hidden, it is out of the views drop-down and out of the Ctrl+F cycle; both hidden, GIPHY is out of the explorer |
| The saved view | None read: every start opens on the favorites (`workfiles` of main, merged 2026-11-05, see Iteration 7) — the view is still saved by name, for an older version |
| Everything else | Shared by both: the key, the rating, the quota and its breadcrumb count, the downloads, the hearts, the attribution, the hidden sort / groups buttons |
| The breadcrumb | Its first segment the view's name (`GIPHY GIFs` / `GIPHY Stickers`), then the search or *Trending*; while the view invites to search, the view's name alone, then *Enter to search* if a text is typed, and the calls of the hour |
| The views drop-down | Filled with the views offered only (`FileExplorerPanel.FillModes`), its entries the modes themselves (`SelectedItem`, never an index) |

---

## The ⚙ Menu's GIPHY Submenu

The ⚙ menu's two GIPHY entries (`MainForm.Giphy.cs`: `_giphyKeyItem`, `_giphyRatingItem`) become one
**🌍 GIPHY** submenu, in their place:

| Entry | Does |
|---|---|
| ☑ **GIPHY GIFs** | Shows or hides the GIPHY GIFs view — a toggle, its icon 🌍 framed while checked (RULES.md § Icons) |
| ☑ **GIPHY Stickers** | Shows or hides the GIPHY Stickers view, the same way |
| — | A separator |
| 🔑 **GIPHY API key…** | Unchanged — its text too — moved here |
| 🔞 **GIPHY content rating** ▸ | Unchanged — its text too — moved here, under the key |

- A change is saved at once; one that cannot be saved says so on the status line (RULES.md § App Settings).
- Hiding the view shown sends the explorer to the **folder view** — the drop-down's first, as at the first
  launch.
- Out of the undo history, like every app setting.

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
| ↑, Backspace, Alt+↑, the breadcrumb's *GIPHY* | The trending — a call unless kept | Unchanged: an explicit request for the trending, as the invitation's **Trending** button |
| The invitation's **🔥 Trending** button | — | The trending, as Enter on the empty box — a call unless kept |
| The rating changed in the ⚙ menu | The search shown done again — a call | **No call**: both views' lists and trending forgotten (another rating), the invitation shown |

### What Each View Keeps

- **Its last list**, for the session: the rows, the query (null: the trending), whether GIPHY holds more —
  per kind, beside the trending already kept per kind (`_giphyTrending`). Entering the view shows it as it was
  left, its thumbnails loaded again from the CDN (out of the budget).
- **Nothing yet** → the **invitation**: the view's grid replaced by the invite panel, as without a key — its
  text *Type a search, then press Enter — nothing is asked of GIPHY before. Enter on an empty box, or the button
  below, shows the trending.*, and a **🔥 Trending** button doing what ↑ does — the box emptied, the trending shown
  (`InviteAction.GiphyTrending`, its icon `UiIcons.Trending`, no link).
- The **box's text** stays as the cycle leaves it (Ctrl+F selects it, as in every view): a text differing from
  the list's query shows *Enter to search* in the breadcrumb, as today.
- A change of the key or of the rating forgets both views' lists and trending: the invitation shows.
- A call running when the view changes — to the other GIPHY view too — is cancelled and its answer dropped
  (`DropGiphyCall`, the answer checked against the kind of the view shown).

---

## Docs

| File | Change |
|---|---|
| `RULES.md` § Online Libraries | The two views, a view's entry in the views drop-down each; *the trending of each kind kept for the session* → each view's last list kept, nothing called until Enter or a next page |
| `GLOSSARY.md` / `.fr.md` | *GIPHY view* → the two views, the buttons gone; *File explorer*'s four views → five |
| `README.md` / `.fr.md` | The GIPHY view paragraph, the views drop-down's list, the Ctrl+F cycle's list, the ⚙ menu's GIPHY submenu |
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

- [x] ~~The views' icons in the drop-down: 🎞️ / 😎 (the buttons' today), 🌍 for both, or 🌍 GIFs / another?~~ → 🌍 for both
- [x] ~~The invitation at a view's first entry: a text only, or the same text with a **Trending** button?~~ → The
      text and a **🔥 Trending** button
- [x] ~~↑, Backspace, Alt+↑ and the breadcrumb's *GIPHY*: still the trending, or the box emptied only?~~ → Still
      the trending, an explicit action — a call unless kept
- [x] ~~The rating changed while a GIPHY view shows: the search done again, or the kept lists forgotten?~~ → The
      kept lists forgotten, the invitation shown, no call

- [x] ~~The ⚙ menu's **GIPHY content rating**: moved into the GIPHY submenu too, or left where it is?~~ → Moved
      into it, under the key
- [x] ~~Hiding the view the explorer shows (and a hidden view saved at start-up): the folder view, or the view
      before it in the drop-down's order?~~ → The folder view

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

### Iteration 2 — 2026-11-05

The four open questions answered (Q&A 5–8): 🌍 for both views; the invitation gets a **🔥 Trending** button; ↑,
Backspace, Alt+↑ and the breadcrumb's *GIPHY* keep showing the trending, an explicit action; a rating change
forgets the kept lists and shows the invitation, no call. The buttons' tooltips are dropped, the README keeping
what they said. No question left.

### Iteration 3 — 2026-11-05

User request: a **GIPHY** submenu of the ⚙ menu, a checkbox per view to show or hide it, the API key entry
moved into it. Each view an app setting, shown by default; a hidden view out of the drop-down and of the
Ctrl+F cycle. Two questions opened: the rating's entry, and where the explorer goes when the view shown is
hidden.

### Iteration 4 — 2026-11-05

Q&A 9–10 answered: the content rating moves into the GIPHY submenu too, under the key; hiding the view shown —
or a hidden view saved at start-up — gives the folder view. No question left.

### Iteration 5 — 2026-11-05 — ✅ Implemented

Go given: code and documentation (no test project), in the worktree `.claude/worktrees/giphy-gif-sticker-views`,
branch `feature/giphy-gif-sticker-views`.

### Iteration 6 — 2026-11-05 — 🧭 Implementation choices

No rule broken. The choices the design left open:

- **The breadcrumb** names the view (`GIPHY GIFs` / `GIPHY Stickers`) as its first segment, where it said `GIPHY`;
  while the view invites to search, that name alone, then the calls of the hour.
- **The invitation's text**: *Type a search, then press Enter — nothing is asked of GIPHY before. Enter on an
  empty box, or the button below, shows the trending.* (French: *Tapez une recherche, puis Entrée — rien n'est
  demandé à GIPHY avant…*). Its button **🔥 Trending** / **Tendances** does what ↑ does.
- **The submenu's entries** keep their texts — *GIPHY API key…*, *GIPHY content rating* — though under a *GIPHY*
  submenu; the two new toggles are named after the views, *GIPHY GIFs* / *GIPHY Stickers* (French *GIPHY GIF* /
  *GIPHY Stickers*), with a tooltip each. 🌍 on the submenu and on both toggles.
- **The removed buttons' strings** (`ExplorerAccGiphy*`, `ExplorerTipGiphyGifs` / `Stickers`) and the
  `UiIcons.Sticker` icon, used by them alone, are deleted; `ExplorerModeGiphy` gives way to
  `ExplorerModeGiphyGifs` / `ExplorerModeGiphyStickers`.
- **The saved setting**: two keys, `GiphyGifsShown` and `GiphyStickersShown` (1 / 0, missing → shown); a saved
  `Giphy` view read as `GiphyGifs` (`AppSettings.OlderGiphyMode`).
- **A call running when the view changes** is cancelled and its answer dropped, so a GIFs answer never lands in
  the Stickers view (`DropGiphyCall`, also on a new key or rating).
- **Two code commits**, not three: the two views and the no-call rule share the same lines of
  `FileExplorerPanel.Giphy.cs` (the toggle's removal, the kind per view); the ⚙ submenu is its own commit.

### Iteration 7 — 2026-11-05 — ⚙️ Post-implementation — Merged with the favorites at start-up

The merge into `main` met `bbfb145` (another session): every start now opens on the favorites, the saved view no
longer read — `AppSettings.ExplorerMode` removed there. Resolved by keeping that: the older `Giphy` name's reading
(`AppSettings.OlderGiphyMode`) went with the getter, and the fallback for a hidden view saved at start-up is moot.
The GIPHY views shown are still set before the start-up view (`MainForm`). The glossary rows took both changes.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5 | 2026-11-05 | The two views and the no-call rule, then the ⚙ submenu — two commits |
| Unit tests | 5 | 2026-11-05 | Not applicable: the app has no test project |
| README | 5 | 2026-11-05 | With RULES.md, GLOSSARY, SHORTCUTS, in English and French — one commit |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where to record this work: a post-implementation iteration of `20261105-online-library-giphy.md`, or a new workfile? | A new workfile | 2026-11-05 |
| 2 | How the two categories show | Two views of the views drop-down, both in the Ctrl+F cycle | 2026-11-05 |
| 3 | What a GIPHY view shows when entered without a search validated | Its last results kept for the session, else an invitation | 2026-11-05 |
| 4 | Depth of the exploration | Straightforward: one pass | 2026-11-05 |
| 5 | The views' icons | 🌍 for both | 2026-11-05 |
| 6 | The invitation's form | Its text and a Trending button | 2026-11-05 |
| 7 | ↑ / Backspace / Alt+↑ / breadcrumb *GIPHY* | The trending, an explicit action — a call unless kept | 2026-11-05 |
| 8 | The rating changed while a view shows | The kept lists forgotten, the invitation shown | 2026-11-05 |
| 9 | The content rating's entry: into the GIPHY submenu, or left where it is? | Moved into it | 2026-11-05 |
| 10 | Hiding the view shown: the folder view, or the view before it? | The folder view | 2026-11-05 |

---

*Last updated: 2026-11-05*
