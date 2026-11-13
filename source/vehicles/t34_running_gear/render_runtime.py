# blender -b --factory-startup -P render_runtime.py -- <in.fbx> <gear json> <out_dir> <tag> <distance,distance,...> [views]
# Splits TrackL / TrackR exactly as TankRunningGear.cs does (rest / wheel discs / belt), turns the wheels and carries
# the belt round the loop for each driven distance, and renders close-ups of the track ends (workbench).
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim

a = sys.argv[sys.argv.index('--') + 1:]
SRC, JP, OUT, TAG = a[0], a[1], a[2], a[3]
DIST = [float(v) for v in a[4].split(',')]
ONLY = a[5].split(',') if len(a) > 5 else None
J = json.load(open(JP))
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
sc = bpy.context.scene
W = np.array(J['wheels']).reshape(-1, 4)

parts = {}
for name, sign in (('TrackL', -1.0), ('TrackR', 1.0)):
    o = bpy.data.objects[name]; me = o.data; me.calc_loop_triangles()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    M = np.array(o.matrix_world); w = co @ M[:3, :3].T + M[:3, 3]
    x, y, z = sign * -w[:, 0], w[:, 2], -w[:, 1]
    t = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', t); t = t.reshape(-1, 3)
    loop = np.array(J[name]['loop']).reshape(-1, 2); T = sim.table(loop); G = sim.grid(T)
    g, seam = sim.classify(J, T, G, x, y, z, t)
    used = np.unique(t[g == 7]); place = np.zeros((len(x), 3))
    s, al, ou = sim.project(T, G, z[used], y[used]); place[used] = np.c_[s, al, ou]
    parts[name] = dict(sign=sign, x=x, y=y, z=z, t=t, g=g, seam=seam, T=T, place=place, used=used)
    o.hide_render = True

def mesh_obj(label, X, Y, Z, sign, tris, col):
    bx, by, bz = -(sign * X), -Z, Y
    me = bpy.data.meshes.new(label)
    me.from_pydata(list(zip(bx.tolist(), by.tolist(), bz.tolist())), [], tris.tolist())
    me.validate(verbose=False)
    for p in me.polygons: p.use_smooth = False
    ob = bpy.data.objects.new(label, me); sc.collection.objects.link(ob); ob.color = col
    return ob

sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'OBJECT'; sh.show_cavity = True; sh.show_object_outline = True; sh.show_backface_culling = os.environ.get("CULL", "0") == "1"
sc.render.resolution_x, sc.render.resolution_y = 1000, 800
sc.world = bpy.data.worlds.new('W'); sc.world.color = (0.05, 0.05, 0.08)
for ob in sc.objects:
    if ob.type == 'MESH': ob.color = (0.35, 0.4, 0.35, 1)
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 45
VIEWS = {   # Blender: TrackL at +x, front at -y, up +z
    'L_front_obl': ((3.3, -4.2, 1.5), (1.25, -2.15, 0.62)),
    'L_front_side': ((4.2, -2.15, 0.75), (1.25, -2.15, 0.62)),
    'L_front_low': ((3.0, -3.6, 0.35), (1.25, -2.15, 0.55)),
    'L_rear_obl': ((3.3, 4.2, 1.5), (1.25, 2.25, 0.62)),
    'L_rear_side': ((4.2, 2.25, 0.75), (1.25, 2.25, 0.62)),
    'R_front_obl': ((-3.3, -4.2, 1.5), (-1.25, -2.15, 0.62)),
    'R_rear_obl': ((-3.3, 4.2, 1.5), (-1.25, 2.25, 0.62)),
    'L_side': ((9.5, 0.0, 0.9), (1.2, 0.0, 0.6)),
    # like the cinematic captures: a little low, three-quarter from the front-left / rear-left of the hull
    'L_cap_front': ((5.2, -4.6, 0.9), (1.2, -1.2, 0.55)),
    'L_cap_rear': ((5.2, 4.6, 0.9), (1.2, 1.2, 0.55)),
}
if ONLY: VIEWS = {k: v for k, v in VIEWS.items() if k in ONLY}
MODE = os.environ.get('MODE', '')
if MODE == 'belt':
    for ob in sc.objects:
        if ob.type == 'MESH': ob.hide_render = True
for d in DIST:
    made = []
    for name, P in parts.items():
        x, y, z, t, g, seam, sign = P['x'], P['y'], P['z'], P['t'], P['g'], P['seam'], P['sign']
        rest = np.r_[np.where(g == 0)[0], np.where(seam)[0]]
        if MODE != 'belt': made.append(mesh_obj(name + '_rest', x, y, z, sign, t[rest], (0.85, 0.2, 0.2, 1) if MODE == 'red' else (0.55, 0.55, 0.5, 1)))
        for k in range(6):
            wz, wy, r, roll = W[k]
            ang = d / roll   # TankRunningGear: AngleAxis(distance / radius, +x) in the track's space
            c, s_ = math.cos(ang), math.sin(ang)
            # rotation about +x (tank body): y' = y cos - z sin, z' = y sin + z cos (left-handed Unity: sign does not matter for shape)
            yy = wy + (y - wy) * c - (z - wz) * s_; zz = wz + (y - wy) * s_ + (z - wz) * c
            if MODE != 'belt': made.append(mesh_obj(name + '_wheel%d' % k, x, yy, zz, sign, t[g == k + 1], (0.62, 0.55, 0.4, 1)))
        mz, my = z.copy(), y.copy()
        u = P['used']; mz[u], my[u] = sim.move(P['T'], P['place'][u], (-d) % P['T']['L'])
        made.append(mesh_obj(name + '_belt', x, my, mz, sign, t[g == 7], (0.45, 0.6, 0.85, 1)))
    for vn, (loc, tgt) in VIEWS.items():
        cam.location = Vector(loc)
        cam.rotation_euler = (Vector(tgt) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, '%s_%s_d%.2f.png' % (TAG, vn, d))
        bpy.ops.render.render(write_still=True)
    for ob in made:
        m = ob.data; bpy.data.objects.remove(ob); bpy.data.meshes.remove(m)
print('RENDER_DONE')
