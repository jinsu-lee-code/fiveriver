# Bake short-range AO on the right hand in its open bind pose (left-hand part removed), multiply into base colour.
import bpy, json, struct, numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=64
D="C:/sk_anim/export/"; d=json.load(open(D+"scene.json")); T={t['id']:t for t in d['transforms']}
m=[x for x in d['meshes'] if x['skinned']][0]
b=open(D+m['file'],'rb').read(); o=0
nv,nt,hasuv,sk=struct.unpack_from('<4i',b,o); o+=16
v=struct.unpack_from('<%df'%(nv*3),b,o); o+=nv*12; tr=struct.unpack_from('<%di'%nt,b,o); o+=nt*4
uv=struct.unpack_from('<%df'%(nv*2),b,o); o+=nv*8; bw=struct.unpack_from('<'+'if'*4*nv,b,o)
names=[T[i]['name'] if i else "" for i in m['bones']]
left=[False]*nv
for k in range(nv):
    wl=sum(bw[k*8+j*2+1] for j in range(4) if names[bw[k*8+j*2]].endswith('.L')); left[k]=wl>0.5
faces=[(tr[k],tr[k+2],tr[k+1]) for k in range(0,nt,3) if not (left[tr[k]] or left[tr[k+1]] or left[tr[k+2]])]
verts=[(v[k*3],v[k*3+2],v[k*3+1]) for k in range(nv)]
me=bpy.data.meshes.new("hand"); me.from_pydata(verts,[],faces); me.update()
lay=me.uv_layers.new()
for poly in me.polygons:
    for li in poly.loop_indices: vi=me.loops[li].vertex_index; lay.data[li].uv=(uv[vi*2],uv[vi*2+1])
for p in me.polygons: p.use_smooth=True
ob=bpy.data.objects.new("hand",me); sc.collection.objects.link(ob)
w=bpy.data.worlds.new("W"); sc.world=w; w.light_settings.distance=0.025
src=bpy.data.images.load("C:/sk_anim/tex/Hands_BaseColor.png"); W,H=src.size
img=bpy.data.images.new("AO",W,H,alpha=False)
mat=bpy.data.materials.new("m"); mat.use_nodes=True; me.materials.append(mat); n=mat.node_tree.nodes.new('ShaderNodeTexImage'); n.image=img; mat.node_tree.nodes.active=n
bpy.context.view_layer.objects.active=ob; ob.select_set(True); sc.render.bake.margin=16
bpy.ops.object.bake(type='AO')
ao=np.array(img.pixels[:]).reshape(H,W,4)[:,:,0]
base=np.array(src.pixels[:]).reshape(H,W,4)
mk0=ao>0.001; shade=np.where(mk0,1-0.5*(1-ao),1.0); out=base.copy(); out[:,:,:3]=np.clip(base[:,:,:3]*shade[:,:,None],0,1)
res=bpy.data.images.new("out",W,H,alpha=True); res.pixels.foreach_set(out.astype(np.float32).ravel())
res.filepath_raw="C:/sk/five_river/Assets/_Project/Art/SurfaceDetail20261030/Hands_BaseColor_Cavity.png"; res.file_format='PNG'; res.save()
mk=ao>0.001; print("AO_OK",len(faces),nt//3,float(ao[mk].mean()),float(np.percentile(ao[mk],5)))
