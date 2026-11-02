"""Makes src/ImageGridFusion/Imaging/TwemojiFlags.ttf, the flag font of the texts.

Twemoji Mozilla (COLRv0) cut down to its flags — the regional indicators and their pairs, the black flag with
the subdivision flags and the pirate flag — then:
- each color glyph's own outline filled with its layers' outlines: the font's are empty, and the app draws a
  text's outline and shadow from the glyphs without their colors;
- the subdivision flags and the pirate flag given private code points, U+E000 to U+E003 (JOINED below), which
  the app draws in place of their sequences: DirectWrite never joins those sequences, their tags and VS16 kept
  out of the font's ligatures (Imaging/Emojis.cs, Joined).

Usage: python make-flag-font.py <Twemoji.Mozilla.ttf> <TwemojiFlags.ttf>
Needs fontTools (pip install fonttools). See CONTRIBUTING.md § Flag Font.
"""

import sys

from fontTools import subset
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

# The regional indicators, the black flag, the tags, and the joiner, skull and VS16 of the pirate flag.
UNICODES = [*range(0x1F1E6, 0x1F200), 0x1F3F4, *range(0xE0020, 0xE0080), 0x200D, 0x2620, 0xFE0F]

# The private code points of the sequences DirectWrite does not join, each after the black flag: England,
# Scotland, Wales (tag letters, then the cancel tag), the pirate flag (joiner, skull, VS16).
JOINED = {
    0xE000: [0xE0067, 0xE0062, 0xE0065, 0xE006E, 0xE0067, 0xE007F],
    0xE001: [0xE0067, 0xE0062, 0xE0073, 0xE0063, 0xE0074, 0xE007F],
    0xE002: [0xE0067, 0xE0062, 0xE0077, 0xE006C, 0xE0073, 0xE007F],
    0xE003: [0x200D, 0x2620, 0xFE0F],
}


def main(source: str, target: str) -> None:
    font = TTFont(source)
    options = subset.Options()
    options.layout_features = ["*"]
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=UNICODES)
    subsetter.subset(font)

    glyphs = font.getGlyphSet()
    glyf = font["glyf"]
    for base, layers in font["COLR"].ColorLayers.items():
        pen = TTGlyphPen(glyphs)
        for layer in layers:
            glyphs[layer.name].draw(pen)
        glyf[base] = pen.glyph()

    cmap = font.getBestCmap()
    ligatures = {}
    for lookup in font["GSUB"].table.LookupList.Lookup:
        for table in lookup.SubTable:
            for ligature in getattr(table, "ligatures", {}).get(cmap[0x1F3F4], []):
                ligatures[tuple(ligature.Component)] = ligature.LigGlyph
    for code, sequence in JOINED.items():
        glyph = ligatures[tuple(cmap[c] for c in sequence)]
        for table in font["cmap"].tables:
            if table.isUnicode():
                table.cmap[code] = glyph

    font.save(target)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
