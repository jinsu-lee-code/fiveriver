# 11-18 둘째 묶음: F 시신 곁 무릎(박용팔) · G 엎드려 겨눔(윤태석) · 달리기 출발(다시) · 달리다 무릎 사격.
# 좌표: 캐릭터는 -Y를 본다(왼쪽 +X, 위 +Z). rot(pitch 앞숙임+, yaw 왼쪽+, roll 왼쪽으로 기울임+).
import math

from mathutils import Matrix, Quaternion, Vector

import rok_common as C
import rok_poses
import rok_run
from rok_rig import rot, gun_rot, copy_pose, blend_pose, ease, frame, blend
from rok_timeline import Key, Timeline, breathe, qx, qy, qz, GROUPS
from rok_clips import Clip, with_, moved, tl_sampler, hand_world, elbow_world, FLIP_LIFT

UP = Vector((0, 0, 1))


# ------------------------------------------------------------------ 도우미
def rifle_matrix(R, P):
    R.apply(P)
    m = R.pb["rifle"].matrix
    out = m.to_3x3().normalized().to_4x4()
    out.translation = m.translation.copy()
    return out


def rifle_to_world(R, P):
    """가슴 공간의 소총 값을 그 순간의 월드 값으로 (rifle_space 0)."""
    Q = copy_pose(P)
    m = rifle_matrix(R, Q)
    Q["rifle"] = (m.translation.copy(), m.to_quaternion())
    Q["rifle_space"] = 0.0
    return Q


def grip_local(R, side):
    """소총 뼈 기준 손잡이 표식(= 제약이 손을 놓는 자리). hand_space 'rifle' 값."""
    m = R.rest["rifle"].inverted() @ R.rest["grip." + side]
    return (m.translation.copy(), m.to_quaternion())


def to_rifle_local(R, P, m_world):
    mr = rifle_matrix(R, P)
    m = mr.inverted() @ m_world
    return (m.translation.copy(), m.to_quaternion())


def hand_matrix(R, side, wrist, fingers, back):
    q = R.hand_abs(side, Vector(fingers), Vector(back))
    m = q.to_matrix().to_4x4()
    m.translation = Vector(wrist)
    return m


def world_hand_at_current_grip(R, P, side):
    R.apply(P)
    m = R.pb["grip." + side].matrix
    return (m.translation.copy(), m.to_quaternion())


def joint(R, name):
    return R.pb[name].head.copy()


def mesh_low(R, P, objs=("Body_Low", "Cap_Low"), near=None, radius=0.3):
    """자세 P에서 메시 최저 높이 (near=(x,y)를 주면 그 수평 반경 안만)."""
    import bpy
    R.apply(P)
    dg = bpy.context.evaluated_depsgraph_get()
    low = 9.0
    for name in objs:
        o = bpy.data.objects.get(name)
        if o is None:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ev.matrix_world
        for v in me.vertices:
            p = mw @ v.co
            if near is not None and math.hypot(p.x - near[0], p.y - near[1]) > radius:
                continue
            low = min(low, p.z)
        ev.to_mesh_clear()
    return low


def hermite(p0, m0, p1, m1, u):
    return rok_run.hermite(p0, m0, p1, m1, u)


def path(pts, t):
    """pts: [(시간, 위치, 속도)] → t의 위치 (구간마다 에르미트)."""
    if t <= pts[0][0]:
        return pts[0][1].copy()
    for (ta, pa, va), (tb, pb, vb) in zip(pts, pts[1:]):
        if t <= tb:
            d = tb - ta
            return hermite(pa, va * d, pb, vb * d, (t - ta) / d)
    return pts[-1][1].copy()


def curve(keys, t, kind="inout"):
    """keys: [(시간, 값)] 숫자 보간."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (ta, a), (tb, b) in zip(keys, keys[1:]):
        if t <= tb:
            return a + (b - a) * ease((t - ta) / (tb - ta), kind)
    return keys[-1][1]


def planted(R, side, ball_xy, theta, yaw=0.0):
    """발볼 자리 · 발 각도(+ 뒤꿈치 듦, - 발끝 듦: 뒤꿈치를 축으로) → (발목, 발 회전, 발가락 회전). rok_run.stance_foot와 같은 식."""
    q = qz(yaw) @ qx(theta)
    off = R.ankle0[side] - R.ball0[side]
    off.x = 0.0
    ball = Vector((ball_xy[0], ball_xy[1], R.ball0[side].z))
    if theta >= 0:
        return ball + q @ off, q, qz(yaw)
    heel0 = Vector((0.0, R.ankle0[side].y - R.ball0[side].y + 0.05, -R.ball0[side].z))
    heel = ball + qz(yaw) @ heel0
    return heel + q @ (off - heel0), q, qz(yaw)


def blend_groups(A, B, w):
    """w: {묶음: 가중치} 로 두 자세를 섞는다."""
    out = {}
    for g, chans in GROUPS.items():
        for c in chans:
            out[c] = blend(A[c], B[c], w[g])
    for k in A:
        if k not in out:
            out[k] = blend(A[k], B[k], w["base"])
    return out


# ------------------------------------------------------------------ F. 시신 곁 무릎 (박용팔)
def mourn_pose(R, stand):
    """오른 무릎을 꿇고 왼 무릎을 세움. 오른손은 세운 소총의 총열덮개 위, 개머리판은 땅. 왼손은 왼 무릎 위. 고개를 숙임."""
    P = copy_pose(stand)
    P["pelvis_rot"] = rot(pitch=7, yaw=-18, roll=-2)
    knee_R = Vector((-0.19, -0.06, 0.062))
    ankle_R, qf, qt, ball_R = R.kneel_leg("R", knee_R, (0.22, 1.0))
    P["foot_R"], P["toe_R"] = (ankle_R, qf), qt
    u = Vector((0.10, 0.36, 0.0))
    u.z = math.sqrt(1 - u.x ** 2 - u.y ** 2)
    P["pelvis_loc"] = R.pelvis_from_hip("R", knee_R + R.thigh * u, P["pelvis_rot"])
    P["knee_R"] = knee_R + Vector((-0.2, -1.4, -0.3))
    aL = Vector((0.125, -0.40, R.ankle0["L"].z))
    P["foot_L"], P["toe_L"] = (aL, qz(2)), qz(2)
    P["knee_L"] = Vector((0.35, -1.6, 0.7))
    P["chest_rot"] = rot(pitch=25, yaw=-9, roll=-3)
    P["head_rot"] = rot(pitch=44, yaw=-3, roll=-5)
    P["neck_share"] = 0.55
    P["grip_L"], P["grip_R"] = 0.0, 0.0
    P["fingers_R"], P["thumb_R"], P["index_R"] = 0.95, 0.55, 0.85
    P["fingers_L"], P["thumb_L"] = 0.32, 0.15
    # 소총: 개머리판을 오른 무릎 오른쪽 앞 땅에, 총구는 오른 어깨 쪽으로 조금 기댐. 가늠자는 몸 쪽(뒤)
    R.apply(P)
    sh = joint(R, "DEF-upper_arm.R")
    pel = P["pelvis_loc"]
    butt = Vector((sh.x - 0.17, sh.y - 0.36, 0.0))
    lean_to = Vector((sh.x - 0.06, sh.y - 0.20, 1.0))
    d = (lean_to - butt).normalized()
    up_hint = Vector((pel.x - butt.x, pel.y - butt.y, 0)).normalized()
    P["rifle"] = rok_poses.rifle_from_butt(R, butt + Vector((0, 0, 0.02)), d, up=up_hint)
    P["rifle_space"] = 0.0
    # 개머리판 바닥이 땅에 닿게 (소총 메시 최저점 = 0)
    lowr = mesh_low(R, P, objs=("M1_Garand",))
    loc, q = P["rifle"]
    P["rifle"] = (loc - Vector((0, 0, lowr)), q)
    # 오른손: 총열덮개 위쪽을 엄지가 위로 오게 감싸 쥠
    mr = rifle_matrix(R, P)
    axis = (mr.to_3x3() @ Vector((0, 1, 0))).normalized()
    origin = mr.translation
    point = origin + axis * 0.30
    sh = joint(R, "DEF-upper_arm.R")
    f = point - sh
    f = Vector((f.x, f.y, 0)).normalized()
    f = (f - axis * f.dot(axis)).normalized()
    back = f.cross(axis).normalized()
    wrist = point - f * 0.062 + back * 0.030
    hm = hand_matrix(R, "R", wrist, f, back)
    P["hand_R"] = to_rifle_local(R, P, hm)
    P["hand_space_R"] = "rifle"
    P = elbow_world(R, P, "R", sh + Vector((-0.40, 0.30, -0.05)))
    # 왼손: 왼 무릎 위 (손목은 무릎 뒤 허벅지 위, 손가락은 무릎을 덮고 아래로)
    R.apply(P)
    kL, hL = joint(R, "DEF-shin.L"), joint(R, "DEF-thigh.L")
    t = (kL - hL).normalized()
    side = t.cross(UP).normalized()
    if side.x < 0:
        side = -side
    wristL = kL - t * 0.115 + UP * 0.070 + side * 0.012
    fl = (t * 0.75 - UP * 0.55 - side * 0.15).normalized()
    bl = (UP - fl * UP.dot(fl)).normalized()
    P = hand_world(R, P, "L", wristL, fl, bl, "world")
    shL = joint(R, "DEF-upper_arm.L")
    P = elbow_world(R, P, "L", shL + Vector((0.40, 0.25, -0.35)))
    return P


def mourn_clips(R, L, clips):
    stand = L["stand"]
    M = mourn_pose(R, stand)
    S0 = rifle_to_world(R, stand)
    S0["hand_space_R"] = "rifle"
    S0["hand_R"] = grip_local(R, "R")
    hgR = M["hand_R"]
    # B: 왼손이 총을 세워 몸 오른쪽 앞에 듦(총구 위), 오른손은 위쪽 총열덮개로 옮겨 잡음
    B = copy_pose(S0)
    B = moved(B, pelvis=(-0.012, -0.004, -0.010), chest_rot=qx(6), head_rot=qx(14) @ qz(-2))
    R.apply(B)
    shR = joint(R, "DEF-upper_arm.R")
    vdir = Vector((0.06, 0.10, 1.0)).normalized()
    pel = B["pelvis_loc"]
    up_hint = Vector((0.25, 1.0, 0)).normalized()
    # 오른손 자리(총의 0.30 m 지점)가 어깨 앞 아래 0.30 m에 오게
    q_v = gun_rot(vdir, up_hint)
    target_pt = shR + Vector((-0.13, -0.24, -0.17))
    B["rifle"] = (target_pt - q_v @ Vector((0, 0.30, 0)), q_v)
    B["rifle_space"] = 0.0
    B["grip_L"], B["grip_R"] = 1.0, 0.0
    B["hand_R"] = hgR
    B["fingers_R"], B["thumb_R"], B["index_R"] = 0.95, 0.55, 0.85
    B = elbow_world(R, B, "R", shR + Vector((-0.45, 0.20, -0.20)))
    B = elbow_world(R, B, "L", joint(R, "DEF-upper_arm.L") + Vector((0.45, 0.10, -0.40)))
    # A: 오른손을 떼고 가는 중 (총 위 바깥쪽에 떠 있음)
    A = blend_pose(S0, B, 0.45)
    A["grip_L"], A["grip_R"] = 1.0, 0.0
    gl = grip_local(R, "R")
    mid = (gl[0].lerp(hgR[0], 0.5) + Vector((0.06, 0.0, -0.05)), gl[1].slerp(hgR[1], 0.5))
    A["hand_R"] = mid
    A["hand_space_R"] = "rifle"
    # B2: 왼손을 놓음 (그 자리에 둔다 = 튀지 않음)
    B2 = copy_pose(B)
    B2["grip_L"] = 0.0
    B2["hand_L"] = world_hand_at_current_grip(R, B, "L")
    B2["hand_space_L"] = "world"
    B["hand_L"], B["hand_space_L"] = B2["hand_L"], "world"
    A["hand_L"], A["hand_space_L"] = B2["hand_L"], "world"
    S0["hand_L"], S0["hand_space_L"] = B2["hand_L"], "world"
    # C: 왼발을 앞으로 내딛고 몸을 낮춤(런지), 오른발은 발볼로 서고 뒤꿈치를 듦. 총은 오른손으로 내려 옴
    Cp = blend_pose(B2, M, 0.55)
    Cp["foot_L"], Cp["toe_L"], Cp["knee_L"] = M["foot_L"], M["toe_L"], M["knee_L"]
    aR0, qR0, tR0 = rok_run.foot_state(R, stand, "R")
    yawR = math.degrees(qR0.to_euler("XYZ").z)
    bR = rok_run.flat_ball(R, "R", aR0, yawR)
    a, q, toe = rok_run.ground_foot(R, "R", (bR.x + 0.02, bR.y + 0.04), 38.0, yawR * 0.6)
    Cp["foot_R"], Cp["toe_R"] = (a, q), toe
    Cp["knee_R"] = M["knee_R"]
    Cp["pelvis_loc"] = M["pelvis_loc"].lerp(B2["pelvis_loc"], 0.45) + Vector((0, -0.03, 0))
    Cp["pelvis_loc"].z = 0.60
    Cp["grip_L"], Cp["grip_R"] = 0.0, 0.0
    Cp["hand_R"], Cp["hand_space_R"] = hgR, "rifle"
    Cp["rifle_space"] = 0.0
    loc, q = M["rifle"]
    Cp["rifle"] = (loc + Vector((0.01, 0.03, 0.17)), B2["rifle"][1].slerp(q, 0.7))
    # 왼손: 허벅지 위로 가는 중
    R.apply(Cp)
    kL = joint(R, "DEF-shin.L")
    Cp = hand_world(R, Cp, "L", kL + Vector((0.03, 0.10, 0.16)), (0.2, -0.8, -0.6), (0.0, 0.3, 1.0), "world")
    Cp["fingers_L"], Cp["thumb_L"] = 0.35, 0.2
    Cp["hand_space_L"] = "world"
    # D: 무릎이 땅에 닿고 개머리판이 땅에 닿음. 몸은 아직 조금 높고 덜 숙임
    D = moved(M, pelvis=(0, 0.0, 0.022), chest_rot=qx(-8), head_rot=qx(-14))
    D["foot_R"], D["toe_R"] = M["foot_R"], M["toe_R"]
    keys = [
        Key(0.0, S0),
        Key(0.35, A, "inout"),
        Key(0.62, B, "inout"),
        Key(0.82, B2, "inout"),
        Key(1.32, Cp, "inout", lift_L=0.05),
        Key(1.66, D, "out", lift_R=0.03),
        Key(2.0, M, "soft"),
    ]
    clip = Clip("A_ROK_Kneel_Mourn_In", 2.0, tl_sampler(keys, 2.0, lags={"chest": 0.06, "head": 0.12, "arms": 0.04}),
                start_ref="A_ROK_Idle_Stand", end_ref="A_ROK_Kneel_Mourn_Loop",
                note="소총을 세워 오른손으로 옮겨 쥐고, 왼발을 내디뎌 오른 무릎을 꿇으며 개머리판을 땅에 짚고 고개를 숙임")
    clip.ground, clip.views, clip.cam, clip.mesh_check = True, ["side", "front", "qf"], ((0, -0.15, 0.78), 2.0), True
    clip.step = 0.1
    clips[clip.name] = clip

    T = 4.0

    def loop(t):
        P = copy_pose(M)
        # 무거운 숨 한 번(4초), 내쉴 때 어깨 · 고개가 더 처짐. 무게가 앞뒤로 조금
        b = (1 - math.cos(2 * math.pi * t / T)) / 2
        P["chest_rot"] = qx(-2.6 * b) @ P["chest_rot"]
        P["pelvis_loc"] = P["pelvis_loc"] + Vector((0.0, 0.004 * math.sin(2 * math.pi * t / T), 0.004 * b))
        h = math.sin(2 * math.pi * t / T - 0.6) - math.sin(-0.6)
        P["head_rot"] = qx(-1.5 * b) @ qz(1.2 * math.sin(2 * math.pi * t / T)) @ qx(1.2 * h) @ P["head_rot"]
        P["shoulder_L"] = qx(1.5 * b) @ P["shoulder_L"]
        P["shoulder_R"] = qx(1.5 * b) @ P["shoulder_R"]
        g = math.sin(math.pi * min(1.0, max(0.0, (t - 2.3) / 0.9))) ** 2
        P["fingers_R"] = M["fingers_R"] + 0.12 * g          # 총을 한 번 꽉 쥠
        P["fingers_L"] = M["fingers_L"] + 0.15 * g
        return P

    clip = Clip("A_ROK_Kneel_Mourn_Loop", T, loop, loop=True, start_ref="A_ROK_Kneel_Mourn_In",
                note="무릎 꿇고 고개 숙인 채 숨 · 작은 고개 움직임 · 손을 한 번 쥠")
    clip.start_ref = None
    clip.ground, clip.views, clip.cam, clip.mesh_check = True, ["side", "front", "qf"], ((0, -0.15, 0.55), 1.5), True
    clip.step = 0.5
    clips[clip.name] = clip
    return M


# ------------------------------------------------------------------ G. 엎드려 겨눔 (윤태석, FM 23-5 90 · 113항)
YAW_B = -22.0       # 몸이 총보다 오른쪽 앞을 향함(다리는 왼쪽 뒤로). 교범: 척추와 총의 각 30도 이하


def spread(v, deg):
    return qz(deg) @ v


def prone_legs(R, P, pel, prot, spread_thigh=9.0, spread_shin=15.0):
    back = qz(YAW_B) @ Vector((0, 1, 0))
    for s, sg in (("L", 1.0), ("R", -1.0)):
        hip = pel + prot @ (R.hj0[s] - R.h0)
        dz = hip.z - 0.072
        hl = math.sqrt(max(1e-6, R.thigh ** 2 - dz ** 2))
        d1 = spread(back, -sg * spread_thigh)
        knee = hip + d1 * hl
        knee.z = 0.072
        d2 = spread(back, -sg * spread_shin)
        az = 0.10
        sl = math.sqrt(max(1e-6, R.shin ** 2 - (knee.z - az) ** 2)) * 0.995
        ankle = knee + d2 * sl
        ankle.z = az
        lat = spread(back, -sg * 90.0)
        tf = (lat * 0.70 + back * 0.45 - UP * 0.42).normalized()      # 발끝은 바깥 · 뒤, 발 안쪽 날이 땅에
        m_rest = frame(Vector((0, -1, 0)), Vector((-sg, 0, 0)))
        m_new = frame(tf, -UP * 0.8 + back * 0.2)
        q = (m_new @ m_rest.inverted()).to_quaternion()
        P["foot_" + s] = (ankle, q)
        P["toe_" + s] = q
        P["knee_" + s] = knee + (lat * 0.6 - UP * 1.0) * 1.2
    return P


CHEST_YAW = -38.0   # 가슴은 골반보다 더 돌림: 왼 어깨가 앞으로 나와 왼손이 총열덮개에 닿음(모델 팔 0.43 m, M1은 실물 1.10 m)
GRIP_SLIDE_L = -0.10


def prone_pose(R, base, cp, pel_z=0.118, rifle_drop=0.0):
    P = copy_pose(base)
    pel = Vector((0.07, -0.47, pel_z))
    P["pelvis_rot"] = rot(pitch=87, yaw=YAW_B, roll=4)
    P["pelvis_loc"] = pel
    P["chest_rot"] = rot(pitch=cp, yaw=CHEST_YAW, roll=-6)
    P["head_rot"] = rot(pitch=24, yaw=-3, roll=-12)
    P["neck_share"] = 0.65
    prone_legs(R, P, pel, P["pelvis_rot"])
    P["grip_L"], P["grip_R"] = 1.0, 1.0
    P["grip_slide_L"] = GRIP_SLIDE_L
    P["hand_space_L"] = P["hand_space_R"] = "chest"
    P["fingers_L"], P["fingers_R"], P["thumb_L"], P["thumb_R"], P["index_R"] = 0.7, 0.8, 0.35, 0.5, 0.45
    P["shoulder_R"] = rot(pitch=0, yaw=0, roll=0)
    P["rifle_space"] = 0.0
    R.apply(P)
    sh = joint(R, "DEF-upper_arm.R")
    aim = Vector((0.0, -1.0, -0.035)).normalized()
    P["rifle"] = rok_poses.rifle_from_butt(R, sh + Vector((0.035, -0.035, -0.02 - rifle_drop)), aim, up=(0, 0, 1), roll=0)
    shL = joint(R, "DEF-upper_arm.L")
    P = elbow_world(R, P, "L", shL + Vector((-0.20, -0.20, -1.0)))
    P = elbow_world(R, P, "R", sh + Vector((-0.75, 0.05, -0.62)))
    return P


def prone_final(R, base):
    """가슴 숙임(=가슴 높이)을 골라 왼 팔꿈치가 땅에 닿게(관절 중심 4.5 cm) 하고, 골반 높이는 배가 땅에 닿게."""
    def score(P):
        R.apply(P)
        eL = R.pb["DEF-forearm.L"].head
        eR = R.pb["DEF-forearm.R"].head
        reach = (R.pb["DEF-hand.L"].head - R.pb["grip.L"].matrix.translation).length
        return abs(eL.z - 0.058) + 0.5 * max(0.0, eR.z - 0.07) + 2.0 * max(0.0, 0.048 - eR.z) + 5.0 * reach, eL, eR

    cp, pz = 84.0, 0.118
    for rnd in range(2):
        # 배(골반 둘레)가 땅에 닿는 골반 높이 → 그 높이에서 가슴 각 찾기
        P = prone_pose(R, base, cp, pel_z=pz)
        low = mesh_low(R, P, near=(P["pelvis_loc"].x, P["pelvis_loc"].y), radius=0.28)
        pz = pz - low + 0.004
        best = None
        for c in range(56, 96, 2):
            err, eL, eR = score(prone_pose(R, base, c, pel_z=pz))
            if best is None or err < best[0]:
                best = (err, c, eL.z, eR.z)
        cp = best[1]
        for it in range(2):
            for c2 in (cp - 1.5, cp - 0.75, cp, cp + 0.75, cp + 1.5):
                err, eL, eR = score(prone_pose(R, base, c2, pel_z=pz))
                if err < best[0]:
                    best = (err, c2, eL.z, eR.z)
            cp = best[1]
    P = prone_pose(R, base, cp, pel_z=pz)
    print("PRONE chest pitch", cp, "elbow L/R z", round(best[2], 3), round(best[3], 3), "pelvis z", round(pz, 3))
    return P, cp, pz


def prone_clips(R, L, clips):
    stand = L["stand"]
    base = R.base_pose()
    Pr, cp, pz = prone_final(R, base)
    Pr_c = rok_poses.chestify(R, copy_pose(with_(Pr, rifle_space=1.0)), keys=("rifle",))
    # 다리 · 무릎(무릎은 내려앉은 자리에서 거의 움직이지 않는다)
    kL_end = None
    R.apply(Pr)
    kneeP = {s: joint(R, "DEF-shin." + s) for s in ("L", "R")}
    # K1: 오른 무릎 먼저(조심스럽게): 왼발 조금 앞, 오른 무릎은 엎드린 자세의 무릎 자리 가까이
    K1 = copy_pose(stand)
    knee_R = Vector((kneeP["R"].x + 0.02, kneeP["R"].y + 0.02, 0.062))
    ankle_R, qf, qt, ball_R = R.kneel_leg("R", knee_R, (0.10, 1.0))
    K1["foot_R"], K1["toe_R"] = (ankle_R, qf), qt
    K1["knee_R"] = knee_R + Vector((-0.1, -1.5, -0.2))
    K1["pelvis_rot"] = rot(pitch=10, yaw=-14, roll=-2)
    u = Vector((0.05, 0.22, 0.0))
    u.z = math.sqrt(1 - u.x ** 2 - u.y ** 2)
    K1["pelvis_loc"] = R.pelvis_from_hip("R", knee_R + R.thigh * u, K1["pelvis_rot"])
    aL = Vector((0.13, kneeP["L"].y - 0.30, R.ankle0["L"].z))
    K1["foot_L"], K1["toe_L"] = (aL, qz(4)), qz(4)
    K1["knee_L"] = Vector((0.3, -1.6, 0.7))
    K1["chest_rot"] = rot(pitch=18, yaw=-10, roll=0)
    K1["head_rot"] = rot(pitch=22, yaw=-2)
    # K2: 두 무릎 (발은 발가락을 세움)
    K2 = copy_pose(K1)
    knee_L = Vector((kneeP["L"].x - 0.03, kneeP["L"].y + 0.02, 0.062))
    ankle_L, qfl, qtl, _ = R.kneel_leg("L", knee_L, (-0.05, 1.0))
    K2["foot_L"], K2["toe_L"] = (ankle_L, qfl), qtl
    K2["knee_L"] = knee_L + Vector((0.1, -1.5, -0.2))
    K2["pelvis_rot"] = rot(pitch=14, yaw=-10, roll=0)
    mid_knee = (knee_L + knee_R) * 0.5
    hipmid = mid_knee + R.thigh * Vector((0.0, 0.12, 0.99)).normalized()
    K2["pelvis_loc"] = hipmid - K2["pelvis_rot"] @ ((R.hj0["L"] + R.hj0["R"]) * 0.5 - R.h0)
    K2["chest_rot"] = rot(pitch=24, yaw=-8)
    K2["head_rot"] = rot(pitch=30, yaw=-2)

    def hip_arc(P, a_deg, pitch, yaw):
        """무릎을 축으로 허벅지를 앞으로 눕힘(a = 수직에서 기운 각)."""
        a = math.radians(a_deg)
        fwd = qz(YAW_B * min(1.0, a_deg / 70.0)) @ Vector((0, -1, 0))
        hip = mid_knee + R.thigh * (fwd * math.sin(a) + UP * math.cos(a))
        P["pelvis_rot"] = rot(pitch=pitch, yaw=yaw)
        P["pelvis_loc"] = hip - P["pelvis_rot"] @ ((R.hj0["L"] + R.hj0["R"]) * 0.5 - R.h0)
        return P

    # K3: 앞으로 숙여 왼손을 땅에 짚음, 오른손은 총을 든 채(총구 앞 위)
    K3 = copy_pose(K2)
    K3 = hip_arc(K3, 24, 30, -10)
    K3["chest_rot"] = rot(pitch=62, yaw=-12)
    K3["head_rot"] = rot(pitch=45, yaw=-4)
    K3["grip_L"] = 0.0
    R.apply(K3)
    shL = joint(R, "DEF-upper_arm.L")
    shR = joint(R, "DEF-upper_arm.R")
    handG = Vector((shL.x + 0.02, shL.y - 0.22, 0.030))
    K3 = hand_world(R, K3, "L", handG, (-0.15, -1.0, 0.0), (0, 0, 1), "world")
    K3["fingers_L"], K3["thumb_L"] = 0.12, 0.1
    K3 = elbow_world(R, K3, "L", shL + Vector((0.45, 0.2, -0.1)))
    # 총: 오른손으로, 오른쪽 몸 옆에서 총구를 앞 위로
    wr = shR + Vector((-0.08, -0.12, -0.32))
    K3["rifle"] = rok_poses.rifle_from_right(R, wr, (0.05, -1.0, 0.35), up=(0.3, 0, 1), roll=-10)
    K3["rifle_space"] = 0.0
    K3 = elbow_world(R, K3, "R", shR + Vector((-0.5, 0.2, -0.2)))
    # K4: 몸을 내림: 허벅지가 거의 누움, 왼 팔꿈치가 땅으로, 왼손은 아직 땅
    K4 = copy_pose(K3)
    K4 = hip_arc(K4, 66, 70, YAW_B * 0.8)
    K4["pelvis_loc"] = K4["pelvis_loc"] + Vector((0, 0, 0.02))
    K4["chest_rot"] = rot(pitch=cp + 4, yaw=YAW_B * 0.4, roll=-2)
    K4["head_rot"] = rot(pitch=40, yaw=-3, roll=-4)
    prone_legs(R, K4, K4["pelvis_loc"], K4["pelvis_rot"], spread_thigh=6.0, spread_shin=10.0)
    for s in ("L", "R"):          # 발은 아직 발가락을 세운 채
        a, q = K4["foot_" + s]
        K4["foot_" + s] = (Vector((a.x, a.y, K2["foot_" + s][0].z + 0.01)), K2["foot_" + s][1].slerp(q, 0.1))
        K4["toe_" + s] = K2["toe_" + s].slerp(q, 0.1)
    R.apply(Pr)
    gl_end = world_hand_at_current_grip(R, Pr, "L")
    K4["hand_L"] = (gl_end[0] + Vector((0.0, 0.02, -0.06)), gl_end[1])
    K4["hand_space_L"] = "world"
    K4["fingers_L"], K4["thumb_L"] = 0.35, 0.2
    R.apply(K4)
    shR4 = joint(R, "DEF-upper_arm.R")
    K4["rifle"] = rok_poses.rifle_from_butt(R, shR4 + Vector((-0.03, -0.20, -0.06)), (0.0, -1.0, 0.06), up=(0.15, 0, 1), roll=0)
    K4 = elbow_world(R, K4, "L", joint(R, "DEF-upper_arm.L") + Vector((-0.20, -0.20, -1.0)))
    K4 = elbow_world(R, K4, "R", shR4 + Vector((-0.6, 0.1, -0.5)))
    # K5: 총을 왼손에 얹고(왼손 다시 쥠) 개머리판을 어깨로, 오른 팔꿈치는 아직 조금 듦
    K5 = copy_pose(Pr)
    K5["pelvis_loc"] = Pr["pelvis_loc"] + Vector((0, 0.0, 0.008))
    K5["chest_rot"] = qx(-2) @ Pr["chest_rot"]
    K5["head_rot"] = rot(pitch=14, yaw=-3, roll=-6)
    loc, q = Pr["rifle"]
    K5["rifle"] = (loc + Vector((0.0, -0.03, -0.015)), q)
    K5["hand_L"], K5["hand_space_L"] = K4["hand_L"], "world"
    K5 = elbow_world(R, K5, "R", joint(R, "DEF-upper_arm.R") + Vector((-0.55, 0.05, -0.35)))
    Pr["hand_L"], Pr["hand_space_L"] = K4["hand_L"], "world"
    K0 = rifle_to_world(R, stand)
    K0["hand_L"], K0["hand_space_L"] = K3["hand_L"], "world"
    K1 = rifle_to_world(R, K1)
    K1["hand_L"], K1["hand_space_L"] = K3["hand_L"], "world"
    K2 = rifle_to_world(R, K2)
    K2["hand_L"], K2["hand_space_L"] = K3["hand_L"], "world"
    # 왼손을 떼는 키: K2→K3 사이에서 grip_L 1 → 0 (떼는 순간의 손잡이 자리를 목표로)
    K2b = copy_pose(K2)
    K2b = moved(K2b, chest_rot=qx(12), head_rot=qx(8), pelvis=(0, -0.02, -0.01))
    K2b["hand_L"] = world_hand_at_current_grip(R, K2b, "L")
    K2b["grip_L"] = 0.0
    K2["hand_L"] = K2b["hand_L"]
    K3["hand_L"] = K3["hand_L"]
    keys = [
        Key(0.0, K0),
        Key(0.42, K1, "inout", lift_L=0.03, lift_R=0.06),
        Key(0.66, K2, "inout", lift_L=0.06),
        Key(0.80, K2b, "inout"),
        Key(1.05, K3, "inout"),
        Key(1.42, K4, "inout", lift_L=0.03, lift_R=0.03),
        Key(1.74, K5, "inout", lift_L=0.04, lift_R=0.04),
        Key(2.0, Pr, "soft"),
    ]
    clip = Clip("A_ROK_GoProne_In", 2.0, tl_sampler(keys, 2.0, lags={"chest": 0.05, "head": 0.09, "arms": 0.05}),
                start_ref="A_ROK_Idle_Stand", end_ref="A_ROK_Prone_Aim_Loop",
                note="오른 무릎 → 왼 무릎 → 왼손 짚기 → 몸을 내려 왼 팔꿈치 → 견착 → 오른 팔꿈치 (FM 23-5 113항 순서, 개머리판 대신 손으로 짚음)")
    clip.ground, clip.views, clip.cam, clip.mesh_check = True, ["side", "front", "qf", "top"], ((0, -0.62, 0.62), 2.7), True
    clip.step = 0.1
    clips[clip.name] = clip

    T = 4.0

    def loop(t):
        P = copy_pose(Pr)
        b = (1 - math.cos(2 * math.pi * t / (T / 2))) / 2            # 2초에 한 번 숨
        P["chest_rot"] = qx(-1.3 * b) @ P["chest_rot"]
        P["pelvis_loc"] = P["pelvis_loc"] + Vector((0, 0, 0.0025 * b))
        w = math.sin(2 * math.pi * t / T)
        P["rifle"] = (P["rifle"][0] + Vector((0.003 * w, 0, 0.002 * b)), P["rifle"][1])
        P["head_rot"] = qz(0.8 * w) @ qx(0.6 * b) @ P["head_rot"]
        g = math.sin(math.pi * min(1.0, max(0.0, (t - 2.6) / 0.8))) ** 2
        P["index_R"] = Pr["index_R"] + 0.15 * g                       # 방아쇠 손가락을 한 번 고쳐 댐
        return P

    clip = Clip("A_ROK_Prone_Aim_Loop", T, loop, loop=True,
                note="엎드려 겨눈 채 숨(2초에 한 번) · 총구 미세한 흔들림 · 방아쇠 손가락 고쳐 대기")
    clip.ground, clip.views, clip.cam, clip.mesh_check = True, ["side", "front", "qf", "top"], ((0, -0.62, 0.30), 2.7), True
    clip.step = 1.0
    clips[clip.name] = clip
    return Pr


# ------------------------------------------------------------------ 달리기 출발 (다시)
def start_clip(R, L, clips, rbase, spec):
    stand = L["stand"]
    T = 0.5
    run0, _ = rok_run.run_pose(R, rbase, spec, 0.0)
    t0 = 0.05

    def speed(t):
        return spec.speed * ease(max(0.0, min(1.0, (t - t0) / (T - t0))), "inout")

    N = 200
    trav = [0.0]
    for i in range(1, N + 1):
        a, b = (i - 1) * T / N, i * T / N
        trav.append(trav[-1] + 0.5 * (speed(a) + speed(b)) * (b - a))

    def travel(t):
        i = max(0, min(N, int(t / T * N)))
        return trav[i]

    fnL = lambda tt: rok_run.run_pose(R, rbase, spec, tt)[0]["foot_L"][0]
    fnR = lambda tt: rok_run.run_pose(R, rbase, spec, tt)[0]["foot_R"][0]
    aL1, vL1 = fnL(0.0), rok_run.velocity(fnL, -1 / 240.0, dt=1 / 240.0)
    aR1, vR1 = fnR(0.0), rok_run.velocity(fnR, 0.0)
    aL0, qL0, tL0 = rok_run.foot_state(R, stand, "L")
    aR0, qR0, tR0 = rok_run.foot_state(R, stand, "R")
    yawL = math.degrees(qL0.to_euler("XYZ").z)
    yawR = math.degrees(qR0.to_euler("XYZ").z)
    bL = rok_run.flat_ball(R, "L", aL0, yawL)
    bR = rok_run.flat_ball(R, "R", aR0, yawR)
    tL_off, tR_off = 0.11, 0.33
    # 왼발: 뒤꿈치 들고 0.11초에 떠나 무릎을 앞으로 끌어올리며 크게 나가 0.5초에 달리기 첫 착지 자리로
    aoffL, qoffL, _ = rok_run.ground_foot(R, "L", (bL.x, bL.y + travel(tL_off)), 30.0, yawL)
    ptsL = [(tL_off, aoffL, Vector((0, 0.4, 0.9))),
            (0.27, Vector((0.10, -0.25, 0.22)), Vector((0, -2.4, 0.30))),
            (T - 0.10, fnL(-0.10), rok_run.velocity(fnL, -0.10)),
            (T, aL1, vL1)]
    th_end_R = 58.0
    aoffR, qoffR, _ = rok_run.ground_foot(R, "R", (bR.x, bR.y + travel(tR_off)), th_end_R, yawR * 0.25)
    ptsR = [(tR_off, aoffR, Vector((0, speed(tR_off), 0.9))), (T, aR1, vR1)]
    qL_land = run0["foot_L"][1]

    def sample(t):
        # 몸통: 처음 0.05초는 그대로, 그 뒤 달리기 자세로 (골반 먼저, 가슴 · 머리 · 팔 순서)
        w = {"base": ease(max(0.0, (t - 0.03) / (T - 0.03)), "inout"),
             "chest": ease(max(0.0, (t - 0.05) / (T - 0.05)), "inout"),
             "head": ease(max(0.0, (t - 0.08) / (T - 0.08)), "inout"),
             "arms": ease(max(0.0, (t - 0.06) / (T - 0.10)), "inout")}
        P = blend_groups(stand, run0, w)
        # 앞으로 기울이며 밀어 냄 (0.1~0.35초에 가장 큼)
        k = math.sin(math.pi * min(1.0, max(0.0, (t - 0.02) / 0.42))) ** 1.5
        P["chest_rot"] = qx(14 * k) @ P["chest_rot"]
        P["pelvis_rot"] = qx(7 * k) @ P["pelvis_rot"]
        P["head_rot"] = qx(-4 * k) @ P["head_rot"]
        pl = P["pelvis_loc"]
        P["pelvis_loc"] = Vector((pl.x - 0.022 * math.sin(math.pi * min(1.0, t / 0.36)), pl.y - 0.05 * k,
                                  pl.z + 0.010 * math.sin(math.pi * min(1.0, t / 0.40)) ** 2))
        # 왼발
        if t <= tL_off:
            th = 30.0 * ease(max(0.0, (t - 0.03) / (tL_off - 0.03)), "in")
            a, q, toe = rok_run.ground_foot(R, "L", (bL.x, bL.y + travel(t)), th, yawL)
            P["foot_L"], P["toe_L"] = (a, q), toe
        else:
            u = (t - tL_off) / (T - tL_off)
            a = path(ptsL, t)
            ths = curve([(0.0, 30.0), (0.35, 40.0), (0.65, 12.0), (1.0, -6.0)], u)
            q = qz(yawL * (1 - ease(u))) @ qx(ths)
            if u > 0.85:
                q = q.slerp(qL_land, ease((u - 0.85) / 0.15))
            if u < 0.3:
                toe = qz(yawL).slerp(q, ease(u / 0.3))
            else:
                toe = q.slerp(run0["toe_L"], ease((u - 0.3) / 0.7))
            P["foot_L"], P["toe_L"] = (a, q), toe
        # 오른발 (디딘 발): 뒤로 밀리며 발볼로 돌아서고 0.37초에 차고 나감
        if t <= tR_off:
            th = th_end_R * ease(max(0.0, (t - 0.06) / (tR_off - 0.06)), "in")
            yaw = yawR + (yawR * 0.25 - yawR) * ease(max(0.0, (t - 0.05) / (tR_off - 0.05)))
            a, q, toe = rok_run.ground_foot(R, "R", (bR.x, bR.y + travel(t)), th, yaw)
            P["foot_R"], P["toe_R"] = (a, q), toe
        else:
            u = (t - tR_off) / (T - tR_off)
            a = path(ptsR, t)
            P["foot_R"] = (a, qoffR.slerp(run0["foot_R"][1], ease(u)))
            P["toe_R"] = qz(yawR * 0.25).slerp(run0["toe_R"], ease(u))
        for s in ("L", "R"):
            a = P["foot_" + s][0]
            P["knee_" + s] = Vector((a.x * 1.3, -1.5, 0.55))
        # 마지막 0.1초: 달리기 주기의 같은 순간 자세로 섞어 들어감(첫 착지에서 속도까지 이어짐)
        if t > T - 0.10:
            wt = ease((t - (T - 0.10)) / 0.10, "inout")
            runP, _ = rok_run.run_pose(R, rbase, spec, t - T)
            P = blend_groups(P, runP, {"base": wt, "chest": wt, "head": wt, "arms": wt})
        return P

    clip = Clip("A_ROK_Run_Start", T, sample, view="run", start_ref="A_ROK_Idle_Stand", end_ref="A_ROK_Run_Rifle",
                note="앞으로 기울며 왼발을 크게 내딛고(무릎을 앞으로) 오른발로 밀어 내 달리기 첫 착지로", reach_fix=True)
    clip.spec = spec
    clip.root_travel = travel
    clip.mesh_check = False
    clip.next_prev = ("A_ROK_Run_Rifle 마지막 직전 프레임(20)", "A_ROK_Run_Rifle", spec.frames - 1)
    clip.end_drop_ref = ("A_ROK_Run_Rifle", 0)
    clips[clip.name] = clip


# ------------------------------------------------------------------ 달리다 무릎 사격
def run_to_fire_clip(R, L, clips, rbase, spec):
    fire = L["fire_crouch"]
    T = 0.8
    t_half = spec.period * 0.5
    runH, _ = rok_run.run_pose(R, rbase, spec, t_half)       # 오른발 착지 순간 (Run_Rifle 11번째 프레임)
    t_stop = 0.42
    v0 = spec.speed

    def travel(t):
        t = min(t, t_stop)
        return v0 * t - 0.5 * (v0 / t_stop) * t * t

    fnL = lambda tt: rok_run.run_pose(R, rbase, spec, tt)[0]["foot_L"][0]
    aL0 = fnL(t_half)
    vL0 = rok_run.velocity(fnL, t_half)
    aRl, qRl, tRl = rok_run.foot_state(R, runH, "R")
    aLf, qLf, tLf = rok_run.foot_state(R, fire, "L")
    aRf, qRf, tRf = rok_run.foot_state(R, fire, "R")
    t_land = 0.28
    yawLf = math.degrees(qLf.to_euler("XYZ").z)
    ballLf = rok_run.flat_ball(R, "L", aLf, yawLf)
    ballR0 = Vector((-spec.foot_x, spec.ball_land))
    land_L, qL_land, _ = planted(R, "L", (ballLf.x, ballLf.y - (travel(t_stop) - travel(t_land))), -8.0, yawLf)
    qL_start, tL_start = runH["foot_L"][1], runH["toe_L"]
    ptsL = [(0.0, aL0, vL0),
            (0.16, Vector((aLf.x * 0.8, (aL0.y + land_L.y) * 0.5 - 0.10, 0.33)), Vector((0, -3.6, -0.3))),
            (t_land, land_L, Vector((0, v0 - (v0 / t_stop) * t_land, -0.5)))]
    t_lift_R = 0.30

    def sample(t):
        w = {"base": ease(min(1.0, t / 0.72), "inout"),
             "chest": ease(min(1.0, max(0.0, (t - 0.03) / 0.70)), "inout"),
             "head": ease(min(1.0, max(0.0, (t - 0.06) / 0.68)), "inout"),
             "arms": ease(min(1.0, max(0.0, (t - 0.14) / 0.50)), "inout")}
        P = blend_groups(runH, fire, w)
        # 브레이크: 몸이 뒤로 버팀(0.1~0.25초)
        k = math.sin(math.pi * min(1.0, t / 0.36))
        P["chest_rot"] = qx(-7 * k) @ P["chest_rot"]
        P["pelvis_rot"] = qx(-4 * k) @ P["pelvis_rot"]
        z = curve([(0.0, runH["pelvis_loc"].z), (0.14, runH["pelvis_loc"].z - 0.05), (0.30, 0.64), (0.56, fire["pelvis_loc"].z + 0.035),
                   (0.66, fire["pelvis_loc"].z - 0.004), (T, fire["pelvis_loc"].z)], t)
        P["pelvis_loc"] = Vector((P["pelvis_loc"].x, P["pelvis_loc"].y, z))
        # 오른발: 착지해 디딘 채 뒤로(브레이크) → 0.18초부터 뒤꿈치를 들고 0.30초부터 발볼로 서며 무릎을 내림
        if t <= t_lift_R:
            if t < 0.07:
                th = -6.0 * (1 - ease(t / 0.07, "out"))
            elif t < 0.18:
                th = 0.0
            else:
                th = 20.0 * ease((t - 0.18) / (t_lift_R - 0.18), "in")
            a, q, toe = planted(R, "R", (ballR0.x, ballR0.y + travel(t)), th)
            P["foot_R"], P["toe_R"] = (a, q), toe
        else:
            # 발볼을 축으로 뒤꿈치를 세우며(20 → 76도) 발볼은 무릎 꿇는 자리로 조금 끌려감
            u = ease(min(1.0, (t - t_lift_R) / (0.58 - t_lift_R)), "inout")
            b0 = Vector((ballR0.x, ballR0.y + travel(t_lift_R)))
            yawf = math.degrees(qRf.to_euler("XYZ").z)
            bf = rok_run.flat_ball(R, "R", aRf, 0.0) if False else None
            a1, q1, t1 = planted(R, "R", (b0.x, b0.y), 20.0)
            # 무릎 꿇은 발의 발볼 자리 (kneel_leg와 같은 식)
            # 11-18 밤: 끝 자세의 꿇은 발은 발등을 눕힌 발이라, 먼저 발끝 세운 발(TUCK)로 무릎을 내린 뒤 0.58초부터 들어 뒤집어 눕힌다
            aT, qT = rok_poses.TUCK["foot_R"]
            tT = rok_poses.TUCK["toe_R"]
            ballf = aT - qT @ (R.ankle0["R"] - R.ball0["R"])
            # 디딘 발볼은 루트가 멈출 때까지 루트 이동만큼 뒤로 가고, 남은 차(옆 5 cm · 뒤 2 cm)만 천천히 옮김
            bplant = Vector((ballR0.x, ballR0.y + travel(t)))
            bend = Vector((ballR0.x, ballR0.y + travel(t_stop)))
            bxy = bplant + (Vector((ballf.x, ballf.y)) - bend) * u
            a, q, toe = planted(R, "R", (bxy.x, bxy.y), 20.0 + 56.0 * u, yawf * u)
            if u > 0.7:   # 무릎이 닿을 때 발끝 세운 꿇은 발과 정확히 같게
                w = ease((u - 0.7) / 0.3)
                a, q, toe = a.lerp(aT, w), q.slerp(qT, w), toe.slerp(tT, w)
            a = a + Vector((0, 0, 0.012 * math.sin(math.pi * u)))
            if t > 0.58:  # 무릎이 땅에 닿은 뒤 발을 들어 뒤집어 발등을 눕힘(서기 → 낮춘 대기와 같은 순서)
                w = ease(min(1.0, (t - 0.58) / 0.18), "inout")
                a = aT.lerp(aRf, w) + Vector((0, 0, FLIP_LIFT * math.sin(math.pi * w)))
                q, toe = qT.slerp(qRf, w), tT.slerp(tRf, w)
            P["foot_R"], P["toe_R"] = (a, q), toe
        # 왼발: 뒤에서 앞으로 와 0.28초에 디딤 → 남은 속도만큼 뒤로 가서 무릎 사격 자리에 멈춤
        if t <= t_land:
            a = path(ptsL, t)
            u = t / t_land
            if u < 0.5:
                q = qL_start.slerp(qx(14), ease(u / 0.5))
            else:
                q = qx(14).slerp(qL_land, ease((u - 0.5) / 0.5))
            toe = tL_start.slerp(q, ease(min(1.0, u / 0.4)))
            P["foot_L"], P["toe_L"] = (a, q), toe
        else:
            th = -8.0 * (1 - ease(min(1.0, (t - t_land) / 0.08), "out"))
            a, q, toe = planted(R, "L", (ballLf.x, ballLf.y - (travel(t_stop) - travel(t))), th, yawLf)
            P["foot_L"], P["toe_L"] = (a, q), toe
        # 무릎 방향
        P["knee_L"] = Vector((P["foot_L"][0].x * 1.3, -1.5, 0.55)).lerp(fire["knee_L"], ease(min(1.0, t / 0.6)))
        P["knee_R"] = Vector((P["foot_R"][0].x * 1.3, -1.5, 0.55)).lerp(fire["knee_R"], ease(min(1.0, max(0.0, (t - 0.2) / 0.4))))
        # 처음 0.1초: 달리기 주기의 같은 순간에서 섞어 나옴(디딘 오른발은 브레이크 그대로)
        if t < 0.10:
            wh = 1.0 - ease(t / 0.10, "inout")
            runP, _ = rok_run.run_pose(R, rbase, spec, t_half + t)
            keep = P["foot_R"], P["toe_R"], P["pelvis_loc"].z
            P = blend_groups(P, runP, {"base": wh, "chest": wh, "head": wh, "arms": wh})
            P["foot_R"], P["toe_R"] = keep[0], keep[1]
            P["pelvis_loc"] = Vector((P["pelvis_loc"].x, P["pelvis_loc"].y, keep[2]))
        return P

    clip = Clip("A_ROK_Run_To_FireCrouch", T, sample, view="run", start_ref=None, end_ref="A_ROK_Idle_Trench",
                note="오른발로 브레이크, 왼발을 앞에 디디며 오른 무릎을 내리고 소총을 견착", reach_fix=True)
    clip.spec = spec
    clip.root_travel = travel
    clip.mesh_check = True
    clip.cam_run = ((0, -0.15, 0.70), 2.05)
    clip.start_pose = ("A_ROK_Run_Rifle 11번째 프레임(오른발 착지)", "A_ROK_Run_Rifle", spec.frames // 2)
    clip.start_next = ("A_ROK_Run_Rifle 12번째 프레임", "A_ROK_Run_Rifle", spec.frames // 2 + 1)
    clip.start_drop_ref = ("A_ROK_Run_Rifle", spec.frames // 2)
    clips[clip.name] = clip


NEW_NAMES = ["A_ROK_Kneel_Mourn_In", "A_ROK_Kneel_Mourn_Loop", "A_ROK_GoProne_In", "A_ROK_Prone_Aim_Loop",
             "A_ROK_Run_Start", "A_ROK_Run_To_FireCrouch"]


def add(R, L, clips, rbase, spec):
    mourn_clips(R, L, clips)
    prone_clips(R, L, clips)
    start_clip(R, L, clips, rbase, spec)
    run_to_fire_clip(R, L, clips, rbase, spec)
    for n in NEW_NAMES:
        clips[n].new = True
    clips["A_ROK_GoProne_In"].res = clips["A_ROK_Prone_Aim_Loop"].res = (330, 220)
    clips["A_ROK_GoProne_In"].views = clips["A_ROK_Prone_Aim_Loop"].views = ["side", "front", "qf", "topx"]
    return NEW_NAMES
