# Image Content Analysis

> Working document — what the indexed images *show* (scenes, objects, a description), analysed
> offline by vision models and searched under 💡, in French and English, plus "find similar images".
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The content search (💡) reads today the **text** of the images and PDF pages (the OCR passes,
RULES.md § Content OCR Passes). This work adds what they **show**: a photo of a dog on a beach is
found by *chien plage*, *dog on a beach* or *un chien qui court sur le sable*, though it holds no
text. Everything runs **offline** — vision models run locally through ONNX Runtime, already used
by RapidOCR; the only network access is the models' one-time download.

Four results, all requested (Q&A 2):

| Result | What it gives | Searched as |
|---|---|---|
| **Free-text search** | An **embedding** per image (a vector), compared with the query's | A similarity tier under 💡, after the text matches |
| **Tags** | Words per image, in **French and English** (*chien* / *dog*, *plage* / *beach*) | Text, like an OCR slot |
| **Caption** | One generated sentence per image (*a dog running on a beach*) | Text, like an OCR slot; shown in the Details view |
| **Similar images** | The images closest to a given one | A search of its own, from a tile's context menu |

Queries in **French and English** (Q&A 3); the results appear **under 💡**, no new criterion or view
(Q&A 4).

Sibling of `20261027-face-detection.md` (implemented, RULES.md § People): the faces answer **who**, this
answers **what**. Both add a pass to the extraction worker, a model engine downloaded on demand
next to the exe, and an index file of vectors of their own. The models are not shared — YuNet and
SFace know nothing of scenes.

What it is not: no object boxes drawn on the cells, no effect on the grid, no editing of tags by
hand on an image (the user adds concepts to the vocabulary, § Tags); videos and animated content are out — a GIF's first frame and a PDF's
first page are read, as the OCR reads them.

Components touched (from the exploration):

| Area | Where |
|---|---|
| Pass worker — sequential, one thread, passes in `OcrPasses.All` order, pause rule, save cadence, bars | `UI/FileExplorerPanel.Extract` (~l.1492), `ExtractAsync`, `ExtractionPaused`, `TrySave`, `PassColors` / `PassColorNames` / `_passBars` |
| Pass / slot declarations | `Explorer/OcrPass.cs` — `OcrPass`, `ContentSlot`, `OcrPasses.All`, `SlotCount` (5), `SlotsOf`, `IsWeak`, `NameOf` |
| Content index format | `Explorer/ContentIndex` — header `ImageGridFusion content 3`, `3 + 2 × slots` columns, `Find`, `SlotText` |
| Per-file extraction, engine lifecycle | `Explorer/ContentExtractor` — `Extract(path, pass)`, `DecodePixels` (the pixels the OCR and the faces read), `StartRapid` / `StopRapid` |
| The face pass — the model for a non-OCR pass | `FileExplorerPanel.ExtractFaces`, called after the Windows OCR pass inside the loop over `OcrPasses.All`, under its own setting; reported as `ExtractionProgress(OcrPass.Windows, …, FacePass: true)`; bars by setting (`LayoutBars`); extraction runs while the content search **or** the face analysis is on |
| ONNX Runtime | `Microsoft.ML.OnnxRuntime 1.29.0`, CPU only, natives in `ocr-engine\` (`OcrEngineFiles.ResolveNativeLibraries`, `HasRuntime`) |
| Engine download | `OcrEngineFiles` reads NuGet packages (`RemoteZip`), `DownloadAsync(…, only)` fetching `onnxruntime.dll` alone; `FaceEngineFiles` downloads pinned direct URLs with their SHA-256 — its loop, `HashMatches` and `EngineFile` private to it |
| Vector index model | `Explorer/FaceIndex` (`index\files.faces`): binary, versioned, per file its stamp, `RetainOnly`, `IsCurrent` |
| Search tiers | `Explorer/FileSearch` — `Rank` (no score field), `Tier()`: path 0..n, `DescriptionTier` n+1, `PersonTier` n+2, content `PersonTier + 1 + grade`; `CompareTier`, `SearchMatch` |
| Explorer search entry point | `FileExplorerPanel.RefreshRows` (~l.1713), `Find` (~l.1984), `WithFavorites` (~l.1956) |
| Tile context menu | `FileExplorerPanel._menu` (l.137, items l.278, `Opening` l.328) — the grid (`GridPreview`) has none |

---

## Models

| Need | Model | Files (int8) | License | Language |
|---|---|---|---|---|
| Embeddings → free-text search, similar images, tags | **SigLIP 2 base** (`onnx-community/siglip2-base-patch16-256-ONNX`): a vision model and a text model sharing one space | vision ~95 MB, text ~283 MB | Apache 2.0 (the base model's, `google/siglip2-base-patch16-256`; the ONNX copy states none) | Multilingual — French queries work |
| Caption | **Florence-2 base** (`onnx-community/Florence-2-base`): vision encoder, token embeddings, encoder, decoder with its cache | ~275 MB | MIT | **English only** |

- **Rejected**: jina-clip-v2 (CC BY-NC, non-commercial), M-CLIP (no official ONNX, two models to
  pair), RAM++ for the tags (a third model, English tags, unofficial exports — the tags come from
  SigLIP 2 instead, § Tags).
- **CPU only**, like RapidOCR. Estimates: SigLIP 2 ~80–200 ms an image, ~20–50 ms a query; Florence-2
  ~2–6 s an image — the heaviest pass of the app by far.
- **Tokenizers** — no tokenizer package is referenced today; `Microsoft.ML.Tokenizers` is the candidate:
  - SigLIP 2's text model takes **Gemma's SentencePiece** tokens (256k vocabulary, BPE with byte
    fallback): the query **lowercased**, no BOS, **EOS (id 1) appended**, truncated so EOS stays last,
    right-padded with **0** to **64** tokens. Read through `SentencePieceTokenizer.Create` from the binary
    `tokenizer.model` — downloaded from `google/siglip2-base-patch16-256`, the ONNX copy may hold only
    `tokenizer.json` — the BOS / EOS / padding set by our code, not the library's options.
  - **Unproven**: no evidence the library reproduces Gemma's ids (no dummy prefix, user-defined tokens).
    The implementation **starts with that check** — ~50 French and English strings against reference ids
    produced once with Python `transformers` and kept as test data. Failing it, a small BPE tokenizer of
    our own reads `tokenizer.json` (its merges and vocabulary) — the fallback, reported as a divergent
    choice.
  - Florence-2 takes a BART BPE (vocabulary + merges): `Microsoft.ML.Tokenizers`' BPE tokenizer.

---

## Vision Engine

- **Never shipped**, downloaded on demand next to the exe — each file from a **pinned URL** with its
  **SHA-256**, into `<name>.~downloading`, then moved into place — the face engine's route. Nothing is
  downloaded without the user's **Yes**.
- **One download loop for both engines**: `FaceEngineFiles`' loop, `HashMatches` and `EngineFile` move
  into a shared `Explorer/DirectEngineDownload` (the folder, the files, the runtime's share of the
  progress as parameters), called by `FaceEngineFiles` and `VisionEngineFiles` — in a commit of its own,
  changing no behaviour of the face engine.
- Its own folder, **`vision-engine\`** (`VisionEngineFiles.FolderName`); ONNX Runtime's native DLL stays
  the OCR engine's, in `ocr-engine\`, fetched with it when missing, as the face engine does.
- **Two parts, two availabilities**: SigLIP 2 (~380 MB) and Florence-2 (~275 MB) — each its own
  `IsAvailable`, each downloaded by its own setting (§ Engine Offer).
- Each `InferenceSession` is created after `OcrEngineFiles.ResolveNativeLibraries()`.
- The **text model** (~300 MB in memory) is loaded at the first search needing it, and **released after
  a few minutes without one** (Q&A 16) — ~1 s to load it again; the vision model and Florence-2 are
  loaded by their pass only, released when it ends — the `StartRapid` / `StopRapid` lifecycle. A similar
  image search from an image not in the index loads the vision model for that one image.

---

## Passes

Two passes of the extraction worker, same pause rule, same save cadence, each its own progress bar
(new colours in `PassColors`), its tooltip saying how many files are done:

| Pass | Per file | Writes |
|---|---|---|
| **Vision** | The pixels `ContentExtractor.DecodePixels` gives, resized to 256 × 256, through SigLIP 2's vision model → one embedding (768 floats, normalized) | The embedding in `index\files.vision`; the **tags** slot of `files.content` (§ Tags) |
| **Caption** | The same pixels at 768 × 768 through Florence-2 (`<CAPTION>` task), greedy decoding | The **caption** slot of `files.content` |

- Over every **image and PDF** entry of the index, like the OCR passes; videos left out.
- A changed file (size or date) is analysed again; a file gone from the index drops its data.
- **Order** (Q&A 18): Windows OCR → faces → **Vision** → RapidOCR → Inverted → Ring → **Caption** — the
  visual search fills early, the slowest pass comes last.
- A pass runs only while **its setting is on** and its engine part present (§ Engine Offer);
  otherwise it is skipped and its bar hidden; the others run. The extraction runs while the content
  search, the face analysis, *Visual analysis* **or** *Captions* is on (today: the first two).
- The face pass's mechanics carry over: its own bar shown by setting (`LayoutBars`), the vector index
  saved with `TrySave` like `FaceIndex`.
- **One `ContentPass` for every pass** (Q&A 17, 21): `OcrPass` / `OcrPasses` become `ContentPass` /
  `ContentPasses` (`OcrPass.cs` → `ContentPass.cs`), and **every pass is one of its values** — the OCR
  ones, `Faces`, `Vision`, `Caption` — in the order above. Each declares its slots of `files.content`
  (none for the faces), its setting and its engine; the worker walks `ContentPasses.All` once, and the
  `FacePass` flag of `ExtractionProgress` gives way to the pass itself.
  - First, in a commit of its own changing no behaviour: the rename, and the face pass made a value of
    it — then the new passes join.
  - `IsWeak` and the grades keep reading the OCR slots only; `files.content`'s slots stay indexed by
    `ContentSlot`, not by pass.

### Vision Index

Its **own file**, `index\files.vision`, never a slot of `files.content` — 768 floats a file would bloat
a text file loaded whole and scanned by every search (the face index's reasoning):

- Binary, versioned header, base folder, the model's id (a model change empties it); per file: path,
  size, ticks, embedding. A file absent is *not done*.
- Saved atomically (temp file then move), loaded off the UI thread.
- Held in memory as one contiguous `float[]` (768 × files: ~3 MB per 1 000 images), so a query is a
  single scan of dot products.

---

## Tags

- Computed **from the embeddings**, never by a model of their own: a **bilingual vocabulary** of
  concepts (*chien* / *dog*, *plage* / *beach*…), each concept's text embedding computed once
  (*a photo of a dog*), compared with every image's.
- A concept above its **threshold** is a tag of the image; the slot holds the French and English
  words of its tags.
- **The vocabulary** (Q&A 10):
  - **~1 000 curated concepts**, French and English, shipped with the app (an embedded resource):
    objects, animals, scenes, places, weather, kinds of pictures (screenshot, document, drawing,
    meme…); each line a concept, its French and English words, optionally the prompt it is embedded
    with.
  - **A user file** next to the exe (`vision-tags.txt`, created empty with a comment line saying its
    format) **adds** concepts, in the same format; read at start-up and when it changes.
  - Each concept's text embedding cached next to the index, keyed by its prompt. **A vocabulary change
    recomputes the tags from the stored embeddings** — no image read again, a few seconds.

---

## Content Index

Two new **text slots** of `files.content`, appended after the OCR slots:

| Slot | Filled by | Grade |
|---|---|---|
| **Tags** | The Vision pass | **Seen** — their own, after every OCR grade (§ Search Under 💡) |
| **Caption** | The Caption pass | **Seen** |

- `ContentSlot` gains two values at its end, `SlotCount` goes from 5 to 7, the header becomes
  `ImageGridFusion content 4`; a version 3 file is read with 5 slots, its two new ones *not done* —
  never discarded (RULES.md § Content OCR Passes › The index format).
- No boxes: the slots use the box-less format of the text files — no arrow, no spaces-ignored match.
- **Word matching** (Q&A 12): by **substring**, as the OCR texts — *rat* finds *pirate* — but a word
  found **whole** ranks higher than one found inside another (§ Search Under 💡). The same for the
  tags and the caption.
- `ContentIndex.Find` returns on the first sure slot today: it looks at the Seen slots only when no
  OCR slot holds the word, so a file is graded by its best OCR match first.

---

## Search Under 💡

- **Text**: the tags and the caption are found by the content search as the OCR's words are, their
  excerpt in the Details view, in **two grades of their own after every OCR grade** (Q&A 11–12):

  | Grade (`ContentGrade`) | Found |
  |---|---|
  | Sure, Joined, Weak | The OCR's words, as today |
  | **Seen, whole word** | A tag or the caption holding the query word as a whole word (*chien*) |
  | **Seen, inside a word** | A tag or the caption holding it inside a longer word (*rat* in *pirate*) |

  So *chien* written in an image comes before a photo of a dog, and a dog tagged *chien* before a pirate
  found by *rat*.
- **Similarity tier** — a new tier **after every content grade**: the query, as a whole sentence, run
  through SigLIP 2's text model → a query embedding; every image's dot product with it; the **N
  closest** images listed in it (Q&A 14 — a constant, `VisionSearch.TopCount`, 100 to start with), **ordered
  by score**, the best first — counted among the files in the search's scope (below the open folder in
  the folder view).
  - A file already found in an earlier tier is not repeated.
  - `Rank` gains a score (`Similarity`), `Tier()` a value after the content grades (`PersonTier + 1 +
    grade`, the Seen grades included), `CompareTier` orders by score inside it; `SearchMatch` carries the
    score.
  - With a user sort order (Date, Name, Size), the other tiers are sorted by it; the similarity tier
    keeps **its score order**, always — the exception (Q&A 13).
  - Its tier number stays above every favorite's tier (`WithFavorites` interleaves by tier).
  - The query embedding is computed off the UI thread, once the typing pauses (the search's own
    debounce), and only while 💡 is on.
- In the search view and in the folder view alike (below the open folder), as every search.

---

## Similar Images

Three ways in (Q&A 15), one search:

| From | Gesture |
|---|---|
| A file tile of the explorer | *Find similar images* in its context menu (`_menu`), file rows only |
| A cell of the grid | *Find similar images* in a **new context menu of the cell** (`GridPreview` has none today), cells holding an image only; forwarded to the explorer by `MainForm`, like `ShowInExplorerClicked` |
| A drop onto the search box | An image dropped on it — a file from the Explorer, a tile, a cell by its ✥ handle — searches what resembles it instead of becoming a favorite (the panel's drop) |

- The source's embedding is the index's when the file has one; otherwise — a file not indexed, a pasted
  image, a cell without a file — it is **computed on the spot** from the pixels the cell or the file
  gives, the vision model loaded for that image.
- It runs a **similarity search**: the search view shows the **N closest** images (the same `TopCount`),
  best first, the source itself first when indexed; the caption line says `Similar to <name>` with a ×
  that clears it.
- Typing in the search box clears it and searches as usual.
- Only while *Visual analysis* is on and SigLIP 2 present: otherwise the menu entries are hidden and a
  drop onto the search box is the panel's ordinary drop.

---

## Engine Offer

**Opt-in**, like the face analysis — two **independent app settings** of the ⚙ menu, **off by
default** (`AppSettings.VisualAnalysis`, `AppSettings.Captions`):

| Setting | Turns on | Downloads when missing |
|---|---|---|
| *Visual analysis* | The Vision pass, the tags, the similarity tier, *Find similar images* | SigLIP 2 (~380 MB) |
| *Captions* | The Caption pass | Florence-2 (~275 MB) |

- Each is usable without the other; turning one on offers its own engine part when missing — the
  download shown and confirmed first, nothing fetched without that **Yes**. A **No** leaves the setting
  off.
- A setting turned off stops its pass and its search; what is already indexed is kept.
- **Forget visual analysis…** (Q&A 19), a ⚙ entry like *Forget faces…*: after a confirmation, deletes
  `index\files.vision` and the cached concept embeddings, empties the Tags and Caption slots of
  `files.content` (back to *not done*), and turns both settings off. The engine and `vision-tags.txt` stay.
- A `--new-instance` instance reads the settings, writes none of its own accord.

### Order With the Face Detection

Implemented **after** `20261027-face-detection.md` (Q&A 6): its direct-download engine route
(`FaceEngineFiles`: pinned URL, SHA-256, `Downloader.DownloadAsync`) and its binary index pattern
(`index\files.faces`) are generalized for the vision engine and `index\files.vision`, rather than
written twice.

---

## Documentation

- `RULES.md`: a new **Image Content Analysis** section (engine, passes, vision index, tags from the
  embeddings, the slots, the similarity tier, similar images), and a line in § Content OCR Passes for the
  new passes' place.
- `GLOSSARY.md` + `GLOSSARY.fr.md`: Vision pass, Caption pass, Vision engine, Tag, Caption, Similarity
  tier, Similar images.
- `README.md` + `README.fr.md`: searching what the images show; similar images.

---

## Test Impact

**No unit tests** (Q&A 20): the repository has no test project (`CONTRIBUTING.md`: a change is checked
by hand in the running app), and none is created — as for the face detection. The behaviours below are
**checked by hand** during the run, from a copy of the build (Iteration 5), and reported:

| Behaviour to pin | Checked by | Create / Update |
|---|---|---|
| SigLIP 2 tokenization of ~50 French and English strings equals the reference ids (`transformers`) | A one-off check, first thing of the run | — |
| Image preprocessing (resize, normalization) gives the reference embedding on a sample image | A one-off check | — |
| Vision index round-trip (version, model id, stamps, embeddings) | Restart of the copy | — |
| `files.content` v3 read as v4: the two new slots *not done*, nothing redone | The user's v3 index, copied | — |
| Tags from embeddings: both languages written, a concept added to `vision-tags.txt` recomputes them | In the app | — |
| Seen grades: a whole word before a word found inside another, both after every OCR grade | In the app | — |
| Similarity tier: after every content grade, the N closest by score, no duplicate file, kept under a user sort order | In the app | — |
| Similar images from a tile, a cell (with and without a file) and a drop onto the search box | In the app | — |
| Florence-2 decoding loop: a sample image gives a sensible caption | In the app | — |
| The `ContentPass` refactoring changes nothing of the OCR and face passes | In the app, before the new passes | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1.

- [x] ~~**Order with the face detection** — implemented after it, sharing its direct-download engine route (`FaceEngineFiles` generalized), or independently first?~~ → After it (Q&A 6)
- [x] ~~**Opt-in / offer** — the analysis off by default, turned on from the ⚙ menu (like the faces), or offered when 💡 is turned on (like RapidOCR)?~~ → Off by default, turned on from the ⚙ menu (Q&A 7)
- [x] ~~**Captions** — part of the same offer, or a separate opt-in (heavy: ~275 MB, 2–6 s an image)? They are **English only**: acceptable, knowing a French query finds them only through the tags and the similarity tier?~~ → Two independent ⚙ settings, *Visual analysis* and *Captions*, each its own download (Q&A 8)
- [x] ~~**Vocabulary** — size (a few hundred concepts, ~1 000, more) and source (curated by hand, generated from an existing list such as RAM++'s 4 585 tags translated)? Editable by the user?~~ → ~1 000 curated bilingual concepts shipped, plus a user file next to the exe adding concepts (Q&A 9–10)
- [x] ~~**Tags and caption: sure or weak?** — sure ranks them with the OCR's sure words; weak puts them in the last content grade.~~ → A grade of their own, after every OCR grade (Q&A 11)
- [x] ~~**Whole-word match for tags** — tags matched as whole words (no *rat* in *pirate*), or the substring rule of the OCR texts?~~ → Substring, a whole word ranking higher (Q&A 12)
- [x] ~~**Similarity under a user sort order** — the similarity tier always by score, or sorted like the others?~~ → Always by score (Q&A 13)
- [x] ~~**Similarity threshold** — a fixed constant tuned during implementation, a top-N, or an app setting?~~ → Top N (Q&A 14)
- [x] ~~**Similar images: other entry points** — also from a cell of the grid (it has no context menu today), or by dropping an image onto the search box?~~ → The tile menu, a grid cell's context menu and a drop onto the search box (Q&A 15)
- [x] ~~**Text model lifetime** — kept loaded while the app runs (~300 MB in memory), or released after a while unused?~~ → Released after a while unused (Q&A 16)
- [x] ~~**Code names** — rename `OcrPass` / `OcrPasses` to `ContentPass` now that passes are not all OCR, or keep the names?~~ → Renamed (Q&A 17) *(revised 2026-10-27, see Iteration 6: one `ContentPass` holding every pass, the faces included — Q&A 21)*
- [x] ~~**Pass order** — where do Vision and Caption run among Windows OCR, faces, RapidOCR, Inverted, Ring?~~ → Vision right after the faces, Caption last (Q&A 18)
- [x] ~~**Forgetting** — a ⚙ entry *Forget visual analysis…* deleting `files.vision` and emptying the Seen slots after a confirmation, turning both settings off — like the faces' *Forget faces…*?~~ → Yes (Q&A 19)
- [x] ~~**Test project** — the face detection chose hand checks only; the same here, or a test project (the tokenizer check against reference ids would be its first test)?~~ → Hand checks only (Q&A 20)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-27

Initial design. Offline is possible: vision models run locally through ONNX Runtime, already used
by RapidOCR. The user asked to take into account the OCR already there and the face detection being
designed (Q&A 1), then chose all four results — free-text search, tags, caption, similar images — in
French and English, under 💡 (Q&A 2–4); the subject is tricky / long (Q&A 5).

From the exploration: one model, **SigLIP 2 multilingual**, covers the free-text search, the similar
images and — compared with a bilingual vocabulary — the tags; **Florence-2** gives the captions, in
English. The embeddings get their own binary index, as the faces do; the tags and the caption are two
new text slots of `files.content` (version 4); the similarity is a new tier after the content grades,
ordered by score — `Rank` has no score today. The worker is sequential, so two passes join its list;
the download follows the face engine's direct-file route, `OcrEngineFiles` reading NuGet packages only.
A "Find similar images" entry joins the tile context menu.

Deepening: SigLIP 2's Apache 2.0 license confirmed on the base model; its tokenizer's rules known
(lowercase, EOS appended, padded with 0 to 64), but whether `Microsoft.ML.Tokenizers` reproduces Gemma's
ids is unproven — the implementation starts by checking it, a tokenizer of our own reading
`tokenizer.json` being the fallback.

### Iteration 2 — 2026-10-27

From the user's answers (Q&A 6–9):

- Implemented **after the face detection**, generalizing its direct-download route and binary index
  pattern instead of duplicating them.
- **Opt-in**: two independent ⚙ settings, off by default — *Visual analysis* (SigLIP 2: tags,
  similarity, similar images) and *Captions* (Florence-2) — each downloading its own engine part.
  The answer "offer both options" was read as these two settings.
- The tag vocabulary: the user asked for advice — recommended: ~1 000 curated bilingual concepts
  shipped, plus a user file adding concepts (Q&A 10, pending).

### Iteration 3 — 2026-10-27

From the user's answers (Q&A 10–13; the batch asked twice at the user's request, the second answers
kept):

- **Vocabulary**: ~1 000 curated bilingual concepts shipped, plus `vision-tags.txt` next to the exe
  adding the user's own.
- The tags and the caption get **grades of their own** after every OCR grade: *Seen, whole word*, then
  *Seen, inside a word* — matching stays by substring, a whole word ranking higher. The rule was asked
  for the tags; applied to the caption too, both sharing the grades.
- The similarity tier keeps **its score order** under a user sort order.

### Iteration 4 — 2026-10-27

From the user's answers (Q&A 14–17):

- The similarity tier and the similar images list the **N closest** (`TopCount`, 100 to start with —
  the value is a proposal, tuned during implementation), no threshold.
- *Find similar images* from **three places**: the tile menu, a **new context menu of the grid's cells**,
  and an image **dropped onto the search box**; an image with no embedding is embedded on the spot.
- The text model is **released after a few minutes unused**.
- `OcrPass` / `OcrPasses` **renamed** `ContentPass` / `ContentPasses`, in a commit of their own.
- New question, from the face detection's design: **forgetting** the visual analysis, like *Forget
  faces…*.

### Iteration 5 — 2026-10-27

The face detection is delivered; the user asked whether it calls for adjustments here. Read from its
🧭 choices, RULES.md § People and its code:

- **The face pass is not an `OcrPasses` value**: `ExtractFaces` runs after the Windows pass inside the
  loop, under its own setting, reported with a `FacePass` flag on `ExtractionProgress`. The Vision and
  Caption passes follow that model. The rename decided in Q&A 17 loses its first reason — the passes no
  longer join `OcrPasses` — so it is asked again with the alternative it opens: every pass, the faces
  included, a value of one `ContentPass` (Q&A 21).
- **The download loop** is private to `FaceEngineFiles`: moved into a shared `DirectEngineDownload` used
  by both engines, rather than copied.
- The pixels are `ContentExtractor.DecodePixels`; the vector index follows `FaceIndex`; the content
  tiers now come after `PersonTier`.
- The extraction runs while the content search or the face analysis is on: *Visual analysis* and
  *Captions* join that condition.
- The face run checked the app from a **copy of the build in the scratchpad** — turning an opt-in on in
  the bin's instance would write the user's settings and analyse their base folder (⚠️ reported as a
  rule broken). This run will face the same: noted here so the go covers it.

### Iteration 6 — 2026-10-27

From the user's answers (Q&A 18–21) — every open question settled:

- **Pass order**: Windows OCR → faces → Vision → RapidOCR → Inverted → Ring → Caption.
- **Forget visual analysis…** in the ⚙ menu, like *Forget faces…*.
- **Hand checks only**, no test project — the Test Impact table lists what is checked by hand.
- **One `ContentPass`** holding every pass, the faces included (revising Q&A 17): the rename and the
  face pass's move into it come first, in a commit changing no behaviour.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 6 | 2026-10-27 | Declined: hand checks only (Q&A 20) |
| RULES.md | | | |
| GLOSSARY (EN + FR) | | | |
| README (EN + FR) | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What should the analysis produce? Search language? Integration? Depth? | Dismissed; answered by a message: take into account the OCR already there, and the face detection being designed, which will bring face detection libraries | 2026-10-27 |
| 2 | What should the analysis produce (free-text search, tags, caption, similar image)? | All four | 2026-10-27 |
| 3 | Which language are the searches typed in? | French and English | 2026-10-27 |
| 4 | How do the results appear in the explorer (under 💡, new criterion, Themes view)? | Under 💡 (content) | 2026-10-27 |
| 5 | Is the subject straightforward or tricky / long? | Tricky / long | 2026-10-27 |
| 6 | Order with the face detection: after it (sharing its download route), or independently first? | After the face detection | 2026-10-27 |
| 7 | Opt-in: off by default (⚙ menu), or offered when 💡 is turned on? | Off by default, turned on from the ⚙ menu | 2026-10-27 |
| 8 | Captions: same offer, or a separate opt-in? English only acceptable? | "Offer both options" — read as two independent ⚙ settings, *Visual analysis* and *Captions*, each its own download | 2026-10-27 |
| 9 | Tag vocabulary: size, source, editable? | "Advise me" — recommendation asked in Q&A 10 | 2026-10-27 |
| 10 | Tag vocabulary — recommended: ~1 000 curated concepts shipped, plus a user file next to the exe adding concepts. Agreed? | Agreed (asked twice, the user asking for the batch again; same answer) | 2026-10-27 |
| 11 | Tags and caption: sure, weak, or a grade of their own? | A grade of their own, after every OCR grade, before the similarity tier | 2026-10-27 |
| 12 | Tags matched as whole words, or by substring like the OCR texts? | First asking: substring. Asked again: **substring, but a whole word ranks higher** | 2026-10-27 |
| 13 | Similarity tier under a user sort order: always by score, or sorted like the others? | First asking: no preference. Asked again: always by score | 2026-10-27 |
| 14 | Similarity threshold: fixed constant, top-N, or ⚙ setting? | Top N | 2026-10-27 |
| 15 | Similar images: other entry points than the tile menu (a grid cell, a drop onto the search box)? | All three: the tile menu, a grid cell's context menu, a drop onto the search box | 2026-10-27 |
| 16 | Text model lifetime: kept loaded while the app runs, or released after a while unused? | Released after a while unused | 2026-10-27 |
| 17 | Rename `OcrPass` / `OcrPasses` to `ContentPass`, or keep the names? | Rename | 2026-10-27 |
| 18 | Pass order: where do Vision and Caption run among Windows OCR, faces, RapidOCR, Inverted, Ring? | Vision early (after the faces), Caption last | 2026-10-27 |
| 19 | Forgetting: a ⚙ *Forget visual analysis…* entry, like the faces'? | Yes, like the faces | 2026-10-27 |
| 20 | Test project, or hand checks only like the face detection? | Hand checks only | 2026-10-27 |
| 21 | The face pass stays outside `OcrPasses` (a flag on `ExtractionProgress`): rename `OcrPass` to `ContentPass` **and** make every pass — faces, vision, caption — one of its values, or follow the face pass's model and keep `OcrPass`? | One `ContentPass`, every pass a value of it, the faces included | 2026-10-27 |

---

*Last updated: 2026-10-27*
