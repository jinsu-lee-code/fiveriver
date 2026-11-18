# New hands for the KPA rifleman from the VARCO hand, cleaned up in Blender (2026-11-18).
#
# The first try (2026-11-15, reverted) carried the generated hand straight onto the rig: its triangles were
# uneven and every vertex hung on one bone, so gripping the rifle threw out needle-like spikes and the stub of
# wrist tore at the cuff. Here the hand is first made over:
#   1. welded (it came with every triangle's corners apart) and rebuilt as an even surface (voxel remesh,
#      smoothing, then thinned to a game count); its colour and surface detail are baked back from the original;
#   2. given a skeleton found from its outline (forearm, hand, three bones per finger) and bound with Blender's
#      automatic weights, so each joint bends over a soft band and no vertex follows the wrong finger;
#   3. carried bone by bone into the bind pose of the game's rig (the knuckles onto the rig's knuckles, each finger
#      segment joint to joint), mirrored for the right hand. The stub of wrist rides the forearm bone, so it turns
#      with the sleeve.
#
#   blender -b --factory-startup -P build_hands.py -- <old SK_CH_KPA_Rifleman.fbx> <hand.glb> <unity_bones.txt> <out.bin>
# Writes <out.bin> (both hands, one submesh) and <out.bin>_BaseColor.png / _Normal.png.
import bpy, bmesh, sys, os, struct
import numpy as np

old_fbx, hand_glb, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
P = 'mixamorig:'
VOXEL, FACES, TEX, STUB, WRIST_REACH = 0.0045, 9000, 2048, 0.05, 0.8
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=old_fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
OJ = {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}
for o in list(bpy.data.objects): bpy.data.objects.remove(o)

# ---- 1. make the hand over
bpy.ops.import_scene.gltf(filepath=hand_glb)
src = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
src.data.transform(src.matrix_world); src.matrix_world.identity()
bm = bmesh.new(); bm.from_mesh(src.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5); bm.to_mesh(src.data); bm.free()
hand = src.copy(); hand.data = src.data.copy(); hand.name = 'hand'; bpy.context.collection.objects.link(hand)
def apply(o, m):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)
r = hand.modifiers.new('remesh', 'REMESH'); r.mode = 'VOXEL'; r.voxel_size = VOXEL; r.adaptivity = 0.0; apply(hand, r)
s = hand.modifiers.new('smooth', 'CORRECTIVE_SMOOTH'); s.iterations = 6; s.factor = 0.5; s.use_only_smooth = True; apply(hand, s)
d = hand.modifiers.new('decimate', 'DECIMATE'); d.ratio = min(1.0, FACES / max(1, len(hand.data.polygons))); apply(hand, d)
# a clean straight cut a little up the forearm, closed: cut along whole faces the stub's end came out in a ragged
# fringe that stood out of the cuff
zs_ = np.array([v.co.z for v in hand.data.vertices])
prof_ = []
for z in np.arange(zs_.min() + 0.25, zs_.min() + 0.55, 0.005):
    sl = np.array([v.co[:] for v in hand.data.vertices if z < v.co.z <= z + 0.005])
    if len(sl) > 5: prof_.append((np.ptp(sl[:, 0]), z))
WRIST_Z = min(prof_)[1]
bm = bmesh.new(); bm.from_mesh(hand.data)
res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, WRIST_Z - STUB), plane_no=(0, 0, 1), clear_inner=True)
edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
bmesh.ops.edgeloop_fill(bm, edges=edges)
bm.to_mesh(hand.data); bm.free()
for p in hand.data.polygons: p.use_smooth = True
print('REMESH %d -> %d faces, wrist at %.3f, cut %.3f below it' % (len(src.data.polygons), len(hand.data.polygons), WRIST_Z, STUB))
while hand.data.uv_layers: hand.data.uv_layers.remove(hand.data.uv_layers[0])
hand.data.uv_layers.new(name='UVMap')
bpy.ops.object.select_all(action='DESELECT'); hand.select_set(True); bpy.context.view_layer.objects.active = hand
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004); bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
bpy.ops.object.mode_set(mode='OBJECT')
# bake colour and normal back from the original
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 4
bk = sc.render.bake; bk.use_selected_to_active = True; bk.cage_extrusion = 0.01; bk.max_ray_distance = 0.03; bk.margin = 16
mat = bpy.data.materials.new('M_Hand'); mat.use_nodes = True; nt = mat.node_tree
hand.data.materials.clear(); hand.data.materials.append(mat)
imgs = {}
for name, typ in (('BaseColor', 'DIFFUSE'),):   # the normal is made from the colour (detail_normal.py): baked, the sides came out as noise
    img = bpy.data.images.new(name, TEX, TEX, alpha=False)
    if name == 'Normal': img.colorspace_settings.name = 'Non-Color'
    node = nt.nodes.new('ShaderNodeTexImage'); node.image = img; nt.nodes.active = node
    if typ == 'DIFFUSE': bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
    bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); hand.select_set(True); bpy.context.view_layer.objects.active = hand
    bpy.ops.object.bake(type=typ)
    img.filepath_raw = '%s_%s.png' % (out, name); img.file_format = 'PNG'; img.save(); imgs[name] = img
    nt.nodes.remove(node)
print('BAKED', ', '.join(imgs))

V = np.array([v.co[:] for v in hand.data.vertices])
# the cut end's cap: the bake found nothing there and left it black. Its corners take the texture spot of the
# nearest wrist vertex above the cut, so it reads as skin if it ever shows past the cuff
cut_z = WRIST_Z - STUB
uvl = hand.data.uv_layers.active.data
capf = [p for p in hand.data.polygons if all(abs(V[i][2] - cut_z) < 1e-4 for i in p.vertices)]
ring = np.array([i for i in range(len(V)) if cut_z + 0.004 < V[i][2] < cut_z + 0.02])
vuv = {}
for p in hand.data.polygons:
    for li in p.loop_indices: vuv[hand.data.loops[li].vertex_index] = tuple(uvl[li].uv)
for p in capf:
    for li in p.loop_indices:
        vi = hand.data.loops[li].vertex_index
        near = ring[np.argmin(np.linalg.norm(V[ring] - V[vi], axis=1))]
        uvl[li].uv = vuv[near]
print('CAP %d faces given the wrist skin' % len(capf))

# ---- 2. the hand's own skeleton, from its outline (x across, z toward the fingertips)
T_ = np.array([list(p.vertices)[:3] for p in hand.data.polygons]); e1 = V[T_[:, 1]] - V[T_[:, 0]]; e2 = V[T_[:, 2]] - V[T_[:, 0]]
area = 0.5 * np.linalg.norm(np.cross(e1, e2), axis=1); rng = np.random.default_rng(1); pick = rng.choice(len(T_), 400000, p=area / area.sum())
u_ = rng.random(400000); v_ = rng.random(400000); f_ = u_ + v_ > 1; u_[f_] = 1 - u_[f_]; v_[f_] = 1 - v_[f_]
S = V[T_[pick, 0]] + e1[pick] * u_[:, None] + e2[pick] * v_[:, None]
step = 0.004; tracks = []; live = []
for z in np.arange(S[:, 2].max(), S[:, 2].min(), -step):
    sl = S[(S[:, 2] <= z) & (S[:, 2] > z - step)]
    if len(sl) < 3: continue
    xs = np.sort(sl[:, 0]); cuts = np.where(np.diff(xs) > 0.004)[0]
    spans = [(xs[a], xs[b]) for a, b in zip(np.r_[0, cuts + 1], np.r_[cuts, len(xs) - 1])]
    nxt = []
    for (a, b) in spans:
        over = [t for t in live if t['span'][0] < b + 0.002 and t['span'][1] > a - 0.002]
        pts = sl[(sl[:, 0] >= a) & (sl[:, 0] <= b)]; c = np.array([(a + b) / 2, (pts[:, 1].max() + pts[:, 1].min()) / 2, z - step / 2])
        if len(over) == 1 and not over[0].get('merged'):
            t = over[0]; t['span'] = (a, b); t['pts'].append(c); nxt.append(t)
        elif len(over) == 0:
            t = dict(span=(a, b), pts=[c], tip=c.copy()); tracks.append(t); nxt.append(t)
        else:
            for t in over:
                if 'valley' not in t: t['valley'] = z
            t = dict(span=(a, b), pts=[c], merged=True, tip=c.copy()); nxt.append(t)
    live = nxt
fingers = sorted([t for t in tracks if 'valley' in t and len(t['pts']) >= 8], key=lambda t: -len(t['pts']))[:5]
thumb = min(fingers, key=lambda t: t['valley'])
rest = sorted([t for t in fingers if t is not thumb], key=lambda t: -np.mean([p[0] for p in t['pts']]) * np.sign(thumb['tip'][0]))
names = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
order = [thumb] + rest
J = {}
for n, t in zip(names, order):
    pts = np.array(t['pts']); c = pts.mean(0); _, _, vt = np.linalg.svd(pts - c); ax = vt[0] * np.sign(vt[0] @ (pts[0] - pts[-1]))
    tip = c + ax * ((pts[0] - c) @ ax + step); base = c + ax * ((pts[-1] - c) @ ax); Lv = np.linalg.norm(tip - base)
    root = base - ax * (0.60 if n == 'Thumb' else 0.30) * Lv
    fr = (0.0, 0.42, 0.72, 1.0) if n == 'Thumb' else (0.0, 0.44, 0.71, 1.0)
    J[n] = [root + (tip - root) * f for f in fr]
sl = V[np.abs(V[:, 2] - WRIST_Z) < 0.005]                 # the wrist: found before the cut, at its narrowest
wrist = np.array([(sl[:, 0].max() + sl[:, 0].min()) / 2, (sl[:, 1].max() + sl[:, 1].min()) / 2, WRIST_Z])
print('WRIST', wrist.round(3), 'FINGERS', [(n, np.round(J[n][3], 3).tolist()) for n in names])

# armature on the hand, bound with automatic weights
ad = bpy.data.armatures.new('hand_rig'); rig = bpy.data.objects.new('hand_rig', ad); bpy.context.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
fa = ad.edit_bones.new('ForeArm'); fa.head = (wrist - np.array([0, 0, 0.30])).tolist(); fa.tail = wrist.tolist()
hb = ad.edit_bones.new('Hand'); hb.head = wrist.tolist(); hb.tail = J['Middle'][0].tolist(); hb.parent = fa
for n in names:
    par = hb
    for i in range(3):
        b = ad.edit_bones.new(n + str(i + 1)); b.head = J[n][i].tolist(); b.tail = J[n][i + 1].tolist(); b.parent = par
        b.use_connect = i > 0; par = b
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT'); hand.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
bones = ['ForeArm', 'Hand'] + [n + str(i + 1) for n in names for i in range(3)]
W = np.zeros((len(V), len(bones)))
gi = {g.index: g.name for g in hand.vertex_groups}
for v in hand.data.vertices:
    for g in v.groups:
        nm = gi[g.group]
        if nm in bones: W[v.index, bones.index(nm)] = g.weight
miss = W.sum(1) <= 1e-6
W[miss, 1] = 1.0
W /= W.sum(1, keepdims=True)
print('WEIGHTS automatic: %d vertices, %d had none (put on the hand)' % (len(V), int(miss.sum())))
keep_from = -1e9                                     # the stub is already cut clean above

# ---- 3. into the rig's bind pose, per side
names_u = []; U = []
for line in open(bones_txt, encoding='utf-8-sig'):
    p = line.split()
    if len(p) == 4: names_u.append(p[0]); U.append([float(x) for x in p[1:]])
U = np.array(U); Bl = np.array([OJ[n] for n in names_u])
Mx, *_ = np.linalg.lstsq(np.hstack([Bl, np.ones((len(Bl), 1))]), U, rcond=None)
Rn = np.linalg.inv(Mx[:3].T).T; mirrored = np.linalg.det(Mx[:3]) < 0
def to_unity(p): return p @ Mx[:3] + Mx[3]
index = {n: i for i, n in enumerate(names_u)}
def rot_between(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b); v = np.cross(a, b); c = a @ b
    if np.linalg.norm(v) < 1e-9: return np.eye(3)
    K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]]); return np.eye(3) + K + K @ K / (1 + c)
N = np.array([v.normal[:] for v in hand.data.vertices])
uv = hand.data.uv_layers.active.data
verts = []; tris = []
for side in ('Left', 'Right'):
    flip = np.array([-1.0, 1, 1]) if side == 'Right' else np.ones(3)
    Vs = V * flip; Ns = N * flip; Js = {n: [j * flip for j in J[n]] for n in names}
    def old(n, i): return OJ[P + side + 'Hand' + n + str(i)]
    lo = sum(np.linalg.norm(old(n, i + 1) - old(n, i)) for n in names[1:] for i in (1, 2, 3)); ln = sum(np.linalg.norm(Js[n][i + 1] - Js[n][i]) for n in names[1:] for i in range(3))
    sc_ = lo / ln
    A = np.array([Js[n][0] for n in names[1:]]); B = np.array([old(n, 1) for n in names[1:]])
    nb = np.cross(B[0] - B[3], B[1] - OJ[P + side + 'Hand']); nb /= np.linalg.norm(nb); nb *= np.sign(nb[2])
    na = np.array([0.0, -1, 0])
    A2 = np.vstack([A, A + na * 0.03 / sc_, A.mean(0) - np.array([0, 0, 0.05 / sc_])]); wdir = OJ[P + side + 'Hand'] - B.mean(0); wdir -= nb * (wdir @ nb); wdir /= np.linalg.norm(wdir)
    B2 = np.vstack([B, B + nb * 0.03, B.mean(0) + wdir * 0.05])
    ca, cb = A2.mean(0), B2.mean(0); Hm = (A2 - ca).T @ (B2 - cb); Uu, _, Vt = np.linalg.svd(Hm); D = np.diag([1, 1, np.sign(np.linalg.det(Vt.T @ Uu.T))]); Rp = Vt.T @ D @ Uu.T
    palm = (Rp * sc_, cb - Rp @ ca * sc_)
    # the generated palm is shorter than the rig's (whose Hand joint sits up the forearm), so placed by its
    # knuckles its wrist stopped short of the sleeve and the stub's cut end showed. The forearm part is moved on to
    # the rig's wrist joint; the automatic weights stretch the wrist between.
    reach = OJ[P + side + 'Hand'] - ((np.array(wrist) * flip) @ palm[0].T + palm[1])
    fore = (palm[0], palm[1] + reach * WRIST_REACH)
    print(side, 'wrist moved %.3f toward the rig wrist joint' % (np.linalg.norm(reach) * WRIST_REACH))
    Tm = [fore, palm]; Rl = [Rp, Rp]; bone = [index[P + side + 'ForeArm'], index[P + side + 'Hand']]
    for n in names:
        for i in range(3):
            a, b = Js[n][i], Js[n][i + 1]; a2, b2 = old(n, i + 1), old(n, i + 2)
            d_ = (b - a) / np.linalg.norm(b - a); R = rot_between(Rp @ d_, b2 - a2) @ Rp
            Sm = sc_ * np.eye(3) + (np.linalg.norm(b2 - a2) / np.linalg.norm(b - a) - sc_) * np.outer(d_, d_)
            Tm.append((R @ Sm, a2 - R @ Sm @ a)); Rl.append(R); bone.append(index[P + side + 'Hand' + n + str(i + 1)])
    Pn = np.zeros_like(Vs); Nn = np.zeros_like(Ns)
    for b_, ((A_, t_), R_) in enumerate(zip(Tm, Rl)):
        Pn += W[:, b_:b_ + 1] * (Vs @ A_.T + t_); Nn += W[:, b_:b_ + 1] * (Ns @ R_.T)
    err = max(np.linalg.norm((Js[n][0] @ palm[0].T + palm[1]) - old(n, 1)) for n in names[1:])
    print(side, 'scale %.3f knuckle fit error %.4f' % (sc_, err))
    key = {}
    for poly in hand.data.polygons:
        if V[list(poly.vertices)][:, 2].mean() < keep_from: continue
        ids = []
        for li in poly.loop_indices:
            vi = hand.data.loops[li].vertex_index; u2 = tuple(uv[li].uv); kk = (side, vi, round(u2[0], 5), round(u2[1], 5))
            if kk not in key:
                top = np.argsort(-W[vi])[:4]; ww = W[vi][top]; ww = ww / ww.sum()
                key[kk] = len(verts); verts.append((to_unity(Pn[vi]), Nn[vi] @ Rn.T, u2, [bone[t] for t in top], ww.tolist()))
            ids.append(key[kk])
        rev = mirrored != (side == 'Right')
        for q in range(1, len(ids) - 1): tris.append((ids[0], ids[q + 1], ids[q]) if rev else (ids[0], ids[q], ids[q + 1]))
with open(out, 'wb') as f:
    f.write(struct.pack('<ii', len(verts), 1))
    for pos, nrm, u2, bi, bw in verts:
        n_ = nrm / max(np.linalg.norm(nrm), 1e-9); f.write(struct.pack('<8f4i4f', *pos, *n_, u2[0], u2[1], *bi, *bw))
    f.write(struct.pack('<i', len(tris) * 3))
    for t in tris: f.write(struct.pack('<3i', *t))
print('WROTE', out, len(verts), 'vertices', len(tris), 'triangles')
