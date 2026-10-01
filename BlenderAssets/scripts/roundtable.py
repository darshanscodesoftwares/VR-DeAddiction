"""Prepare the round bar table for Unity.

    Blender --background --python BlenderAssets/scripts/roundtable.py

Game-ready already at 1,242 triangles with a full PBR set, so nothing is
decimated. The work is fitting it to the environment:

  HEIGHT. It is a BAR table at 0.945 m. Every seated measurement in this project
  is built on a 0.775 m top -- the seat anchor, the eye height, where the props
  sit -- so it is scaled to that. Uniformly, which takes the diameter to 0.74 m:
  a small round table, and the reason the chair spacing and the hero table's
  prop layout both had to be redone.

  ORIGIN AT THE FLOOR, CENTRED. The builder positions tables by their base.

  Z-UP with bake_space_transform False, the same export contract as every other
  asset here -- see pubkit.export_fbx for why.
"""
import bpy, os, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")
# Source lives in the repo, not in a Downloads folder. It used to be read
# from ~/Downloads, which meant this script only ran on the one machine the
# asset was downloaded to -- the exported model was committed but the thing
# it came from was not, so the conversion could never be re-run elsewhere.
SRC = os.path.join(PROJECT, "BlenderAssets", "source", "RoundTable.fbx")

TARGET_HEIGHT = 0.775      # matches PubScenarioBuilder.TableTop
SLOT = "Mat_Pub_TableWood"


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    if not os.path.isfile(SRC):
        sys.exit(f"[roundtable] source not found: {SRC}")

    bpy.ops.import_scene.fbx(filepath=SRC)
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)

    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(objs) != 1:
        sys.exit(f"[roundtable] expected one mesh, got {[o.name for o in objs]}")
    obj = objs[0]
    obj.name = "Asset_RoundTable"

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # Bake the import transform so one Blender unit is one metre from here on.
    # Skipping this is what produced a three-metre drinking glass earlier.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    if obj.dimensions.y > obj.dimensions.z:
        obj.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)

    factor = TARGET_HEIGHT / obj.dimensions.z
    obj.scale = tuple(v * factor for v in obj.scale)   # compose, never assign
    bpy.ops.object.transform_apply(scale=True)

    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.ops.object.transform_apply(location=True)
    obj.location = (0, 0, 0)

    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials.get(SLOT)
                              or bpy.data.materials.new(SLOT))

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "Asset_RoundTable.fbx"),
        use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', use_tspace=True,
        path_mode='COPY', embed_textures=False)

    d = obj.dimensions
    print("\n[roundtable] RESULT")
    print(f"  triangles : {sum(len(p.vertices)-2 for p in obj.data.polygons)}")
    print(f"  size      : {d.x:.3f} x {d.y:.3f} x {d.z:.3f} m")
    print(f"  radius    : {max(d.x, d.y) / 2:.3f} m")
    print(f"  uv layers : {len(obj.data.uv_layers)}")


main()
