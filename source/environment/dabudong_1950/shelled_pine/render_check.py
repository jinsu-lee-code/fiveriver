# Renders the three shelled pines side by side for a look before they go into Unity.
#   blender -b --factory-startup -P render_check.py -- <dir with SM_ShelledPine_*.fbx> <out.png>
import bpy, sys, math
from mathutils import Vector

d, out = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bark = bpy.data.materials.new('bark'); bark.diffuse_color = (0.30, 0.20, 0.14, 1)
inner = bpy.data.materials.new('inner'); inner.diffuse_color = (0.80, 0.66, 0.42, 1)
x = 0.0
for n in ('A', 'B', 'C'):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=d + '/SM_ShelledPine_%s.fbx' % n)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type == 'MESH':
            for i, s in enumerate(o.material_slots): s.material = inner if 'Splinter' in (s.material.name if s.material else '') else bark
        if o.parent is None: o.location.x += x
    x += 4.5
bpy.ops.mesh.primitive_plane_add(size=60, location=(4.5, 0, 0))
g = bpy.data.materials.new('g'); g.diffuse_color = (0.42, 0.36, 0.24, 1); bpy.context.object.data.materials.append(g)
sun = bpy.data.lights.new('s', 'SUN'); sun.energy = 3.5; so = bpy.data.objects.new('s', sun); bpy.context.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(35))
cam = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cam); bpy.context.collection.objects.link(co)
co.location = (4.5, -13, 3.2); co.rotation_euler = (math.radians(82), 0, 0); cam.lens = 35
sc = bpy.context.scene; sc.camera = co; sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'; sc.display.shading.show_shadows = True; sc.display.shading.show_cavity = True
sc.render.resolution_x = 1800; sc.render.resolution_y = 800; sc.render.filepath = out
bpy.ops.render.render(write_still=True)
