# 2026-11-24 내보낸 FBX(결과물)를 몸 메시에 그대로 입혀 그리는 확인 렌더 (관절 검사의 대표 프레임 그림용).
# 실행: blender -b --factory-startup --python scripts/loader_joint_render.py -- <out.png> <폴더> <클립:프레임,프레임;클립:프레임...> [보기,보기] [확대]
#   보기: sideR · side · qf · q · front · top · back (기본 sideR,qf). 확대: "zoom=x,y,z,크기" (탄약수 루트 기준 Blender 좌표)
# 몸 메시 사본의 정점 그룹을 내보내기 뼈 이름으로 바꾸고, 기준 블렌드의 내보내기 뼈대(Armature, 올바른 기준 자세)에
# FBX 각 프레임의 뼈 월드 행렬을 그대로 옮긴다 → 게임(Unity)이 받는 뼈 값 그대로의 모습. 로켓은 _prop.json, 소총은 FBX의 Rifle 뼈.
import io
import json
import math
import os
import subprocess
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rok_common as C
import rok_render as RR

argv = sys.argv[sys.argv.index("--") + 1:]
OUTP, SRC, SPEC = argv[0], argv[1], argv[2]
VIEWS = argv[3].split(",") if len(argv) > 3 and not argv[3].startswith("zoom") else ["sideR", "qf"]
ZOOM = None
for a in argv[3:]:
    if a.startswith("zoom="):
        v = [float(x) for x in a[5:].split(",")]
        ZOOM = (Vector(v[:3]), v[3])

bpy.ops.wm.open_mainfile(filepath=C.BASE_BLEND)
scene = bpy.context.scene
arm = bpy.data.objects["Armature"]
DEF2EXP = {v: k for k, v in C.EXP2DEF.items()}
DEF2EXP["rifle"] = "Rifle"


def skin_copy(name):
    src = bpy.data.objects[name]
    o = src.copy()
    o.data = src.data.copy()
    scene.collection.objects.link(o)
    mw = src.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
    for g in list(o.vertex_groups):
        n = g.name
        e = DEF2EXP.get(n)
        if e is None and n.endswith(".001") and n[:-4] in DEF2EXP:
            e = DEF2EXP[n[:-4]]
        if e is None:
            o.vertex_groups.remove(g)
        else:
            if e in o.vertex_groups and o.vertex_groups[e] != g:
                o.vertex_groups.remove(g)      # 같은 이름이 이미 있으면 (드묾) 버림
            else:
                g.name = e
    for m in list(o.modifiers):
        o.modifiers.remove(m)
    md = o.modifiers.new("Arm", "ARMATURE")
    md.object = arm
    src.hide_render = True
    return o


bodies = [skin_copy(n) for n in ("Body_Low", "Cap_Low", "M1_Garand")]
for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine", "Floor"):
    if o in bpy.data.objects:
        bpy.data.objects[o].hide_render = True
rig = bpy.data.objects["ROK_Rig"]
rig.hide_render = True

# 로켓
def import_mesh(path, name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in objs if o.type == "MESH"]
    for o in objs:
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    m = meshes[0]
    m.parent = None
    m.data.transform(m.matrix_world)
    m.matrix_world = Matrix.Identity(4)
    m.name = name
    return m


rk = import_mesh(os.path.join(C.ART, "Models", "Weapons", "Bazooka", "SM_WP_BazookaRocket.fbx"), "Rocket")
vs = [v.co for v in rk.data.vertices]
ys = [v.y for v in vs]
y0, y1 = min(ys), max(ys)
cx = (min(v.x for v in vs) + max(v.x for v in vs)) * 0.5
cz = (min(v.z for v in vs) + max(v.z for v in vs)) * 0.5
width = lambda lo, hi: max((abs(v.x - cx) for v in vs if lo <= v.y <= hi), default=0)
tail_y = y1 if width(y1 - 0.05, y1) > width(y0, y0 + 0.05) else y0
sign = 1.0 if tail_y == y1 else -1.0
for v in rk.data.vertices:
    p = v.co
    v.co = Vector(((p.x - cx), (p.y - tail_y) * sign, (p.z - cz)))
mat = bpy.data.materials.new("RocketMat")
mat.diffuse_color = (0.85, 0.82, 0.70, 1)
rk.data.materials.clear()
rk.data.materials.append(mat)


def u2b(v):
    return Vector((-v[0], -v[2], v[1]))


def rocket_matrix(fr):
    tail, nose, up = u2b(fr["pos"]), u2b(fr["forward"]).normalized(), u2b(fr["up"])
    y = -nose
    z = (up - y * up.dot(y)).normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = tail
    return m


# 클립 · 프레임
jobs = []
for part in SPEC.split(";"):
    clip, fr = part.split(":")
    jobs.append((clip, [int(x) for x in fr.split(",")]))

cam = RR.setup(scene, (360, 420))
gp = bpy.data.objects["GroundPlane"]
gp.location.z = 0.0
gp.hide_render = False
order = [b.name for b in arm.data.bones]
files = []
tmp = os.environ.get("JR_TMP", os.path.join(C.WORK, "loader", "joint_render_tmp"))   # 여러 개를 함께 돌릴 때 따로
os.makedirs(tmp, exist_ok=True)
for clip, frames in jobs:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, clip + ".fbx"), use_anim=True, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    src = next(o for o in new if o.type == "ARMATURE")
    for o in new:
        o.hide_render = True
    act = src.animation_data.action
    f0 = int(round(act.frame_range[0]))
    pj = os.path.join(SRC, clip + "_prop.json")
    prop = json.load(io.open(pj, encoding="utf-8")) if os.path.exists(pj) else None
    for f in frames:
        scene.frame_set(f0 + f - 1)
        W = {pb.name: src.matrix_world @ pb.matrix for pb in src.pose.bones}
        for n in order:
            arm.pose.bones[n].matrix = W[n]
            bpy.context.view_layer.update()
        if prop:
            fr = prop["frames"][f - 1]
            rk.matrix_world = rocket_matrix(fr)
            rk.hide_render = fr["state"] == "hidden"
        else:
            rk.hide_render = True
        bpy.context.view_layer.update()
        for v in VIEWS:
            if ZOOM:
                tgt, sc = ZOOM
            else:
                tgt, sc = Vector((0.0, -0.25, 0.75)), 2.0
            RR.aim(cam, v if v != "back" else "q", target=tgt, scale=sc)
            if v == "back":
                cam.location = tgt + Vector((0.0, 1.0, 0.25)).normalized() * 6.0
                cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
            for o in ("FrontWall", "Parapet", "BackWall", "ParapetLine", "Floor"):
                if o in bpy.data.objects:
                    bpy.data.objects[o].hide_render = True
            p = os.path.join(tmp, "%s_%03d_%s.png" % (clip, f, v))
            scene.render.filepath = p
            bpy.ops.render.render(write_still=True)
            files.append(("%s f%d %s" % (clip.replace("A_ROK_Loader_", ""), f, v), p))
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
cols = len(VIEWS) if len(VIEWS) > 1 else min(6, sum(len(fr) for _, fr in jobs))
tool = os.path.join(os.path.dirname(os.path.abspath(__file__)), "loader_sheet.py")
args = [os.environ.get("UV_EXE", "uv"), "run", "--no-project", "-q", "--with", "pillow", "python", "-I", tool, OUTP, str(cols)]
args += ["%s=%s" % (l, p) for l, p in files]
res = subprocess.run(args, capture_output=True, text=True, timeout=300)
print("RENDER SHEET", res.returncode, res.stdout.strip(), res.stderr.strip()[-300:])
