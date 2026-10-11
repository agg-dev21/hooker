using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// After a level's boss is beaten: waits for a while (room for story / dialogue later),
// then asks the player if they want to go on. Pressing Interact (E / Y by default) fades to the next scene.
// Nothing needs to be in the scene: just call LevelEndPrompt.Show(...).
// The wait uses game time, so it pauses while the pause menu is open.
public class LevelEndPrompt : MonoBehaviour
{
    string nextScene;
    float delay;
    string message;
    GameObject panel;
    TMP_Text text;
    bool asking;

    public static void Show(string nextScene, float delay, string message)
    {
        var go = new GameObject("LevelEndPrompt");
        var prompt = go.AddComponent<LevelEndPrompt>();
        prompt.nextScene = nextScene;
        prompt.delay = delay;
        prompt.message = message;
    }

    void Start()
    {
        BuildUi();
        StartCoroutine(WaitThenAsk());
    }

    IEnumerator WaitThenAsk()
    {
        yield return new WaitForSeconds(delay);
        string key = GameInput.GetKey(GameAction.Interact).ToString();
        text.text = message + "\n<size=70%>Press " + key + " to continue</size>";
        panel.SetActive(true);
        asking = true;
    }

    void Update()
    {
        if (!asking || Time.timeScale == 0f) return;
        if (GameInput.Pressed(GameAction.Interact))
        {
            asking = false;
            panel.SetActive(false);
            ScreenFade.FadeToScene(nextScene);
        }
    }

    // A dark box in the middle of the screen with the question in it (hidden until it is time to ask)
    void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 220f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(30f, 20f);
        trt.offsetMax = new Vector2(-30f, -20f);
        text = textGo.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 48;
        text.color = Color.white;

        panel.SetActive(false);
    }
}
