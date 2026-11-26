# 입·눈 주변 경계 모서리(면이 하나뿐인 모서리) 확인: 입술/눈꺼풀이 실제로 갈라져 있는지
import bpy, sys, os, json
import numpy as np
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import fs_common as fc

tag = sys.argv[sys.argv.index("--") + 1]
boxes = json.loads(sys.argv[sys.argv.index("--") + 2])
fbx, meshes, arm = fc.load_target(tag)
o = fc.body_mesh(meshes)
me = o.data
co, W, mw = fc.arrays(o)
ecount = {}
for p in me.polygons:
    for k in p.edge_keys:
        ecount[k] = ecount.get(k, 0) + 1
res = {}
for nm, (x0, x1, z0, z1) in boxes.items():
    bd = []
    for (a, b), c in ecount.items():
        if c == 1:
            pa, pb = W[a], W[b]
            if x0 <= pa[0] <= x1 and z0 <= pa[2] <= z1 and pa[1] < -0.03:
                bd.append([round(float(v), 5) for v in (pa[0], pa[1], pa[2], pb[0], pb[1], pb[2])])
    # 같은 위치에 겹친 정점 쌍
    sel = np.where((W[:, 0] >= x0) & (W[:, 0] <= x1) & (W[:, 2] >= z0) & (W[:, 2] <= z1) & (W[:, 1] < -0.03))[0]
    q = np.round(W[sel] / 1e-5).astype(np.int64)
    _, cnt = np.unique(q, axis=0, return_counts=True)
    res[nm] = {"boundary_edges": len(bd), "sample": bd[:12], "verts": int(len(sel)), "coincident_groups": int((cnt > 1).sum())}
print("@@JSON@@" + json.dumps(res))
