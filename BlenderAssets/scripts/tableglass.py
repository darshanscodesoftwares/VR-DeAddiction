"""Prepare the plain drinking glass for Unity.

    Blender --background --python BlenderAssets/scripts/tableglass.py

The source is two concentric shells -- an outer surface of 50,624 triangles and
an inner one of 3,198 whose normals all face inward. That is a glass modelled as
two separate walls with an open rim between them, not a glass with liquid in it.

Rather than bridge the rim, this keeps the OUTER shell (which already has a base
and is already life-size at 0.114 m) and gives it thickness with Solidify. That
produces a genuinely closed, single-walled glass with a rim, which is what a
tumbler is -- and it sidesteps the ragged boundaries decimation leaves behind,
the same problem the Jack Daniel's bottle had.

50,624 triangles is absurd for a smooth cylinder seen at arm's length; the
whole reference glass for this project is 1,490. Decimated to 2,000 and
solidified, it lands near 4,000.

No textures: the source ships none, so this uses the project's existing flat
Mat_Pub_GlassClear like every other procedural prop.
"""

import bpy, bmesh, os, sys, math

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")

# Source lives in the repo, not in a Downloads folder. It used to be read
# from ~/Downloads, which meant this script only ran on the one machine the
# asset was downloaded to -- the exported model was committed but the thing
# it came from was not, so the conversion could never be re-run elsewhere.
SRC = os.path.join(PROJECT, "BlenderAssets", "source", "TableGlass.fbx")

OUTER = "Cylinder"          # the shell with a base and outward normals
INNER = "Cylinder.001"      # inner wall, discarded -- Solidify replaces it
TARGET_TRIS = 2000
TARGET_HEIGHT = 0.114       # already life size; pinned so it cannot drift


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def holes(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    n = len([e for e in bm.edges if len(e.link_faces) == 1])
    bm.free()
    return n


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    if not os.path.isfile(SRC):
        sys.exit(f"[tableglass] source FBX not found: {SRC}")

    bpy.ops.import_scene.fbx(filepath=SRC)

    for o in list(bpy.data.objects):
        if o.type != 'MESH' or o.name == INNER:
            bpy.data.objects.remove(o, do_unlink=True)

    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(objs) != 1:
        sys.exit(f"[tableglass] expected 1 mesh after cleanup, got {[o.name for o in objs]}")
    obj = objs[0]
    obj.name = "Asset_TableGlass"
    before = tris(obj)

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    # Bake the import transform FIRST, so from here on one Blender unit is one
    # metre. The FBX arrives with an object scale of about 0.037 and mesh data
    # ~3 units tall; without baking it, Solidify's thickness (a LOCAL length)
    # comes out 40x too thin, and assigning a new scale below would discard the
    # import scale entirely and produce a three-metre glass.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    m = obj.modifiers.new("dec", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = TARGET_TRIS / before
    bpy.ops.object.modifier_apply(modifier=m.name)
    after_dec = tris(obj)

    before_holes = holes(obj)
    s = obj.modifiers.new("solid", 'SOLIDIFY')
    s.thickness = 0.0025        # 2.5 mm wall, roughly a real tumbler
    s.offset = -1.0             # inward, so the outside profile is preserved
    s.use_rim = True
    bpy.ops.object.modifier_apply(modifier=s.name)

    # Single flat material: this asset has no texture, so it uses the project's
    # existing glass colour rather than introducing another one.
    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials.get("Mat_Pub_GlassClear")
                              or bpy.data.materials.new("Mat_Pub_GlassClear"))

    if obj.dimensions.y > obj.dimensions.z:
        obj.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)

    factor = TARGET_HEIGHT / obj.dimensions.z
    obj.scale = tuple(v * factor for v in obj.scale)   # compose, never assign
    bpy.ops.object.transform_apply(scale=True)

    # Origin at the base: GrabbableUpright builds its collider upward from y=0.
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.ops.object.transform_apply(location=True)
    obj.location = (0, 0, 0)

    for p in obj.data.polygons:
        p.use_smooth = True

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "Asset_TableGlass.fbx"),
        use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)

    d = obj.dimensions
    print("\n[tableglass] RESULT")
    print(f"  triangles     : {before} -> {after_dec} (decimate) -> {tris(obj)} (solidify)")
    print(f"  boundary edges: {before_holes} -> {holes(obj)}")
    print(f"  size          : {d.x:.3f} x {d.y:.3f} x {d.z:.3f} m")


main()
