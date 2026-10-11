using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Set Up Madame O Art.
// Slices Madame O's sheets in Assets/Art/MadameO and puts them on her (OctopusBoss) in Level_3_Boss:
//   madameo_idle    7 frames 1500x1500  floating
//   madameo_attack  7 frames 1500x1500  throwing
//   madameo_bind    6 frames  800x800   chains bursting out of the ground (her holding attack)
//   madameo_chain   1 picture  800x800  one thrown chain
// The old placeholder drawing (ChainSiren_Look) is hidden. Her hit box stays the same.
public static class MadameOArtSetup
{
    const string Folder = "Assets/Art/MadameO";
    const string BossScene = "Assets/Scenes/Level_3_Boss.unity";

    [MenuItem("Tools/Set Up Madame O Art")]
    static void SetUp()
    {
        // About 5.8 tiles tall (she is drawn about 1015 px tall)
        Sprite[] idle = CompanionItemArtSetup.Slice(Folder, "madameo_idle", 1500, 1500, new Vector2(0.5f, 5f / 1500f), 175f);
        Sprite[] attack = CompanionItemArtSetup.Slice(Folder, "madameo_attack", 1500, 1500, new Vector2(0.5f, 5f / 1500f), 175f);
        Sprite[] bind = CompanionItemArtSetup.Slice(Folder, "madameo_bind", 800, 800, new Vector2(0.5f, 24f / 800f), 200f);
        Sprite chain = SingleSprite(Folder + "/madameo_chain.png", 300f);
        if (idle.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Up Madame O Art", "Could not find " + Folder + "/madameo_idle.png.", "OK");
            return;
        }
        if (!File.Exists(BossScene) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(BossScene, OpenSceneMode.Single);
        OctopusBoss siren = Object.FindAnyObjectByType<OctopusBoss>(FindObjectsInactive.Include);
        if (!siren)
        {
            EditorUtility.DisplayDialog("Set Up Madame O Art", "Sheets sliced, but Madame O isn't in Level_3_Boss.", "OK");
            return;
        }

        // Hide the placeholder look
        Transform root = siren.transform.parent;
        Transform look = root ? root.Find("ChainSiren_Look") : null;
        if (look) look.gameObject.SetActive(false);
        if (siren.TryGetComponent(out SpriteRenderer box)) box.enabled = false;

        Transform old = siren.transform.Find("Art");
        if (old) Object.DestroyImmediate(old.gameObject);
        var art = new GameObject("Art");
        art.transform.SetParent(siren.transform, false);
        Vector3 s = siren.transform.lossyScale;
        art.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
        var body = siren.GetComponent<BoxCollider2D>();
        art.transform.localPosition = body ? new Vector3(body.offset.x, body.offset.y - body.size.y * 0.5f, 0f) : new Vector3(0f, -0.5f, 0f);
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        sr.sortingOrder = 5;

        var so = new SerializedObject(siren);
        so.FindProperty("artRenderer").objectReferenceValue = sr;
        SetArray(so.FindProperty("idleFrames"), idle);
        SetArray(so.FindProperty("attackFrames"), attack);
        SetArray(so.FindProperty("bindFrames"), bind);
        so.FindProperty("chainSprite").objectReferenceValue = chain;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = siren.gameObject;
        EditorUtility.DisplayDialog("Set Up Madame O Art",
            "Done! Madame O uses her drawings: idle " + idle.Length + ", attack " + attack.Length + ", chain bind " + bind.Length + " frames.\n\n" +
            "If she faces the wrong way, tick or untick 'Art Faces Right' on her.", "OK");
    }

    static Sprite SingleSprite(string path, float pixelsPerUnit)
    {
        if (!File.Exists(path)) return null;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
