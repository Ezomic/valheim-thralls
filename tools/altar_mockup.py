"""
Candidates to replace the bindstone, rendered beside it and beside one of the upgrades.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/altar_mockup.py

Nothing here is exported. Everything lands in assets/previews/mockups and the shipped
models are untouched, so a mockup cannot ship by accident.

Why the bindstone is being replaced. It reads as a plinth while the three upgrades read
as scenes. Its mass is four rectangular slabs stacked concentrically - kerb, kerb, body,
crown - every edge parallel to every other and the whole thing symmetric about its
centre, in one material. Swapping it to the game's own stone made that worse, because
the flat faces lost the only variation they had.

That is an overcorrection with a history. The bench before it read as furniture because
it was busy and stood on legs, so it was stripped back to a bare mass - and the incident
went out with the workbench cues. The upgrades were built afterwards, with the lesson
learned, which is why they have character it does not.

So the three below all follow what worked there:
  - an irregular mass, with no two edges parallel and stones fallen at the foot
  - five or six materials in play rather than one
  - something staged, so the piece depicts an act rather than presenting a surface

They keep what the altar needs to keep: a face carrying the binding ring, a ledge the
props recipe can stand candles and the trophy on, and more presence than an upgrade.
"""

import bpy
import math
import os
import random
import sys

from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))

import altar_model as am

MOCK_DIR = os.path.join(ROOT, "assets", "previews", "mockups")
am.OUT_DIR = MOCK_DIR
am.PREVIEW_DIR = MOCK_DIR

# The shipped darkstone sheet is deliberately extreme - a wide, hard-edged pattern that
# the game never actually shows, because those groups wear Valheim's own stone material
# via AltarVanillaGroups. Rendered here it comes out as marble camouflage and makes every
# shape unreadable, which is worse than useless for judging one. So the preview wears a
# plain mid-grey granite standing in for the vanilla material.
am.SURFACES["darkstone"] = (am.tex_granite, (0.68, 0.67, 0.63), (0.30, 0.30, 0.28),
                            (0.26, 0.29, 0.20), 0)

# Linen for the shroud. In "rag" - which is rotted grave-wrapping, grey-green - a wrapped
# body came out as a row of green eggs on a table.
am.SURFACES["linen"] = (am.tex_parchment, (0.76, 0.73, 0.64), (0.30, 0.29, 0.25),
                        (0.34, 0.30, 0.22), 0)


def rough_platform(name, radius_x, radius_y, height, count=11, mat="darkstone"):
    """
    A platform of loose flat stones rather than a cut slab.

    This is the single thing the upgrades have that the bindstone does not. A rectangular
    kerb reads as masonry laid by someone with a level; a scatter of stones at their own
    angles reads as something assembled where it stands.
    """
    parts = []
    for i in range(count):
        angle = math.radians(i * (360.0 / count) + random.uniform(-14.0, 14.0))
        dist = random.uniform(0.42, 1.0) ** 0.55
        parts.append(am.add_block(
            "%s_%d" % (name, i),
            (random.uniform(0.26, 0.44) * radius_x,
             random.uniform(0.26, 0.44) * radius_y,
             height * random.uniform(0.62, 1.15)),
            (math.cos(angle) * dist * radius_x * 0.72,
             math.sin(angle) * dist * radius_y * 0.72,
             height * 0.5 + random.uniform(-0.015, 0.015)),
            rot_z=math.degrees(angle) + random.uniform(-26.0, 26.0),
            rot_y=random.uniform(-7.0, 7.0), rot_x=random.uniform(-5.0, 5.0),
            mat=mat, collide=(i % 3 == 0)))
    return parts


def spilled(name, x, y, z, count=5, spread=0.34, mat="bone"):
    """Long bones fallen where they fell, for the foot of a thing."""
    parts = []
    for i in range(count):
        angle = random.uniform(0.0, math.tau)
        dist = random.uniform(0.2, 1.0) * spread
        bone = am.add_cyl("%s_%d" % (name, i), 0.028, random.uniform(0.22, 0.40),
                          (x + math.cos(angle) * dist, y + math.sin(angle) * dist,
                           z + 0.03 + i * 0.012),
                          axis="x", rot_z=random.uniform(-90.0, 90.0), sides=6, mat=mat)
        bone.rotation_euler[0] += math.radians(random.uniform(-16.0, 16.0))
        parts.append(bone)
    return parts


def chain(name, a, b, links=5, radius=0.045, mat="iron"):
    """A run of iron links between two points, for something being held down."""
    parts = []
    va, vb = Vector(a), Vector(b)
    for i in range(links):
        t = (i + 0.5) / float(links)
        point = va.lerp(vb, t)
        ring = am.add_ring("%s_%d" % (name, i), radius, 0.011, tuple(point), sides=8,
                           mat=mat)
        ring.rotation_euler[0] = math.radians(90.0 if i % 2 == 0 else 0.0)
        ring.rotation_euler[2] = math.radians(random.uniform(-12.0, 12.0))
        parts.append(ring)
    return parts


# ----------------------------------------------------------------- candidate A

def build_bier():
    """
    A: the bier.

    A body under grave-wrappings, laid on a slab across two lashed trestles, bound down
    with iron, with a canted head stone carrying the binding ring. What the altar is for
    is raising the dead, so it stages the thing waiting to be raised.

    The body stays fully shrouded. The bog stone owns exposed flesh and a hand out of the
    water, and two corpse pieces in one set would blur both.

    About 1.55 x 1.05 m, 1.32 m to the top of the head stone.
    """
    parts = []
    parts += rough_platform("plat", 1.5, 1.05, 0.13, count=12)

    # Two trestles, lashed rather than joined - a frame knocked together at the graveside.
    for i, x in enumerate((-0.44, 0.46)):
        for side in (-1.0, 1.0):
            parts.append(am.bar("trestle_%d_%d" % (i, side > 0),
                                (x + side * 0.17, side * 0.19, 0.10),
                                (x - side * 0.02, side * 0.03, 0.60),
                                0.045, sides=7, mat="timber"))
        parts.append(am.add_ring("trestle_lash_%d" % i, 0.075, 0.020, (x, 0.0, 0.56),
                                 sides=10, mat="wood"))

    # The slab, overhanging both trestles and a little out of true.
    parts.append(am.add_block("slab", (1.30, 0.64, 0.13), (0.0, -0.02, 0.66),
                              rot_z=-2.5, rot_y=1.4, mat="darkstone"))

    # The body: a wrapped bundle, wider at the shoulders and tapering to the feet, with
    # the wrapping cords across it. Three masses, not one - a single block reads as a
    # crate and a smooth taper reads as a fish.
    for i, (x, w, d, h) in enumerate(((-0.40, 0.26, 0.26, 0.17),
                                      (-0.14, 0.34, 0.30, 0.20),
                                      (0.16, 0.30, 0.26, 0.17),
                                      (0.42, 0.22, 0.18, 0.12))):
        # Overlapping and low, not four separate domes: a wrapped body is one continuous
        # form with the shoulders proud of it, and round lumps in a row read as fruit.
        parts.append(am.add_block("shroud_%d" % i, (w, d, h), (x, -0.02, 0.73 + h * 0.5),
                                  rot_z=random.uniform(-5.0, 5.0),
                                  rot_y=random.uniform(-4.0, 4.0), mat="linen",
                                  collide=False))
        parts.append(am.add_sphere("shroud_cap_%d" % i, (w * 0.98, d * 0.98, h * 0.85),
                                   (x, -0.02, 0.73 + h * 0.72),
                                   rot_z=random.uniform(-6.0, 6.0), mat="linen"))
    for i, x in enumerate((-0.30, 0.04, 0.32)):
        parts.append(am.add_ring("shroud_cord_%d" % i, 0.135, 0.016, (x, -0.02, 0.80),
                                 sides=12, mat="wood"))
        bpy.context.active_object.rotation_euler[1] = math.radians(90.0)

    # Bound to the slab, because a thrall is bound and not asked.
    for i, x in enumerate((-0.42, 0.30)):
        parts += chain("chain_%d" % i, (x, -0.30, 0.72), (x, 0.26, 0.72), links=5)
        parts.append(am.add_ring("bolt_%d" % i, 0.05, 0.012, (x, -0.33, 0.70), sides=8,
                                 mat="iron"))

    # The head stone: canted back, carrying the ring where you can see it standing up.
    tilt = 74.0
    pivot = (0.0, 0.44, 0.98)
    parts.append(am.add_block("headstone", (0.80, 0.17, 0.74), pivot, rot_x=-14.0,
                              rot_z=3.0, mat="darkstone"))
    parts += am.sigil_on("sigil", (0.0, 0.34, 1.02), tilt, radius=0.21, lift=0.06,
                         mat="palebone")

    # A ledge at its foot for the candles and the trophy the props recipe drops.
    parts.append(am.add_block("ledge", (0.62, 0.20, 0.09), (0.0, 0.28, 0.70), rot_z=-3.0,
                              mat="darkstone"))

    parts += am.horns("horn", (0.0, 0.50, 1.32), reach=0.26, rise=0.38, gap=0.38,
                      mat="palebone")
    parts += am.cord_and_charm("charm_l", -0.52, 0.42, 1.22, 0.16, scale=0.8)
    parts += spilled("spill", 0.62, -0.30, 0.13, count=5, spread=0.24)
    parts += am.basin("basin", -0.70, -0.34, 0.13, radius=0.15)
    return parts


# ----------------------------------------------------------------- candidate B

def build_pit():
    """
    B: the pit.

    A hole ringed with broken stone, a beam over it on two leaning posts, and the binding
    ring hanging into the dark on chains. Nothing is displayed on this one - what it
    stages is the place the dead come up from, and the emptiness is the point.

    About 1.5 x 1.4 m, 1.62 m to the beam.
    """
    parts = []

    # Raised, not dug.
    #
    # This was a hole in the ground, and a hole is the one thing a Valheim piece cannot
    # have: pieces do not cut terrain, so on a slope the rim floats on the downhill side
    # and on a wooden floor it is a hole to nowhere sitting on planks. Worse, a black disc
    # at ground level is only legible from above - at eye height you see it edge-on and it
    # reads as a puddle.
    #
    # A raised mouth solves both at once. The kerb stands proud of whatever it is placed
    # on, so it never has to agree with the ground, and you look over it and down into a
    # walled dark rather than at a flat disc.
    mouth = 0.60

    # Two courses of kerb, the upper one stepped in, so the wall has a thickness to it.
    for course, (dist, height, z, count) in enumerate(((mouth + 0.16, 0.24, 0.12, 15),
                                                       (mouth + 0.10, 0.22, 0.32, 13))):
        for i in range(count):
            angle = math.radians(i * (360.0 / count) + random.uniform(-9.0, 9.0)
                                 + course * 13.0)
            parts.append(am.add_block("kerb_%d_%d" % (course, i),
                                      (random.uniform(0.20, 0.32), random.uniform(0.17, 0.26),
                                       height * random.uniform(0.85, 1.15)),
                                      (math.cos(angle) * dist, math.sin(angle) * dist, z),
                                      rot_z=math.degrees(angle) + random.uniform(-16.0, 16.0),
                                      rot_y=random.uniform(-9.0, 9.0),
                                      mat="darkstone", collide=True))

    # The shaft wall, built as a ring of blocks rather than a cylinder.
    #
    # add_cyl makes a capped cylinder, and that cap is a solid disc laid straight across
    # the mouth - the first version had a neat grey lid over the hole, which the eye-height
    # render showed immediately and the view from above hid completely. A ring of blocks
    # has no cap to get in the way, and it matches the kerb it sits inside.
    for i in range(16):
        angle = math.radians(i * 22.5 + random.uniform(-5.0, 5.0))
        parts.append(am.add_block("shaft_%d" % i,
                                  (0.24, 0.17, 0.44 * random.uniform(0.92, 1.08)),
                                  (math.cos(angle) * (mouth - 0.02),
                                   math.sin(angle) * (mouth - 0.02), 0.22),
                                  rot_z=math.degrees(angle) + random.uniform(-8.0, 8.0),
                                  mat="darkstone", collide=False))

    # The dark at the bottom, well below the kerb, so what you see over the rim is a drop
    # rather than a surface.
    parts.append(am.add_cyl("shaft_dark", mouth - 0.06, 0.12, (0.0, 0.0, 0.02), sides=16,
                            mat="pitch"))
    # A floor you cannot stand on: solid, and far enough down to be in shadow. Left open,
    # the first thing anyone does is walk into the middle and stand in the hole.
    parts.append(am.add_cyl("shaft_floor", mouth - 0.06, 0.10, (0.0, 0.0, 0.0), sides=14,
                            mat="pitch", collide=True))

    # One capstone laid flat across the front kerb - somewhere for the candles and the
    # trophy to stand. Everything else here is deliberately uneven, so the props needed a
    # spot that was deliberately not.
    parts.append(am.add_block("ledge", (0.52, 0.26, 0.10), (0.0, -(mouth + 0.13), 0.45),
                              rot_z=-2.0, mat="darkstone", collide=True))

    # Two posts and a beam, heavier and closer in than the first pass: against the mass of
    # the kerb below, the frame was spindly enough to look like scaffolding.
    for i, (x, lean) in enumerate(((-0.60, 6.0), (0.62, -8.0))):
        post = am.add_taper("post_%d" % i, 0.098, 0.072, 1.46, (x, 0.14, 0.79), sides=7,
                            mat="timber")
        post.rotation_euler[1] = math.radians(lean)
        parts.append(post)
        parts.append(am.add_ring("post_lash_%d" % i, 0.125, 0.026, (x, 0.12, 1.40),
                                 sides=10, mat="wood"))
    parts.append(am.add_cyl("beam", 0.098, 1.56, (0.0, 0.12, 1.52), axis="x", sides=9,
                            rot_z=1.5, mat="timber"))

    # The ring hanging in the gap, on chains going down into the hole.
    parts.append(am.add_cyl("hang", 0.010, 0.26, (0.0, 0.10, 1.38), sides=5, mat="wood"))
    parts += am.sigil_on("sigil", (0.0, 0.10, 1.08), 90.0, radius=0.22, mat="palebone")
    parts += chain("chain_down", (0.0, 0.10, 0.96), (0.0, 0.06, 0.30), links=6)

    parts += am.cord_and_charm("charm_l", -0.32, 0.12, 1.47, 0.24, scale=0.85)
    parts += am.cord_and_charm("charm_r", 0.34, 0.12, 1.47, 0.15, scale=0.7)
    parts += spilled("spill", -0.62, -0.52, 0.10, count=6, spread=0.26)
    parts += am.horns("horn", (0.0, -0.80, 0.16), reach=0.24, rise=0.34, gap=0.30,
                      mat="palebone")
    return parts


# ----------------------------------------------------------------- candidate C

def build_cleft():
    """
    C: the cleft stone.

    One boulder split down the middle, held open by iron and by timber wedged in the gap,
    with the ring standing in the crack. What it stages is a thing forced open and kept
    that way - the altar as a door someone has jammed.

    About 1.35 x 1.0 m, 1.45 m at the taller half.
    """
    parts = []
    parts += rough_platform("plat", 1.35, 1.0, 0.12, count=10)

    # Two halves, each built from overlapping rough blocks so neither is a box, and each
    # leaning away from the other.
    for side in (-1.0, 1.0):
        lean = 7.0 * side
        blocks = ((0.44, 0.62, 0.86, 0.0), (0.38, 0.50, 0.62, 0.34),
                  (0.32, 0.42, 0.44, 0.62), (0.26, 0.34, 0.30, 0.86))
        for i, (w, d, h, z) in enumerate(blocks):
            parts.append(am.add_block("half_%d_%d" % (side > 0, i),
                                      (w * random.uniform(0.9, 1.1),
                                       d * random.uniform(0.9, 1.1), h),
                                      (side * (0.30 + z * 0.10) + random.uniform(-0.02, 0.02),
                                       random.uniform(-0.05, 0.05), 0.12 + z + h * 0.5),
                                      rot_z=random.uniform(-13.0, 13.0),
                                      rot_y=lean + random.uniform(-4.0, 4.0),
                                      mat="darkstone", collide=(i < 2)))

    # Timber wedged in the crack, keeping it open.
    for i, (z, length, tilt_deg) in enumerate(((0.42, 0.62, 8.0), (0.88, 0.48, -11.0))):
        prop = am.add_taper("wedge_%d" % i, 0.055, 0.030, length, (0.0, 0.14, z), sides=6,
                            mat="timber")
        prop.rotation_euler[1] = math.radians(90.0 + tilt_deg)
        parts.append(prop)

    # Iron pulling the halves apart, bolted through each.
    for i, z in enumerate((0.60, 1.02)):
        parts += chain("crack_chain_%d" % i, (-0.34, -0.22, z), (0.34, -0.22, z), links=6)
        for side in (-1.0, 1.0):
            parts.append(am.add_ring("crack_bolt_%d_%d" % (i, side > 0), 0.05, 0.013,
                                     (side * 0.38, -0.24, z), sides=8, mat="iron"))
            bpy.context.active_object.rotation_euler[0] = math.radians(90.0)

    # The ring standing in the gap, and the dark behind it.
    parts.append(am.add_block("gap_dark", (0.20, 0.34, 1.10), (0.0, 0.10, 0.70),
                              mat="pitch", collide=False))
    parts += am.sigil_on("sigil", (0.0, -0.10, 0.86), 90.0, radius=0.235, mat="palebone")

    # A ledge on the lower half for the candles and the trophy.
    parts.append(am.add_block("ledge", (0.40, 0.34, 0.10), (0.52, -0.16, 1.02),
                              rot_z=8.0, rot_y=-5.0, mat="darkstone"))

    parts += am.horns("horn", (-0.44, 0.12, 1.26), reach=0.24, rise=0.36, gap=0.30,
                      mat="palebone")
    parts += am.cord_and_charm("charm_l", 0.62, -0.10, 1.16, 0.18, scale=0.75)
    parts += spilled("spill", 0.0, -0.44, 0.12, count=6, spread=0.28)
    return parts


CANDIDATES = {
    "current": am.build_bindstone,      # what ships today, for comparison
    "bier": build_bier,
    "pit": build_pit,
    "cleft": build_cleft,
    "upgrade3": am.build_upgrade3,      # the family the new one has to belong to
}

LAYOUT = [
    ("current", -3.90, 3.00, 0.80, True),
    ("bier", -1.30, 3.10, 0.85, False),
    ("pit", 1.30, 3.20, 0.95, False),
    ("cleft", 3.80, 3.10, 0.85, False),
    ("upgrade3", 6.00, 2.70, 1.00, True),
]


def scale_rod(x, y):
    """A 1.8 m post with a collar at 1.0 m - a Viking's height, and their waist."""
    mat = bpy.data.materials.get("preview_rod")
    if mat is None:
        mat = bpy.data.materials.new("preview_rod")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (0.05, 0.05, 0.06, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.9

    for radius, depth, z in ((0.030, 1.8, 0.9), (0.065, 0.03, 1.0), (0.065, 0.03, 1.79)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=depth,
                                            location=(x, y, z))
        bpy.context.active_object.data.materials.append(mat)


def build_all():
    am.clear_scene()

    kinds = []
    finished = []

    for name, offset, _, _, rod in LAYOUT:
        random.seed(20260811)

        parts = CANDIDATES[name]()
        for obj in parts:
            kind = obj.get("thralls_surface")
            if kind is not None and kind not in kinds:
                kinds.append(kind)

        altar = am.finish(parts, name)
        altar.location.x = offset
        finished.append((name, altar))
        print("THRALLS_MOCK %s verts=%d tris=%d"
              % (name, len(altar.data.vertices), len(altar.data.polygons)))

        if rod:
            scale_rod(offset - 1.25, 1.05)

    textures = am.make_surface_textures("mockup", kinds)
    for _, altar in finished:
        am.dress(altar, textures)

    am.add_ground(None)
    am.setup_lighting()
    return finished


def render(finished):
    os.makedirs(MOCK_DIR, exist_ok=True)

    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)

    target = bpy.data.objects.new("target", None)
    bpy.context.collection.objects.link(target)

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    scene = bpy.context.scene
    scene.camera = cam
    scene.render.film_transparent = False
    scene.view_settings.exposure = -0.45

    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = candidate
            break
        except TypeError:
            continue

    def shoot(filename, location, look_at, width=900, height=760):
        cam.location = location
        target.location = look_at
        scene.render.resolution_x = width
        scene.render.resolution_y = height
        scene.render.filepath = os.path.join(MOCK_DIR, filename)
        try:
            bpy.ops.render.render(write_still=True)
        except RuntimeError as err:
            print("THRALLS_RENDER_FALLBACK %s (%s)" % (filename, err))
            scene.render.engine = "BLENDER_WORKBENCH"
            bpy.ops.render.render(write_still=True)

    shoot("new_lineup.png", (1.1, -18.5, 4.4), (1.1, 0.0, 0.80), 1900, 620)

    for name, offset, distance, look_z, _ in LAYOUT:
        shoot("eye_%s.png" % name,
              (offset + 0.90, -4.30, 1.72), (offset, 0.0, look_z * 0.80))
        shoot("new_%s.png" % name,
              (offset + distance * 0.66, -distance * 0.86, look_z + distance * 0.60),
              (offset, 0.0, look_z))


def main():
    render(build_all())
    print("THRALLS_MOCK_DIR %s" % MOCK_DIR)


if __name__ == "__main__":
    main()
