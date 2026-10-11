using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static LevelKit;

// Editor-only helper. Adds Tools > Decorate > Level 3 Secret Rooms.
// Level 3's two hidden rooms get Madame O's sunken prison look: wet blue stone, seaweed on the floor,
// rusty chains and real water drips from the ceiling, barnacles, a shallow rippling puddle, broken cell bars
// and cold blue lanterns. Decoration only (no colliders). Everything sits in "SecretRoomDecor".
// Running it again replaces it. Tools > Decorations can switch it off and on.
public static class Level3SecretRoomDecorator
{
    const string ScenePath = "Assets/Scenes/Level_3.unity";
    // The two rooms (from Tools > Rebuild Levels > Level 3): inside x left+1 .. left+25, floor top y 30, ceiling y 41
    static readonly int[] RoomLefts = { 0, 30 };
    const int FloorY = 30, Inside = 11, Width = 26;

    static readonly Color Stone = new Color(0.07f, 0.1f, 0.14f);
    static readonly Color StoneLine = new Color(0.04f, 0.06f, 0.09f);
    static readonly Color Seaweed = new Color(0.15f, 0.45f, 0.3f, 0.9f);
    static readonly Color Chain = new Color(0.42f, 0.36f, 0.3f);
    static readonly Color Barnacle = new Color(0.75f, 0.72f, 0.62f);
    static readonly Color Bars = new Color(0.2f, 0.22f, 0.26f);
    static readonly Color Puddle = new Color(0.2f, 0.5f, 0.7f, 0.55f);
    static readonly Color Lamp = new Color(0.5f, 0.85f, 1f);

    [MenuItem("Tools/Decorate/Level 3 Secret Rooms")]
    static void Decorate()
    {
        if (!File.Exists(ScenePath) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BoxSprite = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/Square.asset")?.sprite;
        GroundLayer = LayerMask.NameToLayer("Ground");

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "SecretRoomDecor") Object.DestroyImmediate(r);
        var root = new GameObject("SecretRoomDecor").transform;
        var rng = new System.Random(3);

        foreach (int left in RoomLefts)
        {
            Transform room = Group("Room_" + left, root);
            float x0 = left + 1, x1 = left + Width, y0 = FloorY, y1 = FloorY + Inside;
            float mid = (x0 + x1) * 0.5f;

            // Wet stone wall with rows of blocks and a damp darker band near the floor
            Deco("Stone", room, new Vector2(mid, (y0 + y1) * 0.5f), new Vector2(x1 - x0, y1 - y0), Stone, -20);
            for (float y = y0 + 1.5f; y < y1; y += 1.5f)
                Deco("StoneRow", room, new Vector2(mid, y), new Vector2(x1 - x0, 0.06f), StoneLine, -19);
            Deco("Damp", room, new Vector2(mid, y0 + 0.8f), new Vector2(x1 - x0, 1.6f), new Color(0.05f, 0.12f, 0.16f, 0.6f), -18);

            // Broken cell bars on the back wall
            for (int i = 0; i < 6; i++)
            {
                float h = i == 2 ? 2.5f : i == 4 ? 3.2f : 5f; // two bars broken short
                Deco("CellBar", room, new Vector2(x0 + 3f + i * 0.7f, y0 + h * 0.5f), new Vector2(0.14f, h), Bars, -15);
            }
            Deco("CellTop", room, new Vector2(x0 + 4.75f, y0 + 5f), new Vector2(4.2f, 0.2f), Bars, -15);

            // Seaweed on the floor
            for (float x = x0 + 0.6f; x < x1 - 0.6f; x += 1.3f + (float)rng.NextDouble() * 1.4f)
            {
                float h = 0.8f + (float)rng.NextDouble() * 1.4f;
                GameObject weed = Deco("Seaweed", room, new Vector2(x, y0 + h * 0.5f), new Vector2(0.18f, h), Seaweed, -6);
                weed.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 16f - 8f);
            }

            // Rusty chains hanging from the ceiling, with a hook at the end
            foreach (float cx in new[] { x0 + 8f, x0 + 15f, x0 + 21f })
            {
                float len = 2f + (float)rng.NextDouble() * 3f;
                Deco("Chain", room, new Vector2(cx, y1 - len * 0.5f), new Vector2(0.12f, len), Chain, -5);
                Deco("ChainHook", room, new Vector2(cx, y1 - len - 0.2f), new Vector2(0.4f, 0.4f), Chain, -5);
            }

            // Barnacles on the walls
            for (int i = 0; i < 10; i++)
            {
                bool leftWall = i % 2 == 0;
                float bx = leftWall ? x0 + 0.15f : x1 - 0.15f;
                float by = y0 + 1f + (float)rng.NextDouble() * (Inside - 2f);
                float s = 0.18f + (float)rng.NextDouble() * 0.18f;
                Deco("Barnacle", room, new Vector2(bx, by), new Vector2(s, s), Barnacle, -4);
            }

            // A shallow puddle with a rippling surface, and real water drips from the ceiling
            GameObject puddle = Deco("Puddle", room, new Vector2(mid - 4f, y0 + 0.12f), new Vector2(6f, 0.24f), Puddle, 7);
            puddle.AddComponent<WaterSurface>();
            foreach (float dx in new[] { x0 + 4f, mid - 4f, x1 - 6f })
            {
                var drip = new GameObject("WaterDrip");
                drip.transform.SetParent(room);
                drip.transform.position = new Vector3(dx, y1 - 0.3f, 0f);
                drip.AddComponent<WaterDrip>();
            }

            // Cold blue lanterns
            Lantern("Lantern_A", room, new Vector2(x0 + 1.2f, y0 + 5f), Lamp);
            Lantern("Lantern_B", room, new Vector2(x1 - 1.2f, y0 + 5f), Lamp);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        EditorUtility.DisplayDialog("Level 3 Secret Rooms",
            "Both of Level 3's secret rooms are decorated like Madame O's sunken prison.\n\nDecoration only, no gameplay change.", "OK");
    }
}
