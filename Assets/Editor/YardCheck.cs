using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class YardCheck
{
    [MenuItem("Tools/Check Yard Relief")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");

        var yard = GameObject.Find("YardGround");
        var mf = yard != null ? yard.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) { Debug.Log("[YardCheck] no yard mesh"); return; }
        float lo = 9e9f, hi = -9e9f;
        foreach (var v in mf.sharedMesh.vertices) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
        Debug.Log($"[YardCheck] surface: {mf.sharedMesh.vertexCount} verts, " +
                  $"y {lo:F3}..{hi:F3}  (relief {hi - lo:F3} m)");

        // The comfort claim is that the WALKING ROUTE is flat. Sample it.
        void Profile(string label, Vector3 a, Vector3 b, int n)
        {
            float lo = 9e9f, hi = -9e9f;
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit h, 20f))
                { lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y); }
            }
            Debug.Log($"[YardCheck] {label}: y {lo:F3}..{hi:F3}  rise {hi - lo:F3} m");
        }
        Profile("gate-to-door centreline", new Vector3(0, 0, -18f), new Vector3(0, 0, -10.5f), 40);
        Profile("across the door apron", new Vector3(-2.2f, 0, -12f), new Vector3(2.2f, 0, -12f), 30);
        Profile("outer yard, left wall", new Vector3(-10f, 0, -6f), new Vector3(-10f, 0, 10f), 40);
        Profile("outer yard, back", new Vector3(-8f, 0, 11f), new Vector3(8f, 0, 11f), 40);

        // Does each plant sit on the ground UNDER IT? Raycast, do not assume.
        string[] want = { "Asset_GrassTuft", "Asset_GrassWeed", "Asset_GroundBush", "Tree", "Palm_1", "Palm_2" };
        foreach (string n in want)
        {
            int checkedN = 0; float worst = 0f; string worstAt = "";
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != n) continue;
                var mr = t.GetComponentInChildren<MeshRenderer>();
                if (mr == null) continue;
                Vector3 p = t.position;
                if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f,
                                     ~0, QueryTriggerInteraction.Ignore)) continue;
                float gap = mr.bounds.min.y - hit.point.y;   // <0 = sunk in, >0 = floating
                checkedN++;
                if (Mathf.Abs(gap) > Mathf.Abs(worst)) { worst = gap; worstAt = p.ToString("F1"); }
            }
            if (checkedN > 0)
                Debug.Log($"[YardCheck] {n}: {checkedN} checked, worst gap {worst:+0.000;-0.000} m at {worstAt}");
        }
    }
}
