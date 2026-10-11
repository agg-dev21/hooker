using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A conversation between Raphael and Ariel that plays once when the player walks through this spot,
// but only if Ariel is travelling with him (Companion.Found). She stays out while they talk.
// Needs a trigger Collider2D. Edit the lines in the Inspector.
[RequireComponent(typeof(Collider2D))]
public class CompanionTalk : MonoBehaviour
{
    public List<DialogueLine> lines = new List<DialogueLine>();
    [SerializeField] bool needsAriel = true; // false: plays even if she isn't with you

    bool played;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (played || !other.TryGetComponent(out PlayerController2D _)) return;
        if (needsAriel && !Companion.Found) return;
        if (lines == null || lines.Count == 0) return;
        played = true;
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        DialogueManager dm = DialogueManager.Ensure();
        while (dm.IsOpen) yield return null;
        Companion.StayFor(2f + lines.Count * 2.5f); // she shows herself while she talks
        dm.StartDialogue(lines);
    }
}
