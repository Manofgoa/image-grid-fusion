# Face Detection — People View

> Working document — faces detected in the indexed images, grouped into people, shown in a new
> People view of the file explorer, named, merged, corrected, hidden, and found by name.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer gets a third view, **People**, next to the search view (favorites) and the
folder view: every face found in the base folder's images and PDF pages, grouped automatically
into **people** — one tile per person, their face, their name, their number of files; opened, the
person's files. The user can **name** a person, **merge** two people the grouping kept apart,
**remove** a face wrongly grouped, **hide** a person of no interest, and **find** a person's files
by typing their name in the search.

Delivered in **two phases** of this one workfile:

| Phase | Delivers |
|---|---|
| 1 | The face engine (downloaded on demand), the face pass over the index, the automatic grouping, the People view (person tiles, a person opened as a list of files) |
| 2 | Naming, adding a face to a group, merging and un-merging, removing a face, hiding a person — by drag and drop and context menu, the decisions kept in `people.json` — and the search by name |

What it is not: no face recognition against a known database, no tagging inside the cells, no
effect on the grid; videos and animated content are out (a GIF's first frame and a PDF's first
page are read, as the OCR reads them).

Components touched (from the exploration):

| Area | Where |
|---|---|
| Pass worker, pause rule, save cadence, progress bars | `UI/FileExplorerPanel.Extract`, `ExtractionPaused`, `TrySave`, `_passBars` |
| Pixels of an image / a PDF's first page | `Explorer/ContentExtractor.Decode` (`PdfPages.TryRenderFirstPage`) |
| ONNX Runtime (managed API already referenced, natives downloaded) | `ImageGridFusion.csproj` (`Microsoft.ML.OnnxRuntime 1.29.0`), `Explorer/OcrEngineFiles` |
| Direct file download | `Web/Downloader.DownloadAsync` |
| View switching | `FileExplorerPanel._folderView`, `SetFolderView`, `ApplyView`, `RefreshRows`, `Activate` |
| Rows and tiles | `UI/ThumbnailGrid` (`ExplorerRow`, folder tile painting) |
| Remembered view | `UI/AppSettings.SaveExplorerFolderView`, `ExplorerView` (enum by name) |
| User decisions store (model) | `Explorer/Favorites` (`favorites.json`: version + array, tmp-then-move save, tolerant load) |
| Search tiers | `Explorer/FileSearch` (`Rank.Tier`, `DescriptionTier`, `SearchFavorites`), `FileExplorerPanel.WithFavorites` |

---

## Face Engine

Two ONNX models of the OpenCV Zoo, run through ONNX Runtime's managed API (`InferenceSession`)
directly — no new package:

| Model | Role | Size | License |
|---|---|---|---|
| **YuNet** (`face_detection_yunet_2023mar.onnx`) | Detection: a box, a score and **5 landmarks** (eyes, nose, mouth corners) per face | ~230 KB | MIT |
| **SFace** (`face_recognition_sface_2021dec.onnx`) | Recognition: a **128-value embedding** of the face aligned to 112 × 112 from its landmarks | ~37 MB | Apache 2.0 |

- **Never shipped**: downloaded on demand next to the exe, like the OCR engine (RULES.md § Content
  OCR Passes) — each file from a **pinned URL** with its **SHA-256** checked, through
  `Downloader.DownloadAsync` (a direct file, not a zip entry), into `<name>.~downloading` then moved
  into place. Nothing is downloaded without the user's **Yes**.
- The models go into their own **`face-engine\`** next to the exe (`FaceEngineFiles.FolderName`).
  They need ONNX Runtime's native DLL, which stays the OCR engine's, in `ocr-engine\`: when it is
  missing, the face engine's download fetches it there too, through `OcrEngineFiles`, in the same
  confirmed offer.
- Its own availability check (`FaceEngineFiles.IsAvailable`), separate from `OcrEngineFiles`, so a
  missing face model never re-offers the OCR engine and the other way round.
- The InsightFace models (ArcFace, buffalo) are **not** used: their license is non-commercial.

---

## Face Pass

- One more pass of the extraction worker (`FileExplorerPanel.Extract`): same pause rule
  (`ExtractionPaused`), same save cadence, **its own progress bar** (a fifth colour), its tooltip
  saying how many files are done and how many faces were found.
- Over every **image and PDF** entry of the index — the pixels `ContentExtractor.Decode` gives
  (an image, a GIF's first frame, a PDF's first page). Videos are left out.
- Per file: YuNet on the decoded image (resized to its input), the boxes above a **score threshold**
  kept, after non-maximum suppression; faces smaller than a **minimum size** (constant, ~40 px on the
  decoded image) dropped — SFace is unreliable below; each kept face aligned from its 5 landmarks
  (similarity transform onto SFace's template) and embedded.
- A changed file (size or date, `ContentIndex.IsCurrent`'s rule) is analysed again; a file gone
  from the index drops its faces.
- Runs only while the **face analysis is on** (§ Privacy) and the face engine present; otherwise the
  pass is skipped and its bar hidden.
- It runs **right after the Windows OCR pass**, before RapidOCR, Inverted and Ring — the slower ones —
  so the People view fills early. The order of the OCR passes among themselves is unchanged.

### Face Index

Its **own file**, `index\files.faces`, never a slot of `files.content` — the embeddings would bloat a
text file loaded whole and scanned by every search, and mean nothing to its excerpt and arrow:

- Binary, versioned header, base folder; per file: path, size, ticks (the stamp), then its faces —
  box and landmarks in fractions of the image, score, embedding (128 floats). A file analysed with no
  face is stored with zero faces (done), a file not analysed yet is absent (not done).
- Saved atomically (temp file then move), loaded off the UI thread, like the content index.
- A **face key** identifies a face across sessions: its file's path and its box rounded — stable
  while the file is unchanged.

---

## Grouping

- Two faces are the **same person** when the cosine similarity of their embeddings reaches a
  **threshold** — SFace's published value, 0.363, as the starting point, tuned on real data during
  implementation (`FaceGrouping.SameThreshold`).
- Grouping is computed **from the face index and the user's decisions**, in the background, every
  time either changes — never stored as groups: what is stored are the faces and the decisions.
- **A decision is stored on faces, never on a computed group** (phase 2):
  - a **pinned face** belongs to a person the user acted on (named, merged, hidden, a face dropped on
    it) — its face key, its person id, and a copy of its embedding, so the person survives its file
    moving or the index being rebuilt;
  - an **excluded face** never joins that person again (the face removed from it);
  - a **merge** is a link between two persons (`mergedInto`), each keeping its own pinned faces.
- The computation: the persons with pinned faces first, each face of the index joining the closest
  one above the threshold (never one it is excluded from); the remaining faces grouped among
  themselves (agglomerative, average linkage) into **unnamed people**.
- An unnamed person has no stable id: naming, hiding or merging it pins its current faces to a new
  person id.

---

## People View

- A **third view** of the file explorer, beside the search view and the folder view, reached by a
  **drop-down of the three views** — *Favorites* / *Folders* / *People* — replacing the 📁 toggle
  button at the start of the search row. The current view is remembered between sessions, saved by
  name like `ExplorerView` — an older settings file's folder-view flag read as before.
- The view lists **face groups first, then the ungrouped faces**:
  - a **face group** (*dossier visage*) — a person seen in two faces or more — is drawn **like a
    folder tile in what it means, not in its look**: a tile saying it gathers several files (the
    person's best face — largest box × score — cropped from its file, filling the tile, a stack /
    count badge showing it is a group), its name in bold — or *Unnamed* — and its number of files
    below. Named groups A→Z first, then unnamed groups by number of files, descending.
  - an **ungrouped face** — a face no other face is close to — is listed **after the groups, as an
    individual file**: a file tile (drag into a cell, heart, Details view) whose picture is **the face
    cropped from its file**, the file's name below — so the face dragged is the one seen, even in a
    file holding several. A file with two ungrouped faces gives two tiles.
- **Opened** (double-click on a group): like an open folder — the breadcrumb `People › <name>`, ↑ back
  to the people. It lists:
  - the groups **merged into it**, as **sub-groups** (one sub-group tile each), first;
  - then **every file of the group and of all its sub-groups**, as ordinary file tiles, in the sort
    order — so the merged person's whole content is seen at once.
- While the face pass has not run (engine missing, or the analysis turned off), the view says why
  and offers what is missing.
- Searching in the People view filters the groups by name; inside an opened group, it searches that
  group's files, as the folder view searches below its open folder.

### Decisions (Phase 2)

All by **drag and drop** inside the People view, or the **context menu** (right click):

| Action | Gesture | Effect |
|---|---|---|
| **Name** | *Rename…* on a group tile, or F2 | The group keeps that name |
| **Add a face** | Drag an ungrouped face's tile — or a file tile inside another group — onto a group tile | That face joins the group (pinned to it); dropped on a group, it leaves the one it was in |
| **Merge** | Drag a group tile onto another group tile | The dragged group becomes a **sub-group** of the target; the target keeps its name, takes the dragged one's if it has none. A group with sub-groups dragged onto another brings them along: they all become sub-groups of the target, flat — a group holds sub-groups one level deep |
| **Un-merge** | *Un-merge* on a sub-group tile, inside an opened group | The sub-group becomes a group of its own again, as it was before the merge — its name, its faces |
| **Remove a face** | *Not <name>* on a file inside an opened group | That file's face of this group excluded from it; it goes back to the automatic grouping, never to this group again |
| **Hide** | *Hide* on a group tile | The group leaves the view; a *Show hidden* entry of the view lists them, *Unhide* brings one back |

- A merge is **kept as a link**, never as faces moved: each group keeps its own faces, the merged one
  pointing at its target (`mergedInto`), so un-merging restores it exactly.
- Kept in **`people.json`**, next to the exe, through its own store (`Explorer/People.cs`), on the
  model of `favorites.json`: a version and a `people` array — id, name, hidden, `mergedInto`, pinned
  faces (face key + embedding), excluded face keys — temp-file-then-move save, tolerant load (a bad
  entry skipped). Written on each user action, by a `--new-instance` too (user actions, as for the
  favorites).
- Out of the undo history (the grid's), like the favorites.

---

## Search by Name (Phase 2)

- Under **Aa** (the name criterion), a word matching a **person's name** finds that person's files,
  in **their own tier**: after the path matches and the effect presets' descriptions, before the
  content matches (`FileSearch`, a `PersonTier` next to `DescriptionTier`, the content tiers shifted
  by one — `Rank.Tier` and the tier arithmetic moved together).
- A file already found in an earlier tier is not repeated.
- In the search view and in the folder view alike (below the open folder), as the favorites are.

---

## Privacy

- Every face, embedding and decision stays **on this machine**, in `index\files.faces` and
  `people.json` next to the exe; nothing is sent anywhere — the only network access is the models'
  download.
- The face analysis is **opt-in**: **off by default**, an app setting turned on from the ⚙ menu
  (*Face analysis*, `AppSettings.FaceAnalysis`) — and offered by the People view while off. Turning it
  on offers the face engine's download when it is missing; the pass starts once both are there.
- How the data is forgotten: *see Open Questions*.

---

## Documentation

- `RULES.md`: a new **People** section (engine, pass, face index, grouping, decisions on faces, the
  view, the search tier, privacy) and a line in § Content OCR Passes for the face pass's place.
- `GLOSSARY.md` + `GLOSSARY.fr.md`: Face, Person, People view, Face pass, Face engine, Pinned face.
- `README.md` + `README.fr.md`: the People view.

---

## Test Impact

The repository has **no test project** (`CONTRIBUTING.md`: a change is checked by hand in the
running app). Whether this work creates one for its pure logic: *see Open Questions*. The behaviours
to pin, whatever the answer:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| YuNet output decoded into boxes, landmarks and scores (priors per stride, threshold, NMS) | — (pending the Open Question) | — |
| Alignment: the similarity transform puts the 5 landmarks on SFace's template | — | — |
| Face index round-trip (version, stamps, zero-face files, embeddings) | — | — |
| Grouping: threshold, pinned faces first, exclusions honoured, a dropped face joins its group | — | — |
| Merge as a link: sub-groups one level deep, a composite merged brings its sub-groups, un-merge restores the group as it was | — | — |
| `people.json` round-trip, tolerant load | — | — |
| Person tier: rank between description and content, no duplicate file | — | — |
| Remembered view: the older folder-view flag read as the folder view | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1.

- [x] ~~**Opt-in** — is the face analysis off by default, turned on from the ⚙ menu (and offered by the People view), or on as soon as the engine is downloaded?~~ → Off by default, turned on from the ⚙ menu (Q&A 9)
- [x] ~~**Engine folder** — the models in their own `face-engine\` (ONNX Runtime's DLL still taken from `ocr-engine\`, downloaded with them if missing), or everything in `ocr-engine\`?~~ → `face-engine\` (Q&A 10)
- [x] ~~**Pass order** — the face pass right after the Windows OCR pass (People available early), or after every OCR pass?~~ → Right after the Windows OCR pass (Q&A 11)
- [x] ~~**Reaching the view** — a 👤 button next to 📁, the 📁 button cycling through three views, or a drop-down of the three views?~~ → A drop-down of the three views (Q&A 6)
- [x] ~~**Single-file persons** — shown like the others, gathered in a last *Others* tile, or not shown?~~ → Groups shown as "face folders"; the ungrouped faces listed after them as individual files (Q&A 7)
- [x] ~~**Merge gesture** — drag a person tile onto another (with a confirmation) plus *Merge with…* in the context menu, or multi-select then *Merge*?~~ → Drag a group onto another; a merged group shows as a sub-group, every file of both listed; *Un-merge* on a sub-group; a face dragged onto a group joins it (Q&A 7–8)
- [x] ~~**Ungrouped face tile** — the whole file, as any file tile, or the face cropped from it (the file's name below)? A file holding two ungrouped faces gives two tiles either way.~~ → The face cropped from its file (Q&A 15)
- [ ] **Merge confirmation** — a drop of a group onto another merges at once (un-merge being one right click away), or asks first?
- [ ] **Forgetting** — a ⚙ entry *Forget faces…* deleting `files.faces` and `people.json` after a confirmation?
- [ ] **Grouping threshold** — a fixed constant tuned during implementation, or an app setting of the ⚙ menu?
- [ ] **Test project** — create one for the pure logic (decoder, alignment, face index, grouping, `people.json`, tier), or keep the hand checks only?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-27

Initial design. The user's goal is not mere detection but **grouping people**: a People view
beside favorites and folders, naming a face, merging people detected apart (Q&A 1). Scoped as
one workfile in two phases, the YuNet + SFace engine, plus removing a face, searching by name and
hiding a person (Q&A 2–4).

From the exploration: the faces get their **own index file** (the slot model of `files.content` is
text only); the face pass reuses the extraction worker's skeleton; ONNX Runtime's managed API is
already referenced and its natives downloaded with the OCR engine; a direct download helper exists
(`Downloader.DownloadAsync`); the view switch is a bool to turn into a three-state enum; the
decisions follow `favorites.json`'s model; the search by name is one more tier. Proposed: the user's
decisions are stored **on faces** (pinned / excluded), never on groups, so a regrouping never loses a
name or a merge.

### Iteration 2 — 2026-10-27

The People view's shape, from the user's answers (Q&A 6–8):

- Reached by a **drop-down** of the three views (Favorites / Folders / People), replacing the 📁 toggle.
- **Face groups** drawn as tiles meaning "a group", listed first; the **ungrouped faces** after them,
  as individual files — the *Others* tile is dropped.
- Everything by **drag and drop**: a face onto a group adds it, a group onto another **merges** it.
- A merged group shows **inside its target as a sub-group**, and every file of both is listed too;
  **Un-merge** on a sub-group's context menu restores it. So a merge is now stored as a **link**
  (`mergedInto`), each group keeping its faces — no longer as faces re-pinned to the target.
- New questions: the ungrouped face's tile (whole file or face crop), and whether a merge drop asks
  first.

### Iteration 3 — 2026-10-27

Engine, pass and privacy settled (Q&A 9–11, 15): the face analysis is **opt-in**, off by default,
turned on from the ⚙ menu; the models go into **`face-engine\`**, ONNX Runtime's DLL staying in
`ocr-engine\` and fetched with them when missing; the face pass runs **right after the Windows OCR
pass**; an ungrouped face's tile shows **the face cropped** from its file.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code — phase 1 | | | |
| Code — phase 2 | | | |
| Unit tests | | | Pending the *Test project* question |
| RULES.md | | | |
| GLOSSARY (EN + FR) | | | |
| README (EN + FR) | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What is face detection for (framing, blurring, cropping, explorer search)? Which engine? Videos? Depth? | Dismissed; answered by a message: the goal is to **group automatically the people identified as the same person**, in a new view next to favorites / folders; a face can be given a name to find it easily; a person detected as two groups can be merged | 2026-10-27 |
| 2 | How to split the work? | One workfile, delivered in phases (1: pass + grouping + People view; 2: naming + merging) | 2026-10-27 |
| 3 | Which engine? | YuNet + SFace (ONNX, MIT / Apache 2.0), downloaded on demand | 2026-10-27 |
| 4 | What else, beyond naming and merging? | Removing a face, search by name, hiding a person (videos and GIFs left out) | 2026-10-27 |
| 5 | Is the subject straightforward or tricky / long? | Tricky / long | 2026-10-27 |
| 6 | How is the People view reached (👤 button, 📁 cycling, drop-down)? | C — a drop-down of the views | 2026-10-27 |
| 7 | Single-file persons: shown, gathered in *Others*, or not shown? | The groups show as "folders" (not the folder look, just a way to understand they gather); the other faces are listed after them as individual files; drag and drop into an existing face folder includes it in the group; a folder dragged onto another merges them; inside a folder, merged groups show as sub-folders *and* all the contents of both as files; right click on a sub-folder → un-merge | 2026-10-27 |
| 8 | Merge gesture: drag a tile onto another + *Merge with…*, or multi-select + *Merge*? | See Q&A 7 | 2026-10-27 |
| 9 | Opt-in: off by default (⚙ menu), or on once the engine is downloaded? | Off by default | 2026-10-27 |
| 10 | Engine folder: `face-engine\` or `ocr-engine\`? | `face-engine\` | 2026-10-27 |
| 11 | Pass order: right after the Windows OCR pass, or after every OCR pass? | Right after the Windows pass | 2026-10-27 |
| 12 | Forgetting: a ⚙ *Forget faces…* entry? | | 2026-10-27 |
| 13 | Grouping threshold: fixed constant or ⚙ setting? | | 2026-10-27 |
| 14 | Test project for the pure logic, or hand checks only? | | 2026-10-27 |
| 15 | Ungrouped face tile: the whole file, or the face cropped from it? | The face cropped | 2026-10-27 |
| 16 | Merge drop: at once, or asking first? | | 2026-10-27 |

---

*Last updated: 2026-10-27*
