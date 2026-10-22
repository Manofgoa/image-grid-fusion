# Explorer Transparency Checkerboard

> Working document — the file explorer's thumbnails drawn over white and grey squares instead of a
> flat grey, so a transparent image (or folder icon) shows its transparency.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The file explorer paints every thumbnail box with a flat grey (`SystemColors.ControlLight`) before
drawing the thumbnail. A PNG with an alpha channel, a folder icon from Windows or the drawn folder
glyph then sit on grey, and nothing tells a transparent area from a grey one.

The flat grey becomes a **checkerboard** — white and grey squares, the usual sign of transparency —
over the **whole 4:3 box**, finer than the preview's, its squares scaled to the box.

Components:

| Component | Where the grey / flat fill is today |
|---|---|
| `UI/ThumbnailGrid.PaintPicture` | The tile's box (Thumbnails view) and the Details row's thumbnail — `SystemColors.ControlLight` |
| `UI/ThumbnailGrid.PaintStrip` | Each thumbnail of a folder row's strip (Details view) — `SystemColors.ControlLight` |
| `UI/ThumbnailGrid.PaintLoading` | The Loading… slot — `SystemColors.ControlLight` |
| `UI/UrlZone.UrlPreview.OnPaint` | The URL zone's preview — `g.Clear(SystemColors.Window)` |

Already right, nothing to change: `Imaging/ShellThumbnail` copies Windows' thumbnails **keeping their
alpha channel**, and `ThumbnailCache.Fit` / `UrlZone.Fit` draw into 32-bit ARGB bitmaps — the
transparency reaches the paint, it is only hidden by the grey under it.

---

## Where the Checkerboard Goes

Every thumbnail box of the file explorer:

| Box | Checkerboard |
|---|---|
| A file's tile (Thumbnails view) | ✅ whole 4:3 box, bands around the image included |
| A folder's tile — Windows' thumbnail **or** the drawn folder glyph | ✅ whole box, behind the folder |
| A Details row's thumbnail (file or folder) | ✅ whole box |
| A folder row's strip thumbnails (Details view) | ✅ each strip box |
| The Loading… slot (both views) | ✅ whole slot, "Loading…" text over it |
| The URL zone's preview | ✅ whole preview — see Open Questions for its empty / failed states |
| A tile whose thumbnail is not loaded yet, or has none | ✅ the box shows the checkerboard alone |

The borders, the heart and bulb medallions, the selection and hover outlines, the bulb's arrow, the
spinner's veil and the play badge are drawn over it as today.

---

## The Pattern

- **Colors**: white and the preview's grey, `(204, 204, 204)` (`GridPreview.CheckerGrey`) — one
  definition for both.
- **Finer, scaled to the box**: the square's side follows the box it fills, so a tile shows about the
  same number of squares whatever the tile size — proposed: **the box's height / 16, at least 3 device
  px** (a 75 px high tile → ~5 px squares, a 750 px one → ~47 px). *(See Open Questions.)*
- **Anchored on the box's top-left corner**: each box starts with a white square, so the pattern does
  not slide with the scroll and every tile looks the same.
- **One helper**, shared by the explorer and the URL zone: a new `UI/Checkerboard.cs` —
  `Checkerboard.Fill(Graphics g, Rectangle box)` computing the square from the box and filling it,
  its `TextureBrush` cached per square size (a handful of sizes in practice: tiles, Details
  thumbnails, strips, the Loading… slot, the URL preview), disposed with its tile bitmap.
- The preview's own checkerboard (`GridPreview.PaintCheckerboard`, 8 logical px, anchored on the
  canvas, cut by the Twitter corners) is **not changed** — only its grey constant is shared.

---

## Test Impact

No unit test project exists in the repository (`src/` holds the app only), and the change is pure
painting: **no test created or updated**. Checked by hand on a transparent PNG, a folder with a
Windows thumbnail, a folder without one, the Details view, the Loading… slot and the URL zone.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; painting only) | — | — |

---

## Open Questions

- [ ] Square size: the box's height / 16 with a 3 px minimum (proportional, ~12 rows of squares in
  every box), or another factor / a fixed small size?
- [ ] URL zone preview with no image — empty (🔗) and failed (⚠) states: checkerboard too, or the
  plain window background (no image, so no transparency to show)?
- [ ] Documentation: add the checkerboard to the glossary's *Tile* row (EN + FR), or leave the docs
  untouched (a visual detail)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-22

Scope set with the user (Q&A 1–4): the checkerboard replaces the grey in the tiles, the Details view
(row thumbnails and folder strips), the URL zone preview and the Loading… slot; it covers the whole
4:3 box, bands included; its squares are finer than the preview's, scaled to the box. The subject is
straightforward: one scout pass, done directly.

The exploration found the four fills to replace (`PaintPicture`, `PaintStrip`, `PaintLoading`,
`UrlPreview.OnPaint`) and that the thumbnails already keep their alpha (`ShellThumbnail`). Proposed:
one shared `Checkerboard` helper, white + the preview's grey, square = box height / 16 (min 3 px),
anchored per box.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project, painting only |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the checkerboard replace the grey? | Tiles (Thumbnails view), Details view, URL zone preview, Loading… slot — all four | 2026-10-22 |
| 2 | What surface of the tile does it cover — the image's rectangle only, or the whole 4:3 box? | The whole 4:3 box | 2026-10-22 |
| 3 | Which pattern — the preview's, or finer, scaled to the tile? | Finer, scaled to the tile | 2026-10-22 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-22 |
| 5 | Square size factor? | | |
| 6 | URL preview's empty / failed states: checkerboard or plain? | | |
| 7 | Documentation: glossary *Tile* row updated? | | |

---

*Last updated: 2026-10-22*
