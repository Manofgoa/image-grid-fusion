# Grid Background Image

> Working document — an image, GIF or video as the Grid background, dropped into its options,
> fitted, and edited with every cell effect in an edit view of its own.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The **Grid background** global effect paints one fill — the shared fill (`Composition/FillStyle`) — under
the whole grid. It gets an optional **background image**: a content (`SourceImage`) drawn over that fill,
under the cells, the borders and the layers, across the whole canvas.

- **Grid background only**: the cell's Background gets no image kind. The image is **not** a value of
  `FillKind` — the one exception to § The Shared Fill's "a new fill kind serves both users"; it is a
  member of the Grid background, drawn over its fill (Q&A 1).
- **Any content**: a still, an animated GIF or a video (Q&A 7).
- **A floating cell, like a layer** (Q&A 5): the image has its own `ImageLook`, so **every cell effect**
  applies to it — Crop, Cutout, Upscale, Colors, Blur, Zoom with its Contain / Fill, Rotate, Flip,
  Frames, Volume… — drawn by `Compositor.DrawCell` into the canvas rectangle. A future cell effect works
  on it by itself.
- **Set from the Grid background options**: a thumbnail of the image, a drop zone accepting files from
  the app's file explorer and from Windows Explorer (Q&A 3), a **Browse…** button.
- **Fitted** by option thumbnails — **Fill**, **Contain**, **Stretch**, **Tile** — with a zoom and a
  position (Q&A 2).
- **Edited** in the **background edit view**, opened by a **double-click on its thumbnail** in the options
  (Q&A 6): the image alone as a one-cell grid at the canvas ratio, every cell tool working on it — the
  layer edit view's mechanism, generalised.

Backlog: the Grid background half of *Chosen image fill* (`TODO-FEATURES.md`), split off (Q&A 8).

---

## State

- `Composition/GridBackground` (the record) gains an optional **`BackgroundImage`** — the content
  (`SourceImage`), its `ImageLook`, its **placement** (Fill / Contain / Stretch / Tile, see § Fitting) and
  its **opacity**. `null` = no image: the Grid background is drawn exactly as today.
- Immutable, `With…` methods like the record's others. The record is compared by value in the undo
  history and in `GridBackgroundInitial`: the content compares **by reference**, the look by value.
- **Turned off, kept**: the Grid background turned off keeps its image with its other settings, drawn
  again when it is turned back on (§ Global Effects).
- **Reset — its own, the Global toolbar's, *Clear all***: back to `GridBackground.Initial`, the image
  removed, released through `GridPreview.ReleaseImage` (never `Dispose`).
- **Undo history**: the image is part of the step through `GridBackground` already in `GlobalState`;
  `GridState.Images` adds it next to the layers' images, so the history keeps a parked one alive. The
  change label reads *Grid background*.
- **Not persisted**, like every global effect.

## Drawing

- `GridBackground.Paint` draws the fill, then the image over it — the canvas as its cell — through
  `Compositor.DrawCell` with its look, at its opacity. Everything that paints the Grid background
  today gets it: `Compositor.Draw` (preview, exports, the eyedropper's snapshot), `GridPreview.PaintGridBackground`
  under each cell redrawn (clipped to the cell, full-canvas coordinates).
- `Paint` gains what `DrawCell` needs: the image's **frame** at the grid time (a `Frame` built from
  `SourceImage.DrawnBitmap` / `DrawnPages`, § The Cutout / Upscale Exceptions), and the `fast` flag.
- **The preview keeps one rendered copy** of the image at the canvas size, drawn again only when its
  frame, look, placement, opacity or the canvas change, so a partial redraw of one cell does not render
  the whole background again.
- **The image never sizes the canvas** (`CanvasSizer.Compute` / `ForLayers` untouched), like the layers.
- **A grid holding the background image only** — no cell image, no layer, the Grid background on with
  its image — is **content of its own** (Q&A 12): `MainForm.HasContent` and the preview's empty-grid
  tests (`_images.Count == 0 && ShownLayers is null`) count it, Copy and Save export it, the canvas sized
  on the image alone as `CanvasSizer.ForLayers` sizes a grid of layers only — the smallest canvas where
  the image is not shrunk, at the canvas ratio. With a cell or a layer, the image never sizes the canvas.

## Fitting

Option thumbnails in the Grid background options (§ Options Toolbar › option thumbnails), one pressed:

| Thumbnail | The image in the canvas |
|---|---|
| **Fill** (default) | Covers the canvas, its ratio kept, cut where it overflows — the Zoom effect's **Fill** fit mode |
| **Contain** | Whole, its ratio kept, the fill showing in the bands — the Zoom effect's **Contain** |
| **Stretch** | Exactly the canvas, its ratio not kept |
| **Tile** | Repeated over the canvas from its top-left corner, a tile being the image fitted whole (Contain) into the canvas, scaled by its zoom — 100 % one tile, 50 % tiles half as large (Q&A 9) |

- **Fill and Contain are the Zoom effect's fit modes** — one state, `ImageLook.ZoomFit`, shown pressed in
  both places: the Grid background thumbnails and the Zoom effect's options in the edit view.
- **The zoom and the position** are the image's own, its Zoom effect and its focus, set in the edit view
  by the wheel and a drag as on any cell (§ Zoom Keeps the Image in Place), or by the Zoom slider.
- **Stretch and Tile** are the background's own placement, outside `ImageLook`; the image's look is drawn
  into the tile — or the stretched canvas — as it would be into a cell of the image's own shape. No Tile
  size slider: the zoom is the tile's scale.

## Options

The Grid background options row, after its fill settings, before its Reset:

- The **image thumbnail** — the image as drawn, its look applied — or an empty drop zone reading
  *Click or drop an image here*. **A click** → the file picker at once, as **Browse…** (Iterations 4, 6). No
  double-click: the edit view opens from **Edit** only (Iteration 6). Its tooltip names the file.
- **Paste** (an image on the clipboard, read as Ctrl+V reads it, `UI/ClipboardImage`; Ctrl+V itself keeps filling
  the cells, Q&A 14), then **Browse…** right after it — its icon and its text, so it is seen (Iteration 6) —,
  **Edit** (the edit view) and **Remove** (✖, the image gone, the fill alone).
- The **placement thumbnails** (§ Fitting) and the image's **Opacity** slider (the opacity icon,
  `Opacity: N %`, § Color Options' shape).
- **Acting on any option turns the Grid background on** (§ Options Toolbar), a dropped or browsed image
  included.
- The row keeps the options toolbar's one height (`ThumbnailStrip.EffectBox`).

## Drop and Browse

- A file dropped onto the **Grid background options row**, while its tab is selected, becomes the
  background image — from the app's file explorer tiles, Windows Explorer, the URL zone: all
  `DataFormats.FileDrop`. `MainForm`'s global rows' `DragDrop` gets a third branch beside
  `DroppedOnLayers` and the Soundtrack (`DroppedOnGridBackground`).
- Several files dropped: the first one that loads is taken, the status line saying the others were
  ignored.
- A file that does not load: the image unchanged, the status line saying why.
- An image replaced — drop, Browse or Paste — gets the **default look** (every effect reset, § Scope and
  State, its sound muted, § Playback), Fill pressed, the image replaced released through `ReleaseImage`.
- **An effect preset tile** dropped arrives **with its effects** (`FileExplorerPanel.PresetLookOf`), as
  into a cell (Q&A 13).
- Loaded off the UI thread (`ImageLoader.TryLoadFile`), refused while exporting
  (`RefuseWhileExporting`), as `AddLayerFilesAsync` does.
- One undo step per drop, Browse or Paste.

## Background Edit View

The layer edit view (`GridPreview.BeginLayerEdit` / `EndLayerEdit` / `SyncEditedLayer`) generalised to a
target that is either a layer or the background image:

- `EditState.Layer` becomes an **edit target** — a layer index, or the background. Begin seeds the
  one-cell grid from the target's content and look; `SyncEditedLayer` (renamed `SyncEdited`) writes the
  look back to it — `_layers.With(…)` or `_gridBackground with { … }`; End re-selects the layer, or
  nothing for the background.
- **The one cell is the canvas**: the cell's ratio is the grid's canvas ratio (`CanvasRatio` of the grid
  set aside), so the fit, the zoom and the position chosen there are the ones the grid draws.
- **Shown alone**: no cells, no borders, no layers, no fill — the Grid background is set aside like the
  rest, so the image is never drawn over itself.
- **Everything the layer edit view does holds**: the public `Images`, `ActiveLayout`, `CanvasRatio`,
  `FreeRatio` give the grid set aside; any change of the grid from outside closes the view first;
  Escape, **Done** or a double-click outside close it. The effects toolbar acts on the background image
  meanwhile (`SelectedImage`).
- **Stretch and Tile** in the edit view: the one cell takes the **image's own shape**, so the image shows
  whole — what Contain shows —, a *⇔ Stretch* or *▦ Tile* badge in its corner; the placement applies once back
  in the grid (Q&A 10, Iteration 5).
- Opening it turns the Grid background on when it is off, and closes a layer's edit view first.

## Playback, Sound and Exports

The background image is a **third source of contents** next to the cells and the layers, wherever the
layers are fed in:

| Where | Where it goes |
|---|---|
| Preview playback | `AnimationPlayer.Sync(images, layers)` — a background argument; `GridPreview.SyncPlayer` |
| Export frames | `GridExport.Job.Capture` (`ItemOf`, after the layers: index `CellCount + LayerCount`), an accessor beside `Cells()` / `Layers()` |
| Video length | `Animation.GridLength` / `VideoLength`: its loop counts, like a layer's (never the Cascade) |
| Sound | `Animation.Heard`, `MixedSound` on the non-cascade path, the preview's sound |
| Animated / heard tests | `MainForm.HasAnimation`, `IsAnimated`, the `Heard` test (`MainForm.cs` ~3003, 3052, 3112) |
| Cutout / Upscale | `MainForm.WaitForCutoutsAsync`, `EnsureUpscaled`, the upscale routes (~4060, 4127) |
| Undo history | `GridState.Images`, `GridHistory.Holds` / `Release` |
| Export lock | `GridPreview.SuspendPlayback` / `ResumePlayback` close and reopen its reader too |

- **Muted on arrival** (Q&A 11): a background video arrives with its Volume effect **on at 0 %** — its
  default state, which its Volume Reset and the effects toolbar's Reset in the edit view bring back; the
  background's exception to § The Volume Exception's *a video arrives heard*. Turned up in the edit view,
  it is heard in the mix like a layer's.
- **Starts over** like a cell's content: an image arriving as the background — drop or Browse — starts
  the grid over (§ Preview Playback); its starting point and trim are its Frames effect's.

---

## Test Impact

There is no test project (CONTRIBUTING.md): the change is checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no unit tests) | — | — |

---

## Open Questions

- [ ] **An Animations motion on a still background image** does not move between redraws (Iteration 5): drive it
  by the progress lines' timer, as the cells' motions?
- [x] ~~Grid background only, or the cell's Background too?~~ → Grid background only (Q&A 1)
- [x] ~~How is the image fitted?~~ → Fill / Contain / Stretch / Tile thumbnails, a zoom and a position,
  the edit view by double-click (Q&A 2)
- [x] ~~Where are files dropped?~~ → onto the Grid background options (Q&A 3)
- [x] ~~Image with its own look, or dedicated options only?~~ → its own `ImageLook`, like a layer (Q&A 5)
- [x] ~~Where is the double-click?~~ → on the thumbnail in the options (Q&A 6)
- [x] ~~Which contents?~~ → stills, GIFs and videos (Q&A 7)
- [x] ~~**Tile**: the tile's size — a **Tile size** slider (a share of the canvas's longer side, 10 to 100 %)
  set apart from the zoom, or the image's zoom read as the tile's scale? Proposed: the zoom is the
  tile's scale (100 % = the image fitted Contain into the canvas), no new slider.~~ → The zoom is the tile's scale (Q&A 9)
- [x] ~~**Stretch and Tile in the edit view**: the one cell shows the image as Contain does, the
  placement applied only once back in the grid — or the edit view draws them too? Proposed: shown as
  Contain, with a *Stretch* / *Tile* badge, the effects being edited on the image itself.~~ → Shown as Contain with a badge (Q&A 10)
- [x] ~~**A background video's sound**: heard by default like any video (the Volume effect's default), or
  muted by default since it is a background? Proposed: muted by default (its Volume effect off and at
  0 %), the user turning it up in the edit view.~~ → Muted by default (Q&A 11)
- [x] ~~**A grid holding the background image only**: empty (Copy and Save disabled, as today) or content
  of its own (exported at the canvas size `ForLayers` would give the image alone)? Proposed: empty — a
  background is under something.~~ → Content of its own, the canvas sized on the image (Q&A 12)
- [x] ~~**An effect preset tile dropped** onto the options: arrives with its effects
  (`FileExplorerPanel.PresetLookOf`), as into a cell? Proposed: yes.~~ → Yes, with its effects (Q&A 13)
- [x] ~~**Paste**: a **Paste** button (and Ctrl+V while the Grid background options have the focus) for an
  image on the clipboard? Proposed: a Paste button only, beside Browse…; Ctrl+V keeps filling the cells.~~ → A Paste button only (Q&A 14)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-04

Initial design from the user's request (*Image de fond dans background grid — possibilité de drag & drop
depuis l'explorateur*) and the scoping answers: a background image member of the Grid background, a
floating cell with its own look like a layer, fitted by Fill / Contain / Stretch / Tile thumbnails, dropped
into or browsed from the Grid background options, edited in a background edit view generalised from the
layer edit view, playing and heard as a third source of contents beside the cells and the layers.
Scouted in the code: `GridBackground` / `MainForm.GridBackground.cs` (state, options, reset),
`GridPreview.Layers.cs` (`EditState`, `BeginLayerEdit`), `MainForm.DroppedOnLayers` (global rows' drop),
and the places the layers are fed in (player, export, length, sound, history).

### Iteration 2 — 2026-11-04

The six open questions answered (Q&A 9–14): the image's zoom is the Tile's scale; Stretch and Tile shown
as Contain with a badge in the edit view; a background video muted on arrival; a grid holding the
background image only is content of its own, its canvas sized on the image; a preset tile dropped arrives
with its effects; a Paste button beside Browse…. Sections *Drawing*, *Fitting*, *Options*, *Drop and
Browse*, *Background Edit View* and *Playback* updated.

### Iteration 3 — 2026-11-04 — ✅ Implemented

Go given: code, unit tests and documentation, in the worktree `.claude/worktrees/grid-background-image` on
the branch `feature/grid-background-image`. No test project: the unit-test step does not apply.

### Iteration 4 — 2026-11-04 — ⚙️ Post-implementation — Click on the thumbnail loads an image

Requested by the user while testing the run's first build (*il faut en plus du drag & drop et du « coller »,
cliquer sur la miniature pour charger une image*): a click on the image thumbnail opens the file picker, as
Browse… does, beside the drop and Paste. The double-click keeps opening the edit view: on a thumbnail holding
an image, the click waits for the double-click time before opening the picker. § Options updated.

### Iteration 5 — 2026-11-04 — 🧭 Implementation choices

What the run decided on its own, beyond the frozen design:

- ⚠️ **Rule broken — SHORTCUTS.md / .fr.md in the same commit as the code** (RULES.md § Keyboard Shortcuts): the
  thumbnail's click and double-click went in with the code (`471b4c1`), their rows in the docs commit (`cc8444a`).
  Why: the docs were written as one pass at the end of the run. Autonomous run: reported instead of asked.
- **Stretch and Tile in the edit view** (Q&A 10): the cell takes the image's own shape rather than showing it as
  Contain inside the canvas's shape — the closest workable variant: the image shows whole either way, and its bars
  and zoom then act on the image alone, not on a canvas it does not fill.
- **An effect preset dropped**: its look is taken, its Background turned off when the preset left it at its
  default (a preset only saves a Background turned off), so the fill keeps showing.
- **A replaced image keeps the opacity** of the one it replaces — the Grid background's option, not the image's
  look; the look is the new image's default.
- **Fill and Contain recenter the image** when pressed, as the Zoom effect's fit modes do (RULES.md § Zoom Keeps
  the Image in Place).
- **Tile**: the image is drawn once into a tile bitmap per draw, at least 8 px a side, then repeated over what the
  graphics' clip shows.
- **Icons**: Browse… 🖼️ (`UiIcons.AddImage`, Add image…'s), Paste 📋 (`UiIcons.Copy`, the layers' Paste), Edit ✏️ /
  Done ✅, Remove ❌ (`UiIcons.DeleteLayer`).
- **A grid holding the background image only** is not the empty grid: the canvas's click no longer opens the Add
  images picker there, as for a grid of layers only.
- **A playing background** draws the whole canvas again at each of its frames, fast while the grid runs, in full
  once it pauses (`RedrawAnimatedInFull`).
- **Not covered**: an Animations motion on a *still* background image moves only when the preview is drawn again
  for another reason — the progress lines' timer redraws the cells only. Offered as an open question.
- **Not verified in the running app beyond start-up**: the options row was checked on screen; the drop, Paste, the
  placements, the edit view, playback and exports were not driven by the agent — the user's desktop was in use.

### Iteration 6 — 2026-11-04 — ⚙️ Post-implementation — No double-click on the thumbnail, Browse… beside Paste

Requested by the user after testing (*le double clic sur la miniature est dangereux car le simple clic sert à
changer — mettre un bouton à côté de « coller » pour choisir une image*): the thumbnail's click changes the image,
so a double-click there risks the picker. The double-click is dropped — the edit view opens from **Edit** only —,
the click opens the picker at once (no wait), and the button choosing an image stands right after **Paste**, with
its text **Browse…** beside its icon. § Options updated; Iteration 4's double-click wait undone.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | Iterations 3, 4 | 2026-11-04 | `fad6f58` model and drawing · `b94ddc2` preview · `5609ab7` window, history, exports · `471b4c1` click on the thumbnail |
| Unit tests | Iteration 3 | 2026-11-04 | Does not apply: no test project |
| RULES.md | Iteration 3 | 2026-11-04 | `0ebf9c1` § Grid Background Image (under § Global Effects), § The Shared Fill |
| GLOSSARY.md / .fr.md | Iteration 3 | 2026-11-04 | `cc8444a` *Grid background*, *Background image*, *Background edit view* |
| README.md / .fr.md | Iterations 3, 4 | 2026-11-04 | `cc8444a` § Grid background › its image |
| SHORTCUTS.md / .fr.md | Iteration 4 | 2026-11-04 | `cc8444a` the thumbnail's click and double-click, Escape closing the background edit view |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where is the background image available — Grid background and cell Background, or Grid background only? | Grid background only | 2026-11-04 |
| 2 | How does the image fill the grid — Fill / Contain / Stretch / Tile, Fill only, Fill + zoom / position? | Fill / Contain / Tile choice + zoom / position + a double-click to enter an effects edit mode (notably to crop / cut out) | 2026-11-04 |
| 3 | Where can an image be dropped — the Grid background tab, its options, the visible background of the grid? | The Grid background options | 2026-11-04 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single scout pass | 2026-11-04 |
| 5 | The image as a canvas-sized floating cell with its own `ImageLook`, like a layer, every cell effect applying, Tile an option of the Grid background — or dedicated options only? | Like a layer | 2026-11-04 |
| 6 | Where is the double-click opening the edit view — the options' thumbnail, the background in the preview? | The options' thumbnail | 2026-11-04 |
| 7 | Which contents — stills, GIFs and videos, or stills only? | Stills, GIFs and videos | 2026-11-04 |
| 8 | Split the backlog row *Chosen image fill*: its Grid background half marked by this workfile, its cell half left *To design*? | Yes, split | 2026-11-04 |
| 9 | Tile: the image's zoom as the tile's scale, or a Tile size slider of its own? | The image's zoom | 2026-11-04 |
| 10 | Stretch and Tile in the edit view: shown as Contain with a badge, or drawn as placed? | As Contain with a badge | 2026-11-04 |
| 11 | A background video's sound: muted by default, or heard like any video? | Muted by default | 2026-11-04 |
| 12 | A grid holding the background image only: empty, or content of its own? | Content of its own | 2026-11-04 |
| 13 | An effect preset tile dropped onto the options: with its effects? | Yes, with its effects | 2026-11-04 |
| 14 | Paste: a Paste button beside Browse…, or also Ctrl+V in the options? | A Paste button only | 2026-11-04 |

---

*Last updated: 2026-11-04*
