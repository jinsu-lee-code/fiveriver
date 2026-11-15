# Measures the head of the hybrid body before a new head is fitted (run with the same arguments as build_hybrid.py).
import bpy, sys, numpy as np
old_fbx, new_fbx = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
bpy.ops.wm.read_factory_settings(use_empty=True)
def load(path):
    before = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == 'ARMATURE'), max((o for o in new if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
oa, om = load(old_fbx); na, nm = load(new_fbx)
oj = {b.name: np.array(oa.matrix_world @ b.head_local) for b in oa.data.bones}
nj = {b.name: np.array(na.matrix_world @ b.head_local) for b in na.data.bones}
print('OLD neck', oj['mixamorig:Neck'].round(3), 'head', oj['mixamorig:Head'].round(3), 'headtop', oj.get('mixamorig:HeadTop_End', np.zeros(3)).round(3))
S = 1.756
nv = np.array([nm.matrix_world @ v.co for v in nm.data.vertices])
print('NEW neck', (nj['Neck'] * S).round(3), 'head', (nj['Head'] * S).round(3), 'body top z %.3f' % (nv[:, 2].max() * S))
zn = nj['Head'][2]
head = nv[nv[:, 2] > zn]
print('NEW head above Head joint: width %.3f depth %.3f height %.3f m (scaled), verts %d' % ((np.ptp(head[:, 0]) * S), np.ptp(head[:, 1]) * S, np.ptp(head[:, 2]) * S, len(head)))
ov = np.array([om.matrix_world @ v.co for v in om.data.vertices]); oh = ov[ov[:, 2] > oj['mixamorig:Head'][2]]
print('OLD head above Head joint: width %.3f depth %.3f height %.3f m, verts %d' % (np.ptp(oh[:, 0]), np.ptp(oh[:, 1]), np.ptp(oh[:, 2]), len(oh)))
# faces of the new mesh in the head region: how much texture they get
uv = nm.data.uv_layers.active.data; area3 = areauv = 0.0
for p in nm.data.polygons:
    if all(nv[v][2] > zn for v in p.vertices):
        area3 += p.area * S * S
        u = [np.array(uv[li].uv) for li in p.loop_indices]
        for k in range(1, len(u) - 1): areauv += abs(np.cross(u[k] - u[0], u[k + 1] - u[0])) / 2
print('NEW head: surface %.3f m2, uv share %.1f%% of the 2048 map -> %.0f texels per cm' % (area3, areauv * 100, (areauv * 2048 * 2048 / (area3 * 1e4)) ** 0.5))
