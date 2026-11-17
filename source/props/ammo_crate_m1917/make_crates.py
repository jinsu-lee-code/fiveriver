"""M1917 ammunition packing box (.30 cal M2 ball, 8-round clips in bandoleers) and M1 bandoleers (2026-11-16).

Run (background Blender, never the user's open Blender window):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --python source/props/ammo_crate_m1917/make_crates.py

Sources (README.md in this folder): M1917 packing box 18-7/16 x 9-7/16 x 14-13/16 in, lid held by brass wing nuts on
six threaded posts in the walls (one each end, two each side), wing nuts, metal liner; M1 bandoleer = six cotton pockets, one
8-round en-bloc clip per pocket (48 rounds), 28-30 bandoleers per box.

Blender: X = length, Y = width, Z = up, origin at the centre of the bottom. Units metres.
Writes FBX files into five_river/Assets/_Project/Art/Models/Props/AmmoCrateM1917/. Deterministic (fixed seed).
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "five_river", "Assets", "_Project", "Art", "Models", "Props", "AmmoCrateM1917")
os.makedirs(OUT, exist_ok=True)

INCH = 0.0254
L, W, H = 18.4375 * INCH, 9.4375 * INCH, 14.8125 * INCH       # 0.468 x 0.240 x 0.376 m
TE, TS, TB = 0.022, 0.016, 0.016                                # end, side, bottom board thickness
GAP = 0.0025                                                    # gap between boards
POSTS_X = 0.135                                                 # side posts at +-POSTS_X, under the lid straps
TEX_M = 0.90                                                    # metres of wood across the plank texture width
PLANKS = 6                                                      # planks stacked in the texture


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m


def new_object(name, bm, mat_name, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(material(mat_name))
    if parent is not None:
        ob.parent = parent
    return ob


def cube(size, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect="EDGES", profile=0.5)
    bm.normal_update()
    return bm


def uv_box(bm, scale):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [i for i in range(3) if i != ax]
        for lp in f.loops:
            co = lp.vert.co
            lp[uv].uv = (co[a] / scale, co[b] / scale)


def uv_plank(bm, size, rng):
    """Map a board onto one plank strip of the wood texture: grain along the longest axis."""
    order = sorted(range(3), key=lambda i: size[i])
    thick, width, grain = order
    band = rng.randrange(PLANKS)
    lo, hi = band / PLANKS + 0.012, (band + 1) / PLANKS - 0.012
    uoff = rng.random() * 2.0
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for lp in f.loops:
            co = lp.vert.co
            u = uoff + co[grain] / TEX_M
            if ax == thick:
                v = lo + (co[width] / size[width] + 0.5) * (hi - lo)
            elif ax == width:      # the thin long edge: a sliver of the same plank
                v = lo + (co[thick] / size[thick] + 0.5) * (hi - lo) * 0.18
            else:                  # end grain: squeeze a piece of texture
                u = uoff + co[width] / TEX_M * 0.25
                v = lo + (co[thick] / size[thick] + 0.5) * (hi - lo) * 0.3
            lp[uv].uv = (u, v)


def board(name, size, center, rng, parent, bevel=0.0018, rot=None):
    bm = cube(size, bevel)
    uv_plank(bm, size, rng)
    if rot is not None:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=Vector((0, 0, 0)), matrix=rot)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_object(name, bm, "Wood", parent)


def metal_box(name, size, center, mat, parent, bevel=0.0008):
    bm = cube(size, bevel)
    uv_box(bm, 0.05)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return new_object(name, bm, mat, parent)


def cylinder(name, radius, depth, center, mat, parent, axis="Z", segments=12):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius, radius2=radius, depth=depth)
    if axis == "X":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"))
    elif axis == "Y":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"))
    uv_box(bm, 0.03)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    bm.normal_update()
    return new_object(name, bm, mat, parent)


def stencil_quad(name, w, h, center, normal_axis, sign, uv_rect, parent, flip_u=False):
    bm = bmesh.new()
    corners = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    vs = []
    for (a, b) in corners:
        if normal_axis == "Y":
            co = Vector((a * sign * -1, 0, b))     # facing -Y when sign=-1 reads left to right from outside
        elif normal_axis == "X":
            co = Vector((0, a * sign, b))
        else:
            co = Vector((a, b, 0))
        vs.append(bm.verts.new(co + Vector(center)))
    f = bm.faces.new(vs if not (normal_axis == "Y" and sign > 0) else vs)
    f.normal_update()
    want = Vector((0, sign, 0)) if normal_axis == "Y" else Vector((sign, 0, 0)) if normal_axis == "X" else Vector((0, 0, 1))
    if f.normal.dot(want) < 0:
        f.normal_flip()
    uv = bm.loops.layers.uv.verify()
    u0, v0, u1, v1 = uv_rect
    # assign by local 2D position
    for lp in f.loops:
        co = lp.vert.co - Vector(center)
        if normal_axis == "Y":
            s = (-co.x * sign / w + 0.5)
            t = co.z / h + 0.5
        elif normal_axis == "X":
            s = (co.y * sign / w + 0.5)
            t = co.z / h + 0.5
        else:
            s = co.x / w + 0.5
            t = co.y / h + 0.5
        if flip_u:
            s = 1 - s
        lp[uv].uv = (u0 + (u1 - u0) * s, v0 + (v1 - v0) * t)
    return new_object(name, bm, "Stencil", parent)


def empty(name, parent):
    ob = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(ob)
    ob.parent = parent
    return ob


# ---- the box --------------------------------------------------------------------------------------------------
# 2026-11-17 rebuilt against the record (README.md):
#   TM 9-1990 Small-Arms Ammunition (War Department, 23 May 1942, with C2 of 1943), par. 23 "boxes are painted or
#   stained dark brown (chocolate), and markings are stenciled in yellow"; par. 28 "the outer wooden cover of the box
#   is held in place by six wing nuts. These boxes have watertight, metal liners. The cover is closed by soldering but
#   can readily be torn or ripped off by use of a wire handle"; 18-7/16 x 9-7/16 x 14-13/16 in, 1.49 cu ft;
#   fig. 12.6 "Packing box M1917 for 1344 caliber .30 ... cartridges in 8-round clips in bandoleers" (the marking
#   layout copied here); fig. 13 (opening the box: wing nuts, felt liner, metal liner, bandoleers, bandoleer carton,
#   8-round clip, reference card).
#   Photographs of surviving boxes (QuestMasters Museum T1EDV; IMA-USA T1EHA ball M2): finger-jointed corners, a
#   hand-hold milled into each end, no rope or wire handle, no straps or battens outside.
FINGERS = 28                      # counted on the photographs: about 28 fingers up the 14-13/16 in corner
SLOT_W, SLOT_H = 0.115, 0.028     # hand-hold, about 4-1/2 x 1-1/8 in (scaled from the photographs, +-10 %)
SLOT_Z = 0.292                    # its centre above the bottom


def quad(name, pts, want, mat, parent, uvs=None):
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(p)) for p in pts]
    f = bm.faces.new(vs)
    f.normal_update()
    if f.normal.dot(Vector(want)) < 0:
        f.normal_flip()
    uv = bm.loops.layers.uv.verify()
    for lp in f.loops:
        i = vs.index(lp.vert)
        lp[uv].uv = uvs[i] if uvs else ((i in (1, 2)) * 1.0, (i in (2, 3)) * 1.0)
    return new_object(name, bm, mat, parent)


def finger_joints(root):
    """End grain of the interlocking fingers at the four corners, as flat patches just off the boards."""
    fh = H / FINGERS
    e = 0.0005
    for sx in (-1, 1):
        for sy in (-1, 1):
            for k in range(FINGERS):
                z0, z1 = k * fh + 0.0006, (k + 1) * fh - 0.0006
                if k % 2 == 0:      # the end board's finger shows on the long side
                    y = sy * (W / 2 + e)
                    xa, xb = sx * (L / 2 - TE), sx * (L / 2 - 0.0008)
                    quad(f"FingerS{sx}{sy}{k}", [(xa, y, z0), (xb, y, z0), (xb, y, z1), (xa, y, z1)], (0, sy, 0), "EndGrain", root,
                         [(0.10, 0.10), (0.20, 0.10), (0.20, 0.16), (0.10, 0.16)])
                else:               # the side board's finger shows on the end
                    x = sx * (L / 2 + e)
                    ya, yb = sy * (W / 2 - TS), sy * (W / 2 - 0.0008)
                    quad(f"FingerE{sx}{sy}{k}", [(x, ya, z0), (x, yb, z0), (x, yb, z1), (x, ya, z1)], (sx, 0, 0), "EndGrain", root,
                         [(0.30, 0.10), (0.38, 0.10), (0.38, 0.16), (0.30, 0.16)])


def build_body(root, rng, stencils=True, liner=True):
    inner_l = L - 2 * TE
    half = (H - GAP) / 2
    board("Bottom", (inner_l, W - 2 * TS, TB), (0, 0, TB / 2), rng, root)
    for sy in (-1, 1):
        for k in range(2):
            z = half / 2 + k * (half + GAP)
            board(f"Side{'N' if sy < 0 else 'S'}{k}", (inner_l + 0.004, TS, half), (0, sy * (W / 2 - TS / 2), z), rng, root)
    # ends: lower board whole, upper board with the milled hand-hold
    sd = TE * 0.62
    z_up0 = half + GAP
    for sx in (-1, 1):
        tag = "W" if sx < 0 else "E"
        board(f"End{tag}0", (TE, W, half), (sx * (L / 2 - TE / 2), 0, half / 2), rng, root)
        seed = rng.randrange(1 << 30)

        def piece(name, size, center, seed=seed):
            return board(name, size, center, random.Random(seed), root, bevel=0.0)
        piece(f"End{tag}1In", (TE - sd, W, half), (sx * (L / 2 - sd - (TE - sd) / 2), 0, z_up0 + half / 2))
        xo = sx * (L / 2 - sd / 2)
        lo_h = SLOT_Z - SLOT_H / 2 - z_up0
        hi_h = H - (SLOT_Z + SLOT_H / 2)
        piece(f"End{tag}1Lo", (sd, W, lo_h), (xo, 0, z_up0 + lo_h / 2))
        piece(f"End{tag}1Hi", (sd, W, hi_h), (xo, 0, H - hi_h / 2))
        cw = (W - SLOT_W) / 2
        for sy in (-1, 1):
            piece(f"End{tag}1Ch{sy}", (sd, cw, SLOT_H), (xo, sy * (SLOT_W / 2 + cw / 2), SLOT_Z))
    finger_joints(root)
    # six threaded posts set in the walls (one in each end, two in each long side); only the tips show at the rim
    for sx in (-1, 1):
        cylinder(f"PostEnd{sx}", 0.0036, 0.024, (sx * (L / 2 - TE / 2), 0.0, H + 0.012), "Iron", root)
    for sy in (-1, 1):
        for px in (-POSTS_X, POSTS_X):
            cylinder(f"PostSide{sy}{px:+.2f}", 0.0036, 0.024, (px, sy * (W / 2 - TS / 2), H + 0.012), "Iron", root)
    if liner:
        # the watertight metal liner: four walls just under the rim; its soldered cover torn along three sides (by
        # the wire handle, TM 9-1990 par. 28 and fig. 13 B) and bent up and back along the rear wall
        t, drop = 0.0008, 0.006
        il, iw, ih = inner_l - 0.002, W - 2 * TS - 0.002, H - TB - drop
        for sy in (-1, 1):
            metal_box(f"LinerSide{sy}", (il, t, ih), (0, sy * (iw / 2), TB + ih / 2), "Zinc", root, bevel=0.0)
        for sx in (-1, 1):
            metal_box(f"LinerEnd{sx}", (t, iw, ih), (sx * (il / 2), 0, TB + ih / 2), "Zinc", root, bevel=0.0)
        # 2026-11-17: the cover was three flat quads standing off the rear wall and read as a loose board. It is a
        # thin sheet ripped along the front and both ends and peeled back, so: one skin (the material is two-sided)
        # that leaves the rear wall, rises a little, then rolls over backwards and down behind its own root; the
        # sheet buckles along its length, the torn front edge is ragged, and the ends pull in as it rolls.
        top_z = H - drop
        y_root = iw / 2 - 0.001
        NU, NV = 28, 14
        sheet = iw * 0.58                      # what is left on the strip, rolled tight (the rest tore away)
        xa, xb = -il / 2 + 0.006, il / 2 - 0.006
        frng = random.Random(1917)
        ragged = [frng.uniform(-1.0, 1.0) for _ in range(NU + 1)]
        phase = [frng.uniform(0, math.tau) for _ in range(3)]
        bm = bmesh.new()
        grid = []
        for iu in range(NU + 1):
            fu = iu / NU
            # length of this strip of sheet: ragged at the torn edge, shorter toward the two ends
            rag = (ragged[iu] * 0.6 + ragged[max(iu - 1, 0)] * 0.2 + ragged[min(iu + 1, NU)] * 0.2)
            length = sheet * (0.90 + 0.10 * rag) * (0.80 + 0.20 * math.sin(math.pi * fu) ** 0.5)
            col = []
            y, z, ang = y_root, top_z, math.radians(100)   # leaves the wall leaning a little inward, as bent up
            for iv in range(NV + 1):
                fv = iv / NV
                if iv:
                    step = length / NV
                    # the roll tightens toward the free edge: about 270 degrees in all
                    ang -= math.radians(270) * (0.5 + 1.0 * fv) / (NV * 1.0)
                    y += math.cos(ang) * step
                    z += math.sin(ang) * step
                # buckling along the length grows away from the soldered root
                wob = (math.sin(fu * 9.0 + phase[0]) * 0.0030 + math.sin(fu * 23.0 + phase[1]) * 0.0014
                       + math.sin(fu * 4.0 + fv * 5.0 + phase[2]) * 0.0022) * min(1.0, fv * 2.5)
                nx, nz = -math.sin(ang), math.cos(ang)
                pinch = 1.0 - 0.06 * fv * (1.0 - math.sin(math.pi * fu))   # the corners pull in
                x = (xa + (xb - xa) * fu) * pinch
                col.append(bm.verts.new(Vector((x, y + nx * wob, max(z + nz * wob, top_z - 0.004)))))
            grid.append(col)
        uvl = bm.loops.layers.uv.verify()
        for iu in range(NU):
            for iv in range(NV):
                f = bm.faces.new((grid[iu][iv], grid[iu + 1][iv], grid[iu + 1][iv + 1], grid[iu][iv + 1]))
                f.smooth = True
                for lp, (du, dv) in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
                    lp[uvl].uv = ((iu + du) / NU * 4.0, (iv + dv) / NV * 1.2)
        bm.normal_update()
        new_object("LinerFlap", bm, "Zinc", root)
    if stencils:
        # yellow stencils after TM 9-1990 fig. 12.6: the long side (-Y) and both ends
        stencil_quad("StencilSide", 0.43, 0.345, (0, -W / 2 - 0.0012, H * 0.5), "Y", -1, (0.001, 0.002, 0.497, 0.998), root)
        for sx in (-1, 1):
            stencil_quad(f"StencilEnd{sx}", 0.20, 0.345, (sx * (L / 2 + 0.0012), 0, H * 0.5), "X", sx, (0.504, 0.002, 0.734, 0.998), root)


def build_lid(root, rng, z0=0.0, nuts=False):
    half_w = (W - GAP) / 2
    for k, sy in enumerate((-1, 1)):
        board(f"LidBoard{k}", (L, half_w, TB), (0, sy * (half_w / 2 + GAP / 2), z0 + TB / 2), rng, root)
    posts = [(sx * (L / 2 - TE / 2), 0.0) for sx in (-1, 1)] + [(px, sy * (W / 2 - TS / 2)) for sy in (-1, 1) for px in (-POSTS_X, POSTS_X)]
    for i, (x, y) in enumerate(posts):
        if nuts:
            cylinder(f"Washer{i}", 0.013, 0.002, (x, y, z0 + TB + 0.001), "Iron", root, segments=16)
            cylinder(f"NutPost{i}", 0.0036, 0.014, (x, y, z0 + TB + 0.007), "Iron", root)
            wing_nut(f"Nut{i}", (x, y, z0 + TB + 0.002), root, turn=(i * 37) % 180)
        else:
            cylinder(f"Hole{i}", 0.0048, 0.0006, (x, y, z0 + TB + 0.0002), "Iron", root, segments=12)   # the post holes


def wing_nut(name, center, parent, turn=0.0):
    cylinder(name + "Hub", 0.0065, 0.008, (center[0], center[1], center[2] + 0.004), "Iron", parent, segments=6)
    a = math.radians(turn)
    for s in (-1, 1):
        bm = cube((0.012, 0.0026, 0.011), 0.0006)
        uv_box(bm, 0.05)
        bmesh.ops.translate(bm, vec=Vector((s * 0.0105, 0, 0.0065)), verts=bm.verts)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=Vector(), matrix=Matrix.Rotation(a, 3, "Z"))
        bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
        new_object(name + f"Wing{s}", bm, "Iron", parent)


# ---- the bandoleer -----------------------------------------------------------------------------------------------
# TM 9-1990 par. 30: "The bandoleer is made of olive-drab cotton cloth and is divided into six pockets. A shoulder
# strap of webbing and a safety pin are attached ... In one pocket of the bandoleer a reference card is inserted
# showing the type, ammunition lot number, manufacturer, and powder lot ... The bandoleer M1 is designed to hold a
# carton containing one 8-round clip in each pocket. The packed bandoleer weighs about 3-1/4 pounds."
# So: no flaps, no snaps, no ink stamp (the card carries the lot); a chipboard carton round each clip.
# Pocket size is not given in the manual: taken from the cartridge (3.34 in) plus hem, and fig. 13 C.
PW, PH, PT = 0.067, 0.098, 0.027      # pocket pitch along the strip, depth, thickness when full
STRIP = 6 * PW + 0.012


def soft_box(size, rng, cuts=6, round_=0.45, wrinkle=0.0009, sag=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=cuts, use_grid_fill=True)
    sx, sy, sz = size
    ph = [rng.uniform(0, 6.28) for _ in range(4)]
    fr = [rng.uniform(55, 95), rng.uniform(120, 170), rng.uniform(40, 70)]
    for v in bm.verts:
        c = v.co * 2.0
        e = c.normalized() * 1.18 if c.length > 1e-6 else c
        e = Vector((max(-1, min(1, e.x)), max(-1, min(1, e.y)), max(-1, min(1, e.z))))
        c = c.lerp(e, round_)
        x, y, z = c.x * sx / 2, c.y * sy / 2, c.z * sz / 2
        edge = max(abs(c.x), abs(c.y)) ** 2
        w = math.sin(x * fr[0] + ph[0]) * 0.6 + math.sin((x + y) * fr[1] + ph[1]) * 0.25 + math.sin(y * fr[2] + ph[2]) * 0.4
        z += wrinkle * w * (0.35 + 0.65 * edge) * (1 if c.z >= 0 else 0.3)
        z -= sag * (c.x ** 2)
        v.co = Vector((x, y, z))
    bm.normal_update()
    return bm


def strip(length, width, thick, rng, wave=0.0012, drape=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    long_cuts = max(4, int(length / 0.012))
    bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs((e.verts[0].co - e.verts[1].co).x) > 0.5], cuts=long_cuts)
    bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs((e.verts[0].co - e.verts[1].co).y) > 0.5], cuts=3)
    p0, p1, f0 = rng.uniform(0, 6.28), rng.uniform(0, 6.28), rng.uniform(38, 60)
    for v in bm.verts:
        x, y, z = v.co.x * length, v.co.y * width, v.co.z * thick
        z += wave * (math.sin(x * f0 + p0) + 0.5 * math.sin(x * f0 * 2.3 + y * 40 + p1))
        if drape is not None:
            z += drape(x, y)
        v.co = Vector((x, y, z))
    bm.normal_update()
    return bm


def pocket(name, center, parent, rng, full, cloth="Cloth"):
    """One open-topped cotton pocket (mouth toward +Y). full: the carton with its clip inside, seen at the mouth."""
    bm = soft_box((PW - 0.007, PH - 0.004, PT), rng, round_=0.50 if full else 0.58, wrinkle=0.0022)
    if not full:
        for v in bm.verts:          # the clip is being drawn: slack and flatter toward the mouth
            if v.co.z > 0:
                v.co.z *= 0.70 - 0.20 * max(0.0, v.co.y / (PH / 2))
    uv_box(bm, 0.22)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    bm.normal_update()
    new_object(name, bm, cloth, parent)
    cx, cy, cz = center
    if full:
        # the chipboard carton's open end at the mouth, the steel back of the clip inside it
        metal_box(name + "Carton", (0.042, 0.004, 0.022), (cx, cy + PH / 2 - 0.0045, cz + 0.001), "Card", parent, bevel=0.0006)
        metal_box(name + "ClipBack", (0.036, 0.004, 0.017), (cx, cy + PH / 2 - 0.0035, cz + 0.001), "Iron", parent, bevel=0.0008)
    else:
        s = empty(name + "_ClipSlot", parent)
        s.location = (cx, cy + PH / 2 - 0.02, cz + 0.002)


def bandoleer(name, origin, rng, full):
    root = empty(name, None)
    root.location = origin
    ox = -STRIP / 2 + 0.006 + PW / 2
    back = strip(STRIP, PH, 0.0024, rng, wave=0.0006)
    uv_box(back, 0.22)
    bmesh.ops.translate(back, vec=Vector((0, 0, -PT / 2 + 0.002)), verts=back.verts)
    new_object(name + "Back", back, "ClothDark", root)
    tints = ("Cloth", "Cloth", "ClothB", "ClothC")      # one bolt of cloth: mostly alike, a faded or darker panel here and there
    for i in range(6):
        pocket(f"{name}Pocket{i}", (ox + i * PW, 0.0, 0.0), root, rng, full, cloth=tints[rng.randrange(4)])
    if full:
        # the webbing shoulder strap, folded back and forth over the pockets, and its safety pin
        def seams(x, y):
            u = ((x + STRIP / 2 - 0.006) / PW) % 1.0
            return -0.0030 * (math.cos(u * 2 * math.pi) * 0.5 + 0.5) ** 2
        for k, (y, z) in enumerate(((-0.022, PT / 2 + 0.0046), (0.004, PT / 2 + 0.0064))):
            s = strip(STRIP - 0.05, 0.019, 0.0014, rng, wave=0.0007, drape=seams)
            uv_box(s, 0.22)
            bmesh.ops.rotate(s, verts=s.verts, cent=Vector(), matrix=Matrix.Rotation(math.radians(rng.uniform(-3.0, 3.0)), 3, "Z"))
            bmesh.ops.translate(s, vec=Vector((-0.02, y, z)), verts=s.verts)
            new_object(f"{name}Strap{k}", s, "ClothB", root)
        cylinder(name + "Pin", 0.0007, 0.034, (-0.06, 0.004, PT / 2 + 0.0082), "Brass", root, axis="X", segments=6)
    else:
        # the reference card, half out of the last pocket (type, lot, manufacturer, powder lot)
        cx, cy, cz = ox + 5 * PW + 0.004, PH / 2 + 0.012, PT / 2 * 0.35
        w, h = 0.050, 0.075
        tilt = Matrix.Rotation(math.radians(9), 4, "Z")
        pts = [tilt @ Vector(p) + Vector((cx, cy, cz)) for p in ((-w / 2, -h / 2, 0), (w / 2, -h / 2, 0), (w / 2, h / 2, 0.004), (-w / 2, h / 2, 0.004))]
        quad(name + "Card", [tuple(p) for p in pts], (0, 0, 1), "Card", root)
        up = Vector((0, 0, 0.0006))
        quad(name + "CardPrint", [tuple(p + up) for p in pts], (0, 0, 1), "Stencil", root,
             [(0.743, 0.376), (0.998, 0.376), (0.998, 0.999), (0.743, 0.999)])
    return root


def export(objs_root, filename):
    bpy.ops.object.select_all(action="DESELECT")
    keep = set()

    def walk(o):
        keep.add(o)
        for c in o.children:
            walk(c)
    walk(objs_root)
    meshes = [o for o in keep if o.type == "MESH"]
    empties = [o for o in keep if o.type == "EMPTY"]
    if len(meshes) > 1:
        for o in meshes:
            mw = o.matrix_world.copy()
            o.parent = None
            o.matrix_world = mw
        bpy.ops.object.select_all(action="DESELECT")
        for o in meshes:
            o.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        joined = bpy.context.view_layer.objects.active
        joined.name = objs_root.name + "_Mesh"
        joined.parent = objs_root
        keep = {objs_root, joined} | set(empties)
    for o in bpy.context.scene.objects:
        o.select_set(o in keep)
    path = os.path.join(OUT, filename)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type="FACE")
    print("made", path)


def main():
    rng = random.Random(1917)
    reset()
    r = empty("SM_PR_M1917_CrateOpen", None)
    build_body(r, rng)
    export(r, "SM_PR_M1917_CrateOpen.fbx")

    reset()
    r = empty("SM_PR_M1917_Lid", None)
    build_lid(r, rng)
    export(r, "SM_PR_M1917_Lid.fbx")

    reset()
    r = empty("SM_PR_M1917_CrateClosed", None)
    build_body(r, rng, liner=False)
    lid = empty("Lid", r)
    build_lid(lid, rng, z0=H, nuts=True)
    export(r, "SM_PR_M1917_CrateClosed.fbx")

    reset()
    b = bandoleer("SM_PR_Bandoleer_Closed", (0, 0, 0), rng, True)
    export(b, "SM_PR_Bandoleer_Closed.fbx")
    reset()
    b = bandoleer("SM_PR_Bandoleer_Open", (0, 0, 0), rng, False)
    export(b, "SM_PR_Bandoleer_Open.fbx")

    # the packed bandoleers below the top layer, seen only through the gaps: one block
    reset()
    r = empty("SM_PR_Bandoleer_Fill", None)
    inner_l, inner_w = L - 2 * TE - 0.006, W - 2 * TS - 0.006
    for k in range(9):
        layer = cube((inner_l, inner_w, PT - 0.004), bevel=0.004)
        uv_box(layer, 0.22)
        bmesh.ops.translate(layer, vec=Vector((0, 0, TB + (PT - 0.002) * (k + 0.5))), verts=layer.verts)
        new_object(f"Layer{k}", layer, "ClothDark", r)
    export(r, "SM_PR_Bandoleer_Fill.fbx")


main()
