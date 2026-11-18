# 걷기 진단 0: A_ROK_Walk_Tired.fbx · 학도병 모델(SK_CH_ROK_Soldier.fbx)의 뼈대 · 프레임 · 크기 출력
# 실행: blender -b --factory-startup --python scripts/walk_00_inspect.py
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

WALK = os.path.join(C.CLIP_DIR, "A_ROK_Walk_Tired.fbx")
STUDENT = os.path.join(C.ART, "Models", "Characters", "ROK_Soldier", "SK_CH_ROK_Soldier.fbx")


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, ignore_leaf_bones=False)
    return [o for o in bpy.data.objects if o not in before]


bpy.ops.wm.read_factory_settings(use_empty=True)
objs = load(WALK)
for o in objs:
    print("WALK OBJ", o.name, o.type, tuple(round(v, 4) for v in o.scale), o.parent.name if o.parent else None)
arm = next(o for o in objs if o.type == "ARMATURE")
act = arm.animation_data.action if arm.animation_data else None
print("WALK ACTION", act.name if act else None, act.frame_range if act else None, bpy.context.scene.render.fps,
      bpy.context.scene.render.fps_base)
print("WALK BONES", len(arm.data.bones), [b.name for b in arm.data.bones][:60])
for b in arm.data.bones:
    if any(k in b.name for k in ("Hips", "UpLeg", "Leg", "Foot", "Toe")):
        print("  REST", b.name, tuple(round(v, 4) for v in (arm.matrix_world @ b.head_local)))

bpy.ops.wm.read_factory_settings(use_empty=True)
objs = load(STUDENT)
for o in objs:
    if o.type == "MESH":
        print("STU MESH", o.name, len(o.data.vertices), o.parent.name if o.parent else None, [m.name for m in o.data.materials][:6])
    else:
        print("STU OBJ", o.name, o.type, tuple(round(v, 4) for v in o.scale))
arm = next(o for o in objs if o.type == "ARMATURE")
print("STU BONES", len(arm.data.bones), [b.name for b in arm.data.bones])
for b in arm.data.bones:
    if any(k in b.name for k in ("Hips", "UpLeg", "Leg", "Foot", "Toe", "Head", "Hand")) and "Hand" not in b.name[-6:-1]:
        print("  REST", b.name, tuple(round(v, 4) for v in (arm.matrix_world @ b.head_local)))
zs = [(o.matrix_world @ v.co).z for o in objs if o.type == "MESH" for v in o.data.vertices]
print("STU MESH Z", min(zs), max(zs))
print("STU ACTIONS", [(a.name, tuple(a.frame_range)) for a in bpy.data.actions])
