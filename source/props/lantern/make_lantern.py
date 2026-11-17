"""Kerosene hurricane lantern (cold-blast tubular type, the common field lantern of 1950) for the trench (2026-11-17).

Run (background Blender, never the user's open Blender window):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --python source/props/lantern/make_lantern.py

The old Supply_bay_lantern was a 12 cm dark box frame around a cylinder: from the player's eye, looking down, it read as
a black plate. This one has the parts that make the silhouette: fount (tank) with filler cap, burner cone with the wick
knob, glass globe, crown and chimney cap, the two side air tubes, globe guard wires and the bail handle.

Blender: Z up, origin at the centre of the bottom. Height 0.335 m. Materials: Steel, Glass, Flame.
Writes five_river/Assets/_Project/Art/Models/Props/Lantern/SM_PR_HurricaneLantern.fbx. Deterministic.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "five_river", "Assets", "_Project", "Art", "Models", "Props", "Lantern")
os.makedirs(OUT, exist_ok=True)
SEG = 20


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def add(name, bm, m):
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(mat(m))
    return ob


def lathe(name, profile, m, seg=SEG, cap_top=True, cap_bottom=True):
    """profile: (radius, z) from bottom to top."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    rings = []
    for r, z in profile:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    length = [0.0]
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        length.append(length[-1] + math.hypot(r1 - r0, z1 - z0))
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            f = bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
            us = (i / seg, (i + 1) / seg, (i + 1) / seg, i / seg)
            vs = (length[k], length[k], length[k + 1], length[k + 1])
            for lp, u, v in zip(f.loops, us, vs):
                lp[uv].uv = (u * 2.0, v * 6.0)
    if cap_bottom and profile[0][0] > 1e-5:
        bm.faces.new(list(reversed(rings[0])))
    if cap_top and profile[-1][0] > 1e-5:
        bm.faces.new(rings[-1])
    return add(name, bm, m)


def tube(name, points, radius, m, seg=8):
    """A round tube along a polyline."""
    bm = bmesh.new()
    rings = []
    n = len(points)
    for k, p in enumerate(points):
        p = Vector(p)
        d = (Vector(points[min(k + 1, n - 1)]) - Vector(points[max(k - 1, 0)])).normalized()
        a = d.cross(Vector((0, 0, 1)))
        if a.length < 1e-4:
            a = d.cross(Vector((1, 0, 0)))
        a.normalize()
        b = d.cross(a).normalized()
        rings.append([bm.verts.new(p + radius * (math.cos(2 * math.pi * i / seg) * a + math.sin(2 * math.pi * i / seg) * b)) for i in range(seg)])
    for k in range(n - 1):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return add(name, bm, m)


def arc(c, r, a0, a1, n, plane="XZ"):
    pts = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        if plane == "XZ":
            pts.append((c[0] + r * math.cos(a), c[1], c[2] + r * math.sin(a)))
        else:
            pts.append((c[0] + r * math.cos(a), c[1] + r * math.sin(a), c[2]))
    return pts


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = []
    # fount (tank): a low drum with a rolled foot and a domed shoulder
    parts.append(lathe("Fount", [(0.066, 0.0), (0.074, 0.004), (0.076, 0.012), (0.076, 0.050), (0.070, 0.062), (0.052, 0.070), (0.030, 0.074)], "Steel"))
    # filler cap on the shoulder
    cap = lathe("Filler", [(0.011, 0.0), (0.013, 0.002), (0.013, 0.010), (0.010, 0.013), (0.0, 0.013)], "Steel", seg=12, cap_top=False)
    cap.matrix_world = Matrix.Translation((0.048, 0.030, 0.066)) @ Matrix.Rotation(math.radians(-14), 4, "Y")
    parts.append(cap)
    # burner cone and the perforated globe plate
    parts.append(lathe("Burner", [(0.030, 0.074), (0.032, 0.086), (0.040, 0.092), (0.047, 0.094), (0.047, 0.099), (0.036, 0.101)], "Steel"))
    # wick knob on its stem
    parts.append(tube("WickStem", [(0.030, -0.006, 0.084), (0.062, -0.012, 0.084)], 0.0022, "Steel", seg=6))
    knob = lathe("WickKnob", [(0.0, 0.0), (0.009, 0.001), (0.010, 0.004), (0.009, 0.007), (0.0, 0.008)], "Steel", seg=12, cap_top=False, cap_bottom=False)
    knob.matrix_world = Matrix.Translation((0.062, -0.012, 0.084)) @ Matrix.Rotation(math.radians(90), 4, "Y")
    parts.append(knob)
    # glass globe: pear shape
    globe = [(0.036, 0.100), (0.044, 0.112), (0.052, 0.132), (0.055, 0.152), (0.052, 0.172), (0.044, 0.192), (0.035, 0.208), (0.030, 0.218)]
    parts.append(lathe("Globe", globe, "Glass", seg=24, cap_top=False, cap_bottom=False))
    # flame and wick inside
    parts.append(lathe("Flame", [(0.0, 0.104), (0.006, 0.110), (0.008, 0.122), (0.005, 0.136), (0.0, 0.150)], "Flame", seg=10, cap_top=False, cap_bottom=False))
    # crown: globe holder, chimney and its cap
    parts.append(lathe("Crown", [(0.033, 0.216), (0.036, 0.222), (0.036, 0.230), (0.028, 0.238), (0.022, 0.262), (0.022, 0.270)], "Steel"))
    parts.append(lathe("ChimneyCap", [(0.030, 0.270), (0.034, 0.273), (0.030, 0.281), (0.018, 0.288), (0.0, 0.290)], "Steel", cap_top=False))
    # the two side air tubes: up from the fount's shoulder, round over and into the crown
    for s in (-1, 1):
        x = 0.070 * s
        pts = [(x * 0.93, 0.0, 0.058), (x, 0.0, 0.080), (x, 0.0, 0.215)]
        top = arc((x - 0.030 * s, 0.0, 0.215), 0.030, 0 if s > 0 else 180, 90, 6)
        pts += top[1:]
        pts.append((0.024 * s, 0.0, 0.250))
        parts.append(tube(f"AirTube{s:+d}", pts, 0.0085, "Steel", seg=10))
        # bail ears on the tubes
        parts.append(tube(f"Ear{s:+d}", [(x, 0.0, 0.206), (x + 0.012 * s, 0.0, 0.210)], 0.0025, "Steel", seg=6))
    # bail handle: a wire hoop from ear to ear, fallen a little to one side as it hangs
    hoop = arc((0.0, 0.0, 0.210), 0.082, 0, 180, 14)
    tilt = Matrix.Rotation(math.radians(28), 4, "X")
    hoop = [tuple(Matrix.Translation((0, 0, 0.210)) @ tilt @ Matrix.Translation((0, 0, -0.210)) @ Vector(p)) for p in hoop]
    parts.append(tube("Bail", hoop, 0.0022, "Steel", seg=6))
    # globe guard: two crossed wires round the globe
    for k, ang in enumerate((45, 135)):
        g = [(0.047, 0.0, 0.100), (0.056, 0.0, 0.126), (0.059, 0.0, 0.152), (0.056, 0.0, 0.178), (0.046, 0.0, 0.200), (0.036, 0.0, 0.218)]
        rot = Matrix.Rotation(math.radians(ang), 4, "Z")
        for sgn in (1, -1):
            pts = [tuple(rot @ Vector((p[0] * sgn, 0.0, p[2]))) for p in g]
            parts.append(tube(f"Guard{k}{sgn:+d}", pts, 0.0016, "Steel", seg=5))

    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = "SM_PR_HurricaneLantern"
    path = os.path.join(OUT, "SM_PR_HurricaneLantern.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type="FACE")
    print("made", path, "tris", sum(len(p.vertices) - 2 for p in ob.data.polygons))


main()
