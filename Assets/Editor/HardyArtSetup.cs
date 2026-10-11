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
// 2) Puts the frames on Hardy (in Level_1_Deck) and sizes him and his attacks to match the art.
// Sheets (all frames 400 px tall):
//   hardy_walking     5 frames 400x400  walk / idle
//   hardy_attack      8 frames 400x400  Spikes pose
//   hardy_vertical   12 frames 200x400  Spike effect (grows up from the ground)
//   hardy_attack1     5 frames 400x400  Tentacle (horizontal) pose
//   hardy_horizontal 12 frames 400x200  Horizontal attack effect (grows out to the left)
//   hardy_punch       5 frames 400x400  Punch
//   hardy_jump_slam   8 frames 400x400  Leap + roll: wind-up (0-3), spin (4-6, loops in the air and while rolling), landing (7)
public static class HardyArtSetup
{
    const string ArtFolder = "Assets/Art/Hardy";
    const string DeckScene = "Assets/Scenes/Level_1_Deck.unity";
    const string SquareTile = "Assets/Tiles/Square.asset";
    const float PixelsPerUnit = 60f; // a 400 px frame is about 6.7 tiles tall: Hardy's body is about 6 tiles

    [MenuItem("Tools/Set Up Hardy Art")]
    static void SetUp()
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        Sprite[] walk = Slice("hardy_walking", 400, 400, center);
        Sprite[] spikesPose = Slice("hardy_attack", 400, 400, center);
        Sprite[] spikeFx = Slice("hardy_vertical", 200, 400, new Vector2(0.5f, 0f)); // pivot at the bottom: grows from the ground
        Sprite[] tentaclePose = Slice("hardy_attack1", 400, 400, center);
        Sprite[] tentacleFx = Slice("hardy_horizontal", 400, 200, new Vector2(1f, 0.5f)); // pivot at the right edge: grows to the left
        Sprite[] punch = Slice("hardy_punch", 400, 400, center);
        Sprite[] jumpSlam = Slice("hardy_jump_slam", 400, 400, center);

        if (walk.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Up Hardy Art", "Could not find " + ArtFolder + "/hardy_walking.png.", "OK");
            return;
        }
        if (!File.Exists(DeckScene))
        {
            EditorUtility.DisplayDialog("Set Up Hardy Art", "Sheets sliced. Level_1_Deck does not exist yet, so nothing was put on Hardy.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(DeckScene, OpenSceneMode.Single);
        HardyBoss boss = Object.FindAnyObjectByType<HardyBoss>();
        if (!boss)
        {
            EditorUtility.DisplayDialog("Set Up Hardy Art", "Sheets sliced, but Hardy is not in the deck. Run Tools > Add Hardy To Deck first, then this again.", "OK");
            return;
        }

        // Real drawing instead of the box: normal scale, collider fits his body (about 6 tiles tall)
        var sr = boss.GetComponent<SpriteRenderer>();
        sr.sprite = walk[0];
        sr.color = Color.white;
        boss.transform.localScale = Vector3.one;
        var col = boss.GetComponent<BoxCollider2D>();
        col.size = new Vector2(3.6f, 6f);
        col.offset = Vector2.zero;

        var so = new SerializedObject(boss);
        so.FindProperty("blockSprite").objectReferenceValue = (AssetDatabase.LoadAssetAtPath<Tile>(SquareTile))?.sprite;
        SetArray(so.FindProperty("walkFrames"), walk);
        SetArray(so.FindProperty("spikesPoseFrames"), spikesPose);
        SetArray(so.FindProperty("spikeFxFrames"), spikeFx);
        SetArray(so.FindProperty("tentaclePoseFrames"), tentaclePose);
        SetArray(so.FindProperty("tentacleFxFrames"), tentacleFx);
        SetArray(so.FindProperty("punchFrames"), punch);
        SetArray(so.FindProperty("jumpSlamFrames"), jumpSlam);

        // Attack sizes to match the bigger art (all can be changed on HardyBoss later)
        so.FindProperty("spikeWidth").floatValue = 2.1f;
        so.FindProperty("spikeHeight").floatValue = 5.7f;
        so.FindProperty("spikeSpacing").floatValue = 3f;
        so.FindProperty("tentacleHeight").floatValue = 2f;
        so.FindProperty("tentacleThickness").floatValue = 1.6f;
        so.FindProperty("walkStopDistance").floatValue = 7f;
        so.FindProperty("punchTriggerDistance").floatValue = 7f;
        so.FindProperty("punchRange").floatValue = 3.5f;
        so.FindProperty("punchHitHeight").floatValue = 5f;
        so.FindProperty("landingRadius").floatValue = 7f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SaveAbilityArt(tentacleFx);
        Selection.activeGameObject = boss.gameObject;
        EditorUtility.DisplayDialog("Set Up Hardy Art",
            "Done! Hardy is now about 6 tiles tall and uses the new art.\n\n" +
            "Walk " + walk.Length + ", Spikes pose " + spikesPose.Length + ", Spike effect " + spikeFx.Length +
            ", Tentacle pose " + tentaclePose.Length + ", Horizontal effect " + tentacleFx.Length + ", Punch " + punch.Length + ", Jump slam " + jumpSlam.Length + " frames.", "OK");
    }

    // Cuts a horizontal strip (ArtFolder/name.png) into equal frames and returns them in order, left to right.
    // Returns an empty list if the file is not there.
    static Sprite[] Slice(string name, int frameWidth, int frameHeight, Vector2 pivot)
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
        importer.maxTextureSize = 8192;
        importer.SaveAndReimport();

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int count = width / frameWidth;

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
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
    }

    // The player's Hardy's Spikes ability uses the same animation; keep it in Resources so every scene can load it
    static void SaveAbilityArt(Sprite[] spikes)
    {
        const string path = "Assets/Resources/AbilityArt.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var abilityArt = AssetDatabase.LoadAssetAtPath<AbilityArt>(path);
        if (!abilityArt)
        {
            abilityArt = ScriptableObject.CreateInstance<AbilityArt>();
            AssetDatabase.CreateAsset(abilityArt, path);
        }
        abilityArt.hardySpikesFrames = spikes;
        abilityArt.hardySpikesIcon = spikes.Length > 9 ? spikes[9] : spikes.Length > 0 ? spikes[spikes.Length - 1] : null; // spikes fully out
        EditorUtility.SetDirty(abilityArt);
        AssetDatabase.SaveAssets();
    }

    // Testing helpers for the ability unlock
    [MenuItem("Tools/Abilities/Unlock Hardy's Spikes (testing)")]
    static void UnlockForTesting()
    {
        PlayerPrefs.SetInt(PlayerAbility.HardySpikesSave, 1);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Abilities", "Hardy's Spikes unlocked. Press Q (or LB) in Play mode.", "OK");
    }

    [MenuItem("Tools/Abilities/Unlock Poison Hook (testing)")]
    static void UnlockPoisonHookForTesting()
    {
        PlayerPrefs.SetInt(PlayerUpgrades.PoisonHookSave, 1);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Abilities", "Poison Hook unlocked: hook hits now poison enemies.", "OK");
    }

    [MenuItem("Tools/Abilities/Lock All Abilities Again")]
    static void LockForTesting()
    {
        PlayerPrefs.DeleteKey(PlayerAbility.HardySpikesSave);
        PlayerPrefs.DeleteKey(PlayerAbility.HardySpikesSave + ".new");
        PlayerPrefs.DeleteKey(PlayerUpgrades.PoisonHookSave);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Abilities", "All abilities are locked again (like a new game).", "OK");
    }

    static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
