using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only helper. Adds Tools > Set Up Hardy Art.
// 1) Slices Hardy's sprite sheets in Assets/Art/Hardy into frames.
// 2) Puts the frames on Hardy (in Level_1_Deck): walking/idle, the Spikes pose, the spike effect,
//    and the tentacle effect (if its sheet is there). The tentacle pose is not drawn yet.
public static class HardyArtSetup
{
    const string ArtFolder = "Assets/Art/Hardy";
    const string DeckScene = "Assets/Scenes/Level_1_Deck.unity";
    const string SquareTile = "Assets/Tiles/Square.asset";
    const float PixelsPerUnit = 45f; // a 200 px tall frame is about 4.4 tiles tall, Hardy's body is about 4

    [MenuItem("Tools/Set Up Hardy Art")]
    static void SetUp()
    {
        string walkPath = FindSheet("walk");
        string posePath = FindSheet("boss attack");
        string spikePath = FindSheet("vertical");
        if (walkPath == null || posePath == null || spikePath == null)
        {
            EditorUtility.DisplayDialog("Set Up Hardy Art",
                "Could not find all three sheets in " + ArtFolder + ".\n\nNeeded: a file with 'walk', one with 'boss attack', and one with 'vertical' in its name.", "OK");
            return;
        }

        Sprite[] walk = Slice(walkPath, 200, 200, new Vector2(0.5f, 0.5f));
        Sprite[] pose = Slice(posePath, 200, 200, new Vector2(0.5f, 0.5f));
        Sprite[] spike = Slice(spikePath, 100, 200, new Vector2(0.5f, 0f)); // pivot at the bottom: spikes grow from the ground
        string tentaclePath = FindSheet("horizontal");
        // pivot at the right edge, middle: the tentacle grows out of that point to the left
        Sprite[] tentacle = tentaclePath != null ? Slice(tentaclePath, 400, 200, new Vector2(1f, 0.5f)) : new Sprite[0];

        if (!File.Exists(DeckScene))
        {
            EditorUtility.DisplayDialog("Set Up Hardy Art", "Sheets sliced. Level_1_Deck does not exist yet, so nothing was put on Hardy.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(DeckScene, OpenSceneMode.Single);
        HardyBoss boss = Object.FindFirstObjectByType<HardyBoss>();
        if (!boss)
        {
            EditorUtility.DisplayDialog("Set Up Hardy Art", "Sheets sliced, but Hardy is not in the deck. Run Tools > Add Hardy To Deck first, then this again.", "OK");
            return;
        }

        // Real drawing instead of the box: normal scale, collider fits his body
        var sr = boss.GetComponent<SpriteRenderer>();
        sr.sprite = walk[0];
        sr.color = Color.white;
        Vector3 pos = boss.transform.position;
        boss.transform.localScale = Vector3.one;
        var col = boss.GetComponent<BoxCollider2D>();
        col.size = new Vector2(2.4f, 4f);
        col.offset = Vector2.zero;
        boss.transform.position = new Vector3(pos.x, pos.y, pos.z);

        var so = new SerializedObject(boss);
        so.FindProperty("blockSprite").objectReferenceValue = (AssetDatabase.LoadAssetAtPath<Tile>(SquareTile))?.sprite;
        SetArray(so.FindProperty("walkFrames"), walk);
        SetArray(so.FindProperty("spikesPoseFrames"), pose);
        SetArray(so.FindProperty("spikeFxFrames"), spike);
        SetArray(so.FindProperty("tentacleFxFrames"), tentacle);
        so.FindProperty("spikeWidth").floatValue = 1.4f;  // matches the drawn spike
        so.FindProperty("spikeHeight").floatValue = 3.8f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = boss.gameObject;
        EditorUtility.DisplayDialog("Set Up Hardy Art",
            "Done! Hardy now walks with his walking frames and uses the art for the Spikes and Tentacle attacks.\n\n" +
            "Walk " + walk.Length + " frames, Spikes pose " + pose.Length + " frames, Spike effect " + spike.Length + " frames, Tentacle effect " + tentacle.Length + " frames.", "OK");
    }

    // First image in the art folder whose name contains the keyword (case does not matter)
    static string FindSheet(string keyword)
    {
        if (!Directory.Exists(ArtFolder)) return null;
        return Directory.GetFiles(ArtFolder, "*.png")
            .Select(p => p.Replace('\\', '/'))
            .FirstOrDefault(p => Path.GetFileName(p).ToLower().Contains(keyword));
    }

    // Cuts a horizontal strip into equal frames and returns them in order, left to right
    static Sprite[] Slice(string path, int frameWidth, int frameHeight, Vector2 pivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 8192;
        importer.SaveAndReimport();

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int count = width / frameWidth;
        string baseName = Path.GetFileNameWithoutExtension(path).Replace(' ', '_');

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int i = 0; i < count; i++)
        {
            rects.Add(new SpriteRect
            {
                name = baseName + "_" + i,
                rect = new Rect(i * frameWidth, height - frameHeight, frameWidth, frameHeight),
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                spriteID = GUID.Generate()
            });
        }
        provider.SetSpriteRects(rects.ToArray());
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
    }

    static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
