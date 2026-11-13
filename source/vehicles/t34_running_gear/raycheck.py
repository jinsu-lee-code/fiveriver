# blender -b --factory-startup -P raycheck.py -- <in.fbx> <gear json> <distance,distance,...>
# See-through count as Unity would draw it (back faces culled): the tracks are split as TankRunningGear.cs does, the
# wheels turned and the belt carried round by each distance; rays are cast from cameras all round the tank's left
# side (low / eye / high, front three-quarter to rear three-quarter) at points on the moving belt. A ray whose first
# hit is a BACK face looks through a hole in Unity. Counted by where the aimed belt point is: rear end, runs, front end.
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim

a = sys.argv[sys.argv.index('--') + 1:]
SRC, JP = a[0], a[1]; DIST = [float(v) for v in a[2].split(',')]
J = json.load(open(JP)); W = np.array(J['wheels']).reshape(-1, 4)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
static_v, static_f = [], []
parts = {}
for o in bpy.context.scene.objects:
    if o.type != 'MESH': continue
    me = o.data; me.calc_loop_triangles()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    M = np.array(o.matrix_world); w = co @ M[:3, :3].T + M[:3, 3]
    t = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', t); t = t.reshape(-1, 3)
    if o.name in ('TrackL', 'TrackR'):
        sign = -1.0 if o.name == 'TrackL' else 1.0
        x, y, z = sign * -w[:, 0], w[:, 2], -w[:, 1]
        loop = np.array(J[o.name]['loop']).reshape(-1, 2); T = sim.table(loop); G = sim.grid(T)
        g, seam = sim.classify(J, T, G, x, y, z, t)
        used = np.unique(t[g == 7]); place = np.zeros((len(x), 3))
        s, al, ou = sim.project(T, G, z[used], y[used]); place[used] = np.c_[s, al, ou]
        parts[o.name] = dict(sign=sign, x=x, y=y, z=z, t=t, g=g, seam=seam, T=T, place=place, used=used)
    else:
        static_v.append(w); static_f.append(t)

def world(sign, X, Y, Z): return np.c_[-(sign * X), -Z, Y]

rng = np.random.default_rng(7)
cams = []
for az in np.radians(np.arange(-70, 71, 20)):          # around +x (TrackL side); 0 = straight side on
    for h in (0.25, 0.9, 2.0):
        cams.append(Vector((1.2 + 6.0 * math.cos(az), 6.0 * math.sin(az), h)))
print('cameras', len(cams))
for d in DIST:
    V, F, lab = [], [], []
    off = 0
    for w, t in zip(static_v, static_f):
        V.append(w); F.append(t + off); lab.append(np.zeros(len(t), int)); off += len(w)
    aims = None
    for name, P in parts.items():
        x, y, z, t, g, seam, sign = P['x'], P['y'], P['z'], P['t'], P['g'], P['seam'], P['sign']
        rest = np.r_[np.where(g == 0)[0], np.where(seam)[0]]
        V.append(world(sign, x, y, z)); F.append(t[rest] + off); lab.append(np.ones(len(rest), int)); off += len(x)
        for k in range(6):
            wz, wy, r, roll = W[k]; c, s_ = math.cos(d / roll), math.sin(d / roll)
            yy = wy + (y - wy) * c - (z - wz) * s_; zz = wz + (y - wy) * s_ + (z - wz) * c
            V.append(world(sign, x, yy, zz)); F.append(t[g == k + 1] + off); lab.append(np.full((g == k + 1).sum(), 2)); off += len(x)
        mz, my = z.copy(), y.copy(); u = P['used']
        mz[u], my[u] = sim.move(P['T'], P['place'][u], (-d) % P['T']['L'])
        V.append(world(sign, x, my, mz)); bt = t[g == 7]; F.append(bt + off); lab.append(np.full(len(bt), 3)); off += len(x)
        if name == 'TrackL':
            # aim points: random points on the outer half of the moving belt's triangles (area weighted)
            Pw = world(sign, x, my, mz); A_, B_, C_ = Pw[bt[:, 0]], Pw[bt[:, 1]], Pw[bt[:, 2]]
            ar = 0.5 * np.linalg.norm(np.cross(B_ - A_, C_ - A_), axis=1)
            pick = rng.choice(len(bt), 6000, p=ar / ar.sum())
            r1 = np.sqrt(rng.random(6000)); r2 = rng.random(6000)
            aims = A_[pick] * (1 - r1)[:, None] + B_[pick] * (r1 * (1 - r2))[:, None] + C_[pick] * (r1 * r2)[:, None]
    V = np.vstack(V); F = np.vstack(F); lab = np.concatenate(lab)
    bvh = BVHTree.FromPolygons([tuple(v) for v in V], [tuple(f) for f in F], all_triangles=True)
    tz = -aims[:, 1]   # tank z of the aimed point (front +)
    region = np.where(tz < -1.95, 0, np.where(tz > 1.85, 2, 1))
    back = np.zeros(3, int); total = np.zeros(3, int); back_on = np.zeros(4, int)
    for cam in cams:
        for p, rg in zip(aims, region):
            dvec = Vector(p) - cam; dist = dvec.length; dvec.normalize()
            # Unity skips back faces: walk on to the first FRONT face; see-through if there is none within 12 cm behind
            # the aimed belt point (the ray went through the belt to the background or something far behind it)
            o_ = cam.copy(); travelled = 0.0; front = None; first_back = None
            for _ in range(12):
                hit, nrm, idx, hd = bvh.ray_cast(o_, dvec, dist + 0.5 - travelled)
                if hit is None: break
                travelled += hd
                if nrm.dot(dvec) < 0: front = travelled; break
                if first_back is None: first_back = idx
                o_ = hit + dvec * 1e-4; travelled += 1e-4
            if front is not None and front < dist - 0.12: continue     # something in front hides this belt point
            total[rg] += 1
            if front is None or front > dist + 0.12:
                back[rg] += 1
                if first_back is not None: back_on[lab[first_back]] += 1
    print('RAY distance %.2f  see-through [rear end, runs, front end] %s of %s rays  (%.2f%%)  first back face on [other, still, wheel, belt] %s'
          % (d, back.tolist(), total.tolist(), 100.0 * back.sum() / max(1, total.sum()), back_on.tolist()))
