using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tutorial text that always shows the player's current buttons.
// Write button names in curly brackets, like "{Jump} to jump": it shows the bound key (and the controller
// button when a controller is plugged in), and updates when keys are changed in the pause menu Controls.
// Names: {MoveLeft} {MoveRight} {Jump} {Dash} {Punch} {Hook} {AimUp} {AimDown} {AimLeft} {AimRight}
//        {Interact} {Pause} {Ability} {UseItem} {NextItem} {ChainAbility} {Companion}
// In the game the sign itself is hidden: when the player walks near it, its text shows in a box at the top
// middle of the screen (so it is never cut off by the camera and never overlaps the level).
[RequireComponent(typeof(TMP_Text))]
public class TutorialSign : MonoBehaviour
{
    [TextArea(3, 10)] public string template;
    public float showDistance = 7f;   // the player is this close (left / right): this sign's text shows

    static readonly List<TutorialSign> all = new List<TutorialSign>();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Start()
    {
        // Hide the sign in the level (the text and its dark board); the box at the top of the screen shows it instead
        if (TryGetComponent(out TMP_Text text) && !(text is TextMeshProUGUI)) text.enabled = false;
        if (transform.parent)
            foreach (Transform sibling in transform.parent)
                if (sibling.name == "SignBoard" && ((Vector2)(sibling.position - transform.position)).sqrMagnitude < 0.01f
                    && sibling.TryGetComponent(out SpriteRenderer sr))
                    sr.enabled = false;
        SignBox.Ensure();
    }

    // The sign closest to the player within its show distance (null if none)
    public static TutorialSign Nearest(Vector2 player)
    {
        TutorialSign best = null;
        float bestDistance = float.MaxValue;
        foreach (TutorialSign s in all)
        {
            float dx = Mathf.Abs(s.transform.position.x - player.x);
            if (dx > s.showDistance || dx >= bestDistance) continue;
            best = s;
            bestDistance = dx;
        }
        return best;
    }

    public static string Fill(string template)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var sb = new StringBuilder(template);
        bool pad = Gamepad.current != null;
        foreach (GameAction a in System.Enum.GetValues(typeof(GameAction)))
        {
            string token = "{" + a + "}";
            if (template.IndexOf(token, System.StringComparison.Ordinal) < 0) continue;
            string name = GameInput.KeyName(GameInput.GetKey(a)).ToUpper();
            if (pad && GameInput.HasPadBinding(a)) name += " / " + GameInput.PadButtonName(GameInput.GetPadButton(a));
            sb.Replace(token, "<color=#FFD54A>" + name + "</color>");
        }
        return sb.ToString();
    }

    // ---------------- The box at the top of the screen ----------------

    class SignBox : MonoBehaviour
    {
        static SignBox instance;
        CanvasGroup group;
        TMP_Text label;
        RectTransform panel;
        TutorialSign showing;
        float refresh;

        public static void Ensure()
        {
            if (instance) return;
            instance = new GameObject("TutorialSignBox").AddComponent<SignBox>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            // Top middle, below the corner labels and clear of the health bar on the left
            var p = new GameObject("Box", typeof(RectTransform), typeof(Image));
            p.transform.SetParent(transform, false);
            panel = (RectTransform)p.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -60f);
            panel.sizeDelta = new Vector2(820f, 120f);
            var img = p.GetComponent<Image>();
            img.color = new Color(0.04f, 0.03f, 0.07f, 0.82f);
            img.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform));
            t.transform.SetParent(panel, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(24f, 14f);
            trt.offsetMax = new Vector2(-24f, -14f);
            label = t.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.richText = true;
            label.raycastTarget = false;
        }

        void Update()
        {
            PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
            TutorialSign sign = pc ? Nearest(pc.transform.position) : null;
            if (sign && sign != showing) { showing = sign; refresh = 0f; }

            refresh -= Time.unscaledDeltaTime;
            if (showing && refresh <= 0f)
            {
                refresh = 0.5f; // picks up changed keys
                label.text = Fill(showing.template);
                label.ForceMeshUpdate();
                panel.sizeDelta = new Vector2(820f, Mathf.Max(70f, label.preferredHeight + 30f)); // grows with the text
            }
            group.alpha = Mathf.MoveTowards(group.alpha, sign ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (!sign && group.alpha <= 0f) showing = null;
        }
    }
}
