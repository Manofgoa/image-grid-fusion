# Similar Images Accuracy

> Working document — *Find similar images* and the similarity tier of a 💡 search show images too far
> from what was asked: cutting the results that do not stand out, a strictness setting, the closeness
> shown on each result.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The visual analysis (origin: `workfiles/20261027-image-content-analysis.md`) embeds every indexed image
with SigLIP 2 base. Two features rank images by the cosine of their embeddings to a query:

- **Find similar images** — an image as the query (a tile's or a cell's context menu, an image dropped
  onto the search box);
- the **similarity tier** of a 💡 search — the typed text as the query, embedded by SigLIP 2's text
  model, its results appended after every other tier.

The user sees images **too far** from the query in **both** (Q&A 1). Levers accepted (Q&A 2): a **score
cutoff**, a **strictness setting** in the ⚙ menu, the **score shown** on each result. Not taken: changing
the model. The face grouping was the first half of this subject (`workfiles/20261028-face-grouping-accuracy.md`).

Out of scope: the other relevance tiers of the search — the file name already outranks the content
(Q&A 4 answered the user's question on the global ranking), and this workfile touches the similarity tier
only.

Components: `Explorer/FileSearch.cs` (`SearchSimilar`, `SearchMatch`), `UI/FileExplorerPanel.Vision.cs`
(`WithSimilar`, `SimilarRows`), `Explorer/VisionEncoder.cs` (`TextEncoder`), `UI/ThumbnailGrid.cs`
(`ExplorerRow`, the tile and Details painting), `UI/AppSettings.cs` and `UI/MainForm.cs` (the ⚙ menu).

---

## Diagnosis

Exploration and measurements (Iteration 1) — a throwaway harness, read-only on the user's
`index\files.vision`: 2 308 images.

### Always 100 results, whatever their score

`FileSearch.SearchSimilar` returns the **100 closest** (`SimilarCount`), `OrderByDescending(Score)
.Take(100)`: no minimum, no relative cutoff. Both features go through it. The score — a plain cosine of
two unit vectors (`Embeddings.Dot`), no SigLIP logit scale or bias — is carried by `SearchMatch.Similarity`
and **dropped** when the rows are built (`ExplorerRow` has no field for it). *Find similar* does not leave
the source image out: it comes first when indexed.

### Image → images: the cosines are high for everything

| Measure (300 sources) | p10 | Median | p90 |
|---|---|---|---|
| Random pair of images | — | 0.659 | 0.765 (p99 0.856) |
| Score at rank 1 | 0.817 | 0.901 | 0.964 |
| Score at rank 10 | 0.762 | 0.839 | 0.907 |
| Score at rank 100 | 0.700 | **0.781** | 0.843 |

The embeddings are **anisotropic**: two unrelated images already score 0.66. The 100th result scores
about what a random pair does at its 90th percentile — most of the 100 are no closer than chance.

- An **absolute floor** works badly: ≥ 0.8 keeps a median 49 results but 279 for the 10 % of sources in
  a crowded region, and none for 19 of 300.
- A **relative cutoff** — how far a result **stands out of the source's own scores**, `z = (score − mean)
  / sd` over the whole index — adapts: z ≥ 2.5 keeps a median 11 results (p90 46), z ≥ 3 a median 2
  (p90 16), none for 102 of 300 sources.

### Text → images: little signal

| Query | Rank 1 | Rank 100 | Top 3 files |
|---|---|---|---|
| `chien` | 0.119 | 0.079 | *chat isabelle*, a screenshot, a photo |
| `un chien` | 0.125 | 0.088 | *chat isabelle*, a screenshot, a photo |
| `écran` / `ecran` | 0.117 / 0.104 | 0.094 / 0.082 | |
| `éléphant` / `elephant` | 0.096 / 0.077 | 0.071 / 0.049 | |
| `zzqx licorne violette` (nonsense) | **0.120** | 0.071 | |
| Random text–image pair | p50 0.049 | p99 0.102 | |

- The cosines are small (SigLIP's are), and **a nonsense query reaches the same top score as a real one**:
  measured against its own distribution, `zzqx licorne violette` stands out at z 4.9, `qwerty asdf` at
  5.2 — as much as `chien` (5.1). No cutoff on the query's own scores tells a real match from noise.
- **Per-image debiasing** — an image's score minus its mean over 60 neutral prompts (`un chat`, `a
  house`…), over their spread — helps the real queries (`chien` → *marlène schiappa chien* first, the
  cat gone) but nonsense still stands out (`blorp vintu` top 5.0).
- **The query reaches the model accent-folded**: `WithSimilar` joins `FileSearch.Words`, which folds the
  accents for the name search. The accented query scores higher every time (`écran`, `éléphant`, `fête`).
- A prompt template (`une photo de …`, `a photo of …`) gives mixed results; French queries score higher
  than English ones on this collection.
- The text model is the **int8** export (283 MB): a quantized text tower may lose alignment — not
  measured (it would need another export downloaded).

---

## Image Similarity (Find similar images)

- **Cutoff by standing out** (Q&A 5): a result is kept when its score stands out of the source's scores
  over the index — `z = (score − mean) / sd`, every embedded image but the source — by at least the
  strictness level's `z` (§ Strictness Setting), at most `SimilarCount` (100), the closest first. The mean
  and spread come from the pass that scores them: no second pass.
- **The source image is left out** of its own results.
- **None standing out**: the caption line says so (`Similar to <name> — nothing close enough`), rather
  than an empty list without a word.

---

## Text Similarity (the 💡 similarity tier)

- **The query embedded with its accents**: the text typed, lowercased, its spaces collapsed — not
  `FileSearch.Words`, which folds the accents for the name search.
- **Per-image debiasing** against the **vocabulary's concepts** (`VisionTags.ConceptEmbeddings`, the 1 018
  concepts the tags are chosen from, already embedded and cached): `d = (score − the image's mean over the
  concepts) / its spread over them` (`VisionSimilarity.SpreadOf`). Each image's spread is kept with its
  embedding (`FileExplorerPanel.SpreadOf`, a weak table keyed by the embedding array, so a new embedding is
  computed again); the concepts are loaded and every spread computed off the UI thread with the query's
  embedding (`PrepareConcepts`), once per session.
- **A strict cutoff** (Q&A 6): a result is kept when its score stands out of the query's own scores by the
  level's `z` **and** `d` reaches the level's `z` plus `VisionSimilarity.TextMargin` (**2.5**), at most
  `SimilarCount`, the highest `d` first. At Normal, the 10 nonsense queries of the calibration find nothing;
  17 real queries find 28 images in all, for 4 of them (§ Calibration).
- Its place stays the **last tier**.

---

## Strictness Setting

An app setting of the ⚙ menu, after *Visual analysis*: *Similar images* › **five levels** (Q&A 7) —
*Very loose*, *Loose*, **Normal** (the default), *Strict*, *Very strict* — **one setting for both
features**, each level giving the cutoff `z` (`VisionSimilarity.Cutoffs`: 1.5 / 2.0 / **2.5** / 3.0 / 3.5,
kept by the calibration). Stored in `settings.json` (`AppSettings.SimilarImages`, the level's
index, an unknown value giving Normal) — the `Face grouping` pattern. Disabled while the visual analysis
is off. Changing it searches the list shown again (a Find similar or a 💡 search).

---

## Closeness Shown

- **The closeness** (Q&A 10): how far a result stands out, **0–100 %, computed the same way** for both
  features — from `z` for an image, from `d` for a text: `clamp((v − 1) / 4) × 100`, so 1 σ is 0 % and
  5 σ is 100 % (values set in the run if the measurements ask). Never the raw cosine, which is not
  comparable between an image and a text.
- `ExplorerRow` carries it (`Closeness`, null for every other row; `ClosenessText` writes `87 %`), filled
  by `SimilarRows` and `WithFavorites` from `SearchMatch.Standing`, next to `Similarity`.
- Shown three ways (Q&A 8):
  - a **label** on the tile — `87 %` on a dark label at its bottom-left corner, in the Thumbnails view,
    the group counts' style (`ThumbnailGrid.PaintCornerLabel`, now shared with them), clear of the heart
    and the bulb at the top left;
  - in the **Details** view, before the size on the row's first line: `87 % · 412 Ko`;
  - in the **tooltip**, a line after the path: `≈ 87 % close` (`ExplorerRow.Tip`).

---

## Calibration

The throwaway harness against the worktree's build (`FileSearch.SearchSimilar`, `VisionSimilarity`), read-only
on the user's 2 308 embeddings and their 1 018 concepts:

| Level | Find similar: results p10 / median / p90 | Sources with none (of 300) | Text, 17 real queries: results (queries with some) | Text, 10 nonsense queries |
|---|---|---|---|---|
| Very loose, 1.5 | 100 / 100 / 100 (the cap) | 0 | 174 (13) | 10 (4) |
| Loose, 2.0 | 23 / 47 / 90 | 0 | 80 (10) | 1 (1) |
| **Normal, 2.5** | 1 / **9** / 39 | 17 | 28 (4) | **0** |
| Strict, 3.0 | 0 / 1 / 13 | 116 | 9 (3) | 0 |
| Very strict, 3.5 | 0 / 0 / 5 | 186 | 3 (2) | 0 |

The text margin was swept from 1.0 to 3.0: 1.0 (the first guess) left nonsense results at Normal for 7
queries of 10, 2.0 for 1, 2.5 for none. The real text matches stay weak even when kept (`chien` → a cat
photo first): the text model is the limit — the fp16 export is the follow-up (Q&A 9).

---

## Test Impact

No test project in the repository (`CONTRIBUTING.md`); checked by a throwaway harness on the user's
embeddings and by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A result is kept only when its score stands out of the query's distribution by the level's `z` | Throwaway harness (scratchpad) | Created — passes, the farthest first |
| Find similar leaves the source image out | Throwaway harness (scratchpad) | Created — passes |
| The text query reaches the text model with its accents | By hand, in the app (the query is built in the panel) | — |
| The closeness reaches the row and is shown | By hand, in the app | — |
| The text cutoff returns nothing for nonsense queries at Normal, a few results for real ones | Throwaway harness (scratchpad), on real embeddings | Created — 0 of 10 nonsense, 4 of 17 real (§ Calibration) |
| The closeness maps 1 σ to 0 % and 5 σ to 100 %, the same for an image and a text | Throwaway harness (scratchpad) | Created — passes |
| The ⚙ level is saved, read back at start-up, and the shown list is searched again | By hand, in the app | — |
| *Nothing close enough* shows when no result stands out | By hand, in the app | — |

---

## Open Questions

- [x] ~~1. Find similar's cutoff: relative (z) or an absolute cosine floor?~~ → **Relative**
- [x] ~~2. The text similarity tier: strict cutoff, dropped, or kept with its closeness shown?~~ → A
  **strict cutoff**, with per-image debiasing (§ Text Similarity)
- [x] ~~3. The strictness levels: five or three; one setting or one each?~~ → **Five**, one setting for
  both features
- [x] ~~4. The closeness shown: badge, Details line, tooltip?~~ → **All three**
- [x] ~~5. Try the text model's fp16 export before the go, or later?~~ → **Later**: out of this workfile,
  a follow-up once it is delivered
- [x] ~~6. The number shown: the raw cosine, or how far the result stands out, 0–100 %?~~ → **How far it
  stands out**, 0–100 %, the same way for both

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Request: after the faces, the images too far apart that the similar-image search shows.

- Scoping (Q&A 1–3): seen in Find similar images and in the 💡 similarity tier; levers: a score cutoff,
  a strictness setting, the score shown — no model change; a tricky subject.
- The user asked meanwhile how the global search ranking works (Q&A 4): answered — the file name
  already outranks the content; only the last tier is this workfile's.
- Exploration: always the 100 closest; the score dropped before the rows; the source image not left
  out. Measurements on the user's 2 308 embeddings: image cosines anisotropic, a relative cutoff adapts
  where an absolute one does not; text matches barely above noise, a nonsense query standing out as
  much; the query folded of its accents (§ Diagnosis).
- Proposed: a relative cutoff for Find similar, the source left out, the query's accents kept, a ⚙
  level, the closeness carried to the rows; five questions left open.

### Iteration 2 — 2026-10-29

The open questions answered (Q&A 5–10):

- **Find similar**: a **relative** cutoff, how far a result stands out of the source's own scores.
- **Text**: a **strict cutoff**, per-image debiasing against neutral prompts, calibrated so that nonsense
  queries give nothing at Normal.
- **Five levels**, *Similar images* in the ⚙ menu, one setting for both.
- **The closeness shown** as a tile badge, in the Details row and in the tooltip — how far the result
  stands out, 0–100 %, the same for both.
- **The fp16 text model**: later, out of this workfile.

No question left open: the design is ready for the go.

### Iteration 3 — 2026-10-29 — ✅ Implemented

Go given: **code, unit tests and documentation**, in a **worktree** —
`.claude/worktrees/similar-images-accuracy`, branch `feature/similar-images-accuracy`, created from `main`
at `e313f25`. The Branch Gate is skipped: the worktree's branch is dedicated.

### Iteration 4 — 2026-10-29 — 🧭 Implementation choices

No project rule broken. The choices the frozen design left open, or that diverge from it:

- **The vocabulary's concepts as the neutral texts**, not a list of about 60 prompts: the 1 018 concepts the
  tags are chosen from are already embedded and cached (`vision-concepts.bin`), so the debiasing costs no
  text model call, and the set is broader. `VisionTags.ConceptEmbeddings` gives them.
- **The text margin is 2.5** — the sweep's value where nonsense finds nothing at Normal (§ Calibration).
  The real text matches become rare: what the user chose with *strict cutoff*.
- **Each image's spread is kept with its embedding** (a weak table keyed by the array), rather than "while
  the vision index is unchanged": a re-embedded image is recomputed by itself, nothing to invalidate. The
  concepts are loaded once per session: a vocabulary edited meanwhile reaches the search at the next launch.
- **Text results ordered by how far they stand out of their concepts** (`d`), image results by `z` — the
  same order as their score.
- **The cutoffs kept** (1.5 / 2.0 / 2.5 / 3.0 / 3.5): Very loose reaches the 100-result cap — the old
  behaviour, with the closeness shown.
- **The closeness label** is drawn in the Thumbnails view only (the Details row writes it before the size);
  the group count's label drawing became `ThumbnailGrid.PaintCornerLabel`, shared.
- **The tooltip's closeness line** is a plain line of `ExplorerRow.Tip`, not an icon line of the indexed
  content: it belongs to the search, not to the index.
- **The accents check is by hand**: the query is built in the panel from the search box; the harness
  cannot reach it.
- **Test data for the delivery launch**: the worktree's exe folder gets a copy of the user's engines,
  index, `people.json`, favorites and settings, the version-2 faces of the previous workfile's harness as
  its `files.faces`; the user's own data next to `main`'s exe is untouched. The `bin` folder is ignored
  by git.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-29 | Relative cutoff, texts against the concepts, source left out, accents kept, closeness shown, ⚙ Similar images; calibrated |
| Unit tests | 3, 4 | 2026-10-29 | No test project; the throwaway harness ran 3 checks and the calibration on 2 308 real embeddings (§ Test Impact) |
| RULES.md | 3 | 2026-10-29 | § Image Content Analysis: standing out, closeness, strictness levels |
| Glossary | 3 | 2026-10-29 | Closeness, Similar images setting; similarity tier and similar images updated (EN + FR) |
| README | 3 | 2026-10-29 | What the images show: standing out, closeness, Similar images (EN + FR) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where do you see the images too far apart? | Find similar images; the 💡 text search's similarity tier | 2026-10-29 |
| 2 | Which levers are accepted? | A score cutoff; a strictness setting; the score shown (not a model change) | 2026-10-29 |
| 3 | Is the subject straightforward or tricky / long? | Tricky / long | 2026-10-29 |
| 4 | *(the user's)* How is the global search ranking computed? The file name should weigh more than the content | Answered: relevance tiers, the path matches first (every word in the name, then fewer), presets, people, the content by reliability, the similarity tier last; the sort order inside a tier — the name already outranks the content | 2026-10-29 |
| 5 | Find similar's cutoff: relative (z) or absolute? | Relative | 2026-10-29 |
| 6 | The text similarity tier: strict cutoff, dropped, or kept with its closeness shown? | Strict cutoff | 2026-10-29 |
| 7 | The strictness levels: five or three; one setting or one each? | Five levels, one setting for both | 2026-10-29 |
| 8 | The closeness shown: badge, Details line, tooltip? | All three | 2026-10-29 |
| 9 | Try the text model's fp16 export before the go, or later? | Later | 2026-10-29 |
| 10 | The closeness shown: the raw cosine, or how far the result stands out, on a 0–100 scale? | How far it stands out, 0–100 % | 2026-10-29 |

---

*Last updated: 2026-10-29*
