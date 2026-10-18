using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Editor-only. Adds Tools > Other > Build Main Menu.
// Makes the scene MainMenu (the HOOKER title screen, see TitleScreen.cs) and puts it FIRST in the
// Scene List, so the built game opens on it. Run it again any time to rebuild it.
public static class MainMenuBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string BackgroundArt = "Assets/Art/Ocean/dock_room.png";
    const string SeaArt = "Assets/Art/Ocean/ocean.png";

    [MenuItem("Tools/Other/Build Main Menu", priority = 3000)]
    static void Build()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Build Main Menu",
                "MainMenu already exists. Replace it with a fresh build?", "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera (everything on the title screen is UI, the camera just clears to black)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        // Menu input: mouse, keyboard and controller
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // The title screen itself
        var title = new GameObject("TitleScreen").AddComponent<TitleScreen>();
        var so = new SerializedObject(title);
        so.FindProperty("background").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundArt);
        so.FindProperty("sea").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(SeaArt);
        so.ApplyModifiedPropertiesWithoutUndo();
        if (!AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundArt))
            Debug.LogWarning("Build Main Menu: " + BackgroundArt + " not found, the background will be plain.");

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutFirstInSceneList(ScenePath);
        EditorUtility.DisplayDialog("Build Main Menu",
            "Done! MainMenu is built and is now the first scene in the Scene List.\n\nPress Play in this scene to try it.", "OK");
    }

    static void PutFirstInSceneList(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == path);
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
