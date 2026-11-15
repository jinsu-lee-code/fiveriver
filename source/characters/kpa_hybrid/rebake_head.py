# A generated head bust made fit for close shots (2026-11-16).
# The bust from VARCO is one shell that goes down to the shoulders, cut into ~300 UV islands, and more than half of
# its 2048 map is uniform and cap: the face got a quarter of the texels, scattered over hundreds of small islands,
# and every island border showed as a dark rim in the game (jaw, ears, brows). Here the bust is
#   welded (one surface, smooth normals across the old seams), cut flat 4.5 cm below its neck (the shoulders and its
#   own collar go: they poked out of the body's collar as jagged shards), unwrapped again in large islands, and its
#   colour and normal are baked from the original onto a new map that holds only the head.
# Writes <out>.glb (the head with the new maps) and <out>.json (the bust's neck level, in its own units).
#   blender -b --factory-startup -P rebake_head.py -- <head.glb> <out without extension> [size]
import bpy, bmesh, sys, json
import numpy as np

src_path, out = sys.argv[sys.argv.index('--') + 1: sys.argv.index('--') + 3]
size = int(sys.argv[sys.argv.index('--') + 3]) if len(sys.argv) > sys.argv.index('--') + 3 else 2048
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src_path)
src = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
bpy.context.view_layer.objects.active = src; src.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# the neck: the narrowest level between 15 % and 55 % of the bust's height (as build_hybrid.py measures it)
V = np.array([v.co[:] for v in src.data.vertices]); up = 2; ax = 0
z0, z1 = V[:, up].min(), V[:, up].max()
best = (1e9, z0)
for z in np.arange(z0 + 0.15 * (z1 - z0), z0 + 0.55 * (z1 - z0), 0.005):
    s = V[(V[:, up] >= z) & (V[:, up] < z + 0.01)]
    if len(s) > 8 and np.ptp(s[:, ax]) < best[0]: best = (np.ptp(s[:, ax]), z)
hneck = best[1]
cm = (z1 - hneck) / 25.0            # a head is about 25 cm from the neck to the crown
cut = hneck - 4.5 * cm

dst = src.copy(); dst.data = src.data.copy(); dst.name = 'head'; bpy.context.collection.objects.link(dst)
bm = bmesh.new(); bm.from_mesh(dst.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0008)
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, cut), plane_no=(0, 0, 1), clear_inner=True)
# what is left of separate bits below the jaw (a collar point that reached up past the cut) is dropped
bm.verts.ensure_lookup_table(); seen = set(); islands = []
for v in bm.verts:
    if v.index in seen: continue
    stack = [v]; seen.add(v.index); part = []
    while stack:
        x = stack.pop(); part.append(x)
        for e in x.link_edges:
            y = e.other_vert(x)
            if y.index not in seen: seen.add(y.index); stack.append(y)
    islands.append(part)
islands.sort(key=len, reverse=True)
bmesh.ops.delete(bm, geom=[v for part in islands[1:] for v in part], context='VERTS')
# spikes: a generated surface has thin slivers standing out of it (one hung under the chin like a thorn, others
# off the cap's brim). A vertex much further from its neighbours' middle than their own spacing is put back there.
spikes = 0
for _ in range(3):
    for v in bm.verts:
        ring = [e.other_vert(v) for e in v.link_edges]
        if len(ring) < 3: continue
        mid = sum((r.co for r in ring), v.co * 0) / len(ring)
        spacing = sum((r.co - mid).length for r in ring) / len(ring)
        if (v.co - mid).length > 1.6 * spacing: v.co = mid; spikes += 1
print('SPIKES moved', spikes)
for f in bm.faces: f.smooth = True
bm.to_mesh(dst.data); bm.free()
while dst.data.uv_layers: dst.data.uv_layers.remove(dst.data.uv_layers[0])
dst.data.uv_layers.new(name='UVMap')
print('NECK %.3f cut %.3f (1 cm = %.4f) islands dropped %d, kept %d vertices' % (hneck, cut, cm, len(islands) - 1, len(dst.data.vertices)))

# New UVs, made here rather than by an automatic unwrap (Smart UV cut this uneven generated surface into hundreds of
# islands again). The sides of the head are one band wrapped round it (seam at the back, so the face is one piece);
# what faces up (the cap's crown) and down (under the chin and the cut) is projected from above and below; the
# strip along the seam is projected from behind. Layout: band v 0..0.72, the three caps in the row above it.
D = np.array([v.co[:] for v in dst.data.vertices])
cx, cy = (D[:, 0].max() + D[:, 0].min()) / 2, (D[:, 1].max() + D[:, 1].min()) / 2
# the busts come out of VARCO facing -y (glTF front; checked on the baked maps: with +y the face was on the seam)
front = -1.0
ztop = D[:, 2].max(); R = max(np.ptp(D[:, 0]), np.ptp(D[:, 1])) / 2
def theta(p): return np.arctan2(p[0] - cx, (p[1] - cy) * front)   # 0 at the face, +-pi at the back
BAND, M = 0.72, 0.012
def band(p, th): return ((th / (2 * np.pi) + 0.5) * (1 - 2 * M) + M, (p[2] - cut) / (ztop - cut) * (BAND - M) + M / 2)
def box(p, a, b, slot, size=0.27):   # planar: a, b are the in-plane coordinates in -1..1
    return (slot * 0.333 + 0.02 + (a * 0.5 + 0.5) * size, BAND + 0.01 + (b * 0.5 + 0.5) * size)
bm = bmesh.new(); bm.from_mesh(dst.data); uvl = bm.loops.layers.uv.active
counts = [0, 0, 0, 0]
for f in bm.faces:
    n = f.normal
    pts = [np.array(l.vert.co[:]) for l in f.loops]
    ths = [theta(p) for p in pts]
    zc = sum(p_[2] for p_ in pts) / len(pts)
    # only the crown and the underside go to the caps: lumps on the brow that happen to face up stay in the band
    if n.z > 0.6 and zc > hneck + 17 * cm:       # crown: from above
        for l, p in zip(f.loops, pts): l[uvl].uv = box(p, (p[0] - cx) / R, -(p[1] - cy) / R * front, 0)
        counts[1] += 1
    elif n.z < -0.5 and (zc < hneck + 5 * cm or zc > hneck + 15 * cm):   # under the chin and the brim: from below
        for l, p in zip(f.loops, pts): l[uvl].uv = box(p, (p[0] - cx) / R, (p[1] - cy) / R * front, 1)
        counts[2] += 1
    elif max(ths) - min(ths) > np.pi or min(abs(t) for t in ths) > 0.93 * np.pi:   # on the seam: from behind
        for l, p in zip(f.loops, pts): l[uvl].uv = box(p, -(p[0] - cx) / R * front, (p[2] - cut) / (ztop - cut) * 2 - 1, 2)
        counts[3] += 1
    else:
        for l, p, t in zip(f.loops, pts, ths): l[uvl].uv = band(p, t)
        counts[0] += 1
bm.to_mesh(dst.data); bm.free()
print('UV band %d crown %d under %d seam %d faces, front %+.0f' % (*counts, front))

# bake target material
mat = bpy.data.materials.new('M_Head'); mat.use_nodes = True; nt = mat.node_tree
bsdf = nt.nodes['Principled BSDF']
imgs = {}
for name, colour in (('BaseColor', 'sRGB'), ('Normal', 'Non-Color')):
    im = bpy.data.images.new(name, size, size, alpha=False, float_buffer=False)
    im.colorspace_settings.name = colour
    node = nt.nodes.new('ShaderNodeTexImage'); node.image = im; node.name = name; imgs[name] = (im, node)
nmap = nt.nodes.new('ShaderNodeNormalMap')
nt.links.new(imgs['Normal'][1].outputs[0], nmap.inputs['Color']); nt.links.new(nmap.outputs[0], bsdf.inputs['Normal'])
dst.data.materials.clear(); dst.data.materials.append(mat)

scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 4
bake = scene.render.bake
bake.use_selected_to_active = True; bake.cage_extrusion = 0.004 / 1.0; bake.max_ray_distance = 0.02; bake.margin = 24
bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); dst.select_set(True); bpy.context.view_layer.objects.active = dst
for name, kind in (('BaseColor', 'DIFFUSE'), ('Normal', 'NORMAL')):
    nt.nodes.active = imgs[name][1]
    if kind == 'DIFFUSE':
        bake.use_pass_direct = False; bake.use_pass_indirect = False; bake.use_pass_color = True
    bpy.ops.object.bake(type=kind)
    im = imgs[name][0]; im.filepath_raw = '%s_%s.png' % (out, name); im.file_format = 'PNG'; im.save()
    print('BAKED', name)
# the base colour drives the shader for the export (the normal map is wired already)
nt.links.new(imgs['BaseColor'][1].outputs[0], bsdf.inputs['Base Color'])
bpy.ops.object.select_all(action='DESELECT'); dst.select_set(True); bpy.context.view_layer.objects.active = dst
bpy.ops.export_scene.gltf(filepath=out + '.glb', use_selection=True, export_format='GLB')
json.dump({'neck': float(hneck), 'cut': float(cut), 'cm': float(cm), 'source_top': float(z1), 'source_bottom': float(z0)}, open(out + '.json', 'w'))
print('WROTE', out + '.glb')
