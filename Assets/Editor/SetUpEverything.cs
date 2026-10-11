using UnityEditor;
using UnityEngine;

// Editor-only helper. Adds Tools > Set Up All Art And Jail.
// Runs every art setup tool in a row, then adds the companion jail to Level 2:
//   Hardy, Oscar (lizard), poison, Raphael (player), companion + items, KK (adds KK first if he is missing), jail.
// Each tool shows its own "Done" message: click OK and the next one runs.
// (The tutorial rebuild is not in here because it replaces the tutorial rooms: run Tools > Rebuild Tutorial yourself.)
public static class SetUpEverything
{
    static readonly string[] Steps =
    {
        "Tools/Set Up Hardy Art",
        "Tools/Set Up Lizard Art",
        "Tools/Set Up Poison Art",
        "Tools/Set Up Player Art",
        "Tools/Set Up Companion And Item Art",
        "Tools/Set Up Madame O Art",
        "Tools/Add Father To Level 4 Boss",
        "Tools/Set Up KK Art",
        "Tools/Companion/Add Jail To Level 2",
    };

    [MenuItem("Tools/Set Up All Art And Jail", priority = 0)]
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
            if (step == "Tools/Add Father To Level 4 Boss" && FatherAlreadyThere()) continue;
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
