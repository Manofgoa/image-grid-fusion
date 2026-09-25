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
2. Bitmap (`Ctrl+V` only) — unchanged: **the image wins over text**, so Excel cells, which bring
   both, still paste as an image.
3. Rich text: RTF first (Word, WordPad give it faithfully), else HTML (browsers).
4. Plain text (`UnicodeText`, else `Text`).

- A rich text whose visible text is empty (e.g. an HTML holding only an `<img>`) falls through
  to the plain text.
- **Image dragged from a browser** (a virtual file, its URL as text, an HTML `<img>`): no longer
  refused — it is **rendered as text**, i.e. its URL (the HTML has no visible text, so the
  plain text wins).
- Empty or whitespace-only text → nothing is added, status message
  `Nothing to paste: the clipboard holds no image or text.` (a drop:
  `Nothing added: the dropped text is empty.`)
- Same size limit as a text file: at most 1,048,576 characters of **read** text (after RTF / HTML
  parsing, not of the raw payload), else `Text too long: {n} characters, 1,048,576 at most.`
- `UI/TextData.cs` reads every form on the UI thread (the data object lives there); parsing,
  layout and the first render run off it, like a file's loading.

## Rendering

- **Exactly the text-file preview** (`TextPages`): the same font (Consolas), ink and paper, the
  same fit rule (largest size in [24, 96] px at which the whole text fits one page, else
  paginated at 24 px), the same pages shaped like the cell, the same auto-scroll for long texts,
  the same re-layout on cell changes keeping the reading position.
- `TextPages.TryCreate(StyledText, Size)` takes the text in memory, next to `TryOpen(path, …)`;
  the file entry point keeps its sniffing and decoding and hands a plain `StyledText` to it, so
  both share the layout. `ImageLoader.FromText` builds the `SourceImage` (`FilePath = null`).
- Line endings normalized, tabs expanded, trailing white space trimmed — as for a file, each
  character keeping its style (`StyledText.Normalize`).

### Rich Text

- **Styles kept, in the fixed font**: bold, italic, underline, strikethrough, text color,
  highlight (background) color. Font family and sizes of the source are **ignored** — the page
  stays in Consolas and the `.txt` fit rule is untouched (Consolas bold / italic keep the same
  advance, so the monospace layout holds).
- The RTF and HTML readers (`Imaging/RtfReader.cs`, `Imaging/HtmlReader.cs`) turn the source
  into a `StyledText`: the text and a style per character. `TextPages` lays the text out exactly
  like plain text, then draws each run of one style at its column, its highlight behind it.
- Plain text has no style array: it draws line by line, as before.
- **RTF**: `\b`, `\i`, every `\ul…` kind, `\strike`, `\cf`, `\highlight` / `\cb` / `\chcbpat`,
  `\plain`; `\par` / `\line` / `\row` break lines, `\tab` / `\cell` give tabs; `\u` with its
  fallback skipped, `\'hh` decoded in the `\ansicpg` code page. Font / style tables, pictures,
  headers, footers, field instructions and every `\*` destination are left out; a field's
  result (a link's text) is kept.
- **HTML**: the fragment the clipboard header points to (UTF-8 byte offsets), else the markup
  between the fragment comments. Styles from `b` / `strong` / `th` / `h1`–`h6` (bold), `i` /
  `em`…, `u` / `ins`, `s` / `del`, `mark` (yellow), `font color`, `bgcolor`, and the inline CSS
  `color`, `background(-color)`, `font-weight`, `font-style`, `text-decoration(-line)`,
  `white-space`, `display`. Blocks break lines; paragraphs, headings, top-level lists and
  tables are set apart by a blank line; list items get `• ` (numbered in an `ol`, indented when
  nested); table cells are separated by a tab. White space collapses, except in `pre` and
  `white-space: pre…`. `head`, `script`, `style`, `svg`… and `display: none` are skipped.

### Background and Contrast

- When **every visible character sits on a background** (e.g. VS Code's HTML, a dark theme), the
  most frequent of those backgrounds becomes the paper, and stops being a highlight where it
  was one; otherwise, white paper. Whitespace does not count.
- The default ink follows the paper: a text without its own color is drawn in the dark ink
  (`#222222`) on a light paper, in a light ink (`#DDDDDD`) on a dark one — dark meaning a
  perceived lightness under 128 / 255.
- Colors of the runs are kept as the source gives them — no correction. A translucent CSS color
  is laid over white; a fully transparent background lets the parent's show.

## README

- Line 10: paste / drop text too, not only images.
- *Previews* section: a pasted or dropped text is rendered like a text file (and its rich-text
  rendering).
- *Adding several files at once* rule: unchanged, a text counts as one image.

---

## Test Impact

**No unit test changes**: the solution has no test project, and the user chose **not to create
one** — every behaviour is checked manually. The manual checks to run:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `Ctrl+V` of plain text: first free slot, rendered like a `.txt` | — (manual) | — |
| `Ctrl+V` of text, grid full: replaces the selected cell, its effects reset | — (manual) | — |
| Text dragged from another app onto a cell replaces it; elsewhere, added | — (manual) | — |
| Copy cursor + drop highlight while dragging text | — (manual) | — |
| Rich text from a browser (HTML) and from Word (RTF) keeps bold / italic / underline / strike / colors, in Consolas | — (manual) | — |
| Text copied from VS Code (dark theme) shows on its dark background | — (manual) | — |
| Excel cells still paste as an image | — (manual) | — |
| Image dragged from a browser shows its URL as text | — (manual) | — |
| Long pasted text paginates and auto-scrolls; re-laid out on layout change | — (manual) | — |
| Whitespace-only clipboard → status message, nothing added | — (manual) | — |
| Files and images still pasted / dropped as before | — (manual) | — |

---

## Open Questions

- [x] ~~Rich text: which formatting is kept?~~ → Styles (bold, italic, underline, strike, text and highlight colors) in the fixed font; font family and sizes ignored
- [x] ~~Colors that vanish on white paper (text copied from a dark theme, e.g. VS Code): what happens?~~ → The page takes the source's background when it gives one for the whole text; else white paper
- [x] ~~`Ctrl+V` when the clipboard holds **both** a bitmap and text (Excel cells, some apps): which wins?~~ → The image, as today
- [x] ~~Image dragged from a browser (no real file, only its URL as text + HTML): refused as today, or rendered as text?~~ → Rendered as text (its URL)
- [x] ~~Tests: manual checks as in previous workfiles, or create a test project?~~ → Manual checks, no test project

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

### Iteration 2 — 2026-09-25

All open questions answered:

- Rich text keeps **styles in the fixed font** (no source font / size): the `.txt` engine is
  kept, it draws styled runs. RTF is read before HTML.
- Dark-theme sources: the page takes the **source's whole-text background**; the default ink
  follows the paper's lightness.
- Bitmap and text together: **the image wins**, as today.
- Browser image drag: **rendered as text** (its URL) — an HTML with no visible text falls
  through to the plain text.
- Tests: manual checks, no test project.

### Iteration 3 — 2026-09-26 — ✅ Implemented

Go given ("Go implémente", then *Code, tests and documentation*), after a first "No". Scope
frozen as the design sections stand in Iteration 2: code, plus the README; no unit tests, as
agreed. Work stays on `main` — the project's standing choice.

### Iteration 4 — 2026-09-26 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Paper rule, closest workable variant**: "one background for the whole text" became *every
  visible character sits on a background → the most frequent one is the paper*. An editor's
  HTML nests colored spans in a colored block; a strict "one single background" test would
  fail on the first inner highlight (a selection, a search match) and bring back the white
  paper under light text.
- Light default ink `#DDDDDD`, darkness threshold at a perceived lightness of 128.
- Translucent CSS colors laid over white; `transparent` backgrounds let the parent's show.
- HTML structure rendering (blank lines around paragraphs / headings / top-level lists /
  tables, `• ` bullets, numbered `ol`, two-space indent per nesting level, tab between cells,
  bold headings and `th`, yellow `mark`) — the design only said "as the source shows them".
- RTF: every `\*` destination and the listed tables skipped; a field's result kept, its
  instruction dropped; `\cb` and `\chcbpat` read as highlights next to `\highlight`.
- Drop of a blank text: its own message, `Nothing added: the dropped text is empty.`; the
  1,048,576-character limit applies to the read text, not to the raw RTF / HTML payload.
- Names: `Imaging/StyledText.cs` (with `TextStyle` and its `Builder`), `Imaging/RtfReader.cs`,
  `Imaging/HtmlReader.cs`, `UI/TextData.cs`; `TextPages.TryCreate`, `ImageLoader.FromText`.
- Build output: the app was running (`ImageGridFusion.exe` locked by another process), so every
  build went to a scratch output folder; the running instance was left alone.
- Checks: the readers and the rendering were run on sample RTF (Word-like), VS Code HTML and
  browser HTML through a throwaway console harness outside the repository — styles, `€` / `é`,
  bullets, tables, the dark paper all came out right. The manual checks of *Test Impact* in the
  running app (real clipboard, drags from Word / Chrome / VS Code / Excel) are still to be run.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-09-26 | Styled text rendering, RTF reader, HTML reader, paste / drop wiring — 4 commits |
| Unit tests | 3 | 2026-09-26 | None, as agreed (no test project); manual checks listed in *Test Impact*, to be run in the app |
| README | 3 | 2026-09-26 | Features, Adding images, new *Pasted text* section under Previews |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What should `Ctrl+V` produce when the clipboard holds text only? | Preview like a `.txt` (same preview engine) | 2026-09-25 |
| 2 | Which cell receives the pasted text? | Same rule as `Ctrl+V` of an image | 2026-09-25 |
| 3 | What is in scope? | Plain `Ctrl+V`, text drag & drop, rich text (HTML / RTF) — not multi-cell | 2026-09-25 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-09-25 |
| 5 | Rich text: which formatting is kept? | Styles in the fixed font; font and sizes ignored | 2026-09-25 |
| 6 | Colors that vanish on white paper: what happens? | The source's background, when it gives one for the whole text | 2026-09-25 |
| 7 | Bitmap and text both in the clipboard: which wins? | The image | 2026-09-25 |
| 8 | Browser image drag (URL + HTML, no file): refused or rendered as text? | Rendered as text | 2026-09-25 |
| 9 | Tests: manual checks or a test project? | Manual checks | 2026-09-25 |

---

*Last updated: 2026-09-26*
