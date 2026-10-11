using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Editor-only helper. Adds Tools > Story > Place Hint Scrolls.
// Puts paper scrolls around the levels: hints about finding Ariel, about the next bosses, and jokes about the mother.
// Touching one opens it right away (the game pauses) and keeps it in the inventory (not on the quick bar).
// Also writes the note inside chests that give a scroll.
// Running it again replaces the old scrolls. To add more, add a line to Scrolls or ChestNotes below
// (any scene, any spot: x and y are the tile the scroll floats over).
public static class HintScrolls
{
    struct Note
    {
        public string scene; public Vector2 pos; public string title, text;
        public Note(string scene, float x, float y, string title, string text)
        { this.scene = scene; pos = new Vector2(x, y); this.title = title; this.text = text; }
    }

    const string L2 = "Assets/Scenes/Level_2.unity";
    const float ScrollSize = 0.9f;   // how tall a scroll looks, in tiles
    const float PickupRadius = 0.6f; // how close the player has to get, in tiles
    const string SquareTile = "Assets/Tiles/Square.asset"; // used if the scroll has no picture

    static readonly Note[] Scrolls =
    {
        new Note(L2, 31f, 56.6f, "Guard's Rota",
            "Witch duty: carry her bowl ALL the way down.\nBottom floor. Left side. Always the left.\n\nWhy do witches get the worst rooms."),
        new Note(L2, 33f, 46.6f, "Tower Gossip",
            "The lizard says he was her one true love.\nSo does the octopus lady.\nSo does the guy made of goo.\n\nSomebody is lying. Probably all of them."),
        new Note(L2, 6f, 37.6f, "A Love Letter (never sent)",
            "My dearest,\nyou are the only one for me.\n\nP.S. If Hardy, Oscar or Madame O ask, I told them the same thing."),
        new Note(L2, 36f, 26.6f, "Warning, Scratched In The Wall",
            "The octopus lady never leaves her perch. Her chains do the walking.\n\nWhen the chain flashes, MOVE. The lizard is waiting for you to get stuck."),
        new Note(L2, 5.5f, 12.6f, "Note Under A Bowl",
            "If you hear humming behind the iron door, that's her. Don't let her out.\n\nA freed witch follows you forever and shoots light. Like a cat, but worse.\n\nWhoever leaves this tower without her won't get a second chance."),
        new Note(L2, 31.5f, 0.6f, "Faded Wanted Poster",
            "MISSING: Raphael's mother.\nLast seen: leaving. Then leaving again.\n\nSome say the sea has its own captain now. Get soaked by his waves too many times and it all hits at once."),
    };

    // Chests (by name) that give a scroll: their note
    static readonly (string scene, string chest, string title, string text)[] ChestNotes =
    {
        (L2, "Chest_SecretB", "Mom's Packing List",
            "Things to pack:\n- Red lipstick\n- More red lipstick\n- A map of every port with a handsome man in it (all of them)\n\nNote to self: do NOT tell the baby where I went. He'll find out when he's older.\nHe'll find out a LOT when he's older."),
    };

    [MenuItem("Tools/Story/Place Hint Scrolls")]
    static void Place()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = SceneManager.GetActiveScene().path;
        string report = "";

        var scenes = new System.Collections.Generic.List<string>();
        foreach (Note n in Scrolls) if (!scenes.Contains(n.scene)) scenes.Add(n.scene);
        foreach (var c in ChestNotes) if (!scenes.Contains(c.scene)) scenes.Add(c.scene);

        foreach (string path in scenes)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!File.Exists(path)) { report += name + ": missing\n"; continue; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (GameObject r in scene.GetRootGameObjects())
                if (r.name == "HintScrolls") Object.DestroyImmediate(r);
            var root = new GameObject("HintScrolls");

            int placed = 0, chests = 0;
            foreach (Note n in Scrolls)
            {
                if (n.scene != path) continue;
                MakeScroll(root.transform, n);
                placed++;
            }
            foreach (var c in ChestNotes)
            {
                if (c.scene != path) continue;
                Chest chest = null;
                foreach (Chest ch in Object.FindObjectsByType<Chest>(FindObjectsInactive.Include))
                    if (ch.name == c.chest) chest = ch;
                if (!chest) { report += name + ": chest " + c.chest + " not found\n"; continue; }
                var so = new SerializedObject(chest);
                so.FindProperty("scrollTitle").stringValue = c.title;
                so.FindProperty("scrollText").stringValue = c.text;
                so.ApplyModifiedPropertiesWithoutUndo();
                chests++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report += name + ": " + placed + " scrolls, " + chests + " chest notes\n";
        }

        if (!string.IsNullOrEmpty(start) && File.Exists(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Hint Scrolls", report, "OK");
    }

    static void MakeScroll(Transform parent, Note n)
    {
        var go = new GameObject("Scroll - " + n.title);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(n.pos.x, n.pos.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ItemSettings.Current.Look(ItemType.Scroll).icon;
        if (!sr.sprite) sr.sprite = AssetDatabase.LoadAssetAtPath<Tile>(SquareTile)?.sprite;
        sr.sortingOrder = 5;
        float height = sr.sprite ? sr.sprite.bounds.size.y : 1f;
        float scale = ScrollSize / Mathf.Max(0.01f, height);
        go.transform.localScale = Vector3.one * scale;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = PickupRadius / scale;

        var so = new SerializedObject(go.AddComponent<ItemPickup>());
        so.FindProperty("item").intValue = (int)ItemType.Scroll;
        so.FindProperty("amount").intValue = 1;
        so.FindProperty("scrollTitle").stringValue = n.title;
        so.FindProperty("scrollText").stringValue = n.text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
