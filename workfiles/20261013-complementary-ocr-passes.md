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

- **More indexing passes**, run one after the other over **every image** once the Windows pass is
  done, each with **its own progress bar**: **RapidOCR** (PaddleOCR PP-OCRv5 models through ONNX
  Runtime), then **Inverted** and **Ring** (Windows OCR on reshaped copies of the image — the colours
  inverted, the ring unrolled).
- **A spaces-ignored match in the search**: the typed word compared with the OCR text with its spaces
  removed, so letters the OCR read apart (`S U P P O R T`, `SUP PORT`) are found. Not an OCR pass: it
  works on the whole index at once, with no bar.
- **Less reliable words weigh less**: a file found only by words read on an odd shape (the Ring pass)
  or read with a low confidence (RapidOCR's *unsure* words) ranks after the files found on sure,
  straight text.
- **RapidOCR downloaded on demand** — its models and the native DLLs it runs on — offered when the OCR
  is turned on in the ⚙ menu, and from a ⚙ menu item while missing.

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
| 2 | **RapidOCR** | `RapidOcrNet` (PP-OCRv5 latin: detection, orientation, recognition) | The same files, every one of them | RapidOCR downloaded (§ RapidOCR Download) |
| 3 | **Inverted** | Windows OCR on the image's colours inverted (§ Reshaped Copies) | The same files, every one of them | Nothing new |
| 4 | **Ring** | Windows OCR on the image's ring unrolled (§ Reshaped Copies) | The same files, every one of them | Nothing new |

- **One worker, the passes in order**: pass 1 over every file that needs it, then pass 2, 3, 4.
  A rescan that brings new or changed files runs pass 1 on them first, then carries on. One loop, one
  save cadence, one pause rule (`FileExplorerPanel.Extract`): paused while the thumbnails load or the
  user types, saved every 50 files / 30 s and at the end, refreshed every few seconds.
- **The texts add up**: each pass keeps its own text and word boxes; the search reads them all. A
  pass never replaces another's text.
- **A changed file starts over**: when its stamp (size, last write) changes, pass 1 re-reads it and
  its later passes' texts are dropped, to be redone.
- **Pass 2 without RapidOCR** is skipped: its files stay *not done*, its bar hidden, and it runs as
  soon as RapidOCR is there (downloaded, then the extraction restarted).
- **Rebuild content** (⚙ menu, `_rebuildContent`) forgets and redoes every pass.
- The RapidOCR engine is created when pass 2 starts and disposed when it ends (memory).
- A first run over the user's 2 243 images: roughly 15 to 30 min in the background, the search usable
  all along.

### RapidOCR Pass

- `RapidOcr.InitModels(<paths in ocr-engine\>)`, then `Detect(bitmap)` on the image decoded as the
  Windows pass decodes it (EXIF orientation respected, downscaled past a maximum side).
- **Two confidence levels**, from the block's characters' average score (one constant each):

  | Score | The block is | On the sample |
  |---|---|---|
  | **≥ 0.8** | **Sure** — the pass's text | `TFIINFO` 1.00, `SUPPORT` 1.00, `HEN` 1.00, `FHOMAS` 0.86 |
  | **0.6 to 0.8** | **Unsure** — kept apart, ranked low (§ Ranking) | `SERY` 0.78, `QEHL` 0.73, `IV.` 0.68 |
  | **< 0.6** | Dropped | `T` 0.52, `a` 0.56, `TILMA` 0.57, `HIMA` 0.58 |

  A block also needs **at least 2 letters or digits**. On the sample the noise scored ≤ 0.78 and the
  real finds ≥ 0.86: the sure level holds the finds, the unsure one keeps the doubtful without letting
  them push the sure ones down.
- Word boxes: the word-level polygons when RapidOCR gives them, else the block's polygon, each turned
  into its bounding rectangle in fractions of the image (the `WordBox` format).

### Reshaped Copies

Windows OCR — the pass 1 engines — run on copies of the image, each copy's words mapped back to the
image's coordinates:

| Pass | Copy | What it catches | Mapping back |
|---|---|---|---|
| Inverted | The colours **inverted**, upscaled ×2 (longer side capped) | Light text on a dark background (`T F 1 INFO` on the TF1 logo) | Scale |
| Ring | The **ring unrolled**, clockwise, outer edge on top | Text along the top of a round badge | Each box's corners through the polar transform, bounding rectangle |
| Ring | The **ring unrolled**, counter-clockwise, inner edge on top | Text along the bottom of a round badge | Same |

- Both run on **every image**, whatever its shape; the Ring pass's noise (text read upside down,
  shapes taken for letters) is what the ranking (§ Ranking) keeps out of the way.
- The ring is the band between **0.68 and 1.0** of the image's inscribed circle, unrolled **twice
  around**, so a text across the seam shows whole once.
- Boxes of the unrolled copies span an arc once mapped back: their bounding rectangle is large — the
  arrow points at its edge, which is on the text.

### Index Format

`indexiles.content` goes to **`ImageGridFusion content 3`**:

```
relPath 	 size 	 ticks 	 boxes1 	 text1 	 boxes2 	 text2 	 boxes2u 	 text2u 	 boxes3 	 text3 	 boxes4 	 text4
```

- One **slot** (boxes + text) per source of words: Windows, RapidOCR sure, RapidOCR unsure (`2u`,
  written with the sure one by pass 2), Inverted, Ring.
- `boxesN` / `textN`: as today's `boxes` / `text` for each slot. A pass **not done** writes `?` in its
  boxes column and nothing in its text; a pass done with no words writes an empty boxes column, as
  today. A file read as text keeps `-` and no later pass.
- **Content 2 is read, not discarded**: its text and boxes become pass 1's, the later passes *not done*
  — the existing index is not re-read by Windows OCR. Content 1 is read as today (its images redone).
  A later version adding a pass reads content 3 the same way.
- `ContentText` holds one immutable slot per source; a pass finished swaps in a new `ContentText`
  (the search reads the dictionary from the UI thread). `MaxLength` and `MaxBoxes` apply per pass.
- `ContentIndex.IsCurrent(entry, pass)` replaces `IsCurrent(entry)` in the queue.
- **Loading stays cheap**: the file is parsed once, at start-up, off the UI thread. The folded forms the
  search compares are computed **on demand** — a word box's when the arrow first needs it, a slot's text
  folded once, its spaces-ignored form when a word is first looked for that way — never all at load
  time; the file keeps the original words only. The file list shows **without waiting for the content
  texts**, loaded alongside.

---

## Progress Bars

- **One bar per pass**, stacked in the explorer panel's progress row (today one `IndexingBar` in row 2
  of `_content`): Windows, RapidOCR, Inverted, Ring, top to bottom. The scan's sweeping bar stays the
  first one.
- Each bar shows its pass's share of the files **done over the whole index** — so while pass 2 runs,
  bar 1 stands full and the later ones at what was done before. A bar shows while its pass has files
  left or runs, and hides once its pass is complete; pass 2's is hidden without RapidOCR.
- **One colour per pass**, the bars being 3 px high and unlabelled — Windows blue, RapidOCR purple,
  Inverted orange, Ring green; a tooltip over the row names each bar's pass, its colour and its count.
  The scan's sweeping bar keeps today's blue: it is the Windows pass's bar, the first slot, the scan
  and the extraction never running at once.
- The progress row is **four bars tall while the content search is on**, one bar otherwise
  (`FileExplorerPanel.ApplyMetrics`): turning the setting on or off moves the list by 9 px, nothing else
  does.
- The status line names the running pass: `OCR 2/4 · RapidOCR: 120/2 243 (20220321_132643.jpg)`.

---

## Spaces-Ignored Match

In the search, not in the index:

- A query word not found in a pass's folded text is looked for in that text **with its whitespace
  removed** (`ContentText` keeps the stripped folded text next to `Folded`, computed once).
- Only for query words of **3 characters or more** — shorter ones match everywhere once the spaces go
  (`FileSearch.JoinedMinLength`).
- Only in the texts an **OCR** read (a slot with boxes): a text or HTML file read as text is not joined —
  its spaces are real, *the rapist* is not *therapist*.
- **Details view**: the excerpt is taken around the **run of consecutive words** whose concatenation
  holds the word (`ContentIndex.WordsAround` fallback), and the whole run is highlighted.
- **Arrow**: `ContentIndex.SpotOf` falls back on the boxes of one pass, concatenating consecutive
  boxes' folded words until the word is found, and returns the **union of the boxes** holding the
  matched letters. `ThumbnailGrid.PaintArrow` is unchanged.

---

## Ranking

The content tier of `FileSearch.Rank` (after every path match, as today) is split by how the words were
found, the more reliable first:

| Rank | Found by |
|---|---|
| 1 | An ordinary match in a **sure** slot — Windows, RapidOCR sure, Inverted |
| 2 | A spaces-ignored match in a sure slot |
| 3 | A **weak** slot only, ordinary or spaces-ignored — **Ring** (words read on an odd shape, curved along a circle) or **RapidOCR unsure** (score 0.6 to 0.8) |

- A file takes the best rank any of its passes gives it; inside a rank, the order of today (the sort
  order).
- The excerpt and the arrow come from the slot that gave the rank, the earliest slot first.
- A new source of words declares itself sure or weak; a new pass with an odd reshaping or a doubtful
  engine is weak.

---

## RapidOCR Download

RapidOCR is **never shipped**: its models and the native DLLs it runs on are downloaded on demand.

- Folder: **`ocr-engine\` next to the exe** (`OcrEngineFiles.FolderName`, § Folder Names of
  `../CLAUDE.md`), holding:

  | File | Taken from (nuget.org, the version the app references) | Size |
  |---|---|---|
  | `ch_PP-OCRv5_mobile_det.onnx`, `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx`, `latin_PP-OCRv5_rec_mobile_infer.onnx`, `ppocrv5_latin_dict.txt` | `RapidOcrNet` — `models/v5/` | ~13 MB |
  | `onnxruntime.dll` | `Microsoft.ML.OnnxRuntime` — `runtimes/win-x64/native/` | 16 MB (less compressed) |
  | `libSkiaSharp.dll` | `SkiaSharp.NativeAssets.Win32` — `runtimes/win-x64/native/` | 11 MB (less compressed) |

- **Only the entries needed are downloaded**: a package is a zip; its central directory is read with
  HTTP range requests, then each entry's compressed bytes alone — the ONNX Runtime package is 155 MB
  for every platform, the DLL a few MB of it. Done in `Web/`, next to `Downloader` (partial file, renamed
  once complete).
- Each file is checked against its **SHA-256, a constant in the code** for the referenced versions;
  a mismatch deletes it and fails the download.
- **Loaded from there**: `NativeLibrary.SetDllImportResolver` on the ONNX Runtime and SkiaSharp
  assemblies points `onnxruntime` and `libSkiaSharp` at `ocr-engine\`; the managed assemblies ship with
  the exe (small). No code touches a SkiaSharp or ONNX Runtime type before the files are there, and the
  resolvers are set **before anything touches them** (`RapidRecognizer`'s constructor, `RapidOcr` created
  after): the user's `PATH` holds Git Credential Manager's own `libSkiaSharp.dll` (2.88), which the
  default probing loads otherwise — SkiaSharp 3.119 then refuses it.
- A RapidOCR that fails to start (a DLL or a model that does not load) is said in the status line, its
  pass skipped and left *not done*.
- **Not shipped**: the packages' native DLLs are kept out of the build output, and `RapidOcrNet`'s
  build target copying `models\v5\` next to the exe is switched off.
- Available when every file is there (`OcrEngineFiles.IsAvailable`).
- **When it is offered**:
  - when *Search file contents (OCR)* is turned on in the ⚙ menu and RapidOCR is missing — a Yes / No
    message saying what is downloaded, its size and where;
  - from a ⚙ menu item **Download the OCR engine…**, shown while it is missing (for a user whose OCR is
    already on).
- **Yes**: downloaded, the progress on the status line, then the extraction restarted so pass 2 runs.
  **No**: passes 1, 3 and 4 only; offered again the next time.
- A failed download says so in the status line; nothing half-written stays in `ocr-engine\`.

---

## Rules and Docs

- **RULES.md**: a new § *Content OCR Passes* — the passes, their order, the texts adding up, the format
  rule (a new pass adds its columns and is read as *not done* from an older file), the spaces-ignored
  match, RapidOCR downloaded on demand and never shipped, the ranking by pass.
- **GLOSSARY.md / GLOSSARY.fr.md**: *OCR pass* (*passe OCR*), *OCR engine* (*moteur OCR*, the downloaded RapidOCR); the
  *Index* row mentions the passes.
- **README.md / README.fr.md**: the content search reads several passes; RapidOCR offered for download
  when the OCR is turned on; the third-party components (RapidOcrNet, PaddleOCR models, ONNX Runtime,
  SkiaSharp).

---

## Test Impact

The repository has no test project (CONTRIBUTING § Build): nothing is created. The behaviours below
are checked by hand in the app.

| Behaviour to check | How |
|---|---|
| The badge is found by `support` and `current` | Search after the passes ran |
| `S U P P O R T`-like texts found by the spaces-ignored match, the arrow on the letters | A file whose pass text has split letters |
| A content 2 index is read without redoing pass 1 | Start with the existing `files.content` |
| Four bars filling one after the other, the status line naming the pass | Watch a first run |
| The download offered when the OCR is turned on and from the ⚙ item, No keeping passes 1, 3 and 4 | ⚙ menu with `ocr-engine\` absent |
| Only the needed entries downloaded, hashes checked, the DLLs loaded from `ocr-engine\` | Download, then pass 2 running |
| A file found only by the Ring pass or by unsure words ranked after the sure ones | Search `support`, then a word only an unsure block holds |
| One colour per bar, the tooltip naming them | Hover the bars during a run |
| Search results refreshed while the passes run | Search during the run |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~**Download scope**: the models only, or the native DLLs too?~~ → Models and native DLLs, only
      the needed zip entries downloaded (§ RapidOCR Download)
- [x] ~~**OCR already on**: how is the download offered?~~ → A ⚙ menu item while RapidOCR is missing,
      on top of the offer when the OCR is turned on
- [x] ~~**Reshaped copies on every image, or the ring on near-square images only?**~~ → Every copy on
      every image; the ring's words rank last (§ Ranking)
- [x] ~~**RapidOCR noise filter**: average score ≥ 0.8 and ≥ 2 letters or digits?~~ → Wide but weighed:
      ≥ 0.8 sure, 0.6 to 0.8 unsure and ranked low, < 0.6 dropped; ≥ 2 letters or digits
- [x] ~~**Spaces-ignored match**: 3 characters minimum?~~ → Yes
- [x] ~~**Ranking**: sure ordinary, then spaces-ignored, then the Ring pass only — the Reshaped pass split
      into Inverted and Ring?~~ → Yes; RapidOCR's unsure words join the Ring in rank 3
- [x] ~~**Bars**: one colour or one per pass?~~ → One colour per pass, a tooltip naming them
- [ ] *(Found during the run, out of scope)* README § Planned still lists "OCR over the indexed images",
      done long ago (`workfiles/20260926-ocr-search.md`) — remove the line from both READMEs?

---

## Design Iterations

### Iteration 1 — 2026-10-13

Initial design from the user's outline (passes in order, each with its bar, the letters joined into
words, the arrow recomputed or at worst at the start, the search usable during indexing), the
scratchpad prototype and the scoping answers: passes 2 and 3 on every image, the letters joined in the
search rather than as a pass, the models downloaded on demand — offered when the OCR is turned on in
the settings. Passes ordered RapidOCR then Reshaped, as advised.

---

### Iteration 2 — 2026-10-13

Scoping answers and a new request:

- RapidOCR's native DLLs (ONNX Runtime, SkiaSharp) are downloaded with its models rather than
  shipped; only the zip entries needed are fetched, by HTTP ranges, each checked against a hash; the
  folder becomes `ocr-engine\`.
- A ⚙ menu item offers the download while RapidOCR is missing, for a user whose OCR is already on.
- The reshaped copies run on every image.
- **New request**: the words found on odd shapes (curved, etc.) weigh less in the search. The Reshaped
  pass is split into **Inverted** and **Ring** so the ring's words are told apart; a new § Ranking
  puts the files found by the Ring pass only after the straight-text ones, the spaces-ignored matches
  in between.

---

### Iteration 3 — 2026-10-13

Answers: 3 characters minimum for the spaces-ignored match, the ranking by pass with the Ring pass
apart, one colour per bar. On the noise filter, the user asked for a wide filter whose less precise
results weigh less: RapidOCR keeps every block scoring ≥ 0.6, those under 0.8 in an **unsure** slot
ranked with the Ring's words (rank 3); the index gets a fifth slot for them. The ranking now speaks
of **sure** and **weak** slots.

### Iteration 4 — 2026-10-13 — ✅ Implemented

Go given: code, tests and documentation, in a dedicated worktree (`feature/complementary-ocr-passes`).
The scope is the design sections as they stand in Iteration 3.

### Iteration 5 — 2026-10-13 — 🧭 Implementation choices

No rule broken. Choices the design did not state:

- **The libSkiaSharp on the PATH**: Git Credential Manager ships a `libSkiaSharp.dll` 2.88 on the
  user's `PATH`, which SkiaSharp 3.119 loaded and refused while `RapidOcr` was created in a field
  initializer — before the resolvers were set. `RapidOcr` is now created in `RapidRecognizer`'s
  constructor, after `OcrEngineFiles.ResolveNativeLibraries` (§ RapidOCR Download).
- **The spaces-ignored match reads OCR texts only**, not a text or HTML file read as text, whose spaces
  are real (§ Spaces-Ignored Match).
- **The progress row**: four bars tall while the content search is on, one otherwise; the scan's bar is
  the Windows pass's, the first slot (§ Progress Bars). Colours: blue, purple, orange, green.
- **A RapidOCR failing to start** is said in the status line and its pass skipped (§ RapidOCR Download).
- **The search API**: `FileSearch.Search` takes a `(entry, word) → ContentGrade?` instead of the folded
  text, `ContentIndex.GradeOf` replacing `FoldedOf`; `ContentIndex.Set` takes the pass and one result per
  slot; `IsCurrent` takes the pass.
- **The reshaped copies' sizes**: the Inverted copy upscaled ×2 up to a 2 400 px long side, bilinear; the
  Ring strips at most 200 px high (the band upscaled ×2 at most), two turns within Windows OCR's longest
  side, skipped under 24 px; a box carried back along its arc through 7 points of each edge.
- **RapidOCR's options**: `RapidOcrOptions.Default` with the word boxes on and its own line filter at 0.6,
  the block's average character score deciding sure / unsure.
- **The download offer** says about 25 MB; checked: the six files came down in ~2 s, hashes matching.

Checked in a scratchpad harness calling the built code: the engine downloaded and loaded; on the badge,
RapidOCR reads `SUPPORT` (sure) and the Ring pass `I SUPPORT` and `TH CURRENT THING` (weak), the arrow
spots on the top and bottom bands; `S U P P O R T` found by *support* as a joined match, the arrow over
the letters' union, the highlight over the run; a content 2 index (3 008 entries) read in 63 ms.

### Iteration 6 — 2026-10-14 — ⚙️ Post-implementation — Index load

Asked whether a line-per-file index is the most efficient, or JSON with named properties parsed once:
the file is already parsed once at start-up and the search never reads the disk; JSON would be larger
and slower to parse and to save, its gain extensibility only — kept in columns. Measuring it showed a
real cost this work brought: the content 3 index the user's run wrote (5.3 MB, 114 000 word boxes) loads
in **717 ms**, against 63 ms for the content 2 one (2.1 MB, 43 000 boxes) — the folding (accents and case
dropped) computed at load time for every box, and twice for every slot's text, while the file list waits
for it. Of the two fixes offered — the folded forms stored in the file (~8 MB), or computed on demand —
the user chose **on demand**: a box folded when the arrow first needs it, a slot's text folded once and
its spaces-ignored form when first needed, the file list shown without waiting for the texts
(§ Index Format).

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-13 | Engine download, passes + index + search + bars, ⚙ offer — three commits |
| Unit tests | 4 | 2026-10-13 | No test project in the repository — does not apply; checked in a scratchpad harness (Iteration 5) |
| RULES | 4 | 2026-10-13 | § Content OCR Passes |
| README | 4 | 2026-10-13 | README.md / README.fr.md: OCR passes, letters read apart, ranking, Tech |
| Glossary | 4 | 2026-10-13 | OCR pass, OCR engine, spaces-ignored match; Index row — both languages |

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
| 8 | Download scope: the models only, or the native DLLs too? | Models and native DLLs | 2026-10-13 |
| 9 | OCR already on: how is the download offered? | A ⚙ menu item | 2026-10-13 |
| 10 | Reshaped pass on every image, or near-square images only? | Everything on every image | 2026-10-13 |
| 11 | RapidOCR noise filter threshold? | Asked for help choosing — recommendation given, asked again | 2026-10-13 |
| 12 | (User request) | The words found on odd shapes (curved, etc.) should weigh less, ranked lower when searched | 2026-10-13 |
| 13 | RapidOCR noise filter: 0.8 and 2 characters? | Can't it be wide, the less precise results weighing less? | 2026-10-13 |
| 14 | Spaces-ignored match: 3 characters minimum? | Yes | 2026-10-13 |
| 15 | Ranking by pass, the Reshaped pass split into Inverted and Ring? | Yes, as proposed | 2026-10-13 |
| 16 | Bars: one colour or one per pass? | One colour per pass | 2026-10-13 |
| 17 | (User request) | Implement in a dedicated worktree | 2026-10-13 |
| 18 | Start the implementation? Where? | Code, tests and docs — in a worktree | 2026-10-13 |
| 19 | (User) Why is `5va6bj.jpg` (*SAUCE ?*) not found by *sauce*? | Answered: RapidOCR had not run, the engine not downloaded yet; RapidOCR reads `SAUCE ? imgflip.com` (sure) | 2026-10-14 |
| 20 | (User) A menu item to index the new files only? | Answered: already so — the scan lists the changes, each pass reads only the files it has not read at their stamp | 2026-10-14 |
| 21 | (User) Is the line-per-file index the most efficient — JSON, one parse? | Answered: parsed once already; JSON larger and slower; the 717 ms load found and explained | 2026-10-14 |
| 22 | Fix the load: folded forms stored, or computed on demand? | On demand (B) | 2026-10-14 |

---

*Last updated: 2026-10-14*
