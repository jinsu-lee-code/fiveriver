# M1 Garand surface masks (2026-11-23). The rifle's meshes as the game shows them (exported from Unity by
# m1_export.cs: one object per renderer submesh, named <renderer>__<submesh>__<material>) are baked, per material,
# into their own UVs: ambient occlusion, how sharp an edge the texel lies on, and a few 3D noises that run through
# the whole rifle without seams. compose_maps.py turns these into the colour / metal / smoothness maps.
#
#   blender -b --factory-startup -P bake_masks.py -- <m1_l0.obj> <out dir>
import bpy, sys, os

obj_path, out = sys.argv[sys.argv.index('--') + 1:][:2]
SETS = {'Metal': ('M1_View_Photo_Metal', 4096), 'Wood': ('M1_View_Photo_Wood', 4096), 'Hardware': ('M1_View_Photo_Hardware', 2048)}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=obj_path, forward_axis='NEGATIVE_Z', up_axis='Y')
objs = [o for o in bpy.data.objects if o.type == 'MESH']
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
prefs = bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
    for d in prefs.devices: d.use = True
    sc.cycles.device = 'GPU'
except Exception as e:
    print('GPU not used:', e)
sc.render.bake.margin = 12
sc.render.bake.use_clear = True
world = bpy.data.worlds.new('w'); sc.world = world


def noise(nodes, links, scale, detail, stretch=(1, 1, 1), kind='NOISE'):
    tc = nodes.new('ShaderNodeTexCoord'); mp = nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = stretch
    links.new(tc.outputs['Object'], mp.inputs['Vector'])
    if kind == 'VORONOI':
        n = nodes.new('ShaderNodeTexVoronoi'); n.inputs['Scale'].default_value = scale
        links.new(mp.outputs['Vector'], n.inputs['Vector']); return n.outputs['Distance']
    n = nodes.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = 0.6
    links.new(mp.outputs['Vector'], n.inputs['Vector']); return n.outputs['Fac']


def material(kind):
    m = bpy.data.materials.new(kind); m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    nodes.clear()
    outn = nodes.new('ShaderNodeOutputMaterial'); em = nodes.new('ShaderNodeEmission')
    links.new(em.outputs[0], outn.inputs['Surface'])
    comb = nodes.new('ShaderNodeCombineColor')
    if kind == 'edge':
        # R: edge sharpness from a rounded normal (two radii: fine edges, broad forms). G, B: the same, wider
        geo = nodes.new('ShaderNodeNewGeometry')
        for ch, radius in (('Red', 0.0012), ('Green', 0.004), ('Blue', 0.012)):
            bv = nodes.new('ShaderNodeBevel'); bv.samples = 12; bv.inputs['Radius'].default_value = radius
            dot = nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
            links.new(bv.outputs['Normal'], dot.inputs[0]); links.new(geo.outputs['Normal'], dot.inputs[1])
            inv = nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
            links.new(dot.outputs['Value'], inv.inputs[1])
            mul = nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = 6.0; mul.use_clamp = True
            links.new(inv.outputs[0], mul.inputs[0]); links.new(mul.outputs[0], comb.inputs[ch])
    elif kind == 'noiseA':
        # R: broad clouds (20 cm), G: blotches (3 cm), B: fine grain (2 mm)
        links.new(noise(nodes, links, 6.0, 6.0), comb.inputs['Red'])
        links.new(noise(nodes, links, 45.0, 5.0), comb.inputs['Green'])
        links.new(noise(nodes, links, 900.0, 2.0), comb.inputs['Blue'])
    elif kind == 'noiseB':
        # R: long scratches along the rifle, G: short scratches across, B: specks (dents, pits)
        links.new(noise(nodes, links, 700.0, 3.0, (1, 1, 0.02)), comb.inputs['Red'])
        links.new(noise(nodes, links, 500.0, 3.0, (0.03, 1, 1)), comb.inputs['Green'])
        links.new(noise(nodes, links, 260.0, 0.0, kind='VORONOI'), comb.inputs['Blue'])
    elif kind == 'place':
        # where on the rifle: R along it (0 butt .. 1 muzzle), G height, B side
        tc = nodes.new('ShaderNodeTexCoord'); mp = nodes.new('ShaderNodeMapping')
        mp.inputs['Scale'].default_value = (4.0, 4.0, 0.8); mp.inputs['Location'].default_value = (0.5, 0.5, 0.5)
        sep = nodes.new('ShaderNodeSeparateXYZ')
        links.new(tc.outputs['Object'], mp.inputs['Vector']); links.new(mp.outputs['Vector'], sep.inputs[0])
        links.new(sep.outputs['Z'], comb.inputs['Red']); links.new(sep.outputs['Y'], comb.inputs['Green']); links.new(sep.outputs['X'], comb.inputs['Blue'])
    links.new(comb.outputs[0], em.inputs['Color'])
    img = nodes.new('ShaderNodeTexImage'); nodes.active = img
    return m, img


for name, (mat_name, size) in SETS.items():
    part = [o for o in objs if o.name.split('__')[-1].split('.')[0] == mat_name]
    if not part:
        print('NO MESH for', name); continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.hide_render = False        # the whole rifle shades the part
    for o in part: o.select_set(True)
    bpy.context.view_layer.objects.active = part[0]
    for kind in ('ao', 'edge', 'noiseA', 'noiseB', 'place'):
        image = bpy.data.images.new(name + '_' + kind, size, size, alpha=False, float_buffer=False)
        image.colorspace_settings.name = 'Non-Color'
        if kind == 'ao':
            m = bpy.data.materials.new('ao'); m.use_nodes = True
            img = m.node_tree.nodes.new('ShaderNodeTexImage'); m.node_tree.nodes.active = img
            sc.cycles.samples = 96
        else:
            m, img = material(kind); sc.cycles.samples = 16 if kind == 'edge' else 2
        img.image = image
        for o in part:
            o.data.materials.clear(); o.data.materials.append(m)
        bpy.ops.object.bake(type='AO' if kind == 'ao' else 'EMIT')
        image.filepath_raw = os.path.join(out, 'mask_%s_%s.png' % (name, kind)); image.file_format = 'PNG'; image.save()
        print('BAKED', name, kind, size, len(part), 'objects')
        bpy.data.images.remove(image)
