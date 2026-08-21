using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PropCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);
        var root = GameObject.Find("TableProps");
        if (root == null) { Debug.Log("PROPCHECK no TableProps"); return; }

        foreach (Transform t in root.transform)
        {
            var mf = t.GetComponentInChildren<MeshFilter>();
            string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-";
            Debug.Log($"PROPCHECK {t.name} mesh={mesh} pos={t.position:F2}");
        }

        // Anything else sitting on the hero tabletop?
        Collider[] hits = Physics.OverlapBox(new Vector3(-2.6f, 0.90f, 1.4f),
                                             new Vector3(0.6f, 0.22f, 0.45f));
        foreach (var c in hits)
            Debug.Log($"PROPCHECK onTable: {c.transform.root.name}/{c.name} at {c.transform.position:F2}");
    }
}
