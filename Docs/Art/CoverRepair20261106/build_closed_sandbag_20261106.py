import bpy,bmesh,json,math
from mathutils import Vector, Matrix
from pathlib import Path
project=Path('C:/Users/mbc/Documents/FiveRivers_Dabudong_6000.6.8f1/five_river')
out=project/'Assets/_Project/Art/MapRepair20261106';out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(project/'Assets/_Project/Art/Models/Props/Sandbag/SM_PR_SandbagWall.fbx'))
source=next(o for o in bpy.context.scene.objects if o.type=='MESH')
for v in source.data.vertices:
 p=source.matrix_world@v.co;v.co=p
source.matrix_world=Matrix.Identity(4);bm=bmesh.new();bm.from_mesh(source.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=0.000001)
before=sum(e.is_boundary for e in bm.edges)
bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
bm.edges.index_update();remaining={e for e in bm.edges if e.is_boundary}
while remaining:
 e=min(remaining,key=lambda e:e.index);verts=[e.verts[0],e.verts[1]];edges=[e];current=e.verts[1]
 while True:
  options=sorted([n for n in current.link_edges if n in remaining and n not in edges],key=lambda e:e.index)
  if not options:
   remaining.difference_update(edges);break
  n=options[0];nxt=n.other_vert(current);edges.append(n)
  if nxt in verts:
   idx=verts.index(nxt);cycle=verts[idx:];used=edges[idx:]
   try:bm.faces.new(cycle)
   except ValueError:pass
   remaining.difference_update(used);break
  verts.append(nxt);current=nxt
loose=[f for f in bm.faces if all(e.is_boundary for e in f.edges)]
bmesh.ops.delete(bm,geom=loose,context='FACES')
bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
after=sum(e.is_boundary for e in bm.edges);bm.to_mesh(source.data);bm.free();source.data.update()
print('CLOSED',before,after,'verts',len(source.data.vertices),'tris',sum(len(p.vertices)-2 for p in source.data.polygons))
assert after==0,'Derived source must be closed.'
report=[];objects=[]
for level,ratio in [(0,1),(1,.5),(2,.22)]:
 o=source.copy();o.data=source.data.copy();o.name='SandbagClosed_LOD'+str(level);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
 if ratio<1:
  mod=o.modifiers.new('PreserveBagSilhouetteLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 report.append({'level':level,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'bounds':[[min((o.matrix_world@v.co)[i] for v in o.data.vertices),max((o.matrix_world@v.co)[i] for v in o.data.vertices)] for i in range(3)]})
 objects.append(o);o.select_set(False)
bpy.data.objects.remove(source,do_unlink=True)
for o in bpy.context.scene.objects:o.select_set(o in objects)
bpy.ops.export_scene.fbx(filepath=str(out/'SandbagClosed_20261106.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
(out/'SandbagClosed_20261106.json').write_text(json.dumps({'source':'Assets/_Project/Art/Models/Props/Sandbag/SM_PR_SandbagWall.fbx','preserved':'Source shape, UVs and original material slot; only microscopic source boundary loops closed. Existing cover fitting is done separately without collider edits.','boundaryEdgesBefore':before,'boundaryEdgesAfter':after,'lods':report},indent=2))
print('OUTPUT',report)




