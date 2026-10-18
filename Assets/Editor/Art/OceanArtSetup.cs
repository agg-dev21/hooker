using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only helper. Adds Tools > Art > Ocean And Dock.
//   dock_room.png (6400x1600): the whole background of tutorial room 7 (city, pier, sky, sea).
//     It is lined up so its pier sits on the room's dock (floor y 0, dock end at x 30). The room's own tiles
//     there become invisible (they are still solid), the plain water box is hidden, and the camera can show the
//     whole picture.
//   ocean.png (3200x1600, waves in the bottom half): the sea everywhere else. It repeats sideways:
//     Hardy's deck (behind the ship) and the boat scene between bosses (Level_1_Sail).
// Both pictures are drawn at 80 pixels per tile. The sea moves (OceanLayers: 3 drifting, bobbing layers) and the
// boats rock on it (BoatBob).
public static class OceanArtSetup
{
    const string Folder = "Assets/Art/Ocean";
    const float PixelsPerUnit = 80f;
    // Where the pier is in dock_room.png (pixels): its surface 384 px from the bottom, its end 3100 px from the left
    const float PierTopFromBottom = 384f, PierEndFromLeft = 3100f;
    const float RoomDockEndX = 30f;
    const float WaveTopFromBottom = 576f; // ocean.png: where the water body starts (most wave tops), in pixels from the bottom

    [MenuItem("Tools/Art/Ocean And Dock", priority = 141)]
    static void SetUp()
    {
        Sprite dock = Import(Folder + "/dock_room.png", new Vector2(0f, 0f), false);
        Sprite ocean = Import(Folder + "/ocean.png", new Vector2(0.5f, 0f), true);
        if (!dock || !ocean)
        {
            EditorKit.Message("Ocean And Dock", "Missing " + Folder + "/dock_room.png or ocean.png.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = SceneManager.GetActiveScene().path;
        string report = "";

        report += DockRoom(dock);
        report += Sea("Assets/Scenes/Level_1_Deck.unity", ocean, -3.3f, 26.5f, 120f, -27, new[] { "Sea", "Wave" }, true, false);
        report += Sea("Assets/Scenes/Level_1_Sail.unity", ocean, -5f, 0f, 70f, -5, new[] { "Sea" }, false, true);
        report += Sea("Assets/Scenes/Tutorial_7_Dock.unity", ocean, -1f, 53f, 44f, -45, new string[0], false, true); // moving waves over the picture's sea

        if (!string.IsNullOrEmpty(start) && File.Exists(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        EditorKit.Message("Ocean And Dock", report, "OK");
    }

    // ---------------- Tutorial room 7 ----------------

    static string DockRoom(Sprite dock)
    {
        const string path = "Assets/Scenes/Tutorial_7_Dock.unity";
        if (!File.Exists(path)) return "Tutorial_7_Dock: missing\n";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "DockBackground") Object.DestroyImmediate(r);
        float left = RoomDockEndX - PierEndFromLeft / PixelsPerUnit;
        float bottom = -PierTopFromBottom / PixelsPerUnit;
        var bg = new GameObject("DockBackground");
        bg.transform.position = new Vector3(left, bottom, 0f);
        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = dock;
        sr.sortingOrder = -50;

        // The picture has its own pier and sea: hide the room's tiles (still solid) and the plain water
        foreach (TilemapRenderer tr in Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include))
            tr.enabled = false;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Water" || t.name == "PitDark") t.gameObject.SetActive(false);

        // The camera can show the whole picture
        Vector2 size = dock.rect.size / PixelsPerUnit;
        if (Object.FindAnyObjectByType<CameraFollow2D>(FindObjectsInactive.Include) is CameraFollow2D follow)
        {
            var so = new SerializedObject(follow);
            so.FindProperty("minBounds").vector2Value = new Vector2(left, bottom);
            so.FindProperty("maxBounds").vector2Value = new Vector2(left + size.x, bottom + size.y);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Tutorial room 7: dock picture in place\n";
    }

    // ---------------- Sea ----------------

    // Puts the repeating ocean picture with its wave tops at waveTopY, centred on centerX, width wide.
    // Hides the old plain sea objects named in hide (and their children if hideChildren).
    // frontOverBoats: the front wave layer passes in front of boats (off on Hardy's deck, where it would cover the ship)
    static string Sea(string path, Sprite ocean, float waveTopY, float centerX, float width, int order, string[] hide, bool hideChildren, bool frontOverBoats)
    {
        if (!File.Exists(path)) return Path.GetFileNameWithoutExtension(path) + ": missing\n";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "OceanArt") Object.DestroyImmediate(r);
        int hidden = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (System.Array.IndexOf(hide, t.name) < 0) continue;
            // Only hide the look: anything solid (a collider) keeps working
            foreach (SpriteRenderer r in hideChildren ? t.GetComponentsInChildren<SpriteRenderer>(true) : t.GetComponents<SpriteRenderer>())
                if (r.name == "Sea" || r.name == "Wave" || r.transform == t) { r.enabled = false; hidden++; }
        }

        var go = new GameObject("OceanArt");
        go.transform.position = new Vector3(centerX, waveTopY - WaveTopFromBottom / PixelsPerUnit, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ocean;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(width, ocean.rect.height / PixelsPerUnit);
        sr.sortingOrder = order;

        // Moving layers, and rocking boats
        var layers = new SerializedObject(go.AddComponent<OceanLayers>());
        // Front layer: drawn at order 10 (in front of boats) or just in front of the main sea (behind the ship)
        layers.FindProperty("front").FindPropertyRelative("sortingOffset").intValue = frontOverBoats ? 10 - order : 1;
        layers.ApplyModifiedPropertiesWithoutUndo();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Boat" && !t.GetComponent<BoatBob>()) t.gameObject.AddComponent<BoatBob>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return Path.GetFileNameWithoutExtension(path) + ": ocean in place (" + hidden + " old sea pieces hidden)\n";
    }

    static Sprite Import(string path, Vector2 pivot, bool repeat)
    {
        if (!File.Exists(path)) return null;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;          // pixel art stays crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 8192;
        if (repeat) importer.wrapMode = TextureWrapMode.Repeat;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect; // needed to repeat it
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
