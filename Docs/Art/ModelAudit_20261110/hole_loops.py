# 열린 테두리를 고리로 묶어 크기를 잰다(작은 고리 = 구멍 후보, 큰 고리 = 손목 · 노즐 같은 의도된 열린 곳).
# usage: blender -b --factory-startup --python hole_loops.py -- <out.json> <fbx> [<fbx> ...]
import bpy, bmesh, sys, json, os
args = sys.argv[sys.argv.index('--') + 1:]
OUT, FILES = args[0], args[1:]
res = {}
for path in FILES:
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=path)
    name = os.path.splitext(os.path.basename(path))[0]; res[name] = {}
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': continue
        bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
        size = max(o.dimensions) or 1
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=size * 1e-5)
        bnd = [e for e in bm.edges if e.is_boundary]
        adj = {}
        for e in bnd:
            for v in e.verts: adj.setdefault(v, []).append(e)
        seen = set(); loops = []
        for e in bnd:
            if e in seen: continue
            st = [e]; comp = []
            while st:
                x = st.pop()
                if x in seen: continue
                seen.add(x); comp.append(x)
                for v in x.verts: st.extend(y for y in adj[v] if y not in seen)
            per = sum(x.calc_length() for x in comp)
            loops.append((len(comp), round(per / size, 4)))
        loops.sort(key=lambda l: -l[1])
        small = [l for l in loops if l[1] < 0.02]
        res[name][o.name] = {'loops': len(loops), 'largest(rel perim)': loops[:5], 'small_loops(<2% of size)': len(small),
                             'small_edges': sum(l[0] for l in small)}
        bm.free()
json.dump(res, open(OUT, 'w'), indent=1); print('LOOPS_DONE')
