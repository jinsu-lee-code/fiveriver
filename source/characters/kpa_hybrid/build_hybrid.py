# KPA rifleman: the generated uniform and head on the game's own skeleton and hands (2026-11-15).
#
# The delivered enemy has a rough, lumpy uniform and a face with baked black streaks; a character generated in
# VARCO from a T-pose picture has a clean uniform but mitten hands. The generated body is fitted onto the old
# skeleton (so the avatar, clips, IK, both prefabs and the tests stay as they are), its hands are cut off at the
# wrists and the old hands stay.
#
#   blender -b --factory-startup -P build_hybrid.py -- <old SK_CH_KPA_Rifleman.fbx> <new rigged.fbx> <unity_bones.txt> <out.bin>
# unity_bones.txt: the old mesh's bones in the order of its SkinnedMeshRenderer, with their bind positions in
# the old mesh's own space (bindpose^-1 * 0). The output is in that space, so the old bind poses are reused.
import bpy, os, json, sys, struct, math
import numpy as np
from mathutils import Vector

old_fbx, new_fbx, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
heads = sys.argv[sys.argv.index('--') + 5:]      # optional: head busts (.glb); each is written next to 'out' as <out>.head<N>.bin
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
# the old rig's arms are short for its height: beyond each shoulder the arm is fitted on its own, joint to joint
# (shoulder -> elbow -> wrist), along the axis the arms lie on
ax = int(np.argmax(np.abs(oj[P + 'LeftHand'] - oj[P + 'RightHand'])))
up = int(np.argmax(np.abs(oj[P + 'Head'] - oj[P + 'Hips'])))
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
hands = 0
for poly in om.data.polygons:
    mn = mats[poly.material_index]
    if 'Hand' not in mn: continue
    sub = 1 if 'Left' in mn else 2
    ids = [corner('o', om.data.loops[li].vertex_index, ov[om.data.loops[li].vertex_index], on[om.data.loops[li].vertex_index], tuple(ouv[li].uv), ow[om.data.loops[li].vertex_index]) for li in poly.loop_indices]
    for k in range(1, len(ids) - 1): subs[sub].append((ids[0], ids[k], ids[k + 1]))
    hands += 1
print('FACES new kept %d, hands cut %d, neck skin cut %d, old hand faces %d, vertices %d' % (kept, cut, neckcut, hands, len(verts)))

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
