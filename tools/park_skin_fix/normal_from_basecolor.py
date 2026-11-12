# 박용팔 군복 노멀 맵을 색 텍스처에서 새로 만들기 (ART_REQUEST_20261110 R3, 2026-11-10)
#
# Body_BaseColor.png has the folds painted into it per UV island (auto atlas, hundreds of small islands). The old
# Body_Normal.png was made for the faceted mesh and shows a triangle pattern on the smoothed one (see rebake_report.txt,
# both earlier fixes rejected). This script throws the old map away and derives a new one that matches the paint:
#   1. rasterize sub-mesh 0 (texel -> triangle) and take luminance of the base color inside it
#   2. height = luminance minus its masked low-pass (removes large painted shading, keeps folds and seams)
#   3. gradient in texels -> world slope using each triangle's texel size (3D area / UV area), so islands with a
#      different texel density get the same physical bump; neighbours across island borders are not used
#   4. scale so the 95th percentile tilt is TILT_DEG, encode tangent-space (+X = U, +Y = V, Unity/OpenGL), then
#      bleed 16 px outward so mip levels do not pull in flat colour at island borders
# The base normal is flat relative to the smoothed mesh, so no per-triangle step can come back.
# usage: uv run --no-project --with numpy --with pillow --with scipy python -I normal_from_basecolor.py
#            <this folder> <Body_BaseColor.png> <out.png> [tilt_deg=22]
import os, sys, struct
import numpy as np
from PIL import Image
from scipy import ndimage

D, SRC, OUT = sys.argv[1:4]
TILT_DEG = float(sys.argv[4]) if len(sys.argv) > 4 else 22.0


def unit(a):
    return a / np.maximum(np.linalg.norm(a, axis=-1, keepdims=True), 1e-20)


raw = open(os.path.join(D, 'mesh.bin'), 'rb').read(); off = 0
nv = struct.unpack_from('<i', raw, off)[0]; off += 4
V = np.frombuffer(raw, '<f4', nv * 3, off).reshape(nv, 3).astype(np.float64); off += nv * 12 + nv * 12 + nv * 32
nsub = struct.unpack_from('<i', raw, off)[0]; off += 4
n = struct.unpack_from('<i', raw, off)[0]; off += 4
shirt = np.frombuffer(raw, '<i4', n, off).reshape(-1, 3)
raw = open(os.path.join(D, 'park_uv_tangent_old.bin'), 'rb').read(); assert struct.unpack_from('<i', raw, 0)[0] == nv
UV = np.frombuffer(raw, '<f4', nv * 2, 4).reshape(nv, 2).astype(np.float64)
raw = open(os.path.join(D, 'normals_fixed.bin'), 'rb').read()
N_new = unit(np.frombuffer(raw, '<f4', nv * 3, 4).reshape(nv, 3).astype(np.float64))
raw = open(os.path.join(D, 'park_tangent_new.bin'), 'rb').read()
T_new = np.frombuffer(raw, '<f4', nv * 4, 4).reshape(nv, 4).astype(np.float64)
T_new[:, :3] = unit(T_new[:, :3])

base = np.asarray(Image.open(SRC).convert('RGB')).astype(np.float64) / 255.0
H, W = base.shape[:2]

# ---- 1. rasterize ----
tri_id = np.full((H, W), -1, np.int64)
bary = np.zeros((H, W, 3))
for k, tri in enumerate(shirt):
    uv = UV[tri] * [W, H]; uv[:, 1] = H - uv[:, 1]
    x0, y0 = np.maximum(np.floor(uv.min(0)).astype(int), 0); x1, y1 = np.minimum(np.ceil(uv.max(0)).astype(int), [W - 1, H - 1])
    if x1 < x0 or y1 < y0: continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    a, b, c = uv
    den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(den) < 1e-12: continue
    w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
    w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
    w2 = 1 - w0 - w1
    inside = (w0 >= -1e-4) & (w1 >= -1e-4) & (w2 >= -1e-4)
    py = (ys[inside] - 0.5).astype(int); px = (xs[inside] - 0.5).astype(int)
    tri_id[py, px] = k
    bary[py, px] = np.stack([w0[inside], w1[inside], w2[inside]], -1)
mask = tri_id >= 0
ids = tri_id[mask]

# texel size in world units per triangle
P = V[shirt]
area3 = 0.5 * np.linalg.norm(np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]), axis=-1)
Q = UV[shirt] * [W, H]
areaUV = 0.5 * np.abs((Q[:, 1, 0] - Q[:, 0, 0]) * (Q[:, 2, 1] - Q[:, 0, 1]) - (Q[:, 2, 0] - Q[:, 0, 0]) * (Q[:, 1, 1] - Q[:, 0, 1]))
texel = np.sqrt(area3 / np.maximum(areaUV, 1e-9))
good = areaUV > 0.5
texel[~good] = np.median(texel[good])
texel_img = np.zeros((H, W)); texel_img[mask] = texel[ids]

# ---- 2. height from luminance (masked filters, so black gaps between islands do not leak in) ----
lum = base @ np.array([0.2126, 0.7152, 0.0722])
m = mask.astype(np.float64)
def mblur(img, s):
    return ndimage.gaussian_filter(img * m, s) / np.maximum(ndimage.gaussian_filter(m, s), 1e-6)
fine = mblur(lum, 1.0)
height = fine - mblur(lum, 20.0)

# ---- 3. gradient in texels, only between texels that are both inside ----
def grad(axis):
    fwd = np.roll(height, -1, axis); bwd = np.roll(height, 1, axis)
    mf = np.roll(mask, -1, axis) & mask; mb = np.roll(mask, 1, axis) & mask
    g = np.where(mf & mb, (fwd - bwd) * 0.5, np.where(mf, fwd - height, np.where(mb, height - bwd, 0.0)))
    return g
gx = grad(1)            # +x in image = +U
gy = -grad(0)           # image rows go down, V goes up
sx = gx / np.maximum(texel_img, 1e-12); sy = gy / np.maximum(texel_img, 1e-12)
slope = np.hypot(sx, sy)[mask]
k = np.tan(np.radians(TILT_DEG)) / max(np.percentile(slope, 95), 1e-12)
nx = -k * sx; ny = -k * sy
nrm = unit(np.stack([nx, ny, np.ones_like(nx)], -1))
nrm[~mask] = [0, 0, 1]

# ---- 4. bleed outward 16 px ----
filled = mask.copy(); out_n = nrm.copy()
for _ in range(16):
    acc = np.zeros_like(out_n); cnt = np.zeros((H, W))
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        f = np.roll(filled, (dy, dx), (0, 1)); acc += np.roll(out_n, (dy, dx), (0, 1)) * f[..., None]; cnt += f
    grow = (~filled) & (cnt > 0)
    out_n[grow] = unit(acc[grow] / cnt[grow][:, None]); filled |= grow
enc = np.clip(np.round((out_n + 1) * 0.5 * 255), 0, 255).astype(np.uint8)
Image.fromarray(enc, 'RGB').save(OUT)

tilt = np.degrees(np.arccos(np.clip(nrm[mask][:, 2], -1, 1)))
report = (f"normal from base colour: {int(mask.sum())} texels, tilt mean {tilt.mean():.1f} deg, 95th {np.percentile(tilt, 95):.1f} deg, "
          f"max {tilt.max():.1f} deg (target 95th {TILT_DEG})")
print(report)
open(os.path.join(D, 'normal_from_basecolor_report.txt'), 'w', encoding='utf-8').write(report + '\n')

# ---- check: base colour | old map x0.3 | new map x1, smooth mesh frame, one light ----
def shade(nmap_xyz):
    n_mesh = unit(np.einsum('kj,kjc->kc', bary[mask], N_new[shirt][ids]))
    t = unit(np.einsum('kj,kjc->kc', bary[mask], T_new[shirt][ids][..., :3]))
    sgn = np.sign(T_new[shirt[ids, 0], 3]); sgn[sgn == 0] = 1
    bt = sgn[:, None] * np.cross(n_mesh, t)
    v = nmap_xyz[mask]
    nn = unit(v[:, :1] * t + v[:, 1:2] * bt + v[:, 2:3] * n_mesh)
    L = unit(np.array([0.45, 0.55, 0.7]))
    s = np.full((H, W), 40.0); s[mask] = 40 + 200 * np.clip(nn @ L, 0, 1)
    return np.repeat(s[..., None], 3, -1)
old = np.asarray(Image.open(os.path.join(os.path.dirname(SRC), 'Body_Normal.png')).convert('RGB')).astype(np.float64) / 255 * 2 - 1 \
    if os.path.exists(os.path.join(os.path.dirname(SRC), 'Body_Normal.png')) else None
panels = [base * 255]
if old is not None:
    o = old.copy(); o[..., :2] *= 0.3; o[..., 2] = np.sqrt(np.clip(1 - (o[..., :2] ** 2).sum(-1), 0, 1)); panels.append(shade(o))
panels.append(shade(out_n))
ys, xs = np.nonzero(mask)
for name, (cy, cx) in {'a': (int(H * 0.72), int(W * 0.18)), 'b': (int(H * 0.55), int(W * 0.45))}.items():
    y0, x0 = max(0, min(H - 640, cy - 320)), max(0, min(W - 640, cx - 320))
    row = np.concatenate([np.pad(p[y0:y0 + 640, x0:x0 + 640], ((0, 0), (0, 8), (0, 0)), constant_values=255) for p in panels], 1)
    Image.fromarray(np.clip(row, 0, 255).astype(np.uint8), 'RGB').save(os.path.splitext(OUT)[0] + f'_check_{name}.png')
print('check images: base colour | old map x0.3 (now) | new map x1')
