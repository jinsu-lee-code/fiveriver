# 원본 살피기: 병사 모델 · 소총 · 기존 클립의 뼈대와 치수를 출력한다.
# 실행: blender -b --factory-startup --python 00_inspect.py -- <fbx 경로> [<fbx 경로> ...]
import sys
import bpy
from mathutils import Vector

paths = sys.argv[sys.argv.index("--") + 1:]


def dump(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True, automatic_bone_orientation=False)
    print("=" * 100)
    print("FILE", path)
    scene = bpy.context.scene
    print("fps", scene.render.fps, "frames", scene.frame_start, scene.frame_end)
    for o in bpy.data.objects:
        dims = tuple(round(v, 4) for v in o.dimensions)
        print(" OBJ", o.type, repr(o.name), "parent", o.parent.name if o.parent else None,
              "loc", tuple(round(v, 4) for v in o.location), "rot", tuple(round(v, 3) for v in o.rotation_euler),
              "scale", tuple(round(v, 4) for v in o.scale), "dims", dims)
        if o.type == "MESH":
            me = o.data
            ws = [o.matrix_world @ v.co for v in me.vertices]
            mn = Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws)))
            mx = Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws)))
            print("   verts", len(me.vertices), "polys", len(me.polygons), "vgroups", len(o.vertex_groups),
                  "mats", [m.name if m else None for m in me.materials],
                  "world min", tuple(round(v, 4) for v in mn), "max", tuple(round(v, 4) for v in mx))
            print("   modifiers", [(m.type, getattr(m, "object", None) and m.object.name) for m in o.modifiers])
    for a in bpy.data.actions:
        print(" ACTION", a.name, tuple(a.frame_range))
    for o in bpy.data.objects:
        if o.type != "ARMATURE":
            continue
        print(" ARMATURE", o.name, "bones", len(o.data.bones))
        mw = o.matrix_world
        for b in o.data.bones:
            h = mw @ b.head_local
            t = mw @ b.tail_local
            print("   %-34s parent %-30s head (% .4f % .4f % .4f) tail (% .4f % .4f % .4f) roll_x (% .2f % .2f % .2f)" % (
                b.name, b.parent.name if b.parent else "-", h.x, h.y, h.z, t.x, t.y, t.z,
                *(mw.to_3x3() @ b.x_axis)))


for p in paths:
    dump(p)
