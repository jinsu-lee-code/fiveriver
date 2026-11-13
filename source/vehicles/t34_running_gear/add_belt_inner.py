# blender -b --factory-startup -P add_belt_inner.py -- <in.fbx> <gear json> <out.fbx> [<track basecolor.png> <track normal.png>]
#
# Gives the moving belt of TrackL / TrackR a continuous inner face (2026-11-13).
# Why: TankRunningGear.cs carries every triangle inside the belt band (-beltInside .. +beltHalf from the loop's centre
# line) round the loop. In the decimated 11-10 model the belt's inner face (the side the wheels run on) is welded into
# the idler rim, the sprocket and the road-wheel tyres over long stretches (round the idler 1.37 m, the top run ~4-5 m,
# under each road wheel 0.4-0.8 m): there it lies deeper than -beltInside, so it stays still with the wheels, and the
# moving belt is an open channel there (outer face + side walls only). Standing still the wheels fill the gap; once the
# belt moves, open stretches come round onto the runs and the ends and, with back faces culled, show as two loose edges /
# a floating ribbon.
# What: one strip swept round the whole loop at a fixed offset INNER from the centre line (inside the belt, behind the
# real inner face wherever that exists), across the belt width, facing the wheels. Every point of it lies well inside
# the band, so the game sorts it into the belt and it goes round with it; a strip of constant offset looks the same
# wherever it is carried, so it can never open a gap. It takes one texel of the track texture whose colour is the
# belt's median colour and whose normal is nearly flat (it is the dark underside, seen only through former gaps).
# Nothing else changes: existing vertices, triangles, UVs and normals are kept as they are and the strip is appended.
import bpy, bmesh, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

a = sys.argv[sys.argv.index('--') + 1:]
SRC, GEAR, DST = a[0], a[1], a[2]
BASE = a[3] if len(a) > 3 else None
NORM = a[4] if len(a) > 4 else None
J = json.load(open(GEAR))
INNER = 0.026          # strip offset inward from the loop centre line (closed belt boxes have their inner face at -0.030..-0.046)
X0, X1 = 1.005, 1.480  # across the belt (the plates span 1.00 .. 1.49); stays inside the side walls
STEP = 0.03            # metres between rings round the loop (the idler bend, r ~0.28: chord error < 0.5 mm)
TABLE = 512

def table(loop):
    """the loop sampled evenly by distance, as TankRunningGear.BuildTable does"""
    n = len(loop); q = np.vstack([loop, loop[:1]])
    cum = np.r_[0, np.cumsum(np.hypot(*(q[1:] - q[:-1]).T))]; L = cum[-1]
    s = L * np.arange(TABLE) / TABLE; seg = np.zeros(TABLE, int); k = 0
    for i in range(TABLE):
        while k < n - 1 and cum[k + 1] < s[i]: k += 1
        seg[i] = k
    u = (s - cum[seg]) / np.maximum(1e-6, cum[seg + 1] - cum[seg]); b = (seg + 1) % n
    Z = np.r_[loop[seg, 0] + (loop[b, 0] - loop[seg, 0]) * u, 0]; Y = np.r_[loop[seg, 1] + (loop[b, 1] - loop[seg, 1]) * u, 0]
    Z[TABLE] = Z[0]; Y[TABLE] = Y[0]
    k = np.arange(TABLE + 1); bef = (k + TABLE - 1) % TABLE; aft = (k + 1) % TABLE
    dz = Z[aft] - Z[bef]; dy = Y[aft] - Y[bef]; ln = np.maximum(1e-6, np.hypot(dz, dy))
    return L, Z, Y, dz / ln, dy / ln

def sample(T, s):
    L, Z, Y, TZ, TY = T
    f = s / L * TABLE; i = np.minimum(f.astype(int), TABLE - 1); u = f - i
    g = lambda A: A[i] + (A[i + 1] - A[i]) * u
    return g(Z), g(Y), g(TZ), g(TY)

def pick_uv(me, belt_loops):
    """UV of a belt corner whose texel is closest to the belt's median colour (and has a nearly flat normal)"""
    uvl = me.uv_layers.active.data
    uv = np.array([uvl[i].uv[:] for i in belt_loops])
    if not BASE: return tuple(np.median(uv, 0))
    def texels(path, uv):
        im = bpy.data.images.load(path); W, H = im.size
        px = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, im.channels)
        xi = np.clip((uv[:, 0] % 1.0) * W, 0, W - 1).astype(int); yi = np.clip((uv[:, 1] % 1.0) * H, 0, H - 1).astype(int)
        # a 5x5 neighbourhood mean, so the pick is not a lone speck
        acc = np.zeros((len(uv), 3))
        for dy in range(-2, 3):
            for dx in range(-2, 3):
                acc += px[np.clip(yi + dy, 0, H - 1), np.clip(xi + dx, 0, W - 1), :3]
        return acc / 25
    col = texels(BASE, uv); med = np.median(col, 0)
    cost = np.linalg.norm(col - med, axis=1)
    if NORM:
        nrm = texels(NORM, uv); cost += 0.5 * np.linalg.norm(nrm - np.array([0.5, 0.5, 1.0]), axis=1)
    k = int(np.argmin(cost))
    print('UV pick', uv[k], 'colour', col[k], 'belt median colour', med)
    return tuple(uv[k])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
report = {}
for name, sign in (('TrackL', -1.0), ('TrackR', 1.0)):
    o = bpy.data.objects[name]; me = o.data
    M = np.array(o.matrix_world); Minv = np.linalg.inv(M)
    T = table(np.array(J[name]['loop']).reshape(-1, 2)); L = T[0]
    # corners of the triangles the game carries round (belt band, by triangle centre) for the UV pick
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    w = co @ M[:3, :3].T + M[:3, 3]; tx, ty, tz = sign * -w[:, 0], w[:, 2], -w[:, 1]
    import gear_blender as gb
    loop = np.array(J[name]['loop']).reshape(-1, 2)
    belt_loops = []
    pc = np.array([p.center[:] for p in me.polygons]); pw = pc @ M[:3, :3].T + M[:3, 3]
    cx, cy, cz = sign * -pw[:, 0], pw[:, 2], -pw[:, 1]
    _, n_, al_, _ = gb.project(loop, cz, cy)
    inband = (cx > J['beltInner']) & (n_ < J['beltHalf']) & (n_ > -J['beltInside']) & (np.abs(al_) < J['beltHalf'])
    for p in me.polygons:
        if inband[p.index]: belt_loops.extend(p.loop_indices)
    uv0 = pick_uv(me, belt_loops)

    # keep the delivered corner normals through the edit
    cn = np.array([v.vector[:] for v in me.corner_normals], dtype=np.float32)
    at = me.attributes.new('nrm_tmp', 'FLOAT_VECTOR', 'CORNER'); at.data.foreach_set('vector', cn.ravel())
    v0, f0 = len(me.vertices), len(me.polygons)
    bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active; nrl = bm.loops.layers.float_vector['nrm_tmp']
    n = int(round(L / STEP)); s = L * np.arange(n) / n
    pz, py, tz_, ty_ = sample(T, s)
    oz, oy = ty_, -tz_                      # outward, as TankRunningGear.Project measures it
    zz, yy = pz - INNER * oz, py - INNER * oy
    xs = np.array([X0, (X0 + X1) / 2, X1])
    rings = []
    for i in range(n):
        ring = []
        for xv in xs:
            wpt = np.array([-(sign * xv), -zz[i], yy[i], 1.0])     # tank (x, y, z) -> Blender world
            lp = Minv @ wpt
            ring.append(bm.verts.new(lp[:3].tolist()))
        rings.append(ring)
    inward_world = np.c_[np.zeros(n), oz, -oy]                    # -outward: tank (0, -oy, -oz) -> Blender (0, oz, -oy)
    R = M[:3, :3]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        for c in range(len(xs) - 1):
            for tri in ((rings[i][c], rings[j][c], rings[j][c + 1]), (rings[i][c], rings[j][c + 1], rings[i][c + 1])):
                faces.append((bm.faces.new(tri), i))
    bm.normal_update()
    flipped = 0
    for f, i in faces:
        nw = (R @ np.array(f.normal[:])); nw /= max(np.linalg.norm(nw), 1e-9)
        # face the wheels: compare the face normal (world) with the inward direction at this ring
        if nw @ inward_world[i] < 0:
            f.normal_flip(); flipped += 1
        nl = R.T @ inward_world[i]; nl = nl / np.linalg.norm(nl)    # custom normal in object space (normals go by R^T)
        for l in f.loops:
            l[uvl].uv = uv0
            l[nrl] = nl.tolist()
    bm.to_mesh(me); bm.free()
    nr = np.empty(len(me.loops) * 3, dtype=np.float32); me.attributes['nrm_tmp'].data.foreach_get('vector', nr)
    nr = nr.reshape(-1, 3); nr /= np.maximum(np.linalg.norm(nr, axis=1), 1e-8)[:, None]
    me.attributes.remove(me.attributes['nrm_tmp'])
    me.normals_split_custom_set(nr.tolist())
    me.validate(verbose=False)
    report[name] = dict(verts_before=v0, verts_after=len(me.vertices), polys_before=f0, polys_after=len(me.polygons),
                        strip_rings=n, strip_faces=len(faces), flipped=flipped, uv=[round(float(v), 5) for v in uv0])

meshes = [ob for ob in bpy.context.scene.objects if ob.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for ob in meshes: ob.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.export_scene.fbx(filepath=DST, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
                         add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=False, path_mode='STRIP')
print('BELT_INNER_REPORT', json.dumps(report))
