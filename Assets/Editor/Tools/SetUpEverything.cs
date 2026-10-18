using UnityEditor;

// Editor-only helper. Adds Tools > Other > Set Up All Art And Jail.
// Runs every art setup tool in a row, then adds the companion jail to Level 2:
//   Hardy, Oscar (lizard), poison, Raphael (player), companion + items, Madame O, KK (adds KK first if he is missing),
//   jail, story dialogue.
// Each tool shows its own "Done" message: click OK and the next one runs.
// (The tutorial rebuild is not in here because it replaces the tutorial rooms: run Tools > Other > Rebuild Tutorial yourself.)
public static class SetUpEverything
{
    static readonly string[] Steps =
    {
        "Tools/Art/Hardy",
        "Tools/Art/Oscar (Lizard)",
        "Tools/Art/Poison",
        "Tools/Art/Raphael (Player)",
        "Tools/Art/Ariel And Items",
        "Tools/Art/Madame O",
        "Tools/Other/Rebuild Levels (wipes changes)/Level 4 Boss: Add KK",
        "Tools/Art/KK",
        "Tools/Art/Ocean And Dock",
        "Tools/Art/Health Bars",
        "Tools/Art/Hook Points",
        "Tools/Art/Planks",
        "Tools/Art/Ability Icons And Ground Effects",
        "Tools/Other/Companion/Add Jail To Level 2",
        "Tools/Other/Add Story Dialogue",
        "Tools/Other/Tutorial/Hook Lesson In Jump Room",
        "Tools/Art/Tutorial Backgrounds",
        "Tools/Other/Story/Place Hint Scrolls",
    };

    [MenuItem("Tools/Other/Set Up All Art And Jail", priority = 2990)]
    static void Run()
    {
        if (!EditorUtility.DisplayDialog("Set Up All Art And Jail",
                "This runs all the art setup tools and adds the jail to Level 2.\n\nEach one shows a message: click OK to go on.",
                "Go", "Cancel"))
            return;

        string failed = "";
        foreach (string step in Steps)
        {
            // KK is only added if he isn't in Level 4 Boss yet (adding him again would reset his numbers)
            if (step == "Tools/Other/Rebuild Levels (wipes changes)/Level 4 Boss: Add KK" && FatherAlreadyThere()) continue;
            if (!EditorApplication.ExecuteMenuItem(step)) failed += "\n" + step;
        }
        EditorUtility.DisplayDialog("Set Up All Art And Jail",
            string.IsNullOrEmpty(failed) ? "All done!" : "Done, but these could not run:" + failed, "OK");
    }

    static bool FatherAlreadyThere()
    {
        const string path = "Assets/Scenes/Level_4_Boss.unity";
        if (!System.IO.File.Exists(path)) return false;
        return System.IO.File.ReadAllText(path).Contains("m_Name: Father\n") || System.IO.File.ReadAllText(path).Contains("m_Name: Father\r\n");
    }
}
