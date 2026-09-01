"""Prepare the Sketchfab whiskey glass for Unity.

    Blender --background --python BlenderAssets/scripts/whiskeyglass.py

Unlike the Jack Daniel's bottle, this one arrives GAME READY: 1,490 triangles
across two meshes, already baked to a low-poly with normal maps, already UV'd.
There is nothing to decimate -- doing so would only damage a mesh that is
already at the right budget.

So this script only does the things Unity needs and the source does not know
about: life-size scale, height along Z, origin at the base, and a deterministic
material slot order.
"""

import bpy, os, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")

SRC = os.path.expanduser(
    "~/Downloads/whiskey-glass/extracted/SlHomeWhGL_Scetchab.fbx")

# Matches the procedural Asset_Glass it replaces (height 0.100), so the rest of
# the table's proportions still read correctly.
TARGET_HEIGHT = 0.10

# Source mesh -> Unity material slot. Order here is the order Unity sees.
ORDER = ["WhGlass_low", "WhWh_low"]
SLOTS = ["Mat_WG_Glass", "Mat_WG_Whiskey"]


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    if not os.path.isfile(SRC):
        sys.exit(f"[whiskeyglass] source FBX not found: {SRC}")

    bpy.ops.import_scene.fbx(filepath=SRC)

    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)

    meshes = {o.name: o for o in bpy.data.objects}
    missing = [n for n in ORDER if n not in meshes]
    if missing:
        sys.exit(f"[whiskeyglass] expected meshes missing: {missing} "
                 f"(found {list(meshes)})")

    before = sum(tris(o) for o in meshes.values())

    # One material per part. The source also carries two junk materials from
    # its authoring tool ("Material", "Dots Stroke") which are dropped here.
    for name, slot in zip(ORDER, SLOTS):
        o = meshes[name]
        o.data.materials.clear()
        o.data.materials.append(bpy.data.materials.get(slot)
                                or bpy.data.materials.new(slot))

    bpy.ops.object.select_all(action='DESELECT')
    for name in ORDER:
        meshes[name].select_set(True)
    bpy.context.view_layer.objects.active = meshes[ORDER[0]]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = "Asset_WhiskeyGlass"

    # join() picks its own slot order and Unity assigns materials BY INDEX,
    # so pin it rather than trusting the result.
    old_names = [m.name for m in obj.data.materials]
    old_index = [p.material_index for p in obj.data.polygons]
    remap = {i: SLOTS.index(n) for i, n in enumerate(old_names) if n in SLOTS}
    obj.data.materials.clear()
    for n in SLOTS:
        obj.data.materials.append(bpy.data.materials[n])
    for poly, oi in zip(obj.data.polygons, old_index):
        poly.material_index = remap.get(oi, 0)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    if obj.dimensions.y > obj.dimensions.z:
        obj.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)

    obj.scale = (TARGET_HEIGHT / obj.dimensions.z,) * 3
    bpy.ops.object.transform_apply(scale=True)

    # Origin at the base: GrabbableUpright builds its collider upward from
    # y=0, so a centred origin would sink the glass through the table.
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.ops.object.transform_apply(location=True)
    obj.location = (0, 0, 0)

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "Asset_WhiskeyGlass.fbx"),
        use_selection=True,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_tspace=True,          # export tangents: the normal maps need them
        path_mode='COPY',
        embed_textures=False,
    )

    print("\n[whiskeyglass] RESULT")
    print(f"  triangles : {before} -> {tris(obj)} (unchanged by design)")
    print(f"  height    : {obj.dimensions.z:.3f} m")
    print(f"  slots     : {[m.name for m in obj.data.materials]}")
    print(f"  uv layers : {len(obj.data.uv_layers)}")


main()
