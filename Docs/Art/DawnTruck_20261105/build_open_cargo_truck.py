# Dawn truck rebuild (2026-11-05): open stake-side cargo bed after the 1950 reference photo.
# Run: blender -b --factory-startup --python build_open_cargo_truck.py -- <out_dir>
# Keeps the original GMC scan's cab, chassis and wheels untouched (source file is never modified).
# Removes everything above the cargo floor behind the cab (canvas, bows, scan holes) and adds a
# wooden stake rack: three olive-painted side boards, vertical stakes, front rack, fold-down troop
# benches, and an open rear (the existing Unity ramp attaches there).
import bpy, sys, os, json, math, random
import numpy as np
from mathutils import Vector

SRC = 'C:/Users/mbc/Downloads/FiveRivers_Transfer/Codex/AssetCheck_20261026/Assets/_Project/Resources/Vehicles/'
OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.dirname(os.path.abspath(__file__))
os.makedirs(OUT, exist_ok=True)

# Blender axes of the source: +Y rear, -Y front, Z up. Unity local: front +Z, so Blender y = -Unity z.
CAB_BACK = -0.50      # cargo region starts behind this
FLOOR_TOP = 1.38      # matches Unity passenger sockets and ramp lip
CUT_Z = 1.40          # source geometry above this (behind the cab) is removed
BED_FRONT, BED_REAR = -0.46, 3.36

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC + 'GMC_Truck.fbx')
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = body; body.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
old = body.data
uv = old.uv_layers.active.data
normals = old.corner_normals

# Bed half-width from the source's cargo sides just below the floor.
side_x = sorted(abs(v.co.x) for v in old.vertices if 0.2 < v.co.y < 3.0 and 1.15 < v.co.z < CUT_Z)
HALF = round(side_x[int(len(side_x) * .9)], 3) if side_x else 1.10
HALF = min(max(HALF, 1.0), 1.25)

verts, faces, uvfaces, nfaces, smooth = [], [], [], [], []
kept = clipped = removed = 0

def split(poly, axis, bound):
    below, above = [], []
    for i, a in enumerate(poly):
        b = poly[(i + 1) % len(poly)]
        da, db = a[0][axis] - bound, b[0][axis] - bound
        (below if da <= 1e-7 else above).append(a)
        if (da > 1e-7 and db < -1e-7) or (da < -1e-7 and db > 1e-7):
            t = da / (da - db)
            p = (a[0].lerp(b[0], t), a[1].lerp(b[1], t), a[2].lerp(b[2], t).normalized())
            below.append(p); above.append(p)
    return below, above

def add(poly, sm):
    if len(poly) < 3: return
    base = len(verts); verts.extend(p[0] for p in poly)
    faces.append(tuple(range(base, base + len(poly))))
    uvfaces.append([p[1] for p in poly]); nfaces.append([p[2] for p in poly]); smooth.append(sm)

for f in old.polygons:
    poly = [(old.vertices[old.loops[l].vertex_index].co.copy(), uv[l].uv.copy(), normals[l].vector.copy()) for l in f.loop_indices]
    ys = [p[0].y for p in poly]; zs = [p[0].z for p in poly]
    if max(ys) <= CAB_BACK or max(zs) <= CUT_Z:
        add(poly, f.use_smooth); kept += 1; continue
    if min(ys) >= CAB_BACK and min(zs) >= CUT_Z:
        removed += 1; continue
    front, rear = split(poly, 1, CAB_BACK)
    add(front, f.use_smooth)
    low, _ = split(rear, 2, CUT_Z) if len(rear) >= 3 else ([], [])
    add(low, f.use_smooth)
    clipped += 1

mesh = bpy.data.meshes.new('GMC_Body_OpenCargo'); mesh.from_pydata(verts, [], faces); mesh.update()
layer = mesh.uv_layers.new(name='UVMap'); flat = []
for f, uf, nf, sm in zip(mesh.polygons, uvfaces, nfaces, smooth):
    f.use_smooth = sm
    for li, u, n in zip(f.loop_indices, uf, nf): layer.data[li].uv = u; flat.append(n)
mesh.normals_split_custom_set(flat)
body.data = mesh; body.name = 'GMC_Body'
atlas = bpy.data.materials.new('OriginalAtlas'); atlas.use_nodes = True
bs = atlas.node_tree.nodes['Principled BSDF']; bs.inputs['Roughness'].default_value = .85
tex = atlas.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(SRC + 'T_GMC_Truck_BaseColor.jpg')
atlas.node_tree.links.new(tex.outputs['Color'], bs.inputs['Base Color']); mesh.materials.append(atlas)

# ---------- Painted wood texture (olive drab over weathered grey timber) ----------
rng = np.random.default_rng(1950)
W = 1024
yy, xx = np.mgrid[0:W, 0:W].astype(np.float32) / W
plank = np.floor(yy * 12)                     # 12 planks per tile (tile = 2 m -> ~16 cm boards)
pv = (yy * 12) % 1
offs = rng.random(13).astype(np.float32)
grain = np.sin((xx * 38 + offs[plank.astype(int)] * 9 + np.sin(yy * 120 + xx * 7) * .35) * 6.283) * .5 + .5
fine = rng.random((W, W)).astype(np.float32)
fine = (fine + np.roll(fine, 1, 1) + np.roll(fine, 2, 1) + np.roll(fine, 3, 1)) / 4
paint = np.array([.36, .35, .25], np.float32)       # faded olive drab, matched to the scanned body paint
wood = np.array([.47, .44, .38], np.float32)        # weathered timber where paint wore off
wear_noise = rng.random((W // 32, W // 32)).astype(np.float32)
wear = np.kron(wear_noise, np.ones((32, 32), np.float32))
edge = np.minimum(pv, 1 - pv)
wear_mask = np.clip((wear - .78) * 6 + (0.06 - edge) * 14, 0, 1)[..., None]
col = paint * (0.86 + .14 * plank[..., None] % 3 / 2) * (0.9 + .1 * grain[..., None]) * (0.92 + .16 * fine[..., None])
col = col * (1 - wear_mask) + wood * (0.85 + .2 * grain[..., None]) * wear_mask
col *= np.where(edge < .012, .45, 1.0)[..., None]   # dark gap between boards
col = np.clip(col, 0, 1)
img = bpy.data.images.new('T_DawnTruck_Wood', W, W)
img.pixels.foreach_set(np.concatenate([col[::-1], np.ones((W, W, 1), np.float32)], 2).ravel())
img.filepath_raw = os.path.join(OUT, 'T_DawnTruck_Wood.png'); img.file_format = 'PNG'; img.save()

woodmat = bpy.data.materials.new('Wood'); woodmat.use_nodes = True
wb = woodmat.node_tree.nodes['Principled BSDF']; wb.inputs['Roughness'].default_value = .9
wt = woodmat.node_tree.nodes.new('ShaderNodeTexImage'); wt.image = img
woodmat.node_tree.links.new(wt.outputs['Color'], wb.inputs['Base Color'])

# ---------- Rack geometry (single mesh, box-projected UVs, 2 m per texture tile) ----------
rv, rf, ruv = [], [], []
def box(cx, cy, cz, sx, sy, sz, along='y'):
    a, b, c = sx / 2, sy / 2, sz / 2
    corners = [(cx + dx * a, cy + dy * b, cz + dz * c) for dx, dy, dz in
               [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    for quad, n in [((0,3,2,1),'z'),((4,5,6,7),'z'),((0,1,5,4),'y'),((2,3,7,6),'y'),((1,2,6,5),'x'),((3,0,4,7),'x')]:
        base = len(rv); rv.extend(corners[i] for i in quad); rf.append(tuple(range(base, base + 4)))
        for i in quad:
            x, y, z = corners[i]
            # Board length runs along U so the painted planks follow each board.
            if n == 'x': u, v = (y, z) if along == 'y' else (z, y)
            elif n == 'y': u, v = (x, z) if along == 'x' else (z, x)
            else: u, v = (y, x) if along == 'y' else (x, y)
            ruv.append((u / 2, v / 2))

L = BED_REAR - BED_FRONT; MID = (BED_FRONT + BED_REAR) / 2
box(0, MID, FLOOR_TOP - .04, HALF * 2, L, .08)                            # floor deck
for side in (-1, 1):
    x = side * (HALF - .02)
    box(side * (HALF - .05), MID, FLOOR_TOP + .03, .10, L, .06)          # floor edge sill
    for z in (1.62, 1.88, 2.14):                                          # three side boards
        box(x, MID, z, .035, L - .04, .14)
    stakes = 7
    for i in range(stakes):                                               # vertical stakes, outside the boards
        y = BED_FRONT + .05 + (L - .10) * i / (stakes - 1)
        box(side * (HALF + .015), y, 1.78, .055, .07, .84, along='z')
    # Fold-down troop bench (seat .44 m above floor) with three legs.
    bx = side * (HALF - .21)
    box(bx, MID + .2, FLOOR_TOP + .44, .30, L - .75, .04)
    for y in (BED_FRONT + .55, MID + .2, BED_REAR - .25):
        box(bx + side * -.10, y, FLOOR_TOP + .21, .04, .04, .42, along='z')
for z in (1.62, 1.88, 2.14):                                              # front rack
    box(0, BED_FRONT + .02, z, HALF * 2 - .02, .035, .14, along='x')
for x in (-HALF * .45, HALF * .45):
    box(x, BED_FRONT - .015, 1.78, .07, .055, .84, along='z')

# The source's two mirror heads are disconnected islands; add the missing mounting rods
# (same fix as the 11-02 repair) as thin boxes from the nearest cab vertex.
oldverts = [v.co.copy() for v in old.vertices]
for side, tip in [(-1, Vector((-1.445, -1.46, 2.186))), (1, Vector((1.425, -1.466, 2.386)))]:
    near = [v for v in oldverts if 0 < side * v.x < 1.30 and abs(v.y - tip.y) < .7 and v.z > 1.7]
    start = min(near, key=lambda v: (v - tip).length)
    d = tip - start; n = 6
    for i in range(n):                                   # short segments approximate the diagonal rod
        p = start + d * ((i + .5) / n)
        box(p.x, p.y, p.z, max(.02, abs(d.x) / n + .02), max(.02, abs(d.y) / n + .02), max(.02, abs(d.z) / n + .02), along='z')

rm = bpy.data.meshes.new('CargoRack'); rm.from_pydata(rv, [], rf); rm.update()
ru = rm.uv_layers.new(name='UVMap')
for li, u in enumerate(ruv): ru.data[li].uv = u
for p in rm.polygons: p.use_smooth = False
rack = bpy.data.objects.new('CargoRack', rm); bpy.context.collection.objects.link(rack); rm.materials.append(woodmat)

bpy.ops.object.select_all(action='DESELECT')
for o in (body, rack): o.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'GMC_Truck_Open.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                         bake_anim=False, use_mesh_modifiers=False, path_mode='STRIP')
json.dump({'source': SRC + 'GMC_Truck.fbx', 'bed_half_width': HALF, 'source_faces_kept': kept,
           'source_faces_clipped': clipped, 'source_faces_removed': removed,
           'rack_faces': len(rf), 'floor_top': FLOOR_TOP, 'cut_z': CUT_Z, 'cab_back_y': CAB_BACK},
          open(os.path.join(OUT, 'build_manifest.json'), 'w'), indent=2)

# ---------- Diagnostic renders (studio light, not game light) ----------
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
sc.world = bpy.data.worlds.new('W'); sc.world.color = (.35, .37, .4)
sc.render.resolution_x, sc.render.resolution_y = 1000, 640
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(30))); bpy.context.object.data.energy = 3.5
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 35
for name, loc, target in [('blender_rear_threequarter', (6, 10, 4.5), (0, .8, 1.5)),
                          ('blender_side', (11, .5, 2.6), (0, .3, 1.5)),
                          ('blender_rear_inside', (0, 9, 3.4), (0, 1, 1.6)),
                          ('blender_front_threequarter', (-6.5, -9, 4), (0, 0, 1.4))]:
    cam.location = loc; cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, name + '.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'GMC_Truck_OpenCargo.blend'))
print('BUILD_COMPLETE', HALF, kept, clipped, removed)
