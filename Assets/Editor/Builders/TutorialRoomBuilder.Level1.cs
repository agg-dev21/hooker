using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Part of TutorialRoomBuilder (see TutorialRoomBuilder.cs for its settings and the main loop).
// Level 1 (the ship's lower decks), its secret rooms and the outside room.
public static partial class TutorialRoomBuilder
{
    static float L1HalfWidth => L1HalfHeight * 16f / 9f; // about 13.3

    // Top of the floor you stand on, on floor k (0 = bottom floor, 4 = deck)
    static int FloorTop(int k) => -6 + k * FloorSpacing;
    // Left x of the 3-wide hole that leads up onto floor k (k = 1..4)
    static int HoleX(int k) => k % 2 == 1 ? 62 : -12;

    [MenuItem("Tools/Other/Rebuild Levels (wipes changes)/Level 1 (Ship)", priority = 3000)]
    static void BuildLevel1()
    {
        string path = ScenePath(Level1Name);
        if (File.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("Build Level 1",
                    Level1Name + " already exists.\n\nReplace it with a fresh build? Anything you changed in it by hand will be lost.",
                    "Replace", "Cancel"))
                return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // Level_1 cannot be open while replacing it
            AssetDatabase.DeleteAsset(path);
        }
        if (!AssetDatabase.CopyAsset(ScenePath(TemplateName), path))
        {
            Debug.LogError("Could not copy Room_Template");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        DestroyByName("Exit");
        DestroyByName("LeftWall");

        Tilemap map = FindGroundTilemap();
        TileBase tile = FirstTile(map) ?? AssetDatabase.LoadAssetAtPath<TileBase>(FallbackTile);
        map.ClearAllTiles();

        int width = WallRight - WallLeft + 1;
        int bottom = FloorTop(0) - 1;

        // Bottom floor and walls, up to the top ceiling (the deck is its own scene)
        int wallTop = FloorTop(Floors) - 1;
        FillTiles(map, tile, WallLeft, bottom, width, 1, true);
        FillTiles(map, tile, WallLeft, bottom, 1, wallTop - bottom + 1, true);
        FillTiles(map, tile, WallRight, bottom, 1, wallTop - bottom + 1, true);

        var structure = new GameObject("Structure").transform;
        var obstacles = new GameObject("Obstacles").transform;

        // Dividers between floors, each with a hole, a ring and a checkpoint
        for (int k = 1; k <= Floors; k++)
        {
            int ceilingY = FloorTop(k - 1) + FloorInside; // ceiling row of the floor below
            FillTiles(map, tile, WallLeft, ceilingY, width, 2, true);
            FillTiles(map, null, HoleX(k), ceilingY, 3, 2, false); // the hole

            // Ring sits in the hole, at the top of the screen of the floor below, so you can see it
            GameObject ring = MakeBox("HookRing_" + k, structure, map, new Vector2(HoleX(k) + 1.5f, ceilingY + 0.5f), new Vector2(0.6f, 0.6f), RingColor);
            Object.DestroyImmediate(ring.GetComponent<BoxCollider2D>());
            var ringCol = ring.AddComponent<CircleCollider2D>();
            ringCol.radius = 1f; // bigger than the ring so it is easy to hit
            ringCol.isTrigger = true;
            var hookPoint = ring.AddComponent<HookPoint>();
            var hso = new SerializedObject(hookPoint);
            hso.FindProperty("launchSpeed").floatValue = 14f; // enough to pop up through the 2-tile divider
            if (k == Floors) hso.FindProperty("nextScene").stringValue = DeckSceneName; // top ring: fade to black, go to the deck
            hso.ApplyModifiedPropertiesWithoutUndo();
            if (k == Floors) continue; // no checkpoint above the top ring

            // Checkpoint beside the hole, on the side you walk to
            float cpX = k % 2 == 1 ? HoleX(k) - 1.5f : HoleX(k) + 4.5f;
            GameObject cp = MakeBox("Checkpoint_" + k, structure, map, new Vector2(cpX, FloorTop(k) + 0.75f), new Vector2(0.3f, 1.5f), CheckpointColor);
            cp.GetComponent<BoxCollider2D>().isTrigger = true;
            cp.AddComponent<Checkpoint>();
        }

        // ---- Obstacles (no enemies). Each floor is 3 screens wide (x -13 to 66). ----
        // Tuned for the current movement: held jump about 8.5 tiles high, running jump about 8.3 tiles far,
        // jump + dash about 13.6 tiles. Walls are 7 tall (hold jump), crate stacks 10 tall (break them),
        // dash pits 12 wide (jump + dash), floor gaps 7-8 wide (a real jump).
        const int Stack = 5; // crates per stack (each crate is 2 tall)

        // Floor 1 (start left, hole up on the right):
        // jump pit, dash pit, crate stack, ceiling tendril, barrel next to a crate stack, two more tendrils
        int t = FloorTop(0);
        FillTiles(map, null, -9, bottom, 6, 1, false);  // jump pit (6 wide)
        FillTiles(map, null, 0, bottom, 12, 1, false);  // dash pit (12 wide: jump + dash)
        KillZone kz = Object.FindAnyObjectByType<KillZone>(FindObjectsInactive.Include);
        if (!kz) kz = new GameObject("KillZone").AddComponent<KillZone>();
        kz.transform.position = new Vector3(1.5f, bottom - 1.5f, 0f);
        kz.transform.localScale = Vector3.one;
        if (!kz.TryGetComponent(out BoxCollider2D kzCol)) kzCol = kz.gameObject.AddComponent<BoxCollider2D>();
        kzCol.size = new Vector2(24f, 1f);
        kzCol.offset = Vector2.zero;
        kzCol.isTrigger = true;
        MakeCrateStack("CrateStack_F1a", obstacles, map, 16f, t, Stack);
        MakeTendril("Tendril_F1a", obstacles, map, new Vector2(18.5f, t + FloorInside), Vector2.down, FloorInside, 0f);
        MakeBarrel("Barrel_F1", obstacles, map, new Vector2(21.5f, t + 0.6f));     // touch it, run, it blows the crates
        MakeCrateStack("CrateStack_F1b", obstacles, map, 24f, t, Stack);
        MakeTendril("Tendril_F1b", obstacles, map, new Vector2(28f, t + FloorInside), Vector2.down, FloorInside, 0.8f);
        MakeTendril("Tendril_F1c", obstacles, map, new Vector2(31f, t + FloorInside), Vector2.down, FloorInside, 1.6f);
        // third screen: dash pit, low beam, barrels, ceiling tendril
        FillTiles(map, null, 40, bottom, 12, 1, false);   // dash pit (12 wide)
        MakeKillZone("KillZone_F1b", obstacles, new Vector2(46f, bottom - 1.5f), new Vector2(14f, 1f));
        FillTiles(map, tile, 54, t + 3, 3, FloorInside - 3, true); // low beam
        MakeBarrel("Barrel_F1b", obstacles, map, new Vector2(58.5f, t + 0.6f));
        MakeBarrel("Barrel_F1c", obstacles, map, new Vector2(59.5f, t + 0.6f));
        MakeTendril("Tendril_F1d", obstacles, map, new Vector2(61f, t + FloorInside), Vector2.down, FloorInside, 0.4f);

        // Floor 2 (arrive right, hole up on the left):
        // third screen: tendrils bursting from the floor, crate stack, ceiling cannon, gap (falls to floor 1)
        // then: 7-tall wall, row of barrels to jump over, ceiling cannon, two tendrils, another 7-tall wall, low beam, wall cannon
        t = FloorTop(1);
        MakeTendril("Tendril_F2c", obstacles, map, new Vector2(57.5f, t), Vector2.up, 10f, 0f);
        MakeTendril("Tendril_F2d", obstacles, map, new Vector2(54.5f, t), Vector2.up, 10f, 0.6f);
        MakeCrateStack("CrateStack_F2", obstacles, map, 50.5f, t, Stack);
        MakeCannon("Cannon_F2_Ceiling2", obstacles, map, new Vector2(44.5f, t + FloorInside - 0.5f));
        FillTiles(map, null, 39, t - 2, 8, 2, false);     // gap (8 wide)
        FillTiles(map, tile, 30, t, 1, 7, true);          // 7-tall wall (hold jump)
        MakeBarrel("Barrel_F2a", obstacles, map, new Vector2(25.5f, t + 0.6f));
        MakeBarrel("Barrel_F2b", obstacles, map, new Vector2(24.5f, t + 0.6f));
        MakeBarrel("Barrel_F2c", obstacles, map, new Vector2(23.5f, t + 0.6f));
        MakeCannon("Cannon_F2_Ceiling", obstacles, map, new Vector2(20.5f, t + FloorInside - 0.5f));
        MakeTendril("Tendril_F2a", obstacles, map, new Vector2(15f, t + FloorInside), Vector2.down, FloorInside, 0f);
        MakeTendril("Tendril_F2b", obstacles, map, new Vector2(10f, t + FloorInside), Vector2.down, FloorInside, 1.2f);
        FillTiles(map, tile, 3, t, 1, 7, true);           // 7-tall wall (hold jump)
        FillTiles(map, tile, -6, t + 3, 3, FloorInside - 3, true); // low beam hanging from the ceiling, 3-tall gap under it
        MakeCannon("Cannon_F2_Wall", obstacles, map, new Vector2(-12.5f, t + 1f));

        // Floor 3 (arrive left, hole up on the right):
        // platform up onto a 10-tall wall, gap (falls to floor 2), tendrils bursting up from the floor,
        // barrel next to a crate stack, second gap, cannon on the right wall
        t = FloorTop(2);
        FillTiles(map, tile, -7, t + 2, 4, 1, true);      // platform, top 3 above the floor, touching the wall (no 1-tile gap to get stuck in)
        FillTiles(map, tile, -3, t, 1, 10, true);         // 10-tall wall: too high from the floor, reachable from the platform
        FillTiles(map, null, 2, t - 2, 7, 2, false);      // gap (7 wide)
        MakeTendril("Tendril_F3a", obstacles, map, new Vector2(12f, t), Vector2.up, 10f, 0f);
        MakeTendril("Tendril_F3b", obstacles, map, new Vector2(15f, t), Vector2.up, 10f, 0.7f);
        MakeBarrel("Barrel_F3", obstacles, map, new Vector2(19.5f, t + 0.6f));
        MakeCrateStack("CrateStack_F3", obstacles, map, 22f, t, Stack);
        FillTiles(map, null, 26, t - 2, 7, 2, false);     // second gap (7 wide)
        MakeCannon("Cannon_F3", obstacles, map, new Vector2(WallRight - 0.5f, t + 2f)); // a bit higher: its top is the step up to the secret ledge
        // third screen: 14-wide gap with one small high platform, two ceiling tendrils, barrel next to a crate stack
        FillTiles(map, null, 38, t - 2, 14, 2, false);    // wide gap (falls to floor 2)
        FillTiles(map, tile, 44, t + 4, 2, 1, true);      // small platform, top 5 above the floor
        MakeTendril("Tendril_F3c", obstacles, map, new Vector2(54f, t + FloorInside), Vector2.down, FloorInside, 0f);
        MakeTendril("Tendril_F3d", obstacles, map, new Vector2(56.5f, t + FloorInside), Vector2.down, FloorInside, 0.7f);
        MakeBarrel("Barrel_F3b", obstacles, map, new Vector2(57.5f, t + 0.6f));
        MakeCrateStack("CrateStack_F3b", obstacles, map, 59.5f, t, Stack);

        // Floor 4 (arrive right, hole up on the left):
        // gaps (fall to floor 3), crate stacks, barrels under a tendril, sideways tendril sweeping the floor near the hole,
        // low beam, cannons on both walls
        t = FloorTop(3);
        FillTiles(map, null, 27, t - 2, 7, 2, false);     // gap (7 wide)
        MakeCrateStack("CrateStack_F4", obstacles, map, 22f, t, Stack);
        MakeBarrel("Barrel_F4a", obstacles, map, new Vector2(18.5f, t + 0.6f));
        MakeBarrel("Barrel_F4b", obstacles, map, new Vector2(17.5f, t + 0.6f));
        MakeTendril("Tendril_F4a", obstacles, map, new Vector2(14f, t + FloorInside), Vector2.down, FloorInside, 0f);
        FillTiles(map, tile, -8, t + 3, 3, FloorInside - 3, true); // low beam
        MakeTendril("Tendril_F4b", obstacles, map, new Vector2(-13f, t + 1f), Vector2.right, 10f, 0.5f); // sweeps the floor
        MakeCannon("Cannon_F4_Left", obstacles, map, new Vector2(-12.5f, t + 6f));
        MakeCannon("Cannon_F4_Right", obstacles, map, new Vector2(WallRight - 0.5f, t + 8f));
        // third screen: barrels, two ceiling tendrils, gap (falls to floor 3, maybe further), crate stack
        MakeBarrel("Barrel_F4c", obstacles, map, new Vector2(56.5f, t + 0.6f));
        MakeBarrel("Barrel_F4d", obstacles, map, new Vector2(55.5f, t + 0.6f));
        MakeTendril("Tendril_F4c", obstacles, map, new Vector2(52f, t + FloorInside), Vector2.down, FloorInside, 0f);
        MakeTendril("Tendril_F4d", obstacles, map, new Vector2(49f, t + FloorInside), Vector2.down, FloorInside, 0.6f);
        FillTiles(map, null, 41, t - 2, 7, 2, false);     // gap (7 wide)
        MakeCrateStack("CrateStack_F4b", obstacles, map, 37f, t, Stack);

        // ---- Secret rooms: outside the ship, one per floor, reached through a visible but hard-to-reach door ----
        var secrets = new GameObject("SecretRooms").transform;

        // Floor 1: door on a small ledge high on the left wall, reached by hooking a small ring in the corner
        t = FloorTop(0);
        FillTiles(map, tile, -13, t + 9, 2, 1, true); // ledge, top 10 above the floor (too high to jump to)
        GameObject cornerRing = MakeBox("SecretRing_F1", secrets, map, new Vector2(-11.5f, t + 11.9f), new Vector2(0.4f, 0.4f), RingColor);
        Object.DestroyImmediate(cornerRing.GetComponent<BoxCollider2D>());
        var cornerCol = cornerRing.AddComponent<CircleCollider2D>();
        cornerCol.radius = 1.5f;
        cornerCol.isTrigger = true;
        var cornerHook = new SerializedObject(cornerRing.AddComponent<HookPoint>());
        cornerHook.FindProperty("launchSpeed").floatValue = 2f; // just drop onto the ledge
        cornerHook.ApplyModifiedPropertiesWithoutUndo();
        MakeOutsideRoom(0, secrets, map, tile, new Vector2(-12.4f, t + 11.1f));
        MakeTendril("Guard_F1", secrets, map, new Vector2(-13f, t + 11.2f), Vector2.right, 4f, 0.3f); // sweeps the ledge

        // Floor 2: door on the 1-tile strip between the hole down and the right wall (miss and you fall)
        MakeOutsideRoom(1, secrets, map, tile, new Vector2(WallRight - 0.5f, FloorTop(1) + 1.1f));
        MakeTendril("Guard_F2", secrets, map, new Vector2(WallRight - 0.5f, FloorTop(1) + FloorInside), Vector2.down, FloorInside, 0.9f); // drops onto the strip

        // Floor 3: door on a high ledge in the right corner, reached by jumping off the cannon
        t = FloorTop(2);
        // Narrow perch by the wall, top 10 above the floor: out of the way of the hook to the ring above,
        // too high to jump to from the floor, reachable by jumping straight up off the cannon
        FillTiles(map, tile, 64, t + 9, 1, 1, true);
        MakeOutsideRoom(2, secrets, map, tile, new Vector2(64.5f, t + 11.1f));
        MakeTendril("Guard_F3", secrets, map, new Vector2(WallRight, t + 11.2f), Vector2.left, 4f, 0.5f); // sweeps the ledge

        // Floor 4: door at the bottom of the left wall, right where the sideways tendril comes out
        MakeOutsideRoom(3, secrets, map, tile, new Vector2(-12.4f, FloorTop(3) + 1.1f));
        MakeBarrel("Guard_F4", secrets, map, new Vector2(-10.5f, FloorTop(3) + 0.6f)); // jump over it without touching it

        RebuildColliders(map);

        // Player at the bottom left
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        float feet = 1f;
        if (player.TryGetComponent(out CapsuleCollider2D capsule))
            feet = (capsule.size.y * 0.5f - capsule.offset.y) * player.transform.lossyScale.y;
        player.transform.position = new Vector3(-11f, FloorTop(0) + feet + 0.05f, 0f);

        // Camera scrolls left/right inside the floor, shows only the floor you are on, and slides to the next floor when you go up or down
        Camera cam = Object.FindAnyObjectByType<Camera>();
        cam.transform.position = new Vector3(0f, L1FirstFloorCenterY, cam.transform.position.z);
        cam.orthographicSize = L1HalfHeight;
        if (cam.TryGetComponent(out CameraFollow2D follow))
        {
            follow.enabled = true;
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.FindProperty("useBounds").boolValue = true;
            so.FindProperty("minBounds").vector2Value = new Vector2(-L1HalfWidth, L1FirstFloorCenterY - L1HalfHeight);
            so.FindProperty("maxBounds").vector2Value = new Vector2(5f * L1HalfWidth, L1FirstFloorCenterY - L1HalfHeight + Floors * FloorSpacing);
            so.FindProperty("lockToFloors").boolValue = true;
            so.FindProperty("floorHeight").floatValue = FloorSpacing;
            so.FindProperty("firstFloorCenterY").floatValue = L1FirstFloorCenterY;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorKit.AddToSceneList(path);

        EditorUtility.DisplayDialog("Build Level 1", "Done! Level_1 is built and open, and in the Scene List.", "OK");
    }

    // A secret room outside the ship (x -50 to -24), at the same height as floor k, one screen big.
    // The main camera never goes there. The entrance door (at doorPos, in the level) brings you in and the
    // camera switches to the room; the exit door inside brings you back. An empty chest sits in the middle.
    // ---------------- Secret room obstacles ----------------
    // Like the Level 2 secret rooms: each room has an obstacle guarding the chest, and a health potion as a reward.
    // Hardy-themed (goo tendrils, barrels, cannons, crates), no poison. You come in on the right; the chest is in the middle.

    static void AddSecretRoomObstacles(int k, Transform room, Tilemap map)
    {
        // Replace any obstacles from an earlier run
        Transform old = room.Find("RoomObstacles");
        if (old) Object.DestroyImmediate(old.gameObject);
        var g = new GameObject("RoomObstacles").transform;
        g.SetParent(room);

        const float wallInside = -48.5f; // just inside the room's left wall (x -50 to -49)
        int t = FloorTop(k);

        if (k == 0)
        {
            // Three goo tendrils dropping from the ceiling one after another
            MakeTendril("RoomTendril_1", g, map, new Vector2(-31f, t + FloorInside), Vector2.down, FloorInside, 0f);
            MakeTendril("RoomTendril_2", g, map, new Vector2(-34f, t + FloorInside), Vector2.down, FloorInside, 0.5f);
            MakeTendril("RoomTendril_3", g, map, new Vector2(-41f, t + FloorInside), Vector2.down, FloorInside, 1f);
        }
        else if (k == 1)
        {
            // A tendril sweeping the floor from the far wall, and a barrel to jump over without touching
            MakeTendril("RoomSweeper", g, map, new Vector2(wallInside - 0.5f, t + 1f), Vector2.right, 11f, 0.3f);
            MakeBarrel("RoomBarrel", g, map, new Vector2(-33.5f, t + 0.6f));
        }
        else if (k == 2)
        {
            // Two tendrils bursting up from the floor, and a cannon on the far wall
            MakeTendril("RoomTendril_Up1", g, map, new Vector2(-31f, t), Vector2.up, 10f, 0f);
            MakeTendril("RoomTendril_Up2", g, map, new Vector2(-41f, t), Vector2.up, 10f, 0.7f);
            MakeCannon("RoomCannon", g, map, new Vector2(wallInside, t + 1f));
        }
        else
        {
            // Crate stack in front of the chest (punch it, or touch the barrel and run), a cannon and a ceiling tendril
            MakeCrateStack("RoomCrates", g, map, -33f, t, 5);
            MakeBarrel("RoomBarrel", g, map, new Vector2(-30.5f, t + 0.6f));
            MakeCannon("RoomCannon", g, map, new Vector2(wallInside, t + 6f));
            MakeTendril("RoomTendril", g, map, new Vector2(-42f, t + FloorInside), Vector2.down, FloorInside, 0.4f);
        }

        // Reward: a health potion on the far side (heals 30)
        GameObject potion = MakeBox("HealthPotion", g, map, new Vector2(-45f, t + 1f), new Vector2(0.6f, 0.6f), new Color(0.9f, 0.15f, 0.2f));
        potion.GetComponent<BoxCollider2D>().isTrigger = true;
        potion.AddComponent<HealthPickup>();
    }

    static void MakeOutsideRoom(int k, Transform parent, Tilemap map, TileBase tile, Vector2 doorPos)
    {
        const int left = -50, right = -24;
        int t = FloorTop(k);
        var room = new GameObject("SecretRoom_F" + (k + 1)).transform;
        room.SetParent(parent);
        Color doorColor = new Color(0.3f, 0.18f, 0.08f);

        // Room shell: floor, ceiling, side walls
        FillTiles(map, tile, left, t - 1, right - left + 1, 1, true);
        FillTiles(map, tile, left, t + FloorInside, right - left + 1, 1, true);
        FillTiles(map, tile, left, t - 1, 1, FloorInside + 2, true);
        FillTiles(map, tile, right, t - 1, 1, FloorInside + 2, true);

        // Chest in the middle
        GameObject chest = MakeBox("Chest", room, map, new Vector2((left + right + 1) * 0.5f, t + 0.5f), new Vector2(1.5f, 1f), new Color(0.75f, 0.55f, 0.15f));
        var chestCol = chest.GetComponent<BoxCollider2D>();
        chestCol.isTrigger = true;
        chestCol.size = new Vector2(2f, 2.5f);
        var chestSo = new SerializedObject(chest.AddComponent<Chest>());
        // What each Level 1 secret room's chest gives (floor 1 to 4)
        ItemType[] items = { ItemType.Healing, ItemType.Shield, ItemType.Scroll, ItemType.InstantRevival };
        chestSo.FindProperty("givesItem").boolValue = true;
        chestSo.FindProperty("item").enumValueIndex = (int)items[Mathf.Clamp(k, 0, items.Length - 1)];
        chestSo.FindProperty("amount").intValue = k == 0 ? 2 : 1;
        if (k == 2)
        {
            chestSo.FindProperty("scrollTitle").stringValue = "A Torn Note";
            chestSo.FindProperty("scrollText").stringValue = "...the woman in the blue coat was taken aboard a black-sailed ship heading east.\n\n(Type the real hint about the mother here, in the chest's Inspector.)";
        }
        chestSo.ApplyModifiedPropertiesWithoutUndo();

        // Where you appear inside the room, and where you appear back in the level
        var inside = new GameObject("RoomSpawn").transform;
        inside.SetParent(room);
        inside.position = new Vector2(right - 4.5f, t + 1.1f);
        var back = new GameObject("ReturnSpawn").transform;
        back.SetParent(room);
        back.position = doorPos;

        // Entrance door in the level
        GameObject entrance = MakeBox("SecretDoor_F" + (k + 1), room, map, doorPos, new Vector2(0.9f, 2.2f), doorColor);
        entrance.GetComponent<BoxCollider2D>().isTrigger = true;
        var enter = new SerializedObject(entrance.AddComponent<SecretDoor>());
        enter.FindProperty("destination").objectReferenceValue = inside;
        enter.FindProperty("entersRoom").boolValue = true;
        enter.FindProperty("roomMin").vector2Value = new Vector2(left, t - 1);
        enter.FindProperty("roomMax").vector2Value = new Vector2(right + 1, t + FloorInside + 1);
        enter.ApplyModifiedPropertiesWithoutUndo();

        // Exit door inside the room
        GameObject exitDoor = MakeBox("ExitDoor", room, map, new Vector2(right - 2.5f, t + 1.1f), new Vector2(0.9f, 2.2f), doorColor);
        exitDoor.GetComponent<BoxCollider2D>().isTrigger = true;
        var exit = new SerializedObject(exitDoor.AddComponent<SecretDoor>());
        exit.FindProperty("destination").objectReferenceValue = back;
        exit.FindProperty("entersRoom").boolValue = false;
        exit.ApplyModifiedPropertiesWithoutUndo();

        AddPrompt(entrance, entrance.GetComponent<SecretDoor>());
        AddPrompt(exitDoor, exitDoor.GetComponent<SecretDoor>());
        AddPrompt(chest, chest.GetComponent<Chest>());

        AddSecretRoomObstacles(k, room, map);
    }
}
