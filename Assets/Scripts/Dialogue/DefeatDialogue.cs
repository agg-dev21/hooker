using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Put it on a boss (next to its Health): when the boss is beaten (health 0, even if it runs away instead of
// dying), these lines play once. It sits on its own helper object so it still works after the boss is gone.
public class DefeatDialogue : MonoBehaviour
{
    [SerializeField] Health boss;
    public List<DialogueLine> lines = new List<DialogueLine>();
    bool done;

    public void SetBoss(Health h) => boss = h;

    void Update()
    {
        if (done || !boss) { if (!done && boss == null && started) Play(); return; }
        started = true;
        if (boss.CurrentHealth <= 0) Play();
    }

    bool started; // the boss existed once (so a missing boss now means it was beaten and removed)

    void Play()
    {
        done = true;
        if (lines != null && lines.Count > 0) StartCoroutine(Show());
    }

    IEnumerator Show()
    {
        yield return new WaitForSeconds(0.4f);
        DialogueManager dm = DialogueManager.Ensure();
        while (dm.IsOpen) yield return null;
        dm.StartDialogue(lines);
    }
}
