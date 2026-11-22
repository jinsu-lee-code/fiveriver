# Renders of the hand alone, before it is bound (run from build_hands_model.py when HAND_PREVIEW is set: a path prefix).
import math
pre = os.environ['HAND_PREVIEW']
m = bpy.data.materials.new('skin'); m.use_nodes = True
bs = m.node_tree.nodes['Principled BSDF']; ti = m.node_tree.nodes.new('ShaderNodeTexImage'); ti.image = img
m.node_tree.links.new(ti.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = 0.6
hand.data.materials.clear(); hand.data.materials.append(m)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 700; sc.render.resolution_y = 900
sc.view_settings.view_transform = 'Standard'
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.18, 0.19, 0.21, 1); sc.world = w
sun = bpy.data.objects.new('s', bpy.data.lights.new('s', 'SUN')); sun.data.energy = 3.0; bpy.context.collection.objects.link(sun)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); bpy.context.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.27; cam.data.clip_start = 0.01
c = Vector((0.0, 0.0, 0.075))
views = {'palm': (0, 1, 0), 'back': (0, -1, 0), 'thumb': (1, 0.15, 0), 'little': (-1, 0.15, 0), 'q_palm': (0.7, 0.7, 0.2), 'q_back': (0.6, -0.7, 0.3), 'tips': (0.0, -0.45, 1.0), 'thumb_q': (0.9, 0.5, 0.2), 'from_tips': (0.0, 0.35, 1.0), 'nails': (0.15, -1, 0.55)}
for k, d in views.items():
    dv = Vector(d).normalized(); cam.location = c + dv * 0.5
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    if k == 'nails': cam.data.ortho_scale = 0.12; cam.location = Vector((0.0, 0.0, 0.155)) + dv * 0.5
    else: cam.data.ortho_scale = 0.27
    sun.rotation_euler = (Vector((0.35, 0.25, 0.0)) + Vector(cam.rotation_euler)).to_tuple() if False else cam.rotation_euler
    sun.rotation_euler.rotate_axis('X', 0.6); sun.rotation_euler.rotate_axis('Y', 0.5)
    sc.render.filepath = pre + '_' + k + '.png'; bpy.ops.render.render(write_still=True)
print('PREVIEW written', pre)
raise SystemExit
