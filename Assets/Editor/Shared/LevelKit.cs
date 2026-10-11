using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Editor-only toolbox shared by the Level 3 builders: make a scene from Room_Template,
// paint tiles, make boxes, rings, doors, secret rooms, a boss health bar, and so on.
public static class LevelKit
{
    public const string TemplateScene = "Assets/Scenes/Room_Template.unity";
    public const string ScenesFolder = "Assets/Scenes";
    const string FallbackTile = "Assets/Tiles/Square.asset";

    public static Tilemap Map;
    public static TileBase Tile;
    public static Sprite BoxSprite;
    public static int GroundLayer;

    public static readonly Color RingColor = new Color(1f, 0.85f, 0.1f);
    public static readonly Color DoorColor = new Color(0.45f, 0.25f, 0.08f);
    public static readonly Color CheckpointColor = new Color(0.9f, 0.9f, 0.1f);
    public static readonly Color IronColor = new Color(0.3f, 0.3f, 0.34f);

    // Copies Room_Template to a new scene, opens it, clears the tiles. Returns false if cancelled.
    public static bool NewSceneFromTemplate(string sceneName, out Scene scene)
    {
        scene = default(Scene);
        string path = $"{ScenesFolder}/{sceneName}.unity";
        if (File.Exists(path) && !EditorUtility.DisplayDialog("Build " + sceneName,
                sceneName + " already exists.\n\nReplace it with a fresh build? Anything you changed in it by hand will be lost.",
                "Replace", "Cancel"))
            return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        if (File.Exists(path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(path);
        }
        if (!AssetDatabase.CopyAsset(TemplateScene, path))
        {
            Debug.LogError("Could not copy " + TemplateScene);
            return false;
        }

        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        DestroyByName(scene, "Exit");
        DestroyByName(scene, "LeftWall");
        DestroyByName(scene, "KillZone");

        Map = FindGroundTilemap(scene);
        Tile = FirstTile(Map) ?? AssetDatabase.LoadAssetAtPath<TileBase>(FallbackTile);
        BoxSprite = (Tile as Tile)?.sprite ?? AssetDatabase.LoadAssetAtPath<Tile>(FallbackTile)?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");
        Map.ClearAllTiles();
        return true;
    }

    public static void Finish(Scene scene, string sceneName, string message)
    {
        RebuildColliders(Map);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToSceneList($"{ScenesFolder}/{sceneName}.unity");
        EditorUtility.DisplayDialog("Build " + sceneName, message, "OK");
    }

    // ---------------- Tiles and boxes ----------------

    public static void Fill(int x, int y, int width, int height, bool paint = true)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                Map.SetTile(Map.WorldToCell(new Vector3(x + i + 0.5f, y + j + 0.5f)), paint ? Tile : null);
    }

    public static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        return t;
    }

    public static GameObject Box(string name, Transform parent, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BoxSprite;
        sr.color = color;
        sr.sortingOrder = 1;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    // Decoration: a coloured box with no collider
    public static GameObject Deco(string name, Transform parent, Vector2 center, Vector2 size, Color color, int order)
    {
        GameObject go = Box(name, parent, center, size, color);
        Object.DestroyImmediate(go.GetComponent<BoxCollider2D>());
        go.GetComponent<SpriteRenderer>().sortingOrder = order;
        return go;
    }

    // ---------------- Pieces ----------------

    public static void Ring(string name, Transform parent, Vector2 pos, float launchSpeed)
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

    // Thin platform: jump up through it, hold Down to drop through it. (x, y) = bottom-left tile, top at y + 1.
    public static GameObject OneWay(string name, Transform parent, int x, int y, int width, Color color)
    {
        GameObject go = Box(name, parent, new Vector2(x + width * 0.5f, y + 0.75f), new Vector2(width, 0.5f), color);
        go.GetComponent<BoxCollider2D>().usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 160f;
        go.AddComponent<OneWayPlatform>();
        return go;
    }

    // Thin platform at any height: center = middle of the platform, top at center.y + 0.25
    public static GameObject OneWayAt(string name, Transform parent, Vector2 center, float width, Color color)
    {
        GameObject go = Box(name, parent, center, new Vector2(width, 0.5f), color);
        go.GetComponent<BoxCollider2D>().usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 160f;
        go.AddComponent<OneWayPlatform>();
        return go;
    }

    public static void Checkpoint(string name, Transform parent, Vector2 pos)
    {
        GameObject cp = Box(name, parent, pos, new Vector2(0.3f, 1.5f), CheckpointColor);
        cp.GetComponent<BoxCollider2D>().isTrigger = true;
        cp.AddComponent<Checkpoint>();
    }

    public static void KillZone(string name, Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
        go.AddComponent<KillZone>();
    }

    // A chest that gives an item (and optionally a bonus item) from the inventory system
    public static void Chest(string name, Transform parent, Vector2 pos, ItemType item = ItemType.Healing, int amount = 1,
                             bool bonus = false, ItemType bonusItem = ItemType.Healing)
    {
        GameObject chest = Box(name, parent, pos, new Vector2(1.5f, 1f), new Color(0.75f, 0.55f, 0.15f));
        var col = chest.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2f, 2.5f);
        var so = new SerializedObject(chest.AddComponent<Chest>());
        so.FindProperty("givesItem").boolValue = true;
        so.FindProperty("item").enumValueIndex = (int)item;
        so.FindProperty("amount").intValue = amount;
        so.FindProperty("givesBonus").boolValue = bonus;
        so.FindProperty("bonusItem").enumValueIndex = (int)bonusItem;
        so.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(chest, chest.GetComponent<Chest>());
    }

    public static void HealthPotion(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(0.6f, 0.6f), new Color(0.3f, 1f, 0.8f));
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        go.AddComponent<HealthPickup>();
    }

    public static GameObject Door(string name, Transform parent, Vector2 pos, string scene, Color color)
    {
        GameObject door = Box(name, parent, pos, new Vector2(1.2f, 2.2f), color);
        door.GetComponent<BoxCollider2D>().isTrigger = true;
        var so = new SerializedObject(door.AddComponent<BossDoor>());
        so.FindProperty("bossScene").stringValue = scene;
        so.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(door, door.GetComponent<BossDoor>());
        return door;
    }

    public static void Crab(string name, Transform parent, Vector2 pos)
    {
        GameObject go = Box(name, parent, pos, new Vector2(1.3f, 0.9f), new Color(0.85f, 0.35f, 0.2f));
        AddHealth(go, 14);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        var chase = new SerializedObject(go.AddComponent<EnemyChase>());
        chase.FindProperty("speed").floatValue = 3.5f;
        chase.FindProperty("detectRange").floatValue = 9f;
        chase.FindProperty("stopDistance").floatValue = 0.3f;
        chase.FindProperty("groundLayer").intValue = 1 << GroundLayer;
        chase.ApplyModifiedPropertiesWithoutUndo();
        var touch = new SerializedObject(go.AddComponent<ContactDamage>());
        touch.FindProperty("damage").intValue = 8;
        touch.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Shooter(string name, Transform parent, Vector2 pos, Color color)
    {
        GameObject go = Box(name, parent, pos, new Vector2(1f, 1.6f), color);
        AddHealth(go, 12);
        var so = new SerializedObject(go.AddComponent<EnemyShooter>());
        so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>("Assets/Prefabs/Projectile.prefab");
        so.FindProperty("range").floatValue = 11f;
        so.FindProperty("fireRate").floatValue = 1.6f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void AddHealth(GameObject go, int maxHealth)
    {
        var so = new SerializedObject(go.AddComponent<Health>());
        so.FindProperty("maxHealth").intValue = maxHealth;
        so.FindProperty("invincibleTime").floatValue = 0.1f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static GameObject Breakable(string name, Transform parent, Vector2 center, Vector2 size, Color color, int hp)
    {
        GameObject wall = Box(name, parent, center, size, color);
        wall.layer = GroundLayer;
        AddHealth(wall, hp);
        wall.AddComponent<BreakableWall>();
        return wall;
    }

    // A wall lantern: bracket, glowing flame, flickering light
    public static void Lantern(string name, Transform parent, Vector2 pos, Color lightColor)
    {
        Deco(name + "_Bracket", parent, pos, new Vector2(0.3f, 0.7f), IronColor, -3);
        GameObject flame = Deco(name, parent, pos + new Vector2(0f, 0.6f), new Vector2(0.35f, 0.55f), Color.Lerp(lightColor, Color.white, 0.4f), -2);
        var light = new GameObject("Light").AddComponent<Light2D>();
        light.transform.SetParent(flame.transform, false);
        light.lightType = Light2D.LightType.Point;
        light.color = lightColor;
        light.intensity = 1.2f;
        light.pointLightOuterRadius = 7f;
        light.pointLightInnerRadius = 1f;
        var so = new SerializedObject(flame.AddComponent<TorchFlicker>());
        so.FindProperty("torchLight").objectReferenceValue = light;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetGlobalLight(Scene scene, float intensity, Color color)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (Light2D l in r.GetComponentsInChildren<Light2D>(true))
                if (l.lightType == Light2D.LightType.Global) { l.intensity = intensity; l.color = color; }
    }

    // A room outside the level with a chest and a potion. The entrance is placed in the level at doorPos;
    // the exit door inside brings you to returnPos.
    public static Transform SecretRoom(string id, Transform parent, int left, int floorY, Vector2 doorPos, Vector2 returnPos,
                                       ItemType loot = ItemType.Healing, ItemType bonusLoot = ItemType.Healing)
    {
        const int width = 26, inside = 11;
        int right = left + width;
        var room = Group("SecretRoom_" + id, parent);
        Fill(left, floorY - 1, width + 1, 1);
        Fill(left, floorY + inside, width + 1, 1);
        Fill(left, floorY - 1, 1, inside + 2);
        Fill(right, floorY - 1, 1, inside + 2);

        Chest("Chest_Secret" + id, room, new Vector2(left + 13f, floorY + 0.5f), loot, 1, true, bonusLoot);
        HealthPotion("Potion_Secret" + id, room, new Vector2(left + 5f, floorY + 1f));

        var inSpawn = new GameObject("RoomSpawn").transform;
        inSpawn.SetParent(room);
        inSpawn.position = new Vector2(right - 4.5f, floorY + 1.1f);
        var backSpawn = new GameObject("ReturnSpawn").transform;
        backSpawn.SetParent(room);
        backSpawn.position = returnPos;

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
        return room;
    }

    // ---------------- Player, camera, UI ----------------

    public static Transform PlacePlayer(Vector2 feetPos)
    {
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        if (!player) { Debug.LogError("No Player in Room_Template"); return null; }
        float feet = 1f;
        if (player.TryGetComponent(out BoxCollider2D box)) feet = (box.size.y * 0.5f - box.offset.y) * player.transform.lossyScale.y;
        else if (player.TryGetComponent(out CapsuleCollider2D cap)) feet = (cap.size.y * 0.5f - cap.offset.y) * player.transform.lossyScale.y;
        player.transform.position = new Vector3(feetPos.x, feetPos.y + feet + 0.05f, 0f);
        return player.transform;
    }

    public static void SetUpCamera(Transform player, float size, Vector2 min, Vector2 max)
    {
        Camera cam = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (!cam) return;
        cam.orthographicSize = size;
        if (player) cam.transform.position = new Vector3(player.position.x, player.position.y, cam.transform.position.z);
        if (!cam.TryGetComponent(out CameraFollow2D follow)) return;
        follow.enabled = true;
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player;
        so.FindProperty("useBounds").boolValue = true;
        so.FindProperty("minBounds").vector2Value = min;
        so.FindProperty("maxBounds").vector2Value = max;
        so.FindProperty("lockToFloors").boolValue = false;
        SerializedProperty zones = so.FindProperty("zones");
        if (zones != null) zones.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static GameObject BossBar(Health target, string title, Color fillColor, float yOffset = -40f)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (!canvas) return null;
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var barRoot = new GameObject("BossBar", typeof(RectTransform), typeof(Image));
        barRoot.transform.SetParent(canvas.transform, false);
        var rootRt = (RectTransform)barRoot.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, yOffset);
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
        fill.color = fillColor;

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

    public static void AddPrompt(GameObject target, Component owner)
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

    // ---------------- Scene plumbing ----------------

    public static void RebuildColliders(Tilemap tilemap)
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

    public static Tilemap FindGroundTilemap(Scene scene)
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

    public static TileBase FirstTile(Tilemap tilemap)
    {
        foreach (TileBase t in tilemap.GetTilesBlock(tilemap.cellBounds))
            if (t) return t;
        return null;
    }

    static void AddToSceneList(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}