# 참호 모래주머니 줄 다시 만들기 (2026-11-25, 사용자 지시 "모래주머니 실사감이 느껴지게 각져있지않게 다시 다듬어서 리얼리티 느낌을주게 끔해")
#
# 이전: ENV_sandbag_row.fbx (AI 생성 3 m 덩어리 한 장, 삼각형 3000, 60 cm 넘는 마대 두 단, 끝이 종이 조각처럼 깨짐,
#       같은 모양 반복, 뒤 흉장은 반으로 눌러 씀).
# 이번: 마대를 하나씩 만든다 — FM 5-15 채운 마대 4¾ × 10 × 19 in (약 12 × 25 × 48 cm). 둥글게 부푼 몸통, 꿰맨 끝은
#       납작하게 귀(모서리)가 튀어나오고, 묶은 끝은 오므라듦, 아래 · 위는 무게로 눌려 납작, 잔주름 · 울퉁불퉁.
#       쌓기는 길이쌓기(stretcher) / 마구리쌓기(header)를 번갈아 4단, 한 단마다 2.5 cm 뒤로(옹벽 1:4 기울기),
#       마대마다 크기 · 기울기 · 색(두 재질)을 다르게. 3 m 줄 3벌, 1.5 m 줄 3벌, 각각 LOD0 / LOD1.
#       텍스처: 프로젝트의 Poly Haven hessian_230(삼베) 에 brown_mud_02 흙 얼룩을 섞어 반복 무늬 그대로 새로 굽는다.
#
# 실행 (백그라운드, 여러 번 돌려도 결과 같음):
#   blender -b --factory-startup -P make_sandbag_rows.py
# 결과: five_river/Assets/_Project/Art/MapPolish20261105/TrenchDressing/Sandbags20261125/
import math
import os
import random

import bpy
import bmesh
import numpy as np
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ART = os.path.join(REPO, "five_river", "Assets", "_Project", "Art")
OUT = os.path.join(ART, "MapPolish20261105", "TrenchDressing", "Sandbags20261125")
SCAN = os.path.join(ART, "Maps", "ClaudePolish20261030", "Scan")

ROW_HIGH, ROW_DEPTH = 0.47, 0.67     # 기존 줄과 같은 자리에 놓이도록 (TrenchDressing.cs: 윗면 = 흉장 꼭대기 + 3 cm)
TILE = 0.30                           # 텍스처 한 장 = 30 cm (삼베 올 약 3.4가닥/cm)


def u2b(x, y, z):
    # Unity 좌표 → Blender (tools/make_graybox_fbx.py 와 같은 규칙, FBX -Z forward / Y up)
    return (-x, -z, y)


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def soft_max(x, lim, k):
    # max(x, lim) 의 매끈한 형태 (눌린 면과 부푼 면 사이에 날카로운 선이 생기지 않게)
    return lim + k * math.log1p(math.exp((x - lim) / k)) if (x - lim) / k < 30 else x


def bag_mesh(bm, uvl, mat_index, L, W, T, frame, rng, nu, nv, loaded, seed, sewn_dir=0):
    """마대 하나. 로컬: a = 길이, b = 폭, c = 두께(위). frame(a,b,c 로컬 점) → Unity 월드 점."""
    e = 0.74                    # 단면 둥근 정도(1 = 타원, 작을수록 각진 베개)
    sewn = 1 if rng.random() < 0.5 else -1   # 꿰맨(납작한) 끝이 어느 쪽인지
    if sewn_dir:
        sewn = sewn_dir                       # 마구리쌓기: 꿰맨 끝은 흉장 안쪽, 묶은 끝이 참호 쪽으로 보임
    sag = 0.006 * (1.0 if rng.random() < 0.5 else 0.6)
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    rings = []
    for i in range(nu + 1):
        t = -1 + 2 * i / nu
        u = math.sin(t * math.pi / 2)          # 끝으로 갈수록 고리를 촘촘히(둥근 끝)
        au = abs(u)
        is_sewn_end = (u * sewn) > 0
        # 길이 방향 윤곽: 가운데 부풀고 끝 8~10 cm에서 둥글게 줄어듦 (상자 면 없이)
        # 끝 약 12 cm 를 4분원으로 둥글게 (짧으면 끝이 납작한 상자 면으로 보임)
        r = smooth01((au - 0.5) / 0.5)
        thick = math.sqrt(max(0.0, 1 - r * r))
        if is_sewn_end:
            width = 1.0 - 0.10 * au ** 5          # 꿰맨 끝: 넓게, 두께만 납작해져 솔기 선
        else:
            width = 1.0 - 0.38 * smooth01((au - 0.5) / 0.5)   # 묶은 끝: 오므라듦
            thick = max(thick, 0.18 * (1 - au))
        thick = max(thick, 0.02)
        ring = []
        arc = 0.0
        prev = None
        for j in range(nv + 1):
            th = -math.pi / 2 + 2 * math.pi * j / nv         # 이음매는 바닥(안 보임)
            cb = math.copysign(abs(math.cos(th)) ** e, math.cos(th))
            cc = math.copysign(abs(math.sin(th)) ** e, math.sin(th))
            # 무게: 아래는 눌려 납작, 위에 마대가 얹히면 위도 납작
            cc = soft_max(cc, -0.80, 0.07)
            if loaded:
                cc = -soft_max(-cc, -0.84, 0.07)
            a = L * 0.5 * u
            # 꿰맨 끝 모서리 귀: 폭 끝 두 모서리가 길이 방향으로 튀어나옴
            if is_sewn_end and au > 0.7:
                ear = smooth01((abs(cb) - 0.75) / 0.25) * smooth01((au - 0.7) / 0.3)
                a += math.copysign(1, u) * 0.022 * ear
            b = W * 0.5 * width * cb * (1 + 0.04 * (1 - au * au))   # 옆구리 살짝 부풂
            c = T * 0.5 * thick * cc
            # 결 따라 처짐(아랫단 위에 얹혀 살짝 휨)
            c += -sag * (1 - u * u)
            p = Vector((a, b, c))
            # 겉면 울퉁불퉁 · 주름: 몸통 중심에서 바깥 방향으로 밀기
            n = Vector((0.0, b, c))
            n = n.normalized() if n.length > 1e-6 else Vector((math.copysign(1, u), 0, 0))
            if au > 0.85:
                n = (n + Vector((math.copysign(1, u), 0, 0)) * (au - 0.85) * 6).normalized()
            q = p * 1.0 + off
            d = 0.0075 * noise.noise(q * 5.0) + 0.0022 * noise.noise(q * 19.0)
            # 끝으로 모이는 잔주름(원주 방향으로 촘촘)
            endw = smooth01((au - 0.45) / 0.55)
            d += 0.0038 * endw * noise.noise(Vector((th * 2.2, u * 1.3, seed)) * 2.0)
            p = p + n * d
            ring.append(p)
            if prev is not None:
                arc += (p - prev).length
            prev = p
        rings.append((ring, u))
    # 정점 · 면
    # UV: 길이 방향도 겉면을 따라 잰 길이 (둥근 끝에서 무늬가 늘어나지 않게)
    along = [[0.0] * (nv + 1)]
    for i in range(1, len(rings)):
        along.append([along[i - 1][j] + (rings[i][0][j] - rings[i - 1][0][j]).length for j in range(nv + 1)])
    mid = len(rings) // 2
    verts = []
    for i, (ring, u) in enumerate(rings):
        row = []
        arc = 0.0
        for j, p in enumerate(ring):
            if j > 0:
                arc += (p - ring[j - 1]).length
            v = bm.verts.new(u2b(*frame(p)))
            row.append((v, ((along[i][j] - along[mid][j]) / TILE, arc / TILE)))
        verts.append(row)
    uo = (random.Random(seed).random(), random.Random(seed + 7).random())
    for i in range(nu):
        for j in range(nv):
            q = [verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]]
            try:
                f = bm.faces.new([x[0] for x in q])
            except ValueError:
                continue
            f.material_index = mat_index
            f.smooth = True
            for loop, x in zip(f.loops, q):
                loop[uvl].uv = (x[1][0] + uo[0], x[1][1] + uo[1])
    # 끝 마개(아주 작은 고리 → 가운데 한 점)
    for i, sign in ((0, -1), (nu, 1)):
        ring = verts[i]
        cen = sum((rings[i][0][j] for j in range(nv)), Vector()) / nv
        cen += Vector((sign * 0.006, 0, 0))
        cv = bm.verts.new(u2b(*frame(cen)))
        for j in range(nv):
            a, b = ring[j][0], ring[j + 1][0]
            try:
                f = bm.faces.new([b, a, cv] if sign > 0 else [a, b, cv])
            except ValueError:
                continue
            f.material_index = mat_index
            f.smooth = True
            for loop, lp in zip(f.loops, ([rings[i][0][j + 1], rings[i][0][j], cen] if sign > 0 else [rings[i][0][j], rings[i][0][j + 1], cen])):
                loop[uvl].uv = (uo[0] + lp.y / TILE, uo[1] + lp.z / TILE)   # 끝 면은 폭 · 두께로 평면 투영


def build_row(name, length, seed, lod):
    rng = random.Random(seed)
    nu, nv = (14, 18) if lod == 0 else (7, 9)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    # 4단: 길이쌓기 / 마구리쌓기 번갈아, 단 두께 합 = 0.47 (마대끼리 1 cm씩 눌려 맞물림)
    # 3단(마구리 / 길이 / 마구리): 4단으로 얇게 누르면 참호를 따라 볼 때 판을 겹친 것처럼 읽혔다(Unity 1차 확인)
    pattern = ["H", "S", "H"]
    ts = [rng.uniform(0.17, 0.185) for _ in pattern]
    press = 0.036                     # 위아래 마대가 서로 눌려 맞닿음(틈으로 뒤가 비치지 않게)
    sink = 0.03                       # 아랫단은 팔꿈치 받침 흙에 묻힘
    k = (ROW_HIGH + sink + press * (len(ts) - 1)) / sum(ts)
    ts = [t * k for t in ts]
    y = -sink
    front0 = ROW_DEPTH * 0.5 + 0.03     # 아랫단 둥근 끝이 흙벽 덮개 윗 모서리를 가리도록
    half = length * 0.5
    stagger = rng.random()
    for ci, (kind, T) in enumerate(zip(pattern, ts)):
        loaded = ci < len(pattern) - 1
        face = front0 - ci * 0.018           # 옹벽 기울기 (단마다 뒤로)
        yc = y + T * 0.5
        if kind == "S":
            Lb = 0.48
            n = max(1, round(length / (Lb * 0.97)))
            step = length / n
            shift = (0.5 if ci % 4 == 2 else 0.0) * step + (stagger - 0.5) * 0.06
            xs = [-half + step * (i + 0.5) + shift for i in range(n + 1)]
            for x in xs:
                if x - step * 0.5 > half - 0.05 or x + step * 0.5 < -half + 0.05:
                    continue
                L = step * rng.uniform(1.0, 1.05)
                W = rng.uniform(0.24, 0.28)
                zc = face - W * 0.5 + rng.uniform(-0.012, 0.012)
                yaw = math.radians(rng.uniform(-3.5, 3.5)); roll = math.radians(rng.uniform(-3, 3)); pitch = math.radians(rng.uniform(-1.2, 1.2))
                xc = max(-half + L * 0.45, min(half - L * 0.45, x))
                frame = make_frame((xc, yc + rng.uniform(-0.005, 0.005), zc), yaw, roll, pitch, along_x=True)
                mi = 0 if rng.random() < 0.7 else 1
                bag_mesh(bm, uvl, mi, L, W, T * rng.uniform(1.0, 1.08), frame, rng, nu, nv, loaded, rng.randint(0, 9999))
        else:
            Wb = 0.26
            n = max(1, round(length / (Wb * 0.96)))
            step = length / n
            for i in range(n):
                x = -half + step * (i + 0.5) + rng.uniform(-0.01, 0.01)
                W = step * rng.uniform(1.02, 1.08)
                L = rng.uniform(0.46, 0.52)
                zc = face - L * 0.5 + rng.uniform(-0.015, 0.015)
                yaw = math.radians(rng.uniform(-4, 4)); roll = math.radians(rng.uniform(-2.5, 2.5)); pitch = math.radians(rng.uniform(-1.5, 1.5))
                frame = make_frame((x, yc + rng.uniform(-0.005, 0.005), zc), yaw, roll, pitch, along_x=False)
                mi = 0 if rng.random() < 0.7 else 1
                bag_mesh(bm, uvl, mi, L, W, T * rng.uniform(1.0, 1.08), frame, rng, nu, nv, loaded, rng.randint(0, 9999), sewn_dir=-1)
        y += T - press
    # 면 방향을 바깥쪽으로 통일 (끝 마개가 뒤집혀 Unity 에서 뒷면으로 잘리면 마대 안쪽이 비쳐 보였음)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for m in ("M_TD_SandbagBurlap_A", "M_TD_SandbagBurlap_B"):
        mat = bpy.data.materials.get(m) or bpy.data.materials.new(m)
        me.materials.append(mat)
    return ob, len(me.polygons)


def make_frame(center, yaw, roll, pitch, along_x):
    """마대 로컬(a 길이, b 폭, c 위) → Unity 줄 로컬. along_x: 길이가 줄 방향(x), 아니면 깊이 방향(z)."""
    cy, sy = math.cos(yaw), math.sin(yaw)

    def f(p):
        a, b, c = p.x, p.y, p.z
        # 작은 굴림 · 숙임
        b, c = b * math.cos(roll) - c * math.sin(roll), b * math.sin(roll) + c * math.cos(roll)
        a, c = a * math.cos(pitch) - c * math.sin(pitch), a * math.sin(pitch) + c * math.cos(pitch)
        if along_x:
            x, z = a, b
        else:
            x, z = b, a
        x, z = x * cy - z * sy, x * sy + z * cy
        return (center[0] + x, center[1] + c, center[2] + z)
    return f


def periodic_noise(h, w, cutoff, seed):
    r = np.random.default_rng(seed).standard_normal((h, w))
    F = np.fft.fft2(r)
    fy = np.fft.fftfreq(h)[:, None]; fx = np.fft.fftfreq(w)[None, :]
    F *= np.exp(-(fx * fx + fy * fy) / (2 * cutoff * cutoff))
    n = np.real(np.fft.ifft2(F))
    return (n - n.mean()) / (n.std() + 1e-9)


def load_rgb(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)[:, :, :3].copy(), w, h


def bake_texture():
    hes, w, h = load_rgb(os.path.join(SCAN, "hessian_230_diff_2k.jpg"))
    mud, mw, mh = load_rgb(os.path.join(SCAN, "brown_mud_02_diff_2k.jpg"))
    # 흙 텍스처를 삼베 크기로 (반복이 끊기지 않게 칸 번호를 비율로)
    yi = (np.arange(h) * mh // h)[:, None]; xi = (np.arange(w) * mw // w)[None, :]
    mud = mud[yi, xi]
    # 오래 쓴 마대: 해에 바래고 흙물이 든 색 (새 삼베보다 회갈색, 덜 노랗게)
    lum = hes.mean(axis=2, keepdims=True)
    aged = hes * 0.55 + lum * 0.45
    aged = aged * np.array([0.92, 0.86, 0.76], dtype=np.float32)
    big = periodic_noise(h, w, 0.004, 11)     # 큰 얼룩
    mid = periodic_noise(h, w, 0.02, 12)      # 작은 얼룩
    m = np.clip(0.38 + 0.16 * big + 0.07 * mid, 0, 1)[:, :, None]
    m = m * m * (3 - 2 * m)
    out = aged * (1 - 0.45 * m) + mud * 0.85 * 0.45 * m
    # 올 사이 틈은 흙이 더 끼어 어둡게
    gaps = np.clip((0.33 - lum) * 3.0, 0, 1)
    out = out * (1 - 0.25 * gaps)
    # 평균색을 예전 마대(T_sandbag_row_0) 의 누런 갈색에 맞춤 — 새로 섞은 색이 회색 쪽으로 10 % 어두웠음
    target = np.array([0.49, 0.405, 0.28], dtype=np.float32)
    out = out * (target / out.reshape(-1, 3).mean(0))
    out = np.clip(out, 0, 1)
    img = bpy.data.images.new("T_TD_SandbagBurlap_BaseColor", w, h, alpha=False)
    px = np.concatenate([out, np.ones((h, w, 1))], axis=2).astype(np.float32).reshape(-1)
    img.pixels.foreach_set(px)
    img.filepath_raw = os.path.join(OUT, "T_TD_SandbagBurlap_BaseColor.png")
    img.file_format = "PNG"
    img.save()
    print("texture", w, h)


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, add_leaf_bones=False, mesh_smooth_type="OFF", use_mesh_modifiers=False)


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bake_texture()
    report = []
    for length, tag in ((3.0, "3m"), (1.5, "1m5")):
        for vi, var in enumerate("ABC"):
            for o in list(bpy.data.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            seed = 1950 + int(length * 10) * 10 + vi
            objs = []
            for lod in (0, 1):
                ob, tris = build_row(f"SM_TD_SandbagRow{tag}_{var}_LOD{lod}", length, seed, lod)
                objs.append(ob)
                report.append(f"{ob.name}: faces {tris}, dims {tuple(round(d, 3) for d in ob.dimensions)}")
            export(objs, os.path.join(OUT, f"SM_TD_SandbagRow{tag}_{var}.fbx"))
    with open(os.path.join(OUT, "sandbag_rows_report.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(report) + "\n")
    print("\n".join(report))


main()
