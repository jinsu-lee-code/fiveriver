# blender -b --factory-startup -P dump.py -- <in.fbx> <out_prefix>
# Writes per track: <prefix>_<Track>.npz with Unity tank-body coordinates (x, y, z) of every vertex,
# triangles (loop triangles, vertex indices) and the polygon each triangle came from.
# Mapping: Unity (x, y, z) = (-bx, bz, -by)  (Blender FBX export axis_forward=-Z, axis_up=Y; Unity negates X on import)
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
for name in ('TrackL', 'TrackR'):
    o = bpy.data.objects[name]
    me = o.data
    me.calc_loop_triangles()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    M = np.array(o.matrix_world)
    w = co @ M[:3, :3].T + M[:3, 3]
    u = np.c_[-w[:, 0], w[:, 2], -w[:, 1]]
    lt = np.empty(len(me.loop_triangles) * 3, dtype=np.int32); me.loop_triangles.foreach_get('vertices', lt)
    lp = np.empty(len(me.loop_triangles), dtype=np.int32); me.loop_triangles.foreach_get('polygon_index', lp)
    np.savez(a[1] + '_' + name + '.npz', v=u.astype(np.float64), t=lt.reshape(-1, 3), poly=lp, blender=w)
    print('DUMP', name, len(u), len(lt) // 3)
