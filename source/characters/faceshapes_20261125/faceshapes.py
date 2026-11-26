# 얼굴 블렌드 셰이프 생성 (헤드리스 Blender)
# 사용: blender -b --factory-startup --python faceshapes.py -- <ParkYongpal|Soldier|Person1> [--norender]
# 결과: faceshapes_<tag>.json, faceshapes_<tag>.blend, renders/<tag>_<Shape>.png
#
# 좌표: 계산은 Blender 월드(미터, Z 위, 얼굴 정면 = -Y, +X = 인물의 왼쪽)에서 하고,
#       저장은 메시 로컬 좌표(= FBX Geometry "Vertices" 원본 값)로 되돌린다.
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import fs_common as fc

argv = sys.argv[sys.argv.index("--") + 1:]
TAG = argv[0]
DO_RENDER = "--norender" not in argv
ONLY = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None

MM = 0.001
# ---------------------------------------------------------------- 모델별 표식 (월드 m)
CFG = {
    "ParkYongpal": dict(
        mouth=dict(cL=(-0.0360, 1.7003), cR=(0.0350, 1.7005), z0=1.7018, refine=True,
                   smooth_it=5, box_h=3.5 * MM, d0=1.2 * MM, sig_band=1.0 * MM, up_h=9 * MM, lo_h=9 * MM),
        jaw=dict(pivot=(-0.018, 1.733), chinB=(-0.097, 1.657), gonion=(-0.030, 1.672),
                 deg=10.0, fwd=1.5 * MM, pocket=5.0 * MM, lip_lag=0.55),
        eyes=[dict(I=(0.0215, 1.7787), O=(0.0468, 1.7805), up=1.7820, vis_lo=1.7752, lm_off=-0.6 * MM,
                   H=7 * MM, Hlow=3 * MM, cf=0.18, bulge=0.6 * MM, name="BlinkL"),
              dict(I=(-0.0222, 1.7783), O=(-0.0490, 1.7795), up=1.7818, vis_lo=1.7752, lm_off=-0.6 * MM,
                   H=7 * MM, Hlow=3 * MM, cf=0.18, bulge=0.6 * MM, name="BlinkR")],
        wide=dict(dx=7.0 * MM, dy=3.0 * MM, dz=0.6 * MM), round=dict(dx=9.0 * MM, fwd=6.0 * MM),
    ),
    "Soldier": dict(
        mouth=dict(cL=(-0.0187, 1.7302), cR=(0.0193, 1.7345), z0=1.7337, refine=False,
                   box_h=9.0 * MM, d0=1.2 * MM, sig_band=1.2 * MM, up_h=7 * MM, lo_h=9 * MM),
        jaw=dict(pivot=(-0.015, 1.780), chinB=(-0.088, 1.700), gonion=(-0.028, 1.715),
                 deg=10.0, fwd=1.5 * MM, pocket=3.0 * MM, lip_lag=0.40),
        eyes=[dict(I=(0.0180, 1.8035), O=(0.0515, 1.8018), up=1.8085, vis_lo=1.8005, lm_abs=1.7945,
                   H=9 * MM, Hlow=10 * MM, cf=0.10, bulge=0.5 * MM, name="BlinkL", collapse=True,
                   outline=dict(I=(0.0210, 1.8030), O=(0.0505, 1.8020), up=1.8092, lo=1.7996, sag=0.4 * MM, band=(1.7970, 1.8115))),
              dict(I=(-0.0185, 1.8012), O=(-0.0540, 1.8040), up=1.8088, vis_lo=1.8000, lm_abs=1.7990,
                   H=9 * MM, Hlow=6 * MM, cf=0.10, bulge=0.5 * MM, name="BlinkR", collapse=True,
                   outline=dict(I=(-0.0210, 1.8010), O=(-0.0530, 1.8042), up=1.8092, lo=1.7995, sag=0.4 * MM, band=(1.7970, 1.8115)))],
        no_blink="정점 이동만으로는 1.0에서 흰자·눈동자를 다 가릴 수 없음: 눈 텍스처가 걸친 삼각형 6~8개 중 다수가 꼭짓점을 눈썹(1811~1818mm)·뺨(1795mm)에 두고 있어, 눈 텍스처를 없애려면 눈썹·뺨까지 끌려옴 (2026-11-25 시험 후 제외)",
        wide=dict(dx=5.0 * MM, dy=2.5 * MM, dz=0.5 * MM), round=dict(dx=6.0 * MM, fwd=4.5 * MM),
    ),
    "Person1": dict(
        mouth=dict(cL=(-0.0345, 1.8115), cR=(0.0345, 1.8120), z0=1.8183, refine=True,
                   box_h=6.0 * MM, d0=1.0 * MM, sig_band=1.0 * MM, up_h=12 * MM, lo_h=13 * MM),
        jaw=dict(pivot=(-0.012, 1.853), chinB=(-0.112, 1.772), gonion=(-0.035, 1.790),
                 deg=7.0, fwd=2.0 * MM, pocket=3.5 * MM, lip_lag=0.55, lag_mode="z", roll=2.0 * MM, lo_lip=13 * MM, v2=True),
        eyes=[dict(I=(0.0245, 1.9005), O=(0.0495, 1.9025), up=1.9080, vis_lo=1.8998, lm_off=-0.5 * MM,
                   H=8 * MM, Hlow=3 * MM, cf=0.18, bulge=1.5 * MM, name="BlinkL"),
              dict(I=(-0.0255, 1.9015), O=(-0.0495, 1.9015), up=1.9085, vis_lo=1.9000, lm_off=-0.5 * MM,
                   H=8 * MM, Hlow=3 * MM, cf=0.18, bulge=1.5 * MM, name="BlinkR")],
        wide=dict(dx=7.0 * MM, dy=3.0 * MM, dz=0.6 * MM), round=dict(dx=9.0 * MM, fwd=6.0 * MM),
    ),
}
C = CFG[TAG]


def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- 불러오기
fbx, meshes, arm = fc.load_target(TAG)
o = fc.body_mesh(meshes)
me = o.data
co, W, mw = fc.arrays(o)          # co = 메시 로컬(FBX 원본), W = 월드
M3 = mw[:3, :3]
M3inv = np.linalg.inv(M3)
N = len(W)
X, Y, Z = W[:, 0], W[:, 1], W[:, 2]
hm = fc.head_mat_verts(o)

# 얼굴(머리 재질) 면만으로 BVH: 앞에서 광선을 쏴 표면 앞면 y를 얻는다
polys = [list(p.vertices) for p in me.polygons
         if me.materials[p.material_index] and fc.HEAD_MAT_KEY in me.materials[p.material_index].name]
bvh = BVHTree.FromPolygons([Vector(p) for p in W], polys)


def y_front(x, z):
    hit = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)), 2.0)
    return hit[0].y if hit[0] is not None else np.nan


y_front_v = np.vectorize(y_front)

# ---------------------------------------------------------------- 입술 경계선(seam)
mc = C["mouth"]
xL, zL = mc["cL"]; xR, zR = mc["cR"]; z0 = mc["z0"]
# 세 점을 지나는 2차식 초기값
A2 = np.array([[xL * xL, xL, 1], [0, 0, 1], [xR * xR, xR, 1]])
qa = np.linalg.solve(A2, np.array([zL, z0, zR]))
xs_s = np.linspace(xL, xR, int(round((xR - xL) / (0.5 * MM))) + 1)
zs_s = qa[0] * xs_s ** 2 + qa[1] * xs_s + qa[2]
refined = 0
if mc["refine"]:
    # 열마다 ±2.5mm 안에서 가장 깊은(뒤쪽) 점 = 입술 사이 홈
    for i, x in enumerate(xs_s):
        zz = np.arange(zs_s[i] - 2.5 * MM, zs_s[i] + 2.5 * MM, 0.1 * MM)
        yy = np.array([y_front(x, z) for z in zz])
        if np.all(np.isnan(yy)):
            continue
        k = int(np.nanargmax(yy))
        edge = min(np.nanmin(yy[:5]), np.nanmin(yy[-5:]))
        if 2 <= k <= len(zz) - 3 and yy[k] - edge > 0.3 * MM:
            zs_s[i] = zz[k]; refined += 1
    # 매끈하게 (이동 평균), 양 끝은 입꼬리 고정
    ker = np.ones(7) / 7
    pad = np.concatenate([np.full(3, zs_s[0]), zs_s, np.full(3, zs_s[-1])])
    zs_s = np.convolve(pad, ker, mode="valid")
    zs_s[0], zs_s[-1] = zL, zR
ys_s = np.array([y_front(x, z) for x, z in zip(xs_s, zs_s)])
ys_s = np.where(np.isnan(ys_s), np.nanmax(ys_s), ys_s)


def seam_z(x):
    return np.interp(np.clip(x, xL, xR), xs_s, zs_s)


def seam_y(x):
    return np.interp(np.clip(x, xL, xR), xs_s, ys_s)


xc_half = (xR - xL) / 2
xmid = (xR + xL) / 2

# ---------------------------------------------------------------- 공용 마스크
jc = C["jaw"]
py, pz = jc["pivot"]
cby, cbz = jc["chinB"]; gy, gz = jc["gonion"]
# 목 경계선(턱 아래 선) 아래로 가면 0
zn = cbz + (Y - cby) * (gz - cbz) / (gy - cby)
fade_neck = sstep(zn - 12 * MM, zn + 4 * MM, Z)
fade_post = sstep(py + 15 * MM, py - 5 * MM, Y)          # 턱관절보다 뒤(귀·뒤통수) 0
xcl = np.clip(X, xL, xR)
zsv = seam_z(X)
ysv = seam_y(X)
ex = np.abs(X - xcl)                                     # 입꼬리 바깥 거리
dyb = np.maximum(0.0, Y - ysv - 2 * MM)                  # 입술 앞면보다 뒤쪽 거리
in_mouth = sstep(xc_half + 1 * MM, xc_half - 6 * MM, np.abs(X - xmid))  # 입 안쪽 1, 입꼬리 0

# ---------------------------------------------------------------- 입술 경계 정점 사슬 (메시 연결 기준)
# 입꼬리→입꼬리, 가장 깊은 홈을 따라가는 최단 경로. 사슬로 윗입술/아랫입술 정점을 나눈다.
import heapq
ne = len(me.edges)
E = np.empty(ne * 2, np.int32); me.edges.foreach_get("vertices", E); E = E.reshape(-1, 2)
box = (X > xL - 2 * MM) & (X < xR + 2 * MM) & (np.abs(Z - zsv) < mc["box_h"]) & (Y < ysv + 5 * MM) & (Y > ysv - 12 * MM)
eb = E[box[E[:, 0]] & box[E[:, 1]]]
adj = {}
for a, b in eb:
    adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
bidx = np.where(box)[0]


def ecost(a, b):
    L = np.linalg.norm(W[a] - W[b])
    xm = (X[a] + X[b]) / 2; ym = (Y[a] + Y[b]) / 2; zm = (Z[a] + Z[b]) / 2
    deep = math.exp(-(ym - float(seam_y(xm))) / (0.6 * MM)) if mc["refine"] else 1.0
    zp = ((zm - float(seam_z(xm))) / (1.0 * MM)) ** 2
    return L * (0.05 + min(deep, 50.0) + 0.3 * zp)


def nearest(x, z):
    d = (X[bidx] - x) ** 2 + (Z[bidx] - z) ** 2
    return int(bidx[np.argmin(d)])


s0, s1 = nearest(xL, zL), nearest(xR, zR)
dist_d = {s0: 0.0}; prev = {}
pq = [(0.0, s0)]
while pq:
    dcur, v = heapq.heappop(pq)
    if v == s1:
        break
    if dcur > dist_d.get(v, 1e9):
        continue
    for w2 in adj.get(v, []):
        nd = dcur + ecost(v, w2)
        if nd < dist_d.get(w2, 1e9):
            dist_d[w2] = nd; prev[w2] = v; heapq.heappush(pq, (nd, w2))
chain = [s1]
while chain[-1] != s0 and chain[-1] in prev:
    chain.append(prev[chain[-1]])
chain_set = set(chain)
cx0, cx1 = min(X[v] for v in chain), max(X[v] for v in chain)
bidx_in = [v for v in bidx if cx0 < X[v] < cx1]
in_set = set(bidx_in)
# 사슬을 뺀 나머지를 연결 성분으로 나누고, 성분 평균 높이로 위/아래 판정
comp = {}
cid = 0
for v in bidx_in:
    if v in chain_set or v in comp:
        continue
    stack = [v]; comp[v] = cid
    while stack:
        u = stack.pop()
        for w2 in adj.get(u, []):
            if w2 not in chain_set and w2 not in comp and w2 in in_set:
                comp[w2] = cid; stack.append(w2)
    cid += 1
m_topo = np.full(N, np.nan)
small_island = np.zeros(N, bool)
comps = {}
for v, c in comp.items():
    comps.setdefault(c, []).append(v)
for c, vs in comps.items():
    vs = np.array(vs)
    if len(vs) < 25:
        # 작은 덩어리: 정점마다 높이로 판정, 홈 안쪽(앞면보다 뒤)에 숨은 것만 따로 표시
        m_topo[vs] = (Z[vs] < zsv[vs]).astype(float)
        small_island[vs] = Y[vs] > ysv[vs] + 0.5 * MM
    else:
        up = np.mean(Z[vs] - zsv[vs]) > 0
        m_topo[vs] = 0.0 if up else 1.0
m_topo[list(chain_set)] = mc.get("chain_w", 0.5)
print("@@CHAIN@@", [(round(X[v]*1000,1), round(Y[v]*1000,1), round(Z[v]*1000,1)) for v in chain], s0, s1)
chain_info = {"chain_vertices": len(chain), "components": len(comps), "small_island_vertices": int(small_island.sum())}

shapes = {}

# ---------------------------------------------------------------- JawOpen
# 입꼬리에서 턱관절 쪽으로 올라가는 경계선 (옆에서 본 기울기)
cy_corner = float(seam_y(xL)); cz_corner = float(min(zL, zR))
slope = (pz - cz_corner) / max(py - cy_corner, 1e-3)
zb = zsv + slope * dyb + 0.3 * ex
dist = np.sqrt(ex ** 2 + dyb ** 2)
# 입 안쪽은 좁게(입술 사이), 입꼬리로 갈수록 넓게 → 입꼬리에서 틈이 0으로 모인다
V2 = jc.get("v2", False)   # Person1용 넓은 입꼬리 감쇠 (2026-11-25 감독 피드백)
in_mouth_j = sstep(xc_half + 3 * MM, xc_half - 12 * MM, np.abs(X - xmid)) if V2 else in_mouth
dlt = mc["d0"] * in_mouth_j + (9 if V2 else 6) * MM * (1 - in_mouth_j) + 0.35 * dist
m_field = sstep(zb + dlt, zb - dlt, Z)                  # 경계 아래 = 1
# 입 안쪽 상자에서는 메시 연결로 나눈 값을 쓴다 (입꼬리 쪽은 넓은 공간 경계로 이어짐)
chain_half = (cx1 - cx0) / 2; chain_mid = (cx1 + cx0) / 2
taper = sstep(chain_half, chain_half - (12 if V2 else 6) * MM, np.abs(X - chain_mid))
alpha = np.where(np.isnan(m_topo), 0.0, taper * (sstep(mc["box_h"], 0.5 * mc["box_h"], np.abs(Z - zsv)) if V2 else 1.0))
m_low = alpha * np.nan_to_num(m_topo) + (1 - alpha) * m_field
w_jaw = m_low * fade_neck * fade_post
# 아랫입술은 턱보다 조금 덜 따라간다 (입술 틈이 턱 벌림보다 작게)
# 경계 위/아래를 계단이 아닌 부드러운 감쇠로, 입꼬리 쪽은 사슬 범위로 줄여 입꼬리 옆 꺾임을 막는다
below = sstep(zsv + 1.5 * MM, zsv - 1.5 * MM, Z)
# 아랫입술 붉은 부분(경계~lo_lip)은 한 덩어리로 같은 양, 그 아래 피부에서 0으로 → 입술이 늘어나지 않음
if V2:
    lipzone = sstep(zsv - jc["lo_lip"] - 8 * MM, zsv - jc["lo_lip"], Z) * sstep(xc_half + 12 * MM, xc_half, np.abs(X - xmid)) * below
else:
    lipzone = np.exp(-((Z - zsv) / (6 * MM)) ** 2) * in_mouth * below
ry, rz = Y - py, Z - pz
D = np.zeros_like(W)
if jc.get("lag_mode", "angle") == "angle":
    w_rot = w_jaw * (1 - jc["lip_lag"] * lipzone)
    th = np.radians(jc["deg"]) * w_rot
    D[:, 1] = ry * np.cos(th) - rz * np.sin(th) + py - Y
    D[:, 2] = ry * np.sin(th) + rz * np.cos(th) + pz - Z
else:
    # 'z': 아랫입술은 턱과 같이 뒤로 가되 아래로만 덜 내려간다 → 앞으로 튀어나오지 않음
    th = np.radians(jc["deg"]) * w_jaw
    D[:, 1] = ry * np.cos(th) - rz * np.sin(th) + py - Y
    D[:, 2] = (ry * np.sin(th) + rz * np.cos(th) + pz - Z) * (1 - jc["lip_lag"] * lipzone)
D[:, 1] -= jc["fwd"] * w_jaw
# 아랫입술 말아 넣기: 경계 바로 아래 입술을 뒤로
D[:, 1] += jc.get("roll", 0.0) * np.exp(-((zsv - Z) / (4 * MM)) ** 2) * taper * below
# 아랫입술 윗면(경계 바로 아래)을 안쪽으로 밀어 넣어 틈의 면이 아래·앞을 보게(그늘) 한다
band = np.exp(-((Z - zsv) / mc["sig_band"]) ** 2) * in_mouth * (Y < ysv + 6 * MM)
lower_edge = np.exp(-((zsv - Z) / (1.5 * MM)) ** 2) * alpha * (np.nan_to_num(m_topo, nan=0.0) > 0.5)
D[:, 1] += jc["pocket"] * lower_edge
D[:, 1] += 1.6 * jc["pocket"] * band * alpha * np.isin(np.arange(N), list(chain_set))
# 홈 안에 숨어 있던 작은 조각은 더 깊이 넣어 틈에 드러나지 않게
D[:, 1] += 1.5 * jc["pocket"] * small_island * alpha
# 입 상자 안 이동값을 이웃 평균으로 다듬어 경계 톱니를 줄인다
sm = np.where((X > xL - 4 * MM) & (X < xR + 4 * MM) & (np.abs(Z - zsv) < mc["box_h"] + 2 * MM) & (Y < ysv + 8 * MM))[0]
sm_set = set(sm.tolist())
nb = {}
for a, b in E:
    if a in sm_set and b in sm_set:
        nb.setdefault(a, []).append(b); nb.setdefault(b, []).append(a)
for _ in range(mc.get("smooth_it", 0)):
    D2 = D.copy()
    for v, ns in nb.items():
        D2[v] = 0.5 * D[v] + 0.5 * D[ns].mean(0)
    D = D2
shapes["JawOpen"] = D
if "--diag" in argv:
    q = np.where((X > -0.040) & (X < -0.020) & (Z > 1.796) & (Z < 1.822) & (Y < -0.10))[0]
    for v in q[np.argsort(X[q])]:
        print("@@D@@", round(X[v]*1000,1), round(Y[v]*1000,1), round(Z[v]*1000,1), "m_topo", m_topo[v], "mf", round(float(m_field[v]),2), "al", round(float(alpha[v]),2), "tp", round(float(taper[v]),2), "D", np.round(D[v]*1000,2))

# ---------------------------------------------------------------- MouthClose (ㅁㅂㅍ: 입술을 눌러 붙임)
g_up = np.exp(-((Z - zsv) / mc["up_h"]) ** 2) * (Z >= zsv)
g_lo = np.exp(-((Z - zsv) / mc["lo_h"]) ** 2) * (Z < zsv)
lat = sstep(xc_half + 10 * MM, xc_half - 4 * MM, np.abs(X - xmid))
front = sstep(ysv + 12 * MM, ysv + 4 * MM, Y)
g_lip = (g_up + g_lo) * lat * front * fade_neck
D = np.zeros_like(W)
D[:, 2] = -0.16 * (Z - zsv) * g_lip                     # 위아래 입술을 경계 쪽으로 눌러 얇게
g_lo_s = np.exp(-((Z - zsv) / mc["lo_h"]) ** 2) * below    # 경계에서 계단 없이
D[:, 2] += 0.8 * MM * g_lo_s * lat * front              # 아랫입술을 살짝 밀어 올림(턱끝 근육)
D[:, 1] = 1.0 * MM * g_lip + 0.6 * MM * band            # 입술을 뒤로 눌러 붙임
shapes["MouthClose"] = D

# ---------------------------------------------------------------- MouthWide (ㅣㅔ: 입꼬리를 옆으로)
wc = C["wide"]
gz_w = np.exp(-((Z - zsv) / (9 * MM)) ** 2)
r = np.clip(np.abs(X - xmid) / xc_half, 0, 1)
out = np.maximum(0.0, np.abs(X - xmid) - xc_half)
decay = np.exp(-(out / (12 * MM)) ** 2)
sx = np.sign(X - xmid)
w_face = gz_w * decay * fade_neck * fade_post * sstep(ysv + 35 * MM, ysv + 15 * MM, Y)
D = np.zeros_like(W)
D[:, 0] = wc["dx"] * sx * r * w_face
D[:, 1] = wc["dy"] * r ** 2 * w_face
D[:, 2] = wc["dz"] * r ** 2 * w_face - 0.10 * (Z - zsv) * g_lip
shapes["MouthWide"] = D

# ---------------------------------------------------------------- MouthRound (ㅗㅜ: 입술을 모아 앞으로)
rc = C["round"]
D = np.zeros_like(W)
D[:, 0] = -rc["dx"] * sx * r * w_face
pucker = np.exp(-((Z - zsv) / (8 * MM)) ** 2) * (1 - 0.6 * r ** 2) * sstep(xc_half + 8 * MM, xc_half - 2 * MM, np.abs(X - xmid))
D[:, 1] = -rc["fwd"] * pucker * front * fade_neck
D[:, 2] = 0.08 * (Z - zsv) * g_lip
shapes["MouthRound"] = D

# ---------------------------------------------------------------- Blink
me.calc_loop_triangles()
_nt = len(me.loop_triangles)
TRI = np.empty(_nt * 3, np.int32); me.loop_triangles.foreach_get("vertices", TRI); TRI = TRI.reshape(-1, 3)


def collapse_eye(e):
    """저밀도 눈: 눈 텍스처(흰자·눈동자)가 걸친 삼각형의 정점을 모두 감긴 선 위로 모은다.
    → 1.0에서 눈 텍스처가 덮인 면의 화면 넓이가 0이 되어 흰자·눈동자가 보이지 않는다.
    둘레 1고리 정점은 일부만 따라와 주변 피부 늘어남을 나눈다."""
    ol = e["outline"]
    (oxi, ozi), (oxo, ozo) = ol["I"], ol["O"]
    omid = (ozi + ozo) / 2
    pts = []
    for x in np.linspace(oxi, oxo, 80):
        uu = (x - oxi) / (oxo - oxi)
        bb = (4 * uu * (1 - uu)) ** 0.7
        zline = ozi + (ozo - ozi) * uu
        zlo = zline - (omid - ol["lo"]) * bb - 0.3 * MM
        zup = zline + (ol["up"] - omid) * bb + 0.3 * MM
        for t in np.linspace(0, 1, 9):
            pts.append((x, zlo + t * (zup - zlo)))
    pts = np.array(pts)
    cx_, cz_ = (oxi + oxo) / 2, omid
    near = (np.abs(X - cx_) < 0.045) & (np.abs(Z - cz_) < 0.03) & (Y < -0.03)
    cand_t = TRI[near[TRI].all(1)]
    hit = set()
    for t in cand_t:
        a, b, c = W[t[0]][[0, 2]], W[t[1]][[0, 2]], W[t[2]][[0, 2]]
        v0, v1 = b - a, c - a
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12:
            continue
        p = pts - a
        l1 = (p[:, 0] * v1[1] - v1[0] * p[:, 1]) / den
        l2 = (v0[0] * p[:, 1] - p[:, 0] * v0[1]) / den
        if np.any((l1 >= -1e-6) & (l2 >= -1e-6) & (l1 + l2 <= 1 + 1e-6)):
            hit.update(int(i) for i in t)
    S = np.array(sorted(hit), dtype=int)
    if "band" in ol:   # 눈 띠 안 정점만 접는다 (눈썹·뺨 정점은 고정)
        S = S[(Z[S] > ol["band"][0]) & (Z[S] < ol["band"][1])]

    def closure_z(x):
        uu = (x - oxi) / (oxo - oxi)
        bb = (4 * np.clip(uu, 0, 1) * (1 - np.clip(uu, 0, 1))) ** 0.7
        return ozi + (ozo - ozi) * uu - ol["sag"] * bb

    D = np.zeros_like(W)
    zc = closure_z(X[S])
    yc = np.array([y_front(x, z) for x, z in zip(X[S], zc)])
    yc = np.where(np.isnan(yc), Y[S], yc) - 0.2 * MM
    D[S, 2] = zc - Z[S]
    D[S, 1] = yc - Y[S]
    # 둘레 1고리: 이웃 평균 이동의 일부
    Sset = set(S.tolist())
    ring = {}
    for a2, b2 in E:
        a2, b2 = int(a2), int(b2)
        if a2 in Sset and b2 not in Sset:
            ring.setdefault(b2, []).append(a2)
        elif b2 in Sset and a2 not in Sset:
            ring.setdefault(a2, []).append(b2)
    for v, ns in ring.items():
        if Y[v] < -0.03:
            D[v] = 0.35 * D[ns].mean(0)
    far = float(np.max(np.linalg.norm(D[S], axis=1))) if len(S) else 0.0
    print("@@COLLAPSE@@", e["name"], [(round(X[v] * 1000, 1), round(Z[v] * 1000, 1)) for v in S], "ring", len(ring), "max_mm", round(far * 1000, 2))
    return D, dict(collapsed=int(len(S)), ring=int(len(ring)))


eye_info = {}
for e in C["eyes"]:
    (xi, zi), (xo, zo) = e["I"], e["O"]
    u = (X - xi) / (xo - xi)
    inside = (u > 0) & (u < 1)
    uc = np.clip(u, 0, 1)
    bump = (4 * uc * (1 - uc)) ** 0.7
    zl = zi + (zo - zi) * uc
    zl_mid = zi + (zo - zi) * 0.5
    Hu = e["up"] - zl_mid
    z_um = zl + Hu * bump
    z_vlo = zl - (zl_mid - e["vis_lo"]) * bump          # 보이는 아래 눈꺼풀 선
    if "lm_abs" in e:
        z_lm = zl - (zl_mid - e["lm_abs"]) * bump      # 접어 넣을 아래 한계 (저밀도: 아래 정점 줄까지)
    else:
        z_lm = z_vlo + e["lm_off"] * bump
    z_c = z_vlo + e["cf"] * (z_um - z_vlo)              # 감긴 선
    # 열마다 앞면 y (후보 정점만 광선 투사)
    cand = np.where(inside & (Z > z_lm - 12 * MM) & (Z < z_um + e["H"] + 1 * MM) & (Y < -0.03))[0]
    yf_um = np.full(N, np.nan); yf_vlo = np.full(N, np.nan); yf_v = np.full(N, np.nan)
    if len(cand):
        yf_um[cand] = y_front_v(X[cand], z_um[cand])
        yf_vlo[cand] = y_front_v(X[cand], z_vlo[cand])
        yf_v[cand] = y_front_v(X[cand], Z[cand])
    y_c = np.fmin(yf_vlo, yf_um) - 0.3 * MM
    zone = inside & (Z > z_lm - 4 * MM) & (Z < z_um + e["H"]) & (np.abs(Y - np.nan_to_num(yf_v, nan=0)) < 15 * MM) & (Y < -0.03)
    hidden = (Y > np.nan_to_num(yf_v, nan=-1) + 1.5 * MM)
    clsA = zone & (((Z >= z_lm) & (Z <= z_um)) | (hidden & (Z <= z_um + e["H"]) & (Z >= z_lm)))
    clsL = zone & ~clsA & (Z > z_um)
    clsB = zone & ~clsA & (Z < z_lm) & (Z > z_lm - e["Hlow"])
    D = np.zeros_like(W)
    # A: 눈 면 → 감긴 선 바로 뒤로 접어 넣음
    tA = np.where(clsA)[0]
    D[tA, 2] = z_c[tA] + 0.04 * (Z[tA] - z_c[tA]) - Z[tA]
    D[tA, 1] = y_c[tA] + 0.3 * MM + 0.03 * (Y[tA] - y_c[tA]) - Y[tA]
    # L: 윗눈꺼풀 피부 → 가장자리는 감긴 선까지, 위로 갈수록 덜
    tL = np.where(clsL)[0]
    f = np.clip(1 - (Z[tL] - z_um[tL]) / e["H"], 0, 1)
    fs = f * f * (3 - 2 * f) * 0.35 + f * 0.65
    D[tL, 2] = fs * (z_c[tL] - z_um[tL])
    D[tL, 1] = fs * (y_c[tL] - yf_um[tL]) - e["bulge"] * np.sin(np.pi * fs) * bump[tL]
    # B: 아래 눈꺼풀 → 감긴 선 쪽으로 조금 올라감
    tB = np.where(clsB)[0]
    g = (1 - (z_lm[tB] - Z[tB]) / e["Hlow"]) ** 1.5
    D[tB, 2] = g * (z_c[tB] - z_lm[tB])
    D[tB, 1] = g * (y_c[tB] - np.nan_to_num(yf_vlo[tB], nan=Y[tB]))
    D = np.nan_to_num(D)
    if e.get("collapse"):
        D, info = collapse_eye(e)
        shapes[e["name"]] = D
        eye_info[e["name"]] = info
        continue
    shapes[e["name"]] = D
    eye_info[e["name"]] = dict(nA=int(len(tA)), nL=int(len(tL)), nB=int(len(tB)))

# ---------------------------------------------------------------- 저장 (메시 로컬 = FBX 원본 좌표)
ORDER = ["JawOpen", "MouthClose", "MouthWide", "MouthRound", "BlinkL", "BlinkR"]
OMITTED = {}
if C.get("no_blink"):
    ORDER = [n for n in ORDER if not n.startswith("Blink")]
    OMITTED = {"BlinkL": C["no_blink"], "BlinkR": C["no_blink"]}
TH = 0.005 * MM   # 월드 0.005mm 미만 이동은 버림
out = {
    "fbx": os.path.relpath(fbx, fc.ROOT).replace("\\", "/"),
    "mesh": o.name,
    "fbx_geometry": None,
    "vertex_count_fbx": N,
    "space": None,
    "shapes": {},
    "stats": {},
}
# FBX Geometry 이름 (원본 조사 결과와 동일)
out["space"] = {
    "positions_and_deltas": "FBX 메시 노드 로컬 좌표 = FBX 파일 Geometry 'Vertices' 배열의 원본 값 그대로 (Blender로 가져온 mesh.vertices.co 와 비트 단위로 같음을 확인).",
    "local_axes": "이 로컬 공간은 Z가 위(정수리 방향), -Y가 얼굴 정면, +X가 인물의 왼쪽이다. 메시 노드에 Lcl Rotation X=-90 이 걸려 있어 FBX/Unity의 Y-up으로 세워진다.",
    "fbx_file_units": "GlobalSettings UnitScaleFactor=1 (cm), UpAxis=Y, FrontAxis=+Z, CoordAxis=+X (오른손 좌표).",
    "mesh_node_lcl_scaling_fbx": None,
    "blender_world_from_local": "world(m, Blender Z-up) = matrix_world_blender @ local; 아래 matrix_world_blender 참고 (비균등 배율 포함). 이동값도 같은 3x3로 변환하면 월드 미터가 된다.",
    "unity_expectation": "Unity(useFileScale=1, bakeAxisConversion=0)는 노드 회전·배율을 Transform으로 두고 정점은 로컬 그대로 가져오며, 오른손→왼손 변환으로 X 부호를 뒤집는 것이 일반적이다: unity_vertex ≈ (-x, y, z) * k. k(1 또는 0.01)와 X 부호는 sharedMesh.bounds 를 아래 local_bbox 와 비교해 확정할 것. 이동값에도 같은 (-1,1,1)*k 를 곱한다(평행이동 없음).",
    "match_rule": "Unity가 이음매에서 쪼갠 정점은 위치가 같으므로, 변환한 위치로 최근접 짝(허용 오차 ~1e-5 로컬 단위)을 찾아 같은 위치의 모든 Unity 정점에 같은 이동값을 준다.",
}
mwl = [list(map(float, r)) for r in mw]
out["matrix_world_blender"] = mwl
out["local_bbox"] = {"min": co.min(0).tolist(), "max": co.max(0).tolist()}
out["moved_threshold_world_mm"] = TH / MM
dup_report = {}
for nm in ORDER:
    Dw = shapes[nm]
    mag = np.linalg.norm(Dw, axis=1)
    idx = np.where(mag > TH)[0]
    Dl = Dw @ M3inv.T
    rows = [[float(co[i, 0]), float(co[i, 1]), float(co[i, 2]),
             float(Dl[i, 0]), float(Dl[i, 1]), float(Dl[i, 2])] for i in idx]
    out["shapes"][nm] = rows
    out["stats"][nm] = {"moved_vertices": int(len(idx)), "max_move_world_mm": float(mag.max() / MM),
                        "max_move_local": float(np.linalg.norm(Dl, axis=1).max())}
    # 같은 위치에 서로 다른 이동값이 있는 정점(위치 짝짓기 모호) 확인
    q = np.round(co[idx] / 1e-6).astype(np.int64)
    keys = {}
    amb = 0
    for j, kk in enumerate(map(tuple, q)):
        if kk in keys and np.linalg.norm(Dl[idx[j]] - Dl[keys[kk]]) > 1e-7:
            amb += 1
        keys.setdefault(kk, idx[j])
    dup_report[nm] = amb
out["stats"]["ambiguous_same_position_diff_delta"] = dup_report
out["stats"]["eyes"] = eye_info
out["stats"]["seam_refined_columns"] = refined
out["stats"]["mouth_chain"] = chain_info
out["omitted_shapes"] = OMITTED
out["seam_world"] = {"x": xs_s.tolist(), "z": zs_s.tolist(), "y_front": ys_s.tolist()}
jp = os.path.join(fc.WORK, f"faceshapes_{TAG}.json")
with open(jp, "w", encoding="utf-8") as fp:
    json.dump(out, fp, ensure_ascii=False)
print("@@STATS@@" + json.dumps(out["stats"], ensure_ascii=False))

# ---------------------------------------------------------------- 셰이프 키 추가 + .blend 저장
if not me.shape_keys:
    o.shape_key_add(name="Basis", from_mix=False)
for nm in ORDER:
    sk = o.shape_key_add(name=nm, from_mix=False)
    Dl = shapes[nm] @ M3inv.T
    sk.data.foreach_set("co", (co + Dl).ravel())
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(fc.WORK, f"faceshapes_{TAG}.blend"), compress=True)

if not DO_RENDER:
    raise SystemExit

# ---------------------------------------------------------------- 확인 렌더
eng = fc.set_engine(("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"))
fc.setup_world(0.25)
sc = bpy.context.scene
T = 520
sc.render.resolution_x = sc.render.resolution_y = T
try:
    sc.eevee.taa_render_samples = 32
except Exception:
    pass
eyes_z = float(np.mean([e["up"] for e in C["eyes"]]))
zmid = (eyes_z + z0) / 2
yface = float(np.nanmin(ys_s)) + 0.03
tgt = Vector((0.0, yface, zmid - 0.005))
lens = 85
d_face = 0.19 * lens / 36
d_zoom = 0.085 * lens / 36
fc.make_light("Key", (0.35, -0.55, zmid + 0.35), tgt, 14, 0.5)
fc.make_light("Fill", (-0.5, -0.45, zmid + 0.05), tgt, 5, 0.6)
fc.make_light("Rim", (0.2, 0.5, zmid + 0.3), tgt, 6, 0.4)
cam = fc.make_cam((0, -1, zmid), tgt, lens=lens)
key_eye = {"BlinkL": C["eyes"][0], "BlinkR": C["eyes"][1]}
views = {
    "front": (Vector((0, -1, 0)), d_face, tgt),
    "q45": (Vector((0.707, -0.707, 0)), d_face, tgt),
    "side": (Vector((1, 0, 0)), d_face, tgt + Vector((0, 0.02, 0))),
}


def zoom_target(nm):
    if nm.startswith("Blink"):
        e = key_eye[nm]
        x = (e["I"][0] + e["O"][0]) / 2
        return Vector((x, float(np.nanmin(ys_s)) + 0.02, e["up"] - 0.002))
    return Vector((xmid, float(np.nanmin(ys_s)), z0 - 0.004))


rdir = os.path.join(fc.WORK, "renders")
os.makedirs(rdir, exist_ok=True)
tmp = os.path.join(rdir, "_tmp_" + TAG)
os.makedirs(tmp, exist_ok=True)


def shot(nm_view, direction, dist, target, path):
    fc.aim(cam, target + direction.normalized() * dist, target)
    fc.render(path)
    img = bpy.data.images.load(path)
    a = np.array(img.pixels[:]).reshape(T, T, 4)
    bpy.data.images.remove(img)
    return a


def set_shape(nm):
    for kb in me.shape_keys.key_blocks[1:]:
        kb.value = 1.0 if kb.name == nm else 0.0


base_cache = {}
set_shape(None)
for vn, (dv, dist, tg) in views.items():
    base_cache[vn] = shot(vn, dv, dist, tg, os.path.join(tmp, f"base_{vn}.png"))
for nm in [n for n in ORDER if not ONLY or n in ONLY]:
    rows = []
    zt = zoom_target(nm)
    set_shape(None)
    bz = shot("zoom", Vector((0, -1, 0)), d_zoom, zt, os.path.join(tmp, "bz.png"))
    set_shape(nm)
    for vn, (dv, dist, tg) in views.items():
        s = shot(vn, dv, dist, tg, os.path.join(tmp, f"s_{vn}.png"))
        rows.append(np.concatenate([base_cache[vn], np.ones((T, 6, 4)), s], axis=1))
    sz = shot("zoom", Vector((0, -1, 0)), d_zoom, zt, os.path.join(tmp, "sz.png"))
    rows.append(np.concatenate([bz, np.ones((T, 6, 4)), sz], axis=1))
    sep = np.ones((6, rows[0].shape[1], 4))
    # 이미지 행 0 = 아래쪽: 위에서부터 front, q45, side, zoom 순으로 보이게 뒤집어 쌓는다
    stack = []
    for rr in rows[::-1]:
        stack += [rr, sep]
    big = np.concatenate(stack[:-1], axis=0)
    H_, W_ = big.shape[:2]
    im = bpy.data.images.new(f"{TAG}_{nm}", W_, H_, alpha=True)
    im.pixels[:] = big.ravel()
    im.filepath_raw = os.path.join(rdir, f"{TAG}_{nm}.png")
    im.file_format = "PNG"
    im.save()
    print("rendered", nm)
set_shape(None)

# ---------------------------------------------------------------- 조합 렌더 (말할 때 섞어 쓰는 경우)
COMBOS = [("J05_R05", {"JawOpen": 0.5, "MouthRound": 0.5}),
          ("J06_W04", {"JawOpen": 0.6, "MouthWide": 0.4}),
          ("J03", {"JawOpen": 0.3})]


def set_mix(mix):
    for kb in me.shape_keys.key_blocks[1:]:
        kb.value = float(mix.get(kb.name, 0.0))


if not ONLY or "combos" in ONLY:
    zt = zoom_target("JawOpen")
    set_mix({})
    bz = shot("zoom", Vector((0, -1, 0)), d_zoom, zt, os.path.join(tmp, "bz.png"))
    bq = shot("zoomq", Vector((0.707, -0.707, 0)), d_zoom, zt, os.path.join(tmp, "bq.png"))
    for cname, mix in COMBOS:
        set_mix(mix)
        rows = []
        for vn in ("front", "q45"):
            dv, dist, tg = views[vn]
            s = shot(vn, dv, dist, tg, os.path.join(tmp, f"c_{vn}.png"))
            rows.append(np.concatenate([base_cache[vn], np.ones((T, 6, 4)), s], axis=1))
        sz = shot("zoom", Vector((0, -1, 0)), d_zoom, zt, os.path.join(tmp, "cz.png"))
        rows.append(np.concatenate([bz, np.ones((T, 6, 4)), sz], axis=1))
        sq = shot("zoomq", Vector((0.707, -0.707, 0)), d_zoom, zt, os.path.join(tmp, "cq.png"))
        rows.append(np.concatenate([bq, np.ones((T, 6, 4)), sq], axis=1))
        sep = np.ones((6, rows[0].shape[1], 4))
        stack = []
        for rr in rows[::-1]:
            stack += [rr, sep]
        big = np.concatenate(stack[:-1], axis=0)
        H_, W_ = big.shape[:2]
        im = bpy.data.images.new(f"{TAG}_combo_{cname}", W_, H_, alpha=True)
        im.pixels[:] = big.ravel()
        im.filepath_raw = os.path.join(rdir, f"{TAG}_combo_{cname}.png")
        im.file_format = "PNG"
        im.save()
        print("rendered combo", cname)
    set_mix({})
print("@@DONE@@ engine", eng)
