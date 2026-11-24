# 2026-11-24 로켓포반 탄약수 클립: 키 → 내보내기 뼈대에 굽기 → 수치 → 미리보기(옆 · 3/4 · 위 · 사수 눈) → FBX · 소총 궤적 · 로켓 궤적.
# 실행: blender -b --python scripts/06_loader_clips.py -- <클립,...|all> [render] [export] [poses]
#   poses : 열쇠 자세만 한 장씩 렌더(빠른 확인) → work/loader/poses_*.png
# 같은 리그(work/rok_rig_base.blend) · 같은 굽기(rok_rig.Rig.bake_export) · 같은 FBX 설정(03_make_clips.export)을 쓴다.
# 기존 클립 파일은 건드리지 않는다(export/ 에 A_ROK_Loader_* 만 쓴다).
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
import rok_render as RR
import rok_rig
import rok_clips
import rok_clips_fire
import rok_clips_loader as LD

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
want = argv[0].split(",")
do_render = "render" in argv
do_export = "export" in argv
do_poses = "poses" in argv

bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
scene = bpy.context.scene
scene.render.fps = C.FPS
R = rok_rig.Rig()
clips, LIB = rok_clips.make_clips(R)
rok_clips_fire.add(R, LIB, clips)
NEW = LD.add(R, LIB, clips)
names = NEW if want == ["all"] else want
OUTW = os.path.join(C.WORK, "loader")
os.makedirs(OUTW, exist_ok=True)
# 11-24 관절 검사 반복용: LOADER_EXPORT 로 내보낼 폴더를 바꿀 수 있다(기본 export/). 바꾸면 metrics_loader.json 도 그 폴더에 쓴다.
EXPORT_DIR = os.environ.get("LOADER_EXPORT", C.EXPORT)
os.makedirs(EXPORT_DIR, exist_ok=True)
os.makedirs(C.PREVIEW, exist_ok=True)

BODY = ["Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"]
ARMS = ["LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand"]


def f3(v):
    return [round(float(c), 4) for c in v]


# ------------------------------------------------------------------ 장면 소품: 로켓 · 발사관 · 사수 인형
def import_mesh(path, name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in objs if o.type == "MESH"]
    for o in objs:
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    m = meshes[0]
    m.parent = None
    me = m.data
    me.transform(m.matrix_world)
    m.matrix_world = Matrix.Identity(4)
    m.name = name
    return m


def make_rocket_template():
    path = os.path.join(C.ART, "Models", "Weapons", "Bazooka", "SM_WP_BazookaRocket.fbx")
    m = import_mesh(path, "RocketTemplate")
    vs = [v.co for v in m.data.vertices]
    ys = [v.y for v in vs]
    y0, y1 = min(ys), max(ys)
    cx = (min(v.x for v in vs) + max(v.x for v in vs)) * 0.5
    cz = (min(v.z for v in vs) + max(v.z for v in vs)) * 0.5
    width = lambda lo, hi: max((abs(v.x - cx) for v in vs if lo <= v.y <= hi), default=0)
    w_lo, w_hi = width(y0, y0 + 0.05), width(y1 - 0.05, y1)
    # 꼬리 = 날개가 있는 넓은 쪽. 꼬리 끝을 원점, 탄두를 -Y로
    tail_y = y1 if w_hi > w_lo else y0
    sign = 1.0 if tail_y == y1 else -1.0
    for v in m.data.vertices:
        p = v.co
        v.co = Vector(((p.x - cx), (p.y - tail_y) * sign, (p.z - cz)))
    print("ROCKET mesh: length %.3f, fins half-width %.3f / nose %.3f, tail at %s end" % (y1 - y0, max(w_lo, w_hi), min(w_lo, w_hi), "+Y" if sign > 0 else "-Y"))
    m.hide_render = True
    return m


def rocket_obj(template, name):
    o = bpy.data.objects.get(name)
    if o is None:
        o = bpy.data.objects.new(name, template.data)
        scene.collection.objects.link(o)
    return o


def build_props():
    tpl = make_rocket_template()
    moving = rocket_obj(tpl, "Rocket_Moving")
    piles = [rocket_obj(tpl, "Rocket_Pile%d" % i) for i in range(3)]
    for i, o in enumerate(piles):
        o.matrix_world = LD.pile_frame(i)
    # 발사관 (1인칭 모델 그대로, 실제 크기). 뒤 끝을 LD.REAR, 축을 LD.AXIS
    tube = import_mesh(os.path.join(C.ART, "Models", "Weapons", "Bazooka", "SM_WP_Bazooka.fbx"), "Tube")
    y = -LD.AXIS
    z = LD.TUBE_UP
    x = y.cross(z)
    mt = Matrix((x, y, z)).transposed().to_4x4()
    mt.translation = LD.REAR + LD.AXIS * 0.582
    tube.matrix_world = mt
    # 사수 인형 (단순 도형): 서 있는 사람, 오른 어깨에 발사관
    mat = bpy.data.materials.new("GunnerGrey")
    mat.diffuse_color = (0.42, 0.45, 0.38, 1)
    head_parts = []

    def prim(kind, loc, scale, rot=(0, 0, 0), head=False):
        if kind == "cyl":
            bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=1, depth=1, location=loc, rotation=rot)
        elif kind == "sph":
            bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=1, location=loc)
        else:
            bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
        o = bpy.context.active_object
        o.scale = scale
        o.data.materials.append(mat)
        o.name = "Gunner_" + kind
        head_parts.append(o)   # 사수 눈 보기에서는 사수 몸을 숨긴다(게임에도 1인칭 몸은 없음)
        return o

    g = LD.GUNNER
    prim("cyl", g + Vector((0.10, 0.0, 0.43)), (0.07, 0.07, 0.86))
    prim("cyl", g + Vector((-0.10, 0.0, 0.43)), (0.07, 0.07, 0.86))
    prim("cube", g + Vector((0.0, 0.0, 1.15)), (0.38, 0.22, 0.60))
    prim("sph", LD.EYE_P + Vector((0, 0.07, 0.0)), (0.10, 0.11, 0.12), head=True)
    prim("sph", LD.HELMET, (0.135, 0.15, 0.09), head=True)
    # 오른팔: 어깨 → 손잡이
    sh = g + Vector((-0.20, 0.0, 1.40))
    grip = LD.REAR + LD.AXIS * 0.43 + LD.TUBE_UP * -0.09
    d = grip - sh
    o = prim("cyl", (sh + grip) * 0.5, (0.05, 0.05, d.length))
    head_parts.append(o)
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return tpl, moving, piles, tube, head_parts


# ------------------------------------------------------------------ 키 · 굽기 · 수치 (03_make_clips.make와 같은 순서)
def make(name):
    clip = clips[name]
    n = clip.frames
    times = [i / C.FPS for i in range(n)]
    if clip.loop:
        times[-1] = clip.length
    poses = [clip.sample(t) for t in times]
    if clip.loop:
        poses[-1] = clip.sample(0.0)
    for obj in (R.rig, R.exp):
        if obj.animation_data is None:
            obj.animation_data_create()
        old = bpy.data.actions.get(name + ("_rig" if obj is R.rig else ""))
        if old:
            bpy.data.actions.remove(old)
        act = bpy.data.actions.new(name + ("_rig" if obj is R.rig else ""))
        act.use_fake_user = True
        obj.animation_data.action = act
    R.prev_q = {}
    prev = {}
    rec = []
    for i, P in enumerate(poses):
        f = i + 1
        scene.frame_set(f)
        R.apply(P)
        # 손이 목표에 닿았나 (IK는 늘어나지 않으므로 못 닿으면 짧게 멈춘다)
        reach = {}
        for s in ("L", "R"):
            tgt = R.pb["hand_ik." + s].matrix.translation
            got = R.pb["DEF-hand." + s].head
            reach[s] = (tgt - got).length
            reach["foot" + s] = (P["foot_" + s][0] - R.pb["DEF-foot." + s].head).length
        if os.environ.get("LOADER_DBG") and str(f) in os.environ["LOADER_DBG"].split(","):
            g = lambda n: tuple(round(c, 3) for c in R.pb[n].head)
            print("DBG", name, f, "tgtR", tuple(round(c, 3) for c in R.pb["hand_ik.R"].matrix.translation), "handR", g("DEF-hand.R"),
                  "elbR", g("DEF-forearm.R"), "shR", g("DEF-upper_arm.R"), "poleR", tuple(round(c, 3) for c in R.pb["upper_arm_ik_target.R"].matrix.translation),
                  "tgtL", tuple(round(c, 3) for c in R.pb["hand_ik.L"].matrix.translation), "elbL", g("DEF-forearm.L"), "pelvis", g("DEF-spine"))
        R.key_controls(f)
        world = R.bake_export(f, prev)
        m = R.measure(world)
        m["reach"] = reach
        m["P_grip"] = (P["grip_L"], P["grip_R"])
        m["world"] = {k: v.copy() for k, v in world.items()}
        m["wristL"] = R.pb["DEF-hand.L"].head.copy()
        m["wristR"] = R.pb["DEF-hand.R"].head.copy()
        m["handL_m"] = R.pb["DEF-hand.L"].matrix.copy()
        m["handR_m"] = R.pb["DEF-hand.R"].matrix.copy()
        rec.append(m)
    scene.frame_start, scene.frame_end = 1, n
    return clip, rec


def body_delta(world, name):
    return (world[name].to_3x3().normalized() @ R.exp_rest[name].to_3x3().normalized().inverted()).to_quaternion()


def angle_diff(qa, qb):
    return math.degrees(qa.rotation_difference(qb).angle)


TRACK = {}


def pose_track(P):
    R.apply(P)
    w = R.bake_export(1, {})
    return {b: body_delta(w, b) for b in BODY + ARMS}


def ref_track(ref):
    if ref in TRACK:
        return TRACK[ref]
    if ref == "A_ROK_Idle_Stand":
        TRACK[ref] = pose_track(LIB["stand"])
    elif ref in clips:
        TRACK[ref] = pose_track(clips[ref].sample(clips[ref].length if ref == "A_ROK_Loader_Load" else 0.0))
    return TRACK.get(ref)


def mesh_low_now():
    dg = bpy.context.evaluated_depsgraph_get()
    low = 9.0
    for nm in ("Body_Low", "Cap_Low"):
        o = bpy.data.objects.get(nm)
        if o is None:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        low = min(low, min((ev.matrix_world @ v.co).z for v in me.vertices))
        ev.to_mesh_clear()
    return low


def summarize(clip, rec):
    n = len(rec)
    first, last = rec[0]["world"], rec[-1]["world"]
    S = {
        "clip": clip.name, "frames": n, "seconds": round((n - 1) / C.FPS, 3), "loop": clip.loop,
        "hips_z": [round(min(r["hips"].z for r in rec), 3), round(max(r["hips"].z for r in rec), 3)],
        "head_top_max": round(max(r["head_top"].z for r in rec), 3),
        "reach_err_max_mm": {s: round(1000 * max(r["reach"][s] for r in rec), 1) for s in ("L", "R")},
        "reach_err_frames_over_5mm": {s: [i + 1 for i, r in enumerate(rec) if r["reach"][s] > 0.005] for s in ("L", "R")},
        "foot_err_max_mm": {s: round(1000 * max(r["reach"]["foot" + s] for r in rec), 1) for s in ("L", "R")},
        "foot_err_frames_over_5mm": {s: [i + 1 for i, r in enumerate(rec) if r["reach"]["foot" + s] > 0.005] for s in ("L", "R")},
        "first_last_bone_deg": round(max(angle_diff(body_delta(first, b), body_delta(last, b)) for b in BODY + ARMS), 2),
        "def_err_max": round(max(r["def_err"] for r in rec), 5),
        "def_err_frames_over_2mm": [i + 1 for i, r in enumerate(rec) if r["def_err"] > 0.002],
    }
    for which, ref, w in (("start", clip.start_ref, first), ("end", clip.end_ref, last)):
        if not ref or ref == "self":
            continue
        tr = ref_track(ref)
        if tr is None:
            continue
        d = max((angle_diff(body_delta(w, b), tr[b]), b) for b in BODY + ARMS)
        S["connect_" + which] = {"vs": ref + (" 끝" if ref == "A_ROK_Loader_Load" else " 첫 프레임"), "deg": round(d[0], 2), "bone": d[1]}
    return S


# ------------------------------------------------------------------ 로켓 궤적 · 후폭풍 · 머리 높이
def prop_metrics(clip, rec):
    """로켓을 쥔 프레임의 손목-로켓 거리, 발사관에 넣는 동안 로켓이 축 위에 있는지, 발사 가능 순간 머리가 후폭풍 원뿔 밖인지."""
    out = {"hold_err_max_mm": {"L": 0.0, "R": 0.0}}
    for i, r in enumerate(rec):
        m, state, cl, cr = clip.prop(min(i / C.FPS, clip.length))
        r["prop"] = (m, state, cl, cr)
    # 원뿔: 뒤 끝에서 축 반대 방향, 반각 30도(FM M1A1 "바로 뒤에 서지 말 것" — 반각은 가정), 길이 3 m 안
    def in_cone(p):
        v = p - LD.REAR
        back = -LD.AXIS
        d = v.dot(back)
        if d <= 0:
            return False, 180.0
        ang = math.degrees(math.acos(max(-1, min(1, d / max(1e-6, v.length)))))
        return ang < 30.0 and v.length < 3.0, ang
    last = rec[-1]
    head = last["head_c"]
    inside, ang = in_cone(head)
    out["end_head_vs_cone_deg"] = round(ang, 1)
    out["end_head_in_cone"] = inside
    out["head_to_rear_end_m"] = round((head - LD.REAR).length, 3)
    return out


GRIP_RADIAL = math.hypot(LD.ROCKET_R + 0.02, 0.062)    # grip_on 의 손목-축 거리


def _lerp_m(a, b, w):
    q = a.to_quaternion().slerp(b.to_quaternion(), w)
    m = q.to_matrix().to_4x4()
    m.translation = a.translation.lerp(b.translation, w)
    return m


def attach_prop(clip, rec):
    """11-24 관절 검사: 손에 든 로켓을 손에 붙인다.
    키 자세에서는 손 목표를 로켓 표면에서 계산하므로 손과 로켓이 맞지만, 키 사이에서는 손(IK 목표 보간)과 로켓(꼬리 위치 보간 · 회전 slerp)이
    다른 길로 움직여 3~11 cm 떨어졌다. 손이 정확히 쥔 프레임(기준)에서의 '손 기준 로켓 자리'를 기준 사이에서 보간해 그 프레임 손에 붙이고,
    두 손으로 든 프레임은 오른손 기준으로 붙인 뒤 왼손 쪽으로 돌려 맞춘다. 상태 · 쥠 값 · 다른 상태(pile · tube · hidden)는 그대로."""
    n = len(rec)
    want = lambda st, s: st == "hands" or st == "hand_" + s
    hm = {"L": [r["handL_m"] for r in rec], "R": [r["handR_m"] for r in rec]}
    wr = {"L": [r["wristL"] for r in rec], "R": [r["wristR"] for r in rec]}
    anchors = {"L": [], "R": []}
    key_frames = set(int(math.floor((t + 0.02) * C.FPS + 1e-6)) for t in getattr(clip, "key_times", []))   # 팔 묶음 지연 0.02초
    for i, r in enumerate(rec):
        m, st, cl, cr = r["prop"]
        for s_ in ("L", "R"):
            if not want(st, s_):
                continue
            w = m.inverted() @ wr[s_][i]
            exact = abs(math.hypot(w.x, w.z) - GRIP_RADIAL) < 0.006 and -0.05 < -w.y < LD.ROCKET_LEN + 0.05
            if exact or i in key_frames:          # 키 자세(손 목표를 그 로켓 자리에서 계산한 프레임)도 기준
                anchors[s_].append(i)
    held = [r["prop"][1] in ("hands", "hand_R", "hand_L") for r in rec]

    def interp(s_, i):
        A = anchors[s_]
        lo = max((a for a in A if a <= i), default=None)
        hi = min((a for a in A if a >= i), default=None)
        # 같은 '든' 구간 안의 기준만 쓴다
        if lo is not None and not all(held[lo:i + 1]):
            lo = None
        if hi is not None and not all(held[i:hi + 1]):
            hi = None
        off = lambda a: hm[s_][a].inverted() @ rec[a]["prop"][0]
        if lo is None and hi is None:
            return None
        if lo is None or lo == hi:
            return off(hi if lo is None else lo)
        if hi is None:
            return off(lo)
        return _lerp_m(off(lo), off(hi), (i - lo) / (hi - lo))

    changed = 0
    for i, r in enumerate(rec):
        m, st, cl, cr = r["prop"]
        if st not in ("hands", "hand_R", "hand_L"):
            continue
        prim = "L" if st == "hand_L" else "R"
        o = interp(prim, i)
        if o is None:
            continue
        M = hm[prim][i] @ o
        # 주된 손목이 로켓 표면 쥐는 거리(GRIP_RADIAL)에 오게 로켓을 축에 수직으로 옮김 (기준 사이 보간이 표면을 벗어나지 않게)
        w = M.inverted() @ wr[prim][i]
        rad = math.hypot(w.x, w.z)
        if rad > 1e-4 and -0.05 < -w.y < LD.ROCKET_LEN + 0.05:
            shift_local = Vector((w.x, 0.0, w.z)) * ((rad - GRIP_RADIAL) / rad)
            M = Matrix.Translation(M.to_3x3() @ shift_local) @ M
        if st == "hands":
            oL = interp("L", i)
            if oL is not None:
                ML = hm["L"][i] @ oL
                pR = wr["R"][i]
                wl_loc = M.inverted() @ wr["L"][i]           # 지금 M에서 왼손목 자리
                wl_tgt = ML.inverted() @ wr["L"][i]          # 왼손 기준으로 붙였을 때의 자리 (로켓 공간)
                a = (M @ wl_tgt) - pR
                b = wr["L"][i] - pR
                if a.length > 1e-4 and b.length > 1e-4:
                    q = a.rotation_difference(b)
                    T = Matrix.Translation(pR) @ q.to_matrix().to_4x4() @ Matrix.Translation(-pR)
                    M = T @ M
        if (M.translation - m.translation).length > 1e-5:
            changed += 1
        r["prop"] = (M, st, cl, cr)
    clip.attached = [r["prop"] for r in rec]
    print("ATTACH", clip.name, "anchors L %d R %d, moved %d frames" % (len(anchors["L"]), len(anchors["R"]), changed))


def prop_track(clip, rec):
    """Unity 좌표(루트 기준, x 오른쪽 · y 위 · z 앞) 로켓: pos = 꼬리 끝(축 위), forward = 탄두 쪽, up.
    wrist_L/R = 그 프레임 손목의 로켓 공간 위치(Unity 좌표) — 게임이 손을 로켓에 붙이거나 로켓을 손에 붙일 때 쓴다."""
    frames = []
    for f, r in enumerate(rec, 1):
        m, state, cl, cr = r["prop"]
        tail = m.translation
        nose = LD.r_axis(m)
        up = (m.to_3x3() @ Vector((0, 0, 1))).normalized()
        inv = m.inverted()

        def local(p):
            q = inv @ p                       # 로켓 로컬(Blender): -Y = 탄두, +Z = up
            return f3((-q.x, q.z, -q.y))      # → Unity 로켓 공간: z 탄두 쪽, y up, x = 오른쪽
        entry = {"f": f, "pos": f3(C.b2u(tail)), "forward": f3(C.b2u(nose)), "up": f3(C.b2u(up)),
                 "state": state, "grip_L": round(cl, 3), "grip_R": round(cr, 3),
                 "wrist_L": local(r["wristL"]), "wrist_R": local(r["wristR"])}
        if state == "tube":
            # 발사관에 넣는 정도: 0 = 탄두가 뒤 끝, 1 = 다 들어감 (게임은 실제 발사관 뒤 끝 · 축에 놓는다)
            d = (tail - LD.REAR).dot(LD.AXIS)
            entry["insert"] = round((d + LD.ROCKET_LEN) / (LD.ROCKET_LEN + LD.SEAT), 4)
        frames.append(entry)
    return {"clip": clip.name, "fps": C.FPS, "frames": frames,
            "rocket_length_m": LD.ROCKET_LEN, "seat_m": LD.SEAT,
            "pile_slots": [{"pos": f3(C.b2u(LD.PILE[i])), "forward": f3(C.b2u(LD.r_axis(LD.pile_frame(i))))} for i in range(len(LD.PILE))],
            "gunner": {"feet": f3(C.b2u(LD.GUNNER)), "eye": f3(C.b2u(LD.EYE_P)), "rear_mouth": f3(C.b2u(LD.REAR)),
                       "tube_axis": f3(C.b2u(LD.AXIS)), "tap": f3(C.b2u(LD.TAP))},
            "note": "루트 = 탄약수 원점(애니메이터 원점, 바닥), 게임 크기 m. state: hands(두 손) hand_R hand_L tube(발사관에 넣는 중: "
                    "insert 0~1을 실제 Rocket_RearMouth · 발사관 축에 놓을 것) pile(0번 자리에 세워 둠) hidden(발사관 안). "
                    "로켓 원점 = 꼬리 끝 축 위, forward = 탄두 쪽."}


def rifle_track(clip, rec):
    frames = []
    for f, r in enumerate(rec, 1):
        w = r["world"]
        o = w["Rifle"].translation
        fwd = (w["Rifle_Muzzle"].translation - o).normalized()
        up = (w["Rifle_Top"].translation - o).normalized()
        frames.append({"f": f, "pos": f3(C.b2u(o)), "forward": f3(C.b2u(fwd)), "up": f3(C.b2u(up)),
                       "grip_L": round(r["P_grip"][0], 3), "grip_R": round(r["P_grip"][1], 3)})
    return {"clip": clip.name, "fps": C.FPS, "frames": frames,
            "note": "pos = AlliedM1 원점(게임 소총 원점: 총구 +0.724 m, 개머리 끝 -0.381 m). forward = 총구 쪽, up = 가늠자 쪽. "
                    "AlliedM1.rotation = root.rotation * LookRotation(forward, up), position = root.TransformPoint(pos)."}


def export(clip):
    act = R.exp.animation_data.action
    act.name = clip.name
    bpy.ops.object.select_all(action="DESELECT")
    R.exp.hide_set(False)
    R.exp.hide_viewport = False
    R.exp.select_set(True)
    bpy.context.view_layer.objects.active = R.exp
    scene.frame_start, scene.frame_end = 1, clip.frames
    scene.name = clip.name
    path = os.path.join(EXPORT_DIR, clip.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False)
    return path


# ------------------------------------------------------------------ 렌더
EYE_CAM = None


def eye_camera(yaw=0.0, pitch=-2.0, name="EyeCam"):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        scene.collection.objects.link(cam)
    cam.data.type = "PERSP"
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(70.0)          # PlayerConfig/WeaponConfig.hipFov 70 (세로)
    cam.data.clip_start = 0.01
    cam.location = LD.EYE_P
    # 사수는 -Y를 본다. yaw + = 왼쪽, pitch + = 위
    cam.rotation_euler = (math.radians(90 + pitch), 0, math.radians(180 + yaw))
    return cam


def set_props_frame(clip, i, props):
    tpl, moving, piles, tube, head_parts = props
    if getattr(clip, "attached", None) and i < len(clip.attached):
        m, state, cl, cr = clip.attached[i]
    else:
        m, state, cl, cr = clip.prop(min(i / C.FPS, clip.length))
    moving.matrix_world = m
    moving.hide_render = state == "hidden"
    used = getattr(clip, "pile_used", None)
    for k, o in enumerate(piles):
        o.hide_render = not (k in (clip.pile_show if hasattr(clip, "pile_show") else (1, 2)))


def render(clip, rec, props):
    out = os.path.join(OUTW, "frames", clip.name)
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        os.remove(os.path.join(out, f))
    cam = RR.setup(scene, (300, 340))
    gp = bpy.data.objects["GroundPlane"]
    gp.location.z = 0.05
    for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine", "Floor"):
        if o in bpy.data.objects:
            bpy.data.objects[o].hide_render = True
    gp.hide_render = False
    n = len(rec)
    step = clip.step
    if os.environ.get("LOADER_STEP"):
        step = float(os.environ["LOADER_STEP"])
    idx = sorted(set([int(round(k * step * C.FPS)) for k in range(int(clip.length / step + 1e-6) + 1)] + [n - 1]))
    idx = [i for i in idx if i < n]
    tgt, scale = clip.cam
    files = []
    cols = len(idx) if len(idx) <= 12 else (len(idx) + 1) // 2
    head_parts = props[4]
    for v in clip.views:
        if files and len(files) % cols:
            files += [("", "-")] * (cols - len(files) % cols)
        for i in idx:
            scene.frame_set(i + 1)
            set_props_frame(clip, i, props)
            p = os.path.join(out, "%s_%03d.png" % (v, i + 1))
            if v.startswith("eye"):
                yaw = {"eye": 0.0, "eyeR": -120.0}[v]
                ec = eye_camera(yaw=yaw)
                scene.camera = ec
                scene.render.resolution_x, scene.render.resolution_y = 480, 270
                for o in head_parts:
                    o.hide_render = True
                scene.render.filepath = p
                bpy.ops.render.render(write_still=True)
                for o in head_parts:
                    o.hide_render = False
                scene.camera = cam
                scene.render.resolution_x, scene.render.resolution_y = 300, 340
                # 같은 칸 크기로 맞추려고 모음 이미지에서 줄인다 (sheet.py는 첫 이미지 크기를 쓴다)
            else:
                RR.aim(cam, v, target=tgt, scale=scale)
                for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine", "Floor"):
                    if o in bpy.data.objects:
                        bpy.data.objects[o].hide_render = True
                scene.render.filepath = p
                bpy.ops.render.render(write_still=True)
            files.append(("%s f%d %.2fs" % (v, i + 1, i / C.FPS), p))
    sheet = os.path.join(C.PREVIEW, clip.name + ".jpg")
    tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "loader_sheet.py")
    args = [os.environ.get("UV_EXE", "uv"), "run", "--no-project", "-q", "--with", "pillow", "python", "-I", tool, sheet, str(cols)]
    args += ["%s=%s" % (l, p) for l, p in files]
    try:
        res = subprocess.run(args, capture_output=True, text=True, timeout=300)
        print("SHEET", res.returncode, res.stdout.strip(), res.stderr.strip()[-300:])
    except Exception as e:
        print("SHEET FAILED", e)


def screen_metrics(clip, rec, props):
    """사수 눈(세로 시야각 70도 = hipFov, 16:9 → 가로 약 102도)에서 탄약수 몸 · 소총 · 로켓이 화면에 들어오는 정도.
    in_view: 화면 안 정점 비율의 최대, frames_visible: 하나라도 보인 프레임 수(초), centre: 가운데 40%(가로 · 세로 0.3~0.7)에 든 정점 수.
    가로 70도(세로 약 43도)로 좁게 본 경우도 함께 잰다."""
    from bpy_extras.object_utils import world_to_camera_view
    cam = eye_camera(yaw=0.0)
    scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
    out = {}
    for label, vfov in (("v70", 70.0), ("h70", math.degrees(2 * math.atan(math.tan(math.radians(35)) / (16 / 9))))):
        cam.data.angle = math.radians(vfov)
        worst, vis, centre, rocket_vis = 0.0, 0, 0, 0
        for i in range(len(rec)):
            scene.frame_set(i + 1)
            set_props_frame(clip, i, props)
            bpy.context.view_layer.update()
            dg = bpy.context.evaluated_depsgraph_get()
            n_in = n_all = n_c = 0
            r_in = 0
            for nm in ("Body_Low", "Cap_Low", "M1_Garand", "Rocket_Moving"):
                o = bpy.data.objects.get(nm)
                if o is None or (nm == "Rocket_Moving" and o.hide_render):
                    continue
                ev = o.evaluated_get(dg)
                me = ev.to_mesh()
                mw = ev.matrix_world
                for k, v in enumerate(me.vertices):
                    if k % 3:
                        continue
                    c = world_to_camera_view(scene, cam, mw @ v.co)
                    n_all += 1
                    if c.z > 0 and 0 <= c.x <= 1 and 0 <= c.y <= 1:
                        n_in += 1
                        if nm == "Rocket_Moving":
                            r_in += 1
                        if 0.3 <= c.x <= 0.7 and 0.3 <= c.y <= 0.7:
                            n_c += 1
                ev.to_mesh_clear()
            frac = n_in / max(1, n_all)
            worst = max(worst, frac)
            vis += 1 if n_in else 0
            rocket_vis += 1 if r_in else 0
            centre += n_c
        out[label] = {"in_view_max": round(worst, 4), "frames_visible": vis, "seconds_visible": round(vis / C.FPS, 2),
                      "rocket_frames_visible": rocket_vis, "centre40_vertices": centre}
    cam.data.angle = math.radians(70.0)
    return out


# ------------------------------------------------------------------ 실행
if "probe" in argv:
    J = ["DEF-spine", "DEF-spine.003", "DEF-upper_arm.L", "DEF-upper_arm.R", "DEF-hand.L", "DEF-hand.R", "DEF-shin.L", "DEF-shin.R", "DEF-spine.006"]
    for k, P in LD.DEBUG.items():
        R.apply(P)
        line = " ".join("%s(%.2f,%.2f,%.2f)" % ((j.replace("DEF-", ""),) + tuple(R.pb[j].head)) for j in J)
        err = {s: (R.pb["hand_ik." + s].matrix.translation - R.pb["DEF-hand." + s].head).length for s in ("L", "R")}
        tg = {s: tuple(round(c, 2) for c in R.pb["hand_ik." + s].matrix.translation) for s in ("L", "R")}
        dist = {s: (R.pb["hand_ik." + s].matrix.translation - R.pb["DEF-upper_arm." + s].head).length for s in ("L", "R")}
        print("PROBE", k, "err L %.0f R %.0f mm" % (err["L"] * 1000, err["R"] * 1000), "target", tg, "sh-target L %.2f R %.2f" % (dist["L"], dist["R"]), line)
    R.apply(LD.DEBUG["S0"])
    w = R.bake_export(1, {})
    errs = sorted(((R.pb[C.EXP2DEF[n]].head - w[n].translation).length, n) for n in C.EXP2DEF)[-4:]
    print("PROBE S0 def errs", [(n, round(e * 1000, 1)) for e, n in errs])
    for s_ in ("L", "R"):
        hip = R.pb["DEF-thigh." + s_].head; ank = R.pb["DEF-foot." + s_].head
        print("PROBE leg", s_, "hip-ankle %.3f max %.3f" % ((ank - hip).length, R.thigh + R.shin), "ankle target", tuple(round(c, 3) for c in LD.DEBUG["S0"]["foot_" + s_][0]), "got", tuple(round(c, 3) for c in ank))
    print("PROBE arm upper %.3f fore %.3f" % (R.upper, R.fore))
    print("PROBE rear", tuple(round(c, 3) for c in LD.REAR), "tap", tuple(LD.TAP), "latch", tuple(round(c, 3) for c in LD.LATCH))
    sys.exit(0)
props = build_props()
for nm in names:
    c = clips[nm]
    if nm == "A_ROK_Loader_Load":
        c.pile_show = (0, 1, 2)
    elif nm in ("A_ROK_Loader_Kneel_Idle", "A_ROK_Loader_Brace", "A_ROK_Loader_Wave"):
        c.pile_show = (0, 1, 2)
    else:
        c.pile_show = (1, 2)

# 11-24: 이음 비교용 기준 자세(ref_track)는 bake_export 가 프레임 1에 키를 넣으므로, 클립을 굽기 전에 미리 계산해 둔다.
# (전에는 클립을 구운 뒤 처음 계산될 때 그 클립의 프레임 1을 기준 자세로 덮어썼다 — 7개를 정해진 순서로 한꺼번에 만들 때는
#  덮어쓴 자세가 원래 첫 프레임과 같아 드러나지 않았지만, 일부만 다시 만들면 첫 프레임이 서기 자세 등으로 바뀌었다.)
for nm in names:
    for ref in (clips[nm].start_ref, clips[nm].end_ref):
        if ref and ref != "self":
            ref_track(ref)

ALL = {}
for nm in names:
    clip, rec = make(nm)
    S = summarize(clip, rec)
    S.update(prop_metrics(clip, rec))
    attach_prop(clip, rec)
    S["mesh_low_end"] = None
    if "screen" in argv:
        S["player_screen"] = screen_metrics(clip, rec, props)
    ALL[nm] = S
    print("SUMMARY", json.dumps(S, ensure_ascii=False))
    if do_render:
        render(clip, rec, props)
    if do_export:
        path = export(clip)
        with io.open(os.path.join(EXPORT_DIR, clip.name + "_rifle.json"), "w", encoding="utf-8") as fh:
            json.dump(rifle_track(clip, rec), fh, ensure_ascii=False)
        with io.open(os.path.join(EXPORT_DIR, clip.name + "_prop.json"), "w", encoding="utf-8") as fh:
            json.dump(prop_track(clip, rec), fh, ensure_ascii=False)
        print("EXPORTED", path, os.path.getsize(path))

# 일부 클립만 다시 만들어도 나머지 클립 수치가 지워지지 않게 합친다 (11-24)
mpath = os.path.join(OUTW if EXPORT_DIR == C.EXPORT else EXPORT_DIR, "metrics_loader.json")
old = {}
if os.path.exists(mpath):
    with io.open(mpath, encoding="utf-8") as fh:
        old = json.load(fh)
old.update(ALL)
with io.open(mpath, "w", encoding="utf-8") as fh:
    json.dump(old, fh, ensure_ascii=False, indent=1)
