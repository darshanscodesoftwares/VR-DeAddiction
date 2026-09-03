"""
Prepares two ground-cover variants from the grass/vegetation scatter set.

The source is a 65-mesh cloner arrangement totalling 195,697 triangles, which is
more than the entire hall. It is not usable as shipped, and most of it is not
worth using at all: measured per square metre of ground covered,

    material 1300954650   45 clumps   4130 tris   0.86 x 0.86 m   5594 tris/m2
    material 383085165    14 clumps    369 tris   0.68 x 0.67 m    816 tris/m2
    material 2785131480    5 clumps    388 tris   0.45 x 0.45 m   1937 tris/m2
    material 3457643232     1 patch   2741 tris   2.27 x 2.51 m    481 tris/m2

The single big patch covers ground more than ten times cheaper than the dense
clumps do, so bulk coverage is built from that, with the 369-tri tuft scattered
between to break up the repeat. The 4130-tri clumps are not used at all.

Run:  blender --background --python grass.py
"""
import bpy, os, mathutils

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "Grass.fbx")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")

# name, triangle budget, uniform scale.
#
# The flat 2.27 x 2.51 m patch (material 3457643232) is NOT used. It is the
# cheapest thing here per square metre and that is exactly why: it is a MAT
# 8 cm tall, so from standing height it reads as a texture on the dirt rather
# than as plants. Coverage that cannot be seen is not coverage.
#
# The tuft is scaled up instead. At 1.4x it covers 2.25x the ground for the same
# 239 triangles -- 136 tris/m2 against 265 -- and stands 0.65 m instead of
# 0.46 m, which is the height that makes it read as grass at all.
# NOT decimated any more. Both are only ~380 triangles as they come, and
# collapsing them broke the flowers: the blooms are small separate cards, so
# the decimator tore them off their stems and left yellow petals hanging in mid
# air. 130 triangles saved per plant is not worth that.
#
# The weed is also scaled DOWN. At 1.25 its blooms read as unnaturally large
# dinner-plate flowers; 0.7 puts them at roadside-weed size.
# The TUFT was the oversized one, not the weed. It was scaled 2.0x to buy
# coverage cheaply -- 3.60 m2 of ground for 369 triangles -- and the result was
# dandelions the size of shrubs. 1.15x is a roadside weed: 0.78 x 0.77 m,
# 0.37 m tall.
#
# That costs coverage. A plant at 1.15x covers a quarter of what it did at 2.0x
# for the same triangles, so the same triangle budget now buys about a third of
# the ground it did. Sparse weeds on dirt is what this yard should look like
# anyway; a 70% mat of greenery would read as a lawn.
VARIANTS = [("383085165",  "Asset_GrassTuft", 100000, 1.15),
            ("2785131480", "Asset_GrassWeed", 100000, 0.55)]


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

    os.makedirs(OUTDIR, exist_ok=True)
    for mat, name, budget, scale in VARIANTS:
        cands = [o for o in bpy.data.objects
                 if o.type == 'MESH' and o.data.materials
                 and o.data.materials[0].name.startswith(mat)]
        # the largest instance of that variant, so one patch covers most ground
        me = max(cands, key=lambda o: o.dimensions.x * o.dimensions.y)

        bpy.ops.object.select_all(action='DESELECT')
        me.select_set(True)
        bpy.context.view_layer.objects.active = me
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

        before = tris(me)
        if before > budget:
            d = me.modifiers.new("Decimate", 'DECIMATE')
            d.decimate_type = 'COLLAPSE'
            d.ratio = budget / float(before)
            bpy.ops.object.modifier_apply(modifier=d.name)

        if abs(scale - 1.0) > 1e-4:
            me.scale = (scale, scale, scale)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

        # Centre horizontally and sit the base on z = 0, so a patch dropped at a
        # ground point sits ON the ground rather than half sunk into it.
        co = [v.co for v in me.data.vertices]
        me.data.transform(mathutils.Matrix.Translation((
            -(min(c.x for c in co) + max(c.x for c in co)) * 0.5,
            -(min(c.y for c in co) + max(c.y for c in co)) * 0.5,
            -min(c.z for c in co))))
        me.data.update()

        me.name = name
        path = os.path.join(OUTDIR, name + ".fbx")
        bpy.ops.object.select_all(action='DESELECT')
        me.select_set(True)
        bpy.context.view_layer.objects.active = me
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, global_scale=1.0,
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            bake_space_transform=False, object_types={'MESH'},
            use_mesh_modifiers=True, mesh_smooth_type='EDGE',
            add_leaf_bones=False, path_mode='STRIP')
        d = me.dimensions
        print(f"[grass] {name}: {before} -> {tris(me)} tris, "
              f"{d.x:.2f} x {d.y:.2f} m footprint ({d.x*d.y:.2f} m2), "
              f"{tris(me)/max(d.x*d.y,0.01):.0f} tris/m2")


main()
