using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LevelKit;

// Editor-only helpers for the companion (Tools > Companion):
//   Add Jail To Level 2         - a hidden jail room in Level_2 where she is locked up. The entrance is an iron
//                                 door in the bottom-left corner of the bottom floor. Running it again replaces the jail.
//   Give Companion (testing)    - she joins you right away (saved)
//   Remove Companion (testing)  - forget her, so the jail has her again
public static class CompanionTools
{
    const string LevelScene = "Assets/Scenes/Level_2.unity";
    const int Left = -50, Right = -24, FloorY = 64, Inside = 13; // above Level 2's other secret rooms (x -50 to -24)
    static readonly Vector2 EntrancePos = new Vector2(1.5f, 1.1f); // bottom floor, far left corner
    static readonly Color StoneColor = new Color(0.22f, 0.24f, 0.28f);
    static readonly Color BarColor = new Color(0.35f, 0.35f, 0.4f);
    static readonly Color CompanionColor = new Color(1f, 0.92f, 0.65f);

    [MenuItem("Tools/Companion/Add Jail To Level 2")]
    static void AddJail()
    {
        if (!File.Exists(LevelScene))
        {
            EditorUtility.DisplayDialog("Companion Jail", LevelScene + " does not exist.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Single);

        // Level kit setup for an existing scene
        Map = FindGroundTilemap(scene);
        Tile = FirstTile(Map) ?? AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/Tiles/Square.asset");
        BoxSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Square.asset")?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");
        if (!Map)
        {
            EditorUtility.DisplayDialog("Companion Jail", "No ground tilemap found in Level_2.", "OK");
            return;
        }

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "CompanionJail") Object.DestroyImmediate(r);
        var root = new GameObject("CompanionJail").transform;

        // Room shell (floor, ceiling, walls)
        Fill(Left, FloorY - 1, Right - Left + 1, 1);
        Fill(Left, FloorY + Inside, Right - Left + 1, 1);
        Fill(Left, FloorY - 1, 1, Inside + 2);
        Fill(Right, FloorY - 1, 1, Inside + 2);
        RebuildColliders(Map);

        // Its own solid floor, ceiling and walls (the tilemap's merged collider doesn't always pick up new tiles)
        float midX = (Left + Right + 1) * 0.5f, width = Right - Left + 1;
        SolidBlock("JailFloorSolid", root, new Vector2(midX, FloorY - 0.5f), new Vector2(width, 1f));
        SolidBlock("JailCeilingSolid", root, new Vector2(midX, FloorY + Inside + 0.5f), new Vector2(width, 1f));
        SolidBlock("JailLeftWallSolid", root, new Vector2(Left + 0.5f, FloorY + Inside * 0.5f), new Vector2(1f, Inside + 2f));
        SolidBlock("JailRightWallSolid", root, new Vector2(Right + 0.5f, FloorY + Inside * 0.5f), new Vector2(1f, Inside + 2f));

        // Dungeon look: dark back wall, rows of stone, chains on the wall, a lantern
        Deco("BackWall", root, new Vector2((Left + Right) * 0.5f + 0.5f, FloorY + Inside * 0.5f), new Vector2(Right - Left, Inside), new Color(0.08f, 0.08f, 0.11f), -20);
        for (int row = 2; row < Inside; row += 2)
            Deco("StoneRow", root, new Vector2((Left + Right) * 0.5f + 0.5f, FloorY + row), new Vector2(Right - Left - 1, 0.07f), new Color(0.04f, 0.04f, 0.06f), -19);
        foreach (float cx in new[] { -33f, -29f })
            Deco("WallChain", root, new Vector2(cx, FloorY + 8f), new Vector2(0.12f, 3f), BarColor, -10);
        Lantern("Lantern_Jail", root, new Vector2(-36f, FloorY + 5f), new Color(1f, 0.8f, 0.5f));

        // The cell on the left: back, roof, her placeholder inside, iron bars in front
        const float cellLeft = -48.5f, cellRight = -40f;
        float cellMid = (cellLeft + cellRight) * 0.5f;
        Deco("CellBack", root, new Vector2(cellMid, FloorY + 3f), new Vector2(cellRight - cellLeft, 6f), StoneColor, -12);
        Deco("CellRoof", root, new Vector2(cellMid, FloorY + 6.1f), new Vector2(cellRight - cellLeft + 0.4f, 0.4f), BarColor, 3);
        GameObject prisoner = Deco("Prisoner (companion)", root, new Vector2(cellMid, FloorY + 0.8f), new Vector2(0.8f, 1.6f), CompanionColor, 2);

        var bars = new GameObject("CellBars").transform;
        bars.SetParent(root);
        bars.position = new Vector3(cellRight, FloorY + 3f, 0f);
        for (int i = 0; i < 9; i++) // iron bars across the front of the cell
            Deco("Bar", bars, new Vector2(cellLeft + 0.4f + i * 0.95f, FloorY + 3f), new Vector2(0.12f, 6f), BarColor, 4);

        // Lock in front of the cell: stand here and press E
        GameObject lockBox = Box("CellLock", root, new Vector2(cellRight + 0.6f, FloorY + 1.6f), new Vector2(0.5f, 0.6f), new Color(0.75f, 0.6f, 0.2f));
        var lockCol = lockBox.GetComponent<BoxCollider2D>();
        lockCol.isTrigger = true;
        lockCol.size = new Vector2(6f, 5f); // big area (the box is scaled 0.5 x 0.6)
        var cell = new SerializedObject(lockBox.AddComponent<JailCell>());
        cell.FindProperty("bars").objectReferenceValue = bars;
        cell.FindProperty("prisoner").objectReferenceValue = prisoner;
        cell.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(lockBox, lockBox.GetComponent<JailCell>());

        // Doors: iron door in the level -> into the jail; exit door in the jail -> back
        var inSpawn = new GameObject("JailSpawn").transform;
        inSpawn.SetParent(root);
        inSpawn.position = new Vector2(Right - 4.5f, FloorY + 1.1f);
        var backSpawn = new GameObject("JailReturn").transform;
        backSpawn.SetParent(root);
        backSpawn.position = EntrancePos;

        GameObject entrance = Box("JailDoor", root, EntrancePos, new Vector2(1f, 2.2f), new Color(0.3f, 0.3f, 0.34f));
        entrance.GetComponent<BoxCollider2D>().isTrigger = true;
        var enter = new SerializedObject(entrance.AddComponent<SecretDoor>());
        enter.FindProperty("destination").objectReferenceValue = inSpawn;
        enter.FindProperty("entersRoom").boolValue = true;
        enter.FindProperty("roomMin").vector2Value = new Vector2(Left, FloorY - 1);
        enter.FindProperty("roomMax").vector2Value = new Vector2(Right + 1, FloorY + Inside + 1);
        enter.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(entrance, entrance.GetComponent<SecretDoor>());

        GameObject exitDoor = Box("JailExit", root, new Vector2(Right - 2.5f, FloorY + 1.1f), new Vector2(0.9f, 2.2f), new Color(0.45f, 0.25f, 0.08f));
        exitDoor.GetComponent<BoxCollider2D>().isTrigger = true;
        var exit = new SerializedObject(exitDoor.AddComponent<SecretDoor>());
        exit.FindProperty("destination").objectReferenceValue = backSpawn;
        exit.FindProperty("entersRoom").boolValue = false;
        exit.ApplyModifiedPropertiesWithoutUndo();
        AddPrompt(exitDoor, exitDoor.GetComponent<SecretDoor>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        EditorUtility.DisplayDialog("Companion Jail",
            "Jail added to Level_2.\n\nThe entrance is the grey iron door in the bottom-left corner of the bottom floor. " +
            "Inside, stand at the gold lock and press E to free her.\n\n" +
            "To test again later: Tools > Companion > Remove Companion (testing).", "OK");
    }

    // An invisible solid box on the Ground layer
    static void SolidBlock(string name, Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.layer = GroundLayer;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    [MenuItem("Tools/Companion/Give Companion (testing)")]
    static void Give()
    {
        PlayerPrefs.SetInt(Companion.FoundSave, 1);
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Companion", "She is with you now. Press Play; R fires her light.", "OK");
    }

    [MenuItem("Tools/Companion/Remove Companion (testing)")]
    static void Remove()
    {
        PlayerPrefs.DeleteKey(Companion.FoundSave);
        PlayerPrefs.DeleteKey(Companion.FoundSave + ".new");
        PlayerPrefs.Save();
        EditorUtility.DisplayDialog("Companion", "She is back in the jail in Level_2.", "OK");
    }
}
