"""A candle stub on an upturned tin (2026-11-17), the supply bay's light.

Run: "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --python source/props/candle/make_candle.py

Record: no 1950 record of a lamp in a ROK front-line trench was found. A veteran's photograph from a Korean front-line
dugout (Samichon Valley, 1952, koreanwaronline.com/history/Ron/Frame/small-15.htm) shows a candle as the light;
the hurricane lantern used before had no source and is replaced by this. The tin it stands on is our own choice.
Blender: Z up, origin on the floor. Writes five_river/Assets/_Project/Art/Models/Props/Candle/SM_PR_CandleOnTin.fbx.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "five_river", "Assets", "_Project", "Art", "Models", "Props", "Candle")
os.makedirs(OUT, exist_ok=True)
TIN_R, TIN_H = 0.037, 0.058
CANDLE_R, CANDLE_H = 0.0105, 0.062


def lathe(name, profile, mat, segments=20):
    """profile: (radius, z) pairs from the bottom up."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / segments), r * math.sin(2 * math.pi * k / segments), z)) for k in range(segments)])
    for a, b in zip(rings, rings[1:]):
        for k in range(segments):
            bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for lp in f.loops:
            lp[uv].uv = (math.atan2(lp.vert.co.y, lp.vert.co.x) / (2 * math.pi) + 0.5, lp.vert.co.z * 8)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    m = bpy.data.materials.get(mat) or bpy.data.materials.new(mat)
    me.materials.append(m)
    return ob


bpy.ops.wm.read_factory_settings(use_empty=True)
parts = []
# an emptied ration tin stood upside down: rolled rims at both ends, two beads round the body
t = TIN_R
parts.append(lathe("Tin", [(t + 0.0012, 0), (t + 0.0012, 0.003), (t, 0.004), (t, 0.018), (t + 0.0008, 0.020), (t, 0.022), (t, 0.036), (t + 0.0008, 0.038),
                           (t, 0.040), (t, TIN_H - 0.004), (t + 0.0012, TIN_H - 0.003), (t + 0.0012, TIN_H), (t - 0.002, TIN_H), (t - 0.003, TIN_H - 0.0015), (0.001, TIN_H - 0.0015)], "Steel", 24))
z0 = TIN_H - 0.0015
# wax run down and set round the foot, then the stub, burnt down into a cup at the wick
parts.append(lathe("Puddle", [(0.024, z0), (0.022, z0 + 0.0016), (0.014, z0 + 0.003), (0.0115, z0 + 0.006)], "Wax", 16))
c = CANDLE_R
parts.append(lathe("Candle", [(c, z0), (c, z0 + CANDLE_H - 0.004), (c - 0.0012, z0 + CANDLE_H), (c - 0.0045, z0 + CANDLE_H - 0.003), (0.0012, z0 + CANDLE_H - 0.005)], "Wax", 16))
parts.append(lathe("Drip", [(0.0, 0), (0.0, 0)], "Wax", 3))
zt = z0 + CANDLE_H - 0.005
parts.append(lathe("Wick", [(0.0009, zt), (0.0008, zt + 0.007), (0.0003, zt + 0.009)], "Wick", 6))
parts.append(lathe("Flame", [(0.0008, zt + 0.004), (0.0042, zt + 0.011), (0.0036, zt + 0.019), (0.0015, zt + 0.029), (0.0002, zt + 0.036)], "Flame", 10))
parts = [p for p in parts if p.name != "Drip"]
for o in bpy.context.scene.objects:
    o.select_set(o in parts)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
ob.name = "SM_PR_CandleOnTin"
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "SM_PR_CandleOnTin.fbx"), use_selection=True, object_types={"MESH"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         add_leaf_bones=False, mesh_smooth_type="FACE")
print("flame height", zt + 0.018)
