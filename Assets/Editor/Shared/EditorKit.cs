using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only helpers that several builders and art setups used to copy into
/// their own files. Call them as EditorKit.AddToSceneList(...) and so on.
/// </summary>
public static class EditorKit
{
    /// <summary>Adds a scene to File > Build Profiles (the scene list) if it is not there yet.</summary>
    public static void AddToSceneList(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>
    /// The "Done" message at the end of a tool. While Tools > Update All Art runs (Collecting is on) the messages
    /// are saved instead and shown together at the end, so you don't have to click OK after every tool.
    /// </summary>
    public static void Message(string title, string text, string ok = "OK")
    {
        if (Collecting) { Collected.Add(title + ":  " + text.Replace("\n\n", "\n")); return; }
        EditorUtility.DisplayDialog(title, text, ok);
    }

    public static bool Collecting;
    public static readonly List<string> Collected = new List<string>();

    /// <summary>Fills a serialized Sprite[] field with the given sprites.</summary>
    public static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
