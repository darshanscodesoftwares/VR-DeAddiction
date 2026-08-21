using UnityEditor;
using UnityEngine;

public static class ModelCheck
{
    public static void Run()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            var mf = go.GetComponentInChildren<MeshFilter>();
            Mesh m = mf != null ? mf.sharedMesh : null;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;

            Debug.Log($"MODELCHECK {System.IO.Path.GetFileName(path)} " +
                      $"rootRot={go.transform.localEulerAngles} " +
                      $"childRot={(mf != null ? mf.transform.localEulerAngles.ToString() : "-")} " +
                      $"bounds={(m != null ? m.bounds.size.ToString("F3") : "-")} " +
                      $"center={(m != null ? m.bounds.center.ToString("F3") : "-")} " +
                      $"scaleFactor={(imp != null ? imp.globalScale : -1f)} " +
                      $"useFileScale={(imp != null ? imp.useFileScale : false)}");
        }
    }
}
