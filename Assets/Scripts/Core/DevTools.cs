using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dev tools for testing (only while DevSettings is on; turn it off for the final build):
//   F1  every boss in phase 1 jumps to phase 2; press again to kill them (beaten the normal way: powers unlock, Oscar runs off)
//   F2  heal to full
//   F3  god mode on / off (no damage)
//   F4  skip to the next scene in the Scene List
// A small "DEV" label in the top-right shows the keys. Nothing needs to be set up in scenes.
public class DevTools : MonoBehaviour
{
    static DevTools instance;
    bool god;
    TMP_Text label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!DevSettings.On || instance) return;
        var go = new GameObject("DevTools");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevTools>();
    }

    void Awake() => BuildLabel();

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame) NextBossStep();
        if (kb.f2Key.wasPressedThisFrame) { Health h = PlayerHealth(); if (h) h.Heal(h.MaxHealth); }
        if (kb.f3Key.wasPressedThisFrame) god = !god;
        if (kb.f4Key.wasPressedThisFrame) NextScene();

        if (god)
        {
            Health h = PlayerHealth();
            if (h) { h.MakeInvulnerable(0.2f); if (h.CurrentHealth < h.MaxHealth) h.Heal(h.MaxHealth); }
        }
        label.text = "DEV  F1 boss: phase 2 / kill  F2 heal  F3 god " + (god ? "<color=#7CFF7C>ON</color>" : "off") + "  F4 next scene";
    }

    static Health PlayerHealth()
    {
        PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
        return pc ? pc.GetComponent<Health>() : null;
    }

    // Phase 1 bosses drop to just under their phase 2 health (they switch the normal way); phase 2 bosses die
    static void NextBossStep()
    {
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>())
        {
            if (!(mb is IChainStunnable)) continue; // every boss (Hardy, Oscar, Madame O, KK)
            if (!mb.TryGetComponent(out Health h) || h.CurrentHealth <= 0) continue;
            System.Type t = mb.GetType();
            var phase2 = t.GetField("phase2", Any);
            var at = t.GetField("phase2At", Any) ?? t.GetField("phase2AtHealth", Any);
            bool inPhase2 = phase2 != null && (bool)phase2.GetValue(mb);
            if (!inPhase2 && at != null)
            {
                int target = Mathf.Max(1, Mathf.FloorToInt(h.MaxHealth * (float)at.GetValue(mb)) - 1);
                if (h.CurrentHealth > target) { h.TakeTickDamage(h.CurrentHealth - target); continue; }
            }
            h.TakeTickDamage(h.CurrentHealth + 99999);
        }
    }

    static void NextScene()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(next);
    }

    void BuildLabel()
    {
        var canvasGo = new GameObject("DevCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var go = new GameObject("DevLabel", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -14f);
        rt.sizeDelta = new Vector2(900f, 30f);
        label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.TopRight;
        label.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        label.raycastTarget = false;
    }
}
