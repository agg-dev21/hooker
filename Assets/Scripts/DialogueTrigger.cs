using System.Collections.Generic;
using UnityEngine;

// Put this on any object (Box Collider 2D with Is Trigger ON).
// Write the Lines in the Inspector. The dialogue plays once when the player enters the area.
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] List<DialogueLine> lines = new List<DialogueLine>();

    bool played;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (played) return;
        if (!other.TryGetComponent(out PlayerController2D _)) return;

        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.IsOpen) return;

        played = true;
        dm.StartDialogue(lines);
    }
}
