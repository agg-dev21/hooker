using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper. Adds Tools > Art > Health Bars.
// Imports the health bar pieces in Assets/Art/UI and fills Assets/Resources/HealthBarSkin.asset:
//   Raphael: hb_frame_player (blue, 812x184, window at 64,88 size 700x44) + hb_fill_player (red, 700x44)
//   Bosses:  hb_frame_boss (orange, 784x172, window at 40,72 size 700x44) + hb_fill_boss (pink, 700x44)
// Every health bar picks it up by itself when the game runs (HealthBar).
public static class HealthBarArtSetup
{
    const string Folder = "Assets/Art/UI";
    const string AssetPath = "Assets/Resources/HealthBarSkin.asset";

    [MenuItem("Tools/Art/Health Bars")]
    static void SetUp()
    {
        var skin = AssetDatabase.LoadAssetAtPath<HealthBarSkin>(AssetPath);
        if (!skin)
        {
            skin = ScriptableObject.CreateInstance<HealthBarSkin>();
            AssetDatabase.CreateAsset(skin, AssetPath);
        }
        skin.player = new HealthBarSkin.Look
        {
            frame = Import("hb_frame_player"), fill = Import("hb_fill_player"),
            framePixels = new Vector2(812, 184), windowPixels = new Rect(64, 88, 700, 44)
        };
        skin.boss = new HealthBarSkin.Look
        {
            frame = Import("hb_frame_boss"), fill = Import("hb_fill_boss"),
            framePixels = new Vector2(784, 172), windowPixels = new Rect(40, 72, 700, 44)
        };
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        Selection.activeObject = skin;
        EditorUtility.DisplayDialog("Health Bars",
            (skin.player.frame && skin.boss.frame ? "Done! " : "Some pictures are missing from " + Folder + ". ") +
            "Raphael's bar is blue with a red fill, the boss bars are orange with a pink fill.\n\n" +
            "To swap them, select Assets/Resources/HealthBarSkin.", "OK");
    }

    static Sprite Import(string name)
    {
        string path = Folder + "/" + name + ".png";
        if (!File.Exists(path)) return null;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point; // pixel art stays crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
