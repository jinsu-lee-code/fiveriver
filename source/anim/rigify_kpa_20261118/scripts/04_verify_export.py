# 4단계: 내보낸 FBX를 다시 불러 뼈 이름 · 계층 · 길이 · 반복 이음매 · 엉덩이 높이를 확인한다.
# 실행: blender -b --factory-startup --python scripts/04_verify_export.py
import glob
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

for path in sorted(glob.glob(os.path.join(C.EXPORT, "A_KPA_*.fbx"))):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True, automatic_bone_orientation=False)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    bones = arm.data.bones
    roots = [b.name for b in bones if b.parent is None]
    chain = []
    b = bones.get("Head")
    while b:
        chain.append(b.name)
        b = b.parent
    act = arm.animation_data.action if arm.animation_data else None
    line = "%-36s bones %d roots %s headchain %s" % (os.path.basename(path), len(bones), roots, "<".join(chain))
    if act:
        f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
        sc = bpy.context.scene
        def snap(f):
            sc.frame_set(f)
            return {pb.name: (arm.matrix_world @ pb.matrix).copy() for pb in arm.pose.bones}
        a, z = snap(f0), snap(f1)
        hips = [None, None]
        hs = []
        for f in range(f0, f1 + 1):
            sc.frame_set(f)
            hs.append((arm.matrix_world @ arm.pose.bones["Hips"].matrix).translation.z)
        ang = max(math.degrees(a[n].to_quaternion().rotation_difference(z[n].to_quaternion()).angle) for n in a)
        pos = max((a[n].translation - z[n].translation).length for n in a)
        line += " | action %s frames %d-%d (%d) | hips %.3f-%.3f | first/last max %.2f deg %.4f m | fps %d" % (
            act.name, f0, f1, f1 - f0 + 1, min(hs), max(hs), ang, pos, sc.render.fps)
    print("VERIFY", line)
