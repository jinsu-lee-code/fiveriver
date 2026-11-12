# 삼각형 줄이기 + 작은 구멍 메우기 (2026-11-10, 사용자 요청 "삼각형 너무 많은건 blender로 줄여줘 · 떠 있는 조각 · 구멍 고쳐줘")
# Keeps object names, parent/child, origins, material slots and UVs; exports with the project's FBX rules (ART_SPEC 1-4,
# same call as Docs/Art/T34_Varco432_20261105/build_t34_varco432.py).
#   1. weld coincident vertices (1e-5 of the object size) so collapse can work across the scan's split faces
#   2. fill boundary loops smaller than 2 % of the object size (holes); new faces copy UVs from a neighbouring face
#   3. collapse-decimate each object to its target triangle count; objects listed in "_protect_uv_seams" keep their UV-seam
#      vertices (baked markings such as the turret star stay intact)
#   4. reduced meshes: shade smooth with sharp edges above 40 degrees (normal map + baked colour carry the detail);
#      meshes that only had holes filled keep their delivered normals
#   5. renders before/after with the base colour texture
# usage: blender -b --factory-startup --python reduce_and_fill.py -- <in.fbx> <out.fbx> <render_dir> <targets json> [basecolor.png]
#   targets json: {"Hull": 20000, ...} or {"*": 55000} (split over all mesh objects by their current share)
import bpy, bmesh, sys, os, json, math
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
SRC, DST, RDIR, TARGETS = a[0], a[1], a[2], json.loads(a[3])
PROTECT = set(TARGETS.pop('_protect_uv_seams', []))  # objects whose UV seams must survive (baked markings)
BASE = a[4] if len(a) > 4 else None
os.makedirs(RDIR, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']

def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE' if BASE else 'MATERIAL'
sc.display.shading.show_backface_culling = True
sc.world = bpy.data.worlds.new('W'); sc.world.color = (1, 0, 1)
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
if BASE:
    img = bpy.data.images.load(BASE)
    for o in meshes:
        for m in o.data.materials:
            if not m: continue
            m.use_nodes = True
            t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = img; m.node_tree.nodes.active = t
            bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
            m.node_tree.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
cen = (lo + hi) / 2; rad = (hi - lo).length
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 50
views = {'front34': Vector((0.7, -0.9, 0.45)), 'side': Vector((1, 0, 0.15)), 'rear34': Vector((-0.6, 0.9, 0.5)), 'top': Vector((0.05, 0.05, 1))}
def render(tag):
    for name, d in views.items():
        cam.location = cen + d.normalized() * rad * 1.15
        cam.rotation_euler = (cen - cam.location).to_track_quat('-Z', 'Z' if name != 'top' else 'Y').to_euler()
        sc.render.filepath = os.path.join(RDIR, f'{tag}_{name}.png'); bpy.ops.render.render(write_still=True)
render('before')

total = sum(tris(o) for o in meshes)
log = {'before': {o.name: tris(o) for o in meshes}}
for o in meshes:
    size = max(o.dimensions) or 1.0
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=size * 1e-5)
    uv = bm.loops.layers.uv.active
    bnd = [e for e in bm.edges if e.is_boundary]
    adj = {}
    for e in bnd:
        for v in e.verts: adj.setdefault(v, []).append(e)
    seen = set(); small = []
    for e in bnd:
        if e in seen: continue
        st = [e]; comp = []
        while st:
            x = st.pop()
            if x in seen: continue
            seen.add(x); comp.append(x)
            for v in x.verts: st.extend(y for y in adj[v] if y not in seen)
        if sum(x.calc_length() for x in comp) < size * 0.02: small.extend(comp)
    before_faces = set(bm.faces)
    if small: bmesh.ops.holes_fill(bm, edges=small, sides=0)
    new_faces = [f for f in bm.faces if f not in before_faces]
    if uv:
        for f in new_faces:
            for l in f.loops:
                src = next((ol for ol in l.vert.link_loops if ol.face not in new_faces), None)
                if src: l[uv].uv = src[uv].uv
    if new_faces and len(o.data.materials) > 0:
        for f in new_faces: f.material_index = next((ol.face.material_index for ol in f.loops[0].vert.link_loops if ol.face not in new_faces), 0)
    bmesh.ops.recalc_face_normals(bm, faces=new_faces) if new_faces else None
    bm.to_mesh(o.data); bm.free()
    log.setdefault('holes_filled_faces', {})[o.name] = len(new_faces)

reduced = set()
for o in meshes:
    target = TARGETS.get(o.name)
    if target is None and '*' in TARGETS: target = TARGETS['*'] * log['before'][o.name] / total
    if target is None or tris(o) <= target: continue
    reduced.add(o.name)
    bpy.context.view_layer.objects.active = o
    # protect UV seams (vertices whose corners use more than one UV): collapsing across them tears baked markings
    me = o.data; uvl = me.uv_layers.active
    seam = set()
    if uvl:
        first = {}
        for li, loop in enumerate(me.loops):
            u = tuple(round(x, 5) for x in uvl.data[li].uv)
            if first.setdefault(loop.vertex_index, u) != u: seam.add(loop.vertex_index)
    m = o.modifiers.new('Decimate', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = target / tris(o); m.use_collapse_triangulate = True
    if o.name in PROTECT:
        vg = o.vertex_groups.new(name='keep_uv_seams')
        vg.add(list(seam), 1.0, 'REPLACE')
        log.setdefault('protected_seam_vertices', {})[o.name] = len(seam)
        m.vertex_group = vg.name; m.invert_vertex_group = True; m.vertex_group_factor = 1000.0
    with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier=m.name)
for o in meshes:  # only reduced meshes get new shading; hole-only fixes keep the delivered normals
    if o.name not in reduced: continue
    for p in o.data.polygons: p.use_smooth = True
    o.data.set_sharp_from_angle(angle=math.radians(40))
log['after'] = {o.name: tris(o) for o in meshes}
log['total_before'] = total; log['total_after'] = sum(tris(o) for o in meshes)
render('after')

bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.export_scene.fbx(filepath=DST, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
                         add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=False, path_mode='STRIP')
json.dump(log, open(os.path.join(RDIR, 'reduce_log.json'), 'w'), indent=1)
print('REDUCE_DONE', json.dumps(log))
