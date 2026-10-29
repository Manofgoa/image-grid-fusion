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

The design proposed (Open Questions settle its open points):

- **Cutoff by standing out**: a result is kept when its score stands out of the source's scores over the
  index by at least the strictness level's `z` (§ Strictness Setting), at most `SimilarCount` (100),
  the closest first. Mean and spread computed per query over every embedded image — one pass, already
  made to score them.
- **The source image is left out** of its own results.
- **None standing out**: the caption line says so (`Similar to <name> — nothing close enough`), rather
  than an empty list without a word.

---

## Text Similarity (the 💡 similarity tier)

- **The query embedded with its accents**: the raw text typed, lowercased, not `FileSearch.Words`.
- **A cutoff**: open — the measurements show that no per-query cutoff separates noise (Open Question 2).
- Its place stays the **last tier**, by score.

---

## Strictness Setting

An app setting of the ⚙ menu, after *Visual analysis* (the `Face grouping` pattern): levels giving the
cutoff — Open Question 3. Changing it applies to the next search; the list shown is searched again.

---

## Closeness Shown

- `ExplorerRow` carries the result's **closeness** (`Similarity`, from `SearchMatch.Similarity`), filled by
  `SimilarRows` and `WithFavorites` for the similarity tier only.
- What is shown, and where — Open Question 4.

---

## Test Impact

No test project in the repository (`CONTRIBUTING.md`); checked by a throwaway harness on the user's
embeddings and by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A result is kept only when its score stands out of the query's distribution by the level's `z` | Throwaway harness (scratchpad) | Create |
| Find similar leaves the source image out | Throwaway harness (scratchpad) | Create |
| The text query reaches the text model with its accents | Throwaway harness (scratchpad) | Create |
| The closeness reaches the row and is shown | By hand, in the app | — |
| The ⚙ level is saved, read back at start-up, and the shown list is searched again | By hand, in the app | — |

---

## Open Questions

- [ ] 1. Find similar's cutoff: **relative** (how far a result stands out of the source's own scores, z)
  — recommended by the measurements — or an **absolute** cosine floor?
- [x] ~~2. The text similarity tier: strict cutoff, dropped, or kept with its closeness shown?~~ → A
  **strict cutoff**, with per-image debiasing (§ Text Similarity)
- [x] ~~3. The strictness levels: five or three; one setting or one each?~~ → **Five**, one setting for
  both features
- [x] ~~4. The closeness shown: badge, Details line, tooltip?~~ → **All three**
- [x] ~~5. Try the text model's fp16 export before the go, or later?~~ → **Later**: out of this workfile,
  a follow-up once it is delivered
- [ ] 6. The number shown: the **raw cosine** (0.83 for an image, 0.12 for a text — not comparable), or
  **how far the result stands out**, mapped to 0–100 % the same way for both?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project; a throwaway harness stands in |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where do you see the images too far apart? | Find similar images; the 💡 text search's similarity tier | 2026-10-29 |
| 2 | Which levers are accepted? | A score cutoff; a strictness setting; the score shown (not a model change) | 2026-10-29 |
| 3 | Is the subject straightforward or tricky / long? | Tricky / long | 2026-10-29 |
| 4 | *(the user's)* How is the global search ranking computed? The file name should weigh more than the content | Answered: relevance tiers, the path matches first (every word in the name, then fewer), presets, people, the content by reliability, the similarity tier last; the sort order inside a tier — the name already outranks the content | 2026-10-29 |
| 5 | Find similar's cutoff: relative (z) or absolute? | | |
| 6 | The text similarity tier: strict cutoff, dropped, or kept with its closeness shown? | Strict cutoff | 2026-10-29 |
| 7 | The strictness levels: five or three; one setting or one each? | Five levels, one setting for both | 2026-10-29 |
| 8 | The closeness shown: badge, Details line, tooltip? | All three | 2026-10-29 |
| 9 | Try the text model's fp16 export before the go, or later? | Later | 2026-10-29 |
| 10 | The closeness shown: the raw cosine, or how far the result stands out, on a 0–100 scale? | | |

---

*Last updated: 2026-10-29*
