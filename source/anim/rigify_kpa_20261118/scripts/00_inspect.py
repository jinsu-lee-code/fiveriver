# 0단계: 적 모델(SK_CH_KPA_Rifleman) · 모신나강(SM_WP_Mosin) 치수 출력 (읽기만)
# blender -b --factory-startup --python scripts/00_inspect.py
import os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "five_river", "Assets", "_Project", "Art"))
bpy.ops.wm.read_factory_settings(use_empty=True)
for rel in ("Models/Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx", "Models/Weapons/Mosin/SM_WP_Mosin.fbx"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, rel), use_anim=False, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    print("=====", rel)
    for o in new:
        line = "OBJ %s %s parent=%s loc=%s rot=%s scale=%s" % (o.type, o.name, o.parent.name if o.parent else None,
                                                           tuple(round(v, 4) for v in o.location), tuple(round(v, 4) for v in o.rotation_euler),
                                                           tuple(round(v, 4) for v in o.scale))
        if o.type == "MESH":
            ws = [o.matrix_world @ v.co for v in o.data.vertices]
            mn = Vector((min(p.x for p in ws), min(p.y for p in ws), min(p.z for p in ws)))
            mx = Vector((max(p.x for p in ws), max(p.y for p in ws), max(p.z for p in ws)))
            line += " verts=%d world_min=%s world_max=%s mats=%s" % (len(ws), tuple(round(v, 4) for v in mn), tuple(round(v, 4) for v in mx),
                                                                   [m.name for m in o.data.materials if m])
        print(line)
    for o in new:
        if o.type == "MESH" and "Mosin" in rel:
            # 길이 방향 단면: 축을 따라 10 cm마다 단면 범위
            ws = [o.matrix_world @ v.co for v in o.data.vertices]
            ext = [max(p[i] for p in ws) - min(p[i] for p in ws) for i in range(3)]
            ax = ext.index(max(ext))
            lo = min(p[ax] for p in ws)
            hi = max(p[ax] for p in ws)
            print("  long axis", "XYZ"[ax], "from", round(lo, 4), "to", round(hi, 4), "len", round(hi - lo, 4))
            steps = 24
            for k in range(steps + 1):
                c = lo + (hi - lo) * k / steps
                ring = [p for p in ws if abs(p[ax] - c) < (hi - lo) / steps / 2]
                if ring:
                    o1, o2 = [i for i in range(3) if i != ax]
                    print("   at %.3f  %s %.3f..%.3f  %s %.3f..%.3f  n=%d" % (c, "XYZ"[o1], min(p[o1] for p in ring), max(p[o1] for p in ring),
                                                                     "XYZ"[o2], min(p[o2] for p in ring), max(p[o2] for p in ring), len(ring)))
