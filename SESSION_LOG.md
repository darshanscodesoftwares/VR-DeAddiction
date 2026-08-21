# VR De-Addiction Wellness Software - Session Log

## Project Overview

- **Goal**: VR-based de-addiction wellness / rehabilitation system (POC first)
- **Headset**: Meta Quest 3S (standalone)
- **Dev machine**: MacBook Air M1, 8 GB RAM, 256 GB SSD
- **Unity version**: 6.5 (6000.5.8f1)
- **Project path**: `~/Downloads/VR-DeAddiction-POC`

## Tech Stack

| Layer | Technology |
|---|---|
| VR App | Unity 6 + C# |
| VR Hardware | OpenXR 1.18.0 (installed, Session 2) |
| VR Interaction | XR Interaction Toolkit 3.6.0 (installed, Session 2) |
| Backend (later) | NestJS / Node.js + PostgreSQL |
| AI/ML (later) | Python |
| 3D Assets (later) | Blender |

## Current Restrictions

- ~~No OpenXR, XR Interaction Toolkit~~ — installed in Session 2 (approved)
- No Meta SDK — and none planned; OpenXR only (hardware independence)
- No render pipeline migration (Built-in stays until we have a measured framerate)
- No heavy assets/packages
- Using only Unity built-in primitives and flat-color materials
- No backend or telemetry yet

## Architecture Approach

- Entire environment is **procedurally generated** from `Assets/Editor/PubEnvironmentBuilder.cs`
- No external textures - all flat Standard-shader materials with metallic/smoothness
- Build via Unity menu: **Tools > Pub Environment > Build**
- Clear via: **Tools > Pub Environment > Clear**
- Scene file: `Assets/unity-VR.unity`
- **Code is the source of truth** - manual scene edits are lost on rebuild

## What Was Built (Session 1 - Aug 17, 2026)

### 1. TASMAC-Style Indian Bar/Pub Interior

A realistic but lightweight Indian bar (TASMAC-style) environment using only Unity primitives.

**Building**: 12m wide x 20m deep, 3.8m eaves, shallow pitched roof

| Group | Contents |
|---|---|
| 01_Shell | Concrete floor, whitewashed brick side walls, pink/white plaster front/back walls, entrance with open doors, dado band (split around entrance), pilasters, ventilation windows with metal grilles, gable infill |
| 02_RoofStructure | 6 steel trusses with zigzag web pattern, support pillars, purlins, corrugated roof panels with translucent skylights, corrugation ribs |
| 03_ServiceArea | Concrete counter with return, teal horizontal-bar grille, bottle shelving with 20 bottles, 2 red beverage coolers |
| 04_Seating | 20 concrete tables (4x5 grid), 60 chairs (red/blue/green, 3 per table), 4 stacked spare chairs |
| 05_Fixtures | 6 ceiling fans (3 blades each, radially aligned), 5 pendant lights, 4 bare bulb fixtures, 6 wall tube lights, exposed conduit/pipe runs |
| 06_Signage | 4 banners, 5 framed posters, menu board, 2 small corner pictures |
| 07_Clutter | Water case stacks (9 left + 4 right), green crates, bottles/glasses on tables, floor litter |
| 08_Lighting | Directional sun, 5 warm pendant lights, counter light, 2 skylight bounces, entrance daylight |

### 2. Entrance Doors

- Two door leaves at the front entrance (steel frame + wooden panels)
- Left door: fully open at 90 degrees (swung inward)
- Right door: partially open at 35 degrees
- Wooden panels on the outside face of the door frames
- Back wall: solid (no service doorway)

### 3. Compound / Exterior Yard (Group 10_Compound)

Walled compound surrounding the building:
- **Yard**: 5m clearance each side, 8m approach in front, 2m behind
- **Ground**: Dirt/unpaved surface (brownish), street strip (dark grey asphalt) outside
- **Compound walls**: 2.5m tall weathered plaster with concrete caps on top
- **Gate entrance**: 3.8m wide opening in front wall with concrete gate posts
- **Overhead signboard**: Large green TASMAC-style board on steel frame with cream inner panel
- **Gate leaves**: Half-height (1.5m) wooden gates on steel frames, swung 90 degrees open inward
- **Utility pole**: Concrete pole with steel cross-arm and 3 insulators
- **Signs**: Green signboards on compound walls (inside and outside)
- **Gate light**: Point light illuminating the entrance area

### 4. First-Person Controller (Group 09_Player)

- Runtime script: `Assets/Scripts/FirstPersonController.cs`
- Uses Unity's built-in `CharacterController` (1.75m capsule)
- Player spawns on the street outside the compound gate
- Camera at eye height (1.6m)
- Old Main Camera is disabled when player is built

**Controls**:

| Key | Action |
|---|---|
| WASD / Arrows | Walk |
| Mouse | Look around |
| Left Shift | Sprint |
| Q / E | Float down / up (review mode) |
| Escape | Toggle cursor lock |

## Files Modified/Created

| File | Type | Purpose |
|---|---|---|
| `Assets/Editor/PubEnvironmentBuilder.cs` | Editor Script | Procedural environment generator |
| `Assets/Scripts/FirstPersonController.cs` | Runtime Script | First-person movement controller |
| `Assets/PubEnvironment/Materials/` | Materials (41) | Flat-color Standard shader materials |
| `Assets/unity-VR.unity` | Scene | Main scene file |

## Materials (41 total)

**Shell**: ConcreteFloor, PlasterPink, PlasterWhite, BrickWhitewash, Dado
**Structure**: SteelBlue, RoofSheet, Skylight (emissive)
**Furniture**: ConcreteTable, ChairRed, ChairBlue, ChairGreen
**Service**: GrilleTeal, CoolerRed, DarkWood, GlassClear (transparent)
**Clutter**: WaterCase, CrateGreen, FanBlade, Litter
**Lighting**: Bulb (emissive), Tube (emissive), Enamel
**Signage**: PosterGreen, PosterDark, PosterWarm, PosterCream
**Utility**: Metal, GlassAmber, GlassGreen
**Compound**: DirtGround, CompoundWall, GateWood, SignboardGreen, ConcretePole, StreetGrey

## Key Parameters (Tunable in Code)

```
HallWidth = 12m          HallDepth = 20m
EavesHeight = 3.8m       RidgeRise = 0.7m
WallThickness = 0.25m    DadoHeight = 1.10m
TrussCount = 6           RoofRibSpacing = 0.6m
TableCols = 4            TableRows = 5
ChairsPerTable = 3       Seed = 20260814

YardSide = 5m            YardFront = 8m
YardBack = 2m            CompoundWallH = 2.5m
GateOpeningW = 3.8m
```

## Bugs Fixed

1. **Fan blades jumbled**: Blade Y rotation was `-a` instead of `90-a`, causing tangential alignment instead of radial
2. **Dado blocking entrance**: `Dado_Front` spanned full 12m width including across the entrance opening; split into two segments
3. **Entrance doors swinging outward**: Rotation signs were inverted; corrected to swing inward
4. **Compound gate leaves direction**: Swapped rotation to swing inward into compound yard
5. **Door wooden panels**: Adjusted z-offset to place panels on the outside face of door frames

## Workflow Notes

- **Rebuild command**: Unity menu > Tools > Pub Environment > Build
- **All objects under `PubEnvironment_IN`** are destroyed and recreated on each build
- Manual scene edits are lost on rebuild - always update the code
- To adjust positions: move in Unity Scene view, note Inspector values, update code
- The `Seed` constant ensures reproducible random placement across rebuilds

## What Was Built (Session 2 - Aug 18, 2026): VR Compatibility

Goal of this session: make the project run on Meta Quest 3S, without tying the
codebase to Meta hardware.

### Toolchain

- Freed 14.9 GB of disk (2.8 GB -> 19 GB free) before starting; Unity could not
  have installed the Android module otherwise
- Installed **Android Build Support** + SDK/NDK/OpenJDK (8.7 GB) via Unity Hub CLI

### Packages Added (approved)

| Package | Version | Notes |
|---|---|---|
| `com.unity.xr.openxr` | 1.18.0 | Vendor-neutral runtime |
| `com.unity.xr.interaction.toolkit` | 3.6.0 | Rig + locomotion |
| `com.unity.xr.management` | 4.5.4 | Pulled automatically |
| `com.unity.inputsystem` | 1.20.0 | Pulled automatically |
| `com.unity.xr.core-utils` | 2.6.0 | Pulled automatically |
| `com.unity.xr.legacyinputhelpers` | 3.0.1 | Pulled automatically |

Backup of the original manifest: `Packages/manifest.json.bak`

### New Files

| File | Purpose |
|---|---|
| `Assets/Editor/XRRigBuilder.cs` | Procedural OpenXR rig (XR Origin, head, hands, locomotion) |
| `Assets/Editor/XRPluginSetup.cs` | Enables OpenXR loader + interaction profiles for Android |
| `Assets/Editor/QuestProjectSetup.cs` | Android/Quest player settings |
| `Assets/Editor/HeadlessTasks.cs` | Batch-mode scene rebuild (for CI later) |

New menus: **Tools > VR Setup > ...**

### Project Configuration Applied

- Build target Android, IL2CPP, **ARM64 only**, Vulkan, ASTC, min SDK 29, APK (not AAB)
- OpenXR loader assigned for Android, `InitManagerOnStart` = true
- **Single Pass Instanced** stereo rendering
- Interaction profiles enabled: Oculus Touch, Meta Quest Touch Plus, KHR Simple
  - Vive/Index profiles are PC-only and are not present in the Android feature
    set; standalone Vive/PICO would use their own OpenXR feature package or the
    KHR Simple fallback
- `activeInputHandler` = 2 (**Both**) so XRI and the legacy desktop controller coexist

### XR Rig Design

Built by `PubEnvironmentBuilder` so it survives rebuilds. Toggle with the
`PlayerRig` constant (`RigMode.Desktop` / `RigMode.VR`).

```
XR Origin  (XROrigin + CharacterController + LocomotionMediator
            + XRBodyTransformer + GravityProvider
            + ContinuousMoveProvider + SnapTurnProvider)
└─ Camera Offset
   ├─ XR Camera        (Camera + TrackedPoseDriver + AudioListener, tag MainCamera)
   ├─ Left Controller  (TrackedPoseDriver)
   └─ Right Controller (TrackedPoseDriver)
```

- Tracking origin mode: **Floor** (headset supplies real eye height)
- Move speed 1.6 m/s, **snap** turn 45 degrees (comfort: snap turning is far less
  nauseating than smooth turning, which matters for a clinical population)
- Input bound to generic OpenXR paths (`<XRHMD>`, `<XRController>{LeftHand}`),
  created in code and serialized into the scene - no `.inputactions` asset needed
- Camera is named `XR Camera`, not `Main Camera`, because the builder calls
  `GameObject.Find("Main Camera")` elsewhere to park the review camera

### Bugs Fixed (Session 2)

6. **New Input System was disabled**: `activeInputHandler` was 0 (legacy only),
   which would have made the headset track nothing and fail *silently*. Set to 2.
7. **XR camera name collision**: naming it `Main Camera` would let
   `PlaceReviewCamera()` grab and reposition the headset camera.

### Verified

Headless rebuild is clean: 0 compile errors, 1052 objects, ~23,652 triangles,
36 materials, all 8 input bindings serialized into `unity-VR.unity`.

## Running On Device (Session 2, later the same day)

### Device Setup

- Meta developer account, organisation **scodeVR**, 2FA verified
- Developer Mode on; adb authorised over USB (serial `340YC10H02033W`)
- Gotcha: Developer Mode was enabled *before* the developer org existed. The
  headset only re-checks entitlement on boot, so adb stayed invisible until a
  **reboot**. Symptoms looked exactly like a bad cable.
- Wireless adb (`adb tcpip 5555`) failed - same subnet and pingable, but the
  headset Wi-Fi radio power-saves when not worn. Retry while wearing it.

### Confirmed Working On The Quest 3S

| Feature | State |
|---|---|
| Immersive VR | `XR_SESSION_STATE_FOCUSED`, stereo swapchain 1680x1760 x2 |
| **Framerate** | **72.0 fps locked** (13.9 ms budget), occasional single dropped frame |
| Grab + throw | **Working** - 114 grabbables (bottles, glasses, 64 chairs) |
| Controllers | Articulated hands, fingers curl on analog grip/trigger |
| Hand tracking | 26 joints/hand, auto-swaps with controllers, whole-hand grasp |
| Anti-aliasing | MSAA 4x |
| Ground recovery | B button resets to spawn; auto-unstick after 1.5 s airborne |

### Performance Finding (Important)

The predicted performance problem **did not materialise**. 1,036 renderers, 10
realtime point lights, 114 rigidbodies, MSAA 4x and hand tracking together hold
72 fps. **No URP migration, no light baking, and no draw-call surgery are
needed.** The static-batching and shadows-off work from Session 1 did its job.

Caveat: all measurements so far are near-idle. Framerate while walking, grabbing
and throwing is still unmeasured.

### More Files

| File | Purpose |
|---|---|
| `Assets/Scripts/PerformanceProbe.cs` | Logs real render-loop FPS to logcat (`PERF`) |
| `Assets/Scripts/GroundRecovery.cs` | B-button reset + auto-unstick |
| `Assets/Scripts/ProceduralHand.cs` | Curls controller-hand fingers from grip/trigger |
| `Assets/Scripts/HandTrackingVisualizer.cs` | Tracked-hand joints + grasp-driven interactor |

### Bugs Fixed (continued)

8. **Empty build-settings scene list**: `m_Scenes: []` would have produced an APK
   that installs and launches to a black void.
9. **Missing Quest manifest markers**: without OpenXR's `MetaQuestFeature` the
   app opens as a flat 2D panel instead of immersive VR. Silent failure.
10. **Static flags on grabbables**: Unity batches static geometry into a combined
    mesh, and a batched object cannot move. Cleared across the hierarchy.
11. **`CS0162` unreachable code**: `PlayerRig` as `const` let the compiler fold
    the rig-mode branch. Changed to `static readonly`.
12. **Grab never fired**: select was driven via `manualPerformed`, which sets the
    *level* only. XRI starts a grab on the rising **edge**
    (`ReadWasPerformedThisFrame`). Fixed by using `QueueManualState()`, which
    derives both edges. This produced no error of any kind - the manager,
    interactors, rigidbodies and colliders all looked correct.
13. **Player floating**: could climb onto furniture and never fall. Step offset
    reduced 0.3 -> 0.15, plus `GroundRecovery` as a guaranteed escape.
14. **Misleading APK size log**: `BuildSummary.totalSize` is the uncompressed
    build (~522 MB), not the 31 MB APK. Now reports the real file size.

### Measurement Note

The Quest compositor always reports 72 Hz because it reprojects frames the app
misses. Compositor and `dumpsys` stats therefore cannot tell you whether the app
is actually hitting 72. `PerformanceProbe` measures inside the render loop,
which is the only number that means anything:

```
adb logcat -s Unity | grep PERF
```

## Next Steps (Not Yet Done)

### Immediate (VR)

- **Measure framerate under load** - walking, grabbing, throwing. All readings
  so far are near-idle.
- **Raise eye-texture resolution** above 1680x1760 for distance clarity. Now
  justified by the 72 fps headroom. (Residual softness is the 3S's Fresnel
  optics and panel - not fixable in software.)
- Retry wireless adb while wearing the headset
- Tune grasp thresholds if the whole-hand grab feels off

- Wall texture variation (stain patches for weathered look)
- Entrance threshold/step detail
- Washbasin/handwash area
- TV/small screen on wall
- Water jugs and steel tumblers on tables
- Electrical switchboard/fuse box
- Dustbin near counter
- Rate chart/price board near counter
- More varied chair placement
- Blender models for detailed assets
- Grab/interaction on bottles and glasses (XRI interactables - rig is ready for it)
- Patient/session management
- Behavioral data collection
- Backend API (NestJS)
- AI/ML integration (Python)

## Session 3 (Aug 19, 2026): Blender Assets, Grime, Bulb Lighting

### Blender Pipeline

Blender 5.2.0 LTS driven headlessly, exactly like Unity:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --python BlenderAssets/scripts/build_all.py
```

| File | Purpose |
|---|---|
| `BlenderAssets/scripts/pubkit.py` | lathe, boxes, curved panels, grime, FBX export |
| `BlenderAssets/scripts/build_all.py` | the assets themselves |
| `BlenderAssets/scripts/preview.py` | headless renders, for checking shapes |
| `BlenderAssets/reference/NOTES.md` | 11 reference photos, analysed |

Scripts are the source of truth; `.fbx` files are build artifacts.

**Reference photos confirmed the Session 1 build was right** -- blue zigzag
trusses, corrugated roof, ceiling fans, pink brick, concrete tables, red/blue/
green plastic chairs all match the real venue.

### Assets (1,616 tris total)

Plastic chair (460), spirit bottle (412), beer bottle (380), glass (164),
steel tumbler (164), concrete table (48).

### Grime

`Assets/PubEnvironment/VertexGrime.shader` -- Standard lighting with albedo
multiplied by vertex colour. Unity's Standard shader ignores vertex colours, so
grime baked in Blender would otherwise be invisible. Dirty areas also lose
smoothness. No texture files.

`pubkit.grime()` bakes three things: ground-up dirt on the lowest quarter,
crevice darkening from vertex valence, and low-frequency mottling.

### Lighting: sealed roof, bulbs only

Roof skylight bays closed (`RoofSkylights = false`), ambient dropped to near
black, fog recoloured to warm lamp haze. Interior lights 6 -> 27.

**The cost model that matters:** in Built-in forward rendering every *pixel*
light adds a render pass per affected renderer. Six pixel lights over 650
renderers took the headset from 72 fps to ~36. The fix was not fewer lights but
the right kind:

- **4 ForcePixel** lights (3 centre pendants + counter) cast the visible pools
- **23 ForceVertex** lights colour and lift the room for almost nothing

Plus `pixelLightCount = 4`, trimmed pixel-light ranges, and **GPU instancing**
on all materials (36 chairs and 38 glassware items share mesh+material).

### Layout

Tables reduced 4x5 -> 3x4. Objects 650 -> 613, tris 58,332 -> 45,384,
grabbables 102 -> 75.

### Bugs Fixed (Session 3)

15. **Built Y-up, Blender is Z-up**: every asset exported lying on its side.
16. **Chair back splayed**: each panel leaned about its own base; fixed with a
    shared pivot (`lean_about`).
17. **FBX root rotation overwritten**: Blender FBX imports with a -90 X rotation
    that stands the mesh upright. `Model()` wrote yaw straight onto that
    transform and flattened everything into the floor. Yaw now goes on a holder.
18. **bake_space_transform=True** skipped the axis conversion entirely.
19. **Bottles floating**: clutter still used the old 0.80 m table top (now 0.775).
20. **Grab twitching**: `VelocityTracking` fights the solver against 100+ bodies.
    Switched to `Kinematic`; `throwOnDetach` still carries release velocity.
21. **Invisible block above tables**: chairs had ONE box collider 0 - 0.90 m
    while tucked 0.78 - 0.94 m from table centre, so it reached under the table
    and stood above its 0.775 m top. Objects rested on it. Now three colliders
    following the real shape, with a thin back.
22. **Chairs unreachable + facing backwards**: Blender (x,y,z) maps to Unity
    (x,z,-y), so a back modelled at Blender -Y landed at Unity +Z while its
    collider sat at -Z. Back rebuilt at +Y. Seat collider also thickened
    55 -> 110 mm; a 55 mm target is very hard to hit with a tracked hand.
23. **Pixel-light overload**: see lighting above. 72 -> 36 fps.

### Verified On Device

40-second capture: **every sample 72.0 fps, worstFrame 13.9 ms, zero dropped
frames** -- with models, grime, 27 lights, hand tracking and 75 grabbables.

### Measurement Note

Compositor stats always report 72 Hz because dropped frames are reprojected.
Only `PerformanceProbe` (inside the render loop) tells the truth:

```sh
adb logcat -s Unity | grep PERF
```

## Session 4 (Aug 20, 2026): Seated Table Scenario

The first interactive scenario. Approach a marked table, sit, and handle what
is on it. Spec and reasoning: `SCENARIO_SPEC.md`.

### Design

Explored with a 7-agent design review across clinical, interaction,
architecture and realism angles, then critiqued adversarially against the
project's hard constraints. It killed the Unity-scene-load approach on a
specific repo fact -- `PubEnvironmentBuilder.cs:111` parents the XR Origin
INSIDE the generated root, so it is not a scene root and `DontDestroyOnLoad`
cannot preserve it -- confirming the seamless in-place decision.

### New Files

| File | Purpose |
|---|---|
| `Assets/Editor/PubScenarioBuilder.cs` | Group `11_SeatedScenario`: hero table, marker, seat anchor, props, containment |
| `Assets/Scripts/SeatedTableScenario.cs` | Bar / AtMarker / Seated state machine, sit-stand, interaction gate |
| `Assets/Scripts/LightingDirector.cs` | Cross-fades the room between gloomy-warm and seated-focus |
| `Assets/Scripts/TableContainment.cs` | Speed cap, invisible lip, soft return |
| `Assets/Editor/LayoutCheck.cs` | Measures positions and facing -- catches placement bugs |

### How It Works

- Hero table at (-2.6, 1.4), mid-hall so the bar stays visible all around
- Marker position is **searched**, not hand-placed: candidates radiate from the
  seat and the first with nothing in a 0.45 m sphere wins (probed at torso
  height, so tabletops count as blocked)
- Sit/stand: **A/X on either controller**, or **dwell 1.5 s on the marker** /
  **both palms above head for 0.8 s** with hands only
- Seated eye height 1.18 m via the **camera offset**, never the rig
- 7 gated props: 2 beer bottles, whisky quart, water bottle, steel tumbler,
  glass, ashtray

### Lighting

Three moods cross-faded by `LightingDirector`. Pixel-light count NEVER rises:
the table focus light is promoted to pixel only while seated, and the pendant
furthest from the hero table is demoted to vertex to pay for it. Background is
darkened with **fog**, which is free, rather than by dimming 23 lights.

### Bugs Fixed (Session 4)

24. **Sit/stand bound to `<XRController>`** -- with hands only there is no
    XRController device, so hand-tracking patients could neither sit nor stand.
25. **Hero chair grabbable at the seat position** -- a 4.5 kg rigidbody where
    the capsule teleports; could launch, or be thrown while sat in.
26. **Only hand locomotion suspended when seated** -- controllers could walk out
    of the chair. Now move, turn, hand and gravity providers all suspend.
27. **`GroundRecovery` live while seated** -- B would teleport a seated patient
    to the street outside the compound gate.
28. **No fade on the seat glide** -- sliding the view unfaded is a direct
    visual-vestibular mismatch.
29. **Chairs at `ang + 180`** -- correct only at 90 deg, so most chairs faced
    away from their tables. Correct value is `-ang`, derived from the geometry.
30. **Chairs spawned overlapping tables** -- a side chair at 0.78 m reached
    x = 0.57 against a table half-width of 0.585, so physics flung them across
    the room on load. Now 0.95-1.05 m.
31. **Stacked spare chairs exploded** -- four chairs 0.26 m apart with 0.9 m
    colliders intersect by design. Now scenery: no rigidbody, no colliders.
32. **Marker inside another table** -- placed at (-2.60, -0.25); `TableUnit_06`
    is at (-2.81, -0.15). Hence the search.
33. **Containment lip OUTSIDE the table edge** -- lip at 0.62 x 0.45 against a
    top of half-extents 0.55 x 0.38, so objects rolled off before reaching it.
34. **Seat anchor at floor level** -- Floor tracking reports real head height,
    so moving the rig there left the patient STANDING at the table.
35. **Lowering the RIG buried the capsule** -- the capsule spans rigY to
    rigY+1.75; a 0.4 m drop put it 0.4 m under the floor collider and physics
    shoved it back up, ejecting the patient from the seat. Fixed by lowering
    `CameraFloorOffsetObject` instead: the view drops, the capsule does not move.
36. **Rig yaw ignored head yaw** -- where you look is rig yaw PLUS physical head
    yaw, so sitting left you facing sideways.
37. **Seat chair at yaw 180** -- backrest between the patient and the table.
38. **Sitting moved the RIG to the seat, not the CAMERA** -- in Floor tracking
    the camera can be a metre from the rig origin, so the patient was left
    standing near the marker, out of reach. The old teleport corrected for this;
    `Sit()` never did.

### Process Note

Most of the above are one mistake repeated: placing and rotating objects by
assumption instead of measuring. `LayoutCheck.cs` reads back positions and
facing directly and should be run after every layout change.

Also: a build was reported as succeeding because an APK existed on disk -- it
was the previous day's file, and the real build had been killed by a tool
timeout. **Verify builds by the `[Headless] APK built` log line, never by the
artifact**, and run long builds detached.
