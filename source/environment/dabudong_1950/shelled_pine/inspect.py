import bpy, sys, math
src = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    me = o.data; M = o.matrix_world
    vs = [M @ v.co for v in me.vertices]
    zs = [v.z for v in vs]
    print('OBJ', o.name, 'verts', len(vs), 'z %.2f..%.2f' % (min(zs), max(zs)), 'mats', [m.name for m in me.materials], 'uv', [u.name for u in me.uv_layers], 'scale', tuple(round(s, 3) for s in o.scale))
    if 'LOD0' not in o.name: continue
    H = max(zs) - min(zs)
    for k in range(12):
        a, b = min(zs) + H * k / 12, min(zs) + H * (k + 1) / 12
        r = [math.hypot(v.x, v.y) for v in vs if a <= v.z < b]
        if r: print('  z %.2f-%.2f n=%d r min %.2f med %.2f max %.2f' % (a, b, len(r), min(r), sorted(r)[len(r) // 2], max(r)))
