# 클립 정의. 각 클립 = 길이 + sample(t) → 자세 값.
# 좌표: 캐릭터는 -Y를 본다(왼쪽 +X, 위 +Z). rot(pitch 앞숙임+, yaw 왼쪽+, roll 왼쪽으로 기울임+).
import math

from mathutils import Quaternion, Vector, Matrix

import rok_common as C
import rok_poses
import rok_run
from rok_rig import rot, gun_rot, copy_pose, blend_pose, ease, frame
from rok_timeline import Key, Timeline, breathe, drift, envelope, qx, qy, qz


class Clip:
    def __init__(self, name, length, sample, loop=False, view="trench", start_ref=None, end_ref=None, note="",
                 frames=None, reach_fix=False):
        self.name, self.length, self.sample, self.loop, self.view = name, length, sample, loop, view
        self.start_ref, self.end_ref, self.note = start_ref, end_ref, note
        self.frames = frames if frames is not None else int(round(length * C.FPS)) + 1
        self.reach_fix = reach_fix


# ------------------------------------------------------------------ 도우미
def with_(P, **kw):
    Q = copy_pose(P)
    Q.update(kw)
    return Q


def moved(P, **kw):
    """P의 값에 더하기/돌리기: pelvis=(dx,dy,dz), pelvis_rot=q, chest_rot=q, head_rot=q (q는 앞에 곱함)."""
    Q = copy_pose(P)
    for k, v in kw.items():
        if k in ("pelvis", "pelvis_loc"):
            Q["pelvis_loc"] = Q["pelvis_loc"] + Vector(v)
        elif isinstance(v, Quaternion):
            Q[k] = v @ Q[k]
        else:
            Q[k] = v
    return Q


def rifle_turn(R, P, q, pivot="R", shift=(0, 0, 0)):
    """가슴 공간의 소총을 한 손잡이를 중심으로 q만큼 돌리고 shift만큼 옮긴다."""
    Q = copy_pose(P)
    loc, rq = Q["rifle"]
    g = R.rest["rifle"].inverted() @ R.rest["grip." + pivot].translation
    pv = loc + rq @ g
    nq = q @ rq
    Q["rifle"] = (pv - nq @ g + Vector(shift), nq)
    return Q


def space_matrix(R, space):
    if space == "chest":
        return R.delta4("DEF-spine.003")
    if space == "head":
        return R.delta4("DEF-spine.006")
    if space == "pelvis":
        return R.delta4("DEF-spine")
    return Matrix.Identity(4)


def hand_at_grip(R, P, side, space):
    """손 목표를 지금 손잡이 자리로(제약을 풀 때 튀지 않게)."""
    Q = copy_pose(P)
    R.apply(Q)
    m = space_matrix(R, space).inverted() @ R.pb["grip." + side].matrix
    Q["hand_" + side] = (m.translation.copy(), m.to_quaternion())
    Q["hand_space_" + side] = space
    return Q


def hand_world(R, P, side, loc, fingers, back, space):
    """손목 위치(월드) · 손가락 방향 · 손등 방향 → 그 공간의 손 목표."""
    Q = copy_pose(P)
    R.apply(Q)
    q = R.hand_abs(side, Vector(fingers), Vector(back))
    m = q.to_matrix().to_4x4()
    m.translation = Vector(loc)
    m = space_matrix(R, space).inverted() @ m
    Q["hand_" + side] = (m.translation.copy(), m.to_quaternion())
    Q["hand_space_" + side] = space
    return Q


def elbow_world(R, P, side, loc):
    Q = copy_pose(P)
    R.apply(Q)
    Q["elbow_" + side] = R.delta4("DEF-spine.003").inverted() @ Vector(loc)
    return Q


def body_point(R, P, bone, rest_point):
    """기준 자세에서의 몸 위 한 점이 자세 P에서 어디에 있는가."""
    R.apply(P)
    return R.delta4(bone) @ Vector(rest_point)


def mesh_point(R, P, mat_key, side=-1):
    """자세 P에서 재질 이름에 mat_key가 든 부분 중 한쪽(side -1 = 오른쪽) 앞쪽 끝의 중심 (탄입대 등)."""
    import bpy
    R.apply(P)
    body = bpy.data.objects["Body_Low"]
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    idx = {i for i, m in enumerate(body.data.materials) if m and mat_key.lower() in m.name.lower()}
    pts = []
    for poly in me.polygons:
        if poly.material_index in idx:
            c = ev.matrix_world @ poly.center
            if c.x * side > 0.04:
                pts.append(c)
    ev.to_mesh_clear()
    pts.sort(key=lambda c: c.y)
    front = pts[:max(1, len(pts) // 6)]
    out = Vector((0, 0, 0))
    for c in front:
        out += c
    return out / len(front)


def tl_sampler(keys, length, lags=None, live=None):
    tl = Timeline(keys, length, lags)

    def sample(t):
        P = tl.sample(t)
        if live:
            live(P, t)
        return P
    return sample


def live_ready(length, amount=1.0):
    """준비 자세 위의 숨 · 미세한 흔들림 (양 끝에서 0 → 시작 · 끝이 A 클립 첫 프레임과 같음)."""
    def f(P, t):
        e = envelope(t, length, 0.4) * amount
        breathe(P, t, period=2.5, amount=0.8, envelope=e)
    return f


# ------------------------------------------------------------------ 클립
FLIP_LIFT = 0.10   # 발끝 세운 발 ↔ 눕힌 발: 뒤집는 동안 발목을 이만큼 들어 발끝이 땅을 파고들지 않게(키 사이 가운데에서 최고)


def make_clips(R):
    L = rok_poses.make_poses(R)
    ready, stand, fire = L["ready"], L["stand"], L["fire_crouch"]
    tuck = rok_poses.TUCK      # 무릎을 꿇는 동안 거치는 발끝 세운 오른발
    clips = {}

    # ---------------------------------------------------------------- A. 참호 낮춘 준비 자세 (반복 5초)
    TA = 5.0

    def ready_idle(t):
        P = copy_pose(ready)
        breathe(P, t, period=TA / 2, amount=1.0)            # 2.5초에 한 번 숨
        drift(P, t, period=TA, sway=0.016, head_yaw=4.5)    # 한 번 무게를 옮겼다 돌아옴, 고개를 조금 돌림
        g = math.sin(math.pi * min(1.0, max(0.0, (t - 3.1) / 0.6))) ** 2
        P["fingers_L"] = ready["fingers_L"] - 0.25 * g      # 왼손을 한 번 고쳐 쥠
        return P

    clips["A_ROK_Trench_Ready_Idle"] = Clip("A_ROK_Trench_Ready_Idle", TA, ready_idle, loop=True, start_ref="self",
                                            note="참호 앞벽 아래에서 오른 무릎을 꿇고 소총을 가슴 앞에 비스듬히 든 대기")

    # ---------------------------------------------------------------- B. 서기 ↔ A
    pel_ready = ready["pelvis_loc"]
    sk = [
        Key(0.0, stand),
        Key(0.24, moved(stand, pelvis=(0.03, 0.01, -0.025), pelvis_rot=qy(2.5), chest_rot=qx(4) @ qy(-1.5)), "inout"),
        # 오른발은 발끝으로 뒤를 디딘 채(뒤꿈치 듦) 무릎을 내리고, 무릎이 땅에 닿은 뒤 발을 눕힌다(발등이 땅) — 11-18 저녁
        Key(0.56, with_(blend_pose(stand, ready, 0.35), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"], foot_L=stand["foot_L"],
                        toe_L=stand["toe_L"], knee_L=stand["knee_L"], pelvis_loc=Vector((0.02, 0.07, 0.64))), "inout", lift_R=0.06),
        Key(0.86, with_(blend_pose(stand, ready, 0.75), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"], foot_L=ready["foot_L"],
                        toe_L=ready["toe_L"], knee_L=ready["knee_L"], pelvis_loc=Vector((0.0, 0.12, 0.47))), "inout", lift_L=0.035),
        Key(1.06, with_(moved(ready, pelvis=(0, 0.01, -0.015), chest_rot=qx(4)), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"]), "out"),
        Key(1.32, moved(ready, pelvis=(0, 0.005, -0.008), chest_rot=qx(2)), "inout", lift_R=FLIP_LIFT),
        Key(1.5, ready, "soft"),
    ]
    clips["A_ROK_Stand_To_TrenchReady"] = Clip("A_ROK_Stand_To_TrenchReady", 1.5, tl_sampler(sk, 1.5),
                                               start_ref="A_ROK_Idle_Stand", end_ref="A_ROK_Trench_Ready_Idle",
                                               note="오른발을 뒤로 빼 디딘 뒤 왼발을 앞으로, 오른 무릎을 내림")

    uk = [
        Key(0.0, ready),
        # 예비: 앞으로 숙여 무게를 왼발로, 그사이 눕힌 오른발을 세워 발끝으로 디딤(일어설 때 밀 발) — 11-18 저녁
        Key(0.28, with_(moved(ready, pelvis=(0.03, -0.05, 0.03), chest_rot=qx(9), head_rot=qx(-4)), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"]),
            "inout", lift_R=FLIP_LIFT),
        Key(0.58, with_(blend_pose(ready, stand, 0.35), foot_R=tuck["foot_R"], toe_R=tuck["toe_R"], foot_L=ready["foot_L"],
                        toe_L=ready["toe_L"], knee_L=ready["knee_L"], pelvis_loc=Vector((0.03, 0.02, 0.58))), "inout"),
        Key(0.88, with_(blend_pose(ready, stand, 0.7), foot_R=stand["foot_R"], toe_R=stand["toe_R"], foot_L=ready["foot_L"],
                        toe_L=ready["toe_L"], knee_L=ready["knee_L"], pelvis_loc=Vector((-0.01, 0.0, 0.74))), "inout", lift_R=0.06),
        Key(1.15, with_(moved(stand, pelvis=(0, 0, -0.012)), foot_L=stand["foot_L"]), "inout", lift_L=0.035),
        Key(1.5, stand, "soft"),
    ]
    clips["A_ROK_TrenchReady_To_Stand"] = Clip("A_ROK_TrenchReady_To_Stand", 1.5, tl_sampler(uk, 1.5),
                                               start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Idle_Stand",
                                               note="앞으로 숙여 무게를 왼발에 싣고 일어나며 오른발 · 왼발 순서로 디딤")

    # ---------------------------------------------------------------- C. A → 무릎 사격
    ck = [
        Key(0.0, ready),
        Key(0.30, blend_pose(ready, fire, 0.62), "out", groups={"arms": "out3", "head": "out"}),
        Key(0.66, fire, "soft"),
    ]
    clips["A_ROK_TrenchReady_To_FireCrouch"] = Clip("A_ROK_TrenchReady_To_FireCrouch", 0.66,
                                                    tl_sampler(ck, 0.66, lags={"chest": 0.03, "head": 0.05, "arms": 0.02}),
                                                    start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Idle_Trench",
                                                    note="몸을 세우며 소총을 어깨에 견착")

    # ---------------------------------------------------------------- D. 흉벽 너머 살피기 (3.5초)
    up = with_(ready, pelvis_loc=Vector((pel_ready.x + 0.02, pel_ready.y - 0.13, 0.585)), pelvis_rot=rot(pitch=18),
               chest_rot=rot(pitch=17), head_rot=rot(pitch=-6))
    up = rifle_turn(R, up, qx(16), pivot="R", shift=(0.0, 0.03, -0.02))
    pk = [
        Key(0.0, ready),
        Key(0.45, moved(ready, pelvis=(0.02, -0.02, 0.015), chest_rot=qx(4)), "inout"),
        Key(1.25, up, "inout"),
        Key(1.75, moved(up, head_rot=qz(20) @ qx(2), chest_rot=qz(5)), "inout"),
        Key(2.35, moved(up, head_rot=qz(-16) @ qx(1), chest_rot=qz(-4)), "inout"),
        Key(2.6, moved(up, head_rot=qz(-10)), "inout"),
        Key(3.15, moved(ready, pelvis=(0, 0.0, -0.012), chest_rot=qx(2)), "inout"),
        Key(3.5, ready, "soft"),
    ]
    clips["A_ROK_Trench_PeekOver"] = Clip("A_ROK_Trench_PeekOver", 3.5, tl_sampler(pk, 3.5, live=live_ready(3.5, 0.6)),
                                          start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Trench_Ready_Idle",
                                          note="무릎을 떼고 몸을 올려 흉장 너머를 좌우로 살핀 뒤 내려앉음")

    # ---------------------------------------------------------------- D. 소총 점검 (3.6초)
    R.apply(ready)
    wr = R.pb["grip.R"].matrix.translation.copy()
    tilt = with_(ready, rifle=rok_poses.rifle_from_right(R, wr + Vector((0.03, -0.06, 0.10)), (0.80, -0.38, 0.46), roll=-28),
                 rifle_space=1.0)
    tilt = rok_poses.chestify(R, tilt, keys=("rifle",))
    tilt = moved(tilt, head_rot=qx(28) @ qz(-8), chest_rot=qx(3))
    tilt = hand_at_grip(R, tilt, "R", "world")
    base_R = hand_at_grip(R, ready, "R", "world")
    R.apply(tilt)
    pouch = mesh_point(R, tilt, "Pauch", side=-1)       # 오른쪽 앞 탄입대 (변형된 메시에서 찾음)
    pouch_hand = hand_world(R, tilt, "R", pouch + Vector((-0.015, -0.035, 0.075)), (0.1, 0.25, -1.0), (-0.5, -0.85, 0.0), "world")
    pouch_hand = with_(pouch_hand, grip_R=0.0, fingers_R=0.45, thumb_R=0.3, index_R=0.45)
    tap = copy_pose(pouch_hand)
    tap["hand_R"] = (tap["hand_R"][0] + Vector((0.0, 0.012, -0.018)), tap["hand_R"][1])
    look2 = moved(tilt, head_rot=qx(5) @ qz(3))
    ckr = [
        Key(0.0, base_R),
        Key(0.7, tilt, "inout"),
        Key(1.3, look2, "inout"),
        Key(1.55, with_(look2, grip_R=1.0), "inout"),
        Key(1.9, with_(moved(pouch_hand, head_rot=qx(4) @ qz(-14)), hand_R=pouch_hand["hand_R"]), "inout"),
        Key(2.1, with_(moved(tap, head_rot=qx(4) @ qz(-14))), "inout"),
        Key(2.3, with_(moved(pouch_hand, head_rot=qx(4) @ qz(-14))), "inout"),
        Key(2.5, with_(moved(tap, head_rot=qx(4) @ qz(-14))), "inout"),
        Key(2.85, with_(look2, grip_R=1.0), "inout"),
        Key(3.6, base_R, "soft"),
    ]
    clips["A_ROK_Trench_CheckRifle"] = Clip("A_ROK_Trench_CheckRifle", 3.6, tl_sampler(ckr, 3.6, live=live_ready(3.6, 0.6)),
                                            start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Trench_Ready_Idle",
                                            note="소총을 기울여 기관부를 내려다본 뒤 오른손으로 탄입대(클립 주머니)를 두 번 만져 확인")

    # ---------------------------------------------------------------- D. 이마 훔치기 (2.8초)
    held = rifle_turn(R, ready, qx(-10) @ qz(4), pivot="R", shift=(0, 0.01, -0.02))   # 왼손을 떼면 총구가 조금 내려앉음
    w0 = hand_at_grip(R, ready, "L", "head")
    R.apply(held)
    hp = R.delta4("DEF-spine.006")
    face_fwd = (hp.to_3x3() @ Vector((0, -1, 0))).normalized()
    face_up = (hp.to_3x3() @ Vector((0, 0, 1))).normalized()
    face_left = face_up.cross(face_fwd).normalized() * -1
    if face_left.x < 0:
        face_left = -face_left
    brow = hp @ (R.exp_rest["Head"].translation + Vector((0, -0.095, 0.085)))
    a1 = hand_world(R, moved(held, head_rot=qx(8) @ qz(-6)), "L", brow - face_left * 0.12 + face_fwd * 0.05 + face_up * 0.01,
                    -face_left * 0.6 + face_up * 0.8, face_fwd, "head")
    a2 = hand_world(R, moved(held, head_rot=qx(12) @ qz(8)), "L", brow + face_left * 0.04 + face_fwd * 0.07 + face_up * 0.02,
                    -face_left * 0.4 + face_up * 0.9, face_fwd * 0.8 - face_left * 0.2, "head")
    mid = hand_world(R, moved(held, head_rot=qx(4)), "L", brow - face_left * 0.02 + face_fwd * 0.20 - face_up * 0.15,
                     -face_left * 0.3 + face_up * 1.0, face_fwd, "head")
    for P in (a1, a2, mid):
        P["grip_L"] = 0.0
        P["fingers_L"] = 0.35
        P["thumb_L"] = 0.2
    a1 = elbow_world(R, a1, "L", (0.45, -0.25, 1.25))
    a2 = elbow_world(R, a2, "L", (0.55, -0.20, 1.10))
    mid = elbow_world(R, mid, "L", (0.40, -0.25, 0.90))
    w1 = with_(w0, rifle=held["rifle"], grip_L=1.0)
    wk = [
        Key(0.0, w0),
        Key(0.30, with_(w1, grip_L=1.0), "inout"),
        Key(0.62, with_(mid, hand_L=mid["hand_L"]), "inout"),
        Key(0.95, a1, "inout"),
        Key(1.45, a2, "inout"),
        Key(1.85, mid, "inout"),
        Key(2.15, with_(w1, grip_L=1.0, head_rot=moved(ready, head_rot=qx(3))["head_rot"]), "inout"),
        Key(2.8, w0, "soft"),
    ]
    clips["A_ROK_Trench_WipeBrow"] = Clip("A_ROK_Trench_WipeBrow", 2.8, tl_sampler(wk, 2.8, live=live_ready(2.8, 0.7)),
                                          start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Trench_Ready_Idle",
                                          note="왼손을 총에서 떼어 소매(팔뚝)로 이마를 오른쪽에서 왼쪽으로 훔치고 다시 잡음")

    # ---------------------------------------------------------------- D. 옆 사람 쪽 보기 (2.2초)
    side = moved(ready, head_rot=qz(-58) @ qx(-3), chest_rot=qz(-16), pelvis_rot=qz(-4), pelvis=(-0.008, 0, 0.004))
    lk = [
        Key(0.0, ready),
        Key(0.55, side, "inout", groups={"head": "out"}),
        Key(0.95, moved(side, head_rot=qx(4)), "inout"),
        Key(1.3, moved(side, head_rot=qx(-1)), "inout"),
        Key(2.2, ready, "soft"),
    ]
    clips["A_ROK_Trench_LookSide"] = Clip("A_ROK_Trench_LookSide", 2.2,
                                          tl_sampler(lk, 2.2, lags={"head": -0.0, "chest": 0.08, "base": 0.12, "arms": 0.1},
                                                     live=live_ready(2.2, 0.6)),
                                          start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Trench_Ready_Idle",
                                          note="오른쪽 옆 사람에게 고개와 어깨를 돌렸다가(고개 끄덕) 돌아옴")

    # ---------------------------------------------------------------- D. 고쳐 앉기 (3초)
    rise = moved(ready, pelvis=(0.035, -0.05, 0.10), pelvis_rot=qy(4), chest_rot=qx(-6) @ qy(-2), head_rot=qx(-3))
    rise_foot = copy_pose(rise)
    a, q = ready["foot_R"]
    rise_foot["foot_R"] = (a + Vector((-0.05, 0.035, 0.0)), qz(-12) @ q)
    rise_foot["toe_R"] = qz(-12) @ ready["toe_R"]
    sit = moved(ready, pelvis=(-0.004, 0.006, -0.018), chest_rot=qx(5) @ qy(1.5), head_rot=qx(4))
    sit["foot_R"], sit["toe_R"] = rise_foot["foot_R"], rise_foot["toe_R"]
    sk2 = [
        Key(0.0, ready),
        Key(0.6, rise, "inout"),
        Key(1.05, rise_foot, "inout", lift_R=0.025),
        Key(1.6, sit, "inout"),
        Key(2.2, moved(sit, pelvis=(0, 0, 0.006)), "inout"),
        Key(2.65, with_(moved(ready, pelvis=(0, 0, 0.01)), foot_R=ready["foot_R"], toe_R=ready["toe_R"]), "inout", lift_R=0.02),
        Key(3.0, ready, "soft"),
    ]

    def shift_live(P, t):
        # 앉으며 크게 한숨 (1.5~2.6초)
        e = math.sin(math.pi * min(1.0, max(0.0, (t - 1.45) / 1.15))) ** 2
        P["chest_rot"] = qx(-3.5 * e) @ P["chest_rot"]
        P["head_rot"] = qx(2.0 * e) @ P["head_rot"]

    clips["A_ROK_Trench_ShiftKnee"] = Clip("A_ROK_Trench_ShiftKnee", 3.0, tl_sampler(sk2, 3.0, live=shift_live),
                                           start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Trench_Ready_Idle",
                                           note="무게를 왼발로 옮겨 오른 무릎을 들고 발을 고쳐 디딘 뒤 한숨 쉬며 다시 앉음")

    # ---------------------------------------------------------------- E. 포탄에 급히 숙이기 (1.6초)
    duck = moved(ready, pelvis=(0.0, 0.04, -0.05), pelvis_rot=qx(8), chest_rot=qx(30), head_rot=qx(42))
    duck = rifle_turn(R, duck, qz(-12) @ qx(-27), pivot="R", shift=(-0.035, 0.05, 0.0))
    d0 = hand_at_grip(R, ready, "L", "head")
    R.apply(duck)
    hp = R.delta4("DEF-spine.006")
    top = hp @ (R.exp_rest["Head"].translation + Vector((0.0, 0.02, 0.17)))
    hup = (hp.to_3x3() @ Vector((0, 0, 1))).normalized()
    hfwd = (hp.to_3x3() @ Vector((0, -1, 0))).normalized()
    cover = hand_world(R, duck, "L", top + hfwd * 0.03 + Vector((0.04, 0, 0)) + hup * 0.02, -hfwd * 0.5 + Vector((-0.6, 0, 0)), hup, "head")
    cover = elbow_world(R, cover, "L", (0.38, -0.35, 0.85))
    cover.update(grip_L=0.0, fingers_L=0.3, thumb_L=0.2)
    dk = [
        Key(0.0, d0),
        Key(0.20, cover, "out3", groups={"base": "out", "chest": "out3", "head": "out3", "arms": "out3"}),
        Key(0.65, moved(cover, chest_rot=qx(2), pelvis=(0, 0, -0.005)), "inout"),
        Key(1.0, moved(cover, chest_rot=qx(-6), head_rot=qx(-12), pelvis=(0, -0.01, 0.02)), "inout"),
        Key(1.25, with_(moved(d0, chest_rot=qx(4), head_rot=qx(5)), grip_L=1.0), "inout"),
        Key(1.6, d0, "soft"),
    ]

    def duck_live(P, t):
        # 숙인 채 떨림(짧고 작게)
        e = math.sin(math.pi * min(1.0, max(0.0, (t - 0.2) / 0.8))) ** 2
        P["chest_rot"] = qx(0.6 * e * math.sin(t * 2 * math.pi * 7.0)) @ P["chest_rot"]

    clips["A_ROK_Trench_Duck"] = Clip("A_ROK_Trench_Duck", 1.6,
                                      tl_sampler(dk, 1.6, lags={"chest": 0.02, "head": 0.03, "arms": 0.0}, live=duck_live),
                                      start_ref="A_ROK_Trench_Ready_Idle", end_ref="A_ROK_Trench_Ready_Idle",
                                      note="0.2초 만에 몸을 웅크리며 왼팔로 머리를 감싸고, 천천히 고개를 들어 다시 잡음")

    # ---------------------------------------------------------------- 달리기 (반복)
    spec = rok_run.RunSpec()
    rbase = run_base(R, L)

    def run(t):
        P, _ = rok_run.run_pose(R, rbase, spec, t)
        return P

    clips["A_ROK_Run_Rifle"] = Clip("A_ROK_Run_Rifle", spec.period, run, loop=True, view="run",
                                    frames=spec.frames + 1, reach_fix=True, note="앞에총 자세로 달리기 %.1f m/s" % spec.speed)
    clips["A_ROK_Run_Rifle"].spec = spec

    clips["A_ROK_Run_Start"] = Clip("A_ROK_Run_Start", 0.5, lambda t: rok_run.start_pose(R, stand, rbase, spec, t, 0.5),
                                    view="run", start_ref="A_ROK_Idle_Stand", end_ref="A_ROK_Run_Rifle",
                                    note="앞으로 기울며 오른발로 밀어 내고 왼발이 나가 달리기 첫 걸음으로", reach_fix=True)
    clips["A_ROK_Run_Start"].spec = spec
    clips["A_ROK_Run_Stop"] = Clip("A_ROK_Run_Stop", 0.6, lambda t: rok_run.stop_pose(R, stand, rbase, spec, t, 0.6),
                                   view="run", start_ref="A_ROK_Run_Rifle", end_ref="A_ROK_Idle_Stand",
                                   note="왼발로 받고 오른발로 한 번 더 디뎌 멈춘 뒤 왼발을 모아 섬", reach_fix=True)
    clips["A_ROK_Run_Stop"].spec = spec

    cspec = rok_run.RunSpec(frames=22, speed=2.2, contact=0.38, hip_z=0.665, bob=0.018, pelvis_pitch=22, chest_pitch=30,
                            head_pitch=-6, swing_high=0.27, kick=0.30, heel_lift=36, ball_land=-0.26, sway=0.02,
                            rifle_bounce=0.010)
    cbase = run_base(R, L, crouch=True)

    def crouch_run(t):
        P, _ = rok_run.run_pose(R, cbase, cspec, t)
        return P

    clips["A_ROK_CrouchRun_Rifle"] = Clip("A_ROK_CrouchRun_Rifle", cspec.period, crouch_run, loop=True, view="run",
                                          frames=cspec.frames + 1, reach_fix=True, note="몸을 낮춰 달리기 %.1f m/s" % cspec.speed)
    clips["A_ROK_CrouchRun_Rifle"].spec = cspec
    return clips, L


def run_base(R, L, crouch=False):
    """앞에총: 오른손은 오른 허리 앞 개머리판 손목, 왼손은 가슴 왼쪽 총열덮개, 총구는 왼 어깨 위로."""
    P = R.base_pose()
    P["pelvis_loc"] = Vector((0, 0.0, 0.79 if not crouch else 0.665))
    P["pelvis_rot"] = qx(9 if not crouch else 22)
    P["chest_rot"] = qx(17 if not crouch else 30)
    P["head_rot"] = qx(6 if not crouch else -6)
    for s in ("L", "R"):
        x = 0.095 if s == "L" else -0.095
        P["foot_" + s] = (Vector((x, 0.0, R.ankle0[s].z)), Quaternion())
    R.apply(P)
    shL = R.pb["DEF-upper_arm.L"].head.copy()
    shR = R.pb["DEF-upper_arm.R"].head.copy()
    mid = (shL + shR) * 0.5
    fwd = (R.delta("DEF-spine.003") @ Vector((0, -1, 0))).normalized()
    up = (R.delta("DEF-spine.003") @ Vector((0, 0, 1))).normalized()
    left = Vector((1, 0, 0))
    wrist = mid - left * 0.11 + fwd * 0.20 - up * 0.30
    muzzle = (left * 0.72 + up * 0.62 + fwd * 0.30)
    P["rifle"] = rok_poses.rifle_from_right(R, wrist, muzzle, up=fwd + up * 0.3, roll=0)
    P["elbow_R"] = Vector((-0.55, 0.45, 0.70))
    P["elbow_L"] = Vector((0.40, -0.05, 0.55))
    P["fingers_L"] = 0.8
    P = rok_poses.chestify(R, P)
    return P
