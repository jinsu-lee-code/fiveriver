# 아트 모델 검사 (2026-11-10): 삼각형 예산 · 떠 있는 조각 · 구멍(열린 테두리) · 뒤집힌 면.
# usage: blender -b --factory-startup --python audit_models.py -- <out.json> <fbx> [<fbx> ...]
import bpy, bmesh, sys, json, os
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
OUT, FILES = args[0], args[1:]
BUDGET = {'SK_CH_ROK_Arms': (6000, 15000), 'SK_CH_': (10000, 20000), 'SM_CH_': (500, 15000),
          'SM_WP_M1Garand_Clip': (50, 4000), 'SM_WP_BazookaRocket': (500, 4000), 'SM_WP_': (8000, 15000),
          'SM_VH_': (30000, 60000), 'SM_PR_': (500, 4000)}

def budget(name):
    for k, v in BUDGET.items():
        if name.startswith(k): return v
    return None

def islands(bm):
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen: continue
        stack = [f]; comp = []
        while stack:
            x = stack.pop()
            if x.index in seen: continue
            seen.add(x.index); comp.append(x)
            for e in x.edges:
                for g in e.link_faces:
                    if g.index not in seen: stack.append(g)
        out.append(comp)
    return out

def bbox(points):
    return Vector([min(p[i] for p in points) for i in range(3)]), Vector([max(p[i] for p in points) for i in range(3)])

def gap(a, b):  # distance between two boxes (0 if touching / overlapping)
    d = [max(0.0, a[0][i] - b[1][i], b[0][i] - a[1][i]) for i in range(3)]
    return Vector(d).length

report = []
for path in FILES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as ex:
        report.append({'file': path, 'error': str(ex)}); continue
    name = os.path.splitext(os.path.basename(path))[0]
    entry = {'file': path, 'name': name, 'budget': budget(name), 'objects': []}
    total = 0
    allpts = []
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': continue
        bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
        tris = sum(len(f.verts) - 2 for f in bm.faces); total += tris
        pts = [v.co.copy() for v in bm.verts]; allpts += pts
        lo, hi = bbox(pts) if pts else (Vector(), Vector())
        size = (hi - lo).length or 1.0
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=size * 1e-5)
        bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
        isl = islands(bm)
        boxes = [bbox([v.co for f in c for v in f.verts]) for c in isl]
        sizes = [sum(len(f.verts) - 2 for f in c) for c in isl]
        main = max(range(len(isl)), key=lambda i: sizes[i]) if isl else -1
        floating = []
        for i, c in enumerate(isl):
            if i == main: continue
            g = min((gap(boxes[i], boxes[j]) for j in range(len(isl)) if j != i), default=0.0)
            if g > size * 0.01:
                cen = (boxes[i][0] + boxes[i][1]) / 2
                floating.append({'tris': sizes[i], 'gap': round(g, 4), 'center': [round(x, 3) for x in cen],
                                 'size': [round(x, 3) for x in (boxes[i][1] - boxes[i][0])]})
        bnd = [e for e in bm.edges if e.is_boundary]
        # flipped: manifold edge whose two faces walk it in the same direction
        flipped = 0
        for e in bm.edges:
            if len(e.link_faces) != 2: continue
            a, b = e.link_loops[0], e.link_loops[1]
            if a.vert == b.vert: flipped += 1
        nonman = sum(1 for e in bm.edges if len(e.link_faces) > 2)
        entry['objects'].append({'object': o.name, 'tris': tris, 'size_m': round(size, 3), 'slots': [m.name for m in o.data.materials if m],
                                 'islands': len(isl), 'floating': sorted(floating, key=lambda f: -f['gap'])[:12], 'floating_count': len(floating),
                                 'boundary_edges': len(bnd), 'flipped_edges': flipped, 'nonmanifold_edges': nonman})
        bm.free()
    entry['tris'] = total
    if allpts:
        lo, hi = bbox(allpts); entry['dims_m'] = [round(x, 3) for x in (hi - lo)]
    b = entry['budget']; entry['over_budget'] = bool(b and total > b[1])
    report.append(entry)
json.dump(report, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('AUDIT_DONE', len(report))
