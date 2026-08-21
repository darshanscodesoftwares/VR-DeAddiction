using UnityEditor;
using UnityEngine;

public static class RigDump
{
    public static void Run()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Samples/XRHands/Models/RightHand.fbx");
        if (src == null) { Debug.Log("RIGDUMP not found"); return; }

        foreach (Transform t in src.GetComponentsInChildren<Transform>(true))
        {
            int depth = 0;
            Transform p = t.parent;
            while (p != null) { depth++; p = p.parent; }
            Debug.Log($"RIGDUMP {new string(' ', depth * 2)}{t.name}");
        }
    }
}
