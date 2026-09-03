"""
Prepares one shrub from the bush set for scattering in the yard.

Three bushes ship at 31,405 / 38,521 / 38,258 triangles -- any one of them costs
more than a quarter of the whole hall. The tallest is taken (0.79 m, the one
that actually reads as a shrub rather than a mound) and decimated hard; at this
size its silhouette carries it and the interior detail is never seen.

Unlike the grass these are OPAQUE -- their textures are JPEG with no alpha
channel -- so they cost triangles but not the fill-rate penalty alpha-cut
foliage carries on a tile-based mobile GPU.

Run:  blender --background --python shrub.py
"""
import bpy, os, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "Shrub.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")
BUDGET  = 900


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')

    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    me = max(meshes, key=lambda o: o.dimensions.z)      # the tallest
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    before = tris(me)
    d = me.modifiers.new("Decimate", 'DECIMATE')
    d.decimate_type = 'COLLAPSE'
    d.ratio = BUDGET / float(before)
    bpy.ops.object.modifier_apply(modifier=d.name)

    co = [v.co for v in me.data.vertices]
    me.data.transform(mathutils.Matrix.Translation((
        -(min(c.x for c in co) + max(c.x for c in co)) * 0.5,
        -(min(c.y for c in co) + max(c.y for c in co)) * 0.5,
        -min(c.z for c in co))))
    me.data.update()

    me.name = "Asset_Shrub"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_Shrub.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')
    dm = me.dimensions
    print(f"[shrub] {before} -> {tris(me)} tris, {dm.x:.2f} x {dm.y:.2f} x {dm.z:.2f} m")


main()
