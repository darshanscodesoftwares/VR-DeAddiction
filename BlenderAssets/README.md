# Blender Asset Pipeline

Procedural asset modelling for the VR De-Addiction POC. Same philosophy as
`Assets/Editor/PubEnvironmentBuilder.cs`: **the script is the source of truth**,
the mesh is a build artifact. Edit the Python, not the `.blend`.

## Layout

```
BlenderAssets/
  scripts/      Python (bpy) that models and exports each asset
  reference/    Reference photographs
Assets/Models/  FBX output -- Unity imports these automatically
```

## Running

Blender is driven headlessly, exactly like Unity:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --python BlenderAssets/scripts/build_all.py
```

No GUI, no manual modelling, reproducible from a clean checkout.

## Conventions

These exist so imported meshes drop straight into the existing builder without
per-asset fixing up:

| Rule | Reason |
|---|---|
| **+Z forward, +Y up, metres** | Matches Unity. Export handles the axis conversion. |
| **Origin at the base**, centred in X/Z | The builder positions objects by their footprint on a table or floor. |
| **Real-world scale** | A 0.3 m bottle is 0.3 m. The scene is already dimensioned in metres. |
| **No materials from Blender** | Unity assigns the existing flat Standard materials. Keeps the 41-material palette consistent and avoids importing shader trees that Built-in RP cannot use. |
| **Apply all transforms** before export | Otherwise Unity inherits odd scales that break physics colliders. |
| **One asset per file**, named `Asset_<Name>.fbx` | Keeps imports predictable and diffs small. |

## Poly budget

The scene currently runs **23,652 triangles at a locked 72 fps** on Quest 3S, so
triangles are not the constraint -- draw calls are (~1,000). Shared meshes across
the 64 chairs actually *reduce* draw calls through GPU instancing.

Working targets, generous but sane for mobile VR:

| Asset | Triangles |
|---|---|
| Bottle / glass | 150 - 300 |
| Chair | 300 - 600 |
| Table | 200 - 400 |
| Counter, coolers | 500 - 1,500 |
| Structural (trusses, shell) | keep low, they are large and numerous |

## Pipeline

```
scripts/*.py --[blender --background]--> Assets/Models/*.fbx
    -> Unity imports -> PubEnvironmentBuilder instantiates
    -> HeadlessTasks.BuildAPK -> adb install -> Quest 3S
```

Everything downstream of the FBX already works.
