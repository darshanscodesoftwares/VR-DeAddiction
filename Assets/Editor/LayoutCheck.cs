using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LayoutCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("TableUnit_"))
                Debug.Log($"LAYOUT table {t.name} at {t.position:F2}");
        }

        var marker = GameObject.Find("Marker");
        if (marker != null) Debug.Log($"LAYOUT MARKER at {marker.transform.position:F2}");
        var seat = GameObject.Find("SeatAnchor");
        if (seat != null) Debug.Log($"LAYOUT SEAT at {seat.transform.position:F2}");

        // Chair facing: does the chair's forward point at its table?
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Chair_") || t.parent == null) continue;
            if (n++ >= 4) break;
            Vector3 toTable = (t.parent.position - t.position).normalized;
            float dot = Vector3.Dot(t.forward, toTable);
            Debug.Log($"LAYOUT chair {t.parent.name}/{t.name} yaw={t.eulerAngles.y:F0} facingTable={dot:F2} (1=faces table, -1=away)");
        }
    }
}
