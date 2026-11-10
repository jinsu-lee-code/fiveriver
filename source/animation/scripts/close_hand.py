# Close-up renders of the right hand from two sides at given reload phases (after anim.py has saved reload_anim.blend)
import bpy, sys, math
from mathutils import Vector
ph=[float(x) for x in sys.argv[sys.argv.index("--")+1].split(",")]; tag=sys.argv[sys.argv.index("--")+2]
bpy.ops.wm.open_mainfile(filepath="C:/sk_anim/reload_anim.blend")
sc=bpy.context.scene; F=sc.frame_end; arm=bpy.data.objects["HandRig"]
cd=bpy.data.cameras.new("cc"); cd.lens=60; cd.clip_start=.005; co=bpy.data.objects.new("cc",cd); sc.collection.objects.link(co); sc.camera=co
sc.render.resolution_x,sc.render.resolution_y=640,480
for p in ph:
    sc.frame_set(int(round(p*F))); bpy.context.view_layer.update()
    pb=arm.pose.bones; c=(arm.matrix_world@pb["finger2-1.R"].head+arm.matrix_world@pb["finger1-3.R"].tail)/2
    for side,off in (("a",Vector((0.22,-0.05,0.06))),("b",Vector((-0.05,-0.22,0.10)))):
        co.location=c+off; co.rotation_euler=(c-co.location).to_track_quat('-Z','Y').to_euler(); bpy.context.view_layer.update()
        sc.render.filepath="C:/sk_anim/render/close/%s_%.2f_%s.png"%(tag,p,side); bpy.ops.render.render(write_still=True)
print("CLOSE_OK")
