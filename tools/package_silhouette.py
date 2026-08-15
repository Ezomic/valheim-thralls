"""
Builds the package icon from the altar's own outline.

    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/package_silhouette.py

Two earlier routes were worse and are worth recording so neither gets tried again.

A photograph of the model reads as "screenshot of a piece", which is what a gallery of
Valheim mods is already full of, and the modelling turns to mush at the ~64px thumbnail
most people see.

Drawing a mark by hand went backwards. A ring with six spokes - lifted from the sigil on
the bindstone's face - is a ship's wheel to anyone who has not seen the altar. Replacing
the spokes with a hanging chain made a magnifying glass. The problem in both cases was
inventing an outline instead of using the one the mod already has.

So the outline comes from the model: the altar rendered orthographically with every
surface emitting flat white against a transparent film, which makes the alpha channel an
exact silhouette. PIL then paints that in bone on stone and sets the wordmark under it.
Guaranteed the right shape, because it IS the shape.
"""

import bpy
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))

import altar_model as am
import depot_mockup as dm

OUT = os.path.join(ROOT, "assets", "previews", "package")
MASK = 1024


def flat_white(model):
    """Every slot becomes pure emission, so nothing shades and the alpha is the outline."""
    mat = bpy.data.materials.new("silhouette")
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    emit = tree.nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = (1.0, 1.0, 1.0, 1.0)
    emit.inputs[1].default_value = 1.0
    tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])

    model.data.materials.clear()
    model.data.materials.append(mat)


def render_mask(name, builders):
    am.clear_scene()

    models = []
    for build, at_x in builders:
        parts = build()
        model = am.finish(parts, name + str(len(models)))
        model.location.x = at_x
        flat_white(model)
        models.append(model)

    scene = bpy.context.scene

    # Bounds across everything, so one camera frames the group.
    xs, ys, zs = [], [], []
    for m in models:
        for c in m.bound_box:
            w = m.matrix_world @ __import__("mathutils").Vector(c)
            xs.append(w.x); ys.append(w.y); zs.append(w.z)

    cx = (min(xs) + max(xs)) / 2.0
    cz = (min(zs) + max(zs)) / 2.0
    span = max(max(xs) - min(xs), max(zs) - min(zs))

    cam_data = bpy.data.cameras.new("silhouette_cam")
    cam_data.type = "ORTHO"
    # A tenth of air around it; the layout below adds the rest.
    cam_data.ortho_scale = span * 1.10

    cam = bpy.data.objects.new("silhouette_cam", cam_data)
    bpy.context.collection.objects.link(cam)

    # Straight on from the front. A three-quarter view carries depth the flat fill cannot
    # show, and comes out as an unreadable blob.
    cam.location = (cx, -40.0, cz)
    cam.rotation_euler = (1.5708, 0.0, 0.0)

    scene.camera = cam
    scene.render.resolution_x = MASK
    scene.render.resolution_y = MASK
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = 0.0

    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = candidate
            break
        except TypeError:
            continue

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "mask_" + name + ".png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("THRALLS_MASK %s" % path)


def main():
    import random
    random.seed(20260814)

    render_mask("altar", [(am.build_bindstone, 0.0)])
    render_mask("both", [(am.build_bindstone, -0.85), (dm.build_mast, 1.25)])
    print("THRALLS_MASK_DIR %s" % OUT)


if __name__ == "__main__":
    main()
