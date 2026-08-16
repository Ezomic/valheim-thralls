"""
Four bindstone silhouettes, rendered at eye height for a pick.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/altar_designs.py

Second pass. The first four were designed around the word "altar" and it showed: a stele,
a gate, a mound and a pole are four *places of worship*, and only one of them read. The
piece is a bindstone now, and the word changes the subject entirely - the thing is a rock,
and what happens at it is a fettering, not an offering.

Which hands us the source directly. Fenrir was bound with Gleipnir to the boulder Gjoll,
and the slab Thviti was hammered down on top of it as the anchor. A rock a monster is
chained to is the literal subject of this mod, so every shape here is one stone mass with
iron driven into it, and the four differ in what the stone is doing: holding, splitting,
holed, or pinning something down.

Two things carry over from the first pass as fixed lessons:

  * Spheres cannot play rock. add_sphere is a UV sphere and at any segment count it reads
    as a potato - it sank the mound and the pole's cairn. rock() below is an icosphere at
    one subdivision with its vertices pushed about, which is 20 hard facets and reads as
    stone at a glance.

  * Concentric anything reads as a wedding cake before it reads as rough, however hard it
    is jittered. No stacked discs of falling radius anywhere in this file.

OUT_DIR and PREVIEW_DIR are both redirected into assets/previews/designs, so a run cannot
overwrite a shipped model, texture or icon.
"""

import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import bpy
import altar_model as am

OUT = os.path.join(am.ROOT, "assets", "previews", "designs")
am.OUT_DIR = OUT
am.PREVIEW_DIR = OUT

# Eye height, per the house rule: 1.7m up, 42mm. Backed off to just over 4m rather than
# the usual 3 - at 3m a two metre piece walks out of the top of a square frame, and a
# design cannot be judged on a silhouette that is cropped.
EYE = (2.05, -3.55, 1.70)
LOOK = 0.85
LENS = 42.0
SIZE = 620

SEED = 20260816
TRI_CAP = 10000


# ----------------------------------------------------------------- rock

def rock(name, size, location, rot_z=0.0, rough=0.16, mat="kerbstone", seed=None):
    """
    A boulder: an icosphere at one subdivision, vertices shoved along their own normals.

    Twenty faces and every edge hard, which is what separates stone from a bean. A UV
    sphere cannot do this at any segment count - its quads run in neat rings round a pole
    and the eye finds the rings immediately.
    """
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name

    if seed is not None:
        am.random.seed(seed)

    for vert in obj.data.vertices:
        push = 1.0 + am.random.uniform(-rough, rough)
        vert.co *= push

    obj.scale = (size[0] / 2.0, size[1] / 2.0, size[2] / 2.0)
    obj.rotation_euler[2] = math.radians(rot_z)
    obj.rotation_euler[0] = math.radians(am.random.uniform(-9.0, 9.0))
    obj.rotation_euler[1] = math.radians(am.random.uniform(-9.0, 9.0))
    obj["thralls_collide"] = True
    return am.surface(obj, mat)


def fetter(name, a, b, radius=0.082, mat="iron"):
    """
    A short chain whose links actually touch.

    am.chain spaces its links evenly over whatever distance it is given, so a long run
    comes out as loose washers lying in a line. Links are counted from the distance
    instead, so consecutive rings overlap and read as linked.

    Two numbers are set by cost rather than by looks. A torus is 96 triangles and comes
    out of the bevel pass at roughly 790, so a chain is the most expensive thing per
    metre in this whole file - a 1.4m run to the ground took one design from 9,240
    triangles to 19,568 on its own. Hence heavy links rather than fine ones, and short
    runs: a half metre of chain hanging off a ring says fettered just as clearly as a
    length of it puddled on the floor, and costs a fifth as much.
    """
    ax, ay, az = a
    bx, by, bz = b
    span = math.sqrt((bx - ax) ** 2 + (by - ay) ** 2 + (bz - az) ** 2)

    links = max(3, int(round(span / (radius * 1.25))))
    return am.chain(name, a, b, links=links, radius=radius, mat=mat)


def staple(name, a, b, radius=0.052, mat="iron"):
    """An iron bar driven between two points - a staple, a peg, a fetter's anchor."""
    ax, ay, az = a
    bx, by, bz = b
    mid = ((ax + bx) * 0.5, (ay + by) * 0.5, (az + bz) * 0.5)

    dx, dy, dz = bx - ax, by - ay, bz - az
    length = math.sqrt(dx * dx + dy * dy + dz * dz)

    bpy.ops.mesh.primitive_cylinder_add(vertices=7, radius=radius, depth=length,
                                        location=mid)
    obj = bpy.context.active_object
    obj.name = name

    # Point the cylinder's +Z down the a->b vector.
    obj.rotation_euler[1] = math.acos(max(-1.0, min(1.0, dz / max(length, 1e-6))))
    obj.rotation_euler[2] = math.atan2(dy, dx) + math.radians(90.0)
    obj["thralls_collide"] = False
    return am.surface(obj, mat)


# ----------------------------------------------------------------- the designs

def build_fetter():
    """
    Gjoll: one boulder with a ring driven into it and a chain lying slack.

    The plainest reading of the name there is, and the one with the least to go wrong -
    a rock is a rock at any distance and in any biome. Its whole silhouette is a single
    rounded mass, so the iron has to carry the meaning, which is why the chain runs out
    across the ground rather than staying tidy: slack chain says something was held.
    """
    parts = []

    parts.append(rock("boulder", (1.72, 1.44, 1.55), (0.0, 0.0, 0.60),
                      rot_z=24.0, rough=0.19, mat="crownstone", seed=SEED + 1))

    # Thviti, the slab hammered down on top as the anchor.
    slab = am.add_block("thviti", (0.74, 0.60, 0.38), (0.05, -0.08, 1.24),
                        rot_z=-13.0, mat="kerbstone")
    slab.rotation_euler[1] = math.radians(-6.0)
    parts.append(slab)

    # The ring the fetter runs through, driven through the slab into the rock.
    ring = am.add_ring("fetter_ring", 0.13, 0.030, (0.05, -0.30, 1.36), sides=11,
                       mat="iron")
    ring.rotation_euler[0] = math.radians(72.0)
    parts.append(ring)

    parts.append(staple("fetter_peg", (0.05, -0.22, 1.44), (0.05, -0.10, 1.16)))

    # Chain out across the ground, falling as it goes. Nothing at the far end: what was
    # on it is walking around your base.
    parts.extend(fetter("chain", (0.05, -0.40, 1.30), (-0.16, -0.72, 0.78)))

    # Two smaller rocks shouldered against the big one, so it sits in the ground rather
    # than on it.
    for i, (angle, size) in enumerate(((38.0, 0.74), (196.0, 0.62))):
        rad = math.radians(angle)
        parts.append(rock("shoulder_%d" % i, (size, size * 0.82, size * 0.66),
                          (math.cos(rad) * 0.92, math.sin(rad) * 0.86, 0.16),
                          rot_z=angle, seed=SEED + 10 + i))

    parts.extend(am.skull("skull", (-0.62, -0.72, 0.20), yaw=34.0, scale=0.95))

    return parts


def build_cleft():
    """
    A standing stone split down the middle, held together by iron.

    The outline is a blade with a slot cut out of it, which reads at any distance and is
    the only shape here with a hole in its upper half. The staples bridging the crack are
    the design: the stone is itself something being bound, and whatever comes out of it
    comes out of the gap.
    """
    parts = []

    # Two halves leaning apart from a shared foot. Not one block with a notch - the gap
    # has to run all the way through or it is a groove.
    for i, (side, height, lean) in enumerate(((-1.0, 1.94, -4.5), (1.0, 1.72, 5.5))):
        half = am.add_block("half_%d" % i, (0.46, 0.52, height),
                            (side * 0.30, 0.0, height * 0.5),
                            rot_z=side * 4.0, mat="crownstone")
        half.rotation_euler[1] = math.radians(lean)
        parts.append(half)

        # A shoulder low down where the two are still one stone.
        parts.append(rock("root_%d" % i, (0.78, 0.72, 0.52),
                          (side * 0.34, 0.0, 0.16), rot_z=side * 30.0,
                          seed=SEED + 20 + i))

    # Three staples across the crack, driven in at angles that do not agree.
    for i, (z, span) in enumerate(((0.86, 0.40), (1.28, 0.34), (1.58, 0.28))):
        parts.append(staple("staple_%d" % i,
                            (-span, -0.24 - i * 0.02, z),
                            (span, -0.24 - i * 0.02, z + am.random.uniform(-0.07, 0.07))))

    # A chain hanging down through the gap, anchored at the top.
    parts.extend(fetter("hang", (0.0, -0.12, 1.58), (0.02, -0.16, 1.14), radius=0.07))

    parts.append(am.add_block("offer", (0.66, 0.50, 0.22), (0.0, -0.72, 0.11),
                              rot_z=11.0, mat="kerbstone"))

    for i in range(4):
        angle = math.radians(i * 90.0 + 41.0)
        parts.append(rock("spall_%d" % i, (0.34, 0.28, 0.22),
                          (math.cos(angle) * 1.05, math.sin(angle) * 0.82, 0.07),
                          rot_z=angle * 30.0, seed=SEED + 30 + i))

    return parts


def build_holed():
    """
    A holed stone stood on edge, the fetter passed through it.

    A ring of rock is the most distinctive outline any of these can have - it is the only
    one you could identify from its shadow. Holed stones are real Norse and Scots
    practice, oaths were sworn through them and things were passed through to change what
    they were, which is exactly what this piece does to a corpse.
    """
    parts = []

    # The stone as eight blocks round a circle, which leaves a real hole rather than a
    # torus. A torus reads as a machined washer: perfectly round section, no facets.
    hole_r = 0.40
    ring_r = 0.66
    for i in range(9):
        angle = math.radians(i * (360.0 / 9.0) + 8.0)
        thick = am.random.uniform(0.30, 0.44)
        block = am.add_block("ring_%d" % i, (thick, 0.42, ring_r - hole_r + 0.26),
                             (math.cos(angle) * ring_r, 0.0,
                              0.86 + math.sin(angle) * ring_r),
                             rot_z=0.0, mat="crownstone")
        block.rotation_euler[1] = -angle + math.radians(am.random.uniform(-7.0, 7.0))
        parts.append(block)

    # Foot: the stone is set into a heap, not balanced on its rim.
    for i, (dx, size) in enumerate(((-0.52, 0.82), (0.48, 0.74), (0.02, 0.66))):
        parts.append(rock("foot_%d" % i, (size, size * 0.86, size * 0.62),
                          (dx, am.random.uniform(-0.14, 0.14), 0.14),
                          rot_z=i * 47.0, seed=SEED + 40 + i))

    # The fetter through the hole, hanging out of the bottom of it.
    parts.extend(fetter("through", (0.0, -0.17, 1.24), (-0.04, -0.22, 0.76), radius=0.072))

    ring = am.add_ring("eye", 0.15, 0.032, (0.0, -0.18, 1.24), sides=11, mat="iron")
    ring.rotation_euler[0] = math.radians(90.0)
    parts.append(ring)

    parts.extend(am.skull("skull", (0.54, -0.44, 0.16), yaw=-28.0, scale=0.92))

    return parts


def build_pinned():
    """
    A low broad slab with four rings at its corners and something still chained to it.

    The only horizontal in the set - it reads as a table or a threshold rather than a
    monument, and it is the one you could actually walk up to and put a trophy on. What
    makes it a bindstone rather than a bench is the iron: four corners, four chains, all
    of them going to the middle.
    """
    parts = []

    # The slab, as three overlapping blocks so its edge is broken rather than milled.
    for i, (dx, dy, w, d, rz) in enumerate(((0.0, 0.0, 1.55, 1.15, -4.0),
                                            (-0.42, 0.16, 0.86, 0.92, 13.0),
                                            (0.46, -0.12, 0.78, 0.84, -17.0))):
        block = am.add_block("slab_%d" % i, (w, d, 0.34), (dx, dy, 0.30),
                             rot_z=rz, mat="crownstone")
        block.rotation_euler[1] = math.radians(am.random.uniform(-2.5, 2.5))
        parts.append(block)

    # Rocks propping it, visible under the overhang.
    # Three props, not four. The fourth sat behind the slab where nothing could see it
    # and cost the last three hundred triangles over the cap.
    for i, (dx, dy) in enumerate(((-0.58, 0.34), (0.62, 0.30), (0.54, -0.32))):
        parts.append(rock("prop_%d" % i, (0.48, 0.44, 0.34), (dx, dy, 0.10),
                          rot_z=i * 63.0, seed=SEED + 50 + i))

    # Four rings at the corners, and four chains converging on the middle.
    corners = ((-0.62, 0.40), (0.64, 0.36), (-0.58, -0.42), (0.60, -0.38))
    for i, (cx, cy) in enumerate(corners):
        ring = am.add_ring("corner_%d" % i, 0.11, 0.026, (cx, cy, 0.48), sides=6,
                           mat="iron")
        ring.rotation_euler[0] = math.radians(am.random.uniform(60.0, 90.0))
        parts.append(ring)

        # Only one of the four is chained. Runs converging on one point from every
        # corner read as a diagram rather than a restraint, and chain is by far the most
        # expensive thing per metre here - four runs put this model at 21,496 triangles
        # against a cap of 10,000, and two still left it at 14,296.
        if i == 0:
            parts.extend(fetter("pull_%d" % i, (cx, cy, 0.50),
                                (cx * 0.42, cy * 0.42, 0.50), radius=0.062))

    # The thing being held down, in the middle where the chains meet.
    parts.extend(am.skull("held", (0.0, -0.06, 0.50), yaw=6.0, scale=1.25))

    # A short standing stone at the back, so the piece has something above knee height
    # and does not vanish behind a fence.
    back = am.add_block("marker", (0.52, 0.30, 1.05), (0.16, 0.68, 0.56), rot_z=-9.0,
                        mat="crownstone")
    back.rotation_euler[0] = math.radians(7.0)
    parts.append(back)

    return parts


DESIGNS = [
    ("e_fetter", "boulder, slab and a slack chain (Gjoll)", build_fetter),
    ("f_cleft",  "split stone stapled back together", build_cleft),
    ("g_holed",  "holed stone on edge, fetter through it", build_holed),
    ("h_pinned", "low slab, four corner rings, chained down", build_pinned),
]


# ----------------------------------------------------------------- rendering

def reference_cube():
    """
    A 1m cube beside the piece. Without one every render is scaleless and a design gets
    picked that turns out to be head height in game.
    """
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(1.45, 0.60, 0.50))
    cube = bpy.context.active_object
    cube.name = "reference_metre"

    mat = bpy.data.materials.new("reference")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (0.52, 0.14, 0.12, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.85
    cube.data.materials.append(mat)
    return cube


def shoot(name):
    """One eye-height frame, through the shipped lighting and ground."""
    scene = bpy.context.scene

    cam_data = bpy.data.cameras.new("design_cam")
    cam_data.lens = LENS
    cam = bpy.data.objects.new("design_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = EYE

    target = bpy.data.objects.new("design_target", None)
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
    return path


def main():
    os.makedirs(OUT, exist_ok=True)

    for name, label, builder in DESIGNS:
        am.random.seed(SEED)
        am.clear_scene()

        parts = builder()

        kinds = []
        for obj in parts:
            kind = obj.get("thralls_surface")
            if kind is not None and kind not in kinds:
                kinds.append(kind)

        altar = am.finish(parts, name)
        am.bake_occlusion(altar)

        textures = am.make_surface_textures(name, kinds) if kinds \
            else {None: am.make_texture(name)}

        # The cube goes in after the dressing pass so it cannot be textured with stone.
        am.render_preview(name, altar, textures, shots=False)
        reference_cube()

        tris = sum(max(0, len(p.vertices) - 2) for p in altar.data.polygons)
        path = shoot(name)

        flag = "  ** OVER CAP **" if tris > TRI_CAP else ""
        print("THRALLS_DESIGN %s | %s | tris=%d | %s%s"
              % (name, label, tris, path, flag))


if __name__ == "__main__":
    main()
