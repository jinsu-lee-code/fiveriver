# 진지 소품 미리보기 (검수용). blender -b --factory-startup -P render_preview.py
import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
FBX = os.path.abspath(os.path.join(HERE, "..", "..", "..", "five_river", "Assets", "_Project", "Art", "MapPolish20261105", "PositionDressing20261125", "SM_PD_PositionProps.fbx"))
REN = os.path.join(HERE, "renders")
os.makedirs(REN, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
    try:
        sc.render.engine = e
        break
    except TypeError:
        pass
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
bpy.ops.import_scene.fbx(filepath=FBX)
cols = {"M_PD_PineBark": (0.18, 0.10, 0.06), "M_PD_PineNeedles": (0.07, 0.12, 0.04), "M_PD_PineNeedlesDry": (0.22, 0.20, 0.08),
        "M_PD_Spoil": (0.30, 0.22, 0.15), "M_PD_Poncho": (0.16, 0.17, 0.09), "M_PD_Twig": (0.2, 0.12, 0.07)}
for m in bpy.data.materials:
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*cols.get(m.name.split(".")[0], (0.5, 0.5, 0.5)), 1)
    b.inputs["Roughness"].default_value = 0.8
# 늘어놓기
x = 0.0
for o in sorted([o for o in sc.objects if o.type == "MESH"], key=lambda o: o.name):
    o.location = (x, 0, 0)
    x += max(o.dimensions.x, 1.0) + 0.4
bpy.ops.mesh.primitive_plane_add(size=60, location=(x / 2, 0, -0.01))
g = bpy.context.object; gm = bpy.data.materials.new("ground"); gm.use_nodes = True
next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.35, 0.27, 0.18, 1)
g.data.materials.append(gm)
sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 4; so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(30))
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.6, 1)
cam = bpy.data.cameras.new("cam"); co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co


def shot(name, target_name, dist, h, lens):
    o = sc.objects[target_name]
    c = o.matrix_world.translation + Vector((-o.dimensions.x * 0.5 if "Bough" not in o.name else 0.6, 0, 0))
    co.location = c + Vector((0.2, -dist, h)); cam.lens = lens
    co.rotation_euler = (c - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(REN, name + ".png")
    bpy.ops.render.render(write_still=True)


shot("bough", "PD_Bough_2_LOD0", 1.6, 0.9, 35)
shot("spoil", "PD_Spoil_A", 2.4, 1.3, 30)
shot("poncho", "PD_Poncho_A", 1.8, 1.4, 35)
