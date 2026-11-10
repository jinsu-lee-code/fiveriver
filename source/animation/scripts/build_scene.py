# Rebuild the Unity first-person M1 viewmodel (M1_ShotMotion space) in Blender.
import bpy, json, struct, math, os
from mathutils import Matrix, Vector
D="C:/sk_anim/export/"; d=json.load(open(D+"scene.json"))
P=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))   # Unity (x,y,z) <-> Blender (x,z,y)
def U(m): return Matrix([[m[c*4+r] for c in range(4)] for r in range(4)])
def UB(m): return P@U(m)@P
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
T={t['id']:t for t in d['transforms']}
def mat_for(m):
    name=m['mat'] or 'mat'
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name); mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF'); c=m['color']; bsdf.inputs['Base Color'].default_value=(c[0],c[1],c[2],1)
    tf=m.get('texfile')
    if tf and os.path.exists("C:/sk_anim/tex/"+tf):
        tn=mat.node_tree.nodes.new('ShaderNodeTexImage'); tn.image=bpy.data.images.load("C:/sk_anim/tex/"+tf,check_existing=True)
        mat.node_tree.links.new(tn.outputs['Color'],bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value=.6
    return mat
def read_mesh(m):
    b=open(D+m['file'],'rb').read(); o=0
    nv,nt,hasuv,sk=struct.unpack_from('<4i',b,o); o+=16
    v=struct.unpack_from('<%df'%(nv*3),b,o); o+=nv*12
    tr=struct.unpack_from('<%di'%nt,b,o); o+=nt*4
    uv=None
    if hasuv: uv=struct.unpack_from('<%df'%(nv*2),b,o); o+=nv*8
    bw=None
    if sk: bw=struct.unpack_from('<'+'if'*4*nv,b,o)
    return nv,v,tr,uv,bw
# armature for the hand rig (everything under M1_Grip_Rig) + wrist
hand=[m for m in d['meshes'] if m['skinned']][0]
bone_ids=set(i for i in hand['bones'] if i)
arm_data=bpy.data.armatures.new('HandRig'); arm=bpy.data.objects.new('HandRig',arm_data); sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT')
eb={}
for i in bone_ids:
    t=T[i]; e=arm_data.edit_bones.new(t['name']); M=UB(t['m'])
    e.head=(0,0,0); e.tail=(0,.012,0); e.matrix=M; eb[i]=e
for i,e in eb.items():
    p=T[i]['parent']
    if p and int(p) in eb: e.parent=eb[int(p)]
bpy.ops.object.mode_set(mode='OBJECT')
# meshes
for m in d['meshes']:
    nv,v,tr,uv,bw=read_mesh(m)
    owner=T[m['owner']]
    if m['skinned']:
        # bake the current skinned pose into shot space
        bp=[U(x) for x in m['bindposes']]; bm=[U(T[i]['m']) if i else Matrix.Identity(4) for i in m['bones']]
        sk=[bm[k]@bp[k] for k in range(len(bp))]
        verts=[]
        for k in range(nv):
            x=Vector((v[k*3],v[k*3+1],v[k*3+2],1)); acc=Vector((0,0,0,0))
            for j in range(4):
                bi=bw[k*8+j*2]; w=bw[k*8+j*2+1]
                if w>0: acc+=w*(sk[bi]@x)
            verts.append((acc.x,acc.z,acc.y))
    else:
        M=U(owner['m']); verts=[]
        for k in range(nv):
            x=M@Vector((v[k*3],v[k*3+1],v[k*3+2],1)); verts.append((x.x,x.z,x.y))
    faces=[(tr[k],tr[k+2],tr[k+1]) for k in range(0,len(tr),3)]
    me=bpy.data.meshes.new(m['renderer']); me.from_pydata(verts,[],faces); me.update()
    if uv:
        lay=me.uv_layers.new()
        for poly in me.polygons:
            for li in poly.loop_indices:
                vi=me.loops[li].vertex_index; lay.data[li].uv=(uv[vi*2],uv[vi*2+1])
    me.materials.append(mat_for(m))
    for poly in me.polygons: poly.use_smooth=True
    ob=bpy.data.objects.new(m['renderer'],me); sc.collection.objects.link(ob)
    if m['skinned']:
        for k,i in enumerate(m['bones']):
            if i: ob.vertex_groups.new(name=T[i]['name']) if T[i]['name'] not in ob.vertex_groups else None
        names=[T[i]['name'] if i else None for i in m['bones']]
        for k in range(nv):
            for j in range(4):
                bi=bw[k*8+j*2]; w=bw[k*8+j*2+1]
                if w>0 and names[bi]: ob.vertex_groups[names[bi]].add([k],w,'ADD')
        mod=ob.modifiers.new('Armature','ARMATURE'); mod.object=arm; ob.parent=arm
# camera
cm=UB(d['camera']['m'])
cam_data=bpy.data.cameras.new('GameCam'); cam=bpy.data.objects.new('GameCam',cam_data); sc.collection.objects.link(cam)
# Unity camera looks down +z with y up; Blender camera looks down -z with y up
cam.matrix_world=cm@Matrix.Rotation(math.radians(90),4,'X')
cam_data.sensor_fit='VERTICAL'; cam_data.angle_y=math.radians(d['camera']['fov']); cam_data.clip_start=.01
sc.camera=cam; sc.render.resolution_x=1280; sc.render.resolution_y=720
sc.render.engine='BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
w=bpy.data.worlds.new('W'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs['Color'].default_value=(.45,.5,.6,1); w.node_tree.nodes['Background'].inputs['Strength'].default_value=1.0
sun=bpy.data.objects.new('Sun',bpy.data.lights.new('Sun','SUN')); sun.data.energy=3; sun.rotation_euler=(math.radians(50),0,math.radians(30)); sc.collection.objects.link(sun)
bpy.ops.wm.save_as_mainfile(filepath="C:/sk_anim/reload_scene.blend")
sc.render.filepath="C:/sk_anim/render/rest.png"; bpy.ops.render.render(write_still=True)
print("BUILD_OK", len(bone_ids))
