# Headless: blender -b --python inspect_raw.py -- <raw dir> <out dir>
# Imports each VARCO hand candidate, prints its statistics and renders top / side / palm previews.
import bpy, sys, os, math
from mathutils import Vector

raw, out = sys.argv[sys.argv.index('--') + 1:][:2]
os.makedirs(out, exist_ok=True)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def bounds(objs):
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi

for name in sorted(os.listdir(raw)):
    if not name.endswith('.glb'):
        continue
    reset()
    bpy.ops.import_scene.gltf(filepath=os.path.join(raw, name))
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    lo, hi = bounds(meshes)
    size = hi - lo
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    quads = sum(sum(1 for p in o.data.polygons if len(p.vertices) == 4) for o in meshes)
    polys = sum(len(o.data.polygons) for o in meshes)
    islands = 0
    mats = [(m.name, [n.image.name + ' %dx%d' % tuple(n.image.size) for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]) for m in bpy.data.materials]
    print('RAW %s: objects %d, polys %d (quads %d), tris %d, size %.3f x %.3f x %.3f, armatures %d, mats %s' % (
        name, len(meshes), polys, quads, tris, size.x, size.y, size.z, len([o for o in bpy.data.objects if o.type == 'ARMATURE']), mats))
    # previews
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
    scene.render.resolution_x = 640; scene.render.resolution_y = 640
    world = bpy.data.worlds.new('w'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.35, 0.35, 1); world.node_tree.nodes['Background'].inputs[1].default_value = 1.0
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(30)); scene.collection.objects.link(sun)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.type = 'ORTHO'; centre = (lo + hi) / 2; span = max(size) * 1.15; cam.data.ortho_scale = span
    views = {'top': Vector((0, 0, 1)), 'bottom': Vector((0, 0, -1)), 'front': Vector((0, -1, 0)), 'side': Vector((1, 0, 0))}
    for vn, d in views.items():
        cam.location = centre + d * (max(size) * 3)
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, '%s_%s.png' % (name[:-4], vn))
        bpy.ops.render.render(write_still=True)
