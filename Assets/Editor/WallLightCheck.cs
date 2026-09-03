using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class WallLightCheck
{
    [MenuItem("Tools/Check Wall Lights")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != "Asset_WallLight") continue;
            var mr = t.GetComponentInChildren<MeshRenderer>(true);
            string b = mr == null ? "NO RENDERER"
                : $"bounds centre={mr.bounds.center.ToString("F2")} size={mr.bounds.size.ToString("F2")} " +
                  $"enabled={mr.enabled} mats={mr.sharedMaterials.Length} " +
                  $"[{(mr.sharedMaterials[0] ? mr.sharedMaterials[0].name : "null")}" +
                  (mr.sharedMaterials.Length > 1 ? $", {(mr.sharedMaterials[1] ? mr.sharedMaterials[1].name : "null")}" : "") + "]";
            Debug.Log($"[WallLightCheck] parent={t.parent.name} holderPos={t.position.ToString("F2")} " +
                      $"yaw={t.eulerAngles.y:F0}\n   {b}");
        }
    }
}
