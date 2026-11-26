# 적송(소나무, Pinus densiflora) 다시 만들기 (2026-11-25, 사용자 지시 "나무가 맞지않으면 지금보다 실사에 가깝게 질감이나 느낌이 나게
# 만들고 지금은 대충 만든느낌이 있어서 그런거 고쳐놔"). 기존 Korean_Red_Pine 스캔은 잎이 덩어리 메시라 분재처럼 보이고 줄기가 뭉개졌다.
#  - 수피: 아래는 회갈색 두꺼운 거북등 조각 · 깊은 골, 위는 붉은 주황 얇게 벗겨지는 껍질 (반복 무늬, 노멀 포함, numpy 로 굽기)
#  - 솔잎: 솔잎 다발 달린 잔가지를 그려 알파 아틀라스(4 × 4)로 굽기 (Workbench, 투명 배경)
#  - 나무: 비스듬히 굽은 줄기, 아래 가지는 떨어져 나가고 위쪽 절반에 층층이 퍼진 가지, 가지 끝에 납작한 솔잎 덩어리(카드 묶음)
#  - LOD0 / LOD1 / LOD2 (Unity 가 _LOD 이름으로 LODGroup 을 만든다), 세 벌(A · B · C)
# 결과: five_river/Assets/_Project/Art/Maps/Dabudong_Env/RedPine20261125/
# 실행: blender -b --factory-startup -P make_red_pine.py  (여러 번 돌려도 결과 같음)
import math
import os
import random

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "five_river", "Assets", "_Project", "Art", "Maps", "Dabudong_Env", "RedPine20261125")
ATLAS_N = 4


def u2b(x, y, z):
    return (-x, -z, y)


def smooth01(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- 수피 텍스처
def periodic_noise(h, w, cutoff, seed):
    r = np.random.default_rng(seed).standard_normal((h, w))
    F = np.fft.fft2(r)
    fy = np.fft.fftfreq(h)[:, None]; fx = np.fft.fftfreq(w)[None, :]
    F *= np.exp(-(fx * fx + fy * fy) / (2 * cutoff * cutoff))
    n = np.real(np.fft.ifft2(F)); return (n - n.mean()) / (n.std() + 1e-9)


def voronoi_plates(h, w, cells_x, cells_y, seed, sy=0.45, warp=0.0):
    """반복되는 보로노이: 가장 가까운 점까지 거리 d1, 두 번째 d2 → 경계(골) = d2 - d1 작을수록. 세로로 길쭉한 조각."""
    rng = np.random.default_rng(seed)
    pts = rng.random((cells_x * cells_y, 2)) * [w, h]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    if warp > 0:
        # 좌표를 반복되는 노이즈로 비틀어 조각 가장자리를 불규칙하게 (곧은 보로노이 선은 돌길처럼 보였음)
        xx = (xx + warp * periodic_noise(h, w, 0.005, seed + 100)) % w
        yy = (yy + warp * 1.6 * periodic_noise(h, w, 0.005, seed + 101)) % h
    d1 = np.full((h, w), 1e9, np.float32); d2 = np.full((h, w), 1e9, np.float32); idx = np.zeros((h, w), np.int32)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px); dx = np.minimum(dx, w - dx)
        dy = np.abs(yy - py); dy = np.minimum(dy, h - dy)
        d = np.sqrt(dx * dx + (dy * sy) ** 2)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d))
        idx = np.where(closer, i, idx)
        d1 = np.where(closer, d, d1)
    return d1, d2, idx


def save_rgb(name, rgb, alpha=None):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=alpha is not None)
    a = alpha if alpha is not None else np.ones((h, w), np.float32)
    img.pixels.foreach_set(np.concatenate([np.clip(rgb, 0, 1), a[:, :, None]], axis=2).astype(np.float32).reshape(-1))
    img.filepath_raw = os.path.join(OUT, name + ".png"); img.file_format = "PNG"; img.save()


def height_to_normal(hgt, strength):
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * strength
    gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * strength
    n = np.stack([-gx, -gy, np.ones_like(hgt)], axis=2)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def bake_bark():
    S = 512
    # 아래: 굵은 거북등 조각, 깊은 골
    d1, d2, idx = voronoi_plates(S, S, 10, 4, 7, sy=0.24, warp=5.0)
    edge = np.clip((d2 - d1) / 22.0, 0, 1)
    # 판 위로 겹겹이 일어난 가로 결 (적송 아래 수피의 층)
    layers = np.sin((np.arange(S)[:, None] / S) * 2 * np.pi * 38 + 3 * periodic_noise(S, S, 0.02, 13)) * 0.5 + 0.5
    fine = periodic_noise(S, S, 0.06, 8); mid = periodic_noise(S, S, 0.015, 9)
    streak = periodic_noise(S, S // 8, 0.08, 10); streak = np.repeat(streak, 8, axis=1)   # 세로 결
    plate = np.clip((edge - 0.08) / 0.4, 0, 1); plate = plate * plate * (3 - 2 * plate)   # 넓은 골, 평평한 판 윗면
    hgt = plate * 0.8 + 0.06 * fine + 0.05 * streak + 0.05 * mid + 0.07 * layers * plate
    rng = np.random.default_rng(11); tone = rng.random(idx.max() + 1)[idx]
    base = np.array([0.33, 0.28, 0.24]); warm = np.array([0.44, 0.31, 0.22]); dark = np.array([0.08, 0.065, 0.055])
    col = base[None, None] * (0.85 + 0.3 * tone[:, :, None]) + (warm - base)[None, None] * np.clip(0.4 * mid + 0.2, 0, 1)[:, :, None] * 0.6
    col = col * (0.88 + 0.12 * fine[:, :, None]) * (0.9 + 0.12 * layers[:, :, None])
    col = col * plate[:, :, None] + dark[None, None] * (1 - plate[:, :, None])
    # 판 가장자리는 들떠 밝게 (빛을 받는 모서리)
    rim = np.clip(1 - np.abs(edge - 0.35) / 0.2, 0, 1)[:, :, None]
    col = col + rim * 0.06
    # 조각 위 회색 이끼 · 지의류 얼룩
    lich = np.clip(periodic_noise(S, S, 0.03, 12) - 1.0, 0, 1)[:, :, None]
    col = col * (1 - 0.35 * lich) + np.array([0.48, 0.47, 0.40]) * 0.35 * lich
    save_rgb("T_RedPine_BarkLow_BaseColor", col.astype(np.float32))
    save_rgb("T_RedPine_BarkLow_Normal", height_to_normal(hgt, 6.0).astype(np.float32))
    # 위: 붉은 주황 얇은 껍질 (작은 비늘 조각이 들뜸)
    d1, d2, idx = voronoi_plates(S, S, 16, 10, 21, sy=0.35, warp=3.0)
    edge = np.clip((d2 - d1) / 5.0, 0, 1)
    fine = periodic_noise(S, S, 0.08, 22); mid = periodic_noise(S, S, 0.02, 23)
    hgt = edge * 0.35 + 0.05 * fine
    tone = np.random.default_rng(24).random(idx.max() + 1)[idx]
    a = np.array([0.66, 0.35, 0.19]); b = np.array([0.78, 0.50, 0.30]); g = np.array([0.42, 0.33, 0.27])
    col = a[None, None] + (b - a)[None, None] * tone[:, :, None]
    col = col * (0.9 + 0.1 * fine[:, :, None])
    col = col * (0.65 + 0.35 * edge[:, :, None]) + (g - a)[None, None] * np.clip(-mid, 0, 1)[:, :, None] * 0.5
    save_rgb("T_RedPine_BarkHigh_BaseColor", col.astype(np.float32))
    save_rgb("T_RedPine_BarkHigh_Normal", height_to_normal(hgt, 4.0).astype(np.float32))
    print("bark baked")


# ---------------------------------------------------------------- 솔잎 아틀라스
def needle_atlas():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    bm = bmesh.new(); col = bm.loops.layers.color.new("Col")
    rng = random.Random(1925)

    def tri(a, b, c, colr):
        f = bm.faces.new([bm.verts.new(a), bm.verts.new(b), bm.verts.new(c)])
        for loop in f.loops:
            loop[col] = (*colr, 1.0)

    def twig(cx, cy, size, ang):
        # 잔가지 줄기 + 곁가지, 끝 60 % 에 솔잎 다발 (위에서 본 모습, 솔잎은 사방으로 퍼짐)
        d = Vector((math.cos(ang), math.sin(ang)))
        def branch(p0, dv, L, depth):
            pts = [p0]
            for k in range(6):
                dv = (dv + Vector((rng.uniform(-.15, .15), rng.uniform(-.15, .15)))).normalized()
                pts.append(pts[-1] + dv * L / 6)
            stem = (0.26, 0.17, 0.11)
            for k in range(6):
                a, b = pts[k], pts[k + 1]; nrm = Vector((-(b - a).y, (b - a).x)).normalized() * size * 0.006 * (1.2 - k / 6)
                tri((*(a + nrm), 0), (*(a - nrm), 0), (*b, 0), stem)
            for k in range(2, 7):
                p = pts[k] if k < len(pts) else pts[-1]
                for n in range(34 if depth == 0 else 26):
                    th = rng.uniform(0, 2 * math.pi)
                    fwd = dv * 0.45 + Vector((math.cos(th), math.sin(th)))
                    fwd.normalize()
                    L2 = size * rng.uniform(0.09, 0.14)
                    tip = p + fwd * L2
                    side = Vector((-fwd.y, fwd.x)) * size * 0.0035
                    g = rng.random()
                    c = (0.07 + 0.08 * g, 0.13 + 0.10 * g, 0.045 + 0.03 * g) if rng.random() > 0.08 else (0.32, 0.28, 0.12)
                    tri((*(p + side), 0.001 * n), (*(p - side), 0.001 * n), (*tip, 0.001 * n), c)
            if depth == 0:
                for k in (2, 3, 4):
                    s = 1 if k % 2 else -1
                    branch(pts[k], (dv + Vector((-dv.y, dv.x)) * s * rng.uniform(0.7, 1.1)).normalized(), L * rng.uniform(0.4, 0.6), 1)
        branch(Vector((cx, cy)) - d * size * 0.42, d, size * 0.8, 0)

    for cell in range(ATLAS_N * ATLAS_N):
        cx, cy = cell % ATLAS_N + 0.5, cell // ATLAS_N + 0.5
        twig(cx, cy, 0.95, math.radians(90 + rng.uniform(-25, 25)))
        twig(cx + rng.uniform(-.12, .12), cy + rng.uniform(-.05, .1), 0.8, math.radians(90 + rng.uniform(-60, 60)))
    me = bpy.data.meshes.new("atlas"); bm.to_mesh(me); bm.free()
    me.color_attributes.active_color = me.color_attributes[0]; me.color_attributes.render_color_index = 0
    ob = bpy.data.objects.new("atlas", me); sc.collection.objects.link(ob)
    cam = bpy.data.cameras.new("cam"); cam.type = "ORTHO"; cam.ortho_scale = ATLAS_N
    co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co
    co.location = (ATLAS_N / 2, ATLAS_N / 2, 5)
    sc.render.resolution_x = sc.render.resolution_y = 2048
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading; sh.light = "FLAT"; sh.color_type = "VERTEX"
    sc.render.film_transparent = True; sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = os.path.join(OUT, "T_RedPine_Needles_BaseColor.png")
    bpy.ops.render.render(write_still=True)
    print("needle atlas")


# ---------------------------------------------------------------- 나무
class Builder:
    def __init__(self, name):
        self.bm = bmesh.new(); self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.normals = []   # per face loop normals (Unity space) or None
        self.name = name

    def tube(self, pts, radii, sides, mat_of_t, tile_u=0.9, ragged=None):
        rings = []
        length = 0.0
        for k, p in enumerate(pts):
            if k:
                length += (p - pts[k - 1]).length
            fwd = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
            side = fwd.cross(Vector((0, 0, 1)) if abs(fwd.z) < .95 else Vector((1, 0, 0))).normalized()
            up = side.cross(fwd).normalized()
            ring = []
            for a in range(sides + 1):
                th = 2 * math.pi * a / sides
                v = p + (side * math.cos(th) + up * math.sin(th)) * radii[k]
                ring.append((self.bm.verts.new(u2b(*v)), (a / sides * max(1, round(2 * math.pi * radii[k] / tile_u + .5)), length / tile_u)))
            rings.append((ring, k / (len(pts) - 1)))
        for k in range(len(rings) - 1):
            for a in range(sides):
                mat = mat_of_t(rings[k][1] + (ragged(a) if ragged else 0.0))
                q = [rings[k][0][a], rings[k + 1][0][a], rings[k + 1][0][a + 1], rings[k][0][a + 1]]
                f = self.bm.faces.new([x[0] for x in q]); f.material_index = mat; f.smooth = True
                for loop, x in zip(f.loops, q):
                    loop[self.uv].uv = x[1]
                self.normals.append(None)

    def card(self, center, normal, tangent, size, cell, out_dir):
        """솔잎 카드: 사각형 하나, 아틀라스 셀 하나. 노멀은 덩어리 바깥 + 위 (부드러운 빛)."""
        bt = normal.cross(tangent).normalized()
        hs = size * 0.5
        cx, cy = cell % ATLAS_N, cell // ATLAS_N
        u0, u1, v0, v1 = cx / ATLAS_N, (cx + 1) / ATLAS_N, cy / ATLAS_N, (cy + 1) / ATLAS_N
        corners = [(-1, -1, u0, v0), (1, -1, u1, v0), (1, 1, u1, v1), (-1, 1, u0, v1)]
        vs = []
        for sx, sy, u, v in corners:
            p = center + tangent * hs * sx + bt * hs * sy
            vs.append((self.bm.verts.new(u2b(*p)), (u, v)))
        f = self.bm.faces.new([x[0] for x in vs]); f.material_index = 2; f.smooth = True
        for loop, x in zip(f.loops, vs):
            loop[self.uv].uv = x[1]
        n = (out_dir * 0.7 + Vector((0, 1, 0)) * 0.6).normalized()
        self.normals.append([n] * 4)

    def finish(self, mats):
        me = bpy.data.meshes.new(self.name); self.bm.to_mesh(me); self.bm.free()
        loops = []
        for poly, fn in zip(me.polygons, self.normals):
            if fn is None:
                loops.extend([None] * poly.loop_total)
            else:
                loops.extend([Vector(u2b(n.x, n.y, n.z)) for n in fn])
        # 줄기는 Blender 가 계산한 부드러운 노멀 그대로, 카드는 지정
        me.update()
        auto = [l.normal.copy() for l in me.loops] if hasattr(me.loops[0], "normal") else None
        custom = [loops[i] if loops[i] is not None else (auto[i] if auto else Vector((0, 0, 1))) for i in range(len(me.loops))]
        me.normals_split_custom_set(custom)
        ob = bpy.data.objects.new(self.name, me); bpy.context.scene.collection.objects.link(ob)
        for m in mats:
            me.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
        return ob


def make_tree(tag, seed):
    rng = random.Random(seed)
    H = rng.uniform(7.5, 10.5)
    lean = Vector((rng.uniform(-1, 1), 0, rng.uniform(-1, 1))).normalized() * rng.uniform(0.6, 1.6)
    # 줄기 중심선 (Unity y 위): 밑동에서 비스듬히, 중간에서 한 번 꺾임(적송의 굽은 줄기)
    trunk = []
    kink = rng.uniform(0.35, 0.6)
    for k in range(25):
        t = k / 24
        off = lean * (t ** 1.3) + Vector((math.sin(t * 5.1 + seed) * 0.12, 0, math.cos(t * 4.3 + seed) * 0.12))
        off += Vector((-lean.z, 0, lean.x)) * 0.35 * smooth01((t - kink) / 0.3)
        trunk.append(Vector((off.x, H * t, off.z)))
    r0 = rng.uniform(0.17, 0.24)
    radii = [r0 * (1 - 0.85 * t) ** 1.1 + (0.06 * max(0, 0.06 - t) / 0.06) for t in (k / 24 for k in range(25))]
    # 가지 · 솔잎 덩어리 설계 (LOD 마다 같은 모양, 세밀도만 다르게)
    branches = []
    nb = rng.randint(11, 15)
    for b in range(nb):
        t = 0.48 + 0.5 * (b / (nb - 1)) ** 0.9 + rng.uniform(-0.02, 0.02)
        k = min(23, int(t * 24))
        base = trunk[k].lerp(trunk[k + 1], t * 24 - k)
        yaw = b * 2.39996 + rng.uniform(-0.3, 0.3)      # 황금각으로 돌아가며
        out = Vector((math.cos(yaw), 0, math.sin(yaw)))
        L = (3.2 * (1 - t) + 1.2) * rng.uniform(0.75, 1.15)
        rise = rng.uniform(0.15, 0.45)
        pts = []
        for s in range(7):
            u = s / 6
            p = base + out * L * u + Vector((0, L * (rise * u - 0.18 * u * u + 0.22 * u ** 3), 0))
            pts.append(p)
        branches.append((pts, 0.035 * (1 - t) + 0.016, out, L))
    pads = []   # (중심, 바깥 방향, 크기)
    for pts, r, out, L in branches:
        tip = pts[-1]
        pads.append((tip + Vector((0, 0.25, 0)), out, rng.uniform(1.3, 1.9)))
        for j in range(rng.randint(3, 5)):
            u = rng.uniform(0.35, 0.9)
            p = pts[int(u * 6)]
            side = Vector((-out.z, 0, out.x)) * rng.choice((-1, 1))
            pads.append((p + side * rng.uniform(0.3, 0.8) + Vector((0, rng.uniform(0.15, 0.4), 0)), (out + side).normalized(), rng.uniform(0.9, 1.4)))
    pads.append((trunk[-1] + Vector((0, 0.2, 0)), Vector((0, 1, 0)), 1.3))
    objs = []
    for lod, (sides, cards_per_pad, card_scale) in enumerate([(10, 12, 1.0), (7, 5, 1.3), (5, 2, 1.8)]):
        bld = Builder(f"SM_RedPine_{tag}_LOD{lod}")
        jag = [rng.uniform(-0.07, 0.07) + 0.04 * math.sin(a * 1.7) for a in range(sides)]
        bld.tube(trunk, radii, sides, lambda t: 0 if t < 0.40 else 1, 0.9, ragged=lambda a: jag[a])
        if lod < 2:
            for pts, r, out, L in branches:
                bld.tube(pts, [r * (1 - 0.8 * s / 6) + 0.01 for s in range(7)], max(4, sides - 4), lambda t: 1, 0.6)
        prng = random.Random(seed * 7 + lod)
        for c, out, size in pads:
            for i in range(cards_per_pad):
                # 납작한 덩어리: 대부분 수평에 가까운 카드, 일부는 비스듬히
                tilt = math.radians(prng.uniform(0, 35) if i < max(1, cards_per_pad - 2) else prng.uniform(45, 80))
                yaw = prng.uniform(0, 2 * math.pi)
                nrm = Vector((math.sin(tilt) * math.cos(yaw), math.cos(tilt), math.sin(tilt) * math.sin(yaw)))
                tan = nrm.cross(Vector((math.cos(yaw + 1.3), 0.1, math.sin(yaw + 1.3)))).normalized()
                jitter = Vector((prng.uniform(-.35, .35), prng.uniform(-.12, .2), prng.uniform(-.35, .35))) * size
                bld.card(c + jitter, nrm, tan, size * card_scale * prng.uniform(0.8, 1.15), prng.randrange(ATLAS_N * ATLAS_N), (out + jitter * 0.5).normalized())
        objs.append(bld.finish(["M_RedPine_BarkLow", "M_RedPine_BarkHigh", "M_RedPine_Needles"]))
    return objs, H


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bake_bark()
    needle_atlas()
    for i, tag in enumerate("ABC"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs, H = make_tree(tag, 1950 + i * 17)
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"SM_RedPine_{tag}.fbx"), use_selection=True, object_types={"MESH"},
                                 apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                                 add_leaf_bones=False, mesh_smooth_type="OFF")
        print("tree", tag, round(H, 2), [len(o.data.polygons) for o in objs])


main()
