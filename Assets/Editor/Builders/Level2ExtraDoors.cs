using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only helper. Adds Tools > Level 2 > Add Extra Boss Doors.
// More ways into Oscar's room: two more boss doors on the bottom floor of the tower (middle and right),
// besides the one in the left side room. Running it again replaces them.
public static class Level2ExtraDoors
{
    const string ScenePath = "Assets/Scenes/Level_2.unity";
    static readonly Vector2[] Spots = { new Vector2(22f, 1.1f), new Vector2(38f, 1.1f) }; // bottom floor (top at y 0)

    [MenuItem("Tools/Level 2/Add Extra Boss Doors")]
    static void Add()
    {
        if (!File.Exists(ScenePath) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        BoxSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Square.asset")?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");

        // Same look and target as the existing boss door
        BossDoor original = null;
        foreach (BossDoor d in Object.FindObjectsByType<BossDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!d.name.StartsWith("BossDoor_Extra")) { original = d; break; }
        string bossScene = "Level_2_Boss";
        Color color = new Color(0.55f, 0.15f, 0.6f);
        if (original)
        {
            bossScene = new SerializedObject(original).FindProperty("bossScene").stringValue;
            if (original.TryGetComponent(out SpriteRenderer osr)) color = osr.color;
        }

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "ExtraBossDoors") Object.DestroyImmediate(r);
        var root = new GameObject("ExtraBossDoors").transform;
        for (int i = 0; i < Spots.Length; i++)
            Door("BossDoor_Extra" + (i + 1), root, Spots[i], bossScene, color);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        EditorUtility.DisplayDialog("Extra Boss Doors",
            "Two more doors to Oscar's room are on the bottom floor (middle and right).\n" +
            "Move them anywhere you like: select ExtraBossDoors in the scene.", "OK");
    }
}
