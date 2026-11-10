import bpy
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath="C:/sk_anim/reload_scene.blend")
def bb(name):
    o=bpy.data.objects[name]; vs=[o.matrix_world@v.co for v in o.data.vertices]
    mn=Vector((min(v.x for v in vs),min(v.y for v in vs),min(v.z for v in vs))); mx=Vector((max(v.x for v in vs),max(v.y for v in vs),max(v.z for v in vs)))
    return [round(x,4) for x in mn],[round(x,4) for x in mx]
for n in ["SM_M1_Clip_L0","SM_M1_Receiver_L0","SM_M1_OpRod_L0","SupportHand_M1","M1_Anatomical_Grip_Hands"]:
    print("BB",n,bb(n))
arm=bpy.data.objects["HandRig"]
for b in arm.data.bones:
    print("BONE",b.name,"parent",b.parent.name if b.parent else "-","head",[round(x,4) for x in b.head_local],"len",round(b.length,4),"kids",len(b.children))
print("CAM",[round(x,4) for x in bpy.data.objects["GameCam"].matrix_world.translation])
