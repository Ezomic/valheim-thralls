"""
Draws the Thunderstore package icon as a mark rather than a photograph of the altar.

    python tools/package_logo.py

Why a mark. The 3D renders read as "here is a screenshot of a piece", which is what a
gallery of Valheim mods is already full of, and at the ~64px thumbnail Thunderstore shows
in a list the modelling turns to mush. A flat emblem with two or three tones survives that
size, which is the only size most people will ever see it at.

The mark is not invented: the binding ring is already cut into the bindstone's own face and
the horns already stand over it, so this is the mod's existing art reduced to its outline.

Drawn at 4x and downsampled, which is the whole trick for clean edges - PIL has no
antialiasing on its primitives, so a circle drawn straight at 256 comes out with stair
steps on every diagonal.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "previews", "package")

SIZE = 256
SS = 4                      # supersample factor
S = SIZE * SS

# The mod's own palette, taken off the altar panel so the icon and the UI agree.
STONE_DARK = (24, 20, 16)
STONE_MID = (46, 39, 31)
BONE = (232, 223, 200)
BONE_DIM = (168, 158, 136)
BRASS = (198, 158, 88)

FONT_CANDIDATES = [
    "C:/Windows/Fonts/constanb.ttf",   # Constantia Bold - humanist, close to Valheim's UI
    "C:/Windows/Fonts/cambriab.ttf",
    "C:/Windows/Fonts/georgiab.ttf",
    "C:/Windows/Fonts/timesbd.ttf",
]


def font(px):
    for path in FONT_CANDIDATES:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, px)
            except Exception:
                continue
    return ImageFont.load_default()


def ground(d, colour, edge=True):
    d.rectangle([0, 0, S, S], fill=colour)
    if not edge:
        return
    # A thin inset keyline. Thunderstore puts icons on a pale card, and a dark square with
    # no edge bleeds into its own drop shadow.
    inset = 10 * SS
    d.rectangle([inset, inset, S - inset, S - inset], outline=STONE_MID, width=3 * SS)


def ring(d, cx, cy, radius, thickness, colour):
    """
    The binding ring. No spokes.

    Spokes were the first attempt, taken from the sigil cut into the bindstone's face,
    and six of them turn a ring into a ship's wheel - which is what the first draft
    looked like at any size. A plain heavy annulus reads as iron.
    """
    d.ellipse([cx - radius, cy - radius, cx + radius, cy + radius],
              outline=colour, width=thickness)


def link(base, cx, cy, length, width, thickness, colour, angle):
    """
    One chain link: a stadium outline, drawn on its own layer so it can be turned.

    PIL cannot rotate a primitive, and a chain of links all at the same angle is a
    ladder rather than a chain - real ones alternate about ninety degrees.
    """
    pad = int(length)
    layer = Image.new("RGBA", (pad * 2, pad * 2), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)

    x0 = pad - width / 2.0
    y0 = pad - length / 2.0
    ld.rounded_rectangle([x0, y0, x0 + width, y0 + length],
                         radius=width / 2.0, outline=colour, width=thickness)

    layer = layer.rotate(angle, resample=Image.BICUBIC, expand=False)
    base.alpha_composite(layer, (int(cx - pad), int(cy - pad)))


def chain(base, cx, cy, links, length, width, thickness, colour, step):
    """A run of links hanging, each turned across the one above it."""
    for i in range(links):
        link(base, cx, cy + i * step, length, width, thickness, colour,
             0 if i % 2 == 0 else 90)


def mark(base, d, cx, cy, scale=1.0, colour=BONE):
    """The whole emblem: a heavy ring with a short chain hanging out of it."""
    r = 66 * SS * scale
    t = int(19 * SS * scale)

    ring(d, cx, cy, r, t, colour)

    # Hung from the bottom of the ring and overlapping it, so the chain is attached
    # rather than floating underneath.
    chain(base, cx, cy + r + 26 * SS * scale, links=3,
          length=62 * SS * scale, width=40 * SS * scale,
          thickness=int(15 * SS * scale), colour=colour, step=int(46 * SS * scale))


def wordmark(d, text, cx, y, px, colour):
    f = font(px)
    box = d.textbbox((0, 0), text, font=f)
    d.text((cx - (box[2] - box[0]) / 2 - box[0], y), text, font=f, fill=colour)
    return box[3] - box[1]


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.resize((SIZE, SIZE), Image.LANCZOS).save(path)
    print("THRALLS_LOGO %s" % path)


def draw(name, colour, text, ground_colour=STONE_DARK):
    base = Image.new("RGBA", (S, S), ground_colour + (255,))
    d = ImageDraw.Draw(base)
    ground(d, ground_colour)

    mark(base, d, S / 2, S * (0.40 if text else 0.46), scale=1.0 if text else 1.15,
         colour=colour)

    if text:
        d = ImageDraw.Draw(base)
        wordmark(d, text, S / 2, S * 0.795, int(40 * SS), colour)

    save(base.convert("RGB"), name)


def main():
    draw("logo_chain", BONE, "THRALLS")
    draw("logo_chain_plain", BONE, None)
    draw("logo_chain_brass", BRASS, "THRALLS")
    print("THRALLS_LOGO_DIR %s" % OUT)


def _unused():
    pass


if __name__ == "__main__":
    main()
