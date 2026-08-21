"""Build all pub assets and export them to Assets/Models/.

    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --python BlenderAssets/scripts/build_all.py

Each asset is modelled from scratch in a fresh scene, so assets cannot leak
geometry into each other and any one can be rebuilt in isolation.
"""

import os
import sys

# Blender does not put the script's own directory on sys.path.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pubkit  # noqa: E402

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(PROJECT, "Assets", "Models")


# ---------------------------------------------------------------- glassware


def build_bottle():
    """Quart-style beer bottle, 0.30 m tall, origin at the base.

    Profile follows a real bottle: straight body, rounded shoulder, long neck,
    slight lip flare. The shoulder is where a lathe reads as a real bottle
    rather than a cylinder, so it gets the most profile points.
    """
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0355),   # base edge
        (0.008, 0.0375),   # heel
        (0.020, 0.0380),
        (0.150, 0.0380),   # body
        (0.168, 0.0372),
        (0.190, 0.0330),   # shoulder begins
        (0.208, 0.0262),
        (0.222, 0.0190),
        (0.234, 0.0148),
        (0.250, 0.0135),   # neck
        (0.284, 0.0133),
        (0.291, 0.0154),   # lip
        (0.300, 0.0150),
    ]

    obj = pubkit.lathe("Asset_Bottle", profile, segments=16)
    pubkit.shade_smooth(obj)
    pubkit.grime(obj, floor_dirt=0.18, crevice=0.20, mottle=0.06, seed=11)
    return obj


def build_glass():
    """Short tumbler, 0.10 m tall. Slight taper, thick base."""
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0330),
        (0.010, 0.0345),
        (0.030, 0.0358),
        (0.070, 0.0372),
        (0.098, 0.0380),
        (0.100, 0.0380),
    ]

    obj = pubkit.lathe("Asset_Glass", profile, segments=14)
    pubkit.shade_smooth(obj)
    pubkit.grime(obj, floor_dirt=0.22, crevice=0.18, mottle=0.07, seed=12)
    return obj


def _bottle_with_label(name, profile, segments, label_rings, foil_rings, cap_rings,
                       grime_seed):
    """Shared bottle builder with material slots for glass / label / foil / cap.

    Slot names are matched in Unity to real project materials, so a label is a
    genuinely different colour rather than a tint of the glass. Slot order:
      0 GLASS, 1 LABEL, 2 FOIL, 3 CAP
    """
    obj = pubkit.lathe(name, profile, segments=segments)
    pubkit.shade_smooth(obj)

    pubkit.set_material_slots(obj, ["GLASS", "LABEL", "FOIL", "CAP", "LIQUID"])

    n = len(profile)
    pubkit.assign_faces_to_slot(
        obj, pubkit.lathe_ring_faces(n, segments, *label_rings), 1)
    pubkit.assign_faces_to_slot(
        obj, pubkit.lathe_ring_faces(n, segments, *foil_rings), 2)
    pubkit.assign_faces_to_slot(
        obj, pubkit.lathe_ring_faces(n, segments, *cap_rings), 3)

    pubkit.grime(obj, floor_dirt=0.20, crevice=0.22, mottle=0.06, seed=grime_seed)
    return obj


def build_beer_bottle():
    """650 ml green quart beer bottle with a body label and foil neck.

    The profile gains extra rings purely so the label has clean edges to sit
    between -- a label that ends mid-curve reads as a smudge.
    """
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0360),
        (0.006, 0.0392),
        (0.018, 0.0400),
        (0.055, 0.0400),   # label bottom
        (0.150, 0.0400),   # label top
        (0.170, 0.0400),
        (0.192, 0.0388),
        (0.216, 0.0330),
        (0.234, 0.0242),
        (0.248, 0.0168),
        (0.258, 0.0138),
        (0.272, 0.0134),   # foil bottom
        (0.286, 0.0132),
        (0.294, 0.0152),   # crown cap
        (0.302, 0.0146),
    ]

    return _bottle_with_label("Asset_BeerBottle", profile, 16,
                              label_rings=(3, 5), foil_rings=(11, 13),
                              cap_rings=(13, 14), grime_seed=13)


def build_whisky_quart():
    """Quart spirit bottle -- the flat-shouldered shape sold at TASMAC counters.

    Shorter and squarer than the beer bottle, with a tall paper label covering
    most of the body and a screw cap.
    """
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0340),
        (0.007, 0.0368),
        (0.020, 0.0375),
        (0.045, 0.0375),   # label bottom
        (0.165, 0.0375),   # label top
        (0.180, 0.0368),
        (0.198, 0.0322),   # sharper shoulder than beer
        (0.212, 0.0238),
        (0.222, 0.0170),
        (0.230, 0.0148),
        (0.258, 0.0146),
        (0.266, 0.0158),   # screw cap
        (0.284, 0.0156),
        (0.288, 0.0140),
    ]

    return _bottle_with_label("Asset_WhiskyQuart", profile, 16,
                              label_rings=(3, 5), foil_rings=(9, 11),
                              cap_rings=(11, 13), grime_seed=17)


def build_brandy_half():
    """Half bottle -- squat, wide label, cork-style top. Adds shelf variety."""
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0300),
        (0.006, 0.0328),
        (0.018, 0.0335),
        (0.038, 0.0335),   # label bottom
        (0.128, 0.0335),   # label top
        (0.142, 0.0326),
        (0.160, 0.0272),
        (0.174, 0.0196),
        (0.184, 0.0150),
        (0.206, 0.0148),
        (0.214, 0.0166),   # cap
        (0.230, 0.0162),
        (0.234, 0.0146),
    ]

    return _bottle_with_label("Asset_BrandyHalf", profile, 14,
                              label_rings=(3, 5), foil_rings=(8, 10),
                              cap_rings=(10, 12), grime_seed=19)


def _bottle_shaped(name, profile, segments, label_rings, foil_rings, cap_rings,
                   grime_seed, section="round", flutes=12, flute_depth=0.10,
                   liquid_level=None):
    """Bottle with a non-round cross-section and optional visible liquid.

    liquid_level: fraction of body height the contents fill. A separate inner
    lathe, slightly inside the glass, so through clear glass you read a colour
    and a fill line -- which is most of what makes a bottle look full or empty.
    """
    obj = pubkit.lathe(name, profile, segments=segments,
                       section=section, flutes=flutes, flute_depth=flute_depth)
    pubkit.shade_smooth(obj)

    parts = [obj]

    if liquid_level is not None:
        top_h = profile[0][0] + (profile[-1][0] - profile[0][0]) * liquid_level
        inner = [(h, r * 0.93) for (h, r) in profile if h <= top_h]
        if len(inner) >= 2:
            inner.append((top_h, inner[-1][1]))
            liquid = pubkit.lathe(name + "_Liquid", inner, segments=segments,
                                  section=section, flutes=flutes,
                                  flute_depth=flute_depth)
            pubkit.shade_smooth(liquid)
            parts.append(liquid)

    merged = pubkit.join(name, parts) if len(parts) > 1 else obj

    pubkit.set_material_slots(merged, ["GLASS", "LABEL", "FOIL", "CAP", "LIQUID"])

    n = len(profile)
    pubkit.assign_faces_to_slot(
        merged, pubkit.lathe_ring_faces(n, segments, *label_rings), 1)
    pubkit.assign_faces_to_slot(
        merged, pubkit.lathe_ring_faces(n, segments, *foil_rings), 2)
    pubkit.assign_faces_to_slot(
        merged, pubkit.lathe_ring_faces(n, segments, *cap_rings), 3)

    # Liquid faces are everything after the glass mesh's own face count.
    glass_faces = (n - 1) * segments + 2
    for fi in range(glass_faces, len(merged.data.polygons)):
        merged.data.polygons[fi].material_index = 4
    merged.data.update()

    pubkit.grime(merged, floor_dirt=0.16, crevice=0.18, mottle=0.05, seed=grime_seed)
    return merged


def build_bottle_fluted():
    """Tall fluted bottle -- the Italicus/St-Germain silhouette.

    Vertical ribs down the body, narrow waist, long tapered neck. The fluting
    is what reads at a glance; no label text is needed to tell it apart.
    """
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0320),
        (0.008, 0.0348),
        (0.022, 0.0352),
        (0.050, 0.0352),   # label bottom
        (0.150, 0.0352),   # label top
        (0.182, 0.0348),
        (0.206, 0.0316),
        (0.228, 0.0244),
        (0.244, 0.0172),
        (0.256, 0.0136),
        (0.292, 0.0132),
        (0.300, 0.0150),   # cap
        (0.322, 0.0148),
        (0.326, 0.0132),
    ]

    return _bottle_shaped("Asset_BottleFluted", profile, 24,
                          label_rings=(3, 5), foil_rings=(9, 11),
                          cap_rings=(11, 13), grime_seed=31,
                          section="fluted", flutes=16, flute_depth=0.13,
                          liquid_level=0.62)


def build_bottle_square():
    """Square-section bottle -- the Mr Black silhouette.

    Rounded-rectangle cross-section, broad flat shoulder, short neck. Reads as
    a premium spirit even in silhouette, which is the point.
    """
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0330),
        (0.008, 0.0352),
        (0.020, 0.0356),
        (0.045, 0.0356),   # label bottom
        (0.155, 0.0356),   # label top
        (0.176, 0.0356),
        (0.190, 0.0330),   # abrupt shoulder
        (0.202, 0.0250),
        (0.210, 0.0170),
        (0.216, 0.0142),
        (0.250, 0.0140),
        (0.258, 0.0164),   # cap
        (0.280, 0.0162),
        (0.284, 0.0142),
    ]

    return _bottle_shaped("Asset_BottleSquare", profile, 20,
                          label_rings=(3, 5), foil_rings=(9, 11),
                          cap_rings=(11, 13), grime_seed=33,
                          section="square", liquid_level=0.58)


def build_bottle_tall():
    """Tall slim bottle with coloured contents -- the Zirbenz silhouette."""
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0300),
        (0.008, 0.0322),
        (0.022, 0.0326),
        (0.055, 0.0326),   # label bottom
        (0.145, 0.0326),   # label top
        (0.205, 0.0326),
        (0.232, 0.0310),
        (0.256, 0.0242),
        (0.272, 0.0168),
        (0.282, 0.0136),
        (0.318, 0.0134),
        (0.326, 0.0156),   # cap
        (0.346, 0.0154),
        (0.350, 0.0136),
    ]

    return _bottle_shaped("Asset_BottleTall", profile, 18,
                          label_rings=(3, 5), foil_rings=(9, 11),
                          cap_rings=(11, 13), grime_seed=35,
                          liquid_level=0.70)


def build_steel_tumbler():
    """Small steel tumbler. Straight taper, rolled rim, recessed base."""
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0270),
        (0.004, 0.0290),
        (0.020, 0.0305),
        (0.060, 0.0330),
        (0.086, 0.0348),
        (0.090, 0.0352),
    ]

    obj = pubkit.lathe("Asset_SteelTumbler", profile, segments=14)
    pubkit.shade_smooth(obj)
    pubkit.grime(obj, floor_dirt=0.24, crevice=0.26, mottle=0.09, seed=14)
    return obj


# ---------------------------------------------------------------- furniture


def build_plastic_chair():
    """Monobloc plastic chair - the red/blue/green chairs in the reference.

    One moulded piece: dished seat, slotted curved back, four tapered legs
    splayed slightly outward. The vertical slots in the back are what make it
    read as a cheap plastic chair rather than a generic box.
    """
    pubkit.reset_scene()

    parts = []
    seat_h = 0.435

    # Seat: thin slab, slightly wider at the front than the back.
    parts.append(pubkit.taper_box(
        "seat", bottom_size=(0.430, 0.420), top_size=(0.410, 0.400),
        height=0.032, centre=(0.0, 0.0, seat_h)))

    # Legs: tapered, splayed out towards the floor.
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(pubkit.taper_box(
                f"leg_{sx}_{sy}",
                bottom_size=(0.032, 0.032), top_size=(0.044, 0.044),
                height=seat_h,
                centre=(sx * 0.183, sy * 0.178, 0.0)))

    # Back: two rails with vertical slots between them.
    #
    # Built upright first, then leaned as one unit about a single pivot at the
    # seat. Leaning each panel about its own base makes them splay apart.
    # +Y in Blender maps to -Z in Unity ((x,y,z) -> (x,z,-y)), which is where
    # the original primitive chair put its back. Building the back at -Y put
    # it on the wrong side: chairs faced away from tables and the back had no
    # collider to grab.
    back_y = 0.185
    rail_bottom = seat_h + 0.030
    back_height = 0.430

    back_parts = []

    back_parts.append(pubkit.curved_panel(
        "back_lower_rail", width=0.400, height=0.058, thickness=0.024,
        curve_depth=0.030, centre=(0.0, back_y, rail_bottom)))

    back_parts.append(pubkit.curved_panel(
        "back_top_rail", width=0.415, height=0.072, thickness=0.026,
        curve_depth=0.032,
        centre=(0.0, back_y, rail_bottom + back_height - 0.072)))

    # Ribs span exactly between the two rails, overlapping each slightly.
    rib_bottom = rail_bottom + 0.050
    rib_height = back_height - 0.072 - 0.050 + 0.012

    slots = 5
    slot_span = 0.320
    for i in range(slots):
        u = (i + 0.5) / slots
        x = (u - 0.5) * slot_span
        back_parts.append(pubkit.curved_panel(
            f"back_rib_{i}", width=0.036, height=rib_height,
            thickness=0.020, curve_depth=0.004,
            centre=(x, back_y, rib_bottom)))

    # Side posts carry the back down to the seat.
    for sx in (-1, 1):
        back_parts.append(pubkit.taper_box(
            f"back_post_{sx}",
            bottom_size=(0.030, 0.042), top_size=(0.026, 0.034),
            height=back_height + 0.006,
            centre=(sx * 0.194, back_y + 0.008, rail_bottom - 0.030)))

    pubkit.lean_about(back_parts, -11.0, pivot_y=back_y, pivot_z=seat_h)
    parts.extend(back_parts)

    obj = pubkit.join("Asset_PlasticChair", parts)
    pubkit.shade_smooth(obj, angle_degrees=40.0)
    # Plastic chairs live on a wet concrete floor: dirty feet, grubby joints.
    pubkit.grime(obj, floor_dirt=0.62, crevice=0.34, mottle=0.13, seed=21)
    return obj


def build_concrete_table():
    """Cast-concrete table: heavy slab top on a solid central pedestal.

    The previous version read as a floating slab on a thin fin -- the pedestal
    was far too slender for the overhang, and a second wider slab underneath
    the top created an odd lip. A real cast table has three clear masses:
    a spread base that resists tipping, a chunky pedestal, and a thick top with
    a chamfer line so the edge catches light.

    Total height stays 0.775 m: the clutter placement depends on it.
    """
    pubkit.reset_scene()

    parts = []

    base_h = 0.085
    ped_top = 0.690
    top_h = 0.775

    # Spread base. Wide enough that the table looks like it would not tip.
    parts.append(pubkit.taper_box(
        "base", bottom_size=(0.780, 0.660), top_size=(0.680, 0.580),
        height=base_h, centre=(0.0, 0.0, 0.0)))

    # Pedestal: substantial, with a slight upward taper as cast concrete has.
    parts.append(pubkit.taper_box(
        "pedestal", bottom_size=(0.560, 0.480), top_size=(0.470, 0.400),
        height=ped_top - base_h, centre=(0.0, 0.0, base_h)))

    # Flare where the pedestal meets the top, so the slab is visibly carried
    # rather than balanced on a point.
    parts.append(pubkit.taper_box(
        "capital", bottom_size=(0.470, 0.400), top_size=(0.640, 0.540),
        height=0.055, centre=(0.0, 0.0, ped_top - 0.055)))

    # Top slab, in two layers so the edge reads with a chamfer line.
    slab_bottom = ped_top
    parts.append(pubkit.taper_box(
        "top_underside", bottom_size=(1.060, 0.720), top_size=(1.100, 0.760),
        height=0.022, centre=(0.0, 0.0, slab_bottom)))

    parts.append(pubkit.box(
        "top", size=(1.100, 0.760, top_h - slab_bottom - 0.022 - 0.008),
        centre=(0.0, 0.0, slab_bottom + 0.022 + (top_h - slab_bottom - 0.030) * 0.5)))

    parts.append(pubkit.taper_box(
        "top_chamfer", bottom_size=(1.100, 0.760), top_size=(1.078, 0.740),
        height=0.008, centre=(0.0, 0.0, top_h - 0.008)))

    obj = pubkit.join("Asset_ConcreteTable", parts)
    # Bare concrete stains heavily: scrubbed on top, filthy around the base.
    pubkit.grime(obj, floor_dirt=0.70, crevice=0.30, mottle=0.16, seed=22)
    return obj


# ------------------------------------------------------------- table props


def build_water_bottle():
    """1 litre PET water bottle: ribbed waist, blue cap.

    Ribbing is what makes thin plastic read as plastic rather than glass, and
    it is the one prop on a TASMAC table that is not alcohol -- worth having
    for a de-addiction scenario, where choosing water is a meaningful action.
    """
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0330),
        (0.006, 0.0356),
        (0.020, 0.0360),
        (0.048, 0.0360),
        (0.060, 0.0340),   # ribbed grip waist
        (0.072, 0.0360),
        (0.084, 0.0340),
        (0.096, 0.0360),
        (0.150, 0.0360),
        (0.176, 0.0348),
        (0.198, 0.0286),   # shoulder
        (0.214, 0.0196),
        (0.224, 0.0142),
        (0.244, 0.0140),
        (0.252, 0.0158),   # cap
        (0.272, 0.0156),
        (0.276, 0.0138),
    ]

    return _bottle_shaped("Asset_WaterBottle", profile, 16,
                          label_rings=(8, 10), foil_rings=(12, 13),
                          cap_rings=(13, 15), grime_seed=41,
                          liquid_level=0.66)


def build_snack_plate():
    """Steel thali plate WITH its food, as one object.

    Two things were wrong before. The plate was a 26 mm open lathe with no
    wall thickness, so from a seated angle you saw its unlit backfaces -- it
    read as a dark curved sliver, not a plate. And the food was a separate
    static object, so when the plate was picked up the snacks stayed behind
    (or scattered) instead of travelling with it.

    Now the profile runs up the outside, over the rim and back down the inside
    to a closed base, giving a real bowl with visible thickness. The food is
    modelled inside that bowl and merged in, on its own material slot.
    """
    pubkit.reset_scene()

    # Outer wall up, over the rim, inner wall back down to the base.
    profile = [
        (0.000, 0.0680),   # outer base edge
        (0.004, 0.0940),
        (0.012, 0.1080),
        (0.026, 0.1180),
        (0.040, 0.1235),   # rim, outer lip
        (0.044, 0.1240),
        (0.044, 0.1180),   # over the rim
        (0.038, 0.1120),   # inner wall descending
        (0.024, 0.1010),
        (0.012, 0.0850),
        (0.006, 0.0660),
        (0.006, 0.0300),   # inner base
    ]

    plate = pubkit.lathe("Asset_SnackPlate", profile, segments=22)
    pubkit.shade_smooth(plate)

    plate_faces = len(plate.data.polygons)

    # Food heaped inside the bowl, irregular so it does not read as props.
    import random
    import math as _m
    rng = random.Random(77)

    lumps = []
    for i in range(13):
        a = rng.uniform(0.0, _m.tau)
        r = rng.uniform(0.0, 0.062)
        x = r * _m.cos(a)
        y = r * _m.sin(a)
        # Heaped: taller in the middle, thinning toward the rim.
        peak = 0.030 * (1.0 - (r / 0.075) ** 2)
        z = 0.010 + rng.uniform(0.0, max(0.004, peak))
        sx = rng.uniform(0.020, 0.032)

        lumps.append(pubkit.taper_box(
            f"food_{i}",
            bottom_size=(sx, sx * rng.uniform(0.75, 1.2)),
            top_size=(sx * rng.uniform(0.5, 0.8), sx * rng.uniform(0.5, 0.8)),
            height=sx * rng.uniform(0.6, 1.0),
            centre=(x, y, z)))

    merged = pubkit.join("Asset_SnackPlate", [plate] + lumps)
    pubkit.shade_smooth(merged, angle_degrees=50.0)

    pubkit.set_material_slots(merged, ["STEEL", "FOOD"])
    for fi in range(plate_faces, len(merged.data.polygons)):
        merged.data.polygons[fi].material_index = 1
    merged.data.update()

    pubkit.grime(merged, floor_dirt=0.0, crevice=0.28, mottle=0.14, seed=43)
    return merged


def build_ashtray():
    """Pressed steel ashtray. Small, but it is on every table in the photos."""
    pubkit.reset_scene()

    profile = [
        (0.000, 0.0420),
        (0.003, 0.0520),
        (0.010, 0.0560),
        (0.022, 0.0600),
        (0.026, 0.0610),
    ]

    obj = pubkit.lathe("Asset_Ashtray", profile, segments=14, close_top=False)
    pubkit.shade_smooth(obj)
    pubkit.grime(obj, floor_dirt=0.0, crevice=0.40, mottle=0.16, seed=51)
    return obj


# --------------------------------------------------------------------- main

BUILDERS = {
    "Asset_Bottle": build_bottle,
    "Asset_Glass": build_glass,
    "Asset_BeerBottle": build_beer_bottle,
    "Asset_WhiskyQuart": build_whisky_quart,
    "Asset_BrandyHalf": build_brandy_half,
    "Asset_BottleFluted": build_bottle_fluted,
    "Asset_BottleSquare": build_bottle_square,
    "Asset_BottleTall": build_bottle_tall,
    "Asset_WaterBottle": build_water_bottle,
    "Asset_SnackPlate": build_snack_plate,
    "Asset_Ashtray": build_ashtray,
    "Asset_SteelTumbler": build_steel_tumbler,
    "Asset_PlasticChair": build_plastic_chair,
    "Asset_ConcreteTable": build_concrete_table,
}


def main():
    print(f"[build_all] output: {OUT_DIR}")

    total = 0
    for name, builder in BUILDERS.items():
        obj = builder()
        total += pubkit.tri_count(obj)
        pubkit.export_fbx(obj, OUT_DIR, name)

    print(f"[build_all] done: {len(BUILDERS)} assets, {total} triangles total")


if __name__ == "__main__":
    main()
