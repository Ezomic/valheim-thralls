"""
Paints the rendered silhouettes into finished package icons.

    python tools/package_compose.py

Takes mask_*.png from package_silhouette.py - whose alpha channel is the altar's exact
outline - and lays it in bone on stone with the wordmark under it. Nothing here invents a
shape; the only decisions are colour, scale and where the type sits.
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "previews", "package")

SIZE = 256
SS = 4
S = SIZE * SS

STONE_DARK = (24, 20, 16)
STONE_MID = (46, 39, 31)
BONE = (232, 223, 200)
BRASS = (198, 158, 88)

FONTS = ["C:/Windows/Fonts/constanb.ttf", "C:/Windows/Fonts/cambriab.ttf",
         "C:/Windows/Fonts/georgiab.ttf", "C:/Windows/Fonts/timesbd.ttf"]


def font(px):
    for p in FONTS:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, px)
            except Exception:
                continue
    return ImageFont.load_default()


def silhouette(name, colour):
    """The mask painted in one flat colour, cropped to its own ink."""
    mask = Image.open(os.path.join(OUT, "mask_%s.png" % name)).convert("RGBA")
    alpha = mask.split()[3]

    box = alpha.getbbox()
    if box:
        alpha = alpha.crop(box)

    flat = Image.new("RGBA", alpha.size, colour + (0,))
    flat.putalpha(alpha)
    return flat


def posterised(name, tones):
    """
    The shaded render cut into a few flat bands.

    A solid silhouette is a bold mark and tells you nothing - "no idea what it is" was
    the note on it. Banding the shading keeps the flat, graphic look while giving the
    altar its horns, its crown slab, its stepped base and the ring on its face, because
    those are all separate planes catching separate amounts of light.

    Four bands, not a gradient. Valheim's own art posterises rather than blends, and a
    gradient at 64px turns back into mush.
    """
    img = Image.open(os.path.join(OUT, "shade_%s.png" % name)).convert("RGBA")
    alpha = img.split()[3]

    box = alpha.getbbox()
    if box:
        img = img.crop(box)
        alpha = alpha.crop(box)

    grey = img.convert("L")

    # The band edges are picked off the ink rather than fixed, so a dark model and a
    # bright one both use the whole ramp.
    ink = [p for p, a in zip(grey.getdata(), alpha.getdata()) if a > 8]
    if not ink:
        return silhouette(name, tones[-1])

    ink.sort()
    cuts = [ink[int(len(ink) * f)] for f in (0.25, 0.55, 0.82)]

    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    op = out.load()
    gp = grey.load()
    ap = alpha.load()

    for y in range(img.height):
        for x in range(img.width):
            a = ap[x, y]
            if a <= 8:
                continue
            v = gp[x, y]
            band = 0 if v < cuts[0] else 1 if v < cuts[1] else 2 if v < cuts[2] else 3
            op[x, y] = tones[band] + (a,)

    return out


def fit(img, width, height):
    """Scale to fit inside a box, keeping the aspect."""
    scale = min(width / float(img.width), height / float(img.height))
    return img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))),
                      Image.LANCZOS)


def compose(out_name, mask_name, colour, text, ground=STONE_DARK, keyline=True,
            tones=None):
    base = Image.new("RGBA", (S, S), ground + (255,))
    d = ImageDraw.Draw(base)

    if keyline:
        # Thunderstore lays icons on a pale card; a dark square with no edge bleeds into
        # its own shadow.
        inset = 9 * SS
        d.rectangle([inset, inset, S - inset, S - inset], outline=STONE_MID, width=3 * SS)

    art = posterised(mask_name, tones) if tones else silhouette(mask_name, colour)

    if text:
        art = fit(art, S * 0.74, S * 0.56)
        base.alpha_composite(art, (int((S - art.width) / 2), int(S * 0.11)))

        f = font(int(40 * SS))
        box = d.textbbox((0, 0), text, font=f)
        d.text((S / 2 - (box[2] - box[0]) / 2 - box[0], S * 0.755), text, font=f,
               fill=colour)
    else:
        art = fit(art, S * 0.80, S * 0.80)
        base.alpha_composite(art, (int((S - art.width) / 2), int((S - art.height) / 2)))

    path = os.path.join(OUT, out_name + ".png")
    base.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS).save(path)
    print("THRALLS_ICON %s" % path)


# Darkest to lightest. The bottom band is only a little above the ground so the mark
# still has a clean outer edge instead of dissolving into the square.
TONES_BONE = [(58, 50, 40), (120, 110, 92), (186, 176, 152), BONE]
TONES_WARM = [(56, 44, 30), (116, 92, 56), (170, 138, 84), BRASS]


def main():
    compose("sil_altar_word", "altar", BONE, "THRALLS")

    compose("det_altar_word", "altar", BONE, "THRALLS", tones=TONES_BONE)
    compose("det_altar_plain", "altar", BONE, None, tones=TONES_BONE)
    compose("det_altar_warm", "altar", BRASS, "THRALLS", tones=TONES_WARM)
    print("THRALLS_ICON_DIR %s" % OUT)


if __name__ == "__main__":
    main()
