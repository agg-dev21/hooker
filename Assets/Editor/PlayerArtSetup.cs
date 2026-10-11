using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Editor-only helper. Adds Tools > Set Up Player Art.
// Slices the main character's sheets in Assets/Art/Player (1600 x 1600 frames, feet near the bottom)
// and fills Assets/Resources/PlayerArtSet.asset. After that the player shows his drawings in every scene
// (PlayerArt adds itself), nothing else to set up.
public static class PlayerArtSetup
{
    const string ArtFolder = "Assets/Art/Player";
    const string AssetPath = "Assets/Resources/PlayerArtSet.asset";
    const int Frame = 1600;
    // About 460 pixels per tile: the drawn character (about 1040 px) is about 2.3 tiles tall (his hit box is 2)
    const float PixelsPerUnit = 460f;
    static readonly Vector2 Pivot = new Vector2(0.45f, 296f / 1600f); // between his feet

    [MenuItem("Tools/Set Up Player Art")]
    static void SetUp()
    {
        Sprite[] run = Slice("player_run");
        Sprite[] jump = Slice("player_jump");
        Sprite[] dash = Slice("player_dash");
        Sprite[] attack = Slice("player_attack");
        Sprite[] hook = Slice("player_hook");
        if (run.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Up Player Art", "Could not find " + ArtFolder + "/player_run.png.", "OK");
            return;
        }

        var set = AssetDatabase.LoadAssetAtPath<PlayerArtSet>(AssetPath);
        if (!set)
        {
            set = ScriptableObject.CreateInstance<PlayerArtSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
        }
        set.run = run;
        set.jump = jump;
        set.dash = dash;
        set.attack = attack;
        set.hook = hook;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        Selection.activeObject = set;
        EditorUtility.DisplayDialog("Set Up Player Art",
            "Done! The player now uses his drawings in every scene.\n\n" +
            "Run " + run.Length + ", Jump " + jump.Length + ", Dash " + dash.Length + ", Attack " + attack.Length + ", Hook " + hook.Length + " frames.\n\n" +
            "Select Assets/Resources/PlayerArtSet to change the animation speeds.", "OK");
    }

    // Cuts a horizontal strip (ArtFolder/name.png) into 1600 x 1600 frames, left to right. Empty if missing.
    static Sprite[] Slice(string name)
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
        importer.maxTextureSize = 16384;
        importer.SaveAndReimport();

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int count = Mathf.Max(1, width / Frame);

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
                rect = new Rect(i * Frame, height - Frame, Frame, Frame),
                alignment = SpriteAlignment.Custom,
                pivot = Pivot,
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
