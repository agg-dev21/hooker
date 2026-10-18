using UnityEngine;
using UnityEngine.UI;
using static UiKit; // Panel, Fill, Label, WhiteSprite (shared UI helpers)

// Part of Companion (see Companion.cs for its settings and the main loop).
// The ability icon in the corner of the screen.
public partial class Companion
{
    // ---------------- Icon (bottom right, left of the chain icon) ----------------

    void BuildIcon()
    {
        iconRoot = new GameObject("CompanionIcon");
        var canvas = iconRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = iconRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        const float size = 100f;
        RectTransform frame = Panel("Frame", iconRoot.transform, new Color(0.1f, 0.08f, 0.05f, 0.85f));
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        frame.anchoredPosition = new Vector2(-340f, 80f);
        frame.sizeDelta = new Vector2(size, size);
        RectTransform border = Panel("Border", frame, new Color(1f, 0.85f, 0.45f));
        Fill(border, -4f);
        border.SetAsFirstSibling();

        // Her icon (it has its own frame drawn in), or a placeholder in her colour
        AbilityArt art = AbilityArt.Current;
        if (!AbilityArt.Dress(frame, border, art ? art.arielIcon : null))
        {
            RectTransform pic = Panel("Picture", frame, S.color);
            pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 0.5f);
            pic.sizeDelta = new Vector2(size * 0.3f, size * 0.55f);
        }

        RectTransform sweepRt = Panel("Cooldown", frame, new Color(0f, 0f, 0f, 0.7f));
        Fill(sweepRt, 0f);
        cooldownSweep = sweepRt.GetComponent<Image>();
        cooldownSweep.type = Image.Type.Filled;
        cooldownSweep.fillMethod = Image.FillMethod.Radial360;
        cooldownSweep.fillOrigin = (int)Image.Origin360.Top;
        cooldownSweep.fillClockwise = false;
        cooldownSweep.fillAmount = 0f;

        keyText = Label("Key", frame, "R", 28f, new Vector2(0f, -30f));
        newText = Label("New", frame, "NEW!", 24f, new Vector2(0f, size * 0.5f + 20f));
        newText.color = new Color(1f, 0.85f, 0.2f);
    }

    void UpdateIcon()
    {
        if (!iconRoot) return;
        cooldownSweep.fillAmount = Mathf.Clamp01(cooldownLeft / Mathf.Max(S.cooldown, 0.01f));
        keyText.text = GameInput.KeyName(GameInput.GetKey(GameAction.Companion));
        newText.gameObject.SetActive(PlayerPrefs.GetInt(FoundSave + ".new", 0) == 1);
    }
}
