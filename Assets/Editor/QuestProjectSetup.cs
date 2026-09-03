// QuestProjectSetup.cs
//
// Configures the project for standalone Android XR headsets (POC target:
// Meta Quest 3S). Everything here is vendor-neutral: no Meta/Oculus APIs,
// no store-specific manifest entries. Moving to VIVE/PICO later should be a
// build-target and OpenXR-feature change, not a code change.
//
//   Tools > VR Setup > Configure Android XR Player Settings
//
// Also callable headlessly:
//   Unity -batchmode -quit -projectPath . -executeMethod QuestProjectSetup.Configure
//
// Deliberately NOT changed here:
//   - Color space (Linear is better for VR but visibly re-grades all 41
//     existing materials, so that is a separate, explicit decision).
//   - Render pipeline (Built-in stays until we have a measured framerate).

using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class QuestProjectSetup
{
    // Quest 3S runs Android 12L. 29 is the practical floor for Quest devices;
    // 32+ is what Meta requires for store submission. Sideloaded POC builds
    // are lenient, but there is no cost to being correct now.
    const AndroidSdkVersions MinSdk = AndroidSdkVersions.AndroidApiLevel29;

    // Matches the Meta Horizon developer organisation "scodeVR".
    // PackageId is the identity the headset installs against -- changing it
    // after install produces a second, separate app rather than an update.
    const string CompanyName = "scodeVR";
    const string ProductName = "VR De-Addiction POC";
    const string PackageId = "com.scodevr.vrdeaddiction";

    [MenuItem("Tools/VR Setup/Configure Android XR Player Settings")]
    public static void Configure()
    {
        var group = BuildTargetGroup.Android;
        var named = NamedBuildTarget.Android;

        // ---- Build target -------------------------------------------------
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[QuestSetup] Switching active build target to Android...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, BuildTarget.Android);
        }

        // ---- Identity -----------------------------------------------------
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.SetApplicationIdentifier(named, PackageId);

        // ---- Scripting / architecture --------------------------------------
        // Quest is ARM64-only. IL2CPP is mandatory for ARM64 output.
        PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // ---- Input backends ---------------------------------------------------
        // Must be "Both" (2). XRI and TrackedPoseDriver read through the new
        // Input System, while FirstPersonController.cs (desktop review mode)
        // still calls the legacy UnityEngine.Input API. Left at the default of
        // 0 (old only), the headset tracks nothing and fails silently.
        // There is no PlayerSettings property for this, hence SerializedObject.
        SetActiveInputHandler(2);

        // ---- Android SDK levels ---------------------------------------------
        PlayerSettings.Android.minSdkVersion = MinSdk;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // ---- Graphics --------------------------------------------------------
        // Vulkan is the recommended API for Quest on Unity 6. Explicit list, no
        // auto-fallback to GLES, so we know exactly what the headset receives.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });

        // Mobile GPUs need ASTC; anything else bloats the APK badly.
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        // Headsets render in landscape and must never auto-rotate.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // Multithreaded rendering matters a lot on mobile chips.
        PlayerSettings.MTRendering = true;

        // ---- Packaging --------------------------------------------------------
        // APK for sideloading over adb, not AAB (that is for store upload).
        EditorUserBuildSettings.buildAppBundle = false;

        // ---- Batching -----------------------------------------------------
        //
        // 635 of this scene's renderers are marked BatchingStatic, and the
        // Android batching setting was never written -- the platform entry in
        // ProjectSettings was an empty list, so it was taking whatever the
        // default happened to be rather than anything decided here.
        //
        // Static batching is the single biggest draw-call saving available to
        // this project. Draw calls, not triangles, are what it runs out of --
        // looking down the length of the hall puts almost every object on
        // screen at once, which is exactly the view that drops frames.
        //
        // Dynamic batching stays OFF: it re-transforms vertices on the CPU
        // every frame and only applies to small meshes, and the movable objects
        // here are the ~50 grabbables, which already share meshes and materials
        // and so instance instead.
        SetBatching(BuildTarget.Android, staticBatching: true, dynamicBatching: false);

        ConfigureQualityForVR();

        AssetDatabase.SaveAssets();

        Debug.Log(
            "[QuestSetup] Configured.\n" +
            $"  target        : Android ({PlayerSettings.Android.targetArchitectures})\n" +
            $"  backend       : {PlayerSettings.GetScriptingBackend(named)}\n" +
            $"  min SDK       : {PlayerSettings.Android.minSdkVersion}\n" +
            $"  graphics      : Vulkan\n" +
            $"  texture comp  : ASTC\n" +
            $"  identifier    : {PackageId}");
    }

    /// <summary>
    /// MSAA is the main image-quality lever in VR. Thin distant geometry
    /// (truss webs, chair legs, grille bars) shimmers badly without it.
    ///
    /// On the Quest's tile-based Adreno GPU, MSAA resolves inside tile memory
    /// rather than main memory, so 4x typically costs only a few percent --
    /// unlike on desktop, where it is expensive. It is close to free quality.
    /// </summary>
    /// <summary>
    /// Sets per-platform batching. The typed API has moved between Unity
    /// versions, so this goes through reflection and reports plainly if it is
    /// not there rather than silently doing nothing.
    /// </summary>
    static void SetBatching(BuildTarget platform, bool staticBatching, bool dynamicBatching)
    {
        var method = typeof(PlayerSettings).GetMethod(
            "SetBatchingForPlatform",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (method == null)
        {
            Debug.LogWarning("[QuestSetup] SetBatchingForPlatform not found; " +
                             "batching left at the project default.");
            return;
        }

        method.Invoke(null, new object[]
        {
            platform, staticBatching ? 1 : 0, dynamicBatching ? 1 : 0
        });
        Debug.Log($"[QuestSetup] Batching for {platform}: static={staticBatching}, " +
                  $"dynamic={dynamicBatching}.");
    }

    static void ConfigureQualityForVR()
    {
        int original = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;

        for (int i = 0; i < names.Length; i++)
        {
            // antiAliasing is a per-quality-level property, so each level has
            // to be made current before it can be written.
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.antiAliasing = 4;

            // Android defaults to quality level 2 ("Medium"), which allowed a
            // single pixel light. With the interior lit only by bulbs, every
            // light beyond the first degraded to a vertex light and the room
            // read as flat ambient fill. Point lights here cast no shadows, so
            // the extra cost is a forward pass on affected objects only.
            // 4, not 6: only four lights are ForcePixel, and each pixel light
            // costs a forward pass per affected renderer. Six halved the
            // framerate on device (72 -> ~36 fps).
            // 3, not 4. Each pixel light adds a full render pass per affected
            // renderer, and with ~650 renderers the fourth slot is the most
            // expensive light in the scene for the least visible gain.
            QualitySettings.pixelLightCount = 3;

            // SHADOWS.
            //
            // Android defaults to quality level 2 ("Medium"), whose shadows
            // setting was HardShadowsOnly. That is a CAP, not a default: the
            // sun asked for LightShadows.Soft in code and was silently
            // downgraded to hard on device. Every shadow in the build has been
            // hard-edged regardless of what the builder requested.
            QualitySettings.shadows = ShadowQuality.All;

            // Low resolution is the cheap way to get SOFT. A shadow map with
            // fewer, larger texels blurs under the soft filter, and costs less
            // to render and to sample. High resolution would fight the goal in
            // both directions: sharper and slower.
            QualitySettings.shadowResolution = ShadowResolution.Low;

            // One cascade, and a distance that just covers the building rather
            // than the default 20 m. Shorter distance means fewer casters
            // rendered into the map every frame, which is what pays for the
            // more expensive soft filter. The roof is sealed, so sun shadows
            // are only ever seen outdoors, near the player.
            QualitySettings.shadowCascades = 1;

            // 22 m: far enough to cover the hall end to end.
            //
            // This is the biggest lever on shadow cost -- every caster inside it
            // is re-rendered into every shadow map, every frame -- and I cut it
            // to 9 m to pay for more shadowed lights. That is what made shadows
            // appear and disappear as you walked: anything past 9 m simply
            // stopped casting. Consistency is worth more here than the saving,
            // so it goes back up past the 20 m length of the hall.
            // 16 m. 22 covered the hall corner to corner but re-rendered every
            // caster in it into every map; 9 was cheap but made shadows vanish
            // as you walked. 16 keeps everything you are near and working with
            // shadowed, and only the far end of the room falls out.
            // 12 m. Looking down the hall puts the whole room in view, and every
            // caster inside this radius is re-rendered into every shadow map.
            // At 12 the shadows you are standing among are all still there and
            // the far end of the room stops paying for shadows nobody can
            // resolve at that distance anyway.
            QualitySettings.shadowDistance = 10f;

            // Nothing in this scene is reflective enough to justify realtime
            // probes, and they re-render the surroundings.
            QualitySettings.realtimeReflectionProbes = false;

            // StableFit keeps shadow edges from crawling as the player walks.
            // CloseFit uses texels more efficiently but shimmers, and shimmer
            // is worse than softness in a headset.
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowNearPlaneOffset = 1f;
        }

        QualitySettings.SetQualityLevel(original, false);
        Debug.Log($"[QuestSetup] MSAA 4x, 4 pixel lights, SOFT shadows (low res, 14 m, 1 cascade) on all {names.Length} quality levels.");
    }

    /// <summary>
    /// 0 = legacy Input Manager only, 1 = new Input System only, 2 = both.
    /// Changing this requires an editor restart to take effect.
    /// </summary>
    static void SetActiveInputHandler(int mode)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[QuestSetup] Could not load ProjectSettings.asset.");
            return;
        }

        var so = new SerializedObject(assets[0]);
        SerializedProperty prop = so.FindProperty("activeInputHandler");
        if (prop == null)
        {
            Debug.LogError("[QuestSetup] 'activeInputHandler' property not found.");
            return;
        }

        if (prop.intValue == mode)
        {
            Debug.Log($"[QuestSetup] activeInputHandler already {mode}.");
            return;
        }

        int previous = prop.intValue;
        prop.intValue = mode;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.LogWarning($"[QuestSetup] activeInputHandler {previous} -> {mode} (Both). " +
                         "An editor restart is required before XR input works.");
    }
}
