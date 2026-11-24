# 2026-11-24 손 · 아래팔이 대상(머리 · 모자 · 몸통 · 허벅지 · 무릎 · 탄입대)에 "관통 없이 닿아 있는지"를 실제 메시로 잰다.
# loader_joint_check.py 의 단면 근사(모자 챙 · 탄입대가 단면을 키움)와 달리, 내보낸 뼈 값으로 변형한 메시 표면까지의 거리를 BVH로 잰다.
# 실행: blender -b --factory-startup --python scripts/ally_contact.py -- <폴더> "<클립>:<프레임,..>;..." "<부위>:<대상>,..."
#   부위: handL handR foreL foreR   대상: head(머리+모자) torso(골반~윗가슴 + 탄입대 · 벨트) thighL thighR shinL shinR kneeL kneeR
# 출력: CONTACT <클립> <프레임> <부위>><대상> gap_mm(+ = 떨어짐, 가장 가까운 정점) depth_mm(안으로 들어간 가장 깊은 정점) inside_n
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, SPEC, PAIRS = argv[0], argv[1], argv[2]

bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
scene = bpy.context.scene
arm = bpy.data.objects["Armature"]
DEF2EXP = {v: k for k, v in C.EXP2DEF.items()}


def skin_copy(name):
    src = bpy.data.objects[name]
    o = src.copy()
    o.data = src.data.copy()
    scene.collection.objects.link(o)
    mw = src.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
    for g in list(o.vertex_groups):
        e = DEF2EXP.get(g.name)
        if e is None and g.name.endswith(".001") and g.name[:-4] in DEF2EXP:
            e = DEF2EXP[g.name[:-4]]
        if e is None or (e in o.vertex_groups and o.vertex_groups[e] != g):
            o.vertex_groups.remove(g)
        else:
            g.name = e
    for m in list(o.modifiers):
        o.modifiers.remove(m)
    o.modifiers.new("Arm", "ARMATURE").object = arm
    return o


body = skin_copy("Body_Low")
cap = skin_copy("Cap_Low")
gname = {g.index: g.name for g in body.vertex_groups}


def dom(v):
    best = max(v.groups, key=lambda g: g.weight, default=None)
    return gname.get(best.group) if best else None


DOM = [dom(v) for v in body.data.vertices]
FING = ("Thumb", "Index", "Middle", "Ring", "Little")
SETS = {
    "handL": lambda b: b == "LeftHand" or (b or "").startswith("Left") and any(k in b for k in FING),
    "handR": lambda b: b == "RightHand" or (b or "").startswith("Right") and any(k in b for k in FING),
    "foreL": lambda b: b == "LeftLowerArm", "foreR": lambda b: b == "RightLowerArm",
    "head": lambda b: b in ("Head", "Neck"),
    "torso": lambda b: b in ("Hips", "Spine", "Chest", "UpperChest"),
    "thighL": lambda b: b == "LeftUpperLeg", "thighR": lambda b: b == "RightUpperLeg",
    "shinL": lambda b: b == "LeftLowerLeg", "shinR": lambda b: b == "RightLowerLeg",
    "kneeL": lambda b: b in ("LeftUpperLeg", "LeftLowerLeg"), "kneeR": lambda b: b in ("RightUpperLeg", "RightLowerLeg"),
}
pairs = [p.split(":") for p in PAIRS.split(",")]
RAY_DIR = {   # 광선 방향 (바깥 → 대상)
    "head": lambda W: -(W["Head"].to_3x3() @ Vector((0, 1, 0))).normalized(),     # 머리 위에서 아래로 (모자 정수리 · 뒤통수)
    "kneeL": lambda W: Vector((0, 0, -1)), "kneeR": lambda W: Vector((0, 0, -1)),
    "pouchR": lambda W: Vector((0, 1, 0)),                                           # 앞에서 뒤로
}
POUCH_MAT = {i for i, m in enumerate(body.data.materials) if m and "pauch" in m.name.lower()}
order = [b.name for b in arm.data.bones]

for part in SPEC.split(";"):
    clip, fr = part.split(":")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, clip + ".fbx"), use_anim=True, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    srcarm = next(o for o in new if o.type == "ARMATURE")
    f0 = int(round(srcarm.animation_data.action.frame_range[0]))
    frames = []
    for x in fr.split(","):
        if "-" in x:
            a, b = x.split("-")
            frames += list(range(int(a), int(b) + 1))
        else:
            frames.append(int(x))
    for f in frames:
        scene.frame_set(f0 + f - 1)
        W = {pb.name: srcarm.matrix_world @ pb.matrix for pb in srcarm.pose.bones}
        for n in order:
            arm.pose.bones[n].matrix = W[n]
            bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        eb = body.evaluated_get(dg)
        me = eb.to_mesh()
        P = [eb.matrix_world @ v.co for v in me.vertices]
        polys = [tuple(p.vertices) for p in me.polygons]
        ec = cap.evaluated_get(dg)
        mc = ec.to_mesh()
        PC = [ec.matrix_world @ v.co for v in mc.vertices]
        polys_c = [tuple(p.vertices) for p in mc.polygons]
        for src, tgt in pairs:
            sv = [i for i, b in enumerate(DOM) if SETS[src](b)]
            if tgt == "pouchR":     # 오른쪽 앞 탄입대 (재질 이름에 Pauch, 몸 오른쪽)
                tf = [tuple(p.vertices) for p in me.polygons if p.material_index in POUCH_MAT and (eb.matrix_world @ p.center).x < -0.04]
            else:
                tf = [p for p in polys if all(SETS[tgt](DOM[i]) for i in p)]
            used = sorted(set(i for p in tf for i in p))
            remap = {i: k for k, i in enumerate(used)}
            verts = [P[i] for i in used]
            faces = [tuple(remap[i] for i in p) for p in tf]
            if tgt == "head":
                off = len(verts)
                verts += PC
                faces += [tuple(i + off for i in p) for p in polys_c]
            bvh = BVHTree.FromPolygons(verts, faces)
            # 안 · 밖: 정점에서 6방향(±x ±y ±z)으로 쏜 광선이 모두 0.25 m 안에서 대상에 맞으면 안(둘러싸임).
            # 안이면 가장 가까운 표면까지 = 관통 깊이, 밖이면 = 떨어진 거리. (면 법선 방향에 기대지 않음 — 옷 · 장비 메시 법선이 섞여 있음)
            DIRS = [Vector(v) for v in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
            gap, depth, nin = 9.0, 0.0, 0
            for i in sv:
                near = bvh.find_nearest(P[i])
                if near[0] is None:
                    continue
                dist = near[3]
                if dist < 0.06 and all(bvh.ray_cast(P[i], d, 0.25)[0] is not None for d in DIRS):
                    nin += 1
                    depth = max(depth, dist)
                    gap = min(gap, -dist)
                else:
                    gap = min(gap, dist)
            print("CONTACT %s %d %s>%s gap %.1f depth %.1f inside %d" % (clip, f, src, tgt, gap * 1000, depth * 1000, nin))
            continue
            gap, depth, nin = 9.0, 0.0, 0
            for i in sv:
                hit = bvh.find_nearest(P[i])
                if hit[0] is None:
                    continue
                loc, nrm, _, dist = hit
                inside = (P[i] - loc).dot(nrm) < 0
                if inside:
                    nin += 1
                    depth = max(depth, dist)
                    gap = min(gap, -dist)
                else:
                    gap = min(gap, dist)
            print("CONTACT %s %d %s>%s gap %.1f depth %.1f inside %d" % (clip, f, src, tgt, gap * 1000, depth * 1000, nin))
        eb.to_mesh_clear()
        ec.to_mesh_clear()
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
