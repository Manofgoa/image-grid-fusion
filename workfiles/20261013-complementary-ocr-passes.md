# Complementary OCR Passes

> Working document — further OCR passes after the Windows OCR, so the content search finds the text
> Windows OCR misses (curved, stylised, inverted), and a search that ignores the spaces the OCR put
> between letters.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The content search (`workfiles/20260926-ocr-search.md`) reads one OCR text per image: Windows OCR
(`Windows.Media.Ocr`, fr + en) in `Explorer/ContentExtractor.cs`. It misses a lot: in the user's index,
**1 436 of 2 243 images (64 %) have no text at all**. Many of them really have no text (photos,
portraits), but some do and are not read — `X:\Dropbox\Outils RS\Outils RS Wojak\20220321_132643.jpg`,
a round badge reading *I SUPPORT THE CURRENT THING* on a circle, gives nothing.

The work adds:

- **Two more indexing passes**, run one after the other over **every image** once the Windows pass is
  done, each with **its own progress bar**: **RapidOCR** (PaddleOCR PP-OCRv5 models through ONNX
  Runtime), then **Reshaped** (Windows OCR on reshaped copies of the image — the ring unrolled, the
  colours inverted).
- **A spaces-ignored match in the search**: the typed word compared with the OCR text with its spaces
  removed, so letters the OCR read apart (`S U P P O R T`, `SUP PORT`) are found. Not an OCR pass: it
  works on the whole index at once, with no bar.
- **The RapidOCR models downloaded on demand**, offered when the OCR is turned on in the ⚙ menu.

The search keeps working while the passes run, as it does today: the results are refreshed every few
seconds as the texts arrive.

### Prototype (scratchpad, 2026-10-13)

On the badge above (487 × 487):

| Variant | Windows OCR | RapidOCR |
|---|---|---|
| Raw | nothing | `SUPPORT` (1.00) |
| ×3, inverted | junk | `I SUPPORT` (0.90) |
| Ring unrolled, clockwise, outer edge on top | `1 SUPPORT` (+ upside-down junk) | fragments, 2 s |
| Ring unrolled, counter-clockwise, inner edge on top | `TH CURRENT THING` (+ junk) | fragments, 2 s |
| Rotations by 30° steps | junk | `SUPPORT`, `I SUPPORT` at some angles |

On 24 images of the 1 436 without text, picked at random: RapidOCR mostly returned nothing or
low-confidence noise (`T` 0.52, `a` 0.56), plus a few real finds (`TFIINFO` 1.00 on a TF1 INFO logo,
`HEN` / `VR` 1.00, pieces of names on an X screenshot). RapidOCR takes **0.1 to 0.6 s per image** on
the CPU; models loaded in ~0.5 s.

---

## Indexing Passes

| # | Pass | Engine | Runs on | Needs |
|---|---|---|---|---|
| 1 | **Windows** | Windows OCR, fr + en — today's pass, unchanged | Images, a GIF's first frame, a PDF's first page — what `ContentExtractor.KindOf` calls `Image` / `Pdf` | Nothing new |
| 2 | **RapidOCR** | `RapidOcrNet` (PP-OCRv5 latin: detection, orientation, recognition) | The same files, every one of them | The models in `ocr-models\` |
| 3 | **Reshaped** | Windows OCR on reshaped copies (§ Reshaped Pass) | The same files, every one of them | Nothing new |

- **One worker, the passes in order**: pass 1 over every file that needs it, then pass 2, then pass 3.
  A rescan that brings new or changed files runs pass 1 on them first, then carries on. One loop, one
  save cadence, one pause rule (`FileExplorerPanel.Extract`): paused while the thumbnails load or the
  user types, saved every 50 files / 30 s and at the end, refreshed every few seconds.
- **The texts add up**: each pass keeps its own text and word boxes; the search reads them all. A
  pass never replaces another's text.
- **A changed file starts over**: when its stamp (size, last write) changes, pass 1 re-reads it and
  its pass 2 and 3 texts are dropped, to be redone.
- **Pass 2 without its models** is skipped: its files stay *not done*, its bar hidden, and it runs as
  soon as the models are there (downloaded, then the extraction restarted).
- **Rebuild content** (⚙ menu, `_rebuildContent`) forgets and redoes every pass.
- The RapidOCR engine is created when pass 2 starts and disposed when it ends (memory).

### RapidOCR Pass

- `RapidOcr.InitModels(<paths in ocr-models\>)`, then `Detect(bitmap)` on the image decoded as the
  Windows pass decodes it (EXIF orientation respected, downscaled past a maximum side).
- **Noise filter**: a detected text block is kept when its characters' average score is **≥ 0.8** and it
  holds **at least 2 letters or digits**. On the sample this drops `T` 0.52, `a` 0.56, `IV.` 0.68 and
  keeps `TFIINFO`, `SUPPORT`, `HEN`.
- Word boxes: the word-level polygons when RapidOCR gives them, else the block's polygon, each turned
  into its bounding rectangle in fractions of the image (the `WordBox` format).

### Reshaped Pass

Windows OCR — the pass 1 engines — run on copies of the image, each copy's words mapped back to the
image's coordinates:

| Copy | What it catches | Mapping back |
|---|---|---|
| **Inverted**, upscaled ×2 (longer side capped) | Light text on a dark background (`T F 1 INFO` on the TF1 logo) | Scale |
| **Ring unrolled**, clockwise, outer edge on top | Text along the top of a round badge | Each box's corners through the polar transform, bounding rectangle |
| **Ring unrolled**, counter-clockwise, inner edge on top | Text along the bottom of a round badge | Same |

- The ring is the band between **0.68 and 1.0** of the image's inscribed circle, unrolled **twice
  around**, so a text across the seam shows whole once.
- Boxes of the unrolled copies span an arc once mapped back: their bounding rectangle is large — the
  arrow points at its edge, which is on the text.

### Index Format

`index\files.content` goes to **`ImageGridFusion content 3`**:

```
relPath \t size \t ticks \t boxes1 \t text1 \t boxes2 \t text2 \t boxes3 \t text3
```

- `boxesN` / `textN`: as today's `boxes` / `text` for each pass. A pass **not done** writes `?` in its
  boxes column and nothing in its text; a pass done with no words writes an empty boxes column, as
  today. A file read as text keeps `-` and no later pass.
- **Content 2 is read, not discarded**: its text and boxes become pass 1's, passes 2 and 3 *not done* —
  the existing index is not re-read by Windows OCR. Content 1 is read as today (its images redone).
- `ContentText` holds one immutable slot per pass; a pass finished swaps in a new `ContentText`
  (the search reads the dictionary from the UI thread). `MaxLength` and `MaxBoxes` apply per pass.
- `ContentIndex.IsCurrent(entry, pass)` replaces `IsCurrent(entry)` in the queue.

---

## Progress Bars

- **One bar per pass**, stacked in the explorer panel's progress row (today one `IndexingBar` in row 2
  of `_content`): Windows, RapidOCR, Reshaped, top to bottom. The scan's sweeping bar stays the first
  one.
- Each bar shows its pass's share of the files **done over the whole index** — so while pass 2 runs,
  bar 1 stands full and bar 3 at what was done before. A bar shows while its pass has files left or
  runs, and hides once its pass is complete; pass 2's is hidden without the models.
- The status line names the running pass: `OCR 2/3 · RapidOCR: 120/2 243 (20220321_132643.jpg)`.

---

## Spaces-Ignored Match

In the search, not in the index:

- A query word not found in a pass's folded text is looked for in that text **with its whitespace
  removed** (`ContentText` keeps the stripped folded text next to `Folded`, computed once).
- Only for query words of **3 characters or more** — shorter ones match everywhere once the spaces go.
- A file found this way is in the **content tier, after** the files found by an ordinary content match
  (`FileSearch.Rank`).
- **Details view**: the excerpt is taken around the **run of consecutive words** whose concatenation
  holds the word (`ContentIndex.WordsAround` fallback), and the whole run is highlighted.
- **Arrow**: `ContentIndex.SpotOf` falls back on the boxes of one pass, concatenating consecutive
  boxes' folded words until the word is found, and returns the **union of the boxes** holding the
  matched letters. `ThumbnailGrid.PaintArrow` is unchanged.
- The arrow and the excerpt read every pass, pass 1 first.

---

## OCR Models Download

- Folder: **`ocr-models\` next to the exe** (`OcrModels.FolderName`, § Folder Names of `../CLAUDE.md`),
  holding the four files RapidOCR needs: `ch_PP-OCRv5_mobile_det.onnx`,
  `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx`, `latin_PP-OCRv5_rec_mobile_infer.onnx`,
  `ppocrv5_latin_dict.txt` (~13 MB). Available when all four are there (`OcrModels.Find`).
- Source: the **`RapidOcrNet` package of the referenced version on nuget.org**
  (`https://api.nuget.org/v3-flatcontainer/rapidocrnet/<version>/rapidocrnet.<version>.nupkg`), its
  `models/v5/` entries extracted — the models always match the library. Downloaded with
  `Web/Downloader` (partial file, renamed once complete).
- The package's own build target copying `models\v5\` next to the exe is **switched off**: the models
  are never shipped.
- **When it is offered**: when *Search file contents (OCR)* is turned on in the ⚙ menu and the models
  are missing — a Yes / No message saying what is downloaded, its size and where. **Yes**: downloaded,
  the progress on the status line, then the extraction restarted so pass 2 runs. **No**: passes 1 and 3
  only; offered again the next time the option is turned on.
- A failed download says so in the status line; nothing half-written stays in `ocr-models\`.

---

## Rules and Docs

- **RULES.md**: a new § *Content OCR Passes* — the passes, their order, the texts adding up, the format
  rule (a new pass adds its columns and is read as *not done* from an older file), the spaces-ignored
  match, the models downloaded on demand and never shipped.
- **GLOSSARY.md / GLOSSARY.fr.md**: *OCR pass* (*passe OCR*), *OCR models* (*modèles OCR*); the
  *Index* row mentions the passes.
- **README.md / README.fr.md**: the content search reads several passes; the models offered when the OCR
  is turned on; the third-party components (RapidOcrNet, PaddleOCR models, ONNX Runtime, SkiaSharp).

---

## Test Impact

The repository has no test project (CONTRIBUTING § Build): nothing is created. The behaviours below
are checked by hand in the app.

| Behaviour to check | How |
|---|---|
| The badge is found by `support` and `current` | Search after the passes ran |
| `S U P P O R T`-like texts found by the spaces-ignored match, the arrow on the letters | A file whose pass text has split letters |
| A content 2 index is read without redoing pass 1 | Start with the existing `files.content` |
| Three bars filling one after the other, the status line naming the pass | Watch a first run |
| The download offered when the OCR is turned on, No keeps passes 1 and 3 | ⚙ menu with `ocr-models\` absent |
| Search results refreshed while the passes run | Search during the run |

---

## Open Questions

- [ ] **Download scope**: the models only (~13 MB downloaded; ONNX Runtime and SkiaSharp's native
      DLLs, ~28 MB, shipped with the exe), or the native DLLs downloaded too (the exe as light as
      today, but the ONNX Runtime package is large and loading DLLs from a downloaded folder needs a
      resolver)?
- [ ] **OCR already on**: an existing user with the OCR on never toggles it — offer the download once
      at start-up, add a ⚙ menu item *Download the OCR models…*, or only on the toggle?
- [ ] **Reshaped pass on every image**: the unrolled ring only makes sense on a round badge and gives
      upside-down junk elsewhere — run it on every image, or only on near-square images (ratio ≤ 1.25)?
- [ ] **RapidOCR noise filter**: average score ≥ 0.8 and ≥ 2 letters or digits — right threshold?
- [ ] **Spaces-ignored match**: 3 characters minimum, ranked after the ordinary content matches — right?
- [ ] **Bars**: three bars of the same blue, or one colour per pass?

---

## Design Iterations

### Iteration 1 — 2026-10-13

Initial design from the user's outline (passes in order, each with its bar, the letters joined into
words, the arrow recomputed or at worst at the start, the search usable during indexing), the
scratchpad prototype and the scoping answers: passes 2 and 3 on every image, the letters joined in the
search rather than as a pass, the models downloaded on demand — offered when the OCR is turned on in
the settings. Passes ordered RapidOCR then Reshaped, as advised.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project in the repository — does not apply |
| RULES | | | |
| README | | | |
| Glossary | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Is a more advanced complementary OCR possible for files like the badge? | Yes — several passes: the current one, then the letters joined into words (or one string without spaces), the arrow recomputed (at worst at the start), then RapidOCR, then the preprocessing; each pass with its progress bar; search possible during indexing | 2026-10-13 |
| 2 | Prototype on the file first? | Yes — if confirmed, create the workfile and start | 2026-10-13 |
| 3 | Which images do passes 2 and 3 run on? | All images | 2026-10-13 |
| 4 | Where does the letters-to-words step happen? | In the search | 2026-10-13 |
| 5 | How is RapidOCR delivered? | Downloaded on demand | 2026-10-13 |
| 6 | When is the download offered? | When the OCR mode is turned on in the settings | 2026-10-13 |
| 7 | Exploration depth? | Simple | 2026-10-13 |
| 8 | Download scope: the models only, or the native DLLs too? | | |
| 9 | OCR already on: how is the download offered? | | |
| 10 | Reshaped pass on every image, or near-square images only? | | |
| 11 | RapidOCR noise filter threshold? | | |
| 12 | Spaces-ignored match: minimum length and ranking? | | |
| 13 | Bars: one colour or one per pass? | | |

---

*Last updated: 2026-10-13*
