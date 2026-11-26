# 얼굴 탐색 렌더: 정면/옆, 텍스처 + 와이어
import bpy, sys, os, json
import numpy as np
from mathutils import Vector
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import fs_common as fc

args = sys.argv[sys.argv.index("--") + 1:]
fbx, outdir, tag = args[0], args[1], args[2]
meshes, arm = fc.load_fbx(fbx)
o = fc.body_mesh(meshes)
co, wco, mw = fc.arrays(o)
hw = fc.head_weights(o)
hm = fc.head_mat_verts(o)
sel = hm & (hw > 0.5)
P = wco[sel]
info = {"mesh": o.name, "n_head_mat": int(hm.sum()), "n_sel": int(sel.sum()),
        "min": P.min(0).tolist(), "max": P.max(0).tolist()}
# 얼굴 앞쪽: 머리 재질 정점의 y 범위 중 어느 쪽으로 코가 튀어나왔는지 (xy 중앙 근처)
c = (P.min(0) + P.max(0)) / 2
info["center"] = c.tolist()
near = P[np.abs(P[:, 0] - c[0]) < 0.01]
info["mid_ymin"] = float(near[:, 1].min()); info["mid_ymax"] = float(near[:, 1].max())
# 머리 재질 섬(재질 영역) 정점 분포 높이 히스토그램
print("@@JSON@@" + json.dumps(info))

sc = fc.setup_render((900, 900))
front = -1.0  # 가정: -Y 앞
tgt = Vector((c[0], c[1], c[2] - 0.01))
for nm, d in [("front", Vector((0, front, 0))), ("side", Vector((1, 0, 0))), ("q45", Vector((0.7, front * 0.7, 0)))]:
    for l in [o for o in bpy.data.objects if o.type in ("LIGHT", "CAMERA")]:
        bpy.data.objects.remove(l)
    cam = fc.make_cam(tgt + d.normalized() * 0.75, tgt, lens=85)
    fc.make_light("K", tgt + Vector((0.5, front * 0.8, 0.4)), tgt, 60)
    fc.make_light("F", tgt + Vector((-0.6, front * 0.6, 0.0)), tgt, 25)
    fc.render(os.path.join(outdir, f"explore_{tag}_{nm}.png"))
# 와이어 정면 (workbench)
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_object_outline = False
o.show_wire = True
o.show_all_edges = True
sc.render.use_freestyle = False
for l in [o2 for o2 in bpy.data.objects if o2.type == "CAMERA"]:
    bpy.data.objects.remove(l)
fc.make_cam(tgt + Vector((0, front, 0)) * 0.75, tgt, lens=85)
# workbench는 와이어를 렌더하지 않으므로 Wireframe 모디파이어 복제본으로 표시
dup = o.copy(); dup.data = o.data.copy(); bpy.context.scene.collection.objects.link(dup)
for m in list(dup.modifiers):
    dup.modifiers.remove(m)
wm = dup.modifiers.new("W", "WIREFRAME"); wm.thickness = 0.0006; wm.use_replace = True
dup.data.materials.clear()
mat = bpy.data.materials.new("blk"); mat.diffuse_color = (0, 0, 0, 1); dup.data.materials.append(mat)
sc.display.shading.color_type = "MATERIAL"
o.hide_render = False
# 원본은 텍스처 대신 흰색으로
sc.display.shading.color_type = "OBJECT"
o.color = (0.85, 0.75, 0.65, 1); dup.color = (0, 0, 0, 1)
fc.render(os.path.join(outdir, f"explore_{tag}_wire.png"))
