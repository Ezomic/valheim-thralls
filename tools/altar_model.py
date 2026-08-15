"""
Generates the summoning altar meshes and exports them as OBJ, plus a preview render
of each so the shapes can be compared without launching the game.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/altar_model.py

Everything is deliberately chunky and slightly off-true - Valheim's stonework is
hand-cut, so machined edges read as wrong. Units are metres, origin on the ground
at the centre, exported Y-up for Unity.
"""

import bpy
import bmesh
import math
import random
import os
import shutil
import sys

from mathutils import Vector
import mathutils

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "assets")
PREVIEW_DIR = os.path.join(ROOT, "assets", "previews")

# The shape the mod falls back to when no others are configured. Named directly in the
# AltarModel setting rather than copied to a nameless file, so it is not shipped twice.
DEFAULT_VARIANT = "bindstone"

# The shapes actually built into assets/. The rest are shelved in assets/archive:
# their builders are kept below, so putting a name back in this list and re-running
# is all it takes to revive one. Pass "--all" after the script to build every variant
# regardless, which is how the archived shapes get regenerated for comparison.
ACTIVE = ["bindstone", "upgrade1", "upgrade2", "upgrade3", "upgrade4"]

# Where the icon camera stands, per shape. The default is the three-quarter view from
# above that the game uses for its own piece icons; a shape gets an entry of its own only
# when that angle hides what the piece is.
#
# The bog stone is the one that needed it. Its ring hangs in the air on the front face at
# y=-0.13 and is the only part of the piece that says "altar", but from the default angle
# the three leaning stakes cross straight over it - at 128 pixels the whole thing turned
# into a pale blob with a face in it. Standing further round the front and lower puts the
# ring against the slab and leaves the stakes at the edges where they belong.
# How much air is left around an icon, as a multiple of the model's own silhouette.
#
# Set from the game's own icons rather than by eye. Thirteen vanilla pieces ripped out of
# the running game average a bounding box filling 0.92 of the frame and 0.38 of its pixels;
# at 1.16 ours sat at 0.87 and 0.29, which reads as a small object floating in a tile next
# to pieces that fill theirs. 1.16 * (0.87/0.92) is where that lands.
ICON_MARGIN = 1.10
ICON_ANGLES = {
    None: mathutils.Vector((0.72, -1.0, 0.62)).normalized(),
    "upgrade1": mathutils.Vector((0.26, -1.0, 0.30)).normalized(),
}

# How many times a texture repeats per metre. Must match AltarUvScale in the mod's
# config, or the preview lies about how coarse the grain will look in game.
# Below one, so each texel covers more surface and the boards read chunky.
UV_SCALE = 0.5

# What every file this script writes is called: thrall_altar_bindstone.obj, its .col, its
# textures and its icon. A module global rather than a literal in four separate f-strings
# because the depot builder imports this module and re-points it at "thrall_depot" to get
# the whole pipeline - colliders, occlusion bake, surface textures, icon - for free.
PREFIX = "thrall_altar"


# ----------------------------------------------------------------- scene helpers

def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
    for block in list(bpy.data.cameras):
        bpy.data.cameras.remove(block)
    for block in list(bpy.data.lights):
        bpy.data.lights.remove(block)
    # Materials too, or Blender renames the second run's "wood" to "wood.001" and the
    # exported usemtl no longer matches the texture file the mod goes looking for.
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)
    for block in list(bpy.data.images):
        bpy.data.images.remove(block)


def surface(obj, kind):
    """
    Marks a part as being made of something - timber, iron, stone. Each surface becomes
    its own material on the exported model and its own texture file, which is what lets
    one bench be wood where it is wood and iron where it is iron. Parts left unmarked
    fall back to the single whole-altar texture the other shapes use.
    """
    if kind is None:
        return obj

    mat = bpy.data.materials.get(kind)
    if mat is None:
        mat = bpy.data.materials.new(kind)

    obj.data.materials.append(mat)
    obj["thralls_surface"] = kind
    return obj


def jitter(obj, amount=0.02, rot=1.5):
    obj.location.x += random.uniform(-amount, amount)
    obj.location.y += random.uniform(-amount, amount)
    obj.rotation_euler[1] += math.radians(random.uniform(-rot, rot))


def add_drum(name, radius, height, z, sides=16, taper=0.92):
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius, radius2=radius * taper,
        depth=height, location=(0.0, 0.0, z + height / 2.0))
    obj = bpy.context.active_object
    obj.name = name
    return obj


def add_block(name, size, location, rot_z=0.0, rot_x=0.0, rot_y=0.0,
              mat=None, collide=True):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    # primitive_cube_add(size=1.0) is already one metre across, so scale is the
    # dimension directly - halving it here is what made everything float apart.
    obj.scale = (size[0], size[1], size[2])
    obj.rotation_euler[2] = math.radians(rot_z)
    obj.rotation_euler[0] = math.radians(rot_x)
    obj.rotation_euler[1] = math.radians(rot_y)
    obj["thralls_collide"] = collide
    return surface(obj, mat)


def add_cyl(name, radius, length, location, axis="z", rot_z=0.0, tilt=0.0,
            sides=10, mat=None, collide=False):
    """A round bar - handles, shafts, pots. Cheap at ten sides and reads as turned."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=length,
                                        location=location)
    obj = bpy.context.active_object
    obj.name = name
    if axis == "x":
        obj.rotation_euler[1] = math.radians(90.0)
    elif axis == "y":
        obj.rotation_euler[0] = math.radians(90.0)
    obj.rotation_euler[2] += math.radians(rot_z)
    obj.rotation_euler[0] += math.radians(tilt)
    obj["thralls_collide"] = collide
    return surface(obj, mat)


def add_taper(name, base, tip, length, location, axis="z", rot_z=0.0,
              sides=8, mat=None, collide=False):
    """A tapered shaft: anvil horns, spikes, chisel blades."""
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=base, radius2=tip,
                                    depth=length, location=location)
    obj = bpy.context.active_object
    obj.name = name
    # The cone tapers towards +Z, so the axis rotations are signed to leave the point
    # facing away from the model: -X for a horn on the left, down for a blade.
    if axis == "x":
        obj.rotation_euler[1] = math.radians(-90.0)
    elif axis == "y":
        obj.rotation_euler[0] = math.radians(90.0)
    obj.rotation_euler[2] += math.radians(rot_z)
    obj["thralls_collide"] = collide
    return surface(obj, mat)


def add_clutter(prefix, centre, spread, count, scale=1.0, on_top=None,
                mat=None, collide=False):
    """
    Small loose debris - chips, offcuts, pebbles. Valheim's props are busy at small
    scale, and this is the cheapest way to break up a clean silhouette. Sizes and
    angles are all over the place on purpose.
    """
    parts = []
    cx, cy, cz = centre

    for i in range(count):
        angle = random.uniform(0.0, math.tau)
        dist = random.uniform(0.15, 1.0) ** 0.6 * spread

        sx = random.uniform(0.10, 0.26) * scale
        sy = random.uniform(0.09, 0.22) * scale
        sz = random.uniform(0.06, 0.16) * scale

        z = cz if on_top is None else on_top
        chip = add_block("%s_%d" % (prefix, i),
                         (sx, sy, sz),
                         (cx + math.cos(angle) * dist,
                          cy + math.sin(angle) * dist,
                          z + sz / 2.0),
                         rot_z=random.uniform(0.0, 180.0),
                         mat=mat, collide=collide)
        chip.rotation_euler[0] = math.radians(random.uniform(-14.0, 14.0))
        chip.rotation_euler[1] = math.radians(random.uniform(-14.0, 14.0))
        parts.append(chip)

    return parts


def add_sphere(name, size, location, rot_z=0.0, segments=10, rings=6, mat=None, collide=False):
    """A rounded lump. Bevelled cubes cannot pass for a skull; this can."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings,
                                         radius=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    # Radius one, so the scale is the half-extent in each direction.
    obj.scale = (size[0] / 2.0, size[1] / 2.0, size[2] / 2.0)
    obj.rotation_euler[2] = math.radians(rot_z)
    obj["thralls_collide"] = collide
    return surface(obj, mat)


def add_ring(name, radius, thickness, location, sides=24, mat=None, collide=False):
    """A flat ring - the sigil cut into the bench top."""
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=thickness,
                                     major_segments=sides, minor_segments=6,
                                     location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj["thralls_collide"] = collide
    return surface(obj, mat)


def skull(name, location, yaw=0.0, scale=1.0, mat="bone"):
    """
    A skull, built from the four shapes that make one readable at arm's length: a
    rounded cranium, two sunken sockets, a brow and a jaw. The sockets are iron rather
    than bone because what makes a skull a skull at this size is two dark holes.
    """
    parts = []
    x, y, z = location
    s = scale

    # Two masses only: one big braincase and one much smaller snout in front of and
    # below it. Adding cheekbones and an occiput on top of those seemed like more
    # anatomy, but overlapping spheres of similar size just bulge into each other and
    # the whole head came out as a cauliflower. The step down from cranium to snout is
    # the entire silhouette; everything else is a hole or a plate on the surface.
    parts.append(add_sphere(name + "_cranium", (0.185 * s, 0.180 * s, 0.170 * s),
                            (x, y + 0.020 * s, z + 0.105 * s), rot_z=yaw, mat=mat))

    parts.append(add_sphere(name + "_snout", (0.095 * s, 0.125 * s, 0.085 * s),
                            (x, y - 0.095 * s, z + 0.055 * s), rot_z=yaw, mat=mat))

    # Brow ridge overhanging the sockets, which is what throws them into shadow.
    parts.append(add_block(name + "_brow", (0.150 * s, 0.050 * s, 0.028 * s),
                           (x, y - 0.070 * s, z + 0.140 * s), rot_z=yaw, rot_x=-14.0,
                           mat=mat, collide=False))

    # Two black holes. At the size this is seen from, the sockets are the skull -
    # proud and round they came out as two pale eyeballs instead.
    for i, side in enumerate((-1.0, 1.0)):
        parts.append(add_sphere(name + "_socket_%d" % i,
                                (0.062 * s, 0.032 * s, 0.056 * s),
                                (x + side * 0.044 * s, y - 0.072 * s, z + 0.104 * s),
                                segments=8, rings=5, mat="pitch"))

    # The nose, a third hole between and below them.
    parts.append(add_sphere(name + "_nose", (0.028 * s, 0.020 * s, 0.038 * s),
                            (x, y - 0.118 * s, z + 0.062 * s), segments=6, rings=4,
                            mat="pitch"))

    # Teeth as one pale bar, with the jaw under it.
    parts.append(add_block(name + "_teeth", (0.085 * s, 0.065 * s, 0.020 * s),
                           (x, y - 0.095 * s, z + 0.020 * s), rot_z=yaw,
                           mat=mat, collide=False))
    parts.append(add_block(name + "_jaw", (0.100 * s, 0.100 * s, 0.028 * s),
                           (x, y - 0.065 * s, z), rot_z=yaw, mat=mat, collide=False))

    return parts


def antlers(name, location, scale=1.0, mat="bone"):
    """
    Eikthyr's antlers over the bench. The altar cannot be built without hard antler, so
    the thing that gates it ought to be the thing nailed above it.
    """
    parts = []
    x, y, z = location

    for side in (-1.0, 1.0):
        # One heavy beam a side, sweeping up and out. Two long beams read as antlers;
        # a fan of six short sticks read as a wishbone, which is what the first pass did.
        parts.append(add_taper(name + "_beam_%d" % (side > 0), 0.036 * scale, 0.010 * scale,
                               0.62 * scale, (x + side * 0.15 * scale, y, z + 0.30 * scale),
                               sides=6, mat=mat))
        # Mostly upright. Swept out near the horizontal they made a chevron, not a rack.
        bpy.context.active_object.rotation_euler[1] = math.radians(side * 24.0)

        # Two tines off each beam, kicked further out so the shape spreads as it rises.
        for i, (dx, dz, lean, length) in enumerate(((0.19, 0.30, 62.0, 0.26),
                                                    (0.28, 0.52, 46.0, 0.20))):
            parts.append(add_taper(name + "_tine_%d_%d" % (side > 0, i),
                                   0.018 * scale, 0.005 * scale, length * scale,
                                   (x + side * dx * scale, y + 0.02 * scale,
                                    z + dz * scale),
                                   sides=5, mat=mat))
            bpy.context.active_object.rotation_euler[1] = math.radians(side * lean)

    # The skull plate they are still joined to, pegged to the board.
    parts.append(add_sphere(name + "_plate", (0.20 * scale, 0.10 * scale, 0.14 * scale),
                            (x, y + 0.01 * scale, z + 0.10 * scale), mat=mat))
    return parts


def add_menhir(name, angle_deg, distance, height, radius=0.30, taper=0.62, sides=6):
    angle = math.radians(angle_deg)
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius, radius2=radius * taper, depth=height,
        location=(math.cos(angle) * distance, math.sin(angle) * distance, height / 2.0))
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler[2] = angle
    obj.rotation_euler[0] = math.radians(random.uniform(-4.0, 4.0))
    obj.rotation_euler[1] = math.radians(random.uniform(-4.0, 4.0))
    return obj


# ----------------------------------------------------------------- the variants

def build_plinth():
    """Stepped round plinth, offering slab, three leaning menhirs. The original."""
    parts = [
        add_drum("tier_lower", 1.75, 0.26, 0.0),
        add_drum("tier_upper", 1.30, 0.24, 0.26),
    ]

    parts.append(add_block("pedestal", (0.95, 0.95, 0.75), (0, 0, 0.875), rot_z=12.0))

    slab = add_block("slab", (1.55, 1.55, 0.20), (0, 0, 1.35), rot_z=-7.0)
    jitter(slab)
    parts.append(slab)

    for i, (dx, dy, sx, sy) in enumerate(
            [(0, 0.62, 1.5, 0.22), (0, -0.62, 1.5, 0.22),
             (0.62, 0, 0.22, 1.5), (-0.62, 0, 0.22, 1.5)]):
        kerb = add_block("rim_%d" % i, (sx, sy, 0.14), (dx, dy, 1.52), rot_z=-7.0)
        jitter(kerb, 0.015, 1.0)
        parts.append(kerb)

    for i, (angle, height) in enumerate([(30, 2.35), (150, 1.95), (270, 2.15)]):
        parts.append(add_menhir("menhir_%d" % i, angle, 1.95, height))

    parts.append(add_block("step_1", (1.30, 0.42, 0.16), (0, -1.68, 0.08)))
    parts.append(add_block("step_2", (1.05, 0.34, 0.14), (0, -1.42, 0.22)))
    return parts


def build_dolmen():
    """Two uprights carrying a heavy capstone, with the offering table beneath."""
    parts = [add_drum("base", 1.95, 0.22, 0.0, sides=12, taper=0.97)]

    for i, x in enumerate((-1.05, 1.05)):
        upright = add_block("upright_%d" % i, (0.52, 0.85, 2.30), (x, 0, 1.37),
                            rot_z=random.uniform(-4, 4))
        upright.rotation_euler[1] = math.radians(-3.0 if x < 0 else 3.0)
        parts.append(upright)

    cap = add_block("capstone", (3.30, 1.35, 0.42), (0, 0, 2.70), rot_z=-3.0)
    cap.rotation_euler[1] = math.radians(2.0)
    parts.append(cap)

    parts.append(add_block("table", (1.45, 0.95, 0.28), (0, 0, 0.36), rot_z=6.0))
    parts.append(add_block("table_leg", (0.55, 0.55, 0.30), (0, 0, 0.15)))

    for i, (angle, height) in enumerate([(215, 1.05), (325, 0.85)]):
        parts.append(add_menhir("stub_%d" % i, angle, 2.05, height, radius=0.26))

    return parts


def build_cairn():
    """A stacked cairn with a hollowed fire bowl at the top - rough and piled."""
    parts = []

    layers = [(1.85, 0.30), (1.55, 0.28), (1.28, 0.26), (1.02, 0.24), (0.80, 0.22)]
    z = 0.0
    for i, (radius, height) in enumerate(layers):
        drum = add_drum("cairn_%d" % i, radius, height, z, sides=9, taper=0.95)
        drum.rotation_euler[2] = math.radians(i * 17.0)
        jitter(drum, 0.05, 2.5)
        parts.append(drum)
        z += height

    # Bowl rim: a ring of rough stones instead of a turned basin.
    for i in range(8):
        angle = math.radians(i * 45.0 + 10.0)
        stone = add_block("rim_%d" % i, (0.34, 0.24, 0.26),
                          (math.cos(angle) * 0.62, math.sin(angle) * 0.62, z + 0.12),
                          rot_z=math.degrees(angle))
        jitter(stone, 0.03, 4.0)
        parts.append(stone)

    # Loose stones tumbled around the foot.
    for i in range(6):
        angle = math.radians(i * 60.0 + 25.0)
        dist = random.uniform(1.95, 2.35)
        stone = add_block("scatter_%d" % i, (0.42, 0.36, 0.28),
                          (math.cos(angle) * dist, math.sin(angle) * dist, 0.12),
                          rot_z=random.uniform(0, 90))
        parts.append(stone)

    return parts


def build_circle():
    """A low ring of standing stones around a sunken basin - a place, not an object."""
    parts = [
        add_drum("ground", 2.60, 0.18, 0.0, sides=20, taper=0.99),
        add_drum("inner", 1.45, 0.16, 0.18, sides=16, taper=0.96),
    ]

    parts.append(add_block("basin", (1.05, 1.05, 0.34), (0, 0, 0.51), rot_z=20.0))

    for i in range(7):
        angle = i * (360.0 / 7.0) + 12.0
        height = random.uniform(1.35, 2.05)
        parts.append(add_menhir("stone_%d" % i, angle, 2.15, height,
                                radius=0.26, taper=0.7, sides=5))

    for i in range(3):
        angle = math.radians(i * 120.0 + 60.0)
        parts.append(add_block("lintel_%d" % i, (0.30, 0.30, 0.22),
                               (math.cos(angle) * 1.45, math.sin(angle) * 1.45, 0.37),
                               rot_z=math.degrees(angle)))

    return parts


def build_barrow():
    """
    The necromancy altar: a haugr, a burial mound.

    Norse practice for reaching the dead was utiseta - sitting out on a howe to speak
    with whoever lay under it - and blot, where offered blood was caught in a bowl
    (hlautbolli) and sprinkled. So this is a low mound with a carved rune stone at its
    head, a bowl on a low offering slab, and ribs of stone breaking the turf.
    """
    parts = []

    # The mound itself: wide, low, settling towards the top.
    layers = [(2.30, 0.26), (2.00, 0.24), (1.70, 0.22), (1.38, 0.20)]
    z = 0.0
    for i, (radius, height) in enumerate(layers):
        drum = add_drum("mound_%d" % i, radius, height, z, sides=14, taper=0.97)
        drum.rotation_euler[2] = math.radians(i * 13.0)
        jitter(drum, 0.04, 2.0)
        parts.append(drum)
        z += height

    # The rune stone at the head of the mound, leaning back under its own age.
    runestone = add_block("runestone", (1.15, 0.34, 2.45), (0.0, 1.35, 1.10), rot_z=-6.0)
    runestone.rotation_euler[0] = math.radians(9.0)
    parts.append(runestone)

    # Offering slab and the blood bowl standing on it.
    parts.append(add_block("offer_slab", (1.35, 0.85, 0.22), (0.0, -0.45, z + 0.11), rot_z=4.0))

    bowl_z = z + 0.22
    parts.append(add_drum("bowl_foot", 0.34, 0.16, bowl_z, sides=10, taper=0.85))
    for i in range(7):
        angle = math.radians(i * 51.4 + 12.0)
        parts.append(add_block("bowl_rim_%d" % i, (0.20, 0.14, 0.20),
                               (math.cos(angle) * 0.42, math.sin(angle) * 0.42 - 0.45,
                                bowl_z + 0.18),
                               rot_z=math.degrees(angle)))

    # Ribs of stone pushing up through the mound, like a kerb of a real howe.
    for i in range(9):
        angle = i * 40.0 + 20.0
        parts.append(add_menhir("rib_%d" % i, angle, 2.35, random.uniform(0.55, 1.05),
                                radius=0.20, taper=0.55, sides=5))

    return parts


def build_worktable():
    """
    The summoning bench: a place where something is made, not worshipped.

    Every other altar here speaks the game's boss-altar language - a round stepped
    platform ringed with standing stones. This one deliberately speaks the workbench
    language instead: a waist-height work surface on legs, a rack behind it, an anvil
    block, and clutter. Horizontal and busy rather than tall and ceremonial.
    """
    parts = []
    top_h = 1.02
    top_z = top_h + 0.14          # the working surface itself

    # ---------------------------------------------------------------- the frame

    # Four heavy legs. Chamfered feet stop them ending in a flat cut against the ground.
    for i, (x, y) in enumerate(((-1.05, -0.46), (1.05, -0.46), (-1.05, 0.46), (1.05, 0.46))):
        parts.append(add_block("leg_%d" % i, (0.19, 0.19, top_h), (x, y, top_h / 2.0),
                               rot_z=random.uniform(-3, 3), mat="timber"))
        parts.append(add_block("foot_%d" % i, (0.23, 0.23, 0.07), (x, y, 0.035),
                               rot_z=random.uniform(-3, 3), mat="timber", collide=False))

    # Aprons round all four sides, so the underside is framed rather than open.
    parts.append(add_block("apron_front", (2.26, 0.11, 0.17), (0.0, -0.46, top_h - 0.20),
                           mat="timber"))
    parts.append(add_block("apron_back", (2.26, 0.11, 0.17), (0.0, 0.46, top_h - 0.20),
                           mat="timber"))
    for i, x in enumerate((-1.05, 1.05)):
        parts.append(add_block("apron_side_%d" % i, (0.11, 0.85, 0.15), (x, 0.0, top_h - 0.20),
                               mat="timber", collide=False))

    # Diagonal knee braces. Nothing does more for the silhouette than an edge that is
    # not parallel to the other edges - the frame stops reading as a stack of boxes.
    for i, (x, sign) in enumerate(((-1.05, 1), (1.05, -1))):
        parts.append(add_block("brace_%d" % i, (0.09, 0.09, 0.62),
                               (x + sign * 0.20, -0.46, top_h - 0.36),
                               rot_y=sign * 38.0, mat="timber", collide=False))

    # ---------------------------------------------------------------- the top

    # Four boards with gaps, laid a little unevenly and overhanging the frame.
    for i, y in enumerate((-0.48, -0.16, 0.16, 0.48)):
        plank = add_block("top_%d" % i, (2.52, 0.29, 0.13), (0.0, y, top_h + 0.065),
                          rot_z=random.uniform(-1.2, 1.2), mat="wood")
        jitter(plank, 0.010, 0.6)
        parts.append(plank)

    # Iron straps binding the boards at each end, and the nails holding them.
    for i, x in enumerate((-1.12, 1.12)):
        parts.append(add_block("strap_%d" % i, (0.07, 1.26, 0.028), (x, 0.0, top_z + 0.002),
                               mat="iron", collide=False))
        for j, y in enumerate((-0.48, -0.16, 0.16, 0.48)):
            parts.append(add_cyl("nail_%d_%d" % (i, j), 0.016, 0.024,
                                 (x, y, top_z + 0.022), sides=6, mat="iron"))

    # A low shelf, boarded like the top so the two read as the same carpentry.
    for i, y in enumerate((-0.32, 0.0, 0.32)):
        parts.append(add_block("shelf_%d" % i, (2.06, 0.29, 0.09), (0.0, y, 0.345),
                               rot_z=random.uniform(-1.0, 1.0), mat="wood",
                               collide=(i == 1)))

    # ---------------------------------------------------------------- the rack

    for i, x in enumerate((-1.14, 1.14)):
        parts.append(add_block("post_%d" % i, (0.15, 0.15, 1.92), (x, 0.54, 0.96),
                               rot_z=random.uniform(-2, 2), mat="timber"))
        # Corner braces where the posts meet the beam.
        parts.append(add_block("rack_brace_%d" % i, (0.08, 0.08, 0.44),
                               (x + (0.16 if i == 0 else -0.16), 0.54, 1.72),
                               rot_y=(45.0 if i == 0 else -45.0), mat="timber", collide=False))
    parts.append(add_block("beam", (2.56, 0.15, 0.15), (0.0, 0.54, 1.90), mat="timber"))
    # No cross rail. It ran straight through the band of the backboard where the runes
    # go, chopping them into disconnected bars.

    # A carved backboard between the posts, so what stands behind the bench is a stone
    # covered in runes rather than an empty timber frame.
    parts.append(add_block("stele", (2.06, 0.09, 1.02), (0.0, 0.60, 1.30), mat="runestone"))
    parts.append(add_block("stele_cap", (2.22, 0.14, 0.09), (0.0, 0.60, 1.84),
                           mat="runestone", collide=False))

    # The antlers on the crown are the game's own deer trophy now, dropped on as a prop.
    # Built from cones they took three attempts and still read as a dead branch.
    parts.append(add_block("antler_mount", (0.22, 0.10, 0.12), (0.0, 0.58, 1.99),
                           mat="timber", collide=False))
    # 1.825 is the underside of the beam (centre 1.90, height 0.15), so everything hung
    # from it starts exactly where it should rather than a guess below it.
    parts += hanging_offerings(0.52, 1.825)

    # One row of runes, cut across the clear band of the backboard between the hanging
    # trophies and the beam. Two rows fought with everything else on the board.
    parts += rune_row("runes", 0.545, 1.62, span=1.62, count=6, height=0.26)

    # ---------------------------------------------------------------- what is on it

    # The sigil the summoning is worked in: a ring cut into the boards, spoked, with a
    # disc at its centre. This is the single thing that says the bench is not a bench.
    parts.append(add_ring("sigil", 0.36, 0.020, (-0.06, 0.02, top_z + 0.012), mat="bone"))
    parts.append(add_ring("sigil_inner", 0.20, 0.014, (-0.06, 0.02, top_z + 0.010), mat="bone"))
    # Spokes span exactly from the inner ring to the outer one. Cut longer they poke out
    # past the rim and read as chips dropped on the bench rather than as a drawn figure.
    for i in range(6):
        angle = i * 60.0
        parts.append(add_block("spoke_%d" % i, (0.17, 0.022, 0.013),
                               (-0.06 + math.cos(math.radians(angle)) * 0.28,
                                0.02 + math.sin(math.radians(angle)) * 0.28,
                                top_z + 0.009),
                               rot_z=angle, mat="bone", collide=False))

    # The skull at the heart of it sits on this plinth, but the skull itself is no longer
    # modelled: the game's own skeleton trophy is dropped on top of it as a prop. A skull
    # built from spheres never stopped looking like a lump of spheres, and Valheim already
    # ships a hand-made one.
    parts.append(add_block("focus_plinth", (0.26, 0.24, 0.05), (-0.06, 0.02, top_z + 0.025),
                           rot_z=12.0, mat="stone", collide=False))

    # An offering bowl, and the knife that fills it.
    # Moved forward, so the back edge belongs to the stores.
    parts.append(add_cyl("bowl_base", 0.135, 0.10, (0.84, 0.04, top_z + 0.05), sides=12,
                         mat="stone", collide=True))
    parts.append(add_ring("bowl_rim", 0.145, 0.028, (0.84, 0.04, top_z + 0.10), sides=16,
                          mat="stone"))
    parts.append(add_cyl("bowl_blood", 0.125, 0.02, (0.84, 0.04, top_z + 0.095), sides=12,
                         mat="pitch"))

    parts.append(add_cyl("knife_grip", 0.022, 0.12, (0.40, -0.34, top_z + 0.022),
                         axis="x", rot_z=18.0, mat="bone"))
    parts.append(add_taper("knife_blade", 0.030, 0.006, 0.20, (0.24, -0.36, top_z + 0.022),
                           axis="x", sides=4, rot_z=18.0, mat="iron"))

    # A rune tablet propped against the back.
    parts.append(add_block("tablet", (0.34, 0.06, 0.42), (-0.86, 0.36, top_z + 0.20),
                           rot_x=-14.0, rot_z=6.0, mat="runestone", collide=False))
    # No modelled candles: the props recipe drops real Candle_resin at these spots, and
    # a vanilla candle brings a flame and a light with it that geometry cannot.

    parts += reagents(top_z)

    # An open scroll on the working surface, and two rolled ones stowed on the shelf.
    # Left of centre and clear of the knife: at x 0.08 the sheet ran straight through
    # the blade, which is the clipping you spotted.
    parts += scroll_open("scroll_top", -0.36, -0.40, top_z, yaw=-6.0)
    parts += scroll_rolled("scroll_shelf_a", -0.52, 0.14, 0.39, yaw=8.0)
    parts += scroll_rolled("scroll_shelf_b", -0.26, -0.10, 0.39, yaw=-14.0, length=0.26)



    # ---------------------------------------------------------------- underneath

    parts += crate(-0.70, 0.04, 0.0, 0.48, 12.0)

    # A heap of long bones under the bench, and a cracked skull on top of it.
    for i in range(5):
        parts.append(add_cyl("bone_%d" % i, 0.035, random.uniform(0.34, 0.52),
                             (0.62 + random.uniform(-0.14, 0.14),
                              random.uniform(-0.16, 0.16),
                              0.05 + i * 0.055),
                             axis="x", rot_z=random.uniform(-70.0, 70.0), sides=6,
                             mat="bone"))
    parts += skull("heap", (0.66, -0.02, 0.30), yaw=-38.0, scale=0.95)

    parts.append(add_block("blockstone", (0.40, 0.36, 0.28), (0.06, 0.24, 0.14),
                           rot_z=-24.0, mat="stone"))

    # Wax and bone dust on the shelf, and the sweepings that missed the floor.
    # No scattered debris. Small loose blocks strewn round the feet of the bench read
    # as stray cubes dropped on the ground rather than as sweepings.

    return parts


def rune_row(name, y, z, span=1.70, count=7, height=0.20, mat="wood"):
    """
    A line of runes cut into the backboard.

    Modelled rather than painted: at the texel density this altar runs, a rune drawn in
    the texture is four or five pixels wide and turns to mush. Staves are straight, so
    a handful of thin blocks does the job and reads from across a room.

    Each rune is a vertical stave with a couple of branches, in the manner of the elder
    futhark - not real writing, but the right shapes.
    """
    parts = []

    # Each rune as explicit strokes, given as endpoint pairs in a unit box: x across
    # from -0.5 to 0.5, z up from 0 to 1. Placing branches by angle and offset - which
    # is what the first attempt did - left them floating clear of their staves; joining
    # named endpoints cannot come apart.
    runes = [
        [((0, 0), (0, 1)), ((0, 0.95), (0.42, 0.78)), ((0, 0.68), (0.42, 0.51))],   # fehu
        [((0, 0), (0, 1)), ((0, 1), (0.42, 0.82)), ((0.42, 0.82), (0.42, 0.06))],   # uruz
        [((0, 0), (0, 1)), ((0, 0.78), (0.38, 0.53)), ((0.38, 0.53), (0, 0.28))],   # thurisaz
        [((0, 0), (0, 1)), ((0, 0.96), (0.40, 0.74)), ((0, 0.62), (0.40, 0.40))],   # ansuz
        [((0, 0), (0, 1)), ((0, 1), (0.40, 0.80)), ((0.40, 0.80), (0, 0.56)),
         ((0, 0.56), (0.40, 0.04))],                                                # raidho
        [((0, 0), (0, 1)), ((0, 0.76), (0.38, 1.0)), ((0, 0.76), (-0.38, 1.0))],    # algiz
        [((0.36, 1.0), (0, 0.70)), ((0, 0.70), (0.36, 0.38)), ((0.36, 0.38), (0, 0.06))],  # sowilo
    ]

    width = height * 0.62
    step = span / float(max(1, count - 1))
    start = -span * 0.5
    thickness = 0.032

    for i in range(count):
        ox = start + i * step
        oz = z - height * 0.5

        for j, ((x1, z1), (x2, z2)) in enumerate(runes[i % len(runes)]):
            ax, az = ox + x1 * width, oz + z1 * height
            bx, bz = ox + x2 * width, oz + z2 * height

            length = math.hypot(bx - ax, bz - az)
            if length < 1e-5:
                continue

            # Round stock, not flat strokes: these are meant to be sticks lashed onto
            # the board, and a rectangular bar reads as paint or as carving instead.
            stroke = add_cyl("%s_%d_%d" % (name, i, j), thickness * 0.5,
                             length + thickness, ((ax + bx) * 0.5, y, (az + bz) * 0.5),
                             axis="x", sides=6, mat=mat, collide=False)
            stroke.rotation_euler[1] = math.radians(90.0) - math.atan2(bz - az, bx - ax)
            parts.append(stroke)

    return parts


def scroll_open(name, x, y, z, yaw=0.0, length=0.42, width=0.26, mat="parchment"):
    """
    A scroll lying open: a sheet with a roll at each end, so it reads as parchment
    rather than as a flat card dropped on the bench.
    """
    parts = []

    parts.append(add_block(name + "_sheet", (length, width, 0.010), (x, y, z + 0.006),
                           rot_z=yaw, mat=mat, collide=False))

    for i, side in enumerate((-1.0, 1.0)):
        dx = math.cos(math.radians(yaw)) * side * length * 0.5
        dy = math.sin(math.radians(yaw)) * side * length * 0.5

        parts.append(add_cyl(name + "_roll_%d" % i, 0.026, width,
                             (x + dx, y + dy, z + 0.024),
                             axis="y", rot_z=yaw, sides=10, mat=mat))

    return parts


def scroll_rolled(name, x, y, z, yaw=0.0, length=0.30, mat="parchment"):
    """A scroll rolled up and tied, for stacking on a shelf."""
    parts = []

    parts.append(add_cyl(name + "_body", 0.040, length, (x, y, z + 0.040),
                         axis="x", rot_z=yaw, sides=10, mat=mat))

    # Slightly proud ends, which is what makes a cylinder read as rolled sheet.
    for i, side in enumerate((-1.0, 1.0)):
        dx = math.cos(math.radians(yaw)) * side * length * 0.5
        dy = math.sin(math.radians(yaw)) * side * length * 0.5

        parts.append(add_cyl(name + "_end_%d" % i, 0.047, 0.018,
                             (x + dx, y + dy, z + 0.040),
                             axis="x", rot_z=yaw, sides=10, mat=mat))

    # The cord round the middle.
    parts.append(add_cyl(name + "_tie", 0.044, 0.020, (x, y, z + 0.040),
                         axis="x", rot_z=yaw, sides=10, mat="iron"))

    return parts


def reagents(top_z):
    """
    What a summoner keeps within reach: ground bone, jars of something, a mortar to
    grind more in, and a rack of vials. The bench had a sigil and skulls but nothing
    being *used* - and a working table is defined by its supplies as much as its tools.
    """
    parts = []

    # A bowl of bone meal, heaped. The heap is the point: a flat disc reads as an
    # empty dish, a dome reads as full of something.
    parts.append(add_cyl("meal_bowl", 0.135, 0.075, (-0.98, -0.14, top_z + 0.038),
                         sides=12, mat="stone", collide=True))
    parts.append(add_ring("meal_rim", 0.140, 0.024, (-0.98, -0.14, top_z + 0.072),
                          sides=16, mat="stone"))
    parts.append(add_sphere("meal_heap", (0.235, 0.235, 0.105),
                            (-0.98, -0.14, top_z + 0.072), mat="bone"))

    # Spilled meal round the foot of it.


    # A mortar and its pestle, for grinding the next lot.
    parts.append(add_cyl("mortar", 0.105, 0.11, (-0.62, 0.30, top_z + 0.055), sides=10,
                         mat="stone", collide=True))
    parts.append(add_ring("mortar_rim", 0.110, 0.022, (-0.62, 0.30, top_z + 0.105),
                          sides=14, mat="stone"))
    parts.append(add_cyl("pestle", 0.026, 0.17, (-0.50, 0.24, top_z + 0.10),
                         axis="x", rot_z=26.0, tilt=18.0, sides=8, mat="stone"))

    # Sealed jars, set along the back edge with the rest of the stores.
    for i, (jx, jy, h, r) in enumerate(((0.18, 0.42, 0.20, 0.075),
                                        (0.34, 0.40, 0.15, 0.062))):
        parts.append(add_cyl("jar_%d" % i, r, h, (jx, jy, top_z + h / 2.0), sides=10,
                             mat="runestone", collide=True))
        parts.append(add_taper("jar_neck_%d" % i, r, r * 0.55, 0.045,
                               (jx, jy, top_z + h + 0.022), sides=10, mat="runestone"))
        parts.append(add_cyl("jar_cork_%d" % i, r * 0.50, 0.035,
                             (jx, jy, top_z + h + 0.058), sides=8, mat="bone"))

    # A rack of vials, also along the back. Standing at the front they were the first
    # thing you saw over the sigil, which is the wrong way round for an altar.
    parts.append(add_block("vial_rack", (0.34, 0.13, 0.055), (0.62, 0.40, top_z + 0.028),
                           rot_z=-8.0, mat="wood", collide=False))
    for i in range(3):
        vx = 0.52 + i * 0.10
        parts.append(add_cyl("vial_%d" % i, 0.026, 0.14, (vx, 0.40, top_z + 0.115),
                             sides=8, tilt=random.uniform(-7.0, 7.0), mat="pitch"))
        parts.append(add_cyl("vial_cap_%d" % i, 0.020, 0.028, (vx, 0.40, top_z + 0.196),
                             sides=6, mat="bone"))

    # The herbs are gone: hung over the front edge they crossed the scroll and the
    # boards, and three sticks in a row never read as a bunch of anything.

    return parts


def hanging_offerings(y, top_z):
    """
    What hangs over the bench. This used to be a smith's tool rack - an axe, a hammer,
    tongs, chisels - which is exactly why the thing read as a workbench and not as an
    altar. Same rack, but what swings from it now is what was paid for the thralls.
    """
    parts = []

    # Cords for the trophy heads to hang from. The heads themselves are vanilla skeleton
    # trophies dropped on as props, so only the cord is modelled here.
    for i, (x, drop) in enumerate(((-0.78, 0.30), (-0.44, 0.44), (0.72, 0.34))):
        parts.append(add_cyl("cord_%d" % i, 0.010, drop, (x, y - 0.09, top_z - drop / 2.0),
                             sides=5, mat="wood"))

    # A bundle of long bones tied together, hung from the beam. Everything here is
    # positioned from the beam's underside downwards - hung from a guessed height they
    # stopped an inch short and read as floating.
    for i in range(4):
        parts.append(add_block("charm_%d" % i, (0.035, 0.035, 0.34),
                               (0.34 + i * 0.028, y - 0.08, top_z - 0.17),
                               rot_y=random.uniform(-6.0, 6.0), mat="bone"))
    parts.append(add_block("charm_tie", (0.16, 0.06, 0.035), (0.38, y - 0.08, top_z - 0.04),
                           mat="iron", collide=False))

    # Iron rings on the beam. The ring's top edge touches it rather than hovering below.
    for i, x in enumerate((-1.00, -0.16, 0.98)):
        parts.append(add_ring("hook_%d" % i, 0.042, 0.010, (x, y - 0.09, top_z - 0.040),
                              sides=10, mat="iron"))
        bpy.context.active_object.rotation_euler[0] = math.radians(90.0)

    return parts


def crate(x, y, z, size, yaw):
    """A boarded crate: body plus corner battens, so it is not a plain cube."""
    parts = []

    height = size * 0.86
    parts.append(add_block("crate", (size, size * 0.92, height), (x, y, z + height / 2.0),
                           rot_z=yaw, mat="wood"))

    # Battens round the top and bottom edges, and one iron band across the middle.
    for edge in (0.06, height - 0.06):
        parts.append(add_block("crate_batten", (size * 1.04, size * 0.96, 0.045),
                               (x, y, z + edge), rot_z=yaw, mat="wood", collide=False))
    parts.append(add_block("crate_band", (size * 1.02, size * 0.30, 0.03),
                           (x, y, z + height * 0.55), rot_z=yaw, mat="iron", collide=False))

    return parts


def build_shrine():
    """
    A summoning stone sized like a workbench - roughly 1.9 x 1.3m and waist high, so it
    fits indoors and inside an existing base instead of demanding a clearing of its own.
    Everything else here is a monument; this one is furniture.
    """
    parts = []

    # Low foot, slightly wider than the body so it reads as seated on the ground.
    parts.append(add_block("foot", (1.90, 1.34, 0.16), (0.0, 0.0, 0.08)))
    parts.append(add_block("foot_step", (1.62, 1.10, 0.12), (0.0, 0.0, 0.22)))

    # The block itself, canted a touch so it is not perfectly square.
    body = add_block("body", (1.42, 0.94, 0.58), (0.0, 0.0, 0.57), rot_z=2.5)
    jitter(body, 0.015, 1.0)
    parts.append(body)

    # Table top with a shallow lip along the front edge.
    parts.append(add_block("top", (1.66, 1.16, 0.14), (0.0, 0.0, 0.93), rot_z=-1.5))
    parts.append(add_block("lip", (1.66, 0.14, 0.09), (0.0, -0.51, 1.045), rot_z=-1.5))

    # A short carved backstone, kept low so the whole thing stays bench height.
    back = add_block("backstone", (1.05, 0.18, 0.62), (0.0, 0.46, 1.31), rot_z=-3.0)
    back.rotation_euler[0] = math.radians(5.0)
    parts.append(back)

    # A shallow bowl cut into the top: a ring of low kerbs rather than floating cubes.
    for i in range(6):
        angle = math.radians(i * 60.0 + 15.0)
        parts.append(add_block("bowl_%d" % i, (0.26, 0.11, 0.09),
                               (math.cos(angle) * 0.34, math.sin(angle) * 0.24 - 0.06, 1.045),
                               rot_z=math.degrees(angle)))

    # Corner posts, stubby, to break the plain box silhouette.
    for i, (x, y) in enumerate(((-0.74, -0.50), (0.74, -0.50))):
        parts.append(add_block("post_%d" % i, (0.16, 0.16, 0.30), (x, y, 1.14),
                               rot_z=random.uniform(-6, 6)))

    # Offerings left on the stone, and chips fallen at its foot.
    parts += add_clutter("offering", (0.0, 0.10, 0.0), 0.55, 5, 0.85, on_top=1.00)
    parts += add_clutter("chip", (0.0, 0.0, 0.0), 1.25, 7, 1.0, on_top=0.16)

    return parts


# ------------------------------------------------- the bindstone and its upgrades
#
# The altar and the three pieces built beside it. These replaced the summoning bench,
# which borrowed the vanilla workbench's whole vocabulary - legs under an apron, a
# boarded counter, a tool rack - and so read as a workbench with runes on it however
# much was carved into the top. What is here instead has no legs, no rack and no flat
# surface to work on, and every face you would have worked on is canted towards you.
#
# The upgrades share the binding ring and the bone with the altar and nothing else:
# built to one recipe in three sizes they read as one object repeated, so each one
# carries the creature it unlocks instead - a draugr coming up out of the guck, a
# golem under the cairn, a fuling war totem.

# ----------------------------------------------------------------- tilted surfaces
#
# Every candidate leans its working face towards the player instead of leaving it flat.
# A flat waist-high surface is the single strongest workbench cue there is - it is the
# thing you would put a workpiece on - and a face you cannot set anything down on stops
# reading as furniture immediately.

def face_point(pivot, tilt, u, v, lift=0.0):
    """
    A point on a face tilted by `tilt` degrees about X, given in the face's own
    coordinates: u across it, v up its slope, lift out along its normal.

    `lift` is measured from the pivot, so anything meant to sit ON the face has to clear
    half the slab's thickness first - passing 0 buries it inside the stone.
    """
    t = math.radians(tilt)
    px, py, pz = pivot
    return (px + u,
            py + v * math.cos(t) - lift * math.sin(t),
            pz + v * math.sin(t) + lift * math.cos(t))


def in_plane(obj, tilt):
    """
    Lays an already yaw-rotated part down onto a tilted face.

    Blender's default XYZ euler applies the X rotation first, so setting both yaw and
    tilt on one object spins it about the world's Z afterwards and the part slides off
    the face. ZYX is the order that means "turn it in the plane, then tip the plane".
    """
    obj.rotation_mode = "ZYX"
    obj.rotation_euler[0] = math.radians(tilt)
    return obj


def bar(name, a, b, radius, sides=8, mat=None, over=0.0, tip=None):
    """
    A round bar running between two points.

    Aimed with to_track_quat rather than by euler angles: the poles of a tripod and the
    legs of an arch each need a lean and a bearing at once, and composing those two by
    hand is what left the first pass splayed outwards instead of meeting overhead.
    """
    va, vb = Vector(a), Vector(b)
    direction = vb - va
    length = direction.length + over
    mid = (va + vb) * 0.5

    if tip is None:
        obj = add_cyl(name, radius, length, tuple(mid), sides=sides, mat=mat)
    else:
        obj = add_taper(name, radius, tip, length, tuple(mid), sides=sides, mat=mat)

    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    return obj


def sigil_on(name, pivot, tilt, radius=0.26, lift=0.0, mat="bone", spokes=6):
    """The binding ring, cut into a tilted face rather than laid on a counter."""
    parts = []

    ring = add_ring(name, radius, 0.018, face_point(pivot, tilt, 0.0, 0.0, lift + 0.014),
                       sides=22, mat=mat)
    ring.rotation_euler[0] = math.radians(tilt)
    parts.append(ring)

    inner = add_ring(name + "_inner", radius * 0.52, 0.013,
                        face_point(pivot, tilt, 0.0, 0.0, lift + 0.012), sides=18, mat=mat)
    inner.rotation_euler[0] = math.radians(tilt)
    parts.append(inner)

    span = radius - radius * 0.52
    mid = (radius + radius * 0.52) * 0.5
    for i in range(spokes):
        angle = i * (360.0 / spokes)
        spoke = add_block("%s_spoke_%d" % (name, i), (span, 0.020, 0.012),
                             face_point(pivot, tilt,
                                        math.cos(math.radians(angle)) * mid,
                                        math.sin(math.radians(angle)) * mid, lift + 0.010),
                             rot_z=angle, mat=mat, collide=False)
        parts.append(in_plane(spoke, tilt))

    return parts


def runes_on(name, pivot, tilt, span, count=4, height=0.13, lift=0.0, mat="bone"):
    """
    A row of staves cut across a tilted face.

    rune_row draws into a vertical plane at a fixed y, which is right for a backboard
    and wrong for everything here, so the same stroke table is walked onto the face.
    """
    parts = []
    runes = [
        [((0, 0), (0, 1)), ((0, 0.95), (0.42, 0.78)), ((0, 0.68), (0.42, 0.51))],
        [((0, 0), (0, 1)), ((0, 1), (0.42, 0.82)), ((0.42, 0.82), (0.42, 0.06))],
        [((0, 0), (0, 1)), ((0, 0.78), (0.38, 0.53)), ((0.38, 0.53), (0, 0.28))],
        [((0, 0), (0, 1)), ((0, 0.96), (0.40, 0.74)), ((0, 0.62), (0.40, 0.40))],
        [((0, 0), (0, 1)), ((0, 0.76), (0.38, 1.0)), ((0, 0.76), (-0.38, 1.0))],
        [((0.36, 1.0), (0, 0.70)), ((0, 0.70), (0.36, 0.38)), ((0.36, 0.38), (0, 0.06))],
    ]

    width = height * 0.62
    step = span / float(max(1, count - 1))
    thickness = 0.022

    for i in range(count):
        ou = -span * 0.5 + i * step
        ov = -height * 0.5

        for j, ((u1, v1), (u2, v2)) in enumerate(runes[i % len(runes)]):
            au, av = ou + u1 * width, ov + v1 * height
            bu, bv = ou + u2 * width, ov + v2 * height

            length = math.hypot(bu - au, bv - av)
            if length < 1e-5:
                continue

            stroke = add_cyl("%s_%d_%d" % (name, i, j), thickness * 0.5,
                                length + thickness,
                                face_point(pivot, tilt, (au + bu) * 0.5,
                                           (av + bv) * 0.5, lift + 0.008),
                                axis="x", sides=6, mat=mat, collide=False)
            # add_cyl's axis="x" already laid the bar down; the stroke's own angle in the
            # face goes on top of that, and the tilt on top of both.
            stroke.rotation_mode = "ZYX"
            stroke.rotation_euler[1] = math.radians(90.0)
            stroke.rotation_euler[0] = math.radians(tilt)
            stroke.rotation_euler[2] = math.atan2(bv - av, bu - au)
            parts.append(stroke)

    return parts


def horn_point(centre, side, t, reach=0.34, rise=0.52, gap=0.20, sweep=72.0):
    """
    A point on a horn's curve, t running 0 at the crown to 1 at the tip.

    Split out of horns() so that anything hung off a horn can be placed on the curve
    instead of beside it. The bindstone's two charms were positioned by hand and missed
    by 7-9 cm, which at eye height is a bone charm swinging from thin air.
    """
    cx, cy, cz = centre
    radius = rise / math.sin(math.radians(sweep))
    angle = math.radians(sweep) * t
    return (cx + side * (radius * (1.0 - math.cos(angle))) * (reach / radius * 2.2),
            cy - 0.06 * t,
            cz + radius * math.sin(angle))


def horns(name, centre, reach=0.34, rise=0.52, gap=0.20, mat="bone", segments=5,
          sweep=72.0):
    """
    A pair of heavy horns, curving up and out from the crown of the stone.

    Not a deer rack. antlers builds one - a beam with tines off it - and at this size
    it reads as a dead branch every time, which is why the shipped bench dropped it for
    the game's own trophy. A horn is one thick curve with no branches, and one curve is
    a shape you can still recognise when it is forty pixels tall.
    """
    parts = []
    cx, cy, cz = centre
    radius = rise / math.sin(math.radians(sweep))

    for side in (-1.0, 1.0):
        # The two horns start a hand's width apart. Grown from one point they meet in
        # the middle and read as a single forked stick.
        root = cx + side * gap * 0.5

        points = [horn_point((root, cy, cz), side, i / float(segments),
                             reach=reach, rise=rise, gap=gap, sweep=sweep)
                  for i in range(segments + 1)]

        for i in range(segments):
            t = i / float(segments)
            parts.append(bar("%s_%d_%d" % (name, side > 0, i), points[i], points[i + 1],
                             0.062 * (1.0 - t * 0.78), sides=7, mat=mat, over=0.02,
                             tip=0.062 * (1.0 - (t + 1.0 / segments) * 0.78)))

        # The boss the horn grows out of, which hides the joint at the stone.
        parts.append(add_sphere("%s_boss_%d" % (name, side > 0),
                                   (0.15, 0.13, 0.10), (root, cy, cz + 0.02), mat=mat))

    return parts


def cord_and_charm(name, x, y, top_z, drop, scale=1.0):
    """A bone charm swinging on a cord - the one detail worth keeping off the old rack."""
    parts = [add_cyl(name + "_cord", 0.008, drop, (x, y, top_z - drop / 2.0),
                        sides=5, mat="wood")]

    for i in range(3):
        parts.append(add_block("%s_bone_%d" % (name, i), (0.026, 0.026, 0.19 * scale),
                                  (x - 0.03 + i * 0.03, y, top_z - drop - 0.09 * scale),
                                  rot_y=random.uniform(-7.0, 7.0), mat="bone",
                                  collide=False))
    parts.append(add_block(name + "_tie", (0.10, 0.05, 0.026),
                              (x, y, top_z - drop), mat="iron", collide=False))
    return parts


def draugr_hand(name, x, y, z, yaw=0.0, scale=1.0, mat="flesh", bone_mat="bone",
                wrap_mat="rag"):
    """
    A hand and forearm breaking the surface, fingers curled.

    This was a skeleton's hand and it should never have been: the rung it stands on opens
    draugr, and a draugr is not a skeleton. They are fleshed - waterlogged, rotted, still
    wearing what they were buried in - so bare ivory bones read as the wrong creature
    entirely, whatever the pose.

    Three things carry the difference. The flesh tone does most of it. The proportions do
    the next most: skeletal fingers are thin bars, and a corpse's are swollen and heavy,
    so everything here is close to half again as thick as it was. And the wrist is bound
    in the same rotted wrapping that hangs off the stakes, which ties the hand to the
    grave and hides where the forearm enters the water.

    Bone still shows at the fingertips. Flesh all the way down read as a living hand
    reaching up, which is a different and much less interesting idea.
    """
    parts = []
    s = scale
    yr = math.radians(yaw)

    wrist = (x, y, z + 0.26 * s)
    parts.append(bar(name + "_arm", (x - 0.05 * s, y + 0.08 * s, z - 0.14 * s), wrist,
                     0.046 * s, sides=7, mat=mat))

    # The wrapping, two turns of it, sitting just below the hand.
    for i, (h, r) in enumerate(((0.20, 0.054), (0.145, 0.050))):
        parts.append(add_cyl("%s_wrap_%d" % (name, i), r * s, 0.035 * s,
                             (x - 0.012 * s, y + 0.02 * s, z + h * s), sides=8,
                             tilt=random.uniform(-6.0, 6.0), mat=wrap_mat))
    parts.append(add_block(name + "_wrap_tail", (0.045 * s, 0.02 * s, 0.10 * s),
                           (x - 0.05 * s, y + 0.02 * s, z + 0.13 * s),
                           rot_y=14.0, mat=wrap_mat, collide=False))

    # A heavier palm, and a knuckle ridge across the back of it.
    parts.append(add_block(name + "_palm", (0.128 * s, 0.088 * s, 0.098 * s),
                           (x, y, z + 0.31 * s), rot_z=yaw, mat=mat, collide=False))
    parts.append(add_block(name + "_knuckles", (0.130 * s, 0.070 * s, 0.040 * s),
                           (x, y, z + 0.362 * s), rot_z=yaw, mat=mat, collide=False))

    # Four fingers, each in two joints so they hook over rather than stand up. One
    # straight bar per finger came out as the teeth of a comb.
    for i in range(4):
        offset = (i - 1.5) * 0.036 * s
        length = (0.115 - abs(i - 1.5) * 0.016) * s

        base = (x + math.cos(yr) * offset, y + math.sin(yr) * offset, z + 0.35 * s)
        knuckle = (base[0] - math.sin(yr) * 0.018 * s, base[1] + math.cos(yr) * 0.018 * s,
                   base[2] + length)
        tip = (knuckle[0] - math.sin(yr) * 0.055 * s, knuckle[1] + math.cos(yr) * 0.055 * s,
               knuckle[2] + length * 0.34)

        parts.append(bar("%s_finger_%d" % (name, i), base, knuckle, 0.021 * s, sides=5,
                         mat=mat, tip=0.018 * s, over=0.012))
        # The last joint is bone, so it reads as a corpse rather than a hand.
        parts.append(bar("%s_fingertip_%d" % (name, i), knuckle, tip, 0.017 * s, sides=5,
                         mat=bone_mat, tip=0.008 * s))

    thumb_base = (x + math.cos(yr) * 0.07 * s, y + math.sin(yr) * 0.07 * s, z + 0.30 * s)
    thumb_tip = (x + math.cos(yr) * 0.12 * s - math.sin(yr) * 0.03 * s,
                 y + math.sin(yr) * 0.12 * s + math.cos(yr) * 0.03 * s, z + 0.41 * s)
    parts.append(bar(name + "_thumb", thumb_base, thumb_tip, 0.022 * s, sides=5, mat=mat,
                     tip=0.014 * s))
    return parts


def shard(name, x, y, z, height, yaw=0.0, lean=0.0, radius=0.055, mat="crystal"):
    """A crystal spike out of the rock. Golems carry these; nothing else here does."""
    spike = add_taper(name, radius, radius * 0.10, height, (x, y, z + height * 0.42),
                         sides=5, mat=mat)
    spike.rotation_euler[1] = math.radians(lean)
    spike.rotation_euler[2] = math.radians(yaw)
    return [spike]


def club(name, base, head_at, mat="timber"):
    """
    A fuling war club: a heavy shaft with a knotted head and bone studs driven into it.

    "Still holding both clubs" is the berserker's whole description, so a crossed pair
    is the piece's signature - and a club is only a club once its head is studded.
    """
    parts = [bar(name + "_shaft", base, head_at, 0.040, sides=7, mat=mat, over=0.04)]
    parts.append(add_sphere(name + "_head", (0.19, 0.17, 0.20), head_at, mat=mat))

    hx, hy, hz = head_at
    for i in range(5):
        angle = math.radians(i * 72.0 + 18.0)
        parts.append(bar("%s_stud_%d" % (name, i), (hx, hy, hz),
                         (hx + math.cos(angle) * 0.15, hy + 0.02,
                          hz + math.sin(angle) * 0.15),
                         0.030, sides=5, mat="bone", tip=0.008))
    return parts


def basin(name, x, y, z, radius=0.17, mat="darkstone"):
    """A bowl with something dark standing in it."""
    return [
        add_cyl(name, radius, 0.15, (x, y, z + 0.075), sides=12, mat=mat, collide=True),
        add_ring(name + "_rim", radius * 1.06, 0.030, (x, y, z + 0.14), sides=16, mat=mat),
        add_cyl(name + "_blood", radius * 0.90, 0.02, (x, y, z + 0.135), sides=12,
                   mat="pitch"),
    ]


# ----------------------------------------------------------------- candidate A

def bone_bundle(name, x, y, top_z, drop, scale=1.0, count=3):
    """
    Bones hanging off a knot on their own cords, each a different length.

    The three attempts before this all failed the same way - by hanging things from a
    single rigid point so they came out as one solid silhouette. cord_and_charm packed
    three identical bars under a wide iron plate and read as a pail; bone_cluster took
    the plate off but splayed them outward, which gravity does not do, so it read as a
    whisk. Separate cords, unequal drops and unequal thickness read as bone.
    """
    parts = [add_cyl(name + "_cord", 0.007, drop, (x, y, top_z - drop / 2.0),
                        sides=5, mat="wood")]
    parts.append(add_sphere(name + "_knot", (0.028, 0.024, 0.022),
                               (x, y, top_z - drop), mat="wood"))

    # Hung across the cord rather than in a line with it, so two of them are never
    # exactly behind each other whichever side you walk round.
    for i in range(count):
        offset = (i - (count - 1) * 0.5) * 0.042
        hang = (0.05 + 0.045 * ((i * 5) % 3)) * scale
        length = (0.10 + 0.055 * ((i * 3) % 4) / 3.0) * scale
        thick = 0.017 + 0.007 * ((i * 2) % 3)

        parts.append(add_cyl("%s_tie_%d" % (name, i), 0.005, hang,
                                (x + offset, y + offset * 0.3,
                                 top_z - drop - hang * 0.5),
                                sides=4, mat="wood", collide=False))
        parts.append(add_block("%s_bone_%d" % (name, i), (thick, thick, length),
                                  (x + offset, y + offset * 0.3,
                                   top_z - drop - hang - length * 0.5),
                                  rot_y=(i - 1) * 2.5, mat="bone", collide=False))
    return parts


def bone_tooth(name, x, y, top_z, drop, scale=1.0):
    """
    One heavy tooth on a cord. The most legible option by a distance: a single tapered
    shape survives being forty pixels tall, where any bundle turns to mush.
    """
    return [
        # 4.5 mm, not the 7 mm the other charms use: at 7 mm this reads as a dowel the
        # tooth is skewered on rather than something it hangs from.
        add_cyl(name + "_cord", 0.0045, drop, (x, y, top_z - drop / 2.0),
                   sides=5, mat="wood"),
        add_sphere(name + "_cap", (0.030, 0.026, 0.024), (x, y, top_z - drop),
                      mat="iron"),
        # Narrow radius first. add_taper passes it to primitive_cone_add as radius1,
        # which is the BOTTOM of the cone - so the fat measurement in that slot hung the
        # tooth point-upwards and made it a traffic cone standing on the shelf.
        add_taper(name + "_tooth", 0.009 * scale, 0.038 * scale, 0.24 * scale,
                     (x, y, top_z - drop - 0.12 * scale), sides=7, mat="palebone"),
    ]


def bone_cluster(name, x, y, top_z, drop, scale=1.0, count=4):
    """
    A handful of bones on a cord, hanging unevenly.

    cord_and_charm puts a wide iron tie plate over three identical bars in a row, and
    at altar scale that silhouette is a bucket hanging off a stick, not a charm. Uneven
    lengths, a splay, and no plate across the top read as bone.
    """
    parts = [add_cyl(name + "_cord", 0.008, drop, (x, y, top_z - drop / 2.0),
                        sides=5, mat="wood")]

    for i in range(count):
        spread = (i - (count - 1) * 0.5) * 0.028
        length = (0.13 + 0.075 * ((i * 7) % 5) / 4.0) * scale
        bone = add_block("%s_bone_%d" % (name, i), (0.022, 0.022, length),
                            (x + spread, y + spread * 0.4,
                             top_z - drop - length * 0.5 + 0.01),
                            rot_y=(i - 1.5) * 7.0, mat="bone", collide=False)
        parts.append(bone)

    # A knot rather than a plate: just enough to say the cord gathers them.
    parts.append(add_sphere(name + "_knot", (0.030, 0.026, 0.024),
                               (x, y, top_z - drop), mat="wood"))
    return parts


def build_bindstone(tilt=26.0, ring_radius=0.235, runes="staves",
                    charms="none",
                    kerb="courses", body_skew=0.0, horn_mode="pair"):
    """
    A: the bindstone.

    The keyword arguments exist so variations can be rendered side by side without a
    second copy of this function drifting away from the shipped one. Every default
    reproduces the shipped altar exactly - changing a default changes what ships.

    One rough block of stone, waist high, with its whole top cut away at an angle and
    the binding ring carved into the slope. Horns set into the back of the crown - hard
    antler is what gates the altar, so it is what crowns it. A basin at its foot.

    Nothing about this can be mistaken for furniture: no legs, no underside, and no flat
    surface to work on. 1.28 x 0.90 m on the ground, 1.00 m to the crown, 1.55 m to the
    horn tips - shorter than the Galdr table and half the width of the bench it replaces.
    """
    parts = []
    crown_half = 0.065

    # Every course is turned the same way, and only by about a degree between them.
    #
    # They used to alternate - +2.5, then -3.5 on the course directly above it, then
    # +1.5, then -1.0 - so the yaw changed sign three times going up and the widest gap
    # was six degrees. Off-square in one direction reads as a stone that has settled;
    # off-square in a different direction on every layer reads as a badly stacked pile,
    # which is what "crooked" means. The shelf takes the crown's angle too, rather than
    # sitting square on a slab that is not.
    if kerb == "rough":
        # Broken stone piled round the foot instead of two dressed courses - the same
        # vocabulary the upgrades use, and it stops the stack reading as masonry.
        parts += rough_platform("kerb", 0.68, 0.50, 0.20, count=12)
    else:
        # Two courses of kerb, so the stone is bedded into the ground rather than set on it.
        parts.append(add_block("kerb_0", (1.28, 0.90, 0.13), (0.0, 0.0, 0.065),
                                  rot_z=2.4, mat="kerbstone"))
        parts.append(add_block("kerb_1", (1.08, 0.74, 0.11), (0.0, 0.04, 0.185),
                                  rot_z=1.8, mat="kerbstone"))

    # The block itself, canted a touch off square. Anything perfectly aligned reads as
    # dressed masonry, and this is meant to be a boulder worked on where it lay.
    body = add_block("body", (0.96 + body_skew, 0.62, 0.50),
                        (body_skew * 0.35, 0.02, 0.49), rot_z=1.5 + body_skew * 9.0,
                        mat="darkstone")
    jitter(body, 0.012 + abs(body_skew) * 0.09, 0.9)
    parts.append(body)

    # An iron band round the waist, and the ring bolts a bound thing is tied to.
    parts.append(add_block("band", (1.01, 0.67, 0.045), (0.0, 0.02, 0.58),
                              rot_z=1.5, mat="iron", collide=False))
    # Named bolt, not ring: a local called "ring" here shadowed the ring_radius
    # argument and handed sigil_on a mesh where it wanted a number.
    for i, x in enumerate((-0.46, 0.46)):
        bolt = add_ring("bolt_%d" % i, 0.055, 0.012, (x, -0.28, 0.42), sides=10,
                           mat="iron")
        bolt.rotation_euler[0] = math.radians(90.0)
        parts.append(bolt)

    # The crown: one thick slab, tipped forward so its top is a slope and not a counter.
    # No frame round it - a raised kerb on all four sides turned the face into a picture
    # frame, and a framed panel is a thing hung on furniture.
    pivot = (0.0, 0.02, 0.88)
    parts.append(add_block("crown", (1.12, 0.76, 0.13), pivot, rot_x=tilt, rot_z=1.1,
                              mat="crownstone"))

    # A lip along the low edge only, where the slope drains.
    lip = add_block("crown_lip", (1.12, 0.07, 0.06),
                       face_point(pivot, tilt, 0.0, -0.36, crown_half + 0.02),
                       rot_z=1.1, mat="crownstone", collide=False)
    parts.append(in_plane(lip, tilt))

    parts += sigil_on("sigil", pivot, tilt, radius=ring_radius, lift=crown_half,
                      mat="palebone")

    # Three staves, not a whole line of them. At four the strokes came out shorter than
    # the gaps between them and the row read as chips scattered down the slope - and at
    # three they still read as chips, which is what "none" and "bold" are here to test.
    if runes == "staves":
        parts += runes_on("runes", face_point(pivot, tilt, 0.0, -0.25), tilt, span=0.54,
                          count=3, height=0.17, lift=crown_half, mat="palebone")
    elif runes == "bold":
        parts += runes_on("runes", face_point(pivot, tilt, 0.0, -0.27), tilt, span=0.44,
                          count=2, height=0.26, lift=crown_half, mat="palebone")

    # A level shelf across the back of the crown: the one flat spot, and small - just
    # room for the candles the props recipe drops and the skull between them.
    shelf_z = face_point(pivot, tilt, 0.0, 0.38, crown_half)[2]
    parts.append(add_block("shelf", (1.00, 0.19, 0.09), (0.0, 0.27, shelf_z + 0.045),
                              rot_z=1.1, mat="crownstone"))

    # The focus. In game this is the vanilla skeleton trophy dropped on as a prop; it is
    # modelled here only so the mockup reads at a glance.
    parts += skull("focus", (0.0, 0.27, shelf_z + 0.09), yaw=4.0, scale=0.80, mat="palebone")

    # Centred and straddling the skull, so the crown is symmetric about the sigil.
    horn_centre = (0.0, 0.30, shelf_z + 0.07)
    all_horns = horns("horn", horn_centre, reach=0.30, rise=0.46, gap=0.44,
                      mat="palebone")

    if horn_mode == "single":
        # One horn, the other broken off at the boss. Kills the symmetry the crown has
        # had since the first draft.
        # horns() names its parts with "%d" against a bool, so the positive side is
        # "horn_1_*" and "horn_boss_1" - not "horn_True_*", which matched nothing and
        # quietly left this variation identical to the shipped altar.
        #
        # And they have to be deleted, not just dropped from the list. horns() has
        # already put them in the scene; leaving them out of the returned parts only
        # keeps them out of the joined mesh, and the orphans carry on rendering beside
        # it - which is why this still came out with two horns after the name fix.
        doomed = [o for o in all_horns
                  if o.name.startswith("horn_1_") or o.name == "horn_boss_1"]
        keep = [o for o in all_horns if o not in doomed]
        for dead in doomed:
            bpy.data.objects.remove(dead, do_unlink=True)
        all_horns = keep
        all_horns.append(add_sphere("horn_stump", (0.13, 0.11, 0.07),
                                       (0.22, horn_centre[1], horn_centre[2] + 0.02),
                                       mat="palebone"))
    parts += all_horns

    # One tooth on the right horn and nothing on the left.
    #
    # Hung from points taken off the horn curve itself, so a cord starts on the bone
    # rather than near it. The asymmetry is the point: it puts a mark of wear on a crown
    # that was otherwise a mirror about the sigil, and it does that without breaking a
    # horn off.
    #
    # One heavy shape rather than several small ones, because several small ones fused
    # into a single silhouette every time it was tried - a pail, then a whisk, then a
    # wind chime. A tooth still reads as bone at forty pixels.
    styles = {"cluster": bone_cluster, "bundle": bone_bundle,
              "tooth": bone_tooth, "hung": cord_and_charm}

    left_style, right_style = charms if isinstance(charms, tuple) else (charms, charms)

    sides = ((-1.0, "charm_l", 0.80, 0.14, 0.8),) if horn_mode == "single" else \
            ((-1.0, "charm_l", 0.80, 0.14, 0.8), (1.0, "charm_r", 0.68, 0.13, 0.95))

    for side, tag, t, drop, scale in sides:
        style = right_style if side > 0 else left_style
        if style == "none":
            continue

        hx, hy, hz = horn_point((side * 0.22, horn_centre[1], horn_centre[2]), side, t,
                                reach=0.30, rise=0.46, gap=0.44)
        parts += styles.get(style, cord_and_charm)(tag, hx, hy, hz, drop, scale=scale)

    # The basin, set into the kerb at the foot where the slope drains to.
    parts += basin("basin", 0.0, -0.56, 0.19)

    # Long bones leaning against the stone.
    for i, (x, yaw, lean) in enumerate(((-0.58, 12.0, 24.0), (-0.52, -8.0, 31.0))):
        bone = add_cyl("lean_%d" % i, 0.030, 0.60, (x, -0.26, 0.30), sides=6,
                          mat="palebone")
        bone.rotation_euler[1] = math.radians(lean)
        bone.rotation_euler[2] = math.radians(yaw)
        parts.append(bone)

    return parts


# ----------------------------------------------------------------- the upgrades
#
# Three pieces built beside the altar, one per tier, already separate buildables in the
# mod (thrall_altar_upgrade1..3) - they just have no models of their own yet and fall
# back to an assembly of vanilla prefabs.
#
# They are one object escalating rather than three unrelated props: a standing stone on
# a kerb that grows taller and more crowded each level, so the hammer menu reads as a
# ladder and so a base with all three built reads as one set. Each carries the
# bindstone's own marks - the same dark stone, the same iron band, the same binding ring
# - and each is kept well under the altar in mass, because they stand beside it.
#
# The existing names still fit exactly what these are, so no config renaming: bog stone,
# mountain cairn, war camp arch.

def build_upgrade1():
    """
    I - Bog stone. A drowned marker: a slab sunk to its shoulders in standing guck,
    inside a frame of rough stakes, with the binding ring strung between them and roots
    trailing off it. 0.92 x 0.84 m, 1.12 m tall.

    Low, leaning and wet. The one that is mostly wood and water, and the only one whose
    ring hangs in the air rather than being cut into anything.
    """
    parts = []

    # A wide shallow pan of guck with a stone lip round it, not a plinth. The stone is
    # in the water, not standing on a base beside it.
    parts.append(add_drum("pan", 0.46, 0.10, 0.0, sides=13, taper=0.97))
    surface(parts[-1], "darkstone")
    parts.append(add_cyl("pool", 0.40, 0.05, (0.0, 0.0, 0.095), sides=16, mat="guck"))

    # Stones broken out of the lip, half in the water.
    for i in range(5):
        angle = math.radians(i * 71.0 + 25.0)
        parts.append(add_block("lip_%d" % i, (0.20, 0.15, 0.13),
                                  (math.cos(angle) * 0.44, math.sin(angle) * 0.42, 0.055),
                                  rot_z=math.degrees(angle) + 15.0, rot_y=random.uniform(-9, 9),
                                  mat="darkstone"))

    # The slab, sunk deep and leaning hard back. Barely half of it is above the water,
    # which is the whole difference between a marker in a bog and a headstone on a lawn.
    slab = add_block("slab", (0.50, 0.26, 0.74), (0.0, 0.10, 0.30), rot_z=9.0,
                        rot_x=-27.0, rot_y=4.0, mat="darkstone")
    parts.append(slab)

    # The stake frame driven round it, leaning every which way. Rough poles, no joinery -
    # this is the rung you build with a swamp behind you and nothing else.
    tops = []
    for i, (x, y, height, lean_y, lean_x) in enumerate(
            ((-0.36, -0.16, 1.02, 9.0, -4.0), (0.38, -0.10, 0.88, -12.0, 3.0),
             (0.08, 0.36, 1.10, -3.0, 11.0))):
        stake = add_taper("stake_%d" % i, 0.058, 0.030, height, (x, y, height * 0.46),
                             sides=6, mat="timber")
        stake.rotation_euler[1] = math.radians(lean_y)
        stake.rotation_euler[0] = math.radians(lean_x)
        parts.append(stake)
        tops.append((x + math.sin(math.radians(lean_y)) * height * 0.5,
                     y - math.sin(math.radians(lean_x)) * height * 0.5,
                     height * 0.46 + math.cos(math.radians(lean_y)) * height * 0.44))

    # A cord slung between the two front stakes, and the ring hanging off it.
    parts.append(bar("tie", tops[0], tops[1], 0.010, sides=5, mat="wood"))
    ring_z = (tops[0][2] + tops[1][2]) * 0.5 - 0.20
    for i, x in enumerate((-0.15, 0.15)):
        parts.append(add_cyl("ring_cord_%d" % i, 0.008, 0.20, (x, -0.13, ring_z + 0.20),
                                sides=5, mat="wood"))
    parts += sigil_on("sigil", (0.0, -0.13, ring_z), 90.0, radius=0.165)

    # Roots and weed hanging off the frame, trailing into the water.
    for i, (x, y, drop) in enumerate(((-0.30, -0.14, 0.44), (0.33, -0.08, 0.38))):
        root = add_cyl("root_%d" % i, 0.014, drop, (x, y, tops[0][2] - drop * 0.5 - 0.06),
                          sides=5, mat="wood")
        root.rotation_euler[1] = math.radians(random.uniform(-13.0, 13.0))
        parts.append(root)

    # --------------------------------------------------------------- the draugr in it
    #
    # The rung opens draugr, so what is coming up out of the bog is a draugr: a hand out
    # of the water, its ribs behind, the grave-wrappings still on the frame and the
    # rusted axe it was buried with driven into the stone.

    # The hand, front and centre in the open water where nothing crosses it. It is the
    # whole idea of the piece, so everything else keeps out of its way - the ribcage that
    # used to sit behind it was hidden by the slab and only added clutter.
    parts += draugr_hand("risen", -0.09, -0.27, 0.10, yaw=26.0, scale=1.20)
    parts += skull("drowned", (0.25, -0.14, 0.09), yaw=-42.0, scale=0.68)

    # Grave-wrappings hanging off the top tie and one stake, rotted grey-green. Three of
    # them driven at mid height read as blades stuck in the mud rather than as cloth.
    for i, (x, y, width, drop) in enumerate(((-0.37, -0.13, 0.14, 0.40),
                                             (0.37, -0.09, 0.11, 0.28))):
        top = tops[0][2] - 0.10
        parts.append(add_block("wrap_%d" % i, (width, 0.022, drop),
                                  (x, y - 0.03, top - drop * 0.5),
                                  rot_z=random.uniform(-7.0, 7.0),
                                  rot_y=random.uniform(-6.0, 6.0), mat="rag",
                                  collide=False))
        parts.append(add_block("wrap_tail_%d" % i, (width * 0.42, 0.020, drop * 0.4),
                                  (x + width * 0.22, y - 0.03, top - drop - drop * 0.16),
                                  rot_z=random.uniform(-9.0, 9.0), mat="rag",
                                  collide=False))

    # The rusted axe it was buried with, driven into the ground behind the stone. Sunk
    # into the face of the slab it cut straight across the ring.
    parts.append(bar("axe_haft", (0.34, 0.26, 0.0), (0.24, 0.16, 0.60), 0.024, sides=6,
                     mat="timber"))
    parts.append(add_block("axe_head", (0.09, 0.045, 0.19), (0.235, 0.155, 0.63),
                              rot_z=12.0, rot_y=-16.0, mat="rust", collide=False))
    parts.append(add_taper("axe_beard", 0.095, 0.02, 0.15, (0.30, 0.16, 0.60),
                              axis="x", sides=4, rot_z=-16.0, mat="rust"))

    return parts


def build_upgrade2():
    """
    II - Mountain cairn. A cone of frost-pale rubble with the ring standing free on a
    bone frame at its peak. 0.94 x 0.94 m, 1.34 m tall.

    Built out of loose stones rather than blocks, so there is not a flat face anywhere
    on it - a cone against the two uprights either side of it is a silhouette none of
    the others can be confused with, from any angle, which is the point.
    """
    parts = []

    # The cone, ring by ring. Each stone sits on the two below it and turns a little off
    # the last, which is what a piled cairn does and a stack of drums cannot.
    courses = ((0.44, 0.055, 11, 0.20, 0.16), (0.385, 0.165, 10, 0.19, 0.155),
               (0.325, 0.27, 9, 0.18, 0.15), (0.26, 0.37, 8, 0.17, 0.145),
               (0.195, 0.465, 6, 0.16, 0.14), (0.12, 0.55, 5, 0.15, 0.13))

    for c, (radius, z, count, width, depth) in enumerate(courses):
        for i in range(count):
            angle = i * (360.0 / count) + c * 19.0
            rad = math.radians(angle)
            # Every stone a different size, and flatter than it is wide. Cut to one size
            # they came out as a stack of dice, which is the same failure as the drums.
            stone = add_block("cairn_%d_%d" % (c, i),
                                 (width * random.uniform(0.68, 1.34),
                                  depth * random.uniform(0.66, 1.28),
                                  0.135 * random.uniform(0.58, 1.15)),
                                 (math.cos(rad) * radius * random.uniform(0.90, 1.10),
                                  math.sin(rad) * radius * random.uniform(0.90, 1.10),
                                  z + random.uniform(-0.02, 0.02)),
                                 rot_z=angle + random.uniform(-22.0, 22.0),
                                 rot_y=random.uniform(-17.0, 17.0),
                                 rot_x=random.uniform(-11.0, 11.0),
                                 mat="frost", collide=(c == 0))
            parts.append(stone)

    parts.append(add_block("peak", (0.22, 0.20, 0.14), (0.0, 0.0, 0.63), rot_z=24.0,
                              rot_y=6.0, mat="frost"))

    # Stones that came off the pile, lying where they rolled.
    for i in range(6):
        angle = math.radians(i * 61.0 + 33.0)
        dist = random.uniform(0.52, 0.66)
        parts.append(add_block("fallen_%d" % i, (0.19, 0.15, 0.115),
                                  (math.cos(angle) * dist, math.sin(angle) * dist, 0.055),
                                  rot_z=math.degrees(angle) + 25.0,
                                  rot_y=random.uniform(-14, 14), mat="frost"))

    # Two bone uprights out of the peak, and the ring standing between them against the
    # sky. Carved into a face it would be invisible on a cone; held up, it is the
    # silhouette.
    for side in (-1.0, 1.0):
        parts.append(bar("upright_%d" % (side > 0), (side * 0.11, 0.02, 0.60),
                         (side * 0.235, 0.0, 1.14), 0.046, sides=7, mat="bone",
                         tip=0.030, over=0.04))
        parts.append(add_ring("upright_tie_%d" % (side > 0), 0.052, 0.012,
                                 (side * 0.20, 0.0, 1.00), sides=10, mat="iron"))

    parts += sigil_on("sigil", (0.0, 0.0, 1.09), 90.0, radius=0.205)

    parts += cord_and_charm("charm_l", -0.25, 0.0, 1.11, 0.13, scale=0.7)
    parts += cord_and_charm("charm_r", 0.26, 0.0, 1.11, 0.09, scale=0.6)

    # --------------------------------------------------------------- the golem in it
    #
    # The rung opens golems, so the cairn is piled over one: its head is out at the front
    # with the rubble heaped on its back, and a fist is buried either side. The horns
    # that used to sit here are gone - they are the altar's mark, and this is not the
    # altar.

    # The head sits proud of the pile and forward of it. Tucked into the rubble it was
    # just another stone with two lights on it.
    parts.append(add_block("golem_head", (0.46, 0.38, 0.36), (0.0, -0.50, 0.21),
                              rot_z=-6.0, rot_x=-12.0, mat="frost"))
    parts.append(add_block("golem_brow", (0.48, 0.15, 0.10), (0.0, -0.66, 0.36),
                              rot_z=-6.0, rot_x=-12.0, mat="frost", collide=False))
    parts.append(add_block("golem_jaw", (0.38, 0.26, 0.10), (0.0, -0.56, 0.05),
                              rot_z=-6.0, mat="frost", collide=False))

    # The eyes. Two lit slots under the brow, and they are the entire read: without them
    # the block at the front of a cairn is just another stone.
    for side in (-1.0, 1.0):
        parts.append(add_block("golem_socket_%d" % (side > 0), (0.13, 0.07, 0.085),
                                  (side * 0.12, -0.675, 0.255), rot_z=-6.0, mat="pitch",
                                  collide=False))
        parts.append(add_block("golem_eye_%d" % (side > 0), (0.095, 0.055, 0.058),
                                  (side * 0.12, -0.695, 0.255), rot_z=-6.0, mat="crystal",
                                  collide=False))

    # A fist buried either side of the head, knuckles up.
    for side in (-1.0, 1.0):
        parts.append(add_block("golem_fist_%d" % (side > 0), (0.28, 0.26, 0.21),
                                  (side * 0.55, -0.34, 0.10), rot_z=side * 24.0,
                                  rot_y=side * 9.0, mat="frost"))
        for i in range(3):
            parts.append(add_block("golem_knuckle_%d_%d" % (side > 0, i),
                                      (0.075, 0.17, 0.065),
                                      (side * (0.49 + i * 0.062), -0.37, 0.21),
                                      rot_z=side * 24.0, mat="frost", collide=False))

    # Crystal growing out of the rubble high on its back, where it clears the stones.
    # Down among them the shards were swallowed and only one ever showed.
    for i, (x, y, z, height, yaw, lean) in enumerate(
            ((-0.14, 0.14, 0.50, 0.34, 20.0, -26.0), (-0.05, 0.20, 0.55, 0.23, 70.0, -13.0),
             (-0.20, 0.06, 0.44, 0.19, 40.0, -36.0), (0.17, 0.00, 0.47, 0.27, 200.0, 28.0),
             (0.22, 0.10, 0.42, 0.16, 150.0, 35.0))):
        parts += shard("shard_%d" % i, x, y, z, height, yaw=yaw, lean=lean,
                       radius=0.045 + i * 0.004)

    return parts


def build_upgrade3():
    """
    III - War totem. One hewn pole carrying heads, the ring, the berserker's crossed
    clubs and a crown of tusks. 0.72 x 0.66 m, 1.80 m tall.

    This replaces a gate, and the gate had four things wrong with it. It was 1.58 m wide
    and taller than the altar, so the piece meant to stand beside the altar dominated
    it. An arch is something you walk through, so alone in a base it read as the one
    surviving span of a wall that was never built. The clubs crossed symmetrically over
    a beam read heraldic - a crest rather than a place. And a broad flat banner was the
    loudest surface in the set by a long way.

    A pole fixes all four at once: narrow on the ground, strongly vertical against the
    low leaning bog stone and the cone, no implied fence, and the hide reduced to ribbons.
    """
    parts = []
    base_z = 0.12
    pole_top = 1.50

    # A small footing, and stones packed round the pole to hold it up.
    parts.append(add_block("kerb", (0.64, 0.58, base_z), (0.0, 0.0, base_z / 2.0),
                              rot_z=5.0, mat="darkstone"))
    for i in range(5):
        angle = math.radians(i * 73.0 + 22.0)
        parts.append(add_block("packing_%d" % i, (0.19, 0.15, 0.13),
                                  (math.cos(angle) * 0.24, math.sin(angle) * 0.22,
                                   base_z + 0.04),
                                  rot_z=math.degrees(angle) + 18.0,
                                  rot_y=random.uniform(-12, 12), mat="darkstone"))

    # The pole: eight-sided, so it reads as hewn off a log rather than turned.
    parts.append(add_taper("pole", 0.115, 0.085, pole_top - base_z + 0.10,
                              (0.0, 0.02, base_z + (pole_top - base_z) * 0.5), sides=8,
                              mat="timber", collide=True))
    for i, z in enumerate((0.46, 0.92, 1.32)):
        parts.append(add_ring("pole_lash_%d" % i, 0.115, 0.020, (0.0, 0.02, z),
                                 sides=12, mat="wood"))

    # ------------------------------------------------------------ what is nailed to it
    #
    # Heads up the front of it, each turned its own way. Facing squarely forward and
    # evenly spaced they stack like beads; the stagger is what makes them look nailed on
    # one at a time.
    for i, (x, z, yaw, scale) in enumerate(((-0.03, 0.42, 17.0, 0.78),
                                            (0.04, 0.74, -22.0, 0.70))):
        parts += skull("head_%d" % i, (x, -0.15, z), yaw=yaw, scale=scale)
        parts.append(add_cyl("head_spike_%d" % i, 0.014, 0.14, (x, -0.06, z + 0.02),
                                axis="y", sides=5, mat="rust"))

    # The binding ring, mounted on the pole between the heads and the clubs.
    parts += sigil_on("sigil", (0.0, -0.115, 1.06), 90.0, radius=0.165)

    # ------------------------------------------------------------ the berserker in it
    #
    # Both clubs, lashed crossed near the top where they are the silhouette. Deliberately
    # off-square - one sits higher and reaches further than the other, which is the
    # difference between a pair of weapons stowed on a pole and a coat of arms.
    # Steep, near 45 degrees. Shallow they came off the pole horizontally and the whole
    # thing read as a signpost with two arms.
    parts += club("club_l", (0.07, 0.12, 1.06), (-0.40, 0.12, 1.54))
    parts += club("club_r", (-0.07, 0.12, 1.09), (0.43, 0.12, 1.46))
    parts.append(add_ring("club_lash", 0.09, 0.024, (0.0, 0.12, 1.24), sides=12,
                             mat="wood"))

    # Hide cut into ribbons rather than hung as a sheet, tied out along the club shafts
    # where they hang clear. Bunched at the lashing they were behind the pole from every
    # angle and showed as a red smear.
    for i, (x, z, width, drop) in enumerate(((-0.19, 1.32, 0.075, 0.40),
                                             (-0.27, 1.40, 0.060, 0.28),
                                             (0.20, 1.30, 0.070, 0.34))):
        parts.append(add_block("ribbon_%d" % i, (width, 0.022, drop),
                                  (x, 0.09, z - drop * 0.5),
                                  rot_z=random.uniform(-6.0, 6.0),
                                  rot_y=random.uniform(-5.0, 5.0), mat="hide",
                                  collide=False))
        parts.append(add_block("ribbon_tail_%d" % i, (width * 0.5, 0.020, drop * 0.34),
                                  (x + width * 0.2, 0.09, z - drop - drop * 0.15),
                                  rot_z=random.uniform(-9.0, 9.0), mat="hide",
                                  collide=False))

    # Tusks crowning the pole.
    parts += horns("tusk", (0.0, 0.02, pole_top - 0.06), reach=0.17, rise=0.30, gap=0.15,
                   segments=4, sweep=62.0)

    parts += cord_and_charm("charm_l", -0.30, 0.10, 1.30, 0.22, scale=0.75)
    parts += cord_and_charm("charm_r", 0.33, 0.10, 1.24, 0.14, scale=0.65)

    # Two short stakes driven at the foot. Low and close in, so they read as packing
    # round the pole rather than as the start of a fence.
    for i, (x, y, tilt_deg) in enumerate(((-0.30, 0.16, -19.0), (0.32, -0.14, 15.0))):
        stake = add_taper("stake_%d" % i, 0.046, 0.010, 0.50, (x, y, 0.24), sides=6,
                             mat="timber")
        stake.rotation_euler[1] = math.radians(tilt_deg)
        parts.append(stake)

    return parts


# ------------------------------------------------------ the pit and the rift stone
#
# The altar the stacked-slab bindstone was replaced by, and the fourth upgrade.
#
# The bindstone read as a plinth while its own upgrades read as scenes: four rectangular
# slabs stacked concentrically, every edge parallel, symmetric about its centre and in a
# single material. These two follow what the upgrades got right instead - an irregular
# mass, several materials, and something staged rather than a surface presented.
#
# The pit keeps the shape name 'bindstone' deliberately. Renaming it would change the
# prefab, and ZNetScene discards ZDOs whose prefab it cannot resolve - every altar
# already standing would be destroyed. Swapping the geometry behind the existing name
# costs nothing and nobody loses a build.

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
        parts.append(add_block(
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
        bone = add_cyl("%s_%d" % (name, i), 0.028, random.uniform(0.22, 0.40),
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
        ring = add_ring("%s_%d" % (name, i), radius, 0.011, tuple(point), sides=8,
                           mat=mat)
        ring.rotation_euler[0] = math.radians(90.0 if i % 2 == 0 else 0.0)
        ring.rotation_euler[2] = math.radians(random.uniform(-12.0, 12.0))
        parts.append(ring)
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
            parts.append(add_block("kerb_%d_%d" % (course, i),
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
        parts.append(add_block("shaft_%d" % i,
                                  (0.24, 0.17, 0.44 * random.uniform(0.92, 1.08)),
                                  (math.cos(angle) * (mouth - 0.02),
                                   math.sin(angle) * (mouth - 0.02), 0.22),
                                  rot_z=math.degrees(angle) + random.uniform(-8.0, 8.0),
                                  mat="darkstone", collide=False))

    # The dark at the bottom, well below the kerb, so what you see over the rim is a drop
    # rather than a surface.
    parts.append(add_cyl("shaft_dark", mouth - 0.06, 0.12, (0.0, 0.0, 0.02), sides=16,
                            mat="pitch"))
    # A floor you cannot stand on: solid, and far enough down to be in shadow. Left open,
    # the first thing anyone does is walk into the middle and stand in the hole.
    parts.append(add_cyl("shaft_floor", mouth - 0.06, 0.10, (0.0, 0.0, 0.0), sides=14,
                            mat="pitch", collide=True))

    # One capstone laid flat across the front kerb - somewhere for the candles and the
    # trophy to stand. Everything else here is deliberately uneven, so the props needed a
    # spot that was deliberately not.
    parts.append(add_block("ledge", (0.52, 0.26, 0.10), (0.0, -(mouth + 0.13), 0.45),
                              rot_z=-2.0, mat="darkstone", collide=True))

    # Two posts and a beam, heavier and closer in than the first pass: against the mass of
    # the kerb below, the frame was spindly enough to look like scaffolding.
    for i, (x, lean) in enumerate(((-0.60, 6.0), (0.62, -8.0))):
        post = add_taper("post_%d" % i, 0.098, 0.072, 1.46, (x, 0.14, 0.79), sides=7,
                            mat="timber")
        post.rotation_euler[1] = math.radians(lean)
        parts.append(post)
        parts.append(add_ring("post_lash_%d" % i, 0.125, 0.026, (x, 0.12, 1.40),
                                 sides=10, mat="wood"))
    parts.append(add_cyl("beam", 0.098, 1.56, (0.0, 0.12, 1.52), axis="x", sides=9,
                            rot_z=1.5, mat="timber"))

    # The ring hanging in the gap, on chains going down into the hole.
    parts.append(add_cyl("hang", 0.010, 0.26, (0.0, 0.10, 1.38), sides=5, mat="wood"))
    parts += sigil_on("sigil", (0.0, 0.10, 1.08), 90.0, radius=0.22, mat="palebone")
    parts += chain("chain_down", (0.0, 0.10, 0.96), (0.0, 0.06, 0.30), links=6)

    parts += cord_and_charm("charm_l", -0.32, 0.12, 1.47, 0.24, scale=0.85)
    parts += cord_and_charm("charm_r", 0.34, 0.12, 1.47, 0.15, scale=0.7)
    parts += spilled("spill", -0.62, -0.52, 0.10, count=6, spread=0.26)
    parts += horns("horn", (0.0, -0.80, 0.16), reach=0.24, rise=0.34, gap=0.30,
                      mat="palebone")
    return parts


# ----------------------------------------------------------------- candidate C

def build_upgrade4():
    """
    IV: the rift stone - the cleft, promoted to an upgrade.

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
            parts.append(add_block("half_%d_%d" % (side > 0, i),
                                      (w * random.uniform(0.9, 1.1),
                                       d * random.uniform(0.9, 1.1), h),
                                      (side * (0.30 + z * 0.10) + random.uniform(-0.02, 0.02),
                                       random.uniform(-0.05, 0.05), 0.12 + z + h * 0.5),
                                      rot_z=random.uniform(-13.0, 13.0),
                                      rot_y=lean + random.uniform(-4.0, 4.0),
                                      mat="darkstone", collide=(i < 2)))

    # Timber wedged in the crack, keeping it open.
    for i, (z, length, tilt_deg) in enumerate(((0.42, 0.62, 8.0), (0.88, 0.48, -11.0))):
        prop = add_taper("wedge_%d" % i, 0.055, 0.030, length, (0.0, 0.14, z), sides=6,
                            mat="timber")
        prop.rotation_euler[1] = math.radians(90.0 + tilt_deg)
        parts.append(prop)

    # Iron pulling the halves apart, bolted through each.
    for i, z in enumerate((0.60, 1.02)):
        parts += chain("crack_chain_%d" % i, (-0.34, -0.22, z), (0.34, -0.22, z), links=6)
        for side in (-1.0, 1.0):
            parts.append(add_ring("crack_bolt_%d_%d" % (i, side > 0), 0.05, 0.013,
                                     (side * 0.38, -0.24, z), sides=8, mat="iron"))
            bpy.context.active_object.rotation_euler[0] = math.radians(90.0)

    # The ring standing in the gap, and the dark behind it.
    parts.append(add_block("gap_dark", (0.20, 0.34, 1.10), (0.0, 0.10, 0.70),
                              mat="pitch", collide=False))
    parts += sigil_on("sigil", (0.0, -0.10, 0.86), 90.0, radius=0.235, mat="palebone")

    # A ledge on the lower half for the candles and the trophy.
    parts.append(add_block("ledge", (0.40, 0.34, 0.10), (0.52, -0.16, 1.02),
                              rot_z=8.0, rot_y=-5.0, mat="darkstone"))

    parts += horns("horn", (-0.44, 0.12, 1.26), reach=0.24, rise=0.36, gap=0.30,
                      mat="palebone")
    parts += cord_and_charm("charm_l", 0.62, -0.10, 1.16, 0.18, scale=0.75)
    parts += spilled("spill", 0.0, -0.44, 0.12, count=6, spread=0.28)
    return parts




# Every shape that can be built. Only the names in ACTIVE are written to assets/ on a
# normal run; the others are shelved and kept here so nothing has to be re-modelled.
VARIANTS = {
    # Back to the bindstone the name was coined for. The pit held this slot for a while
    # and is parked below - the key is a slot, not a description of what stands in it,
    # and it stays "bindstone" whatever the shape, because renaming it would take every
    # altar already standing in a world down with it.
    "bindstone": build_bindstone,
    "upgrade4": build_upgrade4,
    "upgrade1": build_upgrade1,
    "upgrade2": build_upgrade2,
    "upgrade3": build_upgrade3,
    # ---- archived: see assets/archive ----
    # The summoning bench the bindstone replaced. Its builder stays here so it can be
    # rebuilt, but its files are shelved and it is out of ACTIVE.
    #
    # Note that shelving the files is NOT what destroys a standing one: Shapes() builds the
    # prefab list from AltarShapes alone and never looks at the disk, so a shape named there
    # with no model still registers and falls back to the AltarParts assembly. It is taking
    # the name out of AltarShapes that discards the ZDOs.
    "pit": build_pit,
    "worktable": build_worktable,
    "shrine": build_shrine,
    "plinth": build_plinth,
    "dolmen": build_dolmen,
    "cairn": build_cairn,
    "circle": build_circle,
    "barrow": build_barrow,
}


# ----------------------------------------------------------------- finishing

def write_colliders(parts, name):
    """
    One box per piece of stone, taken straight off the parts before they are joined.
    A single box around the whole altar is far too crude, and a concave mesh collider
    is both expensive and unpredictable to walk on, so each block gets its own.

    Blender is Z-up and the export is Y-up, so axes are swapped to match the mesh.
    """
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, "%s_%s.col" % (PREFIX, name))

    lines = ["# box  centre x y z  size x y z  qx qy qz qw   (metres, Y-up, Unity quat)"]
    for obj in parts:
        # Hanging and decorative parts opt out - a charm swinging off a horn should not
        # be something you walk into.
        #
        # This used to exclude tilted parts as well, because the line could only carry a
        # yaw and a leaning slab would come out as an upright box standing through its
        # own face - collision you can feel but cannot see. The line carries a full
        # rotation now, so a tilt is no longer a reason to skip anything.
        if not obj.get("thralls_collide", True):
            continue

        cx, cy, cz = obj.location

        # Local bounds times scale gives the box before rotation. Using world dimensions
        # instead would inflate every tilted block into a much larger upright one, which
        # is what made the collision feel bloated.
        xs = [v[0] for v in obj.bound_box]
        ys = [v[1] for v in obj.bound_box]
        zs = [v[2] for v in obj.bound_box]

        dx = (max(xs) - min(xs)) * obj.scale.x
        dy = (max(ys) - min(ys)) * obj.scale.y
        dz = (max(zs) - min(zs)) * obj.scale.z

        if max(dx, dy, dz) < 0.12:
            continue

        # Blender is Z-up and Unity is Y-up, and swapping those two axes is a mirror, not
        # a turn - so the rotation has to be reflected as well as permuted. For a
        # quaternion that works out as swapping y and z and negating the axis, because
        # the mirror reverses which way a positive angle goes.
        #
        # Checked against the yaw this replaces: a Blender turn of t about Z is
        # (cos t/2, 0, 0, sin t/2), which comes through here as a Unity turn of -t about
        # Y - exactly the Quaternion.Euler(0, -yaw, 0) the old pair of lines produced.
        bw, bx, by, bz = obj.rotation_euler.to_quaternion()
        lines.append("box %.3f %.3f %.3f %.3f %.3f %.3f %.5f %.5f %.5f %.5f"
                     % (cx, cz, cy, dx, dz, dy, -bx, -bz, -by, bw))

    with open(path, "w") as handle:
        handle.write("\n".join(lines) + "\n")

    return path, len(lines) - 1


def finish(parts, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    altar = bpy.context.active_object
    altar.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Chamfer every edge. Nothing in Valheim has a knife-sharp 90 degree corner - the
    # bevel catches a highlight along each edge, and that highlight is most of what
    # separates a prop from a box. Cheap too: a few hundred extra triangles.
    bevel = altar.modifiers.new("bevel", "BEVEL")
    bevel.width = 0.028
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(35.0)
    bevel.miter_outer = "MITER_ARC"
    bpy.ops.object.modifier_apply(modifier=bevel.name)

    mesh = altar.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()

    # Smooth across the bevels but keep the flat faces flat, so edges round off into the
    # light instead of every surface turning into a soft blob.
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(32.0))
    except AttributeError:
        for poly in mesh.polygons:
            poly.use_smooth = False

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=1.0)
    bpy.ops.object.mode_set(mode="OBJECT")

    return altar


def bake_occlusion(altar, samples=24, reach=1.6):
    """
    Bakes ambient occlusion into vertex colours.

    This is the single biggest thing separating these from real Valheim props: the
    game's pieces are dark where parts meet, which is what makes them look like they
    are resting on each other rather than floating. Valheim's piece shader multiplies
    by vertex colour, so darkening the crevices here shows up in game for free.

    Rays are cast from each vertex over the hemisphere around its normal; the share
    that hit the model is how enclosed that vertex is.
    """
    import numpy as np
    from mathutils import Vector

    mesh = altar.data
    rng = np.random.default_rng(7)

    # Even-ish hemisphere directions, reused for every vertex.
    dirs = rng.normal(size=(samples, 3))
    dirs /= np.linalg.norm(dirs, axis=1)[:, None]

    colours = []
    for vert in mesh.vertices:
        origin = vert.co
        normal = vert.normal
        hits = 0

        for d in dirs:
            direction = Vector((float(d[0]), float(d[1]), float(d[2])))
            if direction.dot(normal) < 0.0:
                direction = -direction

            # Start just off the surface so the ray does not hit its own face.
            hit, _, _, _ = altar.ray_cast(origin + normal * 0.005 + direction * 0.001,
                                          direction, distance=reach)
            if hit:
                hits += 1

        openness = 1.0 - (hits / float(samples))
        # Keep some light in the deepest creases rather than going to black.
        shade = 0.34 + 0.66 * (openness ** 0.8)
        colours.append(shade)

    layer = mesh.color_attributes.new(name="ao", type="FLOAT_COLOR", domain="POINT")
    flat = []
    for shade in colours:
        flat.extend((shade, shade, shade, 1.0))
    layer.data.foreach_set("color", flat)

    darkest = min(colours)
    print("THRALLS_AO %s verts=%d darkest=%.2f" % (altar.name, len(colours), darkest))


def export(altar, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, "%s_%s.obj" % (PREFIX, name))

    bpy.ops.object.select_all(action="DESELECT")
    altar.select_set(True)
    bpy.context.view_layer.objects.active = altar

    # Materials are exported for the usemtl lines alone - they tell the mod which faces
    # are timber and which are iron. The .mtl file itself is Blender's idea of a
    # material and means nothing to Valheim's shader, so it is thrown away below.
    try:
        # export_colors carries the baked occlusion out as "v x y z r g b".
        bpy.ops.wm.obj_export(
            filepath=path, export_selected_objects=True, export_materials=True,
            export_triangulated_mesh=True, export_normals=True, export_uv=True,
            export_colors=True, forward_axis="Z", up_axis="Y")
    except (AttributeError, TypeError):
        bpy.ops.export_scene.obj(
            filepath=path, use_selection=True, use_materials=True, use_triangles=True,
            use_normals=True, use_uvs=True, axis_forward="Z", axis_up="Y")

    mtl = os.path.splitext(path)[0] + ".mtl"
    if os.path.exists(mtl):
        os.remove(mtl)

    return path


def stone_material():
    """
    A procedural approximation of Valheim's stone, for previews only. In game the mesh
    wears a material lifted off a real piece, so this exists purely so the comparison
    renders read as rock rather than clay.
    """
    mat = bpy.data.materials.new("preview_stone")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.88
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.22

    # Large scale mottling for colour, fine scale for the bumpy surface.
    blotch = nodes.new("ShaderNodeTexNoise")
    blotch.inputs["Scale"].default_value = 3.5
    blotch.inputs["Detail"].default_value = 6.0

    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.35
    ramp.color_ramp.elements[0].color = (0.155, 0.150, 0.142, 1.0)
    ramp.color_ramp.elements[1].position = 0.72
    ramp.color_ramp.elements[1].color = (0.40, 0.39, 0.365, 1.0)

    links.new(blotch.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])

    grain = nodes.new("ShaderNodeTexNoise")
    grain.inputs["Scale"].default_value = 28.0
    grain.inputs["Detail"].default_value = 8.0

    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.55
    links.new(grain.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])

    return mat


def setup_lighting():
    """Low northern sun and a cool sky, which is roughly Valheim's key light."""
    sun_data = bpy.data.lights.new("key", type="SUN")
    sun_data.energy = 2.1
    sun_data.angle = math.radians(3.0)
    sun_data.color = (1.0, 0.94, 0.84)

    sun = bpy.data.objects.new("key", sun_data)
    bpy.context.collection.objects.link(sun)
    sun.location = (4.0, -5.0, 8.0)
    sun.rotation_euler = (math.radians(52.0), 0.0, math.radians(38.0))

    fill_data = bpy.data.lights.new("fill", type="AREA")
    fill_data.energy = 35.0
    fill_data.size = 8.0
    fill_data.color = (0.68, 0.76, 0.92)

    fill = bpy.data.objects.new("fill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (-6.0, 4.0, 4.0)
    fill.rotation_euler = (math.radians(60.0), 0.0, math.radians(-140.0))

    world = bpy.context.scene.world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.18, 0.21, 0.26, 1.0)
        bg.inputs[1].default_value = 1.0


def add_ground(mat):
    bpy.ops.mesh.primitive_plane_add(size=26.0, location=(0.0, 0.0, 0.0))
    ground = bpy.context.active_object
    ground.name = "ground"

    grass = bpy.data.materials.new("preview_ground")
    grass.use_nodes = True
    grass.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.13, 0.16, 0.11, 1.0)
    grass.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(grass)
    return ground


def shipped_material(mat, texture_path):
    """
    Dresses a material in exactly what the game will show: the texture file we ship,
    tiled at the same rate the mod tiles it, multiplied by the baked occlusion in the
    vertex colours.

    The preview used to render a generic grey procedural stone instead, which meant the
    comparison renders said nothing about how the altar actually looks in Valheim - the
    whole bench came out concrete grey in the preview while shipping a brown texture.
    """
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()

    out = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.86
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.20

    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(texture_path)
    tex.extension = "REPEAT"

    # Same tiling the mod applies, so the grain is the size it will be in game.
    mapping = tree.nodes.new("ShaderNodeMapping")
    for i in range(3):
        mapping.inputs["Scale"].default_value[i] = UV_SCALE
    coords = tree.nodes.new("ShaderNodeUVMap")

    # No occlusion multiply. The mod ships white vertex colours by default, so folding
    # the bake in here would make the preview darker than the game - the same class of
    # mistake as previewing a texture the game never loads.
    tree.links.new(coords.outputs["UV"], mapping.inputs["Vector"])
    tree.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    return mat


def dress(altar, textures):
    """Puts the shipped textures on the model's own material slots."""
    if not textures:
        altar.data.materials.clear()
        altar.data.materials.append(stone_material())
        return

    if len(altar.data.materials) == 0:
        # Single-texture altar: one slot, one sheet.
        altar.data.materials.append(
            shipped_material(bpy.data.materials.new("shipped"), list(textures.values())[0]))
        return

    for slot in altar.data.materials:
        path = textures.get(slot.name)
        if path is None:
            path = list(textures.values())[0]
            print("THRALLS_WARN no texture for material '%s'" % slot.name)
        shipped_material(slot, path)


def render_icon(name, altar):
    """
    The square, transparent icon the hammer menu and the unlock card show.

    Without one the pieces inherit the icon of whatever prefab they were cloned from - a
    ward - so the build menu offered four altars all wearing a guard stone's picture.

    Framed by the model's own bounds rather than a fixed camera, because the four shapes
    are nothing like each other in size: one is a waist-high block and one is a pole
    nearly two metres tall, and a single camera flatters one and crops the other.
    """
    os.makedirs(OUT_DIR, exist_ok=True)
    scene = bpy.context.scene

    # Bounds in world space, so the camera can be backed off to fit whatever this is.
    corners = [altar.matrix_world @ mathutils.Vector(c) for c in altar.bound_box]
    centre = sum(corners, mathutils.Vector()) / 8.0
    radius = max((c - centre).length for c in corners)

    cam_data = bpy.data.cameras.new("icon_cam")
    cam = bpy.data.objects.new("icon_cam", cam_data)
    bpy.context.collection.objects.link(cam)

    # Three-quarter view from slightly above: the angle the game uses for its own piece
    # icons, and the one that shows an altar's face and its side at once.
    direction = ICON_ANGLES.get(name, ICON_ANGLES[None])
    cam.location = centre + direction * (radius * 3.4)

    # Framed off the silhouette rather than off a sphere round the bounding box.
    #
    # The old code sat the camera at 2.55 x the bounding radius behind a fixed 60 mm
    # lens - but a 60 mm lens on a square sensor only takes in 33 degrees, so fitting a
    # sphere of that radius needs 3.48 x. Every icon was therefore cropped, which is why
    # all five ran off the bottom edge of the frame. Fitting the lens to the projected
    # outline instead is exact, and it stops a shape with one tall pole from shrinking
    # the part of it that actually reads.
    verts = [altar.matrix_world @ v.co for v in altar.data.vertices] or corners

    aim = centre.copy()
    half = 1.0
    for _ in range(4):
        cam.rotation_euler = (cam.location - aim).to_track_quat("Z", "Y").to_euler()
        bpy.context.view_layer.update()

        view = cam.matrix_world.inverted()
        us, vs = [], []
        for world in verts:
            p = view @ world
            if p.z > -1e-4:      # behind the lens; cannot be projected
                continue
            us.append(p.x / -p.z)
            vs.append(p.y / -p.z)

        if not us:
            break

        umid = (min(us) + max(us)) * 0.5
        vmid = (min(vs) + max(vs)) * 0.5
        half = max(max(us) - min(us), max(vs) - min(vs)) * 0.5

        # u and v are already per unit of depth, so multiplying by the distance turns
        # them back into world units and re-aims the camera at the silhouette's centre
        # rather than at the centre of the bounding box.
        basis = cam.matrix_world.to_3x3()
        depth = (cam.location - aim).length
        aim = aim + (basis @ mathutils.Vector((1.0, 0.0, 0.0))) * (umid * depth) \
                  + (basis @ mathutils.Vector((0.0, 1.0, 0.0))) * (vmid * depth)

    # Square render, so half the sensor is 18 mm on both axes. The margin keeps the
    # silhouette off the edge and leaves the corner clear for the upgrade star the build
    # menu draws over the icon.
    cam_data.sensor_fit = "AUTO"
    cam_data.lens = 18.0 / max(half * ICON_MARGIN, 1e-4)

    previous_cam = scene.camera
    previous_x, previous_y = scene.render.resolution_x, scene.render.resolution_y
    previous_film = scene.render.film_transparent
    previous_exposure = scene.view_settings.exposure

    # The lights stay on.
    #
    # This loop used to hide every object that was not the model or the camera, which
    # included the sun and the fill - so each icon was lit by nothing but flat world
    # ambient, and came out an evenly grey clay model with no lit side and no shadow
    # side. Measured against icons that do sit right on the build menu, ours had 9% of
    # their pixels in real shadow against about 30%, and two of the six had none at all:
    # not one pixel below luminance 60. That reads as modded from across the panel
    # whatever the shape is doing.
    #
    # The exposure was pushed to 1.85 to rescue the brightness that the missing sun cost,
    # which bleached what little modelling was left. With the sun back it comes down to
    # where the render can be judged on its own numbers again.
    # Set by measurement, not by feel. With the key at 1.7x, 0.35 put the batch at mean
    # luminance 104 against the 84 the game's own icons average; exposure is in stops, so
    # log2(84/104) is a third of one. The view transform compresses rather than
    # scaling, so that first step landed at 94 and this is the second measured step.
    scene.view_settings.exposure = -0.16

    # Ambient is pulled right down for the same reason. A world lighting the model evenly
    # from every side is exactly what fills the shadows in.
    world = scene.world
    background = world.node_tree.nodes.get("Background") if world and world.use_nodes else None
    previous_ambient = background.inputs[1].default_value if background else None
    if background:
        background.inputs[1].default_value = 0.12

    # And the broad fill is dimmed to a rim. It is a 8 m area lamp meant to keep a shape
    # readable against a landscape; at icon distance it wraps the whole model.
    # The broad fill is dimmed to a rim and the key is pushed up. Vanilla icons carry a
    # luminance spread of about 30 against the 24 this pass managed with the sun merely
    # switched back on: they are lit hard from one side, not evenly.
    previous_fill = {}
    for obj in scene.objects:
        if obj.type != "LIGHT":
            continue
        previous_fill[obj] = obj.data.energy
        if obj.data.type == "AREA":
            obj.data.energy = obj.data.energy * 0.18
        elif obj.data.type == "SUN":
            obj.data.energy = obj.data.energy * 1.7

    # Everything else goes out of shot.
    #
    # film_transparent only makes the *world* transparent, not geometry, and by the time
    # this runs render_preview has already laid down a 26 m ground plane - so the first
    # batch of icons came out fully opaque with the ground photographed behind them.
    hidden = []
    for obj in scene.objects:
        if obj is altar or obj.type == "CAMERA" or obj.type == "LIGHT":
            continue
        if not obj.hide_render:
            obj.hide_render = True
            hidden.append(obj)

    scene.camera = cam
    scene.render.resolution_x = 128
    scene.render.resolution_y = 128
    scene.render.film_transparent = True

    path = os.path.join(OUT_DIR, "%s_%s_icon.png" % (PREFIX, name))
    scene.render.filepath = path
    try:
        bpy.ops.render.render(write_still=True)
    except RuntimeError as err:
        print("THRALLS_ICON_FAILED %s (%s)" % (name, err))
        path = None

    scene.camera = previous_cam
    scene.render.resolution_x, scene.render.resolution_y = previous_x, previous_y
    scene.render.film_transparent = previous_film
    scene.view_settings.exposure = previous_exposure

    if background and previous_ambient is not None:
        background.inputs[1].default_value = previous_ambient
    for obj, energy in previous_fill.items():
        obj.data.energy = energy
    for obj in hidden:
        obj.hide_render = False

    bpy.data.objects.remove(cam, do_unlink=True)
    return path


def render_preview(name, altar=None, textures=None, shots=True):
    """
    Textured render so the shapes can be judged as they will actually appear.

    With shots=False it still dresses the model, lays the ground and sets the lighting
    and the engine, but takes no picture - which is what "--icons" wants, since an icon
    needs the lit, textured scene but not the two big renders that dominate the run.
    """
    os.makedirs(PREVIEW_DIR, exist_ok=True)

    if altar is not None:
        dress(altar, textures)
        add_ground(None)
        setup_lighting()

    cam_data = bpy.data.cameras.new("preview_cam")
    cam = bpy.data.objects.new("preview_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (6.2, -7.4, 4.6)

    target = bpy.data.objects.new("preview_target", None)
    bpy.context.collection.objects.link(target)
    target.location = (0.0, 0.0, 1.15)

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    scene = bpy.context.scene
    scene.camera = cam
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.render.film_transparent = False
    scene.view_settings.exposure = -0.45

    # EEVEE gives lit, textured stone. Fall back to the flat viewport renderer if this
    # machine cannot give Blender a GL context in background mode.
    engine = None
    for candidate in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = candidate
            engine = candidate
            break
        except TypeError:
            continue

    if engine is None:
        scene.render.engine = "BLENDER_WORKBENCH"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "SINGLE"
        shading.single_color = (0.46, 0.44, 0.41)
        shading.show_shadows = True
        shading.show_cavity = True

    if not shots:
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.objects.remove(target, do_unlink=True)
        return None

    def shoot(suffix, location, look_at):
        cam.location = location
        target.location = (0.0, 0.0, look_at)

        out = os.path.join(PREVIEW_DIR, "%s%s.png" % (name, suffix))
        scene.render.filepath = out
        try:
            bpy.ops.render.render(write_still=True)
        except RuntimeError as err:
            print("THRALLS_RENDER_FALLBACK %s (%s)" % (name, err))
            scene.render.engine = "BLENDER_WORKBENCH"
            bpy.ops.render.render(write_still=True)
        return out

    path = shoot("", (6.2, -7.4, 4.6), 1.15)
    # A close pass as well: at the wide angle every prop is twenty pixels across, which
    # is how a bench full of grey lumps passed for a bench full of tools.
    shoot("_detail", (1.9, -2.5, 2.05), 1.30)
    # And the view the player actually gets: standing next to it, eyes at 1.72 m. Both
    # shots above look down on the shape, and looking down on the pit hid a solid lid
    # sitting over the hole - add_cyl caps both ends - through every round of review it
    # went through. Anything read off an orbit view alone has not really been checked.
    shoot("_eye", (1.35, -3.6, 1.72), 0.95)
    # Square on to the front. Whether the courses of a stack line up with each other is
    # invisible from a three-quarter view and obvious from this one - the altar shipped
    # with its yaw changing sign on every layer and nobody caught it in a preview.
    shoot("_front", (0.0, -4.1, 1.05), 0.95)
    return path


# ----------------------------------------------------------------- textures

# Four quarries that must be tellable apart at a glance, so they differ in hue, in
# value, in how broken they are and in how much has grown over them - not just in tint.
# (light, dark, growth tint, stones across the sheet, growth amount, crack depth)
PALETTES = {
    # Pale dressed sandstone. Bright, warm, clean-cut, almost no growth.
    "plinth": ((0.86, 0.78, 0.62), (0.44, 0.38, 0.28), (0.52, 0.50, 0.34), 22, 0.10, 0.55),

    # Black basalt. Very dark and cold, big smooth slabs, faint blue minerals.
    "dolmen": ((0.34, 0.36, 0.44), (0.07, 0.08, 0.12), (0.20, 0.28, 0.34), 13, 0.18, 0.85),

    # Rust ironstone rubble. Strongly red-orange, small broken stones, deep seams.
    "cairn":  ((0.68, 0.38, 0.20), (0.24, 0.11, 0.06), (0.45, 0.26, 0.10), 70, 0.28, 1.00),

    # Old field stone lost under moss. Green reads before the stone does.
    "circle": ((0.50, 0.56, 0.40), (0.18, 0.22, 0.16), (0.24, 0.44, 0.18), 30, 0.85, 0.45),

    # Bone and old blood: pale yellowed ivory, stained nearly black in the hollows.
    "barrow": ((0.88, 0.84, 0.71), (0.42, 0.38, 0.30), (0.20, 0.05, 0.05), 20, 0.05, 0.50),

    # Tarred timber, dark and work-stained, in Valheim's muted range.
    "worktable": ((0.40, 0.29, 0.19), (0.13, 0.09, 0.06), (0.22, 0.16, 0.09), 1, 0.05, 0.40),

    # Dark carved slate with pale incised runes cut into it.
    "shrine": ((0.36, 0.36, 0.38), (0.11, 0.11, 0.13), (0.66, 0.62, 0.48), 1, 0.05, 0.40),
}

# 256, not 512. Valheim dresses an entire workbench in a 256 square sheet; at twice
# that, over a texture that also repeats every metre, the altar carried roughly four
# times the surface detail of the furniture beside it and read as a higher-resolution
# asset dropped into the world.
TEX_SIZE = 256


def _octave(rng, size, cells):
    """Value noise: a coarse random grid, tiled and smoothed up to full size."""
    import numpy as np

    grid = rng.random((cells, cells))
    # Wrap by one cell so the result tiles seamlessly.
    grid = np.vstack([grid, grid[:1]])
    grid = np.hstack([grid, grid[:, :1]])

    xs = np.linspace(0.0, cells, size, endpoint=False)
    x0 = np.floor(xs).astype(int)
    fx = xs - x0
    # Smoothstep for rounded blobs rather than diamonds.
    fx = fx * fx * (3.0 - 2.0 * fx)

    a = grid[np.ix_(x0, x0)]
    b = grid[np.ix_(x0 + 1, x0)]
    c = grid[np.ix_(x0, x0 + 1)]
    d = grid[np.ix_(x0 + 1, x0 + 1)]

    fxr = fx[:, None]
    fyr = fx[None, :]
    return (a * (1 - fxr) * (1 - fyr) + b * fxr * (1 - fyr)
            + c * (1 - fxr) * fyr + d * fxr * fyr)


def _voronoi_from(seeds, size):
    """
    Seamless Voronoi from explicit seed points. Returns distance to the nearest seed and
    to the second nearest; the gap between them draws the seams.
    """
    import numpy as np

    # Repeat the seeds in a 3x3 block so the pattern wraps at the edges.
    tiled = []
    for dx in (-1.0, 0.0, 1.0):
        for dy in (-1.0, 0.0, 1.0):
            tiled.append(seeds + np.array([dx, dy]))
    tiled = np.concatenate(tiled, axis=0)

    axis = np.linspace(0.0, 1.0, size, endpoint=False)
    gx, gy = np.meshgrid(axis, axis, indexing="ij")

    nearest = np.full((size, size), 10.0)
    second = np.full((size, size), 10.0)

    for px, py in tiled:
        d = np.sqrt((gx - px) ** 2 + (gy - py) ** 2)
        closer = d < nearest
        second = np.where(closer, nearest, np.minimum(second, d))
        nearest = np.where(closer, d, nearest)

    return nearest, second


def _scatter(rng, size, cells):
    return _voronoi_from(rng.random((cells, 2)), size)


def _hex_lattice(rng, size, rows, jitter=0.035):
    """Basalt cools into hexagonal columns, so the seeds sit on a hex grid."""
    import numpy as np

    seeds = []
    step = 1.0 / rows
    for r in range(rows):
        for c in range(rows):
            x = (c + (0.5 if r % 2 else 0.0)) * step
            y = r * step * 0.866
            seeds.append((x % 1.0, y % 1.0))

    seeds = np.array(seeds)
    seeds += (rng.random(seeds.shape) - 0.5) * jitter
    return _voronoi_from(seeds % 1.0, size)


def _fbm(rng, size, base, octaves=4):
    import numpy as np

    out = np.zeros((size, size))
    amp, total = 1.0, 0.0
    for i in range(octaves):
        out += _octave(rng, size, base * (2 ** i)) * amp
        total += amp
        amp *= 0.5
    return out / total


# ---------------------------------------------------------------- patterns
# Each altar gets its own pattern, not a recolour of one pattern. Colour alone was
# not enough to tell them apart.

def tex_veined(rng, size, light, dark, tint):
    """Plinth: smooth dressed sandstone, no cells at all - eroded and vein-shot."""
    import numpy as np

    grain = _fbm(rng, size, 5, 5)

    # Veins: thin bright seams following a warped field.
    warp = _fbm(rng, size, 3, 3)
    field = _fbm(rng, size, 7, 2) + warp * 0.6
    veins = 1.0 - np.clip(np.abs(field - field.mean()) / 0.035, 0.0, 1.0)

    # Shallow erosion pits.
    pits = np.clip((_fbm(rng, size, 22, 3) - 0.62) * 3.0, 0.0, 1.0)

    value = np.clip(0.55 + (grain - 0.5) * 0.75 - pits * 0.35, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]
    rgb = rgb + (tint * 0.35)[None, None, :] * veins[:, :, None]
    return rgb


def tex_columnar(rng, size, light, dark, tint):
    """Dolmen: hexagonal basalt columns with vertical striation."""
    import numpy as np

    nearest, second = _hex_lattice(rng, size, 7)
    gap = second - nearest

    grain = _fbm(rng, size, 8, 4)
    seam = np.clip(gap / (0.010 + grain * 0.012), 0.0, 1.0)

    # Striations running down the columns.
    stripes = 0.5 + 0.5 * np.sin(np.arange(size)[:, None] * 0.55 + grain * 6.0)

    value = np.clip(0.42 + (grain - 0.5) * 0.5 + stripes * 0.12, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    rgb = rgb * seam[:, :, None] + (dark * 0.25)[None, None, :] * (1.0 - seam[:, :, None])
    return rgb


def tex_cobble(rng, size, light, dark, tint):
    """Cairn: piled rounded cobbles, deep shadow between them."""
    import numpy as np

    nearest, second = _scatter(rng, size, 90)

    # Strong doming turns cells into rounded stones rather than flat plates.
    dome = np.clip(1.0 - nearest / (nearest.max() + 1e-6), 0.0, 1.0)
    dome = dome ** 0.55

    grain = _fbm(rng, size, 20, 3)
    stone_tone = _octave(rng, size, 90)

    value = np.clip(dome * (0.5 + stone_tone * 0.9) + (grain - 0.5) * 0.3, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    gap = np.clip((second - nearest) / 0.016, 0.0, 1.0)
    rgb = rgb * gap[:, :, None] + (dark * 0.2)[None, None, :] * (1.0 - gap[:, :, None])
    return rgb


def tex_moss(rng, size, light, dark, tint):
    """Circle: moss and lichen over stone that barely shows through."""
    import numpy as np

    nearest, second = _scatter(rng, size, 26)
    grain = _fbm(rng, size, 12, 4)

    stone_value = np.clip(0.45 + (grain - 0.5) * 0.6, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * stone_value[:, :, None]

    seam = np.clip((second - nearest) / 0.02, 0.0, 1.0)
    rgb = rgb * seam[:, :, None] + (dark * 0.4)[None, None, :] * (1.0 - seam[:, :, None])

    # Moss in thick irregular mats, thickest in the seams.
    mats = _fbm(rng, size, 6, 4)
    moss = np.clip((mats - 0.36) * 2.6, 0.0, 1.0)
    moss = np.clip(moss + (1.0 - seam) * 0.7, 0.0, 1.0)
    moss *= 0.55 + _fbm(rng, size, 30, 2) * 0.5

    rgb = rgb * (1.0 - moss[:, :, None] * 0.88) + tint[None, None, :] * moss[:, :, None] * 0.88
    return rgb


def tex_bone(rng, size, light, dark, tint):
    """Barrow: bone and old blood - porous, fibrous, stained."""
    import numpy as np

    grain = _fbm(rng, size, 9, 5)

    # Porous marrow-like holes.
    pores = np.clip((_fbm(rng, size, 34, 3) - 0.55) * 4.0, 0.0, 1.0)

    # Long fibrous striations, like the grain of a split bone.
    fibre = 0.5 + 0.5 * np.sin(np.arange(size)[None, :] * 0.9 + grain * 9.0)

    value = np.clip(0.68 + (grain - 0.5) * 0.5 + fibre * 0.10 - pores * 0.55, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    # Old blood pooled in the low places, dried nearly black.
    stain = np.clip((_fbm(rng, size, 5, 4) - 0.52) * 3.2, 0.0, 1.0)
    stain = np.clip(stain + pores * 0.6, 0.0, 1.0)
    rgb = rgb * (1.0 - stain[:, :, None] * 0.8) + tint[None, None, :] * stain[:, :, None] * 0.8
    return rgb


def tex_timber(rng, size, light, dark, tint):
    """Worktable: sawn timber. Long grain, knots, and dark wear along the boards."""
    import numpy as np

    # Grain stretched hard along one axis so it reads as sawn wood, not noise.
    stretched = _octave(rng, size, 3)[:, :] * 0.6 + _octave(rng, size, 48)[:, :] * 0.4
    lines = 0.5 + 0.5 * np.sin(np.arange(size)[:, None] * 0.30 + stretched * 22.0)

    fine = _fbm(rng, size, 40, 3)
    value = np.clip(0.42 + lines * 0.34 + (fine - 0.5) * 0.30, 0.0, 1.0)

    # Knots: a few dark whorls.
    knot_field, second = _scatter(rng, size, 7)
    knots = np.clip(1.0 - knot_field / 0.045, 0.0, 1.0)
    value = np.clip(value - knots * 0.55, 0.0, 1.0)

    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    # Grime worked into the timber where hands and tools have been.
    grime = np.clip((_fbm(rng, size, 4, 4) - 0.48) * 2.4, 0.0, 1.0)
    rgb = rgb * (1.0 - grime[:, :, None] * 0.45) + tint[None, None, :] * grime[:, :, None] * 0.45
    return rgb


def tex_carved(rng, size, light, dark, tint):
    """Shrine: dark slate with pale runes cut into it - straight strokes, not noise."""
    import numpy as np

    grain = _fbm(rng, size, 14, 4)
    value = np.clip(0.46 + (grain - 0.5) * 0.55, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    # Runes are cut as straight strokes, so the marks come from banded fields rather
    # than blobs: verticals crossed by a few diagonals, as in a real futhark stave.
    xs = np.arange(size)[None, :] / float(size)
    ys = np.arange(size)[:, None] / float(size)

    verticals = np.abs(((xs * 9.0 + _octave(rng, size, 4) * 0.35) % 1.0) - 0.5)
    diagonals = np.abs((((xs + ys) * 6.0 + _octave(rng, size, 3) * 0.4) % 1.0) - 0.5)

    strokes = np.minimum(verticals, diagonals * 1.4)
    cut = np.clip((0.045 - strokes) / 0.045, 0.0, 1.0)

    # Only carve where a coarse mask allows, so the runes sit in panels not everywhere.
    panel = np.clip((_fbm(rng, size, 3, 3) - 0.44) * 3.0, 0.0, 1.0)
    cut = cut * panel

    rgb = rgb * (1.0 - cut[:, :, None]) + tint[None, None, :] * cut[:, :, None]
    return rgb


def tex_planks(rng, size, light, dark, tint, boards=5):
    """
    Boards, painted rather than generated.

    The difference between this and the noise it replaces is that every feature is put
    somewhere on purpose: boards of uneven width, a shadow on one side of each seam,
    nails driven near the ends, knots with the grain bending round them, and the wear
    rubbed along the edges where hands and tools go. Tiling noise can make a surface
    busy, but only placed detail makes it read as a made object.
    """
    import numpy as np

    rows = np.arange(size)[:, None] / float(size)
    cols = np.arange(size)[None, :] / float(size)

    # Uneven boards. Equal widths are the single clearest sign of a generated texture.
    widths = rng.uniform(0.72, 1.38, boards)
    widths /= widths.sum()
    edges = np.concatenate([[0.0], np.cumsum(widths)])

    index = np.zeros((size, 1))
    within = np.zeros((size, 1))
    for b in range(boards):
        lo, hi = edges[b], edges[b + 1]
        inside = (rows >= lo) & (rows < hi)
        index = np.where(inside, float(b), index)
        within = np.where(inside, (rows - lo) / max(hi - lo, 1e-6), within)

    # Grain, but broad and soft. Side by side with a vanilla workbench the fine grain
    # here was the single loudest thing making this look like a foreign asset: Valheim's
    # wood is large flat areas with a few soft blotches, not a close-up of a plank.
    warp = _octave(rng, size, 3) * 0.9
    grain = 0.5 + 0.5 * np.sin((within * 2.2 + warp * 1.6 + index * 1.7) * math.tau)
    value = np.clip(0.66 + (grain - 0.5) * 0.15 + (_fbm(rng, size, 9, 2) - 0.5) * 0.14,
                    0.0, 1.0)

    # Each board cut from its own log. This is the main variation now, and it works at
    # arm's length where fine detail just turns to mush.
    tone = (np.sin(index * 12.9898 + 4.1) * 43758.5453) % 1.0
    value = np.clip(value + (tone - 0.5) * 0.42, 0.0, 1.0)

    # Seams: a soft gap with a little shading on one side. No nails, no saw marks - at
    # the texel density the game runs, detail that small never resolves into anything
    # but noise.
    gap = np.clip(np.minimum(within, 1.0 - within) / 0.030, 0.0, 1.0)
    value *= 0.40 + 0.60 * gap

    # Mottling across the face of every board.
    #
    # This is the thing that was missing. Valheim's planks run from near-white highlights
    # down to nearly black in the low spots, irregularly, all over each board. Varying
    # the boards against each other while leaving each one smooth - which is what this
    # did before - reads as flat painted panels no matter how far the contrast is pushed.
    mottle = _fbm(rng, size, 6, 4)
    value *= 0.62 + 0.52 * mottle

    patches = _fbm(rng, size, 12, 3)
    value *= 0.74 + 0.34 * patches

    # Weathering: darker streaks running with the grain, as water and dirt leave it.
    streak = _fbm(rng, size, 3, 2)
    value *= 0.80 + 0.28 * np.clip((streak - 0.35) * 2.2, 0.0, 1.0)

    # Sharp cracks along the grain. Thresholded rather than faded in, so they have an
    # edge - a soft dark smear reads as a stain, a hard one reads as a split in the wood.
    # Low frequency, so the contours are long and few: a handful of splits running the
    # length of a board. At higher frequency this became a net of cracks over every
    # surface and the wood read as dried mud.
    splits = _fbm(rng, size, 4, 2)
    allowed = _fbm(rng, size, 3, 2) > 0.54
    value = np.where(np.logical_and(np.abs(splits - 0.5) < 0.010, allowed), value * 0.42, value)

    value = np.clip(value, 0.0, 1.0)

    # Posterise. This is the difference between smooth and painted: every layer above is
    # interpolated noise, which gives soft gradients no matter how far the contrast is
    # pushed. Stepping the value into a handful of flat bands puts hard edges between
    # them, which is what hand-painted low resolution art actually looks like - and what
    # point sampling then keeps crisp instead of blurring back into a gradient.
    levels = 7.0
    value = np.floor(value * levels) / (levels - 1.0)
    value = np.clip(value, 0.0, 1.0)

    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    grime = np.clip((_fbm(rng, size, 4, 3) - 0.48) * 2.2, 0.0, 1.0)
    grime = np.floor(grime * 4.0) / 3.0

    return rgb * (1.0 - grime[:, :, None] * 0.34) + tint[None, None, :] * grime[:, :, None] * 0.34


def tex_parchment(rng, size, light, dark, tint):
    """Scrolls: warm paper, fibrous, foxed brown at the edges and creased across."""
    import numpy as np

    rows = np.arange(size)[:, None] / float(size)
    cols = np.arange(size)[None, :] / float(size)

    fibre = _fbm(rng, size, 90, 3)
    value = np.clip(0.80 + (fibre - 0.5) * 0.20, 0.0, 1.0)

    # Creases where it has been rolled: soft dark lines at intervals across the sheet.
    for pos in (0.22, 0.53, 0.79):
        value -= np.clip(1.0 - np.abs(cols - pos) / 0.010, 0.0, 1.0) * 0.16

    # Foxing: brown blooms, heavier towards the edges where it has been handled.
    edge = np.clip(1.0 - np.minimum(np.minimum(rows, 1.0 - rows),
                                    np.minimum(cols, 1.0 - cols)) / 0.30, 0.0, 1.0)
    blooms = np.clip((_fbm(rng, size, 7, 4) - 0.50) * 2.6, 0.0, 1.0) * (0.35 + edge)

    rgb = dark[None, None, :] + (light - dark)[None, None, :] * np.clip(value, 0.0, 1.0)[:, :, None]
    return rgb * (1.0 - blooms[:, :, None] * 0.55) + tint[None, None, :] * blooms[:, :, None] * 0.55


def tex_iron(rng, size, light, dark, tint):
    """Dark hammered iron: broad facets from the hammer, pitting, and rust in the low spots."""
    import numpy as np

    # Facets left by the hammer - big soft cells, brighter in the middle where the face
    # is flat and catching light.
    near, second = _scatter(rng, size, 9)
    facet = np.clip((second - near) / 0.06, 0.0, 1.0)
    value = np.clip(0.34 + facet * 0.34 + (_fbm(rng, size, 12, 4) - 0.5) * 0.28, 0.0, 1.0)

    # Pitting: small dark specks all over.
    pit, _ = _scatter(rng, size, 46)
    value = np.clip(value - np.clip(1.0 - pit / 0.014, 0.0, 1.0) * 0.45, 0.0, 1.0)

    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    # Rust, following the low ground rather than sprayed evenly.
    rust = np.clip((_fbm(rng, size, 6, 4) - 0.54) * 3.4, 0.0, 1.0) * (1.0 - facet * 0.6)
    return rgb * (1.0 - rust[:, :, None] * 0.75) + tint[None, None, :] * rust[:, :, None] * 0.75


def tex_granite(rng, size, light, dark, tint):
    """Plain grey granite for the whetstone and the block under the bench."""
    import numpy as np

    value = np.clip(0.46 + (_fbm(rng, size, 10, 5) - 0.5) * 0.70, 0.0, 1.0)

    # Mineral speckle, which is what stops grey noise reading as concrete.
    fleck, _ = _scatter(rng, size, 60)
    value = np.clip(value + np.clip(1.0 - fleck / 0.011, 0.0, 1.0) * 0.35, 0.0, 1.0)

    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    dirt = np.clip((_fbm(rng, size, 5, 3) - 0.55) * 3.0, 0.0, 1.0)
    return rgb * (1.0 - dirt[:, :, None] * 0.5) + tint[None, None, :] * dirt[:, :, None] * 0.5


def tex_rock(rng, size, light, dark, tint):
    """
    Rock with the value range Valheim's own stone has.

    tex_granite cannot get there and no palette can rescue it: its value is
    clip(0.46 + (fbm-0.5)*0.70), and interpolated noise sits so tightly around its mean
    that the sheet only ever spans about a quarter of the range. Pushing the palette ends
    apart just drags the whole distribution along - measured, raising the light end from
    0.80 to 1.00 lifted the *floor* from 0.304 to 0.404, which is the opposite of what
    widening means.

    So the structure is built here instead of in the endpoints: strata carry the big value
    swings, the grain swings hard on top of them, seams cut to near-black, and mineral
    catches the light. Measured against the game's stone the target is roughly p5 0.22 and
    p95 0.74 once rendered.
    """
    import numpy as np

    rows = np.arange(size)[:, None] / float(size)

    # Bedding planes. The largest structure in the sheet, and what a single flat face of
    # an altar mostly shows - without it a big slab samples one flat tone.
    warp = _fbm(rng, size, 3, 3)
    strata = 0.5 + 0.5 * np.sin((rows * 3.2 + warp * 1.1) * math.tau)

    # Centred high on purpose. Level and span are separate controls and they live in
    # different places: the palette ends set how wide the range is, this constant sets
    # where the distribution sits inside it. Chasing the level with the palette ends kept
    # dragging the span along with it - the sheet went 0.541 span at the right darkness,
    # then 0.384 at the right level, trading one for the other every time. Raising the
    # centre here lifts the mean while the swings below keep the range.
    grain = _fbm(rng, size, 9, 5)
    value = 0.60 + (strata - 0.5) * 0.44 + (grain - 0.5) * 0.90

    # Seams cut hard rather than fading. A soft dark smear reads as a stain; an edge reads
    # as a split, and it is the near-black floor the measurement said was already right.
    seam = _fbm(rng, size, 5, 3)
    value = np.where(np.abs(seam - 0.5) < 0.022, value * 0.30, value)

    # Mineral, which is where the highlights come from.
    fleck, _ = _scatter(rng, size, 52)
    value = value + np.clip(1.0 - fleck / 0.012, 0.0, 1.0) * 0.42

    value = np.clip(value, 0.0, 1.0)
    rgb = dark[None, None, :] + (light - dark)[None, None, :] * value[:, :, None]

    growth = np.clip((_fbm(rng, size, 4, 3) - 0.56) * 3.0, 0.0, 1.0)
    return rgb * (1.0 - growth[:, :, None] * 0.45) + tint[None, None, :] * growth[:, :, None] * 0.45


# What the parts of an altar are made of. Each is a separate material on the model and
# a separate texture file, which is what lets one bench be timber where it is timber
# and iron where it is iron instead of one brown wash over the whole thing.
# (pattern, light, dark, stain tint, boards)
SURFACES = {
    # Pale and warm, close to the vanilla workbench. The old values were roughly half
    # this brightness, which is why the altar read as a dark object standing next to
    # Valheim's furniture rather than as a piece of it.
    "wood":   (tex_planks, (0.72, 0.56, 0.37), (0.24, 0.17, 0.11), (0.31, 0.22, 0.13), 4),
    "timber": (tex_planks, (0.62, 0.48, 0.32), (0.18, 0.13, 0.09), (0.26, 0.18, 0.11), 3),
    "iron":   (tex_iron,   (0.82, 0.82, 0.84), (0.13, 0.13, 0.15), (0.40, 0.24, 0.13), 0),
    "stone":  (tex_granite,(0.68, 0.66, 0.62), (0.30, 0.29, 0.27), (0.38, 0.36, 0.32), 0),
    # Warm paper for the scrolls, foxed brown where it has been handled.
    "parchment": (tex_parchment, (0.88, 0.82, 0.67), (0.44, 0.39, 0.29),
                  (0.42, 0.28, 0.15), 0),
    # Old bone, not fresh. Pale enough to draw the eye, dull enough not to look like
    # birch: the first pass was the brightest thing on the altar by a long way.
    "bone":   (tex_bone,   (0.84, 0.80, 0.70), (0.42, 0.39, 0.32), (0.30, 0.18, 0.13), 0),
    # Slate with pale runes cut into it, for the backboard and the tablets. Lifted well
    # off black: the board sits in its own shadow between the posts and the occlusion
    # bake darkens it again, so a dark palette here came out as a flat black panel.
    "runestone": (tex_carved, (0.80, 0.77, 0.73), (0.42, 0.40, 0.38), (0.88, 0.84, 0.70), 0),
    # Eye sockets and the bowl's contents. Nearly black, and meant to be.
    "pitch":  (tex_granite, (0.11, 0.10, 0.10), (0.02, 0.02, 0.03), (0.06, 0.05, 0.05), 0),

    # ---- the bindstone and its upgrades ----

    # The altar's own stone. "stone" above is a pale granite meant for a whetstone and a
    # bowl; a whole altar in it came out the colour of a kerb.
    #
    # Twice been too dark. The preview lies about this in two ways at once: it renders at
    # -0.45 exposure, and shipped_material does not fold in the occlusion bake, so a sheet
    # that reads mid-grey in Blender lands as charcoal under Valheim's piece shader. In
    # game at 0.56/0.17 the whole mass came out black while the bone at 0.58 and the frost
    # at 0.46 on the same altar read correctly - so this is judged against those two now
    # rather than against the preview. Warmed a little as well: the bench's timber holds up
    # at a *lower* mean than this because it is warm and saturated, and a neutral cool grey
    # has nothing to catch the low sun with.
    # The mean was never the problem - the range was.
    #
    # This went 0.34 (read black), 0.48 (still black), 0.59 (read as white marble). A jump
    # that steep is not a brightness curve, and chasing the mean was the wrong axis all
    # along: at 0.34 the sheet spanned only 0.25-0.52, so it was a *flat* field, and a flat
    # field reads as a cutout at any level - dark at the bottom, plastic at the top.
    #
    # The diagnostic's vanilla_material pass settled it. Valheim's own stone on this same
    # mesh runs from near-black in the recesses to bright highlights on the sun-facing
    # faces, and that internal contrast is what makes it read as rock. So this is now set
    # for span, not level: a mid mean with the light and dark ends pushed well apart.
    # Measured against the game's own stone rather than guessed. The diagnostic's
    # vanilla_material pass wears Valheim's material on this exact mesh, so photographing
    # both through the same camera cancels the capture's bias and the two are directly
    # comparable. Vanilla came out mean 0.388, sd 0.155, p5 0.220, p95 0.738; this sheet at
    # 0.80/0.18 came out mean 0.297, sd 0.071, p5 0.216, p95 0.446 - the dark floor already
    # matched, and every bit of the deficit was in the highlights.
    #
    # So the floor stays and the light end goes to the top of the range. Lifting the floor
    # instead is what produced white marble two attempts ago: it is the span that makes
    # rock read as rock, and the span lives in the highlights.
    # tex_rock got the span right and the level wrong: measured against vanilla in the same
    # frame it came out mean 0.198 against 0.310, with the floor at 0.070 against 0.133. So
    # the dark end lifts while the structure stays - the previous sheet's fault was a narrow
    # distribution, not a low one, and that is now fixed in the pattern rather than here.
    # All of that tuning is now dead weight, and leaving it in place was actively harmful.
    # Once AltarVanillaGroups took darkstone over to Valheim's own stone material, this
    # sheet stopped being displayed anywhere in the game - but render_icon still rendered
    # with it, so every piece icon showed a 1.00-to-0.10 marble camouflage the player will
    # never see. On the bog stone, which is mostly slab, that turned the icon into a pale
    # blob. The sheet is now the same plain mid-grey granite the mockup script had been
    # substituting all along, so the icons show what actually ships.
    "darkstone": (tex_granite, (0.68, 0.67, 0.63), (0.30, 0.30, 0.28),
                  (0.26, 0.29, 0.20), 0),

    # The altar's stone split three ways. Six blocks all wearing one material is what
    # made it read as a single lump in game - AltarVanillaGroups hands each of these to
    # a different piece of the game's own stonework instead. These sheets are only ever
    # seen in the preview; the routing decides what ships.
    "kerbstone": (tex_rock, (0.60, 0.59, 0.55), (0.24, 0.24, 0.23),
                  (0.24, 0.26, 0.19), 0),
    "crownstone": (tex_granite, (0.74, 0.73, 0.69), (0.34, 0.34, 0.32),
                   (0.22, 0.25, 0.18), 0),

    # Bone rises with it. The preview showed a pale ring on grey stone, and that reading is
    # the point of the altar - lifting the stone alone would have put the ring darker than
    # the rock it is cut into and inverted the whole figure.
    "palebone": (tex_bone, (1.00, 0.99, 0.94), (0.62, 0.59, 0.51), (0.36, 0.24, 0.17), 0),

    # Standing swamp water for the bog stone. In "pitch" it read as a hole in the ground
    # rather than as water.
    # Lifted and widened along with the rest. Measured, this sheet sat at mean 0.167 with a
    # span of 0.049 - darker than the pitch used for eye sockets is meant to be, and flat
    # enough to read as a hole cut in the kerb rather than water standing in it.
    "guck":   (tex_granite, (0.46, 0.56, 0.32), (0.05, 0.08, 0.05), (0.14, 0.20, 0.09), 0),

    # One surface per upgrade that the others do not use. A shared palette across all
    # three is what made them read as one object in three sizes.
    # Every one of these was measured flat: rag spanned 0.067, frost 0.101, rust 0.083. A
    # narrow sheet reads as a cutout whatever its brightness - the same fault that had the
    # altar's stone looking black at one level and like white plastic at another. The ends
    # are pushed apart here so each has range to be lit by.
    "frost":  (tex_granite, (0.90, 0.92, 0.96), (0.19, 0.22, 0.28), (0.46, 0.54, 0.63), 0),
    "rag":    (tex_parchment, (0.74, 0.75, 0.62), (0.16, 0.18, 0.14), (0.20, 0.24, 0.12), 0),

    # Draugr flesh: waterlogged and going green, stained brown where it has rotted through.
    # Kept well off bone - the whole point is that it is not a skeleton - and off the guck
    # it stands in, so the hand does not sink into the puddle behind it.
    "flesh":  (tex_bone, (0.62, 0.66, 0.46), (0.13, 0.16, 0.12), (0.24, 0.16, 0.10), 0),
    "rust":   (tex_iron, (0.76, 0.50, 0.28), (0.11, 0.07, 0.04), (0.44, 0.23, 0.10), 0),
    "crystal": (tex_granite, (0.80, 1.00, 1.00), (0.10, 0.34, 0.42), (0.38, 0.84, 0.90), 0),

    # Oxblood, not tan. Against the timber it hangs from, a tan hide was within a shade
    # of the pole and the ribbons disappeared into it.
    "hide":   (tex_bone, (0.86, 0.38, 0.23), (0.15, 0.06, 0.05), (0.24, 0.11, 0.07), 0),

    # ---- the depot ----

    # Coarse undyed sacking. Added because the depot's sacks were first cut from "rag",
    # which is the pale sage the upgrade's ribbons are made of - at sack size and sack
    # shape that read as a head of garlic rather than as cloth. Brown, low contrast, and
    # duller than the timber it sits in so the load does not out-shout the piece.
    "sackcloth": (tex_parchment, (0.56, 0.49, 0.36), (0.19, 0.16, 0.11),
                  (0.30, 0.24, 0.15), 0),

    # Wet field stone under moss, for the coffer. "darkstone" is the bindstone's own
    # quarry and is deliberately bright - it has to survive Valheim's piece shader - but
    # a metre-and-a-half slab of it with nothing growing on it reads as poured concrete.
    # Green over grey is what makes it rock.
    "mosstone": (tex_rock, (0.52, 0.55, 0.44), (0.17, 0.19, 0.15), (0.26, 0.36, 0.18), 0),
}


PATTERNS = {
    "shrine": tex_carved,
    "worktable": tex_timber,
    "plinth": tex_veined,
    "dolmen": tex_columnar,
    "cairn": tex_cobble,
    "circle": tex_moss,
    "barrow": tex_bone,
}


def _write_texture(label, rgb, path):
    import numpy as np

    size = rgb.shape[0]
    rgba = np.concatenate([np.clip(rgb, 0.0, 1.0), np.ones((size, size, 1))], axis=2)

    image = bpy.data.images.new(label, width=size, height=size, alpha=True)
    image.pixels.foreach_set(rgba.reshape(-1).astype(np.float32))

    os.makedirs(OUT_DIR, exist_ok=True)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return path


def make_texture(name):
    """Writes a seamless stone texture for one altar."""
    import numpy as np

    # Palettes still carry the old cell/growth/crack numbers; the patterns set their own.
    light, dark, tint = (np.array(c) for c in PALETTES[name][:3])
    rng = np.random.default_rng(sum(ord(c) * (i + 7) for i, c in enumerate(name)))

    rgb = PATTERNS[name](rng, TEX_SIZE, light, dark, tint)
    return _write_texture("altar_%s" % name, rgb,
                          os.path.join(OUT_DIR, "%s_%s.png" % (PREFIX, name)))


def make_surface_textures(name, kinds):
    """
    One texture per surface the model uses, named so the mod can find it from the
    usemtl in the OBJ: thrall_altar_worktable_iron.png and so on.

    The first surface is also written under the plain altar name, which is what the mod
    falls back to for any face whose material it cannot match.
    """
    import numpy as np

    written = {}
    for kind in kinds:
        pattern, light, dark, tint, boards = SURFACES[kind]
        light, dark, tint = np.array(light), np.array(dark), np.array(tint)

        seed = sum(ord(c) * (i + 3) for i, c in enumerate(name + kind))
        rng = np.random.default_rng(seed)

        if boards:
            rgb = pattern(rng, TEX_SIZE, light, dark, tint, boards)
        else:
            rgb = pattern(rng, TEX_SIZE, light, dark, tint)

        written[kind] = _write_texture(
            "surface_%s_%s" % (name, kind), rgb,
            os.path.join(OUT_DIR, "%s_%s_%s.png" % (PREFIX, name, kind)))

    if kinds:
        shutil.copyfile(written[kinds[0]],
                        os.path.join(OUT_DIR, "%s_%s.png" % (PREFIX, name)))

    return written




def main():
    everything = "--all" in sys.argv
    icons_only = "--icons" in sys.argv
    wanted = list(VARIANTS) if everything else [n for n in ACTIVE if n in VARIANTS]

    for name in wanted:
        builder = VARIANTS[name]
        random.seed(20260810)
        clear_scene()

        parts = builder()
        col_path, col_count = write_colliders(parts, name)

        # Surfaces in the order the parts introduced them, so the first one is the
        # material most of the altar is made of and makes the best fallback.
        kinds = []
        for obj in parts:
            kind = obj.get("thralls_surface")
            if kind is not None and kind not in kinds:
                kinds.append(kind)

        altar = finish(parts, name)
        bake_occlusion(altar)
        obj_path = export(altar, name)

        if kinds:
            textures = make_surface_textures(name, kinds)
        else:
            textures = {None: make_texture(name)}

        png_path = render_preview(name, altar, textures, shots=not icons_only)
        icon_path = render_icon(name, altar)
        print("THRALLS_VARIANT %s verts=%d tris=%d boxes=%d surfaces=%s obj=%s png=%s icon=%s"
              % (name, len(altar.data.vertices), len(altar.data.polygons), col_count,
                 ",".join(kinds) or "-", obj_path, png_path, icon_path))

    print("THRALLS_DEFAULT %s" % DEFAULT_VARIANT)


if __name__ == "__main__":
    main()

