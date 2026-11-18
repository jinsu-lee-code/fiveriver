# 학도병 지친 걷기 A_ROK_Walk_Tired_v2 — 학도병 모델(SK_CH_ROK_Soldier.fbx, 게임 크기 0.8217)에 맞춘 Rigify 리그로 만든다.
# 실행 (리그 먼저: ROK_MODEL=student blender -b --factory-startup --python scripts/01_build_rig.py):
#   ROK_MODEL=student blender -b --python scripts/walk_10_make.py -- [render] [export] [blend]
# 걸음: 반복 1.2초(36프레임 + 첫 프레임 반복), 제자리. 디딘 발은 땅에 붙은 채 일정 속도 SPEED(1.0 m/s, 게임 크기)로 뒤로 간다.
#   → 게임에서 미끄러지지 않는 재생 배율 = 지면 속도 / (1.0 × 몸 크기 배율). 트럭 1.1 m/s → 1.1.
# 소총: 장면에서 학도병은 소총을 멜빵으로 등에 메고(프리팹이 뼈에 붙임) 두 손은 비어 있다 → 이 클립은 두 팔을 늘어뜨려 흔든다.
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
import rok_rig
from rok_rig import ease
from rok_timeline import qx, qy, qz

assert C.MODEL_KIND == "student", "ROK_MODEL=student 로 실행"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NAME = "A_ROK_Walk_Tired_v2"
OUT = os.path.join(C.WORK, "walk", "v2")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
scene = bpy.context.scene
scene.render.fps = C.FPS
R = rok_rig.Rig()
bpy.data.objects["M1_Garand"].hide_render = True       # 게임에서는 프리팹이 등에 멘 소총을 따로 붙인다
MESH = "StudentSoldier_Rigged_Mesh"
TAU = 2 * math.pi


class Spec:
    # 11-18 이어받은 판의 기본값 (측정 · 렌더로 고름 — README 10번): 34프레임 · 1.0 m/s · 착지 무릎 7 · 체중 받기 20 · 디딤 가운데 11 ·
    # 떼기 39 · 흔들기 57도. 첫 판 기본값은 주석의 "(첫 판 …)".
    frames = 34            # 한 주기 (첫 판 36: 한 주기 1.2 m라 다리가 닿지 않아 골반을 10 cm 내렸다)
    speed = 1.00           # 디딘 발이 뒤로 가는 속도 (m/s, 게임 크기)
    contact = 0.62         # 한 발이 땅에 있는 비율 (두 발 지지 12% × 2)
    foot_x = 0.090         # 발목 좌우 (반)
    toe_out = 7.0          # 발끝 바깥 (도)
    ankle_fwd_mid = 0.05   # 디딤 가운데에서 발목이 골반보다 앞(-)/뒤(+) (첫 판 -0.015: 착지 발이 너무 앞)
    hip_z = 0.776          # 골반(Hips) 평균 높이
    bob = 0.013            # 골반 상하 (반): 디딤 가운데 높고 두 발 지지 때 낮다
    sway = 0.022           # 골반 좌우 (디딘 발 쪽)
    pelvis_pitch = 7.0
    pelvis_yaw = 5.0
    pelvis_roll = 3.0      # 흔드는 발 쪽 골반이 처짐
    chest_pitch = 15.0     # 지친 등: 가슴이 앞으로 숙음
    chest_yaw = 4.0
    head_pitch = 20.0      # 몇 걸음 앞 땅을 봄
    heel_strike = -10.0    # 뒤꿈치 닿을 때 발끝 듦 (도, -) (첫 판 -11)
    heel_off = 32.0        # 발 떼기 직전 뒤꿈치 듦 (도)
    roll_flat = 0.12       # 디딤 중 발이 평평해지는 때 (뒤꿈치 → 발바닥)
    heel_rise = 0.52       # 뒤꿈치가 들리기 시작하는 때
    lift_back = 0.20       # 흔들기 앞부분 발목 높이 (뒤에서 듦)
    swing_toe = 16.0       # 그때 발끝이 아래로 향한 각
    clear = 0.018          # 흔들기 가운데 발볼이 땅에서 뜨는 높이 (첫 판 0.027 → 발 최저 2.7 cm; 실제 보행 1~2 cm)
    arm_swing = 11.0       # 어깨에서 팔 흔듦 (도, 반)
    arm_mean = -2.0        # 팔 평균 (- = 조금 뒤)
    elbow = 16.0
    elbow_fwd = 10.0       # 앞으로 흔들 때 더 굽힘
    arm_out = 7.0          # 팔이 몸통에서 벌어지는 각
    reach = 0.998          # 다리를 이 비율 이상 펴지 않게 골반을 내림 (무릎 최소 = 2·acos(reach): 0.99 → 16도, 0.998 → 7도) (첫 판 0.99)
    reach_over = 1.0       # 골반 내림을 부드럽게 할 때의 여유 배율
    # 11-18 이어받음: 골반 높이를 "디딘 다리의 목표 무릎 각"에서 거꾸로 구한다(knee_drive 1). 수직에 가까운 다리에서 골반을
    # 2 cm만 내려도 무릎이 28도 굽으므로, 평균 높이(hip_z) · 상하(bob)를 정해 두는 방식으로는 무릎을 맞출 수 없었다.
    # 목표(디딤 진행 u 0~1): 착지 k_contact → 체중 받기 최대 k_load(u k_load_u) → 디딤 가운데 최소 k_mid(u k_mid_u) → k_late(u k_late_u).
    # 그 뒤(다른 발이 닿은 뒤)는 떼는 다리가 골반 높이를 정하지 않는다 — 뒤꿈치를 들어 닿고, 무릎은 결과로 굽는다.
    knee_drive = 1.0
    k_contact = 9.0
    k_load = 19.0
    k_load_u = 0.16
    k_mid = 9.0
    k_mid_u = 0.45
    k_late = 11.0
    k_late_u = 0.72
    k_push = 12.0          # 다른 발이 닿는 순간 떼는 다리 무릎 (실제 보행 약 5~10도)
    k_toeoff = 38.0        # 발끝이 떨어지는 순간 (실제 보행 35~40도)
    hand_over = 0.10       # 두 다리 사이에서 골반 높이를 넘겨주는 구간 (디딤 진행 비율)
    pelvis_smooth = 1.2    # 골반 높이 곡선 가우시안 (프레임)
    heel_solve = 1.0       # 뒤꿈치 드는 각을 떼는 다리 무릎 목표에서 거꾸로 구함
    swing_reach = 0.998    # 흔드는 다리가 이보다 펴지지 않게 발목을 엉덩이 쪽으로 당김 (골반은 내리지 않음)
    swing_over = 0.02      # 착지 직전(흔들기 82%) 발목이 착지점보다 앞에 나가는 거리 — 뒤로 당겨 디딤 (첫 판 0.07: 다리 길이를 넘어 골반이 4 cm 내려감)

    @property
    def period(self):
        return self.frames / 30.0


S = Spec()
OPT = {a.split("=", 1)[0]: a.split("=", 1)[1] for a in argv if "=" in a}
TAG = OPT.get("tag", "")
if TAG:
    OUT = os.path.join(C.WORK, "walk", "v2_" + TAG)
    os.makedirs(OUT, exist_ok=True)
# 11-18 이어받음: 학도병 메시의 왼손은 뼈(DEF-hand.L, 오른손과 거울 대칭) 기준으로 손바닥이 뒤집혀 붙어 있다
# (바인드 자세 손가락 마디가 휜 방향: 오른손 손바닥 아래, 왼손 위). 손가락 굽힘 축은 _probe_fingers가 소총 쥔 자세에서
# 고르므로 왼손은 축(x/z)과 부호를 렌더로 보고 정한다: lpalm=1 → 손등 목표를 오른손 거울로, lfax · lfsg → 왼손가락 굽힘 축 · 부호.
# 렌더로 고른 기본값(hE): 왼손도 오른손과 같은 축 · 부호 — 뼈가 거울 대칭이므로. (이전 작업자의 "손바닥 뒤집힘" 보정은 손가락이
# 앞으로 갈고리처럼 굽고 엄지가 뻗어 어색했다. 원인은 _probe_fingers가 소총 쥔 자세(왼손 손바닥 위)에서 축을 고른 것으로 본다.)
for k, v in (("lpalm", "1"), ("lfax", "z"), ("lfsg", "1"), ("lthsg", "1")):
    OPT.setdefault(k, v)
if OPT.get("lold") == "1":       # 이전 작업자 판 그대로
    for k in ("lpalm", "lfax", "lfsg", "lthsg"):
        OPT.pop(k, None)
LEFT_PALM_DOWN = "leftpalmdown" in argv or OPT.get("lpalm") == "1"
for f in ("f_index", "f_middle", "f_ring", "f_pinky"):
    ax, sg = R.finger_sign[(f, "L")]
    if "lfax" in OPT:
        ax = Vector((1, 0, 0)) if OPT["lfax"] == "x" else Vector((0, 0, 1))
    if "lfsg" in OPT:
        sg = float(OPT["lfsg"])
    elif not LEFT_PALM_DOWN:
        sg = -sg
    R.finger_sign[(f, "L")] = (ax, sg)
if "lthsg" in OPT:
    R.finger_sign[("thumb", "L")] = (R.finger_sign[("thumb", "L")][0], float(OPT["lthsg"]))
print("FINGER_SIGN", {k: (tuple(v[0]), v[1]) for k, v in R.finger_sign.items() if k[0] in ("f_middle", "thumb")})
for k, v in OPT.items():
    if hasattr(Spec, k):
        setattr(S, k, float(v))

# ---------------------------------------------------------------- 발 모양 (학도병 기준 자세: 발끝이 14도 바깥)
REST_YAW = {}
CANON = {}       # 발볼 → 발목 (발을 정면으로 돌린 기준)
HEEL = {}        # 발볼 → 뒤꿈치 바닥점
for s in ("L", "R"):
    d = R.ball0[s] - R.ankle0[s]
    REST_YAW[s] = math.atan2(d.x, -d.y)          # 왼발 + = 바깥(+X)
    unyaw = Quaternion((0, 0, 1), -REST_YAW[s])
    off = unyaw @ (R.ankle0[s] - R.ball0[s])
    off.x = 0.0
    CANON[s] = off
    # 뒤꿈치 바닥점: 발목 바로 뒤 4.5 cm, 땅(0)
    HEEL[s] = Vector((0.0, off.y + 0.045, -R.ball0[s].z))
print("REST", {s: (round(math.degrees(REST_YAW[s]), 1), tuple(round(c, 3) for c in CANON[s])) for s in "LR"},
      "ball0 z", round(R.ball0["L"].z, 4), "ankle0 z", round(R.ankle0["L"].z, 4), "thigh", round(R.thigh, 4), "shin", round(R.shin, 4))


def yaw_of(s):
    return math.radians(S.toe_out if s == "L" else -S.toe_out)


def foot_q(s, theta):
    """발 회전(기준 자세 대비): 발을 정면으로 돌림 → 발 옆축으로 theta(+ = 발끝 아래) → 바깥으로 toe_out."""
    return Quaternion((0, 0, 1), yaw_of(s)) @ Quaternion((1, 0, 0), math.radians(theta)) @ Quaternion((0, 0, 1), -REST_YAW[s])


def toe_flat_q(s):
    return Quaternion((0, 0, 1), yaw_of(s)) @ Quaternion((0, 0, 1), -REST_YAW[s])


def ground_foot(s, ball_xy, theta):
    """발볼(theta >= 0) 또는 뒤꿈치(theta < 0)가 땅에 닿은 발 → (발목, 발 회전, 닿은 점)."""
    yq = Quaternion((0, 0, 1), yaw_of(s))
    rq = Quaternion((1, 0, 0), math.radians(theta))
    ball = Vector((ball_xy[0], ball_xy[1], R.ball0[s].z))
    if theta >= 0:
        return ball + yq @ (rq @ CANON[s]), foot_q(s, theta), ball
    heel = ball + yq @ HEEL[s]
    return heel + yq @ (rq @ (CANON[s] - HEEL[s])), foot_q(s, theta), heel


TRAVEL = S.speed * S.contact * S.period
BALL_LAND = S.ankle_fwd_mid - CANON["L"].y - TRAVEL * 0.5      # 디딤 가운데에서 발목이 골반 앞 ankle_fwd_mid


HEEL_TABLE = None      # heel_solve가 채우는 [(u, 뒤꿈치 드는 각)]
# 디딘 발 메시 높이 보정 (11-18): 학도병 군화 바닥은 발 · 발가락 · 정강이 뼈 가중치를 섞어 받아(바닥 정점 평균 정강이 30%),
# 정강이가 기울고 뒤꿈치가 들리면 발 뼈 목표가 땅에 있어도 군화 바닥이 0.5~11 mm 뜬다. 측정한 디딤 진행별 뜸(m)만큼 발 목표를
# 내린다. 표는 walk_v2_footcomp.json (updatefootcomp 인자로 측정해 누적 갱신, nofootcomp로 끔).
FOOTCOMP_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "walk_v2_footcomp.json")
FOOT_COMP = None
if "nofootcomp" not in argv and os.path.exists(FOOTCOMP_PATH):
    with io.open(FOOTCOMP_PATH, encoding="utf-8") as fh:
        FOOT_COMP = json.load(fh)["table"]


def foot_comp(u):
    if not FOOT_COMP:
        return 0.0
    if u <= FOOT_COMP[0][0]:
        return FOOT_COMP[0][1]
    for (u0, c0), (u1, c1) in zip(FOOT_COMP, FOOT_COMP[1:]):
        if u <= u1:
            return c0 + (c1 - c0) * (u - u0) / max(1e-6, u1 - u0)
    return FOOT_COMP[-1][1]


def heel_theta(u):
    if not HEEL_TABLE:
        return S.heel_off * ease((u - S.heel_rise) / (1 - S.heel_rise), "in")
    if u <= HEEL_TABLE[0][0]:
        return HEEL_TABLE[0][1] * max(0.0, (u - S.heel_rise) / max(1e-6, HEEL_TABLE[0][0] - S.heel_rise))
    for (u0, t0), (u1, t1) in zip(HEEL_TABLE, HEEL_TABLE[1:]):
        if u <= u1:
            return t0 + (t1 - t0) * (u - u0) / max(1e-6, u1 - u0)
    (u0, t0), (u1, t1) = HEEL_TABLE[-2], HEEL_TABLE[-1]
    return t1 + (t1 - t0) * (u - u1) / max(1e-6, u1 - u0)


def stance(s, u):
    """디딤 u(0~1): 발볼 기준점이 일정 속도로 뒤로. 발 각도: 뒤꿈치 닿음(발끝 듦) → 평평 → 뒤꿈치 듦."""
    x = S.foot_x if s == "L" else -S.foot_x
    by = BALL_LAND + TRAVEL * u
    if u < S.roll_flat:
        th = S.heel_strike * (1 - ease(u / S.roll_flat, "inout"))
    elif u < S.heel_rise:
        th = 0.0
    else:
        th = heel_theta(u)
    a, q, c = ground_foot(s, (x, by), th)
    a = a - Vector((0.0, 0.0, foot_comp(u)))
    # 발가락: 뒤꿈치로 닿아 발끝이 들린 동안은 발과 나란히(흔들기 끝과 같은 모양 — 11-18 측정에서 착지 순간 발가락이
    # 한 프레임에 11도 꺾여 반복 이음매가 12.8도 튀었다), 발바닥이 닿은 뒤로는 땅에 평평(뒤꿈치를 들면 발가락이 꺾임).
    toe = q if th < 0 else toe_flat_q(s)
    return a, q, toe, c, th


def hermite(p0, m0, p1, m1, u):
    h00 = 2 * u ** 3 - 3 * u ** 2 + 1
    h10 = u ** 3 - 2 * u ** 2 + u
    h01 = -2 * u ** 3 + 3 * u ** 2
    h11 = u ** 3 - u ** 2
    return p0 * h00 + m0 * h10 + p1 * h01 + m1 * h11


def spline(keys, u, m_start, m_end):
    """keys: [(s, value)], 끝 기울기 지정, 안쪽은 Catmull-Rom."""
    n = len(keys)
    for i in range(n - 1):
        s0, v0 = keys[i]
        s1, v1 = keys[i + 1]
        if u <= s1 or i == n - 2:
            d = s1 - s0
            if i == 0:
                m0 = m_start
            else:
                m0 = (v1 - keys[i - 1][1]) / (s1 - keys[i - 1][0])
            if i == n - 2:
                m1 = m_end
            else:
                m1 = (keys[i + 2][1] - v0) / (keys[i + 2][0] - s0)
            return hermite(v0, m0 * d, v1, m1 * d, (u - s0) / d)


def swing(s, u):
    """흔들기 u(0~1): 뒤꿈치 든 채 떠나 → 무릎 굽혀 발을 들고 → 골반 아래를 낮게 지나 → 앞으로 뻗어 뒤꿈치로 닿음."""
    a0, q0, _, _, th0 = stance(s, 1.0)
    a1, q1, _, _, th1 = stance(s, 0.0)
    T = (1 - S.contact) * S.period
    vy = S.speed * T                    # 끝에서 땅 속도와 같게 (뒤로)
    rest_ankle = R.ankle0[s].z
    ball_up = CANON[s].z                # 평평한 발에서 발목이 발볼보다 높은 정도
    mid_z = R.ball0[s].z + S.clear + ball_up * 0.98
    ys = [(0.0, a0.y), (0.28, a0.y - 0.14), (0.55, S.ankle_fwd_mid - 0.02), (0.82, a1.y - S.swing_over), (1.0, a1.y)]
    zs = [(0.0, a0.z), (0.24, S.lift_back), (0.55, mid_z), (0.83, a1.z + 0.030), (1.0, a1.z)]
    ths = [(0.0, th0), (0.24, S.swing_toe), (0.52, 0.0), (0.80, S.heel_strike - 3.0), (1.0, th1)]
    y = spline(ys, u, vy * 0.6, vy * 0.5)
    z = spline(zs, u, 0.55 * T, -0.25 * T)
    th = spline(ths, u, 40.0 * T, 6.0 * T)
    x = a0.x
    yq = Quaternion((0, 0, 1), yaw_of(s))
    q = foot_q(s, th)
    # 발가락: 떠날 때 땅에 평평(발에 대해 꺾임) → 곧 발과 나란히
    bend = ease(min(1.0, u / 0.35), "out")
    toe = toe_flat_q(s).slerp(q, bend)
    return Vector((x, y, z)), q, toe


def walk_pose(base, t):
    phi = (t / S.period) % 1.0
    P = rok_rig.copy_pose(base)
    contact = {}
    for s, ph0 in (("L", 0.0), ("R", 0.5)):
        psi = (phi - ph0) % 1.0
        if psi < S.contact:
            a, q, toe, c, th = stance(s, psi / S.contact)
            contact[s] = (c, th)
        else:
            a, q, toe = swing(s, (psi - S.contact) / (1 - S.contact))
        P["foot_" + s] = (a, q)
        P["toe_" + s] = toe
        P["knee_" + s] = Vector((a.x * 1.2, -1.5, 0.50))
    mid = S.contact / 2                                 # 왼발 디딤 가운데
    z = S.hip_z + S.bob * math.cos(TAU * 2 * (phi - mid))
    x = S.sway * math.cos(TAU * (phi - mid))
    P["pelvis_loc"] = Vector((x, 0.0, z))
    yaw = -S.pelvis_yaw * math.cos(TAU * phi)           # 왼발이 앞일 때 골반 왼쪽이 앞 (qz + 는 왼쪽 엉덩이를 뒤로 보냄)
    roll = S.pelvis_roll * math.cos(TAU * (phi - mid))  # 왼발 디딤 → 오른쪽(흔드는 쪽) 처짐
    P["pelvis_rot"] = qz(yaw) @ qy(-roll) @ qx(S.pelvis_pitch)
    P["chest_rot"] = qz(-yaw * S.chest_yaw / S.pelvis_yaw) @ qy(roll * 0.5) @ qx(S.chest_pitch + 1.2 * math.cos(TAU * 2 * (phi - mid - 0.06)))
    P["head_rot"] = qz(-0.3 * yaw) @ qx(S.head_pitch + 1.5 * math.cos(TAU * 2 * (phi - mid - 0.12)))
    P["neck_share"] = 0.5
    P["grip_L"] = P["grip_R"] = 0.0
    P["fingers_L"] = P["fingers_R"] = 0.42
    P["thumb_L"] = P["thumb_R"] = 0.25
    P["index_R"] = 0.38
    return P, contact, phi


def arms(P, phi):
    """늘어뜨린 두 팔: 어깨에서 다리와 반대로 흔듦(조금 늦게). 손은 월드 목표로."""
    R.apply(P)
    for s, sign in (("L", 1.0), ("R", -1.0)):
        sh = R.pb["DEF-upper_arm." + s].head.copy()
        # 왼팔은 오른발과 같이 앞으로 (오른발 앞 = phi 0.5)
        a = S.arm_mean + S.arm_swing * math.cos(TAU * (phi - (0.5 if s == "L" else 0.0) - 0.04))
        fwd = max(0.0, math.cos(TAU * (phi - (0.5 if s == "L" else 0.0) - 0.04)))
        e = S.elbow + S.elbow_fwd * fwd
        out = Quaternion((0, 1, 0), math.radians(-S.arm_out * sign))      # 팔을 바깥(왼팔 +X)으로
        up_dir = out @ (Quaternion((1, 0, 0), math.radians(-a)) @ Vector((0, 0, -1)))
        fo_dir = out @ (Quaternion((1, 0, 0), math.radians(-(a + e))) @ Vector((0, 0, -1)))
        elbow = sh + up_dir * R.upper
        wrist = elbow + fo_dir * R.fore
        fingers = (fo_dir + Vector((0, -0.12, 0))).normalized()
        # 손등은 바깥 · 조금 앞. 학도병 왼손은 기준 자세에서 손바닥이 위를 향해(렌더로 확인) hand_abs의 '손등 = 위' 가정이 뒤집힌다
        back = Vector((sign * 1.0, -0.35, 0.0)) if s == "R" else Vector((-1.0, 0.35, 0.0)) * (-1.0 if LEFT_PALM_DOWN else 1.0)
        q = R.hand_abs(s, fingers, back)
        P["hand_" + s] = (wrist, q)
        P["hand_space_" + s] = "world"
        pole_w = elbow + Vector((sign * 0.10, 0.45, 0.0))
        P["elbow_" + s] = R.delta4("DEF-spine.003").inverted() @ pole_w
    return P


def reach_fix(poses, contacts=None):
    lmax = S.reach * (R.thigh + R.shin)
    n = len(poses)
    need = []
    why = []
    for i, P in enumerate(poses):
        d = 0.0
        w = ""
        for s in ("L", "R"):
            if contacts is not None and s not in contacts[i]:
                continue          # 흔드는 다리는 swing_clamp가 맡는다
            hip = P["pelvis_loc"] + P["pelvis_rot"] @ (R.hj0[s] - R.h0)
            a = P["foot_" + s][0]
            v = a - hip
            if v.length > lmax:
                horiz = math.hypot(v.x, v.y)
                dd = -v.z - math.sqrt(max(0.0, lmax ** 2 - horiz ** 2))
                if dd > d:
                    d, w = dd, "%s %.3f" % (s, dd)
        need.append(d)
        why.append(w)
    if "debugreach" in argv:
        print("REACH_NEED", [(i + 1, x) for i, x in enumerate(why) if x])
    m = n - 1
    # 필요한 만큼만 내리면 한 프레임에 2 cm씩 툭 떨어진다 → 앞뒤 4프레임의 최댓값을 잡고 넓게 부드럽게
    dil = [max(need[(i + k) % m] for k in range(-2, 3)) for i in range(m)]
    out = []
    for i in range(n):
        acc = ws = 0.0
        for k in range(-6, 7):
            w = math.exp(-0.5 * (k / 2.5) ** 2)
            acc += w * dil[(i + k) % m]
            ws += w
        out.append(max(acc / ws * S.reach_over, need[i % m]))
    for P, d in zip(poses, out):
        P["pelvis_loc"] = P["pelvis_loc"] - Vector((0, 0, d))
    return max(need)


def knee_target(u):
    other_land = 0.5 / S.contact
    keys = [(0.0, S.k_contact), (S.k_load_u, S.k_load), (S.k_mid_u, S.k_mid), (S.k_late_u, S.k_late), (other_land, S.k_push),
            (1.0, S.k_toeoff)]
    return spline(keys, min(max(u, 0.0), 1.0), 0.0, 60.0)


def heel_solve(poses, phis):
    """뒤꿈치가 들리는 동안(u ≥ heel_rise) 발볼을 축으로 발을 얼마나 세워야 떼는 다리 무릎이 목표가 되는가 → [(u, 각)]."""
    got = {}
    for P, phi in zip(poses[:-1], phis[:-1]):
        for s, ph0 in (("L", 0.0), ("R", 0.5)):
            psi = (phi - ph0) % 1.0
            if psi >= S.contact:
                continue
            u = psi / S.contact
            if u < S.heel_rise:
                continue
            x = S.foot_x if s == "L" else -S.foot_x
            by = BALL_LAND + TRAVEL * u
            hj = P["pelvis_loc"] + P["pelvis_rot"] @ (R.hj0[s] - R.h0)
            want = leg_reach(knee_target(u))

            def dist(th):
                return (ground_foot(s, (x, by), th)[0] - hj).length
            lo, hi = 0.0, 75.0
            if dist(lo) <= want:
                th = 0.0
            elif dist(hi) > want:
                th = hi
            else:
                for _ in range(40):
                    mid = 0.5 * (lo + hi)
                    if dist(mid) > want:
                        lo = mid
                    else:
                        hi = mid
                th = 0.5 * (lo + hi)
            got.setdefault(round(u, 4), []).append(th)
    table = sorted((u, sum(v) / len(v)) for u, v in got.items())
    # 단조 증가(뒤꿈치가 다시 내려오지 않게) · 가볍게 부드럽게
    out, m = [], 0.0
    for u, th in table:
        m = max(m, th)
        out.append([u, m])
    for _ in range(2):
        out = [out[0]] + [[out[i][0], (out[i - 1][1] + 2 * out[i][1] + out[i + 1][1]) / 4] for i in range(1, len(out) - 1)] + [out[-1]]
    return [tuple(v) for v in out]


def swing_clamp(poses, phis):
    """흔드는 다리가 다리 길이를 넘는 목표를 받으면(착지 직전 앞으로 뻗을 때) 발목을 엉덩이 쪽으로 당긴다."""
    lmax = S.swing_reach * (R.thigh + R.shin)
    moved = 0.0
    for P, phi in zip(poses, phis):
        for s, ph0 in (("L", 0.0), ("R", 0.5)):
            if (phi - ph0) % 1.0 < S.contact:
                continue
            hj = P["pelvis_loc"] + P["pelvis_rot"] @ (R.hj0[s] - R.h0)
            a, q = P["foot_" + s]
            v = a - hj
            if v.length > lmax:
                na = hj + v.normalized() * lmax
                moved = max(moved, (na - a).length)
                P["foot_" + s] = (na, q)
    return moved


def leg_reach(kappa):
    """무릎을 kappa도 굽혔을 때 엉덩이 관절-발목 거리."""
    k = math.radians(kappa)
    return math.sqrt(R.thigh ** 2 + R.shin ** 2 + 2 * R.thigh * R.shin * math.cos(k))


def knee_drive(poses, phis):
    """디딘 다리의 목표 무릎 각이 나오도록 골반 높이를 정한다. 반환: 프레임별 (목표 무릎, 주 다리)."""
    # 체중을 받는 다리(디딤 0 ~ k_late_u)만 골반 높이를 정한다. 그 뒤 다른 발이 닿을 때까지는 보간하고, 떼는 다리는
    # heel_solve가 뒤꿈치 드는 각으로 무릎 목표를 맞춘다. (두 다리 무게를 섞던 첫 판은 착지 순간 골반이 떼는 다리 기준으로
    # 높아 착지 다리가 닿지 않았다 → reach_fix가 2~3 cm 내려 체중 받기 무릎이 25~28도가 됐다.)
    m = len(poses) - 1
    zs, info = [None] * len(poses), []
    for i, (P, phi) in enumerate(zip(poses, phis)):
        tag = []
        for s, ph0 in (("L", 0.0), ("R", 0.5)):
            psi = (phi - ph0) % 1.0
            if psi >= S.contact:
                continue
            u = psi / S.contact
            if u > S.k_late_u + 1e-6:
                continue
            hj = P["pelvis_loc"] + P["pelvis_rot"] @ (R.hj0[s] - R.h0)
            a = P["foot_" + s][0]
            horiz = math.hypot(hj.x - a.x, hj.y - a.y)
            kappa = knee_target(u)
            d = leg_reach(kappa)
            if horiz >= d:
                continue
            hz = a.z + math.sqrt(d * d - horiz * horiz)
            zs[i] = hz - (P["pelvis_rot"] @ (R.hj0[s] - R.h0)).z
            tag.append((s, round(u, 2), round(kappa, 1)))
        info.append(tag)
    known = [i for i in range(m) if zs[i] is not None]
    for i in range(m):
        if zs[i] is None:
            a = max((k for k in known if k < i), default=known[-1] - m)
            b = min((k for k in known if k > i), default=known[0] + m)
            za, zb = zs[a % m], zs[b % m]
            t = (i - a) / (b - a)
            zs[i] = za + (zb - za) * (t * t * (3 - 2 * t))
    zs[m] = zs[0]
    # 부드럽게 (반복 주기로 감음)
    sm = []
    for i in range(len(poses)):
        acc = ws = 0.0
        for k in range(-5, 6):
            w = math.exp(-0.5 * (k / max(0.01, S.pelvis_smooth)) ** 2)
            acc += w * zs[(i + k) % m]
            ws += w
        sm.append(acc / ws)
    for P, z in zip(poses, sm):
        P["pelvis_loc"] = Vector((P["pelvis_loc"].x, P["pelvis_loc"].y, z))
    print("KNEE_DRIVE pelvis z %.3f~%.3f" % (min(sm), max(sm)))
    return info


base = R.base_pose()
n = int(S.frames) + 1
times = [i / C.FPS for i in range(n)]


def build():
    ps, cs, fs = [], [], []
    for t in times:
        P, c, phi = walk_pose(base, t)
        ps.append(P)
        cs.append(c)
        fs.append(phi)
    if S.knee_drive:
        knee_drive(ps, fs)
    return ps, cs, fs


poses, contacts, phis = build()
if S.knee_drive and S.heel_solve:
    for _ in range(4):
        HEEL_TABLE = heel_solve(poses, phis)
        poses, contacts, phis = build()
    print("HEEL_TABLE", [(round(u, 2), round(t, 1)) for u, t in HEEL_TABLE])
if S.swing_reach > 0:
    print("SWING_CLAMP max %.4f m" % swing_clamp(poses, phis))
reach = reach_fix(poses, contacts if S.swing_reach > 0 else None)

for obj in (R.rig, R.exp):
    if obj.animation_data is None:
        obj.animation_data_create()
    nm = NAME + ("_rig" if obj is R.rig else "")
    old = bpy.data.actions.get(nm)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(nm)
    act.use_fake_user = True
    obj.animation_data.action = act
R.prev_q = {}
prev = {}
rec = []
dg = None
for i, P in enumerate(poses):
    f = i + 1
    scene.frame_set(f)
    P = arms(P, phis[i])
    R.apply(P)
    R.key_controls(f)
    world = R.bake_export(f, prev)
    m = R.measure(world)
    m["world"] = {k: v.copy() for k, v in world.items()}
    m["contact"] = contacts[i]
    rec.append(m)
scene.frame_start, scene.frame_end = 1, n

# ---------------------------------------------------------------- 측정
def ang(a, b):
    return math.degrees(a.angle(b))


heel_loc, tip_loc = {}, {}
for s, Sd in (("L", "Left"), ("R", "Right")):
    ra = R.exp_rest[Sd + "Foot"]
    hw = R.ankle0[s] + (Quaternion((0, 0, 1), REST_YAW[s]) @ Vector((0, 0.045, 0))) - Vector((0, 0, R.ankle0[s].z))
    heel_loc[s] = ra.inverted() @ hw
    rt = R.exp_rest[Sd + "Toes"]
    tw = R.ball0[s] + (Quaternion((0, 0, 1), REST_YAW[s]) @ Vector((0, -0.075, 0))) - Vector((0, 0, R.ball0[s].z))
    tip_loc[s] = rt.inverted() @ tw
rest_ank = {}
for s, Sd in (("L", "Left"), ("R", "Right")):
    j = R.exp_rest
    rest_ank[s] = ang(j[Sd + "Foot"].translation - j[Sd + "LowerLeg"].translation, j[Sd + "Toes"].translation - j[Sd + "Foot"].translation)


def pitch_down(v):
    """수평 아래로 몇 도 (+ = 앞 끝이 아래)."""
    return math.degrees(math.atan2(-v.z, math.hypot(v.x, v.y)))


rest_pitch = {s: pitch_down(R.ball0[s] - R.ankle0[s]) for s in ("L", "R")}

rows = []
for i, r in enumerate(rec):
    w = r["world"]
    row = {"f": i + 1, "hips_z": w["Hips"].translation.z}
    for s, Sd in (("L", "Left"), ("R", "Right")):
        hip, knee, ank, ball = (w[Sd + k].translation for k in ("UpperLeg", "LowerLeg", "Foot", "Toes"))
        heel = w[Sd + "Foot"] @ heel_loc[s]
        tip = w[Sd + "Toes"] @ tip_loc[s]
        # 발바닥이 땅과 이루는 각 (+ = 발끝이 들림, - = 뒤꿈치가 들림): 발목→발볼 선의 기울기를 기준 자세(선 자세)와 비교
        row["sole_" + s] = rest_pitch[s] - pitch_down(ball - ank)
        psi = (phis[i] - (0.0 if s == "L" else 0.5)) % 1.0
        if i == len(rec) - 1:
            psi = (0.0 if s == "L" else 0.5)
        row["u_" + s] = psi / S.contact if psi < S.contact else 1.0 + (psi - S.contact) / (1 - S.contact)   # 0~1 디딤, 1~2 흔들기
        row["knee_" + s] = ang(knee - hip, ank - knee)
        row["dorsi_" + s] = rest_ank[s] - ang(ank - knee, ball - ank)
        row["ankle_z_" + s] = ank.z
        row["ball_z_" + s] = ball.z - R.ball0[s].z
        row["heel_z_" + s] = heel.z
        row["tip_z_" + s] = tip.z
        row["heel_y_" + s] = heel.y
        row["ball_y_" + s] = ball.y
        row["c_" + s] = "-" if s not in r["contact"] else ("heel" if r["contact"][s][1] < 0 else "ball")
    rows.append(row)
summ = {"clip": NAME, "frames": n, "seconds": round((n - 1) / 30, 3), "speed_mps": S.speed, "reach_drop_max": round(reach, 4)}
for s in ("L", "R"):
    st = [r for r in rows[:-1] if r["c_" + s] != "-"]
    sw = [r for r in rows[:-1] if r["c_" + s] == "-"]
    # 닿은 점 속도 (뒤로 +): 디딤 연속 프레임
    sp = []
    for i in range(len(rows) - 1):
        a, b = rows[i], rows[i + 1]
        if a["c_" + s] != "-" and b["c_" + s] != "-":
            k = "heel_y_" if a["c_" + s] == "heel" and b["c_" + s] == "heel" else "ball_y_"
            if a["c_" + s] != b["c_" + s]:
                continue
            sp.append((b[k + s] - a[k + s]) * 30)
    # 디딘 발 접지점이 땅에서 뜨거나 묻힌 정도
    gap = []
    for r in st:
        gap.append(r["heel_z_" + s] if r["c_" + s] == "heel" else r["ball_z_" + s])
    summ[s] = {
        "stance_frames": len(st), "swing_frames": len(sw),
        "knee_heelstrike": round(next(r["knee_" + s] for r in rows if r["c_" + s] == "heel"), 1),
        "knee_stance_max": round(max(r["knee_" + s] for r in st), 1),
        "knee_midstance": round(st[len(st) // 2]["knee_" + s], 1),
        "knee_stance_min": round(min(r["knee_" + s] for r in st), 1),
        "knee_swing_max": round(max(r["knee_" + s] for r in sw), 1),
        "dorsi_range": [round(min(r["dorsi_" + s] for r in rows), 1), round(max(r["dorsi_" + s] for r in rows), 1)],
        "ankle_z_max": round(max(r["ankle_z_" + s] for r in rows), 3),
        "ball_lift_max": round(max(r["ball_z_" + s] for r in rows), 3),
        "tip_z_swing_min": round(min(r["tip_z_" + s] for r in sw), 3),
        "tip_z_swing_max": round(max(r["tip_z_" + s] for r in sw), 3),
        "heel_z_max": round(max(r["heel_z_" + s] for r in rows), 3),
        "toe_above_heel_swing_max": round(max(r["tip_z_" + s] - r["heel_z_" + s] for r in sw), 3),
        "contact_speed": [round(min(sp), 3), round(sum(sp) / len(sp), 3), round(max(sp), 3)] if sp else None,
        "contact_gap_mm": [round(min(gap) * 1000, 1), round(max(gap) * 1000, 1)],
    }
    # 단계별 (실제 보행 자료와 비교: 착지 0~5 · 체중 받기 최대 15~20 · 디딤 가운데 최소 5~10 · 떼기 35~40 · 흔들기 최대 60~65도)
    stn = sorted((r for r in rows[:-1] if r["u_" + s] < 1.0), key=lambda r: r["u_" + s])
    swg = sorted((r for r in rows[:-1] if r["u_" + s] >= 1.0), key=lambda r: r["u_" + s])
    summ[s].update({
        "knee_contact": round(stn[0]["knee_" + s], 1),
        "knee_load_peak": round(max(r["knee_" + s] for r in stn if r["u_" + s] <= 0.35), 1),
        "knee_mid_min": round(min(r["knee_" + s] for r in stn if 0.25 <= r["u_" + s] <= 0.75), 1),
        "knee_toeoff": round(stn[-1]["knee_" + s], 1),
        "knee_swing_max": round(max(r["knee_" + s] for r in swg), 1),
        "sole_contact": round(stn[0]["sole_" + s], 1),
        "sole_toeoff": round(stn[-1]["sole_" + s], 1),
        "sole_swing_up_max": round(max(r["sole_" + s] for r in swg), 1),
        "sole_swing_down_min": round(min(r["sole_" + s] for r in swg), 1),
        "tip_z_midswing_min": round(min(r["tip_z_" + s] for r in swg if 1.25 <= r["u_" + s] <= 1.75), 4),
        "contact_slip_mps": round(max(abs(v - S.speed) for v in sp), 3) if sp else None,
    })
summ["hips_z"] = [round(min(r["hips_z"] for r in rows), 3), round(max(r["hips_z"] for r in rows), 3)]
first, last = rec[0]["world"], rec[-1]["world"]
summ["loop_pos_m"] = round(max((first[b].translation - last[b].translation).length for b in first), 5)
summ["hand_z"] = [round(min(min(r["wrist_L"].z, r["wrist_R"].z) for r in rec), 3), round(max(max(r["wrist_L"].z, r["wrist_R"].z) for r in rec), 3)]
summ["def_err_max"] = round(max(r["def_err"] for r in rec), 5)
# 반복 이음매의 속도: 마지막 한 프레임 동안의 회전 · 이동이 첫 한 프레임 동안의 것과 같아야 튀지 않는다
def step(a, b):
    return a.to_quaternion().rotation_difference(b.to_quaternion())


def second_diff(i):
    """프레임 i에서 한 프레임 회전 · 이동이 얼마나 바뀌나 (i=0이면 마지막 → 첫 프레임 이음매). 뼈별 최대."""
    m = len(rec) - 1
    a, b, c = rec[(i - 1) % m]["world"], rec[i]["world"], rec[(i + 1) % m]["world"]
    if i == 0:
        a = rec[m - 1]["world"]
    best_a = best_p = (0.0, "")
    for bn in b:
        q1, q2 = step(a[bn], b[bn]), step(b[bn], c[bn])
        da = math.degrees(2 * math.acos(min(1.0, abs(q1.rotation_difference(q2).w))))   # 쿼터니언 부호와 무관한 각
        dp = ((c[bn].translation - b[bn].translation) - (b[bn].translation - a[bn].translation)).length * 1000
        best_a = max(best_a, (da, bn))
        best_p = max(best_p, (dp, bn))
    return best_a, best_p


sd = [second_diff(i) for i in range(len(rec) - 1)]
summ["loop_second_diff"] = {"seam_deg": [round(sd[0][0][0], 2), sd[0][0][1]], "seam_mm": [round(sd[0][1][0], 1), sd[0][1][1]],
                            "inside_max_deg": [round(max(x[0][0] for x in sd[1:]), 2), max(sd[1:])[0][1]],
                            "inside_max_mm": [round(max(x[1][0] for x in sd[1:]), 1), max(sd[1:], key=lambda x: x[1])[1][1]],
                            # 같은 위상(오른발 착지, 반 주기)의 값: 이음매가 이와 같으면 이음매가 클립 안쪽과 구별되지 않는다
                            "half_deg": round(sd[(len(rec) - 1) // 2][0][0], 2), "half_mm": round(sd[(len(rec) - 1) // 2][1][0], 1)}
summ["loop_velocity_gap"] = [summ["loop_second_diff"]["seam_deg"][0], summ["loop_second_diff"]["seam_mm"][0], summ["loop_second_diff"]["inside_max_deg"][0], summ["loop_second_diff"]["inside_max_mm"][0]]
# 메시 (땅 0): 땅 박힘 · 디딘 발바닥 미끄러짐(루트가 speed로 앞으로 간다고 보고) · 흔드는 발의 가장 낮은 점
src_mesh = bpy.data.objects[MESH]
foot_vs = {}
for s in ("L", "R"):
    gi = [g.index for g in src_mesh.vertex_groups if g.name in ("DEF-foot." + s, "DEF-toe." + s)]
    foot_vs[s] = [v.index for v in src_mesh.data.vertices if sum(e.weight for e in v.groups if e.group in gi) > 0.5]
print("FOOT_VERTS", {s: len(v) for s, v in foot_vs.items()}, [g.name for g in src_mesh.vertex_groups if "foot" in g.name or "toe" in g.name][:8])
lows = []
foot_pos = {"L": [], "R": []}
for i in range(n):
    scene.frame_set(i + 1)
    dg = bpy.context.evaluated_depsgraph_get()
    o = src_mesh.evaluated_get(dg)
    me = o.to_mesh()
    mw = o.matrix_world
    lows.append(min((mw @ v.co).z for v in me.vertices))
    for s in ("L", "R"):
        foot_pos[s].append([mw @ me.vertices[k].co for k in foot_vs[s]])
    o.to_mesh_clear()
summ["mesh_low"] = [round(min(lows), 4), round(max(lows), 4)]
dt = 1.0 / C.FPS
for s in ("L", "R"):
    slips, flat_slips, swing_low, planted, stance_low = [], [], [], 0, []
    for i in range(n - 1):
        A, B = foot_pos[s][i], foot_pos[s][i + 1]
        lowA = min(p.z for p in A)
        u = rows[i]["u_" + s]
        u2 = rows[i + 1]["u_" + s]
        if u < 1.0:
            stance_low.append(lowA)
        if lowA < 0.004:
            planted += 1
            for pa, pb in zip(A, B):
                # 두 프레임 모두 땅에 1.5 mm 안으로 닿아 있는 발바닥 점 (들리기 시작한 점은 뺀다)
                if pa.z < 0.0015 and pb.z < 0.0015:
                    d = Vector((pb.x - pa.x, pb.y - pa.y - S.speed * dt, 0.0))
                    slips.append(d.length / dt)
                    if S.roll_flat <= u and u2 <= S.heel_rise and u2 < 1.0:
                        flat_slips.append(d.length / dt)
        if 1.25 <= u <= 1.75:
            swing_low.append(lowA)
    slips.sort()
    flat_slips.sort()
    summ[s]["mesh_slip_mps"] = [round(slips[len(slips) // 2], 3), round(slips[int(len(slips) * 0.95)], 3), round(slips[-1], 3)] if slips else None
    summ[s]["mesh_slip_flat_mps"] = [round(flat_slips[len(flat_slips) // 2], 4), round(flat_slips[-1], 4)] if flat_slips else None
    summ[s]["mesh_planted_frames"] = planted
    summ[s]["mesh_stance_low_max"] = round(max(stance_low), 4) if stance_low else None   # 디딤 중 발이 가장 많이 뜬 프레임의 발 최저점
    summ[s]["mesh_midswing_low"] = round(min(swing_low), 4) if swing_low else None
    if "debugfoot" in argv:
        print("FOOT_LOW", s, [(rows[i]["f"], round(rows[i]["u_" + s], 2), round(min(p.z for p in foot_pos[s][i]) * 1000, 1)) for i in range(n - 1) if rows[i]["u_" + s] < 1.0])
if "updatefootcomp" in argv:
    acc = {}
    for s in ("L", "R"):
        for i in range(n - 1):
            u = rows[i]["u_" + s]
            if u < 1.0:
                acc.setdefault(round(u, 4), []).append(min(p.z for p in foot_pos[s][i]))
    meas = sorted((u, sum(v) / len(v)) for u, v in acc.items())
    table = [[u, foot_comp(u) + low] for u, low in meas]
    if table[-1][0] < 1.0:      # 디딤 끝(u = 1)은 다음 프레임이 흔들기라 측정에 없을 수 있다 → 마지막 값 유지
        table.append([1.0, table[-1][1]])
    with io.open(FOOTCOMP_PATH, "w", encoding="utf-8") as fh:
        json.dump({"note": "walk_10_make.py가 디딘 발 메시 최저점을 재어 누적한 발 목표 내림(m). 디딤 진행 u 0~1.", "frames": n - 1,
                   "table": [[round(u, 4), round(c, 5)] for u, c in table]}, fh, ensure_ascii=False, indent=1)
    print("FOOTCOMP_SAVED", [(round(u, 2), round(c * 1000, 1)) for u, c in table])
print("SUMMARY", json.dumps(summ, ensure_ascii=False))
with io.open(os.path.join(OUT, "summary.json"), "w", encoding="utf-8") as fh:
    json.dump(summ, fh, ensure_ascii=False, indent=1)
with io.open(os.path.join(OUT, "frames.tsv"), "w", encoding="utf-8") as fh:
    keys = ["f", "hips_z", "u_L", "u_R", "knee_L", "knee_R", "sole_L", "sole_R", "dorsi_L", "dorsi_R", "ankle_z_L", "ankle_z_R", "ball_z_L", "ball_z_R",
            "heel_z_L", "heel_z_R", "tip_z_L", "tip_z_R", "c_L", "c_R"]
    fh.write("\t".join(keys) + "\tmesh_low\n")
    for r, lo in zip(rows, lows):
        fh.write("\t".join(("%.3f" % r[k]) if isinstance(r[k], float) else str(r[k]) for k in keys) + "\t%.4f\n" % lo)


# ---------------------------------------------------------------- 렌더: 게임처럼 앞으로 걸어가게(몸 이동 = SPEED) 해서 발이 땅에 붙는지 본다
def render():
    rootobj = bpy.data.objects.new("WalkRoot", None)
    scene.collection.objects.link(rootobj)
    R.rig.parent = rootobj
    for f in range(1, n + 1):
        rootobj.location = Vector((0.0, -S.speed * (f - 1) / 30.0, 0.0))
        rootobj.keyframe_insert("location", frame=f)
    for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine", "Floor"):
        if o in bpy.data.objects:
            bpy.data.objects[o].hide_render = True
    gp = bpy.data.objects["GroundPlane"]
    gp.hide_render = False
    gp.location.z = -0.005     # 판(두께 1 cm)의 윗면 = 땅 0 (첫 판 0.05는 윗면이 +5.5 cm라 밑창이 묻혀 보였다)
    gp.scale = (1, 3, 1)
    ticks = bpy.data.materials.new("Tick")
    ticks.diffuse_color = (0.1, 0.1, 0.1, 1)
    for k in range(-30, 8):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0.32, k * 0.1, 0.0005))
        c = bpy.context.object
        c.scale = (0.22, 0.004, 0.001)
        c.data.materials.append(ticks)
    import rok_render as RR
    cam = RR.setup(scene, (240, 300))
    frames_dir = os.path.join(OUT, "frames")
    os.makedirs(frames_dir, exist_ok=True)
    for f in os.listdir(frames_dir):
        os.remove(os.path.join(frames_dir, f))
    files = []
    views = [("side", Vector((1, 0, 0)), (0, -0.05, 0.80), 1.95), ("front", Vector((0, -1, 0.12)), (0, 0, 0.80), 1.95),
             ("qf", Vector((-0.8, -0.75, 0.35)), (0, -0.05, 0.80), 2.05), ("feet", Vector((1, 0, 0)), (0, -0.05, 0.26), 0.85)]
    idx = list(range(0, n - 1, 2))
    for v, d, tgt, sc in views:
        for i in idx:
            scene.frame_set(i + 1)
            body = rootobj.location.copy()
            t = body + Vector(tgt)
            cam.location = t + d.normalized() * 6
            cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
            cam.data.ortho_scale = sc
            p = os.path.join(frames_dir, "%s_%03d.png" % (v, i + 1))
            scene.render.filepath = p
            bpy.ops.render.render(write_still=True)
            files.append(("%s f%d %.2fs" % (v, i + 1, i / 30), p))
    sheet = os.path.join(C.PREVIEW, NAME + ".jpg") if not TAG else os.path.join(OUT, NAME + ".jpg")
    tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sheet.py")
    args = ["uv", "run", "--no-project", "-q", "--with", "pillow", "python", "-I", tool, sheet, str(len(idx))] + ["%s=%s" % (l, p) for l, p in files]
    res = subprocess.run(args, capture_output=True, text=True, timeout=300)
    print("SHEET", res.returncode, res.stdout.strip(), res.stderr.strip()[-300:])
    R.rig.parent = None


def render_hands():
    """늘어뜨린 손 확대: 앞 · 왼쪽 · 오른쪽에서 (프레임 1 · 반 주기 · 1/4 주기). 왼손 손바닥 방향을 고를 때 본다."""
    import rok_render as RR
    for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine", "Floor", "GroundPlane"):
        if o in bpy.data.objects:
            bpy.data.objects[o].hide_render = True
    cam = RR.setup(scene, (300, 300))
    d = os.path.join(OUT, "hands")
    os.makedirs(d, exist_ok=True)
    files = []
    for f in (1, n // 4 + 1, n // 2 + 1):
        scene.frame_set(f)
        for v, dv in (("front", Vector((0, -1, 0.1))), ("left", Vector((1, -0.25, 0.1))), ("right", Vector((-1, -0.25, 0.1))),
                      ("back", Vector((0, 1, 0.1)))):
            t = Vector((0, -0.03, 0.93))
            cam.location = t + dv.normalized() * 5
            cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
            cam.data.ortho_scale = 0.8
            p = os.path.join(d, "%s_%02d.png" % (v, f))
            scene.render.filepath = p
            bpy.ops.render.render(write_still=True)
            files.append(("%s f%d" % (v, f), p))
    # 손목 확대 (손 하나씩, 바깥 · 앞에서)
    for f in (1, n // 2 + 1):
        scene.frame_set(f)
        for s, sx in (("L", 1.0), ("R", -1.0)):
            w = R.exp.matrix_world @ R.exp.pose.bones[("Left" if s == "L" else "Right") + "Hand"].head
            for v, dv in (("out", Vector((sx, -0.3, 0.05))), ("fwd", Vector((0.15 * sx, -1, 0.05)))):
                t = w + Vector((0, 0, -0.06))
                cam.location = t + dv.normalized() * 3
                cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
                cam.data.ortho_scale = 0.28
                p = os.path.join(d, "zoom_%s_%s_%02d.png" % (s, v, f))
                scene.render.filepath = p
                bpy.ops.render.render(write_still=True)
                files.append(("%s %s f%d" % (s, v, f), p))
    sheet = os.path.join(OUT, "hands.jpg")
    tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sheet.py")
    args = ["uv", "run", "--no-project", "-q", "--with", "pillow", "python", "-I", tool, sheet, "4"] + ["%s=%s" % (l, p) for l, p in files]
    res = subprocess.run(args, capture_output=True, text=True, timeout=300)
    print("HANDS", res.returncode, res.stdout.strip(), res.stderr.strip()[-300:])


def export():
    act = R.exp.animation_data.action
    act.name = NAME
    bpy.ops.object.select_all(action="DESELECT")
    R.exp.hide_set(False)
    R.exp.hide_viewport = False
    R.exp.select_set(True)
    bpy.context.view_layer.objects.active = R.exp
    scene.frame_start, scene.frame_end = 1, n
    scene.name = NAME
    path = os.path.join(C.EXPORT, NAME + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE"},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False)
    print("EXPORTED", path, os.path.getsize(path))
    # 기준 자세(동작 없음) = 학도병 바인드 자세. Humanoid 아바타용
    R.exp.animation_data.action = None
    for pb in R.exp.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    tp = os.path.join(C.EXPORT, "A_ROK_Student_Rigify_TPose.fbx")
    bpy.ops.export_scene.fbx(
        filepath=tp, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False, bake_anim=False,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP", embed_textures=False)
    print("EXPORTED", tp, os.path.getsize(tp))
    R.exp.animation_data.action = act


if "hands" in argv:
    render_hands()
if "render" in argv:
    render()
if "export" in argv:
    export()
if "blend" in argv:
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)
    path = os.path.join(C.RIGDIR, "rok_rigify_student_walk.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("SAVED", path, os.path.getsize(path))
