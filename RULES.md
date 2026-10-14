# Agent Rules — Image Grid Fusion

Rules every change to this app follows. Vocabulary: see [GLOSSARY.md](GLOSSARY.md).

## Effects

Apply to **every effect**, the blur being the first one
(origin: `workfiles/20260925-blur-mask.md`).

### Scope and State

- An effect belongs to a **cell + image** pair. Its state lives on the image's immutable state,
  next to `ImageLook`, and is **not persisted**.
- Its geometry is stored in **fractions of the cell**, so it survives resizing and layout changes
  — the Crop aside (§ The Crop Exception).
- Its **settings** and its **on / off** state are independent: turning an effect off **keeps its
  settings**, and it is drawn as its **defaults** — preview, exports, canvas sizing, guides — until
  it is turned on again, as it was (origin: `workfiles/20260925-ui-cleanup.md`).
- Its **default state** is its default settings and its default on / off: every *Reset* brings it
  back to that state, on / off included.

| Event | Effects of the cells concerned |
|---|---|
| The cell's image is replaced (drop, Ctrl+V, browse) | Reset — default state, nothing kept |
| An image is deleted | Reset for the images that shift into another cell |
| Two cells are swapped | Kept — they follow the image, like rotation and zoom |
| The layout changes | Kept |
| The effect's own *Reset* button (options toolbar) is clicked | That effect reset |
| The effects toolbar's *Reset* button is clicked | Every effect reset, all at once |

#### The Volume Exception

The Volume effect survives a deletion (origin: `workfiles/20260925-video-mute.md`):

- An image deleted: the images shifting into another cell **keep their volume**, so a shift never
  changes what is heard. *Heard* means actually heard: a video with a sound track, playing (not
  frozen), not muted, volume above 0.
- Nothing else is special: its default state is the general one — off, the video heard at 100 %
  whatever the other cells play — so a video arrives heard, and every *Reset* brings it back there.

#### The Background Exception

The Background effect is **on by default**, and **off does not draw its defaults** (origin:
`workfiles/20260925-background-effect.md`):

- **Off draws no fill**: the cell is transparent behind its image (the preview's checkerboard, a
  PNG's alpha, white in the outputs without alpha), its settings kept.
- Its **default state is on** — automatic color, 100 % opacity: a new image, a replaced one, an
  image shifting after a deletion, and every *Reset* — its own and the toolbar's — bring it back on.

#### The Background's Edge Extension

In its extending fills (Corner pixel, Miter, Background corners), the Background stretches the
image's edges over the bands (origin: `workfiles/20261007-background-edge-extension.md`):

- It reads the image **as drawn in the cell** — rendered alone at the cell's size
  (`Compositor.DrawBackground`), the outermost rows and columns of the rectangle it covers
  (`EdgeExtension.Covered`) — never the bitmap: crop, orientation, fine angle, zoom and colors
  are extended with it, and a new effect drawn in the image is extended by itself.
- The flat fill stays under the image, fills the Background corners and is what Blend goes to; the
  **opacity covers the whole background**, the extended edges included, and the **edge opacity**
  covers the extended edges only, over the flat fill.
- Blend and Soften grow with the distance from the image relative to the band's depth, so the
  preview and an export at another size look the same.
- The crop edit view (`Compositor.DrawUncropped`) extends the whole image it shows.

#### The Crop Exception

The Crop effect's geometry is **not in fractions of the cell**, and its kept part **is the image**
(origin: `workfiles/20260930-crop-effect.md`):

- Its sides are fractions of the **image as loaded**, before any rotation or flip, so the crop
  keeps the same content when the image is turned or flipped, and survives resizing and layout
  changes like the others (`CropEffect.Seen` / `WithSeen` give and take them as the image is seen).
- The kept part stands for the whole image everywhere the image's size is read: the fitting rule,
  the automatic background (computed on the kept part, live while its bars move), the canvas
  sizing, the free format's ratio and every gesture. They read `ImageLook.Shown` (`Frame.Size`) —
  cropped, then rotated — never the bitmap's oriented size. A new consumer does the same.
- It comes right after the orientation and before the fitting rule: zoom, fine angle, background,
  colors and blur all apply to the cropped image.
- Its **edit view** — the whole image fitted whole in the selected cell, the part cut off dimmed,
  its bars across the image and snapping onto its edges, a drag inside the kept part moving it, the
  wheel scaling it (§ On-Cell Handles › Resizable Zones) — is
  drawn by `GridPreview` only, while its tab is selected and it is on; `Compositor` and the
  exports always draw the cropped image.

### Effects Toolbar

- An **always-visible row of tabs**, hanging down from the options toolbar above it: an **Effects**
  label, one **effect tab** per effect, then, at the far right, a **Reset** button (not an effect)
  **as tall as the tabs**. Every action on the image is an effect: the cell itself only keeps the
  **×**, the **✥** swap handle, and the wheel and drag gestures (origin:
  `workfiles/20260925-toolbar.md`, `workfiles/20260925-ui-cleanup.md`).
- The **separators** between cells resize the **grid**, not an image: they are no effect and have
  no tab. Their sizes belong to the cell slots — kept on a swap or a replaced image, reset with the
  layout or the image count — and the toolbar's **Reset** also puts them all back (origin:
  `workfiles/20260926-cell-resize.md`).
- Each tab holds an **activation checkbox**, checked while its effect is **on** for the **selected
  cell**. The **selected tab** is the one whose options show; it is drawn joined to the options
  toolbar. The two states are never carried by one control.
- With no cell selected, the toolbar stays visible but **disabled**; the selected tab stays
  highlighted.
- Clicks:

  | Click on | Does |
  |---|---|
  | A tab, outside its checkbox | Selects it. Activates nothing |
  | A tab's checkbox | Turns the effect on or off, **keeping its settings**, and selects the tab |

- An effect that does not apply to the selected image (e.g. Frames on a still image) keeps its tab
  **selectable**; its **checkbox is disabled**, with a **tooltip saying why**, and its options are
  disabled.
- The **selected tab** belongs to the toolbar, not to the cell: it **stays selected** when another
  cell is selected, or none. No tab is selected at startup.

### Options Toolbar

- A row **above** the effects toolbar, **always visible**, at the height of the tallest options, so
  nothing moves when another tab is selected. It holds the **selected tab's options only**, and is
  **empty** while no tab is selected.
- An effect that is off shows its **kept settings** in its options.
- **Acting on any option activates the effect**: changing any control of an effect's options turns
  the effect on (its checkbox gets checked) before applying the change, starting from its kept
  settings, so a change never happens without showing. The only exception is the effect's own
  **Reset** button, ending the row, which brings the effect back to its default state.
- **A choice among values is an option thumbnail** (origin: `workfiles/20261007-effect-option-buttons.md`):
  the Background's format — a pictogram in a **56 × 40** box (`ThumbnailStrip.EffectBox`), its name
  below, the pressed ones highlighted, its tooltip saying what it does — never a text button. Every
  strip of the cell effects has that box, so the options toolbar keeps its height.
  - The pictograms use the Background's vocabulary: the cell a grey box, the image a white rectangle,
    both outlined dark, a blue marker in a corner where a direction must show
    (`ThumbnailStrip.PaintCell` / `PaintImage`); faded while disabled.
  - Zero, one or several thumbnails may be pressed, as the effect's state says; an **action** among
    them, never pressed (Crop's 100 %), is **set apart** after them by a wider gap and a line.
  - Out of it: plain on / off checkboxes (Freeze, Mute, Automatic color, Soften), the Color… button,
    the sliders and a drop-down of a single value (the Animations' Type).

### On-Cell Handles

- An effect's handles (e.g. the blur bars) are drawn on the **selected cell** only, while that
  effect's tab is selected **and the effect is on**.
- They take priority over the cell's other gestures on their own hit area only.
- A handle positioned relative to a cell edge **snaps exactly onto it** within 6 logical px
  (scaled with `LogicalToDeviceUnits`), so no 1–2 px strip is left along the edge.

#### Resizable Zones

A **resizable zone** is a rectangle set by an effect's four bars, in fractions of its **bounds**:
the crop's kept part in the image as seen, the blur's sharp rectangle in the cell. Every zone, a
future one included, gets the same wheel (origin: `workfiles/20261007-crop-wheel-resize.md`):

- While its bars show (§ above), the wheel **anywhere over the selected cell** scales the zone
  instead of zooming the image: up grows it, down shrinks it, **5 % a notch**
  (`ZoneScale.NotchFactor`), its **ratio kept** — a crop ratio button's, else its current shape.
- It scales around the zone's **center**; with **Ctrl** held, around the **point under the cursor**,
  clamped to the bounds. Ctrl means no finer step there.
- Growing, it **slides back inside** its bounds where it crosses an edge, then stops at the largest
  rectangle at its ratio within them; shrinking stops at the bars' minimum gap.
- One geometry for every zone: `Composition/ZoneScale.Scaled`, in fractions of the bounds, called
  by the effect (`CropEffect.ScaledSeen`, `BlurEffect.Scaled`) from `GridPreview.ScaleZone`. A new
  zone adds its case there.
- With **Alt** held, the wheel **zooms the image** instead, exactly as without bars — its steps, the
  point under the cursor (§ Rendering › Zoom Keeps the Image in Place), Ctrl's finer steps (origin:
  `workfiles/20261007-resizable-zone-alt-zoom.md`). In the crop edit view, where the zoom does not
  show, it keeps the **image point under the cursor** — clamped into the kept part
  (`GridPreview.KeptPartPoint`) — where the rendered cell draws it, the zoom badge alone showing the
  change. Alt is read from `ModifierKeys` (the wheel's key flags never carry it), and releasing it
  after an Alt + wheel starts no menu mode (`GridPreview.TakeAltWheel`, `MainForm.WndProc`).
- Its bars are **solid along the zone's sides** and **dashed beyond them**, up to the bounds' edge, in
  the guides' pattern (`GridPreview.GuidePen`), each overhang drawn from the zone's corner outward;
  the same in the restore flash (§ Undo History). The dashed part stays **grabbable**, and stays
  green while its bar is hovered or dragged — only the grip and the corners' brackets turn white.
  One drawing for every zone: `GridPreview.PaintBars` (origin:
  `workfiles/20261007-zone-bars-dashed-overhang.md`).

### Rendering

- Every effect is applied in `Compositor.DrawCell`, so the preview, the exports and video
  playback all show it from one place.
- Its strength is **resolution-independent** (relative to the cell size), so the preview and an
  export at another size look the same.
- The zoom is read through **`ImageLook.ZoomIn`** — the Zoom effect's fit mode (Contain / Fill)
  resolved against the cell and `ImageLook.Shown` — never `ImageLook.Zoom` directly, which is the
  free zoom only. A new reader of the zoom does the same (origin: `workfiles/20261007-zoom-fit.md`).

#### Arrival Zoom

An image arriving in a cell is **shown whole** (origin: `workfiles/20261013-arriving-image-shown-whole.md`):

- An image **arriving** — by any route, `GridPreview.Add` — or **shifting** into another cell after a
  deletion gets the zoom **Contain** gives in its cell (`FitCalculator.FitZoom`) as a **free zoom**,
  centered, Contain unpressed (`ImageLook.WithArrivalZoom`), once the layout and the ratio have
  settled (`GridPreview.ShowWhole`, from `OnImagesChanged`). At 100 % the Zoom effect stays off.
- The one place the arrival state of the Zoom differs from its **default state**: its *Reset*, the
  toolbar's *Reset* and the effect turned off still bring back 100 %, the fitting rule.
- While its zoom is **untouched** since it arrived (`ImageLook.ZoomOnArrival`), an image is computed
  again when an arrival or a deletion reshapes its cell. Any other change of the zoom — wheel,
  slider, Contain / Fill, the checkbox, a *Reset* — clears the flag; a move does not. A separator, the
  layout strip or the format never compute it again: a free zoom does not follow the cell.
- A text laying its zoom out (`SourceImage.LaysOutZoom`) keeps filling its cell, at 100 %.

#### Zoom Keeps the Image in Place

A zoom grows or shrinks the image around **one point of it that stays where it is** — nothing else
moves it but the fit modes, below (origin: `workfiles/20261007-zoom-keeps-image-in-place.md`):

- The **wheel** — Alt + wheel over a resizable zone included — keeps the **image point under the
  cursor** (`FitCalculator.ImagePointAt`, clamped into the image: over a band, its nearest edge point).
- The routes **without a cursor** — the Zoom effect's slider, the Animations' Zoom motion, a fit mode
  unpressed — keep the **image's center** (`FitCalculator.ImageCenter`): the position readout's value
  does not change. A new route keeps the point under the cursor if it has one, the center otherwise.
- **Contain / Fill pressed recenter the image** (`GridPreview.FitSelected`): the focus becomes its
  center, the image's center on the cell's — once, at the press: a drag or the arrow keys move it
  afterwards, the mode staying pressed (origin: `workfiles/20261013-arriving-image-shown-whole.md`).
- One computation, `FitCalculator.FocusKeeping`: the stored position stays `ImageLook.Focus`,
  converted from the zoom shown before to the zoom after. The gestures store the converted focus;
  the motion is drawn with `ImageLook.FocusAt` (time 0's position, the stored focus untouched), read
  by `Compositor` next to `ImageLook.ZoomAt`.
- **No stops** after a zoom: the image is not brought back within them, even when a band shows. The
  magnetic stops are the drag's and the arrow keys' only.
- The **10 % coverage** (`FitCalculator.MinCoveredShare`), applied where the image is placed, is the
  only push a zoom gives the image.
- With a fine angle, the cover scale depends on where the image stands; where no position draws the
  point exactly where it was (an image moved far off its cell), the closest one is taken.

## Global Effects

Apply to every **global effect** — a transformation of the whole grid rather than of a cell + image
pair, the soundtrack being the first one (origin: `workfiles/20260925-soundtrack.md`, the rule
drafted by `workfiles/20260925-global-fade.md`). § Effects above covers the cell effects.

| | **Effect** (cell effect) | **Global effect** |
|---|---|---|
| Belongs to | A cell + image pair | The grid |
| UI | The effects toolbar, its options in the options toolbar | The **Global toolbar**, its options in the global options toolbar below it |
| No cell selected | Disabled | Stays enabled; disabled only when it does not apply |
| Image replaced, cell *Reset* buttons | Reset | Untouched |
| The global *Reset* buttons | Untouched | Reset — back to the initial state |
| *Clear all* | Reset (no image left) | Reset — back to the initial state |
| Swap, layout change | Kept, follows the image | Kept |
| Rendering | `Compositor.DrawCell` | At the grid level, in the preview and in every export it concerns |
| Persistence | Not persisted | Not persisted |

- A global effect may read an **app setting** kept outside its state and remembered between
  sessions — the Twitter corners' default, set from the ⚙ menu and stored in the settings file
  (`UI/AppSettings.cs`, see § App Settings). The effect's own state stays not persisted (origin:
  `workfiles/20260926-cell-borders.md`).
- An option of a global effect may **remember its last chosen value** as its start-up value — the
  Borders' color, set from their options (origin: `workfiles/20261008-border-color-in-borders-options.md`).
  It stays an ordinary option otherwise: the Resets and *Clear all* bring back its default, the undo
  history covers it, and only a value chosen by the user is saved — never one a Reset, *Clear all* or
  a restore puts back.
- A global effect that changes the cells' geometry (the Borders' gap) does it in
  `Compositor.Cells` / `Compositor.Draw`, so the preview and every export shrink the cells alike;
  hit-testing keeps the **unshrunk slots**, so no dead zone appears between the cells.

### Global Toolbar

The **mirror of the cell effects' toolbars** at the bottom of the window (origin:
`workfiles/20260926-global-effects-tabs.md`, renamed by `workfiles/20261001-output-formats.md`):

- A **Global toolbar** of tabs **standing on** a **global options toolbar**, both always
  visible, just above the bottom bar: a **Global** label, one **tab** per global setting then per
  global effect, then, at the far right, a **Reset** button as tall as the tabs. The selected tab is
  drawn joined to the options toolbar **below** it.
- Everything § Effects Toolbar and § Options Toolbar say holds, the cells aside: an **activation
  checkbox** per tab, the same clicks, the selected tab belonging to the toolbar (independent of
  the cell effects' one, none at startup), the options toolbar at **one height** and **empty** while
  no tab is selected, the **kept settings** shown while an effect is off, and **acting on any
  option activates the effect**.
- The options toolbar ends with the effect's own **Reset**; the tabs' **Reset** resets every
  global effect at once. Both bring back the **initial state** — the one *Clear all* restores — and
  leave the cells alone.
- It **stays enabled with no cell selected**, and is **locked while exporting**, like the cell
  effects: the export keeps the settings it started with.
- An effect that needs a file before it can be on (the Soundtrack) opens the file picker when its
  checkbox is checked without one; the effect stays off if it is cancelled.
- A **global setting** (the Format) is a tab of the same toolbar for a value **always in force**: its
  tab has **no activation checkbox**, its options show its value, its own **Reset** brings back its
  default, and the tabs' **Reset** and *Clear all* reset it with the global effects. Like them, it is
  not persisted and leaves the cells alone.

## Output Format

The canvas ratio has **one definition** (origin: `workfiles/20261001-output-formats.md`): the
**output format** chosen in the Format tab — Free, Twitter (1200:628, the default), Square 1:1,
Portrait 4:5, Story 9:16, Landscape 16:9 — given by `GridPreview.CanvasRatio`.

- The **Free** ratio is computed by `OutputFormats.FreeRatio`: between 9:16 and 21:9, the one losing
  the least of the images (crops plus bands, weighted by the cells' area), images read through
  `ImageLook.Shown`, texts and empty cells left out, Twitter's ratio with nothing to weigh. It is
  **held** while a separator or a crop bar is dragged, or the wheel scales the crop's kept part,
  computed again on release or when the wheel stops.
- The **preview canvas**, the **canvas sizing** (`CanvasSizer.Compute`), the **exports** (the ratio
  captured in `GridExport.Job` when they start), the **text pages** and the **layout strip** read it.
  A new consumer reads it there too, never a constant: `GridLayout` holds no ratio.
- The export's **longer side** is clamped to **[1200, 4096]** px, the other side following the ratio —
  a **video export** (MP4, GIF) to [1200, the **Video size**], an app setting of the ⚙ menu
  (`AppSettings.VideoMaxSide`: 1920 by default, 2560 or 4096), captured in `GridExport.Job` as the
  export starts, so every canvas-sized buffer of the export shrinks with it (origin:
  `workfiles/20261010-optimisation-lack-of-memory.md`).
- The Borders' **Twitter corners** apply in the **Twitter format only**: elsewhere their option is
  disabled, its setting kept, and nothing is rounded.

## Preview Playback

Applies to whatever changes the grid's content (origin: `workfiles/20260927-video-add-rewind.md`).

- An image **arriving** in a cell — by any route, in an empty cell or replacing another — and an
  image **deleted** make the grid **start over**: every animated image plays again from its
  **starting point** (its Frames effect's), at the **same instant**, and the soundtrack from its
  **beginning**; a frozen image stays on its frame. The preview then plays what an export gives.
- A swap, a layout change, an effect change and the soundtrack's own toggle do **not** start the
  grid over: the images keep playing as they are (a Frames effect change replays its own image
  only). The **Cascade** is the exception: turned on or off, or its pause changed, it starts the grid
  over, so the first content plays at once (origin: `workfiles/20261008-video-cascade.md`).
- The restart is `AnimationPlayer.Restart`, called from `GridPreview` where the images change;
  every route into a cell goes through `GridPreview.Add`, so a new one starts the grid over by
  itself.

### Transport

The preview is played, paused and moved like a video (origin: `workfiles/20261009-playback-controls.md`):

- **One clock**: `AnimationPlayer`'s `PlaybackClock`, read as `GridTime` — the frames, the Animations
  motions, the Cascade, the soundtrack and the Fade all read it, so a pause or a seek moves them
  together. A new consumer of the grid's time reads it there, never a stopwatch of its own.
- **Seek** (`AnimationPlayer.Seek`) puts the grid where the exported video stands at that time: every
  playing content from its starting point at the clock's origin, the soundtrack from its beginning;
  `Restart` is a seek to 0. The clock **wraps at the end of the grid's loop**
  (`Animation.VideoLength`) the same way, so the preview loops as the video does.
- **Paused** — by the user (Play / Pause, Space) or **held** (`AnimationPlayer.Hold` / `Release`, a
  counter): the clock stands still, nothing is heard, a frame is decoded only where a content was
  moved or laid out again. The global pause never reuses a frozen content's `Playback.PausedAt`.
- **Starting over while paused stays paused**, at 0.
- A **Copy or an export holds the grid** from `MainForm.BeginExport` to `EndExport` — and around
  the synchronous still render — then gives it back as it was. A drag of the timeline holds it too,
  each scrub step sounding a short snippet of the mix there (`AnimationPlayer.Scrub`).
- An export **closes the preview's readers** meanwhile (`AnimationPlayer.Suspend` / `Resume`, through
  `GridPreview.SuspendPlayback` / `ResumePlayback`): the cells keep their frames and their position,
  the sound's voices are released too, and the readers open again where the grid stood when the
  export ends — so the export's own readers are the only ones open (origin:
  `workfiles/20261010-optimisation-lack-of-memory.md`).
- **Space** toggles Play / Pause over every control but a text field — a focused button included —
  while the transport is enabled: with a video length, not exporting (`MainForm.ProcessCmdKey`).

## Undo History

Applies to everything the user composes (origin: `workfiles/20261006-undo-redo.md`).

- A **step** is the whole composed state after an action — a `GridState`: per cell its image and
  that image's `ImageLook`, the `GridLayout` (separators included), the output format, the global
  effects. Only references and immutable values: a new piece of composed state joins the snapshot
  (`MainForm.CaptureState`) and the restore (`MainForm.RestoreState`), never a per-action inverse.
- **Out of the history**: the selection, the selected tabs, the playback position and its pause, the app settings
  (§ App Settings),
  the file explorer, the last video.
- Steps are committed by `GridHistory` **once the state settles** — unchanged for ~300 ms, no mouse
  button held, no gesture running (`GridPreview.InGesture`): no route commits its own step, so a new
  route is covered by itself, and a gesture is one step. A new gesture that changes the grid without
  a mouse button held sets its state in `InGesture`.
- **50 steps** kept — fewer when the images they **park**, gone from the grid, weigh more than
  **512 MB** together (`GridHistory.MaxParkedBytes`; an image weighs its bitmap plus the file its
  pages keep whole, `SourceImage.MemoryBytes`): the oldest steps are dropped first (origin:
  `workfiles/20261010-optimisation-lack-of-memory.md`). An image leaving the grid goes through
  `GridPreview.ReleaseImage`, never `Dispose` directly: the history keeps it alive while a step holds it.
- A restore follows § Preview Playback: images that differ start the grid over, a swap, a layout or
  an effect change does not. It selects the cell the step touches when it touches exactly one.
- A restore **shows briefly the helper indicators of the values it changed** (§ On-Cell Helper
  Indicators), whatever tab is selected, with the zoom badge's timing (held 1 s, faded over 0.3 s):
  the zoom badge, the blur bars, the crop's kept-part edges, the guides of the stops a moved image
  rests on, the position readout of every image whose offset changed. The bars are drawn without
  grips — not handles. A new indicator a step can change is added to `GridPreview.ShowRestored` /
  `PaintRestored` — the position readout aside, which sees a restore by itself (§ Position Readout).
- Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z are **locked while exporting** and during a gesture; a focused text
  field keeps them for its own text (the whitelist in `MainForm.ProcessCmdKey`).

## Video Length

The length of the exported video has **one definition**, `Animation.VideoLength` (origin:
`workfiles/20260930-exported-video-length.md`): the grid's loop — the longest **playing** content,
a frozen one being a still, the cycle of an Animations effect counting as a loop
(`Animation.LoopOf`, origin: `workfiles/20260926-animated-zoom.md`) — or the soundtrack's length
for a grid of stills; none for a PNG.

- The **export** (`GridExport.Job.Length`), the **preview's soundtrack loop**
  (`AnimationPlayer.SyncSoundtrack`), the **loop the preview wraps at** (`AnimationPlayer.LoopLength`)
  and the transport's **length readout** and **timeline** read it there. A new consumer reads it
  there too, never re-derives it.
- The readout is refreshed **with the Copy / Save captions** (`MainForm.UpdateButtons`): what the
  buttons say they produce and the length shown never disagree.

### Cascade

While the Cascade global effect is on, the videos and animated GIFs that play take **turns** instead
of playing at once (origin: `workfiles/20261008-video-cascade.md`), one definition,
`Composition/CascadeSchedule`:

- In **cell order**; a turn is one whole loop of the played part from the starting point
  (`SourceImage.PlayedLength`), followed by the effect's pause; the cascade starts over after the last.
  Before its turn a content stands on its starting point, after it on the last frame it played; it is
  heard during its turn only.
- The grid's loop is then the whole cascade, or a loop played alongside when longer — a content
  taking no part, an Animations cycle (`Animation.GridLength(images, cascade)`): the video length, the
  soundtrack's loop and the Fade read it through `Animation.VideoLength`.
- The preview (`AnimationPlayer.Position`), its sound (`AnimationPlayer.InTurn`), the export's frames
  (`GridExport.Job.ContentTime`) and its sound (`MixedSound.TurnAt` / `Cycle`) all read the schedule. A
  new consumer of a content's time reads it there too.

### Played Part

What a content plays has **one definition** too (origin: `workfiles/20261008-video-trim.md`): the
part the Frames effect's **trim** leaves to a video or an animated GIF — `SourceImage.PlayedFrom`
and `SourceImage.PlayedLength`, the whole content untrimmed or for a PDF or a text.

- Its loop is that part (`Animation.LoopOf`); the preview (`AnimationPlayer`, through
  `SourceImage.ContentTime`), the preview's sound (`PreviewSound.Span`), the export's frames
  (`GridExport.Item.From`) and its sound (`MixedSound.From`) all play inside it. A new consumer of a
  content's time reads it there, never `PageSource.LoopDuration`.
- A video's pages are its **frames**, at the file's frame rate (`VideoFrames`), so the trim and the
  starting point stand on any of them; the starting point stays inside the trim (`SourceImage.StartPage`).

## Export Memory

What keeps an animated export within memory, whatever the grid holds (origin:
`workfiles/20261010-optimisation-lack-of-memory.md`):

- **Nothing accumulates per frame**: one canvas bitmap for the whole export, one Media Foundation
  buffer in flight, and every `Bitmap`, `Graphics`, `Region`, `ImageAttributes`, sample and buffer of
  the per-frame draw path released by `using` or `finally` — on the exception paths too. A new
  allocation in `Compositor`, the effects or the readers follows suit; the neutral `ImageAttributes`
  is the thread's shared one (`Compositor.TileFlipAttributes`), and the clip is saved and restored,
  never read back (`Graphics.Clip` allocates a `Region`).
- **The writer's queue holds at most a few raw frames**: `VideoEncoder.WriteFrame` waits while the
  sink writer's statistics (`MediaFoundation.Statistics`) show more than three frames' worth of bytes
  queued — written, not yet given to the encoder — the limit doubling when the queue does not drain
  for two seconds. The frames the encoder keeps in its own pipeline (about seventeen for the inbox
  one, received and not encoded by the statistics) are its working set, converted and smaller, not
  the queue. The writer's own throttling stays on: `MF_SINK_WRITER_DISABLE_THROTTLING` is never set.
- **Fewer holders**: the preview's readers are closed during an export (§ Transport), the undo
  history's parked images are budgeted (§ Undo History), a video export's canvas is clamped to the
  Video size (§ Output Format), and a GIF heavier than 64 MB goes on the clipboard as its file only
  (`MainForm.ClipboardGifBytes`).
- **A shortage never crashes**: `OutOfMemoryException` — Media Foundation's `E_OUTOFMEMORY` is raised
  as one, never as a `COMException` — is in the `catch` filters of every export, Copy and Save path,
  named by `ErrorLog.Describe`; what nothing catches goes to `errors.log` next to the exe and a plain
  message box (`ErrorLog`, installed by `Program.Main`), the app staying up — never the .NET dialog.
- **The probe**: every animated export samples the process's private bytes every 30 frames
  (`GridExport.MemoryProbe`), its peak in `GridExport.Result.PeakPrivateBytes` and the summaries; a
  Debug build logs the samples with the managed heap and the writer's statistics to a
  `fusion-<stamp>.log` in the temp folder, cleaned with the exports at the next start.

## Command-Line Arguments

What the exe accepts, parsed in `Program.Main` (origin: `workfiles/20261006-session-title-argument.md`):

| Argument | Does |
|---|---|
| `--tray` (`TrayApplicationContext.HiddenArgument`) | Starts hidden, the tray icon only — given by *Start with Windows* |
| `--title <text>` (`MainForm.TitleArgument`) | The **second title**: the window reads `Image Grid Fusion — <text>` |
| `--new-instance` (`SingleInstance.NewInstanceArgument`) | Starts an instance **outside the single-instance lock** — for tests |
| Anything else | A file to load into the cells, as a drop would |

- Options and files mix in any order; an option and its value are **never loaded as files**.
- `--title` takes the **next argument** as its value, unless that one is an option itself; a value
  missing or blank is ignored (the plain title), it is trimmed, and given twice the last one wins.
- The second title is shown in the **window title bar** — so in the taskbar and Alt+Tab — and on a
  **second line of the tray tooltip**, below the app's name, cut with `…` past the tooltip's
  127-character cap (`TrayApplicationContext.Tooltip`). It is **not persisted**.
- It exists for the agents: every launch by Claude Code passes the session's name in it (see
  `CLAUDE.md` § Launch), so instances running side by side tell which implementation they test.

### Single Instance

One running instance **per exe location** and Windows session (origin:
`workfiles/20260927-single-instance.md`), `UI/SingleInstance.cs`:

- The lock is a named mutex keyed on the exe's full path, taken in `Program.Main` **before anything
  else** runs; a named pipe carries the hand-over.
- A **later launch** of the same exe hands its files over — as full paths — and ends: the running
  instance comes to the front (`TrayApplicationContext.Launched`, the window restored as the taskbar
  does) and loads them like a drop (`MainForm.AddLaunchFiles`). With `--tray`, it hands nothing over;
  its `--title` is ignored. No answer within 3 s: it ends with nothing done.
- A `--new-instance` instance takes **no lock** and answers no launch, and makes **no write of its
  own accord**: no `RegistryMigration.Run`, no `StartupRegistration.Refresh`, no window size saved.
  A new automatic write at start-up or exit is skipped for it too.
- **Agents launch with `--new-instance`, always** (`CLAUDE.md` § Launch): without it, an agent's launch
  would bring back, and load its files into, the user's instance or another session's.

## Content From a URL

What the URL zone loads (origin: `workfiles/20261009-url-content.md`), `Web/`:

- **One resolver chain**, `Web/UrlResolver`: the X / Twitter post (`XPostResolver`, the public
  syndication endpoint, the post's **first** media), the direct link to a media (`DirectMediaResolver`,
  by its content type), then **yt-dlp** (`YtDlp`) — the first that recognises the URL answers. A new
  site adds its resolver there.
- A downloaded media reaches a cell **as a file**, dragged from the zone as a tile is
  (`DataFormats.FileDrop`, `MainForm.AddFilesAsync`): no route of its own into the grid, so it is
  loaded, started over and reset like any dropped file.
- The downloads go to **`%TEMP%\ImageGridFusion\url-downloads\`** (`UrlDownloads.Folder`), emptied at
  start-up by the normal instance only — a `--new-instance` one deletes nothing of its own accord.
  The same file name already there is reused, not downloaded again.
- **yt-dlp is never shipped**: looked for in `yt-dlp\` next to the exe only (`YtDlp.Folder`), never on
  the `PATH`. Missing, the zone offers to install it there with winget — the command shown and
  confirmed first; nothing is installed without that **Yes**. It is asked for a single-file format,
  so no ffmpeg is needed.
- The zone — its URL, its preview — is **not persisted** and is **out of the undo history**; a newer
  load cancels the one running, its partial file deleted.

## Content OCR Passes

How the content search reads the images and PDF pages (origin:
`workfiles/20261013-complementary-ocr-passes.md`), `Explorer/OcrPass.cs`:

- **The passes, in order** (`OcrPasses.All`), each over every image and PDF the index holds, one after
  the other in one worker (`FileExplorerPanel.Extract`): **Windows** (Windows OCR), **Rapid** (RapidOCR),
  **Inverted** (Windows OCR on the colours inverted), **Ring** (Windows OCR on the inscribed circle's band
  unrolled). One pause rule, one save cadence. A new pass is added there, with its bar.
- **The words add up**: each pass fills its own **slots** (`ContentSlot`) and never touches another's; a
  changed file starts over from the Windows pass, its later slots dropped. A slot not filled yet is a pass
  **not done**.
- **The index format**: `files.content` holds every slot; a version adding a slot reads the older one,
  its new slots *not done* — never discarded, so a pass already done is not redone.
- **Loading stays cheap**: the file is parsed once, off the UI thread, and nothing is derived for every
  word box at load time — a box is decoded and folded when the arrow first needs it, written back
  undecoded otherwise (`ContentIndex.SlotText`); the file list shows without waiting for the texts. A
  new per-box or per-word value follows the same rule.
- **Sure or weak**: a slot is **weak** when its words are less reliable — read on an odd shape (Ring), or
  with a low confidence (RapidOCR's *unsure* block, score 0.6 to 0.8) — `OcrPasses.IsWeak`. A new slot
  declares itself. The search ranks the content matches by `ContentGrade`: a sure slot holding the word,
  then a sure slot holding it once its spaces are removed (**spaces-ignored match**, query words of
  `FileSearch.JoinedMinLength` characters or more, OCR texts only), then a weak slot. The excerpt and the
  arrow come from the slot that gave the grade (`ContentIndex.Find`).
- **RapidOCR is never shipped**: its models and the native DLLs of ONNX Runtime and SkiaSharp are kept
  out of the build and downloaded on demand into **`ocr-engine\` next to the exe** (`OcrEngineFiles`) —
  only the zip entries needed, by HTTP ranges (`Web/RemoteZip`), each checked against its SHA-256 —
  offered when the content search is turned on and from the ⚙ menu while missing; nothing is downloaded
  without that **Yes**. The package versions in `ImageGridFusion.csproj` and in `OcrEngineFiles` change
  together, the hashes with them.
- The native DLLs are resolved from that folder (`OcrEngineFiles.ResolveNativeLibraries`) **before any
  code touches ONNX Runtime or SkiaSharp**: another `libSkiaSharp.dll` on the `PATH` would be loaded
  otherwise.
- Without the engine the RapidOCR pass is skipped, its bar hidden; the others run.

## App Settings

Apply to every setting remembered between sessions (origin:
`workfiles/20260926-remember-last-folder.md`):

- It is kept in **`settings.json`, next to the `.exe`** (`AppContext.BaseDirectory`), read and
  written through `UI/AppSettings.cs` only — like the `index\` folder and `favorites.txt`, the app's data
  lives beside it.
- The app **never uses the registry**. *Start with Windows* is a shortcut in the user's *Startup*
  folder (`UI/StartupRegistration.cs`). The only registry code left is `UI/RegistryMigration.cs`,
  moving an older version's values into the file once, then deleting them.
- A setting that cannot be saved says so in the status line; one that cannot be read falls back to
  its default.

## On-Cell Helper Indicators

Apply to every **helper indicator** — a measure or geometry aid drawn over a cell: guides, handles,
value readouts (origin: `workfiles/20260925-zoom-range-and-percentage.md`).

- It is **fluorescent green** (57, 255, 20), over a black halo that keeps it legible on any image —
  `HelperColor` and `HelperHalo` in `GridPreview`, defined once and shared.
- It is drawn in the **preview only** (`GridPreview.OnPaint`), never in `Compositor`, so it never
  reaches the exports.
- A hovered or dragged handle may turn **white**, as its hover feedback.
- Interaction feedback is not a helper indicator and keeps its own colours: selection outline,
  drop-target highlight, hover outline, dimmed cell being dragged.

### Guides Approach

A guide shows **before** its stop is reached (origin: `workfiles/20260926-guides-approach-gap-bar.md`):

- While an image is moved — a drag, the arrow keys — the **nearest stop** within the **approach
  distance** on an axis shows its guide, whatever the direction of the move: an edge stop from inside
  the stops only, none on an axis whose stops coincide, none while `Shift` ignores them. One
  computation, `PanMagnet.Approach`.
- The approach distance is a share of the cell's width or height, **10 % by default**: an **app
  setting** of the ⚙ menu, *Guides show from* (`GridPreview.GuideApproach`, `AppSettings.GuideApproach`),
  0 % to 25 % by 5 — 0 showing a guide only once its stop holds the image.
- Its **opacity grows linearly** with the closeness, from **20 %** at the reach to full at the stop, the
  halo fading with it (`GridPreview.PaintGuideLines`); a stop holding the image draws it as before.
- A **gap bar** — solid, at 90° to the guide, full opacity, no end ticks — **links the image to the
  anchor**: from **one point of the image** to the **guide** itself. That point is **the same for every
  axis and every stop** — never the edges for one stop and the center for another — an **app setting**
  of the ⚙ menu, *Gap bars from* (`GridPreview.GapBarsFromEdges`): the image's **center** line (the
  default), or its **edge nearest the guide** (`GridPreview.GapBarStart`).
- The bar crosses its guide through the **image's center** — during a drag and after an arrow key
  alike, never the mouse — kept inside the cell's extent (`GridPreview.GapBarAt`). It is **not clipped
  to its cell**: the point of an overflowing image may lie outside it, and the bar runs there, each move
  invalidating the whole preview. It goes once the stop holds the image.
- A new guide follows the same rule.

### Position Readout

The offset of an image from its cell's center, shown while it changes (origin:
`workfiles/20261007-move-position-readout.md`):

- Its value is the offset of the center of the box the image is drawn in (`FitCalculator.ComputeTurned`,
  the zoom read through `ImageLook.ZoomIn`) from the cell's center, in the pixels of the **PNG
  export** of the grid as it stands — the canvas `CanvasSizer.Compute` gives, the cells of
  `Compositor.Cells` (`GridPreview.ExportOffsets`): x to the right, y downward, rounded.
- An **X cross** (strokes of 30 px) on the image's center, a dashed line to it from the cell's center
  at **50 % opacity** (none at `0, 0`), and `x -35px` over `y +12px` in the cell's **top-right
  corner**, right-aligned below the × — the zoom badge's place — or below the zoom badge when it shows
  on the same cell.
- While it shows, the cell's **✥ swap handle is hidden**, neither drawn nor grabbed
  (`GridPreview.HandleOf`): a press at the center keeps moving the image.
- **One rule decides when it shows**: an image **staying in its cell** whose offset changes,
  whatever changed it — a move, its look, the grid's geometry, the export canvas, a restore. It is
  checked at each paint against the offsets last painted (`GridPreview.UpdateReadouts`), so a new
  route that moves an image is covered by itself and needs no call of its own.
- Not shown: an image new in its cell (arrived, replaced, shifted, swapped), a cell showing the crop
  edit view, the Animations motion playing (the zoom at time 0 is read), the window resized.
- Each image's readout has the zoom badge's timing on its own clock, **held at full opacity while
  `InGesture`**, its hold starting when the gesture ends.

## Repository Docs

The repository's docs — the French versions (`README.fr.md`, `GLOSSARY.fr.md`) kept in step with
the English ones, the language line, one place per fact — follow the rules shared by every
mini-app: `../CLAUDE.md` § Repository Docs.
