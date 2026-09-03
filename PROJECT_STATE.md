# VR De-Addiction POC — Project State

Complete working state as of **3 Sep 2026**. Written to survive context loss:
read this first to know what exists, how to drive it, and what has already been
learned the hard way. Per-session narrative lives in `SESSION_LOG.md`.

---

## What this is

A VR alcohol cue-exposure environment for de-addiction therapy. A procedurally
generated TASMAC-style Indian bar (Tamil Nadu state liquor bar), running
standalone on a Meta Quest 3S.

**Status: the environment works and runs at a locked 72 fps.** The clinical
layer does not exist yet.

---

## Hard constraints (user-stated, non-negotiable)

| Rule | Detail |
|---|---|
| No new dependencies | No packages, assets, plugins or **texture files** without explicit approval |
| OpenXR only | No Meta SDK. Must stay portable to VIVE/PICO |
| 72 fps on Quest 3S | Non-negotiable; measured, not assumed |
| Code is the source of truth | Edit the procedural builders. Manual scene edits are destroyed on rebuild |
| Layering | Session logic = plain C#, no XR types, so it stays portable |
| Patient identity | From app/backend. **Never** biometric or facial recognition |
| Dev machine | MacBook Air M1, 8 GB RAM. Disk is chronically tight (~2-8 GB free) |

---

## Toolchain

- **Unity 6.5 (6000.5.8f1)**, Built-in Render Pipeline (NOT URP)
- **Blender 5.2.0 LTS**, driven headlessly
- Packages: `com.unity.xr.openxr` 1.18.0, `com.unity.xr.interaction.toolkit` 3.6.0,
  `com.unity.xr.hands` 1.9.0 (+ management, inputsystem, core-utils, collections)
- Target: Android / IL2CPP / ARM64 / Vulkan / ASTC / minSDK 29 / APK
- App id `com.scodevr.vrdeaddiction`, org **scodeVR**, device serial `340YC10H02033W`

### Commands

```sh
# Rebuild the scene (Unity MUST be closed)
Unity -batchmode -quit -nographics -projectPath . -executeMethod HeadlessTasks.RebuildScene

# Build the APK -- run DETACHED, tool timeouts kill it mid-build
nohup Unity -batchmode -quit -nographics -projectPath . \
  -executeMethod HeadlessTasks.BuildAPK -logFile build.log &

# Blender assets
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python BlenderAssets/scripts/build_all.py
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python BlenderAssets/scripts/hand.py

# Install + run
ADB=/Applications/Unity/Hub/Editor/6000.5.8f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB install -r Builds/VRDeAddiction.apk
$ADB shell am start -n com.scodevr.vrdeaddiction/com.unity3d.player.UnityPlayerGameActivity

# The ONLY trustworthy framerate reading
$ADB logcat -s Unity | grep PERF     # also reports camY / rigY
```

**Verify a build by the `[Headless] APK built` log line, never by an APK
existing on disk** — a stale file from a previous run has already been
mistaken for success once.

**Gate the install on that line, never on the build merely finishing.** A
watcher that greps for `[Headless] Build ` also matches `Build Failed`, and has
twice installed a *stale* APK onto the headset after a failed build — which
looks exactly like a fix that did not work. Match `APK built` alone, and treat
"Unity exited having printed neither verdict" as failure.

**`Build Failed: 1 error(s)` with no `error CS` line usually means the disk is
full.** The real cause hides far up the log as
`No space left on device` while copying `Library/Bee/artifacts/...`. IL2CPP
needs several GB of headroom; this machine sits at 2–8 GB free, so check
`df -h` *before* starting a build, not after it fails.

---

## Files

### Unity — editor (build the world)

| File | Role |
|---|---|
| `Assets/Editor/PubEnvironmentBuilder.cs` | The whole bar: shell, roof, counter, seating, fixtures, signage, clutter, compound, lighting |
| `Assets/Editor/PubScenarioBuilder.cs` | Group `11_SeatedScenario`: hero table, marker, seat, props, containment |
| `Assets/Editor/XRRigBuilder.cs` | XR rig: origin, camera, hands, interactors, locomotion |
| `Assets/Editor/XRPluginSetup.cs` | OpenXR loader + interaction profiles |
| `Assets/Editor/QuestProjectSetup.cs` | Player settings, MSAA, pixel-light count |
| `Assets/Editor/HeadlessTasks.cs` | Batch rebuild / APK build |
| `Assets/Editor/*Check.cs` | LayoutCheck, SceneCheck, GrabCheck, ChairCheck, RigCheck, PropCheck, ModelCheck, RigDump |

### Unity — runtime

| File | Role |
|---|---|
| `Assets/Scripts/SeatedTableScenario.cs` | Bar / AtMarker / Seated state machine |
| `Assets/Scripts/LightingDirector.cs` | Cross-fades room moods |
| `Assets/Scripts/TableContainment.cs` | Keeps props on the table |
| `Assets/Scripts/HandLocomotion.cs` | Arm-swing locomotion for hand tracking |
| `Assets/Scripts/HandTrackingVisualizer.cs` | Tracked-hand joints, grasp-to-grab |
| `Assets/Scripts/RiggedHandPose.cs` | Curls the rigged hand from grip/trigger |
| `Assets/Scripts/GroundRecovery.cs` | B-button reset + instant fall |
| `Assets/Scripts/PerformanceProbe.cs` | Logs real fps + camY/rigY |
| `Assets/PubEnvironment/VertexGrime.shader` | Standard lighting × vertex colours |

### Blender

`BlenderAssets/scripts/pubkit.py` (lathe with fluted/square sections, taper_box,
curved_panel, grime, skin_tone, material slots, FBX export) ·
`build_all.py` (props) · `hand.py` · `preview.py` (headless renders) ·
`reference/NOTES.md` (11 analysed reference photos)

---

## How the environment works

- 12 × 20 m hall, sealed roof, 3×4 concrete tables, 2 chairs each
- ~690 objects, ~55k triangles, **72 fps locked**
- Lighting: **4 ForcePixel + 23 ForceVertex**, ambient near-black, warm fog
- Sun overhead at 84°, overcast procedural skybox
- ~73 grabbables with `XRGrabInteractable`, Kinematic movement, throw on release

### Seated scenario

Hero table at **(-2.6, 1.4)**. Approach the marker → sit → room dims to gloomy
warm, table lights up, 6 props become grabbable. Stand at any time.

- **Sit/stand:** A/X on either controller · or dwell 1.5 s on marker /
  raise both palms above head 0.8 s (hands only)
- Marker position is **searched** for clear floor, not hand-placed
- Seated eye height 1.18 m via the **camera offset**, never the rig

---

## Hard-won lessons — read before changing anything

### Performance

- Cost scales with **pixel lights × affected renderers**, not light count.
  Six pixel lights took 72 fps → 36. Four pixel + 23 vertex looks the same.
- **Variety is cheap when shared, expensive when unique.** Randomising material
  slots per object broke instancing and cost ~7 fps; six shared bottle brands
  look just as varied.
- Triangles have never been the constraint. Draw calls are.
- The Quest compositor always reports 72 Hz because it reprojects dropped
  frames. Only `PerformanceProbe` tells the truth.

### Blender → Unity

- Build in **Blender Z-up**. Export with `bake_space_transform=False`.
- The FBX imports with a **-90° X rotation on its root**. Writing yaw straight
  onto that transform destroys it and lays the asset flat — put yaw on a parent.
- Axis mapping is `(x, y, z)_blender -> (x, z, -y)_unity`. Getting this wrong
  put a chair's back on the wrong side and its collider in empty air.
- `pubkit` builds boxes and lathes. It **cannot** produce organic shapes — the
  hand attempt read as a plank. Unity's XR Hands package ships a properly
  rigged hand; use that.

### XR

- `GravityProvider` re-centres the CharacterController on the camera every
  frame. Leaning at a table pushes the capsule into it and shoves the rig back.
  Disabled via `updateCharacterControllerCenterEachFrame = false`.
- The capsule spans `rigY` to `rigY+1.75`. **Lowering the rig buries it under
  the floor** and physics ejects the player. Lower `CameraFloorOffsetObject`.
- In Floor tracking the camera can be a metre from the rig origin. Move the rig
  so the **camera** lands on target, not the rig.
- Where the player looks is rig yaw **plus** physical head yaw.
- XRI starts a grab on the rising **edge** (`ReadWasPerformedThisFrame`).
  `manualPerformed` sets only the level — use `QueueManualState()`.
- `<XRController>` bindings **do not exist** in a hands-only session. Every
  control needs a hand route too.
- Grabbed objects: use `Kinematic`, not `VelocityTracking` — the latter fights
  the solver against 100+ bodies and reads as twitching.

### Placement

Most bugs this project has had come from **placing and rotating by assumption**.
Compute clearances against real collider extents, then verify with
`LayoutCheck.cs`, which reads back positions and facing.

---

## What changed on 2-3 Sep (uncommitted)

Detail in `TODAY_SEP02_03.md`. Headlines:

- **Downloaded models replaced the procedural furniture** -- round bar tables,
  plastic monobloc chairs, a Jack Daniel's bottle, two glasses, an X icon.
  Each has a Blender script in `BlenderAssets/scripts/`. Source downloads are
  NOT in the repo; the scripts regenerate the exports.
- **Textures** on the yard (ambientCG Ground109), the hall floor (Concrete034)
  and the sky (a de-dithered panorama). All with normal maps.
- **Audio**: footsteps sliced from a real walk, a grab clink, and a fluorescent
  flicker whose light is driven by the recording's own envelope.
- **The TASMAC sign** rebuilt as a lit board with floodlights and a feed wire.
- **`LayoutAudit.cs`** measures tables, chairs, props and the marker at build
  time.

### Numbers that other code depends on

| Thing | Value | Where it comes from |
|---|---|---|
| Table radius | 0.367 m | `PubEnvironmentBuilder.TableRadius` |
| Table top height | 0.775 m | `PubScenarioBuilder.TableTop` |
| Chair spacing from table | 0.74-0.82 m | radius + chair half-depth 0.314 |
| Hero prop usable radius | 0.30 m | rim minus widest prop |
| Ground tiling | 2.8 m | ambientCG's stated capture size |
| Floor tiling | 2 m | chosen |
| Shadow distance | 12 m | `QuestProjectSetup` |
| Interior shadow maps | 128 | `Downlight()` |

## Lessons added on 2-3 Sep

- **`Mat()` loads .mat ASSETS from disk and reuses them between builds.** A
  material that was emissive once stays emissive until something clears it.
  Changing `MatEmissive` to `Mat` in the source did nothing until `Mat()` was
  made to clear emission explicitly.
- **A Blender FBX imports with a -90 X rotation on its ROOT.** Take only the
  mesh and you throw that away, and the asset lies flat. Instantiate the model
  as a child and copy its rotation and scale -- what `Model()` already does.
- **Compose scale, never assign it.** Assigning after an `origin_set` discards
  the FBX's import scale; that produced a three-metre drinking glass.
- **Sketchfab "texture" downloads are usually UV ATLASES**, not tileable
  materials. Rendering the mesh from above beats trying to crop one.
- **Overlapping box strokes at the same depth z-fight.** The sign's S and M
  flickered until each stroke was given a 0.4 mm depth offset.
- **Static batching was never enabled for Android** -- the platform entry was
  an empty list, so it took a default rather than a decision.
- **Fixed grip points with dynamic attach OFF break grabbing.** Tried and
  reverted. Nudge the object AFTER the grab instead, and limit finger curl to
  the held object's measured thickness.
- **Layout faults are measurement faults.** Chairs in tables, bottles off rims,
  crates in counters, a hero table wedged between neighbours -- every one was
  invisible in the source and obvious the moment something measured it. Run
  `LayoutAudit.Run` after any layout change.

## Open / not built

0. **No framerate measurement exists for any change made on 2-3 Sep.** The
   `PERF` probe only logs while the headset is worn, and every capture window
   came back empty. Several large lighting changes went in on reasoning alone.
   Measure before building further on them.
1. **Clinical layer** — craving measurement, graded cue-intensity ladder,
   response-prevented handling, decompression exit. A design review was blunt:
   *"as written this builds a craving-induction engine, not a therapy."*
2. **Session governance** — max length, spacing, stopping rule. Gates the first
   patient session.
3. **Safety abort** — `GroundRecovery` binds B to `<XRController>`, so a
   hand-tracking patient has no in-VR way to stop. Needs a hand route.
4. **No audio at all.**
5. **Tracked hands** still draw joint cubes; only controller hands use the
   rigged mesh.
6. **No telemetry**, no backend, no patient/session records.
7. **ScenarioCheck.cs** — the seated flow has no automated verification.
