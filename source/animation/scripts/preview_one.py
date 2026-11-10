import bpy,sys,glob,os,math
from mathutils import Vector
f=sys.argv[sys.argv.index("--")+1]; out=sys.argv[sys.argv.index("--")+2]
bpy.ops.wm.read_factory_settings(use_empty=True); sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.render.resolution_x=640; sc.render.resolution_y=480
w=bpy.data.worlds.new("W"); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs['Color'].default_value=(.55,.58,.62,1); w.node_tree.nodes['Background'].inputs['Strength'].default_value=.8
s=bpy.data.objects.new("Sun",bpy.data.lights.new("Sun",'SUN')); s.data.energy=3.5; s.rotation_euler=(math.radians(45),math.radians(10),math.radians(35)); sc.collection.objects.link(s)
bpy.ops.import_scene.fbx(filepath=f); objs=[o for o in sc.objects if o.type=='MESH']
folder=os.path.dirname(f); bc=(glob.glob(folder+"/**/*BaseColor*.png",recursive=True)+glob.glob(folder+"/**/*Albedo*.png",recursive=True)+glob.glob(folder+"/**/*.png",recursive=True))
if bc:
    m=bpy.data.materials.new("m"); m.use_nodes=True; t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(bc[0]); m.node_tree.links.new(t.outputs['Color'],m.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    for o in objs: o.data.materials.clear(); o.data.materials.append(m)
mn=Vector((1e9,)*3); mx=Vector((-1e9,)*3)
for o in objs:
    for c in o.bound_box: wv=o.matrix_world@Vector(c); mn=Vector(map(min,mn,wv)); mx=Vector(map(max,mx,wv))
ctr=(mn+mx)/2; r=max((mx-mn).length/2,.05)
cam=bpy.data.objects.new("C",bpy.data.cameras.new("C")); sc.collection.objects.link(cam); sc.camera=cam
d=r/math.tan(math.radians(18))*1.05; cam.location=ctr+Vector((1,-1.3,.8)).normalized()*d; cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath=out; bpy.ops.render.render(write_still=True); print("ONE_OK",[round(x,2) for x in (mx-mn)], bc[0] if bc else None)
