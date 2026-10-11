using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Part of TutorialRoomBuilder (see TutorialRoomBuilder.cs for its settings and the main loop).
// Level 1 Deck: the boss arena on the top deck, its camera and Hardy.
public static partial class TutorialRoomBuilder
{
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
}
