using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Art > Hardy Arena Background.
// Puts the team's arena picture (Assets/Art/Hardy/hardy_arena.png: the dark night, the three glowing masts,
// the railing and the cabin) behind Hardy's boss fight on the deck (Level_1_Deck).
// The picture is STATIC: it stays put in the world while the camera moves past it, like real scenery.
// ONE picture, never repeated: it is made big enough to span the whole deck (3 screens long),
// with its deck lined up on the real deck, so the camera never sees past its edges.
// The picture is the WHOLE background: the old deck decoration (sky, stars, moon, mast, sails, railing, goo)
// and the moving ocean are hidden (not deleted), a dark night fills the picture's empty sky,
// and the light is turned up so the picture shows in its real colours. Run it again any time; it replaces the old picture.
public static class HardyArenaBackground
{
    const string ArtPath = "Assets/Art/Hardy/hardy_arena.png";
    const string ScenePath = "Assets/Scenes/Level_1_Deck.unity";
    const float DeckY = -5f;          // the deck surface in Level_1_Deck
    const float DeckLine = 0.12f;     // the deck in the picture: 12% up from its bottom
    const int SortingOrder = -20;     // in front of the sky (-30) and sea (-27), behind everything you play on
    // What the deck camera can see (matches Level_1_Deck: x from -13.3 to 66.3, y from -7 to 11; it never moves up or down)
    const float ViewLeft = -13.4f, ViewRight = 66.4f, ViewBottom = -7f, ViewTop = 11f;
    const float Margin = 0.5f;         // a little extra so no edge ever shows
    static readonly string[] HideDecor = { "DeckDecor", "OceanArt" }; // the old background: the picture replaces all of it
    static readonly Color NightColor = new Color(0.03f, 0.02f, 0.05f); // fills the picture's empty (see-through) sky
    const float LightIntensity = 1f; // full brightness, so the picture looks like the drawing

    [MenuItem("Tools/Art/Hardy Arena Background")]
    static void SetUp()
    {
        if (!File.Exists(ArtPath))
        {
            EditorUtility.DisplayDialog("Hardy Arena Background", "Put the picture at " + ArtPath + " first.", "OK");
            return;
        }
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("Hardy Arena Background", "Level_1_Deck doesn't exist.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Sprite pic = Import(ArtPath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "HardyArenaBackground") Object.DestroyImmediate(r);

        // One picture: wide enough for the whole deck, and tall enough to cover the view above and below it
        float span = (ViewRight - ViewLeft) + Margin * 2f;
        float aspect = pic.bounds.size.x / pic.bounds.size.y;
        float height = Mathf.Max(span / aspect,
            Mathf.Max((DeckY - (ViewBottom - Margin)) / DeckLine, ((ViewTop + Margin) - DeckY) / (1f - DeckLine)));
        float scale = height / pic.bounds.size.y;
        float width = pic.bounds.size.x * scale;
        float centerY = DeckY - DeckLine * height + height * 0.5f; // its deck line on the real deck
        const int copies = 1;
        float startX = (ViewLeft + ViewRight) * 0.5f - width * 0.5f;

        var bg = new GameObject("HardyArenaBackground");

        // Dark night behind the picture, covering everything the camera can see
        var night = new GameObject("Night");
        night.transform.SetParent(bg.transform, false);
        night.transform.position = new Vector3((ViewLeft + ViewRight) * 0.5f, centerY, 0f);
        var nightSr = night.AddComponent<SpriteRenderer>();
        nightSr.sprite = Square();
        nightSr.color = NightColor;
        nightSr.sortingOrder = SortingOrder - 1;
        if (nightSr.sprite)
        {
            Vector2 s1 = nightSr.sprite.bounds.size;
            night.transform.localScale = new Vector3((copies * width + 4f) / s1.x, (height + 4f) / s1.y, 1f);
        }

        for (int i = 0; i < copies; i++)
        {
            var part = new GameObject("ArenaPicture_" + (i + 1));
            part.transform.SetParent(bg.transform, false);
            part.transform.position = new Vector3(startX + width * (i + 0.5f), centerY, 0f);
            part.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = part.AddComponent<SpriteRenderer>();
            sr.sprite = pic;
            sr.flipX = i % 2 == 1;
            sr.sortingOrder = SortingOrder;
        }

        // Full brightness, so the picture shows in its real colours
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (UnityEngine.Rendering.Universal.Light2D l in r.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
                if (l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global) l.intensity = LightIntensity;

        // Hide (don't delete) the old background, so only the picture shows
        string hidden = "";
        foreach (string path in HideDecor)
        {
            Transform t = Find(scene, path);
            if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden += "\n  " + path; }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Hardy Arena Background", "Done! The arena picture is behind Hardy's fight in Level_1_Deck." +
            (hidden.Length > 0 ? "\n\nHidden (the picture replaces it):" + hidden : ""), "OK");
    }

    static Sprite Square()
    {
        var tile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Square.asset");
        return tile ? tile.sprite : null;
    }

    static Transform Find(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == parts[0])
                return parts.Length == 1 ? r.transform : r.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
        return null;
    }

    static Sprite Import(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;               // ArenaBackdrop sizes it to the camera anyway
        importer.filterMode = FilterMode.Point;           // pixel art stays crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center; // ArenaBackdrop expects a centred picture
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}