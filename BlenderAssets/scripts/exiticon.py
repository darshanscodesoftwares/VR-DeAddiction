"""Prepare the close (X) icon used as the stand-up button.

    Blender --background --python BlenderAssets/scripts/exiticon.py

The download holds six icons in one file; this takes X_button -- the cross in a
disc -- because a solid round target is far easier to grab than a thin cross,
and this is the control a patient reaches for to get out of the chair.

ORIENTATION IS THE WHOLE JOB. The icon is modelled lying flat, facing Blender
+Z. It has to end up standing upright and facing the patient:

    rotate -90 about X   ->  +Z becomes +Y
    Blender +Y           ->  Unity -Z   (this project's export contract)
    an object at yaw     ->  its local -Z points back at the head

so a -90 turn here is what makes the X face the person rather than away.
"""
import bpy, os, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")
SRC = os.path.expanduser(
    "~/Downloads/3d-icons-closeoff/extracted/close/3D.fbx")

KEEP = "X_button"
TARGET_WIDTH = 0.10        # a little wider than the 0.075 cube it replaces
SLOT = "Mat_Pub_ExitIcon"


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    if not os.path.isfile(SRC):
        sys.exit(f"[exiticon] source not found: {SRC}")

    bpy.ops.import_scene.fbx(filepath=SRC)
    obj = bpy.data.objects.get(KEEP)
    if obj is None:
        sys.exit(f"[exiticon] '{KEEP}' not in {[o.name for o in bpy.data.objects]}")

    for o in list(bpy.data.objects):
        if o is not obj:
            bpy.data.objects.remove(o, do_unlink=True)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.name = "Asset_ExitIcon"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # It already stands upright once the import transform is baked -- a -90
    # turn here laid it flat on its back. What still has to be settled is WHICH
    # WAY it faces, and that is measured rather than assumed: the embossed side
    # carries the cross and so holds far more vertices than the plain back.
    # Whichever side that is, it must end up at +Y.
    import numpy as np
    co = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
    obj.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mid = (co[:, 1].max() + co[:, 1].min()) * 0.5
    front = int((co[:, 1] > mid).sum())
    back = int((co[:, 1] <= mid).sum())
    print(f"[exiticon] vertices at +Y {front}, at -Y {back}")

    if front < back:
        print("[exiticon] detail is on -Y; turning it around")
        obj.rotation_euler = (0, 0, math.radians(180))
        bpy.ops.object.transform_apply(rotation=True)
    else:
        print("[exiticon] detail already faces +Y")

    factor = TARGET_WIDTH / max(obj.dimensions.x, obj.dimensions.z)
    obj.scale = tuple(v * factor for v in obj.scale)
    bpy.ops.object.transform_apply(scale=True)

    # Centred, not based: this hangs in the air, it does not stand on anything.
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    obj.location = (0, 0, 0)
    bpy.ops.object.transform_apply(location=True)

    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials.get(SLOT)
                              or bpy.data.materials.new(SLOT))

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "Asset_ExitIcon.fbx"),
        use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)

    d = obj.dimensions
    print("\n[exiticon] RESULT")
    print(f"  triangles : {sum(len(p.vertices)-2 for p in obj.data.polygons)}")
    print(f"  size      : {d.x:.3f} x {d.y:.3f} x {d.z:.3f} m  (thin axis = Y)")
    print(f"  uv layers : {len(obj.data.uv_layers)}")


main()
