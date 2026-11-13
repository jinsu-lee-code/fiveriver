# Blender (5.2) background script for 박용팔's skin (2026-11-10).
# Input (dumped from Unity, see README.md): mesh.bin (vertices, normals, weights, sub-meshes, bindposes by Unity
# vertex index), bones.txt, pose_idle/fire/carry.bin (skin matrices of the live poses).
# 1. smooths the shirt (sub-mesh 0) normals: the delivery splits every face (13.7k tris on 36k vertices,
#    normals 18.7 deg apart per position), so the shirt reads as crushed facets once the body bends;
# 2. smooths shoulder/upper-back/arm weights (UV-seam duplicates welded) so the shoulder turn spreads wider;
# 3. renders before/after in the dumped poses and writes weights_fixed.bin + normals_fixed.bin for the Unity
#    menu FiveRivers/Characters/Apply Park Skin Fix.
# usage: blender -b --python fix_park_skin.py -- <data_dir> [iterations=100] [strength=0.5]
import bpy, sys, os, struct, math
import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:]
D = argv[0]
ITER = int(argv[1]) if len(argv) > 1 else 100
LAM = float(argv[2]) if len(argv) > 2 else 0.5

# ---------- load the Unity mesh ----------
raw = open(os.path.join(D, 'mesh.bin'), 'rb').read()
off = 0
def take(fmt, n):
    global off
    size = np.dtype(fmt).itemsize * n
    arr = np.frombuffer(raw, dtype=np.dtype(fmt), count=n, offset=off)
    off += size
    return arr
nv = int(take('<i4', 1)[0])
V = take('<f4', nv * 3).reshape(nv, 3).astype(np.float64)
N = take('<f4', nv * 3).reshape(nv, 3)
bw = np.frombuffer(raw, dtype=np.dtype([('i', '<i4', 4), ('w', '<f4', 4)]), count=nv, offset=off).copy(); off += nv * 32
BI = bw['i'].astype(np.int64); BW = bw['w'].astype(np.float64)
nsub = int(take('<i4', 1)[0])
subs = []
for s in range(nsub):
    n = int(take('<i4', 1)[0]); subs.append(take('<i4', n).astype(np.int64).reshape(-1, 3))
nb = int(take('<i4', 1)[0])
BP = take('<f4', nb * 16).reshape(nb, 4, 4).astype(np.float64)
bones = [l.strip() for l in open(os.path.join(D, 'bones.txt'), encoding='utf-8') if l.strip()]
assert len(bones) == nb, (len(bones), nb)
B = {n.replace('mixamorig:', ''): i for i, n in enumerate(bones)}

def load_pose(name):
    r = open(os.path.join(D, name + '.bin'), 'rb').read()
    n = struct.unpack_from('<i', r, 0)[0]
    return np.frombuffer(r, dtype='<f4', count=n * 16, offset=4).reshape(n, 4, 4).astype(np.float64)
poses = {p: load_pose('pose_' + p) for p in ('idle', 'fire', 'carry')}

def dense(BI, BW):
    W = np.zeros((nv, nb)); np.add.at(W, (np.repeat(np.arange(nv), 4), BI.ravel()), BW.ravel()); return W

def skin(W, M):
    Vh = np.c_[V, np.ones(nv)]
    out = np.zeros((nv, 3))
    for b in range(nb):
        w = W[:, b]
        idx = np.nonzero(w > 0)[0]
        if len(idx) == 0: continue
        out[idx] += w[idx, None] * (Vh[idx] @ M[b].T)[:, :3]
    return out

# ---------- region: shoulders, upper back, upper arms and elbows ----------
W0 = dense(BI, BW)
arm_names = ['LeftShoulder', 'LeftArm', 'LeftForeArm', 'RightShoulder', 'RightArm', 'RightForeArm', 'Spine2']
hand_names = [n for n in B if 'Hand' in n]
head_names = ['Neck', 'Head']
arm_w = W0[:, [B[n] for n in arm_names]].sum(1)
hand_w = W0[:, [B[n] for n in hand_names]].sum(1)
head_w = W0[:, [B[n] for n in head_names]].sum(1)
region = (arm_w > 0.05) & (hand_w < 0.02) & (head_w < 0.3)
smooth_bones = [B[n] for n in ['Spine1', 'Spine2', 'Neck', 'LeftShoulder', 'LeftArm', 'LeftForeArm',
                               'RightShoulder', 'RightArm', 'RightForeArm']]

# weld seam duplicates by position so both sides of a UV seam keep identical weights
key = np.round(V * 1e5).astype(np.int64)
_, weld = np.unique(key, axis=0, return_inverse=True)
weld = weld.ravel(); nw = weld.max() + 1
tris = np.concatenate(subs)
e = np.concatenate([tris[:, [0, 1]], tris[:, [1, 2]], tris[:, [2, 0]]])
e = weld[e]; e = e[e[:, 0] != e[:, 1]]
e = np.unique(np.sort(e, 1), axis=0)

def smooth(W):
    Ww = np.zeros((nw, nb)); cnt = np.bincount(weld, minlength=nw).astype(np.float64)
    np.add.at(Ww, weld, W); Ww /= cnt[:, None]
    reg_w = np.zeros(nw, bool); reg_w[weld[region]] = True
    deg = np.bincount(e.ravel(), minlength=nw).astype(np.float64)
    S = np.array(smooth_bones)
    for _ in range(ITER):
        acc = np.zeros((nw, len(S)))
        np.add.at(acc, e[:, 0], Ww[e[:, 1]][:, S]); np.add.at(acc, e[:, 1], Ww[e[:, 0]][:, S])
        avg = acc / np.maximum(deg, 1)[:, None]
        upd = reg_w & (deg > 0)
        # only the shoulder/arm/back share moves; other bones (legs, hands) keep their weight
        tot_before = Ww[upd][:, S].sum(1, keepdims=True)
        new = (1 - LAM) * Ww[upd][:, S] + LAM * avg[upd]
        tot_new = new.sum(1, keepdims=True)
        Ww[np.ix_(upd, S)] = new * np.where(tot_new > 0, tot_before / np.maximum(tot_new, 1e-12), 0)
    Wn = Ww[weld]
    Wn[~region] = W[~region]
    # back to Unity's four influences
    top = np.argsort(-Wn, 1)[:, :4]
    tw = np.take_along_axis(Wn, top, 1); tw /= np.maximum(tw.sum(1, keepdims=True), 1e-12)
    return top, tw

def collapse_stats(P, label):
    a = P[tris[:, 1]] - P[tris[:, 0]]; b = P[tris[:, 2]] - P[tris[:, 0]]
    area = np.linalg.norm(np.cross(a, b), axis=1)
    a0 = V[tris[:, 1]] - V[tris[:, 0]]; b0 = V[tris[:, 2]] - V[tris[:, 0]]
    area0 = np.linalg.norm(np.cross(a0, b0), axis=1)
    rt = region[tris].all(1) & (area0 > 0)
    r = area[rt] / area0[rt]
    return f"{label}: crushed(<0.5)={int((r<0.5).sum())} severe(<0.25)={int((r<0.25).sum())} stretched(>2)={int((r>2).sum())} of {int(rt.sum())}"

def anchor_clavicle(indices, weights):
    # 이 메시의 목뼈 기준 바인드 좌표. 앞쪽은 -Y, 위쪽은 +Z다.
    chest, neck, head = (B[name] for name in ("Spine2", "Neck", "Head"))
    neck_origin = np.linalg.inv(BP.astype(np.float32))[neck, :3, 3]
    mm = (V - neck_origin) * 160_000
    weights = weights.astype(np.float32)

    def fade(value, start, end):
        t = np.clip((value - start) / (end - start), 0., 1.)
        return t * t * (3. - 2. * t)

    skin = np.unique(subs[2])
    lower = skin[mm[skin, 2] < 159.]
    assert np.allclose(np.where(indices[lower] == head, weights[lower], 0.).sum(1), 1.)
    assert all(not np.intersect1d(lower, subs[slot]).size for slot in (0, 1, 3, 4))
    height = mm[lower, 2]
    neck_motion = fade(height, 82., 142.)
    head_motion = fade(height, 115., 159.)
    rows = np.c_[1. - neck_motion, neck_motion * (1. - head_motion),
                 neck_motion * head_motion, np.zeros(len(lower))]
    order = np.argsort(-rows, axis=1, kind='stable')
    weights[lower] = np.take_along_axis(rows, order, 1)
    indices[lower] = np.where(weights[lower] > 0., np.array([chest, neck, head, 0])[order], 0)

    # 옷깃 아래는 쇄골처럼 가슴을 따르고, 위로 갈수록 목을 따라 완만하게 굽힌다.
    # 머리뼈의 큰 회전이 옷깃을 당기면 목 밑동과 옷깃 사이에 꺾임이 생긴다.
    cloth = np.unique(subs[0])
    x, y, z = mm[cloth].T
    transfer = ((1. - fade(abs(x), 65., 171.)) * fade(-y, -58., 7.)
                * fade(z, -75., 22.) * (1. - fade(z, 167., 213.)))
    collar = cloth[transfer > .0001]
    assert all(not np.intersect1d(collar, subs[slot]).size for slot in (1, 2, 3, 4))
    rows = np.zeros((len(collar), nb), dtype=np.float32)
    for k in range(4):
        rows[np.arange(len(collar)), indices[collar, k]] += weights[collar, k]
    moved = rows[:, [neck, head]] * transfer[transfer > .0001, None]
    rows[:, [neck, head]] -= moved
    moved_total = moved.sum(1)
    neck_share = fade(mm[collar, 2], 40., 154.)
    rows[:, neck] += moved_total * neck_share
    rows[:, chest] += moved_total * (1. - neck_share)
    can_transfer = (moved_total > .0001) & (np.count_nonzero(rows > 0., axis=1) <= 4)
    collar, rows = collar[can_transfer], rows[can_transfer]
    order = np.argsort(-rows, axis=1, kind='stable')[:, :4]
    collar_weights = np.take_along_axis(rows, order, 1)
    weights[collar] = collar_weights / collar_weights.sum(1, keepdims=True)
    indices[collar] = np.where(weights[collar] > 0., order, 0)
    assert np.allclose(weights[np.r_[lower, collar]].sum(1), 1.)
    return indices, weights


top, tw = anchor_clavicle(*smooth(W0))
W1 = dense(top, tw)
report = [f"region vertices={int(region.sum())} iter={ITER} lam={LAM}"]
posed = {}
for p, M in poses.items():
    P0 = skin(W0, M); P1 = skin(W1, M); posed[p] = (P0, P1)
    report.append(collapse_stats(P0, p + ' before')); report.append(collapse_stats(P1, p + ' after '))
moved = np.abs(W1 - W0).sum(1)
report.append(f"vertices whose weights changed (>0.01): {int((moved>0.01).sum())}")
print('\n'.join(report))
open(os.path.join(D, 'report.txt'), 'w', encoding='utf-8').write('\n'.join(report))

# ---------- write corrected weights (Unity bone indices) ----------
with open(os.path.join(D, 'weights_fixed.bin'), 'wb') as f:
    f.write(struct.pack('<i', nv))
    f.write(top.astype('<i4').tobytes()); f.write(tw.astype('<f4').tobytes())

# ---------- smooth the shirt normals (sub-mesh 0 only, area-weighted per welded position) ----------
shirt = subs[0]
fn = np.cross(V[shirt[:, 1]] - V[shirt[:, 0]], V[shirt[:, 2]] - V[shirt[:, 0]])  # length = 2 x area
acc = np.zeros((nw, 3))
for k in range(3): np.add.at(acc, weld[shirt[:, k]], fn)
shirt_verts = np.unique(shirt)
N1 = N.astype(np.float64).copy()
sm = acc[weld[shirt_verts]]
ln = np.linalg.norm(sm, axis=1, keepdims=True)
ok = ln[:, 0] > 0
N1[shirt_verts[ok]] = sm[ok] / ln[ok]
dev_before = np.degrees(np.arccos(np.clip((N[shirt_verts] * N1[shirt_verts]).sum(1), -1, 1)))
report.append(f"shirt normals smoothed: {int(ok.sum())} vertices, mean change {dev_before.mean():.1f} deg")
print(report[-1])
open(os.path.join(D, 'report.txt'), 'w', encoding='utf-8').write(chr(10).join(report))
with open(os.path.join(D, 'normals_fixed.bin'), 'wb') as f:
    f.write(struct.pack('<i', nv)); f.write(N1.astype('<f4').tobytes())

def skin_normals(W, M, Nrm):
    out = np.zeros((nv, 3))
    for b in range(nb):
        w = W[:, b]; idx = np.nonzero(w > 0)[0]
        if len(idx) == 0: continue
        out[idx] += w[idx, None] * (Nrm[idx] @ M[b][:3, :3].T)
    return out / np.maximum(np.linalg.norm(out, axis=1, keepdims=True), 1e-20)
posed_n = {p: (skin_normals(W0, M, N), skin_normals(W1, M, N1)) for p, M in poses.items()}

# ---------- render before/after in Blender ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 700; sc.render.resolution_y = 700
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.62, 0.6, 0.52)
sc.display.shading.show_cavity = True
def to_blender(P): return P * 160.0  # mesh space is already z-up; scale to roughly 1.75 m
faces = np.concatenate([subs[0], subs[4]])  # body cloth + arm skin
def make(P, name, Nrm):
    me = bpy.data.meshes.new(name); me.from_pydata(to_blender(P).tolist(), [], faces.tolist()); me.update()
    for poly in me.polygons: poly.use_smooth = True
    me.normals_split_custom_set_from_vertices(Nrm.tolist())
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); return ob
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 70
import mathutils
for p in ('fire', 'carry'):
    for tag, P, Nrm in zip(('before', 'after'), posed[p], posed_n[p]):
        ob = make(P, f'{p}_{tag}', Nrm)
        # right shoulder pivot in posed space
        height = float(np.ptp(P[:, 2])) * 160.0
        rs = np.linalg.inv(BP[B['RightArm']]); rs = poses[p][B['RightArm']] @ rs
        piv = to_blender(rs[:3, 3][None])[0]
        for vi, (dx, dy, dz) in enumerate([(0.0, -1.0, 0.25), (1.0, 0.2, 0.3), (0.0, 1.0, 0.35)]):
            d = mathutils.Vector((dx, dy, dz)).normalized()
            target = mathutils.Vector(piv)
            cam.location = target + d * height * 0.55
            cam.rotation_euler = (-d).to_track_quat('-Z', 'Z').to_euler()
            sc.render.filepath = os.path.join(D, f'r_{p}_{tag}_{vi}.png')
            bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(ob, do_unlink=True)
print('DONE')
