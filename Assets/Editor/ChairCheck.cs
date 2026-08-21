using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChairCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);
        GameObject chair = GameObject.Find("Chair_1");
        if (chair == null) { Debug.Log("CHAIRCHECK not found"); return; }

        // Mesh bounds in the chair's own space: is the back at -Z or +Z?
        var mf = chair.GetComponentInChildren<MeshFilter>();
        Bounds local = mf.sharedMesh.bounds;
        Vector3 worldBackDir = chair.transform.InverseTransformPoint(
            mf.transform.TransformPoint(new Vector3(local.center.x, local.max.y, local.center.z)));

        foreach (var c in chair.GetComponents<BoxCollider>())
            Debug.Log($"CHAIRCHECK collider centre={c.center:F3} size={c.size:F3}");

        Debug.Log($"CHAIRCHECK topOfMesh(chairSpace)={worldBackDir:F3}  " +
                  $"meshLocalBoundsCentre={local.center:F3} size={local.size:F3}");
    }
}
