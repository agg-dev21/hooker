using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Editor-only helper. Adds the menu item Tools > Build Tutorial Rooms.
// Makes Room_Template from SampleScene, then the 7 tutorial rooms from Room_Template,
// links every Exit to the next room, and adds the rooms to the Scene List.
// It never overwrites: if any of the scenes already exist, it stops.
public static class TutorialRoomBuilder
{
    const string ScenesFolder = "Assets/Scenes";
    const string SourceScene = "Assets/Scenes/SampleScene.unity";
    const string TemplateName = "Room_Template";
    const string FallbackTile = "Assets/Tiles/Square.asset";

    static readonly string[] Rooms =
    {
        "Tutorial_1_Move",
        "Tutorial_2_Jump",
        "Tutorial_3_Dash",
        "Tutorial_4_Punch",
        "Tutorial_5_Hook",
        "Tutorial_6_DashThroughBullets",
        "Tutorial_7_Dock",
    };

    // Camera frame: orthographic size 5 at 16:9
    const float HalfHeight = 5f;
    const float HalfWidth = HalfHeight * 16f / 9f; // about 8.89
    const int FloorRows = 2;

    // (menu removed: replaced by Tools > Rebuild Tutorial)
    static void Build()
    {
        string templatePath = ScenePath(TemplateName);

        // Stop if anything already exists, so nothing gets overwritten
        var existing = new List<string>();
        if (File.Exists(templatePath)) existing.Add(TemplateName);
        foreach (string room in Rooms)
            if (File.Exists(ScenePath(room))) existing.Add(room);
        if (existing.Count > 0)
        {
            EditorUtility.DisplayDialog("Build Tutorial Rooms",
                "These scenes already exist, so nothing was changed:\n\n" + string.Join("\n", existing), "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1) Room_Template
        if (!AssetDatabase.CopyAsset(SourceScene, templatePath))
        {
            Debug.LogError("Could not copy " + SourceScene);
            return;
        }
        Scene template = EditorSceneManager.OpenScene(templatePath, OpenSceneMode.Single);
        SetUpTemplate();
        EditorSceneManager.SaveScene(template);

        // 2) The 7 rooms, each Exit pointing to the next room
        for (int i = 0; i < Rooms.Length; i++)
        {
            string path = ScenePath(Rooms[i]);
            AssetDatabase.CopyAsset(templatePath, path);
            Scene room = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            string next = i + 1 < Rooms.Length ? Rooms[i + 1] : ""; // last room: no next scene yet
            SceneExit exit = Object.FindAnyObjectByType<SceneExit>();
            var so = new SerializedObject(exit);
            so.FindProperty("nextScene").stringValue = next;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(room);
        }

        // 3) Scene List, in order, after the scenes already there
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string room in Rooms)
            list.Add(new EditorBuildSettingsScene(ScenePath(room), true));
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene(ScenePath(Rooms[0]), OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Build Tutorial Rooms",
            "Done! Made Room_Template and 7 tutorial rooms, and added the rooms to the Scene List.\n\nTutorial_1_Move is now open.", "OK");
    }

    static void SetUpTemplate()
    {
        // Remove Enemy and Checkpoint
        DestroyByName("Enemy");
        DestroyByName("Checkpoint");

        // Rename the KillZone object
        foreach (KillZone kz in Object.FindObjectsByType<KillZone>(FindObjectsInactive.Include))
            if (kz.gameObject.name == "GameObject") kz.gameObject.name = "KillZone";

        // Camera: no follow, at (0,0), size 5
        Camera cam = Object.FindAnyObjectByType<Camera>();
        if (cam.TryGetComponent(out CameraFollow2D follow)) follow.enabled = false;
        cam.transform.position = new Vector3(0f, 0f, cam.transform.position.z);
        cam.orthographic = true;
        cam.orthographicSize = HalfHeight;

        // Ground: clear old tiles and paint a floor across the whole frame
        Tilemap tilemap = FindGroundTilemap();
        TileBase tile = FirstTile(tilemap) ?? AssetDatabase.LoadAssetAtPath<TileBase>(FallbackTile);
        tilemap.ClearAllTiles();
        int xMin = Mathf.FloorToInt(-HalfWidth) - 1;
        int xMax = Mathf.CeilToInt(HalfWidth) + 2; // a bit past the right edge so the player reaches the Exit
        for (int x = xMin; x < xMax; x++)
            for (int row = 0; row < FloorRows; row++)
                tilemap.SetTile(tilemap.WorldToCell(new Vector3(x + 0.5f, -HalfHeight + row + 0.5f)), tile);
        RebuildColliders(tilemap);
        float floorTop = -HalfHeight + FloorRows;

        // LeftWall at the left edge
        var wall = new GameObject("LeftWall");
        wall.transform.position = new Vector3(-HalfWidth - 0.5f, 0f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 20f);

        // Exit just outside the right edge
        var exitObj = new GameObject("Exit");
        exitObj.transform.position = new Vector3(HalfWidth + 0.5f, 0f, 0f);
        var exitCol = exitObj.AddComponent<BoxCollider2D>();
        exitCol.size = new Vector2(1f, 20f);
        exitCol.isTrigger = true;
        exitObj.AddComponent<SceneExit>();

        // Player on the left, standing on the floor
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        float feetOffset = 0.5f;
        if (player.TryGetComponent(out CapsuleCollider2D capsule))
            feetOffset = (capsule.size.y * 0.5f - capsule.offset.y) * player.transform.lossyScale.y;
        player.transform.position = new Vector3(-HalfWidth + 1.5f, floorTop + feetOffset + 0.05f, 0f);

        // HealthBar still targets the Player
        HealthBar bar = Object.FindAnyObjectByType<HealthBar>(FindObjectsInactive.Include);
        if (bar && player.TryGetComponent(out Health health))
        {
            var so = new SerializedObject(bar);
            so.FindProperty("target").objectReferenceValue = health;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------------- OBSTACLES ----------------
    // Adds the obstacles to rooms 2 to 6. Each room gets an "Obstacles" object;
    // if a room already has one, that room is skipped so nothing gets doubled.

    const string ObstaclesName = "Obstacles";
    const string ProjectilePrefab = "Assets/Prefabs/Projectile.prefab";
    static readonly Color PunchDummyColor = new Color(0.55f, 0.35f, 0.2f);
    static readonly Color HookDummyColor = new Color(1f, 0.55f, 0.1f);
    static readonly Color ShooterColor = new Color(0.85f, 0.15f, 0.15f);

    // (menu removed: replaced by Tools > Rebuild Tutorial)
    static void BuildObstacles()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var built = new List<string>();
        var skipped = new List<string>();

        BuildRoom("Tutorial_2_Jump", JumpRoom, built, skipped);
        BuildRoom("Tutorial_3_Dash", DashRoom, built, skipped);
        BuildRoom("Tutorial_4_Punch", PunchRoom, built, skipped);
        BuildRoom("Tutorial_5_Hook", HookRoom, built, skipped);
        BuildRoom("Tutorial_6_DashThroughBullets", BulletRoom, built, skipped);

        EditorSceneManager.OpenScene(ScenePath("Tutorial_2_Jump"), OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Build Tutorial Obstacles",
            "Built: " + (built.Count > 0 ? string.Join(", ", built) : "none") +
            "\n\nSkipped (already had obstacles or missing): " + (skipped.Count > 0 ? string.Join(", ", skipped) : "none"), "OK");
    }

    static void BuildRoom(string room, System.Action<Tilemap, Transform> build, List<string> built, List<string> skipped)
    {
        string path = ScenePath(room);
        if (!File.Exists(path)) { skipped.Add(room); return; }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (GameObject.Find(ObstaclesName)) { skipped.Add(room); return; }

        var parent = new GameObject(ObstaclesName).transform;
        Tilemap tilemap = FindGroundTilemap();
        build(tilemap, parent);

        RebuildColliders(tilemap);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        built.Add(room);
    }

    // Room 2: a 5-tile wall (too high for a tap jump: hold Space), then 3 floating step platforms (up, up, down)
    static void JumpRoom(Tilemap map, Transform parent)
    {
        TileBase tile = FirstTile(map);
        FillTiles(map, tile, -4, -3, 1, JumpWallHeight, true);  // wall: x -4, on the floor
        FillTiles(map, tile, 0, -1, 2, 1, true);   // step 1
        FillTiles(map, tile, 3, 1, 2, 1, true);    // step 2 (higher)
        FillTiles(map, tile, 6, -1, 2, 1, true);   // step 3 (back down)
    }

    // Room 3: an 8-tile gap in the floor with the KillZone below it
    static void DashRoom(Tilemap map, Transform parent)
    {
        FillTiles(map, null, -3, -HalfHeightInt, 8, FloorRows, false); // remove floor from x -3 to 5

        KillZone kz = Object.FindAnyObjectByType<KillZone>(FindObjectsInactive.Include);
        if (!kz) kz = new GameObject("KillZone").AddComponent<KillZone>();
        kz.transform.position = new Vector3(1f, -HalfHeight - 1.5f, 0f);
        kz.transform.localScale = Vector3.one;
        if (!kz.TryGetComponent(out BoxCollider2D col)) col = kz.gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(12f, 1f);
        col.offset = Vector2.zero;
        col.isTrigger = true;
    }

    // Room 4: low ceiling over the right side, and a punch dummy (Health 5) blocking the corridor
    static void PunchRoom(Tilemap map, Transform parent)
    {
        TileBase tile = FirstTile(map);
        FillTiles(map, tile, 0, 0, 11, 5, true); // solid block from y 0 to the top of the screen: corridor is 3 tall

        GameObject dummy = MakeBox("PunchDummy", parent, map, new Vector2(4.5f, -1.55f), new Vector2(1f, 2.9f), PunchDummyColor);
        AddHealth(dummy, 8);
    }

    // Room 5: a high ledge with a hook dummy (Health 6) that can be pulled down
    static void HookRoom(Tilemap map, Transform parent)
    {
        TileBase tile = FirstTile(map);
        FillTiles(map, tile, 3, 2, 5, 1, true); // ledge top at y 3, 6 tiles above the floor

        GameObject dummy = MakeBox("HookDummy", parent, map, new Vector2(5.5f, 4f), new Vector2(1f, 2f), HookDummyColor);
        AddHealth(dummy, 20);
        var rb = dummy.AddComponent<Rigidbody2D>(); // Dynamic, so the hook can pull it
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
    }

    // Room 6: a shooter on a ledge, and the Player only has 5 health in this room
    static void BulletRoom(Tilemap map, Transform parent)
    {
        TileBase tile = FirstTile(map);
        GameObject shooter = MakeBox("ShooterDummy", parent, map, ShooterFloorPosition, new Vector2(1f, 2f), ShooterColor); // on the floor, same level as the player
        AddHealth(shooter, 8);
        var es = shooter.AddComponent<EnemyShooter>();
        var so = new SerializedObject(es);
        so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>(ProjectilePrefab);
        so.FindProperty("range").floatValue = 12f;
        so.FindProperty("fireRate").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        if (player && player.TryGetComponent(out Health health))
        {
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").intValue = 5; // 5 bullets = dead
            hso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------------- GUIDE TEXT ----------------
    // Puts a TextMeshPro text in the level (like a sign) telling the player what to do.
    // Rooms that already have a "GuideText" object are skipped.

    const string GuideTextName = "GuideText";
    static readonly Vector2 GuideTextPosition = new Vector2(-4f, 2.5f); // top left, away from the obstacles

    static readonly (string room, string text)[] GuideTexts =
    {
        ("Tutorial_1_Move", "A / D to move"),
        ("Tutorial_2_Jump", JumpTutorialText),
        ("Tutorial_3_Dash", "Jump, then SHIFT to dash"),
        ("Tutorial_4_Punch", "LEFT CLICK to punch"),
        ("Tutorial_5_Hook", "RIGHT CLICK to hook"),
        ("Tutorial_6_DashThroughBullets", "SHIFT to dash through bullets"),
    };

    const int JumpWallHeight = 5;
    const string JumpTutorialText = "SPACE to jump\nHold SPACE to jump higher";

    // Updates the existing Jump room: makes its first wall 5 tall and changes its guide text.
    // (menu removed: replaced by Tools > Rebuild Tutorial)
    static void UpdateJumpTutorial()
    {
        string path = ScenePath("Tutorial_2_Jump");
        if (!File.Exists(path)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Tilemap map = FindGroundTilemap();
        FillTiles(map, FirstTile(map), -4, -3, 1, JumpWallHeight, true);
        RebuildColliders(map);

        GameObject guide = GameObject.Find(GuideTextName);
        if (guide && guide.TryGetComponent(out TMPro.TextMeshPro text))
        {
            text.text = JumpTutorialText;
            guide.transform.position = new Vector3(-7f, 3.5f, 0f); // above the start, clear of the tall wall
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Update Jump Tutorial", "Tutorial_2_Jump now has a 5-tall wall and the 'hold to jump higher' text.", "OK");
    }

    // (menu removed: replaced by Tools > Rebuild Tutorial)
    static void BuildGuideText()
    {
        if (TMPro.TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("Build Tutorial Text",
                "TextMeshPro is not set up yet.\n\nGo to Window > TextMeshPro > Import TMP Essential Resources, click Import, then try again.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var built = new List<string>();
        var skipped = new List<string>();

        foreach (var (room, text) in GuideTexts)
        {
            string path = ScenePath(room);
            if (!File.Exists(path)) { skipped.Add(room); continue; }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (GameObject.Find(GuideTextName)) { skipped.Add(room); continue; }

            var go = new GameObject(GuideTextName);
            go.transform.position = GuideTextPosition;
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(9f, 2f);
            tmp.GetComponent<MeshRenderer>().sortingOrder = 5; // draw in front of tiles

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            built.Add(room);
        }

        EditorSceneManager.OpenScene(ScenePath(Rooms[0]), OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Build Tutorial Text",
            "Added text to: " + (built.Count > 0 ? string.Join(", ", built) : "none") +
            "\n\nSkipped (already had text or missing): " + (skipped.Count > 0 ? string.Join(", ", skipped) : "none"), "OK");
    }

    static readonly Vector2 ShooterFloorPosition = new Vector2(6.5f, -2f);

    // Makes the tilemap's collider match the painted tiles (it does not update by itself in the Editor).
    // Returns true if the Composite (merge) collider would not update and was removed,
    // so the plain Tilemap Collider 2D is used instead.
    static bool RebuildColliders(Tilemap tilemap)
    {
        tilemap.CompressBounds();
        if (!tilemap.TryGetComponent(out TilemapCollider2D tc)) return false;

        // Switch off and on to force a rebuild
        tc.enabled = false;
        tc.enabled = true;
        tc.ProcessTilemapChanges();

        if (!tilemap.TryGetComponent(out CompositeCollider2D composite)) return false;
        composite.enabled = false;
        composite.enabled = true;
        composite.GenerateGeometry();

        // Does the merged shape now cover the same area as the tiles?
        Bounds tiles = tilemap.localBounds;
        tiles.center = tilemap.transform.TransformPoint(tiles.center);
        Bounds shape = composite.bounds;
        bool matches = Mathf.Abs(shape.min.x - tiles.min.x) < 0.1f && Mathf.Abs(shape.max.x - tiles.max.x) < 0.1f &&
                       Mathf.Abs(shape.min.y - tiles.min.y) < 0.1f && Mathf.Abs(shape.max.y - tiles.max.y) < 0.1f;
        if (matches) return false;

        // Still the old shape: stop merging and remove the Composite, so the tiles' own colliders are used
        tc.compositeOperation = Collider2D.CompositeOperation.None;
        Object.DestroyImmediate(composite);
        return true;
    }

    // ---------------- FIX ----------------
    // Rebuilds the colliders in every room, and moves the Room 6 shooter down to the floor (removing its ledge).
    // (menu removed: replaced by Tools > Rebuild Tutorial)
    static void FixRooms()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var names = new List<string> { TemplateName };
        names.AddRange(Rooms);
        var fixedRooms = new List<string>();

        foreach (string name in names)
        {
            string path = ScenePath(name);
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Tilemap tilemap = FindGroundTilemap();

            if (name == "Tutorial_6_DashThroughBullets")
            {
                FillTiles(tilemap, null, 4, 0, 5, 1, false); // remove the old ledge
                GameObject shooter = GameObject.Find("ShooterDummy");
                if (shooter) shooter.transform.position = ShooterFloorPosition;
            }

            bool usedFallback = RebuildColliders(tilemap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            fixedRooms.Add(usedFallback ? name + " (merge removed)" : name);
        }

        EditorSceneManager.OpenScene(ScenePath(Rooms[0]), OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Fix Tutorial Rooms", "Fixed:\n" + string.Join("\n", fixedRooms), "OK");
    }

    const int HalfHeightInt = 5;

    // Paints (or erases, when paint is false) a block of tiles. x, y = bottom-left in world units.
    static void FillTiles(Tilemap map, TileBase tile, int x, int y, int width, int height, bool paint)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                map.SetTile(map.WorldToCell(new Vector3(x + i + 0.5f, y + j + 0.5f)), paint ? tile : null);
    }

    // A plain coloured box with a solid collider. Swap the sprite later for real art.
    static GameObject MakeBox(string name, Transform parent, Tilemap map, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (FirstTile(map) as Tile)?.sprite ?? AssetDatabase.LoadAssetAtPath<Tile>(FallbackTile)?.sprite;
        sr.color = color;
        sr.sortingOrder = 1;

        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    static void AddHealth(GameObject go, int maxHealth)
    {
        var health = go.AddComponent<Health>();
        var so = new SerializedObject(health);
        so.FindProperty("maxHealth").intValue = maxHealth;
        so.FindProperty("invincibleTime").floatValue = 0.1f; // same as the old Enemy, so fast punches count
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- LEVEL 1: THE SHIP ----------------
    // One tall scene, one screen wide. 4 floors + the top deck (boss arena, empty for now).
    // Each floor is exactly one screen tall, and the camera locks to the floor you are on.
    // Between two floors is a 2-tile divider (ceiling of the lower floor + floor of the upper one).
    // A hole in each divider, with a hook ring in it, takes you up. Holes switch sides: right, left, right, left.
    // A checkpoint is next to where you land.

    const string Level1Name = "Level_1";
    const float L1HalfHeight = 7.5f;          // camera zoomed out 1.5x compared to the tutorial
    static float L1HalfWidth => L1HalfHeight * 16f / 9f; // about 13.3
    const float L1FirstFloorCenterY = 0.5f;   // so floors line up with whole tiles
    const int WallLeft = -14, WallRight = 66;  // wall tile columns (inside is x -13 to 66, 3 screens wide)
    const int FloorSpacing = 15;               // one screen per floor
    const int Floors = 4;                   // floors 0..3. The ring in the top ceiling leads to the deck scene.
    const string DeckSceneName = "Level_1_Deck";
    const string SailSceneName = "Level_1_Sail";
    const string NextLevelName = "Level_2";
    const int DeckLeft = -14, DeckRight = 66; // deck railing columns: 3 screens long (inside x -13 to 66)
    const int FloorInside = 13;             // open space from floor to ceiling
    static readonly Color RingColor = new Color(1f, 0.85f, 0.1f);

    // Hazard damage in Level 1 (the player has 100 health). Change these and rebuild, or edit each object.
    const int TendrilDamage = 1;
    const int BarrelDamage = 20;
    const int CannonTouchDamage = 5;
    const int CannonBallDamage = 8;
    const string CannonBallPrefab = "Assets/Prefabs/CannonBall.prefab";
    static readonly Color CrateColor = new Color(0.6f, 0.4f, 0.2f);
    static readonly Color CannonColor = new Color(0.25f, 0.25f, 0.25f);
    static readonly Color CheckpointColor = new Color(0.9f, 0.9f, 0.1f);
    static readonly Color BoatColor = new Color(0.45f, 0.28f, 0.12f);

    // Top of the floor you stand on, on floor k (0 = bottom floor, 4 = deck)
    static int FloorTop(int k) => -6 + k * FloorSpacing;
    // Left x of the 3-wide hole that leads up onto floor k (k = 1..4)
    static int HoleX(int k) => k % 2 == 1 ? 62 : -12;

    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 1 (Ship)")]
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

    // ---------------- LEVEL 1 DECK (boss arena, no boss yet) ----------------
    // Big flat open-air deck, 3 screens long. The camera (same zoom as Level_1) follows the player left and right.
    // Low railings and invisible edges, nothing else on it (room for the boss later).
    // You start next to the hatch you climbed out of. The screen fades in from black (ScreenFade).

    // Deck camera: a bit wider than Level_1, follows the player left/right only, never shows past the railings
    const float DeckCameraSize = 9f;
    static void SetDeckCamera(Transform player)
    {
        Camera cam = Object.FindAnyObjectByType<Camera>();
        cam.orthographicSize = DeckCameraSize;
        float deckBottom = L1FirstFloorCenterY - L1HalfHeight;
        cam.transform.position = new Vector3(DeckLeft + 0.67f + DeckCameraSize * 16f / 9f, deckBottom + DeckCameraSize, cam.transform.position.z);
        if (!cam.TryGetComponent(out CameraFollow2D follow)) follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.enabled = true;
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player;
        so.FindProperty("useBounds").boolValue = true;
        so.FindProperty("minBounds").vector2Value = new Vector2(DeckLeft + 0.67f, L1FirstFloorCenterY - L1HalfHeight);
        so.FindProperty("maxBounds").vector2Value = new Vector2(DeckRight + 0.33f, deckBottom + DeckCameraSize * 2f); // one screen tall: never moves up/down
        so.FindProperty("lockToFloors").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Changes only the camera in the existing Level_1_Deck (keeps Hardy and everything else)
    // (menu removed: one-time fix, already done)
    static void UpdateDeckCamera()
    {
        string path = ScenePath(DeckSceneName);
        if (!File.Exists(path)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        SetDeckCamera(player.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Update Deck Camera", "The deck camera now follows the player left and right (same zoom as Level 1).", "OK");
    }

    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 1 Deck")]
    static void BuildLevel1Deck()
    {
        string path = ScenePath(DeckSceneName);
        if (File.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("Build Level 1 Deck",
                    DeckSceneName + " already exists.\n\nReplace it with a fresh build? Anything you changed in it by hand will be lost.",
                    "Replace", "Cancel"))
                return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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

        int left = DeckLeft, right = DeckRight; // railing columns
        int deckRow = -7;                 // deck is 2 rows: -7 and -6, top at -5
        int deckTop = deckRow + 2;
        FillTiles(map, tile, left, deckRow, right - left + 1, 2, true);
        FillTiles(map, tile, left, deckTop, 1, 2, true);   // left railing
        FillTiles(map, tile, right, deckTop, 1, 2, true);  // right railing
        RebuildColliders(map);

        var structure = new GameObject("Structure").transform;

        // Invisible edges so you cannot fall off the ship
        foreach (float x in new[] { left + 0.5f, right + 0.5f })
        {
            var edge = new GameObject("DeckEdge");
            edge.transform.SetParent(structure);
            edge.transform.position = new Vector3(x, 15f, 0f);
            edge.AddComponent<BoxCollider2D>().size = new Vector2(1f, 50f);
        }

        // Hatch you climbed out of (just a mark on the deck, no collider)
        GameObject hatch = MakeBox("Hatch", structure, map, new Vector2(-10.5f, deckTop + 0.1f), new Vector2(3f, 0.2f), new Color(0.15f, 0.1f, 0.05f));
        Object.DestroyImmediate(hatch.GetComponent<BoxCollider2D>());

        new GameObject("BossArena (empty)").transform.position = new Vector3((left + right) * 0.5f, deckTop + 3f, 0f);

        // Player starts on the hatch
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        float feet = 1f;
        if (player.TryGetComponent(out CapsuleCollider2D capsule))
            feet = (capsule.size.y * 0.5f - capsule.offset.y) * player.transform.lossyScale.y;
        player.transform.position = new Vector3(-10.5f, deckTop + feet + 0.05f, 0f);

        SetDeckCamera(player.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorKit.AddToSceneList(path);

        EditorUtility.DisplayDialog("Build Level 1 Deck", "Done! Level_1_Deck is built and open, and in the Scene List.", "OK");
    }

    // ---------------- HARDY (Level 1 boss) ----------------
    // Puts Hardy and his health bar (top middle of the screen) into Level_1_Deck.
    // All of Hardy's attack values are on the HardyBoss component in the Inspector.

    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 1 Deck: Add Hardy")]
    static void AddHardy()
    {
        string path = ScenePath(DeckSceneName);
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("Add Hardy", "Build Level 1 Deck first (Tools > Rebuild Levels > Level 1 Deck).", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        if (GameObject.Find("Hardy") || GameObject.Find("BossBar"))
        {
            if (!EditorUtility.DisplayDialog("Add Hardy", "Hardy is already in the deck.\n\nReplace him and his health bar with fresh ones? Values you changed on him will be lost.", "Replace", "Cancel"))
                return;
            DestroyByName("Hardy");
            DestroyByName("BossBar");
        }

        Tilemap map = FindGroundTilemap();
        const float deckTop = -5f;

        // Hardy: dark box 3 wide, 4 tall, on the right side of the deck
        GameObject hardy = MakeBox("Hardy", null, map, new Vector2(6f, deckTop + 2f), new Vector2(3f, 4f), new Color(0.1f, 0.08f, 0.15f));
        var rb = hardy.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        AddHealth(hardy, 400);
        var hardyHealth = new SerializedObject(hardy.GetComponent<Health>());
        hardyHealth.FindProperty("flashOnHit").boolValue = false; // no hit flash on Hardy
        hardyHealth.ApplyModifiedPropertiesWithoutUndo();
        // No ContactDamage: touching Hardy does not hurt (see Touch Hurts on HardyBoss)
        var boss = hardy.AddComponent<HardyBoss>();

        // Boss health bar, top middle of the screen
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var barRoot = new GameObject("BossBar", typeof(RectTransform), typeof(Image));
        barRoot.transform.SetParent(canvas.transform, false);
        var rootRt = (RectTransform)barRoot.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -40f);
        rootRt.sizeDelta = new Vector2(700f, 28f);
        var bg = barRoot.GetComponent<Image>();
        bg.sprite = uiSprite;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(barRoot.transform, false);
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(4f, 4f);
        fillRt.offsetMax = new Vector2(-4f, -4f);
        var fill = fillGo.GetComponent<Image>();
        fill.sprite = uiSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.color = new Color(0.55f, 0.1f, 0.75f);

        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(barRoot.transform, false);
        var nameRt = (RectTransform)nameGo.transform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 0f);
        nameRt.anchoredPosition = new Vector2(0f, 4f);
        nameRt.sizeDelta = new Vector2(0f, 30f);
        var nameText = nameGo.AddComponent<TMPro.TextMeshProUGUI>();
        nameText.text = "HARDY";
        nameText.fontSize = 24;
        nameText.alignment = TMPro.TextAlignmentOptions.Center;
        nameText.color = Color.white;

        var bar = barRoot.AddComponent<HealthBar>();
        var barSo = new SerializedObject(bar);
        barSo.FindProperty("target").objectReferenceValue = hardy.GetComponent<Health>();
        barSo.FindProperty("fill").objectReferenceValue = fill;
        barSo.ApplyModifiedPropertiesWithoutUndo();

        // Boss settings that depend on this deck
        var bossSo = new SerializedObject(boss);
        bossSo.FindProperty("groundY").floatValue = deckTop;
        bossSo.FindProperty("arenaMinX").floatValue = DeckLeft + 1;
        bossSo.FindProperty("arenaMaxX").floatValue = DeckRight;
        bossSo.FindProperty("healthBarRoot").objectReferenceValue = barRoot;
        bossSo.FindProperty("sceneAfterDeath").stringValue = SailSceneName;
        bossSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = hardy;
        EditorUtility.DisplayDialog("Add Hardy", "Hardy is on the deck!\n\nClick Hardy and look at the HardyBoss component to change any attack value.", "OK");
    }

    // A secret room outside the ship (x -50 to -24), at the same height as floor k, one screen big.
    // The main camera never goes there. The entrance door (at doorPos, in the level) brings you in and the
    // camera switches to the room; the exit door inside brings you back. An empty chest sits in the middle.
    // ---------------- Secret room obstacles ----------------
    // Like the Level 2 secret rooms: each room has an obstacle guarding the chest, and a health potion as a reward.
    // Hardy-themed (goo tendrils, barrels, cannons, crates), no poison. You come in on the right; the chest is in the middle.

    // (menu removed: one-time, already done)
    static void AddSecretRoomObstaclesToLevel1()
    {
        string path = ScenePath(Level1Name);
        if (!File.Exists(path)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Tilemap map = FindGroundTilemap();

        int done = 0;
        for (int k = 0; k < Floors; k++)
        {
            // The real room lives under "SecretRooms" (the decoration has groups with the same name)
            GameObject secrets = GameObject.Find("SecretRooms");
            Transform room = secrets ? secrets.transform.Find("SecretRoom_F" + (k + 1)) : null;
            if (!room) continue;
            AddSecretRoomObstacles(k, room, map);
            done++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Secret Room Obstacles", "Added obstacles and a health potion to " + done + " secret rooms.\n\nRun it again any time: it replaces the old ones.", "OK");
    }

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

    // ---------------- "Press E" pop-ups ----------------
    // Adds a "Press E" text above every SecretDoor and Chest in the open scene that does not have one yet.

    // (menu removed: one-time fix, already done)
    static void AddPromptsToScene()
    {
        int added = 0;
        foreach (SecretDoor door in Object.FindObjectsByType<SecretDoor>(FindObjectsInactive.Include))
            if (AddPrompt(door.gameObject, door)) added++;
        foreach (Chest chest in Object.FindObjectsByType<Chest>(FindObjectsInactive.Include))
            if (AddPrompt(chest.gameObject, chest)) added++;

        if (added > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Add Press E Pop-ups", "Added " + added + " pop-ups. Press Ctrl + S to save the scene.", "OK");
    }

    // Puts a "Press E" text just above the object and links it to the component's Prompt slot.
    // The text is a sibling (not a child) so it does not get stretched by the object's scale.
    static bool AddPrompt(GameObject target, Component owner)
    {
        var so = new SerializedObject(owner);
        var promptProp = so.FindProperty("prompt");
        if (promptProp == null || promptProp.objectReferenceValue) return false;

        var go = new GameObject(target.name + "_PressE");
        go.transform.SetParent(target.transform.parent, false);
        var bounds = target.GetComponent<SpriteRenderer>() ? target.GetComponent<SpriteRenderer>().bounds : new Bounds(target.transform.position, Vector3.one);
        go.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.6f, 0f);
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
        return true;
    }

    // ---------------- LEVEL 1 SAIL (after Hardy) ----------------
    // The player stands on the boat on the sea. A / D steer the boat. Sailing off the right edge
    // fades to black and loads Level_2 (stays black until Level_2 exists).
    // Also sets Hardy (if he is in the deck) to fade to this scene when he dies.

    [MenuItem("Tools/Rebuild Levels (wipes changes)/Level 1 Sail")]
    static void BuildLevel1Sail()
    {
        string path = ScenePath(SailSceneName);
        if (File.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("Build Level 1 Sail",
                    SailSceneName + " already exists.\n\nReplace it with a fresh build? Anything you changed in it by hand will be lost.",
                    "Replace", "Cancel"))
                return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
        DestroyByName("KillZone");

        Tilemap map = FindGroundTilemap();
        map.ClearAllTiles();
        RebuildColliders(map);

        // Fixed camera, same zoom as Level_1
        Camera cam = Object.FindAnyObjectByType<Camera>();
        cam.transform.position = new Vector3(0f, L1FirstFloorCenterY, cam.transform.position.z);
        cam.orthographicSize = L1HalfHeight;
        if (cam.TryGetComponent(out CameraFollow2D follow)) follow.enabled = false;

        // Sea along the bottom of the screen (wider than the screen so the boat can sail out)
        float seaTop = L1FirstFloorCenterY - L1HalfHeight + 2f;
        GameObject sea = MakeBox("Sea", null, map, new Vector2(0f, seaTop - 1.5f), new Vector2(L1HalfWidth * 4f, 3f), new Color(0.1f, 0.3f, 0.6f));
        Object.DestroyImmediate(sea.GetComponent<BoxCollider2D>());
        sea.GetComponent<SpriteRenderer>().sortingOrder = 4; // in front of the boat's bottom, like it sits in the water

        // Boat with the player on it
        GameObject boat = MakeBox("Boat", null, map, new Vector2(-9f, seaTop), new Vector2(4f, 1f), new Color(0.45f, 0.28f, 0.12f));
        Object.DestroyImmediate(boat.GetComponent<BoxCollider2D>());
        var drive = new SerializedObject(boat.AddComponent<BoatDrive>());
        drive.FindProperty("leftLimit").floatValue = -L1HalfWidth + 2.5f;
        drive.FindProperty("nextScene").stringValue = NextLevelName;
        drive.ApplyModifiedPropertiesWithoutUndo();

        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        if (player) player.transform.position = boat.transform.position + new Vector3(0f, 1.5f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorKit.AddToSceneList(path);

        // Point Hardy at this scene, if he is already in the deck
        string deckPath = ScenePath(DeckSceneName);
        bool hardyLinked = false;
        if (File.Exists(deckPath))
        {
            Scene deck = EditorSceneManager.OpenScene(deckPath, OpenSceneMode.Single);
            HardyBoss boss = Object.FindAnyObjectByType<HardyBoss>();
            if (boss)
            {
                var so = new SerializedObject(boss);
                so.FindProperty("sceneAfterDeath").stringValue = SailSceneName;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(deck);
                EditorSceneManager.SaveScene(deck);
                hardyLinked = true;
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog("Build Level 1 Sail",
            "Done! Level_1_Sail is built and in the Scene List.\n\n" +
            (hardyLinked ? "Hardy now fades to it when he dies." : "Hardy is not in the deck yet: run Tools > Rebuild Levels > Level 1 Deck: Add Hardy (it links automatically)."), "OK");
    }

    // A pile of big cargo crates stacked on top of each other (too tall to jump over).
    // Each crate breaks after 2 punches; the crates above then drop down (they can only fall straight down).
    static void MakeCrateStack(string name, Transform parent, Tilemap map, float x, float floorTop, int count)
    {
        var stack = new GameObject(name).transform;
        stack.SetParent(parent);
        for (int i = 0; i < count; i++)
        {
            GameObject crate = MakeBox("Crate_" + (i + 1), stack, map, new Vector2(x, floorTop + 1f + i * 2f), new Vector2(1.98f, 1.98f), CrateColor);
            AddHealth(crate, 8);
            var rb = crate.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.mass = 20f;
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }
    }

    // A Venom-style goo tendril. base = where it comes out of (wall/ceiling/floor), it shoots out in dir on a timer.
    static void MakeTendril(string name, Transform parent, Tilemap map, Vector2 basePos, Vector2 dir, float length, float startDelay)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.right = dir;
        go.transform.position = basePos;
        go.transform.localScale = new Vector3(length, 0.6f, 1f); // only for seeing it in the Scene view; it hides itself in Play
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (FirstTile(map) as Tile)?.sprite ?? AssetDatabase.LoadAssetAtPath<Tile>(FallbackTile)?.sprite;
        sr.color = new Color(0.08f, 0.05f, 0.12f);
        sr.sortingOrder = 2;
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        var tendril = go.AddComponent<GooTendril>();
        var so = new SerializedObject(tendril);
        so.FindProperty("direction").vector2Value = dir;
        so.FindProperty("length").floatValue = length;
        so.FindProperty("startDelay").floatValue = startDelay;
        so.FindProperty("damage").intValue = TendrilDamage;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // A trigger that sends the player back to their last checkpoint
    static void MakeKillZone(string name, Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
        go.AddComponent<KillZone>();
    }

    // A red barrel that explodes a moment after the player touches it
    static void MakeBarrel(string name, Transform parent, Tilemap map, Vector2 pos)
    {
        GameObject barrel = MakeBox(name, parent, map, pos, new Vector2(1f, 1.2f), new Color(0.85f, 0.1f, 0.1f));
        var so = new SerializedObject(barrel.AddComponent<ExplodingBarrel>());
        so.FindProperty("damage").intValue = BarrelDamage;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // A cannon that stands still and shoots at the player. Cannot be destroyed, hurts on touch.
    static void MakeCannon(string name, Transform parent, Tilemap map, Vector2 pos)
    {
        GameObject cannon = MakeBox(name, parent, map, pos, new Vector2(1f, 1f), CannonColor);
        var es = cannon.AddComponent<EnemyShooter>();
        var so = new SerializedObject(es);
        so.FindProperty("projectilePrefab").objectReferenceValue = GetCannonBall();
        so.FindProperty("range").floatValue = 14f; // floors are bigger now
        so.FindProperty("fireRate").floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
        var touch = new SerializedObject(cannon.AddComponent<ContactDamage>()); // touching a cannon hurts
        touch.FindProperty("damage").intValue = CannonTouchDamage;
        touch.ApplyModifiedPropertiesWithoutUndo();
    }

    // Level 1 cannon ball: a copy of the tutorial Projectile with more damage
    // (the tutorial keeps its 1-damage bullets, because Room 6 gives the player only 5 health)
    static Projectile GetCannonBall()
    {
        if (!File.Exists(CannonBallPrefab)) AssetDatabase.CopyAsset(ProjectilePrefab, CannonBallPrefab);
        var ball = AssetDatabase.LoadAssetAtPath<Projectile>(CannonBallPrefab);
        var so = new SerializedObject(ball);
        so.FindProperty("damage").intValue = CannonBallDamage;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return ball;
    }

    // ---------------- BOAT (Tutorial_7_Dock) ----------------
    // The dock ends, a boat waits at the end. Step on it: it sails off screen, fades to black, loads Level_1.

    // (menu removed: replaced by Tools > Rebuild Tutorial)
    static void BuildBoat()
    {
        string path = ScenePath("Tutorial_7_Dock");
        if (!File.Exists(path)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (GameObject.Find("Boat"))
        {
            EditorUtility.DisplayDialog("Build Boat Scene", "Tutorial_7_Dock already has a Boat, so nothing was changed.", "OK");
            return;
        }

        Tilemap map = FindGroundTilemap();
        FillTiles(map, null, 3, -5, 9, 2, false); // dock ends at x 3, water after
        RebuildColliders(map);

        // Boat right after the dock, top at floor level so you can walk onto it
        GameObject boat = MakeBox("Boat", null, map, new Vector2(5f, -3.5f), new Vector2(4f, 1f), BoatColor);
        boat.layer = LayerMask.NameToLayer("Ground"); // so the player counts as standing on it
        var board = boat.AddComponent<BoxCollider2D>(); // trigger on top: stepping on starts the sail
        board.isTrigger = true;
        board.size = new Vector2(0.8f, 1f);
        board.offset = new Vector2(0f, 1f);

        // Black fade image over the whole screen, invisible at start
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeGo.transform.SetParent(canvas.transform, false);
        fadeGo.transform.SetAsLastSibling();
        var rt = (RectTransform)fadeGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = fadeGo.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;

        var exit = boat.AddComponent<BoatExit>();
        var so = new SerializedObject(exit);
        so.FindProperty("nextScene").stringValue = Level1Name;
        so.FindProperty("fadeImage").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Build Boat Scene", "Done! Tutorial_7_Dock now has a boat that sails to Level_1.", "OK");
    }

    static string ScenePath(string name) => $"{ScenesFolder}/{name}.unity";

    static void DestroyByName(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t && t.name == name) Object.DestroyImmediate(t.gameObject);
    }

    static Tilemap FindGroundTilemap()
    {
        int ground = LayerMask.NameToLayer("Ground");
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        foreach (Tilemap map in maps)
            if (map.gameObject.layer == ground) return map;
        return maps[0];
    }

    static TileBase FirstTile(Tilemap tilemap)
    {
        foreach (TileBase t in tilemap.GetTilesBlock(tilemap.cellBounds))
            if (t) return t;
        return null;
    }
}
