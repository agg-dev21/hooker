using UnityEngine;
using UnityEngine.SceneManagement;

// The order of the levels, the boat trips between them, and going back to explore.
//   Leaving the tutorial (the dock's boat) -> Hardy's level (Level_1) or Oscar's level (Level_2), 50 / 50.
//   After EVERY boss you sail away in the boat scene (Level_1_Sail), then arrive at the next level:
//     Hardy beaten    -> Oscar's level if he is still left, otherwise Level_3.
//     Oscar beaten    -> Hardy's level if he is still left, otherwise Level_3.
//     Madame O beaten -> Level_4 (KK).
//   Exploring: on the boat you can also pick any level whose boss you already beat (SailMenu), until you go to KK.
//     Beaten bosses stay beaten there, and a spot at the level's start takes you back to the boat (Revisit).
// Ariel's jail is in Oscar's level. A new game (Tutorial 1) resets everything: bosses, powers, Ariel, chests.
// Every scene change goes through Resolve (ScreenFade, BoatExit, SceneExit), so nothing in the scenes changes.
public static class RunOrder
{
    const string FirstKey = "run.first";
    const string FirstStreakKey = "run.firstStreak"; // how many games in a row started with the same boss
    const string HardyDoneKey = "run.hardyDone";
    const string OscarDoneKey = "run.oscarDone";
    const string MadameDoneKey = "run.madameDone";
    const string AfterSailKey = "run.afterSail"; // where the boat goes this time
    const string LastBossKey = "run.lastBoss";   // who you just beat (for the boat ride's talk)
    const string RunIdKey = "run.id";            // a new number every new game (chests remember per game)
    public const string SailScene = "Level_1_Sail"; // the boat scene used between all bosses

    public static bool HardyDone => PlayerPrefs.GetInt(HardyDoneKey, 0) == 1;
    public static bool OscarDone => PlayerPrefs.GetInt(OscarDoneKey, 0) == 1;
    public static bool MadameDone => PlayerPrefs.GetInt(MadameDoneKey, 0) == 1;
    public static string LastBoss => PlayerPrefs.GetString(LastBossKey, "");
    public static int RunId => PlayerPrefs.GetInt(RunIdKey, 0);

    // In the tutorial rooms: no boss powers and no Ariel (you haven't earned them yet)
    public static bool InTutorial => SceneManager.GetActiveScene().name.StartsWith("Tutorial");

    // Where the story goes next
    public static string NextLevel =>
        MadameDone ? "Level_4" :
        HardyDone && OscarDone ? "Level_3" :
        HardyDone ? "Level_2" :
        OscarDone ? "Level_1" :
        PlayerPrefs.GetString(FirstKey, "Level_1");

    // The boss of a level is beaten (so it can be explored freely)
    public static bool LevelDone(string level) =>
        level == "Level_1" ? HardyDone : level == "Level_2" ? OscarDone : level == "Level_3" ? MadameDone : false;

    // A new game (Tutorial 1): everything from the last game is forgotten
    public static void NewGame()
    {
        PlayerPrefs.SetInt(RunIdKey, RunId + 1);
        PlayerPrefs.SetInt(HardyDoneKey, 0);
        PlayerPrefs.SetInt(OscarDoneKey, 0);
        PlayerPrefs.SetInt(MadameDoneKey, 0);
        PlayerPrefs.DeleteKey(AfterSailKey);
        PlayerPrefs.DeleteKey(LastBossKey);
        PlayerPrefs.DeleteKey(Companion.FoundSave);
        PlayerPrefs.DeleteKey(Companion.FoundSave + ".new");
        PlayerPrefs.DeleteKey(PlayerAbility.HardySpikesSave);
        PlayerPrefs.DeleteKey(PlayerAbility.HardySpikesSave + ".new");
        PlayerPrefs.DeleteKey(PlayerUpgrades.PoisonHookSave);
        PlayerPrefs.DeleteKey(PlayerChain.SirenChainSave);
        PlayerPrefs.DeleteKey(PlayerChain.SirenChainSave + ".new");
        PlayerPrefs.Save();
    }

    // The boat menu picked where to go
    public static void SailTo(string level)
    {
        PlayerPrefs.SetString(AfterSailKey, level);
        PlayerPrefs.Save();
    }

    // The boat ride's talk has been said (so it isn't repeated)
    public static void ClearLastBoss()
    {
        PlayerPrefs.DeleteKey(LastBossKey);
        PlayerPrefs.Save();
    }

    // Which scene to really load when the game asks for "requested"
    public static string Resolve(string requested)
    {
        string current = SceneManager.GetActiveScene().name;

        // Leaving the tutorial: pick the first boss level at random
        if (requested == "Level_1" && current.StartsWith("Tutorial"))
        {
            // A fresh coin flip every time (Unity's own Random can repeat itself between plays in the editor),
            // and never the same first boss more than 2 games in a row
            string last = PlayerPrefs.GetString(FirstKey, "");
            int streak = PlayerPrefs.GetInt(FirstStreakKey, 0);
            string first = new System.Random(System.Guid.NewGuid().GetHashCode()).Next(2) == 0 ? "Level_1" : "Level_2";
            if (first == last && streak >= 2) first = first == "Level_1" ? "Level_2" : "Level_1";
            PlayerPrefs.SetInt(FirstStreakKey, first == last ? streak + 1 : 1);
            Debug.Log("RunOrder: first boss level this game is " + first);
            PlayerPrefs.SetString(FirstKey, first);
            PlayerPrefs.Save();
            return first;
        }

        // Leaving a boss room for the boat (the first time it also marks the boss as beaten)
        if (current == "Level_1_Deck" && requested == SailScene) return ToBoat(HardyDoneKey, "Hardy");
        if (current == "Level_2_Boss" && requested == "Level_3") return ToBoat(OscarDoneKey, "Oscar");
        if (current == "Level_3_Boss" && requested == "Level_4") return ToBoat(MadameDoneKey, "Madame O");

        // The boat reached the edge of the screen: arrive where the boat was told to go
        if (current == SailScene)
        {
            string next = PlayerPrefs.GetString(AfterSailKey, "");
            return string.IsNullOrEmpty(next) ? NextLevel : next;
        }

        return requested;
    }

    static string ToBoat(string doneKey, string boss)
    {
        bool firstTime = PlayerPrefs.GetInt(doneKey, 0) == 0;
        PlayerPrefs.SetInt(doneKey, 1);
        if (firstTime) PlayerPrefs.SetString(LastBossKey, boss); // only a fresh win gets the boat ride talk
        PlayerPrefs.SetString(AfterSailKey, NextLevel);
        PlayerPrefs.Save();
        return SailScene;
    }
}
