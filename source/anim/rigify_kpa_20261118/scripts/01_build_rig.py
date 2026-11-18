# 1단계: 병사 모델에 Rigify 메타리그를 맞추고 → Generate Rig → 모델을 리그에 묶고 → 소총 · 참호 · 내보내기용 뼈대를 붙인다.
# 실행: blender -b --factory-startup --python scripts/01_build_rig.py
# 결과: work/kpa_rig_base.blend (다시 돌리면 같은 결과)  — 적(인민군) SK_CH_KPA_Rifleman + 모신나강
import os
import sys
import math

import bpy
import addon_utils
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

S = C.SCALE


def log(*a):
    print("[build]", *a)
    sys.stdout.flush()


def activate(obj, mode="OBJECT"):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mode != "OBJECT":
        bpy.ops.object.mode_set(mode=mode)


def material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    return m


bpy.ops.wm.read_factory_settings(use_empty=True)
addon_utils.enable("rigify", default_set=True)
scene = bpy.context.scene
scene.render.fps = C.FPS
scene.unit_settings.system = "METRIC"

# ---------------------------------------------------------------- 1. 병사 모델 (게임 크기로)
bpy.ops.import_scene.fbx(filepath=C.MODEL_FBX, use_anim=False, automatic_bone_orientation=False)
arm0 = next(o for o in bpy.data.objects if o.type == "ARMATURE")
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arm0.scale = (S, S, S)
bpy.context.view_layer.update()
for m in meshes:
    mw = m.matrix_world.copy()
    m.parent = None
    m.data.transform(mw)
    m.matrix_world = Matrix.Identity(4)
    for mod in list(m.modifiers):
        m.modifiers.remove(mod)
activate(arm0)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
top = max((v.co.z for m in meshes for v in m.data.vertices))
log("model height (helmet top)", round(top, 4), "hips", round(arm0.data.bones["mixamorig:Hips"].head_local.z, 4))

COLORS = {"Body": (0.40, 0.36, 0.21), "Head": (0.62, 0.45, 0.34), "Velt": (0.30, 0.21, 0.12), "Hand": (0.62, 0.45, 0.34),
          "Cap": (0.33, 0.31, 0.18)}
for mat in bpy.data.materials:
    for key, col in COLORS.items():
        if key.lower() in mat.name.lower():
            mat.diffuse_color = (*col, 1.0)

src = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm0.data.bones}


def J(name):
    return src["mixamorig:" + name][0].copy()


def Tl(name):
    return src["mixamorig:" + name][1].copy()


# ---------------------------------------------------------------- 2. 메타리그를 모델의 관절에 맞춘다
bpy.ops.object.armature_human_metarig_add()
meta = bpy.context.active_object
meta.name = "KPA_Metarig"
activate(meta, "EDIT")
eb = meta.data.edit_bones
# 얼굴 · 가슴 · 골반 보조 · 손바닥 뼈는 뺀다 (원본 뼈대에 없다)
for b in list(eb["spine.006"].children_recursive):
    eb.remove(b)
for name in ("breast.L", "breast.R", "pelvis.L", "pelvis.R"):
    eb.remove(eb[name])
for side in ("L", "R"):
    for i in range(1, 5):
        palm = eb["palm.%02d.%s" % (i, side)]
        for child in list(palm.children):
            child.parent = eb["hand." + side]
        eb.remove(palm)
# 목은 원본처럼 한 마디
eb.remove(eb["spine.005"])
eb["spine.006"].parent = eb["spine.004"]

zaxes = {b.name: b.z_axis.copy() for b in eb}


def place(name, head, tail, connect=None):
    b = eb[name]
    if connect is not None:
        b.use_connect = connect
    b.head = head
    b.tail = tail


place("spine", J("Hips"), J("Spine"))
place("spine.001", J("Spine"), J("Spine1"))
place("spine.002", J("Spine1"), J("Spine2"))
place("spine.003", J("Spine2"), J("Neck"))
place("spine.004", J("Neck"), J("Head"), False)
place("spine.006", J("Head"), Tl("Head"), True)
for s, Sd in (("L", "Left"), ("R", "Right")):
    place("shoulder." + s, J(Sd + "Shoulder"), J(Sd + "Arm"))
    place("upper_arm." + s, J(Sd + "Arm"), J(Sd + "ForeArm"))
    place("forearm." + s, J(Sd + "ForeArm"), J(Sd + "Hand"))
    place("hand." + s, J(Sd + "Hand"), J(Sd + "HandMiddle1"))
    place("thigh." + s, J(Sd + "UpLeg"), J(Sd + "Leg"))
    place("shin." + s, J(Sd + "Leg"), J(Sd + "Foot"))
    place("foot." + s, J(Sd + "Foot"), J(Sd + "ToeBase"))
    place("toe." + s, J(Sd + "ToeBase"), Tl(Sd + "ToeBase"))
    # 뒤꿈치 표식: 발목 아래 뒤쪽 바닥, 발 폭만큼
    ankle = J(Sd + "Foot")
    sign = 1.0 if s == "L" else -1.0
    hy = ankle.y + 0.055 * S / 0.777
    eb["heel.02." + s].head = Vector((ankle.x - sign * 0.04, hy, 0.0))
    eb["heel.02." + s].tail = Vector((ankle.x + sign * 0.04, hy, 0.0))
    for m, r in (("Thumb", "thumb"), ("Index", "f_index"), ("Middle", "f_middle"), ("Ring", "f_ring"), ("Pinky", "f_pinky")):
        for i in (1, 2, 3):
            head = J("%sHand%s%d" % (Sd, m, i))
            tail = J("%sHand%s%d" % (Sd, m, i + 1)) if i < 3 else Tl("%sHand%s%d" % (Sd, m, i))
            place("%s.%02d.%s" % (r, i, s), head, tail, i > 1)
# 굽힘 축(롤)은 기본 메타리그의 관례를 그대로 따른다
for b in eb:
    if b.name in zaxes and not b.name.startswith("heel"):
        b.align_roll(zaxes[b.name])
bpy.ops.object.mode_set(mode="OBJECT")
for s in ("L", "R"):
    for limb in ("upper_arm.", "thigh."):
        meta.pose.bones[limb + s].rigify_parameters.segments = 1   # 원본 뼈와 1:1 (비틀림 분절 없음)

# ---------------------------------------------------------------- 3. Generate Rig
activate(meta)
bpy.ops.pose.rigify_generate()
rig = bpy.context.active_object
rig.name = C.RIG_NAME
rig.data.name = C.RIG_NAME
log("rig bones", len(rig.data.bones))
for pb in rig.pose.bones:
    pb.rotation_mode = "QUATERNION"
for s in ("L", "R"):
    for parent in ("upper_arm_parent.", "thigh_parent."):
        pb = rig.pose.bones[parent + s]
        pb["IK_FK"] = 0.0
        pb["IK_Stretch"] = 0.0
        pb["pole_vector"] = True
# Rigify는 IK 사슬 뼈에 ik_stretch 0.1을 둔다. 그러면 IK가 굽히는 대신 뼈를 2~4% 줄여(눌러) 목표에 닿아
# DEF 뼈 길이가 바뀌고, 길이를 바꿀 수 없는 Humanoid 내보내기와 손 · 발이 1~1.5 cm 어긋났다 → 0
for pb in rig.pose.bones:
    if pb.ik_stretch > 0:
        pb.ik_stretch = 0.0
rig.pose.bones["torso"]["neck_follow"] = 0.0
rig.pose.bones["torso"]["head_follow"] = 0.0

# ---------------------------------------------------------------- 4. 모델을 리그에 묶는다 (원본 가중치를 DEF 뼈로)
for m in meshes:
    for vg in m.vertex_groups:
        exp = C.SRC2EXP.get(vg.name)
        if exp is None:
            log("unmapped vertex group", vg.name)
            continue
        vg.name = C.EXP2DEF[exp]
        if vg.name not in rig.data.bones:
            log("MISSING DEF", vg.name)
    mod = m.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    m.parent = rig

# ---------------------------------------------------------------- 5. 소총 (게임 AlliedM1 좌표에 맞춤)
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=C.RIFLE_FBX, use_anim=False)
new = [o for o in bpy.data.objects if o not in before]
bpy.context.view_layer.update()
parts = [o for o in new if o.type == "MESH"]
verts = []
for o in parts:
    o.data.transform(o.matrix_world)
for o in parts:
    mw = Matrix.Identity(4)
    o.parent = None
    o.matrix_world = mw
others = [o.name for o in new if o.type != "MESH"]
activate(parts[0])
for o in parts:
    o.select_set(True)
bpy.ops.object.join()
rifle_mesh = bpy.context.active_object
rifle_mesh.name = C.RIFLE_MESH
for name in others:
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
vs = rifle_mesh.data.vertices
ymin = min(v.co.y for v in vs)
ymax = max(v.co.y for v in vs)
muzzle_ring = [v.co for v in vs if v.co.y < ymin + 0.004]
bore_z = sum(c.z for c in muzzle_ring) / len(muzzle_ring)
bore_x = sum(c.x for c in muzzle_ring) / len(muzzle_ring)
log("rifle y", round(ymin, 4), round(ymax, 4), "len", round(ymax - ymin, 4), "muzzle ring z", round(bore_z, 4), "x", round(bore_x, 4),
    "ring z range", round(min(c.z for c in muzzle_ring), 4), round(max(c.z for c in muzzle_ring), 4))
y0 = ymin + C.RIFLE_MUZZLE_Z
z0 = bore_z - C.RIFLE_BORE_Y
# rifle 뼈의 rest: X 총 오른쪽(-X), Y 총구(-Y), Z 위
RIFLE_REST = Matrix(((-1, 0, 0, 0.0), (0, -1, 0, -0.30), (0, 0, 1, 1.00), (0, 0, 0, 1)))
for v in vs:
    ux, uy, uz = -v.co.x, v.co.z - z0, y0 - v.co.y
    v.co = RIFLE_REST @ C.gun(ux, uy, uz)
# 측정: 개머리판 끝, 노리쇠 손잡이(오른쪽으로 가장 튀어나온 점)
inv = RIFLE_REST.inverted()
loc = [inv @ v.co for v in vs]          # 뼈 로컬 (X 오른쪽, Y 총구, Z 위)
butt = [p for p in loc if p.y < min(q.y for q in loc) + 0.01]
handle = max(loc, key=lambda p: p.x)
log("rifle local: muzzle y", round(max(p.y for p in loc), 4), "butt y", round(min(p.y for p in loc), 4),
    "butt z range", round(min(p.z for p in butt), 4), round(max(p.z for p in butt), 4),
    "op-rod handle (x right, y fwd, z up)", tuple(round(c, 4) for c in handle),
    "z range all", round(min(p.z for p in loc), 4), round(max(p.z for p in loc), 4))
for sec in (-0.30, -0.20, -0.10, 0.0, 0.10, 0.20, 0.30, 0.45):
    ring = [p for p in loc if abs(p.y - sec) < 0.012]
    if ring:
        log("  section y=%.2f  x %.3f..%.3f  z %.3f..%.3f" % (sec, min(p.x for p in ring), max(p.x for p in ring), min(p.z for p in ring), max(p.z for p in ring)))
for mat in rifle_mesh.data.materials:
    if mat:
        mat.diffuse_color = (0.33, 0.19, 0.09, 1)

# ---------------------------------------------------------------- 6. 리그에 소총 뼈 · 손잡이 표식을 넣는다
hand_rest = {s: rig.data.bones["hand_ik." + s].matrix_local.copy() for s in ("L", "R")}
hand_len = {s: rig.data.bones["hand_ik." + s].length for s in ("L", "R")}


def frame(f, b):
    f = f.normalized()
    b = (b - f * b.dot(f)).normalized()
    return Matrix((f, b, f.cross(b))).transposed()


def grip_matrix(side, wrist, fingers, back):
    """총 좌표(게임식)로 준 손목 위치 · 손가락 방향 · 손등 방향 → rest 상태의 hand_ik 목표 행렬."""
    r0 = hand_rest[side].to_3x3()
    m0 = frame(r0.col[1], Vector((0, 0, 1)))                       # T포즈: 손가락 = 뼈 Y, 손등 = +Z
    rr = RIFLE_REST.to_3x3()
    m1 = frame(rr @ C.gun(*fingers), rr @ C.gun(*back))
    q = m1 @ m0.inverted()
    out = (q @ r0).to_4x4()
    out.translation = RIFLE_REST @ C.gun(*wrist)
    return out


# 손목 위치는 게임(AlliedRifleFit)의 접점에서 출발해 이 모델 손 크기에 맞춰 렌더로 고쳤다
GRIPS = {   # 모신나강: 오른손 = 개머리 손목(메시 y 0.06~0.11), 왼손 = 총몸 앞쪽 나무(메시 y -0.40)
    "R": grip_matrix("R", (0.034, -0.100, -0.200), (0.0, 0.42, 1.0), (1.0, 0.10, 0.0)),
    "L": grip_matrix("L", (-0.050, -0.055, 0.300), (1.0, 0.40, 0.12), (0.30, -1.0, 0.0)),
}

activate(rig, "EDIT")
eb = rig.data.edit_bones


def add_bone(name, matrix, length, parent, deform=False):
    b = eb.new(name)
    b.head = (0, 0, 0)
    b.tail = (0, length, 0)
    b.matrix = matrix
    b.length = length
    b.parent = eb[parent] if parent else None
    b.use_deform = deform
    return b


add_bone("rifle", RIFLE_REST, 0.30, "root", True)
for s in ("L", "R"):
    add_bone("grip." + s, GRIPS[s], hand_len[s], "rifle")
for name, off in (("rifle_muzzle", C.gun(0, C.RIFLE_BORE_Y, C.RIFLE_MUZZLE_Z)), ("rifle_butt", C.gun(*C.RIFLE_BUTT)),
                  ("rifle_top", C.gun(0, 0.10, 0)), ("rifle_handle", Vector(handle))):
    m = RIFLE_REST.copy()
    m.translation = RIFLE_REST @ off
    add_bone(name, m, 0.05, "rifle")
bpy.ops.object.mode_set(mode="OBJECT")
for name in ("rifle", "grip.L", "grip.R", "rifle_muzzle", "rifle_butt", "rifle_top", "rifle_handle"):
    rig.pose.bones[name].rotation_mode = "QUATERNION"
coll = rig.data.collections.new("Rifle")
for name in ("rifle", "grip.L", "grip.R"):
    coll.assign(rig.data.bones[name])
for s in ("L", "R"):
    con = rig.pose.bones["hand_ik." + s].constraints.new("COPY_TRANSFORMS")
    con.name = "RifleGrip"
    con.target = rig
    con.subtarget = "grip." + s
    con.influence = 1.0
# 미리보기용 총검(게임 에셋에는 없음): 총구 오른쪽 아래에 붙은 4각 날, 길이 C.BAYONET_LEN
bpy.ops.mesh.primitive_cube_add(size=1.0)
bay = bpy.context.active_object
bay.name = "Bayonet"
bay.scale = (0.012, C.BAYONET_LEN, 0.012)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
for v in bay.data.vertices:
    v.co = RIFLE_REST @ (C.gun(0.016, -0.004, C.RIFLE_MUZZLE_Z + C.BAYONET_LEN * 0.5 - 0.06) + v.co)
bay.data.materials.append(material("Bayonet", (0.55, 0.56, 0.58)))
bvg = bay.vertex_groups.new(name="rifle")
bvg.add(list(range(len(bay.data.vertices))), 1.0, "REPLACE")
bmod = bay.modifiers.new("Armature", "ARMATURE")
bmod.object = rig
bay.parent = rig
vg = rifle_mesh.vertex_groups.new(name="rifle")
vg.add(list(range(len(vs))), 1.0, "REPLACE")
mod = rifle_mesh.modifiers.new("Armature", "ARMATURE")
mod.object = rig
rifle_mesh.parent = rig

# ---------------------------------------------------------------- 7. 내보내기용 뼈대 (원본 계층 + 흔한 Humanoid 이름 + Root + Rifle)
exp = arm0
exp.name = "Armature"
exp.data.name = "KPA_Export"
activate(exp, "EDIT")
eb = exp.data.edit_bones
for b in list(eb):          # 끝 뼈(HeadTop_End · 손가락 4번째 · Toe_End)는 변형에 쓰이지 않으므로 뺀다
    if b.name not in C.SRC2EXP:
        eb.remove(b)
for b in eb:
    b.name = C.SRC2EXP[b.name]
root = eb.new("Root")
root.head = (0, 0, 0)
root.tail = (0, 0.10, 0)
root.roll = 0
eb["Hips"].parent = root
rb = eb.new("Rifle")
rb.head = (0, 0, 0)
rb.tail = (0, 0.30, 0)
rb.matrix = RIFLE_REST
rb.length = 0.30
rb.parent = root
for name, off in (("Rifle_Muzzle", C.gun(0, C.RIFLE_BORE_Y, C.RIFLE_MUZZLE_Z)), ("Rifle_Top", C.gun(0, 0.10, 0))):
    b = eb.new(name)
    b.head = (0, 0, 0)
    b.tail = (0, 0.05, 0)
    m = RIFLE_REST.copy()
    m.translation = RIFLE_REST @ off
    b.matrix = m
    b.length = 0.05
    b.parent = rb
bpy.ops.object.mode_set(mode="OBJECT")
for pb in exp.pose.bones:
    pb.rotation_mode = "QUATERNION"
worst = 0.0
for name, d in C.EXP2DEF.items():
    diff = (rig.data.bones[d].head_local - exp.data.bones[name].head_local).length
    worst = max(worst, diff)
    if diff > 0.002:
        log("rest joint differs", name, d, round(diff, 4))
log("worst rest joint difference DEF vs export", round(worst, 5))
exp.hide_render = True

# ---------------------------------------------------------------- 8. 참호 단면 (깊이 0.9, 바닥 폭 1.8, 흉벽 0.3)
trench = bpy.data.collections.new("Trench")
scene.collection.children.link(trench)


def box(name, x0, x1, y0, y1, z0, z1, color):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    o = bpy.context.active_object
    o.name = name
    o.scale = (x1 - x0, y1 - y0, z1 - z0)
    o.location = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material("Trench_" + name, color))
    for c in list(o.users_collection):
        c.objects.unlink(o)
    trench.objects.link(o)
    return o


fy = C.FRONT_WALL_Y
box("Floor", -1.6, 1.6, fy, fy + C.TRENCH_WIDTH, -0.05, 0.0, (0.34, 0.26, 0.18))
box("FrontWall", -1.6, 1.6, fy - 0.9, fy, -0.05, C.TRENCH_DEPTH, (0.30, 0.22, 0.15))
# 게임 단면(TASKS 11-16 "참호: FM 5-15"): 앞벽 위에 팔꿈치 받침 0.30 m(지면 높이), 그 앞에 흉장 0.30 m
box("Parapet", -1.6, 1.6, fy - 0.30 - 1.2, fy - 0.30, C.TRENCH_DEPTH, C.PARAPET_TOP, (0.42, 0.38, 0.27))
box("BackWall", -1.6, 1.6, fy + C.TRENCH_WIDTH, fy + C.TRENCH_WIDTH + 0.9, -0.05, C.TRENCH_DEPTH, (0.30, 0.22, 0.15))
# 흉벽 높이 표시 막대 (앞에서 볼 때 벽을 숨기고 이것만 남긴다)
box("ParapetLine", -1.6, 1.6, fy - 0.31, fy - 0.30, C.PARAPET_TOP - 0.006, C.PARAPET_TOP + 0.006, (0.9, 0.2, 0.1))
box("GroundPlane", -4.0, 4.0, -4.0, 4.0, -0.06, -0.05, (0.34, 0.26, 0.18))
bpy.data.objects["GroundPlane"].hide_render = True

meta.hide_render = True
meta.hide_viewport = True
os.makedirs(C.WORK, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=C.BASE_BLEND)
log("saved", C.BASE_BLEND)
