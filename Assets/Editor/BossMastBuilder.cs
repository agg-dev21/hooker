using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only helper. Adds Tools > Add Masts To Level 2 Boss.
// Two masts (no sails) left and right of the middle platform, each with a crow's nest on top
// that the player can jump up through and stand on. Camping up there too long makes the boss
// break the mast (see BreakableMast and PoisonBoss). Running it again replaces the old masts.
// Each mast has a hook ring under its nest: hook it to get pulled up and pop up through the nest.
public static class BossMastBuilder
{
    const string BossScene = "Assets/Scenes/Level_2_Boss.unity";
    const string SquareTile = "Assets/Tiles/Square.asset";
    const float NestHeight = 10f;  // top of the crow's nest (the middle platform's top is at 6)
    const float NestWidth = 3f;
    static readonly float[] MastX = { 11f, 25f };
    static readonly Color Pole = new Color(0.3f, 0.2f, 0.12f);
    static readonly Color Nest = new Color(0.42f, 0.29f, 0.17f);
    static readonly Color Rim = new Color(0.22f, 0.14f, 0.08f);
    const float RingBelowNest = 1.5f;  // the hook ring hangs this far under the top of the nest
    const float RingLaunch = 18f;      // upward push after reaching the ring (enough to pop up through the nest)
    static readonly Color RingColor = new Color(1f, 0.85f, 0.2f);

    [MenuItem("Tools/Add Masts To Level 2 Boss")]
    static void AddMasts()
    {
        if (!File.Exists(BossScene))
        {
            EditorUtility.DisplayDialog("Add Masts", BossScene + " does not exist.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(BossScene, OpenSceneMode.Single);
        Sprite box = AssetDatabase.LoadAssetAtPath<Tile>(SquareTile)?.sprite;

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "Masts") Object.DestroyImmediate(r);
        var root = new GameObject("Masts").transform;

        for (int i = 0; i < MastX.Length; i++)
        {
            float x = MastX[i];
            var mast = new GameObject(i == 0 ? "Mast_Left" : "Mast_Right").transform;
            mast.SetParent(root);
            mast.position = new Vector3(x, 0f, 0f);

            // Pole: decoration only (the boss and the player pass in front of it)
            Transform pole = Piece("Pole", mast, box, new Vector2(x, NestHeight * 0.5f), new Vector2(0.5f, NestHeight), Pole, -1).transform;

            // Crow's nest: one-way platform you jump up through and stand on
            GameObject nest = Piece("CrowsNest", mast, box, new Vector2(x, NestHeight - 0.25f), new Vector2(NestWidth, 0.5f), Nest, 1);
            nest.layer = LayerMask.NameToLayer("Ground");
            var col = nest.AddComponent<BoxCollider2D>();
            col.usedByEffector = true;
            var effector = nest.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            nest.AddComponent<OneWayPlatform>();

            // Little rails on the nest's ends (look only)
            Transform railL = Piece("Rail", mast, box, new Vector2(x - NestWidth * 0.5f + 0.1f, NestHeight + 0.4f), new Vector2(0.15f, 0.8f), Rim, 1).transform;
            Transform railR = Piece("Rail", mast, box, new Vector2(x + NestWidth * 0.5f - 0.1f, NestHeight + 0.4f), new Vector2(0.15f, 0.8f), Rim, 1).transform;

            // Hook ring under the nest: falls with the mast when it breaks
            GameObject ring = Piece("HookRing", mast, box, new Vector2(x, NestHeight - RingBelowNest), new Vector2(0.6f, 0.6f), RingColor, 2);
            var ringCol = ring.AddComponent<CircleCollider2D>();
            ringCol.radius = 1f; // bigger than the ring so it is easy to hit
            ringCol.isTrigger = true;
            var hook = new SerializedObject(ring.AddComponent<HookPoint>());
            hook.FindProperty("launchSpeed").floatValue = RingLaunch;
            hook.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(mast.gameObject.AddComponent<BreakableMast>());
            so.FindProperty("platform").objectReferenceValue = col;
            var parts = so.FindProperty("parts");
            Transform[] list = { pole, nest.transform, railL, railR, ring.transform };
            parts.arraySize = list.Length;
            for (int p = 0; p < list.Length; p++) parts.GetArrayElementAtIndex(p).objectReferenceValue = list[p];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Add Masts", "Two masts added to Level_2_Boss (x 11 and 25, crow's nests 10 high).\n\nHook the yellow ring under a nest to get up. Stand on one for 3 seconds and the boss comes to break it.", "OK");
    }

    static GameObject Piece(string name, Transform parent, Sprite sprite, Vector2 center, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }
}
