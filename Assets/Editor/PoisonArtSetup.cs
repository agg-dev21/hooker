using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Editor-only helper. Adds Tools > Art > Poison.
// Slices the poison sheets in Assets/Art/Poison and fills Assets/Resources/PoisonArt.asset,
// which every poison hazard in every level uses when the game runs.
// Sheets (cut from the lizard's poison sheet):
//   poison_drop     1 frame   39x58   a falling drop
//   poison_splash   2 frames 320x207  splash on the ground
//   poison_puddle   1 frame  320x90   puddle lying on the floor
//   poison_drips    2 frames 320x273  rising poison bits
public static class PoisonArtSetup
{
    const string ArtFolder = "Assets/Art/Poison";
    const string AssetPath = "Assets/Resources/PoisonArt.asset";
    const float PixelsPerUnit = 88f; // same as the lizard

    [MenuItem("Tools/Art/Poison")]
    static void SetUp()
    {
        Sprite[] drop = Slice("poison_drop", 39, new Vector2(0.5f, 0.5f));
        Sprite[] splash = Slice("poison_splash", 320, new Vector2(0.5f, 0f));
        Sprite[] puddle = Slice("poison_puddle", 320, new Vector2(0.5f, 0f));
        Sprite[] drips = Slice("poison_drips", 320, new Vector2(0.5f, 0.5f));
        if (drop.Length == 0 || splash.Length == 0 || puddle.Length == 0 || drips.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Up Poison Art", "Some sheets are missing from " + ArtFolder + ".", "OK");
            return;
        }

        var art = AssetDatabase.LoadAssetAtPath<PoisonArt>(AssetPath);
        if (!art)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            art = ScriptableObject.CreateInstance<PoisonArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        art.drop = drop[0];
        art.splash = splash;
        art.puddle = puddle[0];
        art.drips = drips;
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();

        Selection.activeObject = art;
        EditorGUIUtility.PingObject(art);
        EditorUtility.DisplayDialog("Set Up Poison Art",
            "Done! Every poison puddle, drop, glob, slime wall and poisoned character now uses the poison drawings.\n\n" +
            "Select Assets/Resources/PoisonArt to change sizes and speeds.", "OK");
    }

    // Cuts a horizontal strip into equal frames, left to right. Empty if the file is missing.
    static Sprite[] Slice(string name, int frameWidth, Vector2 pivot)
    {
        string path = ArtFolder + "/" + name + ".png";
        if (!File.Exists(path)) return new Sprite[0];

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        // Full rectangles so the drawings can repeat (Tiled) along puddles and walls
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int count = Mathf.Max(1, width / frameWidth);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int i = 0; i < count; i++)
        {
            rects.Add(new SpriteRect
            {
                name = name + "_" + i,
                rect = new Rect(i * frameWidth, 0, frameWidth, height),
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
            .OrderBy(sp => int.Parse(sp.name.Substring(sp.name.LastIndexOf('_') + 1)))
            .ToArray();
    }
}
