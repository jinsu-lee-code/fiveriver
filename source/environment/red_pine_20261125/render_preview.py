# 적송 미리보기 (검수용). blender -b --factory-startup -P render_preview.py
import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "five_river", "Assets", "_Project", "Art", "Maps", "Dabudong_Env", "RedPine20261125"))
REN = os.path.join(HERE, "renders"); os.makedirs(REN, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
    try:
        sc.render.engine = e; break
    except TypeError:
        pass
sc.render.resolution_x, sc.render.resolution_y = 1280, 900
for i, t in enumerate("ABC"):
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, f"SM_RedPine_{t}.fbx"))
    for o in list(sc.objects):
        if o.type == "MESH" and o.name.startswith(f"SM_RedPine_{t}"):
            if not o.name.endswith("LOD0"):
                bpy.data.objects.remove(o, do_unlink=True)
            else:
                o.location = (i * 7.0 - 7.0, 0, 0)


def tex(nt, name, non_color=False):
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(os.path.join(OUT, name))
    if non_color:
        n.image.colorspace_settings.name = "Non-Color"
    return n


for m in bpy.data.materials:
    m.use_nodes = True
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    key = m.name.split(".")[0]
    if key == "M_RedPine_Needles":
        t = tex(nt, "T_RedPine_Needles_BaseColor.png")
        nt.links.new(t.outputs["Color"], b.inputs["Base Color"]); nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
        try:
            m.blend_method = "CLIP"
        except Exception:
            pass
        m.use_backface_culling = False
    else:
        stem = "BarkLow" if key.endswith("Low") else "BarkHigh"
        t = tex(nt, f"T_RedPine_{stem}_BaseColor.png"); nm = tex(nt, f"T_RedPine_{stem}_Normal.png", True)
        nmap = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(nm.outputs[0], nmap.inputs[1])
        nt.links.new(t.outputs["Color"], b.inputs["Base Color"]); nt.links.new(nmap.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = 0.9
bpy.ops.mesh.primitive_plane_add(size=80)
g = bpy.context.object; gm = bpy.data.materials.new("g"); gm.use_nodes = True
next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.32, 0.24, 0.16, 1)
g.data.materials.append(gm)
sun = bpy.data.lights.new("sun", "SUN"); sun.energy = 4; so = bpy.data.objects.new("sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(35))
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.62, 0.72, 1)
cam = bpy.data.cameras.new("cam"); co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co


def shot(name, loc, look, lens):
    co.location = loc; cam.lens = lens
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(REN, name + ".png"); bpy.ops.render.render(write_still=True)


shot("trees", (0, -22, 4.5), (0, 0, 4.5), 30)
shot("trunk", (-6.2, -2.6, 1.6), (-7, 0, 1.4), 35)
shot("crown", (0.5, -5, 3.5), (0, 0, 7.5), 28)
