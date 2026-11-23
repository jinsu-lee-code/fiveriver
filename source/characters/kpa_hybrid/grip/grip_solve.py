# Solves each hold once, offline: hand place and turn, every finger joint and the elbow's swing together.
#   python grip_solve.py [starts] [grip ...]        -> C:/sk_assets/capture/grip/grips.json, grip_<name>.npz
import sys, os, json, collections
import numpy as np
from grip_model import D, FINGERS, Hand, Field, rot, unit, matq

rad = np.radians
field = Field()
hands = {s: Hand(s) for s in ('Left', 'Right')}

# arms as sampled from the game, in the rifle's space
groups = collections.defaultdict(list)
for line in open(D + 'samples.txt', encoding='utf-8-sig'):
    p = line.split()
    key = {('A_KPA_CrouchRun_Charge', False): 'carry', ('A_KPA_Crouch_Aim', True): 'kneel',
           ('A_KPA_Fire_Stand', True): 'stand'}.get((p[2], float(p[3]) > 0.95))
    if key:
        groups[key].append(np.array(p[5:], float).reshape(-1, 3))
pose = {k: np.mean(v, 0) for k, v in groups.items()}

# targets (rifle space). knuckle: the middle finger's; tips: the bones' ends; at: (finger, joint position, where, tolerance)
SPECS = {
    'RightCarry': dict(side='Right', pose='carry', hint=(0.25, -0.5, -0.8), F=(0.05, -0.8, 0.55), N=(-1, 0, 0),
                       knuckle=(0.036, -0.032, -0.010), straight=(1,), wrist=(20, 12), knuckle_tol=0.035,   # (2026-11-23: 55/26 left the wrist bent 67 degrees on the run)
                       at=[(1, 3.0, (0.031, -0.008, 0.078), 0.006), (0, 3.0, (-0.018, 0.016, -0.018), 0.012),
                           (2, 3.0, (-0.022, -0.030, -0.004), 0.015), (3, 3.0, (-0.022, -0.036, -0.026), 0.015),
                           (4, 3.0, (-0.015, -0.046, -0.046), 0.015)]),
    'RightAim': dict(side='Right', pose='kneel', hint=(0.95, 0.0, -0.2), swing=22, F=(-0.4, -0.6, 0.7), N=(-1, 0, 0),
                     knuckle=(0.036, -0.030, -0.012), straight=(), trigger=True,
                     clear_heads=('kneel', 'stand'), at=[(1, 2.55, (0.000, -0.040, 0.0495), 0.002), (1, 3.0, (-0.010, -0.041, 0.047), 0.005), (0, 3.0, (-0.022, 0.008, -0.044), 0.012),
                         (2, 3.0, (-0.022, -0.030, -0.004), 0.015), (3, 3.0, (-0.022, -0.036, -0.026), 0.015),
                         (4, 3.0, (-0.015, -0.046, -0.046), 0.015)]),
    'LeftLow': dict(side='Left', pose='carry', hint=(-0.5, -0.6, -0.3), F=(1, 0.1, 0.1), N=(0, 1, 0),
                    knuckle=(0.014, -0.022, 0.318), straight=(),
                    at=[(0, 3.0, (-0.036, 0.034, 0.355), 0.014), (1, 3.0, (0.026, 0.040, 0.355), 0.016),
                        (2, 3.0, (0.024, 0.044, 0.326), 0.014), (3, 3.0, (0.026, 0.040, 0.300), 0.016),
                        (4, 3.0, (0.028, 0.026, 0.276), 0.018)]),
    'LeftRun': dict(side='Left', pose='carry', hint=(-0.4, -0.8, 0.1), F=(1, 0.1, 0.1), N=(0, 1, 0),
                    knuckle=(0.014, -0.016, 0.455), straight=(),
                    at=[(0, 3.0, (-0.034, 0.036, 0.492), 0.014), (1, 3.0, (0.022, 0.042, 0.490), 0.016),
                        (2, 3.0, (0.020, 0.044, 0.462), 0.014), (3, 3.0, (0.022, 0.040, 0.436), 0.016),
                        (4, 3.0, (0.024, 0.026, 0.412), 0.018)]),
    'LeftKneel': dict(side='Left', pose='kneel', hint=(-0.6, -0.8, 0.0), F=(1, 0.05, 0.3), N=(0, 1, 0),
                      knuckle=(0.014, -0.022, 0.305), straight=(),
                      at=[(0, 3.0, (-0.036, 0.030, 0.345), 0.014), (1, 3.0, (0.030, 0.032, 0.345), 0.016),
                          (2, 3.0, (0.028, 0.036, 0.316), 0.014), (3, 3.0, (0.028, 0.032, 0.290), 0.016),
                          (4, 3.0, (0.030, 0.018, 0.268), 0.018)]),
    'LeftStand': dict(side='Left', pose='stand', hint=(-0.6, -0.8, 0.0), F=(0.75, 0.15, 0.6), N=(0, 1, 0),
                      knuckle=(0.008, -0.068, 0.135), straight=(), thumb=(8, 10), pen=700, wrist=(25, 12),
                      at=[(0, 3.0, (-0.034, -0.012, 0.170), 0.008), (1, 3.0, (0.024, 0.000, 0.200), 0.022),
                          (2, 3.0, (0.030, -0.004, 0.172), 0.022), (3, 3.0, (0.028, -0.014, 0.146), 0.022),
                          (4, 3.0, (0.024, -0.030, 0.124), 0.024)]),
}


def frame(F, N):
    F = unit(np.asarray(F, float)); N = np.asarray(N, float); N = unit(N - F * (F @ N))
    return np.column_stack((F, N, np.cross(F, N)))


class Problem:
    def __init__(self, name, spec):
        self.name, self.s = name, spec
        h = self.h = hands[spec['side']]
        po = pose[spec['pose']]
        o = 0 if spec['side'] == 'Right' else 3
        self.shoulder = po[o]
        self.body = po[8:11].T                          # the animator's axes in rifle space
        g = np.array(groups[spec['pose']])
        self.L1 = np.linalg.norm(g[:, o] - g[:, o + 1], axis=1).mean()
        self.L2 = np.linalg.norm(g[:, o + 1] - g[:, o + 2], axis=1).mean()
        self.hint = self.body @ np.array(spec['hint'])
        self.R0 = frame(spec['F'], spec['N']) @ frame(h.along, h.palm).T
        # heads the forearm must keep clear of (the forearm across the chin seen from the front, standing)
        self.heads = [np.mean(groups[k], 0)[6] for k in spec.get('clear_heads', ())]

        main = h.idx[np.arange(len(h.vids)), h.wgt.argmax(1)]
        sel, self.groups, self.radius = [], [], np.zeros((5, 3))
        use = h.in_hand > 0.6
        for b in range(16):
            ids = np.nonzero((main == b) & use)[0]
            sel += list(ids[::max(1, len(ids) // (170 if b == 0 else 55))])
            if b:
                fi, j = divmod(b - 1, 3)
                d = h.rest_R[b] @ h.axes[fi, j, 2]
                q = h.rest[ids] - h.rest_J[fi, j]
                self.radius[fi, j] = np.median(np.linalg.norm(q - np.outer(q @ d, d), axis=1))
                pad = ids[h.rest_n[ids] @ (h.rest_R[b] @ h.axes[fi, j, 1]) > 0.55]
                trigger = fi == 1 and spec.get('trigger', False)
                weight = (60.0 if j == 2 else 0.0) if trigger else (90.0 if j == 2 else 22.0 if j == 1 else 14.0)   # (the trigger finger's end pad on the blade: 2026-11-23, it stood 2.5 mm off it)
                if b == 1:
                    weight = 45.0                       # the ball of the thumb
            else:
                pad = ids[h.rest_n[ids] @ h.palm > 0.55]
                along = h.rest[pad] @ h.along
                weight = 0.0
                for part in (along < 0.035, along >= 0.035):
                    p2 = pad[part][::2]
                    if len(p2) >= 6:
                        self.groups.append((p2, 50.0, 10))
                        sel += list(p2)
            if weight and len(pad) >= 4:
                p2 = pad[::max(1, len(pad) // 45)]
                self.groups.append((p2, weight, min(5, len(p2))))
                sel += list(p2)
        self.sel = np.unique(np.array(sel, int))
        lut = np.full(len(h.vids), -1); lut[self.sel] = np.arange(len(self.sel))
        self.groups = [(lut[p], w, k) for p, w, k in self.groups]
        self.idx, self.loc, self.wgt = h.idx[self.sel], h.local[self.sel], h.wgt[self.sel]

        # spread that lays each finger in beside the middle one, and the pose the hand likes
        pr = np.zeros(21); sd = np.ones(21); lo = np.zeros(21); hi = np.zeros(21)
        pr[:5] = rad((22, 0, 0, 22, 28)); sd[:5] = rad((30, 25, 20, 28, 30))
        lo[:5] = rad((-25, -35, -35, -10, -15)); hi[:5] = rad((60, 45, 35, 52, 52))
        md = unit(h.rest_J[2, 1] - h.rest_J[2, 0])
        for fi in range(1, 5):
            k = 5 + 4 * (fi - 1)
            t = h.rest_R[1 + 3 * fi] @ h.axes[fi, 0, 1]
            d = h.rest_R[1 + 3 * fi] @ h.axes[fi, 0, 2]
            m = unit(md - t * (t @ md))
            spread = np.arctan2(np.cross(d, m) @ t, d @ m) * (0.85 if fi != 2 else 0)
            straight = fi in spec['straight']
            pr[k:k + 4] = rad(12 if straight else 45), spread, rad(8 if straight else 65), rad(5 if straight else 38)
            sd[k:k + 4] = rad(30), rad(8), rad(14 if straight else 32), rad(10 if straight else 22)
            lo[k:k + 4] = rad(-15), spread - rad(16), rad(-3), rad(-5)
            hi[k:k + 4] = rad(88), spread + rad(16), rad(108), rad(78)
        if 'thumb' in spec:                              # (a thumb laid straight along the magazine, not curled on it)
            pr[3:5] = rad(np.array(spec['thumb'])); sd[3:5] = rad(8)
        self.pr, self.sd, self.lo, self.hi = pr, sd, lo, hi

    def place(self, x):
        v = x[3:6]; a = np.linalg.norm(v)
        return (rot(v / a, a) if a > 1e-9 else np.eye(3)) @ self.R0, x[:3]

    def elbow(self, wrist, swing):
        d = wrist - self.shoulder; L = np.linalg.norm(d); ax = d / L
        D_ = np.clip(L, abs(self.L1 - self.L2) + 1e-3, self.L1 + self.L2 - 1e-3)
        t = (self.L1 ** 2 - self.L2 ** 2 + D_ ** 2) / (2 * D_)
        b = unit(self.hint - ax * (ax @ self.hint))
        b = b * np.cos(swing) + np.cross(ax, b) * np.sin(swing)
        return self.shoulder + ax * t + b * np.sqrt(max(self.L1 ** 2 - t * t, 0)), b, max(0.0, L - D_)

    def wrist(self, R, wrist, swing):
        e, b, over = self.elbow(wrist, swing)
        f = R.T @ unit(wrist - e)
        flex = np.arctan2(f @ self.h.palm, f @ self.h.along)
        dev = np.arctan2(f @ self.h.radial, np.hypot(f @ self.h.along, f @ self.h.palm))
        return e, b, over, flex, dev

    def residual(self, x):
        h = self.h
        R0, T0 = self.place(x)
        R, T, J = h.fk(x[6:27])
        P = ((np.einsum('nkij,nkj->nki', R[self.idx], self.loc) + T[self.idx]) * self.wgt[..., None]).sum(1)
        P = P @ R0.T + T0; JW = J @ R0.T + T0
        d = field(P)
        out = [np.minimum(d - 0.0003, 0) * float(self.s.get('pen', 320))]
        out.append([w * np.mean(np.partition(np.maximum(d[p] - 0.001, 0), k - 1)[:k]) for p, w, k in self.groups])
        for fi, at, where, tol in self.s['at']:
            j = min(int(at), 2); t = at - j
            out.append((JW[fi, j] * (1 - t) + JW[fi, j + 1] * t - np.array(where)) / tol)
        out.append((JW[2, 0] - np.array(self.s['knuckle'])) / self.s.get('knuckle_tol', 0.020))
        a = x[6:27]
        out.append((a - self.pr) / self.sd * 0.45)
        out.append(np.maximum(self.lo - a, 0) / rad(2.5)); out.append(np.maximum(a - self.hi, 0) / rad(2.5))
        extra = []
        for fi in range(1, 5):
            k = 5 + 4 * (fi - 1)
            if fi not in self.s['straight']:
                extra.append((a[k + 3] - 0.62 * a[k + 2]) / rad(24))          # the last two joints bend together
            if fi in (2, 3):
                extra.append((a[k] - a[k + 4]) / rad(28))                     # neighbours bend alike
                extra.append((a[k + 2] - a[k + 6]) / rad(32))
        e, b, over, flex, dev = self.wrist(R0, T0, x[27])
        fl, dl = self.s.get('wrist', (55, 26))
        extra += [flex / rad(38), dev / rad(18), max(0, abs(flex) - rad(fl)) / rad(4), max(0, abs(dev) - rad(dl)) / rad(4),
                  x[27] / rad(self.s.get('swing', 80)), over / 0.003]
        for head in self.heads:
            ab = T0 - e; t_ = np.clip((head - e) @ ab / (ab @ ab), 0, 1)
            extra.append(max(0.0, 0.17 - np.linalg.norm(e + ab * t_ - head)) / 0.004)
        out.append(extra)
        # fingers not inside one another
        s_ = np.array((0.1, 0.5, 0.9))[None, None, :, None]
        mid = (J[:, :3, None] * (1 - s_) + J[:, 1:, None] * s_).reshape(5, 9, 3)
        r = np.repeat(self.radius, 3, axis=1) * 0.80
        for i, j in ((0, 1), (0, 2), (0, 3), (0, 4), (1, 2), (2, 3), (3, 4)):
            skip = 3 if i == 0 else 0                   # (the thumb's first bone lies in the palm)
            dd = np.linalg.norm(mid[i, skip:, None] - mid[j, None, :], axis=2)
            out.append((np.maximum(r[i, skip:, None] + r[j, None, :] - dd, 0) * 170.0).ravel())
        return np.concatenate([np.ravel(o) for o in out])

    def solve(self, x, steps):
        r = self.residual(x); cost = r @ r; lam = 0.03
        scale = np.r_[np.full(3, 0.01), np.full(25, 0.17)]
        for _ in range(steps):
            J = np.empty((len(r), 28))
            for i in range(28):
                y = x.copy(); eps = scale[i] * 1e-3; y[i] += eps
                J[:, i] = (self.residual(y) - r) / 1e-3
            A = J.T @ J; g = J.T @ r; moved = False
            for _ in range(7):
                st = np.linalg.solve(A + lam * np.diag(np.diag(A) + 1.0), -g)
                st *= min(1.0, 2.0 / max(1e-9, np.abs(st).max()))
                y = x + st * scale; r2 = self.residual(y); c2 = r2 @ r2
                if c2 < cost:
                    moved = cost - c2 > 1e-3 * cost
                    x, r, cost = y, r2, c2; lam = max(lam / 3, 1e-4)
                    break
                lam *= 4
            if not moved:
                break
        return x, cost

    def best(self, starts):
        rng = np.random.default_rng(11); found = []
        for i in range(starts):
            x = np.zeros(28); x[6:27] = self.pr
            if i:
                x[3:6] = rng.normal(0, 0.30, 3); x[6:27] += rng.normal(0, 0.35, 21) * self.sd; x[27] = rng.normal(0, 0.4)
            R, _ = self.place(x)
            x[:3] = np.array(self.s['knuckle']) - R @ self.h.rest_J[2, 0] + R @ self.h.palm * -0.012
            if i:
                x[:3] += rng.normal(0, 0.007, 3)
            found.append(self.solve(x, 22))
        found.sort(key=lambda c: c[1])
        return min((self.solve(x, 40) for x, _ in found[:3]), key=lambda c: c[1])

    def report(self, x, cost):
        h = self.h
        R0, T0 = self.place(x); R, T, J = h.fk(x[6:27])
        P = h.skin(R, T) @ R0.T + T0; JW = J @ R0.T + T0
        d = field(P)[h.in_hand > 0.6]
        e, b, over, flex, dev = self.wrist(R0, T0, x[27])
        tips = []
        main = h.idx[np.arange(len(h.vids)), h.wgt.argmax(1)]
        for fi in range(5):
            ds = field(P[main == 3 + 3 * fi])
            tips.append('%s %+.1f' % (FINGERS[fi], ds.min() * 1000))
        print('%-10s cost %.1f | in: max %.1f mm, >1mm %d/%d | wrist flex %+.0f dev %+.0f | reach over %.1f mm'
              % (self.name, cost, max(0, -d.min()) * 1000, (d < -0.001).sum(), len(d), np.degrees(flex), np.degrees(dev), over * 1000))
        print('           tips to wood (mm): ' + ', '.join(tips))
        for fi, at, where, tol in self.s['at'][:2]:
            j = min(int(at), 2); t = at - j
            print('           %s target off %.1f mm' % (FINGERS[fi], np.linalg.norm(JW[fi, j] * (1 - t) + JW[fi, j + 1] * t - np.array(where)) * 1000))
        np.savez(D + 'grip_%s.npz' % self.name, P=P, F=h.faces, vids=h.vids, J=JW, elbow=e, shoulder=self.shoulder, x=x)
        locals_ = [matq(R[p].T @ R[1 + 3 * fi + j]).round(6).tolist()
                   for fi in range(5) for j in range(3) for p in [0 if j == 0 else 3 * fi + j]]
        sys.stdout.flush()
        return dict(side=self.s['side'], pos=T0.round(5).tolist(), rot=matq(R0).round(6).tolist(),
                    elbow=unit(self.body.T @ b).round(4).tolist(), joints=locals_,
                    flex=float(np.degrees(flex)), dev=float(np.degrees(dev)), cost=float(cost))


if __name__ == '__main__':
    starts = int(sys.argv[1]) if len(sys.argv) > 1 else 14
    wanted = sys.argv[2:] or list(SPECS)
    res = json.load(open(D + 'grips.json')) if os.path.exists(D + 'grips.json') else {}
    for name in wanted:
        pb = Problem(name, SPECS[name])
        res[name] = pb.report(*pb.best(starts))
        json.dump(res, open(D + 'grips.json', 'w'), indent=1)
