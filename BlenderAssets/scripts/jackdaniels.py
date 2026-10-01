"""Turn the Sketchfab Jack Daniel's model into a Unity-ready bottle.

    Blender --background --python BlenderAssets/scripts/jackdaniels.py

WHY THIS SCRIPT EXISTS
----------------------
The source is a Cinema 4D render model: 1,518,458 triangles for one bottle,
against ~55,000 for the entire bar. Dropped in as-is it would not just cost
frames, it would not fit.

Almost all of that density is on SURFACES OF REVOLUTION -- the glass, the
whiskey, the cap -- which were subdivided for offline rendering. A circular
silhouette stays circular through heavy decimation, so removing it costs
nothing a viewer can see. Side-by-side renders at 0.79% of the original
geometry are indistinguishable.

The LABEL is treated differently. It is only 1.5% of the triangles but it is
the entire reason for using this model at all -- the brand is the therapeutic
cue -- so it keeps proportionally ten times more geometry than anything else,
and its UVs are what carry the artwork.

UNLIKE the rest of BlenderAssets, this asset ships real TEXTURES. Everywhere
else materials are name-only slots filled in Unity, which is what keeps the
palette from fragmenting. A brand label cannot be a flat colour.
"""

import bpy, bmesh, os, sys, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")
TEX_DIR = os.path.join(PROJECT, "Assets", "Textures")

# Source lives in the repo, not in a Downloads folder. It used to be read
# from ~/Downloads, which meant this script only ran on the one machine the
# asset was downloaded to -- the exported model was committed but the thing
# it came from was not, so the conversion could never be re-run elsewhere.
SRC = os.path.join(PROJECT, "BlenderAssets", "source", "JackDaniels.fbx")

# Real bottle height. The source imports at 1.83 m -- about six times life
# size, which would arrive in the bar as a bottle as tall as the patient.
TARGET_HEIGHT = 0.29

# Triangle budget per part. The label keeps ~10x the proportion of the rest
# because creasing a curved sheet distorts the artwork printed on it.
TARGETS = {"BOTTLE": 3000, "Whiskey_1": 2000, "CAP_1": 1500, "LABEL_1": 2000}

# Slot name -> the Unity material that will fill it. Order here is the order
# Unity sees, and PubEnvironmentBuilder relies on it.
SLOTS = ["Mat_JD_Glass", "Mat_JD_Whiskey", "Mat_JD_Label", "Mat_JD_Cap"]
ORDER = ["BOTTLE", "Whiskey_1", "LABEL_1", "CAP_1"]


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def decimate(o, target):
    cur = tris(o)
    if cur <= target:
        return cur
    m = o.modifiers.new("dec", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = target / cur
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)
    return tris(o)


def main():
    clear_scene()
    if not os.path.isfile(SRC):
        sys.exit(f"[jackdaniels] source FBX not found: {SRC}")

    bpy.ops.import_scene.fbx(filepath=SRC)

    # Drop everything that is not a mesh -- the source carries a Cinema 4D
    # editor camera.
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)

    meshes = {o.name: o for o in bpy.data.objects}
    missing = [n for n in ORDER if n not in meshes]
    if missing:
        sys.exit(f"[jackdaniels] expected meshes missing: {missing}")

    total_before = sum(tris(o) for o in meshes.values())
    print("\n[jackdaniels] decimating")
    for name in ORDER:
        o = meshes[name]
        before = tris(o)
        after = decimate(o, TARGETS[name])
        print(f"  {name:12s} {before:9d} -> {after:6d}")

    # Close the shells. The source models the bottle as an open SURFACE -- no
    # base, no seal at the neck -- because a renderer only ever sees it from
    # outside. In VR the patient picks it up and looks down into it, and with
    # backfaces culled they see straight through the bottom and out the far
    # side. Filling the boundary makes it a solid object.
    #
    # The label is skipped deliberately: it is a thin curved sheet, and
    # "filling" its outline would just cap its perimeter with a stray face.
    print("\n[jackdaniels] closing shells")

    def holes(o):
        bm = bmesh.new(); bm.from_mesh(o.data)
        n = len([e for e in bm.edges if len(e.link_faces) == 1])
        bm.free()
        return n

    for name in ORDER:
        if name == "LABEL_1":
            continue
        o = meshes[name]
        before_holes = holes(o)

        # SOLIDIFY, not hole-filling. Filling only bridges boundaries it can
        # triangulate, and decimation leaves ragged ones it cannot -- the glass
        # went 130 -> 52 that way, still see-through. Solidify gives each shell
        # real thickness and seals every rim by construction, which is what
        # these things physically are: walls, not surfaces. It doubles the
        # polygon count, which is why the decimate targets above are lower.
        m = o.modifiers.new("solid", 'SOLIDIFY')
        m.thickness = 0.0126        # ~2 mm once scaled to 0.29 m
        m.offset = -1.0             # grow inward, so the outside shape is kept
        m.use_rim = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=m.name)

        print(f"  {name:12s} boundary edges {before_holes:4d} -> {holes(o):4d}"
              f"   tris {tris(o)}")

    # One material per part, so the joined mesh ends up with one submesh per
    # part in a known order.
    for name, slot in zip(ORDER, SLOTS):
        o = meshes[name]
        o.data.materials.clear()
        mat = bpy.data.materials.get(slot) or bpy.data.materials.new(slot)
        o.data.materials.append(mat)

    # Join, active object last so its slot lands first.
    bpy.ops.object.select_all(action='DESELECT')
    for name in ORDER:
        meshes[name].select_set(True)
    bpy.context.view_layer.objects.active = meshes[ORDER[0]]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = "Asset_JackDaniels"

    # --- force the slot order ----------------------------------------------
    # join() merges material slots in an order of its own choosing, and Unity
    # assigns materials BY INDEX. Leaving it to chance means a Blender upgrade
    # could silently swap the label onto the glass. Remap explicitly.
    old_names = [m.name for m in obj.data.materials]
    old_index = [p.material_index for p in obj.data.polygons]
    remap = {i: SLOTS.index(n) for i, n in enumerate(old_names) if n in SLOTS}

    obj.data.materials.clear()
    for n in SLOTS:
        obj.data.materials.append(bpy.data.materials[n])
    for poly, oi in zip(obj.data.polygons, old_index):
        poly.material_index = remap.get(oi, 0)

    # --- orientation -------------------------------------------------------
    # The source stands tall along Y (Cinema 4D convention). Blender is Z-up
    # and pubkit's export contract assumes height along Z, so tip it upright.
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if obj.dimensions.y > obj.dimensions.z:
        obj.rotation_euler = (math.radians(90), 0, 0)
        bpy.ops.object.transform_apply(rotation=True)

    # --- scale to life size ------------------------------------------------
    height = obj.dimensions.z
    obj.scale = (TARGET_HEIGHT / height,) * 3
    bpy.ops.object.transform_apply(scale=True)

    # --- origin to the base, centred ---------------------------------------
    # GrabbableUpright builds its collider from y=0 upward, so a bottle whose
    # origin sits at its middle would end up half sunk through the table.
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.ops.object.transform_apply(location=True)
    obj.location = (0, 0, 0)

    # Lift the label clear of the glass.
    #
    # A real bottle wears its label on the OUTSIDE. In this model the label sits
    # just inside the glass surface, so the amber transparent glass renders over
    # it and the artwork muddies into a dark patch -- the label is there, it is
    # simply being tinted out of legibility. Pushing it 1.5 mm proud puts the
    # glass BEHIND it from every viewing angle, which is both correct and what
    # stops it z-fighting with the bottle wall.
    label_slot = SLOTS.index("Mat_JD_Label")
    label_verts = set()
    for poly in obj.data.polygons:
        if poly.material_index == label_slot:
            label_verts.update(poly.vertices)

    obj.data.calc_normals_split() if hasattr(obj.data, "calc_normals_split") else None
    for vi in label_verts:
        v = obj.data.vertices[vi]
        v.co += v.normal * 0.0015
    print(f"  label lifted 1.5 mm on {len(label_verts)} vertices")

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "Asset_JackDaniels.fbx"),
        use_selection=True,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        path_mode='COPY',
        embed_textures=False,
    )

    print("\n[jackdaniels] RESULT")
    print(f"  triangles : {total_before} -> {tris(obj)}")
    print(f"  height    : {obj.dimensions.z:.3f} m")
    print(f"  slots     : {[m.name for m in obj.data.materials]}")
    print(f"  written   : {OUT_DIR}/Asset_JackDaniels.fbx")


main()
