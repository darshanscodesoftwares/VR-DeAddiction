// XRPluginSetup.cs
//
// Enables OpenXR as the XR loader for Android and turns on a spread of
// controller interaction profiles.
//
//   Tools > VR Setup > Enable OpenXR For Android
//
// Headless:
//   Unity -batchmode -quit -projectPath . -executeMethod XRPluginSetup.Enable
//
// WHY SEVERAL INTERACTION PROFILES
// --------------------------------
// A profile is a controller-layout declaration, not a vendor SDK. Enabling
// Quest, Vive, and Index profiles together costs nothing at runtime -- OpenXR
// binds whichever the connected runtime reports -- and it is what makes the
// "swap the headset, not the code" requirement literally true. KHR Simple is
// the spec's generic fallback for anything unrecognised.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

public static class XRPluginSetup
{
    const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
    const string SettingsDir = "Assets/XR";
    const string SettingsAsset = SettingsDir + "/XRGeneralSettings.asset";

    [MenuItem("Tools/VR Setup/Enable OpenXR For Android")]
    public static void Enable()
    {
        const BuildTargetGroup group = BuildTargetGroup.Android;

        // ---- XR general settings container ---------------------------------
        if (!EditorBuildSettings.TryGetConfigObject(
                XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
                AssetDatabase.Refresh();
            }

            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, SettingsAsset);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            Debug.Log("[XRPluginSetup] Created " + SettingsAsset);
        }

        if (!perTarget.HasManagerSettingsForBuildTarget(group))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

        XRGeneralSettings settings = perTarget.SettingsForBuildTarget(group);
        if (settings == null || settings.Manager == null)
        {
            Debug.LogError("[XRPluginSetup] Could not obtain XR manager settings for Android.");
            return;
        }

        // Start XR automatically when the app launches on the headset.
        settings.InitManagerOnStart = true;

        // ---- Assign the OpenXR loader ---------------------------------------
        if (XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXRLoaderType, group))
            Debug.Log("[XRPluginSetup] OpenXR loader assigned for Android.");
        else
            Debug.LogError("[XRPluginSetup] Failed to assign the OpenXR loader.");

        // ---- OpenXR settings --------------------------------------------------
        OpenXRSettings openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (openxr == null)
        {
            Debug.LogError("[XRPluginSetup] No OpenXR settings for Android.");
            return;
        }

        // Renders both eyes in one pass. The single largest stereo perf win on
        // mobile GPUs; it is the package default, set explicitly so a future
        // change is visible in code rather than in a hidden asset.
        openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

        // Meta Quest Support. This is a BUILD-TIME manifest feature shipped
        // inside Unity's vendor-neutral OpenXR package -- not the Meta SDK, and
        // not a code dependency. Without it the AndroidManifest lacks the Quest
        // launch markers and the headset opens the app in a flat 2D panel
        // instead of immersive VR. Nothing in our C# references it, so building
        // for VIVE/PICO just means disabling this and enabling theirs.
        EnableFeature<MetaQuestFeature>(openxr, "Meta Quest Support (manifest)");

        // Camera-based hand tracking. Vendor-neutral: this is the Khronos
        // XR_EXT_hand_tracking path via com.unity.xr.hands, so VIVE/PICO expose
        // the same 26 joints through the same subsystem.
        EnableFeature<UnityEngine.XR.Hands.OpenXR.HandTracking>(openxr, "Hand Tracking Subsystem");

        EnableFeature<OculusTouchControllerProfile>(openxr, "Oculus Touch");
        EnableFeature<MetaQuestTouchPlusControllerProfile>(openxr, "Meta Quest Touch Plus (Quest 3/3S)");
        EnableFeature<HTCViveControllerProfile>(openxr, "HTC Vive");
        EnableFeature<ValveIndexControllerProfile>(openxr, "Valve Index");
        EnableFeature<KHRSimpleControllerProfile>(openxr, "KHR Simple (fallback)");

        EditorUtility.SetDirty(openxr);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"[XRPluginSetup] Done. renderMode={openxr.renderMode}, " +
                  $"initOnStart={settings.InitManagerOnStart}");
    }

    static void EnableFeature<T>(OpenXRSettings settings, string label)
        where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
    {
        T feature = settings.GetFeature<T>();
        if (feature == null)
        {
            Debug.LogWarning($"[XRPluginSetup] Interaction profile not found: {label}");
            return;
        }

        feature.enabled = true;
        EditorUtility.SetDirty(feature);
        Debug.Log($"[XRPluginSetup]   enabled profile: {label}");
    }
}
