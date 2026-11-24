# 탄약수 작업 0단계: 로켓 · 바주카 모델 치수 확인 (읽기만). blender -b --factory-startup --python loader_00_probe.py
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

W = os.path.join(C.ART, "Models", "Weapons", "Bazooka")
for name in ("SM_WP_BazookaRocket.fbx", "SM_WP_Bazooka.fbx"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(W, name))
    for o in bpy.data.objects:
        if o.type != "MESH":
            print("OBJ", name, o.name, o.type, tuple(round(c, 4) for c in o.location), tuple(round(c, 4) for c in o.scale))
            continue
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        lo = [min(p[i] for p in pts) for i in range(3)]
        hi = [max(p[i] for p in pts) for i in range(3)]
        print("MESH", name, o.name, "verts", len(pts), "min", [round(c, 4) for c in lo], "max", [round(c, 4) for c in hi],
              "loc", tuple(round(c, 4) for c in o.location), "rot", tuple(round(c, 3) for c in o.rotation_euler), "scale", tuple(round(c, 4) for c in o.scale),
              "parent", o.parent.name if o.parent else None)
