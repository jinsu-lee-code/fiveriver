# 무릎 자세 클립 (2026-11-18 저녁, 사용자 지시 "발목을 저렇게 꺽어서 사격을 계속할수있는지 상식적으로 생각해서 고쳐",
# 이어서 "발이 저렇게 꺽일수가 있나?").
# 게임의 무릎 대기 · 무릎 사격(컨트롤러 Idle_Trench · Fire_Crouch 상태)은 기존 클립을 썼는데, 꿇은 뒷발이 발끝 하나로 수직으로 서고
# 발볼이 8 cm 떠 있었다(무릎은 땅 밑 1 cm) — 그 자세로는 오래 버티거나 쏠 수 없다. 이 파일은 4번 무릎 자세
# (rok_poses "fire_crouch": 꿇은 무릎이 땅, 뒷발은 발등을 땅에 눕혀 발가락 쪽 발등이 땅을 짚음, 앞발은 발바닥 전체)로 두 클립을 만든다.
#   A_ROK_Kneel_Watch_Idle  3.0초 반복: 무릎 꿇고 총을 낮게 든 채 경계(Idle_Trench 자리). 사격 중에는 게임이 팔을 견착으로 맞춘다
#   A_ROK_FireCrouch_Fire   0.8초: 견착 사격 한 발, 반동(총구 5도 들림 · 개머리판이 어깨로 2.5 cm · 가슴 뒤로 2도) → 0.4초에 다시 겨눔
# 다리 · 골반 · 몸통은 두 클립이 같다(상태가 바뀌어도 어깨 · 발이 움직이지 않음).
# 높은 무릎쏴(허벅지 수직)는 그대로 둔다: 이 참호에서 발뒤꿈치에 앉으면 눈이 약 25 cm 내려가 흉벽 너머를 못 본다.
import math

from mathutils import Quaternion, Vector

from rok_rig import copy_pose, rot
from rok_timeline import Key, breathe, drift, qx
from rok_clips import Clip, rifle_turn, moved, tl_sampler
from rok_poses import chestify, rifle_from_right


def _kick(R, P, deg, back):
    """가슴 공간의 소총을 뒷손잡이(R)를 중심으로 총구가 deg도 들리게, 총구 반대쪽으로 back m 밀기."""
    loc, rq = P["rifle"]
    gR = R.rest["rifle"].inverted() @ R.rest["grip.R"].translation
    gL = R.rest["rifle"].inverted() @ R.rest["grip.L"].translation
    aim = (rq @ (gL - gR)).normalized()          # 뒷손잡이 → 앞손잡이 = 총구 쪽
    axis = aim.cross(Vector((0, 0, 1)))
    if axis.length < 1e-4:
        return P
    axis.normalize()
    q = Quaternion(axis, math.radians(deg))
    if (q @ aim).z < aim.z:                      # 총구가 내려가는 방향이면 반대로
        q = Quaternion(axis, -math.radians(deg))
    return rifle_turn(R, P, q, pivot="R", shift=tuple(-aim * back))


def watch_pose(R, fire):
    """무릎 사격과 같은 다리 · 몸통, 총은 배 높이에 비스듬히(총구 앞-왼쪽, 조금 위) — 기존 무릎 대기의 총 든 모양."""
    P = copy_pose(fire)
    # 아래 값은 루트 기준으로 적고 chestify가 가슴 공간으로 바꾼다 (rifle_space 1 = 가슴과 함께 움직임)
    P["rifle"] = rifle_from_right(R, (-0.140, -0.220, 0.600), (0.52, -0.80, 0.20), roll=-15)
    P["elbow_R"] = Vector((-0.70, 0.30, 0.45))
    P["elbow_L"] = Vector((0.65, -0.20, 0.20))
    P["head_rot"] = rot(pitch=4, yaw=10, roll=0)
    P["index_R"] = 0.35
    return chestify(R, P, keys=("rifle", "elbow_L", "elbow_R"))


def add(R, LIB, clips):
    fire = LIB["fire_crouch"]
    watch = watch_pose(R, fire)
    LIB["kneel_watch"] = watch
    new = []

    T = 3.0

    def idle(t):
        P = copy_pose(watch)
        breathe(P, t, period=T, amount=0.8)
        drift(P, t, period=T, sway=0.005, head_yaw=2.0)
        return P

    clips["A_ROK_Kneel_Watch_Idle"] = Clip("A_ROK_Kneel_Watch_Idle", T, idle, loop=True, start_ref="self",
                                           note="무릎 꿇고 총을 낮게 든 채 경계(꿇은 무릎 · 뒷발 발등이 땅)")
    new.append("A_ROK_Kneel_Watch_Idle")

    peak = moved(_kick(R, fire, 5.0, 0.025), chest_rot=qx(-2.0), head_rot=qx(-1.5), pelvis=(0, 0.005, 0))
    settle = moved(_kick(R, fire, 1.5, 0.006), chest_rot=qx(-0.6))
    fk = [
        Key(0.0, fire),
        Key(0.045, peak, "out"),
        Key(0.20, settle, "inout"),
        Key(0.42, fire, "inout"),
        Key(0.8, fire, "soft"),
    ]
    clips["A_ROK_FireCrouch_Fire"] = Clip("A_ROK_FireCrouch_Fire", 0.8, tl_sampler(fk, 0.8),
                                          note="한 발 쏨: 반동으로 총구가 들리고 상체가 조금 밀렸다가 다시 겨눔")
    new.append("A_ROK_FireCrouch_Fire")

    # 발 확대 확인용(내보내지 않음): -- A_ROK_FireCrouch_FeetCheck,A_ROK_Ready_FeetCheck render
    for name, pose in (("A_ROK_FireCrouch_FeetCheck", fire), ("A_ROK_Ready_FeetCheck", LIB["ready"])):
        chk = Clip(name, 0.1, (lambda Q: (lambda t: copy_pose(Q)))(pose), note="꿇은 발 확대 렌더")
        chk.new, chk.step, chk.views, chk.res, chk.cam = True, 1.0, ["side", "qf", "front"], (560, 460), ((-0.10, 0.12, 0.25), 1.0)
        clips[name] = chk
    return new
