using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Other > Balance > Even Out Hardy And Oscar.
// Either of them can be the first boss (50 / 50), so they should feel about as hard as each other.
// Oscar (Level_2_Boss) was too hard: he hits less, makes less poison, rests longer and is open longer after a crash.
// Hardy (Level_1_Deck) was too easy: a bit more health, hits a bit harder and rests less.
// Oscar in Level 3 is not changed (Tools > Other > Level 3 Boss > Make Harder sets him there).
// Change the numbers below and run it again any time.
public static class HardyOscarBalance
{
    // Oscar, Level 2 (old value after each)
    const int OscarHealth = 320;          // 380
    const float OscarRest = 0.85f;        // 0.6: more time to hit him between attacks
    const float ChargeWarn = 0.6f;        // 0.5: more time to see the dash coming
    const int DashHitDamage = 16;         // 22
    const float CrashStun = 1.3f;         // 0.8: dodge his dash and he stays dizzy longer
    const int CrashPuddles = 3;           // 10: leaves safe floor to stand on
    const int CrashDrops = 5;             // 14
    const int VomitGlobs = 6;             // 9
    const int VomitDamage = 5;            // 6 per glob
    const int OscarPoison = 2;            // 3 per tick
    const float PoisonTime = 2.5f;        // 3 seconds
    const int OscarTouchDamage = 8;       // 10
    const float OscarPhase2At = 0.5f;     // 0.6: angry later
    const float OscarPhase2Speed = 1.3f;  // 1.5
    const float Phase2PoisonEvery = 2.5f; // 1 second
    const int Phase2Puddles = 1;          // 3 per wave
    const int Phase2Drops = 2;            // 3 per wave
    const float PuddleLife = 3f;          // 5 seconds: puddles go away sooner
    const float VomitPuddleWidth = 1.8f;  // 2.5
    const float Phase2TrailGap = 7f;      // 2.5: his phase 2 dash used to cover the whole floor in poison

    // Hardy, Level 1 (old value after each)
    const int HardyHealth = 440;          // 400
    const float HardyRest = 0.4f;         // 0.5
    const int TentacleBody = 8;           // 6
    const int TentacleTip = 12;           // 10
    const int LandingDamage = 15;         // 12
    const int SpikeDamage = 10;           // 8
    const int PunchDamage = 10;           // 8
    const float HardyPhase2Speed = 1.4f;  // 1.3
    const float RandomSpikeEvery = 0.75f; // 0.9 (phase 2)

    [MenuItem("Tools/Other/Balance/Even Out Hardy And Oscar", priority = 3000)]
    static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        bool oscar = Tune("Assets/Scenes/Level_2_Boss.unity", () =>
        {
            PoisonBoss b = Object.FindAnyObjectByType<PoisonBoss>(FindObjectsInactive.Include);
            if (!b) return false;
            SetHealth(b.gameObject, OscarHealth);
            var so = new SerializedObject(b);
            Set(so, "restTime", OscarRest);
            Set(so, "chargeWarnTime", ChargeWarn);
            Set(so, "dashHitDamage", DashHitDamage);
            Set(so, "crashStunTime", CrashStun);
            Set(so, "crashPuddles", CrashPuddles);
            Set(so, "crashDrops", CrashDrops);
            Set(so, "vomitGlobs", VomitGlobs);
            Set(so, "vomitDamage", VomitDamage);
            Set(so, "poisonDamage", OscarPoison);
            Set(so, "poisonDuration", PoisonTime);
            Set(so, "touchDamage", OscarTouchDamage);
            Set(so, "phase2At", OscarPhase2At);
            Set(so, "phase2SpeedUp", OscarPhase2Speed);
            Set(so, "phase2PoisonInterval", Phase2PoisonEvery);
            Set(so, "phase2PuddlesPerWave", Phase2Puddles);
            Set(so, "phase2DropsPerWave", Phase2Drops);
            Set(so, "puddleLifetime", PuddleLife);
            Set(so, "vomitPuddleWidth", VomitPuddleWidth);
            Set(so, "phase2TrailGap", Phase2TrailGap);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        });
        bool hardy = Tune("Assets/Scenes/Level_1_Deck.unity", () =>
        {
            HardyBoss b = Object.FindAnyObjectByType<HardyBoss>(FindObjectsInactive.Include);
            if (!b) return false;
            SetHealth(b.gameObject, HardyHealth);
            var so = new SerializedObject(b);
            Set(so, "restTime", HardyRest);
            Set(so, "tentacleBodyDamage", TentacleBody);
            Set(so, "tentacleTipDamage", TentacleTip);
            Set(so, "landingDamage", LandingDamage);
            Set(so, "spikeDamage", SpikeDamage);
            Set(so, "punchDamage", PunchDamage);
            Set(so, "phase2SpeedMultiplier", HardyPhase2Speed);
            Set(so, "randomSpikeInterval", RandomSpikeEvery);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        });
        EditorUtility.DisplayDialog("Even Out Hardy And Oscar",
            (oscar ? "Oscar (Level 2): " + OscarHealth + " health, hits softer, much less poison (and it goes away sooner), open longer after a crash.\n" : "Oscar not found in Level_2_Boss.\n") +
            (hardy ? "Hardy: " + HardyHealth + " health, hits a bit harder, rests less." : "Hardy not found in Level_1_Deck."), "OK");
    }

    static bool Tune(string path, System.Func<bool> change)
    {
        if (!System.IO.File.Exists(path)) return false;
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        bool found = change();
        if (found)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return found;
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
        if (p == null) { Debug.LogWarning("Even Out Hardy And Oscar: no setting called " + name); return; }
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
        else p.floatValue = value;
    }
}
