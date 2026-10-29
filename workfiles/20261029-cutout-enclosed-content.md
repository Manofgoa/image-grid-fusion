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
- The rest of the Cutout is unchanged: treatments of the part left out, Grow, Soften, Invert, the
  reach, presets, undo, layers.

### What the Example Shows

A flood fill of the example from its edges (white, 94 % of the border pixels; scratch simulation,
tolerance 16 to 64 on the largest channel difference) removes 65 % of the image and keeps the face,
the neck, the right arm, the hand and the controller. The **left arm is lost**: its two lines do not
meet at the bottom, so the background flows in through the gap. Fill holes on IS-Net's mask has the
same limit — an outline open on the background encloses nothing (§ Open Questions).

---

## Effect State

- `CutoutMode` gets a third value, **`Plain`**, after `Picked`.
- `CutoutEffect` gets two settings:
  - **FillHoles**: `bool`, off by default — Subject and Pick only; a Plain mask has no hole.
  - **Tolerance**: from 0 to 1, how far a pixel's color may be from the background color and still be
    background — the largest channel difference, up to `CutoutEffect.MaxTolerance` (128 of 255);
    **0.25** by default (32). Plain only, kept in the other modes.
- `WithPlain()` (mode `Plain`, the points cleared), `WithFillHoles(bool)`, `WithTolerance(double)`.
- **`MaskKey`**: `plain:<tolerance>` in Plain mode — the mask depends on it; Fill holes is **not** in
  the key: it is applied to the mask kept, so checking it never runs the model again.
- `Equals` / `GetHashCode` compare the two new settings.
- **Default state**: unchanged otherwise — off, mode Subject, Fill holes off, Tolerance 0.25.

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
- **Invert** then applies to the filled mask: inverted, the holes are left out with the background.

### Plain Mode

Computed by `CutoutService` off the UI thread like the others, **without the engine** (§ Open
Questions), at the **bitmap's resolution** — a drawing's lines stay crisp — the mask cached on the
image like the models' (`SourceImage.KeepMask`, its key `plain:<tolerance>`):

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
- Placement: § Open Questions. Acting on either turns the effect on (RULES.md § Options Toolbar).
- `MainForm`'s sync of the options (`cutout.Invert` → `_cutoutInvert`) sets the two new controls and
  their enabled state.

### Engine

- **Plain needs no engine**: `CutoutService.Request` computes a Plain mask whether the engine is
  there or not; Subject and Pick still need it (§ Open Questions).
- The **download offer** is not made for Plain: the Plain thumbnail turns the effect on directly. The
  activation checkbox keeps turning the effect on in its kept mode — Subject by default — so the
  offer still comes then when the engine is missing.
- `SourceImage.CutsOut` unchanged: still images only, Plain included.

---

## Presets and History

- **Presets**: `CutoutEffect.ToJson` / `FromJson` get `fillHoles` and `tolerance` (a missing value
  giving its default) and the mode `Plain`; `Describe` lists `plain background, tolerance 25 %` as the
  mode in Plain, and `holes filled` among the options when Fill holes is on.
- **Undo history**: covered by itself — the settings are on `ImageLook`.

---

## Documentation

- RULES.md § The Cutout Exception: the Plain mode — a mask computed without a model, still off the
  UI thread by `CutoutService`, kept like the others; Fill holes applied first in `CutoutMask.Apply`.
  § The engine is never shipped: Plain needs none.
- GLOSSARY (EN + FR, one commit): the *Cutout* row — Plain, Fill holes, Tolerance.
- README (EN + FR, one commit): § Cutout — the Plain mode, Fill holes, the Tolerance.

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
- [ ] **Gaps in the outline** — the example's left arm, open at the bottom, is lost by both: add a
  *Close gaps* setting (an outline closed over a gap of a few pixels before the fill), or leave it?
- [ ] **Plain without the engine** — does the Plain mode work without the cutout engine downloaded?
- [ ] **Placement** of the Plain thumbnail, the Fill holes checkbox and the Tolerance slider in the
  options.
- [ ] **The background color** — taken automatically from the edges only, or also picked with the
  eyedropper?

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
| 5 | Gaps in the outline: a *Close gaps* setting, or leave it? | | |
| 6 | Does the Plain mode work without the cutout engine? | | |
| 7 | Where do Plain, Fill holes and Tolerance go in the options? | | |
| 8 | The background color: automatic from the edges, or the eyedropper too? | | |

---

*Last updated: 2026-10-29*
