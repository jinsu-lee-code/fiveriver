# 그레이박스 애니메이션 FBX (ANIM_SPEC 2 · 3번 파일명 그대로, "FBX 안 클립 이름 = 파일명").
# 뼈 3개(Hips · Spine · Head)짜리 뼈대가 제자리에서 길이만큼 움직인다. 최종은 VARCO(적 · 아군) · Blender(1인칭)로 교체.
# 이미 있는 파일은 건드리지 않는다. 클립 이름 · 루프 · 이벤트는 Unity 가져오기 설정(.meta)에 둔다 (AnimationGraybox.cs).
# 실행: "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python tools/make_graybox_anim.py
import math
import os

import bpy

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "five_river", "Assets", "_Project", "Art", "Animations")
FPS = 30

# 파일명, 폴더, 길이(초), 루프, 움직임 종류
CLIPS = [
    ("A_KPA_Idle_Aim", "KPA", 2.5, True, "breathe"),
    ("A_KPA_Walk_Rifle", "KPA", 1.0, True, "walk"),
    ("A_KPA_Run_Rifle", "KPA", 0.7, True, "run"),
    ("A_KPA_CrouchRun", "KPA", 0.8, True, "run"),
    ("A_KPA_Crouch_Aim", "KPA", 2.0, True, "crouch"),
    ("A_KPA_Fire_Stand", "KPA", 0.6, False, "recoil"),
    ("A_KPA_Fire_Crouch", "KPA", 0.6, False, "recoil"),
    ("A_KPA_HitReact", "KPA", 0.4, False, "hit"),
    ("A_KPA_Death_01", "KPA", 1.2, False, "fall_back"),
    ("A_KPA_Death_02", "KPA", 1.2, False, "fall_forward"),
    ("A_KPA_Death_03", "KPA", 1.2, False, "kneel"),
    ("A_ROK_Idle_Trench", "ROK", 3.0, True, "crouch"),
    ("A_ROK_Fire_Crouch", "ROK", 0.6, False, "recoil"),
    ("A_ROK_Call", "ROK", 3.5, False, "look_around"),
    ("A_ROK_Walk_Tired", "ROK", 1.2, True, "walk"),
    ("A_ROK_Idle_Stand", "ROK", 3.0, True, "breathe"),
    ("A_FP_M1_Idle", "FP", 2.0, True, "breathe"),
    ("A_FP_M1_ReloadEmpty", "FP", 2.5, False, "reload"),
    ("A_FP_M1_ReloadPartial", "FP", 3.2, False, "reload"),
    ("A_FP_M1_Equip", "FP", 0.5, False, "raise"),
    ("A_FP_M1_Unequip", "FP", 0.4, False, "lower"),
    ("A_FP_M1_Sprint", "FP", 0.7, True, "run"),
    ("A_FP_Resupply", "FP", 0.9, False, "reload"),
    ("A_FP_Bazooka_Idle", "FP", 2.0, True, "breathe"),
    ("A_FP_Bazooka_Equip", "FP", 0.7, False, "raise"),
    ("A_FP_Bazooka_Fire", "FP", 0.6, False, "recoil"),
    ("A_FP_Bazooka_Reload", "FP", 2.2, False, "reload"),
]


def rig():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = bpy.data.armatures.new("Armature")
    obj = bpy.data.objects.new("Armature", arm)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    hips = arm.edit_bones.new("Hips")
    hips.head, hips.tail = (0, 0, 0.95), (0, 0, 1.1)
    spine = arm.edit_bones.new("Spine")
    spine.head, spine.tail, spine.parent = (0, 0, 1.1), (0, 0, 1.45), hips
    head = arm.edit_bones.new("Head")
    head.head, head.tail, head.parent = (0, 0, 1.45), (0, 0, 1.7), spine
    bpy.ops.object.mode_set(mode="POSE")
    return obj


def key(obj, bone, frame, rot=(0, 0, 0), loc=(0, 0, 0)):
    pb = obj.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = tuple(math.radians(a) for a in rot)
    pb.location = loc
    pb.keyframe_insert("rotation_euler", frame=frame)
    pb.keyframe_insert("location", frame=frame)


def animate(obj, kind, frames):
    end = frames
    mid = frames / 2
    if kind == "breathe":
        for f, a in ((0, 0), (mid, 2), (end, 0)):
            key(obj, "Spine", f, rot=(a, 0, 0))
    elif kind in ("walk", "run"):
        amp = 6 if kind == "walk" else 10
        for f, a in ((0, 0), (frames * 0.25, amp), (mid, 0), (frames * 0.75, -amp), (end, 0)):
            key(obj, "Hips", f, rot=(0, 0, a), loc=(0, 0, 0.02 * abs(a) / amp))
    elif kind == "crouch":
        for f, a in ((0, 0), (mid, 1.5), (end, 0)):
            key(obj, "Hips", f, loc=(0, 0, -0.4))
            key(obj, "Spine", f, rot=(10 + a, 0, 0))
    elif kind == "recoil":
        for f, a in ((0, 0), (2, -6), (end, 0)):
            key(obj, "Spine", f, rot=(a, 0, 0))
    elif kind == "hit":
        for f, a in ((0, 0), (3, -12), (end, 0)):
            key(obj, "Spine", f, rot=(a, 4, 0))
    elif kind in ("fall_back", "fall_forward", "kneel"):
        target = {"fall_back": -85, "fall_forward": 85, "kneel": 30}[kind]
        drop = {"fall_back": -0.8, "fall_forward": -0.8, "kneel": -0.45}[kind]
        key(obj, "Hips", 0, rot=(0, 0, 0), loc=(0, 0, 0))
        key(obj, "Hips", end, rot=(target, 0, 0), loc=(0, 0, drop))
    elif kind == "look_around":
        for f, a in ((0, 0), (frames * 0.3, 40), (frames * 0.6, -40), (end, 0)):
            key(obj, "Head", f, rot=(0, 0, a))
    elif kind == "reload":
        for f, a in ((0, 0), (frames * 0.3, 20), (frames * 0.7, 20), (end, 0)):
            key(obj, "Spine", f, rot=(a, 0, 0))
    elif kind in ("raise", "lower"):
        a, b = (-40, 0) if kind == "raise" else (0, -40)
        key(obj, "Spine", 0, rot=(a, 0, 0))
        key(obj, "Spine", end, rot=(b, 0, 0))


def build(name, folder, seconds, loop, kind):
    path = os.path.join(ROOT, folder, name + ".fbx")
    if os.path.exists(path):
        print("skip", name)
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    obj = rig()
    frames = max(2, round(seconds * FPS))
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 0, frames
    obj.animation_data_create()
    action = bpy.data.actions.new(name)
    obj.animation_data.action = action
    animate(obj, kind, frames)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={"ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True)
    print("made", name)


for c in CLIPS:
    build(*c)
