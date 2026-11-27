# Headless: blender -b --factory-startup --python render_arms.py -- <before dir> <after dir> <out png prefix>
# 소매 걷기 전후 비교 렌더 (2026-11-27): arms_unity.json + T_soldier_BaseColor(_detail).png 로 팔을 만들어 옆 · 위 · 어깨 뒤에서
# 본 모습을 전(위 줄) / 후(아래 줄)로 찍는다. 화면 없이(Workbench, 텍스처 색 + 스튜디오 조명).
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
before, after, prefix = args[0], args[1], args[2]

def build(folder, tag):
    d = json.load(open(os.path.join(folder, 'arms_unity.json'), encoding='utf-8'))
    tex = os.path.join(folder, 'T_soldier_BaseColor_detail.png')
    if not os.path.exists(tex): tex = os.path.join(folder, 'T_soldier_BaseColor.png')
    img = bpy.data.images.load(tex)
    mat = bpy.data.materials.new('M_' + tag); mat.use_nodes = True
    nt = mat.node_tree; bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    ti = nt.nodes.new('ShaderNodeTexImage'); ti.image = img
    nt.links.new(ti.outputs['Color'], bsdf.inputs['Base Color'])
    objs = {}
    for arm in d['arms']:
        V = np.array(arm['vertices']).reshape(-1, 3); UV = np.array(arm['uv']).reshape(-1, 2); T = np.array(arm['triangles']).reshape(-1, 3)
        P = [(x, z, y) for x, y, z in V]            # Unity (y up, left-handed) -> Blender (z up)
        me = bpy.data.meshes.new(arm['name'] + tag); me.from_pydata(P, [], [tuple(t) for t in T]); me.update()
        uvl = me.uv_layers.new(name='uv')
        for poly in me.polygons:
            for li in poly.loop_indices:
                uvl.data[li].uv = UV[me.loops[li].vertex_index]
        for p in me.polygons: p.use_smooth = True
        ob = bpy.data.objects.new(arm['name'] + tag, me); ob.data.materials.append(mat)
        bpy.context.scene.collection.objects.link(ob)
        objs[arm['name']] = ob
    return objs

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_backface_culling = False
sh.show_cavity = True; sh.cavity_type = 'WORLD'
sc.render.resolution_x, sc.render.resolution_y = 640, 480
sc.view_settings.view_transform = 'Standard'
cam_data = bpy.data.cameras.new('cam'); cam_data.lens = 50; cam = bpy.data.objects.new('cam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
B, A = build(before, '_before'), build(after, '_after')

def look(ob_list, direction, up=(0, 0, 1), dist_scale=2.6):
    pts = np.array([list(o.matrix_world @ v.co) for o in ob_list for v in o.data.vertices])
    c = (pts.min(0) + pts.max(0)) / 2; size = np.linalg.norm(pts.max(0) - pts.min(0))
    d = Vector(direction).normalized()
    cam.location = Vector(c) + d * size * dist_scale * 0.55
    cam.rotation_euler = (Vector(c) - cam.location).to_track_quat('-Z', 'Y').to_euler()

shots = []
for name in ['SK_CH_ROK_Arm_Left', 'SK_CH_ROK_Arm_Right']:
    side = -1 if 'Left' in name else 1
    views = [('side', (side * 1.0, 0.15, 0.25)), ('top', (side * 0.25, 0.1, 1.0)), ('shoulder', (side * 0.25, -1.0, 0.75)), ('under', (-side * 0.8, 0.2, -0.6))]
    for tag, objs in (('before', B), ('after', A)):
        for o in list(B.values()) + list(A.values()): o.hide_render = True
        objs[name].hide_render = False
        for vname, dvec in views:
            look([objs[name]], dvec)
            fp = f"{prefix}_{name[-5:].strip('_')}_{tag}_{vname}.png"
            sc.render.filepath = fp; bpy.ops.render.render(write_still=True); shots.append(fp)
print('rendered', len(shots))
