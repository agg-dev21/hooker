using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LevelKit;

// Editor-only. Adds Tools > Rebuild Tutorial.
// Rebuilds the 7 tutorial rooms from Room_Template as short rooms (about 2 screens each) where the camera
// follows the player. Each room teaches one skill step by step: a sign explains it right before it is needed,
// then it gets harder. Checkpoints sit between the parts. Signs show the player's current buttons (TutorialSign).
//   1 Move    moving, checkpoints, dropping through thin platforms, pause and Controls
//   2 Jump    tap vs hold, floating platforms over a pit, a tall wall
//   3 Dash    ground dash, jump + dash over wide gaps
//   4 Punch   slash, aim up, aim down, crate wall, cracked blue wall hiding a chest
//   5 Hook    hook a ring over a tall wall, ring after ring across a pit, far-away hook damage
//   6 Bullets dash through bullets and enemies (only 5 health in this room)
//   7 Dock    chest, inventory, powers from bosses, then the boat to Level_1
// Anything changed by hand in these rooms is replaced.
public static class TutorialRebuilder
{
    const float CameraSize = 6.5f;
    static readonly Color FloorColor = new Color(0.55f, 0.45f, 0.35f);
    static readonly Color DummyColor = new Color(0.55f, 0.35f, 0.2f);
    static readonly Color CrackedColor = new Color(0.5f, 0.45f, 0.4f);
    static readonly Color SecretColor = new Color(0.25f, 0.45f, 0.85f);
    static readonly Color CrateColor = new Color(0.6f, 0.4f, 0.2f);
    static readonly Color PlankColor = new Color(0.45f, 0.32f, 0.2f);
    static readonly Color WaterColor = new Color(0.1f, 0.35f, 0.55f, 0.85f);
    static readonly Color ShooterColor = new Color(0.85f, 0.15f, 0.15f);
    const string ProjectilePrefab = "Assets/Prefabs/Projectile.prefab";

    [MenuItem("Tools/Rebuild Tutorial")]
    static void RebuildAll()
    {
        if (!EditorUtility.DisplayDialog("Rebuild Tutorial",
                "This rebuilds all 7 tutorial rooms from scratch. Anything you changed in them by hand will be lost.\n\nContinue?",
                "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Delete the old rooms first (so the builder doesn't ask about each one)
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string room in new[] { "Tutorial_1_Move", "Tutorial_2_Jump", "Tutorial_3_Dash", "Tutorial_4_Punch",
                                        "Tutorial_5_Hook", "Tutorial_6_DashThroughBullets", "Tutorial_7_Dock" })
        {
            string path = ScenesFolder + "/" + room + ".unity";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }

        Room("Tutorial_1_Move", "Tutorial_2_Jump", 40, 12, MoveRoom);
        Room("Tutorial_2_Jump", "Tutorial_3_Dash", 44, 12, JumpRoom);
        Room("Tutorial_3_Dash", "Tutorial_4_Punch", 50, 12, DashRoom);
        Room("Tutorial_4_Punch", "Tutorial_5_Hook", 52, 12, PunchRoom);
        Room("Tutorial_5_Hook", "Tutorial_6_DashThroughBullets", 48, 16, HookRoom);
        Room("Tutorial_6_DashThroughBullets", "Tutorial_7_Dock", 46, 12, BulletRoom);
        Room("Tutorial_7_Dock", null, 34, 12, DockRoom);

        EditorSceneManager.OpenScene(ScenesFolder + "/Tutorial_1_Move.unity", OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Rebuild Tutorial",
            "Done! All 7 tutorial rooms are rebuilt. Tutorial_1_Move is open: press Play.\n\n" +
            "To change a sign's words, select it and edit 'Template' on its Tutorial Sign.", "OK");
    }

    // ---------------- One room ----------------

    // Builds a room width tiles long (floor top at y 0, ceiling at y ceiling), runs build, adds the exit and camera
    static void Room(string name, string next, int width, int ceiling, System.Action<Transform, int, int> build)
    {
        if (!NewSceneFromTemplate(name, out Scene scene)) return;
        Map.color = FloorColor;
        var root = new GameObject("Tutorial").transform;

        Fill(-3, -8, 3, ceiling + 10);          // left wall
        Fill(-3, ceiling, width + 9, 2);        // ceiling
        Fill(-3, -1, width + 9, 1);             // floor (rooms carve their pits out of it)
        Fill(width + 3, -8, 3, ceiling + 10);   // wall behind the exit

        build(root, width, ceiling);

        if (!string.IsNullOrEmpty(next))
        {
            var exit = new GameObject("Exit");
            exit.transform.SetParent(root);
            exit.transform.position = new Vector3(width + 1.5f, ceiling * 0.5f, 0f);
            var col = exit.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, ceiling + 16f);
            col.isTrigger = true;
            var so = new SerializedObject(exit.AddComponent<SceneExit>());
            so.FindProperty("nextScene").stringValue = next;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Transform player = PlacePlayer(new Vector2(2f, 0f));
        SetUpCamera(player, CameraSize, new Vector2(-1f, -7f), new Vector2(width + 3f, ceiling + 1f));

        RebuildColliders(Map);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        KeepInSceneList(ScenesFolder + "/" + name + ".unity");
    }

    // The rebuilt room is a new file: make sure the Scene List points at it (same place, switched on)
    static void KeepInSceneList(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(e => e.path == path);
        int at = 0; // tutorial rooms go right after each other at the start of the list
        for (int i = 0; i < list.Count; i++)
            if (list[i].path.Contains("/Tutorial_") && string.CompareOrdinal(list[i].path, path) < 0) at = i + 1;
        if (at == 0) at = list.FindIndex(e => e.path.Contains("/Tutorial_")) is int f && f >= 0 ? f : (list.Count > 0 && !list[0].enabled ? 1 : 0);
        list.Insert(Mathf.Clamp(at, 0, list.Count), new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------------- 1 Move ----------------

    static void MoveRoom(Transform root, int w, int ceil)
    {
        Sign(root, 6f, 6f, "Raphael's mother left when he was a boy.\nFour men are left who might be his father.\n\n{MoveLeft} / {MoveRight} to move");
        Sign(root, 14f, 9.5f, "Yellow posts are CHECKPOINTS.\nDie, and you come back to the last one.");
        Checkpoint("Checkpoint_1", root, new Vector2(16f, 0.75f));

        // A thin bridge over a lower floor. A wall blocks the top: drop through the bridge.
        Fill(20, -1, w - 17, 1, false);
        Fill(18, -6, w - 12, 1);                  // lower floor, top at y -5
        Fill(18, -5, 2, 4);
        OneWay("Bridge", root, 20, -1, 12, PlankColor);
        Fill(32, -1, 1, ceil + 1);                // wall: can't walk past on top
        Sign(root, 25f, 5f, "THIN platforms: jump up through them.\nHold {AimDown} (or S) to drop through.");
        Sign(root, 37f, 1.5f, "{Pause} pauses.\nControls: change any key.", 7.5f);
    }

    // ---------------- 2 Jump ----------------

    static void JumpRoom(Transform root, int w, int ceil)
    {
        Sign(root, 5f, 6.5f, "Tap {Jump} for a small hop.\nHold {Jump} to jump much higher.");
        Fill(9, 0, 1, 2);     // low wall: a hop
        Fill(13, 0, 2, 6);    // tall wall: hold
        Checkpoint("Checkpoint_1", root, new Vector2(17f, 0.75f));

        Pit(root, 20, 34);    // floating platforms over a pit, tops at y 2, 5, 3
        Fill(22, 1, 2, 1);
        Fill(26, 4, 2, 1);
        Fill(30, 2, 2, 1);
        Sign(root, 26f, 10f, "Fall into a pit: back to the last checkpoint.");

        Checkpoint("Checkpoint_2", root, new Vector2(36f, 0.75f));
        Sign(root, 36f, 9.5f, "Long falls hurt. Now hold {Jump} all the way.");
        Fill(40, 0, 2, 7);    // 7 tall: almost a full jump
    }

    // ---------------- 3 Dash ----------------

    static void DashRoom(Transform root, int w, int ceil)
    {
        Sign(root, 4.5f, 6f, "{Dash} to dash forward,\non the ground or in the air.");
        Pit(root, 9, 14);     // 5 wide

        Sign(root, 18.5f, 7f, "Too wide to jump!\nJump first, then {Dash} in the air.");
        Pit(root, 22, 33);    // 11 wide: jump + dash
        Checkpoint("Checkpoint_1", root, new Vector2(35f, 0.75f));

        Sign(root, 40f, 8.5f, "Last one. No mistakes.", 8f);
        Pit(root, 38, 48);    // 10 wide
    }

    // ---------------- 4 Punch ----------------

    static void PunchRoom(Transform root, int w, int ceil)
    {
        Sign(root, 4.5f, 6f, "{Punch} or LEFT CLICK to slash.\nBreak the dummy.", 9f);
        Breakable("Dummy", root, new Vector2(8f, 1.5f), new Vector2(1f, 3f), DummyColor, 8);

        // Low corridor with a cracked block in its ceiling, then the floor above it
        Fill(14, 3, 20, 1);              // corridor ceiling = upper floor (top at y 4), x 14-33
        Fill(14, 4, 1, ceil - 4);        // no jumping over the corridor entrance
        Fill(24, 0, 1, 3);               // corridor dead end
        Fill(22, 3, 2, 1, false);
        Breakable("CrackedBlock_Up", root, new Vector2(23f, 3.5f), new Vector2(2f, 1f), CrackedColor, 8);
        Sign(root, 9f, 9.5f, "Hold {AimUp} while slashing to hit above you.\nBreak the block in the corridor ceiling, jump up.");

        Fill(34, 4, 1, ceil - 4);        // end of the upper floor
        Fill(31, 3, 2, 1, false);
        Breakable("CrackedBlock_Down", root, new Vector2(32f, 3.5f), new Vector2(2f, 1f), CrackedColor, 8);
        Sign(root, 26f, 8.5f, "Stand on the cracked block.\nHold {AimDown} while slashing.", 9f);

        Sign(root, 37.5f, 2.5f, "Crate walls break too.", 7.5f);
        Breakable("CrateWall", root, new Vector2(42f, ceil * 0.5f), new Vector2(1.5f, ceil), CrateColor, 12);

        Fill(46, 3, 5, 1);               // roof of a hidden nook (x 46-50)
        Fill(50, 0, 1, 3);               // its back wall
        Breakable("CrackedBlueWall", root, new Vector2(45.5f, 1.5f), new Vector2(1f, 3f), SecretColor, 8);
        Chest("Chest_Secret", root, new Vector2(48f, 0.5f), ItemType.Healing, 1);
        Sign(root, 47f, 8.5f, "Cracked BLUE walls hide secret rooms!\nPress {Interact} at a chest to open it.", 9f);
    }

    // ---------------- 5 Hook ----------------

    static void HookRoom(Transform root, int w, int ceil)
    {
        Sign(root, 4.5f, 7.5f, "{Hook} or RIGHT CLICK throws your hook.\nAim with the arrows or the mouse.\nHook the gold ring to fly up.", 9f);
        Fill(9, 0, 3, 10);                        // too tall to jump
        Ring("Ring_Wall", root, new Vector2(7.5f, 12.5f), 10f);
        Checkpoint("Checkpoint_1", root, new Vector2(13f, 0.75f));

        Pit(root, 15, 33);
        Ring("Ring_1", root, new Vector2(19f, 9f), 10f);
        Ring("Ring_2", root, new Vector2(25f, 10f), 10f);
        Ring("Ring_3", root, new Vector2(31f, 9f), 10f);
        OneWayAt("RestLedge", root, new Vector2(25f, 5.5f), 2f, PlankColor);
        Sign(root, 24f, 13.5f, "Ring after ring: hook the next one mid-air.");
        Checkpoint("Checkpoint_2", root, new Vector2(35f, 0.75f));

        Sign(root, 39f, 7.5f, "Your hook hurts MORE from far away.\nHook the dummy from a distance.", 9f);
        GameObject dummy = Box("TargetDummy", root, new Vector2(45f, 1.5f), new Vector2(1f, 3f), DummyColor);
        dummy.layer = GroundLayer;
        AddHealth(dummy, 24);
    }

    // ---------------- 6 Bullets ----------------

    static void BulletRoom(Transform root, int w, int ceil)
    {
        Sign(root, 5f, 6.5f, "While you DASH, nothing can hurt you.\nDash through bullets. Only 5 health here!");
        Shooter(root, "Shooter_1", new Vector2(14f, 1f), 1f);
        Checkpoint("Checkpoint_1", root, new Vector2(18f, 0.75f));

        Sign(root, 22f, 8f, "Two at once. Time your dashes.", 8f);
        Shooter(root, "Shooter_2", new Vector2(27f, 1f), 0.8f);
        Fill(30, 3, 3, 1);
        Shooter(root, "Shooter_3", new Vector2(31.5f, 5f), 0.8f);
        Checkpoint("Checkpoint_2", root, new Vector2(35f, 0.75f));

        Sign(root, 38f, 7f, "Dash through enemies too,\nor fight them.", 8f);
        Crab("Crab", root, new Vector2(42f, 0.5f));

        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (player && player.TryGetComponent(out Health health))
        {
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").intValue = 5; // every bullet counts
            hso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------------- 7 Dock ----------------

    static void DockRoom(Transform root, int w, int ceil)
    {
        Sign(root, 5f, 6f, "Chests hold items.\nStand at one and press {Interact}.", 9f);
        Chest("Chest_Dock", root, new Vector2(9f, 0.5f), ItemType.Healing, 2);
        Sign(root, 16f, 6f, "{NextItem} opens your inventory (the game pauses).\n{UseItem} uses the item you picked.", 9.5f);
        Sign(root, 25.5f, 6.5f, "Beat a boss: take its power.\nPowers show bottom right.\nGood luck, Raphael. Step on the boat.", 9f);

        Fill(30, -1, w - 27, 1, false);           // the dock ends, water after
        Deco("Water", root, new Vector2(40f, -2.5f), new Vector2(20f, 4f), WaterColor, 3);
        KillZone("KillZone_Water", root, new Vector2(40f, -4f), new Vector2(20f, 1f));
        GameObject boat = Box("Boat", root, new Vector2(32.5f, -0.5f), new Vector2(4f, 1f), PlankColor);
        boat.layer = GroundLayer;
        var board = boat.AddComponent<BoxCollider2D>(); // trigger on top: stepping on starts the sail
        board.isTrigger = true;
        board.size = new Vector2(0.8f, 1f);
        board.offset = new Vector2(0f, 1f);

        Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        Image fade = null;
        if (canvas)
        {
            var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            fadeGo.transform.SetParent(canvas.transform, false);
            fadeGo.transform.SetAsLastSibling();
            var rt = (RectTransform)fadeGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            fade = fadeGo.GetComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;
        }
        var so = new SerializedObject(boat.AddComponent<BoatExit>());
        so.FindProperty("nextScene").stringValue = "Level_1";
        so.FindProperty("fadeImage").objectReferenceValue = fade;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- Pieces ----------------

    // A sign: text on a dark board. Write buttons as {Jump}, {Dash}... (see TutorialSign)
    static void Sign(Transform parent, float x, float y, string template, float width = 11f)
    {
        int lines = template.Split('\n').Length;
        float height = 0.7f + lines * 0.6f;
        Deco("SignBoard", parent, new Vector2(x, y), new Vector2(width, height), new Color(0f, 0f, 0f, 0.5f), 4);

        var go = new GameObject("Sign");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(x, y, 0f);
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        tmp.text = TutorialSign.Fill(template);
        tmp.fontSize = 2.8f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.richText = true;
        tmp.rectTransform.sizeDelta = new Vector2(width - 0.4f, height);
        tmp.GetComponent<MeshRenderer>().sortingOrder = 5;
        go.AddComponent<TutorialSign>().template = template;
    }

    // A gap in the floor from x0 to x1 with a kill zone below (back to the checkpoint)
    static void Pit(Transform parent, int x0, int x1)
    {
        Fill(x0, -1, x1 - x0, 1, false);
        Deco("PitDark", parent, new Vector2((x0 + x1) * 0.5f, -3f), new Vector2(x1 - x0, 4f), new Color(0.05f, 0.05f, 0.08f), -5);
        KillZone("KillZone_Pit", parent, new Vector2((x0 + x1) * 0.5f, -4f), new Vector2(x1 - x0 + 1f, 1f));
    }

    static void Shooter(Transform parent, string name, Vector2 pos, float fireRate)
    {
        GameObject shooter = Box(name, parent, pos, new Vector2(1f, 2f), ShooterColor);
        AddHealth(shooter, 8);
        var so = new SerializedObject(shooter.AddComponent<EnemyShooter>());
        so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>(ProjectilePrefab);
        so.FindProperty("range").floatValue = 13f;
        so.FindProperty("fireRate").floatValue = fireRate;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
