import bpy
from pathlib import Path
from mathutils import Vector
R=Path('C:/Users/mbc/Documents/FiveRivers_Dabudong_6000.6.8f1');D=R/'Docs/Art/CoverRepair20261106';A=R/'five_river/Assets/_Project/Art/MapRepair20261106'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.fbx(filepath=str(D/'CP_ScannedTwoModules_source.fbx'));parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in bpy.context.scene.objects:o.select_set(o in parts)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.name='ScannedSackStack_LOD0';objects=[o]
p=[o.matrix_world@v.co for v in o.data.vertices];print('SOURCE_WORLD_BOUNDS',[(min(v[i] for v in p),max(v[i] for v in p)) for i in range(3)])
for level,ratio in [(1,.60),(2,.25)]:
 p=o.copy();p.data=o.data.copy();p.name='ScannedSackStack_LOD'+str(level);bpy.context.collection.objects.link(p);bpy.context.view_layer.objects.active=p;mod=p.modifiers.new('ScannedSeamLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name);objects.append(p)
for obj in bpy.context.scene.objects:obj.select_set(obj in objects)
bpy.ops.wm.save_as_mainfile(filepath=str(D/'ScannedSackStack_20261106.blend'))
bpy.ops.export_scene.fbx(filepath=str(A/'ScannedSackStack_20261106.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
print('FINAL_STACK',[(o.name,len(o.data.vertices),sum(len(f.vertices)-2 for f in o.data.polygons)) for o in objects])
