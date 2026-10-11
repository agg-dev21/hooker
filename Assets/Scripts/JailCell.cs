using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The jail cell in Level 2's hidden room, where the companion is locked up.
// Stand at the cell and press Interact (E / controller Y by default): the bars slide up, she steps out,
// you talk (Lines, below), and she joins you (see Companion). If she was already freed, the cell is open and empty.
// Needs a trigger Collider2D (the area in front of the cell).
[RequireComponent(typeof(Collider2D))]
public class JailCell : MonoBehaviour
{
    [SerializeField] Transform bars;        // slide up when the cell opens
    [SerializeField] GameObject prisoner;   // placeholder of her inside the cell (replaced by the real companion)
    [SerializeField] GameObject prompt;     // "Press E" pop-up
    [SerializeField] float barsRise = 4f;
    [SerializeField] float openTime = 0.8f;
    [SerializeField] string joinMessage = "Ariel joins you! Press {key}, then click where her light falls.\nTap: 3 quick beams.  Hold: one big beam.  No click: she picks."; // {key} = her button
    [SerializeField] List<DialogueLine> lines = new List<DialogueLine>
    {
        new DialogueLine { speaker = "Ariel", text = "Finally! Do you know how long I've been in here?" },
        new DialogueLine { speaker = "Raphael", text = "Long enough to need a bath?" },
        new DialogueLine { speaker = "Ariel", text = "...You're not a guard. Or a lizard. That's a start." },
        new DialogueLine { speaker = "Raphael", text = "I'm looking for my father. Four names left on my list of suspects." },
        new DialogueLine { speaker = "Ariel", text = "Then you need someone watching your back. I'm coming with you." },
        new DialogueLine { speaker = "Ariel", text = "When it gets dangerous I'll stay out of sight. Call me, and I'll bring the light." },
    };

    bool playerNear;
    bool open;

    void Start()
    {
        if (prompt) prompt.SetActive(false);
        ShowAriel();
        if (Companion.Found)
        {
            open = true;
            if (bars) bars.position += Vector3.up * barsRise;
            if (prisoner) prisoner.SetActive(false);
        }
    }

    // The waiting prisoner shows Ariel's drawing (first idle frame) instead of the plain box, standing on the floor
    void ShowAriel()
    {
        CompanionSettings s = CompanionSettings.Current;
        if (!prisoner || !s || s.idleFrames == null || s.idleFrames.Length == 0) return;
        if (!prisoner.TryGetComponent(out SpriteRenderer sr)) return;
        Transform t = prisoner.transform;
        float floorY = t.position.y - Mathf.Abs(t.lossyScale.y) * 0.5f; // bottom of the old box
        sr.sprite = s.idleFrames[0];
        sr.color = Color.white;
        t.localScale = Vector3.one;
        t.position = new Vector3(t.position.x, floorY + sr.sprite.bounds.extents.y - sr.sprite.bounds.center.y, t.position.z);
    }

    void Update()
    {
        if (open || !playerNear || Time.timeScale == 0f) return;
        if (GameInput.InteractPressed) StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        open = true;
        if (prompt) prompt.SetActive(false);
        Vector3 from = bars ? bars.position : Vector3.zero;
        for (float t = 0f; t < openTime && bars; t += Time.deltaTime)
        {
            bars.position = Vector3.Lerp(from, from + Vector3.up * barsRise, t / openTime);
            yield return null;
        }
        Vector3 at = prisoner ? prisoner.transform.position : transform.position;
        if (prisoner) prisoner.SetActive(false);
        Companion.Join(at);
        Companion.StayFor(60f); // she stays out while you talk

        // The conversation (the player waits until it is over)
        if (lines != null && lines.Count > 0)
        {
            yield return null;
            DialogueManager dm = DialogueManager.Ensure();
            dm.StartDialogue(lines);
            while (dm && dm.IsOpen) yield return null;
        }
        Companion.StayFor(5f);
        PlayerItems.Toast(joinMessage.Replace("{key}", GameInput.KeyName(GameInput.GetKey(GameAction.Companion))), 4f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = true;
        if (prompt && !open) prompt.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController2D _)) return;
        playerNear = false;
        if (prompt) prompt.SetActive(false);
    }
}
