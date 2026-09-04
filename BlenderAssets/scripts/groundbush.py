"""
Prepares the yard ground bush (ornamental grass with seed heads).

NOT the same asset as shrub.py's bush, which is withdrawn. This one is usable
where that one was not, and the difference is worth stating because it is the
thing to test first on any foliage download:

    shrub.fbx    31,405 tris -> floors at 4,194 (13%)   unusable, shreds
    case_evy.fbx 766,823 tris -> floors at 420,270 (55%) unusable at all
    bush.fbx     128,970 tris -> reaches 2,380 (1.8%)    reduces cleanly

A mesh of DISCONNECTED leaf cards cannot be collapsed past two triangles per
card; one with connected geometry can. Checking the floor takes one run and
saves building on an asset that cannot ship.

Budget 12,000, and the way that was arrived at matters. Slot 1 is 79,230 of the
model's 102,289 faces -- it is almost entirely LEAVES -- so decimation spends
its cuts there and the leaves are what disappears first. At 5,000 the result is
a bare winter bush: branches with a scatter of leaves. At 12,000 it reads as
foliage. Judged by rendering it, not by the triangle count, because the count
says nothing about which geometry survived.

The source is authored in CENTIMETRES (about 150 units across), so it is scaled
to a real bush height rather than trusting the file.

Run:  blender --background --python groundbush.py
"""
import bpy, os, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "GroundBush.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")

# Per-material budgets, NOT one figure for the whole mesh.
#
# A single decimate pass spends its cuts wherever collapsing is cheapest, and on
# this model that is the leaves: they are small separate cards, while the
# branches are connected tubes that resist collapsing. So a uniform budget eats
# the foliage and keeps the twigs -- at 12,000 the bush still came out as a
# thicket of bare branches with a haze of leaves, which is not what the model is.
#
# Splitting by material and decimating each separately spends the budget where
# it shows. The branches lose 90% of their geometry without looking any
# different, because a tube reduced is still a tube.
BRANCH_BUDGET = 6500        # slot 0, 23,059 faces as shipped -- a tube reduced
                            # is still a tube, so this one reduces hard. At
                            # 2,500 it does not: the branches become shards.
LEAF_BUDGET   = 22000       # slot 1, 79,230 faces as shipped.
                            #
                            # Density here can ONLY come from geometry, which is
                            # worth recording because the obvious alternative was
                            # tried and does not work. An alpha leaf texture was
                            # generated and applied: it made the bush THINNER,
                            # not denser, because this model's cards are already
                            # leaf-sized quads. Alpha can only subtract from a
                            # silhouette. The publisher's render looks richer
                            # because it has 128,970 triangles of cards, not
                            # because of its texture.
                            #
                            # Geometry has a measured ceiling: 12,000 leaves put
                            # the scene at 243k triangles and 90% of frames on
                            # budget; 34,000 put it at 309k and 71%. 22,000 is
                            # the middle, chosen to be measured rather than
                            # assumed.
                            # original, and the foliage is what the eye reads,
                            # so this is where the budget goes. 12,000 still
                            # looked thin against the reference render.
TARGET_HEIGHT = 1.05


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')

    me = max((o for o in bpy.data.objects if o.type == 'MESH'),
             key=lambda o: len(o.data.polygons))
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True)
    bpy.context.view_layer.objects.active = me
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    before = tris(me)

    # split by material so each can be reduced on its own terms
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL')
    bpy.ops.object.mode_set(mode='OBJECT')

    parts = [o for o in bpy.context.selected_objects if o.type == 'MESH']
    def part_material(o):
        if not o.data.polygons:
            return ""
        idx = o.data.polygons[0].material_index
        m = o.data.materials[idx] if idx < len(o.data.materials) else None
        return m.name if m else ""

    # the LEAF part is the one with far more faces -- identified by count, not
    # by slot order, which separation does not promise to preserve
    parts.sort(key=lambda o: len(o.data.polygons))
    branch, leaf = parts[0], parts[-1]
    print(f"[groundbush]   branches {len(branch.data.polygons)} faces "
          f"('{part_material(branch)}'), leaves {len(leaf.data.polygons)} faces "
          f"('{part_material(leaf)}')")

    for obj, budget in ((branch, BRANCH_BUDGET), (leaf, LEAF_BUDGET)):
        n = tris(obj)
        if n <= budget:
            continue
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        d = obj.modifiers.new("Decimate", 'DECIMATE')
        d.decimate_type = 'COLLAPSE'
        d.ratio = budget / float(n)
        bpy.ops.object.modifier_apply(modifier=d.name)
        print(f"[groundbush]   {obj.name}: {n} -> {tris(obj)} tris")

    # rejoin with BRANCHES active, so slot 0 stays branches and slot 1 leaves --
    # the builder assigns materials by that order
    bpy.ops.object.select_all(action='DESELECT')
    leaf.select_set(True)
    branch.select_set(True)
    bpy.context.view_layer.objects.active = branch
    bpy.ops.object.join()
    me = branch

    s = TARGET_HEIGHT / me.dimensions.z
    me.scale = (s, s, s)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # Centre in XY and sit the base exactly on z = 0. The brief was explicit
    # that these must not float, and a base anywhere but zero makes that a
    # placement problem forever after.
    co = [v.co for v in me.data.vertices]
    me.data.transform(mathutils.Matrix.Translation((
        -(min(c.x for c in co) + max(c.x for c in co)) * 0.5,
        -(min(c.y for c in co) + max(c.y for c in co)) * 0.5,
        -min(c.z for c in co))))
    me.data.update()

    me.name = "Asset_GroundBush"
    os.makedirs(OUTDIR, exist_ok=True)
    path = os.path.join(OUTDIR, "Asset_GroundBush.fbx")
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
    dm = me.dimensions
    per = {}
    for poly in me.data.polygons:
        per[poly.material_index] = per.get(poly.material_index, 0) + 1
    print(f"[groundbush] faces per slot after join: {per}")
    print(f"[groundbush] {before} -> {tris(me)} tris, scaled x{s:.4f} -> "
          f"{dm.x:.2f} x {dm.y:.2f} x {dm.z:.2f} m, base z={min(c.z for c in co):+.4f}, "
          f"slots={[m.name for m in me.data.materials]}")


main()
