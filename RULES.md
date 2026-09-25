# Agent Rules — Image Grid Fusion

Rules every change to this app follows. Vocabulary: see [GLOSSARY.md](GLOSSARY.md).

## Effects

Apply to **every effect**, the blur being the first one
(origin: `workfiles/20260925-blur-mask.md`).

### Scope and State

- An effect belongs to a **cell + image** pair. Its state lives on the image's immutable state,
  next to `ImageLook`, and is **not persisted**.
- Its geometry is stored in **fractions of the cell**, so it survives resizing and layout changes.

| Event | Effects of the cells concerned |
|---|---|
| The cell's image is replaced (drop, Ctrl+V, browse) | Reset — none active |
| An image is deleted | Reset for the images that shift into another cell |
| Two cells are swapped | Kept — they follow the image, like rotation and zoom |
| The layout changes | Kept |
| The effects toolbar's *Reset* button is clicked | Removed, all at once |

### Effects Toolbar

- An **always-visible row below the top bar**: an **Effects** label, one toggle button per effect,
  then a **Reset** button (not an effect). Every action on the image is an effect: the cell itself
  only keeps the **×**, the **✥** swap handle, and the wheel and drag gestures (origin:
  `workfiles/20260925-toolbar.md`).
- An effect that does not apply to the selected image (e.g. Frames on a still image) has its button
  **disabled**.
- A button is **pressed** when its effect is active on the **selected cell**. With no cell
  selected, the toolbar stays visible but **disabled**.
- Clicks:

  | Effect state on the selected cell | Click does |
  |---|---|
  | Inactive | Activates it with its defaults and selects it |
  | Active, not selected | Selects it |
  | Active and selected | Deactivates it, restoring its defaults |

- The **selected effect** belongs to the toolbar, not to the cell: when another cell is selected
  and the effect is active on it, it stays selected; otherwise its button is released.

### Options Toolbar

- A row below the effects row, holding the **selected effect's options only**; hidden when no
  effect is selected or no cell is selected.

### On-Cell Handles

- An effect's handles (e.g. the blur bars) are drawn on the **selected cell** only, while that
  effect is selected.
- They take priority over the cell's other gestures on their own hit area only.
- A handle positioned relative to a cell edge **snaps exactly onto it** within 6 logical px
  (scaled with `LogicalToDeviceUnits`), so no 1–2 px strip is left along the edge.

### Rendering

- Every effect is applied in `Compositor.DrawCell`, so the preview, the exports and video
  playback all show it from one place.
- Its strength is **resolution-independent** (relative to the cell size), so the preview and an
  export at another size look the same.

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
