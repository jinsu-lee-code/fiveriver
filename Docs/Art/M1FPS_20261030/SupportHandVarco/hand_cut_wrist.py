# Cut the Varco support hand at the wrist so the game's cloth sleeve covers the joint (2026-10-30).
# Input: SM_CH_ROK_SupportHand_M1.blend from hand_prep.py (canonical frame, Unity y = Blender z, wrist toward -Unity y).
import bpy, bmesh, sys
argv = sys.argv[sys.argv.index("--") + 1:]
out_fbx, cut = argv[0], float(argv[1])
ob = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bm = bmesh.new(); bm.from_mesh(ob.data)
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, cut), plane_no=(0, 0, 1), clear_inner=True)
bm.to_mesh(ob.data); bm.free()
zs = [v.co.z for v in ob.data.vertices]
print("CUT", cut, "verts", len(zs), "minZ", round(min(zs), 4))
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                         axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True, mesh_smooth_type='FACE', add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=out_fbx.replace(".fbx", "_Cut.blend"))
