# Author the M1 partial-reload hand animation on the rebuilt viewmodel and bake it for Unity.
import bpy, json, math, sys, os
from mathutils import Matrix, Vector, Quaternion, Euler
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
A=json.load(open(os.path.join(ROOT,"scripts","params.json"))); MODE=sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "render"
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT,A.get("scene","reload_scene_prop.blend")))
sc=bpy.context.scene; sc.render.fps=A["fps"]; F=A["frames"]; sc.frame_start=0; sc.frame_end=F
P=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
V=lambda a:Vector(a)
def smooth(a,b,p):
    t=min(1,max(0,(p-a)/(b-a))) if b>a else 1.0
    return t*t*(3-2*t)
def window(p,s,pk,f,e): return smooth(s,pk,p)*(1-smooth(f,e,p))
arm=bpy.data.objects["HandRig"]; bones=arm.data.bones
bpy.context.view_layer.objects.active=arm
# bone lengths along the chain so IK tips sit on the fingertips
bpy.ops.object.mode_set(mode='EDIT')
skin=[o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==arm for m in o.modifiers)][0]
toA=arm.matrix_world.inverted()@skin.matrix_world; gidx={g.name:g.index for g in skin.vertex_groups}
for e in arm.data.edit_bones:
    if e.name.endswith(".L"): continue
    kids=e.children
    if kids: L=(kids[0].head-e.head).length
    else:
        # fingertip bones end at the tip of the skin (not past it), so the IK puts the real fingertip on its target
        d=(e.tail-e.head).normalized(); gi=gidx.get(e.name)
        reach=[(toA@v.co-e.head).dot(d) for v in skin.data.vertices if gi is not None and any(g.group==gi and g.weight>.05 for g in v.groups)]
        L=(max(reach)-0.002) if reach else ((e.parent.length*0.85) if e.parent else 0.02)
    if L>1e-4: e.length=L
bpy.ops.object.mode_set(mode='OBJECT')
# seated clip -> carried clip object
seat=bpy.data.objects["SM_M1_Clip_L0"]
vs=[seat.matrix_world@v.co for v in seat.data.vertices]
cmin=V((min(v.x for v in vs),min(v.y for v in vs),min(v.z for v in vs))); cmax=V((max(v.x for v in vs),max(v.y for v in vs),max(v.z for v in vs)))
ccen=(cmin+cmax)/2; ctop=V((ccen.x,ccen.y,cmax.z))
clip=seat.copy(); clip.data=seat.data; clip.name="ReloadClip"; sc.collection.objects.link(clip)
clip_rest=clip.matrix_world.copy()
# targets parented to the clip, given relative to the clip top
def target(name,off):
    t=bpy.data.objects.new(name,None); sc.collection.objects.link(t); t.parent=clip
    t.matrix_parent_inverse=clip.matrix_world.inverted(); t.location=ctop+V(off); return t
tT=target("T_thumb",A["thumb_target_clip"]); tI=target("T_index",A["index_target_clip"]); tM=target("T_middle",A["middle_target_clip"])
pb=arm.pose.bones
def ik(bone,tgt):
    c=pb[bone].constraints.new('IK'); c.target=tgt; c.chain_count=3; c.use_tail=True; return c
cT=ik("finger1-3.R",tT); cI=ik("finger2-3.R",tI); cM=ik("finger3-3.R",tM)
# rest hand frame
wr=bones["wrist.R"]; W0=arm.matrix_world@wr.matrix_local
def head(n): return arm.matrix_world@bones[n].head_local
f_rest=(head("finger2-1.R")-head("wrist.R")).normalized(); a_rest=(head("finger5-1.R")-head("finger2-1.R"))
n_rest=f_rest.cross(a_rest).normalized(); a_rest=n_rest.cross(f_rest).normalized()
Rrest=Matrix((f_rest,n_rest,a_rest)).transposed()
fd=V(A["finger_dir"]).normalized(); nd=V(A["palm_normal"]); nd=(nd-fd*nd.dot(fd)).normalized(); ad=nd.cross(fd).normalized()
Rdes=Matrix((fd,nd,ad)).transposed()
load_rot=(Rdes@Rrest.inverted()).to_quaternion()@W0.to_quaternion()
# palm-down hand for taking the old clip and thumbing the new one home; the carry hand (above) faces the gun with the clip held upright in front, as in the reference video
fp=V(A.get("press_finger_dir",A["finger_dir"])).normalized(); npd=V(A.get("press_palm_normal",A["palm_normal"])); npd=(npd-fp*npd.dot(fp)).normalized()
press_rot=(Matrix((fp,npd,npd.cross(fp).normalized())).transposed()@Rrest.inverted()).to_quaternion()@W0.to_quaternion()
rest_rot=W0.to_quaternion(); rest_loc=W0.translation.copy()
cam=bpy.data.objects["GameCam"]; cam0=cam.matrix_world.copy()
def U2B(m): return P@m@P
def present(p):
    k=window(p,.0,.08,.88,.98); o=A["present_offset_unity"]; e=A["present_euler_unity"]
    q=Euler((math.radians(e[0]*k),math.radians(e[1]*k),math.radians(e[2]*k)),'ZXY').to_quaternion()  # Unity Quaternion.Euler order
    mu=Matrix.Translation(V((o[0]*k,o[1]*k,o[2]*k)))@q.to_matrix().to_4x4()
    return U2B(mu)
def pose_at(p):
    # Partial reload as in the reference video (3.2 s): the hand comes in open from the right, takes the part-used
    # clip and pulls it up and out to the right, goes off-screen, returns with a fresh clip, thumbs it home (seated at
    # 66% = the game's insertion cue), lets go and drops away to the right, then back to the grip.
    fetch=V(A["fetch"]); side=V(A["side"]); above=V(A["above"]); zero=V((0,0,0))
    # wrist held off the clip along the back of the hand so the rounds stay below the palm, fingers reach down to it
    gripoff=V(A["wrist_from_clip_top"])-nd*A.get("palm_clear",0.0); follow=A["hand_follow_seat"]
    if p<.28: clip=zero
    elif p<.50:
        c=zero.lerp(above,smooth(.28,.33,p)); c=c.lerp(side,smooth(.33,.40,p)); clip=c.lerp(fetch,smooth(.40,.47,p))
    else:
        c=fetch.lerp(side,smooth(.50,.55,p)); c=c.lerp(above,smooth(.55,.58,p)); clip=c.lerp(zero,smooth(.58,.66,p))
    approach=ctop+side+gripoff+V((0.04,0.0,0.03))
    low=ctop+fetch+gripoff
    seated_hand=ctop+above.lerp(zero,follow)+gripoff
    lift=V(A.get("lift_after_seat",[0,0,0]))
    if p<.10: hp=rest_loc.copy()
    elif p<.22: hp=rest_loc.lerp(approach,smooth(.10,.20,p))
    elif p<.28: hp=approach.lerp(ctop+gripoff,smooth(.22,.28,p))
    elif p<.58: hp=ctop+clip+gripoff
    elif p<.66: hp=ctop+above.lerp(zero,smooth(.58,.66,p)*follow)+gripoff
    else:
        lv=seated_hand+lift*smooth(.66,.70,p)+V(A.get("rest_drift",[0,0,0]))*smooth(.68,.82,p); lv=lv.lerp(ctop+side+gripoff,smooth(.80,.85,p)); lv=lv.lerp(low,smooth(.85,.91,p))  # rests flat on the receiver until 80%, as in the video
        hp=lv.lerp(rest_loc,smooth(.89,.97,p))
    away=window(p,.10,.20,.80,.97)
    carry=window(p,.30,.36,.53,.60); rot=rest_rot.slerp(press_rot.slerp(load_rot,carry),away)
    # while thumbing the clip home the hand rolls about the fingers so the thumb shows instead of the back of the hand
    pr=window(p,.57,.61,.65,.69); rot=Quaternion(fd,math.radians(A.get("press_roll_deg",0.0))*pr)@rot
    # resting open on the receiver the back of the hand faces the eye, as in the video (the cupped grip-modelled palm shows a dark hollow)
    rr=window(p,.64,.70,.82,.88); rot=Quaternion(fd,math.radians(A.get("rest_roll_deg",0.0))*rr)@rot
    grab=window(p,.20,.27,.52,.58)      # index/middle on the clip while it is out and back
    thumb=window(p,.22,.28,.64,.68)     # thumb on the rounds, pressing them home, then off
    return clip,hp,rot,grab,thumb
# The path above stops dead at every waypoint. Pass the hand and the clip through the same waypoints on one
# smooth curve (Hermite with tangents from the neighbours), so the motion flows like the reference video;
# a waypoint that ends a hold keeps a zero tangent so nothing drifts out of a rest.
KT=A.get("smooth_keys",[0,.10,.20,.28,.33,.40,.47,.50,.55,.58,.62,.66,.70,.76,.80,.85,.91,.97,1.0])
KH=[pose_at(t)[1].copy() for t in KT]; KC=[pose_at(t)[0].copy() for t in KT]
def tangents(K):
    out=[]
    for i in range(len(K)):
        if i==0 or i==len(K)-1 or (K[i]-K[i-1]).length<1e-5 or (K[i+1]-K[i]).length<1e-5: out.append(V((0,0,0)))
        else: out.append((K[i+1]-K[i-1])/(KT[i+1]-KT[i-1]))
    return out
TH,TC=tangents(KH),tangents(KC)
def hermite(K,Tg,p):
    i=max(0,min(len(KT)-2,next((j for j in range(len(KT)-1) if p<KT[j+1]),len(KT)-2))); h=KT[i+1]-KT[i]; u=(p-KT[i])/h
    a=2*u**3-3*u**2+1; b=u**3-2*u**2+u; c=-2*u**3+3*u**2; d=u**3-u**2
    return K[i]*a+Tg[i]*(b*h)+K[i+1]*c+Tg[i+1]*(d*h)
for f in range(F+1):
    p=f/F; sc.frame_set(f)
    co,hp,rot,grab,thumb=pose_at(p)
    if A.get("smooth_path",True): co,hp=hermite(KC,TC,p),hermite(KH,TH,p)
    clip.matrix_world=Matrix.Translation(co)@clip_rest; clip.keyframe_insert("location"); clip.keyframe_insert("rotation_euler")
    M=Matrix.Translation(hp)@rot.to_matrix().to_4x4()
    pb["wrist.R"].matrix=arm.matrix_world.inverted()@M; pb["wrist.R"].keyframe_insert("location"); pb["wrist.R"].keyframe_insert("rotation_quaternion")
    cT.influence=thumb; cI.influence=grab; cM.influence=grab
    curl=window(p,.20,.28,.54,.62); cd=A.get("curl_deg",[0,0,0]); ex=window(p,.80,.85,.90,.96); ed=A.get("exit_curl_deg",[0,0,0])
    # ring/little curl and index curl only while holding the clip; after seating the fingers stay loose
    for fn in ("finger2","finger3","finger4","finger5"):
        for j in range(3):
            ang=ed[j]*ex+(cd[j]*curl if fn in("finger4","finger5") else 0)
            # the index stays softly curled from taking the clip until the hand is away, never pointing out straight on its own
            if fn=="finger2": iw=window(p,.30,.40,.64,.70); ang=A.get("index_curl_deg",[0,0,0])[j]*iw+ed[j]*ex*(1-iw)
            # resting on the receiver after the clip is home: a relaxed hand, not a stiff flat one
            ang+=A.get("rest_curl_deg",[0,0,0])[j]*window(p,.62,.70,.82,.88)
            b=pb["%s-%d.R"%(fn,j+1)]; b.rotation_mode='XYZ'; b.rotation_euler=(math.radians(ang),0,0); b.keyframe_insert("rotation_euler")
    # the thumb bends at its joints while pressing, so its knuckle and nail read instead of a straight rod
    tf=A.get("thumb_press_flex_deg",[0,0]); pw=window(p,.56,.60,.66,.70)
    for j,deg in ((2,tf[0]),(3,tf[1])):
        b=pb["finger1-%d.R"%j]; b.rotation_mode='XYZ'; b.rotation_euler=(math.radians(deg*pw),0,0); b.keyframe_insert("rotation_euler")
    # twist the thumb about its own length so its nail faces the eye while it presses
    # (twisting at the thumb's root wrings the web skin into a dark fold; the middle bone carries the twist instead)
    b=pb["finger1-2.R"]; b.rotation_euler=(math.radians(tf[0]*pw),math.radians(A.get("thumb_press_twist_deg",0)*pw),0); b.keyframe_insert("rotation_euler")
    for c in (cT,cI,cM): c.keyframe_insert("influence")
    cam.matrix_world=present(p).inverted()@cam0; cam.keyframe_insert("location"); cam.keyframe_insert("rotation_euler")
seat.hide_render=True; seat.keyframe_insert("hide_render",frame=0)
for fc in (arm.animation_data.action.fcurves if arm.animation_data and hasattr(arm.animation_data.action,'fcurves') else []):
    for k in fc.keyframe_points: k.interpolation='LINEAR'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,"reload_anim.blend"))
if MODE in("render","both"):
    os.makedirs(os.path.join(ROOT,"render","frames"),exist_ok=True)
    for p in A["renders"]:
        sc.frame_set(int(round(p*F))); sc.render.filepath=os.path.join(ROOT,"render","frames","r_%.2f.png"%p); bpy.ops.render.render(write_still=True)
if MODE in("export","both"):
    out={"fps":A["fps"],"frames":F,"bones":[],"wrist":[],"clip":[],"local":{}}
    names=[b.name for b in bones if b.name.endswith(".R")]
    out["bones"]=names
    for n in names: out["local"][n]=[]
    dg=bpy.context.evaluated_depsgraph_get()
    for f in range(F+1):
        sc.frame_set(f); dg.update(); ae=arm.evaluated_get(dg)
        mats={n:U2B(ae.matrix_world@ae.pose.bones[n].matrix) for n in names}
        out["wrist"].append([x for r in mats["wrist.R"].transposed() for x in r])
        out["clip"].append([x for r in U2B(clip.evaluated_get(dg).matrix_world).transposed() for x in r])
        for n in names:
            par=bones[n].parent
            loc=(mats[par.name].inverted()@mats[n]) if par else mats[n]
            q=loc.to_quaternion(); out["local"][n].append([q.x,q.y,q.z,q.w])
    json.dump(out,open(os.path.join(ROOT,"reload_partial_baked.json"),"w"))
print("ANIM_OK")
