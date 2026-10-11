using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Editor-only helper. Adds Tools > Set Up Companion And Item Art.
// 1) Slices the companion's sheets in Assets/Art/Companion and puts them in Resources/CompanionSettings:
//      companion_idle    2 frames 800x800    floating
//      companion_appear  5 frames 800x800    popping in
//      companion_beam    8 frames 800x1600   the column of light striking down
// 2) Gives the items their icons (Assets/Art/Items) in Resources/ItemSettings. Change any icon there later.
public static class CompanionItemArtSetup
{
    const string CompanionFolder = "Assets/Art/Companion";
    const string ItemFolder = "Assets/Art/Items";

    // Which picture each item uses (the order of ItemType). Empty = keeps its coloured square.
    static readonly (ItemType type, string file)[] ItemIcons =
    {
        (ItemType.Shield, "item_shield"),
        (ItemType.PoisonResistance, "item_ring_green"),
        (ItemType.Healing, "item_scroll_red"),
        (ItemType.InstantRevival, "item_scroll_yellow"),
        (ItemType.Flying, "item_wings"),
        (ItemType.Scroll, "item_scroll_blue"),
        (ItemType.Invulnerability, "item_ring_blue"),
    };

    [MenuItem("Tools/Set Up Companion And Item Art")]
    static void SetUp()
    {
        // Companion: about 1.8 tiles tall (her drawing is about 750 px), the light column 5 x 10 tiles
        Sprite[] idle = Slice(CompanionFolder, "companion_idle", 800, 800, new Vector2(0.5f, 0.5f), 420f);
        Sprite[] appear = Slice(CompanionFolder, "companion_appear", 800, 800, new Vector2(0.5f, 0.5f), 420f);
        Sprite[] beam = Slice(CompanionFolder, "companion_beam", 800, 1600, new Vector2(0.5f, 0f), 160f);

        var companion = Resources.Load<CompanionSettings>("CompanionSettings");
        if (companion)
        {
            companion.idleFrames = idle;
            companion.appearFrames = appear;
            companion.beamFrames = beam;
            EditorUtility.SetDirty(companion);
        }

        // Items
        var items = Resources.Load<ItemSettings>("ItemSettings");
        int icons = 0;
        if (items)
        {
            var so = new SerializedObject(items);
            SerializedProperty looks = so.FindProperty("looks");
            foreach (var (type, file) in ItemIcons)
            {
                Sprite icon = SingleSprite(ItemFolder + "/" + file + ".png");
                int i = (int)type;
                if (!icon || i >= looks.arraySize) continue;
                looks.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = icon;
                icons++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Companion And Item Art",
            "Companion: idle " + idle.Length + ", appear " + appear.Length + ", light strike " + beam.Length + " frames" +
            (companion ? "" : "\n(Resources/CompanionSettings is missing!)") +
            "\nItems: " + icons + " icons set (Speed Boost keeps its coloured square).\n\n" +
            "Change any item's icon in Assets/Resources/ItemSettings (Looks).", "OK");
    }

    static Sprite SingleSprite(string path)
    {
        if (!File.Exists(path)) return null;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 400f;
        importer.filterMode = FilterMode.Point; // pixel art icons stay crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Cuts a horizontal strip into equal frames, left to right. Empty if the file is missing.
    public static Sprite[] Slice(string folder, string name, int frameWidth, int frameHeight, Vector2 pivot, float pixelsPerUnit)
    {
        string path = folder + "/" + name + ".png";
        if (!File.Exists(path)) return new Sprite[0];

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 16384;
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
            .OrderBy(sp => int.Parse(sp.name.Substring(sp.name.LastIndexOf('_') + 1)))
            .ToArray();
    }
}
