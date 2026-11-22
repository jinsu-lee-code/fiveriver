# Hands for the KPA rifleman from the user's own hand model (2026-11-22, hand_fixed.blend: a right hand, 1.94 million
# triangles, 4K maps, no rig).
#
# The shape is the user's: it is only welded and reduced for the phone, its detail and colour baked from the full
# model into 2K maps. Joints are set in it (across and along from its creases and knuckles, in depth from rays through
# the skin), it is bound with automatic weights, mirrored for the other hand and placed on the game's rig as
# build_hands_mh.py does (its section 3 is run from that file).
#
#   blender -b --factory-startup --disable-autoexec -P build_hands_user.py -- <old SK_CH_KPA_Rifleman.fbx> <hand_fixed.blend> <unity_bones.txt> <out.bin> [<joints.json>]
#   HAND_PREVIEW=<prefix>: renders of the reduced hand at rest and bent, then stop
import bpy, bmesh, sys, os, struct, json
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

old_fbx, src_blend, bones_txt, out = [sys.argv[sys.argv.index('--') + i] for i in (1, 2, 3, 4)]
P = 'mixamorig:'
WRIST_REACH = 0.8
TRIS, TEX = 12000, 2048      # one hand
HAND_LENGTH = 0.185          # wrist crease to the middle finger's end, metres (a man of about 1.70 m)
STUB = 0.16                  # carried on up inside the sleeve (model units)
HEAD_SKIN = np.array((0.465, 0.368, 0.275))   # mean skin colour of the heads' maps (sRGB)
names = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=old_fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
OJ = {b.name: np.array(arm.matrix_world @ b.head_local) for b in arm.data.bones}
TJ = dict(OJ)
if len(sys.argv) > sys.argv.index('--') + 5:
    TJ.update({k: np.array(v) for k, v in json.load(open(sys.argv[sys.argv.index('--') + 5])).items()})
for o in list(bpy.data.objects): bpy.data.objects.remove(o)

# ---- 1. the model, turned into a left hand: x toward the thumb, y toward the palm, z along the fingers
with bpy.data.libraries.load(src_blend, link=False) as (src, dst): dst.objects = ['hand_R']
high = dst.objects[0]; bpy.context.collection.objects.link(high)
me = high.data
co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co); co[0::3] *= -1
me.vertices.foreach_set('co', co); me.flip_normals(); me.update()
def only(*objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[-1]

hand = high.copy(); hand.data = me.copy(); hand.name = 'hand'; bpy.context.collection.objects.link(hand)
bm = bmesh.new(); bm.from_mesh(hand.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bm.to_mesh(hand.data); bm.free()
while hand.data.uv_layers: hand.data.uv_layers.remove(hand.data.uv_layers[0])
hand.data.materials.clear()
d = hand.modifiers.new('d', 'DECIMATE'); d.ratio = TRIS / len(hand.data.polygons)
only(hand); bpy.ops.object.modifier_apply(modifier=d.name)
# (2026-11-22) Reduced this far, a few dozen slivers folded over backwards: culled, each was a pinhole showing the
# dark inside (a black dot on the bent wrist). A vertex of such a face is eased toward its neighbours until it lies flat.
def unfold():
    me_ = hand.data
    nv, nf = len(me_.vertices), len(me_.polygons)
    P_ = np.empty(nv * 3); me_.vertices.foreach_get('co', P_); P_ = P_.reshape(nv, 3)
    f = np.empty(nf * 3, np.int64); me_.loops.foreach_get('vertex_index', f); f = f.reshape(nf, 3)
    nb = [set() for _ in range(nv)]
    for t_ in f:
        for k in range(3): nb[t_[k]].update((t_[(k + 1) % 3], t_[(k + 2) % 3]))
    nb = [list(n_) for n_ in nb]; first = last = None
    for step in range(80):
        fn = np.cross(P_[f[:, 1]] - P_[f[:, 0]], P_[f[:, 2]] - P_[f[:, 0]])
        vn = np.zeros((nv, 3))
        for k in range(3): np.add.at(vn, f[:, k], fn)
        vn /= np.maximum(np.linalg.norm(vn, axis=1), 1e-15)[:, None]
        round_ = np.zeros((nv, 3))
        for k in range(3):
            for m in range(3):
                if m != k: np.add.at(round_, f[:, k], vn[f[:, m]])
        round_ /= np.maximum(np.linalg.norm(round_, axis=1), 1e-15)[:, None]
        fu = fn / np.maximum(np.linalg.norm(fn, axis=1), 1e-15)[:, None]
        bad = np.nonzero((fu * round_[f].mean(1)).sum(1) < 0.12)[0]
        last = len(bad)
        if first is None: first = last
        if not last: break
        move = set(f[bad].ravel())
        if step % 8 == 7:                       # stuck: its neighbours give a little too
            move |= {u for v in list(move) for u in nb[v]}
        new = P_.copy()
        for v in move: new[v] = P_[v] + (P_[nb[v]].mean(0) - P_[v]) * 0.5
        P_ = new
    me_.vertices.foreach_set('co', P_.ravel()); me_.update()
    print('HAND folded slivers: %d -> %d' % (first, last))
unfold()
for p in hand.data.polygons: p.use_smooth = True
print('HAND reduced: %d vertices, %d triangles' % (len(hand.data.vertices), len(hand.data.polygons)))

# ---- 2. the joints (model units): across and along as read from the renders; the depth between the two skins
bvh = BVHTree.FromPolygons([v.co[:] for v in hand.data.vertices], [tuple(p.vertices) for p in hand.data.polygons])
def depth(x, z, t):
    a = bvh.ray_cast(Vector((x, -1, z)), Vector((0, 1, 0)))[0]
    b = bvh.ray_cast(Vector((x, 1, z)), Vector((0, -1, 0)))[0]
    assert a is not None and b is not None, 'joint outside the hand: %.3f %.3f' % (x, z)
    return a.y + (b.y - a.y) * t
XZ = {'Index': ((0.080, 0.526), (0.117, 0.705), (0.132, 0.813), (0.147, 0.910)),
      'Middle': ((-0.025, 0.509), (-0.021, 0.731), (-0.022, 0.843), (-0.023, 0.968)),
      'Ring': ((-0.112, 0.489), (-0.143, 0.675), (-0.160, 0.792), (-0.174, 0.897)),
      'Pinky': ((-0.183, 0.431), (-0.251, 0.581), (-0.285, 0.652), (-0.313, 0.718))}
J = {'Thumb': [np.array(p) for p in ((0.095, -0.035, 0.185), (0.200, 0.040, 0.380), (0.247, 0.100, 0.500), (0.305, 0.147, 0.602))]}
for n, pts in XZ.items():
    J[n] = []
    for i, (x, z) in enumerate(pts):
        at_ = (x, z) if i < 3 else tuple(np.array((x, z)) * 0.75 + np.array(pts[2]) * 0.25)
        J[n].append(np.array((x, depth(at_[0], at_[1], 0.38 if i == 0 else 0.5), z)))
wrist = np.array((-0.012, -0.070, 0.170))
for n in names: print('JOINTS %-6s %s' % (n, ' '.join('(%.3f %.3f %.3f)' % tuple(p) for p in J[n])))

# ---- 3. bound (at the model's own size: heat weights fail on very small meshes)
ad = bpy.data.armatures.new('rig'); ar = bpy.data.objects.new('rig', ad); bpy.context.collection.objects.link(ar)
only(ar); bpy.ops.object.mode_set(mode='EDIT')
def bone(name, a, b, parent=None):
    e = ad.edit_bones.new(name); e.head = Vector(a.tolist()); e.tail = Vector(b.tolist()); e.parent = parent; return e
bh = bone('Hand', wrist, J['Middle'][0], bone('ForeArm', np.array((-0.015, -0.085, -0.05)), wrist))
order = ['ForeArm', 'Hand']
for n in names:
    par = bh
    for i in range(3): par = bone(n + str(i + 1), J[n][i], J[n][i + 1], par); order.append(n + str(i + 1))
bpy.ops.object.mode_set(mode='OBJECT')
only(hand, ar); bpy.ops.object.parent_set(type='ARMATURE_AUTO')

# ---- 4. unwrapped, and the detail and colour baked from the full model
only(hand); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004); bpy.ops.object.mode_set(mode='OBJECT')
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 4
b = sc.render.bake; b.use_selected_to_active = True; b.cage_extrusion = 0.006; b.max_ray_distance = 0.02; b.margin = 12
mat = bpy.data.materials.new('bake'); mat.use_nodes = True; hand.data.materials.append(mat)
target = mat.node_tree.nodes.new('ShaderNodeTexImage'); mat.node_tree.nodes.active = target
def bake(kind, name):
    im = bpy.data.images.new(name, TEX, TEX, alpha=False); im.colorspace_settings.name = 'Non-Color' if kind == 'NORMAL' else 'sRGB'
    target.image = im; only(high, hand); bpy.ops.object.bake(type=kind)
    px = np.empty(TEX * TEX * 4, np.float32); im.pixels.foreach_get(px); return im, px.reshape(TEX, TEX, 4)
nimg, npx = bake('NORMAL', 'Normal')
# (2026-11-22) where the bake's ray met the skin from behind the normal points into the surface and showed as a black
# dot on the wrist: those texels, two round them, are laid flat
bad = npx[..., 2] < 0.45
for _ in range(2):
    bad = bad | np.roll(bad, 1, 0) | np.roll(bad, -1, 0) | np.roll(bad, 1, 1) | np.roll(bad, -1, 1)
npx[bad, :3] = (0.5, 0.5, 1.0); nimg.pixels.foreach_set(npx.ravel())
nimg.filepath_raw = out + '_Normal.png'; nimg.file_format = 'PNG'; nimg.save()
# the colour as emission, so that neither the lighting nor the metalness comes into it
nt = high.data.materials[0].node_tree
em = nt.nodes.new('ShaderNodeEmission')
nt.links.new(next(n_ for n_ in nt.nodes if n_.type == 'TEX_IMAGE' and n_.image.name.startswith('hand_basecolor')).outputs['Color'], em.inputs['Color'])
nt.links.new(em.outputs[0], next(n_ for n_ in nt.nodes if n_.type == 'OUTPUT_MATERIAL').inputs['Surface'])
cimg, cpx = bake('EMIT', 'BaseColor')
# one man's skin: the median brought to the heads', the model's own variation (nails, knuckles, lines) kept
rgb = cpx[..., :3]; lit = rgb.sum(-1) > 0.05
gain = HEAD_SKIN / np.median(rgb[lit], axis=0)
print('COLOUR gain to the heads\' skin', gain.round(3))
cpx[..., :3] = np.where(lit[..., None], np.clip(rgb * gain, 0, 1), HEAD_SKIN)
cimg.pixels.foreach_set(cpx.astype(np.float32).ravel())
cimg.filepath_raw = out + '_BaseColor.png'; cimg.file_format = 'PNG'; cimg.save()
bpy.data.objects.remove(high)
# an unpainted spot of the maps (plain skin, flat normal), for the piece inside the sleeve
cells = lit[:TEX // 32 * 32, :TEX // 32 * 32].reshape(TEX // 32, 32, TEX // 32, 32).any((1, 3))
cy, cx = np.argwhere(~cells)[0]
PLAIN = Vector(((cx * 32 + 16) / TEX, (cy * 32 + 16) / TEX))
print('PLAIN spot', tuple(round(x, 3) for x in PLAIN), 'normal there', npx[cy * 32 + 16, cx * 32 + 16, :3].round(2))

# ---- 5. the closed end taken off and carried on up inside the sleeve
bm = bmesh.new(); bm.from_mesh(hand.data); bm.normal_update(); uv = bm.loops.layers.uv.active
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < 0.006 and f.normal.z < -0.7], context='FACES')
edge = [e for e in bm.edges if e.is_boundary]
ext = bmesh.ops.extrude_edge_only(bm, edges=edge)
new = [g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)]
for f in (g for g in ext['geom'] if isinstance(g, bmesh.types.BMFace)):
    f.smooth = True
    for l in f.loops: l[uv].uv = PLAIN
for v in new: v.co.z = -STUB
bmesh.ops.triangulate(bm, faces=bm.faces)
bm.to_mesh(hand.data); bm.free(); hand.data.update()
uvl = hand.data.uv_layers.active.data
gi = {g.index: g.name for g in hand.vertex_groups}
W = np.zeros((len(hand.data.vertices), 17))
for v in hand.data.vertices:
    for g in v.groups:
        if gi[g.group] in order: W[v.index, order.index(gi[g.group])] += g.weight
none = W.sum(1) < 1e-6
W[none, 0] = 1.0; W /= W.sum(1, keepdims=True)
print('WEIGHTS automatic: %d vertices, %d had none; %d triangles' % (len(W), none.sum(), len(hand.data.polygons)))

if os.environ.get('HAND_PREVIEW'):
    pre = os.environ['HAND_PREVIEW']
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bs = nodes['Principled BSDF']; bs.inputs['Roughness'].default_value = 0.65
    target.image = cimg; links.new(target.outputs['Color'], bs.inputs['Base Color'])
    t2 = nodes.new('ShaderNodeTexImage'); t2.image = nimg; nm = nodes.new('ShaderNodeNormalMap'); nm.uv_map = hand.data.uv_layers.active.name
    links.new(t2.outputs['Color'], nm.inputs['Color']); links.new(nm.outputs['Normal'], bs.inputs['Normal'])
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = 800; sc.view_settings.view_transform = 'Standard'
    wo = bpy.data.worlds.new('w'); wo.use_nodes = True; wo.node_tree.nodes['Background'].inputs[0].default_value = (0.22, 0.23, 0.25, 1); sc.world = wo
    sun = bpy.data.objects.new('s', bpy.data.lights.new('s', 'SUN')); sun.data.energy = 2.6; sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.15; sc.collection.objects.link(cam); sc.camera = cam
    ar.hide_render = True
    centre = Vector((0, 0, 0.45))
    for pose, deg, th in (('rest', (0, 0, 0), (0, 0, 0)), ('half', (35, 55, 30), (15, 20, 25)), ('fist', (75, 95, 60), (25, 35, 50))):
        for pb in ar.pose.bones: pb.matrix_basis = Matrix()
        bpy.context.view_layer.update()
        for n in names:
            toward = Vector((-1, 0.3, 0)) if n == 'Thumb' else Vector((0, 1, 0))
            axis = Vector((J[n][1] - J[n][0]).tolist()).cross(toward).normalized()
            for i, a_ in enumerate(th if n == 'Thumb' else deg):
                pb = ar.pose.bones[n + str(i + 1)]
                R = pb.bone.matrix_local.to_3x3()
                pb.rotation_mode = 'QUATERNION'
                pb.rotation_quaternion = Matrix.Rotation(np.radians(a_), 3, R.inverted() @ axis).to_quaternion()
        bpy.context.view_layer.update()
        for k, d_ in (('palm', (0, 1, 0)), ('back', (0, -1, 0)), ('thumb', (1, 0.2, 0.1)), ('quarter', (0.6, 0.7, 0.5))):
            d_ = Vector(d_).normalized(); cam.location = centre + d_ * 2
            cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sun.rotation_euler = cam.rotation_euler; sun.rotation_euler.rotate_axis('X', -0.5); sun.rotation_euler.rotate_axis('Y', 0.4)
            sc.render.filepath = '%s_%s_%s.png' % (pre, pose, k); bpy.ops.render.render(write_still=True)
    print('PREVIEW written', pre); raise SystemExit

# ---- 6. to metres about the wrist, then into the rig's bind pose: build_hands_mh.py's section 3
size = HAND_LENGTH / (0.9784 - wrist[2])
V = (np.array([v.co[:] for v in hand.data.vertices]) - wrist) * size
N = np.array([v.normal[:] for v in hand.data.vertices])
J = {n: [(p - wrist) * size for p in J[n]] for n in names}
wrist = np.zeros(3)
def uvmap(u): return (u[0], u[1])
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'build_hands_mh.py'), encoding='utf-8').read()
src = src[src.index("# ---- 3. into the rig's bind pose"):]
# (that script sized the hand by the rig's finger lengths, which were the last hand's; this one has its own size)
assert src.count('sc_ = lo_ / ln') == 1
exec(src.replace('sc_ = lo_ / ln', 'sc_ = 1.0'))
