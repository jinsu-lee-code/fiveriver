# 주요 자세(포즈) 정의. 값은 루트 기준 m · 도. 캐릭터는 -Y를 본다(왼쪽 +X, 위 +Z).
import io
import json
import math

from mathutils import Quaternion, Vector

import rok_common as C
from rok_rig import rot, gun_rot, copy_pose, UP


def load_ref():
    with io.open(C.REF_JSON, encoding="utf-8") as fh:
        return json.load(fh)


def chestify(R, P, keys=("rifle", "elbow_L", "elbow_R", "hand_L", "hand_R")):
    """월드 좌표로 적은 소총 · 팔꿈치 · 뗀 손 목표를 '가슴과 함께 움직이는 공간'의 값으로 바꾼다."""
    R.apply(P)
    inv = R.delta4("DEF-spine.003").inverted()
    invh = R.delta4("DEF-spine.006").inverted()
    for k in keys:
        v = P[k]
        m = inv
        if k.startswith("hand_"):
            space = P["hand_space_" + k[-1]]
            if space == "world":
                continue
            if space == "head":
                m = invh
        if k == "rifle" and P["rifle_space"] < 0.5:
            continue
        if isinstance(v, tuple):
            mm = v[1].to_matrix().to_4x4()
            mm.translation = v[0]
            mm = m @ mm
            P[k] = (mm.translation.copy(), mm.to_quaternion())
        else:
            P[k] = m @ v
    return P


def rifle_from_right(R, wrist, muzzle, up=(0, 0, 1), roll=0.0):
    """오른 손목이 있을 자리와 총구 방향으로 소총 원점 · 회전을 정한다."""
    q = gun_rot(muzzle, up, roll)
    g = R.rest["rifle"].inverted() @ R.rest["grip.R"].translation       # 총 기준 오른 손목 자리 (뼈 로컬)
    return (Vector(wrist) - q @ g, q)


def rifle_from_butt(R, butt, muzzle, up=(0, 0, 1), roll=0.0):
    q = gun_rot(muzzle, up, roll)
    return (Vector(butt) - q @ C.gun(*C.RIFLE_BUTT), q)


KNEEL_KNEE_Z = 0.082      # 꿇은 무릎 관절 중심 높이(m): 무릎 메시가 땅에 닿는 높이
KNEEL_FOOT_TILT = 30.0    # 발등을 눕힌 발: 발바닥 면이 뒤로 가며 아래로 숙인 각(도). 발 뼈보다 군화 윗면이 약 22도 더 숙여져 있다
KNEEL_ANKLE_Z = 0.050     # 그때 발목 관절 높이(m): 정강이 · 군화 윗면이 함께 땅에 닿음(메시 0~2 cm)


TUCK = {}                 # make_poses가 채움: 무릎 꿇는 다리의 발끝 세운 발 {"foot_R": (발목, 회전), "toe_R": 회전}


def instep_leg(R, knee, back, tilt, ankle_z):
    """무릎을 땅에 댄 다리의 발을 발등이 땅에 닿게 눕힌다: 발끝은 무릎 반대쪽(뒤)을 보고 tilt도 아래로, 발바닥은 위-뒤를 본다.
    발목은 무릎에서 정강이 길이만큼 뒤, 높이 ankle_z. 반환 (발목 위치, 발 회전 = 발가락 회전)."""
    from mathutils import Matrix
    b = Vector((back[0], back[1], 0)).normalized()
    t = math.radians(tilt)
    d = b * math.cos(t) - UP * math.sin(t)           # 발끝 방향
    n = UP * math.cos(t) + b * math.sin(t)           # 발바닥이 보는 쪽(바깥)
    d0, n0 = Vector((0, -1, 0)), Vector((0, 0, -1))   # 기준 자세: 발끝 -Y, 발바닥 아래
    m0 = Matrix((d0.cross(n0), d0, n0)).transposed()
    m1 = Matrix((d.cross(n), d, n)).transposed()
    q = (m1 @ m0.transposed()).to_quaternion()
    dz = ankle_z - knee.z
    ankle = knee + b * math.sqrt(max(1e-6, R.shin ** 2 - dz * dz)) + UP * dz
    return ankle, q


def make_poses(R):
    ref = load_ref()

    def rq(clip, bone, f=1):
        return Quaternion(ref[clip]["samples"][str(f)]["delta"][bone])

    def rj(clip, joint, f=1):
        return Vector(ref[clip]["samples"][str(f)]["joints"][joint])

    poses = {}
    base = R.base_pose()

    # ============================================================ 서서 대기 (기존 A_ROK_Idle_Stand 첫 프레임에 맞춤)
    c = "A_ROK_Idle_Stand"
    P = copy_pose(base)
    lift = min(rj(c, "LeftToes").z - R.ball0["L"].z, rj(c, "RightToes").z - R.ball0["R"].z)   # 리타깃으로 뜬 만큼 내린다
    P["pelvis_loc"] = rj(c, "Hips") - UP * lift
    P["pelvis_rot"] = rq(c, "Hips")
    P["chest_rot"] = rq(c, "UpperChest")
    P["head_rot"] = rq(c, "Head")
    for s, Sd in (("L", "Left"), ("R", "Right")):
        a = rj(c, Sd + "Foot")
        yaw = rq(c, Sd + "Foot").to_euler("XYZ").z
        qz = Quaternion((0, 0, 1), yaw)
        P["foot_" + s] = (Vector((a.x, a.y, R.ankle0[s].z)), qz)       # 발은 바닥에 평평하게
        P["toe_" + s] = qz
        k = rj(c, Sd + "LowerLeg")
        mid = (rj(c, Sd + "UpperLeg") + a) * 0.5
        P["knee_" + s] = k + (k - mid).normalized() * 1.5
    # 소총: 배 높이에 가로로 (게임의 서서 대기처럼 총구는 루트의 앞-왼쪽)
    # 게임의 서서 들기(AlliedRifleFit.carryPosition (.04, 1.08, .20), carryEuler (-3.2, -31.9, 0))와 같은 자리
    P["rifle"] = (Vector((-0.04, -0.20, 1.08)), gun_rot((0.528, -0.848, 0.056)))
    P["elbow_R"] = Vector((-0.50, 0.30, 0.95))
    P["elbow_L"] = Vector((0.45, -0.05, 0.85))
    P["shoulder_L"] = rot(roll=0)
    poses["stand"] = chestify(R, P)

    # ============================================================ 무릎 사격 (기존 A_ROK_Idle_Trench 첫 프레임의 다리 · 골반 · 몸통)
    c = "A_ROK_Idle_Trench"
    P = copy_pose(base)
    P["pelvis_rot"] = rq(c, "Hips")
    P["chest_rot"] = rq(c, "UpperChest")
    P["head_rot"] = rq(c, "Head")
    la = rj(c, "LeftFoot")
    lyaw = rq(c, "LeftFoot").to_euler("XYZ").z
    qz = Quaternion((0, 0, 1), lyaw)
    P["foot_L"] = (Vector((la.x, la.y, R.ankle0["L"].z)), qz)
    P["toe_L"] = qz
    lk = rj(c, "LeftLowerLeg")
    P["knee_L"] = lk + (lk - (rj(c, "LeftUpperLeg") + la) * 0.5).normalized() * 1.5
    # 꿇은 다리: 무릎은 기존 클립의 자리에서 땅에, 발은 직접 놓음.
    # (기존 클립의 발 · 발가락 각도를 그대로 쓰면 무릎이 땅 밑 1 cm로 들어가 발볼이 8 cm 떠 있었다 — 기존 클립 쪽 문제)
    # 11-18 저녁 (사용자 "발이 저렇게 꺽일수가 있나?"): 발끝을 세워 발볼로 짚으면(R.kneel_leg) 발바닥이 76도로 서고
    # 군화 앞코가 76도 접혀 주먹처럼 말린다 — 군화 밑창은 그만큼 꺾이지 않는다. 발등을 땅에 눕히고(앞코가 뒤를 봄)
    # 정강이와 군화 윗면이 함께 땅에 눕는다(사용자가 보낸 참고 그림과 같은 모양). 앞코는 꺾이지 않는다.
    # 무릎 관절 중심은 6.2 → 8.2 cm (6.2 cm에서는 무릎 · 정강이 메시가 땅 밑 2.3 cm까지 묻혔다).
    knee = rj(c, "RightLowerLeg")
    knee.z = KNEEL_KNEE_Z
    back = rj(c, "RightFoot") - knee
    ankle, qf = instep_leg(R, knee, back, KNEEL_FOOT_TILT, KNEEL_ANKLE_Z)
    P["foot_R"] = (ankle, qf)
    P["toe_R"] = qf
    # 앉고 일어서는 동안 거치는 "발끝 세운" 발(발볼이 땅, 뒤꿈치 듦): 무릎이 땅에 닿기 전 · 뜬 뒤에는 이 발로 디딘다
    ta, tq, tt, _ = R.kneel_leg("R", knee, (back.x, back.y))
    TUCK["foot_R"], TUCK["toe_R"] = (ta, tq), tt
    P["knee_R"] = knee + (knee - (rj(c, "RightUpperLeg") + ankle) * 0.5).normalized() * 1.5
    # 꿇은 쪽 허벅지 길이가 맞도록 골반 높이를 정한다 (무릎은 땅, 엉덩이 관절은 무릎에서 허벅지 길이만큼)
    hj = rj(c, "Hips") + P["pelvis_rot"] @ (R.hj0["R"] - R.h0)
    u = (hj - knee)
    u = Vector((u.x, u.y, math.sqrt(max(1e-6, R.thigh ** 2 - u.x ** 2 - u.y ** 2))))
    P["pelvis_loc"] = R.pelvis_from_hip("R", knee + u, P["pelvis_rot"])
    kneel_base = copy_pose(P)
    # 견착: 게임(AllyRiflePose.Apply)처럼 오른 어깨에 개머리판, 총은 루트의 앞에서 왼쪽 33.6도
    R.apply(P)
    sh = R.pb["DEF-upper_arm.R"].head.copy()
    aim = Vector((math.sin(math.radians(33.6)), -math.cos(math.radians(33.6)), -0.02))
    P["rifle"] = rifle_from_butt(R, sh + Vector((-0.02, -0.03, -0.02)), aim)
    P["head_rot"] = rot(pitch=6, yaw=30, roll=-7)
    P["elbow_R"] = Vector((-0.75, 0.25, 0.55))
    P["elbow_L"] = Vector((0.45, -0.35, 0.30))
    P["index_R"] = 0.5
    poses["fire_crouch"] = chestify(R, P)

    # ============================================================ A. 참호 낮춘 준비 자세
    # 다리는 무릎 사격 자세와 같은 자리(전환 때 발 · 무릎이 움직이지 않음). 엉덩이를 뒤꿈치 쪽으로 내려 앉고 상체를 숙인다.
    P = copy_pose(kneel_base)
    pel_yaw = math.degrees(kneel_base["pelvis_rot"].to_euler("XYZ").z)
    P["pelvis_rot"] = rot(pitch=4, yaw=pel_yaw * 0.6, roll=-2)
    u = Vector((0.40, 0.50, 0.0))
    u.z = math.sqrt(1 - u.x ** 2 - u.y ** 2)
    P["pelvis_loc"] = R.pelvis_from_hip("R", knee + R.thigh * u, P["pelvis_rot"])
    P["chest_rot"] = rot(pitch=24, yaw=pel_yaw * 0.25, roll=-2)
    P["head_rot"] = rot(pitch=7, yaw=0, roll=0)
    P["rifle"] = rifle_from_right(R, (-0.150, -0.130, 0.455), (0.60, -0.36, 0.72), roll=-20)
    P["elbow_R"] = Vector((-0.60, 0.45, 0.45))
    P["elbow_L"] = Vector((0.55, -0.05, 0.25))
    poses["ready"] = chestify(R, P)
    return poses
