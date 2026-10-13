using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// One line: who is speaking and what they say
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}

// There should be only ONE DialogueManager in a scene. It shows the dialogue box.
// Press E for the next line. The box closes after the last line.
// Scripts can call DialogueManager.Ensure() to get one: if the scene has none, it makes one with its own box.
// While a dialogue is open the player can't move.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] GameObject panel;     // the whole dialogue box
    [SerializeField] TMP_Text speakerText; // speaker's name
    [SerializeField] TMP_Text bodyText;    // dialogue text

    public bool IsOpen { get; private set; }

    // True while any dialogue is showing (bosses wait instead of attacking)
    public static bool Busy => Instance && Instance.IsOpen;

    List<DialogueLine> lines;
    int index;
    int openedFrame;
    PlayerController2D player; // remembered; only searched for again when it is gone (a new scene)

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    // The scene's dialogue manager, or a new one with a simple box at the bottom of the screen
    public static DialogueManager Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("DialogueManager (auto)");
        var dm = go.AddComponent<DialogueManager>();
        dm.BuildBox();
        return dm;
    }

    void BuildBox()
    {
        var canvasGo = new GameObject("DialogueCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("DialogueBox", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0.2f, 0.24f);  // above the item bar and the power icons at the bottom
        rt.anchorMax = new Vector2(0.8f, 0.48f);  // a bit taller to fit the big "next" hint
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

        speakerText = Text("Speaker", new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.95f), 36f, new Color(1f, 0.85f, 0.35f));
        bodyText = Text("Line", new Vector2(0.03f, 0.22f), new Vector2(0.97f, 0.74f), 32f, Color.white);
        // Big and bright so players know how to move on (was size 22, grey)
        TMP_Text hint = Text("Hint", new Vector2(0.5f, 0.02f), new Vector2(0.97f, 0.22f), 38f, new Color(1f, 0.85f, 0.35f));
        hint.text = "[" + GameInput.KeyName(GameInput.GetKey(GameAction.Interact)) + "] next";
        hint.fontStyle = FontStyles.Bold;
        hint.alignment = TextAlignmentOptions.BottomRight;
        panel.SetActive(false);
    }

    TMP_Text Text(string name, Vector2 min, Vector2 max, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(panel.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        return t;
    }

    public void StartDialogue(List<DialogueLine> newLines)
    {
        if (IsOpen || newLines == null || newLines.Count == 0) return;

        lines = newLines;
        index = 0;
        IsOpen = true;
        openedFrame = Time.frameCount;

        if (panel) panel.SetActive(true);
        ShowLine();
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame || Time.timeScale == 0f) return;

        // The player waits while people talk
        if (!player) player = FindAnyObjectByType<PlayerController2D>();
        if (player) player.Root(0.15f);

        if (GameInput.InteractPressed) // E or controller Y
        {
            index++;
            if (index >= lines.Count)
            {
                IsOpen = false;
                if (panel) panel.SetActive(false);
            }
            else
            {
                ShowLine();
            }
        }
    }

    void ShowLine()
    {
        if (speakerText) speakerText.text = lines[index].speaker;
        if (bodyText) bodyText.text = lines[index].text;
    }
}