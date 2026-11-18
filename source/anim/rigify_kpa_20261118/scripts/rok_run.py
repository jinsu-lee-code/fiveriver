# 소총 든 달리기 사이클 (제자리). 발은 접지 구간 동안 땅에 붙어 일정 속도로 뒤로 간다(미끄러짐 없음).
# 위상 φ: 0 = 왼발 착지, 0.5 = 오른발 착지.
import math

from mathutils import Quaternion, Vector

from rok_rig import rot, gun_rot, copy_pose, ease
from rok_timeline import qx, qy, qz

TAU = 2 * math.pi


class RunSpec:
    def __init__(self, **kw):
        self.frames = 20            # 한 주기 (2걸음)
        self.speed = 3.0            # 접지 발이 뒤로 가는 속도 m/s (게임 크기 병사)
        self.contact = 0.30         # 한 주기 중 한 발이 땅에 있는 비율
        self.ball_land = -0.27      # 착지 순간 발볼 위치 (골반 기준 앞 -)
        self.foot_x = 0.095         # 발 좌우 간격(반)
        self.hip_z = 0.795          # 골반 평균 높이
        self.bob = 0.028            # 골반 상하 진폭(반)
        self.sway = 0.016           # 골반 좌우
        self.pelvis_pitch = 9.0
        self.chest_pitch = 17.0
        self.pelvis_yaw = 6.0
        self.pelvis_roll = 4.0
        self.chest_yaw = 4.0
        self.head_pitch = 6.0
        self.swing_high = 0.33      # 지나갈 때 발목 높이
        self.kick = 0.40            # 뒤로 찬 뒤꿈치(발목) 높이
        self.heel_lift = 40.0       # 발 떼기 직전 뒤꿈치 드는 각
        self.rifle_bounce = 0.012
        self.rifle_roll = 3.0
        self.lean_hit = 2.0         # 착지 뒤 가슴이 더 숙여지는 양(도)
        self.__dict__.update(kw)

    @property
    def period(self):
        return self.frames / 30.0


def foot_frame(R, side, theta, yaw=0.0):
    return Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((1, 0, 0), math.radians(theta))


def stance_foot(R, spec, side, s):
    """접지 구간 s(0~1): (발목 위치, 발 회전, 발가락 회전, 접지점)."""
    travel = spec.speed * spec.contact * spec.period
    x = spec.foot_x if side == "L" else -spec.foot_x
    ball = Vector((x, spec.ball_land + travel * s, R.ball0[side].z))
    # 발 각도: 착지 순간 발끝이 6도 들림 → 평평 → 뒤꿈치 듦
    if s < 0.18:
        theta = -6.0 * (1 - ease(s / 0.18, "out"))
    elif s < 0.5:
        theta = 0.0
    else:
        theta = spec.heel_lift * ease((s - 0.5) / 0.5, "in")
    q = foot_frame(R, side, theta)
    off = R.ankle0[side] - R.ball0[side]
    off.x = 0.0
    if theta >= 0:
        ankle = ball + q @ off
        contact = ball
    else:
        heel0 = Vector((0.0, R.ankle0[side].y - R.ball0[side].y + 0.05, -R.ball0[side].z))   # 발볼 기준 뒤꿈치 바닥점
        heel = ball + heel0
        ankle = heel + q @ (off - heel0)
        contact = heel
    toe = Quaternion()     # 발가락은 땅에 평평
    return ankle, q, toe, contact, theta


def hermite(p0, m0, p1, m1, u):
    h00 = 2 * u ** 3 - 3 * u ** 2 + 1
    h10 = u ** 3 - 2 * u ** 2 + u
    h01 = -2 * u ** 3 + 3 * u ** 2
    h11 = u ** 3 - u ** 2
    return p0 * h00 + m0 * h10 + p1 * h01 + m1 * h11


def swing_foot(R, spec, side, s):
    """공중 구간 s(0~1): 뒤로 찬 뒤 앞으로 지나가 착지점으로."""
    a0, q0, _, _, th0 = stance_foot(R, spec, side, 1.0)
    a1, q1, _, _, th1 = stance_foot(R, spec, side, 0.0)
    T = (1 - spec.contact) * spec.period
    v = Vector((0, spec.speed, 0))
    x = a0.x
    # 경유점: 뒤로 높이 찬 곳 → 엉덩이 아래 높이 지나감 → 앞으로 뻗었다 내려옴
    pts = [(0.0, a0, v * T * 0.9 + Vector((0, 0, 0.9))),
           (0.30, Vector((x, a0.y + 0.10, spec.kick)), Vector((0, -0.3, 0.25))),
           (0.62, Vector((x, (a0.y + a1.y) * 0.5 - 0.12, spec.swing_high)), Vector((0, -2.2, -0.15))),
           (0.86, Vector((x, a1.y - 0.07, a1.z + 0.07)), Vector((0, -0.4, -0.55))),
           (1.0, a1, v * T * 0.8 + Vector((0, 0, -0.25)))]
    for (sa, pa, ma), (sb, pb, mb) in zip(pts, pts[1:]):
        if s <= sb:
            u = (s - sa) / (sb - sa)
            d = sb - sa
            ankle = hermite(pa, ma * d, pb, mb * d, u)
            break
    # 발 각도: 뒤꿈치 든 채 떠나 더 펴졌다가(발끝 아래) 앞으로 오며 발끝 듦
    keys = [(0.0, th0), (0.22, th0 + 12), (0.55, 18.0), (0.85, -10.0), (1.0, th1)]
    for (sa, ta), (sb, tb) in zip(keys, keys[1:]):
        if s <= sb:
            theta = ta + (tb - ta) * ease((s - sa) / (sb - sa), "inout")
            break
    q = foot_frame(R, side, theta)
    # 발가락: 떼는 순간 땅에 평평(발에 대해 -th0) → 곧 발과 나란히
    bend = -th0 * (1 - ease(min(1.0, s / 0.3), "out"))
    toe = q @ Quaternion((1, 0, 0), math.radians(bend))
    return ankle, q, toe


def run_pose(R, base, spec, t):
    """한 주기 t(초) → 자세 값."""
    phi = (t / spec.period) % 1.0
    P = copy_pose(base)
    contact = {}
    for side, ph0 in (("L", 0.0), ("R", 0.5)):
        psi = (phi - ph0) % 1.0
        if psi < spec.contact:
            ankle, q, toe, c, th = stance_foot(R, spec, side, psi / spec.contact)
            contact[side] = c
        else:
            ankle, q, toe = swing_foot(R, spec, side, (psi - spec.contact) / (1 - spec.contact))
        P["foot_" + side] = (ankle, q)
        P["toe_" + side] = toe
    # 골반: 접지 중간에 가장 낮고, 공중에서 가장 높음. 디딘 발 쪽으로 옮겨 감
    mid = spec.contact / 2
    z = spec.hip_z - spec.bob * math.cos(TAU * 2 * (phi - mid))
    x = spec.sway * math.cos(TAU * (phi - mid))
    P["pelvis_loc"] = Vector((x, base["pelvis_loc"].y, z))
    yaw = -spec.pelvis_yaw * math.cos(TAU * phi)
    roll = -spec.pelvis_roll * math.cos(TAU * (phi - mid))
    P["pelvis_rot"] = qz(yaw) @ qy(roll) @ qx(spec.pelvis_pitch)
    # 가슴: 골반과 반대로 돌고, 착지 뒤 조금 더 숙임
    hit = math.cos(TAU * 2 * (phi - mid - 0.04))
    P["chest_rot"] = qz(-yaw * spec.chest_yaw / spec.pelvis_yaw) @ qy(-roll * 0.4) @ qx(spec.chest_pitch + spec.lean_hit * hit)
    # 머리: 진행 방향을 보며 안정. 몸의 흔들림을 조금 늦게 받는다
    P["head_rot"] = qz(0.25 * yaw) @ qx(spec.head_pitch + 1.2 * math.cos(TAU * 2 * (phi - mid - 0.08)))
    # 소총(가슴 공간): 착지 뒤 늦게 내려앉고 걸음마다 조금 기운다
    loc, q = base["rifle"]
    bounce = -spec.rifle_bounce * math.cos(TAU * 2 * (phi - mid - 0.10))
    P["rifle"] = (loc + Vector((0.004 * math.cos(TAU * phi), 0, bounce)),
                  q @ Quaternion((0, 1, 0), math.radians(spec.rifle_roll * math.cos(TAU * (phi - 0.1)))))
    # 무릎 방향: 발이 향하는 앞
    for side in ("L", "R"):
        a = P["foot_" + side][0]
        P["knee_" + side] = Vector((a.x * 1.3, -1.5, 0.55))
    return P, contact


def run_base(R, rifle_world, chest_space_fn):
    P = R.base_pose()
    P["pelvis_loc"] = Vector((0, 0.0, 0.79))
    P["pelvis_rot"] = qx(9)
    P["chest_rot"] = qx(17)
    P["head_rot"] = qx(6)
    P["rifle"] = rifle_world
    return P


# ------------------------------------------------------------------ 출발 · 멈춤 (서기 ↔ 달리기)
def ground_foot(R, side, ball_xy, theta, yaw=0.0):
    """발볼이 땅의 ball_xy에 닿은 채 뒤꿈치를 theta만큼 든 발 → (발목, 발 회전, 발가락 회전)."""
    q = foot_frame(R, side, theta, yaw)
    off = R.ankle0[side] - R.ball0[side]
    off.x = 0.0
    ball = Vector((ball_xy[0], ball_xy[1], R.ball0[side].z))
    return ball + q @ off, q, Quaternion((0, 0, 1), math.radians(yaw))


def flat_ball(R, side, ankle, yaw=0.0):
    """평평한 발의 발목 위치 → 발볼 x, y."""
    off = R.ankle0[side] - R.ball0[side]
    off.x = 0.0
    b = ankle - Quaternion((0, 0, 1), math.radians(yaw)) @ off
    return Vector((b.x, b.y))


def foot_state(R, P, side):
    return P["foot_" + side][0].copy(), P["foot_" + side][1].copy(), P["toe_" + side].copy()


def swing_between(a0, v0, a1, v1, u, dur, lift):
    p = hermite(a0, v0 * dur, a1, v1 * dur, u)
    return p + Vector((0, 0, lift * math.sin(math.pi * u) ** 1.5))


def velocity(fn, t, dt=1 / 120):
    a = fn(t + dt)
    b = fn(t - dt)
    return (a - b) / (2 * dt)


def start_pose(R, stand, rbase, spec, t, T=0.5):
    """서기 → 달리기 첫 프레임(왼발 착지). 오른발로 밀어 내고 왼발이 앞으로 나가 디딘다."""
    run0, _ = run_pose(R, rbase, spec, 0.0)
    w = ease(min(1.0, t / T), "in")
    P = {}
    from rok_rig import blend_pose
    P = blend_pose(stand, run0, w)
    # 예비 동작: 처음 0.15초 동안 앞으로 기울며 무게를 오른발로
    lean = math.sin(math.pi * min(1.0, t / 0.36)) * (1 - 0.5 * w)
    P["chest_rot"] = qx(11 * lean) @ P["chest_rot"]
    P["pelvis_rot"] = qx(4 * lean) @ P["pelvis_rot"]
    P["pelvis_loc"] = P["pelvis_loc"] + Vector((-0.02 * lean, -0.035 * lean, -0.03 * lean))
    acc = spec.speed / (T * 0.92)
    def push(tt):          # 오른발(디딘 발)이 뒤로 간 거리: 몸이 빨라지는 만큼
        tt = max(0.0, tt - 0.04)
        return 0.5 * acc * tt * tt
    # 오른발: 디딘 채 뒤로 → 0.30초에 뒤꿈치를 들며 떠남 → 달리기 첫 프레임의 오른발(공중)로
    aR0, qR0, tR0 = foot_state(R, stand, "R")
    yawR = math.degrees(qR0.to_euler("XYZ").z)
    bR = flat_ball(R, "R", aR0, yawR)
    t_off = 0.30
    if t <= t_off:
        th = 38.0 * ease(max(0.0, (t - 0.12) / (t_off - 0.12)), "in")
        a, q, toe = ground_foot(R, "R", (bR.x, bR.y + push(t)), th, yawR)
        P["foot_R"], P["toe_R"] = (a, q), toe
    else:
        aoff, qoff, _ = ground_foot(R, "R", (bR.x, bR.y + push(t_off)), 38.0, yawR)
        voff = Vector((0, acc * (t_off - 0.04), 0.6))
        fn = lambda tt: run_pose(R, rbase, spec, tt)[0]["foot_R"][0]
        a1 = fn(0.0)
        v1 = velocity(fn, 0.0)
        u = (t - t_off) / (T - t_off)
        a = swing_between(aoff, voff, a1, v1, u, T - t_off, 0.0)
        q = qoff.slerp(run0["foot_R"][1], ease(u, "inout"))
        P["foot_R"], P["toe_R"] = (a, q), Quaternion().slerp(run0["toe_R"], ease(u, "inout"))
    # 왼발: 0.08초에 떠서 앞으로 → 0.5초에 달리기의 왼발 착지 자리
    aL0, qL0, tL0 = foot_state(R, stand, "L")
    t_lift = 0.15
    if t <= t_lift:
        P["foot_L"], P["toe_L"] = (aL0, qL0), tL0
    else:
        fn = lambda tt: run_pose(R, rbase, spec, tt)[0]["foot_L"][0]
        a1 = fn(0.0)
        v1 = Vector((0, spec.speed * 0.3, -0.4))
        u = (t - t_lift) / (T - t_lift)
        a = swing_between(aL0, Vector((0, 0, 0.3)), a1, v1, u, T - t_lift, 0.10)
        q = qL0.slerp(run0["foot_L"][1], ease(u, "inout"))
        P["foot_L"], P["toe_L"] = (a, q), tL0.slerp(run0["toe_L"], ease(u, "inout"))
    for s in ("L", "R"):
        a = P["foot_" + s][0]
        P["knee_" + s] = Vector((a.x * 1.3, -1.5, 0.55))
    return P


def stop_pose(R, stand, rbase, spec, t, T=0.6):
    """달리기 첫 프레임(왼발 착지) → 서기. 왼발로 받고 오른발로 한 번 더 디뎌 멈춘 뒤 왼발을 모음."""
    run0, _ = run_pose(R, rbase, spec, 0.0)
    brake = 8.0
    t_stop = spec.speed / brake
    def travel(tt):
        tt = min(tt, t_stop)
        return spec.speed * tt - 0.5 * brake * tt * tt
    from rok_rig import blend_pose
    w = ease(min(1.0, t / T), "out")
    P = blend_pose(run0, stand, w)
    # 멈추며 몸이 앞으로 쏠렸다가(0.15초) 뒤로 받치고(0.3초) 섬
    k = math.sin(math.pi * min(1.0, t / 0.32)) * (1 - ease(min(1.0, t / T), "inout"))
    P["chest_rot"] = qx(5 * k) @ P["chest_rot"]
    # 왼발: 착지 자리에서 디딘 채 뒤로(느려지며) → 0.24초에 떠서 서기 자리로
    aL, qL, tL = foot_state(R, run0, "L")
    bL = flat_ball(R, "L", aL) if True else None
    aS, qS, tS = foot_state(R, stand, "L")
    t_off = 0.24
    if t <= t_off:
        th = 28.0 * ease(max(0.0, (t - 0.1) / (t_off - 0.1)), "in")
        a, q, toe = ground_foot(R, "L", (bL.x, bL.y + travel(t) + 0.0), th)
        P["foot_L"], P["toe_L"] = (a, q), Quaternion()
    else:
        aoff, qoff, _ = ground_foot(R, "L", (bL.x, bL.y + travel(t_off)), 28.0)
        u = min(1.0, (t - t_off) / (0.50 - t_off))
        a = swing_between(aoff, Vector((0, brake * 0.0, 0.3)), aS, Vector((0, 0, -0.1)), u, 0.26, 0.05)
        P["foot_L"], P["toe_L"] = (a, qoff.slerp(qS, ease(u, "inout"))), Quaternion().slerp(tS, ease(u, "inout"))
    # 오른발: 공중(뒤)에서 앞으로 와 0.17초에 디딤 → 남은 속도만큼 뒤로 가서 서기 자리에 멈춤
    fn = lambda tt: run_pose(R, rbase, spec, tt)[0]["foot_R"][0]
    a0 = fn(0.0)
    v0 = velocity(fn, 0.0)
    aRs, qRs, tRs = foot_state(R, stand, "R")
    t_land = 0.17
    land = Vector((aRs.x, aRs.y - (travel(t_stop) - travel(t_land)), aRs.z))
    if t <= t_land:
        u = t / t_land
        a = swing_between(a0, v0, land, Vector((0, spec.speed * 0.4, -0.3)), u, t_land, 0.0)
        P["foot_R"], P["toe_R"] = (a, run0["foot_R"][1].slerp(qRs, ease(u, "inout"))), run0["toe_R"].slerp(tRs, ease(u, "inout"))
    else:
        a = land + Vector((0, travel(t) - travel(t_land), 0))
        P["foot_R"], P["toe_R"] = (a, qRs), tRs
    for s in ("L", "R"):
        a = P["foot_" + s][0]
        kn = Vector((a.x * 1.3, -1.5, 0.55))
        P["knee_" + s] = kn.lerp(stand["knee_" + s], ease(min(1.0, t / T), "inout"))
    return P
