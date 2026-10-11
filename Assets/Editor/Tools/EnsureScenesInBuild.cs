using System.Collections.Generic;
using System.IO;
using UnityEditor;

// Editor-only. Runs by itself every time Unity loads or recompiles:
// makes sure the scenes the story needs are in the Scene List (File > Build Profiles),
// otherwise the game can't load them (for example the grave after KK, or the title screen).
[InitializeOnLoad]
public static class EnsureScenesInBuild
{
    static readonly string[] Needed =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Ending_Grave.unity",
    };

    static EnsureScenesInBuild()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;
        foreach (string path in Needed)
        {
            if (!File.Exists(path)) continue;
            int i = list.FindIndex(s => s.path == path);
            if (i < 0)
            {
                if (path.EndsWith("MainMenu.unity")) list.Insert(0, new EditorBuildSettingsScene(path, true)); // the title screen is first
                else list.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
            }
            else if (!list[i].enabled)
            {
                list[i] = new EditorBuildSettingsScene(path, true);
                changed = true;
            }
        }
        if (changed)
        {
            EditorBuildSettings.scenes = list.ToArray();
            UnityEngine.Debug.Log("EnsureScenesInBuild: added missing story scenes to the Scene List.");
        }
    }
}
