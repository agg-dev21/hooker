using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper. Adds Tools > Art > Planks.
// Imports the plank drawings in Assets/Art/Planks and fills Assets/Resources/PlankArt.asset:
//   wood:   plank_wood (floors), plank_wood_short (platforms, its ends stay)
//   grassy: plank_grass (floors), plank_grass_short (platforms), post_grass (posts)
//   metal:  plank_metal (floors and platforms), post_metal (posts)
// Every level puts them on by itself when the game runs (PlankDressing). Which level gets which set: see PlankArt.
public static class PlankArtSetup
{
    const string Folder = "Assets/Art/Planks";
    const string AssetPath = "Assets/Resources/PlankArt.asset";
    const float PixelsPerUnit = 256f; // a 512 pixel plank = 2 tiles long

    static readonly Vector2 TopMiddle = new Vector2(0.5f, 1f);

    [MenuItem("Tools/Art/Planks")]
    static void Run()
    {
        var art = AssetDatabase.LoadAssetAtPath<PlankArt>(AssetPath);
        if (!art)
        {
            art = ScriptableObject.CreateInstance<PlankArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        // Borders (left, bottom, right, top in pixels) stay as they are; only the middle repeats
        art.wood = new PlankArt.Set
        {
            floor = Import("plank_wood", Vector4.zero),
            platform = Import("plank_wood_short", new Vector4(40, 0, 40, 0)),
        };
        art.grassy = new PlankArt.Set
        {
            floor = Import("plank_grass", new Vector4(72, 0, 72, 0)),
            platform = Import("plank_grass_short", new Vector4(72, 0, 72, 0)),
            post = Import("post_grass", new Vector4(0, 60, 0, 150)), // vines on top stay, the trunk repeats
        };
        art.metal = new PlankArt.Set
        {
            floor = Import("plank_metal", Vector4.zero),
            platform = Import("plank_metal", Vector4.zero),
            post = Import("post_metal", Vector4.zero),
        };
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Selection.activeObject = art;

        bool all = art.wood.floor && art.grassy.floor && art.metal.floor;
        EditorUtility.DisplayDialog("Planks",
            (all ? "Done! " : "Some pictures are missing from " + Folder + ". ") +
            "Tutorial and Level 1 get wood, Level 2 grassy wood, Level 3 metal, Level 4 wood.\n" +
            "They go along the top of every floor and on every jump-through platform, with posts under floating platforms.\n\n" +
            "To change them, or turn them off, select Assets/Resources/PlankArt.", "OK");
    }

    static Sprite Import(string name, Vector4 border)
    {
        string path = Folder + "/" + name + ".png";
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // a picture just copied in may not be imported yet
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spriteMeshType = SpriteMeshType.FullRect; // needed to repeat it
        importer.SetTextureSettings(settings);
        importer.spritePivot = TopMiddle;
        importer.spriteBorder = border;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Point; // pixel art stays crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
