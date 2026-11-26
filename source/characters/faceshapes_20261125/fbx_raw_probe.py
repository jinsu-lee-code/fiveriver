# FBX 원본 좌표계 조사: GlobalSettings, Model 변환, Geometry 정점 vs Blender 가져온 정점
import bpy, sys, json, array
import numpy as np
from io_scene_fbx import parse_fbx

fbx = sys.argv[sys.argv.index("--") + 1]
root, ver = parse_fbx.parse(fbx)

def find(el, name):
    return [c for c in el.elems if c.id == name]

def props70(el):
    out = {}
    for p in find(el, b"Properties70"):
        for pp in p.elems:
            k = pp.props[0].decode("utf-8", "replace")
            out[k] = [x if not isinstance(x, bytes) else x.decode("utf-8", "replace") for x in pp.props[4:]]
    return out

res = {"version": ver}
gs = find(root, b"GlobalSettings")
if gs:
    p = props70(gs[0])
    res["global"] = {k: p.get(k) for k in ["UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign", "UnitScaleFactor", "OriginalUnitScaleFactor"]}
objs = find(root, b"Objects")[0]
geoms = {}
for g in find(objs, b"Geometry"):
    name = g.props[1].decode("utf-8", "replace").split("\x00")[0]
    v = find(g, b"Vertices")
    if v:
        arr = np.array(v[0].props[0], dtype=np.float64).reshape(-1, 3)
        geoms[name] = arr
models = {}
for m in find(objs, b"Model"):
    name = m.props[1].decode("utf-8", "replace").split("\x00")[0]
    models[name] = {"type": m.props[2].decode(), "p": {k: v for k, v in props70(m).items() if k.startswith("Lcl") or k.startswith("Geometric") or k in ("PreRotation", "PostRotation", "RotationOffset", "ScalingOffset")}}
res["models"] = {k: v for k, v in models.items() if v["type"] == "Mesh" or "Rig" in k or k == "Armature"}
res["geoms"] = {k: {"n": len(a), "min": a.min(0).tolist(), "max": a.max(0).tolist(), "first": a[:3].tolist()} for k, a in geoms.items()}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
for o in bpy.data.objects:
    if o.type == "MESH":
        co = np.array([v.co[:] for v in o.data.vertices])
        res.setdefault("blender", {})[o.name] = {"n": len(co), "min": co.min(0).tolist(), "max": co.max(0).tolist(), "first": co[:3].tolist(),
                                                 "matrix_world": [list(r) for r in o.matrix_world],
                                                 "parent_matrix": [list(r) for r in o.parent.matrix_world] if o.parent else None}
print("@@JSON@@" + json.dumps(res, ensure_ascii=False))
