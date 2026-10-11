using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The title screen of HOOKER (scene MainMenu, the first scene of the game).
// Built in code with the game's own look: the pink-and-purple dock at sunset slowly panning behind,
// the purple sea rolling in front, dark purple panels, gold text and purple highlights (same as the pause menu).
//   Continue  - only if a game is in progress: goes to the next level of that game
//   New Game  - starts at the tutorial (asks first if it would wipe a game in progress)
//   Controls  - the same Controls page as the pause menu (change keys and controller buttons)
//   Quit
// Works with mouse, keyboard (arrows + Enter) and controller (stick / D-pad + A, B to go back).
public class TitleScreen : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] Texture2D background;  // Art/Ocean/dock_room.png
    [SerializeField] Texture2D sea;         // Art/Ocean/ocean.png (waves in its bottom half)
    [SerializeField] float panSpeed = 0.01f;   // how fast the background drifts sideways
    [SerializeField] float seaSpeed = 0.035f;  // how fast the waves in front roll

    [Header("Scenes")]
    [SerializeField] string newGameScene = "Tutorial_1_Move";

    // The game's palette (pause menu, inventory, dialogue)
    static readonly Color PanelColor = new Color(0.10f, 0.08f, 0.14f, 0.92f);
    static readonly Color ButtonColor = new Color(0.24f, 0.20f, 0.32f, 0.95f);
    static readonly Color SelectedColor = new Color(0.55f, 0.32f, 0.85f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
    static readonly Color DarkPurple = new Color(0.12f, 0.06f, 0.18f);
    static readonly Color Muted = new Color(0.72f, 0.69f, 0.78f);

    RawImage backImage, seaImage, fade;
    RectTransform title;
    GameObject menuPage, confirmPage;
    Button firstButton, confirmNo;
    readonly System.Collections.Generic.List<(Button button, TextMeshProUGUI marker)> markers =
        new System.Collections.Generic.List<(Button, TextMeshProUGUI)>();
    PauseMenu controls;
    float pan;
    int panDir = 1;
    bool leaving;

    void Start()
    {
        Time.timeScale = 1f;
        EnsureEventSystem();
        Build();
        StartCoroutine(FadeIn());
    }

    void Update()
    {
        // Background drifts slowly left and right; the waves keep rolling
        if (backImage && background)
        {
            float visible = Mathf.Min(1f, ((float)Screen.width / Screen.height) / ((float)background.width / background.height));
            pan += panDir * panSpeed * Time.unscaledDeltaTime;
            if (pan > 1f - visible) { pan = 1f - visible; panDir = -1; }
            if (pan < 0f) { pan = 0f; panDir = 1; }
            backImage.uvRect = new Rect(pan, 0f, visible, 1f);
        }
        if (seaImage)
        {
            Rect r = seaImage.uvRect;
            r.x = Mathf.Repeat(r.x + seaSpeed * Time.unscaledDeltaTime, 1f);
            seaImage.uvRect = r;
        }

        // Title gently bobs like a boat
        if (title) title.anchoredPosition = new Vector2(title.anchoredPosition.x, -150f + Mathf.Sin(Time.unscaledTime * 1.4f) * 6f);

        // Gold » next to the highlighted button
        GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var m in markers) if (m.marker) m.marker.enabled = selected == m.button.gameObject;

        // Nothing selected (e.g. after a mouse click on empty space): a key press selects again
        if (!leaving && selected == null && (controls == null || !controls.IsOpen) && AnyNavigation())
            Select(confirmPage.activeSelf ? confirmNo : firstButton);

        // Back out of the confirm page or the Controls page
        bool back = GameInput.PausePressed || (UnityEngine.InputSystem.Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);
        if (back && confirmPage.activeSelf) ShowMenu();
        else if (GameInput.PausePressed && controls && controls.InControls && !controls.IsListening) controls.Back();
    }

    // ---------------- Buttons ----------------

    void Continue() => Leave(RunOrder.NextLevel);

    void NewGame()
    {
        if (RunOrder.HasProgress) ShowConfirm();
        else Leave(newGameScene);
    }

    void OpenControls()
    {
        if (!controls) controls = PauseMenu.Create(null);
        menuPage.SetActive(false);
        controls.OpenControls(ShowMenu);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Leave(string scene)
    {
        if (leaving) return;
        leaving = true;
        menuPage.SetActive(false);
        confirmPage.SetActive(false);
        ScreenFade.FadeToScene(scene);
    }

    void ShowMenu()
    {
        menuPage.SetActive(true);
        confirmPage.SetActive(false);
        Select(firstButton);
    }

    void ShowConfirm()
    {
        menuPage.SetActive(false);
        confirmPage.SetActive(true);
        Select(confirmNo);
    }

    // ---------------- Building the screen ----------------

    void Build()
    {
        var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        // Sunset dock behind everything
        backImage = Raw("Background", root, background, Vector2.zero, Vector2.one);
        if (background) background.filterMode = FilterMode.Point;

        // Rolling purple waves along the bottom (the bottom half of ocean.png, repeated sideways)
        seaImage = Raw("Sea", root, sea, Vector2.zero, new Vector2(1f, 0.2f));
        if (sea)
        {
            sea.wrapMode = TextureWrapMode.Repeat;
            sea.filterMode = FilterMode.Point;
            seaImage.uvRect = new Rect(0f, 0f, 1.6f, 0.45f);
        }

        // Dark band on the left so the menu reads well over the bright sky
        Image band = Panel("Band", root, new Color(0.06f, 0.04f, 0.09f, 0.72f));
        Stretch(band.rectTransform, new Vector2(0f, 0f), new Vector2(0.40f, 1f));
        Image edge = Panel("BandEdge", root, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        Stretch(edge.rectTransform, new Vector2(0.40f, 0f), new Vector2(0.40f, 1f));
        edge.rectTransform.sizeDelta = new Vector2(4f, 0f);

        // Title
        title = Text("Title", root, "HOOKER", 140f, Gold, TextAlignmentOptions.Center).rectTransform;
        // Spans the dark band (with a margin) so it always fits on one line, whatever the screen size
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(0.4f, 1f);
        title.pivot = new Vector2(0.5f, 0.5f);
        title.sizeDelta = new Vector2(-60f, 220f);
        title.anchoredPosition = new Vector2(0f, -150f);
        var titleText = title.GetComponent<TextMeshProUGUI>();
        titleText.textWrappingMode = TextWrappingModes.NoWrap; // never split HOOKER over two lines
        titleText.enableAutoSizing = true;                     // shrink to fit if the band is narrow
        titleText.fontSizeMin = 48f;
        titleText.fontSizeMax = 140f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 12f;
        titleText.outlineWidth = 0.22f;
        titleText.outlineColor = DarkPurple;
        Image line = Panel("TitleLine", root, Gold);
        Anchor(line.rectTransform, new Vector2(0.2f, 1f), new Vector2(420f, 5f), new Vector2(0f, -268f));

        // Main buttons
        menuPage = new GameObject("Menu", typeof(RectTransform));
        menuPage.transform.SetParent(root, false);
        var menuRt = (RectTransform)menuPage.transform;
        Anchor(menuRt, new Vector2(0.2f, 0.5f), new Vector2(460f, 420f), new Vector2(0f, -40f));
        var layout = menuPage.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (RunOrder.HasProgress) firstButton = MenuButton(menuPage.transform, "Continue", Continue);
        Button newGame = MenuButton(menuPage.transform, "New Game", NewGame);
        if (!firstButton) firstButton = newGame;
        MenuButton(menuPage.transform, "Controls", OpenControls);
        MenuButton(menuPage.transform, "Quit", Quit);

        // "Start a new game?" confirmation
        confirmPage = new GameObject("Confirm", typeof(RectTransform), typeof(Image));
        confirmPage.transform.SetParent(root, false);
        confirmPage.GetComponent<Image>().color = PanelColor;
        Anchor((RectTransform)confirmPage.transform, new Vector2(0.2f, 0.5f), new Vector2(560f, 300f), new Vector2(0f, -40f));
        var cLayout = confirmPage.AddComponent<VerticalLayoutGroup>();
        cLayout.padding = new RectOffset(36, 36, 30, 30);
        cLayout.spacing = 16f;
        cLayout.childAlignment = TextAnchor.UpperCenter;
        cLayout.childControlWidth = cLayout.childControlHeight = true;
        cLayout.childForceExpandWidth = true;
        cLayout.childForceExpandHeight = false;
        Text("Question", confirmPage.transform, "Start a new game?", 40f, Gold, TextAlignmentOptions.Center).gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        Text("Warning", confirmPage.transform, "Your current game will be lost.", 28f, Muted, TextAlignmentOptions.Center).gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        MenuButton(confirmPage.transform, "Yes, start over", () => Leave(newGameScene));
        confirmNo = MenuButton(confirmPage.transform, "No", ShowMenu);
        confirmPage.SetActive(false);

        // How to use the menu
        TextMeshProUGUI help = Text("Help", root, "Arrows / Stick: choose     Enter / A: select     Esc / B: back", 22f, Muted, TextAlignmentOptions.Center);
        Anchor(help.rectTransform, new Vector2(0.2f, 0f), new Vector2(700f, 40f), new Vector2(0f, 40f));

        // Fade in from black
        fade = Raw("Fade", root, null, Vector2.zero, Vector2.one);
        fade.color = Color.black;
        fade.raycastTarget = false;

        Select(firstButton);
    }

    Button MenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 70f;
        var image = go.GetComponent<Image>();
        image.color = Color.white;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = SelectedColor;
        colors.selectedColor = SelectedColor;
        colors.pressedColor = Color.Lerp(SelectedColor, Color.white, 0.3f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = Text("Label", go.transform, label, 36f, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);

        // Gold » on the left when this button is highlighted
        TextMeshProUGUI marker = Text("Marker", go.transform, "\u00BB", 44f, Gold, TextAlignmentOptions.Left);
        Stretch(marker.rectTransform, Vector2.zero, Vector2.one);
        marker.margin = new Vector4(22f, 0f, 0f, 6f);
        marker.enabled = false;
        markers.Add((button, marker));

        // Hovering with the mouse highlights it the same way as the keyboard does
        var trigger = go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (!leaving) Select(button); });
        trigger.triggers.Add(enter);
        return button;
    }

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, 1f - t);
            yield return null;
        }
        fade.gameObject.SetActive(false);
    }

    // ---------------- Small helpers ----------------

    static bool AnyNavigation()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var gp = UnityEngine.InputSystem.Gamepad.current;
        bool keys = kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                                   kb.wKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
        bool pad = gp != null && (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                                  gp.leftStick.ReadValue().magnitude > 0.5f || gp.buttonSouth.wasPressedThisFrame);
        return keys || pad;
    }

    static void Select(Selectable s)
    {
        if (s && EventSystem.current) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current || FindAnyObjectByType<EventSystem>()) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static RawImage Raw(string name, Transform parent, Texture tex, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var raw = go.GetComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;
        Stretch(raw.rectTransform, min, max);
        if (!tex) raw.color = new Color(0.1f, 0.08f, 0.14f);
        return raw;
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

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}