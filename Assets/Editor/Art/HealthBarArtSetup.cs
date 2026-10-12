using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper. Adds Tools > Art > Health Bars.
// Imports the health bar pieces in Assets/Art/UI and fills Assets/Resources/HealthBarSkin.asset:
//   Raphael: hb_frame_player (blue, 812x184, window at 64,88 size 700x44) + hb_fill_player (red, 700x44)
//   Bosses:  hb_frame_boss (orange, 784x172, window at 40,72 size 700x44) + hb_fill_boss (pink, 700x44)
//   Each boss also has its own bar (hb_boss_*.png = the empty bar, hb_boss_*_fill.png = its red fill, cut from the
//   artist's filled version so it matches the drawing exactly):
//     Hardy         hb_boss_hardy         purple
//     Oscar         hb_boss_oscar         green   (Level 2 and Level 3)
//     Madame O      hb_boss_oscar_madame  purple
//     KK            hb_boss_kk            dark red
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
        // Window = the see-through slot in each frame (pixels from the frame's top-left corner)
        skin.hardy = BossLook("hb_boss_hardy", new Vector2(1224, 232), new Rect(120, 120, 1056, 24), HardyColor);
        skin.oscar = BossLook("hb_boss_oscar", new Vector2(1224, 192), new Rect(120, 128, 1056, 24), OscarColor);
        skin.oscarLevel3 = skin.oscar;
        skin.madameO = BossLook("hb_boss_oscar_madame", new Vector2(1224, 264), new Rect(224, 184, 856, 24), MadameOColor);
        skin.kk = BossLook("hb_boss_kk", new Vector2(1128, 136), new Rect(24, 96, 1056, 24), KKColor);
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        Selection.activeObject = skin;
        EditorUtility.DisplayDialog("Health Bars",
            (skin.player.frame && skin.boss.frame ? "Done! " : "Some pictures are missing from " + Folder + ". ") +
            "Raphael's bar is blue with a red fill. Hardy, Oscar, Madame O and KK each have their own bar " +
            "(any other boss gets the orange one).\n\n" +
            "To swap them, select Assets/Resources/HealthBarSkin.", "OK");
    }

    static readonly Color HardyColor = new Color(0.42f, 0.38f, 0.72f);
    static readonly Color OscarColor = new Color(0.48f, 0.70f, 0.31f);
    static readonly Color MadameOColor = new Color(0.58f, 0.50f, 0.88f);
    static readonly Color KKColor = new Color(0.66f, 0.16f, 0.16f);

    // The empty bar behind, its fill picture on top (the empty slot shows as health goes down).
    // No fill picture: a plain fill in this colour instead.
    static HealthBarSkin.Look BossLook(string name, Vector2 size, Rect window, Color color)
    {
        Sprite fill = Import(name + "_fill");
        return new HealthBarSkin.Look
        {
            frame = Import(name), fill = fill, fillColor = fill ? Color.white : color, fillOnTop = fill,
            framePixels = size, windowPixels = window
        };
    }

    static Sprite Import(string name)
    {
        string path = Folder + "/" + name + ".png";
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // a picture just copied in may not be imported yet
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
