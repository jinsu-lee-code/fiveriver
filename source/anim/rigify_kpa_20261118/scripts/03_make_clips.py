# 3단계: 클립을 리그에 키로 넣고 → 내보내기용 뼈대에 굽고 → 수치를 재고 → 미리보기를 렌더하고 → FBX로 내보낸다.
# 실행: blender -b --python scripts/03_make_clips.py -- <클립이름,...|all> [render] [export] [blend]
#   render: preview/<클립>.jpg 를 만든다 (타일 png는 work/frames/<클립>/)
#   export: export/<클립>.fbx 와 export/<클립>_rifle.json
#   blend : rig/rok_rigify_clips.blend 에 리그 + 모든 동작을 저장 (메시 텍스처 없음)
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
import kpa_clips as rok_clips
from rok_rig import ease

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
want = argv[0].split(",")
do_render = "render" in argv
do_export = "export" in argv
do_blend = "blend" in argv

bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
scene = bpy.context.scene
scene.render.fps = C.FPS
R = rok_rig.Rig()
clips, LIB = rok_clips.make_clips(R)
names = list(clips) if want == ["all"] else want
REF = {}
if os.path.exists(C.REF_JSON):
    with io.open(C.REF_JSON, encoding="utf-8") as fh:
        REF = json.load(fh)

BODY = ["Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"]
ARMS = ["LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand"]
os.makedirs(C.EXPORT, exist_ok=True)
os.makedirs(C.PREVIEW, exist_ok=True)


def f3(v):
    return [round(float(c), 4) for c in v]


def clip_poses(clip):
    """make()와 같은 프레임별 자세(골반 높이 보정 포함)."""
    n = clip.frames
    times = [i / C.FPS for i in range(n)]
    if clip.loop:
        times[-1] = clip.length
    poses = [clip.sample(t) for t in times]
    if clip.loop:
        poses[-1] = clip.sample(0.0) if not clip.reach_fix else poses[-1]
    if clip.reach_fix:
        reach_fix(clip, poses)
    return poses


_DROPS = {}


def clip_drops(name):
    """반복 달리기 클립의 프레임별 골반 내림(이어지는 출발 · 멈춤 클립의 끝을 여기에 맞춘다)."""
    if name not in _DROPS:
        c = clips[name]
        n = c.frames
        times = [i / C.FPS for i in range(n)]
        if c.loop:
            times[-1] = c.length
        raw = [c.sample(t) for t in times]
        fixed = clip_poses(c)
        _DROPS[name] = [raw[i]["pelvis_loc"].z - fixed[i]["pelvis_loc"].z for i in range(n)]
    return _DROPS[name]


def reach_fix(clip, poses):
    """다리가 다 펴져도 닿지 않는 프레임은 골반을 내린다(부드럽게, 주기 단위).
    clip.start_drop_ref / end_drop_ref = (반복 클립, 프레임): 처음 · 끝 6프레임에 걸쳐 그 클립의 내림 값으로 맞춘다."""
    d = reach_drops(clip, poses)
    n = len(poses)
    for ref, at_end in ((getattr(clip, "start_drop_ref", None), False), (getattr(clip, "end_drop_ref", None), True)):
        if not ref:
            continue
        target = clip_drops(ref[0])[ref[1]]
        for k in range(6):
            i = n - 1 - k if at_end else k
            w = ease(1.0 - k / 6.0, "inout")
            d[i] = d[i] * (1 - w) + target * w
    for i, P in enumerate(poses):
        P["pelvis_loc"] = P["pelvis_loc"] - Vector((0, 0, d[i]))
    return max(d) if d else 0.0


def reach_drops(clip, poses):
    lmax = 0.975 * (R.thigh + R.shin)
    n = len(poses)
    need = []
    for P in poses:
        d = 0.0
        for s in ("L", "R"):
            hip = P["pelvis_loc"] + P["pelvis_rot"] @ (R.hj0[s] - R.h0)
            a = P["foot_" + s][0]
            dist = (a - hip).length
            if dist > lmax:
                # 골반을 dz 내리면 닿는다
                v = a - hip
                horiz = math.hypot(v.x, v.y)
                dz = -v.z - math.sqrt(max(0.0, lmax ** 2 - horiz ** 2))
                d = max(d, dz)
        need.append(d)
    m = n - 1 if clip.loop else n          # 반복: 마지막 = 첫 프레임
    sm = []
    for i in range(n):
        acc = wsum = 0.0
        for k in range(-3, 4):
            j = (i + k) % m if clip.loop else min(n - 1, max(0, i + k))
            w = math.exp(-0.5 * (k / 1.5) ** 2)
            acc += w * need[j]
            wsum += w
        sm.append(acc / wsum)
    out = []
    for i, P in enumerate(poses):
        j = i % m if clip.loop else i
        out.append(max(sm[j] * 1.25, need[j]))
    return out


def body_delta(world, name):
    return (world[name].to_3x3().normalized() @ R.exp_rest[name].to_3x3().normalized().inverted()).to_quaternion()


def angle_diff(qa, qb):
    return math.degrees(qa.rotation_difference(qb).angle)


def ref_compare(world, clipname, frame, bones):
    s = REF[clipname]["samples"][str(frame)]["delta"]
    worst = (0.0, "")
    for b in bones:
        d = angle_diff(body_delta(world, b), Quaternion(s[b]))
        if d > worst[0]:
            worst = (d, b)
    return worst


def eval_low(objs):
    """지금 평가된 메시의 최저 높이 (땅 = 0)."""
    dg = bpy.context.evaluated_depsgraph_get()
    low = 9.0
    for nm in objs:
        o = bpy.data.objects.get(nm)
        if o is None:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ev.matrix_world
        low = min(low, min((mw @ v.co).z for v in me.vertices))
        ev.to_mesh_clear()
    return low


def make(name):
    clip = clips[name]
    n = clip.frames
    times = [i / C.FPS for i in range(n)]
    if clip.loop:
        times[-1] = clip.length            # 마지막 = 첫 프레임(주기 끝)
    poses = [clip.sample(t) for t in times]
    if clip.loop:
        poses[-1] = clip.sample(0.0) if not clip.reach_fix else poses[-1]
    reach = reach_fix(clip, poses) if clip.reach_fix else 0.0
    # 리그 · 내보내기 뼈대에 새 동작
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
        R.key_controls(f)
        world = R.bake_export(f, prev)
        m = R.measure(world)
        if getattr(clip, "mesh_check", False):
            m["mesh_low"] = eval_low(("Body_Low", "Cap_Low"))
            m["rifle_low"] = eval_low((C.RIFLE_MESH, "Bayonet"))
            if m["mesh_low"] < -0.01:
                print("LOWMESH", name, "frame", f, round(m["mesh_low"], 4))
        m["P_grip"] = (P["grip_L"], P["grip_R"])
        m["world"] = {k: v.copy() for k, v in world.items()}
        rec.append(m)
    scene.frame_start, scene.frame_end = 1, n
    for obj in (R.rig, R.exp):
        for fc in obj.animation_data.action.fcurves if hasattr(obj.animation_data.action, "fcurves") else []:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
    return clip, rec, reach


def foot_metrics(rec, clip):
    """멈춰 있어야 할 발(목표가 그대로인 구간)의 실제 이동 · 달리기는 접지 발 속도."""
    out = {}
    n = len(rec)
    for s in ("L", "R"):
        pts = [r["ball_" + s] for r in rec]
        ank = [r["ankle_" + s] for r in rec]
        if clip.view == "run":
            spec = clip.spec
            sp = []
            hz = []
            slip = []
            tr = getattr(clip, "root_travel", None)
            for i in range(n - 1):
                # 접지: 발볼 높이가 서 있을 때 높이 + 1 cm 안
                if pts[i].z < R.ball0[s].z + 0.01 and pts[i + 1].z < R.ball0[s].z + 0.01:
                    v = (pts[i + 1] - pts[i]).length * C.FPS
                    sp.append(v)
                    hz.append(pts[i].z - R.ball0[s].z)
                    if tr is not None:
                        # 출발 · 멈춤: 디딘 발은 루트가 움직이는 만큼 뒤로 가야 미끄러지지 않는다
                        root = (tr((i + 1) / C.FPS) - tr(i / C.FPS)) * C.FPS
                        slip.append(abs((pts[i + 1].y - pts[i].y) * C.FPS - root) + abs(pts[i + 1].x - pts[i].x) * C.FPS)
                        if slip[-1] > 0.15:
                            print("SLIP", clip.name, s, "frame", i + 1, "->", i + 2, "m/s %.3f" % slip[-1],
                                  "ball", tuple(round(c, 3) for c in pts[i]), tuple(round(c, 3) for c in pts[i + 1]), "root %.3f" % root)
            out[s] = {"contact_frames": len(sp), "ball_speed_mean": sum(sp) / max(1, len(sp)),
                      "ball_speed_min": min(sp) if sp else 0, "ball_speed_max": max(sp) if sp else 0,
                      "ball_height_in_contact_max": max(hz) if hz else 0}
            if tr is not None:
                out[s]["slip_vs_root_max"] = max(slip) if slip else 0.0
        else:
            # 프레임 사이 움직임이 0.5 mm 아래인 구간을 "디딘 발"로 보고, 그 구간 동안 처음 위치에서 얼마나 벗어나는가
            worst = 0.0
            start = None
            for i in range(1, n):
                still = (ank[i] - ank[i - 1]).length < 0.0005
                if still and start is None:
                    start = i - 1
                if start is not None:
                    if still:
                        worst = max(worst, (ank[i] - ank[start]).length, (pts[i] - pts[start]).length)
                    else:
                        start = None
            out[s] = {"planted_drift_max": worst, "ankle_travel_total": sum((ank[i] - ank[i - 1]).length for i in range(1, n))}
    return out


def seg_dist(p, a, b):
    ab = b - a
    u = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (a + ab * u - p).length


def summarize(clip, rec, reach):
    n = len(rec)
    hips = [r["hips"].z for r in rec]
    head = [r["head_top"].z for r in rec]
    eye = [r["eye"].z for r in rec]
    gl = [(r["wrist_L"] - r["grip_L"]).length for r in rec if r["P_grip"][0] > 0.999]
    gr = [(r["wrist_R"] - r["grip_R"]).length for r in rec if r["P_grip"][1] > 0.999]
    rifle_low = min(min(r["muzzle"].z, r["butt"].z) for r in rec)
    knees = {s: min(r["knee_" + s].z for r in rec) for s in ("L", "R")}
    first, last = rec[0]["world"], rec[-1]["world"]
    loop_diff = max(angle_diff(body_delta(first, b), body_delta(last, b)) for b in BODY + ARMS)
    loop_pos = max((first[b].translation - last[b].translation).length for b in ("Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "Rifle"))
    S = {
        "clip": clip.name, "frames": n, "seconds": round((n - 1) / C.FPS, 3), "loop": clip.loop,
        "hips_z": [round(min(hips), 3), round(max(hips), 3)],
        "head_top_max": round(max(head), 3), "eye_max": round(max(eye), 3),
        "head_vs_parapet_max": round(max(head) - C.PARAPET_TOP, 3),
        "hand_rifle_L_max": round(max(gl), 4) if gl else None, "hand_rifle_R_max": round(max(gr), 4) if gr else None,
        "rifle_lowest_point": round(rifle_low, 3), "knee_lowest": {k: round(v, 3) for k, v in knees.items()},
        "first_last_bone_deg": round(loop_diff, 2), "first_last_pos_m": round(loop_pos, 4),
        "feet": {s: {k: round(v, 4) if isinstance(v, float) else v for k, v in d.items()} for s, d in foot_metrics(rec, clip).items()},
        "reach_drop_max": round(reach, 4),
        "muzzle_forward_most_y": round(min(r["muzzle"].y for r in rec), 3),
        "rifle_head_clear_min": round(min(seg_dist(r["head_c"], r["butt"], r["muzzle"]) for r in rec), 3),
        "def_err_max": round(max(r["def_err"] for r in rec), 5),
        "grip_frames": {},
    }
    for i, s in enumerate(("L", "R")):
        ranges, cur = [], None
        for f, r in enumerate(rec, 1):
            on = r["P_grip"][i] > 0.999
            if on and cur is None:
                cur = f
            if not on and cur is not None:
                ranges.append((cur, f - 1))
                cur = None
        if cur is not None:
            ranges.append((cur, n))
        S["grip_frames"][s] = ranges
    # 이어지는 자세와의 관절 각도 차
    conn = {}
    for which, ref, w in (("start", clip.start_ref, first), ("end", clip.end_ref, last)):
        if not ref:
            continue
        if ref in REF:
            fr = REF[ref]["frames"][0]
            d, b = ref_compare(w, ref, fr, BODY)
            conn[which] = {"vs": ref + " (기존, 몸통 · 다리)", "deg": round(d, 2), "bone": b}
        elif ref in TRACK:
            other = TRACK[ref]
            d = max((angle_diff(body_delta(w, b), other[b]), b) for b in BODY + ARMS)
            conn[which] = {"vs": ref + " 첫 프레임", "deg": round(d[0], 2), "bone": d[1]}
    S["connect"] = conn
    # 이어지는 클립과의 다음 · 앞 프레임 차 (속도 이음)
    ex = {}
    for k, (label, D) in EXTRA.get(clip.name, {}).items():
        i = {"start_pose": 0, "start_next": 1, "next_prev": n - 2}[k]
        d = max((angle_diff(body_delta(rec[i]["world"], b), D[b]), b) for b in BODY + ARMS)
        ex[k] = {"vs": label, "frame": i + 1, "deg": round(d[0], 2), "bone": d[1]}
    S["connect_extra"] = ex
    if "mesh_low" in rec[0]:
        S["mesh_low_min"] = round(min(r["mesh_low"] for r in rec), 4)
        S["mesh_low_end"] = round(rec[-1]["mesh_low"], 4)
        S["rifle_low_min"] = round(min(r["rifle_low"] for r in rec), 4)
        S["rifle_low_end"] = round(rec[-1]["rifle_low"], 4)
    tr = getattr(clip, "root_travel", None)
    if tr is not None:
        S["root_travel_m"] = round(tr(clip.length), 3)
    return S


TRACK = {}       # 다른 클립과 비교할 첫 프레임 자세


def ensure_track(name):
    if name in TRACK or name not in clips:
        return
    clip = clips[name]
    P = clip_poses(clip)[0] if clip.reach_fix else clip.sample(0.0)
    R.apply(P)
    world = R.bake_export(1, {})
    TRACK[name] = {b: body_delta(world, b) for b in BODY + ARMS}


def render(clip, rec):
    out = os.path.join(C.WORK, "frames", clip.name)
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        os.remove(os.path.join(out, f))
    cam = RR.setup(scene, getattr(clip, "res", (220, 270)))
    n = len(rec)
    is_new = getattr(clip, "new", False)
    gp = bpy.data.objects["GroundPlane"]
    gp.location.z = 0.05 if is_new else 0.0          # 새 클립: 땅 윗면 = 0 (발바닥 높이)
    on_ground = clip.view == "run" or getattr(clip, "ground", False)
    if is_new:
        if clip.view == "run":
            idx = list(range(0, n - 1)) if clip.loop else list(range(n))
        else:
            step = clip.step
            idx = sorted(set([int(round(k * step * C.FPS)) for k in range(int(clip.length / step + 1e-6) + 1)] + [n - 1]))
            idx = [i for i in idx if i < n]
        views = getattr(clip, "views", ["side", "front", "qf"])
        tgt, scale = getattr(clip, "cam", None) or getattr(clip, "cam_run", None) or ((0, -0.05, 0.86), 2.05)
        if getattr(clip, "root_travel", None):
            # 출발 · 멈춤: 루트가 가는 만큼 몸을 옮겨 그린다(땅에 디딘 발이 화면에서 멈춰 보여야 정상)
            tgt = (tgt[0], tgt[1] - clip.root_travel(clip.length) * 0.5, tgt[2])
            scale = scale + clip.root_travel(clip.length) * 0.8
    elif clip.view == "run":
        idx = list(range(0, n - 1))       # 한 주기 프레임마다
        views = ["side", "front", "qf"]
        tgt, scale = (0, -0.05, 0.86), 2.05
        for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine"):
            bpy.data.objects[o].hide_render = True
        bpy.data.objects["GroundPlane"].hide_render = False
    else:
        step = 0.25 if clip.length <= 6 else 0.3
        idx = sorted(set([int(round(k * step * C.FPS)) for k in range(int(clip.length / step) + 1)] + [n - 1]))
        idx = [i for i in idx if i < n]
        views = ["side", "front", "qf"]
        stand_like = max(r["head_top"].z for r in rec) > 1.3
        tgt, scale = ((0, -0.05, 0.80), 1.95) if stand_like else ((0, -0.08, 0.58), 1.45)
    files = []
    cols = len(idx) if len(idx) <= 11 else (len(idx) + 1) // 2
    for v in views:
        if files and len(files) % cols:
            files += [("", "-")] * (cols - len(files) % cols)
        for i in idx:
            scene.frame_set(i + 1)
            if is_new and getattr(clip, "root_travel", None):
                R.rig.location = (0.0, -clip.root_travel(i / C.FPS), 0.0)
                bpy.context.view_layer.update()
            p = os.path.join(out, "%s_%03d.png" % (v, i + 1))
            if on_ground:
                RR.aim(cam, v, target=tgt, scale=scale)
                for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine") + (("Floor",) if is_new else ()):
                    bpy.data.objects[o].hide_render = True
                gp.hide_render = False
                scene.render.filepath = p
                bpy.ops.render.render(write_still=True)
            else:
                RR.shot(scene, cam, v, p, target=tgt, scale=scale)
            files.append(("%s f%d %.2fs" % (v, i + 1, i / C.FPS), p))
    R.rig.location = (0.0, 0.0, 0.0)
    sheet = os.path.join(C.PREVIEW, clip.name + ".jpg")
    tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sheet.py")
    args = [os.environ.get("UV_EXE", "uv"), "run", "--no-project", "-q", "--with", "pillow", "python", "-I", tool, sheet, str(cols)]
    # 보기마다 한 줄(또는 두 줄)씩: 시간 순서
    args += ["%s=%s" % (l, p) for l, p in files]
    with io.open(os.path.join(out, "sheet_args.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(args))
    try:
        res = subprocess.run(args, capture_output=True, text=True, timeout=300)
        print("SHEET", res.returncode, res.stdout.strip(), res.stderr.strip()[-300:])
    except Exception as e:
        print("SHEET FAILED", e)
    return sheet, args


def export(clip):
    act = R.exp.animation_data.action
    act.name = clip.name
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="DESELECT")
    R.exp.hide_set(False)
    R.exp.hide_viewport = False
    R.exp.select_set(True)
    bpy.context.view_layer.objects.active = R.exp
    scene.frame_start, scene.frame_end = 1, clip.frames
    scene.name = clip.name          # FBX 테이크 이름 = 장면 이름 → 클립 이름 = 파일명
    path = os.path.join(C.EXPORT, clip.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False)
    return path


def rifle_track(clip, rec):
    """Unity 좌표(x 오른쪽, y 위, z 앞, 루트 기준)의 소총 위치 · 방향과 손 접촉. AlliedM1 = LookRotation(forward, up)."""
    frames = []
    for f, r in enumerate(rec, 1):
        w = r["world"]
        o = w["Rifle"].translation
        mz = w["Rifle_Muzzle"].translation
        tp = w["Rifle_Top"].translation
        fwd = (mz - o - (w["Rifle"].to_3x3() @ Vector((0, 0, 0)))).normalized()
        up = (tp - o).normalized()
        frames.append({"f": f, "pos": f3(C.b2u(o)), "forward": f3(C.b2u(fwd)), "up": f3(C.b2u(up)),
                       "grip_L": round(r["P_grip"][0], 3), "grip_R": round(r["P_grip"][1], 3)})
        tr = getattr(clip, "root_travel", None)
        if tr is not None:     # 출발 · 멈춤: 이 프레임까지 루트가 앞으로 가야 할 거리(m, 발이 미끄러지지 않게)
            frames[-1]["root_forward_m"] = round(tr((f - 1) / C.FPS), 4)
    return {"clip": clip.name, "fps": C.FPS, "frames": frames,
            "note": "pos = 모신나강 원점(방아쇠 위 총강 축: 총구 +0.871 m, 개머리 끝 -0.359 m, SM_WP_Mosin 메시 원점에서 앞으로 0.044 m · 아래로 0.038 m). "
                    "forward = 총구 쪽, up = 가늠쇠 쪽. rotation = root.rotation * LookRotation(forward, up), position = root.TransformPoint(pos)."}


ALLOUT = {}
EXTRA = {}
for name in names:
    clip = clips[name]
    for ref in (clip.start_ref, clip.end_ref):
        if ref == "self":
            continue
        if ref:
            ensure_track(ref)
    for attr in ("start_pose", "start_next", "next_prev"):
        v = getattr(clip, attr, None)
        if v:
            R.apply(clip_poses(clips[v[1]])[v[2]] if len(v) == 3 else v[1]())
            w = R.bake_export(1, {})
            EXTRA.setdefault(name, {})[attr] = (v[0], {b: body_delta(w, b) for b in BODY + ARMS})
for name in names:
    clip, rec, reach = make(name)
    if clip.start_ref == "self":
        clip.start_ref = None
    S = summarize(clip, rec, reach)
    ALLOUT[name] = S
    print("SUMMARY", json.dumps(S, ensure_ascii=False))
    if do_render:
        sheet, args = render(clip, rec)
        print("SHEETARGS", os.path.join(C.WORK, "frames", clip.name, "sheet_args.txt"))
    if do_export:
        path = export(clip)
        with io.open(os.path.join(C.EXPORT, clip.name + "_rifle.json"), "w", encoding="utf-8") as fh:
            json.dump(rifle_track(clip, rec), fh, ensure_ascii=False)
        print("EXPORTED", path, os.path.getsize(path))

mpath = os.path.join(C.WORK, "metrics.json")
old = {}
if os.path.exists(mpath):
    with io.open(mpath, encoding="utf-8") as fh:
        old = json.load(fh)
old.update(ALLOUT)
with io.open(mpath, "w", encoding="utf-8") as fh:
    json.dump(old, fh, ensure_ascii=False, indent=1)

if "tpose" in argv:
    # 기준 자세(동작 없음) = 병사 모델의 바인드 자세(팔이 16도 내려간 T자). Humanoid 아바타용
    R.exp.animation_data.action = None
    for pb in R.exp.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    R.exp.select_set(True)
    bpy.context.view_layer.objects.active = R.exp
    path = os.path.join(C.EXPORT, C.PREFIX + "Rigify_TPose.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False, bake_anim=False,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False)
    print("EXPORTED", path, os.path.getsize(path))

if do_blend:
    os.makedirs(C.RIGDIR, exist_ok=True)
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)
    for o in ("PreviewCam",):
        if o in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[o])
    R.rig.animation_data.action = bpy.data.actions.get("A_KPA_CrouchRun_rig")
    path = os.path.join(C.RIGDIR, "kpa_rigify_clips.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("SAVED", path, os.path.getsize(path))
