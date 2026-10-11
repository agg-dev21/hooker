using UnityEngine;
using UnityEngine.SceneManagement;

// The order of the first two bosses is random for every new run:
//   leaving the tutorial (the dock's boat) -> Hardy's level (Level_1) or Oscar's level (Level_2), 50 / 50.
//   Hardy beaten (the sail scene's boat)   -> Oscar's level if he is still left, otherwise Level_3.
//   Oscar beaten (his room's exit door)    -> Hardy's level if he is still left, otherwise Level_3.
// Ariel's jail is in Oscar's level, so she can only be with you in Hardy's level if you did Oscar's level first.
// A new run (leaving the tutorial) also resets Ariel and the boss powers, so every run starts fresh.
// Every scene change goes through Resolve (ScreenFade, BoatExit, SceneExit), so nothing in the scenes changes.
public static class RunOrder
{
    const string FirstKey = "run.first";
    const string HardyDoneKey = "run.hardyDone";
    const string OscarDoneKey = "run.oscarDone";

    public static bool HardyDone => PlayerPrefs.GetInt(HardyDoneKey, 0) == 1;
    public static bool OscarDone => PlayerPrefs.GetInt(OscarDoneKey, 0) == 1;

    // Which scene to really load when the game asks for "requested"
    public static string Resolve(string requested)
    {
        string current = SceneManager.GetActiveScene().name;

        // Leaving the tutorial: a new run, pick the first boss level at random
        if (requested == "Level_1" && current.StartsWith("Tutorial"))
        {
            PlayerPrefs.SetInt(HardyDoneKey, 0);
            PlayerPrefs.SetInt(OscarDoneKey, 0);
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

        // Hardy is beaten (his story ends with the sail scene's boat)
        if (current == "Level_1_Sail")
        {
            PlayerPrefs.SetInt(HardyDoneKey, 1);
            PlayerPrefs.Save();
            return OscarDone ? "Level_3" : "Level_2";
        }

        // Oscar is beaten (his room's exit door)
        if (current == "Level_2_Boss" && requested == "Level_3")
        {
            PlayerPrefs.SetInt(OscarDoneKey, 1);
            PlayerPrefs.Save();
            return HardyDone ? "Level_3" : "Level_1";
        }

        return requested;
    }
}
