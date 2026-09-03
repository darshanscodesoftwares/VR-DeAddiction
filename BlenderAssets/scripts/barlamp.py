"""
Prepares the bar ceiling lamp that replaces the primitive cone pendants.

Two useful properties of the source, both kept: its origin is already at the
TOP of the stem -- the point that meets the ceiling -- so a lamp placed at a
mount height hangs from it with no offset arithmetic; and its bulb is a separate
mesh, which is kept as a separate MATERIAL SLOT so Unity can drive it with the
existing emissive bulb material instead of a painted-on highlight.

8,296 triangles as shipped, and six of them go in the hall, so it is decimated.

Run:  blender --background --python barlamp.py
"""
import bpy, os, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "BarLamp.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")

TARGET_TRIS = 2000


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    body = next(o for o in meshes if 'LightBulb' not in o.name)
    bulb = next(o for o in meshes if 'LightBulb' in o.name)
    before = sum(tris(o) for o in meshes)

    # Join with the BODY active, so slot 0 is the lamp and slot 1 the bulb.
    # Unity's Model() fills slots in order, and the builder relies on that.
    bpy.ops.object.select_all(action='DESELECT')
    bulb.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    me = body

    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    d = me.modifiers.new("Decimate", 'DECIMATE')
    d.decimate_type = 'COLLAPSE'
    d.ratio = min(1.0, TARGET_TRIS / float(before))
    bpy.ops.object.modifier_apply(modifier=d.name)

    # Re-seat the mount point on z = 0 exactly. It already is, but the export
    # depends on it and a silent 1 cm offset would hang every lamp wrong.
    zs = [v.co.z for v in me.data.vertices]
    me.data.transform(mathutils.Matrix.Translation((0, 0, -max(zs))))
    me.data.update()

    me.name = "Asset_BarLamp"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_BarLamp.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')

    zs = [v.co.z for v in me.data.vertices]
    print(f"[barlamp] {before} -> {tris(me)} tris, slots="
          f"{[m.name for m in me.data.materials]}, hangs {abs(min(zs)):.3f} m "
          f"below its mount (top at z={max(zs):+.4f}) -> {path}")


main()
