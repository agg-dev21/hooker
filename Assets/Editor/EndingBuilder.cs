using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only. Adds two menu items:
//   Tools > Story > Build Ending (Mother's Grave)
//       Makes the scene Ending_Grave: a quiet cliff over the sea at dusk with Mom's grave,
//       her letter and her wedding ring on it (see EndingGrave.cs for the whole scene and every line).
//   Tools > Story > Add Ending Hook And Ariel Talks
//       Level_4_Boss: KK's last words when he is beaten, then the fade to Ending_Grave.
//       Level_3 and Level_4: Raphael and Ariel talk as they go (only if Ariel is with him).
//       Run it again any time: it replaces what it added before.
public static class EndingBuilder
{
    const string SceneName = "Ending_Grave";
    const string R = "Raphael", A = "Ariel";

    static readonly Color DuskTop = new Color(0.16f, 0.1f, 0.3f);
    static readonly Color DuskLow = new Color(0.95f, 0.55f, 0.6f);
    static readonly Color Stone = new Color(0.55f, 0.55f, 0.62f);
    static readonly Color GrassColor = new Color(0.28f, 0.42f, 0.3f);

    // ---------------- The grave scene ----------------

    [MenuItem("Tools/Story/Build Ending (Mother's Grave)")]
    static void BuildEnding()
    {
        if (!NewSceneFromTemplate(SceneName, out Scene scene)) return;
        Map.color = new Color(0.32f, 0.3f, 0.36f);

        var root = new GameObject("MothersGrave").transform;
        var decor = Group("Decor", root);
        var props = Group("Props", root);

        // Cliff top: walk from the left to the grave near the edge
        Fill(-6, -12, 38, 12); // the cliff (top at y 0), ending at x 32 where it drops to the sea
        Fill(-6, 0, 3, 14);    // rocks on the left
        Deco("Grass", decor, new Vector2(13f, 0.08f), new Vector2(38f, 0.18f), GrassColor, 2);
        // A low wooden fence at the edge, and an invisible wall so nobody falls off during the ending
        Deco("FencePost", decor, new Vector2(31.2f, 0.5f), new Vector2(0.18f, 1f), new Color(0.35f, 0.24f, 0.15f), 3);
        Deco("FencePost", decor, new Vector2(29.8f, 0.5f), new Vector2(0.18f, 1f), new Color(0.35f, 0.24f, 0.15f), 3);
        Deco("FenceRail", decor, new Vector2(30.5f, 0.75f), new Vector2(1.7f, 0.12f), new Color(0.35f, 0.24f, 0.15f), 3);
        var edge = new GameObject("CliffEdge").AddComponent<BoxCollider2D>();
        edge.transform.SetParent(decor);
        edge.transform.position = new Vector2(31.6f, 6f);
        edge.size = new Vector2(0.4f, 14f);

        // Sky at dusk: bands from deep purple to pink at the horizon, a moon, stars
        for (int i = 0; i < 10; i++)
        {
            Color c = Color.Lerp(DuskLow, DuskTop, i / 9f);
            Deco("Sky", decor, new Vector2(16f, -2f + i * 1.8f), new Vector2(60f, 1.85f), c, -30);
        }
        GameObject moon = Deco("Moon", decor, new Vector2(30f, 11f), new Vector2(1.6f, 1.6f), new Color(1f, 0.95f, 0.85f), -25);
        moon.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        Deco("MoonCore", decor, new Vector2(30f, 11f), new Vector2(1.6f, 1.6f), new Color(1f, 0.95f, 0.85f), -25);
        var rnd = new System.Random(7);
        for (int i = 0; i < 40; i++)
            Deco("Star", decor, new Vector2(-4f + (float)rnd.NextDouble() * 44f, 6f + (float)rnd.NextDouble() * 8f),
                 Vector2.one * (0.06f + (float)rnd.NextDouble() * 0.08f), new Color(1f, 1f, 1f, 0.8f), -26);

        // The sea far below, past the cliff edge
        Sprite ocean = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Ocean/ocean.png").OfType<Sprite>().FirstOrDefault();
        if (ocean)
            for (int i = 0; i < 3; i++)
            {
                var sea = new GameObject("Sea").AddComponent<SpriteRenderer>();
                sea.transform.SetParent(decor);
                sea.sprite = ocean;
                sea.sortingOrder = -20;
                float w = ocean.bounds.size.x;
                float s = 30f / w;
                sea.transform.localScale = new Vector3(s, s, 1f);
                sea.transform.position = new Vector3(5f + i * 30f, -9f - ocean.bounds.min.y * s, 0f);
            }

        // The grave: headstone, inscription, flowers, candles
        Vector2 gravePos = new Vector2(27f, 0f);
        GameObject stone = Deco("Headstone", props, gravePos + new Vector2(0f, 1.3f), new Vector2(1.8f, 2.6f), Stone, 3);
        Deco("HeadstoneTop", props, gravePos + new Vector2(0f, 2.75f), new Vector2(1.3f, 0.5f), Stone, 3);
        Deco("Mound", props, gravePos + new Vector2(-1.8f, 0.2f), new Vector2(2.6f, 0.4f), new Color(0.3f, 0.22f, 0.18f), 2);
        var inscription = new GameObject("Inscription").AddComponent<TMPro.TextMeshPro>();
        inscription.transform.SetParent(props);
        inscription.transform.position = gravePos + new Vector2(0f, 1.5f);
        inscription.text = "MOM\n<size=60%>She sailed away\nbut never far</size>";
        inscription.fontSize = 3f;
        inscription.alignment = TMPro.TextAlignmentOptions.Center;
        inscription.color = new Color(0.25f, 0.24f, 0.3f);
        inscription.rectTransform.sizeDelta = new Vector2(1.7f, 2f);
        inscription.GetComponent<MeshRenderer>().sortingOrder = 4;
        Color[] flowers = { new Color(1f, 0.55f, 0.7f), new Color(1f, 0.85f, 0.4f), new Color(0.75f, 0.55f, 1f), Color.white };
        for (int i = 0; i < 9; i++)
        {
            float x = gravePos.x - 3f + i * 0.45f + (i % 2) * 0.1f;
            Deco("Stem", props, new Vector2(x, 0.3f), new Vector2(0.06f, 0.5f), GrassColor, 3);
            Deco("Flower", props, new Vector2(x, 0.6f), new Vector2(0.22f, 0.22f), flowers[i % flowers.Length], 4);
        }
        Lantern("Candle_Left", decor, gravePos + new Vector2(-1.3f, 0.4f), new Color(1f, 0.7f, 0.4f));
        Lantern("Candle_Right", decor, gravePos + new Vector2(1.3f, 0.4f), new Color(1f, 0.7f, 0.4f));

        // Her letter and her ring, lying on the grave
        SpriteRenderer letter = Item("Letter", props, "Assets/Art/Items/item_scroll_blue.png", gravePos + new Vector2(-1.4f, 0.55f), 0.9f);
        SpriteRenderer ring = Item("WeddingRing", props, "Assets/Art/Items/item_ring_blue.png", gravePos + new Vector2(-0.6f, 0.5f), 0.7f);

        // Ariel (only shown if she travels with Raphael)
        Sprite[] arielFrames = Frames("Assets/Art/Companion/companion_idle.png");
        var arielGo = new GameObject("Ariel");
        arielGo.transform.SetParent(props);
        var ariel = arielGo.AddComponent<SpriteRenderer>();
        ariel.sprite = arielFrames.Length > 0 ? arielFrames[0] : BoxSprite;
        ariel.sortingOrder = 6;
        if (arielFrames.Length == 0) { arielGo.transform.localScale = new Vector3(0.8f, 1.8f, 1f); ariel.color = new Color(0.6f, 0.85f, 1f); }
        arielGo.SetActive(false);

        // The scene's director
        var director = new GameObject("Ending").AddComponent<EndingGrave>();
        var so = new SerializedObject(director);
        so.FindProperty("grave").objectReferenceValue = stone.transform;
        so.FindProperty("letterOnGrave").objectReferenceValue = letter;
        so.FindProperty("ringOnGrave").objectReferenceValue = ring;
        so.FindProperty("ariel").objectReferenceValue = ariel;
        so.FindProperty("arielStart").vector2Value = new Vector2(-1f, 1.6f);
        SerializedProperty frames = so.FindProperty("arielFrames");
        frames.arraySize = arielFrames.Length;
        for (int i = 0; i < arielFrames.Length; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = arielFrames[i];
        so.FindProperty("boxSprite").objectReferenceValue = BoxSprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        SetGlobalLight(scene, 0.75f, new Color(0.85f, 0.7f, 0.9f));
        Transform player = PlacePlayer(new Vector2(1f, 0f));
        SetUpCamera(player, 5.5f, new Vector2(-4f, -3f), new Vector2(42f, 14f));

        Finish(scene, SceneName, "Done! " + SceneName + " is built and in the Scene List.\n\n" +
            "Now run Tools > Story > Add Ending Hook And Ariel Talks so beating KK leads here.");
    }

    static SpriteRenderer Item(string name, Transform parent, string path, Vector2 pos, float worldSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        Sprite sp = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        sr.sprite = sp ? sp : BoxSprite;
        sr.sortingOrder = 5;
        float size = sp ? Mathf.Max(sp.bounds.size.x, sp.bounds.size.y) : 1f;
        go.transform.localScale = Vector3.one * (worldSize / size);
        if (!sp) sr.color = name == "WeddingRing" ? new Color(1f, 0.8f, 0.3f) : new Color(0.9f, 0.85f, 0.75f);
        return sr;
    }

    static Sprite[] Frames(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Where(sp => int.TryParse(sp.name.Substring(sp.name.LastIndexOf('_') + 1), out _))
            .OrderBy(sp => int.Parse(sp.name.Substring(sp.name.LastIndexOf('_') + 1)))
            .ToArray();
    }

    // ---------------- KK's last words + Ariel's talks ----------------

    [MenuItem("Tools/Story/Add Ending Hook And Ariel Talks")]
    static void AddHookAndTalks()
    {
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string report = "";

        // Level_4_Boss: KK's last words, then the grave
        report += InScene("Level_4_Boss", scene =>
        {
            Remove(scene, "EndingHook");
            FatherBoss kk = Object.FindAnyObjectByType<FatherBoss>(FindObjectsInactive.Include);
            if (!kk || !kk.TryGetComponent(out Health h)) return "no KK (FatherBoss) found";
            var root = new GameObject("EndingHook");
            var d = root.AddComponent<DefeatDialogue>();
            d.SetBoss(h);
            d.lines = L(
                "KK", "Heh... the one they call the Flaw. And still my greatest deal.",
                R, "Why didn't you ever look for me?",
                "KK", "The milk was running low.",
                R, "...Guess you're no better than the rest.",
                "KK", "No. I'm not.",
                "KK", "Your mother. You asked where she is.",
                "KK", "Past this hall there's a path up the cliff. She asked to be buried where she could see the sea.",
                "KK", "She left something for you. She made me promise to keep it there until you came.",
                R, "...She's gone?",
                "KK", "A long time now. Go on, kid. She waited long enough.");
            EditorUtility.SetDirty(d);
            root.AddComponent<AfterBossScene>().Set(h, SceneName);
            return "done";
        });

        // Level_3: talks along the way
        report += InScene("Level_3", scene =>
        {
            Remove(scene, "ArielTalks");
            var talks = new[]
            {
                L(A, "So... why are you actively hunting these people?",
                  R, "Long story.",
                  A, "We're in a flooded prison. I have time.",
                  R, "My mom left when I was six. She left me a letter. It said: find me when you're ready.",
                  R, "Four men could be my father. Hardy and Oscar weren't. Two names left.",
                  A, "....",
                  A, "It's kind of funny.",
                  R, "Funny?",
                  A, "You're chasing four strangers to find out who you are. I ran away from a whole kingdom so nobody could tell me who I am."),
                L(A, "Did you read that note? A crewman in love with Madame O. Too scared to tell her.",
                  R, "Poor guy.",
                  A, "Some people wait their whole life to say one sentence.",
                  R, "What sentence would you wait for?",
                  A, "Ask me again when we're not about to drown."),
                L(A, "Whatever she tells you in there... you're not what they call you.",
                  R, "The Flaw of the Seven Seas?",
                  A, "The flaw is the guy who broke a stranger out of a cell for nothing. Weird kind of flaw.",
                  R, "...Thanks.",
                  A, "Don't get used to it."),
            };
            return PlaceTalks(talks, new[] { 10f, 60f, 110f });
        });

        // Level_4: the hallway to the father
        report += InScene("Level_4", scene =>
        {
            Remove(scene, "ArielTalks");
            var talks = new[]
            {
                L(A, "This is it, isn't it? Your father is at the end of this hall.",
                  R, "Probably. Or another guy who paid Mom in fish.",
                  A, "...",
                  A, "Sorry. That shouldn't be funny.",
                  R, "It's a little funny."),
                L(A, "What will you do if it's really him?",
                  R, "Ask him why he never looked for me.",
                  A, "And if you don't like the answer?",
                  R, "Then I'll still have walked here with you. That's not nothing."),
                L(A, "Raphael. Whatever happens in there, I'm not leaving.",
                  R, "Everyone says that.",
                  A, "I'm not everyone. I'm royalty. We keep our word. It's very annoying.",
                  R, "...Okay.",
                  A, "Go get your answers. I'll be right behind you."),
            };
            return PlaceTalks(talks, new[] { 8f, 45f, 85f });
        });

        EditorUtility.DisplayDialog("Ending Hook And Ariel Talks", report, "OK");
    }

    // Puts the talks at the level's checkpoints (in order, left to right), or at the fallback x positions
    static string PlaceTalks(System.Collections.Generic.List<DialogueLine>[] talks, float[] fallbackX)
    {
        var points = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include)
            .Select(c => (Vector2)c.transform.position).OrderBy(p => p.x).ToList();
        var root = new GameObject("ArielTalks");
        for (int i = 0; i < talks.Length; i++)
        {
            Vector2 at = i < points.Count && points.Count >= talks.Length
                ? points[Mathf.RoundToInt(i * (points.Count - 1f) / (talks.Length - 1f))] + Vector2.right * 2f
                : new Vector2(fallbackX[i], 2f);
            var go = new GameObject("Talk_" + (i + 1));
            go.transform.SetParent(root.transform);
            go.transform.position = at;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 30f); // tall: you can't jump over it
            var talk = go.AddComponent<CompanionTalk>();
            talk.lines = talks[i];
            EditorUtility.SetDirty(talk);
        }
        return "done (" + talks.Length + " talks)";
    }

    static string InScene(string sceneName, System.Func<Scene, string> work)
    {
        string path = "Assets/Scenes/" + sceneName + ".unity";
        if (!File.Exists(path)) return sceneName + ": scene missing\n";
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
        string result = work(scene);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return sceneName + ": " + result + "\n";
    }

    static void Remove(Scene scene, string rootName)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == rootName) Object.DestroyImmediate(r);
    }

    static System.Collections.Generic.List<DialogueLine> L(params string[] pairs)
    {
        var list = new System.Collections.Generic.List<DialogueLine>();
        for (int i = 0; i + 1 < pairs.Length; i += 2) list.Add(new DialogueLine { speaker = pairs[i], text = pairs[i + 1] });
        return list;
    }
}
