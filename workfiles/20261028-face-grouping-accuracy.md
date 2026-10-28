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
decoded image's size — both a face index version 2 and the face pass run again (agreed, § Face Index
Version 2).

---

## Face Index Version 2

Agreed (Q&A 5): each face carries what its quality needs, the face pass run once again over the index.

- `FaceBox` gains its **five landmarks** (in fractions of the image, as its box) and its **side in
  pixels** — the shorter side of its box in the pixels the face pass analysed (`DecodePixels`' size),
  which is what the embedding was made from.
- `files.faces` **version 2** (`FaceIndex.Version`): a version-1 file is rejected as today, so the face
  pass starts over and every image is analysed again, with its own bar, as on a first run.
- The detection is unchanged (same model, `MinScore`, `MinFaceSide`, NMS): the boxes come out the same,
  so the **face keys** of `people.json` — path and rounded box (`FaceKeys`) — still match, and the
  user's pins and exclusions survive the re-analysis.

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

- **What it reads** (`FaceQuality.Of`): the YuNet score, the face's **side in pixels** and its **pose** —
  how frontal it is, from the landmarks (the nose's offset from the eyes' midpoint, relative to the
  eyes' distance) — each mapped to (0, 1] and multiplied. The curves are set by the calibration.
- **Its weight**: a face adds `q × embedding` to the sums and `q` to `W`, so a poor face pulls a group's
  average less than a clear one.
- **Its threshold**: a face needs `threshold + QualityMargin × (1 − q)` to join a seed, a group or a
  person — the poorer the face, the closer it must be.
- `FaceRef.Quality` (the representative face, `FaceGroup.Best`) reads the same quality.

### Low Quality Tile

Agreed (Q&A 7): the poor faces close to no one are set apart.

- A face that joins no group nor person **and** whose quality is under a floor
  (`FaceQuality.LowFloor`) is a **low-quality face**; a clear face close to no one stays a **face
  alone**, as today.
- The People view shows a **Low quality (n)** tile while some exist, after the groups and the *Hidden*
  tile, before the faces alone, drawn and opened as the *Hidden* tile is: opened, it lists them as face
  tiles (the face cut out of its file), the breadcrumb `People › Low quality`.
- They keep the faces alone's actions: dragged onto a group, a low-quality face joins it (pinned).
- A search in the People view filters by name and leaves them out, as it does the faces alone.

---

## Strictness Setting

An app setting of the ⚙ menu (§ App Settings of RULES.md), next to *Face analysis*:

- *Face grouping* › **five levels**, one checked (Q&A 6): *Very loose*, *Loose*, *Normal* (the
  default), *Strict*, *Very strict* — each giving the **grouping threshold** (`FaceGrouping.Thresholds`),
  the stricter the higher. Stored in `settings.json` (`AppSettings.FaceGrouping`, by its level's
  index, an unknown value giving *Normal*), read at start-up — the `GuideApproach` pattern.
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
- On the user's data (Q&A 8): their `files.faces` (version 2, once the face pass has run again) and
  `people.json`, next to the exe, **read-only**, nothing of them copied into the repository; their named
  people, several with several pinned faces, are the ground truth.
- *Normal* is set where the false merges become rare without splitting the named people much; the
  other levels spread around it. The values chosen and the harness's figures go into the run's 🧭 entry.

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
| A frontal, large, sure face scores a higher quality than a turned, small or unsure one | Throwaway harness (scratchpad) | Create |
| `files.faces` version 2 round-trips landmarks and pixel side; a version-1 file restarts the face pass | Throwaway harness (scratchpad) | Create |
| The face keys of `people.json` still match after the re-analysis | By hand, in the app (pins kept) | — |
| The ⚙ level is saved, read back at start-up, and regroups at once | By hand, in the app | — |
| The Low quality tile lists the poor faces close to no one; one dragged onto a group joins it | By hand, in the app | — |

---

## Open Questions

- [x] ~~1. Store each face's landmarks and size in pixels (face index version 2, the face pass run
  again over every image), or keep the quality to the score and the relative size?~~ → Store them:
  version 2, the face pass run again (§ Face Index Version 2)
- [x] ~~2. The strictness levels: how many, and their names?~~ → Five: Very loose, Loose, Normal (the
  default), Strict, Very strict
- [x] ~~3. A face too poor to reach any threshold: face alone, or kept out of the view?~~ → A **Low
  quality** tile (§ Low Quality Tile)
- [x] ~~4. May the calibration read the user's `files.faces` and `people.json`; are there enough named
  people?~~ → Yes, read-only; enough named people

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

### Iteration 2 — 2026-10-28

The four open questions answered (Q&A 5–8):

- **Face index version 2**: each face stores its landmarks and its side in pixels; the face pass runs
  again once; the face keys stay valid. The quality reads the score, the pixel side and the pose.
- **Five strictness levels**, *Normal* by default.
- **Low quality tile** for the poor faces close to no one; the clear ones stay faces alone.
- **Calibration on the user's data**, read-only, the named people as ground truth.

No question left open: the design is ready for the go.

### Iteration 3 — 2026-10-28 — ✅ Implemented

Go given: **code, unit tests and documentation**, in a **worktree** —
`.claude/worktrees/face-grouping-accuracy`, branch `feature/face-grouping-accuracy`, created from `main`
at `f8af91f`. The Branch Gate is skipped: the worktree's branch is dedicated.

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
| 5 | Store landmarks and pixel size (index v2, face pass re-run) for the quality, or score and relative size only? | Store them, re-analyse | 2026-10-28 |
| 6 | The strictness levels: how many, and their names? | Five levels | 2026-10-28 |
| 7 | A face too poor to reach any threshold: face alone, or kept out of the view? | A *Low quality* tile | 2026-10-28 |
| 8 | May the calibration read the user's `files.faces` and `people.json`; are there enough named people? | Yes, enough named people | 2026-10-28 |

---

*Last updated: 2026-10-28*
