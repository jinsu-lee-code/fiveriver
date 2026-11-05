# SM_VH_T34_85_Destroyed from VARCO 3D (2026-11-05, user reference: burnt-out museum wreck photo).
# Same frame as the playable SM_VH_T34_85: width 3.0 m, front -Y (Unity +Z), pivot at ground centre of the hull.
# Run: blender -b --factory-startup --python build_destroyed.py -- <varco.glb> <out_dir>
import bpy, sys, os, math, json
import numpy as np
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
WIDTH, TEX = 3.0, 2048

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
o = bpy.context.view_layer.objects.active
o.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co = np.array([v.co[:] for v in o.data.vertices])

# Hull = lower half (the gun and blown hatches sit above it). Robust percentiles ignore stray debris.
zlo, zhi = np.percentile(co[:, 2], 0.5), co[:, 2].max()
hull = co[co[:, 2] < zlo + (zhi - zlo) * 0.45]
x0, x1 = np.percentile(hull[:, 0], [1, 99]); y0, y1 = np.percentile(hull[:, 1], [1, 99])
S = WIDTH / (x1 - x0)
o.data.transform(Matrix.Scale(S, 4) @ Matrix.Translation(Vector((-(x0 + x1) / 2, -(y0 + y1) / 2, -zlo))))
o.name = o.data.name = 'SM_VH_T34_85_Destroyed'

mat = next((m for m in o.data.materials if m), None) or bpy.data.materials.new('M')
mat.name = 'M_VH_T34_Destroyed'
o.data.materials.clear(); o.data.materials.append(mat)
found = {}
bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat.use_nodes else None
def tex_of(sock):
    stack = list(sock.links)
    while stack:
        l = stack.pop(); n = l.from_node
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        for s in n.inputs: stack += list(s.links)
    return None
if bsdf:
    for key, sock in (('base', 'Base Color'), ('normal', 'Normal'), ('rough', 'Roughness')):
        img = tex_of(bsdf.inputs[sock]);
        if img: found[key] = img
def save(img, name):
    img = img.copy(); img.scale(TEX, TEX)
    img.filepath_raw = os.path.join(OUT, name); img.file_format = 'PNG'; img.save()
if 'base' in found: save(found['base'], 'T_VH_T34_Destroyed_BaseColor.png')
if 'normal' in found: save(found['normal'], 'T_VH_T34_Destroyed_Normal.png')
ms = np.zeros((TEX, TEX, 4), np.float32); ms[..., 3] = .15        # burnt steel: dull
if 'rough' in found:
    r = found['rough'].copy(); r.scale(TEX, TEX)
    p = np.empty(TEX * TEX * 4, np.float32); r.pixels.foreach_get(p); p = p.reshape(TEX, TEX, 4)
    ms[..., 0] = p[..., 2]; ms[..., 3] = 1 - p[..., 1]
msi = bpy.data.images.new('MS', TEX, TEX, alpha=True); msi.pixels.foreach_set(ms.ravel())
msi.filepath_raw = os.path.join(OUT, 'T_VH_T34_Destroyed_MetallicSmoothness.png'); msi.file_format = 'PNG'; msi.save()

bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'SM_VH_T34_85_Destroyed.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
                         path_mode='STRIP')
co = np.array([v.co[:] for v in o.data.vertices])
info = dict(scale=S, size=(co.max(0) - co.min(0)).tolist(), min=co.min(0).tolist(), max=co.max(0).tolist(),
            tris=sum(len(p.vertices) - 2 for p in o.data.polygons), textures=sorted(found))
json.dump(info, open(os.path.join(OUT, 'build_manifest.json'), 'w'), indent=2)
print('WRECK ' + json.dumps(info))

sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
sc.world = bpy.data.worlds.new('W'); sc.world.color = (.45, .45, .45); sc.render.resolution_x, sc.render.resolution_y = 1100, 700
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(35))); bpy.context.object.data.energy = 3.2
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 40
for name, loc in (('review_front_right', (10, -9, 4.5)), ('review_left', (-12, -2, 3)), ('review_rear', (-7, 10, 5))):
    cam.location = loc; cam.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, name + '.png'); bpy.ops.render.render(write_still=True)
