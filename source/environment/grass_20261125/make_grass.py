# 기존 풀(ENV_grass_clump / _dry / _tuft) 다시 만들기 (2026-11-25, 사용자 지시 "풀은가져오지말고 느낌만 빼와서 기존에 있는거에 이식해")
# URP 3D 샘플 오아시스 풀의 "느낌"만 옮긴다: 끝이 가늘게 휘는 풀잎, 가운데 잎맥, 뿌리 → 끝 색 변화(어두운 초록 → 누런 끝),
# 마른 잎 섞임, 이삭 달린 줄기, 풀잎 여러 장이 그려진 알파 카드를 방사형으로 꽂은 포기. 템플릿 파일은 하나도 쓰지 않는다.
# 결과 (같은 경로에 덮어씀, .meta 유지):
#   five_river/Assets/_Project/Art/Maps/Dabudong_Env/Models/ENV_grass_clump.fbx · ENV_grass_clump_dry.fbx · ENV_grass_tuft.fbx
#   five_river/Assets/_Project/Art/Maps/Dabudong_Env/Textures/T_grass_wild_0_BaseColor.png (새 아틀라스, 알파 포함)
# 실행: blender -b --factory-startup -P make_grass.py   (여러 번 돌려도 결과 같음)
import math
import os
import random

import bpy
import bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ENV = os.path.join(REPO, "five_river", "Assets", "_Project", "Art", "Maps", "Dabudong_Env")
ATLAS = os.path.join(ENV, "Textures", "T_grass_wild_0_BaseColor.png")
COLS, ROWS = 4, 2          # 셀 4 × 2 (각 256 × 512 px): 0~5 풀잎 다발, 6~7 이삭 줄기
CELL_W, CELL_H = 1.0, 2.0  # 아틀라스를 그릴 때의 셀 크기(임의 단위)


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(len(a)))


GREEN = [(0.10, 0.15, 0.05), (0.27, 0.34, 0.11), (0.50, 0.50, 0.24)]      # 뿌리, 가운데, 끝
OLIVE = [(0.14, 0.16, 0.06), (0.36, 0.37, 0.15), (0.62, 0.56, 0.32)]
DRY = [(0.20, 0.17, 0.09), (0.50, 0.43, 0.26), (0.74, 0.65, 0.44)]


def grad(pal, t):
    return lerp(pal[0], pal[1], t / 0.55) if t < 0.55 else lerp(pal[1], pal[2], (t - 0.55) / 0.45)


def blade(bm, col, x0, y0, height, width, bend, lean, pal, rng, steps=14):
    """풀잎 하나: 뿌리(x0,y0)에서 위로, 끝으로 가늘어지며 한쪽으로 휨. 단면 3점(가장자리 · 잎맥 · 가장자리)."""
    rows = []
    for k in range(steps + 1):
        t = k / steps
        x = x0 + lean * t * height + bend * (t ** 2) * height
        y = y0 + height * (t - 0.12 * bend * bend * t * t)
        dx = lean + 2 * bend * t
        n = Vector((1.0, -dx)).normalized() if abs(dx) < 50 else Vector((1, 0))
        w = width * (1 - t ** 1.7) * (0.75 + 0.25 * math.sin(t * math.pi * 0.9 + 0.3))
        c = grad(pal, t)
        jit = rng.uniform(-0.03, 0.03)
        edge = tuple(max(0, v * 0.92 + jit) for v in c)
        mid = tuple(min(1, v * 1.12 + 0.02 + jit) for v in c)
        L = bm.verts.new((x - n.x * w, y - n.y * w, 0)); M = bm.verts.new((x, y, 0.001)); R = bm.verts.new((x + n.x * w, y + n.y * w, 0))
        rows.append(((L, edge), (M, mid), (R, edge)))
    for k in range(steps):
        for a in range(2):
            q = [rows[k][a], rows[k][a + 1], rows[k + 1][a + 1], rows[k + 1][a]]
            f = bm.faces.new([v[0] for v in q])
            for loop, v in zip(f.loops, q):
                loop[col] = (*v[1], 1.0)


def seed_stalk(bm, col, x0, y0, height, rng):
    """이삭 줄기: 가는 줄기 끝 40 % 에 짧은 가지와 길쭉한 이삭(작은 타원)들."""
    blade(bm, col, x0, y0, height, 0.012, rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), DRY, rng, 10)
    for i in range(46):
        t = 0.58 + 0.42 * (i / 46) ** 0.8
        cx = x0 + (rng.uniform(-0.05, 0.05)) * t
        cy = y0 + height * t
        side = 1 if i % 2 else -1
        ln = rng.uniform(0.04, 0.12) * (1.1 - t)
        ex, ey = cx + side * ln, cy + ln * 0.7
        # 짧은 가지
        blade(bm, col, cx, cy, ln * 1.1, 0.004, 0.0, side * 0.9, DRY, rng, 3)
        # 이삭 낟알
        c = lerp(DRY[1], DRY[2], rng.uniform(0.3, 1.0))
        r = rng.uniform(0.010, 0.016)
        ring = []
        for a in range(8):
            th = 2 * math.pi * a / 8
            ring.append(bm.verts.new((ex + math.cos(th) * r * 0.55, ey + math.sin(th) * r * 1.6, 0.002)))
        f = bm.faces.new(ring)
        for loop in f.loops:
            loop[col] = (*c, 1.0)


def make_atlas():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    bm = bmesh.new()
    col = bm.loops.layers.color.new("Col")
    rng = random.Random(1950)
    for cell in range(COLS * ROWS):
        cx, cy = cell % COLS, cell // COLS
        ox, oy = cx * CELL_W, cy * CELL_H
        if cell < 6:
            pal_for = [GREEN, GREEN, OLIVE, OLIVE, DRY, DRY][cell]
            n = rng.randint(12, 16)
            for b in range(n):
                pal = pal_for if rng.random() < 0.75 else (DRY if pal_for is not DRY else OLIVE)
                h = CELL_H * rng.uniform(0.55, 0.96)
                lean = rng.uniform(-0.25, 0.25)
                bend = rng.uniform(-0.35, 0.35) + (0.25 if cell in (1, 5) else 0) * (1 if lean > 0 else -1)
                x0 = ox + CELL_W * 0.5 + rng.uniform(-0.12, 0.12)
                blade(bm, col, x0, oy + 0.01, h * (0.85 if abs(bend) > 0.3 else 1.0), rng.uniform(0.010, 0.020), bend * 0.5, lean * 0.6, pal, rng)
        else:
            for s in range(2):
                seed_stalk(bm, col, ox + CELL_W * (0.38 + 0.24 * s), oy + 0.01, CELL_H * rng.uniform(0.86, 0.96), rng)
            for b in range(3):
                blade(bm, col, ox + CELL_W * 0.5 + rng.uniform(-.1, .1), oy + 0.01, CELL_H * rng.uniform(0.3, 0.5), 0.028, rng.uniform(-.3, .3), rng.uniform(-.3, .3), OLIVE, rng)
    me = bpy.data.meshes.new("atlas"); bm.to_mesh(me); bm.free()
    ca = me.color_attributes[0]
    me.color_attributes.active_color = ca
    me.color_attributes.render_color_index = 0
    ob = bpy.data.objects.new("atlas", me); sc.collection.objects.link(ob)
    # 셀 밖으로 나간 것은 셀 안에서 잘리지 않게: 그림은 셀 경계 안에 그렸음(풀잎 기울기 제한)
    cam = bpy.data.cameras.new("cam"); cam.type = "ORTHO"; cam.ortho_scale = COLS * CELL_W
    co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co
    co.location = (COLS * CELL_W / 2, ROWS * CELL_H / 2, 5)
    sc.render.resolution_x, sc.render.resolution_y = 1024, 1024
    cam.sensor_fit = "VERTICAL"; cam.ortho_scale = ROWS * CELL_H
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light = "FLAT"; sh.color_type = "VERTEX"
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = ATLAS
    bpy.ops.render.render(write_still=True)
    print("atlas", ATLAS)


def u2b(x, y, z):
    return (-x, -z, y)


def clump(name, mat_name, seed, cards, height, spread, cells, seed_cards=0):
    """풀 포기: 알파 카드(셀 하나 = 카드 하나)를 방사형으로, 위로 갈수록 바깥으로 휘게. 노멀은 위 · 바깥(부드러운 빛)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    normals = []
    faces_normals = []

    def card(cell, h, w, yaw, lean):
        cx, cy = cell % COLS, cell // COLS
        u0, u1 = cx / COLS, (cx + 1) / COLS
        v0, v1 = cy / ROWS, (cy + 1) / ROWS
        d = Vector((math.cos(yaw), 0, math.sin(yaw)))        # 기울어지는 방향(바깥)
        side = Vector((-d.z, 0, d.x))
        segs = 4
        rows = []
        for k in range(segs + 1):
            t = k / segs
            out = d * (lean * h * t * t)                        # 위로 갈수록 바깥으로 휨
            base = Vector((0, h * t * (1 - 0.15 * lean * t), 0)) + out + d * rng.uniform(0, spread) * 0.2
            row = []
            for s in (-1, 1):
                p = base + side * (w * 0.5 * s)
                nrm = (Vector((0, 1, 0)) * 1.0 + d * 0.6 + side * 0.15 * s).normalized()
                v = bm.verts.new(u2b(p.x, p.y, p.z))
                row.append((v, ((u0 if s < 0 else u1), v0 + (v1 - v0) * t), nrm))
            rows.append(row)
        for k in range(segs):
            q = [rows[k][0], rows[k][1], rows[k + 1][1], rows[k + 1][0]]
            f = bm.faces.new([x[0] for x in q])
            for loop, x in zip(f.loops, q):
                loop[uvl].uv = x[1]
            faces_normals.append([x[2] for x in q])

    for i in range(cards):
        yaw = 2 * math.pi * i / cards + rng.uniform(-0.3, 0.3)
        card(rng.choice(cells), height * rng.uniform(0.75, 1.1), height * rng.uniform(0.5, 0.7), yaw, rng.uniform(0.35, 0.85))
    for i in range(seed_cards):
        card(rng.choice((6, 7)), height * rng.uniform(1.3, 1.6), height * 0.55, rng.uniform(0, 2 * math.pi), rng.uniform(0.05, 0.2))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    # Blender 노멀 = u2b 로 바꾼 방향
    loop_normals = []
    for poly, fn in zip(me.polygons, faces_normals):
        for n in fn:
            loop_normals.append(Vector(u2b(n.x, n.y, n.z)))
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set(loop_normals)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name))
    return ob


def export(ob, path):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type="OFF")


def main():
    make_atlas()
    specs = [
        ("ENV_grass_clump", "X_grass_clump", "M_ENV_grass_clump.001", 14, 0.42, 0.25, (0, 1, 2, 3, 4), 0),
        ("ENV_grass_clump_dry", "X_grass_clump_dry", "M_ENV_grass_clump_dry.001", 13, 0.40, 0.25, (4, 5, 3, 5), 0),
        ("ENV_grass_tuft", "X_grass_tuft", "M_Grass_Aug.001", 17, 0.50, 0.35, (0, 1, 2, 3, 4, 5), 3),
    ]
    for fbx, obj, mat, cards, h, spread, cells, seeds in specs:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        ob = clump(obj, mat, hash(obj) % 10000 if False else sum(map(ord, obj)), cards, h, spread, cells, seeds)
        export(ob, os.path.join(ENV, "Models", fbx + ".fbx"))
        print("grass", fbx, len(ob.data.polygons), tuple(round(d, 3) for d in ob.dimensions))


main()
