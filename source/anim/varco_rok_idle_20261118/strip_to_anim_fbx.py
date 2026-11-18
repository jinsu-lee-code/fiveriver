# VARCO 동작 FBX(모델 · 텍스처 포함, 약 30MB)에서 뼈대와 동작만 남겨 다시 내보낸다 (2026-11-18).
# 실행 (백그라운드 Blender 5.2, 원본 폴더 밖에서):
#   blender -b --factory-startup --python source/anim/varco_rok_idle_20261118/strip_to_anim_fbx.py -- <raw 폴더> <출력 폴더> [inspect]
# 출력: <출력 폴더>/A_ROK_*.fbx (클립 하나씩, 테이크 이름 = 파일명) + A_ROK_Varco_TPose.fbx (동작 없는 기준 자세: Humanoid 아바타용)
# 여러 번 실행해도 결과가 같다(같은 이름으로 덮어쓴다). 원본은 읽기만 한다.
import bpy, sys, os

argv = sys.argv[sys.argv.index("--") + 1:]
raw, out = argv[0], argv[1]
inspect = len(argv) > 2 and argv[2] == "inspect"

# raw 파일 키 → 게임 파일명 (ANIM_SPEC 1-2: A_ROK_…, PascalCase, 변형 _01)
NAMES = {
    "kneel_rifle_idle_1": "A_ROK_Trench_Idle_01",
    "kneel_rifle_idle_2": "A_ROK_Trench_Idle_02",
    "kneel_rifle_idle_3": "A_ROK_Trench_Idle_03",
    "kneel_combat_idle": "A_ROK_Trench_Idle_Alert",
    "kneel_look_guard": "A_ROK_Trench_Scan_01",
    "kneel_rifle_guard": "A_ROK_Trench_Scan_02",
    "kneel_clockwise_guard": "A_ROK_Trench_Scan_TurnRight",
    "kneel_counter_guard": "A_ROK_Trench_Scan_TurnLeft",
    "kneel_muzzle_raise_1": "A_ROK_Trench_LookOver_01",
    "kneel_muzzle_raise_2": "A_ROK_Trench_LookOver_02",
    "kneel_rifle_raise": "A_ROK_Trench_LookOver_03",
    "rifle_stand_idle_1": "A_ROK_Stand_Idle_01",
    "rifle_stand_idle_2": "A_ROK_Stand_Idle_02",
    "rifle_guard_idle_1": "A_ROK_Stand_Guard",
    "rifle_look_idle": "A_ROK_Stand_Look",
    "rifle_hand_idle_1": "A_ROK_Stand_HandAdjust",
    "stand_to_kneel_1": "A_ROK_StandToKneel_01",
    "stand_to_kneel_2": "A_ROK_StandToKneel_02",
    "crouch_kneel_idle": "A_ROK_Crouch_Idle",
    "startled_stagger": "A_ROK_Flinch_Stagger",
    "surprise": "A_ROK_Flinch",
}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load(path):
    clear()
    try:
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_anim=True)
    except Exception:
        bpy.ops.wm.fbx_import(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for o in list(bpy.data.objects):
        if o is not arm:
            bpy.data.objects.remove(o, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.textures):
        for item in list(block):
            block.remove(item)
    return arm


def export(arm, path, anim):
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        add_leaf_bones=False, bake_anim=anim, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=1.0,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False)


os.makedirs(out, exist_ok=True)
first = True
for key in sorted(NAMES):
    src = os.path.join(raw, key + ".fbx")
    if not os.path.isfile(src):
        print("MISSING", src)
        continue
    arm = load(src)
    action = arm.animation_data.action if arm.animation_data else None
    if action is None:
        print("NOACTION", key)
        continue
    name = NAMES[key]
    action.name = name
    start, end = int(round(action.frame_range[0])), int(round(action.frame_range[1]))
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = start, end
    scene.render.fps = 30
    if first:
        first = False
        if inspect:
            print("BONES", [(b.name, b.parent.name if b.parent else None) for b in arm.data.bones])
            print("ARM", tuple(arm.scale), tuple(arm.rotation_euler), arm.data.pose_position)
            for b in ("Hips", "LeftArm", "LeftUpLeg", "Head", "LeftFoot", "RightHand"):
                if b in arm.data.bones:
                    bone = arm.data.bones[b]
                    print("REST", b, [round(v, 3) for v in (arm.matrix_world @ bone.head_local)], [round(v, 3) for v in (arm.matrix_world @ bone.tail_local)])
        # 기준 자세(동작 없음): 뼈대의 rest 자세 = VARCO가 만든 T포즈
        saved = arm.animation_data.action
        arm.animation_data.action = None
        arm.data.pose_position = "REST"
        bpy.context.view_layer.update()
        export(arm, os.path.join(out, "A_ROK_Varco_TPose.fbx"), False)
        arm.data.pose_position = "POSE"
        arm.animation_data.action = saved
    if inspect:
        # 무릎 좌우 · 몸 방향 · 높이: 첫 프레임과 중간 프레임
        for f in (start, (start + end) // 2, end):
            scene.frame_set(f)
            p = {b: arm.matrix_world @ arm.pose.bones[b].head for b in ("Hips", "Head", "LeftFoot", "RightFoot", "LeftLeg", "RightLeg", "LeftArm", "RightArm", "LeftHand", "RightHand") if b in arm.pose.bones}
            print("POSE", key, f, {k: [round(c, 3) for c in v] for k, v in p.items()})
    export(arm, os.path.join(out, name + ".fbx"), True)
    print("DONE", key, name, start, end, os.path.getsize(os.path.join(out, name + ".fbx")))
print("ALL DONE")
