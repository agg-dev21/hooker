using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fades the screen to black, loads a new scene, then fades back in from black.
// Nothing needs to be in the scene: just call ScreenFade.FadeToScene("SceneName").
public class ScreenFade : MonoBehaviour
{
    static ScreenFade instance;
    Image image;

    public static void FadeToScene(string sceneName, float fadeOutTime = 0.6f, float fadeInTime = 0.6f)
    {
        if (instance) return; // already fading

        var go = new GameObject("ScreenFade");
        DontDestroyOnLoad(go); // survives the scene change so it can fade in on the new scene
        instance = go.AddComponent<ScreenFade>();
        instance.StartCoroutine(instance.Run(sceneName, fadeOutTime, fadeInTime));
    }

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above all other UI

        var imgGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        image = imgGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(0f, 0f, 0f, 0f);
    }

    IEnumerator Run(string sceneName, float fadeOutTime, float fadeInTime)
    {
        yield return Fade(0f, 1f, fadeOutTime);
        SceneManager.LoadScene(sceneName);
        yield return null; // let the new scene load
        yield return Fade(1f, 0f, fadeInTime);

        instance = null;
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        image.color = new Color(0f, 0f, 0f, to);
    }
}
