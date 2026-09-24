# Export as GIF

> Working document — replace the "Force as image" checkbox with an Image / GIF / Video format
> dropdown, and add an infinitely looping animated GIF export.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today, the bottom bar holds a **Force as image** checkbox (`MainForm._forceImage`), next to Copy,
visible only while the grid holds animated content (`HasAnimation`). Unchecked, Copy and Save
produce an MP4 video (`GridExport.RenderVideo`); checked, a PNG (`GridExport.RenderStill`), and the
preview freezes (`GridPreview.ForceStill`).

The checkbox becomes a **format dropdown** with three choices — **Image**, **GIF**, **Video** —
and a new export writes an **animated GIF that loops forever**.

Relevant components:

| Component | Role |
|---|---|
| `UI/MainForm.cs` | `_forceImage`, `ExportsVideo`, `CopyToClipboard`, `Save`, `ExportVideoAsync`, `UpdateButtons`, status summaries, temp folder |
| `UI/GridPreview.cs` | `ForceStill`, `PlaysCarousel`, `ImagesChanged` |
| `Imaging/GridExport.cs` | `Job`, `RenderVideo` (the 30 fps frame loop), `RenderStill` |
| `Imaging/CarouselExport.cs` | `RenderVideo(job, playContents, …)` — the carousel's MP4 |
| `Imaging/VideoEncoder.cs` | Media Foundation Sink Writer: `Create`, `WriteFrame(bitmap, time, duration)`, `Finish` |
| `Composition/Animation.cs` | `FramesPerSecond = 30`, `FrameCount`, `FrameTime` |

The project has **no NuGet dependency**: video goes through Media Foundation by COM interop.

---

## Format Dropdown (UI)

- Replaces `_forceImage` at the same place in `_outputButtons` (between ⚙ and Copy).
- A `ComboBox` in `DropDownList` style, items in this order: **Image**, **GIF**, **Video**.
- **Animatable** means: the grid holds animated content (`HasAnimation`), **or** the carousel is
  on (`ExportsCarousel`) — the carousel moves even static images.
- **Nothing animatable**: GIF and Video are **greyed out** and cannot be selected; Image is
  selected. WinForms has no native disabled item: the list is owner-drawn
  (greyed text), and picking a greyed item puts the previous choice back.
- **Default**: Video when the grid holds animatable content, else Image.
- **Reset**: **any content change** — adding, removing or replacing a media (`ImagesChanged`) —
  puts the default back, even when the animatable state does not change. **Toggling the
  carousel** does too. A manual choice lasts until the next of these changes.
- **While exporting**: the dropdown is disabled, as the checkbox was.
- **Preview**: Image freezes the preview (`ForceStill = true`), as the checked box did; GIF and
  Video let it play.

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

- The frame loop of `GridExport.RenderVideo` (and of `CarouselExport.RenderVideo`) writes frames
  to a sink instead of `VideoEncoder` directly: a small interface with
  `WriteFrame(Bitmap, TimeSpan time, TimeSpan duration)` and `Finish()`, implemented by
  `VideoEncoder` and by a new `Imaging/GifEncoder.cs`. The loop itself is not duplicated.
- `GifEncoder` uses **Windows' built-in WIC GIF encoder** by COM interop, as `VideoEncoder` uses
  Media Foundation: no NuGet dependency. Each frame is converted to 8-bit indexed with a palette
  WIC computes from it; the frame delay goes in the Graphic Control Extension
  (`/grctlext/Delay`), the infinite loop in the NETSCAPE2.0 application extension
  (`/appext/Application`, `/appext/Data`) on the encoder's metadata.

## Carousel Interplay

With **Carrousel** checked, Copy and Save always produce the carousel's MP4 today, "Force as
image" only freezing the contents while they move. With the dropdown:

- The carousel counts as **animatable**: GIF and Video are enabled even with static images, and
  checking or unchecking it resets the default (Video when checked).
- **Video**: the carousel's MP4, contents playing (today's unchecked behaviour).
- **GIF**: the carousel as an infinitely looping GIF, contents playing, no sound —
  `CarouselExport` writes to `GifEncoder` through the same frame sink.
- **Image**: today's "Force as image" behaviour is kept — the carousel's MP4, contents frozen on
  the page each shows, silent.

---

## Test Impact

The solution holds no unit test project (`ImageGridFusion.slnx` references only
`src/ImageGridFusion`). Nothing is created or updated: the behaviour is checked by running the app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project in the solution) | — | — |

---

## Open Questions

- [x] ~~What resets the dropdown to its default?~~ → Any content change, even when the animatable state stays the same
- [x] ~~With nothing animatable, what happens to GIF and Video?~~ → Greyed out, not selectable
- [x] ~~Which settings does the GIF use?~~ → The video's: length, starting frames, 30 fps
- [x] ~~With **Carrousel** checked, how does the dropdown behave (enabled choices, what Image means, does toggling the carousel reset the default)?~~ → The carousel counts as animatable (GIF/Video enabled, toggling resets the default); GIF/Video export the carousel in that format; Image keeps today's frozen-contents carousel MP4
- [x] ~~**Copy** as GIF: a `.gif` file on the clipboard (like the video), or the file plus the raw `GIF` clipboard format?~~ → Both: the file and the raw `GIF` format
- [x] ~~Which **GIF encoder**: Windows' built-in WIC encoder by COM interop, a hand-written encoder, or a NuGet package?~~ → Windows' WIC encoder by COM interop

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

---

*Last updated: 2026-09-24*
