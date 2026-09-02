// PubScenarioBuilder.cs
//
// Builds group 11_SeatedScenario: the marked "hero" table, its floor marker,
// the seat anchor, the detailed props, and the containment volume.
//
// Split out of PubEnvironmentBuilder because it is scenario content rather
// than environment, and the two will diverge -- there will be more scenarios,
// and they should not all pile into the environment file. Same conventions
// though: procedural, code is the source of truth, destroyed and recreated on
// every Build().
//
// Called from PubEnvironmentBuilder.Build().

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class PubScenarioBuilder
{
    // Where the hero table sits. Chosen near the middle of the hall so the bar
    // is visible all around when seated -- the surrounding room is the cue, so
    // a table tucked against a wall would weaken it.
    public const float TableX = -2.6f;
    public const float TableZ = 1.4f;

    const float TableTop = 0.775f;

    public static GameObject Build(Transform parent, System.Func<string, Material> mat,
                                   System.Func<string, Transform, Vector3, float, Material, bool, GameObject> model,
                                   System.Action<Transform, float, float, float> grabbableUpright,
                                   System.Func<string, Transform, Vector3, float, Material, bool, Material[], GameObject> modelSlots)
    {
        Transform g = new GameObject("11_SeatedScenario").transform;
        g.SetParent(parent, false);

        Vector3 tableCentre = new Vector3(TableX, 0f, TableZ);

        // ---- The hero table --------------------------------------------------
        // Yaw 0, not 4 deg: the seat anchor faces +Z, so any table rotation
        // leaves you sitting skewed to the table edge.
        GameObject table = model("Asset_RoundTable", g, tableCentre, 0f,
                                 mat("Mat_Pub_TableWood"), true);
        if (table != null)
        {
            // Static mesh collider, so the disc collides as a disc. A box would
            // either overhang the rim or stop short of it -- see
            // PubEnvironmentBuilder.AddTableCollider.
            foreach (MeshFilter mf in table.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }
        }

        // ---- Seat and marker ---------------------------------------------------
        // The patient sits on the long side, facing across the table into the
        // room, so the bar stays in view while seated.
        // 0.60 m from table centre. The top's half-depth is 0.38, so this
        // leaves ~0.22 m between the patient and the table edge -- close
        // enough to rest forearms on it and reach the far side.
        float seatOffsetZ = -0.60f;
        Vector3 seatPos = tableCentre + new Vector3(0f, 0f, seatOffsetZ);

        GameObject seatAnchor = new GameObject("SeatAnchor");
        seatAnchor.transform.SetParent(g, false);
        seatAnchor.transform.localPosition = seatPos;
        seatAnchor.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        // A real chair at the seat, so sitting has something to sit on.
        //
        // Built STATIC and non-grabbable, unlike the other 36 chairs. The
        // patient's capsule is placed exactly here: a 4.5 kg dynamic rigidbody
        // in that space gets depenetrated when the capsule arrives, which can
        // launch it, and a grabbable one lets the patient pick up and throw the
        // chair they are sitting in.
        // Yaw 0, NOT 180. A chair faces +Z at yaw 0 and the table is at +Z from
        // the seat, so 180 turned the chair around and put its back between the
        // patient and the tabletop -- you ended up looking over the backrest.
        GameObject seatChair = model("Asset_PlasticChair", g,
                                     seatPos + new Vector3(0f, 0f, -0.06f), 0f,
                                     mat("Mat_Pub_ChairRed"), true);
        if (seatChair != null)
        {
            foreach (var rb in seatChair.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(rb);
            foreach (var gi in seatChair.GetComponentsInChildren<XRGrabInteractable>(true))
                Object.DestroyImmediate(gi);
            foreach (var col in seatChair.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);
        }

        // Marker sits on open floor BEHIND the chair, not tucked between chair
        // and table. At the old -0.35 m offset its 0.62 m ring overlapped the
        // chair (whose back edge is ~1.05 m from table centre) and the pulse
        // clipped through the geometry. 1.65 m from table centre clears both
        // the chair and the table, so the player walks up to a clean ring and
        // then sits forward into the seat.
        // Marker position is SEARCHED, not hand-placed. Twice now a guessed
        // offset landed inside other geometry -- first the chair, then
        // TableUnit_06 at (-2.81, -0.15), which put a glowing shaft through a
        // neighbouring tabletop. The hall is a grid of tables and chairs, so
        // eyeballing a free patch of floor does not work.
        Vector3 markerPos = FindClearFloor(seatPos, tableCentre);
        GameObject marker = BuildMarker(g, markerPos);

        // ---- Focus light ------------------------------------------------------
        // Sits low over the table. Starts at zero intensity and as a vertex
        // light; LightingDirector promotes it to pixel only while seated, and
        // demotes a distant pendant to pay for it, so the pixel count is fixed.
        GameObject focusGo = new GameObject("Lamp_TableFocus");
        focusGo.transform.SetParent(g, false);
        focusGo.transform.localPosition = tableCentre + new Vector3(0f, 1.62f, 0f);
        Light focus = focusGo.AddComponent<Light>();
        focus.type = LightType.Point;
        focus.color = new Color(1f, 0.760f, 0.480f);
        focus.intensity = 1f;             // scaled by the director
        focus.range = 3.4f;
        focus.shadows = LightShadows.None;
        focus.renderMode = LightRenderMode.ForceVertex;

        // ---- Props ---------------------------------------------------------------
        var props = new List<XRGrabInteractable>();
        Transform propRoot = new GameObject("TableProps").transform;
        propRoot.SetParent(g, false);

        // Laid out as a real table is: drinks toward the drinker, shared water
        // and food in the middle, ashtray pushed to the edge.
        // EVERY PROP MUST NOW FIT A CIRCLE, not a 1.10 x 0.76 rectangle. The
        // round table's radius is 0.367, so with the widest prop at 0.052 the
        // usable centre-distance is about 0.30 m. Positions below are chosen
        // against that limit and against each other, not by eye: drinks toward
        // the far side, glasses within easy reach on the near side where the
        // patient sits at -Z.
        AddProp(props, propRoot, model, mat, grabbableUpright,
                "Asset_BeerBottle", tableCentre + new Vector3(-0.18f, TableTop, 0.14f),
                "Mat_Pub_GlassGreen", 0.042f, 0.302f, 0.6f, 18f);

        AddProp(props, propRoot, model, mat, grabbableUpright,
                "Asset_BeerBottle", tableCentre + new Vector3(-0.02f, TableTop, 0.25f),
                "Mat_Pub_GlassGreen", 0.042f, 0.302f, 0.6f, -35f);

        // The branded bottle. Real detail only where the patient actually looks:
        // seated at this table, arm's length away. The source model is 1.5
        // MILLION triangles -- 27x the entire bar -- so it is decimated to
        // ~12,000 by BlenderAssets/scripts/jackdaniels.py. Almost all of that
        // density sat on smooth surfaces of revolution, which decimate without
        // any visible change; the label, which is the whole point, keeps ten
        // times the proportion of everything else.
        //
        // The distant shelf bottles stay procedural. At across-the-room
        // distance nothing distinguishes them, and 73 of these would be
        // 876,000 triangles.
        //
        // Slot order is fixed by the Blender script: glass, whiskey, label, cap.
        AddPropSlots(props, propRoot, modelSlots, grabbableUpright,
                     "Asset_JackDaniels", tableCentre + new Vector3(0.15f, TableTop, 0.16f),
                     new[] { mat("Mat_JD_Glass"), mat("Mat_JD_Whiskey"),
                             mat("Mat_JD_Label"), mat("Mat_JD_Cap") },
                     0.052f, 0.290f, 1.0f, 62f);

        AddProp(props, propRoot, model, mat, grabbableUpright,
                "Asset_WaterBottle", tableCentre + new Vector3(0.24f, TableTop, -0.02f),
                "Mat_Pub_GlassClear", 0.038f, 0.276f, 0.4f, 12f);

        // Two tumblers within easy seated reach.
        AddProp(props, propRoot, model, mat, grabbableUpright,
                "Asset_SteelTumbler", tableCentre + new Vector3(-0.16f, TableTop, -0.13f),
                "Mat_Pub_Metal", 0.038f, 0.090f, 0.25f, 0f);

        // Detailed glass, within seated reach. Unlike the bottle this source
        // needed no decimation -- 1,490 triangles as downloaded, with the
        // facets baked into a normal map instead of carved into geometry.
        // Slot order is fixed by the Blender script: glass, whiskey.
        AddPropSlots(props, propRoot, modelSlots, grabbableUpright,
                     "Asset_WhiskeyGlass", tableCentre + new Vector3(0.06f, TableTop, -0.16f),
                     new[] { mat("Mat_WG_Glass"), mat("Mat_WG_Whiskey") },
                     0.042f, 0.100f, 0.30f, 0f);

        // A second, plain glass -- an empty tumbler beside the poured one.
        //
        // Placed at (0.34, -0.14): clear of the Jack Daniel's at (0.28, 0.16)
        // by 0.31 m and the whiskey glass at (0.12, -0.22) by 0.23 m, against a
        // combined collider radius of ~0.10 m, and comfortably inside the
        // containment lip's 0.515 x 0.345 half-extents. Placement here is
        // computed against real extents rather than eyeballed -- eyeballing is
        // what put chairs inside tables earlier in this project.
        AddProp(props, propRoot, model, mat, grabbableUpright,
                "Asset_TableGlass", tableCentre + new Vector3(0.20f, TableTop, -0.20f),
                "Mat_Pub_GlassClear", 0.049f, 0.114f, 0.25f, 0f);

        // Food plate removed at the user's request.

        // Ashtray removed: as a shallow open dish it read as a small plate,
        // not an ashtray. If it comes back it needs a deeper well and cigarette
        // notches in the rim, or it will be mistaken for crockery again.

        // ---- Containment ---------------------------------------------------------
        GameObject volume = new GameObject("TableVolume");
        volume.transform.SetParent(g, false);
        volume.transform.localPosition = tableCentre + new Vector3(0f, TableTop, 0f);

        var containment = volume.AddComponent<TableContainment>();
        containment.Bounds = new Vector3(0.46f, 0.90f, 0.46f);

        // Low invisible lip around the rim: stops things rolling off on their
        // own, without the hard glass-box feel of a full barrier.
        //
        // A RING now, not a rectangle. Four straight walls around a disc leave
        // the corners hanging over open air and cut across the rim on the flats
        // -- objects would stop dead well inside the visible edge on one axis
        // and roll off it on another. Twelve short segments sit just inside the
        // 0.367 rim all the way round.
        BuildLipRing(volume.transform, 0.335f);

        foreach (XRGrabInteractable gi in props)
        {
            Rigidbody rb = gi.GetComponent<Rigidbody>();
            if (rb != null)
                containment.Register(rb);
        }

        Debug.Log($"[Scenario] Hero table at ({TableX}, {TableZ}) with {props.Count} props.");

        return g.gameObject;
    }

    /// <summary>
    /// Adds a prop whose mesh has several material slots, so each part of the
    /// model gets its own material rather than the whole thing being painted
    /// one colour.
    /// </summary>
    static void AddPropSlots(List<XRGrabInteractable> into, Transform parent,
                             System.Func<string, Transform, Vector3, float, Material, bool, Material[], GameObject> modelSlots,
                             System.Action<Transform, float, float, float> grabbableUpright,
                             string asset, Vector3 pos, Material[] slots,
                             float radius, float height, float mass, float yaw)
    {
        // Slot 0 doubles as the fallback for any slot the mesh has but the
        // caller did not supply, so nothing can render magenta.
        GameObject go = modelSlots(asset, parent, pos, yaw, slots[0], false, slots);
        if (go == null)
            return;

        grabbableUpright(go.transform, radius, height, mass);

        var gi = go.GetComponent<XRGrabInteractable>();
        if (gi != null)
            into.Add(gi);
    }

    static void AddProp(List<XRGrabInteractable> into, Transform parent,
                        System.Func<string, Transform, Vector3, float, Material, bool, GameObject> model,
                        System.Func<string, Material> mat,
                        System.Action<Transform, float, float, float> grabbableUpright,
                        string asset, Vector3 pos, string material,
                        float radius, float height, float mass, float yaw)
    {
        GameObject go = model(asset, parent, pos, yaw, mat(material), false);
        if (go == null)
            return;

        grabbableUpright(go.transform, radius, height, mass);

        var gi = go.GetComponent<XRGrabInteractable>();
        if (gi != null)
            into.Add(gi);
    }

    /// <summary>
    /// Renderer-less collider lip around the table rim. Invisible, but a bottle
    /// nudged sideways stops instead of rolling off.
    /// </summary>
    /// <summary>
    /// Invisible rim for a ROUND table: short collider segments arranged on a
    /// circle, each turned to face the centre. Twelve is enough that a rolling
    /// bottle meets a wall rather than a corner between two of them.
    /// </summary>
    static void BuildLipRing(Transform parent, float radius)
    {
        const int Segments = 12;
        const float lipH = 0.075f;
        const float t = 0.03f;

        // Chord length of one segment, plus a little overlap so there is no gap
        // where two segments meet.
        float seg = 2f * radius * Mathf.Sin(Mathf.PI / Segments) * 1.15f;

        for (int i = 0; i < Segments; i++)
        {
            float a = (360f / Segments) * i;
            float rad = a * Mathf.Deg2Rad;

            GameObject go = new GameObject("Lip_" + i);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(Mathf.Sin(rad) * radius,
                                                     lipH * 0.5f,
                                                     Mathf.Cos(rad) * radius);
            go.transform.localEulerAngles = new Vector3(0f, a, 0f);

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(seg, lipH, t);
        }
    }

    static void BuildLip(Transform parent, float halfX, float halfZ)
    {
        const float lipH = 0.075f;
        const float t = 0.03f;

        (Vector3 pos, Vector3 size)[] sides =
        {
            (new Vector3(0f, lipH * 0.5f, halfZ), new Vector3(halfX * 2f, lipH, t)),
            (new Vector3(0f, lipH * 0.5f, -halfZ), new Vector3(halfX * 2f, lipH, t)),
            (new Vector3(halfX, lipH * 0.5f, 0f), new Vector3(t, lipH, halfZ * 2f)),
            (new Vector3(-halfX, lipH * 0.5f, 0f), new Vector3(t, lipH, halfZ * 2f)),
        };

        for (int i = 0; i < sides.Length; i++)
        {
            GameObject go = new GameObject("Lip_" + i);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = sides[i].pos;

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = sides[i].size;
        }
    }

    /// <summary>
    /// Finds open floor near the seat for the marker: somewhere the patient can
    /// physically stand, clear of every table, chair and wall.
    ///
    /// Candidates radiate outward from the seat, away from the table first,
    /// and the first one with nothing in a 0.45 m sphere wins.
    /// </summary>
    static Vector3 FindClearFloor(Vector3 seatPos, Vector3 tableCentre)
    {
        // Colliders were created moments ago by the builder; queries read stale
        // transforms without this.
        Physics.SyncTransforms();

        Vector3 awayFromTable = seatPos - tableCentre;
        awayFromTable.y = 0f;
        if (awayFromTable.sqrMagnitude < 0.0001f)
            awayFromTable = Vector3.back;
        awayFromTable.Normalize();

        const float probeRadius = 0.45f;
        float[] distances = { 1.35f, 1.65f, 1.95f, 2.25f };
        float[] sweep = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };

        foreach (float dist in distances)
        {
            foreach (float deg in sweep)
            {
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * awayFromTable;
                Vector3 candidate = seatPos + dir * dist;
                candidate.y = 0.012f;

                // Probe at torso height: a floor-level sphere would miss table
                // tops, which is exactly what it must not stand under.
                if (Physics.CheckSphere(candidate + Vector3.up * 0.9f, probeRadius,
                                        ~0, QueryTriggerInteraction.Ignore))
                    continue;

                Debug.Log($"[Scenario] Marker placed at {candidate:F2} " +
                          $"({dist:F2} m from seat, {deg:F0} deg off-axis).");
                return candidate;
            }
        }

        Debug.LogWarning("[Scenario] No clear floor found for the marker; " +
                         "falling back behind the seat.");
        return seatPos + awayFromTable * 1.65f + Vector3.up * 0.012f;
    }

    /// <summary>
    /// A small arrow pointing down at the spot to stand on. The old glowing
    /// disc and 1.5 m light column read as a game HUD dropped into the room and
    /// dominated everything around it.
    /// </summary>
    static GameObject BuildMarker(Transform parent, Vector3 pos)
    {
        GameObject marker = new GameObject("Marker");
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = pos;

        Material glow = MarkerMaterial();

        // Shaft.
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        Object.DestroyImmediate(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(marker.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        shaft.transform.localScale = new Vector3(0.035f, 0.20f, 0.035f);
        shaft.GetComponent<MeshRenderer>().sharedMaterial = glow;

        // Head: a four-sided pyramid pointing straight down.
        GameObject head = new GameObject("Head");
        head.transform.SetParent(marker.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.30f, 0f);

        MeshFilter mf = head.AddComponent<MeshFilter>();
        MeshRenderer mr = head.AddComponent<MeshRenderer>();
        mr.sharedMaterial = glow;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        const float w = 0.085f;
        const float h = 0.13f;
        Mesh mesh = new Mesh { name = "ArrowHead" };
        mesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0f),        // tip, pointing down
            new Vector3(-w, h, -w),
            new Vector3(w, h, -w),
            new Vector3(w, h, w),
            new Vector3(-w, h, w),
        };
        mesh.triangles = new[]
        {
            0, 2, 1,  0, 3, 2,  0, 4, 3,  0, 1, 4,   // sides
            1, 2, 3,  1, 3, 4,                        // cap
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        return marker;
    }

    const string MarkerMatPath = "Assets/PubEnvironment/Materials/Mat_Scenario_Marker.mat";

    static Material MarkerMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MarkerMatPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, MarkerMatPath);
        }

        m.shader = Shader.Find("Standard");
        m.SetColor("_Color", new Color(0.95f, 0.72f, 0.30f));
        m.SetFloat("_Glossiness", 0.4f);
        m.EnableKeyword("_EMISSION");
        // Warm, not the usual sci-fi cyan: it has to sit inside a warm bar
        // without looking like a game HUD dropped into the room.
        m.SetColor("_EmissionColor", new Color(0.85f, 0.55f, 0.18f) * 1.1f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(m);
        return m;
    }
}
