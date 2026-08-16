"""
Four fresh altar silhouettes, rendered at eye height for a pick.

Run headless:
    "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --python tools/altar_designs.py

This is a redesign, not a variation pass - altar_variations.py already covers "the
shipped bindstone with one thing changed", and the shipped bindstone is what is being
replaced. Each shape here has a genuinely different outline: a blade, a doorway, a dome
and a diagonal. If two of them read the same in silhouette there is only one design.

What they are built against is a rip rather than a memory. BossStone_Eikthyr came out of
the running game as a carved standing stone 3 x 4.53 x 1.64m wearing Custom/StaticRock -
one albedo, a moss texture the shader blends in from above, and the runes as a *separate*
emissive sheet. So vanilla's answer to "a stone with writing on it" is a carved slab with
moss in its lee, not a machined block. The shipped bindstone is a flat grey box with horns
on it, and that is the gap being closed.

Sizes are held near 2m. The boss stone is 4.5m tall because it is a boss altar placed once
per world; this is a piece you put in a base and stand next to, and it goes in rows.

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
# the usual 3 - at 3m a 2.2m piece walks straight out of the top of a square frame, and a
# design cannot be judged on a silhouette that is cropped.
EYE = (2.05, -3.55, 1.70)
LOOK = 1.00
LENS = 42.0
SIZE = 620

# Everything shares one seed so two designs never differ because of jitter.
SEED = 20260816

# A buildable piece is capped at 10,000 triangles and gets placed in rows, so the number
# is paid per copy in view rather than once.
TRI_CAP = 10000


# ----------------------------------------------------------------- the designs

def build_stele():
    """
    One carved slab standing on end, the boss stone read down to piece scale.

    A single vertical blade, so the outline is unmistakable from any angle - which is
    the thing the shipped box does not have. The offering plate is deliberately low and
    forward: it gives the eye somewhere to land that is not the top of the stone, and it
    is where you would actually put something down.
    """
    parts = []

    # Half-buried footing stones. The slab wants to look driven into the ground rather
    # than stood on it, and a kerb that overlaps the shaft is what sells that.
    for i in range(6):
        angle = math.radians(i * 60.0 + 15.0)
        dist = am.random.uniform(0.46, 0.62)
        stone = am.add_block("foot_%d" % i, (0.40, 0.34, 0.26),
                             (math.cos(angle) * dist, math.sin(angle) * dist, 0.09),
                             rot_z=math.degrees(angle) + am.random.uniform(-12, 12),
                             mat="kerbstone")
        am.jitter(stone, 0.03, 5.0)
        parts.append(stone)

    # The shaft, as three courses of nearly the same width rather than a wide box with a
    # narrow one balanced on it. The first pass stepped 0.98 down to 0.84 and the step
    # landed at eye level: it read as a chimney with a shoulder, because a visible ledge
    # halfway up a stone is the one thing that says two objects rather than one. Each
    # course now overlaps the one below by a third of its height and differs only in yaw.
    courses = ((1.02, 0.36, 0.86, 0.43, -3.0),
               (0.96, 0.34, 0.80, 1.12, 4.5),
               (0.88, 0.32, 0.72, 1.72, -6.0))

    for i, (w, d, h, z, rz) in enumerate(courses):
        course = am.add_block("shaft_%d" % i, (w, d, h), (0.0, -i * 0.05, z),
                              rot_z=rz, mat="crownstone")
        course.rotation_euler[0] = math.radians(4.5)
        course.rotation_euler[1] = math.radians(am.random.uniform(-2.0, 2.0))
        parts.append(course)

    # A broken head rather than a flat cut: one wedge sheared off the top corner, sunk
    # far enough into the last course to be part of it.
    head = am.add_block("head", (0.62, 0.32, 0.26), (-0.14, -0.16, 2.04),
                        rot_z=11.0, mat="crownstone")
    head.rotation_euler[1] = math.radians(-9.0)
    parts.append(head)

    # Three bone bosses pegged into the face. The carved runes live in the runestone
    # texture; these are what catches the light and says the stone is *used*.
    for i, (bx, bz) in enumerate(((-0.22, 1.42), (0.20, 1.10), (-0.06, 0.78))):
        parts.append(am.add_sphere("boss_%d" % i, (0.15, 0.10, 0.15),
                                   (bx, -0.20, bz), segments=8, rings=5, mat="bone"))

    # Offering plate: a low disc on a stub, standing clear in front of the slab.
    parts.append(am.add_block("plate_foot", (0.34, 0.34, 0.26), (0.0, -0.78, 0.13),
                              rot_z=18.0, mat="kerbstone"))
    plate = am.add_drum("plate", 0.40, 0.12, 0.26, sides=11, taper=0.98)
    plate.location.y = -0.78
    am.surface(plate, "darkstone")
    parts.append(plate)

    return parts


def build_gate():
    """
    Two uprights and a lintel: a doorway you bind something through.

    The outline is a portal, which is as far from a blade as the set gets, and the gap
    between the posts is the whole design - it frames whatever hangs in it, and a piece
    with a hole in it reads at distance where a solid mass does not.
    """
    parts = []

    parts.append(am.add_block("sill", (1.90, 0.72, 0.20), (0.0, 0.0, 0.10),
                              rot_z=2.0, mat="kerbstone"))

    # Uprights lean out slightly at the top, which is what stops a rectangle reading as
    # a door frame someone hung.
    for i, x in enumerate((-0.66, 0.66)):
        post = am.add_block("post_%d" % i, (0.36, 0.40, 1.62), (x, 0.0, 0.94),
                            rot_z=am.random.uniform(-5, 5), mat="crownstone")
        post.rotation_euler[1] = math.radians(-3.5 if x < 0 else 3.5)
        parts.append(post)

        # A collar where each post meets the sill, so the join is a joint and not a seam.
        parts.append(am.add_block("collar_%d" % i, (0.48, 0.50, 0.20), (x, 0.0, 0.26),
                                  rot_z=am.random.uniform(-8, 8), mat="kerbstone"))

    lintel = am.add_block("lintel", (1.98, 0.46, 0.34), (0.0, 0.0, 1.86), rot_z=-2.5,
                          mat="crownstone")
    lintel.rotation_euler[1] = math.radians(1.8)
    parts.append(lintel)

    # The skull hangs in the opening on a short cord - the thing the gap is for.
    parts.append(am.add_cyl("cord", 0.022, 0.34, (0.05, 0.0, 1.55), sides=5, mat="rag"))
    parts.extend(am.skull("hung", (0.05, 0.0, 1.16), yaw=-8.0, scale=1.15))

    # Offering stone under it, off centre so the piece is not symmetrical twice over.
    parts.append(am.add_block("offer", (0.62, 0.50, 0.26), (-0.10, -0.34, 0.30),
                              rot_z=13.0, mat="darkstone"))

    for i in range(4):
        angle = math.radians(i * 90.0 + 38.0)
        parts.append(am.add_block("tumble_%d" % i, (0.30, 0.26, 0.20),
                                  (math.cos(angle) * 1.15, math.sin(angle) * 0.78, 0.09),
                                  rot_z=am.random.uniform(0, 90), mat="kerbstone"))

    return parts


def build_howe():
    """
    A burial mound with a carved slab leaning out of it.

    Norse practice for reaching the dead was utiseta, sitting out on a howe to speak with
    whoever lies under it. So the mass is low and wide and the only vertical is the stone
    - the opposite reading of the same subject to the stele, and a dome in silhouette.
    """
    parts = []

    # One squashed dome, not a stack of discs. Four concentric drums of falling radius is
    # a wedding cake however hard they are jittered - the tiers are concentric and the eye
    # reads concentric long before it reads rough. A mound is a single mass.
    parts.append(am.add_sphere("mound", (2.45, 2.05, 1.44), (0.0, 0.0, 0.30),
                               segments=13, rings=7, mat="mosstone"))

    # Boulders half sunk into its flank at heights that have nothing to do with each
    # other, which breaks the dome up again without tiering it.
    for i in range(7):
        angle = math.radians(i * (360.0 / 7.0) + 22.0)
        dist = am.random.uniform(0.70, 1.00)
        parts.append(am.add_sphere("boulder_%d" % i,
                                   (am.random.uniform(0.40, 0.62),
                                    am.random.uniform(0.36, 0.54),
                                    am.random.uniform(0.30, 0.46)),
                                   (math.cos(angle) * dist, math.sin(angle) * dist,
                                    am.random.uniform(0.12, 0.54)),
                                   rot_z=am.random.uniform(0, 90),
                                   segments=8, rings=5, mat="kerbstone"))

    # Kerb round the foot, the way a real howe is retained. Seven at uneven spacing -
    # eleven evenly spaced came out as the teeth of a cog.
    for i in range(7):
        angle = math.radians(i * (360.0 / 7.0) + am.random.uniform(-16.0, 16.0))
        dist = am.random.uniform(1.14, 1.32)
        stone = am.add_block("kerb_%d" % i,
                             (am.random.uniform(0.26, 0.40),
                              am.random.uniform(0.22, 0.32), 0.26),
                             (math.cos(angle) * dist, math.sin(angle) * dist, 0.09),
                             rot_z=math.degrees(angle), mat="kerbstone")
        am.jitter(stone, 0.03, 8.0)
        parts.append(stone)

    z = 1.00

    # The stone at the head of the mound, leaning back out of it under its own age.
    slab = am.add_block("slab", (0.86, 0.28, 1.52), (0.0, 0.72, 0.86), rot_z=-7.0,
                        mat="crownstone")
    slab.rotation_euler[0] = math.radians(15.0)
    parts.append(slab)

    parts.append(am.add_block("slab_wedge", (0.62, 0.44, 0.30), (0.0, 0.60, 0.22),
                              rot_z=5.0, mat="kerbstone"))

    # Blood bowl set into the crown, as a foot and a rim of rough stones rather than a
    # turned basin - a lathe-true bowl is the one shape that never appears in Valheim.
    bowl_z = z - 0.04
    foot = am.add_drum("bowl_foot", 0.26, 0.14, bowl_z, sides=9, taper=0.86)
    am.surface(foot, "darkstone")
    parts.append(foot)

    for i in range(7):
        angle = math.radians(i * (360.0 / 7.0) + 14.0)
        parts.append(am.add_block("bowl_rim_%d" % i, (0.17, 0.12, 0.17),
                                  (math.cos(angle) * 0.30, math.sin(angle) * 0.30,
                                   bowl_z + 0.16),
                                  rot_z=math.degrees(angle), mat="darkstone"))

    parts.append(am.add_sphere("blood", (0.34, 0.34, 0.08),
                               (0.0, 0.0, bowl_z + 0.19), segments=11, rings=4,
                               mat="pitch"))

    return parts


def build_pole():
    """
    A nithing pole: a stake driven into a cairn with a skull on top.

    A leaning diagonal, which none of the other three is, and the only one whose mass is
    mostly air. It is also the most literally Norse of the set - a nidstang was raised to
    curse, with a horse's head fixed facing the one it was meant for, which is about as
    close to "binding something to work for you" as the sources get.
    """
    parts = []

    # Cairn socket, as a heap of boulders rather than three stacked drums. Concentric
    # discs of falling radius read as a wedding cake at any roughness, which is the same
    # mistake the mound made - a pile is a pile because no two stones agree.
    z = 0.60
    for i in range(9):
        angle = math.radians(i * 40.0 + 17.0)
        ring = 0.62 - (i % 3) * 0.16
        parts.append(am.add_sphere("cairn_%d" % i,
                                   (am.random.uniform(0.34, 0.52),
                                    am.random.uniform(0.30, 0.46),
                                    am.random.uniform(0.26, 0.40)),
                                   (math.cos(angle) * ring, math.sin(angle) * ring,
                                    am.random.uniform(0.08, 0.44)),
                                   rot_z=am.random.uniform(0, 90),
                                   segments=8, rings=5, mat="kerbstone"))

    # The stake. Two tapering segments overlapping, so it thins towards the head rather
    # than being one rotated stick. Lowered from 1.86 to 1.52 above the cairn: at the old
    # height the skull sat exactly on the top edge of the frame and the render came back
    # looking like a bare pole.
    lean = 11.0
    lower = am.add_taper("stake_lower", 0.115, 0.092, 1.24, (0.0, 0.0, z + 0.42),
                         mat="timber")
    lower.rotation_euler[0] = math.radians(lean)
    parts.append(lower)

    drift = math.tan(math.radians(lean)) * 1.04
    upper = am.add_taper("stake_upper", 0.095, 0.072, 0.86, (0.0, -drift, z + 1.22),
                         mat="timber")
    upper.rotation_euler[0] = math.radians(lean)
    parts.append(upper)

    # One crossbar, not two. Two bars at different heights on an upright is a crucifix -
    # unmistakably so in a render, and about as wrong as a Norse altar can read.
    bar_h = 1.02
    bar_off = math.tan(math.radians(lean)) * bar_h
    bar = am.add_cyl("bar", 0.040, 0.66, (0.0, -bar_off, z + bar_h), axis="x",
                     rot_z=13.0, sides=7, mat="timber")
    bar.rotation_euler[1] += math.radians(-7.0)
    parts.append(bar)
    parts.append(am.add_cyl("lash", 0.058, 0.14, (0.0, -bar_off, z + bar_h),
                            axis="x", rot_z=13.0, sides=7, mat="rag"))

    # Two cords hanging off the bar with charms on them, which is what makes the bar read
    # as something things are tied to rather than a spar.
    for i, bx in enumerate((-0.24, 0.21)):
        drop = 0.26 + i * 0.09
        parts.append(am.add_cyl("cord_%d" % i, 0.016, drop,
                                (bx, -bar_off, z + bar_h - drop * 0.5),
                                sides=5, mat="rag"))
        parts.append(am.add_sphere("charm_%d" % i, (0.11, 0.08, 0.15),
                                   (bx, -bar_off, z + bar_h - drop),
                                   segments=7, rings=5, mat="bone"))

    # The head, facing out along -y so it looks at whoever walks up.
    head_off = math.tan(math.radians(lean)) * 1.52
    parts.extend(am.skull("head", (0.0, -head_off, z + 1.52), yaw=0.0, scale=1.35))

    # A flat stone at the foot to put an offering on, and loose spill around it.
    parts.append(am.add_block("offer", (0.60, 0.46, 0.18), (0.10, -0.86, 0.09),
                              rot_z=-14.0, mat="darkstone"))
    parts.extend(am.add_clutter("spill", (0.0, -0.55, 0.0), 0.95, 5, scale=0.9,
                                on_top=0.0, mat="kerbstone"))

    return parts


DESIGNS = [
    ("a_stele", "carved slab on end, plate at its foot", build_stele),
    ("b_gate",  "two posts and a lintel, skull hung in the gap", build_gate),
    ("c_howe",  "low mound, stone leaning out of it, blood bowl", build_howe),
    ("d_pole",  "stake in a cairn, skull facing you", build_pole),
]


# ----------------------------------------------------------------- rendering

def reference_cube():
    """
    A 1m cube beside the piece. Without one every render is scaleless and a design gets
    picked that turns out to be head height in game.
    """
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(1.35, 0.55, 0.50))
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

        # Dress and light through the shipped path, then add the cube and take the frame.
        # The cube goes in after render_preview so the dressing pass cannot texture it.
        am.render_preview(name, altar, textures, shots=False)
        reference_cube()

        tris = sum(max(0, len(p.vertices) - 2) for p in altar.data.polygons)
        path = shoot(name)

        flag = "  ** OVER CAP **" if tris > TRI_CAP else ""
        print("THRALLS_DESIGN %s | %s | tris=%d | %s%s"
              % (name, label, tris, path, flag))


if __name__ == "__main__":
    main()
