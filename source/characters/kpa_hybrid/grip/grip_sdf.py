# Signed distance to the rifle (rifle space, metres) on a 2.5 mm grid round both grips; negative inside.
# run: blender -b --factory-startup -P grip_sdf.py
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

D = 'C:/sk_assets/capture/grip/'
RV, RF = [], []
for line in open(D + 'static.txt', encoding='utf-8-sig'):
    p = line.split()
    if p[0] == 'RV':
        RV.append(tuple(float(x) for x in p[1:4]))
    elif p[0] == 'RF':
        RF.append(tuple(int(x) for x in p[1:4]))
tree = BVHTree.FromPolygons(RV, RF)
lo = np.array((-0.085, -0.16, -0.20)); hi = np.array((0.085, 0.105, 0.62)); h = 0.0025
n = np.ceil((hi - lo) / h).astype(int) + 1
dirs = [Vector(d).normalized() for d in ((1, 0.031, 0.017), (-0.023, 1, 0.041), (-0.8, -0.6, 0.029))]
g = np.empty(n, np.float32)
for i in range(n[0]):
    for j in range(n[1]):
        for k in range(n[2]):
            q = Vector((lo[0] + i * h, lo[1] + j * h, lo[2] + k * h))
            d = tree.find_nearest(q)[3]
            if d < 0.04:
                votes = 0
                for r in dirs:
                    c, o = 0, q.copy()
                    for _ in range(24):
                        hit = tree.ray_cast(o, r)[0]
                        if hit is None:
                            break
                        c += 1; o = hit + r * 2e-5
                    votes += c & 1
                if votes >= 2:
                    d = -d
            g[i, j, k] = d
np.savez(D + 'sdf.npz', d=g, lo=lo, h=h)
print('SDF', tuple(n), 'inside', int((g < 0).sum()), 'deepest', float(g.min()))
