# The generated five-road-wheel T-34-85 cut into the five parts the game uses (2026-11-15):
# Hull, TrackL, TrackR, Turret, Turret/Gun, with the old model's node names and node transforms.
#
# The first generated tank (and the delivered model before it) had four road wheels a side; a T-34 has five. This
# one is taken whole, running gear included, at one uniform scale (its length over the tracks = the old model's
# 5.18 m), so nothing is stretched. The turret's pivot moves to this model's own turret ring.
#
#   blender -b --factory-startup -P split_tank5.py -- <new.glb> <old SM_VH_T34_85.fbx> <out dir>
import bpy, bmesh, sys, math, os, json
import numpy as np
from mathutils import Vector, Matrix

new_glb, old_fbx, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3)]
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---- the old model: node transforms, which way is forward, which side is 'L'
bpy.ops.import_scene.fbx(filepath=old_fbx)
old = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
def wb(o):
    p = np.array([o.matrix_world @ v.co for v in o.data.vertices]); return p.min(0), p.max(0)
gun_lo, gun_hi = wb(old['Gun'])
FWD = -1.0 if (gun_lo[1] + gun_hi[1]) < 0 else 1.0
L_SIGN = np.sign(sum(wb(old['TrackL'])).tolist()[0])          # the sign of x on the old TrackL's side
old_matrix = {n: o.matrix_world.copy() for n, o in old.items()}
track_len = max(wb(old['TrackL'])[1][1], wb(old['TrackR'])[1][1]) - min(wb(old['TrackL'])[0][1], wb(old['TrackR'])[0][1])
print('OLD fwd', FWD, 'L side x sign', L_SIGN, 'track length %.2f' % track_len)
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)

# ---- the new model: welded, turned, scaled, on the ground
bpy.ops.import_scene.gltf(filepath=new_glb)
tank = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
bpy.context.view_layer.objects.active = tank; tank.select_set(True); tank.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = tank.data
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6); bm.to_mesh(me); bm.free()
for node in me.materials[0].node_tree.nodes:
    if node.type == 'TEX_IMAGE' and node.image:
        links = node.outputs[0].links
        name = 'BaseColor' if any(l.to_socket.name == 'Base Color' for l in links) else 'Normal' if any(l.to_node.type == 'NORMAL_MAP' for l in links) else 'ORM'
        node.image.filepath_raw = os.path.join(out, 'T_VH_T34_V5_%s.png' % name); node.image.file_format = 'PNG'; node.image.save()
V = np.array([v.co for v in me.vertices])
lo, hi = V[:, 1].min(), V[:, 1].max()
def width(a, b):
    s = V[(V[:, 1] >= a) & (V[:, 1] <= b)]; return np.ptp(s[:, 0]) if len(s) else 0
gun_at_hi = width(hi - 0.08 * (hi - lo), hi) < width(lo, lo + 0.08 * (hi - lo))
if (1.0 if gun_at_hi else -1.0) != FWD: me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
V = np.array([v.co for v in me.vertices])
zmin, zmax = V[:, 2].min(), V[:, 2].max()
low = V[V[:, 2] < zmin + 0.3 * (zmax - zmin)]
s = track_len / np.ptp(low[:, 1])
me.transform(Matrix.Scale(s, 4) @ Matrix.Translation(Vector((-(low[:, 0].max() + low[:, 0].min()) / 2, -(low[:, 1].max() + low[:, 1].min()) / 2, -zmin))))
V = np.array([v.co for v in me.vertices])
print('NEW scale %.3f size' % s, np.ptp(V, 0).round(2))

# ---- where the parts end
bottom = V[V[:, 2] < 0.10]                                    # the belt's lower run
belt_in = np.percentile(np.abs(bottom[:, 0]), 1); belt_out = np.abs(bottom[:, 0]).max()
xin = belt_in - 0.03
side = V[(np.abs(V[:, 0]) > belt_in) & (np.abs(V[:, 0]) < belt_out + 0.01) & (np.abs(V[:, 1]) < 1.6)]
h, e = np.histogram(side[:, 2], bins=np.arange(0, 1.8, 0.01))
# the belt's upper run is the highest dense layer of the running gear; above it comes a gap, then the fender
dense = np.where(h > 0.25 * np.percentile(h, 95))[0]
gaps = np.where(np.diff(dense) > 2)[0]
track_top = e[dense[gaps[0]] + 1] if len(gaps) else e[dense[-1] + 1]
ztop = track_top + 0.015
def width_z(a, b, around=None):
    s_ = V[(V[:, 2] >= a) & (V[:, 2] < b)]
    return np.ptp(s_[:, 0]) if len(s_) else 0.0
z = ztop + 0.25; ring = None; full = width_z(ztop + 0.05, ztop + 0.12)
while z < V[:, 2].max():
    mid = V[(V[:, 2] >= z) & (V[:, 2] < z + 0.03) & (np.abs(V[:, 1]) < 1.2)]
    if len(mid) and np.ptp(mid[:, 0]) < 0.68 * full: ring = z + 0.01; break
    z += 0.01
print('BELT x %.3f..%.3f  track top %.3f  ring %.3f  hull width %.2f' % (belt_in, belt_out, track_top, ring, full))
up = V[(V[:, 2] > ring + 0.15) & (V[:, 2] < ring + 0.45)]
# the turret's footprint just above the ring (without the gun, which is narrow)
rows = []
for y in np.arange(up[:, 1].min(), up[:, 1].max(), 0.04):
    r = up[(up[:, 1] >= y) & (up[:, 1] < y + 0.04)]
    if len(r) and np.ptp(r[:, 0]) > 0.75: rows.append(y)
ty0, ty1 = min(rows), max(rows) + 0.04
mid_ = up[np.abs(up[:, 1] - (ty0 + ty1) / 2) < 0.4]
pivot = Vector((float((mid_[:, 0].max() + mid_[:, 0].min()) / 2), (ty0 + ty1) / 2, ring))   # the middle of the turret, not of its vertices
front = ty0 if FWD < 0 else ty1
print('TURRET y %.2f..%.2f pivot' % (ty0, ty1), [round(v, 2) for v in pivot], 'gun from y %.2f' % front)

def take(name, keep):
    o = tank.copy(); o.data = tank.data.copy(); o.name = name; o.data.name = name
    bpy.context.collection.objects.link(o)
    b = bmesh.new(); b.from_mesh(o.data)
    bmesh.ops.delete(b, geom=[f for f in b.faces if not keep(f.calc_center_median())], context='FACES')
    b.to_mesh(o.data); b.free(); return o
def cut(o, co, no, fill=True):
    b = bmesh.new(); b.from_mesh(o.data)
    r = bmesh.ops.bisect_plane(b, geom=b.verts[:] + b.edges[:] + b.faces[:], plane_co=co, plane_no=no, clear_inner=True)
    edges = [x for x in r['geom_cut'] if isinstance(x, bmesh.types.BMEdge)]
    if fill and edges:
        try: bmesh.ops.holes_fill(b, edges=edges, sides=0)
        except Exception as ex: print('fill skipped', ex)
    bmesh.ops.triangulate(b, faces=b.faces[:])
    b.to_mesh(o.data); b.free()
def loose(o, least):
    b = bmesh.new(); b.from_mesh(o.data); seen = set(); kill = []
    for f in b.faces:
        if f in seen: continue
        comp = [f]; seen.add(f); i = 0
        while i < len(comp):
            for ed in comp[i].edges:
                for g in ed.link_faces:
                    if g not in seen: seen.add(g); comp.append(g)
            i += 1
        if len(comp) < least: kill += comp
    bmesh.ops.delete(b, geom=kill, context='FACES'); b.to_mesh(o.data); b.free(); return len(kill)

def in_gear(c): return abs(c.x) > xin and c.z < ztop
def in_column(c): return c.z > ring and (ty0 - 0.1 < c.y < ty1 + 0.1 or FWD * (c.y - front) > 0) and abs(c.x - pivot.x) < 1.25
trackL = take('TrackL', lambda c: in_gear(c) and np.sign(c.x) == L_SIGN)
trackR = take('TrackR', lambda c: in_gear(c) and np.sign(c.x) == -L_SIGN)
top = take('TurretAll', lambda c: c.z > ring - 0.25 and in_column(Vector((c.x, c.y, ring + 0.01))))
cut(top, Vector((0, 0, ring)), Vector((0, 0, 1)))
gun = top.copy(); gun.data = top.data.copy(); gun.name = 'Gun'; gun.data.name = 'Gun'; bpy.context.collection.objects.link(gun)
cut(gun, Vector((0, front, 0)), Vector((0, FWD, 0)))
turret = top; turret.name = 'Turret'; turret.data.name = 'Turret'
cut(turret, Vector((0, front, 0)), Vector((0, -FWD, 0)))
hull = take('Hull', lambda c: not in_gear(c) and not in_column(c))
print('LOOSE', loose(hull, 150), loose(turret, 150), loose(gun, 60), loose(trackL, 40), loose(trackR, 40))
bpy.data.objects.remove(tank, do_unlink=True)

# ---- node transforms: the old ones; the turret (and the gun under it) stand on this model's ring
def frame(o, m):
    o.data.transform(m.inverted()); o.matrix_world = m
    for p in o.data.polygons: p.use_smooth = True
mt = old_matrix['Turret'].copy(); mt.translation = pivot
mg = old_matrix['Gun'].copy(); mg.translation = pivot + (old_matrix['Gun'].translation - old_matrix['Turret'].translation)
for o, m in ((hull, old_matrix['Hull']), (trackL, old_matrix['TrackL']), (trackR, old_matrix['TrackR']), (turret, mt), (gun, mg)): frame(o, m)
gun.parent = turret; gun.matrix_world = mg
for o in (hull, trackL, trackR, turret, gun):
    a, b_ = wb(o); print('%-7s faces %6d  %s .. %s' % (o.name, len(o.data.polygons), a.round(2), b_.round(2)))
gl, gh = wb(gun)
json.dump(dict(pivot=list(pivot), muzzle=[float((gl[0] + gh[0]) / 2), float(gl[1] if FWD < 0 else gh[1]), float((gl[2] + gh[2]) / 2)], fwd=FWD,
               belt_in=float(belt_in), belt_out=float(belt_out), track_top=float(track_top)), open(os.path.join(out, 'split.json'), 'w'))

sun = bpy.data.lights.new('s', 'SUN'); so = bpy.data.objects.new('s', sun); bpy.context.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(40))
cam = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cam); bpy.context.collection.objects.link(co)
sc = bpy.context.scene; sc.camera = co; cam.lens = 50
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'; sc.display.shading.show_cavity = True
sc.render.resolution_x = 1300; sc.render.resolution_y = 800
gun.location.z += 0.0
for k, (ang, el, sep) in enumerate(((35, 18, 0), (215, 25, 0), (35, 18, 1), (270, 6, 0))):
    turret.location.z = mt.translation.z + (0.9 if sep else 0); trackL.location.x = old_matrix['TrackL'].translation.x + (L_SIGN * 0.8 if sep else 0); trackR.location.x = old_matrix['TrackR'].translation.x - (L_SIGN * 0.8 if sep else 0)
    a = math.radians(ang); e_ = math.radians(el); ctr = Vector((0, FWD * 0.5, 1.2))
    co.location = ctr + Vector((math.cos(a) * math.cos(e_), math.sin(a) * math.cos(e_), math.sin(e_))) * 14
    co.rotation_euler = (ctr - co.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, 'check5_%d.png' % k); bpy.ops.render.render(write_still=True)
turret.location.z = mt.translation.z; trackL.location.x = old_matrix['TrackL'].translation.x; trackR.location.x = old_matrix['TrackR'].translation.x
bpy.ops.object.select_all(action='DESELECT')
for o in (hull, trackL, trackR, turret, gun): o.select_set(True)
bpy.context.view_layer.objects.active = hull
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'SM_VH_T34_85_V5.fbx'), use_selection=True, mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False, path_mode='STRIP')
print('WROTE')
