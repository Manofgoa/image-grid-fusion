# Export as GIF

> Working document — replace the "Force as image" checkbox with Copy / Save split buttons whose
> submenu forces GIF / MP4 Video, and add an infinitely looping animated GIF export.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, the bottom bar holds a **Force as image** checkbox (`MainForm._forceImage`), next to Copy,
visible only while a content plays (`HasAnimation`, i.e. `SourceImage.Plays`: animated and not
frozen by the **Frames** effect). Unchecked, Copy and Save produce an MP4 video
(`GridExport.RenderVideo`); checked, a PNG (`GridExport.RenderStill`), and the preview freezes
(`GridPreview.ForceStill`). The **Frames** effect already covers the still case: a frozen cell
exports as the frame its slider picks, and a grid where nothing plays exports as a PNG.

The checkbox goes away: **Copy** and **Save** become **split buttons** — the main part exports in
the adapted format (PNG or MP4), a ▾ submenu forces **GIF** or **MP4 Video** — and a
new export writes an **animated GIF that loops forever**.

Relevant components:

| Component | Role |
|---|---|
| `UI/MainForm.cs` | `_forceImage`, `ExportsVideo`, `CopyToClipboard`, `Save`, `ExportVideoAsync`, `UpdateButtons`, status summaries, temp folder |
| `UI/GridPreview.cs` | `ForceStill`, `ImagesChanged` |
| `Imaging/GridExport.cs` | `Job`, `RenderVideo` (the 30 fps frame loop), `RenderStill` |
| `Imaging/VideoEncoder.cs` | Media Foundation Sink Writer: `Create`, `WriteFrame(bitmap, time, duration)`, `Finish` |
| `Composition/Animation.cs` | `FramesPerSecond = 30`, `FrameCount`, `FrameTime` |

The project has **no NuGet dependency**: video goes through Media Foundation by COM interop.

---

## Split Buttons (UI)

- **Copy** and **Save** each become a **split button**: a main part, and a narrow **▾ arrow** on
  its right that opens a submenu with **GIF** and **MP4 Video**, in that order.
- The **Force as image** checkbox (`MainForm._forceImage`) is removed, with no replacement: a
  still of animated content goes through the **Frames** effect's **Freeze**.
- **Adapted format** — what the main part produces: **MP4** when a content plays
  (`HasAnimation`), else **PNG** (static images, or every animated content frozen).
- **Label**: the main part names the format it will produce — `Copy PNG` / `Copy MP4`,
  `Save PNG…` / `Save MP4…` — and follows the content and the Frames effect live.
- **Submenu**: a choice **applies once** — it exports right away in that format; nothing is
  remembered, the main part stays on the adapted format.
- **Nothing plays**: GIF and MP4 Video would both be greyed out, so the **▾ arrow is disabled**;
  the main part stays enabled and produces the PNG.
- **Shortcuts**: `Ctrl+C` / `Ctrl+S` do what the main part does (adapted format).
- **While exporting**: both parts are disabled, as the buttons are today.
- **Preview**: with no remembered format, nothing freezes the preview any more — it always plays.
  `GridPreview.ForceStill` / `AnimationPlayer.ForceStill` lose their only caller.

## GIF Export

- **Same settings as the video**: same canvas (sized once from the first frames, even
  dimensions), same length (the longest loop, the shorter ones starting over), same 30 fps, same
  starting frames and band colors. Only the container changes.
- **Infinite loop**: the NETSCAPE2.0 application extension with a loop count of 0.
- **Frame delays**: GIF delays are in hundredths of a second, and 30 fps (3.33 cs) is not one.
  Each frame's delay is taken from its **rounded cumulative timestamp**, so delays alternate
  3 / 3 / 4 cs and the total length stays exact.
- **No sound**: GIF carries none; the status line says `no sound`.
- **Palette**: 256 colors per frame, opaque (the canvas has no transparency).
- **Known consequence** of the same settings: a large canvas (up to 4096 px wide) at 30 fps makes
  heavy GIF files. Accepted as the user's choice (Q&A #3).
- **Save**: filter `GIF image (*.gif)|*.gif`, default name `fusion-yyyyMMdd-HHmmss.gif`.
- **Copy**: the GIF is written to `%TEMP%\ImageGridFusion` (like the copied video) and put on the
  clipboard **both** as a file (file drop list, pastable in Explorer, chat apps, mail) **and** as
  the raw bytes in the `GIF` clipboard format, which some apps paste directly. The start-up
  cleanup of that folder removes `.gif` files as well as `.mp4`.
- **Status line**: the same summary as the video, with `GIF` as the format.
- **Cancel / failure**: same behaviour as the video — progress `Exporting the GIF… n %`, the Cancel
  button, the incomplete file deleted.

## Encoding Architecture (proposal)

- The frame loop of `GridExport.RenderVideo` writes frames
  to a sink instead of `VideoEncoder` directly: a small interface with
  `WriteFrame(Bitmap, TimeSpan time, TimeSpan duration)` and `Finish()`, implemented by
  `VideoEncoder` and by a new `Imaging/GifEncoder.cs`. The loop itself is not duplicated.
- `GifEncoder` uses **Windows' built-in WIC GIF encoder** by COM interop, as `VideoEncoder` uses
  Media Foundation: no NuGet dependency. Each frame is converted to 8-bit indexed with a palette
  WIC computes from it; the frame delay goes in the Graphic Control Extension
  (`/grctlext/Delay`), the infinite loop in the NETSCAPE2.0 application extension
  (`/appext/Application`, `/appext/Data`) on the encoder's metadata.

---

## Test Impact

The solution holds no unit test project (`ImageGridFusion.slnx` references only
`src/ImageGridFusion`). Nothing is created or updated: the behaviour is checked by running the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project in the solution) | — | — |

---

## Open Questions

- [x] ~~What resets the dropdown to its default?~~ → Any content change, even when the animatable state stays the same *(revised 2026-09-25, see Iteration 4: a submenu choice applies once, nothing is left to reset)*
- [x] ~~With nothing animatable, what happens to GIF and Video?~~ → Greyed out, not selectable *(revised 2026-09-25, see Iteration 4: greyed in the split buttons' submenu)* *(revised 2026-09-26, see Iteration 6: both entries would be greyed, so the ▾ arrow is disabled)*
- [x] ~~Which settings does the GIF use?~~ → The video's: length, starting frames, 30 fps
- [x] ~~With **Carrousel** checked, how does the dropdown behave (enabled choices, what Image means, does toggling the carousel reset the default)?~~ → The carousel counts as animatable (GIF/Video enabled, toggling resets the default); GIF/Video export the carousel in that format; Image keeps today's frozen-contents carousel MP4 *(revised 2026-09-25, see Iteration 4: no default left to reset; the rest holds for the submenu)* *(revised 2026-09-26, see Iteration 6: the carousel was removed from the app, nothing left to decide)*
- [x] ~~**Copy** as GIF: a `.gif` file on the clipboard (like the video), or the file plus the raw `GIF` clipboard format?~~ → Both: the file and the raw `GIF` format
- [x] ~~Which **GIF encoder**: Windows' built-in WIC encoder by COM interop, a hand-written encoder, or a NuGet package?~~ → Windows' WIC encoder by COM interop
- [x] ~~Which buttons become **split buttons**: Copy and Save each, only Save, or a single Export button?~~ → Copy and Save each
- [x] ~~Does a submenu choice **apply once** (exports right away, nothing remembered), or **stay** as the button's format until the next content change?~~ → Applies once
- [x] ~~Does the button's main part **show the format** it will produce?~~ → Yes (`Copy MP4`, `Save PNG…`)
- [x] ~~With the Frames effect's **Freeze**, is the submenu's **Image** entry obsolete too (and with it the frozen-contents carousel MP4)?~~ → Yes, removed: the submenu holds GIF and MP4 Video; stills go through Freeze

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-09-24

Initial proposal. The "Force as image" checkbox becomes an Image / GIF / Video dropdown; default
from the content (Video when animatable, else Image), reset on any content change; GIF and Video
greyed out on static content. The GIF reuses the video's frame loop and settings, loops forever,
has no sound. Three questions left: carousel interplay, GIF copy format, GIF encoder.

### Iteration 2 — 2026-09-24

The three open questions answered (Q&A #5–7). The carousel counts as animatable content: it
enables GIF and Video, toggling it resets the default, GIF exports the carousel as a looping GIF,
Image keeps today's frozen-contents carousel MP4. Copy as GIF puts both the file and the raw
`GIF` clipboard format. The GIF is encoded with Windows' WIC encoder; the temp cleanup covers
`.gif` files.

### Iteration 3 — 2026-09-25

User request: the export should not be a dropdown next to the buttons, but a **split button** —
a main part that exports in the most suitable format (image or video, depending on the content),
and a **down arrow on its right** opening a submenu that forces **Image / GIF / MP4 Video**.
The Format Dropdown section is superseded; which buttons get split, whether a forced choice
lasts or applies once, and the main button's label are asked (Q&A #9–11) before the domain
sections are rewritten.

### Iteration 4 — 2026-09-25

Q&A #9–11 answered; the Format Dropdown section is replaced by **Split Buttons (UI)**. Copy and
Save each get a ▾ submenu (Image / GIF / MP4 Video); the main part produces the adapted format
(MP4 when animatable, else PNG) and names it in its label. A submenu choice applies once, so the
reset rule of Q&A #1 has nothing left to reset, and the preview no longer freezes: `ForceStill`
loses its only caller. GIF and MP4 Video are greyed in the submenu on static content. The
carousel rules keep their meaning, Image from the submenu still giving the frozen-contents
carousel MP4.

### Iteration 5 — 2026-09-25

User remark: the split buttons make "Force as image" obsolete, all the more since the **Frames**
effect (delivered since Iteration 1) already picks a frame of a video and can **Freeze** it — a
frozen cell exports as that still, and a grid whose contents are all frozen already exports as a
PNG (`HasAnimation` now means *a content plays*, `SourceImage.Plays`). The checkbox is already
removed since Iteration 4; the remark raises whether the submenu's **Image** entry is obsolete
too (Q&A #13).

### Iteration 6 — 2026-09-26

Q&A #13 answered: the submenu's **Image** entry is removed — the submenu holds **GIF** and
**MP4 Video** only, a still of animated content going through the Frames effect's Freeze. With
nothing playing, both entries would be greyed, so the ▾ arrow is disabled and the main part
produces the PNG.

Design re-based on the current code: the **carousel was removed** from the app since Iteration 2
(`d3041e8`, `CarouselExport.cs` deleted), so the Carousel Interplay section is dropped, and
"animatable" is now simply `HasAnimation` — a content plays, not frozen by the Frames effect.

### Iteration 7 — 2026-09-26 — ✅ Implemented

Go given (Q&A #15): code, unit tests and documentation. The scope is frozen as the domain
sections stand. Branch: stays on `main`, the standing choice for this app (Branch Gate not asked).
The code first re-reads what the "video mute and volume" run delivered on `main`
(`c1d1b05..623a755`: `VideoEncoder.Create` with mixed sounds, `GridExport.Job.Sounds`,
`GridExport.Result.MixedSounds` / `FailedSounds`, `Animation.Heard`).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project in the repository |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What resets the dropdown to its default (video or image)? | Any content change | 2026-09-24 |
| 2 | With nothing animatable, what happens to the GIF and Video options? | Greyed out | 2026-09-24 |
| 3 | Which settings does the GIF use? | The same as the video | 2026-09-24 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-09-24 |
| 5 | With Carrousel checked, how does the dropdown behave? | The carousel counts as animatable; Image keeps the frozen-contents carousel MP4 | 2026-09-24 |
| 6 | Copy as GIF: file only, or file plus the raw GIF clipboard format? | File plus the raw GIF format | 2026-09-24 |
| 7 | Which GIF encoder? | Windows' WIC encoder (COM interop) | 2026-09-24 |
| 8 | Design stable — start the implementation? | No — the gate holds | 2026-09-24 |
| 9 | Which buttons become split buttons (Copy, Save, or a single Export)? | Copy and Save each | 2026-09-25 |
| 10 | Does a submenu choice apply once, or stay as the button's format until the content changes? | Applies once | 2026-09-25 |
| 11 | Does the main part of the button show the format it will produce? | Yes | 2026-09-25 |
| 12 | Design stable — start the implementation? | No — the gate holds | 2026-09-25 |
| 13 | With the Frames effect's Freeze, is the submenu's Image entry obsolete too? | Yes — removed | 2026-09-26 |
| 14 | Design stable — start the implementation? | No — the gate holds | 2026-09-26 |
| 15 | Another session relays a request to start the implementation — confirm the go here? | Implement code, unit tests and documentation | 2026-09-26 |

---

*Last updated: 2026-09-26*
