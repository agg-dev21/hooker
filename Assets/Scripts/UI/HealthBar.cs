using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A health bar: the fill image empties as the target loses health (it is a Filled image, so any amount of
// damage shows smoothly, no animation needed). If the health bar art is set up (HealthBarSkin), the bar uses it:
// Raphael's look for the player's bar (top-left), the boss look for every other bar (top middle, stacked if
// there are two bosses, name under the bar). The art is scaled evenly so nothing stretches or overlaps.
public class HealthBar : MonoBehaviour
{
    [SerializeField] Health target; // whose health to show
    [SerializeField] Image fill;    // the bar image that fills and empties

    const float Margin = 16f;        // space from the screen edge
    const float PlayerWidth = 0.24f; // the player's frame is this share of the screen width (top-left)
    const float BossWidth = 0.32f;   // a boss frame is this share of the screen width (top middle), so they never touch
    const float BossNameHeight = 30f;

    void Start() => ApplySkin();

    void Update()
    {
        if (!target) { gameObject.SetActive(false); return; } // its boss is gone (beaten, or already beaten earlier)
        if (!fill) return;
        fill.fillAmount = (float)target.CurrentHealth / target.MaxHealth;
    }

    void ApplySkin()
    {
        HealthBarSkin skin = HealthBarSkin.Current;
        if (!skin || !fill) return;
        bool isPlayer = target && target.GetComponent<PlayerController2D>();
        HealthBarSkin.Look look = isPlayer ? skin.player : skin.ForBoss(target);
        if (look == null || !look.frame) return;
        var root = fill.rectTransform.parent as RectTransform; // the bar's background box
        if (!root) return;

        // Size from the screen (canvas) width, so it looks the same on every screen and never overlaps
        RectTransform canvasRt = fill.canvas ? fill.canvas.rootCanvas.transform as RectTransform : null;
        float screenWidth = canvasRt ? canvasRt.rect.width : 1920f;
        float k = (isPlayer ? PlayerWidth : BossWidth) * screenWidth / Mathf.Max(look.framePixels.x, 1f);
        Rect win = look.windowPixels;
        Vector2 frameSize = look.framePixels * k;
        float windowTop = win.y * k;                                     // frame top -> window top
        float windowBottom = (look.framePixels.y - win.y - win.height) * k; // window bottom -> frame bottom
        float windowLeft = win.x * k;

        // The background box becomes exactly the window, and the fill covers it
        float oldY = root.anchoredPosition.y;
        root.sizeDelta = new Vector2(win.width * k, win.height * k);
        if (root.TryGetComponent(out Image bg))
        {
            bg.sprite = null;
            bg.color = look.fillOnTop ? Color.clear : new Color(0.05f, 0.04f, 0.07f, 0.9f); // the empty bar art is the background
        }
        RectTransform frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        fill.sprite = look.fill ? look.fill : UiKit.WhiteSprite; // no fill picture: a plain bar in the look's colour
        fill.color = look.fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;

        if (isPlayer)
        {
            // Frame's top-left corner a margin away from the screen's top-left corner
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(Margin + windowLeft, -(Margin + windowTop));
        }
        else
        {
            // Top middle; a second boss bar (it was placed lower) goes in the slot under the first
            int slot = oldY < -60f ? 1 : 0;
            float slotHeight = frameSize.y + BossNameHeight + 8f;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            float frameCenterOffset = (win.x + win.width * 0.5f - look.framePixels.x * 0.5f) * k; // window vs frame centre
            root.anchoredPosition = new Vector2(frameCenterOffset, -(Margin + slot * slotHeight + windowTop));

            // The name goes under the frame
            Transform name = root.Find("Name");
            if (name is RectTransform nrt)
            {
                nrt.anchorMin = new Vector2(0f, 0f);
                nrt.anchorMax = new Vector2(1f, 0f);
                nrt.pivot = new Vector2(0.5f, 1f);
                nrt.anchoredPosition = new Vector2(0f, -windowBottom);
                nrt.sizeDelta = new Vector2(0f, BossNameHeight);
                if (name.TryGetComponent(out TMP_Text t)) { t.fontSize = 22f; t.alignment = TextAlignmentOptions.Center; }
            }
        }

        // The frame on top, its window exactly over the fill (a child of the fill, so the fill's cut doesn't cut it).
        // Fill On Top: the frame is the empty bar, behind the fill (first child of the background box).
        Transform old = fill.transform.Find("Frame");
        if (old) Destroy(old.gameObject);
        old = root.Find("Frame");
        if (old) Destroy(old.gameObject);
        var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(look.fillOnTop ? root : fill.transform, false);
        if (look.fillOnTop) go.transform.SetAsFirstSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = frameSize;
        float cx = (win.x + win.width * 0.5f - look.framePixels.x * 0.5f) * k;  // window centre vs frame centre (px, y up)
        float cy = (look.framePixels.y * 0.5f - (win.y + win.height * 0.5f)) * k;
        rt.anchoredPosition = new Vector2(-cx, -cy);
        var img = go.GetComponent<Image>();
        img.sprite = look.frame;
        img.raycastTarget = false;
    }
}
