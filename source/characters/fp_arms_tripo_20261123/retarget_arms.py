# Headless: blender -b --python retarget_arms.py -- <tripo soldier fbx> <bazooka_arms_rig.json> <out dir>
#
# First-person arms from the rigged Tripo soldier ("korean infantry private", Mixamo bone names). Both rigs name their
# bones alike, so every arm bone of the soldier is carried onto the same-named bone of the game's first-person rig at
# its rest pose: the bone's head to that bone's head, its direction to that bone's direction, its roll by the palm
# (the back of the hand defines one axis for the whole arm), the soldier's own thickness kept at one scale. The mesh
# follows its own (Tripo) skin weights. Only what hangs on the arm bones is kept: the rolled sleeve, the bare forearm,
# the hand. The soldier is in Blender's right-handed axes and the game rig in Unity's left-handed ones: the soldier is
# mirrored in X first, which also makes his right arm the one on the game's right.
# Out: arms_unity.json (BazookaArmSetup) + textures + previews.
import bpy, sys, os, json, math, shutil
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
fbx, rig_json, out = args[0], args[1], args[2]
os.makedirs(out, exist_ok=True)

def unit(v):
    n = np.linalg.norm(v); return v / n if n > 1e-12 else v
def orth(v, a):
    return unit(v - np.dot(v, a) * a)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')
# One level of Catmull-Clark on the rest shape before anything else (2026-11-23, quality pass): the scan's whole body is
# 9.8k quads, so the fingers were four-sided rods and the forearm showed its facets this close to the lens. Applying the
# subdivision carries the vertex groups (Tripo's skin weights) and the uv over, interpolated.
for m in list(body.modifiers):
    if m.type == 'ARMATURE': body.modifiers.remove(m)
sub = body.modifiers.new('sub', 'SUBSURF'); sub.levels = 1; sub.render_levels = 1; sub.uv_smooth = 'PRESERVE_CORNERS'; sub.quality = 3
bpy.context.view_layer.objects.active = body
for o in bpy.data.objects: o.select_set(o == body)
bpy.ops.object.modifier_apply(modifier='sub')
print('SUBDIV verts', len(body.data.vertices), 'faces', len(body.data.polygons))
M = np.array([-1.0, 1.0, 1.0])                                   # Blender -> Unity handedness
heads = {b.name: np.array(arm.matrix_world @ b.head_local) * M for b in arm.data.bones}
mw = body.matrix_world
P = np.array([list(mw @ v.co) for v in body.data.vertices]) * M
N = len(P)
groups = {g.index: g.name for g in body.vertex_groups}

rig = json.load(open(rig_json, encoding='utf-8'))
result = {'arms': []}
FING = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']
SLEEVE, FOREARM = 0.84, 0.9
# (2026-11-24, 사용자 지시 "바주카 잡는손 매끄럽게 다듬어봐 지금 m1들었을때 손과 차이감이 너무 커서")
# 손가락 · 손목 굵기를 M1 손의 비율에 맞춘다. M1 손(M1_HandCuffTrim, 씬의 M1_Anatomical_Grip_Hands)을 같은 방법으로 잰 값
# (source/unity_staging_20261124_hands/measure/measure_hands.py, 손 길이 = 손목 관절~가운뎃손가락 끝 마디를 따라 170.2 mm):
#   첫마디 지름 / 손 길이: 검지 21.4 mm -> 0.1258, 중지 22.1 -> 0.1297, 약지 19.9 -> 0.1169, 새끼 16.6 -> 0.0978
#   손목 너비 52.4 mm -> 0.3075, 손목 두께(왼손, 오른손은 잘린 자리라 제외) 41.7 / 175.0 -> 0.238
# 같은 방법으로 잰 Tripo 손(2026-11-23 판, 배율 1.25)은 첫마디 지름 28.5 / 28.5 / 29.3 / 29.6 mm (손 길이 198.7 mm: 비율
# 0.143 / 0.143 / 0.147 / 0.149, 새끼손가락이 검지만큼 굵었다), 손목 67.0 x 55.2 mm (0.337 / 0.278).
# 팔에 맞춘 뒤 이 비율을 재서 굵기만(길이는 그대로) 줄인다. 굵게 만들지는 않는다 (배율 0.6~1.0).
FINGER_RATIO = {'Index': 0.1258, 'Middle': 0.1297, 'Ring': 0.1169, 'Pinky': 0.0978}
WRIST_WIDTH_RATIO, WRIST_DEPTH_RATIO = 0.3075, 0.238

def _span_scale(side):
    e = next(a for a in rig['arms'] if a['name'].endswith(side))
    r = {b['name']: np.array(b['rest']).reshape(4, 4)[:3, 3] for b in e['bones']}
    p = 'mixamorig:' + side
    return np.linalg.norm(r[p + 'HandPinky1'] - r[p + 'HandIndex1']) / np.linalg.norm(heads[p + 'HandPinky1'] - heads[p + 'HandIndex1'])
SHARED_SCALE = (_span_scale('Left') + _span_scale('Right')) / 2
print('SCALE shared x%.3f (left x%.3f, right x%.3f)' % (SHARED_SCALE, _span_scale('Left'), _span_scale('Right')))
for side in ('Left', 'Right'):
    entry = next(a for a in rig['arms'] if a['name'].endswith(side))
    rest = {b['name']: np.array(b['rest']).reshape(4, 4) for b in entry['bones']}
    uh = {n: m[:3, 3] for n, m in rest.items()}
    pre = 'mixamorig:' + side
    chain = ['Shoulder', 'Arm', 'ForeArm', 'Hand'] + ['Hand%s%d' % (f, k) for f in FING for k in (1, 2, 3)]
    names = [pre + c for c in chain]
    def child(c):
        if c == 'Shoulder': return 'Arm'
        if c == 'Arm': return 'ForeArm'
        if c == 'ForeArm': return 'Hand'
        if c == 'Hand': return 'HandMiddle1'
        return c[:-1] + str(int(c[-1]) + 1)
    def palm(h):
        return unit(np.cross(h[pre + 'HandIndex1'] - h[pre + 'Hand'], h[pre + 'HandPinky1'] - h[pre + 'Hand']))
    n_t, n_u = palm(heads), palm(uh)
    def length(h, c): return np.linalg.norm(h[pre + child(c)] - h[pre + c])
    # thickness (and finger length) by the width of the hand: the game rig's arms are drawn out long, as first-person
    # arms usually are, and scaled by them the sleeve came out half again as thick as the old one
    def span(h): return np.linalg.norm(h[pre + 'HandPinky1'] - h[pre + 'HandIndex1'])
    # one scale for both hands: the game rig's two hands are 14 % apart in knuckle span, and scaled each to its own the
    # left hand came out visibly the bigger
    s = SHARED_SCALE
    finger_k = {f: 1.0 for f in FING}; wrist_w, wrist_d = 1.0, 1.0
    def build_T():
        T = {}
        for c in chain:
            yt = unit(heads[pre + child(c)] - heads[pre + c]); yu = unit(uh[pre + child(c)] - uh[pre + c])
            zt = orth(n_t, yt); zu = orth(n_u, yu)
            Ft = np.stack([np.cross(yt, zt), yt, zt], 1); Fu = np.stack([np.cross(yu, zu), yu, zu], 1)
            along = length(uh, c) / length(heads, c) if c in ('Arm', 'ForeArm', 'Hand') else s
            # the rolled sleeve and the forearm a little slimmer than the scan (it came out fuller than the old arm)
            thick = s * (SLEEVE if c in ('Shoulder', 'Arm') else FOREARM if c == 'ForeArm' else 1.0)
            tx = tz = thick
            if c == 'Hand': tx, tz = thick * wrist_w, thick * wrist_d          # across the palm, back to palm
            fname = next((f for f in FING if c.startswith('Hand' + f)), None)
            if fname: tx = tz = thick * finger_k[fname]
            L = Fu @ np.diag([tx, along, tz]) @ Ft.T
            T[pre + c] = np.hstack([L, (uh[pre + c] - L @ heads[pre + c]).reshape(3, 1)])
        return T
    T = build_T()
    for c in ('Arm', 'ForeArm', 'Hand', 'HandIndex1', 'HandThumb1'):
        print('BONE %s %s: thickness x%.2f' % (side, c, s))
    # weights on this arm's bones only
    W = np.zeros((N, len(names)))
    for v in body.data.vertices:
        for g in v.groups:
            nm = groups[g.group]
            if nm in names: W[v.index, names.index(nm)] = g.weight
    total = np.zeros(N)
    for v in body.data.vertices: total[v.index] = sum(g.weight for g in v.groups)
    share = W.sum(1) / np.maximum(total, 1e-9)
    # the shoulder bone carries the top of the sleeve; past the upper arm it is not wanted on screen
    keep_v = (share >= 0.5) & ((W[:, 1:].sum(1)) > 0.35 * np.maximum(total, 1e-9))
    W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)
    Ph = np.hstack([P, np.ones((N, 1))])
    def skin(T):
        Q = np.zeros_like(P)
        for b, nm in enumerate(names): Q += W[:, b:b + 1] * (Ph @ T[nm].T)
        return Q
    dom = np.argmax(W, 1); domw = W[np.arange(N), dom]
    def hand_measure(Q):
        """손 길이(마디를 따라) · 첫마디 지름 · 손목 너비/두께 (measure_hands.py 와 같은 방법, 게임 뼈대 기준 자세에서)"""
        H = lambda c: uh[pre + c]
        wrist, mid1 = H('Hand'), H('HandMiddle1'); ax = unit(mid1 - wrist)
        across = orth(H('HandIndex1') - H('HandPinky1'), ax); depth = np.cross(ax, across)
        d3 = unit(H('HandMiddle3') - H('HandMiddle2'))
        tip = Q[keep_v & (dom == names.index(pre + 'HandMiddle3')) & (domw >= 0.3)]
        hand_len = np.linalg.norm(mid1 - wrist) + np.linalg.norm(H('HandMiddle2') - mid1) + np.linalg.norm(H('HandMiddle3') - H('HandMiddle2')) + np.max((tip - H('HandMiddle3')) @ d3)
        diam = {}
        for f in FINGER_RATIO:
            a = H('Hand%s1' % f); d = unit(H('Hand%s2' % f) - a); Lb = np.linalg.norm(H('Hand%s2' % f) - a)
            v = Q[keep_v & (dom == names.index(pre + 'Hand%s1' % f)) & (domw >= 0.6)]; t = (v - a) @ d
            v = v[(t > 0.2 * Lb) & (t < 0.8 * Lb)]; t = (v - a) @ d
            diam[f] = 2 * np.median(np.linalg.norm((v - a) - np.outer(t, d), axis=1))
        wb = Q[keep_v & (np.abs((Q - wrist) @ ax) < 0.006)]
        wb = wb[np.linalg.norm((wb - wrist) - np.outer((wb - wrist) @ ax, ax), axis=1) < 0.06]
        return hand_len, diam, np.ptp(wb @ across), np.ptp(wb @ depth)
    Q = skin(T)
    for it in range(3):
        hl, diam, ww, wd = hand_measure(Q)
        print('MEASURE %s pass %d: hand %.1f mm, proximal %s mm, wrist %.1f x %.1f mm (ratios %s / %.3f %.3f)' % (side, it, hl * 1000,
              ' '.join('%s %.1f' % (f, diam[f] * 1000) for f in diam), ww * 1000, wd * 1000, ' '.join('%.3f' % (diam[f] / hl) for f in diam), ww / hl, wd / hl))
        if it == 2: break
        for f in FINGER_RATIO: finger_k[f] = float(np.clip(finger_k[f] * FINGER_RATIO[f] * hl / diam[f], 0.6, 1.0))
        wrist_w = float(np.clip(wrist_w * WRIST_WIDTH_RATIO * hl / ww, 0.6, 1.0)); wrist_d = float(np.clip(wrist_d * WRIST_DEPTH_RATIO * hl / wd, 0.6, 1.0))
        print('  -> finger thickness %s, wrist width x%.3f depth x%.3f' % (' '.join('%s x%.3f' % (f, finger_k[f]) for f in FINGER_RATIO), wrist_w, wrist_d))
        T = build_T(); Q = skin(T)
    # triangles whose corners are all kept, with uv split per corner
    me = body.data; me.calc_loop_triangles(); uvl = me.uv_layers.active.data
    key, src, uvs, tris = {}, [], [], []
    for t in me.loop_triangles:
        if not all(keep_v[me.loops[li].vertex_index] for li in t.loops): continue
        tri = []
        for li in t.loops:
            vi = me.loops[li].vertex_index; uv = uvl[li].uv; k = (vi, round(uv.x, 5), round(uv.y, 5))
            if k not in key: key[k] = len(src); src.append(vi); uvs.append((uv.x, uv.y))
            tri.append(key[k])
        tris.append(tri)
    src = np.array(src); tris = np.array(tris); V = Q[src]
    # winding: the same sense as the game's own arm mesh (mirroring turned it over)
    ref_v = np.array([v[:3] for v in entry['vertices']]); ref_t = np.array(entry['triangles']).reshape(-1, 3)
    def vol(P_, t_): return float(np.einsum('ij,ij->i', P_[t_[:, 0]], np.cross(P_[t_[:, 1]], P_[t_[:, 2]])).sum())
    if np.sign(vol(V - V.mean(0), tris)) != np.sign(vol(ref_v - ref_v.mean(0), ref_t)): tris = tris[:, ::-1].copy()
    fn = np.cross(V[tris[:, 1]] - V[tris[:, 0]], V[tris[:, 2]] - V[tris[:, 0]])
    vn = np.zeros_like(V)
    for c in range(3): np.add.at(vn, tris[:, c], fn)
    # welded normals across uv seams: sum per original vertex
    acc = np.zeros((N, 3)); np.add.at(acc, src, vn); vn = acc[src]
    vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-12)
    if np.mean(np.einsum('ij,ij->i', vn, V - V.mean(0))) * np.sign(vol(ref_v - ref_v.mean(0), ref_t)) < 0: vn = -vn
    wi, wv = [], []
    for vi in src:
        order = np.argsort(-W[vi])[:4]; w = W[vi, order]; w = w / max(w.sum(), 1e-9)
        wi += [int(o) for o in order]; wv += [float(x) for x in w]
    result['arms'].append({'name': entry['name'], 'bones': names, 'vertices': np.round(V, 6).reshape(-1).tolist(),
                           'normals': np.round(vn, 5).reshape(-1).tolist(), 'uv': np.round(np.array(uvs), 6).reshape(-1).tolist(),
                           'triangles': tris.reshape(-1).tolist(), 'boneIndex': wi, 'boneWeight': wv})
    print('ARM %s: kept %d of %d verts, %d tris, scale x%.3f, bounds %s %s' % (side, len(src), N, len(tris), s, np.round(V.min(0), 3), np.round(V.max(0), 3)))
json.dump(result, open(os.path.join(out, 'arms_unity.json'), 'w'))

# textures beside the FBX
fbm = os.path.splitext(fbx)[0] + '.fbm'
for f in os.listdir(fbm):
    low = f.lower()
    kind = 'BaseColor' if 'basecolor' in low else 'Normal' if 'normal' in low else 'Metallic' if 'metallic' in low else 'Roughness' if 'roughness' in low else None
    if kind: shutil.copyfile(os.path.join(fbm, f), os.path.join(out, 'T_soldier_' + kind + '.png')); print('TEX', f, '->', kind)

# preview: the new arms (textured) over the game's old arm meshes (grey), in the game's mesh space
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
mat = bpy.data.materials.new('soldier'); mat.use_nodes = True
img = bpy.data.images.load(os.path.join(out, 'T_soldier_BaseColor.png'))
tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img
mat.node_tree.links.new(tex.outputs[0], mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
grey = bpy.data.materials.new('old'); grey.use_nodes = True
grey.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.15, 0.15, 0.4, 1)
def add(name, verts, faces, m, uv=None):
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in verts], [], [tuple(int(i) for i in f) for f in faces]); me.update()
    for p in me.polygons: p.use_smooth = True
    if uv is not None:
        lay = me.uv_layers.new(name='UVMap')
        for li, l in enumerate(me.loops): lay.data[li].uv = uv[l.vertex_index]
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob); me.materials.append(m); return ob
for a in result['arms']:
    add(a['name'], np.array(a['vertices']).reshape(-1, 3), np.array(a['triangles']).reshape(-1, 3), mat, np.array(a['uv']).reshape(-1, 2))
for e in rig['arms']:
    ob = add('old_' + e['name'], [v[:3] for v in e['vertices']], np.array(e['triangles']).reshape(-1, 3), grey)
    ob.location.x += 0.0
scene = bpy.context.scene
engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
scene.render.resolution_x = 900; scene.render.resolution_y = 600
scene.world = bpy.data.worlds.new('w'); scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[1].default_value = 1.0
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sun.data.energy = 3.0; sun.rotation_euler = (0.9, 0.2, 0.6); scene.collection.objects.link(sun)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.2
allv = np.vstack([np.array(a['vertices']).reshape(-1, 3) for a in result['arms']]); c = Vector(allv.mean(0))
for vn, d in (('front', (0, 1, 0)), ('top', (0, 0, 1)), ('side', (1, 0, 0)), ('back', (0, -1, 0))):
    d = Vector(d); cam.location = c + d * 3
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(out, 'arms_' + vn + '.png'); bpy.ops.render.render(write_still=True)
