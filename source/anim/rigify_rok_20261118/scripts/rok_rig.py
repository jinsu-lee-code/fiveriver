# Rigify 조절 리그를 자세 값(P)으로 모는 도구.
# 자세 값은 모두 루트(=월드) 기준: 골반 위치 · 회전, 가슴 · 머리 회전, 발 · 무릎, 소총, 손.
# 조절기에는 그 값을 만족하도록 키를 넣는다(발 · 손 IK, 무릎 · 팔꿈치 방향 조절기, 척추 · 머리 조절기).
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

import rok_common as C

FINGERS = ("thumb", "f_index", "f_middle", "f_ring", "f_pinky")
UP = Vector((0, 0, 1))


# ------------------------------------------------------------------ 회전 도우미 (캐릭터: 앞 -Y, 왼쪽 +X, 위 +Z)
def rot(pitch=0.0, yaw=0.0, roll=0.0):
    """pitch: 앞으로 숙임(+), yaw: 왼쪽으로 돌림(+), roll: 왼쪽으로 기울임(+). 도."""
    return (Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((1, 0, 0), math.radians(pitch))
            @ Quaternion((0, 1, 0), math.radians(roll)))


def frame(f, b):
    f = Vector(f).normalized()
    b = Vector(b)
    b = (b - f * b.dot(f)).normalized()
    return Matrix((f, b, f.cross(b))).transposed()


def gun_rot(muzzle, up=(0, 0, 1), roll=0.0):
    """총구 방향과 위쪽 힌트로 소총의 절대 회전을 만든다. roll(도): 총구를 보는 쪽에서 시계 방향(오른쪽으로 눕힘)."""
    y = Vector(muzzle).normalized()
    x = y.cross(Vector(up)).normalized()
    z = x.cross(y)
    q = Matrix((x, y, z)).transposed().to_quaternion()
    return q @ Quaternion((0, 1, 0), math.radians(roll))


def blend(a, b, t):
    if isinstance(a, Quaternion):
        return a.slerp(b, t)
    if isinstance(a, Vector):
        return a.lerp(b, t)
    if isinstance(a, tuple):
        return tuple(blend(x, y, t) for x, y in zip(a, b))
    if isinstance(a, (int, float)):
        return a + (b - a) * t
    return a if t < 0.5 else b


def blend_pose(A, B, t):
    return {k: blend(A[k], B[k], t) for k in A}


def copy_pose(P):
    out = {}
    for k, v in P.items():
        if isinstance(v, (Vector, Quaternion)):
            out[k] = v.copy()
        elif isinstance(v, tuple):
            out[k] = tuple(x.copy() if isinstance(x, (Vector, Quaternion)) else x for x in v)
        else:
            out[k] = v
    return out


def ease(t, kind="inout"):
    t = max(0.0, min(1.0, t))
    if kind == "linear":
        return t
    if kind == "in":        # 느리게 출발
        return t * t
    if kind == "out":       # 느리게 도착
        return 1 - (1 - t) * (1 - t)
    if kind == "out3":
        return 1 - (1 - t) ** 3
    if kind == "in3":
        return t ** 3
    if kind == "soft":      # 더 완만한 S
        return t * t * t * (t * (6 * t - 15) + 10)
    return t * t * (3 - 2 * t)


class Rig:
    def __init__(self):
        self.rig = bpy.data.objects["ROK_Rig"]
        self.exp = bpy.data.objects["Armature"]
        self.pb = self.rig.pose.bones
        self.rest = {b.name: b.matrix_local.copy() for b in self.rig.data.bones}
        self.rest3 = {k: v.to_3x3().normalized() for k, v in self.rest.items()}
        self.exp_order = [b.name for b in self.exp.data.bones]
        self.exp_rest = {b.name: b.matrix_local.copy() for b in self.exp.data.bones}
        self.exp_parent = {b.name: (b.parent.name if b.parent else None) for b in self.exp.data.bones}
        j = self.exp_rest
        self.h0 = j["Hips"].translation.copy()
        self.hj0 = {"L": j["LeftUpperLeg"].translation.copy(), "R": j["RightUpperLeg"].translation.copy()}
        self.knee0 = {"L": j["LeftLowerLeg"].translation.copy(), "R": j["RightLowerLeg"].translation.copy()}
        self.ankle0 = {"L": j["LeftFoot"].translation.copy(), "R": j["RightFoot"].translation.copy()}
        self.ball0 = {"L": j["LeftToes"].translation.copy(), "R": j["RightToes"].translation.copy()}
        self.sh0 = {"L": j["LeftUpperArm"].translation.copy(), "R": j["RightUpperArm"].translation.copy()}
        self.wrist0 = {"L": j["LeftHand"].translation.copy(), "R": j["RightHand"].translation.copy()}
        self.thigh = (self.knee0["L"] - self.hj0["L"]).length
        self.shin = (self.ankle0["L"] - self.knee0["L"]).length
        self.upper = (j["LeftLowerArm"].translation - self.sh0["L"]).length
        self.fore = (self.wrist0["L"] - j["LeftLowerArm"].translation).length
        self.head_top0 = Vector((0.0, j["Head"].translation.y - 0.01, max(
            (v.co.z for o in bpy.data.objects if o.type == "MESH" and o.parent == self.rig and o.name != "M1_Garand" for v in o.data.vertices))))
        self.eye0 = Vector((0.0, j["Head"].translation.y - 0.085, j["Head"].translation.z + 0.075))
        self.hand_m0 = {s: frame(self.rest3["hand_ik." + s].col[1], UP) for s in ("L", "R")}
        self.rifle_rest3 = self.rest3["rifle"]
        self.finger_sign = {}
        self._probe_fingers()
        self.ctrls = ["torso", "hips", "chest", "neck", "head", "shoulder.L", "shoulder.R",
                      "foot_ik.L", "foot_ik.R", "toe_ik.L", "toe_ik.R", "thigh_ik_target.L", "thigh_ik_target.R",
                      "hand_ik.L", "hand_ik.R", "upper_arm_ik_target.L", "upper_arm_ik_target.R", "rifle", "grip.L", "grip.R"]
        for s in ("L", "R"):
            for f in FINGERS:
                for i in (1, 2, 3):
                    self.ctrls.append("%s.%02d.%s" % (f, i, s))
        self.prev_q = {}

    # ---------------------------------------------------------------- 기본
    def update(self):
        bpy.context.view_layer.update()

    def reset(self):
        for pb in self.pb:
            pb.matrix_basis = Matrix.Identity(4)
        for s in ("L", "R"):
            self.pb["hand_ik." + s].constraints["RifleGrip"].influence = 1.0
        self.update()

    def delta(self, name):
        """뼈의 현재 자세가 기준 자세에서 얼마나 돌았는가 (월드 회전)."""
        return (self.pb[name].matrix.to_3x3().normalized() @ self.rest3[name].inverted()).to_quaternion()

    def delta4(self, name):
        m = self.pb[name].matrix
        r = m.to_3x3().normalized().to_4x4()
        r.translation = m.translation
        return r @ self.rest[name].inverted()

    def set_world(self, name, loc, q_abs3):
        m = q_abs3.to_4x4()
        m.translation = loc
        self.pb[name].matrix = m

    def set_delta(self, name, q, loc=None):
        """조절기의 월드 회전을 '기준 자세에서 q만큼 돈 것'으로 놓는다. loc을 주면 위치도."""
        pb = self.pb[name]
        keep = pb.location.copy()
        m = (q.to_matrix() @ self.rest3[name]).to_4x4()
        m.translation = loc if loc is not None else pb.matrix.translation
        pb.matrix = m
        if loc is None:
            pb.location = keep

    def drive(self, ctrl, bone, q, iters=3):
        """조절기 ctrl을 돌려 변형 뼈 bone의 월드 회전 변화가 q가 되게 한다."""
        self.set_delta(ctrl, q)
        for _ in range(iters):
            self.update()
            err = q @ self.delta(bone).inverted()
            if err.angle < 2e-4:
                break
            self.set_delta(ctrl, err @ self.delta(ctrl))
        self.update()

    def _probe_fingers(self):
        """손가락 조절기를 어느 쪽으로 돌려야 주먹 쥐는 방향인지 리그에서 직접 확인한다."""
        self.reset()
        for s in ("L", "R"):
            palm_down = -(self.rest3["hand_ik." + s] @ Vector((0, 0, 1)))
            for f in FINGERS:
                name = "%s.01.%s" % (f, s)
                tip = "DEF-%s.03.%s" % (f, s)
                base = self.pb[tip].tail.copy()
                best = None
                for axis in ((1, 0, 0), (0, 0, 1)):
                    for sign in (1, -1):
                        self.pb[name].rotation_quaternion = Quaternion(axis, sign * 0.5)
                        self.update()
                        move = self.pb[tip].tail - base
                        if f == "thumb":   # 엄지: 검지 뿌리 쪽으로 모이는 방향
                            target = self.pb["DEF-f_index.01.%s" % s].head - base
                            score = move.dot(target.normalized())
                        else:              # 나머지: 손바닥 쪽(T포즈에서 아래)
                            score = move.dot(Vector((0, 0, -1)))
                        if best is None or score > best[0]:
                            best = (score, axis, sign)
                self.pb[name].rotation_quaternion = Quaternion()
                self.finger_sign[(f, s)] = (Vector(best[1]), best[2])
        self.update()

    # ---------------------------------------------------------------- 자세 값 만들기 도우미
    def hand_abs(self, side, fingers, back):
        """손가락 방향 · 손등 방향(월드) → hand_ik의 절대 회전."""
        return (frame(fingers, back) @ self.hand_m0[side].inverted() @ self.rest3["hand_ik." + side]).to_quaternion()

    def pelvis_from_hip(self, side, hip_joint, pelvis_rot):
        return hip_joint - pelvis_rot @ (self.hj0[side] - self.h0)

    def kneel_leg(self, side, knee, back_dir, foot_pitch=76.0, foot_yaw=None):
        """무릎을 땅에 댄 다리: 무릎 관절 위치와 정강이가 뻗는 수평 방향 → (발목 위치, 발 회전, 발가락 회전, 발가락 뿌리 위치)."""
        v0 = self.ankle0[side] - self.ball0[side]
        back = Vector((back_dir[0], back_dir[1], 0)).normalized()
        if foot_yaw is None:
            foot_yaw = math.degrees(math.atan2(back.x, back.y)) * -1.0   # 발끝이 정강이 반대쪽(뒤)으로 가지 않게: 발은 정강이 방향을 본다
        qz = Quaternion((0, 0, 1), math.radians(foot_yaw))
        q = qz @ Quaternion((1, 0, 0), math.radians(foot_pitch))
        az = self.ball0[side].z + (q @ v0).z
        s = max(-1.0, min(1.0, (az - knee.z) / self.shin))
        ankle = knee + self.shin * (math.cos(math.asin(s)) * back + s * UP)
        ball = ankle - q @ v0
        return ankle, q, qz, ball

    def base_pose(self):
        """기준 자세(T포즈의 몸, 팔은 소총을 따라감)."""
        P = {
            "pelvis_loc": self.h0.copy(), "pelvis_rot": Quaternion(), "chest_rot": Quaternion(), "head_rot": Quaternion(),
            "neck_share": 0.4,
            "foot_L": (self.ankle0["L"].copy(), Quaternion()), "foot_R": (self.ankle0["R"].copy(), Quaternion()),
            "toe_L": Quaternion(), "toe_R": Quaternion(),
            "knee_L": Vector((0.15, -2.0, 0.4)), "knee_R": Vector((-0.15, -2.0, 0.4)),
            "rifle": (Vector((0, -0.25, 0.95)), gun_rot((0.6, -0.5, 0.5))), "rifle_space": 1.0,
            "grip_L": 1.0, "grip_R": 1.0, "grip_slide_L": 0.0,
            "hand_L": (self.wrist0["L"].copy(), Quaternion()), "hand_R": (self.wrist0["R"].copy(), Quaternion()),
            "hand_space_L": "chest", "hand_space_R": "chest",
            "elbow_L": Vector((0.45, 0.10, 0.95)), "elbow_R": Vector((-0.45, 0.20, 0.95)),
            "shoulder_L": Quaternion(), "shoulder_R": Quaternion(),
            "fingers_L": 0.75, "fingers_R": 0.8, "thumb_L": 0.3, "thumb_R": 0.5, "index_R": 0.35,
        }
        return P

    # ---------------------------------------------------------------- 자세 값 → 조절기
    def apply(self, P):
        pb = self.pb
        # 1) 몸통: torso 조절기로 전체를 놓고, hips · chest 조절기로 골반 · 가슴 회전을 맞춘다
        qt = P["pelvis_rot"].slerp(P["chest_rot"], 0.5)
        t0 = self.rest["torso"].translation
        self.set_delta("torso", qt, P["pelvis_loc"] + qt @ (t0 - self.h0))
        pb["hips"].matrix_basis = Matrix.Identity(4)
        pb["chest"].matrix_basis = Matrix.Identity(4)
        self.update()
        self.drive("hips", "DEF-spine", P["pelvis_rot"])
        self.drive("chest", "DEF-spine.003", P["chest_rot"])
        for _ in range(3):
            err = P["pelvis_loc"] - pb["DEF-spine"].head
            if err.length < 1e-5:
                break
            m = pb["torso"].matrix.copy()
            m.translation += err
            pb["torso"].matrix = m
            self.update()
        # 2) 목 · 머리
        qc = self.delta("DEF-spine.003")
        rel = qc.inverted() @ P["head_rot"]
        self.drive("neck", "DEF-spine.004", qc @ Quaternion().slerp(rel, P["neck_share"]))
        self.drive("head", "DEF-spine.006", P["head_rot"])
        # 3) 어깨
        pb["shoulder.L"].rotation_quaternion = P["shoulder_L"]
        pb["shoulder.R"].rotation_quaternion = P["shoulder_R"]
        # 4) 발 IK · 무릎 방향
        for s in ("L", "R"):
            loc, q = P["foot_" + s]
            self.set_delta("foot_ik." + s, q, loc)
            pole = pb["thigh_ik_target." + s]
            m = pole.matrix.copy()
            m.translation = P["knee_" + s]
            pole.matrix = m
        self.update()
        for s in ("L", "R"):
            self.drive("toe_ik." + s, "DEF-toe." + s, P["toe_" + s], iters=2)
        # 5) 소총: 가슴과 함께 움직이는 공간 ↔ 월드
        dc = self.delta4("DEF-spine.003")
        loc, q = P["rifle"]
        w = P["rifle_space"]
        m_world = q.to_matrix().to_4x4()
        m_world.translation = loc
        m_chest = dc @ m_world
        rq = q.slerp(m_chest.to_quaternion(), w)
        rl = loc.lerp(m_chest.translation, w)
        self.set_world("rifle", rl, rq.to_matrix())
        grip = pb["grip.L"]
        grip.location = self.rest3["grip.L"].inverted() @ (self.rifle_rest3 @ C.gun(0, 0, P["grip_slide_L"]))
        # 6) 손: 소총의 손잡이 표식을 따라가는 제약의 영향력 + 뗀 손의 목표
        dh = self.delta4("DEF-spine.006")
        m_rifle = rq.to_matrix().to_4x4()
        m_rifle.translation = rl
        for s in ("L", "R"):
            pb["hand_ik." + s].constraints["RifleGrip"].influence = P["grip_" + s]
            loc, q = P["hand_" + s]
            space = P["hand_space_" + s]
            m = q.to_matrix().to_4x4()
            m.translation = loc
            if space == "chest":
                m = dc @ m
            elif space == "head":
                m = dh @ m
            elif space == "rifle":
                # 소총을 따라가는 손 목표: 값은 소총 뼈 기준(기준 자세의 손잡이 표식 = rest_rifle^-1 @ rest_grip)
                m = m_rifle @ m
            pb["hand_ik." + s].matrix = m
            pole = pb["upper_arm_ik_target." + s]
            pm = pole.matrix.copy()
            pm.translation = dc @ P["elbow_" + s]
            pole.matrix = pm
            # 7) 손가락
            curl = P["fingers_" + s]
            for f in FINGERS:
                axis, sign = self.finger_sign[(f, s)]
                for i in (1, 2, 3):
                    if f == "thumb":
                        a = P["thumb_" + s] * (0.5, 0.45, 0.5)[i - 1]
                    else:
                        c = curl
                        if f == "f_index" and s == "R":
                            c = P["index_R"]
                        a = c * (1.05, 1.45, 0.9)[i - 1]
                    pb["%s.%02d.%s" % (f, i, s)].rotation_quaternion = Quaternion(axis, sign * a)
        self.update()

    # ---------------------------------------------------------------- 키
    def key_controls(self, frame_no):
        for name in self.ctrls:
            pb = self.pb[name]
            q = pb.rotation_quaternion.copy()
            prev = self.prev_q.get(name)
            if prev is not None and prev.dot(q) < 0:
                q.negate()
                pb.rotation_quaternion = q
            self.prev_q[name] = q
            pb.keyframe_insert("location", frame=frame_no)
            pb.keyframe_insert("rotation_quaternion", frame=frame_no)
        for s in ("L", "R"):
            self.pb["hand_ik." + s].constraints["RifleGrip"].keyframe_insert("influence", frame=frame_no)

    # ---------------------------------------------------------------- 내보내기용 뼈대에 굽기
    def bake_export(self, frame_no, prev):
        """변형(DEF) 뼈의 '기준 자세 대비 변화'를 내보내기 뼈대에 옮긴다. 기준 자세가 달라도 스키닝 결과가 같다."""
        world = {}
        epb = self.exp.pose.bones
        for name in self.exp_order:
            parent = self.exp_parent[name]
            rest = self.exp_rest[name]
            base = world[parent] @ (self.exp_rest[parent].inverted() @ rest) if parent else rest
            loc = None
            if name in C.EXP2DEF:
                d = self.delta4(C.EXP2DEF[name])
                target3 = d.to_3x3() @ rest.to_3x3()
                if name == "Hips":
                    loc = (d @ rest).translation
            elif name == "Rifle":
                m = self.pb["rifle"].matrix
                target3 = m.to_3x3().normalized()
                loc = m.translation.copy()
            else:
                target3 = base.to_3x3()
            q = (base.to_3x3().inverted() @ target3).to_quaternion()
            p = prev.get(name)
            if p is not None and p.dot(q) < 0:
                q.negate()
            prev[name] = q
            bl = base.inverted() @ loc if loc is not None else Vector((0, 0, 0))
            pose = base @ (Matrix.Translation(bl) @ q.to_matrix().to_4x4())
            world[name] = pose
            pb = epb[name]
            pb.rotation_quaternion = q
            pb.location = bl
            if name in C.EXP2DEF or name == "Rifle":
                pb.keyframe_insert("rotation_quaternion", frame=frame_no)
                if loc is not None:
                    pb.keyframe_insert("location", frame=frame_no)
        return world

    # ---------------------------------------------------------------- 측정
    def measure(self, world):
        """내보내기 뼈대 기준 수치 (m)."""
        t = lambda n: world[n].translation
        head = world["Head"] @ self.exp_rest["Head"].inverted()
        out = {
            "hips": t("Hips").copy(),
            "head_top": head @ self.head_top0,
            "eye": head @ self.eye0,
            "ankle_L": t("LeftFoot").copy(), "ankle_R": t("RightFoot").copy(),
            "ball_L": t("LeftToes").copy(), "ball_R": t("RightToes").copy(),
            "knee_L": t("LeftLowerLeg").copy(), "knee_R": t("RightLowerLeg").copy(),
            "wrist_L": t("LeftHand").copy(), "wrist_R": t("RightHand").copy(),
            "elbow_L": t("LeftLowerArm").copy(), "elbow_R": t("RightLowerArm").copy(),
            "sh_L": t("LeftUpperArm").copy(), "sh_R": t("RightUpperArm").copy(),
            "grip_L": self.pb["grip.L"].matrix.translation.copy(), "grip_R": self.pb["grip.R"].matrix.translation.copy(),
            "muzzle": self.pb["rifle_muzzle"].matrix.translation.copy(), "butt": self.pb["rifle_butt"].matrix.translation.copy(),
            "rifle": self.pb["rifle"].matrix.copy(),
            "head_c": head @ (self.exp_rest["Head"].translation + Vector((0, -0.02, 0.10))),
            "def_err": max((self.pb[C.EXP2DEF[n]].head - world[n].translation).length for n in C.EXP2DEF),
        }
        return out
