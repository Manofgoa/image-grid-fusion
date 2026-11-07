# Status Bar File Size

> Working document — an always-visible readout next to the bottom bar's Copy / Save buttons: the format they
> produce, the file's size (exact when known, else a worst-case estimate marked `≈`) and its dimensions.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

User request (2026-11-07, French): *« En bas à côté des boutons d'enregistrement, afficher toujours taille du
fichier (si connu ou prévisible), dimensions. Taille du fichier estimée au pire (avec le égal vague), mp4/png selon
le type de contenu de la surface. »*

Today the bottom bar (`MainForm._bottom`, a 3-column `TableLayoutPanel`) holds *Clear all*, the status line
(`_statusLine`: `_status`, the open-folder link, Cancel) and the output buttons (`_outputButtons`: Copy ▾,
Copy last, Save ▾). What Copy / Save produce shows only in their caption (`Copy PNG` / `Copy MP4`) and, for a PNG,
in their tooltip (`PNG 2400 × 1256`, `RefreshPngSizeTooltips`, refreshed on mouse enter). No size is shown anywhere
before a Copy.

The feature adds an **output readout** (*indicateur de sortie*): what the main part of Copy / Save would produce,
the grid as it stands — e.g. `MP4 · ≈ 4,2 Mo · 1920 × 1004`.

Relevant code:

| Piece | Where |
|---|---|
| Bottom bar layout, status width fitting | `MainForm` ctor (`_bottom`, l. ~590), `MainForm.FitStatusWidth` |
| What the buttons produce | `MainForm.ProducesVideo` (`HasAnimation \|\| ActiveSoundtrack is not null`) → `"MP4"` / `"PNG"` in `UpdateButtons` |
| PNG dimensions | `MainForm.PngSize(CanvasSizer.MaxLongSide)` — native size, layers-only and background-only grids included |
| MP4 dimensions | `Animation.EvenSize(PngSize(AppSettings.VideoMaxSide))` (`RefreshVideoSizeTooltips`) |
| Video length | `Animation.VideoLength(...)` as `RefreshLength` computes it |
| H.264 bitrate | `VideoEncoder.AddVideoStream`: `clamp(W × H × 30 × 0.12, 2 Mb/s, 40 Mb/s)` |
| AAC bitrate | `VideoEncoder.AacBytesPerSecond` = 24 000 B/s |
| Last video and its exact size | `MainForm._lastVideo` (`LastVideo.Bytes`, `.Format`, `.GridVersion`) |
| "Grid unchanged since" | `GridPreview.ContentVersion` — documented as images, layout, cell sizes, looks, borders, soundtrack, fade |
| Size units (`Ko` / `KB`) | `Explorer/FileSizes` (`Strings.FileSizeUnits`, base 1024) |

---

## The Readout

### Placement and Content

- A **label in the bottom bar, just left of Copy** — inside `_outputButtons`, between the ⚙ button and Copy
  (`MainForm.SetUpOutputReadout`, `UI/MainForm.OutputReadout.cs`): it reads as the caption of the buttons after it;
  the status line keeps the rest of the width, `FitStatusWidth` already subtracting `_outputButtons`' width.
- **Text**: `<format> · <size> · <width> × <height>` — `PNG · ≈ 24,7 Mo · 2400 × 1256`,
  `MP4 · 4,18 Mo · 1920 × 1004` (exact, no `≈`).
- **Format**: the one the main part of Copy / Save produces — `MP4` while `ProducesVideo`, else `PNG` — so the
  readout never contradicts the buttons' caption. The ▾ entries (GIF, MP4 forced, JPEG for sharing, PNG max
  sizes) are not covered: their own captions / tooltips already give their size.
- **Size** in the UI language's units through `FileSizes` (`Ko` / `KB`…), `≈ ` before an estimate.
- **Dimensions**: the PNG's native size, or the MP4's even size clamped to the Video size.
- **Always shown**, whatever the status line says: with an empty grid (`!HasContent`) it reads `—`, as the length
  readout's `⏱ —`, so nothing in the bar moves when the grid empties or fills; its minimum width — the widest
  figure in megabytes, `MP4 · ≈ 999 MB · 8888 × 8888` — keeps it steady, its text right-aligned against Copy.

### During an Export

- While an export runs (`BeginExport` → `EndExport`), the readout shows **that export's progress**: its format
  (the one actually exported — a GIF or a forced MP4 from the ▾ menus included), the **bytes written so far**, its
  dimensions — `MP4 · 1,6 Mo… · 1920 × 1004`. The bytes are the length of the file being written, read on the
  readout's timer (`BeginWriting` / `EndWriting` around `ExportAnimationAsync`), never per frame.
- When it ends, the produced size is shown exact, without `≈`, if it is the main part's format (it becomes the
  known size, § Exact Size); otherwise — a GIF, a PNG max size, a JPEG for sharing — the readout goes back to the
  main part's figure. A cancelled or failed export goes back to it too.
- A still export (PNG) is synchronous and short: no progress, its exact size at the end.
- **Tooltip**: how the figure was obtained — the estimate's basis (*worst case: uncompressed PNG, the file is
  usually much smaller* / *8,4 Mb/s × 12,5 s + sound*), or *size of the file the last Copy / Save produced*.

### Estimate — Worst Case

One static, testable computation (`Composition/OutputEstimate`), never an encode:

| Format | Upper bound |
|---|---|
| PNG | The image stored uncompressed: `H × (1 + W × 4)` bytes (32 bpp, one filter byte per row), plus deflate's stored-block overhead (5 bytes per 65 535), 12 bytes per IDAT chunk of at least 8 KB, and 1 KB of fixed parts. Real PNGs are smaller, often much smaller |
| MP4 | `(videoBitrate / 8 + 24 000 B/s with sound) × length × 1.03 + 64 KB` (the container's share); `videoBitrate` the encoder's **own** formula, moved into `OutputEstimate.VideoBitrate(size)` with `OutputEstimate.AacBytesPerSecond`, both read by `VideoEncoder`; the AAC term while the soundtrack is on or a content — a cell's, a layer's, the background image's — is heard |

### Exact Size — When Known

- After a **Copy or Save of the main part** (PNG at native size, or MP4), the produced file's size is kept with
  the grid's `ContentVersion` at that time; while `ContentVersion` is unchanged **and** the format the buttons
  produce is the same, the readout shows that size, **without `≈`**.
- One record for both formats, `KnownOutput` (format, bytes, dimensions, `ContentVersion`), set by the Copy and Save
  paths of the main part (`MainForm.KeepOutputSize`, `KeepSavedSize`): an MP4, or a PNG at its native size. A GIF,
  a PNG max size entry and the JPEG for sharing leave it alone.
- It is shown while the **format, `ContentVersion` and the dimensions** all still match. `ContentVersion` already
  counts the range, the layers and the Grid background; the dimensions catch what it does not — a Custom format's
  exact pixels changed at the same ratio, the Video size setting.

### Refresh

- The readout follows **every change of the rendered result**, not only `UpdateButtons`' calls: a zoom, a crop or
  a separator changes the native size without touching the buttons (which is why the PNG tooltip refreshes on mouse
  enter today). A 250 ms timer (`_outputTimer`) recomputes it when `ContentVersion` or the Video size differ from
  what it was computed for; `UpdateButtons` recomputes it too (format, content, export start and end).

---

## Documentation

- **GLOSSARY.md / .fr.md**: a new term, *Output readout* (*indicateur de sortie*).
- **RULES.md**: a short § *Output Readout* under the export rules — one definition of the estimate
  (`OutputEstimate`), the bitrate shared with the encoder, the exact size tied to `ContentVersion`, a new export
  input bumping `ContentVersion`.
- **README.md / .fr.md**: one line where the Copy / Save buttons are described.
- **Strings.resx / Strings.fr.resx**: the tooltip texts, the readout's format string.
- No shortcut: `SHORTCUTS.md` untouched.

---

## Test Impact

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| PNG upper bound: `H × (1 + 4W)` plus stored-block and chunk overhead, never below the raw pixel bytes | `tests/ImageGridFusion.Tests/OutputEstimateTests.cs` | Create |
| MP4 upper bound: bitrate × length, the AAC term only with sound, the container margin; zero length → no estimate | `tests/ImageGridFusion.Tests/OutputEstimateTests.cs` | Create |
| The shared bitrate formula: clamped to 2 Mb/s and 40 Mb/s, the encoder's values unchanged | `tests/ImageGridFusion.Tests/OutputEstimateTests.cs` | Create |

The readout's label, its refresh and the exact-size swap are UI wiring in `MainForm`: checked by hand at the
delivery launch.

---

## Open Questions

- [x] ~~With an empty grid, is the readout hidden, or shown as `—`?~~ → Shown as `—`
- [x] ~~Placement: just left of Copy (between the status line and the buttons), or right of Save, at the far right?~~ → Just left of Copy
- [x] ~~During an export: keep the readout as it stood, or show the export's progress there instead?~~ → The export's progress: bytes written so far, then the exact size

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-07

Initial design from the user's request and the scoping batch (Q1–Q4): the format follows the main part of Copy /
Save (PNG / MP4); the size is a worst-case formula marked `≈`, exact without `≈` after a Copy / Save while the grid
is unchanged; the readout shows format, size and dimensions. Exploration (one pass, the questions chained around
`MainForm.UpdateButtons`) found the bitrate formula in `VideoEncoder`, the last video's exact size and
`GridPreview.ContentVersion`, whose documented coverage misses a few export inputs.

### Iteration 2 — 2026-11-07

Open questions answered (Q5–Q7): the readout reads `—` on an empty grid; it sits just left of Copy; during an export
it shows that export's progress — its format, the bytes written so far, its dimensions — then the exact size, or
goes back to the main part's figure when the export was another format, cancelled or failed. New § During an Export.

### Iteration 3 — 2026-11-07 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/status-bar-file-size`, branch
`feature/status-bar-file-size`). The scope is the design sections as they stand at this entry.

### Iteration 4 — 2026-11-07 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **The bitrates live in `Composition/OutputEstimate`** (`VideoBitrate`, `AacBytesPerSecond`, the clamp bounds), not
  in a `VideoEncoder.BitrateFor`: the encoder reads them from there, so `Composition` never depends on `Imaging`.
- **`ContentVersion` left as it is**: it already counts the range, the layers and the Grid background; the exact size
  is also checked against the **dimensions**, which catch a Custom format's exact pixels at the same ratio and the
  Video size — no new bump needed.
- **One `KnownOutput` for both formats**, set by Copy and by Save, rather than reading `_lastVideo` (Copy's only).
- **Placement**: inside `_outputButtons`, between ⚙ and Copy; right-aligned with a fixed minimum width (the widest
  megabyte figure), so `—` leaves blank room rather than moving the buttons.
- **Refresh by a 250 ms timer** comparing `ContentVersion` and the Video size, rather than a new event on the preview.
- **Export progress** read from the file's length on that timer, not from the sink writer's statistics; its dimensions
  the even, Video-size-clamped canvas for a GIF as for an MP4, as their tooltips already say.
- **Margins**: MP4 container 3 % + 64 KB; PNG IDAT chunks assumed at least 8 KB, 1 KB of fixed parts.
- **Sound** counted while the soundtrack is on or any content is heard, a sound file the mixer would fail to read
  included — an upper bound.
- **Commits**: the readout and its export progress landed in one commit, both in `UI/MainForm.OutputReadout.cs`.
- **Not exercised by the agent**: the exact size after a Copy / Save and the export's bytes — a Copy would overwrite
  the user's clipboard. Checked: the estimate `PNG · ≈ 20 MB · 3200 × 1675` for two 1600 × 1000 stills.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-11-07 | `Composition/OutputEstimate`, `UI/MainForm.OutputReadout.cs`, `VideoEncoder` reads the shared bitrates, texts EN/FR |
| Unit tests | 3 | 2026-11-07 | `OutputEstimateTests`, 7 tests, green |
| README | 3 | 2026-11-07 | README.md / README.fr.md, a sub-bullet of Export |
| GLOSSARY / RULES | 3 | 2026-11-07 | *Output readout* / *Indicateur de sortie*; RULES.md § Output Readout |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which format does the estimated size follow? | The one the Copy button produces (PNG for a still grid, MP4 for an animated one) | 2026-11-07 |
| 2 | How is the file size obtained? | Worst-case formula with `≈`; exact, without `≈`, after a Copy / Save while the grid is unchanged | 2026-11-07 |
| 3 | What does the readout show? | Format + size + dimensions | 2026-11-07 |
| 4 | Simple or tricky to explore? | Simple — one scout pass | 2026-11-07 |
| 5 | Empty grid: readout hidden or `—`? | Shown as `—` | 2026-11-07 |
| 6 | Placement: left of Copy or right of Save? | Just left of Copy | 2026-11-07 |
| 7 | During an export: readout kept, or progress shown there? | Progress shown: bytes written so far, then the exact size | 2026-11-07 |

---

*Last updated: 2026-11-07*
