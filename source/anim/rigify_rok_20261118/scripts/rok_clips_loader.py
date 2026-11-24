# 2026-11-24 로켓포반 탄약수 (Ally_3) 동작. 같은 리그 · 같은 굽기 · 같은 검사(03_make_clips의 make/summarize)를 쓴다.
# 좌표: 탄약수 루트 = 원점, 앞 -Y, 왼쪽 +X, 위 +Z (참호 바닥 = 0). rot(pitch 앞숙임+, yaw 왼쪽+, roll 왼쪽 기울임+).
#
# 사수(플레이어)와의 자리 (README 3번): 전차 쪽을 같이 보고, 사수의 오른쪽 0.42 m · 뒤 0.80 m에 탄약수 루트.
#  - 사수 눈 = 서기 눈높이 1.60 (PlayerConfig.standEyeHeight).
#  - 발사관 뒤 끝(Rocket_RearMouth): 장전 중에는 조준이 풀려 "허리" 자세(BazookaPresentation.hipViewPosition
#    (0.22, -0.24, 0.34), hipViewEuler (6, -2, 5), 뒤 끝 = 그 틀의 (0, 0.004, -0.582))라 카메라 기준 (0.24, -0.175, -0.238).
#    발사관 축(총구 쪽) = 카메라 기준 (-0.035, -0.104, 0.994) — 6도 숙임 · 2도 왼쪽.
#  - 로켓: SM_WP_BazookaRocket.fbx 실제 길이 0.60 m(M28A2 23.55 in), 몸통 지름 0.089 m. 원점 = 꼬리 끝 축 위, 앞 = 탄두 쪽.
import math
import os

from mathutils import Matrix, Quaternion, Vector

import rok_common as C
import rok_poses
from rok_rig import rot, gun_rot, copy_pose, blend_pose, ease
from rok_timeline import Key, Timeline, breathe, drift, qx, qy, qz
from rok_clips import Clip, with_, moved, hand_world, elbow_world, hand_at_grip, FLIP_LIFT

UP = Vector((0, 0, 1))

class SpaceSafeTimeline(Timeline):
    """rok_timeline.Timeline과 같되, 두 키의 손 목표 공간(문자열)이 다르면 값을 섞지 않고 공간을 고른 쪽 키의 값을 쓴다.
    (공간이 다른 숫자를 섞으면 엉뚱한 자리가 된다. 공간을 바꾸는 키는 같은 월드 자리를 두 공간으로 적은 쌍으로 둔다.)"""
    def sample(self, t):
        out = super().sample(t)
        tau = self._tau(t, self.lags["arms"])
        a, b, u = self._segment(tau)
        if a is not b:
            w = ease(u, b.kinds.get("arms", b.kind))
            for s in ("L", "R"):
                if a.pose["hand_space_" + s] != b.pose["hand_space_" + s]:
                    k = a if w < 0.5 else b
                    out["hand_" + s] = k.pose["hand_" + s]
                    out["hand_space_" + s] = k.pose["hand_space_" + s]
        return out


def tl_sampler(keys, length, lags=None, live=None):
    tl = SpaceSafeTimeline(keys, length, lags)

    def sample(t):
        P = tl.sample(t)
        if live:
            live(P, t)
        return P
    return sample


# ------------------------------------------------------------------ 사수 · 발사관 기하
GUNNER = Vector((0.42, -0.80, 0.0))
EYE = 1.60
ROCKET_LEN = 0.60
ROCKET_R = 0.0445
SEAT = 0.012            # 다 들어갔을 때 꼬리 끝이 뒤 끝보다 안쪽으로 들어간 거리 (BazookaPresentation seated)


def u2b(v):
    """사수 카메라 기준(Unity x 오른쪽, y 위, z 앞) → 탄약수 공간(Blender, 사수도 -Y를 봄)."""
    return Vector((-v[0], -v[2], v[1]))


EYE_P = GUNNER + Vector((0, 0, EYE))
AXIS = u2b((-0.035, -0.104, 0.994)).normalized()             # 발사관 축(총구 쪽)
REAR = EYE_P + u2b((0.24, -0.175, -0.238))                  # 발사관 뒤 끝 (장전 중)
TUBE_UP = (UP - AXIS * UP.dot(AXIS)).normalized()
TUBE_RIGHT = AXIS.cross(TUBE_UP).normalized()                # 사수가 볼 때 오른쪽
LATCH = REAR + AXIS * 0.012 + TUBE_UP * 0.0 + TUBE_RIGHT * 0.052   # 뒤 끝 위 오른쪽 걸쇠(접점 걸쇠) 자리
# 사수 오른 어깨뼈 위 (철모는 손이 닿지 않는다: README 3-3)
TAP = GUNNER + Vector((-0.18, 0.18, 1.30))
HELMET = EYE_P + Vector((0.0, 0.04, 0.07))

# 로켓 세워 둔 자리 (오른 무릎 옆 바닥, 꼬리를 땅에, 탄두 위). 0번을 먼저 집는다.
PILE = [Vector((-0.32, -0.12, 0.0)), Vector((-0.40, -0.05, 0.0)), Vector((-0.46, 0.05, 0.0))]


def rocket_frame(tail, nose_dir, up_hint=(0, 1, 0)):
    """로켓 월드 행렬. 로컬 -Y = 탄두 쪽(모델을 불러온 그대로), +Z = up_hint 쪽."""
    y = -Vector(nose_dir).normalized()
    z = Vector(up_hint)
    z = (z - y * z.dot(y))
    if z.length < 1e-4:
        z = Vector((1, 0, 0)) - y * y.x
    z.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(tail)
    return m


def r_axis(m):
    return -(m.to_3x3() @ Vector((0, 1, 0))).normalized()


def r_point(m, d):
    """꼬리에서 탄두 쪽으로 d m 축 위의 점."""
    return m.translation + r_axis(m) * d


def pile_frame(i):
    return rocket_frame(PILE[i], (0.0, -0.10, 1.0), (0, -1, 0))


def inserting_frame(s):
    """s: 0 = 탄두가 뒤 끝에 닿음, 1 = 다 들어감."""
    d = -ROCKET_LEN + s * (ROCKET_LEN + SEAT)
    return rocket_frame(REAR + AXIS * d, AXIS, TUBE_UP)


# ------------------------------------------------------------------ 손 도우미
def grip_on(R, P, side, m, d, back, fingers_hint, space="world", palm=0.02, along=0.062, arm=False):
    """로켓 m의 꼬리에서 d m 지점을 back 쪽(손등이 향하는 쪽, 축에 수직으로 맞춤)에서 쥔다.
    arm=True (11-24 관절 검사): 손가락 방향을 어깨 → 쥐는 자리 방향(축에 수직 성분)에 맞추고 손등은 그에 수직인 쪽 중
    back에 가까운 쪽으로 — 손목이 아래팔과 거의 일직선이 되게(손목이 90도 넘게 꺾이던 쥐기 수정)."""
    a = r_axis(m)
    if arm:
        def arm_frame(origin):
            v = r_point(m, d) - origin
            fd = (v - a * v.dot(a)).normalized()
            b = fd.cross(a).normalized()
            if b.dot(Vector(back)) < 0:
                b = -b
            f = a.cross(b).normalized()
            if f.dot(fd) < 0:
                f = -f
            return b, f
        R.apply(P)
        b, f = arm_frame(R.pb["DEF-upper_arm." + side].head)
        # 한 번 놓아 본 뒤 실제 팔꿈치에서 다시 (손 방향을 아래팔에 맞춤)
        surface = r_point(m, d) + b * ROCKET_R
        Q = hand_world(R, P, side, surface + b * palm - f * along, f, b, space)
        R.apply(Q)
        b, f = arm_frame(R.pb["DEF-forearm." + side].head)
    else:
        b = Vector(back)
        b = (b - a * b.dot(a)).normalized()
        f = Vector(fingers_hint)
        f = (f - a * f.dot(a) - b * f.dot(b))
        if f.length < 1e-4:
            f = a.cross(b)
        f.normalize()
    surface = r_point(m, d) + b * ROCKET_R
    wrist = surface + b * palm - f * along
    return hand_world(R, P, side, wrist, f, b, space)


def flat_hand(R, P, side, point, normal, fingers, space="world"):
    """손바닥을 point(면 위의 점)에 대고 손등이 normal 쪽 — 두드리기 · 손짓."""
    n = Vector(normal).normalized()
    f = Vector(fingers)
    f = (f - n * f.dot(n)).normalized()
    wrist = Vector(point) + n * 0.022 - f * 0.065
    return hand_world(R, P, side, wrist, f, n, space)


def look_at_rot(R, P, target, pitch_bias=0.0, share=1.0):
    """머리가 target을 보게 (몸 기준이 아니라 루트 기준 절대 회전)."""
    R.apply(P)
    eye = R.pb["DEF-spine.006"].head + Vector((0, -0.08, 0.10))
    d = Vector(target) - eye
    yaw = math.degrees(math.atan2(d.x, -d.y))          # 왼쪽 +
    pitch = -math.degrees(math.atan2(d.z, math.hypot(d.x, d.y)))   # 아래 +
    return rot(pitch=pitch * share + pitch_bias, yaw=yaw * share)


def rifle_on_back(R, P):
    """소총을 등에: 총구가 오른 어깨 위로 비스듬히, 가늠자는 등 바깥쪽. 가슴 공간 값으로 돌려준다."""
    Q = copy_pose(P)
    R.apply(Q)
    ch = R.pb["DEF-spine.003"]
    m = ch.matrix
    back = (m.to_3x3() @ Vector((0, 0, 1))).normalized()     # DEF-spine.003의 Z = 몸 뒤쪽인지 확인 필요 → 아래에서 보정
    # 몸 앞(-Y) 기준으로 뒤쪽 = 가슴 회전을 적용한 +Y
    dq = R.delta("DEF-spine.003")
    back = (dq @ Vector((0, 1, 0))).normalized()
    up = (dq @ UP).normalized()
    left = (dq @ Vector((1, 0, 0))).normalized()
    centre = ch.head + up * 0.10 + back * 0.175
    muzzle = (up * 0.88 - left * 0.42 + back * 0.12).normalized()
    q = gun_rot(muzzle, back)
    Q["rifle"] = (centre - q @ C.gun(0, 0, 0.17), q)
    Q["rifle_space"] = 1.0
    Q["grip_L"], Q["grip_R"] = 0.0, 0.0
    return rok_poses.chestify(R, Q, keys=("rifle",))


# ------------------------------------------------------------------ 자세
KCY = float(os.environ.get("LOADER_KCY", "-8"))
HOLD_UP = float(os.environ.get("LOADER_HOLD_UP", "0.12"))


def kneel_pose(R, LIB):
    """무릎 대기: 무릎 사격과 같은 다리(오른 무릎 · 발등이 땅, 왼발 앞), 상체를 세우고 로켓을 왼 허벅지에 세워 두 손으로."""
    W = LIB["kneel_watch"]
    P = copy_pose(W)
    P["chest_rot"] = rot(pitch=9, yaw=KCY, roll=-1)      # 11-24: 6 → -8 (골반 대비 가슴 비틀림 47도 → 약 33도)
    P["head_rot"] = rot(pitch=3, yaw=22, roll=0)
    P["neck_share"] = 0.45
    P["shoulder_L"], P["shoulder_R"] = Quaternion(), Quaternion()
    P = rifle_on_back(R, P)
    m = hold_frame(R, P)
    P = grip_on(R, P, "R", m, 0.13, (-1.0, 0.25, 0.0), (0.0, -1.0, 0.0))
    P = grip_on(R, P, "L", m, 0.40, (1.0, 0.10, 0.0), (0.0, -1.0, 0.0))
    P["fingers_L"], P["fingers_R"], P["thumb_L"], P["thumb_R"], P["index_R"] = 0.85, 0.85, 0.55, 0.55, 0.8
    R.apply(P)
    shL, shR = R.pb["DEF-upper_arm.L"].head.copy(), R.pb["DEF-upper_arm.R"].head.copy()
    P = elbow_world(R, P, "L", shL + Vector((0.40, 0.15, -0.45)))
    P = elbow_world(R, P, "R", shR + Vector((-0.40, 0.20, -0.45)))
    return P


def hold_frame(R, P):
    """무릎 대기의 로켓: 꼬리를 왼 허벅지 위(무릎 쪽)에 세우고 탄두는 위 · 조금 앞."""
    R.apply(P)
    hipL = R.pb["DEF-thigh.L"].head.copy()
    kneeL = R.pb["DEF-shin.L"].head.copy()
    top = hipL.lerp(kneeL, 0.62) + Vector((-0.035, 0.0, HOLD_UP))   # 11-24: 0.085 → 0.12 (꼬리가 허벅지 속 4 cm 들어가던 것)
    return rocket_frame(top, (0.12, -0.28, 0.95), (0, -1, 0))


def crouch_stand(R, LIB, pelvis, prot, crot):
    """반쯤 일어선 자세: 왼발은 무릎 대기 자리 그대로(앞), 오른발은 뒤에 발바닥 전체."""
    W = LIB["kneel_watch"]
    stand = LIB["stand"]
    P = copy_pose(W)
    aR = stand["foot_R"][0] + Vector((-0.03, 0.08, 0.0))
    P["foot_R"] = (aR, stand["foot_R"][1])
    P["toe_R"] = stand["toe_R"]
    P["knee_R"] = aR + Vector((-0.15, -1.6, 0.9))
    P["knee_L"] = W["foot_L"][0] + Vector((0.25, -1.6, 0.9))
    P["pelvis_loc"] = Vector(pelvis)
    P["pelvis_rot"] = prot
    P["chest_rot"] = crot
    P["neck_share"] = 0.5
    P = rifle_on_back(R, P)
    return P


import os
# 장전 때 골반 높이 (11-24 코디네이터: 흉벽 1.2 m 위로 상체가 드러나지 않게 낮춤). 환경 변수로 바꿔 시험할 수 있다
PZ0 = float(os.environ.get("LOADER_PZ0", "0.64"))
PZ1 = float(os.environ.get("LOADER_PZ1", "0.62"))
CP0 = float(os.environ.get("LOADER_CP0", "15"))
CP1 = float(os.environ.get("LOADER_CP1", "28"))
LD0 = float(os.environ.get("LOADER_LD0", "0.30"))
RD0 = float(os.environ.get("LOADER_RD0", "0.45"))   # 11-24: 오른손이 받치는 자리(꼬리에서) 0.14 → 0.45 (얼굴 앞으로)
CY0 = float(os.environ.get("LOADER_CY0", "70"))      # 11-24: 넣을 때 가슴 왼쪽 돌림 40 → 58 (오른손이 앞쪽 몸통에 닿게)
CY1 = float(os.environ.get("LOADER_CY1", "50"))
ELB_L = tuple(float(x) for x in os.environ.get("LOADER_ELBL", "0.40,-0.05,-0.20").split(","))
CR1 = float(os.environ.get("LOADER_CR1", "-12"))
S1Y = float(os.environ.get("LOADER_S1Y", "-0.23"))    # 11-24: 밀어 넣은 끝 골반 앞뒤 -0.17 → (왼손이 뒤 끝까지 닿게 몸이 더 앞으로)
# 11-24 관절 검사: 넣는 동안 머리가 로켓 · 팔 밑에 들어가지 않게 (머리를 오른쪽으로 기울이고 덜 올려다봄), 뒷발 뒤꿈치를 듦
HS = float(os.environ.get("LOADER_HS", "0.3"))       # 뒤 끝을 보는 정도 (원래 0.85)
HP = float(os.environ.get("LOADER_HP", "15"))         # 머리 숙임 더하기(도)
HR = float(os.environ.get("LOADER_HR", "-22"))        # 머리 오른쪽으로 기울임(도)
PX = float(os.environ.get("LOADER_PX", "0.0"))      # 골반 오른쪽으로(m)
HEEL = float(os.environ.get("LOADER_HEEL", "28"))
TAPR = tuple(float(x) for x in os.environ.get("LOADER_TAPR", "0.02,-0.30,-0.12").split(","))     # 뒷발(오른발) 뒤꿈치 들기(도) — 발목 발등 굽힘 52도 → 한계 안으로


def heel_up(R, foot, toe, deg):
    """발가락 뿌리를 땅에 둔 채 뒤꿈치를 deg만큼 든다. 발가락은 땅에 평평하게 둔다."""
    ankle, q = foot
    v0 = R.ankle0["R"] - R.ball0["R"]
    ball = ankle - q @ v0
    lat = (q @ Vector((1, 0, 0))).normalized()
    q2 = Quaternion(lat, math.radians(deg)) @ q
    return (ball + q2 @ v0, q2), q          # 발가락은 원래(평평한) 발 회전 그대로


def tail_push(R, P, m, back_off=0.004, side="L"):
    """손바닥을 로켓 꼬리 끝에 대고 축 방향으로 민다 (손등은 뒤, 손가락은 위).
    11-24 관절 검사: 오른손이 꼬리 옆을 쥐면 발사관 뒤 끝이 탄약수 머리 바로 위라 오른팔이 머리 위를 가로질러
    아래팔이 머리를 7~13 cm 뚫었다. 꼬리 바로 아래에 있는 왼 어깨에서 왼손으로 꼬리를 밀고(팔이 머리 왼쪽으로 곧게 올라감),
    오른손은 앞쪽 몸통을 받친다."""
    a = r_axis(m)
    up = (UP - a * UP.dot(a)).normalized()
    side_dir = TUBE_RIGHT * (0.3 if side == "R" else -0.3)
    Q = flat_hand(R, P, side, m.translation - a * back_off, -a, up * 0.9 + side_dir)
    Q["fingers_" + side], Q["thumb_" + side] = 0.35, 0.25
    if side == "R":
        Q["index_R"] = 0.3
    return Q


WAY_T = 0.52
UPD = float(os.environ.get("LOADER_UPD", "0.12"))
UPL = float(os.environ.get("LOADER_UPL", "0.04"))
MID3_T = float(os.environ.get("LOADER_MID3T", "0.70"))   # 이때 로켓이 뒤 끝 자리 · 오른손이 앞 몸통 → 0.80까지 왼손을 꼬리 뒤로
WAY_TAIL = Vector(tuple(float(x) for x in os.environ.get("LOADER_WAY", "0.28,-0.24,1.02").split(",")))
WAY_NOSE = Vector((0.0, -0.85, 0.52))


def way_frame():
    """허벅지에서 뒤 끝으로 올리는 길의 중간: 얼굴 앞 왼쪽 (머리 위로 휘두르지 않게)."""
    return rocket_frame(WAY_TAIL, WAY_NOSE, (0, 1, 0))


def mid2_frame():
    return lerp_frame(way_frame(), inserting_frame(0.0), float(os.environ.get("LOADER_M2W", "0.75")))


def head_away(R, P, target):
    """뒤 끝 쪽을 조금만 보고, 고개를 숙여 오른쪽으로 기울인다(로켓 · 팔이 머리 위를 지나도록)."""
    q = look_at_rot(R, P, target, share=HS)
    return q @ rot(pitch=HP, roll=HR)


def make_poses(R, LIB):
    K = kneel_pose(R, LIB)
    # 일어서서 로켓을 뒤 끝에 맞춘 순간 (탄두가 뒤 끝)
    S0 = crouch_stand(R, LIB, (0.02 + PX, -0.08, PZ0), rot(pitch=10, yaw=24), rot(pitch=CP0, yaw=CY0, roll=-3))
    S0["foot_R"], S0["toe_R"] = heel_up(R, S0["foot_R"], S0["toe_R"], HEEL)
    S0["head_rot"] = head_away(R, S0, REAR + AXIS * 0.1)
    m0 = inserting_frame(0.0)
    S0 = tail_push(R, S0, m0)                                                            # 왼손: 꼬리 끝을 뒤에서 밂
    S0 = grip_on(R, S0, "R", m0, RD0, (-1.0, 0.0, -0.6), (0.0, 0.0, 1.0), arm=True)      # 오른손: 앞쪽 몸통을 오른쪽 아래에서 받침
    # 밀어 넣은 끝 (오른손이 꼬리를 뒤 끝까지 밀었음, 몸이 앞으로)
    S1 = crouch_stand(R, LIB, (0.03 + PX, S1Y, PZ1), rot(pitch=14, yaw=22), rot(pitch=CP1, yaw=CY1, roll=CR1))
    a1, q1 = S1["foot_R"]
    S1["foot_R"] = (a1 + Vector((0.0, -0.10, 0.0)), q1)        # 밀면서 오른발을 반걸음 끌어 디딤
    S1["knee_R"] = S1["knee_R"] + Vector((0.0, -0.10, 0.0))
    S1["foot_R"], S1["toe_R"] = heel_up(R, S1["foot_R"], S1["toe_R"], HEEL)
    for P in (S0, S1):      # 11-24: 왼 팔꿈치를 바깥 아래로 (위로 민 왼팔의 위팔 바깥 돌림 135도 → 줄임)
        R.apply(P)
        P.update(elbow_world(R, P, "L", R.pb["DEF-upper_arm.L"].head + Vector(ELB_L)))
    S1["head_rot"] = head_away(R, S1, REAR)
    m1 = inserting_frame(1.0)
    S1 = tail_push(R, S1, m1, back_off=0.004 - SEAT)        # 다 들어감: 왼손바닥이 뒤 끝에
    S0["fingers_R"], S0["thumb_R"], S0["index_R"] = 0.7, 0.45, 0.65
    return {"K": K, "S0": S0, "S1": S1}


def finish_arms(R, P, elbows=((0.45, 0.10, -0.40), (-0.45, 0.15, -0.40))):
    R.apply(P)
    P = elbow_world(R, P, "L", R.pb["DEF-upper_arm.L"].head + Vector(elbows[0]))
    P = elbow_world(R, P, "R", R.pb["DEF-upper_arm.R"].head + Vector(elbows[1]))
    return P


# ------------------------------------------------------------------ 소품(로켓) 궤적
class Prop:
    """프레임마다의 로켓: (행렬, 상태). 상태: hand_R / hand_L / hands / tube / pile / hidden. 손 접촉 0~1."""
    def __init__(self, fn):
        self.fn = fn

    def __call__(self, t):
        return self.fn(t)


def lerp_frame(a, b, w):
    qa, qb = a.to_quaternion(), b.to_quaternion()
    m = qa.slerp(qb, w).to_matrix().to_4x4()
    m.translation = a.translation.lerp(b.translation, w)
    return m


# ------------------------------------------------------------------ 클립
DEBUG = {}


def add(R, LIB, clips):
    import rok_poses as RP
    tuck = RP.TUCK
    L = make_poses(R, LIB)
    K, S0, S1 = L["K"], L["S0"], L["S1"]
    stand = LIB["stand"]
    new = []
    mK = hold_frame(R, K)

    def reg(name, length, sample, prop, loop=False, note="", start_ref=None, end_ref=None, step=0.1, cam=None, views=None):
        c = Clip(name, length, sample, loop=loop, start_ref=start_ref, end_ref=end_ref, note=note)
        c.new, c.ground, c.mesh_check, c.step = True, True, True, step
        c.views = views or ["sideR", "qf", "top", "eye"]
        c.cam = cam or ((0.1, -0.30, 0.85), 2.3)
        c.prop = prop
        c.loader = True
        c.key_times = []
        clips[name] = c
        new.append(name)
        return c

    # ============================================================ 1. 무릎 대기 (반복 4초): 로켓을 든 채 숨 · 사수 쪽을 봄
    TI = 4.0

    def idle(t):
        P = copy_pose(K)
        breathe(P, t, period=TI / 2, amount=0.9)
        drift(P, t, period=TI, sway=0.008, head_yaw=5.0)
        g = math.sin(math.pi * min(1.0, max(0.0, (t - 2.6) / 0.7))) ** 2
        P["fingers_L"] = K["fingers_L"] - 0.2 * g
        return P

    reg("A_ROK_Loader_Kneel_Idle", TI, idle, Prop(lambda t: (mK, "hands", 1.0, 1.0)), loop=True, start_ref="self",
        note="무릎 꿇고 로켓을 왼 허벅지에 세워 두 손으로 든 채 대기", step=0.5)

    # ============================================================ 2. 장전 (2.2초 = bazookaReloadTime)
    # 0.00 발사 직후(고개를 돌린 채) → 0.30 일어나기 시작 → 0.80 탄두가 뒤 끝 → 1.32 다 밀어 넣음(왼손은 0.98에 놓음)
    # → 1.55 걸쇠 누름 → 1.78 · 1.92 사수 어깨 두 번 두드림 → 2.20 옆으로 내려앉아 고개 숙임(후폭풍 밖)
    TL = 2.2
    DOWN_LIFT = float(os.environ.get("LOADER_DOWN_LIFT", "0.18"))   # 11-24: 서 있던 오른발을 발등으로 뒤집으며 내려앉을 때 발끝이 땅을 파고들던 것(−6 cm)
    R.apply(K)
    flinch = moved(K, chest_rot=qx(10) @ qz(-6), head_rot=qx(22) @ qz(-38), pelvis=(-0.01, 0.01, -0.02))
    up_mid = blend_pose(K, S0, 0.5)
    up_mid = with_(up_mid, foot_R=tuck["foot_R"], toe_R=tuck["toe_R"], pelvis_loc=Vector((0.02, -0.02, PZ0 - 0.10)))
    m_half = way_frame()
    up_mid = grip_on(R, up_mid, "L", m_half, UPL, (0.6, 0.3, -0.6), (1.0, -0.2, 0.3), arm=True)   # 왼손: 꼬리 가까이를 쥔 채 올림 → 0.80에 꼬리 뒤로 (11-24)
    up_mid = grip_on(R, up_mid, "R", m_half, UPD, (-1.0, 0.0, -0.6), (1.0, -0.2, 0.3), arm=True)
    # 11-24: 한 번에 한 손씩 옮겨 쥔다 — 0.52~0.66 왼손이 꼬리 쪽을 쥔 채 오른손이 앞 몸통으로, 0.66~0.80 오른손이 쥔 채 왼손이 꼬리 뒤로
    m2 = mid2_frame()
    mid2 = blend_pose(up_mid, S0, 0.5)
    for k in ("pelvis_loc", "pelvis_rot", "foot_L", "foot_R", "toe_L", "toe_R", "knee_L", "knee_R"):
        mid2[k] = S0[k]          # 다리 · 골반은 0.66에 이미 넣을 자세 (0.70 키와 사이에서 다리가 한 프레임에 튀던 것)
    mid2 = grip_on(R, mid2, "L", m2, UPL, (0.6, 0.3, -0.6), (1.0, -0.2, 0.3), arm=True)
    mid2 = grip_on(R, mid2, "R", m2, float(os.environ.get("LOADER_M2D", "0.30")), (-1.0, 0.0, -0.6), (0.0, 0.0, 1.0), arm=True)
    # 0.74: 로켓이 이미 뒤 끝 자리 — 왼손은 아직 꼬리 쪽을 쥐고, 오른손이 앞 몸통(0.45)을 쥠
    mid3 = copy_pose(S0)        # 몸은 이미 넣을 자세 (팔이 다 펴진 채 손이 닿는 끝에 걸려 팔꿈치가 한 프레임에 튀던 것)
    m0 = inserting_frame(0.0)
    mid3 = grip_on(R, mid3, "L", m0, UPL, (0.6, 0.3, -0.6), (1.0, -0.2, 0.3), arm=True)
    mid3 = grip_on(R, mid3, "R", m0, RD0, (-1.0, 0.0, -0.6), (0.0, 0.0, 1.0), arm=True)
    S_mid = blend_pose(S0, S1, 0.33)
    S_mid["foot_R"], S_mid["toe_R"], S_mid["knee_R"] = S0["foot_R"], S0["toe_R"], S0["knee_R"]   # 디딘 발은 그대로 (11-24)
    m_mid = inserting_frame(0.33)
    S_mid = tail_push(R, S_mid, m_mid)
    S_mid = grip_on(R, S_mid, "R", m_mid, RD0 - 0.33 * (ROCKET_LEN + SEAT), (-1.0, 0.0, -0.6), (0.0, 0.0, 1.0), arm=True)   # 로켓이 오른손 위로 미끄러져 들어감
    # 왼손을 놓은 자리: 뒤 끝 왼쪽 아래 (가운데 쯤)
    # 오른손을 놓은 자리: 오른 어깨 앞 가슴 높이 (11-24: 손을 바꿔 오른손이 받치다 놓음. 뒤 끝 둘레는 머리 바로 위라 비워 둠)
    R.apply(S1)
    S1 = flat_hand(R, S1, "R", R.pb["DEF-upper_arm.R"].head + Vector(TAPR), (-0.9, 0.2, 0.3), (0.2, -0.7, -0.6))
    S1["fingers_R"], S1["thumb_R"], S1["index_R"] = 0.4, 0.3, 0.4
    latch = copy_pose(S1)
    latch = moved(latch, chest_rot=qx(7) @ qz(-8), pelvis=(0.0, 0.0, 0.035))     # 11-24: 골반을 앞으로 더 내밀지 않음(무게중심이 앞발 밖으로)
    # 11-24: 걸쇠는 뒤 끝에 있던 왼손이 그대로 옮겨 누름 (오른손으로 누르면 아래팔이 머리 위를 지나 머리를 뚫음)
    latch = flat_hand(R, latch, "L", LATCH + TUBE_UP * 0.03, TUBE_UP * 0.75 + TUBE_RIGHT * 0.45 - AXIS * 0.2, AXIS * 0.85 - TUBE_RIGHT * 0.3)
    R.apply(latch)
    latch = flat_hand(R, latch, "R", R.pb["DEF-upper_arm.R"].head + Vector(TAPR), (-0.9, 0.2, 0.3), (0.2, -0.7, -0.6))
    latch["fingers_L"], latch["thumb_L"] = 0.45, 0.15
    press = copy_pose(latch)
    press["hand_L"] = (press["hand_L"][0] - TUBE_UP * 0.012, press["hand_L"][1])
    tap_out = moved(latch, chest_rot=qx(5) @ qz(-4), head_rot=qz(-10), pelvis=(0.0, 0.02, 0.0))
    tap_out = flat_hand(R, tap_out, "L", TAP + Vector((0.0, 0.035, 0.03)), (0.30, 0.70, 0.40), (-0.2, -0.45, 0.9))
    # 11-24: 오른손을 머리 위(뒤 끝 옆)에 두면 손 · 아래팔이 머리를 뚫어 → 오른 어깨 앞 가슴 높이로 내려 둠
    R.apply(tap_out)
    tap_out = flat_hand(R, tap_out, "R", R.pb["DEF-upper_arm.R"].head + Vector(TAPR), (-0.9, 0.2, 0.3), (0.2, -0.7, -0.6))
    tap_out["fingers_L"], tap_out["thumb_L"] = 0.2, 0.1
    tap_out["fingers_R"], tap_out["thumb_R"] = 0.4, 0.3
    tap_in = copy_pose(tap_out)
    tap_in = flat_hand(R, tap_in, "L", TAP, (0.30, 0.70, 0.40), (-0.2, -0.45, 0.9))
    tap_in["fingers_L"], tap_in["thumb_L"] = 0.2, 0.1
    # 내려앉은 끝: 무릎 대기 다리, 고개를 숙이고 오른쪽으로 돌림, 오른손은 세워 둔 로켓 쪽, 왼손은 왼 허벅지 위
    down = moved(K, chest_rot=qy(-12) @ qx(18) @ qz(-16), head_rot=qx(26) @ qz(-30), pelvis=(-0.015, 0.0, -0.005))
    down = with_(down, rifle=K["rifle"])
    R.apply(down)
    kneeL = R.pb["DEF-shin.L"].head.copy()
    hipL = R.pb["DEF-thigh.L"].head.copy()
    th = (kneeL - hipL).normalized()
    down = flat_hand(R, down, "L", hipL.lerp(kneeL, 0.72) + Vector((0, 0, 0.095)), (0.15, 0.0, 1.0), th)   # 11-24: 0.075 → 0.095 (아래팔이 허벅지에 3 cm 묻히던 것)
    reach0 = pile_frame(0)
    down = grip_on(R, down, "R", reach0, 0.48, (-0.6, 0.6, 0.3), (1.0, -0.2, 0.0), arm=True)
    down["fingers_R"], down["thumb_R"] = 0.35, 0.25
    down["fingers_L"], down["thumb_L"] = 0.35, 0.2
    DEBUG.update(K=K, up_mid=up_mid, mid2=mid2, mid3=mid3, S0=S0, S_mid=S_mid, S1=S1, latch=latch, tap_in=tap_in, down=down, flinch=flinch)
    load_keys = [
        Key(0.0, K),
        Key(0.22, flinch, "out"),
        Key(0.52, up_mid, "inout", lift_R=FLIP_LIFT),
        Key(0.66, mid2, "inout", lift_R=0.05),
        Key(MID3_T, mid3, "inout"),
        Key(0.80, S0, "inout"),
        Key(0.98, S_mid, "linear"),
        Key(1.32, S1, "out", lift_R=0.07, groups={"base": "inout"}),     # 11-24: 반걸음 디딜 때 발끝이 끌리던 것(0.04 → 0.07, 발은 천천히 출발)
        Key(1.48, latch, "inout"),
        Key(1.56, press, "out"),
        Key(1.70, tap_out, "inout"),
        Key(1.78, tap_in, "in"),
        Key(1.85, tap_out, "out"),
        Key(1.92, tap_in, "in"),
        Key(2.20, down, "out", lift_R=DOWN_LIFT, groups={"base": "inout", "arms": "inout", "chest": "inout"}),   # 11-24: 발 · 팔이 최고 속도로 순간 출발하던 것(발이 땅을 끌고 손이 한 프레임에 17 cm 튐)
    ]
    def insert_s(t):
        """넣은 정도 0~1 (0.80~1.32초). 11-24: 처음 · 끝 속도가 0이 되게 (원래 선형 65% 섞음 → 0.80에 순간 출발)."""
        return ease((t - 0.80) / 0.52, "inout")

    def push_live(P, t):
        """11-24: 0.80~1.32초 왼손바닥을 그 프레임 로켓 꼬리에 붙인다 (키 사이 보간으로 손이 꼬리를 4 cm 파고들던 것)."""
        tau = t - 0.02          # 팔 묶음 지연
        if 0.80 <= tau <= 1.32:
            sv = insert_s(tau)
            Q = tail_push(R, P, inserting_frame(sv), back_off=0.004 - SEAT * sv)
            P["hand_L"], P["hand_space_L"] = Q["hand_L"], Q["hand_space_L"]

    tl = tl_sampler(load_keys, TL, lags={"chest": 0.03, "head": 0.06, "arms": 0.02}, live=push_live)

    def load_prop(t):
        if t < 0.30:
            return mK, "hands", 1.0, 1.0
        if t < WAY_T:     # 11-24: 얼굴 앞 왼쪽을 거쳐 (머리 위로 휘두르지 않게)
            w = ease((t - 0.30) / (WAY_T - 0.30), "in")
            return lerp_frame(mK, way_frame(), w), "hands", 1.0, 1.0
        if t < 0.66:      # 11-24: 왼손만 쥠 (오른손이 앞 몸통으로 옮겨 쥠)
            w = ease((t - WAY_T) / (0.66 - WAY_T), "inout")
            return lerp_frame(way_frame(), mid2_frame(), w), "hand_L", 1.0, 0.0
        if t < MID3_T:
            w = ease((t - 0.66) / (MID3_T - 0.66), "inout")
            return lerp_frame(mid2_frame(), inserting_frame(0.0), w), "hand_L", 1.0, 0.0
        if t < 0.80:      # 11-24: 오른손만 쥠 (왼손이 꼬리 뒤로 옮김)
            return inserting_frame(0.0), "hand_R", 0.0, 1.0
        if t < 1.32:
            s = insert_s(t)
            gr = 1.0 if t < 0.95 else max(0.0, 1.0 - (t - 0.95) / 0.06)     # 11-24: 손을 바꿈 — 오른손이 받치다 놓고 왼손이 꼬리를 밂
            return inserting_frame(s), "tube", 1.0, gr
        if t < 1.40:
            return inserting_frame(1.0), "tube", max(0.0, 1.0 - (t - 1.32) / 0.08), 0.0
        return inserting_frame(1.0), "hidden", 0.0, 0.0

    reg("A_ROK_Loader_Load", TL, tl, Prop(load_prop), start_ref="A_ROK_Loader_Kneel_Idle",
        note="발사 직후 일어나 로켓을 발사관 뒤로 밀어 넣고 걸쇠를 누른 뒤 사수 어깨를 두 번 두드리고 옆으로 내려앉음")

    # ============================================================ 3. 다음 로켓 집기 (0.9초): 장전 끝 → 무릎 대기
    TR = 0.9
    lift = copy_pose(down)
    mL = lerp_frame(reach0, mK, 0.45)
    mL.translation = mL.translation + Vector((0, 0, 0.10))
    lift = moved(lift, chest_rot=qx(-8) @ qz(8), head_rot=qx(-14) @ qz(22))
    lift = grip_on(R, lift, "R", mL, 0.25, (-1.0, 0.3, 0.0), (0.2, -1.0, 0.0), arm=True)
    lift["fingers_R"], lift["thumb_R"] = 0.85, 0.55
    grab = copy_pose(down)
    grab = grip_on(R, grab, "R", reach0, 0.46, (-0.7, 0.6, 0.0), (0.6, -0.7, 0.0), arm=True)
    grab["fingers_R"], grab["thumb_R"] = 0.85, 0.55
    rk = [Key(0.0, down), Key(0.18, grab, "out"), Key(0.55, lift, "inout"), Key(0.9, K, "soft")]

    def reach_prop(t):
        if t < 0.18:
            return reach0, "pile", 0.0, 0.0
        if t < 0.55:
            return lerp_frame(reach0, mL, ease((t - 0.18) / 0.37, "inout")), "hand_R", 0.0, 1.0
        w = ease((t - 0.55) / 0.35, "inout")
        return lerp_frame(mL, mK, w), "hands" if t > 0.75 else "hand_R", 1.0 if t > 0.75 else 0.0, 1.0

    reg("A_ROK_Loader_Reach", TR, tl_sampler(rk, TR, lags={"chest": 0.03, "head": 0.06, "arms": 0.0}), Prop(reach_prop),
        start_ref="A_ROK_Loader_Load", end_ref="A_ROK_Loader_Kneel_Idle", note="옆에 세워 둔 로켓을 하나 집어 무릎 대기로")

    # ============================================================ 4. 발사 순간 웅크림 (1.2초)
    TB = 1.2
    mB = mK.copy()
    duck = moved(K, chest_rot=qx(25) @ qz(-3), head_rot=qx(32) @ qz(-49), pelvis=(-0.01, 0.025, -0.02))   # 11-24: 가슴 오른쪽 돌림 10 → 3 (오른팔이 몸 앞을 46도 넘게 가로지르던 것), 고개로 더 돌림
    R.apply(duck)
    mB = hold_frame(R, duck)
    mB = rocket_frame(mB.translation + Vector((0.05, -0.06, -0.02)), (0.30, -0.45, 0.84), (0, -1, 0))   # 숙인 머리에서 탄두를 떼어 놓음
    duck = grip_on(R, duck, "R", mB, 0.13, (-1.0, 0.25, 0.0), (0.0, -1.0, 0.0))
    R.apply(duck)
    duck = elbow_world(R, duck, "R", R.pb["DEF-upper_arm.R"].head + Vector((-0.50, 0.05, -0.35)))   # 11-24: 오른 팔꿈치를 바깥으로 (아래팔이 배에 묻히던 것)
    duck = grip_on(R, duck, "L", mB, 0.40, (1.0, 0.10, 0.0), (0.0, -1.0, 0.0))
    bk = [Key(0.0, K), Key(0.16, duck, "out"), Key(0.70, duck, "linear"), Key(1.2, K, "soft")]

    def brace_prop(t):
        w = ease(t / 0.16, "out") if t < 0.16 else (1.0 if t < 0.70 else 1.0 - ease((t - 0.70) / 0.5, "inout"))
        return lerp_frame(mK, mB, w), "hands", 1.0, 1.0

    reg("A_ROK_Loader_Brace", TB, tl_sampler(bk, TB, lags={"chest": 0.02, "head": 0.03, "arms": 0.02}), Prop(brace_prop),
        start_ref="A_ROK_Loader_Kneel_Idle", end_ref="A_ROK_Loader_Kneel_Idle",
        note="사수가 쏘는 순간 몸을 웅크리고 얼굴을 후폭풍 반대쪽으로 돌림")

    # ============================================================ 5. 손짓 (2.4초): 참호를 따라 오는 사수에게 "이리 와"
    TW = 2.4
    turn = moved(K, chest_rot=qz(28) @ qx(-2), head_rot=qz(64) @ qx(-6), pelvis_rot=qz(16))
    R.apply(turn)
    mW = hold_frame(R, turn)
    turn = grip_on(R, turn, "R", mW, 0.13, (-1.0, 0.25, 0.0), (0.0, -1.0, 0.0))
    shL = R.pb["DEF-upper_arm.L"].head.copy()
    dq = R.delta("DEF-spine.003")
    side = (dq @ Vector((1, 0.15, 0))).normalized()
    fwd = (dq @ Vector((0, -1, 0))).normalized()
    wave_out = flat_hand(R, turn, "L", shL + side * 0.27 + fwd * 0.10 + Vector((0, 0, 0.10)), (dq @ Vector((0.2, 1.0, 0.0))), (0.0, 0.0, 1.0))
    wave_in = flat_hand(R, turn, "L", shL + side * 0.10 + fwd * 0.10 + Vector((0, 0, 0.20)), (dq @ Vector((0.8, 0.4, 0.3))), (-0.2, -0.3, 1.0))
    for P in (wave_out, wave_in):
        P["fingers_L"], P["thumb_L"] = 0.15, 0.1
        R.apply(P)
        P = elbow_world(R, P, "L", R.pb["DEF-upper_arm.L"].head + side * 0.35 + Vector((0, -0.05, -0.30)))
    wave_in["fingers_L"] = 0.55
    wk = [Key(0.0, K), Key(0.15, K, "linear"), Key(0.40, wave_out, "inout"), Key(0.72, wave_in, "inout"), Key(1.02, wave_out, "inout"),
          Key(1.34, wave_in, "inout"), Key(1.62, wave_out, "inout"), Key(2.4, K, "soft")]

    def wave_prop(t):
        if t < 0.15:
            return mK, "hands", 1.0, 1.0
        if t < 0.40:      # 11-24: 손이 0.15초부터 움직이므로 로켓도 같이 (상태 구간은 그대로)
            return lerp_frame(mK, mW, ease((t - 0.15) / 0.25)), "hands" if t < 0.18 else "hand_R", 1.0 if t < 0.18 else 0.0, 1.0
        if t < 1.95:
            return mW, "hand_R", 0.0, 1.0
        w = ease((t - 1.95) / 0.45)
        return lerp_frame(mW, mK, w), "hand_R" if t < 2.25 else "hands", 0.0 if t < 2.25 else 1.0, 1.0

    cw = reg("A_ROK_Loader_Wave", TW, tl_sampler(wk, TW, lags={"chest": 0.05, "head": 0.08, "arms": 0.02}), Prop(wave_prop),
        start_ref="A_ROK_Loader_Kneel_Idle", end_ref="A_ROK_Loader_Kneel_Idle",
        note="몸과 고개를 왼쪽 뒤로 돌려 왼손으로 두 번 손짓(이리 와)")
    cw.views = ["side", "q", "top", "eye"]

    # ============================================================ 6. 서기 → 무릎 대기 (2.6초): 소총을 등에 메고 로켓을 집어 듦
    TIN = 2.6
    RIF_D = float(os.environ.get("LOADER_RIFD", "0.10"))
    st = copy_pose(stand)
    R.apply(st)
    shR = R.pb["DEF-upper_arm.R"].head.copy()
    thighL = R.pb["DEF-thigh.L"].head.copy()
    # (1) 왼손을 놓고 소총을 오른손으로 세워 오른 어깨 앞에 (총구 위)
    up_r = copy_pose(st)
    up_r["rifle"] = rok_poses.rifle_from_right(R, shR + Vector((-0.06, -0.20, -0.30)), (-0.12, 0.05, 1.0), up=(0, -1, 0))
    up_r["rifle_space"] = 1.0
    up_r = rok_poses.chestify(R, up_r, keys=("rifle",))
    up_r["grip_L"] = 0.0
    up_r = flat_hand(R, up_r, "L", thighL + Vector((0.13, -0.10, -0.01)), (0.9, 0.0, 0.3), (0.0, 0.0, -1.0))   # 11-24: 손 · 아래팔이 탄입대 · 몸통에 6~7 cm 묻히던 것
    up_r["fingers_L"], up_r["thumb_L"] = 0.35, 0.2
    # 손 목표의 공간(문자열)은 두 키 사이 가운데에서 바뀐다: 영향력이 1인 동안(제약이 손을 쥠) 목표를 월드 값으로 바꿔 둔다
    st2 = hand_at_grip(R, st, "L", "world")
    # 11-24: 오른손은 손잡이 표식(사격 쥠) 그대로 소총을 세우면 손목이 150도 꺾였다 → 총몸(기관부)을 어깨 쪽에서 감싸 쥠.
    # 손 목표를 소총 공간으로 두고(제약 자리 = 손잡이 표식) 영향력 1 → 0과 함께 총몸 쥐는 자리로 옮긴다(공간이 같아 튀지 않음)
    from rok_clips_more import grip_local
    st2["hand_R"], st2["hand_space_R"] = grip_local(R, "R"), "rifle"
    up_r = hand_on_rifle(R, up_r, "R", RIF_D, arm_fix=True)
    # (2) 오른쪽 옆으로 (몸 밖으로 돌려 등 뒤로 넘길 준비)
    side = moved(up_r, pelvis=(0.0, 0.0, -0.03), chest_rot=qz(-8))
    side["rifle"] = rok_poses.rifle_from_right(R, shR + Vector((-0.20, 0.02, -0.28)), (-0.25, 0.25, 1.0), up=(-1, 0, 0))
    side["rifle_space"] = 1.0
    side = rok_poses.chestify(R, side, keys=("rifle",))
    side = hand_on_rifle(R, side, "R", RIF_D, arm_fix=True)
    # 소총 공간 → 월드로 바꾸는 키 (같은 손 자리를 월드 값으로): 등에 메기 직전 공간이 바뀌어도 손이 튀지 않게
    side2 = copy_pose(side)
    R.apply(side2)
    _m = R.pb["hand_ik.R"].matrix.copy()
    side2["hand_R"], side2["hand_space_R"] = (_m.translation.copy(), _m.to_quaternion()), "world"
    # (3) 등에 멤 (오른손은 어깨 위에서 놓음), 몸은 내려가기 시작
    slung = with_(blend_pose(stand, K, 0.45), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"], foot_L=stand["foot_L"],
                  toe_L=stand["toe_L"], knee_L=stand["knee_L"], pelvis_loc=Vector((0.02, 0.07, 0.64)))
    slung = rifle_on_back(R, slung)
    R.apply(slung)
    slung = flat_hand(R, slung, "R", R.pb["DEF-upper_arm.R"].head + Vector((-0.04, -0.06, 0.07)), (-0.6, 0.3, 0.7), (0.2, 0.5, 0.85))
    slung = flat_hand(R, slung, "L", R.pb["DEF-thigh.L"].head + Vector((0.10, -0.16, 0.05)), (0.9, 0.0, 0.3), (0.0, -0.3, -1.0))
    slung["fingers_R"], slung["thumb_R"] = 0.3, 0.2
    kneel_low = with_(moved(K, chest_rot=qy(-12) @ qx(16) @ qz(-12), head_rot=qx(18) @ qz(-40), pelvis=(-0.01, 0.0, 0.0)), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"])
    kneel_low = rifle_on_back(R, kneel_low)
    kneel_low = grip_on(R, kneel_low, "R", pile_frame(0), 0.48, (-0.6, 0.6, 0.3), (1.0, -0.2, 0.0), arm=True)
    R.apply(kneel_low)
    kneel_low = flat_hand(R, kneel_low, "L", R.pb["DEF-thigh.L"].head.lerp(R.pb["DEF-shin.L"].head, 0.75) + Vector((0.03, -0.02, 0.10)),
                          (0.15, 0.0, 1.0), (0.1, -1.0, -0.2))
    kneel_low["fingers_R"], kneel_low["thumb_R"] = 0.4, 0.3
    grab0 = with_(copy_pose(kneel_low), foot_R=K["foot_R"], toe_R=K["toe_R"])
    grab0 = grip_on(R, grab0, "R", pile_frame(0), 0.46, (-0.7, 0.6, 0.0), (0.6, -0.7, 0.0), arm=True)
    grab0["fingers_R"], grab0["thumb_R"] = 0.85, 0.55
    mI = lerp_frame(pile_frame(0), mK, 0.45)
    mI.translation = mI.translation + Vector((0, 0, 0.10))
    liftI = moved(copy_pose(grab0), chest_rot=qx(-8) @ qz(10), head_rot=qx(-14) @ qz(30))
    liftI = grip_on(R, liftI, "R", mI, 0.25, (-1.0, 0.3, 0.0), (0.2, -1.0, 0.0), arm=True)
    R.apply(liftI)
    liftI = flat_hand(R, liftI, "L", R.pb["DEF-thigh.L"].head.lerp(R.pb["DEF-shin.L"].head, 0.70) + Vector((0.03, -0.02, 0.10)), (0.15, 0.0, 1.0), (0.1, -1.0, -0.2))
    for P in (up_r, side, slung, kneel_low, grab0, liftI):
        P["neck_share"] = 0.45
    ik = [
        Key(0.0, st),
        Key(0.06, st2, "linear"),
        Key(0.40, up_r, "inout"),
        Key(0.72, side, "inout"),
        Key(0.76, side2, "linear"),
        Key(1.05, slung, "inout", lift_R=0.06),
        Key(1.45, kneel_low, "inout", lift_L=0.035),
        Key(1.70, grab0, "out", lift_R=FLIP_LIFT, groups={"base": "inout"}),   # 11-24: 발등으로 뒤집는 발이 빠르게 출발하며 끌리던 것
        Key(2.15, liftI, "inout"),
        Key(2.6, K, "soft"),
    ]

    def in_prop(t):
        if t < 1.70:
            return pile_frame(0), "pile", 0.0, 0.0
        if t < 2.15:
            return lerp_frame(pile_frame(0), mI, ease((t - 1.70) / 0.45)), "hand_R", 0.0, 1.0
        w = ease((t - 2.15) / 0.45)
        return lerp_frame(mI, mK, w), "hands" if t > 2.35 else "hand_R", 1.0 if t > 2.35 else 0.0, 1.0

    c = reg("A_ROK_Loader_Kneel_In", TIN, tl_sampler(ik, TIN, lags={"chest": 0.05, "head": 0.09, "arms": 0.04}), Prop(in_prop),
            start_ref="A_ROK_Idle_Stand", end_ref="A_ROK_Loader_Kneel_Idle",
            note="서서 든 소총을 오른손으로 세워 오른쪽으로 돌려 등에 메고, 오른 무릎을 꿇은 뒤 옆에 세운 로켓을 집어 듦")

    # ============================================================ 7. 무릎 대기 → 소총 들고 서기 (2.4초)
    TOUT = 2.4
    put = copy_pose(grab0)
    # 11-24 관절 검사로 다시 짬: 처음 판은 오른손이 오른 어깨 뒤(어깨뼈 높이)의 총열을 잡아 팔꿈치 156도 · 위팔 축 돌림 173도로
    # 팔이 어깨를 감았고, 총을 돌리는 동안 손이 총을 따라 180도 돌아 팔이 한 프레임에 4000~5000 deg/s로 뒤집혔다.
    # 또 일어서기 시작할 때 오른발을 들어 왼발 하나로 무게중심이 0.25 m 밖, 왼발이 디딘 채 12 cm 미끄러졌다.
    # 새 순서: 오른손이 어깨 위로 솟은 총열(d=RB_D)을 잡음(오른 무릎은 땅, 오른발은 이때 발끝 세움) → 총을 오른 옆에 세워 들고 일어남
    # → 몸 앞에 세워 왼손이 총열덮개 아래를 잡음 → 총을 서기 자세 각도로, 오른손은 총에서 떼어 옆으로 → 오른손이 손잡이로(서기 자세).
    RB_D = float(os.environ.get("LOADER_RBD", "0.58"))
    RB_ELB = tuple(float(x) for x in os.environ.get("LOADER_RBELB", "-0.30,-0.25,0.10").split(","))
    from rok_clips_more import to_rifle_local, grip_local
    RB_LIFT = float(os.environ.get("LOADER_RBLIFT", "0.07"))
    FA_LD = float(os.environ.get("LOADER_FALD", "0.40"))
    RL_P = tuple(float(x) for x in os.environ.get("LOADER_RLP", "0.12,-0.32,-0.28").split(","))
    FA_P = tuple(float(x) for x in os.environ.get("LOADER_FAP", "0.08,-0.33,0.0").split(","))   # 무릎을 땅에 둔 채 발을 뒤집음 (발목을 많이 들면 무릎도 떠 왼발 하나로 무게를 받게 됨)

    def rifle_at(P, point, muzzle, up, d):
        """총의 d 지점이 point에 오게 (총구 방향 · 가늠자 쪽), 가슴 공간 값으로."""
        Q = copy_pose(P)
        q = gun_rot(Vector(muzzle), Vector(up))
        Q["rifle"] = (Vector(point) - q @ C.gun(0, 0, d), q)
        Q["rifle_space"] = 1.0
        return rok_poses.chestify(R, Q, keys=("rifle",))

    reach_back = with_(moved(K, chest_rot=qz(-14) @ qx(4), head_rot=qz(-28)), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"])
    reach_back = rifle_on_back(R, reach_back)
    R.apply(reach_back)
    reach_back = elbow_world(R, reach_back, "R", R.pb["DEF-upper_arm.R"].head + Vector(RB_ELB))   # 팔꿈치를 앞 · 바깥 위로
    reach_back = hand_on_rifle(R, reach_back, "R", RB_D, arm_fix=True)
    R.apply(reach_back)
    reach_back = flat_hand(R, reach_back, "L", R.pb["DEF-thigh.L"].head.lerp(R.pb["DEF-shin.L"].head, 0.75) + Vector((0.03, -0.02, 0.10)),
                           (0.15, 0.0, 1.0), (0.1, -1.0, -0.2))
    # 오른 옆에 세워 들고 일어나기 시작 (왼발 · 발끝 세운 오른발 · 무게는 두 발 사이)
    rise = with_(blend_pose(K, stand, 0.40), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"], foot_L=K["foot_L"],
                 toe_L=K["toe_L"], knee_L=K["knee_L"], pelvis_loc=Vector((0.03, -0.04, 0.60)))
    R.apply(rise)
    shR2 = R.pb["DEF-upper_arm.R"].head.copy()
    # 11-24 (Unity 검수): -0.12 에서는 세워 든 총의 개머리판 끝이 바닥 3.5 cm 아래(게임 둔덕 비탈에서 8.8 cm 땅속, 6 cm 올린 판도 비탈에서 2.3 cm) → 9 cm 올림 (−0.02 는 오른손목 145도)
    RISE_Z = float(os.environ.get("LOADER_RISEZ", "-0.03"))
    rise = rifle_at(rise, shR2 + Vector((-0.10, -0.26, RISE_Z)), (-0.10, -0.05, 1.0), (-1.0, 0.0, 0.0), RB_D)   # 오른 어깨 앞, 아래팔이 거의 수평
    rise["grip_L"], rise["grip_R"] = 0.0, 0.0
    rise = hand_on_rifle(R, rise, "R", RB_D, arm_fix=True)
    R.apply(rise)
    rise = flat_hand(R, rise, "L", R.pb["DEF-thigh.L"].head + Vector((0.10, -0.16, 0.05)), (0.9, 0.0, 0.3), (0.0, -0.3, -1.0))
    # 거의 일어나 몸 앞에 세움: 오른발이 서기 자리로 디딤, 왼손이 총열덮개 아래(d=0.22)
    front_a = with_(blend_pose(rise, stand, 0.75), foot_R=stand["foot_R"], toe_R=stand["toe_R"], knee_R=stand["knee_R"],
                    foot_L=K["foot_L"], toe_L=K["toe_L"])
    R.apply(front_a)
    shs = (R.pb["DEF-upper_arm.R"].head + R.pb["DEF-upper_arm.L"].head) * 0.5
    dq = R.delta("DEF-spine.003")
    front_a = rifle_at(front_a, shs + dq @ Vector(FA_P), dq @ Vector((0.25, -0.12, 1.0)), dq @ Vector((0.0, -1.0, 0.0)), RB_D)
    front_a["grip_L"], front_a["grip_R"] = 0.0, 0.0
    front_a = hand_on_rifle(R, front_a, "R", RB_D, arm_fix=True)
    front_a = hand_on_rifle(R, front_a, "L", FA_LD, arm_fix=True)
    front_aw = copy_pose(front_a)               # 왼손 목표를 월드 값으로 (허벅지 위 손(월드) → 총 공간으로 바꿀 때 튀지 않게)
    R.apply(front_aw)
    _m = R.pb["hand_ik.L"].matrix.copy()
    front_aw["hand_L"], front_aw["hand_space_L"] = (_m.translation.copy(), _m.to_quaternion()), "world"
    # 오른손을 총에서 떼어 왼손 오른쪽 옆을 지나 내림 (총 공간 값: 총열 쪽으로 곧장 내리면 왼손 · 아래팔을 뚫음)
    pass_r = copy_pose(front_a)
    # 11-24 (Unity 검수): 비키는 동작을 0.34초로 늘리니 (-0.13, 0, -0.30)에서 위팔이 −96도 돌아감(한계 −95) → 덜 비키고 조금 위로
    PASS_OFF = tuple(float(x) for x in os.environ.get("LOADER_PASSOFF", "-0.08,0.04,-0.30").split(","))
    pass_r["hand_R"] = (pass_r["hand_R"][0] + C.gun(*PASS_OFF), pass_r["hand_R"][1])   # 총의 오른쪽(로컬 −X)으로 비켜
    pass_r["fingers_R"], pass_r["thumb_R"] = 0.4, 0.3
    # 11-24 (2차): 왼손이 허벅지 → 총으로 곧장 가면 아래팔이 배 · 탄입대에 2~3 cm 묻힘 → 몸 앞 왼쪽 바깥을 거쳐 감
    reach_l = blend_pose(rise, front_aw, 0.5)
    R.apply(reach_l)
    reach_l = flat_hand(R, reach_l, "L", R.pb["DEF-upper_arm.L"].head + Vector(RL_P), (0.6, 0.0, -0.8), (0.0, -1.0, 0.0))
    reach_l["grip_L"] = 0.0
    # 총을 서기 자세 각도로: 왼손은 총열덮개 쥠(제약, 목표도 같은 자리), 오른손은 총에서 떼어 손잡이 오른쪽 옆에
    front_b = with_(blend_pose(front_a, stand, 0.6), foot_R=stand["foot_R"], toe_R=stand["toe_R"], knee_R=stand["knee_R"],
                    foot_L=K["foot_L"], toe_L=K["toe_L"])
    front_b["rifle"], front_b["rifle_space"] = stand["rifle"], 1.0
    front_b["grip_L"], front_b["grip_R"] = 1.0, 0.0
    front_b["hand_L"], front_b["hand_space_L"] = grip_local(R, "L"), "rifle"
    front_b["hand_R"], front_b["hand_space_R"] = grip_local(R, "R"), "rifle"   # 손잡이 자리 (영향력은 0 → 끝에서 1)
    front_b["fingers_R"], front_b["thumb_R"] = 0.4, 0.3
    # 끝 = 서기 자세 (두 손 모두 제약, 목표도 제약 자리 — 영향력 1이라 서기 자세와 같은 모습)
    fin = copy_pose(stand)
    fin["hand_L"], fin["hand_space_L"] = grip_local(R, "L"), "rifle"
    fin["hand_R"], fin["hand_space_R"] = grip_local(R, "R"), "rifle"
    # 오른손이 로켓을 내려놓은 자리(월드)를 소총 공간 값으로 바꾼 키: 공간이 바뀌는 순간에도 손이 튀지 않게
    put2 = copy_pose(put)
    R.apply(put2)
    put2["hand_R"] = to_rifle_local(R, put2, R.pb["hand_ik.R"].matrix.copy())
    put2["hand_space_R"] = "rifle"
    for P in (reach_back, rise, front_a, front_aw, front_b):
        P["neck_share"] = 0.45
    DEBUG.update(reach_back=reach_back, rise=rise, front_a=front_a, front_b=front_b)
    ok = [
        Key(0.0, K),
        Key(0.40, put, "inout"),
        Key(0.46, put2, "linear"),
        Key(0.85, reach_back, "inout", lift_R=RB_LIFT, groups={"base": "inout"}),
        Key(1.40, rise, "inout"),
        Key(1.58, reach_l, "inout", lift_R=0.07),
        Key(1.75, front_aw, "inout", lift_R=0.07),
        Key(1.79, front_a, "linear"),
        # 11-24 (Unity 검수): 1.95 → 2.12 의 0.17초에 왼손 하나로 총을 0.45 m 올리며 70도 돌려(최고 4.3 m/s · 700 deg/s) 튀어 보임 → 0.34초로
        Key(float(os.environ.get("LOADER_PASST", "1.90")), pass_r, "inout"),
        Key(float(os.environ.get("LOADER_FRONTBT", "2.24")), front_b, "inout"),
        Key(2.4, fin, "soft", lift_L=0.06),
    ]

    def out_prop(t):
        if t < 0.40:
            w = ease(t / 0.40)
            return lerp_frame(mK, pile_frame(0), w), "hand_R" if t > 0.12 else "hands", 0.0 if t > 0.12 else 1.0, 1.0
        return pile_frame(0), "pile", 0.0, 0.0

    DEBUG.update(up_r=up_r, side=side, slung=slung, kneel_low=kneel_low, grab0=grab0, liftI=liftI, put2=put2)
    reg("A_ROK_Loader_ToRifle_Stand", TOUT, tl_sampler(ok, TOUT, lags={"chest": 0.05, "head": 0.09, "arms": 0.04}), Prop(out_prop),
        start_ref="A_ROK_Loader_Kneel_Idle", end_ref="A_ROK_Idle_Stand",
        note="로켓을 옆에 세워 두고, 등의 소총을 오른손으로 잡아 오른쪽으로 내리며 일어나 두 손으로 듦")
    # 11-24: 키 시간 (06 attach_prop 이 손-로켓 기준 프레임으로 씀)
    for nm, ks in (("A_ROK_Loader_Load", load_keys), ("A_ROK_Loader_Reach", rk), ("A_ROK_Loader_Brace", bk),
                   ("A_ROK_Loader_Wave", wk), ("A_ROK_Loader_Kneel_In", ik), ("A_ROK_Loader_ToRifle_Stand", ok)):
        clips[nm].key_times = [k.t for k in ks]
    return new


def hand_on_rifle(R, P, side, d, slide_to_grip=False, arm_fix=False, _pass=0):
    """소총을 따라가는 손 목표(hand_space 'rifle'): 총 원점에서 총구 쪽 d m의 총열을 쥠. slide_to_grip: 손잡이 표식 값.
    arm_fix: 손 방향을 아래팔에 맞춤 — 한 번 놓아 본 뒤 그 자세의 팔꿈치로 다시 계산(11-24)."""
    if arm_fix and _pass == 0:
        P = hand_on_rifle(R, P, side, d, arm_fix=True, _pass=1)
    from rok_clips_more import grip_local, to_rifle_local, rifle_matrix
    Q = copy_pose(P)
    if slide_to_grip:
        Q["hand_" + side] = grip_local(R, side)
        Q["hand_space_" + side] = "rifle"
        return Q
    mr = rifle_matrix(R, Q)
    axis = (mr.to_3x3() @ Vector((0, 1, 0))).normalized()
    top = (mr.to_3x3() @ Vector((0, 0, 1))).normalized()
    pt = mr.translation + axis * d
    R.apply(Q)
    sh = R.pb["DEF-upper_arm." + side].head
    if arm_fix:
        # 11-24: 손가락 방향(f)을 팔꿈치 → 쥐는 자리 방향(총 축에 수직 성분)에 맞춰 손목이 아래팔과 거의 일직선이 되게.
        # (원래 식은 손등을 어깨 쪽으로 두어 손가락이 아래팔과 직각 → 손목 136~152도)
        el = R.pb["DEF-forearm." + side].head
        fd = pt - el
        fd = (fd - axis * fd.dot(axis)).normalized()
        b = fd.cross(axis).normalized()
        if b.dot(top) < 0:
            b = -b                  # 손등은 가늠자 쪽
        f = axis.cross(b).normalized()
        if f.dot(fd) < 0:
            f = -f
    else:
        b = pt - sh
        b = -(b - axis * b.dot(axis)).normalized()          # 손등 = 어깨 쪽(총 바깥에서 감싸 쥠)
        f = axis.cross(b).normalized()
        if f.dot(top) < 0:
            f = -f
    wrist = pt + b * 0.045 - f * 0.06
    from rok_clips_more import hand_matrix
    hm = hand_matrix(R, side, wrist, f, b)
    Q["hand_" + side] = to_rifle_local(R, Q, hm)
    Q["hand_space_" + side] = "rifle"
    Q["grip_" + side] = 0.0
    Q["fingers_" + side], Q["thumb_" + side] = 0.85, 0.55
    return Q
