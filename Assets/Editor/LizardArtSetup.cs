using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Set Up Lizard Art.
// 1) Slices the lizard's sheets in Assets/Art/Lizard into frames (feet at the bottom of each frame).
// 2) Puts the drawing on the Level 2 and Level 3 bosses: a child "Art" shows the frames,
//    the old green box and its eyes are hidden. Their hit boxes stay the same.
// Sheets:
//   lizard_idle     1 frame  480x480  standing
//   lizard_dash     7 frames 560x560  wind-up (0-3), spinning roll (4-6)
//   lizard_spew    10 frames 560x560  wind-up (0-4), spewing (5-9)
//   lizard_poison   6 frames 320x320  splash, puddle, drips
public static class LizardArtSetup
{
    const string ArtFolder = "Assets/Art/Lizard";
    static readonly string[] BossScenes =
    {
        "Assets/Scenes/Level_2_Boss.unity",
        "Assets/Scenes/Level_3_Boss.unity"
    };
    // 60 pixels per tile, same as Hardy: the standing lizard (316 px) is about 5.3 tiles tall, 7 wide.
    // Same sharpness as Hardy. A smaller number makes him bigger but blurrier.
    const float PixelsPerUnit = 60f;
    // His hit box (world units), sized to fit the drawing. His feet stay on the floor (groundY).
    static readonly Vector2 BodySize = new Vector2(5.9f, 4.1f);

    [MenuItem("Tools/Set Up Lizard Art")]
    static void SetUp()
    {
        Sprite[] idle = Slice("lizard_idle", 480, 480, new Vector2(0.5f, 24f / 480f));
        Sprite[] dash = Slice("lizard_dash", 560, 560, new Vector2(0.5f, 64f / 560f));
        Sprite[] spew = Slice("lizard_spew", 560, 560, new Vector2(0.5f, 64f / 560f));
        Sprite[] poison = Slice("lizard_poison", 320, 320, new Vector2(0.5f, 0f));
        if (idle.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Up Lizard Art", "Could not find " + ArtFolder + "/lizard_idle.png.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int done = 0;
        foreach (string scenePath in BossScenes)
        {
            if (!File.Exists(scenePath)) continue;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (PoisonBoss boss in Object.FindObjectsByType<PoisonBoss>(FindObjectsInactive.Include))
            {
                ApplyArt(boss, idle[0], dash, spew, poison);
                done++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (done == 0)
        {
            EditorUtility.DisplayDialog("Set Up Lizard Art", "Sheets sliced, but no lizard boss was found in Level_2_Boss or Level_3_Boss.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Set Up Lizard Art",
            "Done! The lizard now uses his drawings on " + done + " boss(es).\n\nIdle " + idle.Length + ", Dash " + dash.Length + ", Spew " + spew.Length + ", Poison " + poison.Length + " frames.", "OK");
    }

    static void ApplyArt(PoisonBoss boss, Sprite idle, Sprite[] dash, Sprite[] spew, Sprite[] poison)
    {
        // Hit box to match the bigger drawing, feet on the floor
        boss.transform.localScale = new Vector3(BodySize.x, BodySize.y, 1f);
        var bossSo = new SerializedObject(boss);
        float groundY = bossSo.FindProperty("groundY").floatValue;
        boss.transform.position = new Vector3(boss.transform.position.x, groundY + BodySize.y * 0.5f, boss.transform.position.z);

        // Hide the old look: the green box and the eyes on it
        if (boss.TryGetComponent(out SpriteRenderer boxRenderer)) boxRenderer.enabled = false;
        foreach (Transform child in boss.transform)
            if (child.name == "Eye") child.gameObject.SetActive(false);

        // The drawing: a child with normal scale, standing on the bottom of his hit box
        Transform old = boss.transform.Find("Art");
        if (old) Object.DestroyImmediate(old.gameObject);
        var art = new GameObject("Art");
        art.transform.SetParent(boss.transform, false);
        var body = boss.GetComponent<BoxCollider2D>();
        Vector3 s = boss.transform.lossyScale;
        art.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
        art.transform.localPosition = new Vector3(body.offset.x, body.offset.y - body.size.y * 0.5f, 0f);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        sr.sortingOrder = 5;

        var so = new SerializedObject(boss);
        so.FindProperty("artRenderer").objectReferenceValue = sr;
        so.FindProperty("idleFrame").objectReferenceValue = idle[0];
        SetArray(so.FindProperty("dashFrames"), dash);
        SetArray(so.FindProperty("spewFrames"), spew);
        SetArray(so.FindProperty("poisonFrames"), poison);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Cuts a horizontal strip (ArtFolder/name.png) into equal frames, left to right. Empty if the file is missing.
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
            .OrderBy(sp => int.Parse(sp.name.Substring(sp.name.LastIndexOf('_') + 1)))
            .ToArray();
    }

    static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
