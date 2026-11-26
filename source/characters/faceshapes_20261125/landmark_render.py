# 표식 찾기용 정사영 렌더 (Workbench, 텍스처, 5mm 눈금, 10mm 라벨, 단면)
# 사용: blender -b --factory-startup --python landmark_render.py -- <tag> [cx cz scale name] ...
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import fs_common as fc

args = sys.argv[sys.argv.index("--") + 1:]
tag = args[0]
fbx, meshes, arm = fc.load_target(tag)
o = fc.body_mesh(meshes)
co, wco, mw = fc.arrays(o)
hm = fc.head_mat_verts(o)
P = wco[hm]
cz_face = float(np.percentile(P[:, 2], 50))
ymin = float(P[:, 1].min())
outd = os.path.join(fc.WORK, "landmarks")
os.makedirs(outd, exist_ok=True)

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_backface_culling = False
sc.display.shading.show_cavity = False
sc.render.film_transparent = False
try:
    sc.view_settings.view_transform = "Standard"
except TypeError:
    pass
R = 1000
sc.render.resolution_x = sc.render.resolution_y = R
for m in meshes:
    m.color = (1, 1, 1, 1)

# 뷰 목록: (이름, 종류, 중심 가로, 중심 세로, 스케일, 단면 x)
views = []
if len(args) > 1:
    rest = args[1:]
    for i in range(0, len(rest), 6):
        nm, kind, cu, cv, s, sx = rest[i:i + 6]
        views.append((nm, kind, float(cu), float(cv), float(s), float(sx)))
else:
    views = [("front", "front", 0.0, cz_face, 0.18, 0), ("side", "side", float(np.median(P[:, 1])), cz_face, 0.18, 0),
             ("sec0", "section", float(np.median(P[:, 1])), cz_face, 0.18, 0.0)]

def slice_curve(sx, axis=0):
    """월드 좌표 메시를 평면(axis=sx)으로 잘라 단면선을 굵은 곡선으로 만든다."""
    dg = bpy.context.evaluated_depsgraph_get()
    cu = bpy.data.curves.new("slice", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 0.00025
    for m in meshes:
        me = m.data
        me.calc_loop_triangles()
        M = np.array(m.matrix_world)
        n = len(me.vertices)
        c = np.empty(n * 3); me.vertices.foreach_get("co", c); c = c.reshape(-1, 3)
        W = c @ M[:3, :3].T + M[:3, 3]
        nt = len(me.loop_triangles)
        tri = np.empty(nt * 3, np.int32); me.loop_triangles.foreach_get("vertices", tri); tri = tri.reshape(-1, 3)
        d = W[:, axis] - sx
        dt = d[tri]
        cand = np.where((dt.min(1) < 0) & (dt.max(1) > 0))[0]
        for t in cand:
            pts = []
            for a, b in ((0, 1), (1, 2), (2, 0)):
                ia, ib = tri[t, a], tri[t, b]
                if (d[ia] < 0) != (d[ib] < 0):
                    f = d[ia] / (d[ia] - d[ib])
                    pts.append(W[ia] + (W[ib] - W[ia]) * f)
            if len(pts) == 2:
                sp = cu.splines.new("POLY")
                sp.points.add(1)
                for k, p in enumerate(pts):
                    sp.points[k].co = (p[0], p[1], p[2], 1)
    ob = bpy.data.objects.new("slice", cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.color = (1, 0.9, 0.1, 1)
    mat = bpy.data.materials.new("sl"); mat.diffuse_color = (1, 0.9, 0.1, 1); cu.materials.append(mat)
    return ob


def wire_curve(cu0, cv0, s):
    """정면 상자 안 앞쪽 면의 모서리를 가는 곡선으로 (정점 밀도 확인용)."""
    cu = bpy.data.curves.new("slicew", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = s / 4000
    for m in meshes:
        me = m.data
        M = np.array(m.matrix_world)
        n = len(me.vertices)
        c = np.empty(n * 3); me.vertices.foreach_get("co", c); c = c.reshape(-1, 3)
        W = c @ M[:3, :3].T + M[:3, 3]
        ne = len(me.edges)
        e = np.empty(ne * 2, np.int32); me.edges.foreach_get("vertices", e); e = e.reshape(-1, 2)
        inb = (np.abs(W[:, 0] - cu0) < s / 2) & (np.abs(W[:, 2] - cv0) < s / 2) & (W[:, 1] < -0.02)
        for a, b in e[inb[e[:, 0]] & inb[e[:, 1]]]:
            sp = cu.splines.new("POLY"); sp.points.add(1)
            pa, pb = W[a].copy(), W[b].copy()
            pa[1] -= 0.0003; pb[1] -= 0.0003
            sp.points[0].co = (*pa, 1); sp.points[1].co = (*pb, 1)
    ob = bpy.data.objects.new("slicew", cu)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.new("wr"); mat.diffuse_color = (0.1, 1, 0.2, 1); cu.materials.append(mat)
    ob.color = (0.1, 1, 0.2, 1)


for nm, kind, cu, cv, s, sx in views:
    fc.clear()
    for ob in [ob for ob in bpy.data.objects if ob.name.startswith("slice")]:
        bpy.data.objects.remove(ob)
    for m in meshes:
        m.hide_render = (kind == "slice")
    if kind == "slice":
        slice_curve(sx)
        sc.display.shading.color_type = "MATERIAL"
    elif kind == "frontw":
        wire_curve(cu, cv, s)
        sc.display.shading.color_type = "TEXTURE"
        kind = "front"
    else:
        sc.display.shading.color_type = "TEXTURE"
    step = 0.005 if s > 0.1 else (0.002 if s > 0.05 else 0.001)
    major = 0.01 if s > 0.1 else 0.01
    fs = s / 40
    if kind == "front":
        fc.make_cam((cu, ymin - 1.0, cv), (cu, ymin, cv), ortho=s)
        # 라벨: 왼쪽 가장자리에 z, 아래쪽에 x (mm)
        for k in range(-20, 21):
            z = round(cv / 0.01) * 0.01 + k * 0.01
            if abs(z - cv) < s / 2 * 0.95:
                fc.label(f"{z*1000:.0f}", (cu - s / 2 + fs * 2, ymin - 0.2, z), (math.pi / 2, 0, 0), fs)
            x = round(cu / 0.01) * 0.01 + k * 0.01
            if abs(x - cu) < s / 2 * 0.95:
                fc.label(f"{x*1000:.0f}", (x, ymin - 0.2, cv - s / 2 + fs), (math.pi / 2, 0, 0), fs)
        sgn = 1
    else:
        camx = sx + 1.0
        clip = 1.0 if kind == "section" else 0.01
        fc.make_cam((camx, cu, cv), (sx, cu, cv), ortho=s, clip_start=clip)
        lx = camx - (1.0 - 0.3) if kind != "section" else camx - 1.0 + 0.0
        for k in range(-20, 21):
            z = round(cv / 0.01) * 0.01 + k * 0.01
            if abs(z - cv) < s / 2 * 0.95:
                fc.label(f"{z*1000:.0f}", (0.3, cu - s / 2 + fs * 2, z), (math.pi / 2, 0, math.pi / 2), fs)
            y = round(cu / 0.01) * 0.01 + k * 0.01
            if abs(y - cu) < s / 2 * 0.95:
                fc.label(f"{y*1000:.0f}", (0.3, y, cv - s / 2 + fs), (math.pi / 2, 0, math.pi / 2), fs)
        sgn = 1
        if kind == "section":
            # 라벨이 클립에 걸리지 않게 단면 평면 바로 앞에 둔다
            for t in [ob for ob in bpy.data.objects if ob.type == "FONT"]:
                t.location.x = sx - 0.0005
    path = os.path.join(outd, f"{tag}_{nm}.png")
    fc.render(path)
    fc.draw_grid(path, R, R, cu, cv, s, step, major, sgn)
print("@@JSON@@" + json.dumps({"cz_face": cz_face, "ymin": ymin, "mesh": o.name}))
