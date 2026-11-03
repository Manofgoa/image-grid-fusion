# Feature Backlog — Image Grid Fusion

> Features identified but not designed yet.
> Each one gets its own workfile when its turn comes; it is then marked with that workfile's name.

| Feature | Raised in | Status |
|---|---|---|
| **Pause during Copy / export** — while a Copy or an export runs, pause every animation and sound of the preview | User request, 2026-10-09 | [20261009-playback-controls.md](20261009-playback-controls.md) |
| **Image / background gradient** — a slight gradient where the image meets its background, the image's edges fading into the fill instead of a hard cut | User request, 2026-10-09 | To design |
| **Saved project** — save a composition and open it again later: the cells' files and looks, the layout and its separators, the format, the global effects, the layers; nothing of it is persisted today | Backlog review, 2026-10-29 | To design |
| **Grid templates** — a layout, its separators, the format and the Borders kept under a name, applied again in one click | Backlog review, 2026-10-29 | To design |
| **Animated WebP / AVIF export** — much lighter than a GIF, with transparency | Backlog review, 2026-10-29 | To design |
| **More Animations types** — Ken Burns pan, slow rotation, pulse, beside the Zoom motion the Type drop-down offers alone | Backlog review, 2026-10-29 | To design |
| **Face-aware framing** — an arriving image and Fill framed on the faces the face engine finds, rather than on the image's center | Backlog review, 2026-10-29 | To design |
| **Color harmonization** — the white balance and exposure of every cell matched to a reference cell | Backlog review, 2026-10-29 | To design |
| **Shadow, vignette and rounded corners per cell** — set cell by cell, beside the Borders' grid-wide frame | Backlog review, 2026-10-29 | To design |
| **Shape layers** — arrow, rectangle, ellipse, highlighter, as a screenshot editor draws them, to annotate a capture | Backlog review, 2026-10-29 | To design |
| **Cutout to layer** — the cut-out subject sent to a new layer in one click, as a sticker | Backlog review, 2026-10-29 | To design |
| **Cascade transitions** — a crossfade between one turn and the next | Backlog review, 2026-10-29 | To design |
| **Visual fade** — the picture faded from and to black at the video's start and end; the Fade covers the sound only | Backlog review, 2026-10-29 | To design |
| **Alt text** — the captions of the grid's images assembled into an alt text, copied with the export | Backlog review, 2026-10-29 | To design |
| **Duplicates** — near-identical images of the index found through their vision embeddings, listed in a cleanup view | Backlog review, 2026-10-29 | To design |
| **AI upscale** — a small image enlarged by a model (Real-ESRGAN), downloaded on demand like the other engines | Backlog review, 2026-10-29 | [20261029-image-upscale.md](20261029-image-upscale.md) |
| **Shared fill and grid background** — one fill (a kind and its settings) drawn by one painter, used by the cell's Background and by a new global effect, **Grid background**, drawn under the cells: the gap, the Twitter corners, a grid of layers only. The fills below are kinds of it | Backlog review, 2026-10-29 | To design |
| **Gradient fills** — linear (an angle), radial, conic and mesh (a color per corner); their colors chosen or **Auto**, from the image's dominant colors (the grid's for the grid background), with a slight grain against banding | Backlog review, 2026-10-29 | To design |
| **Pattern fills** — dots, stripes, checkerboard, grid, hatching, zigzag: two colors, a scale, an angle | Backlog review, 2026-10-29 | To design |
| **Blurred image and mirror fills** — the image itself enlarged to cover the cell, heavily blurred and darkened (the whole grid blurred for the grid background); or its edges mirrored instead of stretched | Backlog review, 2026-10-29 | To design |
| **Chosen image fill** — a texture or a photo as the background, with a blur and an opacity | Backlog review, 2026-10-29 | To design |
| **Frosted glass fill** — a blur, a light tint and a thin edge line | Backlog review, 2026-10-29 | To design |
| **Palette fill** — bands of the image's dominant colors | Backlog review, 2026-10-29 | To design |
| **Stickers in layers** — ready-made stickers (transparent images, some animated) added as layers. Overlaps the favorites with effect presets: a file cut out by the Cutout and saved as a preset is already the user's own sticker — but a preset dropped onto the Layers loses its effects today, the first gap to close (its own row below). Libraries looked at: **GIPHY** stickers (an API key, a beta key at 100 calls an hour, production priced on request, attribution), **KLIPY** (free, ads among its results), **Tenor** shut down on 2026-06-30; offline packs shipped or downloaded: Microsoft **Fluent Emoji** (MIT), **Noto Animated Emoji** (CC BY 4.0), **OpenMoji** (CC BY-SA 4.0), **Twemoji** (CC BY 4.0, already the flag font's source). The emoji stickers of 2026-09-27 were cancelled for the text layers' emojis | User request, 2026-11-02 | To discuss |
| **Preset dropped onto the Layers keeps its effects** — a preset tile dropped into a cell arrives with its effects (`MainForm.OnDragDrop` passes `FileExplorerPanel.PresetLookOf` to `AddFilesAsync`), but dropped onto the Layers tab or its options it gets `Layer.DefaultLook`: `DropLayerFilesAsync` → `AddLayerFilesAsync` → `AddImageLayers` carry no look. Carry the preset's look down to the new layer; its Background stays off unless the preset has a `background` key (`LookPreset.FromJson` starts from `ImageLook.None`, Background on). The Cutout already works on a layer, so an image cut out and saved as a preset becomes a sticker. A small fix, plus RULES.md § Effect Presets ("into a cell" → or the Layers), the glossary and the READMEs in English and French | User request, 2026-11-03 | To design |
| **Emoji Kitchen layers** — two emojis combined into one sticker by Google's Emoji Kitchen (🐱 + 🌵), laid as a layer. No API: the valid pairs (100 000 and more) listed by the community's `metadata.json` (xsalazar/emoji-kitchen-backend on GitHub), a component of the Component updates window; each image a transparent PNG at a predictable `gstatic.com/android/keyboard/emojikitchen/<date>/…` address, downloaded on demand into a cache folder, never shipped, added as a dropped file would be. An **Emoji Kitchen…** button in the Layers options: a first emoji, then only the ones it pairs with. Maybe already possible by hand: a PNG's address loaded in the URL zone, dropped onto the Layers tab (not tested). No clear license — Google's images, said to be proprietary; Google may move or cut the addresses, as it did Tenor | User request, 2026-11-03 | To discuss |

---

*Last updated: 2026-11-03*
