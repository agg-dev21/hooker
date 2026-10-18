using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only. Adds Tools > Rebuild Levels > Level 4 (Flooded Hall).
// Level 4: one straight flooded hallway, left to right, that leads straight to the boss room.
// No enemies, just a few basic obstacles:
//   low walls to jump, water pits to jump over, a crate wall to punch through.
// Water everywhere: ankle-deep water along the floor, rippling pits, drips from the ceiling.
// Falling into a pit sends you back to the last checkpoint. The door at the end loads Level_4_Boss.
// Made from Room_Template; the exit door of Level_3_Boss already leads here (Level_4).
public static class Level4Builder
{
    const string SceneName = "Level_4";
    const string BossScene = "Level_4_Boss";
    const int Length = 100;     // hallway from x 0 to 100
    const int CeilingY = 10;    // open space from y 0 (floor top) to 10

    static readonly Color StoneColor = new Color(0.32f, 0.42f, 0.5f);
    static readonly Color BackWall = new Color(0.06f, 0.1f, 0.15f);
    static readonly Color WaterColor = new Color(0.1f, 0.4f, 0.6f, 0.75f);
    static readonly Color ShallowColor = new Color(0.25f, 0.55f, 0.75f, 0.45f);
    static readonly Color WallColor = new Color(0.4f, 0.45f, 0.5f);
    static readonly Color CrateColor = new Color(0.55f, 0.38f, 0.2f);
    static readonly Color LanternColor = new Color(0.5f, 0.85f, 1f);

    // Water pits (x from, x to). Everything else along the hallway is floor.
    static readonly Vector2Int[] Pits = { new Vector2Int(16, 20), new Vector2Int(36, 42), new Vector2Int(58, 62), new Vector2Int(64, 68) };

    [MenuItem("Tools/Other/Rebuild Levels (wipes changes)/Level 4 (Flooded Hall)", priority = 3000)]
    static void Build()
    {
        if (!NewSceneFromTemplate(SceneName, out Scene scene)) return;
        Map.color = StoneColor;

        var root = new GameObject("FloodedHall").transform;
        var decor = Group("Decor", root);
        var obstacles = Group("Obstacles", root);
        var props = Group("Props", root);

        // Shell: walls, ceiling, a bottom under the pits
        Fill(-3, -4, 3, CeilingY + 6);         // left wall
        Fill(Length, -4, 3, CeilingY + 6);     // right wall
        Fill(-3, CeilingY, Length + 6, 2);     // ceiling
        Fill(0, -4, Length, 1);                // bottom of the pits

        // Floor with the pits left open
        int x = 0;
        foreach (Vector2Int pit in Pits)
        {
            Fill(x, -1, pit.x - x, 1);
            Pit(decor, obstacles, pit.x, pit.y);
            x = pit.y;
        }
        Fill(x, -1, Length - x, 1);

        // Basic obstacles (all in "Obstacles", so Tools > Obstacles can switch them off)
        Wall("Wall_1", obstacles, 10f, 2f);
        Wall("Wall_2", obstacles, 28f, 3f);
        Breakable("CrateWall", obstacles, new Vector2(50f, 2f), new Vector2(1.5f, 4f), CrateColor, 12); // 3 punches, or jump it
        Wall("Wall_3", obstacles, 78f, 2f);
        Wall("Wall_4", obstacles, 86f, 3f);

        // Checkpoints and the door to the boss
        Checkpoint("Checkpoint_1", props, new Vector2(30f, 0.75f));
        Checkpoint("Checkpoint_2", props, new Vector2(54f, 0.75f));
        Checkpoint("Checkpoint_3", props, new Vector2(72f, 0.75f));
        Door("BossDoor", props, new Vector2(Length - 2.5f, 1.1f), BossScene, new Color(0.15f, 0.3f, 0.55f));

        BuildWaterLook(decor);
        SetGlobalLight(scene, 0.6f, new Color(0.65f, 0.85f, 1f));

        Transform player = PlacePlayer(new Vector2(3f, 0f));
        SetUpCamera(player, 6.5f, new Vector2(-1f, -4f), new Vector2(Length + 1f, CeilingY + 2f));

        Finish(scene, SceneName, "Done! " + SceneName + " is built, open, and in the Scene List.\n\n" +
            "A straight flooded hallway with walls, water pits and a crate wall. The door at the end loads '" + BossScene + "'.");
    }

    // ---------------- Pieces ----------------

    // A water pit: deep water with a rippling surface, and a kill zone at the bottom
    static void Pit(Transform decor, Transform hazards, int x0, int x1)
    {
        float w = x1 - x0;
        GameObject water = Deco("PitWater", decor, new Vector2(x0 + w * 0.5f, -1.65f), new Vector2(w, 2.7f), WaterColor, 3);
        water.AddComponent<WaterSurface>();
        KillZone("KillZone_Pit", hazards, new Vector2(x0 + w * 0.5f, -2.5f), new Vector2(w + 1f, 1f));
    }

    // A plain stone wall standing on the floor (jump over it)
    static void Wall(string name, Transform parent, float centerX, float height)
    {
        GameObject wall = Box(name, parent, new Vector2(centerX, height * 0.5f), new Vector2(1f, height), WallColor);
        wall.layer = GroundLayer;
    }

    // ---------------- Water look ----------------

    static void BuildWaterLook(Transform decor)
    {
        // Wet stone back wall with darker rows of blocks
        Deco("BackWall", decor, new Vector2(Length * 0.5f, CeilingY * 0.5f - 1f), new Vector2(Length + 6f, CeilingY + 6f), BackWall, -20);
        for (int row = 1; row < CeilingY; row += 2)
            Deco("BackRow", decor, new Vector2(Length * 0.5f, row), new Vector2(Length, 0.08f), new Color(0.04f, 0.07f, 0.11f), -19);

        // Ankle-deep water along every piece of floor, in front of the player's feet
        int x = 0;
        foreach (Vector2Int pit in Pits)
        {
            Shallow(decor, x, pit.x);
            x = pit.y;
        }
        Shallow(decor, x, Length);

        // Wet streaks running down the walls, drips from the ceiling
        for (int i = 0; i < 14; i++)
        {
            float sx = 4f + i * 7f;
            Deco("WetStreak", decor, new Vector2(sx, CeilingY - 2f), new Vector2(0.15f, 4f - (i % 3)), new Color(0.2f, 0.4f, 0.55f, 0.5f), -15);
        }
        foreach (float dx in new[] { 6f, 13f, 24f, 33f, 46f, 53f, 71f, 82f, 92f })
        {
            var drip = new GameObject("WaterDrip");
            drip.transform.SetParent(decor);
            drip.transform.position = new Vector3(dx, CeilingY - 0.3f, 0f);
            drip.AddComponent<WaterDrip>();
        }

        // Cold blue lanterns
        foreach (float lx in new[] { 8f, 30f, 52f, 74f, 94f })
            Lantern("Lantern_" + Mathf.RoundToInt(lx), decor, new Vector2(lx, 5f), LanternColor);
    }

    static void Shallow(Transform decor, float x0, float x1)
    {
        float w = x1 - x0;
        GameObject water = Deco("ShallowWater", decor, new Vector2(x0 + w * 0.5f, 0.15f), new Vector2(w, 0.3f), ShallowColor, 8);
        var so = new SerializedObject(water.AddComponent<WaterSurface>());
        so.FindProperty("waveHeight").floatValue = 0.04f;
        so.FindProperty("pieceHeight").floatValue = 0.07f;
        so.FindProperty("sortingOrder").intValue = 9;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
