# SM_PR_AmmoCrate from VARCO 3D (2026-11-05). ART_SPEC 14: 0.70 x 0.40 x 0.30 m box, 1-2k tris,
# texture 512-1K, pivot bottom centre, material M_PR_AmmoCrate.
# Run: blender -b --factory-startup --python build_ammo_crate.py -- <varco.glb> <out_dir>
import bpy, sys, os, math, json
import numpy as np
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
LENGTH, MAX_TRIS, TEX = 0.70, 2000, 1024

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

# Long side along X (Unity X after export); the crate front (white band) stays where VARCO put it.
ext = co.max(0) - co.min(0)
if ext[1] > ext[0]: o.data.transform(Matrix.Rotation(math.pi / 2, 4, 'Z'))
co = np.array([v.co[:] for v in o.data.vertices])
# Size from the box body: the lid is open, so measure length (X) only and scale uniformly.
lo, hi = co.min(0), co.max(0)
S = LENGTH / (hi[0] - lo[0])
o.data.transform(Matrix.Scale(S, 4) @ Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))

tris_before = sum(len(p.vertices) - 2 for p in o.data.polygons)
if tris_before > MAX_TRIS:
    mod = o.modifiers.new('Decimate', 'DECIMATE'); mod.ratio = MAX_TRIS / tris_before * 0.98
    bpy.ops.object.modifier_apply(modifier=mod.name)
tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
o.name = o.data.name = 'SM_PR_AmmoCrate'

# One material, textures saved at 1K with the existing Unity file names.
mats = [m for m in o.data.materials if m]
mat = mats[0] if mats else bpy.data.materials.new('M_PR_AmmoCrate')
mat.name = 'M_PR_AmmoCrate'
o.data.materials.clear(); o.data.materials.append(mat)
def save(img, name, non_color=False):
    img = img.copy(); img.scale(TEX, TEX)
    img.filepath_raw = os.path.join(OUT, name); img.file_format = 'PNG'; img.save()
found = {}
if mat.use_nodes:
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    def tex_of(sock):
        stack = list(sock.links)
        while stack:
            l = stack.pop(); n = l.from_node
            if n.type == 'TEX_IMAGE' and n.image: return n.image
            for s in n.inputs: stack += list(s.links)
        return None
    if bsdf:
        for key, sock in (('base', 'Base Color'), ('normal', 'Normal'), ('rough', 'Roughness'), ('metal', 'Metallic')):
            img = tex_of(bsdf.inputs[sock]) if sock in bsdf.inputs else None
            if img: found[key] = img
if 'base' in found: save(found['base'], 'T_PR_AmmoCrate_BaseColor.png')
if 'normal' in found: save(found['normal'], 'T_PR_AmmoCrate_Normal.png')
# Unity MetallicSmoothness: R = metallic, A = 1 - roughness (glTF ORM: G roughness, B metallic).
ms = np.zeros((TEX, TEX, 4), np.float32); ms[..., 3] = .25
if 'rough' in found:
    r = found['rough'].copy(); r.scale(TEX, TEX)
    p = np.empty(TEX * TEX * 4, np.float32); r.pixels.foreach_get(p); p = p.reshape(TEX, TEX, 4)
    ms[..., 0] = p[..., 2]; ms[..., 3] = 1 - p[..., 1]
msi = bpy.data.images.new('T_PR_AmmoCrate_MetallicSmoothness', TEX, TEX, alpha=True); msi.pixels.foreach_set(ms.ravel())
msi.filepath_raw = os.path.join(OUT, 'T_PR_AmmoCrate_MetallicSmoothness.png'); msi.file_format = 'PNG'; msi.save()

bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'SM_PR_AmmoCrate.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
                         path_mode='STRIP')
co = np.array([v.co[:] for v in o.data.vertices])
info = dict(scale=S, size=(co.max(0) - co.min(0)).tolist(), tris_before=tris_before, tris=tris, textures=sorted(found))
json.dump(info, open(os.path.join(OUT, 'build_manifest.json'), 'w'), indent=2)
print('CRATE ' + json.dumps(info))

sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
sc.world = bpy.data.worlds.new('W'); sc.world.color = (.5, .5, .5); sc.render.resolution_x, sc.render.resolution_y = 900, 650
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(30))); bpy.context.object.data.energy = 3
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 50
for name, loc in (('review_front', (1.1, -1.5, 0.9)), ('review_back', (-1.1, 1.5, 0.9))):
    cam.location = loc; cam.rotation_euler = (Vector((0, 0, .2)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, name + '.png'); bpy.ops.render.render(write_still=True)
