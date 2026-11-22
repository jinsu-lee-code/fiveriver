# KPA rifleman: the generated uniform and head on the game's own skeleton and hands (2026-11-15).
#
# The delivered enemy has a rough, lumpy uniform and a face with baked black streaks; a character generated in
# VARCO from a T-pose picture has a clean uniform but mitten hands. The generated body is fitted onto the old
# skeleton (so the avatar, clips, IK, both prefabs and the tests stay as they are), its hands are cut off at the
# wrists and the old hands stay (since 2026-11-18 the hands from build_hands.py, given as an extra <hands.bin>, take
# their place in this one mesh).
#
#   blender -b --factory-startup -P build_hybrid.py -- <old SK_CH_KPA_Rifleman.fbx> <new rigged.fbx> <unity_bones.txt> <out.bin>
# unity_bones.txt: the old mesh's bones in the order of its SkinnedMeshRenderer, with their bind positions in
# the old mesh's own space (bindpose^-1 * 0). The output is in that space, so the old bind poses are reused.
import bpy, os, json, sys, struct, math
import numpy as np
from mathutils import Vector

old_fbx, new_fbx, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
extra = sys.argv[sys.argv.index('--') + 5:]
heads = [a for a in extra if a.lower().endswith('.glb')]   # optional: head busts; each is written next to 'out' as <out>.head<N>.bin
hands_bin = next((a for a in extra if a.lower().endswith('.bin')), None)   # optional: build_hands.py's hands, in place of the old ones
# 'own-joints' (2026-11-18, the Rigify videos' way): the body is not stretched onto the old rig; every bone is put on
# the body's own joints instead (written to <out>_joints.json for build_hands.py and apply_hybrid.cs) and the body is
# bound to them with Blender's automatic weights, then checked by bending the joints (renders <out>_bend_*.png)
own = 'own-joints' in extra
TWIST_SEGS = 7   # forearm bands (the elbow-twist video cuts the lower arm into seven)
P = 'mixamorig:'
bpy.ops.wm.read_factory_settings(use_empty=True)


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == 'ARMATURE')
    mesh = max((o for o in new if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
    return arm, mesh


def joints(arm):
    return {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}


oa, om = load(old_fbx)
na, nm = load(new_fbx)
oj, nj = joints(oa), joints(na)
print('NEW BONES', ' '.join(n for n in nj if 'Finger' not in n))

# ---- 1. the new body into the old model's frame: per axis scale and offset fitted on the shared joints
pairs = [(n, P + n) for n in nj if P + n in oj and 'Finger' not in n and n != 'Root']
A = np.array([nj[a] for a, b in pairs]); B = np.array([oj[b] for a, b in pairs])
S = np.ones(3); T = np.zeros(3)
for ax in range(3):
    if A[:, ax].std() > 0.05 * max(A.std(0)):
        S[ax], T[ax] = np.polyfit(A[:, ax], B[:, ax], 1)
flat = [ax for ax in range(3) if A[:, ax].std() <= 0.05 * max(A.std(0))]
for ax in flat:                                     # the depth axis: joints lie in a plane, so the scale is borrowed
    S[ax] = np.mean([S[k] for k in range(3) if k not in flat]); T[ax] = (B[:, ax] - A[:, ax] * S[ax]).mean()
# one scale for the whole body, taken from the height: fitted per axis the short arms of the old rig narrowed the
# chest and hips by a fifth and he came out thin as a rail. The arms are fitted separately below.
upx = int(np.argmax(np.abs(oj[P + 'Head'] - oj[P + 'Hips'])))
core = [i for i, (a_, b_) in enumerate(pairs) if not any(k in a_ for k in ('Arm', 'Hand', 'Shoulder'))]
for k in range(3):
    if k == upx: continue
    S[k] = S[upx]; T[k] = (B[core, k] - A[core, k] * S[k]).mean()
err = np.abs(A * S + T - B)
for (a_, b_), e_, x_, y_ in zip(pairs, err, A * S + T, B):
    if any(k in a_ for k in ('Leg', 'Foot', 'Toe', 'Hips')): print('JOINT %-14s new %s old %s diff %s' % (a_, np.round(x_, 3), np.round(y_, 3), np.round(y_ - x_, 3)))
print('FIT scale', S.round(3), 'offset', T.round(3), 'joint error mean %.3f max %.3f m (%s)' % (err.mean(), err.max(), pairs[int(err.max(1).argmax())][0]))
nv = np.array([nm.matrix_world @ v.co for v in nm.data.vertices]) * S + T
ax = int(np.argmax(np.abs(oj[P + 'LeftHand'] - oj[P + 'RightHand'])))   # the axis the arms lie on
up = int(np.argmax(np.abs(oj[P + 'Head'] - oj[P + 'Hips'])))
if not own:   # (own-joints: the body keeps its own proportions; the bones go to it instead)
    # the old rig's arms are short for its height: beyond each shoulder the arm is fitted on its own, joint to joint
    # (shoulder -> elbow -> wrist), along the axis the arms lie on
    # only the upper body is touched (the wide breeches reach as far out as the shoulders), easing in between the
    # waist and the chest, and the map is continuous from the spine outward so nothing tears at the shoulder
    z0, z1 = oj[P + 'Spine'][up], oj[P + 'Spine2'][up]
    h = np.clip((nv[:, up] - z0) / (z1 - z0), 0, 1); h = h * h * (3 - 2 * h)
    mid = oj[P + 'Spine2'][ax]
    x0 = nv[:, ax].copy(); moved = nv.copy()
    for s in ('Left', 'Right'):
        src = [mid] + [nj[s + n][ax] * S[ax] + T[ax] for n in ('Arm', 'ForeArm', 'Hand')]
        dst = [mid] + [oj[P + s + n][ax] for n in ('Arm', 'ForeArm', 'Hand')]
        sign = 1.0 if src[3] > src[0] else -1.0
        sel = sign * (x0 - mid) > 0
        xs = x0[sel]
        y = np.interp(sign * xs, [sign * v for v in src], [sign * v for v in dst]) * 1.0
        far = sign * (xs - src[3]) > 0                    # beyond the wrist: the last segment's slope carries on
        y = np.where(far, sign * (sign * dst[3] + (sign * xs - sign * src[3]) * (dst[3] - dst[2]) / (src[3] - src[2])), sign * y)
        moved[sel, ax] = y
        for k in range(3):
            if k == ax: continue
            d1 = oj[P + s + 'Hand'][k] - (nj[s + 'Hand'][k] * S[k] + T[k])
            t = np.clip((sign * xs - sign * src[1]) / (sign * src[3] - sign * src[1]), 0, 1.2)
            moved[sel, k] += d1 * t
        print('ARM', s, 'src', np.round(src, 3), 'dst', np.round(dst, 3))
    nv = nv + (moved - nv) * h[:, None]
    # the legs: the generated body stands with its feet apart, so its knees, ankles and toes lay up to 8 cm outside
    # the old rig's joints and the shins crumpled when a knee bent. Each leg is brought onto the old joints, level by
    # level from the hip down (a shear, which for this small an angle is as good as turning the leg in)
    zhip = oj[P + 'Hips'][up]
    for s in ('Left', 'Right'):
        chain = ('UpLeg', 'Leg', 'Foot', 'ToeBase')
        src = np.array([nj[s + n] * S + T for n in chain]); dst = np.array([oj[P + s + n] for n in chain])
        side = np.sign(dst[2][ax] - oj[P + 'Hips'][ax])
        sel = (side * (nv[:, ax] - oj[P + 'Hips'][ax]) > 0) & (nv[:, up] < zhip)
        z = nv[sel, up]
        order = np.argsort(src[:, up])                       # interp wants rising heights
        g = np.clip((src[0][up] - z) / 0.10, 0, 1)[:, None]  # eases in below the hip joint
        add = np.zeros((sel.sum(), 3))
        for k in range(3):
            add[:, k] = np.interp(z, src[order, up], (dst - src)[order, k])
        nv[sel] += add * g
        print('LEG', s, 'moved', int(sel.sum()), 'ankle shift', np.round((dst - src)[2], 3))
nn = np.array([(nm.matrix_world.to_3x3() @ v.normal).normalized() for v in nm.data.vertices])
ov = np.array([om.matrix_world @ v.co for v in om.data.vertices])
on = np.array([(om.matrix_world.to_3x3() @ v.normal).normalized() for v in om.data.vertices])

# ---- 2. Blender frame -> the old mesh's own space in Unity (a similarity, fitted on the old bones; may mirror)
names = []; U = []
for line in open(bones_txt, encoding='utf-8-sig'):
    p = line.split()
    if len(p) == 4: names.append(p[0]); U.append([float(x) for x in p[1:]])
U = np.array(U); Bl = np.array([oj[n] for n in names])
# a full affine map: the old mesh's space is not a plain scaled copy (its renderer carries an uneven scale)
X = np.hstack([Bl, np.ones((len(Bl), 1))])
M, *_ = np.linalg.lstsq(X, U, rcond=None)            # 4x3
R = M[:3].T; scale = abs(np.linalg.det(R)) ** (1 / 3)
def to_unity(p): return np.asarray(p) @ M[:3] + M[3]
res = np.abs(to_unity(Bl) - U).max() / scale
mirrored = np.linalg.det(R) < 0
Rn = np.linalg.inv(R).T                              # normals go with the inverse transpose
print('TO UNITY affine, scale about %.5f mirrored %s residual %.4f m' % (scale, mirrored, res))
index = {n: i for i, n in enumerate(names)}
if own:
    # the old rig's joints are replaced by the generated body's own (its skeleton, scaled like the body); the fingers
    # of its mitten hands give the knuckles the new hands are placed on. Done after the affine map above, which is
    # fitted on the old rig's bind positions and must stay as it is.
    ntail = {b.name: np.array(na.matrix_world @ b.tail_local) for b in na.data.bones}
    for a_, b_ in pairs: oj[b_] = nj[a_] * S + T
    for s_, c_ in (('Left', 'L'), ('Right', 'R')):
        for f_, fn in enumerate(('Thumb', 'Index', 'Middle', 'Ring', 'Pinky')):
            seg = ['HandLoRA_Bip01_%s_Finger%d%s' % (c_, f_, x) for x in ('', '1', '2')]
            for i_, b_ in enumerate(seg): oj[P + s_ + 'Hand' + fn + str(i_ + 1)] = nj[b_] * S + T
            oj[P + s_ + 'Hand' + fn + '4'] = ntail[seg[2]] * S + T
    json.dump({k: v.tolist() for k, v in oj.items()}, open(out + '_joints.json', 'w'))
    print('OWN JOINTS written, e.g. hand', np.round(oj[P + 'LeftHand'], 3), 'knee', np.round(oj[P + 'LeftLeg'], 3))

# ---- 3. weights
def weights(mesh, rename):
    groups = [rename(g.name) for g in mesh.vertex_groups]
    out_ = []
    for v in mesh.data.vertices:
        acc = {}
        for g in v.groups:
            b = groups[g.group]
            if b in index and g.weight > 0: acc[index[b]] = acc.get(index[b], 0.0) + g.weight
        top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
        if not top: top = [(index[P + 'Hips'], 1.0)]
        tot = sum(w for _, w in top)
        top += [(0, 0.0)] * (4 - len(top))
        out_.append(([i for i, _ in top], [w / tot for _, w in top]))
    return out_
def new_name(n):
    if 'Finger' in n: return P + ('LeftHand' if '_L_' in n else 'RightHand')
    return P + ('Hips' if n == 'Root' else n)
nw = weights(nm, new_name); ow = weights(om, lambda n: n)

# ---- 3b. heads. The body's own face has 10 texels to the centimetre (4.8% of its map) and reads as a smooth
# doll's face in the close shots, and every man has it. Separate busts, each with a map of its own, replace it:
# the body is cut at the narrowest level of the neck, and each bust is scaled so that its head (neck to crown) has
# the height of the body's head, set on the same spot and kept from 4 cm below its own neck upward.
def neck_level(v, lo, hi):
    best = (1e9, lo)
    z = lo
    while z < hi:
        s = v[(v[:, up] >= z) & (v[:, up] < z + 0.01)]
        if len(s) > 8:
            w = np.ptp(s[:, ax])
            if w < best[0]: best = (w, z)
        z += 0.005
    return best[1]
zneck = neck_level(nv, oj[P + 'Neck'][up] - 0.02, oj[P + 'Head'][up] + 0.06)
bh = nv[nv[:, up] > zneck]
depth = 3 - ax - up
print('BODY neck level %.3f, head above it: height %.3f width %.3f' % (zneck, np.ptp(bh[:, up]), np.ptp(bh[:, ax])))
head_out = []
for hi_, path in enumerate(heads):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    hm = max((o for o in bpy.data.objects if o not in before and o.type == 'MESH'), key=lambda o: len(o.data.vertices))
    hv = np.array([hm.matrix_world @ v.co for v in hm.data.vertices]); hn = np.array([(hm.matrix_world.to_3x3() @ v.normal).normalized() for v in hm.data.vertices])
    z0, z1 = hv[:, up].min(), hv[:, up].max()
    side = path[:-4] + '.json'          # a bust from rebake_head.py: already cut below its neck, the level is stored
    if os.path.exists(side): hneck = json.load(open(side))['neck']
    else: hneck = neck_level(hv, z0 + 0.15 * (z1 - z0), z0 + 0.55 * (z1 - z0))
    k = np.ptp(bh[:, up]) / (z1 - hneck)
    top = hv[hv[:, up] > hneck]
    off = np.zeros(3)
    off[up] = bh[:, up].max() - z1 * k
    off[ax] = (bh[:, ax].max() + bh[:, ax].min()) / 2 - (top[:, ax].max() + top[:, ax].min()) / 2 * k
    off[depth] = (bh[:, depth].max() + bh[:, depth].min()) / 2 - (top[:, depth].max() + top[:, depth].min()) / 2 * k
    hv = hv * k + off
    keep_from = hneck * k + off[up] - 0.06   # below the bust's own flat cut (rebake_head.py): nothing is dropped
    # the body's own neck runs inside the bottom of the head's: pushed out 4 mm there the head's skin always lies
    # over it (where both sat on the same surface they flickered through each other as torn shards, 2026-11-16)
    if hi_ == 0: hv_neck_ring = hv[np.abs(hv[:, up] - (hneck * k + off[up] - 0.02)) < 0.006]
    fade = np.clip((hneck * k + off[up] + 0.01 - hv[:, up]) / 0.02, 0, 1)
    hv = hv + hn * (0.004 * fade)[:, None]
    zj0, zj1 = oj[P + 'Neck'][up], oj[P + 'Head'][up]
    hw = []
    for p_ in hv:
        t = float(np.clip((p_[up] - zj0) / (zj1 - zj0), 0, 1))
        hw.append(([index[P + 'Head'], index[P + 'Neck'], 0, 0], [t, 1 - t, 0.0, 0.0]))
    huv = hm.data.uv_layers.active.data
    hverts = []; hkey = {}; htris = []
    for poly in hm.data.polygons:
        if hv[list(poly.vertices)][:, up].mean() < keep_from: continue
        ids = []
        for li in poly.loop_indices:
            vi = hm.data.loops[li].vertex_index; u_ = tuple(huv[li].uv); kk = (vi, round(u_[0], 5), round(u_[1], 5))
            if kk not in hkey: hkey[kk] = len(hverts); hverts.append((to_unity(hv[vi]), hn[vi] @ Rn.T, u_, hw[vi]))
            ids.append(hkey[kk])
        for q in range(1, len(ids) - 1): htris.append((ids[0], ids[q], ids[q + 1]))
    for node in hm.data.materials[0].node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image:
            role = [l.to_socket.name for l in node.outputs[0].links]
            name = 'BaseColor' if 'Base Color' in role else 'Normal' if 'Color' in role and any(l.to_node.type == 'NORMAL_MAP' for l in node.outputs[0].links) else 'ORM'
            node.image.filepath_raw = '%s.head%d_%s.png' % (out, hi_, name); node.image.file_format = 'PNG'; node.image.save()
    head_out.append((hverts, htris))
    print('HEAD %d scale %.3f bust neck %.3f kept from %.3f: %d vertices %d triangles, width %.3f' % (hi_, k, hneck * k + off[up], keep_from, len(hverts), len(htris), np.ptp(top[:, ax]) * k))

# ---- 4. faces: the new body without its hands, the old hands
wrist = {s: oj[P + s + 'Hand'] for s in ('Left', 'Right')}
elbow = {s: oj[P + s + 'ForeArm'] for s in ('Left', 'Right')}
def past_wrist(p, margin):
    for s in ('Left', 'Right'):
        axis = wrist[s] - elbow[s]; L = np.linalg.norm(axis); axis = axis / L
        d = (p - wrist[s]) @ axis
        if d > margin and np.linalg.norm((p - wrist[s]) - axis * d) < 0.25: return True
    return False
verts = []; key = {}; subs = [[], [], []]
def corner(tag, vi, pos, nrm, uv, w):
    k = (tag, vi, round(uv[0], 5), round(uv[1], 5))
    if k not in key:
        key[k] = len(verts); verts.append((to_unity(pos), nrm @ Rn.T, uv, w))
    return key[k]
nuv = nm.data.uv_layers.active.data
# The body's neck under the head (2026-11-16): between its collar and the cut level the body still had its own
# neck skin, low in texels and cut in saw teeth, and the inside of the collar was painted skin. Both came through
# round the head's neck as orange shards. Whatever is skin-coloured in the body's map there goes; the collar
# (khaki) stays and the head's neck, which runs 4.5 cm down inside it, is what shows.
skin_tex = None
if heads:
    img = bpy.data.images.load(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../five_river/Assets/_Project/Art/Models/Characters/KPA_Hybrid/T_CH_KPA_Hybrid_BaseColor.png'))
    px = np.empty(img.size[0] * img.size[1] * 4, np.float32); img.pixels.foreach_get(px)
    skin_tex = px.reshape(img.size[1], img.size[0], 4)[..., :3]
neck_axis = (oj[P + 'Neck'] + oj[P + 'Head']) / 2
neck_r = 0.058
if heads:
    # the joints sit toward the back of the neck: the middle of the head's own neck ring at the level is used
    ring = hv_neck_ring if 'hv_neck_ring' in globals() else None
    if ring is not None and len(ring) > 20:
        for k_ in (ax, 3 - ax - up): neck_axis[k_] = (ring[:, k_].max() + ring[:, k_].min()) / 2
        dd = ring - neck_axis; dd[:, up] = 0; neck_r = float(np.median(np.linalg.norm(dd, axis=1)))
        print('NECK axis moved to the head ring, radius %.3f' % neck_r)
facing = float(np.sign((oj[P + 'LeftToeBase'] - oj[P + 'LeftFoot'])[depth]))   # +1 if the toes point along +depth
def neck_skin(poly, c):
    # skin, or the white of the shirt inside the collar (it showed as a pale sliver beside the head's neck)
    if skin_tex is None or not (zneck - 0.07 < c[up] <= zneck + 0.04): return False
    d = c - neck_axis; d[up] = 0
    if np.linalg.norm(d) > 0.11: return False
    uvs = np.array([nuv[li].uv[:] for li in poly.loop_indices]).mean(0)
    h, w = skin_tex.shape[:2]
    r, g, b = skin_tex[min(h - 1, int(uvs[1] % 1 * h)), min(w - 1, int(uvs[0] % 1 * w))]
    return (r > g * 1.15 and r - b > 0.15) or (min(r, g, b) > 0.62 and max(r, g, b) - min(r, g, b) < 0.12)
def collar_cloth(poly, c):
    # above the cut level only the stand of the collar may stay: khaki, and outside the neck
    if skin_tex is None or np.linalg.norm((c - neck_axis) * (np.arange(3) != up)) < 0.065: return False
    # only the back and sides stand up past the level: the front ends of the collar there were loose shards
    # beside the chin (2026-11-16)
    if (c - neck_axis)[depth] * facing > -0.01: return False
    uvs = np.array([nuv[li].uv[:] for li in poly.loop_indices]).mean(0)
    h, w = skin_tex.shape[:2]
    r, g, b = skin_tex[min(h - 1, int(uvs[1] % 1 * h)), min(w - 1, int(uvs[0] % 1 * w))]
    return 0.25 < g < 0.75 and abs(r - g) < 0.08 and g - b > 0.05
def inside_collar(c):
    # Whatever of the body lies inside the head's neck (which runs 4.5 cm down into the collar) is never meant to
    # be seen: its lining and the cut ends of the collar's front came through round the neck as dark and pale
    # shards whatever their colour (2026-11-16). The front ends of the collar near the level go as well.
    d = c - neck_axis; d[up] = 0; r = np.linalg.norm(d)
    return zneck - 0.05 < c[up] and r < neck_r - 0.012   # well inside the head's neck only: the collar itself stays
# (2026-11-18) The body's own wrist skin between the end of its sleeve and the cut showed as a pale peach band
# beside the hands. Deleting it left the sleeve's end in torn flaps; instead it is drawn in toward the forearm's axis
# (to 10%, from 12 cm up the arm), under the hand's own wrist (at 35% it still showed beside the whole-model rig's
# hands as a pale jagged patch on the cuff).
if skin_tex is not None:
    tuck = set()
    for poly in nm.data.polygons:
        c = nv[list(poly.vertices)].mean(0)
        for s_ in ('Left', 'Right'):
            axis = wrist[s_] - elbow[s_]; axis = axis / np.linalg.norm(axis); d = (c - wrist[s_]) @ axis
            if -0.12 < d and np.linalg.norm((c - wrist[s_]) - axis * d) < 0.09: break
        else: continue
        uvs = np.array([nuv[li].uv[:] for li in poly.loop_indices]).mean(0)
        h, w = skin_tex.shape[:2]
        r, g, b = skin_tex[min(h - 1, int(uvs[1] % 1 * h)), min(w - 1, int(uvs[0] % 1 * w))]
        if r > g * 1.12 and r - b > 0.12: tuck.update((vi, s_) for vi in poly.vertices)
    for vi, s_ in tuck:
        axis = wrist[s_] - elbow[s_]; axis = axis / np.linalg.norm(axis)
        q = wrist[s_] + axis * ((nv[vi] - wrist[s_]) @ axis); nv[vi] = q + (nv[vi] - q) * 0.10
    print('WRIST skin tucked in: %d vertices' % len(tuck))
kept = cut = neckcut = 0
keep_polys = []
for poly in nm.data.polygons:
    c = nv[list(poly.vertices)].mean(0)
    if past_wrist(c, 0.012): cut += 1; continue
    # the collar stands up past the cut level at the back: it is kept to 4 cm above it (cut there along the level it
    # came out in saw teeth); everything else above the level is the body's own head and goes
    if heads and c[up] > zneck + 0.04: cut += 1; continue
    if neck_skin(poly, c): neckcut += 1; continue
    if heads and inside_collar(c): neckcut += 1; continue
    if heads and c[up] > zneck and not collar_cloth(poly, c): cut += 1; continue
    keep_polys.append(poly)
# (2026-11-20) Seen at 40 cm the collar's cut edge was still in saw teeth, with thin spikes standing off it at the
# front, whether he was turned for the aim or not: the faces dropped above (skin-coloured, inside the head's neck)
# leave the edge however the texels fell. Near the neck, a face hanging by two open edges that meet in a point
# (under 50 degrees) is taken off, three times over, and then each open edge vertex there is drawn halfway to the
# middle of its two neighbours along the edge, six times: the edge runs smooth round the neck.
if heads:
    def near_neck(c):
        d = c - neck_axis; d[up] = 0
        return c[up] > zneck - 0.15 and np.linalg.norm(d) < 0.16
    spikes = 0
    for _ in range(3):
        use = {}
        for poly in keep_polys:
            vs = list(poly.vertices)
            for i_ in range(len(vs)):
                e_ = tuple(sorted((vs[i_], vs[(i_ + 1) % len(vs)]))); use[e_] = use.get(e_, 0) + 1
        drop = set()
        for pi_, poly in enumerate(keep_polys):
            vs = list(poly.vertices)
            if not near_neck(nv[vs].mean(0)): continue
            n_ = len(vs)
            for i_ in range(n_):
                a_, b_, c_ = vs[i_ - 1], vs[i_], vs[(i_ + 1) % n_]
                if use[tuple(sorted((a_, b_)))] == 1 and use[tuple(sorted((b_, c_)))] == 1:
                    u_, w_ = nv[a_] - nv[b_], nv[c_] - nv[b_]
                    cosang = u_ @ w_ / max(1e-9, np.linalg.norm(u_) * np.linalg.norm(w_))
                    if cosang > np.cos(np.radians(50)): drop.add(pi_); break
        if not drop: break
        spikes += len(drop); keep_polys = [p_ for i_, p_ in enumerate(keep_polys) if i_ not in drop]
    use = {}
    for poly in keep_polys:
        vs = list(poly.vertices)
        for i_ in range(len(vs)):
            e_ = tuple(sorted((vs[i_], vs[(i_ + 1) % len(vs)]))); use[e_] = use.get(e_, 0) + 1
    edge_nb = {}
    for (a_, b_), n_ in use.items():
        if n_ == 1: edge_nb.setdefault(a_, []).append(b_); edge_nb.setdefault(b_, []).append(a_)
    loose = [v for v, nb in edge_nb.items() if len(nb) == 2 and near_neck(nv[v])]
    for _ in range(6):
        mid = {v: (nv[edge_nb[v][0]] + nv[edge_nb[v][1]]) / 2 for v in loose}
        for v in loose: nv[v] = nv[v] + (mid[v] - nv[v]) * 0.5
    print('COLLAR edge: %d spikes taken off, %d edge vertices smoothed' % (spikes, len(loose)))
# Shards (2026-11-16): what the cuts round the neck leave behind as small loose pieces of collar - a few faces no
# longer joined to the jacket - stood out beside the chin. Pieces of fewer than 60 faces near the neck are dropped.
parent = {}
def find(x):
    while parent.setdefault(x, x) != x: parent[x] = parent[parent[x]]; x = parent[x]
    return x
for poly in keep_polys:
    vs = list(poly.vertices)
    for v in vs[1:]: parent[find(v)] = find(vs[0])
size = {}
for poly in keep_polys: r_ = find(poly.vertices[0]); size[r_] = size.get(r_, 0) + 1
# (2026-11-18) The sleeve's open end stood a good centimetre off the new hands' wrists all round, and the dark
# inside of the sleeve showed past them. With the new hands the cuff is drawn in: its rim to 80% of its distance
# from the forearm's axis, the ring of faces behind it to 92%.
if hands_bin:
    use = {}
    for poly in keep_polys:
        vs = list(poly.vertices)
        for i_ in range(len(vs)):
            e_ = tuple(sorted((vs[i_], vs[(i_ + 1) % len(vs)]))); use[e_] = use.get(e_, 0) + 1
    rim_v = {v for e_, n_ in use.items() if n_ == 1 for v in e_}
    nbr = {}
    for e_ in use:
        nbr.setdefault(e_[0], set()).add(e_[1]); nbr.setdefault(e_[1], set()).add(e_[0])
    tight = 0
    for s_ in ('Left', 'Right'):
        axis = wrist[s_] - elbow[s_]; axis = axis / np.linalg.norm(axis)
        def near(v):
            d = (nv[v] - wrist[s_]) @ axis
            return -0.12 < d < 0.06 and np.linalg.norm((nv[v] - wrist[s_]) - axis * d) < 0.12
        ring1 = [v for v in rim_v if near(v)]
        ring2 = {u for v in ring1 for u in nbr.get(v, ()) if u not in rim_v and near(u)}
        for vs_, f in ((ring1, 0.80), (ring2, 0.92)):
            for v in vs_:
                q = wrist[s_] + axis * ((nv[v] - wrist[s_]) @ axis); nv[v] = q + (nv[v] - q) * f
        tight += len(ring1)
    print('CUFFS drawn in: %d rim vertices' % tight)
shards = 0
for poly in keep_polys:
    c = nv[list(poly.vertices)].mean(0)
    if heads and size[find(poly.vertices[0])] < 60 and c[up] > zneck - 0.12:
        d = c - neck_axis; d[up] = 0
        if np.linalg.norm(d) < 0.16: shards += 1; continue
    ids = [corner('n', nm.data.loops[li].vertex_index, nv[nm.data.loops[li].vertex_index], nn[nm.data.loops[li].vertex_index], tuple(nuv[li].uv), nw[nm.data.loops[li].vertex_index]) for li in poly.loop_indices]
    for k in range(1, len(ids) - 1): subs[0].append((ids[0], ids[k], ids[k + 1]))
    kept += 1
print('SHARDS dropped %d faces' % shards)
ouv = om.data.uv_layers.active.data
mats = [m.name for m in om.data.materials]
print('OLD MATERIALS', mats)
if own:
    # ---- the Rigify videos' step 3 (Parent > With Automatic Weights) on the body, then part 2's check (bend the
    # deform bones and look for skin that does not follow). An armature with this rig's bone names is put on the
    # body's own joints; the body's faces (split at UV seams) are welded by position for the weighting, and each
    # split vertex takes its welded vertex's weights.
    Minv = np.linalg.inv(M[:3])
    def to_blender(u): return (np.asarray(u) - M[3]) @ Minv
    used = sorted({i_ for t_ in subs[0] for i_ in t_})
    keyp = {}; wid = {}
    for i_ in used:
        k_ = tuple(np.round(to_blender(verts[i_][0]), 5))
        wid[i_] = keyp.setdefault(k_, len(keyp))
    wv = [None] * len(keyp)
    for k_, j_ in keyp.items(): wv[j_] = k_
    wf = list({tuple(sorted((wid[a_], wid[b_], wid[c_]))): (wid[a_], wid[b_], wid[c_]) for a_, b_, c_ in subs[0] if len({wid[a_], wid[b_], wid[c_]}) == 3}.values())
    bme = bpy.data.meshes.new('body_w'); bme.from_pydata(wv, [], wf); bme.update()
    bo = bpy.data.objects.new('body_w', bme); bpy.context.collection.objects.link(bo)
    ad_ = bpy.data.armatures.new('own'); ar_ = bpy.data.objects.new('own', ad_); bpy.context.collection.objects.link(ar_)
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = ar_; bpy.ops.object.mode_set(mode='EDIT')
    deform = [n for n in names if n in oj]
    kids = {}
    for b_ in oa.data.bones:
        if b_.parent and b_.name in oj and b_.parent.name in oj: kids.setdefault(b_.parent.name, []).append(b_.name)
    for n in deform:
        e_ = ad_.edit_bones.new(n); e_.head = oj[n].tolist()
        ch = [c for c in kids.get(n, []) if c in oj and np.linalg.norm(oj[c] - oj[n]) > 1e-4]
        if n.endswith('Hand'): ch = [P + n[len(P):-4] + 'HandMiddle1']                  # the hand points to the knuckles
        par_ = oa.data.bones[n].parent
        tail = np.mean([oj[c] for c in ch], 0) if ch else (oj[n] + (oj[n] - oj[par_.name]) * 0.5 if par_ else oj[n] + np.array([0, 0, 0.1]))
        if np.linalg.norm(tail - oj[n]) < 1e-3: tail = oj[n] + np.array([0, 0, 0.03])
        e_.tail = tail.tolist()
    for b_ in oa.data.bones:
        if b_.name in deform and b_.parent and b_.parent.name in deform: ad_.edit_bones[b_.name].parent = ad_.edit_bones[b_.parent.name]
    # (2026-11-18, the elbow-twist video) the forearm is cut into TWIST_SEGS bones (its "Subdivide, 6 cuts"), so the
    # automatic weights spread the forearm over seven bands; the six new ones become twist bones in the game
    # (children of the forearm, turned a share of the hand's roll each frame by ForearmTwist.cs)
    twist_names = []
    for s_ in ('Left', 'Right'):
        fa_ = ad_.edit_bones[P + s_ + 'ForeArm']; h0, h1 = np.array(fa_.head), np.array(oj[P + s_ + 'Hand'])
        fa_.tail = (h0 + (h1 - h0) / TWIST_SEGS).tolist(); prev = fa_
        for k_ in range(1, TWIST_SEGS):
            n_ = P + s_ + 'ForeArmTwist%d' % k_
            e_ = ad_.edit_bones.new(n_); e_.head = (h0 + (h1 - h0) * k_ / TWIST_SEGS).tolist(); e_.tail = (h0 + (h1 - h0) * (k_ + 1) / TWIST_SEGS).tolist()
            e_.parent = prev; e_.use_connect = True; prev = e_
            oj[n_] = np.array(e_.head); index[n_] = len(index); names.append(n_); twist_names.append(n_)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); bo.select_set(True); ar_.select_set(True); bpy.context.view_layer.objects.active = ar_
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    gname = {g_.index: g_.name for g_ in bo.vertex_groups}
    empty = 0
    for i_ in used:
        acc = {}
        for g_ in bme.vertices[wid[i_]].groups:
            if g_.weight > 0 and gname[g_.group] in index: acc[index[gname[g_.group]]] = g_.weight
        top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
        if not top: empty += 1; continue                                               # heat found nothing: keeps the generated weights
        tot = sum(w_ for _, w_ in top); top += [(0, 0.0)] * (4 - len(top))
        p_, n_, u_, _ = verts[i_]; verts[i_] = (p_, n_, u_, ([i__ for i__, _ in top], [w_ / tot for _, w_ in top]))
    print('AUTOMATIC WEIGHTS body: %d vertices (%d welded), %d left with the generated weights' % (len(used), len(wv), empty))
    # (2026-11-20) Some 370 vertices of each sleeve's end took part of their weight from the hand bone (112 of them
    # more than half), so a bent wrist dragged the cuff after the hand and it folded into points round the wrist
    # (seen at 40 cm in the kneeling aim). A sleeve does not bend with the hand: whatever the body's skin had on a
    # hand or finger bone goes to the forearm's last twist band, which turns with the hand's roll but not its bend.
    moved = 0
    for i_ in used:
        p_, n_, u_, wt = verts[i_]; ids_, ws_ = list(wt[0]), list(wt[1])
        for k_ in range(4):
            nm_ = names[ids_[k_]] if ids_[k_] < len(names) else ''
            for s_ in ('Left', 'Right'):
                if ws_[k_] > 0 and nm_.startswith(P + s_ + 'Hand'):
                    ids_[k_] = index[P + s_ + 'ForeArmTwist%d' % (TWIST_SEGS - 1)]; moved += 1
        acc = {}
        for j_, w_ in zip(ids_, ws_): acc[j_] = acc.get(j_, 0.0) + w_
        top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]; top += [(0, 0.0)] * (4 - len(top))
        verts[i_] = (p_, n_, u_, ([j_ for j_, _ in top], [w_ for _, w_ in top]))
    print('SLEEVES off the hands: %d weights moved to the last twist band' % moved)
    # (2026-11-20) Moved to the last band, a vertex that also kept weight on the forearm bone itself turned half by
    # 0 and half by the whole roll and the cuff closed into a star with the hand turned 140 degrees. As in the
    # video's twist rig, the forearm's share of each vertex now goes only to the two bands either side of where it
    # lies along the forearm (the bands' middles at (k + 1/2)/7 of the way), whatever the heat gave it.
    banded = 0
    for s_ in ('Left', 'Right'):
        bands = [index[P + s_ + 'ForeArm']] + [index[P + s_ + 'ForeArmTwist%d' % k_] for k_ in range(1, TWIST_SEGS)]
        bandset = set(bands)
        e0, e1 = oj[P + s_ + 'ForeArm'], oj[P + s_ + 'Hand']; L_ = np.linalg.norm(e1 - e0); dir_ = (e1 - e0) / L_
        for i_ in used:
            p_, n_, u_, wt = verts[i_]
            wf = sum(w_ for j_, w_ in zip(wt[0], wt[1]) if j_ in bandset)
            if wf < 1e-4: continue
            t_ = float((to_blender(p_) - e0) @ dir_ / L_)
            b_ = min(max(t_ * TWIST_SEGS - 0.5, 0.0), TWIST_SEGS - 1.0); k0 = int(b_); k1 = min(k0 + 1, TWIST_SEGS - 1); f_ = b_ - k0
            acc = {}
            for j_, w_ in zip(wt[0], wt[1]):
                if j_ not in bandset and w_ > 0: acc[j_] = acc.get(j_, 0.0) + w_
            acc[bands[k0]] = acc.get(bands[k0], 0.0) + wf * (1 - f_)
            if f_ > 0: acc[bands[k1]] = acc.get(bands[k1], 0.0) + wf * f_
            top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]; tot = sum(w_ for _, w_ in top); top += [(0, 0.0)] * (4 - len(top))
            verts[i_] = (p_, n_, u_, ([j_ for j_, _ in top], [w_ / tot for _, w_ in top])); banded += 1
    print('FOREARM bands: %d vertices put on their two nearest bands' % banded)
    if 'bend-check' in extra:
        # part 2's check: head back, arms down and bent, a knee bent; renders front and side
        bpy.ops.object.mode_set(mode='OBJECT'); bpy.context.view_layer.objects.active = ar_; bpy.ops.object.mode_set(mode='POSE')
        def turn(n, world_axis, deg):   # about a world axis (0 side, 1 up, 2 depth), whatever the bone's own roll
            wa = np.zeros(3); wa[(ax, up, depth)[world_axis]] = 1.0
            la = ad_.bones[n].matrix_local.to_3x3().inverted() @ Vector(wa.tolist())
            pb = ar_.pose.bones[n]; pb.rotation_mode = 'AXIS_ANGLE'; pb.rotation_axis_angle = (math.radians(deg), *la)
        turn(P + 'Head', 0, 35 * facing); turn(P + 'Neck', 0, 15 * facing)
        for s_ in ('Left', 'Right'):
            sg = 1 if oj[P + s_ + 'Hand'][ax] > 0 else -1
            turn(P + s_ + 'Arm', 2, -55 * sg); turn(P + s_ + 'ForeArm', 1, 80 * sg * facing)
        turn(P + 'LeftUpLeg', 0, -70 * facing); turn(P + 'LeftLeg', 0, 100 * facing)
        bpy.ops.object.mode_set(mode='OBJECT')
        sc_ = bpy.context.scene; sc_.render.engine = 'BLENDER_WORKBENCH'; sc_.render.resolution_x = 700; sc_.render.resolution_y = 900
        sh_ = sc_.display.shading; sh_.light = 'STUDIO'; sh_.color_type = 'SINGLE'; sh_.single_color = (0.62, 0.6, 0.45); sh_.show_cavity = True
        for o_ in bpy.data.objects:
            if o_.type == 'MESH' and o_ is not bo: o_.hide_render = True
        cam_ = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam_); sc_.camera = cam_
        cam_.data.type = 'ORTHO'; cam_.data.ortho_scale = 2.0
        ctr = np.array(wv).mean(0)
        for nm_, d_ in (('front', -1), ('side', 1)):
            dv = np.zeros(3); dv[depth if nm_ == 'front' else ax] = d_ * 3 * (facing if nm_ == 'front' else 1)
            cam_.location = (ctr + dv).tolist(); cam_.rotation_euler = (Vector(ctr.tolist()) - cam_.location).to_track_quat('-Z', 'Y').to_euler()
            sc_.render.filepath = out + '_bend_%s.png' % nm_; bpy.ops.render.render(write_still=True)
if own and 'twist-check' in extra:
    # the elbow-twist video's check: the right hand turned 90 degrees about the forearm, first on the hand bone alone
    # (as before the twist bones), then shared along the forearm's seven bands (each 1/7, chained as in the video)
    bpy.ops.object.mode_set(mode='OBJECT'); bpy.context.view_layer.objects.active = ar_; bpy.ops.object.mode_set(mode='POSE')
    for pb in ar_.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0)
    def roll(n, deg):   # about the bone's own length (its local Y), like the video's R, Y, Y
        pb = ar_.pose.bones[n]; pb.rotation_mode = 'AXIS_ANGLE'; pb.rotation_axis_angle = (math.radians(deg), 0, 1, 0)
    sc_ = bpy.context.scene; sc_.render.engine = 'BLENDER_WORKBENCH'; sc_.render.resolution_x = sc_.render.resolution_y = 700
    sh_ = sc_.display.shading; sh_.light = 'STUDIO'; sh_.color_type = 'SINGLE'; sh_.single_color = (0.62, 0.6, 0.45); sh_.show_cavity = True
    for o_ in bpy.data.objects:
        if o_.type == 'MESH' and o_ is not bo: o_.hide_render = True
    cam_ = bpy.data.objects.new('cam_t', bpy.data.cameras.new('cam_t')); bpy.context.collection.objects.link(cam_); sc_.camera = cam_
    cam_.data.type = 'ORTHO'; cam_.data.ortho_scale = 0.45
    c_ = (oj[P + 'RightForeArm'] + oj[P + 'RightHand']) / 2; dv = np.zeros(3); dv[up] = 2.0; dv[depth] = -0.6 * facing
    cam_.location = (c_ + dv).tolist(); cam_.rotation_euler = (Vector(c_.tolist()) - cam_.location).to_track_quat('-Z', 'Y').to_euler()
    for tag_, shared in (('hand_only', False), ('shared', True)):
        for pb in ar_.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0)
        if shared:
            roll(P + 'RightForeArm', 90.0 / TWIST_SEGS)
            for k_ in range(1, TWIST_SEGS): roll(P + 'RightForeArmTwist%d' % k_, 90.0 / TWIST_SEGS)
        else:
            roll(P + 'RightForeArmTwist%d' % (TWIST_SEGS - 1), 90.0)   # the whole turn at the wrist end
        sc_.render.filepath = out + '_twist_%s.png' % tag_; bpy.ops.render.render(write_still=True)
hands = 0
if hands_bin:
    # (2026-11-18) the new hands become part of this one mesh (submesh 1, both hands) instead of a renderer of their
    # own: they are already in this mesh's space with this rig's bone indices. Their triangles are already wound
    # for Unity, so they are stored the other way round where the writer below flips.
    subs = [subs[0], []]
    with open(hands_bin, 'rb') as f:
        nh, _ = struct.unpack('<ii', f.read(8)); base = len(verts); rec = struct.calcsize('<8f4i4f')
        # (2026-11-20) The hands' wrists are shared between the hand and the forearm bone, which never turns with
        # the hand's roll: turned 140 degrees, a wrist half on each closed in like a sweet wrapper inside the cuff.
        # Their forearm share goes to the forearm's last twist band, which turns with the hand.
        to_band = {}
        if own:
            for s_ in ('Left', 'Right'):
                last_ = P + s_ + 'ForeArmTwist%d' % (TWIST_SEGS - 1)
                if last_ in index: to_band[index[P + s_ + 'ForeArm']] = index[last_]
        for _ in range(nh):
            r_ = struct.unpack('<8f4i4f', f.read(rec))
            verts.append((np.array(r_[0:3]), np.array(r_[3:6]), (r_[6], r_[7]), ([to_band.get(j_, j_) for j_ in r_[8:12]], list(r_[12:16]))))
        nt_, = struct.unpack('<i', f.read(4)); idx = struct.unpack('<%di' % nt_, f.read(4 * nt_))
        for i_ in range(0, nt_, 3):
            a, b_, c = (base + x for x in idx[i_:i_ + 3])
            subs[1].append((a, c, b_) if mirrored else (a, b_, c))
            hands += 1
else:
    for poly in om.data.polygons:
        mn = mats[poly.material_index]
        if 'Hand' not in mn: continue
        sub = 1 if 'Left' in mn else 2
        ids = [corner('o', om.data.loops[li].vertex_index, ov[om.data.loops[li].vertex_index], on[om.data.loops[li].vertex_index], tuple(ouv[li].uv), ow[om.data.loops[li].vertex_index]) for li in poly.loop_indices]
        for k in range(1, len(ids) - 1): subs[sub].append((ids[0], ids[k], ids[k + 1]))
        hands += 1
print('FACES new kept %d, hands cut %d, neck skin cut %d, hand faces %d (%s), vertices %d' % (kept, cut, neckcut, hands, 'new hands' if hands_bin else 'old hands', len(verts)))

if own:
    # every bone's new place in the mesh's own space, for apply_hybrid.cs; the fingers from the hands' own joints
    fitb = {n: to_unity(oj[n]).tolist() for n in names if n in oj}
    if hands_bin and os.path.exists(hands_bin + '_bones.json'): fitb.update(json.load(open(hands_bin + '_bones.json')))
    json.dump(fitb, open(out + '_bones.json', 'w'))
with open(out, 'wb') as f:
    f.write(struct.pack('<ii', len(verts), len(subs)))
    for pos, nrm, uv, (bi, bw) in verts:
        n_ = nrm / max(np.linalg.norm(nrm), 1e-9)
        f.write(struct.pack('<8f4i4f', *pos, *n_, uv[0], uv[1], *bi, *bw))
    for s in subs:
        f.write(struct.pack('<i', len(s) * 3))
        for a, b, c in s:
            f.write(struct.pack('<3i', *((a, c, b) if mirrored else (a, b, c))))
for hi_, (hverts, htris) in enumerate(head_out):
    with open('%s.head%d.bin' % (out, hi_), 'wb') as f:
        f.write(struct.pack('<ii', len(hverts), 1))
        for pos, nrm, uv, (bi, bw) in hverts:
            n_ = nrm / max(np.linalg.norm(nrm), 1e-9)
            f.write(struct.pack('<8f4i4f', *pos, *n_, uv[0], uv[1], *bi, *bw))
        f.write(struct.pack('<i', len(htris) * 3))
        for a, b, c in htris:
            f.write(struct.pack('<3i', *((a, c, b) if mirrored else (a, b, c))))
print('WROTE', out)
