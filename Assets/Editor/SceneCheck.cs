using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);

        foreach (string n in new[] { "Chair_0", "Asset_ConcreteTable", "TBottle_0" })
        {
            GameObject go = GameObject.Find(n);
            if (go == null) { Debug.Log($"SCENECHECK {n}: NOT FOUND"); continue; }

            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }

            var col = go.GetComponent<Collider>();
            Debug.Log($"SCENECHECK {n}: worldY={b.min.y:F3}..{b.max.y:F3} " +
                      $"size={b.size:F3} collider={(col == null ? "none" : col.GetType().Name)} " +
                      $"colBounds={(col == null ? "-" : col.bounds.size.ToString("F3"))} " +
                      $"rb={(go.GetComponent<Rigidbody>() != null)}");
        }
    }
}
