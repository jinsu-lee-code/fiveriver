# 2단계: 기존 아군 클립(mixamorig 뼈대)의 자세를 병사 모델 비율의 뼈대로 옮겨 관절 각도 · 위치를 뽑는다.
# 방법: 뼈마다 "기준 자세 대비 월드 회전 변화"를 그대로 옮기고, 엉덩이 높이는 선 자세 엉덩이 높이 비율로 맞춘다
#       (Unity Humanoid 리타깃의 근사. Unity에서 실제로 보이는 것과 팔 · 손은 다르다: 게임은 팔을 소총에 맞춰 푼다).
# 실행: blender -b --python scripts/02_extract_ref.py
# 결과: work/ref_poses.json
import io
import json
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

CLIPS = ["A_ROK_Idle_Stand", "A_ROK_Idle_Trench", "A_ROK_Fire_Crouch"]

bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
exp = bpy.data.objects["Armature"]
scene = bpy.context.scene
order = [b.name for b in exp.data.bones]            # 부모가 먼저 나온다
exp_rest = {b.name: b.matrix_local.copy() for b in exp.data.bones}
hips_rest_h = exp_rest["Hips"].translation.z
out = {}
T_REST = {}       # T포즈 기준 뼈 회전 (첫 클립의 뼈대에서). 기준 자세가 T포즈가 아닌 파일(Fire_Crouch)은 이것과 비교한다
T_HIPS = [0.0]

for clip in CLIPS:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(C.CLIP_DIR, clip + ".fbx"), use_anim=True, automatic_bone_orientation=False)
    arm = next(o for o in bpy.data.objects if o not in before and o.type == "ARMATURE")
    action = arm.animation_data.action
    f0, f1 = int(round(action.frame_range[0])), int(round(action.frame_range[1]))
    mw = arm.matrix_world.copy()
    rest = {}
    for b in arm.data.bones:
        rest[b.name] = (mw @ b.matrix_local)
    arm_dir = (rest["mixamorig:LeftArm"].to_3x3() @ Vector((0, 1, 0))).normalized()
    if arm_dir.x > 0.9:
        if not T_REST:
            T_REST.update({k: v.copy() for k, v in rest.items()})
            T_HIPS[0] = rest["mixamorig:Hips"].translation.z
    else:
        print("  rest pose is not a T pose: using the T rest of the first clip")
        rest = {k: T_REST.get(k, v) for k, v in rest.items()}
    src_hips_h = T_HIPS[0]
    info = {"frames": [f0, f1], "src_rest_hips": src_hips_h, "samples": {}}
    print("CLIP", clip, f0, f1, "src rest hips", round(src_hips_h, 4),
          "LeftArm rest dir", tuple(round(v, 2) for v in (rest["mixamorig:LeftArm"].to_3x3() @ Vector((0, 1, 0))).normalized()),
          "LeftUpLeg rest dir", tuple(round(v, 2) for v in (rest["mixamorig:LeftUpLeg"].to_3x3() @ Vector((0, 1, 0))).normalized()))
    frames = sorted(set([f0, f1] + list(range(f0, f1 + 1, 5))))
    for f in frames:
        scene.frame_set(f)
        bpy.context.view_layer.update()
        deltas = {}
        for sname, ename in C.SRC2EXP.items():
            pb = arm.pose.bones.get(sname)
            if pb is None:
                continue
            pose = (mw @ pb.matrix).to_3x3().normalized()
            deltas[ename] = (pose @ rest[sname].to_3x3().normalized().inverted()).to_quaternion()
        hips = (mw @ arm.pose.bones["mixamorig:Hips"].matrix).translation * (hips_rest_h / src_hips_h)
        # 병사 뼈대에 적용해 관절 위치를 얻는다
        for name in order:
            pb = exp.pose.bones[name]
            if name not in deltas:
                pb.matrix_basis = Matrix.Identity(4)
                continue
            m = (deltas[name].to_matrix() @ exp_rest[name].to_3x3()).to_4x4()
            m.translation = hips if name == "Hips" else pb.matrix.translation
            pb.matrix = m
            bpy.context.view_layer.update()
        joints = {name: list(exp.pose.bones[name].head) for name in order}
        joints["HeadTop"] = list(exp.pose.bones["Head"].tail)
        joints["LeftToeTip"] = list(exp.pose.bones["LeftToes"].tail)
        joints["RightToeTip"] = list(exp.pose.bones["RightToes"].tail)
        info["samples"][str(f)] = {"delta": {k: list(v) for k, v in deltas.items()}, "hips": list(hips), "joints": joints}
        if f in (f0, f1):
            j = joints
            print("  f", f, "hips", [round(v, 3) for v in hips], "head", [round(v, 3) for v in j["Head"]],
                  "\n     LKnee", [round(v, 3) for v in j["LeftLowerLeg"]], "LAnkle", [round(v, 3) for v in j["LeftFoot"]], "LToe", [round(v, 3) for v in j["LeftToes"]],
                  "\n     RKnee", [round(v, 3) for v in j["RightLowerLeg"]], "RAnkle", [round(v, 3) for v in j["RightFoot"]], "RToe", [round(v, 3) for v in j["RightToes"]],
                  "\n     LHand", [round(v, 3) for v in j["LeftHand"]], "RHand", [round(v, 3) for v in j["RightHand"]],
                  "LShoulder", [round(v, 3) for v in j["LeftUpperArm"]], "RShoulder", [round(v, 3) for v in j["RightUpperArm"]])
    # 첫 · 끝 프레임 차 (반복 확인)
    a, b = info["samples"][str(f0)]["delta"], info["samples"][str(f1)]["delta"]
    worst = max(Quaternion(a[k]).rotation_difference(Quaternion(b[k])).angle for k in a)
    print("  first/last worst bone angle deg", round(worst * 57.2958, 2))
    out[clip] = info
    bpy.data.objects.remove(arm, do_unlink=True)

with io.open(C.REF_JSON, "w", encoding="utf-8") as fh:
    json.dump(out, fh)
print("saved", C.REF_JSON)
