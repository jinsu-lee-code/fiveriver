# 적(인민군) 달리기 클립 정의. 아군 달리기 사이클(rok_run)을 그대로 쓰고 자세 · 소총 드는 법만 적에 맞춘다.
# 야간 돌격: 몸을 낮추고 앞으로 숙여, 총검 꽂은 모신나강을 앞으로(총구 앞 · 조금 위 · 조금 왼쪽) 들고 달린다.
# 좌표: 캐릭터는 -Y를 본다(왼쪽 +X, 위 +Z).
import math

from mathutils import Quaternion, Vector

import rok_common as C
import rok_run
from rok_rig import rot, gun_rot, copy_pose
from rok_timeline import qx, qy, qz


class Clip:
    def __init__(self, name, length, sample, loop=False, view="run", start_ref=None, end_ref=None, note="",
                 frames=None, reach_fix=False):
        self.name, self.length, self.sample, self.loop, self.view = name, length, sample, loop, view
        self.start_ref, self.end_ref, self.note = start_ref, end_ref, note
        self.frames = frames if frames is not None else int(round(length * C.FPS)) + 1
        self.reach_fix = reach_fix


def chestify(R, P, keys=("rifle", "elbow_L", "elbow_R")):
    R.apply(P)
    inv = R.delta4("DEF-spine.003").inverted()
    for k in keys:
        v = P[k]
        if isinstance(v, tuple):
            mm = v[1].to_matrix().to_4x4()
            mm.translation = v[0]
            mm = inv @ mm
            P[k] = (mm.translation.copy(), mm.to_quaternion())
        else:
            P[k] = inv @ v
    return P


def rifle_from_right(R, wrist, muzzle, up=(0, 0, 1), roll=0.0):
    q = gun_rot(muzzle, up, roll)
    g = R.rest["rifle"].inverted() @ R.rest["grip.R"].translation
    return (Vector(wrist) - q @ g, q)


# 소총 드는 법 (가슴 공간): 오른손은 배 오른쪽 앞(팔이 0.34 m로 짧아 허리까지 내려가지 못함), 총구는 앞 · 왼쪽 · 위.
CARRY = {
    "run":    dict(wrist=(-0.03, 0.30, -0.30), muzzle=(0.42, 1.0, 0.42), roll=-8, slide=-0.07),
    "crouch": dict(wrist=(-0.02, 0.30, -0.27), muzzle=(0.38, 1.0, 0.32), roll=-8, slide=-0.07),
}


def run_base(R, kind, spec, chest_yaw=0.0):
    P = R.base_pose()
    P["pelvis_loc"] = Vector((0, 0.0, spec.hip_z))
    P["pelvis_rot"] = qx(spec.pelvis_pitch)
    P["chest_rot"] = rot(pitch=spec.chest_pitch, yaw=chest_yaw)
    P["head_rot"] = qx(spec.head_pitch)
    for s in ("L", "R"):
        x = spec.foot_x if s == "L" else -spec.foot_x
        P["foot_" + s] = (Vector((x, 0.0, R.ankle0[s].z)), Quaternion())
    R.apply(P)
    shR = R.pb["DEF-upper_arm.R"].head.copy()
    dc = R.delta("DEF-spine.003")
    fwd = (dc @ Vector((0, -1, 0))).normalized()
    up = (dc @ Vector((0, 0, 1))).normalized()
    left = (dc @ Vector((1, 0, 0))).normalized()
    c = CARRY[kind]
    shL = R.pb["DEF-upper_arm.L"].head.copy()
    head = R.pb["DEF-spine.006"].head.copy()
    gR = R.rest["rifle"].inverted() @ R.rest["grip.R"].translation
    gL = R.rest["rifle"].inverted() @ R.rest["grip.L"].translation
    hz = Vector((0, -1, 0))
    reach = 0.92 * (R.upper + R.fore)
    best = None
    # 두 손이 닿는(어깨-손목 ≤ 팔 길이 95%) 자세 중 총구가 가장 앞을 향하는 것
    for wf in (0.06, 0.09, 0.12, 0.15, 0.18):
        for wu in (-0.30, -0.26, -0.22, -0.18, -0.14):
            for wx in (-0.06, -0.02, 0.02, 0.06, 0.10):
                wrist = shR + left * wx + fwd * wf + up * wu
                for ml in (0.15, 0.3, 0.45, 0.6, 0.8, 1.0, 1.3):
                    for mu in (0.15, 0.3, 0.45, 0.6):
                        muzzle = left * ml + hz + Vector((0, 0, 1)) * mu
                        loc, q = rifle_from_right(R, wrist, muzzle, up=Vector((0, 0, 1)), roll=c["roll"])
                        for slide in (-0.14, -0.10, -0.06, -0.02):
                            pL = loc + q @ (gL + C.gun(0, 0, slide))
                            dR = (wrist - shR).length
                            dL = (pL - shL).length
                            over = max(0.0, dR - reach) + max(0.0, dL - reach)
                            ang = math.degrees(math.atan2(ml, 1.0))
                            # 총구가 머리에 너무 가까우면 안 됨
                            mz = loc + q @ C.gun(0, 0, C.RIFLE_MUZZLE_Z * 0.5)
                            near = max(0.0, 0.22 - (mz - head).length)
                            score = 20 * over + ang / 90.0 + 0.3 * abs(mu - 0.3) + 5 * near + 0.5 * abs(slide + 0.06)
                            if best is None or score < best[0]:
                                best = (score, wrist, loc, q, slide, (wf, wu, wx, ml, mu), dR, dL)
    print("KPA CARRY", kind, "params wf,wu,wx,ml,mu", best[5], "slide", best[4], "reach R/L %.3f %.3f (max %.3f)" % (best[6], best[7], reach))
    P["rifle"] = (best[2], best[3])
    P["grip_slide_L"] = best[4]
    P["elbow_R"] = shR + Vector((-0.45, 0.35, -0.35))
    shL = R.pb["DEF-upper_arm.L"].head.copy()
    P["elbow_L"] = shL + Vector((0.35, -0.05, -0.45))
    P["fingers_L"], P["fingers_R"], P["thumb_L"], P["thumb_R"], P["index_R"] = 0.85, 1.1, 0.3, 0.45, 0.5
    P = chestify(R, P)
    return P


def make_clips(R):
    clips = {}
    # 선 자세 달리기(후퇴 · 일반 돌격): 아군보다 빠르고 앞으로 더 숙임
    spec = rok_run.RunSpec(frames=18, speed=3.4, contact=0.30, hip_z=0.755, bob=0.026, sway=0.016,
                           pelvis_pitch=14, chest_pitch=26, head_pitch=8, pelvis_yaw=7, chest_yaw=5,
                           swing_high=0.31, kick=0.40, heel_lift=42, ball_land=-0.27, foot_x=0.09,
                           rifle_bounce=0.014, rifle_roll=3.5, lean_hit=2.5)
    base = run_base(R, "run", spec)

    def run(t):
        P, _ = rok_run.run_pose(R, base, spec, t)
        return P

    c = Clip("A_KPA_Run_Rifle", spec.period, run, loop=True, frames=spec.frames + 1, reach_fix=True,
             note="총검 꽂은 소총을 앞으로 들고 숙여 달리기 %.1f m/s" % spec.speed)
    c.spec = spec
    clips[c.name] = c

    # 낮춰 달리기(야간 돌격): 엉덩이를 낮추고 가슴을 깊이 숙이고 고개는 들어 앞을 봄
    cspec = rok_run.RunSpec(frames=20, speed=2.8, contact=0.36, hip_z=0.630, bob=0.020, sway=0.020,
                            pelvis_pitch=26, chest_pitch=38, head_pitch=11, pelvis_yaw=6, chest_yaw=4,
                            swing_high=0.26, kick=0.30, heel_lift=36, ball_land=-0.27, foot_x=0.095,
                            rifle_bounce=0.012, rifle_roll=3.0, lean_hit=2.0)
    cbase = run_base(R, "crouch", cspec)

    def crouch(t):
        P, _ = rok_run.run_pose(R, cbase, cspec, t)
        return P

    c = Clip("A_KPA_CrouchRun", cspec.period, crouch, loop=True, frames=cspec.frames + 1, reach_fix=True,
             note="몸을 낮춰 총검 소총을 앞으로 들고 돌격 %.1f m/s" % cspec.speed)
    c.spec = cspec
    clips[c.name] = c
    for c in clips.values():
        c.new = True
        c.mesh_check = True
        c.cam_run = ((0, -0.10, 0.80), 2.25)
    return clips, {}
