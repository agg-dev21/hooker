using UnityEditor;

// Editor-only. Tools > Dev Tools > Turn On / Turn Off: switches the in-game dev tools (F1-F4, see DevTools).
// Turn them OFF before making the final build of the game.
public static class DevToolsMenu
{
    const string AssetPath = "Assets/Resources/DevSettings.asset";

    [MenuItem("Tools/Dev Tools/Turn On")]
    static void TurnOn() => Set(true);

    [MenuItem("Tools/Dev Tools/Turn Off")]
    static void TurnOff() => Set(false);

    static void Set(bool on)
    {
        var settings = AssetDatabase.LoadAssetAtPath<DevSettings>(AssetPath);
        if (!settings)
        {
            EditorUtility.DisplayDialog("Dev Tools", AssetPath + " is missing.", "OK");
            return;
        }
        settings.devToolsOn = on;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Dev Tools",
            on ? "Dev tools are ON.\n\nWhile playing: F1 kill boss, F2 heal, F3 god mode, F4 next scene."
               : "Dev tools are OFF. The game has no cheat keys now (do this before the final build).", "OK");
    }
}
