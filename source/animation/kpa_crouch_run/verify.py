"""Verify an edited clip against the Mixamo source.
blender -b --factory-startup -P verify.py -- SRC.fbx NEW.fbx
1) FBX tree diff (fbxio): everything identical except AnimationCurve KeyValueFloat arrays.
2) Fresh Blender import of both: armature/bone names, rest pose, frame range, loop closure,
   and bone world positions vs the FBX-space FK (unit/axis sanity)."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import fbxio, rig
from mathutils import Vector, Matrix

src, new = sys.argv[sys.argv.index('--') + 1:][:2]
A, B = fbxio.FbxFile(src), fbxio.FbxFile(new)
diffs = []


def cmp(a, b, path):
    if a.name != b.name or len(a.props) != len(b.props) or len(a.children) != len(b.children):
        diffs.append(('STRUCT', path + '/' + a.name)); return
    for i, (p, q) in enumerate(zip(a.props, b.props)):
        if p.t != q.t:
            diffs.append(('TYPE', path + '/' + a.name, i))
        elif p.v != q.v:
            diffs.append(('VALUE', path + '/' + a.name, i))
    for c, d in zip(a.children, b.children):
        cmp(c, d, path + '/' + a.name)


print('VERSION', A.version, B.version, 'footer same', A.footer == B.footer)
assert len(A.nodes) == len(B.nodes)
for a, b in zip(A.nodes, B.nodes):
    cmp(a, b, '')
kinds = {}
for d in diffs:
    kinds[(d[0], d[1])] = kinds.get((d[0], d[1]), 0) + 1
print('TREE DIFFS', kinds)
ok_tree = all(k[0] == 'VALUE' and k[1].endswith('AnimationCurve/KeyValueFloat') for k in kinds)
print('TREE OK (only curve values differ):', ok_tree)
_, ca = fbxio.anim_index(A); _, cb = fbxio.anim_index(B)
changed = sorted({k[0] + ':' + k[1] for k in ca if fbxio.curve_arrays(ca[k])[1].v != fbxio.curve_arrays(cb[k])[1].v})
print('CHANGED CHANNELS', len(changed), changed)
for k in cb:
    v = fbxio.curve_arrays(cb[k])[1].v
    if len(v) == 24 and any(abs(v[0] - v[23]) > 1e-4 for _ in [0]):
        print('LOOP NOT CLOSED', k, v[0], v[23])
kt = fbxio.curve_arrays(next(iter(cb.values())))[0].v
print('KEYTIME frames', [round(t / 46186158000 * 30, 3) for t in (kt[0], kt[-1])], 'n', len(kt))
for o in B.top('Objects').children:
    if o.name == 'AnimationStack':
        print('TAKE', fbxio.sval(o.props[1]))
gs = fbxio.props70(B.top('GlobalSettings'))
print('UNIT', gs['UnitScaleFactor'], 'UP', gs['UpAxis'], gs['UpAxisSign'], 'FRONT', gs['FrontAxis'], gs['FrontAxisSign'])


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, anim_offset=0)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    act = arm.animation_data.action
    sc = bpy.context.scene
    data = dict(objects=sorted((o.name, o.type) for o in bpy.data.objects), frame_range=tuple(act.frame_range),
                bones=[b.name for b in arm.data.bones],
                rest={b.name: (tuple(round(x, 4) for x in b.head_local), tuple(round(x, 4) for x in b.tail_local)) for b in arm.data.bones},
                obj=(tuple(round(x, 4) for x in arm.location), tuple(round(math.degrees(x), 3) for x in arm.rotation_euler), tuple(round(x, 5) for x in arm.scale)),
                pos={})
    for f in (0, 6, 12, 17, 23):
        sc.frame_set(f)
        bpy.context.view_layer.update()
        data['pos'][f] = {pb.name: (arm.matrix_world @ pb.head).copy() for pb in arm.pose.bones}
    return data


S, N = load(src), load(new)
print('OBJECTS', S['objects'] == N['objects'], N['objects'])
print('ARMATURE OBJ src', S['obj'], 'new', N['obj'])
print('FRAME RANGE src', S['frame_range'], 'new', N['frame_range'])
print('BONE NAMES same order', S['bones'] == N['bones'], len(N['bones']))
print('REST POSE identical', S['rest'] == N['rest'])
# FK prediction (FBX space cm, Y up, Z fwd) -> Blender world: (x, -z, y) * 0.01
Rn = rig.Rig(B)
maxerr = 0
for f in (0, 6, 12, 17, 23):
    G = Rn.fk(f)
    for n, p in N['pos'][f].items():
        t = G[n].translation
        pred = Vector((t.x, -t.z, t.y)) * 0.01
        maxerr = max(maxerr, (pred - p).length)
print('FK vs Blender import max error (m)', maxerr)
loop = max((N['pos'][0][n] - N['pos'][23][n]).length for n in N['pos'][0])
print('LOOP frame0 vs frame23 max bone distance (m)', loop)
for n in ('mixamorig:Hips', 'mixamorig:LeftFoot', 'mixamorig:RightFoot', 'mixamorig:LeftHand', 'mixamorig:RightHand', 'mixamorig:Head'):
    print('  f6', n, 'src', tuple(round(x, 3) for x in S['pos'][6][n]), 'new', tuple(round(x, 3) for x in N['pos'][6][n]))
unchanged = [n for n in N['pos'][0] if 'HandIndex' in n]
d = max(((N['pos'][6][n] - N['pos'][6]['mixamorig:LeftHand']) - (S['pos'][6][n] - S['pos'][6]['mixamorig:LeftHand'])).length for n in unchanged if n.startswith('mixamorig:Left'))
print('finger chain offset change vs hand (m, expected >0 only from hand rotation):', round(d, 4))
