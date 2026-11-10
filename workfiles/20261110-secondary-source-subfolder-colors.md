# Secondary Source Subfolder Colors

> Working document — a secondary source's color carried inside it: every folder of its breadcrumb, and a
> pastel background for the explorer's list while it is open.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, inside a secondary source (`workfiles/20261105-secondary-file-sources.md` § Source Colors), only the
source's **name** in the breadcrumb takes its color: the folders below it keep the usual link blue
(`SystemColors.HotTrack`) and the open folder the usual black, and the list stays white — nothing tells at a glance,
deep in `Outils RS │ 2026 › Vendu › Congélateur Far`, that the folder shown is in a source rather than under the base
folder.

The goal: **everything below the source's root reads in its color** —

- the breadcrumb's segments from the source's name down to the open folder;
- the file explorer's list, on a **pastel** of the source's color instead of white.

The folder tiles inside a source are **not** changed: they keep the manila folder (Q&A #2).

Relevant code:

| Where | What |
|---|---|
| `UI/Breadcrumb.cs` — `SetPath`, `OnPaint` | `_sourceAt` / `_sourceColor`: today only `depth == _sourceAt` is drawn in the source's color |
| `UI/FileExplorerPanel.cs` — `ShowBreadcrumb` | Passes `sourceAt = 1` and `ViewSource.Colors.Text` |
| `UI/FileExplorerPanel.Sources.cs` — `ViewSource` | The source open in the folder view, `null` elsewhere |
| `UI/ThumbnailGrid.cs` — constructor, `OnPaint` | `BackColor = SystemColors.Window`, cleared with it at each paint |
| `UI/SourceColors.cs` — `SourceColor` | `Text`, `Back`, `Front`, `Tint` per palette entry |

---

## Breadcrumb

- **Every segment from the source's name down** is drawn in the source's color (`SourceColor.Text`): the source's
  name, every folder link below it, and the **open folder** — the last segment, today black (Q&A #1).
- The base folder's name, before the `│`, keeps the link blue: it is not in the source.
- The **separators** — the `│` after the base folder and every `›` — stay grey, as today.
- A link hovered is still **underlined**, in the source's color: the underline is what says it is a link.
- One rule in `Breadcrumb`: a segment at `depth >= _sourceAt` takes `_sourceColor`, links and open folder alike
  (`Breadcrumb.SegmentColor`, below). `SetPath`'s signature does not change.

## Pastel Background

- While a secondary source is **open in the folder view** (`FileExplorerPanel.ViewSource` not `null`), the list's
  background (`ThumbnailGrid.BackColor`) is the source's **pastel** instead of `SystemColors.Window` (Q&A #2); it goes
  back to white as soon as the source is left — its root's parent, another view, the search view.
- **The pastel is a member of `SourceColor`**, `Pastel`, computed once from `Text` — **6 % of it over white**
  (blue `#F0F5FB`, violet `#F6F3FB`), a tint rather than a color (Q&A #5) — one definition for the palette, a new
  color getting its pastel by itself.
- **The list alone** takes it — the tiles' grid and the Details view (Q&A #4): the breadcrumb and the status line keep
  their background, so their text in the source's color stays as legible as today.
- **What stays as it is**, drawn over the pastel: the tiles' checkerboard (transparency), the folder tiles' manila
  drawing, the selection and hover washes, the section headers, the Details view's rows and strips, the Loading… tile.
- **Set in one place**: where the breadcrumb is set for the folder view (`ShowBreadcrumb`), so every route that opens
  or leaves a folder sets both together; the views that never show a source (People, search, GIPHY) set white.

## Documentation

- `RULES.md` § Secondary Sources, *In its color*: "the folders inside it keep their usual color" becomes the
  breadcrumb rule above, plus the pastel.
- `README.md` / `README.fr.md` § File explorer, *Secondary sources*: "the folders inside it keep their usual color"
  replaced the same way, in both languages.
- `GLOSSARY.md` / `GLOSSARY.fr.md`, *Secondary source*: the colored places listed gain the breadcrumb's folders and the
  list's pastel background.
- `SHORTCUTS.md`: no key or gesture changes.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Each palette color's `Pastel` is 6 % of `Text` over white — every channel at least 235 — and keeps its hue: its strongest channel is `Text`'s | `tests/ImageGridFusion.Tests/Explorer/SecondarySourceColorsTests.cs` | Update |
| `Breadcrumb.SegmentColor`: a segment before `sourceAt` is a link blue (black when it is the open folder), every segment from `sourceAt` down — the open folder included — the source's color; without a source, as today | `tests/ImageGridFusion.Tests/UI/BreadcrumbColorsTests.cs` | Create |

---

## Open Questions

- [x] ~~Which rows take the pastel: the list alone, or the breadcrumb and the status line above it too?~~ → The list
  alone
- [x] ~~How strong is the pastel: very light (≈ 6 % of the color over white — blue `#F0F5FB`, violet `#F6F3FB`), or
  light (≈ 12 % — blue `#E0EAF7`, violet `#EDE8F6`)?~~ → Very light, 6 %

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-10

Initial design from the request ("every subfolder of a secondary source in the source's color") and the scoping
batch: the whole breadcrumb from the source's name down — the open folder included, the separators grey — in the
source's color; the folder tiles unchanged; instead, the list's background a pastel of the source's color while the
source is open. Two open questions: the pastel's extent and its strength.

### Iteration 2 — 2026-11-10

Both open questions answered: the pastel covers the list alone — the breadcrumb and the status line keep their
background — and is very light, 6 % of the source's color over white.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| GLOSSARY | | | |
| RULES | | | |
| SHORTCUTS | — | — | No key or gesture changes |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | In the breadcrumb inside a source, which segments take its color? | All of them: the folder links and the open folder; the separators stay grey | 2026-11-10 |
| 2 | Do the folder tiles inside a source change too? | Not the folders — instead, the explorer's background in a pastel of the color, in place of the white | 2026-11-10 |
| 3 | Is the subject straightforward or tricky? | Straightforward: one exploration pass | 2026-11-10 |
| 4 | Which rows take the pastel: the list alone, or the breadcrumb and status line too? | The list alone | 2026-11-10 |
| 5 | How strong is the pastel? | Very light, ≈ 6 % | 2026-11-10 |

---

*Last updated: 2026-11-10*
