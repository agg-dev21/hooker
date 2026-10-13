using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The ending (scene Ending_Grave), after KK is beaten.
// A quiet cliff over the sea at dusk. Mom's grave, and on it her letter and her wedding ring.
//   1) Walk to the grave, press Interact (E / Y).
//   2) Raphael reads Mom's last letter (pages on old paper).
//   3) He takes the ring.
//   4) Ariel comes up the path. Press E to propose. Hearts, petals, the end.
//      (Only if the scene has no Ariel object does he keep the ring for "someday".)
//   5) THE END card, then back to the title screen.
// Every line is in EndingGrave.Words.cs (Letter, AtGrave, Proposal...) so it's easy to edit.
// The letter and THE END card are built in EndingGrave.Screens.cs.
public partial class EndingGrave : MonoBehaviour
{
    const string R = "Raphael", A = "Ariel";

    [Header("Scene objects (set by Tools > Build Ending)")]
    [SerializeField] Transform grave;
    [SerializeField] SpriteRenderer letterOnGrave;
    [SerializeField] SpriteRenderer ringOnGrave;
    [SerializeField] SpriteRenderer ariel;         // Ariel for the ending (only shown if she travels with you)
    [SerializeField] Sprite[] arielFrames;         // her idle frames (floating)
    [SerializeField] Vector2 arielStart = new Vector2(2f, 1.6f);
    [SerializeField] float arielStandOffset = -2.2f; // where she stops, next to Raphael
    [SerializeField] Sprite boxSprite;             // plain square for petals
    [SerializeField] string titleScene = "MainMenu";

    [Header("Look")]
    [SerializeField] Color petalColor = new Color(1f, 0.6f, 0.75f);
    [SerializeField] Color heartColor = new Color(1f, 0.35f, 0.55f);

    static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
    static readonly Color Paper = new Color(0.9f, 0.82f, 0.62f);
    static readonly Color Ink = new Color(0.2f, 0.12f, 0.06f);
    static readonly Color InkTitle = new Color(0.25f, 0.15f, 0.08f);
    static readonly Color Muted = new Color(0.72f, 0.69f, 0.78f);

    // ---------------- Running it ----------------

    Transform player;
    PlayerController2D controller;
    Rigidbody2D playerRb;
    TextMeshPro prompt;
    bool started, letterOpen;
    int letterPage;
    GameObject letterUi;
    TextMeshProUGUI letterBody, letterPageText;
    float nextPetal;

    IEnumerator Start()
    {
        controller = FindAnyObjectByType<PlayerController2D>();
        if (controller) { player = controller.transform; playerRb = controller.GetComponent<Rigidbody2D>(); }
        if (ariel) ariel.gameObject.SetActive(false);

        yield return null;
        // The travelling Ariel (Companion) is replaced by the one standing in this scene
        foreach (Companion c in FindObjectsByType<Companion>(FindObjectsInactive.Include)) Destroy(c.gameObject);

        prompt = WorldText("PressE", grave ? (Vector2)grave.position + Vector2.up * 2.4f : Vector2.zero, "Press E", 4f, Color.white);
        prompt.gameObject.SetActive(false);

        yield return new WaitForSeconds(1.2f);
        yield return Talk(Opening());
    }

    void Update()
    {
        // Petals drift down the whole time
        if (Time.time >= nextPetal && boxSprite) { nextPetal = Time.time + 0.35f; SpawnPetal(); }

        if (letterOpen)
        {
            if (GameInput.Pressed(GameAction.Interact) || GameInput.JumpPressed || GameInput.PausePressed) NextLetterPage();
            return;
        }

        if (started || !player || !grave) return;
        bool near = Mathf.Abs(player.position.x - grave.position.x) < 2.5f;
        if (prompt) prompt.gameObject.SetActive(near && !DialogueManager.Busy);
        if (near && !DialogueManager.Busy && GameInput.InteractPressed)
        {
            started = true;
            if (prompt) prompt.gameObject.SetActive(false);
            StartCoroutine(Ending());
        }
    }

    IEnumerator Ending()
    {
        Freeze(true);
        yield return Talk(AtGrave());

        // The letter
        if (letterOnGrave) letterOnGrave.enabled = false;
        OpenLetter();
        while (letterOpen) yield return null;
        yield return new WaitForSeconds(0.4f);
        yield return Talk(AfterLetter());

        // The ring
        if (ringOnGrave) yield return LiftRing();

        bool withAriel = ariel; // Ariel always comes up the path at the end (even if you jumped straight here to test)
        if (withAriel)
        {
            yield return ArielWalksIn();
            yield return Talk(ArielArrives());
            yield return Talk(BeforeProposal());
            yield return WaitToPropose();
            yield return Talk(Proposal());
            if (ringOnGrave) ringOnGrave.enabled = false; // the ring is on her finger now
            yield return Hearts();
        }
        else yield return Talk(Alone());

        yield return Talk(Goodbye(withAriel));
        yield return new WaitForSeconds(1f);
        yield return EndCard(withAriel);
    }

    // "Press E to propose" over Raphael, with the ring glowing in front of him, until the player presses it
    IEnumerator WaitToPropose()
    {
        Vector2 at = player ? (Vector2)player.position : Vector2.zero;
        TextMeshPro ask = WorldText("ProposePrompt", at + Vector2.up * 2.2f, "Press E to propose", 3.6f, Gold);
        if (ringOnGrave)
        {
            ringOnGrave.enabled = true;
            ringOnGrave.transform.position = at + new Vector2(arielStandOffset * 0.35f, 0.5f); // held out toward Ariel
        }
        float t = 0f;
        yield return null;
        while (!GameInput.InteractPressed)
        {
            t += Time.deltaTime;
            ask.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f + Mathf.PingPong(t, 0.4f));
            if (ringOnGrave) ringOnGrave.transform.localScale = Vector3.one * (ringScale * (1f + Mathf.Sin(t * 5f) * 0.08f));
            yield return null;
        }
        Destroy(ask.gameObject);
        StartCoroutine(Heart(at + Vector2.up * 1.2f));
    }

    void Freeze(bool on)
    {
        if (controller) controller.enabled = !on;
        if (on && playerRb) playerRb.linearVelocity = new Vector2(0f, playerRb.linearVelocity.y);
    }

    IEnumerator Talk(List<DialogueLine> lines)
    {
        DialogueManager dm = DialogueManager.Ensure();
        while (dm.IsOpen) yield return null;
        dm.StartDialogue(lines);
        yield return null;
        while (dm.IsOpen) yield return null;
        yield return new WaitForSeconds(0.25f);
    }

    // The ring rises off the grave, glints, and floats to Raphael
    float ringScale = 1f;

    IEnumerator LiftRing()
    {
        ringScale = ringOnGrave.transform.localScale.x;
        Vector3 from = ringOnGrave.transform.position;
        Vector3 to = player ? player.position + Vector3.up * 1.2f : from + Vector3.up;
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.6f);
            ringOnGrave.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
            ringOnGrave.transform.localScale = Vector3.one * (ringScale * (1f + Mathf.Sin(t * 12f) * 0.06f));
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);
        ringOnGrave.enabled = false;
    }

    // Ariel floats up the path from the left and stops next to Raphael
    IEnumerator ArielWalksIn()
    {
        ariel.gameObject.SetActive(true);
        Vector3 start = arielStart;
        Vector3 stop = new Vector3((player ? player.position.x : 20f) + arielStandOffset, arielStart.y, 0f);
        ariel.transform.position = start;
        ariel.flipX = false;
        Color c = ariel.color;
        float dist = Mathf.Abs(stop.x - start.x);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(1f, dist / 4.5f))
        {
            ariel.transform.position = Vector3.Lerp(start, stop, t) + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.12f;
            ariel.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(t * 4f));
            AnimateAriel();
            yield return null;
        }
        StartCoroutine(ArielIdle(stop));
    }

    IEnumerator ArielIdle(Vector3 at)
    {
        while (true)
        {
            ariel.transform.position = at + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.12f;
            AnimateAriel();
            yield return null;
        }
    }

    void AnimateAriel()
    {
        if (arielFrames != null && arielFrames.Length > 0)
            ariel.sprite = arielFrames[(int)(Time.time / 0.4f) % arielFrames.Length];
    }

    // Hearts rise between them
    IEnumerator Hearts()
    {
        Vector2 mid = player ? (Vector2)player.position + new Vector2(arielStandOffset * 0.5f, 1.5f) : Vector2.zero;
        for (int i = 0; i < 26; i++)
        {
            StartCoroutine(Heart(mid + new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(-0.4f, 0.4f))));
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(1.5f);
    }

    IEnumerator Heart(Vector2 from)
    {
        TextMeshPro h = WorldText("Heart", from, "\u2665", Random.Range(4f, 7f), heartColor);
        float drift = Random.Range(-0.6f, 0.6f);
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            h.transform.position = from + new Vector2(drift * t + Mathf.Sin(t * 4f) * 0.15f, t * 1.4f);
            h.color = new Color(heartColor.r, heartColor.g, heartColor.b, 1f - t / 2.2f);
            yield return null;
        }
        Destroy(h.gameObject);
    }

    void SpawnPetal()
    {
        Camera cam = Camera.main;
        if (!cam) return;
        float halfW = cam.orthographicSize * cam.aspect;
        var go = new GameObject("Petal");
        go.transform.position = new Vector3(cam.transform.position.x + Random.Range(-halfW, halfW + 3f), cam.transform.position.y + cam.orthographicSize + 0.5f, 0f);
        go.transform.localScale = new Vector3(0.18f, 0.12f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boxSprite;
        sr.color = Color.Lerp(petalColor, Color.white, Random.value * 0.3f);
        sr.sortingOrder = 15;
        StartCoroutine(Fall(go.transform, cam.orthographicSize * 2f + 1f));
    }

    IEnumerator Fall(Transform petal, float distance)
    {
        float speed = Random.Range(0.8f, 1.4f), sway = Random.Range(0.5f, 1.2f), seed = Random.value * 10f;
        Vector3 start = petal.position;
        for (float y = 0f; y < distance && petal; y += speed * Time.deltaTime)
        {
            petal.position = start + new Vector3(Mathf.Sin(Time.time * sway + seed) * 0.6f - y * 0.4f, -y, 0f);
            petal.rotation = Quaternion.Euler(0f, 0f, Time.time * 90f * sway + seed * 30f);
            yield return null;
        }
        if (petal) Destroy(petal.gameObject);
    }

    // ---------------- Helpers ----------------

    static TextMeshPro WorldText(string name, Vector2 pos, string text, float size, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var t = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset) t.font = TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
        t.GetComponent<MeshRenderer>().sortingOrder = 30;
        return t;
    }
}