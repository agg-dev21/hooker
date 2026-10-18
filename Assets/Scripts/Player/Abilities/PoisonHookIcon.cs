using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiKit; // Panel, Label (shared UI helpers)

// A small badge in the bottom-right corner (left of Ariel's icon) while Oscar's poison hook is yours.
// It is always on, so it shows "always on" instead of a key. Uses AbilityArt's poison icon; no icon = no badge.
// Adds itself to every scene, nothing to set up.
public class PoisonHookIcon : MonoBehaviour
{
    const float Size = 100f;
    static readonly Vector2 Margin = new Vector2(470f, 80f); // the icons to its right: Hardy 60, chain 210, Ariel 340

    GameObject iconRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (RunOrder.InTutorial || FindAnyObjectByType<PoisonHookIcon>()) return;
        if (!FindAnyObjectByType<PlayerController2D>()) return; // menus, the ending
        AbilityArt art = AbilityArt.Current;
        if (!art || !art.poisonIcon) return;
        new GameObject("PoisonHookIcon").AddComponent<PoisonHookIcon>();
    }

    void Start()
    {
        iconRoot = new GameObject("PoisonHookIconCanvas");
        iconRoot.transform.SetParent(transform, false);
        var canvas = iconRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = iconRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RectTransform frame = Panel("Frame", iconRoot.transform, Color.clear);
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        frame.anchoredPosition = new Vector2(-Margin.x, Margin.y);
        frame.sizeDelta = new Vector2(Size, Size);
        AbilityArt.Dress(frame, null, AbilityArt.Current.poisonIcon);
        Label("Key", frame, "always on", 18f, new Vector2(0f, -30f));
        iconRoot.SetActive(PlayerUpgrades.PoisonHook);
    }

    void Update()
    {
        if (iconRoot && iconRoot.activeSelf != PlayerUpgrades.PoisonHook) iconRoot.SetActive(PlayerUpgrades.PoisonHook);
    }
}
