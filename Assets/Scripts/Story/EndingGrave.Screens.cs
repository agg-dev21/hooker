using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// Part of EndingGrave (see EndingGrave.cs for its settings and the main loop).
// The two full-screen pages: Mom's letter (on old paper) and THE END card, plus the UI helpers that build them.
public partial class EndingGrave
{
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
        Fill(Panel("Dim", letterUi.transform, new Color(0f, 0f, 0f, 0.65f)), 0f);
        var paper = Panel("Paper", letterUi.transform, Paper);
        paper.sizeDelta = new Vector2(980f, 760f);
        var edge = Panel("Edge", paper, new Color(0.7f, 0.6f, 0.42f));
        Fill(edge, 14f);
        var inner = Panel("Inner", edge, Paper);
        Fill(inner, 4f);

        var title = UIText("Title", inner, "For my son", 46f, InkTitle, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Italic | FontStyles.Bold;
        title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f); title.rectTransform.sizeDelta = new Vector2(0f, 80f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -30f);

        letterBody = UIText("Body", inner, "", 32f, Ink, TextAlignmentOptions.TopLeft);
        letterBody.fontStyle = FontStyles.Italic;
        letterBody.lineSpacing = 8f;
        Fill(letterBody.rectTransform, 0f);
        letterBody.rectTransform.offsetMin = new Vector2(70f, 80f);
        letterBody.rectTransform.offsetMax = new Vector2(-70f, -120f);

        letterPageText = UIText("Page", inner, "", 24f, new Color(0.45f, 0.35f, 0.2f), TextAlignmentOptions.Bottom);
        Fill(letterPageText.rectTransform, 0f);
        letterPageText.rectTransform.offsetMin = new Vector2(0f, 24f);
        letterUi.SetActive(false);
    }

    // ---------------- The end ----------------

    IEnumerator EndCard(bool withAriel)
    {
        GameObject card = Canvas("EndCard", 1000);
        Image black = Panel("Black", card.transform, new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
        Fill(black.rectTransform, 0f);
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

    // ---------------- UI helpers ----------------

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
}