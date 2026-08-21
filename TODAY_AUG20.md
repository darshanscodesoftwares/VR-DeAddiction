# Completed — 20 August 2026

Only this calendar day. Earlier work is in `SESSION_LOG.md`; overall state is in
`PROJECT_STATE.md`.

---

## 1. Seated table scenario (the main piece)

Designed via a **7-agent review** across clinical, interaction, architecture and
realism angles, then critiqued adversarially against the project's constraints.
The review killed the Unity-scene-load approach on a specific repo fact:
`PubEnvironmentBuilder.cs:111` parents the XR Origin **inside** the generated
root, so it is not a scene root and `DontDestroyOnLoad` cannot preserve it.
That confirmed the seamless in-place approach.

**New files**

| File | Role |
|---|---|
| `Assets/Editor/PubScenarioBuilder.cs` | Hero table, marker, seat anchor, props, containment |
| `Assets/Scripts/SeatedTableScenario.cs` | Bar / AtMarker / Seated state machine |
| `Assets/Scripts/LightingDirector.cs` | Cross-fades room moods |
| `Assets/Scripts/TableContainment.cs` | Speed cap, invisible lip, soft return |
| `Assets/Editor/LayoutCheck.cs` | Reads back positions and facing |
| `SCENARIO_SPEC.md` | The agreed spec and reasoning |

**Behaviour** — hero table at (-2.6, 1.4). Walk to a marker, sit, the room fades
to gloomy warm while the table lights up, props become grabbable only while
seated, stand at any time. Sit/stand by A/X **or** hand gesture. Marker position
is searched for clear floor rather than hand-placed.

---

## 2. Environment work

- **Sun moved overhead** (26° → 84°) so no daylight shafts down the hall
- **Overcast procedural skybox** — no texture files; heavy atmosphere + low
  exposure + sun disc off. Sun intensity 1.45 → 0.78, shadows softened
- **Roof gaps sealed** — panels were 98% of their segment, leaving ~5 cm slots.
  Now 106% and lapped, with alternate panels 45 mm proud to avoid z-fighting
- Table props trimmed to 6 (food plate and ashtray removed on request)
- Seat moved 0.18 m closer to the table

---

## 3. Hands

- Tried a Blender hand built from tapered boxes. **It read as a plank with
  slits** — `pubkit` cannot make organic shapes, and no texture would fix
  geometry
- Replaced with **Unity's rigged hand** from the XR Hands package already
  installed (`Assets/Samples/XRHands/`). Verified as a genuinely skinned mesh.
  Free, already licensed, and vendor-neutral unlike Meta's Interaction SDK
- **`RiggedHandPose.cs`** curls the fingers from grip/trigger. The curl axis is
  **derived per bone** from geometry — bone length direction crossed with the
  index-to-little knuckle axis — rather than assuming "rotate about X"

---

## 4. Bugs fixed (16)

Most were mine, from this session.

| # | Bug | Effect |
|---|---|---|
| 1 | Sit/stand bound to `<XRController>` | Hand-tracking patients could not sit or stand at all |
| 2 | Hero chair grabbable at the seat | Could launch on spawn, or be thrown while sat in |
| 3 | Only hand locomotion suspended when seated | Controllers could walk out of the chair |
| 4 | `GroundRecovery` live while seated | B teleported a seated patient to the street |
| 5 | No fade on the seat glide | Direct visual-vestibular mismatch |
| 6 | Seat anchor at floor level | Patient stood at the table instead of sitting |
| 7 | **Lowering the rig buried the capsule** | Capsule 0.4 m under the floor; physics ejected the patient |
| 8 | Rig yaw ignored head yaw | Sitting left you facing sideways |
| 9 | Seat chair at yaw 180 | Backrest between patient and table |
| 10 | **Sit moved the rig, not the camera** | Left stranded near the marker, out of reach |
| 11 | Chairs at `ang+180` | Correct only at 90°; most faced away. Right answer is `-ang` |
| 12 | Chairs spawned overlapping tables | Physics flung them across the room on load |
| 13 | Stacked spare chairs | Four 0.9 m colliders intersecting by design — exploded |
| 14 | Marker placed inside `TableUnit_06` | Glowing shaft through a neighbouring tabletop |
| 15 | Containment lip outside the table edge | Objects rolled off before reaching it |
| 16 | Roof panel overlap z-fighting | Coplanar faces flickering |

---

## 5. Process failures worth remembering

- **Reported a build as successful from a stale APK on disk.** The real build
  had been killed by a 2-minute tool timeout; the `[Headless] APK built` line
  was absent. Verify by the log line, and run builds detached.
- **Shipped the seating four times without verifying it.** Only checked that
  the scene built and the framerate held. `PerformanceProbe` reports `camY` and
  `rigY` — enough to confirm seating worked — and I had that all along.
- Most placement bugs came from eyeballing offsets instead of computing
  clearances against real collider extents.

---

## 6. Verified on device

72.0 fps with the scenario, models, grime, 27 lights, hand tracking and ~73
grabbables. Framerate has not been the constraint at any point today.

---

# Later the same day — second session

Written after `PROJECT_STATE.md`, so not covered above.

## Build pipeline

| # | Task | What it was |
|---|---|---|
| 1 | **Disk-full build failure diagnosed** | `Build Failed: 1 error(s)` with no `error CS` line. Real cause was `No space left on device`, buried far up the log. Freed 3 GB of caches |
| 2 | **Stale-APK install closed off** | The watcher grepped `[Headless] Build `, which also matches `Build Failed`, so it installed an old APK after a failed build — twice. Now matches `APK built` alone |

## Hands

| # | Task | What it was |
|---|---|---|
| 3 | **Left-hand curl fixed** | Curl axis came from the index-to-little knuckle vector, which points opposite ways on the two hands. Now derived from the back-of-hand normal, correct on both by construction |
| 4 | **Thumb opposition fixed** | The thumb was treated as a fifth finger and splayed sideways. A thumb does not flex, it sweeps across the palm, so it now gets its own axis aimed at a target derived from the knuckles |

## Physics

| # | Task | What it was |
|---|---|---|
| 5 | **Bottles and glasses can be set down** | Every prop had a CAPSULE collider, so all of them stood on a rounded base and rolled off. The tumbler and glass were near-spheres. Now flat-based boxes with a low centre of mass |
| 6 | **Grab magnetism toned down** | The builder never created the interactor's casters, so XRI made its own with package defaults: a 10 cm grab sphere and a **10 m** far reach. Set explicitly to 0.055 m and 1.8 m |

## Controls

| # | Task | What it was |
|---|---|---|
| 7 | **Stand-up button** | A green block that appears only while seated; grab it to get out of the chair. Standing previously needed a controller button or a palms-above-head gesture |
| 8 | **Safety exit built, then stood down** | An app-quit control (grabbable button, face-cover gesture, menu button). Button disabled once the stand-up button took that spot — two similar boxes doing different things is a trap |

## Lighting

| # | Task | What it was |
|---|---|---|
| 9 | **Soft shadows actually enabled** | Android runs quality level "Medium", which capped shadows at *HardShadowsOnly*. The sun asked for Soft in code and was silently downgraded — every build so far had hard shadows |
| 10 | **Sun shadows lightened** | Strength 0.42 → 0.24, low-res map, 1 cascade, 14 m distance. Low resolution is the cheap route to soft: blurrier *and* faster |
| 11 | **Interior shadows added** | Bulbs cast none at all, so nothing indoors was grounded. The 4 pixel lights became downward spots — one shadow map each, not six cubemap faces — each with a vertex point light companion so ceilings stay lit |

**Not verified:** items 9–11 have no framerate measurement. Interior shadows are
the one change that could plausibly cost frames.
