# Paste Text Content

> Working document — text pasted with `Ctrl+V` or dragged from another app becomes a cell's
> image, rendered like the preview of a text file, rich text included.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a text-only clipboard is ignored (`Nothing to paste: the clipboard holds no image.`), and a
text selection dragged from another app is refused (no-drop cursor). The goal: both produce an
image in a cell, **rendered like the preview of a dropped text file**, keeping the formatting
when the source provides rich text (HTML / RTF).

Relevant components:

| Component | Role today |
|---|---|
| `UI/MainForm.cs` — `ProcessCmdKey` (l. 318), `Paste()` (l. 413) | `Ctrl+V`: file drop list first, else a bitmap, else the "nothing to paste" status |
| `UI/MainForm.cs` — `OnDragEnter` / `OnPreviewDragOver` / `OnDragDrop` / `DropCell` (l. 342-373) | External drop: `DataFormats.FileDrop` only; onto a cell replaces it, elsewhere is added like a paste |
| `UI/GridPreview.cs` — `Add` (l. 250), `ExcessTarget` (l. 849), `Replace` (l. 938) | Placement: free slots first, then the replace rule (selected cell, else the last) |
| `Imaging/TextPages.cs` — `TryOpen(path, pageSize)` (l. 96), `TryReadText` (l. 214) | Text preview: reads a file only; the layout from l. 104 on is pure string work |
| `Imaging/ImageLoader.cs` — `TryLoadFile`, `TryPages`, `FromImage` | Builds a `SourceImage` (bitmap, `FilePath`, `PageSource`) |
| `Composition/SourceImage.cs` | `FilePath` is nullable; its 4 consumers already handle `null` |

The cell-swap drag is mouse-message based, not OLE: an external text drop cannot collide with it.
No text input exists in the window, so the global `Ctrl+V` shortcut takes nothing away.

---

## Sources and Targeting

| Gesture | Target cell |
|---|---|
| `Ctrl+V` | **The same rule as pasting an image**: the first free slot; grid full → replaces the selected cell, else the last one |
| Text dropped onto a cell | Replaces that cell (the file-drop rule) |
| Text dropped onto the drop zone or elsewhere in the window | Added like a paste (the file-drop rule) |

- A drop shows the copy cursor and the drop-target highlight for text exactly as for files.
- Replacing a cell's image resets its effects (RULES.md) — implicit: the pasted text is a new
  `SourceImage`, which carries no effect.
- A pasted text has no file behind it: `FilePath` is `null` (default save folder falls back to
  Pictures, no sound — already handled).
- A text that looks like a file path or a URL is **rendered as text**, never loaded.

## Clipboard / Drag Data Priority

In order, the first one present wins:

1. File drop list — unchanged.
2. Bitmap (`Ctrl+V` only) — unchanged. *(see Open Questions: image vs text)*
3. Rich text — *(format order and fidelity: see Open Questions)*.
4. Plain text (`UnicodeText`, else `Text`).

- Empty or whitespace-only text → nothing is added, status message
  `Nothing to paste: the clipboard holds no image or text.`
- Same size limit as a text file: at most 1 M characters, else a status message saying the text
  is too long.

## Rendering

- **Exactly the text-file preview** (`TextPages`): the same font (Consolas), ink and paper, the
  same fit rule (largest size in [24, 96] px at which the whole text fits one page, else
  paginated at 24 px), the same pages shaped like the cell, the same auto-scroll for long texts,
  the same re-layout on cell changes keeping the reading position.
- `TextPages` gets a second entry point taking the text in memory, next to `TryOpen(path, …)`;
  the file entry point keeps its sniffing and decoding, both share the layout.
- Line endings normalized, tabs expanded, trailing blank lines trimmed — as for a file.
- **Rich text**: *(to be settled — see Open Questions)*.

## README

- Line 10: paste / drop text too, not only images.
- *Previews* section: a pasted or dropped text is rendered like a text file (and its rich-text
  rendering).
- *Adding several files at once* rule: unchanged, a text counts as one image.

---

## Test Impact

**No unit test project exists**; the standing choice of previous workfiles is manual checks
*(to be confirmed — see Open Questions)*.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `Ctrl+V` of plain text: first free slot, rendered like a `.txt` | — (manual) | — |
| `Ctrl+V` of text, grid full: replaces the selected cell, its effects reset | — (manual) | — |
| Text dragged from another app onto a cell replaces it; elsewhere, added | — (manual) | — |
| Copy cursor + drop highlight while dragging text | — (manual) | — |
| Rich text from a browser (HTML) and from Word (RTF) keeps its formatting | — (manual) | — |
| Long pasted text paginates and auto-scrolls; re-laid out on layout change | — (manual) | — |
| Whitespace-only clipboard → status message, nothing added | — (manual) | — |
| Files and images still pasted / dropped as before | — (manual) | — |

---

## Open Questions

- [ ] Rich text: which formatting is kept?
- [ ] Colors that vanish on white paper (text copied from a dark theme, e.g. VS Code): what happens?
- [ ] `Ctrl+V` when the clipboard holds **both** a bitmap and text (Excel cells, some apps): which wins?
- [ ] Image dragged from a browser (no real file, only its URL as text + HTML): refused as today, or rendered as text?
- [ ] Tests: manual checks as in previous workfiles, or create a test project?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-25

Scoping answered by the user: pasted text is rendered **like a `.txt` preview**; `Ctrl+V`
targets cells **like an image paste**; the scope covers **plain `Ctrl+V`, text drag & drop from
another app, and rich text (HTML / RTF)** — multi-cell splitting is out. Subject judged
straightforward: one scout pass (paste handling, text preview, drop handling).

Findings: `Paste()` reads only a file drop list or a bitmap; external drop accepts only
`FileDrop`; `TextPages` reads a file only but lays out plain strings; no rich-text rendering
exists anywhere; `FilePath = null` is already handled. Initial design above; rich text fidelity,
color contrast, bitmap-vs-text priority, browser image drags and tests left open.

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
| 1 | What should `Ctrl+V` produce when the clipboard holds text only? | Preview like a `.txt` (same preview engine) | 2026-09-25 |
| 2 | Which cell receives the pasted text? | Same rule as `Ctrl+V` of an image | 2026-09-25 |
| 3 | What is in scope? | Plain `Ctrl+V`, text drag & drop, rich text (HTML / RTF) — not multi-cell | 2026-09-25 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-09-25 |
| 5 | Rich text: which formatting is kept? | | |
| 6 | Colors that vanish on white paper: what happens? | | |
| 7 | Bitmap and text both in the clipboard: which wins? | | |
| 8 | Browser image drag (URL + HTML, no file): refused or rendered as text? | | |
| 9 | Tests: manual checks or a test project? | | |

---

*Last updated: 2026-09-25*
