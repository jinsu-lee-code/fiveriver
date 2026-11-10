import bpy, numpy as np
bpy.ops.wm.open_mainfile(filepath="C:/sk_anim/reload_scene.blend")
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=64
hand=bpy.data.objects["M1_Anatomical_Grip_Hands"]
for o in sc.objects:
    if o!=hand and o.type=='MESH': o.hide_render=True
sc.world.light_settings.distance=0.025
for o in sc.objects: o.select_set(False)
hand.select_set(True); bpy.context.view_layer.objects.active=hand
src=bpy.data.images.load("C:/sk_anim/tex/Hands_BaseColor.png"); W,H=src.size
img=bpy.data.images.new("AO",W,H,alpha=False)
mat=hand.data.materials[0]; nt=mat.node_tree; node=nt.nodes.new('ShaderNodeTexImage'); node.image=img; nt.nodes.active=node
sc.render.bake.margin=8
bpy.ops.object.bake(type='AO')
ao=np.array(img.pixels[:]).reshape(H,W,4)[:,:,0]
base=np.array(src.pixels[:]).reshape(H,W,4)
k=0.55; shade=1-k*(1-ao)
out=base.copy(); out[:,:,:3]=np.clip(base[:,:,:3]*shade[:,:,None],0,1)
res=bpy.data.images.new("out",W,H,alpha=True); res.pixels.foreach_set(out.astype(np.float32).ravel())
res.filepath_raw="C:/sk/five_river/Assets/_Project/Art/SurfaceDetail20261030/Hands_BaseColor_Cavity.png"; res.file_format='PNG'; res.save()
m=ao>0.001
print("AO_OK",W,H,float(ao[m].mean()),float(np.percentile(ao[m],5)))
