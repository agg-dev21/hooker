using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The ending (scene Ending_Grave), after KK is beaten.
// A quiet cliff over the sea at dusk. Mom's grave, and on it her letter and her wedding ring.
//   1) Walk to the grave, press Interact (E / Y).
//   2) Raphael reads Mom's last letter (pages on old paper).
//   3) He takes the ring.
//   4) Ariel comes up the path. Press E to propose. Hearts, petals, the end.
//      (Only if the scene has no Ariel object does he keep the ring for "someday".)
//   5) THE END card, then back to the title screen.
// Every line is in this file (Letter, AtGrave, Proposal...) so it's easy to edit.
public class EndingGrave : MonoBehaviour
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

    // ---------------- The words ----------------

    static readonly string[] Letter =
    {
        "My little Raphael,\n\n" +
        "If you're reading this, you found me. I told you that you'd understand someday. " +
        "I hope you don't understand all of it.\n\n" +
        "I wasn't a good mother. I left because I was scared. Not of you. Of staying. " +
        "Everyone else, I could sail away from. You, I couldn't. So I ran from myself and called it freedom.",

        "They'll have told you stories about me. Most of them are true.\n\n" +
        "Hardy wrote me terrible poems. Oscar kept everything I ever left behind. " +
        "Madame O loved me more than I deserved.\n\n" +
        "And KK... KK made me laugh, and he never once asked me to stay. " +
        "I should have asked him to.",

        "They call you the Flaw of the Seven Seas. Don't you dare believe them.\n\n" +
        "I followed you from far away, more than you know. Every port you sailed into, " +
        "I heard about the boy with the hook who wouldn't give up.\n\n" +
        "You were never my mistake, sweetheart. You were the only thing I ever got right.",

        "The ring is my wedding ring. Nobody ever put it on my finger. " +
        "I bought it myself, the day you were born, and promised I'd give it to the one who stays.\n\n" +
        "That's you. You stayed, even after I didn't.\n\n" +
        "Don't keep it in a drawer. Give it to someone who will stay with you. " +
        "And when you find them, stay too. Don't be like me.",

        "I'm sorry. I love you. I always did.\n\n" +
        "Mom\n\n\n" +
        "P.S. Your father still owes me eleven fish. Collect them."
    };

    List<DialogueLine> Opening() => L(
        R, "The cliff. Just like KK said.",
        R, "...");

    List<DialogueLine> AtGrave() => L(
        R, "Hi, Mom.",
        R, "You said, find me when you're ready.",
        R, "I don't think I was ever going to be ready. I came anyway.",
        R, "There's a letter. It has my name on it.");

    List<DialogueLine> AfterLetter() => L(
        R, "...",
        R, "Eleven fish. Of course.",
        R, "And her ring. She kept it all these years.");

    List<DialogueLine> ArielArrives() => L(
        A, "Raphael?",
        A, "I waited at the bottom of the path. I thought you'd want to be alone. But you've been up here a long time.",
        R, "She left me a letter. And this.",
        A, "...Her ring.",
        A, "Are you okay?",
        R, "No. But I think I'm going to be.");

    List<DialogueLine> BeforeProposal() => L(
        R, "She said to give it to someone who will stay.",
        R, "You followed me across Hardy's ship, a poison tower, a sunken prison, and a hallway full of water. You never once asked me to turn back.",
        A, "You never once let me fall behind.",
        R, "I spent my whole life looking for where I came from.",
        R, "Turns out I was looking in the wrong direction. The answer was walking right next to me.");

    // After the player presses E to propose
    List<DialogueLine> Proposal() => L(
        "", "(Raphael kneels in the grass, the ring held out in his open hand.)",
        R, "Ariel. I don't know how to be anyone's family. Nobody ever showed me how.",
        R, "But I want to learn it. With you.",
        R, "Will you marry me?",
        A, "...",
        A, "You absolute idiot. You proposed at your mother's grave.",
        R, "She'd have wanted a front-row seat.",
        A, "My kingdom gave me titles. Rules. A cage.",
        A, "You gave me a choice.",
        A, "So I choose you. Yes, Raphael. I'll stay.",
        R, "Then I'll stay too.",
        A, "Now get up. The ground is wet, and you're crying on your mother's flowers.",
        R, "...Worth it.");

    List<DialogueLine> Alone() => L(
        R, "Someone who will stay...",
        R, "I'll keep it safe, Mom. Not in a drawer. Someday.",
        R, "I promise.");

    List<DialogueLine> Goodbye(bool withAriel) => withAriel
        ? L(A, "Come on. Let's go home.", R, "Home?", A, "Wherever we're going. Together. That's home now.", R, "Bye, Mom.")
        : L(R, "Bye, Mom.", R, "Thanks for waiting for me.");

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

    // ---------------- The letter ----------------

    void OpenLetter()
    {
        if (!letterUi) BuildLetter();
        letterUi.SetActive(true);
        letterOpen = true;
        letterPage = 0;
        ShowPage();
    }

    void NextLetterPage()
    {
        letterPage++;
        if (letterPage >= Letter.Length) { letterOpen = false; letterUi.SetActive(false); return; }
        ShowPage();
    }

    void ShowPage()
    {
        letterBody.text = Letter[letterPage];
        letterPageText.text = (letterPage + 1) + " / " + Letter.Length + (letterPage == Letter.Length - 1 ? "     E: fold the letter" : "     E: next page");
    }

    void BuildLetter()
    {
        letterUi = Canvas("LetterCanvas", 900);
        var dim = Panel("Dim", letterUi.transform, new Color(0f, 0f, 0f, 0.65f));
        Stretch(dim.rectTransform);
        var paper = Panel("Paper", letterUi.transform, Paper);
        paper.rectTransform.sizeDelta = new Vector2(980f, 760f);
        var edge = Panel("Edge", paper.transform, new Color(0.7f, 0.6f, 0.42f));
        Stretch(edge.rectTransform); edge.rectTransform.offsetMin = new Vector2(14f, 14f); edge.rectTransform.offsetMax = new Vector2(-14f, -14f);
        var inner = Panel("Inner", edge.transform, Paper);
        Stretch(inner.rectTransform); inner.rectTransform.offsetMin = new Vector2(4f, 4f); inner.rectTransform.offsetMax = new Vector2(-4f, -4f);

        var title = UIText("Title", inner.transform, "For my son", 46f, InkTitle, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Italic | FontStyles.Bold;
        title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f); title.rectTransform.sizeDelta = new Vector2(0f, 80f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -30f);

        letterBody = UIText("Body", inner.transform, "", 32f, Ink, TextAlignmentOptions.TopLeft);
        letterBody.fontStyle = FontStyles.Italic;
        letterBody.lineSpacing = 8f;
        Stretch(letterBody.rectTransform);
        letterBody.rectTransform.offsetMin = new Vector2(70f, 80f);
        letterBody.rectTransform.offsetMax = new Vector2(-70f, -120f);

        letterPageText = UIText("Page", inner.transform, "", 24f, new Color(0.45f, 0.35f, 0.2f), TextAlignmentOptions.Bottom);
        Stretch(letterPageText.rectTransform);
        letterPageText.rectTransform.offsetMin = new Vector2(0f, 24f);
        letterUi.SetActive(false);
    }

    // ---------------- The end ----------------

    IEnumerator EndCard(bool withAriel)
    {
        GameObject card = Canvas("EndCard", 1000);
        var black = Panel("Black", card.transform, new Color(0f, 0f, 0f, 0f));
        Stretch(black.rectTransform);
        var end = UIText("TheEnd", card.transform, "THE END", 120f, new Color(Gold.r, Gold.g, Gold.b, 0f), TextAlignmentOptions.Center);
        end.fontStyle = FontStyles.Bold;
        end.characterSpacing = 14f;
        end.rectTransform.sizeDelta = new Vector2(1400f, 200f);
        end.rectTransform.anchoredPosition = new Vector2(0f, 80f);
        var sub = UIText("Names", card.transform, withAriel ? "Raphael \u2665 Ariel" : "Raphael", 48f, new Color(1f, 0.6f, 0.75f, 0f), TextAlignmentOptions.Center);
        sub.rectTransform.sizeDelta = new Vector2(1400f, 80f);
        sub.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        var thanks = UIText("Thanks", card.transform, "Thank you for playing HOOKER", 32f, new Color(Muted.r, Muted.g, Muted.b, 0f), TextAlignmentOptions.Center);
        thanks.rectTransform.sizeDelta = new Vector2(1400f, 60f);
        thanks.rectTransform.anchoredPosition = new Vector2(0f, -150f);
        var hint = UIText("Hint", card.transform, "Press E to return to the title", 24f, new Color(Muted.r, Muted.g, Muted.b, 0f), TextAlignmentOptions.Center);
        hint.rectTransform.sizeDelta = new Vector2(1400f, 50f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -420f);

        for (float t = 0f; t < 2f; t += Time.deltaTime) { black.color = new Color(0f, 0f, 0f, t / 2f); yield return null; }
        yield return FadeTextIn(end, 1.2f);
        yield return FadeTextIn(sub, 1f);
        yield return FadeTextIn(thanks, 1f);
        yield return FadeTextIn(hint, 0.6f);
        RunOrder.FinishGame(); // the game is done: the title screen now offers a New Game
        while (!(GameInput.InteractPressed || GameInput.JumpPressed || GameInput.PausePressed)) yield return null;
        ScreenFade.FadeToScene(titleScene);
    }

    static IEnumerator FadeTextIn(TextMeshProUGUI text, float time)
    {
        Color c = text.color;
        for (float t = 0f; t < time; t += Time.deltaTime) { text.color = new Color(c.r, c.g, c.b, t / time); yield return null; }
        text.color = new Color(c.r, c.g, c.b, 1f);
    }

    // ---------------- Helpers ----------------

    static List<DialogueLine> L(params string[] pairs)
    {
        var list = new List<DialogueLine>();
        for (int i = 0; i + 1 < pairs.Length; i += 2) list.Add(new DialogueLine { speaker = pairs[i], text = pairs[i + 1] });
        return list;
    }

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

    static GameObject Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(UnityEngine.Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    static Image Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI UIText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset) t.font = TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}   