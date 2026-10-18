using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Part of TutorialRoomBuilder (see TutorialRoomBuilder.cs for its settings and the main loop).
// Level 1 Sail: the boat scene and the props built for it.
public static partial class TutorialRoomBuilder
{
    // ---------------- LEVEL 1 SAIL (after Hardy) ----------------
    // The player stands on the boat on the sea. A / D steer the boat. Sailing off the right edge
    // fades to black and loads Level_2 (stays black until Level_2 exists).
    // Also sets Hardy (if he is in the deck) to fade to this scene when he dies.

    [MenuItem("Tools/Other/Rebuild Levels (wipes changes)/Level 1 Sail", priority = 3000)]
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
}
