# 그레이박스 FBX (ART_SPEC 1-1 "최종 파일명과 같은 이름의 임시 파일").
# 실측 크기 · 부품 이름 · 재질 슬롯 이름 · 피벗을 요청서대로 맞춘 기본 도형 모델을 만든다.
# 이미 있는 파일은 건드리지 않는다 (art 브랜치에서 받은 최종본 보호).
# 실행: "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python tools/make_graybox_fbx.py
#
# 좌표: 아래 함수는 Unity 기준 (x 오른쪽, y 위, z 앞)으로 받는다. 내보내기는 ART_SPEC 1-4
# (Forward -Z, Up Y, Apply Transform, Scale 1, FBX All). Blender에서는 앞이 -Y, 위가 +Z.
import math
import os
import sys

import bpy

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "five_river", "Assets", "_Project", "Art", "Models")
SX = -1.0  # Unity x → Blender x 부호. 확인 (2026-10-27): +1이면 Unity에서 TrackL이 +X로 뒤집혀 나옴 → -1


def u2b(x, y, z):
    return (SX * x, -z, y)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return mat


def assign(obj, slot):
    obj.data.materials.clear()
    obj.data.materials.append(material(slot))


def box(name, size, center, slot, parent=None, pivot=None):
    """size: Unity (x, y, z) m, center: Unity 위치(부모 기준), pivot: 오브젝트 원점(없으면 center)."""
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    o.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    place(o, center, pivot, parent)
    assign(o, slot)
    return o


def cylinder(name, radius, length, center, slot, axis="z", parent=None, pivot=None, verts=16):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    if axis == "z":   # Unity 앞뒤 = Blender Y
        o.rotation_euler = (math.radians(90), 0, 0)
    elif axis == "x":
        o.rotation_euler = (0, math.radians(90), 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    place(o, center, pivot, parent)
    assign(o, slot)
    return o


def capsule(name, radius, height, slot):
    """발 중앙 피벗, 위 +Y"""
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, segments=16, ring_count=8)
    o = bpy.context.active_object
    me = o.data
    half = height / 2 - radius
    for v in me.vertices:
        v.co.z += half if v.co.z > 0 else -half
        v.co.z += height / 2
    o.name = name
    me.name = name
    assign(o, slot)
    return o


def place(o, center, pivot, parent):
    piv = pivot if pivot is not None else center
    # 메시를 원점 기준으로 옮겨 pivot이 원점이 되게
    off = tuple(c - p for c, p in zip(center, piv))
    for v in o.data.vertices:
        b = u2b(*off)
        v.co.x += b[0]
        v.co.y += b[1]
        v.co.z += b[2]
    o.location = u2b(*piv)
    if parent is not None:
        bpy.context.view_layer.update()  # 부모 matrix_world 갱신 뒤 계산
        o.parent = parent
        o.location = tuple(a - b for a, b in zip(u2b(*piv), parent.matrix_world.translation))


def empty(name, pos, parent=None):
    o = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(o)
    o.location = u2b(*pos)
    if parent is not None:
        bpy.context.view_layer.update()
        o.parent = parent
        o.location = tuple(a - b for a, b in zip(u2b(*pos), parent.matrix_world.translation))
    return o


def export(rel_path):
    path = os.path.join(ROOT, rel_path)
    if os.path.exists(path):
        print("skip", rel_path)
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={"MESH", "EMPTY"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type="FACE")
    print("made", rel_path)


def build_all():
    # ---- 소품 ----
    reset()
    box("SandbagWall", (2.0, 1.0, 0.6), (0, 0.5, 0), "M_PR_Sandbag", pivot=(0, 0, 0))
    export("Props/Sandbag/SM_PR_SandbagWall.fbx")

    reset()
    box("AmmoCrate", (0.7, 0.3, 0.4), (0, 0.15, 0), "M_PR_AmmoCrate", pivot=(0, 0, 0))
    export("Props/AmmoCrate/SM_PR_AmmoCrate.fbx")

    reset()
    box("Stake", (0.08, 1.2, 0.06), (0, 0.6, 0.04), "M_PR_Target", pivot=(0, 0, 0))
    box("Board", (0.5, 1.2, 0.05), (0, 1.1, 0), "M_PR_Target", pivot=(0, 0, 0))
    export("Props/Target/SM_PR_Target.fbx")

    reset()
    box("Blanket", (0.6, 0.35, 1.8), (0, 0.175, 0), "M_PR_Blanket", pivot=(0, 0, 0))
    box("Boot", (0.12, 0.16, 0.26), (0.08, 0.1, -0.95), "M_PR_Boot", pivot=(0, 0, 0))
    export("Props/BlanketBody/SM_PR_BlanketBody.fbx")

    # ---- 캐릭터 (그레이박스: 뼈대 없는 캡슐 + 머리 모양, 최종은 VARCO 자동 리깅 Humanoid) ----
    for file, height, body, gear, cap in [
        ("Characters/ROK_Soldier/SK_CH_ROK_Soldier.fbx", 1.70, "M_CH_ROK_Body", "M_CH_ROK_Gear", False),
        ("Characters/ROK_ParkYongpal/SK_CH_ROK_ParkYongpal.fbx", 1.72, "M_CH_ROK_Body", "M_CH_ROK_Gear", False),
        ("Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx", 1.68, "M_CH_KPA_Body", "M_CH_KPA_Gear", True),
    ]:
        reset()
        capsule("Body", 0.24, height - 0.2, body)
        if cap:
            box("Cap", (0.24, 0.1, 0.3), (0, height - 0.12, 0.02), gear, pivot=(0, 0, 0))
        else:
            box("Helmet", (0.34, 0.18, 0.36), (0, height - 0.14, 0), gear, pivot=(0, 0, 0))
        export(file)

    # ---- 무기 (피벗 = 손잡이, 총구 +Z) ----
    reset()
    body = box("Body", (0.05, 0.08, 1.10), (0, 0, 0.2), "M_WP_M1_Wood", pivot=(0, 0, 0))
    cylinder("Barrel", 0.011, 0.5, (0, 0.03, 0.55), "M_WP_M1_Metal", parent=body, pivot=(0, 0, 0))
    box("Bolt", (0.02, 0.02, 0.12), (0.02, 0.05, 0.05), "M_WP_M1_Metal", parent=body, pivot=(0.02, 0.05, 0.05))
    empty("ClipSlot", (0, 0.05, 0.08), parent=body)
    export("Weapons/M1Garand/SM_WP_M1Garand.fbx")

    reset()
    box("Clip", (0.02, 0.05, 0.06), (0, 0.025, 0), "M_WP_M1_Clip", pivot=(0, 0, 0))
    box("Rounds", (0.016, 0.06, 0.05), (0, 0.04, 0), "M_WP_M1_Brass", pivot=(0, 0, 0))
    export("Weapons/M1Garand/SM_WP_M1Garand_Clip.fbx")

    reset()
    box("ClipEmpty", (0.02, 0.05, 0.06), (0, 0.025, 0), "M_WP_M1_Clip", pivot=(0, 0, 0))
    export("Weapons/M1Garand/SM_WP_M1Garand_ClipEmpty.fbx")

    reset()
    body = cylinder("Body", 0.045, 1.52, (0, 0, 0.2), "M_WP_Bazooka", pivot=(0, 0, 0))
    empty("RocketSlot", (0, 0, -0.55), parent=body)
    export("Weapons/Bazooka/SM_WP_Bazooka.fbx")

    reset()
    cylinder("Rocket", 0.04, 0.6, (0, 0, 0.3), "M_WP_Bazooka", pivot=(0, 0, 0))
    export("Weapons/Bazooka/SM_WP_BazookaRocket.fbx")

    reset()
    box("Mosin", (0.05, 0.06, 1.23), (0, 0, 0.3), "M_WP_Mosin", pivot=(0, 0, 0))
    export("Weapons/Mosin/SM_WP_Mosin.fbx")

    # ---- 전차 (피벗 바닥 중앙, 포 방향 +Z, 실측 길이 8.1 m (포 포함) · 폭 3.0 · 높이 2.7) ----
    reset()
    hull = box("Hull", (3.0, 1.0, 6.0), (0, 0.95, 0), "M_VH_T34_Body", pivot=(0, 0, 0))
    box("TrackL", (0.5, 1.0, 6.4), (-1.35, 0.5, 0), "M_VH_T34_Track", pivot=(-1.35, 0.5, 0))
    box("TrackR", (0.5, 1.0, 6.4), (1.35, 0.5, 0), "M_VH_T34_Track", pivot=(1.35, 0.5, 0))
    turret = box("Turret", (2.1, 0.8, 2.6), (0, 1.85, -0.3), "M_VH_T34_Body", pivot=(0, 1.45, -0.3))
    gun = cylinder("Gun", 0.09, 4.4, (0, 1.9, 3.2), "M_VH_T34_Body", parent=turret, pivot=(0, 1.9, 1.0))
    export("Vehicles/T34_85/SM_VH_T34_85.fbx")


if __name__ == "__main__":
    if "--sx" in sys.argv:
        SX = float(sys.argv[sys.argv.index("--sx") + 1])
    build_all()
