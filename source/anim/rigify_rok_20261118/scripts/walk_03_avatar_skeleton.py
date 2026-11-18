# 걷기 진단 3: Unity .meta의 humanDescription.skeleton(아바타 기준 자세)으로 다리 · 발 관절 위치를 다시 쌓아,
# 무릎 굽힘 · 발 기울기 · 다리 벌림을 잰다. (Unity를 켜지 않고 아바타가 어떤 자세를 "0"으로 보는지 확인)
# 실행: python walk_03_avatar_skeleton.py <model.fbx.meta> [...]
import math
import re
import sys


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def qrot(q, v):
    x, y, z, w = q
    p = (v[0], v[1], v[2], 0.0)
    r = qmul(qmul(q, p), (-x, -y, -z, w))
    return r[:3]


def parse(path):
    text = open(path, encoding="utf-8").read()
    sk = text.split("    skeleton:")[1].split("    armTwist:")[0]
    bones = {}
    for m in re.finditer(r"- name: (.*?)\n\s+parentName: (.*?)\n\s+position: \{x: (.*?), y: (.*?), z: (.*?)\}\n\s+rotation: \{x: (.*?), y: (.*?), z: (.*?), w: (.*?)\}\n\s+scale: \{x: (.*?), y: (.*?), z: (.*?)\}", sk):
        g = m.groups()
        bones[g[0].strip()] = (g[1].strip(), tuple(float(v) for v in g[2:5]), tuple(float(v) for v in g[5:9]), tuple(float(v) for v in g[9:12]))
    return bones


def world(bones, name, cache):
    if name in cache:
        return cache[name]
    if name not in bones:
        cache[name] = ((0, 0, 0), (0, 0, 0, 1), (1, 1, 1))
        return cache[name]
    parent, pos, rot, sc = bones[name]
    pp, pr, ps = world(bones, parent, cache) if parent else ((0, 0, 0), (0, 0, 0, 1), (1, 1, 1))
    lp = (pos[0] * ps[0], pos[1] * ps[1], pos[2] * ps[2])
    wp = tuple(a + b for a, b in zip(pp, qrot(pr, lp)))
    wr = qmul(pr, rot)
    ws = (ps[0] * sc[0], ps[1] * sc[1], ps[2] * sc[2])
    cache[name] = (wp, wr, ws)
    return cache[name]


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def ang(a, b):
    la = math.sqrt(sum(x * x for x in a))
    lb = math.sqrt(sum(x * x for x in b))
    return math.degrees(math.acos(max(-1, min(1, sum(x * y for x, y in zip(a, b)) / (la * lb)))))


for path in sys.argv[1:]:
    bones = parse(path)
    cache = {}
    pre = next((n.split(":")[0] + ":" for n in bones if n.endswith("Hips")), "")
    print("META", path, len(bones), "bones")
    for S in ("Left", "Right"):
        P = {k: world(bones, pre + S + k, cache)[0] for k in ("UpLeg", "Leg", "Foot", "ToeBase")}
        hip, knee, ank, ball = P["UpLeg"], P["Leg"], P["Foot"], P["ToeBase"]
        thigh, shin, foot = sub(knee, hip), sub(ank, knee), sub(ball, ank)
        down = (0, -1, 0)
        horiz = math.hypot(foot[0], foot[2])
        print("  %s knee bend %.1f deg | thigh from vertical %.1f | shin from vertical %.1f | foot pitch down %.1f deg | foot yaw out %.1f deg | hip %s ankle %s ball %s" % (
            S, ang(thigh, shin), ang(thigh, down), ang(shin, down), math.degrees(math.atan2(-foot[1], horiz)),
            math.degrees(math.atan2(foot[0], foot[2])) * (1 if S == "Left" else -1),
            tuple(round(v, 3) for v in hip), tuple(round(v, 3) for v in ank), tuple(round(v, 3) for v in ball)))
