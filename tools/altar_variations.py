"""
Renders small variations of the shipped bindstone side by side, at eye height.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/altar_variations.py

Every variation is build_bindstone with different keyword arguments - there is no second
copy of the builder here on purpose. A mockup that reimplements the shipped shape drifts
away from it, and then the thing being judged is not the thing that ships.

OUT_DIR and PREVIEW_DIR are both redirected into assets/previews/variations, so a run
cannot overwrite a shipped model, texture or icon.
"""

import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy
import altar_model as am

OUT = os.path.join(am.ROOT, "assets", "previews", "variations")
am.OUT_DIR = OUT
am.PREVIEW_DIR = OUT

# Eye height, close enough to read the crown but far enough to see the whole silhouette.
EYE = (0.95, -2.65, 1.60)
LOOK = 0.88
SIZE = 460

# The ten, plus the shipped altar first for reference. Each changes one thing, so they
# can be mixed rather than picked whole.
# Charm treatments only, cropped tight - the thing hanging off the horn has now been
# wrong three different ways, so it gets judged on its own instead of at altar scale.
CHARMS = [
    ("c0_pail",    "as it ships (reads as a pail)", dict()),
    ("c1_cluster", "cluster, splayed",              dict(charms="cluster")),
    ("c2_bundle",  "bundle on separate cords",      dict(charms="bundle")),
    ("c3_tooth",   "one heavy tooth",               dict(charms="tooth")),
    ("c4_none",    "nothing on the horns",          dict(charms="none")),
]

VARIATIONS = [
    ("00_shipped",      "as it ships",            {}),
    ("01_bone_charms",  "bone cluster charms",    dict(charms="cluster")),
    ("02_no_charms",    "no charms",              dict(charms="none")),
    ("03_no_runes",     "no runes, ring alone",   dict(runes="none")),
    ("04_bold_runes",   "two bold runes",         dict(runes="bold")),
    ("05_rough_kerb",   "broken kerb, no courses", dict(kerb="rough")),
    ("06_skewed",       "body pushed off square", dict(body_skew=0.16)),
    ("07_steep",        "steeper crown (34 deg)", dict(tilt=34.0)),
    ("08_shallow",      "shallower crown (18)",   dict(tilt=18.0)),
    ("09_one_horn",     "one horn, one stump",    dict(horn_mode="single")),
    ("10_big_ring",     "wider binding ring",     dict(ring_radius=0.295)),
]


def shoot(name, altar, textures):
    """Dress and light through the shipped path, then one eye-height frame."""
    am.render_preview(name, altar, textures, shots=False)

    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("var_cam")
    cam = bpy.data.objects.new("var_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = EYE

    target = bpy.data.objects.new("var_target", None)
    bpy.context.collection.objects.link(target)
    target.location = (0.0, 0.0, LOOK)

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    scene.camera = cam
    scene.render.resolution_x = SIZE
    scene.render.resolution_y = SIZE
    scene.render.film_transparent = False

    path = os.path.join(OUT, "%s.png" % name)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.objects.remove(target, do_unlink=True)
    return path


def main():
    os.makedirs(OUT, exist_ok=True)

    wanted = CHARMS if "--charms" in sys.argv else VARIATIONS

    for name, label, kwargs in wanted:
        # The same seed every time, or the jitter alone would make two variations look
        # different for reasons that have nothing to do with what is being compared.
        am.random.seed(20260810)
        am.clear_scene()

        parts = am.build_bindstone(**kwargs)

        kinds = []
        for obj in parts:
            kind = obj.get("thralls_surface")
            if kind is not None and kind not in kinds:
                kinds.append(kind)

        altar = am.finish(parts, name)
        am.bake_occlusion(altar)
        textures = am.make_surface_textures(name, kinds) if kinds \
            else {None: am.make_texture(name)}

        path = shoot(name, altar, textures)
        print("THRALLS_VARIATION %s | %s | verts=%d | %s"
              % (name, label, len(altar.data.vertices), path))


if __name__ == "__main__":
    main()
