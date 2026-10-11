using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Art > KK.
// Slices KK's sheets in Assets/Art/KK and puts them on the father (FatherBoss) in Level_4_Boss:
//   kk_idle    4 frames 1600x1600  standing (when not attacking)
//   kk_attack  5 frames 1600x1600  raising his arms to call the waves
//   kk_surf    4 frames  800x800   the water he floats on while he moves
//   kk_wave    4 frames  800x1200  the waves, small to tall (wave 1 = first drawing)
//   kk2_idle   4 frames 1600x1600  phase 2: the monster form
//   kk2_beam   5 frames 1600x400   phase 2: the blast, growing
// Run Tools > Rebuild Levels > Level 4 Boss: Add KK first. His hit box stays the same.
public static class KKArtSetup
{
    const string Folder = "Assets/Art/KK";
    const string BossScene = "Assets/Scenes/Level_4_Boss.unity";

    [MenuItem("Tools/Art/KK")]
    static void SetUp()
    {
        // About 4.5 tiles tall (he is drawn about 1420 px tall)
        Sprite[] idle = CompanionItemArtSetup.Slice(Folder, "kk_idle", 1600, 1600, new Vector2(0.5f, 40f / 1600f), 320f);
        Sprite[] attack = CompanionItemArtSetup.Slice(Folder, "kk_attack", 1600, 1600, new Vector2(0.5f, 72f / 1600f), 320f);
        Sprite[] surf = CompanionItemArtSetup.Slice(Folder, "kk_surf", 800, 800, new Vector2(0.5f, 0f), 260f);
        Sprite[] waves = CompanionItemArtSetup.Slice(Folder, "kk_wave", 800, 1200, new Vector2(0.5f, 0f), 250f); // tallest wave about 4.8 tiles
        // Phase 2: the monster form (about 4.4 tiles tall, feet near the bottom) and the blast (drawn pointing right)
        Sprite[] monster = CompanionItemArtSetup.Slice(Folder, "kk2_idle", 1600, 1600, new Vector2(0.5f, 324f / 1600f), 200f);
        Sprite[] blast = CompanionItemArtSetup.Slice(Folder, "kk2_beam", 1600, 400, new Vector2(0f, 0.5f), 100f);
        if (idle.Length == 0)
        {
            EditorUtility.DisplayDialog("Set Up KK Art", "Could not find " + Folder + "/kk_idle.png.", "OK");
            return;
        }
        if (!File.Exists(BossScene) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(BossScene, OpenSceneMode.Single);
        FatherBoss kk = Object.FindAnyObjectByType<FatherBoss>(FindObjectsInactive.Include);
        if (!kk)
        {
            EditorUtility.DisplayDialog("Set Up KK Art", "Sheets sliced, but KK isn't in Level_4_Boss yet. Run Tools > Rebuild Levels > Level 4 Boss: Add KK, then this again.", "OK");
            return;
        }

        // Hide the box look, add the drawing and the surf under his feet
        if (kk.TryGetComponent(out SpriteRenderer box)) box.enabled = false;
        foreach (string old in new[] { "Art", "Surf" })
        {
            Transform t = kk.transform.Find(old);
            if (t) Object.DestroyImmediate(t.gameObject);
        }
        var body = kk.GetComponent<BoxCollider2D>();
        Vector3 s = kk.transform.lossyScale;
        Vector3 feet = new Vector3(body.offset.x, body.offset.y - body.size.y * 0.5f, 0f);

        var art = new GameObject("Art");
        art.transform.SetParent(kk.transform, false);
        art.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
        art.transform.localPosition = feet;
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        sr.sortingOrder = 5;

        var surfGo = new GameObject("Surf");
        surfGo.transform.SetParent(kk.transform, false);
        surfGo.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
        surfGo.transform.localPosition = feet + new Vector3(0f, -0.2f / s.y, 0f);
        var surfSr = surfGo.AddComponent<SpriteRenderer>();
        surfSr.sprite = surf.Length > 0 ? surf[0] : null;
        surfSr.sortingOrder = 6;
        surfSr.enabled = false;

        var so = new SerializedObject(kk);
        so.FindProperty("artRenderer").objectReferenceValue = sr;
        so.FindProperty("surfRenderer").objectReferenceValue = surfSr;
        SetArray(so.FindProperty("idleFrames"), idle);
        SetArray(so.FindProperty("attackFrames"), attack);
        SetArray(so.FindProperty("surfFrames"), surf);
        SetArray(so.FindProperty("waveFrames"), waves);
        SetArray(so.FindProperty("phase2Frames"), monster);
        SetArray(so.FindProperty("blastFrames"), blast);
        so.FindProperty("waveArtScale").floatValue = 1f;
        so.FindProperty("waveWidth").floatValue = 2.4f; // hit box about as wide as the drawn water
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = kk.gameObject;
        EditorUtility.DisplayDialog("Set Up KK Art",
            "Done! KK uses his drawings: idle " + idle.Length + ", attack " + attack.Length + ", surf " + surf.Length + ", waves " + waves.Length + ", monster " + monster.Length + ", blast " + blast.Length + " frames.", "OK");
    }

    static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
