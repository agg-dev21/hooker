using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper. Adds Tools > Art > Ability Icons And Ground Effects.
//   Ability icons (Assets/Art/Abilities) go into Resources/AbilityArt:
//     icon_hardy = Hardy's spikes (Q), icon_chain = Madame O's chain (C), icon_ariel = Ariel's light beam (R),
//     icon_poison = Oscar's poison hook (always on, shown as a badge)
//   Ground effects (Assets/Art/GroundFx, 6 frames of 1600 x 1600 each) go into Resources/GroundFxArt:
//     fx_hardy_slam = Hardy's jump slam landing, fx_barrel_blast = the exploding barrels
// Everything picks them up by itself when the game runs.
public static class AbilityAndFxArtSetup
{
    const string IconFolder = "Assets/Art/Abilities";
    const string FxFolder = "Assets/Art/GroundFx";
    const string FxAssetPath = "Assets/Resources/GroundFxArt.asset";

    [MenuItem("Tools/Art/Ability Icons And Ground Effects")]
    static void Run()
    {
        // Icons
        var abilities = Resources.Load<AbilityArt>("AbilityArt");
        int icons = 0;
        if (abilities)
        {
            Sprite hardy = Icon("icon_hardy"), chain = Icon("icon_chain"), ariel = Icon("icon_ariel"), poison = Icon("icon_poison");
            if (hardy) { abilities.hardySpikesIcon = hardy; icons++; }
            if (chain) { abilities.chainIcon = chain; icons++; }
            if (ariel) { abilities.arielIcon = ariel; icons++; }
            if (poison) { abilities.poisonIcon = poison; icons++; }
            EditorUtility.SetDirty(abilities);
        }

        // Ground effects
        var fx = AssetDatabase.LoadAssetAtPath<GroundFxArt>(FxAssetPath);
        if (!fx)
        {
            fx = ScriptableObject.CreateInstance<GroundFxArt>();
            AssetDatabase.CreateAsset(fx, FxAssetPath);
        }
        AssetDatabase.ImportAsset(FxFolder + "/fx_hardy_slam.png"); // pictures just copied in may not be imported yet
        AssetDatabase.ImportAsset(FxFolder + "/fx_barrel_blast.png");
        fx.hardySlamFrames = CompanionItemArtSetup.Slice(FxFolder, "fx_hardy_slam", 1600, 1600, new Vector2(0.5f, 0f), 400f);
        fx.barrelBlastFrames = CompanionItemArtSetup.Slice(FxFolder, "fx_barrel_blast", 1600, 1600, new Vector2(0.5f, 0f), 400f);
        EditorUtility.SetDirty(fx);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Ability Icons And Ground Effects",
            (abilities ? icons + " of 4 ability icons set." : "Resources/AbilityArt is missing: run Tools > Art > Hardy first.") + "\n" +
            "Hardy's slam: " + fx.hardySlamFrames.Length + " frames, barrel blast: " + fx.barrelBlastFrames.Length + " frames.\n\n" +
            "Change them in Assets/Resources/AbilityArt and Assets/Resources/GroundFxArt.", "OK");
    }

    static Sprite Icon(string name)
    {
        string path = IconFolder + "/" + name + ".png";
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
