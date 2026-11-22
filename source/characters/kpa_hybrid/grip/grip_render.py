# A solved hold with the real rifle, six views.  blender -b --factory-startup -P grip_render.py -- <grip> ...
import bpy, sys
import numpy as np
from mathutils import Vector, Matrix

D = 'C:/sk_assets/capture/grip/'
RV, RF, V0, F0 = [], [], [], []
bl = lambda p: (p[0], p[2], p[1])           # rifle space (x right, y up, z muzzle) -> Blender (z up)
for line in open(D + 'static.txt', encoding='utf-8-sig'):
    p = line.split()
    if p[0] == 'RV': RV.append(bl([float(a) for a in p[1:4]]))
    elif p[0] == 'RF': a, b, c = (int(q) for q in p[1:4]); RF.append((a, c, b))

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


def material(col, rough):
    m = bpy.data.materials.new('m'); m.use_nodes = True
    n = m.node_tree.nodes['Principled BSDF']
    n.inputs['Base Color'].default_value = (*col, 1); n.inputs['Roughness'].default_value = rough
    return m


def mesh(v, f, mat):
    me = bpy.data.meshes.new('o'); me.from_pydata(v, [], f); me.update()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    o = bpy.data.objects.new('o', me); sc.collection.objects.link(o); me.materials.append(mat)
    return o


skin, wood, arm_m = material((0.50, 0.27, 0.18), 0.6), material((0.07, 0.10, 0.14), 0.5), material((0.22, 0.25, 0.10), 0.9)
mesh(RV, RF, wood)
sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = 760, 620
sc.view_settings.view_transform = 'Standard'
w = bpy.data.worlds.new('w'); w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.62, 0.64, 0.67, 1)
w.node_tree.nodes['Background'].inputs[1].default_value = 0.6
sc.world = w
sun = bpy.data.objects.new('s', bpy.data.lights.new('s', 'SUN')); sun.data.energy = 2.4; sc.collection.objects.link(sun)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.26
cam.data.clip_start = 0.01; sc.collection.objects.link(cam); sc.camera = cam
for name in sys.argv[sys.argv.index('--') + 1:]:
    g = np.load(D + 'grip_%s.npz' % name)
    hand = mesh([bl(p) for p in g['P']], [(int(a), int(c), int(b)) for a, b, c in g['F']], skin)
    e, wr = Vector(bl(g['elbow'])), Vector(bl(g['x'][:3]))
    dv = wr - e
    bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=dv.length * 0.6, location=e + dv * 0.3,
                                        rotation=dv.to_track_quat('Z', 'Y').to_euler())
    arm = bpy.context.object; arm.data.materials.append(arm_m)
    J = g['J']
    c = Vector(bl((J[1:, :3].reshape(-1, 3).mean(0) + g['x'][:3]) / 2))
    for k, d in (('right', (1, 0, 0.05)), ('left', (-1, 0, 0.05)), ('under', (0.05, 0, -1)),
                 ('top', (-0.05, 0, 1)), ('front', (0.55, 1, 0.1)), ('back', (0.5, -1, 0.35))):
        d = Vector(d).normalized(); cam.location = c + d * 0.5
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sun.rotation_euler = cam.rotation_euler
        sun.rotation_euler.rotate_axis('X', -0.55); sun.rotation_euler.rotate_axis('Y', 0.45)
        sc.render.filepath = D + 'v_%s_%s.png' % (name, k)
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(hand); bpy.data.objects.remove(arm)
print('VIEWS done')
