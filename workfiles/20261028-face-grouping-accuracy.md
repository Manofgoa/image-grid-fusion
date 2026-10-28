# Face Grouping Accuracy

> Working document — the People view's face groups too often mix different people: making the
> grouping stricter and aware of each face's quality, with a strictness setting.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The People view (origin: `workfiles/20261027-face-detection.md`) groups the faces of the indexed
images into people. The user reports **groups mixing different people** — false merges; a person
split over several groups is not the complaint.

Scope, as agreed (Q&A 1–3):

- **Faces only.** The image similarity search (*Find similar images*, the similarity tier) also finds
  images too far apart: it gets a **workfile of its own, later**.
- Levers accepted: a **stricter threshold and a grouping algorithm that does not chain**, **each face's
  quality weighed in the scoring**, and a **strictness setting** in the ⚙ menu.
- Lever not taken: **changing the recognition model** — YuNet and SFace stay.

Components: `Explorer/FaceGrouping.cs` (the grouping), `Explorer/FaceAnalyzer.cs` and
`Explorer/FaceIndex.cs` (what a face carries), `UI/AppSettings.cs` and `UI/MainForm.cs` (the ⚙ menu),
`UI/FileExplorerPanel.cs` (`Regroup`).

---

## Diagnosis

What the exploration found (Iteration 1), the input of the design below.

### The recognition pipeline is sound

Checked against OpenCV's reference implementation (`FaceRecognizerSF`, `alignCrop`):

- The alignment's template is SFace's five points, the similarity transform the least-squares one
  without mirror, the sampling bilinear (`FaceAnalyzer.Align`, `SimilarityTransform`).
- SFace gets **RGB**, 0 to 255, planes — what OpenCV feeds it (`blobFromImage(…, swapRB: true)`).
- YuNet's landmarks reach the template in the model's order, and its boxes and landmarks are decoded as
  OpenCV decodes them (`FaceAnalyzer.Detect`).

So the embeddings are as good as SFace makes them: the mistakes come from **how they are grouped**.

### The grouping lets people mix

`FaceGrouping.Compute` today:

1. Faces sorted by key; each pinned face goes to its person; any other joins the person whose pinned
   faces' **normalized mean** is closest at or above `SameThreshold` (0.363), else the closest
   auto-cluster by its **normalized running mean**, else starts one.
2. `MergeClose`: every pair of clusters whose normalized means reach 0.363 is united, **transitively**
   (union-find).

Three causes of false merges, each enough on its own:

| Cause | Why it mixes people |
|---|---|
| **Single linkage** in `MergeClose` | A ≈ B and B ≈ C unite A and C, however far apart; over hundreds of clusters, chains form |
| **Normalized means** compared to a pairwise threshold | The mean of several embeddings, scaled back to length 1, sits closer to everything than its faces do: 0.363 — SFace's value for **one face against one face** — is laxer against a mean than it reads |
| **No quality** in the scoring | A small, blurred, turned or partly hidden face gives a noisy embedding, which lands near anyone, yet counts as much as a clear one and pulls the means |

Plus: the greedy pass depends on the faces' order (the first faces anchor a cluster), and 0.363 itself
is SFace's equal-error point on LFW pairs — over a collection of thousands of faces, a false accept
rate fine for one pair becomes many false merges.

### What a face carries

`FaceBox(Bounds, Score, Embedding)` in `files.faces` version 1: the box in fractions of the image, the
YuNet score, the embedding (128 values, length 1). **No landmarks, no size in pixels.** The score and
the relative size are usable as they are; a pose (frontality) needs the landmarks, a size in pixels the
decoded image's size — both a face index version 2 and the face pass run again (Open Question 1).

---

## Grouping

The design proposed; the thresholds' values are calibrated during implementation (§ Calibration).

### Average similarity instead of normalized means

- A cluster — and a person's pinned faces — keeps the **weighted sum** of its embeddings `S` and its
  total weight `W` (weights: § Quality).
- A face's similarity to a cluster is the **average** of its similarity to the cluster's faces:
  `e · S / W`. Two clusters' similarity is the average over every pair of their faces:
  `Sᵢ · Sⱼ / (Wᵢ Wⱼ)`. Both cost one dot product, and compare honestly to a **pairwise** threshold — no
  normalized mean any more.

### Two stages, no chaining

1. **Seeds**: the greedy pass kept, at a **stricter seed threshold** (the grouping threshold plus a
   margin, `FaceGrouping.SeedMargin`): a face joins the seed it is closest to on average, else starts
   one. Tight seeds make the faces' order matter little.
2. **Average-linkage merging** replaces `MergeClose`: the two closest seeds on average are merged, one
   pair at a time, the sums added, until no pair reaches the grouping threshold — a nearest-neighbour
   chain, O(seeds²) dot products. Merging a pair never unites a third cluster through it.

### People first

The person-matching step uses the same average similarity against the pinned faces' weighted sum, with
the face's own threshold (§ Quality); `Excluded` is kept as it is.

---

## Quality

Each face gets a **quality** `q` in (0, 1], weighing it in the scoring (Q&A 2: *quality taken into
account in the scoring* — no hard filter):

- **What it reads**: the YuNet score and the face's size, from what `files.faces` holds today; the pose
  (from the landmarks) and the size in pixels if a version 2 stores them (Open Question 1).
- **Its weight**: a face adds `q × embedding` to the sums and `q` to `W`, so a poor face pulls a group's
  average less than a clear one.
- **Its threshold**: a face needs `threshold + QualityMargin × (1 − q)` to join a seed, a group or a
  person — the poorer the face, the closer it must be. A face reaching none stays a **face alone**
  (Open Question 3).
- `FaceRef.Quality` (the representative face, `FaceGroup.Best`) reads the same quality.

---

## Strictness Setting

An app setting of the ⚙ menu (§ App Settings of RULES.md), next to *Face analysis*:

- *Face grouping* › a few levels, one checked (Open Question 2), each giving the **grouping
  threshold** (`FaceGrouping.Thresholds`, the default level in the middle). Stored in `settings.json`
  (`AppSettings.FaceGrouping`), read at start-up.
- Changing it regroups at once (`FileExplorerPanel.Regroup(force: true)`), the People view refreshed.
- The user's decisions are stored on faces: a stricter level never undoes a pin, an exclusion or a merge.
  A person's faces the grouping added (not pinned) follow the level.
- Disabled while the face analysis is off, like *Forget faces…*.
- Replaces RULES.md § People's *"a fixed constant — no setting"*.

---

## Calibration

The thresholds (`Thresholds`, `SeedMargin`, `QualityMargin`) and the quality's formula are set on real
faces, not guessed:

- A throwaway harness, in the scratchpad, never in the repository: it reads a `files.faces` and a
  `people.json`, runs `FaceGrouping.Compute` at a range of values, and reports per value the number of
  groups, the faces alone, and — using the **named people as ground truth** — how often faces pinned to
  two different people land in one group (false merges) or one person's pinned faces land apart
  (splits).
- On which data: Open Question 4.

---

## Test Impact

No test project in the repository (`CONTRIBUTING.md`: *check a change by hand*); the grouping is checked
by the calibration harness and by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Average linkage never unites A and C through B when A and C are below the threshold | Throwaway harness (scratchpad) | Create |
| A face's average similarity to a cluster equals the mean of its pairwise similarities | Throwaway harness (scratchpad) | Create |
| A poorer face needs a higher similarity to join; its weight pulls the sum less | Throwaway harness (scratchpad) | Create |
| A stricter level gives fewer false merges on the named people, pins and exclusions untouched | Throwaway harness (scratchpad), on real faces | Create |
| The ⚙ level is saved, read back at start-up, and regroups at once | By hand, in the app | — |

---

## Open Questions

- [ ] 1. Store each face's **landmarks and size in pixels** (face index version 2, the face pass run
  again over every image) so the quality reads the pose and the true size — or keep the quality to the
  score and the relative size, no re-analysis?
- [ ] 2. The strictness levels: **how many, and their names** (e.g. *Loose / Normal / Strict*), the
  default being the middle one?
- [ ] 3. A face too poor to reach any threshold: listed as a **face alone** as today, or kept out of the
  People view (a *Low quality* tile, or not listed)?
- [ ] 4. Calibration data: may the run read **your `files.faces` and `people.json`** (next to the exe,
  read-only, nothing copied into the repository)? Are there **named people** enough among them — a few
  persons with several pinned faces each — to serve as ground truth?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-28

Request: the face detection — and the similar-image search — find things too far apart; start with the
faces, which often put different people together; how to improve and refine the analysis?

- Scoping (Q&A 1–4): false merges only; levers: threshold and algorithm, quality in the scoring,
  strictness setting — no model change; the similar images in a later workfile; a tricky subject.
- Exploration: the recognition pipeline checked sound against OpenCV's reference; the grouping chains
  (single linkage), compares normalized means to a pairwise threshold, and ignores quality (§ Diagnosis).
- Proposed: average similarity from weighted sums, strict seeds then average-linkage merging, a quality
  weight and threshold margin per face, a ⚙ strictness level, thresholds calibrated on the named people.
- Four questions left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project; the calibration harness stands in |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What exactly do you see in the People view? | Groups mixing different people | 2026-10-28 |
| 2 | Which levers are accepted to improve the grouping? | Threshold + algorithm; quality taken into account in the scoring; a strictness setting (not a model change) | 2026-10-28 |
| 3 | And the similar-image search? | A separate workfile, later | 2026-10-28 |
| 4 | Is the subject straightforward or tricky / long to explore? | Tricky / long | 2026-10-28 |
| 5 | Store landmarks and pixel size (index v2, face pass re-run) for the quality, or score and relative size only? | | |
| 6 | The strictness levels: how many, and their names? | | |
| 7 | A face too poor to reach any threshold: face alone, or kept out of the view? | | |
| 8 | May the calibration read the user's `files.faces` and `people.json`; are there enough named people? | | |

---

*Last updated: 2026-10-28*
