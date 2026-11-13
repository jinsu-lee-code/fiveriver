# blender -b --factory-startup -P cut_tracks.py -- <in.fbx> <gear json> <out.fbx> [<rounding json>]
# (with a rounding json the wheels are first made round about its axles: round_wheels.py, then cut)
# Splits TrackL / TrackR triangles exactly along the lines TankRunningGear.cs sorts them by (each wheel's turning-disc
# circle, the belt band's two edges, the x planes), so that no triangle lies across a cut any more. New vertices are
# put only on existing edges (the surface, its UVs and its normals stay where they were); the pieces are triangulated.
# Nothing else in the file changes. Exported with the same call as Docs/Art/ModelAudit_20261110/reduce_and_fill.py.
import bpy, bmesh, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gear_blender as gb

a = sys.argv[sys.argv.index('--') + 1:]
SRC, GEAR, DST = a[0], a[1], a[2]
ROUND = a[3] if len(a) > 3 else None
J = json.load(open(GEAR))
WHEELS = np.array(J['wheels']).reshape(-1, 4).copy()
WHEELS[:, 2] += 0.012   # the JSON radius is the cut minus TankRunningGear.SeamOverlap (see gear_blender.py)
HALF, INSIDE, WIN, BIN = J['beltHalf'], J['beltInside'], J['wheelInner'], J['beltInner']
EPS = 2e-4   # vertices this close to a cut count as on it (no sliver triangles)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
report = {}
if ROUND:
    import round_wheels
    report['rounding'] = round_wheels.round_tracks(json.load(open(ROUND)))

def tank(co, M, sign):
    """object-local coordinates (n,3) -> sign-corrected tank-body x, y, z"""
    w = co @ M[:3, :3].T + M[:3, 3]
    return sign * -w[:, 0], w[:, 2], -w[:, 1]

for name, sign in (('TrackL', -1.0), ('TrackR', 1.0)):
    o = bpy.data.objects[name]; me = o.data
    M = np.array(o.matrix_world)
    loop = np.array(J[name]['loop']).reshape(-1, 2)
    # carry the delivered corner normals through the edit as a plain float attribute
    cn = np.array([v.vector[:] for v in me.corner_normals], dtype=np.float32)
    at = me.attributes.new('nrm_tmp', 'FLOAT_VECTOR', 'CORNER'); at.data.foreach_set('vector', cn.ravel())
    v0, f0 = len(me.vertices), len(me.polygons)
    bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv['st']
    nrl = bm.loops.layers.float_vector['nrm_tmp']

    def coords():
        bm.verts.ensure_lookup_table()
        co = np.array([v.co[:] for v in bm.verts])
        return tank(co, M, sign)

    def field_band(kind):
        def f(x, y, z):
            s, n, al, L = gb.project(loop, z, y)
            return n - HALF if kind == 'out' else n + INSIDE
        return f

    class face_info(dict):
        """per face: vertex indices, centroid, and (on first use) the centroid's offsets from the loop"""
        def __init__(self, x, y, z):
            bm.faces.ensure_lookup_table()
            idx = [[v.index for v in fc.verts] for fc in bm.faces]
            super().__init__(idx=idx, cx=np.array([x[i].mean() for i in idx]), cy=np.array([y[i].mean() for i in idx]),
                             cz=np.array([z[i].mean() for i in idx]))
        def __missing__(self, key):
            s, n, al, L = gb.project(loop, self['cz'], self['cy'])
            self['n'] = n; self['al'] = al
            return self[key]

    def cut(label, f, face_ok):
        """split every edge of the accepted faces where f changes sign, then join the new points across each face"""
        x, y, z = coords()
        fv = f(x, y, z)
        F = face_info(x, y, z)
        bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
        ok = face_ok(F, x, y, z, fv)
        faces = [fc for fc in bm.faces if ok[fc.index]]
        edges = {}
        for fc in faces:
            for e in fc.edges:
                i, j = e.verts[0].index, e.verts[1].index
                if fv[i] * fv[j] < 0 and abs(fv[i]) > EPS and abs(fv[j]) > EPS: edges[e.index] = e
        el = list(edges.values())
        if not el:
            report.setdefault(name, []).append((label, 0)); return
        ia = np.array([e.verts[0].index for e in el]); ib = np.array([e.verts[1].index for e in el])
        A = np.c_[x[ia], y[ia], z[ia]]; B = np.c_[x[ib], y[ib], z[ib]]
        lo = np.zeros(len(el)); hi = np.ones(len(el)); fa = fv[ia]
        for _ in range(22):          # bisection on the real field (the circle / offset line is curved)
            mid = (lo + hi) / 2; P = A + (B - A) * mid[:, None]
            fm = f(P[:, 0], P[:, 1], P[:, 2])
            same = np.sign(fm) == np.sign(fa)
            lo = np.where(same, mid, lo); hi = np.where(same, hi, mid)
        ts = (lo + hi) / 2
        on = set(v for v in bm.verts if abs(fv[v.index]) <= EPS)
        touched = set()
        for e, t in zip(el, ts):
            va, vb = e.verts
            ca, cb = va.co.copy(), vb.co.copy()
            faces_e = list(e.link_faces)
            ne, nv = bmesh.utils.edge_split(e, va, float(t))
            nv.co = ca.lerp(cb, float(t))
            for l in nv.link_loops:      # set the new corners from the two ends of the edge, in this face
                pa = l.link_loop_prev if l.link_loop_prev.vert in (va, vb) else None
                pb = l.link_loop_next if l.link_loop_next.vert in (va, vb) else None
                if pa is None or pb is None: continue
                la, lb = (pa, pb) if pa.vert is va else (pb, pa)
                l[uvl].uv = la[uvl].uv.lerp(lb[uvl].uv, float(t))
                l[nrl] = la[nrl].lerp(lb[nrl], float(t))
            on.add(nv); touched.update(faces_e)
        joined = odd = 0
        for fc in touched:
            if not fc.is_valid: continue
            vs = [v for v in fc.verts if v in on]
            if len(vs) == 2:
                l0 = next(l for l in fc.loops if l.vert is vs[0])
                if l0.link_loop_next.vert is vs[1] or l0.link_loop_prev.vert is vs[1]: continue
                nf, nl = bmesh.utils.face_split(fc, vs[0], vs[1])
                if nf is None: odd += 1
                else: joined += 1
            elif len(vs) > 2:
                odd += 1
        big = [fc for fc in bm.faces if len(fc.verts) > 3]
        if big: bmesh.ops.triangulate(bm, faces=big, quad_method='BEAUTY', ngon_method='BEAUTY')
        bm.verts.index_update(); bm.faces.index_update(); bm.edges.index_update()
        # check: no accepted face may still have corners on both sides of the cut
        x, y, z = coords(); fv2 = f(x, y, z); F2 = face_info(x, y, z); ok2 = face_ok(F2, x, y, z, fv2)
        left = sum(1 for k, i in enumerate(F2['idx']) if ok2[k] and fv2[i].max() > EPS and fv2[i].min() < -EPS)
        report.setdefault(name, []).append((label, len(el), joined, odd, left))

    def vmax(F, a): return np.array([a[i].max() for i in F['idx']])
    def vabsmin(F, a): return np.array([np.abs(a[i]).min() for i in F['idx']])
    def in_band(F): return (F['cx'] > BIN) & (F['n'] > -INSIDE) & (F['n'] < HALF) & (np.abs(F['al']) < HALF)

    # 1-2. the belt band's outer and inner edges, 3. its inner x plane
    band_face = lambda F, x, y, z, fv: (vmax(F, x) > BIN - 0.05) & (vabsmin(F, fv) < 0.12)
    cut('belt outer edge', field_band('out'), band_face)
    cut('belt inner edge', field_band('in'), band_face)
    cut('belt x plane', lambda x, y, z: x - BIN, lambda F, x, y, z, fv: (F['n'] > -INSIDE) & (F['n'] < HALF))
    # 4a. long chords: the circle cut joins the two points where a triangle's edges cross the circle with a straight
    #     line, which sits inside the circle by chord^2 / 8r (a 25 cm chord: 1.8 cm, and that sliver then turns with
    #     the wheel). Triangles whose chord would be longer than CHORD are first halved by a radial plane through the
    #     axle at the chord's middle angle, until every chord is short (a plane cut: the surface does not move).
    CHORD = 0.06
    def chord_requests(F, x, y, z, wz, wy, r):
        T = np.array(F['idx'])
        P = np.stack([z[T] - wz, y[T] - wy], -1)
        rv = np.hypot(P[..., 0], P[..., 1])
        A = P; Bp = np.roll(P, -1, 1); D = Bp - A
        u = np.clip(-(A * D).sum(-1) / np.maximum((D * D).sum(-1), 1e-12), 0, 1)
        Q = A + D * u[..., None]; seg = np.hypot(Q[..., 0], Q[..., 1])
        crossing = (seg.min(1) < r - EPS) & (rv.max(1) > r + EPS) & (vmax(F, x) > WIN - 0.03) & ~in_band(F)
        out = {}
        for f in np.where(crossing)[0]:
            pts = []
            for e in range(3):
                a_, b_ = P[f, e], P[f, (e + 1) % 3]
                d_ = b_ - a_
                # solve |a + d t| = r on the edge
                qa = (d_ * d_).sum(); qb = 2 * (a_ * d_).sum(); qc = (a_ * a_).sum() - r * r
                disc = qb * qb - 4 * qa * qc
                if qa < 1e-12 or disc < 0: continue
                for tt in ((-qb - np.sqrt(disc)) / (2 * qa), (-qb + np.sqrt(disc)) / (2 * qa)):
                    if 0 < tt < 1: pts.append(a_ + d_ * tt)
            if len(pts) < 2:
                continue
            pts = np.array(pts)
            dmax = max(np.hypot(*(p1 - p2)) for p1 in pts for p2 in pts)
            if dmax <= CHORD: continue
            ang = np.arctan2(pts[:, 1], pts[:, 0])
            ref = ang[0]; rel = (ang - ref + np.pi) % (2 * np.pi) - np.pi
            mid = ref + (rel.max() + rel.min()) / 2
            key = round(float(np.degrees(mid)) % 360, 1)
            out.setdefault(key, []).append(f)
        return out
    for i, (wz, wy, r, rr) in enumerate(WHEELS):
        for rnd in range(8):
            x, y, z = coords(); F = face_info(x, y, z)
            req = chord_requests(F, x, y, z, wz, wy, r)
            if not req: break
            for deg, faces_ in req.items():
                th = np.radians(deg); c, s_ = np.cos(th), np.sin(th)
                want = set(faces_)
                # face indices change after every cut: pick the faces again by their centroid and the requested angle
                def fsp(x, y, z, wz=wz, wy=wy, c=c, s_=s_): return (z - wz) * s_ - (y - wy) * c
                def oksp(F, x, y, z, fv, wz=wz, wy=wy, r=r, c=c, s_=s_, deg=deg):
                    rq = chord_requests(F, x, y, z, wz, wy, r)
                    m = np.zeros(len(F['idx']), bool)
                    for k_, fl in rq.items():
                        if abs((k_ - deg + 180) % 360 - 180) < 1.0: m[fl] = True
                    return m
                cut('wheel %d chord %g' % (i, deg), fsp, oksp)
    # 4. each wheel's turning-disc circle (outside the band; the band wins in the game too)
    for i, (wz, wy, r, rr) in enumerate(WHEELS):
        def fcirc(x, y, z, wz=wz, wy=wy, r=r): return np.hypot(z - wz, y - wy) - r
        cut('wheel %d circle' % i, fcirc, lambda F, x, y, z, fv: (vmax(F, x) > WIN - 0.03) & (vabsmin(F, fv) < 0.15) & ~in_band(F))
    # 5. the wheel x plane inside the discs
    def disc_face(F, x, y, z, fv):
        near = np.zeros(len(F['cx']), bool)
        for (wz, wy, r, rr) in WHEELS: near |= np.hypot(F['cz'] - wz, F['cy'] - wy) < r + 0.02
        return near & ~in_band(F)
    cut('wheel x plane', lambda x, y, z: x - WIN, disc_face)

    bm.to_mesh(me); bm.free()
    nr = np.empty(len(me.loops) * 3, dtype=np.float32); me.attributes['nrm_tmp'].data.foreach_get('vector', nr)
    nr = nr.reshape(-1, 3); nr /= np.maximum(np.linalg.norm(nr, axis=1), 1e-8)[:, None]
    me.attributes.remove(me.attributes['nrm_tmp'])
    me.normals_split_custom_set(nr.tolist())
    me.validate(verbose=False)
    me.calc_loop_triangles()
    report[name + '_counts'] = dict(verts_before=v0, verts_after=len(me.vertices), tris_before=f0,
                                    tris_after=len(me.loop_triangles), polys_after=len(me.polygons))

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.export_scene.fbx(filepath=DST, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
                         add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=False, path_mode='STRIP')
summary = {}
for k, v in report.items():
    if k.endswith('_counts') or k == 'rounding': summary[k] = v; continue
    sp = [e for e in v if 'chord' in e[0]]; other = [e for e in v if 'chord' not in e[0]]
    summary[k] = other + [('chord halvings: cuts, edges split, faces left across', len(sp), sum(e[1] for e in sp), sum(e[-1] for e in sp if len(e) > 2))]
print('CUT_REPORT', json.dumps(summary))
