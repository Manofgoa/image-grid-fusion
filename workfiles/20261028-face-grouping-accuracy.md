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

- `FaceBox` gains its **five landmarks** and its **side**, both **in pixels of the image as analysed**
  (`DecodePixels`' size, what the embedding was made from) — the side being the shorter side of its box.
  Fractions of a non-square image would skew the pose's geometry (Iteration 4).
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
   margin, `FaceGrouping.SeedMargin`, 0.08): a face joins the seed it is closest to on average, else
   starts one — the **clearest faces first** (quality descending, then key), so seeds grow around good
   faces. Tight seeds make the faces' order matter little.
2. **Average-linkage merging** replaces `MergeClose` (`FaceGrouping.MergeAverage`): the closest pair of
   seeds on average is merged, one pair at a time, the sums added, while a pair reaches the grouping
   threshold **plus the quality margin of its poorer side** — a cluster's quality is its best face's —
   so a poor face alone in its seed joins no group it could not join as a face. Only the pairs within
   reach are queued (a priority queue), a merge queuing the merged cluster's pairs again: O(seeds²) dot
   products once, then O(seeds) per merge. Merging a pair never unites a third cluster through it.

### People first

The person-matching step uses the same average similarity against the pinned faces' weighted sum, with
the face's own threshold (§ Quality); `Excluded` is kept as it is.

---

## Quality

Each face gets a **quality** `q` in (0, 1], weighing it in the scoring (Q&A 2: *quality taken into
account in the scoring* — no hard filter):

- **What it reads** (`FaceQuality.Of`): the YuNet score, the face's **side in pixels** and its **pose** —
  how frontal it is, from the landmarks (the nose's offset from the eyes' midpoint along the eyes' line,
  relative to the eyes' distance) — each a linear ramp to 1, multiplied:

  | Factor | Lowest (at) | 1 from |
  |---|---|---|
  | Score | 0.55 (`MinScore`, 0.75) | 0.92 |
  | Side | 0.45 (`MinFaceSide`, 40 px) | 112 px, SFace's input |
  | Pose | 0.3 (offset 0.5, three-quarter view) | offset 0, facing |

  On the user's 3 708 faces: p10 0.27, p25 0.42, median 0.62, p75 0.83.
- **Its weight**: a face adds `q × embedding` to the sums and `q` to `W`, so a poor face pulls a group's
  average less than a clear one.
- **Its threshold**: a face needs `threshold + QualityMargin × (1 − q)` to join a seed, a group or a
  person — the poorer the face, the closer it must be (`QualityMargin` 0.06).
- `FaceRef.Quality` (the representative face, `FaceGroup.Best`) reads the same quality.

### Low Quality Tile

Agreed (Q&A 7): the poor faces close to no one are set apart.

- A face that joins no group nor person **and** whose quality is under a floor
  (`FaceQuality.LowFloor`, 0.35) is a **low-quality face**; a clear face close to no one stays a **face
  alone**, as today.
- The People view shows a **Low quality (n)** tile while some exist, after the groups and the *Hidden*
  tile, before the faces alone, its picture the best of them, opened as the *Hidden* tile is: it lists
  them as face tiles (the face cut out of its file), the breadcrumb `People › Low quality`. Like the
  *Hidden* tile, it is no person: no rename, hide, merge or drag of the tile itself.
- They keep the faces alone's actions — a face tile, its context menu — but the open Low quality list
  shows no group to drop one onto: dragging a low-quality face onto a group is not possible yet
  (Iteration 4, an open question).
- A search in the People view filters by name and leaves them out, as it does the faces alone.

---

## Strictness Setting

An app setting of the ⚙ menu (§ App Settings of RULES.md), next to *Face analysis*:

- *Face grouping* › **five levels**, one checked (Q&A 6): *Very loose*, *Loose*, *Normal* (the
  default), *Strict*, *Very strict* — each giving the **grouping threshold** (`FaceGrouping.Thresholds`:
  0.28, 0.32, **0.36**, 0.40, 0.44). Stored in `settings.json` (`AppSettings.FaceGrouping`, by its level's
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
- On the user's data (Q&A 8), **read-only**, nothing of them copied into the repository: the harness ran
  the face pass itself over the 1 656 files the version-1 `files.faces` found faces in (3 min) — the same
  3 708 faces, **every face key identical** — and wrote a version-2 index in the scratchpad.
- **The named people were too few to be the ground truth**: `people.json` holds 2 people, 1 named, 51
  pinned faces — one person with several faces, so false merges between two people cannot be counted.
  The harness measures instead the **cohesion** of the groups, which needs no names: the **outliers** —
  faces whose mean similarity to the rest of their group is under 0.25, almost surely someone else for
  SFace — the share of **far pairs** (under 0.15) inside groups, the **biggest group**; and the named
  person's pairs kept together (**recall**).
- Figures (3 708 faces, people left out so the algorithm alone is measured):

  | Setting | Groups | In groups | Alone | Low quality | Biggest | Outliers | Far pairs | Recall |
  |---|---|---|---|---|---|---|---|---|
  | Before (greedy + single linkage, 0.363) | 125 | 3 263 | 445 | — | **2 833** | **2 840** | **73.1 %** | 100 % |
  | Very loose, 0.28 | 795 | 3 457 | 193 | 58 | 195 | 17 | 0.8 % | 88.2 % |
  | Loose, 0.32 | 765 | 3 123 | 471 | 114 | 189 | 1 | 0.2 % | 84.5 % |
  | **Normal, 0.36** | 641 | 2 694 | 829 | 185 | 188 | **0** | 0.1 % | 80.8 % |
  | Strict, 0.40 | 537 | 2 351 | 1 087 | 270 | 167 | 0 | 0.0 % | 61.0 % |
  | Very strict, 0.44 | 485 | 2 134 | 1 244 | 330 | 167 | 0 | 0.0 % | 60.6 % |

  Before, one group held three quarters of the faces: the chaining, measured. *Normal* is the lowest
  threshold with no outlier; the margins were swept (seed 0 / 0.08, quality 0 / 0.06 / 0.12 / 0.2,
  weighted or not): 0.06 keeps the named person together best at zero outliers, the weighting changes
  little.

---

## Test Impact

No test project in the repository (`CONTRIBUTING.md`: *check a change by hand*); the grouping is checked
by the calibration harness and by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Average linkage never unites A and C through B when A and C are below the threshold | Throwaway harness (scratchpad) | Created — passes |
| A face's average similarity to a cluster equals the mean of its pairwise similarities | Throwaway harness (scratchpad) | Created — passes |
| A poorer face needs a higher similarity to join — at the merging of seeds too; it lands in Low quality | Throwaway harness (scratchpad) | Created — failed first (the merging ignored the margin), passes since |
| A stricter level gives fewer false merges on the named people, pins and exclusions untouched | Throwaway harness (scratchpad), on real faces | Created — measured by cohesion, too few named people (§ Calibration) |
| A frontal, large, sure face scores a higher quality than a turned, small or unsure one | Throwaway harness (scratchpad) | Created — passes |
| `files.faces` version 2 round-trips landmarks and pixel side; a version-1 file restarts the face pass | Throwaway harness (scratchpad) | Created — round-trip identical on 3 708 faces; the version-1 rejection is the existing `Load` check |
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
  people?~~ → Yes, read-only; enough named people *(revised 2026-10-28, see Iteration 4: one named person
  only — the calibration used the groups' cohesion)*
- [ ] 5. Dragging a low-quality face onto a group: the open Low quality list shows no group. Show the
  groups above the low-quality faces there, let a face be dropped onto the breadcrumb's `People`, or leave
  it?

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

### Iteration 4 — 2026-10-28 — 🧭 Implementation choices

No project rule broken. The choices the frozen design left open, or that diverge from it:

- **Landmarks in pixels, not fractions** (§ Face Index Version 2): fractions of a non-square image
  distort the eyes-to-nose geometry the pose reads. The side was already in pixels.
- **The quality margin also gates the merging of seeds** (§ Grouping): the design said a face needs the
  margin "to join a seed, a group or a person", but the merging step compared seeds without it — the
  harness's test caught a poor face alone in its seed merging into a group it could not join as a face.
  A pair now needs the threshold plus its poorer side's margin, a cluster's quality being its best face's.
- **A priority queue instead of a nearest-neighbour chain** (§ Grouping): the chain needs a reducible
  linkage, which the per-pair margin breaks; the closest pair is now taken from a queue of the pairs
  within reach — exact, and as fast on 3 708 faces (the whole evaluation of 70 settings ran in 18 s).
- **The clearest faces seed first** (§ Grouping): the greedy pass orders by quality, then by key — the
  design only asked for tight seeds.
- **The quality's ramps** (§ Quality): score 0.75 → 0.92, side 40 → 112 px, pose offset 0.5 → 0, each
  multiplied; `LowFloor` 0.35.
- **Calibrated values** (§ Calibration): thresholds 0.28 / 0.32 / **0.36** / 0.40 / 0.44, `SeedMargin`
  0.08, `QualityMargin` 0.06 (designed as 0.12: kept the named person apart more, for no fewer outliers).
- **Calibration by cohesion, not by named people** (§ Calibration): `people.json` held one named person;
  false merges between people could not be counted from it, so outliers, far pairs and the biggest group
  stood in — the decision taken instead of asked, per the run's rules. Open Question 4 annotated.
- **Low-quality faces cannot be dragged onto a group** (§ Low Quality Tile): the open Low quality list
  shows no group to drop onto; not designed — left as Open Question 5 rather than invented.
- **The *Low quality* tile's picture** is its best face; the tile has no context actions of a person.
- **Best face** (`FaceGroup.Best`) is the best quality, the larger side breaking ties.
- **Test data for the delivery launch**: the worktree's exe folder got a copy of the user's engines,
  index, `people.json`, favorites and settings — `ExplorerMode` set to *People* in that copy — and the
  harness's version-2 faces as its `files.faces`, so the People view shows at once without a 3-minute
  re-analysis; the user's own data next to `main`'s exe is untouched. `bin\` is ignored by git.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-28 | Face index v2, quality, average linkage, ⚙ Face grouping, Low quality tile; calibrated |
| Unit tests | 3, 4 | 2026-10-28 | No test project; the throwaway calibration harness ran 4 synthetic checks and the evaluation on 3 708 real faces (§ Test Impact) |
| RULES.md | 3 | 2026-10-28 | § People: face index v2, how faces are grouped, the Low quality tile |
| Glossary | 3 | 2026-10-28 | Face quality, low-quality face, face grouping (EN + FR) |
| README | 3 | 2026-10-28 | People view: Face grouping levels, the Low quality tile, the re-analysis after an update (EN + FR) |

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
