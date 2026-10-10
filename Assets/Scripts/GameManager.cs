using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject pausePanel;

    bool paused;
    bool won;

    public bool IsPausedOrWon => paused || won;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (winPanel) winPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
    }

    void Update()
    {
        // Esc to pause
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void Win()
    {
        won = true;
        if (winPanel) winPanel.SetActive(true);
        Time.timeScale = 0f; // freeze the game
    }

    public void TogglePause()
    {
        if (won) return; // no pausing after winning
        paused = !paused;
        if (pausePanel) pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    // These functions are called by UI buttons
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
