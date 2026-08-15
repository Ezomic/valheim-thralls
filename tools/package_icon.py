"""
Renders the Thunderstore package icon: 256x256, opaque, one per candidate.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/package_icon.py

Thunderstore rejects an upload with no icon.png at 256x256 in the package root, so this
is not decoration - it is the thing standing between the mod and being published.

Deliberately NOT the build-menu icon scaled up. That one is framed and lit to sit in a
grid of forty other pieces on a dark panel, and it is transparent; a package icon is
shown alone on a light page at 256 and again as a thumbnail at about 64, so it wants a
background of its own, a tighter subject and enough contrast to survive being shrunk.
"""

import bpy
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))

import altar_model as am
import depot_mockup as dm

OUT_DIR = os.path.join(ROOT, "assets", "previews", "package")

# Kept off the shipped asset paths until one is chosen, so a candidate cannot be packaged
# by accident.
am.OUT_DIR = OUT_DIR
am.PREVIEW_DIR = OUT_DIR


def backdrop(colour):
    """
    A plain field behind the subject.

    A big emission plane rather than the world colour: the world also lights the model,
    so darkening the background to taste would drag the altar down with it.
    """
    bpy.ops.mesh.primitive_plane_add(size=60.0, location=(0.0, 7.0, 0.0))
    plane = bpy.context.active_object
    plane.rotation_euler[0] = math.radians(90.0)

    mat = bpy.data.materials.new("backdrop")
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    emit = tree.nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = colour
    emit.inputs[1].default_value = 1.0
    tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    plane.data.materials.append(mat)
    return plane


def shoot(name, location, look_at):
    scene = bpy.context.scene

    cam_data = bpy.data.cameras.new("icon_cam")
    cam = bpy.data.objects.new("icon_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = location

    target = bpy.data.objects.new("icon_target", None)
    bpy.context.collection.objects.link(target)
    target.location = look_at

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    scene.camera = cam
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = -0.1

    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = candidate
            break
        except TypeError:
            continue

    os.makedirs(OUT_DIR, exist_ok=True)
    scene.render.filepath = os.path.join(OUT_DIR, name + ".png")
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.objects.remove(target, do_unlink=True)
    print("THRALLS_PACKAGE_ICON %s" % scene.render.filepath)


def dress(parts, name, at_x=0.0):
    kinds = []
    for obj in parts:
        kind = obj.get("thralls_surface")
        if kind is not None and kind not in kinds:
            kinds.append(kind)

    model = am.finish(parts, name)
    model.location.x = at_x
    textures = am.make_surface_textures(name, kinds)
    am.dress(model, textures)
    return model


def altar_alone(colour, name):
    am.clear_scene()
    dress(am.build_bindstone(), "bindstone")
    am.setup_lighting()
    backdrop(colour)
    # Close and slightly low, so the piece fills the square and reads at thumbnail size.
    shoot(name, (2.05, -2.85, 1.62), (0.0, 0.0, 0.92))


def altar_and_depot(colour, name):
    am.clear_scene()
    dress(am.build_bindstone(), "bindstone", at_x=-0.75)
    dress(dm.build_mast(), "depot", at_x=1.15)
    am.setup_lighting()
    backdrop(colour)
    shoot(name, (2.9, -4.3, 2.35), (0.2, 0.0, 1.15))


def main():
    import random
    random.seed(20260814)

    # Two subjects, two grounds. The dark one is what most Valheim mods use and it hides
    # a silhouette; the warm one is what the game's own store art does and it does not.
    altar_alone((0.055, 0.045, 0.038, 1.0), "icon_altar_dark")
    altar_alone((0.30, 0.22, 0.14, 1.0), "icon_altar_warm")
    altar_and_depot((0.055, 0.045, 0.038, 1.0), "icon_both_dark")

    print("THRALLS_PACKAGE_DIR %s" % OUT_DIR)


if __name__ == "__main__":
    main()
