# Headless: blender -b --python bake_hand_detail.py -- raw/<soldier>.fbx raw/tripo_hand_scan_4k.glb work
#
# Skin detail for the soldier's hands (2026-11-23, "손 피부 디테일부터"). The soldier's hands are a small corner of a
# whole-body 4K texture, so close to the lens they had no skin at all. The high-resolution Tripo hand scan (1.9 M
# triangles, 4K: knuckle creases, nails, veins) is bent onto each of the soldier's hands at rest, joint by joint, and its
# surface baked into the soldier's own textures (tangent normal + colour) where the hands lie in the UV:
#   1. soldier: subdivided once, as retarget_arms.py does (same surface, same tangents)
#   2. scan: brought to a standard frame (wrist at the origin, fingers +Y, back of the hand +Z, 19 cm to the middle
#      fingertip), its joints from the landmarks placed by eye on its back view (2026-11-23)
#   3. every scan segment carried onto the soldier's same segment: head to head, along the bone, palm side to palm side,
#      its length to the bone's length and its thickness to the soldier's own finger there (the surfaces meet)
#   4. Cycles bake, selected to active, into copies of the soldier's textures; the colour matched in mean to the
#      soldier's own skin there, the edge of the baked area feathered into the old texture at the wrist
# Out: work/T_soldier_BaseColor_detail.png, work/T_soldier_Normal_detail.png, previews work/detail_*.png
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index('--') + 1:]
fbx, scan_path, out = args[0], args[1], args[2]
os.makedirs(out, exist_ok=True)
FING = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
# scan landmarks in its standard frame (right hand, back up), metres: wrist and per finger three joints and the tip
LAND = {
    'W': (0.00625, 0.02625),
    'Thumb': [(-0.01875, 0.0425), (-0.0425, 0.085), (-0.0575, 0.10125), (-0.069, 0.118)],
    'Index': [(-0.0165, 0.1035), (-0.027, 0.135), (-0.032, 0.15625), (-0.0350, 0.1765)],
    'Middle': [(0.003, 0.09875), (-0.002, 0.1425), (-0.0025, 0.168), (-0.0025, 0.1895)],
    'Ring': [(0.01875, 0.103), (0.02125, 0.13375), (0.024, 0.15875), (0.0258, 0.180)],
    'Pinky': [(0.03375, 0.09875), (0.04325, 0.118), (0.04825, 0.1325), (0.0525, 0.148)],
}

def unit(v):
    n = np.linalg.norm(v); return v / n if n > 1e-12 else v
def orth(v, a): return unit(v - np.dot(v, a) * a)
def coords(me):
    a = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', a); return a.reshape(-1, 3)
def set_coords(me, a):
    me.vertices.foreach_set('co', a.reshape(-1)); me.update()

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- 1. soldier
bpy.ops.import_scene.fbx(filepath=fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')
for m in list(body.modifiers):
    if m.type == 'ARMATURE': body.modifiers.remove(m)
mw = body.matrix_world.copy(); body.parent = None; body.matrix_world = mw   # keep the armature's 0.01 scale
bpy.context.view_layer.objects.active = body
for o in bpy.data.objects: o.select_set(o == body)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
sub = body.modifiers.new('sub', 'SUBSURF'); sub.levels = 1; sub.render_levels = 1; sub.uv_smooth = 'PRESERVE_CORNERS'; sub.quality = 3
bpy.ops.object.modifier_apply(modifier='sub')
for p in body.data.polygons: p.use_smooth = True
heads = {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}
groups = {g.index: g.name for g in body.vertex_groups}
mat = body.data.materials[0]
def find_image(word):
    for n in mat.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and word in n.image.name.lower(): return n.image
    for im in bpy.data.images:
        if word in im.name.lower(): return im
img_color = bpy.data.images.load(os.path.join(out, 'T_soldier_BaseColor.png'))
img_normal = bpy.data.images.load(os.path.join(out, 'T_soldier_Normal.png')); img_normal.colorspace_settings.name = 'Non-Color'
print('SOLDIER verts', len(body.data.vertices), 'colour', img_color.name, tuple(img_color.size), 'normal', img_normal.name, tuple(img_normal.size))
W_, H_ = img_color.size
if tuple(img_normal.size) != (W_, H_): img_normal.scale(W_, H_)   # the skin detail needs the colour map's resolution
def pixels(im): a = np.empty(W_ * H_ * 4, dtype=np.float32); im.pixels.foreach_get(a); return a.reshape(H_, W_, 4)
orig_color, orig_normal = pixels(img_color), pixels(img_normal)
bake_color = bpy.data.images.new('bake_color', W_, H_, alpha=True, float_buffer=False); bake_color.colorspace_settings.name = 'sRGB'
bake_normal = bpy.data.images.new('bake_normal', W_, H_, alpha=True, float_buffer=False); bake_normal.colorspace_settings.name = 'Non-Color'
for im in (bake_color, bake_normal): im.pixels.foreach_set(np.zeros(W_ * H_ * 4, dtype=np.float32))

def hand_low(side):
    pre = 'mixamorig:' + side
    names = {pre + 'Hand'} | {pre + 'Hand%s%d' % (f, k) for f in FING for k in (1, 2, 3, 4)}
    share = np.zeros(len(body.data.vertices)); total = np.zeros(len(body.data.vertices))
    for v in body.data.vertices:
        for g in v.groups:
            total[v.index] += g.weight
            if groups[g.group] in names: share[v.index] += g.weight
    keep = share / np.maximum(total, 1e-9) >= 0.5
    low = body.copy(); low.data = body.data.copy(); low.name = 'low_' + side; scene.collection.objects.link(low)
    bm = bmesh.new(); bm.from_mesh(low.data)
    bm.faces.ensure_lookup_table()
    drop = [f for f in bm.faces if not all(keep[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    bm.to_mesh(low.data); bm.free()
    # weights for thickness measurement, per vertex: dominant hand bone
    return low, keep

# ---------------------------------------------------------------- 2. scan in its standard frame
bpy.ops.import_scene.gltf(filepath=scan_path)
scan = [o for o in bpy.data.objects if o.type == 'MESH' and o != body and not o.name.startswith('low_')][-1]
scan.parent = None
bpy.context.view_layer.objects.active = scan
for o in bpy.data.objects: o.select_set(o == scan)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co = coords(scan.data); centre = co.mean(0)
w, v = np.linalg.eigh(np.cov((co - centre).T)); normal, width, length = v[:, 0], v[:, 1], v[:, 2]
along = (co - centre) @ length
far = np.abs(((co - centre) @ width)[along > np.percentile(along, 80)]).mean()
near = np.abs(((co - centre) @ width)[along < np.percentile(along, 20)]).mean()
if far < near: length = -length
width = np.cross(normal, length)
R = np.stack([width, length, normal]); co = (co - centre) @ R.T
tips = co[co[:, 1] > np.percentile(co[:, 1], 92)][:, 2].mean()
mid = co[(co[:, 1] > np.percentile(co[:, 1], 35)) & (co[:, 1] < np.percentile(co[:, 1], 55))][:, 2].mean()
if tips > mid: co = co * np.array([-1.0, 1.0, -1.0])
wrist = co[co[:, 1] < np.percentile(co[:, 1], 2)].mean(0)
co = co - np.array([wrist[0], co[:, 1].min(), wrist[2]])
co = co * (0.19 / co[:, 1].max())
set_coords(scan.data, co)
scan.data.flip_normals() if False else None
print('SCAN verts', len(co), 'size', co.min(0).round(3), co.max(0).round(3))
bvh = BVHTree.FromObject(scan, bpy.context.evaluated_depsgraph_get())
def mid_depth(x, y):
    t = bvh.ray_cast(Vector((x, y, 1.0)), Vector((0, 0, -1))); b = bvh.ray_cast(Vector((x, y, -1.0)), Vector((0, 0, 1)))
    if t[0] is None or b[0] is None:
        p = bvh.find_nearest(Vector((x, y, 0.01)))[0]; return np.array(p)
    return np.array([x, y, (t[0].z + b[0].z) / 2])
JS = {'W': mid_depth(*LAND['W'])}
for f in FING: JS[f] = [mid_depth(*p) for p in LAND[f]]
SCAN_CO = co.copy()

# geometric weights of the scan to its own segments (as in the earlier graft)
BONES = ['Hand'] + ['%s%d' % (f, k + 1) for f in FING for k in range(3)]
def segs(J):
    S = {'Hand': [(J['W'], J[f][0]) for f in FING[1:]] + [(J['Index'][0], J['Pinky'][0]), (J['W'], J['Thumb'][0]), (J['W'] + np.array([0, -0.05, 0]), J['W'])]}
    for f in FING:
        for k in range(3): S['%s%d' % (f, k + 1)] = [(J[f][k], J[f][k + 1])]
    return S
def seg_dist(P, a, b):
    ab = b - a; t = np.clip(((P - a) @ ab) / max(np.dot(ab, ab), 1e-12), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)
def weights(P, J):
    S = segs(J)
    D = np.stack([np.min([seg_dist(P, a, b) for a, b in S[n]], axis=0) for n in BONES], 1)
    dmin = D.min(1, keepdims=True)
    Wt = np.where(D <= dmin * 2.0 + 0.004, 1.0 / (D + 0.004) ** 4, 0.0)   # soft across the joints: no steps
    return Wt / Wt.sum(1, keepdims=True), D
WS, DS = weights(SCAN_CO, JS)
print('SCAN weights done')

def radius(P, mask, a, b):
    sel = P[mask]
    if len(sel) < 10: return None
    ab = unit(b - a); d = sel - a; d = d - np.outer(d @ ab, ab)
    return float(np.median(np.linalg.norm(d, axis=1)))

# ---------------------------------------------------------------- 3. carry the scan onto each soldier hand
highs = {}
for side in ('Right', 'Left'):
    pre = 'mixamorig:' + side
    low, keep = hand_low(side)
    # soldier joints: wrist, and the four heads of each finger
    JT = {'W': heads[pre + 'Hand']}
    for f in FING: JT[f] = [heads[pre + 'Hand%s%d' % (f, k)] for k in (1, 2, 3, 4)]
    # the scan is a right hand: for the left it is mirrored (in Blender's axes the left hand is the mirror image)
    if side == 'Left':
        mir = np.array([-1.0, 1.0, 1.0]); P = SCAN_CO * mir
        J = {'W': JS['W'] * mir}
        for f in FING: J[f] = [p * mir for p in JS[f]]
    else:
        P = SCAN_CO.copy(); J = JS
    # soldier finger radii from its own surface (vertices dominated by each bone)
    LV = np.array([list(v.co) for v in body.data.vertices])
    dom = {}
    for vv in body.data.vertices:
        if not keep[vv.index] or not vv.groups: continue
        g = max(vv.groups, key=lambda g: g.weight); dom[vv.index] = groups[g.group]
    def soldier_radius(bone, a, b):
        idx = [i for i, n in dom.items() if n == pre + bone]
        if len(idx) < 6: return None
        return radius(LV, np.isin(np.arange(len(LV)), idx), a, b)
    def palm(Jx): return unit(np.cross(Jx['Index'][0] - Jx['W'], Jx['Pinky'][0] - Jx['W']))
    pn_s, pn_t = palm(J), palm(JT)
    T = {}
    # palm: affine through wrist and knuckles, thickness by the palm's own thickness
    src = [J['W']] + [J[f][0] for f in FING]; dst = [JT['W']] + [JT[f][0] for f in FING]
    span_s = np.linalg.norm(J['Pinky'][0] - J['Index'][0]); span_t = np.linalg.norm(JT['Pinky'][0] - JT['Index'][0])
    th = span_t / span_s
    src2 = src + [p + pn_s * 0.012 for p in src] + [p - pn_s * 0.012 for p in src]
    dst2 = dst + [p + pn_t * 0.012 * th for p in dst] + [p - pn_t * 0.012 * th for p in dst]
    A = np.linalg.lstsq(np.hstack([np.array(src2), np.ones((len(src2), 1))]), np.array(dst2), rcond=None)[0].T
    T['Hand'] = A
    hs = 1.0 if np.dot(np.cross(unit(J['Index'][1] - J['Index'][0]), pn_s), unit(J['Pinky'][0] - J['Index'][0])) > 0 else -1.0
    ht = 1.0 if np.dot(np.cross(unit(JT['Index'][1] - JT['Index'][0]), pn_t), unit(JT['Pinky'][0] - JT['Index'][0])) > 0 else -1.0
    for f in FING:
        for k in range(3):
            ds = unit(J[f][k + 1] - J[f][k]); dt = unit(JT[f][k + 1] - JT[f][k])
            zs = orth(pn_s, ds); zt = orth(pn_t, dt)
            Fs = np.stack([hs * np.cross(ds, zs), ds, zs], 1); Ft = np.stack([ht * np.cross(dt, zt), dt, zt], 1)
            ls = np.linalg.norm(J[f][k + 1] - J[f][k]); lt = np.linalg.norm(JT[f][k + 1] - JT[f][k])
            bi = BONES.index('%s%d' % (f, k + 1))
            rs = radius(P, WS[:, bi] > 0.6, J[f][k], J[f][k + 1])
            rt = soldier_radius('Hand%s%d' % (f, k + 1), JT[f][k], JT[f][k + 1])
            thick = (rt / rs) if (rs and rt) else th
            thick = float(np.clip(thick, 0.6 * th, 1.5 * th))
            L = Ft @ np.diag([thick, lt / ls, thick]) @ Fs.T
            T['%s%d' % (f, k + 1)] = np.hstack([L, (JT[f][k] - L @ J[f][k]).reshape(3, 1)])
            if k == 0: print('CARRY %s %s: length x%.2f thickness x%.2f (palm x%.2f)' % (side, f, lt / ls, thick, th))
    Ph = np.hstack([P, np.ones((len(P), 1))])
    Q = np.zeros_like(P)
    for b, n in enumerate(BONES): Q += WS[:, b:b + 1] * (Ph @ T[n].T)
    high = scan.copy(); high.data = scan.data.copy(); high.name = 'high_' + side; scene.collection.objects.link(high)
    set_coords(high.data, Q)
    if side == 'Left': high.data.flip_normals()
    highs[side] = (high, low)
    LC = coords(low.data)
    print('DBG %s soldier wrist %s index1 %s | scan wrist %s index1 %s' % (side, JT['W'].round(3), JT['Index'][0].round(3), J['W'].round(3), J['Index'][0].round(3)))
    print('DBG %s low bounds %s %s | carried bounds %s %s | nan %d' % (side, LC.min(0).round(3), LC.max(0).round(3), np.nanmin(Q, 0).round(3), np.nanmax(Q, 0).round(3), int(np.isnan(Q).sum())))
    print('DBG %s low matrix %s high matrix %s' % (side, [round(x, 3) for x in low.matrix_world.to_translation()], [round(x, 3) for x in high.matrix_world.to_scale()]))
    gap = np.median([BVHTree.FromObject(high, bpy.context.evaluated_depsgraph_get()).find_nearest(Vector(v.co))[3] for v in list(low.data.vertices)[::40]])
    print('FIT %s: median gap soldier surface -> carried scan %.1f mm (soldier units)' % (side, gap * 1000))
scan.hide_render = True; scan.hide_set(True); body.hide_render = True
if os.environ.get('FIT_ONLY'): sys.exit(0)

# ---------------------------------------------------------------- 4. project the scan onto the soldier's hand texels
# (no Cycles rays: every texel of the hand takes the scan's nearest surface point, so nothing misses)
def tri_raster(P):
    x0, y0 = np.floor(P.min(0)).astype(int); x1, y1 = np.ceil(P.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, W_ - 1), min(y1, H_ - 1)
    if x1 < x0 or y1 < y0: return None
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    a, b, c = P
    d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(d) < 1e-12: return None
    l1 = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / d
    l2 = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / d
    l3 = 1 - l1 - l2
    e = -1.0 / max(np.ptp(P[:, 0]) + np.ptp(P[:, 1]), 1.0)
    ok = (l1 >= e) & (l2 >= e) & (l3 >= e)
    if not ok.any(): return None
    L = np.stack([l1[ok], l2[ok], l3[ok]], 1); L = np.clip(L, 0, None); L /= L.sum(1, keepdims=True)
    return gy[ok].astype(int), gx[ok].astype(int), L

def floats(seq, attr, n):
    a = np.empty(len(seq) * n, dtype=np.float64); seq.foreach_get(attr, a); return a.reshape(-1, n) if n > 1 else a
def ints(seq, attr):
    a = np.empty(len(seq), dtype=np.int64); seq.foreach_get(attr, a); return a

# the scan's colour map (glTF base colour)
smat = scan.data.materials[0]
sb = next(n for n in smat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
simg = sb.inputs['Base Color'].links[0].from_node.image if sb.inputs['Base Color'].is_linked else None
SW, SH = simg.size
scol = np.empty(SW * SH * 4, dtype=np.float32); simg.pixels.foreach_get(scol); scol = scol.reshape(SH, SW, 4)
print('SCAN colour', simg.name, SW, SH, 'mean', scol[..., :3].mean((0, 1)).round(3))

DETAIL = 0.6   # strength of the skin detail in the normal
det_n = np.zeros((H_, W_, 3), dtype=np.float32); det_l = np.zeros((H_, W_), dtype=np.float32)
det_w = np.zeros((H_, W_), dtype=np.float32); mask = np.zeros((H_, W_), dtype=bool)
dg = bpy.context.evaluated_depsgraph_get()
for side, (high, low) in highs.items():
    me = low.data; me.calc_tangents(); me.calc_loop_triangles()
    lv = ints(me.loops, 'vertex_index'); lco = coords(me)[lv]
    ln = floats(me.corner_normals, 'vector', 3); lt = floats(me.loops, 'tangent', 3); ls = floats(me.loops, 'bitangent_sign', 1)
    luv = floats(me.uv_layers.active.data, 'uv', 2) * np.array([W_, H_])
    tl = np.array([list(t.loops) for t in me.loop_triangles])
    PY, PX, POS, NRM, TAN, SGN = [], [], [], [], [], []
    for tri in tl:
        r = tri_raster(luv[tri])
        if r is None: continue
        py, px, L = r
        PY.append(py); PX.append(px); POS.append(L @ lco[tri]); NRM.append(L @ ln[tri]); TAN.append(L @ lt[tri]); SGN.append(L @ ls[tri])
    PY, PX = np.concatenate(PY), np.concatenate(PX); POS = np.concatenate(POS)
    NRM = np.concatenate(NRM); NRM /= np.linalg.norm(NRM, axis=1, keepdims=True)
    TAN = np.concatenate(TAN); TAN -= NRM * (TAN * NRM).sum(1, keepdims=True); TAN /= np.maximum(np.linalg.norm(TAN, axis=1, keepdims=True), 1e-9)
    SGN = np.sign(np.concatenate(SGN)); SGN[SGN == 0] = 1
    BIT = SGN[:, None] * np.cross(NRM, TAN)
    # the carried scan: nearest point, its smooth normal and its uv
    hm = high.data
    hv = coords(hm); hvn = floats(hm.vertex_normals, 'vector', 3)
    # the same scan heavily smoothed: its normal is the broad shape, the difference is the skin detail alone
    sm = high.modifiers.new('sm', 'SMOOTH'); sm.factor = 1.0; sm.iterations = 40
    bpy.context.view_layer.update(); dgs = bpy.context.evaluated_depsgraph_get(); dgs.update()
    ev = high.evaluated_get(dgs); smesh = ev.to_mesh()
    hvs = floats(smesh.vertex_normals, 'vector', 3); ev.to_mesh_clear(); high.modifiers.remove(sm)
    bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get(); dg.update()
    pls = ints(hm.polygons, 'loop_start'); hlv = ints(hm.loops, 'vertex_index'); huv = floats(hm.uv_layers.active.data, 'uv', 2)
    tree = BVHTree.FromObject(high, dg)
    HIT = np.zeros((len(POS), 3)); FI = np.zeros(len(POS), dtype=np.int64); DI = np.full(len(POS), 1e9)
    for i, p in enumerate(POS):
        loc, nor, idx, dist = tree.find_nearest(Vector(p), 0.01)
        if loc is not None: HIT[i] = loc; FI[i] = idx; DI[i] = dist
    found = DI < 1e8
    l0 = pls[FI]; corners = np.stack([l0, l0 + 1, l0 + 2], 1)
    A, B, C = (hv[hlv[corners[:, k]]] for k in range(3))
    v0, v1, v2 = B - A, C - A, HIT - A
    d00, d01, d11 = (v0 * v0).sum(1), (v0 * v1).sum(1), (v1 * v1).sum(1); d20, d21 = (v2 * v0).sum(1), (v2 * v1).sum(1)
    den = np.maximum(d00 * d11 - d01 * d01, 1e-20); bv = (d11 * d20 - d01 * d21) / den; bw = (d00 * d21 - d01 * d20) / den; bu = 1 - bv - bw
    BC = np.clip(np.stack([bu, bv, bw], 1), 0, 1); BC /= np.maximum(BC.sum(1, keepdims=True), 1e-9)
    NH = sum(BC[:, k:k + 1] * hvn[hlv[corners[:, k]]] for k in range(3)); NH /= np.maximum(np.linalg.norm(NH, axis=1, keepdims=True), 1e-9)
    NS = sum(BC[:, k:k + 1] * hvs[hlv[corners[:, k]]] for k in range(3)); NS /= np.maximum(np.linalg.norm(NS, axis=1, keepdims=True), 1e-9)
    flip = np.sign((NS * NRM).sum(1, keepdims=True) + 1e-9)       # inward or outward, the scan's side facing the soldier's
    NH *= flip; NS *= flip
    face_s = (NS * NRM).sum(1)
    NH = NRM + DETAIL * (NH - NS); NH /= np.maximum(np.linalg.norm(NH, axis=1, keepdims=True), 1e-9)
    UVH = sum(BC[:, k:k + 1] * huv[corners[:, k]] for k in range(3))
    sx = np.clip((UVH[:, 0] % 1.0) * SW, 0, SW - 1).astype(int); sy = np.clip((UVH[:, 1] % 1.0) * SH, 0, SH - 1).astype(int)
    LUM = (scol[sy, sx, :3] @ np.array([0.2126, 0.7152, 0.0722])).astype(np.float32)
    # trust: full within 1.5 mm of the soldier's surface and facing the same way, none past 4 mm or across
    face = (NH * NRM).sum(1)
    W8 = np.clip(1 - (DI - 0.0015) / 0.0025, 0, 1) * np.clip((face_s - 0.2) / 0.4, 0, 1) * found
    TS = np.stack([(NH * TAN).sum(1), (NH * BIT).sum(1), face], 1)
    det_n[PY, PX] = TS; det_l[PY, PX] = LUM; det_w[PY, PX] = W8; mask[PY, PX] = True
    print('PROJECT %s: texels %d, within 1.5 mm %d%%, trusted mean %.2f, median gap %.2f mm' % (side, len(POS), int(100 * (DI < 0.0015).mean()), W8.mean(), np.median(DI[found]) * 1000))

# ---------------------------------------------------------------- 5. composite into the soldier's own textures
def box(a, r):
    c = np.cumsum(np.cumsum(np.pad(a, ((r + 1, r), (r + 1, r))), 0), 1)
    return c[2 * r + 1:, 2 * r + 1:] - c[:-2 * r - 1, 2 * r + 1:] - c[2 * r + 1:, :-2 * r - 1] + c[:-2 * r - 1, :-2 * r - 1]
print('HAND texels', int(mask.sum()), 'of', W_ * H_)
m = mask.astype(np.float32)
# colour: only the scan's light and dark (creases, nails, knuckles) over the soldier's own skin, relative to its local mean
lmean = box(det_l * m, 14) / np.maximum(box(m, 14), 1e-6)
ratio = np.where(mask, np.clip(det_l / np.maximum(lmean, 1e-4), 0.88, 1.10), 1.0) ** 0.7   # light touch: creases, not cuts
# edge: fade in from every border of the hand area over ~6 texels, and by trust
inner = box(m, 6) / (13.0 * 13.0)
a = np.clip((inner - 0.5) * 2.0, 0, 1) * m * det_w
print('COLOUR detail ratio p5..p95 %.2f..%.2f, blend mean %.2f' % (np.percentile(ratio[mask], 5), np.percentile(ratio[mask], 95), a[mask].mean()))
new_color = orig_color.copy()
new_color[..., :3] = np.clip(orig_color[..., :3] * (1 - a[..., None] + a[..., None] * ratio[..., None]), 0, 1)
flat = np.array([0.0, 0.0, 1.0], dtype=np.float32)
ts = det_n * a[..., None] + flat * (1 - a[..., None]); ts /= np.maximum(np.linalg.norm(ts, axis=2, keepdims=True), 1e-9)
new_normal = orig_normal.copy()
enc = ts * 0.5 + 0.5
# where the scan is trusted it replaces the old normal; elsewhere the old normal stays
new_normal[..., :3] = orig_normal[..., :3] * (1 - a[..., None]) + enc * a[..., None]
def save(arr, name, colorspace):
    im = bpy.data.images.new(name, W_, H_, alpha=True); im.colorspace_settings.name = colorspace
    im.pixels.foreach_set(arr.reshape(-1).astype(np.float32)); im.filepath_raw = os.path.join(out, name + '.png'); im.file_format = 'PNG'; im.save()
save(new_color, 'T_soldier_BaseColor_detail', 'sRGB')
save(new_normal, 'T_soldier_Normal_detail', 'Non-Color')
print('SAVED')

# ---------------------------------------------------------------- 6. skin mask + tiling skin micro-normal (URP Lit detail map)
# the hand texels can't hold pores; a small tiling normal (pores, fine cross wrinkles) laid over the skin only does
chroma = orig_color[..., :3] / np.maximum(orig_color[..., :3].sum(2, keepdims=True), 1e-4)
skin_c = chroma[mask & (a > 0.5)].mean(0); print('SKIN chroma', skin_c.round(3))
dist = np.linalg.norm(chroma - skin_c, axis=2)
lum0 = orig_color[..., :3].mean(2)
skin = np.exp(-(dist / 0.035) ** 2) * np.clip((lum0 - 0.06) / 0.08, 0, 1)
skin = np.maximum(skin, a)
skin = box(skin.astype(np.float32), 3) / 49.0
skin = np.clip((skin - 0.25) / 0.5, 0, 1)
print('SKIN mask texels > 0.5: %d (hand %d)' % (int((skin > 0.5).sum()), int(mask.sum())))
mk = np.ones((H_, W_, 4), dtype=np.float32); mk[..., 0] = skin; mk[..., 1] = skin; mk[..., 2] = skin; mk[..., 3] = skin
save(mk, 'T_soldier_SkinMask', 'Non-Color')
N = 512; rng = np.random.default_rng(1950)
def noise(sigma_px, aniso=1.0, angle=0.0):
    w = rng.standard_normal((N, N)); F = np.fft.fft2(w)
    fy, fx = np.meshgrid(np.fft.fftfreq(N), np.fft.fftfreq(N), indexing='ij')
    ca, sa = math.cos(angle), math.sin(angle); u = fx * ca + fy * sa; v = -fx * sa + fy * ca
    g = np.exp(-2 * (math.pi * sigma_px) ** 2 * (u ** 2 + (v * aniso) ** 2))
    r = np.real(np.fft.ifft2(F * g)); return (r - r.mean()) / r.std()
h = np.zeros((N, N))
# pores: sparse small pits
pits = noise(1.2); h -= np.clip(pits - 1.6, 0, None) * 0.9
# fine skin grain and two crossing sets of fine wrinkles (the diamond pattern of skin)
h += noise(0.9) * 0.18 + noise(2.5) * 0.25
h += np.clip(noise(1.0, 6.0, 0.6), None, 0.4) * 0.22 + np.clip(noise(1.0, 6.0, -0.7), None, 0.4) * 0.22
gy, gx = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / 2, (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2
k = 0.9; nm = np.stack([-gx * k, -gy * k, np.ones_like(h)], 2); nm /= np.linalg.norm(nm, axis=2, keepdims=True)
tile = np.ones((N, N, 4), dtype=np.float32); tile[..., :3] = nm * 0.5 + 0.5
im = bpy.data.images.new('T_skin_micro_normal', N, N, alpha=True); im.colorspace_settings.name = 'Non-Color'
im.pixels.foreach_set(tile.reshape(-1)); im.filepath_raw = os.path.join(out, 'T_skin_micro_normal.png'); im.file_format = 'PNG'; im.save()
print('MICRO saved, tilt p95 %.1f deg' % np.degrees(np.arccos(np.percentile(nm[..., 2], 5))))


# ---------------------------------------------------------------- previews: the soldier's hands with the new textures
for o in list(bpy.data.objects):
    if o.name.startswith('high_') or o == scan: o.hide_render = True
for side, (high, low) in highs.items():
    low.data.materials.clear(); m = bpy.data.materials.new('pv_' + side); m.use_nodes = True; low.data.materials.append(m)
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    tc = m.node_tree.nodes.new('ShaderNodeTexImage'); tc.image = bpy.data.images['T_soldier_BaseColor_detail']
    tn = m.node_tree.nodes.new('ShaderNodeTexImage'); tn.image = bpy.data.images['T_soldier_Normal_detail']; tn.image.colorspace_settings.name = 'Non-Color'
    nm = m.node_tree.nodes.new('ShaderNodeNormalMap')
    m.node_tree.links.new(tc.outputs[0], bsdf.inputs['Base Color']); m.node_tree.links.new(tn.outputs[0], nm.inputs['Color']); m.node_tree.links.new(nm.outputs[0], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = 0.55
engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
scene.render.resolution_x = 800; scene.render.resolution_y = 800
scene.world = bpy.data.worlds.new('w'); scene.world.use_nodes = True; scene.world.node_tree.nodes['Background'].inputs[1].default_value = 1.6
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sun.data.energy = 4.0; sun.rotation_euler = (0.7, 0.3, 0.8); scene.collection.objects.link(sun)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = 'ORTHO'
for side, (high, low) in highs.items():
    c = Vector(np.array([list(v.co) for v in low.data.vertices]).mean(0)); span = max(low.dimensions) * 1.3; cam.data.ortho_scale = span
    for vn, d in (('top', (0, 0, 1)), ('bottom', (0, 0, -1)), ('front', (0, -1, 0))):
        d = Vector(d); cam.location = c + d * 1.0
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, 'detail_%s_%s.png' % (side, vn)); bpy.ops.render.render(write_still=True)
