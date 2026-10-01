"""
Prepares the supermarket drinks shelf that replaces the left beverage cooler.

The source is a photogrammetry-grade scan: 105,652 triangles in a single mesh,
which is the ENTIRE rest of the scene's budget spent on one prop against a back
wall. It has to come down by a factor of ~25 before it can ship on a Quest.

It also arrives off-centre (x -0.966 .. 0.84) with no defined facing, so this
re-origins it -- centred in X and depth, base on z = 0 -- and renders both faces
so the correct yaw can be read off a picture rather than guessed.

Run:  blender --background --python drinksshelf.py
"""
import bpy, os, math, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "DrinksShelf.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")
PREVIEW = os.environ.get("SHELF_PREVIEW", "")

# 16k, not the 4k first tried. At 4k every bottle on the shelf collapsed into a
# triangular smear -- the products are small and densely packed, so they are the
# first thing collapse decimation eats. 16k keeps them readable and is
# affordable now that the scene sits at a locked 72 fps with 2 shadow maps
# instead of 6.
TARGET_TRIS = 16000


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    me = next(o for o in bpy.data.objects if o.type == 'MESH')
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    before = tris(me)

    # Collapse decimation preserves the silhouette far better than un-subdivide
    # on a scan, and the UVs come with it, which matters because the whole shelf
    # shares one texture atlas.
    d = me.modifiers.new("Decimate", 'DECIMATE')
    d.decimate_type = 'COLLAPSE'
    d.ratio = min(1.0, TARGET_TRIS / float(before))
    bpy.ops.object.modifier_apply(modifier=d.name)

    # Re-origin: centred across the front, centred in depth, base on the floor.
    co = [v.co for v in me.data.vertices]
    mn = mathutils.Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = mathutils.Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    me.data.transform(mathutils.Matrix.Translation(
        (-(mn.x + mx.x) * 0.5, -(mn.y + mx.y) * 0.5, -mn.z)))
    me.data.update()

    me.name = "Asset_DrinksShelf"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_DrinksShelf.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='EDGE',
        add_leaf_bones=False, path_mode='STRIP')

    dim = me.dimensions
    print(f"[drinksshelf] {before} -> {tris(me)} tris "
          f"({dim.x:.3f} w x {dim.y:.3f} deep x {dim.z:.3f} tall) -> {path}")

    if PREVIEW:
        render_faces(me, PREVIEW)


def render_faces(me, out):
    """One picture looking at each face, so the open side can be identified."""
    tex = os.path.join(PROJECT, "BlenderAssets", "source", "DrinksShelf_Color.jpeg")
    mat = bpy.data.materials.new("Shelf"); mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    if os.path.exists(tex):
        img = mat.node_tree.nodes.new("ShaderNodeTexImage")
        img.image = bpy.data.images.load(tex)
        mat.node_tree.links.new(b.inputs["Base Color"], img.outputs["Color"])
    me.data.materials.clear(); me.data.materials.append(mat)

    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.render.resolution_x = s.render.resolution_y = 900
    s.world = bpy.data.worlds.new("W"); s.world.use_nodes = True
    s.world.node_tree.nodes["Background"].inputs[0].default_value = (0.06, 0.06, 0.07, 1)
    for loc, e in (((3, -4, 4), 4.0), ((-3, 4, 3), 2.5), ((0, 0, 5), 1.5)):
        l = bpy.data.objects.new("L", bpy.data.lights.new("L", 'SUN'))
        l.data.energy = e; l.location = loc
        s.collection.objects.link(l)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    s.collection.objects.link(cam); s.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.2
    d = me.dimensions
    # Axis-aligned only: setting rotation_euler works headless where building a
    # look-at matrix by hand silently rendered empty frames.
    for tag, y, rx in (("negY", -4.0, math.radians(90)),
                       ("posY", 4.0, math.radians(-90))):
        cam.location = (0.0, y, d.z * 0.5)
        cam.rotation_euler = (rx, 0.0, 0.0)
        bpy.context.view_layer.update()
        s.render.filepath = f"{out}shelf_{tag}.png"
        bpy.ops.render.render(write_still=True)


main()
