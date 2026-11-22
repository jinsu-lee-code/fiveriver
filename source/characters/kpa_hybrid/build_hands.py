# New hands for the KPA rifleman from the VARCO hand, cleaned up in Blender (2026-11-18).
#
# The first try (2026-11-15, reverted) carried the generated hand straight onto the rig: its triangles were
# uneven and every vertex hung on one bone, so gripping the rifle threw out needle-like spikes and the stub of
# wrist tore at the cuff. Here the hand is first made over:
#   1. welded (it came with every triangle's corners apart) and rebuilt as an even surface (voxel remesh,
#      smoothing, then thinned to a game count); its colour and surface detail are baked back from the original;
#   2. given a skeleton found from its outline (forearm, hand, three bones per finger) and bound with Blender's
#      automatic weights, so each joint bends over a soft band and no vertex follows the wrong finger;
#   3. placed on the game's rig by its palm (knuckles onto the rig's knuckles, scaled by the fingers), unstretched;
#      the rig's finger bones are then moved onto the hand's own joints (<out>_bones.json, read by apply_hybrid.cs),
#      as the Rigify video does it: bones fitted to the model, not the model to the bones. Mirrored for the right.
#
#   blender -b --factory-startup -P build_hands.py -- <old SK_CH_KPA_Rifleman.fbx> <hand.glb> <unity_bones.txt> <out.bin> [<joints.json>]
# Writes <out.bin> (both hands, one submesh) and <out.bin>_BaseColor.png; the normal map is made from the colour
# (detail_normal.py). build_hybrid.py takes <out.bin> as an extra argument and puts the hands into the body mesh.
import bpy, bmesh, sys, os, struct, json
import numpy as np

old_fbx, hand_glb, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
P = 'mixamorig:'
VOXEL, FACES, TEX, STUB, WRIST_REACH, FINGER_GIRTH, PALM_THIN = 0.0045, 9000, 2048, 0.05, 0.8, 1.08, 0.9
# (2026-11-19, art-direction pass) the four fingers shortened about their knuckles: the middle finger ran 1.03 palm
# lengths (a real one ~0.85-0.9) and read spidery; with the girth up (was 0.9, thinned) a finger is again about a
# quarter as thick as it is long, as in a real hand
FINGER_LEN = 0.85
KNUCKLES = ((0, 0.050, 0.075), (1, 0.015, 0.050), (2, 0.008, 0.035))
PULP_OUT, PULP_FWD = 0.016, 0.005
TIP_BEND = 18.0   # degrees the end joints are bent toward the palm in the rest shape
# (2026-11-20, "does the thumb look like a person's?") Its free part, root knuckle to tip, was 0.40 of the palm's
# length where a man's is about 0.6 (the whole thumb, metacarpal to tip, about as long as the palm): it read as a
# stub off the side of the hand. Lengthened 1.4 about its root knuckle, the metacarpal and its girth left alone.
THUMB_LEN, THUMB_GIRTH = 1.4, 1.12
THUMB_IN_KNUCKLE = 0.06   # how far the thumb is drawn in at its root knuckle (THUMB_IN at its root by the wrist)
WEB_R, WEB_PASSES, WEB_LAMBDA, WEB_MU = 0.30, 40, 0.6, -0.63   # reach round the thumb metacarpal (palm lengths), Taubin passes
WEB_FILL, WEB_FILL_ITERS = 0.7, 3   # how far into the convex outline the groove round the thumb metacarpal is filled
THUMB_KNUCKLES = ((1, 0.030, 0.07), (2, 0.014, 0.045))   # root knuckle, end joint: bump height, radius (palm lengths)   # fingertip pad: outward on the palm side, and forward past the nail (palm lengths)   # joint, bump height, radius (palm lengths), on the back
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=old_fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
OJ = {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}
# where the hands go: the old rig's joints, or (6th argument, 2026-11-18 whole-model rig) a json of the joints the
# body itself has (build_hybrid.py own-joints writes it). OJ stays the old rig: it fixes the mesh's own space below.
TJ = dict(OJ)
if len(sys.argv) > sys.argv.index('--') + 5:
    TJ.update({k: np.array(v) for k, v in json.load(open(sys.argv[sys.argv.index('--') + 5])).items()})
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
# colour only: a baked normal map came out as noise on the sides of the hand, so it is made from the colour instead
# (detail_normal.py)
img = bpy.data.images.new('BaseColor', TEX, TEX, alpha=False)
node = nt.nodes.new('ShaderNodeTexImage'); node.image = img; nt.nodes.active = node
bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); hand.select_set(True); bpy.context.view_layer.objects.active = hand
bpy.ops.object.bake(type='DIFFUSE')
img.filepath_raw = out + '_BaseColor.png'; img.file_format = 'PNG'; img.save()
nt.nodes.remove(node)
print('BAKED BaseColor')

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
json.dump({'wrist': wrist.tolist(), **{n: [j.tolist() for j in J[n]] for n in names}}, open(out + '_joints.json', 'w'))   # for checking the joints on the mesh

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
# (2026-11-19, "the palm is not the fingers run together") The automatic weights split the hand from the fingers
# by distance alone, so the fingers' first bones held the back of the hand from 0.2 of the palm's length before
# the knuckles (weight 0.45 there, 0.65 at 0.1 before): curled round the rifle, the top of the palm folded in with
# the fingers and the palm read as fingers grown together. In a real hand the palm is one piece up to the
# knuckles. The first finger bones now let go of the hand just behind the knuckle on the back, and on the palm
# side only past the crease at the base of the finger (which lies beyond the knuckle joint).
KNUCKLE_BACK, KNUCKLE_PALM = (-0.05, 0.04), (0.02, 0.14)   # where a finger's hold starts and is full, palm lengths from its knuckle
pl_ = np.linalg.norm(J['Middle'][0] - wrist)
nrm_ = np.array([v.normal[:] for v in hand.data.vertices])
back_ = np.clip(0.5 + 2.0 * (nrm_ @ np.array([0.0, -1.0, 0.0])), 0, 1)   # 1 on the back (normal toward -y), 0 on the palm side
def ss(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
moved = 0.0
for n in names[1:]:
    k0 = J[n][0]; ax_ = (J[n][1] - J[n][0]) / np.linalg.norm(J[n][1] - J[n][0])
    along = (V - k0) @ ax_ / pl_
    keep_ = back_ * ss(*KNUCKLE_BACK, along) + (1 - back_) * ss(*KNUCKLE_PALM, along)
    bi_ = bones.index(n + '1'); lost = W[:, bi_] * (1 - keep_)
    W[:, bi_] -= lost; W[:, bones.index('Hand')] += lost; moved += lost.sum()
print('WEIGHTS palm: %.0f vertices worth of finger hold given back to the palm' % moved)
# (2026-11-19, against photos of a real hand, back and palm) The generated thumb branched off at the row of
# knuckles like a sixth finger: its root joint sat at 0.68 of the palm's length from the wrist (a real one, at
# ~0.2), the cleft between it and the forefinger at 0.9 (real: about half way up the back of the hand), its tip
# above the knuckles (real: level with the forefinger's knuckle), and its nail faced the same way as the other
# nails (real: turned some 70 degrees, seen from the side when the hand lies flat). So the thumb is moved down
# toward the wrist, the side of the palm under it drawn down with it (the web to the forefinger widens), turned
# on its own axis, and the ball of the thumb is filled out on the palm side.
THUMB_DROP, THUMB_TWIST, THENAR, THUMB_IN, SMOOTH_ROOT = 0.32, 70.0, 0.07, 0.20, 30
# (2026-11-20) at 0.22 the thumb still branched off high (root 0.43, root knuckle 0.70, tip 1.19 of the palm's
# length, above the forefinger's knuckle at 1.04; a man's: about 0.2, 0.55-0.6, 1.0-1.1) and read as a sixth finger   # palm lengths / degrees / palm lengths / palm lengths / passes
palm_ax = J['Middle'][0] - wrist; palm_len = np.linalg.norm(palm_ax); palm_ax /= palm_len
sx = np.sign(J['Thumb'][3][0] - wrist[0])                       # the thumb's side across the hand
dorsal = np.array([0.0, -1.0, 0.0])                             # the back of this hand (its nails face -y)
yN = (V - wrist) @ palm_ax / palm_len                           # 0 at the wrist, 1 at the middle knuckle
xN = (V[:, 0] - wrist[0]) * sx / palm_len                       # toward the thumb
y_root = (J['Thumb'][0] - wrist) @ palm_ax / palm_len; y_knuckle = (J['Index'][0] - wrist) @ palm_ax / palm_len
tw = W[:, [bones.index('Thumb%d' % i) for i in (1, 2, 3)]].sum(1)
fw = W[:, [bones.index(n + str(i)) for n in names[1:] for i in (1, 2, 3)]].sum(1)
side_ = np.clip((xN - 0.05) / 0.30, 0, 1); side_ = side_ * side_ * (3 - 2 * side_)
ramp = np.where(yN < y_root, np.clip(yN / y_root, 0, 1), np.clip(1 - (yN - y_root) / max(y_knuckle - y_root, 1e-3), 0, 1))
# the thumb's root bone also holds the side of the palm down to the wrist (automatic weights): below the root
# joint everything on that side is drawn down by less and less toward the wrist (moved whole, the part under the
# root went past the wrist and stood out of it), above it the thumb moves whole
below = yN < y_root
lower = np.clip(yN / y_root, 0, 1)
f = np.where(below, lower * np.maximum(tw, side_ * (1 - fw)), tw + (1 - tw) * side_ * ramp * (1 - fw))
# turned and drawn in: the thumb whole, fading out over the last quarter palm under its root
taper = np.where(below, np.clip((yN - (y_root - 0.25)) / 0.25, 0, 1), 1.0) * tw
drop = -palm_ax * THUMB_DROP * palm_len
# turned on its axis: the nail from facing the back of the hand toward facing away from the fingers
t_ax = J['Thumb'][3] - J['Thumb'][0]; t_ax /= np.linalg.norm(t_ax)
lateral = np.array([sx, 0.0, 0.0]); lateral -= t_ax * (lateral @ t_ax)
ang = np.radians(THUMB_TWIST) * np.sign(np.cross(t_ax, dorsal) @ lateral)
def turn(p, a):   # rotate points p about the thumb axis (through its root) by angles a
    q = p - J['Thumb'][0]; c, s_ = np.cos(a)[:, None], np.sin(a)[:, None]
    return J['Thumb'][0] + q * c + np.cross(t_ax, q) * s_ + t_ax * (q @ t_ax)[:, None] * (1 - c)
V = turn(V, ang * taper) + drop * f[:, None]
# moved down only, its thick root stood out of the side of the hand by the wrist, where the hand is narrower: the
# thumb is drawn in across the hand as well (the palm's own edge stays where it is)
# (2026-11-20) Drawn in by the same amount all along, the thumb's metacarpal came to lie under the edge of the palm
# (cut across half way up, the thumb hung straight below it), and from the back the palm's edge ran over it to the
# wrist like a seam. In a hand the thumb's metacarpal is the radial edge itself, outside the forefinger's. So it is
# drawn in fully at its root, by the wrist (where it had stood out), and less and less toward its root knuckle.
jd0, jd1 = J['Thumb'][0] + drop, J['Thumb'][1] + drop; sg_ = jd1 - jd0
t_in = np.clip((V - jd0) @ sg_ / (sg_ @ sg_), 0, 1)
in_amt = THUMB_IN + (THUMB_IN_KNUCKLE - THUMB_IN) * (t_in * t_in * (3 - 2 * t_in))
V = V - np.array([sx, 0.0, 0.0]) * (in_amt * palm_len * taper)[:, None]
J['Thumb'] = [j - np.array([sx, 0.0, 0.0]) * (THUMB_IN if i == 0 else THUMB_IN_KNUCKLE) * palm_len for i, j in enumerate(J['Thumb'])]
J['Thumb'] = [j + drop for j in J['Thumb']]
# where the thumb's root came down onto the narrower part of the hand it still stood out as a lump by the wrist,
# with folds in the stretched web: that part alone is smoothed out (the fingers and the thumb's free part are not)
nb_ = [[] for _ in range(len(V))]
for e in hand.data.edges: a_, b_ = e.vertices; nb_[a_].append(b_); nb_[b_].append(a_)
r0 = np.linalg.norm(V - J['Thumb'][0], axis=1) / palm_len
# and the corner over the wrist on the thumb's side, where the side of the palm drawn down met the wrist in a knob
corner = wrist + palm_ax * 0.18 * palm_len + np.array([sx, 0.0, 0.0]) * 0.33 * palm_len
r1 = np.linalg.norm(V - corner, axis=1) / palm_len
mask = np.maximum(np.exp(-(r0 / 0.30) ** 2), np.exp(-(r1 / 0.22) ** 2)) * (1 - fw) * (1 - W[:, [bones.index('Thumb2'), bones.index('Thumb3')]].sum(1))
for _ in range(SMOOTH_ROOT):
    avg = np.array([V[n].mean(0) if n else V[i] for i, n in enumerate(nb_)])
    V = V + 0.5 * mask[:, None] * (avg - V)
# (2026-11-20) Cut across at the middle of its metacarpal, the thumb hung under the palm's edge from a narrow neck,
# a deep groove between it and the palm on the palm side and the palm's edge standing over it on the back: from
# behind it read as a separate thing stuck on at the wrist. In a hand the ball of the thumb runs into the palm and
# the web runs from the back of the hand over onto the thumb in one curve. Round the whole metacarpal (not only its
# root) the surface is smoothed without shrinking (Taubin: a step in, a slightly larger step out): the groove fills
# and the edge rounds off, the thumb's free part and the fingers left alone.
m0_, m1w = J['Thumb'][0], J['Thumb'][1]; segw = m1w - m0_
ttw = np.clip((V - m0_) @ segw / (segw @ segw), 0, 1.15)
dw = np.linalg.norm(V - (m0_ + segw[None] * ttw[:, None]), axis=1) / palm_len
yNow = (V - wrist) @ palm_ax / palm_len
free_t = W[:, [bones.index('Thumb2'), bones.index('Thumb3')]].sum(1)
web_mask = np.exp(-(dw / WEB_R) ** 2) * (1 - fw) * np.clip(1 - 1.5 * free_t, 0, 1) * ss(-0.15, 0.05, yNow)
for it in range(WEB_PASSES):
    for k_ in (WEB_LAMBDA, WEB_MU):
        avg = np.array([V[n].mean(0) if n else V[i] for i, n in enumerate(nb_)])
        V = V + k_ * web_mask[:, None] * (avg - V)
print('WEB smoothed round the thumb metacarpal over %.0f vertices' % web_mask.sum())
# Smoothing barely touched the groove (1.5 cm deep, wide against the mesh's spacing). It is filled instead: cut
# across every 0.03 palm lengths, each cut's outline on the thumb's half of the hand is compared with its convex
# outline, and what lies inside that (the groove on the palm side, the notch under the palm's edge on the back) is
# drawn out to it - below the thumb's root knuckle only, so the thumb's free part stays apart from the forefinger.
def hull2(pts):
    pts = sorted(map(tuple, pts)); h = []
    def cr(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    for p_ in pts:
        while len(h) >= 2 and cr(h[-2], h[-1], p_) <= 0: h.pop()
        h.append(p_)
    lo_ = len(h) + 1
    for p_ in reversed(pts[:-1]):
        while len(h) >= lo_ and cr(h[-2], h[-1], p_) <= 0: h.pop()
        h.append(p_)
    return np.array(h[:-1])
y_mcp = (J['Thumb'][1] - wrist) @ palm_ax / palm_len
moved_most = 0.0
for it_ in range(WEB_FILL_ITERS):
    yNow = (V - wrist) @ palm_ax / palm_len; aNow = (V[:, 0] - wrist[0]) * sx / palm_len; dNow = (V - wrist) @ dorsal / palm_len
    # the surface's outward direction at each vertex, from the shape as it now is
    fn_ = np.zeros_like(V)
    for poly in hand.data.polygons:
        vs = list(poly.vertices)
        for q in range(1, len(vs) - 1):
            a_, b_, c_ = vs[0], vs[q], vs[q + 1]; fn_[[a_, b_, c_]] += np.cross(V[b_] - V[a_], V[c_] - V[a_])
    fn_ /= np.maximum(np.linalg.norm(fn_, axis=1), 1e-12)[:, None]
    nA = fn_[:, 0] * sx; nD = fn_ @ dorsal
    fill = np.zeros((len(V), 2))
    for y0 in np.arange(0.0, y_mcp + 0.02, 0.03):
        sl = np.where((np.abs(yNow - y0) < 0.02) & (aNow > -0.3) & (fw < 0.3) & (free_t < 0.5))[0]
        if len(sl) < 10: continue
        pts = np.stack([aNow[sl], dNow[sl]], 1); H = hull2(pts)
        if len(H) < 3: continue
        E0 = H; E1 = np.roll(H, -1, 0)
        for k_, vi in enumerate(sl):
            if aNow[vi] < -0.05: continue
            q = pts[k_]; dn = np.array([nA[vi], nD[vi]]); ln_ = np.linalg.norm(dn)
            if ln_ < 0.2: continue
            dn /= ln_
            # out along the surface's own outward direction to the convex outline; by how squarely the surface
            # faces that edge of it (the floor of the groove fully, its walls hardly, so they rise and do not cross)
            best = None; face = 0.0
            for e0, e1 in zip(E0, E1):
                if e0[0] < -0.25 and e1[0] < -0.25: continue   # the cut-off edge of the half, not the outline
                m_ = np.array([[dn[0], e0[0] - e1[0]], [dn[1], e0[1] - e1[1]]])
                if abs(np.linalg.det(m_)) < 1e-9: continue
                tt_, ss_ = np.linalg.solve(m_, e0 - q)
                if tt_ >= 0 and -1e-6 <= ss_ <= 1 + 1e-6 and (best is None or tt_ < best):
                    ev = e1 - e0; en = np.array([ev[1], -ev[0]]) / max(np.linalg.norm(ev), 1e-12)
                    best = tt_; face = abs(en @ dn)
            if best is not None and best < 0.45: fill[vi] = dn * best * face * face
    w_fill = ss(y_mcp + 0.02, y_mcp - 0.12, yNow) * ss(-0.05, 0.10, aNow) * (1 - fw) * np.clip(1 - 2 * free_t, 0, 1) * WEB_FILL
    for _ in range(4):   # the moves smoothed between neighbours, so the cuts leave no ridges
        fill = np.array([fill[n].mean(0) * 0.5 + fill[i] * 0.5 if n else fill[i] for i, n in enumerate(nb_)])
    V[:, 0] += fill[:, 0] * w_fill * palm_len * sx
    V += np.outer(fill[:, 1] * w_fill * palm_len, dorsal)
    moved_most = max(moved_most, float((np.linalg.norm(fill, axis=1) * w_fill).max()))
    # and the filled part relaxed (Taubin, so it does not shrink back)
    for k_ in (0.5, -0.52, 0.5, -0.52):
        avg = np.array([V[n].mean(0) if n else V[i] for i, n in enumerate(nb_)])
        V = V + k_ * (w_fill > 0.05)[:, None] * w_fill[:, None] * (avg - V)
print('WEB groove filled: largest move %.3f palm lengths' % moved_most)
# the ball of the thumb: palm side, over the thumb's metacarpal (root to its first joint), falling off round it
m0, m1_ = J['Thumb'][0], J['Thumb'][1]; seg = m1_ - m0
tt = np.clip((V - m0) @ seg / (seg @ seg), 0, 1); dist = np.linalg.norm(V - (m0 + seg[None] * tt[:, None]), axis=1) / palm_len
palmar_side = np.clip(((V - m0) @ -dorsal) / (0.05 * palm_len), 0, 1)
V = V - dorsal * (THENAR * palm_len * np.exp(-(dist / 0.22) ** 2) * palmar_side * (1 - fw))[:, None]
print('THUMB moved %.3f toward the wrist, turned %.0f deg, root now at %.2f of the palm' % (THUMB_DROP * palm_len, np.degrees(ang), (J['Thumb'][0] - wrist) @ palm_ax / palm_len))
# (2026-11-20) The palm's colour, as baked from the generated hand, carried a crumpled pattern of large wrinkles
# (it reads as a crushed paper bag where a real palm is smooth between a few creases). On the palm side only, the
# large wrinkles are smoothed out of the texture and the fine skin grain is kept; the normal map is made from this
# colour afterwards (detail_normal.py), so it loses them too.
PALM_SMOOTH, PALM_GRAIN, PALM_KEEP, PALM_TONE = 14, 2, 0.65, 0.5   # px radii of the smoothing and the grain kept, how much grain, how far toward the palm's mean tone
PALM_LIFT, PALM_LIFT_K = (1.20, 1.09, 1.15), 0.6   # (0.85 read as a light glove in the game, edged along the sides of the hand)        # the palm's tone from the back's: lighter and a little pinker; how far toward it
CREASE_W, CREASE_DARK = 0.012, 0.13                      # crease half-width (palm lengths) and how much it darkens
HEAD_SKIN = (0.465, 0.368, 0.275)   # mean skin colour of T_CH_KPA_Head0-2_BaseColor
NAIL_R, NAIL_LIFT = 9, 0.85                              # px radius of the skin the nail rims are lightened toward; how far
# (the inside of the wrist and of the fingers too, the fingers lighter so their real joint creases stay; done on the
# palm alone the smoothed part stood out from them along a line, and a dark blot of the old pattern stayed in its middle)
def box(a, r):   # separable box blur (rows, then columns), same size
    for ax in (0, 1):
        c = np.cumsum(np.pad(a, [(r + 1, r) if i == ax else (0, 0) for i in range(a.ndim)], mode='edge'), axis=ax)
        a = (np.take(c, range(2 * r + 1, c.shape[ax]), axis=ax) - np.take(c, range(0, c.shape[ax] - 2 * r - 1), axis=ax)) / (2 * r + 1)
    return a
V0_ = np.array([v.co[:] for v in hand.data.vertices])   # the shape as baked (the mesh is written back further down)
px = np.array(img.pixels[:], dtype=np.float32).reshape(TEX, TEX, 4)
mask = np.zeros((TEX, TEX), np.float32)
for poly in hand.data.polygons:
    vs = list(poly.vertices); c = np.mean([V0_[i] for i in vs], axis=0)
    yc = (c - wrist) @ palm_ax / palm_len
    if poly.normal[1] < 0.35 or yc < -0.35: continue   # palm side (+y), from the inside of the wrist out
    strength = 0.55 if np.mean(fw[vs]) > 0.3 else 1.0
    uv_ = np.array([uvl[li].uv[:] for li in poly.loop_indices]) * TEX
    for q in range(1, len(uv_) - 1):
        t = np.array([uv_[0], uv_[q], uv_[q + 1]]); lo = np.floor(t.min(0)).astype(int); hi = np.ceil(t.max(0)).astype(int) + 1
        gx, gy = np.meshgrid(np.arange(lo[0], hi[0]), np.arange(lo[1], hi[1])); p_ = np.stack([gx, gy], -1) + 0.5
        d_ = (t[1, 1] - t[2, 1]) * (t[0, 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (t[0, 1] - t[2, 1])
        if abs(d_) < 1e-9: continue
        l1 = ((t[1, 1] - t[2, 1]) * (p_[..., 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (p_[..., 1] - t[2, 1])) / d_
        l2 = ((t[2, 1] - t[0, 1]) * (p_[..., 0] - t[2, 0]) + (t[0, 0] - t[2, 0]) * (p_[..., 1] - t[2, 1])) / d_
        ins = (l1 >= -0.01) & (l2 >= -0.01) & (1 - l1 - l2 >= -0.01)
        yy, xx = gy[ins].clip(0, TEX - 1), gx[ins].clip(0, TEX - 1); mask[yy, xx] = np.maximum(mask[yy, xx], strength)
rgb = px[..., :3]
inside = (mask > 0)[..., None].astype(np.float32)
smooth = box(rgb * inside, PALM_SMOOTH) / np.maximum(box(inside, PALM_SMOOTH), 1e-4)   # averaged over palm-side texels only
tone = (rgb * (mask[..., None] >= 1.0)).reshape(-1, 3).sum(0) / max(1.0, float((mask >= 1.0).sum()))
smooth = smooth * (1 - PALM_TONE) + tone * PALM_TONE
grain = rgb - box(rgb * inside, PALM_GRAIN) / np.maximum(box(inside, PALM_GRAIN), 1e-4)
blend = (box(mask, 10) * (mask > 0))[..., None]
px[..., :3] = rgb * (1 - blend) + (smooth + grain * PALM_KEEP) * blend
print('PALM texture smoothed over %d texels' % int(mask.sum()))
# (2026-11-20, "is this really a human hand?") Smoothed, the palm came out one even brown, darker than the back of
# the hand, like clay; a real palm is lighter and pinker than the back, with a few clear creases. The nails were
# ringed in a thick dark line, which read as dirty buttons on the ends of the fingers. So: the palm is lifted toward
# a lighter, pinker version of the back's own tone; the three main creases (the thenar crease round the ball of the
# thumb, the proximal and distal transverse creases) are drawn in thinly; the dark rims round the nails are
# lightened toward the skin round them.
def raster(keep_face, vals):
    # per texel: the per-vertex values (N x k) interpolated over the faces kept, and where they were written
    out_ = np.zeros((TEX, TEX, vals.shape[1]), np.float32); hit = np.zeros((TEX, TEX), bool)
    for poly in hand.data.polygons:
        if not keep_face(poly): continue
        vs = list(poly.vertices); uv_ = np.array([uvl[li].uv[:] for li in poly.loop_indices]) * TEX
        for q in range(1, len(uv_) - 1):
            ids3 = (0, q, q + 1); t = uv_[list(ids3)]
            lo = np.floor(t.min(0)).astype(int); hi = np.ceil(t.max(0)).astype(int) + 1
            gx, gy = np.meshgrid(np.arange(lo[0], hi[0]), np.arange(lo[1], hi[1])); p_ = np.stack([gx, gy], -1) + 0.5
            d_ = (t[1, 1] - t[2, 1]) * (t[0, 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (t[0, 1] - t[2, 1])
            if abs(d_) < 1e-9: continue
            l1 = ((t[1, 1] - t[2, 1]) * (p_[..., 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (p_[..., 1] - t[2, 1])) / d_
            l2 = ((t[2, 1] - t[0, 1]) * (p_[..., 0] - t[2, 0]) + (t[0, 0] - t[2, 0]) * (p_[..., 1] - t[2, 1])) / d_
            l3 = 1 - l1 - l2; ins = (l1 >= -0.01) & (l2 >= -0.01) & (l3 >= -0.01)
            yy, xx = gy[ins].clip(0, TEX - 1), gx[ins].clip(0, TEX - 1)
            vv = vals[[vs[i] for i in ids3]]
            out_[yy, xx] = l1[ins][:, None] * vv[0] + l2[ins][:, None] * vv[1] + l3[ins][:, None] * vv[2]; hit[yy, xx] = True
    return out_, hit
rgb = px[..., :3]
# the back of the hand's own tone (back of the palm, not the fingers)
backm = np.zeros((TEX, TEX), bool)
for poly in hand.data.polygons:
    vs = list(poly.vertices)
    if poly.normal[1] > -0.5 or np.mean(fw[vs]) > 0.2: continue
    c = np.mean([V0_[i] for i in vs], axis=0); yc = (c - wrist) @ palm_ax / palm_len
    if not (0.15 < yc < 0.85): continue
    uv_ = np.array([uvl[li].uv[:] for li in poly.loop_indices]).mean(0) * TEX
    backm[int(np.clip(uv_[1], 0, TEX - 1)), int(np.clip(uv_[0], 0, TEX - 1))] = True
back_tone = rgb[backm].mean(0) if backm.any() else tone
palm_target = np.clip(back_tone * np.array(PALM_LIFT), 0, 1)
# how much of the palm's lift each vertex takes, from its normal: interpolated over every face, so it runs on
# across the texture's seams (blurred in the texture it left dark rims round each piece of the palm)
palmness = ss(-0.15, 0.7, nrm_[:, 1]) * ss(-0.45, -0.25, yN)
pw_img, _ = raster(lambda poly: np.max(palmness[list(poly.vertices)]) > 0.0, palmness[:, None])
pmask = pw_img
lift = rgb + (palm_target - tone)[None, None, :] * PALM_LIFT_K
px[..., :3] = rgb * (1 - pmask) + lift * pmask
print('PALM tone %s -> %s (back of the hand %s)' % (np.round(tone, 2), np.round(palm_target, 2), np.round(back_tone, 2)))
# creases, in the palm's own frame: v from the wrist (0) to the middle knuckle (1), u across from the little
# finger's knuckle (0) to the forefinger's (1)
xL = (J['Pinky'][0][0] - wrist[0]) * sx / palm_len; xI = (J['Index'][0][0] - wrist[0]) * sx / palm_len
# (on the shape as it now is: the thumb's move brought the ball of the thumb down toward the wrist, and creases
# placed on the shape before it ran on down the wrist)
yC = (V - wrist) @ palm_ax / palm_len; xC = (V[:, 0] - wrist[0]) * sx / palm_len
uN = (xC - xL) / (xI - xL); spanU = abs(xI - xL)
crease_w = palmness * (1 - fw) * (1 - np.clip(tw * 1.5, 0, 1))   # the palm itself: not the fingers or the thumb's free part
uv_img, hitp = raster(lambda poly: np.max(crease_w[list(poly.vertices)]) > 0.05, np.stack([uN, yC, crease_w], 1))
def bez(p0, p1, p2, n=40):
    t = np.linspace(0, 1, n)[:, None]; return (1 - t) ** 2 * np.array(p0) + 2 * (1 - t) * t * np.array(p1) + t * t * np.array(p2)
CREASES = (bez((-0.10, 0.80), (0.35, 0.79), (0.80, 0.93)),    # distal transverse ("heart line")
           bez((1.05, 0.82), (0.55, 0.72), (0.02, 0.63)),     # proximal transverse ("head line")
           bez((1.00, 0.82), (0.55, 0.42), (0.74, 0.14)))     # thenar ("life line"), round the ball of the thumb
dmin = np.full((TEX, TEX), 9.0, np.float32)
qu, qv = uv_img[..., 0], uv_img[..., 1]
for cv in CREASES:
    for k in range(len(cv) - 1):
        a0, a1 = cv[k], cv[k + 1]; e_ = a1 - a0
        tt = np.clip(((qu - a0[0]) * e_[0] * spanU ** 2 + (qv - a0[1]) * e_[1]) / ((e_[0] * spanU) ** 2 + e_[1] ** 2), 0, 1)
        du = (qu - (a0[0] + e_[0] * tt)) * spanU; dv = qv - (a0[1] + e_[1] * tt)
        dmin = np.minimum(dmin, np.sqrt(du * du + dv * dv))
line = np.exp(-(dmin / CREASE_W) ** 2) * hitp * uv_img[..., 2]
px[..., :3] = px[..., :3] * (1 - CREASE_DARK * line[..., None])
print('PALM creases drawn over %d texels' % int((line > 0.3).sum()))
# nail rims: on the backs of the end joints, past their middle
dist_t = np.zeros(len(V)); dist_w = np.zeros(len(V))
for n in names:
    a_, b_ = J[n][2], J[n][3]; d_ = b_ - a_
    t_ = np.clip((V - a_) @ d_ / (d_ @ d_), 0, 1); w_ = W[:, bones.index(n + '3')]
    better = w_ > dist_w; dist_t[better] = t_[better]; dist_w[better] = w_[better]
nail_v = np.clip((dist_t - 0.35) / 0.2, 0, 1) * (dist_w > 0.3) * back_
nimg, hitn = raster(lambda poly: np.mean(nail_v[list(poly.vertices)]) > 0.05, nail_v[:, None])
nm_ = nimg[..., 0] * hitn
lum = px[..., :3].mean(-1); local = box(lum, NAIL_R)
dark = np.clip((local - lum - 0.015) / 0.08, 0, 1) * nm_
px[..., :3] = px[..., :3] + (box(px[..., :3], NAIL_R) - px[..., :3]) * (NAIL_LIFT * dark)[..., None]
print('NAIL rims lightened over %d texels' % int((dark > 0.3).sum()))
# (2026-11-20) Beside the face the hands were another man's: brighter and much redder (skin mean 0.60 0.41 0.33
# against the three heads' 0.465 0.368 0.275; red over green 1.45 against 1.27), brick-coloured in the game's light.
# The whole map is scaled, channel by channel, so its skin mean is the heads'.
r_, g_, b_ = px[..., 0], px[..., 1], px[..., 2]
skin_m = (r_ > g_ * 1.1) & (g_ > b_ * 1.05) & (r_ - b_ > 0.1) & (r_ > 0.25) & (r_ < 0.9)
if skin_m.any():
    gain = np.array(HEAD_SKIN) / px[..., :3][skin_m].mean(0)
    px[..., :3] = np.clip(px[..., :3] * gain, 0, 1)
    print('SKIN matched to the heads: gain', np.round(gain, 3))
img.pixels[:] = px.ravel(); img.save()
# Each finger drawn out from (or in toward) its own bones to FINGER_GIRTH and shortened about its knuckle by
# FINGER_LEN, weighted over its bones (until 2026-11-19 they were thinned to 0.9 and kept their full length).
# the palm a little flatter too (its back read as a round dome, like a fist, round the stock): drawn toward the plane
# through the wrist and the knuckles, by the vertex's weight on the hand bone
kn_ = np.array([J[n][0] for n in names[1:]]); pc = (kn_.mean(0) + wrist) / 2
pn = np.cross(kn_[0] - wrist, kn_[3] - wrist); pn /= np.linalg.norm(pn)
J_size = {n: [j.copy() for j in J[n]] for n in names}   # the lengths the hand is sized to the rig by (step 3), before shortening
Vthin = V.copy()
for vi in range(len(V)):
    acc = np.zeros(3); tot = 0.0
    for bi_, b in enumerate(bones):
        w = W[vi, bi_]
        if w <= 0 or b in ('ForeArm', 'Hand'): continue
        n, k = b[:-1], int(b[-1]) - 1
        a_, d_ = J[n][k], J[n][k + 1] - J[n][k]; t = np.clip((V[vi] - a_) @ d_ / (d_ @ d_), 0, 1); q = a_ + d_ * t
        if n == 'Thumb':   # (2026-11-20) its free part lengthened about its root knuckle; the metacarpal stays
            qs = q if k == 0 else J[n][1] + (q - J[n][1]) * THUMB_LEN
            girth = 1.0 if k == 0 else THUMB_GIRTH   # and thicker than the fingers, as a thumb is
            acc += w * (qs + (V[vi] - q) * girth); tot += w; continue
        else:
            qs = J[n][0] + (q - J[n][0]) * FINGER_LEN   # the point on the bone, shortened about the knuckle
        girth = 1.0 if k == 2 else FINGER_GIRTH   # (2026-11-20) the end joints keep their width: widened, the nails came out round
        acc += w * (qs + (V[vi] - q) * girth); tot += w
    if tot > 0: Vthin[vi] = acc + V[vi] * (1 - tot)   # acc carries the finger weights already; the rest stays put
    wh = W[vi, bones.index('Hand')]
    if wh > 0: Vthin[vi] -= pn * ((V[vi] - pc) @ pn) * (1 - PALM_THIN) * wh
V = Vthin
for n in names[1:]: J[n] = [J[n][0] + (j - J[n][0]) * FINGER_LEN for j in J[n]]
J['Thumb'] = J['Thumb'][:2] + [J['Thumb'][1] + (j - J['Thumb'][1]) * THUMB_LEN for j in J['Thumb'][2:]]
# Where a finger meets the back of the hand its hold goes from 0 to 1 over a short band (the palm step above), so
# the thicker fingers stood a step proud of the hand there and left creased lines down its sides: that band is smoothed
band = np.clip(4 * fw * (1 - fw), 0, 1)
for _ in range(10):
    avg = np.array([V[n_].mean(0) if n_ else V[i] for i, n_ in enumerate(nb_)])
    V = V + 0.5 * band[:, None] * (avg - V)
# Knuckles (art direction, 2026-11-19): the back of the hand had no knuckles at all and read as a glove. Each finger's
# joints raise the skin on the back over them - the big knuckle at the root, smaller ones at the two finger joints.
for n in names[1:]:
    for ji, hgt, rad in KNUCKLES:
        dv = V - J[n][ji]; dv -= dorsal[None] * (dv @ dorsal)[:, None]
        d = np.linalg.norm(dv, axis=1) / pl_
        V = V + dorsal[None] * (hgt * pl_ * np.exp(-(d / rad) ** 2) * back_)[:, None]
# Fingertip pads (2026-11-20): the ends of the fingers were plain rounded tubes with the nail on the very end, like
# a button; a real fingertip's pad bulges under and a little past the nail, so the nail lies on top. On the palm side
# of each end joint, past its middle, the skin is pushed outward from the bone and the last of it forward.
for n in names:
    a_, b_ = J[n][2], J[n][3]; d_ = b_ - a_; L_ = np.linalg.norm(d_); ax3 = d_ / L_
    t_ = np.clip((V - a_) @ ax3 / L_, 0, 1.2); w_ = W[:, bones.index(n + '3')]
    radial = (V - a_) - ax3[None] * ((V - a_) @ ax3)[:, None]
    sel = (w_ > 0.5) & (back_ < 0.2)
    if not sel.any(): continue
    down = radial[sel].mean(0); down -= ax3 * (down @ ax3); down /= max(np.linalg.norm(down), 1e-9)   # this finger's palm side
    # pushed that one way only (pushed outward all round the tips came out wide, like spatulas)
    pad = np.clip((radial @ down) / max(np.abs(radial @ down).max(), 1e-9), 0, 1) * w_ * ss(0.3, 0.7, t_) * (1 - 0.7 * ss(0.85, 1.05, t_))
    V = V + down[None] * (PULP_OUT * pl_ * pad)[:, None] + ax3[None] * (PULP_FWD * pl_ * pad)[:, None]
    # and the end joint bent a little toward the palm, as a relaxed finger's is (straight, the nail faced forward off
    # the end of the finger like a cap; bent, it lies on top and the pad leads)
    side3 = np.cross(ax3, down); side3 /= max(np.linalg.norm(side3), 1e-9)
    ang = np.radians(TIP_BEND) * w_ * ss(0.15, 0.55, t_)
    q = V - a_; c_, s_ = np.cos(ang)[:, None], np.sin(ang)[:, None]
    # turned about side3 through the joint, the way that takes the tip toward the palm
    sgn = 1.0 if np.cross(side3, ax3) @ down > 0 else -1.0
    V = a_ + q * c_ + np.cross(side3[None] * sgn, q) * s_ + side3[None] * (q @ side3)[:, None] * (1 - c_)
print('FINGERTIP pads raised')
# and the thumb's two knuckles, on its nail side (outward from its own bone: it is turned away from the back of
# the hand); without them it read as one even sausage
for ji, hgt, rad in THUMB_KNUCKLES:
    j0 = J['Thumb'][ji]; dj = J['Thumb'][min(ji + 1, 3)] - J['Thumb'][ji - 1]; dj /= np.linalg.norm(dj)
    rv = (V - j0) - dj[None] * ((V - j0) @ dj)[:, None]; rl = np.linalg.norm(rv, axis=1)
    d = np.linalg.norm(V - j0, axis=1) / pl_
    V = V + (rv / np.maximum(rl, 1e-9)[:, None]) * (hgt * pl_ * np.exp(-(d / rad) ** 2) * back_ * tw)[:, None]
# the moved shape back into the mesh, so the normals read below are those of the turned thumb (until now they were
# the generated shape's, close enough while only the thinning moved it)
for vi, v in enumerate(hand.data.vertices): v.co = V[vi]
hand.data.update()

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
verts = []; tris = []; fit_bones = {}
for side in ('Left', 'Right'):
    flip = np.array([-1.0, 1, 1]) if side == 'Right' else np.ones(3)
    Vs = V * flip; Ns = N * flip; Js = {n: [j * flip for j in J[n]] for n in names}
    def at(n, i): return TJ[P + side + 'Hand' + n + str(i)]
    lo = sum(np.linalg.norm(at(n, i + 1) - at(n, i)) for n in names[1:] for i in (1, 2, 3)); ln = sum(np.linalg.norm(J_size[n][i + 1] - J_size[n][i]) for n in names[1:] for i in range(3))
    sc_ = lo / ln
    A = np.array([Js[n][0] for n in names[1:]]); B = np.array([at(n, 1) for n in names[1:]])
    nb = np.cross(B[0] - B[3], B[1] - TJ[P + side + 'Hand']); nb /= np.linalg.norm(nb); nb *= np.sign(nb[2])
    na = np.array([0.0, -1, 0])
    A2 = np.vstack([A, A + na * 0.03 / sc_, A.mean(0) - np.array([0, 0, 0.05 / sc_])]); wdir = TJ[P + side + 'Hand'] - B.mean(0); wdir -= nb * (wdir @ nb); wdir /= np.linalg.norm(wdir)
    B2 = np.vstack([B, B + nb * 0.03, B.mean(0) + wdir * 0.05])
    ca, cb = A2.mean(0), B2.mean(0); Hm = (A2 - ca).T @ (B2 - cb); Uu, _, Vt = np.linalg.svd(Hm); D = np.diag([1, 1, np.sign(np.linalg.det(Vt.T @ Uu.T))]); Rp = Vt.T @ D @ Uu.T
    palm = (Rp * sc_, cb - Rp @ ca * sc_)
    # the generated palm is shorter than the rig's (whose Hand joint sits up the forearm), so placed by its
    # knuckles its wrist stopped short of the sleeve and the stub's cut end showed. The forearm part is turned onto
    # the rig's own forearm line and set on toward the rig's wrist joint. (Only shifted, the automatic weights
    # stretched the back of the hand into a fist-like dome.)
    ws = np.array(wrist) * flip
    w_at = ws @ palm[0].T + palm[1]
    reach = TJ[P + side + 'Hand'] - w_at
    Rf = rot_between(Rp @ np.array([0.0, 0, -1]), TJ[P + side + 'ForeArm'] - TJ[P + side + 'Hand']) @ Rp
    fore = (Rf * sc_, w_at + reach * WRIST_REACH - Rf @ ws * sc_)
    print(side, 'wrist moved %.3f toward the rig wrist joint, forearm turned %.0f deg' % (np.linalg.norm(reach) * WRIST_REACH, np.degrees(np.arccos(np.clip((np.trace(Rf @ Rp.T) - 1) / 2, -1, 1)))))
    Tm = [fore, palm]; Rl = [Rf, Rp]; bone = [index[P + side + 'ForeArm'], index[P + side + 'Hand']]
    # (2026-11-18, the video's way round: the bones go to the model's joints, not the model to the bones) The
    # fingers ride with the palm, unstretched; the rig's finger bones are moved onto the hand's own joints in Unity
    # (apply_hybrid.cs reads <out>_bones.json). Stretched and bent segment by segment onto the old rig's finger
    # joints - spaced and proportioned unlike any real hand - the fingers ran together into one block in the grip.
    for n in names:
        for i in range(3):
            Tm.append(palm); Rl.append(Rp); bone.append(index[P + side + 'Hand' + n + str(i + 1)])
            fit_bones[P + side + 'Hand' + n + str(i + 1)] = to_unity(Js[n][i] @ palm[0].T + palm[1]).tolist()
        fit_bones[P + side + 'Hand' + n + '4'] = to_unity(Js[n][3] @ palm[0].T + palm[1]).tolist()
    Pn = np.zeros_like(Vs); Nn = np.zeros_like(Ns)
    for b_, ((A_, t_), R_) in enumerate(zip(Tm, Rl)):
        Pn += W[:, b_:b_ + 1] * (Vs @ A_.T + t_); Nn += W[:, b_:b_ + 1] * (Ns @ R_.T)
    err = max(np.linalg.norm((Js[n][0] @ palm[0].T + palm[1]) - at(n, 1)) for n in names[1:])
    print(side, 'scale %.3f knuckle fit error %.4f' % (sc_, err))
    key = {}
    for poly in hand.data.polygons:
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
json.dump(fit_bones, open(out + '_bones.json', 'w'))   # finger joints in the mesh's own space, for apply_hybrid.cs
print('WROTE', out, len(verts), 'vertices', len(tris), 'triangles')
