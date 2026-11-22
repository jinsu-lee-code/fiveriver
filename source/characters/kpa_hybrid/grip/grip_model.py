# The hands as the game skins them, in the hand bone's own frame (metres), and the rifle's distance field.
import numpy as np

D = 'C:/sk_assets/capture/grip/'
FINGERS = ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky')


def qmat(q):
    x, y, z, w = np.asarray(q) / np.linalg.norm(q)
    return np.array(((1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
                     (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
                     (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y))))


def matq(R):
    t = R[0, 0] + R[1, 1] + R[2, 2]
    if t > 0:
        s = 2 * np.sqrt(t + 1)
        q = ((R[2, 1] - R[1, 2]) / s, (R[0, 2] - R[2, 0]) / s, (R[1, 0] - R[0, 1]) / s, s / 4)
    else:
        i = int(np.argmax(np.diag(R))); j = (i + 1) % 3; k = (i + 2) % 3
        s = 2 * np.sqrt(1 + R[i, i] - R[j, j] - R[k, k])
        q = [0, 0, 0, (R[k, j] - R[j, k]) / s]
        q[i] = s / 4; q[j] = (R[j, i] + R[i, j]) / s; q[k] = (R[k, i] + R[i, k]) / s
    q = np.array(q)
    return q / np.linalg.norm(q)


def rot(axis, a):
    x, y, z = axis
    c, s = np.cos(a), np.sin(a); C = 1 - c
    return np.array(((c + x * x * C, x * y * C - z * s, x * z * C + y * s),
                     (y * x * C + z * s, c + y * y * C, y * z * C - x * s),
                     (z * x * C - y * s, z * y * C + x * s, c + z * z * C)))


def unit(v):
    return v / np.linalg.norm(v)


_bones, _V, _I, _W, _F = [], [], [], [], []
for _line in open(D + 'static.txt', encoding='utf-8-sig'):
    _p = _line.split()
    if _p[0] == 'BONE':
        f = np.array(_p[4:], float)
        _bones.append(dict(name=_p[2], parent=int(_p[3]), pos=f[0:3], rot=qmat(f[3:7]),
                           scale=f[10:13].mean(), bind=f[13:29].reshape(4, 4)))
    elif _p[0] == 'V':
        _V.append(_p[1:4]); _I.append(_p[4:8]); _W.append(_p[8:12])
    elif _p[0] == 'F':
        _F.append(_p[1:4])
V, I, W, F = np.array(_V, float), np.array(_I, int), np.array(_W, float), np.array(_F, int)


class Hand:
    def __init__(self, side):
        self.side = side
        at = {b['name'].split(':')[-1]: i for i, b in enumerate(_bones)}
        h = at[side + 'Hand']
        self.ids = [h] + [at['%sHand%s%d' % (side, n, k)] for n in FINGERS for k in (1, 2, 3)]
        self.tips = [at['%sHand%s4' % (side, n)] for n in FINGERS]
        local = {b: i for i, b in enumerate(self.ids)}
        s = _bones[h]['scale']
        self.lpos = np.array([_bones[b]['pos'] * s for b in self.ids[1:]]).reshape(5, 3, 3)
        self.tpos = np.array([_bones[b]['pos'] * s for b in self.tips])
        self.lrot = np.array([_bones[b]['rot'] for b in self.ids[1:]]).reshape(5, 3, 3, 3)

        other = 'Left' if side == 'Right' else 'Right'
        names = np.array([b['name'].split(':')[-1] for b in _bones])
        mine = np.array([n.startswith(side) and ('Hand' in n or 'ForeArm' in n) for n in names])
        self.vids = np.nonzero(mine[I[:, 0]])[0]
        I0, W0 = I[self.vids], W[self.vids]
        self.idx = np.vectorize(lambda b: local.get(b, 0))(I0)
        self.wgt = W0 / W0.sum(1, keepdims=True)
        self.in_hand = (W0 * np.isin(I0, self.ids)).sum(1)      # the rest is the stub inside the sleeve
        hom = np.c_[V[self.vids], np.ones(len(self.vids))]
        self.local = np.empty((len(self.vids), 4, 3))
        for k in range(4):
            for b in np.unique(self.idx[:, k]):
                sel = self.idx[:, k] == b
                self.local[sel, k] = (hom[sel] @ _bones[self.ids[b]]['bind'].T)[:, :3] * s
        new = np.full(len(V), -1); new[self.vids] = np.arange(len(self.vids))
        f = new[F]; self.faces = f[(f >= 0).all(1)]

        R, T, J = self.fk(None)
        self.rest_R, self.rest_J = R, J
        self.rest = self.skin(R, T)
        inv = np.linalg.inv(_bones[h]['bind'])
        bind = [((_bones[h]['bind'] @ np.linalg.inv(_bones[b]['bind']))[:3, 3]) * s for b in self.ids]
        self.fk_error = float(np.abs(np.array(bind) - T).max())
        tri = self.rest[self.faces]
        fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        nn = np.zeros_like(self.rest)
        for k in range(3):
            np.add.at(nn, self.faces[:, k], fn)
        self.rest_n = nn / np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-12)
        if (self.rest_n * (self.rest - self.rest.mean(0))).sum() < 0:
            self.rest_n *= -1

        # the hand's own directions: along it, out of the palm, toward the thumb
        k = J[1:, 0]
        along = unit(k.mean(0))
        n = unit(np.cross(k[0], k[3]))
        if n @ (J[0, 3] - k.mean(0)) < 0:
            n = -n
        self.along, self.palm = along, unit(n - along * (n @ along))
        side_ = k[0] - k[3]
        self.radial = unit(side_ - along * (side_ @ along) - self.palm * (side_ @ self.palm))
        # each joint's axes, in that bone's own frame: flexion, spread, roll
        self.axes = np.empty((5, 3, 3, 3))
        toward = unit(-self.radial + 0.3 * self.palm)
        for fi in range(5):
            for j in range(3):
                d = unit(J[fi, j + 1] - J[fi, j])
                flex = unit(np.cross(d, toward if fi == 0 else self.palm))
                spread = unit(np.cross(flex, d))
                Rl = R[1 + 3 * fi + j].T
                self.axes[fi, j] = (Rl @ flex, Rl @ spread, Rl @ d)

    def fk(self, a):
        """a: thumb (flex, spread, roll, flex2, flex3), each finger (flex, spread, flex2, flex3), radians"""
        R = np.empty((16, 3, 3)); T = np.empty((16, 3)); J = np.empty((5, 4, 3))
        R[0] = np.eye(3); T[0] = 0
        for fi in range(5):
            Rp, Tp = R[0], T[0]
            for j in range(3):
                Rl = self.lrot[fi, j]
                if a is not None:
                    ax = self.axes[fi, j]
                    if fi == 0:
                        if j == 0:
                            Rl = Rl @ rot(ax[1], a[1]) @ rot(ax[0], a[0]) @ rot(ax[2], a[2])
                        else:
                            Rl = Rl @ rot(ax[0], a[2 + j])
                    else:
                        o = 5 + 4 * (fi - 1)
                        if j == 0:
                            Rl = Rl @ rot(ax[1], a[o + 1]) @ rot(ax[0], a[o])
                        else:
                            Rl = Rl @ rot(ax[0], a[o + 1 + j])
                T1 = Tp + Rp @ self.lpos[fi, j]; R1 = Rp @ Rl
                R[1 + 3 * fi + j] = R1; T[1 + 3 * fi + j] = T1; J[fi, j] = T1
                Rp, Tp = R1, T1
            J[fi, 3] = Tp + Rp @ self.tpos[fi]
        return R, T, J

    def skin(self, R, T, sel=slice(None)):
        p = np.einsum('nkij,nkj->nki', R[self.idx[sel]], self.local[sel]) + T[self.idx[sel]]
        return (p * self.wgt[sel][..., None]).sum(1)


class Field:
    def __init__(self):
        z = np.load(D + 'sdf.npz')
        self.d, self.lo, self.h = z['d'].astype(float), z['lo'], float(z['h'])
        self.n = np.array(self.d.shape)

    def __call__(self, P):
        q0 = (P - self.lo) / self.h
        q = np.clip(q0, 0, self.n - 1.001)
        out = np.linalg.norm(q0 - q, axis=1) * self.h
        i = q.astype(int); f = q - i
        x, y, z = i.T; fx, fy, fz = f.T; d = self.d
        a = d[x, y, z] * (1 - fx) + d[x + 1, y, z] * fx
        b = d[x, y + 1, z] * (1 - fx) + d[x + 1, y + 1, z] * fx
        c = d[x, y, z + 1] * (1 - fx) + d[x + 1, y, z + 1] * fx
        e = d[x, y + 1, z + 1] * (1 - fx) + d[x + 1, y + 1, z + 1] * fx
        return (a * (1 - fy) + b * fy) * (1 - fz) + (c * (1 - fy) + e * fy) * fz + out
