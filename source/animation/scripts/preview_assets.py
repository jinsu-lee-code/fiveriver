# Render each downloaded Megascans asset (and existing Dabudong env models) under identical light and camera.
import bpy, os, glob, math, json
from mathutils import Vector
OUT="C:/sk_assets/megascans/preview/"; EX="C:/sk_assets/megascans/extracted/"; OLD="C:/sk/five_river/Assets/_Project/Art/Maps/Dabudong_Env/Models/"
info={}
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True); sc=bpy.context.scene
    sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.cycles.device='CPU'; sc.render.resolution_x=640; sc.render.resolution_y=480
    w=bpy.data.worlds.new("W"); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs['Color'].default_value=(.55,.58,.62,1); w.node_tree.nodes['Background'].inputs['Strength'].default_value=.8
    s=bpy.data.objects.new("Sun",bpy.data.lights.new("Sun",'SUN')); s.data.energy=3.5; s.rotation_euler=(math.radians(45),math.radians(10),math.radians(35)); sc.collection.objects.link(s)
    return sc
def tex_mat(folder,name):
    m=bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree; b=nt.nodes['Principled BSDF']
    def find(*keys):
        for k in keys:
            g=glob.glob(folder+"/**/*"+k,recursive=True)
            if g: return g[0]
    bc=find("_BaseColor.jpg","_Diffuse.jpg","_B.png","_B-O.png")
    if bc:
        t=nt.nodes.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(bc); nt.links.new(t.outputs['Color'],b.inputs['Base Color'])
        if bc.endswith("_B-O.png") or "Wire" in bc: nt.links.new(t.outputs['Alpha'],b.inputs['Alpha'])
    op=find("_Opacity.jpg")
    if op:
        t=nt.nodes.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(op); t.image.colorspace_settings.name='Non-Color'; nt.links.new(t.outputs['Color'],b.inputs['Alpha'])
    nm=find("_Normal.jpg","_N.png")
    if nm:
        t=nt.nodes.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(nm); t.image.colorspace_settings.name='Non-Color'; n=nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(t.outputs['Color'],n.inputs['Color']); nt.links.new(n.outputs['Normal'],b.inputs['Normal'])
    b.inputs['Roughness'].default_value=.8
    return m
def frame(sc,objs,label):
    mn=Vector((1e9,)*3); mx=Vector((-1e9,)*3); tris=0
    for o in objs:
        if o.type!='MESH': continue
        for c in o.bound_box:
            w=o.matrix_world@Vector(c); mn=Vector(map(min,mn,w)); mx=Vector(map(max,mx,w))
        tris+=sum(len(p.vertices)-2 for p in o.data.polygons)
    size=mx-mn; ctr=(mn+mx)/2; r=max(size.length/2,0.05)
    cam=bpy.data.objects.new("Cam",bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera=cam; cam.data.lens=50
    d=r/math.tan(math.radians(18))*1.05; direction=Vector((1,-1.3,0.8)).normalized(); cam.location=ctr+direction*d
    cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.clip_end=d*10
    info[label]={"size_m":[round(size.x,2),round(size.y,2),round(size.z,2)],"tris":tris}
    sc.render.filepath=OUT+label+".png"; bpy.ops.render.render(write_still=True)
# downloaded FBX assets
for f in sorted(glob.glob(EX+"*/*.fbx")):
    sc=reset(); folder=os.path.dirname(f); name=os.path.basename(folder)
    bpy.ops.import_scene.fbx(filepath=f); objs=[o for o in sc.objects if o.type=='MESH']
    m=tex_mat(folder,name)
    for o in objs: o.data.materials.clear(); o.data.materials.append(m)
    frame(sc,objs,"new_"+name)
# downloaded surface materials: 2x2 m plane, 45 deg view
for g in sorted(glob.glob(EX+"*/*.gltf")):
    sc=reset(); folder=os.path.dirname(g); name=os.path.basename(folder)
    bpy.ops.mesh.primitive_plane_add(size=2); p=bpy.context.object; p.data.materials.append(tex_mat(folder,name))
    frame(sc,[p],"new_"+name)
# existing env models (with their own imported materials)
for n in ["ENV_barbed_wire","ENV_fighting_position","ENV_trench_revetment","ENV_sandbag_row","ENV_sandbag_wall","ENV_stump","ENV_dead_pine","ENV_straw_stack","ENV_rock_outcrop"]:
    sc=reset(); bpy.ops.import_scene.fbx(filepath=OLD+n+".fbx"); objs=[o for o in sc.objects if o.type=='MESH']
    texdir="C:/sk/five_river/Assets/_Project/Art/Maps/Dabudong_Env/Textures"
    for o in objs:
        for slot in o.material_slots:
            mt=slot.material
            if mt and mt.use_nodes:
                for nd in mt.node_tree.nodes:
                    if nd.type=='TEX_IMAGE' and nd.image and not os.path.exists(bpy.path.abspath(nd.image.filepath)):
                        cand=glob.glob(texdir+"/"+os.path.basename(nd.image.filepath))
                        if cand: nd.image.filepath=cand[0]
    frame(sc,objs,"old_"+n)
json.dump(info,open(OUT+"info.json","w"),indent=1); print("PREVIEW_OK",len(info))
