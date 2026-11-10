# Game-camera view zoomed on the clip, for given phases (reload_anim.blend)
import bpy, sys
ph=[float(x) for x in sys.argv[sys.argv.index("--")+1].split(",")]; tag=sys.argv[sys.argv.index("--")+2]
bpy.ops.wm.open_mainfile(filepath="C:/sk_anim/reload_anim.blend")
sc=bpy.context.scene; F=sc.frame_end; cam=bpy.data.objects["GameCam"]; sc.camera=cam
r=sc.render; r.resolution_x,r.resolution_y=3840,2160; r.use_border=True; r.use_crop_to_border=True; r.border_min_x,r.border_max_x,r.border_min_y,r.border_max_y=.58,.92,.12,.55
clip=bpy.data.objects["ReloadClip"]
for p in ph:
    sc.frame_set(int(round(p*F))); bpy.context.view_layer.update()
    sc.render.filepath="C:/sk_anim/render/close/%s_%.2f.png"%(tag,p); bpy.ops.render.render(write_still=True)
print("ZOOM_OK")
