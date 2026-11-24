# 2026-11-24 탄약수 클립 관절 · 몸 관통 · 물리 · 소품 검사 (사용자 지시: "물리법칙이나 인간의 관절구조상 절대 할 수 없는 모션은 고쳐").
# 내보낸 FBX를 다시 불러(만든 스크립트의 내부 값이 아니라 결과물을) 프레임마다 잰다.
# 실행: blender -b --factory-startup --python scripts/loader_joint_check.py -- <태그> [loader|ref|all|클립,클립] [폴더]
#   loader = A_ROK_Loader_* 7개, ref = 기존 아군 클립(이미 검수된 기준, 비교용), 폴더 기본 = export/
# 결과: work/loader/joint_check/<태그>.json (위반 목록 · 클립별 최대값)
#
# 좌표: 기준 자세 = T포즈, 캐릭터 앞 -Y, 왼쪽 +X, 위 +Z. 관절 각도는 "부모 뼈 기준의 자식 뼈 회전"을 기준 자세 축으로 잰다.
# 몸 관통: 몸 메시(Body_Low · Cap_Low, 리그 가중치 그대로)를 프레임마다 선형 스키닝으로 움직이고,
#   한 부위의 정점을 다른 부위 뼈의 기준 자세 공간으로 되돌려 그 부위의 기준 메시 단면(볼록 껍질) 안으로 들어간 깊이를 잰다.
import glob
import io
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["check", "loader"]
TAG = argv[0]
WHICH = argv[1] if len(argv) > 1 else "loader"
SRC = argv[2] if len(argv) > 2 else C.EXPORT
OUT = os.path.join(C.WORK, "loader", "joint_check")
os.makedirs(OUT, exist_ok=True)
FPS = C.FPS

# ------------------------------------------------------------------ 한계 (사람 관절 가동 범위, 도)
# 근거: AAOS 정상 가동 범위 표(Greene & Heckman 1994)와 무릎 꿇기 · 쪼그려 앉기 연구의 최대값에 여유를 둠.
# "절대 할 수 없는" 자세만 잡으려는 것이라 일반 범위보다 넉넉하다.
LIM = {
    "spine_flex": (-35.0, 90.0),      # 골반→윗가슴: 뒤로 젖힘 · 앞숙임
    "spine_side": (-40.0, 40.0),
    "spine_twist": (-50.0, 50.0),
    "neck_flex": (-70.0, 70.0),       # 윗가슴→머리 (목 전체): 위 보기(−) · 아래 보기(+)
    "neck_side": (-45.0, 45.0),
    "neck_twist": (-80.0, 80.0),      # 지시: 목 회전 ±80
    "clav_elev": (-20.0, 45.0),
    "clav_prot": (-35.0, 35.0),
    "sh_back": (-90.0, 60.0),         # 위팔이 몸통 면 뒤로 간 각
    "sh_across": (-0.72, 1.0),        # 위팔 방향의 옆 성분(바깥 +): 몸 앞을 가로질러 반대쪽으로 약 46도까지
    "hum_rot": (-95.0, 100.0),        # 위팔 축 돌림(팔을 내린 중립 기준 최단 회전, + 바깥). 팔꿈치 25도 이상 굽힌 프레임만(굽힘면이 있을 때)
    "elbow_flex": (-10.0, 155.0),     # − = 반대로 꺾임
    "forearm_ps": (-30.0, 210.0),     # 0 완전 엎침 · 90 중립 · 180 완전 뒤침 (지시 ±85의 해부학적 해석: 중립 ± 90, 손바닥 법선 추정 오차 30도 여유). 팔꿈치 25도 이상 굽힌 프레임만
    "wrist_bend": (0.0, 95.0),        # 손등 · 손바닥 · 옆 굽힘을 합친 손목 꺾임 (아래팔 축 ↔ 손목→가운뎃손가락 뿌리). 기준 자세가 이미 손바닥 쪽 22도
    "hip_flex": (-30.0, 130.0),
    "hip_abd": (-30.0, 50.0),
    "hip_rot": (-50.0, 60.0),         # 무릎 굽힘면으로 본 넓적다리 돌림(+ 바깥)
    "knee_flex": (-10.0, 165.0),      # − = 반대로 꺾임
    "tib_rot": (-40.0, 40.0),         # 발끝 방향 ↔ 무릎 앞 (정강이 돌림 + 발목 벌림)
    "ankle_flex": (-40.0, 75.0),      # 발등 쪽(−) · 발바닥 쪽(+, 발등이 땅 무릎 꿇기 약 70 — 기존 검수 클립 73)
    "ankle_roll": (-35.0, 35.0),
    "toe_flex": (-95.0, 45.0),        # 발가락 젖힘(−) · 굽힘(+)
}
PEN_TOL = 0.020        # 몸 관통 허용 깊이 (옷 · 살 눌림, 스키닝 근사) m
GROUND_TOL = 0.020     # 땅 아래로 들어간 깊이 허용
CONTACT_Z = 0.012      # 이 높이 아래 정점 = 땅에 닿음
SLIP_TOL = 0.015       # 한 번 닿아 있는 동안 같은 정점이 미끄러진 거리 허용 m
COM_TOL = 0.03         # 무게중심이 지지면 밖으로 나간 거리 허용 m
COM_TIME = 0.30        # 그 상태가 이 시간보다 길면 위반 (s)
ANG_SPEED = 1500.0     # 관절 각속도 상한 deg/s — 던지기 · 치기 같은 폭발 동작이 아닌 몸 동작의 최대값(빠른 반사 동작 약 1000~1500)을 넘으면 불가능으로 봄
POP_RATIO, POP_MIN = 2.5, 360.0   # 한 프레임만 튐: 그 프레임 각속도가 앞뒤 프레임 큰 쪽의 2.5배 넘고 360 deg/s 넘음
LIN_SPEED = {"Hips": 3.0, "Head": 3.5, "LeftHand": 7.0, "RightHand": 7.0, "LeftFoot": 5.0, "RightFoot": 5.0}
PROP_GAP = 0.030       # 소품을 쥔 손 정점이 소품 표면에서 떨어져도 되는 거리
PROP_PEN = 0.020       # 손 · 몸 정점이 소품 안으로 들어가도 되는 깊이
ROCKET_R = 0.0445
ROCKET_LEN = 0.60
RIFLE_R = 0.022
TUBE_R = 0.040        # 총몸 · 총열덮개 반지름 근사(개머리판은 더 굵지만 보수적으로 얇게)
RIFLE_BUTT, RIFLE_MUZ = -0.381, 0.724

BODY_MASS = 60.0
RIFLE_KG, ROCKET_KG = 4.3, 1.5

# ------------------------------------------------------------------ 몸 메시 정점 (기준 자세, 가중치)
bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
exp0 = bpy.data.objects["Armature"]
EXP_BONES = [b.name for b in exp0.data.bones]
DEF2EXP = {v: k for k, v in C.EXP2DEF.items()}


def def_to_exp(g):
    if g in DEF2EXP:
        return DEF2EXP[g]
    base = g.rsplit(".", 1)[0] if g.count(".") >= 2 or g.endswith(".001") else g
    if base in DEF2EXP:
        return DEF2EXP[base]
    if g.startswith("DEF-spine.005"):
        return "Neck"
    if g.startswith("DEF-upper_arm") and g in ("DEF-upper_arm.L.001", "DEF-upper_arm.R.001"):
        return DEF2EXP[g[:-4]]
    for k in ("forearm", "thigh", "shin", "upper_arm"):
        for s in ("L", "R"):
            if g == "DEF-%s.%s.001" % (k, s):
                return DEF2EXP["DEF-%s.%s" % (k, s)]
    return None


samples = []      # (rest pos, [(exp bone, w)])
unmapped = {}
for nm, step in (("Body_Low", 5), ("Cap_Low", 3)):
    o = bpy.data.objects[nm]
    names = {g.index: g.name for g in o.vertex_groups}
    mw = o.matrix_world
    for i, v in enumerate(o.data.vertices):
        if i % step:
            continue
        ws = {}
        for g in v.groups:
            if g.weight < 0.01:
                continue
            e = def_to_exp(names[g.group])
            if e is None:
                unmapped[names[g.group]] = unmapped.get(names[g.group], 0) + 1
                continue
            ws[e] = ws.get(e, 0.0) + g.weight
        if not ws:
            continue
        tot = sum(ws.values())
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]
        samples.append((mw @ v.co, [(b, w / tot) for b, w in top]))
print("CHECK samples", len(samples), "unmapped groups", {k: v for k, v in unmapped.items() if not k.startswith("ORG")})
BIDX = {b: i for i, b in enumerate(EXP_BONES)}
NS = len(samples)
P0 = np.array([[*s[0], 1.0] for s in samples])                  # (N,4)
WI = np.zeros((NS, 4), dtype=int)
WW = np.zeros((NS, 4))
for i, (_, ws) in enumerate(samples):
    for k, (b, w) in enumerate(ws):
        WI[i, k] = BIDX[b]
        WW[i, k] = w
DOM = np.array([BIDX[s[1][0][0]] for s in samples])           # 가장 큰 가중치 뼈

REST = {b.name: b.matrix_local.copy() for b in exp0.data.bones}   # 기준 블렌드의 기준 자세(월드)

# 부위 묶음
def group(*names):
    return [BIDX[n] for n in names]

FINGERS = lambda S: [b for b in EXP_BONES if b.startswith(S) and any(k in b for k in ("Thumb", "Index", "Middle", "Ring", "Little"))]
PART = {
    "torso": group("Hips", "Spine", "Chest", "UpperChest"),
    "head": group("Head"),
    "handL": group("LeftHand", *FINGERS("Left")), "handR": group("RightHand", *FINGERS("Right")),
    "foreL": group("LeftLowerArm"), "foreR": group("RightLowerArm"),
    "upperL": group("LeftUpperArm"), "upperR": group("RightUpperArm"),
    "thighL": group("LeftUpperLeg"), "thighR": group("RightUpperLeg"),
    "shinL": group("LeftLowerLeg"), "shinR": group("RightLowerLeg"),
    "footL": group("LeftFoot", "LeftToes"), "footR": group("RightFoot", "RightToes"),
}
MASK = {k: np.isin(DOM, v) for k, v in PART.items()}
# 부위의 기준 자세 단면 껍질: (기준 뼈, 축, 원점, 칸 크기, {칸: 2D 껍질})
HULL_PART = {   # 부위: [(기준 뼈, 축, 그 뼈가 주된 정점)] — 몸통 · 머리는 휘므로 뼈마다 따로
    "torso": [("Hips", (0, 0, 1), ("Hips",)), ("Spine", (0, 0, 1), ("Spine",)), ("Chest", (0, 0, 1), ("Chest",)),
              ("UpperChest", (0, 0, 1), ("UpperChest",))],
    "head": [("Head", (0, 0, 1), ("Head",))],   # 목(승모근 둘레)은 위팔이 원래 닿는 곳이라 빼고 머리만
}
for _s, _S in (("L", "Left"), ("R", "Right")):
    HULL_PART["thigh" + _s] = [(_S + "UpperLeg", (0, 0, 1), (_S + "UpperLeg",))]
    HULL_PART["shin" + _s] = [(_S + "LowerLeg", (0, 0, 1), (_S + "LowerLeg",))]
    HULL_PART["upper" + _s] = [(_S + "UpperArm", (1, 0, 0), (_S + "UpperArm",))]
    HULL_PART["fore" + _s] = [(_S + "LowerArm", (1, 0, 0), (_S + "LowerArm",))]


def hull2d(pts):
    pts = sorted(set((round(p[0], 4), round(p[1], 4)) for p in pts))
    if len(pts) < 3:
        return None
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and cross(up[-2], up[-1], p) <= 0:
            up.pop()
        up.append(p)
    return np.array(lo[:-1] + up[:-1])


def basis(axis):
    a = np.array(axis, float)
    u = np.array((0, 1, 0), float) if abs(a[1]) < 0.9 else np.array((1, 0, 0), float)
    u = u - a * u.dot(a)
    u /= np.linalg.norm(u)
    return a, u, np.cross(a, u)


BIN = 0.02
NANG = 36
HULLS = {}
for part, subs in HULL_PART.items():
    lst = []
    for bone, axis, members in subs:
        a_, u, w = basis(axis)
        pts = P0[np.isin(DOM, [BIDX[m] for m in members]), :3]
        t = pts @ a_
        prof = {}
        for kk in np.unique(np.floor(t / BIN).astype(int)):
            sel = pts[np.floor(t / BIN).astype(int) == kk]
            if len(sel) < 6:
                continue
            q2 = np.c_[sel @ u, sel @ w]
            c = q2.mean(axis=0)
            d = q2 - c
            ang = np.floor((np.arctan2(d[:, 1], d[:, 0]) + math.pi) / (2 * math.pi) * NANG).astype(int) % NANG
            r = np.full(NANG, np.nan)
            for j in range(NANG):
                m = ang == j
                if m.any():
                    r[j] = np.hypot(d[m, 0], d[m, 1]).max()
            if np.isnan(r).all():
                continue
            idx = np.arange(NANG)
            ok = ~np.isnan(r)
            r = np.interp(idx, idx[ok], r[ok], period=NANG)
            prof[int(kk)] = (c, r)
        lst.append((bone, a_, u, w, prof))
    HULLS[part] = lst


def depth_in_hull(H, q):
    """q (M,2) 점들이 볼록 껍질 H 안으로 들어간 깊이 (밖이면 음수). 지지면(무게중심) 계산용."""
    n = len(H)
    d = np.full(len(q), np.inf)
    for i in range(n):
        a, b = H[i], H[(i + 1) % n]
        e = b - a
        nrm = np.array((-e[1], e[0])) / max(1e-9, np.hypot(*e))     # 반시계 껍질 → 안쪽 법선
        d = np.minimum(d, (q - a) @ nrm)
    return d


def depth_in_prof(prof, q2):
    """별 모양 단면 윤곽 안으로 들어간 깊이 (밖이면 음수)."""
    c, r = prof
    d = q2 - c
    th = (np.arctan2(d[:, 1], d[:, 0]) + math.pi) / (2 * math.pi) * NANG - 0.5
    j0 = np.floor(th).astype(int)
    fr = th - j0
    rs = r[j0 % NANG] * (1 - fr) + r[(j0 + 1) % NANG] * fr
    return rs - np.hypot(d[:, 0], d[:, 1])


def penetration(part, pts_world, W, rest):
    """pts_world (M,3) 정점을 part 뼈의 기준 공간으로 되돌려 그 부위 단면 안 깊이. 최대 깊이와 그 정점 번호."""
    best, bi = -1.0, -1
    for bone, a, u, w, prof in HULLS[part]:
        inv = np.array(rest[bone]) @ np.linalg.inv(np.array(W[bone]))   # 현재 → 기준 자세
        q = (np.c_[pts_world, np.ones(len(pts_world))] @ inv.T)[:, :3]
        k = np.floor((q @ a) / BIN).astype(int)
        for kk in np.unique(k):
            P = prof.get(int(kk))
            if P is None:
                continue
            sel = np.where(k == kk)[0]
            dd = depth_in_prof(P, np.c_[q[sel] @ u, q[sel] @ w])
            j = int(np.argmax(dd))
            if dd[j] > best:
                best, bi = float(dd[j]), int(sel[j])
    return best, bi


# 관통을 볼 짝: (들어가는 부위, 받는 부위)
PEN_PAIRS = [(h, b) for h in ("handL", "handR", "foreL", "foreR") for b in ("torso", "head", "thighL", "thighR", "shinL", "shinR")]
PEN_PAIRS += [("handL", "upperR"), ("handR", "upperL"), ("foreL", "foreR"), ("foreR", "foreL"), ("handL", "foreR"), ("handR", "foreL"),
              ("thighL", "thighR"), ("shinL", "shinR"), ("shinL", "thighR"), ("shinR", "thighL"), ("footL", "thighR"),
              ("footR", "thighL"), ("footL", "shinR"), ("footR", "shinL"), ("footL", "torso"), ("footR", "torso"),
              ("head", "upperL"), ("head", "upperR")]
# 위팔 → 몸통은 뺐다: 어깨 · 겨드랑이 둘레 단면이 정확하지 않아 렌더로 관통이 없는 프레임에서도 5~20 cm로 나옴(오탐)
# 위팔→몸통: 겨드랑이는 원래 붙어 있으므로 어깨 관절에서 10 cm 넘게 떨어진 위팔 정점만
SH_EXCL = 0.10

# ------------------------------------------------------------------ 기준 방향 · 축 (기준 자세 월드)
def rest_dir(b, child=None):
    if child:
        return (REST[child].translation - REST[b].translation).normalized()
    return (REST[b].to_3x3() @ Vector((0, 1, 0))).normalized()


SIDES = (("L", "Left", 1.0), ("R", "Right", -1.0))
D = {}
for s, S, sg in SIDES:
    D["up" + s] = rest_dir(S + "UpperArm", S + "LowerArm")
    D["fore" + s] = rest_dir(S + "LowerArm", S + "Hand")
    D["hand" + s] = rest_dir(S + "Hand", S + "MiddleProximal")
    D["clav" + s] = rest_dir(S + "Shoulder", S + "UpperArm")
    D["thigh" + s] = rest_dir(S + "UpperLeg", S + "LowerLeg")
    D["shin" + s] = rest_dir(S + "LowerLeg", S + "Foot")
    D["foot" + s] = rest_dir(S + "Foot", S + "Toes")
    D["toe" + s] = rest_dir(S + "Toes")
    knuck = (REST[S + "IndexProximal"].translation - REST[S + "LittleProximal"].translation)
    n = D["hand" + s].cross(knuck).normalized()
    if n.z > 0:
        n = -n                         # T포즈에서 손바닥은 아래
    D["palm" + s] = n
    D["elbow_axis" + s] = D["up" + s].cross(Vector((0, -1, 0))).normalized()   # 이 축으로 + 돌면 아래팔이 앞으로 = 굽힘
print("CHECK palm", {s: tuple(round(c, 2) for c in D["palm" + s]) for s, _, _ in SIDES},
      "elbow axis", {s: tuple(round(c, 2) for c in D["elbow_axis" + s]) for s, _, _ in SIDES})


def twist(q, axis):
    """q를 axis 둘레 비틀림 · 나머지로 나눈 비틀림 각(도, 오른손 법칙)."""
    v = Vector((q.x, q.y, q.z))
    p = axis * v.dot(axis)
    t = Quaternion((q.w, p.x, p.y, p.z))
    if t.magnitude < 1e-9:
        return 0.0
    t.normalize()
    ang = 2 * math.degrees(math.atan2(Vector((t.x, t.y, t.z)).dot(axis), t.w))
    return (ang + 180.0) % 360.0 - 180.0


def sang(a, b, axis):
    """a → b 의 axis 둘레 부호 있는 각 (a, b를 axis에 수직인 면에 투영)."""
    a = a - axis * a.dot(axis)
    b = b - axis * b.dot(axis)
    if a.length < 1e-6 or b.length < 1e-6:
        return 0.0
    return math.degrees(math.atan2(axis.dot(a.cross(b)), a.dot(b)))


def elev(v, axis):
    return math.degrees(math.asin(max(-1.0, min(1.0, v.dot(axis)))))


# 리그의 실제 경첩 축 (Rigify IK: ORG 뼈의 로컬 X = 굽힘 축). 무릎 축은 기준 자세에서 정면 X보다 약 40도 돌아 있다.
# 내보낸 LowerArm · LowerLeg 는 IK 틀(경첩 포함)을 그대로 따르고, UpperArm · UpperLeg 는 비틀림이 빠져 있다.
# 그래서 관절 각도는 뼈 로컬 축이 아니라 관절 위치 · 경첩 축 · 손바닥 법선으로 해부학적으로 정의한다.
_rig = bpy.data.objects["ROK_Rig"]
H = {}
for _s in ("L", "R"):
    for _k, _n in (("elbow", "ORG-forearm."), ("knee", "ORG-shin."), ("footx", "ORG-foot.")):
        H[_k + _s] = (_rig.matrix_world.to_3x3() @ _rig.data.bones[_n + _s].matrix_local.to_3x3().col[0]).normalized()


def _unit(v):
    return v.normalized() if v.length > 1e-9 else v


def joint_angles(Dq, W, off=None):
    """Dq: 뼈 → 기준 자세 대비 월드 회전, W: 뼈 → 월드 행렬. 관절 이름 → 값(도)."""
    rel = lambda p, c: Dq[p].inverted() @ Dq[c]
    pos = lambda n: W[n].translation
    J = {}
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    q = rel("Hips", "UpperChest")
    d = q @ Z
    J["spine_flex"] = math.degrees(math.atan2(-d.y, d.z))
    J["spine_side"] = math.degrees(math.atan2(d.x, d.z))
    J["spine_twist"] = twist(q, Z)
    q = rel("UpperChest", "Head")
    d = q @ Z
    J["neck_flex"] = math.degrees(math.atan2(-d.y, d.z))
    J["neck_side"] = math.degrees(math.atan2(d.x, d.z))
    J["neck_twist"] = twist(q, Z)
    chest = Dq["UpperChest"].inverted()
    pel = Dq["Hips"].inverted()
    for s, S, sg in SIDES:
        side = Vector((sg, 0, 0))
        q = rel("UpperChest", S + "Shoulder")
        c0, c1 = D["clav" + s], q @ D["clav" + s]
        J["clav_elev_" + s] = elev(c1, Z) - elev(c0, Z)
        J["clav_prot_" + s] = elev(c1, -Y) - elev(c0, -Y)
        # 어깨 · 팔꿈치 · 아래팔 · 손목
        u = _unit(pos(S + "LowerArm") - pos(S + "UpperArm"))
        f = _unit(pos(S + "Hand") - pos(S + "LowerArm"))
        hd = _unit(pos(S + "MiddleProximal") - pos(S + "Hand"))
        h = _unit(Dq[S + "LowerArm"] @ H["elbow" + s])
        ur = chest @ u
        J["sh_back_" + s] = elev(ur, Y)
        J["sh_across_" + s] = ur.dot(side)
        crease = _unit(h.cross(u))                       # 팔꿈치 안쪽(굽는 쪽)
        cr = chest @ crease
        Q = Vector((0, 0, -1)).rotation_difference(ur)   # 팔을 내린 중립(안쪽 앞)에서 가장 짧게 돌린 기준
        J["hum_rot_" + s] = -sg * sang(Q @ Vector((0, -1, 0)), cr, ur)    # + = 바깥 돌림
        J["_arm_elev_" + s] = math.degrees(math.acos(max(-1.0, min(1.0, -ur.z))))
        J["elbow_flex_" + s] = sang(u, f, h)
        J["elbow_plane_" + s] = elev(u, h)                # 진단: IK면 0
        c = _unit(h.cross(f))
        n = Dq[S + "Hand"] @ D["palm" + s]
        nn = _unit(n - f * n.dot(f))
        n_neu = _unit(f.cross(c)) * sg
        J["forearm_ps_" + s] = math.degrees(math.atan2(nn.dot(n_neu), nn.dot(-c)))   # 0 = 완전 엎침, 90 = 중립, 180 = 완전 뒤침
        tf = f.cross(nn)
        J["wrist_bend_" + s] = math.degrees(math.acos(max(-1.0, min(1.0, hd.dot(f)))))
        J["wrist_flex_" + s] = math.degrees(math.atan2(hd.dot(nn), hd.dot(f)))     # 진단: + 손바닥 쪽
        J["wrist_dev_" + s] = math.degrees(math.atan2(hd.dot(tf), hd.dot(f)))
        # 엉덩이 · 무릎 · 발목 · 발가락
        t = _unit(pos(S + "LowerLeg") - pos(S + "UpperLeg"))
        sh = _unit(pos(S + "Foot") - pos(S + "LowerLeg"))
        fd = _unit(pos(S + "Toes") - pos(S + "Foot"))
        hk = _unit(Dq[S + "LowerLeg"] @ H["knee" + s])
        tr = pel @ t
        J["hip_flex_" + s] = math.degrees(math.atan2(-tr.y, -tr.z))
        J["hip_abd_" + s] = elev(tr, side) - elev(D["thigh" + s], side)
        ant = _unit(t.cross(hk))                          # 무릎 앞(굽힘 반대쪽)
        Q = Vector((0, 0, -1)).rotation_difference(tr)
        J["hip_rot_" + s] = -sg * sang(Q @ Vector((0, -1, 0)), pel @ ant, tr)    # + = 바깥 돌림
        J["knee_flex_" + s] = sang(t, sh, hk)
        J["knee_plane_" + s] = elev(t, hk)
        mesh_front = Dq[S + "UpperLeg"] @ Vector((0, -1, 0))
        J["knee_mesh_twist_" + s] = sang(mesh_front, ant, t)                      # 진단: 무릎 메시 앞 ↔ 실제 굽힘면
        a_s = _unit(sh.cross(hk))
        fx = _unit(Dq[S + "Foot"] @ H["footx" + s])
        J["ankle_flex_" + s] = sang(sh, fd, fx)                                   # + 발바닥 쪽
        J["tib_rot_" + s] = -sg * sang(hk, fx, sh)                                # 발 옆 축 ↔ 무릎 경첩 축 (정강이 축 둘레, + 발끝 바깥)
        J["ankle_roll_" + s] = sg * elev(fx, sh)                                  # 발 옆 축이 정강이 축 쪽으로 기운 각 = 안쪽 · 바깥 번짐
        toe = Dq[S + "Toes"] @ D["toe" + s]
        J["toe_flex_" + s] = sang(fd, toe, fx)
    if off:
        for s, S, sg in SIDES:
            if J["elbow_flex_" + s] - off["elbow_flex_" + s] < 25.0 or J.get("_arm_elev_" + s, 0) > 140.0:
                J["hum_rot_" + s] = None
                J["forearm_ps_" + s] = None
        for k in list(J):
            for base in OFF_KEYS:
                if k.startswith(base) and J[k] is not None:
                    J[k] -= off[k]
        for s, S, sg in SIDES:
            if J["forearm_ps_" + s] is not None:      # −150 아래는 180 너머(지나친 뒤침)로 읽음
                J["forearm_ps_" + s] = (J["forearm_ps_" + s] + 150.0) % 360.0 - 150.0
    return J


OFF_KEYS = ("elbow_flex", "knee_flex", "ankle_flex", "ankle_roll", "toe_flex", "forearm_ps")
# forearm_ps: 기준 자세(T포즈 손바닥 아래, 리그 팔꿈치 안쪽이 위 = 완전 엎침 0)에서 30~45도로 나오는 것은
# 손바닥 법선을 손가락 뿌리 선으로 추정한 기울기라 빼서 맞춘다(11-24 기존 클립 검사에서 보정)   # 기준 자세 값을 0으로
_I = Quaternion()
OFF = joint_angles({n: _I for n in EXP_BONES}, REST)
print("CHECK rest offsets", {k: round(v, 1) for k, v in OFF.items() if k.startswith(OFF_KEYS) or k.startswith(("hip_rot", "tib_rot", "forearm_ps", "wrist", "hum_rot", "knee_mesh"))})


def lim_of(name):
    for k in sorted(LIM, key=len, reverse=True):
        if name == k or name.startswith(k + "_"):
            return LIM[k]
    return None


# ------------------------------------------------------------------ 무게중심 (de Leva 1996 남성 비율)
SEG = [  # (질량 비, 시작 뼈, 끝 뼈 또는 None, 시작에서 끝 쪽 비율)
    (0.0694, "Head", None, 0.5), (0.1117, "Hips", "Spine", 0.5), (0.1633, "Spine", "UpperChest", 0.5), (0.1596, "UpperChest", "Neck", 0.5),
]
for s, S, sg in SIDES:
    SEG += [(0.0271, S + "UpperArm", S + "LowerArm", 0.577), (0.0162, S + "LowerArm", S + "Hand", 0.457),
            (0.0061, S + "Hand", S + "MiddleProximal", 0.9), (0.1416, S + "UpperLeg", S + "LowerLeg", 0.41),
            (0.0433, S + "LowerLeg", S + "Foot", 0.446), (0.0137, S + "Foot", S + "Toes", 0.44)]


def hull_signed_dist(pts, p):
    """2D 볼록 껍질 밖으로 나간 거리(+ 밖, − 안)."""
    H = hull2d(pts)
    if H is None:
        if not pts:
            return 9.0
        return min(math.hypot(p[0] - a[0], p[1] - a[1]) for a in pts) if len(pts) else 9.0
    return -float(depth_in_hull(H, np.array([p]))[0])


# ------------------------------------------------------------------ 소품
def u2b(v):
    return Vector((-v[0], -v[2], v[1]))


def cyl_depth(P, a, b, r):
    """점들 P가 원통(a→b, 반지름 r, 납작한 끝) 안으로 들어간 깊이 (밖이면 음수 = 표면까지 거리)."""
    a, b = np.array(a), np.array(b)
    ab = b - a
    L = np.linalg.norm(ab)
    u = ab / max(1e-12, L)
    t = (P - a) @ u
    rp = np.linalg.norm(P - a - t[:, None] * u, axis=1)
    inside = np.minimum(np.minimum(r - rp, t), L - t)
    out_r = np.maximum(rp - r, 0.0)
    out_t = np.maximum(np.maximum(-t, t - L), 0.0)
    outside = -np.hypot(out_r, out_t)
    return np.where((rp <= r) & (t >= 0) & (t <= L), inside, outside)


def seg_dist(P, a, b):
    """점들 P (M,3)에서 선분 a-b 까지 거리."""
    a, b = np.array(a), np.array(b)
    ab = b - a
    t = np.clip(((P - a) @ ab) / max(1e-12, ab @ ab), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)


# ------------------------------------------------------------------ 클립 하나 검사
def check_clip(path):
    name = os.path.splitext(os.path.basename(path))[0]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    act = arm.animation_data.action
    f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
    sc = bpy.context.scene
    mw = arm.matrix_world
    rest = {n: m.copy() for n, m in REST.items()}   # FBX만 불러오면 첫 프레임이 기준 자세가 되므로 기준 블렌드의 기준 자세를 쓴다(뼈 축은 같음 확인)
    rest_err = 0.0
    restq = {n: m.to_3x3().normalized() for n, m in rest.items()}
    jpath = lambda suf: os.path.join(os.path.dirname(path), name + suf)
    prop = json.load(io.open(jpath("_prop.json"), encoding="utf-8")) if os.path.exists(jpath("_prop.json")) else None
    rifle = json.load(io.open(jpath("_rifle.json"), encoding="utf-8")) if os.path.exists(jpath("_rifle.json")) else None
    V = []        # 위반
    series = {}   # 값 기록 (최대 · 최소)
    prevJ = prevW = None
    prev_rel = None
    prev_speed = {}
    contact_prev = None
    slip = np.zeros(NS)
    com_out_run = 0
    com_out_frames = []
    frames = list(range(f0, f1 + 1))

    def add(kind, part, frame, value, limit, note=""):
        V.append({"kind": kind, "part": part, "frame": frame, "value": round(float(value), 3), "limit": limit, "note": note})

    def rec(key, v, f):
        a = series.setdefault(key, [v, f, v, f])
        if v < a[0]:
            a[0], a[1] = v, f
        if v > a[2]:
            a[2], a[3] = v, f

    rel_names = [("Hips", "Spine"), ("Spine", "Chest"), ("Chest", "UpperChest"), ("UpperChest", "Neck"), ("Neck", "Head")]
    for s, S, sg in SIDES:
        rel_names += [("UpperChest", S + "Shoulder"), (S + "Shoulder", S + "UpperArm"), (S + "UpperArm", S + "LowerArm"),
                      (S + "LowerArm", S + "Hand"), ("Hips", S + "UpperLeg"), (S + "UpperLeg", S + "LowerLeg"),
                      (S + "LowerLeg", S + "Foot"), (S + "Foot", S + "Toes")]
    for fi, fbx_f in enumerate(frames):
        sc.frame_set(fbx_f)
        f = fi + 1                        # 클립 프레임 번호 (_prop.json · _rifle.json 의 f)
        if os.environ.get("JC_FRAMES") and str(f) not in os.environ["JC_FRAMES"].split(","):
            continue                      # 지정한 프레임만 (자세 하나 확인용)
        W = {pb.name: mw @ pb.matrix for pb in arm.pose.bones}
        Dq = {n: (W[n].to_3x3().normalized() @ restq[n].inverted()).to_quaternion() for n in W}
        # 1) 관절 각도
        J = joint_angles(Dq, W, OFF)
        for k, v in J.items():
            if v is None or k.startswith("_"):
                continue
            rec(k, v, f)
            lo_hi = lim_of(k)
            if lo_hi and not (lo_hi[0] <= v <= lo_hi[1]):
                add("joint", k, f, v, lo_hi)
        # 2) 스키닝 정점
        Mb = np.zeros((len(EXP_BONES), 4, 4))
        for n, i in BIDX.items():
            Mb[i] = np.array(W[n] @ rest[n].inverted())
        Pw = np.zeros((NS, 3))
        for k in range(4):
            Pw += WW[:, k:k + 1] * np.einsum("nij,nj->ni", Mb[WI[:, k]], P0)[:, :3]
        # 3) 몸 관통
        for a_part, b_part in PEN_PAIRS:
            sel = np.where(MASK[a_part])[0]
            if a_part.startswith("upper") and b_part == "torso":
                shp = np.array(W[("Left" if a_part.endswith("L") else "Right") + "UpperArm"].translation)
                sel = sel[np.linalg.norm(Pw[sel] - shp, axis=1) > SH_EXCL]
            if not len(sel):
                continue
            dpt, bi = penetration(b_part, Pw[sel], W, rest)
            rec("pen_%s>%s" % (a_part, b_part), dpt, f)
            if dpt > PEN_TOL:
                note = ""
                if a_part.startswith("fore") and bi >= 0:   # 가장 깊은 정점이 팔꿈치에서 얼마나 떨어졌나 (팔꿈치가 옆구리 · 탄입대에 닿는 것과 구분)
                    el = np.array(W[("Left" if a_part.endswith("L") else "Right") + "LowerArm"].translation)
                    note = "팔꿈치에서 %.2f m" % float(np.linalg.norm(Pw[sel[bi]] - el))
                add("penetration", "%s>%s" % (a_part, b_part), f, dpt, PEN_TOL, note)
        # 4) 땅
        zmin = float(Pw[:, 2].min())
        rec("ground_min_z", zmin, f)
        if zmin < -GROUND_TOL:
            reg = [k for k, m in MASK.items() if m[int(np.argmin(Pw[:, 2]))]]
            add("ground", ",".join(reg) or "?", f, zmin, -GROUND_TOL)
        # 5) 닿은 정점 미끄러짐 · 지지면 · 무게중심
        contact = Pw[:, 2] < CONTACT_Z
        if contact_prev is not None:
            both = contact & contact_prev
            dxy = np.linalg.norm(Pw[:, :2] - prevW[:, :2], axis=1)
            slip = np.where(both, slip + dxy, 0.0)
            if both.any():
                j = int(np.argmax(np.where(both, slip, -1)))
                rec("slip_max", float(slip[j]), f)
                if slip[j] > SLIP_TOL:
                    reg = [k for k, m in MASK.items() if m[j]]
                    add("slip", ",".join(reg) or "?", f, slip[j], SLIP_TOL)
        contact_prev, prevW = contact, Pw.copy()
        com = Vector((0, 0, 0))
        mass = 0.0
        for m_, a_, b_, r_ in SEG:
            p = W[a_].translation if b_ is None else W[a_].translation.lerp(W[b_].translation, r_)
            if b_ is None:
                p = W[a_].translation + (W[a_].to_3x3().normalized() @ Vector((0, 1, 0))) * 0.09
            com += p * m_ * BODY_MASS
            mass += m_ * BODY_MASS
        if rifle:
            rf = rifle["frames"][min(fi, len(rifle["frames"]) - 1)]
            com += u2b(rf["pos"]) * RIFLE_KG
            mass += RIFLE_KG
        if prop:
            pf = prop["frames"][min(fi, len(prop["frames"]) - 1)]
            if pf["state"] in ("hands", "hand_R", "hand_L"):
                com += (u2b(pf["pos"]) + u2b(pf["forward"]) * 0.3) * ROCKET_KG
                mass += ROCKET_KG
        com /= mass
        cpts = [tuple(p[:2]) for p in Pw[contact]]
        out = hull_signed_dist(cpts, (com.x, com.y)) if cpts else 9.0
        rec("com_outside", out, f)
        if out > COM_TOL:
            com_out_run += 1
            com_out_frames.append(f)
            if com_out_run == int(COM_TIME * FPS) + 1:
                add("balance", "com", f, out, COM_TOL, "무게중심이 지지면 밖 %.2f초 넘음 (%d프레임부터)" % (COM_TIME, f - com_out_run + 1))
        else:
            com_out_run = 0
        # 6) 속도 · 튐
        rel_now = {pc: Dq[pc[0]].inverted() @ Dq[pc[1]] for pc in rel_names}
        rel_now[("World", "Hips")] = Dq["Hips"]
        if prev_rel is not None:
            for pc, q in rel_now.items():
                _a = prev_rel[pc].rotation_difference(q).angle
                w = math.degrees(min(_a, 2 * math.pi - _a)) * FPS
                key = "%s>%s" % pc
                rec("angvel_" + key, w, f)
                if w > ANG_SPEED:
                    add("ang_speed", key, f, w, ANG_SPEED)
                prev_speed.setdefault(key, []).append((f, w))
            for b, lim in LIN_SPEED.items():
                v = (W[b].translation - prevJ[b]).length * FPS
                rec("linvel_" + b, v, f)
                if v > lim:
                    add("lin_speed", b, f, v, lim)
        prev_rel = rel_now
        prevJ = {b: W[b].translation.copy() for b in LIN_SPEED}
        # 7) 소품
        hands = {"L": np.where(MASK["handL"])[0], "R": np.where(MASK["handR"])[0]}
        body_not_hands = np.where(~(MASK["handL"] | MASK["handR"]))[0]
        if prop:
            pf = prop["frames"][min(fi, len(prop["frames"]) - 1)]
            st = pf["state"]
            tail = u2b(pf["pos"])
            nose = u2b(pf["forward"]).normalized()
            a, b = np.array(tail), np.array(tail + nose * ROCKET_LEN)
            if st != "hidden":
                for s in ("L", "R"):
                    d = -cyl_depth(Pw[hands[s]], a, b, ROCKET_R)
                    gap = float(d.min())
                    rec("rocket_gap_" + s, gap, f)
                    want = st == "hands" or st == "hand_" + s or (st == "tube" and pf["grip_" + s] > 0.5)
                    if want and gap > PROP_GAP:
                        add("rocket_float", "hand_" + s, f, gap, PROP_GAP, "상태 %s인데 손이 로켓에서 떨어짐" % st)
                    if -gap > PROP_PEN:
                        add("rocket_pen", "hand_" + s, f, -gap, PROP_PEN, "손이 로켓을 뚫음")
                d = -cyl_depth(Pw[body_not_hands], a, b, ROCKET_R)
                j = int(np.argmin(d))
                rec("rocket_body", float(-d[j]), f)
                if -d[j] > PROP_PEN:
                    reg = [k for k, m in MASK.items() if m[body_not_hands[j]]]
                    add("rocket_pen", "body:" + ",".join(reg), f, -d[j], PROP_PEN, "로켓이 몸을 뚫음")
                zlow = min(tail.z, (tail + nose * ROCKET_LEN).z) - (ROCKET_R * math.sqrt(max(0, 1 - nose.z ** 2)))
                rec("rocket_low_z", zlow, f)
                if zlow < -GROUND_TOL:
                    add("rocket_ground", "rocket", f, zlow, -GROUND_TOL)
                if st == "pile":
                    if abs(tail.z) > 0.01 or nose.z < 0.9:
                        add("rocket_unsupported", "pile", f, tail.z, 0.01, "세워 둔 로켓이 바닥에 서 있지 않음")
                if st == "tube" and pf.get("insert", 1) < 0.5 and pf["grip_L"] < 0.5 and pf["grip_R"] < 0.5:
                    add("rocket_unsupported", "tube", f, pf.get("insert", 0), 0.5, "반도 안 들어갔는데 손을 놓음")
                if st in ("hands", "hand_R", "hand_L"):
                    gl = series.get("rocket_gap_L", [9])[0]
        if prop and "gunner" in prop:
            # 사수의 발사관 (1인칭 모델 자리 · 축, 지름 약 0.08 m · 길이 1.2 m로 근사): 손을 빼고 몸이 들어가면 관통
            g = prop["gunner"]
            ra = u2b(g["rear_mouth"])
            ax = u2b(g["tube_axis"]).normalized()
            d = -cyl_depth(Pw[body_not_hands], np.array(ra), np.array(ra + ax * 1.2), TUBE_R)
            j = int(np.argmin(d))
            rec("tube_body", float(-d[j]), f)
            if -d[j] > PEN_TOL:
                reg = [k for k, m in MASK.items() if m[body_not_hands[j]]]
                add("tube_pen", "body:" + ",".join(reg), f, -d[j], PEN_TOL, "몸이 사수의 발사관을 뚫음")
        if rifle:
            rf = rifle["frames"][min(fi, len(rifle["frames"]) - 1)]
            o = u2b(rf["pos"])
            fw = u2b(rf["forward"]).normalized()
            a, b = np.array(o + fw * RIFLE_BUTT), np.array(o + fw * RIFLE_MUZ)
            gaps = {}
            for s in ("L", "R"):
                gaps[s] = float((seg_dist(Pw[hands[s]], a, b) - RIFLE_R).min())
                rec("rifle_gap_" + s, gaps[s], f)
                if rf["grip_" + s] >= 0.99 and gaps[s] > PROP_GAP + 0.02:
                    add("rifle_float", "hand_" + s, f, gaps[s], PROP_GAP + 0.02, "손잡이를 쥔 상태인데 손이 소총에서 떨어짐")
            # 등에 메었나: 윗가슴 뼈 기준 소총 자리가 몸통 뒤 가까이
            chest = W["UpperChest"]
            mid = (Vector(a) + Vector(b)) * 0.5
            local = chest.inverted() @ mid
            d_back = float(seg_dist(Pw[MASK["torso"]], a, b).min())
            rec("rifle_torso_gap", d_back, f)
            held = min(gaps.values()) <= PROP_GAP + 0.02
            on_back = d_back < 0.06
            if not held and not on_back:
                add("rifle_unsupported", "rifle", f, min(min(gaps.values()), d_back), PROP_GAP + 0.02, "손에도 등에도 없는 소총")
            d = seg_dist(Pw[body_not_hands], a, b) - RIFLE_R
            j = int(np.argmin(d))
            rec("rifle_body", float(-d[j]), f)
            if -d[j] > PROP_PEN + 0.01:
                reg = [k for k, m in MASK.items() if m[body_not_hands[j]]]
                add("rifle_pen", "body:" + ",".join(reg), f, -d[j], PROP_PEN + 0.01, "소총이 몸을 뚫음")
            if prop:
                pf = prop["frames"][min(fi, len(prop["frames"]) - 1)]
                if pf["state"] != "hidden":
                    tail = u2b(pf["pos"])
                    nose = u2b(pf["forward"]).normalized()
                    # 두 선분 거리 (표본)
                    ts = np.linspace(0, 1, 13)
                    pts = np.array([tail + nose * (ROCKET_LEN * t) for t in ts])
                    dd = float(seg_dist(pts, a, b).min()) - ROCKET_R - RIFLE_R
                    rec("rocket_rifle", -dd, f)
                    if -dd > PROP_PEN:
                        add("prop_pen", "rocket>rifle", f, -dd, PROP_PEN, "로켓과 소총이 겹침")
        if fi == 0:
            first_hips = W["Hips"].translation.copy()
    for key, lst in prev_speed.items():
        for i in range(1, len(lst) - 1):
            f, w = lst[i]
            nb = max(lst[i - 1][1], lst[i + 1][1])
            if w > POP_MIN and w > POP_RATIO * nb:
                add("pop", key, f, w, "%.0f > %.1f x %.0f" % (w, POP_RATIO, nb))
    # 지우기
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.actions.remove(act)
    for a in list(bpy.data.armatures):
        if a.users == 0:
            bpy.data.armatures.remove(a)
    summary = {k: {"min": round(v[0], 3), "min_f": v[1], "max": round(v[2], 3), "max_f": v[3]} for k, v in series.items()}
    return {"clip": name, "frames": len(frames), "rest_err_m": round(rest_err, 5), "violations": V, "series": summary,
            "com_out_frames": com_out_frames}


def compress(V):
    """같은 종류 · 부위의 연속 프레임을 구간으로 묶는다."""
    out = {}
    for v in V:
        out.setdefault((v["kind"], v["part"]), []).append(v)
    rows = []
    for (k, p), vs in out.items():
        fr = sorted(set(v["frame"] for v in vs))
        spans = []
        for x in fr:
            if spans and x == spans[-1][1] + 1:
                spans[-1][1] = x
            else:
                spans.append([x, x])
        worst = max(vs, key=lambda v: abs(v["value"]))
        rows.append({"kind": k, "part": p, "frames": ",".join("%d" % a if a == b else "%d-%d" % (a, b) for a, b in spans),
                     "n": len(fr), "worst": worst["value"], "worst_f": worst["frame"], "limit": worst["limit"],
                     "note": worst.get("note", "")})
    return sorted(rows, key=lambda r: (r["kind"], r["part"]))


if WHICH == "loader":
    paths = sorted(glob.glob(os.path.join(SRC, "A_ROK_Loader_*.fbx")))
elif WHICH == "ref":
    paths = [p for p in sorted(glob.glob(os.path.join(SRC, "A_ROK_*.fbx")))
             if "Loader" not in p and "TPose" not in p and "Walk_Tired" not in p]
elif WHICH == "all":
    paths = [p for p in sorted(glob.glob(os.path.join(SRC, "A_ROK_*.fbx"))) if "TPose" not in p and "Walk_Tired" not in p]
else:
    paths = [os.path.join(SRC, n + ".fbx") for n in WHICH.split(",")]

RES = {}
for p in paths:
    r = check_clip(p)
    r["table"] = compress(r["violations"])
    RES[r["clip"]] = r
    print("CHECKCLIP %s frames %d rest_err %.5f violations %d" % (r["clip"], r["frames"], r["rest_err_m"], len(r["violations"])))
    for row in r["table"]:
        print("  VIOL %-18s %-26s frames %-28s worst %8.3f @%d limit %s %s" % (row["kind"], row["part"], row["frames"], row["worst"], row["worst_f"], row["limit"], row["note"]))
    S = r["series"]
    keys = [k for k in S if k.split("_")[0] in ("spine", "neck", "elbow", "knee", "hip", "ankle", "forearm", "wrist", "sh", "clav", "toe", "hum", "tib")]
    print("  RANGE " + " ".join("%s[%.0f,%.0f]" % (k, S[k]["min"], S[k]["max"]) for k in sorted(keys)))
    print("  OTHER " + " ".join("%s=%.3f@%d" % (k, S[k]["max"], S[k]["max_f"]) for k in sorted(S) if k.startswith(("pen_", "slip", "com", "rocket", "rifle")) and S[k]["max"] > -0.5))
    vmax = sorted(((S[k]["max"], k) for k in S if k.startswith("angvel_")), reverse=True)[:3]
    print("  ANGVEL " + " ".join("%s=%.0f@%d" % (k, v, S[k]["max_f"]) for v, k in vmax), "ground_min %.3f" % S["ground_min_z"]["min"])
with io.open(os.path.join(OUT, TAG + ".json"), "w", encoding="utf-8") as fh:
    json.dump({k: dict(v, n_violations=len(v["violations"])) for k, v in RES.items()},
              fh, ensure_ascii=False, indent=1)
print("CHECK DONE", os.path.join(OUT, TAG + ".json"))
