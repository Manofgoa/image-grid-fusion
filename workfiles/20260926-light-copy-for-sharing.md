# Light Copy for Sharing

> Working document — a second copy command putting a light JPEG on the clipboard, pastable in
> chat apps that cap image size (WhatsApp: 16 MB).
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Pasting a grid copied from the app (**Copy PNG**, `Ctrl+C`) into WhatsApp Web fails with
*"1 image que vous avez essayé d'ajouter dépasse la taille limite qui est de 16 Mo"*: the copy
holds the **full-resolution** result, as a bitmap and in the `PNG` clipboard format, and the
browser pastes it as a lossless PNG heavier than 16 MB.

The normal copy **stays as it is** (full quality for Paint, Word, image editors). A **separate
light copy** is added: a JPEG whose long edge is capped at **2560 px**, light enough for any chat
app — WhatsApp recompresses to about 1600 px anyway.

Relevant components: `UI/MainForm.cs` — `Copy`, `CopyToClipboard`, `RenderStillAsync`,
`StillSummary`, `TempExportPath` / `CleanTempVideos`, `UpdateButtons`, the Copy split button
(`_copyButton`, `_copyArrow`, `_copyMenu`), `ProcessCmdKey`.

---

## Current Behaviour

- **Copy** (`Ctrl+C`) with nothing playing: `RenderStillAsync()` renders the grid at full size;
  the clipboard gets `SetImage(bitmap)` + `"PNG"` (a PNG stream).
- While a content plays: an MP4 file in `%TEMP%\ImageGridFusion`, put on the clipboard as a file
  drop list.
- The **▾ arrow** of Copy opens a menu **GIF** / **MP4 Video**; it is **disabled while nothing
  plays** (`_copyArrow.Enabled = any && HasAnimation`), so a still grid has no menu today.
- The status line reports `Copied to the clipboard · PNG · W × H · size · 1 frame · 0 s · encoded
  in …` (`StillSummary`, format hard-coded to `PNG`).

---

## Light Copy

### Output

- **Format**: JPEG.
- **Size**: the rendered still, **downscaled** so that its long edge is at most **2560 px**,
  aspect ratio kept, high-quality resampling. A grid already within 2560 px is **not upscaled**.
- **Quality**: JPEG quality **90** (visually lossless for illustrations at this size; a
  2560 × 1340 grid lands around 1–2 MB).
- **Content**: the same still as the normal copy — `RenderStillAsync()`, so every effect shows
  (`Compositor.DrawCell`), and animated content gives the page each image shows.
- **Transparency**: JPEG carries no alpha, so the still is **flattened on white**
  (`Compositor.Flattened`) — a cell whose Background effect is off comes out white, as RULES.md's
  *Background Exception* requires for outputs without alpha. The downscaled bitmap put on the
  clipboard is the same flattened image.

### Clipboard Content

- The **JPEG file**, written to `%TEMP%\ImageGridFusion`, as a **file drop list** — what WhatsApp,
  Explorer and mail clients paste as is, like the MP4 copy.
- The **downscaled bitmap** (`SetImage`), for apps that paste bitmaps only (Paint, Word). Even if a
  browser converts it to PNG, at 2560 px it stays well under 16 MB.

### Access

- A **"JPEG for sharing"** item in the **Copy ▾ menu**, after GIF and MP4 Video.
- The Copy ▾ arrow becomes **enabled whenever the grid holds an image** (and no export runs);
  **GIF** and **MP4 Video** items are **disabled while nothing plays**, as the whole arrow was.
- **No keyboard shortcut**: `Ctrl+C` stays the full copy.
- **Save is unchanged**: its ▾ menu gets no JPEG item, and its arrow keeps its current rule.

### Status Line

`Copied for sharing · JPEG · W × H · size · 1 frame · 0 s · encoded in …` — `StillSummary`
takes the format name instead of hard-coding `PNG`.

### Temp Files

The JPEG goes to `%TEMP%\ImageGridFusion` like the MP4 and GIF copies (`fusion-{timestamp}.jpg`),
and `CleanTempVideos` also removes `*.jpg` at the next start.

### README

- *Output* (Copy button bullet) and *Animated content* (▾ arrow bullet): the ▾ menu of Copy gains
  **JPEG for sharing**, enabled even when nothing plays; GIF / MP4 stay disabled while nothing plays.
- The feature list line on Copy mentions the light JPEG for chat apps capping image size.

---

## Test Impact

The app has **no test project**: nothing testable is pinned by unit tests. The change is
verified by hand (paste into WhatsApp Web, Paint, Explorer).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project in the app) | — | — |

---

## Open Questions

- [x] ~~Where does the copy come from?~~ → The app's own copy (`Ctrl+C` / Copy button).
- [x] ~~Which approach?~~ → A separate light copy; the normal copy is unchanged.
- [x] ~~What is acceptable for the light version?~~ → JPEG, long edge capped at 2560 px.
- [x] ~~Where is the light copy reached: an item of the Copy ▾ menu (the arrow then always enabled,
      GIF / MP4 greyed while nothing plays), a separate button, or both?~~ → An item of the Copy ▾
      menu.
- [x] ~~Does it get a keyboard shortcut (`Ctrl+Shift+C`)?~~ → No shortcut.
- [x] ~~What goes on the clipboard: the JPEG as a file only, or the file plus the downscaled bitmap
      (for apps that paste bitmaps only — Paint, Word)?~~ → The JPEG file plus the downscaled bitmap.
- [x] ~~Does Save get the same option (a JPEG in the Save ▾ menu), or does the scope stay on Copy?~~
      → Copy only.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

- Diagnosis: the full-resolution PNG the browser pastes exceeds WhatsApp's 16 MB cap.
- The clipboard holds one content in several formats, and the target app picks the format it
  knows — it cannot pick "the lightest" among several sizes. Offering a light JPEG next to the
  full image was considered and set aside by the user in favour of a **separate light copy**.
- Light copy: JPEG, quality 90, long edge ≤ 2560 px, never upscaled, same rendered still as the
  normal copy; status line naming JPEG; temp file cleaned at the next start.
- Access, shortcut, clipboard formats and Save counterpart left open.

### Iteration 2 — 2026-09-26

Open questions answered:
- Access: a **"JPEG for sharing"** item in the Copy ▾ menu; the arrow is enabled whenever the grid
  holds an image, GIF / MP4 greyed while nothing plays.
- No keyboard shortcut.
- Clipboard: the JPEG **file** (file drop list) **plus the downscaled bitmap**.
- Save unchanged — the scope stays on Copy.
- README changes listed.

### Iteration 3 — 2026-09-26

- New rule (RULES.md, *Background Exception*, commit 77775e7): a cell whose Background effect is
  off is transparent, drawn white in the outputs without alpha. The normal copy now flattens its
  bitmap on white (commit e1ebfb7).
- The light copy follows it: the JPEG and its downscaled bitmap are **flattened on white**
  (`Compositor.Flattened`) before encoding.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — the app has no test project |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How was the image pasted into WhatsApp copied? | From the app (Copy / `Ctrl+C`) | 2026-09-26 |
| 2 | Which approach: several formats, a separate light copy, automatic reduction? | A separate light copy | 2026-09-26 |
| 3 | What is acceptable for the light version? | JPEG, max 2560 px | 2026-09-26 |
| 4 | Is the subject straightforward or tricky? | Straightforward — a single scout pass | 2026-09-26 |
| 5 | Where is the light copy reached (Copy ▾ menu, separate button, both)? | Copy ▾ menu | 2026-09-26 |
| 6 | Keyboard shortcut `Ctrl+Shift+C`? | No shortcut | 2026-09-26 |
| 7 | Clipboard content: JPEG file only, or file + downscaled bitmap? | JPEG file + downscaled bitmap | 2026-09-26 |
| 8 | Same option in Save? | No, Copy only | 2026-09-26 |
| 9 | Go for implementation? | No — the gate holds | 2026-09-26 |

---

*Last updated: 2026-09-26*
