# TankRunningGear.cs re-done in numpy (2026-11-13), for checks outside Unity: the 512-sample loop table, the coarse
# grid + local search projection, the triangle sorting (belt first, then wheel discs with the seam drawn by both) and
# the belt carried round the loop. Track dumps: dump.py (<prefix>_TrackL.npz / _TrackR.npz, tank-body coordinates).
import numpy as np

TABLE = 512
SEAM = 0.012
GZ0, GY0, GC, GW, GH = -2.9, -0.3, 0.05, 116, 36

def table(loop):
    """TankRunningGear.BuildTable: the loop sampled evenly by distance, with central-difference directions"""
    n = len(loop); q = np.vstack([loop, loop[:1]])
    cum = np.r_[0, np.cumsum(np.hypot(*(q[1:] - q[:-1]).T))]
    L = cum[-1]
    s = L * np.arange(TABLE) / TABLE
    seg = np.zeros(TABLE, int); k = 0
    for i in range(TABLE):
        while k < n - 1 and cum[k + 1] < s[i]: k += 1
        seg[i] = k
    u = (s - cum[seg]) / np.maximum(1e-6, cum[seg + 1] - cum[seg])
    a = seg; b = (seg + 1) % n
    Z = np.r_[loop[a, 0] + (loop[b, 0] - loop[a, 0]) * u, 0]; Y = np.r_[loop[a, 1] + (loop[b, 1] - loop[a, 1]) * u, 0]
    Z[TABLE] = Z[0]; Y[TABLE] = Y[0]
    k = np.arange(TABLE + 1); bef = (k + TABLE - 1) % TABLE; aft = (k + 1) % TABLE
    dz = Z[aft] - Z[bef]; dy = Y[aft] - Y[bef]; ln = np.maximum(1e-6, np.hypot(dz, dy))
    return dict(L=L, Z=Z, Y=Y, TZ=dz / ln, TY=dy / ln)

def sample(T, s):
    f = s / T['L'] * TABLE; i = np.minimum(f.astype(int), TABLE - 1); u = f - i
    g = lambda A: A[i] + (A[i + 1] - A[i]) * u
    return g(T['Z']), g(T['Y']), g(T['TZ']), g(T['TY'])

def grid(T):
    """TankRunningGear.NearestGrid"""
    gz, gy = np.meshgrid(np.arange(GW), np.arange(GH))
    cz = GZ0 + (gz.ravel() + 0.5) * GC; cy = GY0 + (gy.ravel() + 0.5) * GC
    d = (T['Z'][None, :TABLE] - cz[:, None]) ** 2 + (T['Y'][None, :TABLE] - cy[:, None]) ** 2
    return np.argmin(d, 1)

def project(T, G, z, y):
    """TankRunningGear.Project: distance round the loop, offset along it, offset out from it"""
    gz = np.clip(((z - GZ0) / GC).astype(int), 0, GW - 1); gy = np.clip(((y - GY0) / GC).astype(int), 0, GH - 1)
    c = G[gy * GW + gz]
    best = np.full(len(z), np.inf); s = np.zeros(len(z))
    for o in range(-6, 7):
        k = (c + o + TABLE) % TABLE
        az, ay = T['Z'][k], T['Y'][k]; bz, by = T['Z'][k + 1] - az, T['Y'][k + 1] - ay
        u = np.clip(((z - az) * bz + (y - ay) * by) / np.maximum(1e-9, bz * bz + by * by), 0, 1)
        d = (z - az - bz * u) ** 2 + (y - ay - by * u) ** 2
        m = d < best; best[m] = d[m]; s[m] = (k[m] + u[m]) * T['L'] / TABLE
    pz, py, tz, ty = sample(T, s)
    return s, (z - pz) * tz + (y - py) * ty, (z - pz) * ty - (y - py) * tz

def load(prefix, name):
    """dump.py output: x with the side's sign (outward +), y, z, triangles"""
    d = np.load(prefix + '_' + name + '.npz'); v = d['v']
    sign = -1.0 if name == 'TrackL' else 1.0
    return v[:, 0] * sign, v[:, 1], v[:, 2], d['t'].astype(np.int64)

def classify(J, T, G, x, y, z, t):
    """TankRunningGear.Add: 0 still, 1..6 wheel discs, 7 belt; seam = also drawn still"""
    W = np.array(J['wheels']).reshape(-1, 4)
    cx, cy, cz = x[t].mean(1), y[t].mean(1), z[t].mean(1)
    g = np.zeros(len(t), int); seam = np.zeros(len(t), bool)
    s, al, ou = project(T, G, cz, cy)
    belt = (cx > J['beltInner']) & (ou < J['beltHalf']) & (ou > -J['beltInside']) & (np.abs(al) < J['beltHalf'])
    g[belt] = 7
    for w, (wz, wy, r, _) in enumerate(W):
        d = np.hypot(cz - wz, cy - wy)
        k = (g == 0) & (cx > J['wheelInner']) & (d < r + SEAM)
        seam |= k & (d > r - SEAM); g[k] = w + 1
    return g, seam

def move(T, place, shift):
    """TankRunningGear.BeltJob: (s, along, out) carried `shift` metres round the loop -> (z, y)"""
    s = (place[:, 0] + shift) % T['L']
    z, y, tz, ty = sample(T, s)
    return z + place[:, 1] * tz + place[:, 2] * ty, y + place[:, 1] * ty - place[:, 2] * tz

def samples(P, t, per_m2, seed=3):
    """area-weighted random points on the triangles"""
    a, b, c = P[t[:, 0]], P[t[:, 1]], P[t[:, 2]]
    area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    rng = np.random.default_rng(seed); cnt = rng.poisson(area * per_m2) + 1; idx = np.repeat(np.arange(len(t)), cnt)
    r1 = np.sqrt(rng.random(len(idx))); r2 = rng.random(len(idx))
    return a[idx] * (1 - r1)[:, None] + b[idx] * (r1 * (1 - r2))[:, None] + c[idx] * (r1 * r2)[:, None]
