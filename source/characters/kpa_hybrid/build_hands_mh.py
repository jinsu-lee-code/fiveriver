# Hands for the KPA rifleman from the MakeHuman base mesh (MPFB, CC0), 2026-11-20.
#
# build_hands.py made the hands over from a generated hand and then pushed its shape about by formula (thumb moved,
# fingers shortened, knuckles raised...). In the grip they still read as clay: no tendons, uneven joints, a thumb
# like a tube. A hand modelled as a hand is used instead: the MakeHuman base mesh's own (male, Asian macro), with
# its own finger skeleton and painted weights, cut off 7 cm up the forearm, subdivided once, and placed on the
# game's rig the same way as before (knuckles onto the rig's knuckles, scaled by the fingers; the rig's finger
# bones then go to the hand's own joints through <out>_bones.json, read by apply_hybrid.cs).
#
#   blender -b -P build_hands_mh.py -- <old SK_CH_KPA_Rifleman.fbx> <skin diffuse png> <unity_bones.txt> <out.bin> [<joints.json>]
# (not --factory-startup: the MPFB extension must load). Writes <out.bin>, <out.bin>_bones.json and
# <out.bin>_BaseColor.png in the same formats as build_hands.py; the normal map is made from the colour
# (detail_normal.py).
import bpy, bmesh, sys, struct, json
import numpy as np
from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.targetservice import TargetService

old_fbx, skin_png, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
P = 'mixamorig:'
STUB, WRIST_REACH, TEX = 0.07, 0.8, 1024
HEAD_SKIN = (0.465, 0.368, 0.275)   # mean skin colour of T_CH_KPA_Head0-2_BaseColor: the hands' map is scaled to it
names = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']

for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=old_fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
OJ = {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}
TJ = dict(OJ)
if len(sys.argv) > sys.argv.index('--') + 5:
    TJ.update({k: np.array(v) for k, v in json.load(open(sys.argv[sys.argv.index('--') + 5])).items()})
for o in list(bpy.data.objects): bpy.data.objects.remove(o)

# ---- 1. the human, and his left hand
macro = TargetService.get_default_macro_info_dict()
for k, v in (('gender', 1.0), ('age', 0.5), ('muscle', 0.7), ('weight', 0.45)):
    if k in macro: macro[k] = v
for k in macro['race']: macro['race'][k] = 1.0 if k == 'asian' else 0.0
print('MACRO', macro)
human = HumanService.create_human(mask_helpers=False, macro_detail_dict=macro)
HumanService.add_builtin_rig(human, 'default')
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
def head(n): return np.array(rig.matrix_world @ rig.data.bones[n].head_local)
def tail(n): return np.array(rig.matrix_world @ rig.data.bones[n].tail_local)
J0 = {'Thumb': [head('finger1-1.L'), head('finger1-2.L'), head('finger1-3.L'), tail('finger1-3.L')]}
for i, n in enumerate(names[1:]):
    f = 'finger%d-' % (i + 2)
    J0[n] = [head(f + '1.L'), head(f + '2.L'), head(f + '3.L'), tail(f + '3.L')]
wrist0 = head('wrist.L'); elbow0 = head('lowerarm01.L')
# which rig bone each of the hand's vertex groups goes to (columns: forearm, hand, then three per finger)
cols = {'lowerarm01.L': 0, 'lowerarm02.L': 0, 'wrist.L': 1}
for k in range(1, 5): cols['metacarpal%d.L' % k] = 1
for i in range(5):
    for j in range(3): cols['finger%d-%d.L' % (i + 1, j + 1)] = 2 + i * 3 + j

bpy.ops.object.select_all(action='DESELECT'); human.select_set(True); bpy.context.view_layer.objects.active = human
for m in list(human.modifiers): human.modifiers.remove(m)
if human.data.shape_keys: bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
human.data.transform(human.matrix_world); human.matrix_world.identity()
gi = {g.index: g.name for g in human.vertex_groups}
fore_ax = (wrist0 - elbow0) / np.linalg.norm(wrist0 - elbow0)
keep = set()
for v in human.data.vertices:
    gs = {gi[g.group]: g.weight for g in v.groups}
    if 'body' not in gs: continue
    hw = sum(w for n, w in gs.items() if n in cols and cols[n] >= 1)
    d = (np.array(v.co) - wrist0) @ fore_ax
    if hw > 0.01 and d > -STUB: keep.add(v.index)
    elif d > -STUB and any(n in ('lowerarm01.L', 'lowerarm02.L') and w > 0.01 for n, w in gs.items()) and v.co.x > 0: keep.add(v.index)
bm = bmesh.new(); bm.from_mesh(human.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
bm.to_mesh(human.data); bm.free(); human.data.update()
sub = human.modifiers.new('sub', 'SUBSURF'); sub.levels = 1; bpy.ops.object.modifier_apply(modifier=sub.name)
hand = human
print('HAND cut: %d vertices, %d faces' % (len(hand.data.vertices), len(hand.data.polygons)))

# ---- 2. into the frame build_hands.py used (fingers along +z, the back of the hand toward -y, a left hand)
V = np.array([v.co[:] for v in hand.data.vertices]); N = np.array([v.normal[:] for v in hand.data.vertices])
kn = np.array([J0[n][0] for n in names[1:]])
zc = kn.mean(0) - wrist0; zc /= np.linalg.norm(zc)
back = np.cross(kn[0] - wrist0, kn[3] - wrist0); back /= np.linalg.norm(back)
# the back of the hand is the side the vertex normals over the metacarpals face
over = (np.linalg.norm(V - (wrist0 + kn.mean(0)) / 2, axis=1) < 0.03)
if (N[over] @ back).mean() < 0: pass
side_sum = [(N[over & ((V - wrist0) @ back * s > 0)] @ (back * s)).sum() for s in (1, -1)]
# the nails' side: fingertip joints lie nearer the back surface; decided by the thumb, which lies on the palm side
if (J0['Thumb'][3] - wrist0) @ back > 0: back = -back
yc = -back; yc -= zc * (yc @ zc); yc /= np.linalg.norm(yc)
xc = np.cross(yc, zc)
R = np.stack([xc, yc, zc])            # rows: the new axes
def canon(p): return (np.asarray(p) - wrist0) @ R.T
V = canon(V); N = N @ R.T
J = {n: [canon(j) for j in J0[n]] for n in names}; wrist = np.zeros(3)
W = np.zeros((len(V), 17))
gi = {g.index: g.name for g in hand.vertex_groups}
for v in hand.data.vertices:
    for g in v.groups:
        c = cols.get(gi[g.group])
        if c is not None: W[v.index, c] += g.weight
none = W.sum(1) < 1e-6; W[none, 1] = 1.0
W /= W.sum(1, keepdims=True)
print('FRAME thumb side x %+.3f, fingertip z %.3f, weights: %d vertices had none' % (J['Thumb'][3][0], J['Middle'][3][2], int(none.sum())))
for n in names:
    seg = [np.linalg.norm(J[n][i + 1] - J[n][i]) for i in range(3)]
    print('  %-6s %s  total/palm %.2f' % (n, ' '.join('%.1f' % (s * 1000) for s in seg), sum(seg) / np.linalg.norm(J['Middle'][0])))

# ---- texture: the skin map cropped to the hand's part of it, matched to the heads' skin colour
uvl = hand.data.uv_layers.active.data
uvs = np.array([l.uv[:] for l in uvl]); lo = uvs.min(0) - 0.004; hi = uvs.max(0) + 0.004
src = bpy.data.images.load(skin_png); sw, sh = src.size
px = np.array(src.pixels[:], dtype=np.float32).reshape(sh, sw, 4)
x0, x1 = int(lo[0] * sw), int(np.ceil(hi[0] * sw)); y0, y1 = int(lo[1] * sh), int(np.ceil(hi[1] * sh))
crop = px[max(y0, 0):y1, max(x0, 0):x1, :3]
# bilinear up to TEX
ys = np.linspace(0, crop.shape[0] - 1, TEX); xs = np.linspace(0, crop.shape[1] - 1, TEX)
yi = np.floor(ys).astype(int); xi = np.floor(xs).astype(int); yf = (ys - yi)[:, None, None]; xf = (xs - xi)[None, :, None]
yj = np.minimum(yi + 1, crop.shape[0] - 1); xj = np.minimum(xi + 1, crop.shape[1] - 1)
big = (crop[yi][:, xi] * (1 - yf) * (1 - xf) + crop[yi][:, xj] * (1 - yf) * xf + crop[yj][:, xi] * yf * (1 - xf) + crop[yj][:, xj] * yf * xf)
r_, g_, b_ = big[..., 0], big[..., 1], big[..., 2]
skin_m = (r_ > g_ * 1.05) & (g_ > b_) & (r_ > 0.25)
gain = np.array(HEAD_SKIN) / big[skin_m].mean(0) if skin_m.any() else np.ones(3)
# colours in Blender's pixel buffer are scene-linear for an sRGB image; HEAD_SKIN was measured on sRGB values
def to_srgb(c): return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.clip(c, 1e-9, None) ** (1 / 2.4) - 0.055)
srgb = to_srgb(big); sm = srgb[skin_m].mean(0) if skin_m.any() else np.ones(3)
srgb = np.clip(srgb * (np.array(HEAD_SKIN) / sm), 0, 1)
print('SKIN map cropped %dx%d -> %d, matched to the heads: gain' % (crop.shape[1], crop.shape[0], TEX), np.round(np.array(HEAD_SKIN) / sm, 3))
img = bpy.data.images.new('BaseColor', TEX, TEX, alpha=False); img.colorspace_settings.name = 'Non-Color'
outpx = np.ones((TEX, TEX, 4), np.float32); outpx[..., :3] = srgb
img.pixels[:] = outpx.ravel(); img.filepath_raw = out + '_BaseColor.png'; img.file_format = 'PNG'; img.save()
def uvmap(u): return ((u[0] - x0 / sw) / ((x1 - x0) / sw), (u[1] - y0 / sh) / ((y1 - y0) / sh))

# ---- 3. into the rig's bind pose, per side (as build_hands.py)
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
verts = []; tris = []; fit_bones = {}
for side in ('Left', 'Right'):
    flip = np.array([-1.0, 1, 1]) if side == 'Right' else np.ones(3)
    Vs = V * flip; Ns = N * flip; Js = {n: [j * flip for j in J[n]] for n in names}
    def at(n, i): return TJ[P + side + 'Hand' + n + str(i)]
    lo_ = sum(np.linalg.norm(at(n, i + 1) - at(n, i)) for n in names[1:] for i in (1, 2, 3)); ln = sum(np.linalg.norm(J[n][i + 1] - J[n][i]) for n in names[1:] for i in range(3))
    sc_ = lo_ / ln
    A = np.array([Js[n][0] for n in names[1:]]); B = np.array([at(n, 1) for n in names[1:]])
    nb = np.cross(B[0] - B[3], B[1] - TJ[P + side + 'Hand']); nb /= np.linalg.norm(nb); nb *= np.sign(nb[2])
    na = np.array([0.0, -1, 0])
    A2 = np.vstack([A, A + na * 0.03 / sc_, A.mean(0) - np.array([0, 0, 0.05 / sc_])]); wdir = TJ[P + side + 'Hand'] - B.mean(0); wdir -= nb * (wdir @ nb); wdir /= np.linalg.norm(wdir)
    B2 = np.vstack([B, B + nb * 0.03, B.mean(0) + wdir * 0.05])
    ca, cb = A2.mean(0), B2.mean(0); Hm = (A2 - ca).T @ (B2 - cb); Uu, _, Vt = np.linalg.svd(Hm); D = np.diag([1, 1, np.sign(np.linalg.det(Vt.T @ Uu.T))]); Rp = Vt.T @ D @ Uu.T
    palm = (Rp * sc_, cb - Rp @ ca * sc_)
    ws = wrist * flip
    w_at = ws @ palm[0].T + palm[1]
    reach = TJ[P + side + 'Hand'] - w_at
    Rf = rot_between(Rp @ np.array([0.0, 0, -1]), TJ[P + side + 'ForeArm'] - TJ[P + side + 'Hand']) @ Rp
    fore = (Rf * sc_, w_at + reach * WRIST_REACH - Rf @ ws * sc_)
    print(side, 'wrist %.3f from the rig wrist joint, forearm turned %.0f deg' % (np.linalg.norm(reach), np.degrees(np.arccos(np.clip((np.trace(Rf @ Rp.T) - 1) / 2, -1, 1)))))
    Tm = [fore, palm]; Rl = [Rf, Rp]; bone = [index[P + side + 'ForeArm'], index[P + side + 'Hand']]
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
            vi = hand.data.loops[li].vertex_index; u2 = uvmap(uvl[li].uv); kk = (side, vi, round(u2[0], 5), round(u2[1], 5))
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
json.dump(fit_bones, open(out + '_bones.json', 'w'))
print('WROTE', out, len(verts), 'vertices', len(tris), 'triangles')
