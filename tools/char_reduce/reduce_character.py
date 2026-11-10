# 캐릭터 삼각형 줄이기 (2026-11-10, 사용자 "삼각형 너무 많은건 blender로 줄여줘" · "이번 아트 요청서에 있던거만 줄여줘")
# ART_REQUEST_20261110 R1 일반 아군(ROK_Regular_NoCap_SkinFix) · R3 박용팔(ROK_ParkYongpal_SkinFix) → ART_SPEC 캐릭터 예산 1~2만.
#
# Input src.bin is the Unity mesh dumped vertex for vertex (format in README.md). Steps, per sub-mesh:
#   1. weld corners that share a position (relative 1e-6), so collapse can work across the split faces; UVs stay on the
#      face corners, so UV seams are kept as seams (Blender's collapse interpolates each side of a seam separately)
#   2. collapse-decimate to the sub-mesh's share of the target; bone weights ride along as vertex groups
#   3. normals are not recomputed: every new face corner takes the delivered (already fixed) normal of the nearest
#      original face, barycentric-interpolated, so the SkinFix shading and the hard edges stay as they were
#   4. bone weights: top 4, normalised
# Checks written to report.txt: triangles, surface distance original→new, UV corners outside the original UV
# coverage, UV faces flipped against the original. Renders before/after (front / back / side, rest pose).
# usage: blender -b --factory-startup --python reduce_character.py -- <dir with src.bin> <target triangles>
import bpy, bmesh, sys, os, struct, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index('--') + 1:]
D, TARGET = a[0], int(a[1])


# ---------- read ----------
raw = open(os.path.join(D, 'src.bin'), 'rb').read(); off = 0
def take(dtype, count):
    global off
    arr = np.frombuffer(raw, dtype, count, off); off += arr.nbytes; return arr
nv = int(take('<i4', 1)[0])
P = take('<f4', nv * 3).reshape(nv, 3).astype(np.float64)
N = take('<f4', nv * 3).reshape(nv, 3).astype(np.float64)
take('<f4', nv * 4)  # tangents: recomputed in Unity
bw = np.frombuffer(raw, np.dtype([('i', '<i4', 4), ('w', '<f4', 4)]), nv, off); off += nv * 32
BI = bw['i'].copy(); BW = bw['w'].astype(np.float64)
nch = int(take('<i4', 1)[0]); UVS = {}
for c in range(nch):
    if int(take('<i4', 1)[0]): UVS[c] = take('<f4', nv * 2).reshape(nv, 2).astype(np.float64)
assert list(UVS) == [0], 'only UV0 is handled'
UV = UVS[0]
if int(take('<i4', 1)[0]): raise SystemExit('vertex colours are not handled')
nsub = int(take('<i4', 1)[0]); SUB = []
for i in range(nsub):
    n = int(take('<i4', 1)[0]); SUB.append(take('<i4', n).reshape(-1, 3).copy())
nbp = int(take('<i4', 1)[0]); BIND = take('<f4', nbp * 16).copy()
nbones = int(BI.max()) + 1

size = float(np.max(P.max(0) - P.min(0)))
S = 1.0 / size  # work at unit size; the dumped mesh is about 0.01 across
orig_tris = [len(t) for t in SUB]; total = sum(orig_tris)
share = TARGET / total
log = [f'source {D}: {nv} vertices, triangles {orig_tris} = {total}, target {TARGET} (ratio {share:.3f})']


def unit(v):
    return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-20)


# ---------- per sub-mesh ----------
out_P, out_N, out_UV, out_BI, out_BW, out_SUB = [], [], [], [], [], []
base = 0
render_objs = {'before': [], 'after': []}

for si, tri in enumerate(SUB):
    if len(tri) == 0:
        out_SUB.append(np.zeros((0, 3), np.int64)); log.append(f'sub {si}: empty'); continue
    used = np.unique(tri)
    # 1. weld by position
    key = np.round(P[used] * S / 1e-6).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True); inv = inv.reshape(-1)
    weld = np.full(nv, -1, np.int64); weld[used] = inv
    nw = int(inv.max()) + 1
    WP = np.zeros((nw, 3)); WP[inv] = P[used] * S
    first = np.full(nw, -1, np.int64); first[inv[::-1]] = used[::-1]  # one original vertex per welded vertex
    faces = weld[tri]
    ok = (faces[:, 0] != faces[:, 1]) & (faces[:, 1] != faces[:, 2]) & (faces[:, 0] != faces[:, 2])
    ftri = tri[ok]; faces = faces[ok]
    # drop exact duplicate faces (same three welded corners), Blender would merge them
    srt = np.sort(faces, 1); _, keep = np.unique(srt, axis=0, return_index=True); keep = np.sort(keep)
    ftri = ftri[keep]; faces = faces[keep]

    me = bpy.data.meshes.new(f'sub{si}')
    me.vertices.add(nw); me.vertices.foreach_set('co', WP.astype(np.float32).ravel())
    me.loops.add(len(faces) * 3); me.loops.foreach_set('vertex_index', faces.astype(np.int32).ravel())
    me.polygons.add(len(faces))
    me.polygons.foreach_set('loop_start', (np.arange(len(faces)) * 3).astype(np.int32))
    uvl = me.uv_layers.new(name='UV0')
    uvl.data.foreach_set('uv', UV[ftri].astype(np.float32).ravel())
    me.update()
    if me.validate(): raise SystemExit(f'sub {si}: mesh needed repairs, face order no longer matches')
    ob = bpy.data.objects.new(f'sub{si}', me); bpy.context.scene.collection.objects.link(ob)
    # bone weights as vertex groups (welded vertex takes the weights of one of its originals; SkinFix kept seams equal)
    groups = [ob.vertex_groups.new(name=f'b{b}') for b in range(nbones)]
    for wv in range(nw):
        ov = first[wv]
        for k in range(4):
            w = BW[ov, k]
            if w > 0: groups[BI[ov, k]].add([wv], float(w), 'REPLACE')
    before = len(me.polygons)

    # 2. decimate
    want = max(int(round(orig_tris[si] * share)), 60)
    mod = ob.modifiers.new('dec', 'DECIMATE'); mod.decimate_type = 'COLLAPSE'
    mod.ratio = min(1.0, want / max(before, 1)); mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    low = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ob.modifiers.remove(mod)
    lob = bpy.data.objects.new(f'low{si}', low); bpy.context.scene.collection.objects.link(lob)
    for g in ob.vertex_groups: lob.vertex_groups.new(name=g.name)
    bm = bmesh.new(); bm.from_mesh(low); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(low); bm.free()

    nl = len(low.vertices)
    LP = np.zeros(nl * 3, np.float32); low.vertices.foreach_get('co', LP); LP = LP.reshape(nl, 3).astype(np.float64)
    nf = len(low.polygons)
    LV = np.zeros(nf * 3, np.int32); low.polygons.foreach_get('vertices', LV); LV = LV.reshape(nf, 3)
    LUV = np.zeros(nf * 6, np.float32); low.uv_layers[0].data.foreach_get('uv', LUV); LUV = LUV.reshape(nf, 3, 2).astype(np.float64)
    # weights: top 4 per vertex, normalised
    wi = np.zeros((nl, 4), np.int64); ww = np.zeros((nl, 4))
    for v in low.vertices:
        gs = sorted(((g.weight, g.group) for g in v.groups if g.weight > 0), reverse=True)[:4]
        for k, (w, gidx) in enumerate(gs): wi[v.index, k] = int(lob.vertex_groups[gidx].name[1:]); ww[v.index, k] = w
    sw = ww.sum(1, keepdims=True); lost = int((sw[:, 0] <= 0).sum())
    ww = np.where(sw > 0, ww / np.maximum(sw, 1e-20), ww)

    # 3. normals and checks from the original faces of this sub-mesh
    O = P[tri] * S                                  # original corner positions (k, 3, 3)
    ov = O.reshape(-1, 3); of = np.arange(len(tri) * 3).reshape(-1, 3)
    bvh = BVHTree.FromPolygons([Vector(p) for p in ov], of.tolist(), epsilon=0.0)
    cen = LP[LV].mean(1)
    corner_n = np.zeros((nf, 3, 3)); src_face = np.zeros((nf, 3), np.int64)
    for f in range(nf):
        for c in range(3):
            q = LP[LV[f, c]] + 0.02 * (cen[f] - LP[LV[f, c]])
            loc, _, idx, _ = bvh.find_nearest(Vector(q))
            A, B, C = O[idx]; pl = np.array(loc)
            v0, v1, v2 = B - A, C - A, pl - A
            d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
            den = d00 * d11 - d01 * d01
            if abs(den) < 1e-30: bb = np.array([1 / 3, 1 / 3, 1 / 3])
            else:
                y = (d11 * d20 - d01 * d21) / den; z = (d00 * d21 - d01 * d20) / den; bb = np.clip([1 - y - z, y, z], 0, 1)
            corner_n[f, c] = bb @ N[tri[idx]]
            src_face[f, c] = idx
    corner_n = unit(corner_n)

    # check: distance from original vertices to the new surface (in fractions of the whole mesh size)
    lbvh = BVHTree.FromPolygons([Vector(p) for p in LP], LV.tolist(), epsilon=0.0)
    ou = np.unique(tri); dist = np.array([lbvh.find_nearest(Vector(P[v] * S))[3] for v in ou])
    # check: UV corners outside the original UV coverage, and UV faces flipped against their source face
    R = 1024
    mask = np.zeros((R, R), bool)
    for t in UV[tri] * R:
        x0, y0 = np.maximum(np.floor(t.min(0)).astype(int), 0); x1, y1 = np.minimum(np.ceil(t.max(0)).astype(int), R - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a_, b_, c_ = t; den = (b_[1] - c_[1]) * (a_[0] - c_[0]) + (c_[0] - b_[0]) * (a_[1] - c_[1])
        if abs(den) < 1e-12: continue
        w0 = ((b_[1] - c_[1]) * (xs - c_[0]) + (c_[0] - b_[0]) * (ys - c_[1])) / den
        w1 = ((c_[1] - a_[1]) * (xs - c_[0]) + (a_[0] - c_[0]) * (ys - c_[1])) / den
        ins = (w0 >= -0.6) & (w1 >= -0.6) & (1 - w0 - w1 >= -0.6)   # a little margin: 1 texel at 1K
        mask[(ys[ins] - 0.5).astype(int), (xs[ins] - 0.5).astype(int)] = True
    pix = np.clip((LUV.reshape(-1, 2) * R).astype(int), 0, R - 1)
    outside = int((~mask[pix[:, 1], pix[:, 0]]).sum())
    def uv_area(t): return 0.5 * ((t[..., 1, 0] - t[..., 0, 0]) * (t[..., 2, 1] - t[..., 0, 1]) - (t[..., 2, 0] - t[..., 0, 0]) * (t[..., 1, 1] - t[..., 0, 1]))
    orig_sign = np.sign(uv_area(UV[tri]))
    flipped = int((np.sign(uv_area(LUV)) * orig_sign[src_face[:, 0]] < 0).sum())
    log.append(f'sub {si}: {orig_tris[si]} -> welded {before} -> {nf} triangles; surface distance max {dist.max() * 100:.2f}% '
               f'p99 {np.percentile(dist, 99) * 100:.3f}% of mesh size; UV corners outside coverage {outside}/{nf * 3}; '
               f'UV faces flipped {flipped}; vertices without weights {lost}')

    # 4. Unity vertices: split on (vertex, uv, normal)
    keys = np.concatenate([LV.reshape(-1, 1).astype(np.float64), np.round(LUV.reshape(-1, 2), 6), np.round(corner_n.reshape(-1, 3), 4)], 1)
    uk, first_corner, cinv = np.unique(keys, axis=0, return_index=True, return_inverse=True); cinv = cinv.reshape(-1)
    vidx = LV.reshape(-1)[first_corner]
    out_P.append(LP[vidx] / S); out_N.append(corner_n.reshape(-1, 3)[first_corner]); out_UV.append(LUV.reshape(-1, 2)[first_corner])
    out_BI.append(wi[vidx]); out_BW.append(ww[vidx]); out_SUB.append(cinv.reshape(-1, 3) + base); base += len(uk)

    # render objects: before = the welded original, after = the reduced mesh, both with delivered normals
    me.normals_split_custom_set([tuple(n) for n in N[ftri].reshape(-1, 3)]); render_objs['before'].append(ob)
    low.normals_split_custom_set([tuple(n) for n in corner_n.reshape(-1, 3)]); render_objs['after'].append(lob)

# ---------- write out.bin (same layout as src.bin; tangents zero, recompute in Unity) ----------
OP = np.concatenate(out_P); ON = unit(np.concatenate(out_N)); OUV = np.concatenate(out_UV)
OBI = np.concatenate(out_BI); OBW = np.concatenate(out_BW); m = len(OP)
with open(os.path.join(D, 'out.bin'), 'wb') as f:
    f.write(struct.pack('<i', m)); f.write(OP.astype('<f4').tobytes()); f.write(ON.astype('<f4').tobytes())
    f.write(np.zeros((m, 4), '<f4').tobytes())
    rec = np.zeros(m, np.dtype([('i', '<i4', 4), ('w', '<f4', 4)])); rec['i'] = OBI; rec['w'] = OBW; f.write(rec.tobytes())
    f.write(struct.pack('<i', 8))
    for c in range(8):
        f.write(struct.pack('<i', 1 if c == 0 else 0))
        if c == 0: f.write(OUV.astype('<f4').tobytes())
    f.write(struct.pack('<i', 0))
    f.write(struct.pack('<i', len(out_SUB)))
    for t in out_SUB:
        f.write(struct.pack('<i', t.size)); f.write(t.astype('<i4').tobytes())
    f.write(struct.pack('<i', nbp)); f.write(BIND.astype('<f4').tobytes())
tri_after = [len(t) for t in out_SUB]
log.append(f'result: {m} vertices, triangles {tri_after} = {sum(tri_after)}')

# ---------- renders ----------
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.7, 0.7, 0.7); sc.display.shading.show_backface_culling = False
sc.world = bpy.data.worlds.new('W'); sc.world.color = (0.2, 0.2, 0.25)
sc.render.resolution_x, sc.render.resolution_y = 700, 1000
for ob in bpy.context.scene.objects:
    ob.scale = (-1, 1, 1)   # Unity is left-handed; the dumped mesh space is already Z up (FBX import rotation)
dg = bpy.context.evaluated_depsgraph_get()
pts = np.array([list(o.matrix_world @ Vector(c)) for o in render_objs['before'] for c in o.bound_box])
lo, hi = pts.min(0), pts.max(0); c0 = Vector((lo + hi) / 2); h = float((hi - lo).max())
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = h * 1.05
views = {'front': Vector((0, -1, 0)), 'back': Vector((0, 1, 0)), 'side': Vector((1, 0, 0)), 'front34': Vector((0.7, -0.7, 0.15))}
for tag in ('before', 'after'):
    for o in bpy.context.scene.objects:
        if o.type == 'MESH': o.hide_render = o not in render_objs[tag]
    for name, d in views.items():
        cam.location = c0 + d.normalized() * h * 3
        cam.rotation_euler = (c0 - cam.location).to_track_quat('-Z', 'Z').to_euler()
        sc.render.filepath = os.path.join(D, f'r_{tag}_{name}.png'); bpy.ops.render.render(write_still=True)

open(os.path.join(D, 'report.txt'), 'w', encoding='utf-8').write('\n'.join(log) + '\n')
print('\n'.join(log))
