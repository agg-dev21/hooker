using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper. Adds Tools > Art > Hook Points.
// Imports the hook ring drawings in Assets/Art/HookPoints and fills Assets/Resources/HookPointArt.asset:
//   hook_point_1 = tutorial and Level 1 (Hardy), hook_point_2 = Level 2 (Oscar),
//   hook_point_3 = Level 3 (Madame O),          hook_point_4 = Level 4 (KK).
// Each picture's pivot is the middle of the ring's hole, so the hook grabs right there.
// Every hook ring in every scene uses them by itself when the game runs.
public static class HookPointArtSetup
{
    const string Folder = "Assets/Art/HookPoints";
    const string AssetPath = "Assets/Resources/HookPointArt.asset";
    const float PixelsPerUnit = 160f;

    // Middle of the ring's hole in each 512 x 512 picture (from the bottom-left, 0 to 1)
    static readonly Vector2 RingPivot = new Vector2(0.5f, 0.8f);
    static readonly Vector2 KKRingPivot = new Vector2(0.49f, 0.875f);

    [MenuItem("Tools/Art/Hook Points")]
    static void Run()
    {
        var art = AssetDatabase.LoadAssetAtPath<HookPointArt>(AssetPath);
        if (!art)
        {
            art = ScriptableObject.CreateInstance<HookPointArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        art.tutorialAndLevel1 = Import("hook_point_1", RingPivot);
        art.level2 = Import("hook_point_2", RingPivot);
        art.level3 = Import("hook_point_3", RingPivot);
        art.level4 = Import("hook_point_4", KKRingPivot);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Selection.activeObject = art;

        int found = (art.tutorialAndLevel1 ? 1 : 0) + (art.level2 ? 1 : 0) + (art.level3 ? 1 : 0) + (art.level4 ? 1 : 0);
        EditorUtility.DisplayDialog("Hook Points",
            found + " of 4 hook drawings set.\n\n" +
            "Tutorial + Level 1, Level 2, Level 3 and Level 4 each have their own. Every hook ring uses them by itself.\n" +
            "To change one, or the size (Draw Scale), select Assets/Resources/HookPointArt.", "OK");
    }

    static Sprite Import(string name, Vector2 pivot)
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
        importer.SetTextureSettings(settings);
        importer.spritePivot = pivot;
        importer.filterMode = FilterMode.Point; // pixel art stays crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
