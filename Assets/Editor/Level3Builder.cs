using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only. Adds Tools > Build Level 3 (Sunken Prison).
// Level 3: the Sunken Prison, the Chain Siren's flooded jail. A long sideways level, left to right:
//   A) Flooded Docks   x 0-39   : small islands over deep water, a moving raft, crabs, a hook ring over the last pool
//   B) Chain Gallery   x 40-63  : low corridor with swinging anchors and chains that slam down from the ceiling
//      The Climb       x 64-79  : one-way steps up to the bridge. A blue breakable wall hides Secret Room 1.
//   C) Tide Bridge     x 74-119 : a high bridge over the tide: hook rings, a moving platform, a shooter and a crab.
//                                 Drop off the moving platform onto a sunken ledge to find Secret Room 2.
//                                 The door at the far right leads to the boss (Level_3_Boss).
// Made from Room_Template; the exit door of Level_2_Boss already leads here (Level_3).
public static class Level3Builder
{
    const string SceneName = "Level_3";
    const string BossScene = "Level_3_Boss";

    static readonly Color StoneColor = new Color(0.36f, 0.5f, 0.55f);
    static readonly Color DeepWater = new Color(0.05f, 0.09f, 0.16f);
    static readonly Color WaterColor = new Color(0.1f, 0.4f, 0.6f, 0.75f);
    static readonly Color WoodColor = new Color(0.4f, 0.27f, 0.15f);
    static readonly Color OneWayColor = new Color(0.45f, 0.4f, 0.35f);
    static readonly Color ChainColor = new Color(0.45f, 0.45f, 0.5f);
    static readonly Color SeaweedColor = new Color(0.15f, 0.45f, 0.3f, 0.85f);
    static readonly Color LanternColor = new Color(0.5f, 0.85f, 1f);

    [MenuItem("Tools/Build Level 3 (Sunken Prison)")]
    static void Build()
    {
        if (!NewSceneFromTemplate(SceneName, out Scene scene)) return;
        Map.color = StoneColor;

        var root = new GameObject("SunkenPrison").transform;
        var decor = Group("Decor", root);
        var hazards = Group("Hazards", root);
        var enemies = Group("Enemies", root);
        var props = Group("Props", root);
        var secrets = Group("SecretRooms", root);

        // Shell
        Fill(-3, -1, 3, 25);   // left wall
        Fill(120, -1, 3, 25);  // right wall
        Fill(-3, 22, 126, 2);  // ceiling

        BuildDocks(decor, hazards, enemies, props);
        BuildGallery(decor, hazards, props);
        BuildClimb(decor, props, secrets);
        BuildBridge(decor, hazards, enemies, props, secrets);
        BuildBackground(decor);
        SetGlobalLight(scene, 0.55f, new Color(0.6f, 0.8f, 1f));

        Transform player = PlacePlayer(new Vector2(3f, 0f));
        SetUpCamera(player, 6.5f, new Vector2(-1f, -2f), new Vector2(121f, 23f));

        Finish(scene, SceneName, "Done! " + SceneName + " is built, open, and in the Scene List.\n\n" +
            "The door at the end loads '" + BossScene + "' (build it with Tools > Build Level 3 Boss (Octopus)).");
    }

    // ---------------- A) Flooded Docks ----------------

    static void BuildDocks(Transform decor, Transform hazards, Transform enemies, Transform props)
    {
        Fill(0, -1, 8, 1);    // start
        Fill(14, -1, 4, 1);   // island 1
        Fill(26, -1, 4, 1);   // island 2
        Fill(36, -1, 44, 1);  // end of the docks + gallery + climb floor

        Water(decor, hazards, 8, 14);
        Water(decor, hazards, 18, 26);
        Water(decor, hazards, 30, 36);

        // Raft that drifts across the wide pool
        GameObject raft = Box("Raft", props, new Vector2(19.5f, -0.25f), new Vector2(3f, 0.5f), WoodColor);
        var so = new SerializedObject(raft.AddComponent<MovingPlatform>());
        so.FindProperty("travel").vector2Value = new Vector2(5f, 0f);
        so.FindProperty("speed").floatValue = 2.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        Ring("Ring_LastPool", props, new Vector2(33f, 7f), 8f); // hook over the last pool
        Crab("Crab_Island1", enemies, new Vector2(15.8f, 0.5f));
        Crab("Crab_Island2", enemies, new Vector2(27.8f, 0.5f));
        Checkpoint("Checkpoint_Docks", props, new Vector2(38f, 0.75f));

        // Pier posts under the islands, hanging chains, lanterns
        foreach (float x in new[] { 14.5f, 17.5f, 26.5f, 29.5f })
            Deco("PierPost", decor, new Vector2(x, -1.5f), new Vector2(0.5f, 2f), WoodColor, 2);
        HangingChain(decor, 11f, 22f, 7f);
        HangingChain(decor, 22f, 22f, 10f);
        HangingChain(decor, 33f, 22f, 5f);
        Lantern("Lantern_Docks1", decor, new Vector2(5f, 6f), LanternColor);
        Lantern("Lantern_Docks2", decor, new Vector2(28f, 8f), LanternColor);
    }

    // ---------------- B) Chain Gallery ----------------

    static void BuildGallery(Transform decor, Transform hazards, Transform props)
    {
        Fill(40, 13, 24, 9); // low ceiling over the corridor (x 40-63, open space y 0-12)

        Anchor("Anchor_1", hazards, new Vector2(45f, 13f), 10.5f, 0f);
        Anchor("Anchor_2", hazards, new Vector2(55f, 13f), 10.5f, 0.5f);
        SlamChain("SlamChain_1", hazards, new Vector2(50.5f, 12.7f), 0f);
        SlamChain("SlamChain_2", hazards, new Vector2(58.5f, 12.7f), 1f);

        Lantern("Lantern_Gallery1", decor, new Vector2(41f, 7f), LanternColor);
        Lantern("Lantern_Gallery2", decor, new Vector2(62f, 7f), LanternColor);
        foreach (float x in new[] { 43f, 48f, 53f, 58f })
            Deco("CellBars", decor, new Vector2(x, 4f), new Vector2(2f, 6f), new Color(0.12f, 0.14f, 0.18f), -15);
    }

    // ---------------- The Climb ----------------

    static void BuildClimb(Transform decor, Transform props, Transform secrets)
    {
        Fill(80, -1, 1, 15); // wall between the climb and the tide below the bridge

        OneWay("Step_1", props, 65, 2, 3, OneWayColor);   // tops at 3, 6, 9, 12, 12 (3-tile jumps)
        OneWay("Step_2", props, 70, 5, 3, OneWayColor);
        OneWay("Step_3", props, 65, 8, 3, OneWayColor);
        OneWay("Step_4", props, 70, 11, 3, OneWayColor);
        OneWay("Step_5", props, 65, 11, 3, OneWayColor);  // in front of the breakable wall
        Fill(74, 14, 13, 1);                               // start of the bridge (top at y 15)
        Checkpoint("Checkpoint_Climb", props, new Vector2(66f, 0.75f));
        Lantern("Lantern_Climb", decor, new Vector2(77f, 8f), LanternColor);

        // Secret Room 1: punch the blue wall at the top-left of the climb
        Fill(60, 13, 4, 3, false); // pocket carved into the gallery ceiling (x 60-63, y 13-15)
        Fill(60, 12, 3, 1);        // its floor
        Breakable("BreakableWall", props, new Vector2(63.5f, 14.5f), new Vector2(1f, 3f), new Color(0.25f, 0.45f, 0.85f), 24);
        SecretRoom("1", secrets, 0, 30, new Vector2(61.2f, 14.1f), new Vector2(61.2f, 14.1f), ItemType.Shield, ItemType.Healing);
    }

    // ---------------- C) Tide Bridge ----------------

    static void BuildBridge(Transform decor, Transform hazards, Transform enemies, Transform props, Transform secrets)
    {
        // The tide under the bridge (fall in = back to the checkpoint)
        Deco("Tide", decor, new Vector2(100.5f, 0.5f), new Vector2(39f, 3f), WaterColor, 3);
        KillZone("KillZone_Tide", hazards, new Vector2(100.5f, 1f), new Vector2(39f, 1f));

        Ring("Ring_Bridge1", props, new Vector2(89f, 19.5f), 6f);
        Fill(91, 14, 5, 1);
        GameObject lift = Box("MovingBridge", props, new Vector2(98f, 14.75f), new Vector2(3f, 0.5f), WoodColor);
        var so = new SerializedObject(lift.AddComponent<MovingPlatform>());
        so.FindProperty("travel").vector2Value = new Vector2(5f, 0f);
        so.FindProperty("speed").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
        Fill(106, 14, 3, 1);
        Ring("Ring_Bridge2", props, new Vector2(111f, 19.5f), 6f);
        Fill(114, 14, 6, 1);

        Shooter("Urchin", enemies, new Vector2(94f, 15.8f), new Color(0.35f, 0.2f, 0.45f));
        Crab("Crab_Bridge", enemies, new Vector2(107.3f, 15.5f));
        Checkpoint("Checkpoint_Bridge", props, new Vector2(78f, 15.75f));
        Checkpoint("Checkpoint_End", props, new Vector2(115f, 15.75f));
        Door("BossDoor", props, new Vector2(118.5f, 16.1f), BossScene, new Color(0.45f, 0.15f, 0.5f));

        // Secret Room 2: drop off the moving platform onto the sunken ledge below it
        Fill(99, 7, 4, 1);
        SecretRoom("2", secrets, 30, 30, new Vector2(101f, 9.1f), new Vector2(116.5f, 15.1f), ItemType.InstantRevival, ItemType.SpeedBoost); // comes back out near the boss door (a shortcut!)

        HangingChain(decor, 87f, 22f, 4f);
        HangingChain(decor, 103f, 22f, 4f);
        Lantern("Lantern_Bridge1", decor, new Vector2(83f, 19f), LanternColor);
        Lantern("Lantern_Bridge2", decor, new Vector2(117f, 19f), LanternColor);
    }

    // ---------------- Look ----------------

    static void BuildBackground(Transform decor)
    {
        Deco("DeepWater", decor, new Vector2(60f, 10.5f), new Vector2(130f, 30f), DeepWater, -20);
        // Rows of old stone blocks in the back
        for (int row = 0; row < 11; row++)
            Deco("BackRow", decor, new Vector2(60f, row * 2f), new Vector2(124f, 0.08f), new Color(0.09f, 0.14f, 0.2f), -19);
        // Seaweed swaying up from the floor
        foreach (float x in new[] { 2f, 6f, 15f, 27f, 37f, 42f, 61f, 67f, 76f, 93f, 116f })
        {
            float y = x > 86f ? 15f : 0f;
            Deco("Seaweed", decor, new Vector2(x, y + 1.2f), new Vector2(0.25f, 2.4f), SeaweedColor, -6);
            Deco("Seaweed", decor, new Vector2(x + 0.35f, y + 0.8f), new Vector2(0.2f, 1.6f), SeaweedColor, -6);
        }
    }

    // ---------------- Pieces ----------------

    static void Water(Transform decor, Transform hazards, int x0, int x1)
    {
        float w = x1 - x0;
        Deco("Water", decor, new Vector2(x0 + w * 0.5f, -1.3f), new Vector2(w, 2f), WaterColor, 3);
        KillZone("KillZone_Water", hazards, new Vector2(x0 + w * 0.5f, -2f), new Vector2(w + 1f, 1f));
    }

    static void HangingChain(Transform decor, float x, float top, float length)
    {
        Deco("Chain", decor, new Vector2(x, top - length * 0.5f), new Vector2(0.12f, length), ChainColor, -5);
        Deco("Hook", decor, new Vector2(x, top - length), new Vector2(0.45f, 0.45f), ChainColor, -5);
    }

    // A heavy anchor swinging on a chain (hits hard and knocks you back)
    static void Anchor(string name, Transform parent, Vector2 pivot, float length, float phase)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = pivot;
        var so = new SerializedObject(root.AddComponent<PoisonPendulum>()); // just the swinging motion
        so.FindProperty("phase").floatValue = phase;
        so.FindProperty("swingAngle").floatValue = 45f;
        so.FindProperty("period").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject chain = Deco("Chain", root.transform, pivot + Vector2.down * length * 0.5f, new Vector2(0.15f, length), ChainColor, 2);
        GameObject anchor = Box("AnchorHead", root.transform, pivot + Vector2.down * length, new Vector2(1.6f, 1.2f), new Color(0.3f, 0.32f, 0.36f));
        var col = anchor.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        anchor.AddComponent<DamageZone>().Setup(12, 11f, false, 0, 0f, 1f);
        Deco("AnchorArm", root.transform, pivot + Vector2.down * (length - 0.9f), new Vector2(2.6f, 0.3f), new Color(0.3f, 0.32f, 0.36f), 1);
    }

    // A chain that slams down from the ceiling to the floor on a timer
    static void SlamChain(string name, Transform parent, Vector2 mount, float delay)
    {
        GameObject plate = Deco(name, parent, mount, new Vector2(1f, 0.6f), LevelKit.IronColor, 3);
        var so = new SerializedObject(plate.AddComponent<ChainTrap>());
        so.FindProperty("direction").vector2Value = Vector2.down;
        so.FindProperty("length").floatValue = mount.y;
        so.FindProperty("startDelay").floatValue = delay;
        so.FindProperty("thickness").floatValue = 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}