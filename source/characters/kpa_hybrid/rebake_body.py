# The generated uniform made fit for close shots (2026-11-16), as rebake_head.py does for the heads.
# The rigged body from VARCO came with one 2048 map cut into hundreds of small islands for the whole man; each
# island had its own brightness, so the cloth read as a mottled camouflage, and every border showed. Here the body
# is welded (weights kept), unwrapped again in large islands, and its colour baked from the original onto a 4096
# map. The result is exported with its armature like the source, for build_hybrid.py.
#   blender -b --factory-startup -P rebake_body.py -- <kpa_rigged.fbx> <source base colour.png> <out dir> [size]
import bpy, bmesh, sys, os

args = sys.argv[sys.argv.index('--') + 1:]
src_fbx, src_tex, out = args[0], args[1], args[2]
size = int(args[3]) if len(args) > 3 else 4096
os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src_fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))

# the source to bake from: a copy with the original UVs and the original map
src = body.copy(); src.data = body.data.copy(); src.name = 'source'; bpy.context.collection.objects.link(src)
smat = bpy.data.materials.new('M_Source'); smat.use_nodes = True
tex = smat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(src_tex)
smat.node_tree.links.new(tex.outputs[0], smat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
src.data.materials.clear(); src.data.materials.append(smat)

# the body: welded, new UVs
bm = bmesh.new(); bm.from_mesh(body.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
for f in bm.faces: f.smooth = True
bm.to_mesh(body.data); bm.free()
while body.data.uv_layers: body.data.uv_layers.remove(body.data.uv_layers[0])
body.data.uv_layers.new(name='UVMap')
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.2, island_margin=0.0015, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=0.002, rotate=True)
bpy.ops.object.mode_set(mode='OBJECT')
print('BODY welded %d -> %d vertices' % (before, len(body.data.vertices)))

mat = bpy.data.materials.new('M_Body'); mat.use_nodes = True; nt = mat.node_tree
img = bpy.data.images.new('BaseColor', size, size, alpha=False); node = nt.nodes.new('ShaderNodeTexImage'); node.image = img
body.data.materials.clear(); body.data.materials.append(mat)
scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 2
bake = scene.render.bake
bake.use_selected_to_active = True; bake.cage_extrusion = 0.01; bake.max_ray_distance = 0.03; bake.margin = 16
bake.use_pass_direct = False; bake.use_pass_indirect = False; bake.use_pass_color = True
# both in the rest pose: the armature modifiers would pose them identically anyway, but off is cheaper
for o in (src, body):
    for m in o.modifiers: m.show_render = m.show_viewport = False
bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = body
nt.nodes.active = node
bpy.ops.object.bake(type='DIFFUSE')
img.filepath_raw = os.path.join(out, 'body_BaseColor.png'); img.file_format = 'PNG'; img.save()
nt.links.new(node.outputs[0], nt.nodes['Principled BSDF'].inputs['Base Color'])
for m in body.modifiers: m.show_render = m.show_viewport = True
bpy.data.objects.remove(src)
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'kpa_rigged_clean.fbx'), use_selection=True, add_leaf_bones=False,
                         bake_anim=False, path_mode='STRIP', mesh_smooth_type='FACE')
print('WROTE', out)
