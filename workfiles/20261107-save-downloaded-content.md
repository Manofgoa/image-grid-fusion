# Save Downloaded Content

> Working document — a **Save…** entry in the right-click menu of every downloaded content (URL zone,
> GIPHY, a cell holding one), copying its file where the user chooses.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app downloads content into the temp folder — the URL zone (X / Twitter, YouTube and other sites
through yt-dlp, direct links) into `%TEMP%\ImageGridFusion\url-downloads\`, the GIPHY views into their
own kebab-case folder — emptied at the next start-up. Today the only way to keep such a file is the
heart (a favorite, kept next to the exe) or *Open file location* then a copy by hand.

The feature: a right-click on the image of a downloaded content offers **Save…**, opening Windows'
*Save As* dialog, its folder the **file explorer's open folder** (the base folder at first), so the
file lands where the explorer browses.

Three places show it:

| Place | Shown | Enabled |
|---|---|---|
| The **URL zone**'s preview | Always in its menu (the menu itself opens only once the media is ready) | Its file exists |
| A **GIPHY tile** (GIFs and Stickers views) | Always — the tile's menu, refused today for an online row, opens for it | Only once the result is downloaded (its ✓); **greyed before** |
| A **cell** whose image is a downloaded content | Only for such a cell | Its file exists, not exporting |

---

## Behaviour

### The Save As Dialog

- Windows' standard `SaveFileDialog`, as *Save upscaled image…* uses (`MainForm.SaveUpscaledAsync`):
  overwrite prompt on, its filter the file's own type (`*.mp4`, `*.gif`, `*.png`…) then *All files*.
- **Initial folder**: the folder view's **open folder** — `Path.Combine(BaseFolder, OpenFolder)` —
  whatever view the explorer shows; inside a **secondary source**, the folder browsed in that source;
  the base folder while none is open; *Pictures* when no base folder is set or the folder is gone.
  One method gives it (`FileExplorerPanel.BrowsedFolder`, name decided at implementation).
- **Default file name**: the downloaded file's name, as the temp folder holds it
  (`UrlDownloads.PathOf` — the stem and the height —, `GiphyFiles.NameOf` — `<title>-giphy-<id>.<ext>`).
- **What is written**: a **plain copy of the downloaded file** (`File.Copy`, overwrite after the
  dialog's confirmation) — never re-encoded, no effect of the cell applied. A GIPHY GIF is its MP4, a
  sticker its GIF, as downloaded.
- The status line says where it was saved, or why it failed (the explorer's / the main window's).
- Out of the undo history. The URL zone and the tile are left as they are; a **cell adopts the saved
  file** (`SourceImage.AdoptFile`): a heart or a preset made afterwards points to the kept file, not to
  the temp one emptied at the next start.

### URL Zone

- A new `UrlZone.FileAction.Save` and its menu item, after *Open file location* (or before it — a
  placement decided at implementation), raised through `FileActionRequested(path, action)` and run by
  `FileExplorerPanel.RunUrlFileActionAsync` like the others.

### GIPHY Tiles

- The tile menu's `Opening` (`FileExplorerPanel.cs:522`) refuses an online row today
  (`row.Online is not null`). For an online row it now opens a menu holding **Save…** only — the file
  rows' entries (copy, open location, heart…) do not apply to a result.
- Downloaded = `_giphyFetched` holds the row's key and the file exists; otherwise the item is greyed.
- RULES.md § Online Libraries (*"its menu does not open"*) changes accordingly.

### Cells

- In `MainForm.ShowCellMenu` (`MainForm.cs:6195`), next to *Save upscaled image…*: visible when the
  cell's `SourceImage.FilePath` lies directly in `UrlDownloads.Folder`, `GiphyFiles.Folder` or
  `GiphyFiles.FavoritesFolder` (a hearted GIPHY result) — one test,
  `DownloadedContent.Holds(path)` (name decided at implementation), shared by every place.
- Disabled while exporting, like its neighbour.

### The App's Other Save Dialogs

Every save dialog opens on the **same initial folder** as Save… (§ The Save As Dialog), one method:

| Dialog | Today | Now |
|---|---|---|
| The grid's **Save** — the button and its ▾ entries (PNG max sizes…) | `MainForm.ExportFolder`: the last folder an export was saved into, else the first cell's file's folder, else *Pictures* | The explorer's open folder |
| **Save last…** (the last video) | `MainForm.ExportFolder` | The explorer's open folder |
| **Save upscaled image…** | Its image's folder | The explorer's open folder |

- The last export folder is **no longer remembered**: `AppSettings.ExportFolder` / `SaveExportFolder`
  and the `RememberFolder` calls for it go; a `LastExportFolder` key left in `settings.json` is ignored.
- `MainForm.DefaultSaveFolder` (the first cell's file's folder) goes with it if nothing else reads it.
- The open dialogs (*Browse…*, the soundtrack) keep their own remembered folders: they are not saves.

### Out of Scope

- A **layer** or the **background image** holding a downloaded content: no Save… in the layer list's
  menu nor the Grid background's options.

### Texts and Icon

- New `Strings.resx` / `Strings.fr.resx` entries: the menu label, the dialog title, the status
  messages — French terms of `GLOSSARY.fr.md`.
- Icon: the existing `UiIcons.Save` (💾), shared by every place (RULES.md § Icons › One icon per
  action).

### After Saving

- The explorer has no file watcher: a file saved **under the base folder** is **added to the index
  alone** at once — its entry (creation date, size) written, the folder view shown again if it lists
  that folder — with no full rescan; its content passes run with the next ones. A file saved elsewhere
  changes nothing in the explorer.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A path in `url-downloads\`, the GIPHY download folder or `giphy-favorites\` is downloaded content; one elsewhere (base folder, a subfolder, a sibling folder sharing the prefix) is not | `tests/ImageGridFusion.Tests/UI/DownloadedContentTests.cs` | Create |
| A file saved under the base folder is added to the index alone (its entry present, nothing else rescanned); one saved outside it is not | `tests/ImageGridFusion.Tests/Explorer/` — the index's test file, located at implementation | Create / Update |
| The initial folder: base + open folder; the base folder when none is open; *Pictures* without a base folder or when the folder is gone | `tests/ImageGridFusion.Tests/UI/DownloadedContentTests.cs` | Create |

The menus and the dialog themselves are UI, checked by hand.

---

## Open Questions

- [x] ~~Label: **Save…** as asked, or **Save as…** / **Save file…** to set it apart from the grid's Save button?~~ → **Save…** / **Enregistrer…**, as asked; what matters is the folder
- [x] ~~After saving into the base folder: the file shown at once (indexed alone / the open folder rescanned), or only after 🔄 as today?~~ → Shown at once: the saved file is added to the index alone
- [x] ~~A cell from download: does it **adopt** the saved file (`SourceImage.AdoptFile`), so a heart or a preset then points to the kept file rather than the temp one emptied at next start?~~ → Yes, the cell adopts the saved file
- [x] ~~A **GIPHY favorite** (hearted, in `giphy-favorites\` next to the exe) in a cell: also downloaded content offering Save…?~~ → Yes
- [x] ~~Should the grid's existing **Save** button (and its ▾ entries) also open its dialog on the explorer's open folder, as this Save… does?~~ → Yes: every save dialog opens on the explorer's open folder; the last export folder is no longer remembered
- [x] ~~A secondary source open in the folder view (`OpenFolder` is then `""`): initial folder the source's folder being browsed, or the base folder?~~ → The folder browsed in the source
- [x] ~~A **layer** or the **background image** holding downloaded content: Save… in the layer list's menu too, or out of scope?~~ → Out of scope
- [x] ~~Default file name for GIPHY: kept as `<title>-giphy-<id>.<ext>`, or the title alone?~~ → Kept as it is, `<title>-giphy-<id>.<ext>`

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-07

Initial design from the user's request and the scoping batch: Save… in the URL zone's preview menu,
on GIPHY tiles (greyed until downloaded) and in a downloaded cell's menu; a plain copy through the
standard Save As dialog, its initial folder the folder view's open folder. Exploration (one scout
pass): the URL zone already has a menu routed through `FileActionRequested`; the tile menu refuses
online rows at its `Opening`; `_giphyFetched` holds a downloaded result's path; `SourceImage.FilePath`
tells a downloaded cell by its folder; no file watcher in the explorer.

### Iteration 2 — 2026-11-07

First batch of open questions answered: the label stays **Save…** as asked (the user: what matters
is pointing at the right folder); a file saved under the base folder is added to the index alone, at
once; a downloaded cell adopts the saved file; a hearted GIPHY result in a cell offers Save… too. New
question from the label answer: should the grid's own Save also open on the explorer's open folder?

### Iteration 3 — 2026-11-07

Second batch: inside a secondary source, the initial folder is the folder browsed there; layers and
the background image are out of scope; GIPHY's default name stays `<title>-giphy-<id>.<ext>`. The
grid's Save: the user answered *yes* while asking what it did — it opens on the **last folder an
export was saved into** (`AppSettings.ExportFolder`, `LastExportFolder` in `settings.json`), else the
folder of the first cell's file, else *Pictures* (`MainForm.ExportFolder`, also used by *Save last…*);
*Save upscaled image…* opens on its image's folder. Confirmation asked with that in hand.

### Iteration 4 — 2026-11-07

Confirmed: every save dialog — the grid's Save and its ▾ entries, *Save last…*, *Save upscaled
image…* — opens on the explorer's open folder, like the new Save…; the last export folder is no longer
remembered. New section § The App's Other Save Dialogs.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README (EN + FR) | | | |
| GLOSSARY (EN + FR) — URL zone, GIPHY view rows | | | |
| RULES.md — § Content From a URL, § Online Libraries | | | |
| Strings.resx + Strings.fr.resx | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does Save… show? | URL zone preview, GIPHY tiles, cells of the grid | 2026-11-07 |
| 2 | A GIPHY tile not downloaded yet? | Save… greyed until downloaded | 2026-11-07 |
| 3 | The dialog's initial folder? | The folder view's open folder | 2026-11-07 |
| 4 | Exploration depth? | Straightforward — one scout pass | 2026-11-07 |
| 5 | Label: Save…, Save as… or Save file…? | "If it already exists, I don't get the question — it must point to the right folder" → kept **Save…**; clarification asked (#12) | 2026-11-07 |
| 6 | Saved file shown in the explorer at once, or after 🔄? | At once, added to the index | 2026-11-07 |
| 7 | A downloaded cell adopts the saved file? | Yes | 2026-11-07 |
| 8 | A GIPHY favorite in a cell offers Save… too? | Yes | 2026-11-07 |
| 9 | Initial folder while a secondary source is open? | The folder browsed in the source | 2026-11-07 |
| 10 | Save… for a layer / the background image? | Out of scope | 2026-11-07 |
| 11 | GIPHY default file name? | Kept as it is | 2026-11-07 |
| 12 | The grid's Save button opens on the explorer's open folder too? | "Yes — what did it do until now?" → today's behaviour explained, confirmation asked (#13) | 2026-11-07 |
| 13 | Knowing the grid's Save remembers its last folder: replace that by the open folder? | Yes, the explorer's open folder | 2026-11-07 |

---

*Last updated: 2026-11-07*
