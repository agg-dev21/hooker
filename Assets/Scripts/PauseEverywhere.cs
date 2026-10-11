using UnityEngine;
using UnityEngine.SceneManagement;

// Makes sure the pause menu (Esc / Start) works in every scene, any time.
// The pause menu lives on the GameManager; if a scene is loaded without one, this adds one automatically.
// Nothing needs to be set up: it runs by itself when the game starts.
public static class PauseEverywhere
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // never hook it twice
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureGameManager();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureGameManager();

    static void EnsureGameManager()
    {
        if (Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include)) return;
        new GameObject("GameManager (auto)").AddComponent<GameManager>();
    }
}
