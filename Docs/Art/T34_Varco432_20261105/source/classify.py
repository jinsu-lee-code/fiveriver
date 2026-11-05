# Shared analysis: classify faces of the VARCO single mesh into hull / turret / gun / trackL / trackR.
import bpy, bmesh, numpy as np, math, json
# Height bands in source units. Defaults = first VARCO mesh; the build script overrides them per model.
ZC, ZHI, R_T, TRACK_ZMAX, FACE_LEVEL = 0.322, 0.36, 0.26, 0.215, False
def analyse(o, report=print):
    me = o.data
    nv = len(me.vertices); nf = len(me.polygons)
    co = np.empty(nv*3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    loop_v = np.empty(len(me.loops), np.int64); me.loops.foreach_get('vertex_index', loop_v)
    ls = np.empty(nf, np.int64); me.polygons.foreach_get('loop_start', ls)
    lt = np.empty(nf, np.int64); me.polygons.foreach_get('loop_total', lt)
    fc = np.empty(nf*3); me.polygons.foreach_get('center', fc); fc = fc.reshape(-1, 3)
    # vertex islands via union-find on face loops
    parent = np.arange(nv)
    def find(a):
        r = a
        while parent[r] != r: r = parent[r]
        while parent[a] != r: parent[a], a = r, parent[a]
        return r
    for s, t in zip(ls, lt):
        vs = loop_v[s:s+t]; r0 = find(vs[0])
        for v in vs[1:]:
            r = find(v)
            if r != r0: parent[r] = r0
    root = np.array([find(v) for v in range(nv)])
    _, vis = np.unique(root, return_inverse=True)
    face_isl = vis[loop_v[ls]]
    ni = int(vis.max()) + 1
    zmin = np.full(ni, 9.); zmax = np.full(ni, -9.); np.minimum.at(zmin, vis, co[:, 2]); np.maximum.at(zmax, vis, co[:, 2])
    xmin = np.full(ni, 9.); xmax = np.full(ni, -9.); np.minimum.at(xmin, vis, co[:, 0]); np.maximum.at(xmax, vis, co[:, 0])
    # turret footprint centre from high faces
    hi = fc[:, 2] > ZHI
    tc = np.median(fc[hi][:, :2], axis=0)
    d2 = np.linalg.norm(fc[:, :2] - tc, axis=1)
    face_t = (fc[:, 2] > ZC) & ((d2 < R_T) | (fc[:, 2] > ZHI))
    frac = np.bincount(face_isl, face_t, ni) / np.bincount(face_isl, None, ni)
    isl_turret = (frac >= 0.5) & (zmax > ZC + 0.013)
    # One connected mesh (museum model): islands are meaningless, cut per face.
    turret_f = face_t if FACE_LEVEL else isl_turret[face_isl]
    # gun axis: turret faces clearly ahead of the turret body
    tv = fc[turret_f]
    fwd = tv[tv[:, 1] < -0.24]
    p0 = fwd.mean(0); u, s, vt = np.linalg.svd(fwd - p0); d = vt[0]
    if d[1] > 0: d = -d
    # base ring centre: turret faces near the bottom of the turret
    base = tv[tv[:, 2] < ZC + 0.023]
    ring = np.array([(base[:, 0].min()+base[:, 0].max())/2, (base[:, 1].min()+base[:, 1].max())/2])
    # project ring centre onto the gun line (in XY) to see consistency
    dxy = d[:2]/np.linalg.norm(d[:2])
    off = ring - p0[:2]; lateral = off[0]*dxy[1] - off[1]*dxy[0]
    yaw = math.degrees(math.atan2(dxy[0], -dxy[1]))  # 0 = straight ahead (-Y)
    # distance of turret faces to gun axis, param along axis
    rel = fc - p0; t = rel @ d; dist = np.linalg.norm(rel - np.outer(t, d), axis=1)
    report("DBG nturret=%d nfwd=%d p0=%s d=%s tc=%s distq=%s" % (turret_f.sum(), len(fwd), p0.round(3), d.round(3), tc.round(3), np.percentile(dist[turret_f],[5,50,95]).round(3) if turret_f.any() else None))
    wall = turret_f & (dist > 0.075)
    t_wall = t[wall].max()
    gun_f = turret_f & (dist < 0.06) & (t > t_wall - 0.04)
    # tracks + road wheels: low outer islands
    side = np.where(xmin > 0.14, 1, np.where(xmax < -0.14, -1, 0))
    isl_track = (side != 0) & (zmax < TRACK_ZMAX)
    track_f = ((np.abs(fc[:, 0]) > 0.17) & (fc[:, 2] < TRACK_ZMAX) if FACE_LEVEL else isl_track[face_isl]) & ~turret_f
    trackL_f = track_f & (fc[:, 0] > 0)   # Blender +X is the tank's left when the front is -Y
    trackR_f = track_f & (fc[:, 0] < 0)
    hull_f = ~(turret_f | track_f)
    res = dict(turret_centre=tc.tolist(), ring=ring.tolist(), gun_point=p0.tolist(), gun_dir=d.tolist(), yaw_deg=yaw,
               ring_lateral_from_gun_axis=float(lateral), t_wall=float(t_wall), t_tip=float(t[gun_f].max()),
               counts=dict(hull=int(hull_f.sum()), turret=int((turret_f & ~gun_f).sum()), gun=int(gun_f.sum()),
                           trackL=int(trackL_f.sum()), trackR=int(trackR_f.sum())))
    report('ANALYSIS ' + json.dumps(res))
    labels = np.zeros(nf, np.int8)
    labels[turret_f] = 1; labels[gun_f] = 2; labels[trackL_f] = 3; labels[trackR_f] = 4
    return labels, res
