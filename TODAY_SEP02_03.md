# Completed — 2–3 September 2026

Two sessions, treated as one because nothing was committed between them.
Earlier work is in `SESSION_LOG.md` and `TODAY_AUG20.md`; overall state is in
`PROJECT_STATE.md`.

**Everything below is UNCOMMITTED.** The last commit is `24697d4` (1 Sep).

---

## 1. Downloaded models replaced the procedural ones

Each has its own headless Blender script, so the conversion is repeatable
rather than a one-off manual import. Source files are deliberately NOT in the
repo — they are render assets, not game assets.

| Asset | Source | Triangles | Script |
|---|---|---|---|
| Round bar table (×13) | Sketchfab, TabbieCat | 1,242 — not decimated | `roundtable.py` |
| Plastic monobloc chair | Sketchfab, KhanSaab | 15,008 → **2,000** | `plasticchair.py` |
| Close (X) icon | Sketchfab, Sparrow | 1,060 | `exiticon.py` |

The chair download was **four colour variants merged into one mesh**. A
histogram of vertex X found the gaps between them and the third cluster was
taken, because it has wide gaps either side and cannot clip a neighbour.

## 2. Everything the round table broke

Swapping a 1.10 × 0.76 rectangle for a 0.734 m disc invalidated four things
that had been derived from the old shape:

- **Collision** → static `MeshCollider`. A box cannot describe a disc: sized to
  the diameter its corners stand past the rim and glasses rest on air;
  inscribed, it stops short and they fall through. Static mesh colliders need
  not be convex, so the mesh itself collides.
- **Chair spacing** → 0.95 m to 0.74–0.82 m. The old figure applied a
  rectangle's worst case all the way round; a disc has no worst case.
- **Containment lip** → a 12-segment ring. Four straight walls around a disc
  leave the corners over open air and cut across the rim on the flats.
- **Hero table props** → all seven re-laid inside a 0.30 m usable radius.

## 3. Textures

| Surface | Source | Notes |
|---|---|---|
| Yard ground | ambientCG Ground109 (CC0) | Tiled at **2.8 m**, the capture's real size |
| Hall floor | ambientCG Concrete034 (CC0) | Tiled at 2 m |
| Sky | PS1 cloudy panorama | **De-dithered** — see below |

Two Sketchfab "ground" downloads were tried first and abandoned. Both are
photogrammetry scans: a mesh plus a UV atlas of triangular islands separated by
blurred padding, built to wrap one mesh rather than to repeat. No window of any
useful size came out clean — the best 512 px region was still 8% padding.

## 4. Audio

- **Footsteps** sliced from one continuous walk recording at its transients, so
  odd footfalls are genuinely one foot and even the other
- **Grab clink** on `selectEntered`, so pressing grip on air is silent by
  construction
- **Fluorescent flicker** driven from the clip's own amplitude envelope, baked
  at build time and looked up by the AudioSource's playback position — the
  light cannot drift out of step however long it loops
- Levels: footsteps **0.45**, clink **0.09**, flicker **0.30**

## 5. The TASMAC sign

Rebuilt as a painted board lit by two floodlights rather than a glowing panel.
White lettering, mounting brackets off the top beam, and a service-drop wire
from the utility pole built as a parabolic sag.

## 6. Lighting and shadows

- Every light that CAN cast now does: 3 pendants, 2 sign spots, the sun. 27
  vertex lights cannot cast, by design — that is why they are cheap.
- **Static batching was never enabled for Android.** The platform entry in
  ProjectSettings was an empty list. 635 renderers are BatchingStatic.
- Ceiling lights had been in **three different sets of positions** from the
  visible lamps. `BuildFixtures` now publishes where the lamps hang and
  `BuildLighting` reads it.
- Shadow settings: distance **12 m**, maps **128**, strength 0.72 interior /
  0.46 sun, pendant cones **140°**.

## 7. Grabbing

- `GrabPushOut` nudges a held object 2.8 cm clear of the fingers AFTER the grab,
  so grab freedom is untouched
- `RiggedHandPose` now limits finger curl to the held object's measured
  thickness — a hand on an 84 mm bottle closes to 0.51, not to a fist

A previous attempt using fixed grip points with dynamic attach OFF was
**reverted**: it tidied the intersection and broke grabbing. Do not retry that.

## 8. Layout fixes, all found by measuring

| Fault | Cause |
|---|---|
| Bottles all over the floor | Clutter still sampled on the old rectangle; corners landed at r = 0.50 past a 0.367 rim |
| Hero table wedged between two grid tables | Scenario places its table at a fixed spot the grid knows nothing about |
| Crate stack inside the counter | Crates started at x −1.4; `Counter_Return` spans −1.405 to −0.555 |
| Chair stack like a ladder | 0.26 m rise; monobloc chairs nest at ~0.09 |
| Marker approached from behind the chair | Search took the nearest clear spot at any angle |

`Assets/Editor/LayoutAudit.cs` now measures tables, chairs, props and the marker
against each other at build time.

---

## 9. Finger curl took three passes, settled against a reference image

`RiggedHandPose` limits how far the fingers close by the thickness of what is
held: `curl = 1 - (halfWidth / HandSpan)`, floored at `MinGripCurl`, scaled by
`MaxCurlDegrees` at the knuckle.

| HandSpan | Bottle curl | Knuckle | Result |
|---|---|---|---|
| 0.085 | 0.51 | 32° | Fingers nearly straight, bottle passes through them |
| 0.22 | 0.81 | 68° | A closed fist — fingers fold past the object |
| **0.105** | **0.60** | **50°** | The C shape, which is the target |

The target was not reasoned out, it was given: a screenshot of the hand at the
wanted curl. Both wrong values came from estimating what "gripping a bottle"
should look like, and estimating it wrong in both directions. The thumb bonus
moved with it (+0.22 → +0.10); at +0.22 it crossed the palm, which reads as a
fist rather than a grip.

An empty hand is unaffected — no held object means no limit, so a squeeze with
nothing in it still closes to a full fist.

**These values live in the scene, not just the script.** `XRRigBuilder` sets
them via `AddComponent` at build time, so they serialise into
`Assets/unity-VR.unity`. Editing the defaults in `RiggedHandPose.cs` changes
nothing on device until `HeadlessTasks.RebuildScene` runs.

---

## 10. Third-party hand model: two FBX export flags that only matter for skin

**REVERTED on 3 Sep** -- the project is back on the XR Hands sample hands and
`MaxCurlDegrees` is back to 84. The conversion script survives at
`BlenderAssets/scripts/handsmodel.py` with its source at
`BlenderAssets/source/Hands.fbx`; neither is referenced by the build. The export
findings below are the reason to keep this section: they apply to ANY skinned
mesh this project imports later, not just to that model.

The Sketchfab "Hands model rigged" was swapped in for the XR Hands sample hands. The
conversion itself (`BlenderAssets/scripts/handsmodel.py`) was not the hard part
-- identifying unnamed bones geometrically worked first time. The export did.

**The project's static-prop FBX settings are wrong for a skinned mesh.** Props
export with `apply_scale_options='FBX_SCALE_NONE'` and
`bake_space_transform=False`. Both break a rig, and neither looks like what it
is:

| Flag | Symptom in the headset | What is actually wrong |
|---|---|---|
| `FBX_SCALE_NONE` | Enormous, distorted hands, left and right different sizes | Unit conversion goes into object transforms; bind poses do not follow, so the skin is bound 100x off the skeleton |
| `bake_space_transform=False` | Fingers point up, at right angles to the arm | Axis conversion is written to the file's global settings; Unity applies it to the SKELETON but not the SKIN |

Correct for skinned meshes: `apply_unit_scale=True`,
`apply_scale_options='FBX_SCALE_ALL'`, `bake_space_transform=True`.

**How they were found.** Guessing failed twice. What worked was exporting the
UNTOUCHED source through Blender with no edits at all -- that round trip was
broken too, which ruled out the conversion in one step -- then exporting the same
source four ways and measuring each in Unity. `Assets/Editor/HandCheck.cs` does
the measuring: it bakes the skinned mesh (`SkinnedMeshRenderer.BakeMesh`) and
reports true size plus a bind-pose scale error. The error is 0.0000 on a correct
export and was 171.47 on all three broken ones.

**Renderer bounds are not evidence.** `SkinnedMeshRenderer.bounds` reported
plausible-looking numbers throughout. `localBounds` of (0,0,0) was the first real
signal, and BakeMesh was the one that settled it -- a mesh baking to 1 mm while
its bones sat at correct metre positions.

A further trap, upstream: **the source FBX arrives with its object transforms
keyframed.** Any depsgraph evaluation re-applies them, so `transform_apply`
appeared to succeed and was silently undone at the next `view_layer.update()`.
`strip_animation()` clears it on every import.

Also worth knowing: the two models rest differently -- the XR Hands sample rests
at ~19 deg of bend per finger joint, this one at ~5 -- and curl is applied on top
of rest, so `RiggedHandPose.MaxCurlDegrees` went 84 -> 104 to keep the same
absolute finger angles. That number is only meaningful against one hand model.

---

## Open

1. **No framerate measurement all session.** Every `PERF` capture window came
   back empty because the probe only logs while the headset is worn. Several
   large lighting changes went in unmeasured. This is the top item.
2. **Disk ran out three times today.** Browser caches were cleared each time and
   refill. Xcode Archives (7.8 GB) and Docker (5.7 GB) remain untouched.
3. **Audio and two model licences unrecorded** — see `Assets/Models/CREDITS.md`.
4. Everything since `24697d4` is uncommitted.
5. The clinical layer still does not exist.
