using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// On the boat between bosses: "Where to?". The first choice is the next level of the story; below it are the
// levels whose boss you already beat, to go back and explore (find Ariel, secret rooms, chests...).
// Up / Down (or W / S, or the mouse) to pick, Interact (E), Enter or a click to choose. Then steer the boat off
// the screen as usual. Only shown when there is somewhere to go back to. Shown by SailMonologue.
public class SailMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    static readonly (string scene, string name)[] Levels =
    {
        ("Level_1", "Hardy's ship"),
        ("Level_2", "Oscar's poison tower"),
        ("Level_3", "The sunken prison"),
        ("Level_4", "KK's flooded hall"),
    };

    readonly List<string> choices = new List<string>();
    readonly List<RectTransform> rows = new List<RectTransform>();
    readonly List<TMP_Text> texts = new List<TMP_Text>();
    int picked;
    static Sprite white;

    public static void Show()
    {
        // Somewhere to go back to? (the levels whose boss is beaten, other than where the story goes next)
        string next = RunOrder.NextLevel;
        bool any = false;
        foreach (var l in Levels) if (l.scene != next && RunOrder.LevelDone(l.scene)) any = true;
        if (!any) { RunOrder.SailTo(next); return; }
        new GameObject("SailMenu").AddComponent<SailMenu>();
    }

    static string NameOf(string scene)
    {
        foreach (var l in Levels) if (l.scene == scene) return l.name;
        return scene;
    }

    void Awake()
    {
        IsOpen = true;
        if (!white) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        string next = RunOrder.NextLevel;
        choices.Add(next);
        foreach (var l in Levels) if (l.scene != next && RunOrder.LevelDone(l.scene)) choices.Add(l.scene);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        float h = 90f + choices.Count * 64f + 40f;
        RectTransform box = Panel("Box", transform, new Color(0.04f, 0.03f, 0.07f, 0.92f));
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.55f);
        box.sizeDelta = new Vector2(760f, h);
        TMP_Text title = Text("Title", box, "Where to?", 40f, new Vector2(0f, h * 0.5f - 45f), new Color(1f, 0.85f, 0.35f));
        title.rectTransform.sizeDelta = new Vector2(700f, 50f);

        for (int i = 0; i < choices.Count; i++)
        {
            RectTransform row = Panel("Choice", box, Color.clear);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(680f, 54f);
            row.anchoredPosition = new Vector2(0f, h * 0.5f - 110f - i * 64f);
            string label = i == 0 ? "Next:  " + NameOf(choices[i]) : "Go back:  " + NameOf(choices[i]);
            TMP_Text t = Text("Label", row, label, 30f, Vector2.zero, Color.white);
            t.rectTransform.sizeDelta = new Vector2(660f, 50f);
            rows.Add(row);
            texts.Add(t);
        }
        TMP_Text keys = Text("Keys", box, "Up / Down to pick,  " + GameInput.KeyName(GameInput.GetKey(GameAction.Interact)) + " or Enter to choose",
            20f, new Vector2(0f, -h * 0.5f + 24f), new Color(0.7f, 0.68f, 0.78f));
        keys.rectTransform.sizeDelta = new Vector2(700f, 30f);
        Highlight();
    }

    void OnDestroy() => IsOpen = false;

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
        bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
        if (up) { picked = (picked + choices.Count - 1) % choices.Count; Highlight(); }
        if (down) { picked = (picked + 1) % choices.Count; Highlight(); }

        Mouse m = Mouse.current;
        if (m != null)
            for (int i = 0; i < rows.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(rows[i], m.position.ReadValue(), null))
                {
                    if (picked != i) { picked = i; Highlight(); }
                    if (m.leftButton.wasPressedThisFrame) { Choose(); return; }
                }

        if (GameInput.InteractPressed || (kb != null && kb.enterKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
            Choose();
    }

    void Choose()
    {
        RunOrder.SailTo(choices[picked]);
        PlayerItems.Toast(picked == 0 ? "Sailing on: " + NameOf(choices[picked]) : "Sailing back to " + NameOf(choices[picked]) + "\nWalk back to the start of the level to return to the boat.", 4f);
        Destroy(gameObject);
    }

    void Highlight()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].GetComponent<Image>().color = i == picked ? new Color(1f, 0.85f, 0.35f, 0.9f) : new Color(1f, 1f, 1f, 0.06f);
            texts[i].color = i == picked ? new Color(0.1f, 0.06f, 0.1f) : Color.white;
        }
    }

    static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = white;
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    static TMP_Text Text(string name, Transform parent, string text, float size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }
}
