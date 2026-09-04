"""
Prepares the ceiling fan, with its blades kept as a SEPARATE child so they can
be spun on their own.

The whole fan is 1,148 triangles, so nothing is decimated -- the six in the hall
cost less than one ground bush.

Two things the export has to get right:

  * The BLADES stay their own object, parented under the body. Joining the fan
    into one mesh would make it un-spinnable without an animation, and a
    transform spun by a four-line script is far cheaper on a Quest than an
    Animator per fan.
  * The blade object's ORIGIN sits on the fan's vertical axis at (0,0), so
    rotating it about its own up-axis spins the blades around the shaft rather
    than swinging them around some offset point.

The model is authored at roughly 9.6 units across, which is not metres -- a real
ceiling fan is about 1.3 m -- so it is scaled to a real span rather than trusted.
Its origin is moved to the CEILING MOUNT (the top), so a fan placed at a mount
height hangs from it.

Run:  blender --background --python ceilingfan.py
"""
import bpy, os, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "CeilingFan.blend")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")

TARGET_SPAN = 1.30          # metres, blade tip to blade tip


def main():
    bpy.ops.wm.open_mainfile(filepath=SRC)

    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)     # lights, camera
    for o in bpy.data.objects:
        o.animation_data_clear()

    meshes = list(bpy.data.objects)
    # the blades are the widest object by a long way
    blades = max(meshes, key=lambda o: max(o.dimensions.x, o.dimensions.y))
    body_parts = [o for o in meshes if o is not blades]

    bpy.ops.object.select_all(action='DESELECT')
    for o in body_parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = body_parts[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = "FanBody"
    blades.name = "FanBlades"

    for o in (body, blades):
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # one shared transform for both, so they stay aligned: scale to a real span,
    # put the shaft on x = y = 0 and the ceiling mount on z = 0
    span = max(blades.dimensions.x, blades.dimensions.y)
    s = TARGET_SPAN / span
    allco = [v.co for o in (body, blades) for v in o.data.vertices]
    cx = (min(c.x for c in allco) + max(c.x for c in allco)) * 0.5
    cy = (min(c.y for c in allco) + max(c.y for c in allco)) * 0.5
    top = max(c.z for c in allco)

    M = (mathutils.Matrix.Scale(s, 4)
         @ mathutils.Matrix.Translation((-cx, -cy, -top)))
    for o in (body, blades):
        o.data.transform(M)
        o.data.update()

    blades.parent = body
    blades.matrix_parent_inverse = mathutils.Matrix.Identity(4)

    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_CeilingFan.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    blades.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')

    def z(o):
        c = [v.co for v in o.data.vertices]
        return min(c.z for c in c), max(c.z for c in c)
    bl, bh = z(blades)
    yl, yh = z(body)
    print(f"[ceilingfan] scaled x{s:.4f}  span {TARGET_SPAN:.2f} m  "
          f"hangs {abs(min(bl, yl)):.3f} m below its mount")
    print(f"[ceilingfan]   FanBody {sum(len(p.vertices)-2 for p in body.data.polygons)} tris "
          f"z {yl:+.3f}..{yh:+.3f}, slots={len(body.data.materials)}")
    print(f"[ceilingfan]   FanBlades {sum(len(p.vertices)-2 for p in blades.data.polygons)} tris "
          f"z {bl:+.3f}..{bh:+.3f}, slots={len(blades.data.materials)}")


main()
