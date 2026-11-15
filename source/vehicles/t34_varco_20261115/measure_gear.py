# Side view of the generated tank's running gear, to see whether wheels and belt can be told apart and measured.
#   blender -b --factory-startup -P measure_gear.py -- <new.glb> <out dir>
import bpy, sys, numpy as np
from mathutils import Vector
src, out = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o = max((x for x in bpy.data.objects if x.type == 'MESH'), key=lambda x: len(x.data.vertices))
me = o.data; M = o.matrix_world
V = np.array([M @ v.co for v in me.vertices]); T = np.array([[l for l in p.vertices][:3] for p in me.polygons if len(p.vertices) == 3])
zmin, zmax = V[:, 2].min(), V[:, 2].max()
low = V[V[:, 2] < zmin + 0.45 * (zmax - zmin)]
s = 5.18 / (low[:, 1].max() - low[:, 1].min())                 # the old hull's length
V = (V - [(low[:, 0].max() + low[:, 0].min()) / 2, (low[:, 1].max() + low[:, 1].min()) / 2, zmin]) * s
print('SIZE', np.ptp(V, 0).round(2), 'half width', np.abs(V[:, 0]).max().round(3))
# points spread evenly over the surface
A = V[T[:, 0]]; e1 = V[T[:, 1]] - A; e2 = V[T[:, 2]] - A; area = 0.5 * np.linalg.norm(np.cross(e1, e2), axis=1)
rng = np.random.default_rng(1); pick = rng.choice(len(T), 600000, p=area / area.sum()); u = rng.random(600000); v = rng.random(600000); f = u + v > 1; u[f] = 1 - u[f]; v[f] = 1 - v[f]
S = A[pick] + e1[pick] * u[:, None] + e2[pick] * v[:, None]
np.save(out + '/points.npy', S.astype(np.float32))
print('saved', len(S))
