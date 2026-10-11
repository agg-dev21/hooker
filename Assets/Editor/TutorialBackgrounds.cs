using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only helper. Adds Tools > Art > Tutorial Backgrounds.
// Puts the team's tutorial environment art (Assets/Art/Tutorial, 5 pictures cut from one strip) behind tutorial rooms 1 to 6.
// Each room gets the picture that fits its layout:
//   1 Move    town (left)      you start outside, in town
//   2 Jump    shipyard 2       three pillars, like the blocks you jump over
//   3 Dash    shipyard 3       long low floor line with open space, like the gaps you dash across
//   4 Punch   shipyard 1       crowded beams and blocks, like the walls you break
//   5 Hook    shipyard 3 (flipped) a tall dark wall on the left, like the one you hook over
//   6 Bullets town (right)     back outside, on the way to the dock (room 7 has its own dock picture)
// The picture fills the camera and slides a little as you move (BackdropFollow).
// The town pictures have an empty sky: a plain sky colour is drawn behind them.
// Running it again replaces the old background. Change the list below to swap pictures.
public static class TutorialBackgrounds
{
    const string Folder = "Assets/Art/Tutorial";
    const float PixelsPerUnit = 80f;
    const int SortingOrder = -60;
    static readonly Color SkyColor = new Color(0.80f, 0.72f, 0.66f); // behind the town pictures
    const string SquareTile = "Assets/Tiles/Square.asset";

    static readonly (string scene, string picture, bool flip, bool sky)[] Rooms =
    {
        ("Tutorial_1_Move",              "tutorial_town_left",  false, true),
        ("Tutorial_2_Jump",              "tutorial_shipyard_2", false, false),
        ("Tutorial_3_Dash",              "tutorial_shipyard_3", false, false),
        ("Tutorial_4_Punch",             "tutorial_shipyard_1", false, false),
        ("Tutorial_5_Hook",              "tutorial_shipyard_3", true,  false),
        ("Tutorial_6_DashThroughBullets","tutorial_town_right", false, true),
    };

    [MenuItem("Tools/Art/Tutorial Backgrounds")]
    static void SetUp()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = SceneManager.GetActiveScene().path;
        string report = "";

        foreach (var room in Rooms)
        {
            string path = "Assets/Scenes/" + room.scene + ".unity";
            Sprite pic = Import(Folder + "/" + room.picture + ".png");
            if (!File.Exists(path) || !pic) { report += room.scene + ": missing scene or picture\n"; continue; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (GameObject r in scene.GetRootGameObjects())
                if (r.name == "TutorialBackground") Object.DestroyImmediate(r);

            var bg = new GameObject("TutorialBackground");
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = pic;
            sr.flipX = room.flip;
            sr.sortingOrder = SortingOrder;
            bg.AddComponent<BackdropFollow>();

            if (room.sky)
            {
                var sky = new GameObject("Sky");
                sky.transform.SetParent(bg.transform, false);
                var skyR = sky.AddComponent<SpriteRenderer>();
                skyR.sprite = AssetDatabase.LoadAssetAtPath<Tile>(SquareTile)?.sprite;
                skyR.color = SkyColor;
                skyR.sortingOrder = SortingOrder - 1;
                if (skyR.sprite)
                {
                    Vector2 s = skyR.sprite.bounds.size, p = pic.bounds.size;
                    sky.transform.localScale = new Vector3(p.x / s.x, p.y / s.y, 1f);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report += room.scene + ": " + room.picture + (room.flip ? " (flipped)" : "") + "\n";
        }

        if (!string.IsNullOrEmpty(start) && File.Exists(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Tutorial Backgrounds", report, "OK");
    }

    static Sprite Import(string path)
    {
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // a picture just copied in may not be imported yet
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;          // pixel art stays crisp
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center; // BackdropFollow centres it on the camera
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
