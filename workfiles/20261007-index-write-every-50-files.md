# Content Index Written Every 50 Files

> Working document — while the content texts are extracted, write the content index every 50
> files processed.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer's **content search** (the 💡 criterion) reads the texts of the indexed files —
OCR of images and PDFs, text of text and HTML files — from `ContentIndex`, cached on disk in
`files.content` in the `index\` folder next to the exe. A background **extraction pass**
(`FileExplorerPanel.Extract`) fills it one file at a time.

The request: during that pass, **write the content index every 50 files processed**, with two
goals (Q&A #1):

- **Nothing lost**: the app closed or crashing mid-pass keeps what was already extracted, and the
  next pass resumes from there.
- **Progressive results**: the content already extracted is searchable as the pass goes, without
  waiting for its end.

Scope: the **content extraction only** — the scan of the file list (`FileIndex`) keeps its own
saving as is (Q&A #2). The 50 is a **constant** in the code, not an app setting (Q&A #3).

---

## Current Behaviour

What the code does today (`src/ImageGridFusion/UI/FileExplorerPanel.cs`):

| What | When | Where |
|---|---|---|
| `files.content` written (`TrySave` → `ContentIndex.Save`) | Every **30 s** (`ContentSaveInterval = 30000`), and once at the end of the pass — finished, cancelled or failed (`finally`) | `Extract`, lines ~1009–1014 and 1023–1029 |
| The search shown refreshed (`RefreshContentResults`) | Every **5 s** (`ContentRefreshInterval = 5000`), and at the end | `Extract` → `ExtractionProgress.Refresh`, `ExtractAsync` |
| The search reads | `ContentIndex` **in memory** (a `ConcurrentDictionary`), never the file | `ContentIndex.FoldedOf` |

- `ContentIndex.Save` rewrites the **whole file** (temp file then move, so a crash keeps the
  previous one), one save at a time.
- A file extracted counts even when it has no text (kept with an empty one).
- The pass resumes where it stopped because it only queues the files whose text is missing or
  stale (`ContentIndex.IsCurrent`).

So the *progressive results* goal is already met in memory (refresh every 5 s); the *nothing lost*
goal depends on the save cadence, today time-based.

---

## Design

- Two constants in `FileExplorerPanel`, next to the other timings: **`ContentSaveEvery = 50`**
  (files) and **`ContentSaveFloor = 5000`** (ms).
- In `Extract`, a counter of the files processed since the last save. After each file,
  `files.content` is written (`TrySave`) when **either**:
  - **50 files** were processed since the last save **and at least 5 s** passed since it
    (Q&A #6) — the floor keeps a run of fast text files from rewriting the whole file several times
    a second; or
  - **30 s** passed since the last save, whatever the count (`ContentSaveInterval`, kept — Q&A #5),
    so slow OCR never goes minutes without a save.
- Every save, whichever triggered it, starts the counter over and the 30 s clock again. A count
  reaching 50 inside the floor is not lost: the save comes with the first file once the 5 s have
  passed.
- *Processed* = every file the pass went through, **whatever its result** — text found, no text,
  extraction failed — i.e. each `done++`.
- The save at the end of the pass stays.
- The search shown keeps its **5 s refresh** from memory (Q&A #7): it already sees the texts as
  they arrive, independently of the saves.
- The `Extract` doc comment ("saved every half minute and at the end") is updated to the new
  cadence.

---

## Test Impact

The repository has **no test project** (`src/` holds only `ImageGridFusion`). The cadence lives in
a private worker method that extracts real files; creating a test project for it is outside this
scope. Nothing to create or update — checked by hand: an indexing pass, the app closed mid-pass,
the next launch resuming after the last multiple of 50.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~What is the goal of the cadence?~~ → Both: nothing lost, and progressive results (Q&A #1)
- [x] ~~Which indexing is concerned?~~ → The content extraction only (Q&A #2)
- [x] ~~Constant or setting?~~ → A constant (Q&A #3)
- [x] ~~The current 30 s save timer: replaced by the 50 files, or kept alongside?~~ → Kept
      alongside: whichever comes first (Q&A #5)
- [x] ~~A floor between two saves for fast files, or strictly every 50?~~ → At least 5 s between
      two saves triggered by the 50 files (Q&A #6)
- [x] ~~The 5 s refresh of the search shown: kept, or tied to the 50 files?~~ → Kept as is
      (Q&A #7)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Scoping answered (Q&A #1–#4): both goals, content extraction only, a constant, straightforward.
Single scout pass, done directly: the extraction already saves every 30 s and at the end, and the
search already reads the memory refreshed every 5 s. Proposed: a 50-file counter in `Extract`
triggering `TrySave`; its relation with the 30 s timer, a floor between saves and the refresh
cadence left open.

### Iteration 2 — 2026-10-07

Open questions answered (Q&A #5–#7): the 30 s timer is kept alongside the 50-file trigger,
whichever comes first; the 50-file trigger waits for at least 5 s since the last save; the 5 s
search refresh is unchanged. § Design updated; no open question left.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given: code and documentation, in a worktree (`.claude/worktrees/index-write-every-50-files`,
branch `feature/index-write-every-50-files`). Scope frozen on § Design as of Iteration 2.

### Iteration 4 — 2026-10-07 — 🧭 Implementation choices

No divergent choice: § Design implemented as written — `ContentSaveEvery` and `ContentSaveFloor`
next to `ContentSaveInterval`, an `unsaved` counter in `Extract` reset by every save, the
`Extract` doc comment updated. No rule broken.

- **Documentation**: nothing to update. The README describes the content search (⚙ *Search file
  contents (OCR)*, the 💡 criterion) but not the extraction pass's save cadence; RULES.md and the
  glossary don't mention it either. Adding a description of the pass to the README is outside the
  frozen scope.
- `workfiles/20260926-ocr-search.md` still says "saved every 30 s and at the end": left as is, a
  workfile being the history of its own task.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-07 | `FileExplorerPanel.Extract`: saved every 50 files, 5 s floor, 30 s kept |
| Unit tests | 3 | 2026-10-07 | Not applicable — no test project in the repository |
| README | 3 | 2026-10-07 | Not applicable — the save cadence is not documented there; RULES.md and the glossary don't mention it either |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Main goal of writing the index every 50 files? | Both: nothing lost on a close or crash, and the content already indexed searchable as the pass goes | 2026-10-07 |
| 2 | Which indexing is concerned? | The content only; the scan of the names stays as is | 2026-10-07 |
| 3 | The 50: a constant or a ⚙ setting? | A constant | 2026-10-07 |
| 4 | Straightforward or tricky / long to explore? | Straightforward | 2026-10-07 |
| 5 | The 30 s save timer: replaced by the 50 files, or kept alongside? | Both, whichever comes first | 2026-10-07 |
| 6 | A floor between two saves for fast files, or strictly every 50? | At least 5 s | 2026-10-07 |
| 7 | The 5 s refresh of the search: kept, or tied to the 50 files? | Kept at 5 s | 2026-10-07 |

---

*Last updated: 2026-10-07*
