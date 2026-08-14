"""
Builds the shipped depot - the tally mast - and exports it as OBJ, with its collision
sidecar, its surface textures, an icon and preview renders.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/depot_model.py

The four candidates and the argument for each are in depot_mockup.py; the mast was
picked because the depot governs a radius and a radius wants a landmark. Nothing else
the mod ships can be seen over a hedge.

Everything lands in assets/ as thrall_depot.obj, thrall_depot.col, thrall_depot_<surface>
.png and thrall_depot_icon.png, which is exactly what the mod's loader goes looking for
given DepotModel = thrall_depot.obj.
"""

import bpy
import math
import os
import random
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))

import altar_model as am
import depot_mockup as dm

# depot_mockup points the shared pipeline at the mockup folder the moment it is imported,
# because nothing in it is meant to ship. This is the shipping build, so both are put
# back - deliberately, after the import, rather than by making the mockup tidy up after
# itself, which would leave the order of two module-level assignments deciding where a
# shipped asset lands.
am.OUT_DIR = os.path.join(ROOT, "assets")
am.PREVIEW_DIR = os.path.join(ROOT, "assets", "previews")

# Writes thrall_depot.obj rather than thrall_altar_depot.obj: the piece is the depot,
# not a variety of altar.
am.PREFIX = "thrall"

NAME = "depot"


def main():
    random.seed(20260814)
    am.clear_scene()

    parts = dm.build_mast()

    col_path, col_count = am.write_colliders(parts, NAME)

    # Surfaces in the order the parts introduced them: the first is what most of the
    # piece is made of and is the sheet the mod falls back to for any group it cannot
    # match by name.
    kinds = []
    for obj in parts:
        kind = obj.get("thralls_surface")
        if kind is not None and kind not in kinds:
            kinds.append(kind)

    model = am.finish(parts, NAME)
    am.bake_occlusion(model)
    obj_path = am.export(model, NAME)

    textures = am.make_surface_textures(NAME, kinds)
    png_path = am.render_preview(NAME, model, textures)
    icon_path = am.render_icon(NAME, model)

    print("THRALLS_DEPOT %s verts=%d tris=%d boxes=%d surfaces=%s"
          % (NAME, len(model.data.vertices), len(model.data.polygons), col_count,
             ",".join(kinds) or "-"))
    print("THRALLS_DEPOT_FILES obj=%s col=%s png=%s icon=%s"
          % (obj_path, col_path, png_path, icon_path))


if __name__ == "__main__":
    main()
