# PNG Export at Native Resolution

> Working document — size the PNG export so that no image is downscaled, its zoom included, and
> let Copy / Save force a maximum size from a list (720p, 1080p, 2K, 4K…).
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The PNG export should be **as large as needed to keep every image at its original resolution** —
a text in a screenshot must not be shrunk — whatever the image's zoom: the other cells grow with the
canvas so the most demanding image is drawn at least 1:1.

`CanvasSizer.Compute` already picks "the canvas at which no image is downscaled", but it reads each
image at zoom 1 — no Zoom effect (`ImageLook.ZoomIn`), no fine angle, no Borders gap — and clamps the
longer side to [1200, 4096]. This workfile:

- makes the sizing **zoom-aware** (and fine-angle / Borders aware) for the PNG;
- raises the PNG's **cap to 8192 px**;
- adds, **in addition to the existing menu items** of the Copy and Save split buttons, a **submenu
  forcing a maximum size** for the PNG — 720p, 1080p, 2K, 4K….

Out of scope (user, 2026-10-15): the MP4 / GIF exports keep their **Video size** cap; the submenu is
for the PNG only.

Relevant components: `Composition/CanvasSizer.cs`, `Composition/FitCalculator.cs`
(`Scale`, `DrawnSize`, `ComputeTurned`), `Composition/Compositor.cs` (`Render`, `Cells`),
`Imaging/GridExport.cs` (`Job.Capture`, `RenderStill`), `UI/MainForm.cs` (`_copyMenu`, `_saveMenu`,
`RenderStillAsync`, `UpdateButtons`), `UI/GridPreview.cs` (`ExportOffsets`, which mirrors the PNG
canvas), `UI/AppSettings.cs` (`VideoMaxSides`).

---

## Current Sizing (as found, 2026-10-15)

- `CanvasSizer.Compute(images, layout, ratio, maxLongSide)` (`CanvasSizer.cs:16-37`): for each cell,
  its fraction of a canvas 1 px wide, `FitCalculator.Scale(cell, imageSize)`, and `1 / scale` is the
  canvas width drawing that image 1:1; the widest wins, then the longer side is clamped to
  `[MinLongSide 1200, min(maxLongSide, MaxLongSide 4096)]`.
- Its image sizes are `Frame.SizingSize` / `SourceImage.SizingSize`: `ImageLook.Shown` (crop, quarter
  turn, flips) **divided by `Sharpening`**, so a text or a PDF page rendered sharper never widens the
  canvas.
- **Not counted**: the zoom (`ImageLook.ZoomIn` — a zoom-in shows fewer source pixels per cell pixel,
  a zoom-out more), the fine angle's cover scale (`FitCalculator.ComputeTurned(...).Scale`), the
  Borders' gap shrinking the cells (`Compositor.Cells(layout, canvas, borders)`), the Animations'
  motion zoom.
- Callers: the PNG (`Compositor.Render`, from `MainForm.RenderStillAsync` and
  `GridExport.RenderStill`), the video (`GridExport.cs:217`, with `Job.VideoMaxSide`), the position
  readout (`GridPreview.cs:2453`, the PNG's canvas).
- `AppSettings.VideoMaxSides` is `[1920, 2560, CanvasSizer.MaxLongSide]`: the video choices **read the
  PNG cap** today.
- Loaded images keep their full resolution; only rendered pages are capped
  (`PageSource.MaxSharpSide` = 4096).

---

## Native Sizing Rule

For each cell holding an image with a fixed resolution, the **drawn scale** of one source pixel on a
canvas `W` px wide:

```
drawnScale(W) = FitCalculator.Scale(cell(W), shown) × look.ZoomIn(cell(W), shown) × turned.Scale
```

- `cell(W)` from `Compositor.Cells(layout, canvas, borders)` — the Borders' gap included;
- `shown` = `ImageLook.Shown` (the crop's kept part, turned and flipped) — the kept part stands for the
  whole image, so no crop factor;
- `ZoomIn` = the Zoom effect's free zoom or fit mode; for an Animations motion, the zoom **at time 0**,
  the starting state the PNG shows (`ImageLook.ZoomAt(0, …)`);
- `turned.Scale` = the fine angle's cover scale (`FitCalculator.ComputeTurned`).

The canvas is the **smallest one where every such image has `drawnScale ≥ 1`**, then clamped to
`[1200, cap]`. `drawnScale` is linear in `W` but for the Borders' rounding and their 1 px minimum: it is
computed on a reference canvas (`CanvasSizer.Smallest(ratio)`), the needed width derived, then checked
again at the result (one or two passes absorb the borders).

- A **zoomed-in** image needs a smaller canvas (each source pixel already drawn bigger); a **zoomed-out**
  one a larger canvas.
- An image larger than the cap allows is still downscaled — the cap wins.
- Contents without a fixed resolution — **TBD (Open Question 6)**.

### Cap

- The **PNG cap** becomes **8192 px** on the longer side (a 8192 × 4287 Twitter canvas is ~140 MB of
  ARGB; `OutOfMemoryException` is already caught on every Copy / Save path — RULES.md § Export Memory).
- The **video** keeps its own list: `AppSettings.VideoMaxSides` stops reading the PNG cap and keeps
  4096 as its own constant, so the ⚙ menu's Video size choices do not change.
- *JPEG for sharing* — **TBD (Open Question 5)**.

---

## Copy / Save Submenu

The Copy and Save split buttons already have a drop-down (`MainForm.cs:662-682`): Copy — *GIF*, *MP4*,
*JPEG for sharing*, *Copy last MP4 / GIF*; Save — *GIF*, *MP4*, *Save last…*. **Every existing item
stays**; a submenu is **added** to each menu, for the PNG only:

```
Copy ▾                          Save ▾
  GIF                             GIF
  MP4                             MP4
  JPEG for sharing                PNG max size ▸  720p — 1280 × 670
  PNG max size ▸  720p — …        ─────────────   1080p — 1920 × 1005
  ─────────────   1080p — …       Save last…      2K — 2560 × 1340
  Copy last…      2K — …                          4K — 3840 × 2010
                  4K — …                          8K — 7680 × 4019
                  8K — …
```

- The main button (*Copy PNG* / *Save PNG*) keeps producing the **native** size (§ Native Sizing Rule).
- An entry is an **action**: it Copies / Saves the PNG **right away** at that limit, like the GIF and
  MP4 items. Nothing is remembered; the main button stays native.
- Its name limits the canvas's **longer side**, whatever the output format:

  | Entry | Longer side |
  |---|---|
  | 720p | 1280 |
  | 1080p | 1920 |
  | 2K | 2560 |
  | 4K | 3840 |
  | 8K | 7680 |

- It is a **cap**: the PNG is `min(native, entry)` — never enlarged past the native need. An entry at
  or above the native size gives the native size.
- Each entry **shows the size it will give**, `1080p — 1920 × 1005`, computed when the menu opens (the
  menus' `Opening` already refreshes the last video's tooltips): entries above the native size show the
  same native size.
- Wiring: `RenderStillAsync` passes the chosen cap into `GridExport.Job.Capture` / `RenderStill` (a
  still cap distinct from `videoMaxSide`), down to `CanvasSizer.Compute`.

---

## Position Readout

`GridPreview.ExportOffsets` gives the offsets "in the pixels of the PNG export of the grid as it stands"
(RULES.md § Position Readout): it calls the same sizing as the PNG, with its default (native) cap, so
its values follow the new canvas by themselves. A zoom changing the native canvas then changes the
readout of every image — a consequence to accept, see Open Question 8.

---

## Test Impact

The app has **no test project** (CONTRIBUTING.md: "check a change by hand in the running app"): nothing
testable is created or updated. The behaviours below are checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A zoomed-out image widens the PNG; a zoomed-in one no longer does | — (manual check) | — |
| The PNG's longer side reaches 8192 px, never more | — (manual check) | — |
| The Video size choices of the ⚙ menu are unchanged (1920 / 2560 / 4096) | — (manual check) | — |
| A submenu entry gives a PNG within its limit; the existing menu items still work | — (manual check) | — |

---

## Open Questions

- [x] ~~1. A submenu entry (e.g. *1080p*): an **action** — Copy / Save the PNG at that limit right away,
  like the GIF and MP4 items — or a **remembered choice** the main button then uses?~~ → An action;
  nothing remembered, the main button stays native
- [x] ~~2. What does *720p / 1080p / 2K / 4K* limit: the canvas's **longer side** (1280 / 1920 / 2560 /
  3840), its **height** (720 / 1080 / 1440 / 2160), or its **shorter side**?~~ → The longer side
- [x] ~~3. When the native size is **smaller** than the entry: keep the native size (a cap only), or
  **upscale** to exactly the entry's size?~~ → Keep the native size: a cap only
- [x] ~~4. The list: 720p, 1080p, 2K (1440p), 4K (2160p) — and 8K? Each entry showing the size it will
  give (`1080p — 1920 × 1005`)?~~ → 720p, 1080p, 2K, 4K and 8K (7680), each showing the size it gives
- [ ] 5. *JPEG for sharing* (Copy menu): keeps the 4096 cap, or follows the PNG's 8192?
- [ ] 6. Texts, PDF pages (rendered sharper, `Sharpening`): kept as today — they never widen the
  canvas — or counted at their rendered resolution?
- [ ] 7. MP4 / GIF: keep today's zoom-blind sizing exactly, or use the same zoom-aware rule under their
  Video size cap (one sizing rule, a zoomed-in video then exported smaller)?
- [ ] 8. Show the native size anywhere — the Copy / Save buttons' tooltip (`PNG 6144 × 3215`)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-15

Initial design from the user's request and the scoping batch (Q&A 1–4) plus its clarification (Q&A 5):
PNG only, zoom-aware native sizing (zoom, fine angle, Borders gap, motion at time 0), the PNG cap raised
to 8192 with the video list decoupled from it, and a *PNG max size* submenu added to the Copy and Save
menus, every existing item kept. Exploration: two read-only scouts (canvas sizing; Copy / Save menus).

### Iteration 2 — 2026-10-15

Open Questions 1–4 answered (Q&A 6): a submenu entry is an immediate Copy / Save, the main button
staying native; its name caps the longer side (1280 / 1920 / 2560 / 3840 / 7680, 8K added); a cap
only, never an upscale; each entry shows the size it gives. § Copy / Save Submenu updated.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — does not apply |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which exports aim at the images' original resolution? | PNG only — the videos keep their Video size cap | 2026-10-15 |
| 2 | Which cap for the PNG's longer side (4096 today)? | Raised to 8192 px | 2026-10-15 |
| 3 | Does the sizing replace the current behaviour or become an option? | In Copy / Save, a submenu to force a maximum size from a useful list — 720p, 1080p, 2K, 4K… | 2026-10-15 |
| 4 | Is the subject simple or tricky / long? | No preference — a single scout pass taken | 2026-10-15 |
| 5 | (User clarification) | The submenu comes **in addition to** the current menu items, and for the PNG only | 2026-10-15 |
| 6 | Open Questions 1–4 (submenu behaviour, what the names limit, cap or exact size, list) | An immediate action; the longer side; a cap, the native size kept below it; 720p to 8K, each entry showing the size it gives | 2026-10-15 |
| 7 | Open Questions 5–8 (JPEG for sharing, texts / PDF, videos, size shown) | | |

---

*Last updated: 2026-10-15*
