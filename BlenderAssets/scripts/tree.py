"""
Prepares the yard tree.

Cheap for its size -- 2,948 triangles for the canopy plus 253 for the dead
branches -- but it ships at 18.9 m tall, which beside a 3.8 m eaves line reads
as a forest giant dropped behind a bar. Scaled to about 9 m: still clearly
taller than the building, still a landmark, but part of the same yard.

The two meshes are joined so the canopy is slot 0 and the dead branches slot 1,
because they need different textures and Unity fills slots in order.

Run:  blender --background --python tree.py
"""
import bpy, os, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "Tree.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")
TARGET_HEIGHT = 9.0


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
    canopy = next(o for o in meshes if 'dead' not in o.name.lower())
    others = [o for o in meshes if o is not canopy]

    bpy.ops.object.select_all(action='DESELECT')
    for o in others:
        o.select_set(True)
    canopy.select_set(True)
    bpy.context.view_layer.objects.active = canopy
    if others:
        bpy.ops.object.join()
    me = canopy
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    s = TARGET_HEIGHT / me.dimensions.z
    me.scale = (s, s, s)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    co = [v.co for v in me.data.vertices]
    me.data.transform(mathutils.Matrix.Translation((
        -(min(c.x for c in co) + max(c.x for c in co)) * 0.5,
        -(min(c.y for c in co) + max(c.y for c in co)) * 0.5,
        -min(c.z for c in co))))
    me.data.update()

    me.name = "Asset_Tree"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_Tree.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')
    d = me.dimensions
    print(f"[tree] {tris(me)} tris, scaled x{s:.3f} -> {d.x:.1f} x {d.y:.1f} x {d.z:.1f} m, "
          f"slots={[m.name for m in me.data.materials]}")


main()
