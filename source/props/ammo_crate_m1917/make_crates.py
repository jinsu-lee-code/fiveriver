"""M1917 ammunition packing box (.30 cal M2 ball, 8-round clips in bandoleers) and M1 bandoleers (2026-11-16).

Run (background Blender, never the user's open Blender window):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --python source/props/ammo_crate_m1917/make_crates.py

Sources (README.md in this folder): M1917 packing box 18-7/16 x 9-7/16 x 14-13/16 in, lid held by brass wing nuts on
six threaded posts (one each end, two each side) under two flat iron straps; M1 bandoleer = six cotton pockets, one
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


def wire_handle(name, center, outward, parent):
    """Bent wire handle on an end board: a U of round wire held by two small clips."""
    bm = bmesh.new()
    pts = []
    span, reach, r = 0.11, 0.030, 0.0032
    for i in range(15):
        a = math.pi * i / 14.0
        # horizontal U: spans the board width (Y), bows outward from the end (X), hanging slightly down
        pts.append(Vector((outward * reach * math.sin(a), -span / 2 * math.cos(a), -0.012 * math.sin(a))))
    ring = 8
    prev = None
    for i, p in enumerate(pts):
        tdir = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = tdir.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(tdir).normalized()
        cur = [bm.verts.new(p + (side * math.cos(2 * math.pi * k / ring) + up * math.sin(2 * math.pi * k / ring)) * r) for k in range(ring)]
        if prev is not None:
            for k in range(ring):
                bm.faces.new((prev[k], prev[(k + 1) % ring], cur[(k + 1) % ring], cur[k]))
        prev = cur
    uv_box(bm, 0.03)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    bm.normal_update()
    return new_object(name, bm, "Iron", parent)


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
def build_body(root, rng, stencils=True):
    inner_l = L - 2 * TE
    half = (H - GAP) / 2
    # bottom
    board("Bottom", (inner_l, W - 2 * TS, TB), (0, 0, TB / 2), rng, root)
    # sides: two boards each, full length between the ends
    for sy in (-1, 1):
        for k in range(2):
            z = half / 2 + k * (half + GAP)
            board(f"Side{'N' if sy < 0 else 'S'}{k}", (inner_l + 0.004, TS, half), (0, sy * (W / 2 - TS / 2), z), rng, root)
    # ends: two boards each, full width, with a vertical cleat and a top/bottom batten outside
    for sx in (-1, 1):
        for k in range(2):
            z = half / 2 + k * (half + GAP)
            board(f"End{'W' if sx < 0 else 'E'}{k}", (TE, W, half), (sx * (L / 2 - TE / 2), 0, z), rng, root)
        for k, z in enumerate((0.03, H - 0.03)):
            board(f"Batten{'W' if sx < 0 else 'E'}{k}", (0.014, W - 0.02, 0.042), (sx * (L / 2 + 0.007), 0, z), rng, root)
        # threaded post in the middle of the end, wire handle beside it
        cylinder(f"PostEnd{'W' if sx < 0 else 'E'}", 0.0042, H + 0.03, (sx * (L / 2 + 0.006), 0.0, (H + 0.03) / 2), "Iron", root)
        for k, zz in enumerate((0.07, H - 0.07)):
            metal_box(f"PostStrap{'W' if sx < 0 else 'E'}{k}", (0.004, 0.018, 0.012), (sx * (L / 2 + 0.016), 0, zz), "Iron", root)
        wire_handle(f"Handle{'W' if sx < 0 else 'E'}", (sx * (L / 2 + 0.016), 0.0, H * 0.70), sx, root)
        for sy in (-1, 1):
            metal_box(f"HandleClip{'W' if sx < 0 else 'E'}{sy}", (0.006, 0.014, 0.016), (sx * (L / 2 + 0.017), sy * 0.055, H * 0.70), "Iron", root)
    # side posts (two each side)
    for sy in (-1, 1):
        for px in (-POSTS_X, POSTS_X):
            cylinder(f"PostSide{sy}{px:+.2f}", 0.0042, H + 0.03, (px, sy * (W / 2 + 0.005), (H + 0.03) / 2), "Iron", root)
            for k, zz in enumerate((0.06, H - 0.06)):
                metal_box(f"PostStrapS{sy}{px:+.2f}{k}", (0.018, 0.004, 0.012), (px, sy * (W / 2 + 0.003), zz), "Iron", root)
    if stencils:
        # long side (-Y face) and one end (+X), on the outside, 1.5 mm off the wood
        stencil_quad("StencilSide", 0.40, 0.33, (0, -W / 2 - 0.0015, H * 0.5), "Y", -1, (0.02, 0.05, 0.49, 0.95), root)
        stencil_quad("StencilEnd", 0.19, 0.12, (L / 2 + 0.0145, 0, H * 0.42), "X", 1, (0.57, 0.04, 0.93, 0.48), root)


def build_lid(root, rng, z0=0.0):
    half_w = (W - GAP) / 2
    for k, sy in enumerate((-1, 1)):
        board(f"LidBoard{k}", (L, half_w, TB), (0, sy * (half_w / 2 + GAP / 2), z0 + TB / 2), rng, root)
    # two flat iron straps across, with an eye at each end for the side posts
    for px in (-POSTS_X, POSTS_X):
        metal_box(f"LidStrap{px:+.2f}", (0.032, W + 0.022, 0.003), (px, 0, z0 + TB + 0.0015), "Iron", root)
    # end plates for the end posts
    for sx in (-1, 1):
        metal_box(f"LidEndPlate{sx}", (0.026, 0.05, 0.003), (sx * (L / 2 - 0.006), 0, z0 + TB + 0.0015), "Iron", root)
    stencil_quad("StencilLid", 0.36, 0.15, (0, 0, z0 + TB + 0.0006), "Z", 1, (0.52, 0.53, 0.99, 0.97), root)


def wing_nut(name, center, parent):
    cylinder(name + "Hub", 0.0075, 0.009, (center[0], center[1], center[2] + 0.0045), "Brass", parent, segments=6)
    for s in (-1, 1):
        metal_box(name + f"Wing{s}", (0.013, 0.0028, 0.011), (center[0] + s * 0.011, center[1], center[2] + 0.007), "Brass", parent, bevel=0.0006)


# ---- the bandoleer -----------------------------------------------------------------------------------------------
PW, PH, PT = 0.067, 0.100, 0.028      # pocket width (along the strip), height (clip length + flap), thickness
STRIP = 6 * PW + 0.02


def pocket(name, center, parent, rng, open_flap):
    """One cotton pocket: a soft pillow, the flap over its mouth (+Y) or turned back, and a snap."""
    bm = cube((PW - 0.004, PH - 0.02, PT), bevel=0.0075)
    # soften: push the top face centre up a little (stuffed with a clip)
    for v in bm.verts:
        if v.co.z > 0:
            v.co.z += 0.0025 * (1 - (2 * v.co.x / PW) ** 2) * (1 - (2 * v.co.y / PH) ** 2)
    uv_box(bm, 0.22)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    bm.normal_update()
    new_object(name, bm, "Cloth", parent)
    cx, cy, cz = center
    if not open_flap:
        f = cube((PW - 0.006, 0.036, 0.003), bevel=0.0012)
        uv_box(f, 0.22)
        bmesh.ops.translate(f, vec=Vector((cx, cy + PH / 2 - 0.028, cz + PT / 2 + 0.003)), verts=f.verts)
        new_object(name + "Flap", f, "Cloth", parent)
        cylinder(name + "Snap", 0.0055, 0.0025, (cx, cy + PH / 2 - 0.036, cz + PT / 2 + 0.0055), "Iron", parent, segments=12)
    else:
        # the flap is the backing strip carried on over the mouth; unsnapped, it lies open beyond the mouth at the
        # backing's level (inner face up, with the snap's socket), and the clip comes out over it
        f = cube((PW - 0.006, 0.036, 0.003), bevel=0.0012)
        uv_box(f, 0.22)
        bmesh.ops.rotate(f, verts=f.verts, cent=Vector(), matrix=Matrix.Rotation(math.radians(2 + rng.uniform(-2, 2)), 3, "X"))
        bmesh.ops.translate(f, vec=Vector((cx, cy + PH / 2 + 0.008, cz - PT / 2 + 0.0035)), verts=f.verts)
        new_object(name + "Flap", f, "Cloth", parent)
        cylinder(name + "Snap", 0.0055, 0.0022, (cx, cy + PH / 2 + 0.016, cz - PT / 2 + 0.0061), "Iron", parent, segments=12)
        # the stud it snapped onto, on the pocket's face
        cylinder(name + "Stud", 0.0042, 0.002, (cx, cy + PH / 2 - 0.036, cz + PT / 2 + 0.0035), "Iron", parent, segments=12)
        # the clip slot: mouth of the pocket, +Y out of it, clip lying flat (its thickness up)
        s = empty(name + "_ClipSlot", parent)
        s.location = (cx, cy + PH / 2 - 0.02, cz + 0.002)


def bandoleer(name, origin, rng, open_flaps):
    root = empty(name, None)
    root.location = origin
    ox = -STRIP / 2 + 0.01 + PW / 2
    # backing strip under the pockets
    back = cube((STRIP, PH - 0.016, 0.004), bevel=0.0012)
    uv_box(back, 0.22)
    bmesh.ops.translate(back, vec=Vector((0, 0, -PT / 2 + 0.002)), verts=back.verts)
    new_object(name + "Back", back, "Cloth", root)
    for i in range(6):
        pocket(f"{name}Pocket{i}", (ox + i * PW, 0.0, 0.0), root, rng, open_flaps)
    if not open_flaps:
        # the folded shoulder strap lying across the pockets
        for k, (y, z) in enumerate(((-0.018, PT / 2 + 0.006), (0.012, PT / 2 + 0.0085))):
            s = cube((STRIP - 0.03, 0.021, 0.0022), bevel=0.0007)
            uv_box(s, 0.22)
            bmesh.ops.rotate(s, verts=s.verts, cent=Vector(), matrix=Matrix.Rotation(math.radians(rng.uniform(-2.5, 2.5)), 3, "Z"))
            bmesh.ops.translate(s, vec=Vector((0, y, z)), verts=s.verts)
            new_object(f"{name}Strap{k}", s, "Cloth", root)
    return root


def export(objs_root, filename):
    bpy.ops.object.select_all(action="DESELECT")
    keep = set()

    def walk(o):
        keep.add(o)
        for c in o.children:
            walk(c)
    walk(objs_root)
    # one mesh per model (sub-meshes per material) instead of dozens of board objects: fewer draw calls in Unity
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
    # open box (body only)
    r = empty("SM_PR_M1917_CrateOpen", None)
    build_body(r, rng)
    export(r, "SM_PR_M1917_CrateOpen.fbx")

    reset()
    r = empty("SM_PR_M1917_Lid", None)
    build_lid(r, rng)
    # no loose wing nuts on it: the lid stands leaning on its edge in the trench, anything lying on it would fall off
    export(r, "SM_PR_M1917_Lid.fbx")

    reset()
    r = empty("SM_PR_M1917_CrateClosed", None)
    build_body(r, rng)
    lid = empty("Lid", r)
    build_lid(lid, rng, z0=H)
    for sx in (-1, 1):
        wing_nut(f"NutEnd{sx}", (sx * (L / 2 - 0.006), 0.0, H + TB + 0.003), r)
    for sy in (-1, 1):
        for px in (-POSTS_X, POSTS_X):
            wing_nut(f"NutSide{sy}{px:+.2f}", (px, sy * (W / 2 + 0.005), H + TB + 0.003), r)
    export(r, "SM_PR_M1917_CrateClosed.fbx")

    reset()
    b = bandoleer("SM_PR_Bandoleer_Closed", (0, 0, 0), rng, False)
    export(b, "SM_PR_Bandoleer_Closed.fbx")
    reset()
    b = bandoleer("SM_PR_Bandoleer_Open", (0, 0, 0), rng, True)
    export(b, "SM_PR_Bandoleer_Open.fbx")

    # the packed bandoleers below the top layer, seen only through the gaps: one block
    reset()
    r = empty("SM_PR_Bandoleer_Fill", None)
    inner_l, inner_w = L - 2 * TE - 0.004, W - 2 * TS - 0.004
    for k in range(9):
        layer = cube((inner_l, inner_w, PT - 0.004), bevel=0.004)
        uv_box(layer, 0.22)
        bmesh.ops.translate(layer, vec=Vector((0, 0, TB + (PT - 0.002) * (k + 0.5))), verts=layer.verts)
        new_object(f"Layer{k}", layer, "ClothDark", r)
    export(r, "SM_PR_Bandoleer_Fill.fbx")


main()
