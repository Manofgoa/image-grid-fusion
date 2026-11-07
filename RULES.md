# Agent Rules — Image Grid Fusion

Rules every change to this app follows. Vocabulary: see [GLOSSARY.md](GLOSSARY.md).

## Effects

Apply to **every effect**, the blur being the first one
(origin: `workfiles/20260925-blur-mask.md`).

### Scope and State

- An effect belongs to a **cell + image** pair. Its state lives on the image's immutable state,
  next to `ImageLook`, and is **not persisted** — an effect preset keeps a copy, never the cell's
  state (§ Effect Presets).
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

#### The Grid Background Exception

The **Grid background** global effect acts on the cells' Background (origin:
`workfiles/20260926-forced-background.md`), the one global effect that does:

- **Turned on**, it turns off every cell's Background, its settings kept, marking it as its own
  (`ImageLook.BackgroundForced`, through `ImageLook.ForceBackgroundOff` and `GridPreview.ForceBackgrounds`);
  **turned off** — its checkbox, its Resets, *Clear all* — it turns back on the marked ones only
  (`ImageLook.UnforcedBackground`). The user turning a Background on, changing its options or resetting it
  clears the mark. The layers are left alone.
- **While it is on**, the Background's default state is **off**: an image arriving or shifting after a
  deletion (`GridPreview.BackgroundsForcedOff`), and the cell's Background Reset or the effects toolbar's Reset
  (`MainForm.ResetLook`), leave it off, marked. A new route bringing a cell's Background back to its default
  goes through them.
- An effect preset saves a marked Background **as it was before** (`LookPreset` reads
  `UnforcedBackground`).

#### The Shared Fill

The cell's Background and the Grid background draw **one fill**, `Composition/FillStyle` (origin:
`workfiles/20260926-forced-background.md`): Solid, Linear or Radial, a second color and its opacity, painted by
`FillStyle.Paint` into the rectangle its user gives — the cell, or the canvas — its geometry relative to it. A
new fill kind is a value of `FillKind`, painted there, for both users at once — the Grid background's image aside,
a member of its own (§ Grid Background Image). The Background's extended edges
lie over it pixel by pixel (`FillStyle.OpaquePixels`), and the Seams fade solid fills only
(`Compositor.FlatFill`).

- **Auto is its user's automatic color**, given at paint (`FillStyle.Paint`'s `automatic`): the last cell's for the
  Grid background, the cell's own for a cell's Background — whose first color has an Auto too, its **From**
  (`BackgroundEffect.First`: Auto, Black, White, Chosen; origin: `workfiles/20261105-cell-background-auto-colors.md`).
  A cell reads its band color only when one of them is Auto (`BackgroundEffect.NeedsAutomatic`); a new painter of a
  cell's fill passes it the same way.

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
- **Keep** (`MainForm.KeepCrop`, origin: `workfiles/20261029-image-upscale.md` § Crop to a New Image) makes the kept part
  of the image **as loaded** the cell's image — no effect drawn into its pixels — through `GridPreview.ReplaceSelectedCell`,
  one undo step: the look carries over as effects, the Crop and the Cutout reset, the Upscale off; an image without a
  file keeping the one it was cut from (`SourceImage.CroppedFrom`) for its name. Stills in cells only. A new effect whose
  state is tied to the image's pixels (as the Cutout's mask) is reset there too.

#### The Cutout Exception

The Cutout effect's state holds no pixel, and its mask is computed outside `Compositor` (origin:
`workfiles/20261029-cutout.md`):

- Its **points** are fractions of the **image as loaded**, like the Crop's sides
  (`ImageLook.LoadedPoint` / `ShownPoint`), so they survive a turn, a flip, a crop and a resize.
- Its **mask** — the model's, over the whole image as loaded — and the **cut-out** bitmap are computed
  off the UI thread by `UI/CutoutService`, one at a time, and kept on the `SourceImage` (`MaskFor` /
  `KeepMask`, `SetCut`), counted in its `MemoryBytes`: never in `Compositor.DrawCell`, which runs at
  every frame.
- Every reader drawing an image builds its `Frame` from **`SourceImage.DrawnBitmap`** — the cut-out
  while the Cutout is on and ready, else the bitmap: `Compositor.Draw` / `Render`, `GridPreview.FrameOf`,
  `GridLayers.FrameAt`, `GridExport.Job` (`ItemOf`). A new reader does the same. The cut-out then goes
  through `DrawCell` as the image would: crop, orientation, zoom, colors, blur, the Background's
  extended edges.
- It is asked for **at every paint** (`GridPreview.PaintCutout`) for every image still drawn uncut, so a
  new route changing a look is covered by itself; a request whose look changed before its turn is
  dropped. Meanwhile the cell shows the image uncut with its *Cutting out…* badge, preview only.
- **Exports wait** for the cut-outs they draw (`MainForm.WaitForCutoutsAsync`, under `BeginExport`);
  one that fails is drawn uncut, the status line saying why.
- Its edge options, Grow and Soften, are shares of the image's **longer side**, so a bitmap rendered at
  another size looks the same.
- On stills only (`SourceImage.CutsOut`): a content that plays has its checkbox disabled.
- **The part left out** is transparent, or kept with **treatments that combine** — blur, squares, a
  color mask, opacity, applied in that order, each with its own amount — built into the cut-out
  (`CutoutMask.Apply`), their strengths shares of the longer side too. A new treatment is a value of
  `CutoutBackground`, its place in `CutoutEffect.Treatments`, its case in `CutoutMask.Treated` and its
  slider in the options.
- **A pick's reach** — Auto, Part, Object, Whole — chooses among MobileSAM's masks of a click by area
  (`CutoutEngine.Pick`); it is part of the mask's key.
- **The Plain mode** has no model (origin: `workfiles/20261029-cutout-enclosed-content.md`): its mask —
  the background flooded from the edges within the tolerance, at the bitmap's resolution
  (`CutoutMask.Plain`) — is computed by `CutoutService` like the others, its tolerance and gaps in its key,
  and kept the same way. It needs no engine (`CutoutEffect.NeedsEngine`): every check of the engine on
  the Cutout's routes — the download offer (`MainForm.CutoutEngineReady`), the request, the exports' wait,
  the *Waiting for the cutout engine* badge — lets it through. A new check does the same.
- **Fill holes** is applied first in `CutoutMask.Apply`, on the mask at its own resolution, before Grow,
  Soften and Invert; it is out of the mask's key, so it never runs the model again. Both fills close the
  outline's gaps the same way (`CutoutMask.Flood`), the gap a share of the longer side.
- **The Pick view** and the **hover outline** are the preview's only (`GridPreview.Cutout.cs`), like the
  crop's edit view, and show **while Pick is armed** only, so the cutout's treatments show otherwise:
  the whole image drawn see-through over the selected cell; the object a click would pick outlined in
  the helper color —
  one decode at a time, the last cursor place decoded next, the outline placed by `Compositor.DrawCell`
  with the image's look.
- **The cutout engine is never shipped**: IS-Net (the subject) and MobileSAM (a click),
  `Imaging/CutoutEngineFiles`, installed from the Component updates window into **`cutout-engine\` next to
  the exe** from pinned URLs, each checked against its SHA-256 (§ Downloadable Components) — the window
  opened on its rows when the effect is turned on without it in a mode needing it, the effect left off.
  It runs on the OCR
  engine's ONNX Runtime (`Imaging/CutoutEngine`), its sessions without the memory arena, released after
  5 minutes unused.

#### The Upscale Exception

The Upscale effect's enlargement is computed outside `Compositor`, in a window of its own, and a GIF's is played from
pages of its own (origin: `workfiles/20261029-image-upscale.md`):

- Its state holds the **setting applied** only — the factor, ×2 or ×4, and the model, Quality or Fast — the one an
  enlargement was computed for; a factor whose result would pass `CanvasSizer.MaxLongSide` on the longer side is refused
  (`UpscaleEffect.Fits`), its thumbnail faded, **the reason said in the options row** — the size it would make — after the
  Upscale button (`MainForm.UpscaleReason`), as is why the effect does not apply to the image.
- **Its options choose, they apply nothing** — the exception to *acting on any option activates the effect*: the setting
  chosen is the toolbar's (`MainForm.UpscaleChosen`), applied by the **Upscale** button or the checkbox through
  `MainForm.ApplyUpscale` — at once when the image holds that enlargement already, else after the **Upscale window**
  (`UI/UpscaleWindow`) has computed it: size, estimated weight and duration first, then progress, time left and Cancel,
  modal. Nothing computes an enlargement anywhere else: never at paint, never in the background.
- On **stills and animated GIFs** only (`SourceImage.Upscales`, `PageSource.UpscaledFrom`): a video, a PDF, a text and a
  thumbnail have their checkbox disabled.
- A still's enlargement is computed off the UI thread by `UI/UpscaleService`, one at a time, and kept on the
  `SourceImage` per setting, a few at most (`SetUpscaled`, `HasUpscale`), counted in its `MemoryBytes`; a GIF's frames go
  one by one into **`%TEMP%\ImageGridFusion\upscale-cache\`** (`UpscaleCache`, emptied at start-up by the normal instance
  only), its pages then `UpscaledPages` (`SetUpscaledPages`). Never kept between sessions.
- **Upscale, then the cut-out**: `SourceImage.DrawnBitmap` builds the bitmap → its enlargement (`UpscaleBase`) → the
  cut-out; the Cutout's mask, read on the image as loaded, is applied to the enlarged image.
- Every reader of a content's pages that draws them reads **`SourceImage.DrawnPages`** — the player
  (`AnimationPlayer.RunAsync`), the page loader, the exports (`GridExport.ItemOf`). A new reader does the same. Its change
  reopens the content's player (`GridPreview.FollowDrawnPages`, `AnimationPlayer.Reopen`).
- The **canvas sizing reads the drawn size** (`SourceImage.Sizing` from `DrawnBitmap`, `Sharpening` 1): an enlarged image
  widens the PNG's native size.
- **The preview draws a reduced copy**: an enlargement past `SourceImage.ShownMaxSide` (2048 px) is drawn in the preview
  from `SourceImage.ShownBitmap`, reduced once; the exports, Cell copy and *Save upscaled image…* read `DrawnBitmap`, at
  full resolution. A new preview reader of an image takes `ShownBitmap`, a new export `DrawnBitmap`.
- **No Copy or Save of the grid without the enlargement**: an image whose Upscale is on without it (`UpscalePending` — a
  preset restored) shows a *Not upscaled* badge, and a Copy or a Save of the grid opens the Upscale window for it first
  (`MainForm.EnsureUpscaled`), a Cancel cancelling the Copy or the Save. A new export route calls it.
- **The upscale engine is never shipped**: Real-ESRGAN ×2 and ×4 (Quality) and Real-ESRGAN compact (Fast — its ×2 its ×4
  halved), `Imaging/UpscaleEngineFiles`, two components installed from the Component updates window into
  **`upscale-engine\` next to the exe** (§ Downloadable Components) — the window opened on the model's row when Upscale is
  clicked without it. It runs on the OCR engine's ONNX Runtime in tiles (`Imaging/UpscaleEngine`), the alpha enlarged on
  its own, its sessions released after 5 minutes unused.
- **Save upscaled image…** (`MainForm.SaveUpscaledAsync`, the cell's menu, once the enlargement is computed) writes the
  enlargement alone — cut out with the Cutout on, no other effect — as a PNG, a GIF's as an animated GIF; the cell
  untouched, out of the undo history.

### Effects Toolbar

- An **always-visible row of tabs**, hanging down from the options toolbar above it: an **Effects**
  label, one **effect tab** per effect, then a **⭐ Preset…** button and, at the far right, a **Reset**
  button — neither an effect — **as tall as the tabs** (⭐ Preset…: § Effect Presets). Every action on the image is an effect: the cell itself only keeps the
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

- A row **above** the effects toolbar, **always visible**, at the height of the tallest options — the
  room of a scrollbar below them included (§ Feature Lines) — so nothing moves when another tab is selected. It holds the **selected tab's options only**, and is
  **empty** while no tab is selected.
- An effect that is off shows its **kept settings** in its options.
- **Acting on any option activates the effect**: changing any control of an effect's options turns
  the effect on (its checkbox gets checked) before applying the change, starting from its kept
  settings, so a change never happens without showing. The only exception is the effect's own
  **Reset** button, ending the row, which brings the effect back to its default state — and the
  Upscale's options, which only choose what its **Upscale** button computes (§ The Upscale Exception).
- **A choice among values is an option thumbnail** (origin: `workfiles/20261007-effect-option-buttons.md`):
  the Background's format — a pictogram in a **56 × 40** box (`ThumbnailStrip.EffectBox`), its name
  below, the pressed ones highlighted, its tooltip saying what it does — never a text button. Every
  strip of the cell effects has that box, so the options toolbar keeps its height.
  - The pictograms use the Background's vocabulary: the cell a grey box, the image a white rectangle,
    both outlined dark, a blue marker in a corner where a direction must show
    (`ThumbnailStrip.PaintCell` / `PaintImage`); faded while disabled.
  - Zero, one or several thumbnails may be pressed, as the effect's state says; an **action** among
    them, never pressed (Crop's 100 %), is **set apart** after them by a wider gap and a line.
  - Out of it: plain on / off checkboxes (Freeze, Mute, Soften, Invert, Fill holes), the Color… button,
    the sliders and a drop-down of a single value (the Animations' Type, a fill's From and To — each choice after
    its color's swatch, `UI/ColorChoiceBox`).

#### Feature Lines

A tab's options laid out **one feature per line** (origin: `workfiles/20261104-options-toolbar-layout.md`),
`UI/FeatureLines.cs` — the Background, the Frames and the Layers so far; every tab follows it once migrated, the
others keeping their `FlowLayoutPanel` until then:

- **A feature line**: its checkbox leading it when the feature has one, else its caption, then its controls
  (`FeatureLinesPanel.AddLine`). A tall control — the layer list, the text field — is a **block** of its own
  before the lines, as tall as the toolbar (`AddBlock`).
- **Three layouts, in this order of preference** (`OptionLayout`): **C — Lines**, each slider's name and value
  above it; **A — Grid**, the leading elements right-aligned, each slider's value after it; **B — Groups**, one
  framed group per feature, its controls stacked, its caption at the bottom. The lines run top-down, a new column
  to the right when the height is full; in a column the leading elements share one width, so the controls start
  aligned, each at the top of its line, and a thin visible line separates two columns.
- **A column lines up**: the color buttons and the sliders of its lines of one shape — the same color buttons and
  sliders before them — stand at one x, a line lacking a control leaving its room empty (`FeatureLinesPanel.Keyed`).
  **One spacing**: 6 logical px between two controls, 8 after the leading element, 24 between columns, whatever
  the controls' margins — touching buttons of one row (the text alignments) aside.
- **Fixed per tab**, never recomputed on a resize: the first of C, A, B whose columns fit the toolbar with the
  window **1920 logical px wide**, the Reset aside — the Background and the Frames in C, the Layers in A. A tab
  migrated measures its own.
- **Wider than the window**, the lines scroll sideways by a scrollbar under them. Its **room is kept** under both
  options toolbars, in every tab, whether it shows or not (`FeatureLinesPanel.ScrollRoom`, `MainForm.FitEffectRows`),
  so nothing moves when it shows or when another tab is selected.
- **A tab's height** is its tallest line or block, with room for two lines of sliders
  (`FeatureLinesPanel.GetPreferredSize`); the toolbar takes the tallest tab's, as § above says.
- **A slider** is a `SliderBox`: one compact width (`SliderBox.CompactWidth`, 120 logical px) — a slider aiming
  at one frame among many `LongWidth` (200) —, its icon at its left. Its caption is a `SliderCaption`: the text
  the code sets, `Name: value`, drawn split above the slider — the name at the left, the value at the right —
  or whole after it in the Grid; its `Caption` replaces the name, `""` drawing the value alone when the line's
  caption names it already. `SliderCaption.ValueBounds` gives where the value is drawn.

### Color Options

Every color option of an effect — the Background's two, the Borders', the Grid background's two, the
Cutout's mask, a text layer's four, a future one — takes its color two ways, and has an opacity (origin:
`workfiles/20261021-color-eyedropper.md`, `workfiles/20261029-eyedropper-ergonomics.md`):

- **Two buttons touching** (`UI/ColorSplitButton`): the **eyedropper**, Windows' own split button
  (`UI/SplitButton`, § Icons) held pressed while armed, its arrow opening the **sample menu**; then
  **Color…**, its swatch before its text (shared RULES.md § Colour Items), lying over the eyedropper's
  right edge so one border shows between them, opening the shared standard dialog
  (`UI/SharedColorDialog`) — fully expanded, its custom colors remembered in the settings file, each
  eyedropper pick written into the first empty slot, then round them.
- **Its opacity right after Color…**, in one shape — the opacity icon, a slider, `Opacity: N %` (the
  Cutout's mask keeping its `Color: N %`, its effect having an Opacity of its own); in a tab laid out as
  feature lines, the icon at the slider's left, `Opacity` and `N %` above it (§ Options Toolbar › Feature Lines). An ordinary option:
  it turns its effect on, is undone, the Resets bring back its default; a pick or the dialog sets the
  color, never the opacity, and the swatch stays opaque.
- The eyedropper (`GridPreview.ArmEyedropper`) **holds the grid** and reads a **snapshot** of the cells
  as composed (`Compositor.Draw` at the preview's size, Twitter corners cut), taken when armed: what
  the preview alone draws is never read. A sample square holding a fully transparent pixel reads
  nothing.
- While armed it owns the app's mouse and keyboard (`GridPreview.EyedropperFilter`): a left press over
  the preview picks, any other press or key cancels, each swallowed with its release; leaving the
  window, a resize, an image change or an export cancels too. It is a **gesture** (`InGesture`): a
  pick is one undo step, a cancel none.
- The option follows the color under the cursor **live**, at most every 33 ms, the cells drawn fast
  meanwhile and in full once it ends; the loupe is repainted on its own area only.
- The **sample** (1–5 px, one per option, 1 px at each launch) is chosen in the eyedropper's menu,
  which arms the eyedropper at once; the size in use shows in the tooltip and the menu's check only. An
  eyedropper setting, **the exception** to "acting on any option activates the effect": it activates
  nothing by itself, is out of the undo history, and no Reset touches it.
- **The dialog previews live**: its hook reads the color it shows (its Red, Green and Blue fields) and
  the option follows it, at most every 33 ms; the dialog is a **gesture** (`GridPreview.ColorDialogOpen`)
  — OK is one undo step, Cancel gives the option back as it was, no step.
- **One route per option** (`MainForm.ColorRoute`: the color in use, preview, restore, take), given to
  the eyedropper (`ArmEyedropper`) and to the dialog (`PickColor`) alike: a new color option gets both,
  live, from it.

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

### Effect Presets

A cell's effects saved as a favorite of its file, with a description, to bring the file back into a
cell with them (origin: `workfiles/20261026-effect-preset-favorites.md`):

- **What is saved** has one definition, `LookPreset.Saved`: the effects **turned on** — never the
  settings an effect turned off keeps — the **Background turned off** too (on by default, off is a
  choice), and the **Zoom** only once changed since the image arrived, or the image moved meanwhile
  (then saved as an arrival zoom, its focus kept). Nothing else is a preset: the cell's slot, the
  global effects, the layout.
- **A new effect adds its key** to `LookPreset.ToJson` / `FromJson` and its line to
  `LookPreset.Describe`, its settings written by hand under named keys by the effect itself
  (`ToJson` / `FromJson` on its record, read through `PresetJson`, a missing or foreign value giving
  its default) — never by serializing `ImageLook`.
- **Restored in order**: the orientation first (a turn or a flip turns the crop and the focus along),
  then the crop, then the others, the zoom and its focus last. The look is set on the loaded image
  before `GridPreview.Add` (`MainForm.AddFilesAsync`, its `look`): otherwise an ordinary arrival —
  the grid starts over, one undo step. `GridPreview.ShowWhole` leaves a zoom of its own to an
  arriving image.
- **Linked to its file**: a preset is applied by bringing its file into a cell — a drag carries its
  effects beside the file (`FileExplorerPanel.PresetDataFormat`), another app getting the file
  alone — never to another image. A file has any number of presets, each identified by its own id.
- Kept in **`favorites.json`** with the favorites (`Explorer/Favorites.cs`), an older `favorites.txt`
  migrated once by the normal instance (`Favorites.Migrate`). A preset is **never dropped silently**:
  its file gone, it stays until its heart deletes it, after a confirmation.
- **Searched** under **🔠** (`FileSearch.SearchFavorites`), by its file's name or its description: the
  description matches in their own tier, after the path matches and before the content matches
  (`SearchMatch.Tier`). Every search goes through the favorites — the search view's and the folder
  view's alike (`FileExplorerPanel.WithFavorites`): the presets, and the file favorites it would not
  reach otherwise, wherever they are.
- Out of the undo history: saving, editing or deleting a preset is the favorites' business.

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
- A global effect painting **under** the cells (the Grid background) is painted first in
  `Compositor.Draw`, before the cells, the borders and the layers — over a grid of layers only too — and the
  preview paints it again under every cell it draws again (`GridPreview.DrawIntoCache`, `RedrawArea`).
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
  no tab is selected, its tabs laid out as **feature lines** once migrated, the **kept settings** shown while an effect is off, and **acting on any
  option activates the effect**.
- The options toolbar ends with the effect's own **Reset**; the tabs' **Reset** resets every
  global effect at once. Both bring back the **initial state** — the one *Clear all* restores — and
  leave the cells alone.
- It **stays enabled with no cell selected**, and is **locked while exporting**, like the cell
  effects: the export keeps the settings it started with.
- An effect that needs a file before it can be on (the Soundtrack) opens the file picker when its
  checkbox is checked without one; the effect stays off if it is cancelled.
- **A file dropped onto the toolbar goes to the effect under the cursor** — the Soundtrack, the Layers or
  the Grid background, on its tab or on its options while it is selected — and is refused everywhere else
  and while exporting (origin: `workfiles/20261107-soundtrack-drop-from-url-zone.md`). One decision,
  `UI/GlobalDrop`, read at each move of the drag (`MainForm.OnGlobalDragOver`), so the cursor refuses before
  the drop; the Soundtrack refuses a drag holding no file that may carry sound (`SoundtrackFile.MayCarrySound`).
  Every control of the two rows is a drop target (`MainForm.WireGlobalDrops`, a control added later
  included). A new global effect taking files adds its case to `GlobalDrop`.
- A **global setting** (the Format) is a tab of the same toolbar for a value **always in force**: its
  tab has **no activation checkbox**, its options show its value, its own **Reset** brings back its
  default, and the tabs' **Reset** and *Clear all* reset it with the global effects. Like them, it is
  not persisted and leaves the cells alone.

### Layers

The Layers global effect lays **floating cells** over the whole grid (origin: `workfiles/20261029-layers.md`),
one definition, `Composition/GridLayers`:

- A **layer** is a content (`SourceImage`), the look of its cell effects (`ImageLook`, kept equal on both) and
  its place (`LayerPlacement`) in **fractions of the grid** — center, size of the grid's shorter side, angle.
  Its box takes the shape of its content **as shown** (`ImageLook.Shown`); a text's size measures a line.
- **Drawn last**, over the borders, by `Compositor.DrawLayers` — each layer by `Compositor.DrawCell` into its
  box, turned — in the preview and every export, so **every cell effect applies to a layer** from the same
  place. The layers never size the canvas. A new cell effect works on a layer by itself.
- **Nothing changes for the cells**: the layers take the mouse — their hit-test first, on their own area
  only, the rotation handle before them — only while the Layers tab is selected (`GridPreview.LayersActive`);
  Ctrl+V and a drop on the grid always fill the cells. Layers are added from the Layers options, or by files
  dropped onto the Layers tab or its options (`MainForm.DroppedOnLayers`). A new gesture on the preview
  checks `LayersTakeMouse` the same way.
- **A grid holding layers only** has no layout: `Compositor.Draw` / `Render` and `GridExport.Job` take a
  `null` layout, drawing the layers alone over a transparent canvas, sized by `CanvasSizer.ForLayers` — the
  smallest where no layer's content is shrunk. With cells, the layers never size the canvas. Every check
  for an empty grid reads `MainForm.HasContent`, never the cells' count alone.
- A layer **hidden** from the layer list (`Layer.Visible`) is neither drawn, played, heard, hit nor exported:
  every reader takes `GridLayers.Shown`, never `Items`, for what is drawn.
- The **edit view** (`GridPreview.BeginLayerEdit` / `EndLayerEdit`) sets the grid aside — images, layout,
  ratio, borders, seams — and shows the layer alone as a one-cell grid at its box's ratio, so every cell
  tool works on it unchanged; the layer takes its cell's look (`SyncEditedLayer`). Meanwhile the public
  `Images`, `ActiveLayout`, `CanvasRatio` and `FreeRatio` give the grid set aside, and any change of the
  grid from outside — images, layout, format, global effects, layers, an undo, an export's lock — closes the
  view first. A new route changing the grid from outside does the same.
- A layer is **selected first**, the cell's selection and the layer's exclusive: `GridPreview.SelectedImage`
  is the selected layer's content when there is one, so the effects toolbar acts on it; the cell-only code
  reads `SelectedCellImage`. A layer's default look has the **Background off** (`Layer.DefaultLook`) — the
  layers' exception to § The Background Exception.
- Their contents **play beside the cells**: the player, the export, the sound mix and the video length take
  them (`AnimationPlayer.Sync`, `GridExport.Job.Capture`, `Animation.GridLength`), never the Cascade.
- Every change makes a **new `GridLayers`**, held by `GlobalState`: the undo history covers them, and a
  content leaving the layers goes through `GridPreview.ReleaseImage` like a cell's (`GridState.Images`).
- **Placement guides** (origin: `workfiles/20261029-layer-ergonomics.md`), `UI/GridPreview.LayerGuides.cs`: a
  dragged layer's bounding rectangle — the upright one around its turned box — snaps **by proximity** (within
  6 logical px, `LayerSnap`) onto the grid's edges and center, every cell's as drawn (`CellBounds`) and the
  other shown layers', one line per position, the grid's or a cell's winning over a layer's. They show by
  § Guides Approach — the approach distance a share of the layer's rectangle — every guide crossing the whole
  grid, another layer's at half opacity — and that layer's turned box outlined dashed at the same opacity, so
  the user sees which one matches (`PaintMatchingLayers`). Shift ignores them; the drag only, never the wheel nor the rotation.
  The cursor over the rotation handle, and while it is dragged, is a rotation arrow (`RotationCursor`).
- A **text layer** is drawn into a bitmap by `Imaging/TextRaster` — DirectWrite through Direct2D, called
  through their COM tables: the only way the app draws emojis in color; GDI and GDI+ never do (§ Text
  Emojis).
- **A text layer's changes are drawn off the UI thread** (origin: `workfiles/20261105-text-layer-typing-refresh.md`),
  `UI/MainForm.TextRender.cs`: every change of its text, style or box goes through `MainForm.RequestTextRender` — a
  key, a slider or a color followed **live**: a draft at the preview's resolution at most every 50 ms, then the full
  render 300 ms after the last change; any other change drawn in full at once — by `UI/TextLayerRenderer`, its own
  worker, the last request winning. Meanwhile the preview holds a gesture (`GridPreview.TextLayerTyping`), so the
  undo step holds the full render.
  - What a text layer is wanted to draw is read through **`MainForm.WantedOf`**, never its `Text` / `Style` /
    `TextBox` directly: a new reader of them does the same.
  - A route reading the layers to draw or keep them — an export (`EnsureUpscaled`, `BeginExport`), an edit view
    (`GridPreview.EditOpening`), the emoji fonts redrawn — puts the change in place first, in full
    (`MainForm.FlushTextRender`); a route drawing the layer itself drops it (`CancelTextRender`).

### Grid Background Image

The Grid background's image (origin: `workfiles/20261104-grid-background-image.md`), `Composition/BackgroundImage`:

- **A member of the Grid background**, never a `FillKind` — the exception to § The Shared Fill: the cell's
  Background has no image. `GridBackground.Image` holds the content (`SourceImage`), its `ImageLook` (kept equal
  on both, as a layer's), its placement and its opacity; the record compares the content by reference.
- **A floating cell as large as the canvas**: `Compositor.DrawBackgroundImage` draws it by `Compositor.DrawCell`
  over the fill, under the cells — in the canvas (`Cell`, its Zoom effect's Fill / Contain or a free zoom), into a
  cell of its own shape scaled onto the canvas (`Stretch`), or repeated (`Tile`, a tile the image fitted Contain,
  scaled by its zoom) —, so every cell effect applies to it from the one place. A new cell effect works on it by itself.
- **The preview draws it from one rendered copy** (`GridPreview.PaintBackgroundImage`), clipped under every cell
  drawn again; a new frame of it draws the whole canvas again (`RedrawOverBackground`).
- **A third source of contents** beside the cells and the layers, while the Grid background is on: the player
  (`GridPreview.PlayedBesideCells`), the video length, the sound, the cut-outs, the enlargements and the exports
  (`MainForm.ActiveBesideCells`, `GridExport.Job.BackgroundImage`, its item after the layers'), the undo history
  (`GridState.Images`, while it is off too). A new reader of the contents beside the cells takes it there too.
- **It never sizes the canvas** but alone — no cell image, no layer —, as a one-cell grid (`CanvasSizer.ForBackground`,
  `CanvasSizer.For`); `MainForm.HasContent` counts it.
- **Its default state**: no Background, Fill pressed, a video with sound muted — its Volume on at 0 %
  (`BackgroundImage.DefaultLookFor` / `DefaultOf`), what an arrival and every Reset of the background edit view
  bring back. A new image — a drop onto the options, Paste, Browse… — starts the grid over (its thumbnail takes no
  click: a double-click there opens the edit view); the one it replaces goes through `GridPreview.ReleaseImage` (`MainForm.ReleaseGoneBackgroundImage`), as on a
  Reset, *Clear all* or an undo.
- **The background edit view** is the layer edit view with the background as its target (`EditState.EditsBackground`,
  `GridPreview.BeginBackgroundEdit`): the one cell at the canvas's ratio — the image's own, shown whole with a badge,
  while stretched or tiled —; its look written back by `SyncEditedBackground`, the window told by
  `GridBackgroundEdited`.

### Text Styles

A text layer's style under a name (origin: `workfiles/20261029-text-layer-style-presets.md`), `UI/TextStyles`:

- A text style holds **`LayerTextStyle` only** — never the layer's text, size, place or angle, nor its look.
  A new setting of the text joins `LayerTextStyle`, its `ToJson` / `FromJson` under a named key (a missing or
  foreign value giving its default), and `TextStyleStrip`'s tooltip.
- **Drawn from the bottom up**: the box, the shadow, the outline, the letters (`TextRaster.Draw`); ALL CAPS
  changes the text drawn (`LayerTextStyle.Drawn`), never the text kept.
- The **built-in styles** live in the code (`TextStyles.BuiltIn`), fixed, their names reserved; the **user's** in
  `text-styles.json` next to the exe, with the name of the last style applied — the style of a new text. User
  actions write it, a `--new-instance` too. Out of the undo history; applying a style is a style change, one
  step like any setting.

### Text Emojis

The emojis of the texts — a text layer's and a text cell's — in color, flags included (origin:
`workfiles/20261102-text-flag-emojis.md`), one definition, `Imaging/Emojis`:

- **Every emoji is drawn by `TextRaster`**: a text layer whole, a text cell's emojis one by one
  (`TextRaster.RenderGlyphs`), placed among the letters GDI+ draws. A new text drawn with GDI+ does the same.
- **The flags come from the flag font**, Twemoji Mozilla cut down to its flags, embedded in the exe
  (`Imaging/TwemojiFlags.ttf`, made by `tools/make-flag-font.py`) — Segoe UI Emoji has none. It is **set on
  each flag sequence** of a layout (`TextRaster.SetFlagFont`, the sequences of `Emojis.Flags`), whatever the
  text's font, never left to a font fallback: a fallback leaves a sequence's invisible characters to the text's
  font, and the flag falls apart.
- **England, Scotland, Wales and the pirate flag are drawn from private code points** of the flag font
  (`Emojis.Joined`, U+E000 to U+E003), since DirectWrite drops their tags and VS16 before the font could join
  them; for drawing only, the text kept never changes. A new joined flag adds its code point in both places.
- **The flag font's color glyphs hold their own outline**, their layers' outlines filled in: the outline, the
  shadow and the mask draw the text without its colors.
- **A text cell's grid counts text elements**, never UTF-16 units (`TextPages.CellsOf`): an emoji
  (`Emojis.IsEmoji`) takes two columns, any other element one, and a line is never wrapped inside an element.
- **A text layer's emoji font** (`LayerTextStyle.EmojiFont`, origin: `workfiles/20261102-emoji-fonts.md`) is
  **set on each emoji** of the layout, like the flag font (`TextRaster.SetEmojiFont`, the elements `Emojis.IsEmoji`
  finds), never left to a fallback; Segoe UI Emoji, the default, sets nothing. The flags follow it when it has
  them, the flag font keeping the four joined ones. A font found nowhere draws as the default, its name kept.
- **The installed emoji fonts** are the color fonts holding 😀 (`TextRaster.InstalledEmojiFamilies`), whatever their
  name: the Emoji drop-down lists them, the font drop-down leaves them out.
- **The emoji fonts of the app are never shipped**: Twemoji, Noto Color Emoji, OpenMoji and Blobmoji
  (`Imaging/EmojiFonts`), installed from the Component updates window into **`emoji-fonts\` next to the exe** from
  pinned URLs, each checked against its SHA-256, and updated there to their **latest release** (§ Downloadable
  Components) — the window opened on their rows at the first launch (closed with none installed, the **No** is
  remembered, `AppSettings.EmojiFontsDeclined`; no offer from a `--new-instance`). **Loaded from memory** (`TextRaster.CreateMemoryCollection`), never from their files, so an update
  replaces them; `EmojiFonts.Generation` tells every thread's `TextRaster` to load them again.
- **With an emoji font, the outline and the shadow are made of the text drawn in color** — its alpha, as an
  opacity mask (`TextRaster.Shape`) — since those fonts' color glyphs have no plain shape; Segoe UI Emoji keeps the
  plain shapes.

## Output Format

The canvas ratio has **one definition** (origin: `workfiles/20261001-output-formats.md`): the
**output format** chosen in the Format tab — Free, Twitter (1200:628, the default), Square 1:1,
Portrait 4:5, Story 9:16, Landscape 16:9, Custom — given by `GridPreview.CanvasRatio`.

- The **Custom** format (origin: `workfiles/20261104-custom-output-format.md`) is the user's
  `CustomFormat` — Width, Height, Exact pixels — held by `GridPreview.Custom`, part of the composed state
  (`GridState.Custom`), not persisted, `null` while unset: the first pick of Custom fills it with the PNG
  native size of the format in force (`MainForm.CustomDefault`), the Resets and *Clear all* unset it.
  Exact pixels off, its ratio follows the native size rule; on, **`GridPreview.ExactSize`** is the size of
  every export, read by `CanvasSizer.Compute` / `ForLayers` (`CanvasSizer.Exactly`): that size, no 1200
  floor, reduced, the ratio kept, by the export's cap when smaller. A new consumer of the canvas size
  passes `ExactSize` with the ratio. Its shape stays within 1:10 to 10:1, a side within 100 to 8192 px
  with exact pixels (`CustomFormat.Of` / `WithWidth` / `WithHeight`). With exact pixels the preview
  draws the canvas at its **real size** — one export pixel a device pixel — whenever it fits the working
  area (`GridPreview.CanvasBounds`), fitted otherwise.
- **The canvas's edges set it too** (origin: `workfiles/20261104-output-format-edge-drag.md`),
  `UI/GridPreview.CanvasEdges.cs`: while the Format tab is selected (`GridPreview.FormatActive`, as
  `LayersActive`), green grips — helper indicators — mark the canvas's edges and corners, and a drag
  there takes priority over the cells on its own reach only. A vertical edge sets the width, a horizontal
  one the height, a corner both, Ctrl moving the opposite edge the other way. The canvas is drawn in the
  drag's rectangle meanwhile, its opposite edge still, the ratio held (`UpdateRatio`), fitted and centered
  again on release; a readout at its center gives what the format becomes. Each step goes through the
  fields' route (`MainForm.ResizeCanvas` → `ApplyCustom`): the Custom format in force, its mode kept — a
  ratio while unset —, its fields the PNG export's pixels of the format in force at the press
  (`MainForm.StartCanvasResize`, `PngSize`) scaled by the drag, brought within their bounds by
  `CustomFormat.WithWidth` / `WithHeight`; the canvas drawn at what they hold. One undo step (`InGesture`).

- The **Free** ratio is computed by `OutputFormats.FreeRatio`: between 9:16 and 21:9, the one losing
  the least of the images (crops plus bands, weighted by the cells' area), images read through
  `ImageLook.Shown`, texts and empty cells left out, Twitter's ratio with nothing to weigh. It is
  **held** while a separator or a crop bar is dragged, or the wheel scales the crop's kept part,
  computed again on release or when the wheel stops.
- The **preview canvas**, the **canvas sizing** (`CanvasSizer.Compute`), the **exports** (the ratio
  captured in `GridExport.Job` when they start), the **text pages** and the **layout strip** read it.
  A new consumer reads it there too, never a constant: `GridLayout` holds no ratio.
- The **canvas sizing** (`CanvasSizer.Compute`) gives the smallest canvas where **no image is
  downscaled as drawn in its cell**, read through `SizedImage` (`SourceImage.Sizing`, `Frame.Sizing`):
  its zoom (`ImageLook.ZoomAt` at time 0), its fine angle's cover scale, the cells shrunk by the
  Borders' gap (`Compositor.Cells`). A content that sharpens (`PageSource.Sharpens`: PDF, thumbnail,
  text) is read at zoom 1, unturned, so it never widens the canvas on its own. One rule for every
  export and the position readout, the cap alone differing (origin:
  `workfiles/20261015-png-native-resolution.md`).
- The **longer side** is clamped to [1200, cap], the other side following the ratio:

  | Export | Cap |
  |---|---|
  | PNG — Copy / Save | 8192 (`CanvasSizer.MaxLongSide`) |
  | PNG — a *PNG max size* entry | The entry's: 1280, 1920, 2560, 3840 or 7680 |
  | JPEG for sharing | 4096 (`CanvasSizer.MaxSharedLongSide`), then reduced to 2560 |
  | MP4 / GIF | The **Video size**, an app setting of the ⚙ menu (`AppSettings.VideoMaxSide`: 1920 by default, 2560 or 4096) |

  The cap is captured in `GridExport.Job` as the export starts (`VideoMaxSide`, `StillMaxSide`), so
  every canvas-sized buffer of a video export shrinks with it (origin:
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
  playing content from its starting point at the clock's origin, the soundtrack on the clock;
  `Restart` is a seek to the range's start (§ Video Range), 0 without one. The clock **wraps at the end
  of what the preview loops on** — the grid's loop, or the range — back to its start
  (`AnimationPlayer.WrapLoop`, `LoopStart`, `LoopLength`), so the preview loops as the video does.
- **Paused** — by the user (Play / Pause, Space) or **held** (`AnimationPlayer.Hold` / `Release`, a
  counter): the clock stands still, nothing is heard, a frame is decoded only where a content was
  moved or laid out again. The global pause never reuses a frozen content's `Playback.PausedAt`.
- **Starting over while paused stays paused**, at the range's start.
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
  effects, and the **decisions on the people** (`PeopleState`, § People). Only references and immutable
  values: a new piece of composed state joins the snapshot (`MainForm.CaptureState`) and the restore
  (`MainForm.RestoreState`), never a per-action inverse.
- **Out of the history**: the selection, the selected tabs, the playback position and its pause, the app settings
  (§ App Settings),
  the file explorer — its People decisions aside — the last video.
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
`workfiles/20260930-exported-video-length.md`): the **range**'s when one is set (§ Video Range); else the
grid's loop — the longest **playing** content, a frozen one being a still, the cycle of an Animations
effect counting as a loop (`Animation.LoopOf`, origin: `workfiles/20260926-animated-zoom.md`) — or the
soundtrack's part for a grid of stills; none for a PNG. Without the range, it is the **natural length**
(`Animation.NaturalLength`), what a range is set on.

- The **export** (`GridExport.Job.Length`), the **preview's soundtrack loop**
  (`AnimationPlayer.SyncSoundtrack`), the **loop the preview wraps at** (`AnimationPlayer.LoopLength`)
  and the transport's **length readout** and **timeline** read it there. A new consumer reads it
  there too, never re-derives it.
- The readout is refreshed **with the Copy / Save captions** (`MainForm.UpdateButtons`): what the
  buttons say they produce and the length shown never disagree.

### Video Range

The video plays and exports a **part of the grid's timeline**, its end possibly past the grid's loop, the
contents looping meanwhile (origin: `workfiles/20261106-video-range.md`), one definition,
`Composition/VideoRange`:

- A **start** and an **end** in grid time, at least a frame apart; `null` wherever it is read stands for the
  whole loop, and a range covering the loop exactly is stored as `null` (`MainForm.SetRange`). It only
  applies over a video: a still grid ignores it (`Animation.VideoStart`, `VideoLength`).
- **The export plays it** (`GridExport.Job.Start`, `Length`): the contents, the motions and the layers at
  grid time `Start + FrameTime(k)`, the encoders at the video's own time `FrameTime(k)`; every sound of the mix
  begins there too (`MixedSound.At`) — a cascade voice seeded at its place in the cascade. The Fade covers the
  range, in at its start, out at its end.
- **The preview loops on it** (`AnimationPlayer.SetRange`, `GridPreview.PlaybackRange`): it wraps from its end
  to its start, its Fade there too, and a grid standing outside it goes to its start. The playhead reads
  `LoopPosition`, the grid's time within the range — never wrapped at the grid's loop.
- **The soundtrack stands on the grid's clock** (`Soundtrack.FileTimeAt`): a range starting at 10 s hears its
  10th second, in the preview and the export alike.
- **Part of the composed state** (`GlobalState.Range`, with `KeepRange`): an undo step, reset — the whole
  loop, Keep range unchecked — by the Global toolbar's Reset and *Clear all*; not persisted.
- **When the natural length changes** (`MainForm.FollowLoop`): Keep range checked, the range is kept as it is, its
  start included, the contents looping past a shorter loop; unchecked, it goes back to the whole loop.
- **A range the user sets is kept** (`MainForm.SetRange`, the user's routes only — a handle, a field, *Fit to
  soundtrack*, *Auto*): a range set checks Keep range, the whole loop (*Auto*) unchecks it, so a content added never
  takes away what the user set (origin: `workfiles/20261106-video-range.md` Iteration 15). A step put back
  keeps the range it holds (`MainForm.RestoreState`).
- **The timeline** (`UI/PlaybackTimeline`) spans the loop, or the range's end plus one loop when the range ends
  past it (`VideoRange.Extent`): its handles drag the start and the end, snapping onto the loop's end and the
  Cascade's ticks; a seek stays inside the range. The end held in the **edge zone** — the last 32 logical px — or
  past the edge **grows on its own**, by `PlaybackTimeline.GrowthRate` loops of the grid a second (0.1 × the
  square of the closeness, 0.9 at most), the track rescaling live under the pointer (`SpanFor`); out of the zone
  it never grows, so a position can be held, and a handle grabbed in the zone waits for the pointer to move right.
  The range ending past the loop, the end handle with the pointer **left of the loop's tick** shrinks the end
  on its own at the same rate measured from the tick (`ShrinkRate`), the track rescaling live, down to the loop.
  The track draws the loop apart from what lies past it — plain, then hatched — and the loop's part left out
  mid grey. **Auto**, on the range's line, gives the whole loop back; its readout gives the loop and the range's
  gap with it (`MainForm.ShowRangeGap`). The time shown beside it counts from the range's start, as the exported video does.

### Soundtrack Part

The soundtrack plays a **part of its file**, looped or once (origin: `workfiles/20261106-video-range.md`
§ Soundtrack Trim), on `Soundtrack`:

- **From** and **To** in the file — the whole file by default — and **Loops**, on by default; a new file plays
  whole, keeping the previous one's Loops. A grid of stills lasts the part (`Soundtrack.LoopIn`).
- Its time at a grid time is `Soundtrack.FileTimeAt`: within the part, looped — or, played once, `null` past
  it, silence. The preview's node plays that part (`PreviewSound.SpanSoundtrack`), the export's voice too
  (`MixedSound.From`, `Loop` the part, `Once`).
- Its options are ordinary options of the Soundtrack: they turn it on, are undone, and wait for a file.

### Cascade

While the Cascade global effect is on, the videos and animated GIFs that play take **turns** instead
of playing at once (origin: `workfiles/20261008-video-cascade.md`), one definition,
`Composition/CascadeSchedule`:

- In **cell order**; a turn is one whole loop of the played part from the starting point, at its
  speed (`SourceImage.LoopLength`), followed by the effect's pause; the cascade starts over after the last.
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

#### Speed

A content plays at the Frames effect's **speed** (origin: `workfiles/20260926-frames-speed.md`), one
definition, on `SourceImage`:

- **Two clocks**: the **loop time**, on the grid's clock — what the clock, the Cascade, the video
  length and the progress line count — and the **content time**, what the frames and the sound are
  read at. `SourceImage.LoopLength` (`PlayedLength / Speed`) and `StartTime` are loop time;
  `PlayedFrom` and `PlayedLength` are content time; `ContentTime` goes from one to the other
  (`Animation.AtSpeed`, shared with the export's `GridExport.Item`).
- A new reader of a content's loop reads `LoopLength`, never `PlayedLength`; a new reader of its
  frames goes through `ContentTime`. `SourceImage.Speed` is 1 for a content that does not play.
- **The sound follows, its pitch with it**: the preview's node plays at the speed
  (`AudioFileInputNode.PlaybackSpeedFactor`, `PreviewSound.Sync`); the export's voice decodes the
  file at the mix's rate divided by the speed and mixes it sample for sample (`MixedSound.Speed`).
  No time-stretching.

## Output Readout

What Copy / Save produce, shown before they run (origin: `workfiles/20261107-status-bar-file-size.md`),
`UI/MainForm.OutputReadout.cs`:

- **The main part's output only**: an MP4 while `ProducesVideo`, else the PNG at its native size
  (`MainForm.MainOutput`), its dimensions from `PngSize` — the video's even, clamped to the Video size. The ▾
  entries keep their own captions and tooltips.
- **The worst case has one definition**, `Composition/OutputEstimate`: the PNG stored uncompressed, the MP4 at the
  encoder's bitrate over the video length, its AAC track with sound, its container's share — never an encode. The
  encoder reads its bitrates there (`OutputEstimate.VideoBitrate`, `AacBytesPerSecond`): a change of the encoder's
  bitrate is made there, and the estimate follows.
- **Exact while unchanged**: a Copy or a Save of the main part keeps its file's size with `GridPreview.ContentVersion`
  and the dimensions (`MainForm.KeepOutputSize`); shown without `≈` while they and the format still match. A new
  export route of the main part keeps its size the same way.
- **Refreshed by itself**: on a 250 ms timer comparing `ContentVersion` and the Video size with what it was computed
  for, and in `UpdateButtons` — a new route changing the rendered result bumps `ContentVersion` and is covered.
- **During an animated export**, the bytes its file holds so far (`BeginWriting` / `EndWriting`), read on the same
  timer, never per frame.

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
  own accord**: no `RegistryMigration.Run`, no `StartupRegistration.Refresh`, no window size saved, no icon
  written to the icon cache (`IconCache.Writes`).
  A new automatic write at start-up or exit is skipped for it too.
- **Agents launch with `--new-instance`, always** (`CLAUDE.md` § Launch): without it, an agent's launch
  would bring back, and load its files into, the user's instance or another session's.

## Cell Copy

One cell's image, or one layer, copied alone (origin: `workfiles/20261029-copy-single-cell.md`), one
rendering, `Composition/CellCopy.Render`:

- Drawn by `Compositor.DrawCell` with its look **minus the Background** — every other effect from the
  one place they are drawn, so a new cell effect is copied by itself — on a transparent canvas, **cut to
  the image as drawn** (`TurnedFit.Bounds`) clipped to its cell, never trimmed to its visible pixels.
- At its **native size**: the cell's shape in the PNG export (a layer's box), scaled until the image is
  drawn at `CanvasSizer.DrawnScale` 1 — the grid's rule for one image, without the 1200 floor —, the
  result's longer side at most `CanvasSizer.MaxLongSide`.
- A layer is copied **turned as placed**, its corners transparent; a playing content gives the frame
  shown at the grid time, the grid held while it renders.
- **Routes**: `Ctrl+C` with a cell holding an image or a layer selected (`GridPreview.RenderSelectedAlone`),
  **Copy cell** in the cell's menu (`GridPreview.RenderCellAlone`), **Copy** in the layer list — all through
  `MainForm.CopyAlone`; `Ctrl+C` with nothing selected and **`Ctrl+Shift+C`** copy the grid, the Copy
  button stays the grid's. Locked while exporting; kept as the previous copy like any Copy.
- On the clipboard like the grid's still (`MainForm.PutStillOnClipboard`). **Every paste of an image reads
  the `"PNG"` format first** (`UI/ClipboardImage`), then the bitmap: a cut-out keeps its transparency in
  another cell or a layer, where it arrives as a plain image.

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
  the `PATH`. Missing, the zone's link opens the Component updates window on it, where it is installed
  with winget at its pinned version (§ Downloadable Components); nothing is installed without a click.
- **Its companions, Deno and ffmpeg, neither** (origin: `workfiles/20261015-yt-dlp-best-format.md`):
  Deno, the JavaScript runtime YouTube's formats need, and ffmpeg, joining a video track and its sound
  — each in its own folder next to the exe, `deno\` and `ffmpeg\` (`YtDlpCompanions`), passed to yt-dlp
  explicitly (`--js-runtimes`, `--ffmpeg-location`), never looked for on the `PATH` (`--no-js-runtimes`).
  Offered **together**, the zone's link opening the Component updates window on the ones missing: on a
  yt-dlp load that fails without them, as a hint on one that succeeded without them, and with yt-dlp
  itself. A tool installed with winget goes through `Web/WingetTool`.
- **The largest format up to the Video size** (`YtDlp.FormatSelector`, `AppSettings.VideoMaxSide`, read
  when the load starts), for every site: its longer side capped, the largest of all when every one is
  larger; the resolution first, H.264 > VP9 > AV1 at equal resolution, AAC for the sound
  (`YtDlp.SortOrder`). With ffmpeg, the best video track plus the best sound, merged into an MP4;
  without it, the largest single file. The file's name carries its height, so a larger format is not
  taken for the one already there; one gauge runs over all its tracks, then *Joining…* while ffmpeg
  merges them.
- The zone — its URL, its preview — is **not persisted** and is **out of the undo history**; a newer
  load cancels the one running, its partial file deleted.

## Online Libraries

A web service of media searched from the file explorer, in views of its own — GIPHY the first, its GIFs and its
stickers (origin: `workfiles/20261105-online-library-giphy.md`, `workfiles/20261105-giphy-gif-sticker-views.md`),
`Web/Giphy`, `UI/FileExplorerPanel.Giphy.cs`:

- **A view of the views drop-down per kind of media** (`ExplorerMode`: `GiphyGifs`, `GiphyStickers`), never a row
  of tabs nor a toggle: its entry after its icon, last in the Ctrl+F cycle, skipped while it cannot answer — no
  key (`FileExplorerPanel.NextMode`). Each is **shown or hidden** from the ⚙ menu's library submenu, an app setting
  (`AppSettings.GiphyViewsShown`, shown by default): hidden, it is out of the drop-down and of the cycle
  (`FileExplorerPanel.IsOffered`), and the view shown hidden gives the folder view. A new library, or a new kind of
  one, is another value there.
- **Its results reach the grid as files**, like the URL zone's (§ Content From a URL): downloaded first —
  a click on the tile, its gauge then its ✓ — into a kebab-case folder of the temp folder emptied at start-up
  by the normal instance only (`GiphyFiles.Folder`, `GiphyFiles.Clean`), then dragged as `DataFormats.FileDrop`
  or added by a double-click through `FileActivated`. No route of its own into the grid.
- **A row of a result is no file** (`ExplorerRow.Online`): its `FullPath` a key, never handed to the disk —
  `Exists` refuses it, its menu holds **Save…** alone, greyed until downloaded (§ Save Dialogs), its size and indexed content are none; its picture is its
  animated thumbnail (`ThumbnailGrid.OnlineFrame`, `UI/OnlineThumbnails`), never asked from the Shell.
- **The key's budget**: a free GIPHY key allows **100 calls an hour**. A call is a search or a page of 50
  (`Giphy.PageSize`); the thumbnails and the downloads come from the CDN, out of the budget. **Nothing calls the
  API but a user's request**: **Enter** in the box — an empty box asking for the trending —, the invitation's
  **Trending** button, ↑ or the breadcrumb's view name (the trending), and the next page of the list shown.
  Entering a view, the Ctrl+F cycle, emptying the box, a new key or a new rating call nothing: each view shows its
  **last list, kept for the session** (`_giphyKept`), else **invites to search**; the trending of each kind is kept
  for the session too, and a new key or rating forgets them all. Nothing calls the API in the background or per
  tile; a new route calling it answers a user's request the same way. A refusal — an invalid key, the hour used up — is said on the status line, the list kept.
  GIPHY's answers carry no quota: **the app counts its own calls** of the last hour (`GiphyQuota`), kept in
  the settings file (`AppSettings.GiphyCalls`) so a restart does not reset it, shown at the end of the
  breadcrumb; a new route calling the API counts its call there. Each page received says on the status line
  how many results it brought.
- **The key is the user's**, in `settings.json` only (`AppSettings.GiphyApiKey`), set from the ⚙ menu's GIPHY
  submenu or the view's invitation; never in the repository, never shipped. The content rating every request
  carries is an app setting of the same submenu too (`AppSettings.GiphyRating`, `r` by default).
- **The attribution** GIPHY's terms require — **Powered by GIPHY** — shows at the end of the view's breadcrumb
  line whenever it shows results. A new library's attribution follows its own terms.
- **A heart** keeps a result's file next to the exe (`GiphyFiles.FavoritesFolder`), an ordinary favorite, its
  file recycled once no favorite holds it, as a pasted favorite's.
- Out of the undo history, like the rest of the file explorer.

## Save Dialogs

Saving a downloaded content, and where every save dialog opens (origin: `workfiles/20261107-save-downloaded-content.md`):

- **Save…** on a **downloaded content** — the URL zone's preview, a GIPHY result once downloaded, a cell whose file
  is one (`DownloadedContent.Holds`: directly in `UrlDownloads.Folder`, `GiphyFiles.Folder` or
  `GiphyFiles.FavoritesFolder`) — copies its file, as downloaded, where the user chooses: one route,
  `FileExplorerPanel.SaveDownloadedAsync`. A new place showing a downloaded content offers it there.
- **A cell adopts the saved file** (`SourceImage.AdoptFile`), so a favorite or a preset made afterwards outlives the
  temp folder. Layers and the Grid background's image are out of it.
- **A file saved under the base folder or a secondary source is indexed at once**, alone (`FileIndex.Add`,
  `FileExplorerPanel.AddSavedToIndex`), its content texts dropped for the next passes; no rescan.
- **Every save dialog opens on `FileExplorerPanel.SaveFolder`**: the folder view's open folder — the folder browsed
  in a secondary source, the base folder at first — or its nearest parent still there, else *Pictures*
  (`DownloadedContent.InitialFolder`). The grid's Save and its ▾ entries, *Save last…*, *Save upscaled image…* and
  Save… read it; no save folder is remembered. A new save dialog reads it too; the open dialogs (*Add images*, the
  soundtrack) keep their own remembered folders.

## Content OCR Passes

How the content search reads the images and PDF pages (origin:
`workfiles/20261013-complementary-ocr-passes.md`), `Explorer/ContentPass.cs`:

- **The passes, in order** (`ContentPasses.All`), each over every image and PDF the index holds, one after
  the other in one worker (`FileExplorerPanel.Extract`): **Windows** (Windows OCR), **Faces**, **Vision**,
  **Rapid** (RapidOCR), **Inverted** (Windows OCR on the colours inverted), **Ring** (Windows OCR on the
  inscribed circle's band unrolled), **Caption**. The OCR ones (`ContentPasses.Ocr`) run while the content
  search is on, each other one while its own setting is (§ People, § Image Content Analysis). One pause
  rule, one save cadence. A new pass is a value of `ContentPass`, in its place, with its bar — never a
  flag beside it (origin: `workfiles/20261027-image-content-analysis.md`).
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
  with a low confidence (RapidOCR's *unsure* block, score 0.6 to 0.8) — `ContentPasses.IsWeak`. A new slot
  declares itself. The search ranks the content matches by `ContentGrade`: a sure slot holding the word,
  then a sure slot holding it once its spaces are removed (**spaces-ignored match**, query words of
  `FileSearch.JoinedMinLength` characters or more, OCR texts only), then a weak slot, then the **seen**
  slots — the tags and the caption (`ContentPasses.IsSeen`, § Image Content Analysis). Within a grade, the slot holding the word at its best **level** — whole, at a word's start, inside one
  (§ Search Word Levels) — a word found joined being inside one. The excerpt and the arrow come from the slot
  that gave the grade and the level (`ContentIndex.Find`).
- **RapidOCR is never shipped**: its models and the native DLLs of ONNX Runtime and SkiaSharp are kept
  out of the build and installed from the Component updates window into **`ocr-engine\` next to the exe**
  (`OcrEngineFiles`) — only the zip entries needed, by HTTP ranges (`Web/RemoteZip`), each checked against
  its SHA-256 — the window opened on their rows when the content search is turned on without them, the
  search staying on with Windows OCR; nothing is downloaded without a click. The package versions in
  `ImageGridFusion.csproj` and in `OcrEngineFiles` change together, the hashes with them: they are the
  runtimes' pinned versions (§ Downloadable Components).
- The native DLLs are resolved from that folder (`OcrEngineFiles.ResolveNativeLibraries`) **before any
  code touches ONNX Runtime or SkiaSharp**: another `libSkiaSharp.dll` on the `PATH` would be loaded
  otherwise.
- Without the engine the RapidOCR pass is skipped, its bar hidden; the others run.
- The **face pass** runs in the same worker, right after the Windows pass — § People — then the **Vision**
  pass, before RapidOCR, and the **Caption** pass last — § Image Content Analysis.

## People

The faces of the indexed images grouped into people, in the file explorer's **People view** (origin:
`workfiles/20261027-face-detection.md`):

- **Opt-in**: the face analysis is **off by default**, an app setting of the ⚙ menu (*Face analysis*,
  `AppSettings.FaceAnalysis`); every face, embedding and decision stays on this machine. *Forget faces…*
  deletes `index\files.faces` and `people.json` after a confirmation and turns the analysis off.
- **The face engine is never shipped**: YuNet (detection, MIT) and SFace (recognition, Apache 2.0), two
  OpenCV Zoo models, installed from the Component updates window into **`face-engine\` next to the exe**
  (`FaceEngineFiles`) — pinned revisions, each file checked against its SHA-256 — the window opened on
  their rows when the analysis is turned on without them, or from the People view, the analysis turned off
  again; nothing downloaded without a click. They run on the OCR engine's ONNX Runtime DLL, its row
  installed with them when missing. The non-commercial models
  (InsightFace) are never used.
- **The face pass** (`FileExplorerPanel.ExtractFaces`): over every image and PDF of the index — the pixels
  the OCR reads (`ContentExtractor.DecodePixels`), videos left out — with the OCR passes' pause rule and
  save cadence, its own bar. Per file: YuNet on the image fitted into 640 × 640, faces from
  `FaceAnalyzer.MinScore` and `MinFaceSide` kept, each aligned from its 5 landmarks onto SFace's template
  and embedded (`FaceAnalyzer`).
- **The face index** is a file of its own, `index\files.faces` (`FaceIndex`), never a slot of
  `files.content`: binary, versioned, per file its stamp and its faces — box, score, embedding, and in
  pixels of the image as analysed its 5 landmarks and its side; a file with no face is kept, so it is not
  analysed again until it changes. A version change rejects the older file: the face pass analyses every
  file again, and the face keys still match since the detection is unchanged (origin:
  `workfiles/20261028-face-grouping-accuracy.md`). A new value a face needs is a version change too.
- **Groups are computed, never stored** (`FaceGrouping.Compute`): from the faces, the user's decisions and
  the strictness, every time one changes, off the UI thread. A group of one face is no group: that face is
  listed alone.
- **How faces are grouped** (origin: `workfiles/20261028-face-grouping-accuracy.md`) — never a chain, never
  a normalized mean against a pairwise threshold:
  - Each face has a **quality** (`FaceQuality.Of`): its score, its side in pixels and how frontal it is
    (its landmarks), each mapped to (0, 1] and multiplied. A poor face **weighs less** in a group's sums
    and **needs more** to join: the threshold plus `FaceGrouping.QualityMargin` × (1 − quality).
  - Similarities are **averages** over the faces (`Cluster`: a weighted sum and its weight, one dot
    product): a face against a group or a person's pinned faces, two groups over every pair of theirs.
  - A face joins its person, else the closest **seed** above the threshold plus `SeedMargin`, the clearest
    faces first; the seeds are then merged by **average linkage**, the closest pair first, while a pair
    reaches the threshold, its poorer side's margin included.
  - The threshold is the **strictness level**'s, an app setting of the ⚙ menu (*Face grouping*,
    `AppSettings.FaceGrouping`): Very loose, Loose, **Normal** (the default), Strict, Very strict
    (`FaceGrouping.Thresholds`, set on real faces). Changing it regroups at once; the decisions stay.
  - A face close to no one under `FaceQuality.LowFloor` is a **low-quality face**, set apart from the faces
    alone.
- **A decision is stored on faces, never on a computed group** (`People`, `people.json` next to the exe):
  a person's **pinned** faces — key and a copy of the embedding, so it survives its files moving — its
  **excluded** faces, its name, hidden flag, and the person it was **merged into**. A merge is a link
  (`mergedInto`), each person keeping its faces: un-merging restores it exactly; sub-groups are one level
  deep. Acting on an unnamed group first makes it a person, its faces pinned. A face key is its file's
  full path and its box rounded (`FaceKeys`).
- **The view**: a third view of the file explorer, picked in its **views drop-down** (`ExplorerMode`;
  every start opens on the favorites, the open folder alone remembered): the groups — named A→Z, then the larger
  first — a tile for the hidden ones, a **Low quality** tile for the low-quality faces, then the faces alone
  as file tiles showing the face cut out of their file (`FaceCrops`). A group opened lists its sub-groups,
  then every file of it and of them; the Low quality tile opened lists its faces as the faces alone.
- **Decisions by drag and drop and context menu**: a face onto a group joins it; a group onto another
  merges after a confirmation; *Un-merge* on a sub-group; *Not <name>* on a file of an open group; *Hide*
  / *Unhide*; *Rename…* or F2. User actions write `people.json`, a `--new-instance` too.
- **A decision taking something away is confirmed** (origin:
  `workfiles/20261028-open-people-from-image.md` § Safer People Decisions): *Not <name>*, *Hide* and
  *Un-merge* open a submenu whose single item does it — *Remove from <name>*, *Hide <name>*,
  *Un-merge <name>*. A new one does the same.
- **Every decision is a step of the undo history**, in one timeline with the grid's (§ Undo History):
  each `FileExplorerPanel.SavePeople` makes a new `PeopleState` — a copy of every person and the
  decision's text, named by the status line — read by `MainForm.CaptureState`, put back by
  `FileExplorerPanel.RestorePeople` (`People.Restore`, saved, regrouped). A new decision goes through
  `SavePeople`, and is covered by itself.
- **Loading `people.json` and *Forget faces…* are never undone**: they start a new **generation**
  (`ResetPeopleState`), taken by the last step without a new one (`GridHistory.Rebase`); a step of an
  older generation restores the grid only.
- **From a photo** (origin: `workfiles/20261028-open-people-from-image.md`): a file tile's **People**
  submenu lists its people — one definition, `PhotoPeople.Of`: a group per face, a sub-group's counting
  for its top group, named as the tooltip names it; a face alone; the hidden last, marked — each entry
  its face cut out of the photo, its name or *Unnamed*, its files; clicked, the People view opens its
  group. A face alone is made a person (`People.Create`, saved) and opened once the groups hold it.
- **The group open's title**, at the top of its list, is clicked to rename it — the same route as
  *Rename…* (`FileExplorerPanel.Rename`), its `NameDialog` centered on the explorer.
- **Search by name**: under **🔠**, a person's name finds their files in their own tier — after the
  presets' descriptions, before the content matches (`FileSearch.SearchPeople`) — the hidden people left
  out, in the folder view those below the open folder.

## Image Content Analysis

What the indexed images **show**, analysed offline and searched under 💡 in French and English (origin:
`workfiles/20261027-image-content-analysis.md`):

- **Opt-in, two settings** of the ⚙ menu, off by default, independent: *Visual analysis*
  (`AppSettings.VisualAnalysis`: the Vision pass, the tags, the similarity tier, *Find similar images*) and
  *Captions* (`AppSettings.Captions`: the Caption pass). Each offers its engine's download when turned on
  without it — a **No** turns it off again. *Forget visual analysis…* deletes `index\files.vision` and the
  concepts' cache, empties the Tags and Caption slots, and turns both off; the engines and the user's
  vocabulary stay.
- **The engines are never shipped**: **SigLIP 2 base** (Apache 2.0 — its vision model, its text model and
  Gemma's SentencePiece tokenizer, `VisionEngineFiles`) and **Florence-2 base** (MIT, `CaptionEngineFiles`),
  installed from the Component updates window into **`vision-engine\` next to the exe** from pinned
  revisions, each file checked against its SHA-256 — one download for every component,
  `ComponentInstaller` (§ Downloadable Components) —, the window opened on their rows when the setting
  needing them is turned on without them, the setting turned off again. They run on the OCR engine's ONNX Runtime DLL, on the CPU. The vision encoders are
  the **uint8** exports, about twice as fast as the int8 ones on the CPU for the same results.
- **The Vision pass** (`FileExplorerPanel.Extract`, `RunVision`): over every image and PDF — the pixels
  `ContentExtractor.DecodePixels` gives — each squashed to 256 × 256 over white through SigLIP 2's vision
  model (`VisionEncoder`): an **embedding**, 768 values of length 1, kept in **`index\files.vision`**
  (`VisionIndex`), a file of its own — binary, versioned, its model's id (another model empties it); a file
  that cannot be read is kept without one, so it is not read again until it changes.
- **The tags come from the embeddings**, never from a model of their own (`VisionTags`): a bilingual
  vocabulary — `Explorer/VisionTags.txt`, embedded, ~1 000 concepts, plus the user's `vision-tags.txt` next
  to the exe, created when the analysis is turned on — each concept's text embedding cached in
  `index\vision-concepts.bin`; an image's tags are the concepts within reach of its best one
  (`VisionTags.Floor`, `Spread`, `MaxTags`), their French and English words written into the **Tags** slot.
  A vocabulary or rule change gives another `VisionTags.Key`: the tags are computed again from the stored
  embeddings, no image read; tags lost while the embeddings stay are computed again the same way.
- **The Caption pass**, last (`RunCaptions`, `Captioner`): Florence-2's `<CAPTION>` task — one sentence in
  English, greedy, no trigram twice — into the **Caption** slot. A few seconds an image.
- **`files.content` version 4**: the Tags and Caption slots after the OCR ones, written as text, without
  boxes; a version-3 file is read with its OCR slots, the two new ones *not done*. They need no Windows
  pass; the Windows pass keeps them while the file is unchanged.
- **Searched under 💡**, after every word read: the tags and the caption in two grades of their own —
  **Seen**, the query word found whole, then **Seen inside**, found inside a longer word — then the
  **similarity tier** (`FileSearch.SimilarityTier`): the query — the text typed, lowercased, **its accents
  kept**, never `FileSearch.Words` — embedded by the text model (`TextEncoder`, EOS appended, padded to 64),
  the images of the search's scope not found already that **stand out** (below), at most `SimilarCount`
  (100), the farthest first — **always**, whatever the sort order. The text model is loaded at the first
  search needing it and released after 5 minutes without one.
- **Find similar images**: the images standing out for an image, **itself left out** — from a tile's context
  menu, a **cell's context menu** (`MainForm.ShowCellMenu`), or an image dropped onto the search box, a cell
  by its ✥ handle included — listed in the search view, `Similar to <name>` and its × on the caption line,
  `— nothing close enough` when none stands out; typing gives the search back. An image without an
  embedding is embedded on the spot.
- **Standing out, never the N closest** (origin: `workfiles/20261029-similar-images-accuracy.md`): SigLIP 2's
  cosines are high for every pair of images and low for every text, so a score is read against the
  query's own scores over the index — `z = (score − mean) / spread` (`FileSearch.SearchSimilar`,
  `VisionSimilarity.Spread`) — and kept from the **strictness level**'s cutoff (`VisionSimilarity.Cutoffs`),
  an app setting of the ⚙ menu (*Similar images*, `AppSettings.SimilarImages`): Very loose, Loose,
  **Normal** (the default), Strict, Very strict, one for both features. A **text's** result must also stand
  out of the image's own scores for the vocabulary's concepts (`VisionTags.ConceptEmbeddings`) by the
  cutoff plus `VisionSimilarity.TextMargin` — an image matching any text is not a match — each image's
  spread kept with its embedding (`FileExplorerPanel.SpreadOf`). The margin is set so that nonsense
  queries find nothing at Normal: a new reader of the similarity goes through `SearchSimilar`.
- **The closeness** of every such result — how far it stands out, 0 % at one spread, 100 % at five
  (`VisionSimilarity.Closeness`), the same for an image and a text, never the raw cosine — is carried by
  `ExplorerRow.Closeness` and shown on the tile (a dark label at its bottom left), before the size in the
  Details row, and in the tooltip.
- **The tile's tooltip shows what the index holds** of a file (`ThumbnailGrid.IndexedContent`,
  `FileExplorerPanel.IndexedContentOf`), after its path: 💡 the caption, 🏷 the tags concept by concept
  (`VisionTags.ConceptSeparator`, a word the same in both languages written once), 📝 the beginning of the
  text read — Windows OCR's, else RapidOCR's — and 👨‍🦰 the people its faces belong to, the named ones,
  then the unnamed counted, the hidden ones left out; each line only when known. Drawn by the app
  (`UI/IndexToolTip`): each icon a Segoe UI Emoji glyph in a colour of its own, as the tiles' light bulb —
  GDI draws no full-colour emoji. The explorer's tiles only; a new kind of indexed content adds its line
  there.
- Everything stays on this machine; out of the undo history.

## File Explorer Sections

The file explorer's lists in sections under titled headers (origin: `workfiles/20261103-file-explorer-groups.md`):

- **One field carries them**: `ExplorerRow.Section`, its title with its count; `ThumbnailGrid` draws a header above
  each run of equal titles and starts it on a line of its own (`LayoutSlots`) — every place, hit-test and arrow key
  reads that layout, never `index % perRow`. A new list in sections only sets the titles, in one run each.
- **The folder view** (`FileExplorerPanel.FolderSections`): the folder groups (`SharedPrefix.Groups`), *Other
  folders*, the **series** (`NamePatterns.Series`), *Named files*, one section per **name group**
  (`NamePatterns.Groups`), then the sources found by themselves (`NamePatterns.DetectSources`). A file takes a name
  group first, else a detected source, else a series. A new pattern is a `NameGroup` of `NamePatterns.Groups` — its
  place the fallback order — its id kept in the settings, its icon and tooltip in the ⚙ menu's **File explorer
  groups** (`MainForm.GroupIcon` / `GroupTip`); a new known source, a row of `NamePatterns.KnownSources`.
- **The search** (`FileExplorerPanel.SearchSections`): *Path* then *Content*, by `FileSearch.IsPathTier`; a new tier
  says on which side it falls there. A file whose name and content both hold every word ranks first
  (`Rank.ByNameAndContent`), sections on or off.
- Three buttons, 🗂️, 📚 (the series) and 🗃️, and the groups turned off are app settings
  (`AppSettings.ExplorerGroups`, `ExplorerPatternsOff`) — 📚 and the submenu's **Series** one state; out of the
  undo history.

## Search Word Levels

How a query word stands where it is found ranks the search (origin: `workfiles/20261105-whole-word-search-ranking.md`),
one definition, `FileSearch.LevelOf`:

- **Three levels**, best first (`WordLevel`): **whole** — *tique* in *Tique bulle* —, at a **word's start** — *Tiques* —,
  **inside** one — *Soviétique*. A word is a run of letters or of digits of the folded text: anything else, and a change
  between a letter and a digit, separates two words (*tique2* holds *tique* whole); a change of case does not.
- **They refine a tier, never cross it**: in every path tier — the name before the subfolders, as before — and every
  content grade (`ContentHit`), the matches holding more query words whole, then more at a word's start, come first
  (`Rank.CompareTier`) — **before the sort order**. The favorites' file names are ranked alike (`SearchFavorites`); a
  preset's description and a person's name keep their tier unranked inside.
- **One comparable number**: `SearchMatch.Tier` and `FavoriteMatch.Tier` fold the levels into the tier
  (`FileSearch.Numbered`, a tier spanning `Levels(words)` numbers), so the merge of the favorites, presets and people
  and `IsPathTier` read them unchanged. A new tier is numbered there.
- A new place matching a query word against a name or a text ranks it through `LevelOf`.

## Secondary Sources

Other folders shown in the folder view beside the base folder, each indexed apart (origin:
`workfiles/20261105-secondary-file-sources.md`), `Explorer/SecondarySources`, `UI/FileExplorerPanel.Sources.cs`:

- **Kept in the settings** (`AppSettings.SecondarySources`), managed in the ⚙ menu's window (`UI/SecondarySourcesWindow`);
  a source never overlaps the base folder nor another source (`SecondarySources.Refusal`), so a file belongs to one
  index only — the base folder chosen is refused too when it would overlap one.
- **One index per source**, its four files in `index\secondary-sources\<id>\` (`IndexFiles.Of`), never the base folder's
  `_index` / `_contents` / `_faces` / `_vision`: a new reader of the explorer's index reads the root a file belongs to
  (`Locate`, `AnyEntryOf`), the folder view's through `ViewIndex` / `ViewContents` / `ViewTree`. A removed source's index
  is deleted once its work is over.
- **Read when opened, never at start-up**: its list of files is scanned when its tile is opened; only its faces and
  embeddings are loaded at start-up, from its files, for the two places it is read outside its folder.
- **Searched only while open**: the search view and the base folder's searches never reach a source. **Two exceptions**:
  its faces join the People view (`Regroup` gathers every root's), and its images are among the similar images
  (`SimilarRows`). A new reader beyond them leaves the sources out.
- **Analysed on demand**: 🔄 inside a source scans it and runs the passes over it (`StartSourceRun`, `Extract` with its
  `IndexFiles`); one worker at a time, the base folder's passes waiting (`StartExtraction` returns while `_sourceRun` is
  set); leaving the source — another folder, another view, a setting changed — pauses it (`PauseSourceRun`). The base
  folder's automatic runs never start a source's.
- **In its color**: a source's own tile (`ExplorerRow.Source`) — its folder and its file count — and its name in the
  breadcrumb, after a clear `│` rather than the child's `›` (`Breadcrumb.SetPath`'s `sourceAt`); the folders inside it
  keep their usual color. A source not found stays, faded (`ExplorerRow.Unavailable`). Not remembered as the open folder.
- **One color per source** (`UI/SourceColors`): blue, violet, teal, green, plum — never a red, an orange or a yellow, read
  as an error or a warning —, an index into the palette kept with its folder in the settings, given when it is added — the
  first no source holds, else the least held (`SecondarySources.Colored`) — and kept until it is removed: removing a
  source never recolors another. A new place drawing a source reads `SourceRoot.Colors`.
- **The status line while a source is open** is in its color (`FileExplorerPanel.Show`), an error staying red: its own
  index's summary, then its own run's activity only — never the base folder's (`ShowSummary`).

## Start-Up

What the main window does not wait for before it shows (origin: `workfiles/20261105-initial-load-speed.md`):

- **The emoji icons the icon cache does not hold** (§ Icons): while `UiIcon.Deferring` — set in `Program.Main`,
  unless the app starts hidden — such an icon is handed out **blank**, then drawn at the first idle moment after the
  window has shown (`MainForm.FinishStartup` → `UiIcon.DrawDeferred`), painted into its shared bitmap and every copy
  still alive, the window repainted. A control keeping a copy of its own — a split button's image list — is given
  its icon again there (`UpdateEffectIcons`); a new one does the same.
- **The file explorer's content**: held hidden (`FileExplorerPanel.ContentHeld`), the panel showing its background
  at its width, let go in `FinishStartup`, before the icons; `FileExplorerPanel.Start` still runs from `OnShown`.
- **The font drop-down's families**: the selected one alone until the list is first opened or focused
  (`MainForm.FillFonts`). A new long list built at start-up waits the same way.
- A new piece of start-up work that the window does not need to show waits for `FinishStartup`.

## Keyboard Shortcuts

Every shortcut of the app is documented, in one place (origin: `workfiles/20261029-shortcuts-list.md`):

- **`SHORTCUTS.md` lists every one** — keys and key combinations, wheel gestures, a modifier held
  during a drag or a click, double-clicks with a meaning, and Windows' own behaviour in the app's
  windows — in the `##` section of where it works, one `| Shortcut | Does |` row each.
- **A shortcut added, changed or removed** updates its row in `SHORTCUTS.md` **and** `SHORTCUTS.fr.md`
  **in the same commit** as the code. A commit touching a key or mouse handler — `ProcessCmdKey`,
  `OnKeyDown`, `OnMouseWheel`, `ModifierKeys`, `e.Clicks == 2`, a double-click — without them is
  incomplete. Nothing checks it but this rule.
- **The file is the app's contract**: embedded in the exe, it is what the **shortcuts window** (F1, the ⚙
  menu) shows, parsed by `UI/ShortcutList.Parse` — a `##` heading per section, a table under it whose
  header row and separator row are skipped, the keys written `<kbd>Ctrl</kbd>+<kbd>Z</kbd>`, the
  language line, the title and the paragraphs left out. A row it cannot read does not show.
- **One place per fact**: the README and the other docs never list keys — they link to the section of
  `SHORTCUTS.md` (`SHORTCUTS.fr.md` from a French doc) where they are.

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

## UI Language

The app shows its texts in **English** (the default) or **French**, the **UI language** chosen in the ⚙
menu's **Language / Langue** (origin: `workfiles/20261105-french-translation.md`):

- **One lookup for every user-visible text**: `Resources/Strings.resx` (English) and
  `Resources/Strings.fr.resx` (French), read through the `Strings` class MSBuild generates from them —
  a missing key is a build error; the French texts built into the `fr\` satellite assembly, bundled into
  the published exe. Never a literal shown to the user in the code.
- **A new text is added in both files in the same commit**, its key named after its place and meaning;
  a text with values is **one** entry with numbered placeholders (`Texts.Format(Strings.X, a, b)`),
  never fragments glued together. The French one uses the French terms of
  [GLOSSARY.fr.md](GLOSSARY.fr.md). The build warns of a key missing from either file
  (`CheckFrenchTexts` in the csproj): a build with that warning is not finished.
- **Read once, at start-up**: `Program.Main` sets the UI culture (`CultureInfo.DefaultThreadCurrentUICulture`)
  from `AppSettings.Language` before any form exists; a change applies at the next start, a restart
  offered (`MainForm.RestartApp`). No text is looked up into a `static readonly` field or a `const`.
  Only the UI culture: sorting and casing keep the system's.
- **Displayed numbers follow it** — `12.5 s` / `12,5 s`, the file sizes in `KB` / `Ko` — through
  `Resources/Texts` (`Format`, `Number`); every parse and serialization stays `InvariantCulture`, and a
  number the user types is read in the UI culture or the invariant one.
- **Left as they are**: the app's name, product and format names, key names, the user's content,
  what is written to disk for the app or the agents (`errors.log`, `settings.json`, file and folder
  names), and the **Language / Langue** item — bilingual, each language named in its own.
- **The shortcuts window** shows `SHORTCUTS.fr.md` in French (embedded beside `SHORTCUTS.md`, the same
  parser): it is kept in step with the English one (§ Keyboard Shortcuts).

## Downloadable Components

Every file or tool the app downloads rather than ships — the engines' models, the runtimes they run on,
the URL zone's tools, the emoji fonts — is a **component**, installed and updated from **one place**,
the **Component updates window** (origin: `workfiles/20261102-component-update-verification.md`):

- **One registry**: `Components/ComponentRegistry`, one component per **upstream** — a Hugging Face
  repository, a GitHub release, a NuGet package, a winget package — with its group (Engines, Runtimes,
  Web tools, Emoji fonts), its purpose, its license and its **source** (`ComponentSource`), which gives its
  page too (`PageUrl`, opened by a click on its name; a winget tool's is its project's). Its pinned files
  stay with the code that reads them (`OcrEngineFiles`, `FaceEngineFiles`, `EmojiFonts`…). A new
  downloadable component is registered there.
- **Every install goes through the window**: no download or winget install runs anywhere else, and the ⚙
  menu holds no *Download* entry. A feature lacking a component **opens the window** on its rows
  (`MainForm.ShowComponents` with a `ComponentNeed`), highlighted, a balloon saying why it is wanted;
  **the feature goes back to off** at once — the Cutout's mode not set, an analysis setting turned off —,
  the content search aside, which runs with Windows OCR meanwhile. Nothing installs by itself.
- **The manifest**, `components.json` next to the exe (`ComponentManifest`): per component its
  `pinned` version — **a copy of the code's**, rewritten at every start, the code always winning —, its
  `forced` one — **the user's**, a full list of files with their URLs and SHA-256 (a winget tool: its
  version alone), never written by the app —, the version `installed` with its files' hashes, the one
  `pending` the next start, `installedOn` and `updatedOn`. Missing, it is recreated, a component already
  there recorded as its pinned version; unreadable, it is kept as `components.json.bad`. A
  `--new-instance` writes it only on a user's install.
- **The latest version** is read when the window opens and on *Check now*, never in the background: each
  source's (`LatestAsync`), a Hugging Face file compared by its LFS SHA-256 or its Git blob id, so a commit
  touching other files offers no update; a winget tool's installed version read from the tool itself.
- **An install** downloads into `update-staging\<id>\` in the component's folder, each file checked
  against its SHA-256 when known beforehand and by the component's own check (`Component.Accepts`), off
  the UI thread; then what holds the files is **released** (`IComponentsHost.ReleaseAsync`: the cutout
  engine's sessions, the text model, the content passes), the files swapped, the version replaced deleted.
  A file still locked — a native runtime loaded — stays staged: **swapped at the next start**, in
  `Program.Main` before anything loads it (`ComponentInstaller.ApplyPending`), the window offering a
  restart. A new resident engine adds its release there.
- **Uninstall** (the row's ▾, or **Uninstall all** for every row installed, one confirmation) deletes the installed version's files — a winget tool's folder — after a
  confirmation naming the size freed, what holds them released first; a file still locked is left, the row
  saying a restart frees it. The **size** shown is what those files take on the disk.
- **Several rows at once**, each with its own progress bar and ×, redrawn by the window's timer only, never
  by each progress report; a runtime several rows need is installed
  once; the winget installs one at a time. Closing the window — or quitting the app — while one runs asks
  first, a **Yes** cancelling them, the installed versions untouched.
- Out of the undo history.

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

## Icons

Every control that does something shows an icon, **emoji first** (origin:
`workfiles/20261029-menu-icons.md`), one definition, `UI/UiIcons`:

- **Which controls**: every **action and toggle** of every menu — the ⚙ menu, Copy ▾ / Save ▾, every
  context menu —, the effect and global **tabs**, the **buttons** (icon alone, or before the text) and
  the entries of the **drop-downs** that name a choice (the views, the sort, the Animations' Type), a
  new one included. Never a menu's greyed caption, never the **values** of a submenu (`1920 px`,
  `Normal`…), never the font drop-down (each font in its face) nor the Borders style. A confirming item
  (*Hide ▸ Hide*, *Delete ▸ Delete <name>*) takes its parent's icon: it is the action itself. A colour
  item keeps its swatch (shared RULES.md § Colour Items).
- **Its style**: a **Segoe UI Emoji glyph in full color**, wherever one says the thing — over the
  app's own drawings too; the emoji the tile tooltip shows for a meaning (💡 caption, 🏷 tags, 📝 text
  read, 👨‍🦰 people) is that meaning's everywhere. A **drawing** (`EffectIcons`) only where no emoji
  says it — Crop, Format, Fade, Borders, Seams, Duplicate, the eyedropper, the Fade curves, the
  intensity and opacity, the text alignments, the option thumbnails —, the app's own icon (`AppIcon`)
  for the tray's *Open*.
- **The direction of a label stays text**: `Date ↓`, `Size ↑`, `Name A→Z` — never an arrow emoji
  inside a label.
- **One icon per action**: a `UiIcons` field, shared by every control showing the action (*Find
  similar images*, *Copy*, the Component updates window's *Install* and its ⚙ entry). A new control takes the field of its
  action, or adds one.
- **Drawn at 16 logical px in device pixels**, again when the DPI changes; the emoji rendered by
  Direct2D (`TextRaster`) — GDI draws them in one color — at **four times its side** (`UiIcon.EmFor`), then scaled
  down, each size kept once drawn. Never a small bitmap stretched.
- **Kept on the disk** (origin: `workfiles/20261105-initial-load-speed.md`): an emoji icon drawn is written into
  **`icon-cache\` next to the exe** (`UI/IconCache`), one subfolder per stamp — `IconCache.DrawingVersion` and Segoe
  UI Emoji's file —, the older ones deleted at the first write; a later start reads it instead of drawing. A change
  of the emoji drawing raises `DrawingVersion`. Written by the normal instance only. A menu entry: `UiIcons.Set` on it, `UiIcons.Attach` on its menu, drawn as it opens.
  A button: `UiIcons.SetImage`, an icon-only one stretched to its row's height. A drop-down's entry:
  owner-drawn by `UiIcons.DrawItem`.
- **A button opening a menu of variants** is Windows' own split button (`UI/SplitButton`,
  `BS_SPLITBUTTON`), its arrow built in — never a second button beside it.
- A **checked toggle** of a menu shows its icon framed, as WinForms' default renderer draws it, in
  place of the ✓; a disabled entry's or control's icon is greyed.

## Repository Docs

The repository's docs — the French versions (`README.fr.md`, `GLOSSARY.fr.md`) kept in step with
the English ones, the language line, one place per fact — follow the rules shared by every
mini-app: `../CLAUDE.md` § Repository Docs.
