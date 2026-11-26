# 아군 진지 꾸미기 소품 (2026-11-25, 사용자 지시 "이느낌으로 아군 진지 다시 바꾸어봐" — 1950 한국전 산비탈 진지 사진:
# 불규칙한 흙 둔덕, 솔가지 위장, 낙엽 · 잔가지 · 통나무 · 판초, 모래주머니는 군데군데)
# 여기서 만드는 것: 파낸 흙 둔덕 3벌, 잔가지 6벌, 판초(천막 반쪽) 2벌 → SM_PD_PositionProps.fbx (오브젝트마다 메시 하나)
# 실행: blender -b --factory-startup -P make_position_props.py  (여러 번 돌려도 결과 같음)
import math
import os
import random

import bpy
import bmesh
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "five_river", "Assets", "_Project", "Art", "MapPolish20261105", "PositionDressing20261125")


def u2b(x, y, z):
    return (-x, -z, y)


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def new_obj(name, bm, mats, face_up=False):
    me = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if face_up:
        # 열린 판(흙 둔덕 · 판초): 위를 보게 (Blender +Z = Unity 위). 뒤집혀 있으면 뒤에서 빛을 받아 검게 보였음
        bm.normal_update()
        up = sum((f.normal.z * f.calc_area() for f in bm.faces), 0.0)
        if up < 0:
            bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for m in mats:
        me.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
    return ob


def grid(bm, uvl, nx, nz, pos_uv):
    """pos_uv(i, j) -> (Unity 점, uv). 사각 격자 면."""
    vs = [[None] * (nz + 1) for _ in range(nx + 1)]
    uvs = [[None] * (nz + 1) for _ in range(nx + 1)]
    for i in range(nx + 1):
        for j in range(nz + 1):
            p, uv = pos_uv(i, j)
            vs[i][j] = bm.verts.new(u2b(*p))
            uvs[i][j] = uv
    for i in range(nx):
        for j in range(nz):
            f = bm.faces.new([vs[i][j], vs[i][j + 1], vs[i + 1][j + 1], vs[i + 1][j]])
            for loop, (a, b) in zip(f.loops, [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]):
                loop[uvl].uv = uvs[a][b]
    return vs


def spoil_mound(name, length, seed):
    """흉장 안쪽 팔꿈치 받침 위로 무너져 내린 파낸 흙. 로컬: x 참호 방향, z 깊이(+z = 흉장 쪽, 높음), y 위. 바닥 y=0."""
    rng = random.Random(seed)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    nx, nz = 64, 22
    H = rng.uniform(0.30, 0.36)
    off = Vector((seed * 1.3, seed * 0.7, seed * 2.1))

    def pu(i, j):
        x = -length / 2 + length * i / nx
        z = -0.40 + 0.74 * j / nz
        ex = smooth01((length / 2 - abs(x)) / 0.35)            # 양 끝은 낮아져 땅에 묻힘
        rise = smooth01((z + 0.36) / 0.58)                       # 앞(참호 쪽)은 낮고 흉장 쪽은 높게
        h = H * rise * (0.75 + 0.25 * ex)
        # 앞끝은 흙벽 모서리를 2~3 cm 덮고 벽 속으로 꺾여 묻힘 (더 내밀면 흘러내린 끝이 벽 앞 허공에 떠 턱이 보였음)
        h -= 0.20 * smooth01((-0.33 - z) / 0.07)
        q = Vector((x, z, 0)) + off
        h += 0.05 * noise.noise(q * 2.2) + 0.028 * noise.noise(q * 7.0) + 0.014 * noise.noise(q * 19.0) + 0.006 * noise.noise(q * 45.0)
        # 흙덩이 · 돌: 군데군데 혹
        h += 0.05 * max(0.0, noise.noise(q * 9.0 + Vector((5, 5, 5))) - 0.2) * 2.0
        h += 0.025 * max(0.0, noise.noise(q * 23.0 + Vector((1, 9, 3))) - 0.35) * 3.0
        h = h * (0.35 + 0.65 * ex) - 0.08 * (1 - ex)     # 끝은 땅 아래로
        if j == 0:
            h = -0.24
        y = h + 0.0
        zz = z + 0.025 * noise.noise(q * 3.0 + Vector((9, 0, 0)))
        return (x, y, zz), (x, z + y)
    grid(bm, uvl, nx, nz, pu)
    return new_obj(name, bm, ["M_PD_Spoil"], face_up=True)


def twig(name, seed):
    """잔가지: 굽은 줄기 + 곁가지 1~3개, 6각 단면. 로컬 바닥 y=0 근처에 누움."""
    rng = random.Random(seed)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")

    def branch(p0, d, length, r0, segs):
        pts = [p0]
        dirv = d.normalized()
        for s in range(segs):
            dirv = (dirv + Vector((rng.uniform(-.25, .25), rng.uniform(-.05, .08), rng.uniform(-.25, .25)))).normalized()
            pts.append(pts[-1] + dirv * (length / segs))
        rings = []
        n = 6
        for k, p in enumerate(pts):
            t = k / (len(pts) - 1)
            r = max(0.0015, r0 * (1 - 0.8 * t))
            fwd = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
            side = fwd.cross(Vector((0, 1, 0)))
            if side.length < 1e-4:
                side = Vector((1, 0, 0))
            side.normalize(); up = side.cross(fwd).normalized()
            ring = []
            for a in range(n + 1):
                th = 2 * math.pi * a / n
                v = p + (side * math.cos(th) + up * math.sin(th)) * r
                ring.append((bm.verts.new(u2b(*v)), (a / n, k * length / segs / 0.25)))
            rings.append(ring)
        for k in range(len(rings) - 1):
            for a in range(n):
                q = [rings[k][a], rings[k + 1][a], rings[k + 1][a + 1], rings[k][a + 1]]
                f = bm.faces.new([x[0] for x in q])
                for loop, x in zip(f.loops, q):
                    loop[uvl].uv = x[1]
        return pts

    L = rng.uniform(0.5, 1.3)
    r0 = rng.uniform(0.008, 0.018)
    yaw = rng.uniform(0, math.pi)
    d = Vector((math.cos(yaw), 0.02, math.sin(yaw)))
    pts = branch(Vector((-d.x * L / 2, r0, -d.z * L / 2)), d, L, r0, 8)
    for _ in range(rng.randint(1, 3)):
        k = rng.randint(2, 5)
        side = Vector((-d.z, 0, d.x)) * rng.choice((-1, 1))
        bd = (d * 0.6 + side * 0.8).normalized()
        branch(pts[k], bd, L * rng.uniform(0.25, 0.5), r0 * 0.5, 4)
    return new_obj(name, bm, ["M_PD_Twig"])


def poncho(name, seed):
    """땅이나 흉장에 던져 놓은 판초(천막 반쪽, 약 1.6 × 1.0 m): 주름진 천."""
    rng = random.Random(seed)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    nx, nz = 26, 18
    W, D = rng.uniform(1.3, 1.6), rng.uniform(0.8, 1.0)
    off = Vector((seed * 0.9, seed * 1.9, 0))
    fold_z = rng.uniform(-0.15, 0.15)

    def pu(i, j):
        u, v = i / nx, j / nz
        x = (u - 0.5) * W
        z = (v - 0.5) * D
        q = Vector((x, z, 0)) + off
        # 큰 주름(한 방향으로 잡힌 골) + 잔주름 + 가장자리 늘어짐
        y = 0.035 * max(0.0, math.sin((x * 3.2 + z * 1.1) * 2.1 + seed)) ** 2
        y += 0.03 * noise.noise(q * 2.5) + 0.012 * noise.noise(q * 8.0)
        y += 0.05 * smooth01(1 - abs(z - fold_z) / 0.18)        # 접혀 겹친 두툼한 띠
        edge = min(u, 1 - u, v, 1 - v)
        y -= 0.02 * (1 - smooth01(edge / 0.08))
        y = max(y, 0.004)
        return (x, y, z), (u * W / 0.3, v * D / 0.3)
    grid(bm, uvl, nx, nz, pu)
    return new_obj(name, bm, ["M_PD_Poncho"], face_up=True)


def tube(bm, uvl, pts, r0, r1, n=6, mat=0):
    rings = []
    total = 0.0
    for k, p in enumerate(pts):
        if k:
            total += (p - pts[k - 1]).length
        t = k / (len(pts) - 1)
        r = r0 + (r1 - r0) * t
        fwd = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        side = fwd.cross(Vector((0, 1, 0)))
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up = side.cross(fwd).normalized()
        ring = []
        for a in range(n + 1):
            th = 2 * math.pi * a / n
            v = p + (side * math.cos(th) + up * math.sin(th)) * r
            ring.append((bm.verts.new(u2b(*v)), (a / n, total / 0.4)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for a in range(n):
            q = [rings[k][a], rings[k + 1][a], rings[k + 1][a + 1], rings[k][a + 1]]
            f = bm.faces.new([x[0] for x in q])
            f.material_index = mat
            for loop, x in zip(f.loops, q):
                loop[uvl].uv = x[1]


def needle_tuft(bm, uvl, p, axis, rng, mat, count, length):
    """솔잎 다발: 가지 끝 쪽으로 퍼진 솔잎. 솔잎 하나 = 십자로 엇갈린 가는 삼각형 두 장(어느 쪽에서도 보임)."""
    axis = axis.normalized()
    side = axis.cross(Vector((0, 1, 0)))
    if side.length < 1e-4:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(axis).normalized()
    for _ in range(count):
        th = rng.uniform(0, 2 * math.pi)
        cone = math.radians(rng.uniform(25, 70))
        d = (axis * math.cos(cone) + (side * math.cos(th) + up * math.sin(th)) * math.sin(cone)).normalized()
        d.y -= 0.12   # 무게로 살짝 처짐
        d.normalize()
        L = length * rng.uniform(0.8, 1.15)
        tip = p + d * L
        w = 0.0016
        ref = up if abs(d.dot(up)) < 0.9 else side
        s1 = d.cross(ref).normalized() * w
        s2 = d.cross(s1).normalized() * w
        for s_ in (s1, s2):
            a = bm.verts.new(u2b(*(p + s_)))
            b = bm.verts.new(u2b(*(p - s_)))
            c = bm.verts.new(u2b(*tip))
            f = bm.faces.new([a, b, c])
            f.material_index = mat
            for loop, uv in zip(f.loops, ((0.0, 0.0), (0.02, 0.0), (0.01, 1.0))):
                loop[uvl].uv = uv


def pine_bough(name, seed, density=1.0):
    """꺾어 온 솔가지(위장용): 줄기 + 곁가지, 가지 끝 쪽 60 % 에 솔잎 다발. 로컬: 밑동 원점, +x 로 뻗음, 바닥 y=0 근처.
    재질 0 껍질, 1 푸른 솔잎, 2 시든 솔잎(꺾어 둔 지 며칠)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    Lm = rng.uniform(0.8, 1.35)

    def polyline(p0, d, L, segs, wob):
        pts = [p0]
        dv = d.normalized()
        for _ in range(segs):
            dv = (dv + Vector((rng.uniform(-wob, wob), rng.uniform(-wob * .4, wob * .5), rng.uniform(-wob, wob)))).normalized()
            pts.append(pts[-1] + dv * (L / segs))
        return pts
    stem = polyline(Vector((0, 0.03, 0)), Vector((1, 0.12, 0)), Lm, 10, 0.12)
    tube(bm, uvl, stem, rng.uniform(0.012, 0.018), 0.004, 6, 0)
    branches = [stem]
    nb = rng.randint(5, 9)
    for b in range(nb):
        t = 0.15 + 0.75 * b / max(1, nb - 1)
        k = int(t * (len(stem) - 1))
        base = stem[k]
        fwd = (stem[min(k + 1, len(stem) - 1)] - stem[max(k - 1, 0)]).normalized()
        sidev = fwd.cross(Vector((0, 1, 0))).normalized() * (1 if b % 2 else -1)
        d = (fwd * 0.7 + sidev * rng.uniform(0.6, 1.0) + Vector((0, rng.uniform(0.0, 0.35), 0))).normalized()
        L = (Lm * (1 - t)) * rng.uniform(0.45, 0.8) + 0.12
        pts = polyline(base, d, L, 5, 0.18)
        tube(bm, uvl, pts, 0.0055, 0.002, 5, 0)
        branches.append(pts)
    for pts in branches:
        lens = [0.0]
        for k in range(1, len(pts)):
            lens.append(lens[-1] + (pts[k] - pts[k - 1]).length)
        total = lens[-1]
        sdist = total * 0.25
        while sdist < total:
            k = 1
            while k < len(pts) - 1 and lens[k] < sdist:
                k += 1
            t = (sdist - lens[k - 1]) / max(1e-6, lens[k] - lens[k - 1])
            p = pts[k - 1].lerp(pts[k], min(1.0, max(0.0, t)))
            mat = 1 if rng.random() < 0.72 else 2
            needle_tuft(bm, uvl, p, pts[k] - pts[k - 1], rng, mat, max(6, int(rng.randint(30, 40) * density)), rng.uniform(0.07, 0.10))
            sdist += 0.022 * rng.uniform(0.8, 1.2)
        needle_tuft(bm, uvl, pts[-1], pts[-1] - pts[-2], rng, 1, 26, 0.09)   # 끝눈 다발
    return new_obj(name, bm, ["M_PD_PineBark", "M_PD_PineNeedles", "M_PD_PineNeedlesDry"])


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = []
    for i, v in enumerate("ABC"):
        objs.append(spoil_mound(f"PD_Spoil_{v}", 3.0, 300 + i))
    for i in range(6):
        objs.append(twig(f"PD_Twig_{i}", 400 + i))
    for i, v in enumerate("AB"):
        objs.append(poncho(f"PD_Poncho_{v}", 500 + i))
    for i in range(6):
        objs.append(pine_bough(f"PD_Bough_{i}_LOD0", 600 + i))
        objs.append(pine_bough(f"PD_Bough_{i}_LOD1", 600 + i, 0.3))
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, "SM_PD_PositionProps.fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type="OFF")
    for o in objs:
        print(o.name, len(o.data.polygons), tuple(round(d, 3) for d in o.dimensions))


main()
