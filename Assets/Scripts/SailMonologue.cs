using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// On the boat ride after a boss (RunOrder.SailScene), Raphael talks to himself about what he learned from
// the boss he just beat, and who is next. If Ariel is with him, she answers.
// Adds itself to the boat scene, nothing to set up. The lines are below: change them here.
public class SailMonologue : MonoBehaviour
{
    const string R = "Raphael", A = "Ariel";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != RunOrder.SailScene || FindAnyObjectByType<SailMonologue>()) return;
        new GameObject("SailMonologue").AddComponent<SailMonologue>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1.2f); // let the scene fade in first
        List<DialogueLine> lines = LinesFor(RunOrder.LastBoss);
        if (lines.Count == 0) yield break;
        DialogueManager dm = DialogueManager.Ensure();
        while (dm.IsOpen) yield return null;
        dm.StartDialogue(lines);
    }

    static List<DialogueLine> LinesFor(string boss)
    {
        var l = new List<DialogueLine>();
        bool ariel = Companion.Found;
        switch (boss)
        {
            case "Hardy":
                Add(l, R, "Hardy wasn't my father. Unless goo counts as family.");
                Add(l, R, "Lesson learned: never let a man made of tentacles get close. He hits like a bad breakup.");
                Add(l, R, "And the spikes. When the ground starts shaking, MOVE.");
                Add(l, R, "Took his spikes for myself, though. Mom always said take what you can get.");
                if (ariel) { Add(l, A, "You talk to yourself a lot."); Add(l, R, "You get used to it."); }
                Add(l, R, RunOrder.OscarDone ? "Two names down. Madame O is next." : "Next name on the list: Oscar.");
                break;

            case "Oscar":
                Add(l, R, "Oscar ran. Of course he ran. Mom always said he was fast. I see why now.");
                Add(l, R, "Lesson learned: never stand in lizard spit. Ever.");
                Add(l, R, "And when he curls up into a ball, get out of the way.");
                Add(l, R, "At least his tooth makes my hook poisonous. Thanks, maybe-not-dad.");
                if (ariel) { Add(l, A, "He's coming back, you know. Lizards always come back."); Add(l, R, "Great. Can't wait."); }
                Add(l, R, RunOrder.HardyDone ? "Two names down. Madame O is next. Oscar said she was seeing Mom too..." : "Next stop: Hardy's ship.");
                break;

            case "Madame O":
                Add(l, R, "Madame O. Not my father, obviously. But she knew who was.");
                Add(l, R, "KK. The one Mom liked best. The one who paid in fish.");
                Add(l, R, "Lesson learned: when someone throws chains at you, don't stand still. Don't stand ANYWHERE.");
                Add(l, R, "Her chain is mine now. Let's see how my father likes being held in place.");
                if (ariel) { Add(l, A, "Are you ready to meet him?"); Add(l, R, "No. Let's go anyway."); }
                Add(l, R, "Last name on the list. Time to meet my father.");
                break;
        }
        return l;
    }

    static void Add(List<DialogueLine> l, string who, string text) => l.Add(new DialogueLine { speaker = who, text = text });
}
