using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only. Adds Tools > Rebuild Levels > Level 4 Boss Room.
// The final boss room at the end of the flooded hallway (Level_4): a big flooded hall.
//   Arena inside x 0-40, floor top at y 0, ceiling at y 18. You come in on the left.
//   Two low side ledges (top at y 4), one high middle ledge (top at y 8), two hook rings above.
//   Water: ankle-deep water on the floor, two waterfalls pouring from the ceiling, drips, blue lanterns.
// The boss is not in it yet: "BossSpot (empty)" on the right marks where he will stand.
public static class Level4BossRoomBuilder
{
    const string SceneName = "Level_4_Boss";
    const int Width = 40;       // inside x 0 to 40
    const int CeilingY = 18;

    static readonly Color StoneColor = new Color(0.3f, 0.4f, 0.48f);
    static readonly Color BackWall = new Color(0.05f, 0.08f, 0.13f);
    static readonly Color ShallowColor = new Color(0.25f, 0.55f, 0.75f, 0.45f);
    static readonly Color FallColor = new Color(0.45f, 0.75f, 0.95f, 0.35f);
    static readonly Color OneWayColor = new Color(0.4f, 0.45f, 0.5f);
    static readonly Color LanternColor = new Color(0.5f, 0.85f, 1f);

    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 4 Boss Room")]
    static void Build()
    {
        if (!NewSceneFromTemplate(SceneName, out Scene scene)) return;
        Map.color = StoneColor;

        var root = new GameObject("FloodedThrone").transform;
        var decor = Group("Decor", root);
        var props = Group("Props", root);

        // Shell: floor, ceiling, side walls
        Fill(-3, -3, Width + 6, 3);
        Fill(-3, CeilingY, Width + 6, 3);
        Fill(-3, 0, 3, CeilingY);
        Fill(Width, 0, 3, CeilingY);

        // Platforms and hook rings for moving around during the fight
        OneWayAt("SideLedge_Left", props, new Vector2(7f, 3.75f), 4f, OneWayColor);
        OneWayAt("SideLedge_Right", props, new Vector2(33f, 3.75f), 4f, OneWayColor);
        OneWayAt("MiddleLedge", props, new Vector2(20f, 7.75f), 5f, OneWayColor);
        Ring("Ring_Left", props, new Vector2(12f, 13f), 6f);
        Ring("Ring_Right", props, new Vector2(28f, 13f), 6f);

        // Where the boss will stand (empty for now)
        new GameObject("BossSpot (empty)").transform.position = new Vector3(32f, 0f, 0f);

        BuildWaterLook(decor);
        SetGlobalLight(scene, 0.55f, new Color(0.65f, 0.85f, 1f));

        Transform player = PlacePlayer(new Vector2(3f, 0f));
        SetUpCamera(player, 9f, new Vector2(-1f, -2f), new Vector2(Width + 1f, CeilingY + 1f));

        Finish(scene, SceneName, "Done! " + SceneName + " is built, open, and in the Scene List.\n\n" +
            "The door at the end of Level_4 now leads here. The boss isn't in it yet: 'BossSpot (empty)' marks his place.");
    }

    // ---------------- The father ----------------

    const int FatherHealth = 800; // the toughest boss (see Tools > Level 4 Boss > Make KK Harder)
    static readonly Vector2 FatherSize = new Vector2(2f, 4f);

    // Puts the father (placeholder box) on "BossSpot (empty)" with his health bar. Running it again replaces him.
    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 4 Boss: Add KK")]
    static void AddFather()
    {
        string path = ScenesFolder + "/" + SceneName + ".unity";
        if (!System.IO.File.Exists(path))
        {
            EditorUtility.DisplayDialog("Add Father", "Build the room first (Tools > Rebuild Levels > Level 4 Boss Room).", "OK");
            return;
        }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);

        // Remove an older father and his bar
        FatherBoss old = Object.FindAnyObjectByType<FatherBoss>(FindObjectsInactive.Include);
        if (old)
        {
            var oldSo = new SerializedObject(old);
            Object oldBar = oldSo.FindProperty("healthBarRoot").objectReferenceValue;
            if (oldBar) Object.DestroyImmediate(oldBar);
            Object.DestroyImmediate(old.gameObject);
        }

        // LevelKit's box sprite and ground layer (normally set when a scene is built)
        BoxSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Square.asset")?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");

        GameObject spot = GameObject.Find("BossSpot (empty)");
        float x = spot ? spot.transform.position.x : 32f;
        GameObject father = Box("Father", null, new Vector2(x, FatherSize.y * 0.5f), FatherSize, new Color(0.12f, 0.16f, 0.3f));
        father.GetComponent<SpriteRenderer>().sortingOrder = 5;
        var rb = father.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        AddHealth(father, FatherHealth);
        GameObject bar = BossBar(father.GetComponent<Health>(), "KK", new Color(0.3f, 0.6f, 0.95f));

        var so = new SerializedObject(father.AddComponent<FatherBoss>());
        so.FindProperty("groundY").floatValue = 0f;
        so.FindProperty("arenaMinX").floatValue = 1f;
        so.FindProperty("arenaMaxX").floatValue = Width - 1f;
        so.FindProperty("healthBarRoot").objectReferenceValue = bar;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = father;
        EditorUtility.DisplayDialog("Add Father",
            "The father is in Level_4_Boss (placeholder box, " + FatherHealth + " health).\n\n" +
            "Phase 1: he glides over the water and sends lines of waves, small to tall. Each hit soaks you; the 4th soak hits really hard.\n" +
            "Change his numbers on the FatherBoss component.", "OK");
    }

    // ---------------- Water look ----------------

    static void BuildWaterLook(Transform decor)
    {
        // Back wall with rows of stone blocks and big arches
        Deco("BackWall", decor, new Vector2(Width * 0.5f, CeilingY * 0.5f), new Vector2(Width + 6f, CeilingY + 6f), BackWall, -20);
        for (int row = 2; row < CeilingY; row += 2)
            Deco("BackRow", decor, new Vector2(Width * 0.5f, row), new Vector2(Width, 0.08f), new Color(0.03f, 0.06f, 0.1f), -19);
        foreach (float ax in new[] { 10f, 20f, 30f })
        {
            Deco("Pillar", decor, new Vector2(ax - 3f, 6f), new Vector2(0.8f, 12f), new Color(0.1f, 0.15f, 0.21f), -18);
            Deco("Pillar", decor, new Vector2(ax + 3f, 6f), new Vector2(0.8f, 12f), new Color(0.1f, 0.15f, 0.21f), -18);
            Deco("ArchTop", decor, new Vector2(ax, 12.2f), new Vector2(6.8f, 0.6f), new Color(0.1f, 0.15f, 0.21f), -18);
        }

        // Ankle-deep water on the whole floor
        GameObject shallow = Deco("ShallowWater", decor, new Vector2(Width * 0.5f, 0.15f), new Vector2(Width, 0.3f), ShallowColor, 8);
        var so = new SerializedObject(shallow.AddComponent<WaterSurface>());
        so.FindProperty("waveHeight").floatValue = 0.04f;
        so.FindProperty("pieceHeight").floatValue = 0.07f;
        so.FindProperty("sortingOrder").intValue = 9;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Two waterfalls pouring from cracks in the ceiling, in the back (look only)
        foreach (float fx in new[] { 3f, 37f }) // clear of the side ledges
        {
            Deco("Crack", decor, new Vector2(fx, CeilingY - 0.2f), new Vector2(2f, 0.4f), new Color(0.02f, 0.03f, 0.05f), -10);
            Deco("Waterfall", decor, new Vector2(fx, CeilingY * 0.5f), new Vector2(1.6f, CeilingY), FallColor, -9);
            Deco("WaterfallCore", decor, new Vector2(fx, CeilingY * 0.5f), new Vector2(0.6f, CeilingY), new Color(0.7f, 0.9f, 1f, 0.35f), -9);
            for (int i = 0; i < 3; i++) // fast drops along it, so the water looks like it flows
            {
                var stream = new GameObject("WaterfallDrops");
                stream.transform.SetParent(decor);
                stream.transform.position = new Vector3(fx - 0.5f + i * 0.5f, CeilingY - 0.5f, 0f);
                var drip = new SerializedObject(stream.AddComponent<WaterDrip>());
                drip.FindProperty("minWait").floatValue = 0.05f;
                drip.FindProperty("maxWait").floatValue = 0.25f;
                drip.FindProperty("splashSize").floatValue = 0.9f;
                drip.ApplyModifiedPropertiesWithoutUndo();
            }
            Deco("Foam", decor, new Vector2(fx, 0.35f), new Vector2(2.4f, 0.3f), new Color(0.85f, 0.95f, 1f, 0.6f), 9);
        }

        // Drips from the ceiling
        foreach (float dx in new[] { 9f, 15f, 24f, 31f })
        {
            var drip = new GameObject("WaterDrip");
            drip.transform.SetParent(decor);
            drip.transform.position = new Vector3(dx, CeilingY - 0.3f, 0f);
            drip.AddComponent<WaterDrip>();
        }

        // Cold blue lanterns
        foreach (float lx in new[] { 2f, 14f, 26f, 38f })
            Lantern("Lantern_" + Mathf.RoundToInt(lx), decor, new Vector2(lx, 9f), LanternColor);
    }
}
