# 새벽 트럭 짐칸 바닥 수정 (ART_REQUEST_20261110 R2, 2026-11-10)
# Input: the delivered GMC_Truck_Open.fbx (made by Docs/Art/DawnTruck_20261105/build_open_cargo_truck.py; that
# script's source scan is no longer on this PC, so this script edits its output instead).
# What looked like small black triangle holes on the cargo floor (TASKS "새벽 트럭 하차 연출" 남은 결함) are not
# holes: the source scan (GMC_Body) was cut at z = 1.40 but the new floor deck is at 1.38, so 336 scan triangles
# poke up to 2 cm through the deck and their unlit sides read as black wedges (Unity capture 11-10, raycast).
#   1. GMC_Body vertices inside the bed above the deck (out to the outer side panels, |x| < 1.42) are pushed down to
#      just under it (1.37). The cab back wall (y -0.50) is not part of the bed and is left alone.
#   2. The deck and its edge sills start 4 cm short of the cab (y -0.46 vs cab back -0.50), leaving a full-width
#      slit you can see the chassis through; their front faces are moved to the cab back.
# Object names, material slots, UVs and normals are kept; export uses the same settings as the build script.
# usage: blender -b --factory-startup --python fix_cargo_floor.py -- <in.fbx> <out.fbx> <render_dir>
import bpy, sys, os, json, math
from mathutils import Vector

src, dst, rdir = sys.argv[sys.argv.index('--') + 1:][:3]
FLOOR_TOP, DECK_BOTTOM = 1.38, 1.30
BED_FRONT, BED_REAR, CAB_BACK = -0.46, 3.36, -0.50
SINK_TO = 1.37
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
body = bpy.data.objects['GMC_Body']; rack = bpy.data.objects['CargoRack']
for o in (body, rack):
    assert all(abs(o.matrix_world[r][c] - (1.0 if r == c else 0.0)) < 1e-5 for r in range(4) for c in range(4)), (o.name, o.matrix_world)

def tris_above(me):
    n = 0
    for p in me.polygons:
        c = p.center
        if abs(c.x) < 1.25 and CAB_BACK + .02 < c.y < BED_REAR + .05 and max(me.vertices[i].co.z for i in p.vertices) > FLOOR_TOP + 1e-4:
            n += 1
    return n

log = {'body_polys_poking_before': tris_above(body.data)}
moved = 0
for v in body.data.vertices:
    c = v.co
    if abs(c.x) < 1.42 and BED_FRONT - .06 < c.y < BED_REAR + .06 and FLOOR_TOP - 1e-4 < c.z < 1.41:
        c.z = SINK_TO; moved += 1
body.data.update()
log['body_vertices_moved'] = moved
log['body_polys_poking_after'] = tris_above(body.data)

ext = 0
for v in rack.data.vertices:
    c = v.co
    if abs(c.y - BED_FRONT) < 1e-3 and DECK_BOTTOM - 1e-3 <= c.z <= 1.44 + 1e-3:
        c.y = CAB_BACK; ext += 1
rack.data.update()
log['deck_vertices_extended'] = ext
for o in (body, rack):
    log[o.name] = {'verts': len(o.data.vertices), 'polys': len(o.data.polygons), 'slots': [m.name for m in o.data.materials]}

bpy.ops.object.select_all(action='DESELECT')
for o in (body, rack): o.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
                         use_mesh_modifiers=False, path_mode='STRIP')
os.makedirs(rdir, exist_ok=True)
json.dump(log, open(os.path.join(rdir, 'fix_log.json'), 'w'), indent=2)

# before/after look (backface culling like Unity, scan = red, rack = blue)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'
sc.display.shading.show_backface_culling = True
sc.world = bpy.data.worlds.new('W'); sc.world.color = (1, 0, 1)
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
body.color = (0.85, 0.35, 0.3, 1); rack.color = (0.35, 0.5, 0.85, 1)
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 28
for name, loc, tgt in [('after_rear_left', (0.6, 4.6, 2.9), (-1.15, 1.6, 1.4)), ('after_rear_right', (-0.6, 4.6, 2.9), (1.15, 1.6, 1.4)),
                       ('after_low_along_left', (-0.4, 3.6, 1.7), (-0.9, -0.4, 1.38)), ('after_front_gap', (0, 1.2, 2.2), (0, -0.55, 1.3))]:
    cam.location = loc; cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(rdir, name + '.png'); bpy.ops.render.render(write_still=True)
print('FIX_DONE', json.dumps(log))
