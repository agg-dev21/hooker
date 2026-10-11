using UnityEngine;
using UnityEngine.SceneManagement;

// The order of the levels, and the boat trips between them.
//   Leaving the tutorial (the dock's boat) -> Hardy's level (Level_1) or Oscar's level (Level_2), 50 / 50.
//   After EVERY boss you sail away in the boat scene (Level_1_Sail), then arrive at the next level:
//     Hardy beaten    -> sail -> Oscar's level if he is still left, otherwise Level_3.
//     Oscar beaten    -> sail -> Hardy's level if he is still left, otherwise Level_3.
//     Madame O beaten -> sail -> Level_4 (KK).
// Ariel's jail is in Oscar's level, so she can only be with you in Hardy's level if you did Oscar's level first.
// A new run (leaving the tutorial) also resets Ariel and the boss powers, so every run starts fresh.
// Every scene change goes through Resolve (ScreenFade, BoatExit, SceneExit), so nothing in the scenes changes.
public static class RunOrder
{
    const string FirstKey = "run.first";
    const string HardyDoneKey = "run.hardyDone";
    const string OscarDoneKey = "run.oscarDone";
    const string AfterSailKey = "run.afterSail"; // where the boat goes next
    const string LastBossKey = "run.lastBoss";   // who you just beat (for the boat ride's talk)

    public static string LastBoss => PlayerPrefs.GetString(LastBossKey, "");
    public const string SailScene = "Level_1_Sail"; // the boat scene used between all bosses

    public static bool HardyDone => PlayerPrefs.GetInt(HardyDoneKey, 0) == 1;
    public static bool OscarDone => PlayerPrefs.GetInt(OscarDoneKey, 0) == 1;

    // In the tutorial rooms: no boss powers and no Ariel (you haven't earned them yet)
    public static bool InTutorial => SceneManager.GetActiveScene().name.StartsWith("Tutorial");

    // Which scene to really load when the game asks for "requested"
    public static string Resolve(string requested)
    {
        string current = SceneManager.GetActiveScene().name;

        // Leaving the tutorial: a new run, pick the first boss level at random
        if (requested == "Level_1" && current.StartsWith("Tutorial"))
        {
            PlayerPrefs.SetInt(HardyDoneKey, 0);
            PlayerPrefs.SetInt(OscarDoneKey, 0);
            PlayerPrefs.DeleteKey(AfterSailKey);
            PlayerPrefs.DeleteKey(Companion.FoundSave);
            PlayerPrefs.DeleteKey(Companion.FoundSave + ".new");
            PlayerPrefs.DeleteKey(PlayerAbility.HardySpikesSave);
            PlayerPrefs.DeleteKey(PlayerAbility.HardySpikesSave + ".new");
            PlayerPrefs.DeleteKey(PlayerUpgrades.PoisonHookSave);
            PlayerPrefs.DeleteKey(PlayerChain.SirenChainSave);
            PlayerPrefs.DeleteKey(PlayerChain.SirenChainSave + ".new");
            string first = Random.value < 0.5f ? "Level_1" : "Level_2";
            PlayerPrefs.SetString(FirstKey, first);
            PlayerPrefs.Save();
            return first;
        }

        // Hardy is beaten: his deck sends you to the boat
        if (current == "Level_1_Deck" && requested == SailScene)
            return SailThen(HardyDoneKey, OscarDone ? "Level_3" : "Level_2", "Hardy");

        // Oscar is beaten: his room's exit door goes to the boat instead of straight on
        if (current == "Level_2_Boss" && requested == "Level_3")
            return SailThen(OscarDoneKey, HardyDone ? "Level_3" : "Level_1", "Oscar");

        // Madame O is beaten: her exit door goes to the boat, then to KK
        if (current == "Level_3_Boss" && requested == "Level_4")
            return SailThen(null, "Level_4", "Madame O");

        // The boat reached the edge of the screen: arrive at the next level
        if (current == SailScene)
        {
            string next = PlayerPrefs.GetString(AfterSailKey, "");
            if (!string.IsNullOrEmpty(next)) return next;
            return OscarDone ? (HardyDone ? "Level_3" : "Level_1") : "Level_2"; // started the sail scene directly (testing)
        }

        return requested;
    }

    // Marks a boss as beaten (if doneKey is set), remembers where the boat goes, and goes to the boat scene
    static string SailThen(string doneKey, string next, string boss)
    {
        PlayerPrefs.SetString(LastBossKey, boss);
        if (doneKey != null) PlayerPrefs.SetInt(doneKey, 1);
        PlayerPrefs.SetString(AfterSailKey, next);
        PlayerPrefs.Save();
        return SailScene;
    }
}
