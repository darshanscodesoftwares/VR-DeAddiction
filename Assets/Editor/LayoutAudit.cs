// LayoutAudit.cs
//
// Reads the generated scene back and reports anything placed where it cannot
// physically be. Written because almost every visual fault in this project has
// been a placement fault -- chairs inside tables, bottles hanging off the edge,
// a marker inside a table leg -- and every one of them was invisible in the
// builder source and obvious the moment something measured it.
//
//   Unity -batchmode -quit -nographics -projectPath . \
//     -executeMethod LayoutAudit.Run

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LayoutAudit
{
    const float TableRadius = 0.367f;
    const float ChairHalfDepth = 0.314f;
    const float PropUsableRadius = 0.33f;   // rim minus a prop's own width

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");

        var tables = new List<Vector3>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "Asset_RoundTable")
                tables.Add(new Vector3(t.position.x, 0f, t.position.z));

        int chairsChecked = 0, chairsClashing = 0;
        int propsChecked = 0, propsOffTable = 0;
        float worstProp = 0f, worstChair = 999f;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            Vector3 flat = new Vector3(t.position.x, 0f, t.position.z);

            if (t.name == "Asset_PlasticChair")
            {
                // The seated scenario's chair is TUCKED IN on purpose -- that is
                // what a chair someone is sitting in looks like. It clears the
                // table because its seat sits at 0.38-0.53 m, well under the
                // 0.775 m top, and its back is on the far side. Only chairs
                // standing AT a table have to keep their distance.
                if (t.parent != null && t.parent.name.Contains("SeatedScenario"))
                    continue;

                chairsChecked++;
                float d = Nearest(tables, flat);
                if (d < TableRadius + ChairHalfDepth)
                {
                    chairsClashing++;
                    Debug.Log($"AUDIT   close chair '{t.parent?.name}/{t.name}' at " +
                              $"{d:F3} m from the nearest table centre");
                }
                if (d < worstChair) worstChair = d;
            }

            // Table clutter: bottles and glasses that should be ON a top.
            if ((t.name.StartsWith("TBottle_") || t.name.StartsWith("Glass_"))
                && t.position.y > 0.5f)
            {
                propsChecked++;
                float d = Nearest(tables, flat);
                if (d > PropUsableRadius) { propsOffTable++; }
                if (d > worstProp) worstProp = d;
            }
        }

        // Table-to-table spacing. This is the check that was missing: chairs
        // and props were measured against their own table, but nothing ever
        // asked whether two TABLES were on top of each other -- which is how
        // the hero table ended up wedged between two grid tables.
        float closestPair = 9999f;
        int crowded = 0;
        for (int i = 0; i < tables.Count; i++)
        {
            for (int j = i + 1; j < tables.Count; j++)
            {
                float d = Vector3.Distance(tables[i], tables[j]);
                if (d < closestPair) closestPair = d;
                if (d < 2.3f)
                {
                    crowded++;
                    Debug.Log($"AUDIT   tables {tables[i]:F2} and {tables[j]:F2} " +
                              $"only {d:F2} m apart");
                }
            }
        }

        Debug.Log($"AUDIT tables={tables.Count} crowdedPairs={crowded} " +
                  $"closestPair={closestPair:F2} (must be >= 2.30)");
        Debug.Log($"AUDIT chairs={chairsChecked} clashing={chairsClashing} " +
                  $"closest={worstChair:F3} (must be >= {TableRadius + ChairHalfDepth:F3})");
        Debug.Log($"AUDIT tableProps={propsChecked} offTable={propsOffTable} " +
                  $"furthest={worstProp:F3} (must be <= {PropUsableRadius:F3})");

        GameObject marker = GameObject.Find("Marker");
        GameObject seat = GameObject.Find("SeatAnchor");
        if (marker != null && seat != null)
        {
            Vector3 d = marker.transform.position - seat.transform.position;
            d.y = 0f;
            float off = Vector3.Angle(-seat.transform.forward, d.normalized);
            Debug.Log($"AUDIT marker {d.magnitude:F2} m from seat, {off:F0} deg " +
                      $"off the approach line (0 = straight behind the chair)");
        }
    }

    static float Nearest(List<Vector3> pts, Vector3 p)
    {
        float best = 9999f;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = Vector3.Distance(pts[i], p);
            if (d < best) best = d;
        }
        return best;
    }
    /// <summary>
    /// Large floor-standing geometry with a gap underneath.
    ///
    /// Three separate props have shipped floating this project: the pendant
    /// lamp brackets, the sign wires, and the counter's return wing, which hung
    /// 0.15 m up because it copied the main counter's recess without copying
    /// its kick plate. Each was found by someone noticing it in the headset.
    /// This finds them at build time instead.
    ///
    /// Only objects whose base is 2 cm to 40 cm up are considered: below that
    /// is contact, above it is a wall or ceiling fitting that is meant to hang.
    /// Anything with geometry directly beneath it is resting on something and
    /// is fine.
    /// </summary>
    [MenuItem("Tools/Audit Floating Geometry")]
    public static void AuditFloating()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity");
        var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int flagged = 0;

        foreach (MeshRenderer r in all)
        {
            Bounds b = r.bounds;
            float bottom = b.min.y;
            if (bottom < 0.02f || bottom > 0.40f)
                continue;
            // Small things are litter and clutter; they may lie at any height.
            if (b.size.x < 0.5f && b.size.z < 0.5f)
                continue;

            // Probe a thin slab directly beneath and ask whether ANYTHING
            // intersects it. Matching on a supporter's TOP surface instead was
            // too strict and cried wolf: a stacked chair nests into the one
            // below so their tops do not align, and a shelf deck is carried by
            // full-height side panels whose tops are 2 m up. Both were reported
            // as floating when they are plainly resting on something.
            Bounds probe = new Bounds(
                new Vector3(b.center.x, bottom - 0.03f, b.center.z),
                new Vector3(Mathf.Max(b.size.x - 0.10f, 0.02f), 0.05f,
                            Mathf.Max(b.size.z - 0.10f, 0.02f)));

            bool supported = false;
            foreach (MeshRenderer o in all)
            {
                if (o == r || o.transform.IsChildOf(r.transform) ||
                    r.transform.IsChildOf(o.transform)) continue;
                if (o.bounds.Intersects(probe)) { supported = true; break; }
            }

            if (!supported)
            {
                flagged++;
                Debug.LogWarning($"[LayoutAudit] FLOATING: {r.name} base at " +
                                 $"y={bottom:F3} m, size {b.size}, nothing beneath it.");
            }
        }

        Debug.Log($"[LayoutAudit] Floating check: {flagged} floating of " +
                  $"{all.Length} renderers.");
    }

}
