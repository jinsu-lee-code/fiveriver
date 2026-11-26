# 헤드리스 조사: FBX 메시/재질/머리 웨이트 정점 수
# 사용: blender -b --factory-startup --python inspect_fbx.py -- <fbx>
import bpy, sys, json
from mathutils import Vector

fbx = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
out = {"fbx": fbx, "unit_scale": bpy.context.scene.unit_settings.scale_length, "objects": []}
for o in bpy.data.objects:
    d = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
         "loc": list(o.location), "rot": list(o.rotation_euler), "scale": list(o.scale)}
    if o.type == "MESH":
        me = o.data
        d["verts"] = len(me.vertices)
        d["polys"] = len(me.polygons)
        d["mats"] = [m.name if m else None for m in me.materials]
        d["uv"] = [u.name for u in me.uv_layers]
        d["shape_keys"] = [k.name for k in me.shape_keys.key_blocks] if me.shape_keys else []
        mw = o.matrix_world
        ws = [mw @ v.co for v in me.vertices]
        if ws:
            d["bbox_min"] = [min(p[i] for p in ws) for i in range(3)]
            d["bbox_max"] = [max(p[i] for p in ws) for i in range(3)]
        gi = {g.name: g.index for g in o.vertex_groups}
        d["n_groups"] = len(gi)
        head_names = [n for n in gi if "Head" in n or "Eye" in n or "Jaw" in n]
        d["head_groups"] = head_names
        for hn in head_names:
            idx = gi[hn]
            cnt = 0
            for v in me.vertices:
                for g in v.groups:
                    if g.group == idx and g.weight > 0.5:
                        cnt += 1
            d["cnt_" + hn] = cnt
        # 재질별 정점 수
        mc = {}
        for p in me.polygons:
            mc[p.material_index] = mc.get(p.material_index, 0) + 1
        d["polys_per_mat"] = {str(k): v for k, v in mc.items()}
    if o.type == "ARMATURE":
        bones = o.data.bones
        d["n_bones"] = len(bones)
        hb = [b for b in bones if "Head" in b.name or "Neck" in b.name or "Eye" in b.name or "Jaw" in b.name]
        d["head_bones"] = {b.name: [list(o.matrix_world @ b.head_local), list(o.matrix_world @ b.tail_local)] for b in hb}
    out["objects"].append(d)
for m in bpy.data.materials:
    texs = []
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                texs.append([n.image.name, n.image.filepath, list(n.image.size)])
    out.setdefault("materials", {})[m.name] = texs
print("@@JSON@@" + json.dumps(out, ensure_ascii=False))
