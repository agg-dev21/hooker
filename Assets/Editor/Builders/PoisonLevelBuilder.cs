using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only. Adds the menu item Tools > Rebuild Levels > Level 2 (Poison Tower).
// Builds the poison level from the hand-drawn layout: a tall tower you go DOWN through.
// Makes Level_2 from Room_Template (so the boat at the end of Level_1_Sail lands here), adds it to the Scene List.
//
// Layout (x 0..39 inside the walls, y 0 at the bottom floor, top ceiling at y 62):
//   y 52-54  Start platform (purple). A sealed shaft under it hides Secret Room A.
//   y 43     Floor 2 with poison puddles near the middle. Holes beside the shaft drop you down.
//   y 34     Room with two swinging poison spike balls, a chest, and a blue breakable wall (Secret Room B) on the right.
// The tower is mirrored left/right around x = 20, except the doors: boss door left / Secret Room C right, and the breakable wall (right only).
//   y 29     Row of small platforms over a poison-covered roof, with poison dripping from vents above.
//   y 12-25  Central poison shaft between two side rooms (entered from below with the hook rings):
//              left room  = the BOSS DOOR (bottom-left door)
//              right room = Secret Room C
//   y 0      Bottom: enemies and two pits.
// Secret rooms are outside the tower (x -50 to -24), each with a chest and an antidote.
public static class PoisonLevelBuilder
{
    const string LevelName = "Level_2";
    const string BossSceneName = "Level_2_Boss"; // the boss door loads this (fades to black until it exists)
    const string TemplateScene = "Assets/Scenes/Room_Template.unity";
    const string ScenesFolder = "Assets/Scenes";
    const string FallbackTile = "Assets/Tiles/Square.asset";
    const string ProjectilePrefab = "Assets/Prefabs/Projectile.prefab";

    // Everything from the spike-ball room up is raised by this much, to make the platform-row room taller
    const int UpperLift = 3;
    const int Left = 0, Right = 39, Top = 62 + UpperLift; // inside of the tower
    const float DripInterval = 0.9f; // seconds between poison drops from each vent (was 1.6)
    const float CameraSize = 5f; // zoomed in: shows part of the floor you're on (10 tiles tall)

    // Camera zones (floors), bottom to top: (lowest y the camera may show, highest y, camera size or 0 = normal).
    // The camera never shows outside the zone the player is in. Every zone is at least 10 tall (camera size 5).
    static readonly Vector3[] CameraZones =
    {
        new Vector3(-1f, 12f, 0f),     // bottom area: enemies and pits
        new Vector3(11f, 26f, 0f),     // side rooms and the poison shaft
        new Vector3(25f, 37f, 6f),     // platform row: zoomed out a little so the poison floor AND the vents are both on screen
        new Vector3(36f, 46f, 0f),     // spike-ball room
        new Vector3(45f, 56f, 0f),     // floor 2 (under the start platform)
        new Vector3(55f, 66f, 0f),     // start platform
    };

    // Colors (placeholders until there is art)
    static readonly Color PoisonColor = new Color(0.35f, 0.9f, 0.15f, 0.9f);
    static readonly Color VentColor = new Color(0.5f, 1f, 0.3f);
    static readonly Color SpikeColor = new Color(0.15f, 0.55f, 0.1f);
    static readonly Color ChainColor = new Color(0.2f, 0.35f, 0.15f);
    static readonly Color RingColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color DoorColor = new Color(0.45f, 0.25f, 0.08f);
    static readonly Color BossDoorColor = new Color(0.5f, 0.05f, 0.35f);
    static readonly Color BreakableColor = new Color(0.2f, 0.45f, 1f);
    static readonly Color OneWayColor = new Color(0.55f, 0.15f, 0.1f);
    static readonly Color SludgeColor = new Color(0.2f, 0.4f, 0.1f);
    static readonly Color SpitterColor = new Color(0.45f, 0.6f, 0.1f);
    static readonly Color CheckpointColor = new Color(0.9f, 0.9f, 0.1f);
    static readonly Color ChestColor = new Color(0.75f, 0.55f, 0.15f);
    static readonly Color AntidoteColor = new Color(0.3f, 1f, 0.8f);

    static int lift; // added to every y while building the raised (upper) parts
    static Tilemap map;
    static TileBase tile;
    static Sprite boxSprite;
    static int groundLayer;

    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 2 (Poison Tower)")]
    static void Build()
    {
        string path = $"{ScenesFolder}/{LevelName}.unity";
        if (File.Exists(path) && !EditorUtility.DisplayDialog("Build Level 2",
                LevelName + " already exists.\n\nReplace it with a fresh build? Anything you changed in it by hand will be lost.",
                "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(path);
        }
        if (!AssetDatabase.CopyAsset(TemplateScene, path))
        {
            Debug.LogError("Build Level 2: could not copy " + TemplateScene);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        DestroyByName(scene, "Exit");
        DestroyByName(scene, "LeftWall");
        DestroyByName(scene, "KillZone");

        map = FindGroundTilemap(scene);
        tile = FirstTile(map) ?? AssetDatabase.LoadAssetAtPath<TileBase>(FallbackTile);
        boxSprite = (tile as Tile)?.sprite ?? AssetDatabase.LoadAssetAtPath<Tile>(FallbackTile)?.sprite;
        groundLayer = LayerMask.NameToLayer("Ground");
        map.ClearAllTiles();

        var level = new GameObject("PoisonLevel").transform;
        var hazards = Group("Hazards", level);
        var enemies = Group("Enemies", level);
        var props = Group("Props", level);
        var secrets = Group("SecretRooms", level);

        BuildShell();
        lift = UpperLift; // these three parts are drawn at their old heights and raised by UpperLift
        BuildStart(props);
        BuildFloor2(hazards, props);
        BuildRoom2(hazards, props);
        lift = 0;
        BuildPlatformRow(hazards);
        BuildSideRooms(hazards, props);
        BuildBottom(hazards, enemies, props);
        BuildSecretRooms(secrets, hazards, props);

        RebuildColliders(map);
        PlacePlayerAndCamera(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorKit.AddToSceneList(path);
        EditorUtility.DisplayDialog("Build Level 2", "Done! " + LevelName + " is built, open, and in the Scene List.\n\n" +
            "The boss door loads '" + BossSceneName + "' (the screen stays black until that scene exists).", "OK");
    }

    // ---------------- The tower ----------------

    // Outer walls, ceiling, the bottom floor with two pits, and a thick wall on the right around the hidden pocket
    static void BuildShell()
    {
        Fill(Left - 6, -1, 6, Top + 2);           // left wall (thick: a hidden pocket is carved into it)
        Fill(Right + 1, -1, 6, Top + 2);          // right wall (same, mirrored)
        Fill(Left - 6, Top, Right - Left + 13, 1); // ceiling

        // Bottom floor: x 0-9, 15-24, 30-39 (pits at 10-14 and 25-29)
        Fill(0, -1, 10, 1);
        Fill(15, -1, 10, 1);
        Fill(30, -1, 10, 1);
        MakeKillZone("KillZone_PitLeft", new Vector2(12.5f, -3f), new Vector2(6f, 1f));
        MakeKillZone("KillZone_PitRight", new Vector2(27.5f, -3f), new Vector2(6f, 1f));
    }

    // Start platform (purple dot) with the sealed shaft underneath it (Secret Room A)
    static void BuildStart(Transform props)
    {
        Fill(5, 52, 30, 1);   // wide ledge, open at both ends so you drop down the sides
        Fill(15, 53, 10, 1);  // raised block where you start

        // Shaft under the block: walls at x 17 and 22, inside x 18-21, from floor 2 up to the block
        Fill(17, 42, 1, 10);
        Fill(22, 42, 1, 10);
        OneWay("Shaft_Bottom", props, 18, 42, 4);  // dotted line: jump up through it from below
        OneWay("Shaft_Ledge", props, 18, 48, 4);   // dotted line: the secret door stands on it
        Ring("Ring_ShaftTop", props, new Vector2(20f, 51f), 4f);
        Ring("Ring_FloorLeft", props, new Vector2(16f, 47.5f), 8f);  // yellow dots beside the shaft
        Ring("Ring_FloorRight", props, new Vector2(24f, 47.5f), 8f);
    }

    // Floor 2: poison puddles next to the holes beside the shaft
    static void BuildFloor2(Transform hazards, Transform props)
    {
        Fill(0, 42, 15, 1);
        Fill(25, 42, 15, 1);
        Puddle("Puddle_F2_Left", hazards, 9, 15, 43f);
        Puddle("Puddle_F2_Right", hazards, 25, 31, 43f);
        Checkpoint("Checkpoint_F2_Left", props, new Vector2(2f, 43.75f));
        Checkpoint("Checkpoint_F2_Right", props, new Vector2(38f, 43.75f));
    }

    // Room under floor 2: two swinging poison spike balls, a chest, the breakable wall on the right
    static void BuildRoom2(Transform hazards, Transform props)
    {
        Fill(4, 33, 32, 1); // floor, open at both ends (x 0-3 and 36-39)
        Pendulum("SpikeBall_Left", hazards, new Vector2(8f, 42f), 6.2f, 0f);
        Pendulum("SpikeBall_Right", hazards, new Vector2(32f, 42f), 6.2f, 0.5f); // opposite swing: mirror image
        // Climb up into the shaft: jump onto the wall in the middle (3 high), then hook the ring
        // just under the shaft bottom: it pops you up through the dotted floor into the shaft.
        Fill(18, 34, 4, 3);                        // wall in the middle, top at y 37
        Ring("Ring_UnderShaft", props, new Vector2(20f, 41.3f), 14f);
        Chest("Chest_Room2", props, new Vector2(20f, 37.5f)); // chest on top of the middle wall
        Checkpoint("Checkpoint_Room2_Left", props, new Vector2(16f, 34.75f));
        Checkpoint("Checkpoint_Room2_Right", props, new Vector2(24f, 34.75f));

        // Blue breakable wall in the right wall (punch it) hiding Secret Room B, with a ledge to stand on while punching
        Fill(37, 36, 3, 1);
        Fill(Right + 1, 37, 4, 3, false); // carve the pocket (x 40-43, y 37-39)
        GameObject wall = Box("BreakableWall", props, new Vector2(40.5f, 38.5f), new Vector2(1f, 3f), BreakableColor);
        wall.layer = groundLayer;
        AddHealth(wall, 24);
        wall.AddComponent<BreakableWall>();
        Ring("Ring_Pocket", props, new Vector2(42.5f, 39.3f), 2f);
    }

    // Row of small platforms over a poisoned roof, with poison vents dripping between them
    static void BuildPlatformRow(Transform hazards)
    {
        // 8 platforms, all 3 wide with 2-wide gaps between them, mirrored left/right:
        // left x 0-2, 5-7, 10-12, 15-17 | right x 22-24, 27-29, 32-34, 37-39
        const int platformWidth = 3, platformGap = 2;
        for (int i = 0; i < 4; i++)
        {
            int x = i * (platformWidth + platformGap);
            Fill(x, 28, platformWidth, 1);                          // left side
            Fill(Right + 1 - x - platformWidth, 28, platformWidth, 1); // its mirror on the right
        }

        Fill(0, 25, 18, 1);  // roof of the left room (and the poison underneath the platforms)
        Fill(22, 25, 18, 1); // roof of the right room
        Puddle("Puddle_Roof_Left", hazards, 0, 17, 26f);
        Puddle("Puddle_Roof_Right", hazards, 23, 40, 26f);

        // Vents under the spike-ball room floor, dripping through the gaps between the platforms
        float ventY = 32.6f + UpperLift;
        Dripper("Vent_1", hazards, new Vector2(9f, ventY), 0f);    // over the gap at x 8-9
        Dripper("Vent_2", hazards, new Vector2(14f, ventY), 0.5f); // over the gap at x 13-14
        Dripper("Vent_3", hazards, new Vector2(26f, ventY), 0.5f);
        Dripper("Vent_4", hazards, new Vector2(31f, ventY), 0f);
    }

    // Central poison shaft between the two side rooms
    static void BuildSideRooms(Transform hazards, Transform props)
    {
        // Shaft walls (x 17 and 22), slimy on the inside
        Fill(17, 12, 1, 14);
        Fill(22, 12, 1, 14);
        Slime("Slime_ShaftLeft", hazards, new Vector2(18.1f, 18.5f), new Vector2(0.2f, 13f));
        Slime("Slime_ShaftRight", hazards, new Vector2(21.9f, 18.5f), new Vector2(0.2f, 13f));

        // ---- Left room: the BOSS DOOR. Enter from below at x 0-3 with the hook ring ----
        Fill(4, 11, 13, 1);                 // poison floor
        Puddle("Puddle_LeftRoom", hazards, 4, 17, 12f);
        Ring("Ring_LeftRoomEntry", props, new Vector2(2f, 11.5f), 20f); // strong pop: lands you on the ledge above
        // Every jump in this room is 3 tiles or less (the player jumps about 3.8)
        OneWay("Ledge_LeftRoomEntry", props, 0, 14, 3);   // landing ledge after the ring, top at y 15 (3 above the floor)
        Checkpoint("Checkpoint_LeftRoom", props, new Vector2(0.6f, 15.75f));
        Fill(3, 17, 4, 1);                  // top at y 18 (3 above the ledge)
        Fill(7, 14, 3, 1);
        Fill(13, 16, 3, 1);
        Fill(13, 20, 4, 1);                 // boss door ledge
        Pendulum("SpikeBall_LeftRoom", hazards, new Vector2(11f, 25f), 7.5f, 0.25f);
        Ring("Ring_BossLedge", props, new Vector2(15.5f, 23.5f), 4f);
        GameObject bossDoor = Box("BossDoor", props, new Vector2(15f, 22.1f), new Vector2(1.2f, 2.2f), BossDoorColor);
        bossDoor.GetComponent<BoxCollider2D>().isTrigger = true;
        var bd = new SerializedObject(bossDoor.AddComponent<BossDoor>());
        bd.FindProperty("bossScene").stringValue = BossSceneName;
        bd.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(bossDoor, bossDoor.GetComponent<BossDoor>());

        // ---- Right room: Secret Room C. Enter from below at x 34-39 with the hook ring ----
        Fill(23, 11, 13, 1);                // poison floor
        Puddle("Puddle_RightRoom", hazards, 23, 36, 12f);
        Ring("Ring_RightRoomEntry", props, new Vector2(38f, 11.5f), 20f);
        OneWay("Ledge_RightRoomEntry", props, 37, 14, 3); // landing ledge after the ring, top at y 15
        Checkpoint("Checkpoint_RightRoom", props, new Vector2(39.4f, 15.75f));
        Fill(33, 17, 4, 1);
        Fill(30, 14, 3, 1);
        Fill(24, 16, 3, 1);
        Fill(23, 20, 4, 1);                 // secret door ledge
        Pendulum("SpikeBall_RightRoom", hazards, new Vector2(29f, 25f), 7.5f, 0.75f);
        Ring("Ring_SecretLedge", props, new Vector2(24.5f, 23.5f), 4f);

        // Drips from under the room floors, down the sides of the shaft exit
        Dripper("Vent_ShaftLeft", hazards, new Vector2(16.5f, 10.6f), 0f);
        Dripper("Vent_ShaftRight", hazards, new Vector2(23.5f, 10.6f), 0f);
    }

    // Bottom: enemies (black circles) and two pits
    static void BuildBottom(Transform hazards, Transform enemies, Transform props)
    {
        Checkpoint("Checkpoint_Bottom", props, new Vector2(20f, 0.75f));
        Spitter("Spitter_Left", enemies, new Vector2(4.5f, 1f));
        Sludge("Sludge_Left", enemies, new Vector2(7.5f, 0.6f));
        Sludge("Sludge_Right", enemies, new Vector2(32.5f, 0.6f));
        Spitter("Spitter_Right", enemies, new Vector2(35.5f, 1f));
    }

    // ---------------- Secret rooms ----------------

    static void BuildSecretRooms(Transform secrets, Transform hazards, Transform props)
    {
        // A: inside the shaft under the start, on the dotted ledge
        SecretRoom("A", secrets, 46, new Vector2(20f, 50.1f + UpperLift), room =>
        {
            Pendulum("SpikeBall_SecretA", room, new Vector2(-37f, 59f), 6f, 0f);
        });
        // B: behind the blue breakable wall
        SecretRoom("B", secrets, 28, new Vector2(42.5f, 38.1f + UpperLift), room =>
        {
            Dripper("Vent_SecretB_1", room, new Vector2(-39.5f, 40.6f), 0f);
            Dripper("Vent_SecretB_2", room, new Vector2(-34.5f, 40.6f), 0.8f);
        });
        // C: in the right side room
        SecretRoom("C", secrets, 10, new Vector2(25f, 22.1f), room =>
        {
            Puddle("Puddle_SecretC", room, -44, -30, 10f);
            Fill(-43, 13, 3, 1);
            Fill(-37, 15, 3, 1);
            Fill(-31, 13, 3, 1);
        });
    }

    // One screen-sized room outside the tower (x -50 to -24) with a chest, an antidote and a way back
    static void SecretRoom(string id, Transform parent, int floorY, Vector2 doorPos, System.Action<Transform> extras)
    {
        const int left = -50, right = -24, inside = 13;
        var room = Group("SecretRoom_" + id, parent);

        Fill(left, floorY - 1, right - left + 1, 1);
        Fill(left, floorY + inside, right - left + 1, 1);
        Fill(left, floorY - 1, 1, inside + 2);
        Fill(right, floorY - 1, 1, inside + 2);

        Chest("Chest_Secret" + id, room, new Vector2(-37f, floorY + 0.5f));
        Antidote("Antidote_Secret" + id, room, new Vector2(-45f, floorY + 1f));

        var inSpawn = new GameObject("RoomSpawn").transform;
        inSpawn.SetParent(room);
        inSpawn.position = new Vector2(right - 4.5f, floorY + 1.1f);
        var backSpawn = new GameObject("ReturnSpawn").transform;
        backSpawn.SetParent(room);
        backSpawn.position = doorPos;

        GameObject entrance = Box("SecretDoor_" + id, room, doorPos, new Vector2(0.9f, 2.2f), DoorColor);
        entrance.GetComponent<BoxCollider2D>().isTrigger = true;
        var enter = new SerializedObject(entrance.AddComponent<SecretDoor>());
        enter.FindProperty("destination").objectReferenceValue = inSpawn;
        enter.FindProperty("entersRoom").boolValue = true;
        enter.FindProperty("roomMin").vector2Value = new Vector2(left, floorY - 1);
        enter.FindProperty("roomMax").vector2Value = new Vector2(right + 1, floorY + inside + 1);
        enter.ApplyModifiedPropertiesWithoutUndo();

        GameObject exitDoor = Box("ExitDoor_" + id, room, new Vector2(right - 2.5f, floorY + 1.1f), new Vector2(0.9f, 2.2f), DoorColor);
        exitDoor.GetComponent<BoxCollider2D>().isTrigger = true;
        var exit = new SerializedObject(exitDoor.AddComponent<SecretDoor>());
        exit.FindProperty("destination").objectReferenceValue = backSpawn;
        exit.FindProperty("entersRoom").boolValue = false;
        exit.ApplyModifiedPropertiesWithoutUndo();

        AddPrompt(entrance, entrance.GetComponent<SecretDoor>());
        AddPrompt(exitDoor, exitDoor.GetComponent<SecretDoor>());
        extras?.Invoke(room);
    }

    // ---------------- Player and camera ----------------

    static void PlacePlayerAndCamera(Scene scene)
    {
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (!player) { Debug.LogError("Build Level 2: no Player in Room_Template"); return; }

        float feet = 1f;
        if (player.TryGetComponent(out BoxCollider2D box)) feet = (box.size.y * 0.5f - box.offset.y) * player.transform.lossyScale.y;
        else if (player.TryGetComponent(out CapsuleCollider2D cap)) feet = (cap.size.y * 0.5f - cap.offset.y) * player.transform.lossyScale.y;
        player.transform.position = new Vector3(20f, 54f + UpperLift + feet + 0.05f, 0f); // purple dot

        SetUpCamera(player.transform);
    }

    // Menu item: apply the camera settings to the open Level_2 without rebuilding the level
    // (menu removed: one-time fix, already done)
    static void UpdateCamera()
    {
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (!player) { EditorUtility.DisplayDialog("Level 2 Camera", "Open Level_2 first (no Player found).", "OK"); return; }
        SetUpCamera(player.transform);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        EditorUtility.DisplayDialog("Level 2 Camera", "Camera updated and scene saved.", "OK");
    }

    static void SetUpCamera(Transform player)
    {
        Camera cam = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (!cam) return;
        cam.orthographicSize = CameraSize;
        cam.transform.position = new Vector3(player.position.x, player.position.y, cam.transform.position.z);
        if (!cam.TryGetComponent(out CameraFollow2D follow)) return;

        follow.enabled = true;
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player;
        so.FindProperty("useBounds").boolValue = true;
        so.FindProperty("minBounds").vector2Value = new Vector2(Left - 7, -1f);
        so.FindProperty("maxBounds").vector2Value = new Vector2(Right + 7, Top + 1);
        so.FindProperty("lockToFloors").boolValue = false;
        SerializedProperty zones = so.FindProperty("zones");
        zones.arraySize = CameraZones.Length;
        for (int i = 0; i < CameraZones.Length; i++)
        {
            zones.GetArrayElementAtIndex(i).FindPropertyRelative("bottom").floatValue = CameraZones[i].x;
            zones.GetArrayElementAtIndex(i).FindPropertyRelative("top").floatValue = CameraZones[i].y;
            zones.GetArrayElementAtIndex(i).FindPropertyRelative("size").floatValue = CameraZones[i].z;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- Pieces ----------------

    // Poison puddle along the top of a floor from x0 up to (not including) x1, at surface height y
    static void Puddle(string name, Transform parent, int x0, int x1, float y)
    {
        float width = x1 - x0;
        GameObject go = Box(name, parent, new Vector2(x0 + width * 0.5f, y + 0.12f), new Vector2(width, 0.25f), PoisonColor);
        var col = go.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 3f);    // reaches 0.6 above the surface (scaled by the 0.25 height)
        col.offset = new Vector2(0f, 1f);
        go.AddComponent<PoisonHazard>().Setup(0, 0f, 2, 2f, 0.5f, false);
    }

    // Slimy wall strip: touching it poisons
    static void Slime(string name, Transform parent, Vector2 center, Vector2 size)
    {
        GameObject go = Box(name, parent, center, size, PoisonColor);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<PoisonHazard>().Setup(0, 0f, 2, 2f, 0.5f, false);
    }

    static void Dripper(string name, Transform parent, Vector2 pos, float delay)
    {
        GameObject go = Box(name, parent, pos, new Vector2(0.8f, 0.8f), VentColor);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
        var so = new SerializedObject(go.AddComponent<PoisonDripper>());
        so.FindProperty("startDelay").floatValue = delay;
        so.FindProperty("groundLayer").intValue = 1 << groundLayer;
        so.FindProperty("interval").floatValue = DripInterval;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Swinging spiked poison ball hanging from a pivot
    static void Pendulum(string name, Transform parent, Vector2 pivot, float length, float phase)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = pivot + Vector2.up * lift;
        var so = new SerializedObject(root.AddComponent<PoisonPendulum>());
        so.FindProperty("phase").floatValue = phase;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject chain = Box("Chain", root.transform, pivot + Vector2.down * length * 0.5f, new Vector2(0.15f, length), ChainColor);
        Object.DestroyImmediate(chain.GetComponent<BoxCollider2D>());

        GameObject ball = Box("SpikeBall", root.transform, pivot + Vector2.down * length, new Vector2(1.3f, 1.3f), SpikeColor);
        ball.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        Object.DestroyImmediate(ball.GetComponent<BoxCollider2D>());
        var col = ball.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.55f;
        ball.AddComponent<PoisonHazard>().Setup(10, 11f, 2, 3f, 0.5f, false);

        GameObject spikes = Box("Spikes", ball.transform, pivot + Vector2.down * length, new Vector2(1.3f, 1.3f), SpikeColor);
        spikes.transform.localScale = Vector3.one;
        spikes.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // second square turned 45 degrees: an 8-point star
        Object.DestroyImmediate(spikes.GetComponent<BoxCollider2D>());
    }

    // Slow slime that chases you along the floor. Touch = damage + poison.
    static void Sludge(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(1.2f, 1.1f), SludgeColor);
        AddHealth(go, 16);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        var chase = new SerializedObject(go.AddComponent<EnemyChase>());
        chase.FindProperty("speed").floatValue = 3f;
        chase.FindProperty("detectRange").floatValue = 10f;
        chase.FindProperty("stopDistance").floatValue = 0.3f;
        chase.FindProperty("groundLayer").intValue = 1 << groundLayer;
        chase.ApplyModifiedPropertiesWithoutUndo();
        var touch = new SerializedObject(go.AddComponent<ContactDamage>());
        touch.FindProperty("damage").intValue = 8;
        touch.ApplyModifiedPropertiesWithoutUndo();

        var aura = new GameObject("PoisonTouch");
        aura.transform.SetParent(go.transform, false);
        var auraCol = aura.AddComponent<BoxCollider2D>();
        auraCol.isTrigger = true;
        auraCol.size = new Vector2(1.25f, 1.25f);
        aura.AddComponent<PoisonHazard>().Setup(0, 0f, 2, 3f, 0.5f, false);
    }

    // Stands still and shoots at you
    static void Spitter(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(1f, 2f), SpitterColor);
        AddHealth(go, 12);
        var so = new SerializedObject(go.AddComponent<EnemyShooter>());
        so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>(ProjectilePrefab);
        so.FindProperty("range").floatValue = 12f;
        so.FindProperty("fireRate").floatValue = 1.4f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Ring(string name, Transform parent, Vector2 pos, float launchSpeed)
    {
        GameObject ring = Box(name, parent, pos, new Vector2(0.6f, 0.6f), RingColor);
        Object.DestroyImmediate(ring.GetComponent<BoxCollider2D>());
        var col = ring.AddComponent<CircleCollider2D>();
        col.radius = 1f;
        col.isTrigger = true;
        var so = new SerializedObject(ring.AddComponent<HookPoint>());
        so.FindProperty("launchSpeed").floatValue = launchSpeed;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Thin platform you can jump up through from below, and drop through by holding Down (the dotted lines)
    static void OneWay(string name, Transform parent, int x, int y, int width)
    {
        GameObject go = Box(name, parent, new Vector2(x + width * 0.5f, y + 0.75f), new Vector2(width, 0.5f), OneWayColor);
        go.GetComponent<BoxCollider2D>().usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 160f;
        go.AddComponent<OneWayPlatform>(); // hold Down to drop through it
    }

    static void Chest(string name, Transform parent, Vector2 pos)
    {
        GameObject chest = Box(name, parent, pos, new Vector2(1.5f, 1f), ChestColor);
        var col = chest.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2f, 2.5f);
        chest.AddComponent<Chest>();
        AddPrompt(chest, chest.GetComponent<Chest>());
    }

    static void Antidote(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(0.6f, 0.6f), AntidoteColor);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<HealthPickup>();
    }

    static void Checkpoint(string name, Transform parent, Vector2 pos)
    {
        GameObject cp = Box(name, parent, pos, new Vector2(0.3f, 1.5f), CheckpointColor);
        cp.GetComponent<BoxCollider2D>().isTrigger = true;
        cp.AddComponent<Checkpoint>();
    }

    static void MakeKillZone(string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = center;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
        go.AddComponent<KillZone>();
    }

    // ---------------- Helpers ----------------

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        return t;
    }

    // Paints (or erases) a block of tiles. x, y = bottom-left tile.
    static void Fill(int x, int y, int width, int height, bool paint = true)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                map.SetTile(map.WorldToCell(new Vector3(x + i + 0.5f, y + lift + j + 0.5f)), paint ? tile : null);
    }

    // A plain coloured box with a solid collider. Swap the sprite later for real art.
    static GameObject Box(string name, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center + Vector2.up * lift;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boxSprite;
        sr.color = color;
        sr.sortingOrder = 1;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    static void AddHealth(GameObject go, int maxHealth)
    {
        var so = new SerializedObject(go.AddComponent<Health>());
        so.FindProperty("maxHealth").intValue = maxHealth;
        so.FindProperty("invincibleTime").floatValue = 0.1f; // so fast punches count
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // "Press E" pop-up above a door or chest (hidden until the player is near)
    static void AddPrompt(GameObject target, Component owner)
    {
        var so = new SerializedObject(owner);
        var promptProp = so.FindProperty("prompt");
        if (promptProp == null) return;

        var go = new GameObject(target.name + "_PressE");
        go.transform.SetParent(target.transform.parent, false);
        Bounds b = target.GetComponent<SpriteRenderer>().bounds;
        go.transform.position = new Vector3(b.center.x, b.max.y + 0.6f, 0f);
        var text = go.AddComponent<TMPro.TextMeshPro>();
        text.text = "Press E";
        text.fontSize = 4;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        text.GetComponent<MeshRenderer>().sortingOrder = 20;
        go.SetActive(false);

        promptProp.objectReferenceValue = go;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void RebuildColliders(Tilemap tilemap)
    {
        tilemap.CompressBounds();
        if (!tilemap.TryGetComponent(out TilemapCollider2D tc)) return;
        tc.enabled = false;
        tc.enabled = true;
        tc.ProcessTilemapChanges();
        if (!tilemap.TryGetComponent(out CompositeCollider2D composite)) return;
        composite.enabled = false;
        composite.enabled = true;
        composite.GenerateGeometry();
    }

    static void DestroyByName(Scene scene, string name)
    {
        var doomed = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) doomed.Add(t.gameObject);
        foreach (GameObject go in doomed) if (go) Object.DestroyImmediate(go);
    }

    static Tilemap FindGroundTilemap(Scene scene)
    {
        int ground = LayerMask.NameToLayer("Ground");
        Tilemap first = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Tilemap m in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (m.gameObject.layer == ground) return m;
                if (!first) first = m;
            }
        return first;
    }

    static TileBase FirstTile(Tilemap tilemap)
    {
        foreach (TileBase t in tilemap.GetTilesBlock(tilemap.cellBounds))
            if (t) return t;
        return null;
    }
}