using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared helpers for UI that is built from code (the item bar and inventory,
/// the ability icons, the companion's icon). Before, each of those scripts had
/// its own copy of these methods; now they all use this one.
///
/// Add <c>using static UiKit;</c> at the top of a script to call them as
/// Panel(...), Fill(...), Label(...) and WhiteSprite.
/// </summary>
public static class UiKit
{
    static Sprite whiteSprite;

    /// <summary>A plain white sprite (made the first time it is needed, then shared).</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (!whiteSprite)
                whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return whiteSprite;
        }
    }

    /// <summary>A plain coloured box (an Image that ignores clicks).</summary>
    public static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    /// <summary>Stretches a box over its whole parent, leaving a gap of inset on every side.</summary>
    public static void Fill(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>
    /// A centred white text. Anchored to the middle of the parent, or to its bottom
    /// edge when pos.y is below zero.
    /// </summary>
    public static TMP_Text Label(string name, Transform parent, string text, float size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, pos.y < 0f ? 0f : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(200f, 40f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
