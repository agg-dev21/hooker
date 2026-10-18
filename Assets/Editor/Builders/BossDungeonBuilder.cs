using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Editor-only. Adds the menu item Tools > Rebuild Levels > Level 2 Boss (Dungeon).
// Builds Level_2_Boss: a dark stone dungeon under the poison tower, home of the Plague Warden (PoisonBoss).
// The boss door in Level_2 leads here. Makes the scene from Room_Template and adds it to the Scene List.
//
// Layout (inside x 0..35, floor top at y 0, ceiling at y 20):
//   - Player starts on the left, by the barred entrance gate.
//   - Two thin stone ledges (the boss charges underneath) and a high middle platform to dodge the slam.
//   - Two hook rings near the ceiling to escape upward.
//   - Torches on the walls (flickering lights), chains, pillars and brick walls in the background.
//   - Exit gate on the right: it opens when the boss dies. The door behind it leads to Level_3.
public static class BossDungeonBuilder
{
    const string SceneName = "Level_2_Boss";
    const string NextScene = "Level_3";
    const string TemplateScene = "Assets/Scenes/Room_Template.unity";
    const string ScenesFolder = "Assets/Scenes";
    const string FallbackTile = "Assets/Tiles/Square.asset";
    const int BossHealth = 300;
    const float CameraSize = 10f; // the whole arena on screen

    static readonly Color StoneColor = new Color(0.42f, 0.42f, 0.48f);
    static readonly Color BackWallColor = new Color(0.11f, 0.1f, 0.13f);
    static readonly Color MortarColor = new Color(0.16f, 0.15f, 0.19f);
    static readonly Color PillarColor = new Color(0.17f, 0.16f, 0.2f);
    static readonly Color IronColor = new Color(0.25f, 0.25f, 0.28f);
    static readonly Color FlameColor = new Color(1f, 0.6f, 0.15f);
    static readonly Color TorchLightColor = new Color(1f, 0.6f, 0.3f);
    static readonly Color RingColor = new Color(1f, 0.85f, 0.1f);
    static readonly Color DoorColor = new Color(0.45f, 0.25f, 0.08f);
    static readonly Color BossColor = new Color(0.25f, 0.65f, 0.15f);

    static Tilemap map;
    static TileBase tile;
    static Sprite boxSprite;
    static int groundLayer;

    [MenuItem("Tools/Other/Rebuild Levels (wipes changes)/Level 2 Boss (Dungeon)", priority = 3000)]
    static void Build()
    {
        string path = $"{ScenesFolder}/{SceneName}.unity";
        if (File.Exists(path) && !EditorUtility.DisplayDialog("Build Boss Dungeon",
                SceneName + " already exists.\n\nReplace it with a fresh build? Anything you changed in it by hand will be lost.",
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
            Debug.LogError("Build Boss Dungeon: could not copy " + TemplateScene);
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
        map.color = StoneColor; // grey stone instead of the usual red

        var root = new GameObject("Dungeon").transform;
        var decor = Group("Decor", root);
        var props = Group("Props", root);

        BuildStone();
        BuildBackground(decor);
        BuildTorches(decor);
        DimLights(scene);
        GameObject[] exit = BuildExit(props);
        BuildArena(props);
        BuildEntrance(decor);
        AddBoss(root, exit);

        RebuildColliders(map);
        PlacePlayerAndCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorKit.AddToSceneList(path);
        EditorUtility.DisplayDialog("Build Boss Dungeon", "Done! " + SceneName + " is built, open, and in the Scene List.\n\n" +
            "Beat Oscar to open the exit gate. The door behind it loads '" + NextScene + "'.", "OK");
    }

    // ---------------- Stone ----------------

    static void BuildStone()
    {
        Fill(-3, -3, 46, 3);  // floor (top at y 0), also under the exit corridor
        Fill(-3, 20, 46, 3);  // ceiling
        Fill(-3, 0, 3, 20);   // left wall
        Fill(36, 0, 7, 20);   // right wall (thick: the exit corridor is carved into it)
        Fill(36, 0, 6, 4, false); // exit corridor x 36-41, 4 tall
    }

    // Ledges, middle platform and hook rings
    static void BuildArena(Transform props)
    {
        // Thin one-way ledges (top at y 3.5): the boss is lower than them, so his charge goes underneath
        OneWay("Ledge_Left", props, new Vector2(6f, 3.25f), 4f);
        OneWay("Ledge_Right", props, new Vector2(30f, 3.25f), 4f);
        Fill(15, 5, 6, 1);   // middle platform, top at y 6 (jump from a ledge, or dash)
        // Hook rings above the middle platform: hook one to escape a charge and drop onto the platform
        Ring("Ring_Left", props, new Vector2(16f, 12f), 6f);
        Ring("Ring_Right", props, new Vector2(20f, 12f), 6f);
        // Crumbled stone in the corners
        Fill(0, 0, 1, 1);
        Fill(35, 0, 1, 1);
    }

    // Exit gate (opens when the boss dies) and the door behind it
    static GameObject[] BuildExit(Transform props)
    {
        GameObject gate = Box("ExitGate", props, new Vector2(36.5f, 2f), new Vector2(1f, 4f), IronColor);
        gate.layer = groundLayer;
        for (int i = 0; i < 3; i++) // bars
        {
            GameObject bar = Box("Bar", gate.transform, new Vector2(36.2f + i * 0.3f, 2f), new Vector2(0.08f, 4f), Color.black);
            Object.DestroyImmediate(bar.GetComponent<BoxCollider2D>());
            bar.GetComponent<SpriteRenderer>().sortingOrder = 2;
        }

        GameObject door = Box("ExitDoor", props, new Vector2(40.5f, 1.1f), new Vector2(1.2f, 2.2f), DoorColor);
        door.GetComponent<BoxCollider2D>().isTrigger = true;
        var so = new SerializedObject(door.AddComponent<BossDoor>());
        so.FindProperty("bossScene").stringValue = NextScene;
        so.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(door, door.GetComponent<BossDoor>());
        door.SetActive(false); // appears when the boss dies
        return new[] { gate, door };
    }

    // The barred gate you came in through (just decoration, the way back is shut)
    static void BuildEntrance(Transform decor)
    {
        Fill(-1, 0, 1, 4, false);
        Fill(-3, 0, 2, 4, true);
        GameObject gate = Box("EntranceGate", decor, new Vector2(-0.5f, 2f), new Vector2(1f, 4f), IronColor);
        gate.layer = groundLayer;
        for (int i = 0; i < 3; i++)
        {
            GameObject bar = Box("Bar", gate.transform, new Vector2(-0.8f + i * 0.3f, 2f), new Vector2(0.08f, 4f), Color.black);
            Object.DestroyImmediate(bar.GetComponent<BoxCollider2D>());
            bar.GetComponent<SpriteRenderer>().sortingOrder = 2;
        }
    }

    // ---------------- Look ----------------

    static void BuildBackground(Transform decor)
    {
        Deco("BackWall", decor, new Vector2(19.5f, 10f), new Vector2(50f, 30f), BackWallColor, -20);

        // Brick pattern: mortar lines every 2 units, joints staggered every other row
        for (int row = 0; row < 10; row++)
        {
            float y = row * 2f;
            Deco("Mortar", decor, new Vector2(18f, y), new Vector2(36f, 0.1f), MortarColor, -19);
            for (float x = row % 2 == 0 ? 0f : 1.5f; x < 36f; x += 3f)
                Deco("Joint", decor, new Vector2(x, y + 1f), new Vector2(0.1f, 2f), MortarColor, -19);
        }

        // Pillars with caps
        foreach (float x in new[] { 9f, 27f })
        {
            Deco("Pillar", decor, new Vector2(x, 10f), new Vector2(2f, 20f), PillarColor, -18);
            Deco("PillarCap", decor, new Vector2(x, 19.4f), new Vector2(2.8f, 0.8f), PillarColor, -18);
            Deco("PillarBase", decor, new Vector2(x, 0.4f), new Vector2(2.8f, 0.8f), PillarColor, -18);
        }

        // Hanging chains with a ring at the end
        Vector2[] chains = { new Vector2(6f, 4f), new Vector2(13f, 6f), new Vector2(22.5f, 5f), new Vector2(30f, 3.5f) }; // x, length
        foreach (Vector2 c in chains)
        {
            Deco("Chain", decor, new Vector2(c.x, 20f - c.y * 0.5f), new Vector2(0.12f, c.y), IronColor, -5);
            Deco("Shackle", decor, new Vector2(c.x, 20f - c.y), new Vector2(0.5f, 0.5f), IronColor, -5);
        }

        // Barred windows high on the wall
        foreach (float x in new[] { 4f, 18f, 32f })
        {
            Deco("Window", decor, new Vector2(x, 16.5f), new Vector2(2f, 2.5f), new Color(0.05f, 0.06f, 0.1f), -17);
            for (int i = 0; i < 3; i++)
                Deco("WindowBar", decor, new Vector2(x - 0.6f + i * 0.6f, 16.5f), new Vector2(0.1f, 2.5f), IronColor, -16);
        }

        // Slime oozing down the walls (poison dungeon)
        Vector3[] oozes = { new Vector3(0.1f, 14f, 5f), new Vector3(35.9f, 12f, 6f), new Vector3(15.5f, 19.5f, 2f), new Vector3(21f, 19.5f, 3f) }; // x, top, length
        foreach (Vector3 o in oozes)
            Deco("Ooze", decor, new Vector2(o.x, o.y - o.z * 0.5f), new Vector2(0.25f, o.z), new Color(0.3f, 0.8f, 0.15f, 0.8f), -4);
    }

    static void BuildTorches(Transform decor)
    {
        Vector2[] spots = { new Vector2(0.4f, 8f), new Vector2(35.6f, 8f), new Vector2(9f, 13f), new Vector2(27f, 13f) };
        foreach (Vector2 p in spots)
        {
            Deco("TorchBracket", decor, p, new Vector2(0.3f, 0.8f), IronColor, -3);
            GameObject flame = Deco("TorchFlame", decor, p + new Vector2(0f, 0.65f), new Vector2(0.35f, 0.6f), FlameColor, -2);
            var light = new GameObject("TorchLight").AddComponent<Light2D>();
            light.transform.SetParent(flame.transform, false);
            light.lightType = Light2D.LightType.Point;
            light.color = TorchLightColor;
            light.intensity = 1.3f;
            light.pointLightOuterRadius = 7f;
            light.pointLightInnerRadius = 1f;
            var flicker = new SerializedObject(flame.AddComponent<TorchFlicker>());
            flicker.FindProperty("torchLight").objectReferenceValue = light;
            flicker.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Darken the scene's global light so the torches stand out
    static void DimLights(Scene scene)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Light2D l in r.GetComponentsInChildren<Light2D>(true))
                if (l.lightType == Light2D.LightType.Global)
                {
                    l.intensity = 0.4f;
                    l.color = new Color(0.65f, 0.7f, 0.85f);
                }
    }

    // ---------------- Boss ----------------

    static GameObject AddBoss(Transform parent, GameObject[] exit)
    {
        Vector2 size = new Vector2(4f, 2.8f); // lower than the side ledges
        GameObject boss = Box("PlagueWarden", parent, new Vector2(27f, size.y * 0.5f), size, BossColor);
        boss.GetComponent<SpriteRenderer>().sortingOrder = 5;

        // Eyes, so it reads as a creature
        foreach (float ex in new[] { -0.2f, 0.2f })
        {
            GameObject eye = Deco("Eye", boss.transform, Vector2.zero, Vector2.one, new Color(1f, 0.95f, 0.3f), 6);
            eye.transform.localPosition = new Vector3(ex, 0.18f, 0f);
            eye.transform.localScale = new Vector3(0.12f, 0.15f, 1f);
        }

        var hso = new SerializedObject(boss.AddComponent<Health>());
        hso.FindProperty("maxHealth").intValue = BossHealth;
        hso.FindProperty("invincibleTime").floatValue = 0.1f;
        hso.ApplyModifiedPropertiesWithoutUndo();

        var glow = new GameObject("BossGlow").AddComponent<Light2D>();
        glow.transform.SetParent(boss.transform, false);
        glow.lightType = Light2D.LightType.Point;
        glow.color = new Color(0.5f, 1f, 0.3f);
        glow.intensity = 0.8f;
        glow.pointLightOuterRadius = 6f;

        GameObject bar = MakeBossBar(boss.GetComponent<Health>(), "OSCAR");

        var so = new SerializedObject(boss.AddComponent<PoisonBoss>());
        so.FindProperty("groundY").floatValue = 0f;
        so.FindProperty("ceilingY").floatValue = 19.5f;
        so.FindProperty("arenaMinX").floatValue = 0f;
        so.FindProperty("arenaMaxX").floatValue = 36f;
        so.FindProperty("healthBarRoot").objectReferenceValue = bar;
        so.FindProperty("runsAwayWhenBeaten").boolValue = true; // he flees here and comes back in Level 3
        SetArray(so.FindProperty("removeOnDeath"), exit[0]);
        SetArray(so.FindProperty("showOnDeath"), exit[1]);
        so.ApplyModifiedPropertiesWithoutUndo();
        return boss;
    }

    static GameObject MakeBossBar(Health target, string title)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (!canvas) return null;
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
        fill.color = new Color(0.35f, 0.85f, 0.15f);

        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(barRoot.transform, false);
        var nameRt = (RectTransform)nameGo.transform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 0f);
        nameRt.anchoredPosition = new Vector2(0f, 4f);
        nameRt.sizeDelta = new Vector2(0f, 30f);
        var nameText = nameGo.AddComponent<TMPro.TextMeshProUGUI>();
        nameText.text = title;
        nameText.fontSize = 24;
        nameText.alignment = TMPro.TextAlignmentOptions.Center;
        nameText.color = Color.white;

        var so = new SerializedObject(barRoot.AddComponent<HealthBar>());
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("fill").objectReferenceValue = fill;
        so.ApplyModifiedPropertiesWithoutUndo();
        return barRoot;
    }

    // ---------------- Player and camera ----------------

    static void PlacePlayerAndCamera()
    {
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (!player) { Debug.LogError("Build Boss Dungeon: no Player in Room_Template"); return; }
        float feet = 1f;
        if (player.TryGetComponent(out BoxCollider2D box)) feet = (box.size.y * 0.5f - box.offset.y) * player.transform.lossyScale.y;
        else if (player.TryGetComponent(out CapsuleCollider2D cap)) feet = (cap.size.y * 0.5f - cap.offset.y) * player.transform.lossyScale.y;
        player.transform.position = new Vector3(3f, feet + 0.05f, 0f);

        Camera cam = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (!cam) return;
        cam.orthographicSize = CameraSize;
        cam.transform.position = new Vector3(18f, 10f, cam.transform.position.z);
        if (!cam.TryGetComponent(out CameraFollow2D follow)) return;
        follow.enabled = true;
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.FindProperty("useBounds").boolValue = true;
        so.FindProperty("minBounds").vector2Value = new Vector2(-1f, -1f);
        so.FindProperty("maxBounds").vector2Value = new Vector2(42f, 21f);
        so.FindProperty("lockToFloors").boolValue = false;
        SerializedProperty zones = so.FindProperty("zones");
        if (zones != null) zones.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- Helpers ----------------

    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        return t;
    }

    static void Fill(int x, int y, int width, int height, bool paint = true)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                map.SetTile(map.WorldToCell(new Vector3(x + i + 0.5f, y + j + 0.5f)), paint ? tile : null);
    }

    static GameObject Box(string name, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boxSprite;
        sr.color = color;
        sr.sortingOrder = 1;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    // Decoration only: a coloured box with no collider
    static GameObject Deco(string name, Transform parent, Vector2 center, Vector2 size, Color color, int order)
    {
        GameObject go = Box(name, parent, center, size, color);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
        go.GetComponent<SpriteRenderer>().sortingOrder = order;
        return go;
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

    // Thin platform: jump up through it from below, hold Down to drop through it
    static void OneWay(string name, Transform parent, Vector2 center, float width)
    {
        GameObject go = Box(name, parent, center, new Vector2(width, 0.5f), StoneColor * 0.85f);
        go.GetComponent<BoxCollider2D>().usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 160f;
        go.AddComponent<OneWayPlatform>();
    }

    static void SetArray(SerializedProperty prop, Object value)
    {
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = value;
    }

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
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) doomed.Add(t.gameObject);
        foreach (GameObject go in doomed) if (go) Object.DestroyImmediate(go);
    }

    static Tilemap FindGroundTilemap(Scene scene)
    {
        int ground = LayerMask.NameToLayer("Ground");
        Tilemap first = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Tilemap m in r.GetComponentsInChildren<Tilemap>(true))
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
