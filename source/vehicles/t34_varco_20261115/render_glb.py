import bpy, sys, math
from mathutils import Vector
src, out = sys.argv[sys.argv.index('--')+1], sys.argv[sys.argv.index('--')+2]
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith('.glb'): bpy.ops.import_scene.gltf(filepath=src)
else: bpy.ops.import_scene.fbx(filepath=src)
ms=[o for o in bpy.data.objects if o.type=='MESH']
mn=Vector((1e9,)*3); mx=Vector((-1e9,)*3); tris=0
for o in ms:
    tris+=sum(len(p.vertices)-2 for p in o.data.polygons)
    for c in o.bound_box:
        w=o.matrix_world@Vector(c)
        for i in range(3): mn[i]=min(mn[i],w[i]); mx[i]=max(mx[i],w[i])
size=mx-mn; ctr=(mx+mn)/2; print('MESHES',len(ms),'TRIS',tris,'SIZE',[round(v,2) for v in size])
R=max(size)
sun=bpy.data.lights.new('s','SUN'); so=bpy.data.objects.new('s',sun); bpy.context.collection.objects.link(so); so.rotation_euler=(math.radians(50),0,math.radians(40))
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'; sc.display.shading.show_cavity=True; sc.display.shading.show_shadows=True
sc.render.resolution_x=1300; sc.render.resolution_y=800
cam=bpy.data.cameras.new('c'); co=bpy.data.objects.new('c',cam); bpy.context.collection.objects.link(co); sc.camera=co; cam.lens=50
for k,(ang,el) in enumerate(((35,18),(100,8),(215,25))):
    a=math.radians(ang); e=math.radians(el); d=R*2.1
    co.location=ctr+Vector((math.cos(a)*math.cos(e),math.sin(a)*math.cos(e),math.sin(e)))*d
    co.rotation_euler=(ctr-co.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=out+'_%d.png'%k; bpy.ops.render.render(write_still=True)
