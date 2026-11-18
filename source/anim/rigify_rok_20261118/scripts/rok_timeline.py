# 키 자세(포즈) → 프레임별 자세. 채널 묶음마다 시간 지연(골반이 먼저, 가슴 · 팔 · 머리가 뒤따름)과 가감속을 둔다.
import math

from mathutils import Quaternion, Vector

from rok_rig import blend, ease, copy_pose

GROUPS = {
    "base": ("pelvis_loc", "pelvis_rot", "foot_L", "foot_R", "toe_L", "toe_R", "knee_L", "knee_R"),
    "chest": ("chest_rot", "shoulder_L", "shoulder_R", "neck_share"),
    "head": ("head_rot",),
    "arms": ("rifle", "rifle_space", "grip_L", "grip_R", "grip_slide_L", "hand_L", "hand_R", "hand_space_L", "hand_space_R",
             "elbow_L", "elbow_R", "fingers_L", "fingers_R", "thumb_L", "thumb_R", "index_R"),
}
LAGS = {"base": 0.0, "chest": 0.05, "head": 0.10, "arms": 0.07}


class Key:
    def __init__(self, t, pose, kind="inout", lift_L=0.0, lift_R=0.0, groups=None):
        self.t, self.pose, self.kind = t, pose, kind
        self.lift = {"L": lift_L, "R": lift_R}
        self.kinds = groups or {}       # 묶음별 가감속을 따로 줄 때


class Timeline:
    def __init__(self, keys, length, lags=None):
        self.keys = sorted(keys, key=lambda k: k.t)
        self.length = length
        self.lags = dict(LAGS)
        if lags:
            self.lags.update(lags)

    def _tau(self, t, lag):
        if lag <= 0:
            return t
        ramp = 0.3
        s = min(1.0, t / ramp, max(0.0, self.length - t) / ramp)
        return t - lag * max(0.0, s)

    def _segment(self, tau):
        ks = self.keys
        if tau <= ks[0].t:
            return ks[0], ks[0], 0.0
        for a, b in zip(ks, ks[1:]):
            if tau <= b.t:
                return a, b, (tau - a.t) / max(1e-6, b.t - a.t)
        return ks[-1], ks[-1], 1.0

    def sample(self, t):
        out = {}
        for group, chans in GROUPS.items():
            tau = self._tau(t, self.lags[group])
            a, b, u = self._segment(tau)
            w = ease(u, b.kinds.get(group, b.kind))
            for c in chans:
                out[c] = blend(a.pose[c], b.pose[c], w)
            if group == "base" and a is not b:
                for s in ("L", "R"):
                    h = b.lift[s]
                    if h:
                        loc, q = out["foot_" + s]
                        uu = ease(u, "inout")
                        out["foot_" + s] = (loc + Vector((0, 0, 4 * uu * (1 - uu) * h)), q)
        return out


# ------------------------------------------------------------------ 덧붙이는 생기 (숨 · 무게 이동 · 미세한 고개)
def qx(deg):
    return Quaternion((1, 0, 0), math.radians(deg))


def qy(deg):
    return Quaternion((0, 1, 0), math.radians(deg))


def qz(deg):
    return Quaternion((0, 0, 1), math.radians(deg))


def breathe(P, t, period=2.5, amount=1.0, envelope=1.0):
    """숨: 들이쉴 때 가슴이 들리고(뒤로 1.5도) 골반이 3 mm 오른다. t=0에서 0."""
    b = (1 - math.cos(2 * math.pi * t / period)) / 2 * amount * envelope
    P["chest_rot"] = qx(-2.2 * b) @ P["chest_rot"]
    P["pelvis_loc"] = P["pelvis_loc"] + Vector((0, 0, 0.003 * b))
    P["head_rot"] = qx(0.6 * b) @ P["head_rot"]
    return b


def drift(P, t, period, sway=0.010, head_yaw=2.5, envelope=1.0, phase=0.0):
    """느린 무게 이동: 골반이 좌우로 옮겨 가고 가슴 · 머리가 조금 따라감. t=0에서 0."""
    s = math.sin(2 * math.pi * t / period + phase) - math.sin(phase)
    s *= envelope
    P["pelvis_loc"] = P["pelvis_loc"] + Vector((sway * s, 0, -0.002 * abs(s)))
    P["pelvis_rot"] = qy(-1.0 * s) @ P["pelvis_rot"]
    P["chest_rot"] = qy(0.6 * s) @ qz(0.8 * s) @ P["chest_rot"]
    h = math.sin(2 * math.pi * t / period * 2 + 0.7) - math.sin(0.7)
    P["head_rot"] = qz(head_yaw * h * envelope) @ qy(0.5 * s) @ P["head_rot"]


def envelope(t, length, edge=0.35):
    """끝 자세를 정확히 맞추기 위해 덧붙이는 움직임을 양 끝에서 0으로."""
    if length <= 2 * edge:
        return math.sin(math.pi * t / length) ** 2
    a = min(1.0, t / edge, (length - t) / edge)
    return ease(max(0.0, a), "inout")
