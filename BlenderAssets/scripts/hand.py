"""Human hand, built as a parented segment hierarchy.

WHY SEGMENTS AND NOT ONE MESH
-----------------------------
Both consumers need articulation:

  * the controller hand curls its fingers from the grip/trigger analog values
  * the tracked hand places each segment on a real joint reported by the
    XRHandSubsystem

A single rigid mesh can do neither. Segments are parented so rotating a knuckle
carries everything beyond it, which is what makes a closing fist look right.

NAMING
------
Segments are named after XRHandJointID members (Palm, IndexProximal,
IndexIntermediate, IndexDistal, ThumbMetacarpal, ...) so Unity can map a joint
straight onto a transform by name, with no lookup table to keep in sync.

PROPORTIONS
-----------
Roughly a 190 mm adult hand, wrist to middle fingertip. Finger lengths follow
real ratios: middle longest, then index and ring nearly equal, little shortest.
Getting these wrong is what makes a hand read as a cartoon glove.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
import pubkit  # noqa: E402


# name, (proximal, intermediate, distal) lengths, base offset across the palm,
# base width, and the splay angle each finger sits at
# Widths are deliberately narrower than the spacing between them, so the
# fingers read as separate digits. At equal width and spacing they fuse into
# one slab with slits cut in it.
FINGERS = [
    ("Index",  (0.0400, 0.0255, 0.0205), -0.0285, 0.0152,  8.0),
    ("Middle", (0.0455, 0.0290, 0.0220), -0.0098, 0.0158,  2.5),
    ("Ring",   (0.0415, 0.0270, 0.0210),  0.0090, 0.0150, -3.5),
    ("Little", (0.0325, 0.0205, 0.0180),  0.0262, 0.0130, -10.0),
]


def _segment(name, parent, length, width, thickness, taper=0.88):
    """One finger bone: pivot at the knuckle, mesh extending forward.

    The pivot/mesh split is what makes curling rotate about the joint rather
    than the middle of the bone.
    """
    pivot = bpy.data.objects.new(name, None)   # empty
    bpy.context.collection.objects.link(pivot)
    pivot.parent = parent

    mesh = pubkit.taper_box(
        name + "_Mesh",
        bottom_size=(width, thickness),
        top_size=(width * taper, thickness * taper),
        height=length,
        centre=(0.0, 0.0, 0.0))
    mesh.parent = pivot

    return pivot, mesh


def build_hand(right=True):
    """Returns (all_objects, root). Mirrored on X for the left hand."""
    pubkit.reset_scene()

    side = 1.0 if right else -1.0
    objects = []

    # ---- wrist / forearm stub ------------------------------------------------
    # The "long" part: a hand floating with no wrist reads as a prop. A short
    # forearm gives the eye somewhere for the hand to come from.
    root = bpy.data.objects.new("Wrist", None)
    bpy.context.collection.objects.link(root)
    objects.append(root)

    forearm = pubkit.taper_box(
        "Forearm_Mesh",
        bottom_size=(0.050, 0.038), top_size=(0.058, 0.026),
        height=0.105, centre=(0.0, 0.0, -0.105))
    forearm.parent = root
    objects.append(forearm)

    # ---- palm ------------------------------------------------------------------
    palm_pivot = bpy.data.objects.new("Palm", None)
    bpy.context.collection.objects.link(palm_pivot)
    palm_pivot.parent = root
    objects.append(palm_pivot)

    palm = pubkit.taper_box(
        "Palm_Mesh",
        bottom_size=(0.076, 0.026), top_size=(0.086, 0.021),
        height=0.092, centre=(0.0, 0.0, 0.0))
    palm.parent = palm_pivot
    objects.append(palm)

    # Heel of the hand, so the palm is not a slab.
    heel = pubkit.taper_box(
        "PalmHeel_Mesh",
        bottom_size=(0.062, 0.030), top_size=(0.076, 0.026),
        height=0.024, centre=(0.0, 0.0, -0.024))
    heel.parent = palm_pivot
    objects.append(heel)

    # ---- fingers -----------------------------------------------------------------
    for fname, lengths, offset, width, splay in FINGERS:
        parent = palm_pivot
        base_z = 0.092

        for i, seg_name in enumerate(("Proximal", "Intermediate", "Distal")):
            full = f"{fname}{seg_name}"
            thickness = width * (0.92 - i * 0.06)

            pivot, mesh = _segment(full, parent, lengths[i], width, thickness)

            if i == 0:
                pivot.location = (offset * side, 0.0, base_z)
                pivot.rotation_euler = (0.0, math.radians(splay * side), 0.0)
            else:
                pivot.location = (0.0, 0.0, lengths[i - 1])

            objects.extend([pivot, mesh])
            parent = pivot
            width *= 0.92

        # Fingertip marker, so Unity can find the tip joint.
        tip = bpy.data.objects.new(f"{fname}Tip", None)
        bpy.context.collection.objects.link(tip)
        tip.parent = parent
        tip.location = (0.0, 0.0, lengths[2])
        objects.append(tip)

    # ---- thumb ---------------------------------------------------------------------
    # Sits low on the palm edge and rotated well out of plane -- the thumb's
    # opposition is most of what separates a hand from a mitten.
    thumb_parent = palm_pivot
    thumb_lengths = (0.042, 0.035)
    thumb_base = ("ThumbMetacarpal", "ThumbProximal", "ThumbDistal")

    meta = bpy.data.objects.new(thumb_base[0], None)
    bpy.context.collection.objects.link(meta)
    meta.parent = palm_pivot
    meta.location = (-0.036 * side, 0.006, 0.012)
    meta.rotation_euler = (math.radians(-26.0), math.radians(-52.0 * side), 0.0)
    objects.append(meta)

    parent = meta
    width = 0.0205
    for i in range(2):
        pivot, mesh = _segment(thumb_base[i + 1], parent, thumb_lengths[i],
                               width, width * 0.86)
        pivot.location = (0.0, 0.0, 0.0 if i == 0 else thumb_lengths[i - 1])
        objects.extend([pivot, mesh])
        parent = pivot
        width *= 0.90

    tip = bpy.data.objects.new("ThumbTip", None)
    bpy.context.collection.objects.link(tip)
    tip.parent = parent
    tip.location = (0.0, 0.0, thumb_lengths[1])
    objects.append(tip)

    # Smooth and grime every mesh.
    for o in objects:
        if o.type == 'MESH':
            pubkit.shade_smooth(o, angle_degrees=48.0)
            # Skin tone, not grime: fingertips warmer, creases darker.
            pubkit.skin_tone(o, seed=61)

    return objects, root


def main():
    project = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out = os.path.join(project, "Assets", "Models")

    for right in (True, False):
        objects, _ = build_hand(right)
        pubkit.export_fbx_multi(objects, out,
                                "Asset_HandRight" if right else "Asset_HandLeft")


if __name__ == "__main__":
    main()
