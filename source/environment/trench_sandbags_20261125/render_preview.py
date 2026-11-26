# 모래주머니 줄 미리보기 렌더 (검수용). blender -b --factory-startup -P render_preview.py
import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "five_river", "Assets", "_Project", "Art", "MapPolish20261105", "TrenchDressing", "Sandbags20261125"))
SCAN = os.path.abspath(os.path.join(HERE, "..", "..", "..", "five_river", "Assets", "_Project", "Art", "Maps", "ClaudePolish20261030", "Scan"))
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

bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, "SM_TD_SandbagRow3m_A.fbx"))
for o in list(sc.objects):
    if o.type == "MESH" and o.name.endswith("LOD1"):
        bpy.data.objects.remove(o, do_unlink=True)

def mat(m, tint):
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(OUT, "T_TD_SandbagBurlap_BaseColor.png"))
    mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
    mix.inputs[2].default_value = (*tint, 1)
    nt.links.new(tex.outputs[0], mix.inputs[1]); nt.links.new(mix.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    nm = nt.nodes.new("ShaderNodeTexImage"); nm.image = bpy.data.images.load(os.path.join(SCAN, "hessian_230_nor_gl_2k.jpg")); nm.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.inputs[0].default_value = 0.8
    nt.links.new(nm.outputs[0], nmap.inputs[1]); nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])

for m in bpy.data.materials:
    m.use_backface_culling = True
    mat(m, (1, 1, 1) if m.name.endswith("_A") else (0.80, 0.74, 0.66))

# 흙 바닥 · 뒤 흉장 대신 판
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0))
g = bpy.context.object; gm = bpy.data.materials.new("ground"); gm.use_nodes = True
next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.25, 0.18, 0.12, 1)
g.data.materials.append(gm)

sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 3.5; so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(55), 0, math.radians(-35))
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.40, 0.48, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.8

cam = bpy.data.cameras.new("cam"); co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co

def shot(name, loc, look, lens):
    co.location = loc; cam.lens = lens
    d = Vector(look) - Vector(loc)
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(REN, name + ".png")
    bpy.ops.render.render(write_still=True)

# Unity +z 앞면 = Blender -y
shot("front", (0.4, -1.6, 0.75), (0, 0, 0.25), 35)
shot("close", (0.2, -0.75, 0.42), (0.1, 0, 0.25), 40)
shot("oblique", (2.4, -1.1, 0.9), (-0.5, 0, 0.25), 32)
shot("top", (0.3, -0.9, 1.3), (0.2, 0.1, 0.35), 35)
