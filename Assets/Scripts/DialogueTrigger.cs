using System.Collections.Generic;
using UnityEngine;

// Isko kisi bhi object pe lagao (Box Collider 2D, Is Trigger ON).
// Inspector mein Lines likho. Player area mein aaye toh dialogue ek baar chalega.
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
