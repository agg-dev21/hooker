using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Level 4 Boss > Make KK Harder.
// KK is the last boss, so he is the toughest: more health, two lines of waves per attack, faster waves,
// shorter rests and warnings, harder soaks, and every wave that hits knocks you back and slows you down.
// Change the numbers below and run it again any time.
public static class Level4Balance
{
    const int Health = 800;              // was 500 (Madame O 600, Hardy 400)
    const float Rest = 0.8f;             // was 1.5
    const float Glide = 9f;              // was 7
    const float WaveSpeed = 12f;         // was 9
    const float BetweenWaves = 0.35f;    // was 0.45
    const float Warn = 0.45f;            // was 0.6
    const int Lines = 2;                 // was 1: two lines of waves per attack
    const float BetweenLines = 0.9f;     // was 1.2
    const float Knockback = 12f;         // new: a wave hit pushes you along with it
    const float Slow = 0.5f;             // new: and you move at half speed...
    const float SlowTime = 1.5f;         // ...for this long
    static readonly int[] SoakDamage = { 8, 14, 20, 50 }; // was 5, 10, 15, 40

    [MenuItem("Tools/Level 4 Boss/Make KK Harder")]
    static void Apply()
    {
        const string path = "Assets/Scenes/Level_4_Boss.unity";
        if (!System.IO.File.Exists(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        FatherBoss kk = Object.FindAnyObjectByType<FatherBoss>(FindObjectsInactive.Include);
        if (!kk)
        {
            EditorUtility.DisplayDialog("Make KK Harder", "KK isn't in Level_4_Boss yet. Add him first (Tools > Rebuild Levels > Level 4 Boss: Add KK).", "OK");
            return;
        }

        if (kk.TryGetComponent(out Health h))
        {
            var hso = new SerializedObject(h);
            hso.FindProperty("maxHealth").intValue = Health;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }
        var so = new SerializedObject(kk);
        so.FindProperty("restTime").floatValue = Rest;
        so.FindProperty("glideSpeed").floatValue = Glide;
        so.FindProperty("waveSpeed").floatValue = WaveSpeed;
        so.FindProperty("timeBetweenWaves").floatValue = BetweenWaves;
        so.FindProperty("warnTime").floatValue = Warn;
        so.FindProperty("linesPerAttack").intValue = Lines;
        so.FindProperty("timeBetweenLines").floatValue = BetweenLines;
        so.FindProperty("waveKnockback").floatValue = Knockback;
        so.FindProperty("waveSlow").floatValue = Slow;
        so.FindProperty("waveSlowTime").floatValue = SlowTime;
        SerializedProperty soak = so.FindProperty("soakDamage");
        soak.arraySize = SoakDamage.Length;
        for (int i = 0; i < SoakDamage.Length; i++) soak.GetArrayElementAtIndex(i).intValue = SoakDamage[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = kk.gameObject;
        EditorUtility.DisplayDialog("Make KK Harder",
            "KK: " + Health + " health, two lines of faster waves per attack, shorter rests.\n" +
            "Soaks hit for " + string.Join(", ", SoakDamage) + ", and every wave knocks you back and slows you down.", "OK");
    }
}
