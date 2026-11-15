# The knocked-out T-34-85, made from the five-wheel model itself (2026-11-15), so the wreck is the same tank:
# turret slewed and tipped off its ring, gun fallen, the hull settled to one side, one track broken over the
# front wheels. One mesh, in the old destroyed model's node frame.
#   blender -b --factory-startup -P make_wreck5.py -- <SM_VH_T34_85_V5.fbx> <split.json> <out dir>
import bpy, bmesh, sys, math, json, os
from mathutils import Vector, Matrix

src, info, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3)]
J = json.load(open(info)); FWD = J['fwd']; pivot = Vector(J['pivot'])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
P = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
frame = P['Hull'].matrix_world.copy()
for o in P.values():
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    m = o.matrix_world.copy(); o.parent = None; o.matrix_world = m
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def move(o, m): o.data.transform(m)
def about(point, m): return Matrix.Translation(point) @ m @ Matrix.Translation(-point)
# the gun has dropped onto the glacis, then the turret is thrown round and tipped
trunnion = pivot + Vector((0, FWD * 0.9, 0.3))
move(P['Gun'], about(trunnion, Matrix.Rotation(math.radians(-9 * FWD), 4, 'X')))
T = about(pivot, Matrix.Translation(Vector((0.10, -FWD * 0.14, 0.05))) @ Matrix.Rotation(math.radians(34), 4, 'Z') @ Matrix.Rotation(math.radians(7), 4, 'Y') @ Matrix.Rotation(math.radians(-4), 4, 'X'))
move(P['Turret'], T); move(P['Gun'], T)
# the left track is broken: its upper run over the front wheels is gone, the end hangs
tl = P['TrackL']; top = J['track_top']
bm = bmesh.new(); bm.from_mesh(tl.data)
def front(c): return FWD * c.y
gone = [f for f in bm.faces if f.calc_center_median().z > top - 0.085 and 0.2 < front(f.calc_center_median()) < 2.05]
bmesh.ops.delete(bm, geom=gone, context='FACES')
for v in bm.verts:                                   # what is left behind the break sags toward the wheels
    f_ = front(v.co)
    if v.co.z > top - 0.085 and -0.5 < f_ <= 0.2: v.co.z -= 0.16 * ((f_ + 0.5) / 0.7) ** 2
bm.to_mesh(tl.data); bm.free()
bpy.ops.object.select_all(action='DESELECT')
for o in P.values(): o.select_set(True)
bpy.context.view_layer.objects.active = P['Hull']
bpy.ops.object.join()
w = bpy.context.view_layer.objects.active; w.name = 'SM_VH_T34_85_V5_Destroyed'; w.data.name = w.name
# settled: down a little on the broken side
w.data.transform(Matrix.Translation(Vector((0, 0, -0.035))) @ Matrix.Rotation(math.radians(2.2), 4, 'Y'))
w.data.transform(frame.inverted()); w.matrix_world = frame
for p in w.data.polygons: p.use_smooth = True
print('WRECK faces', len(w.data.polygons), 'removed from the track', len(gone))
sun = bpy.data.lights.new('s', 'SUN'); so = bpy.data.objects.new('s', sun); bpy.context.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(40))
cam = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cam); bpy.context.collection.objects.link(co)
sc = bpy.context.scene; sc.camera = co; cam.lens = 50; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.show_cavity = True
sc.render.resolution_x = 1300; sc.render.resolution_y = 800
for k, ang in enumerate((35, 215)):
    a = math.radians(ang); e = math.radians(18); ctr = Vector((0, FWD * 0.5, 1.2))
    co.location = ctr + Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * 14
    co.rotation_euler = (ctr - co.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, 'wreck_%d.png' % k); bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='DESELECT'); w.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'SM_VH_T34_85_V5_Destroyed.fbx'), use_selection=True, mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False, path_mode='STRIP')
print('WROTE')
