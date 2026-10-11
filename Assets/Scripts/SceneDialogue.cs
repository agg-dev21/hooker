using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays a conversation once, shortly after the scene starts (for example before a boss fight).
// Bosses wait while it is showing (DialogueManager.Busy). Edit the lines in the Inspector.
public class SceneDialogue : MonoBehaviour
{
    [SerializeField] float delay = 0.6f;
    public List<DialogueLine> lines = new List<DialogueLine>();

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delay);
        if (lines == null || lines.Count == 0) yield break;
        DialogueManager dm = DialogueManager.Ensure();
        while (dm.IsOpen) yield return null;
        dm.StartDialogue(lines);
    }
}
