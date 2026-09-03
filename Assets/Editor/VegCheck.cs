using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class VegCheck
{
    [MenuItem("Tools/Check Vegetation")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");
        var seen = new HashSet<string>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name;
            if (n != "Tree" && !n.StartsWith("Palm_") && n != "Asset_Shrub" &&
                n != "Asset_GrassTuft" && n != "Asset_GrassWeed") continue;
            if (!seen.Add(n)) continue;

            Bounds b = new Bounds(t.position, Vector3.zero);
            bool any = false;
            foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(true))
            { if (!any) { b = mr.bounds; any = true; } else b.Encapsulate(mr.bounds); }

            Debug.Log($"[VegCheck] {n}\n   placedAt y={t.position.y:F3}   " +
                      $"renderer bounds min.y={b.min.y:F3}  max.y={b.max.y:F3}  size={b.size.ToString("F2")}\n" +
                      $"   GAP UNDER = {b.min.y - (-0.01f):F3} m  (0 = sitting on the yard surface)");
        }

        var yard = GameObject.Find("YardGround");
        if (yard != null)
        {
            var mr = yard.GetComponent<MeshRenderer>();
            Debug.Log($"[VegCheck] YardGround top y={mr.bounds.max.y:F3} " +
                      $"bounds={mr.bounds.center.ToString("F2")} size={mr.bounds.size.ToString("F2")}");
        }
    }
}
