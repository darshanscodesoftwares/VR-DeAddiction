"""Prepare the plastic monobloc chair for Unity.

    Blender --background --python BlenderAssets/scripts/plasticchair.py

The download is FOUR colour variants merged into a single 15,008-triangle mesh
spread along X, so the first job is isolating one. A histogram of vertex X finds
the gaps between them; the third cluster is taken because it has wide gaps on
both sides and so cannot clip a neighbour.

One chair is then ~3,750 triangles, which is still too many when the room holds
28 of them -- that alone would double the whole scene. Decimated to ~2,000: the
slats and the moulded curves survive, and at that count 28 chairs cost about as
much as the rest of the furniture put together.

FACING MATTERS. The environment places chairs with yaw = -angle, which assumes a
chair faces +Z in Unity with its back at -Z. Blender +Y maps to Unity -Z under
this project's export contract, so the backrest must end up at +Y here. The
script measures where the backrest actually is and rotates if it is not.
"""
import bpy, bmesh, os, sys, math
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")
# Source lives in the repo, not in a Downloads folder. It used to be read
# from ~/Downloads, which meant this script only ran on the one machine the
# asset was downloaded to -- the exported model was committed but the thing
# it came from was not, so the conversion could never be re-run elsewhere.
SRC = os.path.join(PROJECT, "BlenderAssets", "source", "PlasticChair.fbx")

X_LO, X_HI = 1.20, 2.06     # the third cluster, from the X histogram
TARGET_TRIS = 2000
SLOT = "Mat_Pub_ChairPlastic"


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    if not os.path.isfile(SRC):
        sys.exit(f"[chair] source not found: {SRC}")

    bpy.ops.import_scene.fbx(filepath=SRC)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if not meshes:
        sys.exit("[chair] no mesh in source")
    obj = max(meshes, key=lambda o: len(o.data.polygons))

    for o in list(bpy.data.objects):
        if o is not obj:
            bpy.data.objects.remove(o, do_unlink=True)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    before = tris(obj)

    # --- keep only the chosen cluster ---
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    doomed = [f for f in bm.faces
              if not (X_LO <= f.calc_center_median().x <= X_HI)]
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    obj.name = "Asset_PlasticChair"
    isolated = tris(obj)
    print(f"[chair] isolated one chair: {before} -> {isolated} tris")

    # --- centre in X/Y, then stand it on z = 0 ---
    # Done in two explicit steps. Assigning location outright after an
    # origin_set moves the geometry by the bounding-box centre, which buries
    # half the chair below the floor -- the builder places chairs by their feet.
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    obj.location = (0, 0, 0)
    bpy.ops.object.transform_apply(location=True)

    zmin = min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co.z -= zmin
    obj.data.update()

    # --- which way does it face? the backrest is the mass above the seat ---
    co = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
    obj.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    high = co[co[:, 2] > co[:, 2].max() * 0.65]
    back_y = float(high[:, 1].mean())
    print(f"[chair] backrest mean Y = {back_y:+.3f} "
          f"({'already +Y' if back_y > 0 else 'needs a 180 turn'})")

    if back_y < 0:
        obj.rotation_euler = (0, 0, math.radians(180))
        bpy.ops.object.transform_apply(rotation=True)

    # --- decimate ---
    if isolated > TARGET_TRIS:
        m = obj.modifiers.new("dec", 'DECIMATE')
        m.decimate_type = 'COLLAPSE'
        m.ratio = TARGET_TRIS / isolated
        bpy.ops.object.modifier_apply(modifier=m.name)

    # --- one slot, so Unity can tint it per chair and keep instancing ---
    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials.get(SLOT)
                              or bpy.data.materials.new(SLOT))
    for p in obj.data.polygons:
        p.use_smooth = True

    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "Asset_PlasticChair.fbx"),
        use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)

    # --- measurements the Unity colliders need ---
    co = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
    obj.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    d = obj.dimensions
    print("\n[chair] RESULT")
    print(f"  triangles : {tris(obj)}")
    print(f"  size      : {d.x:.3f} wide x {d.y:.3f} deep x {d.z:.3f} tall")
    print(f"  x range   : {co[:,0].min():+.3f} .. {co[:,0].max():+.3f}")
    print(f"  y range   : {co[:,1].min():+.3f} .. {co[:,1].max():+.3f}  (+Y = chair BACK)")
    print(f"  z range   : {co[:,2].min():+.3f} .. {co[:,2].max():+.3f}")

    # seat height: the widest horizontal band of geometry below mid-height
    zs = co[:, 2]
    band = np.histogram(zs[zs < d.z * 0.7], bins=40)
    peak = band[1][int(np.argmax(band[0]))]
    print(f"  seat approx z = {peak:.3f}")


main()
