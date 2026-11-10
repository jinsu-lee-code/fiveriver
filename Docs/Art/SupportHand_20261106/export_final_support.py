"""Export only the editable final hand in the existing Unity support-root frame."""
import bpy, sys
from pathlib import Path
from mathutils import Matrix
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
if len(args)!=1:
    raise RuntimeError('Pass one explicit output directory after --')
output=Path(args[0]).resolve(); output.mkdir(parents=True,exist_ok=True)
source=Path(__file__).with_name('M1_LeftSupport_Editable_20261106.blend')
bpy.ops.wm.open_mainfile(filepath=str(source))
hand=next(o for o in bpy.data.objects if o.get('unity_export_mesh'))
for image in bpy.data.images:
    if image.name=='SupportSkin_Atlas_PACKED' and image.packed_file:
        data=bytes(image.packed_file.data)
        if not data.startswith(b'\x89PNG'):
            raise RuntimeError('Packed skin atlas is not PNG')
        target=output/'T_CH_ROK_SupportHand_Visible_BaseColor.png'
        target.write_bytes(data); image.filepath=str(target)
# Mesh data is already the source-local data used by the live SupportHand_M1.
# Its source display transform places it beside the actual game rifle/cuff.
hand.matrix_world=Matrix.Identity(4)
hand.name=hand.get('unity_export_name','SM_CH_ROK_SupportHand_M1')
bpy.ops.object.select_all(action='DESELECT'); hand.select_set(True)
bpy.context.view_layer.objects.active=hand
bpy.ops.export_scene.fbx(filepath=str(output/'SM_CH_ROK_SupportHand_VisiblePolished.fbx'),
    use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True,
    mesh_smooth_type='FACE',add_leaf_bones=False)
print('Exported final hand and packed atlas to',output)
