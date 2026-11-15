# Shell-broken Korean red pines for the Dabudong map (2026-11-15).
#
# Veterans of the August 1950 fighting: "not a whole tree or a whole rock was left; everything was cracked, torn
# off and smashed" (Maeil Shinmun, 2023-06-22). A shell-broken oak stood on Hill 328 until 2006.
#
# The standing pines of the map are cut, so the bark, the lean of the trunk and the scale match the trees beside
# them exactly. Nothing is sculpted new except the torn top of the break.
#
#   blender -b --factory-startup -P make_shelled_pine.py -- <Korean_Red_Pine.fbx> <out dir>
# Writes SM_ShelledPine_A/B/C.fbx:  A broken at chest height, long tear on one side
#                                   B broken halfway up, its top lying beside it (child object "FallenTop")
#                                   C a stump
import bpy, bmesh, sys, math, random
from mathutils import Vector, Matrix

src, out = sys.argv[sys.argv.index('--') + 1], sys.argv[sys.argv.index('--') + 2]
BARK, INNER = 'Korean_Red_Pine_Game', 'Splintered_Wood'


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    lod0 = next(o for o in bpy.data.objects if o.type == 'MESH' and 'LOD0' in o.name)
    for o in list(bpy.data.objects):
        if o is not lod0: bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.view_layer.objects.active = lod0
    lod0.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if INNER not in bpy.data.materials: bpy.data.materials.new(INNER)
    lod0.data.materials.append(bpy.data.materials[INNER])
    return lod0


def trunk_centre(bm, z, band=0.35):
    vs = [v.co for v in bm.verts if abs(v.co.z - z) < band]
    return sum(vs, Vector()) / len(vs) if vs else Vector((0, 0, z))


def cut(obj, height, tilt_deg, tilt_dir, keep_top, seed, tear):
    """Cuts the tree at 'height'. keep_top False: the standing part with a splintered top; True: the part above."""
    random.seed(seed)
    me = obj.data
    bm = bmesh.new(); bm.from_mesh(me)
    c = trunk_centre(bm, height)
    n = Matrix.Rotation(math.radians(tilt_deg), 3, Vector((math.cos(tilt_dir), math.sin(tilt_dir), 0)).cross(Vector((0, 0, 1)))) @ Vector((0, 0, 1))
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=c, plane_no=n, clear_outer=not keep_top, clear_inner=keep_top)
    if not keep_top:
        # only what is joined to the foot of the trunk stays: a tilted cut can pass under the edge of the crown
        # and leave a tuft of needles hanging in the air above the stump
        foot = min(bm.verts, key=lambda v: v.co.z)
        keep = {foot}; stack = [foot]
        while stack:
            v = stack.pop()
            for e in v.link_edges:
                o = e.other_vert(v)
                if o not in keep: keep.add(o); stack.append(o)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context='VERTS')
    # the open ring left by the cut
    ring_edges = [e for e in bm.edges if len(e.link_faces) == 1 and all(abs((v.co - c).dot(n)) < 0.02 for v in e.verts)]
    bmesh.ops.subdivide_edges(bm, edges=ring_edges, cuts=2)
    ring_edges = [e for e in bm.edges if len(e.link_faces) == 1 and all(abs((v.co - c).dot(n)) < 0.02 for v in e.verts)]
    ring = order_loop(ring_edges)
    if len(ring) < 6:
        bm.to_mesh(me); bm.free(); return
    uv = bm.loops.layers.uv.active
    centre = sum((v.co for v in ring), Vector()) / len(ring)
    up = n if not keep_top else -n
    radius = sum((v.co - centre).length for v in ring) / len(ring)
    # the bark tears unevenly: the ring itself is pulled up and down
    lift = []
    tear_at = random.randrange(len(ring))
    for i, v in enumerate(ring):
        d = min(abs(i - tear_at), len(ring) - abs(i - tear_at)) / max(1, len(ring) // 6)
        h = random.uniform(0.0, 0.16) + (tear * math.exp(-d * d) if not keep_top else 0.0)
        lift.append(h); v.co += up * h
    # splinters standing inside the bark, and the hollow where the heart wood pulled out
    inner = []
    for i, v in enumerate(ring):
        p = centre + (v.co - centre - up * lift[i]) * random.uniform(0.45, 0.7)
        p += up * (lift[i] * 0.6 + random.choice((0.02, 0.08, 0.2, 0.34, 0.5)) * random.uniform(0.5, 1.0) * (radius / 0.16))
        inner.append(bm.verts.new(p))
    core = bm.verts.new(centre - up * 0.06)
    new_faces = []
    for i in range(len(ring)):
        j = (i + 1) % len(ring)
        new_faces.append(bm.faces.new((ring[i], ring[j], inner[j], inner[i])))
        new_faces.append(bm.faces.new((inner[i], inner[j], core)))
    bm.normal_update()
    # face them outward from the break
    for f in new_faces:
        if f.normal.dot(up) < 0: f.normal_flip()
        f.material_index = 1; f.smooth = False
        for l in f.loops:
            rel = l.vert.co - centre
            ang = math.atan2(rel.dot(side(up, 1)), rel.dot(side(up, 0)))
            l[uv].uv = (ang / (2 * math.pi) * 1.5 + 0.5, rel.dot(up) * 1.6 + 0.3)   # grain runs along the trunk
    bm.to_mesh(me); bm.free()
    me.update()


def side(up, k):
    a = up.cross(Vector((1, 0, 0)))
    if a.length < 0.1: a = up.cross(Vector((0, 1, 0)))
    a.normalize()
    return a if k == 0 else up.cross(a).normalized()


def order_loop(edges):
    if not edges: return []
    nxt = {}
    for e in edges:
        a, b = e.verts
        nxt.setdefault(a, []).append(b); nxt.setdefault(b, []).append(a)
    start = edges[0].verts[0]; loop = [start]; prev = None; cur = start
    while True:
        cand = [v for v in nxt[cur] if v is not prev]
        if not cand: break
        prev, cur = cur, cand[0]
        if cur is start or cur in loop: break
        loop.append(cur)
    return loop


def export(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=out + '/' + name + '.fbx', use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', bake_anim=False, add_leaf_bones=False)
    print('wrote', name, [(o.name, len(o.data.vertices)) for o in objs])


def variant(name, height, tilt, direction, seed, tear, fallen):
    tree = load(); tree.name = name; tree.data.name = name
    objs = [tree]
    if fallen:
        top = tree.copy(); top.data = tree.data.copy(); top.name = 'FallenTop'; top.data.name = name + '_FallenTop'
        bpy.context.collection.objects.link(top)
        cut(top, height, tilt, direction, True, seed + 1, 0.0)
        # laid down away from the break: its cut end near the stump, the crown on the ground. The pivot is the
        # cut end, so the placement script can turn it to lie with the slope.
        bm = bmesh.new(); bm.from_mesh(top.data)
        c = trunk_centre(bm, height + 0.2)
        fall = Vector((math.cos(direction), math.sin(direction), 0))
        rot = Matrix.Rotation(math.radians(96), 4, fall.cross(Vector((0, 0, 1))))
        for v in bm.verts: v.co = rot @ (v.co - c)
        low = min(v.co.z for v in bm.verts)
        for v in bm.verts: v.co.z -= low * 0.22     # the crown is crushed into the ground a little, not balanced on its tips
        bm.to_mesh(top.data); bm.free()
        top.location = Vector((c.x, c.y, 0)) + fall * 2.2
        top.parent = tree
        objs.append(top)
    cut(tree, height, tilt, direction, False, seed, tear)
    export(objs, name)


variant('SM_ShelledPine_A', 1.35, 24, 0.6, 11, 0.75, False)
variant('SM_ShelledPine_B', 2.45, 14, 2.4, 23, 0.55, True)
variant('SM_ShelledPine_C', 0.62, 14, 4.1, 37, 0.25, False)
