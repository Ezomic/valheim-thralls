"""
Candidates for the depot - the piece a crew of thralls hauls its work to.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/depot_mockup.py

Nothing here is exported. Everything lands in assets/previews/depot and no shipped
model is touched, so a mockup cannot ship by accident. Whichever one is picked gets
moved into a depot_model.py of its own, with colliders and an icon.

What the piece has to say, before it has to look like anything:

  - it is *storage*, and storage a thrall walks up to and empties a pack into. So the
    opening has to read from across a clearing, not just from arm's length.
  - it is not a chest. A chest is already in the game and the whole point of the depot
    is that it is the crew's, not yours, so it should read as a work stockpile.
  - it belongs beside the bindstone. Same quarry, same tarred timber, same bone -
    a shipping crate in a different palette would look like a different mod.
  - it has a *range*. A piece that governs a radius wants to be findable, which argues
    for at least one candidate with real height on it.

Four silhouettes, deliberately with nothing in common but the palette: wide and low,
wheeled and diagonal, squat and heavy, tall and vertical.
"""

import bpy
import math
import os
import random
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))

import altar_model as am

# Textures and renders both land here. Redirecting the module's own output directory is
# what keeps make_surface_textures from writing sheets into assets/ beside the shipped
# altar textures.
MOCK_DIR = os.path.join(ROOT, "assets", "previews", "depot")
am.OUT_DIR = MOCK_DIR
am.PREVIEW_DIR = MOCK_DIR

add_block = am.add_block
add_cyl = am.add_cyl
add_taper = am.add_taper
add_ring = am.add_ring
add_sphere = am.add_sphere
add_clutter = am.add_clutter
jitter = am.jitter


# ----------------------------------------------------------------- shared parts

def sack(name, x, y, z, size=0.30, yaw=0.0, mat="sackcloth"):
    """
    A sack slumped on its side, tied at the neck.

    First cut was a sphere standing upright with a tapered neck on top, and every one of
    them read as a head of garlic: upright plus a point equals a bulb, whatever it is
    painted. A sack full of ore does not stand up - it sags into whatever it is resting
    on, and the fold across the middle is the thing that says cloth.
    """
    parts = []
    lean = math.radians(yaw)

    lower = add_sphere(name, (size * 1.30, size * 1.05, size * 0.80),
                       (x, y, z + size * 0.36), rot_z=yaw, mat=mat)
    parts.append(lower)

    # The upper lobe sits back and smaller, so the profile steps rather than domes.
    upper = add_sphere(name + "_top", (size * 0.92, size * 0.82, size * 0.62),
                       (x - math.cos(lean) * size * 0.22,
                        y - math.sin(lean) * size * 0.22,
                        z + size * 0.78), rot_z=yaw, mat=mat)
    parts.append(upper)

    # A cord round the throat and the gathered cloth above it, lying over rather than
    # standing up.
    tie = add_cyl(name + "_tie", size * 0.30, size * 0.06,
                  (x - math.cos(lean) * size * 0.44,
                   y - math.sin(lean) * size * 0.44,
                   z + size * 0.86), sides=9, mat="rag")
    tie.rotation_euler[1] = math.radians(38.0)
    parts.append(tie)

    knot = add_taper(name + "_knot", size * 0.24, size * 0.07, size * 0.32,
                     (x - math.cos(lean) * size * 0.60,
                      y - math.sin(lean) * size * 0.60,
                      z + size * 0.92), rot_z=yaw, sides=7, mat=mat)
    knot.rotation_euler[1] = math.radians(52.0)
    parts.append(knot)
    return parts


def billets(name, x, y, z, count=4, length=0.70, yaw=0.0, mat="timber"):
    """A short stack of cut logs, ends toward the camera - firewood, plainly."""
    parts = []
    for i in range(count):
        row = i // 2
        col = i % 2
        log = add_cyl("%s_%d" % (name, i), 0.075, length,
                      (x + col * 0.17 - 0.085 + row * 0.04,
                       y + row * 0.02,
                       z + 0.075 + row * 0.145),
                      axis="y", rot_z=yaw, sides=9, mat=mat, collide=False)
        log.rotation_euler[2] += math.radians(random.uniform(-4.0, 4.0))
        parts.append(log)
    return parts


def band(name, centre, span, axis, z, mat="iron", thickness=0.045, height=0.085):
    """An iron strap. Flat, wide and thin, wrapped round timber to hold it together."""
    cx, cy = centre
    size = (span, thickness, height) if axis == "x" else (thickness, span, height)
    return add_block(name, size, (cx, cy, z), mat=mat, collide=False)


def tally(name, x, y, top_z, drop=0.34, count=3, mat="bone"):
    """
    Bone sticks on a cord - what the crew keeps its count on. The same charm hangs off
    the bindstone, and it is the cheapest way to say "this belongs to the thralls".
    """
    parts = []
    cord = add_cyl(name + "_cord", 0.012, drop, (x, y, top_z - drop / 2.0),
                   sides=5, mat=mat, collide=False)
    parts.append(cord)

    for i in range(count):
        stick = add_block("%s_%d" % (name, i), (0.035, 0.020, 0.20),
                          (x + (i - (count - 1) / 2.0) * 0.055, y - 0.01,
                           top_z - drop - 0.08),
                          rot_z=random.uniform(-8.0, 8.0), mat=mat, collide=False)
        stick.rotation_euler[1] = math.radians(random.uniform(-11.0, 11.0))
        parts.append(stick)
    return parts


def wheel(name, x, y, radius=0.42, mat="timber"):
    """
    A cart wheel: felloe ring, hub and five spokes. The ring is a torus stood on edge -
    a disc would read as a millstone, and the gaps between the spokes are most of what
    makes the silhouette interesting from the side.
    """
    parts = []
    z = radius

    rim = add_ring(name + "_rim", radius, 0.055, (x, y, z), sides=15, mat=mat)
    rim.rotation_euler[0] = math.radians(90.0)
    parts.append(rim)

    tyre = add_ring(name + "_tyre", radius + 0.03, 0.028, (x, y, z), sides=15, mat="iron")
    tyre.rotation_euler[0] = math.radians(90.0)
    parts.append(tyre)

    hub = add_cyl(name + "_hub", 0.11, 0.20, (x, y, z), axis="y", sides=9, mat=mat)
    parts.append(hub)

    for i in range(5):
        spoke = add_block("%s_spoke_%d" % (name, i), (0.055, 0.055, radius * 1.85),
                          (x, y, z), mat=mat, collide=False)
        spoke.rotation_euler[1] = math.radians(i * 36.0 + 9.0)
        parts.append(spoke)

    return parts


# ----------------------------------------------------------------- candidate A

def build_crib():
    """
    The open crib. A slatted timber bin on skids, flaring outward as it rises, heaped
    past the rim with what the crew brought in.

    The flare is the whole idea: straight walls make a box, and a box is a chest. Walls
    that lean out read as a hopper you tip a load into, which is exactly what a thrall
    does with it. Nothing crosses the opening - the rim is four separate bars, not a
    frame with a plate in it, or the eye reads a closed lid.
    """
    parts = []
    random.seed(20260814)

    # Skids first, so the whole thing sits on runners rather than in the mud.
    for side in (-1, 1):
        parts.append(add_block("skid_%d" % side, (1.66, 0.16, 0.14),
                               (0.0, side * 0.40, 0.07), rot_z=side * 1.2, mat="timber"))

    parts.append(add_block("floor", (1.52, 0.94, 0.13), (0.0, 0.0, 0.185), mat="timber"))

    # Corner posts, leaning out with the walls they carry.
    for sx in (-1, 1):
        for sy in (-1, 1):
            post = add_block("post_%d_%d" % (sx, sy), (0.15, 0.15, 1.02),
                             (sx * 0.74, sy * 0.44, 0.56), mat="timber")
            post.rotation_euler[1] = math.radians(-sx * 5.0)
            post.rotation_euler[0] = math.radians(sy * 5.0)
            jitter(post, 0.012, 1.2)
            parts.append(post)

    # Three courses of slats a side. Each course is a little wider and a little further
    # out than the one below, which is the flare.
    #
    # The gap between courses matters more than the courses do. At 0.28 apart with 0.23
    # tall boards the three merged into one slab in the render and the bin read as a
    # solid crate; daylight between the boards is the whole difference between slatted
    # and planked.
    for course, z in enumerate((0.30, 0.62, 0.94)):
        out = 0.02 + course * 0.035
        for sy in (-1, 1):
            slat = add_block("slat_x_%d_%d" % (course, sy), (1.62 + out * 2.0, 0.09, 0.19),
                             (0.0, sy * (0.46 + out), z), mat="timber")
            slat.rotation_euler[0] = math.radians(sy * 6.0)
            jitter(slat, 0.010, 1.0)
            parts.append(slat)

        for sx in (-1, 1):
            slat = add_block("slat_y_%d_%d" % (course, sx), (0.09, 0.96 + out * 2.0, 0.19),
                             (sx * (0.76 + out), 0.0, z), mat="timber")
            slat.rotation_euler[1] = math.radians(-sx * 6.0)
            jitter(slat, 0.010, 1.0)
            parts.append(slat)

    # Iron round the middle course, front and back only - the sides are hidden anyway
    # and four bands turns it into a strongbox.
    for sy in (-1, 1):
        parts.append(band("hoop_%d" % sy, (0.0, sy * 0.50), 1.72, "x", 0.62))

    # The load, standing proud of the rim so the bin reads full rather than empty.
    parts += billets("logs", -0.34, 0.02, 1.00, count=4, length=0.78, yaw=6.0)
    parts += sack("sack_a", 0.36, -0.10, 0.98, 0.30, yaw=24.0)
    parts += sack("sack_b", 0.56, 0.22, 0.96, 0.25, yaw=-38.0)
    parts += add_clutter("ore", (0.12, 0.16, 0.0), 0.26, 5, scale=1.15,
                         on_top=1.02, mat="rust")

    # A tally on the front post and a little spill at the foot: both say the place is
    # in use, and a stockpile nobody has touched looks like furniture.
    parts += tally("tally", -0.74, -0.55, 1.00)
    parts += add_clutter("spill", (0.55, -0.78, 0.0), 0.34, 4, scale=0.9, mat="rust")

    return parts


# ----------------------------------------------------------------- candidate B

def build_cart():
    """
    The hauler's cart, parked with its shafts up.

    Everything else here is a box on the ground; this is two circles and two diagonals,
    so it can never be confused with the others at a glance. Shafts up rather than down
    because a cart tipped onto its nose reads as abandoned, and because the raised pair
    puts a two-metre marker over a piece that is otherwise knee-high.
    """
    parts = []
    random.seed(20260814)

    deck_z = 0.66

    # Axle and wheels. The wheels are the silhouette, so they are big - nearly a metre
    # across, which is right for a cart a person pulls.
    parts.append(add_cyl("axle", 0.07, 1.16, (-0.06, 0.0, 0.42), axis="y",
                         sides=9, mat="iron"))
    for sy in (-1, 1):
        parts += wheel("wheel_%d" % sy, -0.06, sy * 0.56, radius=0.42)

    # Bed: two bearers along the length, then the deck across them.
    for sy in (-1, 1):
        parts.append(add_block("bearer_%d" % sy, (1.52, 0.11, 0.13),
                               (0.0, sy * 0.30, deck_z - 0.09), rot_z=sy * 0.8,
                               mat="timber"))
    parts.append(add_block("deck", (1.46, 0.84, 0.11), (0.0, 0.0, deck_z), mat="timber"))

    # Side boards and a tail board. No head board - the gap at the front is where the
    # load goes in, and it keeps the deck from reading as a closed crate.
    for sy in (-1, 1):
        board = add_block("board_%d" % sy, (1.46, 0.09, 0.30),
                          (0.0, sy * 0.41, deck_z + 0.19), mat="timber")
        board.rotation_euler[0] = math.radians(sy * 4.0)
        jitter(board, 0.010, 1.0)
        parts.append(board)

    tail = add_block("tail", (0.10, 0.86, 0.32), (0.72, 0.0, deck_z + 0.20), mat="timber")
    tail.rotation_euler[1] = math.radians(6.0)
    parts.append(tail)
    parts.append(band("tail_band", (0.72, 0.0), 0.90, "y", deck_z + 0.30))

    # The shafts, rising off the front of the bed. Multi-segment so they taper and bend
    # a little - one straight box would be a stick, which is the note the limbs rule was
    # written for.
    for sy in (-1, 1):
        base_x, base_y = -0.66, sy * 0.34
        run = [(0.55, 0.30, 0.075), (0.52, 0.44, 0.060), (0.46, 0.50, 0.046)]
        x, z = base_x, deck_z
        for i, (dx, dz, radius) in enumerate(run):
            seg = add_cyl("shaft_%d_%d" % (sy, i), radius,
                          math.hypot(dx, dz) * 1.08,
                          (x - dx / 2.0, base_y - i * 0.02, z + dz / 2.0),
                          axis="z", sides=7, mat="timber")
            seg.rotation_euler[1] = math.radians(-math.degrees(math.atan2(dz, -dx)) + 90.0)
            parts.append(seg)
            x -= dx
            z += dz

        # A cross handle between the shaft tips, which is what a hauler actually grips.
        if sy == 1:
            parts.append(add_cyl("handle", 0.048, 0.78, (-2.19, 0.0, deck_z + 1.24),
                                 axis="y", sides=7, mat="timber"))

    # The load: a banded crate, sacks, and a coil of rope over the tail.
    crate = add_block("crate", (0.56, 0.58, 0.46), (0.30, -0.04, deck_z + 0.29),
                      rot_z=7.0, mat="timber")
    parts.append(crate)
    parts.append(band("crate_band_a", (0.30, -0.04), 0.60, "x", deck_z + 0.42))
    parts.append(band("crate_band_b", (0.30, -0.04), 0.60, "x", deck_z + 0.18))

    parts += sack("sack_a", -0.24, 0.06, deck_z + 0.05, 0.30, yaw=18.0)
    parts += sack("sack_b", -0.10, -0.24, deck_z + 0.05, 0.24, yaw=-44.0)
    parts += billets("logs", -0.42, 0.20, deck_z + 0.05, count=2, length=0.64)

    rope = add_ring("rope", 0.16, 0.035, (0.62, -0.26, deck_z + 0.10), sides=13, mat="rag")
    rope.rotation_euler[1] = math.radians(8.0)
    parts.append(rope)

    parts += tally("tally", 0.74, -0.36, deck_z + 0.30, drop=0.26)
    parts += add_clutter("spill", (0.95, -0.30, 0.0), 0.30, 4, scale=0.95, mat="rust")

    return parts


# ----------------------------------------------------------------- candidate C

def build_coffer():
    """
    The stone coffer, in the bindstone's own quarry, with its timber lid shoved aside.

    This is the candidate that matches the altar family hardest: darkstone mass, iron
    straps, a skull on the corner. The lid slid back is doing all the work of saying
    "open" - a closed coffer is a coffin, and the game already has one of those.
    """
    parts = []
    random.seed(20260814)

    # A rough kerb the coffer beds into, so it does not sit on the grass like a crate.
    for i in range(7):
        angle = i * (math.tau / 7.0) + 0.4
        kerb = add_block("kerb_%d" % i,
                         (random.uniform(0.34, 0.52), random.uniform(0.26, 0.38),
                          random.uniform(0.14, 0.22)),
                         (math.cos(angle) * 0.92, math.sin(angle) * 0.72, 0.08),
                         rot_z=math.degrees(angle) + random.uniform(-18.0, 18.0),
                         mat="mosstone")
        kerb.rotation_euler[0] = math.radians(random.uniform(-7.0, 7.0))
        parts.append(kerb)

    # The mass, in three overlapping blocks rather than one.
    #
    # One clean block came out as a poured concrete tomb - and no amount of retinting
    # was going to fix that, because the tell was the geometry: a single unbroken
    # rectangle a metre and a half long has no cut face anywhere on it. Three blocks at
    # disagreeing yaws give the long sides a step and a shadow at every join, which is
    # what a stone somebody split looks like.
    for i, (size, offset, yaw) in enumerate((
            ((0.62, 0.90, 0.60), (-0.40, 0.01), 2.6),
            ((0.58, 0.86, 0.56), (0.02, -0.02), -3.1),
            ((0.60, 0.88, 0.58), (0.44, 0.02), 1.8))):
        block = add_block("body_%d" % i, size, (offset[0], offset[1], size[2] / 2.0),
                          rot_z=yaw, mat="mosstone")
        block.rotation_euler[0] = math.radians(random.uniform(-1.6, 1.6))
        parts.append(block)

    # A broken corner, knocked off and lying where it fell. One deliberate absence does
    # more for "old stone" than any amount of surface noise.
    chip = add_block("chip", (0.30, 0.34, 0.22), (-0.78, -0.36, 0.11), rot_z=-26.0,
                     mat="mosstone")
    chip.rotation_euler[1] = math.radians(13.0)
    parts.append(chip)

    # The rim, as four bars round the edge.
    #
    # This was one 1.50 x 0.96 slab laid across the top at 0.58 and described as a
    # "lip". It was a lid. It roofed the well underneath it completely, which is why two
    # rounds of moving the timber lid about did nothing - the coffer had a second,
    # invisible one, and a full-area plate over an opening is a lid whatever it is
    # called. Four bars leave the mouth open.
    for sy in (-1, 1):
        parts.append(add_block("rim_x_%d" % sy, (1.50, 0.14, 0.11),
                               (0.0, sy * 0.41, 0.58), rot_z=1.6, mat="mosstone"))
    for sx in (-1, 1):
        parts.append(add_block("rim_y_%d" % sx, (0.14, 0.96, 0.11),
                               (sx * 0.68, 0.0, 0.58), rot_z=1.6, mat="mosstone"))

    # Moss down the shaded flank, not over the top. On the ledge it filled the one part
    # of the piece that has to stay clear - the mouth - and the coffer came out looking
    # shut.
    parts += add_clutter("moss", (-0.20, 0.46, 0.0), 0.46, 4, scale=1.15,
                         on_top=0.30, mat="guck")

    # The hollow. A dark inset well below the rim, so the opening has a depth to it and
    # not just a hole punched in a face. Wider and shallower than the first cut, and it
    # now reaches the near rim: sunk in the middle of the top face it was invisible from
    # anywhere a player actually stands.
    parts.append(add_block("well", (1.06, 0.66, 0.40), (-0.20, 0.0, 0.38), rot_z=1.6,
                           mat="pitch", collide=False))

    # The lid, shoved right off the mouth and left leaning on the far end, tilted where
    # it came to rest. Squared over the opening it read as a tray sitting on a box, and
    # the coffer looked closed.
    lid = add_block("lid", (0.78, 0.92, 0.15), (0.68, 0.04, 0.72), rot_z=-7.0,
                    mat="timber")
    lid.rotation_euler[1] = math.radians(-9.0)
    parts.append(lid)
    for sy in (-1, 1):
        strap = band("lid_band_%d" % sy, (0.68 + sy * 0.18, 0.04), 0.94, "y", 0.79)
        strap.rotation_euler[1] = math.radians(-9.0)
        parts.append(strap)

    # A hasp and ring hanging off the front face, at eye level for a crouching thrall.
    parts.append(add_block("hasp", (0.14, 0.06, 0.26), (-0.30, -0.46, 0.46), mat="iron"))
    ring = add_ring("ring", 0.11, 0.024, (-0.30, -0.50, 0.30), sides=15, mat="iron")
    ring.rotation_euler[0] = math.radians(90.0)
    parts.append(ring)

    # Goods heaped out of the open end, which is the only thing that says storage rather
    # than shrine.
    parts += sack("sack_a", -0.40, -0.06, 0.56, 0.28, yaw=16.0)
    parts += sack("sack_b", -0.12, -0.18, 0.58, 0.22, yaw=-32.0)
    parts += add_clutter("ore", (-0.34, 0.16, 0.0), 0.24, 5, scale=1.1,
                         on_top=0.60, mat="rust")

    parts += am.skull("skull", (0.74, -0.36, 0.64), yaw=-34.0, scale=0.9)
    parts += tally("tally", -0.72, -0.46, 0.56, drop=0.24)
    parts += add_clutter("spill", (-0.95, -0.58, 0.0), 0.36, 5, scale=1.0, mat="mosstone")

    return parts


# ----------------------------------------------------------------- candidate D

def build_mast():
    """
    The tally mast: a bound post with a crossarm, baskets hanging off it, and a plank
    bin round its foot.

    The depot governs a radius, and a radius wants a landmark. This is the only
    candidate you can see over a hedge - and the hanging load gives it something to
    read at head height, where every other candidate is empty air.
    """
    parts = []
    random.seed(20260814)

    # Stone footing, three rough blocks the post is wedged between.
    for i in range(3):
        angle = i * (math.tau / 3.0) + 0.7
        parts.append(add_block("foot_%d" % i, (0.42, 0.32, 0.24),
                               (math.cos(angle) * 0.34, math.sin(angle) * 0.30, 0.11),
                               rot_z=math.degrees(angle), mat="darkstone"))

    # The post. Two segments with a slight kink, so it reads as a tree that was squared
    # off rather than as milled stock.
    lower = add_block("post_lower", (0.21, 0.21, 1.50), (0.0, 0.0, 0.86), mat="timber")
    lower.rotation_euler[1] = math.radians(-1.6)
    parts.append(lower)

    upper = add_block("post_upper", (0.18, 0.18, 1.20), (-0.05, 0.01, 2.12), mat="timber")
    upper.rotation_euler[1] = math.radians(2.4)
    parts.append(upper)

    parts.append(band("post_band", (-0.02, 0.0), 0.30, "x", 1.58))
    parts.append(band("post_band2", (-0.02, 0.0), 0.30, "y", 1.62))

    # The crossarm, lashed on and slightly out of true.
    arm = add_block("arm", (1.62, 0.15, 0.15), (-0.04, 0.0, 2.34), mat="timber")
    arm.rotation_euler[1] = math.radians(-2.2)
    parts.append(arm)

    for sx in (-1, 1):
        brace = add_block("brace_%d" % sx, (0.09, 0.09, 0.62),
                          (sx * 0.30, 0.0, 2.06), mat="timber")
        brace.rotation_euler[1] = math.radians(sx * 38.0)
        parts.append(brace)

    # What hangs off it: a basket on the left, a bundle of hides on the right. Both hang
    # by a visible cord - the gap rule again, a load floating under an arm reads as a
    # bug rather than as cargo.
    basket_x = -0.62
    parts.append(add_cyl("basket_cord", 0.016, 0.26, (basket_x, 0.0, 2.20),
                         sides=5, mat="rag"))
    basket = add_cyl("basket", 0.27, 0.42, (basket_x, 0.0, 1.86), sides=13,
                     mat="timber", collide=True)
    parts.append(basket)
    for i in range(3):
        hoop = add_ring("basket_hoop_%d" % i, 0.28, 0.022,
                        (basket_x, 0.0, 1.70 + i * 0.16), sides=13, mat="rag")
        parts.append(hoop)
    parts += add_clutter("basket_load", (basket_x, 0.0, 0.0), 0.16, 4, scale=1.0,
                         on_top=2.05, mat="rust")

    # On the right, a sack strung up out of the wet and a coil of rope.
    #
    # This was an oxblood hide on a frame, and in the render it was a flat red rectangle
    # brighter than anything else in the scene - the eye went to it and never reached
    # the piece. "hide" is tuned to stand out against the timber it hangs from on the
    # swamp upgrade, which is the opposite of what this needed.
    load_x = 0.58
    parts.append(add_cyl("load_cord", 0.016, 0.30, (load_x, 0.0, 2.16), sides=5, mat="rag"))
    parts += sack("hung_sack", load_x, 0.0, 1.62, 0.30, yaw=-72.0)

    # Rope in "rag" came out as a pale mint ring hanging in mid air - a lifebuoy, and
    # the brightest thing on the piece. Hemp is the colour of the sacking beside it.
    coil = add_ring("coil", 0.16, 0.040, (load_x + 0.30, -0.05, 1.98), sides=13,
                    mat="sackcloth")
    coil.rotation_euler[0] = math.radians(78.0)
    parts.append(coil)

    parts += tally("tally", 0.24, -0.08, 2.24, drop=0.42, count=4)

    # The bin at the foot: four plank walls, open topped, heaped. Small - the mast is
    # the piece, the bin is where the goods actually go.
    for sy in (-1, 1):
        wall = add_block("bin_x_%d" % sy, (1.14, 0.10, 0.46),
                         (0.0, sy * 0.42, 0.34), mat="timber")
        wall.rotation_euler[0] = math.radians(sy * 5.0)
        jitter(wall, 0.010, 1.0)
        parts.append(wall)
    for sx in (-1, 1):
        wall = add_block("bin_y_%d" % sx, (0.10, 0.94, 0.46),
                         (sx * 0.57, 0.0, 0.34), mat="timber")
        wall.rotation_euler[1] = math.radians(-sx * 5.0)
        jitter(wall, 0.010, 1.0)
        parts.append(wall)

    parts.append(band("bin_band", (0.0, -0.44), 1.22, "x", 0.44))

    parts += sack("bin_sack", -0.30, -0.06, 0.44, 0.26, yaw=22.0)
    parts += billets("bin_logs", 0.30, 0.06, 0.44, count=2, length=0.60, yaw=-8.0)
    parts += add_clutter("bin_ore", (0.06, 0.16, 0.0), 0.22, 4, scale=1.05,
                         on_top=0.50, mat="rust")
    parts += add_clutter("spill", (0.80, -0.66, 0.0), 0.32, 4, scale=0.95, mat="rust")

    return parts


# ----------------------------------------------------------------- the harness

CANDIDATES = {
    "crib": build_crib,
    "cart": build_cart,
    "coffer": build_coffer,
    "mast": build_mast,
}

# name, x offset, orbit distance, height to look at, scale rod beside it
LAYOUT = [
    ("crib", -5.4, 3.4, 0.95, True),
    ("cart", -1.8, 3.6, 1.10, True),
    ("coffer", 1.8, 3.2, 0.70, True),
    ("mast", 5.4, 4.2, 1.45, True),
]


def scale_rod(x, y):
    """A 1.8 m post with collars at 1.0 m and at the top - a Viking, and their waist."""
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


def reference_cube(x, y):
    """
    A one metre cube on the ground beside each candidate.

    The rod gives human height; this gives volume, which is the question that actually
    matters for a container - "is that bigger or smaller than a chest" cannot be
    answered off a render without something square in the frame.

    It stands *behind* the piece and it is grey. The first pass put a saturated ochre
    cube in front at +1.45 and it did exactly what a bright block in the foreground
    does: it occluded two of the four candidates and drew the eye off all of them.
    """
    mat = bpy.data.materials.get("preview_cube")
    if mat is None:
        mat = bpy.data.materials.new("preview_cube")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (0.24, 0.24, 0.23, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.95

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(x, y, 0.5))
    bpy.context.active_object.name = "reference_metre"
    bpy.context.active_object.data.materials.append(mat)


def build_all():
    am.clear_scene()

    kinds = []
    finished = []

    for name, offset, _, _, rod in LAYOUT:
        random.seed(20260814)

        parts = CANDIDATES[name]()
        for obj in parts:
            kind = obj.get("thralls_surface")
            if kind is not None and kind not in kinds:
                kinds.append(kind)

        model = am.finish(parts, name)
        model.location.x = offset
        finished.append((name, model))
        print("THRALLS_DEPOT %s verts=%d tris=%d"
              % (name, len(model.data.vertices), len(model.data.polygons)))

        if rod:
            scale_rod(offset - 1.55, 1.30)
            reference_cube(offset + 1.50, 1.45)

    textures = am.make_surface_textures("depot", kinds)
    for _, model in finished:
        am.dress(model, textures)

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
    # Blender 4.x defaults to AgX, which rolls the highlights off to white and would
    # make the iron and the bone read as the same pale grey.
    scene.view_settings.view_transform = "Standard"

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

    # The line-up, from eye height rather than from above: four pieces of very different
    # heights are only comparable from where a player stands.
    shoot("lineup.png", (0.0, -19.5, 1.72), (0.0, 0.0, 1.10), 1900, 700)

    for name, offset, distance, look_z, _ in LAYOUT:
        # Stood off far enough to hold the whole piece. At 3.9 m the cart's shafts and
        # the mast's crossarm both ran out of the top of the frame, which is no way to
        # judge a silhouette.
        shoot("eye_%s.png" % name,
              (offset + 1.35, -5.60, 1.72), (offset, 0.0, look_z * 0.80))
        shoot("three_quarter_%s.png" % name,
              (offset + distance * 0.72, -distance * 1.05, look_z + distance * 0.60),
              (offset, 0.0, look_z))


def main():
    render(build_all())
    print("THRALLS_DEPOT_DIR %s" % MOCK_DIR)


if __name__ == "__main__":
    main()
