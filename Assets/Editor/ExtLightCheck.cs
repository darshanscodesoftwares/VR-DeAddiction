using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ExtLightCheck
{
    [MenuItem("Tools/Check Exterior Lights")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");
        var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!l.name.StartsWith("ExtLight")) continue;
            Vector3 p = l.transform.position;
            string enclosing = "", infront = "";
            foreach (MeshRenderer mr in all)
            {
                Bounds b = mr.bounds;
                // (a) geometry whose volume swallows the lamp itself
                if (b.Contains(p))
                    enclosing += (enclosing.Length > 0 ? ", " : "") + $"{mr.name}[{mr.transform.parent?.name}]";
                // (b) geometry sitting in the first metre of the beam
                for (float d = 0.15f; d <= 1.0f; d += 0.15f)
                    if (b.Contains(p + l.transform.forward * d))
                    { infront += (infront.Length > 0 ? ", " : "") + $"{mr.name}@{d:F2}m"; break; }
            }
            Debug.Log($"[ExtLightCheck] {l.name}\n   ENCLOSING: {(enclosing.Length > 0 ? enclosing : "none")}" +
                      $"\n   IN BEAM  : {(infront.Length > 0 ? infront : "none")}");
        }
    }
}
