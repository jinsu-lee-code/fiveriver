# Headless: blender -b --python inspect_rig.py -- <SK_CH_ROK_Arms.fbx>
# Prints the bone tree, rest positions and the meshes of the current first-person arms.
import bpy, sys
from mathutils import Vector
path = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
for o in bpy.data.objects:
    print('OBJ', o.type, o.name, 'parent', o.parent.name if o.parent else None, 'scale', tuple(round(v, 4) for v in o.scale), 'rot', tuple(round(v, 3) for v in o.rotation_euler))
    if o.type == 'MESH':
        lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        print('   verts', len(o.data.vertices), 'tris', sum(len(p.vertices) - 2 for p in o.data.polygons), 'bounds', tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi),
              'groups', len(o.vertex_groups), 'mats', [m.name for m in o.data.materials], 'uv', [u.name for u in o.data.uv_layers])
    if o.type == 'ARMATURE':
        def walk(b, d):
            hw = o.matrix_world @ b.head_local; tw = o.matrix_world @ b.tail_local
            print('   ' + '  ' * d + '%s  head (%.3f, %.3f, %.3f) tail (%.3f, %.3f, %.3f)' % (b.name, hw.x, hw.y, hw.z, tw.x, tw.y, tw.z))
            for c in b.children: walk(c, d + 1)
        for b in o.data.bones:
            if b.parent is None: walk(b, 0)
print('ACTIONS', [(a.name, tuple(a.frame_range)) for a in bpy.data.actions])
