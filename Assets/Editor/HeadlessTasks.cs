// HeadlessTasks.cs
//
// Batch-mode entry points, so the environment can be regenerated and verified
// without opening the editor (useful now, and for CI later).
//
//   Unity -batchmode -quit -projectPath . -executeMethod HeadlessTasks.RebuildScene
//
// Build() marks the scene dirty but deliberately does not save it -- an
// interactive user may want to undo. Headless runs have no such choice, so this
// wrapper opens, builds, and saves explicitly.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HeadlessTasks
{
    const string ScenePath = "Assets/unity-VR.unity";

    public static void RebuildScene()
    {
        // Force every model to reimport before the scene is rebuilt.
        //
        // Blender rewrites these FBXs between runs and Unity does NOT reliably
        // notice: a regenerated model can be picked up as its PREVIOUS import,
        // so a real change looks like it did nothing. That has cost time twice
        // -- once making a corrected hand export look unchanged, and once
        // shipping a build with the old grass in it while the files on disk
        // were already right.
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Models" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),
                                      ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[Headless] Could not open " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        RemoveStrayRigs(scene);
        PubEnvironmentBuilder.Build();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("[Headless] Failed to save " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[Headless] Rebuilt and saved " + ScenePath);
    }

    /// <summary>
    /// Deletes XR rigs sitting at the scene root. Tools > VR Setup > Add XR Rig
    /// To Scene drops one there, and because it lives OUTSIDE PubEnvironment_IN,
    /// neither Build nor Clear removes it. Two rigs means two cameras tagged
    /// MainCamera and the headset picks one arbitrarily.
    /// </summary>
    static void RemoveStrayRigs(Scene scene)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Only root-level objects; the real rig is nested under 09_Player.
            if (root.name == "XR Origin" || root.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null)
            {
                Debug.Log("[Headless] Removing stray root-level rig: " + root.name);
                Object.DestroyImmediate(root);
                removed++;
            }
        }

        if (removed > 0)
            Debug.Log($"[Headless] Removed {removed} stray rig(s).");
    }

    /// <summary>
    /// Produces a sideloadable APK at Builds/VRDeAddiction.apk.
    ///   Unity -batchmode -quit -projectPath . -executeMethod HeadlessTasks.BuildAPK
    /// Headless runs only work when the editor is closed -- an open editor holds
    /// Temp/UnityLockfile and the batch process exits immediately.
    /// </summary>
    [MenuItem("Tools/VR Setup/Build APK")]
    public static void BuildAPK()
    {
        // The scene list was empty, which builds an APK that launches to a black
        // screen. Register the scene explicitly rather than relying on whatever
        // happens to be open.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        const string outDir = "Builds";
        if (!System.IO.Directory.Exists(outDir))
            System.IO.Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir + "/VRDeAddiction.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            // summary.totalSize is the UNCOMPRESSED build size, not the packaged
            // APK -- it reads ~15x larger and is misleading. Report the real file.
            long apkBytes = new System.IO.FileInfo(summary.outputPath).Length;
            Debug.Log($"[Headless] APK built: {summary.outputPath} " +
                      $"({apkBytes / (1024 * 1024)} MB on disk, {summary.totalTime})");
        }
        else
        {
            Debug.LogError($"[Headless] Build {summary.result}: {summary.totalErrors} error(s).");
            EditorApplication.Exit(1);
        }
    }
}
