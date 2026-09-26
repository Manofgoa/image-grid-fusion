# Copy Last Generated Video

> Working document — keeping the last MP4 or GIF that Copy generated, so it can be put back on the
> clipboard or saved without generating it again, and drawing attention when a long export ends while
> the user is elsewhere.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A video or GIF copy can take long. Meanwhile the user goes elsewhere, copies and pastes other
things, and the generated file — put on the clipboard as a file when the export ends — is
overwritten by the next copy before it is pasted: the whole generation is lost.

The app keeps the **last video** Copy generated in the session and offers it back:

- a **Copy last MP4** / **Copy last GIF** button in the bottom bar, and the same action at the end
  of Copy's ▾ menu, putting the kept file back on the clipboard without generating anything;
- a **Save last MP4…** / **Save last GIF…** entry at the end of Save's ▾ menu, saving that file
  where the user chooses;
- labels naming the format, tooltips saying when it was generated and whether the grid has changed
  since;
- an export that ends while the app is not in the foreground draws attention: the taskbar button
  flashes, or a notification comes from the tray icon when the window is hidden.

Stills (PNG, JPEG for sharing) are generated in an instant: they are not kept.

Components: `UI/MainForm.cs` (the bottom bar, `Copy`, `SaveAs`, the status line, the export lock),
`UI/GridPreview.cs` (a content version for the "grid has changed" hint),
`UI/TrayApplicationContext.cs` (the tray icon's notification), `README.md`, `GLOSSARY.md`.

---

## The Last Video

### What Is Kept

- The **last video** is the file of the last **successful animated Copy** — MP4 or GIF, from the
  main Copy button when it produces a video, or from the ▾ menu's *GIF* / *MP4 Video*. Kept with
  it: its path in `%TEMP%\ImageGridFusion`, its format, the time it was generated, its export result
  (size, frames, length, sounds), its encoding time, and the grid's content version at that moment
  (see § The Grid Has Changed Since). A small record, `LastVideo`, held by `MainForm`.
- It is set as soon as the export succeeds, **before** the clipboard is written: a copy that fails
  at the clipboard step (`ExternalException`) still leaves the file available, and *Copy last* is
  the retry.
- It is **not** an effect, cell or global: it is app state, like the status line. The effects
  tables of RULES.md do not apply to it — nothing that happens to the grid touches it.

### What Replaces or Forgets It

| Event | The last video |
|---|---|
| A later animated Copy succeeds (MP4 or GIF) | Replaced by the new one |
| A PNG copy, a JPEG for sharing, any Save | Untouched |
| An animated export cancelled or failed | Untouched — the previous one stays |
| The grid changes: image added, replaced, removed, swapped, layout, separators, effects, global effects, Clear all | Untouched — even emptied, the grid does not matter: the point is to paste what was generated |
| The app quits | Forgotten; the file is removed by the next start's cleanup of the temp folder, as today |
| Its file is gone from the temp folder when *Copy last* or *Save last* is used (deleted by hand, a temp cleaner) | Forgotten then: the status line says so in red, the actions go back to their disabled state |

- Not persisted: a new session starts with no last video, like today.
- A later export never overwrites the kept file: `TempExportPath` names the file by the second
  (`fusion-yyyyMMdd-HHmmss`), so a second export started within the same second would reuse the
  name — and a cancelled one deletes its file. The name gets a `-2`, `-3`… suffix while it exists.
  (This also protects the file the clipboard holds, today already.)

---

## The Actions

### Copy Last MP4 / GIF

- **Where**: a **button in the bottom bar**, right after Copy's ▾ arrow, in the output buttons' flow
  (`_outputButtons`: ⚙, Copy, ▾, **Copy last…**, Save…, ▾), with the usual 3 px margins; and the
  **last entry of Copy's ▾ menu**, after a separator.
- **Label**: **Copy last MP4** or **Copy last GIF**, naming the kept file's format; **Copy last
  video**, disabled, while none was generated in the session — always visible, so the bar never
  reflows and the feature shows. The menu entry adds the time it was generated: *Copy last MP4
  (14:32)*.
- **Enabled** while a last video exists and no export runs — with an **empty grid too**. Copy's ▾
  arrow, disabled today with no image, is enabled as soon as a last video exists; its *GIF* / *MP4
  Video* entries keep their rule (a content plays), *JPEG for sharing* gets the explicit rule it
  lacks (images, no export running).
- **Does**: puts the kept file back on the clipboard exactly as the original copy did — the file
  drop list, plus the bytes in the clipboard's GIF format for a GIF. No generation, no export lock,
  no progress: it is instant. The status line confirms: *Copied again fusion-….mp4 (generated at
  14:32) · MP4 video · 1920 × 1080 · …*, the same summary as the original copy.
- **Tooltip** — the button through the form's `ToolTip`, the menu entry through `ToolTipText` with
  the menu's item tooltips enabled: *Generated at 14:32 · MP4 video · 1920 × 1080 · 3.2 MB ·
  360 frames · 12.0 s · sound: a.mp4 · encoded in 45.3 s*, then *— the grid has changed since* when
  it has. Refreshed when the mouse enters the button and when the menu opens, so the hint reflects
  the grid at that moment. (WinForms shows no tooltip on a disabled control: none while there is no
  last video.)

### Save Last MP4… / GIF…

- **Where**: the **last entry of Save's ▾ menu**, after a separator: **Save last MP4…** / **Save
  last GIF…**, *Save last video…* disabled while none exists. No button: the menu is enough for the
  rarer action.
- **Enabled** like *Copy last*; Save's ▾ arrow, disabled today unless a content plays, is enabled as
  soon as a last video exists, its *GIF* / *MP4 Video* entries getting the arrow's former rule
  (images, a content playing, no export running) — they become fields for it.
- **Does**: a save dialog, filtered on the kept format, proposing the kept file's name
  (`fusion-….mp4`) in the default save folder (the first file-backed image's folder, else Pictures,
  as Save does); then the file is **copied** there — no generation. The dialog asks before
  overwriting, as it does. Status line: *Saved name (the last generated MP4, from 14:32) · MP4 video
  · …*. Errors: *Save failed: …* in red.
- The same tooltip as *Copy last*.

---

## The Grid Has Changed Since

- `GridPreview` gets a **content version**: an integer, `ContentVersion`, incremented by every
  change of the rendered result — `OnImagesChanged` (add, replace, remove, swap, clear),
  `SetLayout`, the separator release, `SetLook` (every effect, selected cell or not; it already
  returns on an equal look), the `Borders` setter (it already returns on an equal value) and the
  `Soundtrack` setter (which gets the same guard). Six sites in one file; a missed one only costs the
  hint.
- The last video records the version when it is generated (the grid is locked during the export,
  so the version at its end is the version at its start).
- The hint *— the grid has changed since* shows in the tooltips while the current version differs.
  A selection change is not a content change: clicking another cell shows no hint.

---

## Attention When the Export Ends

- Applies to the **animated exports** — MP4 or GIF, Copy or Save — that end in **success or
  failure** while the app is **not in the foreground** (`Form.ActiveForm` is null: none of the
  app's windows is active). A cancel is the user's own action: nothing. A still export is instant:
  nothing. The app quitting (a close deferred to the end of the export): nothing.
- **Window shown** — behind another app, or minimized: the **taskbar button flashes** until the
  window comes to the foreground (`FlashWindowEx`, `FLASHW_TRAY | FLASHW_TIMERNOFG`, declared
  inline in `MainForm` as the app's other P/Invokes are in their classes). The status line holds the
  message, as today.
- **Window hidden** in the tray (closed with ×): a **notification from the tray icon**
  (`NotifyIcon.ShowBalloonTip`, a toast on Windows 10 / 11). Title: *MP4 copied* / *GIF copied* /
  *MP4 saved* / *GIF saved* / *Video export failed* / *GIF export failed*. Text: the file name, then
  for a copy *If the clipboard gets overwritten, Copy last MP4 brings it back*, for a failure the
  error. Clicking it opens the window. `MainForm` raises an `ExportEnded(title, text, error)`
  event; `TrayApplicationContext`, which owns the icon, shows the balloon and wires
  `BalloonTipClicked` to `ShowForm()`.
- **App in the foreground**: as today, the status line only.

---

## Documentation

- `README.md`: *Output* — the *Copy last* button and menu entry, *Save last*, the tooltips, what is
  kept and until when; *Animated content* › export — the attention when an export ends away from
  the app; the unique temp names.
- `GLOSSARY.md`: **Last video** (*dernière vidéo*).
- `RULES.md`: nothing — the last video is app state, outside the effects rules.

---

## Test Impact

**None.** The repository has no test project, and the user chose to keep it test-free
(`20260926-cell-resize.md`, Q&A #13), as every previous workfile did. Verification is manual, in the
launched app:

| Behaviour to check | How |
|---|---|
| The last video is kept by an animated Copy, replaced by the next one | Copy MP4, then Copy GIF: the button reads *Copy last GIF* |
| Untouched by a PNG copy, a JPEG for sharing, a Save, a cancel | Do each after a Copy MP4: the button still reads *Copy last MP4* |
| Copy last puts the file back | Copy MP4, copy a text elsewhere, Copy last MP4, paste in Explorer: the video |
| A GIF comes back with its GIF format | Copy GIF, overwrite the clipboard, Copy last GIF, paste in an app taking GIF bytes |
| Works with an emptied grid | Copy MP4, Clear all, Copy last MP4 |
| Menu entries and arrows | Empty grid: Copy's ▾ opens, only *Copy last* enabled; Save's ▾ likewise |
| Save last | Save last MP4… to a folder: the same file, the status line says so |
| Tooltip and the hint | Hover: the summary; zoom an image: *the grid has changed since*; click another cell: no hint |
| File gone | Delete the temp file, Copy last: red status, button back to *Copy last video* |
| Same-second names | Two quick GIF copies of a short GIF: two files |
| Attention, window shown | Copy MP4, switch to another app: the taskbar flashes at the end |
| Attention, window hidden | Copy MP4, close the window: a notification at the end; click it: the window |
| Attention, foreground | Stay in the app: nothing but the status line |

---

## Open Questions

None left by the scoping batch: the intent — both placements, MP4 and GIF, kept until the next
generation or the exit, the notification, *Save last*, the labels with format and time — is settled.
The choices the design took on its own — the button's place after Copy's ▾, *Copy last video*
disabled rather than hidden, the attention covering Save and failures, flash when shown /
notification when hidden, the unique temp names — are recorded above and open to amendment before
the go.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-26

Initial design, from the scoping batch (Q&A #1–5) and one scout pass over the code:

- the last video is app state on `MainForm`, set by a successful animated Copy before the clipboard
  step, replaced only by the next one, forgotten at exit — the temp folder's cleanup unchanged;
- *Copy last MP4 / GIF*: a button after Copy's ▾ and the last entry of Copy's menu, enabled with an
  empty grid too, the arrows' rules widened; *Save last…* the last entry of Save's menu, a file copy;
- the tooltips carry the export summary, the time, and *the grid has changed since*, from a content
  version on `GridPreview`;
- attention at the end of an animated export away from the app: taskbar flash when shown, tray
  notification when hidden, success and failure, Copy and Save;
- temp names made unique within a second.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Does not apply: no test project, by decision (see Test Impact) |
| README | | | |
| Glossary | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does *Copy last generated video* live: Copy's ▾ menu, a button in the bottom bar, both, or a link in the status line? | Both — the menu entry and the button | 2026-09-26 |
| 2 | Which generations are kept: MP4 and GIF copied, MP4 copied only, or copied and saved? | MP4 and GIF copied | 2026-09-26 |
| 3 | When is it forgotten: at the next generation or the exit whatever the grid, as soon as the grid changes, or kept across launches? | The next generation or the exit, whatever the grid | 2026-09-26 |
| 4 | Straightforward, or tricky / long? | Straightforward: one scout pass | 2026-09-26 |
| 5 | Which suggestions to include: an end-of-generation notification, *Save last generated video…*, labels with format and time, a keyboard shortcut? | Notification, *Save last…*, labels with format and time — no shortcut | 2026-09-26 |

---

*Last updated: 2026-09-26*
