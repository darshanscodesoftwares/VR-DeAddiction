"""
Converts the Sketchfab "Hands model rigged" into two Unity-ready hand FBXs that
drop into the slot the XR Hands sample hands currently occupy.

The source is one mesh holding both hands plus arms and a torso stub, on a
53-bone armature where every bone is called "Bone.0NN". Nothing can be matched
by name, so this script works entirely from geometry:

  * A palm is the bone with exactly five children.
  * The thumb is the chain whose base sits furthest back down the palm --
    every other finger starts at the knuckle line.
  * The remaining four are ordered across the palm; the longest is the middle
    finger, which fixes the direction, giving little/ring/middle/index.
  * Handedness comes from which side of the four-knuckle plane the thumb tip
    falls on. Fingertip curl direction is checked as a second opinion.

Alignment is done by mapping three landmarks (wrist, middle knuckle, index
knuckle) onto the same three landmarks of the XR Hands reference model, so the
result lands in Unity at the reference's position, orientation and SIZE. Size
matters: RiggedHandPose's grip calibration (HandSpan, MinGripCurl) is tuned
against the reference hand's proportions, and rescaling would invalidate it.

Run:  blender --background --python handsmodel.py
"""
import bpy, bmesh, os, sys, math, mathutils
from mathutils import Vector, Matrix

HERE    = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC     = os.path.join(PROJECT, "BlenderAssets", "source", "Hands.fbx")
REFDIR  = os.path.join(PROJECT, "Assets", "Samples", "XRHands", "Models")
OUTDIR  = os.path.join(PROJECT, "Assets", "Models")

FINGERS = ("Thumb", "Index", "Middle", "Ring", "Little")
SEGS    = {"Thumb":  ("Metacarpal", "Proximal", "Distal"),
           "_other": ("Proximal", "Intermediate", "Distal")}


# ---------------------------------------------------------------- utilities
def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def strip_animation():
    """Drop the animation data the FBX importer attaches to every object.

    The source rig arrives with its object transforms KEYFRAMED. Any depsgraph
    evaluation then re-applies those keys, so bpy.ops.object.transform_apply
    appeared to work -- the location read back as zero -- and was silently undone
    at the next view_layer.update(). Downstream that put the hands 24 cm from
    where the landmarks said, with a skin bound to a skeleton that had moved
    underneath it: Unity saw localBounds of (0,0,0) and the two hands at
    different sizes. Nothing here is animated, so the safe fix is to remove it.
    """
    for o in bpy.data.objects:
        o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)


def chain_from(bone):
    """The bone plus its single-child descendants, ignoring FBX _end tips."""
    out, n = [bone], bone
    while n.children:
        n = n.children[0]
        if n.name.endswith("_end"):
            break
        out.append(n)
    return out


def frame(p0, p1, p2):
    """Orthonormal frame from wrist(p0), middle knuckle(p1), index knuckle(p2)."""
    u = (p1 - p0).normalized()
    w = p2 - p0
    v = (w - u * w.dot(u)).normalized()
    return Matrix((u, v, u.cross(v))).transposed(), (p1 - p0).length


def identify(arm, palm):
    """Name every finger chain of one palm, and say which hand it is."""
    M  = arm.matrix_world
    pw = M @ palm.head_local
    F  = ((M @ palm.tail_local) - pw).normalized()

    rows = []
    for c in palm.children:
        ch = chain_from(c)
        rows.append(dict(bone=c, chain=ch,
                         head=M @ c.head_local,
                         total=sum((b.tail_local - b.head_local).length for b in ch),
                         along=(M @ c.head_local - pw).dot(F)))

    thumb  = min(rows, key=lambda r: r["along"])      # sits back down the palm
    others = [r for r in rows if r is not thumb]

    ctr = sum((r["head"] for r in others), Vector()) / 4
    S   = (others[0]["head"] - others[-1]["head"])
    S   = (S - F * S.dot(F)).normalized()
    others.sort(key=lambda r: (r["head"] - ctr).dot(S))

    longest = max(others, key=lambda r: r["total"])   # the middle finger
    order = (["Index", "Middle", "Ring", "Little"] if others.index(longest) == 1
             else ["Little", "Ring", "Middle", "Index"])
    named = dict(zip(order, others))
    named["Thumb"] = thumb

    # Handedness: for a right hand F x S points out of the BACK, so the thumb
    # -- which lies on the palm side -- gives a negative offset along it.
    Sf   = (named["Index"]["head"] - named["Little"]["head"])
    Sf   = (Sf - F * Sf.dot(F)).normalized()
    back = F.cross(Sf)
    knuck = [named[k]["head"] for k in ("Index", "Middle", "Ring", "Little")]
    kctr  = sum(knuck, Vector()) / 4
    tipb  = chain_from(named["Thumb"]["bone"])[-1]
    tip   = M @ tipb.tail_local
    side  = (tip - kctr).dot(back)
    return named, ("Right" if side < 0 else "Left"), side


def ref_frame(path, prefix):
    """Landmark frame of an XR Hands reference model, in its own space."""
    clear()
    bpy.ops.import_scene.fbx(filepath=path)
    strip_animation()
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    M   = arm.matrix_world
    B   = arm.data.bones
    p0 = M @ B[prefix + "Wrist"].head_local
    p1 = M @ B[prefix + "MiddleProximal"].head_local
    p2 = M @ B[prefix + "IndexProximal"].head_local
    R, span = frame(p0, p1, p2)
    me = next(o for o in bpy.data.objects if o.type == 'MESH')
    tipb = B[prefix + "MiddleDistal"]
    return dict(p0=p0, R=R, span=span, bends=bend_profile(arm, prefix),
                length=((M @ tipb.tail_local) - p0).length,
                tris=sum(len(p.vertices) - 2 for p in me.data.polygons))


def bend_profile(arm, pre):
    """Rest-pose angle at each finger joint, in degrees.

    A hand at curl 0 should look relaxed, not splayed flat, and RiggedHandPose
    adds its curl ON TOP of whatever rest pose the model ships with. The two
    models disagree here -- the XR Hands reference rests at ~19 deg per joint,
    this model at ~4 -- so the same curl value would read as a much flatter
    hand. Measuring the reference lets the new model be given the same resting
    profile, which keeps the existing HandSpan/MaxCurlDegrees calibration valid.
    """
    M, B, out = arm.matrix_world, arm.data.bones, {}
    palm = (M @ B[pre + "Wrist"].tail_local) - (M @ B[pre + "Wrist"].head_local)
    for f in ("Index", "Middle", "Ring", "Little"):
        v = [(M @ B[pre + f + s].tail_local) - (M @ B[pre + f + s].head_local)
             for s in SEGS["_other"]]
        out[f] = [math.degrees(palm.angle(v[0])),
                  math.degrees(v[0].angle(v[1])),
                  math.degrees(v[1].angle(v[2]))]
    return out


# ------------------------------------------------------------------- build
def build(side, named, palm_name, ref, out_path):
    """Isolate one hand, rename its bones, align it to the reference, export."""
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    me  = next(o for o in bpy.data.objects if o.type == 'MESH')
    p   = "L_" if side == "Left" else "R_"

    keep_src = {palm_name}
    rename   = {palm_name: p + "Wrist"}
    for f in FINGERS:
        segs = SEGS["Thumb"] if f == "Thumb" else SEGS["_other"]
        for bone, seg in zip(named[f]["chain"], segs):
            keep_src.add(bone.name)
            rename[bone.name] = p + f + seg

    # --- drop every vertex not weighted to this hand
    gi = {vg.name: vg.index for vg in me.vertex_groups}
    keep_gi = {gi[n] for n in keep_src if n in gi}
    bpy.context.view_layer.objects.active = me
    bm = bmesh.new(); bm.from_mesh(me.data)
    layer = bm.verts.layers.deform.verify()
    bm.verts.ensure_lookup_table()
    doomed = [v for v in bm.verts
              if not any(w > 0.05 and g in keep_gi for g, w in v[layer].items())]
    bmesh.ops.delete(bm, geom=doomed, context='VERTS')
    bm.to_mesh(me.data); bm.free()
    me.data.update()

    # --- drop every bone not in this hand, then rename what is left
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    for b in [b for b in eb if b.name not in keep_src]:
        eb.remove(b)
    for b in eb:
        b.parent = eb.get(rename.get(b.parent.name, "")) if b.parent else None
    for old, new in rename.items():
        if old in eb:
            eb[old].name = new
    bpy.ops.object.mode_set(mode='OBJECT')
    for vg in me.vertex_groups:
        if vg.name in rename:
            vg.name = rename[vg.name]

    # --- flatten object transforms so data IS world space
    #
    # Without this the armature and mesh each keep a non-identity object matrix,
    # the alignment has to be conjugated into two different local spaces, and the
    # skin binding comes out inconsistent with the skeleton: Unity imported the
    # result with localBounds of (0,0,0) and the two hands at DIFFERENT sizes,
    # which is a broken bind pose, not a bad scale. Baking to identity first and
    # rebinding afterwards removes the whole class of problem.
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True); bpy.context.view_layer.objects.active = me
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    for o in (arm, me):
        for mod in [m for m in o.modifiers if m.type == 'ARMATURE']:
            o.modifiers.remove(mod)
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    print(f"    [dbg] after apply: arm.loc={tuple(round(v,4) for v in arm.matrix_world.translation)} "
          f"me.loc={tuple(round(v,4) for v in me.matrix_world.translation)} "
          f"arm.scale={tuple(round(v,4) for v in arm.matrix_world.to_scale())}")
    # --- align landmarks onto the reference hand's frame
    B  = arm.data.bones
    M  = arm.matrix_world              # identity now
    q0 = M @ B[p + "Wrist"].head_local
    q1 = M @ B[p + "MiddleProximal"].head_local
    q2 = M @ B[p + "IndexProximal"].head_local
    Rn, span = frame(q0, q1, q2)

    # Scale on whole-hand length (wrist to middle fingertip), not on the knuckle
    # span. Matching the span alone left this model's longer fingers sticking out
    # a third further than the reference's, which in the headset just reads as
    # oversized hands.
    tip = M @ B[p + "MiddleDistal"].tail_local
    s = ref["length"] / (tip - q0).length
    R = ref["R"] @ Rn.transposed()
    T = (Matrix.Translation(ref["p0"]) @ (R @ Matrix.Scale(s, 3)).to_4x4()
         @ Matrix.Translation(-q0))

    # Move the DATA, not the objects. Assigning matrix_world on a mesh parented
    # to the armature is recomputed from the parent and silently lost, and the
    # armature's own object transform is baked away on export -- both leave the
    # hand exactly where it started. Transforming edit bones and vertices in
    # their own spaces survives both.
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for b in arm.data.edit_bones:
        b.transform(T)
    bpy.ops.object.mode_set(mode='OBJECT')
    me.data.transform(T)
    bpy.context.view_layer.update()

    # --- rebind the skin to the moved skeleton
    bpy.ops.object.select_all(action='DESELECT')
    me.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE')
    bpy.context.view_layer.update()

    # NOTE: an earlier version baked the reference's resting finger bend into
    # this model so the runtime curl calibration would transfer unchanged. It is
    # left out deliberately. Posing then applying-as-rest kept folding the left
    # hand's knuckles to ~110 deg, because pose-bone matrices read back stale
    # after the edit-bone alignment transform, so the sign search was comparing
    # against the PRE-alignment skeleton. The rest-pose difference is handled in
    # RiggedHandPose.MaxCurlDegrees instead, which is symmetric and tunable.
    # If revisiting: force a depsgraph rebuild between the edit-mode transform
    # and any pose_bone.matrix read.
    print(f"    [dbg] ref p0={tuple(round(v,4) for v in ref['p0'])} "
          f"final wrist={tuple(round(v,4) for v in (arm.matrix_world @ arm.data.bones[p+'Wrist'].head_local))} "
          f"arm.loc={tuple(round(v,4) for v in arm.matrix_world.translation)}")
    arm.name, me.name = "Armature", side + "Hand"

    # --- export exactly as the rest of the project's models are exported
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    # apply_scale_options MUST be FBX_SCALE_ALL for a SKINNED mesh. The project's
    # static props export with the default FBX_SCALE_NONE, which puts the unit
    # conversion into object transforms; the bind poses do not follow, so Unity
    # receives a skeleton at correct metre positions bound to a skin 100x too
    # small. It presents as huge, distorted hands with the two sides at
    # different sizes -- not obviously a scale bug at all.
    #
    # Confirmed by exporting the untouched source four ways and measuring each
    # in Unity: FBX_SCALE_NONE gave a bind-scale error of 171.47 and a mesh that
    # baked to 1 mm, while SCALE_ALL, SCALE_UNITS and unit_scale=False all gave
    # exactly 0.0000. A no-op import/export round trip failed the same way,
    # which is what ruled out the conversion itself.
    #
    # bake_space_transform is likewise True here and False for the static props.
    # Left False, the Blender->Unity axis conversion is written into the file's
    # global settings rather than the data, and Unity honoured it for the
    # SKELETON but not the SKIN: bones pointed down +Z while the mesh still ran
    # along +Y, i.e. the hand's fingers rendered at right angles to its own
    # bones. Baking it into the data removes the ambiguity.
    bpy.ops.export_scene.fbx(
        filepath=out_path, use_selection=True, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        bake_space_transform=True, axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, object_types={'ARMATURE', 'MESH'},
        use_armature_deform_only=True, path_mode='STRIP')
    tris = sum(len(f.vertices) - 2 for f in me.data.polygons)
    print(f"  wrote {os.path.basename(out_path)}: {len(me.data.vertices)} verts, "
          f"{tris} tris, {len(arm.data.bones)} bones, scale x{s:.3f} "
          f"(reference is {ref['tris']} tris)")
    return tris


def posed_profile(arm, pre):
    """Finger joint angles with the current POSE applied (bones point +Y)."""
    def d(n):
        return (arm.pose.bones[n].matrix.to_3x3() @ Vector((0, 1, 0))).normalized()
    out = {}
    for f in ("Index", "Middle", "Ring", "Little"):
        v = [d(pre + f + s) for s in SEGS["_other"]]
        out[f] = [math.degrees(d(pre + "Wrist").angle(v[0])),
                  math.degrees(v[0].angle(v[1])),
                  math.degrees(v[1].angle(v[2]))]
    return out


def bake_rest_pose(arm, me, pre, target):
    """Bend the fingers to the reference's resting profile and make that the
    rest pose, so the model ships relaxed rather than splayed flat.

    The rotation axis per joint is well defined (perpendicular to the bone, in
    the plane the joint bends in) but its SIGN is not worth deriving: the palm
    normal flips between hands, and a joint's own residual bend is too small to
    read a direction from reliably. Both attempts folded the left hand inside
    out at the knuckle. So the sign is measured instead -- pose every joint one
    way, measure, pose the other way, measure, and keep whichever landed nearer
    the target. Two evaluations settle every joint on both hands.
    """
    M, B = arm.matrix_world, arm.data.bones
    have = bend_profile(arm, pre)
    joints = [(f, seg, want, got)
              for f in ("Index", "Middle", "Ring", "Little")
              for seg, want, got in zip(SEGS["_other"], target[f], have[f])]

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')

    axes = {}
    for f, seg, want, got in joints:
        pb = arm.pose.bones[pre + f + seg]
        prev = pb.parent if pb.parent else arm.pose.bones[pre + "Wrist"]
        a = prev.bone.matrix_local.to_3x3() @ Vector((0, 1, 0))
        b = pb.bone.matrix_local.to_3x3() @ Vector((0, 1, 0))
        n = a.cross(b)
        if n.length < 1e-5:                       # joint is dead straight
            n = pb.bone.matrix_local.to_3x3() @ Vector((1, 0, 0))
        axes[(f, seg)] = (pb.bone.matrix_local.to_3x3().inverted()
                          @ n.normalized()).normalized()

    def apply(sign_of):
        for f, seg, want, got in joints:
            pb = arm.pose.bones[pre + f + seg]
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = mathutils.Quaternion(
                axes[(f, seg)], math.radians((want - got) * sign_of(f, seg)))
        bpy.context.view_layer.update()
        return posed_profile(arm, pre)

    print(f"    [dbg] initial rest palm->prox: " +
          " ".join(f"{f}={have[f][0]:.1f}" for f in ("Index","Middle","Ring","Little")))
    print(f"    [dbg] posed_profile before any pose: " +
          " ".join(f"{f}={posed_profile(arm,pre)[f][0]:.1f}" for f in ("Index","Middle","Ring","Little")))
    plus  = apply(lambda f, s: +1.0)
    print(f"    [dbg] plus  palm->prox: " + " ".join(f"{f}={plus[f][0]:.1f}" for f in ("Index","Middle","Ring","Little")))
    minus = apply(lambda f, s: -1.0)
    print(f"    [dbg] minus palm->prox: " + " ".join(f"{f}={minus[f][0]:.1f}" for f in ("Index","Middle","Ring","Little")))
    sign = {}
    for f, seg, want, got in joints:
        i = SEGS["_other"].index(seg)
        sign[(f, seg)] = +1.0 if abs(plus[f][i] - want) <= abs(minus[f][i] - want) else -1.0
    final = apply(lambda f, s: sign[(f, s)])
    bpy.ops.object.mode_set(mode='OBJECT')

    # Bake: the deformed shape becomes the mesh, the posed skeleton becomes rest.
    bpy.context.view_layer.objects.active = me
    for mod in [m for m in me.modifiers if m.type == 'ARMATURE']:
        bpy.ops.object.modifier_apply(modifier=mod.name)
    m = me.modifiers.new("Armature", 'ARMATURE'); m.object = arm
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply()
    bpy.ops.object.mode_set(mode='OBJECT')
    now = bend_profile(arm, pre)
    err = max(abs(now[f][i] - target[f][i])
              for f in ("Index", "Middle", "Ring", "Little") for i in range(3))
    print("    rest pose baked (target / result, degrees) worst error "
          f"{err:.1f} deg:")
    for f in ("Index", "Middle", "Ring", "Little"):
        print(f"      {f:7s} " + "  ".join(f"{t:5.1f}/{n:5.1f}"
                                           for t, n in zip(target[f], now[f])))


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    refs = {"Right": ref_frame(os.path.join(REFDIR, "RightHand.fbx"), "R_"),
            "Left":  ref_frame(os.path.join(REFDIR, "LeftHand.fbx"),  "L_")}

    # one pass just to read the source layout
    clear(); bpy.ops.import_scene.fbx(filepath=SRC); strip_animation()
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    palms = [b for b in arm.data.bones if len(b.children) == 5]
    layout = {}
    for pb in palms:
        named, side, offset = identify(arm, pb)
        layout[pb.name] = side
        print(f"palm {pb.name}: {side} hand  (thumb {offset*100:+.1f} cm off the "
              f"knuckle plane)")
        for f in FINGERS:
            print(f"    {f:7s} <- {named[f]['bone'].name}")
    if sorted(layout.values()) != ["Left", "Right"]:
        sys.exit(f"ERROR: handedness came out as {layout}, expected one of each")

    for palm_name, side in layout.items():
        clear(); bpy.ops.import_scene.fbx(filepath=SRC); strip_animation()
        arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
        named, s2, _ = identify(arm, arm.data.bones[palm_name])
        assert s2 == side
        build(side, named, palm_name, refs[side],
              os.path.join(OUTDIR, f"Asset_Hand{side}.fbx"))


if __name__ == "__main__":
    main()
