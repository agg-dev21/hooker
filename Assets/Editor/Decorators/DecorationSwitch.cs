using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Other > Decorations > Turn Off In All Levels / Turn On In All Levels,
// and Tools > Other > Obstacles > Turn Off In All Levels / Turn On In All Levels (see the bottom of this file).
// Hides (or shows again) the decoration in every level. Nothing is deleted.
// Solid pieces inside a decoration group (walls you can't walk through) always stay.
// The decorations made the levels dark and lit them with their own lamps, so with them off
// the scene's Global Light goes to full brightness; turning them on puts the old brightness back.
public static class DecorationSwitch
{
    // Scene, decoration group (path from the scene root), brightness with the decorations on
    static readonly (string scene, string group, float darkLight)[] Levels =
    {
        ("Level_1",      "ShipDecor",           0.42f),
        ("Level_1_Deck", "DeckDecor",           0.55f),
        ("Level_2",      "PoisonDecor",         0.6f),
        ("Level_2_Boss", "Dungeon/Decor",       0.4f),
        ("Level_3",      "SunkenPrison/Decor",  0.55f),
        ("Level_3",      "SecretRoomDecor",     0.55f),
        ("Level_3_Boss", "AbyssalThrone/Decor", 0.45f),
        ("Level_4",      "FloodedHall/Decor",   0.6f),
        ("Level_4_Boss", "FloodedThrone/Decor", 0.55f),
    };
    const float FullLight = 1f;

    [MenuItem("Tools/Other/Decorations/Turn Off In All Levels", priority = 3000)]
    static void TurnOff() => SetAll(false);

    [MenuItem("Tools/Other/Decorations/Turn On In All Levels", priority = 3000)]
    static void TurnOn() => SetAll(true);

    static void SetAll(bool on)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;
        string report = "";

        foreach (var level in Levels)
        {
            string path = "Assets/Scenes/" + level.scene + ".unity";
            if (!File.Exists(path)) { report += level.scene + ": scene not found\n"; continue; }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Transform group = Find(scene, level.group);
            if (!group) { report += level.scene + ": no decorations found\n"; continue; }

            // Hide each piece, but keep anything solid (like the Level 2 boss room's entrance gate, which is a wall)
            group.gameObject.SetActive(true);
            int kept = 0;
            foreach (Transform piece in group)
            {
                bool solid = false;
                foreach (Collider2D c in piece.GetComponentsInChildren<Collider2D>(true))
                    if (!c.isTrigger) solid = true;
                if (solid) { piece.gameObject.SetActive(true); kept++; }
                else piece.gameObject.SetActive(on);
            }
            if (!on && kept > 0) report += level.scene + ": kept " + kept + " solid piece(s) (walls)\n";
            foreach (Light2D l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
                if (l.lightType == Light2D.LightType.Global && !l.transform.IsChildOf(group))
                {
                    if (on) l.intensity = EditorPrefs.GetFloat(Key(level.scene), level.darkLight);
                    else
                    {
                        if (l.intensity < FullLight) EditorPrefs.SetFloat(Key(level.scene), l.intensity); // remember it
                        l.intensity = FullLight;
                    }
                }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report += level.scene + ": " + (on ? "on" : "off") + "\n";
        }

        if (!string.IsNullOrEmpty(startScene) && File.Exists(startScene))
            EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Decorations", "Decorations " + (on ? "turned on" : "turned off") + ":\n\n" + report +
            "\nThe sail scene has no decoration group, so it is not changed.", "OK");
    }

    static string Key(string scene) => "Hooker.DecorLight." + scene;

    // ---------------- Obstacles ----------------
    // Tools > Other > Obstacles > Turn Off In All Levels / Turn On In All Levels.
    // Hides crate stacks, barrels, tendrils, cannons, poison puddles, spike balls, vents, chains and so on.
    // Also the obstacles inside the secret rooms (chests, doors, potions and guards stay).
    // Kill zones always stay (they catch you if you fall into a pit or the water).
    // Walls and gaps that are part of the level's floor tiles can't be switched off.
    static readonly (string scene, string group, string secretRooms)[] ObstacleGroups =
    {
        ("Level_1", "Obstacles",            "SecretRooms"),
        ("Level_2", "PoisonLevel/Hazards",  "PoisonLevel/SecretRooms"),
        ("Level_3", "SunkenPrison/Hazards", "SunkenPrison/SecretRooms"),
        ("Level_4", "FloodedHall/Obstacles", ""),
    };

    // Names of obstacle pieces inside secret rooms
    static readonly string[] ObstacleWords =
        { "Tendril", "Sweeper", "Barrel", "Cannon", "Crate", "SpikeBall", "Vent", "Puddle", "Slime", "Anchor", "Chain" };

    // Switches the obstacle pieces found anywhere inside the secret rooms. Returns how many.
    static int SetSecretObstacles(Transform parent, bool on)
    {
        int count = 0;
        foreach (Transform child in parent)
        {
            bool obstacle = false;
            if (!child.name.StartsWith("KillZone"))
                foreach (string word in ObstacleWords)
                    if (child.name.Contains(word)) { obstacle = true; break; }

            if (obstacle) { child.gameObject.SetActive(on); count++; }
            else count += SetSecretObstacles(child, on); // look inside (rooms, the RoomObstacles group)
        }
        return count;
    }

    [MenuItem("Tools/Other/Obstacles/Turn Off In All Levels", priority = 3000)]
    static void ObstaclesOff() => SetObstacles(false);

    [MenuItem("Tools/Other/Obstacles/Turn On In All Levels", priority = 3000)]
    static void ObstaclesOn() => SetObstacles(true);

    static void SetObstacles(bool on)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;
        string report = "";

        foreach (var level in ObstacleGroups)
        {
            string path = "Assets/Scenes/" + level.scene + ".unity";
            if (!File.Exists(path)) { report += level.scene + ": scene not found\n"; continue; }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = 0;
            Transform group = Find(scene, level.group);
            if (group)
            {
                group.gameObject.SetActive(true);
                foreach (Transform piece in group)
                {
                    if (piece.name.StartsWith("KillZone")) { piece.gameObject.SetActive(true); continue; }
                    piece.gameObject.SetActive(on);
                    changed++;
                }
            }
            Transform secrets = Find(scene, level.secretRooms);
            int secretChanged = secrets ? SetSecretObstacles(secrets, on) : 0;
            if (!group && !secrets) { report += level.scene + ": no obstacles found\n"; continue; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report += level.scene + ": " + changed + " obstacle(s) + " + secretChanged + " in secret rooms " + (on ? "on" : "off") + "\n";
        }

        if (!string.IsNullOrEmpty(startScene) && File.Exists(startScene))
            EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Obstacles", "Obstacles " + (on ? "turned on" : "turned off") + ":\n\n" + report +
            "\nKill zones (pits and water) always stay on.", "OK");
    }

    // Finds "Parent/Child" from the scene's root objects, even when it is switched off
    static Transform Find(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t; i++) t = t.Find(parts[i]);
            if (t) return t;
        }
        return null;
    }
}
