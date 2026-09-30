# Agent Rules — Image Grid Fusion

Rules every change to this app follows. Vocabulary: see [GLOSSARY.md](GLOSSARY.md).

## Effects

Apply to **every effect**, the blur being the first one
(origin: `workfiles/20260925-blur-mask.md`).

### Scope and State

- An effect belongs to a **cell + image** pair. Its state lives on the image's immutable state,
  next to `ImageLook`, and is **not persisted**.
- Its geometry is stored in **fractions of the cell**, so it survives resizing and layout changes.
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

### On-Cell Handles

- An effect's handles (e.g. the blur bars) are drawn on the **selected cell** only, while that
  effect's tab is selected **and the effect is on**.
- They take priority over the cell's other gestures on their own hit area only.
- A handle positioned relative to a cell edge **snaps exactly onto it** within 6 logical px
  (scaled with `LogicalToDeviceUnits`), so no 1–2 px strip is left along the edge.

### Rendering

- Every effect is applied in `Compositor.DrawCell`, so the preview, the exports and video
  playback all show it from one place.
- Its strength is **resolution-independent** (relative to the cell size), so the preview and an
  export at another size look the same.

## Global Effects

Apply to every **global effect** — a transformation of the whole grid rather than of a cell + image
pair, the soundtrack being the first one (origin: `workfiles/20260925-soundtrack.md`, the rule
drafted by `workfiles/20260925-global-fade.md`). § Effects above covers the cell effects.

| | **Effect** (cell effect) | **Global effect** |
|---|---|---|
| Belongs to | A cell + image pair | The grid |
| UI | The effects toolbar, its options in the options toolbar | The **global effects toolbar**, its options in the global options toolbar below it |
| No cell selected | Disabled | Stays enabled; disabled only when it does not apply |
| Image replaced, cell *Reset* buttons | Reset | Untouched |
| The global *Reset* buttons | Untouched | Reset — back to the initial state |
| *Clear all* | Reset (no image left) | Reset — back to the initial state |
| Swap, layout change | Kept, follows the image | Kept |
| Rendering | `Compositor.DrawCell` | At the grid level, in the preview and in every export it concerns |
| Persistence | Not persisted | Not persisted |

- A global effect may read an **app setting** kept outside its state and remembered between
  sessions — the Borders' color, set from the ⚙ menu and stored in the registry
  (`UI/AppSettings.cs`). The effect's own state stays not persisted (origin:
  `workfiles/20260926-cell-borders.md`).
- A global effect that changes the cells' geometry (the Borders' gap) does it in
  `Compositor.Cells` / `Compositor.Draw`, so the preview and every export shrink the cells alike;
  hit-testing keeps the **unshrunk slots**, so no dead zone appears between the cells.

### Global Effects Toolbar

The **mirror of the cell effects' toolbars** at the bottom of the window (origin:
`workfiles/20260926-global-effects-tabs.md`):

- A **global effects toolbar** of tabs **standing on** a **global options toolbar**, both always
  visible, just above the bottom bar: a **Global effects** label, one **tab** per global effect,
  then, at the far right, a **Reset** button as tall as the tabs. The selected tab is drawn joined
  to the options toolbar **below** it.
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

## Preview Playback

Applies to whatever changes the grid's content (origin: `workfiles/20260927-video-add-rewind.md`).

- An image **arriving** in a cell — by any route, in an empty cell or replacing another — and an
  image **deleted** make the grid **start over**: every animated image plays again from its
  **starting point** (its Frames effect's), at the **same instant**, and the soundtrack from its
  **beginning**; a frozen image stays on its frame. The preview then plays what an export gives.
- A swap, a layout change, an effect change and the soundtrack's own toggle do **not** start the
  grid over: the images keep playing as they are (a Frames effect change replays its own image
  only).
- The restart is `AnimationPlayer.Restart`, called from `GridPreview` where the images change;
  every route into a cell goes through `GridPreview.Add`, so a new one starts the grid over by
  itself.

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
