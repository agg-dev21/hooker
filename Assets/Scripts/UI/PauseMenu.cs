using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Pause menu built entirely from code, so every scene gets it with no setup.
// GameManager creates it automatically when its Pause Panel slot is empty.
//   Main page:     Resume, Controls, Restart, Quit
//   Controls page: change the keyboard key and controller button for every action (saved between sessions)
// Works with mouse, keyboard (arrows + Enter) and controller (stick / D-pad + A, B to go back).
public class PauseMenu : MonoBehaviour
{
    public bool IsOpen => root && root.activeSelf;
    public bool IsListening { get; private set; }
    public bool InControls => controlsPage && controlsPage.activeSelf;

    const float ListenTimeout = 5f; // seconds to wait for a new key before giving up

    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
    static readonly Color PanelColor = new Color(0.10f, 0.08f, 0.14f, 0.97f);
    static readonly Color ButtonColor = new Color(0.24f, 0.20f, 0.32f);
    static readonly Color SelectedColor = new Color(0.55f, 0.32f, 0.85f);
    static readonly Color FixedColor = new Color(0.65f, 0.65f, 0.7f);
    const string DefaultHint = "Select a key or button and press the new one. It is saved automatically.";

    GameManager gm;
    System.Action controlsBack; // set when the title screen opens the Controls page directly
    GameObject root, mainPage, controlsPage;
    CanvasGroup controlsGroup;
    TextMeshProUGUI hint;
    Button resumeButton;
    readonly Dictionary<GameAction, Button> keyButtons = new Dictionary<GameAction, Button>();
    readonly Dictionary<GameAction, Button> padButtons = new Dictionary<GameAction, Button>();
    readonly Dictionary<GameAction, TextMeshProUGUI> keyLabels = new Dictionary<GameAction, TextMeshProUGUI>();
    readonly Dictionary<GameAction, TextMeshProUGUI> padLabels = new Dictionary<GameAction, TextMeshProUGUI>();

    public static PauseMenu Create(GameManager gameManager)
    {
        var go = new GameObject("PauseMenu (runtime)");
        var menu = go.AddComponent<PauseMenu>();
        menu.gm = gameManager;
        menu.Build();
        menu.root.SetActive(false);
        return menu;
    }

    // ---------------- Open / close ----------------

    public void Open()
    {
        root.SetActive(true);
        ShowMain();
    }

    public void Close()
    {
        StopAllCoroutines();
        IsListening = false;
        root.SetActive(false);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    // Title screen: open straight on the Controls page; Back closes it and calls onBack
    public void OpenControls(System.Action onBack)
    {
        controlsBack = onBack;
        root.SetActive(true);
        ShowControls();
    }

    // Back from the Controls page (button, controller B, or the pause key)
    public void Back()
    {
        if (controlsBack == null) { ShowMain(); return; }
        System.Action onBack = controlsBack;
        controlsBack = null;
        Close();
        onBack();
    }

    public void ShowMain()
    {
        mainPage.SetActive(true);
        controlsPage.SetActive(false);
        Select(resumeButton);
    }

    void ShowControls()
    {
        mainPage.SetActive(false);
        controlsPage.SetActive(true);
        RefreshLabels();
        hint.text = DefaultHint;
        Select(keyButtons[GameAction.MoveLeft]);
    }

    void Update()
    {
        // Controller B goes back from the Controls page
        Gamepad gp = Gamepad.current;
        if (InControls && !IsListening && gp != null && gp.buttonEast.wasPressedThisFrame)
            Back();
    }

    // ---------------- Rebinding ----------------

    void StartRebind(GameAction action, bool controller)
    {
        if (!IsListening) StartCoroutine(Listen(action, controller));
    }

    IEnumerator Listen(GameAction action, bool controller)
    {
        IsListening = true;
        controlsGroup.interactable = false; // menu ignores input while we wait for the new key
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        TextMeshProUGUI label = controller ? padLabels[action] : keyLabels[action];
        label.text = controller ? "Press a button..." : "Press a key...";
        hint.text = controller
            ? "Press the new controller button for " + GameInput.ActionName(action) + " (wait 5 s to cancel)"
            : "Press the new key or extra mouse button for " + GameInput.ActionName(action) + " (Esc to cancel)";

        yield return null;
        yield return WaitForRelease(); // ignore the press that opened this

        float t = 0f;
        while (t < ListenTimeout)
        {
            if (controller)
            {
                if (PressedPadButton(out GamepadButton button)) { GameInput.SetPadButton(action, button); break; }
            }
            else
            {
                Key key = PressedKey();
                if (key == Key.Escape && action != GameAction.Pause) break; // cancel
                if (key != Key.None) { GameInput.SetKey(action, key); break; }
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return WaitForRelease(); // so the new key doesn't also press a menu button

        IsListening = false;
        controlsGroup.interactable = true;
        hint.text = DefaultHint;
        RefreshLabels();
        Select(controller ? padButtons[action] : keyButtons[action]);
    }

    IEnumerator WaitForRelease()
    {
        float t = 0f;
        while (AnythingHeld() && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static bool AnythingHeld()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        foreach (Key mk in GameInput.MouseKeys)
        {
            var mb = GameInput.MouseButton(mk);
            if (mb != null && mb.isPressed) return true;
        }
        Gamepad gp = Gamepad.current;
        if (gp != null)
            foreach (GamepadButton b in GameInput.BindablePadButtons)
                if (gp[b].isPressed) return true;
        return false;
    }

    static Key PressedKey()
    {
        // Extra mouse buttons (middle, Mouse 4, Mouse 5) count as keys too
        foreach (Key mk in GameInput.MouseKeys)
        {
            var mb = GameInput.MouseButton(mk);
            if (mb != null && mb.wasPressedThisFrame) return mk;
        }
        Keyboard kb = Keyboard.current;
        if (kb == null) return Key.None;
        foreach (KeyControl k in kb.allKeys)
            if (k != null && k.wasPressedThisFrame) return k.keyCode;
        return Key.None;
    }

    static bool PressedPadButton(out GamepadButton button)
    {
        button = GamepadButton.South;
        Gamepad gp = Gamepad.current;
        if (gp == null) return false;
        foreach (GamepadButton b in GameInput.BindablePadButtons)
            if (gp[b].wasPressedThisFrame) { button = b; return true; }
        return false;
    }

    void ResetDefaults()
    {
        GameInput.ResetToDefaults();
        RefreshLabels();
        hint.text = "Controls reset to defaults.";
    }

    void RefreshLabels()
    {
        foreach (var pair in keyLabels) pair.Value.text = GameInput.KeyName(GameInput.GetKey(pair.Key));
        foreach (var pair in padLabels) pair.Value.text = GameInput.PadButtonName(GameInput.GetPadButton(pair.Key));
    }

    static void Select(Selectable s)
    {
        if (s && EventSystem.current) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // ---------------- Building the UI ----------------

    void Build()
    {
        EnsureEventSystem();

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above every other UI
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Dark background over the whole screen
        root = NewUI("Root", transform);
        Stretch(root);
        root.AddComponent<Image>().color = DimColor;

        // ---- Main page ----
        mainPage = Page("Main", 520f);
        Text(mainPage.transform, "Paused", 64f, 90f);
        resumeButton = AddButton(mainPage.transform, "Resume", 380f, () => gm.Resume());
        AddButton(mainPage.transform, "Controls", 380f, ShowControls);
        AddButton(mainPage.transform, "Restart", 380f, () => gm.Restart());
        AddButton(mainPage.transform, "Main Menu", 380f, () => gm.MainMenu()); // back to the HOOKER title screen
        AddButton(mainPage.transform, "Quit", 380f, () => gm.Quit());

        // ---- Controls page ----
        controlsPage = Page("Controls", 1120f);
        controlsGroup = controlsPage.AddComponent<CanvasGroup>();
        Text(controlsPage.transform, "Controls", 52f, 70f);
        hint = Text(controlsPage.transform, DefaultHint, 24f, 40f);
        hint.color = FixedColor;

        Transform header = Row(controlsPage.transform, 40f);
        Text(header, "Action", 28f, 40f, 360f, TextAlignmentOptions.Left);
        Text(header, "Keyboard", 28f, 40f, 320f);
        Text(header, "Controller", 28f, 40f, 320f);

        foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
        {
            GameAction a = action; // copy for the button callbacks
            Transform row = Row(controlsPage.transform, 46f);
            Text(row, GameInput.ActionName(a), 28f, 46f, 360f, TextAlignmentOptions.Left);

            Button kb = AddButton(row, "", 320f, () => StartRebind(a, false), 46f);
            keyButtons[a] = kb;
            keyLabels[a] = kb.GetComponentInChildren<TextMeshProUGUI>();

            if (GameInput.HasPadBinding(a))
            {
                Button pad = AddButton(row, "", 320f, () => StartRebind(a, true), 46f);
                padButtons[a] = pad;
                padLabels[a] = pad.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                // Moving and aiming on a controller always use the sticks / D-pad
                string fixedText = a == GameAction.MoveLeft || a == GameAction.MoveRight
                    ? "Left Stick / D-pad" : "Sticks / D-pad";
                Text(row, fixedText, 26f, 46f, 320f).color = FixedColor;
            }
        }

        Transform bottom = Row(controlsPage.transform, 60f);
        AddButton(bottom, "Reset to Defaults", 360f, ResetDefaults);
        AddButton(bottom, "Back", 360f, Back);

        RefreshLabels();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current || FindAnyObjectByType<EventSystem>()) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // A centered box that sizes itself to its contents
    GameObject Page(string name, float width)
    {
        GameObject page = NewUI(name, root.transform);
        var rt = (RectTransform)page.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 100f);
        page.AddComponent<Image>().color = PanelColor;

        var layout = page.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 30, 30);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fit = page.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return page;
    }

    static Transform Row(Transform parent, float height)
    {
        GameObject row = NewUI("Row", parent);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        row.AddComponent<LayoutElement>().preferredHeight = height;
        return row.transform;
    }

    static TextMeshProUGUI Text(Transform parent, string text, float size, float height,
                                float width = -1f, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        GameObject go = NewUI("Text", parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        if (width > 0f) le.preferredWidth = width;
        return tmp;
    }

    static Button AddButton(Transform parent, string label, float width,
                            UnityEngine.Events.UnityAction onClick, float height = 60f)
    {
        GameObject go = NewUI("Button " + label, parent);
        var image = go.AddComponent<Image>();
        image.color = Color.white;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = SelectedColor;
        colors.selectedColor = SelectedColor;
        colors.pressedColor = Color.Lerp(SelectedColor, Color.white, 0.3f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        TextMeshProUGUI text = Text(go.transform, label, 30f, height);
        Stretch(text.gameObject);
        return button;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}