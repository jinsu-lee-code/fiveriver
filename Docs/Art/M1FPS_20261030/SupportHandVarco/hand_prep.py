import bpy, sys, math, mathutils
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_fbx, tunnel_radius = argv[0], argv[1], float(argv[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
for o in list(bpy.context.scene.objects):
    if o != ob and o.type != 'MESH': bpy.data.objects.remove(o)
ob.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data
pts = [v.co.copy() for v in me.vertices]
bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())

def hole(axis):
    # Rays along `axis` over the other two axes; the tunnel is an empty cell region enclosed by hits.
    a, b = [i for i in range(3) if i != axis]
    mn = [min(p[i] for p in pts) for i in range(3)]; mx = [max(p[i] for p in pts) for i in range(3)]
    n = 160
    grid = [[False] * n for _ in range(n)]
    for i in range(n):
        for j in range(n):
            o = Vector((0, 0, 0)); o[a] = mn[a] + (mx[a] - mn[a]) * (i + .5) / n; o[b] = mn[b] + (mx[b] - mn[b]) * (j + .5) / n
            o[axis] = mn[axis] - 1
            d = Vector((0, 0, 0)); d[axis] = 1
            grid[i][j] = bvh.ray_cast(o, d, 10)[0] is not None
    seen = [[False] * n for _ in range(n)]
    stack = [(i, j) for i in range(n) for j in (0, n - 1)] + [(i, j) for j in range(n) for i in (0, n - 1)]
    while stack:
        i, j = stack.pop()
        if i < 0 or j < 0 or i >= n or j >= n or seen[i][j] or grid[i][j]: continue
        seen[i][j] = True
        stack += [(i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)]
    cells = [(i, j) for i in range(n) for j in range(n) if not grid[i][j] and not seen[i][j]]
    if not cells: return None
    ci = sum(c[0] for c in cells) / len(cells); cj = sum(c[1] for c in cells) / len(cells)
    cell_a = (mx[a] - mn[a]) / n; cell_b = (mx[b] - mn[b]) / n
    centre = Vector((0, 0, 0)); centre[a] = mn[a] + (ci + .5) * cell_a; centre[b] = mn[b] + (cj + .5) * cell_b
    radius = math.sqrt(len(cells) * cell_a * cell_b / math.pi)
    return centre, radius, len(cells)

best = None
for axis in range(3):
    h = hole(axis)
    print("AXIS", axis, None if h is None else (tuple(round(x, 3) for x in h[0]), round(h[1], 4), h[2]))
    if h and (best is None or h[2] > best[1][2]): best = (axis, h)
axis, (centre, radius, _) = best
axis_vec = Vector((0, 0, 0)); axis_vec[axis] = 1
# Tunnel centre along its axis: mean of vertices near the tunnel wall.
near = [p for p in pts if abs((p - centre - axis_vec * (p - centre).dot(axis_vec)).length - radius) < radius * .35]
centre[axis] = sum(p[axis] for p in near) / len(near)
# Wrist: farthest vertices from the tunnel axis.
def radial(p):
    d = p - centre; return d - axis_vec * d.dot(axis_vec)
far = sorted(pts, key=lambda p: radial(p).length)[-max(20, len(pts) // 30):]
wrist = sum((radial(p) for p in far), Vector()) / len(far)
wrist_dist = wrist.length
w = wrist.normalized()
print("TUNNEL axis", axis, "centre", tuple(round(x, 4) for x in centre), "radius", round(radius, 4), "wrist", tuple(round(x, 3) for x in w), "wristDist", round(wrist_dist, 4))
# Canonical Unity frame: tunnel +Z, wrist -Y. Blender b = (-ux, -uz, uy) after FBX import.
src_frame = Matrix((axis_vec, w, axis_vec.cross(w))).transposed()
t1, t2 = Vector((0, -1, 0)), Vector((0, 0, -1))
dst_frame = Matrix((t1, t2, t1.cross(t2))).transposed()
rot = (dst_frame @ src_frame.transposed()).to_4x4()
s = tunnel_radius / radius
ob.matrix_world = Matrix.Scale(s, 4) @ rot @ Matrix.Translation(-centre)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
ob.name = "SM_CH_ROK_SupportHand_M1"
print("SCALE", round(s, 5), "wrist distance m", round(wrist_dist * s, 4))
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                         axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True, mesh_smooth_type='FACE', add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=out_fbx.replace(".fbx", ".blend"))
