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

    /// <summary>Fills a serialized Sprite[] field with the given sprites.</summary>
    public static void SetArray(SerializedProperty prop, Sprite[] sprites)
    {
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
