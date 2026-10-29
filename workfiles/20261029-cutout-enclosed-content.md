# Cutout: Keep the Enclosed Content

> Working document — the Cutout keeps what its outline encloses: a *Fill holes* option on the
> model's mask, and a *Plain* mode removing a plain background from the image's edges inward.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The Cutout effect (`workfiles/20261029-cutout.md`) removes **content inside the image** when that
content has the background's color. The case reported: a drawing on a white background
(`X:\Dropbox\Outils RS\Outils RS Wojak\Homme soja jeux-videos 3.jpg`, a Wojak) — the white
background removed, as wanted, but the **white face**, neck and arms too, since IS-Net cannot tell
the white inside the outline from the white around it.

Agreed scope (Q&A #1–#3):

- **Both approaches**, side by side:
  - **Fill holes** — an edge option of the mask, in Subject and Pick modes: every part the mask
    leaves out that is **enclosed by the part kept** — not connected to the image's border — is kept.
  - **Plain** — a third mode next to Subject and Pick: no model; the **plain background** is removed
    from the image's edges inward — the pixels close to the background color, connected to a border —
    and **everything it encloses is kept**, whatever its color. A **Tolerance** slider sets how close.
- A part of the real background **enclosed by the drawing** (between an arm, the shirt and a bottle)
  is kept too: **acceptable**, nothing done about it (Q&A #2).
- **Fill holes is off by default**: the Cutout behaves as today until it is checked (Q&A #3).
- **Close gaps**: a setting closing the outline over a gap of a few pixels before either fill, so a
  part whose outline is open on the background is kept too (Q&A #5).
- **Plain needs no engine**: it works without the cutout engine downloaded (Q&A #6).
- The rest of the Cutout is unchanged: treatments of the part left out, Grow, Soften, Invert, the
  reach, presets, undo, layers.

### What the Example Shows

A flood fill of the example from its edges (white, 94 % of the border pixels; scratch simulation,
tolerance 16 to 64 on the largest channel difference) removes 65 % of the image and keeps the face,
the neck, the right arm, the hand and the controller. The **left arm is lost**: its two lines do not
meet at the bottom, so the background flows in through the gap. Fill holes on IS-Net's mask has the
same limit — an outline open on the background encloses nothing: hence **Close gaps** (§ The Masks).
Simulated with it, the left arm is kept from **0.5 %** of the longer side on (r = 4 px), the
background between the arm and the shirt still removed; up to 2 %, 2 points less of the image removed.

---

## Effect State

- `CutoutMode` gets a third value, **`Plain`**, after `Picked`.
- `CutoutEffect` gets two settings:
  - **FillHoles**: `bool`, off by default — Subject and Pick only; a Plain mask has no hole.
  - **Tolerance**: from 0 to 1, how far a pixel's color may be from the background color and still be
    background — the largest channel difference, up to `CutoutEffect.MaxTolerance` (128 of 255);
    **0.25** by default (32). Plain only, kept in the other modes.
  - **CloseGaps**: from 0 to 1, the widest gap of the outline closed before the fill — up to
    `CutoutEffect.MaxGap`, **2 %** of the image's longer side (28 px on the example); **0** by default.
    Plain, and Fill holes in the other modes.
- `WithPlain()` (mode `Plain`, the points cleared), `WithFillHoles(bool)`, `WithTolerance(double)`,
  `WithCloseGaps(double)`.
- **`MaskKey`**: `plain:<tolerance>:<closeGaps>` in Plain mode — the mask depends on both; Fill holes
  and its Close gaps are **not** in the key of the other modes: they are applied to the mask kept, so
  changing them never runs the model again.
- `Equals` / `GetHashCode` compare the three new settings.
- **Default state**: unchanged otherwise — off, mode Subject, Fill holes off, Tolerance 0.25, Close
  gaps 0.

---

## The Masks

### Fill Holes

Applied in `CutoutMask.Apply`, **first**, before Grow, Soften and Invert, on the mask at the
**model's resolution** (before it is stretched over the bitmap), so a bitmap rendered at another size
looks the same and the pass stays cheap (1024 × 1024 at most):

- A pixel is **left out** below 128. The left-out pixels connected to the mask's border — 4-connected,
  a flood fill from every left-out border pixel — are the background; every **other left-out pixel is
  set to 255**: a hole.
- A hole's soft edge goes with it: the pixels between the hole and the part kept, below 128, are part
  of the hole.
- With **Close gaps** above 0, the flood runs through the gaps closed (§ Close Gaps).
- **Invert** then applies to the filled mask: inverted, the holes are left out with the background.

### Plain Mode

Computed by `CutoutService` off the UI thread like the others, **without the engine** (Q&A #6), at
the **bitmap's resolution** — a drawing's lines stay crisp — the mask cached on the image like the
models' (`SourceImage.KeepMask`, its key `plain:<tolerance>:<closeGaps>`):

- **The background color**: the most frequent color of the image's border pixels (its four outer
  rows and columns), each channel rounded to 8 levels for the count, the exact mean of the winning
  bucket taken.
- **The background**: the pixels whose largest channel difference from it is at most the tolerance,
  **connected to a border** through such pixels (4-connected flood fill from every border pixel
  within the tolerance). Opacity 0.
- **The antialiased edge**: a kept pixel touching the background gets an opacity from its distance
  — 0 at the tolerance, 255 at twice the tolerance (32 levels at least) — so the light fringe of a
  drawing's line fades instead of showing as a halo.
- Everything else is kept, 255: what the background encloses included.
- A transparent pixel (a PNG's alpha 0) is background whatever its color.
- With **Close gaps** above 0, the flood runs through the gaps closed (§ Close Gaps).

### Close Gaps

One computation for both fills — a *close gap* flood, as paint programs' bucket fills do:

- The **barrier** — the part kept for Fill holes (mask ≥ 128), the pixels out of the tolerance for
  Plain — is **grown by half the gap** (`r`, the same linear-time square filter as Grow,
  `CutoutMask.Extreme`): a gap of up to `2r` pixels is closed.
- The flood from the border runs over what the grown barrier leaves; the region it reaches is then
  **grown back by `r`**, kept to the pixels that are background-like (left out for Fill holes, within
  the tolerance for Plain): the background comes back up to the lines, and only what the closed
  outline encloses is kept.
- `r` is computed on the image the fill runs on — the mask's resolution for Fill holes, the bitmap's
  for Plain — from the share of its longer side, so it looks the same at every size.
- A gap in the outline wider than the setting still lets the background in, as without it.

---

## UI

### Options

- **Mode strip** (`CutoutModeStrip`): **Subject**, **Pick**, **Plain** — its pictogram the cell's
  grey box, the image white, a blue blob in the middle drawn as an outline over the white (the
  content kept, the white around it gone); tooltip *Removes the plain background around the content —
  the color of the image's edges, from the edges inward — keeping everything it encloses; no engine
  needed*. Clicked: mode Plain, the points cleared, Pick disarmed. Turns the effect on.
- **Reach** strip: disabled in Plain mode, as in Subject mode.
- **Fill holes**: a plain checkbox (RULES.md § Options Toolbar: plain on / off checkboxes are out of
  the thumbnails), tooltip *Keeps what the cutout's outline encloses: the parts of the image the model
  left out but that the part kept surrounds*. Disabled in Plain mode, its tooltip then saying the
  Plain mode keeps them already.
- **Tolerance**: a slider and its label (`Tolerance: 25 %`), 0 to 100 %, tooltip *How close to the
  edges' color a pixel must be to be removed*. Disabled outside Plain mode, its value kept.
- **Close gaps**: a slider and its label (`Gaps: 0.8 %`, a share of the longer side like Grow), 0 to
  2 %, tooltip *Closes the outline over gaps up to this width, so what it almost encloses is kept
  too*. Enabled in Plain mode, and in the other modes while Fill holes is checked.
- **Placement** (Q&A #7, layout B): a **column right after the mode strip**, before the reach strip —
  its first line **Tolerance**, its second **Close gaps** then **Fill holes** — so the controls sit
  next to the mode they belong to and the options toolbar keeps its two lines.
- Acting on any of them turns the effect on (RULES.md § Options Toolbar).
- `MainForm`'s sync of the options (`cutout.Invert` → `_cutoutInvert`) sets the three new controls
  and their enabled state.

### Engine

- **Plain needs no engine** (Q&A #6): `CutoutService.Request` computes a Plain mask whether the
  engine is there or not, never creating a `CutoutEngine` for it; Subject and Pick still need it.
  Every other check of `CutoutEngineFiles.IsAvailable` on the Cutout's route — the exports' wait
  (`MainForm.WaitForCutoutsAsync`), the *Waiting for the cutout engine* badge — lets a Plain cutout
  through the same way.
- The **download offer** is not made for Plain: the Plain thumbnail turns the effect on directly. The
  activation checkbox keeps turning the effect on in its kept mode — Subject by default — so the
  offer still comes then when the engine is missing.
- **The background color is automatic** (Q&A #8): no eyedropper on the Plain mode.
- `SourceImage.CutsOut` unchanged: still images only, Plain included.

---

## Presets and History

- **Presets**: `CutoutEffect.ToJson` / `FromJson` get `fillHoles`, `tolerance` and `closeGaps` (a
  missing value giving its default) and the mode `Plain`; `Describe` lists `plain background,
  tolerance 25 %` as the mode in Plain, `holes filled` among the options when Fill holes is on, and
  `gaps 0.8 %` when Close gaps applies and is above 0.
- **Undo history**: covered by itself — the settings are on `ImageLook`.

---

## Documentation

- RULES.md § The Cutout Exception: the Plain mode — a mask computed without a model, still off the
  UI thread by `CutoutService`, kept like the others; Fill holes applied first in `CutoutMask.Apply`.
  § The engine is never shipped: Plain needs none.
- GLOSSARY (EN + FR, one commit): the *Cutout* row — Plain, Fill holes, Tolerance, Close gaps.
- README (EN + FR, one commit): § Cutout — the Plain mode, Fill holes, the Tolerance, Close gaps; the
  engine paragraph saying Plain needs none.

---

## Test Impact

None: the solution has no test project, and the Cutout workfile settled on no unit tests
(`workfiles/20261029-cutout.md`, Q&A #11). The flood fills are checked by hand on the example image.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

- [x] ~~Which approach keeps the white inside the outline?~~ → Both: Fill holes on the mask, and a
  Plain mode (Q&A #1)
- [x] ~~What of a real part of the background enclosed by the drawing?~~ → Kept too, acceptable (Q&A #2)
- [x] ~~Fill holes on by default?~~ → Off (Q&A #3)
- [x] ~~Gaps in the outline: a *Close gaps* setting, or leave it?~~ → A Close gaps slider, 0 by
  default, for Plain and Fill holes (Q&A #5)
- [x] ~~Does the Plain mode work without the cutout engine downloaded?~~ → Yes, no engine needed (Q&A #6)
- [x] ~~Placement of Plain, Fill holes and Tolerance?~~ → Layout B: a column right after the mode
  strip (Q&A #7)
- [x] ~~The background color: automatic, or the eyedropper too?~~ → Automatic from the edges (Q&A #8)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Scope settled by the first batch (Q&A #1–#4): both approaches, enclosed background kept, Fill holes
off by default, a simple subject. One exploration pass, directly (one area, chained questions:
`CutoutEffect`, `CutoutMask.Apply`, `CutoutService`, `CutoutModeStrip`, `MainForm`'s Cutout options),
plus a scratch simulation of the flood fill on the example: it keeps the face and loses the left arm
through a gap of its outline. First design: Fill holes on the model's mask at its resolution, before
the edge options, out of the mask key; a Plain mode computed at the bitmap's resolution, the
dominant border color, a tolerance, a soft edge, no engine needed. Four questions open: gaps,
the engine, placement, the background color.

### Iteration 2 — 2026-10-29

Second batch (Q&A #5–#8), three placements drawn as a mockup first: a **Close gaps** slider (0 to
2 % of the longer side, 0 by default) closing the outline before both fills — the barrier grown by
half the gap, the flood, the region reached grown back; the Plain mode works **without the engine**;
the new controls in a **column after the mode strip** (Tolerance, then Close gaps and Fill holes); the
background color **automatic**. No question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | None planned: no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which approach keeps the white inside the outline (face, neck, arms)? | Both: Fill holes on the mask, and a plain-background mode | 2026-10-29 |
| 2 | A real part of the background enclosed by the drawing would be filled too: what of it? | Acceptable | 2026-10-29 |
| 3 | Is the option on by default? | Off by default | 2026-10-29 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-29 |
| 5 | Gaps in the outline: a *Close gaps* setting, or leave it? | Yes, a Close gaps setting | 2026-10-29 |
| 6 | Does the Plain mode work without the cutout engine? | Yes, without the engine | 2026-10-29 |
| 7 | Where do Plain, Fill holes and Tolerance go in the options? | B — a column after the mode strip | 2026-10-29 |
| 8 | The background color: automatic from the edges, or the eyedropper too? | Automatic from the edges | 2026-10-29 |

---

*Last updated: 2026-10-29*
