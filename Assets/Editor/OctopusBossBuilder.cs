using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only. Adds Tools > Build Level 3 Boss (Octopus).
// Level_3_Boss: the Abyssal Throne. The Chain Siren (half woman, half octopus) sits on her throne on the
// left side and never moves; she attacks with chains (see OctopusBoss.cs). You come in from the right.
// The lizard from Level 2 is back too: he runs around the arena with his Level 2 moves (PoisonBoss).
// The two take turns attacking (BossTurns), each has a health bar, and the gate opens when BOTH are beaten.
//   - Two thin ledges and two hook rings: safe from the floor lashes, but not from chain rain or grabs.
//   - Beat her and the gate on the right opens; the door behind it loads Level_4.
// Placeholder art (coloured boxes): swap in real sprites on the "ChainSiren" parts later.
public static class OctopusBossBuilder
{
    const string SceneName = "Level_3_Boss";
    const string NextScene = "Level_4";
    const int BossHealth = 600; // tougher than Hardy (see Tools > Make Level 3 Boss Harder)
    const int LizardHealth = 320;
    public const float SirenLift = 7f; // Madame O sits this high above the floor, on a ledge in the top-left

    static readonly Color StoneColor = new Color(0.3f, 0.38f, 0.45f);
    static readonly Color AbyssColor = new Color(0.04f, 0.05f, 0.11f);
    static readonly Color SkinColor = new Color(0.62f, 0.72f, 0.78f);
    static readonly Color TentacleColor = new Color(0.45f, 0.18f, 0.45f);
    static readonly Color HairColor = new Color(0.12f, 0.1f, 0.25f);
    static readonly Color ChainColor = new Color(0.5f, 0.5f, 0.56f);
    static readonly Color OneWayColor = new Color(0.4f, 0.45f, 0.5f);

    [MenuItem("Tools/Build Level 3 Boss (Octopus)")]
    static void Build()
    {
        if (!NewSceneFromTemplate(SceneName, out Scene scene)) return;
        Map.color = StoneColor;

        var root = new GameObject("AbyssalThrone").transform;
        var decor = Group("Decor", root);
        var props = Group("Props", root);

        // Arena: inside x 0-33, floor top at y 0, ceiling at y 18. Exit corridor carved into the right wall.
        Fill(-3, -3, 44, 3);
        Fill(-3, 18, 44, 3);
        Fill(-3, 0, 3, 18);
        Fill(34, 0, 7, 18);
        Fill(34, 0, 6, 4, false);
        Fill(0, (int)SirenLift, 6, 2); // her throne: a high ledge in the top-left (top at y 9)
        OneWayAt("SirenLedge", props, new Vector2(8f, SirenLift + 2.25f), 4f, OneWayColor); // climb up here to slash her

        // Low side ledges (top at y 3.5, like the dungeon): safe from both lashes; the lizard's ledge dash runs along them
        OneWayAt("SideLedge_Left", props, new Vector2(9f, 3.25f), 4f, OneWayColor);
        OneWayAt("SideLedge_Right", props, new Vector2(30f, 3.25f), 4f, OneWayColor);
        // Middle ledges (top at y 6)
        OneWay("Ledge_Left", props, 15, 5, 3, OneWayColor);
        OneWay("Ledge_Right", props, 21, 5, 3, OneWayColor);
        Ring("Ring_Left", props, new Vector2(16.5f, 12f), 6f);
        Ring("Ring_Right", props, new Vector2(22.5f, 12f), 6f);

        // Exit: gate that opens when she dies, door behind it
        GameObject gate = Box("ExitGate", props, new Vector2(34.5f, 2f), new Vector2(1f, 4f), IronColor);
        gate.layer = GroundLayer;
        GameObject door = Door("ExitDoor", props, new Vector2(38.5f, 1.1f), NextScene, DoorColor);
        door.SetActive(false);

        BuildDecor(decor);
        SetGlobalLight(scene, 0.45f, new Color(0.55f, 0.6f, 0.95f));
        Health siren = BuildSiren(root);
        RaiseSiren(siren.transform, root);
        Health lizard = BuildLizard(root);

        // The exit opens only when both are dead
        var gateSo = new SerializedObject(root.gameObject.AddComponent<BossGroupGate>());
        SetArray(gateSo.FindProperty("bosses"), siren);
        gateSo.FindProperty("bosses").arraySize = 2;
        gateSo.FindProperty("bosses").GetArrayElementAtIndex(1).objectReferenceValue = lizard;
        SetArray(gateSo.FindProperty("removeWhenAllDead"), gate);
        SetArray(gateSo.FindProperty("showWhenAllDead"), door);
        gateSo.ApplyModifiedPropertiesWithoutUndo();

        Transform player = PlacePlayer(new Vector2(30f, 0f));
        SetUpCamera(player, 9f, new Vector2(-1f, -1f), new Vector2(41f, 19f));

        Finish(scene, SceneName, "Done! " + SceneName + " is built, open, and in the Scene List.\n\n" +
            "Beat Madame O AND Oscar to open the gate. The door behind it loads '" + NextScene + "'.");
    }

    static Health BuildSiren(Transform parent)
    {
        // Her body: the solid box you hit. Everything else is drawing on top of it.
        GameObject body = Box("ChainSiren", parent, new Vector2(3f, 4.8f), new Vector2(3.2f, 5.6f), TentacleColor);
        body.GetComponent<SpriteRenderer>().enabled = false;
        AddHealth(body, BossHealth);

        Transform look = Group("ChainSiren_Look", parent); // drawing parts (not inside the body box, so they keep their own sizes)
        var parts = new System.Collections.Generic.List<SpriteRenderer>();
        // Octopus half: a skirt of tentacles spilling over the throne
        for (int i = 0; i < 6; i++)
        {
            float x = 0.6f + i * 1.0f;
            GameObject t = Deco("Tentacle", look, new Vector2(x, 2.6f - (i % 2) * 0.3f), new Vector2(0.7f, 2.2f), TentacleColor, 4);
            t.transform.rotation = Quaternion.Euler(0f, 0f, (i - 2.5f) * 12f);
            parts.Add(t.GetComponent<SpriteRenderer>());
        }
        parts.Add(Deco("Lower", look, new Vector2(3f, 3.5f), new Vector2(3.4f, 1.6f), TentacleColor, 5).GetComponent<SpriteRenderer>());
        // Human half
        parts.Add(Deco("Torso", look, new Vector2(3f, 5.4f), new Vector2(1.6f, 2.2f), SkinColor, 5).GetComponent<SpriteRenderer>());
        parts.Add(Deco("Head", look, new Vector2(3f, 7f), new Vector2(1.1f, 1.1f), SkinColor, 6).GetComponent<SpriteRenderer>());
        parts.Add(Deco("Hair", look, new Vector2(2.6f, 6.9f), new Vector2(1.2f, 1.9f), HairColor, 5).GetComponent<SpriteRenderer>());
        Deco("Eye", look, new Vector2(3.3f, 7.1f), new Vector2(0.18f, 0.18f), new Color(1f, 0.85f, 0.2f), 7);
        parts.Add(Deco("Arm", look, new Vector2(4.2f, 5.6f), new Vector2(1.6f, 0.4f), SkinColor, 6).GetComponent<SpriteRenderer>());
        // Chains wrapped around her arms
        Deco("ArmChain", look, new Vector2(4.4f, 5.6f), new Vector2(1.2f, 0.18f), ChainColor, 7);

        var hands = new GameObject("Hands").transform;
        hands.SetParent(body.transform, false);
        hands.position = new Vector2(5.2f, 5.6f);

        GameObject bar = BossBar(body.GetComponent<Health>(), "MADAME O", new Color(0.55f, 0.25f, 0.75f));

        var so = new SerializedObject(body.AddComponent<OctopusBoss>());
        so.FindProperty("groundY").floatValue = 0f;
        so.FindProperty("ceilingY").floatValue = 17.5f;
        so.FindProperty("arenaMaxX").floatValue = 34f;
        so.FindProperty("rainMinX").floatValue = 8f;
        so.FindProperty("rainMaxX").floatValue = 32.5f;
        so.FindProperty("hands").objectReferenceValue = hands;
        so.FindProperty("boxSprite").objectReferenceValue = BoxSprite;
        so.FindProperty("healthBarRoot").objectReferenceValue = bar;
        so.FindProperty("restTime").floatValue = 0.6f;
        SerializedProperty partsProp = so.FindProperty("bodyParts");
        partsProp.arraySize = parts.Count;
        for (int i = 0; i < parts.Count; i++) partsProp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return body.GetComponent<Health>();
    }

    // Lifts Madame O (her body and her drawing) up onto the high throne
    static void RaiseSiren(Transform body, Transform root)
    {
        body.position += Vector3.up * SirenLift;
        Transform look = root.Find("ChainSiren_Look");
        if (look) look.position += Vector3.up * SirenLift;
    }

    // Existing Level_3_Boss: moves Madame O up onto a high throne without rebuilding the room
    // (keeps everything else, like Oscar's art). Running it twice does nothing the second time.
    [MenuItem("Tools/Raise Madame O (Level 3 Boss)")]
    static void RaiseInExistingScene()
    {
        string path = ScenesFolder + "/" + SceneName + ".unity";
        if (!System.IO.File.Exists(path) || !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
        OctopusBoss siren = Object.FindAnyObjectByType<OctopusBoss>(FindObjectsInactive.Include);
        if (!siren) { EditorUtility.DisplayDialog("Raise Madame O", "Madame O isn't in Level_3_Boss.", "OK"); return; }
        if (siren.transform.position.y > SirenLift) { EditorUtility.DisplayDialog("Raise Madame O", "She is already up high.", "OK"); return; }

        Map = FindGroundTilemap(scene);
        Tile = FirstTile(Map);
        BoxSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Square.asset")?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");

        Transform root = siren.transform.parent;
        Fill(0, 0, 6, 2, false);                       // old low throne
        Fill(0, (int)SirenLift, 6, 2);                 // new high throne
        RebuildColliders(Map);
        var solid = new GameObject("HighThroneSolid"); // its own collider, in case the tilemap's merged one doesn't update
        solid.transform.SetParent(root);
        solid.transform.position = new Vector3(3f, SirenLift + 1f, 0f);
        solid.layer = GroundLayer;
        solid.AddComponent<BoxCollider2D>().size = new Vector2(6f, 2f);
        Transform props = root ? root.Find("Props") : null;
        OneWayAt("SirenLedge", props ? props : root, new Vector2(8f, SirenLift + 2.25f), 4f, OneWayColor);
        RaiseSiren(siren.transform, root);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = siren.gameObject;
        EditorUtility.DisplayDialog("Raise Madame O", "Madame O now attacks from a high throne in the top-left.\nA thin ledge next to her lets you climb up and slash her.", "OK");
    }

    // The Level 2 lizard (same script, same moves), kept out of the throne area
    static Health BuildLizard(Transform parent)
    {
        Vector2 size = new Vector2(5.9f, 4.1f); // same as Tools > Set Up Lizard Art (drawn at 60 pixels per tile)
        GameObject lizard = Box("Lizard", parent, new Vector2(20f, size.y * 0.5f), size, new Color(0.25f, 0.65f, 0.15f));
        lizard.GetComponent<SpriteRenderer>().sortingOrder = 5;
        foreach (float ex in new[] { -0.2f, 0.2f }) // placeholder eyes (hidden when the drawing is on)
        {
            GameObject eye = Deco("Eye", lizard.transform, Vector2.zero, Vector2.one, new Color(1f, 0.95f, 0.3f), 6);
            eye.transform.localPosition = new Vector3(ex, 0.18f, 0f);
            eye.transform.localScale = new Vector3(0.12f, 0.15f, 1f);
        }
        AddHealth(lizard, LizardHealth);
        GameObject bar = BossBar(lizard.GetComponent<Health>(), "OSCAR", new Color(0.35f, 0.85f, 0.15f), -95f);

        var so = new SerializedObject(lizard.AddComponent<PoisonBoss>());
        so.FindProperty("groundY").floatValue = 0f;
        so.FindProperty("ceilingY").floatValue = 17.5f;
        so.FindProperty("arenaMinX").floatValue = 6.5f; // never runs into the throne
        so.FindProperty("arenaMaxX").floatValue = 34f;
        so.FindProperty("startDelay").floatValue = 2f; // Madame O opens the fight, then he joins in
        so.FindProperty("restTime").floatValue = 0.8f;
        so.FindProperty("spitPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Projectile>("Assets/Prefabs/Projectile.prefab");
        so.FindProperty("healthBarRoot").objectReferenceValue = bar;
        so.FindProperty("ledgeTopY").floatValue = 3.5f; // the two side ledges
        so.ApplyModifiedPropertiesWithoutUndo();

        PutLizardArt(lizard.GetComponent<PoisonBoss>());
        return lizard.GetComponent<Health>();
    }

    // Same drawings as the Level 2 lizard (sliced by Tools > Set Up Lizard Art). Keeps the green box if they aren't sliced yet.
    static void PutLizardArt(PoisonBoss boss)
    {
        Sprite[] idle = Frames("lizard_idle"), dash = Frames("lizard_dash"), spew = Frames("lizard_spew"), poison = Frames("lizard_poison");
        if (idle.Length == 0)
        {
            Debug.LogWarning("Level 3 Boss: the lizard drawings aren't sliced yet. Run Tools > Set Up Lizard Art, then build this again.");
            return;
        }
        if (boss.TryGetComponent(out SpriteRenderer boxRenderer)) boxRenderer.enabled = false;
        foreach (Transform child in boss.transform)
            if (child.name == "Eye") child.gameObject.SetActive(false);

        var art = new GameObject("Art");
        art.transform.SetParent(boss.transform, false);
        var body = boss.GetComponent<BoxCollider2D>();
        Vector3 s = boss.transform.lossyScale;
        art.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
        art.transform.localPosition = new Vector3(body.offset.x, body.offset.y - body.size.y * 0.5f, 0f);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        sr.sortingOrder = 5;

        var so = new SerializedObject(boss);
        so.FindProperty("artRenderer").objectReferenceValue = sr;
        so.FindProperty("idleFrame").objectReferenceValue = idle[0];
        SetSprites(so.FindProperty("dashFrames"), dash);
        SetSprites(so.FindProperty("spewFrames"), spew);
        SetSprites(so.FindProperty("poisonFrames"), poison);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Sprite[] Frames(string sheet)
    {
        string path = "Assets/Art/Lizard/" + sheet + ".png";
        if (!File.Exists(path)) return new Sprite[0];
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Where(sp => sp.name.Contains("_") && int.TryParse(sp.name.Substring(sp.name.LastIndexOf('_') + 1), out _))
            .OrderBy(sp => int.Parse(sp.name.Substring(sp.name.LastIndexOf('_') + 1)))
            .ToArray();
    }

    static void SetSprites(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    static void BuildDecor(Transform decor)
    {
        Deco("Abyss", decor, new Vector2(19f, 8f), new Vector2(50f, 26f), AbyssColor, -20);
        // Arched windows to the deep sea, with faint light
        foreach (float x in new[] { 11f, 19.5f, 28f })
        {
            Deco("Window", decor, new Vector2(x, 11f), new Vector2(3f, 5f), new Color(0.08f, 0.16f, 0.3f), -18);
            Deco("WindowTop", decor, new Vector2(x, 13.8f), new Vector2(2.2f, 0.8f), new Color(0.08f, 0.16f, 0.3f), -18);
            Deco("WindowBar", decor, new Vector2(x, 11f), new Vector2(0.12f, 5f), IronColor, -17);
        }
        // Chains hanging all around the throne
        foreach (Vector2 c in new[] { new Vector2(1f, 9f), new Vector2(2.5f, 6f), new Vector2(6.5f, 8f), new Vector2(8f, 4f), new Vector2(16f, 3f), new Vector2(31f, 5f) })
        {
            Deco("Chain", decor, new Vector2(c.x, 18f - c.y * 0.5f), new Vector2(0.14f, c.y), ChainColor, -5);
            Deco("Shackle", decor, new Vector2(c.x, 18f - c.y), new Vector2(0.5f, 0.5f), ChainColor, -5);
        }
        // Throne back and carved tentacles
        Deco("ThroneBack", decor, new Vector2(2.8f, 6f), new Vector2(5f, 8f), new Color(0.18f, 0.2f, 0.28f), -10);
        Deco("ThroneCrest", decor, new Vector2(2.8f, 10.4f), new Vector2(3f, 1.2f), new Color(0.35f, 0.25f, 0.45f), -10);
        Lantern("Lantern_1", decor, new Vector2(9f, 8f), new Color(0.6f, 0.4f, 1f));
        Lantern("Lantern_2", decor, new Vector2(33.5f, 8f), new Color(0.5f, 0.85f, 1f));
        // Puddles of sea water on the floor
        foreach (float x in new[] { 10f, 18f, 27f })
            Deco("Puddle", decor, new Vector2(x, 0.06f), new Vector2(2.5f, 0.12f), new Color(0.2f, 0.5f, 0.7f, 0.7f), 2);
    }

    static void SetArray(SerializedProperty prop, Object value)
    {
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = value;
    }
}