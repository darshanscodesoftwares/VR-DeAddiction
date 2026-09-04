// PubEnvironmentBuilder.cs
//
// Generates a lightweight TASMAC-style Indian bar/pub environment from Unity
// built-in primitives and flat Standard-shader materials.
//
//   Tools > Pub Environment > Build      (clears, then rebuilds)
//   Tools > Pub Environment > Clear      (removes generated geometry only)
//
// No packages, no textures, no render-pipeline changes, no VR code.
// Tune the constants in the PARAMETERS region and hit Build again.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PubEnvironmentBuilder
{
    const string RootName = "PubEnvironment_IN";
    const string LegacyRootName = "PubEnvironment";
    const string MaterialFolder = "Assets/PubEnvironment/Materials";

    #region PARAMETERS ---------------------------------------------------------

    // Hall interior. X = width, Z = depth (you enter from -Z and look down +Z).
    const float HallWidth = 12f;
    const float HallDepth = 20f;
    const float EavesHeight = 3.8f;   // underside of truss bottom chord
    const float RidgeRise = 0.7f;     // shallow pitch, as in the references

    // Metres of wall per repeat of the corrugated-sheet textures. Shared by the
    // entrance doors and the compound gate so both sheets have the same
    // corrugation pitch -- they are visible from one another.
    const float CorrugationTileMetres = 1.0f;

    // The building's entrance opening. Constants rather than locals so anything
    // built elsewhere in the yard can keep clear of the doorway without
    // restating its size.
    const float EntranceW = 4.2f;
    const float EntranceH = 2.6f;
    const float WallThickness = 0.25f;
    const float DadoHeight = 1.10f;   // painted lower band on the walls

    const int TrussCount = 6;
    const float RoofRibSpacing = 0.6f; // raise this to cut object count
    const bool  RoofSkylights = false; // false = sealed roof, bulb-lit interior

    const int TableCols = 3;
    const int TableRows = 4;
    const int ChairsPerTable = 2;

    /// <summary>
    /// Chance that any given table chair actually gets placed.
    ///
    /// Two chairs at every table reads as a laid-out dining room. A bar does
    /// not look like that -- chairs get taken to other tables and never come
    /// back, so some tables have two, some one, some none. Dropping them at
    /// random is what produces that unevenness; it also thins a room that had
    /// grown crowded once the chairs became full-size plastic ones with arms.
    ///
    /// Only the GRID chairs are subject to this. The seated scenario places its
    /// own chair, which must always exist.
    /// </summary>
    const float ChairKeepChance = 0.62f;

    const int Seed = 20260814;        // fixed so rebuilds are identical

    // Compound / exterior yard
    const float YardSide  = 5f;       // clearance each side of the building
    const float YardFront = 8f;       // approach yard depth in front of building
    const float YardBack  = 2f;       // space behind building
    const float CompoundWallH = 2.5f; // compound boundary wall height
    const float CompoundWallT = 0.20f;
    const float GateOpeningW  = 3.8f; // entrance gate width

    // Player rig produced by Build().
    //   Desktop = keyboard/mouse walkthrough for reviewing the scene on the Mac
    //   VR      = OpenXR rig for the headset (see XRRigBuilder.cs)
    public enum RigMode { Desktop, VR }
    // static readonly, not const: with a const the compiler folds the comparison
    // in BuildPlayer and reports the desktop branch as unreachable (CS0162).
    static readonly RigMode PlayerRig = RigMode.VR;

    #endregion

    static float HalfW { get { return HallWidth * 0.5f; } }
    static float HalfD { get { return HallDepth * 0.5f; } }
    static float RidgeHeight { get { return EavesHeight + RidgeRise; } }

    // Compound extents (computed from hall size + yard padding).
    static float YardHalfW  { get { return HalfW + WallThickness + YardSide; } }
    static float YardFrontZ { get { return -(HalfD + WallThickness + YardFront); } }
    static float YardBackZ  { get { return HalfD + WallThickness + YardBack; } }

    static System.Random _rng;
    static Dictionary<string, Material> _mats;
    static List<Vector3> _tableTops;   // so clutter lands on tables, not mid-air

    /// <summary>
    /// Where the visible lamps hang. BuildFixtures fills this and BuildLighting
    /// reads it, so a light is always AT a lamp.
    ///
    /// They used to be laid out independently: fixtures every 4.3 m, pixel
    /// lights every 6.2 m, vertex fill every 4.6 m. Three sets of positions
    /// that never coincided -- so the lamps you could see emitted nothing and
    /// the shadows came from empty air between them.
    /// </summary>
    static List<Vector3> _pendantBulbs;

    static List<Vector3> _bareBulbs;
    static int _objectCount;
    static int _triangleCount;

    static UnityEngine.SceneManagement.Scene ActiveScene
    {
        get { return UnityEngine.SceneManagement.SceneManager.GetActiveScene(); }
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Tools/Pub Environment/Build", false, 0)]
    public static void Build()
    {
        Clear();

        _rng = new System.Random(Seed);
        _mats = new Dictionary<string, Material>();
        _tableTops = new List<Vector3>();
        _pendantBulbs = new List<Vector3>();
        _bareBulbs = new List<Vector3>();
        _objectCount = 0;
        _triangleCount = 0;
        _grabbableCount = 0;
        _brands = null;   // rebuilt against this run's materials

        CreateMaterials();
        LoadGrabAudio();   // before ANY builder runs: several create grabbables

        GameObject root = new GameObject(RootName);
        root.transform.position = Vector3.zero;

        BuildShell(Group(root.transform, "01_Shell"));
        BuildRoofStructure(Group(root.transform, "02_RoofStructure"));
        BuildServiceArea(Group(root.transform, "03_ServiceArea"));
        BuildSeating(Group(root.transform, "04_Seating"));
        BuildFixtures(Group(root.transform, "05_Fixtures"));
        BuildSignage(Group(root.transform, "06_Signage"));
        BuildClutter(Group(root.transform, "07_Clutter"));
        BuildLighting(Group(root.transform, "08_Lighting"));
        BuildCompound(Group(root.transform, "10_Compound"));

        GameObject scenario = PubScenarioBuilder.Build(
            root.transform,
            M,
            (asset, parent, pos, yaw, mat, isStatic) =>
                Model(asset, parent, pos, yaw, mat, isStatic),
            (t, radius, height, mass) => GrabbableUpright(t, radius, height, mass),
            (asset, parent, pos, yaw, mat, isStatic, slots) =>
                Model(asset, parent, pos, yaw, mat, isStatic, slots));

        BuildPlayer(Group(root.transform, "09_Player"));

        WireScenario(root, scenario);

        ConfigureRenderSettings();

        EditorSceneManager.MarkSceneDirty(ActiveScene);
        Selection.activeGameObject = root;

        Debug.Log(string.Format(
            "[PubEnvironment] Built {0} objects, ~{1:n0} triangles, {2} materials, " +
            "{6} grabbable. Hall {3}x{4}m, eaves {5}m.",
            _objectCount, _triangleCount, _mats.Count, HallWidth, HallDepth, EavesHeight,
            _grabbableCount));
    }

    /// Headless entry point:
    ///   Unity -batchmode -quit -projectPath "<proj>" \
    ///         -executeMethod PubEnvironmentBuilder.BuildBatch -logFile -
    /// Opens the target scene, builds, and saves. Only needed for command-line
    /// runs; in the editor just use Tools > Pub Environment > Build.
    public static void BuildBatch()
    {
        const string scenePath = "Assets/unity-VR.unity";
        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        Build();

        bool saved = EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[PubEnvironment] BuildBatch saved=" + saved + " -> " + scenePath);
    }

    /// Headless preview render. Writes PNGs of a few fixed viewpoints so the
    /// environment can be checked without opening the editor.
    ///   Unity -batchmode -quit -projectPath "<proj>" \
    ///         -executeMethod PubEnvironmentBuilder.CaptureBatch -outDir /some/dir
    public static void CaptureBatch()
    {
        EditorSceneManager.OpenScene("Assets/unity-VR.unity", OpenSceneMode.Single);

        string outDir = "/tmp";
        string[] argv = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < argv.Length - 1; i++)
            if (argv[i] == "-outDir") outDir = argv[i + 1];

        GameObject camGo = GameObject.Find("Main Camera");
        if (camGo == null) { Debug.LogError("[PubEnvironment] No Main Camera."); return; }
        Camera cam = camGo.GetComponent<Camera>();
        if (cam == null) { Debug.LogError("[PubEnvironment] Main Camera has no Camera."); return; }

        var shots = new[]
        {
            new { name = "01_entrance", pos = new Vector3(0f, 1.6f, -HalfD + 1.0f),  rot = new Vector3(2f, 0f, 0f) },
            new { name = "02_midhall",  pos = new Vector3(-2.2f, 1.6f, -2.0f),       rot = new Vector3(3f, 22f, 0f) },
            new { name = "03_counter",  pos = new Vector3(-1.5f, 1.6f, 3.0f),        rot = new Vector3(4f, 28f, 0f) },
            new { name = "04_roof",     pos = new Vector3(0f, 1.5f, 0f),             rot = new Vector3(-32f, 5f, 0f) },
            new { name = "05_wide",     pos = new Vector3(4.6f, 2.4f, -8.4f),        rot = new Vector3(8f, -24f, 0f) },
        };

        const int W = 1280, H = 720;
        foreach (var s in shots)
        {
            cam.transform.position = s.pos;
            cam.transform.eulerAngles = s.rot;

            RenderTexture rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            string path = System.IO.Path.Combine(outDir, "pub_" + s.name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[PubEnvironment] Wrote " + path);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // Leave the camera back at the review position.
        PlaceReviewCamera();
    }

    [MenuItem("Tools/Pub Environment/Clear", false, 1)]
    public static void Clear()
    {
        int removed = 0;
        foreach (string n in new[] { RootName, LegacyRootName })
        {
            // Guarded loop: DestroyImmediate can no-op on some objects, and an
            // unguarded while(Find(n) != null) would hang the editor.
            for (int guard = 0; guard < 64; guard++)
            {
                GameObject go = GameObject.Find(n);
                if (go == null) break;
                Object.DestroyImmediate(go);
                removed++;
            }
        }
        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(ActiveScene);
            Debug.Log("[PubEnvironment] Cleared " + removed + " generated root(s).");
        }
    }

    // ------------------------------------------------------------- materials

    static void CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/PubEnvironment"))
            AssetDatabase.CreateFolder("Assets", "PubEnvironment");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/PubEnvironment", "Materials");

        // Shell ---------------------------------------------------------------
        // ambientCG Concrete034 (CC0), colour plus normal. The hall floor is the
        // surface a patient looks at most -- you walk on it the whole session --
        // and it was a single flat grey. Tiling is set where the floor is built,
        // from its real size.
        MatTextured("Mat_Pub_ConcreteFloor", "Assets/Textures/Floor_Concrete.png", 0.10f,
                    normalPath: "Assets/Textures/Floor_Concrete_Normal.png");
        Mat("Mat_Pub_PlasterPink", Rgb(214, 176, 168), 0.06f, 0f);
        Mat("Mat_Pub_PlasterWhite", Rgb(222, 216, 204), 0.06f, 0f);
        Mat("Mat_Pub_BrickWhitewash", Rgb(198, 172, 164), 0.05f, 0f);
        Mat("Mat_Pub_Dado", Rgb(150, 88, 106), 0.12f, 0f);

        // Structure -----------------------------------------------------------
        Mat("Mat_Pub_SteelBlue", Rgb(38, 84, 140), 0.35f, 0.25f);
        Mat("Mat_Pub_RoofSheet", Rgb(120, 118, 112), 0.30f, 0.55f);
        MatEmissive("Mat_Pub_Skylight", Rgb(236, 240, 235), Rgb(255, 252, 238), 1.15f);

        // Furniture -----------------------------------------------------------
        Mat("Mat_Pub_ConcreteTable", Rgb(126, 124, 118), 0.20f, 0f);
        // Round bar table. Textured rather than flat, so it keeps its grain and
        // wear -- Shade() sends textured materials straight through and skips
        // the vertex-colour grime shader, which has no _MainTex and would throw
        // the wood away.
        MatTextured("Mat_Pub_TableWood", "Assets/Textures/Table_Wood.png", 0.22f,
                    normalPath: "Assets/Textures/Table_Wood_Normal.png");
        Mat("Mat_Pub_ChairRed", Rgb(190, 34, 44), 0.55f, 0f);
        Mat("Mat_Pub_ChairBlue", Rgb(36, 68, 158), 0.55f, 0f);
        Mat("Mat_Pub_ChairGreen", Rgb(58, 158, 62), 0.55f, 0f);

        // Service area --------------------------------------------------------
        Mat("Mat_Pub_GrilleTeal", Rgb(78, 168, 174), 0.42f, 0.30f);
        Mat("Mat_Pub_CoolerRed", Rgb(176, 32, 34), 0.48f, 0.10f);
        Mat("Mat_Pub_DarkWood", Rgb(64, 44, 32), 0.22f, 0f);
        MatTransparent("Mat_Pub_GlassClear", new Color(0.78f, 0.85f, 0.86f, 0.28f), 0.92f);

        // Jack Daniel's ---------------------------------------------------------
        // The only textured materials in the project. Everywhere else the
        // palette is flat colour, which is what keeps 41 materials instancing
        // cheaply -- but a brand label cannot be a flat colour, and the brand
        // IS the therapeutic cue.
        MatTransparent("Mat_JD_Glass", new Color(0.42f, 0.22f, 0.07f, 0.62f), 0.94f);
        Mat("Mat_JD_Whiskey", Rgb(150, 74, 16), 0.78f, 0f);
        MatTextured("Mat_JD_Label", "Assets/Textures/JD_Label.jpg", 0.30f);
        MatTextured("Mat_JD_Cap", "Assets/Textures/JD_Cap.png", 0.42f);

        // Whiskey glass ---------------------------------------------------------
        // Game-ready source: 1,490 triangles, already baked to low-poly with
        // normal maps, so the cut-glass facets are shading detail rather than
        // geometry. The alpha here is uniform -- the supplied opacity maps are
        // effectively flat, so there is nothing to gain from compositing them.
        MatTextured("Mat_WG_Glass", "Assets/Textures/WG_Glass_BaseColor.png", 0.95f,
                    "Assets/Textures/WG_Glass_Normal.png",
                    new Color(1f, 1f, 1f, 0.42f), transparent: true);
        MatTextured("Mat_WG_Whiskey", "Assets/Textures/WG_Whiskey_BaseColor.png", 0.80f,
                    "Assets/Textures/WG_Whiskey_Normal.png");

        // Stand-up button icon. Emissive, because the seated scenario deliberately
        // dims the room and an unlit control would disappear into it.
        MatTextured("Mat_Pub_ExitIcon", "Assets/Textures/ExitIcon.jpg", 0.35f);
        Material exitIcon = M("Mat_Pub_ExitIcon");
        if (exitIcon != null)
        {
            exitIcon.EnableKeyword("_EMISSION");
            exitIcon.SetColor("_EmissionColor", new Color(0.55f, 0.10f, 0.16f));
            exitIcon.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(exitIcon);
        }

        // Clutter -------------------------------------------------------------
        MatTextured("Mat_Pub_DrinksShelf", "Assets/Textures/DrinksShelf.jpg", 0.30f,
                    "Assets/Textures/DrinksShelf_Normal.jpg");
        // Counter timber. Tiled 4x along its length: the counter runs 6.6 m, and
        // at 1:1 a 2K wood sheet stretches its grain across the whole span and
        // reads as a photograph of wood rather than as boards. Four repeats put
        // a plank at ~1.7 m, which is a believable length.
        MatTextured("Mat_Pub_CounterWood", "Assets/Textures/Counter_Wood.png", 0.30f,
                    "Assets/Textures/Counter_Wood_Normal.png",
                    tiling: new Vector2(4f, 1f));
        // Cardboard is matte -- any sheen on it reads as plastic wrapping.
        MatTextured("Mat_Pub_CardboardBox", "Assets/Textures/CardboardBox.png", 0.05f,
                    "Assets/Textures/CardboardBox_Normal.png");
        Mat("Mat_Pub_WaterCase", Rgb(178, 206, 214), 0.62f, 0f);
        Mat("Mat_Pub_CrateGreen", Rgb(46, 132, 84), 0.50f, 0f);
        Mat("Mat_Pub_FanBlade", Rgb(72, 56, 44), 0.28f, 0f);
        Mat("Mat_Pub_Litter", Rgb(206, 200, 188), 0.18f, 0f);

        // Lights --------------------------------------------------------------
        MatEmissiveBright("Mat_Pub_Bulb", Rgb(255, 238, 198), Rgb(255, 226, 158), 2.2f);
        MatEmissiveBright("Mat_Pub_Tube", Rgb(238, 246, 255), Rgb(206, 228, 255), 1.5f);
        Mat("Mat_Pub_Enamel", Rgb(224, 222, 214), 0.45f, 0.05f);

        // Signage (generic, no real branding) ----------------------------------
        Mat("Mat_Pub_PosterGreen", Rgb(42, 92, 46), 0.20f, 0f);
        Mat("Mat_Pub_PosterDark", Rgb(38, 40, 46), 0.20f, 0f);
        Mat("Mat_Pub_PosterWarm", Rgb(168, 108, 42), 0.20f, 0f);
        Mat("Mat_Pub_PosterCream", Rgb(214, 202, 176), 0.20f, 0f);

        // Reuse whatever the earlier session left, if it is still there.
        Mat("Mat_Pub_Metal", Rgb(139, 140, 146), 0.60f, 0.85f);
        Mat("Mat_Pub_GlassAmber", Rgb(115, 60, 14), 0.88f, 0f);
        Mat("Mat_Pub_GlassGreen", Rgb(28, 74, 40), 0.88f, 0f);

        // Bottle labels. Paper, so almost no smoothness. Several colourways so
        // a shelf or table reads as different brands rather than one product.
        Mat("Mat_Pub_LabelCream",  Rgb(226, 214, 186), 0.06f, 0f);
        Mat("Mat_Pub_LabelRed",    Rgb(156, 38, 34),   0.06f, 0f);
        Mat("Mat_Pub_LabelBlue",   Rgb(30, 56, 112),   0.06f, 0f);
        Mat("Mat_Pub_LabelGold",   Rgb(176, 140, 58),  0.20f, 0.25f);
        Mat("Mat_Pub_LabelGreen",  Rgb(36, 92, 52),    0.06f, 0f);
        Mat("Mat_Pub_LabelWhite",  Rgb(224, 224, 220), 0.06f, 0f);

        // Neck foil and caps: metal, so they catch the bulbs.
        Mat("Mat_Pub_FoilGold",   Rgb(186, 152, 62),  0.62f, 0.75f);
        Mat("Mat_Pub_FoilSilver", Rgb(178, 182, 186), 0.62f, 0.80f);
        Mat("Mat_Pub_CapRed",     Rgb(142, 32, 30),   0.42f, 0.35f);
        Mat("Mat_Pub_CapGold",    Rgb(170, 138, 56),  0.55f, 0.70f);
        Mat("Mat_Pub_CapBlack",   Rgb(26, 26, 28),    0.45f, 0.30f);

        // Bottle contents, seen through clear glass. A visible fill line does
        // more for realism than any amount of label detail.
        Mat("Mat_Pub_LiquidAmber", Rgb(150, 84, 22),  0.80f, 0f);
        Mat("Mat_Pub_LiquidRed",   Rgb(132, 30, 38),  0.80f, 0f);
        Mat("Mat_Pub_LiquidClear", Rgb(206, 212, 200), 0.85f, 0f);
        Mat("Mat_Pub_LiquidDark",  Rgb(28, 20, 18),   0.70f, 0f);
        Mat("Mat_Pub_LiquidBlue",  Rgb(74, 146, 176), 0.82f, 0f);

        // Compound / exterior ------------------------------------------------
        // Yard ground: ambientCG Ground109, a CC0 photogrammetry SURFACE (as
        // opposed to a scanned mesh) so it is genuinely seamless and ships a
        // normal map. The normal is what stops the ground reading as a
        // photograph laid flat -- without it the pebbles have colour but no
        // relief and never catch the light as you walk past.
        MatTextured("Mat_Pub_DirtGround", "Assets/Textures/Ground_Dirt.png", 0.06f,
                    normalPath: "Assets/Textures/Ground_Dirt_Normal.png");
        Mat("Mat_Pub_CompoundWall",   Rgb(202, 190, 162), 0.06f, 0f);
        Mat("Mat_Pub_GateWood",       Rgb(168, 128, 68),  0.18f, 0f);
        // PAINTED PANELS, not a light box.
        //
        // These were emissive, and an emissive surface renders at full
        // brightness no matter what light reaches it -- so the board glowed
        // evenly on its own and the two lamps above it had nothing left to do.
        // The flicker was there and simply could not be seen against it.
        //
        // Plain materials mean the board is only as bright as the lamps make
        // it, which is what lets one failing tube actually show.
        Mat("Mat_Pub_SignboardGreen", Rgb(16, 122, 108), 0.35f, 0f);
        Mat("Mat_Pub_SignboardLight", Rgb(40, 186, 162), 0.30f, 0f);
        Mat("Mat_Pub_SignboardText", Rgb(246, 252, 250), 0.25f, 0f);
        Mat("Mat_Pub_SignWire", Rgb(28, 26, 24), 0.30f, 0.20f);
        Mat("Mat_Pub_ConcretePole",   Rgb(172, 168, 160), 0.10f, 0f);
        Mat("Mat_Pub_StreetGrey",     Rgb(92, 88, 84),    0.10f, 0f);
        MatTextured("Mat_Pub_BarLamp", "Assets/Textures/BarLamp.png", 0.35f,
                    "Assets/Textures/BarLamp_Normal.png");
        // Weathered brass for the exterior gooseneck lights. The model ships no
        // textures, only Maya lamberts, so this is a flat colour.
        Mat("Mat_Pub_WallLightBody", Rgb(118, 100, 66), 0.34f, 0.55f);
        // Yard bush. Slot 0 is BRANCHES and slot 1 is LEAVES -- established by
        // face count (slot 1 carries 79,230 of 102,289) and by UV layout, not
        // assumed. Getting these the wrong way round painted the branches green
        // and the leaves beige, and the bush read as a heap of dead shards.
        Mat("Mat_Pub_BushBark", Rgb(74, 51, 33), 0.10f, 0f);
        Mat("Mat_Pub_BushLeaf", Rgb(33, 82, 23), 0.12f, 0f);

        // Painted corrugated steel for the entrance doors, front and back. Same
        // albedo; the back's normal map has its X and Y negated so its ridges
        // read as grooves -- see the door build for why that is not optional.
        // Slightly metallic and fairly matte: this is old painted sheet, not
        // bare steel.
        foreach (string dn in new[] { "Mat_Pub_DoorSteel", "Mat_Pub_DoorSteelBack" })
        {
            Material dm = MatTextured(dn, "Assets/Textures/DoorSteel.png", 0.28f,
                                      dn.EndsWith("Back")
                                          ? "Assets/Textures/DoorSteel_NormalBack.png"
                                          : "Assets/Textures/DoorSteel_Normal.png");
            dm.SetFloat("_Metallic", 0.30f);
            EditorUtility.SetDirty(dm);
        }

        // Corrugated steel for the compound gate leaves. CorrugatedSteel007A is
        // clean blue paint with rust only around the rivet heads, against the
        // doors' heavily rusted 007B -- so the gate reads as the newer sheet,
        // which is the usual way round for a compound that is locked at night.
        // Same front/back pairing as the doors, and for the same reason.
        foreach (string gn in new[] { "Mat_Pub_GateSteel", "Mat_Pub_GateSteelBack" })
        {
            Material gm = MatTextured(gn, "Assets/Textures/GateSteel.png", 0.34f,
                                      gn.EndsWith("Back")
                                          ? "Assets/Textures/GateSteel_NormalBack.png"
                                          : "Assets/Textures/GateSteel_Normal.png");
            gm.SetFloat("_Metallic", 0.30f);
            EditorUtility.SetDirty(gm);
        }

        // Asphalt: low smoothness, but not zero -- a road is not chalk.
        MatTextured("Mat_Pub_Road", "Assets/Textures/Road.png", 0.18f,
                    "Assets/Textures/Road_Normal.png");

        AssetDatabase.SaveAssets();
    }

    static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    static Material Mat(string name, Color color, float smoothness, float metallic)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        m.SetColor("_Color", color);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);

        // CLEAR EMISSION EXPLICITLY.
        //
        // These are .mat ASSETS on disk, loaded and reused between builds -- not
        // fresh objects. A material that was emissive in an earlier run stays
        // emissive for ever unless something turns it off, because setting a
        // colour says nothing about emission.
        //
        // That is exactly how the signboard kept glowing after being changed
        // from MatEmissive to Mat: the source said "painted panel" and the
        // asset on disk still said "light box". MatEmissive re-enables this
        // immediately afterwards, so nothing that should glow is affected.
        m.SetColor("_EmissionColor", Color.black);
        m.DisableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        _mats[name] = m;
        return m;
    }

    static Material MatEmissive(string name, Color baseColor, Color emission, float intensity)
    {
        Material m = Mat(name, baseColor, 0.30f, 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Emissive fixture material with extra punch. With the roof sealed and
    /// ambient near-black, bulbs and tubes are the brightest things in view,
    /// so their surfaces need to read as the source rather than as pale grey.
    /// </summary>
    static Material MatEmissiveBright(string name, Color baseColor, Color emission, float intensity)
    {
        return MatEmissive(name, baseColor, emission, intensity * 2.6f);
    }

    static Material MatTransparent(string name, Color color, float smoothness)
    {
        Material m = Mat(name, color, smoothness, 0f);
        m.SetFloat("_Mode", 3f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material M(string name)
    {
        Material m;
        if (_mats.TryGetValue(name, out m)) return m;
        Debug.LogWarning("[PubEnvironment] Missing material " + name);
        return null;
    }

    // -------------------------------------------------------------- primitives

    static Transform Group(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent,
                           Vector3 pos, Vector3 scale, Material mat,
                           Vector3 euler, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;

        Collider col = go.GetComponent<Collider>();
        if (col != null && !keepCollider) Object.DestroyImmediate(col);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        _objectCount++;
        _triangleCount += TrisFor(type);
        return go;
    }

    static int TrisFor(PrimitiveType t)
    {
        switch (t)
        {
            case PrimitiveType.Cube: return 12;
            case PrimitiveType.Cylinder: return 80;
            case PrimitiveType.Sphere: return 768;
            case PrimitiveType.Quad: return 2;
            case PrimitiveType.Plane: return 200;
            default: return 12;
        }
    }

    /// <summary>
    /// Connects the scenario to the rig and the lighting after both exist.
    /// Done here rather than in either builder because it is the one place
    /// that can see the whole scene.
    /// </summary>
    static void WireScenario(GameObject root, GameObject scenario)
    {
        if (scenario == null)
            return;

        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null)
        {
            Debug.LogWarning("[Scenario] No XR Origin - scenario left unwired.");
            return;
        }

        // Lighting director lives on the rig so it can be found from the
        // scenario, and so it dies with the rig on rebuild.
        var director = origin.gameObject.AddComponent<LightingDirector>();

        foreach (Light l in root.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Directional)
                continue;   // the sun is exterior; the interior moods must not touch it
            director.RoomLights.Add(l);
        }

        Transform focus = scenario.transform.Find("Lamp_TableFocus");
        if (focus != null)
            director.FocusLight = focus.GetComponent<Light>();

        // Pay for the focus pixel light by demoting the pendant furthest from
        // the hero table, so the pixel-light count never rises above four.
        Light farthest = null;
        float bestDist = -1f;
        Vector3 heroPos = new Vector3(PubScenarioBuilder.TableX, 0f, PubScenarioBuilder.TableZ);
        foreach (Light l in root.GetComponentsInChildren<Light>(true))
        {
            if (l.renderMode != LightRenderMode.ForcePixel) continue;
            float d = Vector3.Distance(new Vector3(l.transform.position.x, 0f, l.transform.position.z), heroPos);
            if (d > bestDist) { bestDist = d; farthest = l; }
        }
        director.DemotableLight = farthest;

        var scen = origin.gameObject.AddComponent<SeatedTableScenario>();
        scen.Lighting = director;

        // The stand-up button's look. Supplied by the builder rather than built
        // at runtime, so the icon is a real asset with a texture instead of a
        // coloured primitive.
        scen.ButtonPrefab = ModelSource("Asset_ExitIcon");
        scen.ButtonMaterial = M("Mat_Pub_ExitIcon");
        if (scen.ButtonPrefab == null)
            Debug.LogWarning("[PubEnvironment] Exit icon mesh missing; button stays a cube.");
        scen.Marker = scenario.transform.Find("Marker");
        scen.SeatAnchor = scenario.transform.Find("SeatAnchor");

        Transform volume = scenario.transform.Find("TableVolume");
        if (volume != null)
            scen.Containment = volume.GetComponent<TableContainment>();

        Transform propRoot = scenario.transform.Find("TableProps");
        if (propRoot != null)
        {
            foreach (var gi in propRoot.GetComponentsInChildren<
                     UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
            {
                scen.TableProps.Add(gi);
            }
        }

        Debug.Log($"[Scenario] Wired: {director.RoomLights.Count} room lights, " +
                  $"{scen.TableProps.Count} gated props, demoted '{(farthest != null ? farthest.name : "none")}'.");
    }

    // --------------------------------------------------------- Blender models

    const string ModelFolder = "Assets/Models";

    static readonly Dictionary<string, GameObject> _modelCache =
        new Dictionary<string, GameObject>();

    /// <summary>
    /// Loads an FBX built by BlenderAssets/scripts/build_all.py.
    /// Returns null when the model is absent, so every call site can fall back
    /// to its original primitive version and the scene still builds.
    /// </summary>
    static GameObject ModelSource(string assetName)
    {
        GameObject src;
        if (_modelCache.TryGetValue(assetName, out src))
            return src;

        src = AssetDatabase.LoadAssetAtPath<GameObject>(
            ModelFolder + "/" + assetName + ".fbx");

        if (src == null)
            Debug.LogWarning("[PubEnvironment] Model not found: " + assetName +
                             " - falling back to primitives.");

        _modelCache[assetName] = src;
        return src;
    }

    /// <summary>
    /// Returns a VertexGrime variant of a flat material, creating it on first
    /// use. Models carry grime baked into vertex colours by the Blender build;
    /// Unity's Standard shader ignores vertex colours entirely, so models need
    /// this shader while primitives keep plain Standard.
    ///
    /// Kept as a separate material rather than switching the palette wholesale:
    /// primitives have no vertex colour channel, and what a shader reads for a
    /// missing channel is not guaranteed.
    /// </summary>
    /// <summary>
    /// A Standard material carrying a real texture. Used only for the branded
    /// bottle: see the note where these are declared.
    /// </summary>
    /// <summary>
    /// Sets a material's tiling on BOTH its albedo and its normal map.
    ///
    /// mainTextureScale only touches _MainTex. Setting it alone leaves the
    /// normal map at 1:1, so the surface relief stops corresponding to the
    /// colour -- which is what the hall floor shipped with: albedo repeating
    /// 6.25 x 10.25 over the slab and its bumps stretched once across the whole
    /// 12 x 20 m. Every tiling change goes through here.
    /// </summary>
    static void Tile(Material m, Vector2 scale)
    {
        if (m == null)
            return;
        m.mainTextureScale = scale;
        if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)
            m.SetTextureScale("_BumpMap", scale);
        EditorUtility.SetDirty(m);
    }

    /// <summary>
    /// A brick material tiled to suit ONE wall's real size.
    ///
    /// Unity's cube UVs run 0-1 per FACE, so a single tiling value gives a
    /// 20 m side wall and a 4 m lintel completely different brick sizes -- the
    /// bricks stop being a unit of measurement and the building stops reading as
    /// masonry. Each distinct wall size therefore gets its own material, tiled
    /// to WHOLE repeats so the pattern wraps without a seam. There are only a
    /// few distinct sizes, so this is a handful of materials, not one per wall.
    /// </summary>
    const float BrickTileMetres = 2.0f;

    static Material BrickWall(float widthM, float heightM)
    {
        // Round to whole repeats where there is more than one, so the pattern
        // wraps without a visible seam. Below that, keep the exact fraction:
        // forcing a 0.5 m pier up to one whole repeat would print bricks four
        // times the size of the wall it stands against, which is worse than any
        // seam -- and on something that narrow there is no seam to see anyway.
        float fx = widthM / BrickTileMetres;
        float fy = heightM / BrickTileMetres;
        float tx = fx >= 1.5f ? Mathf.Round(fx) : fx;
        float ty = fy >= 1.5f ? Mathf.Round(fy) : fy;
        return MatTextured($"Mat_Pub_BrickWall_{tx:0.00}x{ty:0.00}",
                           "Assets/Textures/Brick_Wall.png", 0.06f,
                           "Assets/Textures/Brick_Wall_Normal.png",
                           tiling: new Vector2(tx, ty));
    }

    /// <summary>
    /// Timber tiled to suit ONE object's real size, so grain is the same size
    /// on the 6.6 m counter as on a 0.36 m shelf deck standing beside it.
    ///
    /// Takes the box's dimensions and tiles from its two LARGEST, which is the
    /// face you actually look at. Unlike BrickWall this keeps exact fractions
    /// rather than rounding to whole repeats: wood grain has no module to line
    /// up, the texture is seamless, and a shelf deck 0.36 m deep genuinely
    /// should show only a fifth of a plank.
    /// </summary>
    const float WoodTileMetres = 1.65f;   // a believable plank length

    static Material WoodSurface(Vector3 size)
    {
        float[] d = { Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z) };
        System.Array.Sort(d);
        float tx = Mathf.Max(0.02f, d[2] / WoodTileMetres);
        float ty = Mathf.Max(0.02f, d[1] / WoodTileMetres);
        return MatTextured($"Mat_Pub_Wood_{tx:0.00}x{ty:0.00}",
                           "Assets/Textures/Counter_Wood.png", 0.30f,
                           "Assets/Textures/Counter_Wood_Normal.png",
                           tiling: new Vector2(tx, ty));
    }

    /// <summary>
    /// An ALPHA-CUT material, for foliage. Cutout rather than transparent:
    /// blended geometry has to be sorted back-to-front and grass cards
    /// interpenetrate constantly, so blending gives visible popping as the
    /// player walks. Cutout also still writes depth, which cutout foliage needs.
    /// </summary>
    static Material MatCutout(string name, string texturePath)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        m.SetTexture("_MainTex",
                     AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Mode", 1f);                 // Cutout
        m.SetFloat("_Cutoff", 0.45f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 2450;
        m.SetFloat("_Glossiness", 0.12f);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        _mats[name] = m;
        return m;
    }

    static Material MatTextured(string name, string texturePath, float smoothness,
                                string normalPath = null, Color? tint = null,
                                bool transparent = false, Vector2? tiling = null)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
            Debug.LogWarning("[PubEnvironment] Missing texture " + texturePath);
        m.SetTexture("_MainTex", tex);

        if (!string.IsNullOrEmpty(normalPath))
        {
            Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (nrm == null)
                Debug.LogWarning("[PubEnvironment] Missing normal map " + normalPath);
            m.SetTexture("_BumpMap", nrm);
            m.EnableKeyword("_NORMALMAP");
        }

        m.SetColor("_Color", tint ?? Color.white);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", 0f);

        if (transparent)
        {
            m.SetFloat("_Mode", 3f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        if (tiling.HasValue)
            Tile(m, tiling.Value);

        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        _mats[name] = m;
        return m;
    }

    /// <summary>
    /// Picks the shader a material should actually render with.
    ///
    /// VertexGrime is colour-only -- it has no _MainTex -- so pushing a
    /// textured material through it silently discards the texture and the
    /// label would render as flat brown. Textured materials therefore keep
    /// their own shader. They lose nothing by it: grime is driven by vertex
    /// colours, and an imported model has none, so the effect would be a no-op
    /// on this mesh anyway.
    /// </summary>
    static Material Shade(Material flat)
    {
        if (flat == null)
            return GrimeVariant(flat);

        // Textured materials keep their texture: VertexGrime has no _MainTex.
        if (flat.HasProperty("_MainTex") && flat.GetTexture("_MainTex") != null)
            return flat;

        // EMISSIVE materials keep their emission, for the same reason -- the
        // grime shader has no emission either, so a light source handed through
        // here comes back unlit. The wall lights' emitter strips went dark
        // exactly this way: the fixture rendered, its bulb did not glow, and
        // nothing in the scene said why.
        if (flat.IsKeywordEnabled("_EMISSION") ||
            (flat.HasProperty("_EmissionColor") &&
             flat.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            return flat;

        return GrimeVariant(flat);
    }

    static Material GrimeVariant(Material flat)
    {
        if (flat == null)
            return null;

        string path = MaterialFolder + "/" + flat.name + "_Grime.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader grimeShader = Shader.Find("PubEnvironment/VertexGrime");
        if (grimeShader == null)
        {
            Debug.LogWarning("[PubEnvironment] VertexGrime shader missing - using flat material.");
            return flat;
        }

        if (m == null)
        {
            m = new Material(grimeShader);
            AssetDatabase.CreateAsset(m, path);
        }

        m.shader = grimeShader;
        m.SetColor("_Color", flat.GetColor("_Color"));
        m.SetFloat("_Glossiness", flat.HasProperty("_Glossiness") ? flat.GetFloat("_Glossiness") : 0.3f);
        m.SetFloat("_Metallic", flat.HasProperty("_Metallic") ? flat.GetFloat("_Metallic") : 0f);
        m.SetFloat("_GrimeStrength", 1f);
        // 64 chairs, 38 bottles and glasses all share one mesh and material.
        // Instancing draws them in a handful of calls instead of one each.
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Instantiates a Blender model under <paramref name="parent"/> and applies
    /// one of the existing flat materials. Blender supplies shape only; Unity
    /// keeps owning the look, so the 41-material palette stays consistent.
    /// </summary>
    static GameObject Model(string assetName, Transform parent, Vector3 pos,
                            float yaw, Material mat, bool isStatic = true,
                            Material[] slotMaterials = null)
    {
        GameObject src = ModelSource(assetName);
        if (src == null)
            return null;

        // Blender FBX imports with a -90 deg X rotation on its root, which is
        // what stands the Z-up mesh upright in Unity's Y-up world. Writing yaw
        // straight onto that transform destroys it and lays the asset flat, so
        // yaw goes on a holder and the model's own rotation is left untouched.
        GameObject holder = new GameObject(assetName);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = pos;
        holder.transform.localEulerAngles = new Vector3(0f, yaw, 0f);

        GameObject go = (GameObject)Object.Instantiate(src, holder.transform);
        go.name = assetName + "_Mesh";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = src.transform.localRotation;
        go.transform.localScale = src.transform.localScale;

        Material grimeMat = Shade(mat);

        foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
        {
            if (slotMaterials != null && mr.sharedMaterials.Length > 1)
            {
                // Multi-slot model (bottle: glass / label / foil / cap). Fill
                // every slot the mesh actually has, falling back to the body
                // material so a missing entry never renders as magenta.
                Material[] assigned = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < assigned.Length; i++)
                {
                    Material slotMat = (i < slotMaterials.Length && slotMaterials[i] != null)
                        ? slotMaterials[i] : mat;
                    assigned[i] = Shade(slotMat);
                }
                mr.sharedMaterials = assigned;
            }
            else if (grimeMat != null)
            {
                mr.sharedMaterial = grimeMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                _triangleCount += mf.sharedMesh.triangles.Length / 3;
        }

        // Imported meshes arrive with no collider; grabbables get one from
        // MakeGrabbable, static scenery does not need one here.
        if (isStatic)
        {
            foreach (Transform t in holder.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        _objectCount++;
        return holder;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size,
                          Material mat, bool keepCollider = false)
    {
        return Prim(PrimitiveType.Cube, name, parent, pos, size, mat, Vector3.zero, keepCollider);
    }

    static GameObject BoxR(string name, Transform parent, Vector3 pos, Vector3 size,
                           Material mat, Vector3 euler, bool keepCollider = false)
    {
        return Prim(PrimitiveType.Cube, name, parent, pos, size, mat, euler, keepCollider);
    }

    /// Unity's cylinder is 2 units tall and 1 unit across, so height is halved here.
    static GameObject Tube(string name, Transform parent, Vector3 pos, float diameter,
                           float height, Material mat, Vector3 euler)
    {
        return Prim(PrimitiveType.Cylinder, name, parent, pos,
                    new Vector3(diameter, height * 0.5f, diameter), mat, euler, false);
    }

    /// A straight structural member between two points in the XY plane at depth z.
    static GameObject Member(string name, Transform parent, Vector2 a, Vector2 b,
                             float z, float thickness, Material mat)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Vector2 mid = (a + b) * 0.5f;
        return BoxR(name, parent, new Vector3(mid.x, mid.y, z),
                    new Vector3(len, thickness, thickness), mat, new Vector3(0f, 0f, ang));
    }

    static float Rand(float min, float max)
    {
        return min + (float)_rng.NextDouble() * (max - min);
    }

    static int RandInt(int minInclusive, int maxExclusive)
    {
        return _rng.Next(minInclusive, maxExclusive);
    }

    /// Height of the roof underside at a given x (shallow gable, ridge at x = 0).
    /// <summary>
    /// Where the exterior gooseneck lamps hang. One definition, because two
    /// things need it: the lamps themselves, and TREE placement, which has to
    /// keep canopies off them. A palm planted 1.75 m from the wall swallowed
    /// ExtLight_R_Back entirely -- its fronds enclosed the lamp and sat 0.15 m
    /// into the beam -- and from the yard that reads as a light that does not
    /// work rather than a light behind a tree.
    /// </summary>
    static Vector3[] ExteriorLampMounts()
    {
        const float mountY = 2.95f;              // clear of the entrance head
        float wt = WallThickness;
        float endZ = HalfD - 2.5f;
        return new[]
        {
            new Vector3(0f, mountY, -HalfD - wt),
            new Vector3(-HalfW - wt, mountY, -endZ),
            new Vector3(-HalfW - wt, mountY,  endZ),
            new Vector3( HalfW + wt, mountY, -endZ),
            new Vector3( HalfW + wt, mountY,  endZ),
        };
    }

    /// <summary>
    /// Centre-line Z of truss <paramref name="i"/>. Fixtures hang from these,
    /// so they must come from the same expression the roof uses -- a fixture
    /// spaced by its own arithmetic hangs from nothing.
    /// </summary>
    /// <summary>
    /// Returns the index of a layer, creating it in TagManager if absent.
    /// Ground cover needs its own layer because Unity's cull distances are
    /// per-LAYER, not per-renderer.
    /// </summary>
    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0)
            return existing;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        // 0-7 are Unity's built-ins and must not be touched.
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue))
                continue;
            slot.stringValue = name;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[PubEnvironment] Created layer '{name}' at index {i}.");
            return i;
        }

        Debug.LogWarning($"[PubEnvironment] No free layer slot for '{name}'.");
        return 0;
    }

    static float TrussZ(int i)
    {
        float step = HallDepth / TrussCount;
        return -HalfD + step * 0.5f + i * step;
    }

    static float RoofY(float x)
    {
        return RidgeHeight - Mathf.Abs(x) / HalfW * RidgeRise;
    }

    // ------------------------------------------------------------------ shell

    static void BuildShell(Transform g)
    {
        Material floorMat = M("Mat_Pub_ConcreteFloor");
        Material plaster = M("Mat_Pub_PlasterPink");
        Material brick = M("Mat_Pub_BrickWhitewash");
        Material dado = M("Mat_Pub_Dado");

        float wallH = EavesHeight;
        float t = WallThickness;
        float outerW = HallWidth + t * 2f;
        float outerD = HallDepth + t * 2f;

        Box("Floor", g, new Vector3(0f, -0.1f, 0f),
            new Vector3(outerW, 0.2f, outerD), floorMat, true);

        // 2 m per repeat. Concrete has no obvious features to give the repeat
        // away, so it can tile tighter than the yard's dirt -- and a tighter
        // repeat keeps the grain readable underfoot instead of smearing across
        // a 12 x 20 m slab.
        if (floorMat != null)
        {
            const float FloorTileMetres = 2f;
            Tile(floorMat,
                 new Vector2(outerW / FloorTileMetres, outerD / FloorTileMetres));
        }

        // Every wall of the building is the same brick, inside and out. Each
        // one is tiled to its own dimensions so a brick is the same size on the
        // 20 m side wall as on the 4 m lintel. Doors, dado band and jambs are
        // deliberately NOT brick.
        // Side walls (+/-X).
        Box("Wall_Right", g, new Vector3(HalfW + t * 0.5f, wallH * 0.5f, 0f),
            new Vector3(t, wallH, outerD), BrickWall(outerD, wallH), true);
        Box("Wall_Left", g, new Vector3(-HalfW - t * 0.5f, wallH * 0.5f, 0f),
            new Vector3(t, wallH, outerD), BrickWall(outerD, wallH), true);

        // Back wall (+Z) - solid, no doorway.
        float backZ = HalfD + t * 0.5f;
        Box("Wall_Back", g,
            new Vector3(0f, wallH * 0.5f, backZ),
            new Vector3(outerW, wallH, t), BrickWall(outerW, wallH), true);

        // Front wall (-Z) with the wide open entrance.
        float entW = EntranceW, entH = EntranceH;
        float frontZ = -HalfD - t * 0.5f;
        float fSegW = (HalfW + t) - entW * 0.5f;
        Box("Wall_Front_Left", g,
            new Vector3(-entW * 0.5f - fSegW * 0.5f, wallH * 0.5f, frontZ),
            new Vector3(fSegW, wallH, t), BrickWall(fSegW, wallH), true);
        Box("Wall_Front_Right", g,
            new Vector3(entW * 0.5f + fSegW * 0.5f, wallH * 0.5f, frontZ),
            new Vector3(fSegW, wallH, t), BrickWall(fSegW, wallH), true);
        Box("Wall_Front_Lintel", g,
            new Vector3(0f, entH + (wallH - entH) * 0.5f, frontZ),
            new Vector3(entW, wallH - entH, t), BrickWall(entW, wallH - entH), true);

        // Entrance reveal, so the opening reads as thick masonry.
        Box("Entrance_Jamb_L", g, new Vector3(-entW * 0.5f, entH * 0.5f, frontZ),
            new Vector3(0.12f, entH, t * 1.15f), dado);
        Box("Entrance_Jamb_R", g, new Vector3(entW * 0.5f, entH * 0.5f, frontZ),
            new Vector3(0.12f, entH, t * 1.15f), dado);

        // Open entrance doors -- two leaves swung inward, corrugated steel.
        //
        // Each leaf is one slab plus a thin skin on its reverse, because a box
        // has ONE material and the two faces of a corrugated sheet are not the
        // same surface: the back is the negative of the front, a groove wherever
        // the front has a ridge. Lighting both faces from the same normal map
        // would bulge the ridges toward the viewer from either side, which is
        // impossible, and the door reads as a printed sticker rather than metal.
        //
        // Tiling comes from the leaf's real size at 1 m a repeat. The ACROSS
        // count is rounded to a whole number so a corrugation is never sliced in
        // half at the door's edge; the vertical is left exact, where the rust
        // streaking has no repeating feature to misalign and the frame covers
        // the ends anyway.
        Material doorFront = M("Mat_Pub_DoorSteel");
        Material doorBack  = M("Mat_Pub_DoorSteelBack");
        float doorLeafW = entW * 0.5f - 0.08f;
        float doorLeafH = entH - 0.06f;
        Vector2 doorTile = new Vector2(
            Mathf.Max(1f, Mathf.Round(doorLeafW / CorrugationTileMetres)),
            doorLeafH / CorrugationTileMetres);
        Tile(doorFront, doorTile);
        Tile(doorBack, doorTile);
        const float skinT = 0.012f;

        // Left door (hinged on left jamb, swung fully open against inner wall)
        Transform doorL = Group(g, "EntranceDoor_L");
        doorL.localPosition = new Vector3(-entW * 0.5f + 0.04f, 0f, frontZ);
        doorL.localEulerAngles = new Vector3(0f, 90f, 0f);
        Box("Frame_L", doorL,
            new Vector3(doorLeafW * 0.5f, doorLeafH * 0.5f + 0.03f, 0f),
            new Vector3(doorLeafW, doorLeafH, 0.05f), doorFront);
        Box("Skin_L", doorL,
            new Vector3(doorLeafW * 0.5f, doorLeafH * 0.5f + 0.03f, 0.025f + skinT * 0.5f),
            new Vector3(doorLeafW, doorLeafH, skinT), doorBack);
        SheetFrame(doorL, new Vector3(doorLeafW * 0.5f, doorLeafH * 0.5f + 0.03f, 0f),
                   doorLeafW, doorLeafH, 0.05f + skinT, M("Mat_Pub_SteelBlue"));

        // Right door (hinged on right jamb, partially open)
        Transform doorR = Group(g, "EntranceDoor_R");
        doorR.localPosition = new Vector3(entW * 0.5f - 0.04f, 0f, frontZ);
        doorR.localEulerAngles = new Vector3(0f, -35f, 0f);
        Box("Frame_R", doorR,
            new Vector3(-doorLeafW * 0.5f, doorLeafH * 0.5f + 0.03f, 0f),
            new Vector3(doorLeafW, doorLeafH, 0.05f), doorFront);
        Box("Skin_R", doorR,
            new Vector3(-doorLeafW * 0.5f, doorLeafH * 0.5f + 0.03f, -(0.025f + skinT * 0.5f)),
            new Vector3(doorLeafW, doorLeafH, skinT), doorBack);
        SheetFrame(doorR, new Vector3(-doorLeafW * 0.5f, doorLeafH * 0.5f + 0.03f, 0f),
                   doorLeafW, doorLeafH, 0.05f + skinT, M("Mat_Pub_SteelBlue"));

        // Stepped gable infill at both ends, following the shallow pitch.
        BuildGable(g, "Gable_Back", HalfD + t * 0.5f, plaster);
        BuildGable(g, "Gable_Front", -HalfD - t * 0.5f, plaster);

        // Painted dado band, offset just inside each wall face.
        float o = 0.02f;
        Box("Dado_Right", g, new Vector3(HalfW - o, DadoHeight * 0.5f, 0f),
            new Vector3(0.03f, DadoHeight, HallDepth), dado);
        Box("Dado_Left", g, new Vector3(-HalfW + o, DadoHeight * 0.5f, 0f),
            new Vector3(0.03f, DadoHeight, HallDepth), dado);
        Box("Dado_Back", g, new Vector3(0f, DadoHeight * 0.5f, HalfD - o),
            new Vector3(HallWidth, DadoHeight, 0.03f), dado);
        // Front dado split around the entrance opening.
        float dadoSegW = HalfW - entW * 0.5f;
        Box("Dado_Front_L", g, new Vector3(-entW * 0.5f - dadoSegW * 0.5f, DadoHeight * 0.5f, -HalfD + o),
            new Vector3(dadoSegW, DadoHeight, 0.03f), dado);
        Box("Dado_Front_R", g, new Vector3(entW * 0.5f + dadoSegW * 0.5f, DadoHeight * 0.5f, -HalfD + o),
            new Vector3(dadoSegW, DadoHeight, 0.03f), dado);

        // Wall pilasters, like the brick piers in reference 3.
        for (int i = 0; i < 6; i++)
        {
            float z = -HalfD + 1.8f + i * 3.3f;
            Box("Pier_L_" + i, g, new Vector3(-HalfW + 0.06f, wallH * 0.5f, z),
                new Vector3(0.12f, wallH, 0.5f), BrickWall(0.5f, wallH));
            Box("Pier_R_" + i, g, new Vector3(HalfW - 0.06f, wallH * 0.5f, z),
                new Vector3(0.12f, wallH, 0.5f), BrickWall(0.5f, wallH));
        }
    }
    static void BuildTruss(Transform parent, string name, float z, Material steel)
    {
        Transform t = Group(parent, name);
        const float th = 0.075f;
        float bot = EavesHeight;

        Member(name + "_BottomChord", t, new Vector2(-HalfW - 0.2f, bot),
               new Vector2(HalfW + 0.2f, bot), z, 0.10f, steel);
        Member(name + "_TopChord_L", t, new Vector2(-HalfW - 0.2f, RoofY(-HalfW - 0.2f)),
               new Vector2(0f, RidgeHeight), z, 0.09f, steel);
        Member(name + "_TopChord_R", t, new Vector2(0f, RidgeHeight),
               new Vector2(HalfW + 0.2f, RoofY(HalfW + 0.2f)), z, 0.09f, steel);

        // Zigzag web, the giveaway detail in the reference photos.
        const int bays = 6;
        float bayW = HallWidth / bays;
        for (int i = 0; i < bays; i++)
        {
            float xa = -HalfW + i * bayW;
            float xb = xa + bayW * 0.5f;
            float xc = xa + bayW;
            Member(name + "_Web_a" + i, t, new Vector2(xa, bot), new Vector2(xb, RoofY(xb)), z, th, steel);
            Member(name + "_Web_b" + i, t, new Vector2(xb, RoofY(xb)), new Vector2(xc, bot), z, th, steel);
        }
        // No end posts: at x = +/-HalfW the top chord already meets the bottom
        // chord, so a post there would be a zero-length (invisible) member.
    }

    // ----------------------------------------------------------- service area

    static void BuildServiceArea(Transform g)
    {
        Material concrete = M("Mat_Pub_ConcreteTable");
        Material teal = M("Mat_Pub_GrilleTeal");
        Material wood = M("Mat_Pub_DarkWood");
        Material cooler = M("Mat_Pub_CoolerRed");
        Material glass = M("Mat_Pub_GlassClear");

        float cz = HalfD - 2.4f;
        float cx = 1.9f;
        float cw = 6.6f;

        const float CounterH = 1.05f;   // working height
        const float TopY = CounterH + 0.045f;

        // The counter is timber now, not cast concrete. Every piece of it --
        // kick, base, top slab, chamfer lip, drinking ledge and the return wing
        // with its own kick -- takes the same material, so the whole unit reads
        // as one built object. The teal ledge brackets stay metal.
        // ---- Counter body ---------------------------------------------------
        // Cast counterWood, with a recessed kick at the floor and an overhanging
        // top slab. The recess is what stops it reading as a plain block: real
        // counters are stood at, so your feet go under the front edge.
        Box("Counter_Kick", g, new Vector3(cx, 0.075f, cz + 0.06f),
            new Vector3(cw - 0.10f, 0.15f, 0.72f), WoodSurface(new Vector3(cw - 0.10f, 0.15f, 0.72f)), true);
        Box("Counter_Base", g, new Vector3(cx, 0.60f, cz),
            new Vector3(cw, 0.90f, 0.85f), WoodSurface(new Vector3(cw, 0.90f, 0.85f)), true);

        // Top slab plus a chamfer strip, so the edge catches the counter light.
        Box("Counter_Top", g, new Vector3(cx, TopY, cz - 0.05f),
            new Vector3(cw + 0.26f, 0.09f, 1.02f), WoodSurface(new Vector3(cw + 0.26f, 0.09f, 1.02f)), true);
        Box("Counter_TopLip", g, new Vector3(cx, TopY - 0.062f, cz - 0.05f),
            new Vector3(cw + 0.20f, 0.035f, 0.96f), WoodSurface(new Vector3(cw + 0.20f, 0.035f, 0.96f)));

        // Customer-side drinking ledge: where glasses actually get put down.
        Box("Counter_Ledge", g, new Vector3(cx, 0.98f, cz - 0.62f),
            new Vector3(cw + 0.10f, 0.05f, 0.26f), WoodSurface(new Vector3(cw + 0.10f, 0.05f, 0.26f)), true);
        for (int i = 0; i < 4; i++)
        {
            Box("Counter_LedgeBracket_" + i, g,
                new Vector3(cx - cw * 0.5f + 0.6f + i * (cw - 1.2f) / 3f, 0.86f, cz - 0.60f),
                new Vector3(0.06f, 0.20f, 0.20f), teal);
        }

        // Return wing at the left end.
        //
        // The wing sits at y 0.15 .. 1.05, the same recess as the main counter,
        // but the main counter has Counter_Kick filling that 0.15 m and this had
        // nothing -- so the whole wing hung in the air with daylight under it.
        // Its kick runs back to z -0.30 to meet the main counter's, and is inset
        // 0.05 on each exposed side so the recess reads the same as the rest.
        Box("Counter_ReturnKick", g,
            new Vector3(cx - cw * 0.5f + 0.42f, 0.075f, cz - 1.225f),
            new Vector3(0.75f, 0.15f, 1.85f), WoodSurface(new Vector3(0.75f, 0.15f, 1.85f)), true);
        Box("Counter_Return", g, new Vector3(cx - cw * 0.5f + 0.42f, 0.60f, cz - 1.3f),
            new Vector3(0.85f, 0.90f, 1.8f), WoodSurface(new Vector3(0.85f, 0.90f, 1.8f)), true);
        Box("Counter_ReturnTop", g, new Vector3(cx - cw * 0.5f + 0.42f, TopY, cz - 1.3f),
            new Vector3(0.95f, 0.09f, 1.9f), WoodSurface(new Vector3(0.95f, 0.09f, 1.9f)), true);

        // ---- Security cage ----------------------------------------------------
        // The defining feature of a TASMAC counter: heavy mesh from the counter
        // top to the roof, with one small hatch. Stock is behind the cage and
        // handed out through the gap. The old version was a waist-high rail,
        // which read as a shop display rather than a caged service window.
        Transform cage = Group(g, "ServiceCage");

        float cageBottom = TopY + 0.045f;
        float cageTop = EavesHeight - 0.10f;
        float cageH = cageTop - cageBottom;
        float cageZ = cz - 0.05f;
        float cageW = cw + 0.26f;

        // Hatch: a gap in the bars, wide enough to pass bottles through.
        const float HatchW = 0.86f;
        const float HatchTop = 0.62f;      // above the counter top
        float hatchMinX = cx - HatchW * 0.5f;
        float hatchMaxX = cx + HatchW * 0.5f;

        // Frame.
        Box("Cage_Head", cage, new Vector3(cx, cageTop, cageZ),
            new Vector3(cageW, 0.10f, 0.10f), teal);
        for (int i = 0; i < 4; i++)
        {
            Box("Cage_Post_" + i, cage,
                new Vector3(cx - cageW * 0.5f + i * (cageW / 3f), cageBottom + cageH * 0.5f, cageZ),
                new Vector3(0.09f, cageH, 0.09f), teal);
        }

        // Vertical bars, skipped across the hatch.
        int bars = Mathf.RoundToInt(cageW / 0.26f);
        for (int i = 0; i <= bars; i++)
        {
            float bx = cx - cageW * 0.5f + i * (cageW / bars);
            bool overHatch = bx > hatchMinX - 0.05f && bx < hatchMaxX + 0.05f;

            float barBottom = overHatch ? cageBottom + HatchTop : cageBottom;
            float barH = cageTop - barBottom;
            if (barH <= 0.05f) continue;

            Box("Cage_Bar_" + i, cage, new Vector3(bx, barBottom + barH * 0.5f, cageZ),
                new Vector3(0.035f, barH, 0.035f), teal);
        }

        // Horizontal rails, also broken by the hatch.
        int rails = 7;
        for (int i = 0; i < rails; i++)
        {
            float ry = cageBottom + (i + 0.5f) * (cageH / rails);
            bool throughHatch = ry < cageBottom + HatchTop;

            if (!throughHatch)
            {
                Box("Cage_Rail_" + i, cage, new Vector3(cx, ry, cageZ),
                    new Vector3(cageW, 0.032f, 0.032f), teal);
                continue;
            }

            float leftW = (hatchMinX - (cx - cageW * 0.5f));
            float rightW = ((cx + cageW * 0.5f) - hatchMaxX);
            Box("Cage_RailL_" + i, cage,
                new Vector3(cx - cageW * 0.5f + leftW * 0.5f, ry, cageZ),
                new Vector3(leftW, 0.032f, 0.032f), teal);
            Box("Cage_RailR_" + i, cage,
                new Vector3(hatchMaxX + rightW * 0.5f, ry, cageZ),
                new Vector3(rightW, 0.032f, 0.032f), teal);
        }

        // Hatch surround and its pass-through shelf.
        Box("Hatch_Lintel", cage, new Vector3(cx, cageBottom + HatchTop, cageZ),
            new Vector3(HatchW + 0.16f, 0.07f, 0.09f), teal);
        Box("Hatch_JambL", cage, new Vector3(hatchMinX - 0.045f, cageBottom + HatchTop * 0.5f, cageZ),
            new Vector3(0.06f, HatchTop, 0.09f), teal);
        Box("Hatch_JambR", cage, new Vector3(hatchMaxX + 0.045f, cageBottom + HatchTop * 0.5f, cageZ),
            new Vector3(0.06f, HatchTop, 0.09f), teal);
        Box("Hatch_Shelf", cage, new Vector3(cx, cageBottom + 0.02f, cageZ - 0.09f),
            new Vector3(HatchW + 0.10f, 0.04f, 0.30f), concrete, true);

        // Open bottle shelving against the back wall.
        Transform shelf = Group(g, "BackShelving");
        float sx = -0.6f, sz = HalfD - 0.45f, sw = 4.4f;
        Box("Shelf_Back", shelf, new Vector3(sx, 1.15f, sz + 0.16f), new Vector3(sw, 2.3f, 0.06f), WoodSurface(new Vector3(sw, 2.3f, 0.06f)));
        Box("Shelf_Side_L", shelf, new Vector3(sx - sw * 0.5f, 1.15f, sz), new Vector3(0.06f, 2.3f, 0.36f), WoodSurface(new Vector3(0.06f, 2.3f, 0.36f)));
        Box("Shelf_Side_R", shelf, new Vector3(sx + sw * 0.5f, 1.15f, sz), new Vector3(0.06f, 2.3f, 0.36f), WoodSurface(new Vector3(0.06f, 2.3f, 0.36f)));
        for (int i = 0; i < 5; i++)
        {
            Box("Shelf_Deck_" + i, shelf, new Vector3(sx, 0.35f + i * 0.48f, sz),
                new Vector3(sw, 0.05f, 0.36f), WoodSurface(new Vector3(sw, 0.05f, 0.36f)));
        }

        // Bottles on the shelves.
        Transform stock = Group(g, "BottleStock");
        string[] bottleMats = { "Mat_Pub_GlassAmber", "Mat_Pub_GlassGreen", "Mat_Pub_GlassAmber" };
        int n = 0;
        for (int deck = 1; deck < 5; deck++)
        {
            float y = 0.35f + deck * 0.48f + 0.025f;
            for (int i = 0; i < 5; i++)
            {
                float bx = sx - sw * 0.5f + 0.45f + i * 0.85f;
                Bottle(stock, "Bottle_" + (n++), new Vector3(bx, y, sz),
                       M(bottleMats[RandInt(0, bottleMats.Length)]), 1f);
            }
        }

        // Beverage coolers, generic red - no branding reproduced.
        //
        // Only the right-hand one is a built cooler now; the left was replaced
        // by Asset_DrinksShelf, a scanned supermarket drinks shelf.
        Transform cool = Group(g, "Coolers");
        {
            float bx = 5.45f;
            float bz = HalfD - 0.62f;
            Box("Cooler_1_Body", cool, new Vector3(bx, 0.72f, bz), new Vector3(1.05f, 1.45f, 0.65f), cooler, true);
            Box("Cooler_1_Header", cool, new Vector3(bx, 1.56f, bz), new Vector3(1.05f, 0.24f, 0.65f), cooler);
            Prim(PrimitiveType.Cube, "Cooler_1_Glass", cool,
                 new Vector3(bx, 0.78f, bz - 0.34f), new Vector3(0.88f, 1.1f, 0.03f), glass, Vector3.zero, false);
        }

        // Scanned drinks shelf in place of the left cooler.
        //
        // It is 1.81 m wide against the cooler's 1.05, so it cannot simply drop
        // into the same spot -- it would run through the remaining cooler. It
        // extends LEFT instead: spanning x 3.02 .. 4.83 it clears the cooler's
        // left face at 4.925 with a 0.10 m gap, and stops well short of
        // BackShelving, which ends at x 1.6.
        //
        // Yaw 180, and the reasoning that said 0 was wrong. The render showed
        // the stocked face on Blender -Y, which I mapped to Unity -Z. The FBX
        // axis conversion flips it: Blender -Y arrives as Unity +Z, so the
        // shelf went in with its back panel to the room.
        //
        // It also stands on a plinth. The model's origin is the centre of its
        // base, so the plinth height is simply the shelf's Y and the two stack
        // without any fudge factor. Plinth 0.40 + shelf 1.235 = 1.635 overall,
        // which sits just under the neighbouring cooler's 1.68 and reads as a
        // deliberate pair rather than a mismatch.
        {
            float dsx = 3.92f;
            float dsz = HalfD - 0.48f;
            const float plinthH = 0.40f;
            const float shelfH = 1.235f;

            Box("DrinksShelf_Plinth", cool,
                new Vector3(dsx, plinthH * 0.5f, dsz),
                new Vector3(1.90f, plinthH, 0.46f), M("Mat_Pub_ConcreteTable"), true);

            GameObject shelfUnit = Model("Asset_DrinksShelf", cool,
                                         new Vector3(dsx, plinthH, dsz), 180f,
                                         M("Mat_Pub_DrinksShelf"));
            if (shelfUnit != null)
            {
                shelfUnit.name = "DrinksShelf";
                // Scenery, but solid: a shelf you can walk through reads as a
                // hologram. The primitive coolers get theirs from Box(keepCollider).
                BoxCollider bc = shelfUnit.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.81f, shelfH, 0.37f);
                bc.center = new Vector3(0f, shelfH * 0.5f, 0f);
            }
            else
            {
                Box("Cooler_0_Body", cool, new Vector3(4.3f, 0.72f, HalfD - 0.62f),
                    new Vector3(1.05f, 1.45f, 0.65f), cooler, true);
            }
        }

        // (Back doorway removed — solid wall now.)
    }

    static void Bottle(Transform parent, string name, Vector3 basePos, Material mat, float scale,
                       bool grabbable = false)
    {
        Transform b = Group(parent, name);
        b.localPosition = basePos;

        // Pick a "brand": silhouette plus a label/foil/cap colourway. Bottles
        // on tables are what a patient actually reaches for, so variety here
        // matters more than anywhere else in the scene.
        BottleBrand brand = PickBrand(grabbable);

        GameObject model = Model(brand.Model, b, Vector3.zero, Rand(0f, 360f),
                                 brand.Glass ?? mat, isStatic: !grabbable,
                                 slotMaterials: new[]
                                 {
                                     brand.Glass ?? mat,
                                     brand.Label,
                                     brand.Foil,
                                     brand.Cap,
                                     brand.Liquid ?? brand.Glass,
                                 });

        if (model == null)
        {
            Tube(name + "_Body", b, new Vector3(0f, 0.115f * scale, 0f), 0.075f * scale, 0.23f * scale, mat, Vector3.zero);
            Tube(name + "_Neck", b, new Vector3(0f, 0.285f * scale, 0f), 0.03f * scale, 0.11f * scale, mat, Vector3.zero);
        }

        // Shelf bottles behind the counter stay static -- they are scenery, and
        // 20 more rigidbodies would cost more than they add.
        if (grabbable)
            GrabbableUpright(b, 0.042f, 0.302f, 0.6f);
    }

    // --------------------------------------------------------------- brands

    struct BottleBrand
    {
        public string Model;
        public Material Glass;
        public Material Label;
        public Material Foil;
        public Material Cap;
        public Material Liquid;
    }

    /// <summary>
    /// A FIXED set of bottle brands: silhouette plus colourway.
    ///
    /// Fixed, not randomised per-slot. Randomising each slot independently gave
    /// nearly every bottle a unique material combination, and unique
    /// combinations cannot be batched or instanced -- that alone cost ~7 fps.
    /// A handful of shared brands looks just as varied and draws far cheaper.
    ///
    /// No real trademarks are reproduced; these read as familiar shelf products
    /// at VR distance without copying actual label artwork.
    /// </summary>
    static BottleBrand[] _brands;
    static BottleBrand[] _tableBrands;

    static void BuildBrandTable()
    {
        _brands = new[]
        {
            // Beer: what tables are actually covered in.
            new BottleBrand { Model = "Asset_BeerBottle",  Glass = M("Mat_Pub_GlassGreen"),
                              Label = M("Mat_Pub_LabelRed"),   Foil = M("Mat_Pub_FoilGold"),
                              Cap = M("Mat_Pub_CapRed") },
            new BottleBrand { Model = "Asset_BeerBottle",  Glass = M("Mat_Pub_GlassGreen"),
                              Label = M("Mat_Pub_LabelWhite"), Foil = M("Mat_Pub_FoilGold"),
                              Cap = M("Mat_Pub_CapRed") },
            new BottleBrand { Model = "Asset_BeerBottle",  Glass = M("Mat_Pub_GlassAmber"),
                              Label = M("Mat_Pub_LabelCream"), Foil = M("Mat_Pub_FoilSilver"),
                              Cap = M("Mat_Pub_CapGold") },

            // Spirits: counter shelf range.
            new BottleBrand { Model = "Asset_WhiskyQuart", Glass = M("Mat_Pub_GlassAmber"),
                              Label = M("Mat_Pub_LabelGold"),  Foil = M("Mat_Pub_FoilGold"),
                              Cap = M("Mat_Pub_CapGold") },
            new BottleBrand { Model = "Asset_WhiskyQuart", Glass = M("Mat_Pub_GlassClear"),
                              Label = M("Mat_Pub_LabelBlue"),  Foil = M("Mat_Pub_FoilSilver"),
                              Cap = M("Mat_Pub_CapGold") },
            new BottleBrand { Model = "Asset_BrandyHalf",  Glass = M("Mat_Pub_GlassAmber"),
                              Label = M("Mat_Pub_LabelGreen"), Foil = M("Mat_Pub_FoilGold"),
                              Cap = M("Mat_Pub_CapRed") },

            // Premium silhouettes for the shelf: fluted, square and tall, with
            // visible contents through clear glass.
            new BottleBrand { Model = "Asset_BottleFluted", Glass = M("Mat_Pub_GlassClear"),
                              Label = M("Mat_Pub_LabelCream"), Foil = M("Mat_Pub_FoilGold"),
                              Cap = M("Mat_Pub_CapGold"), Liquid = M("Mat_Pub_LiquidBlue") },
            new BottleBrand { Model = "Asset_BottleSquare", Glass = M("Mat_Pub_GlassClear"),
                              Label = M("Mat_Pub_LabelGold"), Foil = M("Mat_Pub_FoilSilver"),
                              Cap = M("Mat_Pub_CapBlack"), Liquid = M("Mat_Pub_LiquidDark") },
            new BottleBrand { Model = "Asset_BottleTall",   Glass = M("Mat_Pub_GlassClear"),
                              Label = M("Mat_Pub_LabelWhite"), Foil = M("Mat_Pub_FoilGold"),
                              Cap = M("Mat_Pub_CapGold"), Liquid = M("Mat_Pub_LiquidRed") },
            new BottleBrand { Model = "Asset_BottleTall",   Glass = M("Mat_Pub_GlassClear"),
                              Label = M("Mat_Pub_LabelBlue"), Foil = M("Mat_Pub_FoilSilver"),
                              Cap = M("Mat_Pub_CapGold"), Liquid = M("Mat_Pub_LiquidAmber") },
        };

        // Indices 0-2 are the beer brands.
        _tableBrands = new[] { _brands[0], _brands[1], _brands[2] };
    }

    static BottleBrand PickBrand(bool onTable)
    {
        if (_brands == null)
            BuildBrandTable();

        // Tables are mostly beer, as in the reference photographs.
        if (onTable && Rand(0f, 1f) < 0.70f)
            return _tableBrands[RandInt(0, _tableBrands.Length)];

        return _brands[RandInt(0, _brands.Length)];
    }

    // ------------------------------------------------------------ grabbables

    /// <summary>
    /// Makes a procedurally-built group pickable in VR: one approximating
    /// collider on the root, a Rigidbody, and an XRGrabInteractable.
    ///
    /// Static flags must be cleared -- Unity batches static geometry into the
    /// combined mesh, and a batched object cannot move at runtime.
    ///
    /// A single root collider (rather than per-part colliders) is deliberate:
    /// it is far cheaper for physics and accurate enough for bottles, glasses
    /// and chairs.
    /// </summary>
    static void MakeGrabbable(Transform group, Collider shape, float mass)
    {
        GameObject go = group.gameObject;

        // Clear static flags on the whole hierarchy or it will not move.
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // ContinuousDynamic on 100+ bodies is expensive and, for objects this
        // size at hand speeds, unnecessary. ContinuousSpeculative is stable and
        // much cheaper, and it also settles resting contacts more quietly.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;   // stops overlaps flinging objects

        var grab = go.AddComponent<
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Kinematic, not VelocityTracking. VelocityTracking drives the body by
        // applying velocity each physics step, so a held object fights the
        // solver against the floor, the table, the player capsule and the
        // other 100 rigidbodies -- which reads as constant twitching.
        // Kinematic pins it to the hand while held; throwOnDetach restores
        // dynamics and carries release velocity, so throwing still works.
        grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables
                            .XRBaseInteractable.MovementType.Kinematic;
        grab.useDynamicAttach = true;   // grab where you actually touched it
        grab.throwOnDetach = true;      // release velocity carries into the throw

        // Smoothing on top of a kinematic follow adds lag without adding
        // stability, so it stays off.
        grab.smoothPosition = false;
        grab.smoothRotation = false;

        _grabbableCount++;
    }

    /// <summary>Adds a capsule collider sized for an upright bottle/glass.</summary>
    /// <summary>
    /// Collider and physics for anything meant to STAND on a surface: bottles,
    /// glasses, tumblers.
    ///
    /// WHY THIS IS A BOX AND NOT A CAPSULE
    /// -----------------------------------
    /// It used to be a CapsuleCollider, and a capsule has HEMISPHERICAL ENDS.
    /// Every bottle and glass was therefore balanced on a curved base and could
    /// not rest -- put one down and it tipped and rolled off, every time. The
    /// tumbler and glass were worse still: their height (0.090, 0.100) was
    /// barely above 2 x radius (0.076, 0.080), which Unity renders as very
    /// nearly a sphere. They were, in effect, marbles.
    ///
    /// A box gives a flat base to rest on and flat sides to stack against. The
    /// lost realism is that a knocked-over bottle no longer rolls, which is a
    /// far smaller problem than not being able to set a drink down.
    /// </summary>
    static void GrabbableUpright(Transform group, float radius, float height, float mass)
    {
        BoxCollider bc = group.gameObject.AddComponent<BoxCollider>();
        float side = radius * 2f;
        bc.size = new Vector3(side, height, side);
        bc.center = new Vector3(0f, height * 0.5f, 0f);

        MakeGrabbable(group, bc, mass);

        // A small push-out AFTER the grab, which costs no grab freedom at all.
        // See GrabPushOut.cs -- and the note below for what not to do instead.
        group.gameObject.AddComponent<GrabPushOut>();

        // NO fixed grip point here.
        //
        // A grip transform with dynamic attach off was tried, to stop bottles
        // intersecting the hand. It made the intersection tidier and broke the
        // grabbing: snapping every object to one pose fights how you actually
        // reach for things, and a worse grab is a bad trade for a better
        // looking one. Dynamic attach stays.
        //
        // Doing this properly needs per-object hand POSES, not attach points --
        // the fingers have to close differently on a tumbler than on a bottle
        // neck. That is a system this project does not have, and the usual
        // implementations are vendor-specific, which the OpenXR-only rule rules
        // out. Left alone deliberately rather than half-solved.

        // Glass clink on pickup. Attached HERE and not in MakeGrabbable, so it
        // covers the drinkware and not the chairs -- a chair should not chime.
        group.gameObject.AddComponent<GrabClink>().Clip = _clinkClip;

        Rigidbody rb = group.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Mass sits low, the way liquid sits in the bottom of a bottle, so
            // a nudge rocks the object back upright instead of toppling it.
            // Unity's default puts it at the collider centre -- half way up a
            // 30 cm bottle, which is about as tippy as it could be.
            rb.centerOfMass = new Vector3(0f, height * 0.28f, 0f);

            // A release always imparts some spin. Undamped, that spin carries a
            // bottle off the table on its own. This bleeds it off in well under
            // a second without making the object feel like it is in treacle.
            rb.angularDamping = 4f;
        }
    }

    /// <summary>Adds a box collider roughly enclosing a chair.</summary>
    static void GrabbableChair(Transform group)
    {
        // A single box for the whole chair reached 0.90 m high while chairs
        // tuck 0.78-0.94 m from a table centre, so the box overlapped the
        // table and stood proud of its 0.775 m top. Bottles then rested on an
        // invisible slab above the table. Three boxes follow the real shape:
        // the back is thin, so nothing solid sits over the table surface.
        GameObject go = group.gameObject;

        // Sized to the plastic monobloc chair: 0.642 wide x 0.628 deep x 0.880
        // tall, seat at 0.445. It is much WIDER than the chair it replaced
        // because it has arms -- the old boxes were 0.42 x 0.41 and would have
        // left the armrests with no collision at all, so a hand would pass
        // through the most obvious thing to grab.
        BoxCollider legs = go.AddComponent<BoxCollider>();
        legs.center = new Vector3(0f, 0.215f, 0f);
        legs.size = new Vector3(0.58f, 0.430f, 0.56f);

        // Seat is deliberately thicker than the visible slab: a 55 mm target is
        // very hard to hit with a tracked hand. Widened to take in the arms.
        BoxCollider seat = go.AddComponent<BoxCollider>();
        seat.center = new Vector3(0f, 0.455f, 0.02f);
        seat.size = new Vector3(0.64f, 0.150f, 0.58f);

        // Back at -Z. Blender +Y maps to Unity -Z under this project's export
        // contract, and plasticchair.py checks the backrest really is at +Y
        // before exporting -- the yaw = -angle placement depends on it.
        BoxCollider back = go.AddComponent<BoxCollider>();
        back.center = new Vector3(0f, 0.680f, -0.255f);
        back.size = new Vector3(0.58f, 0.420f, 0.150f);

        // 3.2 kg, not 4.5: this is moulded plastic, and the weight is what a
        // patient feels when they pick it up and put it down.
        MakeGrabbable(group, seat, 3.2f);
    }

    /// <summary>
    /// Collision for a round table.
    ///
    /// A box cannot describe a disc: sized to the diameter its corners stand
    /// out past the edge and glasses rest on thin air, and inscribed in the
    /// circle it stops short and they fall through the rim. Approximating with
    /// rotated boxes would take several per table.
    ///
    /// Tables are STATIC, and a static MeshCollider does not need to be convex
    /// -- so the mesh itself becomes the collider, exactly matching what the
    /// eye sees, for 1,242 triangles in a BVH that is never rebuilt.
    /// </summary>
    static void AddTableCollider(GameObject model)
    {
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null)
                continue;
            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }
    }

    /// <summary>
    /// A point on a round table top, at least <paramref name="minGap"/> from
    /// anything already placed there.
    ///
    /// Uniform over the DISC, which needs sqrt: sampling radius linearly would
    /// crowd everything into the middle, because a ring's area grows with r.
    ///
    /// The spacing check matters as much as the radius. Two props spawned
    /// overlapping are resolved by the physics solver on the first step, and it
    /// resolves them by shoving one of them off the table.
    /// </summary>
    static Vector3 OnTable(List<Vector2> placed, float usableRadius, float minGap)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float a = Rand(0f, Mathf.PI * 2f);
            float r = usableRadius * Mathf.Sqrt(Rand(0f, 1f));
            Vector2 p = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);

            bool clear = true;
            for (int i = 0; i < placed.Count; i++)
            {
                if (Vector2.Distance(placed[i], p) < minGap) { clear = false; break; }
            }
            if (!clear && attempt < 23)
                continue;

            placed.Add(p);
            return new Vector3(p.x, 0f, p.y);
        }
        return Vector3.zero;
    }

    /// <summary>
    /// A hanging cable between two points, as a chain of short boxes.
    ///
    /// The sag is parabolic: offset = 4 * sag * t * (1 - t), which is zero at
    /// both ends and deepest in the middle. A real catenary is a cosh curve,
    /// but over a span this short the two are indistinguishable and this needs
    /// no solving for a shape parameter.
    /// </summary>
    static void SagWire(Transform parent, string name, Vector3 from, Vector3 to,
                        float sag, Material mat)
    {
        const int Segments = 10;
        const float thickness = 0.022f;

        Transform g = Group(parent, name);
        Vector3 prev = from;

        for (int i = 1; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y -= 4f * sag * t * (1f - t);

            Vector3 mid = (prev + p) * 0.5f;
            Vector3 d = p - prev;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = name + "_" + i;
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.transform.SetParent(g, false);
            seg.transform.localPosition = mid;
            seg.transform.localRotation = Quaternion.LookRotation(d.normalized);
            seg.transform.localScale = new Vector3(thickness, thickness, d.magnitude);
            seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
            _objectCount++;

            prev = p;
        }
    }

    /// <summary>
    /// Loudness of a clip over time, as one value per 1/hz second.
    ///
    /// Measured HERE, at build time, rather than in the player: reading a whole
    /// clip's samples is far too slow to do on device, and the result never
    /// changes. The runtime then only has to index an array.
    ///
    /// Normalised to its own peak so the flicker uses the full range whatever
    /// the recording's level, and shaped with a square root because loudness is
    /// perceived closer to amplitude than to energy -- without it the quieter
    /// ticks vanish and only the two loudest pops show as light.
    /// </summary>
    static float[] BuildEnvelope(AudioClip clip, float hz)
    {
        if (clip == null)
        {
            Debug.LogWarning("[PubEnvironment] No flicker clip; light stays steady.");
            return new float[0];
        }

        float[] samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0))
        {
            Debug.LogWarning("[PubEnvironment] Could not read " + clip.name +
                             "; light stays steady.");
            return new float[0];
        }

        int perFrame = Mathf.Max(1, Mathf.RoundToInt(clip.frequency / hz)) * clip.channels;
        int frames = Mathf.Max(1, samples.Length / perFrame);
        float[] env = new float[frames];

        float peak = 0f;
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            int start = f * perFrame;
            int end = Mathf.Min(start + perFrame, samples.Length);
            for (int i = start; i < end; i++)
                sum += samples[i] * samples[i];

            float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, end - start)));
            env[f] = rms;
            if (rms > peak) peak = rms;
        }

        if (peak > 1e-6f)
            for (int f = 0; f < frames; f++)
                env[f] = Mathf.Sqrt(Mathf.Clamp01(env[f] / peak));

        Debug.Log($"[PubEnvironment] Flicker envelope: {frames} frames at {hz} Hz " +
                  $"({clip.length:F2}s), peak rms {peak:F4}.");
        return env;
    }

    static int _grabbableCount;

    // ---------------------------------------------------------------- seating

    static void BuildSeating(Transform g)
    {
        Material tableMat = M("Mat_Pub_ConcreteTable");
        Material[] chairMats =
        {
            M("Mat_Pub_ChairRed"), M("Mat_Pub_ChairBlue"), M("Mat_Pub_ChairGreen"),
            M("Mat_Pub_ChairRed"), M("Mat_Pub_ChairBlue")
        };

        // Kept clear of the walls so chairs don't intersect the water-case stacks.
        float usableW = HallWidth - 3.8f;
        float usableD = HallDepth - 7.5f;
        float stepX = usableW / TableCols;
        float stepZ = usableD / TableRows;

        int index = 0;
        for (int r = 0; r < TableRows; r++)
        {
            for (int c = 0; c < TableCols; c++)
            {
                float x = -usableW * 0.5f + stepX * (c + 0.5f) + Rand(-0.18f, 0.18f);
                float z = -HalfD + 2.2f + stepZ * (r + 0.5f) + Rand(-0.22f, 0.22f);

                // Skip any grid table that would crowd the hero table.
                //
                // The scenario drops its table at a fixed (-2.6, 1.4), which the
                // grid knows nothing about -- and the grid puts tables 1.39 m
                // and 1.74 m away. Two tables need about 2.3 m between centres
                // once you allow for a 0.367 radius and chairs reaching 1.13 m
                // out, so the hero table was wedged between two neighbours with
                // the patient's chair stranded in the gap.
                //
                // Skipping the whole unit rather than just its chairs also
                // takes its clutter with it, because clutter is placed from
                // _tableTops and the entry is never added.
                float heroGap = Vector2.Distance(new Vector2(x, z),
                    new Vector2(PubScenarioBuilder.TableX, PubScenarioBuilder.TableZ));
                if (heroGap < 2.4f)
                {
                    Debug.Log($"[PubEnvironment] Grid table at ({x:F2}, {z:F2}) " +
                              $"skipped: {heroGap:F2} m from the hero table.");
                    index++;
                    continue;
                }

                Transform unit = Group(g, "TableUnit_" + index.ToString("00"));
                unit.localPosition = new Vector3(x, 0f, z);
                unit.localEulerAngles = new Vector3(0f, Rand(-8f, 8f), 0f);

                ConcreteTable(unit, tableMat);
                _tableTops.Add(new Vector3(x, 0.775f, z));  // top surface (matches Asset_ConcreteTable)

                for (int s = 0; s < ChairsPerTable; s++)
                {
                    // Rolled before anything else is computed, so skipping a
                    // chair costs nothing and the random sequence stays stable.
                    if (Rand(0f, 1f) > ChairKeepChance)
                        continue;

                    float ang = 90f * s + Rand(-16f, 16f);
                    // Clearance maths, not taste. The table is a DISC of radius
                    // 0.367 and the plastic chair is 0.628 DEEP (half-depth
                    // 0.314) -- nearly half again the 0.41 of the chair it
                    // replaced, because of the arms. Contact distance is
                    // therefore 0.367 + 0.314 = 0.681, and the previous
                    // 0.70-0.78 left as little as 19 mm: close enough that a
                    // chair's random yaw would have driven it into the table
                    // and physics would have flung it, exactly as happened
                    // before with the rectangular tables.
                    //
                    // 0.74-0.82 puts the chair's front edge 60-140 mm off the
                    // rim, which is where a chair actually sits at a pedestal
                    // table -- you cannot push one right under.
                    float dist = Rand(0.74f, 0.82f);
                    float rad = ang * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(Mathf.Sin(rad) * dist, 0f, -Mathf.Cos(rad) * dist);
                    // Yaw must be -ang, not ang+180.
                    //
                    // A chair faces +Z at yaw 0 (its back is at -Z). It sits at
                    // (sin a, 0, -cos a)*d, so the direction to the table is
                    // (-sin a, 0, cos a). Solving (sin y, 0, cos y) for that
                    // gives y = -a. The old ang+180 only coincides with -a at
                    // a = 90 deg, which is why exactly one chair per table
                    // looked right and the rest faced away.
                    // Skip chairs that would sit on top of the scenario table.
                    // Grid chairs sit ~1.0 m out, and TableUnit_09 is only
                    // 1.53 m from the hero table, so its chairs reached into
                    // the hero tabletop.
                    Vector3 worldChair = unit.TransformPoint(p);
                    Vector2 flatChair = new Vector2(worldChair.x, worldChair.z);
                    Vector2 heroFlat = new Vector2(PubScenarioBuilder.TableX,
                                                   PubScenarioBuilder.TableZ);
                    if (Vector2.Distance(flatChair, heroFlat) < 1.45f)
                        continue;

                    Chair(unit, "Chair_" + s, p, -ang + Rand(-14f, 14f),
                          chairMats[RandInt(0, chairMats.Length)]);
                }
                index++;
            }
        }

        // A stack of spare chairs in the corner, as in the references.
        Transform stack = Group(g, "ChairStack");
        stack.localPosition = new Vector3(HalfW - 1.0f, 0f, -HalfD + 1.3f);
        for (int i = 0; i < 4; i++)
        {
            // 0.09 m of rise per chair, not 0.26. Monobloc chairs nest into one
            // another -- that is the point of the shape -- so a real stack gains
            // less than a hand's width per chair. At 0.26 they stood apart like
            // a ladder, which is what the pile in the corner looked like.
            Chair(stack, "Stacked_" + i, new Vector3(Rand(-0.03f, 0.03f), 0.02f + i * 0.09f, Rand(-0.03f, 0.03f)),
                  Rand(-10f, 10f), chairMats[RandInt(0, chairMats.Length)], grabbable: false);
        }
    }

    /// <summary>Radius of the round table top. Chair spacing, the containment
    /// lip and the hero table's prop layout are all derived from this.</summary>
    public const float TableRadius = 0.367f;

    static void ConcreteTable(Transform parent, Material mat)
    {
        GameObject model = Model("Asset_RoundTable", parent, Vector3.zero, 0f,
                                 M("Mat_Pub_TableWood") ?? mat);

        if (model != null)
        {
            AddTableCollider(model);
            return;
        }

        Box("Table_Top", parent, new Vector3(0f, 0.755f, 0f), new Vector3(1.15f, 0.09f, 0.80f), mat, true);
        Box("Table_Leg_L", parent, new Vector3(-0.40f, 0.355f, 0f), new Vector3(0.14f, 0.71f, 0.64f), mat, true);
        Box("Table_Leg_R", parent, new Vector3(0.40f, 0.355f, 0f), new Vector3(0.14f, 0.71f, 0.64f), mat, true);
    }

    static void Chair(Transform parent, string name, Vector3 pos, float yaw, Material mat,
                      bool grabbable = true)
    {
        Transform c = Group(parent, name);
        c.localPosition = pos;
        c.localEulerAngles = new Vector3(0f, yaw, 0f);

        GameObject model = Model("Asset_PlasticChair", c, Vector3.zero, 0f, mat,
                                 isStatic: false);

        if (model == null)
        {
            Box(name + "_Seat", c, new Vector3(0f, 0.445f, 0f), new Vector3(0.44f, 0.05f, 0.44f), mat);
            BoxR(name + "_Back", c, new Vector3(0f, 0.70f, -0.205f), new Vector3(0.42f, 0.46f, 0.045f), mat,
                 new Vector3(-9f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float lx = (i < 2) ? -0.175f : 0.175f;
                float lz = (i % 2 == 0) ? -0.175f : 0.175f;
                Box(name + "_Leg" + i, c, new Vector3(lx, 0.21f, lz), new Vector3(0.038f, 0.42f, 0.038f), mat);
            }
        }

        // Stacked spare chairs are scenery: four chairs 0.26 m apart with
        // 0.9 m colliders intersect by design, and as rigidbodies they explode
        // apart the moment physics starts.
        if (grabbable)
            GrabbableChair(c);
    }

    // --------------------------------------------------------------- fixtures

    static void BuildFixtures(Transform g)
    {
        Material steel = M("Mat_Pub_SteelBlue");
        Material blade = M("Mat_Pub_FanBlade");
        Material enamel = M("Mat_Pub_Enamel");
        Material bulb = M("Mat_Pub_Bulb");
        Material tube = M("Mat_Pub_Tube");
        Material metal = M("Mat_Pub_Metal");

        // Ceiling fans hung off the trusses.
        Transform fans = Group(g, "Fans");
        for (int i = 0; i < TrussCount; i++)
        {
            float x = (i % 2 == 0) ? -3.0f : 3.0f;
            Fan(fans, "Fan_" + i, new Vector3(x, EavesHeight, TrussZ(i)),
                steel, blade, Rand(0f, 90f));
        }

        // Pendants down the centre, ONE PER TRUSS.
        //
        // They used to be spaced 4.3 m on their own arithmetic, which aligned
        // with nothing: their rods stopped at EavesHeight (3.8) while the roof
        // directly above them is the RIDGE at 4.5, so every one hung in a 0.7 m
        // gap. Sitting them on TrussZ(i) puts a steel bottom chord at exactly
        // the height they mount at, so they hang off structure that is there.
        Transform pend = Group(g, "Pendants");
        Material lampBody = M("Mat_Pub_BarLamp");
        for (int i = 0; i < TrussCount; i++)
        {
            Transform p = Group(pend, "Pendant_" + i);
            p.localPosition = new Vector3(0f, 0f, TrussZ(i));

            // Asset_BarLamp's origin is the top of its stem, so mounting it at
            // the chord height needs no offset. Slot 1 is its bulb, driven by
            // the emissive bulb material rather than a painted highlight.
            // Yaw 0, not a random angle. The lamp is radially symmetric so a
            // random yaw changes nothing visible -- but it draws from the shared
            // seeded _rng, which shifts every later draw in the build and
            // silently re-rolls chair placement, clutter and bottle brands. It
            // moved the grabbable count from 44 to 47 before this was pinned.
            GameObject lamp = Model("Asset_BarLamp", p,
                                    new Vector3(0f, EavesHeight, 0f), 0f,
                                    lampBody, slotMaterials: new[] { lampBody, bulb });
            if (lamp == null)
            {
                Tube("Rod", p, new Vector3(0f, EavesHeight - 0.42f, 0f), 0.035f, 0.85f, metal, Vector3.zero);
                Tube("Shade", p, new Vector3(0f, EavesHeight - 0.95f, 0f), 0.40f, 0.20f, enamel, Vector3.zero);
                Tube("Bulb", p, new Vector3(0f, EavesHeight - 1.06f, 0f), 0.13f, 0.10f, bulb, Vector3.zero);
            }
            _pendantBulbs.Add(new Vector3(0f, EavesHeight - 1.14f, TrussZ(i)));
        }

        // Exposed conduit and pipe runs.
        Transform cond = Group(g, "Conduit");
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Tube("Conduit_H_" + i, cond, new Vector3(side * (HalfW - 0.08f), 3.05f, 0f),
                 0.05f, HallDepth - 1f, metal, new Vector3(90f, 0f, 0f));
            for (int d = 0; d < 3; d++)
            {
                Tube("Conduit_V_" + i + "_" + d, cond,
                     new Vector3(side * (HalfW - 0.08f), 2.2f, -6f + d * 6f),
                     0.045f, 1.7f, metal, Vector3.zero);
            }
        }
    }

    static void Fan(Transform parent, string name, Vector3 mount, Material rod, Material blade, float yaw)
    {
        Transform f = Group(parent, name);
        f.localPosition = mount;
        f.localEulerAngles = new Vector3(0f, yaw, 0f);

        // Down-rod and motor housing.
        Tube(name + "_Rod", f, new Vector3(0f, -0.30f, 0f), 0.045f, 0.60f, rod, Vector3.zero);
        Tube(name + "_Hub", f, new Vector3(0f, -0.645f, 0f), 0.22f, 0.13f, rod, Vector3.zero);
        Tube(name + "_HubCap", f, new Vector3(0f, -0.715f, 0f), 0.13f, 0.04f, rod, Vector3.zero);

        // Three blades, as on every Indian ceiling fan.
        //
        // ROTATION: a blade's long axis is local X. Rotating (1,0,0) by theta
        // about Y gives (cos t, 0, -sin t); the radial direction at angle a is
        // (sin a, 0, cos a). Matching both components requires theta = a - 90,
        // NOT 90 - a. The old value aligned only the X component, so blades
        // splayed off-radius -- which is what made the fan look broken.
        //
        // PITCH goes on X, not Z. Unity applies euler as Z, X, then Y, so an X
        // rotation tilts the blade about its own long axis (true pitch), while
        // a Z rotation would cone the blade up or down instead.
        //
        // SIZE: a 1200 mm sweep fan means ~0.59 m radius. The previous blades
        // were 1.15 m long at 0.62 m out -- a 2.4 m sweep, roughly double.
        const float BladeLength = 0.50f;
        const float BladeRadius = 0.335f;   // centre of the blade from the hub
        const float BladePitch = 12f;

        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            float rad = a * Mathf.Deg2Rad;

            BoxR(name + "_Blade" + i, f,
                 new Vector3(Mathf.Sin(rad) * BladeRadius, -0.675f, Mathf.Cos(rad) * BladeRadius),
                 new Vector3(BladeLength, 0.020f, 0.185f), blade,
                 new Vector3(BladePitch, a - 90f, 0f));

            // Short arm from the hub out to the blade root, so blades are not
            // floating unattached in mid-air.
            BoxR(name + "_Arm" + i, f,
                 new Vector3(Mathf.Sin(rad) * 0.115f, -0.665f, Mathf.Cos(rad) * 0.115f),
                 new Vector3(0.13f, 0.022f, 0.055f), rod,
                 new Vector3(0f, a - 90f, 0f));
        }
    }

    // ---------------------------------------------------------------- signage

    static void BuildSignage(Transform g)
    {
        Material green = M("Mat_Pub_PosterGreen");
        Material dark = M("Mat_Pub_PosterDark");
        Material warm = M("Mat_Pub_PosterWarm");
        Material cream = M("Mat_Pub_PosterCream");

        // Long banners high on the side walls.
        Transform ban = Group(g, "Banners");
        for (int i = 0; i < 4; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;
            float z = -HalfD + 3.6f + (i / 2) * 7.5f;
            Material m = (i % 2 == 0) ? green : dark;
            Box("Banner_" + i, ban, new Vector3(side * (HalfW - 0.05f), 3.30f, z),
                new Vector3(0.04f, 0.72f, 3.0f), m);
            Box("Banner_" + i + "_Edge", ban, new Vector3(side * (HalfW - 0.08f), 2.93f, z),
                new Vector3(0.03f, 0.05f, 3.0f), warm);
        }

        // Menu board near the counter.
        Box("MenuBoard", g, new Vector3(-2.4f, 2.35f, HalfD - 0.06f),
            new Vector3(1.6f, 1.05f, 0.05f), green);
        Box("MenuBoard_Panel", g, new Vector3(-2.4f, 2.35f, HalfD - 0.10f),
            new Vector3(1.42f, 0.88f, 0.03f), cream);
    }
    static void Crate(Transform parent, string name, Vector3 pos, Material mat, float yaw)
    {
        Transform c = Group(parent, name);
        c.localPosition = pos;
        c.localEulerAngles = new Vector3(0f, yaw, 0f);
        Box(name + "_Floor", c, new Vector3(0f, -0.14f, 0f), new Vector3(0.60f, 0.03f, 0.42f), mat);
        Box(name + "_S0", c, new Vector3(0f, 0f, 0.20f), new Vector3(0.60f, 0.30f, 0.025f), mat);
        Box(name + "_S1", c, new Vector3(0f, 0f, -0.20f), new Vector3(0.60f, 0.30f, 0.025f), mat);
        Box(name + "_S2", c, new Vector3(0.29f, 0f, 0f), new Vector3(0.025f, 0.30f, 0.42f), mat);
        Box(name + "_S3", c, new Vector3(-0.29f, 0f, 0f), new Vector3(0.025f, 0.30f, 0.42f), mat);
    }

    // ---------------------------------------------------------------- lighting

    static void BuildLighting(Transform g)
    {
        // Afternoon sun almost directly overhead.
        //
        // 84 deg elevation, not the old 26 deg. A low sun drove a shaft of
        // daylight straight through the open entrance and down the hall, which
        // competed with the bulbs the interior is supposed to be lit by. From
        // overhead the sealed roof blocks it entirely, so the inside stays
        // bulb-lit while the compound outside is still daylit.
        //
        // 84 rather than a flat 90: a few degrees of tilt keeps exterior walls
        // and the signboard from rendering perfectly flat.
        GameObject sunGo = new GameObject("Sun_Directional");
        sunGo.transform.SetParent(g, false);
        sunGo.transform.localPosition = new Vector3(0f, 9f, 0f);
        sunGo.transform.localEulerAngles = new Vector3(84f, 15f, 0f);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        // Overcast values. Cloud scatters direct sun into diffuse light, so
        // intensity drops, the colour loses its warm cast, and shadows go weak
        // and soft rather than sharp-edged.
        sun.color = new Color(0.93f, 0.94f, 0.96f);
        sun.intensity = 0.78f;
        sun.shadows = LightShadows.Soft;
        // Lighter. Overcast light is scattered from the whole sky, so a shadow
        // is a gentle darkening rather than a silhouette. This was 0.42, which
        // read as a hard cut-out once the quality level stopped forcing hard
        // shadows and the edges actually softened.
        sun.shadowStrength = 0.46f;
        // A low-resolution map needs more bias to avoid self-shadow acne, and
        // normal bias rather than depth bias keeps contact points from
        // detaching ("peter-panning") when it is raised.
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.5f;
        sun.shadowNearPlane = 0.3f;
        _objectCount++;

        // ---- Interior: bulbs only ------------------------------------------
        //
        // The roof is sealed, so these are the only light sources.
        //
        // COST MODEL: in Built-in forward rendering every PIXEL light adds a
        // render pass for each affected renderer. At 650 renderers, six pixel
        // lights took the headset from 72 fps to ~36. VERTEX lights are
        // evaluated once per vertex and cost almost nothing.
        //
        // So: a small number of pixel lights where a visible pool matters
        // (pendants over the tables, the counter), and vertex lights for
        // everything else, which still colours and lifts the room.

        Color warmBulb = new Color(1f, 0.845f, 0.615f);
        Color tubeWhite = new Color(0.90f, 0.945f, 1f);

        // Pixel: three pendants down the centre, spaced to cover the hall.
        //
        // ONLY ONE OF THEM CASTS SHADOWS. Four shadowed lights meant four extra
        // shadow-map renders every frame, each redrawing every caster inside a
        // 7 m cone in a hall of ~690 renderers. Draw calls are this project's
        // real constraint, and that was enough to lose the 72 fps lock.
        //
        // The one that keeps its shadows is the pendant over the hero table --
        // z = 0.2 against the table at z = 1.4 -- because the seated scenario
        // is the only place a patient studies objects closely enough for
        // contact shadows to matter. The rest of the hall is walked through.
        // A light AT EVERY VISIBLE PENDANT, positions taken from the fixtures.
        //
        // Three of the five are pixel lights that cast shadows; the rest are
        // vertex fill. Which three is chosen to spread them along the hall
        // rather than to sit next to each other -- index 0, 2 and 3, so one
        // covers the entrance end, one the hero table at z = 1.4, and one the
        // middle of the room.
        //
        // Only three cast because pixel lights are the expensive kind and four
        // shadowed ones cost the framerate outright earlier today. This is the
        // dial if it needs to come down further.
        // ONE shadowed pendant, down from three.
        //
        // Three plus the sun plus the two sign floods was six realtime shadow
        // maps, every one re-rendering every caster within shadowDistance each
        // frame. Quest guidance is one. The framerate did not fall evenly, it
        // fell WHEN THE ROOM CAME INTO VIEW, which is the signature of paying
        // per caster per map rather than a flat overhead.
        //
        // Index 0 is the pendant over the hero table, kept because that is the
        // one spot a patient stands still and looks down at a table, where
        // contact shadows carry the scene. The rest of the hall is walked
        // through, and vertex fill reads as lit without paying for maps.
        // The shadowed pendant is chosen by DISTANCE to the hero table rather
        // than by a hard-coded index, so re-spacing the pendants -- as moving
        // them onto the trusses just did -- cannot silently move the one
        // shadowed light away from the one place it is there for.
        int heroPendant = 0;
        float heroBest = float.MaxValue;
        for (int i = 0; i < _pendantBulbs.Count; i++)
        {
            float dz = Mathf.Abs(_pendantBulbs[i].z - PubScenarioBuilder.TableZ);
            if (dz < heroBest) { heroBest = dz; heroPendant = i; }
        }
        var shadowed = new HashSet<int> { heroPendant };

        for (int i = 0; i < _pendantBulbs.Count; i++)
        {
            Vector3 at = _pendantBulbs[i];
            if (shadowed.Contains(i))
            {
                Downlight(g, "Lamp_Pendant_C" + i, at, warmBulb, 2.55f, 9.0f,
                          castShadows: true);
            }
            else
            {
                PointLight(g, "Lamp_Pendant_F" + i, at, warmBulb, 1.55f, 8.0f);
            }
        }

        // Pixel: the counter, always the brightest point in these bars.
        //
        // No shadows here, and this is the deliberate one to give up.
        //
        // The counter, its cage, the crates and the shelf stock are the densest
        // cluster of renderers in the building, so this map cost more than the
        // other three together -- and it is the corner a patient looks at from
        // furthest away. Turning it off buys back the frames that the wider
        // cones and longer shadow distance spent on the seating area, which is
        // where a patient actually stands.
        Downlight(g, "Lamp_Counter", new Vector3(1.9f, 2.7f, HalfD - 2.0f),
                  new Color(1f, 0.905f, 0.735f), 2.55f, 6.8f, castShadows: false);

        // Vertex fill: side rows, so the outer tables and walls are not black.
        for (int i = 0; i < 3; i++)
        {
            float z = -HalfD + 4.6f + i * 5.6f;
            PointLight(g, "Lamp_Side_L" + i, new Vector3(-3.7f, EavesHeight - 1.45f, z),
                       warmBulb, 1.30f, 7.0f);
            PointLight(g, "Lamp_Side_R" + i, new Vector3(3.7f, EavesHeight - 1.45f, z),
                       warmBulb, 1.30f, 7.0f);
        }

        // Vertex: bare bulbs low over the tables.
        for (int i = 0; i < 3; i++)
        {
            float z = -HalfD + 5.0f + i * 6.0f;
            PointLight(g, "Lamp_Bare_" + i, new Vector3(0f, 2.55f, z),
                       new Color(1f, 0.90f, 0.70f), 0.95f, 5.0f);
        }

        PointLight(g, "Lamp_Counter_Shelf", new Vector3(-1.4f, 2.35f, HalfD - 2.3f),
                   new Color(1f, 0.88f, 0.68f), 1.35f, 6.0f);

        // Vertex: cool wall tubes, a colour contrast against the warm bulbs.
        for (int i = 0; i < 3; i++)
        {
            float z = -HalfD + 6.0f + i * 6.0f;
            PointLight(g, "Lamp_Tube_L" + i, new Vector3(-HalfW + 0.55f, 2.85f, z),
                       tubeWhite, 0.90f, 5.5f);
            PointLight(g, "Lamp_Tube_R" + i, new Vector3(HalfW - 0.55f, 2.85f, z),
                       tubeWhite, 0.90f, 5.5f);
        }

        // Doorway spill only: the doors are open, but this must not light the hall.
        // Doorway spill: with the sun overhead almost nothing comes through the
        // opening horizontally, so this is a faint wash at the threshold only.
        PointLight(g, "Lamp_Entrance", new Vector3(0f, 1.9f, -HalfD + 0.35f),
                   new Color(0.88f, 0.91f, 1f), 0.45f, 2.4f);
    }

    /// <summary>How dark an interior shadow gets. Low: these are soft pools.</summary>
    const float InteriorShadowStrength = 0.72f;

    /// <summary>
    /// A ceiling lamp that CASTS SHADOWS, built as two lights.
    ///
    /// WHY A SPOT AND NOT A SHADOWED POINT LIGHT
    /// -----------------------------------------
    /// A shadow-casting point light renders its casters SIX times, once per
    /// cubemap face. A spot renders them once. With ~650 renderers in the hall
    /// and draw calls -- not triangles -- as this project's real constraint,
    /// six-fold was never affordable. One is.
    ///
    /// A downward spot is also the more honest model: these are shaded pendant
    /// lamps, which throw light down, not in all directions.
    ///
    /// WHY THERE IS STILL A POINT LIGHT
    /// --------------------------------
    /// A bare spot would leave the ceiling and upper walls around each lamp
    /// black, because nothing else lights them. The companion is a VERTEX point
    /// light: it restores the omnidirectional wash for almost nothing, since
    /// vertex lights cost no extra forward pass. The pixel-light budget is
    /// unchanged at four -- the spot simply takes the slot the point light used
    /// to hold.
    /// </summary>
    /// <summary>
    /// A four-sided steel frame around a corrugated sheet leaf.
    ///
    /// Corrugated sheet on its own reads as a flat blue rectangle, because the
    /// corrugations run one way and nothing crosses them. A real sheet door is
    /// a welded frame with the sheet fixed to it, and the frame is what gives
    /// the leaf an edge and a top and bottom rail to catch the light.
    ///
    /// The bars run PROUD of the sheet on both faces, so the frame reads from
    /// either side -- these leaves are all seen from both.
    /// </summary>
    static void SheetFrame(Transform leaf, Vector3 centre, float w, float h,
                           float sheetT, Material steel)
    {
        const float bar = 0.075f;                 // frame member width
        float d = sheetT + 0.030f;                // 15 mm proud on each face

        Box("Frame_Top", leaf, centre + new Vector3(0f, (h - bar) * 0.5f, 0f),
            new Vector3(w, bar, d), steel);
        Box("Frame_Bottom", leaf, centre + new Vector3(0f, -(h - bar) * 0.5f, 0f),
            new Vector3(w, bar, d), steel);
        Box("Frame_Left", leaf, centre + new Vector3(-(w - bar) * 0.5f, 0f, 0f),
            new Vector3(bar, h - bar * 2f, d), steel);
        Box("Frame_Right", leaf, centre + new Vector3((w - bar) * 0.5f, 0f, 0f),
            new Vector3(bar, h - bar * 2f, d), steel);
    }

    /// <summary>
    /// One gooseneck fixture on an exterior wall, plus the light it casts.
    ///
    /// <paramref name="outward"/> is the wall's outward normal, which sets both
    /// the yaw and the direction the arm reaches. Asset_WallLight is modelled
    /// with its plate on the origin and its arm along +Z, so yaw comes straight
    /// from that vector and the fixture mounts flush to any wall.
    ///
    /// The light sits at the SHADE, not at the mount: the arm carries the head
    /// 0.28 m out from the wall and 0.13 m down, and a light left at the plate
    /// would pour out of the brickwork behind the fixture.
    /// </summary>
    static void WallLight(Transform parent, string name, Vector3 mount, Vector3 outward)
    {
        // +180 because Unity's FBX axis conversion lands the model's +Y arm on
        // world -Z, not +Z. Verified by measurement rather than derivation: at
        // yaw = atan2 alone the fixtures sat INSIDE the brickwork (bounds centre
        // 6.07 through a wall spanning 6.00..6.25); with +180 they stand clear
        // at 6.43. This is separate from the plate-end bug in walllight.py --
        // that one put the wall plate out in the air, and fixing it did not
        // remove the need for this.
        float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg + 180f;
        Model("Asset_WallLight", parent, mount, yaw, M("Mat_Pub_WallLightBody"),
              slotMaterials: new[] { M("Mat_Pub_WallLightBody"), M("Mat_Pub_Bulb") });

        // NOT Downlight(). That helper is for the interior key lights and sets
        // renderMode = ForcePixel, which bypasses the pixelLightCount budget and
        // adds a full render pass per affected renderer -- and it also spawns a
        // second "wash" point light. Five fixtures built that way put TEN forced
        // pixel lights outside and took the hall from 97% of frames on budget to
        // 55%, measured. These are decorative wall washes; they get one ordinary
        // spot each, left to Unity's own importance sorting.
        Vector3 head = mount + outward.normalized * 0.28f + Vector3.down * 0.13f;
        GameObject lgo = new GameObject(name + "_Light");
        lgo.transform.SetParent(parent, false);
        lgo.transform.localPosition = head;
        lgo.transform.localEulerAngles = new Vector3(78f, yaw, 0f);
        Light wl = lgo.AddComponent<Light>();
        wl.type = LightType.Spot;
        wl.color = new Color(1f, 0.93f, 0.80f);
        wl.intensity = 2.4f;
        wl.range = 5.0f;
        wl.spotAngle = 120f;
        wl.innerSpotAngle = 55f;
        wl.shadows = LightShadows.None;
        wl.renderMode = LightRenderMode.Auto;
        _objectCount++;
    }

    static void Downlight(Transform parent, string name, Vector3 pos, Color color,
                          float intensity, float range, bool castShadows)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = new Vector3(90f, 0f, 0f);   // straight down

        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        // 140 degrees, not 118.
        //
        // A pendant hangs 2.65 m up, so a 118 degree cone lit a circle of only
        // 4.4 m radius -- in a hall 12 m wide that left 1.6 m up each side with
        // no pixel light on it at all, and no pixel light means no shadow. That
        // is the other half of why some tables had shadows and some did not.
        // 140 degrees reaches 7.3 m and the three cones overlap properly.
        l.spotAngle = 140f;
        l.innerSpotAngle = 70f;
        l.renderMode = LightRenderMode.ForcePixel;

        l.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        if (castShadows)
        {
            l.shadowStrength = InteriorShadowStrength;
            l.shadowBias = 0.04f;
            l.shadowNormalBias = 0.45f;
            l.shadowNearPlane = 0.2f;
            // 128, down from 256.
            //
            // Resolution is the one setting that moves softness and cost the
            // same way: fewer, larger texels blur further under the soft filter
            // AND there is a quarter as much shadow map to fill and sample.
            // On a contact shadow under a chair the blur is welcome -- these
            // are meant to read as pools, not as cut-outs.
            l.shadowCustomResolution = 128;
        }
        _objectCount++;

        // Companion wash, so the ceiling around the lamp is not black.
        PointLight(parent, name + "_Wash", pos, color, intensity * 0.42f, range * 0.8f);
    }

    static void PointLight(Transform parent, string name, Vector3 pos, Color color,
                           float intensity, float range,
                           LightRenderMode mode = LightRenderMode.ForceVertex)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;   // keep it cheap for Quest
        // ForceVertex lights are evaluated per-vertex: they tint the room for
        // almost nothing. Only the few lights that must cast a visible pool are
        // ForcePixel, because each pixel light adds a forward pass per affected
        // renderer -- with 650 renderers that is what cost us half the framerate.
        l.renderMode = mode;
        _objectCount++;
    }

    const string SkyMatPath = "Assets/PubEnvironment/Materials/Mat_Sky_Overcast.mat";

    /// <summary>
    /// Overcast sky, built from Unity's procedural skybox -- no texture files,
    /// so it costs nothing on disk and respects the no-textures rule.
    ///
    /// Heavy atmosphere thickness plus low exposure is what reads as cloud:
    /// the sun disc is scattered out rather than drawn, so you get flat grey
    /// light instead of a hard disc in a blue sky.
    /// </summary>
    static Material OvercastSky()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
        Shader sky = Shader.Find("Skybox/Procedural");
        if (sky == null)
            return null;

        if (m == null)
        {
            m = new Material(sky);
            AssetDatabase.CreateAsset(m, SkyMatPath);
        }

        m.shader = sky;
        m.SetFloat("_SunDisk", 0f);            // no visible disc through cloud
        m.SetFloat("_SunSize", 0.02f);
        m.SetFloat("_AtmosphereThickness", 2.6f);
        m.SetColor("_SkyTint", new Color(0.52f, 0.53f, 0.55f));
        m.SetColor("_GroundColor", new Color(0.32f, 0.30f, 0.28f));
        m.SetFloat("_Exposure", 0.62f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Strokes for a blocky sans-serif alphabet, in a unit cell: each entry is
    /// (x, y, width, height) with 0,0 at the letter's bottom-left and 1,1 at
    /// its top-right.
    ///
    /// Only the letters this project actually paints are defined. Diagonals are
    /// squared off deliberately -- an angled box on a sign reads as a mistake,
    /// while a blocky capital reads as signwriting, which is what these boards
    /// are.
    /// </summary>
    static readonly Dictionary<char, float[][]> k_Glyphs = new Dictionary<char, float[][]>
    {
        ['T'] = new[]
        {
            new[] { 0.00f, 0.85f, 1.00f, 0.15f },   // top bar
            new[] { 0.41f, 0.00f, 0.18f, 0.85f },   // stem
        },
        ['A'] = new[]
        {
            new[] { 0.00f, 0.00f, 0.18f, 0.84f },   // left leg
            new[] { 0.82f, 0.00f, 0.18f, 0.84f },   // right leg
            new[] { 0.00f, 0.84f, 1.00f, 0.16f },   // top
            new[] { 0.18f, 0.42f, 0.64f, 0.14f },   // crossbar
        },
        ['S'] = new[]
        {
            new[] { 0.00f, 0.86f, 1.00f, 0.14f },   // top
            new[] { 0.00f, 0.50f, 0.18f, 0.36f },   // upper left
            new[] { 0.00f, 0.43f, 1.00f, 0.14f },   // middle
            new[] { 0.82f, 0.14f, 0.18f, 0.29f },   // lower right
            new[] { 0.00f, 0.00f, 1.00f, 0.14f },   // bottom
        },
        ['M'] = new[]
        {
            new[] { 0.00f, 0.00f, 0.18f, 1.00f },   // left
            new[] { 0.82f, 0.00f, 0.18f, 1.00f },   // right
            new[] { 0.00f, 0.86f, 1.00f, 0.14f },   // top
            new[] { 0.41f, 0.40f, 0.18f, 0.46f },   // centre drop
        },
        ['C'] = new[]
        {
            new[] { 0.00f, 0.86f, 1.00f, 0.14f },   // top
            new[] { 0.00f, 0.14f, 0.18f, 0.72f },   // spine
            new[] { 0.00f, 0.00f, 1.00f, 0.14f },   // bottom
        },
    };

    /// <summary>
    /// Paints a word onto a flat surface as raised box strokes, centred on
    /// <paramref name="centre"/> and fitted inside <paramref name="width"/>.
    /// The letters face -Z, which is the direction the compound's signboard
    /// looks out toward the street.
    /// </summary>
    static void SignText(Transform parent, string word, Vector3 centre,
                         float width, float height, Material mat)
    {
        if (string.IsNullOrEmpty(word))
            return;

        Transform g = Group(parent, "Signboard_Text");

        // Fit to the board width: letters plus the gaps between them.
        const float gapFraction = 0.22f;              // of one letter's width
        float letterW = width / (word.Length + (word.Length - 1) * gapFraction);
        float gap = letterW * gapFraction;
        float depth = 0.025f;

        float x = centre.x - width * 0.5f;

        for (int i = 0; i < word.Length; i++)
        {
            char c = char.ToUpperInvariant(word[i]);
            if (k_Glyphs.TryGetValue(c, out float[][] strokes))
            {
                for (int sIdx = 0; sIdx < strokes.Length; sIdx++)
                {
                    float[] r = strokes[sIdx];
                    float sw = r[2] * letterW;
                    float sh = r[3] * height;

                    // Each stroke sits a fraction deeper than the last.
                    //
                    // Strokes OVERLAP by design -- the S's middle bar crosses
                    // both its verticals, the M's centre drop meets its top bar
                    // -- and two boxes at the same depth share a front plane,
                    // which z-fights. It showed as flickering seams through the
                    // S and the M. A third of a millimetre each is invisible
                    // and gives the depth buffer something to separate.
                    float zBias = sIdx * 0.0004f;

                    Box($"Sign_{c}{i}_{sIdx}", g,
                        new Vector3(x + (r[0] + r[2] * 0.5f) * letterW,
                                    centre.y - height * 0.5f + (r[1] + r[3] * 0.5f) * height,
                                    centre.z - zBias),
                        new Vector3(sw, sh, depth), mat);
                }
            }
            else
            {
                Debug.LogWarning($"[PubEnvironment] No glyph for '{c}'; skipped.");
            }

            x += letterW + gap;
        }
    }

    /// <summary>
    /// Loads the shared clink clip once. GrabClink holds it statically because
    /// every bottle plays the same sound -- storing a reference per object
    /// would serialise ~75 copies of the same pointer into the scene.
    /// </summary>
    static AudioClip _clinkClip;

    static void LoadGrabAudio()
    {
        _clinkClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/Grab/Clink.wav");

        if (_clinkClip == null)
            Debug.LogWarning("[PubEnvironment] Clink.wav missing; grabs stay silent.");
        else
            Debug.Log("[PubEnvironment] Grab clink loaded.");
    }

    // Which panorama the sky uses. BOTH files stay in the project; switching
    // PanoramaPath back to SkyPainted is the whole revert -- no reimport, no
    // asset to restore. PanoramaSky() also falls back to the procedural sky if
    // whichever file is named here goes missing.
    const string SkyPainted = "Assets/Textures/Sky_Overcast.png";   // 1774x887
    const string SkyDayHDRI = "Assets/Textures/Sky_DayHDRI.jpg";    // 4096x2048
    const string PanoramaPath = SkyDayHDRI;
    const string PanoMatPath = "Assets/PubEnvironment/Materials/Mat_Sky_Panorama.mat";

    /// <summary>
    /// Overcast sky from a 2:1 equirectangular panorama.
    ///
    /// Skybox/Panoramic in Lat-Long mode is what reads a 2:1 image correctly --
    /// handed to Skybox/6 Sided or Cubemap it comes out smeared.
    ///
    /// This particular image suits the scene in a way a photographic sky
    /// usually does not: it is fully overcast with NO SUN DISC, so it does not
    /// contradict the directional light sitting at 84 degrees overhead. A sky
    /// with a visible low sun would have shadows falling straight down while
    /// the horizon said sunset.
    ///
    /// Now a 4096x2048 photographic panorama (ambientCG Day Sky HDRI 020 A),
    /// replacing a 1774x887 painted one that the comment here used to concede
    /// was soft. 4K is about 11 pixels per degree of arc against the headset's
    /// ~19; on diffuse cloud that gap does not show, which is why 8K was not
    /// worth four times the memory.
    ///
    /// Its lower hemisphere is a real FIELD, not blank cloud, so a treeline
    /// horizon sits above the compound walls. That suits a rural bar and is
    /// worth knowing: a panorama shot in a city would put skyline through the
    /// same gap.
    ///
    /// Falls back to the procedural sky if the texture is ever missing, so a
    /// lost file degrades to a grey sky rather than to Unity's default blue.
    /// </summary>
    static Material PanoramaSky()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PanoramaPath);
        Shader sky = Shader.Find("Skybox/Panoramic");
        if (tex == null || sky == null)
        {
            Debug.LogWarning("[PubEnvironment] Panorama sky unavailable; using procedural.");
            return null;
        }

        Material m = AssetDatabase.LoadAssetAtPath<Material>(PanoMatPath);
        if (m == null)
        {
            m = new Material(sky);
            AssetDatabase.CreateAsset(m, PanoMatPath);
        }

        m.shader = sky;
        m.SetTexture("_MainTex", tex);
        m.SetFloat("_Mapping", 1f);        // Latitude-Longitude
        m.SetFloat("_ImageType", 0f);      // 360 degrees
        m.SetFloat("_MirrorOnBack", 0f);
        m.SetFloat("_Layout", 0f);         // no stereo split
        m.SetFloat("_Rotation", 0f);
        // Held just under 1 so the sky stays a touch darker than the lit
        // interior, which is what keeps the bulbs reading as the light source.
        m.SetFloat("_Exposure", 0.88f);
        m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));   // neutral, no colour cast
        EditorUtility.SetDirty(m);
        Debug.Log("[PubEnvironment] Overcast panorama sky applied.");
        return m;
    }

    static void ConfigureRenderSettings()
    {
        Material sky = PanoramaSky() ?? OvercastSky();
        if (sky != null)
            RenderSettings.skybox = sky;
        // Ambient is deliberately near-black. With a sealed roof the bulbs are
        // the light source, and any meaningful ambient term flattens the room
        // back into the evenly-lit look we are trying to get away from.
        // A little is kept so unlit corners are dim rather than pure black.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.070f, 0.072f, 0.082f);
        RenderSettings.ambientEquatorColor = new Color(0.052f, 0.048f, 0.045f);
        RenderSettings.ambientGroundColor = new Color(0.028f, 0.026f, 0.024f);
        RenderSettings.ambientIntensity = 1f;

        // Haze does most of the atmospheric work for almost no cost.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.115f, 0.100f, 0.086f);
        RenderSettings.fogStartDistance = 14f;
        RenderSettings.fogEndDistance = 60f;
    }

    static void PlaceReviewCamera()
    {
        GameObject cam = GameObject.Find("Main Camera");
        if (cam == null) return;
        // Standing just inside the entrance at eye height, looking down the hall.
        cam.transform.position = new Vector3(0f, 1.6f, -HalfD + 1.2f);
        cam.transform.eulerAngles = new Vector3(2f, 0f, 0f);
        Camera c = cam.GetComponent<Camera>();
        if (c != null)
        {
            c.fieldOfView = 63f;
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 80f;
        }
    }

    // -------------------------------------------------------------- compound

    static void BuildCompound(Transform g)
    {
        Material dirt      = M("Mat_Pub_DirtGround");
        Material wall      = M("Mat_Pub_CompoundWall");
        Material gateWood  = M("Mat_Pub_GateWood");
        Material signGreen = M("Mat_Pub_SignboardGreen");
        Material steel     = M("Mat_Pub_SteelBlue");
        Material pole      = M("Mat_Pub_ConcretePole");
        Material street    = M("Mat_Pub_StreetGrey");
        Material dark      = M("Mat_Pub_PosterDark");
        Material cream     = M("Mat_Pub_PosterCream");

        float cHW   = YardHalfW;                    // ~11.25
        float fZ    = YardFrontZ;                    // ~-18.25
        float bZ    = YardBackZ;                     // ~12.25
        float cDep  = bZ - fZ;                       // ~30.5
        float cCZ   = (fZ + bZ) * 0.5f;             // center Z
        float t     = CompoundWallT;
        float wH    = CompoundWallH;

        // ---- Ground surfaces ----
        Transform ground = Group(g, "Ground");

        // Compound yard (dirt, slightly below building floor to avoid z-fighting)
        float yardX = cHW * 2f + t * 2f + 0.5f;
        float yardZ = cDep + t * 2f + 0.5f;
        Box("YardGround", ground, new Vector3(0f, -0.04f, cCZ),
            new Vector3(yardX, 0.06f, yardZ), dirt, true);

        // 2.8 m per repeat, because that is the real-world size ambientCG
        // captured. Matching it puts the pebbles at life size instead of at
        // whatever scale looked plausible -- a guess this asset happens to
        // remove the need for.
        const float DirtTileMetres = 2.8f;
        Tile(dirt, new Vector2(yardX / DirtTileMetres, yardZ / DirtTileMetres));

        // ---- Ground cover ----
        //
        // Coverage is a FRACTION OF OPEN YARD, so "70%" means 70% and stays
        // true if the yard is resized. Three zones:
        //
        //   path      the walk from gate to door -- NOTHING, it is a path
        //   entrance  the apron either side of it -- 20%
        //   yard      everything else -- 70%
        //
        // The flat 2.27 m patch this started with is gone. It was the cheapest
        // variant per square metre and unusable for it: an 8 cm mat that reads
        // as dirt texture from standing height. Coverage you cannot see is not
        // coverage. Cover is now the tuft, scaled 1.4x -- 135 tris/m2 against
        // the patch's 96, but actually visible.
        Transform veg = Group(g, "GroundCover");
        int vegLayer = EnsureLayer("GroundCover");
        veg.gameObject.AddComponent<GroundCoverCulling>();

        Material grassTuft = MatCutout("Mat_Pub_GrassTuft", "Assets/Textures/Grass_Tuft.png");
        Material grassWeed = MatCutout("Mat_Pub_GrassWeed", "Assets/Textures/Grass_Weed.png");
        Material shrubMat  = MatTextured("Mat_Pub_Shrub", "Assets/Textures/Shrub.jpg", 0.12f,
                                         "Assets/Textures/Shrub_Normal.jpg");

        // Yard surface is y = -0.01. Plants are set BELOW it: a trunk that only
        // touches shows a hairline of ground through its root flare and reads as
        // floating, which is exactly what was reported even though every piece
        // measured at a 0.000 gap. 0.10 m buries a tree's root flare; 0.04 m is
        // enough for grass.
        const float GroundSink = -0.05f;
        // 1.10 m down. Sinking by the mesh's LOWEST vertex is not enough --
        // that vertex is a thin taproot well below the visible root flare, so
        // the flare, the part you actually look at, kept hanging in the air.
        // 0.11 and 0.45 both still read as floating. On a 9 m tree and a 6.3 m
        // palm, burying 1.1 m of root costs nothing anyone can see.
        const float TreeSink   = -1.10f;

        // 0.24, not 0.70. Small plants cost roughly four times as much per
        // square metre as the oversized ones did, so 70% is no longer
        // affordable at a playable framerate -- and scattered weeds on dirt is
        // the right look for this yard regardless. 0.24 lands the scene back on
        // ~186k triangles, the figure that measured 88% of frames on budget.
        // Raising this is one number, but it buys greenery with frames.
        const float YardCoverage = 0.24f;
        const float EntranceCoverage = 0.20f;
        const float TuftArea = 1.19f;     // 0.78 x 0.77 m, tuft scaled 1.15x
        const float WeedArea = 0.11f;   // 0.29 x 0.36 m at 0.55 scale
        // Nearly all the cover is tufts. Measured per square metre covered the
        // tuft is 66 tris/m2 and the weed 459 -- seven times worse -- so weeds
        // buy variety, not coverage.
        const float TuftShare = 0.985f;
        // 4,194 triangles each -- collapse decimation floors out there, because
        // the mesh is separate leaf cards that cannot merge further. So these
        // are accents, deliberately few; nine of them cost more than the six
        // ceiling lamps and the five wall lights put together.
        // ZERO. The shrub is withdrawn, not tuned down. Three separate faults, any
        // one disqualifying: Unity imports its mesh with a Z extent of 0.262 m
        // where the file Blender reads spans 0.916, so it hung 0.65 m in the
        // air; collapse decimation shreds it, because it is separate leaf cards
        // that cannot merge, leaving the dark spikes seen against the wall; and
        // those cards are single-sided, so half of every bush renders black
        // under the Standard shader. At 4,194 triangles each it was also the
        // worst value in the scene. It needs a two-sided foliage shader and a
        // model that survives reduction -- not a smaller count.
        const int   ShrubCount = 0;

        float bldHalfW = HalfW + t + 0.45f;
        float bldHalfD = HalfD + t + 0.45f;
        float pathHalfW = 2.4f;                  // clear walk to the door
        float entHalfW  = 6.5f;
        float entFrontZ = -bldHalfD;             // apron is everything in front

        System.Random vegRNG = new System.Random(20260903);
        float VRand(float lo, float hi) { return lo + (float)vegRNG.NextDouble() * (hi - lo); }

        float openArea = (yardX * yardZ) - (bldHalfW * 2f * bldHalfD * 2f);
        int tufts  = Mathf.RoundToInt(openArea * YardCoverage * TuftShare / TuftArea);
        int weeds  = Mathf.RoundToInt(openArea * YardCoverage * (1f - TuftShare) / WeedArea);

        int placed = 0, skippedPath = 0, thinnedEntrance = 0;
        for (int kind = 0; kind < 3; kind++)
        {
            int want = kind == 0 ? tufts : kind == 1 ? weeds : ShrubCount;
            string asset = kind == 0 ? "Asset_GrassTuft"
                         : kind == 1 ? "Asset_GrassWeed" : "Asset_Shrub";
            Material mat = kind == 0 ? grassTuft : kind == 1 ? grassWeed : shrubMat;

            for (int i = 0; i < want; i++)
            {
                // Resample until the point is off the building. Discarding
                // those samples instead placed only 105 of 236 pieces -- the
                // building is a third of the yard's bounding box, so a third of
                // every requested count was silently thrown away and the
                // coverage came out far under what was asked for.
                Vector3 p = Vector3.zero;
                bool found = false;
                for (int attempt = 0; attempt < 40 && !found; attempt++)
                {
                    // GroundSink, not the yard surface exactly. Every one of
                    // these measured at a 0.000 gap and still read as hovering,
                    // because a plant whose lowest vertex merely TOUCHES the
                    // ground shows daylight under its outer leaves. Real plants
                    // grow out of the soil, so they are set into it.
                    p = new Vector3(
                        VRand(-yardX * 0.5f + 0.7f, yardX * 0.5f - 0.7f), GroundSink,
                        VRand(cCZ - yardZ * 0.5f + 0.7f, cCZ + yardZ * 0.5f - 0.7f));
                    found = Mathf.Abs(p.x) > bldHalfW || Mathf.Abs(p.z) > bldHalfD;
                }
                if (!found)
                    continue;

                bool inFront = p.z < entFrontZ;
                if (inFront && Mathf.Abs(p.x) < pathHalfW)
                {
                    skippedPath++;                             // the walk itself
                    continue;
                }
                if (inFront && Mathf.Abs(p.x) < entHalfW &&
                    vegRNG.NextDouble() > EntranceCoverage / YardCoverage)
                {
                    thinnedEntrance++;                         // apron thinned to 20%
                    continue;
                }

                GameObject go = Model(asset, veg, p, VRand(0f, 360f), mat);
                if (go == null)
                    continue;
                // No shadows: the sun is one of only two shadow-casting lights
                // and every caster inside shadowDistance is re-rendered into its
                // map each frame.
                foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (Transform tr in go.GetComponentsInChildren<Transform>(true))
                    tr.gameObject.layer = vegLayer;
                placed++;
            }
        }
        // ---- Trees ----
        //
        // One broadleaf and two coconut palms. Their own RNG again, so adding
        // them does not re-roll the grass laid out above.
        //
        // The palm is placed by hand rather than through Model(): it arrives as
        // FIVE separate renderers with one material slot each -- trunk, palm
        // top, leaves, frond stems, coconuts -- and Model() paints every
        // renderer the same colour. The mapping is not guessed; it is read out
        // of the FBX, which is ASCII and states it outright:
        //   Tree_0 palm02   Tree_1 palm top   Tree_2 leaf
        //   Tree_3 frond stem   Tree_4 coconut
        Material palmTrunk = MatCutout("Mat_Pub_PalmTrunk", "Assets/Textures/Palm_palm02.png");
        Material palmTop   = MatCutout("Mat_Pub_PalmTop",   "Assets/Textures/Palm_palm_top.png");
        Material palmLeaf  = MatCutout("Mat_Pub_PalmLeaf",  "Assets/Textures/Palm_coconut_palm_leaf.png");
        Material palmFrond = MatCutout("Mat_Pub_PalmFrond", "Assets/Textures/Palm_frond_stem.png");
        Material palmNut   = MatCutout("Mat_Pub_PalmNut",   "Assets/Textures/Palm_coconut_2.png");
        Material[] palmBits = { palmTrunk, palmTop, palmLeaf, palmFrond, palmNut };

        // Slot 0 is deadbranches, slot 1 the canopy -- that is the order the
        // Blender join produced, checked rather than assumed.
        Material treeDead   = MatCutout("Mat_Pub_TreeDead",   "Assets/Textures/Tree_DeadBranch.png");
        Material treeCanopy = MatCutout("Mat_Pub_TreeCanopy", "Assets/Textures/Tree_Canopy.png");

        Transform trees = Group(g, "Trees");
        System.Random treeRNG = new System.Random(70310926);
        var trunks = new List<Vector3>();

        for (int i = 0; i < 3; i++)
        {
            bool isPalm = i > 0;                       // one tree, two palms

            // Clearance is for the TRUNK, not the canopy. Demanding the whole
            // canopy fit clear placed only one of three: the tree is 7 m across
            // and the side yard is 5 m wide, so no position could satisfy it.
            // Real canopies overhang walls; only the trunk has to stand
            // somewhere sensible.
            float clearance = isPalm ? 1.2f : 1.6f;

            Vector3 p = Vector3.zero;
            bool ok = false;
            for (int attempt = 0; attempt < 200 && !ok; attempt++)
            {
                p = new Vector3(
                    (float)(treeRNG.NextDouble() * yardX - yardX * 0.5f),
                    TreeSink,
                    (float)(treeRNG.NextDouble() * yardZ - yardZ * 0.5f) + cCZ);

                if (Mathf.Abs(p.x) < bldHalfW + clearance &&
                    Mathf.Abs(p.z) < bldHalfD + clearance) continue;   // off the building
                if (Mathf.Abs(p.x) > yardX * 0.5f - clearance - 0.6f) continue;   // off the wall
                if (Mathf.Abs(p.z - cCZ) > yardZ * 0.5f - clearance - 0.6f) continue;
                if (p.z < entFrontZ && Mathf.Abs(p.x) < pathHalfW + clearance)
                    continue;                                          // off the entrance path

                // Canopy radius plus air: a palm is 3.4 m across, so a trunk
                // any closer than this puts fronds over the lamp.
                foreach (Vector3 lamp in ExteriorLampMounts())
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lamp.x, lamp.z)) < 4.2f)
                        goto rejected;

                ok = true;
                foreach (Vector3 q in trunks)
                    if (Vector3.Distance(p, q) < 5f) { ok = false; break; }
                rejected: ;
            }
            if (!ok)
                continue;
            trunks.Add(p);

            GameObject go = Model(isPalm ? "Asset_Palm" : "Asset_Tree", trees, p,
                                  (float)treeRNG.NextDouble() * 360f,
                                  isPalm ? palmTrunk : treeCanopy,
                                  slotMaterials: isPalm ? null
                                               : new[] { treeDead, treeCanopy });
            if (go == null)
                continue;
            go.name = isPalm ? "Palm_" + i : "Tree";

            if (isPalm)
            {
                foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
                    for (int k = 0; k < palmBits.Length; k++)
                        if (mesh == "Tree_" + k)
                            mr.sharedMaterial = palmBits[k];
                }
            }

            // Trees DO cast, unlike the grass. There are only three of them, and
            // a 9 m tree in an open yard with no shadow is the one thing that
            // gives away that the sun is not real. The sun is soft
            // (LightShadows.Soft at shadowStrength 0.46) so what lands is a
            // diffuse pool rather than a hard cut-out.
            //
            // This is a deliberate re-entry into the shadow budget that was cut
            // right back this afternoon, and alpha-cut canopies are the
            // expensive case -- worth watching PERF after.
            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        Debug.Log($"[PubEnvironment] Trees: {trunks.Count} placed " +
                  string.Join(", ", trunks.ConvertAll(v => v.ToString("F1"))));

        // ---- Ground bushes ----
        //
        // Corner-weighted rather than evenly scattered: rubbish and growth
        // collect where two walls meet and nobody sweeps, so most sit near a
        // corner and the rest fall anywhere. They are GROUND plants -- nothing
        // is placed against a wall face, only on the dirt.
        //
        // 5,000 triangles each, which is where this model stops shredding
        // (2,400 turns its leaves into angular shards). They go on the
        // GroundCover layer so the 17 m cull applies; nine of them is 45,000
        // triangles and only the near ones are ever drawn.
        Material bushBark = M("Mat_Pub_BushBark");
        Material bushLeaf = M("Mat_Pub_BushLeaf");
        Transform bushes = Group(g, "GroundBushes");

        // Three, at 40,500 triangles each.
        //
        // The count came down twice and the per-bush budget went up three times,
        // and the measurements say that was the right direction: nine bushes at
        // 5,000 held 15/21 frame windows on budget, four at 12,000 held 16/21,
        // three at 18,500 held 18/20 -- MORE triangles each time, and better.
        //
        // The cost here is per-OBJECT (draw calls, culling, batching), not per
        // triangle. So density inside a few objects is close to free while
        // scattering many cheap ones is not, and the foliage budget is spent
        // accordingly.
        const int BushCount = 3;
        const float BushCornerBias = 0.55f;     // rest land anywhere in the yard
        var corners = new List<Vector2>
        {
            new Vector2(-cHW, fZ), new Vector2(cHW, fZ),      // compound corners
            new Vector2(-cHW, bZ), new Vector2(cHW, bZ),
            new Vector2(-bldHalfW, -bldHalfD), new Vector2(bldHalfW, -bldHalfD),
            new Vector2(-bldHalfW,  bldHalfD), new Vector2(bldHalfW,  bldHalfD),
        };

        System.Random bushRNG = new System.Random(881204);
        float BRand(float lo, float hi) { return lo + (float)bushRNG.NextDouble() * (hi - lo); }

        var bushAt = new List<Vector3>();
        for (int i = 0; i < BushCount; i++)
        {
            Vector3 p = Vector3.zero;
            bool ok = false;
            for (int attempt = 0; attempt < 60 && !ok; attempt++)
            {
                if (bushRNG.NextDouble() < BushCornerBias)
                {
                    Vector2 c = corners[bushRNG.Next(corners.Count)];
                    // pull IN from the corner, never through it
                    p = new Vector3(c.x - Mathf.Sign(c.x) * BRand(0.8f, 2.9f), GroundSink,
                                    c.y - Mathf.Sign(c.y - cCZ) * BRand(0.8f, 2.9f));
                }
                else
                {
                    p = new Vector3(BRand(-yardX * 0.5f + 1.1f, yardX * 0.5f - 1.1f), GroundSink,
                                    BRand(cCZ - yardZ * 0.5f + 1.1f, cCZ + yardZ * 0.5f - 1.1f));
                }

                if (Mathf.Abs(p.x) <= bldHalfW && Mathf.Abs(p.z) <= bldHalfD) continue;
                if (Mathf.Abs(p.x) > yardX * 0.5f - 1.0f) continue;
                if (Mathf.Abs(p.z - cCZ) > yardZ * 0.5f - 1.0f) continue;
                if (p.z < entFrontZ && Mathf.Abs(p.x) < pathHalfW + 0.9f) continue;

                ok = true;
                foreach (Vector3 q in bushAt)
                    if (Vector3.Distance(p, q) < 1.7f) { ok = false; break; }
                foreach (Vector3 q in trunks)
                    if (Vector3.Distance(p, q) < 2.2f) { ok = false; break; }
            }
            if (!ok) continue;
            bushAt.Add(p);

            GameObject go = Model("Asset_GroundBush", bushes, p, BRand(0f, 360f), bushBark,
                                  slotMaterials: new[] { bushBark, bushLeaf });
            if (go == null) continue;
            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (Transform tr in go.GetComponentsInChildren<Transform>(true))
                tr.gameObject.layer = vegLayer;
        }
        Debug.Log($"[PubEnvironment] Ground bushes: {bushAt.Count} placed.");


        Debug.Log($"[PubEnvironment] Ground cover: {placed} placed over {openArea:F0} m2 " +
                  $"({tufts} tufts + {weeds} weeds + {ShrubCount} shrubs requested); " +
                  $"{skippedPath} skipped on the entrance path, " +
                  $"{thinnedEntrance} thinned from the apron to {EntranceCoverage:P0}.");

        // The worn path is deferred. It was a hard-edged box, which reads as a
        // slab dropped on the yard however good the dirt looks -- real trodden
        // earth has no edge, it fades. Fixing that needs a soft alpha falloff,
        // not a better texture, so it comes back as its own piece of work.

        // Street / road outside the compound gate.
        //
        // The texture is tiled from the road's real size rather than a fixed
        // number, at 4 m a repeat, so the patched asphalt stays at life scale.
        // Its lane markings were rotated 90 degrees into Assets/Textures/Road.png
        // (see BlenderAssets -- and the normal map's R/G were remapped to match
        // the rotation, not just spun round) because a cube's top face maps V to
        // Z, and unrotated the markings ran ACROSS the carriageway.
        float roadW = cHW * 2f + 10f;
        const float roadD = 10f;
        const float RoadTileMetres = 4f;
        Material road = M("Mat_Pub_Road");
        Tile(road, new Vector2(roadW / RoadTileMetres, roadD / RoadTileMetres));
        Box("StreetGround", ground, new Vector3(0f, -0.05f, fZ - 5f),
            new Vector3(roadW, 0.06f, roadD), road, true);

        // ---- Exterior wall lights ----
        //
        // One over the entrance and one at each end of both side walls. The
        // building's outside was lit only by an overhead sun, which grazes a
        // vertical wall and leaves it reading as a flat dark slab -- these give
        // the brickwork something to catch.
        Transform extLights = Group(g, "ExteriorLights");
        Vector3[] lamps = ExteriorLampMounts();
        string[] lampNames = { "ExtLight_Entrance", "ExtLight_L_Front", "ExtLight_L_Back",
                               "ExtLight_R_Front", "ExtLight_R_Back" };
        Vector3[] lampOut = { Vector3.back, Vector3.left, Vector3.left,
                              Vector3.right, Vector3.right };
        for (int i = 0; i < lamps.Length; i++)
            WallLight(extLights, lampNames[i], lamps[i], lampOut[i]);

        // ---- Compound walls ----
        Transform walls = Group(g, "CompoundWalls");

        Box("CWall_Left", walls,
            new Vector3(-cHW - t * 0.5f, wH * 0.5f, cCZ),
            new Vector3(t, wH, cDep), wall, true);

        Box("CWall_Right", walls,
            new Vector3(cHW + t * 0.5f, wH * 0.5f, cCZ),
            new Vector3(t, wH, cDep), wall, true);

        Box("CWall_Back", walls,
            new Vector3(0f, wH * 0.5f, bZ + t * 0.5f),
            new Vector3(cHW * 2f + t * 2f, wH, t), wall, true);

        // Front wall - left of gate
        float segW = cHW - GateOpeningW * 0.5f;
        Box("CWall_Front_L", walls,
            new Vector3(-GateOpeningW * 0.5f - segW * 0.5f, wH * 0.5f, fZ - t * 0.5f),
            new Vector3(segW, wH, t), wall, true);

        // Front wall - right of gate
        Box("CWall_Front_R", walls,
            new Vector3(GateOpeningW * 0.5f + segW * 0.5f, wH * 0.5f, fZ - t * 0.5f),
            new Vector3(segW, wH, t), wall, true);

        // Concrete caps on top of compound walls
        float capH = 0.08f;
        float capW = t + 0.06f;
        Box("Cap_Left", walls,
            new Vector3(-cHW - t * 0.5f, wH + capH * 0.5f, cCZ),
            new Vector3(capW, capH, cDep + 0.1f), pole);
        Box("Cap_Right", walls,
            new Vector3(cHW + t * 0.5f, wH + capH * 0.5f, cCZ),
            new Vector3(capW, capH, cDep + 0.1f), pole);
        Box("Cap_Back", walls,
            new Vector3(0f, wH + capH * 0.5f, bZ + t * 0.5f),
            new Vector3(cHW * 2f + t * 2f + 0.1f, capH, capW), pole);
        Box("Cap_Front_L", walls,
            new Vector3(-GateOpeningW * 0.5f - segW * 0.5f, wH + capH * 0.5f, fZ - t * 0.5f),
            new Vector3(segW + 0.05f, capH, capW), pole);
        Box("Cap_Front_R", walls,
            new Vector3(GateOpeningW * 0.5f + segW * 0.5f, wH + capH * 0.5f, fZ - t * 0.5f),
            new Vector3(segW + 0.05f, capH, capW), pole);

        // ---- Gate posts (concrete pillars) ----
        Transform gate = Group(g, "GateEntrance");
        float postS = 0.32f;
        float postH = wH + 0.4f;

        Box("GatePost_L", gate,
            new Vector3(-GateOpeningW * 0.5f, postH * 0.5f, fZ),
            new Vector3(postS, postH, postS), pole);
        Box("GatePost_R", gate,
            new Vector3(GateOpeningW * 0.5f, postH * 0.5f, fZ),
            new Vector3(postS, postH, postS), pole);

        // ---- Overhead signboard (TASMAC-style) ----
        float signPostH = 4.6f;
        float signW = GateOpeningW + 2.2f;
        float signH = 1.4f;
        float signY = signPostH - signH * 0.5f - 0.15f;

        // Steel support posts rising above the gate
        Tube("SignPost_L", gate,
            new Vector3(-GateOpeningW * 0.5f, signPostH * 0.5f, fZ),
            0.07f, signPostH, steel, Vector3.zero);
        Tube("SignPost_R", gate,
            new Vector3(GateOpeningW * 0.5f, signPostH * 0.5f, fZ),
            0.07f, signPostH, steel, Vector3.zero);

        // Horizontal beams
        Box("SignBeam_Top", gate,
            new Vector3(0f, signPostH, fZ),
            new Vector3(signW + 0.3f, 0.07f, 0.07f), steel);
        Box("SignBeam_Bot", gate,
            new Vector3(0f, signY - signH * 0.5f, fZ),
            new Vector3(signW + 0.3f, 0.06f, 0.06f), steel);

        // Signboard panel (dark green)
        Box("Signboard", gate,
            new Vector3(0f, signY, fZ - 0.04f),
            new Vector3(signW, signH, 0.04f), signGreen);

        // Inner light-box face, a lighter green than the frame.
        Box("Signboard_Inner", gate,
            new Vector3(0f, signY, fZ - 0.07f),
            new Vector3(signW - 0.5f, signH - 0.40f, 0.02f),
            M("Mat_Pub_SignboardLight"));

        // Two floodlights over the board, on short arms.
        //
        // They stand 0.42 m OUT from the panel, not 0.16. A lamp a hand's width
        // from a flat surface lights a hot circle and nothing else, however
        // wide its cone -- the beam has no room to spread before it lands. Out
        // on an arm and angled down, it washes the face the way a real sign
        // floodlight does.
        Material lampBody = M("Mat_Pub_SteelBlue");
        Material lampFace = M("Mat_Pub_Tube") ?? M("Mat_Pub_Bulb");

        // An L-shaped bracket off the TOP BEAM: up from the beam, then forward
        // to the housing. The previous version put a short horizontal stub at
        // 4.71 m while the beam is at 4.60, so it reached over the top of
        // everything and touched nothing -- the lamps hung in the sky.
        float beamY = signPostH;                 // 4.60, the beam they hang from
        float lampY = beamY + 0.34f;
        float lampZ = fZ - 0.42f;
        float postZ = fZ - 0.03f;

        for (int i = -1; i <= 1; i += 2)
        {
            float lx = i * (signW * 0.26f);

            // Upright, standing ON the beam.
            Box("SignLamp_Post_" + i, gate,
                new Vector3(lx, (beamY + lampY) * 0.5f, postZ),
                new Vector3(0.05f, lampY - beamY, 0.05f), lampBody);

            // Arm forward from the top of the upright to the housing.
            Box("SignLamp_Arm_" + i, gate,
                new Vector3(lx, lampY, (postZ + lampZ) * 0.5f),
                new Vector3(0.05f, 0.05f, postZ - lampZ), lampBody);

            Box("SignLamp_Body_" + i, gate,
                new Vector3(lx, lampY, lampZ),
                new Vector3(0.30f, 0.12f, 0.18f), lampBody);
            Box("SignLamp_Face_" + i, gate,
                new Vector3(lx, lampY - 0.055f, lampZ),
                new Vector3(0.26f, 0.03f, 0.15f), lampFace);
        }

        // ---- The two lamps actually light the board ----
        //
        // SPOT lights aimed down the board face, one per housing, and PIXEL
        // rather than vertex: the panel is a box of eight vertices, so a vertex
        // light on it produces a smear rather than a pool and the flicker would
        // barely register.
        //
        // Range 3.0 m is chosen so they CANNOT reach the yard ground. The lamps
        // sit 4.6 m up, and an extra pixel light over a single 23 x 31 m ground
        // box is the expensive kind of mistake -- six pixel lights took this
        // project from 72 fps to 36 once already. Confined to the board they
        // touch roughly thirty small renderers and nothing else.
        Light[] signLamps = new Light[2];

        for (int i = 0; i < 2; i++)
        {
            float lx = (i == 0 ? -1f : 1f) * (signW * 0.26f);
            Vector3 lp = new Vector3(lx, lampY - 0.06f, lampZ);

            // Aimed STRAIGHT DOWN THE FACE from each lamp, not at the middle of
            // the board. Both aimed at the centre is what made two bright spots
            // meeting in the middle and left the ends dark; each washing the
            // panel below itself, with cones wide enough to overlap, covers it
            // end to end the way a lit hoarding actually looks.
            Vector3 aim = new Vector3(lx, signY - signH * 0.40f, fZ - 0.07f);

            GameObject lgo = new GameObject(i == 0 ? "Lamp_SignFlicker" : "Lamp_SignSteady");
            lgo.transform.SetParent(gate, false);
            lgo.transform.localPosition = lp;
            lgo.transform.localRotation = Quaternion.LookRotation((aim - lp).normalized);

            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.80f, 1f, 0.96f);
            // Wide and soft: a narrow cone from this close is a spotlight, and
            // a sign flood is meant to be a wash.
            l.spotAngle = 125f;
            l.innerSpotAngle = 78f;
            l.range = 3.6f;
            l.intensity = i == 0 ? 0f : 4.2f;   // the flicker component drives [0]

            // No shadows. The reasoning for giving these two maps was that a
            // 3.6 m range encloses only the sign, so each map is cheap -- true
            // in isolation, and wrong in the place it mattered. At the entrance
            // the sign and the whole hall are in view TOGETHER, so these two
            // maps stacked on top of the interior ones exactly where the drop
            // was reported. They are also ForcePixel, so they hold two of the
            // four pixel-light slots at that same moment.
            //
            // The sign still reads: the flood lights it, the flicker still
            // drives intensity, and only the lettering's cast shadow is lost.
            l.shadows = LightShadows.None;
            l.shadowStrength = 0.55f;
            l.shadowBias = 0.02f;
            l.shadowNormalBias = 0.3f;
            l.shadowNearPlane = 0.1f;
            l.shadowCustomResolution = 256;
            l.renderMode = LightRenderMode.ForcePixel;
            signLamps[i] = l;
            _objectCount++;
        }

        // ---- The failing tube ----
        //
        // ONE of the two lamps flickers; the other stays lit, so the sign never
        // goes fully dark and the fault reads as one failing tube rather than a
        // broken sign.
        Transform flickerFace = gate.Find("SignLamp_Face_-1");
        if (flickerFace != null)
        {
            var lamp = flickerFace.gameObject.AddComponent<FlickeringLamp>();
            lamp.TargetLight = signLamps[0];
            lamp.PeakIntensity = 4.6f;
            lamp.GlowRenderer = flickerFace.GetComponent<Renderer>();
            lamp.Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/Sign/FluorescentFlicker.wav");
            lamp.EnvelopeHz = 60f;
            lamp.Envelope = BuildEnvelope(lamp.Clip, lamp.EnvelopeHz);
        }
        else
        {
            Debug.LogWarning("[PubEnvironment] SignLamp_Face_-1 not found; no flicker.");
        }

        // "TASMAC" across the board, in dark green on the cream panel -- the
        // way these signs are actually painted. Letters are BUILT, not
        // rendered from a font: a TextMesh would drag in a font atlas and an
        // unlit transparent shader for six characters, and this environment
        // already makes everything else out of boxes.
        SignText(gate, "TASMAC",
                 new Vector3(0f, signY, fZ - 0.095f),
                 signW - 0.9f,          // leave a margin inside the lit panel
                 (signH - 0.40f) * 0.62f,
                 M("Mat_Pub_SignboardText"));

        // ---- Gate leaves (half-height corrugated steel, slightly ajar) ----
        //
        // Same construction as the entrance doors: a leaf slab carrying the
        // front sheet plus a thin skin on its reverse carrying the INVERTED
        // normal map, because a box has one material and the back of a
        // corrugated sheet is the negative of its front. See back_normal.py.
        //
        // Tiled from the leaf's real size at 1 m a repeat. The across count is
        // rounded to a whole number so a corrugation is never cut in half at
        // the leaf's edge.
        Transform leaves = Group(g, "GateLeaves");
        float leafW = GateOpeningW * 0.5f - 0.10f;
        float leafH = 1.5f;

        Material gateFront = M("Mat_Pub_GateSteel");
        Material gateBack  = M("Mat_Pub_GateSteelBack");
        Vector2 gateTile = new Vector2(
            Mathf.Max(1f, Mathf.Round(leafW / CorrugationTileMetres)),
            leafH / CorrugationTileMetres);
        Tile(gateFront, gateTile);
        Tile(gateBack, gateTile);
        const float gateSkinT = 0.010f;

        // Left leaf - hinged on left post, swung ~20 deg open
        Transform leafL = Group(leaves, "Leaf_L");
        leafL.localPosition = new Vector3(-GateOpeningW * 0.5f + 0.06f, 0f, fZ);
        leafL.localEulerAngles = new Vector3(0f, -90f, 0f);
        Box("Frame_L", leafL,
            new Vector3(leafW * 0.5f, leafH * 0.5f + 0.02f, 0f),
            new Vector3(leafW, leafH, 0.045f), gateFront);
        Box("Skin_L", leafL,
            new Vector3(leafW * 0.5f, leafH * 0.5f + 0.02f,
                        -(0.0225f + gateSkinT * 0.5f)),
            new Vector3(leafW, leafH, gateSkinT), gateBack);
        SheetFrame(leafL, new Vector3(leafW * 0.5f, leafH * 0.5f + 0.02f, 0f),
                   leafW, leafH, 0.045f + gateSkinT, steel);

        // Right leaf - hinged on right post, swung ~20 deg open
        Transform leafR = Group(leaves, "Leaf_R");
        leafR.localPosition = new Vector3(GateOpeningW * 0.5f - 0.06f, 0f, fZ);
        leafR.localEulerAngles = new Vector3(0f, 90f, 0f);
        Box("Frame_R", leafR,
            new Vector3(-leafW * 0.5f, leafH * 0.5f + 0.02f, 0f),
            new Vector3(leafW, leafH, 0.045f), gateFront);
        Box("Skin_R", leafR,
            new Vector3(-leafW * 0.5f, leafH * 0.5f + 0.02f,
                        -(0.0225f + gateSkinT * 0.5f)),
            new Vector3(leafW, leafH, gateSkinT), gateBack);
        SheetFrame(leafR, new Vector3(-leafW * 0.5f, leafH * 0.5f + 0.02f, 0f),
                   leafW, leafH, 0.045f + gateSkinT, steel);

        // ---- Utility pole ----
        Transform utilPole = Group(g, "UtilityPole");
        float poleX = GateOpeningW * 0.5f + 3.5f;
        float poleFZ = fZ - 1.5f;

        Tube("Pole_Shaft", utilPole,
            new Vector3(poleX, 3.2f, poleFZ),
            0.14f, 6.4f, pole, Vector3.zero);
        Box("Pole_CrossArm", utilPole,
            new Vector3(poleX, 6.2f, poleFZ),
            new Vector3(1.6f, 0.09f, 0.09f), steel);

        // Insulators on cross arm
        for (int i = 0; i < 3; i++)
        {
            Tube("Insulator_" + i, utilPole,
                 new Vector3(poleX - 0.6f + i * 0.6f, 6.32f, poleFZ),
                 0.05f, 0.06f, pole, Vector3.zero);
        }

        // Service drop from the pole to the signboard.
        //
        // A lit sign has to be fed from somewhere, and the pole is already
        // there -- without the wire the board reads as glowing by magic. Built
        // as short straight segments following a PARABOLIC SAG rather than one
        // straight line, because a taut horizontal cable is the giveaway that
        // something is CG; real span wire always hangs.
        SagWire(utilPole, "ServiceDrop",
                new Vector3(poleX - 0.6f, 6.28f, poleFZ),               // insulator
                new Vector3(signW * 0.5f - 0.1f, signY + signH * 0.5f, fZ - 0.05f),
                0.42f, M("Mat_Pub_SignWire"));

        // ---- Signs on compound walls ----
        Transform signs = Group(g, "CompoundSigns");

        // Green sign on inside of left compound wall
        Box("YardSign_L", signs,
            new Vector3(-cHW + 0.03f, 1.85f, fZ + 3.5f),
            new Vector3(0.04f, 0.85f, 2.0f), signGreen);
        Box("YardSign_L_Text", signs,
            new Vector3(-cHW + 0.06f, 1.85f, fZ + 3.5f),
            new Vector3(0.02f, 0.65f, 1.7f), cream);

        // Sign on outside of front wall (facing street)
        Box("StreetSign", signs,
            new Vector3(GateOpeningW * 0.5f + 2.2f, 1.8f, fZ - t - 0.03f),
            new Vector3(1.6f, 0.9f, 0.04f), signGreen);

        // ---- Entrance area light ----
        PointLight(g, "Lamp_Gate",
            new Vector3(0f, 3.8f, fZ + 1.0f),
            new Color(1f, 0.94f, 0.82f), 1.4f, 12f);
    }

    // --------------------------------------------------------------- player

    static void BuildPlayer(Transform g)
    {
        // Disable the old Main Camera so the player camera takes over.
        GameObject oldCam = GameObject.Find("Main Camera");
        if (oldCam != null)
            oldCam.SetActive(false);

        if (PlayerRig == RigMode.VR)
        {
            // Spawn on the street outside the compound gate, facing the entrance.
            // Y = 0: in Floor tracking mode the headset supplies eye height, so
            // the rig root sits on the ground rather than at capsule centre.
            XRRigBuilder.Build(g, new Vector3(0f, 0f, YardFrontZ - 3.0f), 0f);
            _objectCount += 5;
            Debug.Log("[PubEnvironment] XR rig spawned outside the gate (OpenXR).");
            return;
        }

        // Create the player: a capsule with CharacterController + camera.
        GameObject player = new GameObject("Player");
        player.transform.SetParent(g, false);

        // Spawn on the street outside the compound gate, facing the entrance.
        player.transform.localPosition = new Vector3(0f, 0.5f, YardFrontZ - 3.0f);
        player.transform.localEulerAngles = new Vector3(0f, 0f, 0f);

        // CharacterController defines the collision capsule.
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.875f, 0f);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.3f;

        // Attach the first-person controller script.
        player.AddComponent<FirstPersonController>();

        // Camera at eye level inside the capsule.
        GameObject camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        camGo.tag = "MainCamera";

        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 63f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 80f;

        // Audio listener so spatial audio works later.
        camGo.AddComponent<AudioListener>();

        _objectCount += 2;

        Debug.Log("[PubEnvironment] Player spawned near entrance.");
    }


    static void BuildRoofStructure(Transform g)
    {
        Material steel = M("Mat_Pub_SteelBlue");
        Material sheet = M("Mat_Pub_RoofSheet");
        Material sky = M("Mat_Pub_Skylight");

        Transform trusses = Group(g, "Trusses");
        Transform pillars = Group(g, "Pillars");
        Transform purlins = Group(g, "Purlins");
        Transform panels = Group(g, "RoofPanels");
        Transform ribs = Group(g, "Corrugation");

        for (int i = 0; i < TrussCount; i++)
        {
            float z = TrussZ(i);
            BuildTruss(trusses, "Truss_" + i, z, steel);

            Box("Pillar_L_" + i, pillars, new Vector3(-HalfW + 0.12f, EavesHeight * 0.5f, z),
                new Vector3(0.22f, EavesHeight, 0.22f), steel, true);
            Box("Pillar_R_" + i, pillars, new Vector3(HalfW - 0.12f, EavesHeight * 0.5f, z),
                new Vector3(0.22f, EavesHeight, 0.22f), steel, true);
        }

        // Purlins running the length of the hall.
        for (int i = 0; i <= 6; i++)
        {
            float x = -HalfW + i * (HallWidth / 6f);
            // Sits on the truss top chord, under the ribs and sheeting.
            Box("Purlin_" + i, purlins, new Vector3(x, RoofY(x) + 0.045f, 0f),
                new Vector3(0.09f, 0.09f, HallDepth + 0.6f), steel);
        }

        // Roof sheeting, split along Z so some bays can be translucent skylights.
        const int segs = 8;
        float segLen = (HallDepth + 0.6f) / segs;
        float slopeLen = Mathf.Sqrt(HalfW * HalfW + RidgeRise * RidgeRise) + 0.35f;
        float slopeAng = Mathf.Atan2(RidgeRise, HalfW) * Mathf.Rad2Deg;

        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            for (int i = 0; i < segs; i++)
            {
                float z = -(HallDepth + 0.6f) * 0.5f + segLen * (i + 0.5f);
                // Roof closed: the interior is lit by its bulbs, not by daylight
                // through the roof. Flip RoofSkylights to restore the open bays.
                bool skylight = RoofSkylights && (i == 2 || i == 5);
                // Alternate panels sit slightly proud, so a lapped pair is
                // never coplanar. Overlapping them at the SAME height made the
                // overlap z-fight -- the depth buffer cannot order two faces in
                // the same plane, so they flicker. Real sheets lap over each
                // other anyway, so this is also what the roof should look like.
                float lap = (i % 2 == 0) ? 0f : 0.045f;

                Box("Roof_" + (side == 0 ? "L" : "R") + "_" + i, panels,
                    new Vector3(sign * HalfW * 0.5f,
                                EavesHeight + RidgeRise * 0.5f + 0.17f + lap, z),
                    // 1.10, not 0.98. At 98 % each panel fell 2 % short of its
                    // segment, leaving a ~5 cm slot between all 8 panels per
                    // side -- the dashed lines of sunlight across the floor.
                    // Real corrugated sheets overlap; these now do too.
                    new Vector3(slopeLen, 0.06f, segLen * 1.06f),
                    skylight ? sky : sheet)
                    .transform.localEulerAngles = new Vector3(0f, 0f, sign * -slopeAng);
            }
        }

        // Corrugation ribs on the underside, running down the slope.
        int ribCount = Mathf.Max(1, Mathf.RoundToInt((HallDepth + 0.6f) / RoofRibSpacing));
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            for (int i = 0; i < ribCount; i++)
            {
                float z = -(HallDepth + 0.6f) * 0.5f + RoofRibSpacing * (i + 0.5f);
                BoxR("Rib_" + (side == 0 ? "L" : "R") + "_" + i, ribs,
                     new Vector3(sign * HalfW * 0.5f, EavesHeight + RidgeRise * 0.5f + 0.11f, z),
                     new Vector3(slopeLen, 0.045f, 0.07f), sheet,
                     new Vector3(0f, 0f, sign * -slopeAng));
            }
        }
    }


    static void BuildGable(Transform g, string name, float z, Material mat)
    {
        Transform gg = Group(g, name);
        const int steps = 4;
        for (int i = 0; i < steps; i++)
        {
            float x0 = -HalfW + i * (HalfW / steps);
            float x1 = -HalfW + (i + 1) * (HalfW / steps);
            float h = RoofY((x0 + x1) * 0.5f) - EavesHeight;
            if (h <= 0.01f) continue;
            float w = x1 - x0;
            Box(name + "_L" + i, gg, new Vector3((x0 + x1) * 0.5f, EavesHeight + h * 0.5f, z),
                new Vector3(w, h, WallThickness), mat);
            Box(name + "_R" + i, gg, new Vector3(-(x0 + x1) * 0.5f, EavesHeight + h * 0.5f, z),
                new Vector3(w, h, WallThickness), mat);
        }
    }


    static void BuildClutter(Transform g)
    {
        Material caseMat = M("Mat_Pub_WaterCase");
        Material crate = M("Mat_Pub_CrateGreen");
        Material litter = M("Mat_Pub_Litter");
        Material amber = M("Mat_Pub_GlassAmber");
        Material green = M("Mat_Pub_GlassGreen");

        // Cardboard cartons of stock against both side walls.
        //
        // This used to be 9 columns up to 4 high on the left plus 4 columns of 2
        // on the right -- as many as 44 flat-shaded cubes forming a wall of
        // filler that read as scenery geometry rather than as goods. Now four on
        // the left and three on the right, from a real model.
        //
        // Asset_CardboardBox has its origin at the centre of its BASE, so a box
        // placed at y = 0 rests on the floor and a stacked one sits at exactly
        // one box height. No half-buried boxes to hand-correct.
        Transform cases = Group(g, "WaterCases");
        Material carton = M("Mat_Pub_CardboardBox");
        const float boxH = 0.55f;

        Vector3[] cartons =
        {
            new Vector3(-HalfW + 0.42f, 0f,        HalfD - 2.20f),   // left, stacked pair
            new Vector3(-HalfW + 0.42f, boxH,      HalfD - 2.20f),
            new Vector3(-HalfW + 0.44f, 0f,        HalfD - 2.86f),   // left, stacked pair
            new Vector3(-HalfW + 0.44f, boxH,      HalfD - 2.86f),
            new Vector3(HalfW - 0.45f,  0f,        HalfD - 2.60f),   // right, stacked pair
            new Vector3(HalfW - 0.45f,  boxH,      HalfD - 2.60f),
            new Vector3(HalfW - 0.47f,  0f,        HalfD - 3.26f),   // right, single
        };

        for (int i = 0; i < cartons.Length; i++)
        {
            // A few degrees of yaw so a stack looks set down rather than placed.
            GameObject box = Model("Asset_CardboardBox", cases, cartons[i],
                                   Rand(-7f, 7f), carton);
            if (box != null)
                box.name = "Carton_" + i;
            else
                Box("Case_" + i, cases, cartons[i] + new Vector3(0f, boxH * 0.5f, 0f),
                    new Vector3(0.55f, boxH, 0.55f), caseMat);
        }

        // Green crates by the counter.
        //
        // Clear of COUNTER_RETURN, the counter's left wing, which spans
        // x -1.405 to -0.555 and z 5.4 to 7.2. The crates used to start at
        // x = -1.4, so both columns stood inside it -- a crate half sunk into
        // the counter, which is what that green box wedged in the corner was.
        // A crate is 0.60 wide, so its right edge must stay left of -1.405:
        // -2.60 and -1.94 both clear it, and they are still against the wing.
        Transform crates = Group(g, "Crates");
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(-2.60f + (i % 2) * 0.66f, 0.155f + (i / 2) * 0.31f, HalfD - 3.5f);
            Crate(crates, "Crate_" + i, p, crate, Rand(-6f, 6f));
        }

        // Bottles and glasses, placed ON real tables rather than scattered at
        // table height (which would leave them floating in the walkways).
        Transform loose = Group(g, "TableClutter");
        int b = 0, gl = 0;
        for (int i = 0; i < _tableTops.Count; i++)
        {
            Vector3 top = _tableTops[i];
            if (_rng.NextDouble() < 0.35) continue;      // leave some tables clear

            // Positions are sampled inside a CIRCLE now.
            //
            // They used to come from a +/-0.42 x +/-0.26 rectangle, which fitted
            // the old 1.10 x 0.76 table. Against a disc of radius 0.367 a corner
            // sample lands at r = 0.50 -- past the rim, in mid-air -- so on the
            // first physics step it dropped to the floor. That is why the room
            // filled with fallen bottles.
            //
            // 0.30 m of usable radius leaves room for the widest prop (0.045)
            // plus a margin before the edge.
            var placed = new List<Vector2>();

            int bottles = RandInt(1, 4);
            for (int j = 0; j < bottles; j++)
            {
                Vector3 p = top + OnTable(placed, 0.30f, 0.12f);
                Bottle(loose, "TBottle_" + (b++), p, (j % 2 == 0) ? amber : green, 0.95f, true);
            }
            int glasses = RandInt(1, 3);
            for (int j = 0; j < glasses; j++)
            {
                Vector3 p = top + OnTable(placed, 0.30f, 0.10f) + Vector3.up * 0.045f;
                Transform gGroup = Group(loose, "Glass_" + (gl++));
                gGroup.localPosition = p;

                // Alternate glass tumblers and steel tumblers, as in the photos.
                string gModel = (gl % 2 == 0) ? "Asset_Glass" : "Asset_SteelTumbler";
                Material gMat = (gl % 2 == 0) ? litter : M("Mat_Pub_Metal");

                if (Model(gModel, gGroup, Vector3.zero, Rand(0f, 360f), gMat,
                          isStatic: false) == null)
                {
                    Tube("Glass_Body", gGroup, Vector3.zero, 0.065f, 0.10f, litter, Vector3.zero);
                }

                GrabbableUpright(gGroup, 0.040f, 0.10f, 0.25f);
            }
        }

        Transform floorJunk = Group(g, "FloorLitter");
        for (int i = 0; i < 10; i++)
        {
            float x = Rand(-HalfW + 1.2f, HalfW - 1.2f);
            float z = Rand(-HalfD + 1.5f, HalfD - 4.5f);
            BoxR("Litter_" + i, floorJunk, new Vector3(x, 0.006f, z),
                 new Vector3(Rand(0.08f, 0.22f), 0.012f, Rand(0.08f, 0.20f)), litter,
                 new Vector3(0f, Rand(0f, 180f), 0f));
        }

        // A couple of loose bottles on the floor.
        for (int i = 0; i < 3; i++)
        {
            Bottle(floorJunk, "FloorBottle_" + i,
                   new Vector3(Rand(-4f, 4f), 0.04f, Rand(-6f, 6f)), green, 0.95f, true);
        }
    }
}
