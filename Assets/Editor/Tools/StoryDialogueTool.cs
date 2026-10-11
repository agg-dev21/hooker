using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only helper. Adds Tools > Add Story Dialogue.
// Puts the story conversations into the scenes (an object called "StoryDialogue" in each):
//   Tutorial_1_Move  Raphael's opening
//   Level_1_Deck     Hardy: before the fight and when he is beaten
//   Level_2_Boss     Oscar: before the fight and when he runs away
//   Level_3_Boss     Madame O and Oscar: before the fight, and each one's line when beaten
//   Level_4_Boss     KK: before the fight (the ending comes later)
// Hardy's and Oscar's levels come in a random order, so their lines work either way.
// Run it again any time: it replaces the old dialogue. To change a line later, edit it in the scene
// (StoryDialogue > Scene Dialogue / Defeat Dialogue > Lines) or here and run it again.
public static class StoryDialogueTool
{
    const string Hero = "Raphael";

    [MenuItem("Tools/Add Story Dialogue")]
    static void AddAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = SceneManager.GetActiveScene().path;
        string report = "";

        report += Put("Tutorial_1_Move", Lines(
            Hero, "Mom left when I was six. She wanted more men in her life.",
            Hero, "Apparently I wasn't one of them.",
            Hero, "I've hunted down every man I thought could be my father. All wrong.",
            Hero, "Four names left. Hardy. Oscar. Madame O. KK. One of them is my father."), null);

        report += Put("Level_1_Deck", Lines(
            "Hardy", "Your mother? I remember her. Tall. Loud. Took my wallet.",
            Hero, "Are you my father?",
            "Hardy", "Kid, I'm mostly goo now. I'm barely my own father."),
            new[] { (typeof(HardyBoss), Lines("Hardy", "Wasn't me... ask the others...")) });

        report += Put("Level_2_Boss", Lines(
            "Oscar", "Sssso. Another one of her kidsss.",
            Hero, "Another?",
            Hero, "Are you my father?",
            "Oscar", "Only one way to find out, little hook."),
            new[] { (typeof(PoisonBoss), Lines("Oscar", "Not your father! Ask Madame O! She was ssseeing her too!")) });

        report += Put("Level_3_Boss", Lines(
            "Madame O", "Your mother was the love of my life. For about three weeks.",
            Hero, "Then you can't be my father.",
            "Madame O", "Sweetie, with your mother, I stopped asking how things work.",
            "Oscar", "Ssssurprise. Round two."),
            new[]
            {
                (typeof(OctopusBoss), Lines("Madame O", "Fine. It's KK. He was always her favorite. He paid in fish.")),
                (typeof(PoisonBoss), Lines("Oscar", "Ssstill... not your father...")),
            });

        report += Put("Level_4_Boss", Lines(
            "KK", "I know why you're here, son.",
            Hero, "So it's true.",
            "KK", "I wanted to tell you. I just... let you fight three of her exes instead.",
            Hero, "Where is she?",
            "KK", "Get past me first."), null);

        if (!string.IsNullOrEmpty(start) && File.Exists(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Add Story Dialogue", report, "OK");
    }

    // Opens a scene, replaces its StoryDialogue object: intro lines, plus defeat lines for each boss type found
    static string Put(string sceneName, List<DialogueLine> intro, (System.Type boss, List<DialogueLine> lines)[] defeats)
    {
        string path = "Assets/Scenes/" + sceneName + ".unity";
        if (!File.Exists(path)) return sceneName + ": scene missing\n";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "StoryDialogue") Object.DestroyImmediate(r);
        var root = new GameObject("StoryDialogue");
        root.AddComponent<SceneDialogue>().lines = intro;

        string missing = "";
        if (defeats != null)
            foreach (var (type, lines) in defeats)
            {
                var boss = Object.FindAnyObjectByType(type, FindObjectsInactive.Include) as Component;
                if (!boss || !boss.TryGetComponent(out Health h)) { missing += " (no " + type.Name + ")"; continue; }
                var go = new GameObject("DefeatDialogue_" + boss.name);
                go.transform.SetParent(root.transform);
                var d = go.AddComponent<DefeatDialogue>();
                d.SetBoss(h);
                d.lines = lines;
                EditorUtility.SetDirty(d);
            }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return sceneName + ": done" + missing + "\n";
    }

    // Lines("Speaker", "text", "Speaker", "text", ...)
    static List<DialogueLine> Lines(params string[] pairs)
    {
        var list = new List<DialogueLine>();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
            list.Add(new DialogueLine { speaker = pairs[i], text = pairs[i + 1] });
        return list;
    }
}
