# T-34 running gear, measured straight from the FBX in Blender (2026-11-13, re-measured for the 11-10 lighter model).
# Writes what TankRunningGear.cs (Assets/_Project/Scripts/Combat/TankRunningGear.cs) needs:
# Resources/Vehicles/T34_RunningGear.json  (same schema as gear.py) + side-view classification pictures.
#
#   blender -b --factory-startup -P gear_blender.py -- <SM_VH_T34_85.fbx> <out_dir> [<json_out>]
#   (or with Blender's own python on dumps made earlier: python gear_blender.py --npz <prefix> <out_dir> [<json_out>])
#
# Tank-body space (what the game uses): x sideways, y up, z forward, metres. From Blender world coordinates
#   Unity (x, y, z) = (-bx, bz, -by)
# (the FBX is exported with axis_forward=-Z, axis_up=Y and Unity flips X on import; the gun points to Blender -Y = Unity +Z;
# TrackL sits at Blender +x = Unity -x, so TrackL is read with sign -1 like the game does). Checked 2026-11-13: gear.py's
# method on the old 11-05 model dumped this way reproduces the committed loops to 0.03 mm (the z-mirrored mapping is 2 cm
# off on average, 18 cm at worst).
#
# Differences from gear.py (the 11-10 model is collapse-decimated to 10k triangles per track, one welded piece):
#   * the loop is found from points sampled evenly over the triangles (area-weighted), not from vertices: the outer slice
#     (x > 1.40) has too few vertices left to take percentiles per degree
#   * wheel centres are fitted (outer tyre edge of the road wheels, rim ring of sprocket and idler) on both tracks and
#     averaged (the JSON has one wheel list for both sides)
#   * the radius at which each wheel's turning disc stops is where the fewest triangles cross it (decimated triangles
#     span 6-7 cm; a cut through the middle of the tyre face, as 0.465 was, tears them into jagged pieces)
#   * belt band thresholds are checked triangle by triangle against the vertices the game will move
import sys, os, json
import numpy as np

# ---------------------------------------------------------------- settings
APPROX = [(-2.24, 0.80), (-1.646, 0.568), (-0.672, 0.568), (0.334, 0.570), (1.318, 0.569), (2.13, 0.765)]  # start points
FIT_RING = [(0.17, 0.27, 1.2)] + [(0.47, 0.53, 1.0)] * 4 + [(0.24, 0.30, 1.2)]  # annulus (r0, r1) and min x of fit points
TURN_WINDOW = [(0.19, 0.26)] + [(0.40, 0.45)] * 4 + [(0.22, 0.28)]             # where to look for the cleanest cut
SEAM = 0.012            # TankRunningGear.SeamOverlap
WHEEL_INNER = 1.07      # x (outer side positive) beyond which wheel discs turn: between the guide horn and the inner tyre
BELT_INNER = 0.97       # x beyond which the belt band applies: the belt plates span 1.00 .. 1.49
BELT_HALF = 0.060       # outward from the centre line: plates reach +0.035..0.04 on the runs, +0.05..0.06 round the ends
BELT_INSIDE = 0.045     # inward: the plates' inner face is at -0.03..-0.04, the road-wheel tyres start below -0.05
NS = 256

def spike(y, z):        # thin flap above the front of the track (static, kept out of the loop measurement)
    return (z > 2.2) & (y > 1.19 - 1.38 * (z - 2.3))

# ---------------------------------------------------------------- helpers
def resample(p, n):
    q = np.vstack([p, p[:1]]); seg = np.hypot(*(q[1:] - q[:-1]).T); c = np.r_[0, np.cumsum(seg)]
    u = np.linspace(0, c[-1], n, endpoint=False)
    return np.c_[np.interp(u, c, q[:, 0]), np.interp(u, c, q[:, 1])]

def smooth(p, k):
    for _ in range(k): p = (np.roll(p, 1, 0) + 2 * p + np.roll(p, -1, 0)) / 4
    return p

def project(p, z, y):
    """nearest point on the closed polyline p: arc length s, signed distance n (+ outward), along offset, length"""
    q = np.vstack([p, p[:1]]); d = q[1:] - q[:-1]; L = np.hypot(d[:, 0], d[:, 1]); c = np.r_[0, np.cumsum(L)]
    best = np.full(len(z), 1e9); s = np.zeros(len(z)); nn = np.zeros(len(z)); al = np.zeros(len(z))
    for i in range(len(p)):
        rz = z - q[i, 0]; ry = y - q[i, 1]
        u0 = (rz * d[i, 0] + ry * d[i, 1]) / (L[i] ** 2); u = np.clip(u0, 0, 1)
        ez = rz - u * d[i, 0]; ey = ry - u * d[i, 1]; dist = np.hypot(ez, ey)
        k = dist < best
        best[k] = dist[k]; s[k] = c[i] + u[k] * L[i]
        nn[k] = (ez[k] * d[i, 1] - ey[k] * d[i, 0]) / L[i]
        al[k] = (u0[k] - u[k]) * L[i]
    return s, nn, al, c[-1]

def components(nv, t):
    """connected piece of every triangle (union-find over shared vertices)"""
    par = np.arange(nv)
    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]; a = par[a]
        return a
    for a, b, c in t:
        ra = find(a); par[find(b)] = ra; par[find(c)] = ra
    root = np.array([find(i) for i in range(nv)])
    return root[t[:, 0]]

def sample_surface(x, y, z, t, per_m2=20000, seed=1, min_piece=30):
    """area-weighted points on the triangles; loose pieces of fewer than min_piece triangles (decimation debris
    floating off the track) are left out so they cannot pull the centre line"""
    comp = components(len(x), t)
    u, cnt = np.unique(comp, return_counts=True)
    t = t[np.isin(comp, u[cnt >= min_piece])]
    rng = np.random.default_rng(seed)
    P = np.c_[x, y, z]
    a, b, c = P[t[:, 0]], P[t[:, 1]], P[t[:, 2]]
    area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    cnt = rng.poisson(area * per_m2) + 1
    idx = np.repeat(np.arange(len(t)), cnt)
    r1 = np.sqrt(rng.random(len(idx))); r2 = rng.random(len(idx))
    Q = a[idx] * (1 - r1)[:, None] + b[idx] * (r1 * (1 - r2))[:, None] + c[idx] * (r1 * r2)[:, None]
    return Q[:, 0], Q[:, 1], Q[:, 2]

def fit_circle(z, y, it=4):
    k = np.ones(len(z), bool)
    for _ in range(it):
        A = np.c_[2 * z[k], 2 * y[k], np.ones(k.sum())]; b = z[k] ** 2 + y[k] ** 2
        (cz, cy, c), *_ = np.linalg.lstsq(A, b, rcond=None)
        r = np.sqrt(c + cz * cz + cy * cy)
        res = np.abs(np.hypot(z - cz, y - cy) - r)
        k = res < 2.5 * (np.median(res[k]) + 1e-4)
    return np.array([cz, cy, r]), res[k].std()

def area2(z, y, t):
    a, b, c = t[:, 0], t[:, 1], t[:, 2]
    return 0.5 * np.abs((z[b] - z[a]) * (y[c] - y[a]) - (z[c] - z[a]) * (y[b] - y[a]))

# ---------------------------------------------------------------- input
def read_tracks(argv):
    """{name: (x with the side's sign so outward is +, y, z, triangles)} in tank-body space"""
    out = {}
    if argv[0] == '--npz':
        for n in ('TrackL', 'TrackR'):
            d = np.load(argv[1] + '_' + n + '.npz'); v = d['v']
            out[n] = (v[:, 0] * (-1.0 if n == 'TrackL' else 1.0), v[:, 1], v[:, 2], d['t'])
        return out, argv[2:]
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=argv[0])
    for n in ('TrackL', 'TrackR'):
        o = bpy.data.objects[n]; me = o.data; me.calc_loop_triangles()
        co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
        M = np.array(o.matrix_world); w = co @ M[:3, :3].T + M[:3, 3]
        ux, uy, uz = -w[:, 0], w[:, 2], -w[:, 1]
        t = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', t)
        out[n] = (ux * (-1.0 if n == 'TrackL' else 1.0), uy, uz, t.reshape(-1, 3))
    return out, argv[1:]

# ---------------------------------------------------------------- measuring
def fit_wheels(tracks, loops):
    W = []; report = []
    for i in range(6):
        per = []
        for n, (x, y, z, t) in tracks.items():
            nn = project(loops[n], z, y)[1]
            wz, wy = APPROX[i]; r0, r1, xm = FIT_RING[i]
            d = np.hypot(z - wz, y - wy)
            m = (d > r0) & (d < r1) & (x > xm) & (np.abs(nn) > 0.06)
            c, sd = fit_circle(z[m], y[m]); per.append(c); report.append((n, i, c, sd, m.sum()))
        W.append(np.mean(per, 0))
    return np.array(W), report

def loop_of(x, y, z, wheels):
    m = (x > 1.40) & ~spike(y, z)
    for (wz, wy, R) in wheels: m &= np.hypot(z - wz, y - wy) > R + SEAM
    x, y, z = x[m], y[m], z[m]
    C = (0.0, 0.6); ang = np.arctan2(y - C[1], z - C[0]); rad = np.hypot(z - C[0], y - C[1])
    NB = 360; b = ((ang + np.pi) / (2 * np.pi) * NB).astype(int) % NB
    r = np.full(NB, np.nan)
    for i in range(NB):
        k = b == i
        if k.sum() < 6: continue
        ro = np.percentile(rad[k], 95); kk = k & (rad > ro - 0.07)
        r[i] = (np.percentile(rad[kk], 5) + ro) / 2
    idx = np.arange(NB); ok = ~np.isnan(r)
    r = np.interp(idx, np.r_[idx[ok] - NB, idx[ok], idx[ok] + NB], np.r_[r[ok], r[ok], r[ok]])
    a = -np.pi + (idx + 0.5) / NB * 2 * np.pi
    p = np.c_[C[0] + r * np.cos(a), C[1] + r * np.sin(a)]          # counter-clockwise seen with z right, y up
    p = smooth(resample(p, NS), 3)
    for it in range(3):
        s, n, _, L = project(p, z, y)
        k = np.abs(n) < 0.07
        bins = (s[k] / L * NS).astype(int) % NS
        off = np.zeros(NS)
        for i in range(NS):
            v = n[k][bins == i]
            if len(v) >= 6: off[i] = (np.percentile(v, 5) + np.percentile(v, 95)) / 2
        off = smooth(off[:, None], 2)[:, 0]
        tg = np.roll(p, -1, 0) - np.roll(p, 1, 0); tg /= np.hypot(tg[:, 0], tg[:, 1])[:, None]
        p = p + np.c_[tg[:, 1], -tg[:, 0]] * off[:, None]
        p = smooth(resample(p, NS), 2)
    return unkink(p)

def turn_angles(p):
    a = p - np.roll(p, 1, 0); b = np.roll(p, -1, 0) - p
    return np.degrees(np.abs(np.arctan2(a[:, 0] * b[:, 1] - a[:, 1] * b[:, 0], (a * b).sum(1))))

def unkink(p, limit=15.0):
    """smooth only where the centre line turns sharper than the tightest real bend (the sprocket, ~8 degrees per
    sample): a corner in the decimated belt can otherwise leave a small cusp that folds the belt there"""
    for _ in range(400):
        ang = turn_angles(p)
        bad = np.where(ang > limit)[0]
        if len(bad) == 0: break
        w = np.zeros(len(p))
        for i in bad: w[(np.arange(-3, 4) + i) % len(p)] = 0.5
        p = p + w[:, None] * ((np.roll(p, 1, 0) + np.roll(p, -1, 0)) / 2 - p)
        p = resample(p, NS)
    return p

def straddle_cost(tracks, loops, wz, wy, r):
    cost = 0.0
    for n, (x, y, z, t) in tracks.items():
        cx = x[t].mean(1); dt = np.hypot(z - wz, y - wy)[t]
        tn = project(loops[n], z[t].mean(1), y[t].mean(1))[1]
        cand = (cx > WHEEL_INNER) & ~((tn < BELT_HALF) & (tn > -BELT_INSIDE))
        st = cand & (dt.min(1) < r - SEAM) & (dt.max(1) > r + SEAM)
        cost += area2(z, y, t)[st].sum()
    return cost

def classify(x, y, z, t, p, wheels):
    """the game's rule (TankRunningGear.Add): belt first, then wheel discs with the seam drawn by both"""
    cx = x[t].mean(1); cy = y[t].mean(1); cz = z[t].mean(1)
    g = np.zeros(len(t), int); seam = np.zeros(len(t), bool)
    s, n, al, L = project(p, cz, cy)
    belt = (cx > BELT_INNER) & (n < BELT_HALF) & (n > -BELT_INSIDE) & (np.abs(al) < BELT_HALF)
    g[belt] = 7
    for i, (wz, wy, r, _) in enumerate(wheels):
        d = np.hypot(cz - wz, cy - wy)
        k = (g == 0) & (cx > WHEEL_INNER) & (d < r + SEAM)
        seam |= k & (d > r - SEAM)
        g[k] = i + 1
    return g, seam

def problems(x, y, z, t, p, wheels, g, seam):
    """triangles the game will show torn: (kind, mask)"""
    vs, vn, val, L = project(p, z, y)
    out = {}
    tn = vn[t]; ta = val[t]
    b = g == 7
    out['belt_vertex_off_band'] = b & ((tn.min(1) < -(BELT_INSIDE + 0.03)) | (tn.max(1) > BELT_HALF + 0.03) | (np.abs(ta).max(1) > 0.03))
    # a vertex that lands on another stretch of the loop than its triangle (s jumps more than the triangle is long)
    sl = vs[t]; span = sl.max(1) - sl.min(1); span = np.minimum(span, L - span)
    P = np.c_[z, y]; ext = np.max([np.hypot(*(P[t[:, i]] - P[t[:, j]]).T) for i, j in ((0, 1), (1, 2), (2, 0))], axis=0)
    out['belt_vertex_far_round_loop'] = b & (span > 1.3 * ext + 0.03)
    inband = (x[t].min(1) > BELT_INNER) & (tn.max(1) < BELT_HALF) & (tn.min(1) > -BELT_INSIDE)
    out['belt_left_static'] = (g != 7) & inband
    prot = np.zeros(len(t), bool); hole = np.zeros(len(t), bool)
    for i, (wz, wy, r, _) in enumerate(wheels):
        dt = np.hypot(z - wz, y - wy)[t]
        prot |= (g == i + 1) & (dt.max(1) > r + SEAM + 0.015)
        hole |= (g == 0) & ~seam & (x[t].mean(1) > WHEEL_INNER) & (dt.min(1) < r - SEAM - 0.015) & (dt.mean(1) < r + 0.08)
    out['wheel_piece_sticks_out'] = prot
    out['static_piece_reaches_into_disc'] = hole
    return out

# ---------------------------------------------------------------- pictures
def png(path, pix):
    import zlib, struct
    h, w, _ = pix.shape
    raw = b''.join(b'\x00' + pix[r].astype(np.uint8).tobytes() for r in range(h))
    def chunk(tag, data): return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
                           + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b''))

def raster(pix, P, tris, cols, order):
    H, W, _ = pix.shape
    for k in order:
        a, b, c = P[tris[k]]
        x0 = int(max(0, np.floor(min(a[0], b[0], c[0])))); x1 = int(min(W - 1, np.ceil(max(a[0], b[0], c[0]))))
        y0 = int(max(0, np.floor(min(a[1], b[1], c[1])))); y1 = int(min(H - 1, np.ceil(max(a[1], b[1], c[1]))))
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        def e(p, q): return (q[0] - p[0]) * (ys - p[1]) - (q[1] - p[1]) * (xs - p[0])
        e0, e1, e2 = e(a, b), e(b, c), e(c, a)
        m = ((e0 >= 0) & (e1 >= 0) & (e2 >= 0)) | ((e0 <= 0) & (e1 <= 0) & (e2 <= 0))
        sub = pix[y0:y1 + 1, x0:x1 + 1]; sub[m] = cols[k]

def polyline(pix, P, col, closed=True):
    H, W, _ = pix.shape
    for i in range(len(P) if closed else len(P) - 1):
        a, b = P[i], P[(i + 1) % len(P)]
        m = int(max(abs(b[0] - a[0]), abs(b[1] - a[1]))) + 1
        u = np.linspace(0, 1, m + 1)
        xs = (a[0] + (b[0] - a[0]) * u).astype(int); ys = (a[1] + (b[1] - a[1]) * u).astype(int)
        k = (xs >= 0) & (xs < W) & (ys >= 0) & (ys < H)
        pix[ys[k], xs[k]] = col

COLS = np.array([(90, 90, 90)] + [(60, 200, 60), (230, 160, 40)] * 3 + [(70, 140, 255)])

def picture(path, x, y, z, t, g, seam, p, wheels, bad=None, view=None, S=400):
    z0, z1, y0, y1 = view or (-2.65, 2.65, 0.0, 1.30)
    W = int((z1 - z0) * S); H = int((y1 - y0) * S)
    pix = np.zeros((H, W, 3)); pix[:] = 25
    P = np.c_[(z - z0) * S, H - 1 - (y - y0) * S]
    cols = COLS[g].astype(float)
    cols[seam] = cols[seam] * 0.6 + np.array([255, 255, 255]) * 0.4
    if bad is not None: cols[bad] = (255, 0, 255)
    # inner side first so the outer faces end up on top, belt last
    order = np.lexsort((x[t].mean(1), g == 7))
    raster(pix, P, t, cols, order)
    for (wz, wy, r, rr) in wheels:
        a = np.linspace(0, 2 * np.pi, 720)
        polyline(pix, np.c_[(wz + r * np.cos(a) - z0) * S, H - 1 - (wy + r * np.sin(a) - y0) * S], (255, 255, 255))
    polyline(pix, np.c_[(p[:, 0] - z0) * S, H - 1 - (p[:, 1] - y0) * S], (255, 0, 0))
    png(path, pix)

# ---------------------------------------------------------------- main
def evaluate(tracks, J, outdir):
    """only sort the triangles with a given JSON (no measuring) and draw the pictures"""
    global BELT_HALF, BELT_INSIDE, WHEEL_INNER, BELT_INNER
    BELT_HALF, BELT_INSIDE, WHEEL_INNER, BELT_INNER = J['beltHalf'], J['beltInside'], J['wheelInner'], J['beltInner']
    wheels = [tuple(w) for w in np.array(J['wheels']).reshape(-1, 4)]
    for n, (x, y, z, t) in tracks.items():
        p = np.array(J[n]['loop']).reshape(-1, 2)
        g, seam = classify(x, y, z, t, p, wheels)
        bad = problems(x, y, z, t, p, wheels, g, seam)
        print('TRACK %s verts %d tris %d: static %d wheels %s belt %d  seam(both) %d' % (n, len(x), len(t), (g == 0).sum(), [int((g == i).sum()) for i in range(1, 7)], (g == 7).sum(), seam.sum()))
        allbad = np.zeros(len(t), bool)
        for k, m in bad.items():
            print('   %-32s %4d tris  %.1f cm2' % (k, m.sum(), area2(z, y, t)[m].sum() * 1e4)); allbad |= m
        picture(os.path.join(outdir, n + '_class.png'), x, y, z, t, g, seam, p, wheels)
        picture(os.path.join(outdir, n + '_problems.png'), x, y, z, t, g, seam, p, wheels, bad=allbad)

def main(argv):
    tracks, rest = read_tracks(argv)
    outdir = rest[0]; json_out = rest[1] if len(rest) > 1 else None
    centres = None   # centres=<json>: the wheels were rounded about these axles (round_wheels.py), keep them exactly
    for extra in rest[2:]:
        if extra.startswith('centres='):
            centres = np.array(json.load(open(extra[8:]))['wheels']).reshape(-1, 4)[:, :2]
    os.makedirs(outdir, exist_ok=True)
    if json_out and json_out.startswith('eval='):
        return evaluate(tracks, json.load(open(json_out[5:])), outdir)
    samples = {n: sample_surface(x, y, z, t) for n, (x, y, z, t) in tracks.items()}
    # 1. loop with the starting wheels, 2. wheels fitted away from that loop, 3. loop again with the fitted wheels
    start = np.array([(a, b, r) for (a, b), r in zip(APPROX, (0.215, 0.509, 0.509, 0.509, 0.509, 0.285))])
    loops = {n: loop_of(*samples[n], start) for n in tracks}
    W, rep = fit_wheels(tracks, loops)
    for n, i, c, sd, cnt in rep: print('FIT %s wheel %d centre z %.4f y %.4f R %.4f  sd %.4f  points %d' % (n, i, c[0], c[1], c[2], sd, cnt))
    if centres is not None:
        for i in range(6):
            print('CENTRE wheel %d fitted (%.4f, %.4f), rounded about (%.4f, %.4f): %.1f mm apart, the rounding axle is kept'
                  % (i, W[i, 0], W[i, 1], centres[i, 0], centres[i, 1], 1000 * np.hypot(*(W[i, :2] - centres[i]))))
        W[:, :2] = centres
    loops = {n: loop_of(*samples[n], W) for n in tracks}
    # rolling radius: road wheels roll on the belt with their tyre; sprocket and idler carry the belt's centre line
    wheels = []
    for i, (wz, wy, R) in enumerate(W):
        lo, hi = TURN_WINDOW[i]
        rs = np.arange(lo, hi + 1e-9, 0.0025)
        # after rounding, the rim rings sit on exact circles: cutting there splits almost nothing, so try those first
        ring = []
        for n, (x, y, z, t) in tracks.items():
            d = np.hypot(z - wz, y - wy)[x > WHEEL_INNER]
            h, e = np.histogram(d, bins=np.arange(lo, hi + 1e-9, 0.0005))
            ring += [round(float(e[k] + 0.00025), 5) for k in np.where(h >= 8)[0]]
        if ring:
            exact = []
            for rc in sorted(set(ring)):
                vals = np.concatenate([np.hypot(z - wz, y - wy)[(x > WHEEL_INNER) & (np.abs(np.hypot(z - wz, y - wy) - rc) < 0.00025)]
                                       for (x, y, z, t) in tracks.values()])
                exact.append(float(np.median(vals)))
            rs = np.array(sorted(set(round(v, 5) for v in exact)))
        cost = [straddle_cost(tracks, loops, wz, wy, r) for r in rs]
        r = float(rs[int(np.argmin(cost))])
        if i in (0, 5):
            roll = np.mean([np.median(np.sort(np.hypot(loops[n][:, 0] - wz, loops[n][:, 1] - wy))[:24]) for n in tracks])
        else:
            roll = R
        print('WHEEL %d centre (%.4f, %.4f) fitted R %.4f  turn %.4f (crossing %.1f cm2, at old 0.465-style middle %.1f)  roll %.4f'
              % (i, wz, wy, R, r, min(cost) * 1e4, cost[len(cost) // 2] * 1e4, roll))
        # The game turns every triangle whose centre is closer than radius + SEAM (and keeps a still copy of those
        # beyond radius - SEAM). The mesh is cut exactly at r (cut_tracks.py), so the JSON gets r - SEAM: then the
        # turning part is exactly what lies inside the cut, and nothing outside it turns.
        wheels.append((round(float(wz), 4), round(float(wy), 4), round(r - SEAM, 4), round(float(roll), 4)))
    out = {}
    for n, (x, y, z, t) in tracks.items():
        p = loops[n]
        L = project(p, p[:1, 0], p[:1, 1])[3]
        g, seam = classify(x, y, z, t, p, wheels)
        bad = problems(x, y, z, t, p, wheels, g, seam)
        print('LOOP %s sharpest turn %.1f deg per sample' % (n, turn_angles(p).max()))
        print('TRACK %s loop length %.4f  tris static %d wheels %s belt %d  seam(both) %d' % (n, L, (g == 0).sum(), [int((g == i).sum()) for i in range(1, 7)], (g == 7).sum(), seam.sum()))
        allbad = np.zeros(len(t), bool)
        for k, m in bad.items():
            print('   %-32s %4d tris  %.1f cm2' % (k, m.sum(), area2(z, y, t)[m].sum() * 1e4)); allbad |= m
        np.save(os.path.join(outdir, n + '_groups.npy'), np.c_[g, seam, allbad])
        picture(os.path.join(outdir, n + '_class.png'), x, y, z, t, g, seam, p, wheels)
        picture(os.path.join(outdir, n + '_problems.png'), x, y, z, t, g, seam, p, wheels, bad=allbad)
        out[n] = {'length': round(float(L), 4), 'loop': [round(float(v), 4) for v in p.flatten()]}
    out['wheels'] = [v for w in wheels for v in w]
    out['beltHalf'] = BELT_HALF; out['beltInside'] = BELT_INSIDE; out['wheelInner'] = WHEEL_INNER; out['beltInner'] = BELT_INNER
    if json_out:
        with open(json_out, 'w') as f: json.dump(out, f)
        print('WROTE', json_out)
    return out

if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    main(argv)
