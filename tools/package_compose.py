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


def fit(img, width, height):
    """Scale to fit inside a box, keeping the aspect."""
    scale = min(width / float(img.width), height / float(img.height))
    return img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))),
                      Image.LANCZOS)


def compose(out_name, mask_name, colour, text, ground=STONE_DARK, keyline=True):
    base = Image.new("RGBA", (S, S), ground + (255,))
    d = ImageDraw.Draw(base)

    if keyline:
        # Thunderstore lays icons on a pale card; a dark square with no edge bleeds into
        # its own shadow.
        inset = 9 * SS
        d.rectangle([inset, inset, S - inset, S - inset], outline=STONE_MID, width=3 * SS)

    art = silhouette(mask_name, colour)

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


def main():
    compose("sil_altar_word", "altar", BONE, "THRALLS")
    compose("sil_altar_plain", "altar", BONE, None)
    compose("sil_both_word", "both", BONE, "THRALLS")
    compose("sil_altar_brass", "altar", BRASS, "THRALLS")
    print("THRALLS_ICON_DIR %s" % OUT)


if __name__ == "__main__":
    main()
