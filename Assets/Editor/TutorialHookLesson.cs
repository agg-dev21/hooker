using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only helper. Adds Tools > Tutorial > Hook Lesson In Jump Room.
//   - Takes the Hook room (Tutorial_5) and the Dash Through Bullets room (Tutorial_6) out of the game:
//     the Punch room now leads straight to the Dock, and both rooms are switched off in the Scene List (not deleted).
//   - Teaches the basic hook in the Jump room instead, WITHOUT changing its walls, floors or platforms
//     (the environment art is made for them): a gold hook ring in the air above the last tall wall, and the sign
//     before it explains the hook. Running it again does nothing new.
public static class TutorialHookLesson
{
    const string JumpScene = "Assets/Scenes/Tutorial_2_Jump.unity";
    const string PunchScene = "Assets/Scenes/Tutorial_4_Punch.unity";
    static readonly string[] RemovedRooms = { "Assets/Scenes/Tutorial_5_Hook.unity", "Assets/Scenes/Tutorial_6_DashThroughBullets.unity" };
    static readonly Vector2 RingSpot = new Vector2(38.5f, 10.5f); // above the gap before the 7-tall wall (x 40-41)
    const string HookSign = "Long falls hurt.\nToo tall to jump? {Hook} or RIGHT CLICK throws your hook: hook the gold ring to fly up to it.";

    [MenuItem("Tools/Tutorial/Hook Lesson In Jump Room")]
    static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string report = "";

        // 1) The Punch room leads straight to the Dock
        if (File.Exists(PunchScene))
        {
            Scene punch = EditorSceneManager.OpenScene(PunchScene, OpenSceneMode.Single);
            foreach (SceneExit exit in Object.FindObjectsByType<SceneExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(exit);
                so.FindProperty("nextScene").stringValue = "Tutorial_7_Dock";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(punch);
            EditorSceneManager.SaveScene(punch);
            report += "Punch room now leads to the Dock.\n";
        }

        // 2) Switch the two rooms off in the Scene List
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene e in list)
            if (System.Array.IndexOf(RemovedRooms, e.path) >= 0) e.enabled = false;
        EditorBuildSettings.scenes = list.ToArray();
        report += "Hook room and Dash Through Bullets room are off (still in the project).\n";

        // 3) The hook lesson in the Jump room (no structure changes)
        if (File.Exists(JumpScene))
        {
            Scene jump = EditorSceneManager.OpenScene(JumpScene, OpenSceneMode.Single);
            BoxSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Square.asset")?.sprite;
            GroundLayer = LayerMask.NameToLayer("Ground");

            foreach (GameObject r in jump.GetRootGameObjects())
                if (r.name == "HookLesson") Object.DestroyImmediate(r);
            var root = new GameObject("HookLesson").transform;
            Ring("Ring_HookLesson", root, RingSpot, 3f); // small push: you float over the wall

            // The sign just before the wall explains the hook
            TutorialSign nearest = null;
            float best = float.MaxValue;
            foreach (TutorialSign s in Object.FindObjectsByType<TutorialSign>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float d = Mathf.Abs(s.transform.position.x - 36f);
                if (d < best) { best = d; nearest = s; }
            }
            if (nearest && best < 4f)
            {
                nearest.template = HookSign;
                if (nearest.TryGetComponent(out TMPro.TMP_Text t)) t.text = TutorialSign.Fill(HookSign);
                EditorUtility.SetDirty(nearest);
            }
            EditorSceneManager.MarkSceneDirty(jump);
            EditorSceneManager.SaveScene(jump);
            report += "Jump room: gold hook ring above the last wall, and its sign explains the hook.\n";
        }

        EditorUtility.DisplayDialog("Hook Lesson", report, "OK");
    }
}
