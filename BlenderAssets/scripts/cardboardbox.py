"""
Prepares the low-poly cardboard box for use as stacked stock against the side
walls, replacing the flat-shaded primitive cubes that stood there.

The source is already clean -- 44 triangles, one UV map, one material, and its
origin sits at the CENTRE OF ITS BASE, which is exactly what the placement code
wants (a box put at floor height sits on the floor rather than half through it).
So the only work here is scale: the model ships as a 2 m cube, which is a crate
you could sit in, and the cases it replaces were 0.66 m across.

Run:  blender --background --python cardboardbox.py
"""
import bpy, os, math

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "CardboardBox.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")

# A carton of packaged water bottles. Scaled uniformly -- squashing a 2 m cube
# into the old 0.66 x 0.36 x 0.66 case shape would stretch the printed
# cardboard texture and fold the flap geometry flat.
TARGET = 0.55


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)

    # The FBX arrives with its transform keyframed and offset; bake it away so
    # the mesh data alone describes the box.
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    me = next(o for o in bpy.data.objects if o.type == 'MESH')
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    d = me.dimensions
    s = TARGET / max(d.x, d.y, d.z)
    me.scale = (s, s, s)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Re-seat the base on z = 0 so a box placed at floor height rests on it.
    zs = [v.co.z for v in me.data.vertices]
    me.data.transform(__import__("mathutils").Matrix.Translation((0, 0, -min(zs))))
    me.data.update()

    me.name = "Asset_CardboardBox"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_CardboardBox.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')

    d = me.dimensions
    zs = [v.co.z for v in me.data.vertices]
    print(f"[cardboardbox] {sum(len(p.vertices) - 2 for p in me.data.polygons)} tris, "
          f"{d.x:.3f} x {d.y:.3f} x {d.z:.3f} m, base z={min(zs):+.4f} -> {path}")


main()
