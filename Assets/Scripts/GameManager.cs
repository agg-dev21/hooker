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
        // Esc se pause
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void Win()
    {
        won = true;
        if (winPanel) winPanel.SetActive(true);
        Time.timeScale = 0f; // game ruk jaye
    }

    public void TogglePause()
    {
        if (won) return; // jeet gaye toh pause nahi
        paused = !paused;
        if (pausePanel) pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    // Ye functions buttons se call honge
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
