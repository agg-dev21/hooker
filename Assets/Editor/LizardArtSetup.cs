using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Adds Tools > Set Up Lizard Art.
// Puts the lizard drawing (Assets/Art/Lizard/lizard.png) on the lizard boss (PoisonBoss) in
// Level_2_Boss and in Level_3_Boss (where he fights next to the Chain Siren).
// His moves don't change; only his look. Run it again after changing the drawing.
public static class LizardArtSetup
{
    public const string ArtPath = "Assets/Art/Lizard/lizard.png";
    static readonly string[] Scenes = { "Assets/Scenes/Level_2_Boss.unity", "Assets/Scenes/Level_3_Boss.unity" };
    const float ArtWidthVsBody = 1.7f; // the drawing is wider than his hitbox (tail and head stick out)

    [MenuItem("Tools/Set Up Lizard Art")]
    static void SetUp()
    {
        if (!File.Exists(ArtPath))
        {
            EditorUtility.DisplayDialog("Lizard Art", "Put the drawing at " + ArtPath + " first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int done = 0;
        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (PoisonBoss boss in Object.FindObjectsByType<PoisonBoss>(FindObjectsInactive.Include))
            {
                ApplyArt(boss);
                done++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorUtility.DisplayDialog("Lizard Art", done > 0
            ? "Done! The lizard drawing is on " + done + " lizard boss(es)."
            : "No lizard boss found. Build Level_2_Boss / Level_3_Boss first.", "OK");
    }

    // Puts the drawing on one lizard boss (in the open scene). Returns false if the drawing is missing.
    public static bool ApplyArt(PoisonBoss boss)
    {
        if (!File.Exists(ArtPath)) return false;
        var body = boss.GetComponent<BoxCollider2D>();
        Vector3 bossScale = boss.transform.lossyScale;
        float bodyWidth = Mathf.Abs(bossScale.x) * body.size.x;
        float bodyHeight = Mathf.Abs(bossScale.y) * body.size.y;

        // Import the drawing as a sprite: pivot at the bottom middle (his feet), sized to his body
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
        int texWidth = tex ? tex.width : 1036;
        importer.spritePixelsPerUnit = texWidth / (bodyWidth * ArtWidthVsBody);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);

        // Child object that shows the drawing (replaces an old one)
        Transform old = boss.transform.Find("Art");
        if (old) Object.DestroyImmediate(old.gameObject);
        var art = new GameObject("Art");
        art.transform.SetParent(boss.transform, false);
        art.transform.localScale = new Vector3(1f / bossScale.x, 1f / bossScale.y, 1f); // undo the box's stretch
        art.transform.position = boss.transform.position + Vector3.down * (bodyHeight * 0.5f); // feet on the ground
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 6;

        foreach (Transform child in boss.transform)
            if (child.name == "Eye") child.gameObject.SetActive(false); // placeholder eyes from the box version

        var so = new SerializedObject(boss);
        so.FindProperty("art").objectReferenceValue = sr;
        so.FindProperty("artFacesRight").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}