# Duplicate a Layer

> Working document — duplicating a layer of the Layers global effect: a button of the layer list, a
> context menu on the layer, and Ctrl + drag.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The Layers global effect (workfiles/20261029-layers.md) lays layers over the grid. A layer can be
added, edited, deleted and restacked, but not duplicated: laying the same sticker or text twice means
adding it again and redoing its effects, size, angle and style by hand.

This workfile adds **Duplicate**: a copy of the selected layer — its content, its cell effects, its
place, its text and style — laid just above it, a little off so it shows, and selected.

Components concerned:

| Component | Role |
|---|---|
| `Composition/GridLayers.cs` | `GridLayers` — a layer inserted at an index (`Inserting`) |
| `UI/GridPreview.Layers.cs` | Inserting the copy selected; the Ctrl + drag gesture; the right click on a layer |
| `UI/MainForm.Layers.cs` | The **Duplicate** button, the copy's content made anew, the context menu |
| `UI/LayerList.cs` | The right click on a row |
| `UI/EffectIcons.cs` | The `Duplicate` icon of `LayerAction` |
| `README.md` / `README.fr.md`, `GLOSSARY.md` / `GLOSSARY.fr.md` | The layer list's buttons, the gestures on a layer |

---

## The Copy

- **What is copied**: everything a `Layer` holds — its content, its `ImageLook` (every cell effect, the
  Frames trim and starting point included), its `LayerPlacement` (size, angle; the center moved, below),
  its text, its `LayerTextStyle` and its `TextBox`, its visibility (see Open Questions).
- **Its name** in the layer list is the **original's**: the list shows the file's name or the text's first
  line, so the copy needs nothing of its own for it (Q&A 3).
- **Its own content**: a `SourceImage` carries its look (`SetLayers` writes `layer.Image.Look`) and, for a
  GIF, a video or a PDF, its own reader (`PageSource`). Two layers never share one: the copy's content is
  made anew, by its kind:

  | Original | The copy's content |
  |---|---|
  | A text layer | Drawn again — `MainForm.RenderTextLayer(text, style, look, place, box)` |
  | A content with a file (`FilePath`) — image, GIF, video, PDF, text file, URL download | Loaded again from its file — `ImageLoader.TryLoadFile`, off the UI thread, as `AddLayerFilesAsync` does — then given the original's look; `TextOrigin` and `Dropped` copied |
  | A pasted image, without a file | Its bitmap copied — `ImageLoader.FromImage(layer.Image.Bitmap)` |

- **It plays alongside the original**: same look, same Frames, same clock — a video copy shows the same
  frame as its original at every instant.

## Where the Copy Goes

- **Just above its original** in the stacking order (`index + 1`), never on top of everything (Q&A 2):
  `GridLayers.Inserting(int index, Layer layer)`, a new list as every change.
- **A little off**, so it shows: its center moved by `LayerCascadeStep` (0.04 of the grid, the step the
  layers added together already take) to the right and down. Where that would leave the grid — its center
  clamped to [0, 1] — the copy is moved the other way on that axis, so it never lands on its original.
- **Selected** once laid, the original no longer is; the Layers turned on (acting on the Layers options
  turns them on — RULES.md § Global Toolbar).
- **One undo step**, covered by itself (`GridHistory` commits once the state settles); the content made
  anew goes through `GridPreview.ReleaseImage` when an undo takes the copy away, like every content leaving
  the layers.

## Ways to Duplicate

Three ways, chosen by the user (Q&A 1); no keyboard shortcut.

### The Layer List's Button

- A **Duplicate** icon button — two overlapping rectangles — among the list's buttons, after **Add text**
  and before **Edit**: Add image…, Paste, Add text, **Duplicate**, Edit, Delete, To back, Lower, Raise, To
  front.
- Tooltip: *Duplicate — lays a copy of the selected layer just above it, a little off*.
- Enabled as **Delete** is: a layer listed as selected (`ListedLayer`), no export running.

### The Context Menu

- A **right click on a layer** — on the working surface while the layers take the mouse
  (`LayersTakeMouse`), or on its row of the layer list — selects it, then opens a menu holding
  **Duplicate** (its other items: see Open Questions).
- Over a layer, it replaces the cell's menu (*Find similar images*) the right click shows today: the
  layers take the mouse on their own area only, the right click included.

### Ctrl + Drag

- A press with **Ctrl** held on the **selected layer**, then a drag: the layer **leaves a copy at its
  starting place** and moves on with the mouse, as in the image editors.
- The layer moved is the **original** — it keeps the selection and the drag runs on unchanged; the copy
  left behind is inserted **just under it**, at the original's index, so the moved one stays on top of it.
  Both bear the same name and the same look: nothing tells them apart, and the result is the one the user
  expects — a layer where the drag started, its twin where it ended.
- The copy is made **when the drag starts** (the first move past the drag threshold with Ctrl held), not
  at the press: a Ctrl + click alone copies nothing. Ctrl released during the drag changes nothing.
- The whole gesture is **one undo step**: `InGesture` holds until the mouse is released **and** the copy's
  content has arrived (a file reloaded off the UI thread).
- Ctrl over the selected layer shows the **copy cursor** (`Cursors.SizeAll` with a +, or the system's
  drag-copy cursor), so the gesture can be found.

---

## Test Impact

There is **no test project** in this repository (CONTRIBUTING.md: a change is checked by hand in the
running app), so no unit test is created or updated. The behaviours below are checked by hand at the end
of the run:

| Behaviour to check | How |
|---|---|
| The copy lies just above its original, off by 4 %, selected | Duplicate a layer under another one; read the layer list |
| A copy at the grid's edge moves the other way | Duplicate a layer whose center lies on the right edge |
| Each kind gets its own content | Duplicate an image, a GIF, a video, a text, a pasted image; change an effect on the copy, the original unchanged |
| The context menu, on the surface and in the list | Right click a layer in both places |
| Ctrl + drag leaves a twin at the starting place, one undo step | Ctrl + drag a layer, then Ctrl+Z once |
| An undo takes the copy away, a redo brings it back | Duplicate, Ctrl+Z, Ctrl+Y |

---

## Open Questions

- [ ] A **video with sound** duplicated: are both heard — the sound doubled — or is the copy muted (its
  Volume effect on, at 0)?
- [ ] A **hidden layer** duplicated (its checkbox unchecked in the list): is the copy hidden like it, or
  visible?
- [ ] A layer whose **file is gone or unreadable** since it was added: is a still copy made of its current
  frame, or is the duplicate refused, the status line saying why?
- [ ] The **context menu**: Duplicate alone, or also the list's other actions on the layer (Edit, Delete,
  To back, Lower, Raise, To front)?
- [ ] On a layer showing an image, does the context menu also offer **Find similar images**, as the cell's
  menu does?
- [ ] **Ctrl + drag on a layer not selected**: a press on it only selects it today (§ Layers, *selected
  first*). Does Ctrl + drag on it select it and copy it at once, or does it keep the rule — select first,
  then Ctrl + drag?
- [ ] While the **layer edit view** is open: is Duplicate available — the view closing first, as any change
  of the grid from outside closes it — or disabled until the view is closed?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-29

Initial design, from the scoping answers (Q&A 1 to 4) and a single scout pass over the layers' code:

- Three ways to duplicate — the layer list's button, a context menu on the layer, Ctrl + drag — no
  shortcut.
- The copy goes just above its original, off by `LayerCascadeStep`, selected, under the original's name.
- Its content is made anew by kind (text drawn again, file reloaded, pasted bitmap copied), since a
  `SourceImage` carries its look and its reader and cannot be shared by two layers.
- Ctrl + drag moves the original on and leaves the copy under it at the starting place, one undo step.
- No test project: the checks are listed for a manual pass.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Does not apply: no test project in the repository |
| README | | | `README.md` and `README.fr.md`, § Layers |
| Glossary | | | `GLOSSARY.md` and `GLOSSARY.fr.md`, *Layer list* and *Layer* rows |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | By which ways can a layer be duplicated? (list button, Ctrl+D, context menu, Ctrl + drag) | The list's button, the context menu and Ctrl + drag — no Ctrl+D | 2026-10-29 |
| 2 | Where does the copy go? | Just above its original, a little off, selected | 2026-10-29 |
| 3 | What name does the copy bear in the layer list? | The original's name | 2026-10-29 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward — a single scout pass | 2026-10-29 |
| 5 | A video with sound duplicated: both heard, or the copy muted? | | |
| 6 | A hidden layer duplicated: the copy hidden, or visible? | | |
| 7 | A layer whose file is gone: still copy of its frame, or refused? | | |
| 8 | The context menu: Duplicate alone, or the list's other actions too? | | |
| 9 | The context menu on an image layer: Find similar images too? | | |
| 10 | Ctrl + drag on a layer not selected: copy at once, or select first? | | |
| 11 | In the layer edit view: Duplicate available, or disabled? | | |

---

*Last updated: 2026-10-29*
