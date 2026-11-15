# Running gear fit for the five-road-wheel T-34-85 (SM_VH_T34_85_V5, 2026-11-15).
# Unlike gear_refit.py (wheel circles typed in by hand for the old four-wheel model) the wheels are found here:
# the treads of tyres, sprocket and idler are the faces that look sideways along the track, and circles are fitted
# to them by voting. Writes Resources/Vehicles/T34_RunningGear.json and a picture to look at.
#   1. Unity open: unity command script eval --code "<dump_tracks.cs>"   (TrackL/R .bin + .tri in C:/sk_assets/capture/gear)
#   2. python gear_fit_v5.py
import os, json
import numpy as np
from PIL import Image, ImageDraw
import gear_refit as G            # resample / smooth / project / fill (it changes into the capture folder)

TEX = 'C:/team/five_river/Assets/_Project/Art/Models/Vehicles/T34_85_V5/T_VH_T34_V5_BaseColor.png'
OUT = 'C:/team/five_river/Assets/_Project/Art/Resources/Vehicles/T34_RunningGear.json'
NB = 512
BELT_OUT, BELT_IN = 0.055, 0.035      # the belt's band about its centre line: outward (teeth), inward


def load(n, sg):
    a = np.fromfile(n + '.bin', dtype=np.float32).reshape(-1, 5); t = np.fromfile(n + '.tri', dtype=np.int32).reshape(-1, 3)
    P = np.c_[sg * a[:, 0], a[:, 1], a[:, 2]]
    return P, a[:, 3:5], t


def sample(P, t, n=400000):
    e1 = P[t[:, 1]] - P[t[:, 0]]; e2 = P[t[:, 2]] - P[t[:, 0]]; nrm = np.cross(e1, e2); area = 0.5 * np.linalg.norm(nrm, axis=1)
    rng = np.random.default_rng(1); pick = rng.choice(len(t), n, p=area / area.sum()); u = rng.random(n); v = rng.random(n); f = u + v > 1; u[f] = 1 - u[f]; v[f] = 1 - v[f]
    S = P[t[pick, 0]] + e1[pick] * u[:, None] + e2[pick] * v[:, None]
    N = nrm[pick] / np.maximum(area[pick] * 2, 1e-12)[:, None]
    return S, N


def loop_of(S):
    S = S[(S[:, 2] > np.percentile(S[:, 2], 0.2)) & (S[:, 2] < np.percentile(S[:, 2], 99.8))]   # stray scraps beyond the ends
    y, z = S[:, 1], S[:, 2]
    C = (0.0, (y.max() + y.min()) / 2); ang = np.arctan2(y - C[1], z - C[0]); rad = np.hypot(z - C[0], y - C[1])
    nb = 360; b = ((ang + np.pi) / (2 * np.pi) * nb).astype(int) % nb; r = np.full(nb, np.nan)
    for i in range(nb):
        k = b == i
        if k.sum() >= 20: r[i] = np.percentile(rad[k], 90)
    r = G.fill(r)
    # a mud flap or a scrap of fender standing off the belt must not pull the line out: no direction reaches
    # further than its neighbourhood usually does
    near = np.median(np.stack([np.roll(r, k) for k in range(-9, 10)]), axis=0)
    r = np.minimum(r, near * 1.015); a = -np.pi + (np.arange(nb) + 0.5) / nb * 2 * np.pi
    p = np.c_[C[0] + r * np.cos(a), C[1] + r * np.sin(a)]
    p = G.smooth(G.resample(p, NB), 3)
    for _ in range(4):                 # the line settles a fixed distance inside the belt's outer face, stretch by stretch
        s, n, L = G.project(p, z, y)
        k = np.abs(n) < 0.06; bins = np.minimum((s[k] / L * NB).astype(int), NB - 1); off = np.full(NB, np.nan)
        for i in range(NB):
            v = n[k][bins == i]
            if len(v) >= 12: off[i] = np.percentile(v, 85) - 0.030
        off = G.smooth(G.fill(off)[:, None], 3)[:, 0]
        t = np.roll(p, -1, 0) - np.roll(p, 1, 0); t /= np.hypot(t[:, 0], t[:, 1])[:, None]
        p = G.smooth(G.resample(p + np.c_[t[:, 1], -t[:, 0]] * np.clip(off, -0.03, 0.03)[:, None], NB), 1)
    # a T-34's belt has no return rollers: the loop is convex. The hull of the fitted line takes out the small
    # knots the fit ties where a scrap of fender or a tooth stands off the belt
    q = p[np.lexsort((p[:, 1], p[:, 0]))]
    def half(points):
        h = []
        for pt in points:
            while len(h) >= 2 and np.cross(h[-1] - h[-2], pt - h[-2]) <= 0: h.pop()
            h.append(pt)
        return h
    lower, upper = half(q), half(q[::-1])
    hull = np.array(lower[:-1] + upper[:-1])                    # counter-clockwise, z right, y up
    return G.resample(G.smooth(G.resample(hull, NB), 2), NB)


def wheels_of(S, N, p):
    y, z = S[:, 1], S[:, 2]
    s, n, L = G.project(p, z, y)
    tread = (np.abs(N[:, 0]) < 0.35) & (n < -BELT_IN - 0.01)          # looks along the track, and is not the belt
    zt, yt = z[tread], y[tread]
    if len(zt) > 60000:
        k = np.random.default_rng(2).choice(len(zt), 60000, replace=False); zt, yt = zt[k], yt[k]
    step = 0.01; z0, y0 = z.min() - 0.1, y.min() - 0.1; nz = int((np.ptp(z) + 0.2) / step) + 1; ny = int((np.ptp(y) + 0.2) / step) + 1
    th = np.linspace(0, 2 * np.pi, 96, endpoint=False)
    zz, yy = np.meshgrid(np.arange(nz), np.arange(ny)); gz = z0 + zz * step; gy = y0 + yy * step
    def votes(r):
        a = np.zeros((ny, nz), np.float32)
        cz = ((zt[:, None] - r * np.cos(th) - z0) / step).astype(int).ravel(); cy = ((yt[:, None] - r * np.sin(th) - y0) / step).astype(int).ravel()
        ok = (cz >= 0) & (cz < nz) & (cy >= 0) & (cy < ny)
        np.add.at(a, (cy[ok], cz[ok]), 1.0)
        return a
    def pick(acc, radii, count, allowed, apart):
        work = acc * allowed[None]; got = []
        for _ in range(count):
            ri, cy, cz = np.unravel_index(work.argmax(), work.shape)
            if work[ri, cy, cz] <= 0: break
            got.append((z0 + cz * step, y0 + cy * step, radii[ri]))
            work[:, np.hypot(zz - cz, yy - cy) * step < apart] = 0
        return got
    # the five road wheels: one size, standing on the lower run
    bottom = np.percentile(p[:, 1], 2) + BELT_IN
    rr = np.arange(0.33, 0.46, 0.005); acc = np.stack([votes(r) for r in rr])
    stand = np.stack([np.abs(gy - (bottom + r)) < 0.05 for r in rr])
    best = max(range(len(rr)), key=lambda k: sum(sorted((acc[k] * stand[k]).ravel())[-5:]))      # the radius whose five best places score most
    road = pick(acc[best:best + 1], rr[best:best + 1], 5, stand[best], 0.62)
    road.sort()
    # sprocket behind the first road wheel, idler ahead of the last: the belt wraps round them, so each is the
    # circle the loop's end follows (their own outlines are spoked and toothed and vote badly)
    def end(sel):
        q = p[sel]; A = np.c_[2 * q[:, 0], 2 * q[:, 1], np.ones(len(q))]
        (cz, cy, c), *_ = np.linalg.lstsq(A, (q ** 2).sum(1), rcond=None)
        return [(cz, cy, np.sqrt(c + cz * cz + cy * cy) - BELT_IN)]
    rear = end(p[:, 0] < p[:, 0].min() + 0.22)
    front = end(p[:, 0] > p[:, 0].max() - 0.22)
    found = rear + road + front
    return [(float(a), float(b_), float(c)) for a, b_, c in found], (zt, yt)


def belt_fill(P, uv, p, sg):
    s, n, L = G.project(p, P[:, 2], P[:, 1])
    k = (n > 0.0) & (n < BELT_OUT)
    base = np.asarray(Image.open(TEX).convert('RGB'), np.float32); H, W = base.shape[:2]
    px = np.clip((uv[k, 0] % 1 * W).astype(int), 8, W - 9); py = np.clip(((1 - uv[k, 1] % 1) * H).astype(int), 8, H - 9)
    usual = np.median(base[py, px], axis=0); best = None
    for i in np.random.default_rng(0).choice(len(px), min(3000, len(px)), replace=False):
        a = base[py[i] - 8:py[i] + 9, px[i] - 8:px[i] + 9].reshape(-1, 3); sc = np.abs(a.mean(0) - usual).sum() + a.std(0).sum()
        if best is None or sc < best[0]: best = (sc, i)
    i = best[1]
    return [round(float((px[i] + 0.5) / W), 5), round(float(1 - (py[i] + 0.5) / H), 5)]


out = {}; per_side = {}
for name, sg in (('TrackL', -1), ('TrackR', 1)):
    P, uv, t = load(name, sg); S, N = sample(P, t)
    p = loop_of(S)
    s, n, L = G.project(p, S[:, 2], S[:, 1])
    band = (n > -BELT_IN) & (n < BELT_OUT)
    wheels, tread = wheels_of(S, N, p)
    print(name, 'loop %.3f m, belt x %.3f..%.3f, wheels found %d:' % (L, S[band, 0].min(), S[band, 0].max(), len(wheels)), [(round(a, 2), round(b, 2), round(c, 3)) for a, b, c in wheels])
    out[name] = {'beltFill': belt_fill(P, uv, p, sg), 'length': round(float(L), 4), 'loop': [round(float(v), 4) for v in p.flatten()]}
    per_side[name] = (S, p, wheels, band)
    W, H = 2600, 620; im = Image.new('RGB', (W, H), (20, 20, 20)); d = ImageDraw.Draw(im)
    def at(zv, yv): return ((zv + 2.9) / 5.8 * W, H - 20 - yv / 1.2 * (H - 40))
    px = im.load()
    for zv, yv, b in zip(S[::4, 2], S[::4, 1], band[::4]):
        a_, b_ = at(zv, yv)
        if 0 <= a_ < W and 0 <= b_ < H: px[int(a_), int(b_)] = (70, 110, 220) if b else (110, 110, 110)
    d.line([at(a, b) for a, b in np.vstack([p, p[:1]])], fill=(255, 60, 60), width=2)
    for a, b, c in wheels:
        x0, y0 = at(a - c, b + c); x1, y1 = at(a + c, b - c); d.ellipse([x0, y0, x1, y1], outline=(90, 255, 90), width=3)
    im.save(name + '_v5.png')
# the two sides are one design: the wheels are the mean of both
nw = min(len(per_side['TrackL'][2]), len(per_side['TrackR'][2]))
flat = []
for i in range(nw):
    a = np.mean([per_side[k][2][i] for k in per_side], axis=0)
    reach = min(np.hypot(per_side[k][1][:, 0] - a[0], per_side[k][1][:, 1] - a[1]).min() for k in per_side) - BELT_IN - 0.004
    flat += [round(float(a[0]), 4), round(float(a[1]), 4), round(float(min(a[2] - 0.012, reach)), 3), round(float(a[2]), 4)]
xs = np.concatenate([per_side[k][0][per_side[k][3], 0] for k in per_side])
x0, x1 = float(np.percentile(xs, 0.5)), float(np.percentile(xs, 99.5))
out.update(wheels=flat, beltHalf=BELT_OUT, beltInside=BELT_IN, wheelInner=round(x0 + 0.005, 3), beltInner=round(x0 - 0.02, 3),
           tyreFrom=0.0, tyreX0=0.0, tyreX1=0.0, coreIn=-0.022, coreOut=0.0, coreX0=round(x0 + 0.01, 3), coreX1=round(x1 - 0.01, 3))
json.dump(out, open(OUT, 'w'))
print('wheels (z, y, disc, rim):', [flat[i:i + 4] for i in range(0, len(flat), 4)])
print('belt x %.3f..%.3f' % (x0, x1), 'wrote', OUT)
