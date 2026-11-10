# How deep the carried clip sits inside the right hand skin, per frame of reload_anim.blend
import bpy, sys
from mathutils.bvhtree import BVHTree
bpy.ops.wm.open_mainfile(filepath="C:/sk_anim/reload_anim.blend")
sc=bpy.context.scene; F=sc.frame_end
hand=[o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object and m.object.name=="HandRig" for m in o.modifiers)]
clip=bpy.data.objects["ReloadClip"]; worst=(0,0)
for f in range(0,F+1,2):
    sc.frame_set(f); dg=bpy.context.evaluated_depsgraph_get(); deep=0.; who=''
    for h in hand:
        e=h.evaluated_get(dg); m=e.to_mesh(); mw=e.matrix_world
        polys=[tuple(p.vertices) for p in m.polygons]; bvh=BVHTree.FromPolygons([mw@v.co for v in m.vertices],polys); gn=[g.name for g in h.vertex_groups]; vg=[max(v.groups,key=lambda g:g.weight).group if v.groups else -1 for v in h.data.vertices]; e.to_mesh_clear()
        for v in list(clip.data.vertices)[::3]:
            p=clip.matrix_world@v.co; loc,nor,i,d=bvh.find_nearest(p)
            if loc is not None and d<.03 and (p-loc).dot(nor)<0 and d>deep: deep=d; who=gn[vg[polys[i][0]]] if vg[polys[i][0]]>=0 else '?'
    if deep>.001: print("CLIPIN frame %d p=%.2f depth_mm %.1f %s"%(f,f/F,deep*1000,who))
    if deep>worst[0]: worst=(deep,f)
print("CLIPIN_WORST depth_mm %.1f at p=%.2f"%(worst[0]*1000,worst[1]/F), "hand meshes", [h.name for h in hand])
