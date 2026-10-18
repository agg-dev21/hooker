using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Other > Level 3 Boss > Make Harder.
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

    // Oscar: everything he has in Level 2, about 20% stronger, in both phases
    const int OscarHealth = 460;          // Level 2: 380
    const float OscarRest = 0.5f;         // Level 2: 0.6
    const float OscarTurnWait = 0.7f;     // waits less for Madame O
    const float OscarStartDelay = 2f;     // joins in soon after the fight starts
    const float ChargeSpeed = 25f;        // Level 2: 22
    const float ChargeWarn = 0.42f;       // Level 2: 0.5
    const int DashHitDamage = 26;         // Level 2: 22
    const float CrashStun = 0.65f;        // Level 2: 0.8 (shorter = less time to hit him)
    const int CrashDrops = 16;            // Level 2: 14
    const int CrashPuddles = 12;          // Level 2: 10
    const int VomitGlobs = 11;            // Level 2: 9
    const float VomitSpeed = 14f;         // Level 2: 13
    const float VomitWarn = 0.3f;         // Level 2: 0.35
    const int VomitDamage = 8;            // Level 2: 6 per glob
    const int OscarPoison = 4;            // Level 2: 3 per tick
    const float PuddleLife = 5.5f;        // Level 2: 5
    const int OscarTouchDamage = 12;      // Level 2: 10
    // Oscar's phase 2
    const float OscarPhase2At = 0.6f;     // same as Level 2
    const float OscarPhase2Speed = 1.65f; // Level 2: 1.5
    const int Phase2Puddles = 4;          // Level 2: 3 per wave
    const int Phase2Drops = 4;            // Level 2: 3 per wave
    const float Phase2PoisonEvery = 0.85f; // Level 2: 1 second

    [MenuItem("Tools/Other/Level 3 Boss/Make Harder", priority = 3000)]
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
            Set(so, "chargeSpeed", ChargeSpeed);
            Set(so, "chargeWarnTime", ChargeWarn);
            Set(so, "dashHitDamage", DashHitDamage);
            Set(so, "crashStunTime", CrashStun);
            Set(so, "crashDrops", CrashDrops);
            Set(so, "crashPuddles", CrashPuddles);
            Set(so, "vomitGlobs", VomitGlobs);
            Set(so, "vomitSpeed", VomitSpeed);
            Set(so, "vomitWarnTime", VomitWarn);
            Set(so, "vomitDamage", VomitDamage);
            Set(so, "poisonDamage", OscarPoison);
            Set(so, "puddleLifetime", PuddleLife);
            Set(so, "touchDamage", OscarTouchDamage);
            Set(so, "phase2At", OscarPhase2At);
            Set(so, "phase2SpeedUp", OscarPhase2Speed);
            Set(so, "phase2PuddlesPerWave", Phase2Puddles);
            Set(so, "phase2DropsPerWave", Phase2Drops);
            Set(so, "phase2PoisonInterval", Phase2PoisonEvery);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Make Level 3 Boss Harder",
            (siren ? "Madame O: " + SirenHealth + " health, faster and harder hitting.\n" : "Madame O not found.\n") +
            (oscar ? "Oscar: " + OscarHealth + " health, all his Level 2 moves about 20% stronger in both phases.\n" : "Oscar not found.\n") +
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
