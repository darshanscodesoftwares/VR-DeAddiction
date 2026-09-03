"""
Prepares the gooseneck wall light used outside the building.

Two things must be fixed before it can be placed by coordinate:

  * Its origin is a bounding-box CORNER, not the point that touches the wall.
    Placed as-is every fixture would sit a third of a metre off its mounting
    point in two axes.
  * Its emissive strip is a separate mesh. That is kept as a separate MATERIAL
    SLOT so Unity can drive it with the emissive bulb material and the fixture
    reads as switched on rather than painted.

The mount point is derived, not guessed: the wall plate is the geometry lying
against the fixture's back plane, so the origin is moved to the centroid of
every vertex within 5 mm of minimum Y, on that plane. After this the model sits
at Blender +Y = out of the wall, hanging its shade below and forward, and a
fixture placed AT a wall surface point mounts flush to it.

Run:  blender --background --python walllight.py
"""
import bpy, os, math, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "WallLight.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    # Unparent first. The meshes hang off an "alton_light" EMPTY that carries
    # the Maya centimetre-to-metre 0.01 scale, and transform_apply on a CHILD
    # does not include its parent's transform -- so applying without this bakes
    # nothing and exports a fixture 100x too big, which in Unity is a 39 m light
    # fitting rather than a 39 cm one.
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')

    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    body = next(o for o in meshes if 'emmis' not in o.name.lower())
    emis = next(o for o in meshes if 'emmis' in o.name.lower())

    # Join with the BODY active so slot 0 is the fixture, slot 1 the emitter.
    bpy.ops.object.select_all(action='DESELECT')
    emis.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    me = body
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Which end is the wall plate? Derived from the EMITTER, not from an
    # assumption about min/max Y. The first attempt took the plate to be at
    # minimum Y; the emitter strip is what sits there, so the fixture mounted
    # backwards -- plate waving in the air, glowing strip pressed into the
    # brick. The plate is simply the end furthest from the emitter.
    emis_slot = [i for i, m in enumerate(me.data.materials)
                 if 'lambert2' in (m.name if m else '')]
    emis_v = {v for poly in me.data.polygons if poly.material_index in emis_slot
              for v in poly.vertices}
    emisY = sum(me.data.vertices[i].co.y for i in emis_v) / len(emis_v)

    co = [v.co for v in me.data.vertices]
    lo, hi = min(c.y for c in co), max(c.y for c in co)
    plateY = lo if abs(emisY - hi) < abs(emisY - lo) else hi
    plate = [c for c in co if abs(c.y - plateY) < 0.005]
    mount = mathutils.Vector((sum(c.x for c in plate) / len(plate),
                              plateY,
                              sum(c.z for c in plate) / len(plate)))
    me.data.transform(mathutils.Matrix.Translation(-mount))

    # Put the arm on +Y, whichever end the plate turned out to be, so placement
    # code has one convention to rely on.
    if emisY - plateY < 0:
        me.data.transform(mathutils.Matrix.Rotation(math.pi, 4, 'Z'))
    me.data.update()

    ev = [me.data.vertices[i].co for i in emis_v]
    print(f"   emitter now at y={sum(v.y for v in ev)/len(ev):+.3f} "
          f"(must be POSITIVE = away from wall), plate on origin")

    me.name = "Asset_WallLight"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_WallLight.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')

    co = [v.co for v in me.data.vertices]
    # where the emitter ended up, so the Unity light can be put at it
    em = [v.co for v in me.data.vertices]
    print(f"[walllight] {sum(len(p.vertices)-2 for p in me.data.polygons)} tris, "
          f"slots={[m.name for m in me.data.materials]}")
    print(f"   plate centred on origin; body spans "
          f"x {min(c.x for c in co):+.3f}..{max(c.x for c in co):+.3f}  "
          f"y {min(c.y for c in co):+.3f}..{max(c.y for c in co):+.3f} (out of wall)  "
          f"z {min(c.z for c in co):+.3f}..{max(c.z for c in co):+.3f}")
    print(f"   -> {path}")


main()
