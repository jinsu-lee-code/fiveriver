# Lombardy poplars for the road in the Dabudong valley (2026-11-15).
#
# "A road lined with poplar trees ran north through the valley; a stream ran along the west side, closely
# paralleling the road" (the Bowling Alley, August 1950, after Appleman, South to the Naktong, North to the Yalu).
#
# The tree is built, not converted: trunk and steeply rising branches as tubes, the foliage as cards cut from the
# generated branch sheet, and two crossed planes of the generated whole-tree picture for the far LOD.
#
#   blender -b --factory-startup -P make_poplar.py -- <dir with branches.json / far.json> <out dir>
# Writes SM_Poplar_A.fbx and SM_Poplar_B.fbx, each holding <name>_LOD0, _LOD1 and _LOD2.
import bpy, bmesh, sys, json, math, random
from mathutils import Vector, Matrix

tex, out = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
BOXES = json.load(open(tex + '/branches.json'))
FAR = json.load(open(tex + '/far.json'))['far_aspect']
UP = Vector((0, 0, 1))


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def tube(bm, uv, path, radii, sides, v_scale, mat_index):
    """path: list of points; radii: per point. Returns nothing; adds a capped-less tube with wrapped UVs."""
    rings = []
    length = 0.0
    prev_dir = None
    for i, p in enumerate(path):
        d = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        a = d.cross(Vector((1, 0, 0)))
        if a.length < 0.2: a = d.cross(Vector((0, 1, 0)))
        a.normalize(); b = d.cross(a).normalized()
        if i: length += (p - path[i - 1]).length
        ring = [(bm.verts.new(p + (a * math.cos(2 * math.pi * k / sides) + b * math.sin(2 * math.pi * k / sides)) * radii[i]), k / sides, length * v_scale) for k in range(sides)]
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            quad = (rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k])
            f = bm.faces.new([q[0] for q in quad])
            f.material_index = mat_index; f.smooth = True
            us = [rings[i][k][1], rings[i][k][1] + 1 / sides, rings[i][k][1] + 1 / sides, rings[i][k][1]]
            vs = [rings[i][k][2], rings[i][k][2], rings[i + 1][k][2], rings[i + 1][k][2]]
            for l, u_, v_ in zip(f.loops, us, vs): l[uv].uv = (u_ * 2.0, v_)


def card(bm, uv, cards, foot, along, facing, height, box, mat_index):
    """One leafy twig: a quad whose twig foot sits at 'foot', growing along 'along', facing 'facing'."""
    along = along.normalized()
    side = along.cross(facing).normalized()
    width = height * box['aspect']
    fu = (box['foot_u'] - box['u0']) / (box['u1'] - box['u0'])        # where the twig leaves the card, 0..1 across
    o = foot - side * (width * fu)
    corners = [o, o + side * width, o + side * width + along * height, o + along * height]
    vs = [bm.verts.new(c) for c in corners]
    f = bm.faces.new(vs)
    f.material_index = mat_index; f.smooth = True
    for l, (u_, v_) in zip(f.loops, ((box['u0'], box['v0']), (box['u1'], box['v0']), (box['u1'], box['v1']), (box['u0'], box['v1']))):
        l[uv].uv = (u_, v_)
    cards.append(f)


def build(name, seed, H, lod):
    random.seed(seed)
    me = bpy.data.meshes.new(name); ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap')
    if lod == 2:
        me.materials.append(mat('M_Poplar_Far'))
        w = H * FAR * 1.02
        for ang in (0.0, math.pi / 2):
            s = Vector((math.cos(ang), math.sin(ang), 0)) * (w / 2)
            vs = [bm.verts.new(v) for v in (-s, s, s + UP * H, -s + UP * H)]
            f = bm.faces.new(vs)
            for l, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))): l[uv].uv = t
        bm.to_mesh(me); bm.free()
        return ob
    me.materials.append(mat('M_Poplar_Bark')); me.materials.append(mat('M_Poplar_Leaves'))
    # ---- trunk: a slight lean and wander, tapering to nothing at the tip
    lean = Vector((random.uniform(-0.25, 0.25), random.uniform(-0.25, 0.25), 0))
    def axis(t):
        return lean * (t * t) + Vector((math.sin(t * 5 + seed) * 0.05, math.cos(t * 4 + seed) * 0.05, 0)) * t + UP * (H * t)
    n = 16 if lod == 0 else 8
    tube(bm, uv, [axis(i / n) for i in range(n + 1)], [0.25 * (1 - i / n) ** 0.85 + 0.015 + (0.07 if i == 0 else 0) for i in range(n + 1)], 10 if lod == 0 else 6, 1 / 1.3, 0)
    # ---- branches: fastigiate, rising steeply and close to the trunk
    cards = []
    crown0 = 0.13                                   # the crown starts this far up the trunk
    count = 52
    for i in range(count):
        t = crown0 + (1 - crown0) * ((i + random.random()) / count) ** 0.9
        c = (t - crown0) / (1 - crown0)             # 0 at the bottom of the crown, 1 at the tip
        reach = 1.30 * math.sin(math.pi * (0.10 + 0.86 * c)) ** 0.75 * (H / 14.0) * random.uniform(0.8, 1.1)
        az = i * 2.39996 + random.uniform(-0.4, 0.4)
        outward = Vector((math.cos(az), math.sin(az), 0))
        tilt = math.radians(random.uniform(20, 31) * (1 - 0.35 * c))
        L = max(0.5, reach / math.sin(tilt))
        L = min(L, (1 - t) * H * 0.95 + 0.6)
        start = axis(t)
        pts = []
        for k in range(5):
            s = k / 4
            tl = tilt * (1 - 0.45 * s)             # curving back toward the vertical
            pts.append(start + (outward * math.sin(tl) + UP * math.cos(tl)) * (L * s) + outward * (0.25 * (1 - t)) * 0.2)
        if lod == 0:
            tube(bm, uv, pts, [0.045 * (1 - c * 0.5) * (1 - k / 4) + 0.008 for k in range(5)], 4, 1 / 0.8, 0)
        # ---- foliage on the branch
        per = (7 if lod == 0 else 4)
        for j in range(per):
            s = 0.18 + 0.82 * (j + random.random() * 0.8) / per
            k = min(3, int(s * 4)); p = pts[k].lerp(pts[k + 1], s * 4 - k)
            d = (pts[k + 1] - pts[k]).normalized()
            roll = random.uniform(0, 2 * math.pi)
            away = (outward * 0.9 + Vector((math.cos(roll), math.sin(roll), 0)) * 0.8).normalized()
            grow = (d * 0.75 + away * 0.45 + UP * 0.15).normalized()
            facing = grow.cross(Vector((math.cos(roll + 1.3), math.sin(roll + 1.3), 0.2))).normalized()
            hgt = random.uniform(0.95, 1.45) * (1.0 if lod == 0 else 1.45) * (H / 14.0) * (1 - 0.55 * c ** 3)   # smaller toward the tip
            box = random.choice(BOXES)
            card(bm, uv, cards, p, grow, facing, hgt, box, 1)
            if random.random() < (0.55 if lod == 0 else 0.25):
                card(bm, uv, cards, p, grow, grow.cross(facing).normalized(), hgt * random.uniform(0.85, 1.0), random.choice(BOXES), 1)
    # the spire and the shaded inside of the crown
    for i in range(12 if lod == 0 else 6):
        t = random.uniform(0.80, 0.99); az = random.uniform(0, 6.283)
        o = Vector((math.cos(az), math.sin(az), 0))
        # the crown closes to a point: the nearer the tip, the shorter and the more upright
        card(bm, uv, cards, axis(t) - UP * 0.2, (UP + o * 0.10).normalized(), o, (1.25 - 3.6 * (t - 0.80)) * (H / 14.0), random.choice(BOXES), 1)
    bm.normal_update()
    bm.to_mesh(me); bm.free()
    # ---- foliage normals point out of the crown, so it shades as one soft volume instead of as flat cards
    normals = []
    for poly in me.polygons:
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            if poly.material_index == 1:
                t = max(0.0, min(1.0, v.z / H)); a = axis(t)
                n_ = Vector((v.x - a.x, v.y - a.y, 0))
                n_ = (n_.normalized() if n_.length > 1e-4 else UP) * 0.6 + UP * 0.8
                normals.append(n_.normalized())
            else:
                normals.append(me.vertices[me.loops[li].vertex_index].normal.copy())   # bark: the smooth vertex normal
    me.normals_split_custom_set(normals)
    return ob


def make(name, seed, H):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = [build('%s_LOD%d' % (name, lod), seed, H, lod) for lod in (0, 1, 2)]
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=out + '/' + name + '.fbx', use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False)
    print('wrote', name, [(o.name, len(o.data.polygons)) for o in objs])


make('SM_Poplar_A', 1950, 14.0)
make('SM_Poplar_B', 815, 12.2)
