# The old rig's right-hand joints and old hand mesh size, for fitting a new hand (same arguments as build_hybrid.py's first one).
import bpy, sys, numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=sys.argv[sys.argv.index('--') + 1])
a = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for b in a.data.bones:
    if 'RightHand' in b.name or b.name.endswith('RightForeArm'):
        print('BONE %-28s head %s tail %s parent %s' % (b.name.split(':')[1], np.round(a.matrix_world @ b.head_local, 3), np.round(a.matrix_world @ b.tail_local, 3), b.parent.name.split(':')[1]))
