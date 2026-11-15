# Ages the KPA Mosin's textures (2026-11-15): the delivered set read as a new rifle with bright orange wood.
#
# The UV layout is a scatter of small islands, so wear painted in texture space lands in random places. The
# mesh is rasterised into the texture instead (position, normal and convexity per texel) and the wear is laid
# out on the rifle itself: finish darkened by oil where the hands hold it, grain streaks along the stock,
# dents, grime on the underside and in the hollows, bluing rubbed to bare steel on edges, a little rust.
#
#   mesh.bin  : written from Unity (vertices, normals, uv, triangles of SM_WP_Mosin, rifle along +Z)
#   usage     : python mosin_wear.py <mesh.bin> <original texture dir> <output texture dir>
import struct, sys
import numpy as np
from PIL import Image, ImageFilter

MESH, SRC, DST = sys.argv[1], sys.argv[2], sys.argv[3]
N = 1024
rng = np.random.default_rng(1950)


def load_mesh(path):
    data = open(path, 'rb').read()
    o = 0
    count, = struct.unpack_from('<i', data, o); o += 4
    V, T = [], []
    base = 0
    for _ in range(count):
        nv, nt = struct.unpack_from('<ii', data, o); o += 8
        v = np.frombuffer(data, '<f4', nv * 8, o).reshape(nv, 8); o += nv * 32
        t = np.frombuffer(data, '<i4', nt, o).reshape(-1, 3); o += nt * 4
        V.append(v); T.append(t + base); base += nv
    return np.vstack(V).astype(np.float64), np.vstack(T)


def convexity(P, Nn, T):
    # > 0 on outward edges and corners, < 0 in hollows. Vertices split by UV seams are welded by position.
    key = np.round(P * 20000).astype(np.int64)
    _, weld = np.unique(key, axis=0, return_inverse=True)
    weld = weld.reshape(-1)
    nw = weld.max() + 1
    pn = np.zeros((nw, 3)); np.add.at(pn, weld, Nn)
    pn /= np.maximum(np.linalg.norm(pn, axis=1, keepdims=True), 1e-9)
    pp = np.zeros((nw, 3)); cnt = np.zeros(nw); np.add.at(pp, weld, P); np.add.at(cnt, weld, 1); pp /= cnt[:, None]
    acc = np.zeros(nw); num = np.zeros(nw)
    for a, b in ((0, 1), (1, 2), (2, 0)):
        i, j = weld[T[:, a]], weld[T[:, b]]
        d = pp[j] - pp[i]; L = np.maximum(np.linalg.norm(d, axis=1), 1e-6)
        c = np.einsum('ij,ij->i', pn[j] - pn[i], d) / L
        np.add.at(acc, i, c); np.add.at(num, i, 1); np.add.at(acc, j, c); np.add.at(num, j, 1)
    return (acc / np.maximum(num, 1))[weld]


def rasterise(V, T, attrs):
    # attrs: (nv, k) -> (N, N, k) map and coverage mask, by barycentric fill of each triangle in UV space
    out = np.zeros((N, N, attrs.shape[1])); mask = np.zeros((N, N), bool)
    uv = V[:, 6:8].copy(); uv[:, 1] = 1.0 - uv[:, 1]; uv *= N
    for tri in T:
        p = uv[tri]
        x0, y0 = np.floor(p.min(0)).astype(int) - 1; x1, y1 = np.ceil(p.max(0)).astype(int) + 1
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, N - 1), min(y1, N - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(d) < 1e-9: continue
        w0 = ((p[1, 1] - p[2, 1]) * (xs - p[2, 0]) + (p[2, 0] - p[1, 0]) * (ys - p[2, 1])) / d
        w1 = ((p[2, 1] - p[0, 1]) * (xs - p[2, 0]) + (p[0, 0] - p[2, 0]) * (ys - p[2, 1])) / d
        w2 = 1 - w0 - w1
        e = -0.6 / max(np.sqrt(abs(d)), 1.0)  # a little over the edge so island borders are covered
        inside = (w0 >= e) & (w1 >= e) & (w2 >= e)
        if not inside.any(): continue
        a = attrs[tri]
        val = w0[..., None] * a[0] + w1[..., None] * a[1] + w2[..., None] * a[2]
        sub = out[y0:y1 + 1, x0:x1 + 1]; sub[inside] = val[inside]
        mask[y0:y1 + 1, x0:x1 + 1] |= inside
    return out, mask


def spread(m, mask, rounds=48):
    # pushes island values out into the padding so mips and bilinear taps at seams read the same wear
    m = m.copy(); mask = mask.copy()
    for _ in range(rounds):
        if mask.all(): break
        acc = np.zeros_like(m); cnt = np.zeros(mask.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sm = np.roll(mask, (dy, dx), (0, 1)); sv = np.roll(m, (dy, dx), (0, 1))
            acc += sv * sm[..., None]; cnt += sm
        new = (~mask) & (cnt > 0)
        m[new] = acc[new] / cnt[new][:, None]; mask |= new
    return m, mask


def noise3(p, freq, seed):
    # trilinear value noise on 3D points (so the pattern is continuous across UV seams)
    q = p * np.asarray(freq); i = np.floor(q).astype(np.int64); f = q - i; f = f * f * (3 - 2 * f)
    def h(ix, iy, iz):
        n = (ix * 374761393 + iy * 668265263 + iz * 2147483647 + seed * 1274126177) & 0xFFFFFFFF
        n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0
    r = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (f[..., 0] if dx else 1 - f[..., 0]) * (f[..., 1] if dy else 1 - f[..., 1]) * (f[..., 2] if dz else 1 - f[..., 2])
                r = r + w * h(i[..., 0] + dx, i[..., 1] + dy, i[..., 2] + dz)
    return r


def fbm(p, freq, seed, octaves=4):
    r = 0; a = 0.5; tot = 0
    for k in range(octaves):
        r = r + a * noise3(p, np.asarray(freq) * (2 ** k), seed + k * 17); tot += a; a *= 0.5
    return r / tot


def smooth(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def mix(a, b, t): return a + (b - a) * (t[..., None] if a.ndim == 3 and t.ndim == 2 else t)


V, T = load_mesh(MESH)
P, Nn = V[:, 0:3], V[:, 3:6]
conv = convexity(P, Nn, T)
attrs = np.hstack([P, Nn, conv[:, None]])
m, cover = rasterise(V, T, attrs)
m, _ = spread(m, cover)
pos, nrm, cv = m[..., 0:3], m[..., 3:6], m[..., 6]
nrm /= np.maximum(np.linalg.norm(nrm, axis=2, keepdims=True), 1e-6)
zmin, zmax = P[:, 2].min(), P[:, 2].max()
ymin, ymax = P[:, 1].min(), P[:, 1].max()
print('rifle z %.3f..%.3f  y %.3f..%.3f  covered %.1f%%' % (zmin, zmax, ymin, ymax, cover.mean() * 100))

base = np.asarray(Image.open(SRC + '/T_WP_Mosin_BaseColor.png').convert('RGB'), np.float64) / 255
ms = np.asarray(Image.open(SRC + '/T_WP_Mosin_MetallicSmoothness.png').convert('RGBA'), np.float64) / 255
sat = (base.max(2) - base.min(2)) / np.maximum(base.max(2), 1e-3)
# 1 on steel, 0 on wood: the metallic map alone left thin steel parts (trigger guard, sling loops) as wood
metal = np.maximum(smooth(0.25, 0.6, ms[..., 0]), smooth(0.34, 0.18, sat))
wood = 1 - metal
lum = base @ np.array([0.3, 0.59, 0.11])

z = pos[..., 2]; u = (z - zmin) / (zmax - zmin)   # 0 at the butt plate, 1 at the muzzle
# ---- masks laid out on the rifle -------------------------------------------------------------------------
big = fbm(pos, (9, 9, 9), 3)                       # broad uneven staining
fine = fbm(pos, (160, 160, 160), 7, 3)             # pores and grit
grain = fbm(pos, (220, 220, 5), 11, 3)             # streaks along the stock
handle = np.maximum(np.exp(-((u - 0.36) / 0.06) ** 2), np.exp(-((u - 0.58) / 0.09) ** 2))  # stock wrist, fore-end
handle *= 0.6 + 0.4 * fbm(pos, (25, 25, 25), 5)
under = smooth(0.1, 0.8, -nrm[..., 1])             # faces that look down collect dirt
hollow = smooth(0.15, 1.2, -cv)                    # creases and joints
edge = smooth(0.25, 1.4, cv)                       # outward edges and corners
butt = smooth(0.06, 0.0, u)                        # butt end, knocked on the ground
# dents: small spots in rifle space
dent = np.zeros((N, N))
pick = np.argwhere(cover & (wood > 0.6)); pick = pick[rng.choice(len(pick), 140, replace=False)]
for (py, px) in pick:
    c = pos[py, px]; r = rng.uniform(0.0012, 0.0042)
    d = np.linalg.norm((pos - c) * np.array([1, 1, rng.uniform(0.25, 1)]), axis=2)
    dent = np.maximum(dent, smooth(r, r * 0.35, d) * rng.uniform(0.5, 1))
# scratches in the wood: thin lines where a 3D noise field crosses its mid value, kept only in patches
line_a = smooth(0.0045, 0.0, np.abs(fbm(pos, (38, 38, 9), 51, 2) - 0.5))
line_b = smooth(0.0035, 0.0, np.abs(fbm(pos, (14, 60, 22), 57, 2) - 0.5))
# only short pieces of those lines survive, so they read as separate scratches and not as contour lines
gouge = np.clip(line_a * smooth(0.55, 0.7, fbm(pos, (55, 55, 55), 61, 2)) + line_b * smooth(0.55, 0.7, fbm(pos, (48, 48, 48), 67, 2)), 0, 1)
blotch = smooth(0.52, 0.75, fbm(pos, (17, 17, 17), 71))   # old oil and dirt stains
# scratches on steel: thin streaks across the bluing
scr = smooth(0.74, 0.8, fbm(pos, (600, 600, 30), 23, 2)) * smooth(0.45, 0.7, fbm(pos, (30, 30, 30), 29))

# ---- wood ------------------------------------------------------------------------------------------------
# keep the delivered grain as luminance detail only; the colour becomes an old oil-darkened shellac
detail = lum / np.maximum(np.asarray(Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6)), np.float64) / 255, 0.02)
detail = np.clip(detail, 0.7, 1.3)
dark, mid, worn = np.array([0.040, 0.022, 0.014]), np.array([0.150, 0.066, 0.030]), np.array([0.42, 0.30, 0.19])
w = mid[None, None] * (0.55 + 0.95 * big)[..., None]
w = mix(w, dark[None, None] * np.ones_like(w), 0.8 * smooth(0.42, 0.7, grain))   # dark grain streaks
w = mix(w, dark[None, None] * np.ones_like(w), 0.7 * handle)                                   # sweat and oil where it is held
w = mix(w, worn[None, None] * np.ones_like(w), 0.5 * edge * smooth(0.35, 0.65, fbm(pos, (60, 60, 60), 77)))   # finish, in broken patches rubbed off the corners
w = mix(w, worn[None, None] * np.ones_like(w), 0.6 * butt * (0.4 + 0.6 * fine))                 # scuffed butt
w = mix(w, dark[None, None] * np.ones_like(w), 0.7 * blotch)                                   # stains
w = mix(w, dark[None, None] * 0.6 * np.ones_like(w), 0.7 * gouge)                             # dirt-filled scratches
rim = smooth(0.0, 0.5, dent) * smooth(1.0, 0.5, dent)
w = mix(w, worn[None, None] * 0.8 * np.ones_like(w), 0.3 * smooth(0.5, 0.9, dent))            # paler wood in the dents
w = mix(w, dark[None, None] * np.ones_like(w), 0.55 * rim)                                       # bruised rim
w *= (1 - 0.6 * np.maximum(under * 0.55, hollow))[..., None]                                    # grime
w *= (0.8 + 0.4 * fine)[..., None] * (0.5 + 0.5 * detail)[..., None]
wood_s = 0.30 + 0.22 * big + 0.25 * handle - 0.15 * np.maximum(hollow, under * 0.5) - 0.2 * dent - 0.15 * butt - 0.2 * gouge - 0.12 * blotch

# ---- steel -----------------------------------------------------------------------------------------------
blue, bare, rust = np.array([0.060, 0.064, 0.072]), np.array([0.36, 0.35, 0.33]), np.array([0.21, 0.105, 0.055])
rubbed = np.clip(0.55 * edge * smooth(0.3, 0.6, fbm(pos, (70, 70, 70), 83)) + 0.35 * smooth(0.55, 0.8, big) * (0.4 + 0.6 * fine) + 0.7 * scr, 0, 1)
rusty = smooth(0.62, 0.8, fbm(pos, (45, 45, 45), 41)) * (0.5 + 0.5 * np.maximum(hollow, under))
# the delivered steel keeps its painted detail; it is only pulled down and toward a cold blue-black
lin = np.power(base, 2.2)
s = lin * np.array([0.42, 0.44, 0.50])[None, None] * (0.85 + 0.3 * fine)[..., None] + blue[None, None] * 0.35
s = mix(s, bare[None, None] * (0.8 + 0.4 * fine)[..., None], rubbed)
s = mix(s, rust[None, None] * (0.7 + 0.6 * fine)[..., None], 0.75 * rusty)
s *= (1 - 0.4 * hollow)[..., None]
steel_s = ms[..., 3] * 0.8 + 0.05 * big + 0.2 * rubbed - 0.3 * rusty - 0.12 * hollow
steel_m = 1.0 - 0.75 * rusty - 0.25 * hollow

out = np.clip(w * wood[..., None] + s * metal[..., None], 0, 1)
out = np.power(out, 1 / 2.2)  # the masks above are in linear light, the texture is sRGB
smoothness = np.clip(wood_s * wood + steel_s * metal, 0.03, 0.9)
metallic = np.clip(metal * steel_m, 0, 1)

Image.fromarray((out * 255 + 0.5).astype(np.uint8)).save(DST + '/T_WP_Mosin_BaseColor.png')
mo = np.dstack([metallic, metallic, metallic, smoothness])
Image.fromarray((mo * 255 + 0.5).astype(np.uint8), 'RGBA').save(DST + '/T_WP_Mosin_MetallicSmoothness.png')
print('wood mean', (out * wood[..., None]).sum((0, 1)) / wood.sum(), 'steel mean', (out * metal[..., None]).sum((0, 1)) / metal.sum())
