# URL Zone Context Menu

> Working document — the right-click menu of the file explorer's tiles on the URL zone's preview, so
> a downloaded media (an X / Twitter video first) can be copied, its path copied, its folder opened.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The URL zone (`UI/UrlZone.cs`, `workfiles/20261009-url-content.md`) downloads the media behind a URL
into `%TEMP%\ImageGridFusion\url-downloads\` and shows its thumbnail in its preview (`UrlPreview`),
which is **dragged** into a cell — its only gesture today: no right-click menu.

The file explorer's tiles already have one (`FileExplorerPanel._menu`,
`workfiles/20261008-explorer-tile-copy-menu.md`, `workfiles/20261014-copy-full-path.md`):
**Copy** · **Copy PNG** · **Copy full path** · separator · **Open file location**. This task gives
the **same menu** to the URL zone's preview, acting on the **downloaded file**.

Components: `UI/UrlZone.cs` (the preview, its state, `_path`, `GoneCaption`),
`UI/FileExplorerPanel.cs` (the tile menu's actions, `ShowTransient`, the zone's wiring next to
`DragRequested`). Docs: `README.md` / `README.fr.md` § File explorer › URL zone.

No row of `workfiles/TODO-FEATURES.md` matches the request.

---

## Menu

| Preview state | Menu |
|---|---|
| **Ready** — a media downloaded, whatever the resolver (X post, direct link, yt-dlp) | Copy · Copy PNG · Copy full path · separator · Open file location |
| Empty, Busy (loading), Failed | No menu: the opening is cancelled |

- The **same entries, labels and order** as a file tile's menu — the label constants shared, not
  repeated. No `Ctrl+C` shown next to Copy: the preview takes no focus, so no shortcut reaches it.
- **No keyboard shortcut**: the menu is the only way in, as for Copy full path.

## What Each Entry Does

The **same behaviour as on a file tile**, on the zone's downloaded file (`UrlZone._path`):

| Entry | Does |
|---|---|
| Copy | The file itself on the clipboard (`FileDrop`), as `Ctrl+C` in the Explorer — pastable in a folder, a chat app, a browser |
| Copy PNG | Its image as a cell shows it when the file arrives (a video's frame at 10 % of its length), full size, as a bitmap and in the `PNG` clipboard format |
| Copy full path | Its full path as text, raw, no quotes |
| Open file location | The Explorer opened on `url-downloads\`, the file selected (`explorer /select,`) |

- **One implementation**: the tile actions become **path-based** — the copy, the PNG copy, the path
  copy and the Explorer opening take a full path — called by the tile menu with the selected row's
  path, after its `Exists` check (unchanged: a missing tile file leaves the index), and by the URL
  zone with its file.
- **The file gone** (the temp folder emptied by hand): nothing is done, and the zone shows
  `GoneCaption` — *The downloaded file is gone: load it again* — as its drag already does.
- The zone raises **one event** carrying the file and the entry chosen; `FileExplorerPanel` handles
  it with the shared actions, like `DragRequested`.

## Feedback

- Every entry reports in the **explorer's status line** (`ShowTransient`), with the tile menu's
  messages: `Copied: <name>`, `Copied as PNG: <name> (<w> × <h>)`, `Copied path: <full path>`,
  `No image to copy: <name>`, `Copy failed: <message>`, `Explorer could not be opened: <message>`.
  The zone's caption keeps `name · size` — it changes only for a file gone (`GoneCaption`).
- The preview's **tooltip** in the `Ready` state says the menu is there:
  `Drag into a cell · right-click for more`.

---

## Test Impact

None: the change is UI wiring — a context menu, the clipboard, the Explorer, a status message — and
the existing actions made path-based, with no new pure logic to pin, as for
`workfiles/20261008-explorer-tile-copy-menu.md` and `workfiles/20261014-copy-full-path.md`. Checked
by hand in the launched app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no testable logic) | — | — |

---

## Open Questions

- [x] ~~Which entries?~~ → The tile menu exactly: Copy · Copy PNG · Copy full path · separator ·
  Open file location.
- [x] ~~Open dir: the folder alone, or the file selected?~~ → The file selected, as Open file location.
- [x] ~~Every downloaded media, or X / Twitter only?~~ → Every media the zone loaded, whatever the
  resolver.
- [x] ~~Where does the feedback (`Copied: …`, `Copy failed: …`) show: the **explorer's status line**,
  as for a tile (`ShowTransient`), or the **zone's caption**, replacing its `name · size` for a few
  seconds?~~ → The explorer's status line, as for a tile.
- [x] ~~The preview's tooltip, `Drag into a cell`: unchanged, or mentioning the menu
  (`Drag into a cell · right-click for more`)?~~ → Mentioning the menu.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-14

Scoping: the tile menu exactly, Open file location selecting the file, on every media the zone
loads; the subject is simple, explored in one pass. Exploration found the preview with no menu, its
`Ready` state and `_path`, the drag's `GoneCaption` for a file gone, and the tile actions all reading
the selected row. Proposed: a `ContextMenuStrip` on the preview, opened in the `Ready` state only, the
tile actions made path-based and shared, one event from the zone to the panel, no shortcut. Left
open: where the feedback shows, and the tooltip.

### Iteration 2 — 2026-10-14

The feedback goes to the explorer's status line with the tile menu's messages, the zone's caption
keeping `name · size`; the preview's tooltip in the `Ready` state becomes
`Drag into a cell · right-click for more`. No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which entries in the preview's menu? | The tile menu exactly | 2026-10-14 |
| 2 | Open dir: the file selected, or the folder alone? | The file selected | 2026-10-14 |
| 3 | Every downloaded media, or X / Twitter only? | Every media loaded | 2026-10-14 |
| 4 | Is the subject simple, or tricky / long? | Simple | 2026-10-14 |
| 5 | Where does the feedback show? | The explorer's status line | 2026-10-14 |
| 6 | The preview's tooltip: unchanged, or mentioning the menu? | Mentioning the menu | 2026-10-14 |

---

*Last updated: 2026-10-14*
