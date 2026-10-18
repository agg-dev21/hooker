using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only helper. Adds Tools > Update All Art (at the top of the Tools menu).
// Runs every Tools > Art tool in a row, so new or changed pictures in Assets/Art show up in the game.
// It only touches art: no level is rebuilt and no boss numbers change.
// The tools' "Done" messages are collected and shown once at the end (one OK instead of fourteen).
// To update just one thing, use its own item under Tools > Art.
public static class UpdateAllArt
{
    // Hardy before Ability Icons (both set Hardy's icon, the newer icon wins)
    public static readonly string[] Steps =
    {
        "Tools/Art/Raphael (Player)",
        "Tools/Art/Ariel And Items",
        "Tools/Art/Hardy",
        "Tools/Art/Oscar (Lizard)",
        "Tools/Art/Madame O",
        "Tools/Art/KK",
        "Tools/Art/Poison",
        "Tools/Art/Ability Icons And Ground Effects",
        "Tools/Art/Health Bars",
        "Tools/Art/Tutorial Backgrounds",
        "Tools/Art/Ocean And Dock",
        "Tools/Art/Hardy Arena Background",
        "Tools/Art/Hook Points",
        "Tools/Art/Planks",
    };

    const string AddKK = "Tools/Other/Rebuild Levels (wipes changes)/Level 4 Boss: Add KK";

    [MenuItem("Tools/Update All Art", priority = 0)]
    static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Update All Art", "Stop the game first (art changes made while playing are thrown away).", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = EditorSceneManager.GetActiveScene().path;

        EditorKit.Collected.Clear();
        EditorKit.Collecting = true;
        string failed = "";
        try
        {
            // KK's art needs KK in his room: add him first only if he is missing (adding him again would reset his numbers)
            if (!FatherAlreadyThere() && !EditorApplication.ExecuteMenuItem(AddKK)) failed += "\n" + AddKK;
            foreach (string step in Steps)
                if (!EditorApplication.ExecuteMenuItem(step)) failed += "\n" + step;
        }
        finally
        {
            EditorKit.Collecting = false;
        }

        if (!string.IsNullOrEmpty(start) && File.Exists(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        string report = string.Join("\n", EditorKit.Collected);
        EditorKit.Collected.Clear();
        Debug.Log("Update All Art:\n" + report);
        EditorUtility.DisplayDialog("Update All Art",
            (string.IsNullOrEmpty(failed) ? "All art is up to date!" : "Done, but these could not run:" + failed) +
            "\n\nThe details of each tool are in the Console.", "OK");
    }

    public static bool FatherAlreadyThere()
    {
        const string path = "Assets/Scenes/Level_4_Boss.unity";
        if (!File.Exists(path)) return false;
        string text = File.ReadAllText(path);
        return text.Contains("m_Name: Father\n") || text.Contains("m_Name: Father\r\n");
    }
}
