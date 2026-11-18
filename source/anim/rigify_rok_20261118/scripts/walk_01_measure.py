# 걷기 진단 1: 기존 A_ROK_Walk_Tired(혹은 다른 걷기 FBX)를 학도병 모델 · 일반 국군 모델에 옮겨 한 주기를 재고 렌더한다.
# 실행: blender -b --factory-startup --python scripts/walk_01_measure.py -- <clip.fbx> <tag> [render] [rate=1.32] [speed=1.1]
#   옮기는 방법: Unity Humanoid 리타깃처럼 "뼈가 향하는 방향"을 원본과 같게(뼈 길이는 대상 모델 것), 골반 위치는 엉덩이 높이 비율로.
#   잰 것: 무릎 굽힘(허벅지-정강이 사이 각), 발목(정강이-발 사이 각의 서기 대비 변화: + = 발끝이 정강이 쪽으로 들림),
#          발 기울기(발목→발볼 선이 수평 아래로 몇 도: + = 발끝 아래), 발볼 · 발목 · 뒤꿈치 높이, 접지 발 속도.
import io
import json
import math
import os
import subprocess
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
CLIP = argv[0] if argv else os.path.join(C.CLIP_DIR, "A_ROK_Walk_Tired.fbx")
TAG = argv[1] if len(argv) > 1 else "walk_tired"
DO_RENDER = "render" in argv
RATE = next((float(a.split("=")[1]) for a in argv if a.startswith("rate=")), 1.32)
GSPEED = next((float(a.split("=")[1]) for a in argv if a.startswith("speed=")), 1.1)
OUT = os.path.join(C.WORK, "walk", TAG)
os.makedirs(OUT, exist_ok=True)

MODELS = {
    # 이름: (FBX, 게임 프리팹 스케일, 렌더 색)
    "student": (os.path.join(C.ART, "Models", "Characters", "ROK_Soldier", "SK_CH_ROK_Soldier.fbx"), 0.82169086),
    "regular": (C.MODEL_FBX, C.SCALE),
}
MAIN_CHILD = {
    "Hips": "Spine", "Spine": "Spine1", "Spine1": "Spine2", "Spine2": "Neck", "Neck": "Head",
    "LeftShoulder": "LeftArm", "LeftArm": "LeftForeArm", "LeftForeArm": "LeftHand", "LeftHand": "LeftHandMiddle1",
    "RightShoulder": "RightArm", "RightArm": "RightForeArm", "RightForeArm": "RightHand", "RightHand": "RightHandMiddle1",
    "LeftUpLeg": "LeftLeg", "LeftLeg": "LeftFoot", "LeftFoot": "LeftToeBase",
    "RightUpLeg": "RightLeg", "RightLeg": "RightFoot", "RightFoot": "RightToeBase",
}
for s in ("Left", "Right"):
    for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"):
        MAIN_CHILD["%sHand%s1" % (s, f)] = "%sHand%s2" % (s, f)
        MAIN_CHILD["%sHand%s2" % (s, f)] = "%sHand%s3" % (s, f)


def short(n):
    return n.split(":")[-1]


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, ignore_leaf_bones=False)
    return [o for o in bpy.data.objects if o not in before]


def rot3(m):
    return m.to_3x3().normalized()


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
src_objs = load(CLIP)
src = next(o for o in src_objs if o.type == "ARMATURE")
for o in src_objs:
    if o.type == "MESH":
        o.hide_render = True
act = src.animation_data.action
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
NF = f1 - f0 + 1
print("CLIP", CLIP, act.name, f0, f1)
SB = {short(b.name): b for b in src.data.bones}
src_rest = {k: src.matrix_world @ b.matrix_local for k, b in SB.items()}


def src_world(frame):
    scene.frame_set(frame)
    return {short(pb.name): src.matrix_world @ pb.matrix for pb in src.pose.bones}


SRC = [src_world(f) for f in range(f0, f1 + 1)]
src_hip_h = src_rest["Hips"].translation.z

# 가설 시험: hippitch=20 → 골반(몸 전체)을 뒤로 20도 젖힘(상체는 서고 다리는 앞으로 감) — Unity 화면과 비교용
HIPPITCH = next((float(a.split("=")[1]) for a in argv if a.startswith("hippitch=")), 0.0)
if HIPPITCH:
    R_ = Matrix.Rotation(math.radians(-HIPPITCH), 4, "X")
    for W in SRC:
        p = W["Hips"].translation.copy()
        piv = Matrix.Translation(p) @ R_ @ Matrix.Translation(-p)
        for k in list(W):
            W[k] = piv @ W[k]

# 가설 시험: knee=0.5 → 무릎 굽힘을 그 비율로 줄여(발은 정강이를 따라 같이 돎) 게임 측정값과 비교한다
KNEE = next((float(a.split("=")[1]) for a in argv if a.startswith("knee=")), 1.0)
if KNEE != 1.0:
    for W in SRC:
        for S in ("Left", "Right"):
            hip, knee, ank = (W[S + n].translation for n in ("UpLeg", "Leg", "Foot"))
            thigh = (knee - hip).normalized()
            shin = (ank - knee).normalized()
            q = thigh.rotation_difference(shin)
            want = Quaternion(q.axis, q.angle * KNEE) @ thigh
            d = shin.rotation_difference(want).to_matrix().to_4x4()
            pivot = Matrix.Translation(knee) @ d @ Matrix.Translation(-knee)
            for n in ("Leg", "Foot", "ToeBase", "Toe_End"):
                if S + n in W:
                    m = pivot @ W[S + n]
                    if n == "Leg":
                        m.translation = W[S + n].translation
                    W[S + n] = m


def bone_dir(W, name, rest_tail=None):
    ch = MAIN_CHILD.get(name)
    if ch and ch in W:
        return (W[ch].translation - W[name].translation)
    return None


def retarget(tgt, scale):
    """대상 뼈대에 원본 동작을 옮긴 각 프레임의 월드 행렬(대상 원래 크기)."""
    TB = {short(b.name): b for b in tgt.data.bones}
    rest = {k: tgt.matrix_world @ b.matrix_local for k, b in TB.items()}
    k_h = rest["Hips"].translation.z / src_hip_h
    order = [short(b.name) for b in tgt.data.bones]          # 부모가 먼저
    out = []
    for W in SRC:
        F = {}
        for name in order:
            b = TB[name]
            rr = rot3(rest[name])
            if name in SB:
                d = rot3(W[name]) @ rot3(src_rest[name]).inverted()
                q = d @ rr
            else:
                q = rr
            if b.parent is None:
                pos = W["Hips"].translation * k_h if name == "Hips" and "Hips" in W else rest[name].translation
            else:
                p = short(b.parent.name)
                rel = rest[p].inverted() @ rest[name]
                pos = (F[p] @ rel).translation
            # 뼈가 향하는 방향을 원본과 같게 (Humanoid처럼: 대상의 기준 자세 차이(벌린 다리 등)를 따라가지 않음)
            ch = MAIN_CHILD.get(name)
            if ch and ch in TB and name in SB and ch in SB:
                want = (W[ch].translation - W[name].translation).normalized()
                local = rr.inverted() @ (rest[ch].translation - rest[name].translation)
                have = (q @ local).normalized()
                q = have.rotation_difference(want).to_matrix() @ q
            m = q.to_4x4()
            m.translation = pos
            F[name] = m
        out.append(F)
    return out, TB, rest


def apply_frames(tgt, TB, frames_world, act_name):
    if tgt.animation_data is None:
        tgt.animation_data_create()
    a = bpy.data.actions.new(act_name)
    tgt.animation_data.action = a
    inv = tgt.matrix_world.inverted()
    for fi, F in enumerate(frames_world):
        for pb in tgt.pose.bones:
            n = short(pb.name)
            b = pb.bone
            M = inv @ F[n]
            if b.parent is None:
                basis = b.matrix_local.inverted() @ M
            else:
                P = inv @ F[short(b.parent.name)]
                basis = (P @ (b.parent.matrix_local.inverted() @ b.matrix_local)).inverted() @ M
            pb.matrix_basis = basis
            pb.keyframe_insert("rotation_quaternion" if pb.rotation_mode == "QUATERNION" else "rotation_euler", frame=fi + 1)
            pb.keyframe_insert("location", frame=fi + 1)
    return a


def angle(a, b):
    return math.degrees(a.angle(b)) if a.length > 1e-9 and b.length > 1e-9 else 0.0


def pitch_down(v):
    """수평 아래로 몇 도 (+ = 발끝 아래)."""
    return math.degrees(math.atan2(-v.z, math.hypot(v.x, v.y)))


def measure(frames, rest, unit, label):
    """unit: 게임 크기로 바꾸는 배율. 땅 = z 0 (원본도 대상도 발바닥이 0 근처)."""
    sides = {"L": "Left", "R": "Right"}
    rows = []
    # 뒤꿈치 바닥점 = 기준 자세에서 발목 바로 아래 땅(발 뼈 공간에 고정)
    heel_local = {}
    toe_local = {}
    for s, S in sides.items():
        ra = rest[S + "Foot"]
        heel_w = Vector((ra.translation.x, ra.translation.y + 0.035 * (rest["Hips"].translation.z), 0.0))
        heel_local[s] = ra.inverted() @ heel_w
        rb = rest[S + "ToeBase"]
        tip_w = Vector((rb.translation.x, rb.translation.y - 0.10 * rest["Hips"].translation.z, 0.0))
        toe_local[s] = rb.inverted() @ tip_w
    def leg(F, S, s):
        hip, knee, ank, ball = (F[S + n].translation for n in ("UpLeg", "Leg", "Foot", "ToeBase"))
        heel = F[S + "Foot"] @ heel_local[s]
        tip = F[S + "ToeBase"] @ toe_local[s]
        return hip, knee, ank, ball, heel, tip
    R0 = {S: leg(rest, S, s) for s, S in sides.items()}
    ank_rest = {s: angle(R0[S][2] - R0[S][1], R0[S][3] - R0[S][2]) for s, S in sides.items()}
    pitch_rest = {s: pitch_down(R0[S][3] - R0[S][2]) for s, S in sides.items()}
    for i, F in enumerate(frames):
        r = {"f": i + 1, "hips_z": F["Hips"].translation.z * unit, "hips_y": F["Hips"].translation.y * unit}
        for s, S in sides.items():
            hip, knee, ank, ball, heel, tip = leg(F, S, s)
            r["knee_" + s] = angle(knee - hip, ank - knee)
            r["dorsi_" + s] = ank_rest[s] - angle(ank - knee, ball - ank)
            r["pitch_" + s] = pitch_down(ball - ank)
            r["pitch_rel_" + s] = pitch_down(ball - ank) - pitch_rest[s]
            r["ankle_z_" + s] = ank.z * unit
            r["ball_z_" + s] = ball.z * unit
            r["heel_z_" + s] = heel.z * unit
            r["tip_z_" + s] = tip.z * unit
            r["ball_y_" + s] = ball.y * unit
            r["heel_y_" + s] = heel.y * unit
            r["ankle_y_" + s] = ank.y * unit
            r["ankle_x_" + s] = ank.x * unit
        rows.append(r)
    # 한 주기 = 마지막 프레임이 첫 프레임과 같다고 보고 n-1 간격
    n = len(rows) - 1
    dt = 1.0 / 30.0
    ball_rest = {s: R0[S][3].z * unit for s, S in sides.items()}
    for s in ("L", "R"):
        for i, r in enumerate(rows):
            j = (i + 1) if i + 1 < len(rows) else 1
            # 땅에 닿은 점: 뒤꿈치(서 있을 때 높이 0) 또는 발볼(서 있을 때 높이 ball_rest)이 1.5 cm 안
            heel_c = r["heel_z_" + s] < 0.015
            ball_c = r["ball_z_" + s] < ball_rest[s] + 0.015
            r["contact_" + s] = "heel+ball" if heel_c and ball_c else "heel" if heel_c else "ball" if ball_c else "-"
            r["low_" + s] = min(r["heel_z_" + s], r["ball_z_" + s] - ball_rest[s])
            ky = "heel_y_" if heel_c and not ball_c else "ball_y_"
            r["low_pt_" + s] = r["contact_" + s]
            # 닿은 점의 앞뒤 속도 (+ = 뒤로, 클립 시간 기준 게임 크기 m/s)
            r["v_" + s] = (rows[j][ky + s] - rows[i][ky + s]) / dt
    return rows, ank_rest, pitch_rest


def summarize(rows, label):
    n = len(rows) - 1
    S = {"label": label}
    ground = 0.0
    S["ground"] = 0.0
    for s in ("L", "R"):
        st = [r for r in rows[:n] if r["contact_" + s] != "-"]
        sw = [r for r in rows[:n] if r["contact_" + s] == "-"]
        S[s] = {
            "stance_frames": len(st), "swing_frames": len(sw),
            "knee_stance_min_max": [round(min(r["knee_" + s] for r in st), 1), round(max(r["knee_" + s] for r in st), 1)] if st else None,
            "knee_swing_max": round(max(r["knee_" + s] for r in sw), 1) if sw else None,
            "knee_all": [round(min(r["knee_" + s] for r in rows), 1), round(max(r["knee_" + s] for r in rows), 1)],
            "dorsi_all": [round(min(r["dorsi_" + s] for r in rows), 1), round(max(r["dorsi_" + s] for r in rows), 1)],
            "dorsi_swing_max": round(max(r["dorsi_" + s] for r in sw), 1) if sw else None,
            "pitch_rel_swing_min": round(min(r["pitch_rel_" + s] for r in sw), 1) if sw else None,
            "ankle_z_max": round(max(r["ankle_z_" + s] for r in rows) - ground, 3),
            "ball_z_max": round(max(r["ball_z_" + s] for r in rows) - ground, 3),
            "tip_z_max": round(max(r["tip_z_" + s] for r in rows) - ground, 3),
            "heel_z_max": round(max(r["heel_z_" + s] for r in rows) - ground, 3),
            "tip_minus_heel_max": round(max(r["tip_z_" + s] - r["heel_z_" + s] for r in sw), 3) if sw else None,
            "stance_speed": [round(min(r["v_" + s] for r in st), 3), round(sum(r["v_" + s] for r in st) / len(st), 3),
                             round(max(r["v_" + s] for r in st), 3)] if st else None,
            "stride_m": round(max(r["ankle_y_" + s] for r in rows) - min(r["ankle_y_" + s] for r in rows), 3),
            "ankle_x_mean": round(sum(r["ankle_x_" + s] for r in rows) / len(rows), 3),
        }
    S["hips_z"] = [round(min(r["hips_z"] for r in rows), 3), round(max(r["hips_z"] for r in rows), 3)]
    S["hips_y_drift"] = round(rows[-1]["hips_y"] - rows[0]["hips_y"], 3)
    return S


RESULT = {"clip": CLIP, "frames": NF, "rate": RATE, "game_speed": GSPEED}
# 원본 뼈대 그대로 (원본 단위 m)
src_frames = SRC
src_rest_short = src_rest
rows, ar, pr = measure(src_frames, src_rest_short, 1.0, "source")
RESULT["source"] = summarize(rows, "source skeleton")
RESULT["source"]["hip_height_rest"] = round(src_hip_h, 4)
RESULT["source_rows"] = rows

targets = {}
for key, (path, gscale) in MODELS.items():
    objs = load(path)
    arm = next(o for o in objs if o.type == "ARMATURE")
    meshes = [o for o in objs if o.type == "MESH"]
    frames, TB, rest = retarget(arm, gscale)
    hip_h = rest["Hips"].translation.z
    unit = gscale                                  # 게임 크기
    rows_t, _, _ = measure(frames, rest, unit, key)
    S = summarize(rows_t, key)
    S["hip_height_game"] = round(hip_h * gscale, 4)
    # 게임에서 디딘 발 속도: 클립 속도 × 재생 배율 + 몸의 이동(GSPEED, 앞 = -y)
    for s in ("L", "R"):
        if S[s]["stance_speed"]:
            mean = S[s]["stance_speed"][1]
            S[s]["game_slip_mps"] = round(mean * RATE - GSPEED, 3)   # 0이면 안 미끄러짐 (- = 디딘 발이 앞으로 끌림)
    S["clip_speed_game_scale"] = round(sum(S[s]["stance_speed"][1] for s in ("L", "R")) / 2, 3)
    S["rate_for_no_slip"] = round(GSPEED / S["clip_speed_game_scale"], 3) if S["clip_speed_game_scale"] > 0 else None
    RESULT[key] = S
    RESULT[key + "_rows"] = rows_t
    act = apply_frames(arm, TB, frames, "%s_%s" % (TAG, key))
    targets[key] = (arm, meshes, objs, gscale)
    for o in objs:
        o.location.x += {"student": 0.0, "regular": 1.6}[key] / 1.0

with io.open(os.path.join(OUT, "measure.json"), "w", encoding="utf-8") as fh:
    json.dump(RESULT, fh, ensure_ascii=False, indent=1)
for k in ("source", "student", "regular"):
    print("SUMMARY", k, json.dumps(RESULT[k], ensure_ascii=False))

# 표: 프레임마다 (학도병, 게임 크기)
with io.open(os.path.join(OUT, "student_frames.tsv"), "w", encoding="utf-8") as fh:
    keys = ["f", "hips_z", "knee_L", "knee_R", "dorsi_L", "dorsi_R", "pitch_rel_L", "pitch_rel_R", "ankle_z_L", "ankle_z_R",
            "ball_z_L", "ball_z_R", "heel_z_L", "heel_z_R", "tip_z_L", "tip_z_R", "low_pt_L", "low_pt_R", "v_L", "v_R"]
    fh.write("\t".join(keys) + "\n")
    for r in RESULT["student_rows"]:
        fh.write("\t".join(("%.3f" % r[k]) if isinstance(r[k], float) else str(r[k]) for k in keys) + "\n")

if DO_RENDER:
    # 학도병만 렌더: 게임 크기로 줄이고, 게임처럼 앞으로 걸어가게(몸 이동 = GSPEED / RATE m per 클립 초) 해서 미끄러짐도 보이게
    arm, meshes, objs, gscale = targets["student"]
    reg = targets["regular"]
    for o in reg[2]:
        o.hide_render = True
    arm.scale = (gscale * arm.scale[0],) * 3 if False else arm.scale
    root = bpy.data.objects.new("WalkRoot", None)
    scene.collection.objects.link(root)
    for o in objs:
        if o.parent is None:
            o.parent = root
            o.location.x -= 0.0
    root.scale = (gscale, gscale, gscale)
    per_clip_s = GSPEED / RATE
    for fi in range(NF):
        root.location = Vector((0.0, -per_clip_s * fi / 30.0, 0.0))
        root.keyframe_insert("location", frame=fi + 1)
    # 땅 + 눈금(10 cm)
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, -1.0, 0))
    gp = bpy.context.object
    gp.name = "GroundPlane"
    mat = bpy.data.materials.new("Ground")
    mat.diffuse_color = (0.42, 0.38, 0.30, 1)
    gp.data.materials.append(mat)
    for k in range(-30, 6):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0.35, k * 0.1, 0.001))
        c = bpy.context.object
        c.scale = (0.25, 0.004, 0.001)
        m2 = bpy.data.materials.get("Tick") or bpy.data.materials.new("Tick")
        m2.diffuse_color = (0.1, 0.1, 0.1, 1)
        c.data.materials.append(m2)
    for o in meshes:
        for i, ms in enumerate(o.material_slots):
            if ms.material:
                ms.material.diffuse_color = (0.55, 0.52, 0.40, 1) if i % 2 == 0 else (0.45, 0.47, 0.35, 1)
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.show_object_outline = True
    scene.display.render_aa = "8"
    scene.render.resolution_x, scene.render.resolution_y = 240, 300
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.62, 0.66, 0.70)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    scene.camera = cam
    frames_dir = os.path.join(OUT, "frames")
    os.makedirs(frames_dir, exist_ok=True)
    files = []
    views = {"side": Vector((1, 0, 0)), "front": Vector((0, -1, 0.1)), "feet": Vector((1, 0, 0))}
    step = 1 if NF <= 40 else 2
    idx = list(range(0, NF - 1, step))
    for v, d in views.items():
        for fi in idx:
            scene.frame_set(fi + 1)
            body = root.location.copy()
            if v == "feet":
                tgt, sc = body + Vector((0, -0.05, 0.25)), 0.9
            else:
                tgt, sc = body + Vector((0, -0.05, 0.82)), 1.95
            cam.location = tgt + d.normalized() * 6
            cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
            cam.data.ortho_scale = sc
            p = os.path.join(frames_dir, "%s_%03d.png" % (v, fi + 1))
            scene.render.filepath = p
            bpy.ops.render.render(write_still=True)
            files.append(("%s f%d" % (v, fi + 1), p))
    cols = len(idx) if len(idx) <= 19 else (len(idx) + 1) // 2
    sheet = os.path.join(OUT, TAG + "_student_sheet.jpg")
    tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sheet.py")
    args = ["uv", "run", "--no-project", "-q", "--with", "pillow", "python", "-I", tool, sheet, str(cols)] + ["%s=%s" % (l, p) for l, p in files]
    res = subprocess.run(args, capture_output=True, text=True, timeout=300)
    print("SHEET", res.returncode, res.stdout.strip(), res.stderr.strip()[-300:])
