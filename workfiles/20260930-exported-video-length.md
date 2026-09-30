# Exported Video Length

> Working document — a read-only readout of the length the exported video would have, always
> visible in the bottom bar next to Copy / Save, its detail in a tooltip.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the length of the exported video is known only once an export ends — in the status line's
summary and in the Copy last tooltip. With several videos in the grid, which one sets the length
and how many times the others start over is guesswork before the export.

The bottom bar gets a **length readout**: the length the MP4 video would have, always visible next
to the Copy / Save buttons, `—` while the export is a PNG, with the detail in a tooltip — what sets
the length, how many times every other content plays. Read-only: the rule stays the one of today,
the longest playing loop (or the soundtrack's for a grid of stills), written once and read from
one place by the export, the preview's soundtrack loop and the readout.

Relevant components:

| Component | Role |
|---|---|
| `Composition/Animation.cs` — `GridLength` (l.21) | The longest **playing** loop (a frozen content plays nothing), used by the preview's soundtrack loop |
| `Composition/Animation.cs` — `VideoLength` (l.17) | **Orphan**: no caller; counts the frozen contents too (`IsAnimated`). A trap if reused |
| `Composition/Soundtrack.cs` — `LoopIn` (l.23) | The grid's loop, or the soundtrack's own duration when nothing plays |
| `Imaging/GridExport.cs` — `Job` constructor (l.25–26), `Length` (l.44) | The export's **own** max over the captured items' loops, then `LoopIn`: the same rule, written a second time |
| `UI/AnimationPlayer.cs` — `SyncSoundtrack` (l.178) | The preview's soundtrack looped on `GridLength`; the images loop each on its own duration |
| `UI/MainForm.cs` — `_outputButtons` (l.261–267), `_bottom` (l.270–294) | The bottom bar: *Clear all*, the status line, then the right-hand cluster ⚙ · Copy · ▾ · Copy last · Save · ▾ |
| `UI/MainForm.cs` — `UpdateButtons` (l.1658) | Switches the captions PNG ↔ MP4 (`ProducesVideo`); called on every change of the images, layout, selection, soundtrack, borders, and around exports |
| `UI/MainForm.cs` — `Seconds` (l.1564), `Summary` (l.1535), `RefreshLastVideoTooltips` (l.1185) | The `12.5 s` format of the export summaries, the tooltip pattern on the shared `_toolTip` |
| `UI/MainForm.cs` — `FitStatusWidth` (l.2469) | Caps the status text by the cluster's width, re-run when `_outputButtons` resizes |

---

## The Video Length

One definition, the one the export applies today:

| Grid | Video length |
|---|---|
| At least one content **plays** (animated, not frozen) | The **longest playing loop**; the shorter ones start over until it ends |
| Nothing plays, the **soundtrack on** | The **soundtrack's duration** (an MP4 of the stills and the sound) |
| Nothing plays, no soundtrack | **None**: Copy and Save produce a PNG |
| No image | None |

- A **frozen** content is a still: it never sets the length. A Frames **starting point** shifts a
  content, it does not shorten it. The Volume and the soundtrack's level change nothing.
- **One place**: `Animation.VideoLength` becomes the shared rule —
  `VideoLength(TimeSpan gridLength, Soundtrack? soundtrack)` = `soundtrack?.LoopIn(gridLength) ?? gridLength`,
  and an overload over the images, `VideoLength(images, soundtrack)`, the former applied to
  `GridLength(images)`. `GridExport.Job` reads it over its captured items' loops, the readout over
  the preview's images. The orphan `VideoLength(images)` — the wrong rule, frozen contents
  counted — is replaced.
- The preview's playback is untouched: each content still loops on its own duration; only the
  soundtrack follows the grid's loop, as today.

---

## The Length Readout

### Placement and Look

- A **label** in the bottom bar's right-hand cluster (`_outputButtons`), **between ⚙ and Copy**:
  only the gear moves when the text widens; Copy, Copy last and Save keep their place against the
  window's right edge.
- Text: **`⏱ 12.5 s`** — the clock glyph, then the `Seconds` format of the export summaries:
  seconds with one decimal whatever the length (`75.0 s`, never minutes), so the readout and the
  summary after an export agree to the digit — and **`⏱ —`** while there is no video length.
- **Always visible**, never disabled, not locked while exporting, and **a click on it does
  nothing**: a readout, not an action. The export keeps the length it started with; the readout
  keeps following the grid.
- Vertically centred on the buttons (`Anchor = Left` in the flow panel, like the status label),
  the text **left-aligned** so the glyph stays put after the gear while the digits grow, a 6 px
  margin before Copy; a **minimum width** of `⏱ 000.0 s`, so a length under 1000 s never moves
  its neighbours.
- The decimal separator follows the **current culture** (`31,0 s` in French), as the export
  summaries' `Seconds` helper already does: the two agree to the digit.

### Tooltip

On the shared `_toolTip`, several lines, refreshed with the text:

| Grid | Tooltip |
|---|---|
| A content plays | `MP4 video of 12.5 s: the longest loop, clip-a.mp4's`, then one line per other **animated** content, in cell order: `clip-b.mp4 — 4.0 s, plays 3.1 times` · `anim.gif — 12.5 s, plays once` · `long.mp4 — frozen, a still` |
| Stills, the soundtrack on | `MP4 video of 30.0 s: the soundtrack's length, music.mp3's; the images are stills`, then the frozen contents, if any |
| Nothing plays, no soundtrack | `A PNG: no content plays and the soundtrack is off`, then the frozen contents, if any |
| No image | `No image` |

- With a soundtrack over playing contents, one more line: `Soundtrack music.mp3 — 30.0 s, cut at
  12.5 s`, or `— 5.0 s, plays 2.5 times` when shorter.
- *Plays N times* = length ÷ loop, one decimal at most (`3.1`, `2`), `once` for exactly 1.
- A content is named by its **file name**; without one, `cell N` (1-based, in layout order).

### Refresh

- Computed in `UpdateButtons()`, right where the captions switch between PNG and MP4, from the
  preview's images and the active soundtrack: the readout and the captions can never disagree.
- Every route that changes what plays reaches `UpdateButtons` (the captions depend on it): images,
  layout, the selected cell's effects (Frames' freeze goes through the options toolbar, on the
  selected cell), the soundtrack's file, level and toggle, the global Reset, *Clear all*, the
  exports' begin and end. The effects toolbar's Reset acts on the **selected cell only**
  (`MainForm.ResetEffects` → `GridPreview.SetSelectedLook`), raising `SelectedImageChanged` like
  any option: nothing to wire.

---

## Documentation

- `README.md` — *Output*: a bullet for the readout (what it shows, `—`, the tooltip); *Animated
  content → Export*: a pointer to it next to "lasts as long as the longest loop".
- `GLOSSARY.md` — *Video length* (*durée de la vidéo*) and *Length readout* (*durée affichée*).
- `RULES.md` — a short *Video Length* rule: one definition, `Animation.VideoLength`, read by the
  export, the preview's soundtrack loop and the readout; a new consumer reads it there, never
  re-derives it.

---

## Test Impact

The repository holds **no test project** (declined since `20260923-application-v1.md`, Q&A #12,
and every workfile since): nothing is created or updated. The behaviour is checked by hand in
the launched app:

| Check | Expected |
|---|---|
| Empty grid | `⏱ —`, tooltip *No image* |
| One still | `⏱ —`, tooltip *A PNG…* |
| One 12.5 s video | `⏱ 12.5 s`, the tooltip names its file |
| A 12.5 s and a 4.0 s video | `⏱ 12.5 s`; the tooltip: the 4.0 s one *plays 3.1 times* |
| Freeze the 12.5 s one (Frames) | `⏱ 4.0 s`; the frozen one listed *frozen, a still* |
| Two stills and a 30 s soundtrack on | `⏱ 30.0 s`, *the soundtrack's length* |
| The 12.5 s video and the 30 s soundtrack | `⏱ 12.5 s`; *Soundtrack … cut at 12.5 s* |
| The soundtrack off again | Back to the loops' rule |
| Copy MP4 | The status line's summary shows the same length, to the digit |
| A long status message | The text wraps; the readout and the buttons do not move |
| Clear all | `⏱ —` |

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project) | — | — |

---

## Open Questions

- [x] ~~**Placement** of the readout in the bottom bar: **A** between ⚙ and Copy (recommended) ·
  **B** at the far right, after Save's ▾ · **C** at the end of the status line, before the
  cluster · **D** inside the captions (`Copy MP4 · 12.5 s`), no readout while a PNG.~~ → **A**,
  between ⚙ and Copy.
- [x] ~~**Text form**: `⏱ 12.5 s` (recommended) · `12.5 s` · `Length: 12.5 s`.~~ → `⏱ 12.5 s`.
- [x] ~~**Long lengths**: `75.0 s`, as the export summaries write them (recommended) ·
  `1:15.0`.~~ → `75.0 s`, as the summaries, whatever the length.
- [x] ~~**Click on the readout**: nothing (recommended) · selects the cell that sets the
  length.~~ → Nothing.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-30

Initial design. The first scoping batch — rules to *choose* the length (longest, shortest, a
cell's, a value), what the shorter contents do, where the control lives — was set aside by the
user: *what is useful, all the time, is to have the length under one's eyes, near the Copy / Save
buttons*. The second batch settled it: a read-only readout, always visible, the length alone with
the detail in a tooltip, a straightforward subject. Exploration (one scout pass): the rule is
written twice (`Animation.GridLength`, `GridExport.Job`) plus an orphan `Animation.VideoLength`
that counts the frozen contents; `UpdateButtons` is the refresh point of the PNG ↔ MP4 captions;
the `Seconds` helper formats `12.5 s`; no test project exists.

### Iteration 2 — 2026-09-30

The four open questions settled, each on the recommended option, over a mockup of the four
placements: the readout goes **between ⚙ and Copy**, reads **`⏱ 12.5 s`**, keeps the summaries'
seconds format whatever the length, and **does nothing when clicked**. Design complete.

### Iteration 3 — 2026-09-30 — ✅ Implemented

Go given for the code, the unit tests (not applicable: no test project) and the documentation.
Branch: `main`, the repository's standing choice (every run lands on `main`; no worktree asked).

### Iteration 4 — 2026-09-30 — 🧭 Implementation choices

Delivered as designed, with these choices the design left open:

- **The preview's soundtrack loop reads the shared rule**: `AnimationPlayer.SyncSoundtrack` calls
  `Animation.VideoLength(_images, soundtrack)` in place of `soundtrack.LoopIn(GridLength(...))` —
  the same value, from the one place.
- **Nothing wired for the effects toolbar's Reset**: it acts on the selected cell only and raises
  `SelectedImageChanged`, so `UpdateButtons` — and the readout — already follow it.
- **Text left-aligned**, the clock glyph anchored after the gear, a 6 px margin before Copy.
- **Plays N times**: the ratio rounded to one decimal *before* the *once* test (`plays once` from
  0.95 to 1.04), then written with at most one decimal — `plays 2 times`, `plays 3.1 times`.
- **Decimal separator of the current culture** (`31,0 s` in French), as the summaries: not
  forced to a dot, so the readout and the status line's summary keep agreeing.
- **The check instance became the delivery instance**: two videos had been dropped into it and
  its window hidden to the tray before the checks ended; it was kept rather than replaced by a
  fresh launch (⚠️ *Launch After Delivery*, `mini-apps/CLAUDE.md`), the build being the delivered
  one.
- Self-check on the launched app: `⏱ 31,0 s` shown between ⚙ and *Copy MP4* with two videos in
  the grid; build clean, 0 warning.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-09-30 | `928c4c1` the shared rule `Animation.VideoLength`; `987def9` the length readout |
| Unit tests | — | — | Not applicable: no test project |
| README | 3 | 2026-09-30 | `83553f0`, with `GLOSSARY.md` (*Video length*, *Length readout*) and `RULES.md` (§ Video Length) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which rules to choose the length, what the shorter contents do, where the control lives, depth? | Dismissed — redirected: *the length under one's eyes, all the time, near Copy / Save* | 2026-09-30 |
| 2 | Near Copy / Save: a readout of the length, a setting, or both? | A read-only readout | 2026-09-30 |
| 3 | When is it visible? | Always; `—` while there is no video | 2026-09-30 |
| 4 | What does it show? | The length alone, the detail in a tooltip | 2026-09-30 |
| 5 | Straightforward or tricky / long subject? | Straightforward | 2026-09-30 |
| 6 | Placement in the bottom bar (A / B / C / D)? | A — between ⚙ and Copy | 2026-09-30 |
| 7 | Text form (glyph, bare, the word Length)? | `⏱ 12.5 s` | 2026-09-30 |
| 8 | Long lengths: seconds as the summaries, or minutes? | Seconds, as the summaries (`75.0 s`) | 2026-09-30 |
| 9 | Click on the readout: nothing, or select the cell that sets the length? | Nothing | 2026-09-30 |

---

*Last updated: 2026-09-30*
