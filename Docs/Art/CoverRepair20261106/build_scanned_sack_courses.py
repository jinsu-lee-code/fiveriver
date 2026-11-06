import bpy,bmesh,math
from pathlib import Path
from mathutils import Vector,Matrix
R=Path('C:/Users/mbc/Documents/FiveRivers_Dabudong_6000.6.8f1');A=R/'five_river/Assets/_Project/Art/MapRepair20261106';Q=Path('C:/Users/mbc/.codex/visualizations/2026/11/02/01a199af-6dde-73d0-a46f-1508e6f64f5d/Expanded_20261106/SandbagQuality');Q.mkdir(exist_ok=True,parents=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(R/'five_river/Assets/_Project/Art/Models/Props/Sandbag/SM_PR_SandbagWall.fbx'));o=next(o for o in bpy.context.scene.objects if o.type=='MESH');bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
parts=[]
# Keep the original scan's bottom, staggered central bags and exposed top closures.
for index,(lo,hi) in enumerate([(0,.35),(.31,.68),(.02,.37),(.31,.68),(.65,1.01)]):
 p=o.copy();p.data=o.data.copy();p.name='ScannedCourse_'+str(index);bpy.context.collection.objects.link(p)
 bm=bmesh.new();bm.from_mesh(p.data)
 for z,normal in [(lo,(0,0,1)),(hi,(0,0,-1))]:
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(0,0,z),plane_no=normal,clear_inner=True,clear_outer=False)
 # All cut edges are embedded inside the neighboring course, never left as an open rear.
 bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(p.data);bm.free()
 for v in p.data.vertices:v.co.z+=index*.318-lo
 parts.append(p)
bpy.data.objects.remove(o,do_unlink=True)
for obj in bpy.context.scene.objects:obj.select_set(obj in parts)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();source=bpy.context.object;source.name='ScannedSackCourses_LOD0';source.data.name='OriginalUV_FiveCompressedCourses'
# End bag bodies retain their original recorded textile folds and worn seams, not smooth generated pillows.
objs=[source]
for level,ratio in [(1,.6),(2,.25)]:
 p=source.copy();p.data=source.data.copy();p.name='ScannedSackCourses_LOD'+str(level);bpy.context.collection.objects.link(p);bpy.context.view_layer.objects.active=p;mod=p.modifiers.new('ScannedFoldLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name);objs.append(p)
for obj in bpy.context.scene.objects:obj.select_set(obj in objs)
bpy.ops.wm.save_as_mainfile(filepath=str(R/'Docs/Art/CoverRepair20261106/ScannedSackCourses_20261106.blend'))
bpy.ops.export_scene.fbx(filepath=str(A/'ScannedSackCourses_20261106.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
# Diagnostic uses the same original photographed source map as the game.
m=source.data.materials[0];m.use_nodes=True;n=m.node_tree.nodes;lk=m.node_tree.links;p=n.get('Principled BSDF');tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(R/'five_river/Assets/_Project/Art/Models/Props/Sandbag/Textures/T_PR_Sandbag_BaseColor.png'));lk.new(tex.outputs['Color'],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.94
for obj in objs[1:]:obj.hide_render=True
bpy.ops.mesh.primitive_plane_add(size=100,location=(0,0,-.02));ground=bpy.context.object;gm=bpy.data.materials.new('DiagnosticEarth');gm.diffuse_color=(.15,.12,.09,1);ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(2.6,-5.1,2.6));cam=bpy.context.object;target=Vector((0,0,.82));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=50;bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-3,-4,6));bpy.context.object.data.energy=1100;bpy.context.object.data.size=5;bpy.context.scene.world.color=(.3,.3,.3)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.render.resolution_x=1000;s.render.resolution_y=750;s.render.resolution_percentage=100;s.render.filepath=str(Q/'ScannedFiveCourses_Blender.png');bpy.ops.render.render(write_still=True)
print('SCANNED',[(x.name,len(x.data.vertices),sum(len(p.vertices)-2 for p in x.data.polygons)) for x in objs])
