using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Level 3 Boss > Make Harder.
// Tunes Madame O and Oscar in Level_3_Boss so the twin fight is tougher than Hardy's:
// more health, shorter rests, faster warnings, more damage, and they wait less for each other
// (so their attacks overlap more). Change the numbers below and run it again any time.
public static class Level3Balance
{
    // Madame O
    const int SirenHealth = 600;          // was 450 (Hardy has 400, but he is alone)
    const float SirenRest = 0.6f;         // was 1.2
    const float SirenTurnWait = 0.7f;     // was 2: waits less for Oscar to finish
    const float SirenPhase2At = 0.6f;     // was 0.5: angry sooner
    const float LashWarn = 0.6f;          // was 0.75
    const int LashDamage = 16;            // was 12
    const int RainChains = 6;             // was 4
    const float RainWarn = 0.65f;         // was 0.8
    const int RainDamage = 18;            // was 14
    const float GrabWarn = 0.45f;         // was 0.6
    const int GrabDamage = 14;            // was 10
    const int AnchorDamage = 22;          // was 16
    const int SirenTouchDamage = 14;      // was 10

    // Oscar
    const int OscarHealth = 320;          // was 220
    const float OscarRest = 0.8f;         // was 1.5
    const float OscarTurnWait = 0.7f;     // was 2
    const float OscarStartDelay = 2f;     // was 3.5: joins in sooner
    const int VomitDamage = 8;            // was 6 per glob
    const int OscarPoison = 4;            // was 3 per tick

    [MenuItem("Tools/Level 3 Boss/Make Harder")]
    static void Apply()
    {
        const string path = "Assets/Scenes/Level_3_Boss.unity";
        if (!System.IO.File.Exists(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        OctopusBoss siren = Object.FindAnyObjectByType<OctopusBoss>(FindObjectsInactive.Include);
        PoisonBoss oscar = Object.FindAnyObjectByType<PoisonBoss>(FindObjectsInactive.Include);
        if (siren)
        {
            SetHealth(siren.gameObject, SirenHealth);
            var so = new SerializedObject(siren);
            Set(so, "restTime", SirenRest);
            Set(so, "maxTurnWait", SirenTurnWait);
            Set(so, "phase2At", SirenPhase2At);
            Set(so, "lashWarnTime", LashWarn);
            Set(so, "lashDamage", LashDamage);
            Set(so, "rainChains", RainChains);
            Set(so, "rainWarnTime", RainWarn);
            Set(so, "rainDamage", RainDamage);
            Set(so, "grabWarnTime", GrabWarn);
            Set(so, "grabDamage", GrabDamage);
            Set(so, "anchorDamage", AnchorDamage);
            Set(so, "touchDamage", SirenTouchDamage);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (oscar)
        {
            SetHealth(oscar.gameObject, OscarHealth);
            var so = new SerializedObject(oscar);
            Set(so, "restTime", OscarRest);
            Set(so, "maxTurnWait", OscarTurnWait);
            Set(so, "startDelay", OscarStartDelay);
            Set(so, "vomitDamage", VomitDamage);
            Set(so, "poisonDamage", OscarPoison);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Make Level 3 Boss Harder",
            (siren ? "Madame O: " + SirenHealth + " health, faster and harder hitting.\n" : "Madame O not found.\n") +
            (oscar ? "Oscar: " + OscarHealth + " health, rests less, joins in sooner.\n" : "Oscar not found.\n") +
            "They wait less for each other, so their attacks overlap more.", "OK");
    }

    static void SetHealth(GameObject go, int value)
    {
        if (!go.TryGetComponent(out Health h)) return;
        var so = new SerializedObject(h);
        so.FindProperty("maxHealth").intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
        else p.floatValue = value;
    }
}
