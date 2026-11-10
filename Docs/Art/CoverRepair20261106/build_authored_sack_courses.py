import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
R=Path('C:/Users/mbc/Documents/FiveRivers_Dabudong_6000.6.8f1');A=R/'five_river/Assets/_Project/Art/MapRepair20261106';Q=Path('C:/Users/mbc/.codex/visualizations/2026/11/02/01a199af-6dde-73d0-a46f-1508e6f64f5d/Expanded_20261106/SandbagQuality');Q.mkdir(parents=True,exist_ok=True)
scan=R/'five_river/Assets/_Project/Art/Maps/ClaudePolish20261030/Scan'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
cloth=bpy.data.images.load(str(scan/'hessian_230_diff_2k.jpg'));normal=bpy.data.images.load(str(scan/'hessian_230_nor_gl_2k.jpg'));normal.colorspace_settings.name='Non-Color'
mats=[]
for i,tint in enumerate([(0.59,.59,.49),(0.55,.56,.44),(.65,.61,.50),(.57,.55,.44)]):
 m=bpy.data.materials.new('SackCourse_Cloth'+str(i));m.use_nodes=True;n=m.node_tree.nodes;lk=m.node_tree.links;p=n.get('Principled BSDF');tex=n.new('ShaderNodeTexImage');tex.image=cloth;mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*tint,1);lk.new(tex.outputs['Color'],mix.inputs[1]);lk.new(mix.outputs[0],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.94;p.inputs['Specular IOR Level'].default_value=.20;tn=n.new('ShaderNodeTexImage');tn.image=normal;nm=n.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.4;lk.new(tn.outputs['Color'],nm.inputs['Color']);lk.new(nm.outputs[0],p.inputs['Normal']);mats.append(m)
seam=bpy.data.materials.new('SackCourse_Thread');seam.diffuse_color=(.19,.15,.10,1);seam.use_nodes=True;seam.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.19,.15,.10,1);seam.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=1;mats.append(seam)

def signpow(x,p):return math.copysign(abs(x)**p,x)
def wall(seed=83):
 rng=random.Random(seed);vertices=[];faces=[];uvs=[];mi=[]
 def bag(cx,cy,cz,L,D,H,phase,mat):
  nu,nv=32,24;offset=len(vertices);local=[];uv=[]
  for a in range(nu+1):
   u=math.pi*a/nu;x=signpow(-math.cos(u),.86);radius=math.sin(u)**.72;radius=max(radius,.055 if x>.96 else 0)
   for b in range(nv):
    v=math.tau*b/nv;fy=signpow(math.cos(v),.92);fz=signpow(math.sin(v),.78)
    pinch=(abs(x)**2)*.010*math.cos(7*v+phase+3*x)*math.sin(u*2.8)
    fold=.0065*math.sin(12*x+3*v+phase)*abs(x)**1.2 + .0028*math.sin(5*x-2*v+phase)
    gather=.018*(max(0,x-.35)/.65)**1.25*math.cos(5*v+phase)
    sidefold=-.013*math.exp(-((x-(.68-.28*math.sin(v)))/.085)**2)-.009*math.exp(-((x-(.40-.18*math.sin(v)))/.08)**2)
    pinch+=gather+sidefold
    closure=1-.59*(max(0,x-.45)/.55)**1.45;px=L*.5*x;py=(D*.5*radius*fy+(pinch+fold)*math.cos(v))*closure
    pz=(H*.5*radius*fz+(pinch+fold)*math.sin(v))*closure
    pz-=.005*(1-x*x)*max(0,math.sin(v))**4
    # Modest load flattening, with broad softly squared bearing surfaces.
    pz+=.003*math.sin(x*2.7+phase)*radius
    px*=(-1 if phase>math.pi else 1);local.append((px,py,pz));vertices.append((cx+px,cy+py,cz+pz));uv.append((px/.27+phase, b/nv*(2*D+2*H)/.27))
  faces.append(tuple(offset+nu*nv+b for b in range(nv)));uvs.append([(0,0)]*nv);mi.append(mat)
  for a in range(nu):
   for b in range(nv):
    ids=[a*nv+b,a*nv+(b+1)%nv,(a+1)*nv+(b+1)%nv,(a+1)*nv+b]
    faces.append(tuple(offset+k for k in ids));uvs.append([uv[k] for k in ids]);mi.append(mat)
  # Subtle sewn side line. It stays under 2 mm instead of a raised rope-like ridge.
  for side in []:
   pts=[]
   for j in range(17):
    x=-.86+j/16*1.72;px=L*.5*x;r=(1-abs(x)**(1/.62*2))**.31 if abs(x)<1 else 0
    pts.append((cx+px,cy+side*(D*.5*r+.0007),cz-.004+.002*math.sin(8*x+phase)))
   so=len(vertices)
   for j,p in enumerate(pts):
    for k in range(4):
     an=math.tau*k/4;vertices.append((p[0],p[1]+math.cos(an)*.0011,p[2]+math.sin(an)*.0011))
   for j in range(16):
    for k in range(4):
     faces.append((so+j*4+k,so+j*4+(k+1)%4,so+(j+1)*4+(k+1)%4,so+(j+1)*4+k));uvs.append([(0,0)]*4);mi.append(4)
 for course in range(5):
  for depth in range(2):
   # Shift joints by a large fraction of one sack between adjacent courses/depth rows.
   widths=([.81,.83] if (course+depth)%2==0 else [.38,.81,.46])
   cursor=-.82
   for col,L0 in enumerate(widths):
    L=L0+rng.uniform(-.012,.012);cx=cursor+L0*.5;cursor+=L0-.012
    H=.245+(course/4)*.014+rng.uniform(-.006,.006)
    cy=(-.247 if depth==0 else .247)+rng.uniform(-.006,.006)
    cz=.119+course*.231+rng.uniform(-.004,.004)
    bag(cx,cy,cz,L,.525,H,rng.uniform(0,6.2),rng.randrange(4))
 mesh=bpy.data.meshes.new('SackCourses_Authored');mesh.from_pydata(vertices,[],faces);mesh.update();o=bpy.data.objects.new('SackCourses_LOD0',mesh);bpy.context.collection.objects.link(o)
 for m in mats:mesh.materials.append(m)
 layer=mesh.uv_layers.new(name='Meters_0p27mWeave')
 for p,coords,mat in zip(mesh.polygons,uvs,mi):
  p.material_index=mat;p.use_smooth=True
  for li,co in zip(p.loop_indices,coords):layer.data[li].uv=co
 # Surface winding is corrected with the normals operator after construction.
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 return o
obj=wall();objs=[obj]
for level,ratio in [(1,.45),(2,.15)]:
 o=obj.copy();o.data=obj.data.copy();o.name='SackCourses_LOD'+str(level);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('SilhouetteLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name);objs.append(o)
for o in bpy.context.scene.objects:o.select_set(o in objs)
bpy.ops.wm.save_as_mainfile(filepath=str(R/'Docs/Art/CoverRepair20261106/SackCourses_20261106.blend'))
bpy.ops.export_scene.fbx(filepath=str(A/'SackCourses_20261106.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
for o in objs[1:]:o.hide_render=True
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01));ground=bpy.context.object;gm=bpy.data.materials.new('DiagnosticEarth');gm.diffuse_color=(.14,.11,.08,1);ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(2,-4.4,2.1));cam=bpy.context.object;target=Vector((0,0,.62));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=50;bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-3,-4,6));bpy.context.object.data.energy=750;bpy.context.object.data.size=5;bpy.context.scene.world.color=(.25,.25,.25)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.render.resolution_x=1000;s.render.resolution_y=700;s.render.resolution_percentage=100;s.render.filepath=str(Q/'NativeCourses_v3_Blender.png');bpy.ops.render.render(write_still=True)
print('OUTPUT',[(o.name,len(o.data.vertices),sum(len(p.vertices)-2 for p in o.data.polygons)) for o in objs])




